using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Where your Legacy career is.</summary>
    public enum LegacyStage { HighSchool = 0, College = 1, Draft = 2, Pro = 3, Retired = 4 }

    /// <summary>Your line in one Legacy game.</summary>
    [Serializable]
    public class LegacyGameLine
    {
        public int week, round;
        public string opponent = "";
        public bool won, simmed;
        public int us, them, pts, ast, reb, stl, blk;
        public string grade = "";
    }

    /// <summary>A season or stage in the history book.</summary>
    [Serializable]
    public class LegacySeason
    {
        public string label = "";
        public string team = "";
        public int age, games, wins, pts, ast, reb;
        public string result = "";
        public List<string> awards = new List<string>();
        public float Ppg => games == 0 ? 0f : pts / (float)games;
    }

    /// <summary>A contract or college offer waiting for your answer.</summary>
    [Serializable]
    public class LegacyOffer
    {
        public string teamId = "";
        public string name = "";
        /// <summary>Pro: salary per season in thousands of dollars. College: school tier 0..2.</summary>
        public int value;
        public int years;
        public string pitch = "";
    }

    [Serializable]
    public class LegacySaveData
    {
        public bool active;
        public uint seed = 1;
        public LegacyStage stage;
        public int age = 17;
        /// <summary>Pro seasons started (0 before the draft).</summary>
        public int proSeason;
        /// <summary>Your team: the school (generated) or a league team id.</summary>
        public string teamId = "";
        public string schoolName = "";
        public string collegeName = "";
        public int collegeTier;
        public SeasonSaveData season;
        public List<LegacyGameLine> games = new List<LegacyGameLine>();
        public int xp, skillPoints, trainerSessions;
        public List<string> skills = new List<string>();
        /// <summary>Natural growth (and decline) from aging, added to every rating.</summary>
        public int growth;
        public int fans;
        public long cash;
        /// <summary>Coach's trust 0..100: story choices and good games raise it.</summary>
        public int trust = 50;
        /// <summary>High-school recruiting stars (1..5) and draft stock (0..100).</summary>
        public int stars, stock;
        public int draftPick;
        /// <summary>Thousands of dollars per season, and seasons left.</summary>
        public int salary, contractYears;
        public List<string> endorsements = new List<string>();
        public List<LegacyOffer> offers = new List<LegacyOffer>();
        public List<LegacySeason> history = new List<LegacySeason>();
        public List<string> seenEvents = new List<string>();
        public string pendingEvent;
        public int legacyPoints;
        public bool hallOfFame;
    }

    /// <summary>A skill-tree node: costs points, needs a node from the tier below in its branch.</summary>
    public sealed class LegacySkill
    {
        public string Id, Name, Branch;
        public int Tier, Cost;
        public (AttributeType attr, int amount)[] Bonus;
        public LegacySkill(string id, string name, string branch, int tier, params (AttributeType, int)[] bonus)
        {
            Id = id; Name = name; Branch = branch; Tier = tier; Cost = tier; Bonus = bonus;
        }
        public string Describe()
        {
            var parts = new List<string>();
            foreach (var b in Bonus) parts.Add("+" + b.amount + " " + b.attr.ToString().ToUpperInvariant());
            return string.Join("  ", parts);
        }
    }

    /// <summary>A sponsor (all invented) that pays every pro season once you have enough fans.</summary>
    public sealed class LegacyBrand
    {
        public string Id, Name, Pitch;
        public int MinFans;
        /// <summary>Thousands of dollars a season.</summary>
        public int Pay;
        public LegacyBrand(string id, string name, int minFans, int pay, string pitch) { Id = id; Name = name; MinFans = minFans; Pay = pay; Pitch = pitch; }
    }

    /// <summary>A story moment with two or three choices.</summary>
    public sealed class LegacyEvent
    {
        public string Id, Title, Body;
        public LegacyStage[] Stages;
        public (string label, int fans, int cash, int trust, int xp, int points)[] Choices;
    }

    /// <summary>
    /// LEGACY: your created player's whole career. A senior year of high school (recruiting stars), a
    /// college season or the jump straight to the draft, draft night, then pro seasons with a Caller
    /// League team until you retire: XP and a skill tree, coach's trust and your role, contracts and an
    /// agent's offers, sponsors (all invented brands), story choices, awards, and a Hall of Fame vote.
    /// Every game is Full Court with you at the top of the lineup; you can also simulate a game.
    /// </summary>
    public static class Legacy
    {
        public const string YouId = "player.legacy.you";
        public const string SchoolId = "team.lg.school";
        public const string TeamPrefix = "team.lg.";
        public const int XpPerLevel = 100;
        public const int MaxRating = 95;
        /// <summary>Overall rating you start your senior year at, whatever your player's base.</summary>
        public const int StartOverall = 52;
        public const int MaxEndorsements = 3;
        public const int RetireAge = 38;
        public const int CanRetireAge = 33;
        public const int HallOfFameLegacy = 400;
        public const int HighSchoolWeeks = 7;
        public const int CollegeWeeks = 7;
        public const int ProWeeks = 10;

        // ------------------------------------------------------------------ content

        public static readonly LegacySkill[] Skills =
        {
            new LegacySkill("sc1a", "SOFT TOUCH", "SCORING", 1, (AttributeType.Finishing, 3)),
            new LegacySkill("sc1b", "CATCH & SHOOT", "SCORING", 1, (AttributeType.Shooting, 3)),
            new LegacySkill("sc2a", "FLOATER", "SCORING", 2, (AttributeType.Finishing, 2), (AttributeType.Clutch, 2)),
            new LegacySkill("sc2b", "DEEP RANGE", "SCORING", 2, (AttributeType.Shooting, 4)),
            new LegacySkill("sc3a", "ICE VEINS", "SCORING", 3, (AttributeType.Clutch, 5)),
            new LegacySkill("sc3b", "ACROBAT", "SCORING", 3, (AttributeType.Finishing, 4), (AttributeType.Speed, 1)),
            new LegacySkill("sc4a", "BUCKET GETTER", "SCORING", 4, (AttributeType.Finishing, 3), (AttributeType.Shooting, 3), (AttributeType.Clutch, 2)),
            new LegacySkill("sc4b", "MICROWAVE", "SCORING", 4, (AttributeType.Shooting, 5), (AttributeType.Clutch, 3)),
            new LegacySkill("pm1a", "TIGHT HANDLE", "PLAYMAKING", 1, (AttributeType.Playmaking, 3)),
            new LegacySkill("pm1b", "QUICK FIRST STEP", "PLAYMAKING", 1, (AttributeType.Speed, 3)),
            new LegacySkill("pm2a", "COURT VISION", "PLAYMAKING", 2, (AttributeType.Playmaking, 4)),
            new LegacySkill("pm2b", "OPEN FLOOR", "PLAYMAKING", 2, (AttributeType.Speed, 3), (AttributeType.Stamina, 1)),
            new LegacySkill("pm3a", "FLOOR GENERAL", "PLAYMAKING", 3, (AttributeType.Playmaking, 4), (AttributeType.Clutch, 1)),
            new LegacySkill("pm3b", "JETS", "PLAYMAKING", 3, (AttributeType.Speed, 5)),
            new LegacySkill("pm4a", "MAESTRO", "PLAYMAKING", 4, (AttributeType.Playmaking, 5), (AttributeType.Speed, 2), (AttributeType.Clutch, 1)),
            new LegacySkill("pm4b", "BLUR", "PLAYMAKING", 4, (AttributeType.Speed, 5), (AttributeType.Finishing, 3)),
            new LegacySkill("df1a", "ACTIVE HANDS", "DEFENSE", 1, (AttributeType.Defense, 3)),
            new LegacySkill("df1b", "BOX OUT", "DEFENSE", 1, (AttributeType.Rebounding, 3)),
            new LegacySkill("df2a", "LOCKDOWN", "DEFENSE", 2, (AttributeType.Defense, 4)),
            new LegacySkill("df2b", "MOTOR", "DEFENSE", 2, (AttributeType.Stamina, 4)),
            new LegacySkill("df3a", "RIM PROTECTOR", "DEFENSE", 3, (AttributeType.Defense, 3), (AttributeType.Rebounding, 2)),
            new LegacySkill("df3b", "GLASS EATER", "DEFENSE", 3, (AttributeType.Rebounding, 5)),
            new LegacySkill("df4a", "ANCHOR", "DEFENSE", 4, (AttributeType.Defense, 5), (AttributeType.Stamina, 3)),
            new LegacySkill("df4b", "IRONMAN", "DEFENSE", 4, (AttributeType.Stamina, 5), (AttributeType.Rebounding, 3)),
        };

        public static readonly string[] Branches = { "SCORING", "PLAYMAKING", "DEFENSE" };

        public static readonly LegacyBrand[] Brands =
        {
            new LegacyBrand("brand.skyline", "SKYLINE KICKS", 300, 400, "A signature shoe colourway with your number on it."),
            new LegacyBrand("brand.pixelpop", "PIXEL POP SODA", 150, 180, "Your face on a limited-edition can."),
            new LegacyBrand("brand.boltbar", "BOLT BARS", 80, 90, "The official snack of the comeback."),
            new LegacyBrand("brand.neon", "NEON THREADS", 500, 650, "Courtside streetwear line."),
            new LegacyBrand("brand.circuit", "CIRCUIT PHONES", 900, 1100, "A billboard downtown and a commercial shoot."),
            new LegacyBrand("brand.rimshot", "RIMSHOT AUDIO", 220, 260, "Pre-game playlist ads."),
        };

        public static readonly LegacyEvent[] Events =
        {
            E("ev.hs.film", "FILM SESSION", "Coach invites you to watch film after practice. Your friends are going to the movies.",
              new[] { LegacyStage.HighSchool }, ("STAY FOR FILM", 0, 0, 10, 40, 0), ("GO TO THE MOVIES", 15, 0, -8, 0, 0)),
            E("ev.hs.mixtape", "MIXTAPE", "A local filmer wants to cut a highlight mixtape of your season.",
              new[] { LegacyStage.HighSchool }, ("SAY YES", 60, 0, 0, 0, 0), ("KEEP IT LOW-KEY", 0, 0, 5, 20, 0)),
            E("ev.hs.camp", "SUMMER CAMP", "A skills camp has one spot left. It costs your summer job money.",
              new[] { LegacyStage.HighSchool, LegacyStage.College }, ("GO TO CAMP", 0, -1, 0, 0, 1), ("KEEP THE JOB", 0, 2, 0, 10, 0)),
            E("ev.col.class", "MIDTERMS", "Midterms and a road trip in the same week.",
              new[] { LegacyStage.College }, ("STUDY ON THE BUS", 0, 0, 8, 20, 0), ("ALL BASKETBALL", 10, 0, -4, 40, 0)),
            E("ev.col.rival", "RIVALRY WEEK", "The student section wants you to talk some trash before the big game.",
              new[] { LegacyStage.College }, ("BRING THE NOISE", 50, 0, -5, 0, 0), ("LET THE GAME TALK", 10, 0, 8, 10, 0)),
            E("ev.draft.workout", "PRE-DRAFT WORKOUT", "Two clubs want private workouts the same day.",
              new[] { LegacyStage.Draft }, ("DO BOTH", 0, 0, 0, 60, 0), ("REST UP", 0, 0, 5, 0, 0)),
            E("ev.pro.vet", "THE VETERAN", "The team's veteran offers to show you his morning routine. It starts at 5 a.m.",
              new[] { LegacyStage.Pro }, ("SET THE ALARM", 0, 0, 10, 50, 0), ("SLEEP IN", 0, 0, -5, 0, 0)),
            E("ev.pro.bench", "COMING OFF THE BENCH", "Coach wants to try you as the sixth man for a few games.",
              new[] { LegacyStage.Pro }, ("WHATEVER HELPS", 0, 0, 12, 20, 0), ("ASK TO START", 20, 0, -10, 0, 0)),
            E("ev.pro.charity", "COMMUNITY DAY", "Your foundation can host a free clinic at the park where you learned to play.",
              new[] { LegacyStage.Pro }, ("HOST IT", 120, -2, 5, 0, 0), ("SEND A DONATION", 40, -1, 0, 0, 0)),
            E("ev.pro.podcast", "PODCAST INVITE", "A popular hoops podcast wants you on the show.",
              new[] { LegacyStage.Pro }, ("GO ON THE SHOW", 90, 0, -3, 0, 0), ("SKIP IT", 0, 0, 3, 10, 0)),
            E("ev.pro.trainer", "PRIVATE TRAINER", "A famous skills trainer has an opening this summer.",
              new[] { LegacyStage.Pro }, ("HIRE THEM", 0, -3, 0, 0, 1), ("TRAIN ALONE", 0, 0, 0, 40, 0)),
            E("ev.pro.injury", "SORE ANKLE", "Your ankle is sore. The trainer says one game of rest would help.",
              new[] { LegacyStage.Pro }, ("PLAY THROUGH IT", 30, 0, 8, 0, 0), ("TAKE THE REST", 0, 0, -2, 30, 0)),
        };

        private static LegacyEvent E(string id, string title, string body, LegacyStage[] stages,
                                     params (string, int, int, int, int, int)[] choices) =>
            new LegacyEvent { Id = id, Title = title, Body = body, Stages = stages, Choices = choices };

        private static readonly string[] SchoolNames =
        {
            "Central High|Cougars|CHS|#7A1E2C|#FFD166", "Bayside Prep|Gulls|BSP|#1FB5A6|#F4F1DE", "Cedar Ridge|Rams|CRH|#2A9D4F|#F4F1DE",
            "Northgate|Knights|NGK|#3A5BD9|#A8B2BD", "Riverside|Otters|RVO|#4CC9F0|#14213D", "Hilltop Academy|Hornets|HTA|#FFD166|#1A1A1F",
            "Southpoint|Spartans|SPS|#E63946|#F4F1DE", "Valley Tech|Volts|VTV|#7B2CBF|#FFD166",
        };
        private static readonly string[] CollegeNames =
        {
            "Pacific State|Mariners|PCS|#14213D|#4CC9F0", "Mesa Tech|Roadrunners|MTR|#FF8C42|#1A1A1F", "Lakeshore|Loons|LKL|#3A5BD9|#F4F1DE",
            "Granite U|Goats|GUG|#A8B2BD|#7A1E2C", "Sunbelt|Suns|SBS|#FFD166|#E63946", "Evergreen|Firs|EVF|#2A9D4F|#FFD166",
            "Ironwood|Ironmen|IWI|#58586A|#FF8C42", "Coastal A&M|Tides|CAM|#1FB5A6|#14213D",
        };

        // ------------------------------------------------------------------ start

        public static LegacySaveData Start(ContentCatalog c, uint seed)
        {
            var s = new LegacySaveData { active = true, seed = seed == 0 ? 1u : seed, stage = LegacyStage.HighSchool, age = 17 };
            s.schoolName = "Central High";
            s.teamId = SchoolId;
            NewSchoolSeason(c, s, HighSchoolWeeks, false);
            return s;
        }

        /// <summary>Generated school teams for this stage (high school or college), registered in the catalog.</summary>
        public static void Register(ContentCatalog c, LegacySaveData s, CareerSaveData career)
        {
            if (c == null || s == null || !s.active) return;
            bool college = s.stage == LegacyStage.College;
            if (s.stage == LegacyStage.HighSchool || college)
            {
                var names = college ? CollegeNames : SchoolNames;
                int mine = college ? CollegeIndex(s) : 0;
                int level = college ? 56 + s.collegeTier * 2 : 49;
                int slot = 1;
                for (int i = 0; i < names.Length; i++)
                {
                    bool yours = i == mine;
                    string id = yours ? SchoolId : TeamPrefix + (college ? "col." : "hs.") + slot++;
                    UpsertTeam(c, id, names[i].Split('|'), level + (yours ? 2 : (i % 3) - 1), s.seed, college);
                }
            }
            // You, as the matches need you.
            var you = You(c, s, career);
            var def = c.Player(YouId);
            if (def == null) c.Players.Add(you);
            else { def.attributes = you.attributes; def.appearance = you.appearance; def.firstName = you.firstName; def.jerseyNumber = you.jerseyNumber; def.archetypeId = you.archetypeId; }
        }

        private static int CollegeIndex(LegacySaveData s)
        {
            int i = Array.FindIndex(CollegeNames, n => n.StartsWith(s.collegeName + "|", StringComparison.Ordinal));
            return i < 0 ? 0 : i;
        }

        private static void UpsertTeam(ContentCatalog c, string id, string[] parts, int level, uint seed, bool college)
        {
            var team = c.Team(id);
            if (team == null) c.Teams.Add(team = new TeamDef { id = id, tier = TeamTier.Franchise });
            team.city = parts[0];
            team.nickname = parts[1];
            team.abbreviation = parts[2];
            team.primary = RgbColor.FromHex(parts[3]);
            team.secondary = RgbColor.FromHex(parts[4]);
            team.accent = RgbColor.FromHex("#1A1A1F");
            team.logoShape = (LogoShape)(StableHash.Of(id) % 5);
            team.logoMotif = (LogoMotif)(StableHash.Of(id + "m") % 10);
            team.pattern = TeamPattern.Stripes;
            team.homeCourtId = college ? "court.orbit_dome" : "court.grove_gym";
            if (c.Court(team.homeCourtId) == null) team.homeCourtId = "court.overpass_park";
            team.motto = college ? "Go " + parts[1] + "!" : "Home of the " + parts[1] + ".";
            team.rosterPlayerIds.Clear();
            for (int k = 0; k < 7; k++)
            {
                string pid = id.Replace("team.", "player.") + "." + (seed % 997) + "." + k;
                if (c.Player(pid) == null) c.Players.Add(MakePlayer(c, pid, level - (k >= 5 ? 3 : 0)));
                team.rosterPlayerIds.Add(pid);
            }
        }

        private static readonly string[] Firsts = { "Jaylen", "Marcus", "Theo", "Andre", "Kobe", "Darius", "Eli", "Malik", "Isaiah", "Ty", "Cam", "Noah", "Jalen", "Reggie", "Omar", "Luis" };
        private static readonly string[] Lasts = { "Brooks", "Carter", "Diaz", "Ellis", "Fowler", "Grant", "Hayes", "Irving", "Jordan", "Kim", "Lowe", "Moss", "Nash", "Ortiz", "Pryor", "Reyes" };

        private static PlayerDef MakePlayer(ContentCatalog c, string id, int overall)
        {
            uint h = StableHash.Of(id);
            var arch = c.Archetypes[(int)(h % (uint)c.Archetypes.Count)];
            var baseAttrs = DefaultContent.Personalize(arch.baseline, id);
            return new PlayerDef
            {
                id = id, firstName = Firsts[(h >> 4) % (uint)Firsts.Length], lastName = Lasts[(h >> 9) % (uint)Lasts.Length],
                jerseyNumber = (int)(h % 55) + 1, archetypeId = arch.id,
                attributes = baseAttrs.Offset(overall - baseAttrs.Overall),
                appearance = DefaultContent.AppearanceFromSeed(id, arch.archetype),
            };
        }

        // ------------------------------------------------------------------ ratings

        public static LegacySkill Skill(string id) => Array.Find(Skills, k => k.Id == id);

        /// <summary>Your ratings: your player's shape scaled to a high-school senior (overall 52), plus aging and the skill tree (max 95).</summary>
        public static AttributeSet Ratings(PlayerDef basePlayer, LegacySaveData s)
        {
            var a = basePlayer.attributes.Offset(StartOverall - basePlayer.attributes.Overall + s.growth);
            foreach (var id in s.skills)
            {
                var k = Skill(id);
                if (k == null) continue;
                foreach (var b in k.Bonus) a = a.With(b.attr, Math.Min(MaxRating, a.Get(b.attr) + b.amount));
            }
            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var t = (AttributeType)i;
                a = a.With(t, Math.Min(MaxRating, RatingScale.Clamp(a.Get(t))));
            }
            return a;
        }

        public static PlayerDef You(ContentCatalog c, LegacySaveData s, CareerSaveData career)
        {
            var b = PlayerCreator.BasePlayer(career, c);
            return new PlayerDef
            {
                id = YouId, firstName = b.firstName, lastName = b.lastName, jerseyNumber = b.jerseyNumber, archetypeId = b.archetypeId,
                attributes = Ratings(b, s), appearance = b.appearance,
            };
        }

        public static int Level(LegacySaveData s) => 1 + s.xp / XpPerLevel;

        /// <summary>Why a skill can't be learned yet, or null.</summary>
        public static string CannotLearn(LegacySaveData s, string id)
        {
            var k = Skill(id);
            if (k == null) return "Unknown skill.";
            if (s.skills.Contains(id)) return "Already learned.";
            if (s.skillPoints < k.Cost) return "Needs " + k.Cost + " skill point" + (k.Cost == 1 ? "" : "s") + ".";
            if (k.Tier > 1 && !s.skills.Exists(o => Skill(o) is LegacySkill p && p.Branch == k.Branch && p.Tier == k.Tier - 1))
                return "Learn a tier " + (k.Tier - 1) + " " + k.Branch + " skill first.";
            return null;
        }

        public static bool Learn(LegacySaveData s, string id)
        {
            if (CannotLearn(s, id) != null) return false;
            s.skillPoints -= Skill(id).Cost;
            s.skills.Add(id);
            return true;
        }

        private static void AddXp(LegacySaveData s, int xp)
        {
            int before = Level(s);
            s.xp = Math.Max(0, s.xp + xp);
            s.skillPoints += Math.Max(0, Level(s) - before);
        }

        /// <summary>Trainer sessions cost more each time; each one is a skill point.</summary>
        public static long TrainerCost(LegacySaveData s) => 15_000L * (s.trainerSessions + 1);

        public static bool HireTrainer(LegacySaveData s)
        {
            if (s.stage != LegacyStage.Pro || s.cash < TrainerCost(s)) return false;
            s.cash -= TrainerCost(s);
            s.trainerSessions++;
            s.skillPoints++;
            return true;
        }

        // ------------------------------------------------------------------ grading

        public static float GameScore(int pts, int ast, int reb, int stl, int blk, int tov, bool won) =>
            pts + 1.5f * ast + 1.2f * reb + 2f * stl + 2f * blk - tov + (won ? 4f : 0f);

        public static string Grade(float score)
        {
            if (score >= 28) return "A+";
            if (score >= 22) return "A";
            if (score >= 17) return "B+";
            if (score >= 13) return "B";
            if (score >= 10) return "C+";
            if (score >= 7) return "C";
            if (score >= 4) return "D";
            return "F";
        }

        public static int GradeXp(float score) => Math.Min(150, 20 + (int)Math.Round(4f * Math.Max(0f, score)));

        private static float GradeValue(string g)
        {
            switch (g)
            {
                case "A+": return 30; case "A": return 24; case "B+": return 19; case "B": return 15;
                case "C+": return 11; case "C": return 8; case "D": return 5; default: return 2;
            }
        }

        // ------------------------------------------------------------------ seasons

        private static void NewSchoolSeason(ContentCatalog c, LegacySaveData s, int weeks, bool college)
        {
            var ids = new List<string> { SchoolId };
            for (int i = 1; i < 8; i++) ids.Add(TeamPrefix + (college ? "col." : "hs.") + i);
            s.season = Schedule(ids, SchoolId, weeks, StableHash.Of("lg:" + s.seed + ":" + s.stage));
            s.games.Clear();
        }

        /// <summary>A round-robin-ish schedule: you play a different team each week; the others pair up.</summary>
        public static SeasonSaveData Schedule(List<string> teams, string you, int weeks, uint seed)
        {
            var season = new SeasonSaveData { seed = seed, teamIds = new List<string>(teams), weeks = weeks, seasonNumber = 1 };
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            var opponents = teams.FindAll(t => t != you);
            for (int i = opponents.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); (opponents[i], opponents[j]) = (opponents[j], opponents[i]); }
            for (int week = 0; week < weeks; week++)
            {
                string opp = opponents[week % opponents.Count];
                bool home = week % 2 == 0;
                season.games.Add(new ScheduledGame { week = week, homeId = home ? you : opp, awayId = home ? opp : you });
                var rest = teams.FindAll(t => t != you && t != opp);
                for (int k = 0; k < week % Math.Max(1, rest.Count); k++) { var first = rest[0]; rest.RemoveAt(0); rest.Add(first); }
                for (int k = 0; k + 1 < rest.Count; k += 2)
                    season.games.Add(new ScheduledGame { week = week, homeId = rest[k], awayId = rest[k + 1] });
            }
            return season;
        }

        public static ScheduledGame NextGame(LegacySaveData s)
        {
            if (s == null || !s.active || s.season == null || s.pendingEvent != null) return null;
            if (s.stage != LegacyStage.HighSchool && s.stage != LegacyStage.College && s.stage != LegacyStage.Pro) return null;
            return SeasonEngine.NextGameFor(s.season, s.teamId);
        }

        public static string StageName(LegacySaveData s)
        {
            switch (s.stage)
            {
                case LegacyStage.HighSchool: return "SENIOR YEAR · " + s.schoolName.ToUpperInvariant();
                case LegacyStage.College: return "COLLEGE · " + s.collegeName.ToUpperInvariant();
                case LegacyStage.Draft: return "DRAFT NIGHT";
                case LegacyStage.Pro: return "PRO SEASON " + s.proSeason;
                default: return "RETIRED";
            }
        }

        /// <summary>Your next game as a Full Court match with you at the top of your team's lineup.</summary>
        public static MatchRequest NextMatch(LegacySaveData s, ContentCatalog c, CareerSaveData career, string difficultyId)
        {
            var g = NextGame(s);
            if (g == null) return null;
            Register(c, s, career);
            string opp = g.homeId == s.teamId ? g.awayId : g.homeId;
            return new MatchRequest
            {
                Mode = GameMode.Legacy,
                HomeTeamId = s.teamId,
                AwayTeamId = opp,
                CourtId = c.Team(g.homeId)?.homeCourtId,
                RulesId = FullCourt.RulesId,
                FullCourt = true,
                DifficultyId = difficultyId,
                Round = g.round,
                ContextId = "legacy:" + s.stage + ":" + s.proSeason + ":w" + g.week + ":r" + g.round,
                HumanPlayer = c.Player(YouId),
            };
        }

        /// <summary>Records the game you played (your score first) and your box-score line.</summary>
        public static LegacyGameLine RecordGame(LegacySaveData s, ContentCatalog c, int us, int them, PlayerStatLine line)
        {
            var g = NextGame(s);
            if (g == null) return null;
            line = line ?? new PlayerStatLine();
            return Finish(s, c, g, us, them, line.points, line.assists, line.rebounds, line.steals, line.blocks, line.turnovers, false);
        }

        /// <summary>SIM: the game is simulated from team strength and your ratings (half the XP of playing).</summary>
        public static LegacyGameLine SimGame(LegacySaveData s, ContentCatalog c, CareerSaveData career)
        {
            var g = NextGame(s);
            if (g == null) return null;
            Register(c, s, career);
            var rng = new SeededRandom(StableHash.Of(s.seed + ":sim:" + s.stage + ":" + s.proSeason + ":" + g.week + ":" + g.round));
            var copy = new ScheduledGame { homeId = g.homeId, awayId = g.awayId };
            SeasonEngine.SimulateGame(copy, c, rng);
            bool home = g.homeId == s.teamId;
            int us = (home ? copy.homeScore : copy.awayScore) + 10, them = (home ? copy.awayScore : copy.homeScore) + 10;
            int ovr = c.Player(YouId)?.attributes.Overall ?? 50;
            float skill = Math.Max(0.2f, (ovr - 35) / 40f);
            int pts = Math.Min(us, 2 * (int)Math.Round(skill * (3 + rng.Range(0, 6))) + 3 * rng.Range(0, 2));
            int ast = (int)Math.Round(skill * rng.Range(0, 5));
            int reb = (int)Math.Round(skill * rng.Range(1, 6));
            return Finish(s, c, g, us, them, pts, ast, reb, rng.Range(0, 2), rng.Range(0, 2), rng.Range(0, 3), true);
        }

        private static LegacyGameLine Finish(LegacySaveData s, ContentCatalog c, ScheduledGame g, int us, int them,
                                             int pts, int ast, int reb, int stl, int blk, int tov, bool simmed)
        {
            if (us == them) us++;
            bool home = g.homeId == s.teamId;
            g.homeScore = home ? us : them;
            g.awayScore = home ? them : us;
            g.played = true;
            bool won = us > them;
            float score = GameScore(pts, ast, reb, stl, blk, tov, won);
            var line = new LegacyGameLine
            {
                week = g.week, round = g.round, opponent = home ? g.awayId : g.homeId, won = won, simmed = simmed,
                us = us, them = them, pts = pts, ast = ast, reb = reb, stl = stl, blk = blk, grade = Grade(score),
            };
            s.games.Add(line);
            int xp = GradeXp(score);
            AddXp(s, simmed ? xp / 2 : xp);
            s.fans += (won ? 8 : 3) + (int)score / 3 + (g.round > 0 ? 10 : 0);
            s.trust = Math.Max(0, Math.Min(100, s.trust + (score >= 17 ? 2 : (score < 7 ? -2 : 0))));

            // The rest of the week (or round) is simulated, then the playoffs and the end of the stage.
            var rng = new SeededRandom(StableHash.Of(s.seed + ":week:" + s.stage + ":" + s.proSeason + ":" + g.week + ":" + g.round));
            foreach (var other in s.season.games)
                if (!other.played && other.week == g.week && other.round == g.round) SeasonEngine.SimulateGame(other, c, rng);
            if (g.round == 0) s.season.currentWeek = Math.Max(s.season.currentWeek, g.week + 1);
            Advance(s, c);
            MaybeEvent(s, rng);
            return line;
        }

        private static void Advance(LegacySaveData s, ContentCatalog c)
        {
            var season = s.season;
            if (!SeasonEngine.RegularSeasonComplete(season)) return;
            if (!SeasonEngine.HasRound(season, 1))
            {
                SeasonEngine.CreatePlayoffs(season);
                if (!SeasonEngine.Qualified(season, s.teamId)) { SimRest(s, c); EndStage(s, c); return; }
            }
            if (SeasonEngine.RoundComplete(season, 1) && !SeasonEngine.HasRound(season, 2))
            {
                SeasonEngine.CreateFinal(season);
                var final = SeasonEngine.FinalGame(season);
                if (final != null && !final.Involves(s.teamId)) { SimRest(s, c); EndStage(s, c); return; }
            }
            var f = SeasonEngine.FinalGame(season);
            if (f != null && f.played) EndStage(s, c);
        }

        private static void SimRest(LegacySaveData s, ContentCatalog c)
        {
            var rng = new SeededRandom(StableHash.Of(s.seed + ":rest:" + s.stage + ":" + s.proSeason));
            for (int guard = 0; guard < 4; guard++)
            {
                foreach (var g in s.season.games) if (!g.played) SeasonEngine.SimulateGame(g, c, rng);
                if (SeasonEngine.RoundComplete(s.season, 1) && !SeasonEngine.HasRound(s.season, 2)) SeasonEngine.CreateFinal(s.season);
                else break;
            }
            foreach (var g in s.season.games) if (!g.played) SeasonEngine.SimulateGame(g, c, rng);
        }

        private static void MaybeEvent(LegacySaveData s, SeededRandom rng)
        {
            if (!s.active || s.pendingEvent != null || rng.NextFloat() > 0.35f) return;
            var options = Array.FindAll(Events, e => Array.IndexOf(e.Stages, s.stage) >= 0 && !s.seenEvents.Contains(e.Id));
            if (options.Length == 0) return;
            s.pendingEvent = options[rng.Range(0, options.Length)].Id;
        }

        public static LegacyEvent PendingEvent(LegacySaveData s) => s?.pendingEvent == null ? null : Array.Find(Events, e => e.Id == s.pendingEvent);

        /// <summary>Answers the pending story moment. Cash is in units of $5,000 (a camp, a donation, a trainer).</summary>
        public static bool Choose(LegacySaveData s, int choice)
        {
            var e = PendingEvent(s);
            if (e == null || choice < 0 || choice >= e.Choices.Length) return false;
            var ch = e.Choices[choice];
            s.fans = Math.Max(0, s.fans + ch.fans);
            s.cash = Math.Max(0, s.cash + ch.cash * 5_000L);
            s.trust = Math.Max(0, Math.Min(100, s.trust + ch.trust));
            AddXp(s, ch.xp);
            s.skillPoints += ch.points;
            s.seenEvents.Add(e.Id);
            s.pendingEvent = null;
            return true;
        }

        public static TeamRecord Record(LegacySaveData s) =>
            s.season == null ? null : SeasonEngine.Standings(s.season).Find(r => r.TeamId == s.teamId);

        private static float AverageGrade(LegacySaveData s)
        {
            if (s.games.Count == 0) return 0f;
            float sum = 0f;
            foreach (var g in s.games) sum += GradeValue(g.grade);
            return sum / s.games.Count;
        }

        /// <summary>End of a stage: history, awards, legacy points, growth, and what comes next.</summary>
        private static void EndStage(LegacySaveData s, ContentCatalog c)
        {
            var f = SeasonEngine.FinalGame(s.season);
            bool champ = f != null && f.played && f.WinnerId == s.teamId;
            bool finals = f != null && f.Involves(s.teamId);
            bool playoffs = s.season.games.Exists(g => g.round == 1 && g.Involves(s.teamId));
            var rec = Record(s);
            float avg = AverageGrade(s);
            var line = new LegacySeason
            {
                label = s.stage == LegacyStage.Pro ? "PRO " + s.proSeason : (s.stage == LegacyStage.College ? "COLLEGE" : "HIGH SCHOOL"),
                team = c.Team(s.teamId)?.FullName ?? s.teamId, age = s.age, games = s.games.Count,
                wins = s.games.FindAll(g => g.won).Count,
                result = champ ? "CHAMPIONS" : finals ? "LOST FINAL" : playoffs ? "LOST SEMIFINAL" : "MISSED PLAYOFFS",
            };
            foreach (var g in s.games) { line.pts += g.pts; line.ast += g.ast; line.reb += g.reb; }

            switch (s.stage)
            {
                case LegacyStage.HighSchool:
                    if (champ) line.awards.Add("STATE CHAMPION");
                    if (avg >= 19) line.awards.Add("ALL-STATE");
                    if (avg >= 24) line.awards.Add("STATE PLAYER OF THE YEAR");
                    s.stars = Math.Max(1, Math.Min(5, (int)Math.Round(avg / 6f) + (champ ? 1 : 0)));
                    s.legacyPoints += line.awards.Count * 10;
                    s.stage = LegacyStage.College;
                    CollegeOffers(s);
                    s.season = null;
                    break;
                case LegacyStage.College:
                    if (champ) line.awards.Add("CONFERENCE CHAMPION");
                    if (avg >= 21) line.awards.Add("ALL-AMERICAN");
                    s.stock = Math.Max(0, Math.Min(100, (int)Math.Round(avg * 3f) + s.stars * 4 + (champ ? 10 : 0) + s.collegeTier * 4));
                    s.legacyPoints += line.awards.Count * 15;
                    s.stage = LegacyStage.Draft;
                    s.season = null;
                    break;
                case LegacyStage.Pro:
                    var table = SeasonEngine.Standings(s.season);
                    int place = table.FindIndex(r => r.TeamId == s.teamId);
                    if (champ) line.awards.Add("CHAMPION");
                    if (avg >= 24 && place <= 1) line.awards.Add("MVP");
                    else if (avg >= 18) line.awards.Add("ALL-LEAGUE");
                    if (s.proSeason == 1 && avg >= 13) line.awards.Add("ROOKIE OF THE YEAR");
                    s.legacyPoints += 10 + (champ ? 100 : 0) + (line.awards.Contains("MVP") ? 80 : 0) + (line.awards.Contains("ALL-LEAGUE") ? 40 : 0)
                                      + (line.awards.Contains("ROOKIE OF THE YEAR") ? 20 : 0) + (int)line.Ppg;
                    long pay = s.salary * 1000L;
                    foreach (var id in s.endorsements) pay += (Brand(id)?.Pay ?? 0) * 1000L;
                    s.cash += pay;
                    s.contractYears = Math.Max(0, s.contractYears - 1);
                    if (s.contractYears == 0) AgentOffers(s, c);
                    s.season = null;
                    break;
            }
            s.history.Add(line);
            s.age++;
            s.growth += Growth(s.age);
            s.games.Clear();
            if (s.stage == LegacyStage.Pro && s.age >= RetireAge) Retire(s);
        }

        /// <summary>Aging: young players grow every year, veterans slow down.</summary>
        public static int Growth(int age)
        {
            if (age <= 21) return 3;
            if (age <= 24) return 2;
            if (age <= 27) return 1;
            if (age <= 30) return 0;
            return -(1 + (age - 31) / 2);
        }

        // ------------------------------------------------------------------ college, draft, contracts

        private static void CollegeOffers(LegacySaveData s)
        {
            s.offers.Clear();
            var rng = new SeededRandom(StableHash.Of("lg:offers:" + s.seed));
            // More stars, better schools: tier 2 needs 4 stars, tier 1 needs 2.
            int best = s.stars >= 4 ? 2 : (s.stars >= 2 ? 1 : 0);
            var used = new HashSet<int>();
            for (int t = best; t >= 0 && s.offers.Count < 3; t--)
            {
                int i;
                do i = 1 + rng.Range(0, CollegeNames.Length - 1); while (!used.Add(i));
                var parts = CollegeNames[i].Split('|');
                s.offers.Add(new LegacyOffer
                {
                    name = parts[0], value = t,
                    pitch = t == 2 ? "A powerhouse: tough minutes, lots of scouts." : t == 1 ? "A solid program with a starting spot for you." : "A small school: the ball is yours from day one.",
                });
            }
            s.offers.Add(new LegacyOffer { name = "", value = -1, pitch = "Skip college and declare for the draft now (scouts only saw high school)." });
        }

        public static bool ChooseCollege(LegacySaveData s, ContentCatalog c, int offer)
        {
            if (s.stage != LegacyStage.College || s.season != null || offer < 0 || offer >= s.offers.Count) return false;
            var o = s.offers[offer];
            s.offers.Clear();
            if (o.value < 0)
            {
                s.stock = Math.Max(0, Math.Min(100, s.stars * 12 + 10));
                s.stage = LegacyStage.Draft;
                return true;
            }
            s.collegeName = o.name;
            s.collegeTier = o.value;
            s.teamId = SchoolId;
            NewSchoolSeason(c, s, CollegeWeeks, true);
            return true;
        }

        /// <summary>Draft order: the weakest league team picks first. Your stock decides where you go (low stock: undrafted).</summary>
        public static int ProjectedPick(LegacySaveData s) => s.stock >= 85 ? 1 : s.stock >= 75 ? 2 : s.stock >= 65 ? 3 : s.stock >= 55 ? 4
                                                            : s.stock >= 45 ? 5 : s.stock >= 35 ? 6 : s.stock >= 25 ? 7 : s.stock >= 15 ? 8 : 0;

        public static List<TeamDef> DraftOrder(ContentCatalog c)
        {
            var league = c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => SeasonEngine.TeamStrength(a, c).CompareTo(SeasonEngine.TeamStrength(b, c)) != 0
                ? SeasonEngine.TeamStrength(a, c).CompareTo(SeasonEngine.TeamStrength(b, c)) : string.CompareOrdinal(a.id, b.id));
            return league;
        }

        /// <summary>Draft night: you go to the team at your pick (undrafted players sign with the weakest team for the minimum).</summary>
        public static TeamDef Draft(LegacySaveData s, ContentCatalog c)
        {
            if (s.stage != LegacyStage.Draft) return null;
            var order = DraftOrder(c);
            if (order.Count == 0) return null;
            int pick = ProjectedPick(s);
            var team = pick == 0 ? order[0] : order[Math.Min(pick, order.Count) - 1];
            s.draftPick = pick;
            s.teamId = team.id;
            s.salary = pick == 0 ? 900 : 6000 - (pick - 1) * 600;
            s.contractYears = 3;
            s.stage = LegacyStage.Pro;
            NewProSeason(s, c);
            return team;
        }

        private static void NewProSeason(LegacySaveData s, ContentCatalog c)
        {
            s.proSeason++;
            var ids = c.TeamsInTier(TeamTier.League).ConvertAll(t => t.id);
            ids.Sort(string.CompareOrdinal);
            s.season = Schedule(ids, s.teamId, ProWeeks, StableHash.Of("lg:pro:" + s.seed + ":" + s.proSeason));
            s.season.seasonNumber = s.proSeason;
            s.games.Clear();
        }

        /// <summary>Your role on a pro team: by your rating among the five best, and the coach's trust.</summary>
        public static string Role(LegacySaveData s, ContentCatalog c)
        {
            if (s.stage != LegacyStage.Pro) return s.stage == LegacyStage.Retired ? "RETIRED" : "STARTER";
            var team = c.Team(s.teamId);
            int you = c.Player(YouId)?.attributes.Overall ?? 50;
            int better = 0;
            if (team != null) foreach (var id in team.rosterPlayerIds) if ((c.Player(id)?.attributes.Overall ?? 0) > you) better++;
            if (better == 0 && s.trust >= 40) return "FRANCHISE STAR";
            if (better <= 3 || s.trust >= 75) return "STARTER";
            return "SIXTH MAN";
        }

        /// <summary>Contract up: your team and two others make offers sized to your rating and legacy.</summary>
        private static void AgentOffers(LegacySaveData s, ContentCatalog c)
        {
            s.offers.Clear();
            int ovr = c.Player(YouId)?.attributes.Overall ?? 60;
            int baseSalary = Math.Max(1200, (ovr - 45) * (ovr - 45) * 14 + s.legacyPoints * 8);
            var rng = new SeededRandom(StableHash.Of("lg:agent:" + s.seed + ":" + s.proSeason));
            var league = c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var others = league.FindAll(t => t.id != s.teamId);
            for (int i = others.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); (others[i], others[j]) = (others[j], others[i]); }
            var mine = c.Team(s.teamId);
            if (mine != null) s.offers.Add(new LegacyOffer { teamId = mine.id, name = mine.FullName, value = baseSalary, years = s.age >= 32 ? 1 : 3, pitch = "Stay home. The fans know your name." });
            for (int k = 0; k < 2 && k < others.Count; k++)
                s.offers.Add(new LegacyOffer
                {
                    teamId = others[k].id, name = others[k].FullName, value = baseSalary + 300 + rng.Range(0, 900), years = s.age >= 32 ? 1 : 2 + rng.Range(0, 3),
                    pitch = k == 0 ? "A bigger role and a bigger cheque." : "They think you're the missing piece.",
                });
        }

        /// <summary>Starts the next pro season (signing the chosen offer first if your contract was up).</summary>
        public static bool NextSeason(LegacySaveData s, ContentCatalog c, int offer = -1)
        {
            if (s.stage != LegacyStage.Pro || s.season != null) return false;
            if (s.offers.Count > 0)
            {
                if (offer < 0 || offer >= s.offers.Count) return false;
                var o = s.offers[offer];
                s.teamId = o.teamId;
                s.salary = o.value;
                s.contractYears = o.years;
                s.offers.Clear();
            }
            NewProSeason(s, c);
            return true;
        }

        public static LegacyBrand Brand(string id) => Array.Find(Brands, b => b.Id == id);

        /// <summary>Sponsors that would sign you now (pro only, enough fans, a free slot).</summary>
        public static List<LegacyBrand> BrandOffers(LegacySaveData s)
        {
            var list = new List<LegacyBrand>();
            if (s.stage != LegacyStage.Pro || s.endorsements.Count >= MaxEndorsements) return list;
            foreach (var b in Brands) if (s.fans >= b.MinFans && !s.endorsements.Contains(b.Id)) list.Add(b);
            return list;
        }

        public static bool SignBrand(LegacySaveData s, string id)
        {
            var b = Brand(id);
            if (b == null || !BrandOffers(s).Contains(b)) return false;
            s.endorsements.Add(id);
            s.fans += 25;
            return true;
        }

        public static bool DropBrand(LegacySaveData s, string id) => s.endorsements.Remove(id);

        // ------------------------------------------------------------------ the end

        public static bool CanRetire(LegacySaveData s) => s.stage == LegacyStage.Pro && s.season == null && s.age >= CanRetireAge;

        public static void Retire(LegacySaveData s)
        {
            s.stage = LegacyStage.Retired;
            s.season = null;
            s.offers.Clear();
            s.hallOfFame = s.legacyPoints >= HallOfFameLegacy;
        }

        public static (int seasons, int games, int pts, int titles, int mvps) Totals(LegacySaveData s)
        {
            int seasons = 0, games = 0, pts = 0, titles = 0, mvps = 0;
            foreach (var h in s.history)
            {
                if (!h.label.StartsWith("PRO", StringComparison.Ordinal)) continue;
                seasons++; games += h.games; pts += h.pts;
                if (h.awards.Contains("CHAMPION")) titles++;
                if (h.awards.Contains("MVP")) mvps++;
            }
            return (seasons, games, pts, titles, mvps);
        }

        public static string Money(long dollars) =>
            dollars >= 1_000_000 ? "$" + (dollars / 1_000_000) + "." + (dollars % 1_000_000 / 100_000) + "M" : "$" + (dollars / 1000) + "K";
    }
}
