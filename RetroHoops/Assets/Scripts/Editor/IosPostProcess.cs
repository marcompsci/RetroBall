#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// After an iOS build, sets the Info.plist keys App Store Connect would otherwise ask about
    /// (no non-exempt encryption, full screen, hidden status bar, Sports Games category), links
    /// GameKit and MultipeerConnectivity (two-phone play, with the local-network keys), and adds the Game Center and iCloud (key-value storage) capabilities.
    /// </summary>
    public static class IosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetBoolean("UIStatusBarHidden", true);
            root.SetBoolean("UIViewControllerBasedStatusBarAppearance", false);
            // Lets ProMotion iPhones run the game at 120 Hz (Settings ▸ HIGH FRAME RATE).
            root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
            root.SetString("LSApplicationCategoryType", "public.app-category.sports-games");
            // The share sheet's "Save Image" writes highlight GIFs to Photos (add-only access).
            root.SetString("NSPhotoLibraryAddUsageDescription", "Save your Retro Hoops highlights to Photos.");
            // Bluetooth / MFi controllers (Xbox, PlayStation, Switch Pro and similar extended gamepads).
            root.SetBoolean("GCSupportsControllerUserInteraction", true);
            // retrohoops://kit/... (and older retroball://kit/...) links (kit QR codes) open the app straight into the Kit Studio.
            var urlTypes = root["CFBundleURLTypes"] as PlistElementArray ?? root.CreateArray("CFBundleURLTypes");
            bool hasScheme = false;
            foreach (var t in urlTypes.values)
                if (t is PlistElementDict d && d["CFBundleURLSchemes"] is PlistElementArray schemes)
                    foreach (var sch in schemes.values)
                        if (sch is PlistElementString str && str.value == "retrohoops") hasScheme = true;
            if (!hasScheme)
            {
                var type = urlTypes.AddDict();
                type.SetString("CFBundleURLName", "com.phoronomicstudios.retrohoops.kit");
                var schemes = type.CreateArray("CFBundleURLSchemes");
                schemes.AddString("retrohoops");
                schemes.AddString("retroball"); // links shared before the rename
            }
            // Two-phone play (2 PLAYER ▸ TWO PHONES): nearby discovery over Wi-Fi / Bluetooth, no internet.
            root.SetString("NSLocalNetworkUsageDescription", "Find a friend's iPhone or iPad nearby to play Retro Hoops head to head.");
            var bonjour = root["NSBonjourServices"] as PlistElementArray ?? root.CreateArray("NSBonjourServices");
            foreach (var service in new[] { "_retrohoops._tcp", "_retrohoops._udp" })
            {
                bool present = false;
                foreach (var v in bonjour.values) if (v is PlistElementString str && str.value == service) present = true;
                if (!present) bonjour.AddString(service);
            }
            if (root["GCSupportedGameControllers"] == null)
                root.CreateArray("GCSupportedGameControllers").AddDict().SetString("ProfileName", "ExtendedGamepad");
            plist.WriteToFile(plistPath);

            // Game Center: link GameKit and add the capability (sign-in stays opt-in inside the game).
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "GameKit.framework", false);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "MultipeerConnectivity.framework", false);
            // Retro Hoops Live: StoreKit 2 (RetroStore.swift) for the subscription, GameKit matchmaking (RetroLive.mm).
            string fw = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(fw, "StoreKit.framework", false);
            if (string.IsNullOrEmpty(proj.GetBuildPropertyForAnyConfig(fw, "SWIFT_VERSION"))) proj.SetBuildProperty(fw, "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(fw, "CLANG_ENABLE_MODULES", "YES");
            proj.WriteToFile(projPath);
            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/RetroHoops.entitlements", null, proj.GetUnityMainTargetGuid());
            caps.AddGameCenter();
            caps.AddInAppPurchase();
            // iCloud key-value storage for save sync (Settings ▸ ICLOUD SYNC). No documents, no CloudKit.
            caps.AddiCloud(true, false, false, false, new string[0]);
            caps.WriteToFile();
        }
    }
}
#endif
