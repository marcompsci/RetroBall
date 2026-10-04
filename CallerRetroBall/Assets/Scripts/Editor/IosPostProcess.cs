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
    /// GameKit, and adds the Game Center and iCloud (key-value storage) capabilities.
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
            root.SetString("NSPhotoLibraryAddUsageDescription", "Save your RetroBall highlights to Photos.");
            // Bluetooth / MFi controllers (Xbox, PlayStation, Switch Pro and similar extended gamepads).
            root.SetBoolean("GCSupportsControllerUserInteraction", true);
            // retroball://kit/... links (kit QR codes) open the app straight into the Kit Studio.
            var urlTypes = root["CFBundleURLTypes"] as PlistElementArray ?? root.CreateArray("CFBundleURLTypes");
            bool hasScheme = false;
            foreach (var t in urlTypes.values)
                if (t is PlistElementDict d && d["CFBundleURLSchemes"] is PlistElementArray schemes)
                    foreach (var sch in schemes.values)
                        if (sch is PlistElementString str && str.value == "retroball") hasScheme = true;
            if (!hasScheme)
            {
                var type = urlTypes.AddDict();
                type.SetString("CFBundleURLName", "com.marcompsci.retroball.kit");
                type.CreateArray("CFBundleURLSchemes").AddString("retroball");
            }
            if (root["GCSupportedGameControllers"] == null)
                root.CreateArray("GCSupportedGameControllers").AddDict().SetString("ProfileName", "ExtendedGamepad");
            plist.WriteToFile(plistPath);

            // Game Center: link GameKit and add the capability (sign-in stays opt-in inside the game).
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "GameKit.framework", false);
            proj.WriteToFile(projPath);
            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/RetroBall.entitlements", null, proj.GetUnityMainTargetGuid());
            caps.AddGameCenter();
            // iCloud key-value storage for save sync (Settings ▸ ICLOUD SYNC). No documents, no CloudKit.
            caps.AddiCloud(true, false, false, false, new string[0]);
            caps.WriteToFile();
        }
    }
}
#endif
