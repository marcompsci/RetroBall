using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>What the game is running on: iPhone, iPad, or a Mac running the iPad build ("Designed for iPad").</summary>
    public static class DeviceInfo
    {
        private static int _mac = -1;

        /// <summary>
        /// True on a Mac. iOS apps on Apple silicon Macs report the Mac's model (e.g. "MacBookAir10,1",
        /// "Mac14,2") as the hardware model, while iPhones and iPads report "iPhone…" / "iPad…".
        /// </summary>
        public static bool IsMac
        {
            get
            {
                if (_mac < 0)
                {
                    string model = SystemInfo.deviceModel ?? "";
                    _mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor
                           || model.StartsWith("Mac") || model.StartsWith("iMac") ? 1 : 0;
                }
                return _mac == 1;
            }
        }

        /// <summary>Wider than a phone (iPad, Mac window): about 3:4 or wider.</summary>
        public static bool IsWide => Screen.height > 0 && Screen.width / (float)Screen.height > 0.66f;
    }
}
