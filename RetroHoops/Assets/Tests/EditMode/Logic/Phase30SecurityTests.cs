using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 30: backend request signing and replay protection.</summary>
    public class BackendSecurityTests
    {
        private const string Key = "0123456789abcdef0123456789abcdef"; // 32-char match key
        private const double Now = 1_791_250_000_000.0;                // arbitrary epoch ms

        // ---------------------------------------------------------------- RequestTag

        [Test]
        public void RequestTag_IsA64CharHexString()
        {
            string tag = Backend.RequestTag(Key, "{}", Now);
            Assert.AreEqual(64, tag.Length);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(tag, "^[0-9a-f]{64}$"));
        }

        [Test]
        public void RequestTag_IsDeterministic()
        {
            string a = Backend.RequestTag(Key, "{\"x\":1}", Now);
            string b = Backend.RequestTag(Key, "{\"x\":1}", Now);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void RequestTag_ChangesWhenBodyChanges()
        {
            string a = Backend.RequestTag(Key, "{\"seat\":0}", Now);
            string b = Backend.RequestTag(Key, "{\"seat\":1}", Now);
            Assert.AreNotEqual(a, b, "tampered body invalidates the tag");
        }

        [Test]
        public void RequestTag_ChangesWhenTimestampChanges()
        {
            string a = Backend.RequestTag(Key, "{}", Now);
            string b = Backend.RequestTag(Key, "{}", Now + 1);
            Assert.AreNotEqual(a, b, "different timestamp → different tag");
        }

        [Test]
        public void RequestTag_ChangesWhenKeyChanges()
        {
            string other = "ffffffffffffffffffffffffffffffff";
            string a = Backend.RequestTag(Key, "{}", Now);
            string b = Backend.RequestTag(other, "{}", Now);
            Assert.AreNotEqual(a, b, "wrong match key → wrong tag");
        }

        [Test]
        public void RequestTag_NullSafeInputs()
        {
            Assert.DoesNotThrow(() => Backend.RequestTag(null, null, 0));
            Assert.DoesNotThrow(() => Backend.RequestTag("", "", 0));
        }

        // ---------------------------------------------------------------- IsExpired

        [Test]
        public void IsExpired_FreshRequest_IsFalse()
        {
            Assert.IsFalse(Backend.IsExpired(Now, Now, BackendConfig.RequestWindowMs));
            Assert.IsFalse(Backend.IsExpired(Now - 60_000, Now, BackendConfig.RequestWindowMs));
        }

        [Test]
        public void IsExpired_StaleRequest_IsTrue()
        {
            double old = Now - BackendConfig.RequestWindowMs - 1;
            Assert.IsTrue(Backend.IsExpired(old, Now, BackendConfig.RequestWindowMs));
        }

        [Test]
        public void IsExpired_FutureRequest_IsTrue()
        {
            // Clock skew beyond the window is also rejected (prevents pre-fabricated replays).
            double future = Now + BackendConfig.RequestWindowMs + 1;
            Assert.IsTrue(Backend.IsExpired(future, Now, BackendConfig.RequestWindowMs));
        }

        [Test]
        public void ReplayWindow_IsFiveMinutes()
        {
            Assert.AreEqual(300_000.0, BackendConfig.RequestWindowMs);
        }

        // ---------------------------------------------------------------- StartBody with signing

        [Test]
        public void StartBody_Unsigned_HasNoSecurityFields()
        {
            var d = Backend.Parse(Backend.StartBody(Key, "T:opp", 0));
            Assert.IsNull(Backend.Str(d, "tag"),  "unsigned body has no tag");
            Assert.AreEqual(0, Backend.Num(d, "t"), "unsigned body has no t");
        }

        [Test]
        public void StartBody_Signed_HasTagAndTimestamp()
        {
            string body = Backend.StartBody(Key, "T:opp", 0, Now);
            var d = Backend.Parse(body);
            Assert.IsNotNull(Backend.Str(d, "tag"), "signed body carries a tag");
            Assert.AreEqual(Now, Backend.Num(d, "t"), "signed body carries the timestamp");
        }

        [Test]
        public void StartBody_Tag_CoversMatchKey()
        {
            string a = Backend.StartBody(Key, "T:opp", 0, Now);
            string b = Backend.StartBody("ffffffffffffffffffffffffffffffff", "T:opp", 0, Now);
            Assert.AreNotEqual(Backend.Str(Backend.Parse(a), "tag"),
                                Backend.Str(Backend.Parse(b), "tag"), "different match key → different tag");
        }

        // ---------------------------------------------------------------- ResultBody with signing

        [Test]
        public void ResultBody_Signed_HasTagAndTimestamp()
        {
            string body = Backend.ResultBody(Key, 21, 15, 0xdeadbeef, Backend.Outcome.Final, Now);
            var d = Backend.Parse(body);
            Assert.IsNotNull(Backend.Str(d, "tag"));
            Assert.AreEqual(Now, Backend.Num(d, "t"));
        }

        [Test]
        public void ResultBody_Tag_ChangesWithScore()
        {
            string a = Backend.ResultBody(Key, 21, 15, 0, Backend.Outcome.Final, Now);
            string b = Backend.ResultBody(Key, 21, 14, 0, Backend.Outcome.Final, Now);
            Assert.AreNotEqual(Backend.Str(Backend.Parse(a), "tag"),
                                Backend.Str(Backend.Parse(b), "tag"), "score change invalidates the tag");
        }

        [Test]
        public void ResultBody_BackwardCompat_NoTimestamp_StillWorks()
        {
            // Callers that don't yet pass a timestamp still get a valid (unsigned) body.
            var d = Backend.Parse(Backend.ResultBody(Key, 10, 8, 0xbeef, Backend.Outcome.Quit));
            Assert.AreEqual("quit", Backend.Str(d, "outcome"));
            Assert.AreEqual(10.0, Backend.Num(d, "scoreA"));
        }

        // ---------------------------------------------------------------- cross-phone tag agreement

        [Test]
        public void BothPhones_ComputeTheSameTag_ForTheSameGame()
        {
            // Both phones derive the same matchKey and use the same timestamp → identical tags.
            string matchKey = Backend.MatchKey("T:host", "T:guest", 77u);
            double ts = 1_791_300_000_000.0;
            string hostBody   = Backend.StartBody(matchKey, "T:guest", 0, ts);
            string guestBody  = Backend.StartBody(matchKey, "T:guest", 0, ts);
            Assert.AreEqual(Backend.Str(Backend.Parse(hostBody), "tag"),
                            Backend.Str(Backend.Parse(guestBody), "tag"));
        }
    }
}
