using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Integrations;
using HVAC_Pro_Desktop.UI.Controls;

namespace HVAC_Pro_Desktop.UI
{
    public class PurchaseForm : DeferredPageControl
    {
        private enum PurchaseListTab
        {
            AllPurchaseOrders,
            PendingOrders,
            PartialOrders,
            ApprovedOrders,
            ClosedOrders
        }

        private enum PurchaseViewMode
        {
            Orders,
            VendorPayables
        }

        private static bool _openPendingPaymentsRequested;
        private static int? _prefillVendorId;

        public static void RequestPendingPaymentsView()
        {
            _openPendingPaymentsRequested = true;
        }

        public static void RequestVendorPrefill(int vendorId)
        {
            _prefillVendorId = vendorId > 0 ? (int?)vendorId : null;
        }

        public void ApplyNavigationRequest()
        {
            if (_openPendingPaymentsRequested)
            {
                _openPendingPaymentsRequested = false;
                ActivatePendingPaymentsView();
            }

            if (_prefillVendorId.HasValue)
            {
                int vendorId = _prefillVendorId.Value;
                _prefillVendorId = null;
                ApplyVendorPrefill(vendorId);
            }
        }

        private readonly PurchaseService _svc = new PurchaseService();
        private readonly VendorService _vndSvc = new VendorService();
        private readonly InventoryService _invSvc = new InventoryService();
        private readonly ContractService _cntSvc = new ContractService();
        private readonly JobService _jobSvc = new JobService();
        private readonly TenderService _tenderSvc = new TenderService();
        private readonly HsnSacMasterService _hsnSvc = new HsnSacMasterService();
        private readonly UnitMeasurementService _unitSvc = new UnitMeasurementService();
        private readonly PaymentService _paymentSvc = new PaymentService();
        private readonly VendorAdvancePaymentService _vendorAdvanceSvc = new VendorAdvancePaymentService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly ToolTip _toolTip = new ToolTip();
        public Action<int> OnNavigate { get; set; }
        private static bool UsePurchaseOrdersDashboard { get { return true; } }

        private List<StockItem> _inventoryItems = new List<StockItem>();
        private List<Vendor> _vendors = new List<Vendor>();
        private List<AMCContract> _contracts = new List<AMCContract>();
        private List<Job> _jobs = new List<Job>();
        private List<ClientSite> _sites = new List<ClientSite>();
        private List<Employee> _technicians = new List<Employee>();
        private List<HsnSacMasterEntry> _hsnEntries = new List<HsnSacMasterEntry>();
        private List<PurchaseOrder> _orderSource = new List<PurchaseOrder>();
        private readonly Dictionary<int, bool> _expandedVendors = new Dictionary<int, bool>();
        private readonly Dictionary<int, CheckBox> _payableChecks = new Dictionary<int, CheckBox>();

        private FlowLayoutPanel _leftListFlow;
        private Panel _leftRail;
        private Panel _detail;
        private Panel _selectedCard;
        private Button _btnPendingTab;
        private Button _btnPartialTab;
        private Button _btnAllTab;
        private Button _btnApprovedTab;
        private Button _btnClosedTab;
        private GlobalPaginationControl _orderListPager;
        private Button _btnPayablesToggle;
        private Button _btnBatchPay;
        private Button _btnVendorAdvance;
        private Button _btnNewPo;
        private Button _btnSavePo;
        private Button _btnPreview;
        private Button _btnPrintPdf;
        private Button _btnSend;
        private Button _btnConvertToBill;
        private Button _btnViewReceipt;
        private TextBox _txtSearch;
        private ComboBox _cboListStatusFilter;
        private Label _lblStatus;
        private Label _lblBreadcrumb;
        private Label _lblHeaderStatus;

        private ComboBox _cboVendor;
        private TextBox _txtVendorGstin;
        private TextBox _txtPONumber;
        private DateTimePicker _dtpDate;
        private DateTimePicker _dtpPayByDate;
        private TextBox _txtVendorInvoiceNumber;
        private ComboBox _cboStatus;
        private ComboBox _cboLinkedType;
        private ComboBox _cboLinkedRecord;
        private ComboBox _cboProjectSite;
        private RadioButton _rbTechPickup;
        private RadioButton _rbSiteDelivery;
        private Panel _deliveryAddressPanel;
        private TextBox _txtDeliveryAddress;
        private ComboBox _cboTechnician;
        private Label _lblCreatedByMeta;
        private CheckBox _chkAddToClientInvoice;
        private PictureBox _picReceipt;
        private Label _lblReceiptFile;
        private Button _btnAttachReceipt;
        private Button _btnInlineViewReceipt;
        private Button _btnDeleteReceipt;
        private TextBox _txtNotes;
        private Panel _lineItemHeader;
        private FlowLayoutPanel _lineItemFlow;
        private Label _lblTotal;
        private Label _lblLineItemCount;
        private Label _lblLineItemEmptyState;
        private Label _lblSummaryItems;
        private Label _lblSummarySubtotal;
        private Label _lblSummaryDiscount;
        private Label _lblSummaryTax;
        private Label _lblSummaryCharges;
        private Label _lblSummaryTotal;
        private Label _lblSummaryWords;
        private Label _lblPaymentMeta;
        private Label _lblVarianceWarning;
        private readonly List<Control> _poDetailsControls = new List<Control>();
        private readonly List<Control> _shippingControls = new List<Control>();
        private readonly List<Control> _billingControls = new List<Control>();
        private readonly List<Control> _otherDetailsControls = new List<Control>();
        private readonly List<Control> _attachmentControls = new List<Control>();
        private readonly List<Control> _historyControls = new List<Control>();
        private readonly List<Label> _poHeaderTabLabels = new List<Label>();
        private string _activePoHeaderTab = "PO Details";

        private PurchaseOrder _current;
        private string _receiptImagePath;
        private bool _showDashboard = true;
        private PurchaseListTab _activeTab = PurchaseListTab.AllPurchaseOrders;
        private PurchaseViewMode _viewMode = PurchaseViewMode.Orders;
        private int _listPageIndex;
        private int _orderListPageSize = 10;
        private const int MaxRenderedOrderCards = 120;
        private const int OrderCardHeightWithMargin = 114;
        private const string SearchPlaceholder = "Search PO, vendor...";
        private decimal _otherCharges;

        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color DelRed = DS.Red600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color WarnOrange = DS.Amber500;
        private static readonly Color PendingRed = DS.Red500;
        private static readonly Color PendingAmber = DS.Amber500;
        private static readonly Color PendingGreen = DS.Green600;
        private static readonly Color PoPageBg = Color.FromArgb(246, 248, 252);
        private static readonly Color PoSurface = Color.White;
        private static readonly Color PoText = Color.FromArgb(15, 23, 42);
        private static readonly Color PoMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color PoBorder = DS.Border;
        private static readonly Color PoPurple = Color.FromArgb(124, 58, 237);
        private TextBox _poDashSearch;
        private TextBox _poTableSearch;
        private ComboBox _poStatusFilter;
        private ComboBox _poPeriodFilter;
        private ComboBox _poTrendFilter;
        private FlowLayoutPanel _poStatsFlow;
        private TableLayoutPanel _poTable;
        private GlobalPaginationControl _poPager;
        private PoStatusDonutChart _poStatusChart;
        private PoValueTrendChart _poTrendChart;
        private FlowLayoutPanel _poAgingFlow;
        private FlowLayoutPanel _poOverdueFlow;
        private Panel _poSummaryPanel;
        private FlowLayoutPanel _poTopSuppliersFlow;
        private Label _poStatusFilterLabel;
        private Label _poPeriodFilterLabel;
        private Label _poTrendFilterLabel;
        private Label _poDashSearchLabel;
        private Label _poTableSearchLabel;
        private int _poPage = 1;
        private int _poPageSize = 10;

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public PurchaseForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PoPageBg;
            BuildLayout();
            ApplyPermissions();
            MarkDeferredLoadCompleted();
            if (UsePurchaseOrdersDashboard && _showDashboard)
            {
                RefreshPurchaseDashboard();
                Load += (s, e) => QueuePurchaseDashboardRefresh();
            }
        }

