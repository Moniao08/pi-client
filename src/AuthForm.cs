using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>「没有可用模型 / 未配置 API」时弹出的引导窗口</summary>
    internal class AuthForm : Form
    {
        private readonly PiInstall _pi;
        private readonly string _cwd;
        private readonly string _detail;

        public AuthForm(PiInstall pi, string cwd, string detail)
        {
            _pi = pi;
            _cwd = cwd;
            _detail = detail;

            Text = "配置模型 / API";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Panel;
            ForeColor = Theme.TextCol;
            Font = Theme.Ui;
            ClientSize = new Size(620, 470);
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) Icon = ico;
            }
            catch { }

            Label title = new Label();
            title.Text = "还没有可用的模型";
            title.Font = Theme.H2;
            title.AutoSize = false;
            title.Bounds = new Rectangle(20, 16, 580, 26);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "pi 本体需要先登录或配置一个厂商的 API Key，客户端才能选模型、发消息。\n"
                     + "客户端不存你的密钥，配置一次后终端里的 pi 和这个客户端会共用。";
            sub.ForeColor = Theme.Muted;
            sub.AutoSize = false;
            sub.Bounds = new Rectangle(20, 46, 580, 44);
            Controls.Add(sub);

            int y = 96;

            FlatButton login = MakeButton("① 打开登录终端（推荐）", 20, y, 250, true);
            login.Click += delegate { RunLogin(); };
            AddTip("在终端里输入 /login 选择厂商（支持订阅登录或粘贴 API Key），完成后回到本窗口点「重新检测」。", 20, y + 34);
            y += 74;

            FlatButton env = MakeButton("② 用环境变量 / 命令行", 20, y, 250, false);
            env.Click += delegate { ShowEnvHelp(); };
            AddTip("也可以在启动 pi 之前设置 ANTHROPIC_API_KEY、OPENAI_API_KEY 等环境变量。", 20, y + 34);
            y += 74;

            FlatButton dir = MakeButton("③ 打开配置目录", 20, y, 250, false);
            dir.Click += delegate { OpenPiDir(); };
            AddTip("auth.json / settings.json 都在 ~\\.pi\\agent 下，可手动检查或备份。", 20, y + 34);
            y += 70;

            Label det = new Label();
            det.Text = string.IsNullOrEmpty(detail) ? "检测结果：get_available_models 返回空列表。" : "检测结果：" + detail;
            det.ForeColor = Theme.Warn;
            det.AutoSize = false;
            det.Bounds = new Rectangle(20, y, 580, 40);
            Controls.Add(det);

            FlatButton retry = new FlatButton("重新检测");
            retry.Primary = true;
            retry.Bounds = new Rectangle(402, ClientSize.Height - 46, 100, 30);
            retry.Click += delegate { DialogResult = DialogResult.Retry; Close(); };
            Controls.Add(retry);

            FlatButton close = new FlatButton("关闭");
            close.Bounds = new Rectangle(512, ClientSize.Height - 46, 88, 30);
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(close);

            CancelButton = close;
        }

        private void AddTip(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = Theme.Muted2;
            l.AutoSize = false;
            l.Bounds = new Rectangle(x, y, 580, 30);
            Controls.Add(l);
        }

        private FlatButton MakeButton(string text, int x, int y, int w, bool primary)
        {
            FlatButton b = new FlatButton(text);
            b.Bounds = new Rectangle(x, y, w, 30);
            if (primary) b.Primary = true;
            Controls.Add(b);
            return b;
        }

        private void RunLogin()
        {
            try
            {
                string exe = _pi.PiCmd;
                string args = null;
                if (_pi.IsRpc)
                {
                    exe = _pi.NodeExe;
                    args = "\"" + Path.Combine(Path.GetDirectoryName(_pi.RpcEntry), "cli.js") + "\"";
                }
                string cmd = args == null ? "\"" + exe + "\"" : "\"" + exe + "\" " + args;
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe");
                psi.UseShellExecute = true;
                psi.Arguments = "/c start \"pi 登录\" cmd.exe /k \"cd /d \\\"" + _cwd + "\\\" && " + cmd + "\"";
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开失败：" + ex.Message, "pi 客户端", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowEnvHelp()
        {
            string text =
                "在启动 pi 之前设置以下任一环境变量（重启客户端后生效）：\r\n\r\n" +
                "  ANTHROPIC_API_KEY        Claude\r\n" +
                "  OPENAI_API_KEY           OpenAI / Codex\r\n" +
                "  GEMINI_API_KEY           Google Gemini\r\n" +
                "  DEEPSEEK_API_KEY         DeepSeek\r\n" +
                "  OPENROUTER_API_KEY       OpenRouter\r\n\r\n" +
                "设置方式（当前用户，永久）：\r\n" +
                "  setx ANTHROPIC_API_KEY \"sk-ant-...\"\r\n\r\n" +
                "或者直接写进 " + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi", "agent", "auth.json") + "\r\n" +
                "（格式与 pi 自己的 /login 生成的一致，建议优先用 /login）";
            MessageBox.Show(this, text, "环境变量方式", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenPiDir()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pi", "agent");
                if (Directory.Exists(dir)) Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }
    }
}
