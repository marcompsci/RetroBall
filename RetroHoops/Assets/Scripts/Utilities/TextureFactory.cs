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
            // Keyed by look as well as id: your created team's logo changes when you edit it.
            string key = "logo:" + team.id + ":" + (int)team.logoShape + ":" + (int)team.logoMotif + ":" +
                         team.primary.r + "," + team.primary.g + "," + team.primary.b + ":" + team.secondary.r + "," + team.secondary.g + "," + team.secondary.b +
                         ":" + team.accent.r + "," + team.accent.g + "," + team.accent.b;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = ToTexture(LogoGenerator.Generate(team), key);
            Cache[key] = tex;
            return tex;
        }

        /// <summary>8×8 pixel icon for a secret-code symbol, outlined, in <paramref name="color"/>.</summary>
        public static Texture2D CodeIcon(CodeSymbol symbol, RgbColor color)
        {
            string key = "code:" + symbol + ":" + color.r + "," + color.g + "," + color.b;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var mask = Secrets.SymbolIcon(symbol);
            var canvas = new PixelCanvas(10, 10);
            var outline = new RgbColor(0x14, 0x14, 0x20);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (mask[y][x] == '#')
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                canvas.Set(x + 1 + ox, 8 - y + oy, outline);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if (mask[y][x] == '#') canvas.Set(x + 1, 8 - y, color);
            var tex = ToTexture(canvas, key);
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

        /// <summary>Drops the cache without destroying anything (textures still on screen stay; the rest can be unloaded).</summary>
        public static void ForgetCache() => Cache.Clear();

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
