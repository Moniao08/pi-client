using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PiClient
{
    /// <summary>一个 pi --mode rpc 子进程 + JSONL 协议封装</summary>
    internal class PiRpc : IDisposable
    {
        private Process _proc;
        private StreamWriter _in;
        private readonly object _inLock = new object();
        private readonly Dictionary<string, TaskCompletionSource<Dictionary<string, object>>> _pending =
            new Dictionary<string, TaskCompletionSource<Dictionary<string, object>>>();
        private int _seq;
        private volatile bool _dead;

        public event Action<Dictionary<string, object>> Evt;
        public event Action<string> ErrLine;
        public event Action<int> Exited;

        public bool Alive { get { return !_dead && _proc != null && !_proc.HasExited; } }
        public int Pid { get { return _proc != null ? _proc.Id : 0; } }
        public string Cwd;

        private static readonly Dictionary<string, int> Timeouts = BuildTimeouts();

        private static Dictionary<string, int> BuildTimeouts()
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            d["compact"] = 900000;
            d["bash"] = 600000;
            d["abort"] = 120000;
            d["export_html"] = 180000;
            d["get_messages"] = 180000;
            d["get_entries"] = 180000;
            d["new_session"] = 180000;
            d["switch_session"] = 180000;
            d["prompt"] = 120000;
            return d;
        }

        public void Start(PiInstall pi, string cwd, string sessionPath, string name)
        {
            Cwd = cwd;
            ProcessStartInfo psi = new ProcessStartInfo();
            string extra = "";
            if (!string.IsNullOrEmpty(sessionPath)) extra += " --session " + Quote(sessionPath);
            if (!string.IsNullOrEmpty(name)) extra += " --name " + Quote(name);

            if (pi.IsRpc)
            {
                psi.FileName = pi.NodeExe;
                psi.Arguments = Quote(pi.RpcEntry) + extra;
            }
            else
            {
                // 只有 pi.cmd 时借道 cmd.exe（注意 cmd /c 的双重引号规则）
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c \"\"" + pi.PiCmd + "\" --mode rpc" + extra + "\"";
            }

            psi.WorkingDirectory = cwd;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            psi.EnvironmentVariables["NO_COLOR"] = "1";
            psi.EnvironmentVariables["FORCE_COLOR"] = "0";

            _proc = new Process();
            _proc.StartInfo = psi;
            _proc.Start();
            _in = new StreamWriter(_proc.StandardInput.BaseStream, new UTF8Encoding(false));
            _in.AutoFlush = true;

            Thread tOut = new Thread(ReadStdout);
            tOut.IsBackground = true;
            tOut.Start();

            Thread tErr = new Thread(ReadStderr);
            tErr.IsBackground = true;
            tErr.Start();
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\"", "\\\"") + "\"";
        }

        private void ReadStdout()
        {
            try
            {
                StreamReader r = _proc.StandardOutput;
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    Dictionary<string, object> obj;
                    try { obj = Json.ParseObject(line); }
                    catch { continue; }
                    if (obj == null) continue;
                    string type = Json.GetStr(obj, "type");
                    if (type == "response")
                    {
                        string id = Json.GetStr(obj, "id");
                        TaskCompletionSource<Dictionary<string, object>> tcs = null;
                        if (!string.IsNullOrEmpty(id))
                        {
                            lock (_pending)
                            {
                                if (_pending.TryGetValue(id, out tcs)) _pending.Remove(id);
                            }
                        }
                        if (tcs != null) { tcs.TrySetResult(obj); continue; }
                    }
                    Action<Dictionary<string, object>> h = Evt;
                    if (h != null)
                    {
                        try { h(obj); } catch { }
                    }
                }
            }
            catch { }
            finally
            {
                MarkDead();
            }
        }

        private void ReadStderr()
        {
            try
            {
                StreamReader r = _proc.StandardError;
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    Action<string> h = ErrLine;
                    if (h != null) { try { h(line); } catch { } }
                }
            }
            catch { }
        }

        private void MarkDead()
        {
            if (_dead) return;
            _dead = true;
            List<TaskCompletionSource<Dictionary<string, object>>> all = new List<TaskCompletionSource<Dictionary<string, object>>>();
            lock (_pending)
            {
                all.AddRange(_pending.Values);
                _pending.Clear();
            }
            for (int i = 0; i < all.Count; i++) all[i].TrySetException(new Exception("pi 进程已退出"));
            int code = 0;
            try { if (_proc != null && _proc.HasExited) code = _proc.ExitCode; } catch { }
            Action<int> h = Exited;
            if (h != null) { try { h(code); } catch { } }
        }

        public void Send(Dictionary<string, object> cmd)
        {
            if (_dead || _in == null) throw new Exception("pi 进程不可用");
            string text = Json.Stringify(cmd);
            lock (_inLock)
            {
                _in.Write(text);
                _in.Write("\n");
                _in.Flush();
            }
        }

        public Task<Dictionary<string, object>> Request(Dictionary<string, object> cmd, int timeoutMs)
        {
            string type = Json.GetStr(cmd, "type");
            TaskCompletionSource<Dictionary<string, object>> tcs = new TaskCompletionSource<Dictionary<string, object>>();

            if (type == "extension_ui_response")
            {
                try { Send(cmd); tcs.TrySetResult(null); }
                catch (Exception ex) { tcs.TrySetException(ex); }
                return tcs.Task;
            }

            if (_dead)
            {
                tcs.TrySetException(new Exception("pi 进程已退出"));
                return tcs.Task;
            }

            string id = "c" + (++_seq).ToString();
            cmd["id"] = id;
            lock (_pending) { _pending[id] = tcs; }

            try { Send(cmd); }
            catch (Exception ex)
            {
                lock (_pending) { _pending.Remove(id); }
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            int ms = timeoutMs;
            if (ms <= 0)
            {
                int v;
                if (Timeouts.TryGetValue(type, out v)) ms = v;
                else ms = 180000;
            }

            Task.Delay(ms).ContinueWith(delegate
            {
                TaskCompletionSource<Dictionary<string, object>> p = null;
                lock (_pending)
                {
                    if (_pending.TryGetValue(id, out p)) _pending.Remove(id);
                }
                if (p != null) p.TrySetException(new TimeoutException("请求超时（" + type + "）"));
            });

            return tcs.Task;
        }

        public void Dispose()
        {
            if (_proc == null) return;
            try
            {
                if (!_proc.HasExited)
                {
                    // node 会再派生子进程，整棵树一起杀
                    ProcessStartInfo psi = new ProcessStartInfo("taskkill.exe", "/T /F /PID " + _proc.Id);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (Process k = Process.Start(psi)) { if (k != null) k.WaitForExit(3000); }
                }
            }
            catch { }
            try { _proc.Dispose(); } catch { }
            _proc = null;
            _dead = true;
        }
    }
}
