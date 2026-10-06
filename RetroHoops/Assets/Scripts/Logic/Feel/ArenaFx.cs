using System;
using System.Collections.Generic;
using CallerRetroBall.Logic.PixelArt;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 35 arena polish, worked out here so it's the same on every phone and testable: camera flashes popping in
    /// the crowd after a big play, the crowd doing the wave during a run, and the crowd standing for a close finish.
    /// Looks only: nothing here touches the game. Flashes are skipped with Reduce Motion (they're flashing lights).
    /// </summary>
    public static class ArenaFx
    {
        /// <summary>How long camera flashes keep popping after a big play.</summary>
        public const float FlashSeconds = 1.2f;
        public const int MaxFlashes = 10;
        /// <summary>Points in a row (unanswered) that start the wave.</summary>
        public const int WaveRun = 6;
        public const float WaveSeconds = 3.5f;

        public struct Flash
        {
            public int Fan;
            public float Delay;
        }

        /// <summary>Which fans flash and when (seconds after the play), from <paramref name="seed"/>: a few, spread out.</summary>
        public static List<Flash> Flashes(uint seed, int fans, int count)
        {
            var list = new List<Flash>();
            if (fans <= 0) return list;
            count = Math.Max(0, Math.Min(Math.Min(count, MaxFlashes), fans));
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            var used = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int fan = rng.Range(0, fans);
                for (int tries = 0; tries < 4 && used.Contains(fan); tries++) fan = rng.Range(0, fans);
                if (!used.Add(fan)) continue;
                list.Add(new Flash { Fan = fan, Delay = FlashSeconds * i / Math.Max(1, count) + rng.NextFloat() * 0.08f });
            }
            return list;
        }

        /// <summary>Flashes for a basket: more for dunks and deep shots, none for an ordinary bucket.</summary>
        public static int FlashCount(bool dunk, bool deep, bool clutch) => (dunk ? 7 : deep ? 4 : 0) + (clutch ? 3 : 0);

        /// <summary>
        /// The wave: a crest that travels across the stands left to right. Lift in art pixels (0, 1 or 2) for a fan at
        /// <paramref name="x01"/> (0 = far left, 1 = far right), <paramref name="t"/> seconds after it started.
        /// </summary>
        public static int WaveLift(float x01, float t)
        {
            if (t < 0f || t > WaveSeconds) return 0;
            float crest = t / WaveSeconds * 1.4f - 0.2f;
            float d = Math.Abs(x01 - crest);
            return d < 0.06f ? 2 : d < 0.14f ? 1 : 0;
        }

        /// <summary>Unanswered points: the run so far, given who just scored and how many.</summary>
        public static int Run(int runTeam, int run, int scoringTeam, int points, out int newRunTeam)
        {
            newRunTeam = scoringTeam;
            return scoringTeam == runTeam ? run + points : points;
        }

        /// <summary>
        /// A close finish: the clock is under <paramref name="lateSeconds"/> (or, with no clock, someone is within two
        /// baskets of the target) and the game is within one score.
        /// </summary>
        public static bool Clutch(int scoreA, int scoreB, float clock, bool usesClock, int target, int margin = 3, float lateSeconds = 30f)
        {
            if (Math.Abs(scoreA - scoreB) > margin) return false;
            if (usesClock) return clock > 0f && clock <= lateSeconds;
            return target > 0 && Math.Max(scoreA, scoreB) >= target - 4;
        }

        /// <summary>The crowd's idle bob with a close finish on: everyone on their feet, bouncing.</summary>
        public static int ClutchBob(float t, int fanIndex) => ((int)(t * 4f) + fanIndex) % 3 == 0 ? 1 : 0;
    }

    /// <summary>
    /// Phase 35 sound mix: the music and crowd dip under the announcer so the call is clear, the crowd swells with
    /// the mood of the game, and a burst of the same sound in one moment is softened rather than stacking into a roar.
    /// </summary>
    public static class AudioMix
    {
        /// <summary>How far the music dips under the announcer (0..1 of its level).</summary>
        public const float DuckDepth = 0.45f;
        /// <summary>The crowd dips less (it's the room the call happens in).</summary>
        public const float CrowdDuckDepth = 0.25f;
        public const float DuckAttackPerSecond = 8f, DuckReleasePerSecond = 1.6f;
        /// <summary>The same sound again within this many seconds is softened.</summary>
        public const float RepeatWindow = 0.06f;

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> at the given rates (per second), never past it.</summary>
        public static float Approach(float current, float target, float dt, float upPerSecond, float downPerSecond)
        {
            if (dt <= 0f) return current;
            if (current < target) return Math.Min(target, current + upPerSecond * dt);
            return Math.Max(target, current - downPerSecond * dt);
        }

        /// <summary>The duck gain (1 = no dip) one frame later: quick down when the announcer speaks, slow back up.</summary>
        public static float Duck(float current, bool voice, float dt, float depth = DuckDepth) =>
            Approach(current, voice ? 1f - depth : 1f, dt, DuckReleasePerSecond, DuckAttackPerSecond);

        /// <summary>How loud the crowd murmur sits (1 = normal) for a mood, louder for a close finish.</summary>
        public static float CrowdLevel(CrowdMood mood, bool clutch)
        {
            float level = mood == CrowdMood.Cheer ? 1.7f : mood == CrowdMood.Groan ? 0.75f : 1f;
            return level + (clutch ? 0.3f : 0f);
        }

        /// <summary>Volume for a sound played <paramref name="repeats"/> times already within <see cref="RepeatWindow"/>.</summary>
        public static float RepeatGain(int repeats) => repeats <= 0 ? 1f : 1f / (1f + 0.6f * repeats);
    }
}
