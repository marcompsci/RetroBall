using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A character in Summer Story (all original): name, colours, look, and which side of the scene they stand on.</summary>
    public sealed class StoryCharacter
    {
        public StorySpeaker Speaker;
        public string Name, Role, Color;
        public AppearanceDef Look;
        public string Jersey, Trim, Accent;
        /// <summary>Stands on the right (rivals) or the left (friends).</summary>
        public bool Right;
    }

    public enum StoryGoalKind { Win = 0, WinBy = 1, Assists = 2, Steals = 3, HoldUnder = 4, Points = 5, AnkleBreakers = 6, Blocks = 7 }

    /// <summary>One chapter: the scene before, the game (format, court, opponent), the goal, and the scene after.</summary>
    public sealed class StoryChapter
    {
        public int Number;
        public string Id, Title, Blurb, CourtId, CrewName, CrewAbbr, LeaderFirst, LeaderLast;
        /// <summary>Players a side: 1 (1-on-1) to 4; 5 = Full Court.</summary>
        public int Size;
        public Archetype LeaderStyle;
        public string Primary, Secondary;
        /// <summary>Overall rating of their crew.</summary>
        public int Level;
        public StoryGoalKind Goal;
        public int GoalValue;
        public bool FullCourt => Size == 5;
        public string TeamId => "team.story." + Number;
        public string Format => Size == 5 ? "FULL COURT 5 ON 5" : Size == 1 ? "1 ON 1" : Size + " ON " + Size;
    }

    [Serializable]
    public class StorySaveData
    {
        /// <summary>Chapters cleared (0..Chapters). The next chapter to play is this one (or a replay).</summary>
        public int cleared;
        /// <summary>Best result per chapter: 0 = not cleared, 1 = cleared.</summary>
        public List<int> best = new List<int>();
        public int attempts;
        public bool finished;
    }

    public enum StoryOutcome { Cleared = 0, GoalMissed = 1, Lost = 2 }

    /// <summary>
    /// SUMMER STORY: eight chapters of street ball, from a 1-on-1 at the overpass to the Sunburst League
    /// final, told in short scenes with original characters: Nova Quinn (your new running mate), Big Sal
    /// (who runs the Sunburst League), Mic Tally (the voice of the blacktop) and Kojo Stride, captain of
    /// the Velvet Hour. Each chapter has a goal; clear it to unlock the next.
    /// </summary>
    public static class StoryMode
    {
        public const int Chapters = 8;
        public const int ClearReward = 150;
        public const int FinaleReward = 600;
        public const string ContextPrefix = "story:";

        public static readonly StoryCharacter[] Cast =
        {
            new StoryCharacter { Speaker = StorySpeaker.Nova, Name = "NOVA", Role = "Your running mate", Color = "#F72585",
                                 Look = new AppearanceDef(4, 5, 1, BodyType.Slim, 1), Jersey = "#F72585", Trim = "#1A1A2E", Accent = "#FFD166" },
            new StoryCharacter { Speaker = StorySpeaker.Sal, Name = "BIG SAL", Role = "Runs the Sunburst League", Color = "#FFB703",
                                 Look = new AppearanceDef(2, 0, 3, BodyType.Broad, 2), Jersey = "#FFB703", Trim = "#3D348B", Accent = "#FFFFFF" },
            new StoryCharacter { Speaker = StorySpeaker.Mic, Name = "MIC TALLY", Role = "The voice of the blacktop", Color = "#4CC9F0",
                                 Look = new AppearanceDef(1, 2, 2, BodyType.Standard, 1), Jersey = "#4CC9F0", Trim = "#14213D", Accent = "#FB8500" },
            new StoryCharacter { Speaker = StorySpeaker.Kojo, Name = "KOJO", Role = "Captain of the Velvet Hour", Color = "#9D4EDD", Right = true,
                                 Look = new AppearanceDef(5, 3, 0, BodyType.Standard, 2), Jersey = "#5A189A", Trim = "#E0AAFF", Accent = "#10002B" },
        };

        public static StoryCharacter Character(StorySpeaker s) => Array.Find(Cast, c => c.Speaker == s);

        public static readonly StoryChapter[] All =
        {
            Ch(1, "FIRST LIGHT", "A 1-on-1 at the overpass with a stranger who won't stop talking.", "court.overpass_park", 1,
               "OVERPASS", "OVP", "Nova", "Quinn", Archetype.QuickCutter, "#F72585", "#1A1A2E", 58, StoryGoalKind.Win, 0),
            Ch(2, "TWO CAN PLAY", "You and Nova against the Pier Brothers. They pass like twins because they are.", "court.pier_nine", 2,
               "PIER BROTHERS", "PBR", "Hollis", "Pier", Archetype.RimRunner, "#2A9D8F", "#E9C46A", 60, StoryGoalKind.Win, 0),
            Ch(3, "BIG SAL'S TRYOUT", "Win by four in front of Big Sal and you're in the Sunburst League.", "court.sunset_cage", 3,
               "SAL'S ALL-COMERS", "SAL", "Marlo", "Finch", Archetype.PostAnchor, "#FFB703", "#3D348B", 62, StoryGoalKind.WinBy, 4),
            Ch(4, "NIGHT MARKET", "The Lantern Kids switch everything. Move the ball: three assists and a win.", "court.lantern_market", 3,
               "LANTERN KIDS", "LTN", "Pip", "Okoro", Archetype.Playmaker, "#E76F51", "#264653", 64, StoryGoalKind.Assists, 3),
            Ch(5, "RAIN CHECK", "The Velvet Hour's second unit, in the rain. Lock them down: three steals and a win.", "court.rain_alley", 3,
               "VELVET SECOND", "VH2", "Rue", "Calder", Archetype.LockdownWing, "#5A189A", "#E0AAFF", 66, StoryGoalKind.Steals, 3),
            Ch(6, "ROOFTOP RUMOR", "Kojo wants to see you himself. Two on two on the roof. Win.", "court.rooftop_ring", 2,
               "KOJO & SAINT", "KJO", "Kojo", "Stride", Archetype.TwoWaySpark, "#9D4EDD", "#10002B", 70, StoryGoalKind.Win, 0),
            Ch(7, "BREAK THEIR ANKLES", "Street rules at the sundown yard. Shake somebody so hard Mic has to call it.", "court.sundown_yard", 3,
               "YARD DOGS", "YRD", "Bishop", "Lake", Archetype.ShotCreator, "#BC6C25", "#283618", 70, StoryGoalKind.AnkleBreakers, 1),
            Ch(8, "SUNBURST FINAL", "Full court, both baskets, the whole city watching. The Velvet Hour. Win it.", "court.canyon_rim", 5,
               "VELVET HOUR", "VHR", "Kojo", "Stride", Archetype.TwoWaySpark, "#5A189A", "#E0AAFF", 74, StoryGoalKind.Win, 0),
        };

        private static StoryChapter Ch(int n, string title, string blurb, string court, int size, string crew, string abbr, string first, string last,
                                       Archetype style, string primary, string secondary, int level, StoryGoalKind goal, int value) =>
            new StoryChapter
            {
                Number = n, Id = "summer.ch" + n, Title = title, Blurb = blurb, CourtId = court, Size = size, CrewName = crew, CrewAbbr = abbr,
                LeaderFirst = first, LeaderLast = last, LeaderStyle = style, Primary = primary, Secondary = secondary, Level = level, Goal = goal, GoalValue = value,
            };

        public static StoryChapter Chapter(int number) => number >= 1 && number <= Chapters ? All[number - 1] : null;

        public static bool Unlocked(StorySaveData s, int number) => number >= 1 && number <= Math.Min(Chapters, (s?.cleared ?? 0) + 1);

        public static string GoalText(StoryChapter ch)
        {
            switch (ch.Goal)
            {
                case StoryGoalKind.WinBy: return "Win by " + ch.GoalValue + " or more";
                case StoryGoalKind.Assists: return "Win with " + ch.GoalValue + "+ assists";
                case StoryGoalKind.Steals: return "Win with " + ch.GoalValue + "+ steals";
                case StoryGoalKind.HoldUnder: return "Win and hold them under " + ch.GoalValue;
                case StoryGoalKind.Points: return "Win with " + ch.GoalValue + "+ points";
                case StoryGoalKind.AnkleBreakers: return "Win and break " + ch.GoalValue + " defender" + (ch.GoalValue == 1 ? "'s ankles" : "s' ankles");
                case StoryGoalKind.Blocks: return "Win with " + ch.GoalValue + "+ blocks";
                default: return "Win the game";
            }
        }

        /// <summary>Did this game clear the chapter? (Every goal includes winning.)</summary>
        public static bool GoalMet(StoryChapter ch, MatchSummary s)
        {
            if (ch == null || s == null || !s.HumanWon) return false;
            var me = s.HumanLine?.stats;
            int team(Func<PlayerStatLine, int> f)
            {
                int total = 0;
                foreach (var l in s.lines) if (l.team == s.humanTeam && l.stats != null) total += f(l.stats);
                return total;
            }
            switch (ch.Goal)
            {
                case StoryGoalKind.WinBy: return s.Margin >= ch.GoalValue;
                case StoryGoalKind.Assists: return team(x => x.assists) >= ch.GoalValue;
                case StoryGoalKind.Steals: return team(x => x.steals) >= ch.GoalValue;
                case StoryGoalKind.HoldUnder: return s.OpponentScore < ch.GoalValue;
                case StoryGoalKind.Points: return (me?.points ?? 0) >= ch.GoalValue;
                case StoryGoalKind.AnkleBreakers: return (me?.ankleBreakers ?? 0) >= ch.GoalValue;
                case StoryGoalKind.Blocks: return team(x => x.blocks) >= ch.GoalValue;
                default: return true;
            }
        }

        /// <summary>Registers the chapter's opponents (their leader is the named character; the rest are around their level).</summary>
        public static TeamDef Register(ContentCatalog c, StoryChapter ch)
        {
            var team = c.Team(ch.TeamId);
            if (team == null) c.Teams.Add(team = new TeamDef { id = ch.TeamId, tier = TeamTier.Franchise });
            uint h = StableHash.Of(ch.Id);
            team.city = "";
            team.nickname = ch.CrewName;
            team.abbreviation = ch.CrewAbbr;
            team.primary = RgbColor.FromHex(ch.Primary);
            team.secondary = RgbColor.FromHex(ch.Secondary);
            team.accent = RgbColor.FromHex("#1A1A1F");
            team.logoShape = (LogoShape)(h % 5);
            team.logoMotif = (LogoMotif)((h / 5) % 10);
            team.pattern = (TeamPattern)(1 + (h / 50) % 7);
            team.homeCourtId = c.Court(ch.CourtId) != null ? ch.CourtId : "court.overpass_park";
            team.motto = ch.Blurb;
            team.rosterPlayerIds.Clear();
            int count = ch.FullCourt ? 8 : 4;
            for (int k = 0; k < count; k++)
            {
                string pid = "player.story." + ch.Number + "." + k;
                var p = c.Player(pid);
                if (p == null)
                {
                    var arch = c.Archetypes.Find(a => a.archetype == (k == 0 ? ch.LeaderStyle : (Archetype)((int)(h + (uint)k * 5) % 12))) ?? c.Archetypes[0];
                    var attrs = DefaultContent.Personalize(arch.baseline, pid);
                    int target = ch.Level + (k == 0 ? 5 : -2);
                    var kojo = Character(StorySpeaker.Kojo);
                    bool isKojo = k == 0 && ch.LeaderFirst == "Kojo";
                    p = new PlayerDef
                    {
                        id = pid,
                        firstName = k == 0 ? ch.LeaderFirst : Bench[(h + (uint)k) % (uint)Bench.Length],
                        lastName = k == 0 ? ch.LeaderLast : BenchLast[(h / 3 + (uint)k) % (uint)BenchLast.Length],
                        jerseyNumber = isKojo ? 1 : (int)((h >> (k + 2)) % 60),
                        archetypeId = arch.id,
                        attributes = attrs.Offset(target - attrs.Overall),
                        appearance = isKojo ? kojo.Look : DefaultContent.AppearanceFromSeed(pid, arch.archetype),
                    };
                    c.Players.Add(p);
                }
                team.rosterPlayerIds.Add(pid);
            }
            return team;
        }

        private static readonly string[] Bench = { "Ade", "Benny", "Cass", "Dot", "Eko", "Fitz", "Gigi", "Hal", "Ines", "Jules" };
        private static readonly string[] BenchLast = { "Moss", "Reed", "Vale", "Cross", "Hart", "Lowe", "Penn", "Sato", "Ward", "York" };

        /// <summary>The chapter as a match: your crew (you lead it) against theirs, on their court.</summary>
        public static MatchRequest Request(ContentCatalog c, CareerSaveData career, StoryChapter ch)
        {
            Register(c, ch);
            string mine = CustomTeams.IsYours(CustomTeams.TeamId) && c.Team(CustomTeams.TeamId) != null ? CustomTeams.TeamId : DefaultContent.PlayerCrewId;
            bool one = ch.Size == 1;
            return new MatchRequest
            {
                Mode = ch.FullCourt ? GameMode.FullCourt : one ? GameMode.OneOnOne : GameMode.Street,
                HomeTeamId = mine,
                AwayTeamId = ch.TeamId,
                CourtId = c.Team(ch.TeamId).homeCourtId,
                RulesId = ch.FullCourt ? DefaultContent.DefaultRulesId : one ? "rules.oneonone" : Street.RulesId,
                TeamSize = ch.FullCourt || one ? 3 : ch.Size,
                FullCourt = ch.FullCourt,
                StreetRules = !ch.FullCourt,
                DifficultyId = career.settings.difficultyId,
                ContextId = ContextPrefix + ch.Number,
            };
        }

        public static StoryChapter FromContext(string contextId)
        {
            if (contextId == null || !contextId.StartsWith(ContextPrefix, StringComparison.Ordinal)) return null;
            return int.TryParse(contextId.Substring(ContextPrefix.Length), out int n) ? Chapter(n) : null;
        }

        /// <summary>Records a chapter game. Returns the outcome and the SP earned (first clears only).</summary>
        public static StoryOutcome ApplyResult(StorySaveData s, StoryChapter ch, MatchSummary summary, out int reward)
        {
            reward = 0;
            s.attempts++;
            while (s.best.Count < Chapters) s.best.Add(0);
            if (!summary.HumanWon) return StoryOutcome.Lost;
            if (!GoalMet(ch, summary)) return StoryOutcome.GoalMissed;
            if (s.best[ch.Number - 1] == 0)
            {
                s.best[ch.Number - 1] = 1;
                reward = ch.Number == Chapters ? FinaleReward : ClearReward;
            }
            if (ch.Number > s.cleared) s.cleared = Math.Min(Chapters, ch.Number);
            if (ch.Number == Chapters) s.finished = true;
            return StoryOutcome.Cleared;
        }

        public static string IntroId(int n) => "summer.ch" + n + ".intro";
        public static string OutroId(int n) => "summer.ch" + n + ".outro";

        /// <summary>The scene before (or after) chapter <paramref name="n"/>. All dialogue is original.</summary>
        public static StoryBeat Scene(int n, bool after, string nickname)
        {
            string me = string.IsNullOrEmpty(nickname) ? "Rook" : nickname;
            var b = new StoryBeat { Id = after ? OutroId(n) : IntroId(n) };
            void N(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Nova, t));
            void S(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Sal, t));
            void M(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Mic, t));
            void K(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Kojo, t));
            void C(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Coach, t));
            void Y(string t) => b.Lines.Add(new StoryLine(StorySpeaker.You, t));
            switch (n * 2 + (after ? 1 : 0))
            {
                case 2:
                    M("Good morning, overpass! It's Mic Tally, the voice of the blacktop, and it is HOT out here.");
                    N("You're on my court. I'm Nova. First to eleven, loser buys the freeze pops.");
                    Y("I don't even know you.");
                    N("That's fine. You'll know me in about four minutes.");
                    break;
                case 3:
                    N("Okay. Okay! You can actually play. Where have you been all summer?");
                    Y("Right here. Nobody asked.");
                    N("Then I'm asking. The Sunburst League starts in two weeks. We need a crew.");
                    M("Write it down, people: a new duo at the overpass.");
                    break;
                case 4:
                    N("The Pier Brothers. They never miss a pass and they never stop talking about it.");
                    M("Down at Pier Nine the tide is in and the Brothers are warming up. Somebody brought a speaker.");
                    Y("Two on two. Stay close and keep the ball moving.");
                    break;
                case 5:
                    N("Did you see their faces? I'm going to remember that face forever.");
                    C("Not bad, " + me + ". I heard you two were looking for a league. Talk to Big Sal at the cage.");
                    break;
                case 6:
                    S("So you're the overpass kids. Everybody's a star in their own neighbourhood.");
                    S("The Sunburst League's got one spot left. Win by four against my All-Comers and it's yours.");
                    N("Four? Easy.");
                    S("Everybody says easy.");
                    break;
                case 7:
                    S("Huh. Okay. Okay! You're in. First league game's at the night market.");
                    M("Big Sal said okay twice. In thirty years I've heard him say it once.");
                    break;
                case 8:
                    M("Lanterns up, the market's open, and the Lantern Kids are switching on every screen.");
                    N("They want you to go one on one. Don't. Make them chase the ball.");
                    C("Three assists, " + me + ". Find the open player and they'll fold.");
                    break;
                case 9:
                    N("That was beautiful. That was like music.");
                    K("It was okay.");
                    N("...Who was that?");
                    M("That, friends, was Kojo Stride. Captain of the Velvet Hour. Three Sunburst titles in a row.");
                    break;
                case 10:
                    M("Rain on the alley and the Velvet Hour sent their second unit. Kojo's watching from under the awning.");
                    N("They're testing us. Hands up, " + me + ". Steal the ball and the rain is ours.");
                    break;
                case 11:
                    K("You defend. Good. Most people in this league just wait for their turn to shoot.");
                    Y("We're coming for the title.");
                    K("Everybody is. Come up to the roof on Saturday. Just you and Nova. Just me and Saint.");
                    break;
                case 12:
                    M("Saturday on the roof. No crowd, no lights, just the skyline and four people who don't like losing.");
                    K("Show me you're real, " + me + ".");
                    N("We're real. We're very real.");
                    break;
                case 13:
                    K("...Fine. You're real.");
                    K("See you in the final. Bring everything.");
                    N("He smiled. Did you see? Kojo Stride smiled at us.");
                    break;
                case 14:
                    S("Semifinal at the sundown yard. Street rules, so the Yard Dogs will play rough.");
                    M("The yard's packed and the Dogs are barking. Somebody's ankles are getting left on this court tonight.");
                    N("Make it theirs.");
                    break;
                case 15:
                    M("OH! He's DOWN! He's sitting on the court like he's waiting for a bus!");
                    S("We're going to the final. The canyon court. Full court, five on five, both baskets.");
                    C("Rest up. The Velvet Hour has been waiting for this all summer.");
                    break;
                case 16:
                    M("This is it. The Sunburst final at the canyon court. The whole city's on the hill.");
                    K("Three titles. I want the fourth, " + me + ". Nothing personal.");
                    Y("It's a little personal.");
                    N("Let's go. Let's go! LET'S GO!");
                    break;
                case 17:
                    M("IT'S OVER! The overpass kids win the Sunburst League! Write it down! Write it down twice!");
                    K("...Good game. Next summer, I'm coming for you.");
                    S("Okay. Okay. OKAY.");
                    N("I told you. Four minutes. You'd know me in four minutes.");
                    Y("Same time next summer?");
                    N("Same time tomorrow.");
                    break;
            }
            return b;
        }
    }
}
