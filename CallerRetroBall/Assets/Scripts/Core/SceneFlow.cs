using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Persistent scene loader with a short fade. While a transition runs, a
    /// full-screen raycast blocker swallows input, so rapid taps on menu buttons
    /// can never start two loads or leave the game half-transitioned.
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        private const float FadeSeconds = 0.15f;

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
            var image = imageGo.AddComponent<Image>();
            image.color = new Color32(0x1A, 0x1A, 0x2E, 255);

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

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _fade.alpha = Mathf.Lerp(from, to, t / FadeSeconds);
                yield return null;
            }
            _fade.alpha = to;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
