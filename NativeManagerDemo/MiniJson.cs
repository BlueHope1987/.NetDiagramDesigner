using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NativeManagerDemo
{
    /// <summary>无第三方依赖的小型 JSON 读取/写入器，供 kubectl 与设计文件使用。</summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            JsonReader reader = new JsonReader(text);
            object value = reader.ReadValue(0);
            reader.SkipWhiteSpace();
            if (!reader.End) throw reader.Error("JSON 末尾存在多余内容");
            return value;
        }

        public static string Stringify(object value)
        {
            StringBuilder text = new StringBuilder();
            WriteValue(text, value, 0);
            return text.ToString();
        }

        public static IDictionary<string, object> Object(object value)
        {
            return value as IDictionary<string, object>;
        }

        public static IList<object> Array(object value)
        {
            return value as IList<object>;
        }

        public static object Get(object value, string name)
        {
            IDictionary<string, object> map = Object(value);
            object result;
            if (map != null && map.TryGetValue(name, out result)) return result;
            return null;
        }

        public static string String(object value, string name, string fallback)
        {
            object item = Get(value, name);
            if (item == null) return fallback;
            return Convert.ToString(item, CultureInfo.InvariantCulture);
        }

        public static int Int32(object value, string name, int fallback)
        {
            object item = Get(value, name);
            if (item == null) return fallback;
            try { return Convert.ToInt32(item, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        public static bool Boolean(object value, string name, bool fallback)
        {
            object item = Get(value, name);
            if (item == null) return fallback;
            try { return Convert.ToBoolean(item, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static void WriteValue(StringBuilder text, object value, int depth)
        {
            if (depth > 64) throw new FormatException("JSON 嵌套层级超过 64 层");
            if (value == null) { text.Append("null"); return; }
            if (value is string) { WriteString(text, (string)value); return; }
            if (value is char) { WriteString(text, value.ToString()); return; }
            if (value is bool) { text.Append((bool)value ? "true" : "false"); return; }
            if (value is DateTime) { WriteString(text, ((DateTime)value).ToString("o")); return; }
            if (value is Enum) { WriteString(text, value.ToString()); return; }
            if (value is double)
            {
                double number = (double)value;
                if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("JSON 不支持非有限数字");
                text.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is float || value is decimal || value is byte || value is short ||
                value is int || value is long || value is ushort || value is uint || value is ulong)
            {
                text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                text.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first) text.Append(',');
                    first = false;
                    WriteString(text, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                    text.Append(':');
                    WriteValue(text, entry.Value, depth + 1);
                }
                text.Append('}');
                return;
            }

            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                text.Append('[');
                bool first = true;
                foreach (object item in sequence)
                {
                    if (!first) text.Append(',');
                    first = false;
                    WriteValue(text, item, depth + 1);
                }
                text.Append(']');
                return;
            }
            throw new FormatException("不支持的 JSON 值类型: " + value.GetType().FullName);
        }

        private static void WriteString(StringBuilder text, string value)
        {
            text.Append('"');
            int i;
            for (i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\b': text.Append("\\b"); break;
                    case '\f': text.Append("\\f"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 32) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }

        private sealed class JsonReader
        {
            private readonly string _text;
            private int _position;

            internal JsonReader(string text)
            {
                _text = text;
                _position = 0;
            }

            internal bool End { get { return _position >= _text.Length; } }

            internal void SkipWhiteSpace()
            {
                while (!End && char.IsWhiteSpace(_text[_position])) _position++;
            }

            internal FormatException Error(string message)
            {
                return new FormatException(message + "（位置 " + _position.ToString(CultureInfo.InvariantCulture) + "）");
            }

            internal object ReadValue(int depth)
            {
                if (depth > 64) throw Error("JSON 嵌套层级超过 64 层");
                SkipWhiteSpace();
                if (End) throw Error("JSON 意外结束");
                char c = _text[_position];
                if (c == '{') return ReadObject(depth + 1);
                if (c == '[') return ReadArray(depth + 1);
                if (c == '"') return ReadString();
                if (c == 't') { ReadLiteral("true"); return true; }
                if (c == 'f') { ReadLiteral("false"); return false; }
                if (c == 'n') { ReadLiteral("null"); return null; }
                if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                throw Error("无法识别 JSON 值");
            }

            private Dictionary<string, object> ReadObject(int depth)
            {
                Dictionary<string, object> map = new Dictionary<string, object>();
                _position++;
                SkipWhiteSpace();
                if (!End && _text[_position] == '}') { _position++; return map; }
                while (true)
                {
                    SkipWhiteSpace();
                    if (End || _text[_position] != '"') throw Error("JSON 对象键必须是字符串");
                    string key = ReadString();
                    SkipWhiteSpace();
                    if (End || _text[_position] != ':') throw Error("JSON 对象键后缺少冒号");
                    _position++;
                    map[key] = ReadValue(depth);
                    SkipWhiteSpace();
                    if (End) throw Error("JSON 对象未闭合");
                    char delimiter = _text[_position++];
                    if (delimiter == '}') return map;
                    if (delimiter != ',') throw Error("JSON 对象成员之间缺少逗号");
                }
            }

            private List<object> ReadArray(int depth)
            {
                List<object> list = new List<object>();
                _position++;
                SkipWhiteSpace();
                if (!End && _text[_position] == ']') { _position++; return list; }
                while (true)
                {
                    list.Add(ReadValue(depth));
                    SkipWhiteSpace();
                    if (End) throw Error("JSON 数组未闭合");
                    char delimiter = _text[_position++];
                    if (delimiter == ']') return list;
                    if (delimiter != ',') throw Error("JSON 数组成员之间缺少逗号");
                }
            }

            private string ReadString()
            {
                StringBuilder value = new StringBuilder();
                _position++;
                while (!End)
                {
                    char c = _text[_position++];
                    if (c == '"') return value.ToString();
                    if (c < 32) throw Error("JSON 字符串包含未转义控制字符");
                    if (c != '\\') { value.Append(c); continue; }
                    if (End) throw Error("JSON 转义序列意外结束");
                    char escape = _text[_position++];
                    switch (escape)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u': value.Append(ReadUnicode()); break;
                        default: throw Error("未知 JSON 转义序列");
                    }
                }
                throw Error("JSON 字符串未闭合");
            }

            private char ReadUnicode()
            {
                if (_position + 4 > _text.Length) throw Error("JSON Unicode 转义不完整");
                string digits = _text.Substring(_position, 4);
                int value;
                if (!int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    throw Error("JSON Unicode 转义无效");
                _position += 4;
                return (char)value;
            }

            private void ReadLiteral(string literal)
            {
                if (_position + literal.Length > _text.Length ||
                    string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
                    throw Error("JSON 字面量无效");
                _position += literal.Length;
            }

            private object ReadNumber()
            {
                int start = _position;
                if (_text[_position] == '-') _position++;
                if (End) throw Error("JSON 数字不完整");
                if (_text[_position] == '0') _position++;
                else
                {
                    if (_text[_position] < '1' || _text[_position] > '9') throw Error("JSON 数字无效");
                    while (!End && char.IsDigit(_text[_position])) _position++;
                }
                bool integer = true;
                if (!End && _text[_position] == '.')
                {
                    integer = false;
                    _position++;
                    if (End || !char.IsDigit(_text[_position])) throw Error("JSON 小数缺少数字");
                    while (!End && char.IsDigit(_text[_position])) _position++;
                }
                if (!End && (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    integer = false;
                    _position++;
                    if (!End && (_text[_position] == '+' || _text[_position] == '-')) _position++;
                    if (End || !char.IsDigit(_text[_position])) throw Error("JSON 指数缺少数字");
                    while (!End && char.IsDigit(_text[_position])) _position++;
                }
                string token = _text.Substring(start, _position - start);
                if (integer)
                {
                    long integerValue;
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue)) return integerValue;
                }
                double number;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                    double.IsNaN(number) || double.IsInfinity(number)) throw Error("JSON 数字超出范围");
                return number;
            }
        }
    }
}
