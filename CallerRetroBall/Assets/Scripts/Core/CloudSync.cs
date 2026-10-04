#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// iCloud save sync (Settings ▸ ICLOUD SYNC, on by default). The career goes to the player's own
    /// iCloud key-value storage a few seconds after each save, unless iCloud already holds a career
    /// that's further along (then the main menu asks which one to keep, so nothing is overwritten
    /// silently). A fresh install picks up the iCloud career at launch. Editor and other platforms: no-op.
    /// </summary>
    public sealed class CloudSync : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroCloud_Start();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool RetroCloud_IsSignedIn();
        [DllImport("__Internal")] private static extern string RetroCloud_Get(string key);
        [DllImport("__Internal")] private static extern void RetroCloud_Set(string key, string value);
        [DllImport("__Internal")] private static extern void RetroCloud_Remove(string key);
        [DllImport("__Internal")] private static extern void RetroCloud_Synchronize();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool RetroCloud_TakeChanged();
        public static bool Supported => true;
        private static bool SignedIn() => RetroCloud_IsSignedIn();
        private static string Get() => RetroCloud_Get(CloudSave.Key);
        private static void Set(string v) => RetroCloud_Set(CloudSave.Key, v);
        private static void Remove() => RetroCloud_Remove(CloudSave.Key);
        private static void Start0() => RetroCloud_Start();
        private static void Sync0() => RetroCloud_Synchronize();
        private static bool TakeChanged() => RetroCloud_TakeChanged();
#else
        public static bool Supported => false;
        private static bool SignedIn() => false;
        private static string Get() => null;
        private static void Set(string v) { }
        private static void Remove() { }
        private static void Start0() { }
        private static void Sync0() { }
        private static bool TakeChanged() => false;
#endif
        private const float PushDelay = 4f;

        private static CloudSync _instance;
        private float _pushAt = -1f;

        /// <summary>iCloud holds a career that's further along than this device's (the menu offers to load it).</summary>
        public static bool CloudAhead { get; private set; }
        /// <summary>Which device saved the iCloud copy, when it's ahead (for the dialog).</summary>
        public static string CloudDevice { get; private set; }

        private static bool Enabled => Supported && App.Career != null && App.Career.settings.icloudSync;

        /// <summary>iCloud is available on this device (signed in to an Apple Account with iCloud).</summary>
        public static bool Available => Supported && SignedIn();

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[CloudSync]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CloudSync>();
            Start0();
        }

        /// <summary>
        /// At launch: a fresh career adopts the iCloud one straight away (returns it); otherwise, if
        /// iCloud is ahead, <see cref="CloudAhead"/> is set and the local career is kept for now.
        /// </summary>
        public static CareerSaveData AtBoot(CareerSaveData local, ContentCatalog catalog, out bool adopted)
        {
            adopted = false;
            if (!Supported || local == null || !local.settings.icloudSync || !SignedIn()) return local;
            if (!CloudSave.TryUnwrap(Get(), catalog, out var cloud, out _, out var device)) return local;
            if (CloudSave.Choose(local, cloud) != CloudChoice.UseCloud) return local;
            if (local.totals.games == 0 && !local.tutorialDone)
            {
                adopted = true;
                return CloudSave.Adopt(cloud, local);
            }
            CloudAhead = true;
            CloudDevice = device;
            return local;
        }

        /// <summary>Schedules an upload (called after every save).</summary>
        public static void NotifySaved()
        {
            if (_instance != null && Enabled) _instance._pushAt = Time.unscaledTime + PushDelay;
        }

        /// <summary>The player chose to load the iCloud career.</summary>
        public static bool LoadCloud()
        {
            CloudAhead = false;
            if (!Supported || !CloudSave.TryUnwrap(Get(), App.Catalog, out var cloud, out _, out _)) return false;
            App.ReplaceCareer(CloudSave.Adopt(cloud, App.Career));
            return true;
        }

        /// <summary>The player chose to keep this device's career: it replaces the iCloud copy.</summary>
        public static void KeepLocal()
        {
            CloudAhead = false;
            if (Enabled && SignedIn()) Set(CloudSave.Wrap(App.Career, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(), SystemInfo.deviceModel));
        }

        /// <summary>Settings ▸ Reset: the iCloud copy goes too (otherwise it would come back).</summary>
        public static void Erase()
        {
            CloudAhead = false;
            if (Supported) Remove();
        }

        private void Update()
        {
            if (TakeChanged()) Check();
            if (_pushAt < 0f || Time.unscaledTime < _pushAt) return;
            _pushAt = -1f;
            Push();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!Enabled) return;
            if (paused)
            {
                if (_pushAt >= 0f)
                {
                    _pushAt = -1f;
                    Push();
                }
            }
            else
            {
                Sync0();
                Check();
            }
        }

        /// <summary>Notices when another device saved a career that's further along.</summary>
        private static void Check()
        {
            if (!Enabled || !SignedIn()) return;
            if (CloudSave.TryUnwrap(Get(), App.Catalog, out var cloud, out _, out var device)
                && CloudSave.Choose(App.Career, cloud) == CloudChoice.UseCloud)
            {
                CloudAhead = true;
                CloudDevice = device;
            }
        }

        private static void Push()
        {
            if (!Enabled || !SignedIn()) return;
            // Never overwrite a career that's further along: let the player decide on the menu.
            if (CloudSave.TryUnwrap(Get(), App.Catalog, out var cloud, out _, out var device)
                && CloudSave.Choose(App.Career, cloud) == CloudChoice.UseCloud)
            {
                CloudAhead = true;
                CloudDevice = device;
                return;
            }
            string value = CloudSave.Wrap(App.Career, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(), SystemInfo.deviceModel);
            if (value != null) Set(value);
            else Debug.LogWarning("[Retro Hoops] Career is too large for iCloud key-value storage; not synced.");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
