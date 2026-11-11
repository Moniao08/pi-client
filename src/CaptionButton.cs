using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PiClient
{
    internal enum CaptionKind
    {
        Minimize,
        Maximize,
        Restore,
        Close
    }

    /// <summary>自绘窗口按钮（最小化 / 最大化 / 关闭）</summary>
    internal class CaptionButton : Control
    {
        public CaptionKind Kind;
        private bool _hover;

        public CaptionButton(CaptionKind kind)
        {
            Kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            TabStop = false;
            Height = 30;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.None;

            bool close = Kind == CaptionKind.Close;
            if (_hover)
            {
                using (SolidBrush b = new SolidBrush(close ? Color.FromArgb(196, 64, 64) : Theme.Panel3))
                {
                    g.FillRectangle(b, ClientRectangle);
                }
            }

            Color c = _hover && close ? Color.White : Theme.Muted;
            float w = Math.Max(1f, Theme.Scale);
            using (Pen pen = new Pen(c, w))
            {
                pen.StartCap = LineCap.Square;
                pen.EndCap = LineCap.Square;
                int cx = Width / 2;
                int cy = Height / 2;
                int r = (int)Math.Round(5 * Theme.Scale);
                int r2 = (int)Math.Round(4 * Theme.Scale);
                switch (Kind)
                {
                    case CaptionKind.Minimize:
                        g.DrawLine(pen, cx - r, cy, cx + r, cy);
                        break;
                    case CaptionKind.Maximize:
                        g.DrawRectangle(pen, cx - r, cy - r, r * 2, r * 2);
                        break;
                    case CaptionKind.Restore:
                        g.DrawRectangle(pen, cx - r, cy - r + r2, r * 2 - r2, r * 2 - r2);
                        g.DrawRectangle(pen, cx - r + r2, cy - r, r * 2 - r2, r * 2 - r2);
                        break;
                    default:
                        g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                        g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                        break;
                }
            }
        }
    }
}
