using System.Collections;
using CallerRetroBall.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CallerRetroBall.Tests
{
    public class BootFlowTests
    {
        [UnityTest]
        public IEnumerator Boot_InitializesApp_AndReachesMainMenu()
        {
            SceneManager.LoadScene(SceneNames.Boot);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(SceneNames.MainMenu, SceneManager.GetActiveScene().name, "Boot did not reach the main menu in 10s.");
            Assert.IsTrue(App.IsInitialized);
            Assert.IsNotNull(App.Catalog);
        }

        [UnityTest]
        public IEnumerator SceneFlow_IgnoresSecondRequestWhileTransitioning()
        {
            App.EnsureInitialized();
            Assert.IsTrue(SceneFlow.GoTo(SceneNames.Settings));
            Assert.IsFalse(SceneFlow.GoTo(SceneNames.LockerRoom), "A second load started during a transition.");

            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneFlow.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(SceneNames.Settings, SceneManager.GetActiveScene().name);
        }
    }
}
