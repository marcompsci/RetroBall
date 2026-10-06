using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>2 PLAYER ▸ TWO PHONES: host a game or join a friend's nearby (no internet needed).</summary>
    public sealed partial class MainMenuController
    {
        private static string LinkName()
        {
            string nick = App.Career?.nickname;
            return (string.IsNullOrEmpty(nick) ? "Retro Hoops" : nick) + " · " + SystemInfo.deviceModel.Replace("iPhone", "iPhone ").Replace("  ", " ").Trim();
        }

        private void ShowLinkMenu()
        {
            var column = OpenOverlay("TWO PHONES", out var footer);
            UiKit.Size(UiKit.Label(column,
                "Play head to head, each on your own iPhone or iPad. Both phones need Retro Hoops (the same version) and to be near each other with Wi-Fi or Bluetooth on. No internet, no account.",
                30f, Theme.Cream), 190f);
            if (!NearbyLink.Supported)
            {
                UiKit.Size(UiKit.Label(column, "Two-phone play works on iPhone, iPad and Mac builds of the game.", 30f, Theme.Muted, TextAlignmentOptions.Center), 100f);
            }
            else
            {
                Mode(column, "HOST A GAME", "Pick both teams, then wait for your friend to join.", ShowLinkHost, ButtonStyle.Primary);
                Mode(column, "JOIN A GAME", "Find your friend's phone nearby and join their game.", ShowLinkJoin, ButtonStyle.Secondary);
                UiKit.Size(UiKit.Label(column, "The first time, iOS asks to find devices on your local network: tap Allow on both phones.", 26f, Theme.Muted), 90f);
            }
            UiKit.Button(footer, "BACK", ShowVersus, ButtonStyle.Ghost, 130f, 44f);
        }

        private static int _linkHome, _linkAway = 1;

        private void ShowLinkHost()
        {
            var c = App.Catalog;
            var teams = c.TeamsInTier(TeamTier.League);
            if (teams.Count < 2) return;
            _linkHome = Mathf.Clamp(_linkHome, 0, teams.Count - 1);
            _linkAway = Mathf.Clamp(_linkAway, 0, teams.Count - 1);
            if (_linkAway == _linkHome) _linkAway = (_linkHome + 1) % teams.Count;

            var column = OpenOverlay("HOST A GAME", out var footer);
            UiKit.Size(UiKit.Label(column, "YOU (PLAYER 1)", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            TextMeshProUGUI homeLabel = null, awayLabel = null;
            homeLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(homeLabel, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { _linkHome = Next(_linkHome, _linkAway); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Size(UiKit.Label(column, "YOUR FRIEND (PLAYER 2)", 34f, Theme.Cyan, TextAlignmentOptions.Center, true), 50f);
            awayLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(awayLabel, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { _linkAway = Next(_linkAway, _linkHome); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            var status = UiKit.Label(column, "", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 130f);

            Button hostButton = null;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowLinkMenu(); }, ButtonStyle.Ghost, 130f, 44f);
            hostButton = UiKit.Button(footer, "WAIT FOR FRIEND", () =>
            {
                hostButton.interactable = false;
                var home = teams[_linkHome];
                uint seed = (uint)Environment.TickCount | 1u;
                var setup = LinkSetup.From(c, home.id, teams[_linkAway].id, home.homeCourtId, App.Career.settings.difficultyId, seed, App.Version, LinkName());
                var link = NearbyLink.Start(true, LinkName());
                var lobby = new LinkLobby(true, setup, App.Version, LinkProtocol.ContentFingerprint(c), c);
                LinkPoller.Attach(column, link, lobby, status, "Waiting for your friend to join…\nOn their phone: 2 PLAYER ▸ TWO PHONES ▸ JOIN A GAME.", 0);
            }, ButtonStyle.Primary, 130f);

            int Next(int current, int other)
            {
                int n = (current + 1) % teams.Count;
                if (n == other) n = (n + 1) % teams.Count;
                return n;
            }

            void Refresh()
            {
                homeLabel.text = teams[_linkHome].FullName.ToUpperInvariant();
                awayLabel.text = teams[_linkAway].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        private void ShowLinkJoin()
        {
            var c = App.Catalog;
            var column = OpenOverlay("JOIN A GAME", out var footer);
            var status = UiKit.Label(column, "Looking for games nearby…", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 130f);
            var list = UiKit.Column(column, 14f, null, "Hosts");
            UiKit.Size(list, 520f);
            var link = NearbyLink.Start(false, LinkName());
            var lobby = new LinkLobby(false, null, App.Version, LinkProtocol.ContentFingerprint(c), c);
            var poller = LinkPoller.Attach(column, link, lobby, status, "Connected! Getting the game from the host…", 1);
            poller.HostList = list;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowLinkMenu(); }, ButtonStyle.Ghost, 130f, 44f);
        }
    }

    /// <summary>Runs the lobby while a TWO PHONES screen is open; starts the game when both phones are ready.</summary>
    public sealed class LinkPoller : MonoBehaviour
    {
        private NearbyLink _link;
        private LinkLobby _lobby;
        private TextMeshProUGUI _status;
        private string _connectedText;
        private int _seat;
        private bool _started;
        private bool _joining;
        private float _nextList;
        private readonly List<string> _shown = new List<string>();
        public RectTransform HostList;

        public static LinkPoller Attach(Transform host, NearbyLink link, LinkLobby lobby, TextMeshProUGUI status, string connectedText, int seat)
        {
            var p = host.gameObject.AddComponent<LinkPoller>();
            p._link = link;
            p._lobby = lobby;
            p._status = status;
            p._connectedText = connectedText;
            p._seat = seat;
            status.text = seat == 0 ? connectedText : status.text;
            return p;
        }

        private void Update()
        {
            if (_started || _link == null) return;
            PowerMonitor.Wake();
            var state = _link.State;
            if (_lobby.Status == LobbyStatus.Waiting) _lobby.Update(_link);

            switch (_lobby.Status)
            {
                case LobbyStatus.Started:
                    _started = true;
                    var setup = _lobby.Setup;
                    if (!setup.Register(App.Catalog))
                    {
                        _status.text = "Couldn't set up the teams. Go back and try again.";
                        NearbyLink.Stop();
                        return;
                    }
                    LinkMatch.Transport = _link;
                    LinkMatch.Seat = _seat;
                    LinkMatch.Setup = setup;
                    App.PendingMatch = setup.ToRequest();
                    _status.text = "TIP OFF!";
                    SceneFlow.GoTo(SceneNames.Game);
                    return;
                case LobbyStatus.Rejected:
                case LobbyStatus.Lost:
                    _status.text = "<color=#FF6B6B>" + (_lobby.Error ?? "Lost the connection.") + "</color>";
                    NearbyLink.Stop();
                    _link = null;
                    return;
            }

            if (state == LinkState.Connected)
            {
                _status.text = (_seat == 0 ? "Connected to " + _link.PeerName + ". Starting…" : _connectedText);
                if (HostList != null) HostList.gameObject.SetActive(false);
            }
            else if (state == LinkState.Lost)
            {
                _status.text = "<color=#FF6B6B>Couldn't use the local network. Check Settings ▸ Privacy ▸ Local Network ▸ Retro Hoops is on, then try again.</color>";
            }
            else if (_seat == 1 && HostList != null && Time.unscaledTime >= _nextList)
            {
                _nextList = Time.unscaledTime + 0.5f;
                RefreshHosts();
            }
        }

        private void RefreshHosts()
        {
            var found = _link.Found();
            bool same = found.Count == _shown.Count;
            for (int i = 0; same && i < found.Count; i++) same = found[i] == _shown[i];
            if (same) return;
            _shown.Clear();
            _shown.AddRange(found);
            foreach (Transform child in HostList) Destroy(child.gameObject);
            _status.text = found.Count == 0 ? "Looking for games nearby…\nOn your friend's phone: 2 PLAYER ▸ TWO PHONES ▸ HOST A GAME."
                                            : (_joining ? "Joining…" : "Tap your friend's game to join:");
            for (int i = 0; i < found.Count; i++)
            {
                int index = i;
                UiKit.Button(HostList, found[i].ToUpperInvariant(), () =>
                {
                    _joining = true;
                    _status.text = "Joining " + found[index] + "… (the host's phone accepts automatically)";
                    _link.Join(index);
                }, ButtonStyle.Secondary, 110f, 30f);
            }
        }

        private void OnDestroy()
        {
            // Leaving the screen before the game starts hangs up.
            if (!_started && _link != null) NearbyLink.Stop();
        }
    }
}
