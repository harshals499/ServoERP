using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class DashboardForm : DeferredPageControl
    {
        public const string ShortcutNewJob = "NewJob";
        public const string ShortcutNewQuotation = "NewQuotation";
        public const string ShortcutNewInvoice = "NewInvoice";
        public const string ShortcutNewAMC = "NewAMC";

        public Action<int> OnNavigate { get; set; }
        public Action<string> OnShortcut { get; set; }

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
        private Panel _host;
        private Label _clockLabel;
        private ComboBox _languageCombo;
        private bool _languageSelectionChanging;
        private bool _backupNowRunning;
        private bool _buildingShell;
        private Timer _clockTimer;

        public DashboardForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            AutoScroll = false;
            LayoutManager.ResetPage("Dashboard");
            EnableDeferredLoad(async () =>
            {
                BuildShell();
                await Task.Run((Action)LoadData);
                if (!IsDisposed)
                    BuildShell();
            });
        }

        /// <summary>Refreshes dashboard labels and fonts after the selected language changes.</summary>
        protected override void ApplyLanguage()
        {
            base.ApplyLanguage();
            if (_root != null && !IsDisposed)
                BuildShell();
        }

        private void LoadData()
        {
            try { _clients = _clientSvc.GetAllClients() ?? new List<B2BClient>(); } catch { }
            try { _vendors = _vendorSvc.GetSuppliers() ?? new List<Vendor>(); } catch { }
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
            if (_buildingShell || IsDisposed)
                return;

            _buildingShell = true;
            SuspendLayout();
            try
            {
            DisposeDashboardControls();
            _clockTimer?.Stop();
            _clockTimer?.Dispose();

            _host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage };
            _root = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10, 8, 14, 24),
                BackColor = DS.BgPage
            };
            _host.Controls.Add(_root);
            Controls.Add(_host);
            AddTopBar();
            AddGreetingBanner();
            AddShortcutActionsRow();
            AddDepartmentRows();
            AddFinancialOverviewRow();
                AddAlertsBar();
                NormalizeDashboardFixedLayout();

            _clockTimer = new Timer { Interval = 60000 };
            _clockTimer.Tick += (s, e) => { if (_clockLabel != null) _clockLabel.Text = DateTime.Now.ToString("hh:mm tt"); };
            _clockTimer.Start();
            }
            finally
            {
                ResumeLayout(true);
                _buildingShell = false;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            NormalizeDashboardFixedLayout();
            BeginInvoke((Action)(() =>
            {
                RebuildIfReady();
                NormalizeDashboardFixedLayout();
            }));
        }

        private void DisposeDashboardControls()
        {
            Control[] controls = Controls.Cast<Control>().ToArray();
            Controls.Clear();
            foreach (Control control in controls)
                control.Dispose();
        }

        private void RebuildIfReady()
        {
            if (_root == null || IsDisposed || Width <= 0 || _buildingShell)
                return;
            BuildShell();
        }

        private void NormalizeDashboardFixedLayout()
        {
            if (IsDisposed)
                return;

            LayoutManager.ResetPage("Dashboard");
            RemoveDashboardResizeState(this);
            if (_root != null)
                _root.Padding = new Padding(10, 8, 14, 24);
        }

        private static void RemoveDashboardResizeState(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            foreach (Control child in root.Controls.Cast<Control>().ToList())
            {
                string metadata = ((child.Name ?? string.Empty) + " " + (child.Tag == null ? string.Empty : child.Tag.ToString())).ToUpperInvariant();
                if (metadata.Contains("NO_DASHBOARD_RESIZE") || child is DashboardDeptCard)
                    CardResizeGripService.Detach(child);

                RemoveDashboardResizeState(child);
            }
        }

        private void AddTopBar()
        {
            Panel bar = CardPanel(ContentWidth(), 78);
            bar.BackColor = Color.White;
            bar.Padding = new Padding(22, 12, 22, 12);

            const int userWidth = 150;
            const int avatarSize = 32;
            const int languageWidth = 160;
            const int actionWidth = 116;
            const int gap = 10;
            bool showTopBarMeta = bar.Width >= 1120;
            int right = bar.Width - 22;
            int customizeX = right - actionWidth;
            int backupX = customizeX - actionWidth - gap;
            int userBlockWidth = avatarSize + 8 + userWidth;
            int avatarX = backupX - userBlockWidth - gap;
            int userX = avatarX + avatarSize + 8;
            int languageX = avatarX - languageWidth - gap;
            bool showLanguage = languageX >= 820;
            if (!showLanguage)
                languageX = -languageWidth;
            int rightClusterLeft = showLanguage ? languageX : avatarX;
            bool showUser = avatarX >= 1010;
            bool showActions = backupX >= 900;
            bool showBackup = backupX >= 1020;
            bool showCustomize = customizeX >= 900;
            if (!showUser)
            {
                avatarX = -avatarSize;
                userX = -userWidth;
                rightClusterLeft = showActions ? Math.Min(backupX, customizeX) : right;
            }
            if (!showActions)
                rightClusterLeft = right;

            int searchX = 292;
            int searchLimit = rightClusterLeft - searchX - (showTopBarMeta ? 128 : 16);
            int searchWidth = Math.Max(240, Math.Min(430, searchLimit));
            bool showSearch = searchLimit >= 260;
            if (!showSearch)
                searchWidth = 0;

            Label pageTitle = new Label
            {
                Text = "Dashboard",
                Location = new Point(22, 14),
                Size = new Size(220, 26),
                Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
                ForeColor = DS.Slate950,
                AutoEllipsis = true
            };
            Label pageSubtitle = new Label
            {
                Text = "Business overview for today",
                Location = new Point(23, 42),
                Size = new Size(220, 18),
                Font = new Font("Segoe UI", 8.6f),
                ForeColor = DS.Slate600,
                AutoEllipsis = true
            };

            Panel searchHost = new Panel
            {
                Location = new Point(searchX, 18),
                Size = new Size(searchWidth, 40),
                BackColor = DS.Slate50,
                Visible = showSearch
            };
            searchHost.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, searchHost.Width - 1, searchHost.Height - 1), 8))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            TextBox search = new TextBox
            {
                Text = T("SearchPlaceholder"),
                Location = new Point(38, 11),
                Size = new Size(Math.Max(80, searchWidth - 118), 22),
                BorderStyle = BorderStyle.None,
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.4f),
                ForeColor = DS.Slate600,
                BackColor = DS.Slate50
            };
            Label searchIcon = ModernIconSystem.Icon(ModernIconKind.Search, 16, DS.Slate600);
            searchIcon.Location = new Point(14, 10);
            searchIcon.Size = new Size(18, 20);
            Label ctrl = new Label { Text = "Ctrl + K", Location = new Point(Math.Max(10, searchHost.Width - 66), 8), Size = new Size(54, 24), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 7.2f, FontStyle.Bold), ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleCenter };
            searchHost.Controls.AddRange(new Control[] { searchIcon, search, ctrl });

            int timeX = showSearch ? searchHost.Right + 12 : 292;
            Panel timeBlock = new Panel { Location = new Point(timeX, 16), Size = new Size(100, 44), BackColor = Color.White, Visible = showTopBarMeta && timeX + 100 < rightClusterLeft - 8 };
            Label date = new Label { Text = DateTime.Today.ToString("dd/MM/yyyy"), Location = new Point(0, 5), Size = new Size(92, 18), Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), ForeColor = DS.Slate700 };
            _clockLabel = new Label { Text = DateTime.Now.ToString("hh:mm tt"), Location = new Point(0, 24), Size = new Size(78, 18), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500 };
            timeBlock.Controls.AddRange(new Control[] { date, _clockLabel });
            date.Visible = showTopBarMeta;
            _clockLabel.Visible = showTopBarMeta;
            Button customize = SecondaryButton(T("Customize"), customizeX, 22, actionWidth, 34);
            Label avatar = new Label { Text = Initials(CurrentUserName()), Location = new Point(avatarX, 22), Size = new Size(avatarSize, avatarSize), BackColor = DS.Primary50, ForeColor = DS.Primary600, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(avatar, 16);
            Label user = new Label { Text = CurrentUserName(), Location = new Point(userX, 28), Size = new Size(userWidth, 20), Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            Button backupNow = SecondaryButton("Backup Now", backupX, 22, actionWidth, 34);
            customize.Visible = showActions && showCustomize;
            backupNow.Visible = showActions && showBackup;
            avatar.Visible = showUser;
            user.Visible = showUser;
            backupNow.Name = "btnDashboardBackupNow";
            backupNow.Click += (s, e) => RunDashboardBackupNow(backupNow);
            Panel languagePanel = BuildLanguageSelector(languageX, 23, languageWidth, 30);
            languagePanel.Visible = showLanguage;

            bar.Controls.AddRange(new Control[] { pageTitle, pageSubtitle, searchHost, timeBlock, customize, backupNow, languagePanel, avatar, user });
            _root.Controls.Add(bar);
        }

        /// <summary>Runs a manual backup from the dashboard shortcut without blocking the UI.</summary>
        private void RunDashboardBackupNow(Button sourceButton)
        {
            if (_backupNowRunning)
                return;

            _backupNowRunning = true;
            if (sourceButton != null)
            {
                sourceButton.Enabled = false;
                sourceButton.Text = "Backing up...";
            }

            var worker = CreateWorker();
            worker.DoWork += (s, e) => e.Result = new BackupService().RunBackup(BackupTrigger.Manual);
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    RunOnUI(() =>
                    {
                        worker.Dispose();
                        _backupNowRunning = false;
                        if (sourceButton != null && !sourceButton.IsDisposed)
                        {
                            sourceButton.Enabled = true;
                            sourceButton.Text = "Backup Now";
                        }
                        ToastNotification.ShowToast("Backup failed - please check settings", DS.Red600);
                    });
                    ShowError( "Manual backup failed. Please check backup settings.", e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(() =>
                {
                    worker.Dispose();
                    _backupNowRunning = false;
                    if (sourceButton != null && !sourceButton.IsDisposed)
                    {
                        sourceButton.Enabled = true;
                        sourceButton.Text = "Backup Now";
                    }

                    BackupResult result = e.Result as BackupResult;
                    if (result != null && result.Success)
                        ToastNotification.ShowToast("Backup completed - saved to " + FriendlyBackupDestination(result.DestinationUsed), DS.Green600);
                    else
                        ToastNotification.ShowToast("Backup failed - please check settings", DS.Red600);
                });
            };
            worker.RunWorkerAsync();
        }

        /// <summary>Returns display text for backup destinations.</summary>
        private static string FriendlyBackupDestination(string destination)
        {
            if (string.Equals(destination, "Network", StringComparison.OrdinalIgnoreCase))
                return "Network Server";
            if (string.Equals(destination, "Local", StringComparison.OrdinalIgnoreCase))
                return "Local Folder";
            if (string.Equals(destination, "ExternalDrive", StringComparison.OrdinalIgnoreCase))
                return "External Drive";
            return "backup destination";
        }

        private Panel BuildLanguageSelector(int x, int y, int width, int height)
        {
            Panel panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.Transparent
            };

            Label label = new Label
            {
                Text = T("Language"),
                Location = new Point(0, 2),
                Size = new Size(width, 14),
                Font = new Font(LanguageManager.GetUiFontFamily(), 7.2f, FontStyle.Bold),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _languageCombo = new ComboBox
            {
                Location = height <= 32 ? new Point(0, 3) : new Point(0, 16),
                Size = new Size(width, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font(LanguageManager.GetUiFontFamily(), height <= 32 ? 8f : 8.5f)
            };
            _languageCombo.Items.AddRange(new object[] { LanguageManager.English, LanguageManager.Marathi, LanguageManager.Hindi });
            _languageSelectionChanging = true;
            _languageCombo.SelectedItem = LanguageManager.CurrentLanguage;
            if (_languageCombo.SelectedIndex < 0)
                _languageCombo.SelectedIndex = 0;
            _languageSelectionChanging = false;
            _languageCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_languageSelectionChanging || _languageCombo.SelectedItem == null)
                    return;

                string selected = _languageCombo.SelectedItem.ToString();
                LanguageManager.SetLanguage(selected);
                DbSettings.Set("Language", LanguageManager.CurrentLanguage);
            };

            if (height > 32)
                panel.Controls.Add(label);
            panel.Controls.Add(_languageCombo);
            return panel;
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
            Label sub = new Label { Text = T("BusinessToday"), Location = new Point(92, 52), Size = new Size(420, 20), Font = new Font(LanguageManager.GetUiFontFamily(), 8.6f), ForeColor = DS.Slate600 };
            ComboBox range = new ComboBox { Location = new Point(banner.Width - 220, 28), Size = new Size(190, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font(LanguageManager.GetUiFontFamily(), 8.5f) };
            range.Items.AddRange(new object[] { T("This Month"), T("Last Month"), T("This Quarter"), T("This Year") });
            range.SelectedIndex = 0;
            banner.Controls.AddRange(new Control[] { icon, title, sub, range });
            _root.Controls.Add(banner);
        }

        private void AddShortcutActionsRow()
        {
            int width = ContentWidth();
            Panel panel = CardPanel(width, 88);
            panel.BackColor = Color.White;

            Label icon = ModernIconSystem.Badge(ModernIconKind.Status, 42, DS.Primary50, DS.Primary600, 10);
            icon.Location = new Point(20, 23);
            Label title = new Label
            {
                Text = "Quick create",
                Location = new Point(76, 20),
                Size = new Size(220, 22),
                Font = new Font(LanguageManager.GetUiFontFamily(), 10.5f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label subtitle = new Label
            {
                Text = "Start daily sales and service entries.",
                Location = new Point(76, 45),
                Size = new Size(360, 20),
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f),
                ForeColor = DS.Slate600
            };

            int buttonWidth = 154;
            int gap = 12;
            string[] shortcutLabels = { "+ New Job", "+ New Quotation", "+ New Invoice", "+ Add AMC" };
            string[] shortcutActions = { ShortcutNewJob, ShortcutNewQuotation, ShortcutNewInvoice, ShortcutNewAMC };
            Color[] shortcutColors = { DS.Primary600, DS.Teal600, Color.FromArgb(124, 58, 237), Color.FromArgb(16, 185, 129) };
            int startX = Math.Max(460, width - ((buttonWidth * shortcutLabels.Length) + (gap * (shortcutLabels.Length - 1))) - 24);

            for (int i = 0; i < shortcutLabels.Length; i++)
                AddShortcutButton(panel, shortcutLabels[i], startX + (buttonWidth + gap) * i, shortcutActions[i], shortcutColors[i]);

            panel.Controls.AddRange(new Control[] { icon, title, subtitle });
            _root.Controls.Add(panel);
        }

        private void AddShortcutButton(Panel parent, string text, int x, string action, Color color)
        {
            Button button = PrimaryButton(text, x, 25, 154, 38);
            button.BackColor = color;
            button.Cursor = Cursors.Hand;
            button.Click += (s, e) => OnShortcut?.Invoke(action);
            parent.Controls.Add(button);
        }

        private void AddDepartmentRows()
        {
            int rowWidth = ContentWidth();
            const int columns = 5;
            const int cardSideMargin = 4;
            int cardWidth = Math.Max(220, (rowWidth - (columns * cardSideMargin * 2)) / columns);
            FlowLayoutPanel row1 = RowPanel(rowWidth, 232);
            FlowLayoutPanel row2 = RowPanel(rowWidth, 232);
            row1.WrapContents = false;
            row2.WrapContents = false;

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

            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Document, "#ede9fe", "#7c3aed", T("Sales / Quotations"), Count(openQuotes), T("Open Quotations"), Money(quotesMtd), T("Value (MTD)"), null, 6));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Job, "#fff7ed", "#f97316", T("Jobs / Projects"), Count(activeJobs), T("Total Active Jobs"), null, null, new[] { Pill(inProgress + " " + T("In Progress"), Color.FromArgb(249, 115, 22)), Pill(overdueJobs + " " + T("Overdue"), DS.Red500) }, 15));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Purchase, "#f0fdf4", "#16a34a", T("Purchase Orders"), Count(openPos), T("Open POs"), Money(poMtd), T("Value (MTD)"), null, 10, openPos > 0 ? DS.Red500 : (Color?)null));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Invoice, "#f0fdfa", "#0d9488", T("Invoices"), Count(overdueInvoices.Count), T("Overdue Invoices"), Money(overdueInvoices.Sum(i => i.BalanceDue)), T("Overdue Amount"), null, 3, overdueInvoices.Count > 0 ? DS.Red500 : DS.Green600));
            row1.Controls.Add(Dept(cardWidth, ModernIconKind.Payment, "#eff6ff", "#2563eb", T("Payments"), Money(pendingPayables), T("Pending Payables"), Money(netCashFlow), T("Net Cash Flow"), null, 4, DS.Red500, netCashFlow < 0 ? DS.Red500 : DS.Green600));

            decimal overduePayables = _purchaseOrders.Where(p => p.IsOverdue).Sum(p => p.BalanceDue);
            int activeVendors = _vendors.Count(v => v.IsActive && !v.IsArchived);
            int activeClients = _clients.Count(c => c.IsActive);
            decimal outstanding = _invoices.Where(i => !IsPaid(i.PaymentStatus)).Sum(i => Math.Max(0m, i.BalanceDue));
            int toOrder = _inventory.Count(i => i.IsLowStock);
            int pricedItems = _inventory.Count(i => i.LastPurchaseRate > 0);
            int activeEmployees = _employees.Count(e => IsAny(e.Status, "Active"));
            int leaveToday = _employees.Count(e => IsAny(e.Status, "Leave", "On Leave"));
            int openTickets = _serviceTickets.Count(t => !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));
            int highTickets = _serviceTickets.Count(t => IsAny(t.Priority, "High", "Critical") && !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));

            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Vendor, "#fffbeb", "#d97706", T("Suppliers"), Count(activeVendors), T("Active Suppliers"), Money(overduePayables), T("Overdue Supplier Payables"), null, 9, null, overduePayables > 0 ? DS.Red500 : (Color?)null));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Client, "#eff6ff", "#2563eb", T("Clients"), Count(activeClients), T("Active Clients"), outstanding > 0 ? Money(outstanding) : "-", T("Outstanding"), null, 1, DS.Green600));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Inventory, "#faf5ff", "#9333ea", T("Materials / Procurement"), Count(toOrder), T("To Order Items"), Count(pricedItems), T("Priced Items"), null, 11, toOrder > 0 ? Color.FromArgb(217, 119, 6) : (Color?)null, pricedItems > 0 ? DS.Green600 : (Color?)null));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.User, "#eff6ff", "#2563eb", T("Employees"), Count(activeEmployees), T("Active Employees"), Count(leaveToday), T("On Leave Today"), null, 12, DS.Green600));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Service, "#eff6ff", "#2563eb", T("Service Operations"), Count(openTickets), T("Open Service Tickets"), Count(highTickets), T("High Priority"), null, 15, null, highTickets > 0 ? DS.Red500 : (Color?)null));

            _root.Controls.Add(row1);
            _root.Controls.Add(row2);
        }

        private DashboardDeptCard Dept(int width, ModernIconKind icon, string bg, string color, string title, string primaryValue, string primaryLabel, string secondaryValue, string secondaryLabel, IEnumerable<DashboardCardPill> pills, int nav, Color? primaryColor = null, Color? secondaryColor = null)
        {
            var card = new DashboardDeptCard(icon, ColorTranslator.FromHtml(bg), ColorTranslator.FromHtml(color), title,
                new DashboardCardMetric { Value = primaryValue, Label = primaryLabel, Color = primaryColor },
                secondaryLabel == null ? null : new DashboardCardMetric { Value = secondaryValue, Label = secondaryLabel, Color = secondaryColor },
                pills,
                () => OnNavigate?.Invoke(nav));
            card.Width = width;
            card.Margin = new Padding(4, 6, 4, 6);
            card.Tag = "NO_DASHBOARD_RESIZE";
            GlobalCardContextMenu.AttachCard(card, title, "Dashboard", "Nav" + nav, () => OnNavigate?.Invoke(nav));
            return card;
        }

        private void AddFinancialOverviewRow()
        {
            int width = ContentWidth();
            FlowLayoutPanel row = RowPanel(width, 270);
            Panel finance = CardPanel(width, 258);
            BuildFinance(finance);
            row.Controls.Add(finance);
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

        private void AddAlertsBar()
        {
            int overdueJobs = OverdueJobs();
            int openPos = _purchaseOrders.Count(p => IsAny(p.Status, "Draft", "Pending Approval", "Approved", "Open"));
            int overdueInv = _invoices.Count(i => !IsPaid(i.PaymentStatus) && i.DueDate.Date < DateTime.Today);
            int toOrder = _inventory.Count(i => i.IsLowStock);
            int highTickets = _serviceTickets.Count(t => IsAny(t.Priority, "High", "Critical") && !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));

            Panel bar = CardPanel(ContentWidth(), 74);
            bar.Controls.Add(new Label { Text = "🔔  Alerts & Notifications", Location = new Point(22, 25), Size = new Size(210, 24), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            AddAlert(bar, 250, ModernIconKind.Refresh, Color.FromArgb(249, 115, 22), overdueJobs, "Overdue Jobs");
            AddAlert(bar, 450, ModernIconKind.Purchase, Color.FromArgb(59, 130, 246), openPos, "Open Purchase Orders");
            AddAlert(bar, 680, ModernIconKind.Invoice, DS.Red500, overdueInv, "Overdue Invoices");
            AddAlert(bar, 890, ModernIconKind.Inventory, Color.FromArgb(245, 158, 11), toOrder, "Materials To Order");
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

        private DashboardCardPill Pill(string text, Color color) => new DashboardCardPill { Text = text, Color = color };
        private FlowLayoutPanel RowPanel(int width, int height) => new FlowLayoutPanel { Width = width, Height = height, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 10), Tag = "NO_DASHBOARD_RESIZE" };
        private Panel CardPanel(int width, int height)
        {
            Panel panel = new Panel { Width = width, Height = height, BackColor = DS.BgCard, Margin = new Padding(0, 0, 0, 12), Tag = "NO_DASHBOARD_RESIZE" };
            panel.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 10)) using (Pen pen = new Pen(DS.Border)) e.Graphics.DrawPath(pen, path); };
            return panel;
        }
        private Button PrimaryButton(string text, int x, int y, int w, int h)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = DS.Primary600, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font(LanguageManager.GetUiFontFamily(), 8.1f, FontStyle.Bold) };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }
        private Button SecondaryButton(string text, int x, int y, int w, int h)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.White, ForeColor = DS.Slate900, FlatStyle = FlatStyle.Flat, Font = new Font(LanguageManager.GetUiFontFamily(), 8.1f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = DS.BorderStrong;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }
        private int ContentWidth()
        {
            int sourceWidth = ClientSize.Width;
            if (sourceWidth < 900 && Parent != null && Parent.ClientSize.Width > sourceWidth)
                sourceWidth = Parent.ClientSize.Width;
            if (sourceWidth < 900 && _host != null && !_host.IsDisposed && _host.ClientSize.Width > sourceWidth)
                sourceWidth = _host.ClientSize.Width;

            int available = sourceWidth > 0 ? sourceWidth - SystemInformation.VerticalScrollBarWidth - 34 : 1160;
            return Math.Max(1160, available);
        }
        private int OverdueJobs() => _jobs.Count(j => j.ScheduledDate.Date < DateTime.Today && !IsAny(j.Status, "Completed", "Cancelled"));
        private static bool IsThisMonth(DateTime d) => d.Month == DateTime.Today.Month && d.Year == DateTime.Today.Year;
        private static bool IsAny(string actual, params string[] values) => values.Any(v => string.Equals((actual ?? "").Trim(), v, StringComparison.OrdinalIgnoreCase));
        private static bool IsPaid(string status) => IsAny(status, "Paid");
        private static string Count(int n) => n.ToString("N0");
        private static string Money(decimal n) => IndiaFormatHelper.FormatCurrency(n);
        private static string Safe(string text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        private static decimal QuoteValue(TenderBid q) => q == null ? 0 : (q.TotalWithGST > 0 ? q.TotalWithGST : (q.BidValue > 0 ? q.BidValue : q.TotalTaxableValue + q.TotalGSTAmount));
        private static string TimeOfDay() { int h = DateTime.Now.Hour; return h < 12 ? T("morning") : h < 17 ? T("afternoon") : T("evening"); }
        private static string T(string key) => LanguageManager.Get(key);
        private static string CurrentUserName() => !string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.DisplayName) ? SessionManager.CurrentUser.DisplayName : (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.Username) ? SessionManager.CurrentUser.Username : "User");
        private static string Initials(string name) => string.Join("", (name ?? "User").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0])).ToUpperInvariant();
        private static Color Blend(Color color, float amount) => Color.FromArgb(color.R + (int)((255 - color.R) * amount), color.G + (int)((255 - color.G) * amount), color.B + (int)((255 - color.B) * amount));
    }
}


