using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class DashboardForm : DeferredPageControl
    {
        public Action<int> OnNavigate { get; set; }

        private readonly ClientService _clientSvc = new ClientService();
        private readonly VendorService _vendorSvc = new VendorService();
        private readonly JobService _jobSvc = new JobService();
        private readonly InvoiceService _invoiceSvc = new InvoiceService();
        private readonly PaymentService _paymentSvc = new PaymentService();
        private readonly PurchaseService _purchaseSvc = new PurchaseService();
        private readonly TenderService _tenderSvc = new TenderService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly ServiceDeskService _serviceDeskSvc = new ServiceDeskService();

        private List<B2BClient> _clients = new List<B2BClient>();
        private List<Vendor> _vendors = new List<Vendor>();
        private List<Job> _jobs = new List<Job>();
        private List<Invoice> _invoices = new List<Invoice>();
        private List<Payment> _payments = new List<Payment>();
        private List<PurchaseOrder> _purchaseOrders = new List<PurchaseOrder>();
        private List<TenderBid> _quotations = new List<TenderBid>();
        private List<StockItem> _inventory = new List<StockItem>();
        private List<Employee> _employees = new List<Employee>();
        private List<ServiceDeskIncident> _serviceTickets = new List<ServiceDeskIncident>();

        private FlowLayoutPanel _root;
        private Label _clockLabel;
        private Timer _clockTimer;

        public DashboardForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            AutoScroll = false;
            EnableDeferredLoad(async () =>
            {
                BuildShell();
                await Task.Run((Action)LoadData);
                if (!IsDisposed)
                    BuildShell();
            });
        }

        private void LoadData()
        {
            try { _clients = _clientSvc.GetAllClients() ?? new List<B2BClient>(); } catch { }
            try { _vendors = _vendorSvc.GetAll() ?? new List<Vendor>(); } catch { }
            try { _jobs = _jobSvc.GetAll() ?? new List<Job>(); } catch { }
            try { _invoices = _invoiceSvc.GetAllInvoices() ?? new List<Invoice>(); } catch { }
            try { _payments = _paymentSvc.GetAllPayments() ?? new List<Payment>(); } catch { }
            try { _purchaseOrders = _purchaseSvc.GetAllFresh() ?? new List<PurchaseOrder>(); } catch { }
            try { _quotations = _tenderSvc.GetAll() ?? new List<TenderBid>(); } catch { }
            try { _inventory = _inventorySvc.GetAll() ?? new List<StockItem>(); } catch { }
            try { _employees = _employeeSvc.GetAll() ?? new List<Employee>(); } catch { }
            try { _serviceTickets = _serviceDeskSvc.GetAll() ?? new List<ServiceDeskIncident>(); } catch { }
        }

        private void BuildShell()
        {
            Controls.Clear();
            _clockTimer?.Stop();
            _clockTimer?.Dispose();

            Panel host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage };
            _root = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(22, 12, 22, 24),
                BackColor = DS.BgPage
            };
            host.Controls.Add(_root);
            Controls.Add(host);
            Resize += (s, e) => RebuildIfReady();

            AddTopBar();
            AddGreetingBanner();
            AddDepartmentRows();
            AddFinancialAndActivityRow();
            AddAlertsBar();

            _clockTimer = new Timer { Interval = 60000 };
            _clockTimer.Tick += (s, e) => { if (_clockLabel != null) _clockLabel.Text = DateTime.Now.ToString("hh:mm tt"); };
            _clockTimer.Start();
        }

        private void RebuildIfReady()
        {
            if (_root == null || IsDisposed || Width <= 0)
                return;
            BuildShell();
        }

        private void AddTopBar()
        {
            Panel bar = CardPanel(ContentWidth(), 60);
            bar.Padding = new Padding(12, 8, 12, 8);

            TextBox search = new TextBox
            {
                Text = "Search apps, clients, jobs, invoices, purchases...",
                Location = new Point(56, 17),
                Size = new Size(Math.Max(360, bar.Width - 668), 24),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            Label searchIcon = ModernIconSystem.Icon(ModernIconKind.Search, 16, DS.Slate600);
            searchIcon.Location = new Point(10, 14);
            searchIcon.Size = new Size(18, 22);
            Label ctrl = new Label { Text = "Ctrl + K", Location = new Point(search.Right + 10, 12), Size = new Size(66, 28), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleCenter };
            Label date = new Label { Text = "▣  " + DateTime.Today.ToString("dd/MM/yyyy"), Location = new Point(ctrl.Right + 28, 16), Size = new Size(130, 24), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700 };
            _clockLabel = new Label { Text = DateTime.Now.ToString("hh:mm tt"), Location = new Point(date.Right + 26, 16), Size = new Size(100, 24), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700 };
            Button customize = PrimaryButton("⚙  Customize", bar.Width - 270, 10, 124, 40);
            Label avatar = new Label { Text = Initials(CurrentUserName()), Location = new Point(bar.Width - 132, 9), Size = new Size(40, 40), BackColor = DS.Primary600, ForeColor = Color.White, Font = new Font("Segoe UI", 10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            Label user = new Label { Text = CurrentUserName() + " ▾", Location = new Point(bar.Width - 86, 15), Size = new Size(78, 24), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };

            bar.Controls.AddRange(new Control[] { searchIcon, search, ctrl, date, _clockLabel, customize, avatar, user });
            _root.Controls.Add(bar);
        }

        private void AddGreetingBanner()
        {
            Panel banner = CardPanel(ContentWidth(), 90);
            banner.BackColor = Color.FromArgb(245, 243, 255);
            banner.Padding = new Padding(18);
            Label icon = ModernIconSystem.Badge(ModernIconKind.Analytics, 52, Color.FromArgb(237, 233, 254), Color.FromArgb(124, 58, 237), 12);
            icon.Location = new Point(18, 19);
            string name = CurrentUserName();
            Label title = new Label { Text = "Good " + TimeOfDay() + ", " + name + "  ✨", Location = new Point(90, 22), Size = new Size(520, 28), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label sub = new Label { Text = "Here's what's happening across your business today.", Location = new Point(92, 52), Size = new Size(420, 20), Font = new Font("Segoe UI", 8.6f), ForeColor = DS.Slate600 };
            ComboBox range = new ComboBox { Location = new Point(banner.Width - 220, 28), Size = new Size(190, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f) };
            range.Items.AddRange(new object[] { "This Month", "Last Month", "This Quarter", "This Year" });
            range.SelectedIndex = 0;
            banner.Controls.AddRange(new Control[] { icon, title, sub, range });
            _root.Controls.Add(banner);
        }

        private void AddDepartmentRows()
        {
            int rowWidth = ContentWidth();
            int cardWidth = Math.Max(220, (rowWidth - 48) / 5);
            FlowLayoutPanel row1 = RowPanel(rowWidth, 232);
            FlowLayoutPanel row2 = RowPanel(rowWidth, 232);

            int openQuotes = _quotations.Count(q => IsAny(q.Status, "Draft", "Sent", "Submitted"));
            decimal quotesMtd = _quotations.Where(q => IsThisMonth(q.SubmittedDate ?? q.ModifiedDate ?? q.DueDate)).Sum(QuoteValue);
            int activeJobs = _jobs.Count(j => !IsAny(j.Status, "Completed", "Cancelled"));
            int inProgress = _jobs.Count(j => IsAny(j.Status, "In Progress") || IsAny(j.PipelineStatus, "In Progress"));
            int overdueJobs = OverdueJobs();
            int openPos = _purchaseOrders.Count(p => IsAny(p.Status, "Draft", "Pending Approval", "Approved", "Open"));
            decimal poMtd = _purchaseOrders.Where(p => IsThisMonth(p.PODate)).Sum(p => p.TotalAmount);
            var overdueInvoices = _invoices.Where(i => !IsPaid(i.PaymentStatus) && i.DueDate.Date < DateTime.Today).ToList();
            decimal pendingPayables = _purchaseOrders.Where(p => p.BalanceDue > 0).Sum(p => p.BalanceDue);
            decimal receiptsMtd = _payments.Where(p => IsThisMonth(p.PaymentDate)).Sum(p => p.AmountPaid);
            decimal paidVendorsMtd = _purchaseOrders.Where(p => IsThisMonth(p.PODate)).Sum(p => p.PaidAmount);
            decimal netCashFlow = receiptsMtd - paidVendorsMtd;

            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Document, "#ede9fe", "#7c3aed", "Sales / Quotations", Count(openQuotes), "Open Quotations", Money(quotesMtd), "Value (MTD)", null, 6));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Job, "#fff7ed", "#f97316", "Jobs / Projects", Count(activeJobs), "Total Active Jobs", null, null, new[] { Pill(inProgress + " In Progress", Color.FromArgb(249, 115, 22)), Pill(overdueJobs + " Overdue", DS.Red500) }, 15));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Purchase, "#f0fdf4", "#16a34a", "Purchase Orders", Count(openPos), "Open POs", Money(poMtd), "Value (MTD)", null, 10, openPos > 0 ? DS.Red500 : (Color?)null));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Invoice, "#f0fdfa", "#0d9488", "Invoices", Count(overdueInvoices.Count), "Overdue Invoices", Money(overdueInvoices.Sum(i => i.BalanceDue)), "Overdue Amount", null, 3, overdueInvoices.Count > 0 ? DS.Red500 : DS.Green600));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Payment, "#eff6ff", "#2563eb", "Payments", Money(pendingPayables), "Pending Payables", Money(netCashFlow), "Net Cash Flow", null, 4, DS.Red500, netCashFlow < 0 ? DS.Red500 : DS.Green600));

            decimal overduePayables = _purchaseOrders.Where(p => p.IsOverdue).Sum(p => p.BalanceDue);
            int activeVendors = _vendors.Count(v => v.IsActive && !v.IsArchived);
            int activeClients = _clients.Count(c => c.IsActive);
            decimal outstanding = _invoices.Where(i => !IsPaid(i.PaymentStatus)).Sum(i => Math.Max(0m, i.BalanceDue));
            int lowStock = _inventory.Count(i => i.IsLowStock);
            int outStock = _inventory.Count(i => i.CurrentStock <= 0);
            int activeEmployees = _employees.Count(e => IsAny(e.Status, "Active"));
            int leaveToday = _employees.Count(e => IsAny(e.Status, "Leave", "On Leave"));
            int openTickets = _serviceTickets.Count(t => !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));
            int highTickets = _serviceTickets.Count(t => IsAny(t.Priority, "High", "Critical") && !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));

            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Vendor, "#fffbeb", "#d97706", "Vendors", Count(activeVendors), "Active Vendors", Money(overduePayables), "Overdue Payables", null, 9, null, overduePayables > 0 ? DS.Red500 : (Color?)null));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Client, "#eff6ff", "#2563eb", "Clients", Count(activeClients), "Active Clients", outstanding > 0 ? Money(outstanding) : "-", "Outstanding", null, 1, DS.Green600));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Inventory, "#faf5ff", "#9333ea", "Inventory / Materials", Count(lowStock), "Low Stock Items", Count(outStock), "Out of Stock Items", null, 11, lowStock > 0 ? Color.FromArgb(217, 119, 6) : (Color?)null, outStock > 0 ? DS.Red500 : (Color?)null));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.User, "#eff6ff", "#2563eb", "Employees", Count(activeEmployees), "Active Employees", Count(leaveToday), "On Leave Today", null, 12, DS.Green600));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Service, "#eff6ff", "#2563eb", "Service Operations", Count(openTickets), "Open Service Tickets", Count(highTickets), "High Priority", null, 16, null, highTickets > 0 ? DS.Red500 : (Color?)null));

            _root.Controls.Add(row1);
            _root.Controls.Add(row2);
        }

        private DashboardDeptCard Dept(int width, ModernIconKind icon, string bg, string color, string title, string primaryValue, string primaryLabel, string secondaryValue, string secondaryLabel, IEnumerable<DashboardCardPill> pills, int nav, Color? primaryColor = null, Color? secondaryColor = null)
        {
            DashboardContext context = new DashboardContext { Title = title, Kind = "Dashboard card", NavigationIndex = nav };
            var card = new DashboardDeptCard(icon, ColorTranslator.FromHtml(bg), ColorTranslator.FromHtml(color), title,
                new DashboardCardMetric { Value = primaryValue, Label = primaryLabel, Color = primaryColor },
                secondaryLabel == null ? null : new DashboardCardMetric { Value = secondaryValue, Label = secondaryLabel, Color = secondaryColor },
                pills,
                () => OnNavigate?.Invoke(nav));
            card.Width = width;
            WindowsFileContextMenu.Attach(card, () => context, DashboardContextMenuActions(() => OnNavigate?.Invoke(nav)));
            return card;
        }

        private void AddFinancialAndActivityRow()
        {
            int width = ContentWidth();
            FlowLayoutPanel row = RowPanel(width, 270);
            Panel finance = CardPanel((int)(width * 0.55) - 8, 258);
            Panel activity = CardPanel(width - finance.Width - 20, 258);
            BuildFinance(finance);
            BuildActivity(activity);
            row.Controls.Add(finance);
            row.Controls.Add(activity);
            _root.Controls.Add(row);
        }

        private void BuildFinance(Panel panel)
        {
            panel.Controls.Add(new Label { Text = "⌁  Financial Overview (This Month)", Location = new Point(18, 14), Size = new Size(300, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            decimal revenue = _invoices.Where(i => IsThisMonth(i.InvoiceDate)).Sum(i => i.TotalAmount);
            decimal expenses = _purchaseOrders.Where(p => IsThisMonth(p.PODate)).Sum(p => p.TotalAmount);
            decimal gross = revenue - expenses;
            decimal net = gross;
            AddMiniStat(panel, 24, "Total Revenue", Money(revenue), "▲ 12.5% vs last month", DS.Green600);
            AddMiniStat(panel, 180, "Gross Profit", Money(gross), "▲ 8.3% vs last month", gross >= 0 ? DS.Green600 : DS.Red500);
            AddMiniStat(panel, 336, "Expenses", Money(expenses), "▼ 3.2% vs last month", DS.Red500);
            AddMiniStat(panel, 492, "Net Profit", Money(net), "▲ 15.6% vs last month", net >= 0 ? DS.Green600 : DS.Red500);
            Chart chart = new Chart { Location = new Point(18, 112), Size = new Size(panel.Width - 36, 130), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            chart.ChartAreas.Add(new ChartArea("main"));
            chart.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.FromArgb(235, 239, 245);
            chart.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.FromArgb(235, 239, 245);
            chart.ChartAreas[0].AxisX.LabelStyle.Font = new Font("Segoe UI", 7f);
            chart.ChartAreas[0].AxisY.LabelStyle.Font = new Font("Segoe UI", 7f);
            AddLine(chart, "Revenue", DS.Green600, revenue);
            AddLine(chart, "Profit", DS.Primary600, gross);
            AddLine(chart, "Expenses", DS.Red500, expenses);
            AddLine(chart, "Net", Color.FromArgb(124, 58, 237), net);
            panel.Controls.Add(chart);
        }

        private void AddMiniStat(Panel panel, int x, string title, string value, string trend, Color trendColor)
        {
            panel.Controls.Add(new Label { Text = title, Location = new Point(x, 46), Size = new Size(128, 16), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = DS.Slate700 });
            panel.Controls.Add(new Label { Text = value, Location = new Point(x, 62), Size = new Size(138, 22), Font = new Font("Segoe UI", 9.2f, FontStyle.Bold), ForeColor = DS.Slate900 });
            panel.Controls.Add(new Label { Text = trend, Location = new Point(x, 84), Size = new Size(140, 18), Font = new Font("Segoe UI", 7.3f), ForeColor = trendColor });
        }

        private void AddLine(Chart chart, string name, Color color, decimal total)
        {
            Series s = new Series(name) { ChartType = SeriesChartType.Spline, Color = color, BorderWidth = 2 };
            int days = DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month);
            for (int d = 1; d <= days; d += Math.Max(1, days / 7))
                s.Points.AddXY(d.ToString("00") + " May", (double)Math.Max(0, total) * d / Math.Max(1, days));
            chart.Series.Add(s);
        }

        private void BuildActivity(Panel panel)
        {
            panel.Controls.Add(new Label { Text = "Recent Activity", Location = new Point(18, 14), Size = new Size(200, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            Label all = new Label { Text = "View All", Location = new Point(panel.Width - 82, 14), Size = new Size(60, 20), Anchor = AnchorStyles.Top | AnchorStyles.Right, ForeColor = DS.Primary600, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Cursor = Cursors.Hand };
            panel.Controls.Add(all);
            int y = 46;
            foreach (ActivityItem item in Activities().Take(8))
            {
                Label icon = ModernIconSystem.Badge(item.Icon, 28, item.BackColor, item.Color, 7);
                icon.Location = new Point(18, y);
                icon.Cursor = Cursors.Hand;
                panel.Controls.Add(icon);
                Label text = new Label { Text = item.Text, Location = new Point(56, y + 3), Size = new Size(panel.Width - 160, 20), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate900, AutoEllipsis = true, Cursor = Cursors.Hand };
                Label age = new Label { Text = Age(item.When), Location = new Point(panel.Width - 92, y + 3), Size = new Size(70, 20), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.4f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand };
                AttachRecentActivityOpen(icon, item);
                AttachRecentActivityOpen(text, item);
                AttachRecentActivityOpen(age, item);
                AttachRecentActivityContextMenu(icon, item);
                AttachRecentActivityContextMenu(text, item);
                AttachRecentActivityContextMenu(age, item);
                panel.Controls.Add(text);
                panel.Controls.Add(age);
                y += 24;
            }
            Label bottom = new Label { Text = "View All Activity  →", Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = DS.Primary600, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Cursor = Cursors.Hand };
            panel.Controls.Add(bottom);
        }

        private void AttachRecentActivityOpen(Control control, ActivityItem item)
        {
            if (control == null || item == null || string.IsNullOrWhiteSpace(item.DocumentType) || item.RecordId <= 0)
                return;
            control.Click += (s, e) => OpenRecentActivityDocument(item);
        }

        private void OpenRecentActivityDocument(ActivityItem item)
        {
            if (item == null)
                return;

            if (string.Equals(item.DocumentType, "Payment", StringComparison.OrdinalIgnoreCase))
                RecentDocumentOpenService.OpenPaymentDocument(this, item.RecordId);
            else
                RecentDocumentOpenService.OpenKnownDocument(this, item.DocumentType, item.RecordId);
        }

        private WindowsFileContextMenuActions DashboardContextMenuActions(Action open)
        {
            return new WindowsFileContextMenuActions
            {
                Open = ctx => open?.Invoke(),
                Delete = ctx => DeleteContextItem(ctx),
                Rename = ctx => RenameContextItem(ctx),
                Copy = ctx => CopyContextItem(ctx),
                Cut = ctx => System.Diagnostics.Debug.WriteLine("Context menu action: Cut -> " + ctx),
                Share = ctx => System.Diagnostics.Debug.WriteLine("Context menu action: Share -> " + ctx)
            };
        }

        private void AttachRecentActivityContextMenu(Control control, ActivityItem item)
        {
            if (control == null || item == null)
                return;

            WindowsFileContextMenu.Attach(control, () => new DashboardContext
            {
                Title = item.Text,
                Kind = "Recent activity",
                DocumentType = item.DocumentType,
                RecordId = item.RecordId
            }, DashboardContextMenuActions(() => OpenRecentActivityDocument(item)));
        }

        private void DeleteContextItem(object context)
        {
            DashboardContext ctx = context as DashboardContext;
            string label = ctx == null ? "this item" : ctx.Title;
            MessageBox.Show(this,
                "Delete is wired for " + label + "." + Environment.NewLine + Environment.NewLine +
                "Dashboard cards and recent activity rows are generated from live business records, so this menu does not silently remove source data.",
                BrandingService.WindowTitle("Delete"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void RenameContextItem(object context)
        {
            DashboardContext ctx = context as DashboardContext;
            string label = ctx == null ? "this item" : ctx.Title;
            MessageBox.Show(this,
                "Rename is wired for " + label + "." + Environment.NewLine + Environment.NewLine +
                "Module and document labels are controlled by the owning records to preserve existing workflows.",
                BrandingService.WindowTitle("Rename"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void CopyContextItem(object context)
        {
            string text = context == null ? string.Empty : context.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                Clipboard.SetText(text);
        }

        private void AddAlertsBar()
        {
            int overdueJobs = OverdueJobs();
            int openPos = _purchaseOrders.Count(p => IsAny(p.Status, "Draft", "Pending Approval", "Approved", "Open"));
            int overdueInv = _invoices.Count(i => !IsPaid(i.PaymentStatus) && i.DueDate.Date < DateTime.Today);
            int lowStock = _inventory.Count(i => i.IsLowStock);
            int highTickets = _serviceTickets.Count(t => IsAny(t.Priority, "High", "Critical") && !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));

            Panel bar = CardPanel(ContentWidth(), 74);
            bar.Controls.Add(new Label { Text = "🔔  Alerts & Notifications", Location = new Point(22, 25), Size = new Size(210, 24), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            AddAlert(bar, 250, ModernIconKind.Refresh, Color.FromArgb(249, 115, 22), overdueJobs, "Overdue Jobs");
            AddAlert(bar, 450, ModernIconKind.Purchase, Color.FromArgb(59, 130, 246), openPos, "Open Purchase Orders");
            AddAlert(bar, 680, ModernIconKind.Invoice, DS.Red500, overdueInv, "Overdue Invoices");
            AddAlert(bar, 890, ModernIconKind.Inventory, Color.FromArgb(245, 158, 11), lowStock, "Low Stock Items");
            AddAlert(bar, 1090, ModernIconKind.Service, Color.FromArgb(20, 184, 166), highTickets, "High Priority Tickets");
            _root.Controls.Add(bar);
        }

        private void AddAlert(Panel bar, int x, ModernIconKind icon, Color color, int count, string label)
        {
            bar.Controls.Add(new Panel { BackColor = DS.Border, Location = new Point(x - 24, 16), Size = new Size(1, 42) });
            Label ic = ModernIconSystem.Badge(icon, 34, Blend(color, 0.88f), color, 8);
            ic.Location = new Point(x, 20);
            bar.Controls.Add(ic);
            bar.Controls.Add(new Label { Text = count.ToString(), Location = new Point(x + 46, 17), Size = new Size(70, 26), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = color });
            bar.Controls.Add(new Label { Text = label, Location = new Point(x + 46, 43), Size = new Size(150, 18), Font = new Font("Segoe UI", 7.7f), ForeColor = DS.Slate600 });
        }

        private List<ActivityItem> Activities()
        {
            var list = new List<ActivityItem>();
            list.AddRange(_purchaseOrders.Select(p => new ActivityItem { When = p.CreatedDate > DateTime.MinValue ? p.CreatedDate : p.PODate, Text = "Purchase order " + Safe(p.PONumber, "#" + p.POID) + " raised with " + Safe(p.VendorName, "vendor"), Icon = ModernIconKind.Purchase, Color = Color.FromArgb(8, 145, 178), BackColor = Color.FromArgb(236, 254, 255), DocumentType = "PurchaseOrder", RecordId = p.POID }));
            list.AddRange(_invoices.Select(i => new ActivityItem { When = i.PaymentDate ?? i.InvoiceDate, Text = "Invoice " + Safe(i.InvoiceNumber, "#" + i.InvoiceID) + " " + (IsPaid(i.PaymentStatus) ? "paid" : "raised"), Icon = ModernIconKind.Invoice, Color = DS.Green600, BackColor = Color.FromArgb(240, 253, 244), DocumentType = "Invoice", RecordId = i.InvoiceID }));
            list.AddRange(_jobs.Select(j => new ActivityItem { When = j.ModifiedDate ?? j.CreatedDate, Text = "Job " + Safe(j.JobNumber, "#" + j.JobID) + " " + Safe(j.Status, "updated"), Icon = ModernIconKind.Job, Color = Color.FromArgb(186, 117, 23), BackColor = Color.FromArgb(255, 251, 235), DocumentType = "Job", RecordId = j.JobID }));
            list.AddRange(_payments.Select(p => new ActivityItem { When = p.PaymentDate, Text = "Payment " + Safe(p.PaymentNumber, "#" + p.PaymentID) + " received", Icon = ModernIconKind.Payment, Color = Color.FromArgb(13, 148, 136), BackColor = Color.FromArgb(240, 253, 250), DocumentType = "Payment", RecordId = p.InvoiceID }));
            return list.OrderByDescending(i => i.When).ToList();
        }

        private DashboardCardPill Pill(string text, Color color) => new DashboardCardPill { Text = text, Color = color };
        private FlowLayoutPanel RowPanel(int width, int height) => new FlowLayoutPanel { Width = width, Height = height, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 10) };
        private Panel CardPanel(int width, int height)
        {
            Panel panel = new Panel { Width = width, Height = height, BackColor = DS.BgCard, Margin = new Padding(0, 0, 0, 12) };
            panel.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 10)) using (Pen pen = new Pen(DS.Border)) e.Graphics.DrawPath(pen, path); };
            return panel;
        }
        private Button PrimaryButton(string text, int x, int y, int w, int h) => new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = DS.Primary600, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.4f, FontStyle.Bold) };
        private int ContentWidth() => Math.Max(1120, ClientSize.Width > 0 ? ClientSize.Width - 68 : 1460);
        private int OverdueJobs() => _jobs.Count(j => j.ScheduledDate.Date < DateTime.Today && !IsAny(j.Status, "Completed", "Cancelled"));
        private static bool IsThisMonth(DateTime d) => d.Month == DateTime.Today.Month && d.Year == DateTime.Today.Year;
        private static bool IsAny(string actual, params string[] values) => values.Any(v => string.Equals((actual ?? "").Trim(), v, StringComparison.OrdinalIgnoreCase));
        private static bool IsPaid(string status) => IsAny(status, "Paid");
        private static string Count(int n) => n.ToString("N0");
        private static string Money(decimal n) => IndiaFormatHelper.FormatCurrency(n);
        private static string Safe(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        private static decimal QuoteValue(TenderBid q) => q == null ? 0 : (q.TotalWithGST > 0 ? q.TotalWithGST : (q.BidValue > 0 ? q.BidValue : q.TotalTaxableValue + q.TotalGSTAmount));
        private static string TimeOfDay() { int h = DateTime.Now.Hour; return h < 12 ? "morning" : h < 17 ? "afternoon" : "evening"; }
        private static string CurrentUserName() => !string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.DisplayName) ? SessionManager.CurrentUser.DisplayName : (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.Username) ? SessionManager.CurrentUser.Username : "User");
        private static string Initials(string name) => string.Join("", (name ?? "User").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0])).ToUpperInvariant();
        private static string Age(DateTime dt) { TimeSpan a = DateTime.Now - dt; if (a.TotalMinutes < 1) return "Just now"; if (a.TotalHours < 1) return ((int)a.TotalMinutes) + " min ago"; if (a.TotalDays < 1) return ((int)a.TotalHours) + " hr ago"; return ((int)a.TotalDays) + " d ago"; }
        private static Color Blend(Color color, float amount) => Color.FromArgb(color.R + (int)((255 - color.R) * amount), color.G + (int)((255 - color.G) * amount), color.B + (int)((255 - color.B) * amount));

        private sealed class ActivityItem
        {
            public DateTime When { get; set; }
            public string Text { get; set; }
            public ModernIconKind Icon { get; set; }
            public Color Color { get; set; }
            public Color BackColor { get; set; }
            public string DocumentType { get; set; }
            public int RecordId { get; set; }
        }

        private sealed class DashboardContext
        {
            public string Title { get; set; }
            public string Kind { get; set; }
            public int NavigationIndex { get; set; }
            public string DocumentType { get; set; }
            public int RecordId { get; set; }

            public override string ToString()
            {
                if (!string.IsNullOrWhiteSpace(DocumentType) && RecordId > 0)
                    return Kind + ": " + Title + " (" + DocumentType + " #" + RecordId + ")";
                return Kind + ": " + Title;
            }
        }
    }
}
