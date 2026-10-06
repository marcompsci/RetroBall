using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Phase 35: a shot chart block for any vertical list (post-game, Franchise, Legacy, career): a heading, the pixel
    /// half-court with a disc per spot (<see cref="ShotChartArt"/>), and the hot / cold line. The texture is freed with it.
    /// </summary>
    public sealed class ShotChartView : MonoBehaviour
    {
        private Texture2D _texture;

        public static ShotChartView Build(Transform parent, string heading, ShotChartData chart, float chartHeight = 330f)
        {
            var block = UiKit.NewRect("Shot Chart", parent);
            var layout = block.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;
            var view = block.gameObject.AddComponent<ShotChartView>();

            if (!string.IsNullOrEmpty(heading))
                UiKit.Size(UiKit.Label(block, heading, 34f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);

            var canvas = ShotChartArt.Render(chart);
            view._texture = TextureFactory.ToTexture(canvas, "ui.shotchart");
            var holder = UiKit.NewRect("Chart", block);
            UiKit.Size(holder, chartHeight);
            var pic = UiKit.Picture(holder, view._texture, "Court");
            var rt = pic.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(chartHeight * canvas.Width / canvas.Height, chartHeight);

            UiKit.Size(UiKit.Label(block, ShotCharts.Summary(chart), 26f, Theme.Cream, TextAlignmentOptions.Center, true), 44f);
            UiKit.Size(UiKit.Label(block, "<color=#FF5448>RED</color> = HOT   <color=#489CFF>BLUE</color> = COLD   BIGGER = MORE SHOTS", 20f, Theme.Muted, TextAlignmentOptions.Center, false), 30f);
            float total = (string.IsNullOrEmpty(heading) ? 0f : 56f) + chartHeight + 44f + 30f + 18f;
            UiKit.Size(block, total);
            return view;
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }
    }
}
