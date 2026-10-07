using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 38: Season 10 (the Wash House).</summary>
    public class Phase38SeasonTenTests
    {
        [Test]
        public void WashHouse_IsTheTenthRival_WithStoryBadgesAndCourts()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
            var team = c.Team(DefaultContent.Rival10CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.AreEqual(LogoMotif.Bubbles, team.logoMotif);
            Assert.IsNotNull(c.Court("court.suds_alley"));
            Assert.IsNotNull(c.Court("court.coin_op"));
            Assert.IsNotNull(c.Player(DefaultContent.Rival10LeaderId));
            Assert.IsNotNull(c.Find(c.Cosmetics, "cosmetic.jersey.rinse_blue"));
            for (int season = 1; season <= 30; season++)
                Assert.AreEqual(season % 10 == 0, RivalEngine.RivalFor(season) == DefaultContent.Rival10CrewId, "season " + season);

            var d = Career.New(c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 10, currentWeek = 6 };
            Assert.AreEqual(Story.Rival10Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival10CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.washWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d));
            Story.MarkSeen(d, Story.Rival10Intro);
            Assert.AreEqual(Story.Rival10Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.wash").Earned(d));
            d.rival.wins = 10;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = d.rival.keeperWins = d.rival.courierWins = d.rival.lanternWins = d.rival.royalWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllTen(d));
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).rival.washWins);
            foreach (var id in new[] { Story.Rival10Intro, Story.Rival10Beaten })
            {
                Assert.Greater(Story.Beat(id, "ROOK", "en").Lines.Count, 1, id);
                Assert.Greater(Story.Beat(id, "ROOK", "es").Lines.Count, 1, id + " in Spanish");
            }
            Assert.IsTrue(Loc.Has(team.motto));
            Assert.IsTrue(Loc.Has(c.Court("court.suds_alley").description));
            Assert.IsTrue(Loc.Has(c.Court("court.coin_op").description));
        }

        [Test]
        public void BubblesLogo_Draws_AndDiffersFromTheSkate()
        {
            var team = DefaultContent.Create().Team(DefaultContent.Rival10CrewId);
            var a = LogoGenerator.Generate(team);
            Assert.Greater(a.OpaqueCount(), 50);
            team.logoMotif = LogoMotif.Skate;
            var b = LogoGenerator.Generate(team);
            int differ = 0;
            for (int i = 0; i < a.Pixels.Length; i++) if (!a.Pixels[i].Equals(b.Pixels[i])) differ++;
            Assert.Greater(differ, 10);
        }

        [Test]
        public void SeasonTenPass_AndWeekly_StartOnTheirDates()
        {
            Assert.AreEqual("cosmetic.pass.jersey.rink", HoopsPass.GearFor(HoopsPass.SevenSetsFrom, 5), "Season 9's season is unchanged");
            Assert.AreEqual("cosmetic.pass.jersey.fresh_press", HoopsPass.GearFor(HoopsPass.EightSetsFrom, 5));
            Assert.AreEqual("cosmetic.pass.celebration.tumble_dry", HoopsPass.GearFor(HoopsPass.EightSetsFrom, 20));
            Assert.AreEqual("cosmetic.pass.jersey.vapor_court", HoopsPass.GearFor(HoopsPass.EightSetsFrom + 1, 5), "then the eight sets rotate");
            Assert.AreEqual(HoopsPass.EightSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2027, 4, 5))));
            Assert.AreEqual(Weekly.Season10Week, Weekly.WeekOf(DailyChallenges.DayNumber(new System.DateTime(2027, 2, 1))));
            Assert.AreEqual(CelebrationKind.TumbleDry, Flair.CelebrationFor("cosmetic.pass.celebration.tumble_dry"));
            bool arms = false, low = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
            {
                var p = Flair.Celebration(CelebrationKind.TumbleDry, t);
                arms |= p.ArmsUp;
                low |= p.Lift < 0;
            }
            Assert.IsTrue(arms && low);
            for (int week = Weekly.Season9Week; week < Weekly.Season10Week; week++)
                foreach (var g in Weekly.For(week)) Assert.AreNotEqual(WeeklyGoal.Stops, g.Goal, "not before Season 10");
            bool seen = false;
            for (int week = Weekly.Season10Week; week < Weekly.Season10Week + 40 && !seen; week++)
                foreach (var g in Weekly.For(week)) seen |= g.Goal == WeeklyGoal.Stops;
            Assert.IsTrue(seen, "the stops goal turns up");
            var s = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            s.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine { steals = 3, blocks = 2 } });
            Assert.AreEqual(5, Weekly.Amount(new WeeklyChallenge { Goal = WeeklyGoal.Stops, Target = 10 }, s, false));
        }
    }

    /// <summary>Phase 38: DAILY CLUTCH, the CLUTCH editor and share codes, the CLUTCH board.</summary>
    public class Phase38ClutchTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        [Test]
        public void Daily_IsTheSameAllDay_AndVaries()
        {
            Assert.AreEqual(Clutch.DailyFor(500).Id, Clutch.DailyFor(500).Id);
            var seen = new HashSet<string>();
            for (int day = 0; day < 60; day++) seen.Add(Clutch.DailyFor(day).Id);
            Assert.Greater(seen.Count, 10, "most scenarios come round within two months");
            var r = Clutch.DailyRequest(C, 700, DefaultContent.DefaultDifficultyId);
            Assert.AreEqual(700, Clutch.DailyDayOf(r.ContextId));
            Assert.AreEqual(Clutch.DailyFor(700).Id, Clutch.FromContext(r.ContextId).Id);
            Assert.AreEqual(-1, Clutch.DailyDayOf(Clutch.Request(C, Clutch.All[0], DefaultContent.DefaultDifficultyId).ContextId));
            Assert.AreEqual(-1, Clutch.DailyDayOf("clutch:ct_down2@dx"));
        }

        [Test]
        public void Daily_PaysOncePerDay_AndCountsARun()
        {
            var d = new ClutchSaveData();
            var career = new CareerSaveData();
            Assert.AreEqual(0, Clutch.ApplyDaily(d, 100, 100, false, career), "a loss pays nothing");
            Assert.AreEqual(Clutch.DailyBonusSp, Clutch.ApplyDaily(d, 100, 100, true, career));
            Assert.AreEqual(0, Clutch.ApplyDaily(d, 100, 100, true, career), "once a day");
            Assert.AreEqual(1, d.dailyStreak);
            Clutch.ApplyDaily(d, 101, 101, true, career);
            Clutch.ApplyDaily(d, 102, 102, true, career);
            Assert.AreEqual(3, d.dailyStreak);
            Assert.AreEqual(0, Clutch.ApplyDaily(d, 102, 104, true, career), "yesterday's game finished late doesn't count today");
            Clutch.ApplyDaily(d, 105, 105, true, career);
            Assert.AreEqual(1, d.dailyStreak, "a missed day starts the run again");
            Assert.AreEqual(3, d.dailyBest);
            Assert.AreEqual(4 * Clutch.DailyBonusSp, career.signalPoints);
            Assert.IsTrue(Clutch.DailyDone(d, 105));
            Assert.AreEqual(0, Clutch.ApplyDaily(d, 104, 104, true, career), "the clock moved back a day: no second bonus");
            Assert.AreEqual(1, Clutch.DailyRun(d, 106), "yesterday's win keeps the run alive today");
            Assert.AreEqual(0, Clutch.DailyRun(d, 107), "a whole missed day shows the run as broken");
            var back = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData { clutch = d }), C, out _);
            Assert.AreEqual(105, back.clutch.dailyDay);
            Assert.AreEqual(1, back.clutch.dailyStreak);
            Assert.AreEqual(3, back.clutch.dailyBest);
        }

        [Test]
        public void Codes_RoundTrip_EveryField()
        {
            var league = C.TeamsInTier(TeamTier.League);
            var s = Clutch.Make(league[3].id, league[6].id, true, 85, 101, 97, false, ClutchGoal.Assists, 3, ClutchGoal.Blocks, 2);
            string code = Clutch.Encode(C, s);
            Assert.AreEqual(Clutch.CodeLength, code.Length);
            Assert.IsTrue(Clutch.TryDecode(C, Clutch.Pretty(code).ToLowerInvariant(), out var back, out string why), why);
            Assert.AreEqual(s.YourTeamId, back.YourTeamId);
            Assert.AreEqual(s.TheirTeamId, back.TheirTeamId);
            Assert.AreEqual(s.FullCourt, back.FullCourt);
            Assert.AreEqual(85, back.Clock);
            Assert.AreEqual(101, back.ScoreFor);
            Assert.AreEqual(97, back.ScoreAgainst);
            Assert.AreEqual(false, back.YourBall);
            Assert.AreEqual(ClutchGoal.Assists, back.Goal);
            Assert.AreEqual(3, back.GoalValue);
            Assert.AreEqual(ClutchGoal.Blocks, back.Bonus);
            Assert.AreEqual(2, back.BonusValue);
            Assert.IsTrue(back.Custom);
            Assert.AreEqual(code, Clutch.Encode(C, back));
        }

        [Test]
        public void Codes_RejectTyposAndJunk()
        {
            var league = C.TeamsInTier(TeamTier.League);
            string code = Clutch.Encode(C, Clutch.Make(league[0].id, league[1].id, false, 30, 18, 20, true, ClutchGoal.Threes, 1, ClutchGoal.NoTurnovers, 0));
            int rejected = 0;
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            for (int i = 0; i < code.Length; i++)
            {
                var chars = code.ToCharArray();
                chars[i] = alphabet[(alphabet.IndexOf(chars[i]) + 7) % 32];
                if (!Clutch.TryDecode(C, new string(chars), out _, out _)) rejected++;
            }
            Assert.GreaterOrEqual(rejected, code.Length - 1, "a changed character is almost always caught");
            Assert.IsFalse(Clutch.TryDecode(C, "", out _, out _));
            Assert.IsFalse(Clutch.TryDecode(C, "HELLO", out _, out _));
            Assert.IsFalse(Clutch.TryDecode(C, code + "0", out _, out _));
            Assert.IsNull(Clutch.Encode(C, Clutch.Make("team.nope", league[1].id, false, 30, 1, 2, true, ClutchGoal.Win, 0, ClutchGoal.Steals, 1)));
            Assert.IsNull(Clutch.Encode(C, Clutch.Make(league[1].id, league[1].id, false, 30, 1, 2, true, ClutchGoal.Win, 0, ClutchGoal.Steals, 1)), "both sides the same team");
            Assert.IsFalse(Clutch.TryDecode(C, code.Substring(0, 12) + "Z", out _, out _), "unused high bits must be zero");
            var clamped = Clutch.Make(league[0].id, league[1].id, false, 999, 500, -3, true, ClutchGoal.WinBy, 99, ClutchGoal.Steals, 1);
            Assert.AreEqual(Clutch.CustomMaxClock, clamped.Clock);
            Assert.AreEqual(Clutch.CustomMaxScore, clamped.ScoreFor);
            Assert.AreEqual(0, clamped.ScoreAgainst);
            Assert.AreEqual(31, clamped.GoalValue);
        }

        [Test]
        public void Custom_Plays_ButKeepsNoStars()
        {
            var league = C.TeamsInTier(TeamTier.League);
            var s = Clutch.Make(league[2].id, league[4].id, false, 20, 10, 12, true, ClutchGoal.WinBy, 1, ClutchGoal.Assists, 0);
            var r = Clutch.CustomRequest(C, s, DefaultContent.DefaultDifficultyId);
            var back = Clutch.FromContext(r.ContextId);
            Assert.IsNotNull(back);
            Assert.IsTrue(back.Custom);
            Assert.IsTrue(Clutch.IsCustomContext(r.ContextId));
            Assert.IsFalse(Clutch.IsCustomContext(Clutch.DailyRequest(C, 5, DefaultContent.DefaultDifficultyId).ContextId));
            Assert.AreEqual(back.Clock, Clutch.FromContext(r.ContextId, C).Clock);
            var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
            Assert.AreEqual(10, m.Score[0]);
            Assert.AreEqual(12, m.Score[1]);
            Assert.AreEqual(20f, m.GameClock, 0.01f);

            var d = new ClutchSaveData();
            var career = new CareerSaveData();
            var sum = new MatchSummary { mode = GameMode.Clutch, scoreA = 14, scoreB = 12, humanTeam = 0, winner = 0 };
            sum.lines.Add(new SummaryLine { isHuman = true, team = 0, stats = new PlayerStatLine() });
            Assert.AreEqual(ClutchOutcome.Won, Clutch.ApplyResult(d, back, sum, career, out int stars, out int fresh));
            Assert.AreEqual(3, stars);
            Assert.AreEqual(0, fresh);
            Assert.AreEqual(0, career.signalPoints);
            Assert.AreEqual(0, d.played);
            Assert.AreEqual(0, Clutch.TotalStars(d));
        }

        [Test]
        public void ClutchBoard_ReportsTotalStars()
        {
            var board = Achievements.Boards.Find(b => b.Id == Clutch.LeaderboardId);
            Assert.IsNotNull(board);
            var d = new CareerSaveData();
            d.clutch.stars.Add("ct_down2:3");
            d.clutch.stars.Add("iv_last:2");
            Assert.AreEqual(5, board.Score(d));
        }

        [Test]
        public void EditorLines_HaveSpanish()
        {
            foreach (var t in new[] { "DAILY CLUTCH", "MAKE YOUR OWN", "COPY CODE", "HOLD THEM TO", "JUST WIN", "That code has a typo.", "Custom scenario: stars aren't saved." })
                Assert.IsTrue(Loc.Has(t), t);
        }
    }

    /// <summary>
    /// Phase 38 gameplay feel: CLUTCH chapters get harder. Each scenario is played by the AI on both sides (coach mode:
    /// your team runs itself) at Caller difficulty. Phase 38 report, 24 seeds: chapter win rates about 49% / 43% / 35%
    /// after retuning five scenarios (PROTECT THE LEAD was 96%, EIGHT DOWN 0%).
    /// </summary>
    public class Phase38ClutchBalanceTests
    {
        [Test]
        public void Chapters_GetHarder_WithTheAiPlayingForYou()
        {
            var c = DefaultContent.Create();
            var rate = new float[Clutch.ChapterNames.Length];
            const int seeds = 12;
            foreach (var s in Clutch.All)
            {
                int wins = 0;
                for (uint k = 1; k <= seeds; k++)
                {
                    var r = Clutch.Request(c, s, "difficulty.caller");
                    r.Coach = true;
                    r.Seed = k * 7919;
                    var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
                    int g = 0;
                    while (!m.IsOver && g++ < 60 * 400) m.Step(1f / 60f, default);
                    Assert.IsTrue(m.IsOver, s.Id + " finishes");
                    if (m.Winner == 0) wins++;
                }
                rate[s.Chapter] += wins / (float)(seeds * 6);
            }
            Assert.Greater(rate[0], rate[1], "chapter 2 is harder than chapter 1");
            Assert.Greater(rate[1], rate[2], "chapter 3 is harder than chapter 2");
            Assert.Greater(rate[2], 0.15f, "chapter 3 is still winnable");
            Assert.Less(rate[0], 0.8f, "chapter 1 still asks something of you");
        }
    }

    /// <summary>Phase 38: the shot meter shows a contest coming.</summary>
    public class Phase38ShotFeelTests
    {
        [Test]
        public void ContestLevel_RisesAsADefenderClosesOut()
        {
            var c = DefaultContent.Create();
            var m = new MatchSimulation(MatchSetup.FromRequest(MatchRequest.QuickCallDefault(c), c));
            int shooter = m.Index(0, 0);
            var spot = new Vec2(0f, 5f);
            m.Players[shooter].Motion = new MotionState(spot);
            for (int i = 0; i < m.Players.Length; i++)
                if (m.Players[i].Team == 1) m.Players[i].Motion = new MotionState(new Vec2(-6f + i, 0.5f));
            Assert.AreEqual(0f, m.ContestLevel(shooter), "nobody near: open");
            Assert.AreEqual(0f, ShotFeel.ContestStep(m.ContestLevel(shooter)));
            int defender = m.Index(1, 0);
            float last = -1f;
            foreach (float gap in new[] { 1.6f, 1.1f, 0.6f, 0.2f })
            {
                m.Players[defender].Motion = new MotionState(new Vec2(spot.x + gap, spot.y));
                float level = m.ContestLevel(shooter);
                Assert.Greater(level, last, "closer is more contested (gap " + gap + ")");
                last = level;
            }
            Assert.AreEqual(1f, ShotFeel.ContestStep(last), "in your face: full contest colour");
            Assert.AreEqual(0.5f, ShotFeel.ContestStep(0.3f));
            Assert.AreEqual(0f, m.ContestLevel(-1));
        }

        [Test]
        public void ContestLevel_DoesNotChangeTheGame()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Mode = GameMode.Demo;
            r.Seed = 4321;
            var a = new MatchSimulation(MatchSetup.FromRequest(r, c));
            var b = new MatchSimulation(MatchSetup.FromRequest(r, c));
            for (int i = 0; i < 60 * 40; i++)
            {
                a.Step(1f / 60f, default);
                for (int k = 0; k < b.Players.Length; k++) b.ContestLevel(k);
                b.Step(1f / 60f, default);
            }
            Assert.AreEqual(a.Score[0], b.Score[0]);
            Assert.AreEqual(a.Score[1], b.Score[1]);
            Assert.AreEqual(a.Ball.Position.x, b.Ball.Position.x, 1e-6f, "same game, frame for frame");
        }
    }
}

