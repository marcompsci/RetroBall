using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    internal static class Fake
    {
        public static MatchSummary Summary(GameMode mode, bool humanWins, string id, int humanPoints = 8, string opponent = "team.baycity_breakers",
                                           string home = DefaultContent.PlayerCrewId)
        {
            var s = new MatchSummary
            {
                matchId = id, mode = mode, teamAId = home, teamBId = opponent,
                scoreA = humanWins ? 21 : 15, scoreB = humanWins ? 15 : 21, humanTeam = 0,
            };
            s.winner = humanWins ? 0 : 1;
            s.lines.Add(new SummaryLine
            {
                playerIndex = 0, team = 0, isHuman = true, name = "Rook",
                stats = new PlayerStatLine { points = humanPoints, assists = 2, rebounds = 3, steals = 1, blocks = 1, greenReleases = 2, fieldGoalsMade = 4, fieldGoalsAttempted = 8 },
            });
            return s;
        }
    }

    public class RewardAndCareerTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Rewards_WinPaysMoreThanLoss_AndPracticePaysNothing()
        {
            var t = RewardTuning.Default;
            var win = Rewards.For(Fake.Summary(GameMode.Rise, true, "a"), t);
            var loss = Rewards.For(Fake.Summary(GameMode.Rise, false, "b"), t);
            Assert.Greater(win.signalPoints, loss.signalPoints);
            Assert.Greater(win.fans, loss.fans);
            var practice = Rewards.For(Fake.Summary(GameMode.Practice, true, "c"), t);
            Assert.AreEqual(0, practice.signalPoints);
            Assert.AreEqual(0, practice.fans);
        }

        [Test]
        public void Rewards_QuickCallPaysLessThanRise_AndAreCapped()
        {
            var t = RewardTuning.Default;
            Assert.Less(Rewards.For(Fake.Summary(GameMode.QuickCall, true, "a"), t).signalPoints,
                        Rewards.For(Fake.Summary(GameMode.Rise, true, "b"), t).signalPoints);
            var huge = Rewards.For(Fake.Summary(GameMode.Rise, true, "c", 500), t);
            Assert.AreEqual(t.maxPerGame, huge.signalPoints);
        }

        [Test]
        public void ApplyMatch_GrantsOnce()
        {
            var data = Career.New(_c);
            var s = Fake.Summary(GameMode.QuickCall, true, "match-1");
            var g = Rewards.For(s, RewardTuning.Default);
            Assert.IsTrue(Career.ApplyMatch(data, s, g));
            int sp = data.signalPoints;
            Assert.IsFalse(Career.ApplyMatch(data, s, g), "Second grant for the same match must be refused");
            Assert.AreEqual(sp, data.signalPoints);
            Assert.AreEqual(1, data.totals.games);
            Assert.AreEqual(1, data.totals.wins);
            Assert.AreEqual(8, data.totals.points);
        }

        [Test]
        public void NewCareer_OwnsAndEquipsDefaults()
        {
            var data = Career.New(_c);
            foreach (CosmeticSlot slot in System.Enum.GetValues(typeof(CosmeticSlot)))
                Assert.IsNotNull(data.Equipped(slot), slot.ToString());
            Assert.AreEqual("Rook", data.nickname);
        }

        [TestCase("  Big   Shot!! ", "Big   Shot")]
        [TestCase("", "Rook")]
        [TestCase("<script>", "script")]
        [TestCase("AVeryLongNicknameIndeed", "AVeryLongNickn")]
        public void Nickname_IsCleaned(string raw, string expected)
        {
            Assert.AreEqual(expected, Career.CleanNickname(raw));
        }

        [Test]
        public void UpgradeCost_Grows_AndLevelsAreCapped()
        {
            var u = _c.Find(_c.Upgrades, "upgrade.shooting");
            Assert.AreEqual(150, Career.UpgradeCost(u, 0));
            Assert.AreEqual(225, Career.UpgradeCost(u, 1));
            var data = Career.New(_c);
            data.signalPoints = 100000;
            var rook = _c.Player(DefaultContent.RookPlayerId).attributes;
            int bought = 0;
            for (int i = 0; i < 20; i++)
            {
                data.gamesSinceUpgrade = 5;
                if (Career.Buy(data, u, rook) == UpgradeCheck.Ok) bought++;
            }
            Assert.AreEqual(u.maxLevel, bought);
            Assert.AreEqual(UpgradeCheck.MaxLevel, Career.CanBuy(data, u, rook));
            var eff = Career.EffectiveRatings(rook, data, _c);
            Assert.AreEqual(System.Math.Min(rook.shooting + u.maxLevel * u.amountPerLevel, u.attributeCap), eff.shooting);
        }

        [Test]
        public void Upgrade_RequiresTrainingTime_AndPoints()
        {
            var u = _c.Find(_c.Upgrades, "upgrade.speed");
            var rook = _c.Player(DefaultContent.RookPlayerId).attributes;
            var data = Career.New(_c);
            Assert.AreEqual(UpgradeCheck.NotEnoughPoints, Career.CanBuy(data, u, rook));
            data.signalPoints = 10000;
            Assert.AreEqual(UpgradeCheck.Ok, Career.Buy(data, u, rook));
            Assert.AreEqual(UpgradeCheck.NeedsTraining, Career.CanBuy(data, u, rook), "Needs a completed game between upgrades");
            Career.ApplyMatch(data, Fake.Summary(GameMode.QuickCall, false, "g1"), default);
            Assert.AreEqual(UpgradeCheck.Ok, Career.CanBuy(data, u, rook));
        }

        [Test]
        public void Upgrade_StopsAtAttributeCap()
        {
            var u = new UpgradeDef { id = "x", attribute = AttributeType.Shooting, amountPerLevel = 5, maxLevel = 10, baseCost = 1, costGrowth = 1f, attributeCap = 60 };
            var c = DefaultContent.Create();
            c.Upgrades = new List<UpgradeDef> { u };
            var data = Career.New(c);
            data.signalPoints = 1000;
            var baseR = new AttributeSet(50, 50, 50, 50, 50, 50, 50, 50);
            int n = 0;
            while (true)
            {
                data.gamesSinceUpgrade = 5;
                if (Career.Buy(data, u, baseR) != UpgradeCheck.Ok) break;
                n++;
            }
            Assert.AreEqual(2, n);
            Assert.AreEqual(60, Career.EffectiveRatings(baseR, data, c).shooting);
            Assert.AreEqual(UpgradeCheck.AtAttributeCap, Career.CanBuy(data, u, baseR));
        }

        [Test]
        public void Cosmetics_NeedFansAndPoints_ThenEquip()
        {
            var data = Career.New(_c);
            var fancy = _c.Find(_c.Cosmetics, "cosmetic.jersey.midnight_neon");
            data.signalPoints = 1000;
            Assert.AreEqual(CosmeticCheck.NeedsFans, Career.CanBuy(data, fancy));
            data.fans = 500;
            Assert.AreEqual(CosmeticCheck.Ok, Career.Buy(data, fancy));
            Assert.AreEqual(fancy.id, data.equippedJersey);
            Assert.AreEqual(CosmeticCheck.AlreadyOwned, Career.CanBuy(data, fancy));
            var home = _c.Find(_c.Cosmetics, "cosmetic.jersey.crew_home");
            Assert.IsTrue(Career.Equip(data, home));
            Assert.AreEqual(home.id, data.equippedJersey);
        }

        [Test]
        public void PracticeBests_OnlyImprove()
        {
            var data = Career.New(_c);
            Assert.IsTrue(Career.RecordPractice(data, 10, 4, 5, 12.5f));
            Assert.IsFalse(Career.RecordPractice(data, 8, 3, 2, 14f));
            Assert.IsTrue(Career.RecordPractice(data, 0, 0, 0, 11f));
            Assert.AreEqual(11f, data.practice.dribbleLaneTime, 1e-5);
            Assert.AreEqual(10, data.practice.freeShootMakes);
        }
    }

    public class SaveCodecTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var d = Career.New(_c);
            d.nickname = "Signal";
            d.signalPoints = 1234;
            d.fans = 567;
            d.upgrades.Add(new UpgradeProgress { id = "upgrade.shooting", level = 3 });
            d.settings.musicVolume = 0.25f;
            d.settings.colorblindContrast = true;
            d.settings.difficultyId = "difficulty.legend";
            d.totals.wins = 7;
            d.practice.dribbleLaneTime = 9.75f;
            RiseEngine.StartSeason(d.rise, _c);
            d.rise.energy = 64;
            d.rise.season.games[0].played = true;
            d.rise.season.games[0].homeScore = 21;
            d.appliedMatchIds.Add("m-1");

            var json = SaveCodec.Encode(d);
            var back = SaveCodec.Decode(json, _c, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual("Signal", back.nickname);
            Assert.AreEqual(1234, back.signalPoints);
            Assert.AreEqual(567, back.fans);
            Assert.AreEqual(3, back.UpgradeLevel("upgrade.shooting"));
            Assert.AreEqual(0.25f, back.settings.musicVolume, 1e-5);
            Assert.IsTrue(back.settings.colorblindContrast);
            Assert.AreEqual("difficulty.legend", back.settings.difficultyId);
            Assert.AreEqual(7, back.totals.wins);
            Assert.AreEqual(9.75f, back.practice.dribbleLaneTime, 1e-5);
            Assert.AreEqual(RiseStage.Season, back.rise.stage);
            Assert.AreEqual(64, back.rise.energy);
            Assert.AreEqual(d.rise.season.games.Count, back.rise.season.games.Count);
            Assert.AreEqual(21, back.rise.season.games[0].homeScore);
            Assert.AreEqual(d.rise.season.seed, back.rise.season.seed);
            Assert.IsTrue(back.appliedMatchIds.Contains("m-1"));
            Assert.AreEqual(SaveCodec.Encode(back), json, "Encoding must be stable");
        }

        [TestCase("")]
        [TestCase("   ")]
        public void EmptyInput_MakesANewCareer(string json)
        {
            SaveCodec.Decode(json, _c, out var status);
            Assert.AreEqual(LoadStatus.New, status);
        }

        [TestCase("{not json")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"nickname\":\"NoVersion\"}")]
        [TestCase("{\"version\":1,\"signalPoints\":")]
        public void Malformed_Recovers_WithAFreshCareer(string json)
        {
            var d = SaveCodec.Decode(json, _c, out var status);
            Assert.AreEqual(LoadStatus.Recovered, status);
            Assert.IsNotNull(d);
            Assert.AreEqual(0, d.signalPoints);
        }

        [Test]
        public void WrongTypes_FallBackToDefaults()
        {
            var d = SaveCodec.Decode("{\"version\":1,\"signalPoints\":\"lots\",\"fans\":-50,\"settings\":{\"uiScale\":9}}", _c, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(0, d.signalPoints);
            Assert.AreEqual(0, d.fans);
            Assert.AreEqual(1.25f, d.settings.uiScale, 1e-5);
        }

        [Test]
        public void MiniJson_HandlesEscapesAndNumbers()
        {
            var o = new Dictionary<string, object> { ["s"] = "quote \" slash \\ newline \n tab \t", ["n"] = -12.5, ["b"] = false, ["z"] = null };
            var back = (Dictionary<string, object>)MiniJson.Read(MiniJson.Write(o, false));
            Assert.AreEqual(o["s"], back["s"]);
            Assert.AreEqual(-12.5, (double)back["n"], 1e-9);
            Assert.AreEqual(false, back["b"]);
            Assert.IsNull(back["z"]);
        }
    }

    public class SeasonTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();
        private const string Crew = DefaultContent.PlayerCrewId;

        [Test]
        public void Season_HasEightTeams_AndTenGamesForTheCrew()
        {
            var s = SeasonEngine.Create(_c, Crew, 42);
            Assert.AreEqual(8, s.teamIds.Count);
            Assert.AreEqual(8, new HashSet<string>(s.teamIds).Count);
            Assert.AreEqual(10, s.games.FindAll(g => g.Involves(Crew)).Count);
            // Every week, every team plays exactly once.
            for (int w = 0; w < s.weeks; w++)
            {
                var week = s.games.FindAll(g => g.week == w);
                Assert.AreEqual(4, week.Count);
                var seen = new HashSet<string>();
                foreach (var g in week)
                {
                    Assert.IsTrue(seen.Add(g.homeId));
                    Assert.IsTrue(seen.Add(g.awayId));
                }
            }
        }

        [Test]
        public void Season_IsDeterministicPerSeed()
        {
            var a = SeasonEngine.Create(_c, Crew, 7);
            var b = SeasonEngine.Create(_c, Crew, 7);
            Assert.AreEqual(a.games.Count, b.games.Count);
            for (int i = 0; i < a.games.Count; i++)
            {
                Assert.AreEqual(a.games[i].homeId, b.games[i].homeId);
                Assert.AreEqual(a.games[i].awayId, b.games[i].awayId);
            }
        }

        [Test]
        public void Standings_SortByWinPct_ThenDifferential_ThenPointsFor_ThenHeadToHead()
        {
            var s = new SeasonSaveData { teamIds = new List<string> { "a", "b", "c", "d" }, weeks = 3 };
            void G(string h, string a, int hs, int @as) => s.games.Add(new ScheduledGame { homeId = h, awayId = a, played = true, homeScore = hs, awayScore = @as });
            G("a", "b", 21, 10); // a +11
            G("c", "d", 21, 20); // c +1
            G("b", "c", 21, 11); // b +10
            G("d", "a", 21, 20); // d +1
            // a: 1-1 diff +10, b: 1-1 diff -1, c: 1-1 diff -9, d: 1-1 diff 0
            var t = SeasonEngine.Standings(s);
            Assert.AreEqual(new[] { "a", "d", "b", "c" }, new[] { t[0].TeamId, t[1].TeamId, t[2].TeamId, t[3].TeamId });

            // Equal record, differential, and points: head-to-head decides.
            var h = new SeasonSaveData { teamIds = new List<string> { "x", "y" } };
            h.games.Add(new ScheduledGame { homeId = "y", awayId = "x", played = true, homeScore = 21, awayScore = 20 });
            h.games.Add(new ScheduledGame { homeId = "x", awayId = "y", played = true, homeScore = 21, awayScore = 20 });
            var ht = SeasonEngine.Standings(h);
            Assert.AreEqual("x", ht[0].TeamId); // all tied → id order as the final, stable tiebreak
        }

        [Test]
        public void Streaks_AreTracked()
        {
            var s = new SeasonSaveData { teamIds = new List<string> { "a", "b" } };
            s.games.Add(new ScheduledGame { homeId = "a", awayId = "b", played = true, homeScore = 21, awayScore = 5 });
            s.games.Add(new ScheduledGame { homeId = "b", awayId = "a", played = true, homeScore = 5, awayScore = 21 });
            var t = SeasonEngine.Standings(s);
            Assert.AreEqual("W2", t.Find(r => r.TeamId == "a").StreakText);
            Assert.AreEqual("L2", t.Find(r => r.TeamId == "b").StreakText);
        }

        [Test]
        public void RecordResult_SimulatesTheRestOfTheWeek()
        {
            var s = SeasonEngine.Create(_c, Crew, 3);
            var g = SeasonEngine.NextGameFor(s, Crew);
            SeasonEngine.RecordResult(s, g, 21, 12, _c);
            Assert.IsTrue(s.games.FindAll(x => x.week == 0).TrueForAll(x => x.played));
            Assert.AreEqual(1, s.currentWeek);
            foreach (var r in SeasonEngine.Standings(s)) Assert.AreEqual(1, r.Games);
        }

        [Test]
        public void Playoffs_SeedOneVsFour_AndProduceAChampion()
        {
            var s = SeasonEngine.Create(_c, Crew, 5);
            var rng = new SeededRandom(9);
            foreach (var g in s.games) SeasonEngine.SimulateGame(g, _c, rng);
            Assert.IsTrue(SeasonEngine.RegularSeasonComplete(s));
            SeasonEngine.CreatePlayoffs(s);
            var table = SeasonEngine.Standings(s);
            var semis = s.games.FindAll(g => g.round == 1);
            Assert.AreEqual(2, semis.Count);
            Assert.AreEqual(table[0].TeamId, semis[0].homeId);
            Assert.AreEqual(table[3].TeamId, semis[0].awayId);
            SeasonEngine.SimulateRoundWithout(s, 1, "nobody", _c);
            SeasonEngine.CreateFinal(s);
            SeasonEngine.SimulateRoundWithout(s, 2, "nobody", _c);
            Assert.IsNotNull(SeasonEngine.FinalGame(s).WinnerId);
        }
    }

    public class RiseTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Circuit_FiveWins_EntersTheLeague()
        {
            var career = Career.New(_c);
            var r = career.rise;
            Assert.AreEqual(RiseStage.Circuit, r.stage);
            var first = RiseEngine.NextMatch(r, _c, "difficulty.caller");
            Assert.AreEqual(GameMode.Rise, first.Mode);
            Assert.AreEqual("crew.cage_regulars", first.AwayTeamId);
            Assert.AreEqual("court.sunset_cage", first.CourtId);

            Assert.AreEqual(RiseOutcome.CircuitLoss, RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, false, "x", opponent: first.AwayTeamId), career));
            Assert.AreEqual(0, r.circuitBeaten.Count);
            Assert.AreEqual(RiseOutcome.CircuitWin, RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "1", opponent: "crew.cage_regulars"), career));
            Assert.AreEqual(RiseOutcome.CircuitWin, RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "2", opponent: "crew.pier_pressure"), career));
            Assert.AreEqual(RiseOutcome.CircuitWin, RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "3", opponent: "crew.underpass_union"), career));
            Assert.AreEqual(RiseOutcome.CircuitWin, RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "4", opponent: "crew.rooftop_relay"), career));
            Assert.AreEqual(RiseOutcome.EnteredLeague, RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "5", opponent: "crew.boardwalk_bandits"), career));
            Assert.AreEqual(RiseStage.Season, r.stage);
            Assert.IsNotNull(r.season);
        }

        [Test]
        public void UnbeatenSeason_WinsTheGoldSignalCup()
        {
            var career = Career.New(_c);
            var r = career.rise;
            RiseEngine.StartSeason(r, _c);
            int games = 0;
            RiseOutcome last = RiseOutcome.None;
            while (r.stage != RiseStage.Complete && games < 20)
            {
                var req = RiseEngine.NextMatch(r, _c, "difficulty.caller");
                Assert.IsNotNull(req, "No next match at stage " + r.stage);
                last = RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "g" + games, opponent: req.AwayTeamId), career);
                if (r.pendingEventId != null) Assert.IsTrue(RiseEngine.ResolveEvent(r, career, 0));
                games++;
            }
            Assert.AreEqual(RiseOutcome.Champion, last);
            Assert.AreEqual(12, games, "10 regular season + semifinal + final");
            Assert.AreEqual(DefaultContent.PlayerCrewId, r.season.championId);
            Assert.AreEqual(1, career.totals.championships);
            Assert.AreEqual(1, r.seasonsPlayed);
            Assert.IsNull(RiseEngine.NextMatch(r, _c, "difficulty.caller"));
            Assert.IsTrue(RiseEngine.StartNextSeason(r, _c));
            Assert.AreEqual(2, r.season.seasonNumber);
        }

        [Test]
        public void WinlessSeason_MissesThePlayoffs_ButStillCrownsSomeone()
        {
            var career = Career.New(_c);
            var r = career.rise;
            RiseEngine.StartSeason(r, _c);
            RiseOutcome last = RiseOutcome.None;
            for (int i = 0; i < 10; i++)
            {
                var req = RiseEngine.NextMatch(r, _c, "difficulty.caller");
                last = RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, false, "l" + i, opponent: req.AwayTeamId), career);
                r.pendingEventId = null;
            }
            Assert.AreEqual(RiseOutcome.MissedPlayoffs, last);
            Assert.AreEqual(RiseStage.Complete, r.stage);
            Assert.IsNotNull(r.season.championId);
            Assert.AreNotEqual(DefaultContent.PlayerCrewId, r.season.championId);
        }

        [Test]
        public void Energy_DrainsWithGames_AndEventsAdjustIt()
        {
            var career = Career.New(_c);
            var r = career.rise;
            RiseEngine.StartSeason(r, _c);
            int before = r.energy;
            var req = RiseEngine.NextMatch(r, _c, "difficulty.caller");
            RiseEngine.ApplyResult(r, _c, Fake.Summary(GameMode.Rise, true, "e1", opponent: req.AwayTeamId), career);
            Assert.AreEqual(before - RiseEngine.EnergyPerGame + RiseEngine.EnergyRecoveryPerGame, r.energy);
            Assert.GreaterOrEqual(RiseEngine.StartingStamina(new RiseSaveData { energy = 10 }), 0.6f);
            Assert.IsNotNull(r.pendingEventId, "An event card follows the first league game");
            int sp = career.signalPoints;
            var card = EventCards.Find(r.pendingEventId);
            Assert.IsTrue(RiseEngine.ResolveEvent(r, career, 1));
            Assert.IsNull(r.pendingEventId);
            Assert.AreEqual(System.Math.Max(0, sp + card.choices[1].effect.signalPoints), career.signalPoints);
        }

        [Test]
        public void EventCards_AreOriginalAndWellFormed()
        {
            var all = EventCards.All();
            Assert.GreaterOrEqual(all.Count, 6);
            var ids = new HashSet<string>();
            foreach (var e in all)
            {
                Assert.IsTrue(ids.Add(e.id));
                Assert.GreaterOrEqual(e.choices.Count, 2);
                Assert.IsFalse(string.IsNullOrEmpty(e.body));
            }
        }

        [Test]
        public void Objective_TextChangesWithStage()
        {
            var r = new RiseSaveData();
            StringAssertContains("Cage Regulars", RiseEngine.Objective(r, _c));
            RiseEngine.StartSeason(r, _c);
            StringAssertContains("top 4", RiseEngine.Objective(r, _c));
        }

        private static void StringAssertContains(string expected, string actual) =>
            Assert.IsTrue(actual.Contains(expected), "'" + actual + "' should contain '" + expected + "'");
    }

    public class PracticeTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation Practice(bool green = false)
        {
            var c = DefaultContent.Create();
            var setup = MatchSetup.FromRequest(MatchRequest.PracticeDefault(), c);
            setup.Shot.debugGreenAlwaysMakes = green;
            return new MatchSimulation(setup);
        }

        [Test]
        public void FreeShoot_CountsMakesGreensAndStreaks()
        {
            var m = Practice(green: true);
            var p = new PracticeSession(DrillKind.FreeShoot, m);
            for (int shot = 0; shot < 3; shot++)
            {
                for (int i = 0; i < 200 && !m.HumanHasBall; i++) { m.Step(Dt, PlayerInput.None); p.Update(m, Dt); }
                m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
                p.Update(m, Dt);
                while (m.ChargeMeter < m.Setup.Shot.greenCenter - 0.01f) { m.Step(Dt, new PlayerInput { ShootHeld = true }); p.Update(m, Dt); }
                m.Step(Dt, PlayerInput.None);
                p.Update(m, Dt);
                for (int i = 0; i < 200 && m.Phase != MatchPhase.CheckBall; i++) { m.Step(Dt, PlayerInput.None); p.Update(m, Dt); }
            }
            Assert.AreEqual(3, p.Attempts);
            Assert.AreEqual(3, p.Makes);
            Assert.AreEqual(3, p.Greens);
            Assert.AreEqual(3, p.BestStreak);
        }

        [Test]
        public void FreeShoot_EndsAfterSixtySeconds()
        {
            var m = Practice();
            var p = new PracticeSession(DrillKind.FreeShoot, m);
            for (int i = 0; i < 60 * 65 && !p.Finished; i++)
            {
                m.Step(Dt, new PlayerInput { Move = new Vec2(0.01f, 0f) });
                p.Update(m, Dt);
            }
            Assert.IsTrue(p.Finished);
            Assert.AreEqual(0f, p.TimeLeft, 1e-5);
        }

        [Test]
        public void DribbleLane_CompletesWhenAllConesAreTouchedInOrder()
        {
            var m = Practice();
            var p = new PracticeSession(DrillKind.DribbleLane, m);
            Assert.AreEqual(5, p.Cones.Count);
            for (int i = 0; i < 60 * 30 && !p.Finished; i++)
            {
                var target = p.Cones[p.NextCone];
                var move = Movement.ArriveInput(m.Controlled.Position, target, 0.5f, 0.05f);
                m.Step(Dt, new PlayerInput { Move = move });
                p.Update(m, Dt);
            }
            Assert.IsTrue(p.Finished);
            Assert.Greater(p.CourseTime, 1f);
            Assert.IsTrue(p.ResultText().StartsWith("Course:"));
        }

        [Test]
        public void PassingTargets_ScoresOnlyTheHighlightedTeammate_AndGetsTheBallBack()
        {
            var m = Practice();
            var p = new PracticeSession(DrillKind.PassingTargets, m, 5);
            int hits = 0;
            for (int rep = 0; rep < 6; rep++)
            {
                for (int i = 0; i < 200 && !m.HumanHasBall; i++) { m.Step(Dt, PlayerInput.None); p.Update(m, Dt); }
                Assert.IsTrue(m.HumanHasBall, "Ball never came back");
                var target = m.Players[p.TargetPlayer];
                var aim = (target.Position - m.Controlled.Position).Normalized;
                m.Step(Dt, new PlayerInput { PassPressed = true, Move = aim });
                p.Update(m, Dt);
                for (int i = 0; i < 120 && !m.Ball.IsHeld; i++) { m.Step(Dt, PlayerInput.None); p.Update(m, Dt); }
                hits = p.PassScore;
            }
            Assert.GreaterOrEqual(hits, 5, "Aimed passes should hit the highlighted target");
        }
    }
}

