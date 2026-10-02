using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// LockerRoomScene: four tabs over the local career.
    /// PLAYER: nickname and trained ratings. TRAINING: attribute upgrades (cost, level, and why a
    /// purchase is blocked). STYLE: cosmetics shop and equip by slot. CAREER: totals and practice bests.
    /// Every rule is in <see cref="Career"/>; this screen only calls it and saves.
    /// </summary>
    public sealed class LockerRoomController : ScreenBase
    {
        protected override string ScreenTitle => "LOCKER ROOM";
        protected override string BackdropCourtId => "court.pier_nine";

        private enum Tab { Player = 0, Training = 1, Style = 2, Career = 3 }

        private static readonly string[] TabNames = { "PLAYER", "TRAINING", "STYLE", "CAREER" };

        private Tab _tab;
        private RectTransform _content;
        private TextMeshProUGUI _wallet;
        private UnityEngine.UI.Image[] _tabImages;

        protected override void Build()
        {
            _wallet = UiKit.Label(Body, "", 36f, Theme.Cream, TextAlignmentOptions.Center, true, "Wallet");
            UiKit.Band(_wallet.rectTransform, 0.94f, 1f, 24f);

            var tabs = UiKit.Row(Body, 10f, "Tabs");
            UiKit.Band(tabs, 0.875f, 0.935f, 32f);
            _tabImages = new UnityEngine.UI.Image[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                var t = (Tab)i;
                var b = UiKit.Button(tabs, TabNames[i], () => Show(t), ButtonStyle.Secondary, 100f, 30f);
                _tabImages[i] = b.GetComponent<UnityEngine.UI.Image>();
            }

            var holder = UiKit.NewRect("TabBody", Body);
            UiKit.Band(holder, 0f, 0.865f, 0f);
            _content = UiKit.ScrollColumn(holder, 16f, new RectOffset(48, 48, 12, 48));
            Show(Tab.Player);
        }

        private void Show(Tab tab)
        {
            _tab = tab;
            for (int i = 0; i < _tabImages.Length; i++)
                _tabImages[i].color = i == (int)tab ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            Refresh();
        }

        private void Refresh()
        {
            var career = App.Career;
            _wallet.text = "<color=#FFD166>" + career.signalPoints + " SP</color>    <color=#4CC9F0>" + career.fans + " FANS</color>";
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            switch (_tab)
            {
                case Tab.Player: BuildPlayer(); break;
                case Tab.Training: BuildTraining(); break;
                case Tab.Style: BuildStyle(); break;
                default: BuildCareer(); break;
            }
        }

        // ------------------------------------------------------------------ player

        private void BuildPlayer()
        {
            var c = App.Catalog;
            var career = App.Career;
            var rook = c.Player(DefaultContent.RookPlayerId);
            if (rook == null)
            {
                UiKit.Size(UiKit.Label(_content, "Rook's profile is missing from content.", 40f, Theme.Pink), 100f);
                return;
            }
            var archetype = c.ArchetypeById(rook.archetypeId);
            var ratings = Career.EffectiveRatings(rook.attributes, career, c);

            UiKit.Size(UiKit.Label(_content, "NICKNAME", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(_content, career.nickname, 14, value =>
            {
                career.nickname = Career.CleanNickname(value);
                App.SaveCareer();
            });

            UiKit.Size(UiKit.Label(_content, (archetype != null ? archetype.displayName.ToUpperInvariant() : "ROOKIE") +
                                             "  ·  #" + rook.jerseyNumber + "  ·  OVR " + ratings.Overall,
                                   44f, Theme.Gold, TextAlignmentOptions.Center, true), 70f);
            if (archetype != null) UiKit.Size(UiKit.Label(_content, archetype.description, 30f, Theme.Muted), 90f);

            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var type = (AttributeType)i;
                int trained = ratings.Get(type) - rook.attributes.Get(type);
                AttributeBar(_content, type.ToString().ToUpperInvariant() + (trained > 0 ? " <color=#4CC9F0>+" + trained + "</color>" : ""),
                             ratings.Get(type), archetype != null && archetype.strengths.Contains(type));
            }
        }

        private static void AttributeBar(Transform parent, string label, int rating, bool strength)
        {
            var row = UiKit.NewRect("Attr", parent);
            UiKit.Size(row, 72f);

            var name = UiKit.Label(row, label, 32f, strength ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            var track = UiKit.Panel(row, Theme.InkLight, name: "Track");
            track.rectTransform.anchorMin = new Vector2(0.42f, 0.3f);
            track.rectTransform.anchorMax = new Vector2(0.86f, 0.7f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

            var fill = UiKit.Panel(track.transform, strength ? Theme.Gold : Theme.Cyan, name: "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(RatingScale.Normalized(rating), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var value = UiKit.Label(row, rating.ToString(), 34f, Theme.Cream, TextAlignmentOptions.Right, true);
            value.rectTransform.anchorMin = new Vector2(0.86f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 1f);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ training

        private void BuildTraining()
        {
            var c = App.Catalog;
            var career = App.Career;
            var rook = c.Player(DefaultContent.RookPlayerId);
            if (rook == null) return;

            UiKit.Size(UiKit.Label(_content, "Spend Signal Points to train. Each upgrade needs " +
                                             "a game played since your last one.", 30f, Theme.Muted), 90f);
            foreach (var u in c.Upgrades)
            {
                int level = career.UpgradeLevel(u.id);
                var check = Career.CanBuy(career, u, rook.attributes);
                string cost = level >= u.maxLevel ? "MAX" : Career.UpgradeCost(u, level) + " SP";

                var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Upgrade " + u.id);
                UiKit.Size(card, 190f);
                var text = UiKit.Label(card.transform,
                    u.displayName.ToUpperInvariant() + "  <color=#8D99AE>LV " + level + "/" + u.maxLevel + "</color>\n" +
                    "<size=75%><color=#8D99AE>" + u.description + "</color></size>\n" +
                    "<size=75%>" + Reason(check) + "</size>",
                    34f, Theme.Cream, TextAlignmentOptions.Left, true);
                UiKit.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(28f, 10f);
                text.rectTransform.offsetMax = new Vector2(-260f, -10f);

                var upgrade = u;
                var buy = UiKit.Button(card.transform, cost, () =>
                {
                    if (Career.Buy(App.Career, upgrade, rook.attributes) == UpgradeCheck.Ok)
                    {
                        App.SaveCareer();
                        Haptics.Success();
                    }
                    Refresh();
                }, check == UpgradeCheck.Ok ? ButtonStyle.Primary : ButtonStyle.Ghost, 110f, 34f);
                var brt = (RectTransform)buy.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.sizeDelta = new Vector2(220f, 110f);
                brt.anchoredPosition = new Vector2(-20f, 0f);
                buy.interactable = check == UpgradeCheck.Ok;
            }
        }

        private static string Reason(UpgradeCheck check)
        {
            switch (check)
            {
                case UpgradeCheck.Ok: return "<color=#4CC9F0>Ready to train</color>";
                case UpgradeCheck.MaxLevel: return "<color=#FFD166>Fully trained</color>";
                case UpgradeCheck.AtAttributeCap: return "<color=#FFD166>At this upgrade's rating cap</color>";
                case UpgradeCheck.NotEnoughPoints: return "<color=#F72585>Not enough Signal Points</color>";
                case UpgradeCheck.NeedsTraining: return "<color=#F72585>Play a game first (training time)</color>";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ style

        private void BuildStyle()
        {
            var c = App.Catalog;
            var career = App.Career;
            var slots = new[] { CosmeticSlot.JerseyPalette, CosmeticSlot.Shoes, CosmeticSlot.CourtBanner, CosmeticSlot.Celebration, CosmeticSlot.DribbleMove };
            string[] slotNames = { "JERSEY PALETTE", "SHOES", "COURT BANNER", "CELEBRATION", "DRIBBLE MOVE" };
            for (int s = 0; s < slots.Length; s++)
            {
                var items = c.Cosmetics.FindAll(x => x.slot == slots[s]);
                if (items.Count == 0) continue;
                UiKit.Size(UiKit.Label(_content, slotNames[s], 36f, Theme.Gold, TextAlignmentOptions.Left, true), 56f);
                foreach (var item in items) CosmeticRow(item, career);
            }
            if (slots.Length > 0)
                UiKit.Size(UiKit.Label(_content, "Celebrations and dribble moves are collectible style tags; they don't change ratings.",
                                       28f, Theme.Muted), 80f);
        }

        private void CosmeticRow(CosmeticDef item, CareerSaveData career)
        {
            bool owned = career.ownedCosmetics.Contains(item.id);
            bool equipped = career.Equipped(item.slot) == item.id;
            var check = Career.CanBuy(career, item);

            var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Cosmetic " + item.id);
            UiKit.Size(card, 130f);

            var swatchA = UiKit.Panel(card.transform, item.colorA.ToColor(), name: "SwatchA");
            UiKit.Place(swatchA.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 70f));
            swatchA.rectTransform.anchoredPosition = new Vector2(46f, 0f);
            var swatchB = UiKit.Panel(card.transform, item.colorB.ToColor(), name: "SwatchB");
            UiKit.Place(swatchB.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 70f));
            swatchB.rectTransform.anchoredPosition = new Vector2(94f, 0f);

            string status = owned ? (equipped ? "<color=#4CC9F0>EQUIPPED</color>" : "<color=#8D99AE>OWNED</color>")
                          : check == CosmeticCheck.NeedsFans ? "<color=#F72585>Needs " + item.fansRequired + " fans</color>"
                          : "<color=#FFD166>" + item.cost + " SP</color>";
            var text = UiKit.Label(card.transform, item.displayName.ToUpperInvariant() + "\n<size=75%>" + status + "</size>",
                                   32f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(140f, 6f);
            text.rectTransform.offsetMax = new Vector2(-230f, -6f);

            if (equipped) return;
            string label = owned ? "EQUIP" : "BUY";
            bool usable = owned || check == CosmeticCheck.Ok;
            var cosmetic = item;
            var button = UiKit.Button(card.transform, label, () =>
            {
                var data = App.Career;
                bool changed = data.ownedCosmetics.Contains(cosmetic.id)
                    ? Career.Equip(data, cosmetic)
                    : Career.Buy(data, cosmetic) == CosmeticCheck.Ok;
                if (changed) App.SaveCareer();
                Refresh();
            }, usable ? ButtonStyle.Primary : ButtonStyle.Ghost, 90f, 32f);
            button.interactable = usable;
            var brt = (RectTransform)button.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(200f, 90f);
            brt.anchoredPosition = new Vector2(-18f, 0f);
        }

        // ------------------------------------------------------------------ career

        private void BuildCareer()
        {
            var career = App.Career;
            var t = career.totals;
            string fg = t.fieldGoalsAttempted == 0 ? "-" : Mathf.RoundToInt(100f * t.fieldGoalsMade / t.fieldGoalsAttempted) + "%";
            Line("GAMES", t.games + "  (" + t.wins + "-" + t.losses + ")");
            Line("POINTS", t.points.ToString());
            Line("ASSISTS", t.assists.ToString());
            Line("REBOUNDS", t.rebounds.ToString());
            Line("STEALS", t.steals.ToString());
            Line("BLOCKS", t.blocks.ToString());
            Line("FIELD GOALS", t.fieldGoalsMade + "/" + t.fieldGoalsAttempted + "  " + fg);
            Line("GREEN RELEASES", t.greens.ToString());
            Line("CHAMPIONSHIPS", t.championships.ToString());

            UiKit.Size(UiKit.Label(_content, "PRACTICE BESTS", 36f, Theme.Gold, TextAlignmentOptions.Left, true), 60f);
            var p = career.practice;
            Line("FREE SHOOT", p.freeShootMakes + " makes · streak " + p.freeShootStreak);
            Line("PASSING TARGETS", p.passingScore.ToString());
            Line("DRIBBLE LANE", p.dribbleLaneTime > 0f ? p.dribbleLaneTime.ToString("0.00") + " s" : "-");
        }

        private void Line(string label, string value)
        {
            UiKit.Size(UiKit.Label(_content, "<color=#8D99AE>" + label + "</color><pos=55%>" + value, 34f, Theme.Cream,
                                   TextAlignmentOptions.Left, true), 52f);
        }
    }
}
