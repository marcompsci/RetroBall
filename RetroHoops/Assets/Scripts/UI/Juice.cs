using CallerRetroBall.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CallerRetroBall.UI
{
    /// <summary>Buttons squash on press and spring back on release (off with Reduce Motion).</summary>
    public sealed class ButtonPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float _target = 1f;
        private float _scale = 1f;
        private float _velocity;

        private static bool Calm => App.Career != null && App.Career.settings.reduceMotion;

        public void OnPointerDown(PointerEventData e) => _target = Calm ? 1f : 0.92f;
        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        private void Release()
        {
            _target = 1f;
            if (!Calm) _velocity = 6f; // overshoot a little: a springy "pop"
        }

        private void Update()
        {
            if (Mathf.Approximately(_scale, _target) && Mathf.Abs(_velocity) < 0.01f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // Critically-damped-ish spring.
            _velocity += (_target - _scale) * 260f * dt;
            _velocity *= Mathf.Exp(-18f * dt);
            _scale += _velocity * dt;
            if (Mathf.Abs(_scale - _target) < 0.001f && Mathf.Abs(_velocity) < 0.01f) { _scale = _target; _velocity = 0f; }
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }

    /// <summary>Panels grow in from 90% with a short ease-out when they open (off with Reduce Motion).</summary>
    public sealed class OverlayPop : MonoBehaviour
    {
        private const float Seconds = 0.14f;
        private float _start;

        private void OnEnable()
        {
            _start = Time.unscaledTime;
            if (App.Career != null && App.Career.settings.reduceMotion) { enabled = false; return; }
            transform.localScale = new Vector3(0.9f, 0.9f, 1f);
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _start) / Seconds);
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            float s = Mathf.Lerp(0.9f, 1f, e);
            transform.localScale = new Vector3(s, s, 1f);
            if (t >= 1f) enabled = false;
        }
    }
}
