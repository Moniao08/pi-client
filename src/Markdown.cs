using System;
using System.Collections.Generic;
using System.Text;

namespace PiClient
{
    internal class InlineRun
    {
        public string Text = "";
        public bool Bold;
        public bool Italic;
        public bool Code;
        public bool Strike;
        public string Href;
    }

    internal class MdBlock
    {
        public string Kind = "p";   // p | h | li | quote | code | hr | table
        public int Level;
        public bool Ordered;
        public string Number = "";
        public string Lang = "";
        public string Code = "";
        public List<InlineRun> Runs = new List<InlineRun>();
        public List<List<List<InlineRun>>> Rows = new List<List<List<InlineRun>>>();
        public List<List<InlineRun>> Header = new List<List<InlineRun>>();
    }

    /// <summary>极简 Markdown 解析：标题/列表/引用/代码块/表格/行内样式</summary>
    internal static class Markdown
    {
        public static List<MdBlock> Parse(string src)
        {
            List<MdBlock> blocks = new List<MdBlock>();
            if (src == null) return blocks;
            string[] lines = src.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            List<string> para = new List<string>();
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];

                // 代码块
                string fenceMark;
                string lang;
                if (IsFence(line, out fenceMark, out lang))
                {
                    FlushPara(blocks, para);
                    StringBuilder sb = new StringBuilder();
                    i++;
                    while (i < lines.Length && !IsFenceClose(lines[i], fenceMark))
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(lines[i]);
                        i++;
                    }
                    i++; // 跳过结尾围栏
                    MdBlock b = new MdBlock();
                    b.Kind = "code";
                    b.Lang = lang;
                    b.Code = sb.ToString();
                    blocks.Add(b);
                    continue;
                }

                if (line.Trim().Length == 0)
                {
                    FlushPara(blocks, para);
                    i++;
                    continue;
                }

                // 标题
                int hashes = 0;
                while (hashes < line.Length && hashes < 6 && line[hashes] == '#') hashes++;
                if (hashes > 0 && hashes < line.Length && line[hashes] == ' ')
                {
                    FlushPara(blocks, para);
                    MdBlock b = new MdBlock();
                    b.Kind = "h";
                    b.Level = hashes;
                    b.Runs = ParseInline(line.Substring(hashes + 1).Trim());
                    blocks.Add(b);
                    i++;
                    continue;
                }

                // 分割线
                string t = line.Trim();
                if (t.Length >= 3 && (AllChars(t, '-') || AllChars(t, '*') || AllChars(t, '_')))
                {
                    FlushPara(blocks, para);
                    MdBlock b = new MdBlock();
                    b.Kind = "hr";
                    blocks.Add(b);
                    i++;
                    continue;
                }

                // 表格
                if (t.IndexOf('|') >= 0 && i + 1 < lines.Length && IsTableSep(lines[i + 1]))
                {
                    FlushPara(blocks, para);
                    MdBlock b = new MdBlock();
                    b.Kind = "table";
                    b.Header = SplitCells(line);
                    i += 2;
                    while (i < lines.Length && lines[i].IndexOf('|') >= 0 && lines[i].Trim().Length > 0)
                    {
                        b.Rows.Add(SplitCells(lines[i]));
                        i++;
                    }
                    blocks.Add(b);
                    continue;
                }

                // 引用
                if (line.TrimStart().StartsWith(">"))
                {
                    FlushPara(blocks, para);
                    StringBuilder sb = new StringBuilder();
                    while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                    {
                        string s2 = lines[i].TrimStart().Substring(1);
                        if (s2.StartsWith(" ")) s2 = s2.Substring(1);
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(s2);
                        i++;
                    }
                    List<MdBlock> inner = Parse(sb.ToString());
                    for (int k = 0; k < inner.Count; k++)
                    {
                        if (inner[k].Kind == "p") { inner[k].Kind = "quote"; blocks.Add(inner[k]); }
                        else blocks.Add(inner[k]);
                    }
                    continue;
                }

