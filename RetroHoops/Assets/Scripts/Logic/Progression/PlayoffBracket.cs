using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 33: the four-team playoff as a bracket picture (semifinals 1 v 4 and 2 v 3, then the final) for the Rise
    /// season and Franchise screens. Before the playoffs start it shows who would be in them if the season ended today.
    /// </summary>
    public sealed class PlayoffBracket
    {
        public sealed class Slot
        {
            public string TeamId;
            /// <summary>Seed 1-4 (0 = not decided yet).</summary>
            public int Seed;
            public int Score = -1;
            public bool Won;
            public bool Known => !string.IsNullOrEmpty(TeamId);
        }

        public sealed class Matchup
        {
            public Slot Top = new Slot(), Bottom = new Slot();
            public bool Played;
            public string Label = "";
        }

        public readonly Matchup SemiA = new Matchup { Label = "SEMIFINAL" }, SemiB = new Matchup { Label = "SEMIFINAL" };
        public readonly Matchup Final = new Matchup { Label = "FINAL" };
        public string ChampionId;
        /// <summary>True while the regular season is still going (seeds are "if it ended today").</summary>
        public bool Projected;

        public static PlayoffBracket From(SeasonSaveData s)
        {
            var b = new PlayoffBracket();
            if (s == null) return b;
            var table = SeasonEngine.Standings(s);
            var seeds = new Dictionary<string, int>();
            for (int i = 0; i < table.Count && i < SeasonEngine.PlayoffTeams; i++) seeds[table[i].TeamId] = i + 1;
            int Seed(string id) => id != null && seeds.TryGetValue(id, out int n) ? n : 0;

            var semis = s.games.FindAll(g => g.round == 1);
            if (semis.Count == 0)
            {
                b.Projected = true;
                if (table.Count >= 4)
                {
                    Fill(b.SemiA, table[0].TeamId, 1, table[3].TeamId, 4);
                    Fill(b.SemiB, table[1].TeamId, 2, table[2].TeamId, 3);
                }
                return b;
            }
            // Playoffs exist: the semifinals in the order they were made (1 v 4 first).
            Set(b.SemiA, semis[0], Seed);
            if (semis.Count > 1) Set(b.SemiB, semis[1], Seed);
            var final = SeasonEngine.FinalGame(s);
            if (final != null) Set(b.Final, final, Seed);
            else
            {
                // The final's spots fill as each semifinal finishes.
                if (b.SemiA.Played) Fill(b.Final.Top, Winner(b.SemiA));
                if (b.SemiB.Played) Fill(b.Final.Bottom, Winner(b.SemiB));
            }
            if (b.Final.Played) b.ChampionId = b.Final.Top.Won ? b.Final.Top.TeamId : b.Final.Bottom.TeamId;
            return b;
        }

        private static Slot Winner(Matchup m) => m.Top.Won ? m.Top : m.Bottom;

        private static void Fill(Slot target, Slot from)
        {
            target.TeamId = from.TeamId;
            target.Seed = from.Seed;
        }

        private static void Fill(Matchup m, string top, int topSeed, string bottom, int bottomSeed)
        {
            m.Top.TeamId = top; m.Top.Seed = topSeed;
            m.Bottom.TeamId = bottom; m.Bottom.Seed = bottomSeed;
        }

        private static void Set(Matchup m, ScheduledGame g, System.Func<string, int> seed)
        {
            // Better seed on top.
            bool homeTop = seed(g.homeId) == 0 || seed(g.awayId) == 0 || seed(g.homeId) <= seed(g.awayId);
            string top = homeTop ? g.homeId : g.awayId, bottom = homeTop ? g.awayId : g.homeId;
            m.Top.TeamId = top; m.Top.Seed = seed(top);
            m.Bottom.TeamId = bottom; m.Bottom.Seed = seed(bottom);
            m.Played = g.played;
            if (!g.played) return;
            m.Top.Score = homeTop ? g.homeScore : g.awayScore;
            m.Bottom.Score = homeTop ? g.awayScore : g.homeScore;
            m.Top.Won = g.WinnerId == top;
            m.Bottom.Won = !m.Top.Won;
        }

        /// <summary>All three matchups, semifinals first.</summary>
        public IEnumerable<Matchup> All()
        {
            yield return SemiA;
            yield return SemiB;
            yield return Final;
        }
    }
}
