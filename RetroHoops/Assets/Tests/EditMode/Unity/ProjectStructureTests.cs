using System.Linq;
using CallerRetroBall.Core;
using CallerRetroBall.Data;
using CallerRetroBall.Logic;
using NUnit.Framework;
using UnityEditor;

namespace CallerRetroBall.Tests
{
    /// <summary>Unity-only EditMode checks (need the Editor API, so they don't run in the .NET harness).</summary>
    public class ProjectStructureTests
    {
        [Test]
        public void AllScenes_AreInBuildSettings_BootFirst()
        {
            var paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            Assert.Greater(paths.Count, 0, "Run 'Retro Hoops ▸ Run Project Setup' first.");
            Assert.AreEqual("Assets/Scenes/" + SceneNames.Boot + ".unity", paths[0]);
            foreach (var name in SceneNames.All)
                Assert.IsTrue(paths.Contains("Assets/Scenes/" + name + ".unity"), name + " missing from Build Settings");
        }

        [Test]
        public void ContentDatabase_LoadsValidContent()
        {
            var db = ContentDatabase.Load();
            var report = ContentValidator.Validate(db.Catalog);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(8, db.Catalog.TeamsInTier(TeamTier.League).Count);
        }

        [Test]
        public void ContentAssets_HaveBeenGenerated()
        {
            var db = ContentDatabase.Load();
            Assert.AreEqual(0, db.FallbackKinds.Count,
                "Missing assets for: " + string.Join(", ", db.FallbackKinds) + ". Run 'Retro Hoops ▸ Run Project Setup'.");
        }
    }
}
