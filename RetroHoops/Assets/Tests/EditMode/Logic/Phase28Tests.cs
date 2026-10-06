using System;
using System.Collections.Generic;
using System.Text;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 28: backend security — save integrity, session nonces, and rating guard.</summary>
    public class SaveGuardTests
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("test-key-32-bytes-for-unit-tests!");
        private static readonly byte[] OtherKey = Encoding.UTF8.GetBytes("different-key-not-the-same-value!");

        [Test]
        public void Sign_And_Verify_RoundTrip()
        {
            string payload = "{\"version\":1,\"nickname\":\"Rook\"}";
            string envelope = SaveGuard.Sign(payload, Key);
            string back = SaveGuard.Verify(envelope, Key);
            Assert.AreEqual(payload, back);
        }

        [Test]
        public void TamperedPayload_IsRejected()
        {
            string envelope = SaveGuard.Sign("{\"rating\":1000}", Key);
            // Edit the rating inside the envelope without touching the tag.
            string tampered = envelope.Replace("1000", "4000");
            Assert.IsNull(SaveGuard.Verify(tampered, Key));
        }

        [Test]
        public void TamperedTag_IsRejected()
        {
            string envelope = SaveGuard.Sign("hello", Key);
            // Flip the first character of the hex tag.
            char flip = envelope[0] == 'a' ? 'b' : 'a';
            string tampered = flip + envelope.Substring(1);
            Assert.IsNull(SaveGuard.Verify(tampered, Key));
        }

        [Test]
        public void WrongKey_IsRejected()
        {
            string envelope = SaveGuard.Sign("secret", Key);
            Assert.IsNull(SaveGuard.Verify(envelope, OtherKey));
        }

        [Test]
        public void NullAndEmpty_AreSafe()
        {
            Assert.IsNull(SaveGuard.Verify(null, Key));
            Assert.IsNull(SaveGuard.Verify("", Key));
            Assert.IsNull(SaveGuard.Verify("shortstring", Key));
            Assert.IsFalse(SaveGuard.IsSigned(null));
            Assert.IsFalse(SaveGuard.IsSigned("plain json"));
        }

        [Test]
        public void IsSigned_DetectsEnvelopes()
        {
            string envelope = SaveGuard.Sign("{}", Key);
            Assert.IsTrue(SaveGuard.IsSigned(envelope));
            Assert.IsFalse(SaveGuard.IsSigned("{}"));
            // A real save JSON starts with '{', not a 64-char hex line.
            string plainSave = SaveCodec.Encode(Career.New(DefaultContent.Create()));
            Assert.IsFalse(SaveGuard.IsSigned(plainSave));
        }

        [Test]
        public void SignedSave_TamperFallsBackToRecovered()
        {
            var c = DefaultContent.Create();
            var career = Career.New(c);
            career.totals.games = 42;

            string json = SaveCodec.Encode(career);
            string envelope = SaveGuard.Sign(json, Key);

            // Tamper: change the games count in the envelope's payload.
            string tampered = envelope.Replace("\"games\":42", "\"games\":9999");

            // The platform layer: verify → null → feed empty string to Decode.
            string payload = SaveGuard.Verify(tampered, Key) ?? "";
            var loaded = SaveCodec.Decode(payload, c, out LoadStatus status);
            Assert.AreEqual(LoadStatus.Recovered, status);
            Assert.AreEqual(0, loaded.totals.games, "tampered data not used");
        }

        [Test]
        public void ValidSave_LoadsNormally_AfterVerify()
        {
            var c = DefaultContent.Create();
            var career = Career.New(c);
            career.totals.games = 7;

            string json = SaveCodec.Encode(career);
            string envelope = SaveGuard.Sign(json, Key);

            string payload = SaveGuard.Verify(envelope, Key);
            Assert.IsNotNull(payload);
            var loaded = SaveCodec.Decode(payload, c, out LoadStatus status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(7, loaded.totals.games);
        }
    }

    public class RatingGuardTests
    {
        [Test]
        public void DeltaIsPlausible_WithinKFactor()
        {
            // Win against equal: +16
            Assert.IsTrue(LiveMode.DeltaIsPlausible(1000, 1016));
            // Lose against equal: -16
            Assert.IsTrue(LiveMode.DeltaIsPlausible(1000, 984));
            // Maximum gain (big upset win): +32
            Assert.IsTrue(LiveMode.DeltaIsPlausible(1000, 1032));
            // Maximum loss: -32
            Assert.IsTrue(LiveMode.DeltaIsPlausible(1000, 968));
            // Floor clamping: losing at rating 105 → 100 is valid
            Assert.IsTrue(LiveMode.DeltaIsPlausible(105, 100));
        }

        [Test]
        public void DeltaIsPlausible_ExcessiveDelta_IsFlagged()
        {
            // A jump of +100 can't come from a single game.
            Assert.IsFalse(LiveMode.DeltaIsPlausible(1000, 1100));
            Assert.IsFalse(LiveMode.DeltaIsPlausible(1000, 900));
            // Jumping to max rating in one game.
            Assert.IsFalse(LiveMode.DeltaIsPlausible(1000, 4000));
            // Going negative.
            Assert.IsFalse(LiveMode.DeltaIsPlausible(1000, 50));
        }

        [Test]
        public void Sanitize_ClampsRatingAndBest()
        {
            var bad = new LiveSaveData { rating = 9999, best = 1, wins = 3, losses = 1, games = 4 };
            var clean = LiveMode.Sanitize(bad);
            Assert.AreEqual(4000, clean.rating, "rating clamped to max");
            Assert.AreEqual(4000, clean.best, "best follows rating when below");
            Assert.AreEqual(4, clean.games);

            var low = new LiveSaveData { rating = -5, best = 2000, wins = 0, losses = 0, games = 0 };
            clean = LiveMode.Sanitize(low);
            Assert.AreEqual(100, clean.rating, "floor at 100");
            Assert.AreEqual(2000, clean.best);
        }

        [Test]
        public void Sanitize_FixesImpossibleWinLossCount()
        {
            // wins + losses > games is impossible.
            var bad = new LiveSaveData { rating = 1000, best = 1000, wins = 50, losses = 50, games = 10 };
            var clean = LiveMode.Sanitize(bad);
            Assert.GreaterOrEqual(clean.games, clean.wins + clean.losses);
            Assert.AreEqual(50, clean.wins);
            Assert.AreEqual(50, clean.losses);
            Assert.AreEqual(100, clean.games);
        }

        [Test]
        public void Sanitize_NullReturnsDefault()
        {
            var clean = LiveMode.Sanitize(null);
            Assert.IsNotNull(clean);
            Assert.AreEqual(LiveMode.StartRating, clean.rating);
        }

        [Test]
        public void Apply_ProducesPlausibleDeltas()
        {
            var s = new LiveSaveData();
            int before = s.rating;
            LiveMode.Apply(s, 1000, true);
            Assert.IsTrue(LiveMode.DeltaIsPlausible(before, s.rating),
                "Apply itself must produce plausible deltas (self-consistency check)");
            before = s.rating;
            LiveMode.Apply(s, 200, false);
            Assert.IsTrue(LiveMode.DeltaIsPlausible(before, s.rating));
        }
    }

    public class LinkSessionTests
    {
        [Test]
        public void Session_SurvivesWireRoundTrip()
        {
            var c = DefaultContent.Create();
            var league = c.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, null, 5, "1.0.0", "Host");
            Assert.AreNotEqual(0u, s.Session, "From() generates a non-zero session");
            var back = LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(s));
            Assert.AreEqual(s.Session, back.Session, "session survives encode/decode");
        }

        [Test]
        public void Session_DefaultsToZero_InLegacyMessages()
        {
            // A Setup message built without the session= key (old version) decodes to Session = 0.
            var c = DefaultContent.Create();
            var league = c.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, null, 7, "1.0.0", "Old");
            byte[] msg = LinkProtocol.SetupMessage(s);
            // Strip the session line from the wire message.
            string text = System.Text.Encoding.UTF8.GetString(msg, 1, msg.Length - 1);
            string stripped = System.Text.RegularExpressions.Regex.Replace(text, @"session=\d+\n", "");
            byte[] legacyMsg = new byte[1 + System.Text.Encoding.UTF8.GetByteCount(stripped)];
            legacyMsg[0] = msg[0];
            System.Text.Encoding.UTF8.GetBytes(stripped, 0, stripped.Length, legacyMsg, 1);
            var back = LinkProtocol.ReadSetup(legacyMsg);
            Assert.AreEqual(0u, back.Session, "missing session key → 0 (legacy compat)");
            Assert.AreEqual(7u, back.Seed, "other fields still decoded correctly");
        }

        [Test]
        public void Session_IsNonZero_ForEveryNewFrom()
        {
            var c = DefaultContent.Create();
            var league = c.TeamsInTier(TeamTier.League);
            var sessions = new System.Collections.Generic.HashSet<uint>();
            for (int i = 0; i < 10; i++)
            {
                var s = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, null, (uint)i + 1, "1.0.0", "H");
                Assert.AreNotEqual(0u, s.Session);
                sessions.Add(s.Session);
            }
            // All 10 sessions should be distinct (ticks-based, nanosecond resolution in a loop).
            Assert.Greater(sessions.Count, 1, "sessions vary between calls");
        }

        [Test]
        public void LiveLobby_Session_IsNonZero_AfterHandshake()
        {
            var hostCat = DefaultContent.Create();
            var guestCat = DefaultContent.Create();
            MemoryTransport.Pair(out var a, out var b);
            var offer = new LiveOffer { AppVersion = "1.0.0", Name = "H", Rating = 1000, TeamData = LinkTeams.Write(hostCat, hostCat.TeamsInTier(TeamTier.League)[0]), CourtId = hostCat.TeamsInTier(TeamTier.League)[0].homeCourtId };
            var host = new LinkLobby(true, offer, 1, LinkProtocol.ContentFingerprint(hostCat), hostCat);
            var guestOffer = new LiveOffer { AppVersion = "1.0.0", Name = "G", Rating = 900, TeamData = LinkTeams.Write(guestCat, guestCat.TeamsInTier(TeamTier.League)[1]), CourtId = guestCat.TeamsInTier(TeamTier.League)[1].homeCourtId };
            var guest = new LinkLobby(false, guestOffer, 0, LinkProtocol.ContentFingerprint(guestCat), guestCat);
            for (int i = 0; i < 10; i++) { host.Update(a); guest.Update(b); }
            Assert.AreEqual(LobbyStatus.Started, host.Status);
            Assert.AreNotEqual(0u, host.Setup.Session, "Live lobby also generates a session");
            Assert.AreEqual(host.Setup.Session, guest.Setup.Session, "both phones share the same session");
        }
    }
}
