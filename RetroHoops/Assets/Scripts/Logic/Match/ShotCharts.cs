using System;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>How a player is shooting from one spot compared with what a spot like that usually gives up.</summary>
    public enum SpotHeat
    {
        /// <summary>Not enough shots from there to say.</summary>
        Unknown = 0,
        Cold = 1,
        Neutral = 2,
        Hot = 3,
    }

    /// <summary>
    /// Phase 35 SHOT CHARTS, built on <see cref="ShotZones"/>: every shot in a game is filed under one of nine spots
    /// (<see cref="PlayerStatLine.chart"/>). The post-game screen draws the chart, your career, Franchise team and
    /// Legacy player keep running charts, and the AI leans toward the spots it is hot from during a game (and away
    /// from the cold ones) without any extra random draws, so seeded and two-phone games stay in step.
    /// </summary>
    public static class ShotCharts
    {
        /// <summary>Shots from a spot before it can be called hot or cold.</summary>
        public const int HeatMinAttempts = 3;
        /// <summary>How far above / below a spot's usual make rate counts as hot / cold.</summary>
        public const float HotMargin = 0.12f, ColdMargin = 0.15f;
        /// <summary>How much a hot (or cold) spot moves the AI's wish to shoot from there.</summary>
        public const float AiHotBias = 0.15f, AiColdBias = 0.15f;

        /// <summary>The make rate a spot usually gives up (rim, mid-range, deep).</summary>
        public static float Baseline(ShotSpot s) => s == ShotSpot.Paint ? 0.55f : IsDeep(s) ? 0.34f : 0.42f;

        /// <summary>Corner, wing and top-of-the-key spots are beyond the arc.</summary>
        public static bool IsDeep(ShotSpot s) => s >= ShotSpot.CornerLeft;

        public static SpotHeat Heat(ShotSpotRecord r, ShotSpot s, int minAttempts = HeatMinAttempts)
        {
            if (r == null || r.attempted < Math.Max(1, minAttempts)) return SpotHeat.Unknown;
            float d = r.Percentage - Baseline(s);
            if (d >= HotMargin) return SpotHeat.Hot;
            if (d <= -ColdMargin) return SpotHeat.Cold;
            return SpotHeat.Neutral;
        }

        public static SpotHeat Heat(ShotChartData d, ShotSpot s, int minAttempts = HeatMinAttempts) =>
            d == null ? SpotHeat.Unknown : Heat(d[s], s, minAttempts);

        public static int Attempts(ShotChartData d)
        {
            int n = 0;
            if (d != null) for (int i = 0; i < ShotZones.SpotCount; i++) n += d.spots[i].attempted;
            return n;
        }

        public static int Made(ShotChartData d)
        {
            int n = 0;
            if (d != null) for (int i = 0; i < ShotZones.SpotCount; i++) n += d.spots[i].made;
            return n;
        }

        public static ShotChartData Copy(ShotChartData d)
        {
            var c = new ShotChartData();
            ShotZones.Merge(c, d);
            return c;
        }

        public static void Clear(ShotChartData d)
        {
            if (d == null) return;
            for (int i = 0; i < ShotZones.SpotCount; i++) { d.spots[i].made = 0; d.spots[i].attempted = 0; }
        }

        /// <summary>Saved as "made/attempted" for the nine spots, separated by ';' ("" when empty).</summary>
        public static string Encode(ShotChartData d)
        {
            if (d == null || Attempts(d) == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < ShotZones.SpotCount; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(d.spots[i].made).Append('/').Append(d.spots[i].attempted);
            }
            return sb.ToString();
        }

        /// <summary>Reads <see cref="Encode"/>'s text. Anything damaged or out of range reads as zero.</summary>
        public static ShotChartData Decode(string s)
        {
            var d = new ShotChartData();
            if (string.IsNullOrEmpty(s)) return d;
            var parts = s.Split(';');
            for (int i = 0; i < ShotZones.SpotCount && i < parts.Length; i++)
            {
                var ma = parts[i].Split('/');
                if (ma.Length != 2 || !int.TryParse(ma[0], out int made) || !int.TryParse(ma[1], out int att)) continue;
                att = Math.Max(0, Math.Min(SaveIntegrity.MaxCount, att));
                made = Math.Max(0, Math.Min(att, made));
                d.spots[i].attempted = att;
                d.spots[i].made = made;
            }
            return d;
        }

        /// <summary>A point on the court inside <paramref name="s"/> (where the AI goes to get that shot, and where the chart draws it).</summary>
        public static Vec2 CourtPoint(ShotSpot s, CourtGeometry c)
        {
            float h = c.hoopY;
            float deep = c.arcRadius + 0.7f;
            const float diag = 0.7071f;
            switch (s)
            {
                case ShotSpot.Paint: return new Vec2(0f, h + 1.2f);
                case ShotSpot.FoulLine: return new Vec2(0f, h + Math.Min(4.6f, c.arcRadius - 1.2f));
                case ShotSpot.ElbowLeft: return new Vec2(-3.2f, h + 3.2f);
                case ShotSpot.ElbowRight: return new Vec2(3.2f, h + 3.2f);
                case ShotSpot.CornerLeft: return new Vec2(-Math.Min(c.HalfWidth - c.boundsInset - 0.1f, c.CornerLineX + 0.4f), Math.Max(0.6f, c.CornerLineTopY * 0.5f));
                case ShotSpot.CornerRight: return new Vec2(Math.Min(c.HalfWidth - c.boundsInset - 0.1f, c.CornerLineX + 0.4f), Math.Max(0.6f, c.CornerLineTopY * 0.5f));
                case ShotSpot.WingLeft: return new Vec2(-deep * diag, h + deep * diag);
                case ShotSpot.WingRight: return new Vec2(deep * diag, h + deep * diag);
                default: return new Vec2(0f, Math.Min(c.depth - c.boundsInset - 0.2f, h + deep));
            }
        }

        /// <summary>"WING LEFT 5/7 (71%)".</summary>
        public static string SpotLine(ShotChartData d, ShotSpot s)
        {
            var r = d[s];
            return ShotZones.SpotName(s) + " " + r.made + "/" + r.attempted + (r.attempted > 0 ? " (" + (int)Math.Round(r.Percentage * 100f) + "%)" : "");
        }

        /// <summary>One line for under the chart: the hottest and coldest spots, or why there aren't any yet.</summary>
        public static string Summary(ShotChartData d)
        {
            if (d == null || Attempts(d) == 0) return "NO SHOTS YET";
            ShotSpot? hot = null, cold = null;
            float hotBy = 0f, coldBy = 0f;
            for (int i = 0; i < ShotZones.SpotCount; i++)
            {
                var s = (ShotSpot)i;
                var heat = Heat(d, s);
                float by = d.spots[i].Percentage - Baseline(s);
                if (heat == SpotHeat.Hot && (hot == null || by > hotBy)) { hot = s; hotBy = by; }
                if (heat == SpotHeat.Cold && (cold == null || by < coldBy)) { cold = s; coldBy = by; }
            }
            if (hot == null && cold == null)
                return Made(d) + "/" + Attempts(d) + " FROM THE FLOOR · NO HOT OR COLD SPOTS YET";
            string line = "";
            if (hot != null) line += "HOT: " + SpotLine(d, hot.Value);
            if (cold != null) line += (line.Length > 0 ? "   " : "") + "COLD: " + SpotLine(d, cold.Value);
            return line;
        }

        /// <summary>
        /// What the AI adds to its wish to shoot from <paramref name="s"/>: more where it's hot this game, less where it's
        /// cold. Pure arithmetic on the box score (no random draw).
        /// </summary>
        public static float AiShootBias(ShotChartData d, ShotSpot s)
        {
            switch (Heat(d, s))
            {
                case SpotHeat.Hot: return AiHotBias;
                case SpotHeat.Cold: return -AiColdBias;
                default: return 0f;
            }
        }

        /// <summary>
        /// The jump-shot spot a player should drift to off the ball: his hottest spot outside the paint, or null when
        /// none is hot yet (the paint is left to cutters and bigs).
        /// </summary>
        public static ShotSpot? HuntSpot(ShotChartData d)
        {
            if (d == null) return null;
            ShotSpot? best = null;
            float bestBy = 0f;
            for (int i = 1; i < ShotZones.SpotCount; i++)
            {
                var s = (ShotSpot)i;
                if (Heat(d, s) != SpotHeat.Hot) continue;
                float by = d.spots[i].Percentage - Baseline(s);
                if (best == null || by > bestBy) { best = s; bestBy = by; }
            }
            return best;
        }
    }
}
