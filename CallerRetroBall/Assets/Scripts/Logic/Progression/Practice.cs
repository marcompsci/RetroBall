using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum DrillKind { FreeShoot = 0, PassingTargets = 1, DribbleLane = 2, ThreePoint = 3, Lockdown = 4, /** 3-point contest head to head with a CPU shooter. */ Shootout = 5,
        /** Make a shot from each of seven spots, corner to corner, in order. Timed. */ AroundTheWorld = 6,
        /** H-O-R-S-E against the CPU or a friend (run by <see cref="HorseSession"/>, not this class). */ Horse = 7 }

    /// <summary>
    /// Practice Lab drill scoring on top of a practice <see cref="MatchSimulation"/>
    /// (passive defenders, no shot clock). Reads the match's events each step.
    /// </summary>
    public sealed class PracticeSession
    {
        public const float FreeShootSeconds = 60f;
        public const float PassingSeconds = 45f;
        public const float ConeRadius = 0.8f;
        public const float HandBackDelay = 0.4f;
        public const float ThreePointSeconds = 60f;
        public const float MoneyBallRadius = 1.5f;
        public const int LockdownPossessions = 6;
        /// <summary>Around the World: release within this distance of the current spot.</summary>
        public const float WorldSpotRadius = 1.1f;
        /// <summary>Around the World: seconds after a shot before the ball is handed back where you stand.</summary>
        public const float WorldResetDelay = 0.6f;

        /// <summary>Around the World: the seven spots and the one you're on.</summary>
        public readonly List<Vec2> WorldSpots = new List<Vec2>();
        public int WorldSpot { get; private set; }
        private bool _releasedAtSpot;
        private float _resumeAt = -1f;
        /// <summary>A defensive possession that lasts this long without a score counts as a stop.</summary>
        public const float LockdownPossessionSeconds = 10f;

        /// <summary>3-Point Contest: points (arc make = 1, money-ball spot = 2).</summary>
        public int ContestPoints { get; private set; }
        /// <summary>3-Point Contest: the five spots around the arc; the money spot rotates after every shot.</summary>
        public readonly List<Vec2> MoneySpots = new List<Vec2>();
        public int MoneySpot { get; private set; }
        /// <summary>Lockdown: defensive possessions played and stops made.</summary>
        public int Possessions { get; private set; }
        public int Stops { get; private set; }
        private bool _releaseWasMoney;
        private float _possessionStart = -1f;

        public readonly DrillKind Kind;
        public int Makes { get; private set; }
        public int Attempts { get; private set; }
        public int Greens { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }
        public int PassScore { get; private set; }
        public int TargetPlayer { get; private set; } = -1;
        public readonly List<Vec2> Cones = new List<Vec2>();
        public int NextCone { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>Dribble lane completion time (seconds), 0 until finished.</summary>
        public float CourseTime { get; private set; }

        private readonly SeededRandom _rng;
        private float _teammateHeldFor;

        public PracticeSession(DrillKind kind, MatchSimulation m, uint seed = 1)
        {
            Kind = kind;
            _rng = new SeededRandom(seed);
            if (kind == DrillKind.DribbleLane) BuildCourse(m.Setup.Court);
            if (kind == DrillKind.ThreePoint || kind == DrillKind.Shootout) BuildMoneySpots(m.Setup.Court);
            if (kind == DrillKind.PassingTargets) PickTarget(m);
            if (kind == DrillKind.AroundTheWorld) BuildWorld(m.Setup.Court);
        }

        public float TimeLimit => Kind == DrillKind.FreeShoot ? FreeShootSeconds
                                : Kind == DrillKind.PassingTargets ? PassingSeconds
                                : ThreePointStyle ? ThreePointSeconds : 0f;

        /// <summary>Money-ball spots and arc scoring (3-Point Contest and Shootout).</summary>
        public bool ThreePointStyle => Kind == DrillKind.ThreePoint || Kind == DrillKind.Shootout;

        /// <summary>Shootout: the CPU's score to beat and who set it.</summary>
        public int CpuScore { get; private set; }
        public string CpuName { get; private set; }

        public void SetCpu(int score, string name)
        {
            CpuScore = score;
            CpuName = name;
        }

        public bool ShootoutWon => Kind == DrillKind.Shootout && Finished && ContestPoints > CpuScore;

        private void BuildMoneySpots(CourtGeometry c)
        {
            // Corners, wings, top of the key, just beyond the arc.
            float r = c.arcRadius + 0.7f;
            foreach (float deg in new[] { 5f, 45f, 90f, 135f, 175f })
            {
                double a = deg * Math.PI / 180.0;
                var p = new Vec2((float)Math.Cos(a) * r, c.hoopY + (float)Math.Sin(a) * r);
                MoneySpots.Add(c.Clamp(p));
            }
            MoneySpot = 0;
        }
        public float TimeLeft => TimeLimit <= 0f ? 0f : Math.Max(0f, TimeLimit - Elapsed);

        private void BuildWorld(CourtGeometry c)
        {
            // Seven mid-range spots from the right corner, round the top, to the left corner.
            float r = Math.Min(4.4f, c.arcRadius - 1.2f);
            for (int i = 0; i < 7; i++)
            {
                double a = (8f + i * (164f / 6f)) * Math.PI / 180.0;
                WorldSpots.Add(c.Clamp(new Vec2((float)Math.Cos(a) * r, c.hoopY + (float)Math.Sin(a) * r)));
            }
            WorldSpot = 0;
        }

        private void BuildCourse(CourtGeometry c)
        {
            // Zig-zag from the top of the arc down to the rim.
            Cones.Add(new Vec2(-3.2f, c.hoopY + 6.6f));
            Cones.Add(new Vec2(3.2f, c.hoopY + 5.4f));
            Cones.Add(new Vec2(-3.4f, c.hoopY + 3.8f));
            Cones.Add(new Vec2(3.0f, c.hoopY + 2.4f));
            Cones.Add(new Vec2(0f, c.hoopY + 0.9f));
        }

        private void PickTarget(MatchSimulation m)
        {
            int previous = TargetPlayer;
            var options = new List<int>();
            for (int slot = 1; slot < MatchSimulation.PlayersPerTeam; slot++)
            {
                int i = MatchSimulation.IndexOf(m.Setup.HumanTeam, slot);
                if (i != previous) options.Add(i);
            }
            TargetPlayer = options[_rng.Range(0, options.Count)];
        }

        /// <summary>Call after every simulation step.</summary>
        public void Update(MatchSimulation m, float dt)
        {
            if (Finished) return;
            if (m.Phase == MatchPhase.Live) Elapsed += dt;

            if (Kind == DrillKind.Lockdown)
            {
                UpdateLockdown(m);
                return;
            }

            if (Kind == DrillKind.AroundTheWorld)
            {
                UpdateWorld(m);
                return;
            }

            foreach (var e in m.Events)
            {
                if (ThreePointStyle && e.PlayerIndex == m.ControlledIndex)
                {
                    if (e.Type == MatchEventType.ShotReleased)
                    {
                        _releaseWasMoney = MoneySpots.Count > 0 && Vec2.Distance(m.Controlled.Position, MoneySpots[MoneySpot]) <= MoneyBallRadius;
                        MoneySpot = (MoneySpot + 1) % Math.Max(1, MoneySpots.Count);
                    }
                    else if (e.Type == MatchEventType.ShotMade && e.Value >= m.Setup.Rules.beyondArcPoints)
                    {
                        ContestPoints += _releaseWasMoney ? 2 : 1;
                    }
                }
                switch (e.Type)
                {
                    case MatchEventType.ShotReleased when e.PlayerIndex == m.ControlledIndex:
                        Attempts++;
                        if (e.Value == (int)ShotFeedback.Green) Greens++;
                        break;
                    case MatchEventType.ShotMade when e.PlayerIndex == m.ControlledIndex:
                        Makes++;
                        Streak++;
                        BestStreak = Math.Max(BestStreak, Streak);
                        break;
                    case MatchEventType.ShotMissed when e.PlayerIndex == m.ControlledIndex:
                        Streak = 0;
                        break;
                    case MatchEventType.PassCaught when Kind == DrillKind.PassingTargets:
                        if (e.PlayerIndex == TargetPlayer)
                        {
                            PassScore++;
                            PickTarget(m);
                        }
                        break;
                }
            }

            // Teammates hand the ball back so reps keep flowing.
            if (m.HumanTeamHasBall && !m.HumanHasBall)
            {
                _teammateHeldFor += dt;
                if (_teammateHeldFor >= HandBackDelay && m.PassToHuman()) _teammateHeldFor = 0f;
            }
            else
            {
                _teammateHeldFor = 0f;
            }

            if (Kind == DrillKind.DribbleLane && NextCone < Cones.Count && m.HumanHasBall
                && Vec2.Distance(m.Controlled.Position, Cones[NextCone]) <= ConeRadius)
            {
                NextCone++;
                if (NextCone == Cones.Count)
                {
                    CourseTime = Elapsed;
                    Finished = true;
                }
            }

            if (TimeLimit > 0f && Elapsed >= TimeLimit) Finished = true;
        }

        /// <summary>Around the World: make from the current spot to move on; finish all seven as fast as you can.</summary>
        private void UpdateWorld(MatchSimulation m)
        {
            int me = m.ControlledIndex;
            foreach (var e in m.Events)
            {
                if (e.PlayerIndex != me) continue;
                if (e.Type == MatchEventType.ShotReleased)
                {
                    Attempts++;
                    _releasedAtSpot = WorldSpot < WorldSpots.Count && Vec2.Distance(m.Controlled.Position, WorldSpots[WorldSpot]) <= WorldSpotRadius;
                }
                else if (e.Type == MatchEventType.ShotMade || e.Type == MatchEventType.ShotMissed)
                {
                    if (e.Type == MatchEventType.ShotMade)
                    {
                        Makes++;
                        if (_releasedAtSpot) WorldSpot++;
                    }
                    _releasedAtSpot = false;
                    if (WorldSpot >= WorldSpots.Count)
                    {
                        CourseTime = Elapsed;
                        Finished = true;
                        return;
                    }
                    _resumeAt = m.Time + WorldResetDelay;
                }
            }
            // Hand the ball straight back where you stand (no walking back to the top).
            if (_resumeAt >= 0f && m.Time >= _resumeAt)
            {
                _resumeAt = -1f;
                m.ResumeWithBall(me);
            }
        }

        /// <summary>
        /// Lockdown: the other side attacks; each possession ends in a stop (steal, defensive rebound,
        /// block recovery, or 10 seconds without a score) or a score. Six possessions.
        /// </summary>
        private void UpdateLockdown(MatchSimulation m)
        {
            int me = m.Setup.HumanTeam;
            if (_possessionStart < 0f)
            {
                if (m.OffenseTeam != me) _possessionStart = m.Time;
                else { m.RestartWithBall(1 - me); return; }
            }
            bool ended = false, stop = false;
            foreach (var e in m.Events)
            {
                if (e.Type == MatchEventType.ShotMade && e.Team != me) { ended = true; stop = false; }
                else if (e.Type == MatchEventType.PossessionChanged && e.Team == me) { ended = true; stop = true; }
                else if (e.Type == MatchEventType.ShotClockViolation) { ended = true; stop = true; }
            }
            if (!ended && m.Phase == MatchPhase.Live && m.Time - _possessionStart >= LockdownPossessionSeconds) { ended = true; stop = true; }
            if (!ended) return;
            Possessions++;
            if (stop) Stops++;
            if (Possessions >= LockdownPossessions)
            {
                Finished = true;
                return;
            }
            m.RestartWithBall(1 - me);
            _possessionStart = m.Time;
        }

        public string ResultText()
        {
            switch (Kind)
            {
                case DrillKind.FreeShoot:
                    return Makes + " / " + Attempts + " made  ·  " + Greens + " green  ·  best streak " + BestStreak;
                case DrillKind.PassingTargets:
                    return PassScore + " targets hit";
                case DrillKind.ThreePoint:
                    return ContestPoints + " points  ·  " + Makes + " / " + Attempts + " made";
                case DrillKind.Lockdown:
                    return Stops + " / " + LockdownPossessions + " stops";
                case DrillKind.AroundTheWorld:
                    return Finished ? "World tour: " + CourseTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s"
                                    : "Spot " + Math.Min(WorldSpot + 1, WorldSpots.Count) + " / " + WorldSpots.Count;
                case DrillKind.Shootout:
                    return "You " + ContestPoints + "  ·  " + (CpuName ?? "CPU") + " " + CpuScore;
                default:
                    return Finished ? "Course: " + CourseTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s" : "Cone " + NextCone + " / " + Cones.Count;
            }
        }
    }
}
