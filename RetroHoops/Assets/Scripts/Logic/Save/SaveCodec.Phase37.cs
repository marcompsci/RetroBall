using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // Phase 37: CLUTCH scenario stars.
        private static object EncodeClutch(ClutchSaveData d)
        {
            d = d ?? new ClutchSaveData();
            return new Dictionary<string, object> { ["stars"] = Strings(d.stars), ["played"] = d.played, ["won"] = d.won,
                ["dDay"] = d.dailyDay, ["dStreak"] = d.dailyStreak, ["dBest"] = d.dailyBest };
        }

        private static ClutchSaveData DecodeClutch(Dictionary<string, object> o)
        {
            var d = new ClutchSaveData();
            if (o == null) return d;
            d.stars = StrList(o, "stars");
            d.played = Int(o, "played", 0);
            d.won = Int(o, "won", 0);
            d.dailyDay = Int(o, "dDay", -1);
            d.dailyStreak = Int(o, "dStreak", 0);
            d.dailyBest = Int(o, "dBest", 0);
            Clutch.Sanitize(d);
            return d;
        }
    }
}
