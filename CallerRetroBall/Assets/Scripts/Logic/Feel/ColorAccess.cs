using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Settings ▸ COLOR FILTER: which colour-vision deficiency the game's colour cues are tuned for.</summary>
    public enum ColorFilter
    {
        Off = 0,
        /// <summary>Protanopia / deuteranopia (red-green, the most common).</summary>
        RedGreen = 1,
        /// <summary>Tritanopia (blue-yellow).</summary>
        BlueYellow = 2,
    }

    /// <summary>Shot-meter feedback colours: inside the window, just outside it, and a clear miss.</summary>
    public struct MeterPalette
    {
        public RgbColor Good;
        public RgbColor Near;
        public RgbColor Bad;
    }

    /// <summary>
    /// Colour accessibility, engine-free and testable. Simulates colour-vision deficiencies with the
    /// Machado, Oliveira and Fernandes (2009) matrices (severity 1.0, linear RGB) and measures how far
    /// apart two colours look (CIE76 ΔE in Lab). Used to pick shot-meter colours that stay distinct and
    /// to switch the away team to its alternate kit when the two jerseys would look alike.
    /// </summary>
    public static class ColorAccess
    {
        /// <summary>Below this ΔE two jerseys count as "too similar" and the away team changes kit.</summary>
        public const float KitClashDeltaE = 22f;

        private static readonly float[] Protan =
        {
            0.152286f, 1.052583f, -0.204868f,
            0.114503f, 0.786281f, 0.099216f,
            -0.003882f, -0.048116f, 1.051998f,
        };
        private static readonly float[] Deutan =
        {
            0.367322f, 0.860646f, -0.227968f,
            0.280085f, 0.672501f, 0.047413f,
            -0.011820f, 0.042940f, 0.968881f,
        };
        private static readonly float[] Tritan =
        {
            1.255528f, -0.076749f, -0.178779f,
            -0.078411f, 0.930809f, 0.147602f,
            0.004733f, 0.691367f, 0.303900f,
        };

        /// <summary>The meter colours used with no filter (the game's normal look).</summary>
        public static readonly MeterPalette Standard = new MeterPalette
        {
            Good = RgbColor.FromHex("#3DDC84"), Near = RgbColor.FromHex("#FFB03B"), Bad = RgbColor.FromHex("#F72585"),
        };

        public static MeterPalette Meter(ColorFilter f)
        {
            switch (f)
            {
                case ColorFilter.RedGreen:
                    // Sky blue / yellow / violet: apart in both hue and lightness for protan and deutan eyes.
                    return new MeterPalette { Good = RgbColor.FromHex("#56B4E9"), Near = RgbColor.FromHex("#FFD400"), Bad = RgbColor.FromHex("#7C3AED") };
                case ColorFilter.BlueYellow:
                    return new MeterPalette { Good = RgbColor.FromHex("#3DDC84"), Near = RgbColor.FromHex("#E69F00"), Bad = RgbColor.FromHex("#FF2E2E") };
                default:
                    return Standard;
            }
        }

        /// <summary>How <paramref name="c"/> looks to someone with the given colour vision (one simulation).</summary>
        public static RgbColor Simulate(RgbColor c, float[] matrix)
        {
            if (matrix == null) return c;
            float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
            float R = matrix[0] * r + matrix[1] * g + matrix[2] * b;
            float G = matrix[3] * r + matrix[4] * g + matrix[5] * b;
            float B = matrix[6] * r + matrix[7] * g + matrix[8] * b;
            return new RgbColor(Enc(R), Enc(G), Enc(B), c.a);
        }

        public static RgbColor SimulateProtan(RgbColor c) => Simulate(c, Protan);
        public static RgbColor SimulateDeutan(RgbColor c) => Simulate(c, Deutan);
        public static RgbColor SimulateTritan(RgbColor c) => Simulate(c, Tritan);

        /// <summary>CIE76 ΔE between two colours as seen with normal vision.</summary>
        public static float DeltaE(RgbColor a, RgbColor b)
        {
            Lab(a, out float l1, out float a1, out float b1);
            Lab(b, out float l2, out float a2, out float b2);
            float dl = l1 - l2, da = a1 - a2, db = b1 - b2;
            return (float)Math.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>
        /// The smallest ΔE between <paramref name="a"/> and <paramref name="b"/> for normal vision and for
        /// the vision the filter targets (red-green checks both protan and deutan).
        /// </summary>
        public static float PerceivedDistance(RgbColor a, RgbColor b, ColorFilter f)
        {
            float d = DeltaE(a, b);
            switch (f)
            {
                case ColorFilter.RedGreen:
                    d = Math.Min(d, DeltaE(SimulateProtan(a), SimulateProtan(b)));
                    d = Math.Min(d, DeltaE(SimulateDeutan(a), SimulateDeutan(b)));
                    break;
                case ColorFilter.BlueYellow:
                    d = Math.Min(d, DeltaE(SimulateTritan(a), SimulateTritan(b)));
                    break;
            }
            return d;
        }

        /// <summary>True when the two jersey colours are too close to tell apart (for this filter).</summary>
        public static bool KitsClash(RgbColor home, RgbColor away, ColorFilter f) => PerceivedDistance(home, away, f) < KitClashDeltaE;

        /// <summary>
        /// The away team's colours for this game: its alternate kit (trim as the jersey) when its
        /// main jersey clashes with the home jersey and the alternate is easier to tell apart.
        /// </summary>
        public static bool ResolveAwayKit(RgbColor homeJersey, ref RgbColor awayJersey, ref RgbColor awayTrim, ColorFilter f)
        {
            if (!KitsClash(homeJersey, awayJersey, f)) return false;
            if (PerceivedDistance(homeJersey, awayTrim, f) <= PerceivedDistance(homeJersey, awayJersey, f)) return false;
            var t = awayJersey;
            awayJersey = awayTrim;
            awayTrim = t;
            return true;
        }

        public static ColorFilter Normalize(int value) =>
            value == (int)ColorFilter.RedGreen ? ColorFilter.RedGreen : value == (int)ColorFilter.BlueYellow ? ColorFilter.BlueYellow : ColorFilter.Off;

        public static readonly string[] FilterNames = { "OFF", "RED-GREEN", "BLUE-YELLOW" };

        // ------------------------------------------------------------------ colour maths

        private static float Lin(byte v)
        {
            float c = v / 255f;
            return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
        }

        private static byte Enc(float v)
        {
            v = Math.Max(0f, Math.Min(1f, v));
            float c = v <= 0.0031308f ? v * 12.92f : 1.055f * (float)Math.Pow(v, 1.0 / 2.4) - 0.055f;
            return (byte)Math.Round(Math.Max(0f, Math.Min(1f, c)) * 255f);
        }

        private static void Lab(RgbColor c, out float l, out float a, out float b)
        {
            float r = Lin(c.r), g = Lin(c.g), bl = Lin(c.b);
            float x = (0.4124f * r + 0.3576f * g + 0.1805f * bl) / 0.95047f;
            float y = 0.2126f * r + 0.7152f * g + 0.0722f * bl;
            float z = (0.0193f * r + 0.1192f * g + 0.9505f * bl) / 1.08883f;
            float fx = F(x), fy = F(y), fz = F(z);
            l = 116f * fy - 16f;
            a = 500f * (fx - fy);
            b = 200f * (fy - fz);
        }

        private static float F(float t) => t > 0.008856f ? (float)Math.Pow(t, 1.0 / 3.0) : 7.787f * t + 16f / 116f;
    }
}
