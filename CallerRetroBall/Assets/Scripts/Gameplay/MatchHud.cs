using System;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Match HUD: pause button, both scores (the team with the ball is highlighted), game clock,
    /// and shot clock. Only rewrites text when a value actually changes (no per-frame allocs).
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action QuitRequested;
        public event Action RematchRequested;

        private TextMeshProUGUI _teamA, _teamB, _scoreA, _scoreB, _clock, _shotClock, _toast;
        private GameObject _pausePanel;
        private GameObject _finalPanel;
        private TextMeshProUGUI _finalTitle;
        private TextMeshProUGUI _finalScore;
        private int _lastScoreA = -1, _lastScoreB = -1, _lastClock = -1, _lastShot = -1, _lastOffense = -1;
        private float _toastUntil;

        public static MatchHud Create(TeamDef a, TeamDef b)
        {
            var canvas = UiKit.CreateScreenCanvas("MatchHudCanvas", 20);
            var hud = canvas.gameObject.AddComponent<MatchHud>();
            hud.Build(UiKit.SafeArea(canvas.transform), a, b);
            return hud;
        }

        private void Build(RectTransform safe, TeamDef a, TeamDef b)
        {
            var bar = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "ScoreBar");
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-32f, 150f);
            rt.anchoredPosition = new Vector2(0f, -12f);

            var pause = UiKit.Button(rt, "II", () => PauseRequested?.Invoke(), ButtonStyle.Ghost, 110f, 44f);
            var prt = (RectTransform)pause.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(110f, 110f);
            prt.anchoredPosition = new Vector2(20f, 0f);

            _teamA = Text(rt, a.abbreviation, 40f, new Vector2(0.24f, 0.72f));
            _scoreA = Text(rt, "0", 64f, new Vector2(0.24f, 0.32f));
            _teamB = Text(rt, b.abbreviation, 40f, new Vector2(0.86f, 0.72f));
            _scoreB = Text(rt, "0", 64f, new Vector2(0.86f, 0.32f));
            _clock = Text(rt, "2:00", 56f, new Vector2(0.55f, 0.66f));
            _shotClock = Text(rt, "14", 36f, new Vector2(0.55f, 0.24f));
            _shotClock.color = Theme.Gold;

            _toast = UiKit.Label(safe, "", 44f, Theme.Cream, TextAlignmentOptions.Center, true, "Toast");
            UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(1000f, 80f));

            BuildPausePanel(safe);
            BuildFinalPanel(safe);
        }

        private static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 anchor)
        {
            var t = UiKit.Label(parent, text, size, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Place(t.rectTransform, anchor, new Vector2(260f, size * 1.3f));
            return t;
        }

        private void BuildPausePanel(RectTransform safe)
        {
            var scrim = UiKit.Panel(safe.parent, Theme.Scrim, name: "PauseScrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true; // blocks touches to the game while paused
            _pausePanel = scrim.gameObject;

            var column = UiKit.Column(scrim.transform, 28f, null, "PauseMenu");
            UiKit.Band(column, 0.35f, 0.65f, 160f);
            column.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(UiKit.ShadowLabel(column, "PAUSED", 90f, Theme.Cream, Theme.Pink, 8f).transform.parent.GetComponent<RectTransform>(), 140f);
            UiKit.Button(column, "RESUME", () => ResumeRequested?.Invoke(), ButtonStyle.Primary, 150f);
            UiKit.Button(column, "QUIT TO MENU", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 130f, 48f);
            _pausePanel.SetActive(false);
        }

        private void BuildFinalPanel(RectTransform safe)
        {
            var scrim = UiKit.Panel(safe.parent, Theme.Scrim, name: "FinalScrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            _finalPanel = scrim.gameObject;

            var column = UiKit.Column(scrim.transform, 24f, null, "FinalMenu");
            UiKit.Band(column, 0.3f, 0.7f, 140f);
            column.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _finalTitle = UiKit.ShadowLabel(column, "FINAL", 96f, Theme.Cream, Theme.Pink, 8f);
            UiKit.Size(_finalTitle.transform.parent.GetComponent<RectTransform>(), 150f);
            _finalScore = UiKit.Label(column, "", 56f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(_finalScore, 90f);
            UiKit.Size(UiKit.Label(column, "Full box score and rewards arrive in Phase 4.", 30f, Theme.Muted), 60f);
            UiKit.Button(column, "REMATCH", () => RematchRequested?.Invoke(), ButtonStyle.Primary, 150f);
            UiKit.Button(column, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 130f, 48f);
            _finalPanel.SetActive(false);
        }

        /// <summary>End-of-game card (Phase 4 extends it with stats and rewards).</summary>
        public void ShowFinal(string title, string scoreLine)
        {
            foreach (var t in _finalTitle.transform.parent.GetComponentsInChildren<TextMeshProUGUI>()) t.text = title;
            _finalScore.text = scoreLine;
            _pausePanel.SetActive(false);
            _finalPanel.SetActive(true);
        }

        public void ShowPause(bool visible) => _pausePanel.SetActive(visible);

        /// <summary>Brief centre-screen callout ("STEAL!", "BALL!").</summary>
        public void Toast(string text, float seconds = 1.2f)
        {
            _toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
        }

        public void Sync(MatchSimulation m)
        {
            if (m.Score[0] != _lastScoreA) { _lastScoreA = m.Score[0]; _scoreA.text = _lastScoreA.ToString(); }
            if (m.Score[1] != _lastScoreB) { _lastScoreB = m.Score[1]; _scoreB.text = _lastScoreB.ToString(); }

            int clock = Mathf.CeilToInt(m.GameClock);
            if (clock != _lastClock)
            {
                _lastClock = clock;
                _clock.text = m.Setup.Rules.useGameClock ? (clock / 60) + ":" + (clock % 60).ToString("00") : "--:--";
            }

            int shot = Mathf.CeilToInt(m.ShotClock);
            if (shot != _lastShot)
            {
                _lastShot = shot;
                _shotClock.text = m.Setup.Rules.shotClockSeconds >= 99f ? "" : shot.ToString();
            }

            if (m.OffenseTeam != _lastOffense)
            {
                _lastOffense = m.OffenseTeam;
                _teamA.color = m.OffenseTeam == 0 ? Theme.Gold : Theme.Cream;
                _teamB.color = m.OffenseTeam == 1 ? Theme.Gold : Theme.Cream;
                _teamA.text = (m.OffenseTeam == 0 ? "• " : "") + m.Setup.TeamA.abbreviation;
                _teamB.text = (m.OffenseTeam == 1 ? "• " : "") + m.Setup.TeamB.abbreviation;
            }

            if (_toast.text.Length > 0 && Time.unscaledTime > _toastUntil) _toast.text = "";
        }
    }
}
