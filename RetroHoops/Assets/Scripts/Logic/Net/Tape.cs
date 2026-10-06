using System;
using System.Collections.Generic;
using System.Text;

namespace CallerRetroBall.Logic
{
    // GAME TAPES (Phase 31): a whole two-phone, Live or watched game saved as its setup plus both players'
    // inputs for every step. The simulation is deterministic, so playing the inputs back rebuilds the exact
    // game, frame for frame, from a file of a few kilobytes. Tapes can be watched again later and sent to a
    // friend's phone nearby. A tape only plays on the same game version (the same rules and teams).

    public sealed class GameTape
    {
        public LinkSetup Setup;
        public readonly List<PlayerInput> TeamA = new List<PlayerInput>(), TeamB = new List<PlayerInput>();
        public string Title = "";
        /// <summary>Unix seconds when it was saved.</summary>
        public long SavedAt;
        public int ScoreA, ScoreB;

        public int Steps => TeamA.Count;
        public float Seconds => Steps / 60f;
    }

    public static class Tapes
    {
        public const int MaxSaved = 12;
        public const int MaxSteps = 60 * 60 * 40; // 40 minutes at 60 Hz: more than any game
        public const int MaxBytes = 2 * 1024 * 1024;
        private static readonly byte[] Magic = { (byte)'R', (byte)'H', (byte)'T', (byte)'1' };

        /// <summary>A tape of a game that just ended.</summary>
        public static GameTape From(LinkSetup setup, IReadOnlyList<PlayerInput> a, IReadOnlyList<PlayerInput> b, string title, long savedAt, int scoreA, int scoreB)
        {
            var t = new GameTape { Setup = setup, Title = title ?? "", SavedAt = savedAt, ScoreA = scoreA, ScoreB = scoreB };
            int n = Math.Min(Math.Min(a.Count, b.Count), MaxSteps);
            for (int i = 0; i < n; i++)
            {
                t.TeamA.Add(LinkProtocol.Quantize(a[i]));
                t.TeamB.Add(LinkProtocol.Quantize(b[i]));
            }
            return t;
        }

        /// <summary>
        /// The file: "RHT1", then length-prefixed setup text, title, saved-at, both scores, the step count, and the
        /// steps run-length coded (a run count, then the 8 bytes both inputs pack to): held sticks repeat a lot.
        /// </summary>
        public static byte[] Encode(GameTape t)
        {
            var o = new List<byte>(4096);
            o.AddRange(Magic);
            Bytes(o, LinkProtocol.SetupMessage(t.Setup));
            Bytes(o, Encoding.UTF8.GetBytes(t.Title ?? ""));
            Long(o, t.SavedAt);
            Int(o, t.ScoreA);
            Int(o, t.ScoreB);
            int n = Math.Min(t.TeamA.Count, t.TeamB.Count);
            Int(o, n);
            var cur = new byte[8];
            var next = new byte[8];
            int i = 0;
            while (i < n)
            {
                LinkProtocol.PackInput(t.TeamA[i], cur, 0);
                LinkProtocol.PackInput(t.TeamB[i], cur, 4);
                int run = 1;
                while (i + run < n && run < 65535)
                {
                    LinkProtocol.PackInput(t.TeamA[i + run], next, 0);
                    LinkProtocol.PackInput(t.TeamB[i + run], next, 4);
                    bool same = true;
                    for (int k = 0; k < 8 && same; k++) same = next[k] == cur[k];
                    if (!same) break;
                    run++;
                }
                o.Add((byte)(run & 0xFF));
                o.Add((byte)(run >> 8));
                o.AddRange(cur);
                i += run;
            }
            return o.ToArray();
        }

