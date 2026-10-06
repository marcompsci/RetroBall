using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        private static object EncodeSeason(SeasonSaveData s) => s == null ? null : new Dictionary<string, object>
        {
            ["n"] = s.seasonNumber, ["seed"] = (double)s.seed, ["teams"] = Strings(s.teamIds),
            ["week"] = s.currentWeek, ["weeks"] = s.weeks, ["games"] = List(s.games, EncodeGame),
        };

        private static SeasonSaveData DecodeSeason(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var s = new SeasonSaveData
            {
                seasonNumber = Int(o, "n", 1), seed = (uint)Math.Max(0, Num(o, "seed", 1)), teamIds = StrList(o, "teams"),
                currentWeek = Math.Max(0, Int(o, "week", 0)), weeks = Math.Max(1, Int(o, "weeks", 10)),
            };
            foreach (var item in Arr(o, "games")) if (item is Dictionary<string, object> g) s.games.Add(DecodeGame(g));
            return s.teamIds.Count >= 2 ? s : null;
        }

        private static object EncodeLegacy(LegacySaveData l)
        {
            if (l == null || !l.active) return new Dictionary<string, object> { ["active"] = false };
            return new Dictionary<string, object>
            {
                ["active"] = true, ["seed"] = (double)l.seed, ["stage"] = (int)l.stage, ["age"] = l.age, ["pro"] = l.proSeason,
                ["team"] = l.teamId, ["school"] = l.schoolName, ["college"] = l.collegeName, ["tier"] = l.collegeTier,
                ["season"] = EncodeSeason(l.season),
                ["games"] = List(l.games, g => new Dictionary<string, object>
                {
                    ["w"] = g.week, ["r"] = g.round, ["o"] = g.opponent, ["won"] = g.won, ["sim"] = g.simmed, ["us"] = g.us, ["them"] = g.them,
                    ["pts"] = g.pts, ["ast"] = g.ast, ["reb"] = g.reb, ["stl"] = g.stl, ["blk"] = g.blk, ["g"] = g.grade,
                }),
                ["xp"] = l.xp, ["sp"] = l.skillPoints, ["trainer"] = l.trainerSessions, ["skills"] = Strings(l.skills), ["growth"] = l.growth,
                ["fans"] = l.fans, ["cash"] = (double)l.cash, ["trust"] = l.trust, ["stars"] = l.stars, ["stock"] = l.stock, ["pick"] = l.draftPick,
                ["salary"] = l.salary, ["years"] = l.contractYears, ["brands"] = Strings(l.endorsements),
                ["offers"] = List(l.offers, o => new Dictionary<string, object> { ["t"] = o.teamId, ["n"] = o.name, ["v"] = o.value, ["y"] = o.years, ["p"] = o.pitch }),
                ["history"] = List(l.history, h => new Dictionary<string, object>
                {
                    ["l"] = h.label, ["t"] = h.team, ["age"] = h.age, ["g"] = h.games, ["w"] = h.wins, ["pts"] = h.pts, ["ast"] = h.ast, ["reb"] = h.reb,
                    ["res"] = h.result, ["aw"] = Strings(h.awards),
                }),
                ["seen"] = Strings(l.seenEvents), ["event"] = l.pendingEvent, ["legacy"] = l.legacyPoints, ["hof"] = l.hallOfFame,
                ["shots"] = ShotCharts.Encode(l.seasonChart), ["careerShots"] = ShotCharts.Encode(l.careerChart),
            };
        }

        private static LegacySaveData DecodeLegacy(Dictionary<string, object> o)
        {
            var l = new LegacySaveData();
            if (o == null || !Bool(o, "active", false)) return l;
            try
            {
                l.active = true;
                l.seed = (uint)Math.Max(1, Num(o, "seed", 1));
                l.stage = (LegacyStage)Math.Max(0, Math.Min((int)LegacyStage.Retired, Int(o, "stage", 0)));
                l.age = Math.Max(14, Math.Min(50, Int(o, "age", 17)));
                l.proSeason = Math.Max(0, Int(o, "pro", 0));
                l.teamId = Str(o, "team", Legacy.SchoolId);
                l.schoolName = Str(o, "school", "Central High");
                l.collegeName = Str(o, "college", "");
                l.collegeTier = Math.Max(0, Math.Min(2, Int(o, "tier", 0)));
                l.season = DecodeSeason(Obj(o, "season"));
                foreach (var item in Arr(o, "games"))
                    if (item is Dictionary<string, object> g)
                        l.games.Add(new LegacyGameLine
                        {
                            week = Int(g, "w", 0), round = Int(g, "r", 0), opponent = Str(g, "o", ""), won = Bool(g, "won", false), simmed = Bool(g, "sim", false),
                            us = Int(g, "us", 0), them = Int(g, "them", 0), pts = Int(g, "pts", 0), ast = Int(g, "ast", 0), reb = Int(g, "reb", 0),
                            stl = Int(g, "stl", 0), blk = Int(g, "blk", 0), grade = Str(g, "g", "C"),
                        });
                l.xp = Math.Max(0, Int(o, "xp", 0));
                l.skillPoints = Math.Max(0, Int(o, "sp", 0));
                l.trainerSessions = Math.Max(0, Int(o, "trainer", 0));
                l.skills = StrList(o, "skills").FindAll(id => Legacy.Skill(id) != null);
                l.growth = Math.Max(-30, Math.Min(30, Int(o, "growth", 0)));
                l.fans = Math.Max(0, Int(o, "fans", 0));
                l.cash = Math.Max(0L, (long)Num(o, "cash", 0));
                l.trust = Math.Max(0, Math.Min(100, Int(o, "trust", 50)));
                l.stars = Math.Max(0, Math.Min(5, Int(o, "stars", 0)));
                l.stock = Math.Max(0, Math.Min(100, Int(o, "stock", 0)));
                l.draftPick = Math.Max(0, Int(o, "pick", 0));
                l.salary = Math.Max(0, Int(o, "salary", 0));
                l.contractYears = Math.Max(0, Int(o, "years", 0));
                l.endorsements = StrList(o, "brands").FindAll(id => Legacy.Brand(id) != null);
                foreach (var item in Arr(o, "offers"))
                    if (item is Dictionary<string, object> f)
                        l.offers.Add(new LegacyOffer { teamId = Str(f, "t", ""), name = Str(f, "n", ""), value = Int(f, "v", 0), years = Int(f, "y", 1), pitch = Str(f, "p", "") });
                foreach (var item in Arr(o, "history"))
                    if (item is Dictionary<string, object> h)
                        l.history.Add(new LegacySeason
                        {
                            label = Str(h, "l", ""), team = Str(h, "t", ""), age = Int(h, "age", 0), games = Int(h, "g", 0), wins = Int(h, "w", 0),
                            pts = Int(h, "pts", 0), ast = Int(h, "ast", 0), reb = Int(h, "reb", 0), result = Str(h, "res", ""), awards = StrList(h, "aw"),
                        });
                l.seenEvents = StrList(o, "seen");
                l.pendingEvent = Str(o, "event", null);
                if (l.pendingEvent != null && Array.Find(Legacy.Events, e => e.Id == l.pendingEvent) == null) l.pendingEvent = null;
                l.legacyPoints = Math.Max(0, Int(o, "legacy", 0));
                l.hallOfFame = Bool(o, "hof", false);
                l.seasonChart = ShotCharts.Decode(Str(o, "shots", ""));
                l.careerChart = ShotCharts.Decode(Str(o, "careerShots", ""));
                // A season stage with no schedule can't continue: rebuild where it's safe (the UI starts the next one).
                if ((l.stage == LegacyStage.HighSchool) && l.season == null) return new LegacySaveData();
            }
            catch (Exception)
            {
                return new LegacySaveData();
            }
            return l;
        }
    
        private static object EncodeStreet(StreetSaveData t)
        {
            t = t ?? new StreetSaveData();
            return new Dictionary<string, object>
            {
                ["rep"] = t.rep, ["beaten"] = Strings(t.beaten), ["wins"] = t.wins, ["losses"] = t.losses,
                ["ankles"] = t.ankleBreakers, ["best"] = t.bestStreak, ["streak"] = t.streak,
            };
        }

        private static StreetSaveData DecodeStreet(Dictionary<string, object> o)
        {
            var t = new StreetSaveData();
            if (o == null) return t;
            t.rep = Math.Max(0, Int(o, "rep", 0));
            t.beaten = StrList(o, "beaten").FindAll(id => Street.Find(id) != null);
            t.wins = Math.Max(0, Int(o, "wins", 0));
            t.losses = Math.Max(0, Int(o, "losses", 0));
            t.ankleBreakers = Math.Max(0, Int(o, "ankles", 0));
            t.bestStreak = Math.Max(0, Int(o, "best", 0));
            t.streak = Math.Max(0, Int(o, "streak", 0));
            return t;
        }

        private static object EncodeCustomCup(CustomCupSaveData t)
        {
            t = t ?? new CustomCupSaveData();
            return new Dictionary<string, object>
            {
                ["name"] = t.name, ["field"] = Strings(t.field), ["you"] = t.yourTeam, ["format"] = t.format, ["games"] = List(t.games, EncodeGame),
                ["finished"] = t.finished, ["champion"] = t.championId, ["edition"] = t.edition, ["titles"] = t.titles,
            };
        }

        private static CustomCupSaveData DecodeCustomCup(Dictionary<string, object> o)
        {
            var t = new CustomCupSaveData();
            if (o == null) return t;
            t.name = Str(o, "name", "My Tournament");
            t.field = StrList(o, "field");
            t.yourTeam = Str(o, "you", null);
            t.format = Int(o, "format", 3);
            if (Array.IndexOf(CustomCup.Formats, t.format) < 0) t.format = 3;
            foreach (var item in Arr(o, "games")) if (item is Dictionary<string, object> g) t.games.Add(DecodeGame(g));
            t.finished = Bool(o, "finished", false);
            t.championId = Str(o, "champion", null);
            t.edition = Math.Max(0, Int(o, "edition", 0));
            t.titles = Math.Max(0, Int(o, "titles", 0));
            if (Array.IndexOf(CustomCup.Sizes, t.field.Count) < 0) { t.field.Clear(); t.games.Clear(); t.finished = false; }
            return t;
        }
    }
}
