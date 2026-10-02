using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>Main menu: career strip, title, five mode buttons, Quick Call and Practice pickers.</summary>
    public sealed class MainMenuController : ScreenBase
    {
        protected override string BackdropCourtId => "court.sunset_cage";
        protected override uint BackdropSeed => 7;
        protected override float ScrimAlpha => 0.15f;

        private GameObject _overlay;

        protected override void Build()
        {
            var career = App.Career;
            var strip = UiKit.Label(Body, career.nickname.ToUpperInvariant() + "   <color=#FFD166>" + career.signalPoints + " SP</color>   <color=#4CC9F0>" + career.fans + " FANS</color>",
                                    32f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Band(strip.rectTransform, 0.965f, 1f, 24f);

            var title = UiKit.ShadowLabel(Body, "CALLER\nRETRO BALL", 140f, Theme.Cream, Theme.Pink, 10f);
            UiKit.Band((RectTransform)title.transform.parent, 0.78f, 0.96f, 24f);
            foreach (var t in title.transform.parent.GetComponentsInChildren<TextMeshProUGUI>()) t.lineSpacing = -18f;

            var tagline = UiKit.Label(Body, Theme.Tagline, 42f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Band(tagline.rectTransform, 0.735f, 0.78f, 24f);

            var column = UiKit.Column(Body, 24f, null, "Modes");
            UiKit.Band(column, 0.23f, 0.71f, 110f);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            UiKit.Button(column, "PLAY", ShowQuickCall, ButtonStyle.Primary, 150f, 64f);
            UiKit.Button(column, "RISE MODE", () => SceneFlow.GoTo(SceneNames.Season), ButtonStyle.Secondary, 128f);
            UiKit.Button(column, "PRACTICE LAB", ShowPractice, ButtonStyle.Secondary, 128f);
            UiKit.Button(column, "LOCKER ROOM", () => SceneFlow.GoTo(SceneNames.LockerRoom), ButtonStyle.Secondary, 128f);
            UiKit.Button(column, "SETTINGS", () => SceneFlow.GoTo(SceneNames.Settings), ButtonStyle.Ghost, 116f, 48f);

            BuildLogoStrip();

            var footer = UiKit.Label(Body, "v" + App.Version + "  ·  offline  ·  no ads  ·  no purchases", 28f, Theme.Muted);
            UiKit.Band(footer.rectTransform, 0.005f, 0.045f, 24f);

            if (App.CareerLoadStatus == LoadStatus.Recovered)
                UiControls.Dialog("SAVE RESET", "Your save file couldn't be read, so a fresh career was started. A backup of the old file was kept.",
                                  ("OK", ButtonStyle.Primary, null));
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

        // ------------------------------------------------------------------ overlays

        private RectTransform OpenOverlay(string title)
        {
            CloseOverlay();
            var canvas = UiKit.CreateScreenCanvas("Overlay", 30);
            _overlay = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.08f, 0.92f, 48f);
            var column = UiKit.Column(panel.transform, 22f, new RectOffset(40, 40, 40, 40));
            UiKit.Stretch(column);
            UiKit.Size(UiKit.ShadowLabel(column, title, 64f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 100f);
            return column;
        }

        private void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
        }

        protected override void OnBack()
        {
            if (_overlay != null) CloseOverlay();
        }

        private void ShowQuickCall()
        {
            var c = App.Catalog;
            var column = OpenOverlay("QUICK CALL");
            var league = c.TeamsInTier(TeamTier.League);
            var mine = league.FindAll(t => t.unlockedByDefault);
            int myIndex = 0;
            int oppIndex = 0;
            TextMeshProUGUI oppLabel = null; // assigned below; declared first so Refresh() can see it
            var opponents = new List<TeamDef>();

            UiKit.Size(UiKit.Label(column, "PICK YOUR TEAM", 36f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);
            var teamRow = UiKit.Row(column, 20f, "Teams");
            UiKit.Size(teamRow, 300f);
            var cards = new List<Image>();
            for (int i = 0; i < mine.Count; i++)
            {
                int index = i;
                var team = mine[i];
                var card = UiKit.Button(teamRow, "", () => { myIndex = index; Refresh(); }, ButtonStyle.Secondary, 280f);
                cards.Add(card.GetComponent<Image>());
                var logo = UiKit.Picture(card.transform, TextureFactory.TeamLogo(team));
                UiKit.Place(logo.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(150f, 150f));
                var name = UiKit.Label(card.transform, team.FullName.ToUpperInvariant(), 30f, Theme.Cream, TextAlignmentOptions.Center, true);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(360f, 60f));
            }

            oppLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(oppLabel, 70f);
            UiKit.Button(column, "CHANGE OPPONENT", () => { oppIndex = (oppIndex + 1) % opponents.Count; Refresh(); }, ButtonStyle.Ghost, 100f, 36f);

            var difficulties = c.Difficulties;
            int diffIndex = Mathf.Max(0, difficulties.FindIndex(d => d.id == App.Career.settings.difficultyId));
            var names = difficulties.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray();
            UiControls.ChoiceRow(column, "DIFFICULTY", names, diffIndex, i => diffIndex = i);

            UiKit.Button(column, "TIP OFF", () =>
            {
                var home = mine[myIndex];
                var away = opponents[oppIndex];
                App.Career.settings.difficultyId = difficulties[diffIndex].id;
                App.SaveCareer();
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.QuickCall,
                    HomeTeamId = home.id,
                    AwayTeamId = away.id,
                    CourtId = home.homeCourtId,
                    DifficultyId = difficulties[diffIndex].id,
                };
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 140f);
            UiKit.Button(column, "BACK", CloseOverlay, ButtonStyle.Ghost, 100f, 40f);

            void Refresh()
            {
                for (int i = 0; i < cards.Count; i++) cards[i].color = i == myIndex ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
                opponents.Clear();
                foreach (var t in league) if (t.id != mine[myIndex].id) opponents.Add(t);
                oppIndex %= opponents.Count;
                oppLabel.text = "VS  " + opponents[oppIndex].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        private void ShowPractice()
        {
            var column = OpenOverlay("PRACTICE LAB");
            var best = App.Career.practice;
            Drill(column, "FREE SHOOT", "60 seconds. Best: " + best.freeShootMakes + " makes, streak " + best.freeShootStreak, 0);
            Drill(column, "PASSING TARGETS", "45 seconds. Best: " + best.passingScore + " targets", 1);
            Drill(column, "DRIBBLE LANE", "Weave the cones. Best: " + (best.dribbleLaneTime > 0f ? best.dribbleLaneTime.ToString("0.00") + " s" : "—"), 2);
            UiKit.Button(column, "BACK", CloseOverlay, ButtonStyle.Ghost, 100f, 40f);
        }

        private static void Drill(Transform column, string name, string detail, int drill)
        {
            UiKit.Button(column, name, () =>
            {
                var request = MatchRequest.PracticeDefault();
                request.Drill = drill;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Secondary, 130f);
            UiKit.Size(UiKit.Label(column, detail, 30f, Theme.Muted), 50f);
        }
    }
}
