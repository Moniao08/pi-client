using System;
using System.IO;
using System.Net;
using System.Text;

namespace PiClient
{
    /// <summary>极简 HTTP 客户端（只用到 GET，跟随重定向）</summary>
    internal static class Http
    {
        private static bool _tlsReady;

        private static void EnsureTls()
        {
            if (_tlsReady) return;
            _tlsReady = true;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }
        }

        private static HttpWebRequest Create(string url, int timeoutMs, string accept)
        {
            EnsureTls();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "pi-client/" + Program.Version;
            req.Accept = accept ?? "*/*";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AllowAutoRedirect = true;
            req.MaximumAutomaticRedirections = 5;
            try { req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; }
            catch { }
            return req;
        }

        public static string GetString(string url, int timeoutMs, string accept)
        {
            using (HttpWebResponse res = (HttpWebResponse)Create(url, timeoutMs, accept).GetResponse())
            using (Stream s = res.GetResponseStream())
            using (StreamReader r = new StreamReader(s, Encoding.UTF8))
            {
                return r.ReadToEnd();
            }
        }

        public static byte[] GetBytes(string url, int timeoutMs)
        {
            using (HttpWebResponse res = (HttpWebResponse)Create(url, timeoutMs, null).GetResponse())
            using (Stream s = res.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[81920];
                int n;
                long total = 0;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    total += n;
                    if (total > 64L * 1024 * 1024) throw new IOException("文件过大");
                }
                return ms.ToArray();
            }
        }
    }
}
