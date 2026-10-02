using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>Main menu: title, five mode buttons, and a strip of Caller League logos.</summary>
    public sealed class MainMenuController : ScreenBase
    {
        protected override string BackdropCourtId => "court.sunset_cage";
        protected override uint BackdropSeed => 7;
        protected override float ScrimAlpha => 0.15f;

        protected override void Build()
        {
            // Title
            var title = UiKit.ShadowLabel(Body, "CALLER\nRETRO BALL", 140f, Theme.Cream, Theme.Pink, 10f);
            UiKit.Band((RectTransform)title.transform.parent, 0.79f, 0.97f, 24f);
            title.lineSpacing = -18f;
            foreach (var t in title.transform.parent.GetComponentsInChildren<TextMeshProUGUI>()) t.lineSpacing = -18f;

            var tagline = UiKit.Label(Body, Theme.Tagline, 42f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Band(tagline.rectTransform, 0.735f, 0.785f, 24f);

            // Mode buttons
            var column = UiKit.Column(Body, 24f, null, "Modes");
            UiKit.Band(column, 0.23f, 0.71f, 110f);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            UiKit.Button(column, "PLAY", OnPlay, ButtonStyle.Primary, 150f, 64f);
            UiKit.Button(column, "RISE MODE", OnRise, ButtonStyle.Secondary, 128f);
            UiKit.Button(column, "PRACTICE LAB", OnPractice, ButtonStyle.Secondary, 128f);
            UiKit.Button(column, "LOCKER ROOM", () => SceneFlow.GoTo(SceneNames.LockerRoom), ButtonStyle.Secondary, 128f);
            UiKit.Button(column, "SETTINGS", () => SceneFlow.GoTo(SceneNames.Settings), ButtonStyle.Ghost, 116f, 48f);

            BuildLogoStrip();

            var footer = UiKit.Label(Body, "v" + App.Version + "  ·  offline  ·  no ads  ·  no purchases", 28f, Theme.Muted);
            UiKit.Band(footer.rectTransform, 0.005f, 0.045f, 24f);
        }

        private void BuildLogoStrip()
        {
            var caption = UiKit.Label(Body, DefaultContent.LeagueName.ToUpperInvariant(), 30f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Band(caption.rectTransform, 0.165f, 0.2f, 24f);

            var row = UiKit.Row(Body, 14f, "LeagueLogos");
            UiKit.Band(row, 0.08f, 0.16f, 40f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            foreach (var team in App.Catalog.TeamsInTier(TeamTier.League))
            {
                var logo = UiKit.Picture(row, TextureFactory.TeamLogo(team), "Logo " + team.abbreviation);
                UiKit.Size(logo, 104f, 104f);
            }
        }

        private void OnPlay()
        {
            App.PendingMatch = MatchRequest.QuickCallDefault(App.Catalog);
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void OnRise()
        {
            SceneFlow.GoTo(SceneNames.Season);
        }

        private void OnPractice()
        {
            App.PendingMatch = MatchRequest.PracticeDefault();
            SceneFlow.GoTo(SceneNames.Game);
        }

        // Nothing to go back to from the main menu.
        protected override void OnBack() { }
    }
}
