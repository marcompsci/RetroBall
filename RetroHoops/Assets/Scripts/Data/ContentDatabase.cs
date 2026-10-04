using System.Collections.Generic;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Data
{
    /// <summary>Resources sub-folders for each content kind (under Assets/Resources/).</summary>
    public static class ContentPaths
    {
        public const string Root = "Data";
        public const string Archetypes = Root + "/Archetypes";
        public const string Players = Root + "/Players";
        public const string Teams = Root + "/Teams";
        public const string Courts = Root + "/Courts";
        public const string Rules = Root + "/Rules";
        public const string Difficulties = Root + "/Difficulties";
        public const string Upgrades = Root + "/Upgrades";
        public const string Cosmetics = Root + "/Cosmetics";
        public const string Seasons = Root + "/Seasons";
    }

    /// <summary>
    /// Loads all content ScriptableObjects from Resources into a <see cref="ContentCatalog"/>.
    /// Any kind with no assets falls back to <see cref="DefaultContent"/>, so a fresh clone
    /// (before the editor setup has generated assets) is still fully playable. Built-in items
    /// missing from the assets are merged in, so content added by updates always shows up.
    /// Loaded once at boot; never queried with Find* calls during gameplay.
    /// </summary>
    public sealed class ContentDatabase
    {
        public ContentCatalog Catalog { get; }

        /// <summary>Content kinds that had no assets and used built-in defaults.</summary>
        public IReadOnlyList<string> FallbackKinds => _fallbackKinds;

        private readonly List<string> _fallbackKinds;

        private ContentDatabase(ContentCatalog catalog, List<string> fallbackKinds)
        {
            Catalog = catalog;
            _fallbackKinds = fallbackKinds;
        }

        public static ContentDatabase Load()
        {
            var defaults = DefaultContent.Create();
            var catalog = new ContentCatalog();
            var fallback = new List<string>();

            catalog.Archetypes = LoadKind<ArchetypeData, ArchetypeDef>(ContentPaths.Archetypes, defaults.Archetypes, "Archetypes", fallback);
            catalog.Players = LoadKind<PlayerData, PlayerDef>(ContentPaths.Players, defaults.Players, "Players", fallback);
            catalog.Teams = LoadKind<TeamData, TeamDef>(ContentPaths.Teams, defaults.Teams, "Teams", fallback);
            catalog.Courts = LoadKind<CourtData, CourtDef>(ContentPaths.Courts, defaults.Courts, "Courts", fallback);
            catalog.Rules = LoadKind<GameRulesData, GameRulesDef>(ContentPaths.Rules, defaults.Rules, "Rules", fallback);
            catalog.Difficulties = LoadKind<DifficultyData, DifficultyDef>(ContentPaths.Difficulties, defaults.Difficulties, "Difficulties", fallback);
            catalog.Upgrades = LoadKind<UpgradeData, UpgradeDef>(ContentPaths.Upgrades, defaults.Upgrades, "Upgrades", fallback);
            catalog.Cosmetics = LoadKind<CosmeticData, CosmeticDef>(ContentPaths.Cosmetics, defaults.Cosmetics, "Cosmetics", fallback);
            catalog.Seasons = LoadKind<SeasonConfigData, SeasonConfigDef>(ContentPaths.Seasons, defaults.Seasons, "Seasons", fallback);

            catalog.Difficulties.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            return new ContentDatabase(catalog, fallback);
        }

        /// <summary>Wraps an in-memory catalog (tests, tools).</summary>
        public static ContentDatabase FromCatalog(ContentCatalog catalog)
        {
            return new ContentDatabase(catalog, new List<string>());
        }

        private static List<TDef> LoadKind<TAsset, TDef>(string path, List<TDef> defaults, string label, List<string> fallback)
            where TAsset : DefinitionAsset<TDef>
            where TDef : class, IHasId
        {
            var assets = Resources.LoadAll<TAsset>(path);
            var result = new List<TDef>(assets.Length);
            foreach (var asset in assets)
            {
                if (asset != null && asset.Definition != null) result.Add(asset.Definition);
            }

            if (result.Count == 0)
            {
                fallback.Add(label);
                return defaults;
            }

            // Built-in items added in later updates (new crews, courts, cosmetics) are merged in when
            // the project's assets predate them, so they appear without regenerating assets.
            // Assets with the same id always win, so edited content is never overwritten.
            var ids = new HashSet<string>();
            foreach (var d in result) ids.Add(d.Id);
            foreach (var d in defaults)
                if (!ids.Contains(d.Id)) result.Add(d);

            // Resources.LoadAll order is not guaranteed; sort for determinism.
            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }
    }
}
