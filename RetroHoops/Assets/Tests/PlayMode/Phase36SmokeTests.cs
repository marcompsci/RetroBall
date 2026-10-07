using System.Collections;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Gameplay;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// Phase 36: smoke tests for Phase 35's scene features (replay theater jumps, coach mode, shot chart views). Unity
    /// fails a test on any Debug.LogError or exception. Run in Unity: Window ► General ► Test Runner ► PlayMode.
    /// </summary>
    public class Phase36SmokeTests
    {
        private static GameSceneController Controller() => Object.FindAnyObjectByType<GameSceneController>();

        private static GameTape Tape(ContentCatalog c, int steps)
        {
            var league = c.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, DefaultContent.DefaultDifficultyId, 77, App.Version, "Host");
            var a = new List<PlayerInput>();
            var b = new List<PlayerInput>();
            for (int i = 0; i < steps; i++)
            {
                a.Add(LinkProtocol.Quantize(new PlayerInput { Move = new Vec2(i % 120 < 60 ? 1f : -1f, 0.4f), ShootPressed = i % 75 == 0 }));
                b.Add(LinkProtocol.Quantize(new PlayerInput { Move = new Vec2(0f, i % 100 < 50 ? 1f : -1f), ShootPressed = i % 110 == 0 }));
            }
            return Tapes.From(s, a, b, "THEATER TEST", 1, 0, 0);
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Theater_MarksTheTape_AndJumpsForwardAndBack()
        {
            App.EnsureInitialized();
            Assert.IsTrue(TapeStore.PrepareToWatch(Tape(App.Catalog, 3000), null, out string why), why);
            SceneManager.LoadScene(SceneNames.Game);
            yield return Frames(2);
            var c = Controller();
            Assert.IsNotNull(c.TapeTheaterState, "a tape opens in the theater");
            for (int i = 0; i < 600 && !c.TapeTheaterState.Indexed; i++) yield return null;
            Assert.IsTrue(c.TapeTheaterState.Indexed, "the marking pass finishes over a few frames");

            c.TheaterJump(2000);
            for (int i = 0; i < 300 && c.TapeTick < 2000; i++) yield return null;
            Assert.GreaterOrEqual(c.TapeTick, 2000, "jumped forward");

            c.TheaterJump(300);
            yield return Frames(3);
            for (int i = 0; i < 300 && c.TapeTick < 300; i++) yield return null;
            Assert.Less(c.TapeTick, 1000, "jumped back (the game was rebuilt from the tip-off)");
            Assert.GreaterOrEqual(c.TapeTick, 300);
            Assert.AreEqual(SceneNames.Game, SceneManager.GetActiveScene().name, "no scene reload");
            LinkMatch.Clear();
        }

        [UnityTest]
        public IEnumerator CoachMode_PlaysAFranchiseGame()
        {
            App.EnsureInitialized();
            var league = App.Catalog.TeamsInTier(TeamTier.League);
            var f = Franchise.Create(App.Catalog, league[0].id, 9);
            f.coach = true;
            App.PendingMatch = Franchise.NextMatch(f, App.Catalog, DefaultContent.DefaultDifficultyId);
            SceneManager.LoadScene(SceneNames.Game);
            yield return Frames(2);
            var c = Controller();
            Assert.IsTrue(c.Match.Coaching);
            Assert.IsNotNull(Object.FindAnyObjectByType<CoachPanel>(), "the sideline bar is up");
            float t0 = c.Match.Time;
            yield return Frames(240);
            Assert.Greater(c.Match.Time, t0, "the AI plays on");
            Assert.IsFalse(c.Match.HumanHasBall, "nobody on your team is yours to steer");
        }

        [UnityTest]
        public IEnumerator ShotChartView_BuildsAndCleansUp()
        {
            App.EnsureInitialized();
            var canvas = UiKit.CreateScreenCanvas("ChartTest", 5);
            var d = new ShotChartData();
            for (int i = 0; i < 6; i++) ShotZones.Record(d, (ShotSpot)(i % ShotZones.SpotCount), i % 2 == 0);
            var view = ShotChartView.Build(canvas.transform, "TEST CHART", d);
            Assert.IsNotNull(view);
            yield return null;
            Object.Destroy(canvas.gameObject);
            yield return null;
        }
    }
}
