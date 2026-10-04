using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Tunable movement feel. Lives in data so balance never hides in scripts.</summary>
    [Serializable]
    public class MovementTuning
    {
        /// <summary>Top speed (m/s) of a rating-1 player.</summary>
        public float baseSpeed = 4.2f;
        /// <summary>Extra top speed at rating 99.</summary>
        public float speedFromRating = 2.6f;
        /// <summary>Multiplier while dribbling.</summary>
        public float dribbleMultiplier = 0.88f;
        /// <summary>Acceleration toward the desired velocity (m/s²).</summary>
        public float acceleration = 30f;
        /// <summary>Braking when the stick is released or reversed (m/s²).</summary>
        public float deceleration = 42f;
        /// <summary>Player collision radius (m).</summary>
        public float bodyRadius = 0.35f;

        public static MovementTuning Default => new MovementTuning();
    }

    /// <summary>Eight-way facing used to pick sprite frames.</summary>
    public enum Facing8 { N = 0, NE = 1, E = 2, SE = 3, S = 4, SW = 5, W = 6, NW = 7 }

    public struct MotionState
    {
        public Vec2 position;
        public Vec2 velocity;
        public Facing8 facing;

        public MotionState(Vec2 position, Facing8 facing = Facing8.N)
        {
            this.position = position;
            velocity = Vec2.Zero;
            this.facing = facing;
        }

        public bool IsMoving => velocity.SqrMagnitude > 0.04f;
    }

    /// <summary>Pure, deterministic player locomotion.</summary>
    public static class Movement
    {
        public static float MaxSpeed(int speedRating, bool dribbling, MovementTuning t, float difficultyScale = 1f)
        {
            float s = t.baseSpeed + t.speedFromRating * RatingScale.Normalized(speedRating);
            if (dribbling) s *= t.dribbleMultiplier;
            // Difficulty may slow the AI but never speed it past its ratings.
            return s * Math.Min(1f, Math.Max(0f, difficultyScale));
        }

        /// <summary>
        /// Advances one step. <paramref name="desired"/> is a direction with magnitude 0..1
        /// (analog stick); magnitudes above 1 are clamped.
        /// </summary>
        public static MotionState Step(MotionState s, Vec2 desired, float maxSpeed, float dt, MovementTuning t)
        {
            if (dt <= 0f) return s;
            var target = Vec2.ClampMagnitude(desired, 1f) * maxSpeed;
            var delta = target - s.velocity;
            // Brake harder when releasing or reversing so controls feel responsive.
            bool braking = target.SqrMagnitude < s.velocity.SqrMagnitude || Vec2.Dot(target, s.velocity) < 0f;
            float rate = braking ? t.deceleration : t.acceleration;
            s.velocity = Vec2.MoveTowards(s.velocity, target, rate * dt);
            if (delta.SqrMagnitude < 1e-8f) s.velocity = target;
            s.position = s.position + s.velocity * dt;
            if (s.IsMoving) s.facing = FacingOf(s.velocity);
            return s;
        }

        /// <summary>Returns the eight-way facing of a direction (+y is "north", toward half-court).</summary>
        public static Facing8 FacingOf(Vec2 dir)
        {
            if (dir.SqrMagnitude < 1e-8f) return Facing8.N;
            double angle = Math.Atan2(dir.x, dir.y) * 180.0 / Math.PI; // 0 = +y, clockwise positive
            if (angle < 0) angle += 360.0;
            int sector = (int)Math.Floor((angle + 22.5) / 45.0) % 8;
            return (Facing8)sector;
        }

        /// <summary>Steering helper: a stick-like input that arrives at <paramref name="target"/> without overshooting.</summary>
        public static Vec2 ArriveInput(Vec2 from, Vec2 target, float slowRadius = 1.2f, float stopRadius = 0.15f)
        {
            var d = target - from;
            float dist = d.Magnitude;
            if (dist <= stopRadius) return Vec2.Zero;
            float strength = dist >= slowRadius ? 1f : dist / slowRadius;
            return d / dist * strength;
        }

        /// <summary>
        /// Pushes overlapping bodies apart (equal shares) and keeps everyone on the court.
        /// A few relaxation passes keep crowds stable without physics jitter.
        /// </summary>
        public static void Separate(Vec2[] positions, float radius, CourtGeometry court, int iterations = 3)
        {
            float minDist = radius * 2f;
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    for (int j = i + 1; j < positions.Length; j++)
                    {
                        var d = positions[j] - positions[i];
                        float dist = d.Magnitude;
                        if (dist >= minDist) continue;
                        // Coincident bodies: separate along a deterministic axis.
                        var n = dist > 1e-5f ? d / dist : (i % 2 == 0 ? Vec2.Right : -Vec2.Right);
                        float push = (minDist - dist) * 0.5f;
                        positions[i] = positions[i] - n * push;
                        positions[j] = positions[j] + n * push;
                    }
                }
                for (int i = 0; i < positions.Length; i++) positions[i] = court.Clamp(positions[i]);
            }
        }
    }
}
