using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public class AccordionPanel : UserControl
    {
        private readonly HeaderSurface _header;
        private readonly Label _titleLabel;
        private readonly Label _badgeLabel;
        private readonly Panel _contentPanel;
        private bool _isExpanded = true;
        private int _headerHeight = 36;
        private int _storedContentHeight;
        private bool _suppressEvent;

        public event EventHandler CollapsedChanged;

        public AccordionPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.White;
            Margin = new Padding(0, 0, 0, 12);

            _header = new HeaderSurface(this)
            {
                Dock = DockStyle.Top,
                Height = _headerHeight,
                Cursor = Cursors.Hand
            };
            _header.Click += Header_Click;

            _titleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(24, 0),
                Height = _headerHeight,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _titleLabel.Click += Header_Click;

            _badgeLabel = new Label
            {
                AutoSize = false,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                Cursor = Cursors.Hand
            };
            _badgeLabel.Paint += BadgeLabel_Paint;
            _badgeLabel.Click += Header_Click;

            _header.Controls.Add(_titleLabel);
            _header.Controls.Add(_badgeLabel);

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0, 8, 0, 8),
                Visible = true
            };

            Controls.Add(_contentPanel);
            Controls.Add(_header);

            HeaderText = "Section";
            HeaderBackground = Color.White;
            HeaderBorderColor = Color.FromArgb(230, 230, 230);
            HeaderTextColor = Color.FromArgb(60, 60, 60);
            HeaderFontSize = 12;
            BadgeBackground = ColorTranslator.FromHtml("#E1F5EE");
            BadgeTextColor = ColorTranslator.FromHtml("#0F6E56");

            Resize += (s, e) => LayoutHeader();
            Load += (s, e) => CacheContentHeight();
        }

        [Browsable(true)]
        public string HeaderText
        {
            get { return _titleLabel.Text; }
            set { _titleLabel.Text = value ?? string.Empty; LayoutHeader(); }
        }

        [Browsable(true)]
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { ApplyExpanded(value, true); }
        }

        [Browsable(true)]
        public int HeaderHeight
        {
            get { return _headerHeight; }
            set
            {
                _headerHeight = Math.Max(24, value);
                _header.Height = _headerHeight;
                _titleLabel.Height = _headerHeight;
                if (!_isExpanded)
                    Height = _headerHeight;
                LayoutHeader();
                _header.Invalidate();
            }
        }

        [Browsable(true)]
        public Color HeaderBackground
        {
            get { return _header.HeaderBackground; }
            set { _header.HeaderBackground = value; _header.Invalidate(); }
        }

        [Browsable(true)]
        public Color HeaderBorderColor
        {
            get { return _header.HeaderBorderColor; }
            set { _header.HeaderBorderColor = value; _header.Invalidate(); }
        }

        [Browsable(true)]
        public Color HeaderTextColor
        {
            get { return _titleLabel.ForeColor; }
            set { _titleLabel.ForeColor = value; }
        }

        [Browsable(true)]
        public int HeaderFontSize
        {
            get { return (int)Math.Round(_titleLabel.Font.SizeInPoints); }
            set { _titleLabel.Font = new Font("Segoe UI", Math.Max(7, value), FontStyle.Regular); LayoutHeader(); }
        }

        [Browsable(true)]
        public bool ShowBadge
        {
            get { return _badgeLabel.Visible; }
            set { _badgeLabel.Visible = value; LayoutHeader(); }
        }

        [Browsable(true)]
        public string BadgeText
        {
            get { return _badgeLabel.Text; }
            set { _badgeLabel.Text = value ?? string.Empty; LayoutHeader(); _badgeLabel.Invalidate(); }
        }

        [Browsable(true)]
        public Color BadgeBackground
        {
            get { return _badgeLabel.BackColor; }
            set { _badgeLabel.BackColor = value; _badgeLabel.Invalidate(); }
        }

        [Browsable(true)]
        public Color BadgeTextColor
        {
            get { return _badgeLabel.ForeColor; }
            set { _badgeLabel.ForeColor = value; _badgeLabel.Invalidate(); }
        }

        public Panel ContentPanel
        {
            get { return _contentPanel; }
        }

        public void SetExpanded(bool expanded, bool animate = false)
        {
            _suppressEvent = true;
            try
            {
                ApplyExpanded(expanded, false);
            }
            finally
            {
                _suppressEvent = false;
            }
        }

        private void Header_Click(object sender, EventArgs e)
        {
            ApplyExpanded(!_isExpanded, true);
        }

        private void ApplyExpanded(bool expanded, bool fireEvent)
        {
            if (_isExpanded == expanded)
                return;

            CacheContentHeight();
            _isExpanded = expanded;
            _contentPanel.Visible = expanded;
            Height = expanded ? HeaderHeight + Math.Max(1, _storedContentHeight) : HeaderHeight;
            _header.IsExpanded = expanded;
            _header.Invalidate();

            Control parent = Parent;
            if (parent != null)
            {
                parent.PerformLayout();
                if (parent.Parent != null)
                    parent.Parent.PerformLayout();
            }

            if (fireEvent && !_suppressEvent)
                CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CacheContentHeight()
        {
            if (_contentPanel == null)
                return;

            int preferred = _contentPanel.PreferredSize.Height + _contentPanel.Padding.Vertical;
            if (preferred <= _contentPanel.Padding.Vertical)
                preferred = Math.Max(1, Height - HeaderHeight);
            if (preferred > 0)
                _storedContentHeight = preferred;
        }

        private void LayoutHeader()
        {
            if (_header == null || _titleLabel == null || _badgeLabel == null)
                return;

            int badgeWidth = 0;
            if (_badgeLabel.Visible)
            {
                badgeWidth = Math.Max(34, TextRenderer.MeasureText(_badgeLabel.Text ?? string.Empty, new Font("Segoe UI", 8.5f)).Width + 18);
                _badgeLabel.SetBounds(Math.Max(28, Width - badgeWidth - 12), Math.Max(0, (HeaderHeight - 22) / 2), badgeWidth, 22);
            }

            int rightEdge = _badgeLabel.Visible ? _badgeLabel.Left - 8 : Width - 12;
            _titleLabel.SetBounds(24, 0, Math.Max(20, rightEdge - 24), HeaderHeight);
        }

        private void BadgeLabel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, _badgeLabel.Width - 1, _badgeLabel.Height - 1);
            using (GraphicsPath path = RoundedPath(rect, 11))
            using (SolidBrush brush = new SolidBrush(_badgeLabel.BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            TextRenderer.DrawText(e.Graphics, _badgeLabel.Text, new Font("Segoe UI", 8.5f, FontStyle.Bold), rect, _badgeLabel.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class HeaderSurface : Panel
        {
            private readonly AccordionPanel _owner;
            private bool _hover;

            public HeaderSurface(AccordionPanel owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                HeaderBackground = Color.White;
                HeaderBorderColor = Color.FromArgb(230, 230, 230);
                IsExpanded = true;
                MouseEnter += (s, e) => { _hover = true; Invalidate(); };
                MouseLeave += (s, e) => { _hover = false; Invalidate(); };
            }

            public Color HeaderBackground { get; set; }
            public Color HeaderBorderColor { get; set; }
            public bool IsExpanded { get; set; }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Color bg = _hover ? Lighten(HeaderBackground) : HeaderBackground;
                using (SolidBrush brush = new SolidBrush(bg))
                    e.Graphics.FillRectangle(brush, ClientRectangle);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Point[] triangle = IsExpanded
                    ? new[] { new Point(9, 14), new Point(17, 14), new Point(13, 21) }
                    : new[] { new Point(10, 12), new Point(10, 20), new Point(17, 16) };
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(153, 153, 153)))
                    e.Graphics.FillPolygon(brush, triangle);

                using (Pen pen = new Pen(HeaderBorderColor, 1f))
                    e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                _owner.LayoutHeader();
            }

            private static Color Lighten(Color color)
            {
                return Color.FromArgb(color.A, Math.Min(255, color.R + 8), Math.Min(255, color.G + 8), Math.Min(255, color.B + 8));
            }
        }
    }
}
