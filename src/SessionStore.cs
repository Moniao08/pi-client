using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PiClient
{
    /// <summary>会话里的一个内容块</summary>
    internal class Block
    {
        public const string KText = "text";
        public const string KThinking = "thinking";
        public const string KTool = "tool";

        public string Kind = KText;
        public string Text = "";
        public string ToolCallId;
        public string ToolName;
        public Dictionary<string, object> Args;
        public string Output = "";
        public bool IsError;
        public bool Running;
        public int ContentIndex;
        public object Ui;          // 对应的控件，便于增量更新
        public string RenderedText = "";
        public bool FinishedUi;
        public string ArgsRaw = "";
    }

    /// <summary>一条消息（用户或助手）</summary>
    internal class ChatMessage
    {
        public string Role;               // user | assistant
        public string Text = "";
        public List<string> Images = new List<string>();
        public List<Block> Blocks = new List<Block>();
        public object Ui;
        public bool IsLive;
    }

    /// <summary>磁盘上的历史会话条目</summary>
    internal class SessionInfo
    {
        public string Path;
        public string Id;
        public string Cwd;
        public string Name;
        public string Title;
        public string Preview;
        public DateTime Mtime;
        public int MessageCount = -1;

        public string TimeText
        {
            get
            {
                TimeSpan d = DateTime.Now - Mtime;
                if (d.TotalMinutes < 1) return "刚刚";
                if (d.TotalHours < 1) return ((int)d.TotalMinutes).ToString() + " 分钟前";
                if (d.TotalDays < 1) return ((int)d.TotalHours).ToString() + " 小时前";
                if (d.TotalDays < 7) return ((int)d.TotalDays).ToString() + " 天前";
                return Mtime.ToString("M/d");
            }
        }
    }

    internal static class SessionStore
    {
        public static string Root
        {
            get
            {
                string user = Environment.GetEnvironmentVariable("USERPROFILE");
                if (string.IsNullOrEmpty(user)) user = "C:\\";
                return System.IO.Path.Combine(user, @".pi\agent\sessions");
            }
        }

        public static List<SessionInfo> List(string cwd, int limit)
        {
            List<SessionInfo> result = new List<SessionInfo>();
            try
            {
                if (!Directory.Exists(Root)) return result;
                List<FileInfo> files = new List<FileInfo>();
                string[] dirs = Directory.GetDirectories(Root);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string[] fs;
                    try { fs = Directory.GetFiles(dirs[i], "*.jsonl"); }
                    catch { continue; }
                    for (int j = 0; j < fs.Length; j++)
                    {
                        try { files.Add(new FileInfo(fs[j])); }
                        catch { }
                    }
                }
                files.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });

                string want = NormCwd(cwd);
                for (int i = 0; i < files.Count && result.Count < limit; i++)
                {
                    SessionInfo si = ReadInfo(files[i], want);
                    if (si != null) result.Add(si);
                }
            }
            catch { }
            return result;
        }

        private static SessionInfo ReadInfo(FileInfo fi, string wantCwd)
        {
            try
            {
                const int HEAD = 200000;
                bool complete = fi.Length <= HEAD;
                string head = ReadRange(fi.FullName, 0, (int)Math.Min(HEAD, fi.Length));
                string[] lines = head.Split('\n');
                string cwd = null;
                string id = null;
                string name = null;
                string preview = null;
                int userMsgs = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length < 5) continue;
                    Dictionary<string, object> o;
                    try { o = Json.ParseObject(line); }
                    catch { continue; }
                    if (o == null) continue;
                    string t = Json.GetStr(o, "type");
                    if (t == "session")
                    {
                        id = Json.GetStr(o, "id");
                        cwd = Json.GetStr(o, "cwd");
                        name = Json.GetStr(o, "name");
                    }
                    else if (t == "session_info")
                    {
                        string nm = Json.GetStr(o, "name");
                        if (!string.IsNullOrEmpty(nm)) name = nm;
                    }
                    else if (t == "message")
                    {
                        Dictionary<string, object> m = Json.Obj(Get(o, "message"));
                        if (m != null && Json.GetStr(m, "role") == "user")
                        {
                            userMsgs++;
                            if (preview == null) preview = Json.OneLine(ContentText(Get(m, "content")), 150);
                        }
                    }
                }

                if (cwd == null) return null;
                if (wantCwd != null && NormCwd(cwd) != wantCwd) return null;

                if (name == null && !complete)
                {
                    string tail = ReadRange(fi.FullName, (int)Math.Max(0, fi.Length - 32768), 32768);
                    string[] tl = tail.Split('\n');
                    for (int i = 0; i < tl.Length; i++)
                    {
                        if (tl[i].IndexOf("session_info", StringComparison.Ordinal) < 0) continue;
                        try
                        {
                            Dictionary<string, object> o = Json.ParseObject(tl[i]);
                            string nm = Json.GetStr(o, "name");
                            if (!string.IsNullOrEmpty(nm)) name = nm;
                        }
                        catch { }
                    }
                }

                SessionInfo si = new SessionInfo();
                si.Path = fi.FullName;
                si.Id = id;
                si.Cwd = cwd;
                si.Name = name;
                si.Preview = preview;
                si.Mtime = fi.LastWriteTime;
                si.Title = !string.IsNullOrEmpty(name) ? name : (!string.IsNullOrEmpty(preview) ? Json.OneLine(preview, 46) : System.IO.Path.GetFileNameWithoutExtension(fi.Name));
                if (complete) si.MessageCount = userMsgs;
                return si;
            }
            catch
            {
                return null;
            }
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        public static string ContentText(object content)
        {
            string s = content as string;
            if (s != null) return s;
            List<object> arr = Json.Arr(content);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < arr.Count; i++)
            {
                Dictionary<string, object> c = Json.Obj(arr[i]);
                if (c == null) continue;
                if (Json.GetStr(c, "type") == "text")
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(Json.GetStr(c, "text", ""));
                }
            }
            return sb.ToString();
        }

        private static string ReadRange(string path, int position, int length)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (position > 0) fs.Seek(position, SeekOrigin.Begin);
                    byte[] buf = new byte[length];
                    int read = fs.Read(buf, 0, length);
                    return Encoding.UTF8.GetString(buf, 0, read);
                }
            }
            catch
            {
                return "";
            }
        }

        public static string NormCwd(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            string s = p.Replace('\\', '/').TrimEnd('/');
            return s.ToLowerInvariant();
        }
    }
}
