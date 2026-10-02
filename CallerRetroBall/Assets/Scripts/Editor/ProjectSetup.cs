using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallerRetroBall.Core;
using CallerRetroBall.Data;
using CallerRetroBall.Gameplay;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// One-click (and first-open automatic) project setup. Creates the six scenes,
    /// registers them in Build Settings, applies iOS portrait Player Settings,
    /// imports TextMeshPro essentials, and writes default content to editable
    /// ScriptableObject assets. Safe to run repeatedly: existing scenes and assets
    /// are kept unless you choose an explicit overwrite command.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenesFolder = "Assets/Scenes";
        private const string AutoRunKey = "CallerRetroBall.ProjectSetup.AutoRanThisSession";
        private const string PlaceholderBundleId = "com.callerretroball.game";

        static ProjectSetup()
        {
            // First open of a fresh clone: build scenes automatically once the editor settles.
            if (File.Exists(ScenePath(SceneNames.Boot))) return;
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                Debug.Log("[CallerRetroBall] First open detected — running project setup.");
                Run(interactive: false);
            };
        }

        public static string ScenePath(string sceneName) => ScenesFolder + "/" + sceneName + ".unity";

        [MenuItem("Caller Retro Ball/Run Project Setup", priority = 0)]
        public static void RunFromMenu() => Run(interactive: true);

        [MenuItem("Caller Retro Ball/Rebuild Scenes (overwrite)", priority = 20)]
        public static void RebuildScenes()
        {
            if (!EditorUtility.DisplayDialog("Rebuild scenes?",
                    "This overwrites all six scenes in Assets/Scenes with fresh generated versions.", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScenes(overwrite: true);
            ConfigureBuildSettings();
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));
        }

        [MenuItem("Caller Retro Ball/Regenerate Content Assets (overwrite)", priority = 21)]
        public static void RegenerateContent()
        {
            if (!EditorUtility.DisplayDialog("Regenerate content?",
                    "This overwrites every asset in Assets/Resources/Data with the built-in defaults. Your Inspector tuning will be lost.",
                    "Overwrite", "Cancel"))
                return;
            int written = GenerateContentAssets(overwrite: true);
            Debug.Log("[CallerRetroBall] Regenerated " + written + " content assets.");
        }

        [MenuItem("Caller Retro Ball/Validate Content", priority = 40)]
        public static void ValidateContentMenu()
        {
            var report = ContentValidator.Validate(ContentDatabase.Load().Catalog);
            if (report.IsValid) Debug.Log("[CallerRetroBall] " + report);
            else Debug.LogError("[CallerRetroBall] " + report);
        }

        public static void Run(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new List<string>();
            EnsureFolders();
            log.Add(ImportTextMeshProEssentials());
            log.Add(ConfigurePlayerSettings());
            log.Add("Content assets created: " + GenerateContentAssets(overwrite: false));
            log.Add("Scenes created: " + CreateScenes(overwrite: false));
            ConfigureBuildSettings();
            log.Add("Build Settings: " + string.Join(", ", SceneNames.All));
            log.Add(CheckRenderPipeline());

            var report = ContentValidator.Validate(ContentDatabase.Load().Catalog);
            log.Add(report.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));

            Debug.Log("[CallerRetroBall] Project setup finished:\n  • " + string.Join("\n  • ", log));
            if (interactive)
                EditorUtility.DisplayDialog("Caller Retro Ball", "Project setup finished. See the Console for details.\n\nPress Play in BootScene to start.", "OK");
        }

        // ------------------------------------------------------------------ folders

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Art/Generated", "Assets/Art/Placeholder", "Assets/Audio/Placeholder", "Assets/Fonts", "Assets/Materials",
                "Assets/Prefabs/Basketball", "Assets/Prefabs/Characters", "Assets/Prefabs/Courts", "Assets/Prefabs/UI",
                "Assets/Resources/Data", ScenesFolder, "Assets/Settings",
            };
            foreach (var f in folders) EnsureFolder(f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ TextMeshPro

        private static string ImportTextMeshProEssentials()
        {
            if (Resources.Load("TMP Settings") != null) return "TextMeshPro essentials: already present";

            // TMP_PackageResourceImporter lives in the TMP editor assembly; its location has
            // moved between versions, so find it by name rather than hard-referencing it.
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter", false))
                .FirstOrDefault(t => t != null);
            var method = type?.GetMethod("ImportResources", new[] { typeof(bool), typeof(bool), typeof(bool) });
            if (method == null)
            {
                Debug.LogWarning("[CallerRetroBall] Could not auto-import TMP Essentials. Use Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
                return "TextMeshPro essentials: MANUAL STEP REQUIRED (Window ▸ TextMeshPro ▸ Import TMP Essential Resources)";
            }
            method.Invoke(null, new object[] { true, false, false });
            return "TextMeshPro essentials: import started";
        }

        // ------------------------------------------------------------------ player settings

        private static string ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Caller Retro Ball";
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "Caller Retro Ball Dev";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "1.0")
                PlayerSettings.bundleVersion = "0.1.0";

            // Only replace Unity's default identifier; never clobber one the developer set.
            var ios = UnityEditor.Build.NamedBuildTarget.iOS;
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            string currentId = PlayerSettings.GetApplicationIdentifier(ios);
            if (string.IsNullOrEmpty(currentId) || currentId.StartsWith("com.DefaultCompany", StringComparison.Ordinal) ||
                currentId.StartsWith("com.Company", StringComparison.Ordinal))
            {
                PlayerSettings.SetApplicationIdentifier(ios, PlaceholderBundleId);
                PlayerSettings.SetApplicationIdentifier(android, PlaceholderBundleId);
            }

            // Portrait-first.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.statusBarHidden = true;

            // iOS
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = "1";

            return "Player Settings: portrait, iOS 15+, id " + PlayerSettings.GetApplicationIdentifier(ios) +
                   (PlayerSettings.GetApplicationIdentifier(ios) == PlaceholderBundleId ? " (PLACEHOLDER — change before App Store submission)" : "");
        }

        // ------------------------------------------------------------------ content assets

        public static int GenerateContentAssets(bool overwrite)
        {
            var c = DefaultContent.Create();
            int n = 0;
            n += WriteKind<ArchetypeData, ArchetypeDef>(ContentPaths.Archetypes, c.Archetypes, overwrite);
            n += WriteKind<PlayerData, PlayerDef>(ContentPaths.Players, c.Players, overwrite);
            n += WriteKind<TeamData, TeamDef>(ContentPaths.Teams, c.Teams, overwrite);
            n += WriteKind<CourtData, CourtDef>(ContentPaths.Courts, c.Courts, overwrite);
            n += WriteKind<GameRulesData, GameRulesDef>(ContentPaths.Rules, c.Rules, overwrite);
            n += WriteKind<DifficultyData, DifficultyDef>(ContentPaths.Difficulties, c.Difficulties, overwrite);
            n += WriteKind<UpgradeData, UpgradeDef>(ContentPaths.Upgrades, c.Upgrades, overwrite);
            n += WriteKind<CosmeticData, CosmeticDef>(ContentPaths.Cosmetics, c.Cosmetics, overwrite);
            n += WriteKind<SeasonConfigData, SeasonConfigDef>(ContentPaths.Seasons, c.Seasons, overwrite);
            AssetDatabase.SaveAssets();
            return n;
        }

        private static int WriteKind<TAsset, TDef>(string resourcesPath, List<TDef> defs, bool overwrite)
            where TAsset : DefinitionAsset<TDef>
            where TDef : class, IHasId
        {
            string folder = "Assets/Resources/" + resourcesPath;
            EnsureFolder(folder);
            int written = 0;
            foreach (var def in defs)
            {
                string path = folder + "/" + def.Id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (existing != null)
                {
                    if (!overwrite) continue;
                    existing.SetDefinition(def);
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    var asset = ScriptableObject.CreateInstance<TAsset>();
                    asset.SetDefinition(def);
                    AssetDatabase.CreateAsset(asset, path);
                }
                written++;
            }
            return written;
        }

        // ------------------------------------------------------------------ scenes

        private static readonly Dictionary<string, Type> SceneControllers = new Dictionary<string, Type>
        {
            { SceneNames.Boot, typeof(BootController) },
            { SceneNames.MainMenu, typeof(MainMenuController) },
            { SceneNames.Game, typeof(GameSceneController) },
            { SceneNames.Season, typeof(SeasonScreenController) },
            { SceneNames.LockerRoom, typeof(LockerRoomController) },
            { SceneNames.Settings, typeof(SettingsController) },
        };

        public static int CreateScenes(bool overwrite)
        {
            EnsureFolder(ScenesFolder);
            int created = 0;
            foreach (var sceneName in SceneNames.All)
            {
                string path = ScenePath(sceneName);
                if (File.Exists(path) && !overwrite) continue;

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = cameraGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x1A, 0x1A, 0x2E, 255);
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cameraGo.AddComponent<AudioListener>();

                var root = new GameObject(sceneName.Replace("Scene", "") + "Root");
                root.AddComponent(SceneControllers[sceneName]);

                EditorSceneManager.SaveScene(scene, path);
                created++;
            }
            return created;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = SceneNames.All
                .Select(n => new EditorBuildSettingsScene(ScenePath(n), true))
                .ToArray();
        }

        // ------------------------------------------------------------------ render pipeline

        private static string CheckRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
                return "Render pipeline: " + GraphicsSettings.defaultRenderPipeline.name;

            Debug.LogWarning("[CallerRetroBall] No render pipeline asset assigned. The game runs, but to use URP 2D: " +
                             "Assets ▸ Create ▸ Rendering ▸ URP Asset (with 2D Renderer), save it in Assets/Settings, " +
                             "then assign it in Project Settings ▸ Graphics and Project Settings ▸ Quality.");
            return "Render pipeline: NOT ASSIGNED (manual 2-step URP 2D setup, see README)";
        }
    }
}
