using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Optional CRT look over every screen: scanlines plus (on Strong) a vignette. Generated
    /// textures, one persistent overlay canvas that never blocks touches. Settings ► CRT FILTER.
    /// </summary>
    public sealed class CrtOverlay : MonoBehaviour
    {
        private static CrtOverlay _instance;
        private RawImage _lines;
        private RawImage _vignette;
        private RawImage _tint;
        private bool _tintOn;
        private Texture2D _lineTex, _vigTex;
        private int _level = -1;
        private int _screenH;

        /// <summary>POCKET GREEN secret: wash the whole screen in old-handheld green.</summary>
        public static void ApplyTint(bool on)
        {
            if (_instance == null)
            {
                if (!on) return;
                Apply(0);
                if (_instance == null)
                {
                    var go = new GameObject("[CrtOverlay]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CrtOverlay>();
                    _instance.Build();
                    _instance.SetLevel(0);
                }
            }
            _instance._tintOn = on;
            _instance._tint.enabled = on;
            if (on) _instance.gameObject.SetActive(true);
            else if (_instance._level <= 0) _instance.gameObject.SetActive(false);
        }

        /// <summary>0 = off, 1 = soft, 2 = strong.</summary>
        public static void Apply(int level)
        {
            if (_instance == null)
            {
                if (level <= 0) return;
                var go = new GameObject("[CrtOverlay]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CrtOverlay>();
                _instance.Build();
            }
            _instance.SetLevel(level);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // above the game and menus, below the scene wipe
            _tint = NewImage("PocketGreen");
            _tint.color = new Color(0.55f, 0.74f, 0.06f, 0.38f);
            _tint.enabled = false;
            _lines = NewImage("Scanlines");
            _vignette = NewImage("Vignette");
        }

        private RawImage NewImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        private void SetLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            if (level == _level) return;
            _level = level;
            gameObject.SetActive(level > 0 || _tintOn);
            _lines.enabled = _vignette.enabled = level > 0;
            if (level == 0) return;
            if (_lineTex != null) Destroy(_lineTex);
            if (_vigTex != null) Destroy(_vigTex);
            _lineTex = ToTexture(CrtPattern.Scanlines(level), FilterMode.Point, TextureWrapMode.Repeat);
            _vigTex = ToTexture(CrtPattern.Vignette(64, level), FilterMode.Bilinear, TextureWrapMode.Clamp);
            _lines.texture = _lineTex;
            _vignette.texture = _vigTex;
            _screenH = 0;
        }

        private static Texture2D ToTexture(PixelCanvas c, FilterMode filter, TextureWrapMode wrap)
        {
            var tex = new Texture2D(c.Width, c.Height, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = wrap };
            var px = new Color32[c.Width * c.Height];
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    var col = c.Get(x, y);
                    px[y * c.Width + x] = new Color32(col.r, col.g, col.b, col.a);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }

        private void LateUpdate()
        {
            if (_level <= 0 || Screen.height == _screenH) return;
            _screenH = Screen.height;
            // One scanline every ~2 screen pixels per texture row: visible on phones, not muddy.
            float repeats = Mathf.Max(60f, Screen.height / (CrtPattern.ScanlineHeight * 2f));
            _lines.uvRect = new Rect(0f, 0f, 1f, repeats);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_lineTex != null) Destroy(_lineTex);
            if (_vigTex != null) Destroy(_vigTex);
        }
    }
}
