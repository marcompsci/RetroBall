using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public sealed class ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void Error(string message) => Errors.Add(message);
        public void Warn(string message) => Warnings.Add(message);

        public override string ToString()
        {
            return "Content validation: " + Errors.Count + " error(s), " + Warnings.Count + " warning(s)"
                   + (Errors.Count > 0 ? "\n  E: " + string.Join("\n  E: ", Errors) : "")
                   + (Warnings.Count > 0 ? "\n  W: " + string.Join("\n  W: ", Warnings) : "");
        }
    }

    /// <summary>
    /// Catches content mistakes before they reach gameplay: duplicate/missing ids,
    /// out-of-range ratings, broken references, unfair difficulty settings, and
    /// team colours too close to tell apart.
    /// </summary>
    public static class ContentValidator
    {
        public const int MinRosterSize = 3;
        public const double MinTeamColorDistance = 60.0;

        public static ValidationReport Validate(ContentCatalog c)
        {
            var r = new ValidationReport();
            if (c == null)
            {
                r.Error("Catalog is null.");
                return r;
            }

            CheckIds("Archetype", c.Archetypes, r);
            CheckIds("Player", c.Players, r);
            CheckIds("Team", c.Teams, r);
            CheckIds("Court", c.Courts, r);
            CheckIds("Rules", c.Rules, r);
            CheckIds("Difficulty", c.Difficulties, r);
            CheckIds("Upgrade", c.Upgrades, r);
            CheckIds("Cosmetic", c.Cosmetics, r);
            CheckIds("Season", c.Seasons, r);

            CheckArchetypes(c, r);
            CheckPlayers(c, r);
            CheckTeams(c, r);
            CheckRules(c, r);
            CheckDifficulties(c, r);
            CheckUpgrades(c, r);
            CheckCosmetics(c, r);
            CheckSeasons(c, r);
            return r;
        }

        private static void CheckIds<T>(string kind, List<T> items, ValidationReport r) where T : class, IHasId
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    r.Error(kind + " at index " + i + " is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    r.Error(kind + " at index " + i + " has an empty id.");
                    continue;
                }
                if (!seen.Add(item.Id)) r.Error("Duplicate " + kind + " id '" + item.Id + "'.");
            }
        }

        private static void CheckArchetypes(ContentCatalog c, ValidationReport r)
        {
            foreach (var a in c.Archetypes)
            {
                if (a == null) continue;
                if (!a.baseline.AllInRange(out var bad))
                    r.Error("Archetype '" + a.id + "' baseline " + bad + " is outside " + RatingScale.Min + "-" + RatingScale.Max + ".");
                if (string.IsNullOrWhiteSpace(a.displayName)) r.Error("Archetype '" + a.id + "' has no display name.");
                if (a.strengths.Count == 0) r.Warn("Archetype '" + a.id + "' lists no strengths.");
                foreach (var s in a.strengths)
                    if (a.weaknesses.Contains(s)) r.Error("Archetype '" + a.id + "' lists " + s + " as both strength and weakness.");
            }
        }

        private static void CheckPlayers(ContentCatalog c, ValidationReport r)
        {
            foreach (var p in c.Players)
            {
                if (p == null) continue;
                if (!p.attributes.AllInRange(out var bad))
                    r.Error("Player '" + p.id + "' " + bad + " rating " + p.attributes.Get(bad) + " is outside " + RatingScale.Min + "-" + RatingScale.Max + ".");
                if (c.ArchetypeById(p.archetypeId) == null)
                    r.Error("Player '" + p.id + "' references missing archetype '" + p.archetypeId + "'.");
                if (string.IsNullOrWhiteSpace(p.firstName))
                    r.Error("Player '" + p.id + "' has no name.");
                if (p.jerseyNumber < 0 || p.jerseyNumber > 99)
                    r.Error("Player '" + p.id + "' jersey number " + p.jerseyNumber + " is outside 0-99.");
            }
        }

        private static void CheckTeams(ContentCatalog c, ValidationReport r)
        {
            var playerOwner = new Dictionary<string, string>();
            var abbreviations = new HashSet<string>();
            var leaguePatterns = new Dictionary<TeamPattern, string>();

            foreach (var t in c.Teams)
            {
                if (t == null) continue;
                if (string.IsNullOrWhiteSpace(t.nickname)) r.Error("Team '" + t.id + "' has no name.");
                if (string.IsNullOrWhiteSpace(t.abbreviation) || t.abbreviation.Length > 4)
                    r.Error("Team '" + t.id + "' abbreviation must be 1-4 characters.");
                else if (!abbreviations.Add(t.abbreviation))
                    r.Error("Duplicate team abbreviation '" + t.abbreviation + "'.");

                if (c.Court(t.homeCourtId) == null)
                    r.Error("Team '" + t.id + "' references missing home court '" + t.homeCourtId + "'.");

                if (t.rosterPlayerIds.Count < MinRosterSize)
                    r.Error("Team '" + t.id + "' has " + t.rosterPlayerIds.Count + " players; needs at least " + MinRosterSize + ".");

                var jerseyNumbers = new HashSet<int>();
                foreach (var pid in t.rosterPlayerIds)
                {
                    var p = c.Player(pid);
                    if (p == null)
                    {
                        r.Error("Team '" + t.id + "' references missing player '" + pid + "'.");
                        continue;
                    }
                    if (playerOwner.TryGetValue(pid, out var other))
                        r.Error("Player '" + pid + "' is on both '" + other + "' and '" + t.id + "'.");
                    else
                        playerOwner[pid] = t.id;
                    if (!jerseyNumbers.Add(p.jerseyNumber))
                        r.Error("Team '" + t.id + "' has duplicate jersey number " + p.jerseyNumber + ".");
                }

                if (RgbColor.Distance(t.primary, t.secondary) < MinTeamColorDistance)
                    r.Warn("Team '" + t.id + "' primary and secondary colours are very similar.");

                if (t.tier == TeamTier.League)
                {
                    if (leaguePatterns.TryGetValue(t.pattern, out var owner))
                        r.Error("League teams '" + owner + "' and '" + t.id + "' share colourblind pattern " + t.pattern + ".");
                    else
                        leaguePatterns[t.pattern] = t.id;
                }
            }

            // Colourblind / readability: league primaries should not be near-identical.
            var league = c.TeamsInTier(TeamTier.League);
            for (int i = 0; i < league.Count; i++)
                for (int j = i + 1; j < league.Count; j++)
                    if (RgbColor.Distance(league[i].primary, league[j].primary) < MinTeamColorDistance)
                        r.Warn("League teams '" + league[i].id + "' and '" + league[j].id + "' have similar primary colours.");

            int unlocked = 0;
            foreach (var t in league) if (t.unlockedByDefault) unlocked++;
            if (unlocked < 2) r.Error("Quick Call needs at least 2 league teams unlocked by default; found " + unlocked + ".");
        }

        private static void CheckRules(ContentCatalog c, ValidationReport r)
        {
            if (c.Rules.Count == 0) r.Error("No game rules defined.");
            foreach (var rules in c.Rules)
            {
                if (rules == null) continue;
                if (rules.targetScore <= 0) r.Error("Rules '" + rules.id + "' target score must be positive.");
                if (rules.insideArcPoints <= 0) r.Error("Rules '" + rules.id + "' inside-arc points must be positive.");
                if (rules.beyondArcPoints <= rules.insideArcPoints)
                    r.Error("Rules '" + rules.id + "' beyond-arc shots must be worth more than inside shots.");
                if (rules.useGameClock && rules.gameClockSeconds <= 0f)
                    r.Error("Rules '" + rules.id + "' game clock must be positive when enabled.");
                if (rules.shotClockSeconds <= 0f) r.Error("Rules '" + rules.id + "' shot clock must be positive.");
            }
        }

        private static void CheckDifficulties(ContentCatalog c, ValidationReport r)
        {
            if (c.Difficulties.Count == 0) r.Error("No difficulty levels defined.");
            foreach (var d in c.Difficulties)
            {
                if (d == null) continue;
                // Fairness rule: difficulty changes decisions and reactions, never raw ability.
                if (d.movementScale > 1f) r.Error("Difficulty '" + d.id + "' movementScale > 1.0 would give the AI hidden speed.");
                if (d.movementScale <= 0f) r.Error("Difficulty '" + d.id + "' movementScale must be positive.");
                if (d.reactionTime < 0.1f) r.Error("Difficulty '" + d.id + "' reactionTime under 0.1s feels like cheating.");
                if (!In01(d.decisionQuality) || !In01(d.errorRate) || !In01(d.shotQualityThreshold) || !In01(d.releaseAccuracy))
                    r.Error("Difficulty '" + d.id + "' has a 0..1 value out of range.");
            }
        }

        private static void CheckUpgrades(ContentCatalog c, ValidationReport r)
        {
            foreach (var u in c.Upgrades)
            {
                if (u == null) continue;
                if (u.maxLevel <= 0) r.Error("Upgrade '" + u.id + "' maxLevel must be positive.");
                if (u.amountPerLevel <= 0) r.Error("Upgrade '" + u.id + "' amountPerLevel must be positive.");
                if (u.baseCost <= 0) r.Error("Upgrade '" + u.id + "' baseCost must be positive.");
                if (u.costGrowth < 1f) r.Error("Upgrade '" + u.id + "' costGrowth must be >= 1.");
                if (!RatingScale.IsValid(u.attributeCap)) r.Error("Upgrade '" + u.id + "' attributeCap outside rating scale.");
                if (u.trainingGames < 0) r.Error("Upgrade '" + u.id + "' trainingGames cannot be negative.");
            }
        }

        private static void CheckCosmetics(ContentCatalog c, ValidationReport r)
        {
            var defaultsPerSlot = new HashSet<CosmeticSlot>();
            foreach (var cos in c.Cosmetics)
            {
                if (cos == null) continue;
                if (cos.cost < 0) r.Error("Cosmetic '" + cos.id + "' cost cannot be negative.");
                if (cos.fansRequired < 0) r.Error("Cosmetic '" + cos.id + "' fansRequired cannot be negative.");
                if (cos.unlockedByDefault) defaultsPerSlot.Add(cos.slot);
            }
            if (c.Cosmetics.Count > 0)
                foreach (CosmeticSlot slot in System.Enum.GetValues(typeof(CosmeticSlot)))
                    if (!defaultsPerSlot.Contains(slot))
                        r.Warn("Cosmetic slot " + slot + " has no default unlocked item.");
        }

        private static void CheckSeasons(ContentCatalog c, ValidationReport r)
        {
            foreach (var s in c.Seasons)
            {
                if (s == null) continue;
                if (s.regularSeasonGames <= 0) r.Error("Season '" + s.id + "' needs at least one game.");
                if (s.playoffTeams != 2 && s.playoffTeams != 4) r.Error("Season '" + s.id + "' playoffTeams must be 2 or 4.");
                if (s.teamIds.Count < s.playoffTeams) r.Error("Season '" + s.id + "' has fewer teams than playoff spots.");
                var seen = new HashSet<string>();
                foreach (var id in s.teamIds)
                {
                    if (c.Team(id) == null) r.Error("Season '" + s.id + "' references missing team '" + id + "'.");
                    if (!seen.Add(id)) r.Error("Season '" + s.id + "' lists team '" + id + "' twice.");
                }
            }
        }

        private static bool In01(float v) => v >= 0f && v <= 1f;
    }
}
