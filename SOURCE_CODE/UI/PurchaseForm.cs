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
        private readonly PaymentService _paymentSvc = new PaymentService();
        private readonly VendorAdvancePaymentService _vendorAdvanceSvc = new VendorAdvancePaymentService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly ToolTip _toolTip = new ToolTip();

        private List<StockItem> _inventoryItems = new List<StockItem>();
        private List<Vendor> _vendors = new List<Vendor>();
        private List<AMCContract> _contracts = new List<AMCContract>();
        private List<Job> _jobs = new List<Job>();
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
        private Label _lblListPager;

        private ComboBox _cboVendor;
        private TextBox _txtVendorGstin;
        private TextBox _txtPONumber;
        private DateTimePicker _dtpDate;
        private DateTimePicker _dtpPayByDate;
        private TextBox _txtVendorInvoiceNumber;
        private ComboBox _cboStatus;
        private ComboBox _cboLinkedType;
        private ComboBox _cboLinkedRecord;
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
        private PurchaseListTab _activeTab = PurchaseListTab.AllPurchaseOrders;
        private PurchaseViewMode _viewMode = PurchaseViewMode.Orders;
        private const int MaxRenderedOrderCards = 120;
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

        public PurchaseForm()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 247, 250);
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPurchaseReferenceSkin(Controls);
            ApplyPermissions();
            EnableDeferredLoad(
                LoadInitialDataAsync,
                ex => SetStatus("Load error: " + ex.Message, Color.Red));
        }

        private void BuildLayout()
        {
            BackColor = DS.BgPage;
            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = DS.BgPage, Padding = new Padding(22, 12, 22, 10) };
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
            _lblHeaderStatus = MakeStatusBadge("Draft", SaveGreen, new Point(282, 40), 62);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(_lblHeaderStatus);

            FlowLayoutPanel toolbar = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Height = 40,
                BackColor = Color.Transparent
            };
            Button btnRefresh = MakeOutlineButton("Refresh", 78);
            _btnNewPo = MakeBtn("+  New PO", InfoBlue, 104);
            _btnSavePo = MakeOutlineButton("Save as Draft", 118);
            Button btnReceived = MakeOutlineButton("Mark Received", 118);
            _btnPreview = MakeOutlineButton("Preview", 80);
            _btnPrintPdf = MakeOutlineButton("Print PDF", 88);
            _btnSend = MakeOutlineButton("Send", 70);
            _btnConvertToBill = MakeOutlineButton("Convert to Bill", 112);
            _btnViewReceipt = MakeOutlineButton("View Receipt", 102);
            _btnPayablesToggle = MakeOutlineButton("Vendor Payables", 124);
            _btnBatchPay = MakeBtn("Batch Pay", SaveGreen, 92);
            _btnVendorAdvance = MakeOutlineButton("Advance", 82);
            Button btnImport = MakeOutlineButton("Import", 78);
            Button btnTemplate = MakeOutlineButton("Template", 88);
            Button btnSettings = MakeOutlineButton("...", 38);
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
            btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Purchases, FindForm());
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Purchases, FindForm());
            btnSettings.Click += (s, e) => ShowPurchaseMoreMenu();
            toolbar.Controls.AddRange(new Control[] { btnRefresh, _btnPayablesToggle, btnImport, btnTemplate, _btnPreview, _btnPrintPdf, _btnNewPo, btnSettings, _lblStatus });
            header.Controls.Add(toolbar);
            header.Resize += (s, e) =>
            {
                toolbar.Location = new Point(Math.Max(420, header.Width - toolbar.Width - 24), 18);
                LayoutHeaderStatusBadge();
            };
            toolbar.Location = new Point(Math.Max(420, header.Width - toolbar.Width - 24), 18);
            LayoutHeaderStatusBadge();

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(18, 0, 18, 18) };
            _leftRail = new Panel { Dock = DockStyle.Left, Width = 304, MinimumSize = new Size(304, 0), BackColor = DS.BgPage, Padding = new Padding(0, 0, 12, 0) };
            _leftRail.Controls.Add(BuildLeftRailContent());
            Splitter leftSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 6,
                MinSize = 304,
                MinExtra = 620,
                BackColor = DS.Slate200
            };
            leftSplitter.SplitterMoved += (s, e) => ApplyFiltersAndRender();
            Panel rightRail = new Panel { Dock = DockStyle.Right, Width = 300, BackColor = DS.BgPage, Padding = new Padding(12, 0, 0, 0) };
            rightRail.Controls.Add(BuildRightPanel());
            Panel detailHost = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };
            _detail = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage };
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

        private Control BuildLeftRailContent()
        {
            Panel wrap = CreateCardPanel();
            wrap.Dock = DockStyle.Fill;
            wrap.Padding = new Padding(14);

            Panel top = new Panel { Dock = DockStyle.Top, Height = 144, BackColor = Color.White };
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
            _btnAllTab.Width = 86;
            _btnPendingTab.Width = 92;
            _btnPartialTab.Width = 82;
            _btnApprovedTab.Width = 96;
            _btnClosedTab.Width = 82;
            _btnAllTab.Location = new Point(0, 34);
            _btnPendingTab.Location = new Point(90, 34);
            _btnPartialTab.Location = new Point(186, 34);
            _btnApprovedTab.Location = new Point(0, 66);
            _btnClosedTab.Location = new Point(100, 66);
            _btnAllTab.Click += (s, e) => { _activeTab = PurchaseListTab.AllPurchaseOrders; ApplyFiltersAndRender(); };
            _btnPendingTab.Click += (s, e) => { _activeTab = PurchaseListTab.PendingOrders; ApplyFiltersAndRender(); };
            _btnPartialTab.Click += (s, e) => { _activeTab = PurchaseListTab.PartialOrders; ApplyFiltersAndRender(); };
            _btnApprovedTab.Click += (s, e) => { _activeTab = PurchaseListTab.ApprovedOrders; ApplyFiltersAndRender(); };
            _btnClosedTab.Click += (s, e) => { _activeTab = PurchaseListTab.ClosedOrders; ApplyFiltersAndRender(); };

            _txtSearch = new TextBox
            {
                Location = new Point(0, 108),
                Width = 202,
                BorderStyle = BorderStyle.FixedSingle,
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
            _txtSearch.TextChanged += (s, e) => ApplyFiltersAndRender();

            _cboListStatusFilter = new ComboBox
            {
                Location = new Point(210, 108),
                Width = 70,
                Font = new Font("Segoe UI", 9)
            };
            ConfigureDropDownListCombo(_cboListStatusFilter);
            _cboListStatusFilter.Items.AddRange(new object[] { "All", "Pending", "Partial", "Approved", "Closed", "Received", "Paid", "Overdue", "Due Soon" });
            _cboListStatusFilter.SelectedIndex = 0;
            _cboListStatusFilter.SelectedIndexChanged += (s, e) => ApplyFiltersAndRender();

            top.Controls.AddRange(new Control[] { _btnAllTab, _btnPendingTab, _btnPartialTab, _btnApprovedTab, _btnClosedTab, _txtSearch, _cboListStatusFilter });
            top.Resize += (s, e) =>
            {
                int filterWidth = 70;
                _txtSearch.Width = Math.Max(150, top.ClientSize.Width - filterWidth - 12);
                _cboListStatusFilter.Location = new Point(_txtSearch.Right + 8, 108);
                _cboListStatusFilter.Width = filterWidth;
            };

            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _leftListFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
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

            Panel pager = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.White };
            Button prev = MakeOutlineButton("<", 32);
            Button p1 = MakeBtn("1", InfoBlue, 32);
            Button p2 = MakeOutlineButton("2", 32);
            Button next = MakeOutlineButton(">", 32);
            ToolTip pagerTips = new ToolTip();
            prev.Enabled = false;
            p1.Enabled = false;
            p2.Enabled = false;
            next.Enabled = false;
            pagerTips.SetToolTip(prev, "Purchase list pagination is disabled until more than one page is available.");
            pagerTips.SetToolTip(p1, "Current purchase list page.");
            pagerTips.SetToolTip(p2, "Purchase list pagination is disabled until more than one page is available.");
            pagerTips.SetToolTip(next, "Purchase list pagination is disabled until more than one page is available.");
            prev.Location = new Point(0, 5);
            p1.Location = new Point(38, 5);
            p2.Location = new Point(76, 5);
            next.Location = new Point(114, 5);
            _lblListPager = new Label { Text = "0-0 of 0", Location = new Point(156, 11), Width = 116, Height = 18, Font = new Font("Segoe UI", 8), ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleRight };
            pager.Controls.AddRange(new Control[] { prev, p1, p2, next, _lblListPager });

            wrap.Controls.Add(scroll);
            wrap.Controls.Add(pager);
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
                AutoScroll = true,
                BackColor = DS.BgPage
            };

            Panel summary = CreateCardPanel();
            summary.Width = 288;
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
            actions.Width = 288;
            actions.Height = 336;
            actions.Margin = new Padding(0, 0, 0, 14);
            actions.Controls.Add(new Label { Text = "Actions", Location = new Point(16, 16), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = DS.Slate900 });
            Button save = MakePanelActionButton("Save PO", SaveGreen, 48);
            Button convert = MakePanelActionButton("Convert to Bill", SaveGreen, 84);
            Button received = MakePanelActionButton("Mark as Received", SaveGreen, 120);
            Button send = MakePanelActionButton("Send to Vendor Email", InfoBlue, 156);
            Button print = MakePanelActionButton("Print PO", InfoBlue, 192);
            Button clone = MakePanelActionButton("Clone PO", InfoBlue, 228);
            Button cancel = MakePanelActionButton("Cancel PO", DelRed, 264);
            Button delete = MakePanelActionButton("Delete PO", DelRed, 300);
            save.Click += (s, e) => Save();
            convert.Click += (s, e) => ConvertToBill();
            received.Click += (s, e) => MarkReceived();
            send.Click += (s, e) => SendPurchaseOrder();
            print.Click += (s, e) => SavePurchaseOrderPdf();
            clone.Click += (s, e) => ClonePurchaseOrder();
            cancel.Click += (s, e) => CancelPurchaseOrder();
            delete.Enabled = false;
            _toolTip.SetToolTip(delete, "Hard delete is disabled to protect purchasing history. Use Cancel PO to close this order safely.");
            actions.Controls.AddRange(new Control[] { save, convert, received, send, print, clone, cancel, delete });
            panel.Controls.Add(actions);
            return panel;
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
            Button button = MakeOutlineButton(text, 252);
            button.Location = new Point(16, y);
            button.ForeColor = text.StartsWith("Cancel", StringComparison.OrdinalIgnoreCase) ? DelRed : DS.Slate900;
            button.FlatAppearance.BorderColor = Color.FromArgb(220, 226, 235);
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

            FlowLayoutPanel workspace = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
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
            _txtVendorGstin = new TextBox { Width = 150, Font = new Font("Segoe UI", 9), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(248, 250, 252) };
            _txtPONumber = new TextBox { Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            _txtVendorInvoiceNumber = new TextBox { Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            _dtpDate = new DateTimePicker { Width = 150, Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            _dtpDate.ValueChanged += (s, e) => RefreshPayByDate();
            _dtpPayByDate = new DateTimePicker { Width = 150, Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            _cboStatus = new ComboBox { Width = 150, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(_cboStatus);
            _cboStatus.Items.AddRange(new object[] { "Pending", "Approved", "Partial", "Received", "Paid", "Closed", "Cancelled" });
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
            TextBox txtCreatedBy = new TextBox { Text = "Administrator", Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252) };
            TextBox txtCreatedOn = new TextBox { Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), Width = 150, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252) };

            headerGrid.Controls.Add(BuildFieldCell("PO Number *", _txtPONumber, 104, 150), 0, 0);
            headerGrid.Controls.Add(BuildFieldCell("PO Date *", _dtpDate, 104, 150), 1, 0);
            headerGrid.Controls.Add(BuildFieldCell("Vendor GSTIN", _txtVendorGstin, 104, 150), 2, 0);
            headerGrid.Controls.Add(BuildFieldCell("Vendor *", _cboVendor, 104, 150), 0, 1);
            headerGrid.Controls.Add(BuildFieldCell("Required By *", _dtpPayByDate, 104, 150), 1, 1);
            headerGrid.Controls.Add(BuildFieldCell("Vendor Invoice #", _txtVendorInvoiceNumber, 104, 150), 2, 1);
            headerGrid.Controls.Add(BuildFieldCell("Vendor Contact", txtVendorContact, 104, 150), 0, 2);
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
            _txtDeliveryAddress = new TextBox { Location = new Point(128, 8), Width = 676, Height = 34, Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252), BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9) };
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
            _btnInlineViewReceipt = MakeBtn("View", Color.FromArgb(226, 232, 240), 62);
            _btnInlineViewReceipt.Location = new Point(676, 8);
            _btnInlineViewReceipt.ForeColor = Color.FromArgb(51, 65, 85);
            _btnInlineViewReceipt.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _btnInlineViewReceipt.FlatAppearance.BorderSize = 1;
            _btnInlineViewReceipt.Click += (s, e) => ViewReceipt();
            _btnDeleteReceipt = MakeBtn("Delete", Color.FromArgb(254, 226, 226), 62);
            _btnDeleteReceipt.Location = new Point(744, 8);
            _btnDeleteReceipt.ForeColor = DelRed;
            _btnDeleteReceipt.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
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

            Panel otherExtraPanel = BuildTabInfoPanel("Warranty", "Standard vendor warranty", "Vendor quotation reference", "Vendor quote / RFQ reference pending", "Internal remarks", "Approval required only for high-value or variance-flagged purchases.");
            otherExtraPanel.Location = new Point(0, 1088);
            otherExtraPanel.Visible = true;
            fields.Controls.Add(otherExtraPanel);
            _otherDetailsControls.Add(otherExtraPanel);

            Panel attachmentExtraPanel = BuildTabInfoPanel("Vendor quote", "Attach and view vendor quotations", "Delivery challan", "Attach packing slip, challan, receipt, or site photo", "Attachment actions", "View, download, and delete use the receipt/document controls until a document backend is added.");
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
            Button btnImportItems = MakeOutlineButton("Import Items", 106);
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
            btnImportItems.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Purchases, FindForm());
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

            Panel lineScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(0, 0, 0, 0) };
            lineScroll.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
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

            Panel activity = CreateCardPanel();
            activity.Width = 860;
            activity.Height = 116;
            activity.Margin = new Padding(0, 0, 0, 12);
            activity.Controls.Add(new Label { Text = "Activity & History", Location = new Point(16, 14), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = DS.Slate900 });
            activity.Controls.Add(new Label { Text = "No purchase order activity to show.", Location = new Point(30, 44), AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700 });

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
            workspace.Controls.Add(activity);
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
            FlowLayoutPanel workspace = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = DS.BgPage,
                Padding = new Padding(0)
            };
            _detail.Controls.Add(workspace);

            CreatePurchaseEditorControls();

            Panel vendorCard = CreateReferenceSection("1", "PO & Vendor Details", 860, 250);
            Button selectVendor = MakeOutlineButton("Select Vendor", 126);
            selectVendor.Location = new Point(700, 18);
            selectVendor.ForeColor = InfoBlue;
            selectVendor.Click += (s, e) => _cboVendor.Focus();
            vendorCard.Controls.Add(selectVendor);
            AddRefField(vendorCard, "PO Number", _txtPONumber, 18, 58, 255);
            AddRefField(vendorCard, "PO Date *", _dtpDate, 300, 58, 255);
            AddRefField(vendorCard, "Required By", _dtpPayByDate, 582, 58, 255);
            AddRefField(vendorCard, "Vendor *", _cboVendor, 18, 116, 255);
            AddRefField(vendorCard, "Vendor GSTIN", _txtVendorGstin, 300, 116, 255);
            AddRefField(vendorCard, "Vendor Invoice #", _txtVendorInvoiceNumber, 582, 116, 255);
            AddRefField(vendorCard, "Vendor Contact", CreateReadonlyTextBox("Select contact"), 18, 174, 255);
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
            AddRefField(infoCard, "Project / Site", CreateReadonlyTextBox("Select project / site"), 18, 166, 245);
            AddRefField(infoCard, "Created By", CreateReadonlyTextBox("Administrator"), 300, 166, 200);
            _lblCreatedByMeta = new Label { Location = new Point(522, 168), Size = new Size(315, 26), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700, Text = "Created On   " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") };
            infoCard.Controls.Add(_lblCreatedByMeta);

            Panel deliveryCard = CreateReferenceSection("3", "Delivery & Notes", 860, 186);
            Panel modePanel = new Panel { Location = new Point(18, 58), Size = new Size(240, 42), BackColor = Color.White };
            modePanel.Controls.Add(new Label { Text = "Delivery Mode", Location = new Point(0, 0), Size = new Size(150, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = DS.Slate700 });
            _rbTechPickup.Location = new Point(0, 20);
            _rbSiteDelivery.Location = new Point(112, 20);
            modePanel.Controls.Add(_rbTechPickup);
            modePanel.Controls.Add(_rbSiteDelivery);
            deliveryCard.Controls.Add(modePanel);
            AddRefField(deliveryCard, "Assigned Technician", _cboTechnician, 300, 58, 190);
            AddRefField(deliveryCard, "Expected Delivery Date", CreateReadonlyTextBox(DateTime.Today.AddDays(15).ToString("dd/MM/yyyy")), 18, 116, 170);
            _txtDeliveryAddress.Location = new Point(252, 132);
            AddRefField(deliveryCard, "Address", _txtDeliveryAddress, 235, 116, 315);
            AddRefField(deliveryCard, "Notes", _txtNotes, 570, 116, 267);

            _picReceipt = new PictureBox { Visible = false };
            _lblReceiptFile = new Label { Visible = false };
            _btnAttachReceipt = MakeOutlineButton("Attach Photo/File", 132);
            _btnInlineViewReceipt = MakeOutlineButton("View", 62);
            _btnDeleteReceipt = MakeOutlineButton("Delete", 62);

            Panel itemsCard = CreateCardPanel();
            itemsCard.Width = 860;
            itemsCard.Height = 282;
            itemsCard.Margin = new Padding(0, 0, 0, 12);
            itemsCard.Padding = new Padding(14);
            itemsCard.Controls.Add(MakeStepHeader("4", "Items", new Point(18, 16)));
            Panel itemActions = new Panel { Location = new Point(92, 12), Size = new Size(745, 38), BackColor = Color.White };
            Button btnAddLine = MakeBtn("+  Add Item", InfoBlue, 92);
            Button btnAddRfq = MakeOutlineButton("Add from RFQ", 108);
            Button btnImportItems = MakeOutlineButton("Import Items", 106);
            Button btnDiscount = MakeOutlineButton("Apply Discount", 122);
            btnAddLine.Location = new Point(0, 2);
            btnAddRfq.Location = new Point(106, 2);
            btnImportItems.Location = new Point(230, 2);
            btnDiscount.Location = new Point(600, 2);
            btnAddLine.Click += (s, e) => AddLineItemCard();
            btnAddRfq.Click += (s, e) => AddFromRfq();
            btnImportItems.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Purchases, FindForm());
            btnDiscount.Click += (s, e) => ApplyDiscountToLines();
            itemActions.Controls.AddRange(new Control[] { btnAddLine, btnAddRfq, btnImportItems, btnDiscount });
            itemsCard.Controls.Add(itemActions);
            _lblVarianceWarning = new Label { Visible = false };
            _lineItemHeader = BuildLineItemHeader();
            _lineItemHeader.Location = new Point(14, 58);
            _lineItemHeader.Width = 832;
            Panel lineHost = new Panel { Location = new Point(14, 90), Size = new Size(832, 130), BackColor = Color.White, AutoScroll = true };
            lineHost.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, lineHost.Width - 1, lineHost.Height - 1);
            };
            _lineItemFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Padding = new Padding(0) };
            _lblLineItemEmptyState = new Label { Dock = DockStyle.Fill, Text = "No line items added yet.", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.White };
            lineHost.Controls.Add(_lblLineItemEmptyState);
            lineHost.Controls.Add(_lineItemFlow);
            _lblLineItemEmptyState.BringToFront();
            Panel totalBar = new Panel { Location = new Point(14, 226), Size = new Size(832, 42), BackColor = Color.FromArgb(248, 250, 252) };
            _lblLineItemCount = new Label { Text = "Showing 1 item", Location = new Point(12, 10), Size = new Size(160, 22), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate600 };
            _lblTotal = new Label { Text = "Sub Total:  ₹0.00        Discount:  ₹0.00        GST:  ₹0.00        Other Charges:  ₹0.00        Total:  ₹0.00", Location = new Point(180, 10), Size = new Size(640, 22), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = InfoBlue, TextAlign = ContentAlignment.MiddleRight };
            totalBar.Controls.Add(_lblLineItemCount);
            totalBar.Controls.Add(_lblTotal);
            itemsCard.Controls.Add(totalBar);
            itemsCard.Controls.Add(lineHost);
            itemsCard.Controls.Add(_lineItemHeader);

            workspace.Controls.Add(vendorCard);
            workspace.Controls.Add(infoCard);
            workspace.Controls.Add(deliveryCard);
            workspace.Controls.Add(itemsCard);

            _detail.Resize += (s, e) =>
            {
                int width = Math.Max(760, _detail.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
                foreach (Control control in workspace.Controls)
                    control.Width = width;
                if (itemsCard.Width > 120)
                {
                    itemActions.Width = Math.Max(400, itemsCard.Width - 110);
                    btnDiscount.Left = Math.Max(440, itemActions.Width - 130);
                    _lineItemHeader.Width = Math.Max(700, itemsCard.Width - 28);
                    lineHost.Width = _lineItemHeader.Width;
                    totalBar.Width = _lineItemHeader.Width;
                    _lblTotal.Width = Math.Max(420, totalBar.Width - 190);
                }
            };
        }

        private void CreatePurchaseEditorControls()
        {
            _cboVendor = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboVendor.SelectedIndexChanged += (s, e) => OnVendorChanged();
            _txtVendorGstin = CreateReadonlyTextBox("Enter GSTIN");
            _txtPONumber = CreateReadonlyTextBox("Auto generated");
            _txtVendorInvoiceNumber = CreateInputTextBox("Enter invoice number");
            _dtpDate = new DateTimePicker { Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today };
            _dtpDate.ValueChanged += (s, e) => RefreshPayByDate();
            _dtpPayByDate = new DateTimePicker { Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddDays(9) };
            _cboStatus = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cboStatus.Items.AddRange(new object[] { "Pending", "Approved", "Partial", "Received", "Paid", "Closed", "Cancelled" });
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
            Label badge = new Label { Text = number, Location = new Point(0, 1), Size = new Size(20, 20), BackColor = InfoBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
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
            return new TextBox { Text = text, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, ForeColor = DS.Slate500 };
        }

        private TextBox CreateReadonlyTextBox(string text)
        {
            return new TextBox { Text = text, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(248, 250, 252), ForeColor = DS.Slate700, ReadOnly = true };
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

            var inventoryTask = Task.Run(() => _invSvc.GetAll());
            var vendorTask = Task.Run(() => _vndSvc.GetAll());
            var contractTask = Task.Run(() => _cntSvc.GetAllContracts());
            var jobTask = Task.Run(() => _jobSvc.GetAll());
            var technicianTask = Task.Run(() => _employeeSvc.GetActiveTechnicians());
            var hsnTask = Task.Run(() => _hsnSvc.GetAll());
            var orderTask = Task.Run(() => _svc.GetAllFresh());

            _vendors = await vendorTask ?? new List<Vendor>();
            PopulateVendorCombo();

            _technicians = await technicianTask ?? new List<Employee>();
            PopulateTechnicianCombo();

            await Task.WhenAll(inventoryTask, contractTask, jobTask, hsnTask, orderTask);

            _inventoryItems = inventoryTask.Result ?? new List<StockItem>();
            _contracts = contractTask.Result ?? new List<AMCContract>();
            _jobs = jobTask.Result ?? new List<Job>();
            _hsnEntries = hsnTask.Result ?? new List<HsnSacMasterEntry>();
            _orderSource = orderTask.Result ?? new List<PurchaseOrder>();

            if (_cboLinkedType != null && _cboLinkedType.Items.Count > 0)
                _cboLinkedType.SelectedIndex = 0;
            PopulateLinkedRecordCombo();
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
                SetStatus("Load error: " + ex.Message, Color.Red);
            }

            AppRuntime.LogTiming("Purchases.LoadList", sw.ElapsedMilliseconds, "orders=" + _orderSource.Count);
        }

        private void ApplyFiltersAndRender()
        {
            UpdateTabButtons();
            _leftListFlow.SuspendLayout();
            _leftListFlow.Controls.Clear();
            _payableChecks.Clear();

            List<PurchaseOrder> filteredOrders = FilterOrders();
            if (_viewMode == PurchaseViewMode.VendorPayables)
                RenderVendorPayables(filteredOrders);
            else
                RenderOrderCards(filteredOrders);

            _leftListFlow.ResumeLayout();
            UpdateBatchPayVisibility();
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
                query = query.Where(p => string.Equals(p.Status, "Approved", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Received", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.PODate);
            else if (_activeTab == PurchaseListTab.ClosedOrders)
                query = query.Where(p => string.Equals(p.Status, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
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
                case "Received":
                case "Paid":
                    query = query.Where(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase));
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

            foreach (PurchaseOrder po in orders.Take(MaxRenderedOrderCards))
                _leftListFlow.Controls.Add(MakeOrderCard(po));

            if (orders.Count == 0)
                _leftListFlow.Controls.Add(MakeEmptyState("No purchases match this filter."));
            else if (orders.Count > MaxRenderedOrderCards)
                _leftListFlow.Controls.Add(MakeEmptyState("Showing first " + MaxRenderedOrderCards + " of " + orders.Count + ". Use search/status filters to narrow the list."));

            SetStatus("Showing " + orders.Count + " purchase orders.", Color.Gray);
            if (_lblListPager != null)
            {
                _lblListPager.Text = orders.Count == 0 ? "0-0 of 0" : "1-" + Math.Min(8, orders.Count) + " of " + orders.Count;
            }
        }

        private void RenderVendorPayables(List<PurchaseOrder> orders)
        {
            List<VendorPayableGroup> payables = orders
                .Where(p => p.BalanceDue > 0.01m)
                .GroupBy(p => new { p.VendorID, VendorName = string.IsNullOrWhiteSpace(p.VendorName) ? "Unknown Vendor" : p.VendorName })
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

            SetStatus("Showing " + payables.Count + " vendor payable groups.", Color.Gray);
        }

        private Control MakeEmptyState(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 372,
                Height = 72,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 0, 0, 10)
            };
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
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
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
                using (Pen pen = new Pen(row == _selectedCard ? InfoBlue : Color.FromArgb(226, 232, 240)))
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
                SetStatus("Load error: " + ex.Message, Color.Red);
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
                    VendorName = string.IsNullOrWhiteSpace(po.VendorName) ? "New Vendor" : po.VendorName,
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
                return 0;

            string linkedType = _cboLinkedType.SelectedItem?.ToString() ?? "General";
            if (string.Equals(linkedType, "WorkOrder", StringComparison.OrdinalIgnoreCase))
                return _jobs.FirstOrDefault(job => job.JobID == linkedId.Value)?.SiteID ?? 0;
            if (string.Equals(linkedType, "Contract", StringComparison.OrdinalIgnoreCase))
                return _contracts.FirstOrDefault(contract => contract.ContractID == linkedId.Value)?.SiteID ?? 0;
            return 0;
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
                && (_current.Status == "Paid" || _current.Status == "Received")
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
                SetStatus("Vendor and PO number are required. Demo fallback vendors cannot be saved.", Color.Red);
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
                SetStatus("Save error: " + ex.Message, Color.Red);
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
                SetStatus("Error: " + ex.Message, Color.Red);
            }
        }

        private void ToggleVendorPayablesView()
        {
            _viewMode = _viewMode == PurchaseViewMode.Orders ? PurchaseViewMode.VendorPayables : PurchaseViewMode.Orders;
            _btnPayablesToggle.Text = _viewMode == PurchaseViewMode.VendorPayables ? "Back to Orders" : "Vendor Payables";
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

            using (Form prompt = new Form())
            {
                prompt.AutoScaleMode = AutoScaleMode.Dpi;
                prompt.Text = "Batch Pay";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.ClientSize = new Size(380, 150);
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

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
                    SetStatus("Batch pay failed: " + ex.Message, Color.Red);
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

            using (Form prompt = new Form())
            {
                prompt.AutoScaleMode = AutoScaleMode.Dpi;
                prompt.Text = "Vendor Advance";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.ClientSize = new Size(420, 250);
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

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
                    new Label { Text = "Vendor", Location = new Point(16, 18), Width = 120 },
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
                    SetStatus("Vendor advance recorded. It will be deducted during final payment.", SaveGreen);
                }
                catch (Exception ex)
                {
                    SetStatus("Advance failed: " + ex.Message, Color.Red);
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
            _btnPayablesToggle.Text = "Vendor Payables";
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
            int approved = source.Count(p => string.Equals(p.Status, "Approved", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Received", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase));
            int closed = source.Count(p => string.Equals(p.Status, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
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
            btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            btn.FlatAppearance.BorderSize = 1;
            return btn;
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
                        VendorName = vendor?.VendorName ?? "Draft Vendor",
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
                MessageBox.Show(ex.Message, "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(ex.Message, "PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SendPurchaseOrder()
        {
            try
            {
                PurchaseOrder po = BuildPreviewPurchaseOrder();
                Vendor vendor = _cboVendor.SelectedItem as Vendor;
                string pdfPath = EnsurePdfForSend(po);

                using (Form dlg = new Form())
                {
                    dlg.AutoScaleMode = AutoScaleMode.Dpi;
                    dlg.Text = "Send Purchase Order";
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.ClientSize = new Size(360, 132);
                    dlg.MaximizeBox = false;
                    dlg.MinimizeBox = false;

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
                MessageBox.Show(ex.Message, "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                SetStatus("Cancel failed: " + ex.Message, Color.Red);
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
                MessageBox.Show("Clone PO could not complete: " + ex.Message, "Clone PO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeletePurchaseOrder()
        {
            if (_current == null)
            {
                SetStatus("Select a PO to delete.", Color.Gray);
                return;
            }

            CancelPurchaseOrder();
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

                using (Form dialog = new Form())
                using (ComboBox cboQuote = new ComboBox())
                using (CheckedListBox lineList = new CheckedListBox())
                using (Button btnAdd = MakeBtn("Add Lines", SaveGreen, 96))
                using (Button btnCancel = MakeBtn("Cancel", DelRed, 86))
                {
                    dialog.AutoScaleMode = AutoScaleMode.Dpi;
                    dialog.Text = "Add from RFQ / Quotation";
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.ClientSize = new Size(620, 390);
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;

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
                SetStatus("RFQ import failed: " + ex.Message, Color.Red);
            }
        }

        private void ApplyDiscountToLines()
        {
            using (Form prompt = new Form())
            {
                prompt.AutoScaleMode = AutoScaleMode.Dpi;
                prompt.Text = "Apply Discount";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.ClientSize = new Size(280, 126);
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
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
            using (Form prompt = new Form())
            {
                prompt.AutoScaleMode = AutoScaleMode.Dpi;
                prompt.Text = "Other Charges";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.ClientSize = new Size(280, 126);
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
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
            string tempHtml = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(tempHtml, html);

            string browserPath = FindPdfBrowser();
            if (string.IsNullOrWhiteSpace(browserPath))
                throw new Exception("Microsoft Edge or Google Chrome is required to generate PDF output.");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = "--headless=new --disable-gpu --print-to-pdf=\"" + pdfPath + "\" \"" + new Uri(tempHtml).AbsoluteUri + "\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit(15000);
                if (!File.Exists(pdfPath))
                    throw new Exception("PDF generation did not complete.");
            }
        }

        private static string FindPdfBrowser()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private void OpenEmail(PurchaseOrder po, Vendor vendor, string pdfPath)
        {
            string subject = "Purchase Order " + (po.PONumber ?? string.Empty) + " from " + BrandingService.AppName;
            string body = "Dear " + (vendor?.VendorName ?? "Vendor") + "," + Environment.NewLine + Environment.NewLine
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
            string message = "Dear " + (vendor?.VendorName ?? "Vendor") + ", please find attached PO "
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
            Panel header = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(0, 2, 0, 0) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, header.Width - 1, header.Height - 1);
            };

            AddHeaderLabel(header, "#", 12, 24, true);
            AddHeaderLabel(header, "Item / Description", 42, 185);
            AddHeaderLabel(header, "Category", 234, 92);
            AddHeaderLabel(header, "HSN/SAC", 334, 70);
            AddHeaderLabel(header, "Qty", 412, 52, true);
            AddHeaderLabel(header, "UOM", 474, 58);
            AddHeaderLabel(header, "Rate (Rs)", 540, 78, true);
            AddHeaderLabel(header, "Disc. %", 626, 56, true);
            AddHeaderLabel(header, "Taxable", 690, 80, true);
            AddHeaderLabel(header, "GST %", 778, 52, true);
            AddHeaderLabel(header, "GST", 838, 76, true);
            AddHeaderLabel(header, "Total", 922, 90, true);
            AddHeaderLabel(header, "Bill To", 1020, 88);
            AddHeaderLabel(header, "Actions", 1116, 90, true);
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

            int rowWidth = 1228;
            if (_lineItemFlow.Parent != null)
                rowWidth = Math.Max(1228, _lineItemFlow.Parent.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
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
                Width = 1228,
                Height = 62,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawLine(pen, 0, card.Height - 1, card.Width, card.Height - 1);
            };

            Label lblRowNo = new Label { Name = "lblRowNo", Text = rowNumber.ToString(), Location = new Point(12, 20), Width = 24, Height = 18, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleRight };
            ComboBox cmbDesc = new ComboBox { Name = "cmbDesc", Location = new Point(42, 16), Width = 185, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbDesc);
            foreach (StockItem item in _inventoryItems)
                cmbDesc.Items.Add(item.ItemName);
            cmbDesc.Tag = null;

            TextBox txtCategory = new TextBox { Name = "txtCategory", Location = new Point(234, 16), Width = 92, Font = new Font("Segoe UI", 9), ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            ComboBox cmbHsn = new ComboBox { Name = "cmbHsn", Location = new Point(334, 16), Width = 70, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbHsn);
            foreach (HsnSacMasterEntry entry in _hsnEntries.Where(h => h.IsActive))
                cmbHsn.Items.Add(entry.Code);
            cmbHsn.SelectedIndex = string.IsNullOrWhiteSpace(line?.HsnSacCode) ? -1 : cmbHsn.Items.IndexOf(line.HsnSacCode);

            NumericUpDown numQty = MakeDecimalBox("numQty", new Point(412, 16), 52, 2, 999999m, line?.Quantity > 0 ? line.Quantity : 1m);
            ComboBox cmbUom = new ComboBox { Name = "cmbUom", Location = new Point(474, 16), Width = 58, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbUom);
            cmbUom.Items.AddRange(new object[] { "Nos", "Mtr", "Kg", "Ltr", "Set", "Box", "Sqm" });
            cmbUom.SelectedItem = string.IsNullOrWhiteSpace(line?.UOM) ? "Nos" : line.UOM;
            NumericUpDown numRate = MakeDecimalBox("numRate", new Point(540, 16), 78, 2, 9999999m, line?.Rate ?? 0m);
            NumericUpDown numDiscount = MakeDecimalBox("numDiscount", new Point(626, 16), 56, 2, 100m, 0m);
            NumericUpDown numGst = MakeDecimalBox("numGst", new Point(778, 16), 52, 2, 100m, line?.GSTRate > 0 ? line.GSTRate : (line?.IGSTRate > 0 ? line.IGSTRate : 18m));
            Label lblTaxable = new Label { Name = "lblTaxable", Text = "Rs 0.00", Location = new Point(690, 20), Width = 80, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), TextAlign = ContentAlignment.MiddleRight };
            Label lblGstAmount = new Label { Name = "lblGstAmount", Text = "Rs 0.00", Location = new Point(838, 20), Width = 76, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), TextAlign = ContentAlignment.MiddleRight };
            NumericUpDown numCgst = MakeDecimalBox("numCgst", new Point(0, 0), 1, 2, 100m, 0m);
            NumericUpDown numSgst = MakeDecimalBox("numSgst", new Point(0, 0), 1, 2, 100m, 0m);
            NumericUpDown numIgst = MakeDecimalBox("numIgst", new Point(0, 0), 1, 2, 100m, numGst.Value);
            numCgst.Visible = false;
            numSgst.Visible = false;
            numIgst.Visible = false;
            Label lblAmt = new Label { Name = "lblAmt", Text = "Rs 0.00", Location = new Point(922, 20), Width = 90, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = InfoBlue, TextAlign = ContentAlignment.MiddleRight };
            ComboBox cmbJobLink = new ComboBox { Name = "cmbJobLink", Location = new Point(1020, 16), Width = 88, Font = new Font("Segoe UI", 9) };
            ConfigureDropDownListCombo(cmbJobLink);
            cmbJobLink.Items.AddRange(new object[] { "General", "Project", "This Job" });
            cmbJobLink.SelectedItem = string.Equals(line?.JobLink, "Job", StringComparison.OrdinalIgnoreCase) ? "This Job" : (string.Equals(line?.JobLink, "Project", StringComparison.OrdinalIgnoreCase) ? "Project" : "General");
            Button btnEdit = new Button { Text = "/", Location = new Point(1120, 15), Width = 34, Height = 30, BackColor = Color.White, ForeColor = InfoBlue, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            Button btnRemove = new Button { Text = "X", Location = new Point(1160, 15), Width = 34, Height = 30, BackColor = Color.White, ForeColor = DelRed, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            btnEdit.FlatAppearance.BorderColor = DS.Slate200;
            btnEdit.Click += (s, e) => cmbDesc.Focus();
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
            RecalcTotal();
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
            if (string.Equals(status, "Received", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
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

        private void SetStatus(string msg, Color color)
        {
            _lblStatus.Text = msg;
            _lblStatus.ForeColor = color;
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
            btn.FlatAppearance.BorderSize = 0;
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
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = DS.Slate200;
            btn.Margin = new Padding(0, 0, 8, 0);
            DS.Rounded(btn, 5);
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
                using (Pen pen = new Pen(Color.FromArgb(203, 213, 225)))
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
            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            if (button.BackColor == Color.White)
            {
                button.ForeColor = DS.Slate900;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            }
            DS.Rounded(button, 5);
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

        private sealed class PurchasePreviewDialog : Form
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

