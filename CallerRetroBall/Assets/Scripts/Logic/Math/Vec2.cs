using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Engine-free 2D vector in court space (metres). Converted to UnityEngine.Vector2/3 only
    /// at the view boundary so all gameplay maths stays unit-testable.
    /// </summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float x;
        public float y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 Up = new Vec2(0f, 1f);
        public static readonly Vec2 Right = new Vec2(1f, 0f);

        public float SqrMagnitude => x * x + y * y;
        public float Magnitude => (float)Math.Sqrt(x * x + y * y);

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? new Vec2(x / m, y / m) : Zero;
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.y + b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.x, -a.y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.x * s, a.y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.x / s, a.y / s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public static float Dot(Vec2 a, Vec2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Magnitude;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return new Vec2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        }

        public static Vec2 ClampMagnitude(Vec2 v, float max)
        {
            float sq = v.SqrMagnitude;
            if (sq <= max * max) return v;
            float m = (float)Math.Sqrt(sq);
            return new Vec2(v.x / m * max, v.y / m * max);
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDelta"/>.</summary>
        public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDelta)
        {
            var d = target - current;
            float dist = d.Magnitude;
            if (dist <= maxDelta || dist < 1e-6f) return target;
            return current + d / dist * maxDelta;
        }

        public bool ApproximatelyEquals(Vec2 other, float tolerance = 1e-4f) =>
            Math.Abs(x - other.x) <= tolerance && Math.Abs(y - other.y) <= tolerance;

        public bool Equals(Vec2 other) => x.Equals(other.x) && y.Equals(other.y);
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() * 397 ^ y.GetHashCode();
        public override string ToString() => "(" + x.ToString("0.###") + ", " + y.ToString("0.###") + ")";
    }
}
