using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PiClient
{
    /// <summary>便携配置：优先写 exe 同目录，不可写时退回 %APPDATA%\pi-client</summary>
    internal class Config
    {
        public string PiHome = "";
        public string PiNode = "";
        public string LastCwd = "";
        public List<string> RecentDirs = new List<string>();
        public int WinX = int.MinValue;
        public int WinY = int.MinValue;
        public int WinW = 0;
        public int WinH = 0;
        public bool WinMax;
        public string Model = "";
        public string Thinking = "";

        private static string _path;

        public static string Path
        {
            get
            {
                if (_path == null)
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    _path = System.IO.Path.Combine(exeDir, "config.json");
                }
                return _path;
            }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (File.Exists(Path))
                {
                    Dictionary<string, object> d = Json.ParseObject(File.ReadAllText(Path, Encoding.UTF8));
                    if (d != null)
                    {
                        c.PiHome = Json.GetStr(d, "piHome", "");
                        c.PiNode = Json.GetStr(d, "piNode", "");
                        c.LastCwd = Json.GetStr(d, "lastCwd", "");
                        c.Model = Json.GetStr(d, "model", "");
                        c.Thinking = Json.GetStr(d, "thinking", "");
                        c.WinMax = Json.GetBool(d, "winMax", false);
                        c.WinX = Json.GetInt(d, "winX", int.MinValue);
                        c.WinY = Json.GetInt(d, "winY", int.MinValue);
                        c.WinW = Json.GetInt(d, "winW", 0);
                        c.WinH = Json.GetInt(d, "winH", 0);
                        List<object> arr = Json.Arr(Get(d, "recentDirs"));
                        for (int i = 0; i < arr.Count; i++)
                        {
                            string s = Json.Str(arr[i]);
                            if (!string.IsNullOrEmpty(s)) c.RecentDirs.Add(s);
                        }
                    }
                }
            }
            catch
            {
                // 配置损坏时用默认值，不影响启动
            }
            return c;
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        public void RememberDir(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            LastCwd = dir;
            RecentDirs.RemoveAll(delegate(string s) { return string.Equals(s, dir, StringComparison.OrdinalIgnoreCase); });
            RecentDirs.Insert(0, dir);
            while (RecentDirs.Count > 12) RecentDirs.RemoveAt(RecentDirs.Count - 1);
        }

        public void Save()
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["piHome"] = PiHome;
                d["piNode"] = PiNode;
                d["lastCwd"] = LastCwd;
                d["model"] = Model;
                d["thinking"] = Thinking;
                d["winX"] = WinX;
                d["winY"] = WinY;
                d["winW"] = WinW;
                d["winH"] = WinH;
                d["winMax"] = WinMax;
                d["recentDirs"] = RecentDirs.ToArray();
                string text = Json.Stringify(d);
                // 缩进一下，方便手动改
                text = text.Replace(",\"", ",\n  \"").Replace("{\"", "{\n  \"");
                File.WriteAllText(Path, text, new UTF8Encoding(false));
            }
            catch
            {
                // 磁盘不可写时静默失败（例如放在只读目录）
            }
        }
    }
}
