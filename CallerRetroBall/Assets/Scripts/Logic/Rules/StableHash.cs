namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Platform-stable hashing and a tiny deterministic RNG. string.GetHashCode()
    /// is randomised per process in .NET, so never use it for generated content.
    /// </summary>
    public static class StableHash
    {
        /// <summary>32-bit FNV-1a.</summary>
        public static uint Of(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                if (text == null) return hash;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }
                return hash == 0 ? 1u : hash;
            }
        }

        /// <summary>xorshift32 step. Never returns 0 for a non-zero input.</summary>
        public static uint Next(uint state)
        {
            if (state == 0) state = 0x9E3779B9;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }

    /// <summary>Seedable deterministic RNG used by gameplay so tests can reproduce outcomes.</summary>
    public sealed class SeededRandom
    {
        private uint _state;

        public SeededRandom(uint seed)
        {
            _state = seed == 0 ? 0x9E3779B9 : seed;
        }

        public uint NextUInt()
        {
            _state = StableHash.Next(_state);
            return _state;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => NextFloat() < probability;
    }
}