namespace CallerRetroBall.Tests
{
    using CallerRetroBall.Logic.PixelArt;

    public class AudioAndCosmeticArtTests
    {
        [Test]
        public void EverySfx_IsShort_InRange_AndDeterministic()
        {
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
            {
                var a = AudioSynth.Sfx(id);
                var b = AudioSynth.Sfx(id);
                Assert.Greater(a.Length, 100, id.ToString());
                Assert.LessOrEqual(AudioSynth.Duration(a), 2f, id.ToString());
                float peak = 0f;
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i], b[i]);
                    peak = System.Math.Max(peak, System.Math.Abs(a[i]));
                }
                Assert.LessOrEqual(peak, 1f, id.ToString());
                Assert.Greater(peak, 0.05f, id.ToString());
            }
        }

        [Test]
        public void MusicLoop_IsAWholeNumberOfBeats()
        {
            var loop = AudioSynth.MusicLoop();
            float seconds = AudioSynth.Duration(loop);
            float beats = seconds * 112f / 60f;
            Assert.AreEqual(32f, beats, 0.01);
        }

        [Test]
        public void ColourblindPatterns_ChangeTheJersey_AndDifferPerTeam()
        {
            var look = new AppearanceDef(1, 1, 1, BodyType.Standard, 1);
            var plain = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.FromHex("#1FB5A6"), RgbColor.FromHex("#FF6F59"), RgbColor.White, null, TeamPattern.Solid);
            var stripes = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.FromHex("#1FB5A6"), RgbColor.FromHex("#FF6F59"), RgbColor.White, null, TeamPattern.Stripes);
            var dots = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.FromHex("#1FB5A6"), RgbColor.FromHex("#FF6F59"), RgbColor.White, null, TeamPattern.Dots);
            Assert.AreNotEqual(plain.Pixels, stripes.Pixels);
            Assert.AreNotEqual(stripes.Pixels, dots.Pixels);
        }

        [Test]
        public void ShoeColour_IsApplied()
        {
            var look = new AppearanceDef(1, 1, 1, BodyType.Standard, 1);
            var red = RgbColor.FromHex("#FF0000");
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.White, RgbColor.Black, RgbColor.Black, red, TeamPattern.Solid);
            bool found = false;
            foreach (var px in sheet.Pixels) if (px == red) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void CourtBanner_ChangesTheCrowdStrip()
        {
            var c = DefaultContent.Create();
            var g = CourtGeometry.Default;
            var court = c.Court("court.sunset_cage");
            var plain = CourtGenerator.Generate(court, g, 3);
            var banner = CourtGenerator.Generate(court, g, 3, RgbColor.FromHex("#D4A017"), RgbColor.FromHex("#1A1A2E"));
            Assert.AreNotEqual(plain.Pixels, banner.Pixels);
        }
    }
}
