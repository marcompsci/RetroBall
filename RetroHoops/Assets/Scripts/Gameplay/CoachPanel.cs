using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Phase 35 COACH MODE sideline: four buttons along the bottom (CALL A PLAY, the offence's focus, the defence, SUBS).
    /// The play and substitution lists open over the court and hold the game while they're open; FOCUS and DEFENSE
    /// change with a tap. Everything goes through the <see cref="MatchSimulation"/> coach calls.
    /// </summary>
    public sealed class CoachPanel : MonoBehaviour
    {
        /// <summary>Something worth a toast ("PICK AND ROLL ON THE NEXT TRIP").</summary>
        public event Action<string> Said;

        private MatchSimulation _m;
        private GameObject _bar, _popup;
        private TextMeshProUGUI _focus, _defense, _subs;
        private int _subFor = -1;

        /// <summary>A list is open: the game waits.</summary>
        public bool Holding => _popup != null;

        public static readonly DefenseScheme[] Schemes = { DefenseScheme.ManToMan, DefenseScheme.Pressure, DefenseScheme.PackLine, DefenseScheme.Zone };

        public static CoachPanel Create(MatchSimulation m)
        {
            var canvas = UiKit.CreateScreenCanvas("CoachCanvas", 21);
            var panel = canvas.gameObject.AddComponent<CoachPanel>();
            panel._m = m;
            panel.Build(UiKit.SafeArea(canvas.transform));
            return panel;
        }

        private void Build(RectTransform safe)
        {
            var bg = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.8f), name: "CoachBar");
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-24f, 128f);
            rt.anchoredPosition = new Vector2(0f, 10f);
            _bar = bg.gameObject;
            var row = UiKit.Row(rt, 10f, "Buttons");
            UiKit.Stretch(row, 10f);
            Small(row, "CALL A PLAY", OpenPlays, ButtonStyle.Primary);
            _focus = Small(row, "", CycleFocus).GetComponentInChildren<TextMeshProUGUI>();
            _defense = Small(row, "", CycleDefense).GetComponentInChildren<TextMeshProUGUI>();
            _subs = Small(row, "SUBS", OpenSubs).GetComponentInChildren<TextMeshProUGUI>();
            Refresh();
        }

        private static UnityEngine.UI.Button Small(RectTransform row, string text, Action onClick, ButtonStyle style = ButtonStyle.Secondary)
        {
            var b = UiKit.Button(row, text, onClick, style, 96f, 26f);
            UiKit.Size(b, 96f).flexibleWidth = 1f;
            return b;
        }

        public void SetVisible(bool visible)
        {
            if (_bar != null && _bar.activeSelf != visible) _bar.SetActive(visible);
            if (!visible) Close();
        }

        /// <summary>Keeps the button labels current (a substitution happened, a play started).</summary>
        public void Refresh()
        {
            if (_m == null) return;
            _focus.text = "O: " + MatchSimulation.FocusName(_m.Focus);
            _defense.text = "D: " + MatchSimulation.SchemeName(_m.SchemeOf(_m.Setup.HumanTeam));
            _subs.text = _m.PendingSubs > 0 ? "SUBS (" + _m.PendingSubs + ")" : "SUBS";
        }

        private void CycleFocus()
        {
            var next = (CoachFocus)(((int)_m.Focus + 1) % 4);
            _m.CoachSetFocus(next);
            Said?.Invoke("OFFENSE: " + MatchSimulation.FocusName(next));
            Refresh();
        }

        private void CycleDefense()
        {
            var now = _m.SchemeOf(_m.Setup.HumanTeam);
            int i = Array.IndexOf(Schemes, now);
            var next = Schemes[(i + 1) % Schemes.Length];
            _m.CoachSetDefense(next);
            Said?.Invoke("DEFENSE: " + MatchSimulation.SchemeName(next));
            Refresh();
        }

        // ------------------------------------------------------------------ lists

        private RectTransform OpenPopup(string title)
        {
            if (_popup != null) Destroy(_popup);
            var scrim = UiKit.Panel(transform, Theme.Scrim, name: "CoachList");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            _popup = scrim.gameObject;
            var safe = UiKit.SafeArea(scrim.transform);
            var holder = UiKit.NewRect("Body", safe);
            UiKit.Band(holder, 0.04f, 0.96f, 80f);
            var col = UiKit.ScrollColumn(holder, 10f, new RectOffset(20, 20, 16, 30));
            UiKit.Size(UiKit.Label(col, title, 50f, Theme.Gold, TextAlignmentOptions.Center, true), 72f);
            return col;
        }

        public void Close()
        {
            if (_popup != null) Destroy(_popup);
            _popup = null;
            _subFor = -1;
        }

        private void OpenPlays()
        {
            var col = OpenPopup("CALL A PLAY");
            Play(col, PlayCall.PickAndRoll, "PICK AND ROLL", "A big sets a screen for the ball handler, then rolls to the rim.");
            Play(col, PlayCall.Backdoor, "BACKDOOR", "A wing fakes high and cuts behind his man for a lob.");
            Play(col, PlayCall.PostUp, "POST UP", "Your best big seals on the block; everyone clears out.");
            UiKit.Button(col, "BACK", Close, ButtonStyle.Ghost, 80f, 28f);
        }

        private void Play(RectTransform col, PlayCall play, string name, string what)
        {
            UiKit.Button(col, name, () =>
            {
                Close();
                bool ran = _m.CoachCallPlay(play) && _m.PendingPlay == PlayCall.None;
                Said?.Invoke(ran ? name + "!" : name + " ON THE NEXT TRIP");
            }, ButtonStyle.Primary, 90f, 32f);
            UiKit.Size(UiKit.Label(col, what, 24f, Theme.Muted, TextAlignmentOptions.Center, false), 40f);
        }

        private void OpenSubs()
        {
            var col = OpenPopup(_subFor < 0 ? "WHO COMES OUT?" : "WHO GOES IN?");
            int team = _m.Setup.HumanTeam;
            if (_subFor < 0)
            {
                for (int i = 0; i < _m.Players.Length; i++)
                {
                    var p = _m.Players[i];
                    if (p.Team != team) continue;
                    int index = i;
                    var line = _m.Stats[i];
                    string text = p.Def.DisplayName.ToUpperInvariant() + "  ·  " + Mathf.RoundToInt(p.Stamina * 100f) + "% LEGS  ·  "
                                  + line.points + " PTS" + (_m.SubPendingFor(i) ? "  ·  COMING OUT" : "");
                    UiKit.Button(col, text, () => { _subFor = index; OpenSubs(); }, p.Stamina < MatchSimulation.SubBelowStamina ? ButtonStyle.Primary : ButtonStyle.Secondary, 80f, 24f);
                }
                UiKit.Button(col, "AUTO SUBS: " + (_m.CoachAutoSubs ? "ON" : "OFF"), () =>
                {
                    _m.CoachAutoSubs = !_m.CoachAutoSubs;
                    OpenSubs();
                }, ButtonStyle.Ghost, 80f, 26f);
                UiKit.Size(UiKit.Label(col, "Subs go in at the next dead ball. With AUTO SUBS on, tired players also come out by themselves.", 24f, Theme.Muted, TextAlignmentOptions.Center, false), 70f);
                if (_m.PendingSubs > 0) UiKit.Button(col, "CANCEL MY SUBS", () => { _m.CoachCancelSubs(); Refresh(); OpenSubs(); }, ButtonStyle.Ghost, 80f, 26f);
            }
            else
            {
                var bench = _m.CoachBench();
                if (bench.Count == 0) UiKit.Size(UiKit.Label(col, "Nobody on the bench.", 30f, Theme.Cream, TextAlignmentOptions.Center, true), 60f);
                for (int seat = 0; seat < bench.Count; seat++)
                {
                    int s = seat;
                    var b = bench[seat];
                    string text = b.Def.DisplayName.ToUpperInvariant() + "  ·  " + Mathf.RoundToInt(b.Stamina * 100f) + "% LEGS  ·  " + b.Line.points + " PTS";
                    UiKit.Button(col, text, () =>
                    {
                        string outName = _m.Players[_subFor].Def.DisplayName.ToUpperInvariant();
                        if (_m.CoachSub(_subFor, s)) Said?.Invoke(b.Def.DisplayName.ToUpperInvariant() + " IN FOR " + outName + " AT THE NEXT WHISTLE");
                        Close();
                        Refresh();
                    }, ButtonStyle.Secondary, 80f, 24f);
                }
            }
            UiKit.Button(col, "BACK", () =>
            {
                if (_subFor >= 0) { _subFor = -1; OpenSubs(); }
                else Close();
            }, ButtonStyle.Ghost, 80f, 28f);
        }
    }
}
