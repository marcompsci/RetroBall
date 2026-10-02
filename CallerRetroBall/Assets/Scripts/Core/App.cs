using CallerRetroBall.Data;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Tiny service hub. Initialised by BootScene, but every scene controller calls
    /// <see cref="EnsureInitialized"/> too, so pressing Play on any scene in the
    /// Editor still works.
    /// </summary>
    public static class App
    {
        public const string Version = "0.3.0-phase3";

        public static ContentDatabase Content { get; private set; }

        /// <summary>The match the GameScene should play next (set by menus).</summary>
        public static MatchRequest PendingMatch { get; set; }

        public static bool IsInitialized => Content != null;

        public static ContentCatalog Catalog => Content?.Catalog;

        public static void EnsureInitialized()
        {
            if (IsInitialized) return;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            Content = ContentDatabase.Load();
            if (Content.FallbackKinds.Count > 0)
            {
                Debug.LogWarning("[CallerRetroBall] Using built-in default content for: " +
                                 string.Join(", ", Content.FallbackKinds) +
                                 ". Run 'Caller Retro Ball ▸ Run Project Setup' to generate editable assets.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var report = ContentValidator.Validate(Content.Catalog);
            if (!report.IsValid) Debug.LogError("[CallerRetroBall] " + report);
            else if (report.Warnings.Count > 0) Debug.LogWarning("[CallerRetroBall] " + report);
#endif

            SceneFlow.EnsureExists();
        }

        // Supports "Enter Play Mode" without domain reload: static state is reset each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Content = null;
            PendingMatch = null;
            TextureFactory.ClearCache();
        }
    }
}
