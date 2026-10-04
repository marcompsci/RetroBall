using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Controller support for menus. When a gamepad is used, the top-most screen's first button is
    /// selected (d-pad / stick moves between buttons, A / Cross presses), and a blinking gold pixel
    /// cursor sits beside the selected button. Touch players never see it.
    /// </summary>
    public sealed class ControllerCursor : MonoBehaviour
    {
        private static ControllerCursor _instance;
        private RectTransform _cursor;
        private Image _image;
        private Texture2D _tex;
        private bool _padActive;
        private float _nextCheck;

        /// <summary>
        /// True during live play: the controller drives the player, so menu selection is cleared
        /// (pressing A must shoot, not click the pause button).
        /// </summary>
        public static bool Suppressed { get; set; }

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[ControllerCursor]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ControllerCursor>();
            _instance.Build();
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800; // above menus and dialogs, below the CRT overlay and scene wipe
            var go = new GameObject("Cursor", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _cursor = (RectTransform)go.transform;
            _cursor.pivot = new Vector2(1f, 0.5f);
            _cursor.sizeDelta = new Vector2(36f, 42f);
            _image = go.AddComponent<Image>();
            _image.raycastTarget = false;
            // A 6×7 pixel arrow pointing right, drawn in code.
            string[] rows = { "##....", "####..", "######", "######", "####..", "##....", "......" };
            _tex = new Texture2D(6, 7, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 6; x++)
                    _tex.SetPixel(x, 6 - y, rows[y][x] == '#' ? new Color32(0xFF, 0xD1, 0x66, 255) : new Color32(0, 0, 0, 0));
            _tex.Apply(false);
            _image.sprite = Sprite.Create(_tex, new Rect(0, 0, 6, 7), new Vector2(0.5f, 0.5f), 100f);
            _image.enabled = false;
        }

        private void LateUpdate()
        {
            var es = EventSystem.current;
#if ENABLE_INPUT_SYSTEM
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && (pad.dpad.ReadValue().sqrMagnitude > 0.1f || pad.leftStick.ReadValue().sqrMagnitude > 0.2f
                                || pad.buttonSouth.wasPressedThisFrame))
                _padActive = true;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) _padActive = false;
#endif
            if (Suppressed && es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
            if (es == null || !_padActive || Suppressed)
            {
                _image.enabled = false;
                return;
            }

            var selected = es.currentSelectedGameObject;
            // Re-checking which canvas is on top is a scene search, so do it a few times a second.
            bool check = Time.unscaledTime >= _nextCheck;
            if (check) _nextCheck = Time.unscaledTime + 0.2f;
            if (selected == null || !selected.activeInHierarchy || (check && !IsOnTopCanvas(selected)))
            {
                selected = FirstButtonOnTopCanvas();
                es.SetSelectedGameObject(selected);
            }
            _image.enabled = selected != null;
            if (selected == null) return;

            var rt = selected.transform as RectTransform;
            if (rt == null) return;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            // Screen-space overlay canvases: world corners are screen pixels.
            var left = (corners[0] + corners[1]) * 0.5f;
            float blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.55f ? 0f : 6f;
            _cursor.position = new Vector3(left.x - 8f - blink, left.y, 0f);
            float scale = Mathf.Max(1f, Screen.height / 1920f * 1.6f);
            _cursor.localScale = new Vector3(scale, scale, 1f);
        }

        private static Canvas TopCanvas()
        {
            Canvas best = null;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!c.isRootCanvas || c.GetComponent<GraphicRaycaster>() == null) continue;
                if (c.GetComponentInChildren<Selectable>() == null) continue;
                if (best == null || c.sortingOrder > best.sortingOrder) best = c;
            }
            return best;
        }

        private static bool IsOnTopCanvas(GameObject go)
        {
            var top = TopCanvas();
            return top != null && go.transform.IsChildOf(top.transform);
        }

        private static GameObject FirstButtonOnTopCanvas()
        {
            var top = TopCanvas();
            if (top == null) return null;
            foreach (var s in top.GetComponentsInChildren<Selectable>())
                if (s.IsInteractable() && s.isActiveAndEnabled) return s.gameObject;
            return null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_tex != null) Destroy(_tex);
        }
    }
}
