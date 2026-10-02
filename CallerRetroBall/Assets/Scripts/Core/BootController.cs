using System.Collections;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// BootScene entry point: initialises services, shows a brief title card, then
    /// hands off to the main menu.
    /// </summary>
    public sealed class BootController : MonoBehaviour
    {
        [SerializeField] private float minimumSplashSeconds = 0.8f;

        private IEnumerator Start()
        {
            float started = Time.realtimeSinceStartup;
            App.EnsureInitialized();
            UiKit.CheckTextMeshProReady();

            var canvas = UiKit.CreateScreenCanvas("BootCanvas");
            var bg = UiKit.Panel(canvas.transform, Theme.Ink);
            UiKit.Stretch(bg.rectTransform);

            var title = UiKit.Label(canvas.transform, "CALLER\nRETRO BALL", 120, Theme.Cream, TextAlignmentOptions.Center, bold: true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(1000, 360));
            var tagline = UiKit.Label(canvas.transform, Theme.Tagline, 44, Theme.Cyan, TextAlignmentOptions.Center);
            UiKit.Place(tagline.rectTransform, new Vector2(0.5f, 0.42f), new Vector2(1000, 80));

            while (Time.realtimeSinceStartup - started < minimumSplashSeconds) yield return null;
            SceneFlow.GoTo(SceneNames.MainMenu);
        }
    }
}
