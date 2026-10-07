using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>Phase 31: turns what's drawn on a button into what VoiceOver should say.</summary>
    public static class ScreenReaderText
    {
        /// <summary>Strips TextMeshPro rich-text tags, joins lines, and reads separators as pauses.</summary>
        public static string Plain(string richText)
        {
            if (string.IsNullOrEmpty(richText)) return "";
            var sb = new StringBuilder(richText.Length);
            bool inTag = false;
            foreach (char ch in richText)
            {
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>' && inTag) { inTag = false; continue; }
                if (inTag) continue;
                if (ch == '\n' || ch == '·' || ch == '►' || ch == '•') { sb.Append(", "); continue; }
                sb.Append(ch);
            }
            // Collapse runs of spaces and stray commas.
            string s = sb.ToString();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            while (s.Contains(", ,")) s = s.Replace(", ,", ",");
            s = s.Replace(" ,", ",");
            return s.Trim(' ', ',');
        }

        /// <summary>A readable name for a control with no text, from its object name ("Button BACK" → "BACK").</summary>
        public static string FromName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return "Button";
            string n = objectName.Replace("(Clone)", "").Trim();
            if (n.StartsWith("Button ")) n = n.Substring(7);
            return n.Length == 0 ? "Button" : n;
        }

        /// <summary>
        /// The UI scale to use when following the iPhone's text size: never smaller than normal, and capped at the
        /// largest scale the menus are laid out for.
        /// </summary>
        public static float UiScaleForSystemText(float fontScale)
        {
            if (float.IsNaN(fontScale) || fontScale <= 1.05f) return 1f;
            if (fontScale <= 1.2f) return 1.15f;
            return 1.25f;
        }
    }
}
