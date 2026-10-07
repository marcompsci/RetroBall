using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 37: CLUTCH scenarios.</summary>
    public class Phase37ClutchTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        private static MatchSummary Result(ClutchScenario s, int scoreFor, int scoreAgainst, PlayerStatLine you = null, PlayerStatLine mate = null)
        {
            var m = new MatchSummary { matchId = "clutch-test", mode = GameMode.Clutch, scoreA = scoreFor, scoreB = scoreAgainst, humanTeam = 0 };
            m.winner = scoreFor > scoreAgainst ? 0 : scoreAgainst > scoreFor ? 1 : -1;
            m.lines.Add(new SummaryLine { playerIndex = 0, team = 0, isHuman = true, stats = you ?? new PlayerStatLine() });
            m.lines.Add(new SummaryLine { playerIndex = 1, team = 0, stats = mate ?? new PlayerStatLine() });
            m.lines.Add(new SummaryLine { playerIndex = 3, team = 1, stats = new PlayerStatLine() });
            return m;
        }

        [Test]
        public void Scenarios_AreValid()
        {
            var ids = new HashSet<string>();
            Assert.AreEqual(18, Clutch.All.Length);
            foreach (var s in Clutch.All)
            {
                Assert.IsTrue(ids.Add(s.Id), "unique id " + s.Id);
                Assert.IsNotNull(C.Team(s.YourTeamId), s.Id);
                Assert.IsNotNull(C.Team(s.TheirTeamId), s.Id);
                Assert.AreNotEqual(s.YourTeamId, s.TheirTeamId, s.Id);
                Assert.Greater(s.Clock, 0, s.Id);
                Assert.IsTrue(s.Chapter >= 0 && s.Chapter < Clutch.ChapterNames.Length);
                Assert.IsNotEmpty(Clutch.GoalText(s.Goal, s.GoalValue));
                Assert.AreNotEqual(s.Goal, s.Bonus, s.Id + ": the goal and the bonus ask for different things");
                Assert.AreEqual(s.Id, Clutch.FromContext(Clutch.Request(C, s, DefaultContent.DefaultDifficultyId).ContextId).Id);
            }
            for (int ch = 0; ch < Clutch.ChapterNames.Length; ch++) Assert.AreEqual(6, Clutch.InChapter(ch).Count);
            Assert.AreEqual(54, Clutch.MaxStars);
            Assert.IsNotNull(C.Find(C.Rules, Clutch.RulesId));
        }

        [Test]
        public void Match_StartsFromTheScenario()
        {
            foreach (var s in new[] { Clutch.Find("ct_hold"), Clutch.Find("iv_three") })
            {
                var m = new MatchSimulation(MatchSetup.FromRequest(Clutch.Request(C, s, DefaultContent.DefaultDifficultyId), C));
                Assert.AreEqual(s.ScoreFor, m.Score[0], s.Id);
                Assert.AreEqual(s.ScoreAgainst, m.Score[1], s.Id);
                Assert.AreEqual(s.Clock, m.GameClock, 0.001f, s.Id);
                Assert.AreEqual(s.FullCourt, m.Setup.FullCourt, s.Id);
                Assert.AreEqual(s.YourBall ? 0 : 1, m.OffenseTeam, s.Id);
                Assert.AreEqual(s.YourTeamId, m.Setup.TeamA.id);
            }
        }

        [Test]
        public void Match_PlaysOut_AndEndsOnTheClock()
        {
            var s = Clutch.Find("ct_down2");
            var m = new MatchSimulation(MatchSetup.FromRequest(Clutch.Request(C, s, DefaultContent.DefaultDifficultyId), C));
            int steps = 0;
            while (!m.IsOver && steps < 60 * 240) { m.Step(1f / 60f, default); steps++; }
            Assert.IsTrue(m.IsOver, "a clutch game finishes");
            Assert.GreaterOrEqual(m.Score[0], s.ScoreFor);
            Assert.GreaterOrEqual(m.Score[1], s.ScoreAgainst);
            Assert.AreNotEqual(m.Score[0], m.Score[1], "a tie at the horn goes to sudden death");
            var sum = MatchSummary.From(m, GameMode.Clutch, "c1");
            Assert.AreEqual(m.Score[0], sum.scoreA);
        }

        [Test]
        public void Stars_WinGoalAndBonus()
        {
            var s = Clutch.Find("ct_down2"); // goal: a three; bonus: no turnovers
            Assert.AreEqual(0, Clutch.StarsEarned(s, Result(s, 19, 20, new PlayerStatLine { arcMade = 1 })), "a loss earns nothing");
            Assert.AreEqual(1, Clutch.StarsEarned(s, Result(s, 20, 19, new PlayerStatLine { turnovers = 1 })));
            Assert.AreEqual(2, Clutch.StarsEarned(s, Result(s, 21, 19, new PlayerStatLine { turnovers = 1 }, new PlayerStatLine { arcMade = 1 })), "a teammate's three counts for the team");
            Assert.AreEqual(3, Clutch.StarsEarned(s, Result(s, 21, 19, new PlayerStatLine { arcMade = 1 })));

            var hold = Clutch.Find("ct_hold"); // up one from 15-14, allow 1 or fewer
            Assert.IsTrue(Clutch.Met(hold, ClutchGoal.HoldTo, 1, Result(hold, 16, 15)));
            Assert.IsFalse(Clutch.Met(hold, ClutchGoal.HoldTo, 1, Result(hold, 18, 16)));
            Assert.IsTrue(Clutch.Met(hold, ClutchGoal.WinBy, 2, Result(hold, 18, 16)));
            Assert.IsTrue(Clutch.Met(hold, ClutchGoal.YouScore, 3, Result(hold, 18, 16, new PlayerStatLine { points = 3 })));
        }

        [Test]
        public void ApplyResult_KeepsBestStars_AndPaysOnlyNewOnes()
        {
            var d = new ClutchSaveData();
            var career = new CareerSaveData();
            var s = Clutch.Find("ct_down2");
            Assert.AreEqual(ClutchOutcome.Lost, Clutch.ApplyResult(d, s, Result(s, 19, 20), career, out _, out _));
            Assert.AreEqual(0, career.signalPoints);
            Assert.AreEqual(1, d.played);

            Clutch.ApplyResult(d, s, Result(s, 21, 19, new PlayerStatLine { turnovers = 1, arcMade = 1 }), career, out int stars, out int fresh);
            Assert.AreEqual(2, stars);
            Assert.AreEqual(2, fresh);
            Assert.AreEqual(2 * Clutch.SpPerNewStar, career.signalPoints);

            Clutch.ApplyResult(d, s, Result(s, 20, 19, new PlayerStatLine { turnovers = 1 }), career, out stars, out fresh);
            Assert.AreEqual(1, stars);
            Assert.AreEqual(0, fresh);
            Assert.AreEqual(2, Clutch.StarsFor(d, s.Id), "the best is kept");

            Clutch.ApplyResult(d, s, Result(s, 21, 19, new PlayerStatLine { arcMade = 1 }), career, out stars, out fresh);
            Assert.AreEqual(1, fresh);
            Assert.AreEqual(3 * Clutch.SpPerNewStar, career.signalPoints);
            Assert.AreEqual(3, Clutch.TotalStars(d));
            Assert.AreEqual(4, d.played);
            Assert.AreEqual(3, d.won);
        }

        [Test]
        public void Chapters_OpenWithStars()
        {
            var d = new ClutchSaveData();
            Assert.IsTrue(Clutch.ChapterOpen(d, 0));
            Assert.IsFalse(Clutch.ChapterOpen(d, 1));
            foreach (var s in Clutch.InChapter(0)) d.stars.Add(s.Id + ":1");
            d.stars.Add("ct_down2:3"); // a duplicate the codec cleans up: the first entry wins
            Clutch.Sanitize(d);
            Assert.AreEqual(6, Clutch.TotalStars(d));
            Assert.IsFalse(Clutch.ChapterOpen(d, 1));
            d.stars[0] = Clutch.InChapter(0)[0].Id + ":3";
            Assert.AreEqual(8, Clutch.TotalStars(d));
            Assert.IsTrue(Clutch.ChapterOpen(d, 1));
            Assert.IsFalse(Clutch.ChapterOpen(d, 2));
            Assert.IsFalse(Clutch.Unlocked(d, Clutch.Find("ln_buzzer")));
            Assert.IsFalse(Clutch.AllWon(d));
        }

        [Test]
        public void Save_RoundTrips_AndDropsJunk()
        {
            var d = new CareerSaveData();
            d.clutch.stars.Add("iv_last:2");
            d.clutch.stars.Add("nope:3");
            d.clutch.stars.Add("ct_tied:9");
            d.clutch.played = 5;
            d.clutch.won = 7;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(2, Clutch.StarsFor(back.clutch, "iv_last"));
            Assert.AreEqual(3, Clutch.StarsFor(back.clutch, "ct_tied"), "clamped to three");
            Assert.AreEqual(5, Clutch.TotalStars(back.clutch));
            Assert.AreEqual(2, back.clutch.stars.Count);
            Assert.AreEqual(5, back.clutch.played);
            Assert.AreEqual(5, back.clutch.won, "can't win more than you played");
        }

        [Test]
        public void Rewards_NoBlowoutFans_AndNoRecords()
        {
            var s = Clutch.Find("ct_close"); // starts up two
            var big = Result(s, 30, 12, new PlayerStatLine { points = 14 });
            var quick = Result(s, 30, 12, new PlayerStatLine { points = 14 });
            quick.mode = GameMode.QuickCall;
            var t = new RewardTuning();
            Assert.Less(Rewards.For(big, t).fans, Rewards.For(quick, t).fans);
            Assert.Less(Rewards.For(big, t).signalPoints, Rewards.For(quick, t).signalPoints);

            var career = new CareerSaveData();
            Assert.IsTrue(Career.ApplyMatch(career, big, Rewards.For(big, t)));
            Assert.AreEqual(0, career.records.points, "a clutch game doesn't set game records");
            Assert.AreEqual(1, career.totals.wins);
            Assert.AreEqual("EVENTS", ModeDifficulty.GroupOf(GameMode.Clutch));
        }

        [Test]
        public void Badges_ForClutch()
        {
            var d = new CareerSaveData();
            var gene = Badges.All.Find(b => b.Id == "badge.clutch");
            var ice = Badges.All.Find(b => b.Id == "badge.ice_veins");
            Assert.IsFalse(Badges.IsEarned(gene, d));
            foreach (var s in Clutch.All) d.clutch.stars.Add(s.Id + ":1");
            Assert.IsTrue(Badges.IsEarned(gene, d));
            Assert.IsFalse(Badges.IsEarned(ice, d));
            d.clutch.stars.Clear();
            foreach (var s in Clutch.All) d.clutch.stars.Add(s.Id + ":3");
            Assert.IsTrue(Badges.IsEarned(ice, d));
        }

        [Test]
        public void GoalText_InSpanish()
        {
            string before = Loc.Language;
            try
            {
                Loc.Language = Loc.Spanish;
                Assert.AreEqual("Gana por 2+", Clutch.GoalText(ClutchGoal.WinBy, 2));
                Assert.AreEqual("2 tapones", Clutch.GoalText(ClutchGoal.Blocks, 2));
                Assert.AreEqual("1 tapón", Clutch.GoalText(ClutchGoal.Blocks, 1));
                Assert.IsTrue(Clutch.Situation(Clutch.Find("ct_down2")).Contains("PIERDES POR 2"));
                foreach (var s in Clutch.All)
                {
                    Assert.IsTrue(Loc.Has(s.Title), "Spanish title for " + s.Title);
                    Assert.IsTrue(Loc.Has(s.Story), "Spanish story for " + s.Id);
                }
                foreach (var name in Clutch.ChapterNames) Assert.IsTrue(Loc.Has(name), name);
            }
            finally { Loc.Language = before; }
        }
    }

    /// <summary>Phase 37: Season 9 (the Roller Royals).</summary>
    public class Phase37SeasonNineTests
    {
        [Test]
        public void RollerRoyals_AreTheNinthRival_WithStoryBadgesAndCourts()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
            var team = c.Team(DefaultContent.Rival9CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.AreEqual(LogoMotif.Skate, team.logoMotif);
            Assert.AreEqual("court.starlight_rink", team.homeCourtId);
            Assert.IsNotNull(c.Court("court.starlight_rink"));
            Assert.IsNotNull(c.Court("court.skate_bowl"));
            Assert.IsNotNull(c.Player(DefaultContent.Rival9LeaderId));
            Assert.IsNotNull(c.Find(c.Cosmetics, "cosmetic.jersey.rink_violet"));
            for (int season = 1; season <= 27; season++)
                Assert.AreEqual(season % 9 == 0, RivalEngine.RivalFor(season) == DefaultContent.Rival9CrewId, "season " + season);

            var d = Career.New(c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 9, currentWeek = 6 };
            Assert.AreEqual(Story.Rival9Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival9CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.royalWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d), "a Royals win isn't a Neon Static win");
            Story.MarkSeen(d, Story.Rival9Intro);
            Assert.AreEqual(Story.Rival9Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.royals").Earned(d));
            Assert.IsFalse(RivalEngine.BeatAllNine(d));
            d.rival.wins = 9;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = d.rival.keeperWins = d.rival.courierWins = d.rival.lanternWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllNine(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.all_nine").Earned(d));
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).rival.royalWins);
            foreach (var id in new[] { Story.Rival9Intro, Story.Rival9Beaten })
            {
                Assert.Greater(Story.Beat(id, "ROOK", "en").Lines.Count, 1, id);
                Assert.Greater(Story.Beat(id, "ROOK", "es").Lines.Count, 1, id + " in Spanish");
            }
            Assert.IsTrue(Loc.Has(team.motto));
            Assert.IsTrue(Loc.Has(c.Court("court.starlight_rink").description));
            Assert.IsTrue(Loc.Has(c.Court("court.skate_bowl").description));
        }

        [Test]
        public void SkateLogo_Draws_AndDiffersFromTheLantern()
        {
            var team = DefaultContent.Create().Team(DefaultContent.Rival9CrewId);
            var a = LogoGenerator.Generate(team);
            Assert.Greater(a.OpaqueCount(), 50);
            team.logoMotif = LogoMotif.Lantern;
            var b = LogoGenerator.Generate(team);
            int differ = 0;
            for (int i = 0; i < a.Pixels.Length; i++) if (!a.Pixels[i].Equals(b.Pixels[i])) differ++;
            Assert.Greater(differ, 10);
        }

        [Test]
        public void SeasonNinePass_AndWeekly_StartOnTheirDates()
        {
            Assert.AreEqual("cosmetic.pass.jersey.lantern", HoopsPass.GearFor(HoopsPass.SixSetsFrom, 5), "Season 8's season is unchanged");
            Assert.AreEqual("cosmetic.pass.jersey.rink", HoopsPass.GearFor(HoopsPass.SevenSetsFrom, 5));
            Assert.AreEqual("cosmetic.pass.celebration.skate_glide", HoopsPass.GearFor(HoopsPass.SevenSetsFrom, 20));
            Assert.AreEqual("cosmetic.pass.jersey.vapor_court", HoopsPass.GearFor(HoopsPass.SevenSetsFrom + 1, 5), "then the seven sets rotate");
            Assert.AreEqual(HoopsPass.SevenSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2027, 2, 22))));
            Assert.AreEqual(Weekly.Season9Week, Weekly.WeekOf(DailyChallenges.DayNumber(new System.DateTime(2027, 1, 4))));
            Assert.AreEqual(CelebrationKind.SkateGlide, Flair.CelebrationFor("cosmetic.pass.celebration.skate_glide"));
            bool arms = false, low = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
            {
                var p = Flair.Celebration(CelebrationKind.SkateGlide, t);
                arms |= p.ArmsUp;
                low |= p.Lift < 0;
            }
            Assert.IsTrue(arms && low);
            for (int week = Weekly.Season8Week; week < Weekly.Season9Week; week++)
                foreach (var g in Weekly.For(week)) Assert.AreNotEqual(WeeklyGoal.ClutchWins, g.Goal, "not before Season 9");
            bool seen = false;
            for (int week = Weekly.Season9Week; week < Weekly.Season9Week + 40 && !seen; week++)
                foreach (var g in Weekly.For(week)) seen |= g.Goal == WeeklyGoal.ClutchWins;
            Assert.IsTrue(seen, "the clutch goal turns up");
            var won = new MatchSummary { mode = GameMode.Clutch, humanTeam = 0, winner = 0 };
            var quick = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            var goal = new WeeklyChallenge { Goal = WeeklyGoal.ClutchWins, Target = 2 };
            Assert.AreEqual(1, Weekly.Amount(goal, won, false));
            Assert.AreEqual(0, Weekly.Amount(goal, quick, false));
        }
    }

    /// <summary>Phase 37: device-ready polish (frame pacing, store screenshots, text that the font can draw).</summary>
    public class Phase37DeviceTests
    {
        private static void Feed(FrameBudget b, float seconds, int fps, float each)
        {
            for (float t = 0f; t < seconds; t += each) b.Observe(each, fps, true);
        }

        [Test]
        public void FrameBudget_DropsTo60_WhenA120HzPhoneStruggles()
        {
            var b = new FrameBudget();
            Feed(b, 10f, 120, 1f / 120f);
            Assert.IsFalse(b.Struggling, "smooth 120 stays at 120");
            Feed(b, 5f, 120, 1f / 60f);
            Assert.IsTrue(b.Struggling, "every frame twice as long: drop to 60");
            Assert.AreEqual(1, b.Strikes);
        }

        [Test]
        public void FrameBudget_IgnoresMenus_SixtyHertz_AndOneHitch()
        {
            var b = new FrameBudget();
            for (int i = 0; i < 2000; i++) b.Observe(1f / 30f, 120, false);
            Assert.IsFalse(b.Struggling, "menus draw slowly on purpose");
            Feed(b, 10f, 60, 1f / 30f);
            Assert.IsFalse(b.Struggling, "60 Hz has nothing lower to go to");
            b.Observe(0.4f, 120, true);
            Feed(b, 8f, 120, 1f / 120f);
            Assert.IsFalse(b.Struggling, "one long frame isn't a pattern");
            b.Observe(2f, 120, true);
            Assert.IsFalse(b.Struggling, "a multi-second pause (loading, backgrounded) doesn't count");
        }

        [Test]
        public void FrameBudget_TriesAgainOnce_ThenStaysAt60()
        {
            var b = new FrameBudget();
            Feed(b, 5f, 120, 1f / 60f);
            Assert.IsTrue(b.Struggling);
            Feed(b, 30f, 60, 1f / 60f);
            Assert.IsTrue(b.Struggling, "waits a minute");
            Feed(b, 31f, 60, 1f / 60f);
            Assert.IsFalse(b.Struggling, "a calm minute at 60: try 120 again");
            Feed(b, 5f, 120, 1f / 60f);
            Assert.IsTrue(b.Struggling);
            Assert.AreEqual(2, b.Strikes);
            Feed(b, 200f, 60, 1f / 60f);
            Assert.IsTrue(b.Struggling, "second time: 60 for the rest of the session");
        }

        [Test]
        public void StoreShots_ParseAndGames()
        {
            var c = DefaultContent.Create();
            Assert.IsNull(StoreShots.Parse(null));
            Assert.IsNull(StoreShots.Parse(""));
            Assert.IsNull(StoreShots.Parse("nope"));
            Assert.AreEqual(StoreShotPlan.Clutch, StoreShots.Parse(" Clutch ").Plan);
            var names = new HashSet<string>();
            foreach (var s in StoreShots.All)
            {
                Assert.IsTrue(names.Add(s.Name));
                Assert.AreEqual(s.Name, StoreShots.Parse(s.Name).Name);
                Assert.GreaterOrEqual(s.Settle, 5);
            }
            foreach (bool full in new[] { false, true })
            {
                var r = StoreShots.GameRequest(c, full);
                Assert.AreEqual(GameMode.Demo, r.Mode);
                var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
                Assert.AreEqual(full, m.Setup.FullCourt);
                Assert.IsTrue(m.Setup.Demo, "AI on both sides");
                for (int i = 0; i < 60 * 10; i++) m.Step(1f / 60f, default);
                Assert.IsFalse(m.IsOver, "still going when the picture is taken");
            }
        }
    }

    /// <summary>Phase 37: Spanish coverage.</summary>
    public class Phase37SpanishTests
    {
        [Test]
        public void WeeklyGoals_HaveSpanish()
        {
            string before = Loc.Language;
            try
            {
                foreach (WeeklyGoal g in System.Enum.GetValues(typeof(WeeklyGoal)))
                {
                    var w = new WeeklyChallenge { Goal = g, Target = 3 };
                    Loc.Language = Loc.English;
                    string en = w.Describe();
                    Loc.Language = Loc.Spanish;
                    string es = w.Describe();
                    Assert.AreNotEqual(en, es, g.ToString());
                    Assert.IsTrue(es.Contains("3"), g.ToString());
                }
            }
            finally { Loc.Language = before; }
        }

        [Test]
        public void NewMenuLines_HaveSpanish()
        {
            foreach (var t in new[]
            {
                "CLUTCH", "STARS", "CHAPTER", "GOAL", "BONUS", "plays offline", "no ads", "WEEKLY CHALLENGES", "SHARE SHOT CHART",
                "Court. Stick on the left, shoot, pass and defense on the right.", "Court. Stick on the right, shoot, pass and defense on the left.",
                "The game is already on. Take over with the clock running down: win for a star, then go for the goal and the bonus.",
            })
                Assert.IsTrue(Loc.Has(t), t);
        }
    }
}
