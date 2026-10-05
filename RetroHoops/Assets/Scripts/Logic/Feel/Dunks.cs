using System;

namespace CallerRetroBall.Logic
{
    public enum DunkStyle { TwoHand = 0, Tomahawk = 1, Windmill = 2, Reverse = 3, ThreeSixty = 4, Cradle = 5, Skyline = 6 }

    /// <summary>
    /// Dunk packages: how a dunk looks (pose, hang time, callout). Presentation only; the simulation
    /// decides make/miss. Your equipped package (Locker Room ▸ DUNK PACKAGE) is used for your dunks;
    /// the AI picks by finishing rating (big finishers throw down the fancy ones).
    /// </summary>
    public static class Dunks
    {
        public static readonly string[] Names = { "TWO-HAND JAM", "TOMAHAWK!", "WINDMILL!", "REVERSE JAM!", "THREE-SIXTY!", "CRADLE ROCK!", "SKYLINE SLAM!" };

        public static string Name(DunkStyle s) => Names[(int)s];

        public static DunkStyle ForCosmetic(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.dunk.tomahawk": return DunkStyle.Tomahawk;
                case "cosmetic.dunk.windmill": return DunkStyle.Windmill;
                case "cosmetic.dunk.reverse": return DunkStyle.Reverse;
                case "cosmetic.dunk.three_sixty": return DunkStyle.ThreeSixty;
                case "cosmetic.dunk.cradle": return DunkStyle.Cradle;
                case "cosmetic.pass.dunk.skyline": return DunkStyle.Skyline;
                default: return DunkStyle.TwoHand;
            }
        }

        /// <summary>AI dunk choice from finishing and a 0..1 roll: everyone can jam, only flyers go 360.</summary>
        public static DunkStyle ForAi(int finishing, float roll)
        {
            int options = finishing >= 88 ? 6 : finishing >= 80 ? 5 : finishing >= 72 ? 3 : 2;
            int i = Math.Min(options - 1, (int)(Math.Max(0f, Math.Min(0.999f, roll)) * options));
            return (DunkStyle)i;
        }

        /// <summary>How much higher than a plain dunk the sprite rises (1 = the two-hand jam).</summary>
        public static float LiftScale(DunkStyle s)
        {
            switch (s)
            {
                case DunkStyle.Skyline: return 1.5f;
                case DunkStyle.ThreeSixty: return 1.35f;
                case DunkStyle.Windmill: return 1.25f;
                case DunkStyle.Tomahawk: return 1.2f;
                case DunkStyle.Cradle: return 1.15f;
                default: return 1f;
            }
        }

        /// <summary>Camera shake when it goes down.</summary>
        public static float Shake(DunkStyle s) => s == DunkStyle.TwoHand ? 0.12f : 0.2f;

        /// <summary>Pose at <paramref name="u"/> (0..1 through the leap).</summary>
        public static FlairPose Pose(DunkStyle s, float u)
        {
            var p = new FlairPose();
            if (u < 0f || u >= 1f) return p;
            switch (s)
            {
                case DunkStyle.Tomahawk:
                    // Ball cocked back behind the head, then hammered down.
                    p.ArmsUp = true;
                    p.BallLift = u < 0.6f ? 4 : -1;
                    p.BallOffsetX = u < 0.6f ? -2 : 1;
                    break;
                case DunkStyle.Windmill:
                    // The ball circles: down, back, over the top.
                    double a = u * 2.0 * Math.PI;
                    p.BallOffsetX = (int)Math.Round(Math.Cos(a) * 3.0);
                    p.BallLift = (int)Math.Round(Math.Sin(a) * 3.0) + 1;
                    p.ArmsUp = u > 0.5f;
                    break;
                case DunkStyle.Reverse:
                    // Back to the rim, flushed over the head.
                    p.FlipOverride = true;
                    p.ArmsUp = u > 0.35f;
                    break;
                case DunkStyle.ThreeSixty:
                    // Four facing flips read as a full spin at pixel scale.
                    p.FlipOverride = ((int)(u * 4f)) % 2 == 1;
                    p.ArmsUp = u > 0.7f;
                    break;
                case DunkStyle.Skyline:
                    // Hang time: rises arms wide, a double windmill at the top, hammered home.
                    double w = u * 4.0 * Math.PI;
                    p.BallOffsetX = (int)Math.Round(Math.Cos(w) * 3.0);
                    p.BallLift = (int)Math.Round(Math.Sin(w) * 3.0) + 2;
                    p.ArmsUp = true;
                    p.FlipOverride = u > 0.45f && u < 0.6f;
                    break;
                case DunkStyle.Cradle:
                    // Ball rocked to the hip and back up.
                    p.BallOffsetX = (int)Math.Round(Math.Sin(u * Math.PI) * 3.0);
                    p.BallLift = u < 0.5f ? -1 : 3;
                    p.ArmsUp = u > 0.55f;
                    break;
                default:
                    p.ArmsUp = true;
                    p.BallLift = 2;
                    break;
            }
            return p;
        }
    }
}
