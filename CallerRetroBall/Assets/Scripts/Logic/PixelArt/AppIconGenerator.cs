using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Original app icon and launch image, drawn in code like every other piece of art.
    /// The icon is a 64x64 pixel-art basketball on a dithered sunset with gold "signal" arcs
    /// (the caller's call), scaled up 16x to the 1024x1024 App Store size. It is fully opaque:
    /// App Store icons must not contain transparency.
    /// </summary>
    public static class AppIconGenerator
    {
        public const int IconPixels = 64;
        public const int IconScale = 16; // 64 * 16 = 1024
        public const int StoreIconSize = IconPixels * IconScale;

        public static readonly RgbColor SkyTop = new RgbColor(0xF7, 0x25, 0x85);
        public static readonly RgbColor SkyMid = new RgbColor(0x72, 0x09, 0xB7);
        public static readonly RgbColor SkyLow = new RgbColor(0x1A, 0x1A, 0x2E);
        public static readonly RgbColor Gold = new RgbColor(0xFF, 0xD1, 0x66);
        public static readonly RgbColor BallMain = new RgbColor(0xFF, 0x8C, 0x42);
        public static readonly RgbColor BallShade = new RgbColor(0xD9, 0x5F, 0x2B);
        public static readonly RgbColor BallLight = new RgbColor(0xFF, 0xB8, 0x7A);
        public static readonly RgbColor Seam = new RgbColor(0x3A, 0x1F, 0x14);
        public static readonly RgbColor Outline = new RgbColor(0x14, 0x14, 0x20);

        /// <summary>The 64x64 icon art (row 0 is the bottom row).</summary>
        public static PixelCanvas Icon()
        {
            const int n = IconPixels;
            var c = new PixelCanvas(n, n);

            // Background: three-stop sunset, ordered-dithered into 8 bands.
            const int bands = 8;
            for (int y = 0; y < n; y++)
            {
                float t = y / (float)(n - 1); // 0 bottom .. 1 top
                float scaled = t * (bands - 1);
                int band = (int)scaled;
                float frac = scaled - band;
                for (int x = 0; x < n; x++)
                {
                    int b = frac > PixelCanvas.BayerThreshold(x, y) ? band + 1 : band;
                    c.Set(x, y, Sky(b / (float)(bands - 1)));
                }
            }

            // Court floor: a dark band with a highlight line, so the ball sits on something.
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < n; x++)
                    c.Set(x, y, y == 8 ? Gold.Darken(0.25f) : SkyLow.Lighten(0.06f));

            // Signal arcs coming off the ball's upper-right edge (the caller's call): three gold arcs.
            const float ox = 44f, oy = 43f;
            for (int ring = 0; ring < 3; ring++)
            {
                float r = 6f + ring * 5.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - ox, dy = y + 0.5f - oy;
                        if (dx <= 0f || dy <= 0f) continue;
                        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                        if (angle < 12.0 || angle > 78.0) continue;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (Math.Abs(d - r) <= 1.15f) c.Set(x, y, Gold);
                    }
            }

            // Ground shadow, then the ball.
            for (int y = 7; y <= 10; y++)
                for (int x = 14; x < 50; x++)
                {
                    float ex = (x + 0.5f - 32f) / 18f, ey = (y + 0.5f - 8.8f) / 2.2f;
                    if (ex * ex + ey * ey <= 1f) c.Set(x, y, Outline);
                }
            DrawBall(c, 30f, 29f, 19f);
            return c;
        }

        /// <summary>1024x1024 App Store icon (nearest-neighbour scale of <see cref="Icon"/>).</summary>
        public static PixelCanvas StoreIcon() => Scale(Icon(), IconScale);

        /// <summary>
        /// Portrait launch image: the main menu's backdrop with the ball emblem in the middle,
        /// so launch hands off smoothly to the first screen. No text (Apple's guidance).
        /// </summary>
        public static PixelCanvas LaunchImage(CourtDef court, uint seed)
        {
            if (court == null) throw new ArgumentNullException(nameof(court));
            var c = BackdropGenerator.Generate(court, seed);
            // Fill any transparent pixels so the image is fully opaque.
            for (int i = 0; i < c.Pixels.Length; i++)
                if (c.Pixels[i].a < 255) c.Pixels[i] = RgbColor.Lerp(SkyLow, c.Pixels[i].WithAlpha(255), c.Pixels[i].a / 255f);
            DrawBall(c, c.Width * 0.5f, c.Height * 0.56f, c.Width * 0.16f);
            return c;
        }

        /// <summary>Pixel-art basketball with lighting, seams and a 1px outline.</summary>
        public static void DrawBall(PixelCanvas c, float cx, float cy, float r)
        {
            int x0 = (int)Math.Floor(cx - r - 1), x1 = (int)Math.Ceiling(cx + r + 1);
            int y0 = (int)Math.Floor(cy - r - 1), y1 = (int)Math.Ceiling(cy + r + 1);
            float seamWidth = Math.Max(0.6f, r * 0.06f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > r + 1f) continue;
                    if (d > r) { c.Set(x, y, Outline); continue; }

                    // Light from the upper-left: shade bands with a little dithering.
                    float light = (-dx + dy) / (r * 1.6f) + 0.15f;
                    var col = BallMain;
                    if (light > 0.45f || (light > 0.35f && PixelCanvas.BayerThreshold(x, y) < 0.5f)) col = BallLight;
                    else if (light < -0.35f || (light < -0.2f && PixelCanvas.BayerThreshold(x, y) < 0.5f)) col = BallShade;

                    // Seams: vertical and horizontal centre lines plus two side curves.
                    float u = dx / r, v = dy / r;
                    bool seam = Math.Abs(dx) <= seamWidth || Math.Abs(dy) <= seamWidth;
                    float curve = (float)Math.Abs(Math.Abs(u) - (0.62f + 0.38f * v * v));
                    if (curve * r <= seamWidth * 1.1f && Math.Abs(u) > 0.25f) seam = true;
                    c.Set(x, y, seam ? Seam : col);
                }
        }

        /// <summary>Nearest-neighbour upscale (keeps pixels crisp).</summary>
        public static PixelCanvas Scale(PixelCanvas src, int factor)
        {
            if (factor < 1) throw new ArgumentOutOfRangeException(nameof(factor));
            var dst = new PixelCanvas(src.Width * factor, src.Height * factor);
            for (int y = 0; y < dst.Height; y++)
            {
                int sy = y / factor;
                for (int x = 0; x < dst.Width; x++) dst.Pixels[y * dst.Width + x] = src.Pixels[sy * src.Width + x / factor];
            }
            return dst;
        }

        private static RgbColor Sky(float t)
        {
            // t: 0 bottom .. 1 top
            return t < 0.5f ? RgbColor.Lerp(SkyLow, SkyMid, t / 0.5f) : RgbColor.Lerp(SkyMid, SkyTop, (t - 0.5f) / 0.5f);
        }
    }
}
