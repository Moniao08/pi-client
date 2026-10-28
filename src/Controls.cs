using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace PiClient
{
    internal interface IGrowable
    {
        event EventHandler HeightChanged;
    }

    internal static class TextMeter
    {
        private static readonly Dictionary<string, int> Cache = new Dictionary<string, int>();

        public static int Width(string text, Font f)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            string key = ((int)f.Style).ToString() + "|" + f.Name + "|" + f.SizeInPoints.ToString("0.##") + "|" + text;
            int w;
            if (Cache.TryGetValue(key, out w)) return w;
            w = TextRenderer.MeasureText(text, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            if (Cache.Count < 60000) Cache[key] = w;
            return w;
        }

        public static int Height(Font f)
        {
            return TextRenderer.MeasureText("国Ag", f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
        }
    }

    /* ---------------- 面板 ---------------- */
    internal class DarkPanel : Panel
    {
        public DarkPanel() : this(true) { }

        public DarkPanel(bool customPaint)
        {
            if (customPaint)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }
            else
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }
            BackColor = Theme.Bg;
        }
    }

    /* ---------------- 按钮 ---------------- */
    internal class FlatButton : Control, IButtonControl
    {
        private DialogResult _dialog = DialogResult.None;

        public DialogResult DialogResult
        {
            get { return _dialog; }
            set { _dialog = value; }
        }

        public void NotifyDefault(bool value)
        {
            Invalidate();
        }

        public void PerformClick()
        {
            if (Enabled) OnClick(EventArgs.Empty);
        }

        public Color FillColor = Theme.Panel2;
        public Color HoverColor = Theme.Panel3;
        public Color TextColor = Theme.TextCol;
        public Color BorderColor = Theme.Border;
        public bool Primary;
        private bool _hover;
        private bool _down;

        public FlatButton(string text)
        {
            Text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.Ui;
            Height = 30;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Color fill;
            Color border;
            if (Primary)
            {
                fill = _hover ? Color.FromArgb(107, 154, 255) : Theme.Accent;
                border = Color.Empty;
            }
            else
            {
                fill = _down ? Theme.Panel3 : (_hover ? HoverColor : FillColor);
                border = BorderColor;
            }
            if (!Enabled)
            {
                fill = Theme.Panel2;
                border = Theme.BorderSoft;
            }
            Theme.DrawRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 7, fill, border);
            Color tc = !Enabled ? Theme.Muted2 : (Primary ? Color.White : TextColor);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /* ---------------- 暗色下拉框 ---------------- */
    internal class DarkCombo : ComboBox
    {
        public DarkCombo()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            DropDownStyle = ComboBoxStyle.DropDownList;
            BackColor = Theme.Panel2;
            ForeColor = Theme.TextCol;
            Font = Theme.Ui;
            ItemHeight = 20;
            IntegralHeight = false;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                e.DrawBackground();
                return;
            }
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? Theme.Panel3 : Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = Items[e.Index] != null ? GetItemText(Items[e.Index]) : "";
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                sel ? Color.White : Theme.TextCol, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /* ---------------- 暗色下拉选择 ---------------- */
    internal class DarkMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(Theme.Panel2)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using (SolidBrush b = new SolidBrush(e.Item.Selected ? Theme.Panel3 : Theme.Panel2))
                e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.TextCol : Theme.Muted2;
            base.OnRenderItemText(e);
        }
    }

    internal class DarkSelect : Control
    {
        private List<string> _items = new List<string>();
        private int _index = -1;
        private bool _hover;

        public event EventHandler SelectionChanged;

        public DarkSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.Ui;
            Height = 26;
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex { get { return _index; } }

        public List<string> Items { get { return _items; } }

        public void SetItems(List<string> items, int selected)
        {
            _items = items ?? new List<string>();
            if (selected < -1 || selected >= _items.Count) selected = _items.Count > 0 ? 0 : -1;
            _index = selected;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || _items.Count == 0) return;
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new DarkMenuRenderer();
            m.ShowImageMargin = false;
            m.ShowCheckMargin = true;
            m.Font = Theme.Ui;
            m.BackColor = Theme.Panel2;
            for (int i = 0; i < _items.Count; i++)
            {
                int idx = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.Checked = i == _index;
                item.Click += delegate
                {
                    if (_index == idx) return;
                    _index = idx;
                    Invalidate();
                    EventHandler h = SelectionChanged;
                    if (h != null) h(this, EventArgs.Empty);
                };
                m.Items.Add(item);
            }
            m.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Color fill = !Enabled ? Theme.Panel : (_hover ? Theme.Panel3 : Theme.Panel2);
            Theme.DrawRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 7, fill, Theme.Border);
            string text = (_index >= 0 && _index < _items.Count) ? _items[_index] : "";
            TextRenderer.DrawText(g, text, Font, new Rectangle(9, 0, Width - 26, Height),
                Enabled ? Theme.TextCol : Theme.Muted2,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, "▾", Theme.UiSmall, new Rectangle(Width - 18, 0, 14, Height), Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /* ---------------- 会话列表（两行） ---------------- */
    internal class ListEntry
    {
        public string Title = "";
        public string Sub = "";
        public object Tag;
        public Color Dot = Color.Transparent;
        public bool Bold;
    }

    internal class DarkList : ListBox
    {
        public DarkList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Panel;
            ForeColor = Theme.TextCol;
            Font = Theme.Ui;
            ItemHeight = 40;
            IntegralHeight = false;
            SelectionMode = SelectionMode.One;
        }

        public void SetEntries(List<ListEntry> entries)
        {
            object keep = SelectedItem;
            BeginUpdate();
            Items.Clear();
            for (int i = 0; i < entries.Count; i++) Items.Add(entries[i]);
            EndUpdate();
            if (keep != null)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    ListEntry a = keep as ListEntry;
                    ListEntry b = Items[i] as ListEntry;
                    if (a != null && b != null && ReferenceEquals(a.Tag, b.Tag)) { SelectedIndex = i; break; }
                }
            }
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            ListEntry it = Items[e.Index] as ListEntry;
            if (it == null) return;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            if (sel)
            {
                Theme.FillRound(g, new Rectangle(r.X + 2, r.Y + 1, r.Width - 5, r.Height - 3), 7, Theme.Panel3);
            }
            int x = r.X + 10;
            if (it.Dot.A > 0)
            {
                using (SolidBrush b = new SolidBrush(it.Dot))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(b, x, r.Y + 12, 6, 6);
                    g.SmoothingMode = SmoothingMode.Default;
                }
                x += 12;
            }
            Font f = it.Bold ? Theme.UiBold : Theme.Ui;
            TextRenderer.DrawText(g, it.Title, f, new Rectangle(x, r.Y + 4, r.Width - x - 12, 18),
                Theme.TextCol, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, it.Sub, Theme.UiSmall, new Rectangle(r.X + 10, r.Y + 21, r.Width - 22, 16),
                Theme.Muted2, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    /* ---------------- Markdown 视图 ---------------- */
    internal class MdView : Control, IGrowable
    {
        private class Seg
        {
            public string Text = "";
            public Font Font;
            public Color Color;
            public string Href;
            public int X;
            public int W;
            public bool Chip;
        }

        private class Line
        {
            public int Y;
            public int H;
            public readonly List<Seg> Segs = new List<Seg>();
            public int Bar;
            public bool IsCode;
            public bool IsCodeHeader;
        }

        private string _raw = "";
        private bool _plain;
        private bool _dense;
        private List<MdBlock> _blocks;
        private readonly List<Line> _lines = new List<Line>();
        private int _contentHeight;
        private int _preferredWidth;
        private int _layoutWidth = -1;
        private bool _relaying;
        private class CodeRect
        {
            public int Top;
            public int Bottom;
            public string Text = "";
            public string Lang = "";
        }

        private Seg _hot;
        private readonly List<CodeRect> _codeRects = new List<CodeRect>();
        private readonly List<Rectangle> _tableHeaders = new List<Rectangle>();
        private readonly List<Rectangle> _tableRows = new List<Rectangle>();

        public event EventHandler HeightChanged;
        public event Action<string> LinkActivated;
        public event Action<string> CodeCopied;

        public MdView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = Theme.Body;
            TabStop = false;
        }

        public bool Plain { get { return _plain; } set { _plain = value; Relayout(); } }
        public bool Dense { get { return _dense; } set { _dense = value; Relayout(); } }
        public int PreferredWidth { get { return _preferredWidth; } }
        public string RawText { get { return _raw; } }

        public void SetText(string text)
        {
            if (text == null) text = "";
            if (text == _raw) return;
            _raw = text;
            _blocks = null;
            Relayout();
        }

        public void AppendText(string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;
            _raw += delta;
            _blocks = null;
            Relayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width != _layoutWidth) Relayout();
        }

        private void EnsureBlocks()
        {
            if (_blocks != null) return;
            if (_plain)
            {
                _blocks = new List<MdBlock>();
                string[] lines = _raw.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    MdBlock b = new MdBlock();
                    b.Kind = "p";
                    b.Runs.Add(MakeRun(lines[i]));
                    _blocks.Add(b);
                }
            }
            else
            {
                _blocks = Markdown.Parse(_raw);
            }
        }

        private static InlineRun MakeRun(string text)
        {
            InlineRun r = new InlineRun();
            r.Text = text;
            return r;
        }

        public void Relayout()
        {
            if (_relaying) return;
            _relaying = true;
            try { RelayoutCore(); } finally { _relaying = false; }
        }

        private void RelayoutCore()
        {
            EnsureBlocks();
            int w = Math.Max(40, Width);
            _layoutWidth = w;
            _lines.Clear();
            _codeRects.Clear();
            _tableHeaders.Clear();
            _tableRows.Clear();
            _preferredWidth = 0;
            int y = 0;
            int prevKindBottom = 0;

            for (int bi = 0; bi < _blocks.Count; bi++)
            {
                MdBlock b = _blocks[bi];

                if (b.Kind == "code")
                {
                    y += 6;
                    int top = y;
                    Line head = NewLine(y);
                    head.H = 18;
                    head.IsCodeHeader = true;
                    y += 18;
                    string[] codeLines = (b.Code ?? "").Replace("\r\n", "\n").Split('\n');
                    for (int li = 0; li < codeLines.Length; li++)
                    {
                        Line ln = NewLine(y);
                        ln.H = TextMeter.Height(Theme.Mono) + 1;
                        ln.IsCode = true;
                        Seg sg = new Seg();
                        sg.Text = codeLines[li].Length == 0 ? " " : codeLines[li];
                        sg.Font = Theme.Mono;
                        sg.Color = Theme.CodeText;
                        sg.X = 12;
                        sg.W = TextMeter.Width(sg.Text, Theme.Mono);
                        ln.Segs.Add(sg);
                        int right = sg.X + sg.W;
                        if (right > _preferredWidth) _preferredWidth = right;
                        y += ln.H;
                    }
                    y += 8;
                    CodeRect cr = new CodeRect();
                    cr.Top = top;
                    cr.Bottom = y;
                    cr.Text = b.Code ?? "";
                    cr.Lang = b.Lang;
                    _codeRects.Add(cr);
                    y += 4;
                    continue;
                }

                if (b.Kind == "h")
                {
                    y += 10;
                    Font f = b.Level <= 1 ? Theme.H1 : (b.Level == 2 ? Theme.H2 : Theme.H3);
                    y = WrapRuns(b.Runs, 0, w - 4, f, Theme.TextCol, y, 0, 2);
                    y += 4;
                }
                else if (b.Kind == "li")
                {
                    int indent = 6 + b.Level * 16;
                    string marker = b.Ordered ? (b.Number.Length > 0 ? b.Number : "•") : "•";
                    Line first = new Line();
                    first.H = TextMeter.Height(Theme.Body);
                    Seg ms = new Seg();
                    ms.Text = marker;
                    ms.Font = Theme.Body;
                    ms.Color = Theme.Muted;
                    ms.X = indent;
                    ms.W = TextMeter.Width(marker, Theme.Body);
                    first.Segs.Add(ms);
                    first.Y = y;
                    _lines.Add(first);
                    int startY = y;
                    y = WrapRuns(b.Runs, indent + 18, w - indent - 20, Theme.Body, Theme.TextCol, y, 0, 1);
                    if (_lines.Count > 0 && _lines[_lines.Count - 1].Y == startY)
                    {
                        // 首行合并：把 marker 已加入的行与包装行对齐
                    }
                    y += _dense ? 1 : 3;
                }
                else if (b.Kind == "quote")
                {
                    y += 3;
                    int before = _lines.Count;
                    y = WrapRuns(b.Runs, 12, w - 14, Theme.Body, Theme.Muted, y, 0, 3);
                    for (int i = before; i < _lines.Count; i++) _lines[i].Bar = 4;
                    y += 3;
                }
                else if (b.Kind == "hr")
                {
                    Line ln = new Line();
                    ln.Y = y + 6;
                    ln.H = 10;
                    ln.Bar = -1;
                    _lines.Add(ln);
                    y += 16;
                }
                else if (b.Kind == "table")
                {
                    y = LayoutTable(b, w, y);
                }
                else
                {
                    y = WrapRuns(b.Runs, 0, w - 4, Theme.Body, Theme.TextCol, y, 0, _dense ? 2 : 5);
                }
                prevKindBottom = y;
            }

            _contentHeight = Math.Max(6, y + 2);
            if (Height != _contentHeight)
            {
                Height = _contentHeight;
                EventHandler h = HeightChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
            Invalidate();
        }

        private int LayoutTable(MdBlock b, int width, int y)
        {
            int cols = b.Header.Count;
            if (cols == 0) return y;
            int colW = Math.Max(60, (width - 2) / cols);
            int rowY = y;
            int pad = 5;
            int hh = TextMeter.Height(Theme.BodyBold);
            // 表头
            for (int c = 0; c < cols; c++)
            {
                List<InlineRun> cell = c < b.Header.Count ? b.Header[c] : new List<InlineRun>();
                int cx = c * colW;
                WrapRuns(cell, cx + pad, colW - pad * 2, Theme.BodyBold, Theme.TextCol, rowY + pad, 0, 0);
            }
            _tableHeaders.Add(new Rectangle(0, rowY, colW * cols, hh + pad * 2));
            rowY += hh + pad * 2;
            for (int r = 0; r < b.Rows.Count; r++)
            {
                List<List<InlineRun>> row = b.Rows[r];
                int cy = rowY + pad;
                int maxY = cy;
                for (int c = 0; c < cols; c++)
                {
                    List<InlineRun> cell = c < row.Count ? row[c] : new List<InlineRun>();
                    int cx = c * colW;
                    int ny = WrapRuns(cell, cx + pad, colW - pad * 2, Theme.Body, Theme.TextCol, cy, 0, 0);
                    if (ny > maxY) maxY = ny;
                }
                _tableRows.Add(new Rectangle(0, rowY, colW * cols, maxY + pad - rowY));
                rowY = maxY + pad;
            }
            return rowY + 4;
        }

        private int WrapRuns(List<InlineRun> runs, int x0, int availW, Font baseFont, Color baseColor, int y, int padTop, int padBottom)
        {
            int maxW = Math.Max(40, availW);
            int indent = x0;
            int x = x0;
            Line cur = NewLine(y);
            y += padTop;
            Seg pending = null;

            Action flush = delegate
            {
                if (pending == null) return;
                cur.Segs.Add(pending);
                pending = null;
            };

            for (int ri = 0; ri < runs.Count; ri++)
            {
                InlineRun run = runs[ri];
                if (string.IsNullOrEmpty(run.Text)) continue;
                Font f = baseFont;
                if (run.Bold && run.Italic) f = Theme.BodyBoldItalic;
                else if (run.Bold) f = Theme.BodyBold;
                else if (run.Italic) f = Theme.BodyItalic;
                if (run.Code) f = Theme.Mono;
                Color col = run.Href != null ? Color.FromArgb(127, 170, 255) : baseColor;

                List<string> chunks = SplitChunks(run.Text);
                for (int ci = 0; ci < chunks.Count; ci++)
                {
                    string chunk = chunks[ci];
                    if (chunk == "\n")
                    {
                        flush();
                        y += cur.H;
                        cur = NewLine(y);
                        x = x0;
                        continue;
                    }
                    int cw = TextMeter.Width(chunk, f);
                    if (x + cw > x0 + maxW && x > x0)
                    {
                        flush();
                        y += cur.H;
                        cur = NewLine(y);
                        x = x0;
                        if (chunk.Trim().Length == 0) continue;
                    }
                    if (pending != null && pending.Font == f && pending.Color == col && pending.Href == run.Href && pending.Chip == run.Code)
                    {
                        pending.Text += chunk;
                        pending.W += cw;
                    }
                    else
                    {
                        flush();
                        pending = new Seg();
                        pending.Text = chunk;
                        pending.Font = f;
                        pending.Color = col;
                        pending.Href = run.Href;
                        pending.Chip = run.Code;
                        pending.X = x;
                        pending.W = cw;
                    }
                    if (cur.H < TextMeter.Height(f)) cur.H = TextMeter.Height(f);
                    x += cw;
                    if (x > _preferredWidth) _preferredWidth = x;
                }
                flush();
            }
            flush();
            if (cur.Segs.Count == 0)
            {
                cur.H = Math.Max(cur.H, TextMeter.Height(baseFont));
            }
            y += cur.H + padBottom;
            return y;
        }

        private Line NewLine(int y)
        {
            Line l = new Line();
            l.Y = y;
            l.H = TextMeter.Height(Theme.Body);
            _lines.Add(l);
            return l;
        }

        private static bool IsCjk(char c)
        {
            return (c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFF00 && c <= 0xFFEF) || (c >= 0x3000 && c <= 0x303F);
        }

        private static List<string> SplitChunks(string text)
        {
            List<string> chunks = new List<string>();
            StringBuilder cur = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    if (cur.Length > 0) { chunks.Add(cur.ToString()); cur.Length = 0; }
                    chunks.Add("\n");
                    continue;
                }
                if (IsCjk(c))
                {
                    if (cur.Length > 0) { chunks.Add(cur.ToString()); cur.Length = 0; }
                    chunks.Add(c.ToString());
                    continue;
                }
                if (c == ' ')
                {
                    cur.Append(c);
                    chunks.Add(cur.ToString());
                    cur.Length = 0;
                    continue;
                }
                cur.Append(c);
            }
            if (cur.Length > 0) chunks.Add(cur.ToString());
            return chunks;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            for (int ci = 0; ci < _codeRects.Count; ci++)
            {
                CodeRect cr = _codeRects[ci];
                Theme.DrawRound(g, new Rectangle(0, cr.Top, Width - 1, cr.Bottom - cr.Top), 7,
                    Color.FromArgb(12, 17, 27), Color.FromArgb(38, 48, 68));
                string lbl = string.IsNullOrEmpty(cr.Lang) ? "code" : cr.Lang;
                TextRenderer.DrawText(g, lbl + "    （点击复制）", Theme.UiSmall, new Rectangle(12, cr.Top + 2, Width - 24, 16),
                    Theme.Muted2, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }

            for (int ti = 0; ti < _tableHeaders.Count; ti++)
            {
                Rectangle hr = _tableHeaders[ti];
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 27, 41))) g.FillRectangle(b, hr);
                using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, hr.Left, hr.Bottom - 1, hr.Right, hr.Bottom - 1);
            }
            for (int ti = 0; ti < _tableRows.Count; ti++)
            {
                Rectangle rr = _tableRows[ti];
                using (Pen p = new Pen(Color.FromArgb(30, 39, 55))) g.DrawLine(p, rr.Left, rr.Bottom - 1, rr.Right, rr.Bottom - 1);
            }

            for (int i = 0; i < _lines.Count; i++)
            {
                Line ln = _lines[i];
                if (ln.Bar == -1)
                {
                    using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, ln.Y + ln.H / 2, Width, ln.Y + ln.H / 2);
                    continue;
                }
                if (ln.Bar == 4)
                {
                    using (Pen p = new Pen(Color.FromArgb(70, 85, 120), 2)) g.DrawLine(p, 4, ln.Y - 1, 4, ln.Y + ln.H + 1);
                }
                for (int si = 0; si < ln.Segs.Count; si++)
                {
                    Seg s = ln.Segs[si];
                    if (s.Text == "\n") continue;
                    if (s.Chip)
                    {
                        Rectangle chip = new Rectangle(s.X - 3, ln.Y + 1, s.W + 6, ln.H - 2);
                        Theme.FillRound(g, chip, 4, Color.FromArgb(28, 38, 56));
                        TextRenderer.DrawText(g, s.Text, s.Font, new Point(s.X, ln.Y + 1), Color.FromArgb(169, 200, 255),
                            TextFormatFlags.NoPadding);
                    }
                    else if (s.Href != null)
                    {
                        TextRenderer.DrawText(g, s.Text, s.Font, new Point(s.X, ln.Y), s.Color, TextFormatFlags.NoPadding);
                        int tw = TextMeter.Width(s.Text, s.Font);
                        using (Pen p = new Pen(Color.FromArgb(90, 127, 170, 255)))
                            g.DrawLine(p, s.X, ln.Y + ln.H - 2, s.X + tw, ln.Y + ln.H - 2);
                    }
                    else
                    {
                        TextRenderer.DrawText(g, s.Text, s.Font, new Point(s.X, ln.Y), s.Color, TextFormatFlags.NoPadding);
                    }
                }
            }
        }

        private Seg HitTest(Point p)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                Line ln = _lines[i];
                if (p.Y < ln.Y || p.Y > ln.Y + ln.H) continue;
                for (int si = 0; si < ln.Segs.Count; si++)
                {
                    Seg s = ln.Segs[si];
                    if (s.Href == null) continue;
                    if (p.X >= s.X && p.X <= s.X + s.W) return s;
                }
            }
            return null;
        }

        private CodeRect HitCode(Point p)
        {
            for (int i = 0; i < _codeRects.Count; i++)
            {
                CodeRect cr = _codeRects[i];
                if (p.Y >= cr.Top && p.Y <= cr.Bottom) return cr;
            }
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Seg s = HitTest(e.Location);
            if (!ReferenceEquals(s, _hot))
            {
                _hot = s;
                Cursor = s != null ? Cursors.Hand : (HitCode(e.Location) != null ? Cursors.Hand : Cursors.Default);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Seg s = HitTest(e.Location);
            if (s != null && LinkActivated != null)
            {
                LinkActivated(s.Href);
            }
            else
            {
                CodeRect cr = HitCode(e.Location);
                if (cr != null && !string.IsNullOrEmpty(cr.Text))
                {
                    try { Clipboard.SetText(cr.Text); } catch { }
                    Action<string> h = CodeCopied;
                    if (h != null) h(cr.Text);
                }
            }
            base.OnMouseClick(e);
        }
    }

    /* ---------------- 代码块 ---------------- */
    internal class CodeBlockView : Panel, IGrowable
    {
        private readonly TextBox _code;
        private readonly FlatButton _copy;
        private readonly Label _lang;
        private int _naturalHeight;

        public event EventHandler HeightChanged;

        public CodeBlockView()
        {
            BackColor = Theme.CodeBg;
            Padding = new Padding(0);
            SetStyle(ControlStyles.ResizeRedraw, true);

            _lang = new Label();
            _lang.AutoSize = false;
            _lang.Height = 22;
            _lang.ForeColor = Theme.Muted;
            _lang.Font = Theme.MonoSmall;
            _lang.BackColor = Color.FromArgb(18, 26, 40);
            _lang.TextAlign = ContentAlignment.MiddleLeft;
            _lang.Padding = new Padding(8, 0, 0, 0);

            _copy = new FlatButton("复制");
            _copy.Font = Theme.UiSmall;
            _copy.Height = 20;
            _copy.Width = 46;
            _copy.FillColor = Color.FromArgb(18, 26, 40);
            _copy.HoverColor = Theme.Panel3;
            _copy.TextColor = Theme.Muted;
            _copy.Click += delegate
            {
                try { Clipboard.SetText(_code.Text); } catch { }
                _copy.Text = "已复制";
                Timer t = new Timer();
                t.Interval = 1200;
                t.Tick += delegate { _copy.Text = "复制"; t.Stop(); t.Dispose(); };
                t.Start();
            };

            _code = new TextBox();
            _code.Multiline = true;
            _code.ReadOnly = true;
            _code.WordWrap = false;
            _code.ScrollBars = ScrollBars.Both;
            _code.BorderStyle = BorderStyle.None;
            _code.BackColor = Theme.CodeBg;
            _code.ForeColor = Theme.CodeText;
            _code.Font = Theme.Mono;
            _code.TabStop = false;
            _code.ShortcutsEnabled = true;

            Controls.Add(_lang);
            Controls.Add(_copy);
            Controls.Add(_code);
        }

        public string CodeText { get { return _code.Text; } }

        public void SetCode(string lang, string code)
        {
            _lang.Text = string.IsNullOrEmpty(lang) ? "text" : lang;
            _code.Text = code ?? "";
            int lines = 1;
            for (int i = 0; i < _code.Text.Length; i++) if (_code.Text[i] == '\n') lines++;
            int h = Math.Min(360, Math.Max(46, lines * (TextMeter.Height(Theme.Mono) + 2) + 14));
            _naturalHeight = h + 23;
            DoLayout();
        }

        public void DoLayout()
        {
            if (Width <= 0) return;
            _lang.Bounds = new Rectangle(0, 0, Math.Max(30, Width - 56), 22);
            _copy.Bounds = new Rectangle(Width - 52, 1, 46, 20);
            _code.Bounds = new Rectangle(8, 26, Math.Max(20, Width - 16), Math.Max(20, _naturalHeight - 30));
            int want = _code.Bounds.Bottom + 8;
            if (Height != want)
            {
                Height = want;
                EventHandler h = HeightChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Theme.Border))
            {
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                e.Graphics.DrawLine(p, 0, 22, Width, 22);
            }
        }
    }

    /* ---------------- 思考过程 ---------------- */
    internal class ThinkingView : Panel, IGrowable
    {
        private readonly MdView _body;
        private bool _open;
        private bool _hover;

        public event EventHandler HeightChanged;

        public ThinkingView()
        {
            BackColor = Theme.Bg;
            _body = new MdView();
            _body.Dense = true;
            _body.BackColor = Color.FromArgb(19, 26, 39);
            _body.Left = 10;
            _body.HeightChanged += delegate { DoLayout(); };
            Controls.Add(_body);
            Height = 26;
        }

        public void SetText(string text)
        {
            _body.SetText(text);
            DoLayout();
        }

        public void AppendText(string delta)
        {
            _body.AppendText(delta);
        }

        public void DoLayout()
        {
            int w = Math.Max(60, Width - 22);
            if (_body.Width != w) _body.Width = w;
            int bodyH = _open ? _body.Height + 6 : 0;
            int want = 26 + bodyH + (_open ? 6 : 0);
            if (Height != want)
            {
                Height = want;
                EventHandler h = HeightChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
            _body.Visible = _open;
            if (_open) _body.Top = 23;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Y < 26)
            {
                _open = !_open;
                DoLayout();
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            Theme.DrawRound(g, new Rectangle(0, 0, Width - 1, 24), 7, Color.FromArgb(22, 30, 45), _hover ? Color.FromArgb(70, 90, 130) : Color.FromArgb(40, 52, 74));
            TextRenderer.DrawText(g, (_open ? "▾ " : "▸ ") + "思考过程", Theme.UiSmall, new Rectangle(8, 3, Width - 16, 18), Theme.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            if (_open)
            {
                Theme.FillRound(g, new Rectangle(0, 26, Width - 1, Height - 27), 6, Color.FromArgb(19, 26, 39));
                using (Pen p = new Pen(Color.FromArgb(38, 48, 68))) g.DrawRectangle(p, 0, 26, Width - 1, Height - 27);
            }
        }
    }

    /* ---------------- 工具卡片 ---------------- */
    internal class ToolCardView : Panel, IGrowable
    {
        private string _name = "tool";
        private string _summary = "";
        private string _argsText = "";
        private string _outText = "";
        private bool _running = true;
        private bool _error;
        private bool _open;
        private bool _hover;
        private readonly TextBox _argsBox;
        private readonly TextBox _outBox;

        public event EventHandler HeightChanged;

        public ToolCardView()
        {
            BackColor = Theme.Bg;
            _argsBox = MakeBox();
            _outBox = MakeBox();
            Controls.Add(_argsBox);
            Controls.Add(_outBox);
            Height = 30;
        }

        private static TextBox MakeBox()
        {
            TextBox b = new TextBox();
            b.Multiline = true;
            b.ReadOnly = true;
            b.WordWrap = true;
            b.ScrollBars = ScrollBars.Vertical;
            b.BorderStyle = BorderStyle.None;
            b.BackColor = Theme.CodeBg;
            b.ForeColor = Color.FromArgb(196, 210, 230);
            b.Font = Theme.MonoSmall;
            b.TabStop = false;
            b.Visible = false;
            return b;
        }

        public void SetTool(string name, Dictionary<string, object> args)
        {
            _name = name ?? "tool";
            _summary = Summarize(name, args);
            _argsText = args != null && args.Count > 0 ? Pretty(args) : "";
            DoLayout();
        }

        public void SetSummary(string text)
        {
            _summary = text ?? "";
            Invalidate();
        }

        public void SetOutput(string text, bool isError)
        {
            _outText = text ?? "";
            _error = isError;
            _running = false;
            DoLayout();
        }

        public void MarkDone()
        {
            _running = false;
            DoLayout();
        }

        public void SetRunningOutput(string text)
        {
            _outText = text ?? "";
            DoLayout();
        }

        public static string Summarize(string name, Dictionary<string, object> args)
        {
            if (args == null || args.Count == 0) return "";
            string[] keys = new string[] { "command", "cmd", "path", "file_path", "filePath", "pattern", "query", "url", "prompt", "description", "name", "text" };
            for (int i = 0; i < keys.Length; i++)
            {
                string v = Json.GetStr(args, keys[i]);
                if (!string.IsNullOrEmpty(v)) return Json.OneLine(v, 140);
            }
            foreach (KeyValuePair<string, object> kv in args)
            {
                string s = kv.Value as string;
                if (!string.IsNullOrEmpty(s)) return Json.OneLine(s, 140);
            }
            return Json.OneLine(Json.Stringify(args), 140);
        }

        public static string Pretty(Dictionary<string, object> args)
        {
            try
            {
                string s = Json.Stringify(args);
                // 简易缩进美化
                StringBuilder sb = new StringBuilder();
                int indent = 0;
                bool inStr = false;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c == '"' && (i == 0 || s[i - 1] != '\\')) inStr = !inStr;
                    if (!inStr && (c == '{' || c == '['))
                    {
                        sb.Append(c);
                        sb.Append('\n');
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                    }
                    else if (!inStr && (c == '}' || c == ']'))
                    {
                        sb.Append('\n');
                        indent = Math.Max(0, indent - 1);
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(c);
                    }
                    else if (!inStr && c == ',')
                    {
                        sb.Append(c);
                        sb.Append('\n');
                        sb.Append(new string(' ', indent * 2));
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        public void DoLayout()
        {
            int w = Math.Max(80, Width - 2);
            int y = 30;
            bool hasArgs = _argsText.Length > 0;
            bool hasOut = _outText.Length > 0;

            if (_open && hasArgs)
            {
                _argsBox.Visible = true;
                _argsBox.Bounds = new Rectangle(10, y + 4, w - 20, Math.Min(140, 16 + LineCount(_argsText) * 15));
                y = _argsBox.Bottom + 6;
            }
            else _argsBox.Visible = false;

            if (_open && hasOut)
            {
                _outBox.Visible = true;
                _outBox.ForeColor = _error ? Color.FromArgb(255, 180, 180) : Color.FromArgb(196, 210, 230);
                _outBox.Bounds = new Rectangle(10, y + 4, w - 20, Math.Min(320, 16 + LineCount(_outText) * 15));
                y = _outBox.Bottom + 8;
            }
            else _outBox.Visible = false;

            int want = _open ? Math.Max(32, y) : 30;
            if (Height != want)
            {
                Height = want;
                EventHandler h = HeightChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
            Invalidate();
        }

        private static int LineCount(string s)
        {
            int n = 1;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Y < 30)
            {
                _open = !_open;
                DoLayout();
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            // 卡片背景
            Theme.DrawRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 8, Color.FromArgb(15, 20, 32),
                _error ? Color.FromArgb(80, 45, 55) : Color.FromArgb(38, 48, 68));
            Rectangle head = new Rectangle(0, 0, Width - 1, 29);
            Theme.FillRound(g, head, 8, _hover ? Color.FromArgb(24, 32, 48) : Color.FromArgb(19, 26, 40));
            using (Pen p = new Pen(Color.FromArgb(34, 44, 61))) g.DrawLine(p, 1, 29, Width - 2, 29);

            string icon = _running ? "●" : (_error ? "✕" : "✓");
            Color ic = _running ? Theme.Warn : (_error ? Theme.Err : Theme.Ok);
            TextRenderer.DrawText(g, icon, Theme.UiSmall, new Rectangle(10, 7, 16, 16), ic, TextFormatFlags.NoPadding);

            int nx = 26;
            int nameW = TextMeter.Width(_name, Theme.UiBold) + 6;
            TextRenderer.DrawText(g, _name, Theme.UiBold, new Rectangle(nx, 7, nameW, 16), Color.FromArgb(157, 180, 255),
                TextFormatFlags.NoPadding);

            int sx = nx + nameW + 6;
            TextRenderer.DrawText(g, _summary, Theme.UiSmall, new Rectangle(sx, 8, Math.Max(20, Width - sx - 70), 16),
                Theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            string state = _running ? "运行中…" : (_error ? "失败" : "完成");
            TextRenderer.DrawText(g, state, Theme.UiSmall, new Rectangle(Width - 66, 8, 56, 16),
                _running ? Theme.Warn : (_error ? Theme.Err : Theme.Muted2),
                TextFormatFlags.NoPadding | TextFormatFlags.Right);
        }
    }
}
