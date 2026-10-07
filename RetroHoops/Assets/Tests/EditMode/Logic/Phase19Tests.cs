using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 19: Game Center catalogue, iCloud save sync, colour filters, Shootout duel, Season 4.</summary>
    public class GameCenterCatalogTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Achievements_FitAppleLimits()
        {
            var ids = new HashSet<string>();
            foreach (var a in Achievements.All)
            {
                Assert.IsTrue(ids.Add(a.Id), a.Id);
                Assert.IsTrue(a.Id.StartsWith("retroball.ach."), a.Id);
                Assert.IsNotEmpty(a.Title);
                Assert.IsNotEmpty(a.Description);
                Assert.IsTrue(a.Points > 0 && a.Points <= 100, a.Id + " points");
            }
            Assert.LessOrEqual(Achievements.TotalPoints, 1000, "Game Center allows 1,000 points per game");
            Assert.LessOrEqual(Achievements.All.Count, 100);
            Assert.AreEqual(Achievements.All.Count, Achievements.AllAchievements.Length);
        }

        [Test]
        public void Leaderboards_AreUnique_AndOnlyReportRealScores()
        {
            var ids = new HashSet<string>();
            foreach (var b in Achievements.Boards) Assert.IsTrue(ids.Add(b.Id), b.Id);
            var d = Career.New(_c);
            foreach (var kv in Achievements.Scores(d)) Assert.AreEqual(0, kv.Value, kv.Key + " starts at zero (never sent)");
            d.practice.aroundWorldTime = 41.237f;
            d.king.best = 6;
            var scores = new Dictionary<string, long>();
            foreach (var kv in Achievements.Scores(d)) scores[kv.Key] = kv.Value;
            Assert.AreEqual(4124, scores[Achievements.BoardAroundWorld], "times are hundredths of a second");
            Assert.AreEqual(6, scores[Achievements.BoardKing]);
            Assert.IsTrue(Achievements.Boards.Find(b => b.Id == Achievements.BoardAroundWorld).LowIsBetter);
        }

        [Test]
        public void FreshCareer_EarnsNoAchievement_AndPartialSavesDontThrow()
        {
            Assert.AreEqual(0, Achievements.Earned(Career.New(_c)).Count);
            var d = Career.New(_c);
            d.totals.versusGames = 1;
            d.totals.heatUps = 1;
            var earned = Achievements.Earned(d);
            Assert.Contains(Achievements.CouchGame, earned);
            Assert.Contains(Achievements.HeatCheck, earned);
        }
    }

    public class CloudSaveTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Wrap_RoundTrips_WithMetadata()
        {
            var d = Career.New(_c);
            d.nickname = "Kai";
            d.totals.games = 12;
            d.signalPoints = 777;
            string value = CloudSave.Wrap(d, 1790000000L, "iPhone17,2");
            Assert.IsNotNull(value);
            Assert.IsTrue(CloudSave.TryUnwrap(value, _c, out var back, out long at, out string device));
            Assert.AreEqual("Kai", back.nickname);
            Assert.AreEqual(12, back.totals.games);
            Assert.AreEqual(777, back.signalPoints);
            Assert.AreEqual(1790000000L, at);
            Assert.AreEqual("iPhone17,2", device);
        }

        [Test]
        public void Unwrap_RejectsJunk_AndOtherFormats()
        {
            Assert.IsFalse(CloudSave.TryUnwrap(null, _c, out _, out _, out _));
            Assert.IsFalse(CloudSave.TryUnwrap("", _c, out _, out _, out _));
            Assert.IsFalse(CloudSave.TryUnwrap("not json", _c, out _, out _, out _));
            Assert.IsFalse(CloudSave.TryUnwrap("{\"v\":99,\"data\":\"{}\"}", _c, out _, out _, out _));
            Assert.IsFalse(CloudSave.TryUnwrap("{\"v\":1,\"data\":\"garbage\"}", _c, out _, out _, out _));
        }

        [Test]
        public void TheCareerFurtherAlongWins_AndSettingsStayPerDevice()
        {
            var local = Career.New(_c);
            var cloud = Career.New(_c);
            Assert.AreEqual(CloudChoice.KeepLocal, CloudSave.Choose(local, null));
            Assert.AreEqual(CloudChoice.KeepLocal, CloudSave.Choose(local, cloud), "level careers don't swap");
            cloud.totals.games = 3;
            Assert.AreEqual(CloudChoice.UseCloud, CloudSave.Choose(local, cloud));
            local.totals.games = 5;
            Assert.AreEqual(CloudChoice.KeepLocal, CloudSave.Choose(local, cloud), "never roll progress back");
            cloud.totals.games = 5;
            cloud.badgesSeen.Add("badge.first_win");
            Assert.AreEqual(CloudChoice.UseCloud, CloudSave.Choose(local, cloud), "ties broken by story, badges, codes");

            local.settings.sfxVolume = 0.2f;
            local.settings.leftHanded = true;
            cloud.settings.leftHanded = false;
            var adopted = CloudSave.Adopt(cloud, local);
            Assert.AreEqual(5, adopted.totals.games);
            Assert.IsTrue(adopted.settings.leftHanded);
            Assert.AreEqual(0.2f, adopted.settings.sfxVolume, 0.001f);
        }

        [Test]
        public void NewSettings_RoundTrip_AndDefaultOn()
        {
            var d = Career.New(_c);
            Assert.IsTrue(d.settings.icloudSync, "iCloud sync is on by default");
            Assert.AreEqual(0, d.settings.colorFilter);
            d.settings.icloudSync = false;
            d.settings.colorFilter = (int)ColorFilter.BlueYellow;
            d.settings.captions = true;
            d.totals.versusGames = 4;
            d.rival.cranesWins = 2;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsFalse(back.settings.icloudSync);
            Assert.AreEqual((int)ColorFilter.BlueYellow, back.settings.colorFilter);
            Assert.IsTrue(back.settings.captions);
            Assert.AreEqual(4, back.totals.versusGames);
            Assert.AreEqual(2, back.rival.cranesWins);
            Assert.AreEqual(ColorFilter.Off, ColorAccess.Normalize(7), "unknown values fall back to off");
        }
    }

    public class ColorAccessTests
    {
        private static float MinDistance(MeterPalette p, ColorFilter f)
        {
            var fill = RgbColor.FromHex("#F4F1DE");
            var frame = RgbColor.FromHex("#1A1A2E");
            var cols = new[] { fill, frame, p.Good, p.Near, p.Bad };
            float min = float.MaxValue;
            for (int i = 0; i < cols.Length; i++)
                for (int j = i + 1; j < cols.Length; j++)
                    min = System.Math.Min(min, ColorAccess.PerceivedDistance(cols[i], cols[j], f));
            return min;
        }

        [Test]
        public void Simulation_KeepsGreysGrey_AndMergesRedWithGreenForDeutan()
        {
            var grey = new RgbColor(128, 128, 128);
            var sim = ColorAccess.SimulateDeutan(grey);
            Assert.LessOrEqual(System.Math.Abs(sim.r - 128), 2);
            Assert.LessOrEqual(System.Math.Abs(sim.b - 128), 2);
            var red = RgbColor.FromHex("#D03030");
            var green = RgbColor.FromHex("#6A8A2A");
            Assert.Greater(ColorAccess.DeltaE(red, green), 40f, "clearly different to typical vision");
            Assert.Less(ColorAccess.DeltaE(ColorAccess.SimulateDeutan(red), ColorAccess.SimulateDeutan(green)),
                        ColorAccess.DeltaE(red, green) * 0.5f, "much closer for deuteranopia");
        }

        [Test]
        public void FilterPalettes_StayDistinct_ForTheVisionTheyTarget()
        {
            // The standard meter colours are close for red-green colour blindness (green vs amber)...
            Assert.Less(MinDistance(ColorAccess.Standard, ColorFilter.RedGreen), 30f);
            // ...and the filter palettes fix that.
            Assert.Greater(MinDistance(ColorAccess.Meter(ColorFilter.RedGreen), ColorFilter.RedGreen), 40f);
            Assert.Greater(MinDistance(ColorAccess.Meter(ColorFilter.BlueYellow), ColorFilter.BlueYellow), 40f);
            Assert.Greater(MinDistance(ColorAccess.Meter(ColorFilter.Off), ColorFilter.Off), 40f);
        }

        [Test]
        public void ClashingKits_SwitchTheAwayTeamToItsAlternate()
        {
            var home = RgbColor.FromHex("#C0392B");   // red
            var away = RgbColor.FromHex("#4E7A27");   // olive green: fine for most eyes, a clash for red-green
            var trim = RgbColor.FromHex("#F1FAEE");   // near white
            var a = away;
            var t = trim;
            Assert.IsFalse(ColorAccess.ResolveAwayKit(home, ref a, ref t, ColorFilter.Off));
            Assert.AreEqual(away, a);
            Assert.IsTrue(ColorAccess.ResolveAwayKit(home, ref a, ref t, ColorFilter.RedGreen));
            Assert.AreEqual(trim, a, "the alternate (trim) becomes the jersey");
            Assert.AreEqual(away, t);

            // Identical jerseys clash for everyone.
            var same = home;
            var sameTrim = RgbColor.FromHex("#111111");
            Assert.IsTrue(ColorAccess.ResolveAwayKit(home, ref same, ref sameTrim, ColorFilter.Off));
        }
    }

    public class ShootoutDuelTests
    {
        [Test]
        public void TwoRounds_ThenAWinnerOrATie()
        {
            var duel = new ShootoutDuel();
            Assert.AreEqual(0, duel.Round);
            Assert.AreEqual("P1: SET THE SCORE", duel.Intro);
            duel.Record(14);
            Assert.AreEqual(1, duel.Round);
            Assert.AreEqual("P2: BEAT P1'S 14", duel.Intro);
            Assert.IsFalse(duel.Finished);
            Assert.AreEqual(-1, duel.Winner);
            duel.Record(16);
            Assert.IsTrue(duel.Finished);
            Assert.AreEqual(1, duel.Winner);
            Assert.AreEqual("P2 WINS THE SHOOTOUT", duel.ResultTitle);
            duel.Record(99);
            Assert.AreEqual(16, duel.Points[1], "nothing changes once it's over");

            var tie = new ShootoutDuel();
            tie.Record(10);
            tie.Record(10);
            Assert.AreEqual(-1, tie.Winner);
            Assert.AreEqual("TIE GAME", tie.ResultTitle);
        }
    }

    public class Season4Tests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Content_HasTheFourthRival_Courts_Kits_AndValidates()
        {
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid, ContentValidator.Validate(_c).ToString());
            var cranes = _c.Team(DefaultContent.Rival4CrewId);
            Assert.AreEqual(TeamTier.Rival, cranes.tier);
            Assert.AreEqual(LogoMotif.Crane, cranes.logoMotif);
            Assert.IsNotNull(_c.Player(DefaultContent.Rival4LeaderId));
            foreach (var id in new[] { "court.laundromat_lot", "court.drive_in", "court.paper_garden" }) Assert.IsNotNull(_c.Court(id), id);
            foreach (var id in new[] { "cosmetic.jersey.origami", "cosmetic.jersey.wash_fold", "cosmetic.jersey.double_feature",
                                       "cosmetic.shoes.paper_planes", "cosmetic.shoes.marquee",
                                       "cosmetic.celebration.shoulder_brush", "cosmetic.celebration.paper_plane",
                                       "cosmetic.move.rocker_step", "cosmetic.move.snatch_back" })
                Assert.IsNotNull(_c.Find(_c.Cosmetics, id), id);
            var names = new HashSet<string>();
            foreach (var cos in _c.Cosmetics) Assert.IsTrue(names.Add(cos.slot + ":" + cos.displayName), "duplicate name " + cos.displayName);
        }

        [Test]
        public void CraneLogo_Draws_AndCustomTeamsCanUseIt()
        {
            var motif = LogoGenerator.Motif(LogoMotif.Crane);
            Assert.AreEqual(12, motif.Length);
            foreach (var row in motif) Assert.AreEqual(12, row.Length);
            Assert.IsFalse(ReferenceEquals(LogoGenerator.Motif(LogoMotif.Ball), motif));
            Assert.AreEqual("CRANE", CustomTeams.MotifNames[(int)LogoMotif.Crane]);
            Assert.IsTrue(Loc.Has("CRANE"));
        }

        [Test]
        public void NewCelebrationsAndMoves_AreWiredAndAnimate()
        {
            Assert.AreEqual(CelebrationKind.ShoulderBrush, Flair.CelebrationFor("cosmetic.celebration.shoulder_brush"));
            Assert.AreEqual(CelebrationKind.PaperPlane, Flair.CelebrationFor("cosmetic.celebration.paper_plane"));
            Assert.AreEqual(DribbleMoveKind.RockerStep, Flair.DribbleMoveFor("cosmetic.move.rocker_step"));
            Assert.AreEqual(DribbleMoveKind.SnatchBack, Flair.DribbleMoveFor("cosmetic.move.snatch_back"));
            foreach (var kind in new[] { CelebrationKind.ShoulderBrush, CelebrationKind.PaperPlane })
            {
                bool moved = false;
                for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
                {
                    var p = Flair.Celebration(kind, t);
                    if (!p.IsNone) moved = true;
                }
                Assert.IsTrue(moved, kind.ToString());
            }
            foreach (var kind in new[] { DribbleMoveKind.RockerStep, DribbleMoveKind.SnatchBack })
            {
                Assert.IsFalse(Flair.DribbleMove(kind, 0.1f).IsNone, kind.ToString());
                Assert.IsTrue(Flair.DribbleMove(kind, 5f).IsNone);
            }
        }

        [Test]
        public void RivalsTakeTurns_AndTheCranesAreTracked()
        {
            for (int season = 1; season <= 12; season++)
            {
                string expected = ((season - 1) % 12) switch
                {
                    0 => DefaultContent.RivalCrewId,
                    1 => DefaultContent.Rival2CrewId,
                    2 => DefaultContent.Rival3CrewId,
                    3 => DefaultContent.Rival4CrewId,
                    4 => DefaultContent.Rival5CrewId,
                    5 => DefaultContent.Rival6CrewId, // Phase 31
                    6 => DefaultContent.Rival7CrewId, // Phase 34
                    7 => DefaultContent.Rival8CrewId, // Phase 36
                    8 => DefaultContent.Rival9CrewId, // Phase 37
                    9 => DefaultContent.Rival10CrewId, // Phase 38
                    10 => DefaultContent.Rival11CrewId, // Phase 39
                    _ => DefaultContent.Rival12CrewId, // Phase 40
                };
                Assert.AreEqual(expected, RivalEngine.RivalFor(season), "season " + season);
            }

            var d = Career.New(_c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 4, currentWeek = 6 };
            Assert.AreEqual(Story.Rival4Intro, Story.Pending(d));
            Assert.AreEqual(DefaultContent.Rival4CrewId, RivalEngine.Challenge(d, _c, "difficulty.caller").AwayTeamId);
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival4CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.cranesWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d), "a Cranes win isn't a Neon Static win");
            Story.MarkSeen(d, Story.Rival4Intro);
            Assert.AreEqual(Story.Rival4Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.cranes").Earned(d));
            Assert.IsFalse(RivalEngine.BeatEveryRival(d));
            d.rival.wins = 4;
            d.rival.sundownWins = 1;
            d.rival.tideWins = 1;
            Assert.IsFalse(RivalEngine.BeatEveryRival(d), "the Cassette Club too");
            d.rival.wins = 5;
            d.rival.cassetteWins = 1;
            Assert.IsTrue(RivalEngine.BeatEveryRival(d));
            Assert.Contains(Achievements.AllRivals, Achievements.Earned(d));
            foreach (var id in new[] { Story.Rival4Intro, Story.Rival4Beaten, Story.FourCups })
                Assert.AreEqual(Story.Beat(id, "Rook", Loc.English).Lines.Count, Story.Beat(id, "Rook", Loc.Spanish).Lines.Count, id);
        }

        [Test]
        public void NewBadges_HaveSpanish()
        {
            foreach (var id in new[] { "badge.cranes", "badge.every_rival", "badge.couch", "badge.four_rings" })
            {
                var b = Badges.All.Find(x => x.Id == id);
                Assert.IsNotNull(b, id);
                Assert.IsTrue(Loc.Has(b.Title), b.Title);
                Assert.IsTrue(Loc.Has(b.Description), b.Description);
            }
        }
    }
}
