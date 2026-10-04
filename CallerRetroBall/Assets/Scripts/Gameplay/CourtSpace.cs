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

        /// <summary>
        /// Full Court: the simulation runs in the attacking team's frame; while team 1 attacks, that frame
        /// is the court turned 180°. The match controller sets this from <see cref="MatchSimulation.Flipped"/>
        /// so every view draws on the fixed court. Always false in the half-court game and during replays.
        /// </summary>
        public static bool Flip;
        /// <summary>Full Court length (m), used with <see cref="Flip"/>.</summary>
        public static float FlipLength;

        private static Vec2 Fix(Vec2 p) => Flip ? new Vec2(-p.x, FlipLength - p.y) : p;

        /// <summary>A facing as drawn (turned 180° with <see cref="Flip"/>).</summary>
        public static Facing8 Facing(Facing8 f) => Flip ? FullCourt.Turn(f) : f;

        public static Vector3 ToWorld(Vec2 p, float height = 0f)
        {
            p = Fix(p);
            return new Vector3(p.x, -p.y + height * HeightScale, 0f);
        }

        /// <summary>World position snapped to the art pixel grid for crisp movement.</summary>
        public static Vector3 ToWorldSnapped(Vec2 p, float height = 0f)
        {
            var w = ToWorld(p, height);
            return new Vector3(Mathf.Round(w.x * PixelsPerUnit) / PixelsPerUnit,
                               Mathf.Round(w.y * PixelsPerUnit) / PixelsPerUnit, 0f);
        }

        /// <summary>Depth sort: players farther from the baseline are lower on screen, so drawn in front.</summary>
        public static int SortingOrder(Vec2 p, int bias = 0) => Mathf.RoundToInt(Fix(p).y * 100f) + bias;

        public static Vec2 ToCourt(Vector3 world) => new Vec2(world.x, -world.y);
    }
}
