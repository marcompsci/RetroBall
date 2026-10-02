using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Spacing spots and simple positioning rules. PHASE 2 uses these to place and move
    /// non-controlled players; Phase 3 layers real AI decisions on top.
    /// </summary>
    public static class Formation
    {
        /// <summary>Off-ball spacing spots for teammates 1 and 2 (slot 0 is the ball handler).</summary>
        public static Vec2 OffenseSpot(int slot, CourtGeometry c)
        {
            switch (slot)
            {
                case 0: return c.CheckSpot;
                case 1: return new Vec2(-(c.HalfWidth - 2.3f), c.hoopY + 4.6f); // left wing
                case 2: return new Vec2(c.HalfWidth - 2.3f, c.hoopY + 4.6f);   // right wing
                default: return new Vec2(0f, c.hoopY + 3f);
            }
        }

        /// <summary>
        /// Defensive spot between a matchup and the hoop. Defenders play tighter on the ball
        /// handler and sag toward the paint off the ball.
        /// </summary>
        public static Vec2 GuardSpot(Vec2 man, bool manHasBall, CourtGeometry c)
        {
            float gap = manHasBall ? 1.0f : 1.7f;
            var toHoop = c.Hoop - man;
            float dist = toHoop.Magnitude;
            if (dist < 1e-4f) return c.Clamp(man);
            // Never stand behind the hoop or beyond the man.
            float g = Math.Min(gap, dist * 0.7f);
            return c.Clamp(man + toHoop / dist * g);
        }
    }
}
