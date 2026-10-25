// 轻算计算器 — 标准型 / 科学型
// 目标框架：.NET Framework 4.x (WinForms)，无需额外运行时
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("轻算计算器")]
[assembly: AssemblyProduct("轻算计算器")]
[assembly: AssemblyDescription("Windows 计算器（标准型 / 科学型）")]
[assembly: AssemblyCompany("QingSuan")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace QingSuan
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CalculatorForm());
        }
    }

    internal enum BtnKind
    {
        Digit,
        Operator,
        Func,
        Accent,
        Ghost
    }

    /// <summary>圆角扁平按钮，自绘以获得统一的深色外观与悬停/按下反馈。</summary>
    internal class CalcButton : Button
    {
        public Color BackNormal = Palette.DigitBack;
        public Color BackHover = Palette.DigitHover;
        public Color BackPress = Palette.DigitPress;
        public Color TextColor = Palette.Text;
        public int Radius = 4;

        private bool _hover;
        private bool _pressed;

        public CalcButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = false;
            UseVisualStyleBackColor = false;
            Margin = new Padding(2);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Palette.Background);

            Color back;
            if (!Enabled)
            {
                back = Palette.DisabledBack;
            }
            else if (_pressed)
            {
                back = BackPress;
            }
            else if (_hover)
            {
                back = BackHover;
            }
            else
            {
                back = BackNormal;
            }

            using (GraphicsPath path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (SolidBrush brush = new SolidBrush(back))
            {
                g.FillPath(brush, path);
            }

            Color fg = Enabled ? TextColor : Palette.DisabledText;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal static class Palette
    {
        public static readonly Color Background = Color.FromArgb(32, 32, 32);
        public static readonly Color Text = Color.FromArgb(255, 255, 255);
        public static readonly Color TextMuted = Color.FromArgb(160, 160, 160);
        public static readonly Color DigitBack = Color.FromArgb(59, 59, 59);
        public static readonly Color DigitHover = Color.FromArgb(72, 72, 72);
        public static readonly Color DigitPress = Color.FromArgb(48, 48, 48);
        public static readonly Color OpBack = Color.FromArgb(50, 50, 50);
        public static readonly Color OpHover = Color.FromArgb(62, 62, 62);
        public static readonly Color OpPress = Color.FromArgb(40, 40, 40);
        public static readonly Color FuncBack = Color.FromArgb(43, 43, 43);
        public static readonly Color FuncHover = Color.FromArgb(55, 55, 55);
        public static readonly Color FuncPress = Color.FromArgb(35, 35, 35);
        public static readonly Color AccentBack = Color.FromArgb(76, 194, 255);
        public static readonly Color AccentHover = Color.FromArgb(96, 204, 255);
        public static readonly Color AccentPress = Color.FromArgb(58, 172, 232);
        public static readonly Color AccentText = Color.FromArgb(16, 24, 28);
        public static readonly Color DisabledBack = Color.FromArgb(38, 38, 38);
        public static readonly Color DisabledText = Color.FromArgb(110, 110, 110);
    }

    internal sealed class CalculatorForm : Form
    {
        private const int StandardWidth = 360;
        private const int WindowHeight = 600;
        private const int SciWidth = 292;

        private enum AngleMode
        {
            Deg,
            Rad
        }

        // ---- 计算状态 ----
        private decimal _accum;
        private decimal _current;
        private string _entry = "0";
        private bool _typing;
        private char _pendingOp = '\0';
        private char _lastOp = '\0';
        private decimal _lastOperand;
        private bool _justEvaluated;
        private bool _error;
        private string _expr = "";
        private decimal? _memory;
        private AngleMode _angle = AngleMode.Deg;

        // ---- 控件 ----
        private TableLayoutPanel _keys;
        private TableLayoutPanel _sciPanel;
        private Label _exprLabel;
        private Label _resultLabel;
        private CalcButton _modeButton;
        private Label _modeLabel;
        private CalcButton _angleButton;
        private CalcButton _mcButton;
        private CalcButton _mrButton;
        private CalcButton _mPlusButton;
        private CalcButton _mMinusButton;
        private Font _resultFont;

        public CalculatorForm()
        {
            BuildUi();
            ResetAll();
            UpdateDisplay();
        }

        // ================= UI =================

        private void BuildUi()
        {
            SuspendLayout();

            Text = "轻算计算器";
            BackColor = Palette.Background;
            ForeColor = Palette.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            ClientSize = new Size(StandardWidth, WindowHeight);
            MinimumSize = new Size(StandardWidth, 480);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            DoubleBuffered = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Padding = new Padding(6, 4, 6, 6);
            root.BackColor = Palette.Background;
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildMemoryRow(), 0, 1);
            root.Controls.Add(BuildDisplay(), 0, 2);

            _keys = new TableLayoutPanel();
            _keys.Dock = DockStyle.Fill;
            _keys.ColumnCount = 2;
            _keys.RowCount = 1;
            _keys.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0f));
            _keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _keys.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _keys.Margin = new Padding(0);
            _keys.BackColor = Palette.Background;

            _sciPanel = new TableLayoutPanel();
            _sciPanel.Dock = DockStyle.Fill;
            _sciPanel.Margin = new Padding(0, 0, 6, 0);
            _sciPanel.BackColor = Palette.Background;
            _sciPanel.Visible = false;
            BuildScientificKeys(_sciPanel);

            TableLayoutPanel standard = new TableLayoutPanel();
            standard.Dock = DockStyle.Fill;
            standard.Margin = new Padding(0);
            standard.BackColor = Palette.Background;
            BuildStandardKeys(standard);

            _keys.Controls.Add(_sciPanel, 0, 0);
            _keys.Controls.Add(standard, 1, 0);
            root.Controls.Add(_keys, 0, 3);

            ResumeLayout(true);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            header.BackColor = Palette.Background;

            _modeLabel = new Label();
            _modeLabel.Text = "标准";
            _modeLabel.AutoSize = false;
            _modeLabel.Dock = DockStyle.Left;
            _modeLabel.Width = 160;
            _modeLabel.TextAlign = ContentAlignment.MiddleLeft;
            _modeLabel.Padding = new Padding(6, 0, 0, 0);
            _modeLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
            _modeLabel.ForeColor = Color.FromArgb(230, 230, 230);
            header.Controls.Add(_modeLabel);

            _modeButton = new CalcButton();
            _modeButton.Text = "科学";
            _modeButton.Dock = DockStyle.Right;
            _modeButton.Width = 84;
            _modeButton.Margin = new Padding(0);
            _modeButton.Height = 28;
            _modeButton.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _modeButton.BackNormal = Palette.FuncBack;
            _modeButton.BackHover = Palette.FuncHover;
            _modeButton.BackPress = Palette.FuncPress;
            _modeButton.Radius = 6;
            _modeButton.Click += delegate { ToggleMode(); };
            header.Controls.Add(_modeButton);

            return header;
        }

        private Control BuildMemoryRow()
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.ColumnCount = 5;
            row.RowCount = 1;
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            row.Margin = new Padding(0);
            row.BackColor = Palette.Background;
            for (int i = 0; i < 5; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            }

            _mcButton = MakeKey("MC", BtnKind.Ghost, delegate { MemoryClear(); });
            _mrButton = MakeKey("MR", BtnKind.Ghost, delegate { MemoryRecall(); });
            _mPlusButton = MakeKey("M+", BtnKind.Ghost, delegate { MemoryAdd(1); });
            _mMinusButton = MakeKey("M-", BtnKind.Ghost, delegate { MemoryAdd(-1); });
            CalcButton ms = MakeKey("MS", BtnKind.Ghost, delegate { MemoryStore(); });

            row.Controls.Add(_mcButton, 0, 0);
            row.Controls.Add(_mrButton, 1, 0);
            row.Controls.Add(_mPlusButton, 2, 0);
            row.Controls.Add(_mMinusButton, 3, 0);
            row.Controls.Add(ms, 4, 0);
            return row;
        }

        private Control BuildDisplay()
        {
            TableLayoutPanel display = new TableLayoutPanel();
            display.Dock = DockStyle.Fill;
            display.ColumnCount = 1;
            display.RowCount = 2;
            display.Margin = new Padding(0);
            display.Padding = new Padding(4, 0, 4, 4);
            display.BackColor = Palette.Background;
            display.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            display.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            display.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _exprLabel = new Label();
            _exprLabel.Text = "";
            _exprLabel.Dock = DockStyle.Fill;
            _exprLabel.AutoSize = false;
            _exprLabel.TextAlign = ContentAlignment.MiddleRight;
            _exprLabel.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            _exprLabel.ForeColor = Palette.TextMuted;

            _resultLabel = new Label();
            _resultLabel.Text = "0";
            _resultLabel.Dock = DockStyle.Fill;
            _resultLabel.AutoSize = false;
            _resultLabel.TextAlign = ContentAlignment.MiddleRight;
            _resultLabel.Font = new Font("Segoe UI", 40f, FontStyle.Regular, GraphicsUnit.Point);
            _resultLabel.ForeColor = Palette.Text;

            display.Controls.Add(_exprLabel, 0, 0);
            display.Controls.Add(_resultLabel, 0, 1);
            return display;
        }

        private static void ConfigureGrid(TableLayoutPanel panel, int cols, int rows)
        {
            panel.ColumnCount = cols;
            panel.RowCount = rows;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);
            panel.BackColor = Palette.Background;
            for (int i = 0; i < cols; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            }
            for (int j = 0; j < rows; j++)
            {
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            }
        }

        private void BuildStandardKeys(TableLayoutPanel p)
        {
            ConfigureGrid(p, 4, 6);

            p.Controls.Add(MakeKey("%", BtnKind.Func, delegate { DoPercent(); }), 0, 0);
            p.Controls.Add(MakeKey("CE", BtnKind.Func, delegate { ClearEntry(); }), 1, 0);
            p.Controls.Add(MakeKey("C", BtnKind.Func, delegate { ResetAll(); UpdateDisplay(); }), 2, 0);
            p.Controls.Add(MakeKey("\u2190", BtnKind.Func, delegate { Backspace(); }), 3, 0);

            p.Controls.Add(MakeKey("1/x", BtnKind.Func, delegate { DoUnary("1/x", Reciprocal); }), 0, 1);
            p.Controls.Add(MakeKey("x\u00B2", BtnKind.Func, delegate { DoUnary("x\u00B2", Square); }), 1, 1);
            p.Controls.Add(MakeKey("\u221Ax", BtnKind.Func, delegate { DoUnary("\u221Ax", Sqrt); }), 2, 1);
            p.Controls.Add(MakeKey("\u00F7", BtnKind.Operator, delegate { SetOperator('/'); }), 3, 1);

            p.Controls.Add(MakeKey("7", BtnKind.Digit, delegate { DigitInput('7'); }), 0, 2);
            p.Controls.Add(MakeKey("8", BtnKind.Digit, delegate { DigitInput('8'); }), 1, 2);
            p.Controls.Add(MakeKey("9", BtnKind.Digit, delegate { DigitInput('9'); }), 2, 2);
            p.Controls.Add(MakeKey("\u00D7", BtnKind.Operator, delegate { SetOperator('*'); }), 3, 2);

            p.Controls.Add(MakeKey("4", BtnKind.Digit, delegate { DigitInput('4'); }), 0, 3);
            p.Controls.Add(MakeKey("5", BtnKind.Digit, delegate { DigitInput('5'); }), 1, 3);
            p.Controls.Add(MakeKey("6", BtnKind.Digit, delegate { DigitInput('6'); }), 2, 3);
            p.Controls.Add(MakeKey("\u2212", BtnKind.Operator, delegate { SetOperator('-'); }), 3, 3);

            p.Controls.Add(MakeKey("1", BtnKind.Digit, delegate { DigitInput('1'); }), 0, 4);
            p.Controls.Add(MakeKey("2", BtnKind.Digit, delegate { DigitInput('2'); }), 1, 4);
            p.Controls.Add(MakeKey("3", BtnKind.Digit, delegate { DigitInput('3'); }), 2, 4);
            p.Controls.Add(MakeKey("+", BtnKind.Operator, delegate { SetOperator('+'); }), 3, 4);

            p.Controls.Add(MakeKey("\u00B1", BtnKind.Digit, delegate { Negate(); }), 0, 5);
            p.Controls.Add(MakeKey("0", BtnKind.Digit, delegate { DigitInput('0'); }), 1, 5);
            p.Controls.Add(MakeKey(".", BtnKind.Digit, delegate { DigitInput('.'); }), 2, 5);
            p.Controls.Add(MakeKey("=", BtnKind.Accent, delegate { DoEquals(); }), 3, 5);
        }

        private void BuildScientificKeys(TableLayoutPanel p)
        {
            ConfigureGrid(p, 4, 6);

            _angleButton = MakeKey("DEG", BtnKind.Func, delegate { ToggleAngle(); });
            p.Controls.Add(_angleButton, 0, 0);
            p.Controls.Add(MakeKey("sin", BtnKind.Func, delegate { DoUnary("sin", Sin); }), 1, 0);
            p.Controls.Add(MakeKey("cos", BtnKind.Func, delegate { DoUnary("cos", Cos); }), 2, 0);
            p.Controls.Add(MakeKey("tan", BtnKind.Func, delegate { DoUnary("tan", Tan); }), 3, 0);

            p.Controls.Add(MakeKey("\u03C0", BtnKind.Func, delegate { InsertConstant(Math.PI); }), 0, 1);
            p.Controls.Add(MakeKey("log", BtnKind.Func, delegate { DoUnary("log", Log10); }), 1, 1);
            p.Controls.Add(MakeKey("ln", BtnKind.Func, delegate { DoUnary("ln", Ln); }), 2, 1);
            p.Controls.Add(MakeKey("e", BtnKind.Func, delegate { InsertConstant(Math.E); }), 3, 1);

            p.Controls.Add(MakeKey("x^y", BtnKind.Func, delegate { SetOperator('^'); }), 0, 2);
            p.Controls.Add(MakeKey("10^x", BtnKind.Func, delegate { DoUnary("10^x", Pow10); }), 1, 2);
            p.Controls.Add(MakeKey("e^x", BtnKind.Func, delegate { DoUnary("e^x", Exp); }), 2, 2);
            p.Controls.Add(MakeKey("mod", BtnKind.Func, delegate { SetOperator('m'); }), 3, 2);

            p.Controls.Add(MakeKey("x\u00B3", BtnKind.Func, delegate { DoUnary("x\u00B3", Cube); }), 0, 3);
            p.Controls.Add(MakeKey("\u00B3\u221Ax", BtnKind.Func, delegate { DoUnary("\u00B3\u221Ax", CubeRoot); }), 1, 3);
            p.Controls.Add(MakeKey("n!", BtnKind.Func, delegate { DoUnary("n!", Factorial); }), 2, 3);
            p.Controls.Add(MakeKey("1/x", BtnKind.Func, delegate { DoUnary("1/x", Reciprocal); }), 3, 3);

            p.Controls.Add(MakeKey("|x|", BtnKind.Func, delegate { DoUnary("|x|", Abs); }), 0, 4);
            p.Controls.Add(MakeKey("asin", BtnKind.Func, delegate { DoUnary("asin", Asin); }), 1, 4);
            p.Controls.Add(MakeKey("acos", BtnKind.Func, delegate { DoUnary("acos", Acos); }), 2, 4);
            p.Controls.Add(MakeKey("atan", BtnKind.Func, delegate { DoUnary("atan", Atan); }), 3, 4);

            CalcButton floor = MakeKey("floor", BtnKind.Func, delegate { DoUnary("floor", Floor); });
            CalcButton ceil = MakeKey("ceil", BtnKind.Func, delegate { DoUnary("ceil", Ceil); });
            p.Controls.Add(floor, 0, 5);
            p.SetColumnSpan(floor, 2);
            p.Controls.Add(ceil, 2, 5);
            p.SetColumnSpan(ceil, 2);
        }

        private static CalcButton MakeKey(string text, BtnKind kind, Action action)
        {
            CalcButton b = new CalcButton();
            b.Text = text;
            b.Dock = DockStyle.Fill;
            switch (kind)
            {
                case BtnKind.Digit:
                    b.BackNormal = Palette.DigitBack;
                    b.BackHover = Palette.DigitHover;
                    b.BackPress = Palette.DigitPress;
                    b.Font = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Point);
                    break;
                case BtnKind.Operator:
                    b.BackNormal = Palette.OpBack;
                    b.BackHover = Palette.OpHover;
                    b.BackPress = Palette.OpPress;
                    b.Font = new Font("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Point);
                    break;
                case BtnKind.Func:
                    b.BackNormal = Palette.FuncBack;
                    b.BackHover = Palette.FuncHover;
                    b.BackPress = Palette.FuncPress;
                    b.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
                    break;
                case BtnKind.Accent:
                    b.BackNormal = Palette.AccentBack;
                    b.BackHover = Palette.AccentHover;
                    b.BackPress = Palette.AccentPress;
                    b.TextColor = Palette.AccentText;
                    b.Font = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Point);
                    break;
                case BtnKind.Ghost:
                    b.BackNormal = Palette.Background;
                    b.BackHover = Palette.FuncHover;
                    b.BackPress = Palette.FuncPress;
                    b.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
                    b.TextColor = Color.FromArgb(215, 215, 215);
                    break;
            }
            b.Click += delegate { action(); };
            return b;
        }

        // ================= 模式与角度 =================

        private void ToggleMode()
        {
            bool scientific = !_sciPanel.Visible;
            _sciPanel.Visible = scientific;
            _keys.ColumnStyles[0].Width = scientific ? SciWidth : 0f;
            _modeButton.Text = scientific ? "标准" : "科学";
            _modeLabel.Text = scientific ? "科学型" : "标准型";
            ClientSize = new Size(scientific ? StandardWidth + SciWidth : StandardWidth, ClientSize.Height);
            UpdateDisplay();
        }

        private void ToggleAngle()
        {
            _angle = _angle == AngleMode.Deg ? AngleMode.Rad : AngleMode.Deg;
            _angleButton.Text = _angle == AngleMode.Deg ? "DEG" : "RAD";
        }

        // ================= 输入 =================

        private void DigitInput(char c)
        {
            if (_error)
            {
                ResetAll();
            }

            if (_justEvaluated)
            {
                _expr = "";
                _accum = 0m;
                _pendingOp = '\0';
                _current = 0m;
                _justEvaluated = false;
            }

            if (!_typing)
            {
                _entry = c == '.' ? "0." : c.ToString();
                _typing = true;
            }
            else
            {
                string bare = _entry.Replace("-", "").Replace(".", "");
                if (bare.Length >= 16 && c != '.')
                {
                    SystemSounds_Beep();
                    return;
                }
                if (c == '.')
                {
                    if (_entry.IndexOf('.') >= 0)
                    {
                        return;
                    }
                    _entry += ".";
                }
                else if (_entry == "0")
                {
                    _entry = c.ToString();
                }
                else if (_entry == "-0")
                {
                    _entry = "-" + c;
                }
                else
                {
                    _entry += c;
                }
            }
            UpdateDisplay();
        }

        private static void SystemSounds_Beep()
        {
            System.Media.SystemSounds.Beep.Play();
        }

        private void Backspace()
        {
            if (_error)
            {
                ResetAll();
                UpdateDisplay();
                return;
            }
            if (_typing)
            {
                if (_entry.Length <= 1)
                {
                    _entry = "0";
                }
                else
                {
                    _entry = _entry.Substring(0, _entry.Length - 1);
                    if (_entry == "-")
                    {
                        _entry = "0";
                    }
                }
                if (_entry.Length == 0)
                {
                    _entry = "0";
                }
            }
            else
            {
                _current = 0m;
                _entry = "0";
                _justEvaluated = false;
            }
            UpdateDisplay();
        }

        private void ClearEntry()
        {
            if (_error)
            {
                ResetAll();
                UpdateDisplay();
                return;
            }
            _entry = "0";
            _typing = false;
            _current = 0m;
            _justEvaluated = false;
            UpdateDisplay();
        }

        private void ResetAll()
        {
            _accum = 0m;
            _current = 0m;
            _entry = "0";
            _typing = false;
            _pendingOp = '\0';
            _lastOp = '\0';
            _lastOperand = 0m;
            _justEvaluated = false;
            _error = false;
            _expr = "";
        }

        private void Negate()
        {
            if (_error)
            {
                return;
            }
            decimal v = CurrentValue();
            v = -v;
            _current = v;
            _entry = Plain(v);
            _typing = false;
            UpdateDisplay();
        }

        // ================= 运算 =================

        private decimal CurrentValue()
        {
            if (!_typing)
            {
                return _current;
            }
            string s = _entry;
            if (s.EndsWith("."))
            {
                s = s.Substring(0, s.Length - 1);
            }
            decimal v;
            if (!decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                return 0m;
            }
            return v;
        }

        private void SetOperator(char op)
        {
            if (_error)
            {
                return;
            }
            decimal v = CurrentValue();
            if (_pendingOp != '\0')
            {
                decimal res;
                if (!Apply(_accum, v, _pendingOp, out res))
                {
                    return;
                }
                _accum = res;
            }
            else
            {
                _accum = v;
            }
            _current = _accum;
            _entry = Plain(_accum);
            _typing = false;
            _pendingOp = op;
            _justEvaluated = false;
            _expr = Format(_accum) + " " + OpSymbol(op);
            UpdateDisplay();
        }

        private void DoEquals()
        {
            if (_error)
            {
                return;
            }
            decimal v = CurrentValue();
            decimal left;
            decimal right;
            char op;
            if (_pendingOp != '\0')
            {
                left = _accum;
                right = v;
                op = _pendingOp;
                _lastOp = op;
                _lastOperand = right;
            }
            else if (_justEvaluated && _lastOp != '\0')
            {
                left = _current;
                right = _lastOperand;
                op = _lastOp;
            }
            else
            {
                _current = v;
                _entry = Plain(v);
                _typing = false;
                _expr = "";
                UpdateDisplay();
                return;
            }

            decimal res;
            if (!Apply(left, right, op, out res))
            {
                return;
            }
            _expr = Format(left) + " " + OpSymbol(op) + " " + Format(right) + " =";
            _pendingOp = '\0';
            _accum = res;
            _current = res;
            _entry = Plain(res);
            _typing = false;
            _justEvaluated = true;
            UpdateDisplay();
        }

        private void DoPercent()
        {
            if (_error)
            {
                return;
            }
            decimal v = CurrentValue();
            try
            {
                if (_pendingOp == '+' || _pendingOp == '-')
                {
                    v = _accum * v / 100m;
                }
                else
                {
                    v = v / 100m;
                }
            }
            catch (OverflowException)
            {
                ShowError("数值超出范围");
                return;
            }
            _current = v;
            _entry = Plain(v);
            _typing = false;
            UpdateDisplay();
        }

        private bool Apply(decimal a, decimal b, char op, out decimal result)
        {
            result = 0m;
            try
            {
                switch (op)
                {
                    case '+':
                        result = a + b;
                        break;
                    case '-':
                        result = a - b;
                        break;
                    case '*':
                        result = a * b;
                        break;
                    case '/':
                        if (b == 0m)
                        {
                            ShowError("除数不能为零");
                            return false;
                        }
                        result = a / b;
                        break;
                    case 'm':
                        if (b == 0m)
                        {
                            ShowError("除数不能为零");
                            return false;
                        }
                        result = a % b;
                        break;
                    case '^':
                        double p = Math.Pow((double)a, (double)b);
                        if (double.IsNaN(p) || double.IsInfinity(p))
                        {
                            ShowError("结果无效");
                            return false;
                        }
                        if (p > (double)decimal.MaxValue || p < (double)decimal.MinValue)
                        {
                            ShowError("数值超出范围");
                            return false;
                        }
                        result = (decimal)p;
                        break;
                    default:
                        result = b;
                        break;
                }
            }
            catch (DivideByZeroException)
            {
                ShowError("除数不能为零");
                return false;
            }
            catch (OverflowException)
            {
                ShowError("数值超出范围");
                return false;
            }

            if (decimal.Round(result, 12) != result && Math.Abs(result) < 0.000000000001m)
            {
                result = 0m;
            }
            return true;
        }

        // ---- 一元运算 ----

        private delegate bool UnaryFn(decimal x, out decimal result);

        private void DoUnary(string name, UnaryFn fn)
        {
            if (_error)
            {
                return;
            }
            decimal v = CurrentValue();
            decimal res;
            if (!fn(v, out res))
            {
                return;
            }
            _current = res;
            _entry = Plain(res);
            _typing = false;
            _justEvaluated = false;
            _expr = name + "(" + Format(v) + ")";
            UpdateDisplay();
        }

        private static bool FromDouble(double d, string errorText, out decimal result, out string error)
        {
            result = 0m;
            error = null;
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                error = errorText;
                return false;
            }
            if (d > (double)decimal.MaxValue || d < (double)decimal.MinValue)
            {
                error = "数值超出范围";
                return false;
            }
            double rounded = Math.Round(d, 12);
            if (Math.Abs(rounded) < 1e-12)
            {
                rounded = 0d;
            }
            result = (decimal)rounded;
            return true;
        }

        private double ToRadians(decimal x)
        {
            double d = (double)x;
            return _angle == AngleMode.Deg ? d * Math.PI / 180d : d;
        }

        private decimal FromRadians(double r)
        {
            return (decimal)(_angle == AngleMode.Deg ? r * 180d / Math.PI : r);
        }

        private bool Sin(decimal x, out decimal result)
        {
            string err;
            return FromDouble(Math.Sin(ToRadians(x)), "结果无效", out result, out err);
        }

        private bool Cos(decimal x, out decimal result)
        {
            string err;
            return FromDouble(Math.Cos(ToRadians(x)), "结果无效", out result, out err);
        }

        private bool Tan(decimal x, out decimal result)
        {
            string err;
            return FromDouble(Math.Tan(ToRadians(x)), "结果无效", out result, out err);
        }

        private bool Asin(decimal x, out decimal result)
        {
            double d = (double)x;
            if (d < -1d || d > 1d)
            {
                ShowError("输入超出范围");
                result = 0m;
                return false;
            }
            string err;
            return FromDouble(FromRadiansDouble(Math.Asin(d)), "结果无效", out result, out err);
        }

        private bool Acos(decimal x, out decimal result)
        {
            double d = (double)x;
            if (d < -1d || d > 1d)
            {
                ShowError("输入超出范围");
                result = 0m;
                return false;
            }
            string err;
            return FromDouble(FromRadiansDouble(Math.Acos(d)), "结果无效", out result, out err);
        }

        private bool Atan(decimal x, out decimal result)
        {
            string err;
            return FromDouble(FromRadiansDouble(Math.Atan((double)x)), "结果无效", out result, out err);
        }

        private double FromRadiansDouble(double r)
        {
            return _angle == AngleMode.Deg ? r * 180d / Math.PI : r;
        }

        private bool Log10(decimal x, out decimal result)
        {
            if (x <= 0m)
            {
                ShowError("输入超出范围");
                result = 0m;
                return false;
            }
            string err;
            return FromDouble(Math.Log10((double)x), "结果无效", out result, out err);
        }

        private bool Ln(decimal x, out decimal result)
        {
            if (x <= 0m)
            {
                ShowError("输入超出范围");
                result = 0m;
                return false;
            }
            string err;
            return FromDouble(Math.Log((double)x), "结果无效", out result, out err);
        }

        private bool Pow10(decimal x, out decimal result)
        {
            string err;
            return FromDouble(Math.Pow(10d, (double)x), "结果无效", out result, out err);
        }

        private bool Exp(decimal x, out decimal result)
        {
            string err;
            return FromDouble(Math.Exp((double)x), "结果无效", out result, out err);
        }

        private bool Reciprocal(decimal x, out decimal result)
        {
            if (x == 0m)
            {
                ShowError("除数不能为零");
                result = 0m;
                return false;
            }
            result = 1m / x;
            return true;
        }

        private bool Square(decimal x, out decimal result)
        {
            try
            {
                result = x * x;
                return true;
            }
            catch (OverflowException)
            {
                ShowError("数值超出范围");
                result = 0m;
                return false;
            }
        }

        private bool Cube(decimal x, out decimal result)
        {
            try
            {
                result = x * x * x;
                return true;
            }
            catch (OverflowException)
            {
                ShowError("数值超出范围");
                result = 0m;
                return false;
            }
        }

        private bool Sqrt(decimal x, out decimal result)
        {
            if (x < 0m)
            {
                ShowError("输入无效");
                result = 0m;
                return false;
            }
            string err;
            return FromDouble(Math.Sqrt((double)x), "结果无效", out result, out err);
        }

        private bool CubeRoot(decimal x, out decimal result)
        {
            double d = (double)x;
            double r = d < 0d ? -Math.Pow(-d, 1d / 3d) : Math.Pow(d, 1d / 3d);
            string err;
            return FromDouble(r, "结果无效", out result, out err);
        }

        private bool Abs(decimal x, out decimal result)
        {
            result = Math.Abs(x);
            return true;
        }

        private bool Floor(decimal x, out decimal result)
        {
            result = Math.Floor(x);
            return true;
        }

        private bool Ceil(decimal x, out decimal result)
        {
            result = Math.Ceiling(x);
            return true;
        }

        private bool Factorial(decimal x, out decimal result)
        {
            result = 0m;
            if (x < 0m || decimal.Floor(x) != x)
            {
                ShowError("需要非负整数");
                return false;
            }
            if (x > 170m)
            {
                ShowError("数值超出范围");
                return false;
            }
            double acc = 1d;
            for (int i = 2; i <= (int)x; i++)
            {
                acc *= i;
            }
            string err;
            return FromDouble(acc, "数值超出范围", out result, out err);
        }

        private void InsertConstant(double value)
        {
            if (_error)
            {
                ResetAll();
            }
            decimal d = (decimal)value;
            _current = d;
            _entry = Plain(d);
            _typing = false;
            _justEvaluated = false;
            UpdateDisplay();
        }

        private void ShowError(string message)
        {
            _error = true;
            _exprLabel.Text = message;
            _resultLabel.Text = message;
            _entry = "0";
            _typing = false;
            _current = 0m;
            _accum = 0m;
            _pendingOp = '\0';
            _lastOp = '\0';
            FitResultFont();
        }

        // ================= 记忆 =================

        private void MemoryStore()
        {
            _memory = CurrentValue();
            UpdateMemoryButtons();
        }

        private void MemoryRecall()
        {
            if (!_memory.HasValue)
            {
                return;
            }
            if (_error)
            {
                ResetAll();
            }
            _current = _memory.Value;
            _entry = Plain(_current);
            _typing = false;
            _justEvaluated = false;
            UpdateDisplay();
        }

        private void MemoryClear()
        {
            _memory = null;
            UpdateMemoryButtons();
        }

        private void MemoryAdd(int sign)
        {
            decimal v = CurrentValue();
            try
            {
                decimal baseValue = _memory.HasValue ? _memory.Value : 0m;
                _memory = sign > 0 ? baseValue + v : baseValue - v;
            }
            catch (OverflowException)
            {
                ShowError("数值超出范围");
            }
            UpdateMemoryButtons();
        }

        private void UpdateMemoryButtons()
        {
            bool has = _memory.HasValue;
            _mcButton.Enabled = has;
            _mrButton.Enabled = has;
            _mPlusButton.Enabled = true;
            _mMinusButton.Enabled = true;
        }

        // ================= 显示 =================

        private void UpdateDisplay()
        {
            if (_error)
            {
                FitResultFont();
                return;
            }
            _resultLabel.Text = _typing ? FormatEntry(_entry) : Format(_current);
            _exprLabel.Text = _expr;
            FitResultFont();
        }

        private void FitResultFont()
        {
            if (_resultLabel.Width <= 0)
            {
                return;
            }
            float[] sizes = { 40f, 36f, 32f, 28f, 26f, 24f, 22f, 20f, 18f, 16f, 14f, 12f };
            int available = Math.Max(40, _resultLabel.Width - 12);
            float chosen = sizes[sizes.Length - 1];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Font probe = new Font("Segoe UI", sizes[i], FontStyle.Regular, GraphicsUnit.Point))
                {
                    Size sz = TextRenderer.MeasureText(_resultLabel.Text, probe, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding);
                    if (sz.Width <= available)
                    {
                        chosen = sizes[i];
                        break;
                    }
                }
            }
            if (_resultFont == null || _resultFont.Size != chosen)
            {
                if (_resultFont != null)
                {
                    _resultFont.Dispose();
                }
                _resultFont = new Font("Segoe UI", chosen, FontStyle.Regular, GraphicsUnit.Point);
                _resultLabel.Font = _resultFont;
            }
        }

        private static string Format(decimal d)
        {
            if (d == 0m)
            {
                return "0";
            }
            string s = d.ToString("#,0.############", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        private static string Plain(decimal d)
        {
            string s = d.ToString("0.############", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        private static string FormatEntry(string entry)
        {
            string sign = "";
            string body = entry;
            if (body.StartsWith("-"))
            {
                sign = "-";
                body = body.Substring(1);
            }
            int dot = body.IndexOf('.');
            string intPart = dot >= 0 ? body.Substring(0, dot) : body;
            string fracPart = dot >= 0 ? body.Substring(dot) : "";

            string grouped = "";
            int count = 0;
            for (int i = intPart.Length - 1; i >= 0; i--)
            {
                grouped = intPart[i] + grouped;
                count++;
                if (count % 3 == 0 && i > 0)
                {
                    grouped = "," + grouped;
                }
            }
            return sign + grouped + fracPart;
        }

        private static string OpSymbol(char op)
        {
            switch (op)
            {
                case '*':
                    return "\u00D7";
                case '/':
                    return "\u00F7";
                case '-':
                    return "\u2212";
                case '^':
                    return "^";
                case 'm':
                    return "mod";
                default:
                    return "+";
            }
        }

        // ================= 键盘 =================

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.D1))
            {
                if (_sciPanel.Visible)
                {
                    ToggleMode();
                }
                return true;
            }
            if (keyData == (Keys.Alt | Keys.D2))
            {
                if (!_sciPanel.Visible)
                {
                    ToggleMode();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Keys key = e.KeyCode;
            bool shift = e.Shift;

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int digit = (int)key - (int)Keys.D0;
                if (shift && digit == 5)
                {
                    DoPercent();
                }
                else if (shift && digit == 8)
                {
                    SetOperator('*');
                }
                else if (shift && digit == 0)
                {
                    DoEquals();
                }
                else if (shift && digit == 1)
                {
                    ShowHelp();
                }
                else if (!shift)
                {
                    DigitInput((char)('0' + digit));
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                DigitInput((char)('0' + ((int)key - (int)Keys.NumPad0)));
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            switch (key)
            {
                case Keys.OemPeriod:
                case Keys.Decimal:
                    DigitInput('.');
                    break;
                case Keys.Add:
                case Keys.Oemplus:
                    SetOperator('+');
                    break;
                case Keys.Subtract:
                case Keys.OemMinus:
                    SetOperator('-');
                    break;
                case Keys.Multiply:
                    SetOperator('*');
                    break;
                case Keys.Divide:
                case Keys.OemQuestion:
                    SetOperator('/');
                    break;
                case Keys.Enter:
                    DoEquals();
                    break;
                case Keys.Back:
                    Backspace();
                    break;
                case Keys.Escape:
                    ResetAll();
                    UpdateDisplay();
                    break;
                case Keys.Delete:
                    ClearEntry();
                    break;
                case Keys.F9:
                    Negate();
                    break;
                case Keys.R:
                    DoUnary("1/x", Reciprocal);
                    break;
                case Keys.Q:
                    DoUnary("x\u00B2", Square);
                    break;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void ShowHelp()
        {
            MessageBox.Show(this,
                "键盘快捷键：\n\n" +
                "0-9、.        输入数字\n" +
                "+ - * /       四则运算\n" +
                "Enter / =     计算结果\n" +
                "Shift+5       百分比\n" +
                "Backspace     退格\n" +
                "Delete        清除当前项 (CE)\n" +
                "Esc           全部清除 (C)\n" +
                "F9            正负号切换\n" +
                "R             倒数 (1/x)\n" +
                "Q             平方\n" +
                "Alt+1 / Alt+2 标准型 / 科学型",
                "轻算计算器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
