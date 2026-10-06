using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// The Retro Hoops Live server (folder <c>server/</c> in the repo, Cloudflare Workers). Empty
    /// <see cref="Url"/> = no server: Live still works, with ratings kept on each phone (Phase 27).
    /// After deploying the server, put its address here (https only) and rebuild.
    /// </summary>
    public static class BackendConfig
    {
        public const string Url = "";
        /// <summary>Requests older or newer than this are rejected as replays.</summary>
        public const double RequestWindowMs = 5 * 60 * 1000.0;

        public static bool Enabled => IsSafeUrl(Url);

        /// <summary>Only HTTPS, no user info or odd ports; the app never talks to the server in plain text.</summary>
        public static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            return u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.UserInfo) && (u.IsDefaultPort || u.Port == 443) && string.IsNullOrEmpty(u.Query);
        }
    }

    /// <summary>Request bodies and response parsing for the Live server (pure, testable; sending is in Core/BackendClient).</summary>
    public static class Backend
    {
        // ---------------------------------------------------------------- request signing

        /// <summary>
        /// HMAC-SHA256 authentication tag for a signed request. The caller passes the body string
        /// <em>before</em> the tag field is added; the server strips "tag" and recomputes.
        /// Key = matchKey (shared secret both phones derived from the same game seed).
        /// </summary>
        public static string RequestTag(string matchKey, string body, double timestampMs)
        {
            var key = Encoding.UTF8.GetBytes(matchKey ?? "");
            // Cover both the body and the timestamp: changing either one invalidates the tag.
            var data = Encoding.UTF8.GetBytes((body ?? "") + "|" + ((long)Math.Floor(timestampMs)).ToString());
#pragma warning disable SYSLIB0021, CS0618
            using (var hmac = new HMACSHA256(key))
#pragma warning restore SYSLIB0021, CS0618
            {
                var bytes = hmac.ComputeHash(data);
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Returns true when a request is outside the allowed replay window.</summary>
        public static bool IsExpired(double requestMs, double nowMs, double windowMs) =>
            Math.Abs(nowMs - requestMs) > windowMs;

        /// <summary>
        /// The appAccountToken for a player (a UUID from SHA-256 of their Game Center id). Purchases are
        /// tagged with it so the server can tell a subscription belongs to this player. Must match
        /// <c>accountToken()</c> in server/src/appstore.ts.
        /// </summary>
        public static string AccountToken(string teamPlayerId)
        {
            string h = Sha256Hex("retrohoops-account:" + teamPlayerId);
            int variant = (Convert.ToInt32(h.Substring(16, 1), 16) & 0x3) | 0x8;
            string v = h.Substring(0, 12) + "5" + h.Substring(13, 3) + variant.ToString("x") + h.Substring(17, 15);
            return v.Substring(0, 8) + "-" + v.Substring(8, 4) + "-" + v.Substring(12, 4) + "-" + v.Substring(16, 4) + "-" + v.Substring(20, 12);
        }

        /// <summary>Both phones compute the same key for a game: host id, guest id and the seed.</summary>
        public static string MatchKey(string hostId, string guestId, uint seed) =>
            Sha256Hex("match:" + hostId + "|" + guestId + "|" + seed).Substring(0, 32);

        public static string HashHex(uint hash) => hash.ToString("x8");

        public static string Sha256Hex(string text)
        {
            // SHA256Managed directly (not SHA256.Create()): no name lookup that code stripping could break.
#pragma warning disable SYSLIB0021, CS0618
            using (var sha = new SHA256Managed())
#pragma warning restore SYSLIB0021, CS0618
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string SessionBody(string teamPlayerId, string name, string keyUrl, string signature, string salt, double timestampMs) =>
            MiniJson.Write(new Dictionary<string, object>
            {
                ["playerId"] = teamPlayerId ?? "", ["name"] = Clip(name, 40), ["publicKeyUrl"] = keyUrl ?? "",
                ["signature"] = signature ?? "", ["salt"] = salt ?? "", ["timestamp"] = Math.Floor(timestampMs),
            }, false);

        public static string SubscriptionBody(string originalTransactionId) =>
            MiniJson.Write(new Dictionary<string, object> { ["originalTransactionId"] = originalTransactionId ?? "" }, false);

        public static string StartBody(string matchKey, string opponentId, int seat, double timestampMs = 0)
        {
            var d = new Dictionary<string, object> { ["matchKey"] = matchKey, ["opponentId"] = opponentId ?? "", ["seat"] = seat };
            if (timestampMs > 0) Sign(d, matchKey, timestampMs);
            return MiniJson.Write(d, false);
        }

        public enum Outcome { Final, Quit, OpponentLeft }

        public static string ResultBody(string matchKey, int scoreA, int scoreB, uint hash, Outcome outcome, double timestampMs = 0)
        {
            var d = new Dictionary<string, object>
            {
                ["matchKey"] = matchKey, ["scoreA"] = scoreA, ["scoreB"] = scoreB, ["hash"] = HashHex(hash),
                ["outcome"] = outcome == Outcome.Quit ? "quit" : outcome == Outcome.OpponentLeft ? "opponent_left" : "final",
            };
            if (timestampMs > 0) Sign(d, matchKey, timestampMs);
            return MiniJson.Write(d, false);
        }

        // Appends "t" (timestamp) and "tag" (HMAC) to a request dict in-place. The tag covers the
        // unsigned body so tampering with any field or replaying to a different game both fail.
        private static void Sign(Dictionary<string, object> d, string matchKey, double timestampMs)
        {
            string unsigned = MiniJson.Write(d, false);
            d["t"] = Math.Floor(timestampMs);
            d["tag"] = RequestTag(matchKey, unsigned, timestampMs);
        }

        private static string Clip(string s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s.Substring(0, max) : s);

        // ---------------------------------------------------------------- responses

        public sealed class PlayerInfo
        {
            public int Rating = LiveMode.StartRating, Best = LiveMode.StartRating, Wins, Losses, Games;
        }

        public static Dictionary<string, object> Parse(string json)
        {
            try { return MiniJson.Read(json) as Dictionary<string, object>; }
            catch (MiniJson.JsonException) { return null; }
        }

        public static string Str(Dictionary<string, object> o, string key) => o != null && o.TryGetValue(key, out var v) && v is string s ? s : null;
        public static double Num(Dictionary<string, object> o, string key, double fallback = 0) => o != null && o.TryGetValue(key, out var v) && v is double d ? d : fallback;
        public static bool Bool(Dictionary<string, object> o, string key) => o != null && o.TryGetValue(key, out var v) && v is bool b && b;
        public static Dictionary<string, object> Obj(Dictionary<string, object> o, string key) => o != null && o.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static PlayerInfo ReadPlayer(Dictionary<string, object> p)
        {
            if (p == null) return null;
            return new PlayerInfo
            {
                Rating = (int)Num(p, "rating", LiveMode.StartRating), Best = (int)Num(p, "best", LiveMode.StartRating),
                Wins = (int)Num(p, "wins"), Losses = (int)Num(p, "losses"), Games = (int)Num(p, "games"),
            };
        }

        /// <summary>Copies the server's numbers into the local Live save (the server is the source of truth once on).</summary>
        public static void Mirror(LiveSaveData s, PlayerInfo p)
        {
            if (s == null || p == null) return;
            s.rating = p.Rating;
            s.best = Math.Max(p.Best, p.Rating);
            s.wins = p.Wins;
            s.losses = p.Losses;
            s.games = p.Games;
        }
    }
}
