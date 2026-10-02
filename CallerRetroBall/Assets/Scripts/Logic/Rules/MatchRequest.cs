namespace CallerRetroBall.Logic
{
    public enum GameMode
    {
        QuickCall = 0,
        Rise = 1,
        Practice = 2,
        /// <summary>First Call Classic: a four-team knockout for the First Callers.</summary>
        Tournament = 3,
        /// <summary>Local 2-player: player 1 vs player 2, each leading a 3-player side. No rewards.</summary>
        Versus = 4,
        /// <summary>Daily Challenge: a seeded Quick Call with a goal for the day.</summary>
        Daily = 5,
        /// <summary>How-to-play tutorial (practice rules, guided steps).</summary>
        Tutorial = 6,
        /// <summary>Rise Mode Rival Challenge vs Neon Static (doesn't count in the standings).</summary>
        Rival = 7,
        /// <summary>King of the Court: short games against league teams back to back until you lose.</summary>
        King = 8,
        /// <summary>Arcade Ladder: six games ending with the secret boss, with three continues.</summary>
        Arcade = 9,
        /// <summary>Attract-mode demo on the title screen: AI vs AI, nobody controls a player.</summary>
        Demo = 10,
        /// <summary>1-on-1: your player against theirs; teammates sit out.</summary>
        OneOnOne = 11,
        /// <summary>The Caller Cup: an eight-team knockout (quarterfinals, semifinals, final).</summary>
        Cup = 12,
    }

    /// <summary>
    /// What the GameScene should play. Set by menus immediately before loading the
    /// scene; the GameScene falls back to a Quick Call default if it is null (e.g.
    /// when pressing Play directly on GameScene in the Editor).
    /// </summary>
    public sealed class MatchRequest
    {
        public GameMode Mode;
        public string HomeTeamId;
        public string AwayTeamId;
        public string CourtId;
        public string RulesId = DefaultContent.DefaultRulesId;
        public string DifficultyId = DefaultContent.DefaultDifficultyId;
        /// <summary>0 = use a time-based seed. Set for reproducible test matches.</summary>
        public uint Seed;
        /// <summary>Rise Mode: 0 = regular/circuit, 1 = semifinal, 2 = final.</summary>
        public int Round;
        /// <summary>Stable id for this fixture (Rise Mode), used to build a unique match id.</summary>
        public string ContextId;
        /// <summary>Optional ratings for the human's player (training upgrades applied).</summary>
        public AttributeSet? HumanAttributes;
        /// <summary>Optional full replacement for the human's player (your created player: look, archetype, number).</summary>
        public PlayerDef HumanPlayer;
        /// <summary>Optional replacement teammates for the human's side (your recruited crew).</summary>
        public System.Collections.Generic.List<PlayerDef> HumanTeammates;
        /// <summary>Starting stamina for the human's team, 0..1 (Rise Mode energy).</summary>
        public float StartingStamina = 1f;
        /// <summary>Teammate release-accuracy bonus from crew chemistry (visible in the Rise hub).</summary>
        public float ChemistryBonus;
        /// <summary>Practice Lab drill (-1 = none): 0 free shoot, 1 passing targets, 2 dribble lane.</summary>
        public int Drill = -1;
        /// <summary>Secret code ALWAYS HOT: your player starts heated up.</summary>
        public bool StartHeated;

        public static MatchRequest QuickCallDefault(ContentCatalog c)
        {
            var league = c.TeamsInTier(TeamTier.League);
            TeamDef home = null, away = null;
            foreach (var t in league)
            {
                if (!t.unlockedByDefault) continue;
                if (home == null) home = t;
                else if (away == null) away = t;
            }
            if (home == null && league.Count > 0) home = league[0];
            if (away == null && league.Count > 1) away = league[1] == home ? league[0] : league[1];

            return new MatchRequest
            {
                Mode = GameMode.QuickCall,
                HomeTeamId = home?.id,
                AwayTeamId = away?.id,
                CourtId = home?.homeCourtId,
            };
        }

        public static MatchRequest PracticeDefault()
        {
            return new MatchRequest
            {
                Mode = GameMode.Practice,
                HomeTeamId = DefaultContent.PlayerCrewId,
                CourtId = DefaultContent.PracticeCourtId,
                RulesId = "rules.practice",
            };
        }
    }
}
