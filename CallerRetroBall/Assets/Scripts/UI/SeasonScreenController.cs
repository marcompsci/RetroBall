using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// SeasonScene: the Rise Mode hub. Shows the current stage, crew record, energy and
    /// chemistry, the objective, and PLAY NEXT. In the league it adds standings and, in the
    /// playoffs, the bracket. Pending event cards open as a modal before the next game.
    /// All rules live in <see cref="RiseEngine"/> / <see cref="SeasonEngine"/>; this only draws them.
    /// </summary>
    public sealed class SeasonScreenController : ScreenBase
    {
        protected override string ScreenTitle => "RISE MODE";
        protected override string BackdropCourtId => "court.overpass_park";
        protected override int MusicTrack => 2;

        private RectTransform _content;

        protected override void Build()
        {
            _content = UiKit.ScrollColumn(Body, 18f, new RectOffset(48, 48, 12, 40));
            Rebuild();

            var outcome = App.LastRiseOutcome;
            App.LastRiseOutcome = RiseOutcome.None;
            // Story scenes play first, then whatever the last game changed.
            PlayStory(() => AfterStory(outcome));
        }

        private void PlayStory(System.Action then)
        {
            string pending = Story.Pending(App.Career);
            if (pending == null)
            {
                then();
                return;
            }
            StoryView.Show(Story.Beat(pending, App.Career.nickname), () =>
            {
                Rebuild();
                PlayStory(then); // several scenes can be due at once (e.g. circuit cleared + rival)
            });
        }

        private void AfterStory(RiseOutcome outcome)
        {
            if (outcome == RiseOutcome.Champion)
                UiControls.Dialog("CHAMPIONS!", "The First Callers hold " + DefaultContent.ChampionshipName + ". Start the next season when you're ready to defend it.",
                                  ("NICE", ButtonStyle.Primary, ShowPendingEvent));
            else if (outcome == RiseOutcome.EnteredLeague)
                UiControls.Dialog("CIRCUIT CLEARED", "Every street crew is beaten. " + DefaultContent.LeagueName + " has an open spot — it's yours.",
                                  ("LET'S GO", ButtonStyle.Primary, ShowPendingEvent));
            else if (outcome == RiseOutcome.MadePlayoffs)
                UiControls.Dialog("PLAYOFFS", "Top " + SeasonEngine.PlayoffTeams + " finish. Win two more for the Cup.",
                                  ("OK", ButtonStyle.Primary, ShowPendingEvent));
            else
                ShowPendingEvent();
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);

            var c = App.Catalog;
            var career = App.Career;
            var r = career.rise;

            UiKit.Size(UiKit.Label(_content, StageName(r.stage), 52f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            UiKit.Size(UiKit.Label(_content, RiseEngine.Objective(r, c), 36f, Theme.Cream), 110f);

            var stats = UiKit.Row(_content, 12f, "Meters");
            UiKit.Size(stats, 64f);
            UiControls.Stat(stats, "ENERGY", r.energy.ToString());
            UiControls.Stat(stats, "CHEM", r.chemistry.ToString());
            if (r.season != null && r.stage != RiseStage.Circuit)
            {
                var rec = FindRecord(r.season, RiseEngine.CrewId);
                UiControls.Stat(stats, "RECORD", rec != null ? rec.Wins + "-" + rec.Losses : "0-0");
            }
            else
            {
                UiControls.Stat(stats, "CIRCUIT", r.circuitBeaten.Count + "/" + RiseEngine.CircuitOrder.Length);
            }

            // Rival Challenge: once a season, from week 5. Doesn't count in the standings.
            if (RivalEngine.ChallengeAvailable(career))
            {
                var rivalReq = RivalEngine.Challenge(career, c, career.settings.difficultyId);
                UiKit.Button(_content, "RIVAL CHALLENGE: NEON STATIC", () =>
                {
                    App.PendingMatch = rivalReq;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 120f, 40f);
                UiKit.Size(UiKit.Label(_content, "Doesn't count in the standings. Win for +" + RivalEngine.WinBonus + " SP.  Record vs Static: " +
                                       career.rival.wins + "-" + career.rival.losses, 28f, Theme.Muted), 50f);
            }

            var next = RiseEngine.NextMatch(r, c, career.settings.difficultyId);
            if (next != null)
            {
                var opp = c.Team(next.AwayTeamId);
                var court = c.Court(next.CourtId);
                var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "NextGame");
                UiKit.Size(card, 170f);
                if (opp != null)
                {
                    var logo = UiKit.Picture(card.transform, TextureFactory.TeamLogo(opp));
                    logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    logo.rectTransform.pivot = new Vector2(0f, 0.5f);
                    logo.rectTransform.sizeDelta = new Vector2(130f, 130f);
                    logo.rectTransform.anchoredPosition = new Vector2(20f, 0f);
                }
                string round = next.Round == 2 ? "FINAL" : (next.Round == 1 ? "SEMIFINAL" : "NEXT GAME");
                var text = UiKit.Label(card.transform, "<color=#FFD166>" + round + "</color>\nVS " + (opp?.FullName ?? "?").ToUpperInvariant() +
                                       (court != null ? "\n<size=70%><color=#8D99AE>" + court.displayName + "</color></size>" : ""),
                                       36f, Theme.Cream, TextAlignmentOptions.Left, true);
                UiKit.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(170f, 8f);
                text.rectTransform.offsetMax = new Vector2(-20f, -8f);

                UiKit.Button(_content, "PLAY NEXT", () => Play(next), ButtonStyle.Primary, 150f);
            }
            else if (r.stage == RiseStage.Complete)
            {
                UiKit.Button(_content, "START NEXT SEASON", () =>
                {
                    RiseEngine.StartNextSeason(r, c);
                    App.SaveCareer();
                    Rebuild();
                }, ButtonStyle.Primary, 150f);
            }

            if (r.season != null && r.stage != RiseStage.Circuit)
            {
                Bracket(r.season);
                StandingsTable(r.season);
            }
            else
            {
                CircuitList(r);
            }

            UiKit.Button(_content, "YOUR CREW", ShowCrew, ButtonStyle.Secondary, 110f, 40f);
            var mates = CrewEngine.TeammateDefs(career, c);
            UiKit.Size(UiKit.Label(_content, "With " + string.Join(" & ", mates.ConvertAll(m => m.DisplayName)) +
                                   "  ·  " + CrewEngine.Pool(career, c).Count + " players available", 28f, Theme.Muted), 50f);

            var diffs = c.Difficulties;
            int di = Mathf.Max(0, diffs.FindIndex(d => d.id == career.settings.difficultyId));
            UiControls.ChoiceRow(_content, "DIFFICULTY", diffs.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray(), di, i =>
            {
                career.settings.difficultyId = diffs[i].id;
                App.SaveCareer();
            });
        }

        private void Play(MatchRequest request)
        {
            if (!string.IsNullOrEmpty(App.Career.rise.pendingEventId))
            {
                ShowPendingEvent();
                return;
            }
            App.PendingMatch = request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void ShowPendingEvent()
        {
            var r = App.Career.rise;
            var card = EventCards.Find(r.pendingEventId);
            if (card == null) return;
            var buttons = new (string, ButtonStyle, System.Action)[card.choices.Count];
            for (int i = 0; i < card.choices.Count; i++)
            {
                int index = i;
                var choice = card.choices[i];
                buttons[i] = (Loc.T(choice.label).ToUpperInvariant() + "\n<size=60%>" + choice.effect.Describe() + "</size>",
                              i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary,
                              () =>
                              {
                                  RiseEngine.ResolveEvent(r, App.Career, index);
                                  App.SaveCareer();
                                  Rebuild();
                              });
            }
            UiControls.Dialog(Loc.T(card.title).ToUpperInvariant(), Loc.T(card.body), buttons);
        }

        // ------------------------------------------------------------------ crew

        private GameObject _crewOverlay;

        /// <summary>Your two teammate spots and everyone you can sign or swap in.</summary>
        private void ShowCrew()
        {
            if (_crewOverlay != null) Destroy(_crewOverlay);
            var c = App.Catalog;
            var career = App.Career;
            var canvas = UiKit.CreateScreenCanvas("CrewOverlay", 30);
            _crewOverlay = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.04f, 0.96f, 40f);
            panel.raycastTarget = true;
            var footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 130f);
            footer.anchoredPosition = new Vector2(0f, 24f);
            UiKit.Button(footer, "DONE", () =>
            {
                Destroy(_crewOverlay);
                _crewOverlay = null;
                Rebuild();
            }, ButtonStyle.Primary, 120f);
            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, 170f);
            var col = UiKit.ScrollColumn(body, 14f, new RectOffset(36, 36, 30, 20));

            UiKit.Size(UiKit.ShadowLabel(col, "YOUR CREW", 60f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 90f);
            UiKit.Size(UiKit.Label(col, "SP: " + career.signalPoints + "    Beat a team to unlock its players (league teams: their bench player).",
                                   28f, Theme.Muted), 70f);

            var mates = CrewEngine.Teammates(career, c);
            for (int slot = 0; slot < mates.Count; slot++)
            {
                var p = c.Player(mates[slot]);
                UiKit.Size(UiKit.Label(col, "SPOT " + (slot + 1) + ":  " + Describe(p), 34f, Theme.Gold, TextAlignmentOptions.Left, true), 56f);
            }

            UiKit.Size(UiKit.Label(col, "AVAILABLE", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            foreach (var id in CrewEngine.Pool(career, c))
            {
                if (mates.Contains(id)) continue;
                var p = c.Player(id);
                var check = CrewEngine.CanRecruit(career, c, id);
                bool free = CrewEngine.IsFree(career, c, id);
                string price = free ? "FREE" : CrewEngine.Cost(p) + " SP";
                UiKit.Size(UiKit.Label(col, Describe(p) + "   <color=#FFD166>" + price + "</color>", 32f, Theme.Cream, TextAlignmentOptions.Left, false), 52f);
                var row = UiKit.Row(col, 16f, "Swap " + id);
                UiKit.Size(row, 90f);
                for (int slot = 0; slot < CrewEngine.TeammateSlots; slot++)
                {
                    int s = slot;
                    var b = UiKit.Button(row, "PUT IN SPOT " + (slot + 1), () =>
                    {
                        if (CrewEngine.Recruit(App.Career, App.Catalog, id, s) == RecruitCheck.Ok)
                        {
                            App.SaveCareer();
                            Core.Haptics.Success();
                        }
                        ShowCrew();
                    }, check == RecruitCheck.Ok ? ButtonStyle.Secondary : ButtonStyle.Ghost, 80f, 28f);
                    b.interactable = check == RecruitCheck.Ok;
                }
            }
        }

        private string Describe(PlayerDef p)
        {
            if (p == null) return "?";
            var arch = App.Catalog.ArchetypeById(p.archetypeId);
            return p.DisplayName.ToUpperInvariant() + " #" + p.jerseyNumber + "  ·  " +
                   (arch?.displayName ?? "").ToUpperInvariant() + "  ·  OVR " + p.attributes.Overall;
        }

        private void CircuitList(RiseSaveData r)
        {
            var c = App.Catalog;
            UiKit.Size(UiKit.Label(_content, DefaultContent.CircuitName.ToUpperInvariant(), 40f, Theme.Gold, TextAlignmentOptions.Left, true), 60f);
            foreach (var id in RiseEngine.CircuitOrder)
            {
                var team = c.Team(id);
                if (team == null) continue;
                bool beaten = r.circuitBeaten.Contains(id);
                UiKit.Size(UiKit.Label(_content, (beaten ? "<color=#4CC9F0>BEATEN</color>  " : "<color=#8D99AE>—</color>  ") + team.FullName.ToUpperInvariant(),
                                       34f, Theme.Cream, TextAlignmentOptions.Left, true), 52f);
            }
        }

        private void Bracket(SeasonSaveData s)
        {
            if (!SeasonEngine.HasRound(s, 1)) return;
            UiKit.Size(UiKit.Label(_content, DefaultContent.ChampionshipName.ToUpperInvariant(), 40f, Theme.Pink, TextAlignmentOptions.Left, true), 60f);
            foreach (var g in s.games)
            {
                if (g.round == 0) continue;
                string label = g.round == 2 ? "FINAL" : "SEMI";
                string line = label + "  " + Abbr(g.homeId) + (g.played ? " " + g.homeScore + " - " + g.awayScore + " " : "  vs  ") + Abbr(g.awayId);
                bool mine = g.Involves(RiseEngine.CrewId);
                UiKit.Size(UiKit.Label(_content, line, 34f, mine ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true), 50f);
            }
            if (!string.IsNullOrEmpty(s.championId))
                UiKit.Size(UiKit.Label(_content, "CHAMPION: " + (App.Catalog.Team(s.championId)?.FullName ?? s.championId).ToUpperInvariant(),
                                       34f, Theme.Cyan, TextAlignmentOptions.Left, true), 50f);
        }

        private void StandingsTable(SeasonSaveData s)
        {
            UiKit.Size(UiKit.Label(_content, "STANDINGS  ·  WEEK " + Mathf.Min(s.currentWeek + 1, Mathf.Max(1, s.weeks)) + "/" + s.weeks,
                                   40f, Theme.Gold, TextAlignmentOptions.Left, true), 60f);
            UiKit.Size(UiKit.Label(_content, "#  TEAM<pos=62%>W-L<pos=76%>DIFF<pos=90%>STK", 28f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            var table = SeasonEngine.Standings(s);
            for (int i = 0; i < table.Count; i++)
            {
                var t = table[i];
                var team = App.Catalog.Team(t.TeamId);
                bool mine = t.TeamId == RiseEngine.CrewId;
                string diff = t.Differential > 0 ? "+" + t.Differential : t.Differential.ToString();
                string line = (i + 1) + "  " + (team?.FullName ?? t.TeamId).ToUpperInvariant() +
                              "<pos=62%>" + t.Wins + "-" + t.Losses + "<pos=76%>" + diff + "<pos=90%>" + t.StreakText;
                var label = UiKit.Label(_content, line, 30f, mine ? Theme.Gold : (i < SeasonEngine.PlayoffTeams ? Theme.Cream : Theme.Muted),
                                        TextAlignmentOptions.Left, mine);
                UiKit.Size(label, 46f);
            }
        }

        private static TeamRecord FindRecord(SeasonSaveData s, string id)
        {
            foreach (var t in SeasonEngine.Standings(s)) if (t.TeamId == id) return t;
            return null;
        }

        private static string Abbr(string id) => App.Catalog.Team(id)?.abbreviation ?? "?";

        private static string StageName(RiseStage stage)
        {
            switch (stage)
            {
                case RiseStage.Circuit: return DefaultContent.CircuitName.ToUpperInvariant();
                case RiseStage.Season: return DefaultContent.LeagueName.ToUpperInvariant();
                case RiseStage.Playoffs: return "PLAYOFFS";
                default: return "SEASON COMPLETE";
            }
        }
    }
}
