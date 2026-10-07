using System.Collections;
using CallerRetroBall.Core;
using CallerRetroBall.Gameplay;
using CallerRetroBall.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// Phase 37: smoke tests for CLUTCH games and store-screenshot launches. Unity fails a test on any Debug.LogError
    /// or exception. Run in Unity: Window ► General ► Test Runner ► PlayMode.
    /// </summary>
    public class Phase37SmokeTests
    {
        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Clutch_StartsFromTheScenario_AndPlays()
        {
            App.EnsureInitialized();
            var s = Clutch.Find("iv_three");
            App.PendingMatch = Clutch.Request(App.Catalog, s, DefaultContent.DefaultDifficultyId);
            SceneManager.LoadScene(SceneNames.Game);
            yield return Frames(2);
            var c = Object.FindAnyObjectByType<GameSceneController>();
            Assert.IsNotNull(c);
            Assert.AreEqual(s.YourTeamId, c.Match.Setup.TeamA.id);
            Assert.GreaterOrEqual(c.Match.Score[0], s.ScoreFor);
            Assert.GreaterOrEqual(c.Match.Score[1], s.ScoreAgainst);
            Assert.LessOrEqual(c.Match.GameClock, s.Clock);
            float t0 = c.Match.Time;
            yield return Frames(120);
            Assert.Greater(c.Match.Time, t0, "the game runs");
        }

        [UnityTest]
        public IEnumerator StoreShot_GameLaunch_ShowsAnAiGame()
        {
            App.EnsureInitialized();
            StoreShots.Current = StoreShots.Parse("game");
            StoreShots.Shown = true;
            try
            {
                App.PendingMatch = StoreShots.GameRequest(App.Catalog, false);
                SceneManager.LoadScene(SceneNames.Game);
                yield return Frames(60);
                var c = Object.FindAnyObjectByType<GameSceneController>();
                Assert.IsNotNull(c);
                Assert.IsTrue(c.Match.Setup.Demo);
                Assert.IsFalse(c.Match.IsOver);
            }
            finally
            {
                StoreShots.Current = null;
                StoreShots.Shown = false;
            }
        }
    }
}
