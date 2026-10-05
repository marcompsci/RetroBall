using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// DUNK and LAYUP buttons, and baseline alley-oops.
    /// DUNK / LAYUP with the ball: close enough, you go straight up; otherwise you attack the rim at full
    /// speed and finish when you get there. The release is automatic (timed by your Finishing rating),
    /// and the normal contest and block rules apply. DUNK becomes a layup when you can't dunk it.
    /// Off the ball, athletic AI teammates run the baseline while you have it; PASS then throws them the lob.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        /// <summary>Furthest from the rim you can take off for a dunk with the DUNK button (m).</summary>
        public const float DunkReach = 2.2f;
        /// <summary>Furthest from the rim a LAYUP goes up (m).</summary>
        public const float LayupReach = 2.5f;
        /// <summary>A drive gives up after this long without getting there (s).</summary>
        public const float DriveSeconds = 2.6f;
        /// <summary>A baseline runner is a lob target this far from the rim (m).</summary>
        public const float BaselineOopRange = 4.6f;

        private int _driveIndex = -1;
        private bool _driveDunk;
        private float _driveUntil;
        private bool _autoRelease;
        private bool _oopToRunner;

        /// <summary>The player driving to the rim from a DUNK / LAYUP press (-1 = none).</summary>
        public int DrivingIndex => _driveIndex;
        /// <summary>The drive wants a dunk (else a layup).</summary>
        public bool DriveWantsDunk => _driveDunk;

        /// <summary>Can this player dunk from here (rating and distance)?</summary>
        public bool CanDunkFrom(int player)
        {
            var p = Players[player];
            return p.Def.attributes.finishing >= Setup.Shot.dunkMinFinishing && Setup.Court.DistanceToHoop(p.Position) <= DunkReach;
        }

        /// <summary>DUNK / LAYUP pressed with the ball.</summary>
        private void StartFinish(int me, bool dunk)
        {
            float dist = Setup.Court.DistanceToHoop(Players[me].Position);
            if (dist <= (dunk ? DunkReach : LayupReach) || (dunk && dist <= LayupReach && !CanDunkRating(me)))
            {
                FinishNow(me, dunk);
                return;
            }
            _driveIndex = me;
            _euroStepped = false;
            _driveDunk = dunk;
            _driveUntil = Time + DriveSeconds;
            Events.Add(new MatchEvent(MatchEventType.DriveStarted, me, Players[me].Team, dunk ? (int)ShotType.Dunk : (int)ShotType.Layup));
        }

        private bool CanDunkRating(int me) => Players[me].Def.attributes.finishing >= Setup.Shot.dunkMinFinishing;

        private void CancelDrive() => _driveIndex = -1;

        /// <summary>Ends or completes the drive each step.</summary>
        private void UpdateDrive()
        {
            if (_driveIndex < 0) return;
            int me = _driveIndex;
            if (Phase != MatchPhase.Live || !Ball.IsHeld || Ball.HolderIndex != me || ChargingIndex >= 0 || Time > _driveUntil)
            {
                CancelDrive();
                return;
            }
            float dist = Setup.Court.DistanceToHoop(Players[me].Position);
            float reach = _driveDunk && CanDunkRating(me) ? DunkReach : LayupReach;
            if (dist <= reach) FinishNow(me, _driveDunk);
        }

        /// <summary>Playmaking needed to euro-step around a defender in the lane.</summary>
        public const int EuroStepMinPlaymaking = 55;
        private bool _euroStepped;

        /// <summary>
        /// Full speed at the rim, aiming a step in front of it (not under the backboard). A good handler
        /// side-steps (euro step) a defender standing in the lane instead of running into them.
        /// </summary>
        private Vec2 DriveDesired(PlayerRuntimeState p)
        {
            var hoop = Setup.Court.Hoop;
            var away = p.Position - hoop;
            var front = away.SqrMagnitude > 0.01f ? away.Normalized : new Vec2(0f, 1f);
            if (front.y < 0.3f) front = new Vec2(front.x, 0.3f).Normalized; // never go behind the board
            var aim = hoop + front * 0.9f;
            var dir = Movement.ArriveInput(p.Position, aim, 1f, 0.05f);
            if (p.Def.attributes.playmaking < EuroStepMinPlaymaking || dir.SqrMagnitude < 0.01f) return dir;
            var heading = dir.Normalized;
            int blocker = -1;
            float lateral = 0f, best = 1.7f;
            for (int i = 0; i < Players.Length; i++)
            {
                var d = Players[i];
                if (d.Team == p.Team || IsBenched(i) || Time < d.StunnedUntil) continue;
                var rel = d.Position - p.Position;
                float ahead = Vec2.Dot(rel, heading);
                if (ahead <= 0.2f || ahead > best) continue;
                float side = rel.x * heading.y - rel.y * heading.x; // + = defender to the right of the path
                if (Math.Abs(side) > 0.9f) continue;
                best = ahead;
                blocker = i;
                lateral = side;
            }
            if (blocker < 0) return dir;
            if (!_euroStepped)
            {
                _euroStepped = true;
                Events.Add(new MatchEvent(MatchEventType.EuroStep, p.Index, p.Team, blocker));
            }
            // Step to the side away from the defender (perpendicular to the path), still moving forward.
            var perp = new Vec2(-heading.y, heading.x); // left of the path
            float sign = lateral >= 0f ? 1f : -1f;      // defender right → go left
            return (heading * 0.6f + perp * (0.9f * sign)).Normalized;
        }

        // ------------------------------------------------------------------ AI against drives and lobs

        /// <summary>
        /// The defender nearest the rim (not the one guarding the driver) steps in front of a DUNK / LAYUP
        /// drive and goes up with the finish. Smarter difficulties rotate more often.
        /// </summary>
        private bool ProtectRim(PlayerRuntimeState p, AiState s, DifficultyDef profile)
        {
            if (_driveIndex < 0 && !(ChargingIndex >= 0 && _autoRelease)) return false;
            int driverIndex = _driveIndex >= 0 ? _driveIndex : ChargingIndex;
            var driver = Players[driverIndex];
            if (driver.Team == p.Team || _guarding[p.Index] == driverIndex || IsBenched(p.Index)) return false;
            var court = Setup.Court;
            float mine = court.DistanceToHoop(p.Position);
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team != p.Team || i == p.Index || IsBenched(i) || _guarding[i] == driverIndex) continue;
                if (court.DistanceToHoop(o.Position) < mine) return false; // someone else is the last line
            }
            if (_rng.NextFloat() > (0.35f + 0.55f * profile.decisionQuality) * (1f - profile.errorRate)) return false;
            s.Intent = AiIntent.Help;
            var fromHoop = driver.Position - court.Hoop;
            s.Target = court.Clamp(court.Hoop + (fromHoop.SqrMagnitude > 0.01f ? fromHoop.Normalized : new Vec2(0f, 1f)) * 1.1f);
            if (ChargingIndex == driverIndex && Vec2.Distance(p.Position, driver.Position) < 1.7f) Jump(p.Index);
            return true;
        }

        /// <summary>A defender whose man runs the baseline stays between him and the rim (fewer easy lobs).</summary>
        private bool DenyBaselineRunner(PlayerRuntimeState p, AiState s, PlayerRuntimeState man, DifficultyDef profile)
        {
            if (!IsRunningBaseline(man.Index) || _rng.NextFloat() > 0.25f + 0.5f * profile.decisionQuality) return false;
            var court = Setup.Court;
            var toHoop = court.Hoop - man.Position;
            s.Intent = AiIntent.Guard;
            s.Target = court.Clamp(man.Position + (toHoop.SqrMagnitude > 0.01f ? toHoop.Normalized : Vec2.Zero) * 0.7f);
            return true;
        }

        /// <summary>Goes up now: a dunk if wanted and possible, otherwise a layup. Released automatically.</summary>
        private void FinishNow(int me, bool wantDunk)
        {
            CancelDrive();
            var p = Players[me];
            bool dunk = wantDunk && CanDunkFrom(me);
            if (wantDunk && !dunk) Events.Add(new MatchEvent(MatchEventType.DunkToLayup, me, p.Team));
            var toHoop = (Setup.Court.Hoop - p.Position).Normalized;
            ChargeType = dunk ? ShotType.Dunk : ShotType.Layup;
            ChargingIndex = me;
            ChargeTime = 0f;
            _aiReleaseMeter = FinishReleaseMeter(p.Def.attributes.finishing, _rng.NextFloat());
            _autoRelease = true;
            p.Motion.velocity = p.Motion.velocity * 0.5f;
            if (toHoop.SqrMagnitude > 0.01f) p.Motion.facing = Movement.FacingOf(toHoop);
            if (dunk) p.JumpStart = Time;
            Events.Add(new MatchEvent(MatchEventType.ShotStarted, me, p.Team, (int)ChargeType));
        }

        /// <summary>
        /// Release point for a button finish: centred on green, spread wider for poor finishers.
        /// <paramref name="roll"/> is 0..1.
        /// </summary>
        public static float FinishReleaseMeter(int finishing, float roll, ShotTuning t = null)
        {
            t = t ?? ShotTuning.Default;
            float spread = 0.03f + 0.12f * Math.Max(0f, 1f - finishing / 100f);
            return Math.Max(0.2f, Math.Min(0.99f, t.greenCenter + (roll * 2f - 1f) * spread));
        }

        // ------------------------------------------------------------------ baseline alley-oops

        /// <summary>An AI teammate cutting along the baseline toward the rim.</summary>
        public bool IsRunningBaseline(int index)
        {
            if (index < 0 || index >= Players.Length || IsHumanControlled(index)) return false;
            var s = _ai[index];
            var p = Players[index];
            return s.Intent == AiIntent.BaselineRun && Time < s.IntentUntil
                   && p.Position.y <= Setup.Court.hoopY + 1.4f && p.Motion.velocity.Magnitude > 1.2f;
        }

        /// <summary>The teammate a PASS would lob an alley-oop to right now (baseline runners first), or -1.</summary>
        public int AlleyOopCandidate(int passer)
        {
            if (passer < 0 || !Ball.IsHeld || Ball.HolderIndex != passer) return -1;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Players.Length; i++)
            {
                if (!IsAlleyOopTarget(i)) continue;
                float score = Setup.Court.DistanceToHoop(Players[i].Position) - (IsRunningBaseline(i) ? 10f : 0f);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Off-ball AI with the human handling: an athletic teammate sometimes sprints to the corner and
        /// runs the baseline under the rim (two legs: corner, then across). Returns true if it took over.
        /// </summary>
        private bool BaselineRunIntent(PlayerRuntimeState p, AiState s)
        {
            var court = Setup.Court;
            if (s.Intent == AiIntent.BaselineRun && Time < s.IntentUntil)
            {
                // Leg two: from the corner, run the baseline across the front of the rim.
                if (Vec2.Distance(p.Position, s.Target) < 0.9f && s.Target.y < court.hoopY)
                {
                    float side = s.Target.x >= 0f ? 1f : -1f;
                    s.Target = court.Clamp(new Vec2(-side * 1.2f, court.hoopY + 0.5f));
                }
                return true;
            }
            if (s.Intent == AiIntent.BaselineRun) s.Intent = AiIntent.Space;
            bool humanHandling = Ball.IsHeld && IsHumanControlled(Ball.HolderIndex) && Players[Ball.HolderIndex].Team == p.Team;
            if (!humanHandling || p.Def.attributes.finishing < Setup.Pass.alleyOopMinFinishing) return false;
            if (Setup.Court.DistanceToHoop(Players[Ball.HolderIndex].Position) < Setup.Pass.alleyOopMinPassDistance) return false;
            for (int i = 0; i < Players.Length; i++)
                if (i != p.Index && Players[i].Team == p.Team && _ai[i].Intent == AiIntent.BaselineRun && Time < _ai[i].IntentUntil) return false;
            if (_rng.NextFloat() >= 0.12f + 0.25f * p.Tendencies.cut) return false;
            float corner = p.Position.x >= 0f ? 1f : -1f;
            s.Intent = AiIntent.BaselineRun;
            s.Target = court.Clamp(new Vec2(corner * (court.HalfWidth - 1.6f), 0.7f));
            s.IntentUntil = Time + 3.4f;
            return true;
        }
    }
}
