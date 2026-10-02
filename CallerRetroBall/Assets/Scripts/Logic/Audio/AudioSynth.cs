using System;

namespace CallerRetroBall.Logic
{
    public enum SfxId
    {
        Bounce = 0,
        Swish = 1,
        Rim = 2,
        Backboard = 3,
        Squeak = 4,
        Whistle = 5,
        CrowdCheer = 6,
        CrowdGroan = 7,
        Click = 8,
        Steal = 9,
        Block = 10,
        Buzzer = 11,
    }

    /// <summary>
    /// Generates every sound in the game from simple synthesis (sine/square/noise with envelopes)
    /// so no third-party audio is bundled. Output is mono float PCM in [-1, 1]; deterministic.
    /// </summary>
    public static class AudioSynth
    {
        public const int SampleRate = 44100;

        public static float[] Sfx(SfxId id)
        {
            var rng = new SeededRandom(StableHash.Of("sfx:" + (int)id));
            switch (id)
            {
                case SfxId.Bounce: return Thump(0.12f, 95f, 55f, 0.9f, rng, 0.15f);
                case SfxId.Swish: return Noise(0.35f, rng, 0.35f, attack: 0.08f, lowpass: 0.25f);
                case SfxId.Rim: return Metal(0.4f, 480f, 0.55f, rng);
                case SfxId.Backboard: return Thump(0.18f, 140f, 90f, 0.8f, rng, 0.35f);
                case SfxId.Squeak: return Chirp(0.09f, 2200f, 3100f, 0.3f);
                case SfxId.Whistle: return Whistle(0.45f, 2600f, 0.35f);
                case SfxId.CrowdCheer: return Crowd(1.2f, rng, 0.45f, rising: true);
                case SfxId.CrowdGroan: return Crowd(0.9f, rng, 0.35f, rising: false);
                case SfxId.Click: return Square(0.04f, 880f, 0.25f);
                case SfxId.Steal: return Chirp(0.14f, 600f, 1200f, 0.35f);
                case SfxId.Block: return Thump(0.2f, 70f, 40f, 1f, rng, 0.5f);
                default: return Square(0.6f, 220f, 0.3f);
            }
        }

        public static float Duration(float[] samples) => samples.Length / (float)SampleRate;

        /// <summary>
        /// Short original chiptune loop: 8 bars of bass, arpeggio, and a hi-hat, 112 BPM.
        /// Loops seamlessly (length is an exact number of beats).
        /// </summary>
        public static float[] MusicLoop()
        {
            const float bpm = 112f;
            const int beats = 32;
            float beat = 60f / bpm;
            int n = (int)(beats * beat * SampleRate);
            var s = new float[n];
            // I – vi – IV – V in C (MIDI roots), two bars each.
            int[] roots = { 48, 45, 41, 43 };
            int[] arp = { 0, 4, 7, 12, 7, 4, 0, 7 };
            var rng = new SeededRandom(424242);
            for (int b = 0; b < beats; b++)
            {
                int root = roots[(b / 8) % roots.Length];
                int start = (int)(b * beat * SampleRate);
                int len = (int)(beat * SampleRate);
                // Bass on every beat (square, short).
                AddTone(s, start, (int)(len * 0.6f), Midi(root - 12), 0.18f, square: true);
                // Arpeggio: eighth notes.
                for (int e = 0; e < 2; e++)
                {
                    int note = root + 12 + arp[(b * 2 + e) % arp.Length];
                    AddTone(s, start + e * len / 2, len / 2 - 200, Midi(note), 0.07f, square: true);
                }
                // Hi-hat on off-beats.
                int hat = start + len / 2;
                for (int i = 0; i < 1400 && hat + i < n; i++)
                    s[hat + i] += (rng.NextFloat() * 2f - 1f) * 0.05f * (1f - i / 1400f);
            }
            return Normalize(s, 0.8f);
        }

        public static float Midi(int note) => 440f * (float)Math.Pow(2.0, (note - 69) / 12.0);

        // ------------------------------------------------------------------ building blocks

        private static float[] Thump(float seconds, float f0, float f1, float amp, SeededRandom rng, float noise)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = f0 + (f1 - f0) * t;
                phase += 2 * Math.PI * f / SampleRate;
                float env = (float)Math.Exp(-6f * t);
                s[i] = ((float)Math.Sin(phase) + (rng.NextFloat() * 2f - 1f) * noise * (1f - t)) * env * amp;
            }
            return Normalize(s, amp);
        }

        private static float[] Noise(float seconds, SeededRandom rng, float amp, float attack, float lowpass)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = t < attack ? t / attack : (float)Math.Exp(-5f * (t - attack));
                float x = rng.NextFloat() * 2f - 1f;
                y += lowpass * (x - y);
                s[i] = y * env;
            }
            return Normalize(s, amp);
        }

        private static float[] Metal(float seconds, float f, float amp, SeededRandom rng)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            float[] ratios = { 1f, 2.76f, 5.4f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = (float)Math.Exp(-9f * t);
                float v = 0f;
                for (int k = 0; k < ratios.Length; k++) v += (float)Math.Sin(2 * Math.PI * f * ratios[k] * t) / (k + 1);
                s[i] = (v + (i < 300 ? (rng.NextFloat() * 2f - 1f) : 0f)) * env;
            }
            return Normalize(s, amp);
        }

        private static float[] Chirp(float seconds, float f0, float f1, float amp)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2 * Math.PI * (f0 + (f1 - f0) * t) / SampleRate;
                float env = (float)Math.Sin(Math.PI * t);
                s[i] = (float)Math.Sin(phase) * env;
            }
            return Normalize(s, amp);
        }

        private static float[] Whistle(float seconds, float f, float amp)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float trill = 1f + 0.03f * (float)Math.Sin(2 * Math.PI * 28 * t);
                float env = Math.Min(1f, i / 800f) * Math.Min(1f, (n - i) / 1500f);
                s[i] = (float)Math.Sin(2 * Math.PI * f * trill * t) * env;
            }
            return Normalize(s, amp);
        }

        private static float[] Crowd(float seconds, SeededRandom rng, float amp, bool rising)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = rising ? (float)Math.Sin(Math.PI * Math.Min(1f, t * 1.3f)) : (1f - t) * (1f - t);
                float x = rng.NextFloat() * 2f - 1f;
                y1 += 0.08f * (x - y1);
                y2 += 0.3f * (x - y2);
                s[i] = (y1 * 1.4f + y2 * 0.3f) * env;
            }
            return Normalize(s, amp);
        }

        private static float[] Square(float seconds, float f, float amp)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = 1f - i / (float)n;
                s[i] = ((t * f) % 1f < 0.5f ? 1f : -1f) * env;
            }
            return Normalize(s, amp);
        }

        private static void AddTone(float[] s, int start, int length, float f, float amp, bool square)
        {
            for (int i = 0; i < length && start + i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Math.Min(1f, i / 200f) * Math.Min(1f, (length - i) / 400f);
                float v = square ? ((t * f) % 1f < 0.5f ? 1f : -1f) : (float)Math.Sin(2 * Math.PI * f * t);
                s[start + i] += v * env * amp;
            }
        }

        /// <summary>Scales so the peak equals <paramref name="peak"/> (silence stays silence).</summary>
        public static float[] Normalize(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Math.Max(max, Math.Abs(s[i]));
            if (max < 1e-6f) return s;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
            return s;
        }
    }
}
