#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System.Collections.Generic;
using CallerRetroBall.Logic;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// The two-phone connection (Plugins/iOS/RetroLink.mm, Apple's Multipeer Connectivity): one phone
    /// hosts, the other finds it nearby and joins. No server, no internet, no account. Only on
    /// iPhone / iPad / Mac builds; elsewhere <see cref="Supported"/> is false.
    /// </summary>
    public sealed class NearbyLink : ILinkTransport
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroLink_Start(int host, string name);
        [DllImport("__Internal")] private static extern void RetroLink_Stop();
        [DllImport("__Internal")] private static extern int RetroLink_State();
        [DllImport("__Internal")] private static extern int RetroLink_FoundCount();
        [DllImport("__Internal")] private static extern string RetroLink_FoundName(int index);
        [DllImport("__Internal")] private static extern int RetroLink_Join(int index);
        [DllImport("__Internal")] private static extern string RetroLink_PeerName();
        [DllImport("__Internal")] private static extern int RetroLink_Send(byte[] data, int length);
        [DllImport("__Internal")] private static extern int RetroLink_NextLength();
        [DllImport("__Internal")] private static extern int RetroLink_Receive(byte[] buffer, int capacity);
        [DllImport("__Internal")] private static extern int RetroLink_Watch(int index);
        [DllImport("__Internal")] private static extern int RetroLink_WatcherCount();
        [DllImport("__Internal")] private static extern int RetroLink_SendWatchers(byte[] data, int length);
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif

        /// <summary>The link being used (kept across the menu → game scene change).</summary>
        public static NearbyLink Current { get; private set; }

        public readonly bool IsHost;
        private string _peer = "";

        private NearbyLink(bool host) => IsHost = host;

        /// <summary>Starts hosting (other phones can find this one) or searching for a host.</summary>
        public static NearbyLink Start(bool host, string displayName)
        {
            Stop();
            Current = new NearbyLink(host);
#if UNITY_IOS && !UNITY_EDITOR
            RetroLink_Start(host ? 1 : 0, displayName);
#endif
            return Current;
        }

        /// <summary>Hangs up and stops hosting / searching.</summary>
        public static void Stop()
        {
            if (Current == null) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLink_Stop();
#endif
            Current = null;
        }

        public LinkState State
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (LinkState)RetroLink_State();
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
                if (string.IsNullOrEmpty(_peer)) _peer = RetroLink_PeerName() ?? "";
#endif
                return _peer;
            }
        }

        /// <summary>Hosts found nearby (guest only).</summary>
        public List<string> Found()
        {
            var list = new List<string>();
#if UNITY_IOS && !UNITY_EDITOR
            int n = RetroLink_FoundCount();
            for (int i = 0; i < n; i++) list.Add(RetroLink_FoundName(i) ?? "");
#endif
            return list;
        }

        public void Join(int index)
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroLink_Join(index);
#endif
        }

        /// <summary>Joins a nearby host's game to watch it (a third phone).</summary>
        public void Watch(int index)
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroLink_Watch(index);
#endif
        }

        /// <summary>Host: phones watching this game right now.</summary>
        public int WatcherCount
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLink_WatcherCount();
#else
                return 0;
#endif
            }
        }

        /// <summary>Host: sends to every watching phone.</summary>
        public void SendWatchers(byte[] message)
        {
            if (message == null || message.Length == 0) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLink_SendWatchers(message, message.Length);
#endif
        }

        public void Send(byte[] message)
        {
            if (message == null || message.Length == 0) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLink_Send(message, message.Length);
#endif
        }

        public bool TryReceive(out byte[] message)
        {
            message = null;
#if UNITY_IOS && !UNITY_EDITOR
            int n = RetroLink_NextLength();
            if (n < 0) return false;
            message = new byte[n];
            if (n == 0) { RetroLink_Receive(message, 0); return true; }
            return RetroLink_Receive(message, n) == n;
#else
            return false;
#endif
        }
    }

    /// <summary>What the game scene needs to play a two-phone game: the wire, which seat this phone is, and the agreed setup.</summary>
    public static class LinkMatch
    {
        public static ILinkTransport Transport;
        /// <summary>0 = host (team A, player 1), 1 = guest (team B, player 2).</summary>
        public static int Seat;
        public static LinkSetup Setup;
        /// <summary>Live only: both Game Center teamPlayerIDs (for the server's match key).</summary>
        public static string LocalId = "", OpponentId = "";
        public static bool Active => Transport != null && Setup != null && Watching == null;
        /// <summary>A third phone watching the host's game (null when playing).</summary>
        public static Spectator Watching;

        public static void Clear()
        {
            Transport = null;
            Setup = null;
            Watching = null;
        }
    }
}
