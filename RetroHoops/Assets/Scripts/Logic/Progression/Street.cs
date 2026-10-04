using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A park regular you can challenge.</summary>
    public sealed class StreetChallenger
    {
        public string Id, Name, Nickname, Trash, CourtId;
        /// <summary>Rep tier needed to call them out (0..4) and how good their crew is.</summary>
        public int Tier;
        /// <summary>Players a side: 1 (1-on-1) to 4.</summary>
        public int Size;
        public Archetype Style;
        public string TeamId => "team.st." + Id;
        public string Format => Size == 1 ? "1 ON 1" : Size + " ON " + Size;
    }

    [Serializable]
    public class StreetSaveData
    {
        public int rep;
        public List<string> beaten = new List<string>();
        public int wins, losses, ankleBreakers, bestStreak, streak;
    }

    /// <summary>
    /// THE PARK: twelve original street legends, each with a crew and a format (1-on-1 up to 4-on-4).
    /// Street rules: first to 15 by 1s and 2s, win by two, make it take it, and sharp crossovers can
    /// break a defender's ankles. Wins build your rep (Rookie to Legend), which opens tougher callers.
    /// </summary>
    public static class Street
    {
        public const string RulesId = "rules.street";
        public static readonly string[] RepNames = { "ROOKIE", "HOOPER", "PLAYMAKER", "STAR", "LEGEND" };
        public static readonly int[] RepNeeded = { 0, 100, 250, 500, 900 };

        public static readonly StreetChallenger[] Challengers =
        {
            C("slim", "Dez Harlan", "SLIM", 0, 1, Archetype.QuickCutter, "court.overpass_park", "You can't guard what you can't catch."),
            C("pockets", "Marty Quill", "POCKETS", 0, 2, Archetype.HustleGuard, "court.static_lot", "I'll have your lunch money by halftime."),
            C("tuna", "Big Rollo", "BIG TUNA", 0, 3, Archetype.PostAnchor, "court.boardwalk_slab", "Nothing comes in my paint. Nothing."),
            C("lefty", "Nia Okafor", "LEFTY", 1, 1, Archetype.ShotCreator, "court.rooftop_ring", "Everybody knows I go left. Nobody stops it."),
            C("glide", "Teo Marsh", "GLIDE", 1, 2, Archetype.RimRunner, "court.pier_nine", "Watch the sky, rookie."),
            C("professor", "Ike Bramwell", "THE PROFESSOR", 1, 4, Archetype.Playmaker, "court.sunset_cage", "Class is in session. Four on four."),
            C("handles", "Rico Vance", "HANDLES", 2, 1, Archetype.FloorGeneral, "court.rain_alley", "Bring your ankles. Leave without them."),
            C("tower", "Gus Ambrose", "THE TOWER", 2, 3, Archetype.GlassCleaner, "court.snowline_park", "Every rebound is mine."),
            C("splash", "Kiki Laurent", "SPLASH", 3, 2, Archetype.DeepShooter, "court.ferry_deck", "From the logo, every time."),
            C("ghost", "Sol Varga", "GHOST", 3, 4, Archetype.LockdownWing, "court.lantern_market", "You won't see me till the steal."),
            C("mayor", "Old Man Reyes", "THE MAYOR", 4, 1, Archetype.TwoWaySpark, "court.sundown_yard", "I've run this park for thirty years."),
            C("crown", "Jax Monroe", "PARK KING", 4, 3, Archetype.StretchForward, "court.canyon_rim", "Beat me and the park is yours."),
        };

        private static StreetChallenger C(string id, string name, string nick, int tier, int size, Archetype style, string court, string trash) =>
            new StreetChallenger { Id = id, Name = name, Nickname = nick, Tier = tier, Size = size, Style = style, CourtId = court, Trash = trash };

        public static StreetChallenger Find(string id) => Array.Find(Challengers, c => c.Id == id);

        public static int RepLevel(int rep)
        {
            int level = 0;
            for (int i = 0; i < RepNeeded.Length; i++) if (rep >= RepNeeded[i]) level = i;
            return level;
        }

        public static bool Unlocked(StreetSaveData s, StreetChallenger c) => RepLevel(s.rep) >= c.Tier;

        /// <summary>Registers a challenger's crew (they lead it; the crew is a bit below them).</summary>
        public static TeamDef Register(ContentCatalog c, StreetChallenger ch)
        {
            var team = c.Team(ch.TeamId);
            if (team == null) c.Teams.Add(team = new TeamDef { id = ch.TeamId, tier = TeamTier.Franchise });
            uint h = StableHash.Of(ch.Id);
            team.city = "";
            team.nickname = ch.Nickname + "'S CREW";
            team.abbreviation = ch.Nickname.Replace("THE ", "").Replace(" ", "").Substring(0, Math.Min(3, ch.Nickname.Replace("THE ", "").Replace(" ", "").Length));
            team.primary = CustomTeams.Palette[h % 16];
            team.secondary = CustomTeams.Palette[(h / 16 + 7) % 16];
            team.accent = RgbColor.FromHex("#1A1A1F");
            team.logoShape = (LogoShape)(h % 5);
            team.logoMotif = (LogoMotif)((h / 5) % 10);
            team.pattern = (TeamPattern)(1 + (h / 50) % 7);
            team.homeCourtId = c.Court(ch.CourtId) != null ? ch.CourtId : "court.overpass_park";
            team.motto = ch.Trash;
            team.rosterPlayerIds.Clear();
            int level = 58 + ch.Tier * 4;
            for (int k = 0; k < 4; k++)
            {
                string pid = "player.st." + ch.Id + "." + k;
                if (c.Player(pid) == null)
                {
                    var arch = c.Archetypes.Find(a => a.archetype == (k == 0 ? ch.Style : (Archetype)((int)(h + (uint)k * 5) % 12))) ?? c.Archetypes[0];
                    var attrs = DefaultContent.Personalize(arch.baseline, pid);
                    int target = level + (k == 0 ? 4 : -2);
                    var p = new PlayerDef
                    {
                        id = pid, firstName = k == 0 ? ch.Nickname : StreetFirst[(h + (uint)k) % (uint)StreetFirst.Length],
                        lastName = k == 0 ? "" : StreetLast[(h / 3 + (uint)k) % (uint)StreetLast.Length],
                        jerseyNumber = (int)((h >> (k + 2)) % 60), archetypeId = arch.id,
                        attributes = attrs.Offset(target - attrs.Overall),
                        appearance = DefaultContent.AppearanceFromSeed(pid, arch.archetype),
                    };
                    c.Players.Add(p);
                }
                team.rosterPlayerIds.Add(pid);
            }
            return team;
        }

        private static readonly string[] StreetFirst = { "Dre", "Mookie", "Tink", "Bones", "Pepper", "Juice", "Cheddar", "Scoot", "Moose", "Pip" };
        private static readonly string[] StreetLast = { "Crane", "Lark", "Stone", "Wells", "Banks", "Dorsey", "Fitch", "Gray", "Hale", "Pike" };

        /// <summary>The challenge as a match: your crew (you lead it) vs theirs on their court, street rules.</summary>
        public static MatchRequest Challenge(ContentCatalog c, CareerSaveData career, StreetChallenger ch)
        {
            Register(c, ch);
            string mine = CustomTeams.IsYours(CustomTeams.TeamId) && c.Team(CustomTeams.TeamId) != null ? CustomTeams.TeamId : DefaultContent.PlayerCrewId;
            bool one = ch.Size == 1;
            return new MatchRequest
            {
                Mode = one ? GameMode.OneOnOne : GameMode.Street,
                HomeTeamId = mine,
                AwayTeamId = ch.TeamId,
                CourtId = c.Team(ch.TeamId).homeCourtId,
                RulesId = one ? "rules.oneonone" : RulesId,
                TeamSize = one ? 3 : ch.Size,
                StreetRules = true,
                DifficultyId = career.settings.difficultyId,
                ContextId = "street:" + ch.Id,
            };
        }

        public static StreetChallenger FromContext(string contextId)
        {
            if (contextId == null || !contextId.StartsWith("street:", StringComparison.Ordinal)) return null;
            return Find(contextId.Substring(7));
        }

        /// <summary>Rep for a result: wins pay by tier plus 5 per ankle breaker; losses cost a little. First wins over a caller pay double.</summary>
        public static int ApplyResult(StreetSaveData s, StreetChallenger ch, bool won, int ankles)
        {
            if (s == null || ch == null) return 0;
            int delta;
            s.ankleBreakers += Math.Max(0, ankles);
            if (won)
            {
                bool first = !s.beaten.Contains(ch.Id);
                delta = (25 + 20 * ch.Tier) * (first ? 2 : 1) + 5 * Math.Max(0, ankles);
                if (first) s.beaten.Add(ch.Id);
                s.wins++;
                s.streak++;
                s.bestStreak = Math.Max(s.bestStreak, s.streak);
            }
            else
            {
                delta = -5;
                s.losses++;
                s.streak = 0;
            }
            s.rep = Math.Max(0, s.rep + delta);
            return delta;
        }

        public static bool KingOfThePark(StreetSaveData s) => s != null && Array.TrueForAll(Challengers, c => s.beaten.Contains(c.Id));
    }

    // ====================================================================== Tournament Builder

    [Serializable]
    public class CustomCupSaveData
    {
        public string name = "My Tournament";
        /// <summary>Teams in seed order (best first). Empty = nothing running.</summary>
        public List<string> field = new List<string>();
        public string yourTeam;
        /// <summary>Players a side: 2, 3 (half court) or 5 (Full Court).</summary>
        public int format = 3;
        public List<ScheduledGame> games = new List<ScheduledGame>();
        public bool finished;
        public string championId;
        public int edition;
        public int titles;

        public bool Active => field.Count >= 4 && !finished;
    }

    public enum CustomCupOutcome { None = 0, Advanced = 1, Champion = 2, Eliminated = 3 }

    /// <summary>
    /// Tournament Builder: name it, pick 4, 8 or 16 teams (any you can play), the format (2-on-2, 3-on-3
    /// or Full Court 5-on-5) and your team; teams are seeded by strength and play a knockout. Games
    /// you're not in are simulated.
    /// </summary>
    public static class CustomCup
    {
        public static readonly int[] Sizes = { 4, 8, 16 };
        public static readonly int[] Formats = { 2, 3, 5 };

        public static string FormatName(int format) => format == 5 ? "FULL COURT 5 ON 5" : format + " ON " + format;

        /// <summary>Teams you can put in a tournament: league, circuit, rival crews, your own and unlocked secret teams.</summary>
        public static List<TeamDef> Pool(ContentCatalog c, CareerSaveData career)
        {
            var list = new List<TeamDef>();
            foreach (var t in Secrets.PlayableTeams(c, career?.secrets ?? new SecretsSaveData())) if (!list.Contains(t)) list.Add(t);
            foreach (var t in c.Teams)
                if ((t.tier == TeamTier.League || t.tier == TeamTier.Circuit || t.tier == TeamTier.Rival) && !list.Contains(t)) list.Add(t);
            return list;
        }

        /// <summary>Why the tournament can't start, or null.</summary>
        public static string CannotStart(List<string> teams, string yourTeam)
        {
            if (Array.IndexOf(Sizes, teams.Count) < 0) return "Pick 4, 8 or 16 teams (you have " + teams.Count + ").";
            if (yourTeam == null || !teams.Contains(yourTeam)) return "Pick which team is yours.";
            if (new HashSet<string>(teams).Count != teams.Count) return "A team is in twice.";
            return null;
        }

        public static bool Start(CustomCupSaveData cup, ContentCatalog c, string name, List<string> teams, string yourTeam, int format)
        {
            if (CannotStart(teams, yourTeam) != null) return false;
            cup.name = CustomTeams.Clean(name, 20, "My Tournament");
            cup.format = Array.IndexOf(Formats, format) >= 0 ? format : 3;
            cup.yourTeam = yourTeam;
            cup.edition++;
            cup.finished = false;
            cup.championId = null;
            cup.games.Clear();
            var seeded = new List<string>(teams);
            seeded.Sort((a, b) =>
            {
                int d = ArcadeEngine.TeamOverall(c, c.Team(b)).CompareTo(ArcadeEngine.TeamOverall(c, c.Team(a)));
                return d != 0 ? d : string.CompareOrdinal(a, b);
            });
            cup.field = seeded;
            int n = seeded.Count;
            var order = BracketOrder(n);
            for (int k = 0; k < n; k += 2)
                cup.games.Add(new ScheduledGame { round = 1, week = k / 2, homeId = seeded[order[k]], awayId = seeded[order[k + 1]] });
            return true;
        }

        /// <summary>Standard bracket: 1 v N, and the top seeds can only meet late (1 and 2 in the final).</summary>
        public static int[] BracketOrder(int n)
        {
            var order = new List<int> { 0, 1 };
            while (order.Count < n)
            {
                int size = order.Count * 2;
                var next = new List<int>();
                foreach (int s in order) { next.Add(s); next.Add(size - 1 - s); }
                order = next;
            }
            return order.ToArray();
        }

        public static int Rounds(CustomCupSaveData cup) => cup.field.Count <= 1 ? 0 : (int)Math.Round(Math.Log(cup.field.Count, 2));

        public static string RoundName(CustomCupSaveData cup, int round)
        {
            int left = Rounds(cup) - round;
            return left == 0 ? "FINAL" : left == 1 ? "SEMIFINALS" : left == 2 ? "QUARTERFINALS" : "ROUND " + round;
        }

        public static ScheduledGame NextGame(CustomCupSaveData cup)
        {
            if (cup == null || !cup.Active) return null;
            foreach (var g in cup.games) if (!g.played && g.Involves(cup.yourTeam)) return g;
            return null;
        }

        public static MatchRequest NextMatch(CustomCupSaveData cup, ContentCatalog c, string difficultyId)
        {
            var g = NextGame(cup);
            if (g == null) return null;
            string opp = g.homeId == cup.yourTeam ? g.awayId : g.homeId;
            bool full = cup.format == 5;
            return new MatchRequest
            {
                Mode = GameMode.CustomCup,
                HomeTeamId = cup.yourTeam,
                AwayTeamId = opp,
                CourtId = c.Team(g.homeId)?.homeCourtId ?? c.Team(cup.yourTeam)?.homeCourtId,
                RulesId = full ? FullCourt.RulesId : DefaultContent.DefaultRulesId,
                FullCourt = full,
                TeamSize = full ? 0 : cup.format,
                DifficultyId = difficultyId,
                Round = g.round,
                ContextId = "ccup:e" + cup.edition + ":r" + g.round,
            };
        }

        public static CustomCupOutcome ApplyResult(CustomCupSaveData cup, ContentCatalog c, int yourScore, int theirScore)
        {
            var g = NextGame(cup);
            if (g == null) return CustomCupOutcome.None;
            if (yourScore == theirScore) yourScore++;
            bool home = g.homeId == cup.yourTeam;
            g.homeScore = home ? yourScore : theirScore;
            g.awayScore = home ? theirScore : yourScore;
            g.played = true;
            bool won = g.WinnerId == cup.yourTeam;
            var rng = new SeededRandom(StableHash.Of("ccup:" + cup.edition + ":" + g.round));
            int round = g.round;
            while (true)
            {
                foreach (var o in cup.games) if (!o.played && o.round == round) SeasonEngine.SimulateGame(o, c, rng);
                var games = cup.games.FindAll(x => x.round == round);
                if (games.Count == 1)
                {
                    cup.championId = games[0].WinnerId;
                    cup.finished = true;
                    break;
                }
                for (int k = 0; k + 1 < games.Count; k += 2)
                {
                    string a = games[k].WinnerId, b = games[k + 1].WinnerId;
                    bool aFirst = cup.field.IndexOf(a) <= cup.field.IndexOf(b);
                    cup.games.Add(new ScheduledGame { round = round + 1, week = k / 2, homeId = aFirst ? a : b, awayId = aFirst ? b : a });
                }
                round++;
                if (won) break;
            }
            if (!won) return CustomCupOutcome.Eliminated;
            if (cup.finished)
            {
                cup.titles++;
                return CustomCupOutcome.Champion;
            }
            return CustomCupOutcome.Advanced;
        }
    }
}
