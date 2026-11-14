using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace PiClient
{
    /// <summary>从 Gitee（主）/ GitHub（备）检查更新并自更新</summary>
    internal static class Updater
    {
        public const string GiteeOwner = "zhang-yj08";
        public const string GithubOwner = "Moniao08";
        public const string Repo = "pi-client";

        internal class Release
        {
            public string Version = "";
            public string Name = "";
            public string Body = "";
            public string ExeUrl;
            public string ZipUrl;
            public string PageUrl = "";
            public string Source = "";

            public bool HasDownload
            {
                get { return !string.IsNullOrEmpty(ExeUrl) || !string.IsNullOrEmpty(ZipUrl); }
            }
        }

        public static Release CheckLatest(int timeoutMs)
        {
            Release r = TryGitee(timeoutMs);
            if (r != null) return r;
            return TryGithub(timeoutMs);
        }

        private static Release TryGitee(int timeoutMs)
        {
            try
            {
                string url = string.Format("https://gitee.com/api/v5/repos/{0}/{1}/releases/latest", GiteeOwner, Repo);
                string json = Http.GetString(url, timeoutMs, "application/json");
                System.Collections.Generic.Dictionary<string, object> d = Json.ParseObject(json);
                if (d == null) return null;
                Release r = new Release();
                r.Source = "Gitee";
                r.Version = Json.GetStr(d, "tag_name", "");
                r.Name = Json.GetStr(d, "name", r.Version);
                r.Body = Json.GetStr(d, "body", "");
                r.PageUrl = string.Format("https://gitee.com/{0}/{1}/releases", GiteeOwner, Repo);
                System.Collections.Generic.List<object> assets = Json.Arr(Get(d, "assets"));
                for (int i = 0; i < assets.Count; i++)
                {
                    System.Collections.Generic.Dictionary<string, object> a = Json.Obj(assets[i]);
                    if (a == null) continue;
                    string name = (Json.GetStr(a, "name", "") ?? "").ToLowerInvariant();
                    string dl = Json.GetStr(a, "browser_download_url");
                    if (string.IsNullOrEmpty(dl)) continue;
                    if (r.ExeUrl == null && name.EndsWith(".exe")) r.ExeUrl = dl;
                    else if (r.ZipUrl == null && name.EndsWith(".zip") && name.IndexOf("pi-client") >= 0) r.ZipUrl = dl;
                }
                return r.Version.Length > 0 ? r : null;
            }
            catch
            {
                return null;
            }
        }

        private static Release TryGithub(int timeoutMs)
        {
            try
            {
                string url = string.Format("https://api.github.com/repos/{0}/{1}/releases/latest", GithubOwner, Repo);
                string json = Http.GetString(url, timeoutMs, "application/vnd.github+json");
                System.Collections.Generic.Dictionary<string, object> d = Json.ParseObject(json);
                if (d == null) return null;
                Release r = new Release();
                r.Source = "GitHub";
                r.Version = Json.GetStr(d, "tag_name", "");
                r.Name = Json.GetStr(d, "name", r.Version);
                r.Body = Json.GetStr(d, "body", "");
                r.PageUrl = Json.GetStr(d, "html_url", "");
                System.Collections.Generic.List<object> assets = Json.Arr(Get(d, "assets"));
                for (int i = 0; i < assets.Count; i++)
                {
                    System.Collections.Generic.Dictionary<string, object> a = Json.Obj(assets[i]);
                    if (a == null) continue;
                    string name = (Json.GetStr(a, "name", "") ?? "").ToLowerInvariant();
                    string dl = Json.GetStr(a, "browser_download_url");
                    if (string.IsNullOrEmpty(dl)) continue;
                    if (r.ExeUrl == null && name.EndsWith(".exe")) r.ExeUrl = dl;
                    else if (r.ZipUrl == null && name.EndsWith(".zip")) r.ZipUrl = dl;
                }
                return r.Version.Length > 0 ? r : null;
            }
            catch
            {
                return null;
            }
        }

        private static object Get(System.Collections.Generic.Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        /* ---------------- 版本比较 ---------------- */

        private static int[] Parts(string v)
        {
            int[] n = new int[3];
            if (string.IsNullOrEmpty(v)) return n;
            string s = v.Trim();
            if (s.StartsWith("v") || s.StartsWith("V")) s = s.Substring(1);
            string[] seg = s.Split('.');
            for (int i = 0; i < 3 && i < seg.Length; i++)
            {
                string digits = "";
                for (int k = 0; k < seg[i].Length; k++)
                {
                    if (char.IsDigit(seg[i][k])) digits += seg[i][k];
                    else break;
                }
                int x;
                if (int.TryParse(digits, out x)) n[i] = x;
            }
            return n;
        }

        public static bool IsNewer(string latest, string current)
        {
            int[] a = Parts(latest);
            int[] b = Parts(current);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
            return false;
        }

        /* ---------------- 下载并自更新 ---------------- */

        public static string DownloadInstaller(Release rel, Action<int> progress)
        {
            if (rel == null) return null;
            string dir = Path.Combine(Path.GetTempPath(), "pi-client-update");
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "pi-client.exe");
            if (!string.IsNullOrEmpty(rel.ExeUrl))
            {
                byte[] bytes = Http.GetBytes(rel.ExeUrl, 180000);
                if (bytes == null || bytes.Length < 20000 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
                    throw new Exception("下载到的不是有效的程序文件（可能被拦截或返回了网页）");
                File.WriteAllBytes(target, bytes);
                if (progress != null) progress(bytes.Length);
                return target;
            }
            throw new Exception("该版本没有提供 exe 附件，请到发布页手动下载");
        }

        /// <summary>
        /// 生成并启动替换脚本：等本进程退出 → 覆盖 exe → 重新启动。
        /// 返回 null 表示已交给脚本处理，调用方应退出程序。
        /// </summary>
        public static string ApplyUpdate(string newExePath, string currentExePath)
        {
            string dir = Path.GetDirectoryName(newExePath);
            string bat = Path.Combine(dir, "apply-update.cmd");
            string log = Path.Combine(dir, "apply-update.log");
            StringBuilder sb = new StringBuilder();
            sb.Append("@echo off\r\n");
            sb.Append("ping 127.0.0.1 -n 3 >nul\r\n");
            sb.Append("taskkill /F /IM pi-client.exe >nul 2>&1\r\n");
            sb.Append("ping 127.0.0.1 -n 2 >nul\r\n");
            sb.AppendFormat("copy /Y \"{0}\" \"{1}\" >nul 2>&1\r\n", newExePath, currentExePath);
            sb.AppendFormat("if errorlevel 1 echo COPY_FAILED> \"{0}\"\r\n", log);
            sb.AppendFormat("start \"\" \"{0}\"\r\n", currentExePath);
            sb.AppendFormat("del /Q \"{0}\" >nul 2>&1\r\n", bat);
            try
            {
                File.WriteAllText(bat, sb.ToString(), Encoding.Default);
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>上次更新是否失败（脚本写了标记文件）</summary>
        public static bool LastApplyFailed()
        {
            try
            {
                return File.Exists(Path.Combine(Path.Combine(Path.GetTempPath(), "pi-client-update"), "apply-update.log"));
            }
            catch { return false; }
        }

        public static void ClearFailedFlag()
        {
            try
            {
                string p = Path.Combine(Path.Combine(Path.GetTempPath(), "pi-client-update"), "apply-update.log");
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }
    }
}
