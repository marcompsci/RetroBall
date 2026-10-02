using System.Collections;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CallerRetroBall.Tests
{
    /// <summary>Each menu scene builds without logging errors (Unity fails the test on Debug.LogError/exceptions).</summary>
    public class MenuScreensTests
    {
        private static IEnumerator Load(string scene)
        {
            App.EnsureInitialized();
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            for (int i = 0; i < 10; i++) yield return null;
        }

        [UnityTest] public IEnumerator MainMenu_Builds() { yield return Load(SceneNames.MainMenu); Assert.IsNotNull(App.Career); }
        [UnityTest] public IEnumerator RiseHub_Builds() { yield return Load(SceneNames.Season); Assert.IsNotNull(App.Career.rise); }
        [UnityTest] public IEnumerator LockerRoom_Builds() { yield return Load(SceneNames.LockerRoom); Assert.IsNotNull(App.Career); }
        [UnityTest] public IEnumerator Settings_Builds() { yield return Load(SceneNames.Settings); Assert.IsNotNull(App.Career.settings); }

        [UnityTest]
        public IEnumerator PracticeDrill_StartsWithDrillHud()
        {
            App.EnsureInitialized();
            var request = MatchRequest.PracticeDefault();
            request.Drill = (int)DrillKind.DribbleLane;
            App.PendingMatch = request;
            SceneManager.LoadScene(SceneNames.Game);
            yield return null;
            yield return null;
            Assert.IsNotNull(GameObject.Find("Cone 0"), "Dribble lane should place its cones");
        }
    }
}
