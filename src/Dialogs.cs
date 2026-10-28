using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>通用小对话框（暗色）</summary>
    internal static class Dlg
    {
        public static Form MakeDialog(string title, int w, int h)
        {
            Form f = new Form();
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ShowInTaskbar = false;
            f.BackColor = Theme.Panel;
            f.ForeColor = Theme.TextCol;
            f.Font = Theme.Ui;
            f.ClientSize = new Size(w, h);
            return f;
        }

        public static string Input(IWin32Window owner, string title, string label, string value, bool multiline)
        {
            using (Form f = MakeDialog(title, 460, multiline ? 300 : 170))
            {
                Label lb = new Label();
                lb.Text = label;
                lb.AutoSize = false;
                lb.Bounds = new Rectangle(16, 14, 428, 20);
                lb.ForeColor = Theme.Muted;
                f.Controls.Add(lb);

                TextBox tb = new TextBox();
                tb.Multiline = multiline;
                tb.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.BackColor = Theme.Panel2;
                tb.ForeColor = Theme.TextCol;
                tb.Font = Theme.Body;
                tb.Bounds = new Rectangle(16, 38, 428, multiline ? 200 : 26);
                tb.Text = value ?? "";
                tb.SelectAll();
                f.Controls.Add(tb);

                FlatButton ok = new FlatButton("确定");
                ok.Primary = true;
                ok.Bounds = new Rectangle(344, f.ClientSize.Height - 44, 100, 30);
                FlatButton cancel = new FlatButton("取消");
                cancel.Bounds = new Rectangle(260, f.ClientSize.Height - 44, 78, 30);
                ok.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
                cancel.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                tb.Focus();
                f.Shown += delegate { tb.Focus(); tb.SelectAll(); };
                return f.ShowDialog(owner) == DialogResult.OK ? tb.Text : null;
            }
        }

        public static bool Confirm(IWin32Window owner, string title, string message)
        {
            return MessageBox.Show(owner, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        public static string Select(IWin32Window owner, string title, List<string> options)
        {
            using (Form f = MakeDialog(title, 480, 320))
            {
                ListBox lb = new ListBox();
                lb.BorderStyle = BorderStyle.FixedSingle;
                lb.BackColor = Theme.Panel2;
                lb.ForeColor = Theme.TextCol;
                lb.Font = Theme.Body;
                lb.DrawMode = DrawMode.OwnerDrawFixed;
                lb.ItemHeight = 26;
                lb.Bounds = new Rectangle(16, 16, 448, 240);
                for (int i = 0; i < options.Count; i++) lb.Items.Add(options[i]);
                lb.DrawItem += delegate(object s, DrawItemEventArgs e)
                {
                    if (e.Index < 0) return;
                    bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                    using (SolidBrush b = new SolidBrush(sel ? Theme.Panel3 : Theme.Panel2)) e.Graphics.FillRectangle(b, e.Bounds);
                    TextRenderer.DrawText(e.Graphics, Convert.ToString(lb.Items[e.Index]), Theme.Body,
                        new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                        sel ? Color.White : Theme.TextCol, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                };
                if (options.Count > 0) lb.SelectedIndex = 0;
                f.Controls.Add(lb);

                FlatButton ok = new FlatButton("确定");
                ok.Primary = true;
                ok.Bounds = new Rectangle(364, f.ClientSize.Height - 44, 100, 30);
                FlatButton cancel = new FlatButton("取消");
                cancel.Bounds = new Rectangle(280, f.ClientSize.Height - 44, 78, 30);
                ok.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
                cancel.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
                lb.DoubleClick += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK ? Convert.ToString(lb.SelectedItem) : null;
            }
        }
    }
}
