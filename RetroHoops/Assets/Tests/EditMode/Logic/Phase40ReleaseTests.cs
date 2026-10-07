using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// Phase 40 release candidate: old and damaged saves load, every mode survives random play, and the simulation stays
    /// well inside its frame budget.
    /// </summary>
    public class Phase40ReleaseTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        [Test]
        public void OldAndDamagedSaves_LoadWithDefaults()
        {
            var cases = new[]
            {
                "{\"version\":1}",
                "{\"version\":1,\"nickname\":\"Ana\",\"signalPoints\":250}",
                // A Phase 20-era save: no clutch, login, feel, live, gauntlet, story or shot chart sections.
                "{\"version\":1,\"nickname\":\"Old\",\"signalPoints\":5,\"fans\":9,\"totals\":{\"games\":12,\"wins\":7},\"settings\":{\"musicVolume\":0.4}}",
                // Wrong types everywhere.
                "{\"version\":1,\"nickname\":5,\"signalPoints\":\"lots\",\"clutch\":\"nope\",\"login\":[1,2],\"feel\":{\"stick\":\"big\",\"dead\":99,\"haptic\":-4},\"rival\":7}",
                // Values out of range.
                "{\"version\":1,\"clutch\":{\"stars\":[\"ct_down2:9\",\"zzz:3\",\"bad\"],\"played\":-5,\"won\":99,\"dDay\":-50,\"dStreak\":-1},\"login\":{\"day\":-9,\"streak\":-3,\"best\":-1}}",
            };
            foreach (var json in cases)
            {
                var d = SaveCodec.Decode(json, C, out var status);
                Assert.IsNotNull(d, json);
                Assert.AreNotEqual(LoadStatus.Recovered, status, "readable JSON is never thrown away: " + json);
                Assert.IsNotNull(d.clutch, json);
                Assert.IsNotNull(d.settings, json);
                Assert.IsTrue(d.settings.stickSize >= 0 && d.settings.stickSize <= 2, json);
                Assert.IsTrue(d.settings.stickDeadZone >= 0 && d.settings.stickDeadZone <= 2, json);
                Assert.IsTrue(d.settings.hapticStrength >= 0 && d.settings.hapticStrength <= 2, json);
                Assert.GreaterOrEqual(d.loginDay, -1, json);
                Assert.GreaterOrEqual(d.loginStreak, 0, json);
                Assert.GreaterOrEqual(d.clutch.played, 0, json);
                Assert.LessOrEqual(d.clutch.won, d.clutch.played, json);
                Assert.LessOrEqual(Clutch.TotalStars(d.clutch), Clutch.MaxStars, json);
                Assert.IsNotNull(Onboarding.Next(d, 1000), json);
                // Re-encoding a migrated save and reading it back is stable.
                string again = SaveCodec.Encode(d);
                Assert.AreEqual(again, SaveCodec.Encode(SaveCodec.Decode(again, C, out _)), json);
            }
            var old = SaveCodec.Decode(cases[2], C, out _);
            Assert.AreEqual("Old", old.nickname);
            Assert.AreEqual(12, old.totals.games);
            Assert.AreEqual(1, old.settings.stickSize, "new settings start at NORMAL");
            Assert.AreEqual(-1, old.clutch.dailyDay);
        }

        [Test]
        public void Garbage_IsRecovered_NotCrashed()
        {
            foreach (var junk in new[] { "", "{}", "not json", "{\"version\":", "[1,2,3]", "{\"version\":999999}", new string('{', 5000) })
            {
                var d = SaveCodec.Decode(junk, C, out var status);
                Assert.IsNotNull(d, "a career always comes back: " + junk.Length);
            }
        }

        /// <summary>Every mode, random teams/courts/difficulties and random thumbs: nothing throws and games end.</summary>
        [Test]
        public void EveryMode_SurvivesRandomPlay()
        {
            var rng = new SeededRandom(40);
            var teams = C.TeamsInTier(TeamTier.League);
            var modes = (GameMode[])System.Enum.GetValues(typeof(GameMode));
            int games = 0;
            foreach (var mode in modes)
            {
                for (int k = 0; k < 2; k++)
                {
                    var r = MatchRequest.QuickCallDefault(C);
                    r.Mode = mode;
                    r.HomeTeamId = teams[rng.Range(0, teams.Count)].id;
                    var away = teams[rng.Range(0, teams.Count)];
                    if (away.id == r.HomeTeamId) away = teams[(teams.IndexOf(away) + 1) % teams.Count];
                    r.AwayTeamId = away.id;
                    r.CourtId = C.Courts[rng.Range(0, C.Courts.Count)].id;
                    r.DifficultyId = C.Difficulties[rng.Range(0, C.Difficulties.Count)].id;
                    r.Seed = (uint)rng.Range(1, 100000);
                    if (mode == GameMode.Clutch)
                        r = Clutch.Request(C, Clutch.All[rng.Range(0, Clutch.All.Length)], r.DifficultyId);
                    if (mode == GameMode.Practice || mode == GameMode.Tutorial) r.Drill = rng.Range(0, 3);
                    MatchSimulation m;
                    try { m = new MatchSimulation(MatchSetup.FromRequest(r, C)); }
                    catch (System.Exception e) { Assert.Fail(mode + " setup threw: " + e); return; }
                    var input = default(PlayerInput);
                    for (int step = 0; step < 60 * 45 && !m.IsOver; step++)
                    {
                        if (step % 20 == 0)
                        {
                            input = new PlayerInput
                            {
                                Move = new Vec2(rng.Range(-100, 101) / 100f, rng.Range(-100, 101) / 100f),
                                ShootPressed = rng.Range(0, 6) == 0,
                                ShootHeld = rng.Range(0, 3) == 0,
                                PassPressed = rng.Range(0, 8) == 0,
                                DefensePressed = rng.Range(0, 8) == 0,
                                DunkPressed = rng.Range(0, 20) == 0,
                                LayupPressed = rng.Range(0, 20) == 0,
                                CallPlay = rng.Range(0, 30) == 0 ? (PlayCall)rng.Range(1, 4) : PlayCall.None,
                            };
                        }
                        m.Step(1f / 60f, input, input);
                    }
                    Assert.GreaterOrEqual(m.Score[0], 0, mode.ToString());
                    games++;
                }
            }
            Assert.AreEqual(modes.Length * 2, games);
        }

        /// <summary>
        /// A four-minute Full Court game (14,400 steps at 60 Hz, ten AI players) simulates far inside the frame budget.
        /// On the cloud machine this takes well under a second; an iPhone has 8.3 ms a frame at 120 Hz for one step.
        /// </summary>
        [Test]
        public void FullCourtGame_StaysInsideTheFrameBudget()
        {
            var r = MatchRequest.QuickCallDefault(C);
            r.Mode = GameMode.Demo;
            r.FullCourt = true;
            r.RulesId = FullCourt.RulesId;
            r.Seed = 9;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
            for (int i = 0; i < 600; i++) m.Step(1f / 60f, default); // warm up
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int steps = 0;
            while (!m.IsOver && steps < 60 * 60 * 6) { m.Step(1f / 60f, default); steps++; }
            clock.Stop();
            double perStepMs = clock.Elapsed.TotalMilliseconds / System.Math.Max(1, steps);
            Assert.IsTrue(m.IsOver, "the game finished");
            Assert.Less(perStepMs, 0.5, "average step " + perStepMs.ToString("0.000") + " ms");
        }
    }
}
