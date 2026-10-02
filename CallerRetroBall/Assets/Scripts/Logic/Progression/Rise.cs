using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public struct EventEffect
    {
        public int energy;
        public int chemistry;
        public int signalPoints;
        public int fans;

        public EventEffect(int energy, int chemistry, int signalPoints, int fans)
        {
            this.energy = energy;
            this.chemistry = chemistry;
            this.signalPoints = signalPoints;
            this.fans = fans;
        }

        public string Describe()
        {
            var parts = new List<string>();
            if (energy != 0) parts.Add((energy > 0 ? "+" : "") + energy + " energy");
            if (chemistry != 0) parts.Add((chemistry > 0 ? "+" : "") + chemistry + " chemistry");
            if (signalPoints != 0) parts.Add((signalPoints > 0 ? "+" : "") + signalPoints + " SP");
            if (fans != 0) parts.Add((fans > 0 ? "+" : "") + fans + " fans");
            return parts.Count == 0 ? "No change" : string.Join(", ", parts);
        }
    }

    [Serializable]
    public class EventChoice
    {
        public string label;
        public EventEffect effect;
    }

    [Serializable]
    public class EventCardDef : IHasId
    {
        public string id;
        public string title;
        public string body;
        public List<EventChoice> choices = new List<EventChoice>();
        public string Id => id;
    }

    /// <summary>Small, original between-game story beats. No real people or brands.</summary>
    public static class EventCards
    {
        public static List<EventCardDef> All()
        {
            return new List<EventCardDef>
            {
                Card("event.late_practice", "Late Practice",
                    "Your teammate asks to stay after practice and work on timing.",
                    Choice("Stay and run it back", new EventEffect(-10, 12, 0, 0)),
                    Choice("Rest up tonight", new EventEffect(10, 0, 0, 0))),
                Card("event.court_challenge", "Court Challenge",
                    "A local court challenges your crew to a pickup run for bragging rights.",
                    Choice("Accept — the crowd will show up", new EventEffect(-12, 5, 0, 30)),
                    Choice("Politely pass", new EventEffect(0, -3, 0, 0))),
                Card("event.youth_clinic", "Youth Clinic",
                    "The rec centre asks if you'll help run a free youth clinic.",
                    Choice("Lace up and teach", new EventEffect(-6, 4, 0, 40)),
                    Choice("Send a signed ball instead", new EventEffect(0, 0, -20, 15))),
                Card("event.film_session", "Film Session",
                    "Your crew wants to break down last game's tape together.",
                    Choice("Pizza and film", new EventEffect(0, 10, -15, 0)),
                    Choice("Everyone watches alone", new EventEffect(5, -2, 0, 0))),
                Card("event.sponsor_shootout", "Shootout Night",
                    "The neighbourhood is hosting a shootout night with a small prize.",
                    Choice("Enter the shootout", new EventEffect(-8, 0, 60, 10)),
                    Choice("Cheer from the stands", new EventEffect(5, 3, 0, 5))),
                Card("event.rainout", "Rain Delay",
                    "A storm rolls in and the gym floor needs a day to dry.",
                    Choice("Rest and stretch", new EventEffect(15, 0, 0, 0)),
                    Choice("Shoot in the garage", new EventEffect(-5, 6, 10, 0))),
                Card("event.mixtape", "Highlight Tape",
                    "A local video crew wants to cut a highlight tape of your last game.",
                    Choice("Let them roll", new EventEffect(0, 0, 0, 60)),
                    Choice("Keep it low key", new EventEffect(5, 4, 0, 0))),
                Card("event.rival_trash_talk", "Trash Talk",
                    "Next week's opponent is talking loud online.",
                    Choice("Answer on the court", new EventEffect(-6, 8, 0, 20)),
                    Choice("Stay quiet", new EventEffect(4, 2, 0, 0))),
                Card("event.new_kicks", "Shoe Drop",
                    "The corner store got a shipment of fresh sneakers in your size.",
                    Choice("Treat the crew", new EventEffect(0, 10, -40, 0)),
                    Choice("Save the money", new EventEffect(0, -2, 20, 0))),
                Card("event.early_bus", "Early Bus",
                    "Road game tomorrow. The bus leaves at dawn.",
                    Choice("Early night", new EventEffect(12, 0, 0, 0)),
                    Choice("Team dinner first", new EventEffect(-4, 8, -10, 0))),
            };
        }

        private static EventCardDef Card(string id, string title, string body, params EventChoice[] choices) =>
            new EventCardDef { id = id, title = title, body = body, choices = new List<EventChoice>(choices) };

        private static EventChoice Choice(string label, EventEffect e) => new EventChoice { label = label, effect = e };

        public static EventCardDef Find(string id)
        {
            foreach (var c in All()) if (c.id == id) return c;
            return null;
        }
    }

    public enum RiseOutcome
    {
        None = 0,
        CircuitWin = 1,
        CircuitLoss = 2,
        EnteredLeague = 3,
        SeasonGame = 4,
        MadePlayoffs = 5,
        MissedPlayoffs = 6,
        AdvancedToFinal = 7,
        Eliminated = 8,
        Champion = 9,
    }

    /// <summary>
    /// Rise Mode flow: three Blacktop Circuit challenges, then a Caller League season with
    /// event cards between games, then the playoff bracket for The Gold Signal Cup.
    /// </summary>
    public static class RiseEngine
    {
        public const int EnergyPerGame = 12;
        public const int EnergyRecoveryPerGame = 4;
        public const int MinEnergy = 20;

        public static readonly string[] CircuitOrder =
        {
            "crew.cage_regulars", "crew.pier_pressure", "crew.underpass_union", "crew.rooftop_relay", "crew.boardwalk_bandits",
        };

        public static string CrewId => DefaultContent.PlayerCrewId;

        public static string NextCircuitOpponent(RiseSaveData r)
        {
            foreach (var id in CircuitOrder) if (!r.circuitBeaten.Contains(id)) return id;
            return null;
        }

        /// <summary>The next game to play, or null when the run is complete (start a new season).</summary>
        public static MatchRequest NextMatch(RiseSaveData r, ContentCatalog c, string difficultyId)
        {
            switch (r.stage)
            {
                case RiseStage.Circuit:
                {
                    var opp = c.Team(NextCircuitOpponent(r));
                    if (opp == null) return null;
                    return Request(opp.id, opp.homeCourtId, difficultyId, 0, "rise:circuit:" + opp.id + ":" + r.circuitBeaten.Count);
                }
                case RiseStage.Season:
                case RiseStage.Playoffs:
                {
                    var s = r.season;
                    var g = s == null ? null : SeasonEngine.NextGameFor(s, CrewId);
                    if (g == null) return null;
                    string opp = g.homeId == CrewId ? g.awayId : g.homeId;
                    var home = c.Team(g.homeId);
                    return Request(opp, home?.homeCourtId, difficultyId, g.round,
                        "rise:s" + s.seasonNumber + ":r" + g.round + ":w" + g.week);
                }
                default:
                    return null;
            }
        }

        private static MatchRequest Request(string opponentId, string courtId, string difficultyId, int round, string context)
        {
            return new MatchRequest
            {
                Mode = GameMode.Rise,
                HomeTeamId = CrewId,
                AwayTeamId = opponentId,
                CourtId = courtId,
                DifficultyId = difficultyId,
                Round = round,
                ContextId = context,
            };
        }

        /// <summary>Starting stamina from energy (never below 60% so a tired crew can still play).</summary>
        public static float StartingStamina(RiseSaveData r) => Math.Max(0.6f, Math.Min(1f, r.energy / 100f));

        /// <summary>Applies a finished Rise game. Returns what happened for the hub to announce.</summary>
        public static RiseOutcome ApplyResult(RiseSaveData r, ContentCatalog c, MatchSummary summary, CareerSaveData career)
        {
            if (summary.mode != GameMode.Rise) return RiseOutcome.None;
            bool won = summary.HumanWon;
            string opp = summary.humanTeam == 0 ? summary.teamBId : summary.teamAId;

            r.energy = Math.Max(MinEnergy, Math.Min(100, r.energy - EnergyPerGame + EnergyRecoveryPerGame));
            if (won) r.chemistry = Math.Min(100, r.chemistry + 3);

            if (r.stage == RiseStage.Circuit)
            {
                if (!won) return RiseOutcome.CircuitLoss;
                if (!r.circuitBeaten.Contains(opp)) r.circuitBeaten.Add(opp);
                if (NextCircuitOpponent(r) != null) return RiseOutcome.CircuitWin;
                StartSeason(r, c);
                return RiseOutcome.EnteredLeague;
            }

            var s = r.season;
            var game = SeasonEngine.NextGameFor(s, CrewId);
            if (game == null) return RiseOutcome.None;
            bool crewHome = game.homeId == CrewId;
            int crewScore = summary.HumanScore, oppScore = summary.OpponentScore;
            if (crewScore == oppScore) crewScore++; // sudden death means no ties, but stay safe
            SeasonEngine.RecordResult(s, game, crewHome ? crewScore : oppScore, crewHome ? oppScore : crewScore, c);

            if (game.round == 0)
            {
                MaybeQueueEvent(r, s);
                if (!SeasonEngine.RegularSeasonComplete(s)) return RiseOutcome.SeasonGame;
                if (SeasonEngine.Qualified(s, CrewId))
                {
                    SeasonEngine.CreatePlayoffs(s);
                    r.stage = RiseStage.Playoffs;
                    return RiseOutcome.MadePlayoffs;
                }
                FinishSeason(r, s, c);
                return RiseOutcome.MissedPlayoffs;
            }

            if (game.round == 1)
            {
                if (!won)
                {
                    FinishSeason(r, s, c);
                    return RiseOutcome.Eliminated;
                }
                SeasonEngine.CreateFinal(s);
                return RiseOutcome.AdvancedToFinal;
            }

            // Final
            s.championId = game.WinnerId;
            r.stage = RiseStage.Complete;
            r.seasonsPlayed++;
            if (won && career != null) career.totals.championships++;
            return won ? RiseOutcome.Champion : RiseOutcome.Eliminated;
        }

        private static void FinishSeason(RiseSaveData r, SeasonSaveData s, ContentCatalog c)
        {
            // Play out the bracket without the crew so there is still a champion.
            if (SeasonEngine.RegularSeasonComplete(s) && !SeasonEngine.HasRound(s, 1)) SeasonEngine.CreatePlayoffs(s);
            SeasonEngine.SimulateRoundWithout(s, 1, CrewId, c);
            if (!SeasonEngine.RoundComplete(s, 1))
            {
                // The crew lost its semi already; nothing else to simulate in round 1.
            }
            SeasonEngine.CreateFinal(s);
            SeasonEngine.SimulateRoundWithout(s, 2, CrewId, c);
            s.championId = SeasonEngine.FinalGame(s)?.WinnerId;
            r.stage = RiseStage.Complete;
            r.seasonsPlayed++;
        }

        public static void StartSeason(RiseSaveData r, ContentCatalog c)
        {
            int number = r.seasonsPlayed + 1;
            r.season = SeasonEngine.Create(c, CrewId, StableHash.Of("season:" + number), number);
            r.stage = RiseStage.Season;
            r.energy = Math.Max(r.energy, 80);
            r.pendingEventId = null;
        }

        /// <summary>From a completed run, starts the next season (the circuit only happens once).</summary>
        public static bool StartNextSeason(RiseSaveData r, ContentCatalog c)
        {
            if (r.stage != RiseStage.Complete) return false;
            StartSeason(r, c);
            return true;
        }

        /// <summary>Queues an event card after roughly every other game, cycling through unseen cards.</summary>
        private static void MaybeQueueEvent(RiseSaveData r, SeasonSaveData s)
        {
            if (s.currentWeek % 2 != 1) return;
            var all = EventCards.All();
            if (r.seenEvents.Count >= all.Count) r.seenEvents.Clear();
            var rng = new SeededRandom(StableHash.Of(s.seed + ":event:" + s.currentWeek));
            var unseen = all.FindAll(e => !r.seenEvents.Contains(e.id));
            var card = unseen[rng.Range(0, unseen.Count)];
            r.pendingEventId = card.id;
        }

        /// <summary>Resolves the pending event card with the chosen option.</summary>
        public static bool ResolveEvent(RiseSaveData r, CareerSaveData career, int choiceIndex)
        {
            var card = EventCards.Find(r.pendingEventId);
            if (card == null || choiceIndex < 0 || choiceIndex >= card.choices.Count) return false;
            var e = card.choices[choiceIndex].effect;
            r.energy = Math.Max(MinEnergy, Math.Min(100, r.energy + e.energy));
            r.chemistry = Math.Max(0, Math.Min(100, r.chemistry + e.chemistry));
            if (career != null)
            {
                career.signalPoints = Math.Max(0, career.signalPoints + e.signalPoints);
                career.fans = Math.Max(0, career.fans + e.fans);
            }
            r.seenEvents.Add(card.id);
            r.pendingEventId = null;
            return true;
        }

        public static string Objective(RiseSaveData r, ContentCatalog c)
        {
            switch (r.stage)
            {
                case RiseStage.Circuit:
                    var next = c.Team(NextCircuitOpponent(r));
                    var court = next != null ? c.Court(next.homeCourtId) : null;
                    return "Beat " + (next?.FullName ?? "the crews") + (court != null ? " at " + court.displayName : "") +
                           " (" + r.circuitBeaten.Count + "/" + CircuitOrder.Length + ")";
                case RiseStage.Season:
                    return "Finish in the top " + SeasonEngine.PlayoffTeams + " of " + DefaultContent.LeagueName;
                case RiseStage.Playoffs:
                    var g = r.season != null ? SeasonEngine.NextGameFor(r.season, CrewId) : null;
                    return g != null && g.round == 2 ? "Win " + DefaultContent.ChampionshipName : "Win your semifinal";
                default:
                    return r.season != null && r.season.championId == CrewId
                        ? "Champions! Start the next season to defend the Cup."
                        : "Season over. Start the next season.";
            }
        }
    }
}
