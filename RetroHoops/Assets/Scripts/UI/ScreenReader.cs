using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// VoiceOver support for the menus (Unity's screen-reader API). While VoiceOver is on, the visible,
    /// reachable buttons and text on screen are published as an accessibility tree in reading order
    /// (top to bottom, left to right); double-tapping a button presses it. Buttons scrolled out of view in
    /// a list are included and scrolled into view when focused. During play the court is one
    /// "direct touch" area, so the stick and buttons work as usual. Does nothing when VoiceOver is off.
    /// </summary>
    public sealed class ScreenReader : MonoBehaviour
    {
        private const float RefreshSeconds = 0.4f;
        private static ScreenReader _instance;
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        private AccessibilityHierarchy _hierarchy;
        private string _signature = "";
        private string _scene = "";
        private float _next;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private readonly Vector3[] _corners = new Vector3[4];

        private struct Item
        {
            public string Label;
            public RectTransform Rect;
            public Button Button;
            public float Top, Left;
        }

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[ScreenReader]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScreenReader>();
        }

        private static bool On
        {
            get
            {
                try { return AssistiveSupport.isScreenReaderEnabled; }
                catch (System.Exception) { return false; }
            }
        }

        private void Update()
        {
            if (!On)
            {
                if (_hierarchy != null) Clear();
                return;
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        private void Clear()
        {
            _hierarchy = null;
            _signature = "";
            try { AssistiveSupport.activeHierarchy = null; }
            catch (System.Exception) { }
        }

        private void Refresh()
        {
            var items = Collect(out bool playing);
            var sb = new StringBuilder(playing ? "play|" : "menu|");
            foreach (var it in items) sb.Append(it.Label).Append('@').Append(it.Rect.GetHashCode()).Append('|');
            string sig = sb.ToString();
            string scene = SceneManager.GetActiveScene().name;
            if (sig == _signature && scene == _scene)
            {
                _hierarchy?.RefreshNodeFrames();
                return;
            }
            bool newScreen = scene != _scene;
            _signature = sig;
            _scene = scene;

            var h = new AccessibilityHierarchy();
            AccessibilityNode first = null;
            foreach (var it in items)
            {
                var node = h.AddNode(it.Label, null);
                var rt = it.Rect;
                node.frameGetter = () => ScreenRect(rt);
                if (it.Button != null)
                {
                    var button = it.Button;
                    node.role = AccessibilityRole.Button;
                    if (!button.interactable) node.state = AccessibilityState.Disabled;
                    node.invoked += () =>
                    {
                        if (button == null || !button.isActiveAndEnabled || !button.interactable) return false;
                        button.onClick.Invoke();
                        return true;
                    };
                    node.focusChanged += (n, focused) => { if (focused && rt != null) ScrollIntoView(rt); };
                }
                else node.role = AccessibilityRole.StaticText;
                if (first == null) first = node;
            }
            if (playing)
            {
                // The court: touches go straight to the game so the stick and buttons work.
                // Phase 37: in Spanish too, and the right way round for left-handed controls.
                bool lefty = Core.App.Career != null && Core.App.Career.settings.leftHanded;
                var court = h.AddNode(Logic.Loc.T(lefty ? "Court. Stick on the right, shoot, pass and defense on the left."
                                                        : "Court. Stick on the left, shoot, pass and defense on the right."), null);
                court.frameGetter = () => new Rect(0f, 0f, Screen.width, Screen.height);
                court.allowsDirectInteraction = true;
                if (first == null) first = court;
            }
            _hierarchy = h;
            try
            {
                AssistiveSupport.activeHierarchy = h;
                var dispatcher = AssistiveSupport.notificationDispatcher;
                if (newScreen) dispatcher.SendScreenChanged(first);
                else dispatcher.SendLayoutChanged(null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Retro Hoops] Screen reader update failed: " + e.Message);
            }
        }

        private List<Item> Collect(out bool playing)
        {
            var items = new List<Item>();
            playing = false;
            // In a match with the touch controls showing, the court takes direct touches.
            var canvases = FindObjectsByType<Canvas>();
            foreach (var c in canvases)
                if (c.isActiveAndEnabled && c.enabled && c.gameObject.name.StartsWith("TouchControlsCanvas", System.StringComparison.Ordinal))
                    playing = true;

            var events = EventSystem.current;
            foreach (var b in FindObjectsByType<Button>())
            {
                if (!b.isActiveAndEnabled) continue;
                var rt = (RectTransform)b.transform;
                if (!Reachable(events, rt, out bool offscreen) && !(offscreen && InScrollList(rt))) continue;
                string label = LabelOf(b.gameObject);
                if (string.IsNullOrEmpty(label)) continue;
                items.Add(Make(label, rt, b));
            }
            // Text that isn't part of a button (titles, descriptions, stats).
            foreach (var t in FindObjectsByType<TextMeshProUGUI>())
            {
                if (!t.isActiveAndEnabled || t.GetComponentInParent<Button>() != null) continue;
                string label = Clean(t.text);
                if (label.Length < 2) continue;
                var rt = t.rectTransform;
                if (t.canvas != null && t.canvas.name.StartsWith("TouchControlsCanvas", System.StringComparison.Ordinal)) continue;
                if (!Reachable(events, rt, out bool offscreen) && !(offscreen && InScrollList(rt))) continue;
                items.Add(Make(label, rt, null));
            }
            items.Sort((a, b) => Mathf.Abs(a.Top - b.Top) > 8f ? b.Top.CompareTo(a.Top) : a.Left.CompareTo(b.Left));
            return items;
        }

        private Item Make(string label, RectTransform rt, Button b)
        {
            rt.GetWorldCorners(_corners);
            return new Item { Label = label, Rect = rt, Button = b, Top = _corners[1].y, Left = _corners[0].x };
        }

        /// <summary>
        /// True when the element's centre is on screen and nothing unrelated sits on top of it (so menus
        /// behind an open dialog aren't read out). <paramref name="offscreen"/> is set when it's off screen.
        /// </summary>
        private bool Reachable(EventSystem events, RectTransform rt, out bool offscreen)
        {
            rt.GetWorldCorners(_corners);
            var centre = (_corners[0] + _corners[2]) * 0.5f;
            offscreen = centre.x < 0f || centre.y < 0f || centre.x > Screen.width || centre.y > Screen.height;
            if (offscreen) return false;
            if (events == null) return true;
            _hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = centre }, _hits);
            if (_hits.Count == 0) return true;
            var top = _hits[0].gameObject.transform;
            // The topmost hit must be the element itself, inside it, or something it sits on.
            return top == rt || top.IsChildOf(rt) || rt.IsChildOf(top);
        }

        private static bool InScrollList(RectTransform rt)
        {
            var scroll = rt.GetComponentInParent<ScrollRect>();
            return scroll != null && scroll.isActiveAndEnabled && scroll.content != null && rt.IsChildOf(scroll.content);
        }

        /// <summary>Scrolls a list so the focused element is in view.</summary>
        private static void ScrollIntoView(RectTransform rt)
        {
            var scroll = rt.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null || !rt.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            var content = scroll.content;
            var viewport = scroll.viewport;
            Vector2 inContent = content.InverseTransformPoint(rt.position);
            float contentHeight = content.rect.height;
            float viewHeight = viewport.rect.height;
            if (contentHeight <= viewHeight) return;
            // Content is anchored at the top: distance of the element from the content's top edge.
            float fromTop = content.rect.yMax - inContent.y;
            float target = Mathf.Clamp(fromTop - viewHeight * 0.5f, 0f, contentHeight - viewHeight);
            scroll.verticalNormalizedPosition = 1f - target / (contentHeight - viewHeight);
        }

        private static string LabelOf(GameObject go)
        {
            var sb = new StringBuilder();
            foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>())
            {
                if (!t.isActiveAndEnabled) continue;
                string s = Clean(t.text);
                if (s.Length == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(s);
            }
            return sb.ToString();
        }

        private static string Clean(string text) =>
            string.IsNullOrEmpty(text) ? "" : Tags.Replace(text, "").Replace('\n', ' ').Trim();

        /// <summary>Screen rectangle with the origin at the top left, as the screen reader expects.</summary>
        private Rect ScreenRect(RectTransform rt)
        {
            if (rt == null) return Rect.zero;
            rt.GetWorldCorners(_corners);
            float minX = _corners[0].x, minY = _corners[0].y, maxX = _corners[2].x, maxY = _corners[2].y;
            return new Rect(minX, Screen.height - maxY, maxX - minX, maxY - minY);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
