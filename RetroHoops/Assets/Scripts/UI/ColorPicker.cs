using System;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Kit Studio colour picker: the 64-colour pixel palette plus red / green / blue sliders (32 steps each,
    /// the same grid kits are saved on). Every change previews live; DONE keeps it, CANCEL puts it back.
    /// </summary>
    public static class ColorPicker
    {
        public static void Open(string title, RgbColor current, Action<RgbColor> preview, Action<RgbColor> done)
        {
            var original = Kits.Snap(current);
            var color = original;
            var canvas = UiKit.CreateScreenCanvas("ColorPickerCanvas", 55);
            var root = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var panel = UiKit.Panel(canvas.transform, Color.white, Theme.PanelSprite(), true, "Picker");
            UiKit.Band(panel.rectTransform, 0.06f, 0.94f, 40f);
            panel.gameObject.AddComponent<OverlayPop>();
            var column = UiKit.Column(panel.transform, 14f, new RectOffset(36, 36, 30, 30));
            UiKit.Stretch(column);

            UiKit.Size(UiKit.Label(column, title, 46f, Theme.Gold, TextAlignmentOptions.Center, true), 64f);
            var swatch = UiKit.Panel(column, ToColor(color), name: "Current");
            UiKit.Size(swatch, 90f);
            var hex = UiKit.Label(swatch.transform, "", 32f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Stretch(hex.rectTransform);

            // 8 × 8 palette.
            var grid = UiKit.NewRect("Palette", column);
            UiKit.Size(grid, 560f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(112f, 64f);
            layout.spacing = new Vector2(8f, 6f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 8;
            Slider r = null, g = null, b = null;
            bool syncing = false;
            foreach (var pc in Kits.Palette64)
            {
                var chip = UiKit.Panel(grid, ToColor(pc), name: "Swatch");
                chip.raycastTarget = true;
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = chip;
                var pick = pc;
                button.onClick.AddListener(() =>
                {
                    Audio.AudioManager.Click();
                    Set(pick, true);
                });
            }

            r = UiControls.SliderRow(column, "RED", color.r >> 3, v => { if (!syncing) Set(FromSliders(), false); }, 0f, 31f);
            g = UiControls.SliderRow(column, "GREEN", color.g >> 3, v => { if (!syncing) Set(FromSliders(), false); }, 0f, 31f);
            b = UiControls.SliderRow(column, "BLUE", color.b >> 3, v => { if (!syncing) Set(FromSliders(), false); }, 0f, 31f);
            foreach (var s in new[] { r, g, b }) s.wholeNumbers = true;

            var footer = UiKit.Row(column, 20f, "Footer");
            UiKit.Size(footer, 120f);
            UiKit.Button(footer, "CANCEL", () =>
            {
                preview?.Invoke(original);
                UnityEngine.Object.Destroy(root);
            }, ButtonStyle.Ghost, 110f, 40f);
            UiKit.Button(footer, "DONE", () =>
            {
                done?.Invoke(color);
                UnityEngine.Object.Destroy(root);
            }, ButtonStyle.Primary, 110f, 44f);
            Set(color, true);

            RgbColor FromSliders() =>
                Kits.Unpack(((int)r.value << 10) | ((int)g.value << 5) | (int)b.value);

            void Set(RgbColor c, bool moveSliders)
            {
                color = Kits.Snap(c);
                swatch.color = ToColor(color);
                hex.text = color.ToHex();
                hex.color = color.Luminance > 0.5 ? Theme.Ink : Theme.Cream;
                if (moveSliders && r != null)
                {
                    syncing = true;
                    r.value = color.r >> 3;
                    g.value = color.g >> 3;
                    b.value = color.b >> 3;
                    syncing = false;
                }
                preview?.Invoke(color);
            }
        }

        public static Color ToColor(RgbColor c) => new Color32(c.r, c.g, c.b, 255);
    }
}
