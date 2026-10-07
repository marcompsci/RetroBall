using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What a CLUTCH scenario's second and third stars ask for (on top of winning).</summary>
    public enum ClutchGoal
    {
        /// <summary>Just win (used as "no extra goal").</summary>
        Win = 0,
        /// <summary>Win by at least N.</summary>
        WinBy = 1,
        /// <summary>The other team scores at most N more points.</summary>
        HoldTo = 2,
        /// <summary>Your player scores at least N points.</summary>
        YouScore = 3,
        /// <summary>Your team makes at least N shots from beyond the arc.</summary>
        Threes = 4,
        /// <summary>Your team has at least N assists.</summary>
        Assists = 5,
        /// <summary>Your team doesn't turn it over.</summary>
        NoTurnovers = 6,
        /// <summary>Your team gets at least N steals.</summary>
        Steals = 7,
        /// <summary>Your team gets at least N blocks.</summary>
        Blocks = 8,
    }

    /// <summary>One late-game situation: a team, a score, a clock and who has the ball.</summary>
    public sealed class ClutchScenario
    {
        public string Id, Title, Story;
        /// <summary>0, 1 or 2 (see <see cref="Clutch.ChapterNames"/>).</summary>
        public int Chapter;
        public string YourTeamId, TheirTeamId;
        public bool FullCourt;
        public int Clock;
        public int ScoreFor, ScoreAgainst;
        public bool YourBall = true;
        public ClutchGoal Goal;
        public int GoalValue;
        public ClutchGoal Bonus;
        public int BonusValue;

        public int Deficit => ScoreAgainst - ScoreFor;
        /// <summary>Phase 38: a scenario made in the CLUTCH editor (or from a friend's code): played for fun, no stars.</summary>
        public bool Custom;
    }

    [Serializable]
    public class ClutchSaveData
    {
        /// <summary>Best stars per scenario as "id:stars" (0..3).</summary>
        public List<string> stars = new List<string>();
        public int played, won;
        /// <summary>Phase 38 DAILY CLUTCH: the last day it was won, the run of days in a row, and the best run.</summary>
        public int dailyDay = -1, dailyStreak, dailyBest;
    }

    public enum ClutchOutcome { None = 0, Lost = 1, Won = 2 }

    /// <summary>
    /// Phase 37 CLUTCH: eighteen late-game situations in three chapters. You take over a Caller League team with
    /// the clock running down (down three with twenty seconds left, up one and they have the ball...). Win for
    /// one star; meet the scenario's goal and its bonus for two more. New stars pay Signal Points, and stars open
    /// the later chapters. Games start from the scenario's score and clock (<see cref="MatchSetup.StartScoreA"/>).
    /// </summary>
    public static class Clutch
    {
        public const string RulesId = "rules.clutch";
        public const string ContextPrefix = "clutch:";
        public const int StarsPerScenario = 3;
        public const int SpPerNewStar = 25;
        public static readonly string[] ChapterNames = { "CRUNCH TIME", "ICE IN THE VEINS", "LATE-NIGHT LEGENDS" };
        /// <summary>Stars needed to open each chapter.</summary>
        public static readonly int[] ChapterStars = { 0, 8, 20 };

        private const string Volt = "team.eastbay_voltage", Breakers = "team.baycity_breakers", Hounds = "team.harbor_hounds",
            Runners = "team.redwood_runners", Comets = "team.metro_comets", Drifters = "team.desert_drifters",
            Owls = "team.northline_owls", Crowns = "team.solar_crowns";

        public static readonly ClutchScenario[] All =
        {
            // Chapter 1: CRUNCH TIME (half court, 1s and 2s).
            S("ct_down2", 0, "DOWN TWO", "Twenty seconds left. A two ties it. Or does it?", Volt, Owls, false, 20, 17, 19, true, ClutchGoal.Threes, 1, ClutchGoal.NoTurnovers, 0),
            S("ct_hold", 0, "HOLD THE FORT", "Up one, their ball, thirty seconds to survive.", Hounds, Comets, false, 30, 15, 14, false, ClutchGoal.HoldTo, 1, ClutchGoal.Steals, 1),
            S("ct_tied", 0, "ALL SQUARE", "Tied at sixteen. Forty seconds. Your ball.", Runners, Crowns, false, 40, 16, 16, true, ClutchGoal.WinBy, 2, ClutchGoal.Assists, 1),
            S("ct_four", 0, "FOUR-POINT HOLE", "Down four with a minute left. Time to move.", Breakers, Drifters, false, 60, 12, 16, true, ClutchGoal.YouScore, 3, ClutchGoal.Threes, 1),
            S("ct_stops", 0, "GET STOPS", "Tied, their ball, thirty-five seconds.", Comets, Hounds, false, 35, 19, 19, false, ClutchGoal.HoldTo, 1, ClutchGoal.Blocks, 1),
            S("ct_close", 0, "CLOSE IT OUT", "Up two, your ball, a minute left. Don't let it slip.", Drifters, Volt, false, 60, 14, 12, true, ClutchGoal.WinBy, 3, ClutchGoal.NoTurnovers, 0),

            // Chapter 2: ICE IN THE VEINS (Full Court, 2s and 3s).
            S("iv_three", 1, "NEED A THREE", "Down three, eighteen seconds, the length of the floor to go.", Owls, Runners, true, 18, 64, 67, true, ClutchGoal.Threes, 1, ClutchGoal.WinBy, 2),
            S("iv_five", 1, "FIVE DOWN, FIFTY TO GO", "Down five with fifty seconds. You'll need stops too.", Crowns, Breakers, true, 50, 58, 63, true, ClutchGoal.Steals, 1, ClutchGoal.YouScore, 5),
            S("iv_last", 1, "LAST POSSESSION", "Tied, twelve seconds, your ball. One shot.", Volt, Comets, true, 12, 70, 70, true, ClutchGoal.YouScore, 2, ClutchGoal.NoTurnovers, 0),
            S("iv_lead", 1, "PROTECT THE LEAD", "Up two, their ball, thirty seconds.", Hounds, Owls, true, 30, 61, 59, false, ClutchGoal.HoldTo, 2, ClutchGoal.Blocks, 1),
            S("iv_seven", 1, "SEVEN IN A MINUTE", "Down seven with a minute left. Threes and stops.", Runners, Drifters, true, 60, 55, 62, true, ClutchGoal.Threes, 2, ClutchGoal.Steals, 2),
            S("iv_dagger", 1, "THE DAGGER", "Up one, forty seconds, your ball. Put it away.", Breakers, Crowns, true, 40, 66, 65, true, ClutchGoal.WinBy, 4, ClutchGoal.Assists, 2),

            // Chapter 3: LATE-NIGHT LEGENDS (mixed, the hardest spots).
            S("ln_eight", 2, "EIGHT DOWN", "Down eight with ninety seconds, Full Court. Nobody leaves.", Comets, Volt, true, 90, 50, 58, true, ClutchGoal.Threes, 2, ClutchGoal.YouScore, 6),
            S("ln_six", 2, "SIX IN THE CAGE", "Half court, down six, fifty seconds. Every two counts double.", Drifters, Owls, false, 50, 11, 17, true, ClutchGoal.Threes, 2, ClutchGoal.NoTurnovers, 0),
            S("ln_theirs", 2, "THEIR BALL, DOWN TWO", "Full Court, down two, their ball, twenty seconds. Get a stop first.", Owls, Hounds, true, 20, 72, 74, false, ClutchGoal.Steals, 1, ClutchGoal.WinBy, 2),
            S("ln_comeback", 2, "THE COMEBACK", "Half court, down six, seventy-five seconds. Make them nervous.", Crowns, Runners, false, 75, 8, 14, true, ClutchGoal.WinBy, 2, ClutchGoal.Assists, 2),
            S("ln_wall", 2, "THE WALL", "Up one, their ball, forty-five seconds, Full Court. No easy ones.", Volt, Breakers, true, 45, 67, 66, false, ClutchGoal.HoldTo, 2, ClutchGoal.Blocks, 2),
            S("ln_buzzer", 2, "BEAT THE BUZZER", "Down one, eight seconds, Full Court, your ball. Go.", Hounds, Crowns, true, 8, 73, 74, true, ClutchGoal.YouScore, 2, ClutchGoal.WinBy, 2),
        };

        private static ClutchScenario S(string id, int chapter, string title, string story, string you, string them, bool full, int clock,
                                        int scoreFor, int scoreAgainst, bool yourBall, ClutchGoal goal, int goalValue, ClutchGoal bonus, int bonusValue) =>
            new ClutchScenario
            {
                Id = id, Chapter = chapter, Title = title, Story = story, YourTeamId = you, TheirTeamId = them, FullCourt = full, Clock = clock,
                ScoreFor = scoreFor, ScoreAgainst = scoreAgainst, YourBall = yourBall, Goal = goal, GoalValue = goalValue, Bonus = bonus, BonusValue = bonusValue,
            };

        public static ClutchScenario Find(string id) => id == null ? null : Array.Find(All, s => s.Id == id);

        public static List<ClutchScenario> InChapter(int chapter) => new List<ClutchScenario>(Array.FindAll(All, s => s.Chapter == chapter));

        public static bool IsCustomContext(string contextId) =>
            contextId != null && contextId.StartsWith(ContextPrefix + CustomPrefix, StringComparison.Ordinal);

        public static ClutchScenario FromContext(string contextId) => FromContext(contextId, null);

        /// <summary>The scenario a game was for (<paramref name="catalog"/> reads editor codes; null builds the default content).</summary>
        public static ClutchScenario FromContext(string contextId, ContentCatalog catalog)
        {
            if (contextId == null || !contextId.StartsWith(ContextPrefix, StringComparison.Ordinal)) return null;
            string rest = contextId.Substring(ContextPrefix.Length);
            if (rest.StartsWith(CustomPrefix, StringComparison.Ordinal))
                return TryDecode(catalog, rest.Substring(CustomPrefix.Length), out var custom, out _) ? custom : null;
            int at = rest.IndexOf('@');
            return Find(at >= 0 ? rest.Substring(0, at) : rest);
        }

        // ------------------------------------------------------------------ Phase 38: DAILY CLUTCH

        public const int DailyBonusSp = 60;
        public const string LeaderboardId = "retrohoops.lb.clutch";

        /// <summary>Today's scenario: one of the eighteen, the same for everyone that day.</summary>
        public static ClutchScenario DailyFor(int day) => All[(int)(StableHash.Of("clutch-daily:" + day) % (uint)All.Length)];

        /// <summary>The day a DAILY CLUTCH game was for (-1 when it isn't one).</summary>
        public static int DailyDayOf(string contextId)
        {
            if (contextId == null || !contextId.StartsWith(ContextPrefix, StringComparison.Ordinal)) return -1;
            int at = contextId.IndexOf("@d", StringComparison.Ordinal);
            return at >= 0 && int.TryParse(contextId.Substring(at + 2), out int day) && day >= 0 ? day : -1;
        }

        public static MatchRequest DailyRequest(ContentCatalog c, int day, string difficultyId)
        {
            var r = Request(c, DailyFor(day), difficultyId);
            if (r == null) return null;
            r.ContextId += "@d" + day;
            r.Seed = StableHash.Of("clutch-daily-seed:" + day) | 1u;
            return r;
        }

        public static bool DailyDone(ClutchSaveData d, int day) => d != null && d.dailyDay == day;

        /// <summary>The run of days to show today: it's broken once a whole day passes without a DAILY CLUTCH win.</summary>
        public static int DailyRun(ClutchSaveData d, int today) => d != null && d.dailyDay >= today - 1 ? d.dailyStreak : 0;

        /// <summary>
        /// A DAILY CLUTCH win: the first one each day pays <see cref="DailyBonusSp"/> and extends the run of days
        /// (a missed day starts it again). Returns the bonus (0 for a loss, a repeat, or an old day's game).
        /// </summary>
        public static int ApplyDaily(ClutchSaveData d, int day, int today, bool won, CareerSaveData career)
        {
            // day <= dailyDay also stops a second bonus after the phone's clock is moved back and forth.
            if (d == null || !won || day < 0 || day != today || day <= d.dailyDay) return 0;
            d.dailyStreak = d.dailyDay == day - 1 ? d.dailyStreak + 1 : 1;
            d.dailyBest = Math.Max(d.dailyBest, d.dailyStreak);
            d.dailyDay = day;
            if (career != null) career.signalPoints += DailyBonusSp;
            return DailyBonusSp;
        }

        // ------------------------------------------------------------------ Phase 38: the CLUTCH editor and share codes

        public const string CustomPrefix = "custom:";
        public const int CustomMinClock = 5, CustomMaxClock = 155, CustomMaxScore = 127;
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32
        private const int CodeVersion = 1;
        public const int CodeLength = 13;

        /// <summary>A scenario from the editor's choices (teams are Caller League teams).</summary>
        public static ClutchScenario Make(string yourTeamId, string theirTeamId, bool fullCourt, int clock, int scoreFor, int scoreAgainst, bool yourBall,
                                          ClutchGoal goal, int goalValue, ClutchGoal bonus, int bonusValue)
        {
            return new ClutchScenario
            {
                Id = "custom", Custom = true, Chapter = -1, Title = "CUSTOM", Story = "",
                YourTeamId = yourTeamId, TheirTeamId = theirTeamId, FullCourt = fullCourt,
                Clock = Math.Max(CustomMinClock, Math.Min(CustomMaxClock, clock / 5 * 5)),
                ScoreFor = Math.Max(0, Math.Min(CustomMaxScore, scoreFor)), ScoreAgainst = Math.Max(0, Math.Min(CustomMaxScore, scoreAgainst)),
                YourBall = yourBall,
                Goal = goal, GoalValue = Math.Max(0, Math.Min(31, goalValue)),
                Bonus = bonus, BonusValue = Math.Max(0, Math.Min(31, bonusValue)),
            };
        }

        private static List<TeamDef> CodeTeams(ContentCatalog c)
        {
            var league = c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return league;
        }

        /// <summary>
        /// A 13-character code for a scenario (shown as XXXXX-XXXXX-XXX): version, both teams, half/full court, clock,
        /// both scores, possession, goal and bonus, and a checksum. Null when a team isn't a league team.
        /// </summary>
        public static string Encode(ContentCatalog c, ClutchScenario s)
        {
            if (c == null || s == null) return null;
            var teams = CodeTeams(c);
            int you = teams.FindIndex(t => t.id == s.YourTeamId), them = teams.FindIndex(t => t.id == s.TheirTeamId);
            if (you < 0 || them < 0 || you > 15 || them > 15 || you == them) return null;
            ulong v = 0;
            int bits = 0;
            void Put(int value, int count) { v |= ((ulong)value & ((1UL << count) - 1)) << bits; bits += count; }
            Put(CodeVersion, 3);
            Put(you, 4); Put(them, 4); Put(s.FullCourt ? 1 : 0, 1);
            Put((Math.Max(CustomMinClock, Math.Min(CustomMaxClock, s.Clock)) / 5) - 1, 5);
            Put(Math.Min(CustomMaxScore, s.ScoreFor), 7); Put(Math.Min(CustomMaxScore, s.ScoreAgainst), 7);
            Put(s.YourBall ? 1 : 0, 1);
            Put((int)s.Goal, 4); Put(Math.Min(31, s.GoalValue), 5);
            Put((int)s.Bonus, 4); Put(Math.Min(31, s.BonusValue), 5);
            Put(Checksum(v, bits), 8);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < CodeLength; i++) sb.Append(Alphabet[(int)((v >> (i * 5)) & 31)]);
            return sb.ToString();
        }

        private static int Checksum(ulong v, int bits)
        {
            uint h = 2166136261u;
            for (int i = 0; i < bits; i += 8) { h ^= (uint)((v >> i) & 0xFF); h *= 16777619u; }
            return (int)(h & 0xFF);
        }

        public static string Pretty(string code) =>
            code == null || code.Length != CodeLength ? code : code.Substring(0, 5) + "-" + code.Substring(5, 5) + "-" + code.Substring(10);

        /// <summary>Reads a code (forgiving: case, dashes, spaces, I/L/O typos). <paramref name="error"/> says why not.</summary>
        public static bool TryDecode(string text, out ClutchScenario scenario, out string error) => TryDecode(null, text, out scenario, out error);

        public static bool TryDecode(ContentCatalog c, string text, out ClutchScenario scenario, out string error)
        {
            scenario = null;
            error = "That isn't a CLUTCH code.";
            if (string.IsNullOrEmpty(text)) return false;
            var sb = new System.Text.StringBuilder();
            foreach (char raw in text.Trim())
            {
                char ch = char.ToUpperInvariant(raw);
                if (ch == '-' || ch == ' ') continue;
                if (ch == 'I' || ch == 'L') ch = '1';
                if (ch == 'O') ch = '0';
                if (Alphabet.IndexOf(ch) < 0) return false;
                sb.Append(ch);
            }
            if (sb.Length != CodeLength) return false;
            ulong v = 0;
            for (int i = 0; i < CodeLength; i++) v |= (ulong)Alphabet.IndexOf(sb[i]) << (i * 5);
            int pos = 0;
            int Get(int count) { int r = (int)((v >> pos) & ((1UL << count) - 1)); pos += count; return r; }
            if (Get(3) != CodeVersion) { error = "That code is from a newer version of Retro Hoops."; return false; }
            int you = Get(4), them = Get(4);
            bool full = Get(1) == 1;
            int clock = (Get(5) + 1) * 5;
            int scoreFor = Get(7), scoreAgainst = Get(7);
            bool yourBall = Get(1) == 1;
            int goal = Get(4), goalValue = Get(5), bonus = Get(4), bonusValue = Get(5);
            int dataBits = pos;
            ulong data = v & ((1UL << dataBits) - 1);
            if (Get(8) != Checksum(data, dataBits) || (v >> pos) != 0) { error = "That code has a typo."; return false; }
            if (goal > (int)ClutchGoal.Blocks || bonus > (int)ClutchGoal.Blocks || you == them || clock > CustomMaxClock) return false;
            c = c ?? DefaultContent.Create();
            var teams = CodeTeams(c);
            if (you >= teams.Count || them >= teams.Count) return false;
            scenario = Make(teams[you].id, teams[them].id, full, clock, scoreFor, scoreAgainst, yourBall, (ClutchGoal)goal, goalValue, (ClutchGoal)bonus, bonusValue);
            error = null;
            return true;
        }

        public static MatchRequest CustomRequest(ContentCatalog c, ClutchScenario s, string difficultyId)
        {
            string code = Encode(c, s);
            var r = code == null ? null : Request(c, s, difficultyId);
            if (r == null) return null;
            r.ContextId = ContextPrefix + CustomPrefix + code;
            r.Seed = StableHash.Of("clutch-custom:" + code) | 1u;
            return r;
        }

        // ------------------------------------------------------------------ stars

        public static int StarsFor(ClutchSaveData d, string id)
        {
            if (d?.stars == null || id == null) return 0;
            foreach (var e in d.stars)
            {
                int colon = e.LastIndexOf(':');
                if (colon <= 0 || e.Substring(0, colon) != id) continue;
                return int.TryParse(e.Substring(colon + 1), out int n) ? Math.Max(0, Math.Min(StarsPerScenario, n)) : 0;
            }
            return 0;
        }

        private static void SetStars(ClutchSaveData d, string id, int stars)
        {
            d.stars.RemoveAll(e => e.StartsWith(id + ":", StringComparison.Ordinal));
            if (stars > 0) d.stars.Add(id + ":" + stars);
        }

        public static int TotalStars(ClutchSaveData d)
        {
            int n = 0;
            foreach (var s in All) n += StarsFor(d, s.Id);
            return n;
        }

        public static int MaxStars => All.Length * StarsPerScenario;

        public static bool ChapterOpen(ClutchSaveData d, int chapter) =>
            chapter >= 0 && chapter < ChapterStars.Length && TotalStars(d) >= ChapterStars[chapter];

        public static bool Unlocked(ClutchSaveData d, ClutchScenario s) => s != null && ChapterOpen(d, s.Chapter);

        public static bool AllWon(ClutchSaveData d) => Array.TrueForAll(All, s => StarsFor(d, s.Id) >= 1);

        public static bool Perfect(ClutchSaveData d) => TotalStars(d) >= MaxStars;

        // ------------------------------------------------------------------ goals

        public static string GoalText(ClutchGoal g, int n)
        {
            bool es = Loc.Language == Loc.Spanish;
            string pl = n == 1 ? "" : "s";
            switch (g)
            {
                case ClutchGoal.WinBy: return es ? "Gana por " + n + "+" : "Win by " + n + "+";
                case ClutchGoal.HoldTo:
                    if (n == 0) return es ? "Que no anoten" : "Don't let them score";
                    return es ? "Permite " + n + " punto" + pl + " o menos" : "Allow " + n + " point" + pl + " or fewer";
                case ClutchGoal.YouScore: return es ? "Anota " + n + "+ tú mismo" : "Score " + n + "+ yourself";
                case ClutchGoal.Threes:
                    if (n == 1) return es ? "Mete un tiro desde fuera del arco" : "Hit a shot from beyond the arc";
                    return es ? "Mete " + n + " tiros desde fuera del arco" : "Hit " + n + " shots from beyond the arc";
                case ClutchGoal.Assists: return n + (es ? " asistencia" + (n == 1 ? "" : "s") : " assist" + pl);
                case ClutchGoal.NoTurnovers: return es ? "Sin pérdidas" : "No turnovers";
                case ClutchGoal.Steals: return n + (es ? " robo" + pl : " steal" + pl);
                case ClutchGoal.Blocks: return n + (es ? (n == 1 ? " tapón" : " tapones") : " block" + pl);
                default: return es ? "Gana" : "Win";
            }
        }

        /// <summary>Whether a finished game met <paramref name="g"/> (the game started from the scenario's score).</summary>
        public static bool Met(ClutchScenario s, ClutchGoal g, int n, MatchSummary m)
        {
            if (s == null || m == null) return false;
            var team = m.TeamTotals(m.humanTeam);
            switch (g)
            {
                case ClutchGoal.WinBy: return m.Margin >= n;
                case ClutchGoal.HoldTo: return m.OpponentScore - s.ScoreAgainst <= n;
                case ClutchGoal.YouScore: return (m.HumanLine?.stats?.points ?? 0) >= n;
                case ClutchGoal.Threes: return team.arcMade >= n;
                case ClutchGoal.Assists: return team.assists >= n;
                case ClutchGoal.NoTurnovers: return team.turnovers == 0;
                case ClutchGoal.Steals: return team.steals >= n;
                case ClutchGoal.Blocks: return team.blocks >= n;
                default: return true;
            }
        }

        /// <summary>Stars a finished game earned (0 when lost).</summary>
        public static int StarsEarned(ClutchScenario s, MatchSummary m)
        {
            if (s == null || m == null || !m.HumanWon) return 0;
            return 1 + (Met(s, s.Goal, s.GoalValue, m) ? 1 : 0) + (Met(s, s.Bonus, s.BonusValue, m) ? 1 : 0);
        }

        // ------------------------------------------------------------------ playing

        /// <summary>The scenario as a match: you lead your team from the given score with the given clock.</summary>
        public static MatchRequest Request(ContentCatalog c, ClutchScenario s, string difficultyId)
        {
            if (c == null || s == null) return null;
            var you = c.Team(s.YourTeamId);
            var them = c.Team(s.TheirTeamId);
            if (you == null || them == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.Clutch,
                HomeTeamId = you.id,
                AwayTeamId = them.id,
                CourtId = you.homeCourtId,
                RulesId = s.FullCourt ? Logic.FullCourt.RulesId : RulesId,
                FullCourt = s.FullCourt,
                DifficultyId = difficultyId,
                Seed = StableHash.Of("clutch:" + s.Id) | 1u,
                ContextId = ContextPrefix + s.Id,
                StartScoreA = s.ScoreFor,
                StartScoreB = s.ScoreAgainst,
                StartClock = s.Clock,
                StartWithBall = s.YourBall ? 0 : 1,
            };
        }

        /// <summary>
        /// Records a finished scenario: best stars kept, SP for each new star (paid into the career). Returns the
        /// outcome; <paramref name="stars"/> is what this game earned and <paramref name="newStars"/> how many were new.
        /// </summary>
        public static ClutchOutcome ApplyResult(ClutchSaveData d, ClutchScenario s, MatchSummary m, CareerSaveData career, out int stars, out int newStars)
        {
            stars = 0;
            newStars = 0;
            if (d == null || s == null || m == null) return ClutchOutcome.None;
            if (s.Custom)
            {
                // Editor scenarios are for fun (and friends): no stars, no SP, no record.
                stars = StarsEarned(s, m);
                return m.HumanWon ? ClutchOutcome.Won : ClutchOutcome.Lost;
            }
            d.played++;
            stars = StarsEarned(s, m);
            if (!m.HumanWon) return ClutchOutcome.Lost;
            d.won++;
            int before = StarsFor(d, s.Id);
            if (stars > before)
            {
                newStars = stars - before;
                SetStars(d, s.Id, stars);
                if (career != null) career.signalPoints += newStars * SpPerNewStar;
            }
            return ClutchOutcome.Won;
        }

        /// <summary>Keeps only known scenarios with 1..3 stars, one entry each.</summary>
        public static void Sanitize(ClutchSaveData d)
        {
            if (d == null) return;
            var clean = new List<string>();
            foreach (var s in All)
            {
                int n = StarsFor(d, s.Id);
                if (n > 0) clean.Add(s.Id + ":" + n);
            }
            d.stars = clean;
            d.played = Math.Max(0, d.played);
            d.dailyStreak = Math.Max(0, d.dailyStreak);
            d.dailyBest = Math.Max(d.dailyStreak, d.dailyBest);
            if (d.dailyDay < -1) d.dailyDay = -1;
            d.won = Math.Max(0, Math.Min(d.played, d.won));
        }

        public static string Situation(ClutchScenario s)
        {
            bool es = Loc.Language == Loc.Spanish;
            string score = s.Deficit > 0 ? (es ? "PIERDES POR " : "DOWN ") + s.Deficit
                         : s.Deficit < 0 ? (es ? "GANAS POR " : "UP ") + (-s.Deficit)
                         : es ? "EMPATE" : "TIED";
            return score + "  ·  " + Clock(s.Clock) + "  ·  " + (s.YourBall ? (es ? "TU BALÓN" : "YOUR BALL") : (es ? "SU BALÓN" : "THEIR BALL"))
                   + "  ·  " + (s.FullCourt ? (es ? "CANCHA COMPLETA" : "FULL COURT") : (es ? "MEDIA CANCHA" : "HALF COURT"));
        }

        public static string Clock(int seconds) => (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }
}
