using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.Controls
{
    /// <summary>
    /// Round action button that reports press and release separately (hold-to-shoot needs
    /// both). When an action isn't available it still responds with a dimmed flash and a
    /// short label, instead of a popup.
    /// </summary>
    public sealed class TouchActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Pressed;
        public event Action Released;

        private Image _image;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _hint;
        private int _pointerId = int.MinValue;
        private float _hintUntil;
        private Color _baseColor = Color.white;

        public bool IsHeld => _pointerId != int.MinValue;
        public bool Available { get; set; } = true;
        /// <summary>Short label flashed when pressed while unavailable.</summary>
        public string UnavailableHint { get; set; } = "SOON";

        public void Init(Image image, TextMeshProUGUI label, TextMeshProUGUI hint)
        {
            _image = image;
            _label = label;
            _hint = hint;
            _baseColor = image.color;
            if (_hint != null) _hint.text = "";
        }

        public void SetLabel(string text)
        {
            if (_label != null && _label.text != text) _label.text = text;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld) return;
            _pointerId = e.pointerId;
            transform.localScale = Vector3.one * 0.92f;
            if (!Available)
            {
                _hintUntil = Time.unscaledTime + 0.8f;
                if (_hint != null) _hint.text = UnavailableHint;
            }
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            Release();
        }

        public void Release()
        {
            bool wasHeld = IsHeld;
            _pointerId = int.MinValue;
            transform.localScale = Vector3.one;
            if (wasHeld) Released?.Invoke();
        }

        private void OnDisable() => Release();

        private void Update()
        {
            var c = _baseColor;
            if (!Available) c = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.75f);
            _image.color = c;
            if (_hint != null && _hint.text.Length > 0 && Time.unscaledTime > _hintUntil) _hint.text = "";
        }
    }
}
