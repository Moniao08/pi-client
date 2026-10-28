using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace PiClient
{
    /// <summary>JSON 解析/序列化 + 取值小工具（.NET 自带的 JavaScriptSerializer，无第三方依赖）</summary>
    internal static class Json
    {
        private static readonly JavaScriptSerializer Ser = CreateSer();

        private static JavaScriptSerializer CreateSer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 256;
            return s;
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            object o = Ser.DeserializeObject(text);
            return o as Dictionary<string, object>;
        }

        public static string Stringify(object value)
        {
            return Ser.Serialize(value);
        }

        public static Dictionary<string, object> Obj(object o)
        {
            return o as Dictionary<string, object>;
        }

        public static List<object> Arr(object o)
        {
            object[] a = o as object[];
            if (a != null) return new List<object>(a);
            List<object> l = o as List<object>;
            if (l != null) return l;
            return new List<object>();
        }

        public static string Str(object o)
        {
            if (o == null) return null;
            string s = o as string;
            if (s != null) return s;
            return Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        public static string GetStr(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v)) return null;
            return Str(v);
        }

        public static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            string s = GetStr(d, key);
            return s == null ? def : s;
        }

        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch { return def; }
        }

        public static long GetLong(Dictionary<string, object> d, string key, long def)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); }
            catch { return def; }
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return def;
            if (v is bool) return (bool)v;
            try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); }
            catch { return def; }
        }

        public static Dictionary<string, object> NewCmd(string type)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["type"] = type;
            return d;
        }

        /// <summary>把多行文本压成一行用于摘要显示</summary>
        public static string OneLine(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool space = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\r' || c == '\n' || c == '\t' || c == ' ')
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
                if (sb.Length >= max) { sb.Append('…'); break; }
            }
            return sb.ToString();
        }
    }
}
