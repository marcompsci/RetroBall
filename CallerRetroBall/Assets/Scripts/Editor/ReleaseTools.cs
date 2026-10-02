using System;
using System.IO;
using System.Linq;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// Release helpers: writes the procedurally drawn app icon and launch image into the
    /// project and assigns them, applies release Player Settings, and builds the Xcode project
    /// for the iOS Simulator or a device. Every build step can also run from the command line:
    ///   Unity -batchmode -quit -projectPath . -executeMethod CallerRetroBall.EditorTools.ReleaseTools.BuildSimulator
    /// </summary>
    public static class ReleaseTools
    {
        public const string ArtFolder = "Assets/Art/AppIcon";
        public const string IconPath = ArtFolder + "/AppIcon1024.png";
        public const string LaunchPath = ArtFolder + "/LaunchImage.png";
        public const string SimulatorOutput = "iOSBuild/Simulator";
        public const string DeviceOutput = "iOSBuild/Device";
        public const string ReleaseVersion = "1.0.0";

        [MenuItem("RetroBall/Release/Generate App Icon and Launch Image", priority = 60)]
        public static void GenerateIconAndLaunchMenu()
        {
            string report = GenerateIconAndLaunch();
            EditorUtility.DisplayDialog("RetroBall", report, "OK");
        }

        [MenuItem("RetroBall/Release/Apply Release Player Settings", priority = 61)]
        public static void ApplyReleaseSettingsMenu()
        {
            EditorUtility.DisplayDialog("RetroBall", ApplyReleaseSettings(), "OK");
        }

        [MenuItem("RetroBall/Release/Build iOS (Simulator)", priority = 80)]
        public static void BuildSimulator() => Build(iOSSdkVersion.SimulatorSDK, SimulatorOutput);

        [MenuItem("RetroBall/Release/Build iOS (Device)", priority = 81)]
        public static void BuildDevice() => Build(iOSSdkVersion.DeviceSDK, DeviceOutput);

        // ------------------------------------------------------------------ readiness

        [MenuItem("RetroBall/Release/Check iOS Readiness", priority = 59)]
        public static void CheckReadinessMenu()
        {
            var (report, ready) = CheckIosReadiness();
            Debug.Log("[RetroBall] iOS readiness:\n" + report);
            if (!ready && EditorUtility.DisplayDialog("RetroBall: iOS readiness", report + "\n\nFix what can be fixed automatically (icon, launch image, release settings)?", "Fix", "Close"))
            {
                GenerateIconAndLaunch();
                ApplyReleaseSettings();
                CheckReadinessMenu();
            }
            else if (ready)
            {
                EditorUtility.DisplayDialog("RetroBall: iOS readiness", report, "OK");
            }
        }

        /// <summary>Checks everything an iOS build needs that can be checked from the Editor.</summary>
        public static (string report, bool ready) CheckIosReadiness()
        {
            var lines = new System.Collections.Generic.List<string>();
            bool ready = true;
            void Check(bool ok, string good, string bad, bool blocking = true)
            {
                lines.Add((ok ? "✓ " : (blocking ? "✗ " : "! ")) + (ok ? good : bad));
                if (!ok && blocking) ready = false;
            }

            Check(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS),
                  "iOS Build Support module installed",
                  "iOS Build Support is not installed. Unity Hub ▸ Installs ▸ ⚙ ▸ Add modules ▸ iOS Build Support");
            Check(Application.platform == RuntimePlatform.OSXEditor, "Running on a Mac", "iOS builds need a Mac with Xcode");
            Check(Directory.Exists("/Applications/Xcode.app"), "Xcode found in /Applications",
                  "Xcode not found in /Applications. Install it from the Mac App Store", blocking: false);

            var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToList();
            Check(scenes.Count >= 6 && scenes[0].EndsWith("BootScene.unity", StringComparison.Ordinal),
                  "Scenes in Build Settings (" + scenes.Count + ", BootScene first)",
                  "Scenes missing from Build Settings. Run RetroBall ▸ Run Project Setup");

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            bool placeholder = string.IsNullOrEmpty(id) || id == "com.retroball.game" || id.StartsWith("com.Unity", StringComparison.Ordinal);
            Check(!placeholder, "Bundle ID: " + id,
                  "Bundle ID is a placeholder (" + id + "). Set your own in Player Settings ▸ iOS ▸ Bundle Identifier, e.g. com.yourname.retroball",
                  blocking: false);
            Check(!string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID), "Apple Team ID set",
                  "No Apple Team ID yet. Fine for the Simulator; for your iPhone pick your team in Xcode ▸ Signing & Capabilities",
                  blocking: false);
            Check(PlayerSettings.productName == DefaultContent.GameName, "App name: " + PlayerSettings.productName,
                  "App name is \"" + PlayerSettings.productName + "\". Apply Release Player Settings sets it to RetroBall");
            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            Check(icons != null && icons.Length > 0 && icons[0] != null, "App icon assigned",
                  "No app icon. Use Generate App Icon and Launch Image");
            Check(PlayerSettings.iOS.targetOSVersionString == "15.0" || string.CompareOrdinal(PlayerSettings.iOS.targetOSVersionString, "15.0") >= 0,
                  "Minimum iOS " + PlayerSettings.iOS.targetOSVersionString, "Minimum iOS is below 15.0", blocking: false);

            lines.Add("");
            lines.Add(ready
                ? "Ready. Use RetroBall ▸ Release ▸ Build iOS (Simulator), then open iOSBuild/Simulator/Unity-iPhone.xcodeproj in Xcode."
                : "Not ready yet. Fix the ✗ items first (! items are advice).");
            return (string.Join("\n", lines), ready);
        }

        // ------------------------------------------------------------------ icon + launch image

        public static string GenerateIconAndLaunch()
        {
            Directory.CreateDirectory(ArtFolder);
            WritePng(AppIconGenerator.StoreIcon(), IconPath);

            var court = DefaultContent.Create().Court("court.sunset_cage");
            if (court == null) throw new InvalidOperationException("Default content is missing court.sunset_cage.");
            // Same court and seed as the main menu backdrop, scaled 6x to 1080x1920.
            WritePng(AppIconGenerator.Scale(AppIconGenerator.LaunchImage(court, 7), 6), LaunchPath);

            AssetDatabase.Refresh();
            ConfigureImporter(IconPath);
            ConfigureImporter(LaunchPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var launch = AssetDatabase.LoadAssetAtPath<Texture2D>(LaunchPath);

            // The default icon is used for every iOS size; Xcode gets a single 1024 source.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhonePortraitImage);
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x1A, 0x1A, 0x2E, 255);

            AssetDatabase.SaveAssets();
            return "App icon: " + IconPath + " (1024x1024, opaque)\nLaunch image: " + LaunchPath +
                   " (1080x1920)\nBoth assigned in Player Settings. Check them in Player Settings ▸ iOS ▸ Icon and Splash Image.";
        }

        private static void WritePng(PixelCanvas canvas, string path)
        {
            // Readable texture (TextureFactory frees the CPU copy, which EncodeToPNG needs).
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false) { name = Path.GetFileNameWithoutExtension(path) };
            var pixels = new Color32[canvas.Pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = canvas.Pixels[i];
                pixels[i] = new Color32(p.r, p.g, p.b, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            try
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ player settings

        public static string ApplyReleaseSettings()
        {
            PlayerSettings.productName = DefaultContent.GameName;
            PlayerSettings.bundleVersion = ReleaseVersion;
            if (!int.TryParse(PlayerSettings.iOS.buildNumber, out int build) || build < 1) PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // Unity 6 lets every licence turn off the "Made with Unity" splash.
            PlayerSettings.SplashScreen.show = false;
            // Release builds strip the dev-only cheats and checks (they're behind UNITY_EDITOR || DEBUG).
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            return "Version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + "), iOS 15+, portrait, full screen, no splash.\n" +
                   "Bundle ID: " + id + (id == "com.retroball.game" || id.StartsWith("com.Unity", StringComparison.Ordinal)
                       ? "  ← placeholder: change it to your own (e.g. com.yourname.retroball) before signing." : "");
        }

        // ------------------------------------------------------------------ builds

        private static void Build(iOSSdkVersion sdk, string output)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("No scenes in Build Settings. Run 'RetroBall ▸ Run Project Setup' first.");
                return;
            }

            var validation = ContentValidator.Validate(DefaultContent.Create());
            if (!validation.IsValid)
            {
                Fail("Content validation failed:\n" + validation);
                return;
            }

            if (!File.Exists(IconPath) || !File.Exists(LaunchPath)) GenerateIconAndLaunch();
            ApplyReleaseSettings();
            PlayerSettings.iOS.sdkVersion = sdk;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Fail("iOS build " + summary.result + " with " + summary.totalErrors + " error(s). See the Console.");
                return;
            }

            string msg = "Xcode project written to " + Path.GetFullPath(output) + " (" + (summary.totalSize / (1024 * 1024)) + " MB).\n" +
                         "Open Unity-iPhone.xcodeproj, choose your team under Signing & Capabilities, pick " +
                         (sdk == iOSSdkVersion.SimulatorSDK ? "an iPhone simulator" : "your iPhone") + ", and press Run.";
            Debug.Log("[CallerRetroBall] " + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("RetroBall", msg, "OK");
        }

        private static void Fail(string message)
        {
            Debug.LogError("[CallerRetroBall] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("RetroBall", message, "OK");
        }
    }
}
