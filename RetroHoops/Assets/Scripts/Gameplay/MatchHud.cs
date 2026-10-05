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
        public event Action ContinueRequested;
        public event Action<PlayCall> PlayChosen;
        public event Action ReplayRequested;
        public event Action PlayOfTheGameRequested;
        /// <summary>Make a GIF of the play of the game and share / save it.</summary>
        public event Action ShareRequested;
        public event Action PhotoRequested;

        private TextMeshProUGUI _teamA, _teamB, _scoreA, _scoreB, _clock, _shotClock, _toast;
        private GameObject _pausePanel;
        private GameObject _finalPanel;
        private TextMeshProUGUI _finalTitle;
        private TextMeshProUGUI _finalScore;
        private RectTransform _finalColumn;
        private GameObject _callMenu;
        private TextMeshProUGUI _info;
        private string _lastInfo = "";
        private int _lastScoreA = -1, _lastScoreB = -1, _lastClock = -1, _lastShot = -1, _lastOffense = -1;
        private float _toastUntil;
        private float _punchA = -10f, _punchB = -10f;

        /// <summary>Accessibility: no score bounce.</summary>
        public static bool ReduceMotion { get; set; }

        private RectTransform _bar;
        private bool _barLandscape;
        private bool _barSet;

        /// <summary>Landscape: a compact score bar in the middle (the stands and court show either side); portrait: full width.</summary>
        private void LayoutBar()
        {
            if (_bar == null) return;
            bool landscape = Screen.width > Screen.height;
            if (_barSet && landscape == _barLandscape) return;
            _barSet = true;
            _barLandscape = landscape;
            if (landscape)
            {
                _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 1f);
                _bar.sizeDelta = new Vector2(1100f, 130f);
            }
            else
            {
                _bar.anchorMin = new Vector2(0f, 1f);
                _bar.anchorMax = new Vector2(1f, 1f);
                _bar.sizeDelta = new Vector2(-32f, 150f);
            }
        }

        /// <summary>Left-handed layout: the CALL menu opens on the left, near the buttons.</summary>
        public void SetCallMenuLeft(bool left)
        {
            var rt = (RectTransform)_callMenu.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(left ? 0f : 1f, 0f);
            rt.pivot = new Vector2(left ? 0f : 1f, 0f);
            rt.anchoredPosition = new Vector2(left ? 24f : -24f, 520f);
        }
        private const float PunchSeconds = 0.3f;

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
            _bar = rt;
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
            UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(1000f, 120f));
            // Long lines (coach tips, H-O-R-S-E calls) shrink and wrap instead of spilling off screen.
            _toast.enableAutoSizing = true;
            _toast.fontSizeMin = 26f;
            _toast.fontSizeMax = 44f;

            _info = UiKit.Label(safe, "", 36f, Theme.Gold, TextAlignmentOptions.Center, true, "Info");
            _info.rectTransform.anchorMin = _info.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _info.rectTransform.pivot = new Vector2(0.5f, 1f);
            _info.rectTransform.sizeDelta = new Vector2(1000f, 60f);
            _info.enableAutoSizing = true; // long lines (controls hint) shrink to fit
            _info.fontSizeMin = 20f;
            _info.fontSizeMax = 36f;
            _info.rectTransform.anchoredPosition = new Vector2(0f, -172f);

            BuildCallMenu(safe);
            BuildReplayUi(safe);
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
            UiKit.Band(column, 0.12f, 0.88f, 160f);
            column.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(UiKit.ShadowLabel(column, "PAUSED", 90f, Theme.Cream, Theme.Pink, 8f).transform.parent.GetComponent<RectTransform>(), 140f);
            UiKit.Button(column, "RESUME", () => ResumeRequested?.Invoke(), ButtonStyle.Primary, 150f);
            UiKit.Button(column, "PHOTO MODE", () => PhotoRequested?.Invoke(), ButtonStyle.Secondary, 130f, 48f);
            UiKit.Button(column, "QUIT TO MENU", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 130f, 48f);
            _pausePanel.SetActive(false);
        }

        private GameObject _replayButton;
        private float _replayButtonUntil;
        private GameObject _replayOverlay;

        private void BuildReplayUi(RectTransform safe)
        {
            var b = UiKit.Button(safe, "REPLAY", () =>
            {
                _replayButton.SetActive(false);
                ReplayRequested?.Invoke();
            }, ButtonStyle.Secondary, 90f, 34f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.66f);
            rt.sizeDelta = new Vector2(320f, 90f);
            _replayButton = b.gameObject;
            _replayButton.SetActive(false);

            // Letterbox bars + label shown while a replay plays.
            var overlay = UiKit.NewRect("ReplayOverlay", safe.parent);
            UiKit.Stretch(overlay);
            var top = UiKit.Panel(overlay, Color.black, name: "Top");
            top.rectTransform.anchorMin = new Vector2(0f, 0.9f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            var bottom = UiKit.Panel(overlay, Color.black, name: "Bottom");
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.1f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;
            var label = UiKit.Label(top.transform, "REPLAY", 48f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Stretch(label.rectTransform);
            var tapHint = UiKit.Label(bottom.transform, "TAP TO SKIP", 30f, Theme.Muted, TextAlignmentOptions.Center, true);
            UiKit.Stretch(tapHint.rectTransform);
            _replayOverlay = overlay.gameObject;
            _replayOverlay.SetActive(false);
        }

        /// <summary>Offers a REPLAY button for a few seconds after a big play.</summary>
        public void OfferReplay(float seconds)
        {
            _replayButton.SetActive(true);
            _replayButtonUntil = Time.unscaledTime + seconds;
        }

        public void ShowReplayOverlay(bool visible)
        {
            _replayOverlay.SetActive(visible);
            if (visible) _replayButton.SetActive(false);
            _finalPanel.SetActive(!visible && _finalShownOnce);
        }

        private bool _finalShownOnce;

        private void BuildCallMenu(RectTransform safe)
        {
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "CallMenu");
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(460f, 520f);
            rt.anchoredPosition = new Vector2(-24f, 520f);
            var column = UiKit.Column(rt, 14f, new RectOffset(20, 20, 20, 20));
            UiKit.Stretch(column);
            UiKit.Size(UiKit.Label(column, "CALL A PLAY", 36f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);
            UiKit.Button(column, "PICK & ROLL", () => Choose(PlayCall.PickAndRoll), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "GIVE & GO", () => Choose(PlayCall.GiveAndGo), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "CLEAR OUT", () => Choose(PlayCall.ClearOut), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "CANCEL", () => ShowCallMenu(false), ButtonStyle.Ghost, 80f, 30f);
            _callMenu = panel.gameObject;
            _callMenu.SetActive(false);
        }

        private void Choose(PlayCall play)
        {
            ShowCallMenu(false);
            PlayChosen?.Invoke(play);
        }

        public bool CallMenuOpen => _callMenu != null && _callMenu.activeSelf;

        public void ShowCallMenu(bool visible)
        {
            if (_callMenu != null) _callMenu.SetActive(visible);
        }

        /// <summary>Small line under the score bar (drill timer, BOX OUT, objective).</summary>
        public void SetInfo(string text)
        {
            text = text ?? "";
            if (text == _lastInfo) return;
            _lastInfo = text;
            _info.text = Loc.T(text);
        }

        private void BuildFinalPanel(RectTransform safe)
        {
            var scrim = UiKit.Panel(safe.parent, Theme.Scrim, name: "FinalScrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            _finalPanel = scrim.gameObject;
            var inner = UiKit.SafeArea(scrim.transform);
            var holder = UiKit.NewRect("FinalHolder", inner);
            UiKit.Band(holder, 0.04f, 0.96f, 40f);
            _finalColumn = UiKit.ScrollColumn(holder, 18f, new RectOffset(0, 0, 20, 20));
            _finalPanel.SetActive(false);
        }

        private void ClearFinal()
        {
            for (int i = _finalColumn.childCount - 1; i >= 0; i--) Destroy(_finalColumn.GetChild(i).gameObject);
        }

        private void Title(string title, string subtitle)
        {
            _finalTitle = UiKit.ShadowLabel(_finalColumn, title, 96f, Theme.Cream, Theme.Pink, 8f);
            UiKit.Size(_finalTitle.transform.parent.GetComponent<RectTransform>(), 140f);
            _finalScore = UiKit.Label(_finalColumn, subtitle, 52f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(_finalScore, 80f);
        }

        /// <summary>Simple end card (no stats).</summary>
        public void ShowFinal(string title, string scoreLine)
        {
            ClearFinal();
            Title(title, scoreLine);
            UiKit.Button(_finalColumn, "REMATCH", () => RematchRequested?.Invoke(), ButtonStyle.Primary, 140f);
            UiKit.Button(_finalColumn, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 120f, 48f);
            Open();
        }

        /// <summary>
        /// Post-game: score, box score (PTS AST REB STL BLK FG%), player of the game, rewards, and
        /// Rematch / Continue / Home. <paramref name="continueLabel"/> null hides Continue.
        /// </summary>
        public void ShowPostGame(string title, MatchSummary s, RewardGrant grant, bool rewarded, string note, string continueLabel, bool allowRematch)
        {
            ClearFinal();
            Title(title, s.teamAName.ToUpperInvariant() + "  " + s.scoreA + " - " + s.scoreB + "  " + s.teamBName.ToUpperInvariant());

            var pog = s.Line(s.playerOfTheGame);
            if (pog != null)
                UiKit.Size(UiKit.Label(_finalColumn, "PLAYER OF THE GAME  <color=#FFD166>" + pog.name.ToUpperInvariant() + "</color>  " +
                                       pog.stats.points + " PTS", 34f, Theme.Cream, TextAlignmentOptions.Center, true), 56f);

            BoxScore(s, 0);
            BoxScore(s, 1);

            if (rewarded && (grant.signalPoints > 0 || grant.fans > 0))
            {
                var rewards = UiKit.Label(_finalColumn, "", 48f, Theme.Cyan, TextAlignmentOptions.Center, true);
                UiKit.Size(rewards, 76f);
                StartCoroutine(CountUp(rewards, grant.signalPoints, grant.fans));
            }
            if (!string.IsNullOrEmpty(note))
                UiKit.Size(UiKit.Label(_finalColumn, note, 36f, Theme.Gold, TextAlignmentOptions.Center, true), 110f);

            if (PlayOfTheGameRequested != null && HasPlayOfTheGame)
            {
                UiKit.Button(_finalColumn, "PLAY OF THE GAME", () => PlayOfTheGameRequested?.Invoke(), ButtonStyle.Secondary, 110f, 40f);
                if (ShareRequested != null)
                    UiKit.Button(_finalColumn, "SHARE HIGHLIGHT", () => ShareRequested?.Invoke(), ButtonStyle.Ghost, 100f, 36f);
            }
            if (continueLabel != null)
                UiKit.Button(_finalColumn, continueLabel, () => ContinueRequested?.Invoke(), ButtonStyle.Primary, 140f);
            if (allowRematch)
                UiKit.Button(_finalColumn, "REMATCH", () => RematchRequested?.Invoke(), continueLabel == null ? ButtonStyle.Primary : ButtonStyle.Secondary, 120f, 48f);
            UiKit.Button(_finalColumn, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 110f, 44f);
            Open();
        }

        private void BoxScore(MatchSummary s, int team)
        {
            UiKit.Size(UiKit.Label(_finalColumn, (team == 0 ? s.teamAName : s.teamBName).ToUpperInvariant(), 34f,
                                   team == s.humanTeam ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true), 50f);
            UiKit.Size(UiKit.Label(_finalColumn, Row("", "PTS", "AST", "REB", "STL", "BLK", "FG%"), 28f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            foreach (var line in s.lines)
            {
                if (line.team != team) continue;
                var st = line.stats;
                string fg = st.fieldGoalsAttempted == 0 ? "-" : Mathf.RoundToInt(st.FieldGoalPercentage * 100f).ToString();
                string name = (line.isHuman ? "» " : "") + line.name.ToUpperInvariant();
                UiKit.Size(UiKit.Label(_finalColumn, Row(name, st.points.ToString(), st.assists.ToString(), st.rebounds.ToString(),
                                                         st.steals.ToString(), st.blocks.ToString(), fg), 30f,
                                       line.isHuman ? Theme.Cyan : Theme.Cream, TextAlignmentOptions.Left, false), 44f);
            }
        }

        // Fixed columns via TMP <pos> tags (percent of the line width).
        private static string Row(string name, string a, string b, string c, string d, string e, string f) =>
            name + "<pos=44%>" + a + "<pos=53%>" + b + "<pos=62%>" + c + "<pos=71%>" + d + "<pos=80%>" + e + "<pos=90%>" + f;

        /// <summary>Ticks the reward line up from zero (presentation only; the career is already saved).</summary>
        private System.Collections.IEnumerator CountUp(TextMeshProUGUI label, int sp, int fans)
        {
            const float seconds = 0.9f;
            float start = Time.unscaledTime;
            while (true)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
                float e = 1f - (1f - u) * (1f - u);
                label.text = "+" + Mathf.RoundToInt(sp * e) + " SP    +" + Mathf.RoundToInt(fans * e) + " FANS";
                if (u >= 1f) yield break;
                yield return null;
            }
        }

        /// <summary>Practice end card.</summary>
        public void ShowPracticeEnd(string title, string result, bool newBest, string againLabel = "RUN IT BACK")
        {
            ClearFinal();
            Title(title, result);
            if (newBest) UiKit.Size(UiKit.Label(_finalColumn, "NEW PERSONAL BEST!", 48f, Theme.Cyan, TextAlignmentOptions.Center, true), 76f);
            UiKit.Button(_finalColumn, againLabel, () => RematchRequested?.Invoke(), ButtonStyle.Primary, 140f);
            UiKit.Button(_finalColumn, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 110f, 44f);
            Open();
        }

        /// <summary>Set by the match controller when there's a highlight to show on the post-game card.</summary>
        public bool HasPlayOfTheGame { get; set; }

        private void Open()
        {
            _finalShownOnce = true;
            if (_replayButton != null) _replayButton.SetActive(false);
            ShowCallMenu(false);
            _pausePanel.SetActive(false);
            _finalPanel.SetActive(true);
        }

        public void ShowPause(bool visible) => _pausePanel.SetActive(visible);

        /// <summary>Hides the whole HUD (photo mode) without changing what's open.</summary>
        public void SetHudVisible(bool visible)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }

        /// <summary>Brief centre-screen callout ("STEAL!", "BALL!").</summary>
        public void Toast(string text, float seconds = 1.2f)
        {
            _toast.text = Loc.T(text);
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            LayoutBar();
            if (_replayButton != null && _replayButton.activeSelf && Time.unscaledTime > _replayButtonUntil) _replayButton.SetActive(false);
            Punch(_scoreA, _punchA);
            Punch(_scoreB, _punchB);
        }

        /// <summary>Score bounce: pops to 140 % and settles back over 0.3 s.</summary>
        private static void Punch(TextMeshProUGUI label, float since)
        {
            float t = ReduceMotion ? 1f : (Time.unscaledTime - since) / PunchSeconds;
            float scale = t >= 1f || t < 0f ? 1f : 1f + 0.4f * (1f - t) * (1f - t);
            label.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        public void Sync(MatchSimulation m)
        {
            if (m.Score[0] != _lastScoreA)
            {
                if (_lastScoreA >= 0) _punchA = Time.unscaledTime;
                _lastScoreA = m.Score[0];
                _scoreA.text = _lastScoreA.ToString();
            }
            if (m.Score[1] != _lastScoreB)
            {
                if (_lastScoreB >= 0) _punchB = Time.unscaledTime;
                _lastScoreB = m.Score[1];
                _scoreB.text = _lastScoreB.ToString();
            }

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
