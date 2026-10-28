using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>配色与字体</summary>
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(10, 13, 20);
        public static readonly Color Panel = Color.FromArgb(15, 20, 32);
        public static readonly Color Panel2 = Color.FromArgb(20, 27, 41);
        public static readonly Color Panel3 = Color.FromArgb(28, 37, 56);
        public static readonly Color Border = Color.FromArgb(34, 44, 61);
        public static readonly Color BorderSoft = Color.FromArgb(26, 34, 49);
        public static readonly Color TextCol = Color.FromArgb(231, 236, 245);
        public static readonly Color Muted = Color.FromArgb(140, 152, 175);
        public static readonly Color Muted2 = Color.FromArgb(102, 114, 140);
        public static readonly Color Accent = Color.FromArgb(91, 140, 255);
        public static readonly Color Accent2 = Color.FromArgb(154, 107, 255);
        public static readonly Color Ok = Color.FromArgb(62, 207, 142);
        public static readonly Color Err = Color.FromArgb(255, 107, 107);
        public static readonly Color Warn = Color.FromArgb(246, 193, 119);
        public static readonly Color CodeBg = Color.FromArgb(12, 17, 27);
        public static readonly Color CodeText = Color.FromArgb(207, 220, 241);

        public static Font Ui;
        public static Font UiBold;
        public static Font UiSmall;
        public static Font Body;
        public static Font BodyBold;
        public static Font BodyItalic;
        public static Font BodyBoldItalic;
        public static Font Mono;
        public static Font MonoSmall;
        public static Font H1;
        public static Font H2;
        public static Font H3;

        public static float Scale = 1f;

        static Theme()
        {
            BuildFonts(1f);
        }

        /// <summary>按需放大字号（系统 125% 缩放但窗口 DPI 仍报 96 时用）</summary>
        public static void Init(float scale)
        {
            if (scale < 0.5f) scale = 1f;
            if (scale > 3f) scale = 3f;
            Scale = scale;
            BuildFonts(scale);
        }

        private static void BuildFonts(float k)
        {
            string ui = PickFont("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma");
            string mono = PickFont("Cascadia Mono", "Consolas", "Courier New");

            Ui = new Font(ui, 9f * k);
            UiBold = new Font(ui, 9f * k, FontStyle.Bold);
            UiSmall = new Font(ui, 8f * k);
            Body = new Font(ui, 9.75f * k);
            BodyBold = new Font(ui, 9.75f * k, FontStyle.Bold);
            BodyItalic = new Font(ui, 9.75f * k, FontStyle.Italic);
            BodyBoldItalic = new Font(ui, 9.75f * k, FontStyle.Bold | FontStyle.Italic);
            Mono = new Font(mono, 9f * k);
            MonoSmall = new Font(mono, 8.25f * k);
            H1 = new Font(ui, 13f * k, FontStyle.Bold);
            H2 = new Font(ui, 11.5f * k, FontStyle.Bold);
            H3 = new Font(ui, 10.5f * k, FontStyle.Bold);
        }

        public static string PickFont(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    using (Font f = new Font(names[i], 9f))
                    {
                        if (string.Equals(f.Name, names[i], StringComparison.OrdinalIgnoreCase)) return names[i];
                    }
                }
                catch { }
            }
            return "Segoe UI";
        }

        public static Color WithAlpha(Color c, int a)
        {
            return Color.FromArgb(a, c);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(fill))
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(b, p);
                g.SmoothingMode = old;
            }
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = RoundRect(r, radius))
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border.A > 0)
                {
                    using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
                }
                g.SmoothingMode = old;
            }
        }
    }
}
