namespace CallerRetroBall.Core
{
    /// <summary>
    /// Integration boundary for Apple Game Center (leaderboards / achievements). NOT configured:
    /// it needs an Apple Developer account, App Store Connect leaderboard ids, and the Game Center
    /// capability in Xcode. Until then the game uses <see cref="NullGameCenterService"/>.
    /// </summary>
    public interface IGameCenterService
    {
        bool IsAvailable { get; }
        void Authenticate();
        void ReportScore(string leaderboardId, long score);
        void UnlockAchievement(string achievementId);
    }

    /// <summary>Default no-op implementation: the game is fully playable offline without it.</summary>
    public sealed class NullGameCenterService : IGameCenterService
    {
        public bool IsAvailable => false;
        public void Authenticate() { }
        public void ReportScore(string leaderboardId, long score) { }
        public void UnlockAchievement(string achievementId) { }
    }
}
