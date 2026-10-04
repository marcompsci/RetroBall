using System;
using System.IO;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Sharing from the Kit Studio, all offline: a kit as a short code and a QR (the iPhone camera opens
    /// Retro Hoops straight into the kit), entering a friend's code, and a trading card image of your player.
    /// </summary>
    public static class KitShare
    {
        /// <summary>Code + QR for a kit, with COPY and SHARE.</summary>
        public static void ShowCode(KitData kit)
        {
            string code = Kits.Encode(kit.Clone());
            string link = Kits.LinkPrefix + code;
            var qr = QrCode.EncodeText(link).ToCanvas(4, new RgbColor(0x14, 0x14, 0x20), RgbColor.White);
            var qrTex = TextureFactory.ToTexture(qr, "ui.kit.qr");

            var (root, column) = Modal("SHARE YOUR KIT");
            var pic = UiKit.Picture(column, qrTex, "QR");
            UiKit.Size(pic, 520f, 520f);
            pic.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            UiKit.Size(UiKit.Label(column, "Scan with an iPhone camera to open it in Retro Hoops, or send the code.", 28f, Theme.Muted), 70f);
            UiKit.Size(UiKit.Label(column, Kits.Pretty(code), 34f, Theme.Gold, TextAlignmentOptions.Center, true), 100f);
            UiKit.Button(column, "COPY CODE", () =>
            {
                GUIUtility.systemCopyBuffer = code;
                Audio.AudioManager.Click();
                Toast(column, "Copied.");
            }, ButtonStyle.Secondary, 100f, 38f);
            UiKit.Button(column, "SHARE", () =>
            {
                string path = SavePng(qr, "retrohoops-kit-qr.png", 2);
                if (path != null) Share.File(path, "My Retro Hoops kit: " + link);
            }, ButtonStyle.Secondary, 100f, 38f);
            UiKit.Button(column, "CLOSE", () => Close(root, qrTex), ButtonStyle.Ghost, 100f, 38f);
        }

        /// <summary>Type or paste a friend's code; <paramref name="onKit"/> gets the kit when it reads.</summary>
        public static void EnterCode(Action<KitData> onKit)
        {
            var (root, column) = Modal("ENTER A KIT CODE");
            UiKit.Size(UiKit.Label(column, "Paste a code a friend sent you (or a retrohoops://kit/ link).", 28f, Theme.Muted), 70f);
            string text = "";
            TMP_InputField field = null;
            field = UiControls.TextField(column, "", 80, v => text = v);
            var status = UiKit.Label(column, "", 28f, Theme.Pink, TextAlignmentOptions.Center, true);
            UiKit.Size(status, 50f);
            UiKit.Button(column, "PASTE", () =>
            {
                text = GUIUtility.systemCopyBuffer ?? "";
                field.text = text;
            }, ButtonStyle.Secondary, 100f, 38f);
            UiKit.Button(column, "LOAD KIT", () =>
            {
                if (string.IsNullOrEmpty(text)) text = field.text;
                if (Kits.TryDecode(text, out var kit))
                {
                    Close(root, null);
                    onKit?.Invoke(kit);
                }
                else status.text = Loc.T("That code doesn't read. Check it and try again.");
            }, ButtonStyle.Primary, 110f, 42f);
            UiKit.Button(column, "CANCEL", () => Close(root, null), ButtonStyle.Ghost, 100f, 38f);
        }

        /// <summary>Your player's trading card in <paramref name="kit"/>, with SHARE (Photos, Messages, AirDrop...).</summary>
        public static void ShowCard(KitData kit)
        {
            var career = App.Career;
            var c = App.Catalog;
            var me = PlayerCreator.ForMatch(career, c);
            var yours = c.Team(CustomTeams.TeamId) ?? c.Team(DefaultContent.PlayerCrewId);
            var arch = c.ArchetypeById(me.archetypeId);
            var t = career.totals;
            var info = new CardInfo
            {
                Look = me.appearance,
                Kit = Kits.Look(kit),
                Name = career.nickname,
                Number = me.jerseyNumber,
                Team = yours?.FullName ?? "",
                Role = arch?.displayName ?? "",
                Overall = me.attributes.Overall,
                Stats = new[] { ("WINS", t.wins.ToString()), ("PTS", t.points.ToString()), ("GREEN", t.greens.ToString()) },
                Logo = yours != null ? LogoGenerator.Generate(yours) : null,
            };
            var card = TradingCard.Generate(info);
            var tex = TextureFactory.ToTexture(card, "ui.kit.card");

            var (root, column) = Modal("TRADING CARD");
            var pic = UiKit.Picture(column, tex, "Card");
            UiKit.Size(pic, 820f, 820f * TradingCard.Width / TradingCard.Height);
            UiKit.Button(column, "SHARE CARD", () =>
            {
                string path = SavePng(card, "retrohoops-card.png", 8);
                if (path != null) Share.File(path, "My Retro Hoops card");
            }, ButtonStyle.Primary, 110f, 42f);
            UiKit.Button(column, "CLOSE", () => Close(root, tex), ButtonStyle.Ghost, 100f, 38f);
        }

        // ------------------------------------------------------------------ helpers

        private static (GameObject root, RectTransform column) Modal(string title)
        {
            var canvas = UiKit.CreateScreenCanvas("KitShareCanvas", 55);
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var panel = UiKit.Panel(canvas.transform, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.05f, 0.95f, 48f);
            panel.gameObject.AddComponent<OverlayPop>();
            var column = UiKit.Column(panel.transform, 16f, new RectOffset(40, 40, 36, 36));
            UiKit.Stretch(column);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(UiKit.Label(column, title, 50f, Theme.Gold, TextAlignmentOptions.Center, true), 72f);
            return (canvas.gameObject, column);
        }

        private static void Close(GameObject root, Texture2D owned)
        {
            if (owned != null) UnityEngine.Object.Destroy(owned);
            UnityEngine.Object.Destroy(root);
        }

        private static void Toast(Transform column, string text)
        {
            var label = UiKit.Label(column, text, 28f, Theme.Cyan, TextAlignmentOptions.Center, true);
            UiKit.Size(label, 40f);
            UnityEngine.Object.Destroy(label.gameObject, 1.5f);
        }

        /// <summary>Writes a canvas (scaled up for crisp pixels) as a PNG in the cache folder; returns its path.</summary>
        public static string SavePng(PixelCanvas canvas, string fileName, int scale)
        {
            var big = scale > 1 ? TradingCard.Scale(canvas, scale) : canvas;
            var tex = new Texture2D(big.Width, big.Height, TextureFormat.RGBA32, false);
            var px = new Color32[big.Pixels.Length];
            for (int i = 0; i < px.Length; i++)
            {
                var p = big.Pixels[i];
                px[i] = new Color32(p.r, p.g, p.b, p.a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            try
            {
                string path = Path.Combine(Application.temporaryCachePath, fileName);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Retro Hoops] Couldn't save " + fileName + ": " + e.Message);
                return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
