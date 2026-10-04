#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Opens the iOS share sheet for a saved file (Save Image puts a GIF in Photos; Messages,
    /// AirDrop and social apps work too). In the Editor it opens the file instead.
    /// Native side: Assets/Plugins/iOS/RetroShare.mm (original code, UIKit only).
    /// </summary>
    public static class Share
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroShare_ShareFile(string path, string text);
#endif

        public static void File(string path, string text)
        {
            if (string.IsNullOrEmpty(path)) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroShare_ShareFile(path, text);
#else
            Application.OpenURL("file://" + path);
#endif
        }
    }
}
