using System.Collections.Generic;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UI;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Phase 31: VoiceOver support for the menus, using Unity's screen-reader API (AssistiveSupport). While
    /// VoiceOver is on, every visible button on a screen-space canvas becomes an accessibility element (its text
    /// is read out; a double-tap presses it), and screen titles and labels are readable too. The hierarchy is
    /// rebuilt whenever the buttons on screen change. Gameplay itself isn't playable by screen reader.
    /// </summary>
    public sealed class ScreenReader : MonoBehaviour
    {
        private static ScreenReader _instance;
        private readonly List<Selectable> _buttons = new List<Selectable>();
        private readonly List<string> _signature = new List<string>();
        private readonly List<string> _last = new List<string>();
        private float _nextScan;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[ScreenReader]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScreenReader>();
        }

        private static bool On => AssistiveSupport.isScreenReaderEnabled;

        private void Update()
        {
            if (!On)
            {
                if (_last.Count > 0)
                {
                    _last.Clear();
                    AssistiveSupport.activeHierarchy = null;
                }
                return;
            }
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.4f;
            Scan();
        }

        private void Scan()
        {
            _buttons.Clear();
            _signature.Clear();
            var labels = new List<(string text, RectTransform rect)>();
            foreach (var canvas in FindObjectsByType<Canvas>())
            {
                if (!canvas.isActiveAndEnabled || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                foreach (var s in canvas.GetComponentsInChildren<Selectable>(false))
                {
                    if (!s.IsInteractable() || !IsOnScreen(s.transform as RectTransform)) continue;
                    _buttons.Add(s);
                    _signature.Add(Describe(s));
                }
                foreach (var t in canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
                {
                    if (t.GetComponentInParent<Selectable>() != null || string.IsNullOrWhiteSpace(t.text) || !IsOnScreen(t.rectTransform)) continue;
                    labels.Add((Plain(t.text), t.rectTransform));
                    _signature.Add("L:" + Plain(t.text));
                }
            }
            bool same = _signature.Count == _last.Count;
            for (int i = 0; same && i < _signature.Count; i++) same = _signature[i] == _last[i];
            if (same) return;
            _last.Clear();
            _last.AddRange(_signature);
            Build(labels);
        }

        private void Build(List<(string text, RectTransform rect)> labels)
        {
            var h = new AccessibilityHierarchy();
            // Labels first (titles sit above buttons), then the buttons, in screen order top to bottom.
            foreach (var (text, rect) in labels)
            {
                var node = h.AddNode(text);
                node.role = AccessibilityRole.StaticText;
                var r = rect;
                node.frameGetter = () => ScreenRect(r);
            }
            foreach (var s in _buttons)
            {
                var node = h.AddNode(Describe(s));
                node.role = s is Toggle ? AccessibilityRole.Toggle : s is Slider ? AccessibilityRole.Slider : AccessibilityRole.Button;
                var target = s;
                node.frameGetter = () => ScreenRect(target.transform as RectTransform);
                node.invoked += () => Press(target);
            }
            AssistiveSupport.activeHierarchy = h;
            AssistiveSupport.notificationDispatcher.SendLayoutChanged();
        }

        private static bool Press(Selectable s)
        {
            if (s == null || !s.IsInteractable()) return false;
            if (s is Button b) { b.onClick.Invoke(); return true; }
            if (s is Toggle t) { t.isOn = !t.isOn; return true; }
            return false;
        }

        /// <summary>What VoiceOver reads for a control: its text (localized as shown), or its object name.</summary>
        private static string Describe(Selectable s)
        {
            var text = s.GetComponentInChildren<TextMeshProUGUI>(false);
            string label = text != null && !string.IsNullOrWhiteSpace(text.text) ? Plain(text.text) : ScreenReaderText.FromName(s.gameObject.name);
            if (s is Toggle t) label += t.isOn ? ", on" : ", off";
            return label;
        }

        private static string Plain(string richText) => ScreenReaderText.Plain(richText);

        private static bool IsOnScreen(RectTransform rt)
        {
            if (rt == null) return false;
            var r = ScreenRect(rt);
            return r.width > 1f && r.height > 1f && r.xMax > 0f && r.yMax > 0f && r.xMin < Screen.width && r.yMin < Screen.height;
        }

        /// <summary>The element's rectangle in screen pixels (Unity's screen space: origin bottom-left).</summary>
        private static Rect ScreenRect(RectTransform rt)
        {
            if (rt == null) return Rect.zero;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
