using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PiClient
{
    /// <summary>一个对话会话：pi 子进程 + 消息模型</summary>
    internal class Session
    {
        public string Cwd;
        public string SessionPath;
        public string Name;
        public bool Dead;
        public bool Streaming;
        public bool NeedsRebuild;
        public int Revision;

        public List<ChatMessage> Messages = new List<ChatMessage>();
        public Dictionary<string, Block> ToolIndex = new Dictionary<string, Block>();
        public List<Dictionary<string, object>> Models = new List<Dictionary<string, object>>();
        public Dictionary<string, object> Model;
        public List<string> Levels = new List<string>();
        public string ThinkingLevel = "off";
        public List<Dictionary<string, object>> Commands = new List<Dictionary<string, object>>();

        public bool HasStats;
        public long CtxTokens;
        public long CtxWindow;
        public double CtxPercent;
        public double Cost;

        public List<string> Steering = new List<string>();
        public List<string> FollowUp = new List<string>();
        public Dictionary<string, string> Statuses = new Dictionary<string, string>();

        public PiRpc Rpc;

        private ChatMessage _live;
        private readonly List<string> _pendingUsers = new List<string>();

        public event Action<Session> Changed;
        public event Action<Session, string, string> Toast;
        public event Action<Session, Dictionary<string, object>> ExtUi;
        public event Action<Session> DeadChanged;

        public string Title
        {
            get
            {
                if (!string.IsNullOrEmpty(Name)) return Name;
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (Messages[i].Role == "user" && Messages[i].Text.Length > 0)
                        return Json.OneLine(Messages[i].Text, 42);
                }
                return "新对话";
            }
        }

        public void Start(PiInstall pi, string cwd, string sessionPath)
        {
            Cwd = cwd;
            SessionPath = sessionPath;
            Rpc = new PiRpc();
            Rpc.Cwd = cwd;
            Rpc.Evt += OnEvent;
            Rpc.Exited += delegate(int code)
            {
                Dead = true;
                Streaming = false;
                Action<Session> h = DeadChanged;
                if (h != null) h(this);
                Raise();
            };
            Rpc.Start(pi, cwd, sessionPath, null);
        }

        private void Raise()
        {
            Revision++;
            Action<Session> h = Changed;
            if (h != null) h(this);
        }

        private void RaiseToast(string msg, string kind)
        {
            Action<Session, string, string> h = Toast;
            if (h != null) h(this, msg, kind);
        }

        private static object Value(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        private static double GetDouble(Dictionary<string, object> d, string key)
        {
            object v = Value(d, key);
            if (v == null) return 0;
            try { return Convert.ToDouble(v); }
            catch { return 0; }
        }

        /* ---------------- 命令 ---------------- */
        private async Task<Dictionary<string, object>> Cmd(Dictionary<string, object> cmd, int timeout)
        {
            Dictionary<string, object> res = await Rpc.Request(cmd, timeout);
            if (res == null) return null;
            if (!Json.GetBool(res, "success", true))
                throw new Exception(Json.GetStr(res, "error", "命令执行失败"));
            return Json.Obj(Value(res, "data"));
        }

        public async Task InitializeAsync()
        {
            try
            {
                Dictionary<string, object> st = await Cmd(Json.NewCmd("get_state"), 120000);
                ApplyState(st);
            }
            catch (Exception ex) { RaiseToast("读取状态失败：" + ex.Message, "error"); }

            try
            {
                Dictionary<string, object> d = await Cmd(Json.NewCmd("get_available_models"), 60000);
                Models.Clear();
                List<object> arr = Json.Arr(Value(d, "models"));
                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> m = Json.Obj(arr[i]);
                    if (m != null) Models.Add(m);
                }
            }
            catch { }

            await RefreshLevelsAsync();
            await RefreshCommandsAsync();
            await RefreshMessagesAsync();
            await RefreshStatsAsync();
            Raise();
        }

        public void ApplyState(Dictionary<string, object> st)
        {
            if (st == null) return;
            Dictionary<string, object> m = Json.Obj(Value(st, "model"));
            if (m != null) Model = m;
            string tl = Json.GetStr(st, "thinkingLevel");
            if (!string.IsNullOrEmpty(tl)) ThinkingLevel = tl;
            string sf = Json.GetStr(st, "sessionFile");
            if (!string.IsNullOrEmpty(sf)) SessionPath = sf;
            string nm = Json.GetStr(st, "sessionName");
            if (!string.IsNullOrEmpty(nm)) Name = nm;
            Streaming = Json.GetBool(st, "isStreaming", Streaming);
            Raise();
        }

        public async Task RefreshLevelsAsync()
        {
            try
            {
                Dictionary<string, object> d = await Cmd(Json.NewCmd("get_available_thinking_levels"), 60000);
                List<object> arr = Json.Arr(Value(d, "levels"));
                Levels.Clear();
                for (int i = 0; i < arr.Count; i++)
                {
                    string s = Json.Str(arr[i]);
                    if (!string.IsNullOrEmpty(s)) Levels.Add(s);
                }
                if (Levels.Count == 0) Levels.Add("off");
            }
            catch
            {
                if (Levels.Count == 0) Levels.Add("off");
            }
        }

        public async Task RefreshCommandsAsync()
        {
            try
            {
                Dictionary<string, object> d = await Cmd(Json.NewCmd("get_commands"), 60000);
                List<object> arr = Json.Arr(Value(d, "commands"));
                Commands.Clear();
                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> c = Json.Obj(arr[i]);
                    if (c != null) Commands.Add(c);
                }
            }
            catch { }
        }

        public async Task RefreshStatsAsync()
        {
            try
            {
                Dictionary<string, object> d = await Cmd(Json.NewCmd("get_session_stats"), 60000);
                if (d == null) return;
                Dictionary<string, object> cu = Json.Obj(Value(d, "contextUsage"));
                if (cu != null)
                {
                    HasStats = true;
                    CtxTokens = Json.GetLong(cu, "tokens", 0);
                    CtxWindow = Json.GetLong(cu, "contextWindow", 0);
                    CtxPercent = GetDouble(cu, "percent");
                }
                Cost = GetDouble(d, "cost");
                Raise();
            }
            catch { }
        }

        public async Task RefreshMessagesAsync()
        {
            try
            {
                Dictionary<string, object> d = await Cmd(Json.NewCmd("get_messages"), 180000);
                if (d == null) return;
                List<object> msgs = Json.Arr(Value(d, "messages"));
                List<ChatMessage> built = new List<ChatMessage>();
                Dictionary<string, Block> tools = new Dictionary<string, Block>();

                for (int i = 0; i < msgs.Count; i++)
                {
                    Dictionary<string, object> m = Json.Obj(msgs[i]);
                    if (m == null) continue;
                    string role = Json.GetStr(m, "role");
                    object content = Value(m, "content");

                    if (role == "system") continue;

                    if (role == "user")
                    {
                        ChatMessage cm = new ChatMessage();
                        cm.Role = "user";
                        cm.Text = SessionStore.ContentText(content);
                        AddImages(cm, content);
                        built.Add(cm);
                    }
                    else if (role == "assistant")
                    {
                        ChatMessage cm = new ChatMessage();
                        cm.Role = "assistant";
                        List<object> blocks = Json.Arr(content);
                        for (int bi = 0; bi < blocks.Count; bi++)
                        {
                            Dictionary<string, object> c = Json.Obj(blocks[bi]);
                            if (c == null) continue;
                            string ct = Json.GetStr(c, "type");
                            if (ct == "text")
                            {
                                Block b = NewBlock(Block.KText);
                                b.Text = Json.GetStr(c, "text", "");
                                b.ContentIndex = bi;
                                cm.Blocks.Add(b);
                            }
                            else if (ct == "thinking")
                            {
                                Block b = NewBlock(Block.KThinking);
                                b.Text = Json.GetStr(c, "thinking", "");
                                b.ContentIndex = bi;
                                cm.Blocks.Add(b);
                            }
                            else if (ct == "toolCall")
                            {
                                Block b = NewBlock(Block.KTool);
                                b.ToolCallId = Json.GetStr(c, "id");
                                b.ToolName = Json.GetStr(c, "name");
                                b.Args = Json.Obj(Value(c, "arguments"));
                                b.Running = true;
                                cm.Blocks.Add(b);
                                if (b.ToolCallId != null) tools[b.ToolCallId] = b;
                            }
                        }
                        built.Add(cm);
                    }
                    else if (role == "toolResult")
                    {
                        string id = Json.GetStr(m, "toolCallId");
                        Block b;
                        if (id != null && tools.TryGetValue(id, out b))
                        {
                            b.Output = ResultText(Value(m, "content"));
                            b.IsError = Json.GetBool(m, "isError", false);
                            b.Running = false;
                        }
                    }
                    else if (role == "bashExecution")
                    {
                        ChatMessage cm = new ChatMessage();
                        cm.Role = "assistant";
                        Block b = NewBlock(Block.KTool);
                        b.ToolName = "bash";
                        Dictionary<string, object> args = new Dictionary<string, object>();
                        args["command"] = Json.GetStr(m, "command", "");
                        b.Args = args;
                        b.Output = Json.GetStr(m, "output", "");
                        b.IsError = Json.GetInt(m, "exitCode", 0) != 0;
                        b.Running = false;
                        cm.Blocks.Add(b);
                        built.Add(cm);
                    }
                }

                Messages = built;
                ToolIndex = tools;
                NeedsRebuild = true;
                Raise();
            }
            catch (Exception ex)
            {
                RaiseToast("刷新对话失败：" + ex.Message, "error");
            }
        }

        private void AddImages(ChatMessage cm, object content)
        {
            List<object> arr = Json.Arr(content);
            for (int i = 0; i < arr.Count; i++)
            {
                Dictionary<string, object> c = Json.Obj(arr[i]);
                if (c == null || Json.GetStr(c, "type") != "image") continue;
                Dictionary<string, object> src = Json.Obj(Value(c, "source"));
                string data = src != null ? Json.GetStr(src, "data") : Json.GetStr(c, "data");
                string mime = src != null ? Json.GetStr(src, "mediaType") : Json.GetStr(c, "mimeType");
                if (string.IsNullOrEmpty(mime)) mime = "image/png";
                if (!string.IsNullOrEmpty(data)) cm.Images.Add("data:" + mime + ";base64," + data);
            }
        }

        public static string ResultText(object content)
        {
            string s = content as string;
            if (s != null) return s;
            List<object> arr = Json.Arr(content);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < arr.Count; i++)
            {
                Dictionary<string, object> c = Json.Obj(arr[i]);
                if (c == null) continue;
                string t = Json.GetStr(c, "type");
                if (t == "text")
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(Json.GetStr(c, "text", ""));
                }
                else if (t == "image")
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append("[图片]");
                }
            }
            return sb.ToString();
        }

        private static Block NewBlock(string kind)
        {
            Block b = new Block();
            b.Kind = kind;
            return b;
        }

        /* ---------------- 事件 ---------------- */
        private void OnEvent(Dictionary<string, object> ev)
        {
            string type = Json.GetStr(ev, "type");
            try
            {
                if (type == "message_start") OnMessageStart(Json.Obj(Value(ev, "message")));
                else if (type == "message_update") OnMessageUpdate(ev);
                else if (type == "message_end") OnMessageEnd(Json.Obj(Value(ev, "message")));
                else if (type == "tool_execution_start") OnToolStart(ev);
                else if (type == "tool_execution_update") OnToolUpdate(ev);
                else if (type == "tool_execution_end") OnToolEnd(ev);
                else if (type == "agent_start") { Streaming = true; Raise(); }
                else if (type == "agent_settled") { Streaming = false; Raise(); Task.Run(async delegate { await RefreshMessagesAsync(); await RefreshStatsAsync(); }); }
                else if (type == "queue_update") { ReadQueue(ev); Raise(); }
                else if (type == "session_info_changed") { Name = Json.GetStr(ev, "name", ""); Raise(); }
                else if (type == "compaction_start") { RaiseToast(Json.GetStr(ev, "reason") == "manual" ? "正在压缩上下文…" : "上下文接近上限，自动压缩中…", "info"); }
                else if (type == "compaction_end")
                {
                    string err = Json.GetStr(ev, "errorMessage");
                    if (!string.IsNullOrEmpty(err)) RaiseToast("压缩失败：" + err, "error");
                    else if (!Json.GetBool(ev, "aborted", false)) RaiseToast("上下文已压缩", "info");
                    Raise();
                }
                else if (type == "auto_retry_start") { RaiseToast("请求失败，正在自动重试…", "warn"); }
                else if (type == "auto_retry_end") { if (!Json.GetBool(ev, "success", true)) RaiseToast("自动重试最终失败", "error"); }
                else if (type == "extension_error") { RaiseToast("扩展出错：" + Json.GetStr(ev, "error"), "error"); }
                else if (type == "extension_ui_request") { OnExtUi(ev); }
            }
            catch { }
        }

        private void ReadQueue(Dictionary<string, object> ev)
        {
            Steering.Clear();
            FollowUp.Clear();
            List<object> a = Json.Arr(Value(ev, "steering"));
            for (int i = 0; i < a.Count; i++) { string s = Json.Str(a[i]); if (!string.IsNullOrEmpty(s)) Steering.Add(s); }
            List<object> b = Json.Arr(Value(ev, "followUp"));
            for (int i = 0; i < b.Count; i++) { string s = Json.Str(b[i]); if (!string.IsNullOrEmpty(s)) FollowUp.Add(s); }
        }

        private void OnExtUi(Dictionary<string, object> ev)
        {
            string method = Json.GetStr(ev, "method");
            if (method == "notify")
            {
                string kind = Json.GetStr(ev, "notifyType", "info");
                RaiseToast(Json.GetStr(ev, "message", ""), kind == "error" ? "error" : (kind == "warning" ? "warn" : "info"));
                return;
            }
            if (method == "setStatus")
            {
                string key = Json.GetStr(ev, "statusKey", "");
                string text = Json.GetStr(ev, "statusText");
                if (!string.IsNullOrEmpty(text)) Statuses[key] = text;
                else Statuses.Remove(key);
                Raise();
                return;
            }
            if (method == "setTitle" || method == "set_editor_text") return;
            if (method == "select" || method == "confirm" || method == "input" || method == "editor")
            {
                Action<Session, Dictionary<string, object>> h = ExtUi;
                if (h != null) h(this, ev);
            }
        }

        private void OnMessageStart(Dictionary<string, object> m)
        {
            if (m == null) return;
            string role = Json.GetStr(m, "role");
            if (role == "user")
            {
                if (_pendingUsers.Count > 0) { _pendingUsers.RemoveAt(0); return; }
                ChatMessage cm = new ChatMessage();
                cm.Role = "user";
                cm.Text = SessionStore.ContentText(Value(m, "content"));
                AddImages(cm, Value(m, "content"));
                Messages.Add(cm);
                Raise();
            }
            else if (role == "assistant")
            {
                ChatMessage cm = new ChatMessage();
                cm.Role = "assistant";
                cm.IsLive = true;
                Messages.Add(cm);
                _live = cm;
                Streaming = true;
                Raise();
            }
        }

        private Block FindBlock(string kind, int idx)
        {
            if (_live == null) return null;
            for (int i = 0; i < _live.Blocks.Count; i++)
            {
                Block b = _live.Blocks[i];
                if (b.Kind == kind && b.ContentIndex == idx) return b;
            }
            return null;
        }

        private Block EnsureBlock(string kind, int idx)
        {
            if (_live == null)
            {
                ChatMessage cm = new ChatMessage();
                cm.Role = "assistant";
                cm.IsLive = true;
                Messages.Add(cm);
                _live = cm;
            }
            Block b = FindBlock(kind, idx);
            if (b != null) return b;
            b = NewBlock(kind);
            b.ContentIndex = idx;
            _live.Blocks.Add(b);
            return b;
        }

        private Block EnsureTool(string id, string name)
        {
            Block b = null;
            if (!string.IsNullOrEmpty(id) && ToolIndex.TryGetValue(id, out b)) return b;
            if (_live == null)
            {
                ChatMessage cm = new ChatMessage();
                cm.Role = "assistant";
                cm.IsLive = true;
                Messages.Add(cm);
                _live = cm;
            }
            b = NewBlock(Block.KTool);
            b.ToolCallId = id;
            b.ToolName = name;
            b.Running = true;
            b.ContentIndex = 1000 + _live.Blocks.Count;
            _live.Blocks.Add(b);
            if (!string.IsNullOrEmpty(id)) ToolIndex[id] = b;
            return b;
        }

        private void OnMessageUpdate(Dictionary<string, object> ev)
        {
            Dictionary<string, object> a = Json.Obj(Value(ev, "assistantMessageEvent"));
            if (a == null) return;
            string t = Json.GetStr(a, "type");
            int idx = Json.GetInt(a, "contentIndex", 0);

            if (t == "text_start") { EnsureBlock(Block.KText, idx); }
            else if (t == "text_delta") { EnsureBlock(Block.KText, idx).Text += Json.GetStr(a, "delta", ""); }
            else if (t == "text_end") { string c = Json.GetStr(a, "content"); if (c != null) EnsureBlock(Block.KText, idx).Text = c; }
            else if (t == "thinking_start") { EnsureBlock(Block.KThinking, idx); }
            else if (t == "thinking_delta") { EnsureBlock(Block.KThinking, idx).Text += Json.GetStr(a, "delta", ""); }
            else if (t == "thinking_end") { string c = Json.GetStr(a, "content"); if (c != null) EnsureBlock(Block.KThinking, idx).Text = c; }
            else if (t == "toolcall_start") { Block b = EnsureTool(Json.GetStr(a, "id"), Json.GetStr(a, "toolName")); b.Running = true; }
            else if (t == "toolcall_delta")
            {
                Block b = EnsureTool(Json.GetStr(a, "id"), Json.GetStr(a, "toolName"));
                b.ArgsRaw += Json.GetStr(a, "delta", "");
            }
            else if (t == "toolcall_end")
            {
                Dictionary<string, object> tc = Json.Obj(Value(a, "toolCall"));
                if (tc != null)
                {
                    Block b = EnsureTool(Json.GetStr(tc, "id"), Json.GetStr(tc, "name"));
                    b.Args = Json.Obj(Value(tc, "arguments"));
                    b.ToolName = Json.GetStr(tc, "name", b.ToolName);
                }
            }
            Raise();
        }

        private void OnMessageEnd(Dictionary<string, object> m)
        {
            if (m != null && Json.GetStr(m, "role") == "assistant")
            {
                if (_live != null) _live.IsLive = false;
                _live = null;
                string err = Json.GetStr(m, "errorMessage");
                if (!string.IsNullOrEmpty(err)) RaiseToast(err, "error");
            }
            Raise();
        }

        private void OnToolStart(Dictionary<string, object> ev)
        {
            Block b = EnsureTool(Json.GetStr(ev, "toolCallId"), Json.GetStr(ev, "toolName"));
            b.Args = Json.Obj(Value(ev, "args"));
            b.Running = true;
            Raise();
        }

        private void OnToolUpdate(Dictionary<string, object> ev)
        {
            Block b = EnsureTool(Json.GetStr(ev, "toolCallId"), Json.GetStr(ev, "toolName"));
            Dictionary<string, object> pr = Json.Obj(Value(ev, "partialResult"));
            b.Output = ResultText(Value(pr, "content"));
            b.Running = true;
            Raise();
        }

        private void OnToolEnd(Dictionary<string, object> ev)
        {
            Block b = EnsureTool(Json.GetStr(ev, "toolCallId"), Json.GetStr(ev, "toolName"));
            Dictionary<string, object> r = Json.Obj(Value(ev, "result"));
            b.Output = ResultText(Value(r, "content"));
            b.IsError = Json.GetBool(ev, "isError", false);
            b.Running = false;
            Raise();
        }

        /* ---------------- 操作 ---------------- */
        public async Task<string> PromptAsync(string text, List<Dictionary<string, object>> images)
        {
            Dictionary<string, object> cmd = Json.NewCmd("prompt");
            cmd["message"] = text;
            if (images != null && images.Count > 0) cmd["images"] = images.ToArray();
            if (Streaming) cmd["streamingBehavior"] = "steer";

            ChatMessage cm = new ChatMessage();
            cm.Role = "user";
            cm.Text = text;
            if (images != null)
            {
                for (int i = 0; i < images.Count; i++)
                {
                    string mime = Json.GetStr(images[i], "mimeType", "image/png");
                    string data = Json.GetStr(images[i], "data", "");
                    if (data.Length > 0) cm.Images.Add("data:" + mime + ";base64," + data);
                }
            }
            Messages.Add(cm);
            _pendingUsers.Add(text);
            Raise();

            try
            {
                await Cmd(cmd, 120000);
                return null;
            }
            catch (Exception ex)
            {
                Messages.Remove(cm);
                if (_pendingUsers.Count > 0) _pendingUsers.RemoveAt(_pendingUsers.Count - 1);
                Raise();
                return ex.Message;
            }
        }

        public async Task<string> SetModelAsync(string provider, string modelId)
        {
            Dictionary<string, object> cmd = Json.NewCmd("set_model");
            cmd["provider"] = provider;
            cmd["modelId"] = modelId;
            try
            {
                Dictionary<string, object> m = await Cmd(cmd, 60000);
                if (m != null) Model = m;
                await RefreshLevelsAsync();
                Raise();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> SetThinkingAsync(string level)
        {
            Dictionary<string, object> cmd = Json.NewCmd("set_thinking_level");
            cmd["level"] = level;
            try
            {
                await Cmd(cmd, 60000);
                ThinkingLevel = level;
                Raise();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> AbortAsync()
        {
            try
            {
                await Cmd(Json.NewCmd("clear_queue"), 30000);
            }
            catch { }
            try
            {
                await Cmd(Json.NewCmd("abort"), 120000);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> CompactAsync()
        {
            try
            {
                await Cmd(Json.NewCmd("compact"), 900000);
                await RefreshMessagesAsync();
                await RefreshStatsAsync();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> RenameAsync(string name)
        {
            Dictionary<string, object> cmd = Json.NewCmd("set_session_name");
            cmd["name"] = name;
            try
            {
                await Cmd(cmd, 30000);
                Name = name;
                Raise();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public async Task<string> ExportAsync()
        {
            try
            {
                Dictionary<string, object> d = await Cmd(Json.NewCmd("export_html"), 180000);
                return Json.GetStr(d, "path");
            }
            catch (Exception ex) { return "ERR:" + ex.Message; }
        }

        public void DisposeSession()
        {
            if (Rpc != null)
            {
                try { Rpc.Dispose(); }
                catch { }
                Rpc = null;
            }
        }
    }
}
