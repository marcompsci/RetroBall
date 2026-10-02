using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Half-court layout in metres. Court space: x runs sideline to sideline (-W/2..W/2),
    /// y runs from the baseline (0) out to the top of the playing area (Depth). The hoop
    /// sits near the baseline. Proportions follow common 3-on-3 half-court dimensions,
    /// simplified for arcade play; every value is tunable.
    /// </summary>
    [Serializable]
    public class CourtGeometry
    {
        public float width = 15f;
        public float depth = 11f;
        /// <summary>Hoop centre distance from the baseline.</summary>
        public float hoopY = 1.575f;
        public float arcRadius = 6.75f;
        /// <summary>Distance of the straight corner arc lines from the sideline.</summary>
        public float cornerInset = 0.9f;
        public float paintWidth = 4.9f;
        public float paintLength = 5.8f;
        public float freeThrowCircleRadius = 1.8f;
        /// <summary>Rim radius used for "at the rim" checks and art.</summary>
        public float rimRadius = 0.23f;
        /// <summary>Players are kept this far inside the lines.</summary>
        public float boundsInset = 0.3f;

        public static CourtGeometry Default => new CourtGeometry();

        public float HalfWidth => width * 0.5f;
        public Vec2 Hoop => new Vec2(0f, hoopY);
        public float CornerLineX => HalfWidth - cornerInset;

        /// <summary>Height (y) where the straight corner lines meet the arc.</summary>
        public float CornerLineTopY
        {
            get
            {
                float dx = CornerLineX;
                float dy = arcRadius * arcRadius - dx * dx;
                return hoopY + (dy > 0f ? (float)Math.Sqrt(dy) : 0f);
            }
        }

        /// <summary>Top of the arc, straight out from the hoop.</summary>
        public Vec2 ArcTop => new Vec2(0f, hoopY + arcRadius);

        /// <summary>Where the ball is checked after a score or change of possession.</summary>
        public Vec2 CheckSpot => new Vec2(0f, Math.Min(depth - 1.2f, hoopY + arcRadius + 1.3f));

        public bool Contains(Vec2 p) => p.x >= -HalfWidth && p.x <= HalfWidth && p.y >= 0f && p.y <= depth;

        /// <summary>Keeps a point inside the playable area (minus <see cref="boundsInset"/>).</summary>
        public Vec2 Clamp(Vec2 p)
        {
            float minX = -HalfWidth + boundsInset, maxX = HalfWidth - boundsInset;
            float minY = boundsInset, maxY = depth - boundsInset;
            return new Vec2(p.x < minX ? minX : (p.x > maxX ? maxX : p.x),
                            p.y < minY ? minY : (p.y > maxY ? maxY : p.y));
        }

        /// <summary>1-point or 2-point zone for a shot released at <paramref name="p"/>.</summary>
        public ShotZone ZoneOf(Vec2 p)
        {
            if (p.y <= CornerLineTopY) return Math.Abs(p.x) >= CornerLineX ? ShotZone.BeyondArc : ShotZone.InsideArc;
            return Vec2.Distance(p, Hoop) >= arcRadius ? ShotZone.BeyondArc : ShotZone.InsideArc;
        }

        public float DistanceToHoop(Vec2 p) => Vec2.Distance(p, Hoop);

        public bool InPaint(Vec2 p) => Math.Abs(p.x) <= paintWidth * 0.5f && p.y <= paintLength;
    }
}
