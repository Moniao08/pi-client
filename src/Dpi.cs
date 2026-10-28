using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>
    /// 决定是否要手工放大 UI。
    /// 有些环境系统设了 125%/150% 缩放，但窗口 DPI 仍然报 96（DWM 只缩放不告知），
    /// 这时需要自己按比例放大字号与布局，否则界面会显得很小。
    /// </summary>
    internal static class Dpi
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        private static int PhysicalWidth()
        {
            try
            {
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, -1, ref dm)) return dm.dmPelsWidth;
            }
            catch { }
            return 0;
        }

        private static int RegistryDpi()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop\\WindowMetrics"))
                {
                    object v = k == null ? null : k.GetValue("AppliedDPI");
                    if (v != null) return Convert.ToInt32(v);
                }
            }
            catch { }
            return 96;
        }

        public static string LastDebug = "";

        public static float DetectScale()
        {
            try
            {
                int applied = RegistryDpi();
                if (applied < 72) applied = 96;

                int wdpi = 96;
                try
                {
                    // 用屏幕 DC 的 DPI（和实际绘制文本时的 DPI 一致）
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    {
                        int d = (int)Math.Round(g.DpiX);
                        if (d >= 72) wdpi = d;
                    }
                }
                catch { }

                int phys = PhysicalWidth();
                int view = Screen.PrimaryScreen.Bounds.Width;
                if (phys > 0 && view > 0)
                {
                    // 物理宽 > 程序看到的宽 ⇒ 进程已被系统位图缩放，不能再放大
                    if (phys / (float)view > 1.02f) return 1f;
                }

                LastDebug = "applied=" + applied + " wdpi=" + wdpi + " phys=" + phys + " view=" + view;
                float s = applied / (float)Math.Max(96, wdpi);
                if (s < 1f) s = 1f;
                if (s > 3f) s = 3f;
                return s;
            }
            catch
            {
                return 1f;
            }
        }
    }
}
