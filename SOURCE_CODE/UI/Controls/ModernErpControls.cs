using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.UI.Controls
{
    public enum ModernERPButtonVariant
    {
        Primary,
        Secondary,
        Success,
        Warning,
        Danger,
        Ghost
    }

    public static class ModernERPTheme
    {
        public static readonly Color Workspace = DS.BgPage;
        public static readonly Color Surface = DS.BgCard;
        public static readonly Color SurfaceAlt = DS.Slate50;
        public static readonly Color Border = DS.Border;
        public static readonly Color BorderStrong = DS.BorderStrong;
        public static readonly Color Text = DS.Slate900;
        public static readonly Color MutedText = DS.Slate500;
        public static readonly Color RoyalBlue = DS.Primary600;
        public static readonly Color Primary = DS.Primary600;
        public static readonly Color PrimaryHover = DS.Primary700;
        public static readonly Color Blue = DS.Primary500;
        public static readonly Color Success = DS.Green600;
        public static readonly Color Warning = DS.Amber500;
        public static readonly Color Danger = DS.Red600;
        public static readonly Color Info = DS.Teal500;
        public static readonly Color Purple = DS.Primary600;
        public static readonly Color Shadow = DS.Shadow;

        public static readonly Font Body = new Font("Segoe UI", 9f);
        public static readonly Font BodyBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 8f);
        public static readonly Font SmallBold = new Font("Segoe UI", 8f, FontStyle.Bold);
        public static readonly Font Title = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font Heading = new Font("Segoe UI", 11f, FontStyle.Bold);

        public const int RadiusSm = 6;
        public const int Radius = 8;
        public const int RadiusLg = 12;
        public const int Gap = 12;
        public const int GapLg = 18;

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Lighten(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                color.A,
                color.R + (int)((255 - color.R) * amount),
                color.G + (int)((255 - color.G) * amount),
                color.B + (int)((255 - color.B) * amount));
        }

        public static Color Darken(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                color.A,
                (int)(color.R * (1f - amount)),
                (int)(color.G * (1f - amount)),
                (int)(color.B * (1f - amount)));
        }

        public static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return;

            Region old = control.Region;
            using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, control.Width, control.Height), radius))
                control.Region = new Region(path);
            if (old != null)
                old.Dispose();
        }
    }

    public class ModernERPButton : Button
    {
        private ModernERPButtonVariant _variant = ModernERPButtonVariant.Primary;
        private int _radius = ModernERPTheme.Radius;

        public ModernERPButton()
        {
            Height = 36;
            MinimumSize = new Size(110, 36);
            Padding = new Padding(14, 0, 14, 0);
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = ModernERPTheme.BodyBold;
            TextAlign = ContentAlignment.MiddleCenter;
            ApplyVariant();
        }

        [DefaultValue(ModernERPButtonVariant.Primary)]
        public ModernERPButtonVariant Variant
        {
            get { return _variant; }
            set
            {
                _variant = value;
                ApplyVariant();
            }
        }

        [DefaultValue(8)]
        public int Radius
        {
            get { return _radius; }
            set
            {
                _radius = Math.Max(0, value);
                UpdateRegion();
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            ModernERPTheme.ApplyRoundedRegion(this, Radius);
        }

        private void ApplyVariant()
        {
            Color bg;
            Color fg = Color.White;
            Color border = Color.Transparent;

            switch (_variant)
            {
                case ModernERPButtonVariant.Secondary:
                    bg = DS.BgCard;
                    fg = DS.Slate800;
                    border = DS.InputBorder;
                    break;
                case ModernERPButtonVariant.Success:
                    bg = DS.Primary600;
                    break;
                case ModernERPButtonVariant.Warning:
                    bg = DS.BgCard;
                    fg = DS.Slate800;
                    border = DS.InputBorder;
                    break;
                case ModernERPButtonVariant.Danger:
                    bg = DS.Red600;
                    break;
                case ModernERPButtonVariant.Ghost:
                    bg = DS.Slate50;
                    fg = Color.FromArgb(55, 65, 81);
                    border = DS.InputBorder;
                    break;
                default:
                    bg = DS.Primary600;
                    break;
            }

            BackColor = bg;
            ForeColor = fg;
            FlatAppearance.BorderSize = border == Color.Transparent ? 0 : 1;
            FlatAppearance.BorderColor = border == Color.Transparent ? bg : border;
            bool outlined = _variant == ModernERPButtonVariant.Ghost ||
                            _variant == ModernERPButtonVariant.Secondary ||
                            _variant == ModernERPButtonVariant.Warning;
            FlatAppearance.MouseOverBackColor = outlined ? DS.Slate50 : DS.Darken(bg, 0.08f);
            FlatAppearance.MouseDownBackColor = outlined ? DS.Slate100 : DS.Darken(bg, 0.16f);
        }
    }

    public class ModernERPTextBox : TextBox
    {
        public ModernERPTextBox()
        {
            BorderStyle = BorderStyle.None;
            Font = ModernERPTheme.Body;
            Height = 32;
            MinimumSize = new Size(0, 32);
            BackColor = Color.White;
            ForeColor = ModernERPTheme.Text;
            Margin = new Padding(0, 3, 0, 8);
            Enter += (s, e) => InvalidateParentFrame();
            Leave += (s, e) => InvalidateParentFrame();
        }

        protected override void OnReadOnlyChanged(EventArgs e)
        {
            base.OnReadOnlyChanged(e);
            BackColor = ReadOnly ? DS.InputDisabledBack : Color.White;
            ForeColor = ReadOnly ? DS.InputMutedText : DS.InputText;
            InvalidateParentFrame();
        }

        private void InvalidateParentFrame()
        {
            if (Parent != null)
                Parent.Invalidate(new Rectangle(Left - 3, Top - 3, Width + 6, Height + 6));
        }
    }

    public class ModernERPComboBox : ComboBox
    {
        public ModernERPComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            Font = ModernERPTheme.Body;
            Height = 32;
            MinimumSize = new Size(0, 32);
            BackColor = Color.White;
            ForeColor = ModernERPTheme.Text;
            AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            AutoCompleteSource = AutoCompleteSource.ListItems;
            Margin = new Padding(0, 3, 0, 8);
        }
    }

    public class ModernERPDatePicker : DateTimePicker
    {
        public ModernERPDatePicker()
        {
            Font = ModernERPTheme.Body;
            Height = 32;
            MinimumSize = new Size(0, 32);
            Format = DateTimePickerFormat.Short;
            CalendarMonthBackground = Color.White;
            CalendarForeColor = ModernERPTheme.Text;
            Margin = new Padding(0, 3, 0, 8);
        }
    }

    public class ModernERPDataGrid : DataGridView
    {
        public ModernERPDataGrid()
        {
            Dock = DockStyle.Fill;
            BackgroundColor = DS.BgCard;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            GridColor = ModernERPTheme.Border;
            RowHeadersVisible = false;
            AllowUserToResizeRows = false;
            MultiSelect = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            ScrollBars = ScrollBars.Both;
            EnableHeadersVisualStyles = false;
            RowTemplate.Height = 34;
            ColumnHeadersHeight = 38;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            Font = ModernERPTheme.Body;
            ApplyModernGridStyle();
            DataBindingComplete += (s, e) => ApplyModernGridStyle();
            ColumnAdded += (s, e) => ApplyModernGridStyle();
        }

        public void ApplyModernGridStyle()
        {
            ColumnHeadersDefaultCellStyle.BackColor = DS.Slate50;
            ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate900;
            ColumnHeadersDefaultCellStyle.Font = ModernERPTheme.BodyBold;
            ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            ColumnHeadersDefaultCellStyle.SelectionBackColor = DS.Slate100;
            ColumnHeadersDefaultCellStyle.SelectionForeColor = DS.Slate900;

            DefaultCellStyle.Font = ModernERPTheme.Body;
            DefaultCellStyle.ForeColor = ModernERPTheme.Text;
            DefaultCellStyle.BackColor = GridTheme.RowNormal;
            DefaultCellStyle.SelectionBackColor = GridTheme.RowSelected;
            DefaultCellStyle.SelectionForeColor = Color.White;
            DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            AlternatingRowsDefaultCellStyle.BackColor = GridTheme.RowAlt;
            AlternatingRowsDefaultCellStyle.SelectionBackColor = GridTheme.RowSelected;
            AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;

            foreach (DataGridViewColumn column in Columns)
            {
                if (column.MinimumWidth < 70)
                    column.MinimumWidth = 70;
            }
        }
    }

    public class ModernERPPanel : Panel
    {
        public ModernERPPanel()
        {
            DoubleBuffered = true;
            BackColor = ModernERPTheme.Workspace;
            Padding = new Padding(ModernERPTheme.GapLg);
        }
    }

    public class ModernERPCard : Panel
    {
        private int _radius = ModernERPTheme.RadiusLg;

        public ModernERPCard()
        {
            DoubleBuffered = true;
            BackColor = ModernERPTheme.Surface;
            Padding = new Padding(18);
            Margin = new Padding(0, 0, 0, ModernERPTheme.Gap);
        }

        [DefaultValue(12)]
        public int Radius
        {
            get { return _radius; }
            set
            {
                _radius = Math.Max(0, value);
                Invalidate();
            }
        }

        public Color BorderColor { get; set; } = ModernERPTheme.Border;
        public Color ShadowColor { get; set; } = ModernERPTheme.Shadow;
        public bool ShowShadow { get; set; } = true;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 2, Height - 2);
            if (ShowShadow && Width > 6 && Height > 6)
            {
                Rectangle shadowRect = new Rectangle(2, 3, Width - 5, Height - 6);
                using (GraphicsPath shadow = ModernERPTheme.RoundedRect(shadowRect, Radius))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(42, ShadowColor)))
                    e.Graphics.FillPath(brush, shadow);
            }

            using (GraphicsPath path = ModernERPTheme.RoundedRect(rect, Radius))
            using (SolidBrush fill = new SolidBrush(BackColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    public class ModernERPTableLayout : TableLayoutPanel
    {
        public ModernERPTableLayout()
        {
            BackColor = Color.Transparent;
            Padding = new Padding(0);
            Margin = new Padding(0);
            GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            AutoSize = false;
            AutoScroll = false;
        }

        public void UseEqualColumns(int columnCount)
        {
            ColumnStyles.Clear();
            ColumnCount = Math.Max(1, columnCount);
            float width = 100f / ColumnCount;
            for (int i = 0; i < ColumnCount; i++)
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        }
    }

    public class ModernERPFlowLayout : FlowLayoutPanel
    {
        public ModernERPFlowLayout()
        {
            BackColor = Color.Transparent;
            AutoScroll = false;
            WrapContents = true;
            FlowDirection = FlowDirection.LeftToRight;
            Padding = new Padding(0);
            Margin = new Padding(0);
        }
    }

    public class ModernERPSidebar : Panel
    {
        public ModernERPSidebar()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Left;
            Width = 208;
            Padding = new Padding(14, 18, 14, 18);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, Color.White, Color.FromArgb(248, 250, 252), 90f))
                e.Graphics.FillRectangle(brush, bounds);

            using (Pen pen = new Pen(ModernERPTheme.Border))
                e.Graphics.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
        }
    }

    public class ModernERPTopBar : Panel
    {
        public Label TitleLabel { get; private set; }
        public ModernERPFlowLayout Actions { get; private set; }

        public ModernERPTopBar()
        {
            Dock = DockStyle.Top;
            Height = 64;
            BackColor = ModernERPTheme.Surface;
            Padding = new Padding(20, 12, 20, 12);

            TitleLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = ModernERPTheme.Title,
                ForeColor = ModernERPTheme.Text,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Actions = new ModernERPFlowLayout
            {
                Dock = DockStyle.Right,
                Width = 360,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft
            };

            Controls.Add(TitleLabel);
            Controls.Add(Actions);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(ModernERPTheme.Border))
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    public class ModernERPTabs : TabControl
    {
        public ModernERPTabs()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(132, 34);
            Font = ModernERPTheme.BodyBold;
            Padding = new Point(12, 4);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected = e.Index == SelectedIndex;
            Rectangle rect = e.Bounds;
            rect.Inflate(-3, -3);
            Color bg = selected ? DS.Primary50 : ModernERPTheme.Surface;
            Color fg = selected ? DS.Primary700 : ModernERPTheme.MutedText;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = ModernERPTheme.RoundedRect(rect, ModernERPTheme.Radius))
            using (SolidBrush brush = new SolidBrush(bg))
            using (Pen pen = new Pen(selected ? DS.Primary100 : ModernERPTheme.Border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            string text = TabPages[e.Index].Text;
            TextRenderer.DrawText(e.Graphics, text, Font, rect, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    public class ModernERPStatusChip : Label
    {
        public ModernERPStatusChip()
        {
            AutoSize = false;
            Height = 24;
            Width = 92;
            TextAlign = ContentAlignment.MiddleCenter;
            Font = ModernERPTheme.SmallBold;
            BackColor = Color.FromArgb(220, 252, 231);
            ForeColor = ModernERPTheme.Success;
            Padding = new Padding(8, 2, 8, 2);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ModernERPTheme.ApplyRoundedRegion(this, Height);
        }

        public void SetTone(Color background, Color foreground)
        {
            BackColor = background;
            ForeColor = foreground;
            Invalidate();
        }
    }

    public class ModernERPKpiCard : ModernERPCard
    {
        private readonly Label _caption;
        private readonly Label _value;
        private readonly Label _delta;
        private readonly Panel _accent;

        public ModernERPKpiCard()
        {
            Height = 104;
            Padding = new Padding(18, 14, 18, 14);
            _accent = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = ModernERPTheme.Primary };
            _caption = new Label { Dock = DockStyle.Top, Height = 22, Font = ModernERPTheme.SmallBold, ForeColor = ModernERPTheme.MutedText };
            _value = new Label { Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = ModernERPTheme.Text };
            _delta = new Label { Dock = DockStyle.Fill, Font = ModernERPTheme.Small, ForeColor = ModernERPTheme.MutedText };
            Controls.Add(_delta);
            Controls.Add(_value);
            Controls.Add(_caption);
            Controls.Add(_accent);
        }

        public string Caption { get { return _caption.Text; } set { _caption.Text = value; } }
        public string Value { get { return _value.Text; } set { _value.Text = value; } }
        public string Delta { get { return _delta.Text; } set { _delta.Text = value; } }

        public Color AccentColor
        {
            get { return _accent.BackColor; }
            set { _accent.BackColor = value; }
        }
    }

    public class ModernERPSummaryPanel : ModernERPCard
    {
        public ModernERPSummaryPanel()
        {
            Dock = DockStyle.Top;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }
    }

    public class ModernERPActionPanel : ModernERPFlowLayout
    {
        public ModernERPActionPanel()
        {
            Dock = DockStyle.Top;
            Height = 48;
            WrapContents = true;
            Padding = new Padding(0, 0, 0, 10);
        }
    }

    public class ModernERPEmptyState : ModernERPCard
    {
        private readonly Panel _iconHost;
        private readonly Label _title;
        private readonly Label _message;

        public ModernERPEmptyState()
        {
            Height = 190;
            Padding = new Padding(26);
            _iconHost = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.Transparent };
            _title = new Label { Dock = DockStyle.Top, Height = 30, Font = ModernERPTheme.Heading, ForeColor = ModernERPTheme.Text, TextAlign = ContentAlignment.MiddleCenter };
            _message = new Label { Dock = DockStyle.Fill, Font = ModernERPTheme.Body, ForeColor = ModernERPTheme.MutedText, TextAlign = ContentAlignment.TopCenter };
            Controls.Add(_message);
            Controls.Add(_title);
            Controls.Add(_iconHost);
            Title = "No records yet";
            Message = "Create a record or adjust filters to see results here.";
            IconKind = ModernIconKind.EmptyBox;
        }

        public string Title { get { return _title.Text; } set { _title.Text = value; } }
        public string Message { get { return _message.Text; } set { _message.Text = value; } }
        public ModernIconKind IconKind
        {
            set
            {
                _iconHost.Controls.Clear();
                Panel icon = ModernIconSystem.EmptyStateIcon(value, 48, Color.FromArgb(238, 242, 255), ModernERPTheme.Primary);
                icon.Location = new Point((_iconHost.Width - icon.Width) / 2, 4);
                icon.Anchor = AnchorStyles.Top;
                _iconHost.Controls.Add(icon);
                _iconHost.Resize += (s, e) => icon.Left = (_iconHost.Width - icon.Width) / 2;
            }
        }
    }

    public class ModernERPToast : Label
    {
        public ModernERPToast()
        {
            AutoSize = false;
            Height = 38;
            Width = 320;
            BackColor = DS.BgCard;
            ForeColor = ModernERPTheme.Text;
            Font = ModernERPTheme.BodyBold;
            TextAlign = ContentAlignment.MiddleCenter;
            Padding = new Padding(14, 0, 14, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ModernERPTheme.ApplyRoundedRegion(this, ModernERPTheme.Radius);
        }

        public static ModernERPToast Show(Control owner, string message, Color accent)
        {
            if (owner == null || owner.IsDisposed)
                return null;

            Form form = owner.FindForm();
            if (form == null || form.IsDisposed)
                return null;

            ModernERPToast toast = new ModernERPToast
            {
                Text = message ?? string.Empty,
                BackColor = DS.Lighten(accent, 0.86f),
                ForeColor = accent.GetBrightness() < 0.5f ? DS.Darken(accent, 0.10f) : DS.Slate900,
                Width = Math.Min(460, Math.Max(240, (message ?? string.Empty).Length * 7 + 42))
            };
            toast.Left = Math.Max(12, form.ClientSize.Width - toast.Width - 24);
            toast.Top = 24;
            toast.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            form.Controls.Add(toast);
            toast.BringToFront();

            Timer timer = new Timer { Interval = 2800 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!toast.IsDisposed)
                    toast.Dispose();
            };
            timer.Start();
            return toast;
        }
    }

    public class ModernERPWorkflowStepper : ModernERPFlowLayout
    {
        private readonly List<Label> _steps = new List<Label>();
        private int _activeIndex;

        public ModernERPWorkflowStepper()
        {
            Height = 40;
            WrapContents = false;
        }

        public int ActiveIndex
        {
            get { return _activeIndex; }
            set
            {
                _activeIndex = Math.Max(0, value);
                RefreshSteps();
            }
        }

        public void SetSteps(IEnumerable<string> steps)
        {
            Controls.Clear();
            _steps.Clear();
            int index = 0;
            foreach (string step in steps ?? Enumerable.Empty<string>())
            {
                Label label = new Label
                {
                    Text = (index + 1).ToString() + ". " + step,
                    AutoSize = false,
                    Width = 132,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = ModernERPTheme.SmallBold,
                    Margin = new Padding(0, 0, 8, 0)
                };
                _steps.Add(label);
                Controls.Add(label);
                index++;
            }
            RefreshSteps();
        }

        private void RefreshSteps()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                bool done = i < _activeIndex;
                bool active = i == _activeIndex;
                _steps[i].BackColor = active ? DS.Primary50 : done ? DS.Green50 : ModernERPTheme.SurfaceAlt;
                _steps[i].ForeColor = active ? DS.Primary700 : done ? ModernERPTheme.Success : ModernERPTheme.MutedText;
                ModernERPTheme.ApplyRoundedRegion(_steps[i], ModernERPTheme.Radius);
            }
        }
    }

    public class ModernERPFilterBar : ModernERPFlowLayout
    {
        public ModernERPFilterBar()
        {
            Dock = DockStyle.Top;
            Height = 52;
            WrapContents = true;
            Padding = new Padding(0, 4, 0, 10);
        }
    }

    public class ModernERPSearchBox : Panel
    {
        private readonly TextBox _textBox;

        public ModernERPSearchBox()
        {
            Height = 34;
            Width = 280;
            BackColor = Color.White;
            Padding = new Padding(11, 7, 11, 5);
            Margin = new Padding(0, 0, 10, 8);
            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = ModernERPTheme.Body,
                BackColor = Color.White,
                ForeColor = ModernERPTheme.Text
            };
            _textBox.Enter += (s, e) => Invalidate();
            _textBox.Leave += (s, e) => Invalidate();
            Controls.Add(_textBox);
        }

        public TextBox InnerTextBox { get { return _textBox; } }
        public override string Text { get { return _textBox.Text; } set { _textBox.Text = value; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = ModernERPTheme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 7))
            using (Pen pen = new Pen(_textBox.Focused ? DS.FocusBlue : DS.InputBorder, _textBox.Focused ? 2f : 1f))
                e.Graphics.DrawPath(pen, path);
        }
    }

    public class ModernERPSectionHeader : Panel
    {
        public Label TitleLabel { get; private set; }
        public Label SubtitleLabel { get; private set; }

        public ModernERPSectionHeader()
        {
            Height = 54;
            Dock = DockStyle.Top;
            BackColor = Color.Transparent;
            TitleLabel = new Label { Dock = DockStyle.Top, Height = 26, Font = ModernERPTheme.Heading, ForeColor = ModernERPTheme.Text };
            SubtitleLabel = new Label { Dock = DockStyle.Fill, Font = ModernERPTheme.Small, ForeColor = ModernERPTheme.MutedText };
            Controls.Add(SubtitleLabel);
            Controls.Add(TitleLabel);
        }
    }

    public class ModernERPFormGroup : ModernERPCard
    {
        public Label HeaderLabel { get; private set; }
        public ModernERPTableLayout Fields { get; private set; }

        public ModernERPFormGroup()
        {
            HeaderLabel = new Label { Dock = DockStyle.Top, Height = 30, Font = ModernERPTheme.Heading, ForeColor = ModernERPTheme.Text };
            Fields = new ModernERPTableLayout { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            Fields.UseEqualColumns(2);
            Controls.Add(Fields);
            Controls.Add(HeaderLabel);
        }
    }

    public class ModernERPValidationLabel : Label
    {
        public ModernERPValidationLabel()
        {
            AutoSize = true;
            Font = ModernERPTheme.SmallBold;
            ForeColor = ModernERPTheme.Danger;
            Margin = new Padding(0, 0, 0, 8);
        }
    }

    public class ModernERPLoadingOverlay : Panel
    {
        private readonly Label _message;

        public ModernERPLoadingOverlay()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(220, DS.BgPage);
            Visible = false;
            _message = new Label
            {
                AutoSize = false,
                Width = 280,
                Height = 42,
                Font = ModernERPTheme.BodyBold,
                ForeColor = ModernERPTheme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = DS.BgCard,
                Text = "Loading..."
            };
            Controls.Add(_message);
            Resize += (s, e) => CenterMessage();
        }

        public string Message { get { return _message.Text; } set { _message.Text = value; } }

        public void ShowOverlay(string message)
        {
            Message = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
            Visible = true;
            BringToFront();
            CenterMessage();
        }

        public void HideOverlay()
        {
            Visible = false;
        }

        private void CenterMessage()
        {
            _message.Left = Math.Max(0, (Width - _message.Width) / 2);
            _message.Top = Math.Max(0, (Height - _message.Height) / 2);
            ModernERPTheme.ApplyRoundedRegion(_message, ModernERPTheme.Radius);
        }
    }

    public class ModernERPModalDialog : ServoERP.Infrastructure.ServoFormBase
    {
        public ModernERPTopBar Header { get; private set; }
        public ModernERPPanel Body { get; private set; }
        public ModernERPActionPanel Footer { get; private set; }

        public ModernERPModalDialog()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(620, 460);
            MinimumSize = new Size(420, 320);
            BackColor = ModernERPTheme.Workspace;
            Font = ModernERPTheme.Body;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Header = new ModernERPTopBar { Dock = DockStyle.Top };
            Body = new ModernERPPanel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            Footer = new ModernERPActionPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(20, 10, 20, 10), FlowDirection = FlowDirection.RightToLeft };

            Controls.Add(Body);
            Controls.Add(Footer);
            Controls.Add(Header);
        }
    }

    public static class ModernERPControlFactory
    {
        public static ModernERPButton PrimaryButton(string text, int width = 118)
        {
            return Button(text, ModernERPButtonVariant.Primary, width);
        }

        public static ModernERPButton SuccessButton(string text, int width = 118)
        {
            return Button(text, ModernERPButtonVariant.Success, width);
        }

        public static ModernERPButton DangerButton(string text, int width = 118)
        {
            return Button(text, ModernERPButtonVariant.Danger, width);
        }

        public static ModernERPButton Button(string text, ModernERPButtonVariant variant, int width = 118)
        {
            return new ModernERPButton { Text = text, Width = width, Variant = variant };
        }

        public static ModernERPCard Card(params Control[] children)
        {
            ModernERPCard card = new ModernERPCard();
            if (children != null)
                card.Controls.AddRange(children);
            return card;
        }
    }

    public class ModernCard : ModernERPCard
    {
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            return ModernERPTheme.RoundedRect(bounds, radius);
        }
    }

    public class ModernButton : ModernERPButton
    {
        public ModernButton()
        {
            Variant = ModernERPButtonVariant.Ghost;
        }
    }

    public class StatusChip : ModernERPStatusChip
    {
    }

    public class TaxSummaryBox : ModernERPCard
    {
        private readonly Label _title = new Label();
        private readonly Label _value = new Label();

        public TaxSummaryBox(string title, string value, Color valueColor)
        {
            ShowShadow = false;
            BackColor = ModernERPTheme.SurfaceAlt;
            Size = new Size(112, 58);
            Padding = new Padding(10, 7, 10, 7);
            _title.Text = title;
            _title.Dock = DockStyle.Top;
            _title.Height = 18;
            _title.Font = ModernERPTheme.Small;
            _title.ForeColor = ModernERPTheme.MutedText;
            _value.Text = value;
            _value.Dock = DockStyle.Fill;
            _value.Font = ModernERPTheme.BodyBold;
            _value.ForeColor = valueColor;
            Controls.Add(_value);
            Controls.Add(_title);
        }

        public Label ValueLabel { get { return _value; } }
    }

    public static class ToastNotification
    {
        public static void Show(Control owner, string message, Color accent)
        {
            ModernERPToast.Show(owner, message, accent);
        }
    }
}

