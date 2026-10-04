using CallerRetroBall.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Keeps a canvas's width/height match right when the window changes shape (a Mac window
    /// being resized, the screen turning to landscape for Full Court).
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AspectMatch : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private int _w, _h;

        private void Awake() => _scaler = GetComponent<CanvasScaler>();

        private void Update()
        {
            if (Screen.width == _w && Screen.height == _h) return;
            _w = Screen.width;
            _h = Screen.height;
            Apply(_scaler);
        }

        /// <summary>Reference size and match for the current screen shape (and the accessibility UI scale).</summary>
        public static void Apply(CanvasScaler scaler)
        {
            LandscapeMath.Canvas(Screen.width, Screen.height, out float w, out float h, out float match);
            scaler.referenceResolution = new Vector2(w, h) / Mathf.Clamp(UiKit.UiScale, 0.75f, 1.5f);
            scaler.matchWidthOrHeight = match;
        }
    }
}
