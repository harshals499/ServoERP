using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI.Controls
{
    public class ModernTextBox : UserControl
    {
        private readonly TextBox _inner = new TextBox();
        private bool _isFocused;
        private bool _isHovered;
        private string _placeholder = string.Empty;

        public ModernTextBox()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Padding = new Padding(52, 0, 16, 0);
            Height = 52;
            MinimumSize = new Size(180, 50);

            _inner.BorderStyle = BorderStyle.None;
            _inner.Font = new Font("Segoe UI", 10.5f);
            _inner.ForeColor = Color.FromArgb(15, 23, 42);
            _inner.BackColor = Color.White;
            _inner.TextChanged += (s, e) => OnTextChanged(e);
            _inner.Enter += (s, e) => { _isFocused = true; Invalidate(); };
            _inner.Leave += (s, e) => { _isFocused = false; Invalidate(); };
            _inner.MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            _inner.MouseLeave += (s, e) => { _isHovered = ClientRectangle.Contains(PointToClient(Cursor.Position)); Invalidate(); };
            Controls.Add(_inner);

            MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
        }

        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value ?? string.Empty;
        }

        public string Placeholder
        {
            get => _placeholder;
            set { _placeholder = value ?? string.Empty; Invalidate(); }
        }

        public bool UseSystemPasswordChar
        {
            get => _inner.UseSystemPasswordChar;
            set => _inner.UseSystemPasswordChar = value;
        }

        public char LeadingIcon { get; set; } = '\0';
        public TextBox InnerTextBox => _inner;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Height = Math.Max(50, Height);
            int innerHeight = _inner.PreferredHeight;
            _inner.Bounds = new Rectangle(
                Padding.Left,
                Math.Max(1, (Height - innerHeight) / 2),
                Math.Max(20, Width - Padding.Left - Padding.Right),
                innerHeight);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Rounded(rect, 12))
            using (SolidBrush bg = new SolidBrush(Color.White))
            using (Pen border = new Pen(_isFocused ? Color.FromArgb(37, 99, 235) : (_isHovered ? Color.FromArgb(148, 163, 184) : Color.FromArgb(203, 213, 225)), _isFocused ? 2f : 1f))
            {
                e.Graphics.FillPath(bg, path);
                e.Graphics.DrawPath(border, path);
            }

            if (LeadingIcon != '\0')
            {
                using (Font iconFont = new Font("Segoe MDL2 Assets", 16f))
                using (Brush brush = new SolidBrush(Color.FromArgb(82, 103, 143)))
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(LeadingIcon.ToString(), iconFont, brush, new RectangleF(16, 0, 28, Height), format);
            }

            if (string.IsNullOrEmpty(_inner.Text) && !_isFocused && !string.IsNullOrWhiteSpace(_placeholder))
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                    e.Graphics.DrawString(_placeholder, _inner.Font, brush, new RectangleF(Padding.Left, 0, Width - Padding.Left - Padding.Right, Height), format);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _inner.Focus();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            _inner.Focus();
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
