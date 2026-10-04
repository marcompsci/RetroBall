using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Arcade layer of the match: HEAT CHECK (a player who hits three in a row heats up until
    /// they miss or the other team scores) and alley-oops (a pass to a teammate near the rim
    /// becomes a lob they finish in the air).
    /// </summary>
    public sealed partial class MatchSimulation
    {
        private bool _alleyOop;

        /// <summary>True while a lob for an alley-oop is in the air.</summary>
        public bool AlleyOopInFlight => _alleyOop && Ball.Phase == BallPhase.Pass;

        /// <summary>HEAT CHECK: three straight makes (until a miss, a block, or the other team scores).</summary>
        public bool IsHeatedUp(int player) =>
            player >= 0 && player < Players.Length && Players[player].HotStreak >= Setup.Shot.heatThreshold;

        /// <summary>Movement multiplier for a heated-up player.</summary>
        private float HeatSpeedFactor(PlayerRuntimeState p) => p.HotStreak >= Setup.Shot.heatThreshold ? Setup.Shot.heatSpeedScale : 1f;

        /// <summary>Call after a made basket by <paramref name="shooter"/>.</summary>
        private void OnMadeForHeat(PlayerRuntimeState shooter)
        {
            if (shooter.HotStreak == Setup.Shot.heatThreshold) Stats[shooter.Index].heatUps++;
            if (shooter.HotStreak == Setup.Shot.heatThreshold)
                Events.Add(new MatchEvent(MatchEventType.HeatUp, shooter.Index, shooter.Team, shooter.HotStreak));
            // Scoring on a heated-up team cools them off.
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == shooter.Team) continue;
                if (o.HotStreak >= Setup.Shot.heatThreshold) CoolOff(o);
            }
        }

        /// <summary>Clears a player's streak (miss / blocked), announcing the cool-off if they were hot.</summary>
        private void CoolOff(PlayerRuntimeState p)
        {
            if (p.HotStreak >= Setup.Shot.heatThreshold)
                Events.Add(new MatchEvent(MatchEventType.HeatEnded, p.Index, p.Team));
            p.HotStreak = 0;
        }

        /// <summary>
        /// True if a pass to <paramref name="target"/> right now would be an alley-oop: a teammate
        /// of the ball holder close to the rim, able to finish above it, during live play.
        /// </summary>
        public bool IsAlleyOopTarget(int target)
        {
            if (Phase != MatchPhase.Live || !Ball.IsHeld || MustClear || Setup.TeammatesOnlyPass) return false;
            if (target < 0 || target >= Players.Length || target == Ball.HolderIndex) return false;
            var r = Players[target];
            if (r.Team != Players[Ball.HolderIndex].Team || Time < r.StunnedUntil) return false;
            if (r.Def.attributes.finishing < Setup.Pass.alleyOopMinFinishing) return false;
            float dist = Setup.Court.DistanceToHoop(r.Position);
            if (dist > Setup.Pass.alleyOopRange) return false;
            // The passer has to be away from the rim: a lob from under the basket makes no sense.
            return Setup.Court.DistanceToHoop(Players[Ball.HolderIndex].Position) >= Setup.Pass.alleyOopMinPassDistance;
        }

        /// <summary>Lob height for the alley-oop pass at <paramref name="progress"/> 0..1.</summary>
        private float LobHeight(float progress)
        {
            float start = Setup.Flow.passHeight;
            float end = Setup.Pass.alleyOopCatchHeight;
            return start + (end - start) * progress + 4f * Setup.Pass.alleyOopArc * progress * (1f - progress);
        }

        /// <summary>The receiver catches the lob in the air and throws it down.</summary>
        private void FinishAlleyOop(int finisher, int passer)
        {
            _alleyOop = false;
            var p = Players[finisher];
            GiveBall(finisher, announce: false, fromCheck: false);
            p.JumpStart = Time;
            var toHoop = (Setup.Court.Hoop - p.Position).Normalized;
            if (toHoop.SqrMagnitude > 0.01f) p.Motion.facing = Movement.FacingOf(toHoop);
            ChargeType = ShotType.Dunk;
            Stats[finisher].alleyOops++;
            if (passer >= 0) Stats[passer].alleyOopPasses++;
            Events.Add(new MatchEvent(MatchEventType.AlleyOop, finisher, p.Team, passer));
            ReleaseShot(finisher, Setup.Shot.greenCenter);
        }
    }
}
