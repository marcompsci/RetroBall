using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One league or street player's career in Dynasty mode.</summary>
    [Serializable]
    public class DynastyPlayer
    {
        public string id;
        public int age;
        /// <summary>Rating change since their first season (applied to every attribute).</summary>
        public int delta;
        public int peak;
        public int seasons;
        public bool retired;
    }

    /// <summary>A generated rookie (draft class), saved so they exist on every launch.</summary>
    [Serializable]
    public class RookieData
    {
        public string id;
        public string first;
        public string last;
        public int number;
        public int archetype;
        public int skin, hair, hairColor, body, height;
        public int offset;
    }

    [Serializable]
    public class RosterSwap
    {
        public string teamId;
        public string oldId;
        public string newId;
    }

    [Serializable]
    public class HallEntry
    {
        public string name;
        public string team;
        public int peak;
        public int seasons;
        public int year;
    }

    [Serializable]
    public class ChampionEntry
    {
        public int season;
        public string teamId;
    }

    [Serializable]
    public class DynastySaveData
    {
        /// <summary>Off-seasons processed (league years).</summary>
        public int year;
        public int lastSeasonProcessed;
        public List<DynastyPlayer> players = new List<DynastyPlayer>();
        public List<RookieData> rookies = new List<RookieData>();
        public List<RosterSwap> swaps = new List<RosterSwap>();
        public List<HallEntry> hall = new List<HallEntry>();
        public List<ChampionEntry> champions = new List<ChampionEntry>();
        /// <summary>Your draft choices this off-season (rookie ids). Empty = no pick waiting.</summary>
        public List<string> draftPool = new List<string>();
    }

    /// <summary>What happened in an off-season, for the Rise hub to show.</summary>
    public sealed class OffSeasonReport
    {
        public int Year;
        public string ChampionId;
        public readonly List<string> Retired = new List<string>();
        public readonly List<string> HallOfFame = new List<string>();
        public readonly List<string> Improved = new List<string>();
        public readonly List<string> Prospects = new List<string>();
    }

    /// <summary>
    /// Dynasty: the league lives on between Rise seasons. Every off-season players age (young ones
    /// improve, veterans slow down), old players retire (the best go to the Hall of Fame), teams draft
    /// rookies to replace them, and your crew gets a draft pick from three prospects. Champions are
    /// remembered by year. Your own player never declines.
    /// </summary>
    public static class DynastyEngine
    {
        public const int RetireAge = 35;
        public const int HallPeak = 78;
        public const int HallSeasons = 4;
        public const int ProspectCount = 3;

        private static readonly string[] FirstNames =
        {
            "Ari", "Bo", "Cal", "Dax", "Eli", "Fen", "Gus", "Hal", "Ira", "Jax", "Kai", "Leo", "Max", "Nico", "Oz", "Pax",
            "Quin", "Rio", "Sol", "Tai", "Uri", "Vin", "Wes", "Xan", "Yuri", "Zed", "Ama", "Bea", "Cleo", "Dee", "Eve", "Gia",
        };
        private static readonly string[] Syllables =
        {
            "ban", "cor", "del", "fin", "gar", "hol", "jen", "kes", "lan", "mor", "nel", "pry", "quil", "ros", "sev", "tal",
            "ver", "wex", "yor", "zan", "ash", "bri", "cad", "dun", "elt", "fro", "gil", "hart",
        };

        private static bool Tracked(TeamDef t) => t.tier == TeamTier.League || t.tier == TeamTier.Circuit || t.tier == TeamTier.Rival;

        private static int StartingAge(string id) => 20 + (int)(StableHash.Of("age:" + id) % 13); // 20..32

        public static DynastyPlayer Track(DynastySaveData d, string playerId)
        {
            var p = d.players.Find(x => x.id == playerId);
            if (p != null) return p;
            p = new DynastyPlayer { id = playerId, age = StartingAge(playerId) };
            d.players.Add(p);
            return p;
        }

        /// <summary>Age of a player (generated the first time it's asked).</summary>
        public static int AgeOf(DynastySaveData d, string playerId) => Track(d, playerId).age;

        /// <summary>
        /// Rebuilds the dynasty state on a freshly loaded catalog: rookies exist, rosters reflect
        /// retirements, and ratings carry every season's progression. Call once after loading.
        /// </summary>
        public static void Apply(ContentCatalog c, DynastySaveData d)
        {
            if (c == null || d == null) return;
            foreach (var r in d.rookies) if (c.Player(r.id) == null) c.Players.Add(Build(c, r));
            foreach (var s in d.swaps)
            {
                var team = c.Team(s.teamId);
                if (team == null) continue;
                int i = team.rosterPlayerIds.IndexOf(s.oldId);
                if (i >= 0 && c.Player(s.newId) != null) team.rosterPlayerIds[i] = s.newId;
            }
            foreach (var p in d.players)
            {
                if (p.delta == 0) continue;
                var def = c.Player(p.id);
                if (def != null) def.attributes = def.attributes.Offset(p.delta);
            }
        }

        private static PlayerDef Build(ContentCatalog c, RookieData r)
        {
            var archetype = c.Archetypes.Find(a => (int)a.archetype == r.archetype) ?? c.Archetypes[0];
            return new PlayerDef
            {
                id = r.id, firstName = r.first, lastName = r.last, jerseyNumber = r.number, archetypeId = archetype.id,
                attributes = archetype.baseline.Offset(r.offset),
                appearance = new AppearanceDef(r.skin, r.hair, r.hairColor, (BodyType)r.body, r.height),
            };
        }

        private static RookieData NewRookie(ContentCatalog c, DynastySaveData d, SeededRandom rng, int archetype, int offset, TeamDef team)
        {
            string last = "";
            int parts = 2;
            for (int i = 0; i < parts; i++) last += Syllables[rng.Range(0, Syllables.Length)];
            last = char.ToUpperInvariant(last[0]) + last.Substring(1);
            int number = rng.Range(0, 100);
            if (team != null)
                for (int guard = 0; guard < 100 && team.rosterPlayerIds.Exists(id => c.Player(id)?.jerseyNumber == number); guard++)
                    number = (number + 7) % 100;
            var r = new RookieData
            {
                id = "player.rookie.y" + (d.year + 1) + "." + d.rookies.Count,
                first = FirstNames[rng.Range(0, FirstNames.Length)], last = last, number = number, archetype = archetype,
                skin = rng.Range(0, 6), hair = rng.Range(0, 6), hairColor = rng.Range(0, 5), body = rng.Range(0, 3), height = rng.Range(0, 3),
                offset = offset,
            };
            d.rookies.Add(r);
            c.Players.Add(Build(c, r));
            Track(d, r.id).age = 20 + rng.Range(0, 3);
            return r;
        }

        /// <summary>Is there an off-season to run (a finished Rise season not yet processed)?</summary>
        public static bool OffSeasonDue(CareerSaveData career) =>
            career?.rise?.season != null && career.rise.stage == RiseStage.Complete
            && career.rise.season.seasonNumber > (career.dynasty?.lastSeasonProcessed ?? 0);

        /// <summary>Runs the off-season after a finished Rise season. Safe to call once per season.</summary>
        public static OffSeasonReport OffSeason(CareerSaveData career, ContentCatalog c)
        {
            if (!OffSeasonDue(career)) return null;
            if (career.dynasty == null) career.dynasty = new DynastySaveData();
            var d = career.dynasty;
            var season = career.rise.season;
            var report = new OffSeasonReport { Year = d.year + 1, ChampionId = season.championId };
            d.lastSeasonProcessed = season.seasonNumber;
            d.year++;
            if (!string.IsNullOrEmpty(season.championId)) d.champions.Add(new ChampionEntry { season = season.seasonNumber, teamId = season.championId });

            var rng = new SeededRandom(StableHash.Of("dynasty:" + d.year));
            var yours = new HashSet<string>(career.rise.signed);
            foreach (var id in career.rise.teammates) yours.Add(id);

            foreach (var team in c.Teams)
            {
                if (!Tracked(team)) continue;
                for (int slot = 0; slot < team.rosterPlayerIds.Count; slot++)
                {
                    string id = team.rosterPlayerIds[slot];
                    var def = c.Player(id);
                    if (def == null || yours.Contains(id)) continue;
                    var p = Track(d, id);
                    p.age++;
                    p.seasons++;
                    int change = Progression(p.age) + (rng.Range(0, 3) - 1);
                    change = Math.Max(-12 - p.delta, Math.Min(10 - p.delta, change));
                    if (change != 0)
                    {
                        p.delta += change;
                        def.attributes = def.attributes.Offset(change);
                        if (change >= 2 && team.tier == TeamTier.League) report.Improved.Add(def.DisplayName);
                    }
                    p.peak = Math.Max(p.peak, def.attributes.Overall);

                    bool retire = p.age >= RetireAge || (p.age >= 33 && rng.Range(0, 3) == 0);
                    if (!retire) continue;
                    p.retired = true;
                    report.Retired.Add(def.DisplayName);
                    if (p.peak >= HallPeak && p.seasons >= HallSeasons)
                    {
                        d.hall.Add(new HallEntry { name = def.DisplayName, team = team.FullName, peak = p.peak, seasons = p.seasons, year = d.year });
                        report.HallOfFame.Add(def.DisplayName);
                    }
                    var archetype = c.ArchetypeById(def.archetypeId);
                    var rookie = NewRookie(c, d, rng, archetype != null ? (int)archetype.archetype : 0, team.tier == TeamTier.League ? -3 : -5, team);
                    d.swaps.Add(new RosterSwap { teamId = team.id, oldId = id, newId = rookie.id });
                    team.rosterPlayerIds[slot] = rookie.id;
                }
            }

            // Your draft pick: three prospects with different styles.
            d.draftPool.Clear();
            var used = new HashSet<int>();
            for (int i = 0; i < ProspectCount; i++)
            {
                int a;
                do a = rng.Range(0, 12); while (!used.Add(a));
                var r = NewRookie(c, d, rng, a, -2 + rng.Range(0, 4), null);
                d.draftPool.Add(r.id);
                report.Prospects.Add(r.first + " " + r.last);
            }
            return report;
        }

        /// <summary>Rating change for a season at <paramref name="age"/>: young players grow, veterans fade.</summary>
        public static int Progression(int age)
        {
            if (age <= 23) return 2;
            if (age <= 26) return 1;
            if (age <= 29) return 0;
            if (age <= 32) return -1;
            return -2;
        }

        /// <summary>Takes a prospect: they join your crew's pool for free. The others go elsewhere.</summary>
        public static bool Draft(CareerSaveData career, ContentCatalog c, string rookieId)
        {
            var d = career?.dynasty;
            if (d == null || !d.draftPool.Contains(rookieId) || c.Player(rookieId) == null) return false;
            if (!career.rise.signed.Contains(rookieId)) career.rise.signed.Add(rookieId);
            if (!career.rise.recruitable.Contains(rookieId)) career.rise.recruitable.Add(rookieId);
            d.draftPool.Clear();
            return true;
        }

        /// <summary>League titles by team, most first (all-time champions table).</summary>
        public static List<KeyValuePair<string, int>> TitlesByTeam(DynastySaveData d)
        {
            var counts = new Dictionary<string, int>();
            foreach (var ch in d.champions)
                counts[ch.teamId] = counts.TryGetValue(ch.teamId, out int n) ? n + 1 : 1;
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            return list;
        }
    }
}
