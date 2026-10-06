#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// The per-install key that seals the save file (Phase 31). On iPhone / iPad it is 32 random bytes in the
    /// Keychain (Plugins/iOS/RetroKeychain.mm), readable only by Retro Hoops after the first unlock and never
    /// synced. Elsewhere (the Editor, other platforms) it is a hidden file next to the save.
    /// </summary>
    public static class SaveKey
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string RetroKeychain_SaveKey(out int created);
#endif
        private static byte[] _key;
        private static bool _created;

        private static bool _loaded;

        /// <summary>
        /// The key, or null when no key store can be used this launch (the save is then written unsealed and
        /// sealed again later). <paramref name="created"/> is true if it didn't exist before this launch.
        /// </summary>
        public static byte[] Get(out bool created)
        {
            if (!_loaded) Load();
            created = _created || _key == null;
            return _key;
        }

        private static void Load()
        {
            _loaded = true;
            string hex = null;
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                hex = RetroKeychain_SaveKey(out int made);
                _created = made != 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveKey] Keychain unavailable: " + e.Message);
            }
#else
            string path = Path.Combine(Application.persistentDataPath, ".savekey");
            try
            {
                if (File.Exists(path)) hex = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(hex) || hex.Length != 64)
                {
                    hex = NewHex();
                    Directory.CreateDirectory(Application.persistentDataPath);
                    File.WriteAllText(path, hex);
                    _created = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveKey] Couldn't keep the key file: " + e.Message);
            }
#endif
            _key = FromHex(hex);
#if UNITY_IOS && !UNITY_EDITOR
            // Keychain locked or failing: try again next time something is saved.
            if (_key == null) _loaded = false;
#endif
        }

        private static string NewHex()
        {
            var b = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            var sb = new System.Text.StringBuilder(64);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        private static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 64) return null;
            var b = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out b[i])) return null;
            }
            return b;
        }
    }
}
