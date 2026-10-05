using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum WeeklyGoal
    {
        Wins = 0,
        Points = 1,
        Greens = 2,
        Steals = 3,
        Assists = 4,
        Rebounds = 5,
        Blocks = 6,
        ParkWins = 7,
        AnkleBreakers = 8,
        BigWins = 9,
        Games = 10,
    }

    /// <summary>One of the week's three goals. Progress adds up over every counted game that week.</summary>
    public sealed class WeeklyChallenge
    {
        public int Week;
        public int Slot;
        public WeeklyGoal Goal;
        public int Target;

        public string Describe()
        {
            switch (Goal)
            {
                case WeeklyGoal.Wins: return "Win " + Target + " games";
                case WeeklyGoal.Points: return "Score " + Target + " points";
                case WeeklyGoal.Greens: return "Hit " + Target + " GREEN releases";
                case WeeklyGoal.Steals: return "Get " + Target + " steals";
                case WeeklyGoal.Assists: return "Dish " + Target + " assists";
                case WeeklyGoal.Rebounds: return "Grab " + Target + " rebounds";
                case WeeklyGoal.Blocks: return "Block " + Target + " shots";
                case WeeklyGoal.ParkWins: return "Win " + Target + " games at The Park";
                case WeeklyGoal.AnkleBreakers: return "Break " + Target + " ankles";
                case WeeklyGoal.BigWins: return "Win " + Target + " games by " + Weekly.BigWinMargin + "+";
                default: return "Play " + Target + " games";
            }
        }
    }

    [Serializable]
    public class WeeklySaveData
    {
        /// <summary>Week the progress belongs to (-1 = never started).</summary>
        public int week = -1;
        public List<int> progress = new List<int> { 0, 0, 0 };
        public List<bool> done = new List<bool> { false, false, false };
        /// <summary>Goals finished over all weeks.</summary>
        public int completed;
        /// <summary>Weeks with all three goals done.</summary>
        public int perfectWeeks;
    }

    /// <summary>
    /// Weekly Challenges: three goals that rotate every Monday, the same for everyone and fully offline
    /// (they're worked out from the week number). Each finished goal pays Signal Points and Hoops Pass XP.
    /// </summary>
    public static class Weekly
    {
        public const int Goals = 3;
        public const int RewardSp = 100;
        public const int PerfectWeekSp = 150;
        public const int BigWinMargin = 8;
        /// <summary>Day number (since 2000-01-01) of Monday 3 January 2000: weeks start on Mondays.</summary>
        private const int FirstMonday = 2;

        public static int WeekOf(int day) => (int)Math.Floor((day - FirstMonday) / 7.0);

        /// <summary>Days until the next Monday (1..7).</summary>
        public static int DaysLeft(int day) => 7 - (((day - FirstMonday) % 7) + 7) % 7;

        public static WeeklyChallenge[] For(int week)
        {
            var rng = new SeededRandom(StableHash.Of("weekly:" + week));
            var pool = new List<WeeklyGoal>((WeeklyGoal[])Enum.GetValues(typeof(WeeklyGoal)));
            var result = new WeeklyChallenge[Goals];
            for (int i = 0; i < Goals; i++)
            {
                int pick = rng.Range(0, pool.Count);
                var goal = pool[pick];
                pool.RemoveAt(pick);
                result[i] = new WeeklyChallenge { Week = week, Slot = i, Goal = goal, Target = TargetFor(goal, rng) };
            }
            return result;
        }

        private static int TargetFor(WeeklyGoal g, SeededRandom rng)
        {
            switch (g)
            {
                case WeeklyGoal.Wins: return 5 + rng.Range(0, 4);              // 5..8
                case WeeklyGoal.Points: return 60 + 10 * rng.Range(0, 5);      // 60..100
                case WeeklyGoal.Greens: return 10 + 2 * rng.Range(0, 4);       // 10..16
                case WeeklyGoal.Steals: return 8 + rng.Range(0, 5);            // 8..12
                case WeeklyGoal.Assists: return 15 + 5 * rng.Range(0, 3);      // 15..25
                case WeeklyGoal.Rebounds: return 15 + 5 * rng.Range(0, 3);     // 15..25
                case WeeklyGoal.Blocks: return 4 + rng.Range(0, 3);            // 4..6
                case WeeklyGoal.ParkWins: return 2 + rng.Range(0, 2);          // 2..3
                case WeeklyGoal.AnkleBreakers: return 3 + rng.Range(0, 3);     // 3..5
                case WeeklyGoal.BigWins: return 2 + rng.Range(0, 2);           // 2..3
                default: return 8 + 2 * rng.Range(0, 3);                       // 8..12
            }
        }

        /// <summary>Starts a fresh week when the calendar has moved on (progress from last week is dropped).</summary>
        public static void Sync(WeeklySaveData save, int day)
        {
            if (save == null) return;
            int week = WeekOf(day);
            if (save.progress == null || save.progress.Count != Goals) save.progress = new List<int> { 0, 0, 0 };
            if (save.done == null || save.done.Count != Goals) save.done = new List<bool> { false, false, false };
            if (save.week == week) return;
            save.week = week;
            for (int i = 0; i < Goals; i++) { save.progress[i] = 0; save.done[i] = false; }
        }

        /// <summary>Games that count: anything you play against the CPU (not practice, the tutorial, 2 Player or the demo).</summary>
        public static bool Counts(GameMode mode) =>
            mode != GameMode.Practice && mode != GameMode.Tutorial && mode != GameMode.Versus && mode != GameMode.Demo;

        /// <summary>How much one finished game adds to a goal.</summary>
        public static int Amount(WeeklyChallenge c, MatchSummary s, bool parkGame)
        {
            if (c == null || s == null) return 0;
            var line = s.HumanLine?.stats ?? new PlayerStatLine();
            switch (c.Goal)
            {
                case WeeklyGoal.Wins: return s.HumanWon ? 1 : 0;
                case WeeklyGoal.Points: return line.points;
                case WeeklyGoal.Greens: return line.greenReleases;
                case WeeklyGoal.Steals: return line.steals;
                case WeeklyGoal.Assists: return line.assists;
                case WeeklyGoal.Rebounds: return line.rebounds;
                case WeeklyGoal.Blocks: return line.blocks;
                case WeeklyGoal.ParkWins: return parkGame && s.HumanWon ? 1 : 0;
                case WeeklyGoal.AnkleBreakers: return line.ankleBreakers;
                case WeeklyGoal.BigWins: return s.HumanWon && s.Margin >= BigWinMargin ? 1 : 0;
                default: return 1;
            }
        }

        /// <summary>
        /// Adds a finished game to this week's goals (call once per game, after Career.ApplyMatch said it
        /// was new). Pays Signal Points for each goal that's now done and returns what happened, including
        /// the Hoops Pass XP and any pass rewards it unlocked.
        /// </summary>
        public static WeeklyResult ApplyGame(CareerSaveData data, ContentCatalog c, MatchSummary s, int day, bool parkGame)
        {
            var result = new WeeklyResult();
            if (data == null || s == null || !Counts(s.mode)) return result;
            data.weekly = data.weekly ?? new WeeklySaveData();
            Sync(data.weekly, day);
            var goals = For(data.weekly.week);
            int xp = HoopsPass.GameXp + (s.HumanWon ? HoopsPass.WinXp : 0);
            for (int i = 0; i < Goals; i++)
            {
                if (data.weekly.done[i]) continue;
                int add = Amount(goals[i], s, parkGame);
                if (add <= 0) continue;
                data.weekly.progress[i] = Math.Min(goals[i].Target, data.weekly.progress[i] + add);
                if (data.weekly.progress[i] < goals[i].Target) continue;
                data.weekly.done[i] = true;
                data.weekly.completed++;
                data.signalPoints += RewardSp;
                result.Completed.Add(goals[i]);
                result.SignalPoints += RewardSp;
                xp += HoopsPass.WeeklyGoalXp;
            }
            if (result.Completed.Count > 0 && data.weekly.done.TrueForAll(d => d))
            {
                data.weekly.perfectWeeks++;
                data.signalPoints += PerfectWeekSp;
                result.SignalPoints += PerfectWeekSp;
                result.PerfectWeek = true;
            }
            result.PassXp = xp;
            result.PassRewards = HoopsPass.AddXp(data, c, xp, day);
            return result;
        }
    }

    public sealed class WeeklyResult
    {
        public readonly List<WeeklyChallenge> Completed = new List<WeeklyChallenge>();
        public int SignalPoints;
        public bool PerfectWeek;
        public int PassXp;
        public List<PassReward> PassRewards = new List<PassReward>();

        /// <summary>One line for the post-game screen (null when nothing worth saying happened).</summary>
        public string Note()
        {
            var parts = new List<string>();
            foreach (var g in Completed) parts.Add("WEEKLY DONE: " + g.Describe().ToUpperInvariant());
            if (PerfectWeek) parts.Add("PERFECT WEEK!");
            foreach (var r in PassRewards) parts.Add("PASS TIER " + r.Tier + ": " + r.Label.ToUpperInvariant());
            return parts.Count == 0 ? null : string.Join("\n", parts);
        }
    }

    [Serializable]
    public class PassSaveData
    {
        /// <summary>Pass season the XP belongs to (-1 = never started).</summary>
        public int season = -1;
        public int xp;
        /// <summary>Highest tier whose reward has been granted this season.</summary>
        public int granted;
        /// <summary>Seasons where every tier was reached.</summary>
        public int seasonsMaxed;
        /// <summary>Last day the daily-challenge XP was added (so it counts once a day).</summary>
        public int dailyXpDay = -1;
    }

    public sealed class PassReward
    {
        public int Tier;
        /// <summary>Signal Points paid (0 for a cosmetic).</summary>
        public int SignalPoints;
        /// <summary>Pass-exclusive cosmetic (null for Signal Points).</summary>
        public string CosmeticId;
        public string Label;
    }

    /// <summary>
    /// The Hoops Pass: a free season track (no purchases, no ads). Playing fills it; each tier pays
    /// Signal Points, and tiers 5, 10, 15 and 20 unlock gear you can only get here. Seasons are six
    /// weeks long and rotate through three gear sets, so missed gear comes round again.
    /// </summary>
    public static class HoopsPass
    {
        public const int Tiers = 20;
        public const int XpPerTier = 100;
        public const int WeeksPerSeason = 6;
        public const int GameXp = 25;
        public const int WinXp = 15;
        public const int WeeklyGoalXp = 120;
        public const int DailyXp = 60;
        /// <summary>Paid instead when a gear tier comes round again and you already own it.</summary>
        public const int OwnedGearSp = 250;

        /// <summary>Rotating gear sets: tier 5, 10, 15, 20.</summary>
        public static readonly string[][] GearSets =
        {
            new[] { "cosmetic.pass.jersey.vapor_court", "cosmetic.pass.shoes.horizon", "cosmetic.pass.banner.neon_grid", "cosmetic.pass.jersey.sunset_swish" },
            new[] { "cosmetic.pass.jersey.static_bloom", "cosmetic.pass.shoes.cloud_nine", "cosmetic.pass.banner.checker_flag", "cosmetic.pass.jersey.tropic_night" },
            // Season 5: the tape-deck set, topped by a pass-only dunk.
            new[] { "cosmetic.pass.jersey.cassette_deck", "cosmetic.pass.shoes.tape_runners", "cosmetic.pass.banner.boombox", "cosmetic.pass.dunk.skyline" },
        };

        public static int SeasonOf(int day)
        {
            int week = Weekly.WeekOf(day);
            return (int)Math.Floor(week / (double)WeeksPerSeason);
        }

        /// <summary>Days left in the pass season.</summary>
        public static int DaysLeft(int day)
        {
            int week = Weekly.WeekOf(day);
            int weekInSeason = week - SeasonOf(day) * WeeksPerSeason;
            return (WeeksPerSeason - 1 - weekInSeason) * 7 + Weekly.DaysLeft(day);
        }

        public static int Tier(int xp) => Math.Min(Tiers, Math.Max(0, xp) / XpPerTier);

        public static bool IsGearTier(int tier) => tier > 0 && tier % 5 == 0;

        /// <summary>Signal Points for an ordinary tier: grows a little up the track.</summary>
        public static int TierSp(int tier) => 40 + 5 * tier;

        public static string GearFor(int season, int tier)
        {
            if (!IsGearTier(tier)) return null;
            var set = GearSets[((season % GearSets.Length) + GearSets.Length) % GearSets.Length];
            return set[tier / 5 - 1];
        }

        /// <summary>The reward a tier shows (Signal Points or gear), before checking what you own.</summary>
        public static PassReward RewardFor(ContentCatalog c, int season, int tier)
        {
            string gear = GearFor(season, tier);
            if (gear != null)
            {
                var def = c?.Find(c.Cosmetics, gear);
                return new PassReward { Tier = tier, CosmeticId = gear, Label = def != null ? def.displayName : gear };
            }
            int sp = TierSp(tier);
            return new PassReward { Tier = tier, SignalPoints = sp, Label = "+" + sp + " SP" };
        }

        /// <summary>Starts the new season's track when the calendar moves on.</summary>
        public static void Sync(PassSaveData p, int day)
        {
            if (p == null) return;
            int season = SeasonOf(day);
            if (p.season == season) return;
            p.season = season;
            p.xp = 0;
            p.granted = 0;
        }

        /// <summary>Adds XP and grants every tier reached (gear goes straight into your locker). Returns what was granted.</summary>
        public static List<PassReward> AddXp(CareerSaveData data, ContentCatalog c, int xp, int day)
        {
            var granted = new List<PassReward>();
            if (data == null || xp <= 0) return granted;
            data.pass = data.pass ?? new PassSaveData();
            var p = data.pass;
            Sync(p, day);
            bool wasMaxed = Tier(p.xp) >= Tiers;
            p.xp = Math.Min(Tiers * XpPerTier, p.xp + xp);
            int reached = Tier(p.xp);
            while (p.granted < reached)
            {
                p.granted++;
                var r = RewardFor(c, p.season, p.granted);
                if (r.CosmeticId != null)
                {
                    if (data.ownedCosmetics.Contains(r.CosmeticId) || c?.Find(c.Cosmetics, r.CosmeticId) == null)
                    {
                        r.SignalPoints = OwnedGearSp;
                        r.Label = r.Label + " (owned: +" + OwnedGearSp + " SP)";
                        r.CosmeticId = null;
                    }
                    else data.ownedCosmetics.Add(r.CosmeticId);
                }
                data.signalPoints += r.SignalPoints;
                granted.Add(r);
            }
            if (!wasMaxed && reached >= Tiers) p.seasonsMaxed++;
            return granted;
        }

        /// <summary>Daily Challenge XP, once per day.</summary>
        public static List<PassReward> AddDailyXp(CareerSaveData data, ContentCatalog c, int day)
        {
            data.pass = data.pass ?? new PassSaveData();
            if (data.pass.dailyXpDay == day) return new List<PassReward>();
            data.pass.dailyXpDay = day;
            return AddXp(data, c, DailyXp, day);
        }
    }
}
