using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    // ------------------------------------------------------------------
    // Persistent, versioned player data. Plain fields only: written to JSON by SaveCodec.
    // Nothing here identifies the player; the nickname is local only.
    // ------------------------------------------------------------------

    [Serializable]
    public class SettingsData
    {
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.9f;
        public bool haptics = true;
        public bool screenShake = true;
        /// <summary>0.85 – 1.25.</summary>
        public float uiScale = 1f;
        public bool colorblindContrast;
        public string difficultyId = DefaultContent.DefaultDifficultyId;
        /// <summary>Opt-in: sign in to Game Center for leaderboards and achievements.</summary>
        public bool gameCenter;

        // Accessibility
        /// <summary>Joystick on the right, action buttons on the left.</summary>
        public bool leftHanded;
        /// <summary>Action buttons 25% bigger.</summary>
        public bool largeButtons;
        /// <summary>Tap SHOOT to start the meter and tap again to release (no holding).</summary>
        public bool tapToShoot;
        /// <summary>No screen shake, sparks, or score bounce.</summary>
        public bool reduceMotion;
        /// <summary>"en" or "es".</summary>
        public string language = Loc.English;
        /// <summary>CRT look: 0 = off, 1 = soft scanlines, 2 = strong (scanlines + vignette).</summary>
        public int crt = 1;
        /// <summary>Use the screen's full refresh rate (120 Hz on ProMotion iPhones) instead of 60.</summary>
        public bool highFrameRate = true;
        /// <summary>Title screen plays an AI demo game after a while with no input.</summary>
        public bool attractMode = true;
        /// <summary>Small frame-rate readout in the corner (for checking performance).</summary>
        public bool showFps;
        /// <summary>Coach Dee's tips during your first games.</summary>
        public bool coachTips = true;
    }

    /// <summary>Your own player (Locker Room ▸ CREATE). Until created, the game uses Rook.</summary>
    [Serializable]
    public class CustomPlayerData
    {
        public bool created;
        public int skinTone;
        public int hairStyle;
        public int hairColor;
        public int body = 1;
        public int heightTier = 1;
        public int jerseyNumber = 1;
        public string archetypeId;
    }

    /// <summary>Best single-game marks and streaks.</summary>
    [Serializable]
    public class CareerRecords
    {
        public int points;
        public int assists;
        public int rebounds;
        public int steals;
        public int blocks;
        public int greens;
        public int biggestWin;
        public int winStreak;
        public int bestWinStreak;
    }

    [Serializable]
    public class MatchHistoryEntry
    {
        public int day;
        public GameMode mode;
        public string opponentId;
        public int scoreFor;
        public int scoreAgainst;
        public int points;
        public int assists;
        public int rebounds;
        public bool Won => scoreFor > scoreAgainst;
    }

    /// <summary>One Rise season (0 = The Blacktop Circuit).</summary>
    [Serializable]
    public class SeasonHistoryEntry
    {
        public int season;
        public int games;
        public int wins;
        public int losses;
        public int points;
        public int assists;
        public int rebounds;
        public string result;
    }

    [Serializable]
    public class UpgradeProgress
    {
        public string id;
        public int level;
    }

    [Serializable]
    public class CareerTotals
    {
        public int games;
        public int wins;
        public int losses;
        public int points;
        public int assists;
        public int rebounds;
        public int steals;
        public int blocks;
        public int fieldGoalsMade;
        public int fieldGoalsAttempted;
        public int greens;
        public int championships;
        /// <summary>Alley-oops you threw or finished.</summary>
        public int alleyOops;
        public int heatUps;
    }

    [Serializable]
    public class PracticeBests
    {
        public int freeShootMakes;
        public int freeShootStreak;
        public int passingScore;
        /// <summary>Seconds; 0 = never completed.</summary>
        public float dribbleLaneTime;
        public int threePointBest;
        public int lockdownBest;
        public int shootoutWins;
        /// <summary>Around the World best time in seconds (0 = never finished).</summary>
        public float aroundWorldTime;
        public int horseWins;
    }

    public enum RiseStage { Circuit = 0, Season = 1, Playoffs = 2, Complete = 3 }

    [Serializable]
    public class ScheduledGame
    {
        public int week;
        public string homeId;
        public string awayId;
        public bool played;
        public int homeScore;
        public int awayScore;
        /// <summary>0 = regular season, 1 = semifinal, 2 = final.</summary>
        public int round;

        public bool Involves(string teamId) => homeId == teamId || awayId == teamId;
        public string WinnerId => !played ? null : (homeScore >= awayScore ? homeId : awayId);
        public string LoserId => !played ? null : (homeScore >= awayScore ? awayId : homeId);
    }

    [Serializable]
    public class SeasonSaveData
    {
        public int seasonNumber = 1;
        public uint seed;
        public List<string> teamIds = new List<string>();
        public List<ScheduledGame> games = new List<ScheduledGame>();
        public int currentWeek;
        public int weeks;
        public string championId;
    }

    [Serializable]
    public class RiseSaveData
    {
        public RiseStage stage = RiseStage.Circuit;
        /// <summary>Crew ids beaten in The Blacktop Circuit.</summary>
        public List<string> circuitBeaten = new List<string>();
        public SeasonSaveData season;
        /// <summary>0..100. Games drain it; rest events restore it. Starts matches with less stamina when low.</summary>
        public int energy = 100;
        /// <summary>0..100. Helps teammates' timing a little.</summary>
        public int chemistry = 50;
        public List<string> seenEvents = new List<string>();
        /// <summary>Your two teammates (player ids). Empty = the original First Callers.</summary>
        public List<string> teammates = new List<string>();
        /// <summary>Players unlocked for recruiting by beating their team.</summary>
        public List<string> recruitable = new List<string>();
        /// <summary>Players you've paid to sign (swap back in for free).</summary>
        public List<string> signed = new List<string>();
        public string pendingEventId;
        public int seasonsPlayed;
    }

    [Serializable]
    public class CareerSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string nickname = "Rook";
        public int signalPoints;
        public int fans;
        public List<UpgradeProgress> upgrades = new List<UpgradeProgress>();
        /// <summary>Completed games since the last upgrade (training time).</summary>
        public int gamesSinceUpgrade = 99;
        public List<string> ownedCosmetics = new List<string>();
        public string equippedJersey;
        public string equippedShoes;
        public string equippedBanner;
        public string equippedCelebration;
        public string equippedMove;
        public CareerTotals totals = new CareerTotals();
        public PracticeBests practice = new PracticeBests();
        public SettingsData settings = new SettingsData();
        public RiseSaveData rise = new RiseSaveData();
        public ClassicSaveData classic = new ClassicSaveData();
        public CupSaveData cup = new CupSaveData();
        public DynastySaveData dynasty = new DynastySaveData();
        public DailySaveData daily = new DailySaveData();
        /// <summary>Finished the how-to-play tutorial at least once.</summary>
        public bool tutorialDone;
        public CustomPlayerData customPlayer = new CustomPlayerData();
        public CareerRecords records = new CareerRecords();
        public RivalSaveData rival = new RivalSaveData();
        public KingSaveData king = new KingSaveData();
        public SecretsSaveData secrets = new SecretsSaveData();
        public CustomTeamData customTeam = new CustomTeamData();
        /// <summary>Badges already announced.</summary>
        public List<string> badgesSeen = new List<string>();
        /// <summary>Coach tips already shown.</summary>
        public List<string> tipsSeen = new List<string>();
        /// <summary>Story scenes already shown.</summary>
        public List<string> storySeen = new List<string>();
        public List<MatchHistoryEntry> history = new List<MatchHistoryEntry>();
        public List<SeasonHistoryEntry> seasons = new List<SeasonHistoryEntry>();
        /// <summary>Records broken by the last applied game (not saved; shown on the post-game screen).</summary>
        [NonSerialized] public List<string> lastNewRecords = new List<string>();
        /// <summary>Recent match ids already rewarded (guards against double grants).</summary>
        public List<string> appliedMatchIds = new List<string>();

        public int UpgradeLevel(string upgradeId)
        {
            foreach (var u in upgrades) if (u.id == upgradeId) return u.level;
            return 0;
        }

        public string Equipped(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.JerseyPalette: return equippedJersey;
                case CosmeticSlot.Shoes: return equippedShoes;
                case CosmeticSlot.CourtBanner: return equippedBanner;
                case CosmeticSlot.Celebration: return equippedCelebration;
                default: return equippedMove;
            }
        }

        public void SetEquipped(CosmeticSlot slot, string id)
        {
            switch (slot)
            {
                case CosmeticSlot.JerseyPalette: equippedJersey = id; break;
                case CosmeticSlot.Shoes: equippedShoes = id; break;
                case CosmeticSlot.CourtBanner: equippedBanner = id; break;
                case CosmeticSlot.Celebration: equippedCelebration = id; break;
                default: equippedMove = id; break;
            }
        }
    }
}