        /// <summary>The tape in <paramref name="data"/>, or null if it isn't one (or is damaged).</summary>
        public static GameTape Decode(byte[] data)
        {
            try
            {
                if (data == null || data.Length < 4 || data.Length > MaxBytes) return null;
                for (int k = 0; k < 4; k++) if (data[k] != Magic[k]) return null;
                int p = 4;
                var setup = LinkProtocol.ReadSetup(ReadBytes(data, ref p));
                if (setup == null) return null;
                var t = new GameTape
                {
                    Setup = setup,
                    Title = Encoding.UTF8.GetString(ReadBytes(data, ref p)),
                    SavedAt = ReadLong(data, ref p),
                    ScoreA = ReadInt(data, ref p),
                    ScoreB = ReadInt(data, ref p),
                };
                int n = ReadInt(data, ref p);
                if (n < 0 || n > MaxSteps) return null;
                var buf = new byte[8];
                while (t.TeamA.Count < n)
                {
                    if (p + 10 > data.Length) return null;
                    int run = data[p] | (data[p + 1] << 8);
                    p += 2;
                    Array.Copy(data, p, buf, 0, 8);
                    p += 8;
                    if (run <= 0 || t.TeamA.Count + run > n) return null;
                    var a = LinkProtocol.UnpackInput(buf, 0);
                    var b = LinkProtocol.UnpackInput(buf, 4);
                    for (int r = 0; r < run; r++) { t.TeamA.Add(a); t.TeamB.Add(b); }
                }
                return p == data.Length ? t : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A watcher loaded with the whole tape (played back like a game being watched).</summary>
        public static Spectator Player(GameTape t, string appVersion, uint content, ContentCatalog catalog)
        {
            var w = new Spectator(appVersion, content, catalog) { IsTape = true };
            w.Receive(LinkProtocol.WatchSetupMessage(t.Setup));
            for (int i = 0; i < t.Steps; i += 255)
                w.Receive(LinkProtocol.Both(i, t.TeamA, t.TeamB, i, Math.Min(255, t.Steps - i)));
            w.Receive(LinkProtocol.Simple(LinkMessage.Bye));
            return w;
        }

        /// <summary>The list line for a tape: "LIVE · NOVA vs SAL · 52-48 · 6:12".</summary>
        public static string Line(GameTape t)
        {
            int s = (int)t.Seconds;
            return (string.IsNullOrEmpty(t.Title) ? "GAME" : t.Title) + "  ·  " + t.ScoreA + "-" + t.ScoreB + "  ·  " + (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>The title saved with a tape: what kind of game, and who played.</summary>
        public static string TitleFor(LinkSetup s, string homeName, string awayName)
        {
            string kind = s.IsLive ? "LIVE" : s.IsCup ? "COUCH CUP" : "TWO PHONES";
            string a = !string.IsNullOrEmpty(s.HomeLabel) ? s.HomeLabel : s.IsLive ? s.HostName : homeName;
            string b = !string.IsNullOrEmpty(s.AwayLabel) ? s.AwayLabel : s.IsLive ? s.GuestName : awayName;
            return (kind + " · " + (a ?? "HOME") + " vs " + (b ?? "AWAY")).ToUpperInvariant();
        }

        private static void Int(List<byte> o, int v) { o.Add((byte)v); o.Add((byte)(v >> 8)); o.Add((byte)(v >> 16)); o.Add((byte)(v >> 24)); }
        private static void Long(List<byte> o, long v) { Int(o, (int)v); Int(o, (int)(v >> 32)); }
        private static void Bytes(List<byte> o, byte[] b) { Int(o, b.Length); o.AddRange(b); }

        private static int ReadInt(byte[] d, ref int p)
        {
            if (p + 4 > d.Length) throw new FormatException();
            int v = d[p] | (d[p + 1] << 8) | (d[p + 2] << 16) | (d[p + 3] << 24);
            p += 4;
            return v;
        }

        private static long ReadLong(byte[] d, ref int p) => (uint)ReadInt(d, ref p) | ((long)ReadInt(d, ref p) << 32);

        private static byte[] ReadBytes(byte[] d, ref int p)
        {
            int n = ReadInt(d, ref p);
            if (n < 0 || p + n > d.Length) throw new FormatException();
            var b = new byte[n];
            Array.Copy(d, p, b, 0, n);
            p += n;
            return b;
        }
    }

    /// <summary>
    /// Sends one tape to a phone nearby over the TWO PHONES link: the sender hosts, the receiver joins, the
    /// tape goes over in chunks, the receiver checks it and says thanks, and both hang up.
    /// </summary>
    public sealed class TapeTransfer
    {
        public const int Chunk = 32 * 1024;
        public enum State { Waiting = 0, Sending = 1, Done = 2, Failed = 3 }

        public State Status { get; private set; }
        public string Error { get; private set; }
        /// <summary>Receiver: the tape, once it has all arrived.</summary>
        public GameTape Received { get; private set; }
        public float Progress { get; private set; }

        private readonly bool _sender;
        private readonly byte[] _data;
        private int _sent;
        private List<byte> _incoming;
        private int _expected = -1;

        private TapeTransfer(bool sender, byte[] data) { _sender = sender; _data = data; }

        public static TapeTransfer Send(GameTape t) => new TapeTransfer(true, Tapes.Encode(t));
        public static TapeTransfer Receive() => new TapeTransfer(false, null);

        /// <summary>Message: [Tape][total length (4)][offset (4)][bytes].</summary>
        public void Update(ILinkTransport t)
        {
            if (Status == State.Done || Status == State.Failed) return;
            if (t.State == LinkState.Lost) { Fail("Lost the connection to the other phone."); return; }
            if (t.State != LinkState.Connected) return;
            if (_sender)
            {
                Status = State.Sending;
                // A few chunks a frame keeps the link busy without flooding it.
                for (int k = 0; k < 4 && _sent < _data.Length; k++)
                {
                    int n = Math.Min(Chunk, _data.Length - _sent);
                    var m = new byte[9 + n];
                    m[0] = (byte)LinkMessage.Tape;
                    LinkProtocol.WriteInt(m, 1, _data.Length);
                    LinkProtocol.WriteInt(m, 5, _sent);
                    Array.Copy(_data, _sent, m, 9, n);
                    t.Send(m);
                    _sent += n;
                }
                Progress = _data.Length == 0 ? 1f : (float)_sent / _data.Length;
            }
            while (t.TryReceive(out var msg))
            {
                if (msg == null || msg.Length == 0) continue;
                var type = (LinkMessage)msg[0];
                if (_sender)
                {
                    if (type == LinkMessage.Ready && _sent >= _data.Length) Status = State.Done;
                    else if (type == LinkMessage.Reject) Fail(LinkProtocol.ReadText(msg));
                    continue;
                }
                if (type == LinkMessage.Setup) { Fail("That phone is hosting a game, not sending a tape."); return; }
                if (type != LinkMessage.Tape || msg.Length < 9) continue;
                int total = LinkProtocol.ReadInt(msg, 1), offset = LinkProtocol.ReadInt(msg, 5);
                if (total <= 0 || total > Tapes.MaxBytes) { t.Send(LinkProtocol.Text(LinkMessage.Reject, "Tape too big.")); Fail("The tape was too big."); return; }
                if (_expected < 0) { _expected = total; _incoming = new List<byte>(total); }
                if (total != _expected || offset != _incoming.Count) { t.Send(LinkProtocol.Text(LinkMessage.Reject, "Tape arrived out of order.")); Fail("The tape arrived damaged. Try again."); return; }
                for (int i = 9; i < msg.Length; i++) _incoming.Add(msg[i]);
                Progress = (float)_incoming.Count / _expected;
                if (_incoming.Count >= _expected)
                {
                    var tape = Tapes.Decode(_incoming.ToArray());
                    if (tape == null) { t.Send(LinkProtocol.Text(LinkMessage.Reject, "Damaged tape.")); Fail("The tape arrived damaged. Try again."); return; }
                    Received = tape;
                    t.Send(LinkProtocol.Simple(LinkMessage.Ready));
                    Status = State.Done;
                    return;
                }
            }
        }

        private void Fail(string why)
        {
            Error = why;
            Status = State.Failed;
        }
    }
}
