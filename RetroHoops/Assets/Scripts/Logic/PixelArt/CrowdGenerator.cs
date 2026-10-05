using System;
namespace CallerRetroBall.Logic.PixelArt
{
    public enum CrowdMood { Idle = 0, Cheer = 1, Groan = 2 }

    /// <summary>
    /// Tiny seated fans for the animated crowd behind the baseline (5x8 art pixels each): shoulders,
    /// head, and, when cheering, both arms up. Original pixel art. Row 0 is the bottom row.
    /// </summary>
    public static class CrowdGenerator
    {
        public const int Width = 5;
        public const int Height = 8;

        public static PixelCanvas Fan(RgbColor shirt, RgbColor skin, bool armsUp)
        {
            var c = new PixelCanvas(Width, Height);
            c.FillRect(0, 0, 5, 3, shirt);               // shoulders
            c.FillRect(1, 3, 3, 3, skin);                // head
            c.Set(1, 5, skin.Darken(0.35f));             // hair line
            c.Set(2, 5, skin.Darken(0.35f));
            c.Set(3, 5, skin.Darken(0.35f));
            if (armsUp)
            {
                c.FillRect(0, 3, 1, 4, shirt);           // left arm
                c.FillRect(4, 3, 1, 4, shirt);           // right arm
                c.Set(0, 7, skin);                       // hands
                c.Set(4, 7, skin);
            }
            return c;
        }

        /// <summary>Vertical bob (art pixels) for a fan at time <paramref name="t"/>; each fan has its own phase.</summary>
        public static int Bob(CrowdMood mood, float t, int fanIndex)
        {
            switch (mood)
            {
                case CrowdMood.Cheer:
                    return ((int)(t * 8f) + fanIndex) % 2 == 0 ? 1 : 0;
                case CrowdMood.Groan:
                    return -1;
                default:
                    return ((int)(t * 1.5f) + fanIndex * 3) % 7 == 0 ? 1 : 0;
            }
        }

        /// <summary>Rows of the landscape sideline stands (fans stand on each).</summary>
        public const int StandRows = 3;
        public const int StandRowHeight = 7;

        /// <summary>
        /// Bleachers along the far sideline for landscape Full Court: a low wall, three stepped rows and a
        /// back rail, in the court's colours. <paramref name="widthPx"/> wide; row 0 is the front (lowest) row.
        /// </summary>
        public static PixelCanvas SidelineStands(int widthPx, RgbColor wall, RgbColor seat, RgbColor accent)
        {
            int h = 4 + StandRows * StandRowHeight + 3;
            var c = new PixelCanvas(Math.Max(8, widthPx), h);
            // Front wall with an accent stripe.
            c.FillRect(0, 0, c.Width, 4, wall.Darken(0.2f));
            c.FillRect(0, 2, c.Width, 1, accent);
            for (int row = 0; row < StandRows; row++)
            {
                int y = 4 + row * StandRowHeight;
                var face = row % 2 == 0 ? seat : seat.Darken(0.12f);
                c.FillRect(0, y, c.Width, StandRowHeight, face.Darken(0.1f * row));
                c.FillRect(0, y + StandRowHeight - 1, c.Width, 1, seat.Lighten(0.2f)); // bench edge
                for (int x = (row * 5) % 16; x < c.Width; x += 16) c.FillRect(x, y, 1, StandRowHeight - 1, wall.Darken(0.35f)); // aisle posts
            }
            // Back rail.
            c.FillRect(0, h - 3, c.Width, 3, wall.Darken(0.4f));
            c.FillRect(0, h - 2, c.Width, 1, accent.Darken(0.2f));
            return c;
        }

        /// <summary>Bottom pixel row (in the stands canvas) where fans in <paramref name="row"/> stand.</summary>
        public static int StandRowFloor(int row) => 4 + row * StandRowHeight + 1;
    }
}
