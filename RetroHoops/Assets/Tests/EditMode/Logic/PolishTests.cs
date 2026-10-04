using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 9: celebrations, dribble moves, bursts, crowd ambience.</summary>
    public class FlairTests
    {
        [Test]
        public void CelebrationFor_MapsCosmeticsAndDefaultsToFistPump()
        {
            Assert.AreEqual(CelebrationKind.CallIt, Flair.CelebrationFor("cosmetic.celebration.call_it"));
            Assert.AreEqual(CelebrationKind.ShimmyStep, Flair.CelebrationFor("cosmetic.celebration.shimmy_step"));
            Assert.AreEqual(CelebrationKind.FistPump, Flair.CelebrationFor(null));
            Assert.AreEqual(CelebrationKind.FistPump, Flair.CelebrationFor("unknown"));
        }

        [Test]
        public void DribbleMoveFor_MapsCosmeticsAndDefaultsToCrossover()
        {
            Assert.AreEqual(DribbleMoveKind.HesiHop, Flair.DribbleMoveFor("cosmetic.move.hesi_hop"));
            Assert.AreEqual(DribbleMoveKind.SpinCycle, Flair.DribbleMoveFor("cosmetic.move.spin_cycle"));
            Assert.AreEqual(DribbleMoveKind.Crossover, Flair.DribbleMoveFor(null));
        }

        [Test]
        public void EveryCelebration_DoesSomethingThenEnds()
        {
            foreach (CelebrationKind k in Enum.GetValues(typeof(CelebrationKind)))
            {
                bool moved = false;
                for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.02f)
                    if (!Flair.Celebration(k, t).IsNone) moved = true;
                Assert.IsTrue(moved, k + " should animate");
                Assert.IsTrue(Flair.Celebration(k, Flair.CelebrationSeconds).IsNone, k + " should end");
                Assert.IsTrue(Flair.Celebration(k, -0.1f).IsNone);
            }
        }

        [Test]
        public void EveryDribbleMove_DoesSomethingThenEnds()
        {
            foreach (DribbleMoveKind k in Enum.GetValues(typeof(DribbleMoveKind)))
            {
                bool moved = false;
                for (float t = 0f; t < Flair.DribbleMoveSeconds; t += 0.01f)
                    if (!Flair.DribbleMove(k, t).IsNone) moved = true;
                Assert.IsTrue(moved, k + " should animate");
                Assert.IsTrue(Flair.DribbleMove(k, Flair.DribbleMoveSeconds).IsNone);
            }
        }

        [Test]
        public void Poses_StayWithinAFewArtPixels()
        {
            for (float t = 0f; t < 1f; t += 0.01f)
            {
                foreach (CelebrationKind k in Enum.GetValues(typeof(CelebrationKind)))
                {
                    var p = Flair.Celebration(k, t);
                    Assert.LessOrEqual(Math.Abs(p.Lift) + Math.Abs(p.OffsetX), 6);
                }
                foreach (DribbleMoveKind k in Enum.GetValues(typeof(DribbleMoveKind)))
                {
                    var p = Flair.DribbleMove(k, t);
                    Assert.LessOrEqual(Math.Abs(p.BallOffsetX) + Math.Abs(p.BallLift) + Math.Abs(p.Lift), 8);
                }
            }
        }

        [Test]
        public void SharpCut_OnlyForRealReversals()
        {
            Assert.IsTrue(Flair.IsSharpCut(new Vec2(1f, 0f), new Vec2(-1f, 0.1f)));
            Assert.IsFalse(Flair.IsSharpCut(new Vec2(1f, 0f), new Vec2(0.8f, 0.6f)), "gentle turn");
            Assert.IsFalse(Flair.IsSharpCut(new Vec2(0.1f, 0f), new Vec2(-1f, 0f)), "previous was stick noise");
            Assert.IsFalse(Flair.IsSharpCut(new Vec2(1f, 0f), Vec2.Zero), "stopping isn't a cut");
        }

        [Test]
        public void Bursts_AreDeterministicAndFanUpward()
        {
            var a = Bursts.Create(42, 16, 4f, 0.6f);
            var b = Bursts.Create(42, 16, 4f, 0.6f);
            Assert.AreEqual(16, a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].vx, b[i].vx);
                Assert.Greater(a[i].vy, 0f, "sparks start moving up");
                Assert.LessOrEqual(a[i].life, 0.6f);
            }
            Bursts.Offset(a[0], 0f, out float x0, out float y0);
            Assert.AreEqual(0f, x0);
            Assert.AreEqual(0f, y0);
        }

        [Test]
        public void CrowdAmbience_IsFourSecondsLoudEnoughAndLoopsWithoutAClick()
        {
            var s = AudioSynth.CrowdAmbience();
            Assert.AreEqual(4f, AudioSynth.Duration(s), 0.01f);
            float peak = 0f;
            foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
            Assert.AreEqual(0.5f, peak, 0.001f);
            // The jump across the loop point is no bigger than a typical sample-to-sample step.
            float seam = Math.Abs(s[0] - s[s.Length - 1]);
            float typical = 0f;
            for (int i = 1; i < 2000; i++) typical = Math.Max(typical, Math.Abs(s[i] - s[i - 1]));
            Assert.LessOrEqual(seam, typical * 1.5f);
        }

        [Test]
        public void ShotType_IsRecordedOnRelease()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 3;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            bool sawShot = false;
            for (int i = 0; i < 60 * 120 && !sawShot; i++)
            {
                m.Step(1f / 60f, default);
                foreach (var e in m.Events)
                    if (e.Type == MatchEventType.ShotReleased && m.Ball.Phase == BallPhase.Shot)
                    {
                        sawShot = true;
                        Assert.AreEqual(m.ChargeType, m.Ball.ShotType);
                    }
            }
            Assert.IsTrue(sawShot, "an AI shot should happen within two minutes");
        }
    }
}
