using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Engine-free RGBA pixel buffer used to generate all placeholder art procedurally.
    /// Row 0 is the BOTTOM row to match UnityEngine.Texture2D.SetPixels32.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly RgbColor[] Pixels;

        public PixelCanvas(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Canvas must be at least 1x1.");
            Width = width;
            Height = height;
            Pixels = new RgbColor[width * height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public RgbColor Get(int x, int y) => InBounds(x, y) ? Pixels[y * Width + x] : RgbColor.Clear;

        /// <summary>Writes a pixel, alpha-blending over what is there. Out-of-bounds writes are ignored.</summary>
        public void Set(int x, int y, RgbColor c)
        {
            if (!InBounds(x, y) || c.a == 0) return;
            int i = y * Width + x;
            if (c.a == 255)
            {
                Pixels[i] = c;
                return;
            }
            var dst = Pixels[i];
            if (dst.a == 0)
            {
                Pixels[i] = c;
                return;
            }
            var blended = RgbColor.Lerp(dst.WithAlpha(255), c.WithAlpha(255), c.a / 255f);
            blended.a = (byte)Math.Min(255, dst.a + c.a * (255 - dst.a) / 255);
            Pixels[i] = blended;
        }

        public void Fill(RgbColor c)
        {
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = c;
        }

        public void FillRect(int x, int y, int w, int h, RgbColor c)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(Height, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(Width, x + w); xx++)
                    Set(xx, yy, c);
        }

        public void FillCircle(float cx, float cy, float radius, RgbColor c)
        {
            int minX = (int)Math.Floor(cx - radius), maxX = (int)Math.Ceiling(cx + radius);
            int minY = (int)Math.Floor(cy - radius), maxY = (int)Math.Ceiling(cy + radius);
            float r2 = radius * radius;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2) Set(x, y, c);
                }
        }

        /// <summary>Bresenham line.</summary>
        public void Line(int x0, int y0, int x1, int y1, RgbColor c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>
        /// Stamps a text bitmap. Rows are given TOP to bottom; '.' or ' ' = skip,
        /// '#' = primary colour, '+' = secondary colour.
        /// </summary>
        public void Stamp(string[] rows, int left, int top, RgbColor primary, RgbColor secondary, int scale = 1)
        {
            for (int row = 0; row < rows.Length; row++)
            {
                string line = rows[row];
                for (int col = 0; col < line.Length; col++)
                {
                    char ch = line[col];
                    if (ch != '#' && ch != '+') continue;
                    var color = ch == '#' ? primary : secondary;
                    for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                            Set(left + col * scale + sx, top - row * scale - sy, color);
                }
            }
        }

        /// <summary>4x4 Bayer threshold (0..1) for ordered dithering between colour bands.</summary>
        public static float BayerThreshold(int x, int y)
        {
            int v = Bayer4[(y & 3) * 4 + (x & 3)];
            return (v + 0.5f) / 16f;
        }

        private static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>Counts pixels with non-zero alpha. Handy in tests.</summary>
        public int OpaqueCount()
        {
            int n = 0;
            for (int i = 0; i < Pixels.Length; i++) if (Pixels[i].a > 0) n++;
            return n;
        }
    }
}
