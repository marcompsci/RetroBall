using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum DailyGoal
    {
        WinBy = 0,
        Greens = 1,
        Steals = 2,
        Points = 3,
        Assists = 4,
        WinOnLegend = 5,
    }

    /// <summary>One day's challenge. Fully determined by the day number, so it's the same for everyone and works offline.</summary>
    public sealed class DailyChallenge
    {
        /// <summary>Days since 2000-01-01 (local calendar day).</summary>
        public int Day;
        public DailyGoal Goal;
        public int Target;
        public string HomeTeamId;
        public string OpponentId;
        public string CourtId;
        public string DifficultyId;

        public string Describe()
        {
            switch (Goal)
            {
                case DailyGoal.WinBy: return "Win by " + Target + " or more";
                case DailyGoal.Greens: return "Hit " + Target + " GREEN releases";
                case DailyGoal.Steals: return "Get " + Target + " steals";
                case DailyGoal.Points: return "Score " + Target + " points yourself";
                case DailyGoal.Assists: return "Dish " + Target + " assists";
                default: return "Win on Legend";
            }
        }

        public MatchRequest ToRequest() => new MatchRequest
        {
            Mode = GameMode.Daily,
            HomeTeamId = HomeTeamId,
            AwayTeamId = OpponentId,
            CourtId = CourtId,
            DifficultyId = DifficultyId,
            ContextId = "daily:" + Day,
        };
    }

    [Serializable]
    public class DailySaveData
    {
        /// <summary>Day number of the last completed challenge (-1 = never).</summary>
        public int lastCompletedDay = -1;
        public int streak;
        public int bestStreak;
        public int completed;
    }

    public static class DailyChallenges
    {
        public const int BaseBonus = 75;
        public const int BonusPerStreakDay = 15;
        public const int MaxStreakBonusDays = 10;

        private static readonly DateTime Epoch = new DateTime(2000, 1, 1);

        public static int DayNumber(DateTime localDate) => (int)(localDate.Date - Epoch).TotalDays;

        public static DailyChallenge For(int day, ContentCatalog c)
        {
            var rng = new SeededRandom(StableHash.Of("daily:" + day));
            var league = c.TeamsInTier(TeamTier.League);
            var mine = league.FindAll(t => t.unlockedByDefault);
            var home = mine.Count > 0 ? mine[rng.Range(0, mine.Count)] : league[0];
            var opponents = league.FindAll(t => t.id != home.id);
            var opp = opponents[rng.Range(0, opponents.Count)];

            var goal = (DailyGoal)rng.Range(0, 6);
            int target;
            switch (goal)
            {
                case DailyGoal.WinBy: target = 5 + rng.Range(0, 4); break;      // 5..8
                case DailyGoal.Greens: target = 2 + rng.Range(0, 3); break;     // 2..4
                case DailyGoal.Steals: target = 2 + rng.Range(0, 2); break;     // 2..3
                case DailyGoal.Points: target = 8 + rng.Range(0, 5); break;     // 8..12
                case DailyGoal.Assists: target = 3 + rng.Range(0, 3); break;    // 3..5
                default: target = 1; break;
            }
            string difficulty = goal == DailyGoal.WinOnLegend ? "difficulty.legend" : "difficulty.caller";
            if (c.Difficulty(difficulty) == null) difficulty = DefaultContent.DefaultDifficultyId;
            return new DailyChallenge
            {
                Day = day,
                Goal = goal,
                Target = target,
                HomeTeamId = home.id,
                OpponentId = opp.id,
                CourtId = home.homeCourtId,
                DifficultyId = difficulty,
            };
        }

        /// <summary>Did this finished game meet the challenge?</summary>
        public static bool IsMet(DailyChallenge d, MatchSummary s)
        {
            if (d == null || s == null) return false;
            var line = s.HumanLine?.stats ?? new PlayerStatLine();
            switch (d.Goal)
            {
                case DailyGoal.WinBy: return s.HumanWon && s.Margin >= d.Target;
                case DailyGoal.Greens: return line.greenReleases >= d.Target;
                case DailyGoal.Steals: return line.steals >= d.Target;
                case DailyGoal.Points: return line.points >= d.Target;
                case DailyGoal.Assists: return line.assists >= d.Target;
                default: return s.HumanWon;
            }
        }

        public static bool CompletedToday(DailySaveData save, int day) => save != null && save.lastCompletedDay == day;

        public static int BonusFor(int streak) => BaseBonus + BonusPerStreakDay * Math.Min(Math.Max(0, streak - 1), MaxStreakBonusDays);

        /// <summary>
        /// Marks today's challenge done (once per day). Consecutive days build the streak; a missed
        /// day resets it to 1. Returns the Signal Point bonus granted (0 if already done today).
        /// </summary>
        public static int Complete(DailySaveData save, CareerSaveData career, int day)
        {
            if (save == null || CompletedToday(save, day)) return 0;
            save.streak = save.lastCompletedDay == day - 1 ? save.streak + 1 : 1;
            save.bestStreak = Math.Max(save.bestStreak, save.streak);
            save.lastCompletedDay = day;
            save.completed++;
            int bonus = BonusFor(save.streak);
            if (career != null) career.signalPoints += bonus;
            return bonus;
        }

        /// <summary>Streak shown on the menu: it lapses if yesterday was missed.</summary>
        public static int LiveStreak(DailySaveData save, int today) =>
            save == null ? 0 : (save.lastCompletedDay >= today - 1 ? save.streak : 0);
    }

    /// <summary>Game Center achievement and leaderboard ids, and what has been earned (pure, testable).</summary>
    public static class Achievements
    {
        public const string FirstWin = "retroball.ach.first_win";
        public const string FirstGreen = "retroball.ach.first_green";
        public const string TenWins = "retroball.ach.ten_wins";
        public const string HundredGreens = "retroball.ach.hundred_greens";
        public const string CircuitCleared = "retroball.ach.circuit_cleared";
        public const string CupChampion = "retroball.ach.cup_champion";
        public const string ClassicChampion = "retroball.ach.classic_champion";
        public const string DailyWeek = "retroball.ach.daily_streak_7";
        public const string TutorialDone = "retroball.ach.tutorial_done";

        public const string BoardWins = "retroball.lb.career_wins";
        public const string BoardGreens = "retroball.lb.career_greens";
        public const string BoardDailyStreak = "retroball.lb.daily_best_streak";

        public static readonly string[] AllAchievements =
        {
            FirstWin, FirstGreen, TenWins, HundredGreens, CircuitCleared, CupChampion, ClassicChampion, DailyWeek, TutorialDone,
        };

        public static List<string> Earned(CareerSaveData d)
        {
            var list = new List<string>();
            if (d == null) return list;
            var t = d.totals;
            if (t.wins >= 1) list.Add(FirstWin);
            if (t.greens >= 1) list.Add(FirstGreen);
            if (t.wins >= 10) list.Add(TenWins);
            if (t.greens >= 100) list.Add(HundredGreens);
            if (d.rise != null && (d.rise.stage != RiseStage.Circuit || d.rise.seasonsPlayed > 0)) list.Add(CircuitCleared);
            if (t.championships > 0) list.Add(CupChampion);
            if (d.classic != null && d.classic.titles > 0) list.Add(ClassicChampion);
            if (d.daily != null && d.daily.bestStreak >= 7) list.Add(DailyWeek);
            if (d.tutorialDone) list.Add(TutorialDone);
            return list;
        }

        public static List<KeyValuePair<string, long>> Scores(CareerSaveData d)
        {
            var list = new List<KeyValuePair<string, long>>();
            if (d == null) return list;
            list.Add(new KeyValuePair<string, long>(BoardWins, d.totals.wins));
            list.Add(new KeyValuePair<string, long>(BoardGreens, d.totals.greens));
            list.Add(new KeyValuePair<string, long>(BoardDailyStreak, d.daily?.bestStreak ?? 0));
            return list;
        }
    }
}
