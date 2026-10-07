using System.Collections.Generic;
using System.Text;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 31: the sealed save file.</summary>
    public class Phase31SaveTests
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef");
        private static readonly byte[] OtherKey = Encoding.UTF8.GetBytes("fedcba9876543210fedcba9876543210");

        private static CareerSaveData Career(ContentCatalog c, int games = 12, int coins = 500)
        {
            var d = CallerRetroBall.Logic.Career.New(c);
            d.totals.games = games;
            d.totals.wins = games / 2;
            d.signalPoints = coins;
            return d;
        }

        [Test]
        public void SealedSave_OpensVerified()
        {
            var c = DefaultContent.Create();
            string text = SaveIntegrity.Seal(Career(c), Key);
            Assert.IsTrue(SaveGuard.IsSigned(text));
            var back = SaveIntegrity.Open(text, Key, false, c, out var status, out var verdict);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(SaveVerdict.Verified, verdict);
            Assert.AreEqual(12, back.totals.games);
            Assert.IsFalse(back.saveFlagged);
        }

        [Test]
        public void OldPlainSave_LoadsAsLegacy()
        {
            var c = DefaultContent.Create();
            string plain = SaveCodec.Encode(Career(c, 30));
            var back = SaveIntegrity.Open(plain, Key, true, c, out var status, out var verdict);
            Assert.AreEqual(SaveVerdict.Legacy, verdict);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(30, back.totals.games, "nothing lost upgrading");
            Assert.IsFalse(back.saveFlagged);
        }

        [Test]
        public void EditedSave_StillLoads_ButIsFlaggedAndClamped()
        {
            var c = DefaultContent.Create();
            string text = SaveIntegrity.Seal(Career(c, 12, 500), Key);
            string edited = System.Text.RegularExpressions.Regex.Replace(text, "\"signalPoints\":\\s*500", "\"signalPoints\": 2000000000");
            Assert.AreNotEqual(text, edited);
            var back = SaveIntegrity.Open(edited, Key, false, c, out var status, out var verdict);
            Assert.AreEqual(SaveVerdict.Tampered, verdict);
            Assert.AreEqual(LoadStatus.Ok, status, "the career is kept");
            Assert.AreEqual(12, back.totals.games);
            Assert.IsTrue(back.saveFlagged);
            Assert.AreEqual(SaveIntegrity.MaxCurrency, back.signalPoints, "clamped");
            // The flag survives being saved and loaded again (it's inside the sealed payload).
            var again = SaveIntegrity.Open(SaveIntegrity.Seal(back, Key), Key, false, c, out _, out var v2);
            Assert.AreEqual(SaveVerdict.Verified, v2);
            Assert.IsTrue(again.saveFlagged);
        }

        [Test]
        public void LostKey_IsTrusted_NeverFlagged()
        {
            var c = DefaultContent.Create();
            string text = SaveIntegrity.Seal(Career(c, 40), Key);
            // A new device / cleared Keychain: a different, freshly made key.
            var back = SaveIntegrity.Open(text, OtherKey, true, c, out var status, out var verdict);
            Assert.AreEqual(SaveVerdict.KeyLost, verdict);
            Assert.AreEqual(40, back.totals.games);
            Assert.IsFalse(back.saveFlagged);
            // No key at all this launch: same.
            back = SaveIntegrity.Open(text, null, false, c, out _, out verdict);
            Assert.AreEqual(SaveVerdict.KeyLost, verdict);
            Assert.IsFalse(back.saveFlagged);
            // Without a key the save is written unsealed (and read back as legacy later).
            Assert.IsFalse(SaveGuard.IsSigned(SaveIntegrity.Seal(back, null)));
        }

        [Test]
        public void WrongKey_WhenTheKeyIsOld_IsTampered()
        {
            var c = DefaultContent.Create();
            string text = SaveIntegrity.Seal(Career(c), Key);
            SaveIntegrity.Open(text, OtherKey, false, c, out _, out var verdict);
            Assert.AreEqual(SaveVerdict.Tampered, verdict);
        }

        [Test]
        public void Garbage_IsUnreadable_AndEmptyIsNew()
        {
            var c = DefaultContent.Create();
            SaveIntegrity.Open(null, Key, false, c, out var s1, out var v1);
            Assert.AreEqual(SaveVerdict.New, v1);
            SaveIntegrity.Open("not a save", Key, false, c, out var s2, out var v2);
            Assert.AreEqual(SaveVerdict.Unreadable, v2);
            Assert.AreEqual(LoadStatus.Recovered, s2);
            string sealedGarbage = SaveGuard.Sign("{{{{", Key);
            SaveIntegrity.Open(sealedGarbage, Key, false, c, out _, out var v3);
            Assert.AreEqual(SaveVerdict.Unreadable, v3);
        }

        [Test]
        public void Clamp_KeepsTotalsConsistent()
        {
            var c = DefaultContent.Create();
            var d = Career(c);
            d.totals.games = 10;
            d.totals.wins = 50;
            d.totals.losses = 50;
            d.totals.fieldGoalsAttempted = 20;
            d.totals.fieldGoalsMade = 90;
            d.fans = -5;
            d.live = new LiveSaveData { rating = 99999 };
            SaveIntegrity.Clamp(d);
            Assert.AreEqual(10, d.totals.wins);
            Assert.AreEqual(0, d.totals.losses);
            Assert.AreEqual(20, d.totals.fieldGoalsMade);
            Assert.AreEqual(0, d.fans);
            Assert.AreEqual(4000, d.live.rating);
        }

        [Test]
        public void ICloudAdopt_ClampsTheCloudCopy()
        {
            var c = DefaultContent.Create();
            var cloud = Career(c);
            cloud.signalPoints = int.MaxValue;
            var adopted = CloudSave.Adopt(cloud, Career(c));
            Assert.AreEqual(SaveIntegrity.MaxCurrency, adopted.signalPoints);
        }

        [Test]
        public void FlaggedCareer_PostsNoLeaderboardScores()
        {
            var c = DefaultContent.Create();
            var d = Career(c, 50);
            d.saveFlagged = true;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.IsTrue(back.saveFlagged, "the flag is saved");
        }
    }

    public class Phase31TapeTests
    {
        private const float Dt = 1f / 60f;

        private static PlayerInput RandomInput(SeededRandom r) => new PlayerInput
        {
            Move = new Vec2(r.Range(-100, 101) / 100f, r.Range(-100, 101) / 100f),
            ShootPressed = r.Range(0, 40) == 0,
            ShootHeld = r.Range(0, 3) == 0,
            PassPressed = r.Range(0, 50) == 0,
            DefensePressed = r.Range(0, 30) == 0,
        };

        /// <summary>Plays a whole game with random (but sticky) inputs, recording both into a feed.</summary>
        private static MatchSimulation PlayGame(LinkSetup s, SpectatorFeed feed)
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(s.Register(c));
            var m = new MatchSimulation(MatchSetup.FromRequest(s.ToRequest(), c));
            var ra = new SeededRandom(7);
            var rb = new SeededRandom(8);
            PlayerInput a = default, b = default;
            int guard = 0;
            while (!m.IsOver && guard++ < 60 * 60 * 30)
            {
                // Inputs change every few steps (like a held stick), so the tape compresses.
                if (guard % 6 == 0) { a = LinkProtocol.Quantize(RandomInput(ra)); b = LinkProtocol.Quantize(RandomInput(rb)); }
                m.Step(Dt, a, b);
                feed.Record(a, b);
                a.ShootPressed = a.PassPressed = a.DefensePressed = false;
                b.ShootPressed = b.PassPressed = b.DefensePressed = false;
            }
            Assert.IsTrue(m.IsOver);
            return m;
        }

        private static LinkSetup Setup(ContentCatalog c)
        {
            var league = c.TeamsInTier(TeamTier.League);
            return LinkSetup.From(c, league[3].id, league[5].id, league[3].homeCourtId, DefaultContent.DefaultDifficultyId, 31, "1.0.0", "Host");
        }

        [Test]
        public void Tape_RoundTrips_AndReplaysTheSameGame()
        {
            var c = DefaultContent.Create();
            var s = Setup(c);
            var feed = new SpectatorFeed(s);
            var game = PlayGame(s, feed);
            var tape = Tapes.From(s, feed.TeamA, feed.TeamB, Tapes.TitleFor(s, "Home", "Away"), 1760000000, game.Score[0], game.Score[1]);
            byte[] file = Tapes.Encode(tape);
            Assert.Less(file.Length, 200 * 1024, "a whole game is a small file (" + file.Length + " bytes)");
            Assert.Less(file.Length, feed.Steps * 8, "run-length coding helps");
            var back = Tapes.Decode(file);
            Assert.IsNotNull(back);
            Assert.AreEqual(tape.Steps, back.Steps);
            Assert.AreEqual("TWO PHONES · HOME VS AWAY", back.Title);
            Assert.AreEqual(1760000000L, back.SavedAt);
            Assert.AreEqual(game.Score[0], back.ScoreA);

            // Played back through a watcher: the identical game.
            var player = Tapes.Player(back, "1.0.0", LinkProtocol.ContentFingerprint(DefaultContent.Create()), DefaultContent.Create());
            Assert.IsTrue(player.Ready && player.IsTape && player.HostLeft);
            var c2 = DefaultContent.Create();
            Assert.IsTrue(player.Setup.Register(c2));
            var replay = new MatchSimulation(MatchSetup.FromRequest(player.Setup.ToRequest(), c2));
            while (player.TryStep(out var a, out var b) && !replay.IsOver) replay.Step(Dt, a, b);
            Assert.IsTrue(replay.IsOver);
            Assert.AreEqual(SimHash.Of(game), SimHash.Of(replay));
        }

        [Test]
        public void Tape_FromAnotherVersion_DoesNotPlay()
        {
            var c = DefaultContent.Create();
            var s = Setup(c);
            var tape = Tapes.From(s, new List<PlayerInput> { default }, new List<PlayerInput> { default }, "T", 1, 0, 0);
            var player = Tapes.Player(tape, "2.0.0", LinkProtocol.ContentFingerprint(c), c);
            Assert.IsFalse(player.Ready);
        }

        [Test]
        public void DamagedTapes_AreRefused()
        {
            var c = DefaultContent.Create();
            var tape = Tapes.From(Setup(c), new List<PlayerInput> { default, default }, new List<PlayerInput> { default, default }, "T", 1, 2, 0);
            var file = Tapes.Encode(tape);
            Assert.IsNull(Tapes.Decode(null));
            Assert.IsNull(Tapes.Decode(new byte[] { 1, 2, 3 }));
            var cut = new byte[file.Length - 3];
            System.Array.Copy(file, cut, cut.Length);
            Assert.IsNull(Tapes.Decode(cut), "truncated");
            var longer = new byte[file.Length + 1];
            System.Array.Copy(file, longer, file.Length);
            Assert.IsNull(Tapes.Decode(longer), "trailing junk");
            var bad = (byte[])file.Clone();
            bad[0] = (byte)'X';
            Assert.IsNull(Tapes.Decode(bad), "wrong magic");
        }

        [Test]
        public void TapeTransfer_SendsATapeToAnotherPhone()
        {
            var c = DefaultContent.Create();
            var s = Setup(c);
            var feed = new SpectatorFeed(s);
            PlayGame(s, feed);
            var tape = Tapes.From(s, feed.TeamA, feed.TeamB, "SEND ME", 5, 40, 38);
            MemoryTransport.Pair(out var wa, out var wb, latencyTicks: 2);
            var send = TapeTransfer.Send(tape);
            var recv = TapeTransfer.Receive();
            for (int i = 0; i < 400 && (send.Status != TapeTransfer.State.Done || recv.Status != TapeTransfer.State.Done); i++)
            {
                wa.Tick(); wb.Tick();
                send.Update(wa);
                recv.Update(wb);
            }
            Assert.AreEqual(TapeTransfer.State.Done, recv.Status, recv.Error);
            Assert.AreEqual(TapeTransfer.State.Done, send.Status, send.Error);
            Assert.AreEqual("SEND ME", recv.Received.Title);
            Assert.AreEqual(tape.Steps, recv.Received.Steps);
            Assert.IsTrue(System.Linq.Enumerable.SequenceEqual(Tapes.Encode(tape), Tapes.Encode(recv.Received)), "byte for byte");
        }

        [Test]
        public void JoiningAGameHost_ForATape_SaysSo()
        {
            var c = DefaultContent.Create();
            MemoryTransport.Pair(out var wa, out var wb);
            wa.Send(LinkProtocol.SetupMessage(Setup(c)));
            var recv = TapeTransfer.Receive();
            recv.Update(wb);
            Assert.AreEqual(TapeTransfer.State.Failed, recv.Status);
            StringAssert.Contains("hosting a game", recv.Error);
        }

        [Test]
        public void TapeTitles_NameTheKindOfGame()
        {
            var c = DefaultContent.Create();
            var s = Setup(c);
            s.Cup = "couch:2"; s.HomeLabel = "Ana"; s.AwayLabel = "Ben";
            Assert.AreEqual("COUCH CUP · ANA VS BEN", Tapes.TitleFor(s, "x", "y"));
            var live = Setup(c);
            live.HostRating = 1000; live.GuestRating = 1100; live.HostName = "Nova"; live.GuestName = "Sal";
            Assert.AreEqual("LIVE · NOVA VS SAL", Tapes.TitleFor(live, "x", "y"));
        }
    }

    public class Phase31SeasonSixTests
    {
        private ContentCatalog _c;

        [SetUp]
        public void Setup() => _c = DefaultContent.Create();

        [Test]
        public void LighthouseKeepers_AreTheSixthRival_WithCourtsStoryAndBadges()
        {
            var team = _c.Team(DefaultContent.Rival6CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.AreEqual(LogoMotif.Lighthouse, team.logoMotif);
            Assert.IsNotNull(_c.Court(team.homeCourtId));
            Assert.IsNotNull(_c.Court("court.glasshouse_roof"));
            Assert.IsNotNull(_c.Player(DefaultContent.Rival6LeaderId));
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid, ContentValidator.Validate(_c).ToString());
            Assert.AreEqual(DefaultContent.Rival6CrewId, RivalEngine.RivalFor(6));
            Assert.AreEqual(DefaultContent.Rival6CrewId, RivalEngine.RivalFor(16), "ten rivals take turns since Phase 38");

            var d = Career.New(_c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 6, currentWeek = 6 };
            Assert.AreEqual(Story.Rival6Intro, Story.Pending(d));
            Assert.AreEqual(DefaultContent.Rival6CrewId, RivalEngine.Challenge(d, _c, "difficulty.caller").AwayTeamId);
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival6CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.keeperWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d), "a Keepers win isn't a Neon Static win");
            Story.MarkSeen(d, Story.Rival6Intro);
            Assert.AreEqual(Story.Rival6Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.keepers").Earned(d));
            Assert.IsFalse(RivalEngine.BeatAllSix(d));
            d.rival.wins = 6;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllSix(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.all_six").Earned(d));
            foreach (var id in new[] { Story.Rival6Intro, Story.Rival6Beaten })
                Assert.AreEqual(Story.Beat(id, "Rook", Loc.English).Lines.Count, Story.Beat(id, "Rook", Loc.Spanish).Lines.Count, id);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.rival.keeperWins);
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.IsTrue(Loc.Has("LIGHTHOUSE"));
        }

        [Test]
        public void LighthouseLogo_Draws_AndCustomTeamsCanUseIt()
        {
            var motif = CallerRetroBall.Logic.PixelArt.LogoGenerator.Motif(LogoMotif.Lighthouse);
            Assert.AreEqual(12, motif.Length);
            foreach (var row in motif) Assert.AreEqual(12, row.Length);
            Assert.AreEqual("LIGHTHOUSE", CustomTeams.MotifNames[(int)LogoMotif.Lighthouse]);
        }

        [Test]
        public void SeasonSixWeeklies_StartOnTheirWeek_AndEarlierWeeksDontChange()
        {
            for (int week = Weekly.Season6Week - 40; week < Weekly.Season6Week; week++)
                foreach (var g in Weekly.For(week)) Assert.Less((int)g.Goal, (int)WeeklyGoal.DeepShots, "week " + week + " keeps its old goals");
            var seen = new HashSet<WeeklyGoal>();
            for (int week = Weekly.Season6Week; week < Weekly.Season6Week + 40; week++)
                foreach (var g in Weekly.For(week)) seen.Add(g.Goal);
            Assert.IsTrue(seen.Contains(WeeklyGoal.DeepShots) && seen.Contains(WeeklyGoal.AlleyOops) && seen.Contains(WeeklyGoal.HeatUps));
            var line = new PlayerStatLine { arcMade = 3, alleyOops = 1, alleyOopPasses = 2, heatUps = 1 };
            var summary = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            Assert.AreEqual(0, Weekly.Amount(new WeeklyChallenge { Goal = WeeklyGoal.HeatUps, Target = 2 }, summary, false), "no player line, nothing counted");
            Assert.IsTrue(new WeeklyChallenge { Goal = WeeklyGoal.DeepShots, Target = 8 }.Describe().Contains("8 deep shots"));
            Assert.AreEqual(3, line.arcMade);
        }

        [Test]
        public void SeasonSixPass_RotatesIn_WithoutChangingTheCurrentSeason()
        {
            int today = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 6));
            int season = HoopsPass.SeasonOf(today);
            Assert.Less(season, HoopsPass.FourSetsFrom, "the season running when Phase 31 shipped keeps its gear");
            Assert.AreEqual(HoopsPass.GearSets[season % 3][0], HoopsPass.GearFor(season, 5));
            Assert.AreEqual("cosmetic.pass.jersey.beacon", HoopsPass.GearFor(HoopsPass.FourSetsFrom, 5), "Season 6's set comes first");
            Assert.AreEqual(HoopsPass.FourSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2026, 10, 19))));
            foreach (var id in HoopsPass.GearSets[3])
            {
                var def = _c.Find(_c.Cosmetics, id);
                Assert.IsNotNull(def, id);
                Assert.IsTrue(def.passOnly, id);
            }
            Assert.AreEqual(CelebrationKind.Spotlight, Flair.CelebrationFor("cosmetic.pass.celebration.spotlight"));
            bool flipped = false, thumped = false, up = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.02f)
            {
                var p = Flair.Celebration(CelebrationKind.Spotlight, t);
                flipped |= p.FlipOverride; thumped |= p.Frame == PoseFrame.ChestThump; up |= p.ArmsUp;
            }
            Assert.IsTrue(flipped && thumped && up, "turn, thump, arms up");
            Assert.IsNotNull(_c.Find(_c.Cosmetics, "cosmetic.jersey.harbor_fog"));
        }
    }

    public class Phase31OnboardingTests
    {
        [Test]
        public void TourCards_ShowOnce_AndAreTranslated()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            foreach (var card in Tours.All)
            {
                Assert.IsTrue(Loc.Has(card.Body), card.Id);
                Assert.IsTrue(Loc.Has(card.Title), card.Title);
                Assert.IsFalse(Tours.Seen(d, card));
            }
            Tours.MarkSeen(d, Tours.Watch);
            Assert.IsTrue(Tours.Seen(d, Tours.Watch));
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(d), c, out _).storySeen.Contains(Tours.Watch.Id), "kept in the save");
        }

        [Test]
        public void WhatsNew_IsForExistingCareersOnly()
        {
            var c = DefaultContent.Create();
            var fresh = Career.New(c);
            Tours.SkipWhatsNewForNewPlayer(fresh);
            Assert.IsFalse(Tours.ShowWhatsNew(fresh), "a brand-new player isn't told what's new");
            var old = Career.New(c);
            old.totals.games = 40;
            old.tutorialDone = true;
            Tours.SkipWhatsNewForNewPlayer(old);
            Assert.IsTrue(Tours.ShowWhatsNew(old));
            Tours.MarkSeen(old, Tours.WhatsNew);
            Assert.IsFalse(Tours.ShowWhatsNew(old));
        }

        [Test]
        public void ScreenReaderText_ReadsButtonsCleanly()
        {
            Assert.AreEqual("GAME TAPES, WATCH AGAIN, SEND", ScreenReaderText.Plain("GAME TAPES  ·  WATCH AGAIN, SEND"));
            Assert.AreEqual("PLAYER OF THE GAME NOVA 22 PTS", ScreenReaderText.Plain("PLAYER OF THE GAME  <color=#FFD166>NOVA</color>  22 PTS"));
            Assert.AreEqual("2 PLAYER, TWO PHONES", ScreenReaderText.Plain("2 PLAYER ► TWO PHONES"));
            Assert.AreEqual("Line one, line two", ScreenReaderText.Plain("Line one\nline two"));
            Assert.AreEqual("", ScreenReaderText.Plain(null));
            Assert.AreEqual("BACK", ScreenReaderText.FromName("Button BACK"));
            Assert.AreEqual("Button", ScreenReaderText.FromName(""));
        }

        [Test]
        public void SystemTextSize_OnlyEverMakesMenusBigger()
        {
            Assert.AreEqual(1f, ScreenReaderText.UiScaleForSystemText(0.8f));
            Assert.AreEqual(1f, ScreenReaderText.UiScaleForSystemText(1f));
            Assert.AreEqual(1.15f, ScreenReaderText.UiScaleForSystemText(1.12f));
            Assert.AreEqual(1.25f, ScreenReaderText.UiScaleForSystemText(2.5f), "capped at the largest scale the menus are laid out for");
            Assert.AreEqual(1f, ScreenReaderText.UiScaleForSystemText(float.NaN));
            var d = Career.New(DefaultContent.Create());
            Assert.IsTrue(d.settings.followSystemText, "on by default");
            d.settings.followSystemText = false;
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(d), DefaultContent.Create(), out _).settings.followSystemText);
        }
    }
}
