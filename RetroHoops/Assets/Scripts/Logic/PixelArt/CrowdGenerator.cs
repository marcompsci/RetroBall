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
    }
}
