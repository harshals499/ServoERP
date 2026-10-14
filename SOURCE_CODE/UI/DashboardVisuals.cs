using System;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Shared presentation used by Quotations and My Work.</summary>
    internal static class DashboardVisuals
    {
        internal static readonly Color Ink = Color.FromArgb(17, 24, 39);
        internal static readonly Color Blue = Color.FromArgb(37, 99, 235);

        internal static Panel Card(int width, int height)
        {
            return new QuotationForecastCard { Width = width, Height = height,
                Margin = new Padding(0, 0, 0, 12), Tag = "NO_DASHBOARD_RESIZE" };
        }

        internal static Panel Metric(string title, string valueText, string subtitle,
            ModernIconKind icon, Color accent, Color iconBack,
            out Label value, out Label sub)
        {
            var card = new QuotationForecastCard { Padding = new Padding(70, 10, 8, 8) };
            var metricIcon = new PictureBox { Name = "MetricIcon", Location = new Point(14, 17),
                Size = new Size(42, 42), BackColor = iconBack,
                Image = ModernIconSystem.IconBitmap(icon, 22, accent), SizeMode = PictureBoxSizeMode.CenterImage };
            metricIcon.Disposed += (s, e) => metricIcon.Image?.Dispose();
            card.Controls.Add(metricIcon);
            var titleLabel = Text(title, 70, 11, 18, 8.2f, true, Ink);
            titleLabel.Name = "MetricTitle";
            value = Text(valueText, 70, 31, 27, 15f, true, accent);
            value.Name = "MetricValue";
            sub = Text(subtitle, 70, 63, 17, 7.5f, false, DS.Slate500);
            sub.Name = "MetricSubtitle";
            card.Controls.Add(titleLabel); card.Controls.Add(value); card.Controls.Add(sub);
            Label valueLabel = value, subLabel = sub;
            EventHandler layout = (s, e) =>
            {
                int width = Math.Max(72, card.ClientSize.Width - 80);
                titleLabel.Width = valueLabel.Width = subLabel.Width = width;
            };
            card.Resize += layout;
            layout(card, EventArgs.Empty);
            return card;
        }

        internal static void Heading(Control card, string title, ModernIconKind icon,
            Color accent, string subtitle = null, int actionWidth = 0)
        {
            var headingIcon = new PictureBox { Location = new Point(12, 10), Size = new Size(20, 20),
                Image = ModernIconSystem.IconBitmap(icon, 17, accent), SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent };
            headingIcon.Disposed += (s, e) => headingIcon.Image?.Dispose();
            card.Controls.Add(headingIcon);
            var heading = Text(title, 38, 8, 24, 9.5f, true, Ink);
            heading.Name = "CardHeading";
            heading.TextAlign = ContentAlignment.MiddleLeft;
            heading.Width = Math.Max(80, card.Width - 50 - actionWidth);
            card.Controls.Add(heading);
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var description = Text(subtitle, 12, 34, 20, 7.5f, false, DS.Slate500);
                description.Width = Math.Max(80, card.Width - 24 - actionWidth);
                card.Controls.Add(description);
            }
        }

        internal static Button Action(string text, int width, Color back, Color fore)
        {
            var button = new Button { Text = text, Width = width, Height = 30, BackColor = back,
                ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false, AutoEllipsis = true };
            button.FlatAppearance.BorderColor = Color.FromArgb(214, 222, 232);
            return button;
        }

        internal static Button Chip(string text, bool selected)
        {
            var button = Action(text, Math.Max(66, TextRenderer.MeasureText(text, DS.SmallBold).Width + 24),
                selected ? Blue : Color.FromArgb(245, 248, 252), selected ? Color.White : DS.Slate500);
            button.Height = 27; button.Margin = new Padding(0, 0, 6, 0);
            button.FlatAppearance.BorderColor = selected ? Blue : Color.FromArgb(226, 232, 240);
            return button;
        }

        private static Label Text(string text, int x, int y, int height, float size, bool bold, Color fore)
        {
            return new Label { Text = text, Location = new Point(x, y), Height = height,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = fore, BackColor = Color.Transparent, AutoEllipsis = true, UseMnemonic = false };
        }
    }
}
