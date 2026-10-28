using System;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace PiClient
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);

        private static void EnableDpiAwareness()
        {
            try
            {
                // -4 = PER_MONITOR_AWARE_V2（显示器用了 125% 缩放时才拿得到真实 DPI）
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        public static void Trace(string msg)
        {
            if (!TraceOn) return;
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trace.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n");
            }
            catch { }
        }

        public static bool TraceOn;

        public static void LogCrash(Exception ex)
        {
            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                    (ex == null ? "unknown" : ex.ToString()) + "\r\n\r\n");
            }
            catch { }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            string user = Environment.UserName;
            bool created;
            using (Mutex mutex = new Mutex(true, "pi-client-native-" + user, out created))
            {
                if (!created)
                {
                    MessageBox.Show("pi 客户端已经在运行了。", "pi 客户端", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--smoke" || args[i] == "--trace" || args[i] == "--shot") TraceOn = true;
                }

                EnableDpiAwareness();
                Application.EnableVisualStyles();
                Theme.Init(Dpi.DetectScale());
                Trace("dpi: " + Dpi.LastDebug + " => scale=" + Theme.Scale);
                Application.SetCompatibleTextRenderingDefault(false);

                // 写崩溃日志，方便排查（窗口程序没有控制台）
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    LogCrash(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    LogCrash(e.ExceptionObject as Exception);
                };

                MainForm form = new MainForm();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--smoke" && i + 1 < args.Length) form.SetSmokePrompt(args[i + 1]);
                    if (args[i] == "--setup") form.ForceSetup = true;
                    if (args[i] == "--shot") form.SetShotOnly();
                    if (args[i] == "--session" && i + 1 < args.Length) form.OpenSessionPath = args[i + 1];
                }
                try
                {
                    Trace("run form");
                    Application.Run(form);
                    Trace("form closed");
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}
