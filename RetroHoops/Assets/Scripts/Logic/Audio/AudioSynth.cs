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
        /// <summary>Two-note "announcer" sting for a big play.</summary>
        Stinger = 12,
        /// <summary>Rising arpeggio for HEATING UP / ON FIRE.</summary>
        OnFire = 13,
        /// <summary>Short fanfare for titles and records.</summary>
        Fanfare = 14,
        /// <summary>Crackling whoosh when a player heats up (HEAT CHECK).</summary>
        HeatUp = 15,
        /// <summary>Airy whoosh for an alley-oop lob.</summary>
        Lob = 16,
        /// <summary>Two-tone "coin" chime: secret code accepted, continue used.</summary>
        Coin = 17,
        /// <summary>Low buzz: wrong code.</summary>
        Error = 18,
        /// <summary>Short rising jingle when a game starts.</summary>
        TipOff = 19,
        /// <summary>Win jingle.</summary>
        Victory = 20,
        /// <summary>Loss jingle (gentle, not mocking).</summary>
        Defeat = 21,
        /// <summary>The crowd's "OOOOH": a vowel-ish swell for blocks, broken ankles and huge dunks.</summary>
        CrowdOooh = 22,
        /// <summary>Deep arena horn: final buzzer and substitutions.</summary>
        ArenaHorn = 23,
        /// <summary>Crowd clapping in rhythm (clap, clap, clap-clap-clap) in a tight finish.</summary>
        ClapChant = 24,
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
                case SfxId.Stinger: return Notes(new[] { 79, 84 }, 0.11f, 0.4f);
                case SfxId.OnFire: return Notes(new[] { 72, 76, 79, 84, 88 }, 0.06f, 0.35f);
                case SfxId.Fanfare: return Notes(new[] { 67, 72, 76, 79, 84, 84 }, 0.1f, 0.4f);
                case SfxId.HeatUp: return Mix(Noise(0.5f, rng, 0.3f, attack: 0.3f, lowpass: 0.5f), Chirp(0.5f, 300f, 1400f, 0.3f));
                case SfxId.Lob: return Noise(0.4f, rng, 0.25f, attack: 0.5f, lowpass: 0.12f);
                case SfxId.Coin: return Notes(new[] { 83, 88 }, 0.07f, 0.35f);
                case SfxId.Error: return Square(0.25f, 110f, 0.3f);
                case SfxId.TipOff: return Notes(new[] { 64, 67, 71, 76 }, 0.08f, 0.35f);
                case SfxId.Victory: return Notes(new[] { 72, 76, 79, 84, 79, 84, 88 }, 0.09f, 0.4f);
                case SfxId.Defeat: return Notes(new[] { 67, 64, 60, 55 }, 0.14f, 0.3f);
                case SfxId.CrowdOooh: return Oooh(1.3f, rng, 0.5f);
                case SfxId.ArenaHorn: return Horn(1.1f, 0.45f);
                case SfxId.ClapChant: return Claps(new[] { 0f, 0.42f, 0.84f, 1.05f, 1.26f }, rng, 0.45f);
                default: return Square(0.6f, 220f, 0.3f);
            }
        }

        public static float Duration(float[] samples) => samples.Length / (float)SampleRate;

        /// <summary>
        /// Announcer "voice": one formant-ish blip per syllable (square wave through two vowel
        /// resonances) following <see cref="Announcer.Contour"/>. Deterministic per phrase.
        /// </summary>
        public static float[] Voice(string phrase)
        {
            var notes = Announcer.Contour(phrase);
            const float syllable = 0.085f, gap = 0.025f;
            int per = (int)((syllable + gap) * SampleRate);
            var s = new float[per * notes.Count + (int)(0.05f * SampleRate)];
            var rng = new SeededRandom(StableHash.Of("vowels:" + phrase));
            // Vowel formant pairs (Hz): "ah", "eh", "ee", "oh", "oo".
            float[,] formants = { { 730, 1090 }, { 530, 1840 }, { 270, 2290 }, { 570, 840 }, { 300, 870 } };
            for (int k = 0; k < notes.Count; k++)
            {
                float f0 = Midi(notes[k]);
                int v = rng.Range(0, 5);
                float fa = formants[v, 0], fb = formants[v, 1];
                int start = k * per;
                int len = (int)(syllable * SampleRate);
                for (int i = 0; i < len && start + i < s.Length; i++)
                {
                    float t = i / (float)SampleRate;
                    float env = Math.Min(1f, i / 120f) * Math.Min(1f, (len - i) / 300f);
                    // Pulse train at the pitch, shaped by two resonances.
                    float pulse = (t * f0) % 1f < 0.25f ? 1f : -0.33f;
                    float res = 0.6f * (float)Math.Sin(2 * Math.PI * fa * t) + 0.4f * (float)Math.Sin(2 * Math.PI * fb * t);
                    s[start + i] += pulse * (0.55f + 0.45f * res) * env;
                }
            }
            return Normalize(s, 0.45f);
        }

        private static float[] Mix(float[] a, float[] b)
        {
            var s = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < s.Length; i++) s[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            return Normalize(s, 0.4f);
        }

        public const int MusicTrackCount = 3;

        /// <summary>
        /// Original chiptune loops, 32 beats each so they loop seamlessly.
        /// 0 = "Sunset Cage" (menus): C major, 112 BPM. 1 = "Blacktop Bounce" (matches): A minor,
        /// 126 BPM, with a kick drum. 2 = "Gold Signal" (Rise hub): D major, 100 BPM, bright arpeggio.
        /// </summary>
        public static float[] MusicLoop(int track = 0)
        {
            switch (((track % MusicTrackCount) + MusicTrackCount) % MusicTrackCount)
            {
                case 1:
                    return Loop(126f, new[] { 45, 41, 48, 43 }, new[] { 0, 7, 12, 7, 3, 7, 12, 15 }, 535353, kick: true, arpLevel: 0.06f);
                case 2:
                    return Loop(100f, new[] { 50, 45, 47, 43 }, new[] { 0, 4, 7, 11, 12, 11, 7, 4 }, 909090, kick: false, arpLevel: 0.08f);
                default:
                    return Loop(112f, new[] { 48, 45, 41, 43 }, new[] { 0, 4, 7, 12, 7, 4, 0, 7 }, 424242, kick: false, arpLevel: 0.07f);
            }
        }

        private static float[] Loop(float bpm, int[] roots, int[] arp, uint seed, bool kick, float arpLevel)
        {
            const int beats = 32;
            float beat = 60f / bpm;
            int n = (int)(beats * beat * SampleRate);
            var s = new float[n];
            var rng = new SeededRandom(seed);
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
                    AddTone(s, start + e * len / 2, len / 2 - 200, Midi(note), arpLevel, square: true);
                }
                // Kick on the beat (pitch-dropping sine).
                if (kick)
                {
                    double ph = 0;
                    int kl = Math.Min(len / 3, 3000);
                    for (int i = 0; i < kl && start + i < n; i++)
                    {
                        float t = i / (float)kl;
                        ph += 2 * Math.PI * (110f - 70f * t) / SampleRate;
                        s[start + i] += (float)Math.Sin(ph) * 0.22f * (1f - t);
                    }
                }
                // Hi-hat on off-beats.
                int hat = start + len / 2;
                for (int i = 0; i < 1400 && hat + i < n; i++)
                    s[hat + i] += (rng.NextFloat() * 2f - 1f) * 0.05f * (1f - i / 1400f);
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>A quick square-wave phrase (stingers).</summary>
        private static float[] Notes(int[] midi, float noteSeconds, float amp)
        {
            int noteLen = (int)(noteSeconds * SampleRate);
            int tail = noteLen * 2;
            var s = new float[noteLen * midi.Length + tail];
            for (int i = 0; i < midi.Length; i++)
            {
                bool last = i == midi.Length - 1;
                AddTone(s, i * noteLen, last ? noteLen + tail : noteLen - 100, Midi(midi[i]), 0.5f, square: true);
                AddTone(s, i * noteLen, last ? noteLen + tail : noteLen - 100, Midi(midi[i] - 12), 0.25f, square: false);
            }
            return Normalize(s, amp);
        }

        /// <summary>
        /// Low crowd murmur for matches: filtered noise with slow swells, 4 s, looping seamlessly
        /// (the tail is cross-faded into the head so there is no click at the loop point).
        /// </summary>
        public static float[] CrowdAmbience()
        {
            const float seconds = 4f;
            int n = (int)(seconds * SampleRate);
            int fade = SampleRate / 2;
            var raw = new float[n + fade];
            var rng = new SeededRandom(777);
            float y1 = 0f, y2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = rng.NextFloat() * 2f - 1f;
                y1 += 0.05f * (x - y1);  // low rumble
                y2 += 0.18f * (x - y2);  // chatter
                // Two slow swells per loop, plus an off-beat one; periods divide the loop length.
                float swell = 0.75f + 0.15f * (float)Math.Sin(2 * Math.PI * t / 2.0) + 0.1f * (float)Math.Sin(2 * Math.PI * t / 1.0 + 1.3);
                raw[i] = (y1 * 1.6f + y2 * 0.35f) * swell;
            }
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < fade)
                {
                    float w = i / (float)fade;
                    s[i] = raw[i] * w + raw[n + i] * (1f - w);
                }
                else
                {
                    s[i] = raw[i];
                }
            }
            return Normalize(s, 0.5f);
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

        /// <summary>Crowd noise shaped by a sliding vowel formant (low "oo" → open "oh"), swelling then fading.</summary>
        private static float[] Oooh(float seconds, SeededRandom rng, float amp)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            float lp = 0f, bp1 = 0f, bp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = (float)Math.Sin(Math.PI * Math.Min(1f, t * 1.15f)) * (1f - 0.3f * t);
                float x = rng.NextFloat() * 2f - 1f;
                // Many voices: band-limited noise around a formant that opens from ~300 Hz to ~650 Hz.
                float formant = 300f + 350f * Math.Min(1f, t * 1.6f);
                float k = Math.Min(0.9f, 2f * (float)Math.PI * formant / SampleRate * 1.6f);
                lp += k * (x - lp);
                bp1 += k * 0.5f * (lp - bp1);
                bp2 += 0.04f * (x - bp2); // low rumble under it
                s[i] = ((lp - bp1) * 2.2f + bp2 * 1.2f) * env;
            }
            return Normalize(s, amp);
        }

        /// <summary>Arena horn: a stack of slightly detuned low square-ish tones with a flat sustain.</summary>
        private static float[] Horn(float seconds, float amp)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            float[] freqs = { 110f, 110.6f, 164.8f, 220.4f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = i / (float)n;
                float env = Math.Min(1f, u * 25f) * (u > 0.88f ? (1f - u) / 0.12f : 1f);
                float v = 0f;
                foreach (var f in freqs)
                {
                    float ph = (t * f) % 1f;
                    v += (ph < 0.5f ? 1f : -1f) * 0.6f + (float)Math.Sin(2 * Math.PI * f * t) * 0.4f;
                }
                s[i] = v / freqs.Length * env;
            }
            return Normalize(s, amp);
        }

        /// <summary>Hand claps (short noise bursts with a body thump) at the given times, many hands slightly out of time.</summary>
        private static float[] Claps(float[] times, SeededRandom rng, float amp)
        {
            int n = (int)((times[times.Length - 1] + 0.3f) * SampleRate);
            var s = new float[n];
            foreach (var at in times)
            {
                for (int hand = 0; hand < 6; hand++)
                {
                    int start = (int)((at + rng.NextFloat() * 0.03f) * SampleRate);
                    int len = (int)(0.07f * SampleRate);
                    float hp = 0f, prev = 0f;
                    for (int j = 0; j < len && start + j < n; j++)
                    {
                        float x = rng.NextFloat() * 2f - 1f;
                        hp = 0.85f * (hp + x - prev); // bright: high-passed noise
                        prev = x;
                        float env = (float)Math.Exp(-j / (0.012f * SampleRate));
                        s[start + j] += hp * env / 6f;
                    }
                }
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
