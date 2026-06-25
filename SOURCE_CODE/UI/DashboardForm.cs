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
        private sealed class DashboardRecentItem
        {
            public string Module { get; set; }
            public int RecordId { get; set; }
            public string Reference { get; set; }
            public string PartyName { get; set; }
            public string SiteName { get; set; }
            public string Status { get; set; }
            public string Summary { get; set; }
            public decimal Amount { get; set; }
            public DateTime ActivityDate { get; set; }
        }

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
        private readonly NotificationCenterService _notificationSvc = new NotificationCenterService();

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
        private string _notificationCountText = string.Empty;
        private bool _notificationCountLoading;

        public DashboardForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            AutoScroll = false;
            DashboardRefreshService.RefreshRequested += DashboardRefreshService_RefreshRequested;
            EnableDeferredLoad(async () =>
            {
                BuildShell();
                await Task.Run((Action)LoadData);
                if (!IsDisposed)
                    BuildShell();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DashboardRefreshService.RefreshRequested -= DashboardRefreshService_RefreshRequested;
                _clockTimer?.Stop();
                _clockTimer?.Dispose();
                _clockTimer = null;
            }

            base.Dispose(disposing);
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
            try { _clients = (_clientSvc.GetAllClients() ?? new List<B2BClient>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Clients", ex); }
            try { _vendors = (_vendorSvc.GetSuppliers() ?? new List<Vendor>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Vendors", ex); }
            try { _jobs = (_jobSvc.GetAll() ?? new List<Job>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Jobs", ex); }
            try { _invoices = (_invoiceSvc.GetAllInvoices() ?? new List<Invoice>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Invoices", ex); }
            try { _payments = (_paymentSvc.GetAllPayments() ?? new List<Payment>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Payments", ex); }
            try { _purchaseOrders = (_purchaseSvc.GetAllFresh() ?? new List<PurchaseOrder>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Purchases", ex); }
            try { _quotations = (_tenderSvc.GetAll() ?? new List<TenderBid>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Quotations", ex); }
            try { _inventory = (_inventorySvc.GetAll() ?? new List<StockItem>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Inventory", ex); }
            try { _employees = (_employeeSvc.GetAll() ?? new List<Employee>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.Employees", ex); }
            try { _serviceTickets = (_serviceDeskSvc.GetAll() ?? new List<ServiceDeskIncident>()).ToList(); } catch (Exception ex) { AppLogger.LogError("DashboardForm.LoadData.ServiceDesk", ex); }
        }

        private void DashboardRefreshService_RefreshRequested(object sender, DashboardRefreshEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke((Action)(async () => await RefreshDashboardDataAsync()));
        }

        private async Task RefreshDashboardDataAsync()
        {
            if (IsDisposed || _buildingShell)
                return;

            try
            {
                await Task.Run((Action)LoadData);
                if (!IsDisposed)
                    BuildShell();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DashboardForm.RefreshDashboardDataAsync", ex);
            }
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
            AddAlertsBar();
            AddShortcutActionsRow();
            AddRecentActivityRow();
            AddDepartmentRows();
            AddFinancialOverviewRow();

            _clockTimer = new Timer { Interval = 60000 };
            _clockTimer.Tick += (s, e) => { if (_clockLabel != null) _clockLabel.Text = DateTime.Now.ToString("hh:mm tt"); };
            _clockTimer.Start();
            QueueNotificationCountRefresh();
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
            BeginInvoke((Action)(() =>
            {
                RebuildIfReady();
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

        private void AddTopBar()
        {
            Panel bar = CardPanel(ContentWidth(), 96);
            bar.BackColor = Color.White;

            Button notifications = BuildNotificationButton(0, 0, 38, GetNotificationCountText());
            Button customize = SecondaryButton(T("Customize"), 0, 0, 110, 34);
            Button backupNow = SecondaryButton("Backup Now", 0, 0, 138, 34);
            ModernIconSystem.AddButtonIcon(backupNow, ModernIconKind.Backup);
            backupNow.TextAlign = ContentAlignment.MiddleRight;
            backupNow.Padding = new Padding(10, 0, 14, 0);
            backupNow.Name = "btnDashboardBackupNow";
            backupNow.Click += (s, e) => RunDashboardBackupNow(backupNow);

            SharedPageHeaderModel model = SharedPageHeader.CreateWorkspaceDashboard(
                "DashboardTopHeader",
                "Dashboard",
                "Business overview for today",
                new List<Control> { notifications, customize, backupNow },
                SharedPageHeader.CreateSearchCommand("DashboardGlobalSearch", 300, "Search", "Ctrl + K", () => SharedUiPrimitives.OpenGlobalSearch(this)),
                BuildDashboardHeaderMetaPanel(),
                Color.White,
                new Padding(22, 12, 22, 12));
            model.Dock = DockStyle.Fill;
            model.DrawBottomBorder = false;
            model.DefaultHeight = 82;
            model.CompactHeight = 118;
            Panel header = SharedPageHeader.Build(model).Header;
            bar.Controls.Add(header);
            _root.Controls.Add(bar);
        }

        private Panel BuildDashboardHeaderMetaPanel()
        {
            Panel meta = new Panel
            {
                Name = "DashboardHeaderMetaPanel",
                Size = new Size(184, 38),
                BackColor = Color.Transparent
            };

            Label avatar = new Label
            {
                Text = Initials(CurrentUserName()),
                Location = new Point(0, 3),
                Size = new Size(32, 32),
                BackColor = DS.Primary50,
                ForeColor = DS.Primary600,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(avatar, 16);

            Label user = new Label
            {
                Text = CurrentUserName(),
                Location = new Point(40, 2),
                Size = new Size(72, 16),
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            Label date = new Label
            {
                Text = DateTime.Today.ToString("dd/MM/yyyy"),
                Location = new Point(40, 20),
                Size = new Size(72, 14),
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                ForeColor = DS.Slate700,
                AutoEllipsis = true
            };

            _clockLabel = new Label
            {
                Text = DateTime.Now.ToString("hh:mm tt"),
                Location = new Point(118, 10),
                Size = new Size(60, 16),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleRight
            };

            meta.Controls.AddRange(new Control[] { avatar, user, date, _clockLabel });
            return meta;
        }

        private Button BuildNotificationButton(int x, int y, int size, string countText)
        {
            Button button = SecondaryButton("!", x, y, size, size);
            button.Name = "btnDashboardNotifications";
            button.AccessibleName = "Open alerts and notifications";
            button.Text = string.Empty;
            button.BackColor = Color.White;
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            button.Click += (s, e) => OpenNotificationCenter();
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle iconBounds = new Rectangle((button.Width - 18) / 2, (button.Height - 18) / 2 + 1, 18, 18);
                using (Pen pen = new Pen(DS.Slate700, 1.6f))
                {
                    e.Graphics.DrawArc(pen, iconBounds.Left + 4, iconBounds.Top + 3, 10, 10, 205, 130);
                    e.Graphics.DrawLine(pen, iconBounds.Left + 5, iconBounds.Top + 9, iconBounds.Left + 4, iconBounds.Bottom - 4);
                    e.Graphics.DrawLine(pen, iconBounds.Right - 5, iconBounds.Top + 9, iconBounds.Right - 4, iconBounds.Bottom - 4);
                    e.Graphics.DrawLine(pen, iconBounds.Left + 4, iconBounds.Bottom - 4, iconBounds.Right - 4, iconBounds.Bottom - 4);
                    e.Graphics.DrawLine(pen, iconBounds.Left + 8, iconBounds.Top + 2, iconBounds.Right - 8, iconBounds.Top + 2);
                    e.Graphics.DrawArc(pen, iconBounds.Left + 7, iconBounds.Bottom - 6, 4, 4, 0, 180);
                }

                if (!string.IsNullOrWhiteSpace(countText))
                {
                    Rectangle badge = new Rectangle(button.Width - 18, 3, 14, 14);
                    using (GraphicsPath path = DS.RoundedRect(badge, 7))
                    using (Brush brush = new SolidBrush(DS.Red500))
                        e.Graphics.FillPath(brush, path);

                    using (Font font = new Font("Segoe UI", 6.2f, FontStyle.Bold))
                        TextRenderer.DrawText(e.Graphics, countText.Length > 2 ? "!" : countText, font, badge, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            };

            ToolTip tip = new ToolTip();
            tip.SetToolTip(button, "Alerts and notifications");
            return button;
        }

        private string GetNotificationCountText()
        {
            return _notificationCountText ?? string.Empty;
        }

        private void QueueNotificationCountRefresh()
        {
            if (_notificationCountLoading || IsDisposed || !IsHandleCreated)
                return;

            _notificationCountLoading = true;
            Task.Run(() =>
            {
                try
                {
                    int count = _notificationSvc.GetActiveCount(99);
                    return count <= 0 ? string.Empty : (count > 99 ? "99+" : count.ToString());
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("DashboardForm.QueueNotificationCountRefresh", ex);
                    return string.Empty;
                }
            }).ContinueWith(task =>
            {
                if (IsDisposed)
                    return;

                _notificationCountLoading = false;
                string nextValue = task.Status == TaskStatus.RanToCompletion ? task.Result ?? string.Empty : string.Empty;
                if (string.Equals(_notificationCountText, nextValue, StringComparison.Ordinal))
                    return;

                _notificationCountText = nextValue;
                BuildShell();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OpenNotificationCenter()
        {
            try
            {
                using (var dialog = new NotificationCenterDialog(pageKey => OnShortcut?.Invoke(pageKey)))
                    dialog.ShowDialog(FindForm());

                BuildShell();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Notifications"), "Opening alerts and notifications", ex);
            }
        }

        /// <summary>Runs a manual backup from the dashboard shortcut without blocking the UI.</summary>
        private async void RunDashboardBackupNow(Button sourceButton)
        {
            if (_backupNowRunning)
                return;

            _backupNowRunning = true;
            if (sourceButton != null)
            {
                sourceButton.Enabled = false;
                sourceButton.Text = "Backing up...";
            }

            try
            {
                BackupResult result = await Task.Run(() => new BackupService().RunBackup(BackupTrigger.Manual));

                RunOnUI(() =>
                {
                    _backupNowRunning = false;
                    if (sourceButton != null && !sourceButton.IsDisposed)
                    {
                        sourceButton.Enabled = true;
                        sourceButton.Text = "Backup Now";
                    }

                    if (result != null && result.Success)
                        ToastNotification.ShowToast("Backup completed - saved to " + FriendlyBackupDestination(result.DestinationUsed), DS.Green600);
                    else
                        ToastNotification.ShowToast("Backup failed - please check settings", DS.Red600);
                });
            }
            catch (Exception ex)
            {
                RunOnUI(() =>
                {
                    _backupNowRunning = false;
                    if (sourceButton != null && !sourceButton.IsDisposed)
                    {
                        sourceButton.Enabled = true;
                        sourceButton.Text = "Backup Now";
                    }
                    ToastNotification.ShowToast("Backup failed - please check settings", DS.Red600);
                });
                ShowError("Manual backup failed. Please check backup settings.", ex);
            }
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
            int procurementRequired = _inventory.Count(i => i.AvailableStock <= 0m || i.IsLowStock);
            int pricedItems = _inventory.Count(i => i.LastPurchaseRate > 0);
            int activeEmployees = _employees.Count(e => IsAny(e.Status, "Active"));
            int leaveToday = _employees.Count(e => IsAny(e.Status, "Leave", "On Leave"));
            int openTickets = _serviceTickets.Count(t => !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));
            int highTickets = _serviceTickets.Count(t => IsAny(t.Priority, "High", "Critical") && !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));

            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Vendor, "#fffbeb", "#d97706", T("Suppliers"), Count(activeVendors), T("Active Suppliers"), Money(overduePayables), T("Overdue Supplier Payables"), null, 9, null, overduePayables > 0 ? DS.Red500 : (Color?)null));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Client, "#eff6ff", "#2563eb", T("Clients"), Count(activeClients), T("Active Clients"), outstanding > 0 ? Money(outstanding) : "-", T("Outstanding"), null, 1, DS.Green600));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Inventory, "#faf5ff", "#9333ea", T("Materials / Procurement"), Count(procurementRequired), T("Procurement Required"), Count(pricedItems), T("Priced Items"), null, 11, procurementRequired > 0 ? Color.FromArgb(217, 119, 6) : (Color?)null, pricedItems > 0 ? DS.Green600 : (Color?)null));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.User, "#eff6ff", "#2563eb", T("Employees"), Count(activeEmployees), T("Active Employees"), Count(leaveToday), T("On Leave Today"), null, 12, DS.Green600));
            row2.Controls.Add(Dept(cardWidth, ModernIconKind.Service, "#eff6ff", "#2563eb", T("Service Operations"), Count(openTickets), T("Open Service Tickets"), Count(highTickets), T("High Priority"), null, 15, null, highTickets > 0 ? DS.Red500 : (Color?)null));

            _root.Controls.Add(row1);
            _root.Controls.Add(row2);
        }

        private void AddRecentActivityRow()
        {
            int width = ContentWidth();
            List<DashboardRecentItem> items = BuildRecentItems().Take(6).ToList();
            int cardHeight = items.Count == 0 ? 184 : 86 + (items.Count * 74);
            FlowLayoutPanel row = RowPanel(width, cardHeight + 12);
            Panel card = CardPanel(width, cardHeight);
            card.BackColor = Color.White;

            Label title = new Label
            {
                Text = "Recent Activity",
                Location = new Point(18, 14),
                Size = new Size(240, 22),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label subtitle = new Label
            {
                Text = "Latest saved quotations, invoices, jobs, and purchase orders.",
                Location = new Point(18, 38),
                Size = new Size(width - 140, 18),
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.1f),
                ForeColor = DS.Slate600
            };

            card.Controls.Add(title);
            card.Controls.Add(subtitle);

            if (items.Count == 0)
            {
                card.Controls.Add(new Label
                {
                    Text = "No recent records yet. Saved quotations, invoices, jobs, and purchase orders will appear here.",
                    Location = new Point(18, 106),
                    Size = new Size(width - 36, 24),
                    Font = new Font(LanguageManager.GetUiFontFamily(), 8.5f),
                    ForeColor = DS.Slate500
                });
            }
            else
            {
                int y = 66;
                for (int i = 0; i < items.Count; i++)
                {
                    Panel itemRow = BuildRecentItemRow(items[i], width - 36, i == items.Count - 1);
                    itemRow.Location = new Point(18, y);
                    card.Controls.Add(itemRow);
                    y += itemRow.Height + 8;
                }
            }

            row.Controls.Add(card);
            _root.Controls.Add(row);
        }

        private Panel BuildRecentItemRow(DashboardRecentItem item, int width, bool isLast)
        {
            var row = new Panel
            {
                Size = new Size(width, 66),
                BackColor = Color.FromArgb(249, 251, 253)
            };

            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, row.Width - 1, row.Height - 1), 10))
                using (Pen pen = new Pen(isLast ? Color.FromArgb(220, 227, 237) : Color.FromArgb(214, 223, 234)))
                    e.Graphics.DrawPath(pen, path);
            };

            Label module = new Label
            {
                Text = item.Module.ToUpperInvariant(),
                Location = new Point(12, 12),
                Size = new Size(88, 22),
                Font = new Font("Segoe UI", 7.2f, FontStyle.Bold),
                ForeColor = ModuleColor(item.Module),
                BackColor = Blend(ModuleColor(item.Module), 0.88f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(module, 10);

            int textLeft = 112;
            int actionLeft = Math.Max(textLeft + 222, width - 86);
            int amountLeft = Math.Max(textLeft + 180, actionLeft - 136);
            int textWidth = Math.Max(190, amountLeft - textLeft - 16);

            Label reference = new Label
            {
                Text = Safe(item.Reference, "-"),
                Location = new Point(textLeft, 9),
                Size = new Size(textWidth, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };
            Label detail = new Label
            {
                Text = BuildRecentDetail(item),
                Location = new Point(textLeft, 31),
                Size = new Size(textWidth, 16),
                Font = new Font(LanguageManager.GetUiFontFamily(), 8f),
                ForeColor = DS.Slate700,
                AutoEllipsis = true
            };
            Label summary = new Label
            {
                Text = Safe(item.Summary, item.Status),
                Location = new Point(textLeft, 48),
                Size = new Size(textWidth, 14),
                Font = new Font(LanguageManager.GetUiFontFamily(), 7.6f),
                ForeColor = DS.Slate500,
                AutoEllipsis = true
            };
            Button view = SecondaryButton("View", actionLeft, 17, 74, 32);
            view.Font = new Font(LanguageManager.GetUiFontFamily(), 8f, FontStyle.Bold);
            view.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 216);
            view.Click += (s, e) => OpenRecentActivityItem(item);
            Label amount = new Label
            {
                Text = item.Amount > 0m ? Money(item.Amount) : item.Status,
                Location = new Point(amountLeft, 11),
                Size = new Size(actionLeft - amountLeft - 10, 18),
                Font = new Font("Segoe UI", 8.4f, FontStyle.Bold),
                ForeColor = item.Amount > 0m ? DS.Slate900 : ModuleColor(item.Module),
                TextAlign = ContentAlignment.MiddleRight
            };
            Label when = new Label
            {
                Text = item.ActivityDate.ToString("dd/MM/yyyy hh:mm tt"),
                Location = new Point(amountLeft, 33),
                Size = new Size(actionLeft - amountLeft - 10, 16),
                Font = new Font("Segoe UI", 7.6f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleRight
            };

            row.Controls.Add(module);
            row.Controls.Add(reference);
            row.Controls.Add(detail);
            row.Controls.Add(summary);
            row.Controls.Add(view);
            row.Controls.Add(amount);
            row.Controls.Add(when);
            return row;
        }

        private void OpenRecentActivityItem(DashboardRecentItem item)
        {
            if (item == null || item.RecordId <= 0)
                return;

            NavigationHelper.OpenRecord(this, item.Module, item.RecordId);
        }

        private IEnumerable<DashboardRecentItem> BuildRecentItems()
        {
            IEnumerable<DashboardRecentItem> quotes = _quotations.Select(q => new DashboardRecentItem
            {
                Module = "Quotation",
                RecordId = q.BidID,
                Reference = q.QuotationNumber,
                PartyName = q.ClientName,
                SiteName = q.SiteName,
                Status = Safe(q.Status, "Draft"),
                Summary = q.TenderName,
                Amount = QuoteValue(q),
                ActivityDate = q.ModifiedDate ?? q.SubmittedDate ?? q.RequiredByDate ?? q.DueDate
            });

            IEnumerable<DashboardRecentItem> invoices = _invoices.Select(i => new DashboardRecentItem
            {
                Module = "Invoice",
                RecordId = i.InvoiceID,
                Reference = i.InvoiceNumber,
                PartyName = i.ClientName,
                SiteName = i.SiteName,
                Status = Safe(i.PaymentStatus, "Pending"),
                Summary = i.Subject,
                Amount = i.TotalAmount,
                ActivityDate = i.ModifiedDate ?? i.InvoiceDate
            });

            IEnumerable<DashboardRecentItem> jobs = _jobs.Select(j => new DashboardRecentItem
            {
                Module = "Job",
                RecordId = j.JobID,
                Reference = j.JobNumber,
                PartyName = j.ClientName,
                SiteName = j.SiteName,
                Status = Safe(FirstNonEmpty(j.PipelineStatus, j.Status), "Pending"),
                Summary = FirstNonEmpty(j.JobTitle, j.Title, j.Description),
                Amount = JobRecentAmount(j),
                ActivityDate = j.ModifiedDate ?? j.CreatedDate
            });

            IEnumerable<DashboardRecentItem> purchases = _purchaseOrders.Select(p => new DashboardRecentItem
            {
                Module = "Purchase",
                RecordId = p.POID,
                Reference = p.PONumber,
                PartyName = p.VendorName,
                SiteName = p.SiteName,
                Status = Safe(p.Status, "Draft"),
                Summary = p.Notes,
                Amount = p.TotalAmount,
                ActivityDate = p.ModifiedDate ?? p.CreatedByDate ?? p.CreatedDate
            });

            return quotes
                .Concat(invoices)
                .Concat(jobs)
                .Concat(purchases)
                .OrderByDescending(item => item.ActivityDate)
                .ThenByDescending(item => item.Amount);
        }

        private static string BuildRecentDetail(DashboardRecentItem item)
        {
            string party = Safe(item.PartyName, "No party");
            string site = string.IsNullOrWhiteSpace(item.SiteName) ? "No site" : item.SiteName.Trim();
            return party + " | " + site;
        }

        private static Color ModuleColor(string module)
        {
            if (string.Equals(module, "Quotation", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(124, 58, 237);
            if (string.Equals(module, "Invoice", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(13, 148, 136);
            if (string.Equals(module, "Job", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(249, 115, 22);
            if (string.Equals(module, "Purchase", StringComparison.OrdinalIgnoreCase))
                return Color.FromArgb(22, 163, 74);
            return DS.Slate700;
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
            card.Tag = "dashboard-card";
            AttachDashboardExceptionCard(card, title);
            GlobalCardContextMenu.AttachCard(card, title, "Dashboard", "Nav" + nav, () => OnNavigate?.Invoke(nav));
            return card;
        }

        private void AddFinancialOverviewRow()
        {
            int width = ContentWidth();
            FlowLayoutPanel row = RowPanel(width, 270);
            Panel finance = CardPanel(width, 258);
            BuildFinance(finance);
            AttachDashboardExceptionCard(finance, "Financial Overview");
            row.Controls.Add(finance);
            _root.Controls.Add(row);
        }

        private void AttachDashboardExceptionCard(Control card, string key)
        {
            if (card == null)
                return;
            card.Cursor = Cursors.Hand;
            card.DoubleClick += (s, e) => ExceptionCardDetailDialog.ShowFor(this, BuildDashboardExceptionDetail(key));
            foreach (Control child in card.Controls)
            {
                if (child is Button || child is ComboBox)
                    continue;
                child.Cursor = Cursors.Hand;
                child.DoubleClick += (s, e) => ExceptionCardDetailDialog.ShowFor(this, BuildDashboardExceptionDetail(key));
            }
        }

        private ExceptionCardDetail BuildDashboardExceptionDetail(string key)
        {
            string normalized = (key ?? string.Empty).Trim();
            if (normalized == T("Sales / Quotations") || normalized.IndexOf("Quotation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Sales / Quotations", "All quotation rows behind the dashboard card.", "Quotation", "Client", "Site", "Date", "Value", "Status");
                foreach (TenderBid q in _quotations.OrderByDescending(q => q.ModifiedDate ?? q.SubmittedDate ?? q.DueDate))
                    detail.AddRow(q.QuotationNumber, q.ClientName, q.SiteName, (q.ModifiedDate ?? q.SubmittedDate ?? q.DueDate).ToString("dd/MM/yyyy"), Money(QuoteValue(q)), Safe(q.Status, "Draft"));
                return detail;
            }
            if (normalized.IndexOf("Purchase", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Purchase Orders", "All purchase rows behind the dashboard card.", "PO", "Supplier", "PO Date", "Required", "Value", "Balance", "Status");
                foreach (PurchaseOrder p in _purchaseOrders.OrderByDescending(p => p.PODate))
                    detail.AddRow(p.PONumber, p.VendorName, p.PODate.ToString("dd/MM/yyyy"), p.PayByDate.ToString("dd/MM/yyyy"), Money(p.TotalAmount), Money(Math.Max(0m, p.BalanceDue)), Safe(p.Status, "Draft"));
                return detail;
            }
            if (normalized.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Invoices", "All invoices behind the dashboard card.", "Invoice", "Client", "Site", "Date", "Due", "Amount", "Balance", "Status");
                foreach (Invoice i in _invoices.OrderByDescending(i => i.InvoiceDate))
                    detail.AddRow(i.InvoiceNumber, i.ClientName, i.SiteName, i.InvoiceDate.ToString("dd/MM/yyyy"), i.DueDate.ToString("dd/MM/yyyy"), Money(i.TotalAmount), Money(Math.Max(0m, i.BalanceDue)), Safe(i.PaymentStatus, "Pending"));
                return detail;
            }
            if (normalized.IndexOf("Payment", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Payments", "All payments behind the dashboard card.", "Payment", "Client", "Invoice", "Date", "Mode", "Amount", "Reference");
                foreach (Payment p in _payments.OrderByDescending(p => p.PaymentDate))
                    detail.AddRow(p.PaymentNumber, p.ClientName, p.InvoiceNumber, p.PaymentDate.ToString("dd/MM/yyyy"), p.PaymentMode, Money(p.AmountPaid), p.ReferenceNumber);
                return detail;
            }
            if (normalized.IndexOf("Client", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Clients", "All client rows behind the dashboard card.", "Client", "Phone", "Email", "Status");
                foreach (B2BClient c in _clients.OrderBy(c => c.CompanyName))
                    detail.AddRow(c.CompanyName, c.Phone, c.Email, c.IsActive ? "Active" : "Inactive");
                return detail;
            }
            if (normalized.IndexOf("Supplier", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Suppliers", "All supplier rows behind the dashboard card.", "Supplier", "Phone", "Email", "Status");
                foreach (Vendor v in _vendors.OrderBy(v => v.VendorName))
                    detail.AddRow(v.VendorName, v.Phone, v.Email, v.IsActive && !v.IsArchived ? "Active" : "Inactive");
                return detail;
            }
            if (normalized.IndexOf("Material", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("Procurement", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Materials / Procurement", "All inventory rows behind the dashboard card.", "Item", "Category", "Available", "Minimum", "Rate", "Stock Status");
                foreach (StockItem item in _inventory.OrderBy(i => i.ItemName))
                    detail.AddRow(item.ItemName, item.Category, item.AvailableStock.ToString("0.##"), item.ReorderLevel.ToString("0.##"), Money(item.LastPurchaseRate), item.IsLowStock ? "Low Stock" : "Healthy");
                return detail;
            }
            if (normalized.IndexOf("Employee", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Employees", "All employee rows behind the dashboard card.", "Employee", "Role", "Phone", "Status");
                foreach (Employee e in _employees.OrderBy(e => e.Name))
                    detail.AddRow(e.Name, e.Designation, e.Phone, e.Status);
                return detail;
            }
            if (normalized.IndexOf("Service", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.IndexOf("Job", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var detail = ExceptionCardDetail.Create("Service Operations", "Open jobs and service tickets behind the dashboard cards.", "Type", "Reference", "Client", "Site", "Date", "Priority", "Status");
                foreach (Job j in _jobs.OrderByDescending(j => j.ScheduledDate))
                    detail.AddRow("Job", j.JobNumber, j.ClientName, j.SiteName, j.ScheduledDate.ToString("dd/MM/yyyy"), FirstNonEmpty(j.Priority, "-"), FirstNonEmpty(j.PipelineStatus, j.Status));
                foreach (ServiceDeskIncident t in _serviceTickets.OrderByDescending(t => t.OpenedAt))
                    detail.AddRow("Ticket", t.IncidentNumber, t.ClientName, t.SiteName, t.OpenedAt.ToString("dd/MM/yyyy"), t.Priority, t.Status);
                return detail;
            }
            if (normalized.IndexOf("Financial", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                decimal revenue = _invoices.Where(i => IsThisMonth(i.InvoiceDate)).Sum(i => i.TotalAmount);
                decimal expenses = _purchaseOrders.Where(p => IsThisMonth(p.PODate)).Sum(p => p.TotalAmount);
                return ExceptionCardDetail.Create("Financial Overview", "This-month totals behind the finance card.", "Metric", "Amount")
                    .AddRow("Total Revenue", Money(revenue))
                    .AddRow("Expenses", Money(expenses))
                    .AddRow("Gross Profit", Money(revenue - expenses))
                    .AddRow("Net Profit", Money(revenue - expenses))
                    .AddRow("Receipts", Money(_payments.Where(p => IsThisMonth(p.PaymentDate)).Sum(p => p.AmountPaid)));
            }
            return ExceptionCardDetail.Create("Dashboard Details", "No matching dashboard detail found.", "Message").AddRow("No rows found.");
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
            int procurementRequired = _inventory.Count(i => i.AvailableStock <= 0m || i.IsLowStock);
            int highTickets = _serviceTickets.Count(t => IsAny(t.Priority, "High", "Critical") && !IsAny(t.Status, "Resolved", "Closed", "Cancelled"));

            Panel bar = CardPanel(ContentWidth(), 74);
            bar.Name = "pnlDashboardAlertsNotifications";
            bar.Cursor = Cursors.Hand;
            bar.Click += (s, e) => OpenNotificationCenter();
            Label title = new Label { Text = "Alerts & Notifications", Location = new Point(58, 25), Size = new Size(174, 24), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label titleIcon = ModernIconSystem.Badge(ModernIconKind.Alert, 30, DS.Primary50, DS.Primary600, 8);
            titleIcon.Location = new Point(20, 21);
            title.Click += (s, e) => OpenNotificationCenter();
            titleIcon.Click += (s, e) => OpenNotificationCenter();
            bar.Controls.Add(titleIcon);
            bar.Controls.Add(title);
            AddAlert(bar, 250, ModernIconKind.Refresh, Color.FromArgb(249, 115, 22), overdueJobs, "Overdue Jobs");
            AddAlert(bar, 450, ModernIconKind.Purchase, Color.FromArgb(59, 130, 246), openPos, "Open Purchase Orders");
            AddAlert(bar, 680, ModernIconKind.Invoice, DS.Red500, overdueInv, "Overdue Invoices");
            AddAlert(bar, 890, ModernIconKind.Inventory, Color.FromArgb(245, 158, 11), procurementRequired, "Procurement Required");
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
            button.FlatAppearance.MouseOverBackColor = DS.Slate50;
            button.FlatAppearance.MouseDownBackColor = DS.Slate100;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = new Padding(10, 0, 10, 0);
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
        private static string FirstNonEmpty(params string[] values) => (values ?? new string[0]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
        private static decimal JobRecentAmount(Job job) => job == null ? 0m : Math.Max(job.ActualRevenue, Math.Max(job.QuotedRevenue, job.Revenue));
        private static decimal QuoteValue(TenderBid q) => q == null ? 0 : (q.TotalWithGST > 0 ? q.TotalWithGST : (q.BidValue > 0 ? q.BidValue : q.TotalTaxableValue + q.TotalGSTAmount));
        private static string TimeOfDay() { int h = DateTime.Now.Hour; return h < 12 ? T("morning") : h < 17 ? T("afternoon") : T("evening"); }
        private static string T(string key) => LanguageManager.Get(key);
        private static string CurrentUserName() => !string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.DisplayName) ? SessionManager.CurrentUser.DisplayName : (!string.IsNullOrWhiteSpace(SessionManager.CurrentUser?.Username) ? SessionManager.CurrentUser.Username : "User");
        private static string Initials(string name) => string.Join("", (name ?? "User").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(s => s[0])).ToUpperInvariant();
        private static Color Blend(Color color, float amount) => Color.FromArgb(color.R + (int)((255 - color.R) * amount), color.G + (int)((255 - color.G) * amount), color.B + (int)((255 - color.B) * amount));
    }
}


