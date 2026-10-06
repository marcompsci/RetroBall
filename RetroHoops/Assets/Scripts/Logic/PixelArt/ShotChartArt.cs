using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Phase 35: draws a shot chart as pixel art: the half-court from above (hoop at the bottom), and one disc per spot
    /// shot from, sized by attempts and coloured by how that spot is going (red hot, blue cold, cream in between,
    /// grey when there aren't enough shots to say), with "made/attempted" under it.
    /// </summary>
    public static class ShotChartArt
    {
        public const int Scale = 8;
        public static readonly RgbColor Floor = new RgbColor(28, 22, 46);
        public static readonly RgbColor Lines = new RgbColor(92, 82, 128);
        public static readonly RgbColor Rim = new RgbColor(255, 140, 64);
        public static readonly RgbColor HotColor = new RgbColor(255, 84, 72);
        public static readonly RgbColor ColdColor = new RgbColor(72, 156, 255);
        public static readonly RgbColor NeutralColor = new RgbColor(246, 232, 200);
        public static readonly RgbColor UnknownColor = new RgbColor(140, 134, 160);

        public static RgbColor ColorFor(SpotHeat h)
        {
            switch (h)
            {
                case SpotHeat.Hot: return HotColor;
                case SpotHeat.Cold: return ColdColor;
                case SpotHeat.Neutral: return NeutralColor;
                default: return UnknownColor;
            }
        }

        /// <summary>Disc radius in pixels for a spot with <paramref name="attempts"/> shots (0 = none drawn).</summary>
        public static float Radius(int attempts, int mostAttempts)
        {
            if (attempts <= 0) return 0f;
            float share = mostAttempts <= 0 ? 0f : (float)attempts / mostAttempts;
            return 3f + 4f * (float)Math.Sqrt(Math.Min(1f, share));
        }

        public static PixelCanvas Render(ShotChartData d, CourtGeometry court = null)
        {
            court = court ?? CourtGeometry.Default;
            int w = (int)Math.Ceiling(court.width * Scale), h = (int)Math.Ceiling(court.depth * Scale);
            var c = new PixelCanvas(w, h);
            c.Fill(Floor);
            DrawCourt(c, court);
            if (d == null) return c;

            int most = 0;
            for (int i = 0; i < ShotZones.SpotCount; i++) most = Math.Max(most, d.spots[i].attempted);
            for (int i = 0; i < ShotZones.SpotCount; i++)
            {
                var r = d.spots[i];
                if (r.attempted <= 0) continue;
                var spot = (ShotSpot)i;
                var at = ShotCharts.CourtPoint(spot, court);
                float px = X(at.x, court), py = at.y * Scale;
                var col = ColorFor(ShotCharts.Heat(d, spot));
                float rad = Radius(r.attempted, most);
                c.FillCircle(px, py, rad + 1f, Floor);
                c.FillCircle(px, py, rad, col);
                string label = r.made + "/" + r.attempted;
                int top = (int)Math.Round(py - rad - 2f);
                int lw = PixelFont.Measure(label);
                int left = Math.Max(0, Math.Min(w - lw, (int)Math.Round(px) - lw / 2));
                if (top - 5 < 0) top = (int)Math.Round(py + rad + 7f); // no room underneath (the corners): above it
                c.FillRect(left - 1, top - 5, lw + 2, 7, Floor);
                PixelFont.Draw(c, label, left, top, NeutralColor);
            }
            return c;
        }

        private static float X(float courtX, CourtGeometry court) => (courtX + court.HalfWidth) * Scale;

        private static void DrawCourt(PixelCanvas c, CourtGeometry court)
        {
            int w = c.Width, h = c.Height;
            // Outline.
            c.Line(0, 0, w - 1, 0, Lines);
            c.Line(0, 0, 0, h - 1, Lines);
            c.Line(w - 1, 0, w - 1, h - 1, Lines);
            c.Line(0, h - 1, w - 1, h - 1, Lines);
            // Paint.
            int pl = (int)Math.Round(X(-court.paintWidth * 0.5f, court)), pr = (int)Math.Round(X(court.paintWidth * 0.5f, court));
            int pt = (int)Math.Round(court.paintLength * Scale);
            c.Line(pl, 0, pl, pt, Lines);
            c.Line(pr, 0, pr, pt, Lines);
            c.Line(pl, pt, pr, pt, Lines);
            // Corner lines and the arc.
            int cl = (int)Math.Round(X(-court.CornerLineX, court)), cr = (int)Math.Round(X(court.CornerLineX, court));
            int ct = (int)Math.Round(court.CornerLineTopY * Scale);
            c.Line(cl, 0, cl, ct, Lines);
            c.Line(cr, 0, cr, ct, Lines);
            float start = (float)Math.Atan2(court.CornerLineTopY - court.hoopY, court.CornerLineX);
            int steps = 90;
            int lastX = -1, lastY = -1;
            for (int k = 0; k <= steps; k++)
            {
                float a = start + (float)(Math.PI - 2 * start) * k / steps;
                int x = (int)Math.Round(X(court.arcRadius * (float)Math.Cos(a), court));
                int y = (int)Math.Round((court.hoopY + court.arcRadius * (float)Math.Sin(a)) * Scale);
                if (lastX >= 0) c.Line(lastX, lastY, x, y, Lines);
                lastX = x; lastY = y;
            }
            // Hoop.
            float hx = X(0f, court), hy = court.hoopY * Scale;
            c.FillCircle(hx, hy, court.rimRadius * Scale + 1.5f, Rim);
            c.FillCircle(hx, hy, court.rimRadius * Scale, Floor);
        }
    }
}
