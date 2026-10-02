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
    public class GameSceneTests
    {
        private static IEnumerator LoadMatch(MatchRequest request)
        {
            App.EnsureInitialized();
            App.PendingMatch = request;
            SceneManager.LoadScene(SceneNames.Game);
            yield return null; // scene loads
            yield return null; // Start runs
        }

        private static GameSceneController Controller() => Object.FindAnyObjectByType<GameSceneController>();

        [UnityTest]
        public IEnumerator QuickCall_StartsZeroZero_HomeBallAtCheckSpot()
        {
            yield return LoadMatch(MatchRequest.QuickCallDefault(DefaultContent.Create()));
            var c = Controller();
            Assert.IsNotNull(c, "GameSceneController missing — run project setup.");
            Assert.IsNotNull(c.Match);
            Assert.AreEqual(0, c.Match.Score[0]);
            Assert.AreEqual(0, c.Match.Score[1]);
            Assert.AreEqual(0, c.Match.OffenseTeam);
            Assert.AreEqual(0, c.Match.HolderIndex);
            Assert.AreEqual(6, Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator Match_RunsFramesWithoutErrors()
        {
            yield return LoadMatch(MatchRequest.QuickCallDefault(DefaultContent.Create()));
            float t0 = c0();
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(Controller().Match.Time, t0);
        }

        private static float c0() => Controller().Match.Time;

        [UnityTest]
        public IEnumerator GameClock_RunsDuringLivePlay()
        {
            yield return LoadMatch(MatchRequest.QuickCallDefault(DefaultContent.Create()));
            var m = Controller().Match;
            float start = m.GameClock;
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.Less(m.GameClock, start, "Clock should tick once the check-ball freeze ends");
        }

        [UnityTest]
        public IEnumerator Practice_LoadsOnPracticeCourt()
        {
            yield return LoadMatch(MatchRequest.PracticeDefault());
            Assert.IsNotNull(Controller().Match);
        }
    }
}
