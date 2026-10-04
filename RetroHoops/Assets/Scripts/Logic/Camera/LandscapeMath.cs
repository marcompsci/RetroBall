using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Pure maths for drawing the court portrait (hoop at the top) or landscape (Full Court turned a
    /// quarter turn, baskets left and right), and for turning stick input into court directions.
    /// Screen plane: x right, y up, 1 unit = 1 m.
    /// </summary>
    public static class LandscapeMath
    {
        /// <summary>Court point (x across, y down the floor) → screen plane.</summary>
        public static Vec2 ToScreenPlane(Vec2 court, bool landscape, float length)
        {
            return landscape ? new Vec2(court.y - length * 0.5f, court.x) : new Vec2(court.x, -court.y);
        }

        public static Vec2 FromScreenPlane(Vec2 plane, bool landscape, float length)
        {
            return landscape ? new Vec2(plane.y, plane.x + length * 0.5f) : new Vec2(plane.x, -plane.y);
        }

        /// <summary>Screen direction (+y up) → court direction for this view.</summary>
        public static Vec2 ScreenToCourt(Vec2 screen, bool landscape) =>
            landscape ? new Vec2(screen.y, screen.x) : new Vec2(screen.x, -screen.y);

        /// <summary>Depth for sorting: bigger = lower on screen = drawn in front.</summary>
        public static float Depth(Vec2 court, bool landscape) => landscape ? -court.x : court.y;

        /// <summary>
        /// A court facing as seen in landscape. Facing N is court +y (down the screen in portrait); in
        /// landscape court +y points right, which is the portrait sprite set's E, so every facing turns
        /// two steps clockwise.
        /// </summary>
        public static Facing8 TurnFacing(Facing8 f) => (Facing8)(((int)f + 2) % 8);

        /// <summary>Canvas reference size and width/height match for a screen (UI is laid out at 1080 x 1920 portrait, 1920 x 1080 landscape).</summary>
        public static void Canvas(int width, int height, out float refW, out float refH, out float match)
        {
            if (height <= 0 || width <= 0) { refW = 1080f; refH = 1920f; match = 0.35f; return; }
            if (width <= height)
            {
                refW = 1080f;
                refH = 1920f;
                match = CameraMath.UiMatch(width / (float)height);
            }
            else
            {
                refW = 1920f;
                refH = 1080f;
                // Wider than 16:9 (phones): fit the height. Squarer (iPad landscape): fit the width.
                match = width / (float)height >= 1920f / 1080f ? 1f : 0f;
            }
        }
    }
}
