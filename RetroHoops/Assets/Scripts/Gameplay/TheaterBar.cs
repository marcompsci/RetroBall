using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Phase 35 REPLAY THEATER controls under a game tape: the scrub bar (with a tick for every mark), the clock, and
    /// buttons for previous / next mark, slower / pause / faster, camera, ADD MARK and the list of marks.
    /// The screen does the work (<see cref="TapeTheater"/>); this only shows it and reports taps.
    /// </summary>
    public sealed class TheaterBar : MonoBehaviour
    {
        public event Action PlayPause, Slower, Faster, NextCamera, AddMark, PreviousMark, NextMark;
        /// <summary>The scrub bar was let go at this fraction of the tape (0..1).</summary>
        public event Action<float> Scrubbed;
        public event Action<TapeMark> MarkPicked;

        private GameObject _root;
        private Slider _slider;
        private RectTransform _ticks;
        private TextMeshProUGUI _clock, _status, _play;
        private GameObject _list;
        private bool _dragging;
        private int _marksShown = -1;
        private float _lastFraction = -1f;

        public bool Dragging => _dragging;

        public static TheaterBar Create()
        {
            var canvas = UiKit.CreateScreenCanvas("TheaterCanvas", 21);
            var bar = canvas.gameObject.AddComponent<TheaterBar>();
            bar.Build(UiKit.SafeArea(canvas.transform));
            return bar;
        }

        private void Build(RectTransform safe)
        {
            var panel = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.82f), name: "TheaterBar");
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-24f, 330f);
            rt.anchoredPosition = new Vector2(0f, 12f);
            _root = panel.gameObject;

            _clock = UiKit.Label(rt, "0:00 / 0:00", 30f, Theme.Cream, TextAlignmentOptions.Left, true, "Clock");
            Anchor(_clock.rectTransform, 0.03f, 0.80f, 0.45f, 0.97f);
            _status = UiKit.Label(rt, "", 26f, Theme.Gold, TextAlignmentOptions.Right, true, "Status");
            Anchor(_status.rectTransform, 0.40f, 0.80f, 0.97f, 0.97f);
            _status.enableAutoSizing = true;
            _status.fontSizeMin = 16f;
            _status.fontSizeMax = 26f;

            // Scrub bar.
            var sliderRt = UiKit.NewRect("Scrub", rt);
            Anchor(sliderRt, 0.04f, 0.60f, 0.96f, 0.76f);
            var bg = UiKit.Panel(sliderRt, Theme.InkLight, name: "Background");
            UiKit.Stretch(bg.rectTransform);
            _ticks = UiKit.NewRect("Marks", sliderRt);
            UiKit.Stretch(_ticks);
            var fillArea = UiKit.NewRect("Fill Area", sliderRt);
            UiKit.Stretch(fillArea);
            var fill = UiKit.Panel(fillArea, new Color(Theme.Cyan.r, Theme.Cyan.g, Theme.Cyan.b, 0.55f), name: "Fill");
            UiKit.Stretch(fill.rectTransform);
            var handleArea = UiKit.NewRect("Handle Slide Area", sliderRt);
            UiKit.Stretch(handleArea);
            var handle = UiKit.Panel(handleArea, Color.white, Theme.DiscSprite(Theme.Cream, Theme.Shadow), false, "Handle");
            handle.rectTransform.sizeDelta = new Vector2(56f, 56f);
            handle.raycastTarget = true;
            _slider = sliderRt.gameObject.AddComponent<Slider>();
            _slider.fillRect = fill.rectTransform;
            _slider.handleRect = handle.rectTransform;
            _slider.targetGraphic = handle;
            _slider.direction = Slider.Direction.LeftToRight;
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            var grab = sliderRt.gameObject.AddComponent<ScrubGrab>();
            grab.Down = () => _dragging = true;
            grab.Up = () =>
            {
                _dragging = false;
                Scrubbed?.Invoke(_slider.value);
            };

            // Buttons: two rows.
            var row1 = UiKit.Row(rt, 10f, "Transport");
            Anchor(row1, 0.02f, 0.31f, 0.98f, 0.56f);
            Small(row1, "< MARK", () => PreviousMark?.Invoke());
            Small(row1, "SLOWER", () => Slower?.Invoke());
            _play = Small(row1, "PAUSE", () => PlayPause?.Invoke(), ButtonStyle.Primary).GetComponentInChildren<TextMeshProUGUI>();
            Small(row1, "FASTER", () => Faster?.Invoke());
            Small(row1, "MARK >", () => NextMark?.Invoke());
            var row2 = UiKit.Row(rt, 10f, "Tools");
            Anchor(row2, 0.02f, 0.04f, 0.98f, 0.29f);
            Small(row2, "CAMERA", () => NextCamera?.Invoke());
            Small(row2, "ADD MARK", () => AddMark?.Invoke());
            Small(row2, "ALL MARKS", () => _pendingList = true);
        }

        private bool _pendingList;

        private static UnityEngine.UI.Button Small(RectTransform row, string text, Action onClick, ButtonStyle style = ButtonStyle.Secondary)
        {
            var b = UiKit.Button(row, text, onClick, style, 70f, 26f);
            UiKit.Size(b, 70f).flexibleWidth = 1f;
            return b;
        }

        private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
            if (!visible && _list != null) CloseList();
        }

        /// <summary>Shows where the tape is (<paramref name="tick"/>) and the theater's settings.</summary>
        public void Sync(TapeTheater t, int tick, string cameraNote)
        {
            if (t == null) return;
            float f = t.FractionOf(tick);
            if (!_dragging && Mathf.Abs(f - _lastFraction) > 0.0005f)
            {
                _slider.SetValueWithoutNotify(f);
                _lastFraction = f;
            }
            int at = _dragging ? t.TickAt(_slider.value) : tick;
            _clock.text = TapeTheater.Clock(at) + " / " + TapeTheater.Clock(t.Steps);
            var next = t.NextMark(tick);
            _status.text = (t.Paused ? "PAUSED" : t.SpeedLabel) + "  ·  " + cameraNote + (next != null ? "\n<size=20><color=#8D99AE>NEXT: " + next.Label + "</color></size>" : "");
            _play.text = t.Paused ? "PLAY" : "PAUSE";
            if (t.Marks.Count != _marksShown) DrawTicks(t);
            if (_pendingList)
            {
                _pendingList = false;
                OpenList(t);
            }
        }

        private void DrawTicks(TapeTheater t)
        {
            _marksShown = t.Marks.Count;
            for (int i = _ticks.childCount - 1; i >= 0; i--) Destroy(_ticks.GetChild(i).gameObject);
            foreach (var m in t.Marks)
            {
                var tick = UiKit.Panel(_ticks, m.Auto ? Theme.Gold : Theme.Pink, name: "Mark");
                tick.raycastTarget = false;
                float x = t.FractionOf(m.Tick);
                var r = tick.rectTransform;
                r.anchorMin = new Vector2(x, m.Auto ? 0f : -0.35f);
                r.anchorMax = new Vector2(x, m.Auto ? 1f : 1.35f);
                r.sizeDelta = new Vector2(m.Auto ? 4f : 6f, 0f);
                r.anchoredPosition = Vector2.zero;
            }
        }

        private void OpenList(TapeTheater t)
        {
            CloseList();
            var scrim = UiKit.Panel(transform, Theme.Scrim, name: "MarksList");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            _list = scrim.gameObject;
            var safe = UiKit.SafeArea(scrim.transform);
            var holder = UiKit.NewRect("Body", safe);
            UiKit.Band(holder, 0.08f, 0.9f, 60f);
            var col = UiKit.ScrollColumn(holder, 10f, new RectOffset(20, 20, 20, 40));
            UiKit.Size(UiKit.Label(col, "MARKS", 56f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            if (t.Marks.Count == 0)
                UiKit.Size(UiKit.Label(col, "No marks yet. Baskets, blocks and steals are marked by themselves; ADD MARK marks the moment you're watching.", 30f, Theme.Cream, TextAlignmentOptions.Center, true), 140f);
            foreach (var m in t.Marks)
            {
                var mark = m;
                string text = TapeTheater.Clock(m.Tick) + "  " + (m.Auto ? "" : "* ") + m.Label;
                UiKit.Button(col, text, () =>
                {
                    CloseList();
                    MarkPicked?.Invoke(mark);
                }, m.Auto ? ButtonStyle.Secondary : ButtonStyle.Primary, 80f, 26f);
            }
            UiKit.Button(col, "CLOSE", CloseList, ButtonStyle.Ghost, 90f, 32f);
        }

        private void CloseList()
        {
            if (_list != null) Destroy(_list);
            _list = null;
        }

        public bool ListOpen => _list != null;

        /// <summary>Reports pressing and letting go of the scrub bar (the Slider itself handles the drag).</summary>
        private sealed class ScrubGrab : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public Action Down, Up;
            public void OnPointerDown(PointerEventData e) => Down?.Invoke();
            public void OnPointerUp(PointerEventData e) => Up?.Invoke();
        }
    }
}
