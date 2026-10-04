using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 8-bit console-style UI skin, drawn in code: flat 2D buttons with a hard black outline,
    /// a bright bevel and shine band on top, a darker lip underneath, and a white glint in the
    /// corner. Every button is a 9-sliceable sprite; the round action buttons are fixed size.
    /// Original pixel art (no console-maker assets or logos).
    /// </summary>
    public static class UiSkinGenerator
    {
        public const int ButtonSize = 16;
        /// <summary>9-slice border in art pixels (corners, shine, and lip stay crisp).</summary>
        public const int ButtonBorder = 6;
        public const int DiscSize = 24;

        public static readonly RgbColor Outline = new RgbColor(0x10, 0x10, 0x18);
        public static readonly RgbColor Glint = new RgbColor(0xFF, 0xFF, 0xFF);

        /// <summary>
        /// Rectangular button. <paramref name="pressed"/> draws the pushed-in state: no lip, darker
        /// face, shine shifted down one pixel, so the button visibly sinks when tapped.
        /// </summary>
        public static PixelCanvas Button(RgbColor face, bool pressed = false)
        {
            const int s = ButtonSize;
            var c = new PixelCanvas(s, s);
            var body = pressed ? face.Darken(0.12f) : face;
            var light = body.Lighten(0.35f);
            var shine = body.Lighten(0.55f);
            var shade = body.Darken(0.3f);
            var lip = body.Darken(0.5f);
            int lipRows = pressed ? 0 : 2;     // the 3D "edge" under the face
            int top = s - 1, bottom = 0;
            int faceBottom = bottom + 1 + lipRows;  // first face row above the outline + lip
            int shift = pressed ? 1 : 0;

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int dx = Math.Min(x, s - 1 - x);
                    int dyTop = top - y, dyBottom = y - bottom;
                    int dy = Math.Min(dyTop, dyBottom);
                    if (dx == 0 && dy == 0) continue;                 // 1px cut corners
                    bool outline = dx == 0 || dy == 0 || (dx == 1 && dy == 1);
                    if (outline) { c.Set(x, y, Outline); continue; }

                    RgbColor col;
                    if (y < faceBottom) col = lip;                     // bottom lip
                    else if (y == faceBottom) col = shade;             // shadow line on the face
                    else if (dyTop == 1 + shift) col = light;          // top bevel
                    else if (dyTop == 2 + shift || dyTop == 3 + shift) col = shine; // shine band
                    else if (dx == 1) col = x < s / 2 ? light : shade; // left bevel / right shade
                    else col = body;
                    c.Set(x, y, col);
                }

            // Corner glint: a little white "L" in the top-left (inside the 9-slice corner).
            int gy = top - 2 - shift;
            c.Set(2, gy, Glint);
            c.Set(3, gy, Glint);
            c.Set(4, gy, Glint);
            c.Set(2, gy - 1, Glint);
            return c;
        }

        /// <summary>Round action button (SHOOT / PASS / ...): outlined, shaded, with a shine crescent and glint.</summary>
        public static PixelCanvas Disc(RgbColor face, bool pressed = false)
        {
            const int s = DiscSize;
            var c = new PixelCanvas(s, s);
            var body = pressed ? face.Darken(0.12f) : face;
            var shine = body.Lighten(0.5f);
            var shade = body.Darken(0.3f);
            var lip = body.Darken(0.5f);
            float r = s / 2f;
            float lipOffset = pressed ? 0f : 1.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float px = x + 0.5f - r, py = y + 0.5f - r;
                    float d = (float)Math.Sqrt(px * px + py * py);
                    if (d > r) continue;
                    if (d > r - 1.2f) { c.Set(x, y, Outline); continue; }
                    // Face is a circle raised by the lip; below it the lip shows.
                    float fy = py - lipOffset;
                    float fd = (float)Math.Sqrt(px * px + fy * fy);
                    float faceR = r - 1.2f - lipOffset;
                    if (fd > faceR) { c.Set(x, y, lip); continue; }
                    RgbColor col = body;
                    // Shine crescent on the upper-left, shade on the lower-right.
                    float light = (-px + fy) / faceR;
                    if (light > 0.55f && fd > faceR - 3f) col = shine;
                    else if (light < -0.45f && fd > faceR - 3f) col = shade;
                    c.Set(x, y, col);
                }
            // Glint
            int gx = (int)(r - r * 0.45f), gy = (int)(r + r * 0.45f + lipOffset * 0.5f);
            c.Set(gx, gy, Glint);
            c.Set(gx + 1, gy, Glint);
            c.Set(gx, gy - 1, Glint);
            return c;
        }
    }
}
