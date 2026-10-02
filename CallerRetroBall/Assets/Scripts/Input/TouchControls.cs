using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.Controls
{
    /// <summary>
    /// On-screen controls for portrait play. Left thumb: floating joystick in the lower-left
    /// zone. Right thumb: SHOOT (hold/release), PASS, DEF, and CALL, laid out in an arc so each
    /// is reachable without looking. Presses go into an <see cref="InputBuffer"/> so slightly
    /// early taps still count.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public VirtualJoystick Joystick { get; private set; }
        public TouchActionButton Shoot { get; private set; }
        public TouchActionButton Pass { get; private set; }
        public TouchActionButton Defense { get; private set; }
        public TouchActionButton Call { get; private set; }

        private InputBuffer _buffer;
        private Canvas _canvas;

        public static TouchControls Create(InputBuffer buffer)
        {
            var canvas = UiKit.CreateScreenCanvas("TouchControlsCanvas", 10);
            var controls = canvas.gameObject.AddComponent<TouchControls>();
            controls._canvas = canvas;
            controls._buffer = buffer;
            controls.Build(UiKit.SafeArea(canvas.transform));
            return controls;
        }

        private void Build(RectTransform safe)
        {
            // Joystick zone: lower-left, generous so the thumb never has to aim.
            var zone = UiKit.NewRect("JoystickZone", safe);
            zone.anchorMin = new Vector2(0f, 0f);
            zone.anchorMax = new Vector2(0.55f, 0.48f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            var zoneImage = zone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f); // invisible but receives touches
            zoneImage.raycastTarget = true;

            var baseRing = UiKit.Panel(zone, new Color(1f, 1f, 1f, 0.35f), Theme.DiscSprite(new Color(0f, 0f, 0f, 0.25f), Theme.Cream), false, "StickBase");
            baseRing.rectTransform.anchorMin = baseRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            baseRing.rectTransform.sizeDelta = new Vector2(300f, 300f);
            var knob = UiKit.Panel(zone, Color.white, Theme.DiscSprite(Theme.Cream, Theme.Shadow), false, "StickKnob");
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(130f, 130f);

            Joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            Joystick.Init(zone, baseRing.rectTransform, knob.rectTransform);

            var hint = UiKit.Label(zone, "MOVE", 30f, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Center, true);
            UiKit.Place(hint.rectTransform, new Vector2(0.45f, 0.4f), new Vector2(300f, 60f));

            // Action buttons, anchored to the lower-right corner of the safe area.
            Shoot = MakeButton(safe, "SHOOT", 250f, new Vector2(-185f, 245f), Theme.Pink, 44f);
            Pass = MakeButton(safe, "PASS", 180f, new Vector2(-440f, 165f), Theme.Cyan, 36f);
            Defense = MakeButton(safe, "DEF", 160f, new Vector2(-175f, 520f), Theme.Gold, 32f);
            Call = MakeButton(safe, "CALL", 130f, new Vector2(-420f, 420f), Theme.Muted, 28f);

            Shoot.Pressed += () => Press(ActionButton.Shoot);
            Pass.Pressed += () => Press(ActionButton.Pass);
            Defense.Pressed += () => Press(ActionButton.Defense);
            Call.Pressed += () => Press(ActionButton.Call);
        }

        private void Press(ActionButton b) => _buffer.Press(b, Time.unscaledTime);

        private static TouchActionButton MakeButton(RectTransform parent, string text, float size, Vector2 fromCorner, Color color, float fontSize)
        {
            var image = UiKit.Panel(parent, Color.white, Theme.DiscSprite(color, Theme.Shadow), false, "Btn " + text);
            image.raycastTarget = true;
            image.preserveAspect = true;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = fromCorner;

            var label = UiKit.Label(rt, text, fontSize, Theme.Shadow, TextAlignmentOptions.Center, true);
            UiKit.Stretch(label.rectTransform);
            var hint = UiKit.Label(rt, "", 24f, Theme.Cream, TextAlignmentOptions.Center, true, "Hint");
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(size, 40f));
            hint.rectTransform.anchoredPosition = new Vector2(0f, 24f);

            var button = image.gameObject.AddComponent<TouchActionButton>();
            button.Init(image, label, hint);
            return button;
        }

        /// <summary>Contextual labels, e.g. SHOOT/PASS with the ball, SHOOT/ASK when a teammate has it.</summary>
        public void SetLabels(string shoot, string pass, string defense)
        {
            Shoot.SetLabel(shoot);
            Pass.SetLabel(pass);
            Defense.SetLabel(defense);
        }

        /// <summary>Marks which actions are usable right now (dimmed + "SOON"/no-op otherwise).</summary>
        public void SetAvailability(bool shoot, bool pass, bool defense, bool call)
        {
            Shoot.Available = shoot;
            Pass.Available = pass;
            Defense.Available = defense;
            Call.Available = call;
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                Joystick.Release();
                Shoot.Release();
                Pass.Release();
                Defense.Release();
                Call.Release();
            }
            _canvas.enabled = visible;
        }

        /// <summary>Stick direction in court space (+y away from the hoop = screen-down).</summary>
        public Vec2 CourtMove
        {
            get
            {
                var v = Joystick.Value;
                return new Vec2(v.x, -v.y);
            }
        }
    }
}
