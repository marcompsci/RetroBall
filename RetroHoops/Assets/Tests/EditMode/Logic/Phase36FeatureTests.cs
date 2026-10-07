using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 36: hardening fixes from the pre-ship review.</summary>
    public class Phase36HardeningTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        private static GameTape BlankTape(int steps)
        {
            var t = new GameTape { Setup = new LinkSetup(), Title = "T" };
            for (int i = 0; i < steps; i++) { t.TeamA.Add(default); t.TeamB.Add(default); }
            return t;
        }

        private static System.Func<MatchSimulation> DemoGame(uint seed)
        {
            var req = MatchRequest.QuickCallDefault(C);
            req.Mode = GameMode.Demo;
            req.Seed = seed;
            return () => new MatchSimulation(MatchSetup.FromRequest(req, C));
        }

        [Test]
        public void MarkingInSlices_GivesTheSameMarks_AsAllAtOnce()
        {
            var tape = BlankTape(60 * 60 * 5);
            var whole = new TapeTheater(tape);
            whole.Index(DemoGame(515), 1f / 60f);
            var sliced = new TapeTheater(tape);
            sliced.StartIndex(DemoGame(515));
            int frames = 0;
            while (!sliced.IndexSome(150, 1f / 60f)) { frames++; Assert.Less(sliced.IndexProgress, 1f); }
            Assert.Greater(frames, 10, "it really was spread over many slices");
            Assert.AreEqual(whole.Marks.Count, sliced.Marks.Count);
            for (int i = 0; i < whole.Marks.Count; i++)
            {
                Assert.AreEqual(whole.Marks[i].Tick, sliced.Marks[i].Tick);
                Assert.AreEqual(whole.Marks[i].Label, sliced.Marks[i].Label);
            }
            Assert.AreEqual(1f, sliced.IndexProgress);
        }

        [Test]
        public void Watcher_Rewinds_AndRecorder_Resets()
        {
            var league = C.TeamsInTier(TeamTier.League);
            var setup = LinkSetup.From(C, league[0].id, league[1].id, league[0].homeCourtId, DefaultContent.DefaultDifficultyId, 9, "1.0.0", "Host");
            var tape = Tapes.From(setup, BlankTape(300).TeamA, BlankTape(300).TeamB, "T", 1, 0, 0);
            var w = Tapes.Player(tape, "1.0.0", LinkProtocol.ContentFingerprint(C), C);
            Assert.IsTrue(w.Ready, w.Error);
            Assert.IsTrue(w.TryStep(out _, out _));
            Assert.IsTrue(w.TryStep(out _, out _));
            Assert.AreEqual(2, w.NextTick);
            w.Rewind();
            Assert.AreEqual(0, w.NextTick);
            Assert.AreEqual(300, w.Received, "the steps are kept");

            var m = DemoGame(3)();
            var rec = new ReplayRecorder(m.Players.Length);
            for (int i = 0; i < 30; i++) { m.Step(1f / 60f, default); rec.Capture(m); }
            rec.OfferBestPlay("X", 50);
            rec.Reset();
            Assert.AreEqual(0, rec.Count);
            Assert.IsNull(rec.BestPlay);
        }

        [Test]
        public void TheaterHoldsAsManyMarksAsATape()
        {
            Assert.AreEqual(Tapes.MaxMarks, TapeTheater.MaxUserMarks);
        }

        [Test]
        public void CoachedGames_HaveNoHumanBall_AndDontTouchYourCareerNumbers()
        {
            var league = C.TeamsInTier(TeamTier.League);
            var f = Franchise.Create(C, league[2].id, 4);
            f.coach = true;
            var req = Franchise.NextMatch(f, C, DefaultContent.DefaultDifficultyId);
            var m = new MatchSimulation(MatchSetup.FromRequest(req, C));
            for (int i = 0; i < 60 * 40; i++)
            {
                m.Step(1f / 60f, default);
                Assert.IsFalse(m.HumanHasBall);
                Assert.IsFalse(m.HumanTeamHasBall);
            }
            var career = Career.New(C);
            var s = MatchSummary.From(m, GameMode.Franchise, "coached-1");
            Assert.IsTrue(Career.ApplyMatch(career, s, new RewardGrant { signalPoints = 10 }));
            Assert.AreEqual(0, career.totals.games, "a coached game isn't one of your games");
            Assert.AreEqual(10, career.signalPoints - Career.New(C).signalPoints, "but it pays");
        }
    }

    /// <summary>Phase 36: Season 8 (the Night Lanterns).</summary>
    public class Phase36SeasonEightTests
    {
        [Test]
        public void NightLanterns_AreTheEighthRival_WithStoryBadgesAndCourts()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
            var team = c.Team(DefaultContent.Rival8CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.AreEqual(LogoMotif.Lantern, team.logoMotif);
            Assert.IsNotNull(c.Court("court.night_market"));
            Assert.IsNotNull(c.Court("court.lantern_steps"));
            Assert.IsNotNull(c.Player(DefaultContent.Rival8LeaderId));
            for (int season = 1; season <= 24; season++)
                Assert.AreEqual(season % 11 == 8, RivalEngine.RivalFor(season) == DefaultContent.Rival8CrewId, "season " + season);

            var d = Career.New(c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 8, currentWeek = 6 };
            Assert.AreEqual(Story.Rival8Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival8CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.lanternWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d));
            Story.MarkSeen(d, Story.Rival8Intro);
            Assert.AreEqual(Story.Rival8Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.lanterns").Earned(d));
            d.rival.wins = 8;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = d.rival.keeperWins = d.rival.courierWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllEight(d));
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).rival.lanternWins);
            foreach (var id in new[] { Story.Rival8Intro, Story.Rival8Beaten })
            {
                Assert.Greater(Story.Beat(id, "ROOK", "en").Lines.Count, 1, id);
                Assert.Greater(Story.Beat(id, "ROOK", "es").Lines.Count, 1, id + " in Spanish");
            }
        }

        [Test]
        public void LanternLogo_Draws_AndDiffersFromTheOthers()
        {
            var team = DefaultContent.Create().Team(DefaultContent.Rival8CrewId);
            var a = LogoGenerator.Generate(team);
            Assert.Greater(a.OpaqueCount(), 50);
            team.logoMotif = LogoMotif.Lighthouse;
            var b = LogoGenerator.Generate(team);
            int differ = 0;
            for (int i = 0; i < a.Pixels.Length; i++) if (!a.Pixels[i].Equals(b.Pixels[i])) differ++;
            Assert.Greater(differ, 10);
        }

        [Test]
        public void SeasonEightPass_AndWeekly_StartOnTheirDates()
        {
            Assert.AreEqual("cosmetic.pass.jersey.courier", HoopsPass.GearFor(HoopsPass.FiveSetsFrom, 5), "Season 7's season is unchanged");
            Assert.AreEqual("cosmetic.pass.jersey.beacon", HoopsPass.GearFor(HoopsPass.FourSetsFrom, 5));
            Assert.AreEqual("cosmetic.pass.jersey.lantern", HoopsPass.GearFor(HoopsPass.SixSetsFrom, 5));
            Assert.AreEqual("cosmetic.pass.celebration.lantern_release", HoopsPass.GearFor(HoopsPass.SixSetsFrom, 20));
            Assert.AreEqual(HoopsPass.SixSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2027, 1, 11))));
            Assert.AreEqual(Weekly.Season8Week, Weekly.WeekOf(DailyChallenges.DayNumber(new System.DateTime(2026, 12, 7))));
            Assert.AreEqual(CelebrationKind.LanternRelease, Flair.CelebrationFor("cosmetic.pass.celebration.lantern_release"));
            bool arms = false, lift = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f)
            {
                var p = Flair.Celebration(CelebrationKind.LanternRelease, t);
                arms |= p.ArmsUp;
                lift |= p.Lift > 0;
            }
            Assert.IsTrue(arms && lift);
            for (int week = Weekly.Season7Week; week < Weekly.Season8Week; week++)
                foreach (var g in Weekly.For(week)) Assert.AreNotEqual(WeeklyGoal.CornerThrees, g.Goal, "not before Season 8");
            bool seen = false;
            for (int week = Weekly.Season8Week; week < Weekly.Season8Week + 40 && !seen; week++)
                foreach (var g in Weekly.For(week)) seen |= g.Goal == WeeklyGoal.CornerThrees;
            Assert.IsTrue(seen, "the corner-threes goal turns up");

            var line = new PlayerStatLine();
            ShotZones.Record(line.chart, ShotSpot.CornerLeft, true);
            ShotZones.Record(line.chart, ShotSpot.CornerRight, true);
            ShotZones.Record(line.chart, ShotSpot.CornerRight, false);
            ShotZones.Record(line.chart, ShotSpot.WingLeft, true);
            var s = new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, winner = 0 };
            s.lines.Add(new SummaryLine { isHuman = true, stats = line });
            Assert.AreEqual(2, Weekly.Amount(new WeeklyChallenge { Goal = WeeklyGoal.CornerThrees, Target = 4 }, s, false));
        }
    }

    /// <summary>Phase 36: SPOT SPECIALIST ranks.</summary>
    public class Phase36SpecialistTests
    {
        private static ShotChartData Chart(ShotSpot spot, int made, int att)
        {
            var d = new ShotChartData();
            d[spot].made = made;
            d[spot].attempted = att;
            return d;
        }

        [Test]
        public void Ranks_NeedVolumeAndAccuracy()
        {
            Assert.AreEqual(SpecialistTier.None, Specialist.TierOf(Chart(ShotSpot.CornerLeft, 19, 30), SpotGroup.Corner), "not enough makes");
            Assert.AreEqual(SpecialistTier.Bronze, Specialist.TierOf(Chart(ShotSpot.CornerLeft, 20, 50), SpotGroup.Corner), "40% from the corner");
            Assert.AreEqual(SpecialistTier.None, Specialist.TierOf(Chart(ShotSpot.CornerLeft, 30, 100), SpotGroup.Corner), "30% is below the usual 34%");
            Assert.AreEqual(SpecialistTier.Bronze, Specialist.TierOf(Chart(ShotSpot.CornerRight, 60, 170), SpotGroup.Corner), "35%: volume for Silver, not the accuracy");
            Assert.AreEqual(SpecialistTier.Silver, Specialist.TierOf(Chart(ShotSpot.CornerRight, 60, 150), SpotGroup.Corner));
            Assert.AreEqual(SpecialistTier.Gold, Specialist.TierOf(Chart(ShotSpot.Paint, 150, 220), SpotGroup.Rim), "68% at the rim");
            Assert.AreEqual(SpecialistTier.None, Specialist.TierOf(Chart(ShotSpot.Paint, 150, 220), SpotGroup.Corner), "ranks are per area");
            var mid = new ShotChartData();
            mid[ShotSpot.FoulLine].made = 10; mid[ShotSpot.FoulLine].attempted = 20;
            mid[ShotSpot.ElbowLeft].made = 10; mid[ShotSpot.ElbowLeft].attempted = 20;
            Assert.AreEqual(SpecialistTier.Bronze, Specialist.TierOf(mid, SpotGroup.Mid), "an area adds up its spots");
        }

        [Test]
        public void Bonuses_GoToTheRightSpots()
        {
            var b = Specialist.SpotBonuses(Chart(ShotSpot.WingLeft, 60, 140));
            Assert.AreEqual(Specialist.Bonus[2], b[(int)ShotSpot.WingLeft], 1e-6);
            Assert.AreEqual(Specialist.Bonus[2], b[(int)ShotSpot.WingRight], 1e-6, "the whole area");
            Assert.AreEqual(0f, b[(int)ShotSpot.Paint]);
            var ranks = Specialist.NewRanks(Chart(ShotSpot.WingLeft, 19, 40), Chart(ShotSpot.WingLeft, 21, 42));
            Assert.AreEqual(1, ranks.Count);
            StringAssert.Contains("WING SPECIALIST: BRONZE", ranks[0]);
            StringAssert.Contains("NEXT: SILVER", Specialist.Line(Chart(ShotSpot.WingLeft, 21, 42), SpotGroup.Wing));
        }

        [Test]
        public void OnlySinglePlayerGames_UseThem()
        {
            var r = new MatchRequest { Mode = GameMode.QuickCall };
            Assert.IsTrue(Specialist.AppliesTo(r));
            Assert.IsFalse(Specialist.AppliesTo(new MatchRequest { Mode = GameMode.Versus }));
            Assert.IsFalse(Specialist.AppliesTo(new MatchRequest { Mode = GameMode.QuickCall, ContextId = "link" }), "two phones / Live / tapes");
            Assert.IsFalse(Specialist.AppliesTo(new MatchRequest { Mode = GameMode.Practice }));
            Assert.IsFalse(Specialist.AppliesTo(new MatchRequest { Mode = GameMode.Franchise, Coach = true }));
        }

        [Test]
        public void TheBonus_RaisesOnlyYourMakeChance()
        {
            var c = DefaultContent.Create();
            var plain = MatchRequest.QuickCallDefault(c);
            var boosted = MatchRequest.QuickCallDefault(c);
            boosted.SpotBonus = new float[ShotZones.SpotCount];
            for (int i = 0; i < boosted.SpotBonus.Length; i++) boosted.SpotBonus[i] = 0.06f;
            var a = new MatchSimulation(MatchSetup.FromRequest(plain, c));
            var b = new MatchSimulation(MatchSetup.FromRequest(boosted, c));
            int me = a.ControlledIndex;
            float mine = b.ShotContextFor(me, ShotType.MidRange, 0.5f).Bonus;
            Assert.AreEqual(0.06f, mine, 1e-6);
            Assert.AreEqual(0f, a.ShotContextFor(me, ShotType.MidRange, 0.5f).Bonus);
            int opponent = me == 0 ? a.Players.Length - 1 : 0;
            Assert.AreEqual(0f, b.ShotContextFor(opponent, ShotType.MidRange, 0.5f).Bonus, "never the other team");
            float pa = ShotModel.Evaluate(a.ShotContextFor(me, ShotType.MidRange, 0.3f), a.Setup.Shot).MakeChance;
            float pb = ShotModel.Evaluate(b.ShotContextFor(me, ShotType.MidRange, 0.3f), b.Setup.Shot).MakeChance;
            Assert.Greater(pb, pa);
        }
    }

    /// <summary>Phase 36: Live seasons, friends' boards and the shareable chart card.</summary>
    public class Phase36SocialTests
    {
        [Test]
        public void LiveSeason_PaysOnce_ByTier_WithEnoughGames()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            var s = d.live;
            s.rating = 1000;
            LiveSeason.Observe(s, 202610, false);
            for (int i = 0; i < 3; i++) LiveSeason.Observe(s, 202610, true);
            s.rating = 1340; // reached ALL-STAR
            LiveSeason.Observe(s, 202610, false);
            s.rating = 1200;
            LiveSeason.Observe(s, 202610, false);
            Assert.AreEqual(1340, s.seasonBest);
            Assert.IsNull(LiveSeason.Settle(d, c), "the season isn't over");
            LiveSeason.Observe(s, 202611, false);
            Assert.AreEqual(202610, s.endedMonth);
            Assert.AreEqual(1200, s.seasonBest, "the new season starts from where you are");
            int sp = d.signalPoints;
            var r = LiveSeason.Settle(d, c);
            Assert.IsNotNull(r);
            Assert.AreEqual("ALL-STAR", r.Tier);
            Assert.AreEqual(LiveSeason.TierSp[3], d.signalPoints - sp);
            Assert.AreEqual(LiveSeason.StarBannerId, r.CosmeticId);
            Assert.IsTrue(d.ownedCosmetics.Contains(LiveSeason.StarBannerId));
            Assert.IsNull(LiveSeason.Settle(d, c), "paid once");
            Assert.AreEqual("OCT 2026", LiveSeason.MonthName(202610));

            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _).live;
            Assert.AreEqual(202611, back.seasonMonth);
            Assert.AreEqual(202610, back.rewardedMonth);
            Assert.AreEqual(1340, back.endedBest);
        }

        [Test]
        public void LiveSeason_NeedsGamesToQualify()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.live.rating = 1600;
            LiveSeason.Observe(d.live, 202610, true);
            LiveSeason.Observe(d.live, 202611, false);
            Assert.IsNull(LiveSeason.Settle(d, c), "one game isn't a season");
            Assert.IsNull(LiveSeason.Settle(d, c));
        }

        [Test]
        public void ChartCard_IsCrisp_AndOnlyUsesLettersTheFontHas()
        {
            var d = new ShotChartData();
            ShotZones.Record(d, ShotSpot.TopOfKey, true);
            var card = ShotChartArt.Card(d, "MY SHOT CHART · 21-18", "HOT: TOP OF KEY 1/1 (100%)", 4);
            Assert.AreEqual(0, card.Width % 4);
            Assert.Greater(card.Width, 400);
            Assert.AreEqual("MY SHOT CHART 21-18", ShotChartArt.Fit("my shot chart · 21-18", 500));
            Assert.LessOrEqual(PixelFont.Measure(ShotChartArt.Fit(new string('W', 200), 100)), 100);
        }
    }
}
