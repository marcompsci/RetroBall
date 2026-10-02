using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    public class DefenseTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>A live match where the AI team (1) has the ball and the human defends.</summary>
        private static MatchSimulation HumanOnDefense(uint seed = 3)
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            req.Seed = seed;
            var setup = MatchSetup.FromRequest(req, c);
            setup.StartingOffense = 1;
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 60; i++) m.Step(Dt, PlayerInput.None);
            return m;
        }

        private static List<MatchEvent> Run(MatchSimulation m, int frames, PlayerInput input = default)
        {
            var log = new List<MatchEvent>();
            for (int i = 0; i < frames; i++)
            {
                m.Step(Dt, input);
                log.AddRange(m.Events);
                input.DefensePressed = false;
                input.ShootPressed = false;
                input.PassPressed = false;
            }
            return log;
        }

        [Test]
        public void Steal_OutOfRange_Whiffs_AndCostsRecovery()
        {
            var m = HumanOnDefense();
            var me = m.Controlled;
            me.Motion.position = m.Holder.Position + new Vec2(0f, 4f);
            Assert.IsFalse(m.TrySteal(me.Index));
            Assert.Greater(me.StunnedUntil, m.Time);
            Assert.AreEqual(1, m.OffenseTeam);
        }

        [Test]
        public void Steal_HasACooldown()
        {
            var m = HumanOnDefense();
            m.TrySteal(m.ControlledIndex);
            Assert.IsFalse(m.TrySteal(m.ControlledIndex), "Second reach inside the cooldown must be refused");
        }

        [Test]
        public void RepeatedCloseSteals_EventuallySucceed_AndForceAClear()
        {
            bool stole = false;
            for (uint seed = 1; seed < 40 && !stole; seed++)
            {
                var m = HumanOnDefense(seed);
                var me = m.Controlled;
                for (int tries = 0; tries < 6 && !stole && m.Ball.IsHeld; tries++)
                {
                    me.Motion.position = m.Holder.Position + new Vec2(0.2f, -0.3f);
                    me.StunnedUntil = -1f;
                    me.StealReadyAt = 0f;
                    stole = m.TrySteal(me.Index);
                }
                if (!stole) continue;
                Assert.AreEqual(0, m.OffenseTeam);
                Assert.AreEqual(me.Index, m.HolderIndex);
                Assert.AreEqual(1, m.Stats[me.Index].steals);
                Assert.IsTrue(m.MustClear || m.Setup.Court.ZoneOf(me.Position) == ShotZone.BeyondArc);
            }
            Assert.IsTrue(stole, "Point-blank steals never succeeded across 40 seeds");
        }

        [Test]
        public void Jump_ContestsHarderThanStandingStill()
        {
            var m = HumanOnDefense();
            var shooter = m.Holder;
            var me = m.Controlled;
            me.Motion.position = shooter.Position + new Vec2(0f, -1.2f);
            foreach (var p in m.Players) if (p.Team == 0 && p != me) p.Motion.position = new Vec2(-6f, 9f);
            var standing = ShotModel.Evaluate(m.ShotContextFor(shooter.Index, ShotType.Arc, 0.85f), m.Setup.Shot);
            Assert.IsTrue(m.Jump(me.Index));
            var jumping = ShotModel.Evaluate(m.ShotContextFor(shooter.Index, ShotType.Arc, 0.85f), m.Setup.Shot);
            Assert.Greater(jumping.ContestPenalty, standing.ContestPenalty);
            Assert.IsFalse(m.Jump(me.Index), "Can't jump again mid-air");
        }

        [Test]
        public void JumpHeight_RisesAndLands()
        {
            var m = HumanOnDefense();
            m.Jump(m.ControlledIndex);
            float peak = 0f;
            for (int i = 0; i < 40; i++)
            {
                m.Step(Dt, PlayerInput.None);
                peak = System.Math.Max(peak, m.JumpHeight01(m.ControlledIndex));
            }
            Assert.Greater(peak, 0.9f);
            Assert.AreEqual(0f, m.JumpHeight01(m.ControlledIndex), 1e-5);
        }

        [Test]
        public void Blocks_HappenOnlyWhenAirborneAndClose()
        {
            int blocks = 0, seedsWithShots = 0;
            for (uint seed = 1; seed <= 80; seed++)
            {
                var m = HumanOnDefense(seed);
                var me = m.Controlled;
                // Wait for an AI shooter to rise up.
                for (int i = 0; i < 60 * 20 && !(m.ChargingIndex >= 0 && m.Players[m.ChargingIndex].Team == 1); i++)
                    m.Step(Dt, PlayerInput.None);
                if (m.ChargingIndex < 0) continue;
                seedsWithShots++;
                var shooter = m.Players[m.ChargingIndex];
                var log = new List<MatchEvent>();
                for (int i = 0; i < 90 && m.ChargingIndex == shooter.Index; i++)
                {
                    me.Motion.position = shooter.Position + new Vec2(0f, -0.6f);
                    if (!m.IsJumping(me.Index)) m.Jump(me.Index);
                    m.Step(Dt, PlayerInput.None);
                    log.AddRange(m.Events);
                }
                if (log.Exists(e => e.Type == MatchEventType.Block && e.PlayerIndex == me.Index)) blocks++;
            }
            Assert.Greater(seedsWithShots, 20);
            Assert.Greater(blocks, 0, "Point-blank airborne contests never produced a block");
            Assert.Less(blocks, seedsWithShots, "Blocks should not be automatic");
        }

        [Test]
        public void NoBlock_WithoutJumping()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                var m = HumanOnDefense(seed);
                for (int i = 0; i < 60 * 20; i++)
                {
                    // Only the human defends in the shooter's face, never jumping; AI teammates stay away.
                    foreach (var p in m.Players) if (p.Team == 0 && p.Index != m.ControlledIndex) p.Motion.position = new Vec2(p.Slot * 3f - 4f, 10f);
                    if (m.ChargingIndex >= 0) m.Controlled.Motion.position = m.Players[m.ChargingIndex].Position + new Vec2(0f, -0.6f);
                    m.Step(Dt, PlayerInput.None);
                    Assert.IsFalse(m.Events.Exists(e => e.Type == MatchEventType.Block), "Blocked without anyone jumping");
                }
            }
        }

        [Test]
        public void Switch_SwapsAssignmentsWithTheTeammateOnTheBall()
        {
            var m = HumanOnDefense();
            // Give the ball to the AI team's slot 1 so the human isn't already on it.
            var target = m.Players[MatchSimulation.IndexOf(1, 1)];
            m.KnockLoose(Vec2.Zero, 0f);
            m.Ball.Position = target.Position;
            m.Ball.Height = 0.2f;
            foreach (var p in m.Players) if (p != target) p.Motion.position = p.Position + new Vec2(0f, p.Team == 0 ? 3f : 0f);
            Run(m, 5);
            if (m.HolderIndex != target.Index) Assert.Inconclusive("Setup did not hand the ball to slot 1");
            int before = m.GuardingOf(m.ControlledIndex);
            Assert.IsTrue(m.SwitchOntoBall(m.ControlledIndex));
            Assert.AreEqual(target.Index, m.GuardingOf(m.ControlledIndex));
            Assert.AreNotEqual(before, m.GuardingOf(m.ControlledIndex));
        }

        [Test]
        public void BoxOut_IsDetectedBetweenManAndRim()
        {
            var m = HumanOnDefense();
            var hoop = m.Setup.Court.Hoop;
            var me = m.Controlled;
            var man = m.Players[m.GuardingOf(me.Index)];
            me.Motion.position = hoop + new Vec2(0f, 2.0f);
            man.Motion.position = hoop + new Vec2(0f, 2.8f);
            m.Ball.Phase = BallPhase.Shot;
            Assert.IsTrue(m.IsBoxingOut(me.Index));
            Assert.IsFalse(m.IsBoxingOut(man.Index));
        }

        [Test]
        public void Stamina_DrainsWhenSprinting_AndRecovers()
        {
            var c = DefaultContent.Create();
            var m = new MatchSimulation(MatchSetup.FromRequest(MatchRequest.PracticeDefault(), c));
            var me = m.Controlled;
            for (int i = 0; i < 60 * 8; i++)
                m.Step(Dt, new PlayerInput { Move = new Vec2(i / 60 % 2 == 0 ? 1f : -1f, 0f) });
            float tired = me.Stamina;
            Assert.Less(tired, 1f);
            for (int i = 0; i < 60 * 8; i++) m.Step(Dt, PlayerInput.None);
            Assert.Greater(me.Stamina, tired);
        }
    }

    public class PlayCallTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation LiveOffense()
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            req.Seed = 9;
            var m = new MatchSimulation(MatchSetup.FromRequest(req, c));
            m.Step(Dt, new PlayerInput { Move = new Vec2(0.01f, 0.3f) }); // starts live play
            return m;
        }

        [Test]
        public void CallPlay_OnlyWorksOnOffense()
        {
            var m = LiveOffense();
            Assert.IsTrue(m.CallPlay(PlayCall.ClearOut));
            Assert.AreEqual(PlayCall.ClearOut, m.ActivePlay);

            var c = DefaultContent.Create();
            var setup = MatchSetup.FromRequest(MatchRequest.QuickCallDefault(c), c);
            setup.StartingOffense = 1;
            var d = new MatchSimulation(setup);
            for (int i = 0; i < 60; i++) d.Step(Dt, PlayerInput.None);
            Assert.IsFalse(d.CallPlay(PlayCall.PickAndRoll));
        }

        [Test]
        public void ClearOut_SendsTeammatesToTheCorners()
        {
            var m = LiveOffense();
            m.CallPlay(PlayCall.ClearOut);
            for (int i = 0; i < 150; i++) m.Step(Dt, PlayerInput.None);
            for (int slot = 1; slot < 3; slot++)
            {
                var p = m.Players[MatchSimulation.IndexOf(0, slot)];
                Assert.Less(p.Position.y, 3f, "slot " + slot + " should be in the corner");
                Assert.Greater(System.Math.Abs(p.Position.x), 4.5f);
            }
        }

        [Test]
        public void PickAndRoll_SetsAScreen_ThatSlowsTheDefender()
        {
            var m = LiveOffense();
            Assert.IsTrue(m.CallPlay(PlayCall.PickAndRoll));
            Assert.GreaterOrEqual(m.Screener, 0);
            var log = new List<MatchEvent>();
            // Wait for the screen to be set (handler holds still), then use it.
            for (int i = 0; i < 240 && m.ActivePlay == PlayCall.PickAndRoll; i++)
            {
                var toward = (m.Players[m.Screener].Position - m.Controlled.Position).Normalized;
                m.Step(Dt, new PlayerInput { Move = i < 90 ? Vec2.Zero : toward * 0.8f });
                log.AddRange(m.Events);
            }
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.Screen), "No defender ran into the screen");
        }

        [Test]
        public void GiveAndGo_ReturnsTheBallOnTheCut()
        {
            var m = LiveOffense();
            m.Setup.PassiveOpponents = true;
            m.CallPlay(PlayCall.GiveAndGo);
            m.Step(Dt, new PlayerInput { PassPressed = true, Move = new Vec2(-1f, 0f) });
            for (int i = 0; i < 90 && !m.Ball.IsHeld; i++) m.Step(Dt, PlayerInput.None);
            Assert.AreNotEqual(m.ControlledIndex, m.HolderIndex);
            // Cut hard to the rim.
            bool back = false;
            for (int i = 0; i < 240 && !back; i++)
            {
                var toRim = (m.Setup.Court.Hoop - m.Controlled.Position).Normalized;
                m.Step(Dt, new PlayerInput { Move = toRim });
                back = m.HumanHasBall;
            }
            Assert.IsTrue(back, "Teammate never returned the pass on the cut");
        }
    }

    public class Phase4SoakTests
    {
        [TestCase(11u)]
        [TestCase(12u)]
        public void FullGames_StillFinish_WithDefenseAndPlays(uint seed)
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            req.Seed = seed;
            var m = new MatchSimulation(MatchSetup.FromRequest(req, c));
            int blocks = 0, steals = 0;
            for (int i = 0; i < 60 * 600 && !m.IsOver; i++)
            {
                m.Step(1f / 60f, PlayerInput.None);
                foreach (var e in m.Events)
                {
                    if (e.Type == MatchEventType.Block) blocks++;
                    if (e.Type == MatchEventType.Steal) steals++;
                }
            }
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(m.Score[0], m.Stats.TeamPoints(0, 3));
            Assert.AreEqual(m.Score[1], m.Stats.TeamPoints(1, 3));
            int statBlocks = 0, statSteals = 0;
            foreach (var s in m.Stats.players) { statBlocks += s.blocks; statSteals += s.steals; }
            Assert.AreEqual(blocks, statBlocks);
            Assert.GreaterOrEqual(statSteals, steals);
        }
    }
}
