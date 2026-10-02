using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// SeasonScene (Rise Mode hub). PHASE 1: shows the Rise Mode path and a Caller
    /// League preview from content data. Phase 5 adds the playable season, standings,
    /// bracket, save state, and event cards.
    /// </summary>
    public sealed class SeasonScreenController : ScreenBase
    {
        protected override string ScreenTitle => "RISE MODE";
        protected override string BackdropCourtId => "court.overpass_park";

        protected override void Build()
        {
            var c = App.Catalog;
            var season = c.Seasons.Count > 0 ? c.Seasons[0] : new SeasonConfigDef();

            string path =
                "<color=#FFD166>" + DefaultContent.CircuitName.ToUpperInvariant() + "</color>\n" +
                "Win the three street courts\n" +
                "<color=#FFD166>" + DefaultContent.LeagueName.ToUpperInvariant() + "</color>\n" +
                season.regularSeasonGames + "-game season · top " + season.playoffTeams + " make the playoffs\n" +
                "<color=#F72585>" + season.championshipName.ToUpperInvariant() + "</color>";
            var intro = UiKit.Label(Body, path, 38f, Theme.Cream);
            UiKit.Band(intro.rectTransform, 0.72f, 0.99f, 48f);

            var list = UiKit.Column(Body, 14f, null, "LeagueList");
            UiKit.Band(list, 0.03f, 0.70f, 48f);

            foreach (var id in season.teamIds)
            {
                var team = c.Team(id);
                if (team != null) TeamRow(list, team);
            }
        }

        private static void TeamRow(Transform parent, TeamDef team)
        {
            var row = UiKit.Panel(parent, Color.white, Theme.PanelSprite(), true, "Row " + team.abbreviation);
            UiKit.Size(row, 112f);

            var logo = UiKit.Picture(row.transform, TextureFactory.TeamLogo(team));
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            logo.rectTransform.pivot = new Vector2(0f, 0.5f);
            logo.rectTransform.sizeDelta = new Vector2(88f, 88f);
            logo.rectTransform.anchoredPosition = new Vector2(16f, 0f);

            var name = UiKit.Label(row.transform, team.FullName.ToUpperInvariant() + "\n<size=70%><color=#8D99AE>" + team.motto + "</color></size>",
                                   36f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Stretch(name.rectTransform);
            name.rectTransform.offsetMin = new Vector2(124f, 6f);
            name.rectTransform.offsetMax = new Vector2(-140f, -6f);

            var tag = UiKit.Label(row.transform, team.abbreviation, 34f, Theme.Gold, TextAlignmentOptions.Right, true);
            UiKit.Stretch(tag.rectTransform);
            tag.rectTransform.offsetMax = new Vector2(-24f, 0f);
        }
    }
}
