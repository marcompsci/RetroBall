using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// The single mapping between court space (metres, y away from the baseline) and Unity
    /// world space (1 unit = 1 m, hoop at the top of the screen). Height (ball arcs, rim) is
    /// drawn as a screen-up offset scaled by <see cref="HeightScale"/> for the 3/4 view.
    /// </summary>
    public static class CourtSpace
    {
        public const float PixelsPerUnit = CourtGenerator.PixelsPerMeter;
        public const float HeightScale = 0.5f;
        public const float RimHeight = 3.05f;

        public static Vector3 ToWorld(Vec2 p, float height = 0f) => new Vector3(p.x, -p.y + height * HeightScale, 0f);

        /// <summary>World position snapped to the art pixel grid for crisp movement.</summary>
        public static Vector3 ToWorldSnapped(Vec2 p, float height = 0f)
        {
            var w = ToWorld(p, height);
            return new Vector3(Mathf.Round(w.x * PixelsPerUnit) / PixelsPerUnit,
                               Mathf.Round(w.y * PixelsPerUnit) / PixelsPerUnit, 0f);
        }

        /// <summary>Depth sort: players farther from the baseline are lower on screen, so drawn in front.</summary>
        public static int SortingOrder(Vec2 p, int bias = 0) => Mathf.RoundToInt(p.y * 100f) + bias;

        public static Vec2 ToCourt(Vector3 world) => new Vec2(world.x, -world.y);
    }
}
