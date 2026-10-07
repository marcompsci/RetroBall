using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 39: Season 11 (the Sky Kites).</summary>
    public class Phase39SeasonElevenTests
    {
        [Test]
        public void SkyKites_AreTheEleventhRival_WithStoryBadgesAndCourts()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
            var team = c.Team(DefaultContent.Rival11CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.AreEqual(LogoMotif.Kite, team.logoMotif);
            Assert.IsNotNull(c.Court("court.bluff_top"));
            Assert.IsNotNull(c.Court("court.pinwheel_lot"));
            Assert.IsNotNull(c.Player(DefaultContent.Rival11LeaderId));
            Assert.IsNotNull(c.Find(c.Cosmetics, "cosmetic.jersey.gust_yellow"));
            for (int season = 1; season <= 33; season++)
                Assert.AreEqual(season % 12 == 11, RivalEngine.RivalFor(season) == DefaultContent.Rival11CrewId, "season " + season);

            var d = Career.New(c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 11, currentWeek = 6 };
            Assert.AreEqual(Story.Rival11Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival11CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.kiteWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d));
            Story.MarkSeen(d, Story.Rival11Intro);
            Assert.AreEqual(Story.Rival11Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.kites").Earned(d));
            d.rival.wins = 11;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = d.rival.keeperWins = d.rival.courierWins = d.rival.lanternWins = d.rival.royalWins = d.rival.washWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllEleven(d));
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).rival.kiteWins);
            foreach (var id in new[] { Story.Rival11Intro, Story.Rival11Beaten })
            {
                Assert.Greater(Story.Beat(id, "ROOK", "en").Lines.Count, 1, id);
                Assert.Greater(Story.Beat(id, "ROOK", "es").Lines.Count, 1, id + " in Spanish");
            }
            Assert.IsTrue(Loc.Has(team.motto));
            Assert.IsTrue(Loc.Has(c.Court("court.bluff_top").description));
            Assert.IsTrue(Loc.Has(c.Court("court.pinwheel_lot").description));
        }

        [Test]
        public void KiteLogo_Draws_AndDiffersFromTheBubbles()
        {
            var team = DefaultContent.Create().Team(DefaultContent.Rival11CrewId);
            var a = LogoGenerator.Generate(team);
            Assert.Greater(a.OpaqueCount(), 50);
            team.logoMotif = LogoMotif.Bubbles;
            var b = LogoGenerator.Generate(team);
            int differ = 0;
            for (int i = 0; i < a.Pixels.Length; i++) if (!a.Pixels[i].Equals(b.Pixels[i])) differ++;
            Assert.Greater(differ, 10);
        }

        [Test]
        public void SeasonElevenPass_AndWeekly_StartOnTheirDates()
        {
            Assert.AreEqual("cosmetic.pass.jersey.fresh_press", HoopsPass.GearFor(HoopsPass.EightSetsFrom, 5), "Season 10's season is unchanged");
            Assert.AreEqual("cosmetic.pass.jersey.tailwind", HoopsPass.GearFor(HoopsPass.NineSetsFrom, 5));
            Assert.AreEqual("cosmetic.pass.celebration.kite_run", HoopsPass.GearFor(HoopsPass.NineSetsFrom, 20));
            Assert.AreEqual("cosmetic.pass.jersey.fairway", HoopsPass.GearFor(HoopsPass.NineSetsFrom + 1, 5), "Phase 40: Season 12's set comes next");
            Assert.AreEqual(HoopsPass.NineSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2027, 5, 17))));
            Assert.AreEqual(Weekly.Season11Week, Weekly.WeekOf(DailyChallenges.DayNumber(new System.DateTime(2027, 3, 1))));
            Assert.AreEqual(CelebrationKind.KiteRun, Flair.CelebrationFor("cosmetic.pass.celebration.kite_run"));
            bool arms = false, low = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
            {
                var p = Flair.Celebration(CelebrationKind.KiteRun, t);
                arms |= p.ArmsUp;
                low |= p.Lift < 0;
            }
            Assert.IsTrue(arms && low);
            for (int week = Weekly.Season10Week; week < Weekly.Season11Week; week++)
                foreach (var g in Weekly.For(week)) Assert.AreNotEqual(WeeklyGoal.CleanWins, g.Goal, "not before Season 11");
            bool seen = false;
            for (int week = Weekly.Season11Week; week < Weekly.Season11Week + 40 && !seen; week++)
                foreach (var g in Weekly.For(week)) seen |= g.Goal == WeeklyGoal.CleanWins;
            Assert.IsTrue(seen, "the clean-wins goal turns up");
            var goal = new WeeklyChallenge { Goal = WeeklyGoal.CleanWins, Target = 2 };
            var clean = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            clean.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine() });
            var sloppy = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            sloppy.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine { turnovers = 1 } });
            var lost = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 1 };
            lost.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine() });
            Assert.AreEqual(1, Weekly.Amount(goal, clean, false));
            Assert.AreEqual(0, Weekly.Amount(goal, sloppy, false));
            Assert.AreEqual(0, Weekly.Amount(goal, lost, false));
        }
    }


    /// <summary>Phase 39: onboarding, the NEXT suggestion and the welcome-back bonus.</summary>
    public class Phase39OnboardingTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        [Test]
        public void FirstGame_IsAQuickCallOnRookie()
        {
            var r = Onboarding.FirstGame(C);
            Assert.AreEqual(GameMode.QuickCall, r.Mode);
            Assert.AreEqual("difficulty.rookie", r.DifficultyId);
            Assert.IsNotNull(C.Difficulty(r.DifficultyId));
            var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
            Assert.AreEqual(0, m.Score[0]);
        }

        [Test]
        public void Next_WalksANewPlayerThroughTheModes()
        {
            var d = Career.New(C);
            int today = 900;
            Assert.AreEqual(NextAction.Tutorial, Onboarding.Next(d, today).Action);
            d.tutorialDone = true;
            Assert.AreEqual(NextAction.QuickCall, Onboarding.Next(d, today).Action);
            d.totals.games = 1;
            Assert.AreEqual(NextAction.Rise, Onboarding.Next(d, today).Action);
            d.rise.circuitBeaten.Add("crew.cage_regulars");
            d.totals.games = 3;
            Assert.AreEqual(NextAction.Clutch, Onboarding.Next(d, today).Action);
            d.clutch.played = 1;
            Assert.AreEqual(NextAction.DailyClutch, Onboarding.Next(d, today).Action);
            d.clutch.dailyDay = today;
            Assert.AreEqual(NextAction.Daily, Onboarding.Next(d, today).Action);
            d.daily.lastCompletedDay = today;
            d.weekly.week = Weekly.WeekOf(today);
            d.weekly.done = new List<bool> { true, false, true };
            Assert.AreEqual(NextAction.Weekly, Onboarding.Next(d, today).Action);
            d.weekly.done = new List<bool> { true, true, true };
            Assert.AreEqual(NextAction.Park, Onboarding.Next(d, today).Action);
            d.street.wins = 1;
            Assert.AreEqual(NextAction.Franchise, Onboarding.Next(d, today).Action);
            Assert.AreEqual(NextAction.DailyClutch, Onboarding.Next(d, today + 1).Action, "a new day brings the daily back");
            Assert.IsNotNull(Onboarding.Next(null, today));
        }

        [Test]
        public void Login_PaysOncePerDay_AndGrowsOverAWeek()
        {
            Assert.AreEqual(20, Onboarding.LoginReward(1));
            Assert.AreEqual(80, Onboarding.LoginReward(6) + 10);
            Assert.AreEqual(80 + Onboarding.LoginWeekBonusSp, Onboarding.LoginReward(7));
            Assert.AreEqual(80, Onboarding.LoginReward(8));
            var d = new CareerSaveData();
            int sp = 0;
            for (int day = 50; day < 57; day++) sp += Onboarding.ClaimLogin(d, day);
            Assert.AreEqual(20 + 30 + 40 + 50 + 60 + 70 + 80 + 100, sp);
            Assert.AreEqual(sp, d.signalPoints);
            Assert.AreEqual(0, Onboarding.ClaimLogin(d, 56), "once a day");
            Assert.AreEqual(0, Onboarding.ClaimLogin(d, 40), "the clock moved back");
            Assert.AreEqual(7, d.loginStreak);
            Assert.AreEqual(20, Onboarding.ClaimLogin(d, 60), "a missed day starts again");
            Assert.AreEqual(1, d.loginStreak);
            Assert.AreEqual(7, d.loginBest);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(60, back.loginDay);
            Assert.AreEqual(1, back.loginStreak);
            Assert.AreEqual(7, back.loginBest);
        }

        [Test]
        public void ControlFeel_Steps_AndSaves()
        {
            var s = new SettingsData();
            Assert.AreEqual(130f, ControlFeel.RadiusFor(s), "NORMAL keeps the old stick");
            Assert.AreEqual(0.12f, ControlFeel.DeadZoneFor(s), 1e-6f);
            s.stickSize = 2; s.stickDeadZone = 0;
            Assert.Greater(ControlFeel.RadiusFor(s), 130f);
            Assert.Less(ControlFeel.DeadZoneFor(s), 0.12f);
            Assert.AreEqual(1, ControlFeel.ImpactStyle(1, 1), "NORMAL plays as before");
            Assert.AreEqual(0, ControlFeel.ImpactStyle(1, 0));
            Assert.AreEqual(2, ControlFeel.ImpactStyle(1, 2));
            Assert.AreEqual(0, ControlFeel.ImpactStyle(0, 0), "never softer than light");
            Assert.AreEqual(2, ControlFeel.ImpactStyle(2, 2), "never firmer than heavy");
            var d = new CareerSaveData();
            d.settings.stickSize = 0; d.settings.stickDeadZone = 2; d.settings.hapticStrength = 2;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(0, back.settings.stickSize);
            Assert.AreEqual(2, back.settings.stickDeadZone);
            Assert.AreEqual(2, back.settings.hapticStrength);
            var fresh = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), C, out _);
            Assert.AreEqual(1, fresh.settings.stickSize);
            foreach (var n in ControlFeel.SizeNames) Assert.IsTrue(Loc.Has(n), n);
            foreach (var n in ControlFeel.DeadZoneNames) Assert.IsTrue(Loc.Has(n), n);
            foreach (var n in ControlFeel.HapticNames) Assert.IsTrue(Loc.Has(n), n);
        }
    }
}
