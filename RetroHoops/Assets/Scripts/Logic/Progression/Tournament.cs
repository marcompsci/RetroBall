using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class ClassicSaveData
    {
        /// <summary>Bracket seeds 1–4 (index 0 = first seed). Empty when no Classic is running.</summary>
        public List<string> seeds = new List<string>();
        /// <summary>Semifinals then final (round 1 = semi, 2 = final).</summary>
        public List<ScheduledGame> games = new List<ScheduledGame>();
        public int edition;
        public string championId;
        public bool finished;
        public int titles;

        public bool Active => seeds.Count == 4 && !finished;
    }

    public enum ClassicOutcome { None = 0, WonSemi = 1, Champion = 2, Eliminated = 3 }

    /// <summary>
    /// First Call Classic: a four-team knockout. The First Callers and three league teams drawn by seed
    /// play two semifinals (1 v 4, 2 v 3) and a final. Games the crew isn't in are simulated from team
    /// strength. Each edition can be entered again after it ends.
    /// </summary>
    public static class ClassicEngine
    {
        public static string CrewId => DefaultContent.PlayerCrewId;

        /// <summary>Starts a new edition (abandons any one in progress).</summary>
        public static void Start(ClassicSaveData t, ContentCatalog c)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            t.edition++;
            t.finished = false;
            t.championId = null;
            t.games.Clear();
            t.seeds.Clear();

            var rng = new SeededRandom(StableHash.Of("classic:" + t.edition));
            var pool = c.TeamsInTier(TeamTier.League).ConvertAll(x => x.id);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            // The crew is the underdog: fourth seed, so its semi is against the top seed.
            t.seeds.Add(pool[0]);
            t.seeds.Add(pool[1]);
            t.seeds.Add(pool[2]);
            t.seeds.Add(CrewId);
            t.games.Add(new ScheduledGame { round = 1, week = 0, homeId = t.seeds[0], awayId = t.seeds[3] });
            t.games.Add(new ScheduledGame { round = 1, week = 0, homeId = t.seeds[1], awayId = t.seeds[2] });
        }

        public static ScheduledGame NextGameForCrew(ClassicSaveData t)
        {
            if (t == null || !t.Active) return null;
            foreach (var g in t.games) if (!g.played && g.Involves(CrewId)) return g;
            return null;
        }

        public static MatchRequest NextMatch(ClassicSaveData t, ContentCatalog c, string difficultyId)
        {
            var g = NextGameForCrew(t);
            if (g == null) return null;
            string opp = g.homeId == CrewId ? g.awayId : g.homeId;
            return new MatchRequest
            {
                Mode = GameMode.Tournament,
                HomeTeamId = CrewId,
                AwayTeamId = opp,
                CourtId = c.Team(g.homeId)?.homeCourtId ?? c.Team(opp)?.homeCourtId,
                DifficultyId = difficultyId,
                Round = g.round,
                ContextId = "classic:e" + t.edition + ":r" + g.round,
            };
        }

        /// <summary>Records the crew's game, simulates the rest of that round, and builds the final.</summary>
        public static ClassicOutcome ApplyResult(ClassicSaveData t, ContentCatalog c, MatchSummary summary)
        {
            if (summary == null || summary.mode != GameMode.Tournament) return ClassicOutcome.None;
            var g = NextGameForCrew(t);
            if (g == null) return ClassicOutcome.None;

            int crew = summary.HumanScore, opp = summary.OpponentScore;
            if (crew == opp) crew++; // sudden death means no ties; stay safe anyway
            bool crewHome = g.homeId == CrewId;
            g.homeScore = crewHome ? crew : opp;
            g.awayScore = crewHome ? opp : crew;
            g.played = true;

            var rng = new SeededRandom(StableHash.Of("classic:" + t.edition + ":sim:" + g.round));
            foreach (var other in t.games)
                if (!other.played && other.round == g.round) SeasonEngine.SimulateGame(other, c, rng);

            bool won = g.WinnerId == CrewId;
            if (g.round == 1)
            {
                var semis = t.games.FindAll(x => x.round == 1);
                // The final is home for the better seed.
                string a = semis[0].WinnerId, b = semis[1].WinnerId;
                bool aFirst = t.seeds.IndexOf(a) <= t.seeds.IndexOf(b);
                var final = new ScheduledGame { round = 2, week = 1, homeId = aFirst ? a : b, awayId = aFirst ? b : a };
                t.games.Add(final);
                if (won) return ClassicOutcome.WonSemi;
                SeasonEngine.SimulateGame(final, c, rng);
                Finish(t, final.WinnerId);
                return ClassicOutcome.Eliminated;
            }

            Finish(t, g.WinnerId);
            if (!won) return ClassicOutcome.Eliminated;
            t.titles++;
            return ClassicOutcome.Champion;
        }

        private static void Finish(ClassicSaveData t, string champion)
        {
            t.championId = champion;
            t.finished = true;
        }
    }
}
