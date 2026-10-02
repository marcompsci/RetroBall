using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Settings ▸ SECRET CODES: tap four symbols. Right codes unlock hidden content or switch a
    /// fun mode on/off; wrong ones just buzz. Hints are earned by playing (Locker Room ▸ TROPHY).
    /// </summary>
    public static class SecretCodeScreen
    {
        private static readonly RgbColor[] SymbolColors =
        {
            RgbColor.FromHex("#FFD166"), RgbColor.FromHex("#FF8C42"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#F72585"),
        };

        /// <param name="onClose">Runs when the screen closes; the bool says whether anything changed.</param>
        public static void Open(Action<bool> onClose)
        {
            var canvas = UiKit.CreateScreenCanvas("SecretCodes", 55);
            var root = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var panel = UiKit.Panel(canvas.transform, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.18f, 0.82f, 60f);
            var column = UiKit.Column(panel.transform, 24f, new RectOffset(40, 40, 40, 40));
            UiKit.Stretch(column);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            UiKit.Size(UiKit.ShadowLabel(column, "SECRET CODES", 60f, Theme.Cream, Theme.Pink, 5f).transform.parent.GetComponent<RectTransform>(), 90f);
            UiKit.Size(UiKit.Label(column, "Enter four symbols. Earn hints by playing.", 30f, Theme.Muted), 50f);

            // Four slots showing what you've entered.
            var slotsRow = UiKit.Row(column, 24f, "Slots");
            UiKit.Size(slotsRow, 150f);
            slotsRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var slots = new RawImage[Secrets.CodeLength];
            for (int i = 0; i < slots.Length; i++)
            {
                var frame = UiKit.Panel(slotsRow, new Color(0.1f, 0.1f, 0.18f, 1f), name: "Slot " + i);
                UiKit.Size(frame, 140f, 140f);
                slots[i] = UiKit.Picture(frame.transform, null, "Icon");
                UiKit.Stretch(slots[i].rectTransform, 16f);
                slots[i].enabled = false;
            }

            var status = UiKit.Label(column, "", 38f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(status, 70f);

            var entered = new List<CodeSymbol>();
            bool changed = false;

            void Refresh()
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i].enabled = i < entered.Count;
                    if (i < entered.Count) slots[i].texture = TextureFactory.CodeIcon(entered[i], SymbolColors[(int)entered[i]]);
                }
            }

            void Press(CodeSymbol symbol)
            {
                if (entered.Count >= Secrets.CodeLength) entered.Clear();
                entered.Add(symbol);
                status.text = "";
                Refresh();
                if (entered.Count < Secrets.CodeLength) return;

                var result = Secrets.Enter(App.Career.secrets, entered, out var code);
                switch (result)
                {
                    case CodeResult.Wrong:
                        status.text = Loc.T("NOTHING HAPPENED...");
                        status.color = Theme.Muted;
                        Audio.AudioManager.Play(SfxId.Error, 0.7f);
                        break;
                    case CodeResult.Toggled:
                        bool on = Secrets.IsOn(App.Career.secrets, code.Id);
                        status.text = Loc.T(code.Name) + ": " + Loc.T(on ? "ON" : "OFF");
                        status.color = Theme.Gold;
                        Audio.AudioManager.Play(SfxId.Coin, 0.9f);
                        Audio.AudioManager.Voice(code.Name + "!");
                        Haptics.Success();
                        changed = true;
                        break;
                    default:
                        status.text = Loc.T(code.Name) + " " + Loc.T("UNLOCKED!");
                        status.color = Theme.Gold;
                        Audio.AudioManager.Play(SfxId.Fanfare, 0.8f);
                        Haptics.Success();
                        changed = true;
                        break;
                }
                App.SaveCareer();
                entered.Clear();
                Refresh();
            }

            var pad = UiKit.Row(column, 20f, "Symbols");
            UiKit.Size(pad, 170f);
            pad.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (CodeSymbol symbol in Enum.GetValues(typeof(CodeSymbol)))
            {
                var s = symbol;
                var b = UiKit.Button(pad, "", () => Press(s), ButtonStyle.Secondary, 160f);
                UiKit.Size(b.GetComponent<Image>(), 160f, 170f);
                var icon = UiKit.Picture(b.transform, TextureFactory.CodeIcon(s, SymbolColors[(int)s]), "Icon");
                UiKit.Place(icon.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(100f, 100f));
            }

            var buttons = UiKit.Row(column, 24f, "Buttons");
            UiKit.Size(buttons, 120f);
            UiKit.Button(buttons, "CLEAR", () =>
            {
                entered.Clear();
                status.text = "";
                Refresh();
            }, ButtonStyle.Ghost, 110f, 40f);
            UiKit.Button(buttons, "CLOSE", () =>
            {
                UnityEngine.Object.Destroy(root);
                onClose?.Invoke(changed);
            }, ButtonStyle.Primary, 110f, 40f);
        }
    }
}
