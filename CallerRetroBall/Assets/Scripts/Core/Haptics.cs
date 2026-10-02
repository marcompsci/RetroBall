#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Light / medium / success haptics behind the Settings toggle. iOS uses the native
    /// UIKit generators (Assets/Plugins/iOS/CallerHaptics.mm); other platforms do nothing.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled => App.Career == null || App.Career.settings.haptics;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CallerHaptics_Impact(int style);
        [DllImport("__Internal")] private static extern void CallerHaptics_Success();
#endif

        /// <summary>Pass thrown.</summary>
        public static void Light() => Impact(0);

        /// <summary>Perfect (green) release.</summary>
        public static void Medium() => Impact(1);

        /// <summary>Dunk, alley-oop, heating up.</summary>
        public static void Heavy() => Impact(2);

        /// <summary>Steal or block.</summary>
        public static void Success()
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            CallerHaptics_Success();
#endif
        }

        private static void Impact(int style)
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            CallerHaptics_Impact(style);
#endif
        }
    }
}
