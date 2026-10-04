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
        /// <summary>
        /// Where the controls sit. Full = the whole screen (one player). Bottom / Top = tabletop
        /// 2 Player on one phone lying flat: player 1 holds the bottom edge, player 2 the top edge
        /// (their controls are turned 180° to face them).
        /// </summary>
        public enum Seat { Full = 0, Bottom = 1, Top = 2 }

        public VirtualJoystick Joystick { get; private set; }
        public TouchActionButton Shoot { get; private set; }
        public TouchActionButton Pass { get; private set; }
        public TouchActionButton Defense { get; private set; }
        public TouchActionButton Call { get; private set; }

        private InputBuffer _buffer;
        private Canvas _canvas;

        /// <param name="leftHanded">Mirror the layout: stick on the right, buttons on the left.</param>
        /// <param name="largeButtons">Action buttons 25% bigger (accessibility).</param>
        public static TouchControls Create(InputBuffer buffer, bool leftHanded = false, bool largeButtons = false, Seat seat = Seat.Full)
        {
            var canvas = UiKit.CreateScreenCanvas(seat == Seat.Top ? "TouchControlsCanvasP2" : "TouchControlsCanvas", 10);
            var controls = canvas.gameObject.AddComponent<TouchControls>();
            controls._canvas = canvas;
            controls._buffer = buffer;
            controls._leftHanded = leftHanded;
            controls._seat = seat;
            // Half a screen each in tabletop play: slightly smaller buttons so both sets fit.
            controls._scale = (largeButtons ? 1.25f : 1f) * (seat == Seat.Full ? 1f : 0.82f);
            var safe = UiKit.SafeArea(canvas.transform);
            if (seat == Seat.Full)
            {
                controls.Build(safe);
            }
            else
            {
                var half = UiKit.NewRect(seat == Seat.Top ? "SeatP2" : "SeatP1", safe);
                half.anchorMin = new Vector2(0f, seat == Seat.Top ? 0.5f : 0f);
                half.anchorMax = new Vector2(1f, seat == Seat.Top ? 1f : 0.5f);
                half.offsetMin = half.offsetMax = Vector2.zero;
                // Turned to face player 2 across the table.
                if (seat == Seat.Top) half.localEulerAngles = new Vector3(0f, 0f, 180f);
                controls.Build(half);
                var tag = UiKit.Label(half, seat == Seat.Top ? "P2" : "P1", 40f, seat == Seat.Top ? Theme.Cyan : Theme.Gold, TextAlignmentOptions.Center, true);
                UiKit.Place(tag.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(160f, 60f));
                tag.raycastTarget = false;
            }
            return controls;
        }

        private bool _leftHanded;
        private float _scale = 1f;
        private Seat _seat;

        private void Build(RectTransform safe)
        {
            // Joystick zone: lower-left, generous so the thumb never has to aim.
            var zone = UiKit.NewRect("JoystickZone", safe);
            zone.anchorMin = new Vector2(_leftHanded ? 0.45f : 0f, 0f);
            zone.anchorMax = new Vector2(_leftHanded ? 1f : 0.55f, _seat == Seat.Full ? 0.48f : 0.92f);
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
            float k = _scale;
            Shoot = MakeButton(safe, "SHOOT", 250f * k, Corner(-185f * k, 245f * k), Theme.Pink, 44f * k);
            Pass = MakeButton(safe, "PASS", 180f * k, Corner(-440f * k, 165f * k), Theme.Cyan, 36f * k);
            Defense = MakeButton(safe, "DEF", 160f * k, Corner(-175f * k, 520f * k), Theme.Gold, 32f * k);
            Call = MakeButton(safe, "CALL", 130f * k, Corner(-420f * k, 420f * k), Theme.Muted, 28f * k);
            if (_leftHanded)
                foreach (var b in new[] { Shoot, Pass, Defense, Call })
                {
                    var rt = (RectTransform)b.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                }

            Shoot.Pressed += () => Press(ActionButton.Shoot);
            Pass.Pressed += () => Press(ActionButton.Pass);
            Defense.Pressed += () => Press(ActionButton.Defense);
            Call.Pressed += () => Press(ActionButton.Call);
        }

        private void Press(ActionButton b) => _buffer.Press(b, Time.unscaledTime);

        /// <summary>Offset from the lower-right corner, mirrored to the lower-left for left-handed play.</summary>
        private Vector2 Corner(float x, float y) => _leftHanded ? new Vector2(-x, y) : new Vector2(x, y);

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
            button.SetSprites(Theme.DiscSprite(color, Theme.Shadow), Theme.DiscPressedSprite(color));
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
                // Player 2's controls are upside down: their "up" points down the screen.
                return _seat == Seat.Top ? new Vec2(-v.x, v.y) : new Vec2(v.x, -v.y);
            }
        }
    }
}
