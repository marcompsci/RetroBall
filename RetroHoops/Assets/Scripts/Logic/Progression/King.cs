using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class KingSaveData
    {
        /// <summary>Opponent order for the current run (league teams, shuffled).</summary>
        public List<string> order = new List<string>();
        public int index;
        public int streak;
        public int best;
        public bool active;
        public int runs;
        /// <summary>Your team for the current run.</summary>
        public string homeTeamId;
    }

    public enum KingOutcome { None = 0, Defended = 1, Dethroned = 2 }

    /// <summary>
    /// King of the Court: your team takes on league teams one after another in short games
    /// (first to 11 or 90 seconds). Every win extends the streak; the first loss ends the run.
    /// After all eight, the order reshuffles and the run keeps going.
    /// </summary>
    public static class KingEngine
    {
        public const string RulesId = "rules.king";
        public const int BonusPerStreakWin = 20;
        public const int MaxStreakBonus = 200;

        public static void Start(KingSaveData k, ContentCatalog c, string homeTeamId)
        {
            k.runs++;
            k.active = true;
            k.homeTeamId = homeTeamId;
            k.streak = 0;
            k.index = 0;
            Shuffle(k, c, homeTeamId, k.runs);
        }

        private static void Shuffle(KingSaveData k, ContentCatalog c, string homeTeamId, int salt)
        {
            k.order = c.TeamsInTier(TeamTier.League).ConvertAll(t => t.id);
            k.order.Remove(homeTeamId);
            var rng = new SeededRandom(StableHash.Of("king:" + salt));
            for (int i = k.order.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (k.order[i], k.order[j]) = (k.order[j], k.order[i]);
            }
            k.index = 0;
        }

        public static MatchRequest NextMatch(KingSaveData k, ContentCatalog c, string homeTeamId, string difficultyId)
        {
            if (k == null || !k.active || k.order.Count == 0) return null;
            var opp = c.Team(k.order[k.index % k.order.Count]);
            var home = c.Team(homeTeamId);
            if (opp == null || home == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.King,
                HomeTeamId = home.id,
                AwayTeamId = opp.id,
                CourtId = home.homeCourtId,
                RulesId = RulesId,
                DifficultyId = difficultyId,
                ContextId = "king:r" + k.runs + ":g" + k.streak,
            };
        }

        /// <summary>Applies a finished King game. Returns the bonus Signal Points granted.</summary>
        public static KingOutcome ApplyResult(KingSaveData k, ContentCatalog c, MatchSummary s, CareerSaveData career, out int bonus)
        {
            bonus = 0;
            if (k == null || !k.active || s == null || s.mode != GameMode.King) return KingOutcome.None;
            if (!s.HumanWon)
            {
                k.active = false;
                return KingOutcome.Dethroned;
            }
            k.streak++;
            k.best = Math.Max(k.best, k.streak);
            bonus = Math.Min(MaxStreakBonus, BonusPerStreakWin * k.streak);
            if (career != null) career.signalPoints += bonus;
            k.index++;
            if (k.index >= k.order.Count) Shuffle(k, c, s.humanTeam == 0 ? s.teamAId : s.teamBId, k.runs * 100 + k.streak);
            return KingOutcome.Defended;
        }
    }
}
