using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class DS
    {
        public static readonly Color White = Color.White;
        public static readonly Color BgPage = Color.FromArgb(244, 248, 252);
        public static readonly Color BgSubtle = Color.FromArgb(239, 246, 255);
        public static readonly Color BgCard = Color.White;
        public static readonly Color BgCardHov = Color.FromArgb(244, 247, 255);
        public static readonly Color BgInput = Color.White;
        public static readonly Color BgRail = Color.FromArgb(250, 250, 250);

        public static readonly Color Primary700 = Color.FromArgb(29, 78, 216);
        public static readonly Color Primary600 = Color.FromArgb(37, 99, 235);
        public static readonly Color Primary500 = Color.FromArgb(59, 130, 246);
        public static readonly Color Primary100 = Color.FromArgb(219, 234, 254);
        public static readonly Color Primary50 = Color.FromArgb(239, 246, 255);

        public static readonly Color Teal600 = Color.FromArgb(13, 148, 136);
        public static readonly Color Teal500 = Color.FromArgb(20, 184, 166);
        public static readonly Color Teal50 = Color.FromArgb(240, 253, 250);
        public static readonly Color Green600 = Color.FromArgb(22, 163, 74);
        public static readonly Color Green50 = Color.FromArgb(240, 253, 244);
        public static readonly Color Amber600 = Color.FromArgb(217, 119, 6);
        public static readonly Color Amber500 = Color.FromArgb(245, 158, 11);
        public static readonly Color Amber50 = Color.FromArgb(255, 251, 235);
        public static readonly Color Red600 = Color.FromArgb(220, 38, 38);
        public static readonly Color Red500 = Color.FromArgb(239, 68, 68);
        public static readonly Color Red50 = Color.FromArgb(254, 242, 242);

        public static readonly Color Slate950 = Color.FromArgb(2, 6, 23);
        public static readonly Color Slate900 = Color.FromArgb(15, 23, 42);
        public static readonly Color Slate800 = Color.FromArgb(30, 41, 59);
        public static readonly Color Slate700 = Color.FromArgb(51, 65, 85);
        public static readonly Color Slate600 = Color.FromArgb(71, 85, 105);
        public static readonly Color Slate500 = Color.FromArgb(100, 116, 139);
        public static readonly Color Slate400 = Color.FromArgb(148, 163, 184);
        public static readonly Color Slate300 = Color.FromArgb(203, 213, 225);
        public static readonly Color Slate200 = Color.FromArgb(226, 232, 240);
        public static readonly Color Slate100 = Color.FromArgb(241, 245, 249);
        public static readonly Color Slate50 = Color.FromArgb(248, 250, 252);

        public static readonly Color Border = Color.FromArgb(228, 228, 231);
        public static readonly Color BorderStrong = Color.FromArgb(212, 212, 216);
        public static readonly Color FocusBlue = Color.FromArgb(129, 140, 248);
        public static readonly Color Shadow = Color.FromArgb(205, 216, 232);
        public static readonly Color Indigo600 = Primary700;
        public static readonly Color Indigo500 = Primary600;
        public static readonly Color Indigo100 = Primary100;
        public static readonly Color Indigo50 = Primary50;
        public static readonly Color Navy900 = Slate950;
        public static readonly Color Navy800 = Slate900;
        public static readonly Color Green500 = Green600;

        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 12;
        public const int Space4 = 16;
        public const int Space5 = 20;
        public const int Space6 = 24;
        public const int RadiusSm = 6;
        public const int RadiusMd = 8;
        public const int RadiusLg = 10;
        public const int RadiusXl = 12;

        private static readonly object RoundedSync = new object();
        private static readonly System.Collections.Generic.Dictionary<Control, int> RoundedControls =
            new System.Collections.Generic.Dictionary<Control, int>();

        public static Font H1 => new Font("Segoe UI", 18f, FontStyle.Bold);
        public static Font H2 => new Font("Segoe UI", 14f, FontStyle.Bold);
        public static Font H3 => new Font("Segoe UI", 11f, FontStyle.Bold);
        public static Font Body => new Font("Segoe UI", 9f);
        public static Font BodyBold => new Font("Segoe UI", 9f, FontStyle.Bold);
        public static Font Small => new Font("Segoe UI", 8f);
        public static Font SmallBold => new Font("Segoe UI", 8f, FontStyle.Bold);
        public static Font Caption => new Font("Segoe UI", 7.5f);
        public static Font Mono => new Font("Consolas", 8f);

        [DllImport("Gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeft, int nTop, int nRight, int nBottom, int nWidth, int nHeight);

        [DllImport("Gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static void Rounded(Control control, int radius = RadiusMd)
        {
            if (control == null)
                return;

            ApplyRoundedRegion(control, radius);

            lock (RoundedSync)
            {
                if (RoundedControls.ContainsKey(control))
                {
                    RoundedControls[control] = radius;
                    return;
                }

                RoundedControls[control] = radius;
            }

            control.Resize += RoundedControlResize;
            control.Disposed += RoundedControlDisposed;
        }

        private static void RoundedControlResize(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            int radius;
            lock (RoundedSync)
            {
                if (!RoundedControls.TryGetValue(control, out radius))
                    return;
            }

            ApplyRoundedRegion(control, radius);
        }

        private static void RoundedControlDisposed(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            lock (RoundedSync)
            {
                RoundedControls.Remove(control);
            }
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0 || control.IsDisposed)
                return;

            IntPtr region = CreateRoundRectRgn(0, 0, control.Width + 1, control.Height + 1, radius, radius);
            Region oldRegion = control.Region;
            control.Region = Region.FromHrgn(region);
            DeleteObject(region);
            if (oldRegion != null)
                oldRegion.Dispose();
        }

        public static Panel MakeCard(out Panel inner, int radius = RadiusLg, Padding? padding = null)
        {
            Panel shadow = new Panel
            {
                BackColor = Shadow,
                Padding = new Padding(1),
                Margin = new Padding(0, 0, 0, Space4)
            };
            Rounded(shadow, radius + 2);

            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgCard,
                Padding = padding ?? new Padding(Space4)
            };
            Rounded(card, radius);
            shadow.Controls.Add(card);
            inner = card;
            return shadow;
        }

        public static Button PrimaryBtn(string text, int width = 120, int height = 34)
        {
            return MakeButton(text, Primary600, Color.White, Color.Transparent, width, height, BodyBold);
        }

        public static Button GhostBtn(string text, int width = 100, int height = 34)
        {
            return MakeButton(text, White, Slate700, BorderStrong, width, height, BodyBold);
        }

        public static Button DangerBtn(string text, int width = 100, int height = 34)
        {
            return MakeButton(text, Red50, Red600, Color.FromArgb(254, 202, 202), width, height, BodyBold);
        }

        public static Button CommandBtn(string text, Color accent, int width = 118, int height = 34)
        {
            return MakeButton(text, accent, Color.White, Color.Transparent, width, height, BodyBold);
        }

        private static Button MakeButton(string text, Color bg, Color fg, Color border, int width, int height, Font font)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = bg,
                ForeColor = fg,
                Font = font,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(10, 0, 10, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = border == Color.Transparent ? 0 : 1;
            button.FlatAppearance.BorderColor = border == Color.Transparent ? bg : border;
            button.FlatAppearance.MouseOverBackColor = bg.GetBrightness() > 0.92f ? BgCardHov : Lighten(bg, 0.08f);
            button.FlatAppearance.MouseDownBackColor = bg.GetBrightness() > 0.92f ? Slate100 : Darken(bg, 0.10f);
            Rounded(button, RadiusSm);
            return button;
        }

        public static Panel Badge(string text, Color bg, Color fg)
        {
            Panel panel = new Panel { AutoSize = true, BackColor = bg, Padding = new Padding(8, 3, 8, 3) };
            panel.Controls.Add(new Label { Text = text, Font = SmallBold, ForeColor = fg, AutoSize = true });
            Rounded(panel, 12);
            return panel;
        }

        public static Label StatusChipLabel(string text, Color bg, Color fg, int width = 86)
        {
            var label = new Label
            {
                Text = text,
                BackColor = bg,
                ForeColor = fg,
                Font = SmallBold,
                AutoSize = false,
                Width = width,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(6, 2, 6, 2)
            };
            Rounded(label, 11);
            return label;
        }

        public static Label SectionHeading(string text)
        {
            return new Label
            {
                Text = (text ?? string.Empty).ToUpperInvariant(),
                Font = CaptionBold(),
                ForeColor = Slate500,
                AutoSize = true
            };
        }

        public static Font CaptionBold()
        {
            return new Font("Segoe UI", 7.5f, FontStyle.Bold);
        }

        public static Panel PageHeader(string title)
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = White,
                Padding = new Padding(18, 0, 18, 0)
            };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border, 1))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            header.Controls.Add(new Label
            {
                Text = title ?? string.Empty,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Slate900,
                TextAlign = ContentAlignment.MiddleLeft
            });

            return header;
        }

        public static Panel FieldRow(string label, out TextBox textBox, bool multiline = false, int height = 34)
        {
            int rowHeight = multiline ? height + 26 : 62;
            Panel row = new Panel { Height = rowHeight, Dock = DockStyle.Top, BackColor = Color.Transparent };

            var fieldLabel = new Label
            {
                Text = label,
                Font = SmallBold,
                ForeColor = Slate600,
                Location = new Point(0, 0),
                AutoSize = true
            };

            Panel inputWrap = new Panel
            {
                Location = new Point(0, 20),
                Height = multiline ? height : 34,
                BackColor = BgInput,
                Padding = new Padding(9, 5, 9, 5)
            };
            inputWrap.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, inputWrap.Width - 1, inputWrap.Height - 1);
            };
            Rounded(inputWrap, RadiusSm);

            textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = Body,
                BorderStyle = BorderStyle.None,
                BackColor = BgInput,
                ForeColor = Slate900,
                Multiline = multiline
            };
            inputWrap.Controls.Add(textBox);
            inputWrap.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Resize += (s, e) => { inputWrap.Width = row.ClientSize.Width; };

            row.Controls.Add(fieldLabel);
            row.Controls.Add(inputWrap);
            return row;
        }

        public static void ApplyTheme(Control root)
        {
            if (root == null)
                return;

            ApplyThemeToControl(root);
            foreach (Control child in root.Controls)
                ApplyTheme(child);
        }

        public static void StyleGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            GridTheme.Apply(grid);
        }

        public static void StyleButton(Button button)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI", Math.Max(8.5f, button.Font.Size), FontStyle.Bold);
            button.Height = Math.Max(button.Height, 32);
            button.Padding = button.Padding == Padding.Empty ? new Padding(10, 0, 10, 0) : button.Padding;

            bool isLight = button.BackColor.ToArgb() == Color.White.ToArgb()
                || button.BackColor.ToArgb() == Color.Transparent.ToArgb()
                || button.BackColor.GetBrightness() > 0.92f;

            if (isLight)
            {
                button.BackColor = White;
                button.ForeColor = Slate700;
                button.FlatAppearance.BorderColor = BorderStrong;
                button.FlatAppearance.BorderSize = Math.Max(1, button.FlatAppearance.BorderSize);
                button.FlatAppearance.MouseOverBackColor = BgCardHov;
                button.FlatAppearance.MouseDownBackColor = Slate100;
            }
            else
            {
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Lighten(button.BackColor, 0.08f);
                button.FlatAppearance.MouseDownBackColor = Darken(button.BackColor, 0.10f);
            }
            Rounded(button, RadiusSm);
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

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(2, radius) * 2;
            GraphicsPath path = new GraphicsPath();
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void ApplyThemeToControl(Control control)
        {
            if (control is DataGridView grid)
            {
                if ((grid.Tag as string)?.IndexOf("SkipGlobalGridTheme", StringComparison.OrdinalIgnoreCase) >= 0)
                    return;
                StyleGrid(grid);
                return;
            }

            if (control is Button button)
            {
                StyleButton(button);
                return;
            }

            if (control is Label label)
            {
                bool whiteText = label.ForeColor.ToArgb() == Color.White.ToArgb();
                Color parentBack = label.Parent == null ? Color.Empty : label.Parent.BackColor;
                bool onLightSurface = parentBack == Color.Empty
                    || parentBack.ToArgb() == White.ToArgb()
                    || parentBack.ToArgb() == Slate50.ToArgb()
                    || parentBack.GetBrightness() > 0.92f;

                if (whiteText && onLightSurface)
                    label.ForeColor = Slate900;
                return;
            }

            if (control is TabPage tabPage)
            {
                tabPage.BackColor = BgPage;
                return;
            }

            if (control is Form || control is UserControl)
            {
                control.BackColor = BgPage;
                return;
            }

            if (control is Panel panel && panel.BackColor.ToArgb() == SystemColors.Control.ToArgb())
            {
                panel.BackColor = BgPage;
                return;
            }

            if (control is TabControl tabControl)
            {
                tabControl.Font = Body;
                return;
            }

            if (control is TextBox textBox)
            {
                textBox.Font = Body;
                textBox.ForeColor = Slate900;
                if (textBox.BorderStyle != BorderStyle.None)
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                if (!textBox.Multiline)
                    textBox.Height = Math.Max(textBox.Height, 30);
                if (!textBox.ReadOnly)
                    textBox.BackColor = White;
                else
                    textBox.BackColor = Slate50;
                return;
            }

            if (control is ComboBox comboBox)
            {
                comboBox.Font = Body;
                comboBox.BackColor = White;
                comboBox.ForeColor = Slate900;
                comboBox.FlatStyle = FlatStyle.System;
                comboBox.Height = Math.Max(comboBox.Height, 30);
                return;
            }

            if (control is DateTimePicker datePicker)
            {
                datePicker.Font = Body;
                datePicker.CalendarForeColor = Slate900;
                datePicker.CalendarMonthBackground = White;
                return;
            }

            if (control is NumericUpDown numeric)
            {
                numeric.Font = Body;
                numeric.BackColor = White;
                numeric.ForeColor = Slate900;
                numeric.BorderStyle = BorderStyle.FixedSingle;
                return;
            }
        }
    }
}
