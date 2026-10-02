using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// SettingsScene: volumes, haptics, screen shake, UI scale, colourblind team contrast,
    /// difficulty, reset save (with confirmation), and credits / licences. Every change is
    /// saved immediately to the local career file.
    /// </summary>
    public sealed class SettingsController : ScreenBase
    {
        protected override string ScreenTitle => "SETTINGS";
        protected override string BackdropCourtId => "court.overpass_park";
        protected override float ScrimAlpha => 0.75f;

        public const string LicenseText =
            "<color=#FFD166>CREDITS & LICENCES</color>\n" +
            "RetroBall is an original game. All teams, players, leagues, courts, logos, " +
            "and pixel art are original and generated procedurally inside this project.\n\n" +
            "Text font: Liberation Sans, bundled with Unity TextMeshPro (SIL Open Font License 1.1).\n" +
            "Audio: every sound effect and the music loop are synthesised in code at runtime (no recordings or samples).\n" +
            "Haptics: a small original iOS plugin using Apple's UIKit feedback generators.\n\n" +
            "No ads, analytics, tracking, accounts, or purchases. Progress is stored only on this device. " +
            "Game Center is optional and off unless you turn it on.";

        private static readonly float[] UiScales = { 0.85f, 1f, 1.15f, 1.25f };
        private static readonly string[] UiScaleNames = { "SMALL", "DEFAULT", "LARGE", "LARGEST" };

        protected override void Build()
        {
            var column = UiKit.ScrollColumn(Body, 14f, new RectOffset(48, 48, 12, 60));
            var c = App.Catalog;
            var s = App.Career.settings;

            Header(column, "AUDIO");
            UiControls.SliderRow(column, "MUSIC", s.musicVolume, v => { s.musicVolume = v; App.ApplySettings(); });
            UiControls.SliderRow(column, "SFX", s.sfxVolume, v => { s.sfxVolume = v; App.ApplySettings(); });

            Header(column, "FEEL");
            UiControls.ToggleRow(column, "HAPTICS", s.haptics, v => { s.haptics = v; Save(); if (v) Haptics.Light(); });
            UiControls.ToggleRow(column, "SCREEN SHAKE", s.screenShake, v => { s.screenShake = v; Save(); });

            Header(column, "ACCESSIBILITY");
            int scaleIndex = 1;
            for (int i = 0; i < UiScales.Length; i++) if (Mathf.Abs(UiScales[i] - s.uiScale) < 0.01f) scaleIndex = i;
            UiControls.ChoiceRow(column, "UI SCALE", UiScaleNames, scaleIndex, i =>
            {
                s.uiScale = UiScales[i];
                Save();
                SceneFlow.GoTo(SceneNames.Settings); // rebuild canvases at the new scale
            });
            UiControls.ToggleRow(column, "TEAM PATTERNS", s.colorblindContrast, v => { s.colorblindContrast = v; Save(); });
            UiKit.Size(UiKit.Label(column, "Team patterns give each side a distinct jersey pattern, not just a colour.", 28f, Theme.Muted), 70f);
            UiControls.ToggleRow(column, "LEFT-HANDED", s.leftHanded, v => { s.leftHanded = v; Save(); });
            UiControls.ToggleRow(column, "LARGE BUTTONS", s.largeButtons, v => { s.largeButtons = v; Save(); });
            UiControls.ToggleRow(column, "TAP TO SHOOT", s.tapToShoot, v => { s.tapToShoot = v; Save(); });
            UiControls.ToggleRow(column, "REDUCE MOTION", s.reduceMotion, v => { s.reduceMotion = v; Save(); });
            UiKit.Size(UiKit.Label(column,
                "Left-handed puts the stick on the right and buttons on the left. Tap to shoot: tap once to start the meter, tap again to release. " +
                "Reduce motion turns off screen shake, sparks, the score bounce, and crowd bobbing.", 28f, Theme.Muted), 150f);

            Header(column, "LANGUAGE");
            int li = System.Array.IndexOf(Loc.Languages, Loc.Normalize(s.language));
            UiControls.ChoiceRow(column, "LANGUAGE", Loc.LanguageNames, Mathf.Max(0, li), i =>
            {
                s.language = Loc.Languages[i];
                App.ApplySettings();
                Save();
                SceneFlow.GoTo(SceneNames.Settings); // rebuild in the new language
            });

            Header(column, "GAME");
            var diffs = c.Difficulties;
            int di = Mathf.Max(0, diffs.FindIndex(d => d.id == s.difficultyId));
            UiControls.ChoiceRow(column, "DIFFICULTY", diffs.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray(), di, i =>
            {
                s.difficultyId = diffs[i].id;
                Save();
            });
            UiKit.Size(UiKit.Label(column, "Difficulty changes how fast and how well the AI decides. It never boosts their ratings.", 28f, Theme.Muted), 70f);

            UiKit.Button(column, "HOW TO PLAY", MainMenuController.StartTutorial, ButtonStyle.Secondary, 110f, 40f);

            Header(column, "GAME CENTER");
            UiControls.ToggleRow(column, "SIGN IN", s.gameCenter, v => App.SetGameCenter(v));
            UiKit.Size(UiKit.Label(column, App.GameCenter.IsAvailable
                    ? "Optional. Posts your wins, greens, and daily streak to leaderboards and unlocks achievements. The game works the same without it."
                    : "Game Center is only available in the iPhone app.",
                28f, Theme.Muted), 90f);
            if (App.GameCenter.IsAvailable)
                UiKit.Button(column, "OPEN GAME CENTER", () =>
                {
                    App.ReportGameCenter();
                    App.GameCenter.ShowDashboard();
                }, ButtonStyle.Secondary, 110f, 40f);

            UiKit.Button(column, "RESET SAVE", () =>
                UiControls.Dialog("RESET SAVE?",
                    "This deletes your career, Rise Mode progress, upgrades, and cosmetics on this device. It can't be undone.",
                    ("RESET", ButtonStyle.Primary, () =>
                    {
                        App.ResetCareer();
                        SceneFlow.GoTo(SceneNames.MainMenu);
                    }),
                    ("CANCEL", ButtonStyle.Ghost, null)),
                ButtonStyle.Ghost, 110f, 40f);

#if UNITY_EDITOR || DEBUG
            UiKit.Button(column, "DEV: UNLOCK ALL", () => App.DevUnlockAll(), ButtonStyle.Ghost, 90f, 30f);
#endif

            var credits = UiKit.Label(column, LicenseText + "\n\n<color=#8D99AE>v" + App.Version + "</color>", 30f, Theme.Cream, TextAlignmentOptions.TopLeft);
            UiKit.Size(credits, 620f);
        }

        private static void Header(Transform parent, string text) =>
            UiKit.Size(UiKit.Label(parent, text, 38f, Theme.Gold, TextAlignmentOptions.Left, true), 64f);

        private static void Save() => App.SaveCareer();
    }
}
