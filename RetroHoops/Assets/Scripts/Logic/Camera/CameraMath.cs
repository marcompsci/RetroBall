using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Pure camera maths: smoothing, court clamping, and crisp integer pixel zoom.</summary>
    public static class CameraMath
    {
        /// <summary>Frame-rate independent exponential follow.</summary>
        public static Vec2 Follow(Vec2 current, Vec2 target, float dt, float stiffness)
        {
            if (dt <= 0f) return current;
            float t = 1f - (float)Math.Exp(-stiffness * dt);
            return Vec2.Lerp(current, target, t);
        }

        /// <summary>
        /// Clamps a view centre so the view stays within [min, max] (+margin). If the view is
        /// larger than the area on an axis, it centres on that axis instead.
        /// </summary>
        public static Vec2 ClampView(Vec2 center, float halfViewWidth, float halfViewHeight,
                                     Vec2 areaMin, Vec2 areaMax, float margin = 0f)
        {
            return new Vec2(ClampAxis(center.x, halfViewWidth, areaMin.x - margin, areaMax.x + margin),
                            ClampAxis(center.y, halfViewHeight, areaMin.y - margin, areaMax.y + margin));
        }

        private static float ClampAxis(float c, float half, float min, float max)
        {
            if (half * 2f >= max - min) return (min + max) * 0.5f;
            if (c - half < min) return min + half;
            if (c + half > max) return max - half;
            return c;
        }

        /// <summary>
        /// Largest whole-number scale at which <paramref name="minVisibleUnits"/> world units
        /// (at <paramref name="pixelsPerUnit"/> art pixels each) still fit across the screen.
        /// Keeps pixel art crisp: every art pixel maps to N×N screen pixels.
        /// </summary>
        public static int IntegerZoom(int screenPixels, float minVisibleUnits, float pixelsPerUnit)
        {
            if (screenPixels <= 0 || minVisibleUnits <= 0f || pixelsPerUnit <= 0f) return 1;
            int zoom = (int)Math.Floor(screenPixels / (minVisibleUnits * pixelsPerUnit));
            return Math.Max(1, zoom);
        }

        /// <summary>Orthographic size (half height in world units) for a given integer zoom.</summary>
        public static float OrthographicSize(int screenHeightPixels, int zoom, float pixelsPerUnit)
        {
            return screenHeightPixels / (2f * zoom * pixelsPerUnit);
        }
    }
}
