using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>The game's side of the Retro Hoops Live server: ids, keys and request bodies must match server/.</summary>
    public class BackendTests
    {
        [Test]
        public void AccountToken_MatchesServer()
        {
            // Same vector as server/test/api.test.ts ("the appAccountToken matches the game's (C#) version").
            Assert.AreEqual("d2742be6-91fe-55df-9feb-32f4c5b398c4", Backend.AccountToken("T:12345"));
            Assert.AreNotEqual(Backend.AccountToken("T:1"), Backend.AccountToken("T:2"));
        }

        [Test]
        public void MatchKey_IsTheSameOnBothPhones_AndPerGame()
        {
            string k = Backend.MatchKey("T:host", "T:guest", 42);
            Assert.AreEqual(32, k.Length);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(k, "^[0-9a-f]{32}$"));
            Assert.AreEqual(k, Backend.MatchKey("T:host", "T:guest", 42));
            Assert.AreNotEqual(k, Backend.MatchKey("T:host", "T:guest", 43));
            Assert.AreNotEqual(k, Backend.MatchKey("T:guest", "T:host", 42));
            Assert.AreEqual("0000beef", Backend.HashHex(0xbeef));
        }

        [Test]
        public void Bodies_AreValidJson_WithTheFieldsTheServerChecks()
        {
            var s = Backend.Parse(Backend.SessionBody("T:1", "Omari", "https://static.gc.apple.com/k.cer", "c2ln", "c2FsdA==", 1791249785013.0));
            Assert.AreEqual("T:1", Backend.Str(s, "playerId"));
            Assert.AreEqual(1791249785013.0, Backend.Num(s, "timestamp"));
            var r = Backend.Parse(Backend.ResultBody("0123456789abcdef0123456789abcdef", 21, 15, 0xdeadbeef, Backend.Outcome.OpponentLeft));
            Assert.AreEqual("deadbeef", Backend.Str(r, "hash"));
            Assert.AreEqual("opponent_left", Backend.Str(r, "outcome"));
            Assert.AreEqual(21.0, Backend.Num(r, "scoreA"));
            var st = Backend.Parse(Backend.StartBody("k", "T:2", 1));
            Assert.AreEqual(1.0, Backend.Num(st, "seat"));
            Assert.IsNull(Backend.Parse("not json"));
        }

        [Test]
        public void ServerUrl_MustBePlainHttps()
        {
            Assert.IsTrue(BackendConfig.IsSafeUrl("https://retrohoops-api.example.workers.dev"));
            Assert.IsFalse(BackendConfig.IsSafeUrl("http://retrohoops-api.example.workers.dev"));
            Assert.IsFalse(BackendConfig.IsSafeUrl("https://user:pw@example.com"));
            Assert.IsFalse(BackendConfig.IsSafeUrl("https://example.com:8443"));
            Assert.IsFalse(BackendConfig.IsSafeUrl(""));
            Assert.IsFalse(BackendConfig.Enabled, "off until Omari deploys the server and sets the address");
        }

        [Test]
        public void ServerNumbers_ReplaceTheLocalRating()
        {
            var o = Backend.Parse("{\"player\":{\"rating\":1234,\"best\":1300,\"wins\":7,\"losses\":3,\"games\":10}}");
            var p = Backend.ReadPlayer(Backend.Obj(o, "player"));
            var s = new LiveSaveData { rating = 5000 };
            Backend.Mirror(s, p);
            Assert.AreEqual(1234, s.rating);
            Assert.AreEqual(1300, s.best);
            Assert.AreEqual(10, s.games);
        }
    }
}
