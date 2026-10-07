using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>Phase 37 CLUTCH: the scenario list (three chapters, stars, goals), opened from PLAY.</summary>
    public sealed partial class MainMenuController
    {
        private static int _clutchChapter = -1;

        private void ShowClutch()
        {
            var c = App.Catalog;
            var d = App.Career.clutch ?? (App.Career.clutch = new ClutchSaveData());
            int total = Clutch.TotalStars(d);
            if (_clutchChapter < 0)
            {
                // Open on the newest chapter you can play.
                _clutchChapter = 0;
                for (int ch = 0; ch < Clutch.ChapterNames.Length; ch++) if (Clutch.ChapterOpen(d, ch)) _clutchChapter = ch;
            }
            var column = OpenOverlay("CLUTCH", out var footer);
            UiKit.Size(UiKit.Label(column, "The game is already on. Take over with the clock running down: win for a star, then go for the goal and the bonus.",
                                   28f, Theme.Cream), 110f);
            UiControls.Stat(column, "STARS", total + " / " + Clutch.MaxStars + "  ·  " + Loc.T("WON") + " " + d.won + " / " + d.played);
            UiControls.ChoiceRow(column, "CHAPTER", System.Array.ConvertAll(Clutch.ChapterNames, Loc.T), _clutchChapter, i => { _clutchChapter = i; ShowClutch(); });

            bool open = Clutch.ChapterOpen(d, _clutchChapter);
            if (!open)
            {
                int need = Clutch.ChapterStars[_clutchChapter] - total;
                UiKit.Size(UiKit.Label(column, Loc.T("Locked") + ": " + need + " " + Loc.T(need == 1 ? "more star to open this chapter." : "more stars to open this chapter."), 30f, Theme.Muted,
                                       TextAlignmentOptions.Center, true), 60f);
            }
            foreach (var s in Clutch.InChapter(_clutchChapter))
            {
                var scenario = s;
                int stars = Clutch.StarsFor(d, s.Id);
                var you = c.Team(s.YourTeamId);
                var them = c.Team(s.TheirTeamId);
                var row = UiKit.Row(column, 10f, "Scenario");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 170f);
                string starText = "<color=#FFD166>" + new string('●', stars) + "</color><color=#5C6378>" + new string('○', Clutch.StarsPerScenario - stars) + "</color>";
                var label = UiKit.Label(row,
                    "<b>" + Loc.T(s.Title) + "</b>  " + starText + "\n<size=22><color=#8D99AE>" + (you?.abbreviation ?? "?") + " vs " + (them?.abbreviation ?? "?")
                    + "  ·  " + Loc.T(Clutch.Situation(s)) + "</color>\n" + Loc.T(s.Story) + "\n<color=#4CC9F0>" + Loc.T("GOAL") + ":</color> "
                    + Loc.T(Clutch.GoalText(s.Goal, s.GoalValue)) + "  <color=#4CC9F0>" + Loc.T("BONUS") + ":</color> " + Loc.T(Clutch.GoalText(s.Bonus, s.BonusValue)) + "</size>",
                    28f, open ? Theme.Cream : Theme.Muted, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.Normal;
                UiKit.Size(label).flexibleWidth = 1f;
                var b = UiKit.Button(row, open ? (stars > 0 ? "AGAIN" : "PLAY") : "LOCKED", () =>
                {
                    var request = Clutch.Request(App.Catalog, scenario, App.Career.settings.difficultyId);
                    if (request == null) return;
                    App.PendingMatch = request;
                    SceneFlow.GoTo(SceneNames.Game);
                }, open ? (stars > 0 ? ButtonStyle.Secondary : ButtonStyle.Primary) : ButtonStyle.Ghost, 110f, 24f);
                UiKit.Size(b, 110f, 170f);
                b.interactable = open;
            }
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
        }
    }
}
