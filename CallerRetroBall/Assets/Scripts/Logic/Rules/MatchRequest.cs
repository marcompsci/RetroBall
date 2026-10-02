namespace CallerRetroBall.Logic
{
    public enum GameMode
    {
        QuickCall = 0,
        Rise = 1,
        Practice = 2,
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
