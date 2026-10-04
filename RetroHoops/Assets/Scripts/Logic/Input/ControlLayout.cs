using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>The on-screen action buttons, in drawing order.</summary>
    public enum ControlButton { Shoot = 0, Pass = 1, Dunk = 2, Layup = 3, Call = 4 }

    /// <summary>One button's place: offset from the lower-right corner of the safe area (canvas units) and size.</summary>
    public struct ControlSlot
    {
        public ControlButton Button;
        public float X, Y;
        public float Size;

        public ControlSlot(ControlButton b, float x, float y, float size)
        {
            Button = b;
            X = x;
            Y = y;
            Size = size;
        }
    }

    /// <summary>
    /// The touch button layout: a thumb arc in the lower-right corner (SHOOT biggest on the outside,
    /// PASS beside it, DUNK above, LAYUP on the diagonal, CALL further in), and the player's own
    /// arrangement from Settings ▸ CUSTOMIZE CONTROLS, saved as a short string.
    /// </summary>
    public static class ControlLayout
    {
        public const float MinScale = 0.7f;
        public const float MaxScale = 1.5f;
        /// <summary>Furthest a button may sit from the corner (canvas units), so it can't be lost off screen.</summary>
        public const float MaxReachX = 1040f, MaxReachY = 1000f;

        public static ControlSlot[] Default() => new[]
        {
            new ControlSlot(ControlButton.Shoot, -190f, 215f, 250f),
            new ControlSlot(ControlButton.Pass, -455f, 140f, 190f),
            new ControlSlot(ControlButton.Dunk, -160f, 480f, 175f),
            new ControlSlot(ControlButton.Layup, -405f, 375f, 150f),
            new ControlSlot(ControlButton.Call, -640f, 285f, 120f),
        };

        public static float DefaultSize(ControlButton b) => Default()[(int)b].Size;

        /// <summary>Keeps a slot on screen and within the size limits.</summary>
        public static ControlSlot Clamp(ControlSlot s)
        {
            float baseSize = DefaultSize(s.Button);
            s.Size = Math.Max(baseSize * MinScale, Math.Min(baseSize * MaxScale, s.Size));
            float half = s.Size * 0.5f;
            s.X = Math.Max(-MaxReachX, Math.Min(-half, s.X));
            s.Y = Math.Max(half, Math.Min(MaxReachY, s.Y));
            return s;
        }

        /// <summary>"button:x,y,size;..." (invariant culture). Only buttons that differ from the default are written.</summary>
        public static string Serialize(ControlSlot[] slots)
        {
            if (slots == null) return "";
            var d = Default();
            var sb = new StringBuilder();
            foreach (var raw in slots)
            {
                var s = Clamp(raw);
                var def = d[(int)s.Button];
                if (Math.Abs(s.X - def.X) < 0.5f && Math.Abs(s.Y - def.Y) < 0.5f && Math.Abs(s.Size - def.Size) < 0.5f) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append((int)s.Button).Append(':')
                  .Append(Math.Round(s.X).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Math.Round(s.Y).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Math.Round(s.Size).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>The default layout with any saved changes applied. Bad entries are ignored.</summary>
        public static ControlSlot[] Parse(string saved)
        {
            var slots = Default();
            if (string.IsNullOrEmpty(saved)) return slots;
            foreach (var part in saved.Split(';'))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (!int.TryParse(part.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
                if (id < 0 || id >= slots.Length) continue;
                var nums = part.Substring(colon + 1).Split(',');
                if (nums.Length != 3) continue;
                if (!float.TryParse(nums[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(nums[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                if (!float.TryParse(nums[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float size)) continue;
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(size)) continue;
                slots[id] = Clamp(new ControlSlot((ControlButton)id, x, y, size));
            }
            return slots;
        }

        /// <summary>True when two buttons overlap by more than a sliver (the editor warns about it).</summary>
        public static bool Overlaps(ControlSlot a, ControlSlot b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            float r = (a.Size + b.Size) * 0.5f * 0.9f;
            return dx * dx + dy * dy < r * r;
        }

        public static List<ControlButton> Overlapping(ControlSlot[] slots)
        {
            var list = new List<ControlButton>();
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                    if (Overlaps(slots[i], slots[j]))
                    {
                        if (!list.Contains(slots[i].Button)) list.Add(slots[i].Button);
                        if (!list.Contains(slots[j].Button)) list.Add(slots[j].Button);
                    }
            return list;
        }
    }
}
