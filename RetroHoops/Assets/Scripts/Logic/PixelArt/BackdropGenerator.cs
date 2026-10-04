using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Generates the original street-court backdrop: dithered sunset sky, a striped
    /// setting sun, a seeded skyline, a chain-link fence, and an asphalt court edge.
    /// Portrait virtual resolution by default (180 x 320 = 9:16).
    /// </summary>
    public static class BackdropGenerator
    {
        public const int DefaultWidth = 180;
        public const int DefaultHeight = 320;

        public static PixelCanvas Generate(CourtDef court, uint seed, int width = DefaultWidth, int height = DefaultHeight)
        {
            if (court == null) throw new ArgumentNullException(nameof(court));
            return Generate(court.skyTop, court.skyBottom, court.floor, court.lines, seed, width, height);
        }

        public static PixelCanvas Generate(RgbColor skyTop, RgbColor skyBottom, RgbColor asphalt, RgbColor lineColor,
                                           uint seed, int width = DefaultWidth, int height = DefaultHeight)
        {
            var c = new PixelCanvas(width, height);
            var rng = new SeededRandom(seed);

            int horizon = (int)(height * 0.34f);  // skyline base
            int courtTop = (int)(height * 0.30f); // asphalt starts below this

            // 1. Sky: 6 colour bands with ordered dithering between them.
            const int bands = 6;
            // Starts at courtTop so the strip behind the fence is filled too.
            for (int y = courtTop; y < height; y++)
            {
                float t = Math.Max(0, y - horizon) / (float)Math.Max(1, height - horizon); // 0 at horizon, 1 at top
                float scaled = (1f - t) * (bands - 1);
                int band = (int)scaled;
                float frac = scaled - band;
                for (int x = 0; x < width; x++)
                {
                    int b = frac > PixelCanvas.BayerThreshold(x, y) ? band + 1 : band;
                    float bt = b / (float)(bands - 1);
                    c.Set(x, y, RgbColor.Lerp(skyTop, skyBottom, bt));
                }
            }

            // 2. Sun with horizontal cut stripes near its base.
            var sun = RgbColor.Lerp(skyBottom, new RgbColor(255, 230, 120), 0.65f);
            float sunR = width * 0.28f;
            float sunCx = width * 0.5f, sunCy = horizon + sunR * 0.55f;
            for (int y = (int)(sunCy - sunR); y <= (int)(sunCy + sunR); y++)
            {
                // Stripes start at the visible horizon and thin out as they rise.
                int fromBase = y - horizon;
                bool cut = fromBase >= 0 && (fromBase % 6) < (3 - fromBase / 10);
                if (cut) continue;
                for (int x = (int)(sunCx - sunR); x <= (int)(sunCx + sunR); x++)
                {
                    float dx = x + 0.5f - sunCx, dy = y + 0.5f - sunCy;
                    if (dx * dx + dy * dy <= sunR * sunR) c.Set(x, y, sun);
                }
            }

            // 3. Skyline silhouettes (two layers) with a few lit windows.
            var far = RgbColor.Lerp(skyTop, RgbColor.Black, 0.35f);
            var near = RgbColor.Lerp(skyTop, RgbColor.Black, 0.65f);
            DrawSkyline(c, rng, horizon, 12, 38, 8, 20, far, null);
            DrawSkyline(c, rng, horizon, 6, 26, 10, 24, near, new RgbColor(255, 214, 102));

            // 4. Asphalt + court lines (simple perspective baseline and lane).
            var asphaltDark = asphalt.Darken(0.25f);
            for (int y = 0; y < courtTop; y++)
                for (int x = 0; x < width; x++)
                {
                    bool speck = ((x * 7 + y * 13) ^ (int)(seed & 0xFF)) % 11 == 0;
                    c.Set(x, y, speck ? asphaltDark : asphalt);
                }
            for (int x = 0; x < width; x++) c.Set(x, courtTop - 1, asphalt.Lighten(0.15f));
            int laneHalf = width / 6;
            int cx = width / 2;
            c.Line(cx - laneHalf, courtTop - 2, cx - laneHalf - 14, 0, lineColor);
            c.Line(cx + laneHalf, courtTop - 2, cx + laneHalf + 14, 0, lineColor);
            c.Line(cx - laneHalf - 6, courtTop / 2, cx + laneHalf + 6, courtTop / 2, lineColor);

            // 5. Chain-link fence between court and skyline.
            var fence = new RgbColor(200, 200, 210, 70);
            for (int y = courtTop; y < horizon + 22; y++)
                for (int x = 0; x < width; x++)
                    if (((x + y) % 6 == 0) || ((x - y + 600) % 6 == 0)) c.Set(x, y, fence);
            for (int x = 0; x < width; x += 30)
                for (int y = courtTop; y < horizon + 24; y++) c.Set(x, y, new RgbColor(90, 90, 100));

            // 6. Halftone dots fading toward the top for print-poster texture.
            var dot = new RgbColor(255, 255, 255, 28);
            for (int y = horizon + 30; y < height; y += 4)
            {
                float t = (y - horizon) / (float)(height - horizon);
                if (t < 0.45f) continue;
                int offset = (y / 4) % 2 * 2;
                for (int x = offset; x < width; x += 4) c.Set(x, y, dot);
            }

            return c;
        }

        private static void DrawSkyline(PixelCanvas c, SeededRandom rng, int baseY, int minW, int maxW,
                                        int minH, int maxH, RgbColor color, RgbColor? windowColor)
        {
            int x = -rng.Range(0, 8);
            while (x < c.Width)
            {
                int w = rng.Range(minW, maxW);
                int h = rng.Range(minH, maxH) + (rng.Chance(0.2f) ? 18 : 0);
                c.FillRect(x, baseY, w, h, color);
                if (rng.Chance(0.3f)) c.FillRect(x + w / 2, baseY + h, 1, 5, color); // antenna
                if (windowColor.HasValue)
                {
                    for (int wy = baseY + 3; wy < baseY + h - 2; wy += 4)
                        for (int wx = x + 2; wx < x + w - 2; wx += 3)
                            if (rng.Chance(0.18f)) c.Set(wx, wy, windowColor.Value);
                }
                x += w + rng.Range(0, 3);
            }
        }
    }
}
