using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Full Court 5-on-5: geometry, rules id, and the extra players a team needs. The simulation runs
    /// in the attacking team's frame (its hoop is always at the "top"), and the whole picture turns 180°
    /// when possession changes, so shots, passes and the AI work exactly as in the half-court game.
    /// <see cref="MatchSimulation.ToWorldCourt"/> maps back to the fixed court the views draw.
    /// </summary>
    public static class FullCourt
    {
        public const int TeamSize = 5;
        /// <summary>Bench players per team (subs come on when someone's tired).</summary>
        public const int BenchSize = 2;
        public const string RulesId = "rules.fullcourt";
        /// <summary>Length of each half (baseline to the half-court line), metres.</summary>
        public const float HalfLength = 12f;

        /// <summary>Simulation geometry: one hoop, the whole floor in front of it.</summary>
        public static CourtGeometry Geometry() => new CourtGeometry { depth = HalfLength * 2f };

        /// <summary>One half, for drawing (the art is two halves back to back).</summary>
        public static CourtGeometry HalfGeometry() => new CourtGeometry { depth = HalfLength };

        /// <summary>Half-court line in the attacking team's frame.</summary>
        public static float MidY => HalfLength;

        // Original, generic names for the extra players (no real people).
        private static readonly string[] Firsts =
        {
            "Theo", "Imani", "Rafa", "Jonah", "Priya", "Marcus", "Lena", "Dario", "Ines", "Caleb",
            "Noor", "Felix", "Zuri", "Owen", "Mika", "Tariq", "Sol", "Hana", "Rocco", "Ada",
        };
        private static readonly string[] Lasts =
        {
            "Brooks", "Okoye", "Lindgren", "Paz", "Marlow", "Achebe", "Tanaka", "Reyes", "Quinlan", "Varga",
            "Holt", "Mensah", "Fairley", "Dunmore", "Sato", "Ellery", "Navarro", "Kincaid", "Abara", "Whitlow",
        };
        // Bigs and wings to round out three-player crews.
        private static readonly Archetype[] Roles =
        {
            Archetype.PostAnchor, Archetype.LockdownWing, Archetype.GlassCleaner, Archetype.DeepShooter, Archetype.StretchForward,
        };

        /// <summary>
        /// Deterministic extra players so a team has <paramref name="needed"/> more bodies: same team, same
        /// look every time, a little below the team's own players (they come off the bench).
        /// </summary>
        public static List<PlayerDef> Reserves(TeamDef team, ContentCatalog c, int needed)
        {
            var list = new List<PlayerDef>();
            if (team == null || c == null || needed <= 0) return list;
            uint h = StableHash.Of(team.id + ".reserves");
            var usedNumbers = new HashSet<int>();
            foreach (var id in team.rosterPlayerIds)
            {
                var p = c.Player(id);
                if (p != null) usedNumbers.Add(p.jerseyNumber);
            }
            for (int i = 0; i < needed; i++)
            {
                h = StableHash.Next(h);
                var role = Roles[(int)(((h >> 3) + (uint)i) % (uint)Roles.Length)];
                var archetype = FindArchetype(c, role);
                string id = "player.res." + team.id.Replace('.', '_') + "." + (i + 1);
                int number = 10 + (int)(h % 40);
                while (usedNumbers.Contains(number)) number = number % 54 + 1;
                usedNumbers.Add(number);
                var baseline = archetype != null ? archetype.baseline.Offset(-3) : new AttributeSet();
                list.Add(new PlayerDef
                {
                    id = id,
                    firstName = Firsts[(int)(h % (uint)Firsts.Length)],
                    lastName = Lasts[(int)((h / 7) % (uint)Lasts.Length)],
                    jerseyNumber = number,
                    archetypeId = archetype?.id,
                    attributes = DefaultContent.Personalize(baseline, id),
                    appearance = DefaultContent.AppearanceFromSeed(id, role),
                });
            }
            return list;
        }

        private static ArchetypeDef FindArchetype(ContentCatalog c, Archetype a)
        {
            foreach (var def in c.Archetypes) if (def.archetype == a) return def;
            return c.Archetypes.Count > 0 ? c.Archetypes[0] : null;
        }

        /// <summary>Rotates a facing by 180° (the picture turning when possession changes).</summary>
        public static Facing8 Turn(Facing8 f) => (Facing8)(((int)f + 4) % 8);
    }
}
