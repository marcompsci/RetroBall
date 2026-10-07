namespace CallerRetroBall.Logic
{
    /// <summary>Phase 38: how the shot meter shows a contest while you charge (see <see cref="MatchSimulation.ContestLevel"/>).</summary>
    public static class ShotFeel
    {
        /// <summary>Below this the shot counts as open (no tint).</summary>
        public const float LightContest = 0.2f;
        /// <summary>At or above this the frame shows the full "contested" colour.</summary>
        public const float HeavyContest = 0.55f;

        /// <summary>0 (open), 0.5 (a hand nearby) or 1 (contested): stepped rather than smooth so it's easy to read mid-game.</summary>
        public static float ContestStep(float contest) => contest >= HeavyContest ? 1f : contest >= LightContest ? 0.5f : 0f;
    }
}
