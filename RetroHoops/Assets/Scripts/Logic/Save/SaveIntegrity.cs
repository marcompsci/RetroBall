using System;

namespace CallerRetroBall.Logic
{
    /// <summary>What the seal on a save file said when it was read.</summary>
    public enum SaveVerdict
    {
        /// <summary>No save yet (a fresh career).</summary>
        New = 0,
        /// <summary>A plain save from before Phase 31: loaded as-is and sealed on the next write.</summary>
        Legacy = 1,
        /// <summary>The seal matches this install's key.</summary>
        Verified = 2,
        /// <summary>
        /// Sealed, but this install's key was just created (the Keychain was cleared, or the save came from a
        /// backup on another device). It can't be checked, so it's trusted and resealed with the new key.
        /// </summary>
        KeyLost = 3,
        /// <summary>The key exists and the seal doesn't match: the file was edited outside the game.</summary>
        Tampered = 4,
        /// <summary>The file couldn't be read at all (a fresh career; the old file is kept as a backup).</summary>
        Unreadable = 5,
    }

    /// <summary>
    /// Phase 31: the on-disk career is sealed with <see cref="SaveGuard"/> (HMAC-SHA256) using a random key kept in
    /// the iOS Keychain, so an edit to the file outside the game is noticed. A flagged career is never wiped: it
    /// loads with its numbers clamped to what the game can produce, and it stops posting Game Center leaderboard
    /// scores (<see cref="CareerSaveData.saveFlagged"/>). Retro Hoops Live ratings live on the server, so a local
    /// edit can't change them. iCloud copies stay unsealed (they're adopted on other devices, which have their own
    /// keys) and are clamped the same way when adopted.
    /// This stops casual editing; a jailbroken phone can read the Keychain, and that's out of scope.
    /// </summary>
    public static class SaveIntegrity
    {
        /// <summary>The largest currency / count a real career could plausibly hold.</summary>
        public const int MaxCurrency = 9_999_999;
        public const int MaxCount = 999_999;

        /// <summary>Text to write to disk: the save sealed with <paramref name="key"/>.</summary>
        /// <remarks>With no key (the Keychain can't be read this launch) the save is written unsealed, like a pre-Phase 31 save.</remarks>
        public static string Seal(CareerSaveData data, byte[] key) =>
            key == null || key.Length == 0 ? SaveCodec.Encode(data) : SaveGuard.Sign(SaveCodec.Encode(data), key);

        /// <param name="keyIsNew">True when this install's key was created just now (there was none before).</param>
        public static CareerSaveData Open(string fileText, byte[] key, bool keyIsNew, ContentCatalog catalog, out LoadStatus status, out SaveVerdict verdict)
        {
            if (string.IsNullOrEmpty(fileText))
            {
                verdict = SaveVerdict.New;
                return SaveCodec.Decode(fileText, catalog, out status);
            }
            if (!SaveGuard.IsSigned(fileText))
            {
                var legacy = SaveCodec.Decode(fileText, catalog, out status);
                verdict = status == LoadStatus.Recovered ? SaveVerdict.Unreadable : SaveVerdict.Legacy;
                return legacy;
            }
            if (key == null || key.Length == 0) keyIsNew = true; // can't check it: trust it, never flag it
            string payload = SaveGuard.Verify(fileText, key);
            if (payload != null)
            {
                var ok = SaveCodec.Decode(payload, catalog, out status);
                verdict = status == LoadStatus.Recovered ? SaveVerdict.Unreadable : SaveVerdict.Verified;
                return ok;
            }
            // The seal didn't match: read what's inside anyway, so nobody loses a career to a lost key.
            var data = SaveCodec.Decode(fileText.Substring(fileText.IndexOf('\n') + 1), catalog, out status);
            if (status == LoadStatus.Recovered)
            {
                verdict = SaveVerdict.Unreadable;
                return data;
            }
            verdict = keyIsNew ? SaveVerdict.KeyLost : SaveVerdict.Tampered;
            if (verdict == SaveVerdict.Tampered)
            {
                Clamp(data);
                data.saveFlagged = true;
            }
            return data;
        }

        /// <summary>Pulls every number back into the range the game itself can produce (also used on iCloud copies).</summary>
        public static void Clamp(CareerSaveData d)
        {
            if (d == null) return;
            d.signalPoints = Math.Max(0, Math.Min(MaxCurrency, d.signalPoints));
            d.fans = Math.Max(0, Math.Min(MaxCurrency, d.fans));
            if (d.totals != null)
            {
                var t = d.totals;
                t.games = Range(t.games);
                t.wins = Math.Min(Range(t.wins), t.games);
                t.losses = Math.Min(Range(t.losses), t.games - t.wins);
                t.points = Range(t.points);
                t.assists = Range(t.assists);
                t.rebounds = Range(t.rebounds);
                t.steals = Range(t.steals);
                t.blocks = Range(t.blocks);
                t.fieldGoalsAttempted = Range(t.fieldGoalsAttempted);
                t.fieldGoalsMade = Math.Min(Range(t.fieldGoalsMade), t.fieldGoalsAttempted);
                t.greens = Range(t.greens);
                t.championships = Math.Min(Range(t.championships), t.games);
            }
            d.live = LiveMode.Sanitize(d.live);
        }

        private static int Range(int v) => Math.Max(0, Math.Min(MaxCount, v));
    }
}
