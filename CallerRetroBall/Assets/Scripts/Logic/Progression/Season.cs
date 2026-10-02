using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Standings row computed from played games (never stored, so it can't drift).</summary>
    public sealed class TeamRecord
    {
        public string TeamId;
        public int Wins;
        public int Losses;
        public int PointsFor;
        public int PointsAgainst;
        /// <summary>+N = won last N, −N = lost last N.</summary>
        public int Streak;

        public int Games => Wins + Losses;
        public int Differential => PointsFor - PointsAgainst;
        public float WinPct => Games == 0 ? 0f : (float)Wins / Games;
        public string StreakText => Streak == 0 ? "-" : (Streak > 0 ? "W" + Streak : "L" + (-Streak));
    }

    /// <summary>
    /// Mini-season rules: an 8-team league where the player's crew plays a 10-game schedule,
    /// the other results are simulated from team ratings (seeded, so reloads match), then a
    /// four-team bracket for The Gold Signal Cup.
    /// </summary>
    public static class SeasonEngine
    {
        public const int PlayoffTeams = 4;

        /// <summary>
        /// Builds a season for <paramref name="crewId"/> joining the league. The league has one open
        /// spot, so one league team (chosen by seed) sits the season out.
        /// </summary>
        public static SeasonSaveData Create(ContentCatalog c, string crewId, uint seed, int seasonNumber = 1)
        {
            var config = c.Seasons.Count > 0 ? c.Seasons[0] : new SeasonConfigDef();
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            var league = new List<string>(config.teamIds);
            league.Sort(string.CompareOrdinal);
            if (league.Count >= 8) league.RemoveAt(rng.Range(0, league.Count));
            var teams = new List<string> { crewId };
            teams.AddRange(league);

            var season = new SeasonSaveData { seed = seed, teamIds = teams, weeks = config.regularSeasonGames, seasonNumber = seasonNumber };

            // Player's opponents: everyone once, then repeats, in a seeded order.
            var opponents = new List<string>(league);
            Shuffle(opponents, rng);
            var order = new List<string>();
            while (order.Count < season.weeks) order.AddRange(opponents);

            for (int week = 0; week < season.weeks; week++)
            {
                string opp = order[week];
                bool home = week % 2 == 0;
                season.games.Add(new ScheduledGame { week = week, homeId = home ? crewId : opp, awayId = home ? opp : crewId });

                // Pair the remaining teams (rotating so matchups vary).
                var rest = new List<string>();
                foreach (var t in teams) if (t != crewId && t != opp) rest.Add(t);
                Rotate(rest, week);
                for (int i = 0; i + 1 < rest.Count; i += 2)
                    season.games.Add(new ScheduledGame { week = week, homeId = rest[i], awayId = rest[i + 1] });
            }
            return season;
        }

        public static ScheduledGame NextGameFor(SeasonSaveData s, string teamId)
        {
            foreach (var g in s.games)
                if (!g.played && g.Involves(teamId)) return g;
            return null;
        }

        /// <summary>Records the player's game and simulates the rest of that week (or playoff round).</summary>
        public static void RecordResult(SeasonSaveData s, ScheduledGame game, int homeScore, int awayScore, ContentCatalog c)
        {
            game.played = true;
            game.homeScore = homeScore;
            game.awayScore = awayScore;
            if (homeScore == awayScore) game.homeScore++; // no ties in the standings
            var rng = new SeededRandom(StableHash.Of(s.seed + ":" + game.week + ":" + game.round));
            foreach (var g in s.games)
            {
                if (g.played || g.week != game.week || g.round != game.round) continue;
                SimulateGame(g, c, rng);
            }
            if (game.round == 0) s.currentWeek = Math.Max(s.currentWeek, game.week + 1);
        }

        /// <summary>Quick rating-based result for games the player isn't in.</summary>
        public static void SimulateGame(ScheduledGame g, ContentCatalog c, SeededRandom rng)
        {
            float home = TeamStrength(c.Team(g.homeId), c) + 1.5f;
            float away = TeamStrength(c.Team(g.awayId), c);
            float pHome = 1f / (1f + (float)Math.Exp(-(home - away) / 6f));
            bool homeWins = rng.NextFloat() < pHome;
            int winner = 12 + rng.Range(0, 10);  // 12..21
            int loser = Math.Max(2, winner - 1 - rng.Range(0, 9));
            g.homeScore = homeWins ? winner : loser;
            g.awayScore = homeWins ? loser : winner;
            g.played = true;
        }

        /// <summary>Average overall of a team's top three players.</summary>
        public static float TeamStrength(TeamDef t, ContentCatalog c)
        {
            if (t == null) return 50f;
            var overalls = new List<int>();
            foreach (var id in t.rosterPlayerIds)
            {
                var p = c.Player(id);
                if (p != null) overalls.Add(p.attributes.Overall);
            }
            overalls.Sort((a, b) => b.CompareTo(a));
            float sum = 0f;
            int n = Math.Min(3, overalls.Count);
            for (int i = 0; i < n; i++) sum += overalls[i];
            return n == 0 ? 50f : sum / n;
        }

        public static bool RegularSeasonComplete(SeasonSaveData s)
        {
            foreach (var g in s.games) if (g.round == 0 && !g.played) return false;
            return true;
        }

        /// <summary>
        /// Standings sorted by: win percentage, then point differential, then points scored,
        /// then head-to-head wins, then team id (so the order is always stable).
        /// </summary>
        public static List<TeamRecord> Standings(SeasonSaveData s)
        {
            var map = new Dictionary<string, TeamRecord>();
            foreach (var id in s.teamIds) map[id] = new TeamRecord { TeamId = id };
            foreach (var g in s.games)
            {
                if (!g.played || g.round != 0) continue;
                Apply(map, g.homeId, g.homeScore, g.awayScore);
                Apply(map, g.awayId, g.awayScore, g.homeScore);
            }
            var list = new List<TeamRecord>(map.Values);
            list.Sort((a, b) => Compare(a, b, s));
            return list;
        }

        private static void Apply(Dictionary<string, TeamRecord> map, string id, int pf, int pa)
        {
            if (!map.TryGetValue(id, out var r)) return;
            bool win = pf > pa;
            if (win) r.Wins++;
            else r.Losses++;
            r.PointsFor += pf;
            r.PointsAgainst += pa;
            r.Streak = win ? (r.Streak > 0 ? r.Streak + 1 : 1) : (r.Streak < 0 ? r.Streak - 1 : -1);
        }

        private static int Compare(TeamRecord a, TeamRecord b, SeasonSaveData s)
        {
            int c = b.WinPct.CompareTo(a.WinPct);
            if (c != 0) return c;
            c = b.Differential.CompareTo(a.Differential);
            if (c != 0) return c;
            c = b.PointsFor.CompareTo(a.PointsFor);
            if (c != 0) return c;
            c = HeadToHead(s, b.TeamId, a.TeamId).CompareTo(HeadToHead(s, a.TeamId, b.TeamId));
            if (c != 0) return c;
            return string.CompareOrdinal(a.TeamId, b.TeamId);
        }

        /// <summary>Regular-season wins of <paramref name="team"/> over <paramref name="other"/>.</summary>
        public static int HeadToHead(SeasonSaveData s, string team, string other)
        {
            int wins = 0;
            foreach (var g in s.games)
                if (g.played && g.round == 0 && g.Involves(team) && g.Involves(other) && g.WinnerId == team) wins++;
            return wins;
        }

        // ------------------------------------------------------------------ playoffs

        /// <summary>Creates the semifinals (1 v 4, 2 v 3) from the final standings.</summary>
        public static void CreatePlayoffs(SeasonSaveData s)
        {
            if (HasRound(s, 1)) return;
            var table = Standings(s);
            int week = s.weeks;
            s.games.Add(new ScheduledGame { week = week, round = 1, homeId = table[0].TeamId, awayId = table[3].TeamId });
            s.games.Add(new ScheduledGame { week = week, round = 1, homeId = table[1].TeamId, awayId = table[2].TeamId });
        }

        public static bool HasRound(SeasonSaveData s, int round)
        {
            foreach (var g in s.games) if (g.round == round) return true;
            return false;
        }

        public static bool RoundComplete(SeasonSaveData s, int round)
        {
            bool any = false;
            foreach (var g in s.games)
            {
                if (g.round != round) continue;
                any = true;
                if (!g.played) return false;
            }
            return any;
        }

        /// <summary>After both semis are played, creates the final between the winners.</summary>
        public static void CreateFinal(SeasonSaveData s)
        {
            if (HasRound(s, 2) || !RoundComplete(s, 1)) return;
            var winners = new List<string>();
            foreach (var g in s.games) if (g.round == 1) winners.Add(g.WinnerId);
            s.games.Add(new ScheduledGame { week = s.weeks + 1, round = 2, homeId = winners[0], awayId = winners[1] });
        }

        /// <summary>Simulates any unplayed games in a round that don't involve <paramref name="teamId"/>.</summary>
        public static void SimulateRoundWithout(SeasonSaveData s, int round, string teamId, ContentCatalog c)
        {
            var rng = new SeededRandom(StableHash.Of(s.seed + ":round:" + round));
            foreach (var g in s.games)
                if (g.round == round && !g.played && !g.Involves(teamId)) SimulateGame(g, c, rng);
        }

        public static ScheduledGame FinalGame(SeasonSaveData s)
        {
            foreach (var g in s.games) if (g.round == 2) return g;
            return null;
        }

        public static bool Qualified(SeasonSaveData s, string teamId)
        {
            var table = Standings(s);
            for (int i = 0; i < Math.Min(PlayoffTeams, table.Count); i++)
                if (table[i].TeamId == teamId) return true;
            return false;
        }

        private static void Shuffle(List<string> list, SeededRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static void Rotate(List<string> list, int by)
        {
            if (list.Count == 0) return;
            by %= list.Count;
            var copy = new List<string>(list);
            for (int i = 0; i < list.Count; i++) list[i] = copy[(i + by) % copy.Count];
        }
    }
}
