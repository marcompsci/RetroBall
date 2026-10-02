using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Shootout: a 60-second 3-point contest against a CPU shooter. The CPU's round is simulated
    /// with the real shot model before yours starts, so you know the score to beat. The CPU walks
    /// to the gold money spot every time (2 per make) and needs a little longer per shot.
    /// </summary>
    public static class Shootout
    {
        public const float SecondsPerCpuShot = 3.1f;

        /// <summary>The CPU shooter: the best shooter on a league team.</summary>
        public static PlayerDef PickShooter(ContentCatalog c, string teamId)
        {
            var team = c.Team(teamId);
            PlayerDef best = null;
            if (team == null) return null;
            foreach (var id in team.rosterPlayerIds)
            {
                var p = c.Player(id);
                if (p != null && (best == null || p.attributes.shooting > best.attributes.shooting)) best = p;
            }
            return best;
        }

        /// <summary>Simulates the CPU's 60 seconds. Release timing comes from the difficulty's accuracy.</summary>
        public static int SimulateCpu(PlayerDef shooter, DifficultyDef difficulty, ShotTuning t, uint seed, float arcDistance)
        {
            if (shooter == null) return 0;
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            int shots = (int)(PracticeSession.ThreePointSeconds / SecondsPerCpuShot);
            float accuracy = difficulty != null ? difficulty.releaseAccuracy : 0.55f;
            int score = 0;
            for (int i = 0; i < shots; i++)
            {
                // Accurate shooters land near the green centre; misses spread the release.
                float spread = (1f - accuracy) * 0.3f;
                float meter = t.greenCenter + (rng.NextFloat() * 2f - 1f) * spread;
                var ctx = new ShotContext
                {
                    Type = ShotType.Arc, Distance = arcDistance, Meter = meter, Shooter = shooter.attributes,
                    NearestDefenderDistance = float.MaxValue, NearestDefenderDefense = 50, Stamina01 = 1f,
                };
                var e = ShotModel.Evaluate(ctx, t);
                if (rng.NextFloat() < e.MakeChance) score += 2;
            }
            return score;
        }
    }
}
