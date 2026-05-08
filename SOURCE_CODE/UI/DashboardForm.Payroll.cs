using System;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public partial class DashboardForm
    {
        private Panel BuildPayrollSection()
        {
            bool compact = IsCompactDashboard();
            int payrollHeight = DashboardCardHeight(148, 170);
            Panel wrap = new Panel
            {
                Dock = DockStyle.Top,
                Height = payrollHeight,
                Padding = compact ? new Padding(32, 4, 16, 4) : new Padding(32, 8, 24, 8),
                BackColor = BgPage
            };

            Panel card = new Panel
            {
                BackColor = Color.White,
                Size = new Size(1080, Math.Max(96, payrollHeight - (compact ? 8 : 20))),
                Padding = compact ? new Padding(12, 8, 12, 8) : new Padding(18, 16, 18, 16)
            };
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(BorderLine))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            card.Dock = DockStyle.Fill;

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = compact ? 42 : 48,
                BackColor = Color.White
            };
            var title = new Label
            {
                Text = "Payroll",
                Font = new Font("Segoe UI", compact ? 10.5f : 12, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(0, 0),
                Size = new Size(520, compact ? 18 : 22),
                AutoEllipsis = true
            };
            var subtitle = new Label
            {
                Text = "Payroll cycle, liabilities, and the most recent processing status in one place.",
                Font = new Font("Segoe UI", compact ? 7.5f : 8.5f),
                ForeColor = TextMid,
                Location = new Point(0, compact ? 18 : 24),
                Size = new Size(860, compact ? 16 : 20),
                AutoEllipsis = true
            };
            _dashboardToolTip.SetToolTip(subtitle, subtitle.Text);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Resize += (s, e) =>
            {
                title.Width = Math.Max(120, header.ClientSize.Width - 8);
                subtitle.Width = Math.Max(120, header.ClientSize.Width - 8);
            };

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = compact ? new Padding(0, 4, 0, 0) : new Padding(0, 8, 0, 0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19f));

            content.Controls.Add(BuildPayrollMetric("Next payroll due", _payrollSnapshot?.NextPayrollLabel ?? "-", (_payrollSnapshot?.DaysUntilNextPayroll ?? 0) + " days left", AccentIndigo, "dashboard_payroll_next_due"), 0, 0);
            content.Controls.Add(BuildPayrollMetric("Statutory overdue", (_payrollSnapshot?.OverdueStatutoryPayments ?? 0).ToString(), (_payrollSnapshot?.OverdueStatutoryPayments ?? 0) > 0 ? "Needs attention" : "All clear", (_payrollSnapshot?.OverdueStatutoryPayments ?? 0) > 0 ? AccentRed : AccentGreen, "dashboard_payroll_statutory_overdue"), 1, 0);
            content.Controls.Add(BuildPayrollMetric("Last payroll run", _payrollSnapshot?.LastRun == null ? "None" : new System.DateTime(_payrollSnapshot.LastRun.PayrollYear, _payrollSnapshot.LastRun.PayrollMonth, 1).ToString("MMM yyyy"), _payrollSnapshot?.LastRun?.Status ?? "Pending", AccentIndigo, "dashboard_payroll_last_run"), 2, 0);

            Button btnOpen = new Button
            {
                Text = "Open Payroll",
                Dock = DockStyle.Fill,
                BackColor = AccentIndigo,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", compact ? 8 : 9, FontStyle.Bold),
                Margin = compact ? new Padding(8, 4, 0, 4) : new Padding(12, 8, 0, 8)
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) => OnNavigate?.Invoke(13);
            content.Controls.Add(btnOpen, 3, 0);

            card.Controls.Add(content);
            card.Controls.Add(header);
            wrap.Controls.Add(card);
            return wrap;
        }

        private Control BuildPayrollMetric(string title, string value, string subtext, Color accent, string cardKey)
        {
            bool compact = IsCompactDashboard();
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = compact ? new Padding(0, 0, 8, 0) : new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = compact ? new Padding(8, 6, 8, 6) : new Padding(12, 10, 12, 10)
            };
            panel.Paint += (s, e) =>
            {
                using (var pen = new Pen(BorderLine))
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", compact ? 7 : 8, FontStyle.Bold),
                ForeColor = TextLight,
                AutoSize = false,
                Location = new Point(panel.Padding.Left, 0),
                Size = new Size(220, compact ? 15 : 18),
                AutoEllipsis = true
            };
            _dashboardToolTip.SetToolTip(titleLabel, title);
            panel.Controls.Add(titleLabel);
            var valueLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", compact ? 12.5f : 15, FontStyle.Bold),
                ForeColor = accent,
                AutoSize = false,
                Location = new Point(panel.Padding.Left, compact ? 16 : 18),
                Size = new Size(220, compact ? 24 : 28),
                AutoEllipsis = true
            };
            _dashboardToolTip.SetToolTip(valueLabel, value);
            panel.Controls.Add(valueLabel);
            var subLabel = new Label
            {
                Text = subtext,
                Font = new Font("Segoe UI", compact ? 7 : 8),
                ForeColor = TextMid,
                AutoSize = false,
                Location = new Point(panel.Padding.Left, compact ? 40 : 46),
                Size = new Size(220, compact ? 15 : 18),
                AutoEllipsis = true
            };
            _dashboardToolTip.SetToolTip(subLabel, subtext);
            panel.Controls.Add(subLabel);
            panel.Resize += (s, e) =>
            {
                int width = Math.Max(40, panel.ClientSize.Width - panel.Padding.Horizontal);
                titleLabel.Width = width;
                valueLabel.Width = width;
                subLabel.Width = width;
            };
            return panel;
        }
    }
}
