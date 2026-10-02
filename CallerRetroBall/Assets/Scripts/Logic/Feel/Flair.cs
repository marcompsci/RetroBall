using System;

namespace CallerRetroBall.Logic
{
    public enum CelebrationKind { FistPump = 0, CallIt = 1, ShimmyStep = 2 }

    public enum DribbleMoveKind { Crossover = 0, HesiHop = 1, SpinCycle = 2 }

    /// <summary>
    /// Presentation-only offsets for one player sprite at one moment, in art pixels.
    /// The views add these on top of what the simulation says; gameplay never reads them.
    /// </summary>
    public struct FlairPose
    {
        /// <summary>Sprite lift in art pixels (up).</summary>
        public int Lift;
        /// <summary>Sideways sprite offset in art pixels.</summary>
        public int OffsetX;
        /// <summary>Flip the sprite horizontally relative to its normal facing.</summary>
        public bool FlipOverride;
        /// <summary>Use the arms-up frame.</summary>
        public bool ArmsUp;
        /// <summary>Held-ball offset in art pixels (dribble moves move the ball between hands).</summary>
        public int BallOffsetX;
        public int BallLift;

        public static FlairPose None => default;
        public bool IsNone => Lift == 0 && OffsetX == 0 && !FlipOverride && !ArmsUp && BallOffsetX == 0 && BallLift == 0;
    }

    /// <summary>
    /// Celebrations (after you score) and dribble moves (sharp cuts with the ball), chosen by the
    /// equipped cosmetics. Pure functions of time, so they are deterministic and unit-tested.
    /// </summary>
    public static class Flair
    {
        public const float CelebrationSeconds = 0.9f;
        public const float DribbleMoveSeconds = 0.36f;
        public const float DribbleMoveCooldown = 0.7f;
        /// <summary>A direction change sharper than this (degrees) triggers a dribble move.</summary>
        public const float DribbleMoveAngle = 110f;

        public static CelebrationKind CelebrationFor(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.celebration.call_it": return CelebrationKind.CallIt;
                case "cosmetic.celebration.shimmy_step": return CelebrationKind.ShimmyStep;
                default: return CelebrationKind.FistPump;
            }
        }

        public static DribbleMoveKind DribbleMoveFor(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.move.hesi_hop": return DribbleMoveKind.HesiHop;
                case "cosmetic.move.spin_cycle": return DribbleMoveKind.SpinCycle;
                default: return DribbleMoveKind.Crossover;
            }
        }

        /// <summary>Pose <paramref name="t"/> seconds into a celebration (None once it's over).</summary>
        public static FlairPose Celebration(CelebrationKind kind, float t)
        {
            if (t < 0f || t >= CelebrationSeconds) return FlairPose.None;
            var p = new FlairPose();
            switch (kind)
            {
                case CelebrationKind.FistPump:
                    // Hop, then three quick arm pumps.
                    p.Lift = Hop(t, 0f, 0.3f, 3);
                    p.ArmsUp = t < 0.3f || ((int)((t - 0.3f) / 0.1f) % 2 == 0);
                    break;
                case CelebrationKind.CallIt:
                    // Arms up the whole time, two hops: "called it".
                    p.ArmsUp = true;
                    p.Lift = Hop(t, 0f, 0.35f, 3) + Hop(t, 0.45f, 0.35f, 2);
                    break;
                default: // ShimmyStep
                    int step = (int)(t / 0.12f);
                    p.OffsetX = step % 2 == 0 ? -1 : 1;
                    p.FlipOverride = step % 2 == 1;
                    p.Lift = step % 4 == 3 ? 1 : 0;
                    break;
            }
            return p;
        }

        /// <summary>Pose <paramref name="t"/> seconds into a dribble move (None once it's over).</summary>
        public static FlairPose DribbleMove(DribbleMoveKind kind, float t)
        {
            if (t < 0f || t >= DribbleMoveSeconds) return FlairPose.None;
            float u = t / DribbleMoveSeconds; // 0..1
            var p = new FlairPose();
            switch (kind)
            {
                case DribbleMoveKind.Crossover:
                    // Ball swings low from one hand to the other.
                    p.BallOffsetX = (int)Math.Round(-3f + 6f * u);
                    p.BallLift = -(int)Math.Round(Math.Sin(u * Math.PI) * 2f);
                    break;
                case DribbleMoveKind.HesiHop:
                    // Freeze-and-hop: the player pops up while the ball is held high.
                    p.Lift = Hop(t, 0.1f, 0.22f, 2);
                    p.BallLift = u < 0.6f ? 3 : 0;
                    break;
                default: // SpinCycle
                    // Two facing flips in quick succession reads as a spin at pixel scale.
                    p.FlipOverride = u < 0.25f || (u >= 0.5f && u < 0.75f);
                    p.BallOffsetX = u < 0.5f ? 2 : -2;
                    break;
            }
            return p;
        }

        /// <summary>
        /// True when the move direction turned sharply (from <paramref name="previous"/> to
        /// <paramref name="current"/>), both being real moves rather than stick noise.
        /// </summary>
        public static bool IsSharpCut(Vec2 previous, Vec2 current)
        {
            if (previous.SqrMagnitude < 0.25f || current.SqrMagnitude < 0.25f) return false;
            float dot = (previous.x * current.x + previous.y * current.y) / (previous.Magnitude * current.Magnitude);
            dot = Math.Max(-1f, Math.Min(1f, dot));
            double angle = Math.Acos(dot) * 180.0 / Math.PI;
            return angle >= DribbleMoveAngle;
        }

        /// <summary>Parabolic hop of <paramref name="height"/> art pixels between start and start+duration.</summary>
        private static int Hop(float t, float start, float duration, int height)
        {
            float u = (t - start) / duration;
            if (u <= 0f || u >= 1f) return 0;
            return (int)Math.Round(4f * u * (1f - u) * height);
        }
    }

    /// <summary>One particle of a pixel burst (score sparks, block dust).</summary>
    public struct BurstParticle
    {
        /// <summary>Initial velocity in world units per second.</summary>
        public float vx, vy;
        public float life;
    }

    /// <summary>Deterministic pixel bursts; the Unity layer moves and fades them.</summary>
    public static class Bursts
    {
        public const float Gravity = -9f;

        public static BurstParticle[] Create(uint seed, int count, float speed, float life)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            var parts = new BurstParticle[count];
            for (int i = 0; i < count; i++)
            {
                // Mostly upward fan, like sparks off the rim.
                double angle = (20.0 + rng.NextFloat() * 140.0) * Math.PI / 180.0;
                float s = speed * (0.5f + 0.5f * rng.NextFloat());
                parts[i] = new BurstParticle
                {
                    vx = (float)(Math.Cos(angle) * s),
                    vy = (float)(Math.Sin(angle) * s),
                    life = life * (0.7f + 0.3f * rng.NextFloat()),
                };
            }
            return parts;
        }

        /// <summary>Offset from the burst origin after <paramref name="t"/> seconds.</summary>
        public static void Offset(BurstParticle p, float t, out float x, out float y)
        {
            x = p.vx * t;
            y = p.vy * t + 0.5f * Gravity * t * t;
        }
    }
}
