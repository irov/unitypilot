using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pilot.SDK
{
    /// <summary>
    /// Lightweight JSON helper for the Pilot SDK.
    /// Wraps Dictionary for object access and provides serialization.
    /// </summary>
    public sealed class SimpleJson
    {
        private readonly Dictionary<string, object> m_data;

        public SimpleJson()
        {
            m_data = new Dictionary<string, object>();
        }

        public SimpleJson(Dictionary<string, object> data)
        {
            m_data = data ?? new Dictionary<string, object>();
        }

        public void Put(string key, object value)
        {
            m_data[key] = value;
        }

        public object Get(string key)
        {
            m_data.TryGetValue(key, out var value);
            return value;
        }

        public string GetString(string key, string defaultValue = null)
        {
            if (m_data.TryGetValue(key, out var value) && value is string s)
                return s;
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (m_data.TryGetValue(key, out var value))
            {
                if (value is int i) return i;
                if (value is long l) return (int)l;
                if (value is double d) return (int)d;
                if (value is float f) return (int)f;
                if (value is string s && int.TryParse(s, out int parsed)) return parsed;
            }
            return defaultValue;
        }

        public long GetLong(string key, long defaultValue = 0)
        {
            if (m_data.TryGetValue(key, out var value))
            {
                if (value is long l) return l;
                if (value is int i) return i;
                if (value is double d) return (long)d;
                if (value is string s && long.TryParse(s, out long parsed)) return parsed;
            }
            return defaultValue;
        }

        public double GetDouble(string key, double defaultValue = 0.0)
        {
            if (m_data.TryGetValue(key, out var value))
            {
                if (value is double d) return d;
                if (value is float f) return f;
                if (value is int i) return i;
                if (value is long l) return l;
                if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            }
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (m_data.TryGetValue(key, out var value))
            {
                if (value is bool b) return b;
            }
            return defaultValue;
        }

        public SimpleJson GetObject(string key)
        {
            if (m_data.TryGetValue(key, out var value))
            {
                if (value is SimpleJson sj) return sj;
                if (value is Dictionary<string, object> dict) return new SimpleJson(dict);
            }
            return null;
        }

        public List<object> GetArray(string key)
        {
            if (m_data.TryGetValue(key, out var value) && value is List<object> list)
                return list;
            return null;
        }

        public bool IsNull(string key)
        {
            if (!m_data.TryGetValue(key, out var value))
                return true;
            return value == null;
        }

        public bool ContainsKey(string key) => m_data.ContainsKey(key);
        public int Count => m_data.Count;

        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(m_data);

        public override string ToString()
        {
            return Serialize(m_data);
        }

        // ── Serialization ──

        public static string Serialize(object obj)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, obj);
            return sb.ToString();
        }

        private static void SerializeValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string s)
            {
                SerializeString(sb, s);
            }
            else if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else if (value is int || value is long)
            {
                sb.Append(value);
            }
            else if (value is float f)
            {
                if (float.IsNaN(f) || float.IsInfinity(f))
                    sb.Append("null");
                else
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                    sb.Append("null");
                else
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is SimpleJson sj)
            {
                SerializeValue(sb, sj.m_data);
            }
            else if (value is Dictionary<string, object> dict)
            {
                SerializeDict(sb, dict);
            }
            else if (value is IList list)
            {
                SerializeList(sb, list);
            }
            else
            {
                SerializeString(sb, value.ToString());
            }
        }

        private static void SerializeDict(StringBuilder sb, Dictionary<string, object> dict)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                SerializeString(sb, kvp.Key);
                sb.Append(':');
                SerializeValue(sb, kvp.Value);
            }
            sb.Append('}');
        }

        private static void SerializeList(StringBuilder sb, IList list)
        {
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                SerializeValue(sb, list[i]);
            }
            sb.Append(']');
        }

        private static void SerializeString(StringBuilder sb, string str)
        {
            sb.Append('"');
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ── Deserialization ──

        public static SimpleJson Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new SimpleJson();

            int index = 0;
            var result = ParseValue(json, ref index);
            if (result is Dictionary<string, object> dict)
                return new SimpleJson(dict);

            return new SimpleJson();
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);

            if (index >= json.Length)
                return null;

            char c = json[index];

            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            return ParseNumber(json, ref index);
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            index++; // skip '{'
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                return dict;
            }

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);

                if (index < json.Length && json[index] == ':')
                    index++;

                object value = ParseValue(json, ref index);
                dict[key] = value;

                SkipWhitespace(json, ref index);

                if (index < json.Length && json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (index < json.Length && json[index] == '}')
                {
                    index++;
                    break;
                }

                break;
            }

            return dict;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            index++; // skip '['
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']')
            {
                index++;
                return list;
            }

            while (index < json.Length)
            {
                list.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);

                if (index < json.Length && json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (index < json.Length && json[index] == ']')
                {
                    index++;
                    break;
                }

                break;
            }

            return list;
        }

        private static string ParseString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
                return "";

            index++; // skip opening quote
            var sb = new StringBuilder();

            while (index < json.Length)
            {
                char c = json[index];
                if (c == '"')
                {
                    index++;
                    return sb.ToString();
                }

                if (c == '\\' && index + 1 < json.Length)
                {
                    index++;
                    char esc = json[index];
                    switch (esc)
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
                            if (index + 4 < json.Length)
                            {
                                string hex = json.Substring(index + 1, 4);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                    sb.Append((char)code);
                                index += 4;
                            }
                            break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }

                index++;
            }

            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            bool isFloat = false;

            if (index < json.Length && json[index] == '-')
                index++;

            while (index < json.Length && char.IsDigit(json[index]))
                index++;

            if (index < json.Length && json[index] == '.')
            {
                isFloat = true;
                index++;
                while (index < json.Length && char.IsDigit(json[index]))
                    index++;
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                isFloat = true;
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                    index++;
                while (index < json.Length && char.IsDigit(json[index]))
                    index++;
            }

            string numStr = json.Substring(start, index - start);

            if (isFloat)
            {
                if (double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                    return d;
                return 0.0;
            }

            if (long.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out long l))
            {
                if (l >= int.MinValue && l <= int.MaxValue)
                    return (int)l;
                return l;
            }

            return 0;
        }

        private static bool ParseBool(string json, ref int index)
        {
            if (json.Length - index >= 4 && json.Substring(index, 4) == "true")
            {
                index += 4;
                return true;
            }
            if (json.Length - index >= 5 && json.Substring(index, 5) == "false")
            {
                index += 5;
                return false;
            }
            return false;
        }

        private static object ParseNull(string json, ref int index)
        {
            if (json.Length - index >= 4 && json.Substring(index, 4) == "null")
            {
                index += 4;
                return null;
            }
            return null;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }
    }
}
