using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    // ═══════════════════════════════════════════════════════════════════════
    //  HVAC PRO — Command Center (ServiceTitan-style)
    //
    //  Nav index map (matches MainForm switch):
    //   0=Dashboard  1=Clients   2=Contracts   3=Invoices  4=Payments
    //   5=SLA        6=Tenders   7=Reports     8=Settings  9=Vendors
    //  10=Purchases 11=Inventory 12=Employees
    // ═══════════════════════════════════════════════════════════════════════
    public partial class DashboardForm : DeferredPageControl
    {
        protected override bool EnableAutomaticLayoutScaling => false;

        // ── Services ───────────────────────────────────────────────────────
        private readonly ContractService  _contractSvc  = new ContractService();
        private readonly InvoiceService   _invoiceSvc   = new InvoiceService();
        private readonly PaymentService   _paymentSvc   = new PaymentService();
        private readonly VendorService    _vendorSvc    = new VendorService();
        private readonly PurchaseService  _purchaseSvc  = new PurchaseService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly EmployeeService  _employeeSvc  = new EmployeeService();
        private readonly PayrollService   _payrollSvc   = new PayrollService();
        private readonly ClientService    _clientSvc    = new ClientService();
        private readonly JobService       _jobSvc       = new JobService();
        private readonly TenderService    _tenderSvc    = new TenderService();

        // ── Navigation callback (wired by MainForm) ────────────────────────
        public Action<int> OnNavigate { get; set; }

        // ── Palette (ServiceTitan-inspired) ────────────────────────────────
        private static readonly Color BgPage = DS.BgPage;
        private static readonly Color BgCard = DS.BgCard;
        private static readonly Color BgHeader = DS.BgCard;
        private static readonly Color AccentIndigo = DS.Primary700;
        private static readonly Color AccentLight = DS.Primary50;
        private static readonly Color AccentOrange = Color.FromArgb(249, 115, 22);
        private static readonly Color AccentGreen = DS.Green600;
        private static readonly Color AccentRed = DS.Red500;
        private static readonly Color AccentBlue = DS.Primary600;
        private static readonly Color AccentPurple = Color.FromArgb(124, 58, 237);
        private static readonly Color AccentTeal = Color.FromArgb(6, 182, 212);
        private static readonly Color TextDark = DS.Slate900;
        private static readonly Color TextMid = DS.Slate600;
        private static readonly Color TextLight = DS.Slate500;
        private static readonly Color BorderLine = DS.Border;

        // ── Live data (loaded once) ─────────────────────────────────────────
        private decimal _mrr, _collectedMonth, _pendingAmount, _purchaseSpend, _stockValue;
        private decimal _jobRevenueMonth, _jobProfitMonth, _avgRevenuePerJob;
        private int     _activeContracts, _expiringContracts, _overdueCount, _purchasePaymentsOverdue;
        private int     _vendorCount, _lowStockCount, _employeeCount;
        private int     _jobPendingCount, _jobProgressCount, _jobCompletedCount;
        private string  _backupStatusText = "Not checked";
        private string  _backupSubText = "Open Settings";
        private Color   _backupAccent = AccentOrange;
        private List<AMCContract> _expiringList  = new List<AMCContract>();
        private List<Invoice>     _overdueList   = new List<Invoice>();
        private List<Invoice>     _recentInvoices = new List<Invoice>();
        private List<PurchaseOrder> _purchaseOrders = new List<PurchaseOrder>();
        private List<StockItem> _inventoryItems = new List<StockItem>();
        private List<Vendor> _vendors = new List<Vendor>();
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<Job> _jobs = new List<Job>();
        private List<Payment> _payments = new List<Payment>();
        private List<TenderBid> _quotes = new List<TenderBid>();
        private PayrollDashboardSnapshot _payrollSnapshot = new PayrollDashboardSnapshot();
        private Panel _dashboardHost;
        private Panel _dashboardContent;
        private DashboardAnalysisDrawer _analysisDrawer;
        private TextBox _dashboardSearchText;
        private Label _headerDateLabel;
        private Label _headerTimeLabel;
        private Timer _headerClockTimer;
        private readonly Dictionary<string, Button> _dashboardTabButtons = new Dictionary<string, Button>();
        private readonly ToolTip _dashboardToolTip = new ToolTip { InitialDelay = 400, ReshowDelay = 100, AutoPopDelay = 8000 };
        private string _selectedDashboardTabKey = "QuoteAnalysis";
        private int _dashboardCardSizeStep = 0;
        private const int DashboardMaxContentWidth = 1680;
        private const int DashboardSafeLeftInset = 32;

        private int DashboardCardDelta => _dashboardCardSizeStep * 12;

        private int DashboardCardHeight(int compactHeight, int regularHeight)
        {
            int baseHeight = IsCompactDashboard() ? compactHeight : regularHeight;
            return Math.Max(56, baseHeight + DashboardCardDelta);
        }

        private bool IsCompactDashboard()
        {
            if (LayoutScaler.IsLaptopFitModeEnabled(this))
                return true;

            Rectangle workArea = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : SystemInformation.WorkingArea;
            int hostWidth = _dashboardHost != null && _dashboardHost.ClientSize.Width > 0
                ? Math.Min(_dashboardHost.ClientSize.Width, DashboardMaxContentWidth)
                : ClientSize.Width;
            return workArea.Width <= 1366 || workArea.Height <= 760 || hostWidth < 1180;
        }

        private int DashHeight(int regular, int compact)
        {
            return IsCompactDashboard() ? compact : regular;
        }

        public DashboardForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = BgPage;
            this.AutoScroll = false;
            Controls.Add(new Label
            {
                Text = "Loading dashboard...",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextMid,
                AutoSize = true,
                Location = new Point(24, 24)
            });
            EnableDeferredLoad(
                async () =>
                {
                    RenderDashboard();
                    await Task.Run(() => LoadData());
                    if (IsDisposed)
                        return;
                    RenderDashboard();
                },
                ex =>
                {
                    Controls.Clear();
                    Controls.Add(new Label
                    {
                        Text = "Dashboard load error: " + ex.Message,
                        Font = new Font("Segoe UI", 10, FontStyle.Bold),
                        ForeColor = AccentRed,
                        AutoSize = true,
                        Location = new Point(24, 24)
                    });
                });
        }

        private void RenderDashboard()
        {
            if (IsDisposed)
                return;

            Controls.Clear();
            BuildUI();
            DS.ApplyTheme(this);
            UIHelper.ApplyInputStyles(Controls);
            if (_dashboardSearchText != null)
                _dashboardSearchText.BorderStyle = BorderStyle.None;
            AutoScroll = false;
            if (_dashboardHost != null)
                _dashboardHost.AutoScroll = true;
        }

        // ══════════════════════════════════════════════════════════════
        //  DATA LOAD
        // ══════════════════════════════════════════════════════════════
        private void LoadData()
        {
            try { _mrr              = _contractSvc.GetMonthlyRecurringRevenue();   } catch { }
            try { _activeContracts  = _contractSvc.GetActiveContractCount();       } catch { }
            try { _expiringContracts= _contractSvc.GetExpiringContractCount(90);   } catch { }
            try { _pendingAmount    = _invoiceSvc.GetTotalPendingAmount();          } catch { }
            try { _collectedMonth   = _paymentSvc.GetTotalCollectedThisMonth();    } catch { }
            try { _vendorCount      = _vendorSvc.GetActiveCount();                 } catch { }
            try { _purchaseSpend    = _purchaseSvc.GetTotalSpendThisMonth();       } catch { }
            try { _purchasePaymentsOverdue = _purchaseSvc.GetOverduePaymentsCountFresh(); } catch { }
            try { _purchaseOrders   = _purchaseSvc.GetAllFresh();                         } catch { }
            try { _stockValue       = _inventorySvc.GetTotalStockValue();          } catch { }
            try { _lowStockCount    = _inventorySvc.GetLowStockCount();            } catch { }
            try { _inventoryItems   = _inventorySvc.GetAll();                      } catch { }
            try { _vendors          = _vendorSvc.GetAll();                         } catch { }
            try { _clients          = _clientSvc.GetAllClients();                  } catch { }
            try { _jobs             = _jobSvc.GetAll();                            } catch { }
            try { _payments         = _paymentSvc.GetAllPayments();                } catch { }
            try { _quotes           = _tenderSvc.GetAll();                         } catch { }
            try { _employeeCount    = _employeeSvc.GetActiveCount();              } catch { }
            try { _payrollSnapshot  = _payrollSvc.GetDashboardSnapshot();         } catch { }
            try { _jobRevenueMonth  = _jobSvc.GetRevenueThisMonth();              } catch { }
            try { _jobProfitMonth   = _jobSvc.GetProfitThisMonth();               } catch { }
            try { _avgRevenuePerJob = _jobSvc.GetAverageRevenuePerCompletedJob(); } catch { }
            try { _jobPendingCount  = _jobSvc.GetPendingCount();                  } catch { }
            try { _jobProgressCount = _jobSvc.GetInProgressCount();               } catch { }
            try { _jobCompletedCount= _jobSvc.GetCompletedCount();                } catch { }
            try { LoadBackupHealth();                                             } catch { }

            try
            {
                _expiringList = _contractSvc.GetExpiringContractsInNextDays(90);
            }
            catch { }

            try
            {
                List<Invoice> all = _invoiceSvc.GetAllInvoices();
                _overdueList    = all.Where(i =>
                    i.DueDate < DateTime.Today &&
                    !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _overdueCount   = _overdueList.Count;
                _recentInvoices = all.OrderByDescending(i => i.InvoiceDate).Take(8).ToList();
            }
            catch { }
        }

        private void LoadBackupHealth()
        {
            FileInfo latest = new BackupService().GetBackups().FirstOrDefault();
            if (latest == null)
            {
                _backupStatusText = "No backup";
                _backupSubText = "Create one today";
                _backupAccent = AccentRed;
                return;
            }

            TimeSpan age = DateTime.Now - latest.LastWriteTime;
            if (age.TotalHours < 24)
            {
                _backupStatusText = "Protected";
                _backupSubText = "Last backup " + FormatBackupAge(age);
                _backupAccent = AccentGreen;
            }
            else if (age.TotalHours < 72)
            {
                _backupStatusText = "Due soon";
                _backupSubText = "Last backup " + FormatBackupAge(age);
                _backupAccent = AccentOrange;
            }
            else
            {
                _backupStatusText = "Overdue";
                _backupSubText = "Last backup " + FormatBackupAge(age);
                _backupAccent = AccentRed;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  UI BUILD — ServiceTitan Layout
        // ══════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            if (_dashboardHost == null || _dashboardHost.IsDisposed)
            {
                _dashboardHost = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = BgPage,
                    AutoScroll = true
                };
                _dashboardHost.Resize += (s, e) =>
                {
                    LayoutDashboardContent();
                    if (IsCompactDashboard())
                        BeginInvoke((Action)ResetDashboardScrollToTop);
            };
        }

            if (_dashboardContent == null || _dashboardContent.IsDisposed)
            {
                _dashboardContent = new Panel
                {
                    BackColor = BgPage,
                    AutoScroll = false
                };
            }

            _dashboardHost.SuspendLayout();
            _dashboardHost.Controls.Clear();
            _dashboardContent.SuspendLayout();
            _dashboardContent.Controls.Clear();

            // Controls added in REVERSE visual order for DockStyle.Top

            // ── 5. Pipeline / expiring contracts table ─────────────────
            Panel pipelineSection = BuildPipelineSection();

            // ── 4. Recent Invoices table ───────────────────────────────
            Panel invoicesSection = BuildRecentInvoicesSection();

            // ── 3. Ops row (secondary KPIs) ────────────────────────────
            Panel opsSection = BuildOpsRow();

            // ── 2.5 Owner row ──────────────────────────────────────────
            Panel ownerSection = BuildOwnerRow();
            Panel purchaseAlertsSection = BuildPurchaseOrderAlertsSection();
            Panel payrollSection = BuildPayrollSection();

            // ── 2. KPI scorecard row ────────────────────────────────────
            Panel kpiSection = BuildKpiRow();
            Panel filterSection = BuildRevenueFilterBar();
            Panel coreWorkflowSection = BuildCoreWorkflowSection();

            // ── 1. Page header (header + quick actions) ─────────────────
            Panel pageHeader = BuildPageHeader();

            // Add in reverse visual order
            _dashboardContent.Controls.Add(pipelineSection);
            _dashboardContent.Controls.Add(invoicesSection);
            _dashboardContent.Controls.Add(opsSection);
            _dashboardContent.Controls.Add(purchaseAlertsSection);
            _dashboardContent.Controls.Add(payrollSection);
            _dashboardContent.Controls.Add(ownerSection);
            _dashboardContent.Controls.Add(kpiSection);
            _dashboardContent.Controls.Add(filterSection);
            _dashboardContent.Controls.Add(coreWorkflowSection);
            _dashboardContent.Controls.Add(pageHeader);
            _dashboardContent.ResumeLayout();
            _dashboardHost.Controls.Add(_dashboardContent);
            _dashboardHost.ResumeLayout();

            if (!Controls.Contains(_dashboardHost))
                Controls.Add(_dashboardHost);
            _dashboardHost.Visible = true;
            _dashboardHost.BringToFront();
            LayoutDashboardContent();
            ResetDashboardScrollToTop();
            if (IsHandleCreated)
                BeginInvoke((Action)ResetDashboardScrollToTop);

        }

        // ══════════════════════════════════════════════════════════════
        //  PAGE HEADER
        // ══════════════════════════════════════════════════════════════
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.K))
            {
                FocusDashboardSearch();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ResetDashboardScrollToTop()
        {
            if (_dashboardHost == null || _dashboardContent == null || _dashboardHost.IsDisposed || _dashboardContent.IsDisposed)
                return;

            _dashboardHost.AutoScrollPosition = new Point(0, 0);
            _dashboardContent.Location = new Point(0, 0);
        }

        private void LayoutDashboardContent()
        {
            if (_dashboardHost == null || _dashboardContent == null || _dashboardHost.IsDisposed || _dashboardContent.IsDisposed)
                return;

            int viewportWidth = GetDashboardVisibleWidth();

            int outerGutter = 8;
            int usableWidth = Math.Max(720, viewportWidth - (outerGutter * 2) - 2);
            int maxContentWidth = DashboardMaxContentWidth;
            int contentWidth = Math.Min(maxContentWidth, usableWidth);
            _dashboardContent.Location = new Point(outerGutter, 0);
            _dashboardContent.Width = contentWidth;
            _dashboardHost.HorizontalScroll.Enabled = false;
            _dashboardHost.HorizontalScroll.Visible = false;

            int contentHeight = _dashboardContent.Controls
                .Cast<Control>()
                .Where(control => control.Visible)
                .Sum(control => control.Height);

            _dashboardContent.Height = Math.Max(_dashboardHost.ClientSize.Height, contentHeight + 24);
            _dashboardHost.AutoScrollMinSize = new Size(0, _dashboardContent.Height);
        }

        private int GetDashboardVisibleWidth()
        {
            int viewportWidth = _dashboardHost != null && _dashboardHost.ClientSize.Width > 0
                ? _dashboardHost.ClientSize.Width
                : ClientSize.Width;

            try
            {
                if (_dashboardHost != null && _dashboardHost.IsHandleCreated)
                {
                    Point screenPoint = _dashboardHost.PointToScreen(Point.Empty);
                    Rectangle controlWorkArea = Screen.FromControl(_dashboardHost).WorkingArea;
                    int controlVisibleWidth = controlWorkArea.Right - screenPoint.X;
                    if (controlVisibleWidth > 0)
                        viewportWidth = Math.Min(viewportWidth, controlVisibleWidth);

                    Rectangle primaryWorkArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
                    if (screenPoint.X >= primaryWorkArea.Left && screenPoint.X < primaryWorkArea.Right)
                    {
                        int primaryVisibleWidth = primaryWorkArea.Right - screenPoint.X;
                        if (primaryVisibleWidth > 0)
                            viewportWidth = Math.Min(viewportWidth, primaryVisibleWidth);
                    }
                }
            }
            catch { }

            if (_dashboardHost != null && _dashboardHost.VerticalScroll.Visible)
                viewportWidth = Math.Max(0, viewportWidth - SystemInformation.VerticalScrollBarWidth);

            return viewportWidth;
        }

        private Panel BuildPageHeader()
        {
            bool compact = IsCompactDashboard();
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = compact ? 102 : 112,
                BackColor = BgPage,
                Padding = new Padding(compact ? 8 : 16, compact ? 8 : 12, compact ? 8 : 16, compact ? 8 : 12)
            };

            Panel topBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            DS.Rounded(topBar, compact ? 10 : 14);
            topBar.Paint += (s, e) => PaintModernHeaderBackground(topBar, e);

            Panel menuButton = MakeGradientHeaderButton(compact ? new Size(54, 54) : new Size(66, 66));
            Label menuGlyph = new Label
            {
                Text = "☰",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", compact ? 18f : 22f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            menuButton.Controls.Add(menuGlyph);
            menuButton.Click += (s, e) => ToggleShellSidebar();
            menuGlyph.Click += (s, e) => ToggleShellSidebar();

            Label title = new Label
            {
                Text = "Dashboard",
                Font = new Font("Segoe UI", compact ? 15.5f : 18f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoSize = false,
                Height = compact ? 28 : 32,
                TextAlign = ContentAlignment.BottomLeft
            };
            Label subtitle = new Label
            {
                Text = "Overview of your business performance",
                Font = new Font("Segoe UI", compact ? 8.5f : 9.5f),
                ForeColor = DS.Slate500,
                AutoSize = false,
                Height = compact ? 18 : 22,
                TextAlign = ContentAlignment.TopLeft
            };

            Panel divider1 = MakeVerticalDivider();
            Panel divider2 = MakeVerticalDivider();

            Panel searchBox = new Panel
            {
                BackColor = Color.White,
                Height = compact ? 38 : 44
            };
            DS.Rounded(searchBox, 10);
            searchBox.Paint += (s, e) => PaintSearchBox(searchBox, e);
            Label searchIcon = new Label
            {
                Text = "⌕",
                Font = new Font("Segoe UI Symbol", compact ? 14f : 16f, FontStyle.Bold),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(12, 0),
                Size = new Size(28, searchBox.Height)
            };
            _dashboardSearchText = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Text = "Search app: clients, jobs, invoices, purchases...",
                ForeColor = DS.Slate500,
                Font = new Font("Segoe UI", compact ? 9f : 10.5f),
                BackColor = Color.White
            };
            Label shortcutHint = new Label
            {
                Text = "Ctrl + K",
                Font = new Font("Segoe UI", compact ? 8f : 9f, FontStyle.Bold),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = DS.Slate50,
                Size = compact ? new Size(60, 24) : new Size(72, 28)
            };
            DS.Rounded(shortcutHint, 6);
            searchBox.Controls.Add(searchIcon);
            searchBox.Controls.Add(_dashboardSearchText);
            searchBox.Controls.Add(shortcutHint);
            _dashboardSearchText.GotFocus += (s, e) =>
            {
                if (_dashboardSearchText.ForeColor == DS.Slate500)
                {
                    _dashboardSearchText.Text = string.Empty;
                    _dashboardSearchText.ForeColor = DS.Slate900;
                }
            };
            _dashboardSearchText.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_dashboardSearchText.Text))
                {
                    _dashboardSearchText.Text = "Search app: clients, jobs, invoices, purchases...";
                    _dashboardSearchText.ForeColor = DS.Slate500;
                }
            };
            _dashboardSearchText.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    HandleDashboardSearch(_dashboardSearchText.Text);
                }
            };

            Label calendar = MakeHeaderMetaLabel("▣", compact);
            _headerDateLabel = MakeHeaderMetaLabel(DateTime.Now.ToString("dd/MM/yyyy"), compact);
            Label clock = MakeHeaderMetaLabel("◷", compact);
            _headerTimeLabel = MakeHeaderMetaLabel(DateTime.Now.ToString("hh:mm tt"), compact);

            Panel customize = MakeGradientHeaderButton(compact ? new Size(118, 38) : new Size(146, 44));
            Label customizeText = new Label
            {
                Text = "⚙  Customize",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", compact ? 8.5f : 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            customize.Controls.Add(customizeText);
            customize.Click += (s, e) => OnNavigate?.Invoke(8);
            customizeText.Click += (s, e) => OnNavigate?.Invoke(8);

            string currentDisplayName = SessionManager.CurrentUser?.DisplayName ?? "Guest";
            string currentRole = SessionManager.CurrentUser?.RoleName ?? "No Role";
            string userMenuText = compact ? ShortUserName(currentDisplayName) : currentDisplayName;

            Panel avatar = MakeGradientHeaderButton(compact ? new Size(38, 38) : new Size(44, 44));
            Label avatarText = new Label
            {
                Text = UserInitials(currentDisplayName),
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", compact ? 10f : 11f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            avatar.Controls.Add(avatarText);
            Label user = new Label
            {
                Text = userMenuText + " ˅",
                Font = new Font("Segoe UI", compact ? 8.5f : 9.5f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            avatar.Click += (s, e) => ShowUserMenu(avatar);
            avatarText.Click += (s, e) => ShowUserMenu(avatar);
            user.Click += (s, e) => ShowUserMenu(user);
            _dashboardToolTip.SetToolTip(user, currentDisplayName + " (" + currentRole + ")");
            StartHeaderClock();

            topBar.Resize += (s, e) =>
            {
                int pad = compact ? 12 : 18;
                int centerY = topBar.Height / 2;
                menuButton.Visible = false;
                title.Visible = false;
                subtitle.Visible = false;
                divider1.Visible = false;
                int titleLeft = pad + (compact ? 4 : 10);
                title.Location = new Point(titleLeft, compact ? 14 : 20);
                title.Size = new Size(compact ? 150 : 210, compact ? 26 : 32);
                subtitle.Location = new Point(titleLeft, title.Bottom - 1);
                subtitle.Size = new Size(compact ? 190 : 260, compact ? 18 : 22);
                divider1.Location = new Point(title.Left + title.Width + (compact ? 10 : 22), centerY - divider1.Height / 2);

                int right = topBar.Width - pad;
                user.Size = new Size(compact ? 64 : 118, compact ? 38 : 44);
                user.Location = new Point(right - user.Width, centerY - user.Height / 2);
                avatar.Location = new Point(user.Left - avatar.Width - 8, centerY - avatar.Height / 2);
                customize.Location = new Point(avatar.Left - customize.Width - (compact ? 10 : 16), centerY - customize.Height / 2);

                int timeWidth = compact ? 62 : 82;
                _headerTimeLabel.Size = new Size(timeWidth, compact ? 26 : 30);
                clock.Size = new Size(compact ? 20 : 24, _headerTimeLabel.Height);
                _headerTimeLabel.Location = new Point(customize.Left - _headerTimeLabel.Width - (compact ? 10 : 18), centerY - _headerTimeLabel.Height / 2);
                clock.Location = new Point(_headerTimeLabel.Left - clock.Width - 4, _headerTimeLabel.Top);
                _headerDateLabel.Size = new Size(compact ? 86 : 112, _headerTimeLabel.Height);
                calendar.Size = new Size(compact ? 20 : 24, _headerTimeLabel.Height);
                _headerDateLabel.Location = new Point(clock.Left - _headerDateLabel.Width - (compact ? 8 : 14), _headerTimeLabel.Top);
                calendar.Location = new Point(_headerDateLabel.Left - calendar.Width - 4, _headerTimeLabel.Top);
                divider2.Location = new Point(calendar.Left - (compact ? 8 : 14), centerY - divider2.Height / 2);

                int searchLeft = pad + (compact ? 4 : 10);
                int searchRight = divider2.Left - (compact ? 10 : 18);
                searchBox.Location = new Point(searchLeft, centerY - searchBox.Height / 2);
                searchBox.Width = Math.Max(compact ? 210 : 320, searchRight - searchLeft);
                bool showMeta = searchBox.Width >= (compact ? 230 : 300);
                calendar.Visible = _headerDateLabel.Visible = clock.Visible = _headerTimeLabel.Visible = showMeta;
                if (!showMeta)
                {
                    searchRight = customize.Left - (compact ? 10 : 18);
                    searchBox.Width = Math.Max(compact ? 220 : 320, searchRight - searchLeft);
                }
                shortcutHint.Location = new Point(searchBox.Width - shortcutHint.Width - 10, (searchBox.Height - shortcutHint.Height) / 2);
                searchIcon.Height = searchBox.Height;
                _dashboardSearchText.Location = new Point(searchIcon.Right + 8, (searchBox.Height - _dashboardSearchText.Height) / 2 + 1);
                _dashboardSearchText.Width = Math.Max(60, shortcutHint.Left - _dashboardSearchText.Left - 8);
            };

            topBar.Controls.Add(title);
            topBar.Controls.Add(subtitle);
            topBar.Controls.Add(divider1);
            topBar.Controls.Add(searchBox);
            topBar.Controls.Add(divider2);
            topBar.Controls.Add(calendar);
            topBar.Controls.Add(_headerDateLabel);
            topBar.Controls.Add(clock);
            topBar.Controls.Add(_headerTimeLabel);
            topBar.Controls.Add(customize);
            topBar.Controls.Add(avatar);
            topBar.Controls.Add(user);

            _dashboardTabButtons.Clear();
            header.Controls.Add(topBar);
            return header;
        }

        private void PaintModernHeaderBackground(Panel panel, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using (GraphicsPath path = DS.RoundedRect(bounds, 14))
            using (SolidBrush fill = new SolidBrush(Color.White))
            {
                e.Graphics.FillPath(fill, path);
            }
        }

        private Panel MakeGradientHeaderButton(Size size)
        {
            Panel button = new Panel
            {
                Size = size,
                Cursor = Cursors.Hand,
                BackColor = DS.Primary600
            };
            DS.Rounded(button, 10);
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
                using (GraphicsPath path = DS.RoundedRect(rect, 10))
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(37, 99, 235), Color.FromArgb(109, 40, 217), 35f))
                {
                    e.Graphics.FillPath(brush, path);
                }
            };
            button.MouseEnter += (s, e) => button.BackColor = Color.FromArgb(67, 56, 202);
            button.MouseLeave += (s, e) => button.BackColor = DS.Primary600;
            return button;
        }

        private Panel MakeVerticalDivider()
        {
            return new Panel
            {
                BackColor = Color.FromArgb(226, 232, 240),
                Size = new Size(1, 42)
            };
        }

        private Label MakeHeaderMetaLabel(string text, bool compact)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", compact ? 8.5f : 9.5f, FontStyle.Regular),
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
        }

        private void PaintSearchBox(Panel panel, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using (GraphicsPath path = DS.RoundedRect(rect, 10))
            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen border = new Pen(Color.FromArgb(226, 232, 240)))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }

        private void StartHeaderClock()
        {
            if (_headerClockTimer != null)
                _headerClockTimer.Stop();

            _headerClockTimer = new Timer { Interval = 30000 };
            _headerClockTimer.Tick += (s, e) => UpdateHeaderClock();
            _headerClockTimer.Start();
            UpdateHeaderClock();
        }

        private void UpdateHeaderClock()
        {
            if (_headerDateLabel == null || _headerTimeLabel == null)
                return;

            DateTime now = DateTime.Now;
            _headerDateLabel.Text = now.ToString("dd/MM/yyyy");
            _headerTimeLabel.Text = now.ToString("hh:mm tt");
        }

        private void FocusDashboardSearch()
        {
            if (_dashboardSearchText == null)
                return;

            _dashboardSearchText.Focus();
            _dashboardSearchText.SelectAll();
        }

        private void ToggleShellSidebar()
        {
            MainForm main = FindForm() as MainForm;
            if (main != null)
            {
                main.ToggleSidebarVisibility();
                return;
            }

            FocusDashboardSearch();
        }

        private void ShowUserMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Profile", null, (s, e) => OnNavigate?.Invoke(8));
            menu.Items.Add("Settings", null, (s, e) => OnNavigate?.Invoke(8));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Logout", null, (s, e) =>
            {
                try
                {
                    new AuthService().Logout();
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("Dashboard.Logout", ex);
                }
                FindForm()?.Close();
            });
            menu.Show(owner, new Point(0, owner.Height + 4));
        }

        private static string UserInitials(string displayName)
        {
            string[] parts = (displayName ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "NA";
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
        }

        private static string ShortUserName(string displayName)
        {
            string[] parts = (displayName ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "User";
            return parts[0];
        }

        private Panel BuildCoreWorkflowSection()
        {
            bool compact = IsCompactDashboard();
            int viewportWidth = GetDashboardVisibleWidth();
            int columns = viewportWidth < 1180 ? 3 : 6;
            int rows = columns == 3 ? 2 : 1;
            int cardHeight = compact ? 86 : 96;
            int labelHeight = compact ? 36 : 42;

            Panel wrap = new Panel
            {
                Dock = DockStyle.Top,
                Height = labelHeight + (cardHeight * rows) + (rows > 1 ? 8 : 0) + 10,
                BackColor = BgPage,
                Padding = new Padding(DashboardSafeLeftInset, 8, 16, 8)
            };

            Label label = new Label
            {
                Text = "CORE SERVICE WORKFLOW",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMid,
                Location = new Point(DashboardSafeLeftInset, 6),
                AutoSize = true
            };
            Label helper = new Label
            {
                Text = "Move daily work from customer request to job, invoice, payment, AMC renewal, and backup.",
                Font = new Font("Segoe UI", compact ? 7.5f : 8.5f),
                ForeColor = TextLight,
                Location = new Point(DashboardSafeLeftInset, 22),
                Size = new Size(Math.Max(320, viewportWidth - DashboardSafeLeftInset - 16), 18),
                AutoEllipsis = true
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                ColumnCount = columns,
                RowCount = rows,
                BackColor = BgPage,
                Location = new Point(DashboardSafeLeftInset, labelHeight),
                Size = new Size(Math.Max(720, viewportWidth - DashboardSafeLeftInset - 16), cardHeight * rows + (rows > 1 ? 8 : 0)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            for (int i = 0; i < columns; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));

            int openJobs = _jobPendingCount + _jobProgressCount;
            int activeClients = _clients.Count(c => c.IsActive);
            AddWorkflowCard(grid, 0, columns, "1. Customers", activeClients.ToString(), _clients.Count + " total clients", AccentBlue, "Open Clients", () => OnNavigate?.Invoke(1));
            AddWorkflowCard(grid, 1, columns, "2. Jobs", openJobs.ToString(), _jobPendingCount + " pending, " + _jobProgressCount + " in progress", openJobs > 0 ? AccentOrange : AccentGreen, "Open Jobs", () => OnNavigate?.Invoke(15));
            AddWorkflowCard(grid, 2, columns, "3. Invoices", FormatLakhs(_pendingAmount), _overdueCount > 0 ? _overdueCount + " overdue" : "No overdue invoices", _overdueCount > 0 ? AccentRed : AccentGreen, "Open Invoices", () => OnNavigate?.Invoke(3));
            AddWorkflowCard(grid, 3, columns, "4. Payments", FormatLakhs(_collectedMonth), "collected this month", AccentTeal, "Open Payments", () => OnNavigate?.Invoke(4));
            AddWorkflowCard(grid, 4, columns, "5. AMC Renewals", _expiringContracts.ToString(), _expiringContracts > 0 ? "due in 90 days" : "all renewals clear", _expiringContracts > 0 ? AccentOrange : AccentGreen, "Open AMC", () => OnNavigate?.Invoke(2));
            AddWorkflowCard(grid, 5, columns, "6. Backup", _backupStatusText, _backupSubText, _backupAccent, "Backup", () => OnNavigate?.Invoke(8));

            wrap.Resize += (s, e) =>
            {
                grid.Width = Math.Max(0, wrap.ClientSize.Width - DashboardSafeLeftInset - 16);
                helper.Width = grid.Width;
            };

            wrap.Controls.Add(grid);
            wrap.Controls.Add(helper);
            wrap.Controls.Add(label);
            return wrap;
        }

        private void AddWorkflowCard(TableLayoutPanel grid, int index, int columns, string title, string value, string sub, Color accent, string actionText, Action action)
        {
            grid.Controls.Add(MakeWorkflowCard(title, value, sub, accent, actionText, action), index % columns, index / columns);
        }

        private Panel MakeWorkflowCard(string title, string value, string sub, Color accent, string actionText, Action action)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgCard,
                Margin = new Padding(5, 0, 5, 8),
                Cursor = Cursors.Hand
            };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (Pen pen = new Pen(BorderLine, 1))
                    e.Graphics.DrawPath(pen, path);
                using (SolidBrush brush = new SolidBrush(accent))
                    e.Graphics.FillRectangle(brush, 0, 0, 4, card.Height);
            };

            Label titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = TextMid,
                Location = new Point(14, 10),
                Size = new Size(150, 16),
                AutoEllipsis = true
            };
            Label valueLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = accent,
                Location = new Point(14, 29),
                Size = new Size(150, 24),
                AutoEllipsis = true
            };
            Label subLabel = new Label
            {
                Text = sub,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = TextLight,
                Location = new Point(14, 55),
                Size = new Size(150, 16),
                AutoEllipsis = true
            };
            Label actionLabel = new Label
            {
                Text = actionText,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = accent,
                Location = new Point(14, 74),
                Size = new Size(150, 16),
                AutoEllipsis = true
            };

            card.Resize += (s, e) =>
            {
                int width = Math.Max(40, card.ClientSize.Width - 26);
                titleLabel.Width = width;
                valueLabel.Width = width;
                subLabel.Width = width;
                actionLabel.Width = width;
            };

            EventHandler open = (s, e) => action?.Invoke();
            card.Click += open;
            titleLabel.Click += open;
            valueLabel.Click += open;
            subLabel.Click += open;
            actionLabel.Click += open;
            card.MouseEnter += (s, e) => card.BackColor = DS.Primary50;
            card.MouseLeave += (s, e) => card.BackColor = BgCard;

            card.Controls.Add(titleLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(subLabel);
            card.Controls.Add(actionLabel);
            ConfigureDashboardDataLabel(valueLabel);
            ConfigureDashboardDataLabel(subLabel);
            return card;
        }

        private Panel BuildRevenueFilterBar()
        {
            Panel section = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = BgPage,
                Padding = new Padding(DashboardSafeLeftInset, 6, 16, 6)
            };
            Button filter = MakeLinkBtn("Revenue Snapshot   v", TextDark);
            filter.Width = 180;
            filter.Height = 32;
            filter.Location = new Point(DashboardSafeLeftInset, 8);
            filter.TextAlign = ContentAlignment.MiddleLeft;
            filter.Click += (s, e) => ShowRevenueSnapshotMenu(filter);
            section.Controls.Add(filter);
            return section;
        }

        private void ShowRevenueSnapshotMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Open Reports", null, (s, e) => OnNavigate?.Invoke(7));
            menu.Items.Add("Open Invoices", null, (s, e) => OnNavigate?.Invoke(3));
            menu.Items.Add("Open Payments", null, (s, e) => OnNavigate?.Invoke(4));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Refresh Dashboard", null, (s, e) => RenderDashboard());
            menu.Show(owner, new Point(0, owner.Height + 4));
        }

        // ══════════════════════════════════════════════════════════════
        //  KPI SCORECARD ROW
        // ══════════════════════════════════════════════════════════════
        private void ResizeDashboardCards(int direction)
        {
            int nextStep = Math.Max(-1, Math.Min(3, _dashboardCardSizeStep + direction));
            if (nextStep == _dashboardCardSizeStep)
                return;

            _dashboardCardSizeStep = nextStep;
            RenderDashboard();
            ResetDashboardScrollToTop();
        }

        private void PaintDashboardResizeArrow(Button button, PaintEventArgs e, bool grow)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int centerX = button.ClientSize.Width / 2;
            int centerY = button.ClientSize.Height / 2;
            int halfWidth = Math.Max(5, button.ClientSize.Width / 5);
            int halfHeight = Math.Max(4, button.ClientSize.Height / 6);
            Point[] points = grow
                ? new[]
                {
                    new Point(centerX, centerY - halfHeight - 2),
                    new Point(centerX - halfWidth, centerY + halfHeight),
                    new Point(centerX + halfWidth, centerY + halfHeight)
                }
                : new[]
                {
                    new Point(centerX - halfWidth, centerY - halfHeight),
                    new Point(centerX + halfWidth, centerY - halfHeight),
                    new Point(centerX, centerY + halfHeight + 2)
                };

            using (SolidBrush brush = new SolidBrush(button.Enabled ? AccentIndigo : TextLight))
                e.Graphics.FillPolygon(brush, points);
        }

        private Panel BuildKpiRow()
        {
            bool compact = IsCompactDashboard();
            int viewportWidth = GetDashboardVisibleWidth();
            int hostClientWidth = _dashboardHost != null && _dashboardHost.ClientSize.Width > 0
                ? _dashboardHost.ClientSize.Width
                : ClientSize.Width;
            bool twoColumnCards = hostClientWidth < 1400 || viewportWidth < 1400 || Screen.FromControl(this).Bounds.Width < 1400;
            bool wrapCards = twoColumnCards || viewportWidth < 1500;
            int kpiColumns = twoColumnCards ? 2 : (wrapCards ? 3 : 6);
            int kpiRows = (int)Math.Ceiling(6d / kpiColumns);
            int kpiCardHeight = DashboardCardHeight(90, 90);
            Panel wrap = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = wrapCards ? (kpiCardHeight * kpiRows) + (compact ? 40 : 44) : kpiCardHeight + (compact ? 36 : 40),
                BackColor = BgPage,
                Padding   = new Padding(DashboardSafeLeftInset, compact ? 24 : 28, 16, 8)
            };

            // Section label
            Label lbl = new Label
            {
                Text      = "REVENUE SNAPSHOT",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMid,
                Location  = new Point(DashboardSafeLeftInset, 4),
                AutoSize  = true
            };
            wrap.Controls.Add(lbl);

            TableLayoutPanel grid = new TableLayoutPanel
            {
                ColumnCount = kpiColumns,
                RowCount    = kpiRows,
                Dock        = DockStyle.Fill,
                BackColor   = BgPage,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            for (int i = 0; i < grid.ColumnCount; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / grid.ColumnCount));
            for (int i = 0; i < grid.RowCount; i++)
                grid.RowStyles.Add(wrapCards ? new RowStyle(SizeType.Absolute, kpiCardHeight) : new RowStyle(SizeType.Percent, 100f));

            // Card 1: MRR
            var cardMrr = MakeScoreCard(
                "Monthly Recurring Revenue",
                FormatLakhs(_mrr),
                _mrr.ToString("N0") + " /mo",
                AccentGreen,
                () => OpenAnalysis(BuildContractPortfolioAnalysis()),
                "dashboard_monthly_recurring_revenue");
            AddKpiCard(grid, cardMrr, 0, kpiColumns);

            // Card 2: Cash collected
            var cardCash = MakeScoreCard(
                "Cash Collected",
                FormatLakhs(_collectedMonth),
                _collectedMonth.ToString("N0") + " rcvd",
                AccentTeal,
                () => OpenAnalysis(BuildCashCollectionAnalysis()),
                "dashboard_cash_collected");
            AddKpiCard(grid, cardCash, 1, kpiColumns);

            // Card 3: Pending invoices
            Color pendingAccent = _overdueCount > 0 ? AccentRed : AccentPurple;
            var cardPending = MakeScoreCard(
                "Pending Invoices",
                FormatLakhs(_pendingAmount),
                _overdueCount > 0 ? _overdueCount + " OVERDUE" : "All current",
                pendingAccent,
                () => OpenAnalysis(BuildInvoicePortfolioAnalysis(true)),
                "dashboard_pending_invoices");
            AddKpiCard(grid, cardPending, 2, kpiColumns);

            // Card 4: Active contracts
            var cardContracts = MakeScoreCard(
                "Live Contracts",
                _activeContracts.ToString(),
                "ARR: " + FormatLakhs(_mrr * 12),
                AccentBlue,
                () => OpenAnalysis(BuildContractPortfolioAnalysis()),
                "dashboard_live_contracts");
            AddKpiCard(grid, cardContracts, 3, kpiColumns);

            // Card 5: Expiring
            Color expAccent = _expiringContracts > 0 ? AccentOrange : AccentGreen;
            string expSub   = _expiringContracts > 0 ? "Renew now" : "All clear";
            var cardExp = MakeScoreCard(
                "Renewals (90 days)",
                _expiringContracts.ToString(),
                expSub,
                expAccent,
                () => OpenAnalysis(BuildRenewalAnalysis()),
                "dashboard_renewals_90_days");
            AddKpiCard(grid, cardExp, 4, kpiColumns);

            // Card 6: Employees
            var cardEmp = MakeScoreCard(
                "Active Employees",
                _employeeCount.ToString(),
                "field & office",
                AccentPurple,
                () => OpenAnalysis(BuildJobPerformanceAnalysis()),
                "dashboard_active_employees");
            AddKpiCard(grid, cardEmp, 5, kpiColumns);

            wrap.Controls.Add(grid);
            lbl.BringToFront();
            return wrap;
        }

        private void AddKpiCard(TableLayoutPanel grid, Control card, int index, int columns)
        {
            grid.Controls.Add(card, index % columns, index / columns);
        }

        // ══════════════════════════════════════════════════════════════
        //  OPS ROW
        // ══════════════════════════════════════════════════════════════
        private Panel BuildOpsRow()
        {
            bool compact = IsCompactDashboard();
            int cardHeight = DashboardCardHeight(64, 80);
            Panel wrap = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = cardHeight + (compact ? 12 : 16),
                BackColor = BgPage,
                Padding   = new Padding(DashboardSafeLeftInset, 4, 16, 4)
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                ColumnCount = 5,
                RowCount    = 1,
                Height      = cardHeight,
                Location    = new Point(DashboardSafeLeftInset, compact ? 6 : 8),
                Width       = Math.Max(720, GetDashboardVisibleWidth() - DashboardSafeLeftInset - 16),
                BackColor   = BgPage,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            for (int i = 0; i < 5; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            wrap.Resize += (s, e) =>
            {
                grid.Width = Math.Max(0, wrap.ClientSize.Width - DashboardSafeLeftInset - 16);
            };

            grid.Controls.Add(MakeInfoCard("VENDORS",        _vendorCount.ToString(),      "active suppliers",          DS.Indigo600, () => OpenAnalysis(BuildVendorPortfolioAnalysis()), "dashboard_vendors"), 0, 0);
            grid.Controls.Add(MakeInfoCard("PURCHASE SPEND", FormatLakhs(_purchaseSpend),  "this month",                Color.FromArgb(211, 84, 0), () => OpenAnalysis(BuildPurchasePortfolioAnalysis(false)), "dashboard_purchase_spend"), 1, 0);
            grid.Controls.Add(MakeInfoCard("STOCK VALUE",    FormatLakhs(_stockValue),
                _lowStockCount > 0 ? _lowStockCount + " items low" : "All stocked",
                _lowStockCount > 0 ? AccentRed : AccentGreen, () => OpenAnalysis(BuildInventoryPortfolioAnalysis(false)), "dashboard_stock_value"), 2, 0);
            grid.Controls.Add(MakeInfoCard("LOW STOCK ITEMS",_lowStockCount.ToString(),
                _lowStockCount > 0 ? "Reorder required" : "All stocked",
                _lowStockCount > 0 ? AccentRed : AccentGreen, () => OpenAnalysis(BuildInventoryPortfolioAnalysis(true)), "dashboard_low_stock_items"), 3, 0);
            grid.Controls.Add(MakePendingPaymentsCard(), 4, 0);

            wrap.Controls.Add(grid);
            return wrap;
        }

        private Panel BuildOwnerRow()
        {
            bool compact = IsCompactDashboard();
            int cardHeight = DashboardCardHeight(64, 80);
            Panel wrap = new Panel
            {
                Dock = DockStyle.Top,
                Height = cardHeight + (compact ? 12 : 16),
                BackColor = BgPage,
                Padding = new Padding(DashboardSafeLeftInset, 4, 16, 4)
            };

            TableLayoutPanel grid = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 1,
                Dock = DockStyle.Fill,
                BackColor = BgPage,
                Margin = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            grid.Controls.Add(MakeInfoCard("JOB REVENUE", FormatLakhs(_jobRevenueMonth), "this month", AccentBlue, () => OpenAnalysis(BuildJobPerformanceAnalysis()), "dashboard_job_revenue"), 0, 0);
            grid.Controls.Add(MakeInfoCard("MONTHLY PROFIT", FormatLakhs(_jobProfitMonth), _jobProfitMonth >= 0 ? "healthy margin" : "margin at risk", _jobProfitMonth >= 0 ? AccentGreen : AccentRed, () => OpenAnalysis(BuildJobPerformanceAnalysis()), "dashboard_monthly_profit"), 1, 0);
            grid.Controls.Add(MakeInfoCard("REV / COMPLETED JOB", FormatLakhs(_avgRevenuePerJob), _jobCompletedCount + " completed", AccentOrange, () => OpenAnalysis(BuildJobPerformanceAnalysis()), "dashboard_revenue_per_completed_job"), 2, 0);
            grid.Controls.Add(MakeInfoCard("JOB PIPELINE", _jobPendingCount + " / " + _jobProgressCount, "pending / in progress", AccentTeal, () => OpenAnalysis(BuildJobPerformanceAnalysis()), "dashboard_job_pipeline"), 3, 0);

            wrap.Controls.Add(grid);
            return wrap;
        }

        // ══════════════════════════════════════════════════════════════
        //  RECENT INVOICES TABLE
        // ══════════════════════════════════════════════════════════════
        private Panel BuildPurchaseOrderAlertsSection()
        {
            bool compact = IsCompactDashboard();
            Panel section = new Panel
            {
                Dock = DockStyle.Top,
                Height = DashboardCardHeight(132, 150),
                BackColor = BgPage,
                Padding = new Padding(DashboardSafeLeftInset, compact ? 6 : 8, 16, compact ? 8 : 10)
            };

            Panel hdr = new Panel { Height = compact ? 28 : 34, Dock = DockStyle.Top, BackColor = BgPage };
            hdr.Controls.Add(new Label
            {
                Text = "PURCHASE ORDER ALERTS",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMid,
                Location = new Point(0, compact ? 6 : 10),
                AutoSize = true
            });

            Button btnOpen = MakeLinkBtn("Open Purchases ->", AccentOrange);
            btnOpen.Dock = DockStyle.Right;
            btnOpen.Width = 118;
            btnOpen.Click += (s, e) => OnNavigate?.Invoke(10);
            hdr.Controls.Add(btnOpen);

            DataGridView grid = BuildGrid(new[]
            {
                ("Event", 130),
                ("PO", 100),
                ("Vendor", 180),
                ("Date", 82),
                ("Status", 90),
                ("Amount", 90)
            });

            List<PurchaseOrder> purchases = (_purchaseOrders ?? new List<PurchaseOrder>())
                .Where(p => p != null && (p.IsOverdue
                    || p.PriceVarianceFlag
                    || string.Equals(p.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Status, "Partial", StringComparison.OrdinalIgnoreCase)
                    || (p.BalanceDue > 0.01m && p.PayByDate.Date <= DateTime.Today.AddDays(7))))
                .OrderByDescending(p => p.IsOverdue)
                .ThenByDescending(p => p.PriceVarianceFlag)
                .ThenBy(p => p.PayByDate)
                .Take(6)
                .ToList();
            foreach (PurchaseOrder po in purchases)
            {
                DateTime eventDate = po.CreatedByDate ?? (po.CreatedDate == default ? po.PODate : po.CreatedDate);
                string status = string.Equals(po.Status, "Paid", StringComparison.OrdinalIgnoreCase) || po.BalanceDue <= 0.01m
                    ? "Paid"
                    : po.IsOverdue
                        ? "Overdue"
                        : po.PriceVarianceFlag
                            ? "Price variance"
                        : string.Equals(po.Status, "Received", StringComparison.OrdinalIgnoreCase)
                            ? "Received"
                            : po.PendingChargeCreated
                                ? "Billing queued"
                                : "Created";

                Color rowColor = status == "Paid" ? AccentGreen :
                                 status == "Overdue" ? AccentRed :
                                 status == "Received" ? AccentTeal :
                                 status == "Billing queued" ? AccentOrange : AccentBlue;

                var row = grid.Rows[grid.Rows.Add(
                    status,
                    po.PONumber ?? ("PO #" + po.POID),
                    po.VendorName ?? ("Vendor #" + po.VendorID),
                    eventDate.ToString("dd/MM/yyyy"),
                    status,
                    po.TotalAmount.ToString("₹#,##0.00")
                )];
                row.Tag = po;
                row.DefaultCellStyle.ForeColor = rowColor;
                if (status == "Overdue" || status == "Paid")
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            }

            if (grid.Rows.Count == 0)
            {
                var r = grid.Rows[grid.Rows.Add("No purchase alerts ...", "", "", "", "", "")];
                r.DefaultCellStyle.ForeColor = AccentGreen;
            }

            grid.Cursor = Cursors.Hand;
            grid.ScrollBars = ScrollBars.None;
            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is PurchaseOrder po)
                    OnNavigate?.Invoke(10);
            };
            grid.Dock = DockStyle.Fill;
            section.Controls.Add(grid);
            section.Controls.Add(hdr);
            hdr.BringToFront();
            return section;
        }

        private Panel BuildRecentInvoicesSection()
        {
            bool compact = IsCompactDashboard();
            Panel section = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = DashboardCardHeight(150, 184),
                BackColor = BgPage,
                Padding   = new Padding(DashboardSafeLeftInset, compact ? 4 : 8, 16, compact ? 4 : 8)
            };

            // Section header
            Panel hdr = new Panel { Height = compact ? 28 : 36, Dock = DockStyle.Top, BackColor = BgPage };
            hdr.Controls.Add(new Label
            {
                Text = "RECENT INVOICES", Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMid, Location = new Point(0, compact ? 6 : 10), AutoSize = true
            });
            Button btnAll = MakeLinkBtn("View All Invoices ->", AccentBlue);
            hdr.Resize += (s, e) => { btnAll.Location = new Point(hdr.Width - 160, 8); };
            btnAll.Click += (s, e) => OnNavigate?.Invoke(3);
            hdr.Controls.Add(btnAll);
            section.Controls.Add(hdr);

            DataGridView grid = BuildGrid(new[]
            {
                ("Invoice #",   80),
                ("Client",     180),
                ("Invoice Date", 100),
                ("Due Date",     92),
                ("Amount",      90),
                ("Status",      90)
            });

            foreach (Invoice inv in _recentInvoices)
            {
                bool overdue = inv.DueDate < DateTime.Today &&
                               !string.Equals(inv.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
                Color rowColor = string.Equals(inv.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                    ? AccentGreen
                    : overdue ? AccentRed : TextDark;

                string badge = overdue && !string.Equals(inv.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                    ? "OVERDUE"
                    : inv.PaymentStatus?.ToUpper() ?? "DRAFT";

                var row = grid.Rows[grid.Rows.Add(
                    inv.InvoiceNumber,
                    inv.ClientName ?? ("Client #" + inv.ClientID),
                    inv.InvoiceDate.ToString("dd/MM/yyyy"),
                    inv.DueDate.ToString("dd/MM/yyyy"),
                    inv.TotalAmount.ToString("₹#,##0.00"),
                    badge
                )];
                row.Tag = inv;
                row.DefaultCellStyle.ForeColor = rowColor;
                if (overdue) row.DefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            }

            if (_recentInvoices.Count == 0)
            {
                var r = grid.Rows[grid.Rows.Add("", "No invoices yet", "You don't have any invoices at the moment.", "", "", "")];
                r.DefaultCellStyle.ForeColor = TextLight;
            }

            grid.Cursor = Cursors.Hand;
            grid.ScrollBars = ScrollBars.None;
            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is Invoice invoice)
                    OnNavigate?.Invoke(3);
            };
            Panel gridWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };
            grid.Dock = DockStyle.Fill;
            gridWrap.Controls.Add(grid);
            section.Controls.Add(gridWrap);

            return section;
        }

        // ══════════════════════════════════════════════════════════════
        //  PIPELINE — EXPIRING CONTRACTS
        // ══════════════════════════════════════════════════════════════
        private Panel BuildPipelineSection()
        {
            bool compact = IsCompactDashboard();
            // Alerts + expiring contracts side by side
            Panel section = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = DashboardCardHeight(160, 200),
                BackColor = BgPage,
                Padding   = new Padding(DashboardSafeLeftInset, compact ? 4 : 8, 16, compact ? 8 : 16)
            };

            // Left: Alerts box (40% width)
            Panel alertsPanel = BuildAlertsBox();
            alertsPanel.Location = new Point(DashboardSafeLeftInset, 0);
            alertsPanel.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            // Right: Expiring contracts table
            Panel expiringPanel = BuildExpiringTable();
            expiringPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            section.Resize += (s, e) =>
            {
                int totalW = section.ClientSize.Width - DashboardSafeLeftInset - 16;
                int leftW  = (int)(totalW * 0.38);
                int rightW = totalW - leftW - 12;
                alertsPanel.Size   = new Size(leftW, section.ClientSize.Height - 24);
                expiringPanel.Location = new Point(DashboardSafeLeftInset + leftW + 12, 0);
                expiringPanel.Size     = new Size(rightW, section.ClientSize.Height - 24);
            };

            section.Controls.Add(alertsPanel);
            section.Controls.Add(expiringPanel);
            return section;
        }

        private Panel BuildAlertsBox()
        {
            Panel p = new Panel { BackColor = BgCard, BorderStyle = BorderStyle.None };
            DS.Rounded(p, 10);
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 10))
                using (Pen pen = new Pen(BorderLine, 1))
                    e.Graphics.DrawPath(pen, path);
            };

            // Header
            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = BgCard };
            hdr.Controls.Add(new Label
            {
                Text = "  BUSINESS ALERTS", Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMid, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });
            p.Controls.Add(hdr);

            var alerts = new (string icon, string text, Color color, Action open)[]
            {
                ("ok", _expiringContracts > 0
                    ? _expiringContracts + " contract(s) expiring in 90 days - initiate renewal"
                    : "No contracts expiring soon",
                    _expiringContracts > 0 ? AccentOrange : AccentGreen,
                    () => OnNavigate?.Invoke(2)),

                ("ok", _lowStockCount > 0
                    ? _lowStockCount + " stock item(s) below reorder level"
                    : "All inventory levels adequate",
                    _lowStockCount > 0 ? AccentRed : AccentGreen,
                    () => OnNavigate?.Invoke(11)),

                ("i", "Generate monthly AMC invoices on 1st of month",
                    AccentBlue,
                    () => OnNavigate?.Invoke(3)),
            };

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4) };
            int ay = 6;
            foreach (var (icon, text, color, open) in alerts)
            {
                var dot = new Label
                {
                    Text      = icon,
                    Font      = new Font("Segoe UI", 7),
                    ForeColor = color,
                    Location  = new Point(6, ay + 3),
                    Size      = new Size(14, 16)
                };
                var lbl = new Label
                {
                    Text      = text,
                    Font      = new Font("Segoe UI", 8),
                    ForeColor = color,
                    Location  = new Point(22, ay),
                    AutoSize  = false,
                    Height    = 32,
                    AutoEllipsis = true
                };
                _dashboardToolTip.SetToolTip(lbl, text);
                body.Resize += (s, e) => { lbl.Width = body.ClientSize.Width - 28; };
                dot.Cursor = Cursors.Hand;
                lbl.Cursor = Cursors.Hand;
                dot.Click += (s, e) => open?.Invoke();
                lbl.Click += (s, e) => open?.Invoke();
                body.Controls.Add(dot);
                body.Controls.Add(lbl);
                ay += 36;
            }
            p.Controls.Add(body);
            return p;
        }

        private Panel BuildExpiringTable()
        {
            Panel p = new Panel { BackColor = BgCard, BorderStyle = BorderStyle.None };
            DS.Rounded(p, 10);
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 10))
                using (Pen pen = new Pen(BorderLine, 1))
                    e.Graphics.DrawPath(pen, path);
            };

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = BgCard };
            hdr.Controls.Add(new Label
            {
                Text = "  RENEWAL PIPELINE - Contracts Expiring Within 90 Days",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMid, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });
            Button btnView = MakeLinkBtn("Manage ->", AccentOrange);
            btnView.Dock = DockStyle.Right;
            btnView.Width = 90;
            btnView.Click += (s, e) => OnNavigate?.Invoke(2);
            hdr.Controls.Add(btnView);
            p.Controls.Add(hdr);

            DataGridView grid = BuildGrid(new[]
            {
                ("Contract",    100),
                ("Client",      180),
                ("Expiry Date",  100),
                ("Status",      100)
            });

            foreach (AMCContract c in _expiringList.Take(8))
            {
                int daysLeft = (c.EndDate - DateTime.Today).Days;
                Color rowColor = daysLeft <= 30 ? AccentRed : daysLeft <= 60 ? AccentOrange : TextDark;
                string action  = daysLeft <= 30 ? "URGENT: RENEW" : daysLeft <= 60 ? "Send proposal" : "Schedule call";

                var row = grid.Rows[grid.Rows.Add(
                    c.ContractID,
                    "Client #" + c.ClientID,
                    c.EndDate.ToString("dd/MM/yyyy"),
                    action
                )];
                row.Tag = c;
                row.DefaultCellStyle.ForeColor = rowColor;
                if (daysLeft <= 30) row.DefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            }

            if (_expiringList.Count == 0)
            {
                var r = grid.Rows[grid.Rows.Add("", "No contracts expiring soon", "", "All clear")];
                r.DefaultCellStyle.ForeColor = AccentGreen;
            }

            grid.Cursor = Cursors.Hand;
            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is AMCContract contract)
                    OnNavigate?.Invoke(2);
            };
            grid.Dock = DockStyle.Fill;
            p.Controls.Add(grid);
            return p;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS — CARD BUILDERS
        // ══════════════════════════════════════════════════════════════
        private Panel MakeScoreCard(string title, string value, string sub, Color accent, Action onClick, string cardKey = null)
        {
            bool compact = IsCompactDashboard();
            Panel card = new Panel
            {
                Dock        = DockStyle.Fill,
                BackColor   = BgCard,
                Margin      = new Padding(5, 0, 5, 0),
                Cursor      = Cursors.Hand
            };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (Pen pen = new Pen(BorderLine, 1))
                    e.Graphics.DrawPath(pen, path);
            };

            // Top accent bar
            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = accent };
            card.Controls.Add(topBar);

            Label lblVal = new Label
            {
                Text      = value,
                Font      = new Font("Segoe UI", compact ? 14 : 16, FontStyle.Bold),
                ForeColor = accent,
                AutoSize  = false,
                Height    = 28,
                Location  = new Point(14, 12),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ConfigureDashboardDataLabel(lblVal);
            card.Resize += (s, e) => { lblVal.Width = card.ClientSize.Width - 18; };

            Label lblTitle = new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", compact ? 6.75f : 7, FontStyle.Bold),
                ForeColor = TextMid,
                AutoSize  = false,
                Height    = 16,
                Location  = new Point(14, 44),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ConfigureDashboardDataLabel(lblTitle);
            card.Resize += (s, e) => { lblTitle.Width = card.ClientSize.Width - 18; };

            Label lblSub = new Label
            {
                Text      = sub,
                Font      = new Font("Segoe UI", compact ? 6.75f : 7),
                ForeColor = TextLight,
                AutoSize  = false,
                Height    = 16,
                Location  = new Point(14, 62),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ConfigureDashboardDataLabel(lblSub);
            card.Resize += (s, e) => { lblSub.Width = card.ClientSize.Width - 18; };

            card.Controls.Add(lblVal);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblSub);

            if (onClick != null)
            {
                EventHandler open = (s, e) => onClick();
                card.Click += open;
                lblVal.Click += open;
                lblTitle.Click += open;
                lblSub.Click += open;
            }

            // Hover effect
            card.MouseEnter += (s, e) => { card.BackColor = DS.Primary50; };
            card.MouseLeave += (s, e) => { card.BackColor = BgCard; };

            return card;
        }

        private Panel MakeInfoCard(string title, string value, string sub, Color accent, Action onClick, string cardKey = null)
        {
            bool compact = IsCompactDashboard();
            Panel card = new Panel
            {
                Dock        = DockStyle.Fill,
                BackColor   = BgCard,
                Margin      = new Padding(5, 0, 5, 0),
                BorderStyle = BorderStyle.None,
                Cursor      = Cursors.Hand
            };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10))
                using (Pen pen = new Pen(BorderLine, 1))
                    e.Graphics.DrawPath(pen, path);
                using (var pen = new Pen(accent, 3))
                    e.Graphics.DrawLine(pen, 2, 8, 2, card.Height - 9);
            };

            Label lT = new Label
            {
                Text = title, Font = new Font("Segoe UI", compact ? 6.75f : 7, FontStyle.Bold),
                ForeColor = TextLight, AutoSize = true, Location = new Point(12, compact ? 5 : 8)
            };
            Label lV = new Label
            {
                Text = value, Font = new Font("Segoe UI", compact ? 12 : 14, FontStyle.Bold),
                ForeColor = accent, AutoSize = false, Height = compact ? 24 : 30,
                Location = new Point(12, compact ? 18 : 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ConfigureDashboardDataLabel(lV);
            card.Resize += (s, e) => { lV.Width = card.ClientSize.Width - 16; };
            Label lS = new Label
            {
                Text = sub, Font = new Font("Segoe UI", compact ? 6.75f : 7),
                ForeColor = TextLight, AutoSize = true, Location = new Point(12, compact ? 43 : 56)
            };
            ConfigureDashboardDataLabel(lS);

            card.Controls.Add(lT);
            card.Controls.Add(lV);
            card.Controls.Add(lS);

            if (onClick != null)
            {
                EventHandler open = (s, e) => onClick();
                card.Click += open;
                lT.Click += open;
                lV.Click += open;
                lS.Click += open;
            }
            card.MouseEnter += (s, e) => { card.BackColor = DS.Primary50; };
            card.MouseLeave += (s, e) => { card.BackColor = BgCard; };

            return card;
        }

        private Panel MakePendingPaymentsCard()
        {
            Panel card = MakeInfoCard(
                "PAYMENTS OVERDUE",
                _purchasePaymentsOverdue.ToString(),
                _purchasePaymentsOverdue > 0 ? "open pending payments" : "all supplier dues on track",
                _purchasePaymentsOverdue > 0 ? AccentRed : AccentGreen,
                () => OpenAnalysis(BuildPurchasePortfolioAnalysis(true)),
                "dashboard_payments_overdue");

            return card;
        }

        private DataGridView BuildGrid((string col, int width)[] cols)
        {
            bool compact = IsCompactDashboard();
            var grid = new DataGridView
            {
                AllowUserToAddRows       = false,
                AllowUserToDeleteRows    = false,
                ReadOnly                 = true,
                RowHeadersVisible        = false,
                BorderStyle              = BorderStyle.None,
                BackgroundColor          = BgCard,
                GridColor                = BorderLine,
                SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeRowsMode         = DataGridViewAutoSizeRowsMode.None,
                EnableHeadersVisualStyles = false,
                CellBorderStyle          = DataGridViewCellBorderStyle.SingleHorizontal,
                ShowCellToolTips         = true
            };
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            DS.StyleGrid(grid);
            grid.ColumnHeadersHeight = compact ? 26 : 34;
            grid.RowTemplate.Height = compact ? 24 : 32;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", compact ? 7.75f : 9f);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", compact ? 7f : 8.5f, FontStyle.Bold);
            grid.Cursor = Cursors.Hand;

            foreach (var (col, width) in cols)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = col, Width = width, MinimumWidth = 30,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    DefaultCellStyle = { Padding = new Padding(6, 0, 6, 0) }
                });
            }
            if (grid.Columns.Count > 0)
                grid.Columns[grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;
                string text = Convert.ToString(e.Value) ?? string.Empty;
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = text;
            };

            return grid;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS — BUTTON BUILDERS
        // ══════════════════════════════════════════════════════════════
        private Button MakeBtn(string text, Color bg, int width, int y)
        {
            var b = new Button
            {
                Text      = text,
                Width     = width,
                Height    = 38,
                BackColor = bg,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Location  = new Point(0, y),
                Margin    = new Padding(0, 0, 12, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding   = new Padding(12, 0, 12, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Lighten(bg, 0.08f);
            b.FlatAppearance.MouseDownBackColor = Darken(bg, 0.12f);
            ApplyRoundedButton(b, 6);
            return b;
        }

        private Button MakeHeaderButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Width = 36,
                Height = 32,
                BackColor = Color.White,
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = BorderLine;
            ApplyRoundedButton(b, 6);
            return b;
        }

        private void ShowDashboardTodo(string feature)
        {
            AppRuntime.LogTiming("Dashboard.Action", 0, feature);
        }

        private void HandleDashboardSearch(string text)
        {
            string query = (text ?? string.Empty).Trim();
            if (query.Length == 0
                || query.Equals("Search modules, invoices, jobs...", StringComparison.OrdinalIgnoreCase)
                || query.Equals("Search app: clients, jobs, invoices, purchases...", StringComparison.OrdinalIgnoreCase))
                return;

            string value = query.ToLowerInvariant();
            if (value.Contains("dashboard") || value.Contains("home")) { OnNavigate?.Invoke(0); return; }
            if (value.Contains("client") || value.Contains("customer")) { OnNavigate?.Invoke(1); return; }
            if (value.Contains("invoice")) { OnNavigate?.Invoke(3); return; }
            if (value.Contains("payment") || value.Contains("cash")) { OnNavigate?.Invoke(4); return; }
            if (value.Contains("job")) { OnNavigate?.Invoke(15); return; }
            if (value.Contains("dispatch") || value.Contains("schedule")) { OnNavigate?.Invoke(14); return; }
            if (value.Contains("service") || value.Contains("ticket")) { OnNavigate?.Invoke(16); return; }
            if (value.Contains("purchase") || value.Contains("po")) { OnNavigate?.Invoke(10); return; }
            if (value.Contains("quote") || value.Contains("quotation") || value.Contains("tender")) { OnNavigate?.Invoke(6); return; }
            if (value.Contains("contract") || value.Contains("renewal")) { OnNavigate?.Invoke(2); return; }
            if (value.Contains("payroll")) { OnNavigate?.Invoke(13); return; }
            if (value.Contains("employee") || value.Contains("staff")) { OnNavigate?.Invoke(12); return; }
            if (value.Contains("vendor") || value.Contains("supplier")) { OnNavigate?.Invoke(9); return; }
            if (value.Contains("inventory") || value.Contains("stock")) { OnNavigate?.Invoke(11); return; }
            if (value.Contains("report")) { OnNavigate?.Invoke(7); return; }
            if (value.Contains("setting") || value.Contains("config")) { OnNavigate?.Invoke(8); return; }
            if (value.Contains("master")) { OnNavigate?.Invoke(17); return; }
            if (value.Contains("sla")) { OnNavigate?.Invoke(5); return; }
            MessageBox.Show("No dashboard module matched: " + query, "Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Button MakeDashboardTabButton(string key, string text, Color accent, Action open)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(14, 0, 14, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = accent
            };
            button.FlatAppearance.BorderSize = 1;
            button.Click += (s, e) =>
            {
                _selectedDashboardTabKey = key;
                UpdateDashboardActionTabs();
                open?.Invoke();
            };
            _dashboardTabButtons[key] = button;
            ApplyRoundedButton(button, 7);
            return button;
        }

        private void UpdateDashboardActionTabs()
        {
            foreach (var entry in _dashboardTabButtons)
            {
                Button button = entry.Value;
                Color accent = button.Tag is Color c ? c : AccentBlue;
                bool active = string.Equals(entry.Key, _selectedDashboardTabKey, StringComparison.OrdinalIgnoreCase);

                button.BackColor = active ? accent : Lighten(accent, 0.88f);
                button.ForeColor = active ? Color.White : Color.FromArgb(51, 65, 85);
                button.FlatAppearance.BorderColor = active ? accent : Lighten(accent, 0.68f);
                button.FlatAppearance.MouseOverBackColor = active ? Darken(accent, 0.08f) : Lighten(accent, 0.80f);
                button.FlatAppearance.MouseDownBackColor = active ? Darken(accent, 0.14f) : Lighten(accent, 0.72f);
            }
        }

        private Button MakeLinkBtn(string text, Color fg)
        {
            var b = new Button
            {
                Text      = text,
                AutoSize  = true,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = fg,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Height    = 30,
                Padding   = new Padding(12, 0, 12, 0)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            b.FlatAppearance.MouseOverBackColor = Lighten(fg, 0.90f);
            b.FlatAppearance.MouseDownBackColor = Lighten(fg, 0.82f);
            ApplyRoundedButton(b, 6);
            return b;
        }

        private static void ApplyRoundedButton(Button button, int radius)
        {
            if (button == null)
                return;

            void updateRegion(object sender, EventArgs args)
            {
                int safeRadius = Math.Max(2, Math.Min(radius, Math.Min(button.Width, button.Height) / 2));
                using (GraphicsPath path = CreateRoundedRectPath(new Rectangle(0, 0, button.Width, button.Height), safeRadius))
                    button.Region = new Region(path);
            }

            button.Resize += updateRegion;
            updateRegion(button, EventArgs.Empty);
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
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

        private static Color Darken(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                Math.Max(0, color.R - (int)(255 * amount)),
                Math.Max(0, color.G - (int)(255 * amount)),
                Math.Max(0, color.B - (int)(255 * amount)));
        }

        private static Color Lighten(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                color.R + (int)((255 - color.R) * amount),
                color.G + (int)((255 - color.G) * amount),
                color.B + (int)((255 - color.B) * amount));
        }

        private static string FormatLakhs(decimal v) =>
            v >= 100000 ? "₹" + (v / 100000m).ToString("F1") + " L"
            : v >= 1000  ? "₹" + (v / 1000m).ToString("F0") + " K"
            : v.ToString("₹#,##0.00");

        private static string FormatBackupAge(TimeSpan age)
        {
            if (age.TotalMinutes < 1)
                return "just now";
            if (age.TotalHours < 1)
                return ((int)Math.Max(1, age.TotalMinutes)).ToString() + " min ago";
            if (age.TotalHours < 24)
                return ((int)Math.Max(1, age.TotalHours)).ToString() + " hr ago";
            return ((int)Math.Max(1, age.TotalDays)).ToString() + " days ago";
        }

        private void ConfigureDashboardDataLabel(Label label)
        {
            if (label == null)
                return;

            label.AutoEllipsis = true;
            label.UseMnemonic = false;
            if (!string.IsNullOrWhiteSpace(label.Text))
                _dashboardToolTip.SetToolTip(label, label.Text);
        }
    }
}
