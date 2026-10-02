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
    }

    [Serializable]
    public class PracticeBests
    {
        public int freeShootMakes;
        public int freeShootStreak;
        public int passingScore;
        /// <summary>Seconds; 0 = never completed.</summary>
        public float dribbleLaneTime;
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
