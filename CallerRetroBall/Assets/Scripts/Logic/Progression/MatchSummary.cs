using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One box-score row with display info, detached from the live simulation.</summary>
    [Serializable]
    public class SummaryLine
    {
        public int playerIndex;
        public int team;
        public string playerId;
        public string name;
        public int jerseyNumber;
        public bool isHuman;
        public PlayerStatLine stats;
    }

    /// <summary>
    /// Everything the post-game screen and progression need from a finished match. Built once
    /// from a <see cref="MatchSimulation"/>; its <see cref="matchId"/> guards against granting
    /// rewards twice.
    /// </summary>
    [Serializable]
    public class MatchSummary
    {
        public string matchId;
        public GameMode mode;
        public string teamAId;
        public string teamBId;
        public string teamAName;
        public string teamBName;
        public int scoreA;
        public int scoreB;
        public int humanTeam;
        /// <summary>0 = team A, 1 = team B, -1 = tie.</summary>
        public int winner;
        public GameOverReason reason;
        public int playerOfTheGame;
        public List<SummaryLine> lines = new List<SummaryLine>();
        public bool isPlayoff;
        public bool isFinal;

        public bool HumanWon => winner == humanTeam;
        public int HumanScore => humanTeam == 0 ? scoreA : scoreB;
        public int OpponentScore => humanTeam == 0 ? scoreB : scoreA;
        public int Margin => HumanScore - OpponentScore;

        public SummaryLine HumanLine
        {
            get
            {
                foreach (var l in lines) if (l.isHuman) return l;
                return null;
            }
        }

        public SummaryLine Line(int playerIndex)
        {
            foreach (var l in lines) if (l.playerIndex == playerIndex) return l;
            return null;
        }

        public static MatchSummary From(MatchSimulation m, GameMode mode, string matchId)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            var s = new MatchSummary
            {
                matchId = matchId,
                mode = mode,
                teamAId = m.Setup.TeamA.id,
                teamBId = m.Setup.TeamB.id,
                teamAName = m.Setup.TeamA.FullName,
                teamBName = m.Setup.TeamB.FullName,
                scoreA = m.Score[0],
                scoreB = m.Score[1],
                humanTeam = m.Setup.HumanTeam,
                winner = Scoring.Winner(m.Score[0], m.Score[1]),
                reason = m.EndReason,
            };
            int potgTeam = s.winner < 0 ? s.humanTeam : s.winner;
            s.playerOfTheGame = m.Stats.PlayerOfTheGame(potgTeam, MatchSimulation.PlayersPerTeam);
            foreach (var p in m.Players)
            {
                s.lines.Add(new SummaryLine
                {
                    playerIndex = p.Index,
                    team = p.Team,
                    playerId = p.Def.id,
                    name = p.Def.DisplayName,
                    jerseyNumber = p.Def.jerseyNumber,
                    isHuman = p.IsHuman,
                    stats = m.Stats[p.Index],
                });
            }
            return s;
        }
    }
}
