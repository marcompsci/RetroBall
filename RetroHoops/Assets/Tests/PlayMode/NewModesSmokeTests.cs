using System.Collections;
using System.Collections.Generic;
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
    /// Phase 34: smoke tests for the screens and scene paths added in Phases 26-34 that the logic tests can't reach
    /// (Unity fails a test on any Debug.LogError or exception). Run in Unity: Window ► General ► Test Runner ► PlayMode.
    /// </summary>
    public class NewModesSmokeTests
    {
        private static GameSceneController Controller() => Object.FindAnyObjectByType<GameSceneController>();

        private static GameTape ShortTape(ContentCatalog c)
        {
            var league = c.TeamsInTier(TeamTier.League);
            var s = LinkSetup.From(c, league[0].id, league[1].id, league[0].homeCourtId, DefaultContent.DefaultDifficultyId, 1234, App.Version, "Host");
            var a = new List<PlayerInput>();
            var b = new List<PlayerInput>();
            for (int i = 0; i < 600; i++)
            {
                a.Add(LinkProtocol.Quantize(new PlayerInput { Move = new Vec2(i % 120 < 60 ? 1f : -1f, 0.3f), ShootPressed = i % 90 == 0 }));
                b.Add(LinkProtocol.Quantize(new PlayerInput { Move = new Vec2(0f, i % 100 < 50 ? 1f : -1f) }));
            }
            return Tapes.From(s, a, b, "SMOKE TEST", 1, 0, 0);
        }

        [UnityTest]
        public IEnumerator GameTape_PlaysInTheGameScene()
        {
            App.EnsureInitialized();
            Assert.IsTrue(TapeStore.PrepareToWatch(ShortTape(App.Catalog), out string why), why);
            SceneManager.LoadScene(SceneNames.Game);
            yield return null;
            yield return null;
            var c = Controller();
            Assert.IsNotNull(c);
            float t0 = c.Match.Time;
            for (int i = 0; i < 90; i++) yield return null;
            Assert.Greater(c.Match.Time, t0, "the tape plays");
            LinkMatch.Clear();
        }

        [UnityTest]
        public IEnumerator TapeStore_SavesListsAndDeletes()
        {
            App.EnsureInitialized();
            var tape = ShortTape(App.Catalog);
            tape.SavedAt = 4242;
            Assert.IsTrue(TapeStore.Save(tape));
            var found = TapeStore.List().Find(e => e.Tape.SavedAt == 4242);
            Assert.IsNotNull(found.Path);
            TapeStore.Delete(found.Path);
            Assert.IsFalse(TapeStore.List().Exists(e => e.Tape.SavedAt == 4242));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SealedSave_RoundTripsThroughTheRealStore()
        {
            App.EnsureInitialized();
            int games = App.Career.totals.games; // saved unchanged: the Editor's career isn't altered
            Assert.IsTrue(SaveStore.Save(App.Career));
            Assert.IsTrue(SaveGuard.IsSigned(System.IO.File.ReadAllText(SaveStore.FilePath)), "written sealed");
            var back = SaveStore.Load(App.Catalog, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(SaveVerdict.Verified, SaveStore.LastVerdict);
            Assert.AreEqual(games, back.totals.games);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Franchise_WithAnOfferAndBracket_DoesntError()
        {
            App.EnsureInitialized();
            var league = App.Catalog.TeamsInTier(TeamTier.League);
            App.Career.franchise = Franchise.Create(App.Catalog, league[0].id, 99);
            var f = App.Career.franchise;
            if (f.phase == FranchisePhase.Preseason) Franchise.StartSeason(f, App.Catalog);
            for (int i = 0; i < 6 && f.phase == FranchisePhase.Regular; i++) Franchise.SimNext(f, App.Catalog);
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;
            yield return null;
            // The bracket logic the LEAGUE tab draws.
            var b = PlayoffBracket.From(f.season);
            Assert.IsTrue(b.Projected);
            for (int i = 0; i < 10; i++) yield return null;
        }
    }
}
