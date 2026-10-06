using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 33: Franchise trade offers, training focus and development report; the playoff bracket.</summary>
    public class Phase33FranchiseTests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            _f = Franchise.Create(_c, league[2].id, 77);
            if (_f.phase == FranchisePhase.Preseason) Assert.IsTrue(Franchise.StartSeason(_f, _c), Franchise.CannotStart(_f));
        }

        [Test]
        public void GMsCall_WithDealsTheyWouldReallyMake_OnlyBeforeTheDeadline()
        {
            FrOffer first = null;
            int offers = 0, lastWeek = -1;
            for (int guard = 0; guard < 40 && _f.phase == FranchisePhase.Regular; guard++)
            {
                Franchise.SimNext(_f, _c);
                if (_f.offer != null && _f.offer.week != lastWeek)
                {
                    offers++;
                    lastWeek = _f.offer.week;
                    first = first ?? _f.offer;
                    Assert.Less(_f.offer.week, Franchise.TradeDeadlineWeek, "no calls after the deadline");
                    Assert.IsTrue(Franchise.Evaluate(_f, _f.offer.team, _f.offer.want, _f.offer.send).Accepted, "the other GM would make it");
                    Assert.IsNotEmpty(_f.offer.pitch);
                    var best = Franchise.Roster(_f, _f.you);
                    best.Sort((x, y) => Franchise.Value(y).CompareTo(Franchise.Value(x)));
                    Assert.AreNotEqual(best[0].id, _f.offer.want[0], "they never ask for your best player");
                }
                if (!Franchise.TradesOpen(_f)) Assert.IsNull(_f.offer, "offers lapse at the deadline");
            }
            Assert.Greater(offers, 0, "a GM calls at least once before the deadline");
        }

        [Test]
        public void AcceptingAnOffer_SwapsThePlayers()
        {
            FrOffer o = null;
            for (int guard = 0; guard < 20 && o == null && Franchise.TradesOpen(_f); guard++)
            {
                Franchise.SimNext(_f, _c);
                o = _f.offer;
            }
            Assert.IsNotNull(o, "an offer came in");
            int mine = o.want[0], theirs = o.send[0];
            Assert.IsTrue(FranchiseDepth.Accept(_f));
            Assert.IsNull(_f.offer);
            Assert.AreEqual(o.team, Franchise.Player(_f, mine).team);
            Assert.AreEqual(_f.you, Franchise.Player(_f, theirs).team);
            Assert.IsTrue(_f.moves.Exists(m => m.text.StartsWith("TRADE:")));
            Assert.IsFalse(FranchiseDepth.Accept(_f), "nothing left to accept");
        }

        [Test]
        public void TrainingFocus_GrowsYoungPlayers_InTheFocus()
        {
            var p = new FrPlayer { age = 22, potential = 90, attrs = new AttributeSet(50, 50, 50, 50, 50, 50, 50, 50) };
            var a = FranchiseDepth.ApplyFocus(p, TrainingFocus.Shooting);
            Assert.AreEqual(52, a.shooting);
            Assert.AreEqual(52, a.finishing);
            Assert.AreEqual(50, a.defense);
            Assert.AreEqual(50, FranchiseDepth.ApplyFocus(p, TrainingFocus.Balanced).shooting, "balanced: no extra");
            p.age = 30;
            Assert.AreEqual(50, FranchiseDepth.ApplyFocus(p, TrainingFocus.Shooting).shooting, "veterans don't");
            p.age = 22; p.potential = 50;
            Assert.AreEqual(50, FranchiseDepth.ApplyFocus(p, TrainingFocus.Shooting).shooting, "not past potential");
        }

        [Test]
        public void OffSeason_WritesADevelopmentReport_AndEverythingSaves()
        {
            _f.focus = (int)TrainingFocus.Defense;
            for (int guard = 0; guard < 80 && _f.phase != FranchisePhase.Draft; guard++) Franchise.SimNext(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
            Assert.AreEqual(Franchise.Roster(_f, _f.you).Count, _f.devReport.Count, "a line per player");
            Assert.IsTrue(_f.devReport.TrueForAll(l => l.Contains("→")));

            var career = Career.New(_c);
            career.franchise = _f;
            _f.offer = new FrOffer { team = (_f.you + 1) % Franchise.Teams, want = new List<int> { Franchise.Roster(_f, _f.you)[1].id },
                                     send = new List<int> { Franchise.Roster(_f, (_f.you + 1) % Franchise.Teams)[0].id }, week = 3, pitch = "x" };
            var back = SaveCodec.Decode(SaveCodec.Encode(career), _c, out var status).franchise;
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual((int)TrainingFocus.Defense, back.focus);
            Assert.AreEqual(_f.devReport.Count, back.devReport.Count);
            Assert.IsNotNull(back.offer);
            Assert.AreEqual(_f.offer.send[0], back.offer.send[0]);
        }
    }

    public class Phase33BracketTests
    {
        private static SeasonSaveData Season(ContentCatalog c)
        {
            var s = SeasonEngine.Create(c, DefaultContent.PlayerCrewId, 9);
            return s;
        }

        [Test]
        public void Bracket_IsProjected_ThenFillsIn_ThenCrownsAChampion()
        {
            var c = DefaultContent.Create();
            var s = Season(c);
            var b = PlayoffBracket.From(s);
            Assert.IsTrue(b.Projected);
            Assert.AreEqual(1, b.SemiA.Top.Seed);
            Assert.AreEqual(4, b.SemiA.Bottom.Seed);
            Assert.AreEqual(2, b.SemiB.Top.Seed);
            Assert.AreEqual(3, b.SemiB.Bottom.Seed);
            Assert.IsFalse(b.Final.Top.Known);

            var rng = new SeededRandom(4);
            foreach (var g in s.games) if (g.round == 0 && !g.played) SeasonEngine.SimulateGame(g, c, rng);
            SeasonEngine.CreatePlayoffs(s);
            b = PlayoffBracket.From(s);
            Assert.IsFalse(b.Projected);
            Assert.AreEqual(SeasonEngine.Standings(s)[0].TeamId, b.SemiA.Top.TeamId, "the top seed sits on top");
            Assert.IsFalse(b.SemiA.Played);

            foreach (var g in s.games) if (g.round == 1) SeasonEngine.SimulateGame(g, c, rng);
            b = PlayoffBracket.From(s);
            Assert.IsTrue(b.SemiA.Played && b.SemiB.Played);
            Assert.IsTrue(b.SemiA.Top.Won ^ b.SemiA.Bottom.Won, "one winner");
            SeasonEngine.CreateFinal(s);
            b = PlayoffBracket.From(s);
            Assert.IsTrue(b.Final.Top.Known && b.Final.Bottom.Known);
            Assert.IsNull(b.ChampionId);
            SeasonEngine.SimulateGame(SeasonEngine.FinalGame(s), c, rng);
            b = PlayoffBracket.From(s);
            Assert.AreEqual(SeasonEngine.FinalGame(s).WinnerId, b.ChampionId);
            int matchups = 0;
            foreach (var m in b.All()) { matchups++; Assert.IsTrue(m.Played); }
            Assert.AreEqual(3, matchups);
        }
    }

    public class Phase33LiveTests
    {
        [Test]
        public void MonthlyBoard_OnlyCountsThisMonth()
        {
            var now = new System.DateTime(2026, 10, 15, 0, 0, 0, System.DateTimeKind.Utc);
            Assert.AreEqual(202610, LiveMode.MonthKey(now));
            var s = new LiveSaveData { rating = 1080, games = 3, month = 202610 };
            Assert.AreEqual(1080L, LiveMode.MonthlyScore(s, now));
            Assert.AreEqual(0L, LiveMode.MonthlyScore(s, now.AddMonths(1)), "a new month starts from nothing");
            Assert.AreEqual(0L, LiveMode.MonthlyScore(new LiveSaveData(), now), "never played");
            Assert.IsTrue(Achievements.Boards.Exists(b => b.Id == LiveMode.MonthlyLeaderboardId));
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.live = s;
            Assert.AreEqual(202610, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).live.month);
            Assert.AreEqual(202610, LiveMode.Sanitize(s).month);
        }

        [Test]
        public void Rematch_StartsFromTheRatingsAfterTheGame()
        {
            var me = new LiveSaveData { rating = 1000 };
            int delta = LiveMode.Apply(me, 1100, true);
            Assert.Greater(delta, 0);
            Assert.AreEqual(1100 - delta, LiveMode.OpponentAfter(1100, delta), "zero-sum");
            Assert.AreEqual(100, LiveMode.OpponentAfter(105, 40), "never below the floor");
        }
    }
}
