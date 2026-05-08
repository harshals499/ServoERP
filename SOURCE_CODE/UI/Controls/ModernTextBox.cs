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
        private string _placeholder = string.Empty;

        public ModernTextBox()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Padding = new Padding(50, 9, 44, 7);
            Height = 58;
            MinimumSize = new Size(180, 54);

            _inner.BorderStyle = BorderStyle.None;
            _inner.Font = new Font("Segoe UI", 11f);
            _inner.ForeColor = Color.FromArgb(15, 23, 42);
            _inner.Dock = DockStyle.Fill;
            _inner.BackColor = Color.White;
            _inner.TextChanged += (s, e) => OnTextChanged(e);
            _inner.Enter += (s, e) => { _isFocused = true; Invalidate(); };
            _inner.Leave += (s, e) => { _isFocused = false; Invalidate(); };
            Controls.Add(_inner);
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
            Height = Math.Max(54, Height);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Rounded(rect, 12))
            using (SolidBrush bg = new SolidBrush(Color.White))
            using (Pen border = new Pen(_isFocused ? Color.FromArgb(37, 99, 235) : Color.FromArgb(203, 213, 225), _isFocused ? 2f : 1f))
            {
                e.Graphics.FillPath(bg, path);
                e.Graphics.DrawPath(border, path);
            }

            if (LeadingIcon != '\0')
            {
                using (Font iconFont = new Font("Segoe MDL2 Assets", 16f))
                using (Brush brush = new SolidBrush(Color.FromArgb(82, 103, 143)))
                    e.Graphics.DrawString(LeadingIcon.ToString(), iconFont, brush, new PointF(17, 15));
            }

            if (string.IsNullOrEmpty(_inner.Text) && !_isFocused && !string.IsNullOrWhiteSpace(_placeholder))
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                    e.Graphics.DrawString(_placeholder, _inner.Font, brush, new PointF(Padding.Left + 2, 18));
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
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
