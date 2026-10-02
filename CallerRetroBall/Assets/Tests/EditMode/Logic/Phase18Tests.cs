using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 18: power policy, frame stats, coach tips, Season 3 content, new flair and codes.</summary>
    public class PowerAndPerfTests
    {
        [Test]
        public void Policy_SavesPowerOnLowPowerOrHeat_And120OnlyWhenAllowed()
        {
            Assert.IsFalse(PowerPolicy.ShouldSave(false, 0));
            Assert.IsFalse(PowerPolicy.ShouldSave(false, 1));
            Assert.IsTrue(PowerPolicy.ShouldSave(false, 2));
            Assert.IsTrue(PowerPolicy.ShouldSave(true, 0));
            Assert.AreEqual(120, PowerPolicy.TargetFrameRate(true, 120, false));
            Assert.AreEqual(60, PowerPolicy.TargetFrameRate(true, 120, true));
            Assert.AreEqual(60, PowerPolicy.TargetFrameRate(true, 60, false));
            Assert.AreEqual(60, PowerPolicy.TargetFrameRate(false, 120, false));
        }

        [Test]
        public void FrameStats_RollingAverageAndWorst()
        {
            var f = new FrameStats(4);
            Assert.AreEqual(0, f.Fps);
            foreach (var dt in new[] { 1f / 60f, 1f / 60f, 1f / 30f, 1f / 60f }) f.Add(dt);
            Assert.AreEqual(33.3, f.WorstMs, 0.2);
            Assert.AreEqual(20.8, f.AverageMs, 0.2);
            for (int i = 0; i < 4; i++) f.Add(1f / 60f);
            Assert.AreEqual(60, f.Fps, "old samples roll out of the window");
        }

        [Test]
        public void BurstFill_MatchesCreate_WithoutAllocating()
        {
            var a = Bursts.Create(9, 5, 3f, 0.5f);
            var buf = new BurstParticle[10];
            Bursts.Fill(9, buf, 5, 3f, 0.5f);
            for (int i = 0; i < 5; i++) Assert.AreEqual(a[i].vx, buf[i].vx, 1e-6);
        }
    }

    public class CoachTipTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Tips_ShowOnceEach_SpacedOut_OnlyInEarlyGames()
        {
            var d = Career.New(_c);
            float last = -99f;
            var s = new TipSituation { Live = true, HumanHasBall = true, MatchTime = 5f, HeatThreshold = 3 };
            var tip = CoachTips.Next(d, s, 10f, ref last);
            Assert.AreEqual("tip.meter", tip.Id);
            Assert.IsNull(CoachTips.Next(d, new TipSituation { LastShotTooEarly = true }, 12f, ref last), "too soon after the last tip");
            Assert.AreEqual("tip.early", CoachTips.Next(d, new TipSituation { LastShotTooEarly = true }, 30f, ref last).Id);
            Assert.IsNull(CoachTips.Next(d, s, 60f, ref last), "the meter tip was already shown");
            d.totals.games = CoachTips.MaxGames;
            Assert.IsFalse(CoachTips.Active(d));
            d.totals.games = 0;
            d.settings.coachTips = false;
            Assert.IsFalse(CoachTips.Active(d));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsFalse(back.settings.coachTips);
            Assert.AreEqual(2, back.tipsSeen.Count);
        }

        [Test]
        public void EveryTip_HasSpanish()
        {
            foreach (var t in CoachTips.All) Assert.IsTrue(Loc.Has(t.Text), t.Text);
        }
    }

    public class Season3Tests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Content_HasTheThirdRival_Courts_Kits_AndValidates()
        {
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid, ContentValidator.Validate(_c).ToString());
            Assert.AreEqual(TeamTier.Rival, _c.Team(DefaultContent.Rival3CrewId).tier);
            Assert.IsNotNull(_c.Player(DefaultContent.Rival3LeaderId));
            foreach (var id in new[] { "court.ferry_deck", "court.lantern_market", "court.canyon_rim" }) Assert.IsNotNull(_c.Court(id), id);
            foreach (var id in new[] { "cosmetic.celebration.pixel_wave", "cosmetic.celebration.take_a_bow", "cosmetic.move.double_cross", "cosmetic.move.step_back" })
                Assert.IsNotNull(_c.Find(_c.Cosmetics, id), id);
        }

        [Test]
        public void NewCelebrationsAndMoves_AreWiredAndAnimate()
        {
            Assert.AreEqual(CelebrationKind.PixelWave, Flair.CelebrationFor("cosmetic.celebration.pixel_wave"));
            Assert.AreEqual(CelebrationKind.TakeABow, Flair.CelebrationFor("cosmetic.celebration.take_a_bow"));
            Assert.AreEqual(DribbleMoveKind.DoubleCross, Flair.DribbleMoveFor("cosmetic.move.double_cross"));
            Assert.AreEqual(DribbleMoveKind.StepBack, Flair.DribbleMoveFor("cosmetic.move.step_back"));
            bool moved = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
                if (!Flair.Celebration(CelebrationKind.PixelWave, t).IsNone && !Flair.Celebration(CelebrationKind.TakeABow, t).IsNone) moved = true;
            Assert.IsTrue(moved);
            Assert.IsFalse(Flair.DribbleMove(DribbleMoveKind.DoubleCross, 0.1f).IsNone);
            Assert.IsFalse(Flair.DribbleMove(DribbleMoveKind.StepBack, 0.1f).IsNone);
            Assert.IsTrue(Flair.DribbleMove(DribbleMoveKind.StepBack, 5f).IsNone);
        }

        [Test]
        public void TheTide_IsTracked_AndChapterThreePlays()
        {
            var d = Career.New(_c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 3, currentWeek = 6 };
            Assert.AreEqual(Story.Rival3Intro, Story.Pending(d));
            Assert.AreEqual(DefaultContent.Rival3CrewId, RivalEngine.Challenge(d, _c, "difficulty.caller").AwayTeamId);
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival3CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.tideWins);
            Story.MarkSeen(d, Story.Rival3Intro);
            Assert.AreEqual(Story.Rival3Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.tide").Earned(d));
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), _c, out _).rival.tideWins);
            foreach (var id in new[] { Story.Rival3Intro, Story.Rival3Beaten, Story.ThreePeat, Story.Welcome })
                Assert.AreEqual(Story.Beat(id, "Rook", Loc.English).Lines.Count, Story.Beat(id, "Rook", Loc.Spanish).Lines.Count, id);
        }

        [Test]
        public void NewCodes_Toggle_AndHintsComeFromPlaying()
        {
            var d = Career.New(_c);
            Assert.AreEqual(CodeResult.Toggled, Secrets.Enter(d.secrets, Secrets.Find(Secrets.PocketGreen).Sequence, out _));
            Assert.IsTrue(Secrets.IsOn(d.secrets, Secrets.PocketGreen));
            Secrets.Enter(d.secrets, Secrets.Find(Secrets.SkyHigh).Sequence, out _);
            Assert.IsTrue(Secrets.IsOn(d.secrets, Secrets.SkyHigh));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsTrue(back.secrets.pocketGreen && back.secrets.skyHigh);
            d.practice.horseWins = 1;
            d.totals.alleyOops = 5;
            var revealed = Secrets.RevealHints(d);
            Assert.IsTrue(revealed.Exists(x => x.Id == Secrets.PocketGreen));
            Assert.IsTrue(revealed.Exists(x => x.Id == Secrets.SkyHigh));
        }
    }
}
