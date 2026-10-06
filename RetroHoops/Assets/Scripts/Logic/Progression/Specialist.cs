using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>The five areas a SPOT SPECIALIST rank is earned in (each covers one or more of the nine chart spots).</summary>
    public enum SpotGroup { Rim = 0, Mid = 1, Corner = 2, Wing = 3, Top = 4 }

    public enum SpecialistTier { None = 0, Bronze = 1, Silver = 2, Gold = 3 }

    /// <summary>
    /// Phase 36 SPOT SPECIALIST: your career shot chart earns ranks in five areas (rim, mid-range, corners, wings, top of the
    /// key). A rank needs both volume (makes) and accuracy (make rate above what that area usually gives up), so it can't be
    /// farmed with bad shots. Each rank adds a little to your own make chance from that area (+2 / +4 / +6 points) in
    /// single-player games. Two-phone, Live and watched games ignore it (both phones must play the same game), and so do
    /// practice and the tutorial.
    /// </summary>
    public static class Specialist
    {
        public const int Groups = 5;
        /// <summary>Makes needed for Bronze, Silver and Gold (index = tier).</summary>
        public static readonly int[] MakesFor = { 0, 20, 60, 150 };
        /// <summary>How far above the area's usual make rate you must be, per tier.</summary>
        public static readonly float[] AboveUsual = { 0f, 0f, 0.05f, 0.10f };
        /// <summary>Make-chance bonus per tier.</summary>
        public static readonly float[] Bonus = { 0f, 0.02f, 0.04f, 0.06f };

        public static SpotGroup GroupOf(ShotSpot s)
        {
            switch (s)
            {
                case ShotSpot.Paint: return SpotGroup.Rim;
                case ShotSpot.FoulLine: case ShotSpot.ElbowLeft: case ShotSpot.ElbowRight: return SpotGroup.Mid;
                case ShotSpot.CornerLeft: case ShotSpot.CornerRight: return SpotGroup.Corner;
                case ShotSpot.WingLeft: case ShotSpot.WingRight: return SpotGroup.Wing;
                default: return SpotGroup.Top;
            }
        }

        public static string Name(SpotGroup g)
        {
            switch (g)
            {
                case SpotGroup.Rim: return "RIM";
                case SpotGroup.Mid: return "MID-RANGE";
                case SpotGroup.Corner: return "CORNER";
                case SpotGroup.Wing: return "WING";
                default: return "TOP OF THE KEY";
            }
        }

        public static string TierName(SpecialistTier t) => t == SpecialistTier.None ? "—" : t.ToString().ToUpperInvariant();

        /// <summary>The usual make rate for an area (the same baselines the chart's hot/cold colours use).</summary>
        public static float Usual(SpotGroup g) =>
            g == SpotGroup.Rim ? ShotCharts.Baseline(ShotSpot.Paint) : g == SpotGroup.Mid ? ShotCharts.Baseline(ShotSpot.FoulLine) : ShotCharts.Baseline(ShotSpot.TopOfKey);

        public static void Totals(ShotChartData d, SpotGroup g, out int made, out int attempted)
        {
            made = attempted = 0;
            if (d == null) return;
            for (int i = 0; i < ShotZones.SpotCount; i++)
            {
                if (GroupOf((ShotSpot)i) != g) continue;
                made += d.spots[i].made;
                attempted += d.spots[i].attempted;
            }
        }

        public static SpecialistTier TierOf(ShotChartData d, SpotGroup g)
        {
            Totals(d, g, out int made, out int att);
            if (att <= 0) return SpecialistTier.None;
            float pct = made / (float)att;
            for (int t = 3; t >= 1; t--)
                if (made >= MakesFor[t] && pct >= Usual(g) + AboveUsual[t] - 1e-4f) return (SpecialistTier)t;
            return SpecialistTier.None;
        }

        /// <summary>Make-chance bonus for each of the nine spots, from your career chart.</summary>
        public static float[] SpotBonuses(ShotChartData d)
        {
            var b = new float[ShotZones.SpotCount];
            var tiers = new SpecialistTier[Groups];
            for (int g = 0; g < Groups; g++) tiers[g] = TierOf(d, (SpotGroup)g);
            for (int i = 0; i < ShotZones.SpotCount; i++) b[i] = Bonus[(int)tiers[(int)GroupOf((ShotSpot)i)]];
            return b;
        }

        /// <summary>Ranks reached by adding a game's chart (for the post-game line): "CORNER SPECIALIST: SILVER".</summary>
        public static List<string> NewRanks(ShotChartData before, ShotChartData after)
        {
            var list = new List<string>();
            for (int g = 0; g < Groups; g++)
            {
                var was = TierOf(before, (SpotGroup)g);
                var now = TierOf(after, (SpotGroup)g);
                if (now > was) list.Add(Name((SpotGroup)g) + " SPECIALIST: " + TierName(now));
            }
            return list;
        }

        /// <summary>The Locker Room line for an area: rank, record, and what the next rank needs.</summary>
        public static string Line(ShotChartData d, SpotGroup g)
        {
            Totals(d, g, out int made, out int att);
            var tier = TierOf(d, g);
            string pct = att > 0 ? (int)Math.Round(made * 100f / att) + "%" : "-";
            string next = "";
            if (tier < SpecialistTier.Gold)
            {
                int t = (int)tier + 1;
                next = "  ·  NEXT: " + TierName((SpecialistTier)t) + " at " + MakesFor[t] + " makes, " + (int)Math.Round((Usual(g) + AboveUsual[t]) * 100f) + "%+";
            }
            return Name(g) + ": " + TierName(tier) + "  (" + made + "/" + att + ", " + pct + ")" + next;
        }

        /// <summary>Whether a game may use your ranks: single-player games that count, never shared or watched ones.</summary>
        public static bool AppliesTo(MatchRequest r) =>
            r != null && r.ContextId != "link" && r.Mode != GameMode.Versus && r.Mode != GameMode.Demo
            && r.Mode != GameMode.Practice && r.Mode != GameMode.Tutorial && !r.Coach;
    }
}
