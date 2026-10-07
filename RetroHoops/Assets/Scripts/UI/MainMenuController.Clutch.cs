using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
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
            // Phase 38: today's scenario, and the editor / codes.
            int today = App.Today;
            var daily = Clutch.DailyFor(today);
            bool dailyDone = Clutch.DailyDone(d, today);
            Mode(column, "DAILY CLUTCH", Loc.T(daily.Title) + "  ·  " + (dailyDone ? Loc.T("Done for today") + " √" : "+" + Clutch.DailyBonusSp + " SP")
                 + "  ·  " + Loc.T("RUN") + " " + Clutch.DailyRun(d, today) + " (" + Loc.T("best") + " " + d.dailyBest + ")", () =>
            {
                var request = Clutch.DailyRequest(App.Catalog, App.Today, App.Career.settings.difficultyId);
                if (request == null) return;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, dailyDone ? ButtonStyle.Secondary : ButtonStyle.Primary);
            Mode(column, "MAKE YOUR OWN", Loc.T("Set the teams, score, clock and goals. Share the code with friends."), ShowClutchEditor, ButtonStyle.Secondary);
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
                UiKit.Size(row, 200f);
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
    
        // ------------------------------------------------------------------ Phase 38: the CLUTCH editor

        private static ClutchScenario _custom;

        private TextMeshProUGUI _clutchSummary, _clutchCode, _clutchMessage;

        private void ShowClutchEditor()
        {
            var c = App.Catalog;
            var league = c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (_custom == null)
                _custom = Clutch.Make(league[0].id, league[1].id, false, 30, 18, 20, true, ClutchGoal.Threes, 1, ClutchGoal.NoTurnovers, 0);
            var s = _custom;
            var column = OpenOverlay("MAKE YOUR OWN", out var footer);
            var names = league.ConvertAll(t => t.FullName.ToUpperInvariant()).ToArray();
            // Every control updates the scenario in place; only the summary and code lines are redrawn (the list keeps its scroll).
            UiControls.ChoiceRow(column, "YOUR TEAM", names, league.FindIndex(t => t.id == s.YourTeamId), i => { s.YourTeamId = league[i].id; RefreshClutchEditor(); });
            UiControls.ChoiceRow(column, "THEM", names, league.FindIndex(t => t.id == s.TheirTeamId), i => { s.TheirTeamId = league[i].id; RefreshClutchEditor(); });
            UiControls.ChoiceRow(column, "COURT", new[] { "HALF COURT", "FULL COURT" }, s.FullCourt ? 1 : 0, i => { s.FullCourt = i == 1; RefreshClutchEditor(); });
            Stepper(column, "CLOCK", () => Clutch.Clock(s.Clock), () => s.Clock = System.Math.Max(Clutch.CustomMinClock, s.Clock - 5), () => s.Clock = System.Math.Min(Clutch.CustomMaxClock, s.Clock + 5));
            Stepper(column, "YOU", () => s.ScoreFor.ToString(), () => s.ScoreFor = System.Math.Max(0, s.ScoreFor - 1), () => s.ScoreFor = System.Math.Min(Clutch.CustomMaxScore, s.ScoreFor + 1));
            Stepper(column, "THEM", () => s.ScoreAgainst.ToString(), () => s.ScoreAgainst = System.Math.Max(0, s.ScoreAgainst - 1), () => s.ScoreAgainst = System.Math.Min(Clutch.CustomMaxScore, s.ScoreAgainst + 1));
            UiControls.ChoiceRow(column, "BALL", new[] { "YOUR BALL", "THEIR BALL" }, s.YourBall ? 0 : 1, i => { s.YourBall = i == 0; RefreshClutchEditor(); });
            var goals = (ClutchGoal[])System.Enum.GetValues(typeof(ClutchGoal));
            var goalNames = System.Array.ConvertAll(goals, GoalName);
            UiControls.ChoiceRow(column, "GOAL", goalNames, System.Array.IndexOf(goals, s.Goal), i => { s.Goal = goals[i]; RefreshClutchEditor(); });
            Stepper(column, "GOAL N", () => s.GoalValue.ToString(), () => s.GoalValue = System.Math.Max(0, s.GoalValue - 1), () => s.GoalValue = System.Math.Min(31, s.GoalValue + 1));
            UiControls.ChoiceRow(column, "BONUS", goalNames, System.Array.IndexOf(goals, s.Bonus), i => { s.Bonus = goals[i]; RefreshClutchEditor(); });
            Stepper(column, "BONUS N", () => s.BonusValue.ToString(), () => s.BonusValue = System.Math.Max(0, s.BonusValue - 1), () => s.BonusValue = System.Math.Min(31, s.BonusValue + 1));

            _clutchSummary = UiKit.Label(column, "", 26f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(_clutchSummary, 100f);
            _clutchCode = UiKit.Label(column, "", 34f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(_clutchCode, 60f);
            UiKit.Button(column, "COPY CODE", () =>
            {
                string copy = Clutch.Encode(App.Catalog, _custom);
                if (copy == null) { _clutchMessage.text = Loc.T("Pick two different teams."); return; }
                GUIUtility.systemCopyBuffer = copy;
                _clutchMessage.text = Loc.T("Copied. Send it to a friend: they copy it and tap ENTER A CODE.");
            }, ButtonStyle.Secondary, 90f, 32f);
            UiKit.Button(column, "ENTER A CODE", () =>
            {
                if (Clutch.TryDecode(App.Catalog, GUIUtility.systemCopyBuffer ?? "", out var pasted, out string why))
                {
                    _custom = pasted;
                    ShowClutchEditor();
                    _clutchMessage.text = Loc.T("Loaded the code you copied.");
                }
                else _clutchMessage.text = Loc.T("Copy a CLUTCH code first, then tap ENTER A CODE.") + (why != null ? " " + Loc.T(why) : "");
            }, ButtonStyle.Ghost, 90f, 32f);
            _clutchMessage = UiKit.Label(column, "", 26f, Theme.Cyan, TextAlignmentOptions.Center);
            UiKit.Size(_clutchMessage, 70f);
            RefreshClutchEditor();

            UiKit.Button(footer, "BACK", ShowClutch, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "PLAY", () =>
            {
                if (_custom.TheirTeamId == _custom.YourTeamId) { _clutchMessage.text = Loc.T("Pick two different teams."); return; }
                var request = Clutch.CustomRequest(App.Catalog, _custom, App.Career.settings.difficultyId);
                if (request == null) return;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);
        }

        private void RefreshClutchEditor()
        {
            var s = _custom;
            if (s == null || _clutchSummary == null) return;
            bool same = s.TheirTeamId == s.YourTeamId;
            _clutchSummary.text = Loc.T(Clutch.Situation(s)) + "\n" + Loc.T("GOAL") + ": " + Clutch.GoalText(s.Goal, s.GoalValue) + "  ·  "
                                  + Loc.T("BONUS") + ": " + Clutch.GoalText(s.Bonus, s.BonusValue);
            string code = same ? null : Clutch.Encode(App.Catalog, s);
            _clutchCode.text = same ? Loc.T("Pick two different teams.") : Loc.T("CODE") + "  <color=#FFD166>" + Clutch.Pretty(code) + "</color>";
        }

        private static string GoalName(ClutchGoal g)
        {
            switch (g)
            {
                case ClutchGoal.WinBy: return "WIN BY";
                case ClutchGoal.HoldTo: return "HOLD THEM TO";
                case ClutchGoal.YouScore: return "YOU SCORE";
                case ClutchGoal.Threes: return "THREES";
                case ClutchGoal.Assists: return "ASSISTS";
                case ClutchGoal.NoTurnovers: return "NO TURNOVERS";
                case ClutchGoal.Steals: return "STEALS";
                case ClutchGoal.Blocks: return "BLOCKS";
                default: return "JUST WIN";
            }
        }

        /// <summary>Label, then - value + (taps change the value in place).</summary>
        private void Stepper(Transform parent, string label, System.Func<string> value, System.Action down, System.Action up)
        {
            var row = UiKit.Row(parent, 10f, "Stepper " + label);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(row, 96f);
            var text = UiKit.Label(row, label, 34f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Size(text).flexibleWidth = 1f;
            TextMeshProUGUI shown = null;
            UiKit.Size(UiKit.Button(row, "-", () => { down(); shown.text = value(); RefreshClutchEditor(); }, ButtonStyle.Secondary, 86f, 40f), 86f, 110f);
            shown = UiKit.Label(row, value(), 36f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(shown, 86f, 150f);
            UiKit.Size(UiKit.Button(row, "+", () => { up(); shown.text = value(); RefreshClutchEditor(); }, ButtonStyle.Secondary, 86f, 40f), 86f, 110f);
        }
    }
}
