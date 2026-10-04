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
        private Sprite _upSprite, _downSprite;

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

        /// <summary>Raised and pushed-in faces; the button sinks while held.</summary>
        public void SetSprites(Sprite up, Sprite down)
        {
            _upSprite = up;
            _downSprite = down;
            if (_image != null) _image.sprite = IsHeld && down != null ? down : up;
        }

        private Image _icon;

        /// <summary>The pixel icon drawn above the label.</summary>
        public void SetIcon(Image icon) => _icon = icon;

        public void SetIconSprite(Sprite sprite)
        {
            if (_icon != null && _icon.sprite != sprite) _icon.sprite = sprite;
        }

        /// <summary>Kept for layouts that resize buttons; labels auto-size to the ring.</summary>
        public void SetFontScale(float scale) { }

        public void SetLabel(string text)
        {
            text = Logic.Loc.T(text);
            if (_label != null && _label.text != text) _label.text = text;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld) return;
            _pointerId = e.pointerId;
            transform.localScale = Vector3.one * 0.95f;
            if (_downSprite != null) _image.sprite = _downSprite;
            if (!Available)
            {
                _hintUntil = Time.unscaledTime + 0.8f;
                if (_hint != null) _hint.text = Logic.Loc.T(UnavailableHint);
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
            if (_upSprite != null && _image != null) _image.sprite = _upSprite;
            if (wasHeld) Released?.Invoke();
        }

        private void OnDisable() => Release();

        private void Update()
        {
            var c = _baseColor;
            if (!Available) c = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.75f);
            _image.color = c;
            if (_icon != null) _icon.color = Available ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (_hint != null && _hint.text.Length > 0 && Time.unscaledTime > _hintUntil) _hint.text = "";
        }
    }
}
