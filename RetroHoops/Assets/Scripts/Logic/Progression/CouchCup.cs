using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class CouchGame
    {
        public int round;
        /// <summary>Entrant indices; b = -1 is a bye (a goes through).</summary>
        public int a, b = -1;
        public int scoreA, scoreB;
        public bool played;
        public int Winner => !played ? -1 : b < 0 ? a : (scoreA >= scoreB ? a : b);
    }

    [Serializable]
    public class CouchCupSaveData
    {
        public List<string> names = new List<string>();
        public List<string> teamIds = new List<string>();
        public List<CouchGame> games = new List<CouchGame>();
        /// <summary>Champion's entrant index, -1 while it's being played.</summary>
        public int champion = -1;
        public int cupsPlayed;
        public bool Active => names.Count >= 2 && champion < 0;
    }

    public enum CouchOutcome { Recorded = 0, Champion = 1, TieReplay = 2, NoGame = 3 }

    /// <summary>
    /// COUCH CUP: a knockout tournament for 2 to 8 friends sharing one iPhone or iPad. Everyone types a
    /// name and picks a team; the bracket says who's up next, the two of them play a 2 Player game on the
    /// same device (tabletop or with controllers), then pass it on. Byes fill out odd numbers. A tied game
    /// is played again. Progress is saved, so a cup can go on across an evening.
    /// </summary>
    public static class CouchCup
    {
        public const int MinPlayers = 2, MaxPlayers = 8;
        public const string ContextPrefix = "couch:";

        public static int BracketSize(int players)
        {
            int size = 2;
            while (size < players) size *= 2;
            return size;
        }

        public static int Rounds(CouchCupSaveData c)
        {
            int size = BracketSize(c.names.Count), r = 0;
            while (size > 1) { size /= 2; r++; }
            return r;
        }

        /// <summary>Starts a cup: the order is shuffled by <paramref name="seed"/>, then the top seeds get any byes.</summary>
        public static void Start(CouchCupSaveData c, IList<string> names, IList<string> teamIds, uint seed)
        {
            int n = Math.Max(MinPlayers, Math.Min(MaxPlayers, Math.Min(names.Count, teamIds.Count)));
            if (names.Count < MinPlayers || teamIds.Count < MinPlayers) throw new ArgumentException("A Couch Cup needs at least two players.");
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            c.names = new List<string>();
            c.teamIds = new List<string>();
            foreach (int i in order)
            {
                string name = string.IsNullOrWhiteSpace(names[i]) ? "PLAYER " + (i + 1) : names[i].Trim();
                c.names.Add(name.Length > 14 ? name.Substring(0, 14) : name);
                c.teamIds.Add(teamIds[i]);
            }
            c.games = new List<CouchGame>();
            c.champion = -1;
            int size = BracketSize(n);
            // Seed s plays seed size-1-s; seeds past the field are byes.
            for (int s = 0; s < size / 2; s++)
            {
                int other = size - 1 - s;
                var g = new CouchGame { round = 0, a = s, b = other < n ? other : -1 };
                if (g.b < 0) g.played = true;
                c.games.Add(g);
            }
            Advance(c);
        }

        /// <summary>The next game to play (both players real), or null when the cup is over.</summary>
        public static CouchGame NextGame(CouchCupSaveData c)
        {
            if (c == null || !c.Active) return null;
            foreach (var g in c.games) if (!g.played && g.b >= 0) return g;
            return null;
        }

        public static int GameIndex(CouchCupSaveData c, CouchGame g) => c.games.IndexOf(g);

        /// <summary>Records the next game's score (a = home side). A tie is not recorded: play it again.</summary>
        public static CouchOutcome Report(CouchCupSaveData c, int scoreA, int scoreB)
        {
            var g = NextGame(c);
            if (g == null) return CouchOutcome.NoGame;
            if (scoreA == scoreB) return CouchOutcome.TieReplay;
            g.scoreA = scoreA;
            g.scoreB = scoreB;
            g.played = true;
            Advance(c);
            if (c.champion >= 0)
            {
                c.cupsPlayed++;
                return CouchOutcome.Champion;
            }
            return CouchOutcome.Recorded;
        }

        /// <summary>When a round is finished, pairs its winners for the next one (or crowns the champion).</summary>
        private static void Advance(CouchCupSaveData c)
        {
            while (true)
            {
                int round = 0;
                foreach (var g in c.games) round = Math.Max(round, g.round);
                var current = c.games.FindAll(g => g.round == round);
                if (current.Exists(g => !g.played)) return;
                if (current.Count == 1)
                {
                    c.champion = current[0].Winner;
                    return;
                }
                for (int i = 0; i + 1 < current.Count; i += 2)
                    c.games.Add(new CouchGame { round = round + 1, a = current[i].Winner, b = current[i + 1].Winner });
            }
        }

        public static string RoundName(CouchCupSaveData c, int round)
        {
            int left = Rounds(c) - round;
            return left <= 1 ? "FINAL" : left == 2 ? "SEMIFINALS" : left == 3 ? "QUARTERFINALS" : "ROUND " + (round + 1);
        }

        /// <summary>The game as a 2 Player match: the first-named player is Player 1 (bottom edge in tabletop).</summary>
        public static MatchRequest Request(CouchCupSaveData c, ContentCatalog catalog, CouchGame g, string difficultyId)
        {
            var home = catalog.Team(c.teamIds[g.a]);
            return new MatchRequest
            {
                Mode = GameMode.Versus,
                HomeTeamId = c.teamIds[g.a],
                AwayTeamId = c.teamIds[g.b],
                CourtId = home?.homeCourtId,
                DifficultyId = difficultyId,
                ContextId = ContextPrefix + GameIndex(c, g),
            };
        }

        public static bool IsCouch(string contextId) => contextId != null && contextId.StartsWith(ContextPrefix, StringComparison.Ordinal);

        /// <summary>Clears the cup (names and teams are kept for next time by the menu, not here).</summary>
        public static void Reset(CouchCupSaveData c)
        {
            c.names.Clear();
            c.teamIds.Clear();
            c.games.Clear();
            c.champion = -1;
        }
    }
}
