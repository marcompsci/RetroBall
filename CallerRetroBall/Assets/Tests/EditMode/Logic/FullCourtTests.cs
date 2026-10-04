using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Full Court 5-on-5 and half-court winners' ball.</summary>
    public class FullCourtTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchRequest Request(GameMode mode, uint seed = 7)
        {
            var league = _c.TeamsInTier(TeamTier.League);
            return new MatchRequest { Mode = mode, HomeTeamId = league[0].id, AwayTeamId = league[1].id, CourtId = league[0].homeCourtId, Seed = seed };
        }

        private MatchSimulation FullCourtDemo(uint seed)
        {
            var setup = MatchSetup.FromRequest(Request(GameMode.FullCourt, seed), _c);
            setup.Demo = true; // every player is AI
            return new MatchSimulation(setup);
        }

        [Test]
        public void Setup_IsFiveOnFive_WithTwosAndThrees_AndReserves()
        {
            var setup = MatchSetup.FromRequest(Request(GameMode.FullCourt), _c);
            Assert.IsTrue(setup.FullCourt);
            Assert.AreEqual(5, setup.TeamSize);
            Assert.AreEqual(5, setup.RosterA.Count);
            Assert.AreEqual(5, setup.RosterB.Count);
            Assert.AreEqual(FullCourt.RulesId, setup.Rules.id);
            Assert.AreEqual(2, setup.Rules.insideArcPoints);
            Assert.AreEqual(3, setup.Rules.beyondArcPoints);
            Assert.IsFalse(setup.WinnersBall, "Full Court changes possession after a make");
            Assert.AreEqual(FullCourt.HalfLength * 2f, setup.Court.depth, 0.001f);

            var ids = new HashSet<string>();
            var numbers = new HashSet<int>();
            foreach (var p in setup.RosterA)
            {
                Assert.IsTrue(ids.Add(p.id), p.id);
                Assert.IsTrue(numbers.Add(p.jerseyNumber), "jersey " + p.jerseyNumber);
                Assert.IsNotNull(_c.ArchetypeById(p.archetypeId), p.id);
            }
            // Reserves are the same every time.
            var again = MatchSetup.FromRequest(Request(GameMode.FullCourt), _c);
            Assert.AreEqual(setup.RosterA[4].id, again.RosterA[4].id);
            Assert.AreEqual(setup.RosterA[4].DisplayName, again.RosterA[4].DisplayName);

            var m = new MatchSimulation(setup);
            Assert.AreEqual(10, m.Players.Length);
            Assert.AreEqual(5, m.Index(1, 0));
        }

        [Test]
        public void ThreePlayerCrews_GetTwoReserves()
        {
            var req = Request(GameMode.FullCourt);
            req.HomeTeamId = DefaultContent.PlayerCrewId;
            var setup = MatchSetup.FromRequest(req, _c);
            Assert.AreEqual(5, setup.RosterA.Count);
            Assert.IsTrue(setup.RosterA[3].id.StartsWith("player.res."));
        }

        [Test]
        public void HalfCourtModes_AreWinnersBall_ExceptOneOnOne()
        {
            foreach (var mode in new[] { GameMode.QuickCall, GameMode.Rise, GameMode.Versus, GameMode.King, GameMode.Cup, GameMode.Arcade })
                Assert.IsTrue(MatchSetup.FromRequest(Request(mode), _c).WinnersBall, mode.ToString());
            Assert.IsFalse(MatchSetup.FromRequest(Request(GameMode.OneOnOne), _c).WinnersBall);
            Assert.IsFalse(MatchSetup.FromRequest(Request(GameMode.FullCourt), _c).WinnersBall);
        }

        [Test]
        public void TipOff_AtHalfCourt_ThenInboundsFromTheFarBaseline()
        {
            var m = FullCourtDemo(11);
            Assert.Greater(m.Ball.Position.y, FullCourt.MidY, "tip-off: ball just inside the attacking team's backcourt");
            Assert.Less(m.Ball.Position.y, FullCourt.MidY + 3f);
            // Play until a made basket leads to the next inbound.
            bool sawInbound = false;
            for (int i = 0; i < 60 * 240 && !m.IsOver && !sawInbound; i++)
            {
                m.Step(Dt, default);
                foreach (var e in m.Events)
                    if (e.Type == MatchEventType.CheckBall && m.Time > 1f)
                    {
                        sawInbound = true;
                        Assert.Greater(m.Ball.Position.y, m.Setup.Court.depth - 3f, "inbound from the far end");
                    }
            }
            Assert.IsTrue(sawInbound);
        }

        [Test]
        public void TurningThePicture_KeepsEveryoneWhereTheyWere()
        {
            var m = FullCourtDemo(5);
            int checkedTurns = 0;
            var before = new Vec2[m.Players.Length];
            for (int step = 0; step < 60 * 240 && !m.IsOver && checkedTurns < 3; step++)
            {
                for (int i = 0; i < m.Players.Length; i++) before[i] = m.ToWorldCourt(m.Players[i].Position);
                int offense = m.OffenseTeam;
                m.Step(Dt, default);
                if (m.Phase != MatchPhase.Live || m.OffenseTeam == offense) continue;
                // Possession changed on a live ball: what you see moves by one frame at most.
                for (int i = 0; i < m.Players.Length; i++)
                    Assert.Less(Vec2.Distance(before[i], m.ToWorldCourt(m.Players[i].Position)), 0.3f, "player " + i);
                checkedTurns++;
            }
            Assert.Greater(checkedTurns, 0, "a steal or defensive rebound happened");
        }

        [Test]
        public void AiGame_PlaysToTheHorn_BothTeamsScoreAtBothEnds()
        {
            for (uint seed = 1; seed <= 3; seed++)
            {
                var m = FullCourtDemo(seed);
                float topY = float.MaxValue, bottomY = float.MinValue;
                int changes = 0;
                for (int i = 0; i < 60 * 600 && !m.IsOver; i++)
                {
                    m.Step(Dt, default);
                    var w = m.ToWorldCourt(m.Ball.Position);
                    topY = System.Math.Min(topY, w.y);
                    bottomY = System.Math.Max(bottomY, w.y);
                    foreach (var e in m.Events) if (e.Type == MatchEventType.CheckBall || e.Type == MatchEventType.PossessionChanged) changes++;
                }
                Assert.IsTrue(m.IsOver, "seed " + seed + " finished");
                Assert.Greater(m.Score[0], 0, "seed " + seed);
                Assert.Greater(m.Score[1], 0, "seed " + seed);
                Assert.Greater(changes, 10, "plenty of possessions");
                Assert.Less(topY, 3f, "the ball reached the top hoop");
                Assert.Greater(bottomY, m.Setup.Court.depth - 3f, "and the bottom hoop");
                int points = 0;
                for (int i = 0; i < m.Players.Length; i++) points += m.Stats[i].points;
                foreach (var b in m.Bench) points += b.Line.points; // subs keep their own lines
                Assert.AreEqual(m.Score[0] + m.Score[1], points);
            }
        }

        [Test]
        public void Stick_MovesYouTheSameWayOnScreen_AtBothEnds()
        {
            var setup = MatchSetup.FromRequest(Request(GameMode.FullCourt), _c);
            setup.PassiveOpponents = true;
            var m = new MatchSimulation(setup);
            int me = m.ControlledIndex;
            foreach (int offense in new[] { 0, 1 })
            {
                m.RestartWithBall(offense);
                Assert.AreEqual(offense == 1, m.Flipped);
                var start = m.ToWorldCourt(m.Players[me].Position);
                // Court-space "up the screen" is -y.
                for (int i = 0; i < 120; i++) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -1f) }); // past the inbound pause
                var end = m.ToWorldCourt(m.Players[me].Position);
                Assert.Less(end.y, start.y - 1f, "offense team " + offense);
                Assert.AreEqual(offense, m.OffenseTeam);
            }
        }

        [Test]
        public void FullCourt_PaysLikeQuickCall_AndSkipsThePointsRecord()
        {
            var d = Career.New(_c);
            var s = new MatchSummary { matchId = "fc1", mode = GameMode.FullCourt, humanTeam = 0, winner = 0, scoreA = 40, scoreB = 30 };
            s.lines.Add(new SummaryLine { playerIndex = 0, team = 0, isHuman = true, stats = new PlayerStatLine { points = 30 } });
            var quick = new MatchSummary { matchId = "q1", mode = GameMode.QuickCall, humanTeam = 0, winner = 0, scoreA = 40, scoreB = 30 };
            quick.lines.Add(new SummaryLine { playerIndex = 0, team = 0, isHuman = true, stats = new PlayerStatLine { points = 30 } });
            Assert.AreEqual(Rewards.For(quick, RewardTuning.Default).signalPoints, Rewards.For(s, RewardTuning.Default).signalPoints);
            Career.ApplyMatch(d, s, Rewards.For(s, RewardTuning.Default));
            Assert.AreEqual(0, d.records.points, "2s and 3s don't count toward the half-court points record");
            Assert.AreEqual(1, d.totals.games);
        }

        [Test]
        public void FullCourtArt_IsTwoHalves_TurnedAboutTheCentre()
        {
            var g = FullCourt.HalfGeometry();
            var court = _c.Court("court.sunset_cage");
            var art = CourtGenerator.GenerateFullCourt(court, g, 7, null, null, false);
            Assert.AreEqual(CourtGenerator.TextureWidth(g), art.Width);
            Assert.AreEqual(CourtGenerator.FullCourtTextureHeight(g), art.Height);
            CourtGenerator.FullCourtOriginPivot(g, out float px, out float py);
            Assert.AreEqual(0.5f, px, 0.01f);
            // The top baseline is 2 × half length above the bottom one.
            float baselineRow = py * art.Height;
            Assert.AreEqual(baselineRow - FullCourt.HalfLength * 2f * CourtGenerator.PixelsPerMeter, art.Height - baselineRow, 2f);
            // Court lines are symmetric under the 180° turn (sampled away from the stands).
            var line = court.lines;
            int matches = 0, lines = 0;
            for (int y = (int)(art.Height * 0.2f); y < art.Height * 0.8f; y++)
                for (int x = 2; x < art.Width - 2; x++)
                {
                    if (!art.Get(x, y).Equals(line)) continue;
                    lines++;
                    if (art.Get(art.Width - x, art.Height - y).Equals(line)) matches++;
                }
            Assert.Greater(lines, 200);
            Assert.Greater(matches, lines * 0.95f);
        }
    }
}