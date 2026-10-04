using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Settings ▸ CAPTIONS: the announcer's calls ("HEATING UP!", "ALLEY-OOP!") as text near the
    /// bottom of the screen. Also on when iOS Settings ▸ Accessibility ▸ Subtitles &amp; Captioning ▸
    /// Closed Captions is on. One persistent overlay canvas that never blocks touches.
    /// </summary>
    public sealed class Captions : MonoBehaviour
    {
        private const float Seconds = 1.8f;
        private static Captions _instance;
        private TextMeshProUGUI _text;
        private CanvasGroup _group;
        private float _until;

        public static bool Enabled =>
            App.Career != null && (App.Career.settings.captions || SystemCaptionsOn());

        private static bool SystemCaptionsOn()
        {
            try { return UnityEngine.Accessibility.AccessibilitySettings.isClosedCaptioningEnabled; }
            catch (System.Exception) { return false; }
        }

        /// <summary>Shows <paramref name="line"/> as a caption when captions are on.</summary>
        public static void Show(string line)
        {
            if (string.IsNullOrEmpty(line) || !Enabled) return;
            if (_instance == null)
            {
                var canvas = UiKit.CreateScreenCanvas("CaptionsCanvas", 880);
                DontDestroyOnLoad(canvas.gameObject);
                _instance = canvas.gameObject.AddComponent<Captions>();
                _instance.Build(UiKit.SafeArea(canvas.transform));
            }
            _instance._text.text = "[ " + Loc.T(line) + " ]";
            _instance._until = Time.unscaledTime + Seconds;
            _instance._group.alpha = 1f;
        }

        private void Build(RectTransform safe)
        {
            var bar = UiKit.Panel(safe, new Color(0f, 0f, 0f, 0.72f), null, false, "CaptionBar");
            bar.raycastTarget = false;
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0.08f, 0.5f);
            rt.anchorMax = new Vector2(0.92f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 90f);
            rt.anchoredPosition = new Vector2(0f, -120f);
            _group = bar.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _text = UiKit.Label(rt, "", 40f, Color.white, TextAlignmentOptions.Center, true);
            UiKit.Stretch(_text.rectTransform);
            _text.raycastTarget = false;
            _group.alpha = 0f;
        }

        private void Update()
        {
            if (_group.alpha > 0f && Time.unscaledTime > _until)
                _group.alpha = Mathf.Max(0f, _group.alpha - Time.unscaledDeltaTime * 4f);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
