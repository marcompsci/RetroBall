using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 35 (this session's half): shot charts in play and in saves, and the AI hunting its hot spots.</summary>
    public class Phase35ShotChartTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        private static MatchSimulation Play(uint seed, GameMode mode = GameMode.Demo)
        {
            var r = MatchRequest.QuickCallDefault(C);
            r.Mode = mode;
            r.Seed = seed;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
            int g = 0;
            while (!m.IsOver && g++ < 60 * 600) m.Step(1f / 60f, default);
            return m;
        }

        [Test]
        public void EverySpotsCourtPoint_IsInsideThatSpot()
        {
            foreach (var court in new[] { CourtGeometry.Default, FullCourt.Geometry() })
                for (int i = 0; i < ShotZones.SpotCount; i++)
                {
                    var spot = (ShotSpot)i;
                    var p = ShotCharts.CourtPoint(spot, court);
                    Assert.AreEqual(spot, ShotZones.SpotOf(p, court), spot + " at " + p.x + "," + p.y);
                    Assert.IsTrue(court.Contains(p), spot + " is on the floor");
                }
        }

        [Test]
        public void TheChart_AddsUpToTheBoxScore()
        {
            int shots = 0;
            for (uint s = 3; s <= 5; s++)
            {
                var m = Play(s * 101);
                for (int i = 0; i < m.Players.Length; i++)
                {
                    var line = m.Stats[i];
                    Assert.AreEqual(line.fieldGoalsAttempted, ShotCharts.Attempts(line.chart), "attempts, player " + i);
                    Assert.AreEqual(line.fieldGoalsMade, ShotCharts.Made(line.chart), "makes, player " + i);
                    foreach (var r in line.chart.spots) Assert.LessOrEqual(r.made, r.attempted);
                    shots += line.fieldGoalsAttempted;
                }
            }
            Assert.Greater(shots, 20, "the games had shots in them");
        }

        [Test]
        public void TeamTotals_MergeTheCharts()
        {
            var m = Play(707);
            var s = MatchSummary.From(m, GameMode.QuickCall, "x");
            var team = s.TeamTotals(0);
            Assert.AreEqual(team.fieldGoalsAttempted, ShotCharts.Attempts(team.chart));
            Assert.AreEqual(team.fieldGoalsMade, ShotCharts.Made(team.chart));
        }

        [Test]
        public void Encode_RoundTrips_AndDamageReadsAsZero()
        {
            var d = new ShotChartData();
            ShotZones.Record(d, ShotSpot.WingLeft, true);
            ShotZones.Record(d, ShotSpot.WingLeft, false);
            ShotZones.Record(d, ShotSpot.Paint, true);
            var back = ShotCharts.Decode(ShotCharts.Encode(d));
            for (int i = 0; i < ShotZones.SpotCount; i++)
            {
                Assert.AreEqual(d.spots[i].made, back.spots[i].made);
                Assert.AreEqual(d.spots[i].attempted, back.spots[i].attempted);
            }
            Assert.AreEqual("", ShotCharts.Encode(new ShotChartData()));
            Assert.AreEqual(0, ShotCharts.Attempts(ShotCharts.Decode("garbage;;x/y")));
            var clamped = ShotCharts.Decode("9/4;-3/2");
            Assert.AreEqual(4, clamped.spots[0].made, "more makes than shots is pulled back");
            Assert.AreEqual(0, clamped.spots[1].made);
        }

        [Test]
        public void Heat_ComparesWithWhatASpotUsuallyGives()
        {
            var d = new ShotChartData();
            for (int i = 0; i < 5; i++) ShotZones.Record(d, ShotSpot.CornerLeft, i < 3);   // 60% from the corner: hot
            for (int i = 0; i < 5; i++) ShotZones.Record(d, ShotSpot.Paint, i < 1);        // 20% at the rim: cold
            for (int i = 0; i < 4; i++) ShotZones.Record(d, ShotSpot.FoulLine, i < 2);     // 50% mid: about usual
            ShotZones.Record(d, ShotSpot.TopOfKey, true);                                   // 1 shot: can't say
            Assert.AreEqual(SpotHeat.Hot, ShotCharts.Heat(d, ShotSpot.CornerLeft));
            Assert.AreEqual(SpotHeat.Cold, ShotCharts.Heat(d, ShotSpot.Paint));
            Assert.AreEqual(SpotHeat.Neutral, ShotCharts.Heat(d, ShotSpot.FoulLine));
            Assert.AreEqual(SpotHeat.Unknown, ShotCharts.Heat(d, ShotSpot.TopOfKey));
            StringAssert.Contains("HOT: CORNER LEFT 3/5", ShotCharts.Summary(d));
            StringAssert.Contains("COLD: PAINT 1/5", ShotCharts.Summary(d));
            Assert.Greater(ShotCharts.AiShootBias(d, ShotSpot.CornerLeft), 0f);
            Assert.Less(ShotCharts.AiShootBias(d, ShotSpot.Paint), 0f);
            Assert.AreEqual(0f, ShotCharts.AiShootBias(d, ShotSpot.TopOfKey));
            Assert.AreEqual(ShotSpot.CornerLeft, ShotCharts.HuntSpot(d));
            Assert.AreEqual("NO SHOTS YET", ShotCharts.Summary(new ShotChartData()));
        }

        [Test]
        public void HuntSpot_LeavesThePaintToTheBigs()
        {
            var d = new ShotChartData();
            for (int i = 0; i < 5; i++) ShotZones.Record(d, ShotSpot.Paint, true);
            Assert.IsNull(ShotCharts.HuntSpot(d));
        }

        [Test]
        public void SeededGames_StillPlayTheSameEveryTime()
        {
            var a = Play(4242, GameMode.QuickCall);
            var b = Play(4242, GameMode.QuickCall);
            Assert.AreEqual(a.Score[0], b.Score[0]);
            Assert.AreEqual(a.Score[1], b.Score[1]);
            Assert.AreEqual(ShotCharts.Encode(a.Stats[1].chart), ShotCharts.Encode(b.Stats[1].chart));
        }

        [Test]
        public void Career_KeepsYourChart_AndItSaves()
        {
            var career = Career.New(C);
            var m = Play(99, GameMode.QuickCall);
            // Give the human line a couple of shots so the test doesn't depend on an idle player shooting.
            var s = MatchSummary.From(m, GameMode.QuickCall, "chart-1");
            ShotZones.Record(s.HumanLine.stats.chart, ShotSpot.WingRight, true);
            s.HumanLine.stats.fieldGoalsAttempted++;
            s.HumanLine.stats.fieldGoalsMade++;
            Career.ApplyMatch(career, s, default);
            Assert.AreEqual(s.HumanLine.stats.fieldGoalsAttempted, ShotCharts.Attempts(career.shotChart));
            Career.ApplyMatch(career, s, default); // the same game twice counts once
            Assert.AreEqual(s.HumanLine.stats.fieldGoalsAttempted, ShotCharts.Attempts(career.shotChart));
            var back = SaveCodec.Decode(SaveCodec.Encode(career), C, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(ShotCharts.Encode(career.shotChart), ShotCharts.Encode(back.shotChart));
        }

        [Test]
        public void Franchise_TeamChart_FillsFromYourGames_ResetsEachSeason_AndSaves()
        {
            var league = C.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var f = Franchise.Create(C, league[1].id, 5);
            var shots = new ShotChartData();
            ShotZones.Record(shots, ShotSpot.ElbowLeft, true);
            ShotZones.Record(shots, ShotSpot.ElbowLeft, false);
            Assert.IsTrue(Franchise.RecordYourGame(f, C, 60, 50, shots));
            Assert.AreEqual(2, ShotCharts.Attempts(f.teamChart));
            f.coach = true;
            var career = Career.New(C);
            career.franchise = f;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), C, out _).franchise;
            Assert.AreEqual(2, ShotCharts.Attempts(back.teamChart));
            Assert.IsTrue(back.coach);
            Assert.IsTrue(Franchise.NextMatch(f, C, DefaultContent.DefaultDifficultyId).Coach, "coach mode goes with the game");
        }

        [Test]
        public void Legacy_KeepsSeasonAndCareerCharts()
        {
            var l = Legacy.Start(C, 41);
            var line = new PlayerStatLine();
            ShotZones.Record(line.chart, ShotSpot.TopOfKey, true);
            Assert.IsNotNull(Legacy.RecordGame(l, C, 50, 40, line));
            Assert.AreEqual(1, ShotCharts.Attempts(l.seasonChart));
            Assert.AreEqual(1, ShotCharts.Attempts(l.careerChart));
            var career = Career.New(C);
            career.legacy = l;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), C, out _).legacy;
            Assert.AreEqual(1, ShotCharts.Attempts(back.seasonChart));
            Assert.AreEqual(1, ShotCharts.Attempts(back.careerChart));
        }

        [Test]
        public void ChartArt_DrawsTheCourt_AndColoursEachSpot()
        {
            var d = new ShotChartData();
            for (int i = 0; i < 5; i++) ShotZones.Record(d, ShotSpot.CornerRight, i < 4);
            for (int i = 0; i < 5; i++) ShotZones.Record(d, ShotSpot.FoulLine, false);
            var court = CourtGeometry.Default;
            var art = ShotChartArt.Render(d, court);
            Assert.AreEqual((int)System.Math.Ceiling(court.width * ShotChartArt.Scale), art.Width);
            var hot = ShotCharts.CourtPoint(ShotSpot.FoulLine, court);
            var px = art.Get((int)((hot.x + court.HalfWidth) * ShotChartArt.Scale), (int)(hot.y * ShotChartArt.Scale));
            Assert.AreEqual(ShotChartArt.ColdColor.r, px.r, "0 for 5 from the foul line is blue");
            var c2 = ShotCharts.CourtPoint(ShotSpot.CornerRight, court);
            var px2 = art.Get((int)((c2.x + court.HalfWidth) * ShotChartArt.Scale), (int)(c2.y * ShotChartArt.Scale));
            Assert.AreEqual(ShotChartArt.HotColor.r, px2.r, "4 for 5 from the corner is red");
        }
    }

    /// <summary>Phase 35: coach mode.</summary>
    public class Phase35CoachTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        private static FranchiseSaveData NewFranchise()
        {
            var league = C.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var f = Franchise.Create(C, league[3].id, 12);
            f.coach = true;
            return f;
        }

        private static MatchSimulation Coached(uint seed, System.Action<MatchSimulation, int> everyStep = null)
        {
            var req = Franchise.NextMatch(NewFranchise(), C, DefaultContent.DefaultDifficultyId);
            req.Seed = seed;
            var m = new MatchSimulation(MatchSetup.FromRequest(req, C));
            int g = 0;
            while (!m.IsOver && g < 60 * 60 * 20)
            {
                everyStep?.Invoke(m, g);
                // Mash every button: in a coached game nobody's input is read.
                var input = new PlayerInput { Move = new Vec2(1f, 0f), ShootPressed = g % 7 == 0, PassPressed = g % 11 == 0, DefensePressed = g % 5 == 0 };
                m.Step(1f / 60f, input);
                g++;
            }
            return m;
        }

        [Test]
        public void CoachedGame_HasNoHumanPlayer_AndIgnoresInput()
        {
            var m = Coached(31);
            Assert.IsTrue(m.Coaching);
            Assert.IsTrue(m.IsOver, "the AI plays it out");
            for (int i = 0; i < m.Players.Length; i++)
            {
                Assert.IsFalse(m.IsHumanControlled(i));
                Assert.IsFalse(m.Players[i].IsHuman);
            }
            Assert.AreEqual(-1, m.HumanIndexOf(m.Setup.HumanTeam));
            var s = MatchSummary.From(m, GameMode.Franchise, "coach");
            Assert.IsNull(s.HumanLine, "no line of yours to add to your career");
            var again = Coached(31);
            Assert.AreEqual(m.Score[0], again.Score[0], "coached games are repeatable");
            Assert.AreEqual(m.Score[1], again.Score[1]);
        }

        [Test]
        public void CoachCalls_DoNothing_WhenYoureNotCoaching()
        {
            var r = MatchRequest.QuickCallDefault(C);
            var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
            Assert.IsFalse(m.Coaching);
            Assert.IsFalse(m.CoachCallPlay(PlayCall.PickAndRoll));
            m.CoachSetDefense(DefenseScheme.Zone);
            Assert.AreEqual(DefenseScheme.ManToMan, m.SchemeOf(m.Setup.HumanTeam));
            Assert.IsFalse(m.CoachSub(0, 0));
        }

        [Test]
        public void Defense_And_Focus_TakeEffect()
        {
            var req = Franchise.NextMatch(NewFranchise(), C, DefaultContent.DefaultDifficultyId);
            var m = new MatchSimulation(MatchSetup.FromRequest(req, C));
            m.CoachSetDefense(DefenseScheme.Zone);
            Assert.AreEqual(DefenseScheme.Zone, m.SchemeOf(m.Setup.HumanTeam));
            Assert.IsTrue(m.Events.Exists(e => e.Type == MatchEventType.SchemeChanged));
            m.CoachSetFocus(CoachFocus.LetItFly);
            Assert.AreEqual(CoachFocus.LetItFly, m.Focus);
            Assert.IsFalse(m.CoachCallPlay(PlayCall.ClearOut), "only the plays the AI knows how to run");
        }

        [Test]
        public void YourDefense_StaysWhatYouCalled()
        {
            int team = -1;
            var m = Coached(77, (sim, step) =>
            {
                if (step == 0) { sim.CoachSetDefense(DefenseScheme.PackLine); team = sim.Setup.HumanTeam; }
                else Assert.AreEqual(DefenseScheme.PackLine, sim.SchemeOf(team), "the AI never changes the coach's defense");
            });
            Assert.IsTrue(m.IsOver);
        }

        [Test]
        public void CalledPlays_Run()
        {
            var m = Coached(55, (sim, step) =>
            {
                if (step % 600 == 30) sim.CoachCallPlay(step % 1200 == 30 ? PlayCall.PickAndRoll : PlayCall.PostUp);
            });
            Assert.Greater(m.CoachPlaysRun, 2, "plays called from the sideline get run");
        }

        [Test]
        public void Subs_GoInAtTheNextDeadBall()
        {
            string outId = null, inId = null;
            int you = -1, slot = -1;
            bool swapped = false;
            var m = Coached(21, (sim, step) =>
            {
                if (step == 10)
                {
                    you = sim.Setup.HumanTeam;
                    slot = you * sim.TeamSize + 2;
                    outId = sim.Players[slot].Def.id;
                    inId = sim.CoachBench()[1].Def.id;
                    Assert.IsTrue(sim.CoachSub(slot, 1));
                    Assert.IsTrue(sim.SubPendingFor(slot));
                    Assert.AreEqual(1, sim.PendingSubs);
                }
                if (step > 10 && !swapped && sim.Players[slot].Def.id == inId)
                {
                    swapped = true;
                    Assert.AreEqual(0, sim.PendingSubs);
                    Assert.IsTrue(sim.CoachBench().Exists(b => b.Def.id == outId), "the player who came out sits on the bench");
                }
            });
            Assert.IsTrue(swapped, "the sub went in");
        }

        [Test]
        public void AutoSubsOff_OnlyYourCallsChangeTheFive()
        {
            var starters = new HashSet<string>();
            var m = Coached(88, (sim, step) =>
            {
                if (step == 0)
                {
                    sim.CoachAutoSubs = false;
                    for (int i = 0; i < sim.Players.Length; i++) if (sim.Players[i].Team == sim.Setup.HumanTeam) starters.Add(sim.Players[i].Def.id);
                }
                for (int i = 0; i < sim.Players.Length; i++)
                    if (sim.Players[i].Team == sim.Setup.HumanTeam) Assert.IsTrue(starters.Contains(sim.Players[i].Def.id));
            });
            Assert.IsTrue(m.IsOver);
        }

        [Test]
        public void Focus_ChangesWhereYourTeamShoots()
        {
            int deepFly = 0, deepRim = 0;
            for (uint s = 1; s <= 3; s++)
            {
                foreach (var focus in new[] { CoachFocus.LetItFly, CoachFocus.AttackRim })
                {
                    var f = focus;
                    var m = Coached(s * 13, (sim, step) => { if (step == 0) sim.CoachSetFocus(f); });
                    int deep = 0;
                    for (int i = 0; i < m.Players.Length; i++)
                        if (m.Players[i].Team == m.Setup.HumanTeam) deep += m.Stats[i].arcAttempted;
                    foreach (var b in m.Bench) if (b.Team == m.Setup.HumanTeam) deep += b.Line.arcAttempted;
                    if (f == CoachFocus.LetItFly) deepFly += deep; else deepRim += deep;
                }
            }
            System.Console.WriteLine("deep attempts: let it fly " + deepFly + ", attack the rim " + deepRim);
            Assert.Greater(deepFly, deepRim, "LET IT FLY takes more deep shots than ATTACK THE RIM");
        }
    }

    /// <summary>Phase 35: the replay theater and tapes with marks.</summary>
    public class Phase35TheaterTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        private static GameTape BlankTape(int steps)
        {
            var t = new GameTape { Setup = new LinkSetup(), Title = "T" };
            for (int i = 0; i < steps; i++) { t.TeamA.Add(default); t.TeamB.Add(default); }
            return t;
        }

        [Test]
        public void SlowMotion_AndFast_StepEvenly()
        {
            var th = new TapeTheater(BlankTape(100));
            th.SetSpeed(0);
            int total = 0;
            for (int f = 0; f < 8; f++) total += th.StepsFor(1f / 60f, 1f / 60f, 8);
            Assert.AreEqual(2, total, "a quarter speed is one step every four frames");
            th.SetSpeed(4);
            Assert.AreEqual(4, th.StepsFor(1f / 60f, 1f / 60f, 8));
            th.Paused = true;
            Assert.AreEqual(0, th.StepsFor(1f / 60f, 1f / 60f, 8));
            th.Paused = false;
            Assert.AreEqual(8, th.StepsFor(1f, 1f / 60f, 8), "never more than the frame allows");
            th.SetSpeed(0);
            Assert.AreEqual("0.25x", th.SpeedLabel);
            th.SetSpeed(TapeTheater.NormalSpeed);
            Assert.AreEqual("1x", th.SpeedLabel);
        }

        [Test]
        public void Seeking_ForwardRunsOn_BackStartsOver()
        {
            Assert.AreEqual(50, TapeTheater.SeekPlan(100, 150, 1000, out bool restart));
            Assert.IsFalse(restart);
            Assert.AreEqual(40, TapeTheater.SeekPlan(100, 40, 1000, out restart));
            Assert.IsTrue(restart);
            Assert.AreEqual(900, TapeTheater.SeekPlan(100, 5000, 1000, out restart), "past the end stops at the end");
            var th = new TapeTheater(BlankTape(600));
            Assert.AreEqual(300, th.TickAt(0.5f));
            Assert.AreEqual(0.5f, th.FractionOf(300), 0.0001f);
            Assert.AreEqual("1:05", TapeTheater.Clock(65 * 60));
        }

        [Test]
        public void Marks_StaySorted_AndNextPreviousSkipAround()
        {
            var th = new TapeTheater(BlankTape(6000));
            th.AddMark(3000, "B", true);
            th.AddMark(1000, "A", true);
            th.AddUserMark(5000);
            Assert.AreEqual("1000,3000,5000", string.Join(",", th.Marks.ConvertAll(m => m.Tick)));
            Assert.AreEqual("A", th.NextMark(0).Label);
            Assert.AreEqual("B", th.NextMark(TapeTheater.StartOf(th.Marks[0])).Label, "next from a mark goes to the one after");
            Assert.AreEqual("B", th.PreviousMark(4000).Label);
            Assert.AreEqual("A", th.PreviousMark(TapeTheater.StartOf(th.Marks[1]) + 10).Label, "previous just after a mark goes further back");
            Assert.IsNull(th.PreviousMark(100));
            Assert.IsNull(th.NextMark(5900));
            Assert.IsTrue(th.Marks[2].Label.StartsWith("MARK 1"), th.Marks[2].Label);
        }

        [Test]
        public void Index_MarksEveryBasket()
        {
            var req = MatchRequest.QuickCallDefault(C);
            req.Mode = GameMode.Demo;
            req.Seed = 909;
            var tape = BlankTape(60 * 60 * 6);
            var th = new TapeTheater(tape);
            th.Index(() => new MatchSimulation(MatchSetup.FromRequest(req, C)), 1f / 60f);
            Assert.IsTrue(th.Indexed);
            // Play the same game and count the baskets.
            var m = new MatchSimulation(MatchSetup.FromRequest(req, C));
            int baskets = 0;
            for (int i = 0; i < tape.Steps && !m.IsOver; i++)
            {
                m.Step(1f / 60f, default, default);
                foreach (var e in m.Events) if (e.Type == MatchEventType.ShotMade) baskets++;
            }
            Assert.Greater(baskets, 0);
            Assert.AreEqual(baskets, th.Marks.FindAll(x => x.Label.Contains("SCORES") || x.Label.Contains("FROM DEEP")).Count);
            th.Index(() => null, 1f / 60f); // already done: nothing happens
        }

        [Test]
        public void TapesWithMarks_RoundTrip_AndOldTapesStillRead()
        {
            var plain = BlankTape(500);
            var bytes = Tapes.Encode(plain);
            Assert.AreEqual((byte)'1', bytes[3], "no marks: still an RHT1 file");
            Assert.IsNotNull(Tapes.Decode(bytes));
            plain.Marks.Add(120);
            plain.Marks.Add(480);
            var marked = Tapes.Encode(plain);
            Assert.AreEqual((byte)'2', marked[3]);
            var back = Tapes.Decode(marked);
            Assert.IsNotNull(back);
            Assert.AreEqual("120,480", string.Join(",", back.Marks));
            Assert.AreEqual(500, back.Steps);
            var bad = (byte[])marked.Clone();
            bad[bad.Length - 1] = 0x7F; // a mark far past the end
            Assert.IsNull(Tapes.Decode(bad));
            Assert.IsNull(Tapes.Decode(new byte[] { (byte)'R', (byte)'H', (byte)'T', (byte)'9' }));
        }

        [Test]
        public void Camera_CyclesThroughEveryPlayer()
        {
            var th = new TapeTheater(BlankTape(10));
            Assert.AreEqual(TheaterCamera.Ball, th.Camera);
            th.NextCamera(3);
            Assert.AreEqual(TheaterCamera.Player, th.Camera);
            th.NextCamera(3);
            th.NextCamera(3);
            Assert.AreEqual(2, th.CameraPlayer);
            th.NextCamera(3);
            Assert.AreEqual(TheaterCamera.Rim, th.Camera);
            th.NextCamera(3);
            Assert.AreEqual(TheaterCamera.CloseUp, th.Camera);
            th.NextCamera(3);
            Assert.AreEqual(TheaterCamera.Ball, th.Camera);
        }
    }

    /// <summary>Phase 35: the polish pass (transitions, tips, arena, sound mix).</summary>
    public class Phase35PolishTests
    {
        [Test]
        public void EveryWipeStyle_StartsClear_EndsCovered_AndNeverUncovers()
        {
            const int cols = 32, rows = 18;
            for (int style = 0; style < ScreenWipe.Styles; style++)
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        Assert.IsFalse(ScreenWipe.Covered(style, x, y, cols, rows, 0f));
                        Assert.IsTrue(ScreenWipe.Covered(style, x, y, cols, rows, 1f));
                        bool was = false;
                        for (int s = 1; s < 20; s++)
                        {
                            bool now = ScreenWipe.Covered(style, x, y, cols, rows, s / 20f);
                            if (was) Assert.IsTrue(now, "style " + style + " uncovered " + x + "," + y);
                            was = now;
                        }
                    }
            // The styles really differ.
            int differ = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    if (ScreenWipe.Covered(1, x, y, cols, rows, 0.4f) != ScreenWipe.Covered(0, x, y, cols, rows, 0.4f)) differ++;
            Assert.Greater(differ, 40);
            Assert.AreEqual(ScreenWipe.Covered(3, 4, 4, cols, rows, 0.5f), ScreenWipe.Covered(4, 4, cols, rows, 0.5f), "style 3 wraps to the first");
        }

        [Test]
        public void Tips_ComeInTurn_NeverTwiceInARow()
        {
            var seen = new HashSet<string>();
            for (int n = 0; n < LoadingTips.All.Length; n++)
            {
                seen.Add(LoadingTips.Tip(n));
                Assert.AreNotEqual(LoadingTips.Tip(n), LoadingTips.Tip(n + 1));
            }
            Assert.AreEqual(LoadingTips.All.Length, seen.Count, "every tip turns up once per lap");
            foreach (var t in LoadingTips.All)
            {
                Assert.LessOrEqual(t.Length, 90, "fits on the wipe: " + t);
                foreach (var banned in new[] { "NBA", "2K", "Jordan", "LeBron" }) StringAssert.DoesNotContain(banned, t);
            }
        }

        [Test]
        public void Flashes_AreFewSpreadAndRepeatable()
        {
            var a = ArenaFx.Flashes(77, 40, 7);
            var b = ArenaFx.Flashes(77, 40, 7);
            Assert.AreEqual(7, a.Count);
            var fans = new HashSet<int>();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Fan, b[i].Fan);
                Assert.IsTrue(fans.Add(a[i].Fan), "a different fan each time");
                Assert.IsTrue(a[i].Delay >= 0f && a[i].Delay <= ArenaFx.FlashSeconds + 0.1f, "delay " + a[i].Delay);
            }
            Assert.AreEqual(0, ArenaFx.Flashes(1, 0, 5).Count);
            Assert.AreEqual(ArenaFx.MaxFlashes, ArenaFx.Flashes(3, 100, 99).Count);
            Assert.AreEqual(0, ArenaFx.FlashCount(false, false, false), "an ordinary bucket: no flashes");
            Assert.Greater(ArenaFx.FlashCount(true, false, false), ArenaFx.FlashCount(false, true, false));
        }

        [Test]
        public void TheWave_RollsLeftToRight()
        {
            float peakLeft = -1f, peakRight = -1f;
            for (float t = 0f; t <= ArenaFx.WaveSeconds; t += 0.05f)
            {
                if (peakLeft < 0f && ArenaFx.WaveLift(0.1f, t) == 2) peakLeft = t;
                if (peakRight < 0f && ArenaFx.WaveLift(0.9f, t) == 2) peakRight = t;
            }
            Assert.Greater(peakLeft, 0f);
            Assert.Greater(peakRight, peakLeft, "the right side stands up after the left");
            Assert.AreEqual(0, ArenaFx.WaveLift(0.5f, ArenaFx.WaveSeconds + 1f));
            Assert.AreEqual(6, ArenaFx.Run(0, 4, 0, 2, out int team));
            Assert.AreEqual(0, team);
            Assert.AreEqual(3, ArenaFx.Run(0, 4, 1, 3, out team), "the other team scoring ends the run");
            Assert.AreEqual(1, team);
        }

        [Test]
        public void Clutch_IsCloseAndLate()
        {
            Assert.IsTrue(ArenaFx.Clutch(40, 41, 12f, true, 0));
            Assert.IsFalse(ArenaFx.Clutch(40, 50, 12f, true, 0), "not close");
            Assert.IsFalse(ArenaFx.Clutch(40, 41, 200f, true, 0), "not late");
            Assert.IsTrue(ArenaFx.Clutch(18, 19, 0f, false, 21));
            Assert.IsFalse(ArenaFx.Clutch(8, 9, 0f, false, 21));
        }

        [Test]
        public void Mix_DucksFast_ComesBackSlowly_AndSoftensRepeats()
        {
            float g = 1f;
            for (int i = 0; i < 6; i++) g = AudioMix.Duck(g, true, 1f / 60f);
            Assert.AreEqual(1f - AudioMix.DuckDepth, g, 0.001f, "down within a tenth of a second");
            float back = AudioMix.Duck(g, false, 0.1f);
            Assert.Less(back, 1f, "and back up gently");
            Assert.Greater(back, g);
            Assert.AreEqual(1f, AudioMix.RepeatGain(0));
            Assert.Less(AudioMix.RepeatGain(2), AudioMix.RepeatGain(1));
            Assert.Greater(AudioMix.CrowdLevel(CrowdMood.Cheer, false), AudioMix.CrowdLevel(CrowdMood.Idle, false));
            Assert.Greater(AudioMix.CrowdLevel(CrowdMood.Idle, true), AudioMix.CrowdLevel(CrowdMood.Idle, false));
            Assert.Less(AudioMix.CrowdLevel(CrowdMood.Groan, false), 1f);
        }
    }
}
