using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Pooled one-pixel sparks (made shots, dunks, blocks, steals). Motion comes from
    /// <see cref="Bursts"/>; positions snap to art pixels so the effect stays crisp.
    /// No allocations after creation.
    /// </summary>
    public sealed class PixelBursts : MonoBehaviour
    {
        private const int PoolSize = 160;

        private struct Spark
        {
            public SpriteRenderer Renderer;
            public BurstParticle Motion;
            public Vector3 Origin;
            public Color Color;
            public float Age;
            public bool Active;
        }

        private readonly Spark[] _sparks = new Spark[PoolSize];
        private int _next;
        private uint _seed = 1;
        private readonly BurstParticle[] _scratch = new BurstParticle[PoolSize];

        public static PixelBursts Create(Transform parent, MatchArt art)
        {
            var go = new GameObject("PixelBursts");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<PixelBursts>();
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Spark");
                child.transform.SetParent(go.transform, false);
                var sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = art.Pixel;
                sr.sortingOrder = 31600; // above players, ball, and HUD-world markers
                sr.enabled = false;
                fx._sparks[i].Renderer = sr;
            }
            return fx;
        }

        /// <summary>Spawns <paramref name="count"/> sparks at <paramref name="origin"/> (world space).</summary>
        public void Spawn(Vector3 origin, Color color, int count, float speed = 4f, float life = 0.6f)
        {
            int n = Mathf.Min(count, PoolSize);
            Bursts.Fill(_seed++, _scratch, n, speed, life);
            var parts = _scratch;
            for (int i = 0; i < n; i++)
            {
                ref var s = ref _sparks[_next];
                _next = (_next + 1) % PoolSize;
                s.Motion = parts[i];
                s.Origin = origin;
                s.Color = color;
                s.Age = 0f;
                s.Active = true;
                s.Renderer.enabled = true;
                s.Renderer.color = color;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            const float ppu = CourtSpace.PixelsPerUnit;
            for (int i = 0; i < PoolSize; i++)
            {
                ref var s = ref _sparks[i];
                if (!s.Active) continue;
                s.Age += dt;
                if (s.Age >= s.Motion.life)
                {
                    s.Active = false;
                    s.Renderer.enabled = false;
                    continue;
                }
                Bursts.Offset(s.Motion, s.Age, out float x, out float y);
                var p = s.Origin + new Vector3(x, y, 0f);
                s.Renderer.transform.position = new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, 0f);
                // Hard pixel fade: full colour, then a dimmer step for the last third.
                var c = s.Color;
                if (s.Age > s.Motion.life * 0.66f) c.a *= 0.5f;
                s.Renderer.color = c;
            }
        }
    }
}
