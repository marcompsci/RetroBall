using System;
using System.Globalization;

namespace CallerRetroBall.Logic
{
    /// <summary>Engine-free 8-bit RGBA colour. Converted to UnityEngine.Color32 at the boundary.</summary>
    [Serializable]
    public struct RgbColor : IEquatable<RgbColor>
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public RgbColor(byte r, byte g, byte b, byte a = 255)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static readonly RgbColor Clear = new RgbColor(0, 0, 0, 0);
        public static readonly RgbColor White = new RgbColor(255, 255, 255);
        public static readonly RgbColor Black = new RgbColor(0, 0, 0);

        /// <summary>Parses "#RRGGBB" or "#RRGGBBAA" (the # is optional).</summary>
        public static RgbColor FromHex(string hex)
        {
            if (hex == null) throw new ArgumentNullException(nameof(hex));
            string h = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
            if (h.Length != 6 && h.Length != 8)
                throw new FormatException("Expected #RRGGBB or #RRGGBBAA but got '" + hex + "'.");

            byte P(int i) => byte.Parse(h.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new RgbColor(P(0), P(2), P(4), h.Length == 8 ? P(6) : (byte)255);
        }

        public string ToHex(bool includeAlpha = false)
        {
            return includeAlpha
                ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", r, g, b, a)
                : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        public RgbColor WithAlpha(byte alpha) => new RgbColor(r, g, b, alpha);

        /// <summary>Linear interpolation in sRGB space (fine for pixel-art gradients).</summary>
        public static RgbColor Lerp(RgbColor from, RgbColor to, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            byte L(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
            return new RgbColor(L(from.r, to.r), L(from.g, to.g), L(from.b, to.b), L(from.a, to.a));
        }

        public RgbColor Darken(float amount) => Lerp(this, new RgbColor(0, 0, 0, a), amount);
        public RgbColor Lighten(float amount) => Lerp(this, new RgbColor(255, 255, 255, a), amount);

        /// <summary>Relative luminance (WCAG definition), 0..1.</summary>
        public double Luminance
        {
            get
            {
                double C(byte c)
                {
                    double s = c / 255.0;
                    return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
                }
                return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
            }
        }

        /// <summary>WCAG contrast ratio between two colours, 1..21.</summary>
        public static double ContrastRatio(RgbColor x, RgbColor y)
        {
            double l1 = x.Luminance, l2 = y.Luminance;
            if (l2 > l1) { var tmp = l1; l1 = l2; l2 = tmp; }
            return (l1 + 0.05) / (l2 + 0.05);
        }

        /// <summary>Plain Euclidean RGB distance, 0..~441. Used for "too similar" warnings.</summary>
        public static double Distance(RgbColor x, RgbColor y)
        {
            int dr = x.r - y.r, dg = x.g - y.g, db = x.b - y.b;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        public bool Equals(RgbColor other) => r == other.r && g == other.g && b == other.b && a == other.a;
        public override bool Equals(object obj) => obj is RgbColor other && Equals(other);
        public override int GetHashCode() => (r << 24) | (g << 16) | (b << 8) | a;
        public static bool operator ==(RgbColor x, RgbColor y) => x.Equals(y);
        public static bool operator !=(RgbColor x, RgbColor y) => !x.Equals(y);
        public override string ToString() => ToHex(true);
    }
}
