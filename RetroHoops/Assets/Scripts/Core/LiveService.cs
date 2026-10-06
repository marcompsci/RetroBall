#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CallerRetroBall.Logic;

namespace CallerRetroBall.Core
{
    public enum StoreState { Loading = 0, NotSubscribed = 1, Subscribed = 2, Purchasing = 3, Failed = 4, Pending = 5 }

    /// <summary>
    /// The Retro Hoops Live subscription (Plugins/iOS/RetroStore.swift, StoreKit 2). The App Store is the
    /// only judge of whether it's active; the career keeps the last known end date so the menu can show
    /// the right state straight away. In the Editor a test switch stands in for the App Store.
    /// </summary>
    public static class LiveStore
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroStore_Start(string productId);
        [DllImport("__Internal")] private static extern int RetroStore_State();
        [DllImport("__Internal")] private static extern string RetroStore_Price();
        [DllImport("__Internal")] private static extern string RetroStore_Error();
        [DllImport("__Internal")] private static extern double RetroStore_Expires();
        [DllImport("__Internal")] private static extern void RetroStore_Buy();
        [DllImport("__Internal")] private static extern void RetroStore_Restore();
        [DllImport("__Internal")] private static extern void RetroStore_Refresh();
#else
        /// <summary>Editor only: pretend the subscription is active (to try the Live screens).</summary>
        public static bool EditorSubscribed;
#endif
        private static bool _started;

        public static void EnsureStarted()
        {
            if (_started) return;
            _started = true;
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Start(LiveMode.ProductId);
#endif
        }

        public static StoreState State
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (StoreState)RetroStore_State();
#else
                return EditorSubscribed ? StoreState.Subscribed : StoreState.NotSubscribed;
#endif
            }
        }

        /// <summary>The localised price from the App Store ("$10.99", "£9.99"…), or the fallback while it loads.</summary>
        public static string Price
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                string p = RetroStore_Price();
                return string.IsNullOrEmpty(p) ? LiveMode.PriceFallback : p;
#else
                return LiveMode.PriceFallback;
#endif
            }
        }

        public static string Error
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroStore_Error() ?? "";
#else
                return "";
#endif
            }
        }

        /// <summary>Unix seconds when the current period ends (0 if unknown).</summary>
        public static double Expires
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroStore_Expires();
#else
                return EditorSubscribed ? Now + 30 * 86400 : 0;
#endif
            }
        }

        public static double Now => (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;

        /// <summary>Subscribed right now: the App Store's answer, or the cached end date while it's still loading.</summary>
        public static bool Active
        {
            get
            {
                var s = State;
                if (s == StoreState.Subscribed) return true;
                if (s == StoreState.Loading) return LiveMode.CachedActive(App.Career?.live, Now);
                return false;
            }
        }

        /// <summary>Copies the App Store's answer into the career (for the next launch).</summary>
        public static void Remember()
        {
            var live = App.Career?.live;
            if (live == null) return;
            var s = State;
            double until = s == StoreState.Subscribed ? Expires : (s == StoreState.NotSubscribed ? 0 : live.subscribedUntil);
            if (System.Math.Abs(until - live.subscribedUntil) > 1)
            {
                live.subscribedUntil = until;
                App.SaveCareer();
            }
        }

        public static void Buy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Buy();
#else
            EditorSubscribed = true;
#endif
        }

        public static void Restore()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Restore();
#endif
        }

        public static void Refresh()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Refresh();
#endif
        }
    }

    /// <summary>
    /// A Live match connection (Plugins/iOS/RetroLive.mm): Game Center finds an opponent on the same game
    /// version and relays the messages. Implements the same wire as two-phone play.
    /// </summary>
    public sealed class LiveLink : ILinkTransport
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroLive_Find(int group);
        [DllImport("__Internal")] private static extern void RetroLive_Stop();
        [DllImport("__Internal")] private static extern int RetroLive_State();
        [DllImport("__Internal")] private static extern string RetroLive_Error();
        [DllImport("__Internal")] private static extern string RetroLive_OpponentName();
        [DllImport("__Internal")] private static extern string RetroLive_OpponentId();
        [DllImport("__Internal")] private static extern string RetroLive_LocalId();
        [DllImport("__Internal")] private static extern string RetroLive_LocalName();
        [DllImport("__Internal")] private static extern int RetroLive_Send(byte[] data, int length);
        [DllImport("__Internal")] private static extern int RetroLive_NextLength();
        [DllImport("__Internal")] private static extern int RetroLive_Receive(byte[] buffer, int capacity);
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif
        public static LiveLink Current { get; private set; }

        /// <summary>Asks Game Center for an opponent on this game version.</summary>
        public static LiveLink Find(string appVersion)
        {
            Stop();
            Current = new LiveLink();
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Find(LiveMode.PlayerGroup(appVersion));
#endif
            return Current;
        }

        public static void Stop()
        {
            if (Current == null) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Stop();
#endif
            Current = null;
        }

        public LinkState State
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (LinkState)RetroLive_State();
#else
                return LinkState.Idle;
#endif
            }
        }

        public string PeerName
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_OpponentName() ?? "";
#else
                return "";
#endif
            }
        }

        public string Error
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_Error() ?? "";
#else
                return "Live needs an iPhone, iPad or Mac build.";
#endif
            }
        }

        /// <summary>0 = host (the player whose Game Center id sorts first), 1 = guest.</summary>
        public int Seat
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return LiveMode.Seat(RetroLive_LocalId(), RetroLive_OpponentId());
#else
                return 0;
#endif
            }
        }

        public static string LocalName
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_LocalName() ?? "";
#else
                return "";
#endif
            }
        }

        public void Send(byte[] message)
        {
            if (message == null || message.Length == 0) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Send(message, message.Length);
#endif
        }

        public bool TryReceive(out byte[] message)
        {
            message = null;
#if UNITY_IOS && !UNITY_EDITOR
            int n = RetroLive_NextLength();
            if (n < 0) return false;
            message = new byte[n];
            if (n == 0) { RetroLive_Receive(message, 0); return true; }
            return RetroLive_Receive(message, n) == n;
#else
            return false;
#endif
        }
    }
}
