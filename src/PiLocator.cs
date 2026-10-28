using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PiClient
{
    /// <summary>一个可用的 pi 安装</summary>
    internal class PiInstall
    {
        public string Home;      // 安装根目录（含 node.exe / node_modules 那一层）
        public string NodeExe;   // node.exe 路径（或 "node"）
        public string RpcEntry;  // dist\bundle\rpc-entry.js
        public string PiCmd;     // pi.cmd（找不到 rpc-entry 时用）
        public string Version;
        public string Source;    // 来源说明（自动检测/手动选择/配置文件）

        public bool IsRpc { get { return !string.IsNullOrEmpty(RpcEntry); } }

        public string Key
        {
            get { return (Home ?? "").ToLowerInvariant(); }
        }

        public string Display
        {
            get
            {
                string v = string.IsNullOrEmpty(Version) ? "" : " v" + Version;
                return "pi" + v + "   " + Home;
            }
        }
    }

    internal static class PiLocator
    {
        public static string InstallCommandText = "npm install -g --ignore-scripts @earendil-works/pi-coding-agent";
        public static string BashInstallText = "curl -fsSL https://pi.dev/install.sh | sh";

        /// <summary>按优先级探测本机所有可用的 pi，去重后返回</summary>
        public static List<PiInstall> Detect(Config cfg)
        {
            List<PiInstall> found = new List<PiInstall>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<string, string> add = delegate(string dir, string source)
            {
                PiInstall p = Inspect(dir);
                if (p == null) return;
                if (seen.Contains(p.Key)) return;
                seen.Add(p.Key);
                p.Source = source;
                found.Add(p);
            };

            if (cfg != null && !string.IsNullOrEmpty(cfg.PiHome)) add(cfg.PiHome, "配置文件");

            string local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            string roaming = Environment.GetEnvironmentVariable("APPDATA");
            string user = Environment.GetEnvironmentVariable("USERPROFILE");

            if (!string.IsNullOrEmpty(local)) add(Path.Combine(local, @"pi-node\current"), "官方安装器");
            if (!string.IsNullOrEmpty(roaming)) add(Path.Combine(roaming, @"npm"), "npm 全局");
            if (!string.IsNullOrEmpty(local)) add(Path.Combine(local, @"Programs\pi"), "Programs");
            if (!string.IsNullOrEmpty(user)) add(Path.Combine(user, @".pi\npm"), "pi 自更新目录");

            // PATH 里可能直接有 pi
            string[] dirs = SplitPath();
            for (int i = 0; i < dirs.Length; i++) add(dirs[i], "PATH");

            // 最后再问一次 where pi（可能被 shell alias/包装过）
            string where = RunWherePi();
            if (!string.IsNullOrEmpty(where)) add(where, "where pi");

            return found;
        }

        public static PiInstall Inspect(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            string full;
            try
            {
                full = Path.GetFullPath(dir.Trim().Trim('"').Trim());
                if (full.Length > 3) full = full.TrimEnd('\\');
            }
            catch
            {
                return null;
            }
            if (!Directory.Exists(full)) return null;

            string pkg = Path.Combine(full, @"node_modules\@earendil-works\pi-coding-agent");
            string entry = Path.Combine(pkg, @"dist\bundle\rpc-entry.js");
            if (!File.Exists(entry))
            {
                string altEntry = Path.Combine(full, @"dist\bundle\rpc-entry.js");
                if (File.Exists(altEntry))
                {
                    pkg = full;
                    entry = altEntry;
                }
                else
                {
                    entry = null;
                }
            }

            if (entry != null)
            {
                string node = Path.Combine(full, "node.exe");
                if (!File.Exists(node)) node = FindInPath("node.exe");
                if (node == null) node = "node";
                PiInstall p = new PiInstall();
                p.Home = full;
                p.NodeExe = node;
                p.RpcEntry = entry;
                p.Version = ReadVersion(pkg);
                return p;
            }

            string cmd = Path.Combine(full, "pi.cmd");
            if (File.Exists(cmd))
            {
                PiInstall p = new PiInstall();
                p.Home = full;
                p.PiCmd = cmd;
                p.Version = ReadVersion(pkg);
                return p;
            }
            return null;
        }

        private static string ReadVersion(string pkgDir)
        {
            try
            {
                string pj = Path.Combine(pkgDir, "package.json");
                if (!File.Exists(pj)) return null;
                Dictionary<string, object> d = Json.ParseObject(File.ReadAllText(pj, Encoding.UTF8));
                return Json.GetStr(d, "version");
            }
            catch
            {
                return null;
            }
        }

        public static string FindInPath(string fileName)
        {
            string[] dirs = SplitPath();
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    string p = Path.Combine(dirs[i], fileName);
                    if (File.Exists(p)) return p;
                }
                catch { }
            }
            return null;
        }

        private static string[] SplitPath()
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path)) return new string[0];
            return path.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string RunWherePi()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c where pi");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(4000);
                    string[] lines = outp.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        string first = lines[0].Trim();
                        if (File.Exists(first)) return Path.GetDirectoryName(first);
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>生成安装命令：优先用 pi 自带的 npm，其次 PATH 里的 npm</summary>
        public static string BuildInstallCommand(PiInstall hint)
        {
            string npm = null;
            if (hint != null && !string.IsNullOrEmpty(hint.Home))
            {
                string cand = Path.Combine(hint.Home, "npm.cmd");
                if (File.Exists(cand)) npm = cand;
            }
            if (npm == null) npm = FindInPath("npm.cmd");
            if (npm == null) npm = FindInPath("npm.exe");
            if (npm == null) return null;
            return "\"" + npm + "\" install -g --ignore-scripts @earendil-works/pi-coding-agent";
        }

        /// <summary>在新控制台窗口里执行安装命令，让用户能看到进度</summary>
        public static void RunInConsole(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = true;
            psi.Arguments = "/c start \"pi 安装\" cmd.exe /k \"" + command + "\"";
            try { Process.Start(psi); }
            catch { }
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }
    }
}
