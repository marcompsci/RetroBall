using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.UI
{
    public enum ButtonStyle { Primary = 0, Secondary = 1, Ghost = 2 }

    /// <summary>Original Caller Retro Ball UI palette and generated pixel skin.</summary>
    public static class Theme
    {
        public const string Title = "CALLER RETRO BALL";
        public const string Tagline = "Call your shot. Build your legacy.";

        public static readonly Color Ink = new Color32(0x1A, 0x1A, 0x2E, 255);
        public static readonly Color InkLight = new Color32(0x2A, 0x2A, 0x45, 255);
        public static readonly Color Asphalt = new Color32(0x2F, 0x2F, 0x36, 255);
        public static readonly Color Pink = new Color32(0xF7, 0x25, 0x85, 255);
        public static readonly Color Cyan = new Color32(0x4C, 0xC9, 0xF0, 255);
        public static readonly Color Gold = new Color32(0xFF, 0xD1, 0x66, 255);
        public static readonly Color Cream = new Color32(0xF4, 0xF1, 0xDE, 255);
        public static readonly Color Muted = new Color32(0x8D, 0x99, 0xAE, 255);
        public static readonly Color Shadow = new Color32(0x0B, 0x0B, 0x16, 255);
        public static readonly Color Scrim = new Color32(0x1A, 0x1A, 0x2E, 200);

        /// <summary>Canvas units per art pixel for chunky 9-slice borders.</summary>
        public const float PixelSize = 6f;

        private static Sprite _primary, _secondary, _ghost, _panel;

        public static Sprite ButtonSprite(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary:
                    return _primary != null ? _primary : (_primary = Chunky("ui.button.primary", Pink.ToRgb32(), Shadow.ToRgb32()));
                case ButtonStyle.Secondary:
                    return _secondary != null ? _secondary : (_secondary = Chunky("ui.button.secondary", InkLight.ToRgb32(), Cyan.ToRgb32()));
                default:
                    return _ghost != null ? _ghost : (_ghost = Chunky("ui.button.ghost", new RgbColor(0, 0, 0, 90), Cream.ToRgb32()));
            }
        }

        public static Sprite PanelSprite() =>
            _panel != null ? _panel : (_panel = Chunky("ui.panel", new RgbColor(0x1A, 0x1A, 0x2E, 225), Muted.ToRgb32()));

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Discs =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Round pixel button face (24x24 art pixels) with a 2px border and top highlight.</summary>
        public static Sprite DiscSprite(Color fill, Color border)
        {
            string key = ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(border);
            if (Discs.TryGetValue(key, out var cached) && cached != null) return cached;

            const int s = 24;
            var c = new PixelCanvas(s, s);
            var f = fill.ToRgb32();
            var b = border.ToRgb32();
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > s / 2f) continue;
                    RgbColor col = d > s / 2f - 2f ? b : (dy > s / 2f - 6f && f.a == 255 ? f.Lighten(0.2f) : f);
                    c.Set(x, y, col);
                }
            var sprite = TextureFactory.ToSprite(c, "ui.disc." + key);
            Discs[key] = sprite;
            return sprite;
        }

        private static RgbColor ToRgb32(this Color c) => ((Color32)c).ToRgb();

        /// <summary>
        /// 12x12 chunky pixel frame: clipped corners, 1px border, top highlight,
        /// bottom shade. Sliced with a 4px border so it scales to any size.
        /// </summary>
        private static Sprite Chunky(string name, RgbColor fill, RgbColor border)
        {
            const int s = 12;
            var c = new PixelCanvas(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int dx = Mathf.Min(x, s - 1 - x), dy = Mathf.Min(y, s - 1 - y);
                    if (dx + dy < 2) continue;                       // clipped corner
                    bool edge = dx == 0 || dy == 0 || dx + dy == 2;
                    RgbColor col = edge ? border : fill;
                    if (!edge && y == s - 2) col = fill.a == 255 ? fill.Lighten(0.25f) : fill; // highlight
                    if (!edge && y == 1) col = fill.a == 255 ? fill.Darken(0.25f) : fill;      // shade
                    c.Set(x, y, col);
                }
            return TextureFactory.ToSprite(c, name, 100f, new Vector4(4, 4, 4, 4));
        }
    }
}
