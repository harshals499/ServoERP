using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace HVAC_Pro_Desktop.UI
{
    public class ReportForm : DeferredPageControl
    {
        private readonly ContractService _contractSvc = new ContractService();
        private readonly InvoiceService _invoiceSvc = new InvoiceService();
        private readonly PurchaseService _purchaseSvc = new PurchaseService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly JobService _jobSvc = new JobService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly VendorAdvancePaymentService _vendorAdvanceSvc = new VendorAdvancePaymentService();
        private readonly PayrollService _payrollSvc = new PayrollService();
        private readonly TenderService _tenderSvc = new TenderService();
        private readonly ServiceDeskService _serviceDeskSvc = new ServiceDeskService();
        private readonly FinancialReportingService _financialSvc = new FinancialReportingService();
        private readonly ProfitabilityWorkbookImportService _profitabilityImportSvc = new ProfitabilityWorkbookImportService();

        private static int? PendingTabIndex;
        private int _currentReportIndex = 9;

        private Label _lblStatus;
        private Label _lblRevenue, _lblRevenueSub, _lblReceivable, _lblReceivableSub, _lblSla, _lblSlaSub;
        private Label _lblMargin, _lblMarginSub, _lblPayroll, _lblPayrollSub, _lblInventory, _lblInventorySub;
        private DataGridView _detailGrid;
        private FlowLayoutPanel _reportLibrary;
        private Panel _dashboardFlow;
        private Panel _surface;
        private Chart _explorerChart;
        private Label _lblChartTitle;
        private Label _lblReportTitle;
        private Label _lblReportSubtitle;
        private Label _lblReportCount;
        private Label _lblSummaryRevenue;
        private Label _lblSummaryCost;
        private Label _lblSummaryProfit;
        private Label _lblSummaryMargin;
        private Label _lblSummaryOutstanding;
        private TextBox _txtLibrarySearch;
        private TextBox _txtResultSearch;
        private ComboBox _cmbFinancialYear;
        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private ComboBox _cmbStatus;
        private ComboBox _cmbExportFormat;
        private ComboBox _cmbGroupBy;
        private CheckBox _chkIncludeIncompleteCosts;
        private Label _lblSavedView;
        private Label _lblSchedule;
        private Button _btnResetExplorer;
        private FlowLayoutPanel _headerActions;
        private readonly HashSet<string> _selectedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Timer _reportScheduleTimer;
        private string _scheduledExportFolder;
        private string _scheduledExportFrequency;
        private TimeSpan _scheduledExportTime;
        private DateTime _lastScheduledExportDate = DateTime.MinValue;
        private DateTime _requestedFinancialYearStart;
        private string _savedClientFilter;
        private string _savedSiteFilter;
        private readonly Dictionary<string, ResizableCard> _dashboardCards = new Dictionary<string, ResizableCard>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TableLayoutPanel> _ownerCardBodies = new Dictionary<string, TableLayoutPanel>(StringComparer.OrdinalIgnoreCase);
        private ResizableCard _dragCard;

        private List<AMCContract> _contracts = new List<AMCContract>();
        private List<Invoice> _invoices = new List<Invoice>();
        private List<PurchaseOrder> _purchases = new List<PurchaseOrder>();
        private List<Job> _jobs = new List<Job>();
        private List<TenderBid> _quotations = new List<TenderBid>();
        private List<Employee> _technicians = new List<Employee>();
        private List<StockItem> _stock = new List<StockItem>();
        private List<ServiceDeskIncident> _serviceTickets = new List<ServiceDeskIncident>();
        private List<VendorAdvancePayment> _vendorAdvances = new List<VendorAdvancePayment>();
        private List<JobProfitabilityRow> _profitabilityRows = new List<JobProfitabilityRow>();
        private List<MonthlyProfitLossRow> _monthlyProfitLoss = new List<MonthlyProfitLossRow>();
        private PayrollDashboardSnapshot _payrollSnapshot = new PayrollDashboardSnapshot();
        private Dictionary<int, string> _clientNames = new Dictionary<int, string>();
        private bool _initialRefreshQueued;
        private bool _refreshing;
        private bool _usingPreviewData;

        private sealed class ExpenseJobChoice
        {
            public int? JobId { get; set; }
            public string Label { get; set; }
            public override string ToString() => Label ?? string.Empty;
        }

        private static readonly string[] ReportNames =
        {
            "Revenue", "Collections", "Contracts", "Jobs", "Technicians", "Materials", "Purchases", "Supplier Advances", "Clients / Sites", "Profitability", "Import Review"
        };
        private static readonly string[] ReportTileLabels =
        {
            "Revenue", "Collect", "AMC", "Jobs", "Techs", "Stock", "POs", "Advances", "Clients", "Profit", "Review"
        };
        private const string PageKey = "ReportsCommandCenter";
        private const string CardOrderPath = @"C:\HVAC_PRO_MSE\CONFIG\reports_card_order.txt";

        private static readonly Color PageBg = DS.BgPage;
        private static readonly Color CardBg = DS.White;
        private static readonly Color Border = DS.Border;
        private static readonly Color TextDark = DS.Slate900;
        private static readonly Color TextMid = DS.Slate500;
        private static readonly Color Blue = DS.Primary600;
        private static readonly Color Green = DS.Green600;
        private static readonly Color Amber = DS.Amber500;
        private static readonly Color Red = DS.Red600;
        private static readonly Color Teal = DS.Teal500;

        public ReportForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            var ctorWatch = System.Diagnostics.Stopwatch.StartNew();
            BuildLayout();
            AppRuntime.LogTiming("Reports.BuildLayout", ctorWatch.ElapsedMilliseconds);
            UIHelper.ApplyInputStyles(Controls);
            if (_btnResetExplorer != null)
                UIHelper.ApplyButtonStyle(_btnResetExplorer, ButtonRole.Neutral);
            if (_headerActions != null)
                _headerActions.WrapContents = false;
            RegisterFirstPaintTiming("Reports.FirstPaint", ctorWatch);
            EnableDeferredLoad(
                (Func<Task>)(async () => await RefreshAllAsync()),
                ex =>
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Loading reports", ex);
                    _lblStatus.Text = "Reports could not load. Refresh and try again.";
                    _lblStatus.ForeColor = Red;
                });
            HandleCreated += (s, e) => QueueInitialReportRefresh();
        }

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public static void QueueTechnicianEfficiencyNavigation()
        {
            PendingTabIndex = 4;
        }

        public void ApplyNavigationRequest()
        {
            if (!PendingTabIndex.HasValue)
                return;
            SelectReport(Math.Max(0, Math.Min(ReportNames.Length - 1, PendingTabIndex.Value)));
            PendingTabIndex = null;
        }

        public void LoadProfitabilityPreviewForVisualTest()
        {
            _usingPreviewData = true;
            _currentReportIndex = 9;
            DateTime fyStart = IndiaFinancialYearHelper.GetFinancialYearStart(DateTime.Today);
            _profitabilityRows = new List<JobProfitabilityRow>
            {
                PreviewProfitabilityRow("JOB-26041", "Aarti Industries", "Plant 2", "INV-2026-041", 185000m, 122400m, 25000m, "Complete", fyStart.AddMonths(5)),
                PreviewProfitabilityRow("JOB-26042", "Bluejet", "Ambernath", "INV-2026-042", 78500m, 48500m, 18000m, "Complete", fyStart.AddMonths(6)),
                PreviewProfitabilityRow("JOB-26043", "Tata Consultancy Services", "Pune", "INV-2026-043", 242000m, 151000m, 42000m, "Complete", fyStart.AddMonths(7)),
                PreviewProfitabilityRow("JOB-26044", "Larsen & Toubro", "Powai", "INV-2026-044", 164000m, 0m, 164000m, "Cost incomplete", fyStart.AddMonths(8)),
                PreviewProfitabilityRow("JOB-26045", "Infosys", "Mysuru Campus", "INV-2026-045", 198000m, 127000m, 38000m, "Complete", fyStart.AddMonths(9))
            };
            _monthlyProfitLoss = Enumerable.Range(0, 12).Select(month =>
            {
                decimal revenue = 180000m + (month * 27000m);
                decimal cost = 122000m + (month * 15000m);
                MonthlyProfitLossRow row = new MonthlyProfitLossRow { Month = fyStart.AddMonths(month), Revenue = revenue, DirectCosts = cost };
                FinancialReportingService.ApplyCalculations(row);
                return row;
            }).ToList();
            PopulateExplorerFilters();
            SelectReport(9);
            _lblStatus.Text = "Profitability preview | FY 2026-27";
            _lblStatus.ForeColor = Green;
        }

        private static JobProfitabilityRow PreviewProfitabilityRow(string job, string client, string site, string invoice, decimal revenue, decimal cost, decimal outstanding, string costStatus, DateTime date)
        {
            JobProfitabilityRow row = new JobProfitabilityRow
            {
                JobNumber = job,
                ClientName = client,
                SiteName = site,
                InvoiceNumber = invoice,
                BilledRevenue = revenue,
                ActualDirectCost = cost,
                OutstandingAmount = outstanding,
                CostStatus = costStatus,
                JobStatus = "Completed",
                ReportingDate = date
            };
            FinancialReportingService.ApplyCalculations(row);
            if (!string.IsNullOrWhiteSpace(costStatus)) row.CostStatus = costStatus;
            return row;
        }

        public void ScrollToProfitabilityPreviewForVisualTest()
        {
            // The explorer layout fits the viewport and no longer requires a scroll capture.
        }

        private void BuildLayout()
        {
            Controls.Clear();

            _surface = new Panel
            {
                Name = "ReportsSurface",
                Tag = "NO_CARD_SURFACE",
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = PageBg,
                Padding = new Padding(16)
            };
            Panel header = BuildHeader();
            _surface.Controls.Add(BuildExplorerWorkspace());
            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            header.Dock = DockStyle.Fill;
            shell.Controls.Add(header, 0, 0);
            shell.Controls.Add(_surface, 0, 1);
            Controls.Add(shell);
        }

        private Control BuildExplorerWorkspace()
        {
            TableLayoutPanel workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238f));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252f));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            workspace.Controls.Add(BuildExplorerLibrary(), 0, 0);
            workspace.Controls.Add(BuildExplorerCenter(), 1, 0);
            workspace.Controls.Add(BuildExplorerSetup(), 2, 0);
            return workspace;
        }

        private Control BuildExplorerLibrary()
        {
            Panel panel = ExplorerSurface(new Padding(12));
            panel.Margin = new Padding(0, 0, 10, 0);

            Label title = ExplorerHeading("Report library", 24);
            title.Dock = DockStyle.Top;
            _txtLibrarySearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 34,
                Font = DS.Body,
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = "Search reports"
            };
            _txtLibrarySearch.TextChanged += (s, e) => FilterReportLibrary();
            SetCueBanner(_txtLibrarySearch, "Search reports");

            _reportLibrary = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = CardBg,
                Padding = new Padding(0, 10, 0, 0)
            };
            AddExplorerGroup("FINANCIAL", 9, 0, 1);
            AddExplorerGroup("JOBS & SERVICE", 3, 4);
            AddExplorerGroup("AMC", 2);
            AddExplorerGroup("INVENTORY & PURCHASES", 5, 6, 7);
            AddExplorerGroup("CLIENTS & SITES", 8);
            AddExplorerGroup("DATA QUALITY", 10);
            _reportLibrary.Resize += (s, e) => ResizeExplorerLibraryButtons();

            Panel searchWrap = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = CardBg, Padding = new Padding(0, 6, 0, 6) };
            searchWrap.Controls.Add(_txtLibrarySearch);
            panel.Controls.Add(_reportLibrary);
            panel.Controls.Add(searchWrap);
            panel.Controls.Add(title);
            return panel;
        }

        private Control BuildExplorerCenter()
        {
            TableLayoutPanel center = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Margin = new Padding(0, 0, 10, 0),
                Padding = Padding.Empty,
                ColumnCount = 1,
                RowCount = 5
            };
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 43f));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 57f));

            Panel title = ExplorerSurface(new Padding(14, 8, 14, 6));
            title.Margin = new Padding(0, 0, 0, 8);
            _lblReportTitle = new Label { Dock = DockStyle.Top, Height = 28, Text = "Job profitability", Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = TextDark };
            _lblReportSubtitle = new Label { Dock = DockStyle.Fill, Text = "Analyse job-wise revenue, cost and margin across clients and sites.", Font = DS.Small, ForeColor = TextMid, AutoEllipsis = true };
            title.Controls.Add(_lblReportSubtitle);
            title.Controls.Add(_lblReportTitle);

            Panel filters = BuildExplorerFilters();
            filters.Margin = new Padding(0, 0, 0, 8);
            Panel chart = BuildExplorerChart();
            chart.Margin = new Padding(0, 0, 0, 8);
            Control summary = BuildExplorerSummary();
            summary.Margin = new Padding(0, 0, 0, 8);
            Control grid = BuildExplorerGrid();

            center.Controls.Add(title, 0, 0);
            center.Controls.Add(filters, 0, 1);
            center.Controls.Add(chart, 0, 2);
            center.Controls.Add(summary, 0, 3);
            center.Controls.Add(grid, 0, 4);
            return center;
        }

        private Panel BuildExplorerFilters()
        {
            Panel surface = ExplorerSurface(new Padding(12, 8, 12, 8));
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = CardBg, Margin = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9f));

            _cmbFinancialYear = ExplorerCombo();
            _cmbClient = ExplorerCombo();
            _cmbSite = ExplorerCombo();
            _cmbStatus = ExplorerCombo();
            FillFinancialYears();
            _cmbClient.Items.Add("All clients");
            _cmbSite.Items.Add("All sites");
            _cmbStatus.Items.AddRange(new object[] { "All jobs", "Completed", "In progress", "Open", "Cost incomplete" });
            _cmbClient.SelectedIndex = _cmbSite.SelectedIndex = _cmbStatus.SelectedIndex = 0;

            Button run = MakeButton("Run report", Blue, 112);
            run.Dock = DockStyle.Fill;
            run.Margin = new Padding(6, 17, 0, 0);
            ModernIconSystem.AddButtonIcon(run, ModernIconKind.Analytics);
            run.Click += async (s, e) => await RunExplorerReportAsync();
            Button reset = MakeButton("Defaults", Color.White, 82);
            _btnResetExplorer = reset;
            reset.Dock = DockStyle.Fill;
            reset.Margin = new Padding(6, 17, 0, 0);
            reset.Click += (s, e) => ResetExplorerFilters();

            row.Controls.Add(ExplorerField("Financial year", _cmbFinancialYear), 0, 0);
            row.Controls.Add(ExplorerField("Client", _cmbClient), 1, 0);
            row.Controls.Add(ExplorerField("Site", _cmbSite), 2, 0);
            row.Controls.Add(ExplorerField("Status", _cmbStatus), 3, 0);
            row.Controls.Add(run, 4, 0);
            row.Controls.Add(reset, 5, 0);
            surface.Controls.Add(row);
            return surface;
        }

        private Panel BuildExplorerChart()
        {
            Panel surface = ExplorerSurface(new Padding(12, 42, 12, 8));
            _lblChartTitle = new Label { Text = "Monthly trend", Location = new Point(14, 10), Height = 24, Width = 360, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = TextDark };
            _explorerChart = CreateProfitabilityChart();
            _explorerChart.Location = new Point(12, 42);
            _explorerChart.Size = new Size(720, 240);
            surface.Resize += (s, e) => _explorerChart.Size = new Size(Math.Max(100, surface.ClientSize.Width - 24), Math.Max(80, surface.ClientSize.Height - 50));
            surface.Controls.Add(_explorerChart);
            surface.Controls.Add(_lblChartTitle);
            return surface;
        }

        private Control BuildExplorerSummary()
        {
            TableLayoutPanel summary = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = CardBg, ColumnCount = 5, RowCount = 1, Padding = new Padding(8, 6, 8, 6) };
            summary.Paint += (s, e) => DrawBorder(e.Graphics, summary);
            for (int i = 0; i < 5; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            _lblSummaryRevenue = AddExplorerSummary(summary, 0, "Total revenue", Blue);
            _lblSummaryCost = AddExplorerSummary(summary, 1, "Total direct cost", Amber);
            _lblSummaryProfit = AddExplorerSummary(summary, 2, "Gross profit", Green);
            _lblSummaryMargin = AddExplorerSummary(summary, 3, "Margin %", Green);
            _lblSummaryOutstanding = AddExplorerSummary(summary, 4, "Outstanding", Amber);

            _lblRevenue = _lblSummaryRevenue;
            _lblRevenueSub = new Label();
            _lblReceivable = _lblSummaryOutstanding;
            _lblReceivableSub = new Label();
            _lblMargin = _lblSummaryMargin;
            _lblMarginSub = new Label();
            _lblSla = new Label();
            _lblSlaSub = new Label();
            _lblPayroll = new Label();
            _lblPayrollSub = new Label();
            _lblInventory = new Label();
            _lblInventorySub = new Label();
            return summary;
        }

        private Control BuildExplorerGrid()
        {
            Panel surface = ExplorerSurface(new Padding(12, 42, 12, 8));
            _lblReportCount = new Label { Text = "Report preview", Location = new Point(14, 10), Size = new Size(360, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = TextDark };
            _txtResultSearch = new TextBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(520, 8), Size = new Size(220, 28), Font = DS.Body, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Search report results" };
            surface.Resize += (s, e) => _txtResultSearch.Left = Math.Max(360, surface.ClientSize.Width - _txtResultSearch.Width - 14);
            _txtResultSearch.TextChanged += (s, e) => ApplyGridSearchAndFilters();
            SetCueBanner(_txtResultSearch, "Search results");
            _detailGrid = MakeGrid();
            _detailGrid.Dock = DockStyle.Fill;
            _detailGrid.CellFormatting += DetailGrid_CellFormatting;
            surface.Controls.Add(_detailGrid);
            surface.Controls.Add(_txtResultSearch);
            surface.Controls.Add(_lblReportCount);
            return surface;
        }

        private Control BuildExplorerSetup()
        {
            Panel panel = ExplorerSurface(new Padding(14));
            panel.Margin = Padding.Empty;
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = CardBg, Padding = Padding.Empty };
            flow.Controls.Add(ExplorerHeading("Report setup", 34));

            _cmbExportFormat = ExplorerCombo(214);
            _cmbExportFormat.Items.AddRange(new object[] { "Excel (.xlsx)", "CSV (.csv)" });
            _cmbExportFormat.SelectedIndex = 0;
            flow.Controls.Add(ExplorerField("Format", _cmbExportFormat, 214));

            _cmbGroupBy = ExplorerCombo(214);
            _cmbGroupBy.Items.AddRange(new object[] { "Client", "Site", "Status", "No grouping" });
            _cmbGroupBy.SelectedIndex = 0;
            _cmbGroupBy.SelectedIndexChanged += (s, e) => ApplyGridGrouping();
            flow.Controls.Add(ExplorerField("Group by", _cmbGroupBy, 214));

            Button columns = MakeButton("Choose columns...", Color.White, 214);
            columns.Margin = new Padding(0, 12, 0, 6);
            columns.Click += (s, e) => ChooseVisibleColumns();
            flow.Controls.Add(columns);

            _chkIncludeIncompleteCosts = new CheckBox { Text = "Include incomplete costs", Width = 214, Height = 44, Font = DS.Body, ForeColor = TextDark, Checked = true, Padding = new Padding(2, 8, 0, 0) };
            _chkIncludeIncompleteCosts.CheckedChanged += (s, e) => ApplyGridSearchAndFilters();
            flow.Controls.Add(_chkIncludeIncompleteCosts);
            flow.Controls.Add(ExplorerDivider(214));

            flow.Controls.Add(ExplorerHeading("Saved view", 30));
            _lblSavedView = new Label { Text = "No saved view", Width = 214, Height = 34, Font = DS.Small, ForeColor = TextMid, AutoEllipsis = true };
            flow.Controls.Add(_lblSavedView);
            Button saveView = MakeButton("Save current view", Color.White, 214);
            saveView.Click += (s, e) => SaveExplorerView();
            flow.Controls.Add(saveView);
            flow.Controls.Add(ExplorerDivider(214));

            flow.Controls.Add(ExplorerHeading("Schedule", 30));
            _lblSchedule = new Label { Text = "No automatic export scheduled", Width = 214, Height = 46, Font = DS.Small, ForeColor = TextMid, AutoEllipsis = true };
            flow.Controls.Add(_lblSchedule);
            Button schedule = MakeButton("Schedule export...", Color.White, 214);
            schedule.Click += (s, e) => ConfigureExplorerSchedule();
            flow.Controls.Add(schedule);

            Label updated = new Label { Text = "Data refreshes when you run or refresh a report.", Width = 214, Height = 58, Margin = new Padding(0, 24, 0, 0), Font = DS.Small, ForeColor = TextMid };
            flow.Controls.Add(updated);
            panel.Controls.Add(flow);
            _selectedColumns.UnionWith(new[] { "Job", "Client / Site", "Revenue", "Direct Cost", "Gross Profit", "Margin %", "Outstanding" });
            LoadExplorerPreferences();
            return panel;
        }

        private Panel ExplorerSurface(Padding padding)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Padding = padding };
            panel.Paint += (s, e) => DrawBorder(e.Graphics, panel);
            return panel;
        }

        private static Label ExplorerHeading(string text, int height)
        {
            return new Label
            {
                Text = text,
                Width = 214,
                Height = height,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = TextDark,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            };
        }

        private static Control ExplorerDivider(int width)
        {
            Panel divider = new Panel { Width = width, Height = 18, Margin = new Padding(0, 8, 0, 4), BackColor = CardBg };
            divider.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, 0, divider.Height / 2, divider.Width, divider.Height / 2);
            };
            return divider;
        }

        private static ComboBox ExplorerCombo(int width = 150)
        {
            return new ComboBox
            {
                Dock = DockStyle.Bottom,
                Width = width,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = DS.Body,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextDark
            };
        }

        private static Control ExplorerField(string label, Control input, int width = 0)
        {
            Panel field = new Panel
            {
                Dock = width > 0 ? DockStyle.None : DockStyle.Fill,
                Width = width > 0 ? width : input.Width,
                Height = 54,
                BackColor = CardBg,
                Margin = new Padding(0, 0, 6, 0)
            };
            Label caption = new Label { Text = label, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), ForeColor = TextMid };
            input.Dock = DockStyle.Bottom;
            input.Height = 30;
            field.Controls.Add(input);
            field.Controls.Add(caption);
            return field;
        }

        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCueBanner(TextBox box, string text)
        {
            if (box == null) return;
            box.HandleCreated += (s, e) => SendMessage(box.Handle, EmSetCueBanner, new IntPtr(1), text ?? string.Empty);
            if (box.IsHandleCreated)
                SendMessage(box.Handle, EmSetCueBanner, new IntPtr(1), text ?? string.Empty);
        }

        private Label AddExplorerSummary(TableLayoutPanel host, int column, string title, Color color)
        {
            Panel item = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Padding = new Padding(10, 2, 8, 0), Margin = Padding.Empty };
            if (column > 0)
                item.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 4, 0, item.Height - 4);
            Label caption = new Label { Text = title, Dock = DockStyle.Top, Height = 20, Font = DS.Small, ForeColor = TextMid, AutoEllipsis = true };
            Label value = new Label { Text = "-", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = color, AutoEllipsis = true };
            item.Controls.Add(value);
            item.Controls.Add(caption);
            host.Controls.Add(item, column, 0);
            return value;
        }

        private void AddExplorerGroup(string heading, params int[] indexes)
        {
            Label group = new Label
            {
                Text = heading,
                Width = 204,
                Height = 27,
                Font = new Font("Segoe UI", 7.6f, FontStyle.Bold),
                ForeColor = TextMid,
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(5, 0, 0, 3),
                Margin = new Padding(0, 4, 0, 0),
                Tag = "GROUP"
            };
            _reportLibrary.Controls.Add(group);
            foreach (int index in indexes)
                _reportLibrary.Controls.Add(MakeExplorerLibraryButton(index));
        }

        private Button MakeExplorerLibraryButton(int index)
        {
            string label = ExplorerReportLabel(index);
            Button button = new Button
            {
                Tag = index,
                Text = label,
                Width = 204,
                Height = 31,
                Margin = new Padding(0),
                Padding = new Padding(10, 0, 6, 0),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = index == _currentReportIndex ? Color.FromArgb(239, 246, 255) : CardBg,
                ForeColor = index == _currentReportIndex ? Blue : TextDark,
                Font = new Font("Segoe UI", 8.7f, index == _currentReportIndex ? FontStyle.Bold : FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                AccessibleName = "Open " + label + " report"
            };
            button.Click += (s, e) => SelectReport((int)((Control)s).Tag);
            return button;
        }

        private static string ExplorerReportLabel(int index)
        {
            switch (index)
            {
                case 0: return "Revenue summary";
                case 1: return "Receivables ageing";
                case 2: return "AMC contracts";
                case 3: return "Job register";
                case 4: return "Technician productivity";
                case 5: return "Inventory summary";
                case 6: return "Purchase register";
                case 7: return "Supplier advances";
                case 8: return "Clients & sites";
                case 9: return "Job profitability";
                default: return "Import review";
            }
        }

        private static string ExplorerReportDescription(int index)
        {
            switch (index)
            {
                case 0: return "Review recurring and contract revenue by client.";
                case 1: return "Prioritise outstanding invoices and collection follow-up.";
                case 2: return "Monitor AMC value, expiry dates and renewal actions.";
                case 3: return "Review job pipeline, priority, assignment and completion.";
                case 4: return "Compare technician workload, completion and revenue.";
                case 5: return "Identify low stock, reservations and procurement needs.";
                case 6: return "Track purchase orders, balances and supplier status.";
                case 7: return "Review supplier advances, application and remaining balance.";
                case 8: return "Compare client activity, open work and revenue.";
                case 9: return "Analyse job-wise revenue, cost and margin across clients and sites.";
                default: return "Review imported profitability rows before they affect reporting.";
            }
        }

        private void ResizeExplorerLibraryButtons()
        {
            if (_reportLibrary == null) return;
            int width = Math.Max(150, _reportLibrary.ClientSize.Width - _reportLibrary.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2);
            foreach (Control control in _reportLibrary.Controls)
                control.Width = width;
        }

        private void FilterReportLibrary()
        {
            string query = (_txtLibrarySearch.Text ?? string.Empty).Trim();
            foreach (Control control in _reportLibrary.Controls)
            {
                if (control.Tag is int)
                    control.Visible = query.Length == 0 || control.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private void FillFinancialYears()
        {
            DateTime current = IndiaFinancialYearHelper.GetFinancialYearStart(DateTime.Today);
            for (int offset = 0; offset < 4; offset++)
            {
                int start = current.Year - offset;
                _cmbFinancialYear.Items.Add(start + "-" + ((start + 1) % 100).ToString("00"));
            }
            _cmbFinancialYear.SelectedIndex = 0;
        }

        private DateTime SelectedFinancialYearStart()
        {
            string value = Convert.ToString(_cmbFinancialYear.SelectedItem, CultureInfo.InvariantCulture);
            int year;
            if (value != null && value.Length >= 4 && int.TryParse(value.Substring(0, 4), out year))
                return new DateTime(year, 4, 1);
            return IndiaFinancialYearHelper.GetFinancialYearStart(DateTime.Today);
        }

        private Chart CreateProfitabilityChart()
        {
            Chart chart = new Chart { Dock = DockStyle.None, BackColor = CardBg, Palette = ChartColorPalette.None, MinimumSize = new Size(100, 80), Size = new Size(720, 240) };
            ChartArea area = new ChartArea("ProfitabilityTrend") { BackColor = CardBg };
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 7.5f);
            area.AxisX.LabelStyle.ForeColor = TextMid;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(232, 237, 244);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 7.5f);
            area.AxisY.LabelStyle.ForeColor = TextMid;
            area.AxisY.Title = "Amount (Rs)";
            area.AxisY2.Enabled = AxisEnabled.True;
            area.AxisY2.LabelStyle.Font = new Font("Segoe UI", 7.5f);
            area.AxisY2.LabelStyle.ForeColor = TextMid;
            area.AxisY2.Title = "Margin %";
            area.AxisX.LineColor = area.AxisY.LineColor = area.AxisY2.LineColor = Border;
            chart.ChartAreas.Add(area);

            Series revenue = new Series("Revenue") { ChartType = SeriesChartType.Column, Color = Blue, ChartArea = area.Name, IsValueShownAsLabel = false };
            Series cost = new Series("Direct cost") { ChartType = SeriesChartType.Column, Color = Color.FromArgb(249, 115, 22), ChartArea = area.Name, IsValueShownAsLabel = false };
            Series margin = new Series("Margin %") { ChartType = SeriesChartType.Line, Color = Green, BorderWidth = 3, YAxisType = AxisType.Secondary, ChartArea = area.Name, MarkerStyle = MarkerStyle.Circle, MarkerSize = 6 };
            chart.Series.Add(revenue);
            chart.Series.Add(cost);
            chart.Series.Add(margin);
            chart.Legends.Add(new Legend { Docking = Docking.Bottom, Alignment = StringAlignment.Center, Font = new Font("Segoe UI", 8f), BackColor = CardBg });
            return chart;
        }

        private void PopulateExplorerFilters()
        {
            string selectedClient = _savedClientFilter ?? Convert.ToString(_cmbClient.SelectedItem, CultureInfo.InvariantCulture) ?? "All clients";
            string selectedSite = _savedSiteFilter ?? Convert.ToString(_cmbSite.SelectedItem, CultureInfo.InvariantCulture) ?? "All sites";
            IEnumerable<string> clients = _profitabilityRows.Select(r => r.ClientName).Concat(_jobs.Select(j => j.ClientName));
            IEnumerable<string> sites = _profitabilityRows.Select(r => r.SiteName).Concat(_jobs.Select(j => j.SiteName));

            FillExplorerFilter(_cmbClient, "All clients", clients, selectedClient);
            FillExplorerFilter(_cmbSite, "All sites", sites, selectedSite);
            _savedClientFilter = null;
            _savedSiteFilter = null;
        }

        private static void FillExplorerFilter(ComboBox combo, string allLabel, IEnumerable<string> values, string selected)
        {
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(allLabel);
            foreach (string value in values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v))
                combo.Items.Add(value.Trim());
            int index = combo.FindStringExact(selected);
            combo.SelectedIndex = index >= 0 ? index : 0;
            combo.EndUpdate();
        }

        private async Task RunExplorerReportAsync()
        {
            try
            {
                _lblStatus.Text = "Running " + ExplorerReportLabel(_currentReportIndex) + "...";
                _lblStatus.ForeColor = Blue;
                if (_currentReportIndex == 9)
                {
                    DateTime start = SelectedFinancialYearStart();
                    var result = await Task.Run(() => new
                    {
                        Rows = _financialSvc.GetJobProfitability(start, start.AddYears(1).AddDays(-1)) ?? new List<JobProfitabilityRow>(),
                        Trend = _financialSvc.GetMonthlyProfitLoss(start, 12) ?? new List<MonthlyProfitLossRow>()
                    });
                    _profitabilityRows = result.Rows;
                    _monthlyProfitLoss = result.Trend;
                    PopulateExplorerFilters();
                }
                BindDetailGrid();
                _lblStatus.Text = "Updated " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                _lblStatus.ForeColor = Green;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Running Reports explorer", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Running report", ex);
                _lblStatus.Text = "Report could not run. Check the filters and try again.";
                _lblStatus.ForeColor = Red;
            }
        }

        private void ResetExplorerFilters()
        {
            if (_cmbFinancialYear.Items.Count > 0) _cmbFinancialYear.SelectedIndex = 0;
            if (_cmbClient.Items.Count > 0) _cmbClient.SelectedIndex = 0;
            if (_cmbSite.Items.Count > 0) _cmbSite.SelectedIndex = 0;
            if (_cmbStatus.Items.Count > 0) _cmbStatus.SelectedIndex = 0;
            _txtResultSearch.Clear();
            _chkIncludeIncompleteCosts.Checked = true;
            BindDetailGrid();
        }

        private List<JobProfitabilityRow> FilteredProfitabilityRows(bool includeResultSearch)
        {
            IEnumerable<JobProfitabilityRow> query = _profitabilityRows ?? Enumerable.Empty<JobProfitabilityRow>();
            string client = Convert.ToString(_cmbClient.SelectedItem, CultureInfo.InvariantCulture);
            string site = Convert.ToString(_cmbSite.SelectedItem, CultureInfo.InvariantCulture);
            string status = Convert.ToString(_cmbStatus.SelectedItem, CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(client) && !client.StartsWith("All ", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => string.Equals(r.ClientName, client, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(site) && !site.StartsWith("All ", StringComparison.OrdinalIgnoreCase))
                query = query.Where(r => string.Equals(r.SiteName, site, StringComparison.OrdinalIgnoreCase));
            if (!_chkIncludeIncompleteCosts.Checked)
                query = query.Where(r => string.Equals(r.CostStatus, "Complete", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(status) && !status.StartsWith("All ", StringComparison.OrdinalIgnoreCase))
            {
                if (status.IndexOf("cost incomplete", StringComparison.OrdinalIgnoreCase) >= 0)
                    query = query.Where(r => !string.Equals(r.CostStatus, "Complete", StringComparison.OrdinalIgnoreCase));
                else if (status.IndexOf("complete", StringComparison.OrdinalIgnoreCase) >= 0)
                    query = query.Where(r => string.Equals(r.JobStatus, "Completed", StringComparison.OrdinalIgnoreCase) || string.Equals(r.JobStatus, "Closed", StringComparison.OrdinalIgnoreCase));
                else if (status.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0)
                    query = query.Where(r => (r.JobStatus ?? string.Empty).IndexOf("Progress", StringComparison.OrdinalIgnoreCase) >= 0);
                else if (status.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0)
                    query = query.Where(r => !IsComplete(r.JobStatus));
            }

            string search = includeResultSearch ? (_txtResultSearch.Text ?? string.Empty).Trim() : string.Empty;
            if (search.Length > 0)
            {
                query = query.Where(r => new[] { r.JobNumber, r.ClientName, r.SiteName, r.InvoiceNumber, r.JobStatus, r.CostStatus }
                    .Any(v => !string.IsNullOrWhiteSpace(v) && v.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            return query.OrderByDescending(r => r.ReportingDate).ToList();
        }

        private void BindExplorerPresentation()
        {
            if (_explorerChart == null) return;
            _lblChartTitle.Text = "Monthly trend (FY " + Convert.ToString(_cmbFinancialYear.SelectedItem, CultureInfo.InvariantCulture) + ")";
            _explorerChart.Series["Revenue"].Name = "Revenue";
            _explorerChart.Series["Direct cost"].Enabled = true;
            _explorerChart.Series["Margin %"].Enabled = true;
            foreach (Series series in _explorerChart.Series)
                series.Points.Clear();

            IEnumerable<MonthlyProfitLossRow> trend = (_monthlyProfitLoss ?? new List<MonthlyProfitLossRow>()).OrderBy(r => r.Month);
            foreach (MonthlyProfitLossRow row in trend)
            {
                string month = row.Month.ToString("MMM yy", CultureInfo.InvariantCulture);
                _explorerChart.Series["Revenue"].Points.AddXY(month, row.Revenue);
                _explorerChart.Series["Direct cost"].Points.AddXY(month, row.DirectCosts);
                _explorerChart.Series["Margin %"].Points.AddXY(month, row.GrossMarginPercent);
            }

            List<JobProfitabilityRow> rows = FilteredProfitabilityRows(false);
            decimal revenue = rows.Sum(r => r.BilledRevenue);
            decimal cost = rows.Sum(r => r.ActualDirectCost);
            decimal profit = rows.Sum(r => r.GrossProfit);
            decimal outstanding = rows.Sum(r => r.OutstandingAmount);
            decimal margin = revenue == 0m ? 0m : Math.Round(profit / revenue * 100m, 1);
            SetExplorerSummary(_lblSummaryRevenue, "Total revenue", IndiaFormatHelper.FormatCurrency(revenue), Blue);
            SetExplorerSummary(_lblSummaryCost, "Total direct cost", IndiaFormatHelper.FormatCurrency(cost), Amber);
            SetExplorerSummary(_lblSummaryProfit, "Gross profit", IndiaFormatHelper.FormatCurrency(profit), Green);
            SetExplorerSummary(_lblSummaryMargin, "Margin %", margin.ToString("N1", CultureInfo.GetCultureInfo("en-IN")) + "%", Green);
            SetExplorerSummary(_lblSummaryOutstanding, "Outstanding", IndiaFormatHelper.FormatCurrency(outstanding), Amber);
        }

        private void BindGenericExplorerPresentation()
        {
            if (_explorerChart == null || _detailGrid == null) return;
            _lblChartTitle.Text = ExplorerReportLabel(_currentReportIndex) + " overview";
            Series records = _explorerChart.Series[0];
            records.Points.Clear();
            _explorerChart.Series[1].Enabled = false;
            _explorerChart.Series[2].Enabled = false;

            DataGridViewColumn categoryColumn = _detailGrid.Columns.Cast<DataGridViewColumn>().LastOrDefault();
            if (categoryColumn != null)
            {
                var groups = _detailGrid.Rows.Cast<DataGridViewRow>()
                    .Select(r => Convert.ToString(r.Cells[categoryColumn.Index].Value, CultureInfo.CurrentCulture))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .GroupBy(v => v)
                    .OrderByDescending(g => g.Count())
                    .Take(8);
                foreach (var group in groups)
                    records.Points.AddXY(ShortText(group.Key, 14), group.Count());
            }

            int total = _detailGrid.Rows.Count;
            int columns = _detailGrid.Columns.Count;
            SetExplorerSummary(_lblSummaryRevenue, "Total records", total.ToString("N0"), Blue);
            SetExplorerSummary(_lblSummaryCost, "Visible columns", columns.ToString("N0"), Blue);
            SetExplorerSummary(_lblSummaryProfit, "Report", ExplorerReportLabel(_currentReportIndex), Green);
            SetExplorerSummary(_lblSummaryMargin, "Financial year", Convert.ToString(_cmbFinancialYear.SelectedItem, CultureInfo.InvariantCulture), Green);
            SetExplorerSummary(_lblSummaryOutstanding, "Export", "Ready", Amber);
        }

        private static void SetExplorerSummary(Label value, string caption, string text, Color color)
        {
            value.Text = text;
            value.ForeColor = color;
            Label captionLabel = value.Parent == null
                ? null
                : value.Parent.Controls.OfType<Label>().FirstOrDefault(label => !ReferenceEquals(label, value));
            if (captionLabel != null) captionLabel.Text = caption;
        }

        private void ApplyGridSearchAndFilters()
        {
            if (_detailGrid == null) return;
            if (_currentReportIndex == 9)
            {
                _detailGrid.Columns.Clear();
                _detailGrid.Rows.Clear();
                BindProfitabilityDetail();
                BindExplorerPresentation();
                return;
            }

            string search = (_txtResultSearch.Text ?? string.Empty).Trim();
            _detailGrid.CurrentCell = null;
            foreach (DataGridViewRow row in _detailGrid.Rows)
            {
                row.Visible = search.Length == 0 || row.Cells.Cast<DataGridViewCell>()
                    .Any(cell => Convert.ToString(cell.Value, CultureInfo.CurrentCulture).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            UpdateExplorerGridCount();
            ApplySelectedColumns();
            ApplyGridGrouping();
        }

        private void UpdateExplorerGridCount()
        {
            if (_lblReportCount == null || _detailGrid == null) return;
            int visible = _detailGrid.Rows.Cast<DataGridViewRow>().Count(r => r.Visible);
            _lblReportCount.Text = "Report preview (" + visible.ToString("N0") + " records)";
        }

        private void ApplyGridGrouping()
        {
            if (_detailGrid == null || _detailGrid.Columns.Count == 0 || _cmbGroupBy == null) return;
            string group = Convert.ToString(_cmbGroupBy.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty;
            if (group == "No grouping") return;
            DataGridViewColumn column = _detailGrid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.HeaderText.IndexOf(group, StringComparison.OrdinalIgnoreCase) >= 0);
            if (column != null && column.SortMode != DataGridViewColumnSortMode.NotSortable)
            {
                try { _detailGrid.Sort(column, System.ComponentModel.ListSortDirection.Ascending); }
                catch { }
            }
        }

        private void DetailGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string header = _detailGrid.Columns[e.ColumnIndex].HeaderText;
            if (header.IndexOf("Revenue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf("Cost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf("Profit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf("Outstanding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf("Balance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf("Amount", StringComparison.OrdinalIgnoreCase) >= 0)
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            string value = Convert.ToString(e.Value, CultureInfo.CurrentCulture);
            if (value.IndexOf("incomplete", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("overdue", StringComparison.OrdinalIgnoreCase) >= 0)
                e.CellStyle.ForeColor = Red;
            else if (value.Equals("Complete", StringComparison.OrdinalIgnoreCase) || value.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                e.CellStyle.ForeColor = Green;
        }

        private void ChooseVisibleColumns()
        {
            if (_detailGrid.Columns.Count == 0) return;
            using (var dialog = new ExplorerDialog("Choose report columns", new Size(420, 480)))
            {
                CheckedListBox list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, Font = DS.Body, BorderStyle = BorderStyle.None };
                foreach (DataGridViewColumn column in _detailGrid.Columns)
                    list.Items.Add(column.HeaderText, _selectedColumns.Count == 0 || _selectedColumns.Contains(column.HeaderText));
                Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(12), BackColor = PageBg };
                Button apply = MakeButton("Apply", Blue, 96);
                apply.Dock = DockStyle.Right;
                apply.DialogResult = DialogResult.OK;
                Button cancel = MakeButton("Cancel", Color.White, 96);
                cancel.Dock = DockStyle.Right;
                cancel.DialogResult = DialogResult.Cancel;
                footer.Controls.Add(apply);
                footer.Controls.Add(cancel);
                dialog.Controls.Add(list);
                dialog.Controls.Add(footer);
                dialog.AcceptButton = apply;
                dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _selectedColumns.Clear();
                foreach (object item in list.CheckedItems)
                    _selectedColumns.Add(Convert.ToString(item, CultureInfo.InvariantCulture));
                if (_selectedColumns.Count == 0)
                {
                    MessageBox.Show(this, "Select at least one report column.", BrandingService.WindowTitle("Reports"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ApplySelectedColumns();
            }
        }

        private void ApplySelectedColumns()
        {
            if (_detailGrid == null) return;
            foreach (DataGridViewColumn column in _detailGrid.Columns)
                column.Visible = _selectedColumns.Count == 0 || _selectedColumns.Contains(column.HeaderText);
        }

        private static string ExplorerViewPath => Path.Combine(Application.UserAppDataPath, "reports-view.txt");
        private static string ExplorerSchedulePath => Path.Combine(Application.UserAppDataPath, "reports-schedule.txt");

        private void SaveExplorerView()
        {
            try
            {
                Directory.CreateDirectory(Application.UserAppDataPath);
                File.WriteAllLines(ExplorerViewPath, new[]
                {
                    _currentReportIndex.ToString(CultureInfo.InvariantCulture),
                    Convert.ToString(_cmbFinancialYear.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(_cmbClient.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(_cmbSite.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(_cmbStatus.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(_cmbExportFormat.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(_cmbGroupBy.SelectedItem, CultureInfo.InvariantCulture) ?? string.Empty,
                    _chkIncludeIncompleteCosts.Checked.ToString(CultureInfo.InvariantCulture),
                    string.Join("|", _selectedColumns.OrderBy(v => v))
                });
                _lblSavedView.Text = ExplorerReportLabel(_currentReportIndex) + " · " + Convert.ToString(_cmbFinancialYear.SelectedItem, CultureInfo.InvariantCulture);
                _lblStatus.Text = "Report view saved for this Windows user.";
                _lblStatus.ForeColor = Green;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Saving report view", ex);
                MessageBox.Show(this, "ServoERP could not save this report view. Please try again.", BrandingService.WindowTitle("Reports"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadExplorerPreferences()
        {
            try
            {
                if (File.Exists(ExplorerViewPath))
                {
                    string[] values = File.ReadAllLines(ExplorerViewPath);
                    int reportIndex;
                    if (values.Length > 0 && int.TryParse(values[0], out reportIndex))
                        _currentReportIndex = Math.Max(0, Math.Min(ReportNames.Length - 1, reportIndex));
                    SelectComboValue(_cmbFinancialYear, values.Length > 1 ? values[1] : null);
                    _savedClientFilter = values.Length > 2 ? values[2] : null;
                    _savedSiteFilter = values.Length > 3 ? values[3] : null;
                    SelectComboValue(_cmbStatus, values.Length > 4 ? values[4] : null);
                    SelectComboValue(_cmbExportFormat, values.Length > 5 ? values[5] : null);
                    SelectComboValue(_cmbGroupBy, values.Length > 6 ? values[6] : null);
                    bool includeIncomplete;
                    if (values.Length > 7 && bool.TryParse(values[7], out includeIncomplete))
                        _chkIncludeIncompleteCosts.Checked = includeIncomplete;
                    _selectedColumns.Clear();
                    if (values.Length > 8)
                    {
                        foreach (string column in values[8].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                            _selectedColumns.Add(column);
                    }
                    _lblSavedView.Text = ExplorerReportLabel(_currentReportIndex) + " · saved locally";
                }

                if (File.Exists(ExplorerSchedulePath))
                {
                    string[] schedule = File.ReadAllLines(ExplorerSchedulePath);
                    if (schedule.Length >= 3)
                    {
                        _scheduledExportFrequency = schedule[0];
                        TimeSpan.TryParse(schedule[1], CultureInfo.InvariantCulture, out _scheduledExportTime);
                        _scheduledExportFolder = schedule[2];
                        _lblSchedule.Text = _scheduledExportFrequency + " at " + DateTime.Today.Add(_scheduledExportTime).ToString("hh:mm tt") + "\n" + _scheduledExportFolder;
                        StartScheduleTimer();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Loading report preferences", ex);
            }
        }

        private static void SelectComboValue(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrWhiteSpace(value)) return;
            int index = combo.FindStringExact(value);
            if (index >= 0) combo.SelectedIndex = index;
        }

        private void ConfigureExplorerSchedule()
        {
            using (var folderDialog = new FolderBrowserDialog { Description = "Choose where ServoERP should save scheduled report exports", ShowNewFolderButton = true })
            {
                if (!string.IsNullOrWhiteSpace(_scheduledExportFolder) && Directory.Exists(_scheduledExportFolder))
                    folderDialog.SelectedPath = _scheduledExportFolder;
                if (folderDialog.ShowDialog(this) != DialogResult.OK) return;

                using (var dialog = new ExplorerDialog("Schedule report export", new Size(430, 270)))
                {
                    TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18), BackColor = CardBg };
                    body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
                    body.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
                    body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    body.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
                    ComboBox frequency = ExplorerCombo();
                    frequency.Items.AddRange(new object[] { "Daily", "Every Monday", "First day of month" });
                    frequency.SelectedIndex = 0;
                    DateTimePicker time = new DateTimePicker { Dock = DockStyle.Bottom, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = DS.Body, Value = DateTime.Today.AddHours(18) };
                    Label note = new Label { Dock = DockStyle.Fill, Text = "Scheduled exports run while ServoERP is open. The selected report and saved view are used.", Font = DS.Small, ForeColor = TextMid };
                    FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = CardBg };
                    Button save = MakeButton("Save schedule", Blue, 124);
                    save.DialogResult = DialogResult.OK;
                    Button cancel = MakeButton("Cancel", Color.White, 92);
                    cancel.DialogResult = DialogResult.Cancel;
                    actions.Controls.Add(save);
                    actions.Controls.Add(cancel);
                    body.Controls.Add(ExplorerField("Frequency", frequency), 0, 0);
                    body.Controls.Add(ExplorerField("Export time", time), 0, 1);
                    body.Controls.Add(note, 0, 2);
                    body.Controls.Add(actions, 0, 3);
                    dialog.Controls.Add(body);
                    dialog.AcceptButton = save;
                    dialog.CancelButton = cancel;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    _scheduledExportFrequency = Convert.ToString(frequency.SelectedItem, CultureInfo.InvariantCulture);
                    _scheduledExportTime = time.Value.TimeOfDay;
                    _scheduledExportFolder = folderDialog.SelectedPath;
                    Directory.CreateDirectory(Application.UserAppDataPath);
                    File.WriteAllLines(ExplorerSchedulePath, new[] { _scheduledExportFrequency, _scheduledExportTime.ToString("c", CultureInfo.InvariantCulture), _scheduledExportFolder });
                    _lblSchedule.Text = _scheduledExportFrequency + " at " + time.Value.ToString("hh:mm tt") + "\n" + _scheduledExportFolder;
                    SaveExplorerView();
                    StartScheduleTimer();
                    _lblStatus.Text = "Scheduled report export saved.";
                    _lblStatus.ForeColor = Green;
                }
            }
        }

        private void StartScheduleTimer()
        {
            if (_reportScheduleTimer == null)
            {
                _reportScheduleTimer = new Timer { Interval = 60000 };
                _reportScheduleTimer.Tick += (s, e) => RunScheduledExportIfDue();
                Disposed += (s, e) =>
                {
                    if (_reportScheduleTimer != null)
                    {
                        _reportScheduleTimer.Stop();
                        _reportScheduleTimer.Dispose();
                    }
                };
            }
            _reportScheduleTimer.Start();
        }

        private void RunScheduledExportIfDue()
        {
            if (string.IsNullOrWhiteSpace(_scheduledExportFolder) || !Directory.Exists(_scheduledExportFolder)) return;
            DateTime now = DateTime.Now;
            if (_lastScheduledExportDate.Date == now.Date || now.TimeOfDay < _scheduledExportTime) return;
            bool due = string.Equals(_scheduledExportFrequency, "Daily", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(_scheduledExportFrequency, "Every Monday", StringComparison.OrdinalIgnoreCase) && now.DayOfWeek == DayOfWeek.Monday)
                || (string.Equals(_scheduledExportFrequency, "First day of month", StringComparison.OrdinalIgnoreCase) && now.Day == 1);
            if (!due) return;

            try
            {
                string path = BuildScheduledExportPath(_scheduledExportFolder);
                WriteCurrentGridExport(path);
                _lastScheduledExportDate = now.Date;
                _lblStatus.Text = "Scheduled report exported to " + path;
                _lblStatus.ForeColor = Green;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Scheduled report export", ex);
                _lblStatus.Text = "Scheduled report export failed. Review the export folder.";
                _lblStatus.ForeColor = Red;
            }
        }

        private string BuildScheduledExportPath(string folder)
        {
            bool excel = _cmbExportFormat == null || _cmbExportFormat.SelectedIndex <= 0;
            string extension = excel ? ".xlsx" : ".csv";
            string fileName = ExplorerReportLabel(_currentReportIndex).Replace(" ", "_").Replace("&", "and") + "_" + DateTime.Now.ToString("yyyyMMdd-HHmm") + extension;
            return Path.Combine(folder, fileName);
        }

        private Panel BuildHeader()
        {
            Button export = MakeButton("Export", Green, 104);
            Button pnl = MakeButton("Export P&L", Color.White, 116);
            Button refresh = MakeButton("Refresh", Blue, 94);
            Button forms = MakeButton("Service", Color.White, 104);
            Button importProfit = MakeButton("Import Job", Color.White, 112);
            Button addExpense = MakeButton("Add", Color.White, 98);
            ModernIconSystem.AddButtonIcon(export, ModernIconKind.Export);
            ModernIconSystem.AddButtonIcon(pnl, ModernIconKind.Export);
            ModernIconSystem.AddButtonIcon(refresh, ModernIconKind.Refresh);
            ModernIconSystem.AddButtonIcon(forms, ModernIconKind.Document);
            ModernIconSystem.AddButtonIcon(importProfit, ModernIconKind.Import);
            ModernIconSystem.AddButtonIcon(addExpense, ModernIconKind.Money);
            export.Click += (s, e) => ExportCurrentReport();
            pnl.Click += (s, e) => ExportMonthlyProfitLoss();
            refresh.Click += async (s, e) => await RefreshAllAsync();
            forms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Reports", "Reports", null, "service completion report AMC visit report compliance audit job costing sheet export analytics");
            importProfit.Click += (s, e) => ImportProfitabilityWorkbook();
            addExpense.Click += (s, e) => AddExpense();

            _lblStatus = new Label
            {
                Text = "Loading reports...",
                Location = new Point(490, 28),
                Width = 148,
                Height = 22,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Green,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Panel header = new Panel { Name = "ReportsPageHeader", Tag = "custom-header-actions no-global-actions", Dock = DockStyle.Fill, Height = 88, BackColor = PageBg, Padding = Padding.Empty };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Label title = new Label { Text = "Reports Command Center", Location = new Point(10, 9), Size = new Size(286, 30), Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = TextDark, AutoEllipsis = true };
            Label subtitle = new Label { Text = "Real-time insights and analytics across your business.", Location = new Point(10, 42), Size = new Size(286, 20), Font = DS.Small, ForeColor = TextMid, AutoEllipsis = true };
            Control search = SharedPageHeader.CreateSearchCommand("ReportsHeaderSearch", 180, "Search", "Ctrl + K", () => SharedUiPrimitives.OpenGlobalSearch(this));
            search.Location = new Point(302, 22);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 770,
                Height = 72,
                Padding = new Padding(0, 20, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = PageBg
            };
            _headerActions = actions;
            foreach (Control action in new Control[] { refresh, addExpense, importProfit, forms, pnl, export })
            {
                action.Tag = "FIXED_WIDTH";
                action.Margin = new Padding(4, 0, 0, 0);
                actions.Controls.Add(action);
            }
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(search);
            header.Controls.Add(_lblStatus);
            header.Controls.Add(actions);
            actions.BringToFront();
            header.Resize += (s, e) =>
            {
                bool roomy = header.ClientSize.Width >= 1280;
                search.Visible = roomy;
                _lblStatus.Visible = roomy;
                actions.Width = Math.Min(770, Math.Max(560, header.ClientSize.Width - (roomy ? 664 : 310)));
            };
            header.Dock = DockStyle.Top;
            header.Height = 88;
            return header;
        }

        private TableLayoutPanel BuildKpiStrip()
        {
            TableLayoutPanel strip = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 110,
                ColumnCount = 6,
                BackColor = PageBg,
                Padding = new Padding(0, 0, 0, 12)
            };
            for (int i = 0; i < 6; i++)
                strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666f));

            _lblRevenue = AddKpi(strip, 0, "Revenue", Green, out _lblRevenueSub);
            _lblReceivable = AddKpi(strip, 1, "Overdue Receivables", Red, out _lblReceivableSub);
            _lblSla = AddKpi(strip, 2, "SLA Risk", Amber, out _lblSlaSub);
            _lblMargin = AddKpi(strip, 3, "Live Margin", Blue, out _lblMarginSub);
            _lblPayroll = AddKpi(strip, 4, "Tech Load", Teal, out _lblPayrollSub);
            _lblInventory = AddKpi(strip, 5, "Material Plan", Amber, out _lblInventorySub);
            return strip;
        }

        private Panel BuildCommandSection()
        {
            Panel wrapper = new Panel { Dock = DockStyle.Top, Height = 520, BackColor = PageBg, Padding = new Padding(0, 0, 0, 12) };
            _dashboardFlow = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = PageBg,
                AllowDrop = true,
                Padding = new Padding(0)
            };
            _dashboardFlow.DragEnter += DashboardFlow_DragEnter;
            _dashboardFlow.DragDrop += DashboardFlow_DragDrop;
            _dashboardFlow.Resize += (s, e) => LayoutDashboardCards();
            wrapper.Controls.Add(_dashboardFlow);

            _dashboardCards.Clear();
            _ownerCardBodies.Clear();
            AddDashboardCard("business_health", "Today's Business Health", MakeOwnerCardBody("business_health"), 380, 220, "Medium");
            AddDashboardCard("receivables", "Receivables / Pending Collection", MakeOwnerCardBody("receivables"), 380, 220, "Medium");
            AddDashboardCard("sales_pipeline", "Sales Pipeline", MakeOwnerCardBody("sales_pipeline"), 380, 220, "Medium");
            AddDashboardCard("jobs_workload", "Jobs / Service Workload", MakeOwnerCardBody("jobs_workload"), 380, 220, "Medium");
            AddDashboardCard("amc_contracts", "AMC / Contract Health", MakeOwnerCardBody("amc_contracts"), 380, 220, "Medium");
            AddDashboardCard("purchase_payables", "Purchase & Payables", MakeOwnerCardBody("purchase_payables"), 380, 220, "Medium");
            AddDashboardCard("inventory_risk", "Inventory Risk", MakeOwnerCardBody("inventory_risk"), 380, 220, "Medium");
            AddDashboardCard("top_clients", "Top Clients", MakeOwnerCardBody("top_clients"), 380, 220, "Medium");
            AddDashboardCard("top_suppliers", "Top Suppliers", MakeOwnerCardBody("top_suppliers"), 380, 220, "Medium");
            AddDashboardCard("payroll_snapshot", "Payroll Snapshot", MakeOwnerCardBody("payroll_snapshot"), 380, 220, "Medium");
            AddDashboardCard("service_desk", "Service Desk / Complaints", MakeOwnerCardBody("service_desk"), 380, 220, "Medium");
            AddDashboardCard("owner_action_queue", "Owner Action Queue", MakeOwnerCardBody("owner_action_queue"), 380, 452, "Large");
            ApplySavedCardOrder();
            new CardLayoutService().ApplyLayoutToPage(this, PageKey, CardLayoutService.ResolveCurrentUserId());
            LayoutDashboardCards();
            return wrapper;
        }

        private Panel BuildLibrarySection()
        {
            Panel wrapper = new Panel { Dock = DockStyle.Top, Height = 124, BackColor = PageBg, Padding = new Padding(0, 0, 0, 12) };
            Panel card = MakePlainCard("Report Library");
            _reportLibrary = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10, 6, 10, 8),
                BackColor = CardBg
            };
            for (int i = 0; i < ReportNames.Length; i++)
                _reportLibrary.Controls.Add(MakeReportTile(i, ReportTileLabels[i]));
            _reportLibrary.Resize += (s, e) => LayoutReportLibraryTiles();
            Label hint = new Label
            {
                Text = "Pinned owner views: Revenue, Collections, SLA Risk, Materials, Purchases, Payroll, and Client/Site reports.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = DS.Small,
                ForeColor = TextMid,
                TextAlign = ContentAlignment.MiddleLeft
            };
            card.Controls.Add(_reportLibrary);
            card.Controls.Add(hint);
            wrapper.Controls.Add(card);
            LayoutReportLibraryTiles();
            return wrapper;
        }

        private Panel BuildDetailSection()
        {
            Panel wrapper = new Panel { Dock = DockStyle.Top, Height = 310, BackColor = PageBg };
            Panel card = MakePlainCard("Detailed report");
            _detailGrid = MakeGrid();
            _detailGrid.Dock = DockStyle.Fill;
            card.Controls.Add(_detailGrid);
            wrapper.Controls.Add(card);
            return wrapper;
        }

        private async Task RefreshAllAsync()
        {
            if (_refreshing)
                return;

            _refreshing = true;
            try
            {
                _lblStatus.Text = "Refreshing reports...";
                _lblStatus.ForeColor = Blue;
                _requestedFinancialYearStart = SelectedFinancialYearStart();
                var fetchWatch = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(() => LoadData());
                AppRuntime.LogTiming("Reports.FetchData", fetchWatch.ElapsedMilliseconds);
                if (IsDisposed)
                    return;
                var bindWatch = System.Diagnostics.Stopwatch.StartNew();
                PopulateExplorerFilters();
                BindKpis();
                BindOwnerCommandCards();
                SelectReport(_currentReportIndex);
                AppRuntime.LogTiming("Reports.BindData", bindWatch.ElapsedMilliseconds);
                _lblStatus.Text = string.Format(
                    "Reports updated {0} | {1} inv, {2} jobs",
                    DateTime.Now.ToString("dd-MMM HH:mm"),
                    _invoices.Count,
                    _jobs.Count);
                _lblStatus.ForeColor = Green;
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Refreshing report", ex);
                _lblStatus.Text = "Report could not be refreshed. Review the filters and try again.";
                _lblStatus.ForeColor = Red;
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void QueueInitialReportRefresh()
        {
            if (_initialRefreshQueued || IsDisposed || !IsHandleCreated)
                return;

            _initialRefreshQueued = true;
            BeginInvoke((Action)(async () =>
            {
                await RefreshAllAsync();
                if (!IsDisposed && _surface != null)
                    _surface.AutoScrollPosition = Point.Empty;
                MarkDeferredLoadCompleted();
            }));
        }

        private void LoadData()
        {
            TimeSpan ttl = TimeSpan.FromMinutes(2);
            Task<List<AMCContract>> contractsTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("contracts:all", ttl, () => _contractSvc.GetAllContracts() ?? new List<AMCContract>()).ToList(); }
                catch { return new List<AMCContract>(); }
            });
            Task<List<Invoice>> invoicesTask = Task.Run(() =>
            {
                try { return (_invoiceSvc.GetAllInvoices() ?? new List<Invoice>()).ToList(); }
                catch { return new List<Invoice>(); }
            });
            Task<List<PurchaseOrder>> purchasesTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("purchases:all", ttl, () => _purchaseSvc.GetAll() ?? new List<PurchaseOrder>()).ToList(); }
                catch { return new List<PurchaseOrder>(); }
            });
            Task<List<Job>> jobsTask = Task.Run(() =>
            {
                try { return (_jobSvc.GetAll() ?? new List<Job>()).ToList(); }
                catch { return new List<Job>(); }
            });
            Task<List<TenderBid>> quotationsTask = Task.Run(() =>
            {
                try { return (_tenderSvc.GetAll() ?? new List<TenderBid>()).ToList(); }
                catch { return new List<TenderBid>(); }
            });
            Task<List<Employee>> techniciansTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("employees:technicians-active", ttl, () => _employeeSvc.GetActiveTechnicians() ?? new List<Employee>()).ToList(); }
                catch { return new List<Employee>(); }
            });
            Task<List<StockItem>> stockTask = Task.Run(() =>
            {
                try { return (_inventorySvc.GetAll() ?? new List<StockItem>()).ToList(); }
                catch { return new List<StockItem>(); }
            });
            Task<List<VendorAdvancePayment>> advancesTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("vendors:advances", ttl, () => _vendorAdvanceSvc.GetAll() ?? new List<VendorAdvancePayment>()).ToList(); }
                catch { return new List<VendorAdvancePayment>(); }
            });
            Task<List<ServiceDeskIncident>> serviceTicketsTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("service-desk:all", ttl, () => _serviceDeskSvc.GetAll() ?? new List<ServiceDeskIncident>()).ToList(); }
                catch { return new List<ServiceDeskIncident>(); }
            });
            Task<PayrollDashboardSnapshot> payrollTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("payroll:dashboard", ttl, () => _payrollSvc.GetDashboardSnapshot() ?? new PayrollDashboardSnapshot()); }
                catch { return new PayrollDashboardSnapshot(); }
            });
            Task<Dictionary<int, string>> clientNamesTask = Task.Run(() =>
            {
                try { return AppDataCache.GetOrCreate("clients:active", ttl, () => _clientSvc.GetAllClients() ?? new List<B2BClient>()).ToDictionary(c => c.ClientID, c => c.CompanyName); }
                catch { return new Dictionary<int, string>(); }
            });
            DateTime fyStart = _requestedFinancialYearStart == default(DateTime)
                ? IndiaFinancialYearHelper.GetFinancialYearStart(DateTime.Today)
                : _requestedFinancialYearStart;
            Task<List<JobProfitabilityRow>> profitabilityTask = Task.Run(() =>
            {
                try { return _financialSvc.GetJobProfitability(fyStart, fyStart.AddYears(1).AddDays(-1)) ?? new List<JobProfitabilityRow>(); }
                catch { return new List<JobProfitabilityRow>(); }
            });
            Task<List<MonthlyProfitLossRow>> monthlyProfitLossTask = Task.Run(() =>
            {
                try { return _financialSvc.GetMonthlyProfitLoss(fyStart, 12) ?? new List<MonthlyProfitLossRow>(); }
                catch { return new List<MonthlyProfitLossRow>(); }
            });

            Task.WaitAll(contractsTask, invoicesTask, purchasesTask, jobsTask, quotationsTask, techniciansTask, stockTask, advancesTask, serviceTicketsTask, payrollTask, clientNamesTask, profitabilityTask, monthlyProfitLossTask);

            _contracts = contractsTask.Result;
            _invoices = invoicesTask.Result;
            _purchases = purchasesTask.Result;
            _jobs = jobsTask.Result;
            _quotations = quotationsTask.Result;
            _technicians = techniciansTask.Result;
            _stock = stockTask.Result;
            _vendorAdvances = advancesTask.Result;
            _serviceTickets = serviceTicketsTask.Result;
            _payrollSnapshot = payrollTask.Result;
            _clientNames = clientNamesTask.Result;
            _profitabilityRows = profitabilityTask.Result;
            _monthlyProfitLoss = monthlyProfitLossTask.Result;
        }


        private void BindKpis()
        {
            if (_explorerChart != null)
            {
                BindExplorerPresentation();
                return;
            }

            decimal arr = _contracts.Where(c => c.ContractStatus == "Active").Sum(c => c.AnnualValue);
            decimal mrr = _contracts.Where(c => c.ContractStatus == "Active").Sum(c => c.MonthlyValue);
            List<Invoice> openInvoices = _invoices.Where(i => i.PaymentStatus != "Paid").ToList();
            decimal receivables = openInvoices.Sum(i => i.BalanceDue);
            int overdueInvoices = openInvoices.Count(i => i.PaymentStatus == "Overdue" || i.DueDate < DateTime.Today);
            int overdueJobs = _jobs.Count(j => j.IsOverdue || (j.ScheduledDate.Date < DateTime.Today && !IsComplete(j.Status)));
            int urgentJobs = _jobs.Count(j => (j.Priority ?? "").IndexOf("High", StringComparison.OrdinalIgnoreCase) >= 0 && !IsComplete(j.Status));
            decimal jobRevenue = _jobs.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
            decimal jobCost = _jobs.Sum(j => j.EstimatedCost);
            decimal margin = jobRevenue <= 0 ? 0 : Math.Round((jobRevenue - jobCost) / jobRevenue * 100m, 1);
            int openJobs = _jobs.Count(j => !IsComplete(j.Status));
            int procurementRequired = _stock.Count(s => s.AvailableStock <= 0m || s.IsLowStock);

            _lblRevenue.Text = "Rs " + arr.ToString("N0");
            _lblRevenueSub.Text = "MRR Rs " + mrr.ToString("N0");
            _lblReceivable.Text = "Rs " + receivables.ToString("N0");
            _lblReceivableSub.Text = overdueInvoices + " overdue invoices";
            _lblSla.Text = (overdueJobs + urgentJobs).ToString();
            _lblSlaSub.Text = overdueJobs + " overdue, " + urgentJobs + " high priority";
            _lblMargin.Text = margin.ToString("N1") + "%";
            _lblMarginSub.Text = "Revenue Rs " + jobRevenue.ToString("N0");
            _lblPayroll.Text = openJobs.ToString();
            _lblPayrollSub.Text = _technicians.Count + " active technicians";
            _lblInventory.Text = procurementRequired.ToString();
            _lblInventorySub.Text = "procurement required";
        }

        private void BindOwnerCommandCards()
        {
            DateTime today = DateTime.Today;
            decimal revenueMtd = _invoices.Where(i => IsThisMonth(i.InvoiceDate)).Sum(i => i.TotalAmount);
            decimal collectionsMtd = _invoices.Where(i => IsThisMonth(i.InvoiceDate)).Sum(i => i.PaidAmount);
            decimal purchaseMtd = _purchases.Where(p => IsThisMonth(p.PODate)).Sum(p => p.TotalAmount);
            decimal netCashFlow = collectionsMtd - _purchases.Where(p => IsThisMonth(p.PODate)).Sum(p => p.PaidAmount);

            List<Invoice> openInvoices = _invoices.Where(i => !IsPaid(i.PaymentStatus) && i.BalanceDue > 0m).ToList();
            List<Invoice> overdueInvoices = openInvoices.Where(i => IsOverdueInvoice(i)).ToList();
            string topOutstandingClient = openInvoices
                .GroupBy(i => string.IsNullOrWhiteSpace(i.ClientName) ? "Client #" + i.ClientID : i.ClientName)
                .OrderByDescending(g => g.Sum(i => i.BalanceDue))
                .Select(g => ShortText(g.Key, 22))
                .FirstOrDefault() ?? "-";

            List<TenderBid> openQuotes = _quotations.Where(q => IsOpenQuote(q.Status)).ToList();
            int wonQuotes = _quotations.Count(q => IsAny(q.Status, "Won", "Converted", "Approved"));
            int lostQuotes = _quotations.Count(q => IsAny(q.Status, "Lost", "Rejected", "Cancelled"));
            decimal quoteWinRate = wonQuotes + lostQuotes == 0 ? 0m : Math.Round(wonQuotes * 100m / (wonQuotes + lostQuotes), 1);
            decimal quoteMtdValue = _quotations.Where(q => IsThisMonth(q.SubmittedDate ?? q.ModifiedDate ?? q.DueDate)).Sum(QuoteValue);

            int activeJobs = _jobs.Count(j => !IsComplete(j.Status));
            int inProgressJobs = _jobs.Count(j => IsAny(j.Status, "In Progress") || IsAny(j.PipelineStatus, "In Progress"));
            int overdueJobs = _jobs.Count(j => j.IsOverdue || (j.ScheduledDate.Date < today && !IsComplete(j.Status)));
            int jobsDueWeek = _jobs.Count(j => !IsComplete(j.Status) && j.ScheduledDate.Date >= today && j.ScheduledDate.Date <= today.AddDays(7));

            int activeContracts = _contracts.Count(c => IsAny(c.ContractStatus, "Active") && c.EndDate.Date >= today);
            int expiringSoon = _contracts.Count(c => c.EndDate.Date >= today && c.EndDate.Date <= today.AddDays(30));
            int expiredContracts = _contracts.Count(c => IsAny(c.ContractStatus, "Expired") || c.EndDate.Date < today);
            decimal amcAnnualValue = _contracts.Where(c => IsAny(c.ContractStatus, "Active") && c.EndDate.Date >= today).Sum(c => c.AnnualValue);

            int openPos = _purchases.Count(p => IsAny(p.Status, "Draft", "Pending", "Pending Approval", "Approved", "Open", "Partial"));
            decimal pendingPayables = _purchases.Where(p => p.BalanceDue > 0m).Sum(p => p.BalanceDue);
            int overduePayables = _purchases.Count(p => p.IsOverdue);

            int lowStock = _stock.Count(s => s.IsLowStock && s.AvailableStock > 0m);
            int outOfStock = _stock.Count(s => s.AvailableStock <= 0m);
            int procurementRequired = _stock.Count(s => s.AvailableStock <= 0m || s.IsLowStock);
            decimal stockValue = _stock.Sum(s => s.StockValue);

            var topClient = _jobs
                .GroupBy(j => string.IsNullOrWhiteSpace(j.ClientName) ? "Client #" + j.ClientID : j.ClientName)
                .Select(g => new { Name = g.Key, Revenue = g.Sum(JobValue), Open = g.Count(j => !IsComplete(j.Status)) })
                .OrderByDescending(g => g.Revenue)
                .FirstOrDefault();
            int repeatClients = _jobs.GroupBy(j => j.ClientID).Count(g => g.Count() > 1);

            var topSupplier = _purchases
                .GroupBy(p => string.IsNullOrWhiteSpace(p.VendorName) ? "Supplier #" + p.VendorID : p.VendorName)
                .Select(g => new { Name = g.Key, Spend = g.Sum(p => p.TotalAmount), Open = g.Count(p => p.BalanceDue > 0m), Overdue = g.Count(p => p.IsOverdue) })
                .OrderByDescending(g => g.Spend)
                .FirstOrDefault();

            PayrollRun lastRun = _payrollSnapshot == null ? null : _payrollSnapshot.LastRun;
            decimal employerLiability = lastRun == null ? 0m : lastRun.TotalEPFEmployer + lastRun.TotalESIEmployer;
            decimal statutory = lastRun == null ? 0m : lastRun.TotalTDS + lastRun.TotalPT;

            int openTickets = _serviceTickets.Count(t => !IsClosedTicket(t.Status));
            int highTickets = _serviceTickets.Count(t => !IsClosedTicket(t.Status) && IsAny(t.Priority, "High", "Critical"));
            int breachedTickets = _serviceTickets.Count(t => !IsClosedTicket(t.Status) && (t.SlaBreached || (t.SlaDueAt != default(DateTime) && t.SlaDueAt < DateTime.Now)));
            int resolvedToday = _serviceTickets.Count(t => t.ResolvedAt.HasValue && t.ResolvedAt.Value.Date == today);

            SetOwnerCardRows("business_health",
                Metric("Revenue MTD", Money(revenueMtd), Green),
                Metric("Collections MTD", Money(collectionsMtd), Blue),
                Metric("Expenses MTD", Money(purchaseMtd), purchaseMtd > 0 ? Amber : TextMid),
                Metric("Net cash flow", Money(netCashFlow), netCashFlow < 0 ? Red : Green));
            SetOwnerCardRows("receivables",
                Metric("Outstanding", Money(openInvoices.Sum(i => i.BalanceDue)), Red),
                Metric("Overdue invoices", overdueInvoices.Count.ToString("N0"), overdueInvoices.Count > 0 ? Red : Green),
                Metric("Overdue amount", Money(overdueInvoices.Sum(i => i.BalanceDue)), overdueInvoices.Count > 0 ? Red : Green),
                Metric("Top pending client", topOutstandingClient, Blue));
            SetOwnerCardRows("sales_pipeline",
                Metric("Open quotations", openQuotes.Count.ToString("N0"), Blue),
                Metric("Quotation value MTD", Money(quoteMtdValue), Green),
                Metric("Conversion rate", quoteWinRate.ToString("N1") + "%", quoteWinRate >= 40m ? Green : Amber),
                Metric("Pending value", Money(openQuotes.Sum(QuoteValue)), Amber));
            SetOwnerCardRows("jobs_workload",
                Metric("Active jobs", activeJobs.ToString("N0"), Blue),
                Metric("In progress", inProgressJobs.ToString("N0"), Amber),
                Metric("Overdue jobs", overdueJobs.ToString("N0"), overdueJobs > 0 ? Red : Green),
                Metric("Due next 7 days", jobsDueWeek.ToString("N0"), Teal));
            SetOwnerCardRows("amc_contracts",
                Metric("Active AMC", activeContracts.ToString("N0"), Green),
                Metric("Expiring soon", expiringSoon.ToString("N0"), expiringSoon > 0 ? Amber : Green),
                Metric("Expired", expiredContracts.ToString("N0"), expiredContracts > 0 ? Red : Green),
                Metric("Annual AMC value", Money(amcAnnualValue), Blue));
            SetOwnerCardRows("purchase_payables",
                Metric("Open purchase orders", openPos.ToString("N0"), Blue),
                Metric("PO value MTD", Money(purchaseMtd), Green),
                Metric("Pending payables", Money(pendingPayables), pendingPayables > 0 ? Amber : Green),
                Metric("Overdue payables", overduePayables.ToString("N0"), overduePayables > 0 ? Red : Green));
            SetOwnerCardRows("inventory_risk",
                Metric("Low stock items", lowStock.ToString("N0"), lowStock > 0 ? Amber : Green),
                Metric("Out of stock", outOfStock.ToString("N0"), outOfStock > 0 ? Red : Green),
                Metric("Procurement required", procurementRequired.ToString("N0"), procurementRequired > 0 ? Amber : Green),
                Metric("Stock value", Money(stockValue), Blue));
            SetOwnerCardRows("top_clients",
                Metric("Highest revenue client", topClient == null ? "-" : ShortText(topClient.Name, 22), Blue),
                Metric("Client revenue", topClient == null ? Money(0m) : Money(topClient.Revenue), Green),
                Metric("Open jobs for client", topClient == null ? "0" : topClient.Open.ToString("N0"), Amber),
                Metric("Repeat clients", repeatClients.ToString("N0"), Teal));
            SetOwnerCardRows("top_suppliers",
                Metric("Highest spend supplier", topSupplier == null ? "-" : ShortText(topSupplier.Name, 22), Blue),
                Metric("Supplier spend", topSupplier == null ? Money(0m) : Money(topSupplier.Spend), Green),
                Metric("Suppliers with open dues", _purchases.Where(p => p.BalanceDue > 0m).Select(p => p.VendorID).Distinct().Count().ToString("N0"), Amber),
                Metric("Overdue supplier POs", topSupplier == null ? "0" : topSupplier.Overdue.ToString("N0"), topSupplier != null && topSupplier.Overdue > 0 ? Red : Green));
            SetOwnerCardRows("payroll_snapshot",
                Metric("Active technicians", _technicians.Count.ToString("N0"), Blue),
                Metric("Last net payroll", lastRun == null ? "-" : Money(lastRun.TotalNetPay), Green),
                Metric("Employer liability", Money(employerLiability), Amber),
                Metric("TDS / PT", Money(statutory), Teal));
            SetOwnerCardRows("service_desk",
                Metric("Open tickets", openTickets.ToString("N0"), Blue),
                Metric("High priority", highTickets.ToString("N0"), highTickets > 0 ? Red : Green),
                Metric("SLA risk", breachedTickets.ToString("N0"), breachedTickets > 0 ? Red : Green),
                Metric("Resolved today", resolvedToday.ToString("N0"), Green));
            SetOwnerCardRows("owner_action_queue",
                Metric("Collect payment", overdueInvoices.Count + " overdue invoices", overdueInvoices.Count > 0 ? Red : Green, 1),
                Metric("Follow up quotation", openQuotes.Count + " open quotes", openQuotes.Count > 0 ? Amber : Green, 0),
                Metric("Close overdue jobs", overdueJobs + " overdue jobs", overdueJobs > 0 ? Red : Green, 3),
                Metric("Reorder material", procurementRequired + " stock risks", procurementRequired > 0 ? Amber : Green, 5),
                Metric("Renew AMC / pay vendor", expiringSoon + " renewals, " + overduePayables + " overdue POs", expiringSoon + overduePayables > 0 ? Red : Green, 2));
        }

        private void SelectReport(int index)
        {
            int previousIndex = _currentReportIndex;
            _currentReportIndex = Math.Max(0, Math.Min(ReportNames.Length - 1, index));
            foreach (Control tile in _reportLibrary.Controls)
            {
                if (!(tile.Tag is int)) continue;
                int tileIndex = (int)tile.Tag;
                bool selected = tileIndex == _currentReportIndex;
                tile.BackColor = selected ? Color.FromArgb(239, 246, 255) : CardBg;
                tile.ForeColor = selected ? Blue : TextDark;
                tile.Font = new Font("Segoe UI", 8.7f, selected ? FontStyle.Bold : FontStyle.Regular);
            }
            if (_lblReportTitle != null) _lblReportTitle.Text = ExplorerReportLabel(_currentReportIndex);
            if (_lblReportSubtitle != null) _lblReportSubtitle.Text = ExplorerReportDescription(_currentReportIndex);
            if (previousIndex != _currentReportIndex)
            {
                _selectedColumns.Clear();
                if (_currentReportIndex == 9)
                    _selectedColumns.UnionWith(new[] { "Job", "Client / Site", "Revenue", "Direct Cost", "Gross Profit", "Margin %", "Outstanding" });
            }
            BindDetailGrid();
        }

        private void BindDetailGrid()
        {
            _detailGrid.Columns.Clear();
            _detailGrid.Rows.Clear();
            switch (_currentReportIndex)
            {
                case 0: BindRevenueDetail(); break;
                case 1: BindCollectionDetail(); break;
                case 2: BindContractDetail(); break;
                case 3: BindJobDetail(); break;
                case 4: BindTechnicianDetail(); break;
                case 5: BindInventoryDetail(); break;
                case 6: BindPurchaseDetail(); break;
                case 7: BindVendorAdvanceDetail(); break;
                case 8: BindClientSiteDetail(); break;
                case 9: BindProfitabilityDetail(); break;
                default: BindProfitabilityImportReview(); break;
            }
            ApplySelectedColumns();
            ApplyGridGrouping();
            UpdateExplorerGridCount();
            if (_currentReportIndex == 9)
                BindExplorerPresentation();
            else
                BindGenericExplorerPresentation();
        }

        private void BindRevenueDetail()
        {
            AddColumns("Client", "Monthly", "Annual", "Type", "Status");
            foreach (AMCContract c in _contracts.Where(c => c.ContractStatus == "Active").OrderByDescending(c => c.AnnualValue))
            {
                string client = ResolveClientName(c.ClientID);
                _detailGrid.Rows.Add(client, c.MonthlyValue.ToString("N0"), c.AnnualValue.ToString("N0"), c.ContractType, c.ContractStatus);
            }
        }

        private void BindCollectionDetail()
        {
            AddColumns("Invoice", "Client", "Due", "Balance", "Status");
            foreach (Invoice inv in _invoices.Where(i => i.PaymentStatus != "Paid").OrderByDescending(i => i.BalanceDue).Take(30))
                _detailGrid.Rows.Add(inv.InvoiceNumber, inv.ClientName ?? "", inv.DueDate.ToString("dd-MMM-yy"), inv.BalanceDue.ToString("N0"), inv.PaymentStatus);
        }

        private void BindContractDetail()
        {
            AddColumns("Client", "Type", "Expires", "Days", "Monthly", "Action");
            foreach (AMCContract c in _contracts.OrderBy(c => c.EndDate).Take(30))
            {
                int days = (c.EndDate - DateTime.Today).Days;
                string client = ResolveClientName(c.ClientID);
                _detailGrid.Rows.Add(client, c.ContractType, c.EndDate.ToString("dd-MMM-yyyy"), days.ToString(), c.MonthlyValue.ToString("N0"), days <= 30 ? "Renew" : "Monitor");
            }
        }

        private string ResolveClientName(int clientId)
        {
            string name;
            return _clientNames != null && _clientNames.TryGetValue(clientId, out name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : "Client #" + clientId;
        }

        private void BindJobDetail()
        {
            AddColumns("Job", "Client", "Type", "Priority", "Technician", "Status");
            foreach (Job j in _jobs.OrderByDescending(j => j.ScheduledDate).Take(30))
                _detailGrid.Rows.Add(j.JobNumber, j.ClientName ?? "", j.JobType, j.Priority, j.AssignedEmployeeName ?? "", j.Status);
        }

        private void BindTechnicianDetail()
        {
            AddColumns("Technician", "Open Jobs", "Completed", "Revenue", "Avg / Job");
            foreach (Employee tech in _technicians)
            {
                var techJobs = _jobs.Where(j => j.AssignedEmployeeID.HasValue && j.AssignedEmployeeID.Value == tech.EmployeeID).ToList();
                int open = techJobs.Count(j => !IsComplete(j.Status));
                int completed = techJobs.Count(j => IsComplete(j.Status));
                decimal revenue = techJobs.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
                decimal avg = techJobs.Count == 0 ? 0 : revenue / techJobs.Count;
                _detailGrid.Rows.Add(tech.Name, open.ToString(), completed.ToString(), revenue.ToString("N0"), avg.ToString("N0"));
            }
        }

        private void BindInventoryDetail()
        {
            AddColumns("Item", "Category", "Buffer Qty", "Reserved", "Typical Buy Qty", "Reference Value");
            foreach (StockItem item in _stock.OrderByDescending(i => i.AvailableStock <= 0m || i.IsLowStock).ThenBy(i => i.ItemName).Take(30))
                _detailGrid.Rows.Add(item.ItemName, item.Category, item.CurrentStock.ToString("N1"), item.ReservedStock.ToString("N1"), item.ReorderLevel.ToString("N1"), item.StockValue.ToString("N0"));
        }

        private void BindPurchaseDetail()
        {
            AddColumns("PO", "Supplier", "Date", "Amount", "Balance", "Status");
            foreach (PurchaseOrder po in _purchases.OrderByDescending(p => p.PODate).Take(30))
                _detailGrid.Rows.Add(po.PONumber, po.VendorName ?? "", po.PODate.ToString("dd-MMM-yy"), po.TotalAmount.ToString("N0"), po.BalanceDue.ToString("N0"), po.Status);
        }

        private void BindVendorAdvanceDetail()
        {
            AddColumns("Supplier", "Type", "Date", "Amount", "Applied", "Balance", "Reference");
            foreach (VendorAdvancePayment advance in _vendorAdvances.Take(30))
                _detailGrid.Rows.Add(
                    advance.VendorName ?? ("Supplier #" + advance.VendorId),
                    advance.TransactionType,
                    advance.TransactionDate.ToString("dd-MMM-yy"),
                    advance.Amount.ToString("N0"),
                    advance.AppliedAmount.ToString("N0"),
                    advance.Balance.ToString("N0"),
                    string.IsNullOrWhiteSpace(advance.ReferenceNumber) ? advance.PONumber : advance.ReferenceNumber);
        }

        private void BindClientSiteDetail()
        {
            AddColumns("Client", "Jobs", "Revenue", "Open Jobs", "Last Update");
            foreach (var row in _jobs.GroupBy(j => string.IsNullOrWhiteSpace(j.ClientName) ? "Client #" + j.ClientID : j.ClientName).OrderByDescending(g => g.Count()).Take(30))
            {
                decimal revenue = row.Sum(j => Math.Max(j.Revenue, Math.Max(j.ActualRevenue, j.QuotedRevenue)));
                int open = row.Count(j => !IsComplete(j.Status));
                DateTime last = row.Max(j => j.ScheduledDate);
                _detailGrid.Rows.Add(row.Key, row.Count().ToString(), revenue.ToString("N0"), open.ToString(), last.ToString("dd-MMM-yy"));
            }
        }

        private void ExportCurrentReport()
        {
            bool excel = _cmbExportFormat == null || _cmbExportFormat.SelectedIndex <= 0;
            string extension = excel ? ".xlsx" : ".csv";
            string filter = excel ? "Excel workbook (*.xlsx)|*.xlsx" : "CSV file (*.csv)|*.csv";
            using (var dlg = new SaveFileDialog { FileName = ExplorerReportLabel(_currentReportIndex).Replace(" ", "_") + "_" + DateTime.Today.ToString("yyyyMMdd") + extension, Filter = filter })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    WriteCurrentGridExport(dlg.FileName);
                    _lblStatus.Text = "Exported: " + Path.GetFileName(dlg.FileName);
                    _lblStatus.ForeColor = Green;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Exporting report", ex);
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Exporting report", ex);
                    _lblStatus.Text = "Report export failed.";
                    _lblStatus.ForeColor = Red;
                }
            }
        }

        private void WriteCurrentGridExport(string path)
        {
            List<DataGridViewColumn> columns = _detailGrid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
            List<DataGridViewRow> rows = _detailGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Visible && !r.IsNewRow).ToList();
            if (string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                using (ExcelPackage package = new ExcelPackage())
                {
                    ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Report");
                    sheet.Cells[1, 1].Value = ExplorerReportLabel(_currentReportIndex);
                    sheet.Cells[1, 1, 1, Math.Max(1, columns.Count)].Merge = true;
                    sheet.Cells[1, 1].Style.Font.Bold = true;
                    sheet.Cells[1, 1].Style.Font.Size = 14;
                    sheet.Cells[2, 1].Value = "Generated";
                    sheet.Cells[2, 2].Value = DateTime.Now;
                    sheet.Cells[2, 2].Style.Numberformat.Format = "dd/mm/yyyy hh:mm";
                    for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        sheet.Cells[4, columnIndex + 1].Value = columns[columnIndex].HeaderText;
                        sheet.Cells[4, columnIndex + 1].Style.Font.Bold = true;
                        sheet.Cells[4, columnIndex + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[4, columnIndex + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(239, 246, 255));
                    }
                    for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                    {
                        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                            sheet.Cells[rowIndex + 5, columnIndex + 1].Value = rows[rowIndex].Cells[columns[columnIndex].Index].Value;
                    }
                    if (columns.Count > 0) sheet.Cells[1, 1, Math.Max(5, rows.Count + 4), columns.Count].AutoFitColumns();
                    package.SaveAs(new FileInfo(path));
                }
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", columns.Select(c => CsvValue(c.HeaderText))));
            foreach (DataGridViewRow row in rows)
                sb.AppendLine(string.Join(",", columns.Select(c => CsvValue(Convert.ToString(row.Cells[c.Index].Value, CultureInfo.CurrentCulture)))));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string CsvValue(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private void ExportMonthlyProfitLoss()
        {
            using (var dlg = new SaveFileDialog { FileName = "Monthly_PL_" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx", Filter = "Excel workbook (*.xlsx)|*.xlsx" })
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    using (ExcelPackage package = new ExcelPackage())
                    {
                        ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Monthly P&L");
                        WriteProfitLossSheet(sheet);
                        package.SaveAs(new FileInfo(dlg.FileName));
                    }

                    _lblStatus.Text = "P&L exported: " + Path.GetFileName(dlg.FileName);
                    _lblStatus.ForeColor = Green;
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Exporting monthly profit and loss", ex);
                    _lblStatus.Text = "P&L export failed.";
                    _lblStatus.ForeColor = Red;
                }
            }
        }

        private void WriteProfitLossSheet(ExcelWorksheet sheet)
        {
            DateTime firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
            List<MonthlyProfitLossRow> monthly = _financialSvc.GetMonthlyProfitLoss(firstMonth, 12);
            string[] headers = { "Month", "Taxable Revenue", "Direct Costs", "Gross Profit", "Gross Margin %", "Payroll", "Operating Expenses", "Net Profit", "Net Margin %" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[1, i + 1].Value = headers[i];
                sheet.Cells[1, i + 1].Style.Font.Bold = true;
                sheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                sheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(239, 246, 255));
            }

            for (int i = 0; i < monthly.Count; i++)
            {
                MonthlyProfitLossRow value = monthly[i];
                int row = i + 2;

                sheet.Cells[row, 1].Value = value.Month;
                sheet.Cells[row, 1].Style.Numberformat.Format = "mmm-yy";
                sheet.Cells[row, 2].Value = value.Revenue;
                sheet.Cells[row, 3].Value = value.DirectCosts;
                sheet.Cells[row, 4].Value = value.GrossProfit;
                sheet.Cells[row, 5].Value = value.GrossMarginPercent / 100m;
                sheet.Cells[row, 6].Value = value.PayrollExpense;
                sheet.Cells[row, 7].Value = value.OperatingExpenses;
                sheet.Cells[row, 8].Value = value.NetProfit;
                sheet.Cells[row, 9].Value = value.NetMarginPercent / 100m;
            }

            int totalRow = monthly.Count + 2;
            sheet.Cells[totalRow, 1].Value = "Total";
            sheet.Cells[totalRow, 1].Style.Font.Bold = true;
            foreach (int col in new[] { 2, 3, 4, 6, 7, 8 })
            {
                sheet.Cells[totalRow, col].Formula = "SUM(" + sheet.Cells[2, col].Address + ":" + sheet.Cells[13, col].Address + ")";
                sheet.Cells[totalRow, col].Style.Font.Bold = true;
            }
            sheet.Cells[totalRow, 5].Formula = "IF(" + sheet.Cells[totalRow, 2].Address + "=0,0," + sheet.Cells[totalRow, 4].Address + "/" + sheet.Cells[totalRow, 2].Address + ")";
            sheet.Cells[totalRow, 9].Formula = "IF(" + sheet.Cells[totalRow, 2].Address + "=0,0," + sheet.Cells[totalRow, 8].Address + "/" + sheet.Cells[totalRow, 2].Address + ")";
            sheet.Cells[totalRow, 5].Style.Font.Bold = true;
            sheet.Cells[totalRow, 9].Style.Font.Bold = true;
            sheet.Cells[2, 2, totalRow, 4].Style.Numberformat.Format = "₹#,##0.00;[Red](₹#,##0.00);-";
            sheet.Cells[2, 6, totalRow, 8].Style.Numberformat.Format = "₹#,##0.00;[Red](₹#,##0.00);-";
            sheet.Cells[2, 5, totalRow, 5].Style.Numberformat.Format = "0.0%;[Red](0.0%);-";
            sheet.Cells[2, 9, totalRow, 9].Style.Numberformat.Format = "0.0%;[Red](0.0%);-";
            sheet.View.FreezePanes(2, 1);
            sheet.Cells.AutoFitColumns();
        }

        private void ImportProfitabilityWorkbook()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Import job P&L workbook",
                Filter = "Excel workbooks (*.xlsx)|*.xlsx",
                Multiselect = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    ProfitabilityImportPreview preview = _profitabilityImportSvc.Preview(dialog.FileName);
                    string summary = preview.ValidRowCount.ToString("N0") + " rows found. " +
                                     preview.MatchedRowCount.ToString("N0") + " matched to ServoERP invoices and " +
                                     preview.ReviewRowCount.ToString("N0") + " require review.\n\n" +
                                     "The import will be staged for review. It will not overwrite invoices, jobs, purchases, or payments.";
                    if (MessageBox.Show(this, summary, "Review Job P&L Import", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                        return;
                    int batchId = _financialSvc.StageImport(preview);
                    _lblStatus.Text = "Job P&L import staged as batch #" + batchId.ToString(CultureInfo.InvariantCulture) + ".";
                    _lblStatus.ForeColor = preview.ReviewRowCount > 0 ? Amber : Green;
                    SelectReport(ReportNames.Length - 1);
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Importing job profitability workbook", ex);
                    _lblStatus.Text = "Job P&L import could not be staged. Review the workbook and try again.";
                    _lblStatus.ForeColor = Red;
                }
            }
        }

        private void AddExpense()
        {
            List<ExpenseCategory> categories;
            try { categories = _financialSvc.GetExpenseCategories(); }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Loading expense categories", ex);
                return;
            }

            using (Form dialog = ServoModalForm.Create("Add P&L Expense", 470, 380))
            {
                var category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DataSource = categories };
                var job = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
                var jobChoices = new List<ExpenseJobChoice> { new ExpenseJobChoice { JobId = null, Label = "Company expense / no job" } };
                jobChoices.AddRange(_jobs.OrderByDescending(j => j.ScheduledDate).Select(j => new ExpenseJobChoice
                {
                    JobId = j.JobID,
                    Label = (j.JobNumber ?? "Job #" + j.JobID) + " - " + (j.ClientName ?? j.JobTitle ?? j.Title)
                }));
                job.DataSource = jobChoices;
                var date = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Dock = DockStyle.Fill };
                var amount = new NumericUpDown { DecimalPlaces = 2, Maximum = 1000000000m, ThousandsSeparator = true, Dock = DockStyle.Fill };
                var reference = new TextBox { Dock = DockStyle.Fill };
                var description = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 58 };
                var save = MakeButton("Save Expense", Green, 120);
                var cancel = MakeButton("Cancel", Color.White, 90);
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
                actions.Controls.Add(save);
                actions.Controls.Add(cancel);
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 7 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                layout.Controls.Add(new Label { Text = "Category", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                layout.Controls.Add(category, 1, 0);
                layout.Controls.Add(new Label { Text = "Job allocation", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
                layout.Controls.Add(job, 1, 1);
                layout.Controls.Add(new Label { Text = "Expense date", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
                layout.Controls.Add(date, 1, 2);
                layout.Controls.Add(new Label { Text = "Amount excl. GST (₹)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
                layout.Controls.Add(amount, 1, 3);
                layout.Controls.Add(new Label { Text = "Reference", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
                layout.Controls.Add(reference, 1, 4);
                layout.Controls.Add(new Label { Text = "Description", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 5);
                layout.Controls.Add(description, 1, 5);
                layout.Controls.Add(actions, 0, 6);
                layout.SetColumnSpan(actions, 2);
                dialog.Controls.Add(layout);
                cancel.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;
                save.Click += (s, e) =>
                {
                    try
                    {
                        ExpenseCategory selected = category.SelectedItem as ExpenseCategory;
                        ExpenseJobChoice selectedJob = job.SelectedItem as ExpenseJobChoice;
                        if (selected != null && selected.ProfitLossGroup == "Direct Cost" && (selectedJob == null || !selectedJob.JobId.HasValue))
                            throw new InvalidOperationException("Select a job for direct labour, travel, or other direct cost.");
                        _financialSvc.AddExpense(new ExpenseEntry
                        {
                            ExpenseCategoryId = selected == null ? 0 : selected.ExpenseCategoryId,
                            JobId = selectedJob == null ? null : selectedJob.JobId,
                            ExpenseDate = date.Value.Date,
                            Amount = amount.Value,
                            ReferenceNumber = reference.Text,
                            Description = description.Text
                        });
                        dialog.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(dialog, ex.Message, "Expense not saved", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _lblStatus.Text = "Expense saved and included in the P&L.";
                    _lblStatus.ForeColor = Green;
                    BindProfitabilityDetail();
                }
            }
        }

        private Label AddKpi(TableLayoutPanel host, int column, string title, Color accent, out Label subLabel)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Margin = new Padding(column == 0 ? 0 : 5, 0, column == 5 ? 0 : 5, 0), Padding = new Padding(14, 10, 10, 8) };
            card.Paint += (s, e) => DrawBorder(e.Graphics, card);
            Panel icon = new Panel { Dock = DockStyle.Left, Width = 38, BackColor = CardBg, Padding = new Padding(0, 3, 8, 0) };
            Label badge = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 30, DS.Lighten(accent, 0.82f), accent, 10);
            badge.Dock = DockStyle.Top;
            icon.Controls.Add(badge);
            Label titleLabel = new Label { Text = title.ToUpperInvariant(), Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextMid, AutoEllipsis = true };
            Label valueLabel = new Label { Text = "-", Dock = DockStyle.Top, Height = 31, Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
            subLabel = new Label { Text = "Loading...", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.2f), ForeColor = TextMid, AutoEllipsis = true };
            card.Controls.Add(subLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(icon);
            host.Controls.Add(card, column, 0);
            return valueLabel;
        }

        private void BindProfitabilityDetail()
        {
            AddColumns("Job", "Client / Site", "Invoice", "Revenue", "Direct Cost", "Gross Profit", "Margin %", "Outstanding", "Cost Status");
            foreach (JobProfitabilityRow row in FilteredProfitabilityRows(true).Take(250))
            {
                int rowIndex = _detailGrid.Rows.Add(
                    row.JobNumber,
                    string.Join(" / ", new[] { row.ClientName, row.SiteName }.Where(v => !string.IsNullOrWhiteSpace(v))),
                    row.InvoiceNumber ?? "",
                    row.BilledRevenue.ToString("N2"),
                    row.ActualDirectCost.ToString("N2"),
                    row.GrossProfit.ToString("N2"),
                    row.BilledRevenue <= 0m ? "n.a." : row.GrossMarginPercent.ToString("N1"),
                    row.OutstandingAmount.ToString("N2"),
                    row.CostStatus);
                if (!string.Equals(row.CostStatus, "Complete", StringComparison.OrdinalIgnoreCase))
                    _detailGrid.Rows[rowIndex].Cells[_detailGrid.Columns.Count - 1].Style.ForeColor = Red;
            }
            ApplySelectedColumns();
            ApplyGridGrouping();
            UpdateExplorerGridCount();
        }

        private void BindProfitabilityImportReview()
        {
            AddColumns("Row", "Customer", "Invoice", "Revenue", "Vendor Cost", "Invoice Match", "Job Match", "Status", "Review Message");
            if (_usingPreviewData)
            {
                _detailGrid.Rows.Add("2", "Aarti Industries", "INV-2026-041", "185000.00", "122400.00", "#41", "#26041", "Matched", "Ready for review");
                _detailGrid.Rows.Add("3", "Larsen & Toubro", "INV-2026-044", "164000.00", "n.a.", "#44", "#26044", "Review", "Vendor cost is incomplete");
                return;
            }
            foreach (ProfitabilityImportRow row in _financialSvc.GetLatestImportRows().Take(250))
            {
                _detailGrid.Rows.Add(
                    row.SourceRowNumber.ToString(CultureInfo.InvariantCulture),
                    row.CustomerName,
                    row.InvoiceNumber,
                    row.TaxableRevenue.HasValue ? row.TaxableRevenue.Value.ToString("N2") : "n.a.",
                    row.VendorCost.HasValue ? row.VendorCost.Value.ToString("N2") : "n.a.",
                    row.MatchedInvoiceId.HasValue ? "#" + row.MatchedInvoiceId.Value : "Not found",
                    row.MatchedJobId.HasValue ? "#" + row.MatchedJobId.Value : "Not linked",
                    row.ReviewStatus,
                    row.ReviewMessage);
            }
        }

        private TableLayoutPanel MakeOwnerCardBody(string key)
        {
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBg,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(2, 4, 2, 0)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            _ownerCardBodies[key] = body;
            SetOwnerCardRows(key, Metric("Status", "Loading...", TextMid));
            return body;
        }

        private void SetOwnerCardRows(string key, params OwnerMetric[] metrics)
        {
            TableLayoutPanel body;
            if (!_ownerCardBodies.TryGetValue(key, out body) || body == null)
                return;
            if (metrics == null || metrics.Length == 0)
                metrics = new[] { Metric("Status", "No data", TextMid) };

            body.SuspendLayout();
            body.Controls.Clear();
            body.RowStyles.Clear();
            body.RowCount = Math.Max(1, metrics.Length);

            for (int i = 0; i < metrics.Length; i++)
            {
                OwnerMetric metric = metrics[i];
                body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / metrics.Length));
                Label label = new Label
                {
                    Text = metric.Label,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = TextMid,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    Cursor = metric.ReportIndex.HasValue ? Cursors.Hand : Cursors.Default
                };
                Label value = new Label
                {
                    Text = metric.Value,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = metric.Color,
                    TextAlign = ContentAlignment.MiddleRight,
                    AutoEllipsis = true,
                    Cursor = metric.ReportIndex.HasValue ? Cursors.Hand : Cursors.Default
                };
                if (metric.ReportIndex.HasValue)
                {
                    EventHandler openReport = (s, e) =>
                    {
                        SelectReport(metric.ReportIndex.Value);
                        _lblStatus.Text = "Opened " + ReportNames[metric.ReportIndex.Value] + " report from action queue.";
                        _lblStatus.ForeColor = Blue;
                    };
                    label.Click += openReport;
                    value.Click += openReport;
                }
                else
                {
                    EventHandler openCardDetail = (s, e) => OpenOwnerCardDetail(key);
                    label.Cursor = Cursors.Hand;
                    value.Cursor = Cursors.Hand;
                    label.Click += openCardDetail;
                    value.Click += openCardDetail;
                }
                body.Controls.Add(label, 0, i);
                body.Controls.Add(value, 1, i);
            }
            body.Visible = true;
            body.ResumeLayout(true);
            body.PerformLayout();
            body.Invalidate();
        }

        private static OwnerMetric Metric(string label, string value, Color color)
        {
            return new OwnerMetric { Label = label, Value = value, Color = color };
        }

        private static OwnerMetric Metric(string label, string value, Color color, int reportIndex)
        {
            return new OwnerMetric { Label = label, Value = value, Color = color, ReportIndex = reportIndex };
        }

        private void OpenOwnerCardDetail(string key)
        {
            try
            {
                OwnerCardDetail detail = BuildOwnerCardDetail(key);
                using (OwnerCardDetailDialog dialog = new OwnerCardDetailDialog(detail))
                    dialog.ShowDialog(FindForm());
                _lblStatus.Text = "Opened " + detail.Title + " details.";
                _lblStatus.ForeColor = Blue;
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Reports"), "Opening report card details", ex);
                _lblStatus.Text = "Could not open card details.";
                _lblStatus.ForeColor = Red;
            }
        }

        private OwnerCardDetail BuildOwnerCardDetail(string key)
        {
            OwnerCardDetail detail = new OwnerCardDetail
            {
                Key = key,
                Title = ResolveOwnerCardTitle(key),
                Summary = ReadOwnerCardSummary(key)
            };

            switch ((key ?? string.Empty).ToLowerInvariant())
            {
                case "business_health":
                    detail.Columns.AddRange(new[] { "Type", "Reference", "Party", "Date", "Amount", "Paid", "Balance", "Status" });
                    foreach (Invoice invoice in _invoices.Where(i => IsThisMonth(i.InvoiceDate)).OrderByDescending(i => i.InvoiceDate))
                        detail.Rows.Add(Row("Invoice", invoice.InvoiceNumber, invoice.ClientName, DateText(invoice.InvoiceDate), Money(invoice.TotalAmount), Money(invoice.PaidAmount), Money(invoice.BalanceDue), invoice.PaymentStatus));
                    foreach (PurchaseOrder po in _purchases.Where(p => IsThisMonth(p.PODate)).OrderByDescending(p => p.PODate))
                        detail.Rows.Add(Row("Purchase", po.PONumber, po.VendorName, DateText(po.PODate), Money(po.TotalAmount), Money(po.PaidAmount), Money(po.BalanceDue), po.Status));
                    break;

                case "receivables":
                    detail.Columns.AddRange(new[] { "Invoice", "Client", "Invoice Date", "Due Date", "Total", "Paid", "Balance", "Days Late", "Status" });
                    foreach (Invoice invoice in _invoices.Where(i => !IsPaid(i.PaymentStatus) && i.BalanceDue > 0m).OrderByDescending(i => i.BalanceDue))
                    {
                        int daysLate = Math.Max(0, (DateTime.Today - invoice.DueDate.Date).Days);
                        detail.Rows.Add(Row(invoice.InvoiceNumber, invoice.ClientName, DateText(invoice.InvoiceDate), DateText(invoice.DueDate), Money(invoice.TotalAmount), Money(invoice.PaidAmount), Money(invoice.BalanceDue), daysLate.ToString("N0"), invoice.PaymentStatus));
                    }
                    break;

                case "sales_pipeline":
                    detail.Columns.AddRange(new[] { "Quotation", "Client", "Submitted", "Due", "Value", "Status", "Owner", "Vendor" });
                    foreach (TenderBid quote in _quotations.OrderByDescending(q => q.SubmittedDate ?? q.ModifiedDate ?? q.DueDate))
                        detail.Rows.Add(Row(quote.QuotationNumber, quote.ClientName, DateText(quote.SubmittedDate), DateText(quote.DueDate), Money(QuoteValue(quote)), quote.Status, quote.CreatedByName, quote.RecommendedVendorName));
                    break;

                case "jobs_workload":
                    detail.Columns.AddRange(new[] { "Job", "Client", "Site", "Technician", "Scheduled", "Priority", "Revenue", "Cost", "Status" });
                    foreach (Job job in _jobs.Where(j => !IsComplete(j.Status)).OrderByDescending(j => j.IsOverdue).ThenBy(j => j.ScheduledDate))
                        detail.Rows.Add(Row(job.JobNumber, job.ClientName, job.SiteName, job.AssignedEmployeeName, DateText(job.ScheduledDate), job.Priority, Money(JobValue(job)), Money(job.EstimatedCost), job.Status));
                    break;

                case "amc_contracts":
                    detail.Columns.AddRange(new[] { "Client", "Type", "Start", "End", "Days Left", "Monthly", "Annual", "Status" });
                    foreach (AMCContract contract in _contracts.OrderBy(c => c.EndDate))
                    {
                        int daysLeft = (contract.EndDate.Date - DateTime.Today).Days;
                        detail.Rows.Add(Row(ResolveClientName(contract.ClientID), contract.ContractType, DateText(contract.StartDate), DateText(contract.EndDate), daysLeft.ToString("N0"), Money(contract.MonthlyValue), Money(contract.AnnualValue), contract.ContractStatus));
                    }
                    break;

                case "purchase_payables":
                    detail.Columns.AddRange(new[] { "PO", "Supplier", "PO Date", "Pay By", "Total", "Paid", "Balance", "Age", "Status" });
                    foreach (PurchaseOrder po in _purchases.OrderByDescending(p => p.IsOverdue).ThenByDescending(p => p.BalanceDue))
                        detail.Rows.Add(Row(po.PONumber, po.VendorName, DateText(po.PODate), DateText(po.PayByDate), Money(po.TotalAmount), Money(po.PaidAmount), Money(po.BalanceDue), po.AgeDays.ToString("N0"), po.Status));
                    break;

                case "inventory_risk":
                    detail.Columns.AddRange(new[] { "Item", "Category", "Available", "Current", "Reserved", "Reorder", "Rate", "Value", "Supplier" });
                    foreach (StockItem item in _stock.OrderByDescending(i => i.AvailableStock <= 0m).ThenByDescending(i => i.IsLowStock).ThenBy(i => i.ItemName))
                        detail.Rows.Add(Row(item.ItemName, item.Category, QuantityText(item.AvailableStock, item.Unit), QuantityText(item.CurrentStock, item.Unit), QuantityText(item.ReservedStock, item.Unit), QuantityText(item.ReorderLevel, item.Unit), Money(item.LastPurchaseRate), Money(item.StockValue), item.VendorName));
                    break;

                case "top_clients":
                    detail.Columns.AddRange(new[] { "Client", "Jobs", "Open Jobs", "Completed", "Revenue", "Cost", "Margin", "Last Scheduled" });
                    foreach (var client in _jobs.GroupBy(j => string.IsNullOrWhiteSpace(j.ClientName) ? "Client #" + j.ClientID : j.ClientName).Select(g => new { Name = g.Key, Jobs = g.ToList() }).OrderByDescending(g => g.Jobs.Sum(JobValue)))
                    {
                        decimal revenue = client.Jobs.Sum(JobValue);
                        decimal cost = client.Jobs.Sum(j => j.EstimatedCost);
                        detail.Rows.Add(Row(client.Name, client.Jobs.Count.ToString("N0"), client.Jobs.Count(j => !IsComplete(j.Status)).ToString("N0"), client.Jobs.Count(j => IsComplete(j.Status)).ToString("N0"), Money(revenue), Money(cost), Money(revenue - cost), DateText(client.Jobs.Max(j => j.ScheduledDate))));
                    }
                    break;

                case "top_suppliers":
                    detail.Columns.AddRange(new[] { "Supplier", "POs", "Open Dues", "Overdue POs", "Total Spend", "Paid", "Balance", "Last PO" });
                    foreach (var supplier in _purchases.GroupBy(p => string.IsNullOrWhiteSpace(p.VendorName) ? "Supplier #" + p.VendorID : p.VendorName).Select(g => new { Name = g.Key, Purchases = g.ToList() }).OrderByDescending(g => g.Purchases.Sum(p => p.TotalAmount)))
                        detail.Rows.Add(Row(supplier.Name, supplier.Purchases.Count.ToString("N0"), supplier.Purchases.Count(p => p.BalanceDue > 0m).ToString("N0"), supplier.Purchases.Count(p => p.IsOverdue).ToString("N0"), Money(supplier.Purchases.Sum(p => p.TotalAmount)), Money(supplier.Purchases.Sum(p => p.PaidAmount)), Money(supplier.Purchases.Sum(p => p.BalanceDue)), DateText(supplier.Purchases.Max(p => p.PODate))));
                    break;

                case "payroll_snapshot":
                    detail.Columns.AddRange(new[] { "Employee", "Code", "Designation", "Department", "Client Site", "Gross Salary", "Basic Salary", "Status" });
                    foreach (Employee employee in _technicians.OrderBy(e => e.Name))
                        detail.Rows.Add(Row(employee.Name, employee.EmployeeCode, employee.Designation, employee.Department, employee.ClientSite, Money(employee.GrossSalary), Money(employee.BasicSalary), employee.Status));
                    break;

                case "service_desk":
                    detail.Columns.AddRange(new[] { "Ticket", "Client", "Site", "Assigned To", "Priority", "Opened", "SLA Due", "Status", "Summary" });
                    foreach (ServiceDeskIncident ticket in _serviceTickets.OrderBy(t => IsClosedTicket(t.Status)).ThenByDescending(t => t.SlaBreached).ThenBy(t => t.SlaDueAt))
                        detail.Rows.Add(Row(ticket.IncidentNumber, ticket.ClientName, ticket.SiteName, ticket.AssignedEmployeeName, ticket.Priority, DateTimeText(ticket.OpenedAt), DateTimeText(ticket.SlaDueAt), ticket.Status, ticket.ShortDescription));
                    break;

                default:
                    detail.Columns.AddRange(new[] { "Area", "Reference", "Party", "Due / Date", "Amount / Count", "Status", "Action" });
                    AddOwnerActionRows(detail);
                    break;
            }

            if (detail.Rows.Count == 0)
                detail.Rows.Add(Row("No records", "Nothing currently needs attention for this card."));

            return detail;
        }

        private void AddOwnerActionRows(OwnerCardDetail detail)
        {
            foreach (Invoice invoice in _invoices.Where(IsOverdueInvoice).OrderByDescending(i => i.BalanceDue))
                detail.Rows.Add(Row("Collection", invoice.InvoiceNumber, invoice.ClientName, DateText(invoice.DueDate), Money(invoice.BalanceDue), invoice.PaymentStatus, "Collect payment"));
            foreach (TenderBid quote in _quotations.Where(q => IsOpenQuote(q.Status)).OrderBy(q => q.DueDate))
                detail.Rows.Add(Row("Quotation", quote.QuotationNumber, quote.ClientName, DateText(quote.DueDate), Money(QuoteValue(quote)), quote.Status, "Follow up"));
            foreach (Job job in _jobs.Where(j => j.IsOverdue || (j.ScheduledDate.Date < DateTime.Today && !IsComplete(j.Status))).OrderBy(j => j.ScheduledDate))
                detail.Rows.Add(Row("Job", job.JobNumber, job.ClientName, DateText(job.ScheduledDate), Money(JobValue(job)), job.Status, "Close overdue job"));
            foreach (StockItem item in _stock.Where(i => i.AvailableStock <= 0m || i.IsLowStock).OrderBy(i => i.AvailableStock))
                detail.Rows.Add(Row("Inventory", item.ItemName, item.VendorName, DateText(item.LastUpdated), QuantityText(item.AvailableStock, item.Unit), item.AvailableStock <= 0m ? "Out of stock" : "Low stock", "Reorder material"));
            foreach (AMCContract contract in _contracts.Where(c => c.EndDate.Date >= DateTime.Today && c.EndDate.Date <= DateTime.Today.AddDays(30)).OrderBy(c => c.EndDate))
                detail.Rows.Add(Row("AMC", contract.ContractType, ResolveClientName(contract.ClientID), DateText(contract.EndDate), Money(contract.AnnualValue), contract.ContractStatus, "Renew contract"));
            foreach (PurchaseOrder po in _purchases.Where(p => p.IsOverdue).OrderByDescending(p => p.BalanceDue))
                detail.Rows.Add(Row("Payable", po.PONumber, po.VendorName, DateText(po.PayByDate), Money(po.BalanceDue), po.Status, "Pay vendor"));
        }

        private string ResolveOwnerCardTitle(string key)
        {
            ResizableCard card;
            return _dashboardCards.TryGetValue(key ?? string.Empty, out card) && card != null
                ? card.CardTitle
                : "Report Card";
        }

        private List<OwnerMetric> ReadOwnerCardSummary(string key)
        {
            List<OwnerMetric> metrics = new List<OwnerMetric>();
            TableLayoutPanel body;
            if (!_ownerCardBodies.TryGetValue(key ?? string.Empty, out body) || body == null)
                return metrics;

            for (int row = 0; row < body.RowCount; row++)
            {
                Label label = body.GetControlFromPosition(0, row) as Label;
                Label value = body.GetControlFromPosition(1, row) as Label;
                if (label != null && value != null)
                    metrics.Add(Metric(label.Text, value.Text, value.ForeColor));
            }
            return metrics;
        }

        private static object[] Row(params object[] values)
        {
            return values ?? new object[0];
        }

        private static string DateText(DateTime date)
        {
            return date == default(DateTime) ? "-" : date.ToString("dd-MMM-yyyy");
        }

        private static string DateText(DateTime? date)
        {
            return date.HasValue ? DateText(date.Value) : "-";
        }

        private static string DateTimeText(DateTime date)
        {
            return date == default(DateTime) ? "-" : date.ToString("dd-MMM-yyyy HH:mm");
        }

        private static string QuantityText(decimal value, string unit)
        {
            return value.ToString("N1") + (string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit);
        }

        private ResizableCard AddDashboardCard(string key, string title, Control content, int width, int height, string preset)
        {
            ResizableCard card = new ResizableCard
            {
                PageKey = PageKey,
                CardKey = key,
                CardTitle = title,
                Size = new Size(width, height),
                MinimumSize = new Size(300, 170),
                SizePreset = preset,
                ResizeAxes = CardResizeAxes.Both,
                AllowResize = true,
                Margin = new Padding(0, 0, 12, 12)
            };
            content.Dock = DockStyle.Fill;
            card.ContentPanel.Tag = MergeTag(card.ContentPanel.Tag, "NO_CARD_SURFACE");
            content.Tag = MergeTag(content.Tag, "NO_CARD_SURFACE");
            card.ContentPanel.Controls.Add(content);
            AttachOwnerCardDetailOpen(card.ContentPanel, key);
            card.CardDragRequested += DashboardCard_DragRequested;
            card.CardResizeComplete += (s, e) => LayoutDashboardCards();
            _dashboardCards[key] = card;
            _dashboardFlow.Controls.Add(card);
            CardLayoutService.RegisterDefaultSize(PageKey, key, card.Size, preset);
            return card;
        }

        private void AttachOwnerCardDetailOpen(Control control, string key)
        {
            if (control == null)
                return;

            control.Cursor = Cursors.Hand;
            control.Click += (s, e) => OpenOwnerCardDetail(key);
            foreach (Control child in control.Controls)
                AttachOwnerCardDetailOpen(child, key);
        }

        /// <summary>Adds a metadata token to an existing control tag.</summary>
        private static string MergeTag(object existing, string token)
        {
            string current = existing == null ? string.Empty : existing.ToString();
            if (current.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return current;

            return string.IsNullOrWhiteSpace(current) ? token : current + " " + token;
        }

        private void DashboardCard_DragRequested(object sender, MouseEventArgs e)
        {
            ResizableCard card = sender as ResizableCard;
            if (card == null)
                return;

            _dragCard = card;
            card.DoDragDrop(card.CardKey ?? string.Empty, DragDropEffects.Move);
        }

        private void DashboardFlow_DragEnter(object sender, DragEventArgs e)
        {
            if (_dragCard != null)
                e.Effect = DragDropEffects.Move;
        }

        private void DashboardFlow_DragDrop(object sender, DragEventArgs e)
        {
            if (_dragCard == null || _dashboardFlow == null)
                return;

            Point clientPoint = _dashboardFlow.PointToClient(new Point(e.X, e.Y));
            int newIndex = _dashboardFlow.Controls.Count - 1;
            for (int i = 0; i < _dashboardFlow.Controls.Count; i++)
            {
                Control candidate = _dashboardFlow.Controls[i];
                if (candidate == _dragCard)
                    continue;

                Rectangle bounds = candidate.Bounds;
                if (clientPoint.Y < bounds.Top + bounds.Height / 2 ||
                    (clientPoint.Y <= bounds.Bottom && clientPoint.X < bounds.Left + bounds.Width / 2))
                {
                    newIndex = i;
                    break;
                }
            }

            _dashboardFlow.Controls.SetChildIndex(_dragCard, Math.Max(0, newIndex));
            _dashboardFlow.PerformLayout();
            SaveCardOrder();
            _dragCard = null;
        }

        private void ApplySavedCardOrder()
        {
            try
            {
                if (!File.Exists(CardOrderPath))
                    return;

                string[] keys = File.ReadAllText(CardOrderPath)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToArray();
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    ResizableCard card;
                    if (_dashboardCards.TryGetValue(keys[i], out card))
                        _dashboardFlow.Controls.SetChildIndex(card, 0);
                }
            }
            catch
            {
            }
        }

        private void SaveCardOrder()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CardOrderPath));
                string order = string.Join(",", _dashboardFlow.Controls.OfType<ResizableCard>().Select(c => c.CardKey));
                File.WriteAllText(CardOrderPath, order);
            }
            catch
            {
            }
        }

        private void LayoutDashboardCards()
        {
            if (_dashboardFlow == null || _dashboardFlow.ClientSize.Width <= 0)
                return;

            int gap = 12;
            int availableWidth = Math.Max(320, _dashboardFlow.ClientSize.Width);
            int columns = availableWidth >= 1160 ? 3 : (availableWidth >= 780 ? 2 : 1);
            int cardWidth = columns == 1 ? availableWidth : (availableWidth - (gap * (columns - 1))) / columns;
            int[] columnHeights = new int[columns];
            ResizableCard[] cards = _dashboardFlow.Controls
                .OfType<ResizableCard>()
                .ToArray();

            _dashboardFlow.SuspendLayout();
            foreach (ResizableCard card in cards)
            {
                int span = Math.Min(columns, ResolveDashboardCardColumnSpan(card.CardKey, columns));
                int column = FindDashboardColumn(columnHeights, span);
                card.Width = Math.Max(300, (cardWidth * span) + (gap * (span - 1)));
                card.Height = ResolveDashboardCardHeight(card.CardKey, columns);
                card.Margin = Padding.Empty;

                card.Location = new Point(column * (cardWidth + gap), columnHeights[column]);
                int newHeight = columnHeights[column] + card.Height + gap;
                for (int i = column; i < column + span; i++)
                    columnHeights[i] = newHeight;
            }
            _dashboardFlow.ResumeLayout(false);

            int contentHeight = columnHeights.Length == 0 ? 0 : columnHeights.Max();
            if (contentHeight > 0)
                contentHeight -= gap;
            _dashboardFlow.Height = contentHeight;

            Panel wrapper = _dashboardFlow.Parent as Panel;
            if (wrapper != null)
                wrapper.Height = Math.Max(220, contentHeight + wrapper.Padding.Vertical + 12);
        }

        private static int FindDashboardColumn(int[] columnHeights, int span)
        {
            int bestColumn = 0;
            int bestHeight = int.MaxValue;
            for (int column = 0; column <= columnHeights.Length - span; column++)
            {
                int height = 0;
                for (int i = column; i < column + span; i++)
                    height = Math.Max(height, columnHeights[i]);
                if (height < bestHeight)
                {
                    bestHeight = height;
                    bestColumn = column;
                }
            }
            return bestColumn;
        }

        private static int ResolveDashboardCardColumnSpan(string cardKey, int columns)
        {
            if (columns < 2)
                return 1;
            if (columns >= 3 && IsAny(cardKey, "business_health", "receivables"))
                return 2;
            if (columns == 2 && IsAny(cardKey, "business_health", "owner_action_queue"))
                return 2;
            return 1;
        }

        private static int ResolveDashboardCardHeight(string cardKey, int columns)
        {
            if (IsAny(cardKey, "owner_action_queue"))
                return columns == 1 ? 360 : 392;
            if (IsAny(cardKey, "business_health", "receivables"))
                return columns == 1 ? 240 : 270;
            if (IsAny(cardKey, "sales_pipeline", "jobs_workload", "amc_contracts", "purchase_payables", "inventory_risk"))
                return 230;
            return 196;
        }

        private Panel MakeCard(string title, Control content)
        {
            Panel card = MakePlainCard(title);
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            return card;
        }

        private Panel MakePlainCard(string title)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Margin = new Padding(0, 0, 12, 12), Padding = new Padding(14, 44, 14, 14) };
            card.Paint += (s, e) => DrawBorder(e.Graphics, card);
            Label titleIcon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 24, DS.Indigo50, Blue, 8);
            titleIcon.Location = new Point(12, 9);
            Label titleLabel = new Label { Text = title, Location = new Point(44, 10), Size = new Size(Math.Max(80, card.ClientSize.Width - 58), 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoEllipsis = true, Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = TextDark, BackColor = CardBg };
            card.Controls.Add(titleLabel);
            card.Controls.Add(titleIcon);
            titleLabel.BringToFront();
            return card;
        }

        private Button MakeReportTile(int index, string title)
        {
            Button tile = new Button
            {
                Tag = index,
                Text = title,
                Image = ModernIconSystem.IconBitmap(ModernIconSystem.KindForTitle(title), 16, Blue),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Width = 132,
                Height = 42,
                Margin = new Padding(0, 0, 8, 0),
                BackColor = CardBg,
                ForeColor = TextDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(7, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            tile.FlatAppearance.BorderColor = DS.InputBorder;
            tile.FlatAppearance.BorderSize = 1;
            tile.AccessibleName = "Open " + title + " report";
            UIHelper.ApplyButtonStyle(tile, ButtonRole.Secondary);
            tile.ImageAlign = ContentAlignment.MiddleLeft;
            tile.TextImageRelation = TextImageRelation.ImageBeforeText;
            tile.TextAlign = ContentAlignment.MiddleCenter;
            tile.Click += (s, e) => SelectReport(Convert.ToInt32(((Control)s).Tag));
            return tile;
        }

        private void LayoutReportLibraryTiles()
        {
            if (_reportLibrary == null || _reportLibrary.Controls.Count == 0)
                return;

            int gap = 8;
            int available = Math.Max(0, _reportLibrary.ClientSize.Width - _reportLibrary.Padding.Horizontal);
            int count = _reportLibrary.Controls.Count;
            int width = Math.Max(108, Math.Min(154, (available - (gap * (count - 1))) / Math.Max(1, count)));
            foreach (Control tile in _reportLibrary.Controls)
            {
                tile.Width = width;
                tile.Height = 42;
                tile.Margin = new Padding(0, 0, gap, 0);
            }
        }

        private Chart MakeChart(string name, SeriesChartType type, Color color)
        {
            Chart chart = new Chart { Dock = DockStyle.Fill, BackColor = CardBg };
            ChartArea area = new ChartArea(name);
            area.BackColor = CardBg;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisX.LabelStyle.ForeColor = TextMid;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(235, 239, 245);
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisY.LabelStyle.ForeColor = TextMid;
            area.AxisY.LineColor = Border;
            area.AxisX.LineColor = Border;
            chart.ChartAreas.Add(area);
            Series series = new Series("Value") { ChartType = type, Color = color, BorderWidth = 3, ChartArea = name };
            series.IsValueShownAsLabel = type == SeriesChartType.Column || type == SeriesChartType.Bar;
            series.Font = new Font("Segoe UI", 8f);
            chart.Series.Add(series);
            ChartHoverService.Enable(chart, (hoverSeries, point) => new ChartHoverContent
            {
                Key = "report-" + name + "-" + point.AxisLabel,
                Title = string.IsNullOrWhiteSpace(point.AxisLabel) ? name : point.AxisLabel,
                Value = ChartHoverFormat.Number(Convert.ToDecimal(point.YValues[0])),
                Calculation = "report value aggregated for this category using the active report filters"
            });
            return chart;
        }

        private DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = CardBg,
                GridColor = Border,
                Font = new Font("Segoe UI", 8.8f),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false
            };
            DS.StyleGrid(grid);
            return grid;
        }

        private static DataGridViewTextBoxColumn C(string header, int width)
        {
            return new DataGridViewTextBoxColumn { HeaderText = header, Width = width, MinimumWidth = Math.Max(70, width), SortMode = DataGridViewColumnSortMode.Automatic };
        }

        private void AddColumns(params string[] headers)
        {
            foreach (string header in headers)
                _detailGrid.Columns.Add(C(header, 120));
        }

        private Button MakeButton(string text, Color bg, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = Math.Max(100, width),
                Height = 34,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            UIHelper.ApplyButtonStyle(button, UIHelper.ResolveButtonRole(button));
            return button;
        }

        private void DrawBorder(Graphics g, Control control)
        {
            using (Pen pen = new Pen(Border, 1))
                g.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        }

        private static bool IsComplete(string status)
        {
            return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsClosedTicket(string status)
        {
            return IsAny(status, "Resolved", "Closed", "Cancelled");
        }

        private static bool IsPaid(string status)
        {
            return IsAny(status, "Paid", "Closed", "Received");
        }

        private static bool IsOpenQuote(string status)
        {
            return IsAny(status, "Draft", "Analysed", "Analyzed", "Sent", "Submitted", "Pending", "Open");
        }

        private static bool IsOverdueInvoice(Invoice invoice)
        {
            if (invoice == null || IsPaid(invoice.PaymentStatus))
                return false;
            return IsAny(invoice.PaymentStatus, "Overdue") || invoice.DueDate.Date < DateTime.Today;
        }

        private static bool IsAny(string value, params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            foreach (string candidate in candidates)
            {
                if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsThisMonth(DateTime date)
        {
            DateTime today = DateTime.Today;
            return date.Year == today.Year && date.Month == today.Month;
        }

        private static decimal QuoteValue(TenderBid quote)
        {
            if (quote == null)
                return 0m;
            if (quote.TotalWithGST > 0m)
                return quote.TotalWithGST;
            if (quote.BidValue > 0m)
                return quote.BidValue;
            return quote.TotalTaxableValue + quote.TotalGSTAmount;
        }

        private static decimal JobValue(Job job)
        {
            return job == null ? 0m : Math.Max(job.Revenue, Math.Max(job.ActualRevenue, job.QuotedRevenue));
        }

        private static string Money(decimal value)
        {
            return IndiaFormatHelper.FormatCurrency(value);
        }

        private static string ShortText(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            return text.Length <= max ? text : text.Substring(0, max - 3) + "...";
        }

        private sealed class OwnerCardDetail
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public List<OwnerMetric> Summary { get; set; } = new List<OwnerMetric>();
            public List<string> Columns { get; private set; } = new List<string>();
            public List<object[]> Rows { get; private set; } = new List<object[]>();
        }

        private sealed class OwnerCardDetailDialog : ServoERP.Infrastructure.ServoFormBase
        {
            private readonly OwnerCardDetail _detail;
            private readonly DataGridView _grid;

            public OwnerCardDetailDialog(OwnerCardDetail detail)
            {
                _detail = detail ?? new OwnerCardDetail { Title = "Report Card" };
                Text = _detail.Title + " Details";
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(980, 620);
                Size = new Size(1180, 720);
                BackColor = PageBg;
                Padding = new Padding(18);

                Panel footer = BuildFooter();
                Panel summary = BuildSummaryStrip();
                Panel header = BuildDialogHeader();
                _grid = BuildDialogGrid();

                Controls.Add(_grid);
                Controls.Add(summary);
                Controls.Add(header);
                Controls.Add(footer);
                BindGrid();
            }

            private Panel BuildDialogHeader()
            {
                Panel header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 72,
                    BackColor = PageBg,
                    Padding = new Padding(0, 0, 0, 12)
                };

                Label title = new Label
                {
                    Text = _detail.Title,
                    Dock = DockStyle.Top,
                    Height = 34,
                    Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                    ForeColor = TextDark,
                    AutoEllipsis = true
                };
                Label subtitle = new Label
                {
                    Text = _detail.Rows.Count.ToString("N0") + " records from the selected report card",
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = TextMid,
                    AutoEllipsis = true
                };

                header.Controls.Add(subtitle);
                header.Controls.Add(title);
                return header;
            }

            private Panel BuildSummaryStrip()
            {
                Panel wrapper = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 76,
                    BackColor = PageBg,
                    Padding = new Padding(0, 0, 0, 12)
                };
                FlowLayoutPanel strip = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    WrapContents = false,
                    AutoScroll = false,
                    BackColor = PageBg
                };

                foreach (OwnerMetric metric in _detail.Summary.Take(8))
                    strip.Controls.Add(BuildSummaryChip(metric));
                wrapper.Controls.Add(strip);
                return wrapper;
            }

            private Control BuildSummaryChip(OwnerMetric metric)
            {
                Panel chip = new Panel
                {
                    Width = 204,
                    Height = 58,
                    BackColor = CardBg,
                    Margin = new Padding(0, 0, 10, 0),
                    Padding = new Padding(12, 6, 12, 6)
                };
                chip.Paint += (s, e) => DrawRoundedBorder(e.Graphics, chip.ClientRectangle, Border);

                Label label = new Label
                {
                    Text = metric == null ? "" : metric.Label,
                    Dock = DockStyle.Top,
                    Height = 20,
                    Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                    ForeColor = TextMid,
                    AutoEllipsis = true
                };
                Label value = new Label
                {
                    Text = metric == null ? "" : metric.Value,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    ForeColor = metric == null ? TextDark : metric.Color,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                };

                chip.Controls.Add(value);
                chip.Controls.Add(label);
                return chip;
            }

            private DataGridView BuildDialogGrid()
            {
                DataGridView grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    ReadOnly = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    RowHeadersVisible = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    BackgroundColor = CardBg,
                    GridColor = Border,
                    Font = new Font("Segoe UI", 8.8f),
                    BorderStyle = BorderStyle.None,
                    EnableHeadersVisualStyles = false
                };
                DS.StyleGrid(grid);
                return grid;
            }

            private Panel BuildFooter()
            {
                Panel footer = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 54,
                    BackColor = PageBg,
                    Padding = new Padding(0, 12, 0, 0)
                };
                TableLayoutPanel actions = new TableLayoutPanel
                {
                    Dock = DockStyle.Right,
                    Width = 250,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = PageBg,
                    Padding = new Padding(0)
                };
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                Button close = DialogButton("Close", Blue, 112);
                close.DialogResult = DialogResult.OK;
                close.Dock = DockStyle.Fill;
                Button export = DialogButton("Export CSV", Green, 112);
                export.Dock = DockStyle.Fill;
                export.Click += (s, e) => ExportGrid();
                actions.Controls.Add(export, 0, 0);
                actions.Controls.Add(close, 1, 0);
                footer.Controls.Add(actions);
                return footer;
            }

            private Button DialogButton(string text, Color color, int width)
            {
                Button button = new Button
                {
                    Text = text,
                    Width = width,
                    Height = 34,
                    BackColor = color,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(8, 0, 0, 0)
                };
                button.FlatAppearance.BorderSize = 0;
                return button;
            }

            private void BindGrid()
            {
                _grid.Columns.Clear();
                _grid.Rows.Clear();

                foreach (string column in _detail.Columns)
                    _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = column, MinimumWidth = 100, SortMode = DataGridViewColumnSortMode.Automatic });

                foreach (object[] row in _detail.Rows)
                {
                    object[] normalized = new object[_detail.Columns.Count];
                    for (int i = 0; i < normalized.Length; i++)
                        normalized[i] = row != null && i < row.Length && row[i] != null ? row[i].ToString() : string.Empty;
                    _grid.Rows.Add(normalized);
                }

                if (_grid.Columns.Count > 0)
                    _grid.Columns[0].MinimumWidth = 150;
            }

            private void ExportGrid()
            {
                using (SaveFileDialog dialog = new SaveFileDialog
                {
                    FileName = SafeFileName(_detail.Title) + "_" + DateTime.Today.ToString("yyyyMMdd") + ".csv",
                    Filter = "CSV|*.csv"
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", _detail.Columns.Select(Csv)));
                    foreach (object[] row in _detail.Rows)
                        sb.AppendLine(string.Join(",", row.Select(v => Csv(v == null ? string.Empty : v.ToString()))));
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                }
            }

            private static string Csv(string value)
            {
                return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
            }

            private static string SafeFileName(string value)
            {
                string name = string.IsNullOrWhiteSpace(value) ? "ReportCard" : value;
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    name = name.Replace(invalid, '_');
                return name.Replace(' ', '_');
            }

            private static void DrawRoundedBorder(Graphics graphics, Rectangle bounds, Color color)
            {
                using (Pen pen = new Pen(color))
                {
                    Rectangle rect = new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
                    graphics.DrawRectangle(pen, rect);
                }
            }
        }

        private sealed class ExplorerDialog : ServoERP.Infrastructure.ServoFormBase
        {
            public ExplorerDialog(string title, Size size)
            {
                Text = BrandingService.WindowTitle(title);
                Size = size;
                MinimumSize = size;
                StartPosition = FormStartPosition.CenterParent;
                BackColor = CardBg;
                Font = DS.Body;
                ShowInTaskbar = false;
                MaximizeBox = false;
                MinimizeBox = false;
            }
        }

        private sealed class OwnerMetric
        {
            public string Label { get; set; }
            public string Value { get; set; }
            public Color Color { get; set; }
            public int? ReportIndex { get; set; }
        }
    }
}

