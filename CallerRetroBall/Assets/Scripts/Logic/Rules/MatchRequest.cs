namespace CallerRetroBall.Logic
{
    public enum GameMode
    {
        QuickCall = 0,
        Rise = 1,
        Practice = 2,
        /// <summary>First Call Classic: a four-team knockout for the First Callers.</summary>
        Tournament = 3,
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
        /// <summary>Starting stamina for the human's team, 0..1 (Rise Mode energy).</summary>
        public float StartingStamina = 1f;
        /// <summary>Teammate release-accuracy bonus from crew chemistry (visible in the Rise hub).</summary>
        public float ChemistryBonus;
        /// <summary>Practice Lab drill (-1 = none): 0 free shoot, 1 passing targets, 2 dribble lane.</summary>
        public int Drill = -1;

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
