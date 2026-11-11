using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PiClient
{
    /// <summary>配色与字体（青绿主题）</summary>
    internal static class Theme
    {
        // 底色（由深到浅）
        public static readonly Color Bg = Color.FromArgb(7, 14, 14);
        public static readonly Color Panel = Color.FromArgb(11, 22, 21);
        public static readonly Color Panel2 = Color.FromArgb(16, 31, 29);
        public static readonly Color Panel3 = Color.FromArgb(22, 44, 41);
        public static readonly Color Surface = Color.FromArgb(13, 26, 25);
        public static readonly Color Border = Color.FromArgb(30, 58, 54);
        public static readonly Color BorderSoft = Color.FromArgb(22, 44, 41);

        // 文字
        public static readonly Color TextCol = Color.FromArgb(226, 242, 239);
        public static readonly Color Muted = Color.FromArgb(127, 163, 157);
        public static readonly Color Muted2 = Color.FromArgb(92, 127, 121);

        // 强调色（青绿）
        public static readonly Color Accent = Color.FromArgb(45, 212, 191);
        public static readonly Color Accent2 = Color.FromArgb(52, 211, 153);
        public static readonly Color AccentDim = Color.FromArgb(20, 138, 126);
        public static readonly Color Link = Color.FromArgb(94, 234, 212);
        public static readonly Color OnAccent = Color.FromArgb(4, 26, 24);

        // 语义色
        public static readonly Color Ok = Color.FromArgb(52, 211, 153);
        public static readonly Color Err = Color.FromArgb(248, 113, 113);
        public static readonly Color Warn = Color.FromArgb(251, 191, 116);

        // 代码 / 气泡 / 浮层
        public static readonly Color CodeBg = Color.FromArgb(8, 17, 16);
        public static readonly Color CodeHead = Color.FromArgb(13, 26, 25);
        public static readonly Color CodeText = Color.FromArgb(205, 232, 226);
        public static readonly Color InlineCodeBg = Color.FromArgb(20, 46, 43);
        public static readonly Color InlineCodeText = Color.FromArgb(140, 236, 220);
        public static readonly Color BubbleBg = Color.FromArgb(17, 52, 48);
        public static readonly Color BubbleBorder = Color.FromArgb(30, 84, 77);
        public static readonly Color ToastBg = Color.FromArgb(18, 38, 35);
        public static readonly Color ToastBorder = Color.FromArgb(44, 106, 97);
        public static readonly Color StatusBarBg = Color.FromArgb(9, 18, 17);

        // 选取高亮 / 滚动条
        public static readonly Color Selection = Color.FromArgb(70, 45, 212, 191);
        public static readonly Color ScrollTrack = Color.FromArgb(12, 24, 23);
        public static readonly Color ScrollThumb = Color.FromArgb(45, 79, 74);
        public static readonly Color ScrollThumbHot = Color.FromArgb(66, 108, 101);

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
        public static Font Icon;

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
            Icon = new Font("Segoe MDL2 Assets", 8.5f * k);
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

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
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

        /// <summary>青绿渐变填充（用于头像、图标、主按钮）</summary>
        public static void FillGradient(Graphics g, Rectangle r, int radius)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = RoundRect(r, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)),
                Accent, Accent2, 45f))
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(b, p);
                g.SmoothingMode = old;
            }
        }
    }
}
