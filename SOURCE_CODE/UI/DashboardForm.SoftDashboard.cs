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
        private static readonly Color SoftCanvas = Color.FromArgb(246, 247, 245);
        private static readonly Color SoftBorder = Color.FromArgb(227, 231, 228);
        private static readonly Color SoftInk = Color.FromArgb(42, 55, 66);
        private static readonly Color SoftBlue = Color.FromArgb(69, 110, 147);
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
            Button button = SecondaryButton(text, x, y, width, 34);
            button.Tag = "FIXED_WIDTH";
            button.Font = new Font(LanguageManager.GetUiFontFamily(), 8.5f);
            button.BackColor = primary ? SoftBlue : Color.White;
            button.ForeColor = primary ? Color.White : SoftBlue;
            button.FlatAppearance.BorderColor = primary ? SoftBlue : SoftBorder;
            button.Padding = new Padding(5, 0, 5, 0);
            DS.Rounded(button, 6);
            button.Click += (s, e) => click();
            parent.Controls.Add(button);
            button.BringToFront();
            return button;
        }

        private Panel SoftCard(string name, string title, string subtitle, int width, int height)
        {
            Panel card = CardPanel(width, height);
            card.Name = name;
            SoftText(card, title, 18, 14, width - 36, 25, 11f, true);
            SoftText(card, subtitle, 18, 42, width - 36, 20, 8.5f).ForeColor = DS.Slate500;
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
            AddSoftToolbar(width);
            AddSoftMetrics(width);
            int gap = 14;
            bool stacked = width < 1000;
            int mainWidth = stacked ? width : (int)((width - gap) * 0.65);
            int sideWidth = stacked ? width : width - mainWidth - gap;
            Panel inbox = BuildSoftInbox(mainWidth);
            Panel team = BuildSoftTeam(sideWidth);
            Panel coming = BuildSoftComing(sideWidth);
            Panel jobs = BuildSoftJobs(mainWidth);
            Panel collections = BuildSoftCollections(sideWidth);
            if (stacked)
            {
                _root.Controls.Add(inbox); _root.Controls.Add(jobs);
                _root.Controls.Add(team); _root.Controls.Add(coming); _root.Controls.Add(collections);
            }
            else
            {
                var body = new Panel { Width = width, Height = inbox.Height + 14 + jobs.Height, BackColor = SoftCanvas, Margin = new Padding(0, 0, 0, 12), Tag = "NO_DASHBOARD_RESIZE" };
                inbox.Location = Point.Empty;
                jobs.Location = new Point(0, inbox.Height + 14);
                team.Location = new Point(mainWidth + gap, 0);
                coming.Location = new Point(mainWidth + gap, 220);
                collections.Location = new Point(mainWidth + gap, 382);
                body.Controls.AddRange(new Control[] { inbox, jobs, team, coming, collections });
                _root.Controls.Add(body);
            }
            AddSoftActivity(width);
            CrashProtectionService.AttachToTree(this);
        }

        private void AddSoftToolbar(int width)
        {
            var bar = new Panel { Width = width, Height = 40, BackColor = SoftCanvas, Margin = new Padding(0, 0, 0, 10) };
            SoftText(bar, DateTime.Today.ToString("dddd, dd MMMM yyyy"), 0, 9, Math.Max(160, width - 620), 24, 9f);
            var commands = new List<Tuple<string, string, Action>>();
            if (DashboardCan("WorkOrders", "Create")) commands.Add(Tuple.Create("+ New job", "New job", (Action)(() => OnShortcut?.Invoke(ShortcutNewJob))));
            if (DashboardCan("Quotations", "Create")) commands.Add(Tuple.Create("Quotation", "Quotation", (Action)(() => OnShortcut?.Invoke(ShortcutNewQuotation))));
            if (DashboardCan("Invoices", "Create")) commands.Add(Tuple.Create("Invoice", "Invoice", (Action)(() => OnShortcut?.Invoke(ShortcutNewInvoice))));
            if (DashboardCan("Payments", "Create")) commands.Add(Tuple.Create("Record payment", "Payment", (Action)(() => OnNavigate?.Invoke(4))));
            int x = Math.Max(210, width - commands.Count * 134);
            foreach (var command in commands) { SoftButton(bar, command.Item1, x, 2, 126, command.Item3, command.Item2 == "New job"); x += 134; }
            if (width < 800 && commands.Count > 2)
            {
                bar.Height = 92;
                int i = 0;
                foreach (Button b in bar.Controls.OfType<Button>()) { b.Location = new Point((i++ % 4) * 142, 43); }
            }
            _root.Controls.Add(bar);
        }

        private void AddSoftMetrics(int width)
        {
            bool twoRows = width < 850;
            int count = twoRows ? 2 : 4;
            int cardWidth = (width - (count - 1) * 12) / count;
            var row = new Panel { Width = width, Height = twoRows ? 202 : 96, BackColor = SoftCanvas, Margin = new Padding(0, 0, 0, 14) };
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
            for (int i = 0; i < cards.Length; i++) { cards[i].Location = new Point((i % count) * (cardWidth + 12), (i / count) * 106); row.Controls.Add(cards[i]); }
            cards[0].Enabled = DashboardCanView("WorkOrders");
            cards[2].Enabled = DashboardCanView("Invoices"); cards[3].Enabled = DashboardCanView("Inventory");
            _root.Controls.Add(row);
        }

        private Panel SoftMetric(string name, string title, string value, string subtitle, int width, ModernIconKind icon, Action click)
        {
            Panel card = CardPanel(width, 96); card.Name = name; card.Cursor = Cursors.Hand; card.TabStop = true;
            Label badge = ModernIconSystem.Badge(icon, 38, Color.FromArgb(239, 244, 246), SoftBlue, 9);
            badge.Location = new Point(16, 30); card.Controls.Add(badge);
            SoftText(card, title, 68, 15, width - 84, 22, 8.8f).ForeColor = DS.Slate600;
            SoftText(card, value, 68, 39, width - 84, 31, value.Length > 10 ? 16f : 19f, true);
            SoftText(card, subtitle, 68, 71, width - 84, 20, 8.2f).ForeColor = DS.Slate500;
            foreach (Control child in card.Controls) child.Click += (s, e) => click();
            card.Click += (s, e) => click();
            card.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; click(); } };
            return card;
        }

        private Panel BuildSoftInbox(int width)
        {
            Panel card = SoftCard("PriorityInboxCard", "Priority inbox", "Items that need your attention", width, 274);
            string[] filters = { "All", "Mine", "Critical", "Today", "Overdue", "Upcoming", "Waiting" };
            int x = 18;
            foreach (string filter in filters)
            {
                Label chip = FilterChip(filter, x, 66, filter == _actionFilter);
                chip.BackColor = filter == _actionFilter ? Color.FromArgb(230, 239, 245) : Color.White;
                chip.ForeColor = filter == _actionFilter ? SoftBlue : DS.Slate600;
                chip.Height = 28;
                chip.Click += (s, e) => { _actionFilter = filter; _inboxPage = 1; BeginInvoke((Action)BuildShell); };
                card.Controls.Add(chip); x += chip.Width + 3;
            }
            var actions = FilterActions(_actionSnapshot.Items).OrderByDescending(i => i.PriorityScore).ThenBy(i => i.DueAt ?? DateTime.MaxValue).ToList();
            var pager = new Controls.GlobalPaginationControl { Location = new Point(18, 273), Width = width - 36 };
            pager.SetState(_inboxPage, actions.Count, _inboxPageSize);
            _inboxPage = pager.CurrentPage;
            pager.PageChanged += (s, e) => { _inboxPage = pager.CurrentPage; BeginInvoke((Action)BuildShell); };
            pager.PageSizeChanged += (s, e) => { _inboxPageSize = pager.PageSize; _inboxPage = 1; BeginInvoke((Action)BuildShell); };
            card.Height = pager.Bottom + 10;
            card.Controls.Add(pager);
            var rows = new Panel { Location = new Point(18, 102), Width = width - 36, Height = 165, AutoScroll = true, BackColor = Color.White };
            card.Controls.Add(rows);
            var visibleActions = actions.Skip(pager.Skip).Take(pager.PageSize).ToList();
            if (actions.Count == 0) SoftText(rows, "You're clear here — no matching work items.", 0, 25, rows.Width, 40);
            for (int i = 0; i < visibleActions.Count; i++)
            {
                ActionCenterItem item = visibleActions[i];
                Panel row = new Panel { Width = rows.Width - (_inboxPageSize > 3 ? SystemInformation.VerticalScrollBarWidth : 0), Height = 54, BackColor = Color.White };
                row.Location = new Point(0, i * 55);
                Label badge = ModernIconSystem.Badge(ActionIcon(item), 28, Blend(PriorityColor(item), .94f), PriorityColor(item), 7);
                badge.Location = new Point(0, 10); row.Controls.Add(badge);
                SoftText(row, item.Title, 40, 1, row.Width - 180, 18, 8.8f, true);
                SoftText(row, ActionContext(item), 40, 20, row.Width - 180, 16, 8.1f).ForeColor = DS.Slate500;
                SoftText(row, item.PriorityReason, 40, 37, row.Width - 180, 16, 7.8f).ForeColor = PriorityColor(item);
                Button action = SoftButton(row, item.SuggestedAction, row.Width - 130, 10, 130, () => { });
                action.Click += async (s, e) => await OpenActionAsync(item, action);
                rows.Controls.Add(row);
            }
            Preview(card, "Priority inbox", actions.Count + " matching work items, ranked by urgency. Choose a filter to narrow your work.", actions.Select(i => i.Title + " · " + i.Reference + "\n" + i.PriorityReason));
            return card;
        }

        private Panel BuildSoftTeam(int width)
        {
            Panel card = SoftCard("TeamTodayCard", "Team today", "Recorded job assignments for today", width, 206);
            var active = DashboardJobs.Where(j => ActiveJob(j) && j.ScheduledDate.Date == DateTime.Today && j.AssignedEmployeeID.HasValue).ToList();
            var groups = active.GroupBy(j => j.AssignedEmployeeID.Value).OrderBy(g => g.First().AssignedEmployeeName).ToList();
            for (int i = 0; i < Math.Min(3, groups.Count); i++)
            {
                string name = Safe(groups[i].First().AssignedEmployeeName, "Technician #" + groups[i].Key);
                SoftText(card, Initials(name), 18, 69 + i * 32, 34, 27, 9f, true).ForeColor = SoftBlue;
                SoftText(card, name, 60, 65 + i * 32, width - 78, 20, 9f, true);
                SoftText(card, groups[i].Count() + " active job(s) · " + groups[i].First().SiteName, 60, 84 + i * 32, width - 78, 18, 8f).ForeColor = DS.Slate500;
            }
            if (groups.Count == 0) SoftText(card, "No recorded assignments today.", 18, 85, width - 36, 36);
            if (DashboardCanView("GeoIntelligence")) SoftButton(card, "Open dispatch", width - 148, 167, 130, () => OnNavigate?.Invoke(14));
            Preview(card, "Team today", "Today's active jobs grouped by assigned technician. Attendance and availability are not inferred.", groups.Select(g => Safe(g.First().AssignedEmployeeName, "Technician #" + g.Key) + " · " + g.Count() + " jobs\n" + string.Join(", ", g.Select(j => j.JobNumber))));
            return card;
        }

        private Panel BuildSoftComing(int width)
        {
            var items = _actionSnapshot.Items.Where(i => i.Bucket == ActionCenterBucket.Upcoming || i.Bucket == ActionCenterBucket.Waiting).OrderBy(i => i.DueAt ?? DateTime.MaxValue).ToList();
            Panel card = SoftCard("ComingUpCard", "Coming up", "Next obligations and follow-ups", width, 148);
            for (int i = 0; i < Math.Min(2, items.Count); i++)
            {
                var item = items[i]; int y = 65 + i * 40;
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
            Panel card = SoftCard("TodayJobsCard", "Today's jobs", "Scheduled work for today", width, 230);
            if (DashboardCanView("WorkOrders")) SoftButton(card, "View all jobs", width - 138, 16, 120, () => OnNavigate?.Invoke(15));
            int partyX = (int)(width * .20), techX = (int)(width * .50), stateX = (int)(width * .72);
            SoftText(card, "Job / Time", 18, 73, partyX - 26, 20, 8f);
            SoftText(card, "Client & site", partyX, 73, techX - partyX - 12, 20, 8f);
            SoftText(card, "Technician", techX, 73, stateX - techX - 12, 20, 8f);
            SoftText(card, "Status", stateX, 73, width - stateX - 88, 20, 8f);
            for (int i = 0; i < Math.Min(3, jobs.Count); i++)
            {
                Job job = jobs[i]; int y = 94 + i * 43;
                SoftText(card, job.JobNumber, 18, y, partyX - 26, 20, 8.5f, true);
                SoftText(card, job.ScheduledDate.ToString("hh:mm tt"), 18, y + 20, partyX - 26, 17, 7.8f).ForeColor = DS.Slate500;
                SoftText(card, job.ClientName, partyX, y, techX - partyX - 12, 20, 8.5f);
                SoftText(card, job.SiteName, partyX, y + 20, techX - partyX - 12, 17, 7.8f).ForeColor = DS.Slate500;
                SoftText(card, Safe(job.AssignedEmployeeName, "Unassigned"), techX, y, stateX - techX - 12, 35, 8.3f);
                SoftText(card, Safe(FirstNonEmpty(job.PipelineStatus, job.Status), "Scheduled"), stateX, y, width - stateX - 88, 36, 8f).ForeColor = SoftBlue;
                SoftButton(card, "Open", width - 80, y, 62, () => NavigationHelper.OpenRecord(this, "Jobs", job.JobID));
            }
            if (jobs.Count == 0) SoftText(card, DashboardCanView("WorkOrders") ? "No active jobs scheduled today." : "You do not have access to jobs.", 18, 115, width - 36, 35);
            Preview(card, "Today's jobs", jobs.Count + " active jobs scheduled today, ordered by scheduled time.", jobs.Select(j => j.JobNumber + " · " + j.ClientName + "\n" + j.ScheduledDate.ToString("hh:mm tt") + " · " + Safe(j.AssignedEmployeeName, "Unassigned")));
            return card;
        }

        private Panel BuildSoftCollections(int width)
        {
            var payments = DashboardCanView("Payments") ? _payments.Where(p => IsThisMonth(p.PaymentDate)).ToList() : new List<Payment>();
            var invoices = DashboardCanView("Invoices") ? _invoices.Where(i => IsThisMonth(i.InvoiceDate)).ToList() : new List<Invoice>();
            decimal received = payments.Sum(p => p.AmountPaid), pending = invoices.Sum(i => Math.Max(0, i.BalanceDue));
            Panel card = SoftCard("CollectionsCard", "Collections", "This month · payments and invoice balances", width, 136);
            SoftText(card, DashboardCanView("Payments") ? Money(received) : "Restricted", 18, 75, width / 2 - 24, 25, 12f, true);
            SoftText(card, DashboardCanView("Invoices") ? Money(pending) : "Restricted", width / 2, 75, width / 2 - 18, 25, 12f, true);
            SoftText(card, "Payments received", 18, 104, width / 2 - 24, 22, 8f).ForeColor = DS.Slate500;
            SoftText(card, "Pending on this month's invoices", width / 2, 104, width / 2 - 18, 32, 8f).ForeColor = DS.Slate500;
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
            Panel card = SoftCard("RecentActivityCard", "Recent activity", "Latest changes across your business", width, 102);
            if (items.Count == 0) SoftText(card, "No recent activity.", 18, 73, width - 36, 22);
            for (int i = 0; i < Math.Min(2, items.Count); i++)
            {
                var item = items[i]; int left = 18 + i * ((width - 36) / 2);
                Label label = SoftText(card, item.Module + " · " + item.Reference + " · " + item.PartyName, left, 73, (width - 60) / 2, 28, 8.5f);
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
