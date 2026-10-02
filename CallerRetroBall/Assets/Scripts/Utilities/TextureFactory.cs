using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Utilities
{
    /// <summary>
    /// Converts procedurally generated <see cref="PixelCanvas"/> art into point-filtered
    /// textures and sprites, with a small cache so menus don't regenerate logos.
    /// </summary>
    public static class TextureFactory
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D ToTexture(PixelCanvas canvas, string name)
        {
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[canvas.Pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = canvas.Pixels[i];
                pixels[i] = new Color32(p.r, p.g, p.b, p.a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true); // upload and free the CPU copy
            return tex;
        }

        /// <summary>Creates a sprite. A non-zero <paramref name="border"/> makes it 9-sliceable.</summary>
        public static Sprite ToSprite(PixelCanvas canvas, string name, float pixelsPerUnit = 100f, Vector4 border = default)
        {
            var tex = ToTexture(canvas, name);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                                       pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }

        public static Texture2D TeamLogo(TeamDef team)
        {
            string key = "logo:" + team.id;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = ToTexture(LogoGenerator.Generate(team), key);
            Cache[key] = tex;
            return tex;
        }

        public static Texture2D Backdrop(CourtDef court, uint seed)
        {
            string key = "backdrop:" + court.id + ":" + seed;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = ToTexture(BackdropGenerator.Generate(court, seed), key);
            Cache[key] = tex;
            return tex;
        }

        public static void ClearCache()
        {
            if (Application.isPlaying)
            {
                foreach (var tex in Cache.Values)
                    if (tex != null) Object.Destroy(tex);
            }
            Cache.Clear();
        }
    }
}
