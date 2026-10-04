using CallerRetroBall.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Keeps a canvas's width/height match right when the window changes shape (a Mac window
    /// being resized, an iPad rotating its Stage Manager window).
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
            if (_h > 0) _scaler.matchWidthOrHeight = CameraMath.UiMatch(_w / (float)_h);
        }
    }
}
