using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>首次运行 / 需要绑定 pi 时的选择窗口</summary>
    internal class SetupForm : Form
    {
        private readonly Config _cfg;
        private readonly bool _firstRun;
        private List<PiInstall> _found = new List<PiInstall>();
        private ListBox _list;
        private Label _hint;
        private TextBox _cmdBox;
        private FlatButton _btnUse;
        private FlatButton _btnRescan;
        private FlatButton _btnBrowse;
        private FlatButton _btnInstall;
        private FlatButton _btnCopy;
        private FlatButton _btnCancel;

        public PiInstall Selected;

        public SetupForm(Config cfg, bool firstRun)
        {
            _cfg = cfg;
            _firstRun = firstRun;

            Text = firstRun ? "pi 客户端 · 首次设置" : "绑定本机的 pi";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Panel;
            ForeColor = Theme.TextCol;
            Font = Theme.Ui;
            ClientSize = new Size(660, 470);
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) Icon = ico;
            }
            catch { }

            Label title = new Label();
            title.Text = firstRun ? "选择一个要绑定的 pi" : "选择要绑定的 pi";
            title.Font = Theme.H2;
            title.AutoSize = false;
            title.Bounds = new Rectangle(18, 14, 620, 26);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "客户端调用你电脑上已有的 pi（不含 AI 能力）。选一个即可，之后可在右上角 ⚙ 更换。";
            sub.ForeColor = Theme.Muted;
            sub.AutoSize = false;
            sub.Bounds = new Rectangle(18, 42, 620, 34);
            Controls.Add(sub);

            _list = new ListBox();
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.BackColor = Theme.Panel2;
            _list.ForeColor = Theme.TextCol;
            _list.Font = Theme.Body;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 38;
            _list.Bounds = new Rectangle(18, 80, 624, 190);
            _list.DrawItem += DrawItem;
            _list.SelectedIndexChanged += delegate { UpdateSelection(); };
            _list.DoubleClick += delegate { UseSelected(); };
            Controls.Add(_list);

            _hint = new Label();
            _hint.ForeColor = Theme.Muted;
            _hint.AutoSize = false;
            _hint.Bounds = new Rectangle(18, 278, 624, 20);
            Controls.Add(_hint);

            Label cmdLabel = new Label();
            cmdLabel.Text = "没有安装？可以在本机执行官方安装命令（会打开一个命令行窗口显示进度）：";
            cmdLabel.ForeColor = Theme.Muted;
            cmdLabel.AutoSize = false;
            cmdLabel.Bounds = new Rectangle(18, 304, 624, 20);
            Controls.Add(cmdLabel);

            _cmdBox = new TextBox();
            _cmdBox.ReadOnly = true;
            _cmdBox.BorderStyle = BorderStyle.FixedSingle;
            _cmdBox.BackColor = Theme.Panel2;
            _cmdBox.ForeColor = Theme.CodeText;
            _cmdBox.Font = Theme.MonoSmall;
            _cmdBox.Bounds = new Rectangle(18, 326, 624, 24);
            Controls.Add(_cmdBox);

            _btnInstall = MakeBtn("安装 pi", 18, 356, 100, true);
            _btnInstall.Click += delegate { InstallPi(); };
            _btnCopy = MakeBtn("复制命令", 124, 356, 90);
            _btnCopy.Click += delegate
            {
                try { Clipboard.SetText(_cmdBox.Text); _btnCopy.Text = "已复制"; }
                catch { }
            };
            _btnBrowse = MakeBtn("手动选择目录…", 220, 356, 118);
            _btnBrowse.Click += delegate { BrowseFolder(); };
            _btnRescan = MakeBtn("重新检测", 344, 356, 90);
            _btnRescan.Click += delegate { Rescan(); };
            _btnUse = MakeBtn("使用选中的 pi", 434, 356, 118, true);
            _btnUse.Click += delegate { UseSelected(); };
            _btnCancel = MakeBtn("取消", 558, 356, 84);
            _btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Label tip = new Label();
            tip.Text = "提示：安装完成后点「重新检测」；也可以用 pi 官方脚本（Git Bash）：curl -fsSL https://pi.dev/install.sh | sh";
            tip.ForeColor = Theme.Muted2;
            tip.AutoSize = false;
            tip.Bounds = new Rectangle(18, 396, 624, 32);
            Controls.Add(tip);

            FlatButton docs = MakeBtn("打开 pi 文档", 18, 432, 104);
            docs.Click += delegate { PiLocator.OpenUrl("https://pi.dev"); };
            AcceptButton = _btnUse;
            CancelButton = _btnCancel;

            Rescan();
        }

        private FlatButton MakeBtn(string text, int x, int y, int w, bool primary = false)
        {
            FlatButton b = new FlatButton(text);
            b.Bounds = new Rectangle(x, y, w, 30);
            if (primary) b.Primary = true;
            Controls.Add(b);
            return b;
        }

        private void DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            PiInstall p = _found[e.Index];
            bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush b = new SolidBrush(sel ? Theme.Panel3 : Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
            string head = "pi " + (p.Version ?? "?") + "   " + (p.Source ?? "");
            TextRenderer.DrawText(e.Graphics, head, Theme.UiBold,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 3, e.Bounds.Width - 16, 18), Theme.TextCol,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, p.Home, Theme.UiSmall,
                new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 20, e.Bounds.Width - 16, 16), Theme.Muted2,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void Rescan()
        {
            _found = PiLocator.Detect(_cfg);
            _list.Items.Clear();
            for (int i = 0; i < _found.Count; i++) _list.Items.Add(_found[i].Home);
            if (_found.Count > 0)
            {
                _list.SelectedIndex = 0;
                _hint.Text = "已找到 " + _found.Count + " 个 pi，选择后点「使用选中的 pi」。";
                _hint.ForeColor = Theme.Ok;
            }
            else
            {
                _hint.Text = "本机没有检测到 pi —— 先安装，或者手动指定安装目录。";
                _hint.ForeColor = Theme.Warn;
            }
            string cmd = PiLocator.BuildInstallCommand(_found.Count > 0 ? _found[0] : null);
            _cmdBox.Text = cmd != null ? cmd : PiLocator.InstallCommandText;
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            _btnUse.Enabled = _list.SelectedIndex >= 0 && _list.SelectedIndex < _found.Count;
        }

        private void UseSelected()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _found.Count) return;
            Selected = _found[i];
            DialogResult = DialogResult.OK;
            Close();
        }

        private void InstallPi()
        {
            string cmd = _cmdBox.Text;
            if (MessageBox.Show(this,
                "将在新的命令行窗口执行：\r\n\r\n" + cmd + "\r\n\r\n装完后回到本窗口点「重新检测」。是否继续？",
                "安装 pi", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (PiLocator.FindInPath("npm.cmd") == null && PiLocator.BuildInstallCommand(null) == null)
            {
                MessageBox.Show(this, "本机没有 npm，无法自动安装。\r\n请先安装 Node.js，或使用首页文档里的官方安装脚本。",
                    "缺少 npm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                PiLocator.OpenUrl("https://pi.dev");
                return;
            }
            PiLocator.RunInConsole(cmd);
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择 pi 的安装目录（包含 node.exe / node_modules 的那一层，或 pi-coding-agent 包目录）";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                PiInstall p = PiLocator.Inspect(d.SelectedPath);
                if (p == null)
                {
                    if (MessageBox.Show(this, "该目录里没找到 pi（没有 node_modules\\@earendil-works\\pi-coding-agent，也没有 pi.cmd）。\r\n仍要把它记下来吗？",
                        "未识别到 pi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    _cfg.PiHome = d.SelectedPath;
                    _cfg.Save();
                    Rescan();
                    return;
                }
                p.Source = "手动选择";
                Selected = p;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
