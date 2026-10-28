using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace PiClient
{
    internal class MainForm : Form
    {
        private List<string> _modelLabels = new List<string>();
        private List<string> _thinkLabels = new List<string>();

        private Config _cfg;
        private PiInstall _pi;
        private readonly List<Session> _sessions = new List<Session>();
        private Session _active;
        private List<SessionInfo> _history = new List<SessionInfo>();

        private ChatFlow _chat;
        private Panel _topBar;
        private Panel _sideBar;
        private Panel _composer;
        private Panel _statusBar;
        private Panel _toastPanel;
        private Label _toastLabel;
        private Panel _inputWrap;
        private TextBox _input;
        private DarkSelect _modelCombo;
        private DarkSelect _thinkCombo;
        private DarkList _openList;
        private DarkList _historyList;
        private ListBox _palette;
        private Label _titleLabel;
        private Label _ctxLabel;
        private Label _dirLabel;
        private Label _attachLabel;
        private Label _placeholder;
        private Label _statusLabel;
        private Label _openTitle;
        private Label _historyTitle;
        private Label _dirCaption;
        private Label _footLabel;
        private FlatButton _btnNew;
        private FlatButton _btnPick;
        private FlatButton _btnReveal;
        private FlatButton _btnSend;
        private FlatButton _btnStop;
        private FlatButton _btnCompact;
        private FlatButton _btnExport;
        private FlatButton _btnRename;
        private FlatButton _btnRestart;
        private FlatButton _btnSetup;
        private FlatButton _btnAttachClear;
        private FlatButton _btnFoot;

        private readonly Timer _uiTimer = new Timer();
        private readonly Timer _toastTimer = new Timer();
        private volatile bool _dirty = true;
        private readonly ConcurrentQueue<string[]> _toastQueue = new ConcurrentQueue<string[]>();
        private readonly ConcurrentQueue<Dictionary<string, object>> _extQueue = new ConcurrentQueue<Dictionary<string, object>>();
        private readonly List<Dictionary<string, object>> _attachments = new List<Dictionary<string, object>>();
        private bool _syncing;
        private bool _forceRebuild;
        private float _scale = 1f;
        private string _smokePrompt;
        private int _smokeTick;
        private bool _smokeDone;
        public bool ForceSetup;
        public string OpenSessionPath;
        private bool _shotOnly;

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.TextCol;
            Text = "pi 客户端";
            MinimumSize = new Size(880, 560);
            Size = new Size(1180, 760);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            KeyPreview = true;
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) Icon = ico;
            }
            catch { }
            _scale = Theme.Scale > 0 ? Theme.Scale : 1f;
            BuildUi();
        }

        private static float ScreenDpi()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    float d = g.DpiX / 96f;
                    return d > 0.1f ? d : 1f;
                }
            }
            catch { return 1f; }
        }

        private int S(int px)
        {
            return (int)Math.Round(px * _scale);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                int build = Environment.OSVersion.Version.Build;
                int attr = build >= 18985 ? 20 : 19;
                DwmSetWindowAttribute(Handle, attr, ref on, 4);
            }
            catch { }

        }

        /* ---------------- 构建界面 ---------------- */
        private void BuildUi()
        {
            _chat = new ChatFlow();
            _chat.Scrolled += delegate { _dirty = true; };

            _topBar = new DarkPanel();
            _topBar.Paint += PaintTopBar;

            _sideBar = new DarkPanel();
            _sideBar.Paint += PaintSideBar;

            _composer = new DarkPanel();
            _composer.Paint += PaintComposer;

            _statusBar = new DarkPanel();
            _statusBar.Paint += PaintStatusBar;

            // 顶栏
            _titleLabel = MakeLabel("新对话", Theme.UiBold, Theme.TextCol);
            _modelCombo = new DarkSelect();
            _modelCombo.SelectionChanged += OnModelChanged;
            _thinkCombo = new DarkSelect();
            _thinkCombo.SelectionChanged += OnThinkChanged;
            _ctxLabel = MakeLabel("", Theme.UiSmall, Theme.Muted);

            _btnCompact = MakeIconButton("⤵", "压缩上下文");
            _btnCompact.Click += delegate { Compact(); };
            _btnExport = MakeIconButton("⭳", "导出为 HTML");
            _btnExport.Click += delegate { ExportHtml(); };
            _btnRename = MakeIconButton("✎", "重命名会话");
            _btnRename.Click += delegate { RenameSession(); };
            _btnRestart = MakeIconButton("↻", "重启会话进程");
            _btnRestart.Click += delegate { RestartSession(); };
            _btnSetup = MakeIconButton("⚙", "绑定 / 更换 pi");
            _btnSetup.Click += delegate { SetupPi(false); };

            // 侧栏
            _btnNew = new FlatButton("＋  新对话");
            _btnNew.Primary = true;
            _btnNew.Click += delegate { NewSession(); };

            _dirCaption = MakeLabel("当前工作目录", Theme.UiSmall, Theme.Muted2);
            _dirLabel = MakeLabel("—", Theme.UiSmall, Color.FromArgb(195, 208, 230));
            _btnPick = new FlatButton("选择文件夹");
            _btnPick.Font = Theme.UiSmall;
            _btnPick.Height = S(24);
            _btnPick.Click += delegate { PickDirectory(); };
            _btnReveal = new FlatButton("打开目录");
            _btnReveal.Font = Theme.UiSmall;
            _btnReveal.Height = S(24);
            _btnReveal.Click += delegate { RevealCwd(); };

            _openTitle = MakeLabel("打开的对话", Theme.UiSmall, Theme.Muted2);
            _openList = new DarkList();
            _openList.ItemHeight = S(40);
            _openList.SelectedIndexChanged += delegate
            {
                if (_syncing) return;
                ListEntry e = _openList.SelectedItem as ListEntry;
                if (e == null) return;
                Session s = e.Tag as Session;
                if (s != null && s != _active) Activate(s);
            };

            _historyTitle = MakeLabel("历史会话", Theme.UiSmall, Theme.Muted2);
            _historyList = new DarkList();
            _historyList.ItemHeight = S(40);
            _historyList.SelectedIndexChanged += delegate
            {
                if (_syncing) return;
                ListEntry e = _historyList.SelectedItem as ListEntry;
                if (e == null) return;
                SessionInfo si = e.Tag as SessionInfo;
                if (si != null) OpenSession(si.Cwd, si.Path);
            };

            _btnFoot = new FlatButton("数据目录");
            _btnFoot.FillColor = Theme.Panel;
            _btnFoot.BorderColor = Color.Transparent;
            _btnFoot.TextColor = Theme.Muted2;
            _btnFoot.Font = Theme.UiSmall;
            _btnFoot.Height = S(22);
            _btnFoot.Click += delegate { OpenDataDir(); };
            _footLabel = MakeLabel("", Theme.UiSmall, Theme.Muted2);

            // 输入区
            _inputWrap = new DarkPanel();
            _inputWrap.Paint += PaintInputWrap;
            _input = new TextBox();
            _input.Multiline = true;
            _input.BorderStyle = BorderStyle.None;
            _input.BackColor = Theme.Panel2;
            _input.ForeColor = Theme.TextCol;
            _input.Font = Theme.Body;
            _input.ScrollBars = ScrollBars.None;
            _input.KeyDown += OnInputKeyDown;
            _input.TextChanged += delegate { UpdateComposerHeight(); SlashPalette(); UpdatePlaceholder(); };
            _inputWrap.Controls.Add(_input);
            _placeholder = MakeLabel("给 pi 发消息…  Enter 发送 / Shift+Enter 换行 / Ctrl+V 贴图", Theme.Body, Color.FromArgb(93, 104, 128));
            _placeholder.Click += delegate { _input.Focus(); };
            _inputWrap.Controls.Add(_placeholder);

            _attachLabel = MakeLabel("", Theme.UiSmall, Theme.Warn);
            _btnAttachClear = new FlatButton("清除图片");
            _btnAttachClear.Font = Theme.UiSmall;
            _btnAttachClear.Height = S(22);
            _btnAttachClear.Visible = false;
            _btnAttachClear.Click += delegate { _attachments.Clear(); UpdateAttachLabel(); };

            _btnSend = new FlatButton("发送");
            _btnSend.Primary = true;
            _btnSend.Width = S(74);
            _btnSend.Height = S(30);
            _btnSend.Click += delegate { SendMessage(); };

            _btnStop = new FlatButton("停止");
            _btnStop.Width = S(62);
            _btnStop.Height = S(30);
            _btnStop.TextColor = Color.FromArgb(255, 180, 180);
            _btnStop.FillColor = Color.FromArgb(44, 24, 30);
            _btnStop.BorderColor = Color.FromArgb(74, 38, 48);
            _btnStop.Visible = false;
            _btnStop.Click += delegate { AbortCurrent(); };

            _inputWrap.Controls.Add(_btnSend);
            _inputWrap.Controls.Add(_btnStop);
            _btnSend.BringToFront();
            _btnStop.BringToFront();
            _placeholder.BringToFront();

            _palette = new ListBox();
            _palette.Visible = false;
            _palette.DrawMode = DrawMode.OwnerDrawFixed;
            _palette.BorderStyle = BorderStyle.FixedSingle;
            _palette.BackColor = Theme.Panel2;
            _palette.ForeColor = Theme.TextCol;
            _palette.Font = Theme.Ui;
            _palette.ItemHeight = S(22);
            _palette.DrawItem += PaintPaletteItem;
            _palette.DoubleClick += delegate { ApplyPalette(); };
            _palette.KeyDown += OnPaletteKeyDown;

            _statusLabel = MakeLabel("", Theme.UiSmall, Theme.Muted2);

            _toastPanel = new DarkPanel();
            _toastPanel.Visible = false;
            _toastPanel.Paint += PaintToast;
            _toastLabel = MakeLabel("", Theme.UiSmall, Theme.TextCol);
            _toastLabel.AutoSize = false;
            _toastPanel.Controls.Add(_toastLabel);

            Controls.Add(_chat);
            Controls.Add(_topBar);
            Controls.Add(_sideBar);
            Controls.Add(_composer);
            Controls.Add(_statusBar);
            Controls.Add(_toastPanel);
            Controls.Add(_palette);

            _topBar.Controls.AddRange(new Control[] { _titleLabel, _modelCombo, _thinkCombo, _ctxLabel, _btnCompact, _btnExport, _btnRename, _btnRestart, _btnSetup });
            _sideBar.Controls.AddRange(new Control[] { _btnNew, _dirCaption, _dirLabel, _btnPick, _btnReveal, _openTitle, _openList, _historyTitle, _historyList, _btnFoot, _footLabel });
            _composer.Controls.AddRange(new Control[] { _inputWrap, _attachLabel, _btnAttachClear });
            _statusBar.Controls.Add(_statusLabel);

            _uiTimer.Interval = 60;
            _uiTimer.Tick += delegate { Pump(); };
            _uiTimer.Start();

            _toastTimer.Interval = 3800;
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                _toastPanel.Visible = false;
            };

            DoLayout();
        }

        private Label MakeLabel(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private FlatButton MakeIconButton(string glyph, string tip)
        {
            FlatButton b = new FlatButton(glyph);
            b.Font = new Font(Theme.Ui.FontFamily, 10f);
            b.Width = S(28);
            b.Height = S(26);
            b.FillColor = Color.Transparent;
            b.BorderColor = Color.Transparent;
            b.TextColor = Theme.Muted;
            b.HoverColor = Theme.Panel3;
            ToolTip tipObj = new ToolTip();
            tipObj.SetToolTip(b, tip);
            return b;
        }

        /* ---------------- 布局 ---------------- */
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            DoLayout();
        }

        private void DoLayout()
        {
            if (_chat == null) return;
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int topH = S(46);
            int statusH = S(24);
            int sideW = S(252);
            int composerH = ComposerHeight();

            _topBar.Bounds = new Rectangle(0, 0, w, topH);
            _statusBar.Bounds = new Rectangle(0, h - statusH, w, statusH);
            _sideBar.Bounds = new Rectangle(0, topH, sideW, h - topH - statusH);
            _composer.Bounds = new Rectangle(sideW, h - statusH - composerH, w - sideW, composerH);
            _chat.Bounds = new Rectangle(sideW, topH, w - sideW, h - topH - statusH - composerH);

            // 顶栏
            int right = w - S(10);
            int cy = S(10);
            _btnSetup.Bounds = new Rectangle(right - S(28), cy, S(28), S(26));
            right -= S(30);
            _btnRestart.Bounds = new Rectangle(right - S(28), cy, S(28), S(26));
            right -= S(30);
            _btnRename.Bounds = new Rectangle(right - S(28), cy, S(28), S(26));
            right -= S(30);
            _btnExport.Bounds = new Rectangle(right - S(28), cy, S(28), S(26));
            right -= S(30);
            _btnCompact.Bounds = new Rectangle(right - S(28), cy, S(28), S(26));
            right -= S(38);
            _ctxLabel.Bounds = new Rectangle(right - S(64), cy, S(64), S(26));
            _ctxLabel.TextAlign = ContentAlignment.MiddleRight;
            right -= S(70);
            _thinkCombo.Bounds = new Rectangle(right - S(126), cy, S(126), S(26));
            right -= S(132);
            _modelCombo.Bounds = new Rectangle(right - S(170), cy, S(170), S(26));
            right -= S(178);
            _titleLabel.Bounds = new Rectangle(S(16), S(10), Math.Max(S(80), right - S(16)), S(26));

            // 侧栏
            int sx = S(12);
            int sw = sideW - S(24);
            _btnNew.Bounds = new Rectangle(sx, S(12), sw, S(32));
            _dirCaption.Bounds = new Rectangle(sx + S(2), S(56), sw, S(16));
            _dirLabel.Bounds = new Rectangle(sx + S(2), S(72), sw, S(32));
            _btnPick.Bounds = new Rectangle(sx, S(108), S(96), S(24));
            _btnReveal.Bounds = new Rectangle(sx + S(102), S(108), S(88), S(24));

            int openTitleY = S(142);
            _openTitle.Bounds = new Rectangle(sx, openTitleY, sw, S(16));
            int openH = S(40) * 3;
            _openList.Bounds = new Rectangle(sx - S(2), openTitleY + S(20), sw + S(4), openH);

            int histTitleY = openTitleY + S(20) + openH + S(10);
            _historyTitle.Bounds = new Rectangle(sx, histTitleY, sw, S(16));
            int histY = histTitleY + S(20);
            int histH = Math.Max(S(60), _sideBar.Height - histY - S(46));
            _historyList.Bounds = new Rectangle(sx - S(2), histY, sw + S(4), histH);

            _btnFoot.Bounds = new Rectangle(sx, _sideBar.Height - S(30), S(80), S(22));
            _footLabel.Bounds = new Rectangle(sx + S(80), _sideBar.Height - S(30), sw - S(84), S(22));
            _footLabel.TextAlign = ContentAlignment.MiddleRight;

            // 输入区（发送/停止按钮在输入框内部）
            int padX = S(20);
            int padY = S(10);
            int cw = _composer.Width - padX * 2;
            int inputH = InputHeight();
            int boxH = inputH + S(8);
            _inputWrap.Bounds = new Rectangle(padX, padY, cw, boxH);
            _btnSend.Bounds = new Rectangle(cw - S(10) - S(74), boxH - S(8) - S(30), S(74), S(30));
            _btnStop.Bounds = new Rectangle(_btnSend.Left - S(70), boxH - S(8) - S(30), S(62), S(30));
            _input.Bounds = new Rectangle(S(12), S(9), Math.Max(S(60), cw - S(12) - S(94)), Math.Max(S(22), boxH - S(18)));
            _placeholder.Bounds = new Rectangle(S(14), S(11), Math.Max(S(60), cw - S(20) - S(94)), S(20));
            _attachLabel.Bounds = new Rectangle(padX + S(2), padY + boxH + S(3), S(260), S(20));
            _btnAttachClear.Bounds = new Rectangle(padX + S(190), padY + boxH + S(2), S(74), S(22));

            _statusLabel.Bounds = new Rectangle(S(14), 0, w - S(28), statusH);

            if (_toastPanel.Visible) PositionToast();
            if (_palette.Visible) PositionPalette();
        }

        private int InputHeight()
        {
            int lineH = TextMeter.Height(Theme.Body) + 2;
            int lines = 1;
            string t = _input.Text;
            for (int i = 0; i < t.Length; i++) if (t[i] == '\n') lines++;
            lines = Math.Max(1, Math.Min(6, lines));
            return S(16) + lineH * lines;
        }

        private int ComposerHeight()
        {
            return S(10) + InputHeight() + S(8) + S(6) + S(22) + S(8);
        }

        private void UpdateComposerHeight()
        {
            int want = ComposerHeight();
            if (_composer.Height != want)
            {
                DoLayout();
            }
        }

        /* ---------------- 绘制 ---------------- */
        private void PaintTopBar(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            using (Pen p = new Pen(Theme.BorderSoft)) g.DrawLine(p, 0, _topBar.Height - 1, _topBar.Width, _topBar.Height - 1);
        }

        private void PaintSideBar(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Panel);
            using (Pen p = new Pen(Theme.BorderSoft)) g.DrawLine(p, _sideBar.Width - 1, 0, _sideBar.Width - 1, _sideBar.Height);
        }

        private void PaintComposer(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
        }

        private void PaintInputWrap(object sender, PaintEventArgs e)
        {
            Theme.DrawRound(e.Graphics, new Rectangle(0, 0, _inputWrap.Width - 1, _inputWrap.Height - 1), 9, Theme.Panel2, Theme.Border);
        }

        private void PaintStatusBar(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(13, 17, 26));
            using (Pen p = new Pen(Theme.BorderSoft)) e.Graphics.DrawLine(p, 0, 0, _statusBar.Width, 0);
        }

        private void PaintToast(object sender, PaintEventArgs e)
        {
            Theme.DrawRound(e.Graphics, new Rectangle(0, 0, _toastPanel.Width - 1, _toastPanel.Height - 1), 8,
                Color.FromArgb(26, 34, 51), Color.FromArgb(60, 78, 110));
        }

        private void PaintPaletteItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? Theme.Panel3 : Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
            string text = Convert.ToString(_palette.Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, Theme.Ui, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                sel ? Color.White : Theme.TextCol, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        /* ---------------- 启动 ---------------- */
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Program.Trace("OnShown");
            _cfg = Config.Load();
            ApplySavedBounds();
            if (ForceSetup)
            {
                if (!SetupPi(true)) { Close(); return; }
            }
            else if (!ResolvePi(true))
            {
                Close();
                return;
            }
            RefreshHistory();
            string cwd = !string.IsNullOrEmpty(_cfg.LastCwd) && Directory.Exists(_cfg.LastCwd) ? _cfg.LastCwd : DefaultCwd();
            if (!string.IsNullOrEmpty(OpenSessionPath))
            {
                string dir = Path.GetDirectoryName(OpenSessionPath);
                OpenSession(cwd, OpenSessionPath);
            }
            else
            {
                OpenSession(cwd, null);
            }
            _input.Focus();
            Program.Trace("after open session, smoke=" + (_smokePrompt != null));
            if (_smokePrompt != null) StartSmoke();
            else if (_shotOnly) StartShot();
        }

        private static string DefaultCwd()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? "C:\\" : home;
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        private void ApplySavedBounds()
        {
            try
            {
                int wdpi = 0;
                try { wdpi = GetDpiForWindow(Handle); } catch { }
                Program.Trace("screen=" + Screen.PrimaryScreen.Bounds + " windowDpi=" + wdpi + " gfxDpi=" + ScreenDpi());
                Program.Trace("scale=" + _scale + " saved=" + _cfg.WinX + "," + _cfg.WinY + " " + _cfg.WinW + "x" + _cfg.WinH);
                Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
                int w = _cfg.WinW > 0 ? _cfg.WinW : S(1180);
                int h = _cfg.WinH > 0 ? _cfg.WinH : S(760);
                w = Math.Min(w, wa.Width - S(20));
                h = Math.Min(h, wa.Height - S(20));
                int x = _cfg.WinX > int.MinValue ? _cfg.WinX : wa.X + (wa.Width - w) / 2;
                int y = _cfg.WinY > int.MinValue ? _cfg.WinY : wa.Y + (wa.Height - h) / 2;
                if (x < wa.X - 40 || y < wa.Y - 40 || x > wa.Right - S(200) || y > wa.Bottom - S(120))
                {
                    x = wa.X + (wa.Width - w) / 2;
                    y = wa.Y + (wa.Height - h) / 2;
                }
                Bounds = new Rectangle(x, y, w, h);
                Program.Trace("bounds set " + x + "," + y + " " + w + "x" + h + " -> " + Bounds);
                if (_cfg.WinMax) WindowState = FormWindowState.Maximized;
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                _cfg.WinX = b.X;
                _cfg.WinY = b.Y;
                _cfg.WinW = b.Width;
                _cfg.WinH = b.Height;
                _cfg.WinMax = WindowState == FormWindowState.Maximized;
                if (_active != null)
                {
                    _cfg.LastCwd = _active.Cwd;
                    if (_active.Model != null)
                    {
                        _cfg.Model = Json.GetStr(_active.Model, "id", "");
                        _cfg.Thinking = _active.ThinkingLevel;
                    }
                }
                _cfg.Save();
            }
            catch { }
            for (int i = 0; i < _sessions.Count; i++)
            {
                try { _sessions[i].DisposeSession(); }
                catch { }
            }
            base.OnFormClosing(e);
        }

        /* ---------------- pi 绑定 ---------------- */
        private bool ResolvePi(bool interactive)
        {
            List<PiInstall> found = PiLocator.Detect(_cfg);
            if (found.Count > 0)
            {
                _pi = found[0];
                _cfg.PiHome = _pi.Home;
                _cfg.Save();
                return true;
            }
            if (!interactive) return false;
            return SetupPi(true);
        }

        private bool SetupPi(bool firstRun)
        {
            using (SetupForm f = new SetupForm(_cfg, firstRun))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return false;
                _pi = f.Selected;
                _cfg.PiHome = _pi.Home;
                _cfg.Save();
                RefreshStatus();
                return true;
            }
        }

        private void RefreshStatus()
        {
            _dirty = true;
        }

        /* ---------------- 会话 ---------------- */
        private Session CreateSession(string cwd, string sessionPath)
        {
            if (_pi == null)
            {
                if (!SetupPi(false)) return null;
            }
            Session s = new Session();
            s.Start(_pi, cwd, sessionPath);
            s.Changed += delegate
            {
                _dirty = true;
                if (s.NeedsRebuild) _forceRebuild = true;
            };
            s.DeadChanged += delegate { _dirty = true; };
            s.Toast += delegate(Session src, string msg, string kind)
            {
                string[] item = new string[3];
                item[0] = msg;
                item[1] = kind;
                item[2] = src.Cwd;
                _toastQueue.Enqueue(item);
            };
            s.ExtUi += delegate(Session src, Dictionary<string, object> req) { _extQueue.Enqueue(req); };
            _sessions.Add(s);
            _cfg.RememberDir(cwd);
            _cfg.Save();
            Task.Run(async delegate { await s.InitializeAsync(); });
            return s;
        }

        private void OpenSession(string cwd, string sessionPath)
        {
            if (string.IsNullOrEmpty(cwd) || !Directory.Exists(cwd))
            {
                PickDirectory();
                return;
            }
            Session s = CreateSession(cwd, sessionPath);
            if (s == null) return;
            Activate(s);
        }

        private void Activate(Session s)
        {
            _active = s;
            _forceRebuild = true;
            _dirty = true;
            _attachLabel.Text = "";
            UpdateAttachLabel();
            _input.Focus();
        }

        private void NewSession()
        {
            string cwd = _active != null ? _active.Cwd : (_cfg.LastCwd ?? DefaultCwd());
            OpenSession(cwd, null);
        }

        private void RestartSession()
        {
            if (_active == null) return;
            if (MessageBox.Show(this, "重启该会话的 pi 进程？正在生成的回答会被中断。", "重启会话",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            string cwd = _active.Cwd;
            string path = _active.SessionPath;
            _sessions.Remove(_active);
            _active.DisposeSession();
            _active = null;
            OpenSession(cwd, path);
        }

        private void CloseSession(Session s)
        {
            if (s == null) return;
            _sessions.Remove(s);
            if (_active == s)
            {
                _active = null;
                if (_sessions.Count > 0) Activate(_sessions[0]);
            }
            s.DisposeSession();
            _dirty = true;
        }

        private void PickDirectory()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择 pi 的工作目录";
                d.ShowNewFolderButton = true;
                string start = _active != null ? _active.Cwd : _cfg.LastCwd;
                if (!string.IsNullOrEmpty(start) && Directory.Exists(start)) d.SelectedPath = start;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                OpenSession(d.SelectedPath, null);
            }
        }

        private void RevealCwd()
        {
            if (_active == null) return;
            try { Process.Start("explorer.exe", "\"" + _active.Cwd + "\""); }
            catch { }
        }

        private void OpenDataDir()
        {
            try
            {
                string root = SessionStore.Root;
                if (Directory.Exists(root)) Process.Start("explorer.exe", "\"" + root + "\"");
            }
            catch { }
        }

        private void RefreshHistory()
        {
            string cwd = _active != null ? _active.Cwd : _cfg.LastCwd;
            if (string.IsNullOrEmpty(cwd)) cwd = DefaultCwd();
            _history = SessionStore.List(cwd, 60);
            _dirty = true;
        }

        /* ---------------- 刷新循环 ---------------- */
        private void Pump()
        {
            string[] toast;
            while (_toastQueue.TryDequeue(out toast)) ShowToast(toast[0], toast[1]);

            Dictionary<string, object> ext;
            while (_extQueue.TryDequeue(out ext)) HandleExtUi(ext);

            if (!_dirty)
            {
                if (_chat.PendingLayout) _chat.Relayout();
                return;
            }
            _dirty = false;
            RefreshUi();
        }

        private void RefreshUi()
        {
            _syncing = true;
            try
            {
                SyncSidebar();
                SyncTopBar();
                SyncChat();
                SyncStatus();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void SyncSidebar()
        {
            List<ListEntry> open = new List<ListEntry>();
            for (int i = 0; i < _sessions.Count; i++)
            {
                Session s = _sessions[i];
                ListEntry e = new ListEntry();
                e.Title = s.Title;
                e.Sub = ShortPath(s.Cwd, 30) + (s.Dead ? "  · 已退出" : "");
                e.Tag = s;
                e.Bold = s == _active;
                if (s.Streaming) e.Dot = Theme.Ok;
                else if (s.Dead) e.Dot = Theme.Err;
                open.Add(e);
            }
            _openList.SetEntries(open);
            for (int i = 0; i < _openList.Items.Count; i++)
            {
                ListEntry e = _openList.Items[i] as ListEntry;
                if (e != null && e.Tag == _active) { _openList.SelectedIndex = i; break; }
            }

            List<ListEntry> hist = new List<ListEntry>();
            for (int i = 0; i < _history.Count; i++)
            {
                SessionInfo si = _history[i];
                ListEntry e = new ListEntry();
                e.Title = si.Title;
                e.Sub = (si.MessageCount > 0 ? si.MessageCount + " 条 · " : "") + si.TimeText;
                e.Tag = si;
                hist.Add(e);
            }
            _historyList.SetEntries(hist);
        }

        private static string ShortPath(string p, int max)
        {
            if (string.IsNullOrEmpty(p)) return "";
            string s = p.Replace('\\', '/');
            return s.Length > max ? "…" + s.Substring(s.Length - max + 1) : s;
        }

        private void SyncTopBar()
        {
            Session s = _active;
            _titleLabel.Text = s == null ? "pi 客户端" : s.Title;
            _titleLabel.ForeColor = s != null && s.Dead ? Theme.Err : Theme.TextCol;
            _dirLabel.Text = s == null ? "—" : s.Cwd;
            if (_pi != null) _footLabel.Text = "pi " + (_pi.Version ?? "?");

            _modelCombo.Enabled = s != null && !s.Dead;
            _thinkCombo.Enabled = s != null && !s.Dead;
            _btnSend.Enabled = s != null && !s.Dead;
            _btnStop.Visible = s != null && s.Streaming;
            _btnCompact.Enabled = s != null && !s.Dead && !s.Streaming;

            if (s != null)
            {
                // 模型下拉
                List<string> items = new List<string>();
                int sel = -1;
                for (int i = 0; i < s.Models.Count; i++)
                {
                    Dictionary<string, object> m = s.Models[i];
                    string label = Json.GetStr(m, "name", Json.GetStr(m, "id", "?"));
                    items.Add(label);
                    if (s.Model != null &&
                        Json.GetStr(s.Model, "provider") == Json.GetStr(m, "provider") &&
                        Json.GetStr(s.Model, "id") == Json.GetStr(m, "id")) sel = i;
                }
                if (!SameItems(items, _modelLabels))
                {
                    _modelLabels = items;
                    _modelCombo.SetItems(items, sel);
                }
                else if (sel >= 0 && _modelCombo.SelectedIndex != sel)
                {
                    _modelCombo.SetItems(items, sel);
                }

                List<string> lv = new List<string>();
                for (int i = 0; i < s.Levels.Count; i++) lv.Add("thinking: " + s.Levels[i]);
                int li = s.Levels.IndexOf(s.ThinkingLevel);
                if (!SameItems(lv, _thinkLabels) || _thinkCombo.SelectedIndex != li)
                {
                    _thinkLabels = lv;
                    _thinkCombo.SetItems(lv, li);
                }

                if (s.HasStats)
                {
                    double pct = s.CtxPercent;
                    _ctxLabel.Text = pct < 10 ? pct.ToString("0.0") + "%" : Math.Round(pct) + "%";
                }
                else _ctxLabel.Text = "—";
            }
            else
            {
                _modelLabels = new List<string>();
                _thinkLabels = new List<string>();
                _modelCombo.SetItems(_modelLabels, -1);
                _thinkCombo.SetItems(_thinkLabels, -1);
                _ctxLabel.Text = "—";
            }
        }

        private static bool SameItems(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void SyncStatus()
        {
            Session s = _active;
            if (s == null)
            {
                _statusLabel.Text = "就绪";
                return;
            }
            StringBuilder sb = new StringBuilder();
            if (_pi != null) sb.Append("pi ").Append(_pi.Version ?? "?").Append("   ");
            sb.Append(s.Streaming ? "生成中…" : (s.Dead ? "会话已退出" : "就绪"));
            if (s.HasStats && s.CtxWindow > 0)
            {
                sb.Append("   ·   上下文 ").Append(Fmt(s.CtxTokens)).Append(" / ").Append(Fmt(s.CtxWindow));
                if (s.Cost > 0) sb.Append("   ·   $").Append(s.Cost.ToString("0.0000"));
            }
            if (s.Steering.Count + s.FollowUp.Count > 0)
                sb.Append("   ·   排队 ").Append(s.Steering.Count + s.FollowUp.Count).Append(" 条");
            for (int i = 0; i < s.Statuses.Count; i++)
            {
                foreach (KeyValuePair<string, string> kv in s.Statuses)
                {
                    sb.Append("   ·   ").Append(kv.Value);
                    break;
                }
            }
            _statusLabel.Text = sb.ToString();
            _statusLabel.ForeColor = s.Dead ? Theme.Err : (s.Streaming ? Theme.Warn : Theme.Muted2);
        }

        private static string Fmt(long n)
        {
            if (n >= 1000000) return (n / 1000000.0).ToString("0.0") + "M";
            if (n >= 1000) return (n / 1000).ToString("0") + "k";
            return n.ToString();
        }

        /* ---------------- 对话渲染 ---------------- */
        private void SyncChat()
        {
            Session s = _active;
            bool pinned = _chat.NearBottom;
            if (s == null)
            {
                if (_chat.Items.Count > 0) _chat.ClearAll();
                return;
            }
            if (_forceRebuild)
            {
                _forceRebuild = false;
                s.NeedsRebuild = false;
                RebuildChat(s);
                if (pinned) _chat.ScrollToBottom();
                return;
            }
            // 增量：补齐消息与块
            while (_chat.Items.Count < s.Messages.Count)
            {
                int i = _chat.Items.Count;
                MessageView v = new MessageView(s.Messages[i]);
                _chat.Add(v);
            }
            for (int i = 0; i < _chat.Items.Count && i < s.Messages.Count; i++)
            {
                _chat.Items[i].Sync();
            }
            _chat.Relayout();
            if (pinned) _chat.ScrollToBottom();
        }

        private void RebuildChat(Session s)
        {
            _chat.SuspendLayout();
            _chat.ClearAll();
            for (int i = 0; i < s.Messages.Count; i++)
            {
                MessageView v = new MessageView(s.Messages[i]);
                _chat.Add(v);
            }
            _chat.ResumeLayout(true);
            _chat.Relayout();
        }

        /* ---------------- 发送 / 中止 ---------------- */
        private async void SendMessage()
        {
            Session s = _active;
            if (s == null || s.Dead) return;
            string text = _input.Text.Trim();
            if (text.Length == 0 && _attachments.Count == 0) return;

            if (text.StartsWith("/"))
            {
                if (RunLocalCommand(text)) return;
            }

            List<Dictionary<string, object>> images = null;
            if (_attachments.Count > 0)
            {
                images = new List<Dictionary<string, object>>(_attachments);
                _attachments.Clear();
                UpdateAttachLabel();
            }

            _input.Text = "";
            HidePalette();
            _dirty = true;

            string err = await s.PromptAsync(text, images);
            if (!string.IsNullOrEmpty(err)) ShowToast("发送失败：" + err, "error");
            _dirty = true;
        }

        private void AbortCurrent()
        {
            Session s = _active;
            if (s == null) return;
            Task.Run(async delegate
            {
                string err = await s.AbortAsync();
                if (!string.IsNullOrEmpty(err))
                {
                    string[] item = new string[3];
                    item[0] = "中止失败：" + err;
                    item[1] = "error";
                    item[2] = s.Cwd;
                    _toastQueue.Enqueue(item);
                }
                _dirty = true;
            });
        }

        private bool RunLocalCommand(string text)
        {
            string[] parts = text.Substring(1).Split(new char[] { ' ' }, 2);
            string name = parts[0].ToLowerInvariant();
            string rest = parts.Length > 1 ? parts[1].Trim() : "";
            if (name == "new") { _input.Text = ""; NewSession(); return true; }
            if (name == "compact") { _input.Text = ""; Compact(); return true; }
            if (name == "export") { _input.Text = ""; ExportHtml(); return true; }
            if (name == "stop") { _input.Text = ""; AbortCurrent(); return true; }
            if (name == "dir") { _input.Text = ""; PickDirectory(); return true; }
            if (name == "rename") { _input.Text = ""; RenameSession(); return true; }
            if (name == "setup") { _input.Text = ""; SetupPi(false); return true; }
            return false;
        }

        private void Compact()
        {
            Session s = _active;
            if (s == null || s.Streaming) return;
            ShowToast("正在压缩上下文…", "info");
            Task.Run(async delegate
            {
                string err = await s.CompactAsync();
                string[] item = new string[3];
                item[0] = err == null ? "上下文已压缩" : "压缩失败：" + err;
                item[1] = err == null ? "info" : "error";
                item[2] = s.Cwd;
                _toastQueue.Enqueue(item);
                _forceRebuild = true;
                _dirty = true;
            });
        }

        private void ExportHtml()
        {
            Session s = _active;
            if (s == null) return;
            Task.Run(async delegate
            {
                string path = await s.ExportAsync();
                string[] item = new string[3];
                if (path != null && path.StartsWith("ERR:"))
                {
                    item[0] = "导出失败：" + path.Substring(4);
                    item[1] = "error";
                }
                else
                {
                    item[0] = "已导出：" + path;
                    item[1] = "info";
                    try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
                    catch { }
                }
                item[2] = s.Cwd;
                _toastQueue.Enqueue(item);
            });
        }

        private void RenameSession()
        {
            Session s = _active;
            if (s == null) return;
            string name = Dlg.Input(this, "重命名会话", "会话名称：", s.Name ?? s.Title, false);
            if (name == null) return;
            Task.Run(async delegate
            {
                string err = await s.RenameAsync(name);
                string[] item = new string[3];
                item[0] = err == null ? "已重命名" : "重命名失败：" + err;
                item[1] = err == null ? "info" : "error";
                item[2] = s.Cwd;
                _toastQueue.Enqueue(item);
                _dirty = true;
            });
        }

        /* ---------------- 输入框 ---------------- */
        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (_palette.Visible)
            {
                if (e.KeyCode == Keys.Down) { if (_palette.SelectedIndex < _palette.Items.Count - 1) _palette.SelectedIndex++; e.Handled = true; return; }
                if (e.KeyCode == Keys.Up) { if (_palette.SelectedIndex > 0) _palette.SelectedIndex--; e.Handled = true; return; }
                if (e.KeyCode == Keys.Tab || (e.KeyCode == Keys.Enter && !e.Shift)) { ApplyPalette(); e.Handled = true; e.SuppressKeyPress = true; return; }
                if (e.KeyCode == Keys.Escape) { HidePalette(); e.Handled = true; return; }
            }

            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SendMessage();
                return;
            }
            if (e.Control && e.KeyCode == Keys.V)
            {
                TryPasteImage();
            }
            if (e.Control && e.KeyCode == Keys.N)
            {
                e.Handled = true;
                NewSession();
            }
        }

        private void OnPaletteKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { HidePalette(); _input.Focus(); }
        }

        private void TryPasteImage()
        {
            try
            {
                if (!Clipboard.ContainsImage()) return;
                Image img = Clipboard.GetImage();
                if (img == null) return;
                using (MemoryStream ms = new MemoryStream())
                {
                    img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    string b64 = Convert.ToBase64String(ms.ToArray());
                    Dictionary<string, object> item = new Dictionary<string, object>();
                    item["type"] = "image";
                    item["data"] = b64;
                    item["mimeType"] = "image/png";
                    _attachments.Add(item);
                }
                img.Dispose();
                UpdateAttachLabel();
            }
            catch { }
        }

        private void UpdatePlaceholder()
        {
            if (_placeholder == null) return;
            _placeholder.Visible = _input.Text.Length == 0;
            if (_placeholder.Visible) _placeholder.BringToFront();
        }

        private void UpdateAttachLabel()
        {
            bool has = _attachments.Count > 0;
            _attachLabel.Text = has ? "已附加 " + _attachments.Count + " 张图片（随消息一起发送）" : "";
            _btnAttachClear.Visible = has;
        }

        private void SlashPalette()
        {
            string t = _input.Text;
            if (!t.StartsWith("/") || t.IndexOf('\n') >= 0 || t.IndexOf(' ') > 0)
            {
                HidePalette();
                return;
            }
            string q = t.Substring(1).ToLowerInvariant();
            List<string> items = new List<string>();
            string[] local = new string[] { "new", "compact", "export", "stop", "dir", "rename", "setup" };
            for (int i = 0; i < local.Length; i++)
                if (local[i].StartsWith(q)) items.Add("/" + local[i] + "   （客户端）");
            if (_active != null)
            {
                for (int i = 0; i < _active.Commands.Count; i++)
                {
                    Dictionary<string, object> c = _active.Commands[i];
                    string name = Json.GetStr(c, "name", "");
                    if (name.Length == 0 || !name.ToLowerInvariant().StartsWith(q)) continue;
                    string desc = Json.GetStr(c, "description", "");
                    items.Add("/" + name + (desc.Length > 0 ? "   " + Json.OneLine(desc, 50) : ""));
                    if (items.Count > 12) break;
                }
            }
            if (items.Count == 0) { HidePalette(); return; }
            _palette.Items.Clear();
            for (int i = 0; i < items.Count && i < 14; i++) _palette.Items.Add(items[i]);
            _palette.SelectedIndex = 0;
            _palette.Visible = true;
            PositionPalette();
        }

        private void PositionPalette()
        {
            int h = Math.Min(S(220), S(22) * _palette.Items.Count + S(4));
            _palette.Bounds = new Rectangle(_composer.Left + S(20), _composer.Top - h - S(4), S(420), h);
            _palette.BringToFront();
        }

        private void HidePalette()
        {
            if (_palette.Visible) _palette.Visible = false;
        }

        private void ApplyPalette()
        {
            if (!_palette.Visible || _palette.SelectedIndex < 0) return;
            string s = Convert.ToString(_palette.Items[_palette.SelectedIndex]);
            int sp = s.IndexOf(' ');
            string cmd = sp > 0 ? s.Substring(0, sp) : s;
            _input.Text = cmd + " ";
            _input.SelectionStart = _input.Text.Length;
            HidePalette();
            _input.Focus();
        }

        /* ---------------- 下拉框 ---------------- */
        private void OnModelChanged(object sender, EventArgs e)
        {
            if (_syncing || _active == null) return;
            int i = _modelCombo.SelectedIndex;
            if (i < 0 || i >= _active.Models.Count) return;
            Dictionary<string, object> m = _active.Models[i];
            string provider = Json.GetStr(m, "provider", "");
            string id = Json.GetStr(m, "id", "");
            if (provider.Length == 0 || id.Length == 0) return;
            Session s = _active;
            Task.Run(async delegate
            {
                string err = await s.SetModelAsync(provider, id);
                if (!string.IsNullOrEmpty(err))
                {
                    string[] item = new string[3];
                    item[0] = "切换模型失败：" + err;
                    item[1] = "error";
                    item[2] = s.Cwd;
                    _toastQueue.Enqueue(item);
                }
                _dirty = true;
            });
        }

        private void OnThinkChanged(object sender, EventArgs e)
        {
            if (_syncing || _active == null) return;
            int i = _thinkCombo.SelectedIndex;
            if (i < 0 || i >= _active.Levels.Count) return;
            string level = _active.Levels[i];
            Session s = _active;
            Task.Run(async delegate
            {
                string err = await s.SetThinkingAsync(level);
                if (!string.IsNullOrEmpty(err))
                {
                    string[] item = new string[3];
                    item[0] = "设置思考等级失败：" + err;
                    item[1] = "error";
                    item[2] = s.Cwd;
                    _toastQueue.Enqueue(item);
                }
                _dirty = true;
            });
        }

        /* ---------------- 宿主扩展 UI ---------------- */
        private void HandleExtUi(Dictionary<string, object> req)
        {
            string id = Json.GetStr(req, "id");
            string method = Json.GetStr(req, "method");
            Dictionary<string, object> resp = new Dictionary<string, object>();
            resp["type"] = "extension_ui_response";
            resp["id"] = id;

            if (method == "confirm")
            {
                bool ok = MessageBox.Show(this, Json.GetStr(req, "message", ""), Json.GetStr(req, "title", "确认"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
                resp["confirmed"] = ok;
            }
            else if (method == "select")
            {
                List<object> arr = Json.Arr(Get(req, "options"));
                List<string> opts = new List<string>();
                for (int i = 0; i < arr.Count; i++) opts.Add(Json.Str(arr[i]));
                string sel = Dlg.Select(this, Json.GetStr(req, "title", "请选择"), opts);
                if (sel == null) resp["cancelled"] = true;
                else resp["value"] = sel;
            }
            else
            {
                bool multi = method == "editor";
                string val = Dlg.Input(this, Json.GetStr(req, "title", "输入"), Json.GetStr(req, "placeholder", ""), Json.GetStr(req, "prefill", ""), multi);
                if (val == null) resp["cancelled"] = true;
                else resp["value"] = val;
            }

            Session s = _active;
            if (s != null && s.Rpc != null)
            {
                try { s.Rpc.Send(resp); }
                catch { }
            }
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        /* ---------------- 提示 ---------------- */
        private void ShowToast(string message, string kind)
        {
            if (string.IsNullOrEmpty(message)) return;
            _toastLabel.Text = message;
            _toastLabel.ForeColor = kind == "error" ? Color.FromArgb(255, 190, 190) : (kind == "warn" ? Theme.Warn : Theme.TextCol);
            int w = Math.Min(S(460), TextMeter.Width(message, Theme.UiSmall) + S(28));
            _toastLabel.Bounds = new Rectangle(S(12), S(6), w - S(24), S(20));
            _toastPanel.Size = new Size(w, S(32));
            _toastPanel.Visible = true;
            PositionToast();
            _toastPanel.BringToFront();
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void PositionToast()
        {
            _toastPanel.Left = Math.Max(0, ClientSize.Width - _toastPanel.Width - S(20));
            _toastPanel.Top = ClientSize.Height - _statusBar.Height - _toastPanel.Height - S(16);
        }

        /* ---------------- 自检模式 ---------------- */
        public void SetSmokePrompt(string prompt)
        {
            _smokePrompt = prompt;
        }

        public void SetShotOnly()
        {
            _shotOnly = true;
        }

        private void StartSmoke()
        {
            Timer t = new Timer();
            t.Interval = 500;
            t.Tick += delegate
            {
                _smokeTick++;
                if (_active == null) return;
                bool busy = _active.Streaming;
                int msgCount = _active.Messages.Count;
                if (_smokeTick < 240 && (busy || msgCount < 2 || _active.Messages.Count == 0)) return;
                t.Stop();
                Program.Trace("smoke capture tick=" + _smokeTick);
                CaptureSmoke();
            };
            t.Start();
            Task.Run(async delegate
            {
                await Task.Delay(1500);
                Program.Trace("smoke prompt ->");
                if (_active != null)
                {
                    string err = await _active.PromptAsync(_smokePrompt, null);
                    Program.Trace("smoke prompt result: " + (err == null ? "ok" : err));
                }
                else Program.Trace("smoke: no active session");
                _dirty = true;
            });
        }

        private void StartShot()
        {
            Timer t = new Timer();
            t.Interval = 600;
            t.Tick += delegate
            {
                _smokeTick++;
                if (_smokeTick < 14) return;
                t.Stop();
                CaptureSmoke();
            };
            t.Start();
        }

        private void CaptureSmoke()
        {
            if (_smokeDone) return;
            _smokeDone = true;
            try
            {
                RefreshUi();
                Bitmap bmp = new Bitmap(Width, Height);
                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke.png");
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-result.txt"), "OK " + path);
                Program.Trace("captured");
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-result.txt"), "FAIL " + ex.Message); } catch { }
            }
            Close();
        }
    }
}
