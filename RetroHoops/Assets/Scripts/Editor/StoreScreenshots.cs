using System.IO;
using UnityEditor;
using UnityEngine;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// App Store screenshots straight from Play mode. Set the Game view to 1320×2868 (6.9" iPhone,
    /// portrait), press Play, set up the moment, then Retro Hoops ▸ Release ▸ Capture Store Screenshot.
    /// Saved to StoreScreenshots/ in the project as opaque PNGs (App Store Connect rejects alpha).
    /// </summary>
    public static class StoreScreenshots
    {
        public const string Folder = "StoreScreenshots";

        [MenuItem("Retro Hoops/Release/Capture Store Screenshot %#k", priority = 90)]
        public static void Capture()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Retro Hoops", "Press Play first, then capture while the game is running.", "OK");
                return;
            }
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            int w = shot.width, h = shot.height;
            // Copy into an RGB texture so the PNG has no alpha channel.
            var rgb = new Texture2D(w, h, TextureFormat.RGB24, false);
            rgb.SetPixels32(shot.GetPixels32());
            rgb.Apply(false);
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "retrohoops_" + w + "x" + h + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            File.WriteAllBytes(path, rgb.EncodeToPNG());
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(rgb);

            string note = IsStoreSize(w, h) ? "" : "\n\nNote: " + w + "×" + h + " isn't an App Store size. Set the Game view to 1320×2868 (or 1290×2796 / 1260×2736) for iPhone, 2064×2752 (or 2048×2732) for iPad.";
            Debug.Log("[Retro Hoops] Screenshot saved: " + Path.GetFullPath(path) + note);
            ReleaseTools.WriteReport("SCREENSHOT " + path + note);
        }

        /// <summary>6.9" iPhone portrait sizes App Store Connect accepts.</summary>
        public static bool IsStoreSize(int w, int h) =>
            (w == 1320 && h == 2868) || (w == 1290 && h == 2796) || (w == 1260 && h == 2736)
            // 13" iPad (required now the app runs on iPad).
            || (w == 2064 && h == 2752) || (w == 2048 && h == 2732);
    }
}
