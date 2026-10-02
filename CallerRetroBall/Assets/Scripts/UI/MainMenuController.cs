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
        private Texture2D _logoTex;
        private static bool _tutorialOffered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _tutorialOffered = false;

        /// <summary>Starts the how-to-play tutorial (also reachable from Settings).</summary>
        public static void StartTutorial()
        {
            var request = MatchRequest.PracticeDefault();
            request.Mode = GameMode.Tutorial;
            App.PendingMatch = request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void OnDestroy()
        {
            if (_logoTex != null) Destroy(_logoTex);
        }

        protected override void Build()
        {
            var career = App.Career;
            var strip = UiKit.Label(Body, career.nickname.ToUpperInvariant() + "   <color=#FFD166>" + career.signalPoints + " SP</color>   <color=#4CC9F0>" + career.fans + " FANS</color>",
                                    32f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Band(strip.rectTransform, 0.965f, 1f, 24f);

            // Pixel-art "RETROBALL" logo (drawn in code), scaled with crisp pixels.
            var logoTex = _logoTex = Utilities.TextureFactory.ToTexture(Logic.PixelArt.TitleLogoGenerator.Generate(), "ui.title.logo");
            var holder = UiKit.NewRect("TitleLogo", Body);
            UiKit.Band(holder, 0.75f, 0.955f, 32f);
            var logo = UiKit.Picture(holder, logoTex, "Logo");
            UiKit.Stretch(logo.rectTransform);
            var fit = logo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = logoTex.width / (float)logoTex.height;

            var column = UiKit.Column(Body, 24f, null, "Modes");
            UiKit.Band(column, 0.23f, 0.71f, 110f);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            UiKit.Button(column, "PLAY", ShowPlayMenu, ButtonStyle.Primary, 150f, 64f);
            UiKit.Button(column, "RISE MODE", () => SceneFlow.GoTo(SceneNames.Season), ButtonStyle.Secondary, 120f, 52f);
            UiKit.Button(column, "PRACTICE LAB", ShowPractice, ButtonStyle.Secondary, 120f, 52f);
            UiKit.Button(column, "LOCKER ROOM", () => SceneFlow.GoTo(SceneNames.LockerRoom), ButtonStyle.Secondary, 120f, 52f);
            UiKit.Button(column, "SETTINGS", () => SceneFlow.GoTo(SceneNames.Settings), ButtonStyle.Ghost, 104f, 46f);

            BuildLogoStrip();

            var footer = UiKit.Label(Body, "v" + App.Version + "  ·  offline  ·  no ads  ·  no purchases", 28f, Theme.Muted);
            UiKit.Band(footer.rectTransform, 0.005f, 0.045f, 24f);

            if (App.OpenClassicOnMenu)
            {
                App.OpenClassicOnMenu = false;
                ShowClassic();
            }

            // First-time players are offered the tutorial once per session until they finish it.
            if (!App.Career.tutorialDone && App.Career.totals.games == 0 && !_tutorialOffered && _overlay == null)
            {
                _tutorialOffered = true;
                UiControls.Dialog("NEW TO RETROBALL?", "Learn the controls in about two minutes: move, shoot, pass, call plays, and defend.",
                                  ("PLAY TUTORIAL", ButtonStyle.Primary, StartTutorial),
                                  ("MAYBE LATER", ButtonStyle.Ghost, null));
            }

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

        private RectTransform OpenOverlay(string title) => OpenOverlay(title, out _);

        /// <summary>
        /// Opens a modal panel. <paramref name="footer"/> is a bar pinned to the bottom of the panel
        /// for the main buttons, so they stay visible on any screen shape; everything else scrolls.
        /// </summary>
        private RectTransform OpenOverlay(string title, out RectTransform footer)
        {
            CloseOverlay();
            var canvas = UiKit.CreateScreenCanvas("Overlay", 30);
            _overlay = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.04f, 0.96f, 48f);
            panel.raycastTarget = true;

            const float footerHeight = 170f;
            footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, footerHeight - 30f);
            footer.anchoredPosition = new Vector2(0f, 24f);

            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, footerHeight);
            var column = UiKit.ScrollColumn(body, 20f, new RectOffset(40, 40, 30, 20));
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
            var column = OpenOverlay("QUICK CALL", out var footer);
            var league = c.TeamsInTier(TeamTier.League);
            var mine = league.FindAll(t => t.unlockedByDefault);
            int myIndex = 0;
            int oppIndex = 0;
            TextMeshProUGUI oppLabel = null; // assigned below; declared first so Refresh() can see it
            var opponents = new List<TeamDef>();

            UiKit.Size(UiKit.Label(column, "PICK YOUR TEAM", 36f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);
            var teamRow = UiKit.Row(column, 20f, "Teams");
            UiKit.Size(teamRow, 250f);
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

            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
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
            }, ButtonStyle.Primary, 130f);
            UiKit.Size(UiKit.Label(column, "Touch: stick + SHOOT / PASS / DEF / CALL\nKeyboard: WASD move · K shoot (hold) · J pass · L steal · C call · Esc pause",
                                   28f, Theme.Muted), 90f);

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

        /// <summary>PLAY: every way to start a game.</summary>
        private void ShowPlayMenu()
        {
            var column = OpenOverlay("PLAY", out var footer);
            Mode(column, "QUICK CALL", "Pick a team and an opponent. One game.", ShowQuickCall, ButtonStyle.Primary);

            var today = DailyChallenges.For(App.Today, App.Catalog);
            bool done = DailyChallenges.CompletedToday(App.Career.daily, App.Today);
            int streak = DailyChallenges.LiveStreak(App.Career.daily, App.Today);
            Mode(column, "DAILY CHALLENGE", (done ? "Done for today ✓" : today.Describe()) + "  ·  streak " + streak, ShowDaily, ButtonStyle.Secondary);
            Mode(column, "2 PLAYER", "Head to head on one device: keyboard or two controllers.", ShowVersus, ButtonStyle.Secondary);
            Mode(column, "FIRST CALL CLASSIC", "Four-team knockout. Titles won: " + App.Career.classic.titles, ShowClassic, ButtonStyle.Secondary);
            Mode(column, "HOW TO PLAY", "Two-minute guided tutorial.", StartTutorial, ButtonStyle.Ghost);
            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);
        }

        private static void Mode(Transform column, string name, string detail, System.Action onClick, ButtonStyle style)
        {
            UiKit.Button(column, name, onClick, style, 120f, 50f);
            UiKit.Size(UiKit.Label(column, detail, 28f, Theme.Muted), 44f);
        }

        /// <summary>Today's Daily Challenge: goal, matchup, streak.</summary>
        private void ShowDaily()
        {
            var c = App.Catalog;
            var career = App.Career;
            int day = App.Today;
            var d = DailyChallenges.For(day, c);
            var column = OpenOverlay("DAILY CHALLENGE", out var footer);
            UiKit.Size(UiKit.Label(column, d.Describe().ToUpperInvariant(), 48f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            var home = c.Team(d.HomeTeamId);
            var opp = c.Team(d.OpponentId);
            UiKit.Size(UiKit.Label(column, (home?.FullName ?? "?").ToUpperInvariant() + "\nVS  " + (opp?.FullName ?? "?").ToUpperInvariant(),
                                   36f, Theme.Cream, TextAlignmentOptions.Center, true), 110f);
            var diff = c.Difficulty(d.DifficultyId);
            UiControls.Stat(column, "DIFFICULTY", (diff?.displayName ?? "?").ToUpperInvariant());
            int streak = DailyChallenges.LiveStreak(career.daily, day);
            UiControls.Stat(column, "STREAK", streak + "  (best " + career.daily.bestStreak + ")");
            bool done = DailyChallenges.CompletedToday(career.daily, day);
            UiKit.Size(UiKit.Label(column, done
                ? "Done for today! A new challenge arrives tomorrow. You can still play for fun."
                : "Complete it for +" + DailyChallenges.BonusFor(streak + 1) + " SP. Keep the streak going every day for a bigger bonus.",
                30f, done ? Theme.Cyan : Theme.Muted), 100f);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "PLAY", () =>
            {
                App.PendingMatch = d.ToRequest();
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);
        }

        /// <summary>Local 2-player setup: each player picks a team.</summary>
        private void ShowVersus()
        {
            var c = App.Catalog;
            var league = c.TeamsInTier(TeamTier.League);
            int p1 = 0, p2 = 1;
            var column = OpenOverlay("2 PLAYER", out var footer);
            TextMeshProUGUI p1Label = null, p2Label = null;
            UiKit.Size(UiKit.Label(column, "PLAYER 1", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            p1Label = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(p1Label, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { p1 = Next(p1, p2); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Size(UiKit.Label(column, "PLAYER 2", 34f, Theme.Cyan, TextAlignmentOptions.Center, true), 50f);
            p2Label = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(p2Label, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { p2 = Next(p2, p1); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Size(UiKit.Label(column,
                "P1: touch, or WASD · K shoot (hold) · J pass · L steal · C call\n" +
                "P2: arrows · Num1 shoot (hold) · Num2 pass · Num3 steal · Num0 pick & roll\n" +
                "Controllers: with two, P1 gets the first; with one, it's P2's.",
                26f, Theme.Muted), 150f);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
            {
                var home = league[p1];
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.Versus,
                    HomeTeamId = home.id,
                    AwayTeamId = league[p2].id,
                    CourtId = home.homeCourtId,
                    DifficultyId = App.Career.settings.difficultyId,
                };
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);

            int Next(int current, int other)
            {
                int n = (current + 1) % league.Count;
                if (n == other) n = (n + 1) % league.Count;
                return n;
            }

            void Refresh()
            {
                p1Label.text = league[p1].FullName.ToUpperInvariant();
                p2Label.text = league[p2].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        /// <summary>First Call Classic: four-team knockout. Shows the bracket and plays the crew's next game.</summary>
        private void ShowClassic()
        {
            var c = App.Catalog;
            var career = App.Career;
            var t = career.classic;
            var column = OpenOverlay("FIRST CALL CLASSIC", out var footer);
            UiKit.Size(UiKit.Label(column, "Your First Callers vs three league teams. Win two in a row for the title.",
                                   32f, Theme.Cream), 100f);
            UiKit.Size(UiKit.Label(column, "TITLES WON: " + t.titles, 36f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);

            if (t.seeds.Count == 4)
            {
                UiKit.Size(UiKit.Label(column, "EDITION " + t.edition, 30f, Theme.Muted, TextAlignmentOptions.Center, true), 44f);
                foreach (var g in t.games)
                {
                    string label = g.round == 2 ? "FINAL" : "SEMI";
                    string home = c.Team(g.homeId)?.abbreviation ?? "?";
                    string away = c.Team(g.awayId)?.abbreviation ?? "?";
                    string line = label + "   " + home + (g.played ? "  " + g.homeScore + " - " + g.awayScore + "  " : "  vs  ") + away;
                    bool mine = g.Involves(ClassicEngine.CrewId);
                    UiKit.Size(UiKit.Label(column, line, 40f, mine ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Center, true), 60f);
                }
                if (t.finished)
                    UiKit.Size(UiKit.Label(column, "CHAMPION: " + (c.Team(t.championId)?.FullName ?? "?").ToUpperInvariant(),
                                           36f, Theme.Cyan, TextAlignmentOptions.Center, true), 60f);
            }

            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            var next = ClassicEngine.NextMatch(t, c, career.settings.difficultyId);
            if (next != null)
            {
                var opp = c.Team(next.AwayTeamId);
                UiKit.Size(UiKit.Label(column, "NEXT: " + (next.Round == 2 ? "FINAL" : "SEMIFINAL") + " VS " +
                                               (opp?.FullName ?? "?").ToUpperInvariant(), 34f, Theme.Cream, TextAlignmentOptions.Center, true), 60f);
                UiKit.Button(footer, "PLAY", () =>
                {
                    App.PendingMatch = next;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 130f);
            }
            else
            {
                UiKit.Button(footer, t.edition == 0 ? "ENTER" : "NEW CLASSIC", () =>
                {
                    ClassicEngine.Start(App.Career.classic, App.Catalog);
                    App.SaveCareer();
                    ShowClassic();
                }, ButtonStyle.Primary, 130f);
            }
        }

        private void ShowPractice()
        {
            var column = OpenOverlay("PRACTICE LAB", out var footer);
            var best = App.Career.practice;
            Drill(column, "FREE SHOOT", "60 seconds. Best: " + best.freeShootMakes + " makes, streak " + best.freeShootStreak, 0);
            Drill(column, "PASSING TARGETS", "45 seconds. Best: " + best.passingScore + " targets", 1);
            Drill(column, "DRIBBLE LANE", "Weave the cones. Best: " + (best.dribbleLaneTime > 0f ? best.dribbleLaneTime.ToString("0.00") + " s" : "—"), 2);
            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);
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
