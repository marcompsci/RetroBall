using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Compact vertical shot meter beside the human shooter: dark frame, green target band
    /// (wider for better shooters), and a fill that turns green inside the window. After the
    /// release it holds for a moment, coloured by the timing grade.
    /// </summary>
    public sealed class ShotMeterView : MonoBehaviour
    {
        private static readonly Color FillColor = new Color32(0xF4, 0xF1, 0xDE, 255);
        // Settings ▸ COLOR FILTER picks these (green / amber / pink with no filter).
        private static Color GreenColor = new Color32(0x3D, 0xDC, 0x84, 255);
        private static Color EarlyLateColor = new Color32(0xFF, 0xB0, 0x3B, 255);
        private static Color BadColor = new Color32(0xF7, 0x25, 0x85, 255);

        /// <summary>Applies the colour filter's meter palette (call before creating the meter).</summary>
        public static void UsePalette(MeterPalette p)
        {
            GreenColor = new Color32(p.Good.r, p.Good.g, p.Good.b, 255);
            EarlyLateColor = new Color32(p.Near.r, p.Near.g, p.Near.b, 255);
            BadColor = new Color32(p.Bad.r, p.Bad.g, p.Bad.b, 255);
        }

        private const float Ppu = CourtSpace.PixelsPerUnit;
        private const int InnerX = 2;
        private const int InnerY = 2;
        private const int InnerW = PropSpriteGenerator.MeterWidth - 4;
        private const int InnerH = PropSpriteGenerator.MeterInnerHeight;

        private SpriteRenderer _frame, _band, _fill;
        private float _holdUntil;
        private float _heldMeter;
        private Color _heldColor;

        public static ShotMeterView Create(Transform parent, MatchArt art)
        {
            var go = new GameObject("ShotMeter");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ShotMeterView>();
            v._frame = Child(go.transform, "Frame", art.MeterFrame, Vector3.zero);
            v._band = Child(go.transform, "GreenBand", art.Pixel, Vector3.zero);
            v._fill = Child(go.transform, "Fill", art.Pixel, Vector3.zero);
            v._band.color = new Color(GreenColor.r, GreenColor.g, GreenColor.b, 0.55f);
            v.SetVisible(false);
            return v;
        }

        private static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 32000;
            return sr;
        }

        /// <summary>Shows the meter while the human is charging; <paramref name="meter"/> 0..1+.</summary>
        public void ShowCharging(Vector3 shooterWorld, float meter, int shootingRating, ShotTuning t)
        {
            SetVisible(true);
            PlaceBeside(shooterWorld);
            float gh = ShotModel.GreenHalfWidth(shootingRating, t);
            SetBand(t.greenCenter - gh, t.greenCenter + gh);
            var grade = ShotModel.Grade(Mathf.Min(meter, 1f), shootingRating, t);
            SetFill(meter, grade == TimingGrade.Green ? GreenColor : FillColor);
        }

        /// <summary>Freezes the meter at the release point, coloured by grade.</summary>
        public void ShowRelease(float meter, TimingGrade grade)
        {
            _heldMeter = meter;
            _heldColor = grade == TimingGrade.Green ? GreenColor
                : (grade == TimingGrade.SlightlyEarly || grade == TimingGrade.SlightlyLate ? EarlyLateColor : BadColor);
            _holdUntil = Time.unscaledTime + 0.6f;
            SetFill(_heldMeter, _heldColor);
        }

        /// <summary>Call every frame when not charging.</summary>
        public void Idle()
        {
            if (Time.unscaledTime < _holdUntil) return;
            SetVisible(false);
        }

        private void PlaceBeside(Vector3 shooterWorld)
        {
            // Right of the shooter's head; snapped to the pixel grid.
            var p = shooterWorld + new Vector3(0.7f, 0.2f, 0f);
            transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
        }

        private void SetBand(float from, float to)
        {
            float y0 = Mathf.Round(Mathf.Clamp01(from) * InnerH);
            float y1 = Mathf.Max(y0 + 1f, Mathf.Round(Mathf.Clamp01(to) * InnerH));
            _band.transform.localPosition = new Vector3(InnerX / Ppu, (InnerY + y0) / Ppu, 0f);
            _band.transform.localScale = new Vector3(InnerW, y1 - y0, 1f);
            _band.sortingOrder = 32001;
        }

        private void SetFill(float meter, Color color)
        {
            float h = Mathf.Round(Mathf.Clamp01(meter) * InnerH);
            _fill.transform.localPosition = new Vector3(InnerX / Ppu, InnerY / Ppu, 0f);
            _fill.transform.localScale = new Vector3(InnerW, Mathf.Max(0f, h), 1f);
            _fill.color = color;
            _fill.sortingOrder = 32002;
        }

        private void SetVisible(bool v)
        {
            if (_frame.enabled == v) return;
            _frame.enabled = v;
            _band.enabled = v;
            _fill.enabled = v;
        }
    }
}
