using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>对话区域：自绘滚动条 + 贴底自动跟随（不再用系统滚动条，风格统一）</summary>
    internal class ChatFlow : Panel
    {
        private readonly List<MessageView> _items = new List<MessageView>();
        private bool _layingOut;
        private int _scrollY;
        private int _contentH;
        private bool _dragThumb;
        private int _dragGrab;
        private bool _hoverThumb;
        private Rectangle _thumbRect;

        public event EventHandler Scrolled;

        public ChatFlow()
        {
            AutoScroll = false;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(0);
        }

        public List<MessageView> Items { get { return _items; } }

        /// <summary>有子控件通知需要重排</summary>
        public bool PendingLayout;

        private int S(int px)
        {
            return (int)Math.Round(px * Theme.Scale);
        }

        private int BarW { get { return S(10); } }

        public void ClearAll()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Dispose();
            _items.Clear();
            Controls.Clear();
            _contentH = 0;
            _scrollY = 0;
            Invalidate();
        }

        public void Add(MessageView v)
        {
            // 高度变化不在事件里同步重排（会导致递归/栈溢出），交给 60ms 的刷新循环
            v.HeightChanged += delegate { PendingLayout = true; };
            _items.Add(v);
            Controls.Add(v);
            Relayout();
        }

        public bool NearBottom
        {
            get
            {
                if (_contentH <= ClientSize.Height) return true;
                return _scrollY + ClientSize.Height >= _contentH - S(80);
            }
        }

        public void ScrollToBottom()
        {
            Relayout();
            _scrollY = Math.Max(0, _contentH - ClientSize.Height);
            Relayout();
        }

        /// <summary>滚轮/按键滚动（delta 为像素，负数向上）</summary>
        public void ScrollBy(int delta)
        {
            int max = Math.Max(0, _contentH - ClientSize.Height);
            int y = _scrollY + delta;
            if (y < 0) y = 0;
            if (y > max) y = max;
            if (y == _scrollY) return;
            _scrollY = y;
            Relayout();
            EventHandler h = Scrolled;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void Relayout()
        {
            if (_layingOut) return;
            _layingOut = true;
            PendingLayout = false;
            try
            {
                SuspendLayout();
                int pad = S(26);
                int width = Math.Max(S(200), ClientSize.Width - pad * 2 - BarW);
                if (width > S(940)) width = S(940);
                int left = Math.Max(pad, (ClientSize.Width - BarW - width) / 2);
                int y = S(12);
                for (int i = 0; i < _items.Count; i++)
                {
                    MessageView v = _items[i];
                    if (v.Width != width) v.Width = width;
                    v.Left = left;
                    v.Top = y - _scrollY;
                    y += v.Height + S(4);
                }
                _contentH = y + S(16);
                int max = Math.Max(0, _contentH - ClientSize.Height);
                if (_scrollY > max) _scrollY = max;
                if (_scrollY < 0) _scrollY = 0;
                UpdateThumb();
            }
            finally
            {
                ResumeLayout(true);
                _layingOut = false;
            }
        }

        private void UpdateThumb()
        {
            if (_contentH <= ClientSize.Height + 1)
            {
                _thumbRect = Rectangle.Empty;
                return;
            }
            int trackTop = S(6);
            int trackH = ClientSize.Height - S(12);
            int thumbH = Math.Max(S(36), (int)((long)ClientSize.Height * trackH / _contentH));
            int maxScroll = Math.Max(1, _contentH - ClientSize.Height);
            int y = trackTop + (int)((long)(trackH - thumbH) * _scrollY / maxScroll);
            _thumbRect = new Rectangle(ClientSize.Width - BarW - S(2), y, BarW, thumbH);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_thumbRect.IsEmpty) return;
            bool hot = _hoverThumb || _dragThumb;
            Theme.FillRound(e.Graphics, _thumbRect, BarW / 2,
                hot ? Theme.ScrollThumbHot : Theme.ScrollThumb);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(-e.Delta * S(60) / 120 * 3);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_thumbRect.IsEmpty)
            {
                if (_thumbRect.Contains(e.Location))
                {
                    _dragThumb = true;
                    _dragGrab = e.Y - _thumbRect.Top;
                    return;
                }
                if (e.X >= _thumbRect.Left - S(6))
                {
                    // 点在轨道上：翻页
                    ScrollBy(e.Y < _thumbRect.Top ? -ClientSize.Height + S(60) : ClientSize.Height - S(60));
                    return;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragThumb)
            {
                int trackTop = S(6);
                int trackH = ClientSize.Height - S(12);
                int thumbH = _thumbRect.Height;
                int span = Math.Max(1, trackH - thumbH);
                int y = e.Y - _dragGrab - trackTop;
                if (y < 0) y = 0;
                if (y > span) y = span;
                int max = Math.Max(1, _contentH - ClientSize.Height);
                _scrollY = (int)((long)y * max / span);
                Relayout();
                Invalidate();
                return;
            }

            bool hot = !_thumbRect.IsEmpty && e.X >= _thumbRect.Left - S(6);
            if (hot != _hoverThumb)
            {
                _hoverThumb = hot;
                Cursor = hot ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragThumb = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverThumb && !_dragThumb)
            {
                _hoverThumb = false;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }
    }
}
