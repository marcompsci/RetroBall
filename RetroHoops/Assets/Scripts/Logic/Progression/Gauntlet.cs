using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class GauntletSaveData
    {
        /// <summary>Day of the run in progress (-1 = none).</summary>
        public int day = -1;
        /// <summary>Stations finished in today's run, and the points from each.</summary>
        public List<int> points = new List<int>();
        /// <summary>Best finished total for <see cref="bestDay"/>, and that day.</summary>
        public int todayBest;
        public int bestDay = -1;
        /// <summary>Best finished total ever (the Game Center board).</summary>
        public int best;
        public int runs;
    }

    /// <summary>
    /// SKILLS GAUNTLET (Phase 34): four practice drills back to back, the same four for everyone each day (worked out
    /// from the date, so it's offline and fair). Each drill's result turns into points; the four add up to the day's
    /// score. Replay as often as you like: your best run of the day and your best ever are kept, and the best ever
    /// goes to a Game Center leaderboard.
    /// </summary>
    public static class Gauntlet
    {
        public const int Stations = 4;
        public const string ContextPrefix = "gauntlet:";
        public const string LeaderboardId = "retrohoops.lb.gauntlet";

        /// <summary>The drills a Gauntlet can use (Shootout and H-O-R-S-E need a CPU or a friend, so they're left out).</summary>
        public static readonly DrillKind[] Pool =
        {
            DrillKind.FreeShoot, DrillKind.PassingTargets, DrillKind.DribbleLane, DrillKind.ThreePoint, DrillKind.Lockdown, DrillKind.AroundTheWorld,
        };

        /// <summary>Today's four stations, in order (a seeded shuffle of the pool).</summary>
        public static DrillKind[] For(int day)
        {
            var rng = new SeededRandom(StableHash.Of("gauntlet:" + day));
            var pool = new List<DrillKind>(Pool);
            var result = new DrillKind[Stations];
            for (int i = 0; i < Stations; i++)
            {
                int k = rng.Range(0, pool.Count);
                result[i] = pool[k];
                pool.RemoveAt(k);
            }
            return result;
        }

        public static uint SeedFor(int day, int station) => StableHash.Of("gauntlet:" + day + ":" + station) | 1u;

        /// <summary>
        /// Points for one finished drill, scaled so each station is worth about the same (a strong run ≈ 300 each).
        /// Timed courses score for finishing fast; an unfinished course scores what it got through.
        /// </summary>
        public static int Points(DrillKind kind, int makes, int bestStreak, int passScore, float courseTime, bool courseFinished,
                                 int contestPoints, int stops, int coursePartial = 0)
        {
            switch (kind)
            {
                case DrillKind.FreeShoot: return Math.Max(0, makes * 12 + bestStreak * 6);
                case DrillKind.PassingTargets: return Math.Max(0, passScore * 25);
                case DrillKind.ThreePoint: return Math.Max(0, contestPoints * 12);
                case DrillKind.Lockdown: return Math.Max(0, stops * 55);
                case DrillKind.DribbleLane:
                    return courseFinished ? Math.Max(60, (int)Math.Round(420 - courseTime * 12)) : Math.Max(0, coursePartial * 20);
                case DrillKind.AroundTheWorld:
                    return courseFinished ? Math.Max(60, (int)Math.Round(500 - courseTime * 6)) : Math.Max(0, coursePartial * 25);
                default: return 0;
            }
        }

        /// <summary>The practice request for a station of a day's run.</summary>
        public static MatchRequest Request(int day, int station)
        {
            var r = MatchRequest.PracticeDefault();
            r.Drill = (int)For(day)[Math.Max(0, Math.Min(Stations - 1, station))];
            r.Seed = SeedFor(day, station);
            r.ContextId = ContextPrefix + day + ":" + station;
            return r;
        }

        public static bool IsGauntlet(string contextId) => contextId != null && contextId.StartsWith(ContextPrefix, StringComparison.Ordinal);

        public static bool TryParse(string contextId, out int day, out int station)
        {
            day = station = -1;
            if (!IsGauntlet(contextId)) return false;
            var parts = contextId.Substring(ContextPrefix.Length).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out day) && int.TryParse(parts[1], out station) && station >= 0 && station < Stations;
        }

        /// <summary>Starts (or restarts) today's run.</summary>
        public static void Start(GauntletSaveData g, int day)
        {
            g.day = day;
            g.points.Clear();
        }

        /// <summary>The station to play next in today's run (0 = start; <see cref="Stations"/> = finished).</summary>
        public static int NextStation(GauntletSaveData g, int day) => g == null || g.day != day ? 0 : g.points.Count;

        public static int Total(GauntletSaveData g)
        {
            int t = 0;
            if (g?.points != null) foreach (int p in g.points) t += p;
            return t;
        }

        /// <summary>
        /// Records a finished station. Only the station the run is on counts (a replayed or stale station is ignored).
        /// Returns true when this finished the run.
        /// </summary>
        public static bool Record(GauntletSaveData g, int day, int station, int points)
        {
            if (g == null) return false;
            if (g.day != day) Start(g, day);
            if (station != g.points.Count || station >= Stations) return false;
            g.points.Add(Math.Max(0, points));
            if (g.points.Count < Stations) return false;
            int total = Total(g);
            g.runs++;
            if (g.bestDay != day) { g.bestDay = day; g.todayBest = 0; }
            g.todayBest = Math.Max(g.todayBest, total);
            g.best = Math.Max(g.best, total);
            return true;
        }

        public static string StationName(DrillKind k)
        {
            switch (k)
            {
                case DrillKind.FreeShoot: return "FREE SHOOT";
                case DrillKind.PassingTargets: return "PASSING TARGETS";
                case DrillKind.DribbleLane: return "DRIBBLE LANE";
                case DrillKind.ThreePoint: return "3-POINT CONTEST";
                case DrillKind.Lockdown: return "LOCKDOWN";
                case DrillKind.AroundTheWorld: return "AROUND THE WORLD";
                default: return k.ToString().ToUpperInvariant();
            }
        }
    }
}
