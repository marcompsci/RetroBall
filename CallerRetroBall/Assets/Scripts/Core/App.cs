using CallerRetroBall.Audio;
using CallerRetroBall.Data;
using CallerRetroBall.UI;
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
        /// <summary>The Player Settings version (set by ProjectSetup / ReleaseTools), shown on menus.</summary>
        public static string Version => Application.version;

        public static ContentDatabase Content { get; private set; }

        /// <summary>The match the GameScene should play next (set by menus).</summary>
        public static MatchRequest PendingMatch { get; set; }

        public static bool IsInitialized => Content != null;

        public static ContentCatalog Catalog => Content?.Catalog;

        /// <summary>The local career (progress, settings). Loaded at boot; saved after games and changes.</summary>
        public static CareerSaveData Career { get; private set; }
        public static LoadStatus CareerLoadStatus { get; private set; }

        public static readonly RewardTuning Rewards = RewardTuning.Default;
        /// <summary>Game Center on iOS builds, a no-op elsewhere. Only used after the player opts in.</summary>
        public static readonly IGameCenterService GameCenter = GameCenterSync.Create();

        /// <summary>Settings ▸ Game Center toggle: signs in when turned on.</summary>
        public static void SetGameCenter(bool on)
        {
            if (Career == null) return;
            Career.settings.gameCenter = on;
            SaveCareer();
            if (on) GameCenter.Authenticate();
        }

        /// <summary>Pushes achievements and leaderboard scores (no-op unless opted in and signed in).</summary>
        public static void ReportGameCenter() => GameCenterSync.Report(GameCenter, Career);

        /// <summary>Today's local day number for the Daily Challenge.</summary>
        public static int Today => DailyChallenges.DayNumber(System.DateTime.Now);

        /// <summary>What the last Rise game changed (announced once by the Rise hub, then cleared).</summary>
        public static RiseOutcome LastRiseOutcome { get; set; }

        /// <summary>Set after a First Call Classic game so the main menu reopens the bracket.</summary>
        public static bool OpenClassicOnMenu { get; set; }

        /// <summary>
        /// Applies the career to a request: your trained ratings when you play as the First Callers
        /// (Rise and Practice), plus Rise energy (starting stamina) and chemistry.
        /// </summary>
        public static MatchRequest PrepareRequest(MatchRequest request)
        {
            if (request == null || Career == null || Catalog == null) return request;
            if (request.HomeTeamId == DefaultContent.PlayerCrewId)
            {
                var rook = Catalog.Player(DefaultContent.RookPlayerId);
                if (rook != null) request.HumanAttributes = Logic.Career.EffectiveRatings(rook.attributes, Career, Catalog);
            }
            if (request.Mode == GameMode.Rise)
            {
                request.StartingStamina = RiseEngine.StartingStamina(Career.rise);
                request.ChemistryBonus = Career.rise.chemistry / 100f * 0.1f;
            }
            return request;
        }

        public static void SaveCareer()
        {
            if (Career != null) SaveStore.Save(Career);
        }

        /// <summary>Settings ▸ Reset: wipes the save and starts a fresh career.</summary>
        public static void ResetCareer()
        {
            SaveStore.Delete();
            Career = Logic.Career.New(Catalog);
            SaveCareer();
            ApplySettings();
        }

        /// <summary>Pushes settings to the systems that use them.</summary>
        public static void ApplySettings()
        {
            if (Career == null) return;
            UiKit.UiScale = Career.settings.uiScale;
            AudioManager.ApplySettings();
        }

#if UNITY_EDITOR || DEBUG
        /// <summary>Development only: lots of currency and every cosmetic unlocked.</summary>
        public static void DevUnlockAll()
        {
            Career.signalPoints += 100000;
            Career.fans += 100000;
            foreach (var c in Catalog.Cosmetics)
                if (!Career.ownedCosmetics.Contains(c.id)) Career.ownedCosmetics.Add(c.id);
            Career.gamesSinceUpgrade = 99;
            SaveCareer();
        }
#endif

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
                                 ". Run 'RetroBall ▸ Run Project Setup' to generate editable assets.");
            }

#if UNITY_EDITOR || DEBUG
            var report = ContentValidator.Validate(Content.Catalog);
            if (!report.IsValid) Debug.LogError("[CallerRetroBall] " + report);
            else if (report.Warnings.Count > 0) Debug.LogWarning("[CallerRetroBall] " + report);
#endif

            Career = SaveStore.Load(Content.Catalog, out var status);
            CareerLoadStatus = status;
            AudioManager.EnsureExists();
            ApplySettings();
            if (Career.settings.gameCenter) GameCenter.Authenticate();

            SceneFlow.EnsureExists();
        }

        // Supports "Enter Play Mode" without domain reload: static state is reset each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Content = null;
            PendingMatch = null;
            Career = null;
            TextureFactory.ClearCache();
            GameCenterSync.ResetSession();
        }
    }
}
