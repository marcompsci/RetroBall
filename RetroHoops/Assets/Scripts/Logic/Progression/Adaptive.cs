using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 40 SMART DIFFICULTY for Rise Mode: after a run of heavy losses the other team's shooting eases a little;
    /// after a run of blowouts it sharpens a little. It only ever nudges (at most ±5 points of release accuracy), it
    /// looks at your last three Rise games, and Settings can turn it off. Rival Challenges use it too.
    /// </summary>
    public static class Adaptive
    {
        public const int Remember = 3;
        public const int BigMargin = 6;
        public const float MaxEdge = 0.05f;

        /// <summary>Adds a finished Rise game's margin (yours minus theirs), keeping the last <see cref="Remember"/>.</summary>
        public static void Record(RiseSaveData r, int margin)
        {
            if (r == null) return;
            if (r.recentMargins == null) r.recentMargins = new List<int>();
            r.recentMargins.Add(Math.Max(-99, Math.Min(99, margin)));
            while (r.recentMargins.Count > Remember) r.recentMargins.RemoveAt(0);
        }

        /// <summary>
        /// The nudge for the next game: needs all three of the last games to agree. Three losses by
        /// <see cref="BigMargin"/>+ (or an average 10+ down) ease off; three wins by that much sharpen up.
        /// </summary>
        public static float EdgeFor(List<int> margins)
        {
            if (margins == null || margins.Count < Remember) return 0f;
            int sum = 0;
            bool allBigLosses = true, allBigWins = true;
            foreach (int m in margins)
            {
                sum += m;
                allBigLosses &= m <= -BigMargin;
                allBigWins &= m >= BigMargin;
            }
            float avg = sum / (float)margins.Count;
            if (allBigLosses) return avg <= -10f ? -MaxEdge : -MaxEdge * 0.6f;
            if (allBigWins) return avg >= 10f ? MaxEdge : MaxEdge * 0.6f;
            return 0f;
        }

        public static List<int> Clean(List<int> margins)
        {
            var list = new List<int>();
            if (margins == null) return list;
            int start = Math.Max(0, margins.Count - Remember);
            for (int i = start; i < margins.Count; i++) list.Add(Math.Max(-99, Math.Min(99, margins[i])));
            return list;
        }
    }
}
