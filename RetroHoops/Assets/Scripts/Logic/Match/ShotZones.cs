using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Nine shot-location buckets for half-court tracking.</summary>
    public enum ShotSpot
    {
        Paint = 0,       // inside PaintDistance from the rim: layups and dunks
        FoulLine = 1,    // mid-range, within the centre band
        ElbowLeft = 2,   // mid-range, left of centre
        ElbowRight = 3,  // mid-range, right of centre
        CornerLeft = 4,  // corner three, left (straight corner-line area)
        CornerRight = 5, // corner three, right
        WingLeft = 6,    // above-break three, left
        WingRight = 7,   // above-break three, right
        TopOfKey = 8,    // above-break three, centre
    }

    [Serializable]
    public class ShotSpotRecord
    {
        public int made;
        public int attempted;
        public float Percentage => attempted == 0 ? 0f : (float)made / attempted;
    }

    /// <summary>Per-spot make/attempt counts for one player or a collection of games.</summary>
    [Serializable]
    public class ShotChartData
    {
        public ShotSpotRecord[] spots;

        public ShotChartData()
        {
            spots = new ShotSpotRecord[ShotZones.SpotCount];
            for (int i = 0; i < ShotZones.SpotCount; i++) spots[i] = new ShotSpotRecord();
        }

        public ShotSpotRecord this[ShotSpot spot] => spots[(int)spot];
        public ShotSpotRecord this[int i] => spots[i];
    }

    /// <summary>
    /// Shot-zone classification and per-spot tracking. Each shot is mapped to one of
    /// nine spots on the half-court (paint, four mid-range buckets, four arc buckets)
    /// so the game can show a hot-zone chart and generate archetype-aware opponent shot
    /// tendencies without any Unity dependency.
    /// </summary>
    public static class ShotZones
    {
        public const int SpotCount = 9;

        /// <summary>Inside this distance from the rim the shot is filed under Paint.</summary>
        public const float PaintDistance = 2.5f;

        /// <summary>Mid-range shots whose |x| is at most this are FoulLine; wider shots are Elbow left/right.</summary>
        public const float MidCentreHalfWidth = 1.8f;

        /// <summary>Above-break arc shots whose |x| is at most this are TopOfKey; wider shots are Wing left/right.</summary>
        public const float TopKeyHalfWidth = 2.5f;

        /// <summary>Minimum attempts needed before a spot is considered for hot/cold-spot selection.</summary>
        public const int MinAttempts = 3;

        /// <summary>
        /// Maps a shooter's court position to one of the nine <see cref="ShotSpot"/> buckets.
        /// Corners are defined by the court's straight corner lines (same geometry as
        /// <see cref="CourtGeometry.ZoneOf"/>); everything else is divided by x-distance.
        /// </summary>
        public static ShotSpot SpotOf(Vec2 position, CourtGeometry court)
        {
            if (court.DistanceToHoop(position) <= PaintDistance) return ShotSpot.Paint;

            bool beyond = court.ZoneOf(position) == ShotZone.BeyondArc;
            if (!beyond)
            {
                if (Math.Abs(position.x) <= MidCentreHalfWidth) return ShotSpot.FoulLine;
                return position.x < 0f ? ShotSpot.ElbowLeft : ShotSpot.ElbowRight;
            }

            // Corner three: still in the straight corner-line section.
            if (position.y <= court.CornerLineTopY)
                return position.x < 0f ? ShotSpot.CornerLeft : ShotSpot.CornerRight;

            if (Math.Abs(position.x) <= TopKeyHalfWidth) return ShotSpot.TopOfKey;
            return position.x < 0f ? ShotSpot.WingLeft : ShotSpot.WingRight;
        }

        public static void Record(ShotChartData d, ShotSpot spot, bool made)
        {
            d.spots[(int)spot].attempted++;
            if (made) d.spots[(int)spot].made++;
        }

        /// <summary>
        /// The spot with the highest make percentage (minimum <paramref name="minAttempts"/> required),
        /// or null when no spot has enough data.
        /// </summary>
        public static ShotSpot? HotSpot(ShotChartData d, int minAttempts = MinAttempts)
        {
            ShotSpot? best = null;
            float bestPct = -1f;
            for (int i = 0; i < SpotCount; i++)
            {
                var r = d.spots[i];
                if (r.attempted < minAttempts || r.Percentage <= bestPct) continue;
                bestPct = r.Percentage;
                best = (ShotSpot)i;
            }
            return best;
        }

        /// <summary>
        /// The spot with the lowest make percentage (minimum <paramref name="minAttempts"/> required),
        /// or null when no spot has enough data.
        /// </summary>
        public static ShotSpot? ColdSpot(ShotChartData d, int minAttempts = MinAttempts)
        {
            ShotSpot? worst = null;
            float worstPct = 2f;
            for (int i = 0; i < SpotCount; i++)
            {
                var r = d.spots[i];
                if (r.attempted < minAttempts || r.Percentage >= worstPct) continue;
                worstPct = r.Percentage;
                worst = (ShotSpot)i;
            }
            return worst;
        }

        /// <summary>Folds <paramref name="from"/> into <paramref name="into"/> for multi-game or season roll-ups.</summary>
        public static void Merge(ShotChartData into, ShotChartData from)
        {
            if (into == null || from == null) return;
            for (int i = 0; i < SpotCount; i++)
            {
                into.spots[i].made += from.spots[i].made;
                into.spots[i].attempted += from.spots[i].attempted;
            }
        }

        /// <summary>
        /// The spots each archetype gravitates toward in open play. Used to generate
        /// opponent shot-tendency hints without needing game history.
        /// </summary>
        public static ShotSpot[] PreferredSpots(Archetype arch)
        {
            switch (arch)
            {
                case Archetype.FloorGeneral:   return new[] { ShotSpot.FoulLine, ShotSpot.TopOfKey };
                case Archetype.DeepShooter:    return new[] { ShotSpot.TopOfKey, ShotSpot.WingLeft, ShotSpot.WingRight };
                case Archetype.RimRunner:      return new[] { ShotSpot.Paint };
                case Archetype.LockdownWing:   return new[] { ShotSpot.WingLeft, ShotSpot.WingRight };
                case Archetype.GlassCleaner:   return new[] { ShotSpot.Paint, ShotSpot.ElbowLeft, ShotSpot.ElbowRight };
                case Archetype.TwoWaySpark:    return new[] { ShotSpot.WingLeft, ShotSpot.WingRight, ShotSpot.Paint };
                case Archetype.PostAnchor:     return new[] { ShotSpot.Paint, ShotSpot.ElbowLeft, ShotSpot.ElbowRight };
                case Archetype.QuickCutter:    return new[] { ShotSpot.Paint, ShotSpot.CornerLeft, ShotSpot.CornerRight };
                case Archetype.Playmaker:      return new[] { ShotSpot.FoulLine, ShotSpot.TopOfKey };
                case Archetype.ShotCreator:    return new[] { ShotSpot.FoulLine, ShotSpot.ElbowLeft, ShotSpot.ElbowRight };
                case Archetype.HustleGuard:    return new[] { ShotSpot.WingLeft, ShotSpot.WingRight, ShotSpot.Paint };
                case Archetype.StretchForward: return new[] { ShotSpot.CornerLeft, ShotSpot.CornerRight, ShotSpot.WingLeft, ShotSpot.WingRight };
                default:                       return new[] { ShotSpot.Paint };
            }
        }

        public static string SpotName(ShotSpot spot)
        {
            switch (spot)
            {
                case ShotSpot.Paint:       return "PAINT";
                case ShotSpot.FoulLine:    return "FOUL LINE";
                case ShotSpot.ElbowLeft:   return "ELBOW LEFT";
                case ShotSpot.ElbowRight:  return "ELBOW RIGHT";
                case ShotSpot.CornerLeft:  return "CORNER LEFT";
                case ShotSpot.CornerRight: return "CORNER RIGHT";
                case ShotSpot.WingLeft:    return "WING LEFT";
                case ShotSpot.WingRight:   return "WING RIGHT";
                case ShotSpot.TopOfKey:    return "TOP OF KEY";
                default:                   return "UNKNOWN";
            }
        }
    }
}
