using System;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>
    /// 命中测试透明的面板/标签：鼠标点在这上面时，命中测试会继续找下层的窗口，
    /// 从而让主窗口返回 HTCAPTION —— 这样才能拖动无边框窗口、双击最大化。
    /// </summary>
    internal class DragPanel : Panel
    {
        public DragPanel() : this(true) { }

        public DragPanel(bool customPaint)
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

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCHITTEST)
            {
                m.Result = (IntPtr)(-1); // HTTRANSPARENT
                return;
            }
            base.WndProc(ref m);
        }
    }

    internal class DragLabel : Label
    {
        /// <summary>true 时鼠标命中测试穿透到主窗口（顶栏标题用），false 时正常响应点击</summary>
        public bool TransparentToHitTest = true;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_NCHITTEST && TransparentToHitTest)
            {
                m.Result = (IntPtr)(-1); // HTTRANSPARENT
                return;
            }
            base.WndProc(ref m);
        }
    }
}
