using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>一条消息的显示（用户气泡 / 助手内容块）</summary>
    internal class MessageView : Panel
    {
        public ChatMessage Model;
        public event EventHandler HeightChanged;

        private readonly bool _isUser;
        private Panel _bubble;
        private MdView _bubbleText;
        private readonly List<Control> _images = new List<Control>();
        private readonly List<Control> _stack = new List<Control>();

        private bool _inLayout;
        private const int AvatarSize = 26;
        private const int Gutter = 38;

        public MessageView(ChatMessage m)
        {
            Model = m;
            _isUser = m.Role == "user";
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 40;
            Build();
        }

        private void Build()
        {
            if (_isUser) BuildUser(); else BuildAssistant();
        }

        private void BuildUser()
        {
            _bubble = new DarkPanel();
            _bubble.BackColor = Color.FromArgb(27, 39, 64);
            _bubble.Paint += delegate(object s, PaintEventArgs e)
            {
                Theme.DrawRound(e.Graphics, new Rectangle(0, 0, _bubble.Width - 1, _bubble.Height - 1), 10, Color.FromArgb(27, 39, 64), Color.FromArgb(43, 61, 94));
            };

            int y = 10;
            for (int i = 0; i < Model.Images.Count; i++)
            {
                Image img = LoadImage(Model.Images[i]);
                if (img == null) continue;
                PictureBox pb = new PictureBox();
                pb.Image = img;
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                pb.BackColor = Color.FromArgb(27, 39, 64);
                _images.Add(pb);
                _bubble.Controls.Add(pb);
            }

            _bubbleText = new MdView();
            _bubbleText.Plain = true;
            _bubbleText.BackColor = Color.FromArgb(27, 39, 64);
            _bubbleText.ForeColor = Theme.TextCol;
            _bubbleText.SetText(Model.Text);
            _bubbleText.HeightChanged += delegate { LayoutUser(); };
            _bubble.Controls.Add(_bubbleText);
            Controls.Add(_bubble);
            LayoutUser();
        }

        private static Image LoadImage(string dataUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(dataUrl)) return null;
                int comma = dataUrl.IndexOf(',');
                if (comma < 0) return null;
                byte[] bytes = Convert.FromBase64String(dataUrl.Substring(comma + 1));
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    Image img = Image.FromStream(ms);
                    return img;
                }
            }
            catch { return null; }
        }

        private void AddStackControl(Control c)
        {
            IGrowable g = c as IGrowable;
            if (g != null) g.HeightChanged += delegate { LayoutAssistant(); };
            _stack.Add(c);
            Controls.Add(c);
        }

        private void BuildAssistant()
        {
            for (int i = 0; i < Model.Blocks.Count; i++)
            {
                Control c = CreateBlockControl(Model.Blocks[i]);
                if (c == null) continue;
                AddStackControl(c);
            }
        }

        public static Control CreateBlockControl(Block b)
        {
            if (b.Kind == Block.KText)
            {
                MdView v = new MdView();
                v.BackColor = Theme.Bg;
                v.SetText(b.Text);
                b.Ui = v;
                b.RenderedText = b.Text;
                return v;
            }
            if (b.Kind == Block.KThinking)
            {
                ThinkingView t = new ThinkingView();
                t.SetText(b.Text);
                b.Ui = t;
                b.RenderedText = b.Text;
                return t;
            }
            ToolCardView card = new ToolCardView();
            card.SetTool(b.ToolName, b.Args);
            if (b.Output != null && b.Output.Length > 0) card.SetOutput(b.Output, b.IsError);
            else if (!b.Running) card.MarkDone();
            b.Ui = card;
            b.RenderedText = b.Output == null ? "" : b.Output;
            return card;
        }

        /// <summary>把模型里的内容同步到控件（增量）</summary>
        public void Sync()
        {
            if (_isUser)
            {
                if (_bubbleText != null && _bubbleText.RawText != Model.Text)
                {
                    _bubbleText.SetText(Model.Text);
                    LayoutUser();
                }
                return;
            }

            // 补齐新增的块
            while (_stack.Count < CountRenderable())
            {
                Block b = GetRenderable(_stack.Count);
                if (b == null) break;
                Control c = CreateBlockControl(b);
                if (c == null) break;
                AddStackControl(c);
            }

            int idx = 0;
            for (int i = 0; i < Model.Blocks.Count; i++)
            {
                Block b = Model.Blocks[i];
                if (b.Kind == Block.KTool && b.ToolName == null) continue;
                if (idx >= _stack.Count) break;
                Control c = _stack[idx];
                idx++;

                if (b.Kind == Block.KText)
                {
                    MdView v = c as MdView;
                    if (v != null && v.RawText != b.Text) { v.SetText(b.Text); b.RenderedText = b.Text; }
                }
                else if (b.Kind == Block.KThinking)
                {
                    ThinkingView t = c as ThinkingView;
                    if (t != null && b.Text != b.RenderedText) { t.SetText(b.Text); b.RenderedText = b.Text; }
                }
                else if (b.Kind == Block.KTool)
                {
                    ToolCardView card = c as ToolCardView;
                    if (card != null)
                    {
                        string outText = b.Output == null ? "" : b.Output;
                        if (b.Running)
                        {
                            if (outText != b.RenderedText) { card.SetRunningOutput(outText); b.RenderedText = outText; }
                        }
                        else if (!b.FinishedUi || outText != b.RenderedText)
                        {
                            card.SetOutput(outText, b.IsError);
                            b.RenderedText = outText;
                            b.FinishedUi = true;
                        }
                    }
                }
            }
            LayoutAssistant();
        }

        private int CountRenderable()
        {
            int n = 0;
            for (int i = 0; i < Model.Blocks.Count; i++)
            {
                Block b = Model.Blocks[i];
                if (b.Kind == Block.KTool && b.ToolName == null) continue;
                n++;
            }
            return n;
        }

        private Block GetRenderable(int index)
        {
            int n = 0;
            for (int i = 0; i < Model.Blocks.Count; i++)
            {
                Block b = Model.Blocks[i];
                if (b.Kind == Block.KTool && b.ToolName == null) continue;
                if (n == index) return b;
                n++;
            }
            return null;
        }

        private void LayoutUser()
        {
            if (_bubble == null || _inLayout) return;
            _inLayout = true;
            try { LayoutUserCore(); } finally { _inLayout = false; }
        }

        private void LayoutUserCore()
        {
            int maxBubble = Math.Max(220, (int)(Width * 0.78));
            int innerMax = Math.Max(120, maxBubble - 24);

            int imgH = 0;
            int imgW = 0;
            for (int i = 0; i < _images.Count; i++)
            {
                _images[i].Width = Math.Min(180, innerMax);
                _images[i].Height = 120;
                _images[i].Left = 12 + i * (_images[i].Width + 6);
                _images[i].Top = 10;
                imgW = _images[i].Right;
                imgH = 130;
            }

            _bubbleText.Width = innerMax;
            int textW = Math.Max(40, Math.Min(innerMax, _bubbleText.PreferredWidth + 14));
            if (_bubbleText.RawText.Length == 0) textW = 0;
            _bubbleText.Left = 12;
            _bubbleText.Top = imgH + 8;

            int contentW = Math.Max(textW, _images.Count > 0 ? Math.Min(innerMax, imgW) : 0);
            int bubbleW = Math.Min(maxBubble, contentW + 24);
            _bubbleText.Width = Math.Max(40, Math.Min(innerMax, bubbleW - 24));

            _bubble.Width = bubbleW;
            _bubble.Height = imgH + 8 + _bubbleText.Height + 10;
            _bubble.Left = Math.Max(0, Width - bubbleW - 4);
            _bubble.Top = 6;

            int want = _bubble.Bottom + 10;
            if (Height != want)
            {
                Height = want;
                EventHandler h = HeightChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private void LayoutAssistant()
        {
            if (_inLayout) return;
            _inLayout = true;
            try { LayoutAssistantCore(); } finally { _inLayout = false; }
        }

        private void LayoutAssistantCore()
        {
            int y = 6;
            int gutter = Gutter;
            int w = Math.Max(120, Width - gutter - 6);
            for (int i = 0; i < _stack.Count; i++)
            {
                Control c = _stack[i];
                if (c.Width != w) c.Width = w;
                c.Left = gutter;
                c.Top = y;
                y += c.Height + 6;
            }
            int want = Math.Max(40, y + 6);
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
            if (_isUser) LayoutUser(); else LayoutAssistant();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_isUser) return;
            Rectangle av = new Rectangle(2, 8, AvatarSize, AvatarSize);
            using (GraphicsPath p = Theme.RoundRect(av, 7))
            {
                if (_isUser)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(58, 68, 92))) g.FillPath(b, p);
                }
                else
                {
                    using (LinearGradientBrush b = new LinearGradientBrush(av, Theme.Accent, Theme.Accent2, 45f)) g.FillPath(b, p);
                }
            }
            g.SmoothingMode = SmoothingMode.Default;
            TextRenderer.DrawText(g, "π", Theme.UiSmall, av, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>对话区域（可滚动，贴底自动跟随）</summary>
    internal class ChatFlow : Panel
    {
        private readonly List<MessageView> _items = new List<MessageView>();
        private bool _layingOut;
        public event EventHandler Scrolled;

        public ChatFlow()
        {
            AutoScroll = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            Padding = new Padding(0);
        }

        public List<MessageView> Items { get { return _items; } }

        public void ClearAll()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Dispose();
            _items.Clear();
            Controls.Clear();
            AutoScrollMinSize = Size.Empty;
        }

        public void Add(MessageView v)
        {
            // 高度变化不在事件里同步重排（会导致递归/栈溢出），交给 60ms 的刷新循环
            v.HeightChanged += delegate { PendingLayout = true; };
            _items.Add(v);
            Controls.Add(v);
            Relayout();
        }

        /// <summary>有子控件通知需要重排</summary>
        public bool PendingLayout;

        public bool NearBottom
        {
            get
            {
                int total = AutoScrollMinSize.Height;
                if (total <= ClientSize.Height) return true;
                return (-AutoScrollPosition.Y) + ClientSize.Height >= total - 80;
            }
        }

        public void ScrollToBottom()
        {
            Relayout();
            AutoScrollPosition = new Point(0, AutoScrollMinSize.Height);
            Relayout();
        }

        public void Relayout()
        {
            if (_layingOut) return;
            _layingOut = true;
            PendingLayout = false;
            try
            {
                SuspendLayout();
                int pad = 26;
                int width = Math.Max(200, ClientSize.Width - pad * 2);
                if (width > 940) width = 940;
                int left = Math.Max(pad, (ClientSize.Width - width) / 2);
                int scrollY = AutoScrollPosition.Y;
                int y = 12;
                for (int i = 0; i < _items.Count; i++)
                {
                    MessageView v = _items[i];
                    if (v.Width != width) v.Width = width;
                    v.Left = left;
                    v.Top = y + scrollY;
                    y += v.Height + 4;
                }
                AutoScrollMinSize = new Size(0, y + 16);
            }
            finally
            {
                ResumeLayout(true);
                _layingOut = false;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            EventHandler h = Scrolled;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
