using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Retro Hoops Live: rating, matchmaking groups, the team-swapping hand-shake, and an internet-lag game.</summary>
    public class LiveModeTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Rating_IsElo_AndStaysSane()
        {
            Assert.AreEqual(16, LiveMode.Change(1000, 1000, true));
            Assert.AreEqual(-16, LiveMode.Change(1000, 1000, false));
            Assert.Greater(LiveMode.Change(1000, 1400, true), 16, "beating a stronger player pays more");
            Assert.Less(LiveMode.Change(1400, 1000, true), 16);
            var s = new LiveSaveData();
            int d = LiveMode.Apply(s, 1200, true);
            Assert.AreEqual(1000 + d, s.rating);
            Assert.AreEqual(s.rating, s.best);
            LiveMode.Apply(s, 800, false);
            Assert.AreEqual(2, s.games);
            Assert.AreEqual(1, s.wins);
            Assert.AreEqual(1, s.losses);
            Assert.Less(s.rating, s.best);
            s.rating = 105;
            LiveMode.Apply(s, 3000, false);
            Assert.GreaterOrEqual(s.rating, 100, "a floor");
            Assert.AreEqual("ROOKIE", LiveMode.Tier(850));
            Assert.AreEqual("HOOPER", LiveMode.Tier(1000));
            Assert.AreEqual("LEGEND", LiveMode.Tier(1600));
        }

        [Test]
        public void Matchmaking_GroupsByVersion_AndBothPhonesAgreeWhoHosts()
        {
            Assert.AreEqual(LiveMode.PlayerGroup("1.0.0"), LiveMode.PlayerGroup("1.0.0"));
            Assert.AreNotEqual(LiveMode.PlayerGroup("1.0.0"), LiveMode.PlayerGroup("1.0.1"));
            Assert.Greater(LiveMode.PlayerGroup("x"), 0);
            Assert.AreEqual(0, LiveMode.Seat("A:123", "B:456"));
            Assert.AreEqual(1, LiveMode.Seat("B:456", "A:123"));
            Assert.AreEqual(1 - LiveMode.Seat("p1", "p2"), LiveMode.Seat("p2", "p1"));
        }

        [Test]
        public void Subscription_CacheAndTerms()
        {
            var s = new LiveSaveData { subscribedUntil = 2000 };
            Assert.IsTrue(LiveMode.CachedActive(s, 1999));
            Assert.IsFalse(LiveMode.CachedActive(s, 2001));
            Assert.IsFalse(LiveMode.CachedActive(null, 0));
            string t = LiveMode.Terms("£9.99");
            StringAssert.Contains("£9.99", t);
            StringAssert.Contains("auto", t);
            StringAssert.Contains("24 hours", t);
            StringAssert.Contains(LiveMode.PriceFallback, LiveMode.Terms(null));
        }

        [Test]
        public void LiveSave_RoundTrips()
        {
            var c = DefaultContent.Create();
            var career = Career.New(c);
            career.live.rating = 1234;
            career.live.best = 1300;
            career.live.wins = 5;
            career.live.losses = 3;
            career.live.games = 8;
            career.live.teamId = c.TeamsInTier(TeamTier.League)[2].id;
            career.live.subscribedUntil = 1790000000.5;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), c, out _);
            Assert.AreEqual(1234, back.live.rating);
            Assert.AreEqual(1300, back.live.best);
            Assert.AreEqual(8, back.live.games);
            Assert.AreEqual(career.live.teamId, back.live.teamId);
            Assert.AreEqual(1790000000.5, back.live.subscribedUntil, 0.01);
            // The best rating is on the leaderboard once you've played.
            bool found = false;
            foreach (var kv in Achievements.Scores(career)) if (kv.Key == LiveMode.LeaderboardId) { found = true; Assert.AreEqual(1300, kv.Value); }
            Assert.IsTrue(found);
        }

        private static LiveOffer Offer(ContentCatalog c, int team, string name, int rating, string version = "1.0.0")
        {
            var t = c.TeamsInTier(TeamTier.League)[team];
            return new LiveOffer { AppVersion = version, Name = name, Rating = rating, TeamData = LinkTeams.Write(c, t), CourtId = t.homeCourtId };
        }

        [Test]
        public void LiveLobby_EachPlayerBringsTheirOwnTeam()
        {
            var hostCat = DefaultContent.Create();
            var guestCat = DefaultContent.Create();
            MemoryTransport.Pair(out var a, out var b);
            var host = new LinkLobby(true, Offer(hostCat, 0, "Omari", 1100), 55, LinkProtocol.ContentFingerprint(hostCat), hostCat);
            var guest = new LinkLobby(false, Offer(guestCat, 3, "Sam", 950), 0, LinkProtocol.ContentFingerprint(guestCat), guestCat);
            for (int i = 0; i < 10; i++) { host.Update(a); guest.Update(b); }
            Assert.AreEqual(LobbyStatus.Started, host.Status);
            Assert.AreEqual(LobbyStatus.Started, guest.Status);
            var s = guest.Setup;
            Assert.IsTrue(s.IsLive);
            Assert.AreEqual(1100, s.HostRating);
            Assert.AreEqual(950, s.GuestRating);
            Assert.AreEqual("Sam", s.GuestName);
            Assert.AreEqual(LiveMode.InputDelay, s.Delay);
            Assert.AreEqual(55u, s.Seed);
            Assert.IsTrue(s.Register(guestCat));
            Assert.AreEqual(hostCat.TeamsInTier(TeamTier.League)[0].nickname, guestCat.Team(LinkSetup.HomeId).nickname);
            Assert.AreEqual(hostCat.TeamsInTier(TeamTier.League)[3].nickname, guestCat.Team(LinkSetup.AwayId).nickname);
            Assert.AreEqual(LiveMode.DifficultyId, s.ToRequest().DifficultyId);

            // Different versions never play (Game Center groups them apart too).
            MemoryTransport.Pair(out a, out b);
            host = new LinkLobby(true, Offer(hostCat, 0, "Omari", 1100), 55, 1, hostCat);
            guest = new LinkLobby(false, Offer(guestCat, 3, "Sam", 950, "0.9.0"), 0, 1, guestCat);
            for (int i = 0; i < 10; i++) { host.Update(a); guest.Update(b); }
            Assert.AreEqual(LobbyStatus.Rejected, host.Status);
            Assert.AreEqual(LobbyStatus.Rejected, guest.Status);
            StringAssert.Contains("different versions", guest.Error);
        }

        [Test]
        public void LiveGame_OverInternetLag_StaysInSync()
        {
            var hostCat = DefaultContent.Create();
            MemoryTransport.Pair(out var a, out var b, latencyTicks: 7); // ~117 ms each way
            var hostLobby = new LinkLobby(true, Offer(hostCat, 1, "H", 1000), 99, LinkProtocol.ContentFingerprint(hostCat), hostCat);
            var guestCat = DefaultContent.Create();
            var guestLobby = new LinkLobby(false, Offer(guestCat, 4, "G", 1000), 0, LinkProtocol.ContentFingerprint(guestCat), guestCat);
            for (int i = 0; i < 60 && (hostLobby.Status != LobbyStatus.Started || guestLobby.Status != LobbyStatus.Started); i++)
            {
                a.Tick(); b.Tick();
                hostLobby.Update(a); guestLobby.Update(b);
            }
            Assert.AreEqual(LobbyStatus.Started, guestLobby.Status);
            var setups = new[] { hostLobby.Setup, guestLobby.Setup };
            var cats = new[] { hostCat, guestCat };
            var wires = new[] { a, b };
            var sims = new MatchSimulation[2];
            var locks = new Lockstep[2];
            var rngs = new[] { new SeededRandom(1), new SeededRandom(2) };
            for (int p = 0; p < 2; p++)
            {
                Assert.IsTrue(setups[p].Register(cats[p]));
                sims[p] = new MatchSimulation(MatchSetup.FromRequest(setups[p].ToRequest(), cats[p]));
                locks[p] = new Lockstep(p, setups[p].Delay);
            }
            var frames = new SeededRandom(77);
            int stalls = 0;
            for (int f = 0; f < 60 * 60 * 12 && (!sims[0].IsOver || !sims[1].IsOver); f++)
            {
                for (int p = 0; p < 2; p++)
                {
                    wires[p].Tick();
                    int n = frames.Range(0, 3);
                    for (int i = 0; i < n && locks[p].CanQueueLocal; i++)
                        locks[p].QueueLocal(new PlayerInput { Move = new Vec2(rngs[p].Range(-10, 11) / 10f, rngs[p].Range(-10, 11) / 10f), ShootHeld = rngs[p].Range(0, 2) == 0, ShootPressed = rngs[p].Range(0, 40) == 0, PassPressed = rngs[p].Range(0, 60) == 0 });
                    foreach (var m in locks[p].TakeOutgoing()) wires[p].Send(m);
                    while (wires[p].TryReceive(out var m)) locks[p].Receive(m);
                    bool stepped = false;
                    while (!sims[p].IsOver && locks[p].TryStep(out var ia, out var ib))
                    {
                        int tick = locks[p].NextTick - 1;
                        sims[p].Step(Dt, ia, ib);
                        locks[p].AfterStep(tick, SimHash.Of(sims[p]));
                        stepped = true;
                    }
                    if (!stepped && !sims[p].IsOver) stalls++;
                }
            }
            Assert.IsTrue(sims[0].IsOver && sims[1].IsOver);
            Assert.IsFalse(locks[0].Desynced || locks[1].Desynced);
            Assert.AreEqual(SimHash.Of(sims[0]), SimHash.Of(sims[1]));
            Assert.AreEqual(sims[0].Winner, sims[1].Winner);
        }
    }

    public class TwoPhoneRematchTests
    {
        [Test]
        public void Rematch_HandshakeArrivingMidGame_IsKept_AndTheNextGamesInputsAreNotLost()
        {
            var c = DefaultContent.Create();
            var league = c.TeamsInTier(TeamTier.League);
            var first = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, null, 5, "1.0.0", "Host");
            MemoryTransport.Pair(out var a, out var b);
            var hostLock = new Lockstep(0);
            var guestLock = new Lockstep(1);

            // The host finished and tapped REMATCH while the guest is still playing out the last steps.
            var again = first.Again(77);
            Assert.AreEqual(77u, again.Seed);
            Assert.AreEqual(first.HomeTeamData, again.HomeTeamData);
            var hostLobby = new LinkLobby(true, again, "1.0.0", LinkProtocol.ContentFingerprint(c), c);
            hostLobby.Update(new ReplayWire(a, hostLock.Control));
            b.Send(LinkProtocol.Inputs(0, new List<PlayerInput> { default }, 0, 1)); // stray old-game input from the guest side
            while (b.TryReceive(out var m)) guestLock.Receive(m);
            Assert.AreEqual(1, guestLock.Control.Count, "the Setup was kept, not dropped");

            // The guest taps REMATCH: its lobby reads the saved Setup first.
            var guestLobby = new LinkLobby(false, null, "1.0.0", LinkProtocol.ContentFingerprint(c), c);
            var guestWire = new ReplayWire(b, guestLock.Control);
            var hostWire = new ReplayWire(a, hostLock.Control);
            for (int i = 0; i < 6; i++) { guestLobby.Update(guestWire); hostLobby.Update(hostWire); }
            Assert.AreEqual(LobbyStatus.Started, hostLobby.Status);
            Assert.AreEqual(LobbyStatus.Started, guestLobby.Status);
            Assert.AreEqual(77u, guestLobby.Setup.Seed);

            // The host's new game sends input at once; it's still waiting on the wire for the guest's new game.
            var newHost = new Lockstep(0);
            newHost.QueueLocal(new PlayerInput { ShootHeld = true });
            foreach (var m in newHost.TakeOutgoing()) a.Send(m);
            var newGuest = new Lockstep(1);
            int got = 0;
            while (b.TryReceive(out var m)) { newGuest.Receive(m); got++; }
            Assert.AreEqual(1, got);
            for (int t = 0; t < newGuest.Delay; t++) Assert.IsTrue(newGuest.TryStep(out _, out _));
            newGuest.QueueLocal(default);
            Assert.IsTrue(newGuest.TryStep(out var ia, out _), "the host's first new-game input arrived");
            Assert.IsTrue(ia.ShootHeld);
        }
    }
}

