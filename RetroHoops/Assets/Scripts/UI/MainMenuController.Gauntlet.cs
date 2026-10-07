using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;

namespace CallerRetroBall.UI
{
    /// <summary>EVENTS ► SKILLS GAUNTLET (Phase 34): today's four drills, your run so far, and your bests.</summary>
    public sealed partial class MainMenuController
    {
        private void ShowGauntlet()
        {
            var g = App.Career.gauntlet ?? (App.Career.gauntlet = new GauntletSaveData());
            int day = App.Today;
            var stations = Gauntlet.For(day);
            int next = Gauntlet.NextStation(g, day);
            var column = OpenOverlay("SKILLS GAUNTLET", out var footer);
            UiKit.Size(UiKit.Label(column, "Four drills back to back. Every drill's result turns into points. The same four for everyone today; run it as often as you like.",
                                   28f, Theme.Muted, TextAlignmentOptions.Center), 110f);
            for (int i = 0; i < Gauntlet.Stations; i++)
            {
                bool done = g.day == day && i < g.points.Count;
                string line = (i + 1) + ".  " + Gauntlet.StationName(stations[i]) + (done ? "   <color=#4CC9F0>" + g.points[i] + "</color>" : "");
                UiKit.Size(UiKit.Label(column, line, 34f, i == next ? Theme.Gold : (done ? Theme.Cream : Theme.Muted), TextAlignmentOptions.Left, true), 56f);
            }
            if (g.day == day && g.points.Count > 0)
                UiKit.Size(UiKit.Label(column, "THIS RUN: " + Gauntlet.Total(g), 40f, Theme.Cream, TextAlignmentOptions.Center, true), 64f);
            UiKit.Size(UiKit.Label(column, "TODAY'S BEST " + (g.bestDay == day ? g.todayBest : 0) + "  ·  ALL-TIME BEST " + g.best + "  ·  RUNS " + g.runs,
                                   28f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);

            // Phase 36: how your friends did (Game Center, friends only).
            if (App.GameCenter != null && App.GameCenter.IsSignedIn)
                UiKit.Button(column, "FRIENDS' BEST RUNS", () => App.GameCenter.ShowLeaderboard(Gauntlet.LeaderboardId, true), ButtonStyle.Secondary, 100f, 34f);
            else
                UiKit.Size(UiKit.Label(column, "Sign in to Game Center (Settings) to compare runs with friends.", 24f, Theme.Muted, TextAlignmentOptions.Center), 44f);

            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);
            bool inRun = g.day == day && next > 0 && next < Gauntlet.Stations;
            if (inRun)
                UiKit.Button(footer, "CONTINUE", () => StartGauntletStation(day, next), ButtonStyle.Primary, 130f);
            UiKit.Button(footer, inRun ? "RESTART" : "START", () =>
            {
                Gauntlet.Start(g, day);
                App.SaveCareer();
                StartGauntletStation(day, 0);
            }, inRun ? ButtonStyle.Ghost : ButtonStyle.Primary, 130f, inRun ? 36f : 44f);
        }

        private static void StartGauntletStation(int day, int station)
        {
            App.PendingMatch = Gauntlet.Request(day, station);
            SceneFlow.GoTo(SceneNames.Game);
        }
    }
}
