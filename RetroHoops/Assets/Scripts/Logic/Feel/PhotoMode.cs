using System;
using CallerRetroBall.Logic.PixelArt;

namespace CallerRetroBall.Logic
{
    public enum PhotoFilter
    {
        None = 0,
        Arcade = 1,
        FourColor = 2,
        Dusk = 3,
        Neon = 4,
        Mono = 5,
    }

    /// <summary>
    /// Photo mode (pure, testable): filters and a frame for a paused moment. The game captures the
    /// screen at its art resolution (one pixel per art pixel), this styles it, and the result is
    /// scaled up crisp for the share sheet.
    /// </summary>
    public static class PhotoMode
    {
        public static readonly string[] FilterNames = { "NO FILTER", "ARCADE", "4-COLOR", "DUSK", "NEON", "MONO" };
        public static int FilterCount => FilterNames.Length;

        /// <summary>The 4-COLOR palette, darkest to lightest (Sunset Swish colours).</summary>
        public static readonly RgbColor[] FourColor =
        {
            new RgbColor(0x1B, 0x0B, 0x3A), new RgbColor(0x5B, 0x2A, 0x86), new RgbColor(0xFF, 0x4F, 0xA3), new RgbColor(0xFF, 0xE0, 0x66),
        };

        public static readonly RgbColor FrameOuter = AppIconGenerator.Outline;
        public static readonly RgbColor FrameInner = AppIconGenerator.Board;
        public static readonly RgbColor StripColor = AppIconGenerator.DuskTop;
        public const int Border = 3;
        public const int StripHeight = 11;

        public static PhotoFilter Next(PhotoFilter f, int step = 1)
        {
            int n = FilterCount;
            return (PhotoFilter)((((int)f + step) % n + n) % n);
        }

        /// <summary>Perceived brightness 0..1.</summary>
        public static float Luma(RgbColor c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

        private static byte B(float v) => (byte)(v < 0f ? 0 : v > 255f ? 255 : Math.Round(v));

        /// <summary>A filtered copy of <paramref name="src"/> (the source is left alone).</summary>
        public static PixelCanvas Filter(PixelCanvas src, PhotoFilter f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            var dst = new PixelCanvas(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var c = src.Pixels[y * src.Width + x].WithAlpha(255);
                    dst.Pixels[y * src.Width + x] = Pixel(c, f, x, y, src.Height);
                }
            return dst;
        }

        private static RgbColor Pixel(RgbColor c, PhotoFilter f, int x, int y, int height)
        {
            switch (f)
            {
                case PhotoFilter.Arcade:
                {
                    // Scanlines on every other row (counted from the top), slight bloom on the lit rows.
                    bool dark = (height - 1 - y) % 2 == 1;
                    return dark ? c.Darken(0.3f) : new RgbColor(B(c.r * 1.06f), B(c.g * 1.06f), B(c.b * 1.06f));
                }
                case PhotoFilter.FourColor:
                {
                    // Ordered dither between the four shades.
                    float l = Luma(c) * 3f;
                    int band = (int)l;
                    if (l - band > PixelCanvas.BayerThreshold(x, y)) band++;
                    return FourColor[Math.Max(0, Math.Min(3, band))];
                }
                case PhotoFilter.Dusk:
                    return new RgbColor(B(c.r * 1.08f + 12f), B(c.g * 0.92f + 4f), B(c.b * 0.85f + 18f));
                case PhotoFilter.Neon:
                {
                    // More saturation and contrast.
                    float l = Luma(c) * 255f;
                    float Sat(float v) => (l + (v - l) * 1.5f - 128f) * 1.15f + 128f;
                    return new RgbColor(B(Sat(c.r)), B(Sat(c.g)), B(Sat(c.b)));
                }
                case PhotoFilter.Mono:
                {
                    byte v = B(Luma(c) * 255f);
                    return new RgbColor(v, v, v);
                }
                default:
                    return c;
            }
        }

        /// <summary>
        /// A framed copy: a dark outline and light mat around the picture, and a strip along the bottom
        /// with RETRO HOOPS on the left and the caption (score, date) on the right.
        /// </summary>
        public static PixelCanvas Frame(PixelCanvas src, string caption)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int w = src.Width + Border * 2, h = src.Height + Border * 2 + StripHeight;
            var dst = new PixelCanvas(w, h);
            dst.Fill(FrameOuter);
            dst.FillRect(1, 1, w - 2, h - 2, FrameInner);
            // Picture sits above the strip.
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                    dst.Pixels[(y + Border + StripHeight) * w + x + Border] = src.Pixels[y * src.Width + x].WithAlpha(255);
            // Strip.
            dst.FillRect(Border, Border, w - Border * 2, StripHeight - 1, StripColor);
            int textTop = Border + StripHeight - 4;
            PixelFont.Draw(dst, "RETRO HOOPS", Border + 3, textTop, AppIconGenerator.SunTop);
            string cap = Clean(caption);
            int room = w - Border * 2 - 6 - PixelFont.Measure("RETRO HOOPS") - 8;
            while (cap.Length > 0 && PixelFont.Measure(cap) > room) cap = cap.Substring(0, cap.Length - 1).TrimEnd();
            if (cap.Length > 0) PixelFont.Draw(dst, cap, w - Border - 3 - PixelFont.Measure(cap), textTop, AppIconGenerator.Net);
            return dst;
        }

        /// <summary>Upper-case and only characters the pixel font can draw.</summary>
        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var chars = s.ToUpperInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] != ' ' && !PixelFont.Has(chars[i])) chars[i] = ' ';
            return new string(chars).Trim();
        }

        /// <summary>Filter, optional frame, then a crisp nearest-neighbour upscale for sharing.</summary>
        public static PixelCanvas Compose(PixelCanvas src, PhotoFilter filter, bool frame, string caption, int scale)
        {
            var c = Filter(src, filter);
            if (frame) c = Frame(c, caption);
            return scale > 1 ? AppIconGenerator.Scale(c, scale) : c;
        }

        /// <summary>Output scale so the photo comes out about <paramref name="targetWidth"/> pixels wide.</summary>
        public static int ScaleFor(int artWidth, int targetWidth = 1080) => Math.Max(1, Math.Min(8, (int)Math.Round(targetWidth / (double)Math.Max(1, artWidth))));

        /// <summary>File name for a saved photo.</summary>
        public static string FileName(DateTime now) => "RetroHoops_" + now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".png";
    }
}
