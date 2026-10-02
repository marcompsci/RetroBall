using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum LoadStatus
    {
        /// <summary>Loaded normally.</summary>
        Ok = 0,
        /// <summary>No save existed; a fresh career was created.</summary>
        New = 1,
        /// <summary>An older save version was upgraded.</summary>
        Migrated = 2,
        /// <summary>The file was unreadable; a fresh career was created (the bad file should be kept as a backup).</summary>
        Recovered = 3,
    }

    /// <summary>
    /// Versioned JSON encoding for <see cref="CareerSaveData"/>. Reading is forgiving: missing
    /// or wrongly-typed fields fall back to defaults instead of failing, and anything unreadable
    /// produces a fresh career with <see cref="LoadStatus.Recovered"/>.
    /// </summary>
    public static class SaveCodec
    {
        public static string Encode(CareerSaveData d)
        {
            var o = new Dictionary<string, object>
            {
                ["version"] = CareerSaveData.CurrentVersion,
                ["nickname"] = d.nickname,
                ["signalPoints"] = d.signalPoints,
                ["fans"] = d.fans,
                ["gamesSinceUpgrade"] = d.gamesSinceUpgrade,
                ["upgrades"] = List(d.upgrades, u => new Dictionary<string, object> { ["id"] = u.id, ["level"] = u.level }),
                ["ownedCosmetics"] = Strings(d.ownedCosmetics),
                ["equipped"] = new Dictionary<string, object>
                {
                    ["jersey"] = d.equippedJersey, ["shoes"] = d.equippedShoes, ["banner"] = d.equippedBanner,
                    ["celebration"] = d.equippedCelebration, ["move"] = d.equippedMove,
                },
                ["totals"] = new Dictionary<string, object>
                {
                    ["games"] = d.totals.games, ["wins"] = d.totals.wins, ["losses"] = d.totals.losses,
                    ["points"] = d.totals.points, ["assists"] = d.totals.assists, ["rebounds"] = d.totals.rebounds,
                    ["steals"] = d.totals.steals, ["blocks"] = d.totals.blocks, ["fgm"] = d.totals.fieldGoalsMade,
                    ["fga"] = d.totals.fieldGoalsAttempted, ["greens"] = d.totals.greens, ["championships"] = d.totals.championships,
                },
                ["practice"] = new Dictionary<string, object>
                {
                    ["freeShootMakes"] = d.practice.freeShootMakes, ["freeShootStreak"] = d.practice.freeShootStreak,
                    ["passingScore"] = d.practice.passingScore, ["dribbleLaneTime"] = (double)d.practice.dribbleLaneTime,
                },
                ["settings"] = new Dictionary<string, object>
                {
                    ["musicVolume"] = (double)d.settings.musicVolume, ["sfxVolume"] = (double)d.settings.sfxVolume,
                    ["haptics"] = d.settings.haptics, ["screenShake"] = d.settings.screenShake,
                    ["uiScale"] = (double)d.settings.uiScale, ["colorblindContrast"] = d.settings.colorblindContrast,
                    ["difficultyId"] = d.settings.difficultyId,
                },
                ["rise"] = EncodeRise(d.rise),
                ["appliedMatchIds"] = Strings(d.appliedMatchIds),
            };
            return MiniJson.Write(o);
        }

        private static object EncodeRise(RiseSaveData r)
        {
            return new Dictionary<string, object>
            {
                ["stage"] = (int)r.stage,
                ["circuitBeaten"] = Strings(r.circuitBeaten),
                ["energy"] = r.energy,
                ["chemistry"] = r.chemistry,
                ["seenEvents"] = Strings(r.seenEvents),
                ["pendingEventId"] = r.pendingEventId,
                ["seasonsPlayed"] = r.seasonsPlayed,
                ["season"] = r.season == null ? null : new Dictionary<string, object>
                {
                    ["seasonNumber"] = r.season.seasonNumber,
                    ["seed"] = (double)r.season.seed,
                    ["teamIds"] = Strings(r.season.teamIds),
                    ["currentWeek"] = r.season.currentWeek,
                    ["weeks"] = r.season.weeks,
                    ["championId"] = r.season.championId,
                    ["games"] = List(r.season.games, g => new Dictionary<string, object>
                    {
                        ["week"] = g.week, ["round"] = g.round, ["home"] = g.homeId, ["away"] = g.awayId,
                        ["played"] = g.played, ["homeScore"] = g.homeScore, ["awayScore"] = g.awayScore,
                    }),
                },
            };
        }

        /// <summary>Decodes a save. Never throws; check <paramref name="status"/>.</summary>
        public static CareerSaveData Decode(string json, ContentCatalog c, out LoadStatus status)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                status = LoadStatus.New;
                return Career.New(c);
            }
            try
            {
                if (!(MiniJson.Read(json) is Dictionary<string, object> o)) throw new MiniJson.JsonException("Root is not an object.");
                int version = Int(o, "version", 0);
                if (version <= 0) throw new MiniJson.JsonException("Missing version.");

                var d = new CareerSaveData
                {
                    nickname = Career.CleanNickname(Str(o, "nickname", "Rook")),
                    signalPoints = Math.Max(0, Int(o, "signalPoints", 0)),
                    fans = Math.Max(0, Int(o, "fans", 0)),
                    gamesSinceUpgrade = Math.Max(0, Int(o, "gamesSinceUpgrade", 99)),
                    ownedCosmetics = StrList(o, "ownedCosmetics"),
                    appliedMatchIds = StrList(o, "appliedMatchIds"),
                };
                foreach (var item in Arr(o, "upgrades"))
                {
                    if (!(item is Dictionary<string, object> u)) continue;
                    string id = Str(u, "id", null);
                    if (id != null) d.upgrades.Add(new UpgradeProgress { id = id, level = Math.Max(0, Int(u, "level", 0)) });
                }
                var eq = Obj(o, "equipped");
                d.equippedJersey = Str(eq, "jersey", null);
                d.equippedShoes = Str(eq, "shoes", null);
                d.equippedBanner = Str(eq, "banner", null);
                d.equippedCelebration = Str(eq, "celebration", null);
                d.equippedMove = Str(eq, "move", null);

                var t = Obj(o, "totals");
                d.totals = new CareerTotals
                {
                    games = Int(t, "games", 0), wins = Int(t, "wins", 0), losses = Int(t, "losses", 0),
                    points = Int(t, "points", 0), assists = Int(t, "assists", 0), rebounds = Int(t, "rebounds", 0),
                    steals = Int(t, "steals", 0), blocks = Int(t, "blocks", 0), fieldGoalsMade = Int(t, "fgm", 0),
                    fieldGoalsAttempted = Int(t, "fga", 0), greens = Int(t, "greens", 0), championships = Int(t, "championships", 0),
                };
                var pr = Obj(o, "practice");
                d.practice = new PracticeBests
                {
                    freeShootMakes = Int(pr, "freeShootMakes", 0), freeShootStreak = Int(pr, "freeShootStreak", 0),
                    passingScore = Int(pr, "passingScore", 0), dribbleLaneTime = (float)Num(pr, "dribbleLaneTime", 0),
                };
                var st = Obj(o, "settings");
                d.settings = new SettingsData
                {
                    musicVolume = Clamp01((float)Num(st, "musicVolume", 0.6)),
                    sfxVolume = Clamp01((float)Num(st, "sfxVolume", 0.9)),
                    haptics = Bool(st, "haptics", true),
                    screenShake = Bool(st, "screenShake", true),
                    uiScale = Math.Max(0.85f, Math.Min(1.25f, (float)Num(st, "uiScale", 1))),
                    colorblindContrast = Bool(st, "colorblindContrast", false),
                    difficultyId = Str(st, "difficultyId", DefaultContent.DefaultDifficultyId),
                };
                d.rise = DecodeRise(Obj(o, "rise"));

                Career.EnsureDefaults(d, c);
                status = version < CareerSaveData.CurrentVersion ? LoadStatus.Migrated : LoadStatus.Ok;
                d.version = CareerSaveData.CurrentVersion;
                return d;
            }
            catch (Exception)
            {
                status = LoadStatus.Recovered;
                return Career.New(c);
            }
        }

        private static RiseSaveData DecodeRise(Dictionary<string, object> o)
        {
            var r = new RiseSaveData();
            if (o == null) return r;
            int stage = Int(o, "stage", 0);
            r.stage = stage >= 0 && stage <= (int)RiseStage.Complete ? (RiseStage)stage : RiseStage.Circuit;
            r.circuitBeaten = StrList(o, "circuitBeaten");
            r.energy = Math.Max(RiseEngine.MinEnergy, Math.Min(100, Int(o, "energy", 100)));
            r.chemistry = Math.Max(0, Math.Min(100, Int(o, "chemistry", 50)));
            r.seenEvents = StrList(o, "seenEvents");
            r.pendingEventId = Str(o, "pendingEventId", null);
            r.seasonsPlayed = Math.Max(0, Int(o, "seasonsPlayed", 0));

            var s = Obj(o, "season");
            if (s != null)
            {
                r.season = new SeasonSaveData
                {
                    seasonNumber = Int(s, "seasonNumber", 1),
                    seed = (uint)Math.Max(0, Num(s, "seed", 1)),
                    teamIds = StrList(s, "teamIds"),
                    currentWeek = Int(s, "currentWeek", 0),
                    weeks = Int(s, "weeks", 10),
                    championId = Str(s, "championId", null),
                };
                foreach (var item in Arr(s, "games"))
                {
                    if (!(item is Dictionary<string, object> g)) continue;
                    r.season.games.Add(new ScheduledGame
                    {
                        week = Int(g, "week", 0), round = Int(g, "round", 0),
                        homeId = Str(g, "home", null), awayId = Str(g, "away", null),
                        played = Bool(g, "played", false), homeScore = Int(g, "homeScore", 0), awayScore = Int(g, "awayScore", 0),
                    });
                }
            }
            // A season stage without a season is inconsistent: fall back to the circuit.
            if ((r.stage == RiseStage.Season || r.stage == RiseStage.Playoffs) && r.season == null) r.stage = RiseStage.Circuit;
            return r;
        }

        // ------------------------------------------------------------------ helpers

        private static List<object> List<T>(List<T> items, Func<T, object> map)
        {
            var list = new List<object>();
            if (items != null) foreach (var i in items) list.Add(map(i));
            return list;
        }

        private static List<object> Strings(List<string> items)
        {
            var list = new List<object>();
            if (items != null) foreach (var s in items) list.Add(s);
            return list;
        }

        private static Dictionary<string, object> Obj(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        private static List<object> Arr(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        private static List<string> StrList(Dictionary<string, object> o, string key)
        {
            var list = new List<string>();
            foreach (var v in Arr(o, key)) if (v is string s) list.Add(s);
            return list;
        }

        private static string Str(Dictionary<string, object> o, string key, string fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is string s ? s : fallback;

        private static double Num(Dictionary<string, object> o, string key, double fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is double d ? d : fallback;

        private static int Int(Dictionary<string, object> o, string key, int fallback)
        {
            double d = Num(o, key, fallback);
            if (d > int.MaxValue) return int.MaxValue;
            if (d < int.MinValue) return int.MinValue;
            return (int)Math.Round(d);
        }

        private static bool Bool(Dictionary<string, object> o, string key, bool fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
