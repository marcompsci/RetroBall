#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Watches Low Power Mode and the phone's temperature (iOS) every few seconds and applies
    /// <see cref="PowerPolicy"/>: 60 fps and fewer cosmetic effects while saving power.
    /// Also drives the optional SHOW FPS readout.
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
        private GUIStyle _style;

        /// <summary>True while the game is saving power (Low Power Mode or a hot phone).</summary>
        public static bool SavingPower => _instance != null && _instance._saving;
        public static FrameStats Stats => _instance != null ? _instance._stats : null;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[PowerMonitor]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PowerMonitor>();
        }

        private void Update()
        {
            _stats.Add(Time.unscaledDeltaTime);
            if (Time.unscaledTime < _nextCheck) return;
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
                      _stats.Fps + " fps  avg " + _stats.AverageMs.ToString("0.0") + " ms  worst " + _stats.WorstMs.ToString("0.0") + " ms" + (_saving ? "  (saving power)" : ""), _style);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
