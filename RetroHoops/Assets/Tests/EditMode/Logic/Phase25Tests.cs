using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Dunk packages and dunk looks.</summary>
    public class DunkStyleTests
    {
        [Test]
        public void EveryStyle_HasANameAPoseAndLift()
        {
            foreach (DunkStyle s in System.Enum.GetValues(typeof(DunkStyle)))
            {
                StringAssert.Contains("!", Dunks.Name(s) + (s == DunkStyle.TwoHand ? "!" : ""));
                Assert.GreaterOrEqual(Dunks.LiftScale(s), 1f);
                bool posed = false;
                for (float u = 0f; u < 1f; u += 0.05f) posed |= !Dunks.Pose(s, u).IsNone;
                Assert.IsTrue(posed, s.ToString());
                Assert.IsTrue(Dunks.Pose(s, 1.2f).IsNone, "nothing after the dunk");
            }
        }

        [Test]
        public void ThreeSixty_Spins_AndReverse_FacesAway()
        {
            var flips = new HashSet<bool>();
            for (float u = 0f; u < 1f; u += 0.05f) flips.Add(Dunks.Pose(DunkStyle.ThreeSixty, u).FlipOverride);
            Assert.AreEqual(2, flips.Count, "facing turns during a 360");
            Assert.IsTrue(Dunks.Pose(DunkStyle.Reverse, 0.5f).FlipOverride);
        }

        [Test]
        public void Ai_OnlyFlyersGoFancy()
        {
            for (float r = 0f; r < 1f; r += 0.01f)
            {
                Assert.LessOrEqual((int)Dunks.ForAi(60, r), (int)DunkStyle.Tomahawk);
                Assert.AreNotEqual(DunkStyle.ThreeSixty, Dunks.ForAi(75, r));
            }
            var seen = new HashSet<DunkStyle>();
            for (float r = 0f; r < 1f; r += 0.01f) seen.Add(Dunks.ForAi(95, r));
            Assert.AreEqual(6, seen.Count, "elite finishers use every store package");
        }

        [Test]
        public void Packages_AreInTheStore_AndMapToStyles()
        {
            var c = DefaultContent.Create();
            var packs = c.Cosmetics.FindAll(x => x.slot == CosmeticSlot.DunkPackage);
            Assert.GreaterOrEqual(packs.Count, 7);
            Assert.IsTrue(packs.Exists(p => p.unlockedByDefault), "one default package");
            foreach (var p in packs)
                if (p.id != "cosmetic.dunk.two_hand") Assert.AreNotEqual(DunkStyle.TwoHand, Dunks.ForCosmetic(p.id), p.id);
            Assert.AreEqual(DunkStyle.Skyline, Dunks.ForCosmetic("cosmetic.pass.dunk.skyline"));
            Assert.IsTrue(c.Find(c.Cosmetics, "cosmetic.pass.dunk.skyline").passOnly);
            Assert.IsTrue(ContentValidator.Validate(c).IsValid);
        }

        [Test]
        public void EquippedDunk_SurvivesTheSave()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.signalPoints = 5000;
            d.fans = 5000;
            Assert.AreEqual(CosmeticCheck.Ok, Career.Buy(d, c.Find(c.Cosmetics, "cosmetic.dunk.windmill")));
            Assert.AreEqual("cosmetic.dunk.windmill", d.Equipped(CosmeticSlot.DunkPackage));
            d.settings.landscapeAll = true;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual("cosmetic.dunk.windmill", back.equippedDunk);
            Assert.IsTrue(back.settings.landscapeAll);
        }
    }

    /// <summary>Euro step and the AI protecting the rim.</summary>
    public class DriveDefenseTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation LiveMatch(bool passive, int playmaking = 80)
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 21;
            var setup = MatchSetup.FromRequest(r, c);
            setup.PassiveOpponents = passive;
            var me = setup.RosterA[0];
            setup.RosterA[0] = new PlayerDef
            {
                id = "test.me", firstName = "T", lastName = "Me", jerseyNumber = 1, archetypeId = me.archetypeId,
                attributes = new AttributeSet { finishing = 90, speed = 75, stamina = 90, playmaking = playmaking, shooting = 60 },
                appearance = me.appearance,
            };
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            Assert.IsTrue(m.HumanHasBall);
            return m;
        }

        [Test]
        public void GoodHandler_EuroStepsADefenderInTheLane()
        {
            var m = LiveMatch(passive: true);
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 6.5f);
            int d = MatchSimulation.IndexOf(1, 0);
            m.Players[d].Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 5.2f);
            var log = new List<MatchEvent>();
            m.Step(Dt, new PlayerInput { LayupPressed = true });
            log.AddRange(m.Events);
            float maxSide = 0f;
            for (int i = 0; i < 40; i++)
            {
                m.Players[d].Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 5.2f); // planted in the lane
                m.Step(Dt, default);
                log.AddRange(m.Events);
                maxSide = System.Math.Max(maxSide, System.Math.Abs(m.Controlled.Position.x));
            }
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.EuroStep && e.PlayerIndex == m.ControlledIndex));
            Assert.Greater(maxSide, 0.4f, "went around, not through");
        }

        [Test]
        public void PoorHandler_DoesntEuroStep()
        {
            var m = LiveMatch(passive: true, playmaking: 30);
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 6.5f);
            int d = MatchSimulation.IndexOf(1, 0);
            var log = new List<MatchEvent>();
            m.Step(Dt, new PlayerInput { LayupPressed = true });
            for (int i = 0; i < 40; i++)
            {
                m.Players[d].Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 5.2f);
                m.Step(Dt, default);
                log.AddRange(m.Events);
            }
            Assert.IsFalse(log.Exists(e => e.Type == MatchEventType.EuroStep));
        }

        [Test]
        public void Defense_SendsSomeoneToTheRim_OnADrive()
        {
            int helped = 0, trials = 12;
            for (int k = 0; k < trials; k++)
            {
                var m = LiveMatch(passive: false);
                m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(2.5f, 6.5f);
                m.Step(Dt, new PlayerInput { DunkPressed = true });
                bool help = false;
                for (int i = 0; i < 60 && !help && m.DrivingIndex >= 0; i++)
                {
                    m.Step(Dt, default);
                    for (int p = 0; p < m.Players.Length; p++)
                        if (m.Players[p].Team != m.Setup.HumanTeam && m.AiStateOf(p).Intent == AiIntent.Help) help = true;
                }
                if (help) helped++;
            }
            Assert.Greater(helped, trials / 3, "a helper rotates to the rim on most drives");
        }
    }

    /// <summary>Season 5: the Cassette Club and the new courts.</summary>
    public class Season5Tests
    {
        private ContentCatalog _c;

        [SetUp]
        public void Setup() => _c = DefaultContent.Create();

        [Test]
        public void CassetteClub_IsTheFifthRival_WithItsCourtAndStory()
        {
            var team = _c.Team(DefaultContent.Rival5CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.IsNotNull(_c.Court(team.homeCourtId));
            Assert.IsNotNull(_c.Court("court.night_bus_depot"));
            Assert.IsNotNull(_c.Player(DefaultContent.Rival5LeaderId));
            Assert.AreEqual(DefaultContent.Rival5CrewId, RivalEngine.RivalFor(5));
            Assert.AreEqual(DefaultContent.Rival5CrewId, RivalEngine.RivalFor(11), "six rivals take turns since Phase 31");

            var d = Career.New(_c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 5, currentWeek = 6 };
            Assert.AreEqual(Story.Rival5Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival5CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.cassetteWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d));
            Story.MarkSeen(d, Story.Rival5Intro);
            Assert.AreEqual(Story.Rival5Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.cassette").Earned(d));
            foreach (var id in new[] { Story.Rival5Intro, Story.Rival5Beaten })
                Assert.AreEqual(Story.Beat(id, "Rook", Loc.English).Lines.Count, Story.Beat(id, "Rook", Loc.Spanish).Lines.Count, id);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.rival.cassetteWins);
        }

        [Test]
        public void ThirdPassSet_IsPassOnlyGear()
        {
            Assert.AreEqual(4, HoopsPass.GearSets.Length, "Phase 31 added Season 6's set");
            foreach (var id in HoopsPass.GearSets[2])
            {
                var def = _c.Find(_c.Cosmetics, id);
                Assert.IsNotNull(def, id);
                Assert.IsTrue(def.passOnly);
            }
        }

        [Test]
        public void SidelineStands_HaveRowsAndFitTheCourt()
        {
            var s = CrowdGenerator.SidelineStands(400, RgbColor.FromHex("#334455"), RgbColor.FromHex("#AA8866"), RgbColor.FromHex("#FF0000"));
            Assert.AreEqual(400, s.Width);
            foreach (var p in s.Pixels) Assert.AreEqual(255, p.a, "solid bleachers");
            for (int row = 0; row < CrowdGenerator.StandRows; row++)
                Assert.Less(CrowdGenerator.StandRowFloor(row) + CrowdGenerator.Height - 2, s.Height + 4);
        }
    }
}
