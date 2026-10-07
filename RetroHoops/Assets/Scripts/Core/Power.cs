#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Watches Low Power Mode and the phone's temperature (iOS) every few seconds and applies
    /// <see cref="PowerPolicy"/>: 60 fps and fewer cosmetic effects while saving power.
    /// Also drives the optional SHOW FPS readout, menu frame pacing (menus draw at 60 fps while
    /// touched and 30 fps when idle; live gameplay draws every frame) and low-memory clean-up.
    /// </summary>
    public sealed class PowerMonitor : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroPower_IsLowPower();
        [DllImport("__Internal")] private static extern int RetroPower_ThermalState();
#endif
        private static PowerMonitor _instance;
        private float _nextCheck;
        private bool _saving;
        private readonly FrameStats _stats = new FrameStats(60);
        private readonly FrameBudget _budget = new FrameBudget();
        private GUIStyle _style;

        /// <summary>True while the game is saving power (Low Power Mode or a hot phone).</summary>
        public static bool SavingPower => _instance != null && (_instance._saving || _instance._budget.Struggling);
        public static FrameStats Stats => _instance != null ? _instance._stats : null;

        /// <summary>Set by the game scene while a match (or replay, or the attract demo) is moving: draw every frame.</summary>
        public static bool Gameplay { get; set; }

        /// <summary>Call when something on a menu animates on its own and must stay smooth for a moment.</summary>
        public static void Wake()
        {
            if (_instance != null) _instance._lastInput = Time.unscaledTime;
        }

        private float _lastInput;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[PowerMonitor]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PowerMonitor>();
            Application.lowMemory += OnLowMemory;
        }

        /// <summary>iOS memory warning: forget cached generated art (anything still on screen stays) and free the rest.</summary>
        private static void OnLowMemory()
        {
            TextureFactory.ForgetCache();
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            Debug.Log("[Retro Hoops] Low memory: cleared cached art.");
        }

        private static bool AnyInput()
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.delta.ReadValue().sqrMagnitude > 0f || mouse.scroll.ReadValue().sqrMagnitude > 0f)) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.dpad.ReadValue().sqrMagnitude > 0f
                                || pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed)) return true;
            return false;
#else
            return Input.touchCount > 0 || Input.anyKey;
#endif
        }

        private void Update()
        {
            _stats.Add(Time.unscaledDeltaTime);
            float now = Time.unscaledTime;
            if (AnyInput() || SceneFlow.IsTransitioning) _lastInput = now;
            int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            if (_budget.Observe(Time.unscaledDeltaTime, target, Gameplay))
            {
                App.ApplyFrameRate();
                Debug.Log("[Retro Hoops] " + (_budget.Struggling ? "Can't hold 120 fps here: playing at a steady 60." : "Trying 120 fps again."));
            }
            int interval = PowerPolicy.RenderInterval(target, Gameplay, now - _lastInput, _saving);
            if (OnDemandRendering.renderFrameInterval != interval) OnDemandRendering.renderFrameInterval = interval;
            if (now < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 5f;
            bool low = false;
            int thermal = 0;
#if UNITY_IOS && !UNITY_EDITOR
            low = RetroPower_IsLowPower() != 0;
            thermal = RetroPower_ThermalState();
#endif
            bool saving = PowerPolicy.ShouldSave(low, thermal);
            if (saving == _saving) return;
            _saving = saving;
            App.ApplyFrameRate();
            Debug.Log("[Retro Hoops] Power saving " + (saving ? "on" : "off") + " (low power " + low + ", thermal " + thermal + ")");
        }

        private void OnGUI()
        {
            if (App.Career == null || !App.Career.settings.showFps) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 60) };
            _style.normal.textColor = _stats.Fps >= 55 ? Color.green : Color.yellow;
            GUI.Label(new Rect(12, Screen.height - Screen.safeArea.yMax + 12, 700, 60),
                      _stats.Fps + " fps  avg " + _stats.AverageMs.ToString("0.0") + " ms  worst " + _stats.WorstMs.ToString("0.0") + " ms" + (_saving ? "  (saving power)" : _budget.Struggling ? "  (steady 60)" : ""), _style);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                Application.lowMemory -= OnLowMemory;
                OnDemandRendering.renderFrameInterval = 1;
            }
        }
    }
}
