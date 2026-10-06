using System;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class LiveSaveData
    {
        public int rating = LiveMode.StartRating;
        public int best = LiveMode.StartRating;
        public int wins, losses, games;
        /// <summary>The team you take into Live games.</summary>
        public string teamId = "";
        /// <summary>Last known subscription end (Unix seconds), from the App Store. Only used to show the state while it refreshes.</summary>
        public double subscribedUntil;
    }

    /// <summary>
    /// RETRO HOOPS LIVE: online head-to-head against other players through Game Center matchmaking
    /// (Apple runs the servers; no account of ours). Needs the Retro Hoops Live subscription, an
    /// auto-renewing monthly App Store subscription. Games use the same lockstep simulation as two-phone
    /// play, with a longer input delay for internet latency. Wins and losses move an Elo rating.
    /// </summary>
    public static class LiveMode
    {
        /// <summary>App Store Connect product id (auto-renewable subscription, 1 month).</summary>
        public const string ProductId = "com.phoronomicstudios.retrohoops.live.monthly";
        public const string SubscriptionGroup = "Retro Hoops Live";
        /// <summary>Shown only until the App Store returns the real, localised price.</summary>
        public const string PriceFallback = "$10.99";
        /// <summary>8 steps = 133 ms: covers most internet round trips without stalls.</summary>
        public const int InputDelay = 8;
        /// <summary>AI teammates play at this level in Live games, whatever either player's setting.</summary>
        public const string DifficultyId = DefaultContent.DefaultDifficultyId;
        public const int StartRating = 1000;
        public const int K = 32;
        public const string LeaderboardId = "retrohoops.live.rating";
        /// <summary>A game counts once it's this far in (quitting earlier is a loss for the quitter only after this).</summary>
        public const float CountsAfterSeconds = 20f;

        public static readonly string[] Tiers = { "ROOKIE", "HOOPER", "STARTER", "ALL-STAR", "LEGEND" };
        public static readonly int[] TierFloor = { 0, 900, 1100, 1300, 1500 };

        public static string Tier(int rating)
        {
            int t = 0;
            for (int i = 0; i < TierFloor.Length; i++) if (rating >= TierFloor[i]) t = i;
            return Tiers[t];
        }

        /// <summary>Chance <paramref name="mine"/> beats <paramref name="theirs"/> (Elo).</summary>
        public static double Expected(int mine, int theirs) => 1.0 / (1.0 + Math.Pow(10.0, (theirs - mine) / 400.0));

        /// <summary>Rating change for a result: beating a stronger player moves you more.</summary>
        public static int Change(int mine, int theirs, bool won) =>
            (int)Math.Round(K * ((won ? 1.0 : 0.0) - Expected(mine, theirs)));

        /// <summary>Records a finished Live game. Returns the rating change.</summary>
        public static int Apply(LiveSaveData s, int theirRating, bool won)
        {
            int delta = Change(s.rating, theirRating, won);
            s.rating = Math.Max(100, s.rating + delta);
            s.best = Math.Max(s.best, s.rating);
            s.games++;
            if (won) s.wins++; else s.losses++;
            return delta;
        }

        /// <summary>
        /// Game Center player group: only players on the same game version (and link version) are matched,
        /// so both sides always run the same simulation.
        /// </summary>
        public static int PlayerGroup(string appVersion) =>
            (int)(StableHash.Of("live:" + LinkProtocol.Version + ":" + (appVersion ?? "")) % 1000000u) + 1;

        /// <summary>Host (seat 0) is the player whose Game Center id sorts first; both phones agree without talking.</summary>
        public static int Seat(string myPlayerId, string theirPlayerId) =>
            string.CompareOrdinal(myPlayerId ?? "", theirPlayerId ?? "") <= 0 ? 0 : 1;

        /// <summary>Cached view of the subscription (the App Store's answer replaces it when it arrives).</summary>
        public static bool CachedActive(LiveSaveData s, double nowUnix) => s != null && s.subscribedUntil > nowUnix;

        /// <summary>Plain-language subscription terms for the paywall (App Review guideline 3.1.2).</summary>
        public static string Terms(string price) =>
            "Retro Hoops Live is a monthly auto-renewing subscription for " + (string.IsNullOrEmpty(price) ? PriceFallback : price) + " a month. " +
            "Payment is charged to your Apple Account when you confirm. It renews automatically unless you turn off auto-renew at least 24 hours " +
            "before the end of the current period; your account is charged for the next month within 24 hours before it ends. " +
            "Manage or cancel any time in Settings ▸ your name ▸ Subscriptions. Everything else in Retro Hoops stays free and works offline.";

        /// <summary>Must be a public page before App Review (docs/PRIVACY.md is the text; host it, e.g. on GitHub Pages, and update this).</summary>
        public const string PrivacyPolicyUrl = "https://github.com/marcompsci/RetroBall/blob/main/RetroHoops/docs/PRIVACY.md";
        public const string TermsOfUseUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
        public const string ManageUrl = "https://apps.apple.com/account/subscriptions";
    }
}
