using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Renders the half court as a single top-down pixel texture: crowd strip behind the
    /// baseline, floor (asphalt or hardwood), paint, and all line markings. The hoop is a
    /// separate sprite so the ball can pass in front of or behind it.
    ///
    /// Texture space maps from court space with a fixed margin; see <see cref="CourtToPixel"/>.
    /// </summary>
    public static class CourtGenerator
    {
        public const int PixelsPerMeter = 16;
        /// <summary>Extra floor shown beside each sideline (m).</summary>
        public const float SideMargin = 1.0f;
        /// <summary>Crowd/wall strip shown behind the baseline (m).</summary>
        public const float BaselineMargin = 2.0f;
        /// <summary>Extra floor beyond the top of the playing area (m).</summary>
        public const float TopMargin = 1.0f;

        public static int TextureWidth(CourtGeometry g) => (int)Math.Round((g.width + SideMargin * 2f) * PixelsPerMeter);
        public static int TextureHeight(CourtGeometry g) => (int)Math.Round((g.depth + BaselineMargin + TopMargin) * PixelsPerMeter);

        /// <summary>
        /// Court space → texture pixel. The baseline is near the TOP of the texture (hoop at the
        /// top of the screen); texture row 0 is the bottom row.
        /// </summary>
        public static void CourtToPixel(CourtGeometry g, Vec2 p, out float px, out float py)
        {
            px = (p.x + g.HalfWidth + SideMargin) * PixelsPerMeter;
            py = (g.depth + TopMargin - p.y) * PixelsPerMeter;
        }

        /// <summary>Texture pivot (0..1) that sits exactly on court origin (centre of the baseline).</summary>
        public static void OriginPivot(CourtGeometry g, out float pivotX, out float pivotY)
        {
            CourtToPixel(g, Vec2.Zero, out float px, out float py);
            pivotX = px / TextureWidth(g);
            pivotY = py / TextureHeight(g);
        }

        public static PixelCanvas Generate(CourtDef court, CourtGeometry g, uint seed) => Generate(court, g, seed, null, null);

        /// <summary>With an optional two-colour court banner (cosmetic) hung over the crowd.</summary>
        public static PixelCanvas Generate(CourtDef court, CourtGeometry g, uint seed, RgbColor? bannerA, RgbColor? bannerB)
        {
            if (court == null) throw new ArgumentNullException(nameof(court));
            int w = TextureWidth(g), h = TextureHeight(g);
            var c = new PixelCanvas(w, h);
            var rng = new SeededRandom(seed);
            bool hardwood = court.circuit == CourtCircuit.League;

            // Floor
            var floorDark = court.floor.Darken(0.12f);
            var floorLight = court.floor.Lighten(0.06f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    RgbColor col;
                    if (hardwood)
                    {
                        // Vertical planks with staggered seams.
                        int plank = x / 6;
                        bool seam = x % 6 == 0 || (y + plank * 13) % 40 == 0;
                        col = seam ? floorDark : (plank % 2 == 0 ? court.floor : floorLight);
                    }
                    else
                    {
                        bool speck = ((x * 7 + y * 13) ^ (int)(seed & 0xFF)) % 9 == 0;
                        bool crack = ((x * 3 + y * 5) % 97) == 0;
                        col = speck || crack ? floorDark : court.floor;
                    }
                    c.Set(x, y, col);
                }

            // Paint (key) fill
            FillCourtRect(c, g, -g.paintWidth * 0.5f, 0f, g.paintWidth * 0.5f, g.paintLength, court.paint.WithAlpha(hardwood ? (byte)200 : (byte)150));

            // Lines
            var line = court.lines;
            float hw = g.HalfWidth;
            CourtLine(c, g, new Vec2(-hw, 0f), new Vec2(hw, 0f), line);            // baseline
            CourtLine(c, g, new Vec2(-hw, 0f), new Vec2(-hw, g.depth), line);      // sidelines
            CourtLine(c, g, new Vec2(hw, 0f), new Vec2(hw, g.depth), line);
            CourtLine(c, g, new Vec2(-hw, g.depth), new Vec2(hw, g.depth), line);  // top of playing area
            float pw = g.paintWidth * 0.5f;
            CourtLine(c, g, new Vec2(-pw, 0f), new Vec2(-pw, g.paintLength), line);
            CourtLine(c, g, new Vec2(pw, 0f), new Vec2(pw, g.paintLength), line);
            CourtLine(c, g, new Vec2(-pw, g.paintLength), new Vec2(pw, g.paintLength), line);
            CourtArc(c, g, new Vec2(0f, g.paintLength), g.freeThrowCircleRadius, 0f, 360f, line);
            CourtArc(c, g, g.Hoop, 1.25f, -90f, 90f, line); // restricted area

            // Arc line + straight corners
            float cornerX = g.CornerLineX, cornerTop = g.CornerLineTopY;
            CourtLine(c, g, new Vec2(-cornerX, 0f), new Vec2(-cornerX, cornerTop), line);
            CourtLine(c, g, new Vec2(cornerX, 0f), new Vec2(cornerX, cornerTop), line);
            float edge = (float)(Math.Asin(cornerX / g.arcRadius) * 180.0 / Math.PI);
            CourtArc(c, g, g.Hoop, g.arcRadius, -edge, edge, line);

            // Centre-top logo circle segment (decor, original)
            CourtArc(c, g, new Vec2(0f, g.depth), 1.8f, 90f, 270f, line);

            DrawCrowd(c, g, court, rng);
            if (bannerA.HasValue) DrawBanner(c, g, bannerA.Value, bannerB ?? bannerA.Value);
            return c;
        }

        private static void FillCourtRect(PixelCanvas c, CourtGeometry g, float x0, float y0, float x1, float y1, RgbColor col)
        {
            CourtToPixel(g, new Vec2(x0, y1), out float ax, out float ay);
            CourtToPixel(g, new Vec2(x1, y0), out float bx, out float by);
            int left = (int)Math.Round(ax), right = (int)Math.Round(bx);
            int bottom = (int)Math.Round(ay), top = (int)Math.Round(by);
            c.FillRect(left, bottom, right - left, top - bottom, col);
        }

        private static void CourtLine(PixelCanvas c, CourtGeometry g, Vec2 a, Vec2 b, RgbColor col)
        {
            CourtToPixel(g, a, out float ax, out float ay);
            CourtToPixel(g, b, out float bx, out float by);
            c.Line(Clamp((int)Math.Round(ax), c.Width), Clamp((int)Math.Round(ay), c.Height),
                   Clamp((int)Math.Round(bx), c.Width), Clamp((int)Math.Round(by), c.Height), col);
        }

        private static int Clamp(int v, int size) => v < 0 ? 0 : (v >= size ? size - 1 : v);

        /// <summary>
        /// Arc in court space. Angles are degrees measured from +y (toward half-court),
        /// clockwise toward +x, so -90..90 is the half facing away from the baseline.
        /// </summary>
        private static void CourtArc(PixelCanvas c, CourtGeometry g, Vec2 center, float radius, float fromDeg, float toDeg, RgbColor col)
        {
            int steps = Math.Max(16, (int)(radius * PixelsPerMeter * 8f * (toDeg - fromDeg) / 360f));
            for (int i = 0; i <= steps; i++)
            {
                double a = (fromDeg + (toDeg - fromDeg) * i / steps) * Math.PI / 180.0;
                var p = new Vec2(center.x + (float)Math.Sin(a) * radius, center.y + (float)Math.Cos(a) * radius);
                if (p.y < 0f) continue; // never draw behind the baseline
                CourtToPixel(g, p, out float px, out float py);
                c.Set((int)Math.Round(px), (int)Math.Round(py), col);
            }
        }

        private static void DrawBanner(PixelCanvas c, CourtGeometry g, RgbColor a, RgbColor b)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int y0 = (int)Math.Round(baselinePy) + 12;
            // Two banners either side of the backboard, with a zig-zag trim.
            foreach (int x0 in new[] { 20, c.Width - 20 - 64 })
            {
                c.FillRect(x0, y0, 64, 10, a);
                for (int x = 0; x < 64; x++)
                {
                    c.Set(x0 + x, y0, b);
                    if (x % 4 < 2) c.Set(x0 + x, y0 + 1, b);
                    c.Set(x0 + x, y0 + 9, b);
                }
                for (int x = 8; x < 56; x += 8) c.FillRect(x0 + x, y0 + 4, 4, 3, b);
            }
        }

        private static void DrawCrowd(PixelCanvas c, CourtGeometry g, CourtDef court, SeededRandom rng)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int start = (int)Math.Round(baselinePy) + 2;
            var wall = court.skyTop.Darken(0.35f);
            var wallLight = court.skyTop.Darken(0.15f);
            for (int y = start; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, (y - start) % 8 == 0 ? wallLight : wall);

            // Seated silhouettes: heads + shoulders in muted sky colours.
            var person = court.skyBottom.Darken(0.55f);
            var personAlt = court.skyBottom.Darken(0.4f);
            for (int row = 0; row < 3; row++)
            {
                int baseY = start + 2 + row * 8;
                for (int x = 2; x < c.Width - 4; x += 5)
                {
                    if (!rng.Chance(court.crowdDensity)) continue;
                    var col = rng.Chance(0.5f) ? person : personAlt;
                    int jitter = rng.Range(-1, 2);
                    c.FillRect(x, baseY + jitter, 4, 3, col);        // shoulders
                    c.FillRect(x + 1, baseY + 3 + jitter, 2, 2, col); // head
                }
            }
        }
    }
}
