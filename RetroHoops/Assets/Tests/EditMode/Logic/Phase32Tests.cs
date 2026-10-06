using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// Phase 32: the signed match requests the Mac session's Phase30SecurityTests describe, checked against the
    /// server (server/src/sign.ts uses the same two vectors in test/api.test.ts).
    /// </summary>
    public class Phase32Tests
    {
        private const string Key = "0123456789abcdef0123456789abcdef";

        [Test]
        public void SignedRequests_MatchTheServer()
        {
            Assert.AreEqual("2e1abf95571a7a23b34a4b7bd646ada437cbc907198e0c7dd807ec1ac0dee222",
                Backend.Str(Backend.Parse(Backend.StartBody(Key, "T:opp", 0, 1_791_250_000_000.0)), "tag"));
            Assert.AreEqual("7cdaa7411f36ca4ae7cc371072db939704365cab0e750d0382b08e2091d421d7",
                Backend.Str(Backend.Parse(Backend.ResultBody(Key, 21, 15, 0xdeadbeef, Backend.Outcome.Final, 1_791_250_000_000.0)), "tag"));
        }

        [Test]
        public void SignedBodies_KeepEveryOldField()
        {
            var d = Backend.Parse(Backend.ResultBody(Key, 21, 15, 0xdeadbeef, Backend.Outcome.OpponentLeft, 5_000.7));
            Assert.AreEqual(Key, Backend.Str(d, "matchKey"));
            Assert.AreEqual("deadbeef", Backend.Str(d, "hash"));
            Assert.AreEqual("opponent_left", Backend.Str(d, "outcome"));
            Assert.AreEqual(5000.0, Backend.Num(d, "t"), "whole milliseconds");
        }
    }
}
