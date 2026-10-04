using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class CupSaveData
    {
        /// <summary>Eight teams in bracket order: QF pairs are (0,7), (1,6), (2,5), (3,4). Empty = no Cup running.</summary>
        public List<string> bracket = new List<string>();
        /// <summary>Quarterfinals (round 1), semifinals (2), final (3).</summary>
        public List<ScheduledGame> games = new List<ScheduledGame>();
        public string homeTeamId;
        public int edition;
        public string championId;
        public bool finished;
        public int titles;

        public bool Active => bracket.Count == CupEngine.Teams && !finished;
    }

    public enum CupOutcome { None = 0, Advanced = 1, Champion = 2, Eliminated = 3 }

    /// <summary>
    /// The Caller Cup: an eight-team knockout. Your team plus seven league teams, seeded by strength
    /// (you're the eighth seed). Three rounds; games you're not in are simulated from team strength.
    /// The title pays a bonus on top of the normal game rewards.
    /// </summary>
    public static class CupEngine
    {
        public const int Teams = 8;
        public const int Rounds = 3;
        public const int TitleBonus = 300;
        private static readonly int[] FirstRound = { 0, 7, 1, 6, 2, 5, 3, 4 };

        public static void Start(CupSaveData cup, ContentCatalog c, string homeTeamId)
        {
            if (cup == null) throw new ArgumentNullException(nameof(cup));
            cup.edition++;
            cup.finished = false;
            cup.championId = null;
            cup.homeTeamId = homeTeamId;
            cup.games.Clear();
            cup.bracket.Clear();

            var pool = c.TeamsInTier(TeamTier.League).FindAll(t => t.id != homeTeamId);
            var rng = new SeededRandom(StableHash.Of("cup:" + cup.edition));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var field = pool.GetRange(0, Math.Min(Teams - 1, pool.Count));
            // Seed by strength, strongest first; your team is the underdog eighth seed.
            field.Sort((a, b) => ArcadeEngine.TeamOverall(c, b).CompareTo(ArcadeEngine.TeamOverall(c, a)));
            foreach (var t in field) cup.bracket.Add(t.id);
            cup.bracket.Add(homeTeamId);
            for (int k = 0; k < FirstRound.Length; k += 2)
                cup.games.Add(new ScheduledGame { round = 1, week = k / 2, homeId = cup.bracket[FirstRound[k]], awayId = cup.bracket[FirstRound[k + 1]] });
        }

        public static ScheduledGame NextGame(CupSaveData cup)
        {
            if (cup == null || !cup.Active) return null;
            foreach (var g in cup.games) if (!g.played && g.Involves(cup.homeTeamId)) return g;
            return null;
        }

        public static string RoundName(int round) => round == 1 ? "QUARTERFINAL" : round == 2 ? "SEMIFINAL" : "FINAL";

        public static MatchRequest NextMatch(CupSaveData cup, ContentCatalog c, string difficultyId)
        {
            var g = NextGame(cup);
            if (g == null) return null;
            string opp = g.homeId == cup.homeTeamId ? g.awayId : g.homeId;
            var home = c.Team(cup.homeTeamId);
            if (home == null || c.Team(opp) == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.Cup,
                HomeTeamId = cup.homeTeamId,
                AwayTeamId = opp,
                // The final is played on the better seed's court.
                CourtId = c.Team(g.homeId)?.homeCourtId ?? home.homeCourtId,
                DifficultyId = difficultyId,
                ContextId = "cup:e" + cup.edition + ":r" + g.round,
            };
        }

        /// <summary>Records your game, simulates the rest of the round, and builds the next one.</summary>
        public static CupOutcome ApplyResult(CupSaveData cup, ContentCatalog c, MatchSummary s, CareerSaveData career, out int bonus)
        {
            bonus = 0;
            if (s == null || s.mode != GameMode.Cup) return CupOutcome.None;
            var g = NextGame(cup);
            if (g == null) return CupOutcome.None;
            int mine = s.HumanScore, theirs = s.OpponentScore;
            if (mine == theirs) mine++;
            bool home = g.homeId == cup.homeTeamId;
            g.homeScore = home ? mine : theirs;
            g.awayScore = home ? theirs : mine;
            g.played = true;
            bool won = g.WinnerId == cup.homeTeamId;

            var rng = new SeededRandom(StableHash.Of("cup:" + cup.edition + ":sim:" + g.round));
            // Play out the rest of the bracket round by round (all of it, if you're knocked out).
            int round = g.round;
            while (true)
            {
                foreach (var other in cup.games)
                    if (!other.played && other.round == round) SeasonEngine.SimulateGame(other, c, rng);
                var thisRound = cup.games.FindAll(x => x.round == round);
                if (thisRound.Count == 1)
                {
                    cup.championId = thisRound[0].WinnerId;
                    cup.finished = true;
                    break;
                }
                for (int k = 0; k + 1 < thisRound.Count; k += 2)
                {
                    string a = thisRound[k].WinnerId, b = thisRound[k + 1].WinnerId;
                    bool aFirst = cup.bracket.IndexOf(a) <= cup.bracket.IndexOf(b);
                    cup.games.Add(new ScheduledGame { round = round + 1, week = k / 2, homeId = aFirst ? a : b, awayId = aFirst ? b : a });
                }
                round++;
                if (won) break; // your next game is yours to play
            }

            if (!won) return CupOutcome.Eliminated;
            if (cup.finished)
            {
                cup.titles++;
                bonus = TitleBonus;
                if (career != null) career.signalPoints += bonus;
                return CupOutcome.Champion;
            }
            return CupOutcome.Advanced;
        }
    }
}
