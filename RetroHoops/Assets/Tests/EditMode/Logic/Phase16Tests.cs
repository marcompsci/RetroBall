using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 16: create-a-team, Season 2 content, defensive schemes, 1-on-1, Shootout, Caller Cup.</summary>
    public class CustomTeamTests
    {
        [Test]
        public void Clamp_CleansNamesAndIndexes()
        {
            var c = DefaultContent.Create();
            var d = new CustomTeamData
            {
                nickname = "  <Night>Owls!!  and more text ", city = "", abbreviation = "", primary = 99, shorts = -1,
                pattern = 42, logoMotif = -3, homeCourtId = DefaultContent.PracticeCourtId,
            };
            CustomTeams.Clamp(d, c);
            Assert.IsTrue(d.nickname.Length <= CustomTeams.MaxNameLength);
            Assert.IsFalse(d.nickname.Contains("<"));
            Assert.AreEqual("NIG", d.abbreviation);
            Assert.Less(d.primary, CustomTeams.Palette.Length);
            Assert.GreaterOrEqual(d.shorts, 0);
            Assert.Less(d.pattern, CustomTeams.PatternNames.Length);
            Assert.AreEqual("court.overpass_park", d.homeCourtId, "practice court isn't a home court");
        }

        [Test]
        public void Apply_AddsAPlayableTeam_InYourKit()
        {
            var c = DefaultContent.Create();
            var d = new CustomTeamData { created = true, nickname = "Comets", abbreviation = "MCM", primary = 7, shorts = 15, shoes = 2, pattern = 1 };
            var team = CustomTeams.Apply(c, d);
            Assert.IsNotNull(c.Team(CustomTeams.TeamId));
            Assert.AreEqual(TeamTier.Custom, team.tier);
            Assert.AreNotEqual("MCM", team.abbreviation, "can't copy a league team's scoreboard name");
            Assert.IsTrue(team.customKit);
            Assert.AreEqual(CustomTeams.Palette[15], team.shorts);
            Assert.AreEqual(TeamPattern.Stripes, team.pattern);
            Assert.AreEqual(CustomTeams.TeamId, Secrets.PlayableTeams(c, new SecretsSaveData())[0].id);
            Assert.IsTrue(CustomTeams.IsYours(team.id));

            var r = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = team.id, AwayTeamId = "team.metro_comets", CourtId = team.homeCourtId };
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            Assert.AreEqual(team.id, m.Setup.TeamA.id);
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());

            d.created = false;
            CustomTeams.Apply(c, d);
            Assert.IsNull(c.Team(CustomTeams.TeamId), "deleting the team removes it");
        }

        [Test]
        public void WearInRise_DressesTheCrew_AndCanBeUndone()
        {
            var c = DefaultContent.Create();
            var crew = c.Team(DefaultContent.PlayerCrewId);
            string original = crew.nickname;
            var d = new CustomTeamData { created = true, nickname = "Lanterns", primary = 2, useInRise = true };
            CustomTeams.Apply(c, d);
            Assert.AreEqual("Lanterns", crew.nickname);
            Assert.AreEqual(CustomTeams.Palette[2], crew.primary);
            Assert.IsTrue(crew.customKit);
            d.useInRise = false;
            CustomTeams.Apply(c, d);
            Assert.AreEqual(original, crew.nickname);
            Assert.IsFalse(crew.customKit);
        }

        [Test]
        public void CustomTeam_SurvivesSaveAndLoad()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.customTeam = new CustomTeamData { created = true, nickname = "Lanterns", city = "Fresno", abbreviation = "LAN", primary = 3, secondary = 8, accent = 1, shorts = 9, shoes = 4, pattern = 2, logoShape = 3, logoMotif = 6, homeCourtId = "court.rain_alley", useInRise = true };
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _).customTeam;
            Assert.IsTrue(back.created);
            Assert.AreEqual("Fresno", back.city);
            Assert.AreEqual("LAN", back.abbreviation);
            Assert.AreEqual(9, back.shorts);
            Assert.AreEqual(6, back.logoMotif);
            Assert.AreEqual("court.rain_alley", back.homeCourtId);
            Assert.IsTrue(back.useInRise);
        }

        [Test]
        public void ShortsColour_IsDrawn()
        {
            var look = new AppearanceDef(1, 0, 0, BodyType.Standard, 1);
            var red = RgbColor.FromHex("#FF0000");
            var green = RgbColor.FromHex("#00FF00");
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, red, red, red, null, TeamPattern.Solid, green);
            bool found = false;
            for (int i = 0; i < sheet.Pixels.Length && !found; i++) found = sheet.Pixels[i].Equals(green);
            Assert.IsTrue(found, "green shorts appear on the sheet");
        }
    }

    public class Season2Tests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Content_HasTheSecondRival_NewCourts_AndValidates()
        {
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid, ContentValidator.Validate(_c).ToString());
            Assert.AreEqual(TeamTier.Rival, _c.Team(DefaultContent.Rival2CrewId).tier);
            Assert.IsNotNull(_c.Player(DefaultContent.Rival2LeaderId));
            foreach (var id in new[] { "court.sundown_yard", "court.rain_alley", "court.snowline_park" }) Assert.IsNotNull(_c.Court(id), id);
            Assert.IsNotNull(_c.Find(_c.Cosmetics, "cosmetic.jersey.last_light"));
        }

        [Test]
        public void Rivals_Alternate_BySeason()
        {
            Assert.AreEqual(DefaultContent.RivalCrewId, RivalEngine.RivalFor(1));
            Assert.AreEqual(DefaultContent.Rival2CrewId, RivalEngine.RivalFor(2));
            Assert.AreEqual(DefaultContent.Rival3CrewId, RivalEngine.RivalFor(3));
            Assert.AreEqual(DefaultContent.Rival4CrewId, RivalEngine.RivalFor(4), "the four rivals take turns");
            Assert.AreEqual(DefaultContent.Rival5CrewId, RivalEngine.RivalFor(5));
            Assert.AreEqual(DefaultContent.Rival6CrewId, RivalEngine.RivalFor(6), "Phase 31: six rivals");
            Assert.AreEqual(DefaultContent.RivalCrewId, RivalEngine.RivalFor(7));
            Assert.AreEqual(DefaultContent.Rival2CrewId, RivalEngine.RivalFor(8));
        }

        [Test]
        public void BeatingTheSyndicate_IsTracked_AndTriggersChapterTwo()
        {
            var d = Career.New(_c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 2, currentWeek = 6 };
            Assert.AreEqual(Story.Season2, Story.Pending(d));
            Story.MarkSeen(d, Story.Season2);
            Assert.AreEqual(Story.Rival2Intro, Story.Pending(d), "season 2 brings the Syndicate, not Neon Static");
            var req = RivalEngine.Challenge(d, _c, "difficulty.caller");
            Assert.AreEqual(DefaultContent.Rival2CrewId, req.AwayTeamId);

            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival2CrewId };
            Assert.AreEqual(RivalOutcome.Won, RivalEngine.ApplyResult(d, s));
            Assert.AreEqual(1, d.rival.sundownWins);
            Story.MarkSeen(d, Story.Rival2Intro);
            Assert.AreEqual(Story.Rival2Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.sundown").Earned(d));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.rival.sundownWins);
        }

        [Test]
        public void ChapterTwo_HasSpanishLinesForEveryScene()
        {
            foreach (var id in new[] { Story.Season2, Story.Rival2Intro, Story.Rival2Beaten, Story.TwoTime })
            {
                var en = Story.Beat(id, "Rook", Loc.English);
                var es = Story.Beat(id, "Rook", Loc.Spanish);
                Assert.IsNotNull(en, id);
                Assert.IsNotNull(es, id);
                Assert.AreEqual(en.Lines.Count, es.Lines.Count, id);
            }
            Assert.IsTrue(Story.Beat(Story.Rival2Intro, "Rook", Loc.English).Lines.Exists(l => l.Speaker == StorySpeaker.Rival2));
        }

        [Test]
        public void AlleyOopsAndHeatUps_CountTowardTotals()
        {
            var d = Career.New(_c);
            var line = new PlayerStatLine { alleyOops = 2, alleyOopPasses = 1, heatUps = 3, points = 10 };
            var s = new MatchSummary
            {
                mode = GameMode.QuickCall, matchId = "x", humanTeam = 0, winner = 0, scoreA = 21, scoreB = 10,
                lines = new List<SummaryLine> { new SummaryLine { playerIndex = 0, team = 0, isHuman = true, stats = line } },
            };
            Career.ApplyMatch(d, s, default);
            Assert.AreEqual(3, d.totals.alleyOops);
            Assert.AreEqual(3, d.totals.heatUps);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(3, back.totals.alleyOops);
            Assert.AreEqual(3, back.totals.heatUps);
        }
    }

    public class SchemeAndOneOnOneTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void AiTeams_DefendInTheirFavouriteScheme_YourTeamPlaysMan()
        {
            var r = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = "team.desert_drifters", AwayTeamId = "team.baycity_breakers", Seed = 3 };
            var m = new MatchSimulation(MatchSetup.FromRequest(r, _c));
            Assert.AreEqual(DefenseScheme.ManToMan, m.SchemeOf(0), "your AI teammates play man");
            Assert.AreEqual(DefenseScheme.Zone, m.SchemeOf(1));
            Assert.AreEqual("ZONE", MatchSimulation.SchemeName(m.SchemeOf(1)));
        }

        [Test]
        public void SmartAi_GetsOutOfTheZone_WhenThreesKeepFalling()
        {
            var r = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = "team.metro_comets", AwayTeamId = "team.baycity_breakers", Seed = 3, DifficultyId = "difficulty.legend" };
            var setup = MatchSetup.FromRequest(r, _c);
            setup.Shot.debugGreenAlwaysMakes = true;
            setup.KeepPossessionAfterScore = true;
            var m = new MatchSimulation(setup);
            var log = new List<MatchEvent>();
            for (int shot = 0; shot < 4 && m.SchemeOf(1) == DefenseScheme.Zone; shot++)
            {
                for (int i = 0; i < 600 && !(m.HumanHasBall && m.Phase != MatchPhase.DeadBall && m.ChargingIndex < 0); i++) { m.Step(Dt, default); log.AddRange(m.Events); }
                m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
                for (int i = 0; i < 120 && m.ChargingIndex >= 0 && m.ChargeMeter < m.Setup.Shot.greenCenter - 0.01f; i++) m.Step(Dt, new PlayerInput { ShootHeld = true });
                m.Step(Dt, default);
                for (int i = 0; i < 600 && m.Phase != MatchPhase.CheckBall; i++) { m.Step(Dt, default); log.AddRange(m.Events); }
            }
            Assert.AreEqual(DefenseScheme.ManToMan, m.SchemeOf(1), "two threes in a row should pull the zone out");
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.SchemeChanged && e.Team == 1));
        }

        [Test]
        public void OneOnOne_OnlyTheLeadersTouchTheBall()
        {
            var r = new MatchRequest { Mode = GameMode.OneOnOne, HomeTeamId = "team.metro_comets", AwayTeamId = "team.harbor_hounds", RulesId = "rules.oneonone", Seed = 8 };
            var setup = MatchSetup.FromRequest(r, _c);
            setup.Demo = true; // let the AI play both leaders
            var m = new MatchSimulation(setup);
            Assert.IsTrue(m.IsBenched(1));
            Assert.IsFalse(m.IsBenched(0));
            for (int i = 0; i < 60 * 200 && !m.IsOver; i++)
            {
                m.Step(Dt, default);
                if (m.Ball.IsHeld) Assert.IsFalse(m.IsBenched(m.Ball.HolderIndex), "a benched player got the ball");
            }
            Assert.IsTrue(m.IsOver);
            for (int i = 0; i < m.Players.Length; i++)
                if (m.IsBenched(i)) Assert.AreEqual(0, m.Stats[i].points + m.Stats[i].rebounds + m.Stats[i].steals);
            Assert.Greater(m.Score[0] + m.Score[1], 0);
        }
    }

    public class ShootoutAndCupTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void CpuShootout_IsDeterministic_AndBetterShootersScoreMore()
        {
            var t = ShotTuning.Default;
            var shooter = Shootout.PickShooter(_c, "team.metro_comets");
            Assert.IsNotNull(shooter);
            var legend = _c.Difficulty("difficulty.legend");
            int a = Shootout.SimulateCpu(shooter, legend, t, 42, 7.5f);
            Assert.AreEqual(a, Shootout.SimulateCpu(shooter, legend, t, 42, 7.5f));
            Assert.AreEqual(0, a % 2, "every CPU make is a money ball");
            int good = 0, bad = 0;
            var brick = new PlayerDef { attributes = new AttributeSet { shooting = 20 } };
            var star = new PlayerDef { attributes = new AttributeSet { shooting = 95 } };
            for (uint s = 1; s <= 20; s++)
            {
                good += Shootout.SimulateCpu(star, legend, t, s, 7.5f);
                bad += Shootout.SimulateCpu(brick, legend, t, s, 7.5f);
            }
            Assert.Greater(good, bad);
        }

        [Test]
        public void ShootoutWin_NeedsMorePointsThanTheCpu()
        {
            var r = MatchRequest.PracticeDefault();
            r.Drill = (int)DrillKind.Shootout;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, _c));
            var s = new PracticeSession(DrillKind.Shootout, m);
            Assert.IsTrue(s.ThreePointStyle);
            Assert.AreEqual(5, s.MoneySpots.Count);
            s.SetCpu(0, "TEST");
            for (int i = 0; i < 60 * 70 && !s.Finished; i++) { m.Step(1f / 60f, default); s.Update(m, 1f / 60f); }
            Assert.IsTrue(s.Finished);
            Assert.IsFalse(s.ShootoutWon, "0 vs 0 is not a win");
            Assert.IsTrue(s.ResultText().Contains("TEST"));
            var d = Career.New(_c);
            Career.RecordPractice(d, 0, 0, 0, 0f, 0, 0, true);
            Assert.AreEqual(1, d.practice.shootoutWins);
        }

        private static MatchSummary Result(bool won) => new MatchSummary
        {
            mode = GameMode.Cup, humanTeam = 0, winner = won ? 0 : 1, scoreA = won ? 21 : 15, scoreB = won ? 15 : 21, matchId = "c",
        };

        [Test]
        public void Cup_IsEightTeams_YouAreTheEighthSeed()
        {
            var cup = new CupSaveData();
            CupEngine.Start(cup, _c, DefaultContent.PlayerCrewId);
            Assert.AreEqual(CupEngine.Teams, cup.bracket.Count);
            Assert.AreEqual(DefaultContent.PlayerCrewId, cup.bracket[7]);
            Assert.AreEqual(4, cup.games.Count);
            Assert.AreEqual(cup.bracket.Count, new HashSet<string>(cup.bracket).Count);
            var next = CupEngine.NextMatch(cup, _c, "difficulty.caller");
            Assert.AreEqual(GameMode.Cup, next.Mode);
            Assert.AreEqual(cup.bracket[0], next.AwayTeamId, "the eighth seed opens against the top seed");
        }

        [Test]
        public void WinningThreeGames_WinsTheCup()
        {
            var d = Career.New(_c);
            CupEngine.Start(d.cup, _c, DefaultContent.PlayerCrewId);
            int sp = d.signalPoints;
            Assert.AreEqual(CupOutcome.Advanced, CupEngine.ApplyResult(d.cup, _c, Result(true), d, out _));
            Assert.AreEqual(2, CupEngine.NextGame(d.cup).round);
            Assert.AreEqual(CupOutcome.Advanced, CupEngine.ApplyResult(d.cup, _c, Result(true), d, out _));
            Assert.AreEqual(3, CupEngine.NextGame(d.cup).round);
            Assert.AreEqual(CupOutcome.Champion, CupEngine.ApplyResult(d.cup, _c, Result(true), d, out int bonus));
            Assert.AreEqual(CupEngine.TitleBonus, bonus);
            Assert.AreEqual(sp + bonus, d.signalPoints);
            Assert.AreEqual(1, d.cup.titles);
            Assert.AreEqual(DefaultContent.PlayerCrewId, d.cup.championId);
            Assert.AreEqual(7, d.cup.games.Count, "4 + 2 + 1 games");
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.cup.titles);
            Assert.AreEqual(7, back.cup.games.Count);
        }

        [Test]
        public void LosingEarly_StillCrownsAChampion()
        {
            var d = Career.New(_c);
            CupEngine.Start(d.cup, _c, DefaultContent.PlayerCrewId);
            Assert.AreEqual(CupOutcome.Eliminated, CupEngine.ApplyResult(d.cup, _c, Result(false), d, out _));
            Assert.IsTrue(d.cup.finished);
            Assert.IsNotNull(d.cup.championId);
            Assert.AreNotEqual(DefaultContent.PlayerCrewId, d.cup.championId);
            Assert.IsNull(CupEngine.NextMatch(d.cup, _c, "difficulty.caller"));
        }
    }
}
