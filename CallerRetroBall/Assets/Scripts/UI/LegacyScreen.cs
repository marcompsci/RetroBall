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
    /// LEGACY career, drawn over the main menu: HOME (the next game, college offers, draft night,
    /// contracts, retirement), SKILLS (the skill tree and trainer), SPONSORS and HISTORY. Rules in <see cref="Legacy"/>.
    /// </summary>
    public sealed class LegacyScreen : MonoBehaviour
    {
        private enum Tab { Home = 0, Skills = 1, Sponsors = 2, History = 3 }
        private static readonly string[] TabNames = { "HOME", "SKILLS", "SPONSORS", "HISTORY" };

        private Tab _tab;
        private RectTransform _content;
        private TextMeshProUGUI _header;
        private Image[] _tabImages;
        private GameObject _tabsRow;
        private GameObject _eventDialog;

        private static LegacySaveData L => App.Career.legacy;
        private static ContentCatalog C => App.Catalog;

        public static LegacyScreen Open()
        {
            var existing = FindAnyObjectByType<LegacyScreen>();
            if (existing != null) return existing;
            var go = new GameObject("LegacyScreen");
            var screen = go.AddComponent<LegacyScreen>();
            screen.Build();
            return screen;
        }

        private void Build()
        {
            var canvas = UiKit.CreateScreenCanvas("LegacyCanvas", 35);
            canvas.transform.SetParent(transform, false);
            var bg = UiKit.Panel(canvas.transform, Theme.Ink, name: "Backdrop");
            UiKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);

            var top = UiKit.Row(safe, 16f, "Top");
            UiKit.Band(top, 0.93f, 0.995f, 24f);
            top.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(UiKit.Button(top, "< BACK", Close, ButtonStyle.Ghost, 90f, 32f), 90f, 200f);
            _header = UiKit.Label(top, "", 30f, Theme.Cream, TextAlignmentOptions.Right, true);
            UiKit.Size(_header, 90f).flexibleWidth = 1f;

            var tabs = UiKit.Row(safe, 8f, "Tabs");
            UiKit.Band(tabs, 0.875f, 0.925f, 16f);
            _tabsRow = tabs.gameObject;
            _tabImages = new Image[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                var t = (Tab)i;
                _tabImages[i] = UiKit.Button(tabs, TabNames[i], () => { _tab = t; Refresh(); }, ButtonStyle.Secondary, 80f, 22f).GetComponent<Image>();
            }
            var holder = UiKit.NewRect("TabBody", safe);
            UiKit.Band(holder, 0f, 0.865f, 0f);
            _content = UiKit.ScrollColumn(holder, 14f, new RectOffset(40, 40, 12, 60));
            if (L.active) Legacy.Register(C, L, App.Career);
            Refresh();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (_eventDialog == null && ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))) Close();
