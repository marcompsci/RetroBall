using System;
using System.Security.Cryptography;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// HMAC-SHA256 integrity seal for the on-disk save file. The key lives in the iOS
    /// Keychain (one per install), so edits to the JSON on disk are detected at load
    /// time and fall back to <see cref="LoadStatus.Recovered"/>. This layer is pure
    /// logic: callers supply the key; nothing here touches the Keychain directly.
    ///
    /// Envelope format: "{64 hex chars HMAC}\n{original payload}"
    /// </summary>
    public static class SaveGuard
    {
        // HMAC-SHA256 is 32 bytes = 64 lowercase hex characters.
        private const int TagHexLen = 64;

        /// <summary>Returns a signed envelope wrapping <paramref name="payload"/>.</summary>
        public static string Sign(string payload, byte[] key)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (key == null || key.Length == 0) throw new ArgumentException("key required", nameof(key));
            return ComputeTag(payload, key) + "\n" + payload;
        }

        /// <summary>
        /// Returns the original payload when the envelope's HMAC is valid, or <c>null</c>
        /// when the envelope is missing, malformed, or tampered with.
        /// </summary>
        public static string Verify(string envelope, byte[] key)
        {
            if (string.IsNullOrEmpty(envelope) || key == null || key.Length == 0) return null;
            int nl = envelope.IndexOf('\n');
            if (nl != TagHexLen) return null;
            string tag = envelope.Substring(0, TagHexLen);
            string payload = envelope.Substring(TagHexLen + 1);
            return ConstantTimeEquals(tag, ComputeTag(payload, key)) ? payload : null;
        }

        /// <summary>True when <paramref name="s"/> looks like a SaveGuard-wrapped value (has a tag line of the right length).</summary>
        public static bool IsSigned(string s) =>
            s != null && s.Length > TagHexLen && s.IndexOf('\n') == TagHexLen;

        private static string ComputeTag(string payload, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var sb = new StringBuilder(TagHexLen);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // XOR-based constant-time string comparison prevents timing attacks.
        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
