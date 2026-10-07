using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 40: Season 12 (the Putt Club).</summary>
    public class Phase40SeasonTwelveTests
    {
        [Test]
        public void PuttClub_IsTheTwelfthRival_WithStoryBadgesAndCourts()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
            var team = c.Team(DefaultContent.Rival12CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.AreEqual(LogoMotif.Flag, team.logoMotif);
            Assert.IsNotNull(c.Court("court.hole_eighteen"));
            Assert.IsNotNull(c.Court("court.clubhouse_roof"));
            Assert.IsNotNull(c.Player(DefaultContent.Rival12LeaderId));
            Assert.IsNotNull(c.Find(c.Cosmetics, "cosmetic.jersey.fairway_green"));
            for (int season = 1; season <= 36; season++)
                Assert.AreEqual(season % 12 == 0, RivalEngine.RivalFor(season) == DefaultContent.Rival12CrewId, "season " + season);

            var d = Career.New(c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 12, currentWeek = 6 };
            Assert.AreEqual(Story.Rival12Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival12CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.puttWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d));
            Story.MarkSeen(d, Story.Rival12Intro);
            Assert.AreEqual(Story.Rival12Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.putt").Earned(d));
            d.rival.wins = 12;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = d.rival.keeperWins = d.rival.courierWins = d.rival.lanternWins = d.rival.royalWins = d.rival.washWins = d.rival.kiteWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllTwelve(d));
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).rival.puttWins);
            foreach (var id in new[] { Story.Rival12Intro, Story.Rival12Beaten })
            {
                Assert.Greater(Story.Beat(id, "ROOK", "en").Lines.Count, 1, id);
                Assert.Greater(Story.Beat(id, "ROOK", "es").Lines.Count, 1, id + " in Spanish");
            }
            Assert.IsTrue(Loc.Has(team.motto));
            Assert.IsTrue(Loc.Has(c.Court("court.hole_eighteen").description));
            Assert.IsTrue(Loc.Has(c.Court("court.clubhouse_roof").description));
        }

        [Test]
        public void FlagLogo_Draws_AndDiffersFromTheKite()
        {
            var team = DefaultContent.Create().Team(DefaultContent.Rival12CrewId);
            var a = LogoGenerator.Generate(team);
            Assert.Greater(a.OpaqueCount(), 50);
            team.logoMotif = LogoMotif.Kite;
            var b = LogoGenerator.Generate(team);
            int differ = 0;
            for (int i = 0; i < a.Pixels.Length; i++) if (!a.Pixels[i].Equals(b.Pixels[i])) differ++;
            Assert.Greater(differ, 10);
        }

        [Test]
        public void SeasonTwelvePass_AndWeekly_StartOnTheirDates()
        {
            Assert.AreEqual("cosmetic.pass.jersey.tailwind", HoopsPass.GearFor(HoopsPass.NineSetsFrom, 5), "Season 11's season is unchanged");
            Assert.AreEqual("cosmetic.pass.jersey.fairway", HoopsPass.GearFor(HoopsPass.TenSetsFrom, 5));
            Assert.AreEqual("cosmetic.pass.celebration.putt_drop", HoopsPass.GearFor(HoopsPass.TenSetsFrom, 20));
            Assert.AreEqual("cosmetic.pass.jersey.vapor_court", HoopsPass.GearFor(HoopsPass.TenSetsFrom + 1, 5), "then the ten sets rotate");
            Assert.AreEqual(HoopsPass.TenSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2027, 6, 28))));
            Assert.AreEqual(Weekly.Season12Week, Weekly.WeekOf(DailyChallenges.DayNumber(new System.DateTime(2027, 3, 29))));
            Assert.AreEqual(CelebrationKind.PuttDrop, Flair.CelebrationFor("cosmetic.pass.celebration.putt_drop"));
            bool arms = false, low = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
            {
                var p = Flair.Celebration(CelebrationKind.PuttDrop, t);
                arms |= p.ArmsUp;
                low |= p.Lift < 0;
            }
            Assert.IsTrue(arms && low);
            for (int week = Weekly.Season11Week; week < Weekly.Season12Week; week++)
                foreach (var g in Weekly.For(week)) Assert.AreNotEqual(WeeklyGoal.PlaymakerWins, g.Goal, "not before Season 12");
            bool seen = false;
            for (int week = Weekly.Season12Week; week < Weekly.Season12Week + 40 && !seen; week++)
                foreach (var g in Weekly.For(week)) seen |= g.Goal == WeeklyGoal.PlaymakerWins;
            Assert.IsTrue(seen, "the playmaker goal turns up");
            var goal = new WeeklyChallenge { Goal = WeeklyGoal.PlaymakerWins, Target = 2 };
            var good = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            good.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine { assists = 4 } });
            var few = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            few.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine { assists = 3 } });
            Assert.AreEqual(1, Weekly.Amount(goal, good, false));
            Assert.AreEqual(0, Weekly.Amount(goal, few, false));
        }
    }

    /// <summary>Phase 40: the HIGHLIGHTS reel and the result card.</summary>
    public class Phase40HighlightsTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        private static MatchSimulation Demo()
        {
            var r = MatchRequest.QuickCallDefault(C);
            r.Mode = GameMode.Demo;
            r.Seed = 77;
            return new MatchSimulation(MatchSetup.FromRequest(r, C));
        }

        private static void Run(MatchSimulation m, ReplayRecorder rec, float seconds)
        {
            for (int i = 0; i < (int)(seconds * 60); i++) { m.Step(1f / 60f, default); rec.Capture(m); }
        }

        [Test]
        public void Reel_KeepsTheTopThree_OnePerMoment_InOrder()
        {
            var m = Demo();
            var rec = new ReplayRecorder(m.Players.Length);
            Assert.IsFalse(rec.OfferBestPlay("EMPTY", 50), "nothing recorded yet");
            Run(m, rec, 3f);
            rec.OfferBestPlay("A", 40);
            Run(m, rec, 0.5f);
            rec.OfferBestPlay("A2", 60); // same moment as A: replaces it
            Assert.AreEqual(1, rec.Highlights.Count);
            Assert.AreEqual("A2", rec.Highlights[0].Label);
            Run(m, rec, 3f);
            rec.OfferBestPlay("B", 30);
            Run(m, rec, 3f);
            rec.OfferBestPlay("C", 90);
            Run(m, rec, 3f);
            rec.OfferBestPlay("D", 20); // worse than all three: dropped
            Assert.AreEqual(3, rec.Highlights.Count);
            Run(m, rec, 3f);
            rec.OfferBestPlay("E", 70); // pushes out the worst (B)
            var labels = new List<string>();
            foreach (var h in rec.Highlights) labels.Add(h.Label);
            Assert.AreEqual("A2,C,E", string.Join(",", labels), "top three, oldest first");
            Assert.AreEqual("C", rec.BestPlay.Label);
            for (int i = 1; i < rec.Highlights.Count; i++)
                Assert.Greater(rec.Highlights[i].Frames[0].Time, rec.Highlights[i - 1].Frames[0].Time);
            rec.Reset();
            Assert.AreEqual(0, rec.Highlights.Count);
        }

        [Test]
        public void ResultCard_Draws()
        {
            var m = Demo();
            for (int i = 0; i < 60 * 30; i++) m.Step(1f / 60f, default);
            var s = MatchSummary.From(m, GameMode.QuickCall, "card");
            var card = ResultCard.Render(s, "EASTBAY VOLTAGE WIN");
            Assert.AreEqual(ResultCard.Width * ResultCard.Upscaled, card.Width);
            Assert.AreEqual(ResultCard.Height * ResultCard.Upscaled, card.Height);
            Assert.Greater(card.OpaqueCount(), card.Width * card.Height / 2, "a solid card");
            Assert.AreEqual(ResultCard.Width * ResultCard.Upscaled, ResultCard.Render(null, null).Width, "never throws");
        }
    }

    /// <summary>Phase 40: smarter AI (in-game scouting) and SMART DIFFICULTY.</summary>
    public class Phase40SmarterAiTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        [Test]
        public void Scouting_ReadsShootersAndDrivers_OnlyForPeople()
        {
            var m = new MatchSimulation(MatchSetup.FromRequest(MatchRequest.QuickCallDefault(C), C));
            int you = m.ControlledIndex;
            var line = m.Stats.players[you];
            line.fieldGoalsAttempted = 4; line.arcAttempted = 4;
            Assert.AreEqual(ScoutedStyle.None, m.ScoutOf(you), "not enough shots yet");
            line.fieldGoalsAttempted = 6; line.arcAttempted = 4;
            Assert.AreEqual(ScoutedStyle.Shooter, m.ScoutOf(you));
            line.arcAttempted = 1;
            Assert.AreEqual(ScoutedStyle.Driver, m.ScoutOf(you));
            line.arcAttempted = 2;
            Assert.AreEqual(ScoutedStyle.None, m.ScoutOf(you), "a mix: no read");
            int ai = m.Index(1, 0);
            m.Stats.players[ai].fieldGoalsAttempted = 10;
            m.Stats.players[ai].arcAttempted = 10;
            Assert.AreEqual(ScoutedStyle.None, m.ScoutOf(ai), "the AI isn't scouted");
        }

        [Test]
        public void Scouting_ChangesTheDefence_ButStaysDeterministic()
        {
            // The same scripted possession twice gives the same game (no random draws from scouting).
            var r = MatchRequest.QuickCallDefault(C);
            r.Seed = 31;
            MatchSimulation Play()
            {
                var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
                for (int i = 0; i < 60 * 60; i++)
                    m.Step(1f / 60f, new PlayerInput { Move = new Vec2(i % 200 < 100 ? 0.6f : -0.6f, 0.3f), ShootPressed = i % 90 == 0, ShootHeld = i % 90 < 40 });
                return m;
            }
            var a = Play();
            var b = Play();
            Assert.AreEqual(a.Score[0], b.Score[0]);
            Assert.AreEqual(a.Score[1], b.Score[1]);
            Assert.AreEqual(a.Players[0].Position.x, b.Players[0].Position.x, 1e-6f);
        }

        [Test]
        public void SmartDifficulty_NudgesOnlyAfterAClearRun()
        {
            Assert.AreEqual(0f, Adaptive.EdgeFor(null));
            Assert.AreEqual(0f, Adaptive.EdgeFor(new List<int> { -12, -12 }), "needs three games");
            Assert.AreEqual(0f, Adaptive.EdgeFor(new List<int> { -12, 3, -12 }), "a win in the middle: no change");
            Assert.AreEqual(-Adaptive.MaxEdge, Adaptive.EdgeFor(new List<int> { -12, -10, -11 }), 1e-6f);
            Assert.AreEqual(-Adaptive.MaxEdge * 0.6f, Adaptive.EdgeFor(new List<int> { -6, -7, -8 }), 1e-6f);
            Assert.AreEqual(Adaptive.MaxEdge, Adaptive.EdgeFor(new List<int> { 12, 15, 10 }), 1e-6f);
            Assert.AreEqual(0f, Adaptive.EdgeFor(new List<int> { 5, 5, 5 }), "close wins don't count");
            var rise = new RiseSaveData();
            foreach (int x in new[] { 1, 2, 3, 4, 5 }) Adaptive.Record(rise, x);
            Assert.AreEqual("3,4,5", string.Join(",", rise.recentMargins));
            var d = new CareerSaveData();
            d.rise.recentMargins = new List<int> { -9, -10, -11 };
            d.settings.smartDifficulty = false;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual("-9,-10,-11", string.Join(",", back.rise.recentMargins));
            Assert.IsFalse(back.settings.smartDifficulty);
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), C, out _).settings.smartDifficulty, "on by default");
            var req = MatchRequest.QuickCallDefault(C);
            req.AiEdge = 0.9f;
            Assert.AreEqual(Adaptive.MaxEdge, MatchSetup.FromRequest(req, C).AiEdge, 1e-6f, "clamped");
        }
    }
}

