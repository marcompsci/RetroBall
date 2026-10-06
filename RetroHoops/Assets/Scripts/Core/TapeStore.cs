using System;
using System.Collections.Generic;
using System.IO;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>GAME TAPES on this phone (Phase 31): one small file per tape, newest kept, oldest dropped past 12.</summary>
    public static class TapeStore
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "tapes");

        /// <summary>The tape being watched right now (for WATCH AGAIN).</summary>
        public static GameTape Playing;
        /// <summary>Phase 35: the file the tape being watched came from (so new marks can be saved into it), or null.</summary>
        public static string PlayingPath;
        /// <summary>Phase 35: the replay theater for <see cref="Playing"/> (kept across a jump back, which restarts the scene).</summary>
        public static TapeTheater Theater;
        /// <summary>Phase 35: where the next start of <see cref="Playing"/> begins (a jump back), in steps.</summary>
        public static int StartAt;

        /// <summary>Writes <paramref name="t"/> back over its own file (its marks changed).</summary>
        public static bool Overwrite(string path, GameTape t)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                File.WriteAllBytes(path, Tapes.Encode(t));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tapes] Couldn't save the marks: " + e.Message);
                return false;
            }
        }

        public struct Entry
        {
            public string Path;
            public GameTape Tape;
        }

        public static List<Entry> List()
        {
            var list = new List<Entry>();
            try
            {
                if (!Directory.Exists(Folder)) return list;
                foreach (var f in Directory.GetFiles(Folder, "*.rht"))
                {
                    var t = Tapes.Decode(File.ReadAllBytes(f));
                    if (t != null) list.Add(new Entry { Path = f, Tape = t });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tapes] Couldn't list tapes: " + e.Message);
            }
            list.Sort((x, y) => y.Tape.SavedAt.CompareTo(x.Tape.SavedAt));
            return list;
        }

        public static bool Save(GameTape t)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string name = "tape-" + t.SavedAt + "-" + UnityEngine.Random.Range(1000, 9999) + ".rht";
                File.WriteAllBytes(Path.Combine(Folder, name), Tapes.Encode(t));
                var all = List();
                for (int i = Tapes.MaxSaved; i < all.Count; i++) Delete(all[i].Path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Tapes] Couldn't save the tape: " + e.Message);
                return false;
            }
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tapes] Couldn't delete: " + e.Message);
            }
        }

        /// <summary>Sets up the game scene to play <paramref name="tape"/>; false (with a reason) if it can't play here.</summary>
        public static bool PrepareToWatch(GameTape tape, out string why) => PrepareToWatch(tape, PlayingPath, out why);

        public static bool PrepareToWatch(GameTape tape, string path, out string why)
        {
            why = null;
            var c = App.Catalog;
            var w = Tapes.Player(tape, App.Version, LinkProtocol.ContentFingerprint(c), c);
            if (!w.Ready)
            {
                why = "This tape was recorded on a different version of Retro Hoops, so it can't be played on this one.";
                return false;
            }
            if (!w.Setup.Register(c))
            {
                why = "Couldn't read the teams on this tape.";
                return false;
            }
            if (Playing != tape)
            {
                Theater = null;
                StartAt = 0;
            }
            Playing = tape;
            PlayingPath = path;
            LinkMatch.Transport = new TapeWire();
            LinkMatch.Seat = 1;
            LinkMatch.Setup = w.Setup;
            LinkMatch.Watching = w;
            App.PendingMatch = w.Setup.ToRequest();
            return true;
        }

        /// <summary>The "connection" while a tape plays: everything is already here.</summary>
        private sealed class TapeWire : ILinkTransport
        {
            public LinkState State => LinkState.Connected;
            public string PeerName => "";
            public void Send(byte[] message) { }
            public bool TryReceive(out byte[] message) { message = null; return false; }
        }
    }
}
