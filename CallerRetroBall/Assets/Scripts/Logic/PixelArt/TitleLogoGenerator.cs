using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// The "RETRO HOOPS" title logo, drawn in code with an original chunky pixel font:
    /// "RETRO" in a dithered sunset gradient, "HOOPS" in basketball orange with a basketball for
    /// the first O, a chrome shine line, a hard outline, a deep drop shadow, and 80s speed
    /// stripes underneath. Row 0 is the bottom row.
    /// </summary>
    public static class TitleLogoGenerator
    {
        private const int GlyphW = 7, GlyphH = 9, Spacing = 2, WordGap = 7, BallSize = 11;
        private const int Pad = 2;        // outline + room
        private const int ShadowDepth = 2;
        private const int StripeRows = 8;

        public static readonly RgbColor SunTop = new RgbColor(0xFF, 0xE0, 0x66);
        public static readonly RgbColor SunBottom = new RgbColor(0xF7, 0x25, 0x85);
        public static readonly RgbColor OrangeTop = new RgbColor(0xFF, 0xB8, 0x5C);
        public static readonly RgbColor OrangeBottom = new RgbColor(0xE0, 0x5A, 0x1E);
        public static readonly RgbColor Chrome = new RgbColor(0xFF, 0xFF, 0xF0);
        public static readonly RgbColor Outline = new RgbColor(0x12, 0x0A, 0x1E);
        public static readonly RgbColor ShadowColor = new RgbColor(0x3A, 0x0C, 0xA3);
        public static readonly RgbColor StripeCyan = new RgbColor(0x4C, 0xC9, 0xF0);

        private static readonly string[] R =
        {
            "######.", "##...##", "##...##", "##...##", "######.", "####...", "##.##..", "##..##.", "##...##",
        };
        private static readonly string[] E =
        {
            "#######", "#######", "##.....", "######.", "######.", "##.....", "##.....", "#######", "#######",
        };
        private static readonly string[] T =
        {
            "#######", "#######", "..###..", "..###..", "..###..", "..###..", "..###..", "..###..", "..###..",
        };

        private static readonly string[] O =
        {
            ".#####.", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", "##...##", ".#####.",
        };
        private static readonly string[] H =
        {
            "##...##", "##...##", "##...##", "##...##", "#######", "#######", "##...##", "##...##", "##...##",
        };
        private static readonly string[] P =
        {
            "######.", "##...##", "##...##", "##...##", "######.", "##.....", "##.....", "##.....", "##.....",
        };
        private static readonly string[] S =
        {
            ".######", "##.....", "##.....", ".#####.", ".....##", ".....##", ".....##", "##...##", ".#####.",
        };

        // RETRO + gap + H, ball, O, P, S.
        public static int Width => Pad * 2 + ShadowDepth + 5 * (GlyphW + Spacing) - Spacing + WordGap + 4 * (GlyphW + Spacing) + BallSize;
        public static int Height => Pad * 2 + ShadowDepth + BallSize + StripeRows;

        public static PixelCanvas Generate()
        {
            var c = new PixelCanvas(Width, Height);
            // Fill mask: 1 = RETRO (sunset), 2 = HOOPS (orange). Built first, then outlined and shadowed.
            var mask = new byte[c.Width * c.Height];
            int baseY = Pad + ShadowDepth + StripeRows; // bottom row of the letters
            int x = Pad;
            foreach (var g in new[] { R, E, T, R, O }) { Stamp(mask, c.Width, g, x, baseY, 1); x += GlyphW + Spacing; }
            x += WordGap - Spacing;
            Stamp(mask, c.Width, H, x, baseY, 2);
            x += GlyphW + Spacing;
            int ballX = x;
            x += BallSize + Spacing;
            foreach (var g in new[] { O, P, S }) { Stamp(mask, c.Width, g, x, baseY, 2); x += GlyphW + Spacing; }

            // Speed stripes under the word: cyan, pink, gold, each a little shorter.
            int stripeTop = baseY - ShadowDepth - 3;
            RgbColor[] stripeColors = { StripeCyan, SunBottom, SunTop };
            for (int i = 0; i < stripeColors.Length; i++)
            {
                int y = stripeTop - i * 2;
                int inset = 4 + i * 6;
                for (int sx = Pad + inset; sx < c.Width - Pad - ShadowDepth - inset; sx++) c.Set(sx, y, stripeColors[i]);
            }

            // Drop shadow (down-right), then outline, then fill.
            for (int y = 0; y < c.Height; y++)
                for (int xx = 0; xx < c.Width; xx++)
                {
                    if (Mask(mask, c.Width, c.Height, xx - ShadowDepth, y + ShadowDepth) != 0 ||
                        Mask(mask, c.Width, c.Height, xx - 1, y + 1) != 0)
                        c.Set(xx, y, ShadowColor);
                }
            for (int y = 0; y < c.Height; y++)
                for (int xx = 0; xx < c.Width; xx++)
                {
                    if (Mask(mask, c.Width, c.Height, xx, y) != 0) continue;
                    bool near = false;
                    for (int oy = -1; oy <= 1 && !near; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                            if (Mask(mask, c.Width, c.Height, xx + ox, y + oy) != 0) { near = true; break; }
                    if (near) c.Set(xx, y, Outline);
                }
            for (int y = 0; y < c.Height; y++)
                for (int xx = 0; xx < c.Width; xx++)
                {
                    byte m = Mask(mask, c.Width, c.Height, xx, y);
                    if (m == 0) continue;
                    int row = baseY + GlyphH - 1 - y; // 0 = top row of the letters
                    float t = row / (float)(GlyphH - 1);
                    // 4 dithered bands top to bottom.
                    float scaled = t * 3f;
                    int band = (int)scaled;
                    if (scaled - band > PixelCanvas.BayerThreshold(xx, y)) band++;
                    float bt = Math.Min(3, band) / 3f;
                    var col = m == 1 ? RgbColor.Lerp(SunTop, SunBottom, bt) : RgbColor.Lerp(OrangeTop, OrangeBottom, bt);
                    if (row == 1) col = RgbColor.Lerp(col, Chrome, 0.7f); // chrome shine line
                    c.Set(xx, y, col);
                }

            // The basketball "O" (drawn last so its own outline sits on top).
            DrawSmallBall(c, ballX + BallSize / 2f, baseY + GlyphH / 2f, BallSize / 2f);
            return c;
        }

        /// <summary>Basketball readable at ~11 px: outline, two-tone shading, 1px cross seams, one curved seam each side.</summary>
        private static void DrawSmallBall(PixelCanvas c, float cx, float cy, float r)
        {
            var seam = new RgbColor(0x3A, 0x1F, 0x14);
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d > r) continue;
                    if (d > r - 1f) { c.Set(x, y, Outline); continue; }
                    var col = (-dx + dy) > r * 0.35f ? AppIconGenerator.BallLight
                            : (-dx + dy) < -r * 0.45f ? AppIconGenerator.BallShade : AppIconGenerator.BallMain;
                    bool cross = Math.Abs(dx) < 0.5f || Math.Abs(dy) < 0.5f;
                    float ax = Math.Abs(dx), arc = r * 0.55f + dy * dy / (r * 1.6f);
                    bool curve = Math.Abs(ax - arc) < 0.5f && ax > 1f;
                    c.Set(x, y, cross || curve ? seam : col);
                }
        }

        private static void Stamp(byte[] mask, int width, string[] glyph, int left, int bottom, byte value)
        {
            for (int row = 0; row < glyph.Length; row++)
            {
                int y = bottom + glyph.Length - 1 - row;
                for (int col = 0; col < glyph[row].Length; col++)
                    if (glyph[row][col] == '#') mask[y * width + left + col] = value;
            }
        }

        private static byte Mask(byte[] mask, int w, int h, int x, int y) =>
            x < 0 || y < 0 || x >= w || y >= h ? (byte)0 : mask[y * w + x];
    }
}
