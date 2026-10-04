using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 10: new courts, crews, cosmetics, event cards, and the First Call Classic.</summary>
    public class ContentPhase10Tests
    {
        private ContentCatalog _c;

        [SetUp]
        public void SetUp() => _c = DefaultContent.Create();

        [Test]
        public void Content_StillValidates()
        {
            var report = ContentValidator.Validate(_c);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void Circuit_HasFiveCrewsInOrder_EachWithItsOwnCourt()
        {
            Assert.AreEqual(5, RiseEngine.CircuitOrder.Length);
            var courts = new HashSet<string>();
            foreach (var id in RiseEngine.CircuitOrder)
            {
                var t = _c.Team(id);
                Assert.IsNotNull(t, id);
                Assert.AreEqual(TeamTier.Circuit, t.tier);
                Assert.IsNotNull(_c.Court(t.homeCourtId), t.homeCourtId);
                Assert.IsTrue(courts.Add(t.homeCourtId), "one court per crew");
            }
        }

        [Test]
        public void Circuit_BeatingAllFiveEntersTheLeague()
        {
            var r = new RiseSaveData();
            var career = Career.New(_c);
            for (int i = 0; i < RiseEngine.CircuitOrder.Length; i++)
            {
                var req = RiseEngine.NextMatch(r, _c, DefaultContent.DefaultDifficultyId);
                Assert.AreEqual(RiseEngine.CircuitOrder[i], req.AwayTeamId);
                var outcome = RiseEngine.ApplyResult(r, _c, Summary(GameMode.Rise, req.AwayTeamId, 21, 10), career);
                Assert.AreEqual(i < RiseEngine.CircuitOrder.Length - 1 ? RiseOutcome.CircuitWin : RiseOutcome.EnteredLeague, outcome);
            }
            Assert.AreEqual(RiseStage.Season, r.stage);
        }

        [Test]
        public void EventCards_AtLeastTen_UniqueIds()
        {
            var all = EventCards.All();
            Assert.GreaterOrEqual(all.Count, 10);
            var ids = new HashSet<string>();
            foreach (var e in all) Assert.IsTrue(ids.Add(e.id), e.id);
        }

        [Test]
        public void NewCosmetics_ExistAndMapToFlair()
        {
            foreach (var id in new[] { "cosmetic.jersey.arcade_mint", "cosmetic.jersey.gold_rush", "cosmetic.shoes.glacier",
                                       "cosmetic.shoes.cosmic", "cosmetic.banner.boardwalk", "cosmetic.celebration.raise_roof",
                                       "cosmetic.move.behind_back" })
                Assert.IsNotNull(_c.Find(_c.Cosmetics, id), id);
            Assert.AreEqual(CelebrationKind.RaiseTheRoof, Flair.CelebrationFor("cosmetic.celebration.raise_roof"));
            Assert.AreEqual(DribbleMoveKind.BehindTheBack, Flair.DribbleMoveFor("cosmetic.move.behind_back"));
        }

        // ------------------------------------------------------------------ First Call Classic

        [Test]
        public void Classic_Start_BuildsFourTeamBracket_CrewIsFourthSeed()
        {
            var t = new ClassicSaveData();
            ClassicEngine.Start(t, _c);
            Assert.IsTrue(t.Active);
            Assert.AreEqual(4, t.seeds.Count);
            Assert.AreEqual(DefaultContent.PlayerCrewId, t.seeds[3]);
            Assert.AreEqual(4, new HashSet<string>(t.seeds).Count);
            var req = ClassicEngine.NextMatch(t, _c, DefaultContent.DefaultDifficultyId);
            Assert.AreEqual(GameMode.Tournament, req.Mode);
            Assert.AreEqual(t.seeds[0], req.AwayTeamId, "crew plays the top seed in the semi");
            Assert.AreEqual(1, req.Round);
        }

        [Test]
        public void Classic_WinTwice_IsChampion()
        {
            var t = new ClassicSaveData();
            ClassicEngine.Start(t, _c);
            var semi = ClassicEngine.NextMatch(t, _c, "x");
            Assert.AreEqual(ClassicOutcome.WonSemi, ClassicEngine.ApplyResult(t, _c, Summary(GameMode.Tournament, semi.AwayTeamId, 21, 15)));
            var final = ClassicEngine.NextMatch(t, _c, "x");
            Assert.AreEqual(2, final.Round);
            Assert.AreNotEqual(semi.ContextId, final.ContextId);
            Assert.AreEqual(ClassicOutcome.Champion, ClassicEngine.ApplyResult(t, _c, Summary(GameMode.Tournament, final.AwayTeamId, 21, 19)));
            Assert.IsFalse(t.Active);
            Assert.AreEqual(1, t.titles);
            Assert.AreEqual(DefaultContent.PlayerCrewId, t.championId);
            Assert.IsNull(ClassicEngine.NextMatch(t, _c, "x"));
        }

        [Test]
        public void Classic_LoseSemi_IsEliminated_AndStillCrownsSomeone()
        {
            var t = new ClassicSaveData();
            ClassicEngine.Start(t, _c);
            var semi = ClassicEngine.NextMatch(t, _c, "x");
            Assert.AreEqual(ClassicOutcome.Eliminated, ClassicEngine.ApplyResult(t, _c, Summary(GameMode.Tournament, semi.AwayTeamId, 10, 21)));
            Assert.IsFalse(t.Active);
            Assert.IsNotNull(t.championId);
            Assert.AreNotEqual(DefaultContent.PlayerCrewId, t.championId);
            Assert.AreEqual(0, t.titles);
        }

        [Test]
        public void Classic_NewEdition_DrawsAgainAndKeepsTitles()
        {
            var t = new ClassicSaveData { titles = 2 };
            ClassicEngine.Start(t, _c);
            var first = new List<string>(t.seeds);
            ClassicEngine.Start(t, _c);
            Assert.AreEqual(2, t.edition);
            Assert.AreEqual(2, t.titles);
            Assert.AreEqual(4, t.seeds.Count);
            Assert.AreEqual(2, t.games.Count, "fresh semis only");
            Assert.IsTrue(first != null);
        }

        [Test]
        public void Classic_RoundTripsThroughTheSaveFile()
        {
            var d = Career.New(_c);
            ClassicEngine.Start(d.classic, _c);
            var semi = ClassicEngine.NextMatch(d.classic, _c, "x");
            ClassicEngine.ApplyResult(d.classic, _c, Summary(GameMode.Tournament, semi.AwayTeamId, 21, 12));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(d.classic.seeds, back.classic.seeds);
            Assert.AreEqual(d.classic.games.Count, back.classic.games.Count);
            Assert.AreEqual(d.classic.edition, back.classic.edition);
            Assert.IsTrue(back.classic.Active);
        }

        [Test]
        public void Classic_TitleBonusIsSmallerThanTheCup()
        {
            var t = RewardTuning.Default;
            var cupFinal = Summary(GameMode.Rise, "team.metro_comets", 21, 10);
            cupFinal.isPlayoff = cupFinal.isFinal = true;
            var classicFinal = Summary(GameMode.Tournament, "team.metro_comets", 21, 10);
            classicFinal.isFinal = true;
            Assert.Less(Rewards.For(classicFinal, t).signalPoints, Rewards.For(cupFinal, t).signalPoints);
            var classicSemi = Summary(GameMode.Tournament, "team.metro_comets", 21, 10);
            Assert.Greater(Rewards.For(classicFinal, t).signalPoints, Rewards.For(classicSemi, t).signalPoints);
        }

        private static MatchSummary Summary(GameMode mode, string opponent, int crew, int opp)
        {
            var s = new MatchSummary
            {
                matchId = System.Guid.NewGuid().ToString("N"),
                mode = mode,
                teamAId = DefaultContent.PlayerCrewId,
                teamBId = opponent,
                teamAName = "First Callers",
                teamBName = opponent,
                scoreA = crew,
                scoreB = opp,
                humanTeam = 0,
                winner = crew > opp ? 0 : 1,
            };
            return s;
        }
    }
}
