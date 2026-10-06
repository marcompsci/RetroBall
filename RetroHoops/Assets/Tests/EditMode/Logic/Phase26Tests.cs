using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Two-phone link play: wire format, hand-shake, and lockstep keeping both games identical.</summary>
    public class LinkTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Inputs_RoundTripThroughFourBytes()
        {
            var i = new PlayerInput { Move = new Vec2(0.5f, -1f), ShootHeld = true, PassPressed = true, DunkPressed = true, CallPlay = PlayCall.GiveAndGo };
            var q = LinkProtocol.Quantize(i);
            Assert.AreEqual(0.5f, q.Move.x, 0.01f);
            Assert.AreEqual(-1f, q.Move.y, 1e-6f);
            Assert.IsTrue(q.ShootHeld && q.PassPressed && q.DunkPressed);
            Assert.IsFalse(q.ShootPressed || q.DefensePressed || q.LayupPressed);
            Assert.AreEqual(PlayCall.GiveAndGo, q.CallPlay);
            // Quantizing twice changes nothing (both phones simulate exactly these numbers).
            var qq = LinkProtocol.Quantize(q);
            Assert.AreEqual(q.Move.x, qq.Move.x);
            Assert.AreEqual(q.Move.y, qq.Move.y);
            // Out-of-range and NaN sticks are safe.
            var wild = LinkProtocol.Quantize(new PlayerInput { Move = new Vec2(float.NaN, 9f) });
            Assert.AreEqual(0f, wild.Move.x);
            Assert.AreEqual(1f, wild.Move.y);
        }

        [Test]
        public void InputsMessage_RoundTrips()
        {
            var list = new List<PlayerInput> { new PlayerInput { ShootPressed = true }, new PlayerInput { Move = new Vec2(-0.3f, 0.2f) } };
            var m = LinkProtocol.Inputs(1234, list, 0, 2);
            var back = new List<PlayerInput>();
            Assert.IsTrue(LinkProtocol.ReadInputs(m, out int first, back));
            Assert.AreEqual(1234, first);
            Assert.AreEqual(2, back.Count);
            Assert.IsTrue(back[0].ShootPressed);
            Assert.AreEqual(-0.3f, back[1].Move.x, 0.01f);
            Assert.IsFalse(LinkProtocol.ReadInputs(new byte[] { 5, 0 }, out _, back), "short messages are ignored");
        }

        [Test]
        public void Setup_CarriesTheHostsExactTeams()
        {
            var host = DefaultContent.Create();
            var league = host.TeamsInTier(TeamTier.League);
            // The host's career has changed a rating (Rise dynasty): the guest must get the host's numbers.
            var star = host.Player(league[0].rosterPlayerIds[0]);
            star.attributes = star.attributes.Offset(5);
            var s = LinkSetup.From(host, league[0].id, league[1].id, league[0].homeCourtId, DefaultContent.DefaultDifficultyId, 77, "1.0.0", "Omari's iPhone");
            var back = LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(s));
            Assert.AreEqual(77u, back.Seed);
            Assert.AreEqual("Omari's iPhone", back.HostName);

            var guest = DefaultContent.Create();
            Assert.IsNull(LinkProtocol.Incompatible(back, "1.0.0", LinkProtocol.ContentFingerprint(guest), guest));
            Assert.IsTrue(back.Register(guest));
            var team = guest.Team(LinkSetup.HomeId);
            Assert.AreEqual(league[0].nickname, team.nickname);
            Assert.AreEqual(league[0].rosterPlayerIds.Count, team.rosterPlayerIds.Count);
            Assert.AreEqual(star.attributes.Overall, guest.Player(team.rosterPlayerIds[0]).attributes.Overall);
            Assert.AreEqual(star.appearance.hairStyle, guest.Player(team.rosterPlayerIds[0]).appearance.hairStyle);
            // Registering again replaces the old copy instead of piling up.
            int teams = guest.Teams.Count;
            Assert.IsTrue(back.Register(guest));
            Assert.AreEqual(teams, guest.Teams.Count);
        }

        [Test]
        public void Setup_RejectsDifferentVersionsAndBadData()
        {
            var c = DefaultContent.Create();
            var league = c.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, null, 1, "1.0.0", "Host");
            uint fp = LinkProtocol.ContentFingerprint(c);
            StringAssert.Contains("same version", LinkProtocol.Incompatible(s, "1.0.1", fp, c));
            Assert.IsNotNull(LinkProtocol.Incompatible(s, "1.0.0", fp + 1, c));
            s.CourtId = "court.nowhere";
            Assert.IsNotNull(LinkProtocol.Incompatible(s, "1.0.0", fp, c));
            s.CourtId = league[0].homeCourtId;
            s.AwayTeamData = "garbage|x";
            Assert.IsNotNull(LinkProtocol.Incompatible(s, "1.0.0", fp, c));
            Assert.IsNotNull(LinkProtocol.Incompatible(null, "1.0.0", fp, c));
        }

        [Test]
        public void Lobby_HandshakeStartsBothPhones_OrExplainsWhyNot()
        {
            var host = DefaultContent.Create();
            var guest = DefaultContent.Create();
            var league = host.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(host, league[0].id, league[1].id, league[0].homeCourtId, null, 9, "1.0.0", "Host");
            MemoryTransport.Pair(out var a, out var b);
            var hl = new LinkLobby(true, s, "1.0.0", LinkProtocol.ContentFingerprint(host), host);
            var gl = new LinkLobby(false, null, "1.0.0", LinkProtocol.ContentFingerprint(guest), guest);
            for (int i = 0; i < 10; i++) { hl.Update(a); gl.Update(b); }
            Assert.AreEqual(LobbyStatus.Started, hl.Status);
            Assert.AreEqual(LobbyStatus.Started, gl.Status);
            Assert.AreEqual(9u, gl.Setup.Seed);

            // An older guest is told why, and so is the host.
            MemoryTransport.Pair(out a, out b);
            hl = new LinkLobby(true, s, "1.0.0", LinkProtocol.ContentFingerprint(host), host);
            gl = new LinkLobby(false, null, "0.9.0", LinkProtocol.ContentFingerprint(guest), guest);
            for (int i = 0; i < 10; i++) { hl.Update(a); gl.Update(b); }
            Assert.AreEqual(LobbyStatus.Rejected, gl.Status);
            Assert.AreEqual(LobbyStatus.Rejected, hl.Status);
            StringAssert.Contains("same version", hl.Error);

            // A dropped connection ends the wait.
            MemoryTransport.Pair(out a, out b);
            hl = new LinkLobby(true, s, "1.0.0", 1, host);
            a.State = LinkState.Lost;
            hl.Update(a);
            Assert.AreEqual(LobbyStatus.Lost, hl.Status);
        }

        private sealed class Phone
        {
            public MatchSimulation Match;
            public Lockstep Lock;
            public MemoryTransport Wire;
            public SeededRandom Rng;
        }

        private static Phone MakePhone(LinkSetup s, int seat, MemoryTransport wire, uint inputSeed)
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(s.Register(c));
            return new Phone
            {
                Match = new MatchSimulation(MatchSetup.FromRequest(s.ToRequest(), c)),
                Lock = new Lockstep(seat),
                Wire = wire,
                Rng = new SeededRandom(inputSeed),
            };
        }

        private static PlayerInput RandomInput(SeededRandom r)
        {
            return new PlayerInput
            {
                Move = new Vec2(r.Range(-100, 101) / 100f, r.Range(-100, 101) / 100f),
                ShootPressed = r.Range(0, 40) == 0,
                ShootHeld = r.Range(0, 3) == 0,
                PassPressed = r.Range(0, 50) == 0,
                DefensePressed = r.Range(0, 30) == 0,
                DunkPressed = r.Range(0, 200) == 0,
                LayupPressed = r.Range(0, 200) == 0,
            };
        }

        /// <summary>One screen frame on one phone: schedule some local steps, swap messages, simulate what's ready.</summary>
        private static void Frame(Phone p, int localSteps)
        {
            p.Wire.Tick();
            for (int i = 0; i < localSteps && p.Lock.CanQueueLocal; i++) p.Lock.QueueLocal(RandomInput(p.Rng));
            foreach (var m in p.Lock.TakeOutgoing()) p.Wire.Send(m);
            while (p.Wire.TryReceive(out var m)) p.Lock.Receive(m);
            int guard = 0;
            while (guard++ < 64 && !p.Match.IsOver && p.Lock.TryStep(out var a, out var b))
            {
                int tick = p.Lock.NextTick - 1;
                p.Match.Step(Dt, a, b);
                p.Lock.AfterStep(tick, SimHash.Of(p.Match));
            }
        }

        [Test]
        public void TwoPhones_PlayTheSameGame_WithLagAndUnevenFrames()
        {
            var host = DefaultContent.Create();
            var league = host.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(host, league[2].id, league[3].id, league[2].homeCourtId, DefaultContent.DefaultDifficultyId, 4242, "1.0.0", "Host");
            MemoryTransport.Pair(out var wa, out var wb, latencyTicks: 3);
            var p1 = MakePhone(s, 0, wa, 11);
            var p2 = MakePhone(s, 1, wb, 22);
            var frames = new SeededRandom(5);
            int f = 0;
            while ((!p1.Match.IsOver || !p2.Match.IsOver) && f++ < 60 * 60 * 12)
            {
                // The phones' frames don't line up: sometimes one does two steps, sometimes none (a hitch).
                Frame(p1, frames.Range(0, 3));
                Frame(p2, frames.Range(0, 3));
            }
            Assert.IsTrue(p1.Match.IsOver && p2.Match.IsOver, "both games finish");
            Assert.IsFalse(p1.Lock.Desynced || p2.Lock.Desynced, "checksums always agreed");
            Assert.AreEqual(p1.Match.Score[0], p2.Match.Score[0]);
            Assert.AreEqual(p1.Match.Score[1], p2.Match.Score[1]);
            Assert.AreEqual(SimHash.Of(p1.Match), SimHash.Of(p2.Match));
            Assert.Greater(p1.Match.Score[0] + p1.Match.Score[1], 0, "somebody scored");
        }

        [Test]
        public void Lockstep_WaitsForTheSlowPhone_AndCatchesADesync()
        {
            var a = new Lockstep(0, 4);
            // Without the other phone's input nothing past the opening delay is simulated.
            for (int i = 0; i < 100; i++) if (a.CanQueueLocal) a.QueueLocal(default);
            Assert.IsFalse(a.CanQueueLocal, "it stops running ahead");
            Assert.AreEqual(4 + Lockstep.MaxAhead, a.NextLocalTick);
            int steps = 0;
            while (a.TryStep(out _, out _)) steps++;
            Assert.AreEqual(4, steps);
            Assert.IsTrue(a.WaitingForRemote);

            // Checksums that disagree flag the game as out of sync.
            var b = new Lockstep(1, 4);
            a.AfterStep(59, 123u);
            b.AfterStep(59, 456u);
            foreach (var m in a.TakeOutgoing()) b.Receive(m);
            Assert.IsTrue(b.Desynced);
            Assert.AreEqual(59, b.DesyncTick);
            Assert.IsFalse(b.TryStep(out _, out _));

            b.Receive(LinkProtocol.Simple(LinkMessage.Bye));
            Assert.IsTrue(b.RemoteLeft);
        }
    }

    public class FramePacingTests
    {
        [Test]
        public void Menus_DrawLess_WhenIdle_GameplayDrawsEveryFrame()
        {
            Assert.AreEqual(1, PowerPolicy.RenderInterval(120, true, 99f, false));
            Assert.AreEqual(2, PowerPolicy.RenderInterval(120, false, 0f, false), "menus: 60 fps on a 120 Hz screen");
            Assert.AreEqual(4, PowerPolicy.RenderInterval(120, false, 10f, false), "idle menus: 30 fps");
            Assert.AreEqual(1, PowerPolicy.RenderInterval(60, false, 0f, false));
            Assert.AreEqual(2, PowerPolicy.RenderInterval(60, false, 10f, false));
            Assert.AreEqual(4, PowerPolicy.RenderInterval(60, false, 10f, true), "saving power: 15 fps when idle");
            Assert.AreEqual(1, PowerPolicy.RenderInterval(0, false, 0f, false));
        }
    }

    public class CouchCupTests
    {
        private static List<string> Names(int n) { var l = new List<string>(); for (int i = 0; i < n; i++) l.Add("P" + i); return l; }
        private static List<string> Teams(ContentCatalog c, int n) { var l = new List<string>(); var t = c.TeamsInTier(TeamTier.League); for (int i = 0; i < n; i++) l.Add(t[i % t.Count].id); return l; }

        [Test]
        public void EveryFieldSize_PlaysDownToOneChampion()
        {
            var c = DefaultContent.Create();
            for (int n = 2; n <= 8; n++)
            {
                var cup = new CouchCupSaveData();
                CouchCup.Start(cup, Names(n), Teams(c, n), (uint)n * 7);
                Assert.AreEqual(n, cup.names.Count);
                int games = 0;
                CouchOutcome last = CouchOutcome.Recorded;
                while (CouchCup.NextGame(cup) != null && games < 20)
                {
                    var g = CouchCup.NextGame(cup);
                    Assert.AreNotEqual(g.a, g.b);
                    var r = CouchCup.Request(cup, c, g, null);
                    Assert.AreEqual(GameMode.Versus, r.Mode);
                    Assert.IsTrue(CouchCup.IsCouch(r.ContextId));
                    last = CouchCup.Report(cup, 11, 7 + games % 3);
                    games++;
                }
                Assert.AreEqual(n - 1, games, "a knockout of " + n + " takes " + (n - 1) + " games");
                Assert.AreEqual(CouchOutcome.Champion, last);
                Assert.GreaterOrEqual(cup.champion, 0);
                Assert.IsFalse(cup.Active);
                Assert.AreEqual(1, cup.cupsPlayed);
                Assert.AreEqual(CouchOutcome.NoGame, CouchCup.Report(cup, 1, 0));
            }
        }

        [Test]
        public void Ties_ArePlayedAgain_AndRoundsAreNamed()
        {
            var c = DefaultContent.Create();
            var cup = new CouchCupSaveData();
            CouchCup.Start(cup, new List<string> { "Ana", " ", "Bo", "Cy" }, Teams(c, 4), 3);
            Assert.IsTrue(cup.names.Contains("PLAYER 2"), "blank names get a default");
            var g = CouchCup.NextGame(cup);
            Assert.AreEqual(CouchOutcome.TieReplay, CouchCup.Report(cup, 9, 9));
            Assert.IsTrue(ReferenceEquals(g, CouchCup.NextGame(cup)));
            Assert.AreEqual("SEMIFINALS", CouchCup.RoundName(cup, 0));
            Assert.AreEqual("FINAL", CouchCup.RoundName(cup, 1));
            Assert.Throws<System.ArgumentException>(() => CouchCup.Start(new CouchCupSaveData(), Names(1), Teams(c, 1), 1));
        }

        [Test]
        public void CupAndStory_SurviveSaveAndLoad()
        {
            var c = DefaultContent.Create();
            var career = Career.New(c);
            CouchCup.Start(career.couch, Names(5), Teams(c, 5), 9);
            CouchCup.Report(career.couch, 5, 3);
            career.story.cleared = 3;
            career.story.best = new List<int> { 1, 1, 1, 0, 0, 0, 0, 0 };
            career.settings.commentary = false;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), c, out _);
            Assert.AreEqual(5, back.couch.names.Count);
            Assert.AreEqual(career.couch.games.Count, back.couch.games.Count);
            Assert.AreEqual(CouchCup.NextGame(career.couch).a, CouchCup.NextGame(back.couch).a);
            Assert.AreEqual(3, back.story.cleared);
            Assert.AreEqual(1, back.story.best[2]);
            Assert.IsFalse(back.settings.commentary);

            // A damaged cup is dropped rather than crashing the bracket.
            career.couch.games[0].a = 42;
            back = SaveCodec.Decode(SaveCodec.Encode(career), c, out _);
            Assert.IsFalse(back.couch.Active);
        }
    }

    public class StoryModeTests
    {
        [Test]
        public void EightChapters_WithScenes_CastAndRealCourts()
        {
            var c = DefaultContent.Create();
            Assert.AreEqual(StoryMode.Chapters, StoryMode.All.Length);
            for (int n = 1; n <= StoryMode.Chapters; n++)
            {
                var ch = StoryMode.Chapter(n);
                Assert.AreEqual(n, ch.Number);
                Assert.IsNotNull(c.Court(ch.CourtId), ch.CourtId);
                Assert.Greater(StoryMode.Scene(n, false, "Rook").Lines.Count, 1, "intro " + n);
                Assert.Greater(StoryMode.Scene(n, true, "Rook").Lines.Count, 1, "outro " + n);
                var team = StoryMode.Register(c, ch);
                Assert.AreEqual(ch.FullCourt ? 8 : 4, team.rosterPlayerIds.Count);
                var r = StoryMode.Request(c, Career.New(c), ch);
                Assert.IsTrue(ReferenceEquals(ch, StoryMode.FromContext(r.ContextId)));
                var setup = MatchSetup.FromRequest(r, c);
                Assert.AreEqual(ch.FullCourt, setup.FullCourt);
                StringAssert.Contains("Win", StoryMode.GoalText(ch));
            }
            foreach (var who in StoryMode.Cast) Assert.IsNotNull(StoryMode.Character(who.Speaker));
            Assert.IsNull(StoryMode.Chapter(0));
            Assert.IsNull(StoryMode.FromContext("street:slim"));
            // Registering twice doesn't add the players again.
            int players = c.Players.Count;
            StoryMode.Register(c, StoryMode.Chapter(1));
            Assert.AreEqual(players, c.Players.Count);
        }

        private static MatchSummary Summary(int us, int them, int assists = 0, int steals = 0, int ankles = 0)
        {
            var s = new MatchSummary { scoreA = us, scoreB = them, humanTeam = 0, winner = us > them ? 0 : 1 };
            s.lines.Add(new SummaryLine { team = 0, isHuman = true, stats = new PlayerStatLine { assists = assists, steals = steals, ankleBreakers = ankles, points = us } });
            s.lines.Add(new SummaryLine { team = 0, stats = new PlayerStatLine { assists = 1 } });
            s.lines.Add(new SummaryLine { team = 1, stats = new PlayerStatLine { assists = 9, steals = 9 } });
            return s;
        }

        [Test]
        public void Goals_NeedTheWinAndTheTwist_AndUnlockInOrder()
        {
            var s = new StorySaveData();
            Assert.IsTrue(StoryMode.Unlocked(s, 1));
            Assert.IsFalse(StoryMode.Unlocked(s, 2));

            var ch3 = StoryMode.Chapter(3); // win by 4
            Assert.IsFalse(StoryMode.GoalMet(ch3, Summary(15, 13)));
            Assert.IsTrue(StoryMode.GoalMet(ch3, Summary(15, 10)));
            var ch4 = StoryMode.Chapter(4); // 3 assists as a team (the other team's don't count)
            Assert.IsFalse(StoryMode.GoalMet(ch4, Summary(15, 10, assists: 1)));
            Assert.IsTrue(StoryMode.GoalMet(ch4, Summary(15, 10, assists: 2)));
            Assert.IsFalse(StoryMode.GoalMet(ch4, Summary(10, 15, assists: 5)), "every goal needs the win");
            var ch7 = StoryMode.Chapter(7);
            Assert.IsTrue(StoryMode.GoalMet(ch7, Summary(15, 10, ankles: 1)));

            Assert.AreEqual(StoryOutcome.Lost, StoryMode.ApplyResult(s, StoryMode.Chapter(1), Summary(5, 11), out int r));
            Assert.AreEqual(0, r);
            Assert.AreEqual(StoryOutcome.Cleared, StoryMode.ApplyResult(s, StoryMode.Chapter(1), Summary(11, 5), out r));
            Assert.AreEqual(StoryMode.ClearReward, r);
            Assert.IsTrue(StoryMode.Unlocked(s, 2));
            Assert.AreEqual(StoryOutcome.Cleared, StoryMode.ApplyResult(s, StoryMode.Chapter(1), Summary(11, 5), out r));
            Assert.AreEqual(0, r, "replays don't pay again");
            Assert.AreEqual(StoryOutcome.GoalMissed, StoryMode.ApplyResult(s, ch3, Summary(15, 14), out _));
            Assert.AreEqual(1, s.cleared);

            s.cleared = 7;
            Assert.AreEqual(StoryOutcome.Cleared, StoryMode.ApplyResult(s, StoryMode.Chapter(8), Summary(30, 20), out r));
            Assert.AreEqual(StoryMode.FinaleReward, r);
            Assert.IsTrue(s.finished);
        }
    }

    public class CommentaryTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Mic_CallsBigMoments_ButNotEveryStep()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 21;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            var mic = new Commentary(m.Setup.TeamA.nickname, m.Setup.TeamB.nickname, 3);
            var rng = new SeededRandom(8);
            var lines = new List<string>();
            var times = new List<float>();
            int guard = 0;
            while (!m.IsOver && guard++ < 60 * 400)
            {
                var input = new PlayerInput { Move = new Vec2(rng.Range(-10, 11) / 10f, rng.Range(-10, 11) / 10f), ShootPressed = rng.Range(0, 50) == 0, ShootHeld = rng.Range(0, 2) == 0, PassPressed = rng.Range(0, 80) == 0 };
                m.Step(Dt, input);
                string line = mic.React(m, i => m.Players[i].Def.lastName, m.Ball.ShotType);
                if (line != null) { lines.Add(line); times.Add(m.Time); }
            }
            Assert.IsTrue(m.IsOver);
            Assert.Greater(lines.Count, 0, "something worth calling happened");
            StringAssert.Contains("!", lines[lines.Count - 1]);
            for (int i = 1; i < lines.Count; i++)
            {
                Assert.AreNotEqual(lines[i - 1], lines[i], "no repeats back to back");
                if (i < lines.Count - 1) // the final-buzzer call may cut in
                    Assert.GreaterOrEqual(times[i] - times[i - 1] + 0.001f, Commentary.Gap * 0.5f, "lines are spaced out");
            }
            Assert.Less(lines.Count, m.Time / 2f, "quiet most of the time");
        }
    }
}
