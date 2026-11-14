using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>检查更新结果窗口</summary>
    internal class UpdateForm : Form
    {
        private readonly Updater.Release _rel;
        private readonly TextBox _notes;
        private readonly FlatButton _install;
        private readonly FlatButton _page;
        private readonly FlatButton _later;
        private readonly Label _status;

        public bool Installed;

        public UpdateForm(Updater.Release rel)
        {
            _rel = rel;

            Text = "发现新版本";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Panel;
            ForeColor = Theme.TextCol;
            Font = Theme.Ui;
            ClientSize = new Size(640, 520);
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) Icon = ico;
            }
            catch { }

            Label title = new Label();
            title.Text = "发现新版本 " + rel.Version;
            title.Font = Theme.H2;
            title.AutoSize = false;
            title.Bounds = new Rectangle(20, 14, 600, 26);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = string.Format("当前版本 v{0} → 最新版本 {1}（来源：{2}）", Program.Version, rel.Version, rel.Source);
            sub.ForeColor = Theme.Muted;
            sub.AutoSize = false;
            sub.Bounds = new Rectangle(20, 42, 600, 20);
            Controls.Add(sub);

            _notes = new TextBox();
            _notes.Multiline = true;
            _notes.ReadOnly = true;
            _notes.ScrollBars = ScrollBars.Vertical;
            _notes.BorderStyle = BorderStyle.FixedSingle;
            _notes.BackColor = Theme.Panel2;
            _notes.ForeColor = Theme.CodeText;
            _notes.Font = Theme.Ui;
            _notes.Bounds = new Rectangle(20, 70, 600, 340);
            _notes.Text = string.IsNullOrEmpty(rel.Body) ? "（该版本没有提供说明）" : rel.Body.Replace("\r\n", "\n").Replace("\n", "\r\n");
            _notes.HandleCreated += delegate { Native.DarkScrollBar(_notes.Handle); };
            Controls.Add(_notes);

            _status = new Label();
            _status.Text = rel.HasDownload ? "可以直接在客户端里更新，会自动重启。" : "该版本没有提供可下载的 exe，请打开发布页手动下载。";
            _status.ForeColor = Theme.Muted2;
            _status.AutoSize = false;
            _status.Bounds = new Rectangle(20, 418, 600, 20);
            Controls.Add(_status);

            _install = new FlatButton("立即更新");
            _install.Primary = true;
            _install.Bounds = new Rectangle(392, ClientSize.Height - 46, 100, 30);
            _install.Enabled = rel.HasDownload;
            _install.Click += delegate { InstallNow(); };
            Controls.Add(_install);

            _page = new FlatButton("打开发布页");
            _page.Bounds = new Rectangle(276, ClientSize.Height - 46, 106, 30);
            _page.Click += delegate { OpenPage(); };
            Controls.Add(_page);

            _later = new FlatButton("稍后");
            _later.Bounds = new Rectangle(502, ClientSize.Height - 46, 98, 30);
            _later.Click += delegate { Close(); };
            Controls.Add(_later);

            CancelButton = _later;
        }

        private void OpenPage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_rel.PageUrl)) Process.Start(new ProcessStartInfo(_rel.PageUrl) { UseShellExecute = true });
            }
            catch { }
        }

        private void InstallNow()
        {
            _install.Enabled = false;
            _page.Enabled = false;
            _status.ForeColor = Theme.Warn;
            _status.Text = "正在下载…";
            string exe = Application.ExecutablePath;

            Task.Run(delegate
            {
                try
                {
                    string path = Updater.DownloadInstaller(_rel, null);
                    string err = Updater.ApplyUpdate(path, exe);
                    if (err != null)
                    {
                        BeginInvoke((Action)delegate
                        {
                            _status.ForeColor = Theme.Err;
                            _status.Text = "更新失败：" + err + "（请打开发布页手动下载）";
                            _install.Enabled = true;
                            _page.Enabled = true;
                        });
                        return;
                    }
                    Installed = true;
                    BeginInvoke((Action)delegate
                    {
                        _status.Text = "下载完成，正在重启客户端…";
                        Application.Exit();
                    });
                }
                catch (Exception ex)
                {
                    BeginInvoke((Action)delegate
                    {
                        _status.ForeColor = Theme.Err;
                        _status.Text = "下载失败：" + ex.Message + "（可打开发布页手动下载）";
                        _install.Enabled = true;
                        _page.Enabled = true;
                    });
                }
            });
        }
    }
}
