using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum CloudChoice
    {
        /// <summary>Nothing to do (no cloud copy, or it isn't ahead).</summary>
        KeepLocal = 0,
        /// <summary>The cloud copy is further along: load it (keeping this device's settings).</summary>
        UseCloud = 1,
    }

    /// <summary>
    /// iCloud save sync (Settings ▸ iCloud Sync), engine-free and testable. The career is stored as one
    /// value in the player's own iCloud key-value storage, wrapped with a little metadata. Conflict rule:
    /// the career that's further along wins (more games played, then more story, badges and codes), so a
    /// device that was offline can't roll your progress back. This device's settings always stay.
    /// </summary>
    public static class CloudSave
    {
        public const string Key = "retroball.career.v1";
        /// <summary>Apple's key-value store allows 1 MB in total; stay well under it.</summary>
        public const int MaxChars = 900_000;
        public const int FormatVersion = 1;

        /// <summary>The value written to iCloud, or null when the save is too big to sync.</summary>
        public static string Wrap(CareerSaveData d, long savedAtUnix, string deviceName)
        {
            if (d == null) return null;
            var o = new Dictionary<string, object>
            {
                ["v"] = FormatVersion,
                ["savedAt"] = (double)savedAtUnix,
                ["device"] = deviceName ?? "",
                ["games"] = d.totals.games,
                ["data"] = SaveCodec.Encode(d),
            };
            string json = MiniJson.Write(o, false);
            return json.Length <= MaxChars ? json : null;
        }

        /// <summary>Reads a wrapped cloud value. False for anything missing, unreadable or from a newer format.</summary>
        public static bool TryUnwrap(string value, ContentCatalog c, out CareerSaveData data, out long savedAtUnix, out string deviceName)
        {
            data = null;
            savedAtUnix = 0;
            deviceName = null;
            if (string.IsNullOrEmpty(value)) return false;
            try
            {
                if (!(MiniJson.Read(value) is Dictionary<string, object> o)) return false;
                if (!o.TryGetValue("v", out var v) || !(v is double ver) || (int)ver != FormatVersion) return false;
                if (!o.TryGetValue("data", out var raw) || !(raw is string json)) return false;
                var d = SaveCodec.Decode(json, c, out var status);
                if (status == LoadStatus.Recovered || status == LoadStatus.New) return false;
                data = d;
                if (o.TryGetValue("savedAt", out var at) && at is double t) savedAtUnix = (long)t;
                if (o.TryGetValue("device", out var dev) && dev is string name) deviceName = name;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>How far along a career is, compared item by item (games first).</summary>
        public static int[] Progress(CareerSaveData d)
        {
            if (d == null) return new[] { -1, -1, -1, -1, -1 };
            return new[]
            {
                d.totals?.games ?? 0,
                d.storySeen?.Count ?? 0,
                d.badgesSeen?.Count ?? 0,
                d.secrets?.codesFound?.Count ?? 0,
                d.tutorialDone ? 1 : 0,
            };
        }

        /// <summary>Negative when <paramref name="a"/> is behind <paramref name="b"/>, 0 when level.</summary>
        public static int Compare(CareerSaveData a, CareerSaveData b)
        {
            var pa = Progress(a);
            var pb = Progress(b);
            for (int i = 0; i < pa.Length; i++)
                if (pa[i] != pb[i]) return pa[i] < pb[i] ? -1 : 1;
            return 0;
        }

        public static CloudChoice Choose(CareerSaveData local, CareerSaveData cloud) =>
            cloud != null && Compare(cloud, local) > 0 ? CloudChoice.UseCloud : CloudChoice.KeepLocal;

        /// <summary>Takes the cloud career but keeps this device's settings (volume, controls, language...).</summary>
        public static CareerSaveData Adopt(CareerSaveData cloud, CareerSaveData local)
        {
            if (cloud == null) return local;
            if (local != null && local.settings != null) cloud.settings = local.settings;
            // iCloud copies aren't sealed (each device has its own key): keep their numbers in range.
            SaveIntegrity.Clamp(cloud);
            return cloud;
        }
    }
}
