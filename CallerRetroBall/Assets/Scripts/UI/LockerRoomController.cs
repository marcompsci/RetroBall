using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// LockerRoomScene. PHASE 1: Rook's player card with archetype and all eight
    /// attributes read from content data. Phase 6 adds nickname editing, cosmetics,
    /// upgrades, and career stats from the save file.
    /// </summary>
    public sealed class LockerRoomController : ScreenBase
    {
        protected override string ScreenTitle => "LOCKER ROOM";
        protected override string BackdropCourtId => "court.pier_nine";

        protected override void Build()
        {
            var c = App.Catalog;
            var rook = c.Player(DefaultContent.RookPlayerId);
            if (rook == null)
            {
                UiKit.Stretch(UiKit.Label(Body, "Rook's profile is missing from content.", 40f, Theme.Pink).rectTransform);
                return;
            }
            var archetype = c.ArchetypeById(rook.archetypeId);

            var card = UiKit.Panel(Body, Color.white, Theme.PanelSprite(), true, "PlayerCard");
            UiKit.Band(card.rectTransform, 0.04f, 0.97f, 48f);

            var column = UiKit.Column(card.transform, 18f, new RectOffset(48, 48, 40, 40));
            UiKit.Stretch(column);

            UiKit.Size(UiKit.ShadowLabel(column, rook.DisplayName.ToUpperInvariant() + "  #" + rook.jerseyNumber,
                                         84f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 120f);
            UiKit.Size(UiKit.Label(column, (archetype != null ? archetype.displayName.ToUpperInvariant() : "ROOKIE") +
                                           "  ·  OVR " + rook.attributes.Overall, 44f, Theme.Gold, TextAlignmentOptions.Center, true), 70f);
            if (archetype != null)
                UiKit.Size(UiKit.Label(column, archetype.description, 32f, Theme.Muted), 100f);

            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var type = (AttributeType)i;
                AttributeBar(column, type.ToString().ToUpperInvariant(), rook.attributes.Get(type),
                             archetype != null && archetype.strengths.Contains(type));
            }

            UiKit.Size(UiKit.Label(column, "Nickname, cosmetics, and upgrades unlock in Phase 6.", 30f, Theme.Cyan), 70f);
        }

        private static void AttributeBar(Transform parent, string label, int rating, bool strength)
        {
            var row = UiKit.NewRect("Attr " + label, parent);
            UiKit.Size(row, 76f);

            var name = UiKit.Label(row, label, 34f, strength ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0.38f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            var track = UiKit.Panel(row, Theme.InkLight, name: "Track");
            track.rectTransform.anchorMin = new Vector2(0.38f, 0.3f);
            track.rectTransform.anchorMax = new Vector2(0.86f, 0.7f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

            var fill = UiKit.Panel(track.transform, strength ? Theme.Gold : Theme.Cyan, name: "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(RatingScale.Normalized(rating), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var value = UiKit.Label(row, rating.ToString(), 36f, Theme.Cream, TextAlignmentOptions.Right, true);
            value.rectTransform.anchorMin = new Vector2(0.86f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 1f);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
