using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>2 PLAYER ▸ GAME TAPES: watch saved two-phone, Live and watched games again, or send one to a friend nearby.</summary>
    public sealed partial class MainMenuController
    {
        private void ShowTapes()
        {
            var column = OpenOverlay("GAME TAPES", out var footer);
            FirstVisit(Tours.TapesCard);
            var tapes = TapeStore.List();
            UiKit.Size(UiKit.Label(column,
                "Whole games saved from TWO PHONES, LIVE and WATCH A GAME (tap SAVE GAME TAPE after the final). Up to " + Tapes.MaxSaved +
                " are kept. While one plays, tap the court to change the speed.", 26f, Theme.Muted, TextAlignmentOptions.Center), 120f);
            if (tapes.Count == 0)
                UiKit.Size(UiKit.Label(column, "No tapes yet.", 34f, Theme.Cream, TextAlignmentOptions.Center, true), 80f);
            foreach (var e in tapes)
            {
                var entry = e;
                UiKit.Size(UiKit.Label(column, Tapes.Line(entry.Tape), 28f, Theme.Cream, TextAlignmentOptions.Left, true), 46f);
                var row = UiKit.Row(column, 10f, "Tape buttons");
                UiKit.Size(row, 70f);
                UiKit.Button(row, "WATCH", () =>
                {
                    if (TapeStore.PrepareToWatch(entry.Tape, entry.Path, out string why)) SceneFlow.GoTo(SceneNames.Game);
                    else UiControls.Dialog("CAN'T PLAY THIS TAPE", why, ("OK", ButtonStyle.Primary, null));
                }, ButtonStyle.Primary, 64f, 28f);
                if (NearbyLink.Supported) UiKit.Button(row, "SEND", () => ShowTapeSend(entry.Tape), ButtonStyle.Secondary, 64f, 28f);
                UiKit.Button(row, "DELETE", () =>
                    UiControls.Dialog("DELETE TAPE", "Delete " + Tapes.Line(entry.Tape) + "?",
                        ("DELETE", ButtonStyle.Primary, () => { TapeStore.Delete(entry.Path); ShowTapes(); }),
                        ("KEEP", ButtonStyle.Ghost, null)), ButtonStyle.Ghost, 64f, 26f);
            }
            UiKit.Button(footer, "BACK", ShowVersus, ButtonStyle.Ghost, 130f, 44f);
            if (NearbyLink.Supported) UiKit.Button(footer, "RECEIVE A TAPE", ShowTapeReceive, ButtonStyle.Secondary, 130f, 34f);
        }

        private void ShowTapeSend(GameTape tape)
        {
            var column = OpenOverlay("SEND A TAPE", out var footer);
            UiKit.Size(UiKit.Label(column, Tapes.Line(tape), 32f, Theme.Gold, TextAlignmentOptions.Center, true), 70f);
            UiKit.Size(UiKit.Label(column, "On your friend's phone: 2 PLAYER ▸ GAME TAPES ▸ RECEIVE A TAPE, then tap this phone's name.", 28f, Theme.Cream, TextAlignmentOptions.Center), 120f);
            var status = UiKit.Label(column, "Waiting for your friend…", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 120f);
            var link = NearbyLink.Start(true, LinkName());
            TapePoller.Attach(column, link, TapeTransfer.Send(tape), status, null);
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowTapes(); }, ButtonStyle.Ghost, 130f, 44f);
        }

        private void ShowTapeReceive()
        {
            var column = OpenOverlay("RECEIVE A TAPE", out var footer);
            var status = UiKit.Label(column, "Looking for phones nearby…", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 120f);
            var list = UiKit.Column(column, 14f, null, "Senders");
            UiKit.Size(list, 520f);
            var link = NearbyLink.Start(false, LinkName());
            TapePoller.Attach(column, link, TapeTransfer.Receive(), status, list).Saved += ShowTapes;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowTapes(); }, ButtonStyle.Ghost, 130f, 44f);
        }
    }

    /// <summary>Runs a tape transfer while SEND / RECEIVE A TAPE is open.</summary>
    public sealed class TapePoller : MonoBehaviour
    {
        private NearbyLink _link;
        private TapeTransfer _transfer;
        private TextMeshProUGUI _status;
        private RectTransform _list;
        private float _nextList;
        private bool _done, _joining;
        private readonly List<string> _shown = new List<string>();
        public event Action Saved;

        public static TapePoller Attach(Transform host, NearbyLink link, TapeTransfer transfer, TextMeshProUGUI status, RectTransform hostList)
        {
            var p = host.gameObject.AddComponent<TapePoller>();
            p._link = link;
            p._transfer = transfer;
            p._status = status;
            p._list = hostList;
            return p;
        }

        private void Update()
        {
            if (_done || _link == null) return;
            PowerMonitor.Wake();
            _transfer.Update(_link);
            switch (_transfer.Status)
            {
                case TapeTransfer.State.Done:
                    _done = true;
                    if (_transfer.Received != null)
                    {
                        bool ok = TapeStore.Save(_transfer.Received);
                        _status.text = ok ? "Got it: " + Tapes.Line(_transfer.Received) : "<color=#FF6B6B>Couldn't save the tape.</color>";
                    }
                    else _status.text = "Sent!";
                    Haptics.Success();
                    StartCoroutine(HangUpSoon());
                    return;
                case TapeTransfer.State.Failed:
                    _done = true;
                    _status.text = "<color=#FF6B6B>" + _transfer.Error + "</color>";
                    NearbyLink.Stop();
                    return;
                case TapeTransfer.State.Sending:
                    _status.text = "Sending… " + Mathf.RoundToInt(_transfer.Progress * 100f) + "%";
                    break;
            }
            if (_link.State == LinkState.Connected)
            {
                if (_list != null) _list.gameObject.SetActive(false);
                if (_transfer.Status == TapeTransfer.State.Waiting)
                    _status.text = _transfer.Progress > 0f ? "Receiving… " + Mathf.RoundToInt(_transfer.Progress * 100f) + "%" : "Connected…";
            }
            else if (_link.State == LinkState.Lost)
            {
                _status.text = "<color=#FF6B6B>Couldn't use the local network. Check Settings ▸ Privacy ▸ Local Network ▸ Retro Hoops, then try again.</color>";
            }
            else if (_list != null && Time.unscaledTime >= _nextList)
            {
                _nextList = Time.unscaledTime + 0.5f;
                var found = _link.Found();
                bool same = found.Count == _shown.Count;
                for (int i = 0; same && i < found.Count; i++) same = found[i] == _shown[i];
                if (same) return;
                _shown.Clear();
                _shown.AddRange(found);
                foreach (Transform child in _list) Destroy(child.gameObject);
                _status.text = found.Count == 0 ? "Looking for phones nearby…\nOn your friend's phone: GAME TAPES ▸ SEND." : (_joining ? "Connecting…" : "Tap your friend's phone:");
                for (int i = 0; i < found.Count; i++)
                {
                    int index = i;
                    UiKit.Button(_list, found[i].ToUpperInvariant(), () => { _joining = true; _status.text = "Connecting…"; _link.Join(index); }, ButtonStyle.Secondary, 110f, 30f);
                }
            }
        }

        private System.Collections.IEnumerator HangUpSoon()
        {
            // Give the last message a moment to arrive before hanging up.
            yield return new WaitForSecondsRealtime(1f);
            NearbyLink.Stop();
            _link = null;
            Saved?.Invoke();
        }

        private void OnDestroy()
        {
            if (_link != null) NearbyLink.Stop();
        }
    }
}
