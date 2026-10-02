using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Persistent scene loader with a pixel screen wipe (an old-console block dissolve; a plain
    /// fade when Reduce Motion is on). While a transition runs, a
    /// full-screen raycast blocker swallows input, so rapid taps on menu buttons
    /// can never start two loads or leave the game half-transitioned.
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        private const float FadeSeconds = 0.15f;
        private const int WipeCols = 32, WipeRows = 18;
        private Texture2D _wipe;
        private Color32[] _wipePixels;
        private static readonly Color32 WipeColor = new Color32(0x1A, 0x1A, 0x2E, 255);

        private static SceneFlow _instance;

        private CanvasGroup _fade;
        private bool _busy;

        public static bool IsTransitioning => _instance != null && _instance._busy;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[SceneFlow]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneFlow>();
            _instance.BuildOverlay();
        }

        /// <summary>Loads a scene by name. Ignored if a transition is already running.</summary>
        public static bool GoTo(string sceneName)
        {
            EnsureExists();
            if (_instance._busy) return false;
            _instance.StartCoroutine(_instance.Transition(sceneName));
            return true;
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("FadeCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvasGo.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("Fade", typeof(RectTransform));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _wipe = new Texture2D(WipeCols, WipeRows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _wipePixels = new Color32[WipeCols * WipeRows];
            var image = imageGo.AddComponent<RawImage>();
            image.texture = _wipe;
            SetWipe(1f);

            _fade = imageGo.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
        }

        private IEnumerator Transition(string sceneName)
        {
            _busy = true;
            _fade.blocksRaycasts = true;
            yield return Fade(0f, 1f);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[SceneFlow] Scene '" + sceneName + "' is not in Build Settings. Run 'RetroBall ▸ Run Project Setup'.");
            }
            else
            {
                while (!op.isDone) yield return null;
            }

            yield return Fade(1f, 0f);
            _fade.blocksRaycasts = false;
            _busy = false;
        }

        private static bool ReduceMotion => App.Career != null && App.Career.settings.reduceMotion;

        private IEnumerator Fade(float from, float to)
        {
            if (ReduceMotion)
            {
                SetWipe(1f);
                float t = 0f;
                while (t < FadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    _fade.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                    yield return null;
                }
                _fade.alpha = to;
                yield break;
            }
            // Pixel wipe: blocks sweep in (or back out) in a dithered diagonal.
            _fade.alpha = 1f;
            float w = 0f;
            float seconds = Logic.ScreenWipe.Seconds;
            while (w < seconds)
            {
                w += Time.unscaledDeltaTime;
                SetWipe(Mathf.Lerp(from, to, w / seconds));
                yield return null;
            }
            SetWipe(to);
            if (to <= 0f) _fade.alpha = 0f;
        }

        private void SetWipe(float t)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < WipeRows; y++)
                for (int x = 0; x < WipeCols; x++)
                    _wipePixels[y * WipeCols + x] = Logic.ScreenWipe.Covered(x, WipeRows - 1 - y, WipeCols, WipeRows, t) ? WipeColor : clear;
            _wipe.SetPixels32(_wipePixels);
            _wipe.Apply(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
