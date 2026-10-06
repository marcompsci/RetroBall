using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Battery and heat policy. On Low Power Mode or when the phone gets hot, the game drops to
    /// 60 fps and turns off purely cosmetic extras (embers, crowd bobbing) so it stays smooth and cool.
    /// </summary>
    public static class PowerPolicy
    {
        public const int ThermalSerious = 2;

        /// <summary>True when the game should save power.</summary>
        public static bool ShouldSave(bool lowPowerMode, int thermalState) => lowPowerMode || thermalState >= ThermalSerious;

        /// <summary>Frame rate to ask for: 120 only when allowed, supported, and not saving power.</summary>
        public static int TargetFrameRate(bool highFrameRateSetting, int screenHz, bool savePower) =>
            highFrameRateSetting && screenHz >= 119 && !savePower ? 120 : 60;

        /// <summary>Seconds without a touch, key or button before a menu drops to its idle rate.</summary>
        public const float MenuIdleSeconds = 3f;

        /// <summary>
        /// How many screen updates to skip between drawn frames (OnDemandRendering): live gameplay draws
        /// every frame; menus draw at most 60 fps while you touch them and 30 fps once they sit idle
        /// (15 fps while saving power). Game logic still updates every frame, only drawing is skipped.
        /// </summary>
        public static int RenderInterval(int targetFps, bool gameplay, float idleSeconds, bool savePower)
        {
            if (targetFps <= 0) targetFps = 60;
            if (gameplay) return 1;
            int fps = idleSeconds >= MenuIdleSeconds ? (savePower ? 15 : 30) : (savePower ? 30 : 60);
            return Math.Max(1, targetFps / fps);
        }
    }

    /// <summary>Rolling frame-time statistics for the optional SHOW FPS readout (no allocations).</summary>
    public sealed class FrameStats
    {
        private readonly float[] _samples;
        private int _next, _count;
        private float _sum;

        public FrameStats(int window = 60) => _samples = new float[Math.Max(1, window)];

        public void Add(float seconds)
        {
            if (_count == _samples.Length) _sum -= _samples[_next];
            else _count++;
            _samples[_next] = seconds;
            _sum += seconds;
            _next = (_next + 1) % _samples.Length;
        }

        public float AverageMs => _count == 0 ? 0f : _sum / _count * 1000f;
        public int Fps => _count == 0 || _sum <= 0f ? 0 : (int)Math.Round(_count / _sum);

        /// <summary>Worst frame in the window, in milliseconds.</summary>
        public float WorstMs
        {
            get
            {
                float worst = 0f;
                for (int i = 0; i < _count; i++) worst = Math.Max(worst, _samples[i]);
                return worst * 1000f;
            }
        }
    }
}
