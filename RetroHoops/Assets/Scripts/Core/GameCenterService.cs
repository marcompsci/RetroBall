using System.Collections.Generic;
using CallerRetroBall.Logic;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Integration boundary for Apple Game Center (leaderboards and achievements). The game is
    /// fully playable offline without it; sign-in is opt-in from Settings. On iOS builds the
    /// <see cref="GameKitGameCenterService"/> talks to Assets/Plugins/iOS/CallerGameCenter.mm.
    /// Ids must also exist in App Store Connect (docs/GAME_CENTER.md).
    /// </summary>
    public interface IGameCenterService
    {
        bool IsAvailable { get; }
        bool IsSignedIn { get; }
        void Authenticate();
        void ReportScore(string leaderboardId, long score);
        void UnlockAchievement(string achievementId);
        void ShowDashboard();
        /// <summary>Phase 36: one leaderboard, friends only or everyone.</summary>
        void ShowLeaderboard(string leaderboardId, bool friendsOnly);
    }

    /// <summary>No-op implementation for the Editor and non-iOS platforms.</summary>
    public sealed class NullGameCenterService : IGameCenterService
    {
        public bool IsAvailable => false;
        public bool IsSignedIn => false;
        public void Authenticate() { }
        public void ReportScore(string leaderboardId, long score) { }
        public void UnlockAchievement(string achievementId) { }
        public void ShowDashboard() { }
        public void ShowLeaderboard(string leaderboardId, bool friendsOnly) { }
    }

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>Real Game Center on iOS via the native GameKit bridge.</summary>
    public sealed class GameKitGameCenterService : IGameCenterService
    {
        [DllImport("__Internal")] private static extern void CallerGC_Authenticate();
        [DllImport("__Internal")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CallerGC_IsAuthenticated();
        [DllImport("__Internal")] private static extern void CallerGC_ReportScore(string leaderboardId, long score);
        [DllImport("__Internal")] private static extern void CallerGC_ReportAchievement(string achievementId, double percent);
        [DllImport("__Internal")] private static extern void CallerGC_ShowDashboard();
        [DllImport("__Internal")] private static extern void CallerGC_ShowLeaderboard(string leaderboardId, [MarshalAs(UnmanagedType.I1)] bool friendsOnly);

        public bool IsAvailable => true;
        public bool IsSignedIn => CallerGC_IsAuthenticated();
        public void Authenticate() => CallerGC_Authenticate();
        public void ReportScore(string leaderboardId, long score) => CallerGC_ReportScore(leaderboardId, score);
        public void UnlockAchievement(string achievementId) => CallerGC_ReportAchievement(achievementId, 100.0);
        public void ShowDashboard() => CallerGC_ShowDashboard();
        public void ShowLeaderboard(string leaderboardId, bool friendsOnly) => CallerGC_ShowLeaderboard(leaderboardId, friendsOnly);
    }
#endif

    /// <summary>Sends career progress to Game Center when the player has opted in (deduplicated per session).</summary>
    public static class GameCenterSync
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static IGameCenterService Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new GameKitGameCenterService();
#else
            return new NullGameCenterService();
#endif
        }

        public static void Report(IGameCenterService gc, CareerSaveData career)
        {
            if (gc == null || career == null || !career.settings.gameCenter || !gc.IsAvailable || !gc.IsSignedIn) return;
            foreach (var id in Achievements.Earned(career))
                if (Reported.Add(id)) gc.UnlockAchievement(id);
            // A save edited outside the game keeps its achievements on this device but posts no leaderboard scores.
            if (career.saveFlagged) return;
            foreach (var kv in Achievements.Scores(career))
                if (kv.Value > 0) gc.ReportScore(kv.Key, kv.Value);
        }

        public static void ResetSession() => Reported.Clear();
    }
}
