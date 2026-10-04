using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>The Park (street challenges) and the Tournament Builder, opened from PLAY.</summary>
    public sealed partial class MainMenuController
    {
        // ------------------------------------------------------------------ the park

        private void ShowPark()
        {
            var c = App.Catalog;
            var career = App.Career;
            var s = career.street;
            int level = Street.RepLevel(s.rep);
            var column = OpenOverlay("THE PARK", out var footer);
            UiKit.Size(UiKit.Label(column, "Street rules: first to 15 by 1s and 2s, win by two, make it take it. Cut hard near a defender and you might break their ankles.",
                                   28f, Theme.Cream), 110f);
            string next = level + 1 < Street.RepNames.Length ? "  ·  " + (Street.RepNeeded[level + 1] - s.rep) + " to " + Street.RepNames[level + 1] : "";
            UiControls.Stat(column, "REP", s.rep + "  " + Street.RepNames[level] + next);
            UiControls.Stat(column, "RECORD", s.wins + "-" + s.losses + "  ·  ANKLES " + s.ankleBreakers);
            if (Street.KingOfThePark(s))
                UiKit.Size(UiKit.Label(column, "KING OF THE PARK: every legend beaten.", 32f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);

            foreach (var ch in Street.Challengers)
            {
                var caller = ch;
                bool open = Street.Unlocked(s, ch);
                bool beat = s.beaten.Contains(ch.Id);
                var row = UiKit.Row(column, 10f, "Caller");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 120f);
                var label = UiKit.Label(row, open
                    ? "<b>" + ch.Nickname + "</b>  <size=22>" + ch.Format + (beat ? "  ·  <color=#4CC9F0>BEATEN</color>" : "") + "</size>\n<size=22><color=#8D99AE>\""
                      + ch.Trash + "\"  ·  " + (c.Court(ch.CourtId)?.displayName ?? "") + "</color></size>"
                    : "<b>???</b>\n<size=22><color=#8D99AE>" + Loc.T("Reach") + " " + Street.RepNames[ch.Tier] + " " + Loc.T("rep to find them") + "</color></size>",
                    28f, open ? Theme.Cream : Theme.Muted, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.Normal;
                UiKit.Size(label).flexibleWidth = 1f;
                var b = UiKit.Button(row, open ? "CALL OUT" : "LOCKED", () =>
                {
                    App.PendingMatch = Street.Challenge(App.Catalog, App.Career, caller);
                    SceneFlow.GoTo(SceneNames.Game);
                }, open ? ButtonStyle.Primary : ButtonStyle.Ghost, 110f, 24f);
                UiKit.Size(b, 110f, 190f);
                b.interactable = open;
            }
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
        }

        // ------------------------------------------------------------------ tournament builder

        private static string _tbName = "Summer Jam";
        private static int _tbSize = 8;
        private static int _tbFormat = 3;
        private static readonly List<string> _tbTeams = new List<string>();
        private static string _tbYours;

        private void ShowTournamentBuilder()
        {
            var c = App.Catalog;
            var career = App.Career;
            var cup = career.customCup;
            if (cup.field.Count > 0)
            {
                ShowCustomBracket();
                return;
            }
            var pool = CustomCup.Pool(c, career);
            _tbTeams.RemoveAll(id => !pool.Exists(t => t.id == id));
            if (_tbYours == null || !pool.Exists(t => t.id == _tbYours)) _tbYours = pool.Count > 0 ? pool[0].id : null;
            if (_tbYours != null && !_tbTeams.Contains(_tbYours)) _tbTeams.Insert(0, _tbYours);

            var column = OpenOverlay("TOURNAMENT BUILDER", out var footer);
            UiKit.Size(UiKit.Label(column, "NAME", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(column, _tbName, 20, v => _tbName = CustomTeams.Clean(v, 20, "My Tournament"));
            var sizeNames = System.Array.ConvertAll(CustomCup.Sizes, n => n + " TEAMS");
            UiControls.ChoiceRow(column, "SIZE", sizeNames, System.Array.IndexOf(CustomCup.Sizes, _tbSize), i => { _tbSize = CustomCup.Sizes[i]; ShowTournamentBuilder(); });
            var formatNames = System.Array.ConvertAll(CustomCup.Formats, CustomCup.FormatName);
            UiControls.ChoiceRow(column, "FORMAT", formatNames, System.Array.IndexOf(CustomCup.Formats, _tbFormat), i => _tbFormat = CustomCup.Formats[i]);
            UiKit.Button(column, "RANDOM FIELD", () =>
            {
                var rng = new SeededRandom((uint)System.Environment.TickCount | 1u);
                var others = pool.FindAll(t => t.id != _tbYours);
                for (int i = others.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); (others[i], others[j]) = (others[j], others[i]); }
                _tbTeams.Clear();
                _tbTeams.Add(_tbYours);
                foreach (var t in others) if (_tbTeams.Count < _tbSize) _tbTeams.Add(t.id);
                ShowTournamentBuilder();
            }, ButtonStyle.Secondary, 100f, 36f);

            string why = CustomCup.CannotStart(_tbTeams, _tbYours);
            UiKit.Size(UiKit.Label(column, _tbTeams.Count + " / " + _tbSize + " " + Loc.T("teams picked") + (why != null ? "  ·  " + why : ""), 28f,
                                   why == null ? Theme.Cyan : Theme.Muted), 60f);
            foreach (var t in pool)
            {
                var team = t;
                bool inIt = _tbTeams.Contains(t.id);
                bool yours = t.id == _tbYours;
                var row = UiKit.Row(column, 10f, "Team");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 90f);
                var logo = UiKit.Picture(row, TextureFactory.TeamLogo(t), "Logo");
                UiKit.Size(logo, 80f, 80f);
                var label = UiKit.Label(row, t.FullName.ToUpperInvariant() + (yours ? "  <color=#FFD166>(YOU)</color>" : "") + "\n<size=22><color=#8D99AE>OVR "
                                        + ArcadeEngine.TeamOverall(c, t) + "</color></size>", 26f, inIt ? Theme.Cream : Theme.Muted, TextAlignmentOptions.Left);
                UiKit.Size(label).flexibleWidth = 1f;
                UiKit.Size(UiKit.Button(row, yours ? "YOURS" : "MINE", () => { _tbYours = team.id; if (!_tbTeams.Contains(team.id)) _tbTeams.Add(team.id); ShowTournamentBuilder(); },
                                        yours ? ButtonStyle.Primary : ButtonStyle.Ghost, 80f, 22f), 80f, 140f);
                UiKit.Size(UiKit.Button(row, inIt ? "IN" : "ADD", () =>
                {
                    if (inIt) { if (!yours) _tbTeams.Remove(team.id); }
                    else if (_tbTeams.Count < _tbSize) _tbTeams.Add(team.id);
                    ShowTournamentBuilder();
                }, inIt ? ButtonStyle.Secondary : ButtonStyle.Ghost, 80f, 22f), 80f, 120f);
            }
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            var start = UiKit.Button(footer, "START", () =>
            {
                if (CustomCup.Start(App.Career.customCup, App.Catalog, _tbName, new List<string>(_tbTeams), _tbYours, _tbFormat))
                {
                    App.SaveCareer();
                    ShowCustomBracket();
                }
            }, ButtonStyle.Primary, 130f);
            start.interactable = why == null;
        }

        private void ShowCustomBracket()
        {
            var c = App.Catalog;
            var cup = App.Career.customCup;
            var column = OpenOverlay(cup.name.ToUpperInvariant(), out var footer);
            UiKit.Size(UiKit.Label(column, CustomCup.FormatName(cup.format) + "  ·  " + cup.field.Count + " " + Loc.T("teams") + "  ·  " + Loc.T("titles") + " " + cup.titles,
                                   28f, Theme.Muted), 50f);
            for (int round = 1; round <= CustomCup.Rounds(cup); round++)
            {
                var games = cup.games.FindAll(g => g.round == round);
                if (games.Count == 0) continue;
                UiKit.Size(UiKit.Label(column, CustomCup.RoundName(cup, round), 30f, Theme.Muted, TextAlignmentOptions.Center, true), 44f);
                foreach (var g in games)
                {
                    bool mine = g.Involves(cup.yourTeam);
                    string home = c.Team(g.homeId)?.abbreviation ?? "?", away = c.Team(g.awayId)?.abbreviation ?? "?";
                    UiKit.Size(UiKit.Label(column, home + (g.played ? "  " + g.homeScore + " - " + g.awayScore + "  " : "  vs  ") + away,
                                           34f, mine ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Center, true), 48f);
                }
            }
            if (cup.finished)
                UiKit.Size(UiKit.Label(column, "CHAMPION: " + (c.Team(cup.championId)?.FullName ?? "?").ToUpperInvariant(), 36f, Theme.Cyan, TextAlignmentOptions.Center, true), 60f);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            var next = CustomCup.NextMatch(cup, c, App.Career.settings.difficultyId);
            if (next != null)
                UiKit.Button(footer, "PLAY", () => { App.PendingMatch = next; SceneFlow.GoTo(SceneNames.Game); }, ButtonStyle.Primary, 130f);
            UiKit.Button(column, cup.finished ? "NEW TOURNAMENT" : "ABANDON", () =>
            {
                void Clear()
                {
                    var keepTitles = App.Career.customCup.titles;
                    var keepEdition = App.Career.customCup.edition;
                    App.Career.customCup = new CustomCupSaveData { titles = keepTitles, edition = keepEdition };
                    App.SaveCareer();
                    ShowTournamentBuilder();
                }
                if (cup.finished) Clear();
                else UiControls.Dialog("ABANDON?", "This tournament ends with no champion.", ("ABANDON", ButtonStyle.Primary, Clear), ("KEEP PLAYING", ButtonStyle.Ghost, null));
            }, ButtonStyle.Ghost, 100f, 34f);
        }
    }
}
