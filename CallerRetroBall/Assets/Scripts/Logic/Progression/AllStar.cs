using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum DunkInput { Up = 0, Right = 1, Down = 2, Left = 3 }

    /// <summary>A dunk in the contest: its name, the combo you enter in the air, and how hard it is (1..5).</summary>
    public sealed class DunkMove
    {
        public string Name;
        public DunkInput[] Combo;
        public int Difficulty;
        /// <summary>Spin of the sprite in the air, in full turns (0, 0.5, 1).</summary>
        public float Turns;
        /// <summary>Flip the jumper to take off from the other side (reverse dunks).</summary>
        public bool Reverse;

        public DunkMove(string name, int difficulty, float turns, bool reverse, params DunkInput[] combo)
        {
            Name = name;
            Difficulty = difficulty;
            Turns = turns;
            Reverse = reverse;
            Combo = combo;
        }
    }

    /// <summary>One dunker in a contest (you or a league player).</summary>
    [Serializable]
    public class ContestEntrant
    {
        public string playerId;
        public string name = "";
        public bool you;
        /// <summary>Round scores (−1 = not yet). Dunks: total of both dunks in the round; 3-point: points.</summary>
        public int r1 = -1, r2 = -1;
        /// <summary>Dunk names used, so judges can mark down repeats.</summary>
        public List<string> used = new List<string>();
    }

    /// <summary>A Dunk Contest or 3-Point Contest in progress.</summary>
    [Serializable]
    public class ContestSaveData
    {
        /// <summary>"dunk" or "three"; empty = no contest running.</summary>
        public string kind = "";
        public uint seed = 1;
        /// <summary>1 = first round, 2 = final, 3 = finished.</summary>
        public int round = 1;
        /// <summary>Dunks: how many dunks you've done in this round (0..2).</summary>
        public int yourDunks;
        public int yourRoundTotal;
        public List<ContestEntrant> field = new List<ContestEntrant>();
        /// <summary>Part of a Rise All-Star Weekend (pays out and counts for the weekend).</summary>
        public bool rise;
        public string difficultyId;
        public string championName = "";
        public bool youWon;

        public bool Active => !string.IsNullOrEmpty(kind) && round < 3;
    }

    /// <summary>Rise Mode All-Star Weekend for the current season, plus all-time contest titles.</summary>
    [Serializable]
    public class AllStarSaveData
    {
        /// <summary>Rise season the weekend below belongs to.</summary>
        public int season;
        public bool dunkDone, threeDone, gameDone;
        public ContestSaveData contest = new ContestSaveData();
        public int dunkTitles, threeTitles, allStarGames, allStarWins, bestDunk;
    }

    /// <summary>The judges' scorecards for one dunk.</summary>
    public sealed class DunkScore
    {
        public readonly int[] Judges = new int[AllStar.JudgeCount];
        public bool Made;
        public int Total { get { int s = 0; foreach (var j in Judges) s += j; return s; } }
    }

    /// <summary>
    /// All-Star Weekend: the Dunk Contest (pick a dunk, enter its combo in the air, time the slam;
    /// five judges score 5–10 each), the 3-Point Contest against the league's best shooters (a
    /// 60-second Shootout round, then a final), and the All-Star Game. In Rise Mode the weekend
    /// opens at mid-season; the contests are also open any time from the PLAY menu.
    /// </summary>
    public static class AllStar
    {
        public const int JudgeCount = 5;
        public const int Entrants = 4;
        public const int DunksPerRound = 2;
        public const int ContestTitleBonus = 150;
        public const int GameWinBonus = 120;
        public const string GameTeamA = "team.allstar.sunrise";
        public const string GameTeamB = "team.allstar.moonlight";
        public const string ThreeContext = "contest:three";
        public static readonly string[] JudgeNames = { "OLD SCHOOL", "HYPE", "TECH", "COACH", "CROWD" };

        public static readonly DunkMove[] Moves =
        {
            new DunkMove("TWO-HAND HAMMER", 1, 0f, false, DunkInput.Up, DunkInput.Down),
            new DunkMove("TOMAHAWK", 1, 0f, false, DunkInput.Up, DunkInput.Up, DunkInput.Down),
            new DunkMove("REVERSE JAM", 2, 0.5f, true, DunkInput.Left, DunkInput.Up, DunkInput.Down),
            new DunkMove("WINDMILL", 2, 0f, false, DunkInput.Down, DunkInput.Left, DunkInput.Up, DunkInput.Right),
            new DunkMove("CRADLE ROCK", 3, 0f, false, DunkInput.Down, DunkInput.Up, DunkInput.Down, DunkInput.Up),
            new DunkMove("DOUBLE CLUTCH", 3, 0f, false, DunkInput.Up, DunkInput.Down, DunkInput.Up, DunkInput.Down, DunkInput.Down),
            new DunkMove("360 SPIN", 3, 1f, false, DunkInput.Right, DunkInput.Down, DunkInput.Left, DunkInput.Up),
            new DunkMove("BETWEEN THE LEGS", 4, 0f, false, DunkInput.Down, DunkInput.Left, DunkInput.Down, DunkInput.Right, DunkInput.Up),
            new DunkMove("BACKBOARD BOUNCE", 4, 0f, true, DunkInput.Right, DunkInput.Up, DunkInput.Left, DunkInput.Up, DunkInput.Down),
            new DunkMove("360 WINDMILL", 5, 1f, false, DunkInput.Right, DunkInput.Down, DunkInput.Left, DunkInput.Up, DunkInput.Left, DunkInput.Down),
            new DunkMove("ORBIT REVERSE", 5, 1f, true, DunkInput.Left, DunkInput.Up, DunkInput.Right, DunkInput.Down, DunkInput.Left, DunkInput.Up, DunkInput.Down),
        };

        public static DunkMove Move(string name) => Array.Find(Moves, m => m.Name == name);

        /// <summary>Seconds you get to enter a combo: longer combos get a little more time.</summary>
        public static float ComboSeconds(DunkMove m) => 1.4f + 0.42f * m.Combo.Length;

        public static string Arrow(DunkInput i) => i == DunkInput.Up ? "UP" : i == DunkInput.Down ? "DOWN" : i == DunkInput.Left ? "LEFT" : "RIGHT";

        // ------------------------------------------------------------------ judging

        /// <summary>
        /// Five judges score 5..10. A made dunk earns points for difficulty, clean execution (combo
        /// entered with time to spare) and the slam timing; each repeat of a dunk you've already done
        /// costs a point. A blown dunk scores 5s and 6s.
        /// </summary>
        public static DunkScore Judge(DunkMove move, bool made, float execution, float timing, int repeats, uint seed)
        {
            var s = new DunkScore { Made = made };
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            execution = Clamp01(execution);
            timing = Clamp01(timing);
            for (int j = 0; j < JudgeCount; j++)
            {
                if (!made)
                {
                    s.Judges[j] = 5 + (rng.NextFloat() < 0.35f ? 1 : 0);
                    continue;
                }
                // Each judge leans on something different.
                float d = move.Difficulty / 5f, e = execution, t = timing;
                float w;
                switch (j)
                {
                    case 0: w = 0.25f * d + 0.45f * e + 0.30f * t; break;  // OLD SCHOOL: clean fundamentals
                    case 1: w = 0.55f * d + 0.15f * e + 0.30f * t; break;  // HYPE: big moves
                    case 2: w = 0.40f * d + 0.40f * e + 0.20f * t; break;  // TECH: difficulty done right
                    case 3: w = 0.30f * d + 0.30f * e + 0.40f * t; break;  // COACH: the finish
                    default: w = 0.45f * d + 0.20f * e + 0.35f * t; break; // CROWD
                }
                float raw = 6f + 4.4f * w + (rng.NextFloat() - 0.5f) * 0.9f - repeats;
                s.Judges[j] = Math.Max(5, Math.Min(10, (int)Math.Round(raw)));
            }
            return s;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>Timing quality of the slam tap from the meter position (0..1, centre of the green = 1).</summary>
        public static float SlamTiming(float meter, float center = 0.5f, float halfWidth = 0.5f)
        {
            float off = Math.Abs(meter - center) / halfWidth;
            return Clamp01(1f - off);
        }

        // ------------------------------------------------------------------ contests

        private static List<PlayerDef> LeaguePlayers(ContentCatalog c)
        {
            var list = new List<PlayerDef>();
            foreach (var t in c.TeamsInTier(TeamTier.League))
                foreach (var id in t.rosterPlayerIds)
                {
                    var p = c.Player(id);
                    if (p != null && !list.Contains(p)) list.Add(p);
                }
            return list;
        }

        /// <summary>Dunkers are picked by finishing and speed; shooters by shooting.</summary>
        public static List<PlayerDef> Invitees(ContentCatalog c, bool dunk, int count, uint seed)
        {
            var pool = LeaguePlayers(c);
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            var keyed = new List<KeyValuePair<float, PlayerDef>>();
            foreach (var p in pool)
            {
                float k = dunk ? p.attributes.finishing + p.attributes.speed : p.attributes.shooting * 2f;
                keyed.Add(new KeyValuePair<float, PlayerDef>(k + rng.NextFloat() * 6f, p));
            }
            keyed.Sort((a, b) => b.Key.CompareTo(a.Key));
            var result = new List<PlayerDef>();
            for (int i = 0; i < Math.Min(count, keyed.Count); i++) result.Add(keyed[i].Value);
            return result;
        }

        /// <summary>Starts a contest: you plus three league stars.</summary>
        public static ContestSaveData Start(ContentCatalog c, string kind, string yourName, uint seed, bool rise, string difficultyId = null)
        {
            var s = new ContestSaveData { kind = kind, seed = seed == 0 ? 1u : seed, rise = rise, difficultyId = difficultyId ?? DefaultContent.DefaultDifficultyId };
            s.field.Add(new ContestEntrant { you = true, name = string.IsNullOrEmpty(yourName) ? "YOU" : yourName });
            foreach (var p in Invitees(c, kind == "dunk", Entrants - 1, seed))
                s.field.Add(new ContestEntrant { playerId = p.id, name = p.DisplayName });
            SimulateRound(s, c, 1);
            return s;
        }

        public static ContestEntrant You(ContestSaveData s) => s.field.Find(e => e.you);

        /// <summary>Everyone but you in the round: their scores are simulated up front (you see what to beat).</summary>
        private static void SimulateRound(ContestSaveData s, ContentCatalog c, int round)
        {
            for (int i = 0; i < s.field.Count; i++)
            {
                var e = s.field[i];
                if (e.you) continue;
                if (round == 2 && !InFinal(s, e)) continue;
                var p = c.Player(e.playerId);
                uint seed = StableHash.Of(s.seed + ":" + round + ":" + i);
                int score = s.kind == "dunk" ? SimulateDunks(p, e, seed) : SimulateThrees(p, c.Difficulty(s.difficultyId), seed);
                if (round == 1) e.r1 = score; else e.r2 = score;
            }
        }

        private static int SimulateDunks(PlayerDef p, ContestEntrant e, uint seed)
        {
            var rng = new SeededRandom(seed);
            float skill = p == null ? 0.5f : Clamp01((p.attributes.finishing + p.attributes.speed) / 2f / 99f);
            int total = 0;
            for (int d = 0; d < DunksPerRound; d++)
            {
                // Better dunkers try harder dunks and land them more often.
                int maxDiff = 1 + (int)Math.Round(skill * 3f) + (rng.NextFloat() < 0.3f ? 1 : 0);
                var options = Array.FindAll(Moves, m => m.Difficulty <= Math.Min(5, maxDiff) && !e.used.Contains(m.Name));
                if (options.Length == 0) options = Moves;
                var move = options[rng.Range(Math.Max(0, options.Length - 3), options.Length)];
                float makeChance = 0.55f + 0.4f * skill - 0.06f * move.Difficulty;
                bool made = rng.NextFloat() < makeChance || rng.NextFloat() < makeChance; // two attempts
                float exec = 0.2f + 0.55f * skill * (0.6f + 0.4f * rng.NextFloat());
                float timing = 0.15f + 0.65f * rng.NextFloat() * (0.5f + 0.5f * skill);
                int repeats = e.used.FindAll(n => n == move.Name).Count;
                e.used.Add(move.Name);
                total += Judge(move, made, exec, timing, repeats, rng.NextUInt()).Total;
            }
            return total;
        }

        /// <summary>A star's 60-second round: the same simulated shooter as the Shootout's CPU.</summary>
        public static int SimulateThrees(PlayerDef p, DifficultyDef difficulty, uint seed) =>
            Shootout.SimulateCpu(p, difficulty, ShotTuning.Default, seed == 0 ? 1u : seed, CourtGeometry.Default.arcRadius + 0.7f);

        /// <summary>The two best first-round scores go to the final (ties: you lose, the star stays).</summary>
        public static List<ContestEntrant> Finalists(ContestSaveData s)
        {
            var order = new List<ContestEntrant>(s.field);
            order.Sort((a, b) => b.r1 != a.r1 ? b.r1.CompareTo(a.r1) : (a.you ? 1 : 0).CompareTo(b.you ? 1 : 0));
            return order.GetRange(0, Math.Min(2, order.Count));
        }

        public static bool InFinal(ContestSaveData s, ContestEntrant e) => s.round >= 2 && Finalists(s).Contains(e);

        /// <summary>The score you need this round: the 2nd-best star in round one, the other finalist in the final.</summary>
        public static int Target(ContestSaveData s)
        {
            if (s.round == 1)
            {
                var others = s.field.FindAll(e => !e.you);
                others.Sort((a, b) => b.r1.CompareTo(a.r1));
                return others.Count >= 2 ? others[1].r1 + 1 : 0;
            }
            var rival = Finalists(s).Find(e => !e.you);
            return rival != null ? rival.r2 + 1 : 0;
        }

        /// <summary>Records one of your dunks. After your second dunk in a round the round is scored.</summary>
        public static DunkScore RecordDunk(ContestSaveData s, ContentCatalog c, DunkMove move, bool made, float execution, float timing)
        {
            if (s == null || s.kind != "dunk" || !s.Active) return null;
            var me = You(s);
            int repeats = me.used.FindAll(n => n == move.Name).Count;
            me.used.Add(move.Name);
            var score = Judge(move, made, execution, timing, repeats, StableHash.Of(s.seed + ":you:" + s.round + ":" + s.yourDunks));
            s.yourRoundTotal += score.Total;
            s.yourDunks++;
            if (s.yourDunks >= DunksPerRound)
            {
                FinishYourRound(s, c, s.yourRoundTotal);
                s.yourDunks = 0;
                s.yourRoundTotal = 0;
            }
            return score;
        }

        /// <summary>Records your 3-point round (points from the Shootout).</summary>
        public static void RecordThrees(ContestSaveData s, ContentCatalog c, int points)
        {
            if (s == null || s.kind != "three" || !s.Active) return;
            FinishYourRound(s, c, Math.Max(0, points));
        }

        private static void FinishYourRound(ContestSaveData s, ContentCatalog c, int total)
        {
            var me = You(s);
            if (s.round == 1)
            {
                me.r1 = total;
                s.round = 2;
                SimulateRound(s, c, 2);
                if (!InFinal(s, me)) Decide(s);
            }
            else
            {
                me.r2 = total;
                Decide(s);
            }
        }

        private static void Decide(ContestSaveData s)
        {
            var finals = Finalists(s);
            if (finals.Count == 0) return;
            // Final ties go to the better first round.
            finals.Sort((a, b) => b.r2 != a.r2 ? b.r2.CompareTo(a.r2) : b.r1.CompareTo(a.r1));
            s.championName = finals[0].name;
            s.youWon = finals[0].you;
            s.round = 3;
        }

        public static string Title(string kind) => kind == "dunk" ? "DUNK CONTEST" : "3-POINT CONTEST";

        /// <summary>The 3-Point Contest round you shoot next, as a Shootout drill whose "CPU" score is the one to beat.</summary>
        public static MatchRequest ThreeRound(ContestSaveData s, string difficultyId)
        {
            var req = MatchRequest.PracticeDefault();
            req.Drill = (int)DrillKind.Shootout;
            req.ContextId = ThreeContext + ":r" + s.round;
            req.DifficultyId = difficultyId;
            req.Seed = StableHash.Of(s.seed + ":shots:" + s.round);
            return req;
        }

        // ------------------------------------------------------------------ results & Rise weekend

        /// <summary>Pays out a finished contest once: titles, SP and fans (Rise contests mark the weekend event done).</summary>
        public static int Settle(CareerSaveData career, ContestSaveData s)
        {
            if (career == null || s == null || s.round != 3 || string.IsNullOrEmpty(s.kind)) return 0;
            var a = career.allStar;
            int bonus = 0;
            if (s.youWon)
            {
                if (s.kind == "dunk") a.dunkTitles++; else a.threeTitles++;
                bonus = ContestTitleBonus;
                career.signalPoints += bonus;
                career.fans += 40;
            }
            else career.fans += 10;
            if (s.kind == "dunk")
            {
                var me = You(s);
                a.bestDunk = Math.Max(a.bestDunk, Math.Max(me.r1, me.r2));
            }
            if (s.rise)
            {
                var w = Weekend(career);
                if (s.kind == "dunk") w.dunkDone = true; else w.threeDone = true;
            }
            s.kind = "";
            return bonus;
        }

        /// <summary>The weekend opens halfway through a Rise league season and stays open until the regular season ends.</summary>
        public static bool WeekendOpen(CareerSaveData career)
        {
            var r = career?.rise;
            if (r == null || r.stage != RiseStage.Season || r.season == null) return false;
            return r.season.currentWeek >= Math.Max(1, r.season.weeks / 2) && !SeasonEngine.RegularSeasonComplete(r.season);
        }

        /// <summary>Resets the weekend record when a new Rise season reaches its break.</summary>
        public static AllStarSaveData Weekend(CareerSaveData career)
        {
            var a = career.allStar ?? (career.allStar = new AllStarSaveData());
            int season = career.rise?.season?.seasonNumber ?? 0;
            if (a.season != season)
            {
                a.season = season;
                a.dunkDone = a.threeDone = a.gameDone = false;
            }
            return a;
        }

        /// <summary>
        /// All-Star Game teams: the league's best ten split into two original sides by a snake draft,
        /// with you leading Team Sunrise. Registered in the catalog so the match can load them.
        /// </summary>
        public static void RegisterTeams(ContentCatalog c)
        {
            var stars = LeaguePlayers(c);
            stars.Sort((a, b) => b.attributes.Overall != a.attributes.Overall ? b.attributes.Overall.CompareTo(a.attributes.Overall) : string.CompareOrdinal(a.id, b.id));
            var a = new List<string>();
            var b = new List<string>();
            for (int i = 0; i < Math.Min(13, stars.Count); i++)
            {
                // Sunrise keeps a spot for you, so Moonlight picks first: B A A B B A A B ...
                bool toB = (i % 4 == 0 || i % 4 == 3);
                (toB ? b : a).Add(stars[i].id);
            }
            Upsert(c, GameTeamA, "Team", "Sunrise", "SUN", "#FF8C42", "#FFD166", "#7A1E2C", LogoShape.Circle, LogoMotif.Comet, a);
            Upsert(c, GameTeamB, "Team", "Moonlight", "MNL", "#3A5BD9", "#A8B2BD", "#14213D", LogoShape.Shield, LogoMotif.Owl, b);
        }

        private static void Upsert(ContentCatalog c, string id, string city, string nick, string abbr, string p, string s, string a,
                                   LogoShape shape, LogoMotif motif, List<string> roster)
        {
            var t = c.Team(id);
            if (t == null) c.Teams.Add(t = new TeamDef { id = id, tier = TeamTier.Franchise });
            t.city = city;
            t.nickname = nick;
            t.abbreviation = abbr;
            t.primary = RgbColor.FromHex(p);
            t.secondary = RgbColor.FromHex(s);
            t.accent = RgbColor.FromHex(a);
            t.logoShape = shape;
            t.logoMotif = motif;
            t.pattern = TeamPattern.Stripes;
            t.homeCourtId = "court.orbit_dome";
            t.motto = "All-Star Weekend.";
            t.rosterPlayerIds = roster;
        }

        /// <summary>The All-Star Game: Full Court, you start for Team Sunrise.</summary>
        public static MatchRequest GameRequest(ContentCatalog c, CareerSaveData career, string difficultyId)
        {
            RegisterTeams(c);
            string court = c.Team(GameTeamA)?.homeCourtId;
            if (court == null || c.Court(court) == null) court = c.TeamsInTier(TeamTier.League)[0].homeCourtId;
            return new MatchRequest
            {
                Mode = GameMode.AllStar,
                HomeTeamId = GameTeamA,
                AwayTeamId = GameTeamB,
                CourtId = court,
                RulesId = FullCourt.RulesId,
                FullCourt = true,
                DifficultyId = difficultyId,
                ContextId = "allstar:s" + (career?.rise?.season?.seasonNumber ?? 0),
                HumanPlayer = career != null ? PlayerCreator.ForMatch(career, c) : null,
            };
        }

        /// <summary>Records the All-Star Game (once per Rise season pays the win bonus).</summary>
        public static int ApplyGame(CareerSaveData career, MatchSummary s)
        {
            if (career == null || s == null || s.mode != GameMode.AllStar) return 0;
            var a = career.allStar;
            a.allStarGames++;
            int bonus = 0;
            if (s.HumanWon)
            {
                a.allStarWins++;
                if (WeekendOpen(career) && !Weekend(career).gameDone)
                {
                    bonus = GameWinBonus;
                    career.signalPoints += bonus;
                }
            }
            if (WeekendOpen(career)) Weekend(career).gameDone = true;
            career.fans += 25;
            return bonus;
        }
    }
}