        private void QueuePurchaseDashboardRefresh()
        {
            var timer = new Timer { Interval = 1500 };
            timer.Tick += async (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!IsDisposed && Visible)
                    await RefreshPurchaseDashboardFromHeaderAsync();
            };
            timer.Start();
        }

        private void BuildLayout()
        {
            if (UsePurchaseOrdersDashboard && _showDashboard)
            {
                BuildPurchaseOrdersDashboardLayout();
                return;
            }

            BackColor = DS.BgPage;
            Panel header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = DS.BgPage, Padding = new Padding(22, 12, 22, 10) };
            Label title = new Label
            {
                Text = "Purchase Orders",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Location = new Point(24, 12),
                AutoSize = true
            };
            Label subtitle = new Label
            {
                Text = "Purchase  >  Purchase Orders  >  New PO",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate600,
                Location = new Point(25, 42),
                AutoSize = true
            };
            _lblBreadcrumb = subtitle;
            _lblHeaderStatus = MakeStatusBadge("Draft", SaveGreen, new Point(248, 42), 62);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(_lblHeaderStatus);

            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = false,
                Height = 40,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            Button btnRefresh = MakeOutlineButton("Refresh", 78);
            Button btnBackToDashboard = MakeOutlineButton("<- Dashboard", 112);
            _btnNewPo = MakeBtn("+  New PO", InfoBlue, 104);
            _btnSavePo = MakeOutlineButton("Save as Draft", 118);
            Button btnReceived = MakeOutlineButton("Mark Received", 118);
            _btnPreview = MakeOutlineButton("Preview", 80);
            _btnPrintPdf = MakeOutlineButton("Print PDF", 88);
            _btnSend = MakeOutlineButton("Send", 70);
            _btnConvertToBill = MakeOutlineButton("Convert to Bill", 112);
            _btnViewReceipt = MakeOutlineButton("View Receipt", 102);
            _btnPayablesToggle = MakeOutlineButton("Supplier Payables", 132);
            _btnBatchPay = MakeBtn("Batch Pay", SaveGreen, 92);
            _btnVendorAdvance = MakeOutlineButton("Advance", 82);
            Button btnImport = MakeOutlineButton("Import Excel", 100);
            Button btnTemplate = MakeOutlineButton("Excel Template", 114);
            Button btnForms = MakeOutlineButton("Service Forms", 104);
            Button btnSettings = MakeOutlineButton("More Actions", 96);
            _btnBatchPay.Visible = false;
            _btnVendorAdvance.Visible = false;

            _lblStatus = new Label
            {
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(8, 8, 0, 0)
            };

            btnRefresh.Click += async (s, e) =>
            {
                await LoadInitialDataAsync();
                SetStatus("Showing " + (_orderSource?.Count ?? 0) + " purchase orders.", DS.Slate600);
            };
            btnBackToDashboard.Click += async (s, e) => await BackToPurchaseDashboardAsync();
            _btnPayablesToggle.Click += (s, e) => ToggleVendorPayablesView();
            _btnBatchPay.Click += (s, e) => BatchPaySelected();
            _btnVendorAdvance.Click += (s, e) => RecordVendorAdvance();
            _btnPreview.Click += (s, e) => PreviewPurchaseOrder();
            _btnPrintPdf.Click += (s, e) => SavePurchaseOrderPdf();
            _btnSend.Click += (s, e) => SendPurchaseOrder();
            _btnConvertToBill.Click += (s, e) => ConvertToBill();
            _btnViewReceipt.Click += (s, e) => ViewReceipt();
            _btnNewPo.Click += (s, e) => NewRecord();
            _btnSavePo.Click += (s, e) => Save();
            btnReceived.Click += (s, e) => MarkReceived();
            btnImport.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(btnImport, ExcelImportModule.Purchases, FindForm());
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Purchases, FindForm());
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Purchase Orders", "Inventory / Purchases", null, "purchase request goods received note spare parts requisition stock issue return vendor approval");
            btnSettings.Click += (s, e) => ShowPurchaseMoreMenu();
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            _toolTip.SetToolTip(btnBackToDashboard, "Return to the purchase dashboard.");
            _toolTip.SetToolTip(_btnSavePo, "Save the PO header and line items as a draft.");
            _toolTip.SetToolTip(btnSettings, "Open more purchase actions.");
            toolbar.Controls.AddRange(new Control[] { btnBackToDashboard, btnRefresh, _btnPayablesToggle, btnImport, btnTemplate, btnForms, _btnPreview, _btnPrintPdf, _btnNewPo, btnSettings });
            header.Controls.Add(toolbar);
            header.Controls.Add(_lblStatus);
            header.Resize += (s, e) =>
            {
                LayoutPurchaseEditorHeader(header, toolbar);
                LayoutHeaderStatusBadge();
            };
            LayoutPurchaseEditorHeader(header, toolbar);
            LayoutHeaderStatusBadge();

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(18, 0, 18, 18) };
            _leftRail = new Panel { Dock = DockStyle.Left, Width = 326, MinimumSize = new Size(326, 0), BackColor = DS.BgPage, Padding = new Padding(0, 0, 12, 0) };
            _leftRail.Controls.Add(BuildLeftRailContent());
            Splitter leftSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 6,
                MinSize = 326,
                MinExtra = 620,
                BackColor = DS.Slate200
            };
            leftSplitter.SplitterMoved += (s, e) => ApplyFiltersAndRender();
            Panel rightRail = new Panel { Dock = DockStyle.Right, Width = 340, BackColor = DS.BgPage, Padding = new Padding(12, 0, 8, 0) };
            rightRail.Controls.Add(BuildRightPanel());
            Panel detailHost = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };
            _detail = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage };
            _detail.HorizontalScroll.Enabled = false;
            _detail.HorizontalScroll.Visible = false;
            BuildDetailPanel();
            detailHost.Controls.Add(_detail);

            body.Controls.Add(detailHost);
            body.Controls.Add(rightRail);
            body.Controls.Add(leftSplitter);
            body.Controls.Add(_leftRail);

            Controls.Add(body);
            Controls.Add(header);
            ApplyPurchaseReferenceSkin(Controls);
        }

        /// <summary>Prevents the purchase editor action toolbar from overlapping the title block on compact widths.</summary>
        private void LayoutPurchaseEditorHeader(Panel header, FlowLayoutPanel toolbar)
        {
            if (header == null || toolbar == null)
                return;

            int protectedTitleWidth = Math.Min(460, Math.Max(320, header.ClientSize.Width / 3));
            int rightPadding = 22;
            int available = Math.Max(300, header.ClientSize.Width - protectedTitleWidth - rightPadding);
            bool compact = available < 840;

            toolbar.SetBounds(protectedTitleWidth, 12, available, compact ? 74 : 38);
            toolbar.WrapContents = compact;
            header.Height = compact ? 116 : 86;

            foreach (Control control in toolbar.Controls)
            {
                control.Margin = new Padding(4, 0, 4, 8);
            }

            SetToolbarControlVisible(toolbar, "Supplier Payables", available >= 910);
            SetToolbarControlVisible(toolbar, "Import Excel", available >= 760);
            SetToolbarControlVisible(toolbar, "Excel Template", available >= 900);
            SetToolbarControlVisible(toolbar, "Service Forms", available >= 820);

            if (_lblStatus != null)
            {
                _lblStatus.MaximumSize = new Size(Math.Max(120, header.ClientSize.Width - 48), 20);
                _lblStatus.AutoEllipsis = true;
                _lblStatus.Location = new Point(24, compact ? 88 : 64);
                _lblStatus.Size = new Size(Math.Max(120, header.ClientSize.Width - 48), 20);
                _lblStatus.Visible = !string.IsNullOrWhiteSpace(_lblStatus.Text);
            }
        }

        private static void SetToolbarControlVisible(FlowLayoutPanel toolbar, string text, bool visible)
        {
            if (toolbar == null)
                return;

            foreach (Control control in toolbar.Controls)
            {
                Button button = control as Button;
                if (button != null && string.Equals(button.Text, text, StringComparison.OrdinalIgnoreCase))
                    button.Visible = visible;
            }
        }

        private async Task LoadPurchaseDashboardAsync()
        {
            TimeSpan ttl = TimeSpan.FromMinutes(2);
            await Task.Run(() =>
            {
                _orderSource = AppDataCache.GetOrCreate("purchases:fresh", ttl, () => _svc.GetAllFresh() ?? new List<PurchaseOrder>()).ToList();
            });
            BeginInvoke((Action)(() =>
            {
                if (_showDashboard)
                    RefreshPurchaseDashboard();
            }));
        }

        private async Task RefreshPurchaseDashboardFromHeaderAsync()
        {
            SetStatus("Refreshing purchase orders...", PoMuted);
            try
            {
                await LoadPurchaseDashboardAsync();
                SetStatus("Loaded " + (_orderSource?.Count ?? 0) + " purchase orders.", SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Refreshing purchase dashboard", ex);
                SetStatus("Purchase dashboard could not refresh. Check SQL connection and try again.", DelRed);
            }
        }

        private static List<PurchaseOrder> BuildPurchaseDashboardSeed()
        {
            return new List<PurchaseOrder>();
        }

        private void BuildPurchaseOrdersDashboardLayout()
        {
            Controls.Clear();
            BackColor = PoPageBg;
            Panel root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PoPageBg,
                Padding = new Padding(24, 18, 24, 16)
            };
            Control header = BuildPoDashboardHeader();
            header.Dock = DockStyle.Top;
            header.Height = 64;

            Panel main = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PoPageBg, Padding = new Padding(0, 0, 10, 0) };
            FlowLayoutPanel stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = PoPageBg
            };
            _poStatsFlow = new FlowLayoutPanel { Height = 112, BackColor = PoPageBg, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 4, 0, 6) };
            stack.Controls.Add(_poStatsFlow);
            stack.Controls.Add(BuildPoWorkflowRow());
            stack.Controls.Add(BuildPoChartRow());
            stack.Controls.Add(BuildPoTableCard());
            stack.Controls.Add(BuildPoBottomRow());
            main.Controls.Add(stack);
            main.Resize += (s, e) =>
            {
                int width = Math.Max(760, main.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);
                foreach (Control child in stack.Controls)
                    child.Width = width;
            };

            Panel sidebar = BuildPoRightSidebar();
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PoPageBg,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 304));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(main, 0, 0);
            body.Controls.Add(sidebar, 1, 0);
            root.Controls.Add(body);
            root.Controls.Add(header);
            Controls.Add(root);
        }

        private Control BuildPoDashboardHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = PoPageBg };
            Label title = new Label { Text = "Purchase Orders", Location = new Point(0, 0), Size = new Size(340, 28), Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = PoText, UseMnemonic = false };
            Label subtitle = new Label { Text = "Manage and track all purchase orders from creation to receipt and payment.", Location = new Point(1, 31), Size = new Size(520, 22), Font = new Font("Segoe UI", 9f), ForeColor = PoMuted, UseMnemonic = false, AutoEllipsis = true };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            _poDashSearch = new TextBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, Size = new Size(300, 32), Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle, Text = "Search PO number, supplier, item...", ForeColor = DS.Slate400, Tag = "CUSTOM_INPUT_SHELL" };
            _poDashSearch.BackColor = Color.White;
            AddDashboardPlaceholder(_poDashSearch, "Search PO number, supplier, item...");
            _poDashSearch.TextChanged += (s, e) => { _poPage = 1; RefreshPoTableOnly(); };
            _poDashSearchLabel = MakeSearchVisual("Search PO number, supplier, item...");
            _poDashSearchLabel.Visible = false;
            _poDashSearchLabel.Click += (s, e) => { _poDashSearch.Focus(); _poDashSearchLabel.Visible = false; };
            Button refreshPo = MakePoOutlineButton("Refresh", 86);
            refreshPo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshPo.Click += async (s, e) => await RefreshPurchaseDashboardFromHeaderAsync();
            Button importPo = MakePoOutlineButton("Import", 92);
            importPo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            importPo.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(importPo, ExcelImportModule.Purchases, FindForm());
            Button newPo = MakePoButton("+  New Purchase Order", InfoBlue, 166);
            newPo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            newPo.Click += async (s, e) => await OpenNewPurchaseOrderFormAsync();
            Label bell = MakeHeaderIcon("🔔", Color.White, PoMuted);
            Label gear = MakeHeaderIcon("⚙", Color.White, PoMuted);
            Panel toolbar = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Width = 760,
                Height = 36,
                Padding = Padding.Empty,
                BackColor = PoPageBg
            };
            Control[] toolbarItems = { _poDashSearch, refreshPo, importPo, newPo, bell, gear };
            foreach (Control control in toolbarItems)
            {
                control.Margin = Padding.Empty;
                control.MinimumSize = control.Size;
                control.Height = 32;
                control.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }
            toolbar.Controls.AddRange(toolbarItems);
            header.Controls.Add(toolbar);
            Action layoutToolbar = () =>
            {
                int headerWidth = Math.Max(0, header.ClientSize.Width);
                int right = headerWidth;
                int titleReserve = headerWidth >= 1120 ? 560 : 430;
                int available = Math.Max(0, right - titleReserve);
                int iconWidth = bell.Width + gear.Width + 16;
                int actionWidth = refreshPo.Width + importPo.Width + newPo.Width + 24;
                int searchWidth = Math.Min(300, available - iconWidth - actionWidth - 8);
                bool showSearch = searchWidth >= 190;

                _poDashSearch.Visible = showSearch;
                _poDashSearch.Width = showSearch ? searchWidth : 0;
                toolbar.Width = Math.Max(iconWidth + actionWidth + 8, (showSearch ? _poDashSearch.Width + 8 : 0) + actionWidth + iconWidth + 16);
                toolbar.Location = new Point(Math.Max(titleReserve, headerWidth - toolbar.Width), 8);
                subtitle.Width = Math.Max(260, Math.Min(520, toolbar.Left - subtitle.Left - 18));

                int x = 0;
                foreach (Control control in toolbarItems)
                {
                    if (!control.Visible)
                        continue;

                    control.Location = new Point(x, 0);
                    x += control.Width + 8;
                }
            };
            header.Resize += (s, e) =>
            {
                _poDashSearchLabel.Visible = false;
                _poDashSearch.BringToFront();
                layoutToolbar();
            };
            layoutToolbar();
            return header;
        }

        private Label MakeHeaderIcon(string text, Color back, Color fore)
        {
            Label icon = new Label { Size = new Size(32, 32), BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Text = text, TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(icon, 8);
            return icon;
        }

        private Label MakeFilterPill(string text)
        {
            Label label = new Label
            {
                Text = text,
                Size = new Size(126, 28),
                BackColor = Color.White,
                ForeColor = PoText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.3f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                AutoEllipsis = true
            };
            return label;
        }

        private Label MakeSearchVisual(string text)
        {
            return new Label
            {
                Text = "  🔎  " + text,
                Size = new Size(300, 32),
                BackColor = Color.White,
                ForeColor = PoMuted,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.IBeam,
                AutoEllipsis = true
            };
        }

        private void CycleCombo(ComboBox combo, Label visual)
        {
            if (combo == null || combo.Items.Count == 0 || visual == null)
                return;
            int next = combo.SelectedIndex + 1;
            if (next >= combo.Items.Count)
                next = 0;
            combo.SelectedIndex = next;
            visual.Text = Convert.ToString(combo.SelectedItem);
        }

        private void ShowNewPurchaseMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("New Purchase Order", null, async (s, e) => await OpenNewPurchaseOrderFormAsync());
            menu.Items.Add("New Request for Quote", null, (s, e) => OnNavigate?.Invoke(7));
            menu.Items.Add("New Goods Receipt", null, (s, e) => ShowDashboardAction("Goods Receipt", "Select an existing PO, then use Mark Received or the Goods Receipt action to record received goods."));
            menu.Show(owner, new Point(0, owner.Height + 4));
        }

        private Control BuildPoWorkflowRow()
        {
            Panel card = MakePoCard();
            card.Tag = "dash-card";
            card.Height = 300;
            card.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(new Label { Text = "Purchase Workflow", Location = new Point(16, 14), Size = new Size(220, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            Control workflow = SharedUiPrimitives.BuildDirectionalWorkflowLayout(
                "Supplier Workflow",
                "Sent to Suppliers",
                BuildPurchaseWorkflowLines(IsOpenPo),
                "Received from Suppliers",
                BuildPurchaseWorkflowLines(po => IsStatus(po, "Fully Received") || IsStatus(po, "Partially Received")),
                "Client Workflow",
                "Sent to Clients",
                BuildPurchaseWorkflowLines(po => po.ClientID > 0 || po.SiteID > 0 || po.AddToClientInvoice),
                "Received from Clients",
                BuildPurchaseWorkflowLines(po => po.BalanceDue <= 0.01m && po.TotalAmount > 0m));
            workflow.Location = new Point(10, 42);
            workflow.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            card.Controls.Add(workflow);
            card.Resize += (s, e) => workflow.Size = new Size(Math.Max(120, card.ClientSize.Width - 20), Math.Max(120, card.ClientSize.Height - 54));
            return card;
        }

        private IEnumerable<string> BuildPurchaseWorkflowLines(Func<PurchaseOrder, bool> predicate)
        {
            return (_orderSource ?? new List<PurchaseOrder>())
                .Where(predicate)
                .OrderByDescending(po => po.PODate)
                .Take(4)
                .Select(po => CleanPoNumber(po.PONumber, po.POID) + " - " + Safe(po.VendorName, "Supplier") + " - " + NormalizePoStatus(po.Status));
        }

        private Control BuildPoChartRow()
        {
            TableLayoutPanel row = new TableLayoutPanel { Height = 218, BackColor = PoPageBg, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            Panel statusCard = MakePoCard();
            statusCard.Margin = new Padding(0, 0, 8, 0);
            statusCard.Controls.Add(new Label { Text = "PO Status Overview", Location = new Point(16, 14), Size = new Size(220, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            _poStatusChart = new PoStatusDonutChart { Location = new Point(16, 42), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            statusCard.Controls.Add(_poStatusChart);
            statusCard.Resize += (s, e) => _poStatusChart.Size = new Size(statusCard.ClientSize.Width - 32, statusCard.ClientSize.Height - 54);

            Panel trendCard = MakePoCard();
            trendCard.Margin = new Padding(8, 0, 0, 0);
            trendCard.Controls.Add(new Label { Text = "PO Value Trend", Location = new Point(16, 14), Size = new Size(220, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            _poTrendFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f), Size = new Size(126, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _poTrendFilter.Items.AddRange(new object[] { "This Year", "Last 6 Months", "Last 12 Months" });
            _poTrendFilter.SelectedIndex = 0;
            _poTrendFilter.Text = "This Year";
            _poTrendFilter.SelectedIndexChanged += (s, e) => RefreshPurchaseDashboard();
            _poTrendFilterLabel = MakeFilterPill("This Year");
            _poTrendFilterLabel.Click += (s, e) => CycleCombo(_poTrendFilter, _poTrendFilterLabel);
            _poTrendChart = new PoValueTrendChart { Location = new Point(16, 48), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            trendCard.Controls.Add(_poTrendFilter);
            trendCard.Controls.Add(_poTrendFilterLabel);
            trendCard.Controls.Add(_poTrendChart);
            trendCard.Resize += (s, e) =>
            {
                _poTrendFilter.Location = new Point(trendCard.ClientSize.Width - 146, 12);
                _poTrendFilterLabel.Location = _poTrendFilter.Location;
                _poTrendChart.Size = new Size(trendCard.ClientSize.Width - 32, trendCard.ClientSize.Height - 62);
                _poTrendFilterLabel.BringToFront();
            };
            row.Controls.Add(statusCard, 0, 0);
            row.Controls.Add(trendCard, 1, 0);
            return row;
        }

        private Control BuildPoTableCard()
        {
            Panel card = MakePoCard();
            card.Tag = "dash-card";
            card.Height = 300;
            card.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(new Label { Text = "Recent Purchase Orders", Location = new Point(16, 14), Size = new Size(250, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            Button viewAll = MakePoOutlineButton("View All", 78);
            viewAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            viewAll.Click += (s, e) => ResetPurchaseOrdersDashboardFilters();
            card.Controls.Add(viewAll);
            _poStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f), Size = new Size(132, 28) };
            _poStatusFilter.Items.AddRange(new object[] { "All Status", "Draft", "Pending Approval", "Approved", "Partially Received", "Fully Received", "Cancelled" });
            _poStatusFilter.SelectedIndex = 0;
            _poStatusFilter.Text = "All Status";
            _poStatusFilter.SelectedIndexChanged += (s, e) => { _poPage = 1; RefreshPoTableOnly(); };
            _poStatusFilterLabel = MakeFilterPill("All Status");
            _poStatusFilterLabel.Click += (s, e) => { CycleCombo(_poStatusFilter, _poStatusFilterLabel); _poPage = 1; RefreshPoTableOnly(); };
            _poPeriodFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f), Size = new Size(118, 28) };
            _poPeriodFilter.Items.AddRange(new object[] { "This Month", "Last Month", "This Quarter", "This Year", "Custom Range" });
            _poPeriodFilter.SelectedIndex = 3;
            _poPeriodFilter.Text = "This Year";
            _poPeriodFilter.SelectedIndexChanged += (s, e) => { _poPage = 1; RefreshPoTableOnly(); };
            _poPeriodFilterLabel = MakeFilterPill("This Year");
            _poPeriodFilterLabel.Click += (s, e) => { CycleCombo(_poPeriodFilter, _poPeriodFilterLabel); _poPage = 1; RefreshPoTableOnly(); };
            _poTableSearch = new TextBox { Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.FixedSingle, Size = new Size(170, 28), Text = "Search PO, Supplier...", ForeColor = DS.Slate400, Tag = "CUSTOM_INPUT_SHELL" };
            AddDashboardPlaceholder(_poTableSearch, "Search PO, Supplier...");
            _poTableSearch.TextChanged += (s, e) => { _poPage = 1; RefreshPoTableOnly(); };
            _poTableSearchLabel = MakeSearchVisual("Search PO, Supplier...");
            _poTableSearchLabel.Size = new Size(170, 28);
            _poTableSearchLabel.Visible = false;
            _poTableSearchLabel.Click += (s, e) => { _poTableSearch.Focus(); _poTableSearchLabel.Visible = false; };
            FlowLayoutPanel filterBar = new FlowLayoutPanel
            {
                Location = new Point(16, 44),
                Size = new Size(card.Width - 32, 34),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.White
            };
            _poTable = new TableLayoutPanel { ColumnCount = 9, RowCount = 6, Location = new Point(16, 84), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.White };
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
            _poTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
            _poPager = new GlobalPaginationControl { Height = 34, Width = 560, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, BackColor = Color.Transparent };
            _poPager.PageChanged += (s, e) => { _poPage = _poPager.CurrentPage; RefreshPoTableOnly(); };
            _poPager.PageSizeChanged += (s, e) => { _poPageSize = _poPager.PageSize; _poPage = 1; RefreshPoTableOnly(); };
            _poPeriodFilterLabel.Visible = false;
            _poStatusFilterLabel.Visible = false;
            foreach (Control control in new Control[] { _poTableSearch, _poPeriodFilter, _poStatusFilter })
            {
                control.Margin = new Padding(8, 0, 0, 0);
                control.MinimumSize = control.Size;
            }
            filterBar.Controls.AddRange(new Control[] { _poTableSearch, _poPeriodFilter, _poStatusFilter });
            card.Controls.AddRange(new Control[] { filterBar, _poTable, _poPager });
            card.Resize += (s, e) =>
            {
                viewAll.Location = new Point(card.Width - 92, 12);
                filterBar.Size = new Size(card.Width - 32, 34);
                _poTableSearchLabel.Visible = false;
                _poTable.Size = new Size(card.Width - 32, card.Height - 132);
                _poPager.Width = Math.Max(260, Math.Min(560, card.Width - 36));
                _poPager.Location = new Point(card.Width - _poPager.Width - 18, card.Height - 42);
            };
            return card;
        }

        private Control BuildPoBottomRow()
        {
            TableLayoutPanel row = new TableLayoutPanel { Height = 156, BackColor = PoPageBg, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 16) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            Panel aging = MakePoCard();
            aging.Margin = new Padding(0, 0, 8, 0);
            aging.Controls.Add(new Label { Text = "PO Aging Summary", Location = new Point(16, 14), Size = new Size(220, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            _poAgingFlow = new FlowLayoutPanel { Location = new Point(16, 48), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
            aging.Controls.Add(_poAgingFlow);
            aging.Resize += (s, e) => _poAgingFlow.Size = new Size(aging.Width - 32, aging.Height - 62);
            Panel overdue = MakePoCard();
            overdue.Margin = new Padding(8, 0, 0, 0);
            overdue.Controls.Add(new Label { Text = "Overdue POs", Location = new Point(16, 14), Size = new Size(180, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            _poOverdueFlow = new FlowLayoutPanel { Location = new Point(16, 44), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            overdue.Controls.Add(_poOverdueFlow);
            overdue.Resize += (s, e) => _poOverdueFlow.Size = new Size(overdue.Width - 32, overdue.Height - 52);
            row.Controls.Add(aging, 0, 0);
            row.Controls.Add(overdue, 1, 0);
            return row;
        }

        private Panel BuildPoRightSidebar()
        {
            FlowLayoutPanel side = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = PoPageBg, Padding = new Padding(8, 0, 0, 0), AutoScroll = false };
            _poSummaryPanel = MakePoCard();
            _poSummaryPanel.Dock = DockStyle.None;
            _poSummaryPanel.Width = 280;
            _poSummaryPanel.Height = 180;
            _poTopSuppliersFlow = new FlowLayoutPanel { Width = 252, Height = 178, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = PoSurface };
            Panel suppliers = MakePoCard();
            suppliers.Dock = DockStyle.None;
            suppliers.Width = 280;
            suppliers.Height = 260;
            suppliers.Controls.Add(new Label { Text = "Top Suppliers", Location = new Point(16, 14), Size = new Size(120, 22), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            suppliers.Controls.Add(new Label { Text = "By PO Value", Location = new Point(176, 15), Size = new Size(86, 20), Font = new Font("Segoe UI", 8f), ForeColor = PoMuted, TextAlign = ContentAlignment.MiddleRight });
            _poTopSuppliersFlow.Location = new Point(14, 44);
            suppliers.Controls.Add(_poTopSuppliersFlow);
            Label allSuppliers = new Label { Text = "View All Suppliers →", Location = new Point(18, 228), Size = new Size(230, 22), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = InfoBlue, Cursor = Cursors.Hand };
            allSuppliers.Click += (s, e) => ShowTopSuppliersDialog();
            suppliers.Controls.Add(allSuppliers);
            Panel quick = MakePoCard();
            quick.Dock = DockStyle.None;
            quick.Width = 280;
            quick.Height = 214;
            quick.Controls.Add(new Label { Text = "Quick Actions", Location = new Point(16, 14), Size = new Size(160, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            AddQuickPoButton(quick, "+  New Purchase Order", 16, 48, 248, InfoBlue, Color.White);
            AddQuickPoButton(quick, "+  New RFQ", 16, 86, 120, Color.White, InfoBlue);
            AddQuickPoButton(quick, "Goods Receipt", 144, 86, 120, Color.White, SaveGreen);
            AddQuickPoButton(quick, "Supplier Return", 16, 124, 120, Color.White, WarnOrange);
            AddQuickPoButton(quick, "Import POs", 144, 124, 120, Color.White, PoMuted);
            AddQuickPoButton(quick, "Download Report", 16, 162, 248, Color.White, PoPurple);
            side.Controls.Add(_poSummaryPanel);
            side.Controls.Add(suppliers);
            side.Controls.Add(quick);
            return side;
        }

        private void RefreshPurchaseDashboard()
        {
            List<PurchaseOrder> orders = _orderSource ?? new List<PurchaseOrder>();
            RefreshPoStats(orders);
            RefreshPoCharts(orders);
            RefreshPoTableOnly();
            RefreshPoBottom(orders);
            RefreshPoSidebar(orders);
        }

        private void RefreshPoStats(List<PurchaseOrder> orders)
        {
            _poStatsFlow.Controls.Clear();
            DateTime today = DateTime.Today;
            List<PurchaseOrder> thisMonth = orders.Where(o => o.PODate.Year == today.Year && o.PODate.Month == today.Month).ToList();
            List<PurchaseOrder> lastMonth = orders.Where(o => o.PODate.Year == today.AddMonths(-1).Year && o.PODate.Month == today.AddMonths(-1).Month).ToList();
            decimal monthValue = thisMonth.Sum(o => o.TotalAmount);
            decimal lastValue = lastMonth.Sum(o => o.TotalAmount);
            decimal trend = lastValue <= 0 ? (monthValue > 0 ? 100m : 0m) : Math.Round((monthValue - lastValue) * 100m / lastValue, 1);
            List<PurchaseOrder> open = orders.Where(o => IsOpenPo(o)).ToList();
            List<PurchaseOrder> receivedMonth = thisMonth.Where(o => IsStatus(o, "Fully Received") || IsStatus(o, "Received") || IsStatus(o, "Partially Received") || IsStatus(o, "Partial")).ToList();
            List<PurchaseOrder> overdue = orders.Where(IsOverduePo).ToList();
            List<PurchaseOrder> pendingPay = orders.Where(o => o.BalanceDue > 0.01m).ToList();
            _poStatsFlow.Controls.Add(MakePoStat("Total PO Value", CompactCurrency(monthValue), "This Month", "↑ " + trend.ToString("0.#") + "% vs last month", PoPurple, ModernIconKind.Purchase, false));
            _poStatsFlow.Controls.Add(MakePoStat("Open POs", open.Count.ToString(), "Worth " + FormatCurrency(open.Sum(o => o.TotalAmount)), open.Count(o => NormalizePoStatus(o.Status) == "Pending Approval") + " Pending Approval", InfoBlue, ModernIconKind.Document, true));
            _poStatsFlow.Controls.Add(MakePoStat("Goods Received", receivedMonth.Count.ToString(), "This Month", "↑ " + Math.Max(0, receivedMonth.Count).ToString() + "% vs last month", SaveGreen, ModernIconKind.Inventory, true));
            _poStatsFlow.Controls.Add(MakePoStat("Overdue POs", overdue.Count.ToString(), "Worth " + FormatCurrency(overdue.Sum(o => o.TotalAmount)), "● Requires Attention", DelRed, ModernIconKind.Alert, true));
            _poStatsFlow.Controls.Add(MakePoStat("Pending Payment", CompactCurrency(pendingPay.Sum(o => o.BalanceDue)), "For " + pendingPay.Count + " POs", "● Due in next 30 days", WarnOrange, ModernIconKind.Payment, false));
        }

        private Panel MakePoStat(string label, string value, string sub, string trend, Color accent, ModernIconKind icon, bool chevron)
        {
            Panel card = MakePoCard();
            card.Dock = DockStyle.None;
            card.Width = 206;
            card.Height = 96;
            card.Margin = new Padding(0, 0, 8, 0);
            Label badge = new Label { Location = new Point(14, 18), Size = new Size(42, 42), BackColor = DS.Lighten(accent, 0.84f), Image = ModernIconSystem.IconBitmap(icon, 20, accent), ImageAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(badge, 12);
            card.Controls.Add(badge);
            card.Controls.Add(new Label { Text = label, Location = new Point(70, 14), Size = new Size(116, 18), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = PoMuted, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = value, Location = new Point(70, 34), Size = new Size(122, 24), Font = new Font("Segoe UI", 12.5f, FontStyle.Bold), ForeColor = PoText, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = sub, Location = new Point(70, 58), Size = new Size(122, 18), Font = new Font("Segoe UI", 8f), ForeColor = PoMuted, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = trend, Location = new Point(14, 76), Size = new Size(176, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true });
            if (chevron)
                card.Controls.Add(new Label { Text = ">", Location = new Point(card.Width - 24, 12), Size = new Size(16, 20), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = accent });
            return card;
        }

        private void RefreshPoCharts(List<PurchaseOrder> orders)
        {
            Dictionary<string, int> statusCounts = PoStatuses.ToDictionary(s => s, s => 0);
            foreach (PurchaseOrder po in orders)
            {
                string status = NormalizePoStatus(po.Status);
                if (statusCounts.ContainsKey(status)) statusCounts[status]++;
            }
            _poStatusChart.SetData(statusCounts);

            DateTime start = DateTime.Today.AddMonths(-11);
            string filter = _poTrendFilter?.SelectedItem?.ToString() ?? "This Year";
            if (filter == "This Year") start = new DateTime(DateTime.Today.Year, 1, 1);
            if (filter == "Last 6 Months") start = DateTime.Today.AddMonths(-5);
            List<MonthlyPoPoint> points = new List<MonthlyPoPoint>();
            DateTime cursor = new DateTime(start.Year, start.Month, 1);
            DateTime end = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            while (cursor <= end)
            {
                decimal value = orders.Where(o => o.PODate.Year == cursor.Year && o.PODate.Month == cursor.Month).Sum(o => o.TotalAmount);
                points.Add(new MonthlyPoPoint { Label = cursor.ToString("MMM"), Value = value });
                cursor = cursor.AddMonths(1);
            }
            _poTrendChart.SetData(points);
        }

        private void RefreshPoTableOnly()
        {
            if (_poTable == null) return;
            List<PurchaseOrder> filtered = GetFilteredPos();
            int pageSize = Math.Max(1, _poPageSize);
            _poPage = PaginationState.NormalizePage(_poPage, filtered.Count, pageSize);
            List<PurchaseOrder> page = filtered.Skip((_poPage - 1) * pageSize).Take(pageSize).ToList();
            RenderPoTable(page);
            RenderPoPagination(filtered.Count, pageSize);
        }

        private List<PurchaseOrder> GetFilteredPos()
        {
            IEnumerable<PurchaseOrder> query = _orderSource ?? new List<PurchaseOrder>();
            string topSearch = GetTextFilter(_poDashSearch, "Search PO number, supplier, item...");
            string tableSearch = GetTextFilter(_poTableSearch, "Search PO, Supplier...");
            string search = string.IsNullOrWhiteSpace(tableSearch) ? topSearch : tableSearch;
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(o => (o.PONumber ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (o.VendorName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (o.LineItems ?? new List<PurchaseLineItem>()).Any(i => (i.Description ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            string status = _poStatusFilter?.SelectedItem?.ToString() ?? "All Status";
            if (status != "All Status")
                query = query.Where(o => NormalizePoStatus(o.Status) == status);
            string period = _poPeriodFilter?.SelectedItem?.ToString() ?? "This Month";
            DateTime today = DateTime.Today;
            if (period == "This Month")
                query = query.Where(o => o.PODate.Year == today.Year && o.PODate.Month == today.Month);
            else if (period == "Last Month")
            {
                DateTime last = today.AddMonths(-1);
                query = query.Where(o => o.PODate.Year == last.Year && o.PODate.Month == last.Month);
            }
            else if (period == "This Quarter")
            {
                int q = ((today.Month - 1) / 3) + 1;
                query = query.Where(o => o.PODate.Year == today.Year && ((o.PODate.Month - 1) / 3) + 1 == q);
            }
            else if (period == "This Year")
                query = query.Where(o => o.PODate.Year == today.Year);
            return query.OrderByDescending(o => o.PODate).ThenByDescending(o => o.POID).ToList();
        }

        private void RenderPoTable(List<PurchaseOrder> rows)
        {
            _poTable.SuspendLayout();
            _poTable.Controls.Clear();
            _poTable.RowStyles.Clear();
            int pageSize = Math.Max(1, _poPageSize);
            _poTable.RowCount = pageSize + 1;
            _poTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            for (int i = 0; i < pageSize; i++) _poTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / pageSize));
            string[] headers = { "PO Number", "Supplier", "PO Date", "Required", "PO Value", "Received", "Status", "Payment", "Actions" };
            for (int i = 0; i < headers.Length; i++) _poTable.Controls.Add(MakePoCell(headers[i], true, PoText), i, 0);
            for (int row = 0; row < pageSize; row++)
            {
                if (row >= rows.Count)
                {
                    for (int c = 0; c < headers.Length; c++) _poTable.Controls.Add(MakePoCell("", false, PoText), c, row + 1);
                    continue;
                }
                PurchaseOrder po = rows[row];
                decimal received = Math.Max(0m, po.PaidAmount);
                decimal pct = po.TotalAmount <= 0 ? 0 : Math.Round(received * 100m / po.TotalAmount);
                _poTable.Controls.Add(MakePoLink(CleanPoNumber(po.PONumber, po.POID), po), 0, row + 1);
                _poTable.Controls.Add(MakePoRecordCell(Safe(po.VendorName, "Supplier #" + po.VendorID), po), 1, row + 1);
                _poTable.Controls.Add(MakePoRecordCell(FormatDate(po.PODate), po), 2, row + 1);
                _poTable.Controls.Add(MakePoRecordCell(FormatDate(po.PayByDate), po), 3, row + 1);
                _poTable.Controls.Add(MakePoRecordCell(FormatCurrency(po.TotalAmount), po), 4, row + 1);
                _poTable.Controls.Add(MakePoRecordCell(FormatCurrency(received) + " (" + pct.ToString("0") + "%)", po), 5, row + 1);
                _poTable.Controls.Add(MakePoBadge(NormalizePoStatus(po.Status), po), 6, row + 1);
                _poTable.Controls.Add(MakePaymentStatus(GetPaymentStatus(po), po), 7, row + 1);
                _poTable.Controls.Add(MakePoActions(po), 8, row + 1);
            }
            _poTable.ResumeLayout(true);
        }

        private void RenderPoPagination(int total, int pageSize)
        {
            if (_poPager != null)
                _poPager.SetState(_poPage, total, pageSize);
        }

        private void RefreshPoBottom(List<PurchaseOrder> orders)
        {
            _poAgingFlow.Controls.Clear();
            var open = orders.Where(o => !IsClosedPo(o)).ToList();
            AddAgingBucket("0 - 15 Days", open.Where(o => o.AgeDays <= 15).ToList(), SaveGreen);
            AddAgingBucket("16 - 30 Days", open.Where(o => o.AgeDays >= 16 && o.AgeDays <= 30).ToList(), DS.Teal600);
            AddAgingBucket("31 - 60 Days", open.Where(o => o.AgeDays >= 31 && o.AgeDays <= 60).ToList(), WarnOrange);
            AddAgingBucket("61 - 90 Days", open.Where(o => o.AgeDays >= 61 && o.AgeDays <= 90).ToList(), DelRed);
            AddAgingBucket("90+ Days", open.Where(o => o.AgeDays > 90).ToList(), Color.FromArgb(127, 29, 29));
            _poOverdueFlow.Controls.Clear();
            foreach (PurchaseOrder po in orders.Where(IsOverduePo).OrderBy(o => o.PayByDate).Take(4))
                _poOverdueFlow.Controls.Add(MakeOverdueRow(po));
            Label link = new Label { Text = "View All Overdue POs →", Width = 220, Height = 22, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = InfoBlue, Cursor = Cursors.Hand };
            link.Click += (s, e) => ShowOverduePurchaseOrdersDialog();
            _poOverdueFlow.Controls.Add(link);
        }

        private void RefreshPoSidebar(List<PurchaseOrder> orders)
        {
            _poSummaryPanel.Controls.Clear();
            _poSummaryPanel.Controls.Add(new Label { Text = "Purchase Summary", Location = new Point(16, 14), Size = new Size(180, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = PoText });
            decimal total = orders.Sum(o => o.TotalAmount);
            decimal received = orders.Sum(o => Math.Max(0m, o.PaidAmount));
            decimal overdue = orders.Where(IsOverduePo).Sum(o => o.TotalAmount);
            AddPoSummaryLine(_poSummaryPanel, "Total POs", orders.Count.ToString(), 44, PoText);
            AddPoSummaryLine(_poSummaryPanel, "Total PO Value", FormatCurrency(total), 66, PoText);
            AddPoSummaryLine(_poSummaryPanel, "Received Value", FormatCurrency(received), 88, PoText);
            AddPoSummaryLine(_poSummaryPanel, "Pending Value", FormatCurrency(Math.Max(0m, total - received)), 110, PoText);
            AddPoSummaryLine(_poSummaryPanel, "Overdue Value", FormatCurrency(overdue), 132, DelRed);
            Label detailReport = new Label { Text = "View Detailed Report →", Location = new Point(16, 154), Size = new Size(200, 20), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = InfoBlue, Cursor = Cursors.Hand };
            detailReport.Click += (s, e) => ShowPurchaseReportDialog();
            _poSummaryPanel.Controls.Add(detailReport);

            _poTopSuppliersFlow.Controls.Clear();
            decimal grand = Math.Max(1m, total);
            int rank = 1;
            foreach (var supplier in orders.GroupBy(o => Safe(o.VendorName, "Supplier #" + o.VendorID)).Select(g => new { Name = g.Key, Value = g.Sum(o => o.TotalAmount), Count = g.Count() }).OrderByDescending(g => g.Value).Take(5))
            {
                Panel row = new Panel { Width = 250, Height = 27, BackColor = PoSurface, Margin = new Padding(0, 0, 0, 5) };
                row.Controls.Add(new Label { Text = rank.ToString(), Location = new Point(0, 3), Size = new Size(22, 19), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = PoMuted, TextAlign = ContentAlignment.MiddleCenter });
                row.Controls.Add(new Label { Text = supplier.Name, Location = new Point(28, 2), Size = new Size(118, 20), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = PoText, AutoEllipsis = true });
                row.Controls.Add(new Label { Text = FormatCurrency(supplier.Value), Location = new Point(142, 2), Size = new Size(72, 20), Font = new Font("Segoe UI", 8f), ForeColor = PoText, TextAlign = ContentAlignment.MiddleRight });
                row.Controls.Add(new Label { Text = Math.Round(supplier.Value * 100m / grand).ToString("0") + "%", Location = new Point(218, 2), Size = new Size(32, 20), Font = new Font("Segoe UI", 8f), ForeColor = PoMuted, TextAlign = ContentAlignment.MiddleRight });
                _poTopSuppliersFlow.Controls.Add(row);
                rank++;
            }
        }

        private static readonly string[] PoStatuses = { "Draft", "Pending Approval", "Approved", "Partially Received", "Fully Received", "Cancelled" };

        private static string NormalizePoStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "Draft";
            status = status.Trim();
            if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "Pending Approval";
            if (status.Equals("Partial", StringComparison.OrdinalIgnoreCase)) return "Partially Received";
            if (status.Equals("Received", StringComparison.OrdinalIgnoreCase) || status.Equals("Paid", StringComparison.OrdinalIgnoreCase) || status.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return "Fully Received";
            return PoStatuses.Contains(status) ? status : "Approved";
        }

        private static bool IsStatus(PurchaseOrder po, string status) => NormalizePoStatus(po?.Status) == status;
        private static bool IsOpenPo(PurchaseOrder po) => new[] { "Draft", "Pending Approval", "Approved" }.Contains(NormalizePoStatus(po.Status));
        private static bool IsClosedPo(PurchaseOrder po) => new[] { "Fully Received", "Cancelled" }.Contains(NormalizePoStatus(po.Status));
        private static bool IsOverduePo(PurchaseOrder po) => po != null && po.PayByDate.Year > 2000 && po.IsOverdue;
        private static string GetPaymentStatus(PurchaseOrder po) => po == null ? "Pending" : (po.IsPaymentCompleted || (po.TotalAmount > 0 && po.BalanceDue <= 0.01m)) ? "Paid" : po.PaidAmount > 0 ? "Partial" : "Pending";
        private static string FormatDate(DateTime date) => date == default || date.Year < 2001 ? "-" : date.ToString("dd/MM/yyyy");
        private static string FormatCurrency(decimal value) => IndiaFormatHelper.FormatCurrency(value);
        private static string CompactCurrency(decimal value)
        {
            decimal abs = Math.Abs(value);
            if (abs >= 10000000m)
                return "₹" + (value / 10000000m).ToString("0.##") + "Cr";
            if (abs >= 100000m)
                return "₹" + (value / 100000m).ToString("0.##") + "L";
            return IndiaFormatHelper.FormatCurrency(value);
        }
        private static string Safe(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
        private static string CleanPoNumber(string value, int id)
        {
            string fallback = "PO-" + DateTime.Today.ToString("ddMM") + "-" + id.ToString("0000");
            value = Safe(value, fallback).Trim();
            if (value.Length > 18)
                value = value.Substring(0, 18);
            return value;
        }

        private Panel MakePoCard()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = PoSurface };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(PoSurface))
                using (Pen pen = new Pen(PoBorder))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            AttachPurchaseDashboardResize(panel);
            return panel;
        }

        private void AttachPurchaseDashboardResize(Panel card)
        {
            CardResizeGripService.Attach(card, ApplyPurchaseDashboardCardSize, ApplyPurchaseDashboardCardSize);
        }

        private void ApplyPurchaseDashboardCardSize(Control card, Size size)
        {
            if (card == null || card.Parent == null)
                return;

            TableLayoutPanel table = card.Parent as TableLayoutPanel;
            if (table != null)
            {
                if (table.RowCount == 1 && table.Height < size.Height)
                    table.Height = size.Height;
                if (table.RowCount == 1 && table.RowStyles.Count > 0)
                    table.RowStyles[0] = new RowStyle(SizeType.Absolute, Math.Max(96, size.Height));

                int column = table.GetColumn(card);
                if (column >= 0 && column < table.ColumnStyles.Count && size.Width > 0)
                    table.ColumnStyles[column] = new ColumnStyle(SizeType.Absolute, Math.Max(180, size.Width));
            }
            else if (card.Parent is FlowLayoutPanel)
            {
                card.Dock = DockStyle.None;
                card.Size = new Size(Math.Max(180, size.Width), Math.Max(96, size.Height));
            }

            card.Parent.PerformLayout();
        }

        private Button MakePoButton(string text, Color back, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 34, BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = 0;
            DS.Rounded(b, 7);
            return b;
        }

        private Button MakePoOutlineButton(string text, int width)
        {
            Button b = MakePoButton(text, Color.White, width);
            b.ForeColor = InfoBlue;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = PoBorder;
            return b;
        }

        private Label MakePoCell(string text, bool header, Color fore)
        {
            Label label = new Label { Text = text, Dock = DockStyle.Fill, BackColor = header ? Color.FromArgb(248, 250, 252) : Color.White, ForeColor = fore, Font = new Font("Segoe UI", header ? 7.6f : 7.4f, header ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 2, 0), AutoEllipsis = true, UseMnemonic = false };
            label.Paint += (s, e) => { using (Pen p = new Pen(Color.FromArgb(241, 245, 249))) e.Graphics.DrawLine(p, 0, label.Height - 1, label.Width, label.Height - 1); };
            return label;
        }

        private Label MakePoRecordCell(string text, PurchaseOrder po)
        {
            Label label = MakePoCell(text, false, PoText);
            label.Cursor = Cursors.Hand;
            label.Click += (s, e) => OpenStoredPurchaseOrderPdfFromDashboard(po);
            return label;
        }

        private Label MakePoLink(string text, PurchaseOrder po)
        {
            Label label = MakePoCell(Safe(text, "PO #" + (po?.POID ?? 0)), false, InfoBlue);
            label.Cursor = Cursors.Hand;
            label.Font = new Font("Segoe UI", 7.4f, FontStyle.Bold);
            label.Click += (s, e) => OpenPurchaseOrderPdfFromDashboard(po);
            return label;
        }

        private Control MakePoBadge(string status, PurchaseOrder po = null)
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(4, 8, 4, 8), Cursor = po == null ? Cursors.Default : Cursors.Hand };
            Label pill = new Label { Text = status, Height = 22, Width = 100, BackColor = DS.Lighten(StatusColor(status), 0.84f), ForeColor = StatusColor(status), Font = new Font("Segoe UI", 7f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            DS.Rounded(pill, 11);
            host.Controls.Add(pill);
            if (po != null)
            {
                host.Click += (s, e) => OpenPurchaseOrderPdfFromDashboard(po);
                pill.Cursor = Cursors.Hand;
                pill.Click += (s, e) => OpenPurchaseOrderPdfFromDashboard(po);
            }
            return host;
        }

        private Control MakePaymentStatus(string status, PurchaseOrder po = null)
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Cursor = po == null ? Cursors.Default : Cursors.Hand };
            Color c = status == "Paid" ? SaveGreen : status == "Partial" ? WarnOrange : Color.FromArgb(202, 138, 4);
            Label label = new Label { Text = "●  " + status, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7.6f, FontStyle.Bold), ForeColor = c, TextAlign = ContentAlignment.MiddleLeft };
            host.Controls.Add(label);
            if (po != null)
            {
                host.Click += (s, e) => OpenPurchaseOrderPdfFromDashboard(po);
                label.Cursor = Cursors.Hand;
                label.Click += (s, e) => OpenPurchaseOrderPdfFromDashboard(po);
            }
            return host;
        }

        /// <summary>Opens the selected dashboard purchase order as a PDF, generating one when no linked PDF exists.</summary>
        private void OpenPurchaseOrderPdfFromDashboard(PurchaseOrder po)
        {
            if (po == null)
                return;

            try
            {
                PurchaseOrder printable = po.POID > 0 ? _svc.GetById(po.POID) ?? po : po;
                if (RecentDocumentOpenService.OpenPdf(this, printable))
                {
                    SetStatus("Opened PO PDF: " + CleanPoNumber(printable.PONumber, printable.POID), SaveGreen);
                    return;
                }

                string pdfPath = CreateTemporaryPurchaseOrderPdf(printable);
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                SetStatus("Opened generated PO PDF: " + CleanPoNumber(printable.PONumber, printable.POID), SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Opening purchase order PDF", ex);
                SetStatus("Purchase order PDF could not open. Try Download / Print from the action menu.", Color.Red);
            }
        }

        /// <summary>Opens only the saved PDF linked to the selected purchase order number.</summary>
        private void OpenStoredPurchaseOrderPdfFromDashboard(PurchaseOrder po)
        {
            if (po == null)
                return;

            try
            {
                PurchaseOrder printable = po.POID > 0 ? _svc.GetById(po.POID) ?? po : po;
                if (RecentDocumentOpenService.OpenPdf(this, printable))
                {
                    SetStatus("Opened stored PO PDF: " + CleanPoNumber(printable.PONumber, printable.POID), SaveGreen);
                    return;
                }

                MessageBox.Show(
                    this,
                    "No stored PDF is linked to " + CleanPoNumber(printable.PONumber, printable.POID) + ". Use View or Download / Print to generate a fresh PO PDF.",
                    BrandingService.WindowTitle("Purchase Orders"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                SetStatus("No stored PO PDF is linked. Use View or Download / Print to generate one.", WarnOrange);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Opening stored purchase order PDF", ex);
                SetStatus("Stored PO PDF could not open. Check the linked file path.", Color.Red);
            }
        }

        /// <summary>Creates a temporary PDF for dashboard viewing without asking the user for a save path.</summary>
        private string CreateTemporaryPurchaseOrderPdf(PurchaseOrder po)
        {
            string stem = SanitizeFolderName(CleanPoNumber(po?.PONumber, po?.POID ?? 0));
            string pdfPath = Path.Combine(Path.GetTempPath(), stem + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf");
            string html = _svc.BuildPurchaseOrderHtml(po);
            ExportHtmlToPdf(html, pdfPath);
            return pdfPath;
        }

        private Control MakePoActions(PurchaseOrder po)
        {
            Button b = new Button { Text = "View", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = InfoBlue, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => OpenPurchaseOrderPdfFromDashboard(po);
            b.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ShowPoActionMenu(b, po);
            };
            return b;
        }

        /// <summary>Shows secondary dashboard purchase-order actions without hiding the primary View command.</summary>
        private void ShowPoActionMenu(Control anchor, PurchaseOrder po)
        {
            if (anchor == null || po == null)
                return;

            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("View", null, (a, b2) => OpenPurchaseOrderPdfFromDashboard(po));
            menu.Items.Add("Summary", null, (a, b2) => ShowPurchaseOrderSummary(po));
            menu.Items.Add("Download / Print", null, (a, b2) => DownloadDashboardPurchaseOrder(po));
            menu.Items.Add("Edit", null, (a, b2) => ShowPurchaseOrderSummary(po));
            menu.Items.Add("Duplicate", null, (a, b2) => DuplicateDashboardPurchaseOrder(po));
            menu.Items.Add("Cancel", null, (a, b2) => CancelDashboardPurchaseOrder(po));
            menu.Items.Add("Delete", null, (a, b2) => DeleteDashboardPurchaseOrder(po));
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void AddPoPageButton(string text, int page, bool enabled, bool selected = false)
        {
            Button b = new Button { Text = text, Width = 30, Height = 28, Enabled = enabled, BackColor = selected ? InfoBlue : Color.White, ForeColor = selected ? Color.White : PoText, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(2) };
            b.FlatAppearance.BorderColor = PoBorder;
            b.Click += (s, e) => { _poPage = page; RefreshPoTableOnly(); };
            _poPager.Controls.Add(b);
        }

        private void AddAgingBucket(string label, List<PurchaseOrder> orders, Color color)
        {
            Panel card = new Panel { Width = 118, Height = 82, BackColor = Color.White, Margin = new Padding(0, 0, 8, 0) };
            card.Paint += (s, e) => { using (Pen p = new Pen(PoBorder)) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1); };
            card.Controls.Add(new Label { Text = "◷", Location = new Point(10, 10), Size = new Size(22, 22), BackColor = DS.Lighten(color, 0.84f), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter });
            card.Controls.Add(new Label { Text = label, Location = new Point(38, 10), Size = new Size(72, 18), Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = PoMuted, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = orders.Count + " POs", Location = new Point(12, 34), Size = new Size(96, 20), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = PoText, TextAlign = ContentAlignment.MiddleCenter });
            card.Controls.Add(new Label { Text = FormatCurrency(orders.Sum(o => o.TotalAmount)), Location = new Point(8, 58), Size = new Size(102, 18), Font = new Font("Segoe UI", 7.5f), ForeColor = PoMuted, TextAlign = ContentAlignment.MiddleCenter });
            _poAgingFlow.Controls.Add(card);
        }

        private Control MakeOverdueRow(PurchaseOrder po)
        {
            Panel row = new Panel { Width = 230, Height = 24, BackColor = PoSurface, Margin = new Padding(0, 0, 0, 4) };
            int days = Math.Max(0, (DateTime.Today - po.PayByDate.Date).Days);
            Color c = days > 10 ? DelRed : days >= 5 ? WarnOrange : PendingAmber;
            row.Controls.Add(new Label { Text = Safe(po.PONumber, "PO #" + po.POID), Location = new Point(0, 2), Size = new Size(72, 18), ForeColor = InfoBlue, Font = new Font("Segoe UI", 7.3f, FontStyle.Bold), AutoEllipsis = true });
            row.Controls.Add(new Label { Text = Safe(po.VendorName, "Supplier"), Location = new Point(74, 2), Size = new Size(58, 18), ForeColor = PoText, Font = new Font("Segoe UI", 7.2f), AutoEllipsis = true });
            row.Controls.Add(new Label { Text = days + " Days Overdue", Location = new Point(134, 2), Size = new Size(62, 18), ForeColor = c, Font = new Font("Segoe UI", 6.8f, FontStyle.Bold), AutoEllipsis = true });
            row.Controls.Add(new Label { Text = FormatCurrency(po.TotalAmount), Location = new Point(194, 2), Size = new Size(36, 18), ForeColor = PoText, Font = new Font("Segoe UI", 7f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight });
            return row;
        }

        private void AddPoSummaryLine(Control parent, string label, string value, int y, Color valueColor)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(16, y), Size = new Size(112, 18), Font = new Font("Segoe UI", 8f), ForeColor = PoMuted });
            parent.Controls.Add(new Label { Text = value, Location = new Point(132, y), Size = new Size(130, 18), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = valueColor, TextAlign = ContentAlignment.MiddleRight });
        }

        private void AddQuickPoButton(Control parent, string text, int x, int y, int width, Color back, Color fore)
        {
            Button b = new Button { Text = text, Tag = "FIXED_WIDTH", Location = new Point(x, y), Size = new Size(width, 30), BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderColor = back == Color.White ? PoBorder : back;
            b.Click += (s, e) => HandleQuickPoAction(text);
            DS.Rounded(b, 6);
            parent.Controls.Add(b);
        }

        private void HandleQuickPoAction(string text)
        {
            if (text.IndexOf("New Purchase", StringComparison.OrdinalIgnoreCase) >= 0)
                _ = OpenNewPurchaseOrderFormAsync();
            else if (text.IndexOf("RFQ", StringComparison.OrdinalIgnoreCase) >= 0)
                OnNavigate?.Invoke(7);
            else if (text.IndexOf("Goods Receipt", StringComparison.OrdinalIgnoreCase) >= 0)
                ShowOverduePurchaseOrdersDialog();
            else if (text.IndexOf("Supplier Return", StringComparison.OrdinalIgnoreCase) >= 0)
                ShowDashboardAction("Supplier Return", "Select the source PO from the table actions, then record the supplier return against that order.");
            else if (text.IndexOf("Import", StringComparison.OrdinalIgnoreCase) >= 0)
                ImportDashboardPurchaseOrders();
            else if (text.IndexOf("Download", StringComparison.OrdinalIgnoreCase) >= 0)
                ExportPurchaseOrdersDashboardCsv();
        }

        private async Task OpenNewPurchaseOrderFormAsync()
        {
            _showDashboard = false;
            Controls.Clear();
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPurchaseReferenceSkin(Controls);
            await LoadInitialDataAsync();
            NewRecord();
            SetStatus("New purchase order form opened. Next: select vendor, add line items, and save the draft.", SaveGreen);
        }

        private async Task BackToPurchaseDashboardAsync()
        {
            _showDashboard = true;
            Controls.Clear();
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPurchaseReferenceSkin(Controls);
            await LoadPurchaseDashboardAsync();
        }

        private void ResetPurchaseOrdersDashboardFilters()
        {
            if (_poDashSearch != null) _poDashSearch.Text = "Search PO number, supplier, item...";
            if (_poTableSearch != null) _poTableSearch.Text = "Search PO, Supplier...";
            if (_poStatusFilter != null) _poStatusFilter.SelectedIndex = 0;
            if (_poPeriodFilter != null) _poPeriodFilter.SelectedItem = "This Year";
            if (_poStatusFilterLabel != null) _poStatusFilterLabel.Text = "All Status";
            if (_poPeriodFilterLabel != null) _poPeriodFilterLabel.Text = "This Year";
            _poPage = 1;
            RefreshPurchaseDashboard();
        }

        private void ShowDashboardAction(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPurchaseOrderSummary(PurchaseOrder po)
        {
            if (po == null) return;
            MessageBox.Show(
                "PO Number: " + CleanPoNumber(po.PONumber, po.POID) + Environment.NewLine +
                "Supplier: " + Safe(po.VendorName, "Supplier #" + po.VendorID) + Environment.NewLine +
                "PO Date: " + FormatDate(po.PODate) + Environment.NewLine +
                "Required Date: " + FormatDate(po.PayByDate) + Environment.NewLine +
                "Value: " + FormatCurrency(po.TotalAmount) + Environment.NewLine +
                "Status: " + NormalizePoStatus(po.Status),
                "Purchase Order",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void DownloadDashboardPurchaseOrder(PurchaseOrder po)
        {
            if (po == null) return;
            try
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Filter = "PDF files (*.pdf)|*.pdf|HTML files (*.html)|*.html";
                    dlg.FileName = CleanPoNumber(po.PONumber, po.POID) + ".pdf";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string html = _svc.BuildPurchaseOrderHtml(po);
                    if (Path.GetExtension(dlg.FileName).Equals(".html", StringComparison.OrdinalIgnoreCase))
                        File.WriteAllText(dlg.FileName, html, Encoding.UTF8);
                    else
                        ExportHtmlToPdf(html, dlg.FileName);
                    SetStatus("PO exported: " + Path.GetFileName(dlg.FileName), SaveGreen);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Exporting purchase order", ex);
                SetStatus("Purchase order export could not complete. Try again.", Color.Red);
            }
        }

        private void DuplicateDashboardPurchaseOrder(PurchaseOrder source)
        {
            if (source == null) return;
            try
            {
                PurchaseOrder clone = new PurchaseOrder
                {
                    VendorID = source.VendorID,
                    ClientID = source.ClientID,
                    SiteID = source.SiteID,
                    RelatedContractID = source.RelatedContractID,
                    RecommendedByBidID = source.RecommendedByBidID,
                    VendorName = source.VendorName,
                    VendorGSTIN = source.VendorGSTIN,
                    ClientName = source.ClientName,
                    SiteName = source.SiteName,
                    PONumber = "PO-" + DateTime.Now.ToString("ddMMyyyy-HHmm"),
                    PODate = DateTime.Today,
                    PayByDate = DateTime.Today.AddDays(7),
                    VendorInvoiceNumber = source.VendorInvoiceNumber,
                    LinkedToType = source.LinkedToType,
                    LinkedToId = source.LinkedToId,
                    LinkedToLabel = source.LinkedToLabel,
                    DeliveryMode = source.DeliveryMode,
                    AssignedTechnicianId = source.AssignedTechnicianId,
                    AssignedTechnicianName = source.AssignedTechnicianName,
                    DeliveryAddress = source.DeliveryAddress,
                    AddToClientInvoice = source.AddToClientInvoice,
                    TotalAmount = source.TotalAmount,
                    PaidAmount = 0m,
                    Status = "Pending",
                    Notes = "Duplicated from " + CleanPoNumber(source.PONumber, source.POID),
                    LineItems = (source.LineItems ?? new List<PurchaseLineItem>()).Select(i => new PurchaseLineItem
                    {
                        Description = i.Description,
                        HsnSacCode = i.HsnSacCode,
                        Quantity = i.Quantity,
                        UOM = i.UOM,
                        Rate = i.Rate,
                        GSTRate = i.GSTRate,
                        CGSTRate = i.CGSTRate,
                        SGSTRate = i.SGSTRate,
                        IGSTRate = i.IGSTRate,
                        JobLink = i.JobLink,
                        LinkedWorkOrderId = i.LinkedWorkOrderId,
                        LinkedWorkOrderName = i.LinkedWorkOrderName,
                        Amount = i.Amount
                    }).ToList()
                };
                int id = _svc.Create(clone);
                _orderSource = _svc.GetAllFresh();
                RefreshPurchaseDashboard();
                SetStatus("PO duplicated as " + CleanPoNumber(clone.PONumber, id) + ". Next: review vendor, dates, and quantities before saving.", SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Duplicating purchase order", ex);
                SetStatus("Purchase order could not be duplicated. Try again.", Color.Red);
            }
        }

        private void CancelDashboardPurchaseOrder(PurchaseOrder po)
        {
            if (po == null) return;
            if (MessageBox.Show("Cancel " + CleanPoNumber(po.PONumber, po.POID) + "?", "Cancel PO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                PurchaseOrder existing = _svc.GetById(po.POID) ?? po;
                existing.Status = "Cancelled";
                _svc.Update(existing);
                _orderSource = _svc.GetAllFresh();
                RefreshPurchaseDashboard();
                SetStatus("Purchase order cancelled.", DelRed);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Cancelling purchase order", ex);
                SetStatus("Purchase order could not be cancelled. Refresh and try again.", Color.Red);
            }
        }

        private void DeleteDashboardPurchaseOrder(PurchaseOrder po)
        {
            if (po == null || po.POID <= 0) return;
            if (MessageBox.Show("Delete " + CleanPoNumber(po.PONumber, po.POID) + "? This cannot be undone.", "Delete PO", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                _svc.Delete(po.POID);
                _orderSource = _svc.GetAllFresh();
                RefreshPurchaseDashboard();
                SetStatus("Purchase order deleted.", DelRed);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Deleting purchase order", ex);
                SetStatus("Purchase order could not be deleted. Refresh and try again.", Color.Red);
            }
        }

        private void ShowOverduePurchaseOrdersDialog()
        {
            List<PurchaseOrder> overdue = (_orderSource ?? new List<PurchaseOrder>()).Where(IsOverduePo).OrderBy(o => o.PayByDate).ToList();
            ShowPurchaseOrderListDialog("Overdue Purchase Orders", overdue);
        }

        private void ShowTopSuppliersDialog()
        {
            List<PurchaseOrder> orders = _orderSource ?? new List<PurchaseOrder>();
            string body = string.Join(Environment.NewLine, orders
                .GroupBy(o => Safe(o.VendorName, "Supplier #" + o.VendorID))
                .Select(g => new { Name = g.Key, Value = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .OrderByDescending(g => g.Value)
                .Select(g => g.Name + " - " + FormatCurrency(g.Value) + " across " + g.Count + " POs"));
            MessageBox.Show(string.IsNullOrWhiteSpace(body) ? "No suppliers found." : body, "Top Suppliers", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPurchaseReportDialog()
        {
            List<PurchaseOrder> orders = _orderSource ?? new List<PurchaseOrder>();
            decimal total = orders.Sum(o => o.TotalAmount);
            decimal received = orders.Sum(o => Math.Max(0m, o.PaidAmount));
            decimal overdue = orders.Where(IsOverduePo).Sum(o => o.TotalAmount);
            MessageBox.Show(
                "Total POs: " + orders.Count + Environment.NewLine +
                "Total PO Value: " + FormatCurrency(total) + Environment.NewLine +
                "Received Value: " + FormatCurrency(received) + Environment.NewLine +
                "Pending Value: " + FormatCurrency(Math.Max(0m, total - received)) + Environment.NewLine +
                "Overdue Value: " + FormatCurrency(overdue),
                "Purchase Detailed Report",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowPurchaseOrderListDialog(string title, List<PurchaseOrder> orders)
        {
            string body = string.Join(Environment.NewLine, (orders ?? new List<PurchaseOrder>()).Take(20).Select(o => CleanPoNumber(o.PONumber, o.POID) + " - " + Safe(o.VendorName, "Supplier") + " - " + FormatCurrency(o.TotalAmount) + " - " + NormalizePoStatus(o.Status)));
            MessageBox.Show(string.IsNullOrWhiteSpace(body) ? "No purchase orders found." : body, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ImportDashboardPurchaseOrders()
        {
            ImportUiHelper.RunImport(ExcelImportModule.Purchases, FindForm());
        }

        private void ExportPurchaseOrdersDashboardCsv()
        {
            List<PurchaseOrder> rows = GetFilteredPos();
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV files (*.csv)|*.csv";
                dlg.FileName = "purchase-orders-" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("PO Number,Supplier,PO Date,Required Date,PO Value,Received,Status,Payment Status");
                foreach (PurchaseOrder po in rows)
                    csv.AppendLine(string.Join(",", new[]
                    {
                        Csv(CleanPoNumber(po.PONumber, po.POID)),
                        Csv(Safe(po.VendorName, "Supplier #" + po.VendorID)),
                        Csv(FormatDate(po.PODate)),
                        Csv(FormatDate(po.PayByDate)),
                        Csv(po.TotalAmount.ToString("0.##")),
                        Csv(Math.Max(0m, po.PaidAmount).ToString("0.##")),
                        Csv(NormalizePoStatus(po.Status)),
                        Csv(GetPaymentStatus(po))
                    }));
                File.WriteAllText(dlg.FileName, csv.ToString(), Encoding.UTF8);
                SetStatus("Purchase report exported.", SaveGreen);
            }
        }

        private static string Csv(string value)
        {
            value = value ?? "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static Color StatusColor(string status)
        {
            switch (status)
            {
                case "Draft": return PoMuted;
                case "Pending Approval": return WarnOrange;
                case "Approved": return InfoBlue;
                case "Partially Received": return Color.FromArgb(234, 88, 12);
                case "Fully Received": return SaveGreen;
                case "Cancelled": return DelRed;
                default: return InfoBlue;
            }
        }

        private static string GetTextFilter(TextBox box, string placeholder)
        {
            if (box == null || box.Text == placeholder || box.ForeColor == DS.Slate400) return string.Empty;
            return box.Text.Trim();
        }

        private static void AddDashboardPlaceholder(TextBox box, string placeholder)
        {
            box.GotFocus += (s, e) =>
            {
                if (box.Text == placeholder)
                {
                    box.Text = string.Empty;
                    box.ForeColor = PoText;
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = placeholder;
                    box.ForeColor = DS.Slate400;
                }
            };
        }

        private sealed class MonthlyPoPoint
        {
            public string Label { get; set; }
            public decimal Value { get; set; }
        }

        private sealed class PoStatusDonutChart : Control
        {
            private Dictionary<string, int> _data = new Dictionary<string, int>();
            private readonly Color[] _colors = { PoMuted, InfoBlue, PoPurple, WarnOrange, SaveGreen, DelRed };
            public void SetData(Dictionary<string, int> data) { _data = data ?? new Dictionary<string, int>(); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(PoSurface);
                int total = _data.Values.DefaultIfEmpty(0).Sum();
                int size = Math.Min(126, Height - 24);
                Rectangle rect = new Rectangle(18, Math.Max(24, (Height - size) / 2), size, size);
                float start = -90;
                for (int i = 0; i < PoStatuses.Length; i++)
                {
                    int count = _data.ContainsKey(PoStatuses[i]) ? _data[PoStatuses[i]] : 0;
                    float sweep = total == 0 ? 0 : count * 360f / total;
                    using (Pen p = new Pen(_colors[i], 24)) e.Graphics.DrawArc(p, rect, start, sweep);
                    start += sweep;
                }
                TextRenderer.DrawText(e.Graphics, total.ToString(), new Font("Segoe UI", 16f, FontStyle.Bold), new Rectangle(rect.X, rect.Y + 36, rect.Width, 28), PoText, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(e.Graphics, "Total POs", new Font("Segoe UI", 8f), new Rectangle(rect.X, rect.Y + 64, rect.Width, 20), PoMuted, TextFormatFlags.HorizontalCenter);
                int x = rect.Right + 36;
                for (int i = 0; i < PoStatuses.Length; i++)
                {
                    int count = _data.ContainsKey(PoStatuses[i]) ? _data[PoStatuses[i]] : 0;
                    int pct = total == 0 ? 0 : (int)Math.Round(count * 100m / total);
                    using (SolidBrush b = new SolidBrush(_colors[i])) e.Graphics.FillEllipse(b, x, 22 + i * 24, 8, 8);
                    TextRenderer.DrawText(e.Graphics, PoStatuses[i], new Font("Segoe UI", 8f), new Rectangle(x + 16, 17 + i * 24, Width - x - 80, 20), PoText, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(e.Graphics, count + " (" + pct + "%)", new Font("Segoe UI", 8f, FontStyle.Bold), new Rectangle(Width - 76, 17 + i * 24, 70, 20), PoText, TextFormatFlags.Right);
                }
            }
        }

        private sealed class PoValueTrendChart : Control
        {
            private List<MonthlyPoPoint> _points = new List<MonthlyPoPoint>();
            public void SetData(List<MonthlyPoPoint> points) { _points = points ?? new List<MonthlyPoPoint>(); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(PoSurface);
                Rectangle plot = new Rectangle(42, 16, Width - 60, Height - 44);
                using (Pen grid = new Pen(DS.Border))
                    for (int i = 0; i <= 4; i++) e.Graphics.DrawLine(grid, plot.Left, plot.Top + plot.Height * i / 4, plot.Right, plot.Top + plot.Height * i / 4);
                decimal max = Math.Max(1m, _points.Select(p => p.Value).DefaultIfEmpty(0).Max());
                PointF[] pts = _points.Select((p, i) => new PointF(plot.Left + (plot.Width * i / Math.Max(1, _points.Count - 1)), plot.Bottom - (float)(p.Value / max) * plot.Height)).ToArray();
                if (pts.Length > 1)
                {
                    using (Pen line = new Pen(InfoBlue, 2)) e.Graphics.DrawLines(line, pts);
                    foreach (PointF p in pts)
                        using (SolidBrush b = new SolidBrush(InfoBlue)) e.Graphics.FillEllipse(b, p.X - 4, p.Y - 4, 8, 8);
                }
                for (int i = 0; i < _points.Count; i++)
                {
                    float x = plot.Left + (plot.Width * i / Math.Max(1, _points.Count - 1));
                    TextRenderer.DrawText(e.Graphics, _points[i].Label, new Font("Segoe UI", 7f), new Rectangle((int)x - 18, plot.Bottom + 4, 36, 18), PoMuted, TextFormatFlags.HorizontalCenter);
                }
                TextRenderer.DrawText(e.Graphics, "₹" + Math.Round(max / 100000m) + "L", new Font("Segoe UI", 7f), new Rectangle(0, plot.Top, 38, 18), PoMuted, TextFormatFlags.Right);
                TextRenderer.DrawText(e.Graphics, "₹0", new Font("Segoe UI", 7f), new Rectangle(0, plot.Bottom - 10, 38, 18), PoMuted, TextFormatFlags.Right);
            }
        }

        private Control BuildLeftRailContent()
        {
            Panel wrap = CreateCardPanel();
            wrap.Dock = DockStyle.Fill;
            wrap.Padding = new Padding(14);

            Panel top = new Panel { Dock = DockStyle.Top, Height = 158, BackColor = Color.White };
            top.Controls.Add(new Label
            {
                Text = "+  Purchase Orders",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = DS.Slate900,
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0)
            });

            _btnAllTab = MakeTabButton("All");
            _btnPendingTab = MakeTabButton("Pending");
            _btnPartialTab = MakeTabButton("Partial");
            _btnApprovedTab = MakeTabButton("Approved");
            _btnClosedTab = MakeTabButton("Closed");
            _btnAllTab.Click += (s, e) => { _activeTab = PurchaseListTab.AllPurchaseOrders; ResetListPageAndRender(); };
            _btnPendingTab.Click += (s, e) => { _activeTab = PurchaseListTab.PendingOrders; ResetListPageAndRender(); };
            _btnPartialTab.Click += (s, e) => { _activeTab = PurchaseListTab.PartialOrders; ResetListPageAndRender(); };
            _btnApprovedTab.Click += (s, e) => { _activeTab = PurchaseListTab.ApprovedOrders; ResetListPageAndRender(); };
            _btnClosedTab.Click += (s, e) => { _activeTab = PurchaseListTab.ClosedOrders; ResetListPageAndRender(); };

            TableLayoutPanel tabGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.White,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new Padding(0, 6, 0, 2),
                Margin = new Padding(0)
            };
            tabGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            tabGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            tabGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            tabGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tabGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            AddTabButtonToGrid(tabGrid, _btnAllTab, 0, 0);
            AddTabButtonToGrid(tabGrid, _btnPendingTab, 1, 0);
            AddTabButtonToGrid(tabGrid, _btnPartialTab, 2, 0);
            AddTabButtonToGrid(tabGrid, _btnClosedTab, 0, 1);
            AddTabButtonToGrid(tabGrid, _btnApprovedTab, 1, 1);

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            _txtSearch.Text = SearchPlaceholder;
            _txtSearch.ForeColor = DS.Slate400;
            _txtSearch.GotFocus += (s, e) =>
            {
                if (IsSearchPlaceholder())
                {
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = DS.Slate900;
                }
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = SearchPlaceholder;
                    _txtSearch.ForeColor = DS.Slate400;
                }
            };
            _txtSearch.TextChanged += (s, e) => ResetListPageAndRender();

            _cboListStatusFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9)
            };
            ConfigureDropDownListCombo(_cboListStatusFilter);
            _cboListStatusFilter.Items.AddRange(new object[] { "All", "Pending", "Partial", "Approved", "Closed", "Received", "Paid", "Overdue", "Due Soon" });
            _cboListStatusFilter.SelectedIndex = 0;
            _cboListStatusFilter.SelectedIndexChanged += (s, e) => ResetListPageAndRender();

            TableLayoutPanel filterGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 0),
                Margin = new Padding(0)
            };
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            filterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            filterGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _txtSearch.Margin = new Padding(0, 0, 8, 0);
            _cboListStatusFilter.Margin = new Padding(0);
            filterGrid.Controls.Add(_txtSearch, 0, 0);
            filterGrid.Controls.Add(_cboListStatusFilter, 1, 0);

            top.Controls.Add(filterGrid);
            top.Controls.Add(tabGrid);

            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _leftListFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            scroll.Resize += (s, e) =>
            {
                _leftListFlow.Width = GetLeftListContentWidth(268);
            };
            scroll.Controls.Add(_leftListFlow);

            _orderListPager = new GlobalPaginationControl { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.White };
            _orderListPager.PageChanged += (s, e) =>
            {
                _listPageIndex = Math.Max(0, _orderListPager.CurrentPage - 1);
                ApplyFiltersAndRender();
            };
            _orderListPager.PageSizeChanged += (s, e) =>
            {
                _orderListPageSize = _orderListPager.PageSize;
                _listPageIndex = 0;
                ApplyFiltersAndRender();
            };

            wrap.Controls.Add(scroll);
            wrap.Controls.Add(_orderListPager);
            wrap.Controls.Add(top);
            return wrap;
        }

        private Control BuildRightPanel()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = DS.BgPage
            };
            panel.HorizontalScroll.Enabled = false;
            panel.HorizontalScroll.Visible = false;

            Panel summary = CreateCardPanel();
            summary.Width = 276;
            summary.Height = 274;
            summary.Margin = new Padding(0, 0, 0, 14);
            summary.Controls.Add(new Label { Text = "Summary", Location = new Point(16, 16), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = DS.Slate900 });
            _lblSummaryItems = AddSummaryRow(summary, "Items", "0", 48, false);
            _lblSummarySubtotal = AddSummaryRow(summary, "Sub Total", IndiaFormatHelper.FormatCurrency(0), 78, false);
            _lblSummaryDiscount = AddSummaryRow(summary, "Discount", IndiaFormatHelper.FormatCurrency(0), 108, false);
            _lblSummaryTax = AddSummaryRow(summary, "Tax (GST 18%)", IndiaFormatHelper.FormatCurrency(0), 138, false);
            _lblSummaryCharges = AddSummaryRow(summary, "Other Charges", IndiaFormatHelper.FormatCurrency(0), 168, false);
            _lblSummaryTotal = AddSummaryRow(summary, "Total", IndiaFormatHelper.FormatCurrency(0), 204, true);
            _lblSummaryWords = new Label
            {
                Text = "Zero Rupees Only",
                Location = new Point(16, 236),
                Size = new Size(252, 32),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate600
            };
            summary.Controls.Add(_lblSummaryWords);
            panel.Controls.Add(summary);

            Panel actions = CreateCardPanel();
            actions.Width = 276;
            actions.Height = 172;
            actions.Margin = new Padding(0, 0, 0, 14);
            actions.Controls.Add(new Label { Text = "Actions", Location = new Point(16, 16), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = DS.Slate900 });
            Button save = MakePanelActionButton("Save PO", SaveGreen, 48);
            Button openActions = MakePanelActionButton("Open PO Actions", InfoBlue, 92);
            Label hint = new Label
            {
                Text = "More purchase operations live in this menu.",
                Location = new Point(16, 132),
                Size = new Size(244, 32),
                Font = new Font("Segoe UI", 7.8f),
                ForeColor = DS.Slate600
            };
            save.Click += (s, e) => Save();
            openActions.Click += (s, e) => ShowPurchaseOrderActionMenu(openActions);
            _toolTip.SetToolTip(openActions, "Open secondary purchase order actions.");
            actions.Controls.AddRange(new Control[] { save, openActions, hint });
            panel.Controls.Add(actions);
            panel.Resize += (s, e) =>
            {
                int cardWidth = Math.Max(248, panel.ClientSize.Width - 4);
                summary.Width = cardWidth;
                actions.Width = cardWidth;
                foreach (Button button in actions.Controls.OfType<Button>())
                    button.Width = Math.Max(216, cardWidth - 32);
                hint.Width = Math.Max(216, cardWidth - 32);
            };
            return panel;
        }

        private void ShowPurchaseOrderActionMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddPurchaseOrderAction(menu, "Convert to Bill", (s, e) => ConvertToBill());
            AddPurchaseOrderAction(menu, "Mark as Received", (s, e) => MarkReceived());
            AddPurchaseOrderAction(menu, "Send to Supplier Email", (s, e) => SendPurchaseOrder());
            AddPurchaseOrderAction(menu, "Print PO", (s, e) => SavePurchaseOrderPdf());
            AddPurchaseOrderAction(menu, "Clone PO", (s, e) => ClonePurchaseOrder());
            menu.Items.Add(new ToolStripSeparator());
            AddPurchaseOrderAction(menu, "Cancel PO", (s, e) => CancelPurchaseOrder());
            AddPurchaseOrderAction(menu, "Delete PO", (s, e) => DeletePurchaseOrder());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void AddPurchaseOrderAction(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            menu.Items.Add(item);
        }

        private Label AddSummaryRow(Control parent, string label, string value, int y, bool total)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(16, y), Size = new Size(120, 22), Font = new Font("Segoe UI", 8.5f, total ? FontStyle.Bold : FontStyle.Regular), ForeColor = DS.Slate900 });
            Label val = new Label { Text = value, Location = new Point(134, y), Size = new Size(134, 22), Font = new Font("Segoe UI", total ? 12f : 8.5f, FontStyle.Bold), ForeColor = total ? InfoBlue : DS.Slate900, TextAlign = ContentAlignment.MiddleRight };
            parent.Controls.Add(val);
            return val;
        }

        private Button MakePanelActionButton(string text, Color accent, int y)
        {
            Button button = MakeOutlineButton(text, 244);
            button.Location = new Point(16, y);
            UIHelper.ApplyActionButton(
                button,
                text.StartsWith("Cancel", StringComparison.OrdinalIgnoreCase)
                    ? UiActionVariant.Danger
                    : UIHelper.ResolveActionVariant(text));
            return button;
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("Purchases", this, _btnNewPo, _btnSavePo, null);
        }

        private void BuildDetailPanel()
        {
            BuildReferencePurchaseDetailPanel();
            if (DateTime.MinValue != DateTime.MaxValue)
                return;

            Panel workspace = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = DS.BgPage,
                Padding = new Padding(0)
            };
            _detail.Controls.Add(workspace);

            Panel fields = CreateCardPanel();
            fields.Width = 860;
            fields.Height = 1664;
            fields.Margin = new Padding(0, 0, 0, 12);
            fields.Padding = new Padding(18, 14, 18, 0);
            fields.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            fields.Resize += (s, e) =>
            {
                foreach (Control child in fields.Controls)
                {
                    if (child is TableLayoutPanel table)
                        table.Width = Math.Max(720, fields.Width - 36);
                    else if (child is Panel panel && panel.Tag as string == "StaticInfoPanel")
                    {
                        panel.Width = Math.Max(720, fields.Width - 36);
                        foreach (Control row in panel.Controls)
                            row.Width = Math.Max(700, panel.Width - 20);
                    }
                }
            };

            fields.Controls.Add(new Label
            {
                Text = "PO Header",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = InfoBlue,
                Location = new Point(0, 0),
                Width = 500,
                Height = 22,
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            });

            FlowLayoutPanel poTabs = new FlowLayoutPanel
            {
                Location = new Point(0, 28),
                Size = new Size(820, 30),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White,
                Visible = false
            };
            foreach (string tab in new[] { "PO Details", "Shipping", "Billing", "Other Details", "Attachments", "History" })
            {
                Label tabLabel = new Label
                {
                    Text = tab,
                    Tag = tab,
                    Width = tab == "Other Details" ? 104 : 86,
                    Height = 26,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    ForeColor = tab == "PO Details" ? InfoBlue : DS.Slate600,
                    BackColor = tab == "PO Details" ? Color.FromArgb(238, 242, 255) : Color.White,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0, 0, 8, 0),
                    Cursor = Cursors.Hand
                };
                tabLabel.Click += (s, e) => ActivatePoHeaderTab((string)((Label)s).Tag);
                _poHeaderTabLabels.Add(tabLabel);
                poTabs.Controls.Add(tabLabel);
            }
            fields.Controls.Add(poTabs);

            TableLayoutPanel headerGrid = new TableLayoutPanel
            {
                Location = new Point(0, 36),
                Size = new Size(820, 366),
                ColumnCount = 3,
                RowCount = 8,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 273));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 273));
            headerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 274));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            headerGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));

            _cboVendor = new ComboBox { Width = 150, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(_cboVendor);
            _cboVendor.SelectedIndexChanged += (s, e) => OnVendorChanged();
            _txtVendorGstin = new TextBox { Width = 150, Font = new Font("Segoe UI", 9), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = DS.Slate700, Tag = "CUSTOM_INPUT_SHELL" };
            _txtPONumber = new TextBox { Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            _txtVendorInvoiceNumber = new TextBox { Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            _dtpDate = new DateTimePicker { Width = 150, Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            _dtpDate.ValueChanged += (s, e) => RefreshPayByDate();
            _dtpPayByDate = new DateTimePicker { Width = 150, Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            _cboStatus = new ComboBox { Width = 150, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(_cboStatus);
            _cboStatus.Items.AddRange(new object[] { "Pending", "Approved", "Partial", "Received", "Fully Received", "Paid", "Closed", "Cancelled" });
            _cboStatus.SelectedIndex = 0;
            _cboLinkedType = new ComboBox { Width = 116, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(_cboLinkedType);
            _cboLinkedType.Items.AddRange(new object[] { "General", "Contract", "WorkOrder" });
            _cboLinkedType.SelectedIndexChanged += (s, e) =>
            {
                PopulateLinkedRecordCombo();
                UpdateBillingControls();
                RefreshDeliveryAddressPreview();
            };
            _cboLinkedRecord = new ComboBox { Width = 126, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(_cboLinkedRecord);
            _cboLinkedRecord.SelectedIndexChanged += (s, e) =>
            {
                UpdateBillingControls();
                RefreshDeliveryAddressPreview();
            };
            _txtNotes = new TextBox { Width = 676, Height = 54, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Multiline = true };
            TextBox txtVendorContact = new TextBox { Text = string.Empty, Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtPhone = new TextBox { Text = string.Empty, Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtEmail = new TextBox { Text = string.Empty, Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtCurrency = new TextBox { Text = "INR - Indian Rupee", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtPaymentTerms = new TextBox { Text = "30 Days", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtExchangeRate = new TextBox { Text = "1.0000", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtVendorAddress = new TextBox { Text = string.Empty, Width = 150, Height = 72, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtPurchaseType = new TextBox { Text = "General Purchase", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtPriority = new TextBox { Text = "Normal", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtDepartment = new TextBox { Text = "Purchase", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtProjectSite = new TextBox { Text = "Select project", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            TextBox txtCreatedBy = new TextBox { Text = "Administrator", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, BackColor = Color.White, ForeColor = DS.Slate700, Tag = "CUSTOM_INPUT_SHELL" };
            TextBox txtCreatedOn = new TextBox { Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, BackColor = Color.White, ForeColor = DS.Slate700, Tag = "CUSTOM_INPUT_SHELL" };

            headerGrid.Controls.Add(BuildFieldCell("PO Number *", _txtPONumber, 104, 150), 0, 0);
            headerGrid.Controls.Add(BuildFieldCell("PO Date *", _dtpDate, 104, 150), 1, 0);
            headerGrid.Controls.Add(BuildFieldCell("Supplier GSTIN", _txtVendorGstin, 104, 150), 2, 0);
            headerGrid.Controls.Add(BuildFieldCell("Supplier *", _cboVendor, 104, 150), 0, 1);
            headerGrid.Controls.Add(BuildFieldCell("Required By *", _dtpPayByDate, 104, 150), 1, 1);
            headerGrid.Controls.Add(BuildFieldCell("Supplier Invoice #", _txtVendorInvoiceNumber, 104, 150), 2, 1);
            headerGrid.Controls.Add(BuildFieldCell("Supplier Contact", txtVendorContact, 104, 150), 0, 2);
            headerGrid.Controls.Add(BuildFieldCell("Currency", txtCurrency, 104, 150), 1, 2);
            headerGrid.Controls.Add(BuildFieldCell("Payment Terms", txtPaymentTerms, 104, 150), 2, 2);
            headerGrid.Controls.Add(BuildFieldCell("Phone", txtPhone, 104, 150), 0, 3);
            headerGrid.Controls.Add(BuildFieldCell("Exchange Rate", txtExchangeRate, 104, 150), 1, 3);
            headerGrid.Controls.Add(BuildFieldCell("Credit Days", new TextBox { Text = "30", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle }, 104, 150), 2, 3);
            headerGrid.Controls.Add(BuildFieldCell("Email", txtEmail, 104, 150), 0, 4);
            headerGrid.Controls.Add(BuildLinkedToCell(), 1, 4);
            headerGrid.Controls.Add(BuildFieldCell("Status *", _cboStatus, 104, 150), 2, 4);
            headerGrid.Controls.Add(BuildFieldCell("Purchase Type *", txtPurchaseType, 104, 150), 0, 5);
            headerGrid.Controls.Add(BuildFieldCell("Priority", txtPriority, 104, 150), 1, 5);
            headerGrid.Controls.Add(BuildFieldCell("Department", txtDepartment, 104, 150), 2, 5);
            headerGrid.Controls.Add(BuildFieldCell("Project / Site", txtProjectSite, 104, 150), 0, 6);
            headerGrid.Controls.Add(BuildFieldCell("Created By", txtCreatedBy, 104, 150), 1, 6);
            headerGrid.Controls.Add(BuildFieldCell("Created On", txtCreatedOn, 104, 150), 2, 6);

            Panel notesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
            Label notesLabel = new Label { Text = "Notes", Width = 70, Location = new Point(0, 8), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            _txtNotes.Location = new Point(78, 4);
            _txtNotes.Width = 450;
            notesPanel.Controls.Add(notesLabel);
            notesPanel.Controls.Add(_txtNotes);
            headerGrid.Controls.Add(BuildFieldCell("Address", txtVendorAddress, 104, 150), 0, 7);
            headerGrid.Controls.Add(notesPanel, 1, 7);
            headerGrid.SetColumnSpan(notesPanel, 2);
            fields.Controls.Add(headerGrid);
            _poDetailsControls.Add(headerGrid);

            _lblPaymentMeta = new Label
            {
                Location = new Point(128, 406),
                Width = 680,
                Height = 24,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            fields.Controls.Add(_lblPaymentMeta);
            _poDetailsControls.Add(_lblPaymentMeta);

            Panel deliveryModePanel = new Panel { Location = new Point(0, 436), Size = new Size(840, 34) };
            Label deliveryModeLabel = new Label { Text = "Delivery Mode", Width = 120, Location = new Point(0, 8), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            _rbTechPickup = new RadioButton { Text = "Tech Pickup", Location = new Point(128, 7), Width = 110, Checked = true, Font = new Font("Segoe UI", 9) };
            _rbSiteDelivery = new RadioButton { Text = "Site Delivery", Location = new Point(246, 7), Width = 110, Font = new Font("Segoe UI", 9) };
            _rbTechPickup.CheckedChanged += (s, e) => { if (_rbTechPickup.Checked) RefreshDeliveryAddressPreview(); };
            _rbSiteDelivery.CheckedChanged += (s, e) => { if (_rbSiteDelivery.Checked) RefreshDeliveryAddressPreview(); };
            deliveryModePanel.Controls.AddRange(new Control[] { deliveryModeLabel, _rbTechPickup, _rbSiteDelivery });
            fields.Controls.Add(deliveryModePanel);
            _shippingControls.Add(deliveryModePanel);

            _deliveryAddressPanel = new Panel { Location = new Point(0, 470), Size = new Size(840, 48), Visible = false };
            Label deliveryAddressLabel = new Label { Text = "Delivery Address", Width = 120, Location = new Point(0, 12), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            _txtDeliveryAddress = new TextBox { Location = new Point(128, 8), Width = 676, Height = 34, Multiline = true, ReadOnly = true, BackColor = Color.White, ForeColor = DS.Slate700, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9), Tag = "CUSTOM_INPUT_SHELL" };
            _toolTip.SetToolTip(_txtDeliveryAddress, "Delivery address auto-filled from linked site");
            _deliveryAddressPanel.Controls.AddRange(new Control[] { deliveryAddressLabel, _txtDeliveryAddress });
            fields.Controls.Add(_deliveryAddressPanel);
            _shippingControls.Add(_deliveryAddressPanel);

            Panel technicianPanel = new Panel { Location = new Point(0, 522), Size = new Size(840, 34) };
            Label technicianLabel = new Label { Text = "Assigned Technician", Width = 120, Location = new Point(0, 8), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            _cboTechnician = new ComboBox { Location = new Point(128, 4), Width = 250, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(_cboTechnician);
            _cboTechnician.SelectedIndexChanged += (s, e) => ApplyTechnicianSelection();
            technicianPanel.Controls.AddRange(new Control[] { technicianLabel, _cboTechnician });
            fields.Controls.Add(technicianPanel);
            _shippingControls.Add(technicianPanel);

            _lblCreatedByMeta = new Label
            {
                Location = new Point(128, 560),
                Width = 676,
                Height = 24,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(71, 85, 105),
                Visible = false
            };
            fields.Controls.Add(_lblCreatedByMeta);
            _otherDetailsControls.Add(_lblCreatedByMeta);

            Panel billingPanel = new Panel { Location = new Point(0, 592), Size = new Size(840, 34) };
            Label billingLabel = new Label { Text = "Bill to Client", Width = 120, Location = new Point(0, 8), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            _chkAddToClientInvoice = new CheckBox { Text = "Add parts to client invoice when saved", Location = new Point(128, 7), Width = 320, Font = new Font("Segoe UI", 9) };
            _chkAddToClientInvoice.CheckedChanged += (s, e) =>
            {
                if (_chkAddToClientInvoice.Checked && !IsWorkOrderLinked())
                {
                    _toolTip.Show("Link this PO to a Work Order first to enable billing", _chkAddToClientInvoice, 2500);
                    _chkAddToClientInvoice.Checked = false;
                }
            };
            billingPanel.Controls.AddRange(new Control[] { billingLabel, _chkAddToClientInvoice });
            fields.Controls.Add(billingPanel);
            _billingControls.Add(billingPanel);

            Panel receiptPanel = new Panel { Location = new Point(0, 630), Size = new Size(840, 70) };
            Label receiptLabel = new Label { Text = "Attach packing slip", Width = 120, Location = new Point(0, 10), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight };
            _btnAttachReceipt = MakeBtn("Attach Photo/File", InfoBlue, 132);
            _btnAttachReceipt.Location = new Point(128, 6);
            _btnAttachReceipt.Click += (s, e) => AttachReceipt();
            _picReceipt = new PictureBox { Location = new Point(272, 4), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Visible = false };
            _lblReceiptFile = new Label { Location = new Point(346, 10), Width = 320, Height = 40, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(71, 85, 105) };
            _btnInlineViewReceipt = MakeBtn("View", Color.White, 62);
            _btnInlineViewReceipt.Location = new Point(676, 8);
            _btnInlineViewReceipt.ForeColor = Color.FromArgb(51, 65, 85);
            _btnInlineViewReceipt.FlatAppearance.BorderColor = DS.Border;
            _btnInlineViewReceipt.FlatAppearance.BorderSize = 1;
            _btnInlineViewReceipt.Click += (s, e) => ViewReceipt();
            _btnDeleteReceipt = MakeBtn("Delete", Color.FromArgb(254, 226, 226), 62);
            _btnDeleteReceipt.Location = new Point(744, 8);
            _btnDeleteReceipt.ForeColor = DelRed;
            _btnDeleteReceipt.FlatAppearance.BorderColor = DS.Border;
            _btnDeleteReceipt.FlatAppearance.BorderSize = 1;
            _btnDeleteReceipt.Click += (s, e) => DeleteReceiptReference();
            receiptPanel.Controls.AddRange(new Control[] { receiptLabel, _btnAttachReceipt, _picReceipt, _lblReceiptFile, _btnInlineViewReceipt, _btnDeleteReceipt });
            fields.Controls.Add(receiptPanel);
            _attachmentControls.Add(receiptPanel);

            Panel shippingExtraPanel = BuildTabInfoPanel("Expected Delivery Date", "04/06/2026", "Contact Person", "Site supervisor", "Site Delivery Notes", "Coordinate delivery window with assigned technician.");
            shippingExtraPanel.Location = new Point(0, 712);
            shippingExtraPanel.Visible = true;
            fields.Controls.Add(shippingExtraPanel);
            _shippingControls.Add(shippingExtraPanel);

            Panel billingExtraPanel = BuildTabInfoPanel("Bill To", "New Client", "GST Treatment", "Registered Business", "Linked invoice/client billing reference", "Client invoice reference will be created from Work Order billing.");
            billingExtraPanel.Location = new Point(0, 900);
            billingExtraPanel.Visible = true;
            fields.Controls.Add(billingExtraPanel);
            _billingControls.Add(billingExtraPanel);

            Panel otherExtraPanel = BuildTabInfoPanel("Warranty", "Standard supplier warranty", "Supplier quotation reference", "Supplier quote / RFQ reference pending", "Internal remarks", "Approval required only for high-value or variance-flagged purchases.");
            otherExtraPanel.Location = new Point(0, 1088);
            otherExtraPanel.Visible = true;
            fields.Controls.Add(otherExtraPanel);
            _otherDetailsControls.Add(otherExtraPanel);

            Panel attachmentExtraPanel = BuildTabInfoPanel("Supplier quote", "Attach and view supplier quotations", "Delivery challan", "Attach packing slip, challan, receipt, or site photo", "Attachment actions", "View, download, and delete use the receipt/document controls until a document backend is added.");
            attachmentExtraPanel.Location = new Point(0, 1276);
            attachmentExtraPanel.Visible = true;
            fields.Controls.Add(attachmentExtraPanel);
            _attachmentControls.Add(attachmentExtraPanel);

            Panel historyPanel = BuildTabInfoPanel("Audit trail", "No purchase order selected", "Attachment", "No attachment selected", "Status", "No status changes to show.");
            historyPanel.Location = new Point(0, 1464);
            historyPanel.Visible = true;
            fields.Controls.Add(historyPanel);
            _historyControls.Add(historyPanel);
            ActivatePoHeaderTab("PO Details");

            Panel gridPanel = CreateCardPanel();
            gridPanel.Width = 860;
            gridPanel.Height = 520;
            gridPanel.Margin = new Padding(0, 0, 0, 12);
            gridPanel.Padding = new Padding(14, 14, 14, 14);

            _lblVarianceWarning = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Text = "One or more items are priced above historical rates. Review highlighted rows.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(146, 64, 14),
                BackColor = Color.FromArgb(255, 247, 237),
                Padding = new Padding(8, 8, 0, 0),
                Visible = false
            };

            Panel lineActions = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White, Padding = new Padding(0, 4, 0, 8) };
            FlowLayoutPanel lineActionLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 470,
                Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            FlowLayoutPanel lineActionRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 268,
                Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            Button btnAddLine = MakeBtn("+ Add Item", InfoBlue, 108);
            Button btnAddRfq = MakeOutlineButton("Add from RFQ", 108);
            Button btnImportItems = MakeBtn("Import Items", InfoBlue, 118);
            Button btnClearLines = MakeBtn("Clear Items", DelRed, 104);
            Button btnDiscount = MakeOutlineButton("Apply Discount", 120);
            Button btnCharges = MakeOutlineButton("+ Add Charges", 110);
            foreach (Button button in new[] { btnAddLine, btnAddRfq, btnImportItems, btnClearLines, btnDiscount, btnCharges })
            {
                button.Height = 36;
                button.Margin = new Padding(0, 0, 10, 0);
            }
            btnCharges.Margin = new Padding(0);
            btnAddLine.Click += (s, e) => AddLineItemCard();
            btnAddRfq.Click += (s, e) => AddFromRfq();
            btnImportItems.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(btnImportItems, ExcelImportModule.Purchases, FindForm());
            btnClearLines.Click += (s, e) =>
            {
                if (MessageBox.Show("Clear all purchase line items?", "Clear Items", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _lineItemFlow.Controls.Clear();
                RecalcTotal();
            };
            btnDiscount.Click += (s, e) => ApplyDiscountToLines();
            btnCharges.Click += (s, e) => AddCharges();
            lineActionLeft.Controls.AddRange(new Control[] { btnAddLine, btnAddRfq, btnImportItems, btnClearLines });
            lineActionRight.Controls.AddRange(new Control[] { btnDiscount, btnCharges });
            lineActions.Resize += (s, e) =>
            {
                lineActionLeft.Width = Math.Max(440, lineActions.ClientSize.Width - lineActionRight.Width - 16);
                lineActionRight.Width = 268;
            };
            lineActions.Controls.Add(lineActionRight);
            lineActions.Controls.Add(lineActionLeft);

            _lineItemHeader = BuildLineItemHeader();
            _lineItemHeader.Dock = DockStyle.Top;

            Panel lineScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(0, 0, 0, 0) };
            lineScroll.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, lineScroll.Width - 1, lineScroll.Height - 1);
            };
            _lineItemFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0, 0, 0, 8)
            };
            _lblLineItemEmptyState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "No line items added yet. Use + Add Item to start this purchase order.",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White,
                Visible = true
            };
            lineScroll.Resize += (s, e) => ResizeLineItemRows();
            lineScroll.Controls.Add(_lblLineItemEmptyState);
            lineScroll.Controls.Add(_lineItemFlow);
            _lblLineItemEmptyState.BringToFront();

            Panel totalBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = Color.FromArgb(235, 245, 255)
            };
            _lblLineItemCount = new Label
            {
                Dock = DockStyle.Left,
                Width = 220,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate600,
                Text = "Showing 0 items",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                BackColor = Color.FromArgb(235, 245, 255)
            };
            _lblTotal = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = InfoBlue,
                Text = "Sub Total  Rs 0.00     Discount  Rs 0.00     GST  Rs 0.00     Other Charges  Rs 0.00     Total  Rs 0.00",
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.FromArgb(235, 245, 255),
                Padding = new Padding(0, 0, 12, 0)
            };
            totalBar.Controls.Add(_lblTotal);
            totalBar.Controls.Add(_lblLineItemCount);

            gridPanel.Controls.Add(totalBar);
            gridPanel.Controls.Add(lineScroll);
            gridPanel.Controls.Add(_lineItemHeader);
            gridPanel.Controls.Add(lineActions);
            gridPanel.Controls.Add(_lblVarianceWarning);

            Panel communication = CreateCardPanel();
            communication.Width = 860;
            communication.Height = 130;
            communication.Margin = new Padding(0, 0, 0, 18);
            communication.Controls.Add(new Label { Text = "Internal Communication", Location = new Point(16, 14), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = DS.Slate900 });
            TextBox txtMessage = new TextBox { Name = "txtInternalMessage", Location = new Point(16, 46), Width = 660, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            txtMessage.Text = "Type your message...";
            txtMessage.ForeColor = DS.Slate400;
            txtMessage.GotFocus += (s, e) =>
            {
                if (txtMessage.ForeColor == DS.Slate400)
                {
                    txtMessage.Text = string.Empty;
                    txtMessage.ForeColor = DS.Slate900;
                }
            };
            Button btnSendComment = MakeBtn("Send", InfoBlue, 76);
            btnSendComment.Location = new Point(690, 43);
            btnSendComment.Click += (s, e) => SendInternalMessage(txtMessage);
            communication.Controls.AddRange(new Control[] { txtMessage, btnSendComment });
            communication.Controls.Add(new Label { Text = "Administrator    Please ensure the delivery is done before the required-by date.", Location = new Point(16, 88), Width = 650, Height = 22, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700 });

            workspace.Controls.Add(fields);
            workspace.Controls.Add(gridPanel);
            workspace.Controls.Add(communication);
            _detail.Resize += (s, e) =>
            {
                int width = Math.Max(820, _detail.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
                foreach (Control control in workspace.Controls)
                    control.Width = width;
            };
        }

        private void BuildReferencePurchaseDetailPanel()
        {
            Panel workspace = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = DS.BgPage,
                Padding = new Padding(0)
            };
            _detail.Controls.Add(workspace);

            CreatePurchaseEditorControls();

            Panel guideCard = CreatePurchaseWorkflowGuide();
            Panel vendorCard = CreateReferenceSection("1", "PO Information", 860, 250);
            Button selectVendor = MakeOutlineButton("Select Supplier", 136);
            selectVendor.Location = new Point(700, 18);
            selectVendor.ForeColor = InfoBlue;
            selectVendor.Click += (s, e) => _cboVendor.Focus();
            vendorCard.Controls.Add(selectVendor);
            AddRefField(vendorCard, "PO Number", _txtPONumber, 18, 58, 255);
            AddRefField(vendorCard, "PO Date *", _dtpDate, 300, 58, 255);
            AddRefField(vendorCard, "Required By", _dtpPayByDate, 582, 58, 255);
            AddRefField(vendorCard, "Supplier *", _cboVendor, 18, 116, 255);
            AddRefField(vendorCard, "Supplier GSTIN", _txtVendorGstin, 300, 116, 255);
            AddRefField(vendorCard, "Supplier Invoice #", _txtVendorInvoiceNumber, 582, 116, 255);
            AddRefField(vendorCard, "Supplier Contact", CreateReadonlyTextBox("Select contact"), 18, 174, 255);
            AddRefField(vendorCard, "Phone", CreateReadonlyTextBox("Enter phone"), 300, 174, 255);
            AddRefField(vendorCard, "Email", CreateReadonlyTextBox("Enter email"), 582, 174, 255);

            Panel infoCard = CreateReferenceSection("2", "Order Information", 860, 210);
            AddRefField(infoCard, "Currency", CreateReadonlyTextBox("INR - Indian Rupee"), 18, 58, 195);
            AddRefField(infoCard, "Exchange Rate", CreateReadonlyTextBox("1.0000"), 235, 58, 120);
            AddRefField(infoCard, "Payment Terms", CreateReadonlyTextBox("30 Days"), 378, 58, 170);
            AddRefField(infoCard, "Credit Days", CreateReadonlyTextBox("30"), 570, 58, 145);
            AddRefField(infoCard, "Purchase Type", CreateReadonlyTextBox("General Purchase"), 732, 58, 105);
            AddRefField(infoCard, "Priority", CreateReadonlyTextBox("Normal"), 18, 116, 170);
            AddRefField(infoCard, "Linked To", _cboLinkedType, 210, 116, 118);
            AddRefField(infoCard, "", _cboLinkedRecord, 340, 116, 160);
            AddRefField(infoCard, "Status", _cboStatus, 522, 116, 150);
            AddRefField(infoCard, "Department", CreateReadonlyTextBox("Purchase"), 694, 116, 143);
            AddRefField(infoCard, "Project / Site (optional)", _cboProjectSite, 18, 166, 245);
            AddRefField(infoCard, "Created By", CreateReadonlyTextBox("Administrator"), 300, 166, 200);
            _lblCreatedByMeta = new Label { Location = new Point(522, 168), Size = new Size(315, 26), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700, Text = "Created On   " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") };
            infoCard.Controls.Add(_lblCreatedByMeta);

            Panel deliveryCard = CreateReferenceSection("3", "Delivery & Notes", 860, 220);
            Panel modePanel = new Panel { Location = new Point(18, 58), Size = new Size(250, 56), BackColor = Color.White };
            modePanel.Controls.Add(new Label { Text = "Delivery Mode", Location = new Point(0, 0), Size = new Size(150, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = DS.Slate700 });
            _rbTechPickup.Location = new Point(0, 24);
            _rbSiteDelivery.Location = new Point(118, 24);
            modePanel.Controls.Add(_rbTechPickup);
            modePanel.Controls.Add(_rbSiteDelivery);
            deliveryCard.Controls.Add(modePanel);
            AddRefField(deliveryCard, "Assigned Technician", _cboTechnician, 300, 58, 220);
            AddRefField(deliveryCard, "Expected Delivery Date", CreateReadonlyTextBox(DateTime.Today.AddDays(15).ToString("dd/MM/yyyy")), 18, 142, 170);
            AddRefField(deliveryCard, "Address", _txtDeliveryAddress, 218, 142, 300);
            AddRefField(deliveryCard, "Notes", _txtNotes, 540, 142, 300);
            _lblPaymentMeta = new Label
            {
                Location = new Point(18, 116),
                Size = new Size(812, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate600,
                Text = "Outstanding: ₹0.00   |   In credit window   |   No payment ref yet"
            };
            deliveryCard.Controls.Add(_lblPaymentMeta);

            _picReceipt = new PictureBox { Visible = false };
            _lblReceiptFile = new Label { Visible = false };
            _btnAttachReceipt = MakeOutlineButton("Attach Photo/File", 132);
            _btnInlineViewReceipt = MakeOutlineButton("View", 62);
            _btnDeleteReceipt = MakeOutlineButton("Delete", 62);
            _btnAttachReceipt.Click += (s, e) => AttachReceipt();
            _btnInlineViewReceipt.Click += (s, e) => ViewReceipt();
            _btnDeleteReceipt.Click += (s, e) => DeleteReceiptReference();

            Panel itemsCard = CreateCardPanel();
            itemsCard.Width = 960;
            itemsCard.Height = 386;
            itemsCard.Margin = new Padding(0, 0, 0, 12);
            itemsCard.Padding = new Padding(14);
            itemsCard.Controls.Add(MakeStepHeader("4", "Items", new Point(18, 16)));
            Panel itemActions = new Panel { Location = new Point(18, 48), Size = new Size(924, 42), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            FlowLayoutPanel primaryItemActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 390,
                Height = 38,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            Button btnAddLine = MakeBtn("+  Add Item", InfoBlue, 106);
            Button btnAddRfq = MakeOutlineButton("Add from RFQ", 122);
            Button btnImportItems = MakeOutlineButton("Import Items", 116);
            Button btnDiscount = MakeOutlineButton("Apply Discount", 122);
            foreach (Button actionButton in new[] { btnAddLine, btnAddRfq, btnImportItems })
            {
                actionButton.Height = 34;
                actionButton.Margin = new Padding(0, 2, 10, 0);
            }
            btnDiscount.Height = 34;
            btnDiscount.Location = new Point(itemActions.Width - btnDiscount.Width, 2);
            btnDiscount.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddLine.Click += (s, e) => AddLineItemCard();
            btnAddRfq.Click += (s, e) => AddFromRfq();
            btnImportItems.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(btnImportItems, ExcelImportModule.Purchases, FindForm());
            btnDiscount.Click += (s, e) => ApplyDiscountToLines();
            primaryItemActions.Controls.AddRange(new Control[] { btnAddLine, btnAddRfq, btnImportItems });
            itemActions.Controls.Add(btnDiscount);
            itemActions.Controls.Add(primaryItemActions);
            itemActions.Resize += (s, e) =>
            {
                primaryItemActions.Width = Math.Max(360, itemActions.ClientSize.Width - btnDiscount.Width - 24);
                btnDiscount.Left = Math.Max(primaryItemActions.Right + 12, itemActions.ClientSize.Width - btnDiscount.Width);
            };
            itemsCard.Controls.Add(itemActions);
            itemActions.BringToFront();
            _lblVarianceWarning = new Label { Visible = false };
            _lineItemHeader = BuildLineItemHeader();
            _lineItemHeader.Location = new Point(14, 96);
            _lineItemHeader.Width = 940;
            Panel lineHost = new Panel { Location = new Point(14, 128), Size = new Size(940, 174), BackColor = Color.White, AutoScroll = true };
            lineHost.HorizontalScroll.Enabled = false;
            lineHost.HorizontalScroll.Visible = false;
            lineHost.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, lineHost.Width - 1, lineHost.Height - 1);
            };
            _lineItemFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Padding = new Padding(0) };
            _lblLineItemEmptyState = new Label { Dock = DockStyle.Fill, Text = ModernIconSystem.Icon(ModernIconKind.Inventory, 16, DS.Slate500).Text + "  No line items added yet. Add items, import Excel, or save the draft after selecting a vendor.", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.White };
            lineHost.Controls.Add(_lblLineItemEmptyState);
            lineHost.Controls.Add(_lineItemFlow);
            _lblLineItemEmptyState.BringToFront();
            Panel totalBar = new Panel { Location = new Point(14, 310), Size = new Size(940, 42), BackColor = Color.FromArgb(248, 250, 252) };
            _lblLineItemCount = new Label { Text = "Showing 1 item", Location = new Point(12, 10), Size = new Size(160, 22), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate600 };
            _lblTotal = new Label { Text = "Sub Total:  ₹0.00        Discount:  ₹0.00        GST:  ₹0.00        Other Charges:  ₹0.00        Total:  ₹0.00", Location = new Point(180, 10), Size = new Size(640, 22), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = InfoBlue, TextAlign = ContentAlignment.MiddleRight };
            totalBar.Controls.Add(_lblLineItemCount);
            totalBar.Controls.Add(_lblTotal);
            itemsCard.Controls.Add(totalBar);
            itemsCard.Controls.Add(lineHost);
            itemsCard.Controls.Add(_lineItemHeader);

            workspace.Controls.Add(guideCard);
            workspace.Controls.Add(vendorCard);
            workspace.Controls.Add(infoCard);
            workspace.Controls.Add(deliveryCard);
            workspace.Controls.Add(itemsCard);

            EventHandler resizePurchaseWorkspace = (s, e) =>
            {
                int width = Math.Max(960, _detail.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
                workspace.Width = width;
                int sectionY = 0;
                foreach (Control control in workspace.Controls)
                {
                    control.Width = width;
                    control.Location = new Point(0, sectionY);
                    sectionY += control.Height + 12;
                }
                workspace.Height = sectionY;
                if (itemsCard.Width > 120)
                {
                    itemActions.Width = Math.Max(500, itemsCard.Width - 36);
                    primaryItemActions.Width = Math.Max(360, itemActions.ClientSize.Width - btnDiscount.Width - 24);
                    btnDiscount.Left = Math.Max(primaryItemActions.Right + 12, itemActions.ClientSize.Width - btnDiscount.Width);
                    _lineItemHeader.Width = Math.Max(940, itemsCard.Width - 28);
                    lineHost.Width = _lineItemHeader.Width;
                    totalBar.Width = _lineItemHeader.Width;
                    _lblTotal.Width = Math.Max(420, totalBar.Width - 190);
                }
            };
            _detail.Resize += resizePurchaseWorkspace;
            resizePurchaseWorkspace(_detail, EventArgs.Empty);
            UIHelper.ApplyInputStyles(workspace.Controls);
            ForcePurchaseOrderInputBackColors(workspace);
        }

        private Panel CreatePurchaseWorkflowGuide()
        {
            Panel guide = CreateCardPanel();
            guide.Width = 860;
            guide.Height = 88;
            guide.Margin = new Padding(0, 0, 0, 12);
            guide.Padding = new Padding(18, 14, 18, 12);

            Label title = new Label
            {
                Text = "Purchase flow",
                Location = new Point(18, 12),
                Size = new Size(130, 24),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            guide.Controls.Add(title);

            TableLayoutPanel steps = new TableLayoutPanel { Location = new Point(160, 12), Size = new Size(670, 58), ColumnCount = 4, RowCount = 1, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            for (int i = 0; i < 4; i++)
                steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            steps.Controls.Add(BuildPurchaseGuideStep("1. Supplier", "Supplier is required; GST and contact fill after selection.", InfoBlue), 0, 0);
            steps.Controls.Add(BuildPurchaseGuideStep("2. Link", "Contract, work order, project/site are optional.", SaveGreen), 1, 0);
            steps.Controls.Add(BuildPurchaseGuideStep("3. Delivery", "Choose technician pickup or site delivery.", WarnOrange), 2, 0);
            steps.Controls.Add(BuildPurchaseGuideStep("4. Items", "Add/import line items before approval or receipt.", DelRed), 3, 0);
            guide.Controls.Add(steps);
            guide.Resize += (s, e) => steps.Width = Math.Max(520, guide.ClientSize.Width - steps.Left - 20);
            return guide;
        }

        private Panel BuildPurchaseGuideStep(string title, string subtitle, Color accent)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(8, 5, 8, 4) };
            DS.Rounded(panel, 7);
            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 18, Font = DS.SmallBold, ForeColor = accent };
            Label subtitleLabel = new Label { Text = subtitle, Dock = DockStyle.Fill, Font = DS.Caption, ForeColor = DS.Slate600 };
            panel.Controls.Add(subtitleLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private void CreatePurchaseEditorControls()
        {
            _cboVendor = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboVendor.SelectedIndexChanged += (s, e) => OnVendorChanged();
            _txtVendorGstin = CreateReadonlyTextBox("Enter GSTIN");
            _txtPONumber = CreateInputTextBox("Enter PO number");
            _txtVendorInvoiceNumber = CreateInputTextBox("Enter invoice number");
            _dtpDate = new DateTimePicker { Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today };
            _dtpDate.ValueChanged += (s, e) => RefreshPayByDate();
            _dtpPayByDate = new DateTimePicker { Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddDays(9) };
            _cboStatus = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboStatus.Items.AddRange(new object[] { "Pending", "Approved", "Partial", "Received", "Fully Received", "Paid", "Closed", "Cancelled" });
            _cboStatus.SelectedIndex = 0;
            _chkAddToClientInvoice = new CheckBox { Visible = false };
            _txtNotes = CreateInputTextBox("Enter notes");
            _txtNotes.Multiline = true;
            _txtNotes.Height = 42;
            _rbTechPickup = new RadioButton { Text = "Tech Pickup", Width = 104, Font = new Font("Segoe UI", 8.5f), Checked = true };
            _rbSiteDelivery = new RadioButton { Text = "Site Delivery", Width = 110, Font = new Font("Segoe UI", 8.5f) };
            _rbTechPickup.CheckedChanged += (s, e) => { if (_rbTechPickup.Checked) RefreshDeliveryAddressPreview(); };
            _rbSiteDelivery.CheckedChanged += (s, e) => { if (_rbSiteDelivery.Checked) RefreshDeliveryAddressPreview(); };
            _txtDeliveryAddress = CreateInputTextBox("Enter delivery address");
            _txtDeliveryAddress.Multiline = true;
            _txtDeliveryAddress.Height = 42;
            _deliveryAddressPanel = new Panel { Visible = false };
            _cboTechnician = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboTechnician.SelectedIndexChanged += (s, e) => ApplyTechnicianSelection();
            _cboLinkedType = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboLinkedType.Items.AddRange(new object[] { "General", "Contract", "WorkOrder" });
            _cboLinkedType.SelectedIndexChanged += (s, e) =>
            {
                PopulateLinkedRecordCombo();
                UpdateBillingControls();
                RefreshDeliveryAddressPreview();
            };
            _cboLinkedRecord = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboLinkedRecord.SelectedIndexChanged += (s, e) =>
            {
                UpdateBillingControls();
                RefreshDeliveryAddressPreview();
            };
            _cboProjectSite = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboProjectSite.SelectedIndexChanged += (s, e) => RefreshDeliveryAddressPreview();
            _cboLinkedType.SelectedIndex = 0;
        }

        private Panel CreateReferenceSection(string number, string title, int width, int height)
        {
            Panel card = CreateCardPanel();
            card.Width = width;
            card.Height = height;
            card.Margin = new Padding(0, 0, 0, 0);
            card.Padding = new Padding(14);
            card.Controls.Add(MakeStepHeader(number, title, new Point(18, 18)));
            return card;
        }

        private Control MakeStepHeader(string number, string title, Point location)
        {
            Panel panel = new Panel { Location = location, Size = new Size(300, 24), BackColor = Color.White };
            Label badge = new Label { Text = number, Location = new Point(0, 1), Size = new Size(20, 20), BackColor = DS.Slate200, ForeColor = DS.Slate700, Font = new Font("Segoe UI", 8, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(badge, 10);
            panel.Controls.Add(badge);
            panel.Controls.Add(new Label { Text = title, Location = new Point(28, 1), Size = new Size(250, 20), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleLeft });
            return panel;
        }

        private void AddRefField(Control parent, string label, Control input, int x, int y, int width)
        {
            if (!string.IsNullOrWhiteSpace(label))
                parent.Controls.Add(new Label { Text = label, Location = new Point(x, y), Size = new Size(width, 18), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = DS.Slate700 });
            input.Location = new Point(x, y + 20);
            input.Width = width;
            if (input.Height < 30)
                input.Height = 30;
            parent.Controls.Add(input);
        }

        private TextBox CreateInputTextBox(string text)
        {
            return new TextBox { Text = text, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = DS.Slate700 };
        }

        private TextBox CreateReadonlyTextBox(string text)
        {
            return new TextBox { Text = text, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = DS.Slate700, ReadOnly = true, Tag = "CUSTOM_INPUT_SHELL" };
        }

        private static void ForcePurchaseOrderInputBackColors(Control root)
        {
            if (root == null)
                return;

            foreach (Control control in root.Controls)
            {
                TextBox textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.BackColor = Color.White;
                    textBox.ForeColor = DS.Slate700;
                }

                ComboBox comboBox = control as ComboBox;
                if (comboBox != null)
                {
                    comboBox.BackColor = Color.White;
                    comboBox.ForeColor = DS.Slate900;
                }

                NumericUpDown numeric = control as NumericUpDown;
                if (numeric != null)
                {
                    numeric.BackColor = Color.White;
                    numeric.ForeColor = DS.Slate900;
                }

                if (control.HasChildren)
                    ForcePurchaseOrderInputBackColors(control);
            }
        }

        private Panel BuildTabInfoPanel(string label1, string value1, string label2, string value2, string label3, string value3)
        {
            Panel panel = new Panel { Location = new Point(0, 462), Size = new Size(840, 180), BackColor = Color.White, Visible = false, Tag = "StaticInfoPanel" };
            panel.Controls.Add(BuildStaticInfo(label1, value1, 0));
            panel.Controls.Add(BuildStaticInfo(label2, value2, 54));
            panel.Controls.Add(BuildStaticInfo(label3, value3, 108));
            panel.Resize += (s, e) =>
            {
                foreach (Control child in panel.Controls)
                    if (child is Panel row)
                        LayoutStaticInfoRow(row);
            };
            return panel;
        }

        private Control BuildStaticInfo(string label, string value, int y)
        {
            Panel row = new Panel { Location = new Point(0, y), Size = new Size(820, 48), BackColor = Color.White };
            row.Controls.Add(new Label { Name = "lblStaticInfo", Text = label, Location = new Point(0, 8), Width = 128, Height = 20, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleRight });
            row.Controls.Add(new TextBox { Name = "txtStaticInfo", Text = value, Location = new Point(136, 4), Width = 640, Height = 28, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle });
            row.Resize += (s, e) => LayoutStaticInfoRow(row);
            LayoutStaticInfoRow(row);
            return row;
        }

        private static void LayoutStaticInfoRow(Panel row)
        {
            Label label = row.Controls["lblStaticInfo"] as Label;
            TextBox textBox = row.Controls["txtStaticInfo"] as TextBox;
            if (label == null || textBox == null)
                return;

            int labelWidth = Math.Min(150, Math.Max(96, row.ClientSize.Width / 5));
            label.Location = new Point(0, 8);
            label.Width = labelWidth;
            textBox.Location = new Point(label.Right + 10, 4);
            textBox.Width = Math.Max(220, row.ClientSize.Width - textBox.Left - 10);
        }

        private void ActivatePoHeaderTab(string tab)
        {
            _activePoHeaderTab = string.IsNullOrWhiteSpace(tab) ? "PO Header" : tab;
            foreach (Label label in _poHeaderTabLabels)
            {
                bool active = string.Equals(label.Tag as string, _activePoHeaderTab, StringComparison.OrdinalIgnoreCase);
                label.ForeColor = active ? InfoBlue : DS.Slate600;
                label.BackColor = active ? Color.FromArgb(238, 242, 255) : Color.White;
            }

            SetHeaderTabVisibility(_poDetailsControls, true);
            SetHeaderTabVisibility(_shippingControls, true);
            SetHeaderTabVisibility(_billingControls, true);
            SetHeaderTabVisibility(_otherDetailsControls, true);
            SetHeaderTabVisibility(_attachmentControls, true);
            SetHeaderTabVisibility(_historyControls, true);
            if (_deliveryAddressPanel != null)
                _deliveryAddressPanel.Visible = _rbSiteDelivery != null && _rbSiteDelivery.Checked;
        }

        private static void SetHeaderTabVisibility(IEnumerable<Control> controls, bool visible)
        {
            foreach (Control control in controls)
                control.Visible = visible;
        }

        private async Task LoadInitialDataAsync()
        {
            SetStatus("Loading purchase orders...", Color.Gray);
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan ttl = TimeSpan.FromMinutes(2);

            var inventoryTask = Task.Run(() => AppDataCache.GetOrCreate("inventory:all", ttl, () => _invSvc.GetAll() ?? new List<StockItem>()).ToList());
            var vendorTask = Task.Run(() => AppDataCache.GetOrCreate("vendors:suppliers", ttl, () => _vndSvc.GetSuppliers() ?? new List<Vendor>()).ToList());
            var contractTask = Task.Run(() => AppDataCache.GetOrCreate("contracts:all", ttl, () => _cntSvc.GetAllContracts() ?? new List<AMCContract>()).ToList());
            var jobTask = Task.Run(() => AppDataCache.GetOrCreate("jobs:all", ttl, () => _jobSvc.GetAll() ?? new List<Job>()).ToList());
            var siteTask = Task.Run(() => AppDataCache.GetOrCreate("sites:all", ttl, () => _siteSvc.GetAll() ?? new List<ClientSite>()).ToList());
            var technicianTask = Task.Run(() => AppDataCache.GetOrCreate("employees:technicians-active", ttl, () => _employeeSvc.GetActiveTechnicians() ?? new List<Employee>()).ToList());
            var hsnTask = Task.Run(() => AppDataCache.GetOrCreate("hsnsac:all", ttl, () => _hsnSvc.GetAll() ?? new List<HsnSacMasterEntry>()).ToList());
            var orderTask = Task.Run(() => AppDataCache.GetOrCreate("purchases:fresh", ttl, () => _svc.GetAllFresh() ?? new List<PurchaseOrder>()).ToList());

            _vendors = await vendorTask ?? new List<Vendor>();
            PopulateVendorCombo();

            _technicians = await technicianTask ?? new List<Employee>();
            PopulateTechnicianCombo();

            await Task.WhenAll(inventoryTask, contractTask, jobTask, siteTask, hsnTask, orderTask);

            _inventoryItems = inventoryTask.Result ?? new List<StockItem>();
            _contracts = contractTask.Result ?? new List<AMCContract>();
            _jobs = jobTask.Result ?? new List<Job>();
            _sites = siteTask.Result ?? new List<ClientSite>();
            _hsnEntries = hsnTask.Result ?? new List<HsnSacMasterEntry>();
            _orderSource = orderTask.Result ?? new List<PurchaseOrder>();

            if (_cboLinkedType != null && _cboLinkedType.Items.Count > 0)
                _cboLinkedType.SelectedIndex = 0;
            PopulateLinkedRecordCombo();
            PopulateProjectSiteCombo();
            NewRecord();
            ApplyFiltersAndRender();
            if (_orderSource.Count > 0)
                PopulateDetail(_orderSource[0]);
            ApplyNavigationRequest();
            AppRuntime.LogTiming("Purchases.InitialLoad", sw.ElapsedMilliseconds, "orders=" + _orderSource.Count);
        }

        private void PopulateVendorCombo()
        {
            if (_cboVendor == null)
                return;

            _cboVendor.BeginUpdate();
            _cboVendor.Items.Clear();
            foreach (Vendor vendor in _vendors)
                _cboVendor.Items.Add(vendor);
            UIHelper.ShowEmptyVendorsMessageIfNeeded(FindForm(), _vendors, "PurchaseForm.PopulateVendorCombo");
            if (_cboVendor.Items.Count > 0)
                _cboVendor.SelectedIndex = 0;
            _cboVendor.EndUpdate();
        }

        private void PopulateTechnicianCombo()
        {
            if (_cboTechnician == null)
                return;

            _cboTechnician.Items.Clear();
            _cboTechnician.Items.Add(new ComboItem<int?>(null, "Unassigned"));
            foreach (Employee employee in _technicians ?? new List<Employee>())
                _cboTechnician.Items.Add(new ComboItem<int?>(employee.EmployeeID, employee.Name));
            _cboTechnician.SelectedIndex = 0;
        }

        private void PopulateLinkedRecordCombo()
        {
            if (_cboLinkedType == null || _cboLinkedRecord == null)
                return;

            string linkedType = _cboLinkedType.SelectedItem?.ToString() ?? "General";
            _cboLinkedRecord.Items.Clear();

            if (string.Equals(linkedType, "Contract", StringComparison.OrdinalIgnoreCase))
            {
                foreach (AMCContract contract in _contracts)
                    _cboLinkedRecord.Items.Add(new ComboItem<int?>(contract.ContractID, "Contract #" + contract.ContractID + "  |  Client " + contract.ClientID));
            }
            else if (string.Equals(linkedType, "WorkOrder", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Job job in _jobs)
                    _cboLinkedRecord.Items.Add(new ComboItem<int?>(job.JobID, string.IsNullOrWhiteSpace(job.JobNumber) ? "Job #" + job.JobID : job.JobNumber));
            }
            else
            {
                _cboLinkedRecord.Items.Add(new ComboItem<int?>(null, "General purchase"));
            }

            if (_cboLinkedRecord.Items.Count > 0)
                _cboLinkedRecord.SelectedIndex = 0;
        }

        private void PopulateProjectSiteCombo()
        {
            if (_cboProjectSite == null)
                return;

            int selectedSiteId = GetSelectedProjectSiteId();
            _cboProjectSite.BeginUpdate();
            _cboProjectSite.Items.Clear();
            _cboProjectSite.Items.Add(new ComboItem<int?>(null, "No project / site selected"));
            foreach (ClientSite site in _sites ?? new List<ClientSite>())
            {
                string displayName = SiteService.GetDisplayName(site);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = "Site #" + site.SiteID;
                if (!string.IsNullOrWhiteSpace(site.City))
                    displayName += " - " + site.City.Trim();
                _cboProjectSite.Items.Add(new ComboItem<int?>(site.SiteID, displayName));
            }
            _cboProjectSite.EndUpdate();
            SelectProjectSite(selectedSiteId);
        }

        private int GetSelectedProjectSiteId()
        {
            if (_cboProjectSite?.SelectedItem is ComboItem<int?> item && item.Value.HasValue)
                return item.Value.Value;
            return 0;
        }

        private void SelectProjectSite(int siteId)
        {
            if (_cboProjectSite == null || _cboProjectSite.Items.Count == 0)
                return;

            for (int i = 0; i < _cboProjectSite.Items.Count; i++)
            {
                if (_cboProjectSite.Items[i] is ComboItem<int?> item && (item.Value ?? 0) == siteId)
                {
                    _cboProjectSite.SelectedIndex = i;
                    return;
                }
            }
            _cboProjectSite.SelectedIndex = 0;
        }

        private ClientSite GetSelectedProjectSite()
        {
            int siteId = GetSelectedProjectSiteId();
            return siteId > 0 ? _sites.FirstOrDefault(site => site.SiteID == siteId) : null;
        }

        private async void LoadList()
        {
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                SetStatus("Refreshing purchase orders...", Color.Gray);
                _orderSource = await Task.Run(() => _svc.GetAllFresh());
                ApplyFiltersAndRender();
                SetStatus("Purchase list refreshed.", Color.Gray);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Loading purchase details", ex);
                SetStatus("Purchase details could not load. Refresh and try again.", Color.Red);
            }

            AppRuntime.LogTiming("Purchases.LoadList", sw.ElapsedMilliseconds, "orders=" + _orderSource.Count);
        }

        private void ApplyFiltersAndRender()
        {
            UpdateTabButtons();
            UiPerformanceService.WithSuspendedDrawing(_leftListFlow, () =>
            {
                _leftListFlow.Controls.Clear();
                _payableChecks.Clear();

                List<PurchaseOrder> filteredOrders = FilterOrders();
                if (_viewMode == PurchaseViewMode.VendorPayables)
                    RenderVendorPayables(filteredOrders);
                else
                    RenderOrderCards(filteredOrders);
            });
            UpdateBatchPayVisibility();
        }

        private void ResetListPageAndRender()
        {
            _listPageIndex = 0;
            ApplyFiltersAndRender();
        }

        private void ChangeListPage(int delta)
        {
            _listPageIndex = Math.Max(0, _listPageIndex + delta);
            ApplyFiltersAndRender();
        }

        private List<PurchaseOrder> FilterOrders()
        {
            IEnumerable<PurchaseOrder> query = _orderSource ?? Enumerable.Empty<PurchaseOrder>();

            if (_activeTab == PurchaseListTab.PendingOrders)
                query = query.Where(p => string.Equals(p.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.PODate);
            else if (_activeTab == PurchaseListTab.PartialOrders)
                query = query.Where(p => string.Equals(p.Status, "Partial", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.PODate);
            else if (_activeTab == PurchaseListTab.ApprovedOrders)
                query = query.Where(p => NormalizePoStatus(p.Status) == "Approved")
                    .OrderByDescending(p => p.PODate);
            else if (_activeTab == PurchaseListTab.ClosedOrders)
                query = query.Where(p => IsClosedPo(p))
                    .OrderByDescending(p => p.PODate);
            else
                query = query.OrderByDescending(p => p.PODate);

            string search = IsSearchPlaceholder() ? string.Empty : (_txtSearch.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                query = query.Where(p =>
                    (!string.IsNullOrWhiteSpace(p.VendorName) && p.VendorName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(p.PONumber) && p.PONumber.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(p.LinkedToLabel) && p.LinkedToLabel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(p.LinkedToType) && p.LinkedToType.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(p.SiteName) && p.SiteName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(p.ClientName) && p.ClientName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(p.Status) && p.Status.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (p.LineItems != null && p.LineItems.Any(li =>
                        (!string.IsNullOrWhiteSpace(li.Description) && li.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrWhiteSpace(li.JobLink) && li.JobLink.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))));
            }

            string status = _cboListStatusFilter.SelectedItem?.ToString() ?? "All";
            switch (status)
            {
                case "Pending":
                case "Partial":
                case "Approved":
                case "Closed":
                    query = query.Where(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase));
                    break;
                case "Received":
                    query = query.Where(p => NormalizePoStatus(p.Status) == "Fully Received");
                    break;
                case "Paid":
                    query = query.Where(p => GetPaymentStatus(p) == "Paid");
                    break;
                case "Overdue":
                    query = query.Where(p => p.IsOverdue);
                    break;
                case "Due Soon":
                    query = query.Where(p => p.BalanceDue > 0.01m && !p.IsOverdue && (p.PayByDate.Date - DateTime.Today).Days <= 7);
                    break;
            }

            return query.ToList();
        }

        private bool IsSearchPlaceholder()
        {
            return _txtSearch == null ||
                   string.Equals((_txtSearch.Text ?? string.Empty).Trim(), SearchPlaceholder, StringComparison.Ordinal);
        }

        private void RenderOrderCards(List<PurchaseOrder> orders)
        {
            orders = orders ?? new List<PurchaseOrder>();
            int pageSize = Math.Max(1, _orderListPageSize);
            int currentPage = PaginationState.NormalizePage(_listPageIndex + 1, orders.Count, pageSize);
            _listPageIndex = currentPage - 1;
            List<PurchaseOrder> visibleOrders = orders
                .Skip(_listPageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            foreach (PurchaseOrder po in visibleOrders.Take(MaxRenderedOrderCards))
                _leftListFlow.Controls.Add(MakeOrderCard(po));

            if (orders.Count == 0)
                _leftListFlow.Controls.Add(MakeEmptyState("No purchases match this filter."));
            else if (orders.Count > MaxRenderedOrderCards)
                _leftListFlow.Controls.Add(MakeEmptyState("Showing first " + MaxRenderedOrderCards + " of " + orders.Count + ". Use search/status filters to narrow the list."));

            SetStatus("Showing " + orders.Count + " purchase orders.", Color.Gray);
            ResizeLeftListFlowToContent();
            if (_orderListPager != null)
                _orderListPager.SetState(_listPageIndex + 1, orders.Count, pageSize);
        }

        private void ResizeLeftListFlowToContent()
        {
            if (_leftListFlow == null)
                return;

            int contentHeight = _leftListFlow.Controls
                .Cast<Control>()
                .Sum(control => control.Height + control.Margin.Vertical);
            int viewportHeight = _leftListFlow.Parent?.ClientSize.Height ?? 0;
            _leftListFlow.Height = Math.Max(viewportHeight, contentHeight);
        }

        private void RenderVendorPayables(List<PurchaseOrder> orders)
        {
            List<VendorPayableGroup> payables = orders
                .Where(p => p.BalanceDue > 0.01m)
                .GroupBy(p => new { p.VendorID, VendorName = string.IsNullOrWhiteSpace(p.VendorName) ? "Unknown Supplier" : p.VendorName })
                .Select(g => new VendorPayableGroup
                {
                    VendorID = g.Key.VendorID,
                    VendorName = g.Key.VendorName,
                    TotalOutstanding = g.Sum(x => x.BalanceDue),
                    OverdueCount = g.Count(x => x.IsOverdue),
                    Purchases = g.OrderBy(x => x.PayByDate).ThenBy(x => x.PODate).ToList()
                })
                .OrderByDescending(g => g.OverdueCount)
                .ThenBy(g => g.Purchases.FirstOrDefault()?.PayByDate ?? DateTime.MaxValue)
                .ThenBy(g => g.VendorName)
                .ToList();

            foreach (VendorPayableGroup group in payables)
                _leftListFlow.Controls.Add(MakeVendorPayableCard(group));

            if (payables.Count == 0)
                _leftListFlow.Controls.Add(MakeEmptyState("No vendor payables match this filter."));

            ResizeLeftListFlowToContent();
            SetStatus("Showing " + payables.Count + " vendor payable groups.", Color.Gray);
        }

        private Control MakeEmptyState(string text)
        {
            Panel empty = new Panel
            {
                Width = GetLeftListContentWidth(268),
                Height = 120,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 0, 0, 10)
            };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Purchase, 42, Color.FromArgb(238, 242, 255), InfoBlue);
            icon.Location = new Point((empty.Width - icon.Width) / 2, 16);
            Label title = new Label
            {
                Text = text.IndexOf("payables", StringComparison.OrdinalIgnoreCase) >= 0 ? "No payables found" : "No purchases found",
                Location = new Point(16, 66),
                Size = new Size(empty.Width - 32, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent
            };
            Label helper = new Label
            {
                Text = "Adjust filters or create a PO.",
                Location = new Point(16, 88),
                Size = new Size(empty.Width - 32, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.Transparent
            };
            empty.Controls.Add(icon);
            empty.Controls.Add(title);
            empty.Controls.Add(helper);
            empty.Resize += (s, e) =>
            {
                icon.Left = (empty.Width - icon.Width) / 2;
                title.Width = empty.Width - 32;
                helper.Width = empty.Width - 32;
            };
            return empty;
        }

        private Panel MakeOrderCard(PurchaseOrder po)
        {
            Color accent = GetDueColor(po);
            int cardWidth = GetLeftListContentWidth(268);
            Panel card = new Panel
            {
                Width = cardWidth,
                Height = 104,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Tag = po.POID
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(card == _selectedCard ? InfoBlue : DS.Slate200, card == _selectedCard ? 2 : 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            AddClickableControl(card, new Label { Text = po.PONumber, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(12, 12), Width = Math.Max(150, cardWidth - 104) }, po.POID);
            AddClickableControl(card, MakeStatusBadge(po.Status ?? "Pending", accent, new Point(cardWidth - 76, 12), 62), po.POID);
            AddClickableControl(card, new Label { Text = po.VendorName ?? "-", Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate700, Location = new Point(12, 34), Width = Math.Max(178, cardWidth - 92) }, po.POID);
            if (po.PriceVarianceFlag)
                AddClickableControl(card, new Label { Text = "!", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(180, 83, 9), BackColor = Color.FromArgb(255, 237, 213), Location = new Point(Math.Max(166, cardWidth - 102), 34), Width = 20, Height = 20, TextAlign = ContentAlignment.MiddleCenter }, po.POID);
            AddClickableControl(card, new Label { Text = "PO Date: " + po.PODate.ToString("dd MMM yyyy"), Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate600, Location = new Point(12, 58), Width = 118 }, po.POID);
            AddClickableControl(card, new Label { Text = "Age: " + po.AgeDays + "d", Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate600, Location = new Point(136, 58), Width = 70 }, po.POID);
            AddClickableControl(card, new Label { Text = IndiaFormatHelper.FormatCurrency(po.TotalAmount > 0 ? po.TotalAmount : po.BalanceDue), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(cardWidth - 112, 76), Width = 98, TextAlign = ContentAlignment.TopRight }, po.POID);
            AddClickableControl(card, MakeStatusBadge(string.IsNullOrWhiteSpace(po.LinkedToLabel) ? "General" : po.LinkedToLabel, DS.Slate500, new Point(12, 78), 58), po.POID);
            card.Click += (s, e) => SelectOrderCard(card, po.POID);
            return card;
        }

        private int GetLeftListContentWidth(int fallback)
        {
            int width = fallback;
            if (_leftListFlow != null && _leftListFlow.Parent != null)
                width = _leftListFlow.Parent.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
            else if (_leftRail != null)
                width = _leftRail.ClientSize.Width - _leftRail.Padding.Horizontal - 18;
            return Math.Max(fallback, width);
        }

        private Panel MakeVendorPayableCard(VendorPayableGroup group)
        {
            bool expanded = _expandedVendors.ContainsKey(group.VendorID) && _expandedVendors[group.VendorID];
            Panel card = new Panel { Width = 372, AutoSize = true, BackColor = Color.White, Margin = new Padding(0, 0, 0, 10) };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            Panel header = new Panel { Width = 372, Height = 66, BackColor = Color.White, Cursor = Cursors.Hand };
            header.Controls.Add(new Label { Text = expanded ? "-" : "+", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = InfoBlue, Location = new Point(10, 18), Width = 18 });
            header.Controls.Add(new Label { Text = group.VendorName, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(32, 12), Width = 220 });
            header.Controls.Add(new Label { Text = group.OverdueCount > 0 ? group.OverdueCount + " overdue" : "Within terms", Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = group.OverdueCount > 0 ? PendingRed : PendingGreen, Location = new Point(32, 36), Width = 110 });
            header.Controls.Add(new Label { Text = "Rs " + group.TotalOutstanding.ToString("N2"), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(218, 18), Width = 136, TextAlign = ContentAlignment.TopRight });
            header.Click += (s, e) => { _expandedVendors[group.VendorID] = !expanded; ApplyFiltersAndRender(); };
            foreach (Control ctl in header.Controls)
                ctl.Click += (s, e) => { _expandedVendors[group.VendorID] = !expanded; ApplyFiltersAndRender(); };
            card.Controls.Add(header);

            if (expanded)
            {
                FlowLayoutPanel children = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    Location = new Point(0, 66),
                    Width = 372,
                    BackColor = Color.FromArgb(248, 250, 252),
                    Padding = new Padding(8, 0, 8, 8)
                };

                foreach (PurchaseOrder po in group.Purchases)
                    children.Controls.Add(MakeVendorPayableRow(po));
                card.Controls.Add(children);
            }

            return card;
        }

        private Control MakeVendorPayableRow(PurchaseOrder po)
        {
            Color accent = GetDueColor(po);
            Panel row = new Panel { Width = 352, Height = 62, BackColor = Color.White, Margin = new Padding(0, 8, 0, 0), Cursor = Cursors.Hand };
            row.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(row == _selectedCard ? InfoBlue : DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
            };

            CheckBox chk = new CheckBox { Location = new Point(10, 22), Width = 18, Tag = po.POID };
            chk.CheckedChanged += (s, e) => UpdateBatchPayVisibility();
            _payableChecks[po.POID] = chk;
            row.Controls.Add(chk);
            row.Controls.Add(new Label { Text = po.PONumber, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(36, 10), Width = 120 });
            row.Controls.Add(new Label { Text = "Age " + po.AgeDays + "d", Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(36, 32), Width = 60 });
            row.Controls.Add(new Label { Text = po.PayByDate.ToString("dd MMM"), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = accent, Location = new Point(104, 32), Width = 64 });
            row.Controls.Add(new Label { Text = po.BalanceDue.ToString("₹#,##0.00"), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(214, 18), Width = 126, TextAlign = ContentAlignment.TopRight });

            EventHandler select = (s, e) => SelectOrderCard(row, po.POID);
            row.Click += select;
            foreach (Control ctl in row.Controls.Cast<Control>().Where(c => !(c is CheckBox)))
                ctl.Click += select;

            return row;
        }

        private void AddClickableControl(Panel card, Control control, int poId)
        {
            control.Click += (s, e) => SelectOrderCard(card, poId);
            card.Controls.Add(control);
        }

        private async void SelectOrderCard(Panel card, int poId)
        {
            if (_selectedCard != null)
                _selectedCard.Invalidate();
            _selectedCard = card;
            _selectedCard.Invalidate();
            SetStatus("Loading purchase order...", Color.Gray);
            try
            {
                _current = poId <= 0
                    ? (_orderSource ?? new List<PurchaseOrder>()).FirstOrDefault(p => p.POID == poId)
                    : await Task.Run(() => _svc.GetById(poId));
                PopulateDetail(_current);
                SetStatus("Purchase order loaded.", Color.Gray);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Loading purchase history", ex);
                SetStatus("Purchase history could not load. Refresh and try again.", Color.Red);
            }
        }

        private void PopulateDetail(PurchaseOrder po)
        {
            if (po == null)
                return;

            bool vendorSelected = false;
            for (int i = 0; i < _cboVendor.Items.Count; i++)
            {
                if (((Vendor)_cboVendor.Items[i]).VendorID == po.VendorID)
                {
                    _cboVendor.SelectedIndex = i;
                    vendorSelected = true;
                    break;
                }
            }
            if (!vendorSelected && po.POID < 0)
            {
                Vendor fallbackVendor = new Vendor
                {
                    VendorID = 0,
                    VendorName = string.IsNullOrWhiteSpace(po.VendorName) ? "New Supplier" : po.VendorName,
                    GSTNumber = string.IsNullOrWhiteSpace(po.VendorGSTIN) ? string.Empty : po.VendorGSTIN,
                    DefaultCreditDays = 30,
                    Phone = string.Empty,
                    Email = string.Empty,
                    Address = string.Empty
                };
                _cboVendor.Items.Add(fallbackVendor);
                _cboVendor.SelectedItem = fallbackVendor;
            }

            _txtPONumber.Text = po.PONumber ?? string.Empty;
            if (_lblBreadcrumb != null)
                _lblBreadcrumb.Text = "Purchase > Purchase Orders > " + (po.PONumber ?? "New PO");
            if (_lblHeaderStatus != null)
            {
                _lblHeaderStatus.Text = string.IsNullOrWhiteSpace(po.Status) ? "Draft" : po.Status;
                _lblHeaderStatus.ForeColor = GetStatusColor(po.Status);
                LayoutHeaderStatusBadge();
            }
            _dtpDate.Value = po.PODate == default ? DateTime.Today : po.PODate;
            _dtpPayByDate.Value = _svc.AutoSuggestPayByDate(_dtpDate.Value.Date, po.VendorID, po.PayByDate);
            _txtVendorInvoiceNumber.Text = po.VendorInvoiceNumber ?? string.Empty;
            _txtNotes.Text = po.Notes ?? string.Empty;
            _rbSiteDelivery.Checked = string.Equals(po.DeliveryMode, "SiteDelivery", StringComparison.OrdinalIgnoreCase);
            _rbTechPickup.Checked = !_rbSiteDelivery.Checked;
            _txtDeliveryAddress.Text = po.DeliveryAddress ?? string.Empty;
            _deliveryAddressPanel.Visible = _rbSiteDelivery.Checked;
            SelectTechnician(po.AssignedTechnicianId);
            _chkAddToClientInvoice.Checked = po.AddToClientInvoice;
            UpdateReceiptPreview(po.ReceiptImagePath);
            UpdateCreatedByDisplay(po);

            int statusIndex = _cboStatus.Items.IndexOf(po.Status);
            _cboStatus.SelectedIndex = statusIndex >= 0 ? statusIndex : 0;

            SelectComboByText(_cboLinkedType, string.IsNullOrWhiteSpace(po.LinkedToType) ? "General" : po.LinkedToType);
            PopulateLinkedRecordCombo();
            SelectLinkedRecord(po.LinkedToId);
            SelectProjectSite(po.SiteID > 0 ? po.SiteID : ResolveLinkedSiteId());

            _lineItemFlow.Controls.Clear();
            _otherCharges = 0m;
            foreach (PurchaseLineItem li in po.LineItems ?? new List<PurchaseLineItem>())
                AddLineItemCard(li);

            RecalcTotal();
            UpdatePaymentMeta(po);
            UpdateBillingControls();
            _lblVarianceWarning.Visible = po.PriceVarianceFlag;
            ApplyToolbarState();
        }

        private void SelectLinkedRecord(int? linkedId)
        {
            if (_cboLinkedRecord.Items.Count == 0)
                return;
            if (!linkedId.HasValue)
            {
                _cboLinkedRecord.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < _cboLinkedRecord.Items.Count; i++)
            {
                if (_cboLinkedRecord.Items[i] is ComboItem<int?> item && item.Value == linkedId)
                {
                    _cboLinkedRecord.SelectedIndex = i;
                    return;
                }
            }
        }

        private bool IsWorkOrderLinked()
        {
            return string.Equals(_cboLinkedType.SelectedItem?.ToString(), "WorkOrder", StringComparison.OrdinalIgnoreCase)
                && GetSelectedLinkedId().HasValue;
        }

        private int ResolveLinkedSiteId()
        {
            int? linkedId = GetSelectedLinkedId();
            if (!linkedId.HasValue)
                return GetSelectedProjectSiteId();

            string linkedType = _cboLinkedType.SelectedItem?.ToString() ?? "General";
            if (string.Equals(linkedType, "WorkOrder", StringComparison.OrdinalIgnoreCase))
                return _jobs.FirstOrDefault(job => job.JobID == linkedId.Value)?.SiteID ?? 0;
            if (string.Equals(linkedType, "Contract", StringComparison.OrdinalIgnoreCase))
                return _contracts.FirstOrDefault(contract => contract.ContractID == linkedId.Value)?.SiteID ?? 0;
            return GetSelectedProjectSiteId();
        }

        private void RefreshDeliveryAddressPreview()
        {
            if (_txtDeliveryAddress == null || _deliveryAddressPanel == null)
                return;

            string mode = _rbSiteDelivery != null && _rbSiteDelivery.Checked ? "SiteDelivery" : "TechPickup";
            int siteId = ResolveLinkedSiteId();
            PurchaseOrder updated = _svc.OnDeliveryModeChanged(_current?.POID ?? 0, mode, siteId);
            _txtDeliveryAddress.Text = updated?.DeliveryAddress ?? string.Empty;
            _deliveryAddressPanel.Visible = string.Equals(mode, "SiteDelivery", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyTechnicianSelection()
        {
            if (!(_cboTechnician.SelectedItem is ComboItem<int?> item))
                return;

            PurchaseOrder updated = _svc.OnTechnicianAssigned(_current?.POID ?? 0, item.Value ?? 0);
            if (_current != null)
            {
                _current.AssignedTechnicianId = updated.AssignedTechnicianId;
                _current.AssignedTechnicianName = updated.AssignedTechnicianName;
            }
            ApplyToolbarState();
        }

        private void UpdateBillingControls()
        {
            if (_chkAddToClientInvoice == null)
                return;

            bool canBill = IsWorkOrderLinked();
            if (!canBill && _chkAddToClientInvoice.Checked)
                _chkAddToClientInvoice.Checked = false;
            _chkAddToClientInvoice.Enabled = canBill;
            _toolTip.SetToolTip(_chkAddToClientInvoice, canBill ? string.Empty : "Link this PO to a Work Order first to enable billing");
        }

        private void UpdateCreatedByDisplay(PurchaseOrder po)
        {
            if (po != null && !string.IsNullOrWhiteSpace(po.CreatedByName) && po.CreatedByDate.HasValue)
            {
                _lblCreatedByMeta.Text = "Created by " + po.CreatedByName + " on " + po.CreatedByDate.Value.ToString("dd/MM/yyyy HH:mm");
                _lblCreatedByMeta.Visible = true;
            }
            else
            {
                _lblCreatedByMeta.Text = string.Empty;
                _lblCreatedByMeta.Visible = false;
            }
        }

        private void UpdateReceiptPreview(string path)
        {
            _receiptImagePath = string.IsNullOrWhiteSpace(path) ? null : path;
            _picReceipt.Image = null;
            _picReceipt.Visible = !string.IsNullOrWhiteSpace(_receiptImagePath);
            _lblReceiptFile.Text = string.IsNullOrWhiteSpace(_receiptImagePath) ? "No receipt attached." : Path.GetFileName(_receiptImagePath);
            _btnInlineViewReceipt.Enabled = !string.IsNullOrWhiteSpace(_receiptImagePath);
            if (_btnDeleteReceipt != null)
                _btnDeleteReceipt.Enabled = !string.IsNullOrWhiteSpace(_receiptImagePath);
            _btnViewReceipt.Enabled = !string.IsNullOrWhiteSpace(_receiptImagePath);

            if (string.IsNullOrWhiteSpace(_receiptImagePath) || !File.Exists(_receiptImagePath))
                return;

            string extension = Path.GetExtension(_receiptImagePath).ToLowerInvariant();
            if (extension == ".pdf")
            {
                _picReceipt.Image = SystemIcons.Application.ToBitmap();
                return;
            }

            using (Image image = Image.FromFile(_receiptImagePath))
                _picReceipt.Image = new Bitmap(image);
        }

        private void AttachReceipt()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "Receipt Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.pdf";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                string poNumber = string.IsNullOrWhiteSpace(_txtPONumber.Text) ? "PO-" + DateTime.Now.ToString("yyyyMMdd-HHmm") : _txtPONumber.Text.Trim();
                string targetDir = Path.Combine(@"C:\HVAC_PRO_MSE\RECEIPTS", SanitizeFolderName(poNumber));
                Directory.CreateDirectory(targetDir);
                string targetPath = Path.Combine(targetDir, Path.GetFileName(dlg.FileName));
                File.Copy(dlg.FileName, targetPath, true);
                UpdateReceiptPreview(targetPath);
                SetStatus("Receipt attached.", SaveGreen);
            }
        }

        private void ViewReceipt()
        {
            if (string.IsNullOrWhiteSpace(_receiptImagePath))
            {
                SetStatus("No receipt is attached.", WarnOrange);
                return;
            }
            if (!File.Exists(_receiptImagePath))
            {
                MessageBox.Show("Receipt file not found at " + _receiptImagePath, "Receipt Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(_receiptImagePath) { UseShellExecute = true });
        }

        private void DeleteReceiptReference()
        {
            if (string.IsNullOrWhiteSpace(_receiptImagePath))
            {
                SetStatus("No attachment is selected.", WarnOrange);
                return;
            }
            if (MessageBox.Show("Remove this attachment from the purchase order?", "Delete Attachment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            UpdateReceiptPreview(null);
            SetStatus("Attachment removed from this PO. Save to persist the change.", SaveGreen);
        }

        private void ApplyToolbarState()
        {
            bool canConvert = _current != null
                && (_current.IsPaymentCompleted || _current.Status == "Paid" || _current.Status == "Received")
                && IsWorkOrderLinked();
            _btnConvertToBill.Enabled = canConvert;
            _btnViewReceipt.Enabled = !string.IsNullOrWhiteSpace(_receiptImagePath);
        }

        private string ResolveSelectedWorkOrderName()
        {
            int? linkedId = GetSelectedLinkedId();
            if (!linkedId.HasValue || !string.Equals(_cboLinkedType.SelectedItem?.ToString(), "WorkOrder", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            Job job = _jobs.FirstOrDefault(x => x.JobID == linkedId.Value);
            if (job == null)
                return "Job #" + linkedId.Value;
            return string.IsNullOrWhiteSpace(job.JobNumber) ? "Job #" + job.JobID : job.JobNumber;
        }

        private static string SanitizeFolderName(string value)
        {
            foreach (char ch in Path.GetInvalidFileNameChars())
                value = value.Replace(ch, '-');
            return value;
        }

        private void UpdatePaymentMeta(PurchaseOrder po)
        {
            if (po == null)
            {
                _lblPaymentMeta.Text = string.Empty;
                return;
            }

            string dueState = po.IsOverdue ? "Overdue" : (po.BalanceDue > 0.01m && (po.PayByDate.Date - DateTime.Today).Days <= 7 ? "Due soon" : "In credit window");
            string refText = string.IsNullOrWhiteSpace(po.PaymentReference) ? "No payment ref yet" : "Payment ref: " + po.PaymentReference;
            _lblPaymentMeta.Text = "Outstanding: " + po.BalanceDue.ToString("₹#,##0.00") + "  |  " + dueState + "  |  " + refText;
        }

        private void SelectTechnician(int? technicianId)
        {
            if (_cboTechnician.Items.Count == 0)
                return;
            if (!technicianId.HasValue)
            {
                _cboTechnician.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < _cboTechnician.Items.Count; i++)
            {
                if (_cboTechnician.Items[i] is ComboItem<int?> item && item.Value == technicianId)
                {
                    _cboTechnician.SelectedIndex = i;
                    return;
                }
            }
            _cboTechnician.SelectedIndex = 0;
        }

        private void ApplyLinkedContext(PurchaseOrder po)
        {
            if (po == null)
                return;

            if (string.Equals(po.LinkedToType, "WorkOrder", StringComparison.OrdinalIgnoreCase) && po.LinkedToId.HasValue)
            {
                Job job = _jobs.FirstOrDefault(x => x.JobID == po.LinkedToId.Value);
                if (job != null)
                {
                    po.ClientID = job.ClientID;
                    po.SiteID = job.SiteID;
                    po.ClientName = job.ClientName;
                    po.SiteName = job.SiteName;
                }
            }
            else if (string.Equals(po.LinkedToType, "Contract", StringComparison.OrdinalIgnoreCase) && po.LinkedToId.HasValue)
            {
                AMCContract contract = _contracts.FirstOrDefault(x => x.ContractID == po.LinkedToId.Value);
                if (contract != null)
                {
                    po.ClientID = contract.ClientID;
                    po.SiteID = contract.SiteID;
                    po.RelatedContractID = contract.ContractID;
                    ClientSite contractSite = _sites.FirstOrDefault(site => site.SiteID == contract.SiteID);
                    po.SiteName = SiteService.GetDisplayName(contractSite);
                }
            }

            if (po.SiteID <= 0)
            {
                ClientSite selectedSite = GetSelectedProjectSite();
                if (selectedSite != null)
                {
                    po.ClientID = selectedSite.ClientID;
                    po.SiteID = selectedSite.SiteID;
                    po.SiteName = SiteService.GetDisplayName(selectedSite);
                }
            }
        }

        private void NewRecord()
        {
            _current = null;
            if (_cboVendor.Items.Count > 0)
                _cboVendor.SelectedIndex = 0;
            _txtPONumber.Text = string.Empty;
            if (_lblBreadcrumb != null)
                _lblBreadcrumb.Text = "Purchase > Purchase Orders > New PO";
            if (_lblHeaderStatus != null)
            {
                _lblHeaderStatus.Text = "Draft";
                _lblHeaderStatus.ForeColor = SaveGreen;
                LayoutHeaderStatusBadge();
            }
            _dtpDate.Value = DateTime.Today;
            RefreshPayByDate();
            _txtVendorInvoiceNumber.Text = string.Empty;
            _cboStatus.SelectedIndex = 0;
            _cboLinkedType.SelectedItem = "General";
            PopulateLinkedRecordCombo();
            SelectProjectSite(0);
            _rbTechPickup.Checked = true;
            _rbSiteDelivery.Checked = false;
            _txtDeliveryAddress.Text = string.Empty;
            _deliveryAddressPanel.Visible = _rbSiteDelivery.Checked;
            _chkAddToClientInvoice.Checked = false;
            SelectTechnician(null);
            _txtNotes.Text = string.Empty;
            UpdateReceiptPreview(null);
            _lineItemFlow.Controls.Clear();
            _otherCharges = 0m;
            AddLineItemCard();
            _selectedCard = null;
            PurchaseOrder createdByDraft = new PurchaseOrder
            {
                CreatedByName = SessionManager.CurrentUser?.DisplayName,
                CreatedByDate = DateTime.Now
            };
            UpdateCreatedByDisplay(createdByDraft);
            UpdatePaymentMeta(new PurchaseOrder { PODate = DateTime.Today, PayByDate = _dtpPayByDate.Value, TotalAmount = 0m, PaidAmount = 0m });
            _lblVarianceWarning.Visible = false;
            UpdateBillingControls();
            ApplyToolbarState();
            SetStatus("New purchase order ready.", Color.Gray);
        }

        private void Save()
        {
            if (_cboVendor.SelectedItem == null || ((_cboVendor.SelectedItem as Vendor)?.VendorID ?? 0) <= 0 || string.IsNullOrWhiteSpace(_txtPONumber.Text))
            {
                SetStatus("Supplier and PO number are required. Demo fallback suppliers cannot be saved.", Color.Red);
                return;
            }

            try
            {
                Vendor vendor = (Vendor)_cboVendor.SelectedItem;
                PurchaseOrder po = BuildPurchaseOrderFromForm(vendor);
                PendingChargeResult chargeResult = null;

                if (_current == null || _current.POID == 0)
                {
                    po.POID = _svc.Create(po);
                    _svc.SetCreatedBy(po.POID);
                    if (po.AddToClientInvoice)
                        chargeResult = _svc.CreatePendingCharge(po.POID);
                    _current = _svc.GetById(po.POID);
                }
                else
                {
                    po.POID = _current.POID;
                    _svc.Update(po);
                    if (po.AddToClientInvoice)
                        chargeResult = _svc.CreatePendingCharge(po.POID);
                    _current = _svc.GetById(po.POID);
                }

                LoadList();
                SetStatus(chargeResult != null && !string.IsNullOrWhiteSpace(chargeResult.Message) ? chargeResult.Message : "Purchase order saved.", SaveGreen);
                if (_current != null)
                    PopulateDetail(_current);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Saving purchase order", ex);
                SetStatus("Purchase order could not be saved. Check the form and try again.", Color.Red);
            }
        }

        private PurchaseOrder BuildPurchaseOrderFromForm(Vendor vendor)
        {
            PurchaseOrder po = new PurchaseOrder
            {
                VendorID = vendor.VendorID,
                VendorName = vendor.VendorName,
                VendorGSTIN = vendor.GSTNumber,
                PONumber = _txtPONumber.Text.Trim(),
                PODate = _dtpDate.Value.Date,
                PayByDate = _dtpPayByDate.Value.Date,
                VendorInvoiceNumber = _txtVendorInvoiceNumber.Text.Trim(),
                Status = _cboStatus.SelectedItem?.ToString() ?? "Pending",
                Notes = _txtNotes.Text.Trim(),
                DeliveryMode = _rbSiteDelivery.Checked ? "SiteDelivery" : "TechPickup",
                AssignedTechnicianId = (_cboTechnician.SelectedItem as ComboItem<int?>)?.Value,
                AssignedTechnicianName = (_cboTechnician.SelectedItem as ComboItem<int?>)?.Value.HasValue == true ? _cboTechnician.Text : null,
                DeliveryAddress = _rbSiteDelivery.Checked ? _txtDeliveryAddress.Text.Trim() : null,
                AddToClientInvoice = _chkAddToClientInvoice.Checked,
                PendingChargeCreated = _current != null && _current.PendingChargeCreated,
                ReceiptImagePath = _receiptImagePath,
                CreatedByUserId = _current?.CreatedByUserId ?? SessionManager.CurrentUser?.UserId,
                CreatedByName = _current?.CreatedByName ?? SessionManager.CurrentUser?.DisplayName,
                CreatedByDate = _current?.CreatedByDate ?? DateTime.Now
            };

            string linkedType = _cboLinkedType.SelectedItem?.ToString() ?? "General";
            po.LinkedToType = linkedType;
            if (string.Equals(linkedType, "Contract", StringComparison.OrdinalIgnoreCase))
                po.RelatedContractID = GetSelectedLinkedId();
            po.LinkedToId = GetSelectedLinkedId();
            ApplyLinkedContext(po);

            decimal total = 0m;
            bool hasVariance = false;
            foreach (Control row in _lineItemFlow.Controls)
            {
                ComboBox descCombo = row.Controls["cmbDesc"] as ComboBox;
                string desc = descCombo?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(desc))
                    continue;
                decimal qty = (row.Controls["numQty"] as NumericUpDown)?.Value ?? 0m;
                decimal rate = (row.Controls["numRate"] as NumericUpDown)?.Value ?? 0m;
                decimal discount = (row.Controls["numDiscount"] as NumericUpDown)?.Value ?? 0m;
                decimal gstRate = (row.Controls["numGst"] as NumericUpDown)?.Value ?? 0m;
                decimal gross = Math.Round(qty * rate, 2);
                decimal taxable = Math.Round(gross - (gross * discount / 100m), 2);
                decimal gstAmt = Math.Round(taxable * (gstRate / 100m), 2);
                decimal amt = taxable + gstAmt;
                PurchaseLineItem line = new PurchaseLineItem
                {
                    InventoryItemId = row.Controls["cmbDesc"]?.Tag is int itemId ? (int?)itemId : null,
                    Description = desc,
                    HsnSacCode = (row.Controls["cmbHsn"] as ComboBox)?.Text?.Trim(),
                    Quantity = qty,
                    UOM = (row.Controls["cmbUom"] as ComboBox)?.Text ?? "Nos",
                    Rate = rate,
                    GSTRate = gstRate,
                    CGSTRate = 0m,
                    SGSTRate = 0m,
                    IGSTRate = gstRate,
                    JobLink = (row.Controls["cmbJobLink"] as ComboBox)?.Text == "This Job" ? "Job" : "General",
                    LinkedWorkOrderId = IsWorkOrderLinked() && (row.Controls["cmbJobLink"] as ComboBox)?.Text == "This Job" ? GetSelectedLinkedId() : null,
                    LinkedWorkOrderName = IsWorkOrderLinked() && (row.Controls["cmbJobLink"] as ComboBox)?.Text == "This Job" ? ResolveSelectedWorkOrderName() : null,
                    Amount = amt
                };
                hasVariance |= _svc.CheckLineItemPriceVariance(line);
                po.LineItems.Add(line);
                total += amt;
            }

            po.TotalAmount = total + _otherCharges;
            po.PriceVarianceFlag = hasVariance;
            if (_current != null)
            {
                po.PaidAmount = _current.PaidAmount;
                po.PaymentReference = _current.PaymentReference;
                po.PendingChargeCreated = _current.PendingChargeCreated;
                if (po.PaidAmount >= po.TotalAmount && po.TotalAmount > 0)
                    po.Status = "Paid";
                else if (po.PaidAmount > 0 && po.PaidAmount < po.TotalAmount)
                    po.Status = "Partial";
            }

            return po;
        }

        private int? GetSelectedLinkedId()
        {
            if (_cboLinkedRecord.SelectedItem is ComboItem<int?> item)
                return item.Value;
            return null;
        }

        private void MarkReceived()
        {
            if (_current == null)
            {
                SetStatus("Select a PO to mark as received.", Color.Gray);
                return;
            }

            try
            {
                _svc.MarkReceived(_current.POID);
                foreach (PurchaseLineItem li in _current.LineItems)
                {
                    if (string.IsNullOrWhiteSpace(li.Description))
                        continue;
                    StockItem stockItem = _invSvc.GetByName(li.Description);
                    if (stockItem != null)
                        _invSvc.AddStock(stockItem.ItemID, li.Quantity);
                }

                _current = _svc.GetById(_current.POID);
                PopulateDetail(_current);
                LoadList();
                PendingChargeResult chargeResult = _current.AddToClientInvoice ? _svc.CreatePendingCharge(_current.POID) : null;
                StringBuilder confirmation = new StringBuilder();
                confirmation.AppendLine("PO marked received.");
                if (chargeResult != null && chargeResult.Created)
                    confirmation.AppendLine("Parts added to " + (chargeResult.WorkOrderName ?? "linked work order") + " invoice queue.");
                if (!string.IsNullOrWhiteSpace(_current.AssignedTechnicianName))
                    confirmation.AppendLine(_current.AssignedTechnicianName + " assigned - parts at job site.");
                MessageBox.Show(confirmation.ToString().Trim(), "Purchase Order Received", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("PO received. Stock updated.", SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Updating purchase totals", ex);
                SetStatus("Purchase totals could not be updated. Review the line items and try again.", Color.Red);
            }
        }

        private void ToggleVendorPayablesView()
        {
            _viewMode = _viewMode == PurchaseViewMode.Orders ? PurchaseViewMode.VendorPayables : PurchaseViewMode.Orders;
            _btnPayablesToggle.Text = _viewMode == PurchaseViewMode.VendorPayables ? "Back to Orders" : "Supplier Payables";
            ApplyFiltersAndRender();
        }

        private void BatchPaySelected()
        {
            List<int> selectedIds = _payableChecks.Where(kvp => kvp.Value.Checked).Select(kvp => kvp.Key).ToList();
            if (selectedIds.Count == 0)
            {
                SetStatus("Select at least one payable to batch pay.", Color.Gray);
                return;
            }

                using (Form prompt = ServoModalForm.Create("Batch Pay", 380, 150))
                {
                Label lbl = new Label { Text = "Payment reference", Location = new Point(16, 20), Width = 120 };
                TextBox txtRef = new TextBox { Location = new Point(16, 48), Width = 340, BorderStyle = BorderStyle.FixedSingle };
                Button btnOk = MakeBtn("Confirm", SaveGreen, 96);
                Button btnCancel = MakeBtn("Cancel", DelRed, 96);
                btnOk.Location = new Point(156, 92);
                btnCancel.Location = new Point(260, 92);
                btnOk.DialogResult = DialogResult.OK;
                btnCancel.DialogResult = DialogResult.Cancel;

                prompt.Controls.AddRange(new Control[] { lbl, txtRef, btnOk, btnCancel });
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return;

                string paymentReference = txtRef.Text.Trim();
                if (string.IsNullOrWhiteSpace(paymentReference))
                {
                    SetStatus("Payment reference is required for batch pay.", Color.Red);
                    return;
                }

                try
                {
                    _paymentSvc.BatchPayPurchaseOrders(selectedIds, paymentReference);
                    LoadList();
                    SetStatus("Marked " + selectedIds.Count + " purchase(s) as paid.", SaveGreen);
                }
                catch (Exception ex)
                {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Applying batch payment", ex);
                SetStatus("Batch payment could not be applied. Review the selection and try again.", Color.Red);
                }
            }
        }

        private void UpdateBatchPayVisibility()
        {
            _btnBatchPay.Visible = _viewMode == PurchaseViewMode.VendorPayables;
            _btnVendorAdvance.Visible = _viewMode == PurchaseViewMode.VendorPayables;
            _btnBatchPay.Enabled = _payableChecks.Values.Any(chk => chk.Checked);
        }

        private void RecordVendorAdvance()
        {
            Vendor selectedVendor = _current != null && _current.VendorID > 0
                ? _vendors.FirstOrDefault(v => v.VendorID == _current.VendorID)
                : _cboVendor.SelectedItem as Vendor;

            using (Form prompt = ServoModalForm.Create("Supplier Advance", 420, 250))
            {
                ComboBox cboVendor = new ComboBox { Location = new Point(16, 38), Width = 370, DropDownStyle = ComboBoxStyle.DropDownList };
                cboVendor.DataSource = new List<Vendor>(_vendors);
                cboVendor.DisplayMember = "VendorName";
                cboVendor.ValueMember = "VendorID";
                if (selectedVendor != null)
                    cboVendor.SelectedValue = selectedVendor.VendorID;
                TextBox txtAmount = new TextBox { Location = new Point(16, 88), Width = 170, BorderStyle = BorderStyle.FixedSingle };
                TextBox txtRef = new TextBox { Location = new Point(210, 88), Width = 176, BorderStyle = BorderStyle.FixedSingle };
                TextBox txtNotes = new TextBox { Location = new Point(16, 142), Width = 370, Height = 48, Multiline = true, BorderStyle = BorderStyle.FixedSingle };
                Button btnOk = MakeBtn("Record", SaveGreen, 96);
                Button btnCancel = MakeBtn("Cancel", DelRed, 96);
                btnOk.Location = new Point(186, 204);
                btnCancel.Location = new Point(290, 204);
                btnOk.DialogResult = DialogResult.OK;
                btnCancel.DialogResult = DialogResult.Cancel;

                prompt.Controls.AddRange(new Control[]
                {
                    new Label { Text = "Supplier", Location = new Point(16, 18), Width = 120 },
                    cboVendor,
                    new Label { Text = "Amount", Location = new Point(16, 68), Width = 120 },
                    txtAmount,
                    new Label { Text = "Reference", Location = new Point(210, 68), Width = 120 },
                    txtRef,
                    new Label { Text = "Notes", Location = new Point(16, 122), Width = 120 },
                    txtNotes,
                    btnOk,
                    btnCancel
                });
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return;

                Vendor vendor = cboVendor.SelectedItem as Vendor;
                if (vendor == null)
                {
                    SetStatus("Select a vendor for the advance.", Color.Red);
                    return;
                }
                decimal amount;
                if (!decimal.TryParse((txtAmount.Text ?? string.Empty).Replace(",", "").Trim(), out amount) || amount <= 0m)
                {
                    SetStatus("Enter a valid advance amount.", Color.Red);
                    return;
                }

                try
                {
                    _vendorAdvanceSvc.RecordAdvance(vendor.VendorID, amount, DateTime.Today, "Bank Transfer", txtRef.Text.Trim(), txtNotes.Text.Trim());
                    LoadList();
                    SetStatus("Supplier advance recorded. It will be deducted during final payment.", SaveGreen);
                }
                catch (Exception ex)
                {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Recording vendor advance", ex);
                SetStatus("Supplier advance could not be recorded. Review the amount and try again.", Color.Red);
                }
            }
        }

        private void OnVendorChanged()
        {
            if (_cboVendor == null)
                return;

            Vendor vendor = _cboVendor.SelectedItem as Vendor;
            if (_txtVendorGstin != null)
                _txtVendorGstin.Text = vendor?.GSTNumber ?? string.Empty;
            RefreshPayByDate();
        }

        private void RefreshPayByDate()
        {
            if (_cboVendor == null || _dtpDate == null || _dtpPayByDate == null)
                return;

            Vendor vendor = _cboVendor.SelectedItem as Vendor;
            DateTime payByDate = _svc.AutoSuggestPayByDate(_dtpDate.Value.Date, vendor?.VendorID ?? 0);
            if (_dtpPayByDate.Value.Date != payByDate)
                _dtpPayByDate.Value = payByDate;
        }

        private void ActivatePendingPaymentsView()
        {
            _activeTab = PurchaseListTab.PendingOrders;
            _viewMode = PurchaseViewMode.Orders;
            _btnPayablesToggle.Text = "Supplier Payables";
            ApplyFiltersAndRender();
        }

        private void ApplyVendorPrefill(int vendorId)
        {
            NewRecord();
            for (int i = 0; i < _cboVendor.Items.Count; i++)
            {
                Vendor vendor = _cboVendor.Items[i] as Vendor;
                if (vendor != null && vendor.VendorID == vendorId)
                {
                    _cboVendor.SelectedIndex = i;
                    break;
                }
            }

            _txtVendorGstin.Text = (_cboVendor.SelectedItem as Vendor)?.GSTNumber ?? string.Empty;
            RefreshPayByDate();

            if (_lineItemFlow.Controls.Count > 0)
            {
                Control firstCard = _lineItemFlow.Controls[0];
                ComboBox desc = firstCard.Controls["cmbDesc"] as ComboBox;
                if (desc != null)
                    BeginInvoke((Action)(() => desc.Focus()));
            }

            SetStatus("Quick PO ready for selected vendor.", SaveGreen);
        }

        private void UpdateTabButtons()
        {
            List<PurchaseOrder> source = _orderSource ?? new List<PurchaseOrder>();
            int pending = source.Count(p => string.Equals(p.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            int partial = source.Count(p => string.Equals(p.Status, "Partial", StringComparison.OrdinalIgnoreCase));
            int approved = source.Count(p => NormalizePoStatus(p.Status) == "Approved");
            int closed = source.Count(IsClosedPo);
            int all = source.Count;
            if (_btnPendingTab != null) _btnPendingTab.Text = "Pending (" + pending + ")";
            if (_btnPartialTab != null) _btnPartialTab.Text = "Partial (" + partial + ")";
            if (_btnAllTab != null) _btnAllTab.Text = "All (" + all + ")";
            if (_btnApprovedTab != null) _btnApprovedTab.Text = "Approved (" + approved + ")";
            if (_btnClosedTab != null) _btnClosedTab.Text = "Closed (" + closed + ")";
            SetTabStyle(_btnPendingTab, _activeTab == PurchaseListTab.PendingOrders);
            SetTabStyle(_btnPartialTab, _activeTab == PurchaseListTab.PartialOrders);
            SetTabStyle(_btnAllTab, _activeTab == PurchaseListTab.AllPurchaseOrders);
            SetTabStyle(_btnApprovedTab, _activeTab == PurchaseListTab.ApprovedOrders);
            SetTabStyle(_btnClosedTab, _activeTab == PurchaseListTab.ClosedOrders);
        }

        private void SetTabStyle(Button button, bool active)
        {
            if (button == null)
                return;
            button.BackColor = active ? InfoBlue : Color.FromArgb(241, 245, 249);
            button.ForeColor = active ? Color.White : Color.FromArgb(71, 85, 105);
        }

        private Button MakeTabButton(string text)
        {
            Button btn = new Button
            {
                Text = text,
                Width = 176,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = DS.Border;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private static void AddTabButtonToGrid(TableLayoutPanel grid, Button button, int column, int row)
        {
            if (grid == null || button == null)
                return;

            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 8, 6);
            button.MinimumSize = new Size(0, 26);
            grid.Controls.Add(button, column, row);
        }

        private void PreviewPurchaseOrder()
        {
            try
            {
                PurchaseOrder po;
                try
                {
                    po = BuildPreviewPurchaseOrder();
                }
                catch
                {
                    Vendor vendor = _cboVendor?.SelectedItem as Vendor;
                    po = new PurchaseOrder
                    {
                        PONumber = string.IsNullOrWhiteSpace(_txtPONumber?.Text) ? "DRAFT-PO-PREVIEW" : _txtPONumber.Text.Trim(),
                        PODate = _dtpDate?.Value.Date ?? DateTime.Today,
                        PayByDate = _dtpPayByDate?.Value.Date ?? DateTime.Today.AddDays(30),
                        VendorName = vendor?.VendorName ?? "Draft Supplier",
                        VendorGSTIN = string.IsNullOrWhiteSpace(_txtVendorGstin?.Text) ? vendor?.GSTNumber : _txtVendorGstin.Text.Trim(),
                        VendorInvoiceNumber = _txtVendorInvoiceNumber?.Text?.Trim(),
                        LineItems = new List<PurchaseLineItem>()
                    };
                    po.LineItems.Add(new PurchaseLineItem { Description = "Draft service / material line", HsnSacCode = "9987", UOM = "Nos", Quantity = 1, Rate = 0, IGSTRate = 18 });
                }
                string html = _svc.BuildPurchaseOrderHtml(po);
                new HtmlPreviewDialog("Purchase Order Preview - " + (po.PONumber ?? "(draft)"), html).ShowDialog(this);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Previewing purchase order", ex);
            }
        }

        private void SavePurchaseOrderPdf()
        {
            try
            {
                PurchaseOrder po = BuildPreviewPurchaseOrder();
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Filter = "PDF Files (*.pdf)|*.pdf";
                    dlg.DefaultExt = "pdf";
                    dlg.AddExtension = true;
                    dlg.FileName = (po.PONumber ?? "purchase-order") + ".pdf";
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    string html = _svc.BuildPurchaseOrderHtml(po);
                    ExportHtmlToPdf(html, dlg.FileName);
                    SetStatus("PDF saved to " + dlg.FileName, SaveGreen);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Generating purchase order PDF", ex);
            }
        }

        private void SendPurchaseOrder()
        {
            try
            {
                PurchaseOrder po = BuildPreviewPurchaseOrder();
                Vendor vendor = _cboVendor.SelectedItem as Vendor;
                string pdfPath = EnsurePdfForSend(po);

                using (Form dlg = ServoModalForm.Create("Send Purchase Order", 360, 132))
                {
                    Label lbl = new Label { Text = "Choose how to send " + (po.PONumber ?? "this PO"), Location = new Point(16, 18), Width = 300 };
                    Button btnEmail = MakeBtn("Email", InfoBlue, 120);
                    Button btnWhatsApp = MakeBtn("WhatsApp", SaveGreen, 120);
                    btnEmail.Location = new Point(36, 58);
                    btnWhatsApp.Location = new Point(184, 58);
                    btnEmail.Click += (s, e) =>
                    {
                        OpenEmail(po, vendor, pdfPath);
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    };
                    btnWhatsApp.Click += (s, e) =>
                    {
                        OpenWhatsApp(po, vendor, pdfPath);
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    };
                    dlg.Controls.AddRange(new Control[] { lbl, btnEmail, btnWhatsApp });
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Preparing purchase order email", ex);
            }
        }

        private void ConvertToBill()
        {
            if (_current == null)
            {
                SetStatus("Select a purchase order first.", WarnOrange);
                return;
            }

            if (!_current.AddToClientInvoice)
            {
                _current.AddToClientInvoice = true;
                _svc.Update(_current);
            }
            PendingChargeResult result = _svc.CreatePendingCharge(_current.POID);
            _current = _svc.GetById(_current.POID);
            PopulateDetail(_current);
            MessageBox.Show(result.Message, "Convert to Bill", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CancelPurchaseOrder()
        {
            if (_current == null)
            {
                SetStatus("Select a purchase order first.", WarnOrange);
                return;
            }

            if (MessageBox.Show("Cancel this purchase order?", "Cancel PO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _current.Status = "Cancelled";
                _svc.Update(_current);
                _current = _svc.GetById(_current.POID);
                PopulateDetail(_current);
                LoadList();
                SetStatus("Purchase order cancelled.", DelRed);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Cancelling purchase order", ex);
                SetStatus("Purchase order could not be cancelled. Refresh and try again.", Color.Red);
            }
        }

        private void ClonePurchaseOrder()
        {
            if (_current == null)
            {
                SetStatus("Select a PO to clone.", Color.Gray);
                return;
            }

            try
            {
                PurchaseOrder clone = BuildPurchaseOrderFromForm((Vendor)_cboVendor.SelectedItem);
                clone.POID = 0;
                clone.PONumber = "PO-" + DateTime.Now.ToString("yyyyMMdd-HHmm");
                clone.Status = "Pending";
                clone.CreatedByDate = DateTime.Now;
                int newId = _svc.Create(clone);
                _current = _svc.GetById(newId);
                LoadList();
                PopulateDetail(_current);
                SetStatus("PO cloned as " + _current.PONumber + ".", SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Cloning purchase order", ex);
                SetStatus("Purchase order could not be cloned. Review the source PO and try again.", Color.Red);
            }
        }

        private void DeletePurchaseOrder()
        {
            if (_current == null || _current.POID <= 0)
            {
                SetStatus("Select a saved PO to delete.", Color.Gray);
                return;
            }

            string poNo = string.IsNullOrWhiteSpace(_current.PONumber) ? "this purchase order" : _current.PONumber;
            if (MessageBox.Show("Permanently delete " + poNo + " including line items, pending charges, and vendor advance links?\r\n\r\nThis cannot be undone.", "Delete PO", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                int deletedId = _current.POID;
                _svc.Delete(deletedId);
                _current = null;
                LoadList();
                NewRecord();
                SetStatus("Purchase order deleted.", DelRed);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Deleting purchase order", ex);
                SetStatus("Purchase order could not be deleted. Refresh and try again.", Color.Red);
            }
        }

        private void AddFromRfq()
        {
            try
            {
                List<TenderBid> quotes = _tenderSvc.GetAll()
                    .Where(q => q.BidID > 0)
                    .OrderByDescending(q => q.RequiredByDate ?? q.DueDate)
                    .Take(80)
                    .ToList();
                if (quotes.Count == 0)
                {
                    SetStatus("No RFQ/quotation records are available.", WarnOrange);
                    return;
                }

                using (Form dialog = ServoModalForm.Create("Add from RFQ / Quotation", 620, 390))
                using (ComboBox cboQuote = new ComboBox())
                using (CheckedListBox lineList = new CheckedListBox())
                using (Button btnAdd = MakeBtn("Add Lines", SaveGreen, 96))
                using (Button btnCancel = MakeBtn("Cancel", DelRed, 86))
                {
                    cboQuote.Location = new Point(18, 38);
                    cboQuote.Width = 570;
                    cboQuote.DropDownStyle = ComboBoxStyle.DropDownList;
                    foreach (TenderBid quote in quotes)
                        cboQuote.Items.Add(new ComboItem<TenderBid>(quote, quote.QuotationNumber + " - " + quote.TenderName));

                    lineList.Location = new Point(18, 92);
                    lineList.Size = new Size(570, 220);
                    lineList.CheckOnClick = true;

                    Action loadLines = () =>
                    {
                        lineList.Items.Clear();
                        ComboItem<TenderBid> selected = cboQuote.SelectedItem as ComboItem<TenderBid>;
                        if (selected == null)
                            return;
                        TenderBid detailed = _tenderSvc.GetByIdDetailed(selected.Value.BidID);
                        foreach (TenderBidLineItem line in detailed.LineItems.Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription)))
                        {
                            decimal rate = line.CostPerUnit > 0 ? line.CostPerUnit : line.SellPricePerUnit;
                            string text = line.ItemDescription + " | " + line.Quantity.ToString("0.##") + " " + (line.Unit ?? "Nos") + " | " + IndiaFormatHelper.FormatCurrency(rate);
                            lineList.Items.Add(new ComboItem<TenderBidLineItem>(line, text), true);
                        }
                    };

                    cboQuote.SelectedIndexChanged += (s, e) => loadLines();
                    if (cboQuote.Items.Count > 0)
                        cboQuote.SelectedIndex = 0;

                    btnAdd.Location = new Point(394, 332);
                    btnCancel.Location = new Point(502, 332);
                    btnAdd.DialogResult = DialogResult.OK;
                    btnCancel.DialogResult = DialogResult.Cancel;
                    dialog.Controls.AddRange(new Control[]
                    {
                        new Label { Text = "Quotation", Location = new Point(18, 16), Width = 180, Font = new Font("Segoe UI", 9, FontStyle.Bold) },
                        cboQuote,
                        new Label { Text = "Line items", Location = new Point(18, 70), Width = 180, Font = new Font("Segoe UI", 9, FontStyle.Bold) },
                        lineList,
                        btnAdd,
                        btnCancel
                    });
                    dialog.AcceptButton = btnAdd;
                    dialog.CancelButton = btnCancel;

                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    int added = 0;
                    foreach (object checkedItem in lineList.CheckedItems)
                    {
                        ComboItem<TenderBidLineItem> item = checkedItem as ComboItem<TenderBidLineItem>;
                        TenderBidLineItem rfqLine = item?.Value;
                        if (rfqLine == null)
                            continue;
                        decimal rate = rfqLine.CostPerUnit > 0 ? rfqLine.CostPerUnit : rfqLine.SellPricePerUnit;
                        AddLineItemCard(new PurchaseLineItem
                        {
                            InventoryItemId = rfqLine.InventoryItemId,
                            Description = rfqLine.ItemDescription,
                            HsnSacCode = rfqLine.HsnSacCode,
                            Quantity = rfqLine.Shortfall > 0 ? rfqLine.Shortfall : rfqLine.Quantity,
                            UOM = string.IsNullOrWhiteSpace(rfqLine.Unit) ? "Nos" : rfqLine.Unit,
                            Rate = rate,
                            GSTRate = rfqLine.GSTRatePct,
                            IGSTRate = rfqLine.GSTRatePct,
                            JobLink = "General"
                        });
                        added++;
                    }
                    SetStatus(added + " RFQ line item(s) added.", SaveGreen);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Adding RFQ lines", ex);
                SetStatus("RFQ lines could not be added. Review the quotation and try again.", Color.Red);
            }
        }

        private void ApplyDiscountToLines()
        {
            using (Form prompt = ServoModalForm.Create("Apply Discount", 280, 126))
            {
                Label lbl = new Label { Text = "Discount percent", Location = new Point(16, 18), Width = 160 };
                NumericUpDown value = MakeDecimalBox("discount", new Point(16, 46), 120, 2, 100m, 0m);
                Button ok = MakeBtn("Apply", InfoBlue, 80);
                Button cancel = MakeOutlineButton("Cancel", 80);
                ok.Location = new Point(104, 84);
                cancel.Location = new Point(190, 84);
                ok.DialogResult = DialogResult.OK;
                cancel.DialogResult = DialogResult.Cancel;
                prompt.Controls.AddRange(new Control[] { lbl, value, ok, cancel });
                prompt.AcceptButton = ok;
                prompt.CancelButton = cancel;
                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return;

                foreach (Control row in _lineItemFlow.Controls)
                    if (row.Controls["numDiscount"] is NumericUpDown discount)
                        discount.Value = value.Value;
                RecalcTotal();
            }
        }

        private void AddCharges()
        {
            using (Form prompt = ServoModalForm.Create("Other Charges", 280, 126))
            {
                Label lbl = new Label { Text = "Other charges amount", Location = new Point(16, 18), Width = 180 };
                NumericUpDown value = MakeDecimalBox("charges", new Point(16, 46), 140, 2, 9999999m, _otherCharges);
                Button ok = MakeBtn("Apply", InfoBlue, 80);
                Button cancel = MakeOutlineButton("Cancel", 80);
                ok.Location = new Point(104, 84);
                cancel.Location = new Point(190, 84);
                ok.DialogResult = DialogResult.OK;
                cancel.DialogResult = DialogResult.Cancel;
                prompt.Controls.AddRange(new Control[] { lbl, value, ok, cancel });
                prompt.AcceptButton = ok;
                prompt.CancelButton = cancel;
                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return;

                _otherCharges = value.Value;
                RecalcTotal();
            }
        }

        private void SendInternalMessage(TextBox textBox)
        {
            string message = textBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message) || textBox.ForeColor == DS.Slate400)
            {
                SetStatus("Type an internal message first.", WarnOrange);
                return;
            }

            MessageBox.Show("Internal comments are not persisted yet. Note captured for this session: " + message, "Internal Communication", MessageBoxButtons.OK, MessageBoxIcon.Information);
            textBox.Clear();
            SetStatus("Internal note captured.", SaveGreen);
        }

        private void ShowPurchaseMoreMenu()
        {
            MessageBox.Show("More actions: purchase settings remain under Settings > Master Data. Export and audit tools can be added here when their services are available.", "Purchase Actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private PurchaseOrder BuildPreviewPurchaseOrder()
        {
            if (_cboVendor.SelectedItem == null)
                throw new Exception("Select a vendor before previewing or sending a purchase order.");

            Vendor vendor = (Vendor)_cboVendor.SelectedItem;
            PurchaseOrder po = BuildPurchaseOrderFromForm(vendor);
            po.POID = _current?.POID ?? 0;
            po.PaymentReference = _current?.PaymentReference;
            po.PaidAmount = _current?.PaidAmount ?? 0m;
            return po;
        }

        private string EnsurePdfForSend(PurchaseOrder po)
        {
            string html = _svc.BuildPurchaseOrderHtml(po);
            string fileName = (po.PONumber ?? "purchase-order") + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf";
            string pdfPath = Path.Combine(Path.GetTempPath(), fileName);
            ExportHtmlToPdf(html, pdfPath);
            return pdfPath;
        }

        private void ExportHtmlToPdf(string html, string pdfPath)
        {
            HtmlPdfExportService.ExportHtmlToPdf(html, pdfPath);
        }

        private void OpenEmail(PurchaseOrder po, Vendor vendor, string pdfPath)
        {
            string subject = "Purchase Order " + (po.PONumber ?? string.Empty) + " from " + BrandingService.AppName;
            string body = "Dear " + (vendor?.VendorName ?? "Supplier") + "," + Environment.NewLine + Environment.NewLine
                + "Please find attached PO " + (po.PONumber ?? string.Empty) + "." + Environment.NewLine
                + "PDF saved at: " + pdfPath + Environment.NewLine + Environment.NewLine
                + "Regards," + Environment.NewLine + BrandingService.Subtitle;

            string mailto = "mailto:" + Uri.EscapeDataString(vendor?.Email ?? string.Empty)
                + "?subject=" + Uri.EscapeDataString(subject)
                + "&body=" + Uri.EscapeDataString(body);

            Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + pdfPath + "\"") { UseShellExecute = true });
        }

        private void OpenWhatsApp(PurchaseOrder po, Vendor vendor, string pdfPath)
        {
            string phone = NormalizeWhatsAppPhone(vendor?.Phone);
            string message = "Dear " + (vendor?.VendorName ?? "Supplier") + ", please find attached PO "
                + (po.PONumber ?? string.Empty) + " dated " + _dtpDate.Value.ToString("dd/MM/yyyy")
                + " for " + IndiaFormatHelper.FormatCurrency(po.TotalAmount) + ". Kindly confirm receipt.";

            string url = "https://wa.me/" + phone + "?text=" + Uri.EscapeDataString(message);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + pdfPath + "\"") { UseShellExecute = true });
        }

        private static string NormalizeWhatsAppPhone(string phone)
        {
            string digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 10)
                return "91" + digits;
            return digits;
        }

        private Panel BuildFieldCell(string labelText, Control field, int labelWidth, int fieldWidth)
        {
            Panel cell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 12, 4) };
            Label label = new Label
            {
                Text = labelText,
                Width = labelWidth,
                Location = new Point(0, 8),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleRight
            };
            field.Location = new Point(labelWidth + 8, 4);
            field.Width = fieldWidth;
            cell.Controls.Add(label);
            cell.Controls.Add(field);
            return cell;
        }

        private Panel BuildLinkedToCell()
        {
            Panel cell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 12, 4) };
            Label label = new Label
            {
                Text = "Linked To",
                Width = 104,
                Location = new Point(0, 14),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleRight
            };
            _cboLinkedType.Location = new Point(112, 10);
            _cboLinkedType.Width = 76;
            _cboLinkedRecord.Location = new Point(194, 10);
            _cboLinkedRecord.Width = 72;
            cell.Controls.Add(label);
            cell.Controls.Add(_cboLinkedType);
            cell.Controls.Add(_cboLinkedRecord);
            return cell;
        }

        private Panel BuildLineItemHeader()
        {
            Panel header = new Panel { Height = 42, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(0, 2, 0, 0) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, header.Width - 1, header.Height - 1);
            };

            AddHeaderLabel(header, "#", 12, 24, true);
            AddHeaderLabel(header, "Item / Description", 42, 188);
            AddHeaderLabel(header, "Category", 238, 62);
            AddHeaderLabel(header, "HSN", 306, 52);
            AddHeaderLabel(header, "Qty", 364, 46, true);
            AddHeaderLabel(header, "UOM", 416, 52);
            AddHeaderLabel(header, "Rate", 474, 66, true);
            AddHeaderLabel(header, "Disc", 546, 44, true);
            AddHeaderLabel(header, "Taxable", 596, 62, true);
            AddHeaderLabel(header, "GST", 664, 44, true);
            AddHeaderLabel(header, "GST Amt", 714, 60, true);
            AddHeaderLabel(header, "Total", 780, 70, true);
            AddHeaderLabel(header, "Actions", 858, 70, true);
            return header;
        }

        private static void AddHeaderLabel(Panel parent, string text, int x, int width, bool rightAlign = false)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, 12),
                Width = width,
                Height = 18,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                TextAlign = rightAlign ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft
            });
        }

        private void ResizeLineItemRows()
        {
            if (_lineItemFlow == null)
                return;

            int rowWidth = 940;
            if (_lineItemFlow.Parent != null)
                rowWidth = Math.Max(940, _lineItemFlow.Parent.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
            _lineItemFlow.Width = rowWidth;
            if (_lineItemHeader != null)
                _lineItemHeader.Width = rowWidth;
            foreach (Control row in _lineItemFlow.Controls)
                row.Width = rowWidth;
            ApplyLineItemTableTheme();
            if (_lblLineItemEmptyState != null && _lblLineItemEmptyState.Visible)
                _lblLineItemEmptyState.BringToFront();
        }

        private void UpdateLineItemTableState(int itemCount = -1)
        {
            if (_lineItemFlow == null)
                return;

            if (itemCount < 0)
                itemCount = _lineItemFlow.Controls.Count;

            if (_lblLineItemEmptyState != null)
            {
                _lblLineItemEmptyState.Visible = itemCount == 0;
                if (_lblLineItemEmptyState.Visible)
                    _lblLineItemEmptyState.BringToFront();
            }

            if (_lblLineItemCount != null)
            {
                _lblLineItemCount.Text = itemCount == 0
                    ? "Showing 0 items"
                    : "Showing 1 to " + itemCount + " of " + itemCount + " items";
            }

            ApplyLineItemTableTheme();
        }

        private void ApplyLineItemTableTheme()
        {
            if (_lineItemFlow == null)
                return;

            for (int i = 0; i < _lineItemFlow.Controls.Count; i++)
            {
                Control row = _lineItemFlow.Controls[i];
                row.BackColor = i % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252);

                TextBox category = row.Controls["txtCategory"] as TextBox;
                if (category != null)
                {
                    category.BorderStyle = BorderStyle.None;
                    category.BackColor = Color.FromArgb(239, 246, 255);
                    category.ForeColor = InfoBlue;
                    category.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    category.TextAlign = HorizontalAlignment.Center;
                }

                Label amount = row.Controls["lblAmt"] as Label;
                if (amount != null)
                    amount.ForeColor = DS.Slate900;
            }
        }

        private string ResolveCategory(string hsnCode)
        {
            HsnSacMasterEntry entry = _hsnEntries.FirstOrDefault(h => string.Equals(h.Code, hsnCode, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(entry?.BusinessCategory) ? string.Empty : entry.BusinessCategory;
        }

        private static void ConfigureDropDownListCombo(ComboBox combo)
        {
            if (combo == null)
                return;

            combo.AutoCompleteMode = AutoCompleteMode.None;
            combo.AutoCompleteSource = AutoCompleteSource.None;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        private void AddLineItemCard(PurchaseLineItem line = null)
        {
            int rowNumber = _lineItemFlow?.Controls.Count + 1 ?? 1;
            Panel card = new Panel
            {
                Width = 940,
                Height = 62,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, card.Height - 1, card.Width, card.Height - 1);
            };

            Label lblRowNo = new Label { Name = "lblRowNo", Text = rowNumber.ToString(), Location = new Point(12, 20), Width = 24, Height = 18, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleRight };
            ComboBox cmbDesc = new ComboBox { Name = "cmbDesc", Location = new Point(42, 16), Width = 188, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbDesc);
            foreach (StockItem item in _inventoryItems)
                cmbDesc.Items.Add(item.ItemName);
            cmbDesc.Tag = null;

            TextBox txtCategory = new TextBox { Name = "txtCategory", Location = new Point(238, 16), Width = 62, Font = new Font("Segoe UI", 9), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Tag = "CUSTOM_INPUT_SHELL" };
            ComboBox cmbHsn = new ComboBox { Name = "cmbHsn", Location = new Point(306, 16), Width = 52, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbHsn);
            foreach (HsnSacMasterEntry entry in _hsnEntries.Where(h => h.IsActive))
                cmbHsn.Items.Add(entry.Code);
            cmbHsn.SelectedIndex = string.IsNullOrWhiteSpace(line?.HsnSacCode) ? -1 : cmbHsn.Items.IndexOf(line.HsnSacCode);

            NumericUpDown numQty = MakeDecimalBox("numQty", new Point(364, 16), 46, 2, 999999m, line?.Quantity > 0 ? line.Quantity : 1m);
            ComboBox cmbUom = new ComboBox { Name = "cmbUom", Location = new Point(416, 16), Width = 52, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbUom);
            cmbUom.Items.AddRange(_unitSvc.GetDisplayUnits().Cast<object>().ToArray());
            EnsureComboItem(cmbUom, "Nos");
            EnsureComboItem(cmbUom, "RMT");
            string selectedUom = _unitSvc.NormalizeForDisplayOrDefault(line?.UOM);
            EnsureComboItem(cmbUom, selectedUom);
            cmbUom.SelectedItem = selectedUom;
            NumericUpDown numRate = MakeDecimalBox("numRate", new Point(474, 16), 66, 2, 9999999m, line?.Rate ?? 0m);
            NumericUpDown numDiscount = MakeDecimalBox("numDiscount", new Point(546, 16), 44, 2, 100m, 0m);
            NumericUpDown numGst = MakeDecimalBox("numGst", new Point(664, 16), 44, 2, 100m, line?.GSTRate > 0 ? line.GSTRate : (line?.IGSTRate > 0 ? line.IGSTRate : 18m));
            Label lblTaxable = new Label { Name = "lblTaxable", Text = "Rs 0.00", Location = new Point(596, 20), Width = 62, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), TextAlign = ContentAlignment.MiddleRight };
            Label lblGstAmount = new Label { Name = "lblGstAmount", Text = "Rs 0.00", Location = new Point(714, 20), Width = 60, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), TextAlign = ContentAlignment.MiddleRight };
            NumericUpDown numCgst = MakeDecimalBox("numCgst", new Point(0, 0), 1, 2, 100m, 0m);
            NumericUpDown numSgst = MakeDecimalBox("numSgst", new Point(0, 0), 1, 2, 100m, 0m);
            NumericUpDown numIgst = MakeDecimalBox("numIgst", new Point(0, 0), 1, 2, 100m, numGst.Value);
            numCgst.Visible = false;
            numSgst.Visible = false;
            numIgst.Visible = false;
            Label lblAmt = new Label { Name = "lblAmt", Text = "Rs 0.00", Location = new Point(780, 20), Width = 70, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = InfoBlue, TextAlign = ContentAlignment.MiddleRight };
            ComboBox cmbJobLink = new ComboBox { Name = "cmbJobLink", Location = new Point(0, 0), Width = 1, Font = new Font("Segoe UI", 9), Visible = false };
            ConfigureDropDownListCombo(cmbJobLink);
            cmbJobLink.Items.AddRange(new object[] { "General", "Project", "This Job" });
            cmbJobLink.SelectedItem = string.Equals(line?.JobLink, "Job", StringComparison.OrdinalIgnoreCase) ? "This Job" : (string.Equals(line?.JobLink, "Project", StringComparison.OrdinalIgnoreCase) ? "Project" : "General");
            Button btnEdit = new Button { Text = "Cmp", Location = new Point(852, 15), Width = 42, Height = 30, BackColor = Color.White, ForeColor = InfoBlue, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold) };
            Button btnRemove = new Button { Text = "X", Location = new Point(898, 15), Width = 34, Height = 30, BackColor = Color.White, ForeColor = DelRed, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            btnEdit.FlatAppearance.BorderColor = DS.Border;
            _toolTip.SetToolTip(btnEdit, "Compare supplier prices for this material");
            btnEdit.Click += (s, e) => ShowSupplierComparisonForLineItem(cmbDesc, txtCategory, numQty, cmbUom, numRate);
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.Click += (s, e) => { _lineItemFlow.Controls.Remove(card); RenumberLineItems(); RecalcTotal(); };

            Action applySelectedInventory = () =>
            {
                StockItem stock = _inventoryItems.FirstOrDefault(item => string.Equals(item.ItemName, cmbDesc.Text, StringComparison.OrdinalIgnoreCase));
                if (stock != null)
                {
                    cmbDesc.Tag = stock.ItemID;
                    txtCategory.Text = string.IsNullOrWhiteSpace(stock.Category) ? ResolveCategory(cmbHsn.Text) : stock.Category;
                    if (numRate.Value <= 0 && stock.LastPurchaseRate > 0)
                        numRate.Value = stock.LastPurchaseRate;
                    if (string.IsNullOrWhiteSpace(cmbHsn.Text))
                    {
                        HsnSacMasterEntry resolved = ResolveHsnForDescription(cmbDesc.Text);
                        if (resolved != null)
                        {
                            cmbHsn.Text = resolved.Code;
                            ApplyHsnRates(resolved, numGst, numCgst, numSgst, numIgst);
                            txtCategory.Text = resolved.BusinessCategory ?? string.Empty;
                        }
                    }
                }

                RecalcTotal();
                ApplySupplierPriceHint(cmbDesc, txtCategory, numQty, btnEdit);
            };
            cmbDesc.SelectionChangeCommitted += (s, e) => applySelectedInventory();
            cmbJobLink.SelectedIndexChanged += (s, e) =>
            {
                if (cmbJobLink.Text == "This Job" && !IsWorkOrderLinked())
                {
                    cmbJobLink.SelectedItem = "General";
                    _toolTip.Show("Link PO to a Work Order first", cmbJobLink, 2500);
                }
            };

            cmbHsn.SelectedIndexChanged += (s, e) =>
            {
                HsnSacMasterEntry entry = _hsnEntries.FirstOrDefault(h => string.Equals(h.Code, cmbHsn.Text, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    ApplyHsnRates(entry, numGst, numCgst, numSgst, numIgst);
                    txtCategory.Text = entry.BusinessCategory ?? string.Empty;
                }
            };

            foreach (NumericUpDown num in new[] { numQty, numRate, numGst, numCgst, numSgst, numIgst })
                num.ValueChanged += (s, e) => RecalcTotal();
            numDiscount.ValueChanged += (s, e) => RecalcTotal();
            cmbUom.SelectedIndexChanged += (s, e) => RecalcTotal();

            numRate.ValueChanged += (s, e) => ApplyRateVarianceVisual(card);

            foreach (Control ctl in new Control[] { lblRowNo, cmbDesc, txtCategory, cmbHsn, numQty, cmbUom, numRate, numDiscount, lblTaxable, numGst, lblGstAmount, numCgst, numSgst, numIgst, lblAmt, cmbJobLink, btnEdit, btnRemove })
                card.Controls.Add(ctl);

            StockItem selectedStock = null;
            if (line?.InventoryItemId.HasValue == true)
                selectedStock = _inventoryItems.FirstOrDefault(item => item.ItemID == line.InventoryItemId.Value);
            if (selectedStock == null && !string.IsNullOrWhiteSpace(line?.Description))
                selectedStock = _inventoryItems.FirstOrDefault(item => string.Equals(item.ItemName, line.Description, StringComparison.OrdinalIgnoreCase));

            if (selectedStock != null)
            {
                cmbDesc.SelectedItem = selectedStock.ItemName;
                applySelectedInventory();
            }
            else if (!string.IsNullOrWhiteSpace(line?.Description))
            {
                if (!cmbDesc.Items.Contains(line.Description))
                    cmbDesc.Items.Add(line.Description);
                cmbDesc.SelectedItem = line.Description;
                cmbDesc.Tag = line.InventoryItemId;
                txtCategory.Text = ResolveCategory(cmbHsn.Text);
            }

            if (!string.IsNullOrWhiteSpace(cmbHsn.Text))
                txtCategory.Text = ResolveCategory(cmbHsn.Text);
            _lineItemFlow.Controls.Add(card);
            ResizeLineItemRows();
            ApplyLineItemTableTheme();
            ApplyRateVarianceVisual(card, line?.PriceVariance > 10m);
            ApplySupplierPriceHint(cmbDesc, txtCategory, numQty, btnEdit);
            RecalcTotal();
        }

        private void ShowSupplierComparisonForLineItem(ComboBox description, TextBox category, NumericUpDown quantity, ComboBox unit, NumericUpDown rate)
        {
            string itemDescription = description?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                _toolTip.Show("Select a material first.", description ?? (Control)rate, 2500);
                SetStatus("Select a material before comparing supplier prices.", WarnOrange);
                return;
            }

            using (var dialog = new SupplierPriceComparisonDialog(itemDescription, category?.Text, quantity?.Value ?? 1m, _vndSvc))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedOption == null)
                    return;

                SupplierOption option = dialog.SelectedOption;
                if (rate != null)
                    rate.Value = Math.Max(rate.Minimum, Math.Min(rate.Maximum, option.Rate));
                if (unit != null && !string.IsNullOrWhiteSpace(option.Unit))
                {
                    string optionUnit = _unitSvc.NormalizeForDisplayOrDefault(option.Unit);
                    EnsureComboItem(unit, optionUnit);
                    unit.SelectedItem = optionUnit;
                }

                SelectPurchaseVendorById(option.VendorID);
                RecalcTotal();
                SetStatus("Supplier comparison applied: " + option.VendorName + " at " + IndiaFormatHelper.FormatCurrency(option.Rate) + ".", SaveGreen);
            }
        }

        private void ApplySupplierPriceHint(ComboBox description, TextBox category, NumericUpDown quantity, Button compareButton)
        {
            if (compareButton == null)
                return;

            string itemDescription = description?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                _toolTip.SetToolTip(compareButton, "Compare supplier prices for this material");
                return;
            }

            try
            {
                SupplierOption best = _vndSvc.GetBestSupplierForItem(itemDescription, quantity?.Value ?? 1m, category?.Text);
                if (best == null)
                {
                    compareButton.ForeColor = DS.Slate500;
                    _toolTip.SetToolTip(compareButton, "No saved supplier price found. Click to review purchase-history options.");
                    return;
                }

                compareButton.ForeColor = SaveGreen;
                _toolTip.SetToolTip(compareButton, "Best supplier: " + best.VendorName + " at " + IndiaFormatHelper.FormatCurrency(best.Rate) + ". Click to compare all suppliers.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PurchaseForm.ApplySupplierPriceHint", ex);
                compareButton.ForeColor = InfoBlue;
                _toolTip.SetToolTip(compareButton, "Compare supplier prices for this material");
            }
        }

        private void SelectPurchaseVendorById(int vendorId)
        {
            if (_cboVendor == null || vendorId <= 0)
                return;

            for (int i = 0; i < _cboVendor.Items.Count; i++)
            {
                Vendor vendor = _cboVendor.Items[i] as Vendor;
                if (vendor != null && vendor.VendorID == vendorId)
                {
                    _cboVendor.SelectedIndex = i;
                    return;
                }
            }
        }

        private NumericUpDown MakeDecimalBox(string name, Point location, int width, int decimals, decimal maximum, decimal value)
        {
            return new NumericUpDown
            {
                Name = name,
                Location = location,
                Width = width,
                Font = new Font("Segoe UI", 9),
                DecimalPlaces = decimals,
                Maximum = maximum,
                Minimum = 0,
                Value = value < 0 ? 0 : value
            };
        }

        private HsnSacMasterEntry ResolveHsnForDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return _hsnEntries.FirstOrDefault(h => h.IsDefault);
            string text = description.ToLowerInvariant();
            if (text.Contains("cable") || text.Contains("mcb") || text.Contains("contactor"))
                return _hsnEntries.FirstOrDefault(h => h.Code == "8544");
            if (text.Contains("copper"))
                return _hsnEntries.FirstOrDefault(h => h.Code == "7411");
            return _hsnEntries.FirstOrDefault(h => h.Code == "8415") ?? _hsnEntries.FirstOrDefault(h => h.IsDefault);
        }

        private static void ApplyHsnRates(HsnSacMasterEntry entry, NumericUpDown gst, NumericUpDown cgst, NumericUpDown sgst, NumericUpDown igst)
        {
            gst.Value = ClampDecimal(entry.TaxRate, gst.Maximum);
            cgst.Value = ClampDecimal(entry.CGSTRate, cgst.Maximum);
            sgst.Value = ClampDecimal(entry.SGSTRate, sgst.Maximum);
            igst.Value = ClampDecimal(entry.IGSTRate, igst.Maximum);
        }

        private static decimal ClampDecimal(decimal value, decimal max)
        {
            if (value < 0)
                return 0;
            return value > max ? max : value;
        }

        private static string ToSimpleAmountWords(decimal amount)
        {
            long rupees = (long)Math.Floor(amount);
            int paise = (int)Math.Round((amount - rupees) * 100m, 0, MidpointRounding.AwayFromZero);
            if (paise == 100)
            {
                rupees++;
                paise = 0;
            }
            if (rupees <= 0 && paise <= 0)
                return "Zero Rupees Only";
            if (rupees >= 10000000)
                return rupees.ToString("N0") + (paise > 0 ? " Rupees and " + paise.ToString("00") + " Paise" : " Rupees Only");

            string[] ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            Func<long, string> belowThousand = null;
            belowThousand = n =>
            {
                string result = string.Empty;
                if (n >= 100)
                {
                    result += ones[n / 100] + " Hundred ";
                    n %= 100;
                }
                if (n >= 20)
                {
                    result += tens[n / 10] + " ";
                    n %= 10;
                }
                if (n > 0)
                    result += ones[n] + " ";
                return result.Trim();
            };

            StringBuilder words = new StringBuilder();
            if (rupees >= 100000)
            {
                words.Append(belowThousand(rupees / 100000)).Append(" Lakh ");
                rupees %= 100000;
            }
            if (rupees >= 1000)
            {
                words.Append(belowThousand(rupees / 1000)).Append(" Thousand ");
                rupees %= 1000;
            }
            if (rupees > 0)
                words.Append(belowThousand(rupees));
            string amountWords = words.ToString().Trim();
            if (string.IsNullOrWhiteSpace(amountWords))
                amountWords = "Zero";
            if (paise > 0)
                return amountWords + " Rupees and " + belowThousand(paise) + " Paise";
            return amountWords + " Rupees Only";
        }

        private void RecalcTotal()
        {
            decimal subtotal = 0m;
            decimal discountTotal = 0m;
            decimal gstTotal = 0m;
            decimal total = 0m;
            int itemCount = 0;
            bool hasVariance = false;
            foreach (Control row in _lineItemFlow.Controls)
            {
                NumericUpDown qtyCtl = row.Controls["numQty"] as NumericUpDown;
                NumericUpDown rateCtl = row.Controls["numRate"] as NumericUpDown;
                NumericUpDown discountCtl = row.Controls["numDiscount"] as NumericUpDown;
                NumericUpDown gstCtl = row.Controls["numGst"] as NumericUpDown;
                NumericUpDown igstCtl = row.Controls["numIgst"] as NumericUpDown;
                Label taxableLbl = row.Controls["lblTaxable"] as Label;
                Label gstAmountLbl = row.Controls["lblGstAmount"] as Label;
                Label amountLbl = row.Controls["lblAmt"] as Label;
                if (qtyCtl == null || rateCtl == null || amountLbl == null || taxableLbl == null)
                    continue;

                decimal gross = Math.Round(qtyCtl.Value * rateCtl.Value, 2);
                decimal discount = Math.Round(gross * ((discountCtl?.Value ?? 0m) / 100m), 2);
                decimal taxable = Math.Round(gross - discount, 2);
                decimal gstRate = gstCtl?.Value ?? 0m;
                if (igstCtl != null)
                    igstCtl.Value = ClampDecimal(gstRate, igstCtl.Maximum);
                decimal totalTax = Math.Round(taxable * (gstRate / 100m), 2);
                decimal amount = taxable + totalTax;
                taxableLbl.Text = "Rs " + taxable.ToString("N2");
                if (gstAmountLbl != null) gstAmountLbl.Text = "Rs " + totalTax.ToString("N2");
                amountLbl.Text = "Rs " + amount.ToString("N2");
                subtotal += taxable;
                discountTotal += discount;
                gstTotal += totalTax;
                total += amount;
                itemCount++;
                if (rateCtl != null && rateCtl.BackColor == Color.FromArgb(255, 235, 235))
                    hasVariance = true;
            }

            total += _otherCharges;
            _lblTotal.Text = "Sub Total  " + IndiaFormatHelper.FormatCurrency(subtotal)
                + "     Discount  " + IndiaFormatHelper.FormatCurrency(discountTotal)
                + "     GST  " + IndiaFormatHelper.FormatCurrency(gstTotal)
                + "     Other Charges  " + IndiaFormatHelper.FormatCurrency(_otherCharges)
                + "     Total  " + IndiaFormatHelper.FormatCurrency(total);
            if (_lblSummaryItems != null) _lblSummaryItems.Text = itemCount.ToString();
            if (_lblSummarySubtotal != null) _lblSummarySubtotal.Text = IndiaFormatHelper.FormatCurrency(subtotal);
            if (_lblSummaryDiscount != null) _lblSummaryDiscount.Text = IndiaFormatHelper.FormatCurrency(discountTotal);
            if (_lblSummaryTax != null) _lblSummaryTax.Text = IndiaFormatHelper.FormatCurrency(gstTotal);
            if (_lblSummaryCharges != null) _lblSummaryCharges.Text = IndiaFormatHelper.FormatCurrency(_otherCharges);
            if (_lblSummaryTotal != null) _lblSummaryTotal.Text = IndiaFormatHelper.FormatCurrency(total);
            if (_lblSummaryWords != null) _lblSummaryWords.Text = ToSimpleAmountWords(total);
            _lblVarianceWarning.Visible = hasVariance;
            UpdateLineItemTableState(itemCount);
        }

        private void ApplyRateVarianceVisual(Control row, bool forceHighlight = false)
        {
            if (row == null)
                return;

            ComboBox descCombo = row.Controls["cmbDesc"] as ComboBox;
            NumericUpDown rateCtl = row.Controls["numRate"] as NumericUpDown;
            if (descCombo == null || rateCtl == null)
                return;

            PurchaseLineItem line = new PurchaseLineItem
            {
                Description = descCombo.Text,
                Rate = rateCtl.Value
            };
            bool detectedVariance = _svc.CheckLineItemPriceVariance(line);
            bool isVariance = forceHighlight || detectedVariance;
            if (isVariance)
            {
                rateCtl.BackColor = Color.FromArgb(255, 235, 235);
                _toolTip.SetToolTip(rateCtl, "Price is " + line.PriceVariance.ToString("0.##") + "% above last recorded rate. Last rate: " + IndiaFormatHelper.FormatCurrency(line.HistoricalRate));
            }
            else
            {
                rateCtl.BackColor = Color.White;
                _toolTip.SetToolTip(rateCtl, string.Empty);
            }
            RecalcTotal();
        }

        private Color GetDueColor(PurchaseOrder po)
        {
            if (po == null)
                return InfoBlue;
            if (po.BalanceDue <= 0.01m || string.Equals(po.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                return InfoBlue;
            if (po.IsOverdue)
                return PendingRed;
            if ((po.PayByDate.Date - DateTime.Today).Days <= 7)
                return PendingAmber;
            return PendingGreen;
        }

        private Color GetStatusColor(string status)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
                return DelRed;
            if (string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
                return WarnOrange;
            if (string.Equals(status, "Received", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Fully Received", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
                return SaveGreen;
            return InfoBlue;
        }

        private void RenumberLineItems()
        {
            int index = 1;
            foreach (Control row in _lineItemFlow.Controls)
            {
                if (row.Controls["lblRowNo"] is Label label)
                    label.Text = index.ToString();
                index++;
            }
            UpdateLineItemTableState(index - 1);
        }

        private void SelectComboByText(ComboBox combo, string text)
        {
            int index = combo.Items.IndexOf(text);
            combo.SelectedIndex = index >= 0 ? index : 0;
        }

        private static void EnsureComboItem(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrWhiteSpace(value))
                return;

            bool exists = combo.Items.Cast<object>().Any(item => string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                combo.Items.Add(value);
        }

        private void SetStatus(string msg, Color color)
        {
            if (_lblStatus == null)
                return;
            _lblStatus.Text = msg;
            _lblStatus.ForeColor = color;
            _lblStatus.Visible = !string.IsNullOrWhiteSpace(msg);
        }

        private Label MakeLabel(string text, Point loc)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.Gray,
                Location = loc,
                Width = 135,
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            UIHelper.ApplyActionButton(btn);
            return btn;
        }

        private Button MakeOutlineButton(string text, int width)
        {
            Button btn = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                BackColor = Color.White,
                ForeColor = DS.Slate900,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.Margin = new Padding(0, 0, 8, 0);
            UIHelper.ApplyActionButton(btn, UIHelper.ResolveActionVariant(text));
            return btn;
        }

        private void ApplyPurchaseReferenceSkin(Control.ControlCollection controls)
        {
            foreach (Control child in controls.Cast<Control>().ToList())
            {
                if (child is TextBox || child is ComboBox || child is DateTimePicker || child is NumericUpDown)
                {
                    WrapPurchaseReferenceInput(child);
                    continue;
                }

                if (child is Button button)
                    StylePurchaseReferenceButton(button);

                if (child is Panel panel && panel.BackColor == Color.White && panel.Width > 30 && panel.Height > 24)
                {
                    if (!(panel.Tag as string == "purchase-input-host"))
                        DS.Rounded(panel, 8);
                }

                ApplyPurchaseReferenceSkin(child.Controls);
            }
        }

        private void WrapPurchaseReferenceInput(Control input)
        {
            Control parent = input.Parent;
            if (parent == null || parent.Tag as string == "purchase-input-host" || input is DataGridView)
                return;

            Rectangle bounds = input.Bounds;
            int index = parent.Controls.GetChildIndex(input);
            parent.Controls.Remove(input);

            Panel host = new Panel
            {
                Tag = "purchase-input-host",
                Location = bounds.Location,
                Size = bounds.Size,
                BackColor = Color.White,
                Margin = input.Margin
            };
            host.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, host.Width - 1, host.Height - 1);
                using (GraphicsPath path = CreatePurchaseRoundedPath(rect, 4))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(host, 4);

            input.Location = input is DateTimePicker || input is ComboBox || input is NumericUpDown
                ? new Point(4, Math.Max(1, (host.Height - input.Height) / 2))
                : new Point(8, input is TextBox tb && tb.Multiline ? 6 : Math.Max(2, (host.Height - input.Height) / 2));
            input.Width = Math.Max(24, host.Width - (input.Left * 2));
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            input.Font = new Font("Segoe UI", 8.25f, input.Font.Style);
            input.BackColor = Color.White;
            input.ForeColor = DS.Slate900;

            if (input is TextBox textBox)
                textBox.BorderStyle = BorderStyle.None;
            if (input is NumericUpDown numeric)
                numeric.BorderStyle = BorderStyle.None;
            if (input is ComboBox combo)
            {
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.FlatStyle = FlatStyle.Flat;
            }

            host.Controls.Add(input);
            parent.Controls.Add(host);
            parent.Controls.SetChildIndex(host, index);
        }

        private void StylePurchaseReferenceButton(Button button)
        {
            if (IsPurchaseActionButton(button.Text))
            {
                UIHelper.ApplyActionButton(button);
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            if (button.BackColor == Color.White)
            {
                button.ForeColor = DS.Slate900;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = DS.Border;
            }
            DS.Rounded(button, 5);
        }

        private static bool IsPurchaseActionButton(string text)
        {
            string key = (text ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string[] actionTokens =
            {
                "new po", "refresh", "vendor payables", "save", "draft", "mark received",
                "preview", "print", "send", "convert", "receipt", "advance", "import",
                "template", "forms", "batch pay", "attach", "view", "delete", "clear",
                "add item", "add from rfq", "import items", "apply discount", "add charges",
                "select vendor", "confirm", "record", "email", "whatsapp", "apply", "cancel po",
                "clone po", "..."
            };

            return actionTokens.Any(token => key.Contains(token));
        }

        private GraphicsPath CreatePurchaseRoundedPath(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Panel CreateCardPanel()
        {
            Panel panel = new Panel
            {
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = CreatePurchaseRoundedPath(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8))
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(panel, 8);
            return panel;
        }

        private Label MakeStatusBadge(string text, Color color, Point location, int width)
        {
            return new Label
            {
                Text = text,
                Location = location,
                Size = new Size(width, 20),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = color,
                BackColor = Color.FromArgb(245, 247, 255),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private void LayoutHeaderStatusBadge()
        {
            if (_lblHeaderStatus == null)
                return;

            int textWidth = TextRenderer.MeasureText(_lblHeaderStatus.Text ?? string.Empty, _lblHeaderStatus.Font).Width;
            _lblHeaderStatus.Width = Math.Max(68, textWidth + 24);
            int left = _lblBreadcrumb != null ? _lblBreadcrumb.Right + 10 : 224;
            _lblHeaderStatus.Location = new Point(left, 40);
        }

        private sealed class PurchasePreviewDialog : ServoERP.Infrastructure.ServoFormBase
        {
            private readonly WebBrowser _browser = new WebBrowser();

            public PurchasePreviewDialog(string title, string html)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Purchase Order Preview - " + title;
                Width = 1100;
                Height = 760;
                StartPosition = FormStartPosition.CenterParent;

                Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
                Button btnPrintPreview = new Button { Text = "Print Preview", Width = 110, Height = 28, Location = new Point(10, 8), BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnPrint = new Button { Text = "Print", Width = 90, Height = 28, Location = new Point(128, 8), BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnPrintPreview.FlatAppearance.BorderSize = 0;
                btnPrint.FlatAppearance.BorderSize = 0;
                btnPrintPreview.Click += (s, e) => _browser.ShowPrintPreviewDialog();
                btnPrint.Click += (s, e) => _browser.ShowPrintDialog();
                toolbar.Controls.AddRange(new Control[] { btnPrintPreview, btnPrint });

                _browser.Dock = DockStyle.Fill;
                _browser.DocumentText = html;

                Controls.Add(_browser);
                Controls.Add(toolbar);
            }
        }

        private sealed class ComboItem<T>
        {
            public ComboItem(T value, string text)
            {
                Value = value;
                Text = text;
            }

            public T Value { get; }
            public string Text { get; }

            public override string ToString() => Text;
        }
    }
}


