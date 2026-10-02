using System;

namespace CallerRetroBall.Logic
{
    public enum ShotZone
    {
        /// <summary>Layups, dunks, and anything inside the arc.</summary>
        InsideArc = 0,
        /// <summary>Beyond the marked arc.</summary>
        BeyondArc = 1,
    }

    public enum GameOverReason { None = 0, TargetScore = 1, ClockExpired = 2 }

    /// <summary>Pure half-court scoring and end-of-game rules.</summary>
    public static class Scoring
    {
        public static int PointsFor(ShotZone zone, GameRulesDef rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            return zone == ShotZone.BeyondArc ? rules.beyondArcPoints : rules.insideArcPoints;
        }

        /// <summary>
        /// Decides whether the game is over. <paramref name="clockRemaining"/> is ignored
        /// when the rules do not use a game clock.
        /// </summary>
        public static GameOverReason Evaluate(int scoreA, int scoreB, float clockRemaining, GameRulesDef rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            int high = Math.Max(scoreA, scoreB);
            int margin = Math.Abs(scoreA - scoreB);

            if (high >= rules.targetScore && (!rules.winByTwo || margin >= 2))
                return GameOverReason.TargetScore;

            if (rules.useGameClock && clockRemaining <= 0f)
            {
                // Tied at the horn with sudden death: keep playing until someone scores.
                if (margin == 0 && rules.suddenDeathOnTie) return GameOverReason.None;
                return GameOverReason.ClockExpired;
            }

            return GameOverReason.None;
        }

        /// <summary>0 = team A wins, 1 = team B wins, -1 = tie (only possible without sudden death).</summary>
        public static int Winner(int scoreA, int scoreB)
        {
            if (scoreA == scoreB) return -1;
            return scoreA > scoreB ? 0 : 1;
        }
    }
}
