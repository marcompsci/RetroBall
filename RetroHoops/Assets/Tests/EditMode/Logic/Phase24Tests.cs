using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>DUNK and LAYUP buttons, and baseline alley-oops.</summary>
    public class FinishButtonTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation LiveMatch(int finishing = 90)
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 5;
            var setup = MatchSetup.FromRequest(r, c);
            setup.PassiveOpponents = true;
            var me = setup.RosterA[0];
            setup.RosterA[0] = new PlayerDef
            {
                id = "test.me", firstName = "Test", lastName = "Me", jerseyNumber = 1, archetypeId = me.archetypeId,
                attributes = new AttributeSet { finishing = finishing, speed = 70, stamina = 90, shooting = 60, playmaking = 60 },
                appearance = me.appearance,
            };
            setup.RosterA[1] = new PlayerDef
            {
                id = "test.finisher", firstName = "Test", lastName = "Finisher", jerseyNumber = 77,
                archetypeId = setup.RosterA[1].archetypeId, attributes = new AttributeSet { finishing = 90, speed = 70, stamina = 80 },
                appearance = setup.RosterA[1].appearance,
            };
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            Assert.AreEqual(MatchPhase.Live, m.Phase);
            Assert.IsTrue(m.HumanHasBall);
            return m;
        }

        private static List<MatchEvent> Run(MatchSimulation m, PlayerInput first, int steps, System.Func<bool> until = null)
        {
            var log = new List<MatchEvent>();
            m.Step(Dt, first);
            log.AddRange(m.Events);
            for (int i = 0; i < steps && (until == null || !until()); i++)
            {
                m.Step(Dt, default);
                log.AddRange(m.Events);
            }
            return log;
        }

        [Test]
        public void Dunk_AtTheRim_GoesUpAndReleasesOnItsOwn()
        {
            var m = LiveMatch();
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(0.2f, 1.5f);
            var log = Run(m, new PlayerInput { DunkPressed = true }, 120, () => m.Ball.Phase == BallPhase.Shot);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.ShotStarted && e.Value == (int)ShotType.Dunk));
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.ShotReleased), "no need to hold and release");
            Assert.AreEqual(ShotType.Dunk, m.Ball.ShotType);
        }

        [Test]
        public void Dunk_WithoutTheFinishing_IsALayup()
        {
            var m = LiveMatch(finishing: 40);
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(0.2f, 1.5f);
            var log = Run(m, new PlayerInput { DunkPressed = true }, 120, () => m.Ball.Phase == BallPhase.Shot);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.DunkToLayup));
            Assert.AreEqual(ShotType.Layup, m.Ball.ShotType);
        }

        [Test]
        public void Layup_FromOutside_DrivesToTheRimThenFinishes()
        {
            var m = LiveMatch();
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(1.5f, 6f);
            float start = m.Setup.Court.DistanceToHoop(m.Controlled.Position);
            var log = Run(m, new PlayerInput { LayupPressed = true }, 60 * 3, () => m.Ball.Phase == BallPhase.Shot);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.DriveStarted && e.Value == (int)ShotType.Layup));
            Assert.AreEqual(BallPhase.Shot, m.Ball.Phase, "finished within the drive");
            Assert.AreEqual(ShotType.Layup, m.Ball.ShotType);
            Assert.Less(m.Setup.Court.DistanceToHoop(m.Ball.FlightFrom), start - 3f);
            Assert.LessOrEqual(m.Setup.Court.DistanceToHoop(m.Ball.FlightFrom), MatchSimulation.LayupReach + 0.3f);
        }

        [Test]
        public void Dunk_FromOutside_DrivesAndThrowsItDown()
        {
            var m = LiveMatch();
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(-1f, 5.5f);
            Run(m, new PlayerInput { DunkPressed = true }, 60 * 3, () => m.Ball.Phase == BallPhase.Shot);
            Assert.AreEqual(BallPhase.Shot, m.Ball.Phase);
            Assert.AreEqual(ShotType.Dunk, m.Ball.ShotType);
        }

        [Test]
        public void PullingTheStickAway_CancelsTheDrive()
        {
            var m = LiveMatch();
            m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 7f);
            m.Step(Dt, new PlayerInput { DunkPressed = true });
            Assert.AreEqual(m.ControlledIndex, m.DrivingIndex);
            // Stick toward half court (+y in the attacking frame).
            for (int i = 0; i < 10; i++) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, 1f) });
            Assert.AreEqual(-1, m.DrivingIndex);
            Assert.IsTrue(m.HumanHasBall);
        }

        [Test]
        public void ReleaseSpread_IsTighterForBetterFinishers()
        {
            float goodLo = MatchSimulation.FinishReleaseMeter(95, 0f), goodHi = MatchSimulation.FinishReleaseMeter(95, 1f);
            float poorLo = MatchSimulation.FinishReleaseMeter(40, 0f), poorHi = MatchSimulation.FinishReleaseMeter(40, 1f);
            Assert.Less(goodHi - goodLo, poorHi - poorLo);
            Assert.AreEqual(ShotTuning.Default.greenCenter, MatchSimulation.FinishReleaseMeter(70, 0.5f), 1e-4);
        }

        [Test]
        public void ButtonFinishes_MakeMoreThanHalfWhenOpen()
        {
            int made = 0, n = 60;
            for (int k = 0; k < n; k++)
            {
                var m = LiveMatch();
                m.Controlled.Motion.position = m.Setup.Court.Hoop + new Vec2(0.3f, 1.4f);
                var log = Run(m, new PlayerInput { LayupPressed = true }, 240, () => m.Phase != MatchPhase.Live);
                if (log.Exists(e => e.Type == MatchEventType.ShotMade)) made++;
            }
            Assert.Greater(made, n / 2);
        }
    }

    public class BaselineOopTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation LiveMatch()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 9;
            var setup = MatchSetup.FromRequest(r, c);
            setup.PassiveOpponents = true;
            setup.RosterA[1] = new PlayerDef
            {
                id = "test.flyer", firstName = "Test", lastName = "Flyer", jerseyNumber = 23,
                archetypeId = setup.RosterA[1].archetypeId, attributes = new AttributeSet { finishing = 92, speed = 80, stamina = 90 },
                appearance = setup.RosterA[1].appearance,
            };
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            Assert.IsTrue(m.HumanHasBall);
            return m;
        }

        private static void MakeRunner(MatchSimulation m, int i)
        {
            var s = m.AiStateOf(i);
            s.Intent = AiIntent.BaselineRun;
            s.IntentUntil = m.Time + 3f;
            s.Target = new Vec2(-1.2f, m.Setup.Court.hoopY + 0.5f);
            s.NextDecision = m.Time + 5f;
            m.Players[i].Motion.position = new Vec2(3.2f, 0.8f);
            m.Players[i].Motion.velocity = new Vec2(-4f, 0f);
        }

        [Test]
        public void BaselineRunner_IsALobTarget_FurtherOutThanAStandingTeammate()
        {
            var m = LiveMatch();
            m.Controlled.Motion.position = new Vec2(0f, 7.5f);
            m.Players[1].Motion.position = new Vec2(3.2f, 0.8f);
            Assert.IsFalse(m.IsAlleyOopTarget(1), "standing 3 m out isn't an oop");
            MakeRunner(m, 1);
            Assert.IsTrue(m.IsRunningBaseline(1));
            Assert.IsTrue(m.IsAlleyOopTarget(1));
            Assert.AreEqual(1, m.AlleyOopCandidate(m.ControlledIndex));
        }

        [Test]
        public void Pass_GoesUpAsAnOop_ToTheRunner_AndTheyThrowItDown()
        {
            var m = LiveMatch();
            m.Controlled.Motion.position = new Vec2(-2f, 7.5f);
            MakeRunner(m, 1);
            // Aim at the other wing: the runner still gets the lob.
            m.Step(Dt, new PlayerInput { PassPressed = true, Move = new Vec2(-1f, 0f) });
            Assert.IsTrue(m.AlleyOopInFlight);
            Assert.AreEqual(1, m.Ball.TargetIndex);
            var log = new List<MatchEvent>();
            for (int i = 0; i < 300 && !log.Exists(e => e.Type == MatchEventType.AlleyOop); i++)
            {
                m.Step(Dt, default);
                log.AddRange(m.Events);
            }
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.AlleyOop && e.PlayerIndex == 1));
            Assert.AreEqual(ShotType.Dunk, m.Ball.ShotType);
        }

        [Test]
        public void AthleticTeammates_RunTheBaseline_WhileYouHandle()
        {
            var m = LiveMatch();
            bool ran = false;
            for (int i = 0; i < 60 * 10 && !ran && m.HumanHasBall; i++)
            {
                m.Controlled.Motion.position = new Vec2(0f, 7.5f);
                m.Step(Dt, default);
                for (int k = 0; k < m.Players.Length; k++)
                    if (m.Players[k].Team == m.Setup.HumanTeam && m.AiStateOf(k).Intent == AiIntent.BaselineRun) ran = true;
            }
            Assert.IsTrue(ran);
        }
    }

    public class ControlLayoutTests
    {
        [Test]
        public void DefaultArc_HasEveryButton_NoOverlaps_ShootBiggest()
        {
            var d = ControlLayout.Default();
            Assert.AreEqual(System.Enum.GetValues(typeof(ControlButton)).Length, d.Length);
            for (int i = 0; i < d.Length; i++) Assert.AreEqual((ControlButton)i, d[i].Button);
            Assert.AreEqual(0, ControlLayout.Overlapping(d).Count);
            foreach (var s in d) Assert.LessOrEqual(s.Size, d[(int)ControlButton.Shoot].Size);
            // Everything fits a 1080-wide portrait screen and a 1080-tall landscape one.
            foreach (var s in d)
            {
                Assert.GreaterOrEqual(1080f + s.X - s.Size / 2f, 0f);
                Assert.LessOrEqual(s.Y + s.Size / 2f, 1080f);
            }
        }

        [Test]
        public void SaveString_RoundTrips_AndOnlyStoresChanges()
        {
            Assert.AreEqual("", ControlLayout.Serialize(ControlLayout.Default()));
            var slots = ControlLayout.Default();
            slots[(int)ControlButton.Dunk] = new ControlSlot(ControlButton.Dunk, -300f, 600f, 200f);
            string saved = ControlLayout.Serialize(slots);
            StringAssert.Contains("2:", saved);
            StringAssert.DoesNotContain("0:", saved);
            var back = ControlLayout.Parse(saved);
            Assert.AreEqual(-300f, back[2].X, 0.5f);
            Assert.AreEqual(600f, back[2].Y, 0.5f);
            Assert.AreEqual(200f, back[2].Size, 0.5f);
            Assert.AreEqual(ControlLayout.Default()[0].X, back[0].X);
        }

        [Test]
        public void BadStrings_AreIgnored_AndValuesClamped()
        {
            var d = ControlLayout.Default();
            var p = ControlLayout.Parse("junk;9:1,2,3;1:a,b,c;0:500,-50,9999;3:-5000,5000,1");
            Assert.LessOrEqual(p[0].X, -p[0].Size / 2f, "kept on screen");
            Assert.GreaterOrEqual(p[0].Y, p[0].Size / 2f);
            Assert.LessOrEqual(p[0].Size, d[0].Size * ControlLayout.MaxScale + 0.01f);
            Assert.GreaterOrEqual(p[3].Size, d[3].Size * ControlLayout.MinScale - 0.01f);
            Assert.GreaterOrEqual(p[3].X, -ControlLayout.MaxReachX);
            Assert.AreEqual(d[1].X, p[1].X, "unparseable entry ignored");
        }

        [Test]
        public void Layout_SurvivesTheSaveFile()
        {
            var c = DefaultContent.Create();
            var career = Career.New(c);
            var slots = ControlLayout.Default();
            slots[0] = new ControlSlot(ControlButton.Shoot, -250f, 300f, 280f);
            career.settings.controlLayout = ControlLayout.Serialize(slots);
            var back = SaveCodec.Decode(SaveCodec.Encode(career), c, out _);
            Assert.AreEqual(career.settings.controlLayout, back.settings.controlLayout);
        }

        [Test]
        public void Icons_ExistForEveryRole()
        {
            foreach (ControlIcon icon in System.Enum.GetValues(typeof(ControlIcon)))
            {
                var c = CallerRetroBall.Logic.PixelArt.ControlIconGenerator.Generate(icon);
                int lit = 0;
                foreach (var p in c.Pixels) if (p.a > 0) lit++;
                Assert.Greater(lit, 20, icon.ToString());
            }
            Assert.AreEqual(ControlIcon.Steal, CallerRetroBall.Logic.PixelArt.ControlIconGenerator.For(ControlButton.Dunk, true));
            Assert.AreEqual(ControlIcon.Oop, CallerRetroBall.Logic.PixelArt.ControlIconGenerator.For(ControlButton.Pass, false, true));
            Assert.AreEqual(ControlIcon.Block, CallerRetroBall.Logic.PixelArt.ControlIconGenerator.For(ControlButton.Shoot, true));
        }
    }

    public class LandscapeTests
    {
        [Test]
        public void Landscape_PutsTheBasketsLeftAndRight()
        {
            var g = FullCourt.Geometry();
            float L = g.depth;
            var top = LandscapeMath.ToScreenPlane(g.Hoop, true, L);
            var bottom = LandscapeMath.ToScreenPlane(new Vec2(0f, L - g.hoopY), true, L);
            Assert.Less(top.x, 0f);
            Assert.Greater(bottom.x, 0f);
            Assert.AreEqual(0f, top.y, 1e-4);
            Assert.AreEqual(-top.x, bottom.x, 1e-4, "centred");
            // Sidelines run along the top and bottom of the screen.
            Assert.AreEqual(g.HalfWidth, LandscapeMath.ToScreenPlane(new Vec2(g.HalfWidth, 5f), true, L).y, 1e-4);
        }

        [Test]
        public void ScreenPlane_RoundTrips_BothViews()
        {
            foreach (bool land in new[] { false, true })
            {
                var p = new Vec2(3.2f, 17.5f);
                var back = LandscapeMath.FromScreenPlane(LandscapeMath.ToScreenPlane(p, land, 28f), land, 28f);
                Assert.AreEqual(p.x, back.x, 1e-4);
                Assert.AreEqual(p.y, back.y, 1e-4);
            }
        }

        [Test]
        public void StickRight_MovesTowardTheRightBasket_InLandscape()
        {
            // Pushing the stick right moves along the court toward the bottom (right-hand) basket.
            var d = LandscapeMath.ScreenToCourt(new Vec2(1f, 0f), true);
            Assert.AreEqual(1f, d.y, 1e-4);
            var screen = LandscapeMath.ToScreenPlane(new Vec2(0f, 10f) + d, true, 28f) - LandscapeMath.ToScreenPlane(new Vec2(0f, 10f), true, 28f);
            Assert.AreEqual(1f, screen.x, 1e-4);
            // Stick up moves toward the far sideline (up the screen).
            var up = LandscapeMath.ScreenToCourt(new Vec2(0f, 1f), true);
            Assert.AreEqual(1f, LandscapeMath.ToScreenPlane(up, true, 0f).y, 1e-4);
            // Portrait unchanged: stick up is toward the hoop (-y).
            Assert.AreEqual(-1f, LandscapeMath.ScreenToCourt(new Vec2(0f, 1f), false).y, 1e-4);
        }

        [Test]
        public void Facings_TurnWithTheCourt()
        {
            // Running toward the right-hand basket (court +y = facing N) uses the sprite that faces right (E).
            Assert.AreEqual(Facing8.E, LandscapeMath.TurnFacing(Movement.FacingOf(new Vec2(0f, 1f))));
            // Running up the screen (court +x) uses the sprite that faces up the screen (S in the portrait set).
            Assert.AreEqual(Facing8.S, LandscapeMath.TurnFacing(Movement.FacingOf(new Vec2(1f, 0f))));
        }

        [Test]
        public void LowerOnScreen_DrawsInFront()
        {
            Assert.Greater(LandscapeMath.Depth(new Vec2(-3f, 10f), true), LandscapeMath.Depth(new Vec2(3f, 10f), true));
            Assert.Greater(LandscapeMath.Depth(new Vec2(0f, 8f), false), LandscapeMath.Depth(new Vec2(0f, 2f), false));
        }

        [Test]
        public void Canvas_UsesALandscapeReferenceWhenSideways()
        {
            LandscapeMath.Canvas(2532, 1170, out float w, out float h, out float m);
            Assert.AreEqual(1920f, w);
            Assert.AreEqual(1080f, h);
            Assert.AreEqual(1f, m, "wide phones fit the height");
            LandscapeMath.Canvas(2732, 2048, out w, out h, out m);
            Assert.AreEqual(0f, m, "iPad landscape fits the width");
            LandscapeMath.Canvas(1170, 2532, out w, out h, out m);
            Assert.AreEqual(1080f, w);
            Assert.AreEqual(0.35f, m, 1e-4);
        }

        [Test]
        public void LandscapeZoom_FitsTheFullCourtOnAPhone()
        {
            // iPhone 15-class landscape: 18 m top to bottom at an integer zoom, and the whole 28 m floor across.
            int zoom = CameraMath.IntegerZoom(2532, 1170, 11.5f, 18f, 16f);
            float tall = 2f * CameraMath.OrthographicSize(1170, zoom, 16f);
            float wide = 2532f / (zoom * 16f);
            Assert.GreaterOrEqual(tall, 18f);
            Assert.GreaterOrEqual(wide, FullCourt.Geometry().depth);
        }
    }
}