#endif
        }

        private void Close() => Destroy(gameObject);

        private void Save()
        {
            App.SaveCareer();
            App.ReportGameCenter();
            if (L.active) Legacy.Register(C, L, App.Career);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            var s = L;
            _tabsRow.SetActive(s.active);
            for (int i = 0; i < _tabImages.Length; i++) _tabImages[i].color = i == (int)_tab ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            if (!s.active)
            {
                _header.text = "LEGACY";
                BuildStart();
                return;
            }
            var you = C.Player(Legacy.YouId);
            _header.text = Loc.T(Legacy.StageName(s)) + "\n<size=24><color=#8D99AE>" + Loc.T("AGE") + " " + s.age + "  ·  OVR " + (you?.attributes.Overall ?? 0)
                           + "  ·  LV " + Legacy.Level(s) + "  ·  " + s.fans + " " + Loc.T("FANS") + "</color></size>";
            switch (_tab)
            {
                case Tab.Skills: BuildSkills(); break;
                case Tab.Sponsors: BuildSponsors(); break;
                case Tab.History: BuildHistory(); break;
                default: BuildHome(); break;
            }
            ShowEvent();
        }

        // ------------------------------------------------------------------ helpers

        private TextMeshProUGUI Line(string text, Color color, float size = 28f, float height = 44f, TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var l = UiKit.Label(_content, text, size, color, align, bold);
            l.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(l, height);
            return l;
        }

        private void Heading(string text) => Line(text, Theme.Gold, 34f, 56f, TextAlignmentOptions.Left, true);

        private void Big(string text, System.Action onClick, ButtonStyle style = ButtonStyle.Primary) =>
            UiKit.Button(_content, text, () => onClick(), style, 110f, 40f);

        private void Row(string text, Color color, float height, string button, ButtonStyle style, System.Action onClick)
        {
            var row = UiKit.Row(_content, 10f, "Row");
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(row, height);
            var label = UiKit.Label(row, text, 26f, color, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(label).flexibleWidth = 1f;
            if (button == null) return;
            var b = UiKit.Button(row, button, () => onClick?.Invoke(), style, height - 10f, 24f);
            UiKit.Size(b, height - 10f, 170f);
            if (onClick == null) b.interactable = false;
        }

        private static string TeamName(string id) => C.Team(id)?.FullName ?? id;

        // ------------------------------------------------------------------ start

        private void BuildStart()
        {
            var me = PlayerCreator.BasePlayer(App.Career, C);
            Line("Your player's whole career: a senior year of high school, college (or straight to the draft), draft night, and pro seasons until you retire. " +
                 "Grades and XP, a skill tree, contracts and sponsors, story choices, awards and a Hall of Fame vote. Every game is Full Court with you starting.",
                 Theme.Cream, 30f, 240f);
            Line(Loc.T("You play as") + " <b>" + App.Career.nickname.ToUpperInvariant() + "</b>  ·  " + (C.ArchetypeById(me.archetypeId)?.displayName ?? "")
                 + "\n<size=22><color=#8D99AE>" + Loc.T("Change your look and position in Locker Room ▸ CREATE first if you like.") + "</color></size>", Theme.Gold, 32f, 110f);
            Big("START LEGACY", () =>
            {
                App.Career.legacy = Legacy.Start(C, (uint)System.Environment.TickCount | 1u);
                _tab = Tab.Home;
                Save();
            });
        }

        // ------------------------------------------------------------------ home

        private void BuildHome()
        {
            var s = L;
            switch (s.stage)
            {
                case LegacyStage.College when s.season == null:
                    Heading("RECRUITING");
                    Line(new string('*', s.stars) + "  " + s.stars + Loc.T("-STAR RECRUIT"), Theme.Gold, 36f, 60f);
                    for (int i = 0; i < s.offers.Count; i++)
                    {
                        int pick = i;
                        var o = s.offers[i];
                        Row(o.value < 0 ? "<b>" + Loc.T("SKIP COLLEGE") + "</b>\n<size=22><color=#8D99AE>" + o.pitch + "</color></size>"
                                        : "<b>" + o.name.ToUpperInvariant() + "</b>  " + new string('*', o.value + 1) + "\n<size=22><color=#8D99AE>" + o.pitch + "</color></size>",
                            Theme.Cream, 120f, o.value < 0 ? "DECLARE" : "COMMIT", o.value < 0 ? ButtonStyle.Ghost : ButtonStyle.Primary, () =>
                            {
                                Legacy.ChooseCollege(s, C, pick);
                                Save();
                            });
                    }
                    break;
                case LegacyStage.Draft:
                    Heading("DRAFT NIGHT");
                    int projected = Legacy.ProjectedPick(s);
                    Line(Loc.T("DRAFT STOCK") + " " + s.stock + " / 100  ·  " + (projected > 0 ? Loc.T("projected pick") + " " + projected : Loc.T("projected: undrafted")), Theme.Cream, 30f, 60f);
                    Big("HEAR YOUR NAME", () =>
                    {
                        var team = Legacy.Draft(s, C);
                        Save();
                        if (team != null)
                            UiControls.Dialog(s.draftPick > 0 ? "PICK " + s.draftPick : "UNDRAFTED, NOT UNWANTED",
                                (s.draftPick > 0 ? "With pick " + s.draftPick + ", the " + team.FullName + " select " : "The " + team.FullName + " sign ") + App.Career.nickname.ToUpperInvariant()
                                + ". Rookie deal: " + Legacy.Money(s.salary * 1000L) + " a season for 3 seasons.", ("LET'S GO", ButtonStyle.Primary, null));
                    });
                    break;
                case LegacyStage.Pro when s.season == null:
                    Heading("OFF-SEASON");
                    if (s.history.Count > 0) LastSeason(s.history[s.history.Count - 1]);
                    if (s.offers.Count > 0)
                    {
                        Heading("YOUR AGENT: OFFERS");
                        for (int i = 0; i < s.offers.Count; i++)
                        {
                            int pick = i;
                            var o = s.offers[i];
                            Row("<b>" + o.name.ToUpperInvariant() + "</b>  " + Legacy.Money(o.value * 1000L) + " × " + o.years + "\n<size=22><color=#8D99AE>" + o.pitch + "</color></size>",
                                Theme.Cream, 120f, "SIGN", ButtonStyle.Primary, () => { Legacy.NextSeason(s, C, pick); Save(); });
                        }
                    }
                    else Big("START SEASON " + (s.proSeason + 1), () => { Legacy.NextSeason(s, C); Save(); });
                    if (Legacy.CanRetire(s))
                        Big("RETIRE", () => UiControls.Dialog("HANG THEM UP?", "Your career ends here and the Hall of Fame votes.",
                            ("RETIRE", ButtonStyle.Primary, () => { Legacy.Retire(s); Save(); }), ("NOT YET", ButtonStyle.Ghost, null)), ButtonStyle.Ghost);
                    break;
                case LegacyStage.Retired:
                    Heading(s.hallOfFame ? "HALL OF FAME" : "RETIRED");
                    var t = Legacy.Totals(s);
                    Line(s.hallOfFame ? "First ballot. Your number goes up in the rafters." : "A career to be proud of. (" + Legacy.HallOfFameLegacy + " legacy points get you in the Hall.)",
                         Theme.Cream, 30f, 90f);
                    UiControls.Stat(_content, "LEGACY", s.legacyPoints.ToString());
                    UiControls.Stat(_content, "PRO SEASONS", t.seasons.ToString());
                    UiControls.Stat(_content, "TITLES", t.titles.ToString());
                    UiControls.Stat(_content, "MVPs", t.mvps.ToString());
                    Big("NEW LEGACY", () => UiControls.Dialog("START A NEW LEGACY?", "This career stays in the history book until you start the next one.",
                        ("START", ButtonStyle.Primary, () => { App.Career.legacy = new LegacySaveData(); Save(); }), ("CANCEL", ButtonStyle.Ghost, null)), ButtonStyle.Secondary);
                    break;
                default:
                    BuildGameDay(s);
                    break;
            }
        }

        private void LastSeason(LegacySeason h)
        {
            Line(h.label + "  ·  " + h.team + "\n" + h.wins + "-" + (h.games - h.wins) + "  ·  " + h.Ppg.ToString("0.0") + " PPG  ·  " + Loc.T(h.result)
                 + (h.awards.Count > 0 ? "\n<color=#FFD166>" + string.Join("  ·  ", h.awards) + "</color>" : ""), Theme.Cream, 28f, h.awards.Count > 0 ? 120f : 90f);
        }

        private void BuildGameDay(LegacySaveData s)
        {
            var g = Legacy.NextGame(s);
            var rec = Legacy.Record(s);
            Line(TeamName(s.teamId).ToUpperInvariant() + "  ·  " + (rec != null ? rec.Wins + "-" + rec.Losses : "0-0")
                 + (s.stage == LegacyStage.Pro ? "  ·  " + Loc.T(Legacy.Role(s, C)) + "  ·  " + Legacy.Money(s.salary * 1000L) : ""), Theme.Muted, 26f, 44f);
            if (g != null)
            {
                bool home = g.homeId == s.teamId;
                string opp = home ? g.awayId : g.homeId;
                Heading(g.round == 2 ? "FINAL" : g.round == 1 ? "SEMIFINAL" : Loc.T("GAME") + " " + (g.week + 1));
                var row = UiKit.Row(_content, 30f, "Matchup");
                UiKit.Size(row, 200f);
                foreach (var id in new[] { s.teamId, opp })
                {
                    var t = C.Team(id);
                    if (t == null) continue;
                    var pic = UiKit.Picture(row, TextureFactory.TeamLogo(t), "Logo");
                    pic.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                }
                Line((home ? Loc.T("HOME vs") : Loc.T("AWAY at")) + " " + TeamName(opp).ToUpperInvariant(), Theme.Cream, 32f, 60f, TextAlignmentOptions.Center, true);
                Big("PLAY GAME", () =>
                {
                    var req = Legacy.NextMatch(s, C, App.Career, App.Career.settings.difficultyId);
                    if (req == null) return;
                    App.PendingMatch = req;
                    SceneFlow.GoTo(SceneNames.Game);
                });
                Big("SIM GAME (HALF XP)", () =>
                {
                    var line = Legacy.SimGame(s, C, App.Career);
                    Save();
                    if (line != null) Toast((line.won ? "W " : "L ") + line.us + "-" + line.them, line.pts + " PTS  " + line.ast + " AST  " + line.reb + " REB  ·  GRADE " + line.grade);
                }, ButtonStyle.Secondary);
            }
            if (s.games.Count > 0)
            {
                Heading("THIS SEASON");
                for (int i = s.games.Count - 1; i >= 0 && i >= s.games.Count - 6; i--)
                {
                    var l = s.games[i];
                    Line((l.won ? "<color=#4CC9F0>W</color> " : "<color=#F72585>L</color> ") + l.us + "-" + l.them + " vs " + (C.Team(l.opponent)?.abbreviation ?? "?")
                         + "   " + l.pts + " PTS  " + l.ast + " AST  " + l.reb + " REB   <color=#FFD166>" + l.grade + "</color>" + (l.simmed ? "  (SIM)" : ""), Theme.Cream, 26f, 44f);
                }
            }
            Line(Loc.T("COACH'S TRUST") + " " + s.trust + "  ·  XP " + s.xp + "  ·  " + Loc.T("SKILL POINTS") + " " + s.skillPoints, Theme.Muted, 24f, 40f);
        }

        private static void Toast(string title, string body) => UiControls.Dialog(title, body, ("OK", ButtonStyle.Primary, null));

        private void ShowEvent()
        {
            var e = Legacy.PendingEvent(L);
            if (e == null || _eventDialog != null) return;
            var buttons = new List<(string, ButtonStyle, System.Action)>();
            for (int i = 0; i < e.Choices.Length; i++)
            {
                int pick = i;
                buttons.Add((e.Choices[i].label, i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary, () =>
                {
                    _eventDialog = null;
                    Legacy.Choose(L, pick);
                    Save();
                }));
            }
            _eventDialog = UiControls.Dialog(e.Title, e.Body, buttons.ToArray());
        }

        // ------------------------------------------------------------------ skills

        private void BuildSkills()
        {
            var s = L;
            var b = PlayerCreator.BasePlayer(App.Career, C);
            var r = Legacy.Ratings(b, s);
            Line(Loc.T("SKILL POINTS") + ": " + s.skillPoints + "  ·  LV " + Legacy.Level(s) + " (" + (s.xp % Legacy.XpPerLevel) + "/" + Legacy.XpPerLevel + " XP)", Theme.Gold, 30f, 50f);
            Line("FIN " + r.finishing + "  SHO " + r.shooting + "  PLY " + r.playmaking + "  DEF " + r.defense + "\nREB " + r.rebounding + "  SPD " + r.speed + "  STA " + r.stamina + "  CLU " + r.clutch,
                 Theme.Cream, 28f, 80f);
            foreach (var branch in Legacy.Branches)
            {
                Heading(branch);
                foreach (var k in Legacy.Skills)
                {
                    if (k.Branch != branch) continue;
                    string id = k.Id;
                    bool owned = s.skills.Contains(id);
                    string why = Legacy.CannotLearn(s, id);
                    Row("<b>" + k.Name + "</b>  <size=22>" + Loc.T("TIER") + " " + k.Tier + " · " + k.Cost + " SP</size>\n<size=22><color=#8D99AE>" + k.Describe()
                        + (why != null && !owned ? "  ·  " + why : "") + "</color></size>", owned ? Theme.Gold : Theme.Cream, 100f,
                        owned ? "LEARNED" : "LEARN", owned ? ButtonStyle.Ghost : ButtonStyle.Primary,
                        owned || why != null ? (System.Action)null : () => { Legacy.Learn(s, id); Audio.AudioManager.Play(SfxId.Coin, 0.6f); Save(); });
                }
            }
            if (s.stage == LegacyStage.Pro)
            {
                Heading("PRIVATE TRAINER");
                Row(Loc.T("One session = 1 skill point.") + "  " + Legacy.Money(Legacy.TrainerCost(s)) + "\n<size=22><color=#8D99AE>" + Loc.T("Cash") + ": " + Legacy.Money(s.cash) + "</color></size>",
                    Theme.Cream, 100f, "HIRE", ButtonStyle.Secondary, s.cash >= Legacy.TrainerCost(s) ? (System.Action)(() => { Legacy.HireTrainer(s); Save(); }) : null);
            }
        }

        // ------------------------------------------------------------------ sponsors

        private void BuildSponsors()
        {
            var s = L;
            Line(Loc.T("Cash") + ": " + Legacy.Money(s.cash) + "  ·  " + s.fans + " " + Loc.T("FANS"), Theme.Gold, 30f, 50f);
            if (s.stage != LegacyStage.Pro)
            {
                Line("Sponsors can only sign pros. Get drafted first.", Theme.Cream, 28f, 60f);
                return;
            }
            Heading("YOUR SPONSORS (" + s.endorsements.Count + "/" + Legacy.MaxEndorsements + ")");
            if (s.endorsements.Count == 0) Line("None yet.", Theme.Muted);
            foreach (var id in s.endorsements)
            {
                var b = Legacy.Brand(id);
                string bid = id;
                Row("<b>" + b.Name + "</b>  " + Legacy.Money(b.Pay * 1000L) + " " + Loc.T("a season") + "\n<size=22><color=#8D99AE>" + b.Pitch + "</color></size>",
                    Theme.Cream, 110f, "DROP", ButtonStyle.Ghost, () => { Legacy.DropBrand(s, bid); Save(); });
            }
            Heading("OFFERS");
            var offers = Legacy.BrandOffers(s);
            if (offers.Count == 0) Line("More fans bring more offers. Win games and say yes to the spotlight.", Theme.Muted, 26f, 70f);
            foreach (var b in offers)
            {
                string bid = b.Id;
                Row("<b>" + b.Name + "</b>  " + Legacy.Money(b.Pay * 1000L) + " " + Loc.T("a season") + "\n<size=22><color=#8D99AE>" + b.Pitch + "</color></size>",
                    Theme.Cream, 110f, "SIGN", ButtonStyle.Primary, () => { Legacy.SignBrand(s, bid); Audio.AudioManager.Play(SfxId.Coin, 0.7f); Save(); });
            }
            Heading("LOCKED");
            foreach (var b in Legacy.Brands)
                if (s.fans < b.MinFans) Line(b.Name + "  ·  " + b.MinFans + " " + Loc.T("fans"), Theme.Muted, 24f, 40f);
            Line("All brands here are invented for RetroBall.", Theme.Muted, 22f, 40f);
        }

        // ------------------------------------------------------------------ history

        private void BuildHistory()
        {
            var s = L;
            UiControls.Stat(_content, "LEGACY POINTS", s.legacyPoints + " / " + Legacy.HallOfFameLegacy);
            var t = Legacy.Totals(s);
            if (t.seasons > 0) UiControls.Stat(_content, "PRO CAREER", t.seasons + " SEASONS  ·  " + (t.games > 0 ? (t.pts / (float)t.games).ToString("0.0") : "0") + " PPG");
            if (s.draftPick > 0) UiControls.Stat(_content, "DRAFTED", "PICK " + s.draftPick);
            for (int i = s.history.Count - 1; i >= 0; i--) LastSeason(s.history[i]);
            if (s.history.Count == 0) Line("Your first season is still being written.", Theme.Muted);
            if (s.stage != LegacyStage.Retired)
                UiKit.Button(_content, "START OVER", () => UiControls.Dialog("START OVER?", "This ends this Legacy for good.",
                    ("START OVER", ButtonStyle.Primary, () => { App.Career.legacy = new LegacySaveData(); Save(); }), ("CANCEL", ButtonStyle.Ghost, null)), ButtonStyle.Ghost, 100f, 34f);
        }
    }
}
