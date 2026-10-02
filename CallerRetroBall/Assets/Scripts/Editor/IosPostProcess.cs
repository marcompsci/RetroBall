#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// After an iOS build, sets the Info.plist keys App Store Connect would otherwise ask about:
    /// no non-exempt encryption (the game uses none), full screen, hidden status bar, and the
    /// Sports Games category.
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
            root.SetString("LSApplicationCategoryType", "public.app-category.sports-games");
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
