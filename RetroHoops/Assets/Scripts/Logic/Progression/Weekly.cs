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
        /** Season 6 goals (from week <see cref="Weekly.Season6Week"/>). */
        DeepShots = 11,
        AlleyOops = 12,
        HeatUps = 13,
        /** Season 7 goal (from week <see cref="Weekly.Season7Week"/>): wins in Full Court games (Full Court, Franchise, Legacy, All-Star). */
        FullCourtWins = 14,
        /** Season 8 goal (from week <see cref="Weekly.Season8Week"/>): corner threes made (from the shot chart). */
        CornerThrees = 15,
        /** Season 9 goal (from week <see cref="Weekly.Season9Week"/>): CLUTCH scenarios won. */
        ClutchWins = 16,
        /** Season 10 goal (from week <see cref="Weekly.Season10Week"/>): stops (steals plus blocks). */
        Stops = 17,
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
            int n = Target;
            if (Loc.Language == Loc.Spanish)
            {
                // Phase 37: the weekly goals in Spanish.
                switch (Goal)
                {
                    case WeeklyGoal.Wins: return "Gana " + n + " partidos";
                    case WeeklyGoal.Points: return "Anota " + n + " puntos";
                    case WeeklyGoal.Greens: return "Consigue " + n + " tiros perfectos";
                    case WeeklyGoal.Steals: return "Consigue " + n + " robos";
                    case WeeklyGoal.Assists: return "Da " + n + " asistencias";
                    case WeeklyGoal.Rebounds: return "Captura " + n + " rebotes";
                    case WeeklyGoal.Blocks: return "Pon " + n + " tapones";
                    case WeeklyGoal.ParkWins: return "Gana " + n + " partidos en The Park";
                    case WeeklyGoal.AnkleBreakers: return "Rompe " + n + " tobillos";
                    case WeeklyGoal.BigWins: return "Gana " + n + " partidos por " + Weekly.BigWinMargin + "+";
                    case WeeklyGoal.DeepShots: return "Mete " + n + " tiros lejanos";
                    case WeeklyGoal.AlleyOops: return "Pasa o remata " + n + " alley-oops";
                    case WeeklyGoal.HeatUps: return "Ponte al rojo vivo " + n + " veces";
                    case WeeklyGoal.FullCourtWins: return "Gana " + n + " partidos de cancha completa";
                    case WeeklyGoal.CornerThrees: return "Mete " + n + " tiros desde las esquinas";
                    case WeeklyGoal.ClutchWins: return "Gana " + n + " escenarios CLUTCH";
                    case WeeklyGoal.Stops: return "Consigue " + n + " paradas (robos o tapones)";
                    default: return "Juega " + n + " partidos";
                }
            }
            switch (Goal)
            {
                case WeeklyGoal.Wins: return "Win " + n + " games";
                case WeeklyGoal.Points: return "Score " + n + " points";
                case WeeklyGoal.Greens: return "Hit " + n + " GREEN releases";
                case WeeklyGoal.Steals: return "Get " + n + " steals";
                case WeeklyGoal.Assists: return "Dish " + n + " assists";
                case WeeklyGoal.Rebounds: return "Grab " + n + " rebounds";
                case WeeklyGoal.Blocks: return "Block " + n + " shots";
                case WeeklyGoal.ParkWins: return "Win " + n + " games at The Park";
                case WeeklyGoal.AnkleBreakers: return "Break " + n + " ankles";
                case WeeklyGoal.BigWins: return "Win " + n + " games by " + Weekly.BigWinMargin + "+";
                case WeeklyGoal.DeepShots: return "Make " + n + " deep shots";
                case WeeklyGoal.AlleyOops: return "Throw or finish " + n + " alley-oops";
                case WeeklyGoal.HeatUps: return "Heat up " + n + " times";
                case WeeklyGoal.FullCourtWins: return "Win " + n + " Full Court games";
                case WeeklyGoal.CornerThrees: return "Make " + n + " shots from the corners";
                case WeeklyGoal.ClutchWins: return "Win " + n + " CLUTCH scenarios";
                case WeeklyGoal.Stops: return "Get " + n + " stops (steals or blocks)";
                default: return "Play " + n + " games";
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
        /// <summary>Week of Monday 12 October 2026: Season 6's goals join the pool from here (earlier weeks keep their goals).</summary>
        public const int Season6Week = 1397;
        /// <summary>Week of Monday 9 November 2026: Season 7's goal joins the pool.</summary>
        public const int Season7Week = 1401;
        /// <summary>Week of Monday 7 December 2026: Season 8's goal joins the pool.</summary>
        public const int Season8Week = 1405;
        /// <summary>Week of Monday 4 January 2027: Season 9's goal joins the pool.</summary>
        public const int Season9Week = 1409;
        /// <summary>Week of Monday 1 February 2027: Season 10's goal joins the pool.</summary>
        public const int Season10Week = 1413;

        public static int WeekOf(int day) => (int)Math.Floor((day - FirstMonday) / 7.0);

        /// <summary>Days until the next Monday (1..7).</summary>
        public static int DaysLeft(int day) => 7 - (((day - FirstMonday) % 7) + 7) % 7;

        public static WeeklyChallenge[] For(int week)
        {
            var rng = new SeededRandom(StableHash.Of("weekly:" + week));
            var pool = new List<WeeklyGoal>((WeeklyGoal[])Enum.GetValues(typeof(WeeklyGoal)));
            if (week < Season6Week) pool.RemoveAll(g => g >= WeeklyGoal.DeepShots);
            else if (week < Season7Week) pool.RemoveAll(g => g >= WeeklyGoal.FullCourtWins);
            else if (week < Season8Week) pool.RemoveAll(g => g >= WeeklyGoal.CornerThrees);
            else if (week < Season9Week) pool.RemoveAll(g => g >= WeeklyGoal.ClutchWins);
            else if (week < Season10Week) pool.RemoveAll(g => g >= WeeklyGoal.Stops);
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
                case WeeklyGoal.DeepShots: return 8 + 2 * rng.Range(0, 4);     // 8..14
                case WeeklyGoal.AlleyOops: return 3 + rng.Range(0, 3);         // 3..5
                case WeeklyGoal.HeatUps: return 2 + rng.Range(0, 3);           // 2..4
                case WeeklyGoal.FullCourtWins: return 2 + rng.Range(0, 2);     // 2..3
                case WeeklyGoal.CornerThrees: return 4 + rng.Range(0, 3);      // 4..6
                case WeeklyGoal.ClutchWins: return 2 + rng.Range(0, 3);        // 2..4
                case WeeklyGoal.Stops: return 10 + 2 * rng.Range(0, 4);        // 10..16
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
                case WeeklyGoal.BigWins: return s.HumanWon && s.Margin >= BigWinMargin && s.mode != GameMode.Clutch ? 1 : 0; // CLUTCH starts from a set score
                case WeeklyGoal.DeepShots: return line.arcMade;
                case WeeklyGoal.AlleyOops: return line.alleyOops + line.alleyOopPasses;
                case WeeklyGoal.HeatUps: return line.heatUps;
                case WeeklyGoal.FullCourtWins:
                    return s.HumanWon && (s.mode == GameMode.FullCourt || s.mode == GameMode.Franchise || s.mode == GameMode.Legacy || s.mode == GameMode.AllStar) ? 1 : 0;
                case WeeklyGoal.CornerThrees:
                    return line.chart == null ? 0 : line.chart[ShotSpot.CornerLeft].made + line.chart[ShotSpot.CornerRight].made;
                case WeeklyGoal.ClutchWins: return s.mode == GameMode.Clutch && s.HumanWon ? 1 : 0;
                case WeeklyGoal.Stops: return line.steals + line.blocks;
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
            // Season 6: the lighthouse set, topped by the Spotlight celebration.
            new[] { "cosmetic.pass.jersey.beacon", "cosmetic.pass.shoes.fog_runners", "cosmetic.pass.banner.lighthouse_beam", "cosmetic.pass.celebration.spotlight" },
            // Season 7: the courier set, topped by the Victory Lap celebration.
            new[] { "cosmetic.pass.jersey.courier", "cosmetic.pass.shoes.spoke_runners", "cosmetic.pass.banner.express_lane", "cosmetic.pass.celebration.victory_lap" },
            // Season 8: the night-market set, topped by the Lantern Release celebration.
            new[] { "cosmetic.pass.jersey.lantern", "cosmetic.pass.shoes.paper_soles", "cosmetic.pass.banner.glow_row", "cosmetic.pass.celebration.lantern_release" },
            // Season 9: the roller-rink set, topped by the Skate Glide celebration.
            new[] { "cosmetic.pass.jersey.rink", "cosmetic.pass.shoes.quad_glide", "cosmetic.pass.banner.mirror_ball", "cosmetic.pass.celebration.skate_glide" },
            // Season 10: the laundromat set, topped by the Tumble Dry celebration.
            new[] { "cosmetic.pass.jersey.fresh_press", "cosmetic.pass.shoes.tumble_treads", "cosmetic.pass.banner.open_all_night", "cosmetic.pass.celebration.tumble_dry" },
        };

        /// <summary>First pass season with eight sets (the one starting Monday 5 April 2027): Season 10's set first.</summary>
        public const int EightSetsFrom = 237;

        /// <summary>First pass season with seven sets (the one starting Monday 22 February 2027): Season 9's set first.</summary>
        public const int SevenSetsFrom = 236;

        /// <summary>First pass season with six sets (the one starting Monday 11 January 2027): Season 8's set first.</summary>
        public const int SixSetsFrom = 235;

        /// <summary>First pass season with five sets (the one starting Monday 30 November 2026): Season 7's set first.</summary>
        public const int FiveSetsFrom = 234;

        /// <summary>
        /// First pass season with four gear sets (the one starting Monday 19 October 2026). Earlier seasons keep the
        /// three-set rotation they were played with; from here the four sets rotate, starting with Season 6's.
        /// </summary>
        public const int FourSetsFrom = 233;

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
            int index = season < FourSetsFrom ? ((season % 3) + 3) % 3
                      : season < FiveSetsFrom ? (3 + (season - FourSetsFrom)) % 4
                      : season < SixSetsFrom ? (4 + (season - FiveSetsFrom)) % 5
                      : season < SevenSetsFrom ? (5 + (season - SixSetsFrom)) % 6
                      : season < EightSetsFrom ? (6 + (season - SevenSetsFrom)) % 7
                      : (7 + (season - EightSetsFrom)) % GearSets.Length;
            var set = GearSets[index];
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
