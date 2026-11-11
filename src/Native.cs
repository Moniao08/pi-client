using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PiClient
{
    /// <summary>Win32 小工具：无边框窗口的命中测试、最大化边界、暗色滚动条等</summary>
    internal static class Native
    {
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_GETMINMAXINFO = 0x0024;
        public const int WM_MOUSEWHEEL = 0x020A;

        public const int HTCLIENT = 1;
        public const int HTCAPTION = 2;
        public const int HTLEFT = 10;
        public const int HTRIGHT = 11;
        public const int HTTOP = 12;
        public const int HTTOPLEFT = 13;
        public const int HTTOPRIGHT = 14;
        public const int HTBOTTOM = 15;
        public const int HTBOTTOMLEFT = 16;
        public const int HTBOTTOMRIGHT = 17;

        private const int GCL_STYLE = -26;
        private const int CS_DROPSHADOW = 0x00020000;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        private static extern int SetClassLong(IntPtr hWnd, int index, int value);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subApp, string subIdList);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        /// <summary>屏幕坐标下最深的窗口句柄</summary>
        public static IntPtr WindowFromPoint(Point screenPoint)
        {
            POINT p;
            p.X = screenPoint.X;
            p.Y = screenPoint.Y;
            return WindowFromPoint(p);
        }

        /// <summary>给窗口加一层阴影（无边框窗口默认没有）</summary>
        public static void AddDropShadow(IntPtr hwnd)
        {
            try { SetClassLong(hwnd, GCL_STYLE, CS_DROPSHADOW); }
            catch { }
        }

        /// <summary>系统标题栏深色（保留旧接口，无边框后基本用不到）</summary>
        public static void EnableDarkTitleBar(IntPtr hwnd)
        {
            try
            {
                int on = 1;
                int attr = Environment.OSVersion.Version.Build >= 18985 ? 20 : 19;
                DwmSetWindowAttribute(hwnd, attr, ref on, 4);
            }
            catch { }
        }

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        private static extern int SetPreferredAppMode(int appMode);

        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        private static extern void FlushMenuThemes();

        /// <summary>让系统控件（滚动条、菜单、列表框）进入暗色模式（Win10 1903+ 未公开接口）</summary>
        public static void EnableDarkMode()
        {
            try
            {
                SetPreferredAppMode(2);   // 2 = ForceDark
                FlushMenuThemes();
            }
            catch { }
        }

        /// <summary>让滚动条跟随深色主题（Win10 1809+ 的 DarkMode_Explorer）</summary>
        public static void DarkScrollBar(IntPtr hwnd)
        {
            try { SetWindowTheme(hwnd, "DarkMode_Explorer", null); }
            catch { }
        }

        /// <summary>把最大化尺寸限制在工作区内（无边框窗口否则会盖住任务栏）</summary>
        public static void ConstrainToWorkArea(IntPtr hwnd, IntPtr lParam)
        {
            try
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(mon, ref mi))
                {
                    mmi.ptMaxPosition.X = mi.rcWork.Left - mi.rcMonitor.Left;
                    mmi.ptMaxPosition.Y = mi.rcWork.Top - mi.rcMonitor.Top;
                    mmi.ptMaxSize.X = mi.rcWork.Right - mi.rcWork.Left;
                    mmi.ptMaxSize.Y = mi.rcWork.Bottom - mi.rcWork.Top;
                    Marshal.StructureToPtr(mmi, lParam, false);
                }
            }
            catch { }
        }

        public static Point LParamToPoint(IntPtr lParam)
        {
            int v = lParam.ToInt32();
            return new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
        }
    }
}