                // 列表
                int indent;
                bool ordered;
                string marker;
                string content;
                if (TryList(line, out indent, out ordered, out marker, out content))
                {
                    FlushPara(blocks, para);
                    MdBlock b = new MdBlock();
                    b.Kind = "li";
                    b.Level = indent / 2;
                    b.Ordered = ordered;
                    b.Number = marker;
                    b.Runs = ParseInline(content);
                    blocks.Add(b);
                    i++;
                    // 续行（缩进的非列表行）
                    while (i < lines.Length && lines[i].Trim().Length > 0 && lines[i].StartsWith("  ") && !TryList(lines[i], out indent, out ordered, out marker, out content))
                    {
                        AppendRuns(b.Runs, ParseInline(" " + lines[i].Trim()));
                        i++;
                    }
                    continue;
                }

                para.Add(line);
                i++;
            }
            FlushPara(blocks, para);
            return blocks;
        }

        private static void AppendRuns(List<InlineRun> target, List<InlineRun> extra)
        {
            for (int i = 0; i < extra.Count; i++)
            {
                if (target.Count > 0 && SameStyle(target[target.Count - 1], extra[i]))
                    target[target.Count - 1].Text += extra[i].Text;
                else
                    target.Add(extra[i]);
            }
        }

        private static bool SameStyle(InlineRun a, InlineRun b)
        {
            return a.Bold == b.Bold && a.Italic == b.Italic && a.Code == b.Code && a.Strike == b.Strike && a.Href == b.Href;
        }

        private static void FlushPara(List<MdBlock> blocks, List<string> para)
        {
            if (para.Count == 0) return;
            MdBlock b = new MdBlock();
            b.Kind = "p";
            b.Runs = ParseInline(string.Join(" ", para.ToArray()));
            blocks.Add(b);
            para.Clear();
        }

        private static bool AllChars(string s, char c)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] != c) return false;
            return true;
        }

        private static bool IsFence(string line, out string mark, out string lang)
        {
            mark = null;
            lang = "";
            string t = line.TrimStart();
            if (t.StartsWith("```") || t.StartsWith("~~~"))
            {
                char c = t[0];
                int n = 0;
                while (n < t.Length && t[n] == c) n++;
                if (n < 3) return false;
                mark = new string(c, n);
                lang = t.Substring(n).Trim();
                int sp = lang.IndexOf(' ');
                if (sp > 0) lang = lang.Substring(0, sp);
                return true;
            }
            return false;
        }

        private static bool IsFenceClose(string line, string mark)
        {
            string t = line.Trim();
            if (t.Length < mark.Length) return false;
            for (int i = 0; i < mark.Length; i++) if (t[i] != mark[i]) return false;
            return t.TrimEnd(mark[0]).Length == 0;
        }

        private static bool IsTableSep(string line)
        {
            string t = line.Trim();
            if (t.IndexOf('-') < 0) return false;
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (c != '-' && c != '|' && c != ':' && c != ' ') return false;
            }
            return true;
        }

        private static List<List<InlineRun>> SplitCells(string line)
        {
            string s = line.Trim();
            if (s.StartsWith("|")) s = s.Substring(1);
            if (s.EndsWith("|")) s = s.Substring(0, s.Length - 1);
            string[] parts = s.Split('|');
            List<List<InlineRun>> cells = new List<List<InlineRun>>();
            for (int i = 0; i < parts.Length; i++) cells.Add(ParseInline(parts[i].Trim()));
            return cells;
        }

        private static bool TryList(string line, out int indent, out bool ordered, out string marker, out string content)
        {
            indent = 0;
            ordered = false;
            marker = "";
            content = null;
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            {
                indent += line[i] == '\t' ? 2 : 1;
                i++;
            }
            if (i >= line.Length) return false;
            char c = line[i];
            if (c == '-' || c == '*' || c == '+')
            {
                if (i + 1 < line.Length && line[i + 1] == ' ')
                {
                    marker = c.ToString();
                    content = line.Substring(i + 2).Trim();
                    return true;
                }
                return false;
            }
            int d = i;
            while (d < line.Length && char.IsDigit(line[d])) d++;
            if (d > i && d < line.Length && (line[d] == '.' || line[d] == ')') && d + 1 < line.Length && line[d + 1] == ' ')
            {
                ordered = true;
                marker = line.Substring(i, d - i + 1);
                content = line.Substring(d + 2).Trim();
                return true;
            }
            return false;
        }

        /* ---------------- 行内 ---------------- */
        public static List<InlineRun> ParseInline(string text)
        {
            List<InlineRun> runs = new List<InlineRun>();
            if (string.IsNullOrEmpty(text)) return runs;
            StringBuilder buf = new StringBuilder();
            bool bold = false;
            bool italic = false;
            bool strike = false;

            Action flush = delegate
            {
                if (buf.Length == 0) return;
                InlineRun r = new InlineRun();
                r.Text = buf.ToString();
                r.Bold = bold;
                r.Italic = italic;
                r.Strike = strike;
                runs.Add(r);
                buf.Length = 0;
            };

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                if (c == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        flush();
                        InlineRun r = new InlineRun();
                        r.Code = true;
                        r.Text = text.Substring(i + 1, end - i - 1);
                        runs.Add(r);
                        i = end + 1;
                        continue;
                    }
                }

                if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    flush();
                    bold = !bold;
                    i += 2;
                    continue;
                }

                if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
                {
                    flush();
                    strike = !strike;
                    i += 2;
                    continue;
                }

                if (c == '*')
                {
                    // 单星号斜体：要求前后不是空格，避免误判
                    bool prevOk = i > 0 && !char.IsWhiteSpace(text[i - 1]);
                    bool nextOk = i + 1 < text.Length && !char.IsWhiteSpace(text[i + 1]);
                    if (prevOk || nextOk)
                    {
                        if (italic)
                        {
                            flush();
                            italic = false;
                            i++;
                            continue;
                        }
                        int close = FindItalicClose(text, i + 1);
                        if (close > 0)
                        {
                            flush();
                            italic = true;
                            i++;
                            continue;
                        }
                    }
                }

                if (c == '[')
                {
                    int rb = text.IndexOf(']', i + 1);
                    if (rb > i && rb + 1 < text.Length && text[rb + 1] == '(')
                    {
                        int rp = text.IndexOf(')', rb + 2);
                        if (rp > rb)
                        {
                            flush();
                            InlineRun r = new InlineRun();
                            r.Text = text.Substring(i + 1, rb - i - 1);
                            r.Href = text.Substring(rb + 2, rp - rb - 2).Trim();
                            r.Bold = bold;
                            r.Italic = italic;
                            runs.Add(r);
                            i = rp + 1;
                            continue;
                        }
                    }
                }

                if ((c == 'h' || c == 'H') && IsUrlStart(text, i))
                {
                    int end = i;
                    while (end < text.Length && !char.IsWhiteSpace(text[end]) && "<)>\"'".IndexOf(text[end]) < 0) end++;
                    if (end - i > 8)
                    {
                        flush();
                        InlineRun r = new InlineRun();
                        string url = text.Substring(i, end - i);
                        r.Text = url;
                        r.Href = url;
                        runs.Add(r);
                        i = end;
                        continue;
                    }
                }

                buf.Append(c);
                i++;
            }
            flush();
            if (runs.Count == 0) runs.Add(new InlineRun());
            return runs;
        }

        private static bool IsUrlStart(string s, int i)
        {
            if (i + 7 > s.Length) return false;
            string head = s.Substring(i, 7).ToLowerInvariant();
            if (!(head.StartsWith("http://") || head.StartsWith("https:/"))) return false;
            return i == 0 || !char.IsLetterOrDigit(s[i - 1]);
        }

        private static int FindItalicClose(string s, int from)
        {
            for (int i = from; i < s.Length; i++)
            {
                if (s[i] == '*' && i > 0 && !char.IsWhiteSpace(s[i - 1])) return i;
            }
            return -1;
        }
    }
}
