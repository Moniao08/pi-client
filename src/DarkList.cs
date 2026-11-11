using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>自绘列表（青绿滚动条，和对话区风格一致）</summary>
    internal class DarkList : Control
    {
        private readonly List<ListEntry> _items = new List<ListEntry>();
        private int _sel = -1;
        private int _scrollY;
        private int _hot = -1;
        private bool _drag;
        private int _dragGrab;
        private Rectangle _thumb;

        public int ItemHeight = 40;
        public event EventHandler SelectedIndexChanged;

        public DarkList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Panel;
            ForeColor = Theme.TextCol;
            Font = Theme.Ui;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public IList<ListEntry> Items { get { return _items; } }

        public int SelectedIndex
        {
            get { return _sel; }
            set
            {
                if (value < -1) value = -1;
                if (value >= _items.Count) value = _items.Count - 1;
                if (_sel == value) return;
                _sel = value;
                EnsureVisible();
                Invalidate();
                EventHandler h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public ListEntry SelectedItem
        {
            get { return _sel >= 0 && _sel < _items.Count ? _items[_sel] : null; }
        }

        private int S(int px)
        {
            return (int)Math.Round(px * Theme.Scale);
        }

        private int BarW { get { return S(8); } }

        public void SetEntries(List<ListEntry> entries)
        {
            ListEntry keep = SelectedItem;
            _items.Clear();
            if (entries != null) _items.AddRange(entries);
            _sel = -1;
            if (keep != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (ReferenceEquals(_items[i].Tag, keep.Tag)) { _sel = i; break; }
                }
            }
            _scrollY = 0;
            UpdateThumb();
            Invalidate();
        }

        private int ContentH { get { return _items.Count * ItemHeight; } }

        public void ScrollBy(int pixels)
        {
            int max = Math.Max(0, ContentH - ClientSize.Height);
            int y = _scrollY + pixels;
            if (y < 0) y = 0;
            if (y > max) y = max;
            if (y == _scrollY) return;
            _scrollY = y;
            UpdateThumb();
            Invalidate();
        }

        private void EnsureVisible()
        {
            if (_sel < 0) return;
            int top = _sel * ItemHeight;
            int bottom = top + ItemHeight;
            if (top < _scrollY) ScrollBy(top - _scrollY);
            else if (bottom > _scrollY + ClientSize.Height) ScrollBy(bottom - _scrollY - ClientSize.Height);
        }

        private void UpdateThumb()
        {
            if (ContentH <= ClientSize.Height + 1)
            {
                _thumb = Rectangle.Empty;
                return;
            }
            int trackTop = S(4);
            int trackH = Math.Max(S(20), ClientSize.Height - S(8));
            int thumbH = Math.Max(S(28), (int)((long)ClientSize.Height * trackH / ContentH));
            int max = Math.Max(1, ContentH - ClientSize.Height);
            int y = trackTop + (int)((long)(trackH - thumbH) * _scrollY / max);
            _thumb = new Rectangle(ClientSize.Width - BarW - S(2), y, BarW, thumbH);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateThumb();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Panel);

            int top = S(1);
            for (int i = 0; i < _items.Count; i++)
            {
                int y = top + i * ItemHeight - _scrollY;
                if (y + ItemHeight < 0 || y > ClientSize.Height) continue;
                ListEntry it = _items[i];
                if (it == null) continue;
                Rectangle r = new Rectangle(S(2), y, ClientSize.Width - BarW - S(4), ItemHeight - S(2));
                if (i == _sel) Theme.DrawRound(g, r, 7, Theme.Panel3, Color.FromArgb(46, Theme.Accent));
                else if (i == _hot) Theme.DrawRound(g, r, 7, Theme.Panel2, Color.Empty);

                int x = r.X + S(10);
                if (it.Dot.A > 0)
                {
                    using (SolidBrush b = new SolidBrush(it.Dot))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.FillEllipse(b, x, y + S(13), S(6), S(6));
                        g.SmoothingMode = SmoothingMode.Default;
                    }
                    x += S(12);
                }
                Font f = it.Bold ? Theme.UiBold : Theme.Ui;
                TextRenderer.DrawText(g, it.Title, f,
                    new Rectangle(x, y + S(5), r.Right - x - S(8), S(18)), Theme.TextCol,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, it.Sub, Theme.UiSmall,
                    new Rectangle(r.X + S(10), y + S(22), r.Width - S(18), S(15)),
                    i == _sel ? Theme.Muted : Theme.Muted2,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }

            if (!_thumb.IsEmpty)
            {
                Theme.FillRound(g, _thumb, BarW / 2, (_drag || _hot >= 0) ? Theme.ScrollThumbHot : Theme.ScrollThumb);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(-e.Delta * ItemHeight / 120 * 3);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag)
            {
                int trackTop = S(4);
                int trackH = Math.Max(S(20), ClientSize.Height - S(8));
                int span = Math.Max(1, trackH - _thumb.Height);
                int y = e.Y - _dragGrab - trackTop;
                if (y < 0) y = 0;
                if (y > span) y = span;
                int max = Math.Max(1, ContentH - ClientSize.Height);
                _scrollY = (int)((long)y * max / span);
                UpdateThumb();
                Invalidate();
                return;
            }
            int idx = (e.Y + _scrollY - S(1)) / ItemHeight;
            if (idx >= _items.Count || e.X > ClientSize.Width - BarW) idx = -1;
            if (idx != _hot)
            {
                _hot = idx;
                Invalidate();
            }
            Cursor = e.X > ClientSize.Width - BarW ? Cursors.Default : Cursors.Hand;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hot != -1)
            {
                _hot = -1;
                Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_thumb.IsEmpty && e.X >= _thumb.Left - S(4))
            {
                if (_thumb.Contains(e.Location))
                {
                    _drag = true;
                    _dragGrab = e.Y - _thumb.Top;
                    return;
                }
                ScrollBy(e.Y < _thumb.Top ? -ClientSize.Height : ClientSize.Height);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                int idx = (e.Y + _scrollY - S(1)) / ItemHeight;
                if (idx >= 0 && idx < _items.Count) SelectedIndex = idx;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _drag = false;
            base.OnMouseUp(e);
        }
    }
}
