using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// The handcrafted MVP content for The Caller League — every name, colour, and
    /// line of copy here is original to RetroBall. The editor setup tool
    /// writes these into ScriptableObject assets; the runtime falls back to this
    /// class if the assets are missing, so the game is always playable.
    /// </summary>
    public static class DefaultContent
    {
        public const string LeagueName = "The Caller League";
        public const string ChampionshipName = "The Gold Signal Cup";
        public const string CircuitName = "The Blacktop Circuit";
        public const string RookieTournamentName = "First Call Classic";
        public const string GameName = "RetroBall";

        public const string RookPlayerId = "player.crew.rook";
        public const string PlayerCrewId = "crew.first_callers";
        public const string RivalCrewId = "crew.neon_static";
        public const string RivalLeaderId = "player.nst.marlowe";
        public const string DefaultRulesId = "rules.default";
        public const string DefaultDifficultyId = "difficulty.caller";
        public const string PracticeCourtId = "court.practice_lab";

        public static ContentCatalog Create()
        {
            var c = new ContentCatalog();
            c.Archetypes = Archetypes.CreateAll();
            AddCourts(c);
            AddLeagueTeams(c);
            AddCircuitCrews(c);
            AddRules(c);
            AddDifficulties(c);
            AddUpgrades(c);
            AddCosmetics(c);
            AddSeason(c);
            return c;
        }

        // ------------------------------------------------------------------ courts

        private static void AddCourts(ContentCatalog c)
        {
            // Blacktop Circuit (Rise Mode starting courts)
            c.Courts.Add(Court("court.sunset_cage", "Sunset Cage", CourtCircuit.Blacktop,
                "A chain-link cage that glows orange at golden hour.",
                "#4A4A52", "#F4F1DE", "#E07A5F", "#6A3D9A", "#FF7E5F", 0.35f));
            c.Courts.Add(Court("court.pier_nine", "Pier Nine Blacktop", CourtCircuit.Blacktop,
                "Salt air, loose rims, and the loudest benches on the water.",
                "#3D4A56", "#FFFFFF", "#2A9D8F", "#8ECAE6", "#FFB4A2", 0.30f));
            c.Courts.Add(Court("court.overpass_park", "Overpass Park", CourtCircuit.Blacktop,
                "Every dribble echoes under the highway. Night games only.",
                "#2F2F36", "#FFE066", "#9B5DE5", "#1B1B3A", "#F15BB5", 0.45f));
            c.Courts.Add(Court("court.rooftop_ring", "Rooftop Ring", CourtCircuit.Blacktop,
                "A fenced court on top of the old cannery. The wind always wins.",
                "#3A3F4B", "#E0FBFC", "#3D5A80", "#0B132B", "#5BC0BE", 0.40f));
            c.Courts.Add(Court("court.static_lot", "The Static Lot", CourtCircuit.League,
                "A parking-lot court under buzzing neon. Neon Static's home.",
                "#26262E", "#00F5D4", "#7209B7", "#10002B", "#3A0CA3", 0.55f));
            c.Courts.Add(Court("court.boardwalk_slab", "Boardwalk Slab", CourtCircuit.Blacktop,
                "Sun-bleached concrete between the arcade and the sand.",
                "#5C5552", "#FFF3B0", "#E09F3E", "#FF9F1C", "#FFBF69", 0.50f));

            // Caller League venues (one per team)
            c.Courts.Add(Court("court.volt_box", "The Volt Box", CourtCircuit.League,
                "A buzzing warehouse gym wired with yellow strip lights.",
                "#C89B5C", "#2B2B2E", "#4B1F7A", "#231942", "#5E548E", 0.80f));
            c.Courts.Add(Court("court.breakwater", "Breakwater Court", CourtCircuit.League,
                "A seaside gym with windows that rattle when the crowd gets up.",
                "#D2A56D", "#14213D", "#1FB5A6", "#14213D", "#1FB5A6", 0.75f));
            c.Courts.Add(Court("court.kennel_yard", "Kennel Yard", CourtCircuit.League,
                "Brick walls, low ceiling, and fans who bark on defense.",
                "#B5835A", "#F3E9D2", "#CC5500", "#3E2415", "#CC5500", 0.85f));
            c.Courts.Add(Court("court.grove_gym", "Grove Gym", CourtCircuit.League,
                "Timber rafters and a floor with a hand-painted tree at centre.",
                "#C49A6C", "#111111", "#1E5631", "#0B2E1A", "#D4A017", 0.70f));
            c.Courts.Add(Court("court.orbit_dome", "Orbit Dome", CourtCircuit.League,
                "A round arena under a starfield ceiling.",
                "#CFA878", "#FFFFFF", "#7B2CBF", "#10002B", "#5BC0EB", 0.80f));
            c.Courts.Add(Court("court.mirage_pavilion", "Mirage Pavilion", CourtCircuit.League,
                "Open sides, hot air, and heat shimmer off the baseline.",
                "#D9B98C", "#191970", "#B7410E", "#191970", "#E6C79C", 0.65f));
            c.Courts.Add(Court("court.nightroost_hall", "Nightroost Hall", CourtCircuit.League,
                "A quiet, cold gym where every squeak carries.",
                "#BFA27E", "#0B0B0B", "#A8DADC", "#0B0B0B", "#457B9D", 0.70f));
            c.Courts.Add(Court("court.sunspire", "Sunspire Court", CourtCircuit.League,
                "A glass-roofed arena that turns pink at sunset.",
                "#D4A373", "#0F1A3C", "#D1127A", "#0F1A3C", "#FF8C1A", 0.90f));

            c.Courts.Add(Court(PracticeCourtId, "Practice Lab", CourtCircuit.Practice,
                "An empty rec-centre court with chalk targets on the floor.",
                "#8D99AE", "#EDF2F4", "#2B2D42", "#2B2D42", "#8D99AE", 0f));
        }

        private static CourtDef Court(string id, string name, CourtCircuit circuit, string description,
                                      string floor, string lines, string paint, string skyTop, string skyBottom, float crowd)
        {
            return new CourtDef
            {
                id = id,
                displayName = name,
                circuit = circuit,
                description = description,
                floor = RgbColor.FromHex(floor),
                lines = RgbColor.FromHex(lines),
                paint = RgbColor.FromHex(paint),
                skyTop = RgbColor.FromHex(skyTop),
                skyBottom = RgbColor.FromHex(skyBottom),
                crowdDensity = crowd,
            };
        }

        // ------------------------------------------------------------------ teams

        private static void AddLeagueTeams(ContentCatalog c)
        {
            AddTeam(c, "team.eastbay_voltage", "Eastbay", "Voltage", "EBV", TeamTier.League,
                "#FFD400", "#2B2B2E", "#4B1F7A", LogoShape.Hexagon, LogoMotif.Bolt, TeamPattern.Stripes,
                "court.volt_box", "Stay charged.", true,
                P("castellan", "Rio", "Castellan", 3, Archetype.FloorGeneral, 3),
                P("vance", "Marlo", "Vance", 11, Archetype.DeepShooter, 2),
                P("tremont", "Obi", "Tremont", 32, Archetype.RimRunner, 1),
                P("park", "Sully", "Park", 5, Archetype.HustleGuard, 0));

            AddTeam(c, "team.baycity_breakers", "Bay City", "Breakers", "BCB", TeamTier.League,
                "#1FB5A6", "#FF6F59", "#14213D", LogoShape.Circle, LogoMotif.Wave, TeamPattern.Diagonal,
                "court.breakwater", "Crash the shore.", true,
                P("loren", "Kai", "Loren", 7, Archetype.Playmaker, 3),
                P("brightwater", "Teo", "Brightwater", 24, Archetype.StretchForward, 2),
                P("delacroix", "Ash", "Delacroix", 9, Archetype.LockdownWing, 1),
                P("stoll", "Remy", "Stoll", 15, Archetype.QuickCutter, 0));

            AddTeam(c, "team.harbor_hounds", "Harbor", "Hounds", "HHD", TeamTier.League,
                "#CC5500", "#F3E9D2", "#3E2415", LogoShape.Shield, LogoMotif.Paw, TeamPattern.Checker,
                "court.kennel_yard", "Never let go.", false,
                P("ketteridge", "Bo", "Ketteridge", 44, Archetype.PostAnchor, 4),
                P("sato", "Lenny", "Sato", 2, Archetype.ShotCreator, 2),
                P("halloway", "Cruz", "Halloway", 21, Archetype.GlassCleaner, 1),
                P("varga", "Pip", "Varga", 12, Archetype.TwoWaySpark, 0));

            AddTeam(c, "team.redwood_runners", "Redwood", "Runners", "RWR", TeamTier.League,
                "#1E5631", "#D4A017", "#111111", LogoShape.Badge, LogoMotif.Tree, TeamPattern.Chevrons,
                "court.grove_gym", "Deep roots, quick feet.", false,
                P("adair", "Finn", "Adair", 1, Archetype.QuickCutter, 3),
                P("mercer", "Dash", "Mercer", 10, Archetype.FloorGeneral, 2),
                P("ekwe", "Rowan", "Ekwe", 33, Archetype.RimRunner, 2),
                P("hart", "Quill", "Hart", 14, Archetype.DeepShooter, 0));

            AddTeam(c, "team.metro_comets", "Metro", "Comets", "MCM", TeamTier.League,
                "#5BC0EB", "#FFFFFF", "#7B2CBF", LogoShape.Diamond, LogoMotif.Comet, TeamPattern.Dots,
                "court.orbit_dome", "Light the lane.", false,
                P("lumen", "Zeke", "Lumen", 0, Archetype.ShotCreator, 3),
                P("castano", "Ivo", "Castano", 8, Archetype.Playmaker, 2),
                P("pelletier", "Orin", "Pelletier", 23, Archetype.LockdownWing, 1),
                P("brandt", "Tully", "Brandt", 50, Archetype.GlassCleaner, 0));

            AddTeam(c, "team.desert_drifters", "Desert", "Drifters", "DSD", TeamTier.League,
                "#E6C79C", "#B7410E", "#191970", LogoShape.Badge, LogoMotif.Dune, TeamPattern.Rings,
                "court.mirage_pavilion", "Heat checks welcome.", false,
                P("arrieta", "Sol", "Arrieta", 30, Archetype.DeepShooter, 3),
                P("venn", "Mako", "Venn", 4, Archetype.TwoWaySpark, 2),
                P("dunmore", "Cy", "Dunmore", 41, Archetype.PostAnchor, 1),
                P("fairbanks", "Lio", "Fairbanks", 6, Archetype.HustleGuard, 0));

            AddTeam(c, "team.northline_owls", "Northline", "Owls", "NLO", TeamTier.League,
                "#A8DADC", "#0B0B0B", "#C0C0C0", LogoShape.Shield, LogoMotif.Owl, TeamPattern.Cross,
                "court.nightroost_hall", "See it first.", false,
                P("quade", "Ezra", "Quade", 25, Archetype.StretchForward, 3),
                P("halvorsen", "Juno", "Halvorsen", 13, Archetype.FloorGeneral, 2),
                P("aberdeen", "Wes", "Aberdeen", 22, Archetype.LockdownWing, 1),
                P("roe", "Tamsin", "Roe", 17, Archetype.QuickCutter, 0));

            AddTeam(c, "team.solar_crowns", "Solar", "Crowns", "SOL", TeamTier.League,
                "#D1127A", "#FF8C1A", "#0F1A3C", LogoShape.Circle, LogoMotif.Crown, TeamPattern.Solid,
                "court.sunspire", "Shine last.", false,
                P("solano", "Rafe", "Solano", 3, Archetype.ShotCreator, 4),
                P("maddox", "Onyx", "Maddox", 34, Archetype.RimRunner, 2),
                P("farrow", "Ziggy", "Farrow", 11, Archetype.Playmaker, 1),
                P("olander", "Brick", "Olander", 45, Archetype.GlassCleaner, 0));
        }

        private static void AddCircuitCrews(ContentCatalog c)
        {
            AddTeam(c, "crew.cage_regulars", "", "Cage Regulars", "CGR", TeamTier.Circuit,
                "#E07A5F", "#3D405B", "#F2CC8F", LogoShape.Shield, LogoMotif.Ball, TeamPattern.Stripes,
                "court.sunset_cage", "First come, first served.", false,
                P("bram", "Nico", "Bram", 2, Archetype.FloorGeneral, -6),
                P("juarez", "Hal", "Juarez", 31, Archetype.GlassCleaner, -6),
                P("parrish", "Dee", "Parrish", 13, Archetype.DeepShooter, -5));

            AddTeam(c, "crew.pier_pressure", "", "Pier Pressure", "PRP", TeamTier.Circuit,
                "#2A9D8F", "#E9C46A", "#264653", LogoShape.Circle, LogoMotif.Wave, TeamPattern.Dots,
                "court.pier_nine", "Hold the pier.", false,
                P("quon", "Marty", "Quon", 9, Archetype.ShotCreator, -5),
                P("whitlow", "Gus", "Whitlow", 40, Archetype.PostAnchor, -5),
                P("tenney", "Ray", "Tenney", 18, Archetype.QuickCutter, -4));

            AddTeam(c, "crew.underpass_union", "", "Underpass Union", "UPU", TeamTier.Circuit,
                "#9B5DE5", "#FEE440", "#1B1B3A", LogoShape.Diamond, LogoMotif.Signal, TeamPattern.Chevrons,
                "court.overpass_park", "Loud under the lights.", false,
                P("ansel", "Kip", "Ansel", 6, Archetype.TwoWaySpark, -4),
                P("sturgis", "Mo", "Sturgis", 35, Archetype.RimRunner, -3),
                P("corwin", "Jax", "Corwin", 1, Archetype.Playmaker, -3));

            AddTeam(c, "crew.rooftop_relay", "", "Rooftop Relay", "RFR", TeamTier.Circuit,
                "#3D5A80", "#E0FBFC", "#EE6C4D", LogoShape.Badge, LogoMotif.Comet, TeamPattern.Rings,
                "court.rooftop_ring", "Pass it up.", false,
                P("okafor", "Tobi", "Okafor", 7, Archetype.Playmaker, -3),
                P("lindqvist", "Saul", "Lindqvist", 33, Archetype.StretchForward, -2),
                P("pryce", "Remy", "Pryce", 15, Archetype.LockdownWing, -2));

            AddTeam(c, "crew.boardwalk_bandits", "", "Boardwalk Bandits", "BWB", TeamTier.Circuit,
                "#E09F3E", "#540B0E", "#FFF3B0", LogoShape.Circle, LogoMotif.Dune, TeamPattern.Checker,
                "court.boardwalk_slab", "Take what's open.", false,
                P("castellan", "Vic", "Castellan", 0, Archetype.ShotCreator, -2),
                P("brightwater", "Ike", "Brightwater", 24, Archetype.HustleGuard, -1),
                P("moreau", "Dax", "Moreau", 44, Archetype.RimRunner, -1));

            // The rival crew: shows up once a season in Rise Mode.
            AddTeam(c, RivalCrewId, "", "Neon Static", "NST", TeamTier.Rival,
                "#00F5D4", "#10002B", "#F72585", LogoShape.Diamond, LogoMotif.Bolt, TeamPattern.Diagonal,
                "court.static_lot", "Turn it up.", false,
                P("marlowe", "Vex", "Marlowe", 13, Archetype.ShotCreator, 2),
                P("okonjo", "Ira", "Okonjo", 9, Archetype.LockdownWing, 1),
                P("delacroix", "Bo", "Delacroix", 50, Archetype.RimRunner, 1));

            // The player's crew. Rook is the default avatar; nickname is stored in save data.
            AddTeam(c, PlayerCrewId, "", "First Callers", "FCL", TeamTier.PlayerCrew,
                "#F72585", "#4CC9F0", "#1A1A2E", LogoShape.Hexagon, LogoMotif.Signal, TeamPattern.Diagonal,
                "court.overpass_park", "Call your shot.", true,
                PFull(RookPlayerId, "Rook", "", 1, Archetype.TwoWaySpark, -10),
                P("quarles", "Benny", "Quarles", 12, Archetype.HustleGuard, -8, "crew"),
                P("ashby", "Dre", "Ashby", 21, Archetype.StretchForward, -8, "crew"));
        }

        private struct PlayerSeed
        {
            public string id;
            public string first;
            public string last;
            public int number;
            public Archetype archetype;
            public int offset;
        }

        private static PlayerSeed P(string slug, string first, string last, int number, Archetype a, int offset, string prefix = null)
        {
            return new PlayerSeed { id = prefix == null ? slug : "player." + prefix + "." + slug, first = first, last = last, number = number, archetype = a, offset = offset };
        }

        private static PlayerSeed PFull(string id, string first, string last, int number, Archetype a, int offset)
        {
            return new PlayerSeed { id = id, first = first, last = last, number = number, archetype = a, offset = offset };
        }

        private static void AddTeam(ContentCatalog c, string id, string city, string nickname, string abbr, TeamTier tier,
                                    string primary, string secondary, string accent,
                                    LogoShape shape, LogoMotif motif, TeamPattern pattern,
                                    string courtId, string motto, bool unlocked, params PlayerSeed[] roster)
        {
            var team = new TeamDef
            {
                id = id,
                city = city,
                nickname = nickname,
                abbreviation = abbr,
                tier = tier,
                primary = RgbColor.FromHex(primary),
                secondary = RgbColor.FromHex(secondary),
                accent = RgbColor.FromHex(accent),
                logoShape = shape,
                logoMotif = motif,
                pattern = pattern,
                homeCourtId = courtId,
                motto = motto,
                unlockedByDefault = unlocked,
            };

            string teamSlug = abbr.ToLowerInvariant();
            foreach (var seed in roster)
            {
                string playerId = seed.id.StartsWith("player.", System.StringComparison.Ordinal)
                    ? seed.id
                    : "player." + teamSlug + "." + seed.id;
                var archetypeDef = FindArchetype(c.Archetypes, seed.archetype);
                c.Players.Add(new PlayerDef
                {
                    id = playerId,
                    firstName = seed.first,
                    lastName = seed.last,
                    jerseyNumber = seed.number,
                    archetypeId = archetypeDef.id,
                    attributes = Personalize(archetypeDef.baseline.Offset(seed.offset), playerId),
                    appearance = AppearanceFromSeed(playerId, seed.archetype),
                });
                team.rosterPlayerIds.Add(playerId);
            }
            c.Teams.Add(team);
        }

        private static ArchetypeDef FindArchetype(List<ArchetypeDef> list, Archetype a)
        {
            foreach (var def in list) if (def.archetype == a) return def;
            throw new System.InvalidOperationException("Missing archetype " + a);
        }

        /// <summary>Deterministic ±3 variation per attribute so same-archetype players differ.</summary>
        private static AttributeSet Personalize(AttributeSet baseline, string playerId)
        {
            uint h = StableHash.Of(playerId);
            var result = baseline;
            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                h = StableHash.Next(h);
                int delta = (int)(h % 7) - 3;
                var t = (AttributeType)i;
                result = result.With(t, RatingScale.Clamp(result.Get(t) + delta));
            }
            return result;
        }

        private static AppearanceDef AppearanceFromSeed(string playerId, Archetype a)
        {
            uint h = StableHash.Of(playerId + ".look");
            int skin = (int)(h % 6); h = StableHash.Next(h);
            int hair = (int)(h % 6); h = StableHash.Next(h);
            int hairColor = (int)(h % 5);
            BodyType body;
            int height;
            switch (a)
            {
                case Archetype.PostAnchor:
                case Archetype.GlassCleaner:
                    body = BodyType.Broad; height = 2; break;
                case Archetype.StretchForward:
                case Archetype.RimRunner:
                    body = BodyType.Standard; height = 2; break;
                case Archetype.HustleGuard:
                case Archetype.FloorGeneral:
                case Archetype.QuickCutter:
                    body = BodyType.Slim; height = 0; break;
                default:
                    body = BodyType.Standard; height = 1; break;
            }
            return new AppearanceDef(skin, hair, hairColor, body, height);
        }

        // ------------------------------------------------------------------ rules & difficulty

        private static void AddRules(ContentCatalog c)
        {
            c.Rules.Add(new GameRulesDef { id = DefaultRulesId });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.king",
                targetScore = 11,
                useGameClock = true,
                gameClockSeconds = 90f,
            });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.practice",
                targetScore = 999,
                useGameClock = false,
                shotClockSeconds = 999f,
                checkBallAfterScore = false,
                suddenDeathOnTie = false,
            });
        }

        private static void AddDifficulties(ContentCatalog c)
        {
            c.Difficulties.Add(new DifficultyDef
            {
                id = "difficulty.rookie", displayName = "Rookie", sortOrder = 0,
                reactionTime = 0.55f, decisionQuality = 0.45f, shotQualityThreshold = 0.30f,
                errorRate = 0.22f, movementScale = 0.90f, releaseAccuracy = 0.35f,
            });
            c.Difficulties.Add(new DifficultyDef
            {
                id = DefaultDifficultyId, displayName = "Caller", sortOrder = 1,
                reactionTime = 0.35f, decisionQuality = 0.70f, shotQualityThreshold = 0.40f,
                errorRate = 0.12f, movementScale = 1.0f, releaseAccuracy = 0.55f,
            });
            c.Difficulties.Add(new DifficultyDef
            {
                id = "difficulty.legend", displayName = "Legend", sortOrder = 2,
                reactionTime = 0.20f, decisionQuality = 0.90f, shotQualityThreshold = 0.48f,
                errorRate = 0.05f, movementScale = 1.0f, releaseAccuracy = 0.75f,
            });
        }

        // ------------------------------------------------------------------ progression

        private static void AddUpgrades(ContentCatalog c)
        {
            c.Upgrades.Add(Upgrade("upgrade.finishing", "Finishing Drills", "Touch at the rim through contact.", AttributeType.Finishing));
            c.Upgrades.Add(Upgrade("upgrade.shooting", "Shooting Reps", "Late-night reps from every spot.", AttributeType.Shooting));
            c.Upgrades.Add(Upgrade("upgrade.playmaking", "Film Study", "See the pass before it opens.", AttributeType.Playmaking));
            c.Upgrades.Add(Upgrade("upgrade.defense", "Slide Work", "Stay in front, hands active.", AttributeType.Defense));
            c.Upgrades.Add(Upgrade("upgrade.rebounding", "Box-Out Circuit", "Find a body, then find the ball.", AttributeType.Rebounding));
            c.Upgrades.Add(Upgrade("upgrade.speed", "Sprint Ladders", "A quicker first step.", AttributeType.Speed));
            c.Upgrades.Add(Upgrade("upgrade.stamina", "Hill Runs", "Legs that last to the final possession.", AttributeType.Stamina));
            c.Upgrades.Add(Upgrade("upgrade.clutch", "Pressure Sets", "Practise with the clock running down.", AttributeType.Clutch));
        }

        private static UpgradeDef Upgrade(string id, string name, string description, AttributeType attribute)
        {
            return new UpgradeDef
            {
                id = id, displayName = name, description = description, attribute = attribute,
                amountPerLevel = 2, maxLevel = 5, baseCost = 150, costGrowth = 1.5f, trainingGames = 1, attributeCap = 90,
            };
        }

        private static void AddCosmetics(ContentCatalog c)
        {
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.crew_home", "Crew Home", CosmeticSlot.JerseyPalette, 0, 0, true, "#F72585", "#4CC9F0"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.sunset_fade", "Sunset Fade", CosmeticSlot.JerseyPalette, 200, 50, false, "#FF7E5F", "#FEB47B"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.midnight_neon", "Midnight Neon", CosmeticSlot.JerseyPalette, 300, 150, false, "#0F0C29", "#00F5D4"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.arcade_mint", "Arcade Mint", CosmeticSlot.JerseyPalette, 350, 300, false, "#2EC4B6", "#FFBF69"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.gold_rush", "Gold Rush", CosmeticSlot.JerseyPalette, 500, 600, false, "#FFD166", "#1A1A2E"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.chalk_brick", "Chalk & Brick", CosmeticSlot.JerseyPalette, 300, 250, false, "#EDE6D6", "#A23E48"));

            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.classic", "Classic Whites", CosmeticSlot.Shoes, 0, 0, true, "#FFFFFF", "#D9D9D9"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.volt_laces", "Volt Laces", CosmeticSlot.Shoes, 150, 40, false, "#1A1A1A", "#FFD400"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.glacier", "Glacier Highs", CosmeticSlot.Shoes, 200, 120, false, "#E0FBFC", "#3D5A80"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.cosmic", "Cosmic Runners", CosmeticSlot.Shoes, 350, 350, false, "#7209B7", "#F72585"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.lava_soles", "Lava Soles", CosmeticSlot.Shoes, 250, 200, false, "#2B2B2B", "#FF4D00"));

            c.Cosmetics.Add(Cosmetic("cosmetic.banner.blacktop", "Blacktop Banner", CosmeticSlot.CourtBanner, 0, 0, true, "#2F2F36", "#FFE066"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.signal_flag", "Signal Flag", CosmeticSlot.CourtBanner, 200, 100, false, "#4CC9F0", "#F72585"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.boardwalk", "Boardwalk Pennant", CosmeticSlot.CourtBanner, 250, 180, false, "#2EC4B6", "#FFBF69"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.gold_signal", "Gold Signal", CosmeticSlot.CourtBanner, 600, 400, false, "#D4A017", "#1A1A2E"));

            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.fist_pump", "Fist Pump", CosmeticSlot.Celebration, 0, 0, true, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.call_it", "Call It", CosmeticSlot.Celebration, 250, 120, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.raise_roof", "Raise the Roof", CosmeticSlot.Celebration, 400, 450, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.shimmy_step", "Shimmy Step", CosmeticSlot.Celebration, 350, 300, false, "#FFFFFF", "#FFFFFF"));

            c.Cosmetics.Add(Cosmetic("cosmetic.move.basic_cross", "Basic Crossover", CosmeticSlot.DribbleMove, 0, 0, true, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.hesi_hop", "Hesitation Hop", CosmeticSlot.DribbleMove, 300, 150, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.behind_back", "Behind the Back", CosmeticSlot.DribbleMove, 400, 250, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.spin_cycle", "Spin Cycle", CosmeticSlot.DribbleMove, 450, 350, false, "#FFFFFF", "#FFFFFF"));
        }

        private static CosmeticDef Cosmetic(string id, string name, CosmeticSlot slot, int cost, int fans, bool unlocked, string a, string b)
        {
            return new CosmeticDef
            {
                id = id, displayName = name, slot = slot, cost = cost, fansRequired = fans,
                unlockedByDefault = unlocked, colorA = RgbColor.FromHex(a), colorB = RgbColor.FromHex(b),
            };
        }

        private static void AddSeason(ContentCatalog c)
        {
            var season = new SeasonConfigDef();
            foreach (var t in c.Teams)
                if (t.tier == TeamTier.League) season.teamIds.Add(t.id);
            c.Seasons.Add(season);
        }
    }
}
