using CallerRetroBall.Core;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// SettingsScene. PHASE 1: lists the settings that Phase 6 will make interactive
    /// and shows the credits / asset-licence disclosure (required for release).
    /// </summary>
    public sealed class SettingsController : ScreenBase
    {
        protected override string ScreenTitle => "SETTINGS";
        protected override string BackdropCourtId => "court.overpass_park";
        protected override float ScrimAlpha => 0.75f;

        public const string LicenseText =
            "<color=#FFD166>CREDITS & LICENCES</color>\n" +
            "Caller Retro Ball is an original game. All teams, players, leagues, courts, logos, " +
            "and pixel art are original and generated procedurally inside this project.\n\n" +
            "Text font: Liberation Sans, bundled with Unity TextMeshPro (SIL Open Font License 1.1).\n" +
            "Audio: none bundled yet; any placeholder tones will be generated in-project.\n\n" +
            "No ads, analytics, tracking, accounts, or purchases. Progress is stored only on this device.";

        protected override void Build()
        {
            var planned = UiKit.Panel(Body, Color.white, Theme.PanelSprite(), true, "Planned");
            UiKit.Band(planned.rectTransform, 0.52f, 0.97f, 48f);
            var plannedText = UiKit.Label(planned.transform,
                "<color=#4CC9F0>COMING IN PHASE 6</color>\n" +
                "Music volume · SFX volume\n" +
                "Haptics · Screen shake\n" +
                "UI scale · Colourblind team contrast\n" +
                "Difficulty: Rookie / Caller / Legend\n" +
                "Reset local save (with confirmation)",
                38f, Theme.Cream);
            UiKit.Stretch(plannedText.rectTransform, 32f);

            var credits = UiKit.Panel(Body, Color.white, Theme.PanelSprite(), true, "Credits");
            UiKit.Band(credits.rectTransform, 0.04f, 0.49f, 48f);
            var creditsText = UiKit.Label(credits.transform, LicenseText + "\n\n<color=#8D99AE>v" + App.Version + "</color>",
                                          30f, Theme.Cream, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(creditsText.rectTransform, 36f);
        }
    }
}
