using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    // WATCH ON A THIRD PHONE: friends nearby can watch a two-phone game live on their own iPhone.
    // The host phone already has both players' inputs for every step it simulates, so it simply
    // forwards them (8 bytes a step) to anyone watching. The watching phone runs the same deterministic
    // simulation from the tip-off, so it shows exactly the same game. Someone who joins late gets the
    // whole game so far and fast-forwards to catch up. Watchers never send anything that matters.

    /// <summary>The host's side: records both inputs for every simulated step and sends them to watchers.</summary>
    public sealed class SpectatorFeed
    {
        /// <summary>Steps per <see cref="LinkMessage.Both"/> message.</summary>
        public const int Chunk = 120;
        public const int MaxWatchers = 3;

        public readonly LinkSetup Setup;
        private readonly List<PlayerInput> _a = new List<PlayerInput>(), _b = new List<PlayerInput>();
        private int _sent;
        private int _watchers;
        private bool _ended, _byeSent;

        public SpectatorFeed(LinkSetup setup) { Setup = setup; }

        public int Steps => _a.Count;
        public int Watchers => _watchers;

        /// <summary>Call once per simulated step, in order, with the inputs that step used.</summary>
        public void Record(PlayerInput teamA, PlayerInput teamB)
        {
            _a.Add(LinkProtocol.Quantize(teamA));
            _b.Add(LinkProtocol.Quantize(teamB));
        }

        /// <summary>The host is leaving: watchers get a goodbye.</summary>
        public void End() => _ended = true;

        /// <summary>
        /// Messages for the watchers now. When someone new has started watching, everything is sent again
        /// from the tip-off (the others ignore what they already have); otherwise only the new steps.
        /// </summary>
        public List<byte[]> Take(int watcherCount)
        {
            var list = new List<byte[]>();
            if (watcherCount > _watchers) _sent = -1; // a new watcher: start over
            _watchers = Math.Max(0, watcherCount);
            if (_watchers == 0) { _sent = -1; return list; }
            if (_sent < 0)
            {
                list.Add(LinkProtocol.WatchSetupMessage(Setup));
                _sent = 0;
            }
            while (_sent < _a.Count)
            {
                int n = Math.Min(Chunk, _a.Count - _sent);
                list.Add(LinkProtocol.Both(_sent, _a, _b, _sent, n));
                _sent += n;
            }
            if (_ended && !_byeSent)
            {
                list.Add(LinkProtocol.Simple(LinkMessage.Bye));
                _byeSent = true;
            }
            return list;
        }
    }

    /// <summary>
    /// The watching phone's side: collects the host's stream and hands the steps back in order.
    /// If the host starts another game (a rematch or the next Couch Cup game), everything after its
    /// setup goes to <see cref="Next"/>.
    /// </summary>
    public sealed class Spectator
    {
        public LinkSetup Setup { get; private set; }
        public string Error { get; private set; }
        public bool HostLeft { get; private set; }
        /// <summary>The host's next game, once its setup has arrived.</summary>
        public Spectator Next { get; private set; }
        /// <summary>Next step to simulate.</summary>
        public int NextTick { get; private set; }

        private readonly List<PlayerInput> _a = new List<PlayerInput>(), _b = new List<PlayerInput>();
        private readonly List<PlayerInput> _scratchA = new List<PlayerInput>(), _scratchB = new List<PlayerInput>();
        private readonly string _appVersion;
        private readonly uint _content;
        private readonly ContentCatalog _catalog;

        public Spectator(string appVersion, uint content, ContentCatalog catalog)
        {
            _appVersion = appVersion;
            _content = content;
            _catalog = catalog;
        }

        /// <summary>Steps received so far.</summary>
        public int Received => _a.Count;
        /// <summary>Steps received but not yet simulated.</summary>
        public int Behind => _a.Count - NextTick;
        public bool Ready => Setup != null && Error == null;

        public void Receive(byte[] m)
        {
            if (m == null || m.Length == 0) return;
            if (Next != null) { Next.Receive(m); return; }
            switch ((LinkMessage)m[0])
            {
                case LinkMessage.WatchSetup:
                    var s = LinkProtocol.ReadSetup(m);
                    if (s == null) return;
                    if (Setup == null)
                    {
                        Error = LinkProtocol.Incompatible(s, _appVersion, _content, _catalog);
                        Setup = s;
                    }
                    else if (s.Session != Setup.Session || s.Seed != Setup.Seed)
                    {
                        // The host started another game.
                        Next = new Spectator(_appVersion, _content, _catalog);
                        Next.Receive(m);
                    }
                    break;
                case LinkMessage.Both:
                    if (Setup == null) return;
                    _scratchA.Clear();
                    _scratchB.Clear();
                    if (!LinkProtocol.ReadBoth(m, out int first, _scratchA, _scratchB)) return;
                    // Steps arrive in order; anything already held (a resend for a newer watcher) is skipped,
                    // anything past a gap waits for the resend.
                    if (first > _a.Count) return;
                    for (int i = _a.Count - first; i < _scratchA.Count; i++)
                    {
                        _a.Add(_scratchA[i]);
                        _b.Add(_scratchB[i]);
                    }
                    break;
                case LinkMessage.Bye:
                    HostLeft = true;
                    break;
            }
        }

        public bool TryStep(out PlayerInput teamA, out PlayerInput teamB)
        {
            teamA = teamB = default;
            if (!Ready || NextTick >= _a.Count) return false;
            teamA = _a[NextTick];
            teamB = _b[NextTick];
            NextTick++;
            return true;
        }

        /// <summary>
        /// How many steps to simulate this frame: what the clock says when caught up (one extra while a
        /// backlog builds), and up to 10x speed while catching up after joining late.
        /// </summary>
        public static int StepsThisFrame(int behind, int dueByClock)
        {
            if (behind <= 0) return 0;
            if (behind > 90) return Math.Min(behind, Math.Max(dueByClock, 10));
            return Math.Min(behind, Math.Max(0, dueByClock) + (behind > 12 ? 1 : 0));
        }
    }
}
