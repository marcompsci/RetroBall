using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // Phase 39: the welcome-back bonus and the controls feel settings.
        private static object EncodeLogin(CareerSaveData d) =>
            new Dictionary<string, object> { ["day"] = d.loginDay, ["streak"] = d.loginStreak, ["best"] = d.loginBest };

        private static void DecodeLogin(CareerSaveData d, Dictionary<string, object> o)
        {
            if (o == null) return;
            d.loginDay = Math.Max(-1, Int(o, "day", -1));
            d.loginStreak = Math.Max(0, Int(o, "streak", 0));
            d.loginBest = Math.Max(d.loginStreak, Int(o, "best", 0));
        }

        private static object EncodeFeel(SettingsData s)
        {
            s = s ?? new SettingsData();
            return new Dictionary<string, object> { ["stick"] = s.stickSize, ["dead"] = s.stickDeadZone, ["haptic"] = s.hapticStrength };
        }

        private static void DecodeFeel(SettingsData s, Dictionary<string, object> o)
        {
            if (s == null || o == null) return;
            s.stickSize = ControlFeel.Clamp(Int(o, "stick", 1));
            s.stickDeadZone = ControlFeel.Clamp(Int(o, "dead", 1));
            s.hapticStrength = ControlFeel.Clamp(Int(o, "haptic", 1));
        }
    }
}
