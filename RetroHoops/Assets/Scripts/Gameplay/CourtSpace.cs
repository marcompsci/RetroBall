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

        /// <summary>
        /// Full Court on a landscape screen: the court is turned a quarter turn so the baskets sit at the
        /// left and right (court y runs left → right, court x runs up the screen). Heights still draw
        /// screen-up. <see cref="LandscapeLength"/> is the court length (its middle is world x = 0).
        /// </summary>
        public static bool Landscape;
        public static float LandscapeLength;

        /// <summary>A facing as drawn (turned 180° with <see cref="Flip"/>, a quarter turn in landscape).</summary>
        public static Facing8 Facing(Facing8 f)
        {
            if (Flip) f = FullCourt.Turn(f);
            return Landscape ? LandscapeMath.TurnFacing(f) : f;
        }

        public static Vector3 ToWorld(Vec2 p, float height = 0f)
        {
            p = Fix(p);
            var w = LandscapeMath.ToScreenPlane(p, Landscape, LandscapeLength);
            return new Vector3(w.x, w.y + height * HeightScale, 0f);
        }

        /// <summary>A stick or key direction on screen (+y up) as a court direction for this view.</summary>
        public static Vec2 ScreenToCourt(Vec2 screen) => LandscapeMath.ScreenToCourt(screen, Landscape);

        /// <summary>World position snapped to the art pixel grid for crisp movement.</summary>
        public static Vector3 ToWorldSnapped(Vec2 p, float height = 0f)
        {
            var w = ToWorld(p, height);
            return new Vector3(Mathf.Round(w.x * PixelsPerUnit) / PixelsPerUnit,
                               Mathf.Round(w.y * PixelsPerUnit) / PixelsPerUnit, 0f);
        }

        /// <summary>Depth sort: players farther from the baseline are lower on screen, so drawn in front.</summary>
        public static int SortingOrder(Vec2 p, int bias = 0) => Mathf.RoundToInt(LandscapeMath.Depth(Fix(p), Landscape) * 100f) + bias;

        public static Vec2 ToCourt(Vector3 world) => LandscapeMath.FromScreenPlane(new Vec2(world.x, world.y), Landscape, LandscapeLength);
    }
}
