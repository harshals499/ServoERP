using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class DashboardCardMetric
    {
        public string Value { get; set; }
        public string Label { get; set; }
        public Color? Color { get; set; }
    }

    public sealed class DashboardCardPill
    {
        public string Text { get; set; }
        public Color Color { get; set; }
    }

    public sealed class DashboardDeptCard : Panel
    {
        public DashboardDeptCard(
            ModernIconKind icon,
            Color iconBackColor,
            Color iconColor,
            string title,
            DashboardCardMetric primary,
            DashboardCardMetric secondary,
            IEnumerable<DashboardCardPill> pills,
            Action viewAction)
        {
            BackColor = DS.BgCard;
            Height = 220;
            MinimumSize = new Size(220, 220);
            Padding = new Padding(14);
            Margin = new Padding(6);
            DoubleBuffered = true;

            Label badge = ModernIconSystem.Badge(icon, 48, iconBackColor, iconColor, 10);
            badge.Location = new Point(14, 14);
            Controls.Add(badge);

            Controls.Add(new Label
            {
                Text = title,
                Location = new Point(74, 20),
                Size = new Size(160, 22),
                Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            });

            AddMetric(primary, 70);
            if (pills != null)
                AddPills(pills, 116);
            if (secondary != null)
                AddMetric(secondary, pills == null ? 122 : 138);

            Button view = new Button
            {
                Text = "View Details    >",
                Height = 30,
                Dock = DockStyle.Bottom,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = DS.Slate900,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            view.FlatAppearance.BorderColor = DS.Border;
            view.FlatAppearance.MouseOverBackColor = DS.Primary50;
            view.Click += (s, e) => viewAction?.Invoke();
            Controls.Add(view);
        }

        private void AddMetric(DashboardCardMetric metric, int y)
        {
            if (metric == null)
                return;

            Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(metric.Value) ? "-" : metric.Value,
                Location = new Point(74, y),
                Size = new Size(170, 26),
                Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = metric.Color ?? DS.Slate900,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            });
            Controls.Add(new Label
            {
                Text = metric.Label ?? string.Empty,
                Location = new Point(74, y + 27),
                Size = new Size(170, 18),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate600,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            });
        }

        private void AddPills(IEnumerable<DashboardCardPill> pills, int y)
        {
            int x = 74;
            foreach (DashboardCardPill pill in pills)
            {
                Label label = new Label
                {
                    Text = "● " + pill.Text,
                    Location = new Point(x, y),
                    Size = new Size(86, 18),
                    Font = new Font("Segoe UI", 7.6f),
                    ForeColor = pill.Color,
                    AutoEllipsis = true,
                    BackColor = Color.Transparent
                };
                Controls.Add(label);
                x += 92;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (SolidBrush brush = new SolidBrush(BackColor))
            using (Pen pen = new Pen(DS.Border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }
    }
}
