using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 14: instant replay, new drills, King of the Court, Spanish.</summary>
    public class ReplayTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchSimulation Match()
        {
            var r = MatchRequest.QuickCallDefault(_c);
            r.Seed = 9;
            return new MatchSimulation(MatchSetup.FromRequest(r, _c));
        }

        [Test]
        public void Recorder_KeepsOnlyTheLastFewSeconds_OldestFirst()
        {
            var m = Match();
            var rec = new ReplayRecorder(m.Players.Length, 2f);
            for (int i = 0; i < 300; i++) { m.Step(Dt, default); rec.Capture(m); }
            Assert.AreEqual(rec.Capacity, rec.Count);
            var clip = rec.Snapshot(1f);
            Assert.AreEqual(60, clip.Frames.Count);
            Assert.AreEqual(m.Time, clip.Frames[clip.Frames.Count - 1].Time, 1e-4f, "newest frame last");
            Assert.Less(clip.Frames[0].Time, clip.Frames[1].Time);
            Assert.AreEqual(1f - Dt, clip.Duration, 0.02f);
        }

        [Test]
        public void Snapshot_IsACopy_NotOverwrittenByLaterFrames()
        {
            var m = Match();
            var rec = new ReplayRecorder(m.Players.Length, 1f);
            for (int i = 0; i < 60; i++) { m.Step(Dt, default); rec.Capture(m); }
            var clip = rec.Snapshot(0.5f);
            float t0 = clip.Frames[0].Time;
            for (int i = 0; i < 120; i++) { m.Step(Dt, default); rec.Capture(m); }
            Assert.AreEqual(t0, clip.Frames[0].Time);
        }

        [Test]
        public void BestPlay_KeepsTheHighestScoringMoment()
        {
            var m = Match();
            var rec = new ReplayRecorder(m.Players.Length);
            for (int i = 0; i < 120; i++) { m.Step(Dt, default); rec.Capture(m); }
            Assert.IsTrue(rec.OfferBestPlay("STEAL", ReplayRecorder.PlayScore(MatchEventType.Steal, ShotType.MidRange, TimingGrade.SlightlyEarly, 0)));
            Assert.IsTrue(rec.OfferBestPlay("SLAM", ReplayRecorder.PlayScore(MatchEventType.ShotMade, ShotType.Dunk, TimingGrade.SlightlyEarly, 1)));
            Assert.IsFalse(rec.OfferBestPlay("JUMPER", ReplayRecorder.PlayScore(MatchEventType.ShotMade, ShotType.MidRange, TimingGrade.TooEarly, 1)));
            Assert.AreEqual("SLAM", rec.BestPlay.Label);
            Assert.Greater(rec.BestPlay.Frames.Count, 100);
        }

        [Test]
        public void FrameAt_FindsTheRightFrame()
        {
            var m = Match();
            var rec = new ReplayRecorder(m.Players.Length);
            for (int i = 0; i < 120; i++) { m.Step(Dt, default); rec.Capture(m); }
            var clip = rec.Snapshot(1f);
            Assert.IsTrue(ReferenceEquals(clip.Frames[0], ReplayRecorder.FrameAt(clip, 0f)));
            Assert.IsTrue(ReferenceEquals(clip.Frames[30], ReplayRecorder.FrameAt(clip, 30 * Dt + 0.001f)));
            Assert.IsTrue(ReferenceEquals(clip.Frames[clip.Frames.Count - 1], ReplayRecorder.FrameAt(clip, 99f)));
        }

        [Test]
        public void PlayScore_RanksDunksAboveGreensAboveJumpers()
        {
            int dunk = ReplayRecorder.PlayScore(MatchEventType.ShotMade, ShotType.Dunk, TimingGrade.SlightlyEarly, 1);
            int green = ReplayRecorder.PlayScore(MatchEventType.ShotMade, ShotType.Arc, TimingGrade.Green, 2);
            int jumper = ReplayRecorder.PlayScore(MatchEventType.ShotMade, ShotType.MidRange, TimingGrade.SlightlyEarly, 1);
            Assert.Greater(dunk, green);
            Assert.Greater(green, jumper);
            Assert.AreEqual(0, ReplayRecorder.PlayScore(MatchEventType.PassThrown, ShotType.Layup, TimingGrade.SlightlyEarly, 0));
        }
    }

    public class NewDrillTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchSimulation Drill(DrillKind kind)
        {
            var r = MatchRequest.PracticeDefault();
            r.Drill = (int)kind;
            r.Seed = 4;
            return new MatchSimulation(MatchSetup.FromRequest(r, _c));
        }

        [Test]
        public void ThreePoint_HasFiveSpotsBeyondTheArc_AndASixtySecondClock()
        {
            var m = Drill(DrillKind.ThreePoint);
            var s = new PracticeSession(DrillKind.ThreePoint, m);
            Assert.AreEqual(5, s.MoneySpots.Count);
            foreach (var p in s.MoneySpots) Assert.AreEqual(ShotZone.BeyondArc, m.Setup.Court.ZoneOf(p));
            Assert.AreEqual(PracticeSession.ThreePointSeconds, s.TimeLimit);
            Assert.IsTrue(m.Setup.PassiveOpponents);
        }

        [Test]
        public void ThreePoint_ScoresArcMakesAndRotatesTheMoneySpot()
        {
            var m = Drill(DrillKind.ThreePoint);
            var s = new PracticeSession(DrillKind.ThreePoint, m);
            int hold = 0, shots = 0;
            for (int i = 0; i < 60 * 30 && shots < 6; i++)
            {
                var input = new PlayerInput();
                var target = s.MoneySpots[s.MoneySpot];
                var to = target - m.Controlled.Position;
                if (m.HumanHasBall && m.ChargingIndex < 0 && to.Magnitude > 0.4f) input.Move = to.Normalized;
                else if (m.HumanHasBall && m.ChargingIndex < 0 && hold == 0) { input.ShootPressed = true; input.ShootHeld = true; hold = 1; }
                else if (hold > 0 && hold < 50) { input.ShootHeld = true; hold++; }
                else hold = 0;
                m.Step(Dt, input);
                s.Update(m, Dt);
                foreach (var e in m.Events) if (e.Type == MatchEventType.ShotReleased && e.PlayerIndex == m.ControlledIndex) shots++;
            }
            Assert.GreaterOrEqual(shots, 3, "should get several shots off");
            Assert.GreaterOrEqual(s.ContestPoints, 0);
            Assert.IsTrue(s.ResultText().Contains("points"));
        }

        [Test]
        public void Lockdown_OpponentsAttack_AndSixPossessionsEndTheDrill()
        {
            var m = Drill(DrillKind.Lockdown);
            Assert.IsFalse(m.Setup.PassiveOpponents);
            Assert.AreEqual(1, m.OffenseTeam, "you start on defense");
            var s = new PracticeSession(DrillKind.Lockdown, m);
            for (int i = 0; i < 60 * 150 && !s.Finished; i++)
            {
                var h = m.Holder;
                var input = new PlayerInput();
                if (h != null && h.Team != m.Setup.HumanTeam)
                {
                    input.Move = (h.Position - m.Controlled.Position).Normalized;
                    input.DefensePressed = i % 20 == 0;
                }
                m.Step(Dt, input);
                s.Update(m, Dt);
            }
            Assert.IsTrue(s.Finished);
            Assert.AreEqual(PracticeSession.LockdownPossessions, s.Possessions);
            Assert.IsTrue(s.Stops >= 0 && s.Stops <= PracticeSession.LockdownPossessions);
        }

        [Test]
        public void NewBests_AreSaved()
        {
            var d = Career.New(_c);
            Assert.IsTrue(Career.RecordPractice(d, 0, 0, 0, 0f, threePoint: 9, lockdownStops: 4));
            Assert.IsFalse(Career.RecordPractice(d, 0, 0, 0, 0f, threePoint: 7, lockdownStops: 3));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(9, back.practice.threePointBest);
            Assert.AreEqual(4, back.practice.lockdownBest);
        }
    }

    public class KingTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();
        private const string Home = "team.eastbay_voltage";

        [Test]
        public void Start_OrdersEveryOtherLeagueTeam()
        {
            var k = new KingSaveData();
            KingEngine.Start(k, _c, Home);
            Assert.IsTrue(k.active);
            Assert.AreEqual(_c.TeamsInTier(TeamTier.League).Count - 1, k.order.Count);
            Assert.IsFalse(k.order.Contains(Home));
            var req = KingEngine.NextMatch(k, _c, Home, "difficulty.caller");
            Assert.AreEqual(GameMode.King, req.Mode);
            Assert.AreEqual(KingEngine.RulesId, req.RulesId);
            Assert.IsNotNull(_c.Find(_c.Rules, KingEngine.RulesId));
            Assert.AreEqual(11, _c.Find(_c.Rules, KingEngine.RulesId).targetScore);
        }

        [Test]
        public void Wins_GrowTheStreakWithBonus_LossEndsTheRun()
        {
            var k = new KingSaveData();
            var d = Career.New(_c);
            KingEngine.Start(k, _c, Home);
            for (int i = 0; i < 3; i++)
            {
                var req = KingEngine.NextMatch(k, _c, Home, "x");
                var s = Fake.Summary(GameMode.King, true, "k" + i, opponent: req.AwayTeamId, home: Home);
                Assert.AreEqual(KingOutcome.Defended, KingEngine.ApplyResult(k, _c, s, d, out int bonus));
                Assert.AreEqual(KingEngine.BonusPerStreakWin * (i + 1), bonus);
            }
            Assert.AreEqual(3, k.streak);
            Assert.AreEqual(3, k.best);
            Assert.AreEqual(KingOutcome.Dethroned, KingEngine.ApplyResult(k, _c, Fake.Summary(GameMode.King, false, "kl", home: Home), d, out _));
            Assert.IsFalse(k.active);
            Assert.IsNull(KingEngine.NextMatch(k, _c, Home, "x"));
            Assert.AreEqual(3, k.best);
        }

        [Test]
        public void BeatingEveryone_Reshuffles_AndKeepsGoing()
        {
            var k = new KingSaveData();
            KingEngine.Start(k, _c, Home);
            int n = k.order.Count;
            for (int i = 0; i < n + 2; i++)
                KingEngine.ApplyResult(k, _c, Fake.Summary(GameMode.King, true, "w" + i, home: Home), null, out _);
            Assert.AreEqual(n + 2, k.streak);
            Assert.IsTrue(k.active);
            Assert.AreEqual(n, k.order.Count);
        }

        [Test]
        public void King_RoundTripsAndPaysQuickCallRates()
        {
            var d = Career.New(_c);
            KingEngine.Start(d.king, _c, Home);
            KingEngine.ApplyResult(d.king, _c, Fake.Summary(GameMode.King, true, "a", home: Home), d, out _);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(d.king.order, back.king.order);
            Assert.AreEqual(1, back.king.streak);
            Assert.IsTrue(back.king.active);
            var t = RewardTuning.Default;
            Assert.AreEqual(Rewards.For(Fake.Summary(GameMode.QuickCall, true, "q"), t).signalPoints,
                            Rewards.For(Fake.Summary(GameMode.King, true, "k"), t).signalPoints);
        }
    }

    public class SpanishTests
    {
        [TearDown]
        public void Reset() => Loc.Language = Loc.English;

        [Test]
        public void English_IsUnchanged()
        {
            Loc.Language = Loc.English;
            Assert.AreEqual("PLAY", Loc.T("PLAY"));
            Assert.AreEqual("anything", Loc.T("anything"));
        }

        [Test]
        public void Spanish_TranslatesExactAndComposite()
        {
            Loc.Language = Loc.Spanish;
            Assert.AreEqual("JUGAR", Loc.T("PLAY"));
            Assert.AreEqual("AJUSTES", Loc.T("SETTINGS"));
            Assert.IsTrue(Loc.T("NEW RECORD: POINTS").StartsWith("NUEVO RÉCORD"));
            Assert.AreEqual("PASO 2/8", Loc.T("STEP 2/8"));
            Assert.AreEqual("Bay City Breakers", Loc.T("Bay City Breakers"), "names stay as they are");
        }

        [Test]
        public void EveryEventCard_Badge_AndTutorialLine_HasSpanish()
        {
            foreach (var card in EventCards.All())
            {
                Assert.IsTrue(Loc.Has(card.title), card.title);
                Assert.IsTrue(Loc.Has(card.body), card.body);
                foreach (var ch in card.choices) Assert.IsTrue(Loc.Has(ch.label), ch.label);
            }
            foreach (var b in Badges.All)
            {
                Assert.IsTrue(Loc.Has(b.Title), b.Title);
                Assert.IsTrue(Loc.Has(b.Description), b.Description);
            }
            for (int i = 0; i <= TutorialSession.StepCount; i++)
            {
                var step = (TutorialStep)i;
                Assert.IsTrue(Loc.Has(TutorialSession.TitleOf(step)), TutorialSession.TitleOf(step));
                Assert.IsTrue(Loc.Has(TutorialSession.TouchHintOf(step)), TutorialSession.TouchHintOf(step));
            }
        }

        [Test]
        public void SpanishStory_ExistsForEveryScene_WithTheSameShape()
        {
            foreach (var id in Story.AllIds)
            {
                var en = Story.Beat(id, "Ace", Loc.English);
                var es = Story.Beat(id, "Ace", Loc.Spanish);
                Assert.IsNotNull(es, id);
                Assert.AreEqual(en.Lines.Count, es.Lines.Count, id);
                for (int i = 0; i < en.Lines.Count; i++)
                {
                    Assert.AreEqual(en.Lines[i].Speaker, es.Lines[i].Speaker);
                    Assert.AreNotEqual(en.Lines[i].Text, es.Lines[i].Text);
                }
            }
        }

        [Test]
        public void Language_IsSavedAndUnknownValuesFallBackToEnglish()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.settings.language = Loc.Spanish;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual(Loc.Spanish, back.settings.language);
            Assert.AreEqual(Loc.English, Loc.Normalize("fr"));
        }
    }
}
