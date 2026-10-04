using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        private static object EncodeWeekly(WeeklySaveData w)
        {
            w = w ?? new WeeklySaveData();
            var done = new List<object>();
            foreach (var d in w.done ?? new List<bool>()) done.Add(d);
            var progress = new List<object>();
            foreach (var p in w.progress ?? new List<int>()) progress.Add(p);
            return new Dictionary<string, object>
            {
                ["week"] = w.week, ["progress"] = progress, ["done"] = done, ["completed"] = w.completed, ["perfect"] = w.perfectWeeks,
            };
        }

        private static WeeklySaveData DecodeWeekly(Dictionary<string, object> o)
        {
            var w = new WeeklySaveData();
            if (o == null) return w;
            w.week = Int(o, "week", -1);
            int i = 0;
            foreach (var item in Arr(o, "progress"))
            {
                if (i >= Weekly.Goals) break;
                w.progress[i++] = item is double d ? Math.Max(0, (int)d) : 0;
            }
            i = 0;
            foreach (var item in Arr(o, "done"))
            {
                if (i >= Weekly.Goals) break;
                w.done[i++] = item is bool b && b;
            }
            w.completed = Math.Max(0, Int(o, "completed", 0));
            w.perfectWeeks = Math.Max(0, Int(o, "perfect", 0));
            return w;
        }

        private static object EncodePass(PassSaveData p)
        {
            p = p ?? new PassSaveData();
            return new Dictionary<string, object>
            {
                ["season"] = p.season, ["xp"] = p.xp, ["granted"] = p.granted, ["maxed"] = p.seasonsMaxed, ["dailyDay"] = p.dailyXpDay,
            };
        }

        private static PassSaveData DecodePass(Dictionary<string, object> o)
        {
            var p = new PassSaveData();
            if (o == null) return p;
            p.season = Int(o, "season", -1);
            p.xp = Math.Max(0, Math.Min(HoopsPass.Tiers * HoopsPass.XpPerTier, Int(o, "xp", 0)));
            p.granted = Math.Max(0, Math.Min(HoopsPass.Tiers, Int(o, "granted", 0)));
            p.seasonsMaxed = Math.Max(0, Int(o, "maxed", 0));
            p.dailyXpDay = Int(o, "dailyDay", -1);
            return p;
        }
    }
}
