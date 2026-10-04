using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Original app icon and launch image, drawn in code like every other piece of art.
    /// The icon ("Sunset Swish") is 64x64 pixel art scaled up 16x to the 1024x1024 App Store size. It is fully opaque:
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

        public static readonly RgbColor DuskTop = new RgbColor(0x1B, 0x0B, 0x3A);
        public static readonly RgbColor DuskLow = new RgbColor(0xFF, 0x4F, 0xA3);
        public static readonly RgbColor SunTop = new RgbColor(0xFF, 0xE0, 0x66);
        public static readonly RgbColor SunLow = new RgbColor(0xFF, 0x7A, 0x3D);
        public static readonly RgbColor Neon = new RgbColor(0x3B, 0xD5, 0xFF);
        public static readonly RgbColor Grid = new RgbColor(0x9B, 0x4D, 0xFF);
        public static readonly RgbColor Floor = new RgbColor(0x1A, 0x08, 0x30);
        public static readonly RgbColor Board = new RgbColor(0xF4, 0xF1, 0xDE);
        public static readonly RgbColor BoardRed = new RgbColor(0xE6, 0x39, 0x46);
        public static readonly RgbColor Rim = new RgbColor(0xFF, 0x6B, 0x1A);
        public static readonly RgbColor Net = new RgbColor(0xF8, 0xF8, 0xFF);
        public static readonly RgbColor NetShade = new RgbColor(0xB8, 0xB4, 0xD8);

        /// <summary>
        /// "Sunset Swish", the Retro Hoops icon (64x64, row 0 is the bottom row): a ball dropping
        /// through the net under a backboard, in front of a striped synthwave sun over a neon grid floor.
        /// Drawn top-down here and flipped as it's written.
        /// </summary>
        public static PixelCanvas Icon()
        {
            const int n = IconPixels;
            var c = new PixelCanvas(n, n);
            void Put(int x, int y, RgbColor col) { if (x >= 0 && x < n && y >= 0 && y < n) c.Set(x, n - 1 - y, col); }
            const int horizon = 46;

            // Sky: dusk purple to hot pink in six dithered bands.
            for (int y = 0; y < horizon; y++)
            {
                float scaled = y / (float)(horizon - 1) * 5f;
                int band = (int)scaled;
                for (int x = 0; x < n; x++)
                {
                    int b = scaled - band > PixelCanvas.BayerThreshold(x, y) ? band + 1 : band;
                    Put(x, y, RgbColor.Lerp(DuskTop, DuskLow, Math.Min(5, b) / 5f));
                }
            }
            // Striped sun sitting on the horizon (the cuts thicken toward the bottom).
            for (int y = 16; y < horizon; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - 32f, dy = y + 0.5f - 42f;
                    if (dx * dx + dy * dy > 24f * 24f) continue;
                    int below = y - 33;
                    if (below >= 0 && below % 3 < 1 + below / 6) continue;
                    Put(x, y, RgbColor.Lerp(SunTop, SunLow, (y - 18) / 28f));
                }
            // Neon grid floor in perspective.
            for (int y = horizon; y < n; y++)
                for (int x = 0; x < n; x++) Put(x, y, Floor);
            for (int k = 0; k < 7; k++)
            {
                int y = horizon + (int)Math.Round(k * k * 0.9 + k * 1.1);
                for (int x = 0; x < n; x++) Put(x, y, k == 0 ? Neon : Grid);
            }
            // Lines fanning out from the vanishing point (sampled finely so they stay unbroken).
            for (int xb = -100; xb <= 164; xb += 24)
                for (int i = 0; i <= 400; i++)
                {
                    float t = i / 400f;
                    float y = horizon + 6 + t * (n - 7 - horizon);
                    float tt = (y - horizon) / (n - 1 - horizon);
                    Put((int)Math.Round(32 + (xb - 32) * tt), (int)Math.Round(y), Grid);
                }

            // Backboard with its square, and a bracket.
            for (int y = 4; y <= 16; y++)
                for (int x = 20; x <= 43; x++)
                {
                    bool edge = y == 4 || y == 16 || x == 20 || x == 43;
                    bool square = (x == 28 || x == 35) && y >= 9 && y <= 15 || (y == 9 || y == 15) && x >= 28 && x <= 35;
                    Put(x, y, edge ? Outline : (square ? BoardRed : Board));
                }

            // Net behind the ball (back strands), the ball, then the front strands and the rim.
            bool InNet(int x, int y, out bool front)
            {
                front = false;
                if (y < 20 || y > 32) return false;
                float t = (y - 20) / 12f;
                float left = 23 + 4 * t, right = 40 - 4 * t;
                if (x < left - 0.5f || x > right + 0.5f) return false;
                int u = x - 23, v = y - 20;
                bool mesh = (u + v) % 4 == 0 || (u - v + 100) % 4 == 0 || y == 32;
                front = mesh && (u + v) % 4 == 0;
                return mesh;
            }
            for (int y = 20; y <= 32; y++)
                for (int x = 20; x <= 43; x++)
                    if (InNet(x, y, out _)) Put(x, y, NetShade);
            DrawBall(c, 32f, n - 22.5f, 8.5f);
            for (int y = 25; y <= 32; y++)
                for (int x = 20; x <= 43; x++)
                    if (InNet(x, y, out bool front) && front) Put(x, y, Net);
            for (int y = 17; y <= 19; y++)
                for (int x = 21; x <= 42; x++)
                    Put(x, y, y == 18 && x > 21 && x < 42 ? Rim : Outline);

            // Gold sparkles: the swish.
            foreach (var (sx, sy) in new[] { (12, 12), (52, 10), (9, 30), (55, 28), (47, 39) })
            {
                Put(sx, sy, Gold);
                Put(sx - 1, sy, Gold.Darken(0.2f)); Put(sx + 1, sy, Gold.Darken(0.2f));
                Put(sx, sy - 1, Gold.Darken(0.2f)); Put(sx, sy + 1, Gold.Darken(0.2f));
            }
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
