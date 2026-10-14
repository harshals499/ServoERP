using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class DashboardForm
    {
        private static readonly Color SoftCanvas = DS.BgPage;
        private static readonly Color SoftBorder = DS.Border;
        private static readonly Color SoftInk = Color.FromArgb(17, 24, 39);
        private static readonly Color SoftBlue = DS.Primary600;
        private DashboardHoverPreview _dashboardPreview;
        private int _inboxPage = 1;
        private int _inboxPageSize = 3;

        internal static bool OwnsDashboardPresentation(Control control)
        {
            for (Control current = control; current != null; current = current.Parent)
                if (current is DashboardForm || current is GlobalSearchDialog || current is WorkspaceHeaderPanel) return true;
            return false;
        }

        private bool DashboardCan(string module, string action)
        {
            var user = SessionManager.CurrentUser;
            RolePermissionDto permission;
            if (user?.Permissions != null && user.Permissions.Count > 0 &&
                (!user.Permissions.TryGetValue(module, out permission) || permission == null ||
                 (action == "Create" ? !permission.CanCreate : !permission.CanView))) return false;
            return SessionManager.HasPermission(module, action);
        }
        private bool DashboardCanView(string module) => DashboardCan(module, "View");
        private List<Job> DashboardJobs
        {
            get
            {
                if (!DashboardCanView("WorkOrders")) return new List<Job>();
                var user = SessionManager.CurrentUser;
                if ((user?.RoleName ?? "").IndexOf("technician", StringComparison.OrdinalIgnoreCase) < 0) return _jobs;
                return _jobs.Where(j => (user.EmployeeId.HasValue && j.AssignedEmployeeID == user.EmployeeId) ||
                    (!string.IsNullOrWhiteSpace(j.AssignedEmployeeName) &&
                     (IsAny(j.AssignedEmployeeName, user.DisplayName, user.Username)))).ToList();
            }
        }
        private static bool ActiveJob(Job job) => !IsAny(FirstNonEmpty(job.PipelineStatus, job.Status), "Completed", "Closed", "Cancelled");

        private Label SoftText(Control parent, string text, int x, int y, int width, int height, float size = 9f, bool bold = false)
        {
            var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(Math.Max(1, width), height),
                Font = new Font(LanguageManager.GetUiFontFamily(), size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = SoftInk, BackColor = Color.Transparent, AutoEllipsis = true, UseMnemonic = false };
            parent.Controls.Add(label);
            return label;
        }

        private Button SoftButton(Control parent, string text, int x, int y, int width, Action click, bool primary = false)
        {
            Button button = DashboardVisuals.Action(text, width, primary ? DashboardVisuals.Blue : Color.White,
                primary ? Color.White : DashboardVisuals.Ink);
            button.Location = new Point(x, y);
            button.Tag = "FIXED_WIDTH";
            button.Click += (s, e) => click();
            parent.Controls.Add(button);
            button.BringToFront();
            return button;
        }

        private Panel SoftCard(string name, string title, string subtitle, int width, int height)
        {
            Panel card = CardPanel(width, height);
            card.Name = name;
            ModernIconKind icon = name == "PriorityInboxCard" ? ModernIconKind.Alert :
                name == "TeamTodayCard" ? ModernIconKind.Technician :
                name == "ComingUpCard" || name == "TodayJobsCard" ? ModernIconKind.Calendar :
                name == "CollectionsCard" ? ModernIconKind.Money : ModernIconKind.Activity;
            DispatchIcon(card, icon, 18, 17);
            SoftText(card, title, 56, 14, width - 210, 26, 12f, true);
            SoftText(card, subtitle, 56, 42, width - 78, 22, 9f).ForeColor = DS.Slate500;
            return card;
        }

        private void Preview(Control card, string title, string description, IEnumerable<string> rows)
        {
            var all = rows.Where(row => !string.IsNullOrWhiteSpace(row)).ToList();
            string text = all.Count == 0 ? "No matching records." : string.Join("\n", all.Take(4));
            if (all.Count > 4) text += "\n+ " + (all.Count - 4) + " more records";
            _dashboardPreview.Attach(card, title, description, text);
        }

        private void AddSoftDashboard()
        {
            _actionSnapshot = BuildActionSnapshot();
            _actionSnapshot.Items = _actionSnapshot.Items.Where(i => DashboardCanView(
                IsAny(i.SourceModule, "Jobs", "ServiceDesk") ? "WorkOrders" : i.SourceModule)).ToList();
            int width = ContentWidth();
            AddSoftMetrics(width);
            int gap = 14;
            bool stacked = width < 1100;
            int mainWidth = stacked ? width : (int)((width - gap) * 0.555);
            int sideWidth = stacked ? width : width - mainWidth - gap;
            Panel inbox = BuildSoftInbox(sideWidth);
            Panel team = BuildSoftTeam(mainWidth);
            Panel coming = BuildSoftComing(sideWidth);
            Panel jobs = BuildSoftJobs(mainWidth);
            jobs.Height = Math.Max(jobs.Height, inbox.Height);
            if (stacked)
            {
                _root.Controls.Add(jobs); _root.Controls.Add(inbox);
                _root.Controls.Add(team); _root.Controls.Add(coming);
            }
            else
            {
                var body = new Panel { Width = width, Height = jobs.Height + 14 + team.Height,
                    BackColor = SoftCanvas, Margin = new Padding(0, 0, 0, 14), Tag = "NO_DASHBOARD_RESIZE" };
                jobs.Location = Point.Empty;
                inbox.Location = new Point(mainWidth + gap, 0);
                team.Width = mainWidth;
                team.Location = new Point(0, jobs.Height + gap);
                coming.Location = new Point(mainWidth + gap, jobs.Height + gap);
                body.Controls.AddRange(new Control[] { jobs, inbox, team, coming });
                _root.Controls.Add(body);
            }
            _root.Controls.Add(BuildSoftCollections(width));
            AddSoftActivity(width);
            CrashProtectionService.AttachToTree(this);
        }

        private void DispatchIcon(Control parent, ModernIconKind icon, int x, int y)
        {
            var picture = new PictureBox { Location = new Point(x, y), Size = new Size(28, 28),
                Image = ModernIconSystem.IconBitmap(icon, 24, SoftBlue), SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent };
            picture.Disposed += (sender, args) => picture.Image?.Dispose();
            parent.Controls.Add(picture);
        }

        private void AddSoftMetrics(int width)
        {
            bool twoRows = width < 1050;
            int count = twoRows ? 2 : 4;
            int cardWidth = width / count;
            var row = new Panel { Width = width, Height = twoRows ? 216 : 108, BackColor = Color.FromArgb(237, 246, 255), Margin = new Padding(0, 0, 0, 14), Tag = "DISPATCH_PRESENTATION NO_DASHBOARD_RESIZE", Name = "DispatchSummaryBand" };
            List<Job> today = DashboardJobs.Where(j => j.ScheduledDate.Date == DateTime.Today && ActiveJob(j)).ToList();
            List<ActionCenterItem> risk = _actionSnapshot.Items.Where(i => i.SourceModule == "ServiceDesk" && i.SlaDeadline.HasValue && i.Priority >= ActionCenterPriority.Urgent).ToList();
            var overdue = DashboardCanView("Invoices") ? _invoices.Where(i => i.DueDate.Date < DateTime.Today && i.BalanceDue > 0 && !IsPaid(i.PaymentStatus)).ToList() : new List<Invoice>();
            var low = DashboardCanView("Inventory") ? _inventory.Where(i => i.AvailableStock <= 0 || i.IsLowStock).ToList() : new List<StockItem>();
            Panel[] cards = {
                SoftMetric("JobsTodayCard", "Jobs today", Count(today.Count), today.Count(j => !j.AssignedEmployeeID.HasValue) + " need assignment", cardWidth, ModernIconKind.Calendar, () => OnNavigate?.Invoke(15)),
                SoftMetric("SlaRiskCard", "SLA at risk", Count(risk.Count), "Review service priorities", cardWidth, ModernIconKind.Alert, () => { _actionFilter = "SLA risk"; _inboxPage = 1; BuildShell(); }),
                SoftMetric("ReceivablesCard", "Overdue receivables", DashboardCanView("Invoices") ? Money(overdue.Sum(i => i.BalanceDue)) : "Restricted", overdue.Count + " invoices", cardWidth, ModernIconKind.Invoice, () => OnNavigate?.Invoke(3)),
                SoftMetric("LowStockCard", "Low stock", DashboardCanView("Inventory") ? Count(low.Count) : "Restricted", "Review inventory", cardWidth, ModernIconKind.Inventory, () => OnNavigate?.Invoke(11)) };
            Preview(cards[0], "Jobs today", "Active jobs scheduled today. " + today.Count + " matching jobs.", today.Select(j => j.JobNumber + " · " + j.ClientName + " · " + Safe(j.AssignedEmployeeName, "Unassigned")));
            Preview(cards[1], "SLA at risk", "Urgent or critical service incidents with an SLA deadline. " + risk.Count + " matching incidents.", risk.Select(i => i.Reference + " · " + i.ClientName + "\n" + i.PriorityReason));
            Preview(cards[2], "Overdue receivables", "Unpaid invoice balances past their due date. Total: " + Money(overdue.Sum(i => i.BalanceDue)), overdue.Select(i => i.InvoiceNumber + " · " + i.ClientName + " · " + Money(i.BalanceDue)));
            Preview(cards[3], "Low stock", "Items at the reorder level or with no available stock. " + low.Count + " matching items.", low.Select(i => i.ItemName + " · " + i.AvailableStock.ToString("N2") + " " + i.Unit + " available"));
            for (int i = 0; i < cards.Length; i++) { cards[i].Location = new Point((i % count) * cardWidth, (i / count) * 108); row.Controls.Add(cards[i]); }
            cards[0].Enabled = DashboardCanView("WorkOrders");
            cards[2].Enabled = DashboardCanView("Invoices"); cards[3].Enabled = DashboardCanView("Inventory");
            _root.Controls.Add(row);
        }

        private Panel SoftMetric(string name, string title, string value, string subtitle, int width, ModernIconKind icon, Action click)
        {
            var card = new Panel { Size = new Size(width, 108), Name = name,
                BackColor = Color.Transparent, Tag = "DISPATCH_PRESENTATION NO_DASHBOARD_RESIZE",
                Cursor = Cursors.Hand, TabStop = true };
            DispatchIcon(card, icon, 22, 28);
            SoftText(card, value, 66, 13, width - 80, 34, 18f, true).Name = "MetricValue";
            SoftText(card, title, 66, 48, width - 80, 23, 9.5f, true).Name = "MetricTitle";
            SoftText(card, subtitle, 66, 74, width - 80, 22, 8.5f).ForeColor = DS.Slate500;
            card.Paint += (sender, args) => { using (var pen = new Pen(Color.FromArgb(216, 230, 245)))
                args.Graphics.DrawLine(pen, width - 1, 20, width - 1, 88); };
            foreach (Control child in card.Controls) child.Click += (s, e) => click();
            card.Click += (s, e) => click();
            card.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; click(); } };
            return card;
        }

        private Panel BuildSoftInbox(int width)
        {
            Panel card = SoftCard("PriorityInboxCard", "Needs attention", "Your work, ranked by urgency", width, 330);
            string[] filters = { "All", "Mine", "Critical", "Today", "Overdue", "Upcoming", "Waiting", "SLA risk" };
            var filterMenu = new ContextMenuStrip();
            Button filterButton = SoftButton(card, _actionFilter + " ▾", width - 130, 16, 112, () => { });
            filterButton.Name = "InboxFilterButton";
            filterButton.ContextMenuStrip = filterMenu;
            foreach (string filter in filters)
            {
                var entry = new ToolStripMenuItem(filter) { Checked = filter == _actionFilter };
                entry.Click += (sender, args) => { _actionFilter = filter; _inboxPage = 1; BeginInvoke((Action)BuildShell); };
                filterMenu.Items.Add(entry);
            }
            filterButton.Click += (sender, args) => filterMenu.Show(filterButton, new Point(0, filterButton.Height));
            filterButton.Disposed += (sender, args) => filterMenu.Dispose();
            var actions = FilterActions(_actionSnapshot.Items).OrderByDescending(i => i.PriorityScore).ThenBy(i => i.DueAt ?? DateTime.MaxValue).ToList();
            var pager = new Controls.GlobalPaginationControl { Location = new Point(18, 273), Width = width - 36 };
            pager.SetState(_inboxPage, actions.Count, _inboxPageSize);
            _inboxPage = pager.CurrentPage;
            pager.PageChanged += (s, e) => { _inboxPage = pager.CurrentPage; BeginInvoke((Action)BuildShell); };
            pager.PageSizeChanged += (s, e) => { _inboxPageSize = pager.PageSize; _inboxPage = 1; BeginInvoke((Action)BuildShell); };
            card.Height = Math.Max(pager.Bottom + 10, width < 650 ? 380 : 330);
            card.Controls.Add(pager);
            var rows = new Panel { Location = new Point(18, 76), Width = width - 36, Height = 191, AutoScroll = true, BackColor = Color.White, Tag = "DISPATCH_PRESENTATION" };
            card.Controls.Add(rows);
            var visibleActions = actions.Skip(pager.Skip).Take(pager.PageSize).ToList();
            if (actions.Count == 0) SoftText(rows, "You're clear here — no matching work items.", 0, 25, rows.Width, 40);
            for (int i = 0; i < visibleActions.Count; i++)
            {
                ActionCenterItem item = visibleActions[i];
                Panel row = new Panel { Width = rows.Width - (_inboxPageSize > 3 ? SystemInformation.VerticalScrollBarWidth : 0), Height = 62, BackColor = Color.White };
                row.Location = new Point(0, i * 64);
                DispatchIcon(row, ActionIcon(item), 0, 12);
                Label priority = SoftText(row, item.Priority.ToString(), 36, 13, 68, 24, 8f, true);
                priority.Tag = "DISPATCH_PRESENTATION";
                priority.ForeColor = PriorityColor(item);
                priority.BackColor = Blend(PriorityColor(item), .93f);
                SoftText(row, item.Title, 116, 1, row.Width - 256, 18, 8.8f, true);
                SoftText(row, ActionContext(item), 116, 20, row.Width - 256, 16, 8.1f).ForeColor = DS.Slate500;
                SoftText(row, item.PriorityReason, 116, 37, row.Width - 256, 16, 7.8f).ForeColor = PriorityColor(item);
                Button action = SoftButton(row, item.SuggestedAction, row.Width - 130, 10, 130, () => { });
                action.Click += async (s, e) => await OpenActionAsync(item, action);
                rows.Controls.Add(row);
            }
            Preview(card, "Priority inbox", actions.Count + " matching work items, ranked by urgency. Choose a filter to narrow your work.", actions.Select(i => i.Title + " · " + i.Reference + "\n" + i.PriorityReason));
            return card;
        }

        private Panel BuildSoftTeam(int width)
        {
            Panel card = SoftCard("TeamTodayCard", "Team today", "Recorded job assignments for today", width, 151);
            var active = DashboardJobs.Where(j => ActiveJob(j) && j.ScheduledDate.Date == DateTime.Today && j.AssignedEmployeeID.HasValue).ToList();
            var groups = active.GroupBy(j => j.AssignedEmployeeID.Value).OrderBy(g => g.First().AssignedEmployeeName).ToList();
            for (int i = 0; i < Math.Min(2, groups.Count); i++)
            {
                string name = Safe(groups[i].First().AssignedEmployeeName, "Technician #" + groups[i].Key);
                SoftText(card, Initials(name), 18, 69 + i * 32, 34, 27, 9f, true).ForeColor = SoftBlue;
                SoftText(card, name, 60, 65 + i * 32, width - 78, 20, 9f, true);
                SoftText(card, groups[i].Count() + " active job(s) · " + groups[i].First().SiteName, 60, 84 + i * 32, width - 78, 18, 8f).ForeColor = DS.Slate500;
            }
            if (groups.Count == 0) SoftText(card, "No recorded assignments today.", 18, 85, width - 36, 36);
            if (DashboardCanView("GeoIntelligence")) SoftButton(card, "Open dispatch", width - 148, 16, 130, () => OnNavigate?.Invoke(14));
            Preview(card, "Team today", "Today's active jobs grouped by assigned technician. Attendance and availability are not inferred.", groups.Select(g => Safe(g.First().AssignedEmployeeName, "Technician #" + g.Key) + " · " + g.Count() + " jobs\n" + string.Join(", ", g.Select(j => j.JobNumber))));
            return card;
        }

        private Panel BuildSoftComing(int width)
        {
            var items = _actionSnapshot.Items.Where(i => i.Bucket == ActionCenterBucket.Upcoming || i.Bucket == ActionCenterBucket.Waiting).OrderBy(i => i.DueAt ?? DateTime.MaxValue).ToList();
            Panel card = SoftCard("ComingUpCard", "Coming up", "Next obligations and follow-ups", width, 151);
            for (int i = 0; i < Math.Min(2, items.Count); i++)
            {
                var item = items[i]; int y = 72 + i * 37;
                SoftText(card, item.Title, 18, y, width - 134, 20, 8.5f, true);
                SoftText(card, FirstNonEmpty(item.ClientName, item.Reference) + " · " + (item.DueAt.HasValue ? item.DueAt.Value.ToString("dd/MM/yyyy") : "Waiting"), 18, y + 21, width - 134, 18, 8f).ForeColor = DS.Slate500;
                Button action = SoftButton(card, "Review", width - 98, y, 80, () => { });
                action.Click += async (s, e) => await OpenActionAsync(item, action);
            }
            if (items.Count == 0) SoftText(card, "No upcoming obligations.", 18, 85, width - 36, 32);
            Preview(card, "Coming up", items.Count + " upcoming or waiting items, ordered by due date.", items.Select(i => i.Title + " · " + ActionContext(i) + "\n" + DueText(i)));
            return card;
        }

        private Panel BuildSoftJobs(int width)
        {
            var jobs = DashboardJobs.Where(j => j.ScheduledDate.Date == DateTime.Today && ActiveJob(j)).OrderBy(j => j.ScheduledDate).ToList();
            Panel card = SoftCard("TodayJobsCard", "Today's schedule", "Active jobs scheduled for today", width, 330);
            if (DashboardCanView("WorkOrders")) SoftButton(card, "View all jobs", width - 138, 16, 120, () => OnNavigate?.Invoke(15));
            var schedule = new Panel { Location = new Point(18, 78), Size = new Size(width - 36, 234), AutoScroll = true, BackColor = Color.White, Tag = "DISPATCH_PRESENTATION" };
            card.Controls.Add(schedule);
            schedule.Paint += (sender, args) =>
            {
                using (var pen = new Pen(Color.FromArgb(219, 229, 241)))
                using (var brush = new SolidBrush(SoftBlue))
                {
                    for (int n = 0; n < jobs.Count; n++)
                    {
                        int y = n * 96 + schedule.AutoScrollPosition.Y;
                        args.Graphics.DrawLine(pen, 67, y + 15, 67, y + 94);
                        args.Graphics.FillEllipse(brush, 63, y + 10, 8, 8);
                        args.Graphics.DrawLine(pen, 78, y + 94, schedule.Width - 8, y + 94);
                    }
                }
            };
            for (int i = 0; i < jobs.Count; i++)
            {
                Job job = jobs[i]; int y = i * 96;
                int rowWidth = schedule.Width - (jobs.Count > 2 ? SystemInformation.VerticalScrollBarWidth : 0);
                SoftText(schedule, job.ScheduledDate.ToString("HH:mm"), 0, y + 8, 62, 28, 11f, true);
                SoftText(schedule, job.JobNumber, 86, y + 5, rowWidth - 360, 25, 10f, true);
                SoftText(schedule, job.ClientName, 86, y + 31, rowWidth - 360, 23, 10f, true).ForeColor = SoftBlue;
                SoftText(schedule, Safe(job.SiteName, "Site not specified"), 86, y + 60, rowWidth - 360, 22, 9f).ForeColor = DS.Slate500;
                bool assigned = job.AssignedEmployeeID.HasValue;
                var status = SoftText(schedule, assigned ? "Assigned" : "Unassigned", rowWidth - 265, y + 10, 110, 26, 9f, true);
                status.Tag = "DISPATCH_PRESENTATION";
                status.BackColor = assigned ? Color.FromArgb(221, 249, 232) : Color.FromArgb(238, 242, 247);
                status.ForeColor = assigned ? Color.FromArgb(21, 128, 61) : DS.Slate500;
                SoftText(schedule, assigned ? Safe(job.AssignedEmployeeName, "Assigned technician") : "Unassigned · needs dispatch", rowWidth - 265, y + 48, 160, 22, 9f).ForeColor = assigned ? SoftBlue : Color.FromArgb(180, 83, 9);
                SoftButton(schedule, assigned ? "Open" : "Assign", rowWidth - 82, y + 22, 82, () => NavigationHelper.OpenRecord(this, "Jobs", job.JobID), !assigned);
            }
            if (jobs.Count == 0) SoftText(schedule, DashboardCanView("WorkOrders") ? "No active jobs scheduled today." : "You do not have access to jobs.", 0, 30, width - 72, 40, 10f);
            Preview(card, "Today's jobs", jobs.Count + " active jobs scheduled today, ordered by scheduled time.", jobs.Select(j => j.JobNumber + " · " + j.ClientName + "\n" + j.ScheduledDate.ToString("hh:mm tt") + " · " + Safe(j.AssignedEmployeeName, "Unassigned")));
            return card;
        }

        private Panel BuildSoftCollections(int width)
        {
            var payments = DashboardCanView("Payments") ? _payments.Where(p => IsThisMonth(p.PaymentDate)).ToList() : new List<Payment>();
            var invoices = DashboardCanView("Invoices") ? _invoices.Where(i => IsThisMonth(i.InvoiceDate)).ToList() : new List<Invoice>();
            decimal received = payments.Sum(p => p.AmountPaid), pending = invoices.Sum(i => Math.Max(0, i.BalanceDue));
            Panel card = SoftCard("CollectionsCard", "Collections & receivables", "A clear view of outstanding balances and this month’s receipts", width, 156);
            decimal overdue = DashboardCanView("Invoices") ? _invoices.Where(i => i.DueDate.Date < DateTime.Today && i.BalanceDue > 0 && !IsPaid(i.PaymentStatus)).Sum(i => i.BalanceDue) : 0;
            int column = (width - 36) / 3;
            string[] values = { DashboardCanView("Invoices") ? Money(overdue) : "Restricted", DashboardCanView("Payments") ? Money(received) : "Restricted", DashboardCanView("Invoices") ? Money(pending) : "Restricted" };
            string[] captions = { "Overdue invoice balances", "Payments received this month", "Pending on this month's invoices" };
            for (int i = 0; i < 3; i++)
            {
                SoftText(card, values[i], 20 + i * column, 81, column - 20, 32, 17f, true).ForeColor = i == 0 ? SoftBlue : SoftInk;
                SoftText(card, captions[i], 20 + i * column, 119, column - 20, 24, 9f).ForeColor = DS.Slate500;
            }
            // These are different cohorts; do not present a misleading paid/pending percentage.
            if (DashboardCanView("Invoices")) SoftButton(card, "Receivables", width - 138, 12, 120, () => OnNavigate?.Invoke(3));
            Preview(card, "Collections this month", "Payments received this month: " + Money(received) + ". Pending balances on invoices issued this month: " + Money(pending) + ".", payments.Select(p => p.PaymentNumber + " · " + p.ClientName + " · " + Money(p.AmountPaid)));
            return card;
        }

        private void AddSoftActivity(int width)
        {
            var allowedJobIds = new HashSet<int>(DashboardJobs.Select(j => j.JobID));
            var items = BuildRecentItems().Where(i => DashboardCanView(RecentPermissionModule(i.Module)) &&
                (i.Module != "Job" || allowedJobIds.Contains(i.RecordId))).Take(6).ToList();
            Panel card = CardPanel(width, 64);
            card.Name = "RecentActivityCard";
            DispatchIcon(card, ModernIconKind.Activity, 18, 18);
            SoftText(card, "Recent activity", 56, 21, 142, 25, 10f, true);
            if (items.Count == 0) SoftText(card, "No recent activity.", 210, 23, width - 228, 22);
            for (int i = 0; i < Math.Min(2, items.Count); i++)
            {
                var item = items[i]; int left = 210 + i * ((width - 228) / 2);
                Label label = SoftText(card, item.Module + " · " + item.Reference + " · " + item.PartyName, left, 23, (width - 252) / 2, 28, 8.5f);
                label.Cursor = Cursors.Hand; label.Click += (s, e) => OpenRecentActivityItem(item);
            }
            Preview(card, "Recent activity", "Most recent records from the modules you can view.", items.Select(i => i.Module + " · " + i.Reference + " · " + i.Status + "\n" + i.ActivityDate.ToString("dd/MM/yyyy hh:mm tt")));
            _root.Controls.Add(card);
        }

        private static string RecentPermissionModule(string module)
        {
            switch (module)
            {
                case "Job": return "WorkOrders";
                case "Invoice": return "Invoices";
                case "Quotation": return "Quotations";
                case "Purchase": return "Purchases";
                default: return module;
            }
        }
    }
}
