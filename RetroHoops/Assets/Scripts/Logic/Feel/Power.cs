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

    /// <summary>
    /// Phase 37: notices when a phone can't keep up with 120 Hz during play. If at least a quarter of the frames in
    /// a four-second window run long (more than 1.5× the frame budget), it reports <see cref="Struggling"/> and the
    /// game drops to a steady 60 fps, which looks smoother than an uneven 120. After a minute at 60 with no long
    /// frames it tries 120 again, once; if the phone struggles a second time it stays at 60 until the app restarts.
    /// </summary>
    public sealed class FrameBudget
    {
        public const float WindowSeconds = 4f;
        public const float LongFactor = 1.5f;
        public const float LongShare = 0.25f;
        public const float RetrySeconds = 60f;
        public const int MaxStrikes = 2;

        private float _windowTime;
        private int _frames, _long;
        private float _calmTime;

        public bool Struggling { get; private set; }
        /// <summary>Times it has dropped to 60 this session.</summary>
        public int Strikes { get; private set; }

        /// <summary>Adds one frame (<paramref name="seconds"/> long, asked for at <paramref name="targetFps"/>). Returns true when <see cref="Struggling"/> changed.</summary>
        public bool Observe(float seconds, int targetFps, bool gameplay)
        {
            if (!gameplay) { ResetWindow(); return false; } // menus don't count, and a new game starts a fresh window
            if (seconds <= 0f || seconds > 0.5f || targetFps <= 0) return false; // pauses and hitches from loading don't count
            float budget = 1f / targetFps;
            if (!Struggling)
            {
                if (targetFps < 100) return false; // only 120 Hz can be too much
                _windowTime += seconds;
                _frames++;
                if (seconds > budget * LongFactor) _long++;
                if (_windowTime < WindowSeconds) return false;
                bool tooSlow = _long >= _frames * LongShare;
                ResetWindow();
                if (!tooSlow) return false;
                Struggling = true;
                Strikes++;
                _calmTime = 0f;
                return true;
            }
            // At 60: count calm time; any long frame at 60 restarts the wait.
            if (Strikes >= MaxStrikes) return false;
            if (seconds > budget * LongFactor) _calmTime = 0f;
            else _calmTime += seconds;
            if (_calmTime < RetrySeconds) return false;
            Struggling = false;
            ResetWindow();
            return true;
        }

        private void ResetWindow()
        {
            _windowTime = 0f;
            _frames = 0;
            _long = 0;
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
