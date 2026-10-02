using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Utilities
{
    /// <summary>Boundary conversions between engine-free RgbColor and Unity colours.</summary>
    public static class ColorConvert
    {
        public static Color32 ToColor32(this RgbColor c) => new Color32(c.r, c.g, c.b, c.a);
        public static Color ToColor(this RgbColor c) => new Color32(c.r, c.g, c.b, c.a);
        public static RgbColor ToRgb(this Color32 c) => new RgbColor(c.r, c.g, c.b, c.a);
    }
}
