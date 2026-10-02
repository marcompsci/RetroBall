using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Small, dependency-free JSON reader/writer for save files. Values map to
    /// Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool, or null.
    /// Numbers always use the invariant culture.
    /// </summary>
    public static class MiniJson
    {
        public sealed class JsonException : Exception
        {
            public JsonException(string message) : base(message) { }
        }

        // ------------------------------------------------------------------ write

        public static string Write(object value, bool pretty = true)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, pretty, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object v, bool pretty, int indent)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(FormatNumber(d)); break;
                case float f: sb.Append(FormatNumber(f)); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case uint u: sb.Append(u.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> obj:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in obj)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, pretty, indent + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(pretty ? ": " : ":");
                        WriteValue(sb, kv.Value, pretty, indent + 1);
                    }
                    if (!first) NewLine(sb, pretty, indent);
                    sb.Append('}');
                    break;
                }
                case IList<object> list:
                {
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        NewLine(sb, pretty, indent + 1);
                        WriteValue(sb, list[i], pretty, indent + 1);
                    }
                    if (list.Count > 0) NewLine(sb, pretty, indent);
                    sb.Append(']');
                    break;
                }
                default: throw new JsonException("Unsupported JSON value type " + v.GetType().Name);
            }
        }

        private static string FormatNumber(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) return "0";
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void NewLine(StringBuilder sb, bool pretty, int indent)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ read

        public static object Read(string json)
        {
            if (json == null) throw new JsonException("No JSON text.");
            int i = 0;
            var v = ParseValue(json, ref i, 0);
            SkipWs(json, ref i);
            if (i != json.Length) throw new JsonException("Unexpected trailing characters at " + i + ".");
            return v;
        }

        private const int MaxDepth = 64;

        private static object ParseValue(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new JsonException("JSON nested too deeply.");
            SkipWs(s, ref i);
            if (i >= s.Length) throw new JsonException("Unexpected end of JSON.");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i, depth);
            if (c == '[') return ParseArray(s, ref i, depth);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') return Literal(s, ref i, "true", true);
            if (c == 'f') return Literal(s, ref i, "false", false);
            if (c == 'n') return Literal(s, ref i, "null", null);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref i);
            throw new JsonException("Unexpected character '" + c + "' at " + i + ".");
        }

        private static object Literal(string s, ref int i, string word, object value)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new JsonException("Bad literal at " + i + ".");
            i += word.Length;
            return value;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i, int depth)
        {
            var obj = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new JsonException("Expected a key at " + i + ".");
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new JsonException("Expected ':' at " + i + ".");
                i++;
                obj[key] = ParseValue(s, ref i, depth + 1);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new JsonException("Unterminated object.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return obj; }
                throw new JsonException("Expected ',' or '}' at " + i + ".");
            }
        }

        private static List<object> ParseArray(string s, ref int i, int depth)
        {
            var list = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i, depth + 1));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new JsonException("Unterminated array.");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw new JsonException("Expected ',' or ']' at " + i + ".");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new JsonException("Bad unicode escape.");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw new JsonException("Bad escape '\\" + e + "'.");
                }
            }
            throw new JsonException("Unterminated string.");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                throw new JsonException("Bad number at " + start + ".");
            return d;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }
    }
}
