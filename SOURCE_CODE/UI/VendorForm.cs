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
    public class VendorForm : DeferredPageControl
    {
        private const int VendorListMinWidth = 300;

        private static readonly Color White = DS.White;
        private static readonly Color PageBg = DS.BgPage;
        private static readonly Color Surface = DS.Slate50;
        private static readonly Color Border = DS.Border;
        private static readonly Color BorderLight = DS.Slate100;
        private static readonly Color TextPrimary = DS.Slate900;
        private static readonly Color TextSecondary = DS.Slate500;
        private static readonly Color TextHint = DS.Slate400;
        private static readonly Color Teal = DS.Teal500;
        private static readonly Color TealLightBg = DS.Teal50;
        private static readonly Color Amber = DS.Amber500;
        private static readonly Color AmberDark = Color.FromArgb(146, 64, 14);
        private static readonly Color AmberLightBg = DS.Amber50;
        private static readonly Color Red = DS.Red500;
        private static readonly Color RedDark = DS.Red600;
        private static readonly Color RedLightBg = DS.Red50;
        private static readonly Color Blue = DS.Primary600;
        private static readonly Color BlueDark = DS.Primary700;
        private static readonly Color BlueLightBg = DS.Primary50;

        private readonly VendorService _vendorSvc = new VendorService();
        private readonly VendorAdvancePaymentService _vendorAdvanceSvc = new VendorAdvancePaymentService();
        private readonly PurchaseService _purchaseSvc = new PurchaseService();
        private readonly ContractService _contractSvc = new ContractService();
        private readonly ErrorProvider _errors = new ErrorProvider();

        private readonly List<string> _categories = new List<string> { "General", "HVAC Equipment", "Copper & Pipe", "Electrical", "Refrigerant", "Tools", "Chemicals", "Other" };
        private readonly List<string> _vendorTypes = new List<string> { "Distributor", "Supplier", "Subcontractor", "Labour" };
        private readonly List<string> _msmeTypes = new List<string> { "No", "Yes-Micro", "Yes-Small", "Yes-Medium" };
        private readonly List<string> _gstTypes = new List<string> { "Regular", "Composition", "Unregistered" };
        private readonly List<string> _tdsSections = new List<string> { "194C", "194J", "194Q" };
        private readonly List<string> _paymentModes = new List<string> { "NEFT", "RTGS", "UPI", "Cheque", "Cash" };
        private readonly Dictionary<string, decimal> _tdsRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "194C", 2m },
            { "194J", 10m },
            { "194Q", 0.1m }
        };

        private SplitContainer _split;
        private FlowLayoutPanel _vendorListFlow;
        private FlowLayoutPanel _chipFlow;
        private Panel _searchSection;
        private Panel _duplicateBanner;
        private Label _lblDuplicateBanner;
        private TextBox _txtSearch;
        private Button _btnClearSearch;
        private Label _lblListFooter;
        private LinkLabel _lnkClearSearch;

        private Panel _topBar;
        private Label _lblTopBadge;
        private Label _lblDuplicateBadge;
        private Button _btnMerge;
        private Button _btnImport;
        private Button _btnTemplate;

        private Panel _rightScroll;
        private Panel _rightHost;
        private Panel _vendorHeaderRow;
        private Label _lblHeaderName;
        private Label _lblHeaderMeta;
        private Button _btnRaisePo;
        private Button _btnViewPos;
        private Button _btnSave;
        private Button _btnArchive;

        private Panel _statsRow;
        private Label _lblStatOutstanding;
        private Label _lblStatPurchased;
        private Label _lblStatOpenPos;
        private Label _lblStatCreditDays;

        private Panel _warningStrip;
        private Label _lblWarningStrip;

        private ResizableCard _cardIdentity;
        private ResizableCard _cardContact;
        private ResizableCard _cardTax;
        private ResizableCard _cardPayment;
        private ResizableCard _cardRecentPos;
        private ResizableCard _cardNotes;

        private TextBox _txtVendorName;
        private ComboBox _cmbCategory;
        private FlowLayoutPanel _tagFlow;
        private LinkLabel _lnkAddTag;
        private TextBox _txtAddTag;
        private ComboBox _cmbVendorType;
        private ComboBox _cmbMsme;
        private TextBox _txtMsmeNumber;
        private Label _lblMsmeNote;

        private TextBox _txtPhone;
        private TextBox _txtWhatsApp;
        private LinkLabel _lnkSamePhone;
        private TextBox _txtEmail;
        private TextBox _txtAddress;
        private TextBox _txtCity;
        private ComboBox _cmbState;

        private TextBox _txtGstin;
        private Label _lblGstinState;
        private Label _lblPanHint;
        private TextBox _txtPan;
        private ComboBox _cmbGstType;
        private CheckBox _chkTds;
        private ComboBox _cmbTdsSection;
        private NumericUpDown _numTdsRate;
        private CheckBox _chkRcm;
        private Label _lblTaxNote;
        private Label _lblGstinHint;
        private Label _lblIfscHint;

        private NumericUpDown _numCreditDays;
        private Panel _creditTrack;
        private Panel _creditFill;
        private Label _lblCreditHint;
        private Label _lblMsmeWarning;
        private ComboBox _cmbPaymentMode;
        private TextBox _txtBankAccount;
        private TextBox _txtIfsc;
        private TextBox _txtAccountName;
        private TextBox _txtBankName;
        private Button _btnWhatsApp;

        private FlowLayoutPanel _recentPoFlow;
        private Label _lblRecentPoFooter;
        private TextBox _txtNotes;

        private List<VendorSummaryDto> _vendorSummaries = new List<VendorSummaryDto>();
        private List<DuplicateGroupDto> _duplicateGroups = new List<DuplicateGroupDto>();
        private List<PurchaseOrder> _dashboardPurchases = new List<PurchaseOrder>();
        private List<AMCContract> _dashboardContracts = new List<AMCContract>();
        private VendorDetailDto _currentVendor;
        private bool _isBinding;
        private bool _isNewMode;
        private bool _showDashboard = true;
        private Panel _vendorDashboardHost;
        private TextBox _dashboardSearch;
        private ComboBox _dashboardCategoryFilter;
        private string _dashboardTab = "All Vendors";
        private string _dashboardCategory = "All Categories";
        private int _dashboardPage = 1;
        private const int DashboardPageSize = 10;
        private string _activeFilter = "All";
        private bool _searchPlaceholderActive;

        public VendorForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            RestoreVendorInputChrome();
            EnableDeferredLoad((Func<Task>)(async () => await LoadInitialAsync()), ex => AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Vendor screen", ex));
        }

        private void BuildLayout()
        {
            Controls.Clear();
            if (_showDashboard)
            {
                BuildVendorDashboardLayout();
                return;
            }

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = VendorListMinWidth,
                Panel1MinSize = VendorListMinWidth,
                SplitterWidth = 1,
                BackColor = Border
            };

            BuildLeftPanel();
            BuildRightPanel();

            Controls.Add(_split);
            Resize += (s, e) => LayoutDetailCards();
            Resize += (s, e) => EnsureVendorListWidth();
        }

        private void EnsureVendorListWidth()
        {
            if (_split == null || _split.IsDisposed)
                return;

            int maxDistance = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
            if (maxDistance >= VendorListMinWidth && _split.SplitterDistance < VendorListMinWidth)
                _split.SplitterDistance = VendorListMinWidth;
        }

        private void BuildVendorDashboardLayout()
        {
            BackColor = PageBg;
            _vendorDashboardHost = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, AutoScroll = true, Padding = new Padding(22, 16, 22, 22) };
            Controls.Add(_vendorDashboardHost);
        }

        private void RenderVendorDashboard()
        {
            if (_vendorDashboardHost == null || _vendorDashboardHost.IsDisposed)
                return;

            _vendorDashboardHost.SuspendLayout();
            _vendorDashboardHost.Controls.Clear();

            Panel content = new Panel { BackColor = PageBg, Location = new Point(22, 16), Width = Math.Max(1120, _vendorDashboardHost.ClientSize.Width - 58), Height = 1180 };
            _vendorDashboardHost.Controls.Add(content);

            Control header = BuildVendorDashboardHeader(content.Width);
            header.Location = new Point(0, 0);
            content.Controls.Add(header);

            FlowLayoutPanel stats = new FlowLayoutPanel { Location = new Point(0, 76), Size = new Size(content.Width, 96), BackColor = PageBg, WrapContents = false, AutoScroll = true };
            foreach (Control card in BuildVendorStatCards(Math.Max(205, (content.Width - 48) / 5)))
                stats.Controls.Add(card);
            content.Controls.Add(stats);

            TableLayoutPanel top = new TableLayoutPanel { Location = new Point(0, 190), Size = new Size(content.Width, 230), BackColor = PageBg, ColumnCount = 4, RowCount = 1 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            top.Controls.Add(BuildStatusOverviewCard(), 0, 0);
            top.Controls.Add(BuildSpendCategoryCard(), 1, 0);
            top.Controls.Add(BuildTopVendorsCard(), 2, 0);
            top.Controls.Add(BuildPerformanceCard(), 3, 0);
            content.Controls.Add(top);

            TableLayoutPanel mid = new TableLayoutPanel { Location = new Point(0, 438), Size = new Size(content.Width, 430), BackColor = PageBg, ColumnCount = 2, RowCount = 1 };
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78f));
            mid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            mid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mid.Controls.Add(BuildRecentVendorsCard(), 0, 0);
            mid.Controls.Add(BuildVendorDashboardSidebar(), 1, 0);
            content.Controls.Add(mid);

            TableLayoutPanel bottom = new TableLayoutPanel { Location = new Point(0, 886), Size = new Size(content.Width, 230), BackColor = PageBg, ColumnCount = 2, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            bottom.Controls.Add(BuildUpcomingRenewalsCard(), 0, 0);
            bottom.Controls.Add(BuildRecentActivitiesCard(), 1, 0);
            content.Controls.Add(bottom);

            _vendorDashboardHost.Resize += (s, e) =>
            {
                if (content.IsDisposed) return;
                content.Width = Math.Max(1120, _vendorDashboardHost.ClientSize.Width - 58);
            };
            _vendorDashboardHost.ResumeLayout();
        }

        private Control BuildVendorDashboardHeader(int width)
        {
            Panel header = new Panel { Size = new Size(width, 58), BackColor = PageBg };
            header.Controls.Add(new Label { Text = "Vendor Management", Location = new Point(0, 0), Size = new Size(320, 28), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = TextPrimary });
            header.Controls.Add(new Label { Text = "Manage vendors, performance, contracts and compliance in one place.", Location = new Point(1, 30), Size = new Size(560, 18), Font = new Font("Segoe UI", 8.8f), ForeColor = TextSecondary });

            _dashboardSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary, Text = GetDashboardSearchText(), Size = new Size(250, 28) };
            ConfigurePlaceholder(_dashboardSearch, "Search vendor name, code, email...");
            _dashboardSearch.TextChanged += (s, e) => { _dashboardPage = 1; RenderVendorDashboard(); };

            Button filters = MakeDashboardButton("Filters", White, TextPrimary, 84, true);
            filters.Click += (s, e) => { if (_dashboardCategoryFilter != null) _dashboardCategoryFilter.DroppedDown = true; };

            Button addVendor = MakeDashboardButton("+ Add Vendor  v", Blue, White, 134, false);
            addVendor.Click += (s, e) =>
            {
                ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
                menu.Items.Add("Add Vendor", null, async (mi, ev) => await BeginNewVendorAsync());
                menu.Items.Add("Import Vendors", null, (mi, ev) => ImportUiHelper.RunImport(ExcelImportModule.Vendors, FindForm()));
                menu.Items.Add("Add Contact", null, (mi, ev) => ShowVendorDashboardMessage("Add Contact", "Open a vendor row first, then add contact details on the vendor form."));
                menu.Show(addVendor, new Point(0, addVendor.Height));
            };

            Label bell = MakeDashboardIconBadge("!");
            bell.Click += (s, e) => ShowVendorDashboardMessage("Vendor Alerts", BuildVendorAlertText());
            Panel user = BuildVendorSessionPanel();

            header.Controls.AddRange(new Control[] { _dashboardSearch, filters, addVendor, bell, user });
            header.Resize += (s, e) =>
            {
                user.Location = new Point(header.Width - user.Width, 2);
                bell.Location = new Point(user.Left - 38, 2);
                addVendor.Location = new Point(bell.Left - 144, 1);
                filters.Location = new Point(addVendor.Left - 94, 1);
                _dashboardSearch.Location = new Point(filters.Left - 260, 1);
            };
            return header;
        }

        private IEnumerable<Control> BuildVendorStatCards(int width)
        {
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime lastMonthStart = monthStart.AddMonths(-1);
            DateTime lastMonthEnd = monthStart.AddDays(-1);
            var vendors = ActiveDashboardVendors().ToList();
            var purchasesThisMonth = _dashboardPurchases.Where(p => p.PODate.Date >= monthStart && p.PODate.Date <= DateTime.Today).ToList();
            var purchasesLastMonth = _dashboardPurchases.Where(p => p.PODate.Date >= lastMonthStart && p.PODate.Date <= lastMonthEnd).ToList();
            int active = vendors.Count(v => GetVendorDashboardStatus(v) == "Active");
            int blocked = vendors.Count(v => GetVendorDashboardStatus(v) == "Blocked");
            decimal spendThisMonth = purchasesThisMonth.Sum(p => p.TotalAmount);
            decimal spendLastMonth = purchasesLastMonth.Sum(p => p.TotalAmount);
            decimal overdue = _dashboardPurchases.Where(p => p.IsOverdue).Sum(p => Math.Max(0m, p.BalanceDue));
            decimal overdueLastMonth = _dashboardPurchases.Where(p => p.PayByDate.Date >= lastMonthStart && p.PayByDate.Date <= lastMonthEnd && p.BalanceDue > 0).Sum(p => Math.Max(0m, p.BalanceDue));

            yield return MakeDashboardStatCard(width, "Total Vendors", vendors.Count.ToString(), TrendForNewVendors(), BlueLightBg, Blue);
            yield return MakeDashboardStatCard(width, "Active Vendors", active.ToString(), PercentText(active, vendors.Count) + " of total", TealLightBg, Teal);
            yield return MakeDashboardStatCard(width, "Total Spend (This Month)", IndiaFormatHelper.FormatCurrency(spendThisMonth), TrendPercent(spendThisMonth, spendLastMonth) + " vs last month", Color.FromArgb(245, 243, 255), Color.FromArgb(124, 58, 237));
            yield return MakeDashboardStatCard(width, "Overdue Payments", IndiaFormatHelper.FormatCurrency(overdue), TrendPercent(overdue, overdueLastMonth) + " vs last month", AmberLightBg, Amber);
            yield return MakeDashboardStatCard(width, "Blocked Vendors", blocked.ToString(), blocked + " require review", RedLightBg, Red);
        }

        private Panel BuildStatusOverviewCard()
        {
            Panel card = MakeDashboardCard("Vendor Status Overview", "View All", (s, e) => { _dashboardTab = "All Vendors"; _dashboardPage = 1; RenderVendorDashboard(); });
            var statuses = new[] { "Active", "Inactive", "Pending Approval", "Blocked" }
                .Select(s => new DashboardSlice { Name = s, Value = ActiveDashboardVendors().Count(v => GetVendorDashboardStatus(v) == s), Color = StatusColor(s) })
                .ToList();
            card.Controls.Add(MakeDonutPanel(statuses, ActiveDashboardVendors().Count().ToString(), "Total", new Point(14, 44), new Size(112, 112)));
            AddLegend(card, statuses, 138, 54, ActiveDashboardVendors().Count());
            return card;
        }

        private Panel BuildSpendCategoryCard()
        {
            Panel card = MakeDashboardCard("Spend by Category (This Month)", "View Report", (s, e) => ExportCsv());
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            List<DashboardSlice> slices = _dashboardPurchases
                .Where(p => p.PODate.Date >= monthStart && p.PODate.Date <= DateTime.Today)
                .GroupBy(p => CategoryForVendorId(p.VendorID))
                .Select((g, i) => new DashboardSlice { Name = g.Key, ValueDecimal = g.Sum(p => p.TotalAmount), Value = (int)Math.Round(g.Sum(p => p.TotalAmount)), Color = CategoryColor(g.Key) })
                .OrderByDescending(s => s.ValueDecimal)
                .ToList();
            decimal total = slices.Sum(s => s.ValueDecimal);
            card.Controls.Add(MakeDonutPanel(slices, CompactCurrency(total), "Total Spend", new Point(14, 44), new Size(112, 112)));
            AddLegend(card, slices, 138, 52, total);
            return card;
        }

        private Panel BuildTopVendorsCard()
        {
            Panel card = MakeDashboardCard("Top Vendors by Spend (This Month)", "View All", (s, e) => ShowVendorDashboardMessage("Top Vendors", BuildTopVendorsText()));
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var rows = _dashboardPurchases
                .Where(p => p.PODate.Date >= monthStart && p.PODate.Date <= DateTime.Today)
                .GroupBy(p => p.VendorID)
                .Select(g => new { VendorId = g.Key, Name = VendorNameById(g.Key, g.FirstOrDefault()?.VendorName), Spend = g.Sum(p => p.TotalAmount) })
                .OrderByDescending(r => r.Spend)
                .Take(5)
                .ToList();
            decimal max = rows.Count == 0 ? 1m : rows.Max(r => r.Spend);
            decimal total = rows.Sum(r => r.Spend);
            int y = 48;
            foreach (var row in rows)
            {
                card.Controls.Add(MakeInitialsAvatar(row.Name, 16, y + 2, BlueLightBg, Blue));
                card.Controls.Add(new Label { Text = row.Name, Location = new Point(48, y), Size = new Size(130, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true });
                Panel track = new Panel { Location = new Point(182, y + 7), Size = new Size(92, 5), BackColor = BorderLight };
                track.Controls.Add(new Panel { Dock = DockStyle.Left, Width = (int)(track.Width * (row.Spend / max)), BackColor = Blue });
                card.Controls.Add(track);
                card.Controls.Add(new Label { Text = CompactCurrency(row.Spend), Location = new Point(280, y), Size = new Size(78, 17), Font = new Font("Segoe UI", 7.2f), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight });
                card.Controls.Add(new Label { Text = PercentText(row.Spend, total), Location = new Point(360, y), Size = new Size(42, 17), Font = new Font("Segoe UI", 7.2f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight });
                y += 32;
            }
            if (rows.Count == 0)
                AddDashboardEmpty(card, "No purchase spend this month.");
            return card;
        }

        private Panel BuildPerformanceCard()
        {
            Panel card = MakeDashboardCard("Vendor Performance Summary", null, null);
            var vendors = ActiveDashboardVendors().ToList();
            double avg = vendors.Count == 0 ? 0 : vendors.Average(GetVendorRating);
            card.Controls.Add(MakeGaugePanel(avg, new Point(24, 42), new Size(150, 104)));
            card.Controls.Add(new Label { Text = avg.ToString("0.0"), Location = new Point(62, 78), Size = new Size(74, 26), Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter });
            card.Controls.Add(new Label { Text = RatingStars(avg), Location = new Point(42, 108), Size = new Size(116, 18), Font = new Font("Segoe UI", 9f), ForeColor = Amber, TextAlign = ContentAlignment.MiddleCenter });
            card.Controls.Add(new Label { Text = "Average Rating", Location = new Point(42, 128), Size = new Size(116, 18), Font = new Font("Segoe UI", 7.5f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleCenter });

            int x = 190, y = 52;
            AddDistributionRow(card, "Excellent (4.5 - 5)", vendors.Count(v => GetVendorRating(v) >= 4.5), vendors.Count, Teal, x, y); y += 30;
            AddDistributionRow(card, "Good (3.5 - 4.4)", vendors.Count(v => GetVendorRating(v) >= 3.5 && GetVendorRating(v) < 4.5), vendors.Count, Blue, x, y); y += 30;
            AddDistributionRow(card, "Average (2.5 - 3.4)", vendors.Count(v => GetVendorRating(v) >= 2.5 && GetVendorRating(v) < 3.5), vendors.Count, Amber, x, y); y += 30;
            AddDistributionRow(card, "Poor (0 - 2.4)", vendors.Count(v => GetVendorRating(v) > 0 && GetVendorRating(v) < 2.5), vendors.Count, Red, x, y);
            return card;
        }

        private Panel BuildRecentVendorsCard()
        {
            Panel card = MakeDashboardCard("Recent Vendors", null, null);
            card.Size = new Size(860, 420);
            card.Padding = new Padding(0);
            string[] tabs = { "All Vendors", "Active", "Pending Approval", "Inactive", "Blocked" };
            FlowLayoutPanel tabBar = new FlowLayoutPanel { Location = new Point(16, 38), Size = new Size(420, 30), BackColor = White, WrapContents = false };
            foreach (string tab in tabs)
            {
                Button b = MakeTabButton(tab, string.Equals(_dashboardTab, tab, StringComparison.OrdinalIgnoreCase));
                b.Click += (s, e) => { _dashboardTab = ((Button)s).Text; _dashboardPage = 1; RenderVendorDashboard(); };
                tabBar.Controls.Add(b);
            }
            card.Controls.Add(tabBar);

            _dashboardCategoryFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8f), Location = new Point(card.Width - 342, 40), Size = new Size(132, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _dashboardCategoryFilter.Items.AddRange(new object[] { "All Categories", "Components", "Equipment", "Consumables", "Services", "Others" });
            _dashboardCategoryFilter.SelectedItem = string.IsNullOrWhiteSpace(_dashboardCategory) ? "All Categories" : _dashboardCategory;
            _dashboardCategoryFilter.SelectedIndexChanged += (s, e) => { _dashboardCategory = Convert.ToString(_dashboardCategoryFilter.SelectedItem); _dashboardPage = 1; RenderVendorDashboard(); };
            card.Controls.Add(_dashboardCategoryFilter);

            Button export = MakeDashboardButton("Export", White, TextPrimary, 76, true);
            export.Location = new Point(card.Width - 88, 38);
            export.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            export.Click += (s, e) => ExportCsv();
            card.Controls.Add(export);

            TableLayoutPanel table = new TableLayoutPanel { Location = new Point(16, 80), Size = new Size(card.Width - 32, 286), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, BackColor = White, ColumnCount = 9, RowCount = 1 };
            float[] widths = { 11, 18, 12, 11, 9, 13, 11, 10, 5 };
            foreach (float w in widths) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            string[] headers = { "Vendor Code", "Vendor Name", "Category", "Status", "Rating", "Spend (MTD)", "Outstanding", "Last Activity", "Actions" };
            for (int i = 0; i < headers.Length; i++)
                table.Controls.Add(MakeTableLabel(headers[i], true, TextPrimary), i, 0);

            List<VendorSummaryDto> filtered = FilterDashboardVendors().ToList();
            int totalRows = filtered.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)DashboardPageSize));
            _dashboardPage = Math.Max(1, Math.Min(_dashboardPage, totalPages));
            foreach (VendorSummaryDto v in filtered.Skip((_dashboardPage - 1) * DashboardPageSize).Take(DashboardPageSize))
                AddVendorDashboardTableRow(table, v);
            card.Controls.Add(table);
            if (totalRows == 0)
            {
                Label empty = new Label
                {
                    Text = "No vendors found. Click 'Add Vendor' to get started.",
                    Location = new Point(16, 130),
                    Size = new Size(card.Width - 32, 80),
                    Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = TextSecondary,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = White
                };
                Button add = MakeDashboardButton("+ Add Vendor", White, Blue, 118, true);
                add.Location = new Point((card.Width - add.Width) / 2, 210);
                add.Anchor = AnchorStyles.Top;
                add.Click += async (s, e) => await BeginNewVendorAsync();
                card.Resize += (s, e) =>
                {
                    empty.Width = card.Width - 32;
                    add.Left = (card.Width - add.Width) / 2;
                };
                card.Controls.Add(empty);
                card.Controls.Add(add);
                empty.BringToFront();
                add.BringToFront();
            }

            int showingFrom = totalRows == 0 ? 0 : ((_dashboardPage - 1) * DashboardPageSize) + 1;
            int showingTo = Math.Min(totalRows, _dashboardPage * DashboardPageSize);
            card.Controls.Add(new Label { Text = "Showing " + showingFrom + " to " + showingTo + " of " + totalRows + " entries", Location = new Point(16, 378), Size = new Size(260, 18), Font = new Font("Segoe UI", 7.8f), ForeColor = TextSecondary });
            FlowLayoutPanel pager = new FlowLayoutPanel { Location = new Point(card.Width - 184, 372), Size = new Size(170, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = White };
            Button next = MakeDashboardButton(">", White, TextPrimary, 32, true);
            next.Enabled = _dashboardPage < totalPages;
            next.Click += (s, e) => { _dashboardPage++; RenderVendorDashboard(); };
            Button page = MakeDashboardButton(_dashboardPage.ToString(), Blue, White, 34, false);
            Button prev = MakeDashboardButton("<", White, TextPrimary, 32, true);
            prev.Enabled = _dashboardPage > 1;
            prev.Click += (s, e) => { _dashboardPage--; RenderVendorDashboard(); };
            pager.Controls.Add(next);
            pager.Controls.Add(page);
            pager.Controls.Add(prev);
            card.Controls.Add(pager);
            return card;
        }

        private Panel BuildVendorDashboardSidebar()
        {
            Panel sidebar = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(10, 0, 0, 0) };
            TableLayoutPanel stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = PageBg };
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));
            stack.Controls.Add(BuildExpiringDocumentsCard(), 0, 0);
            stack.Controls.Add(BuildQuickActionsCard(), 0, 1);
            sidebar.Controls.Add(stack);
            return sidebar;
        }

        private Panel BuildExpiringDocumentsCard()
        {
            Panel card = MakeDashboardCard("Expiring Documents", "View All", (s, e) => ShowVendorDashboardMessage("Documents", BuildDocumentSummaryText()));
            card.Size = new Size(260, 190);
            var rows = BuildDocumentCounts();
            int y = 48;
            foreach (var row in rows)
            {
                Label name = new Label { Text = row.Name, Location = new Point(16, y), Size = new Size(155, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextPrimary };
                Label count = new Label { Text = row.Count.ToString(), Location = new Point(card.Width - 52, y - 1), Size = new Size(28, 20), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = row.Color, ForeColor = White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold) };
                card.Controls.Add(name);
                card.Controls.Add(count);
                y += 30;
            }
            return card;
        }

        private Panel BuildQuickActionsCard()
        {
            Panel card = MakeDashboardCard("Quick Actions", null, null);
            card.Size = new Size(260, 220);
            card.Controls.Add(new Label
            {
                Text = "Create, review, import, and evaluate vendors from one compact menu.",
                Location = new Point(16, 48),
                Size = new Size(card.Width - 32, 48),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8f),
                ForeColor = TextSecondary
            });
            Button open = MakeDashboardButton("Open Vendor Actions", Blue, White, 220, false);
            open.Location = new Point(16, 108);
            open.Size = new Size(card.Width - 32, 34);
            open.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            open.Click += (s, e) => ShowVendorDashboardActionsMenu(open);
            card.Controls.Add(open);
            return card;
        }

        private void ShowVendorDashboardActionsMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            var actions = new[]
            {
                Tuple.Create("Add Vendor", "Add"),
                Tuple.Create("Add Contact", "Contact"),
                Tuple.Create("New Contract", "Contract"),
                Tuple.Create("Review Vendors", "Review"),
                Tuple.Create("Import / Export", "Import"),
                Tuple.Create("Evaluation", "Eval")
            };
            foreach (var action in actions)
            {
                string command = action.Item2;
                menu.Items.Add(action.Item1, null, async (s, e) => await HandleVendorQuickActionAsync(command));
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private Panel BuildUpcomingRenewalsCard()
        {
            Panel card = MakeDashboardCard("Upcoming Renewals", "View All", (s, e) => ShowVendorDashboardMessage("Upcoming Renewals", BuildRenewalsText()));
            card.Size = new Size(540, 220);
            TableLayoutPanel table = new TableLayoutPanel { Location = new Point(16, 48), Size = new Size(card.Width - 32, 150), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, ColumnCount = 5, RowCount = 1, BackColor = White };
            foreach (float w in new[] { 23f, 28f, 18f, 12f, 19f }) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            string[] headers = { "Vendor", "Contract / Document", "Expiry Date", "Days Left", "Status" };
            for (int i = 0; i < headers.Length; i++) table.Controls.Add(MakeTableLabel(headers[i], true, TextPrimary), i, 0);
            foreach (var r in BuildRenewalRows().Take(5))
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                int row = table.RowCount++;
                table.Controls.Add(MakeTableLabel(r.Vendor, false, TextPrimary), 0, row);
                table.Controls.Add(MakeTableLabel(r.Document, false, TextSecondary), 1, row);
                table.Controls.Add(MakeTableLabel(r.Expiry.ToString("dd MMM yyyy"), false, TextPrimary), 2, row);
                table.Controls.Add(MakeTableLabel(r.DaysLeft.ToString(), false, r.DaysLeft < 0 ? Red : TextPrimary), 3, row);
                table.Controls.Add(MakeTableLabel(r.Status, false, r.DaysLeft < 0 ? Red : (r.DaysLeft <= 7 ? AmberDark : Amber)), 4, row);
            }
            card.Controls.Add(table);
            return card;
        }

        private Panel BuildRecentActivitiesCard()
        {
            Panel card = MakeDashboardCard("Recent Activities", "View All", (s, e) => ShowVendorDashboardMessage("Recent Activities", BuildActivitiesText()));
            card.Size = new Size(540, 220);
            int y = 48;
            foreach (string activity in BuildActivities().Take(5))
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(18, y + 2), Size = new Size(18, 18), ForeColor = Blue, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
                card.Controls.Add(new Label { Text = activity, Location = new Point(42, y), Size = new Size(card.Width - 66, 34), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 8f), ForeColor = TextPrimary, AutoEllipsis = true });
                y += 38;
            }
            if (!BuildActivities().Any())
                AddDashboardEmpty(card, "No vendor activity yet.");
            return card;
        }

        private IEnumerable<VendorSummaryDto> ActiveDashboardVendors()
        {
            return _vendorSummaries.Where(v => !v.IsArchived || GetVendorDashboardStatus(v) == "Blocked");
        }

        private IEnumerable<VendorSummaryDto> FilterDashboardVendors()
        {
            IEnumerable<VendorSummaryDto> query = ActiveDashboardVendors();
            if (!string.Equals(_dashboardTab, "All Vendors", StringComparison.OrdinalIgnoreCase))
                query = query.Where(v => GetVendorDashboardStatus(v) == _dashboardTab);
            if (!string.IsNullOrWhiteSpace(_dashboardCategory) && !string.Equals(_dashboardCategory, "All Categories", StringComparison.OrdinalIgnoreCase))
                query = query.Where(v => NormalizeDashboardCategory(v.Category) == _dashboardCategory);

            string term = GetDashboardSearchText();
            if (!string.IsNullOrWhiteSpace(term))
            {
                string needle = term.Trim().ToUpperInvariant();
                query = query.Where(v =>
                    ("VEN-" + v.VendorId.ToString("D4")).Contains(needle) ||
                    (v.VendorName ?? string.Empty).ToUpperInvariant().Contains(needle) ||
                    (v.Category ?? string.Empty).ToUpperInvariant().Contains(needle) ||
                    (v.Phone ?? string.Empty).ToUpperInvariant().Contains(needle));
            }
            return query.OrderByDescending(v => LastActivityForVendor(v.VendorId)).ThenBy(v => v.VendorName);
        }

        private string GetDashboardSearchText()
        {
            if (_dashboardSearch == null || _dashboardSearch.ForeColor == TextHint)
                return string.Empty;
            return _dashboardSearch.Text ?? string.Empty;
        }

        private string GetVendorDashboardStatus(VendorSummaryDto vendor)
        {
            if (vendor == null) return "Inactive";
            if (vendor.IsArchived) return "Blocked";
            if (!vendor.IsActive) return "Inactive";
            if (string.IsNullOrWhiteSpace(vendor.Phone) && string.IsNullOrWhiteSpace(vendor.Category)) return "Pending Approval";
            return "Active";
        }

        private double GetVendorRating(VendorSummaryDto vendor)
        {
            if (vendor == null) return 0;
            double rating = 4.4;
            if (vendor.HasOverdue) rating -= 1.0;
            if (vendor.OutstandingBalance > 0) rating -= 0.4;
            if (vendor.OpenPOCount > 0) rating += 0.2;
            if (!vendor.IsActive) rating -= 0.6;
            if (vendor.IsArchived) rating -= 1.0;
            if (vendor.TotalPurchased > 0) rating += 0.2;
            return Math.Max(0, Math.Min(5, rating));
        }

        private string NormalizeDashboardCategory(string category)
        {
            string c = (category ?? string.Empty).ToUpperInvariant();
            if (c.Contains("EQUIP") || c.Contains("HVAC")) return "Equipment";
            if (c.Contains("SERVICE") || c.Contains("LABOUR") || c.Contains("LABOR")) return "Services";
            if (c.Contains("CHEM") || c.Contains("REFRIG") || c.Contains("CONSUM")) return "Consumables";
            if (c.Contains("COPPER") || c.Contains("PIPE") || c.Contains("ELECT") || c.Contains("TOOL") || c.Contains("COMP")) return "Components";
            return "Others";
        }

        private string CategoryForVendorId(int vendorId)
        {
            VendorSummaryDto vendor = _vendorSummaries.FirstOrDefault(v => v.VendorId == vendorId);
            return NormalizeDashboardCategory(vendor?.Category);
        }

        private string VendorNameById(int vendorId, string fallback)
        {
            VendorSummaryDto vendor = _vendorSummaries.FirstOrDefault(v => v.VendorId == vendorId);
            return !string.IsNullOrWhiteSpace(vendor?.VendorName) ? vendor.VendorName : (string.IsNullOrWhiteSpace(fallback) ? "Vendor #" + vendorId : fallback);
        }

        private DateTime LastActivityForVendor(int vendorId)
        {
            DateTime fromPo = _dashboardPurchases.Where(p => p.VendorID == vendorId).Select(p => p.ModifiedDate ?? p.CreatedByDate ?? p.CreatedDate).DefaultIfEmpty(DateTime.MinValue).Max();
            VendorSummaryDto vendor = _vendorSummaries.FirstOrDefault(v => v.VendorId == vendorId);
            if (vendor != null && vendor.TotalPurchased > 0 && fromPo == DateTime.MinValue)
                return DateTime.Today;
            return fromPo == DateTime.MinValue ? DateTime.Today.AddDays(-3650) : fromPo;
        }

        private Color StatusColor(string status)
        {
            if (status == "Active") return Teal;
            if (status == "Pending Approval") return Amber;
            if (status == "Blocked") return Red;
            return TextHint;
        }

        private Color CategoryColor(string category)
        {
            switch (NormalizeDashboardCategory(category))
            {
                case "Components": return Blue;
                case "Equipment": return Color.FromArgb(124, 58, 237);
                case "Consumables": return Color.FromArgb(20, 184, 166);
                case "Services": return Teal;
                default: return TextHint;
            }
        }

        private Panel MakeDashboardCard(string title, string linkText, EventHandler linkClick)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, Size = new Size(320, 220), Margin = new Padding(0, 0, 10, 0), BackColor = White };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(White))
                using (Pen pen = new Pen(BorderLight))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            card.Controls.Add(new Label { Text = title, Location = new Point(16, 12), Size = new Size(260, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary });
            if (!string.IsNullOrWhiteSpace(linkText))
            {
                LinkLabel link = new LinkLabel { Text = linkText, LinkColor = Blue, Location = new Point(card.Width - 84, 14), Size = new Size(72, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.8f), TextAlign = ContentAlignment.TopRight };
                if (linkClick != null) link.Click += linkClick;
                card.Controls.Add(link);
            }
            return card;
        }

        private Button MakeDashboardButton(string text, Color bg, Color fg, int width, bool outline)
        {
            Button button = new Button { Text = text, Size = new Size(width, 30), FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = Border;
            return button;
        }

        private Control MakeDashboardStatCard(int width, string label, string value, string subLabel, Color iconBg, Color iconFg)
        {
            Panel card = new Panel { Size = new Size(width, 86), BackColor = White, Margin = new Padding(0, 0, 12, 0) };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(White))
                using (Pen pen = new Pen(BorderLight))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            Label icon = new Label { Text = "□", Location = new Point(16, 22), Size = new Size(42, 42), BackColor = iconBg, ForeColor = iconFg, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15f, FontStyle.Bold) };
            card.Controls.Add(icon);
            card.Controls.Add(new Label { Text = label, Location = new Point(72, 16), Size = new Size(width - 88, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextSecondary, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = value, Location = new Point(72, 34), Size = new Size(width - 88, 24), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = subLabel, Location = new Point(72, 60), Size = new Size(width - 88, 18), Font = new Font("Segoe UI", 7.5f), ForeColor = subLabel.Contains("-") || subLabel.Contains("Blocked") ? Red : Teal, AutoEllipsis = true });
            return card;
        }

        private Panel MakeDonutPanel(List<DashboardSlice> slices, string center, string subtitle, Point location, Size size)
        {
            Panel donut = new Panel { Location = location, Size = size, BackColor = White };
            donut.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                decimal total = Math.Max(1m, slices.Sum(sl => sl.ValueDecimal > 0 ? sl.ValueDecimal : sl.Value));
                float start = -90f;
                Rectangle rect = new Rectangle(6, 6, donut.Width - 12, donut.Height - 12);
                foreach (DashboardSlice slice in slices.Where(sl => (sl.ValueDecimal > 0 ? sl.ValueDecimal : sl.Value) > 0))
                {
                    decimal val = slice.ValueDecimal > 0 ? slice.ValueDecimal : slice.Value;
                    float sweep = (float)(360m * val / total);
                    using (Pen pen = new Pen(slice.Color, 18f))
                        e.Graphics.DrawArc(pen, rect, start, sweep);
                    start += sweep;
                }
                using (SolidBrush brush = new SolidBrush(White))
                    e.Graphics.FillEllipse(brush, new Rectangle(28, 28, donut.Width - 56, donut.Height - 56));
                TextRenderer.DrawText(e.Graphics, center, new Font("Segoe UI", 11f, FontStyle.Bold), new Rectangle(18, 40, donut.Width - 36, 24), TextPrimary, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(e.Graphics, subtitle, new Font("Segoe UI", 7f), new Rectangle(18, 62, donut.Width - 36, 18), TextSecondary, TextFormatFlags.HorizontalCenter);
            };
            return donut;
        }

        private Panel MakeGaugePanel(double rating, Point location, Size size)
        {
            Panel gauge = new Panel { Location = location, Size = size, BackColor = White };
            gauge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(10, 12, gauge.Width - 20, gauge.Height * 2 - 22);
                using (Pen redPen = new Pen(Red, 12f)) e.Graphics.DrawArc(redPen, rect, 180, 54);
                using (Pen amberPen = new Pen(Amber, 12f)) e.Graphics.DrawArc(amberPen, rect, 234, 54);
                using (Pen greenPen = new Pen(Teal, 12f)) e.Graphics.DrawArc(greenPen, rect, 288, 72);
                float needle = 180f + (float)(Math.Max(0, Math.Min(5, rating)) / 5d * 180d);
                double rad = needle * Math.PI / 180d;
                Point center = new Point(gauge.Width / 2, gauge.Height - 10);
                Point tip = new Point(center.X + (int)(Math.Cos(rad) * (gauge.Width / 2 - 24)), center.Y + (int)(Math.Sin(rad) * (gauge.Width / 2 - 24)));
                using (Pen pen = new Pen(TextPrimary, 2f)) e.Graphics.DrawLine(pen, center, tip);
            };
            return gauge;
        }

        private void AddLegend(Panel card, List<DashboardSlice> slices, int x, int y, int total)
        {
            foreach (DashboardSlice slice in slices)
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(x, y), Size = new Size(16, 16), ForeColor = slice.Color });
                card.Controls.Add(new Label { Text = slice.Name, Location = new Point(x + 18, y), Size = new Size(112, 16), Font = new Font("Segoe UI", 7.4f), ForeColor = TextPrimary, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = slice.Value + " (" + PercentText(slice.Value, total) + ")", Location = new Point(x + 132, y), Size = new Size(78, 16), Font = new Font("Segoe UI", 7.2f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight });
                y += 28;
            }
        }

        private void AddLegend(Panel card, List<DashboardSlice> slices, int x, int y, decimal total)
        {
            foreach (DashboardSlice slice in slices.Take(5))
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(x, y), Size = new Size(16, 16), ForeColor = slice.Color });
                card.Controls.Add(new Label { Text = slice.Name, Location = new Point(x + 18, y), Size = new Size(96, 16), Font = new Font("Segoe UI", 7.2f), ForeColor = TextPrimary, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = CompactCurrency(slice.ValueDecimal) + " (" + PercentText(slice.ValueDecimal, total) + ")", Location = new Point(x + 112, y), Size = new Size(102, 16), Font = new Font("Segoe UI", 7.1f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight });
                y += 27;
            }
        }

        private void AddVendorDashboardTableRow(TableLayoutPanel table, VendorSummaryDto v)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            int row = table.RowCount++;
            string status = GetVendorDashboardStatus(v);
            decimal spend = VendorSpendThisMonth(v.VendorId);
            table.Controls.Add(MakeTableLabel("VEN-" + v.VendorId.ToString("D4"), false, TextSecondary), 0, row);
            table.Controls.Add(MakeTableLabel(v.VendorName, false, TextPrimary), 1, row);
            table.Controls.Add(MakeTableLabel(NormalizeDashboardCategory(v.Category), false, CategoryColor(v.Category)), 2, row);
            table.Controls.Add(MakeTableLabel(status, false, StatusColor(status)), 3, row);
            table.Controls.Add(MakeTableLabel(RatingStars(GetVendorRating(v)) + " " + GetVendorRating(v).ToString("0.0"), false, Amber), 4, row);
            table.Controls.Add(MakeTableLabel(IndiaFormatHelper.FormatCurrency(spend), false, TextPrimary), 5, row);
            table.Controls.Add(MakeTableLabel(IndiaFormatHelper.FormatCurrency(v.OutstandingBalance), false, v.OutstandingBalance > 0 ? Red : TextPrimary), 6, row);
            DateTime last = LastActivityForVendor(v.VendorId);
            table.Controls.Add(MakeTableLabel(last.Year < 2000 ? "No activity" : last.ToString("dd MMM yyyy"), false, TextSecondary), 7, row);
            Button actions = MakeDashboardButton("⋯", White, TextPrimary, 30, true);
            actions.Click += (s, e) => ShowVendorRowMenu(v, actions);
            table.Controls.Add(actions, 8, row);
        }

        private Label MakeTableLabel(string text, bool header, Color color)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", header ? 7.6f : 7.4f, header ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(4, 0, 4, 0), BackColor = header ? Color.FromArgb(248, 250, 252) : White };
        }

        private Button MakeTabButton(string text, bool active)
        {
            Button b = new Button { Text = text, Height = 28, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = White, ForeColor = active ? Blue : TextSecondary, Font = new Font("Segoe UI", 7.4f, active ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 12, 0) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private Label MakeDashboardIconBadge(string text)
        {
            return new Label { Text = text, Size = new Size(30, 30), BackColor = RedLightBg, ForeColor = Red, Font = new Font("Segoe UI", 9f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
        }

        private Panel BuildVendorSessionPanel()
        {
            AppUserDto user = SessionManager.CurrentUser;
            string name = !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName : (!string.IsNullOrWhiteSpace(user?.Username) ? user.Username : Environment.UserName);
            string role = !string.IsNullOrWhiteSpace(user?.RoleName) ? user.RoleName : "User";
            Panel panel = new Panel { Size = new Size(150, 38), BackColor = PageBg };
            panel.Controls.Add(MakeInitialsAvatar(name, 0, 4, BlueLightBg, Blue));
            panel.Controls.Add(new Label { Text = name, Location = new Point(38, 1), Size = new Size(92, 17), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = role, Location = new Point(38, 18), Size = new Size(84, 15), Font = new Font("Segoe UI", 7.2f), ForeColor = TextSecondary, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = "v", Location = new Point(132, 8), Size = new Size(14, 18), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private Label MakeInitialsAvatar(string name, int x, int y, Color bg, Color fg)
        {
            string initials = string.Concat((name ?? "V").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
            if (string.IsNullOrWhiteSpace(initials)) initials = "V";
            return new Label { Text = initials, Location = new Point(x, y), Size = new Size(30, 30), BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        }

        private void AddDistributionRow(Panel card, string label, int count, int total, Color color, int x, int y)
        {
            card.Controls.Add(new Label { Text = "●", Location = new Point(x, y), Size = new Size(16, 16), ForeColor = color });
            card.Controls.Add(new Label { Text = label, Location = new Point(x + 18, y), Size = new Size(120, 16), Font = new Font("Segoe UI", 7.2f), ForeColor = TextPrimary, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = count + " (" + PercentText(count, total) + ")", Location = new Point(x + 142, y), Size = new Size(78, 16), Font = new Font("Segoe UI", 7.2f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight });
        }

        private void AddDashboardEmpty(Panel card, string text)
        {
            card.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleCenter });
        }

        private decimal VendorSpendThisMonth(int vendorId)
        {
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return _dashboardPurchases.Where(p => p.VendorID == vendorId && p.PODate.Date >= monthStart && p.PODate.Date <= DateTime.Today).Sum(p => p.TotalAmount);
        }

        private string TrendForNewVendors()
        {
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime lastMonthStart = monthStart.AddMonths(-1);
            DateTime lastMonthEnd = monthStart.AddDays(-1);
            int current = _vendorSummaries.Count(v => LastActivityForVendor(v.VendorId).Date >= monthStart);
            int previous = _vendorSummaries.Count(v => LastActivityForVendor(v.VendorId).Date >= lastMonthStart && LastActivityForVendor(v.VendorId).Date <= lastMonthEnd);
            return (current - previous >= 0 ? "+" : "") + (current - previous) + " (" + PercentText(current - previous, Math.Max(1, previous)) + ") vs last month";
        }

        private string TrendPercent(decimal current, decimal previous)
        {
            if (previous == 0) return current == 0 ? "0.0%" : "+100.0%";
            decimal pct = ((current - previous) / previous) * 100m;
            return (pct >= 0 ? "+" : "") + pct.ToString("0.0") + "%";
        }

        private string PercentText(int value, int total)
        {
            if (total <= 0) return "0.0%";
            return ((value * 100m) / total).ToString("0.0") + "%";
        }

        private string PercentText(decimal value, decimal total)
        {
            if (total <= 0) return "0.0%";
            return ((value * 100m) / total).ToString("0.0") + "%";
        }

        private string CompactCurrency(decimal value)
        {
            decimal abs = Math.Abs(value);
            if (abs >= 10000000m) return "₹" + (value / 10000000m).ToString("0.##") + " Cr";
            if (abs >= 100000m) return "₹" + (value / 100000m).ToString("0.##") + " L";
            return IndiaFormatHelper.FormatCurrency(value);
        }

        private string RatingStars(double rating)
        {
            int filled = (int)Math.Round(Math.Max(0, Math.Min(5, rating)), MidpointRounding.AwayFromZero);
            return new string('★', filled) + new string('☆', Math.Max(0, 5 - filled));
        }

        private void ShowVendorRowMenu(VendorSummaryDto vendor, Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("View", null, async (s, e) => await OpenVendorEditorAsync(vendor.VendorId));
            menu.Items.Add("Edit", null, async (s, e) => await OpenVendorEditorAsync(vendor.VendorId));
            menu.Items.Add("Add Contact", null, (s, e) => ShowVendorDashboardMessage("Add Contact", "Open the vendor record and update the contact fields."));
            menu.Items.Add("New PO", null, (s, e) => _vendorSvc.RaiseQuickPO(vendor.VendorId));
            menu.Items.Add("Block", null, async (s, e) => await ArchiveVendorFromDashboardAsync(vendor));
            menu.Items.Add("Delete", null, async (s, e) => await ArchiveVendorFromDashboardAsync(vendor));
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private async Task ArchiveVendorFromDashboardAsync(VendorSummaryDto vendor)
        {
            if (vendor == null) return;
            DialogResult result = MessageBox.Show("Archive " + vendor.VendorName + "?", "Vendor Management", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
            await Task.Run(() => _vendorSvc.ArchiveVendor(vendor.VendorId));
            await RefreshDashboardAsync();
        }

        private async Task HandleVendorQuickActionAsync(string action)
        {
            if (action == "Add")
                await BeginNewVendorAsync();
            else if (action == "Import")
                ImportUiHelper.RunImport(ExcelImportModule.Vendors, FindForm());
            else if (action == "Contract")
                (FindForm() as MainForm)?.NavigateTo("Contracts");
            else
                ShowVendorDashboardMessage("Vendor " + action, "Select a vendor row to continue this workflow.");
        }

        private List<DashboardDocCount> BuildDocumentCounts()
        {
            var vendors = ActiveDashboardVendors().ToList();
            return new List<DashboardDocCount>
            {
                new DashboardDocCount { Name = "GST Certificate", Count = vendors.Count(v => string.IsNullOrWhiteSpace(v.Phone)), Color = Red },
                new DashboardDocCount { Name = "MSME Certificate", Count = vendors.Count(v => !string.Equals(v.MSMERegistered, "No", StringComparison.OrdinalIgnoreCase)), Color = Amber },
                new DashboardDocCount { Name = "Insurance Policy", Count = _dashboardPurchases.Count(p => p.IsOverdue), Color = Red },
                new DashboardDocCount { Name = "PAN Card", Count = vendors.Count(v => string.IsNullOrWhiteSpace(v.Category)), Color = Blue },
                new DashboardDocCount { Name = "ISO Certificate", Count = vendors.Count(v => v.HasOverdue), Color = Teal }
            };
        }

        private List<RenewalRow> BuildRenewalRows()
        {
            List<RenewalRow> rows = new List<RenewalRow>();
            foreach (PurchaseOrder po in _dashboardPurchases.Where(p => p.PayByDate.Date <= DateTime.Today.AddDays(60) && p.BalanceDue > 0))
            {
                int days = (po.PayByDate.Date - DateTime.Today).Days;
                rows.Add(new RenewalRow { Vendor = VendorNameById(po.VendorID, po.VendorName), Document = string.IsNullOrWhiteSpace(po.PONumber) ? "Purchase payable" : po.PONumber, Expiry = po.PayByDate.Date, DaysLeft = days, Status = days < 0 ? "Expired" : days <= 7 ? "Expiring" : "Due Soon" });
            }
            foreach (AMCContract contract in _dashboardContracts.Where(c => c.EndDate.Date <= DateTime.Today.AddDays(60)))
            {
                int days = (contract.EndDate.Date - DateTime.Today).Days;
                rows.Add(new RenewalRow { Vendor = "Contract #" + contract.ContractID, Document = string.IsNullOrWhiteSpace(contract.ContractType) ? "Contract" : contract.ContractType, Expiry = contract.EndDate.Date, DaysLeft = days, Status = days < 0 ? "Expired" : days <= 7 ? "Expiring" : "Due Soon" });
            }
            return rows.OrderBy(r => r.Expiry).ToList();
        }

        private IEnumerable<string> BuildActivities()
        {
            foreach (PurchaseOrder po in _dashboardPurchases.OrderByDescending(p => p.ModifiedDate ?? p.CreatedByDate ?? p.CreatedDate).Take(10))
            {
                DateTime ts = po.ModifiedDate ?? po.CreatedByDate ?? po.CreatedDate;
                string actor = !string.IsNullOrWhiteSpace(po.ModifiedByName) ? po.ModifiedByName : (!string.IsNullOrWhiteSpace(po.CreatedByName) ? po.CreatedByName : CurrentUserName());
                if (po.PaidAmount > 0)
                    yield return "Vendor " + VendorNameById(po.VendorID, po.VendorName) + " payment of " + IndiaFormatHelper.FormatCurrency(po.PaidAmount) + " recorded. " + ts.ToString("dd MMM yyyy") + " by " + actor;
                else
                    yield return "Purchase order " + po.PONumber + " created for " + VendorNameById(po.VendorID, po.VendorName) + ". " + ts.ToString("dd MMM yyyy") + " by " + actor;
            }
            foreach (VendorSummaryDto vendor in _vendorSummaries.OrderByDescending(v => LastActivityForVendor(v.VendorId)).Take(5))
                yield return "Vendor " + vendor.VendorName + " reviewed. " + LastActivityForVendor(vendor.VendorId).ToString("dd MMM yyyy") + " by " + CurrentUserName();
        }

        private string CurrentUserName()
        {
            AppUserDto user = SessionManager.CurrentUser;
            return !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName : (!string.IsNullOrWhiteSpace(user?.Username) ? user.Username : Environment.UserName);
        }

        private string BuildVendorAlertText()
        {
            int overdue = _dashboardPurchases.Count(p => p.IsOverdue);
            int duplicates = _duplicateGroups.Count;
            int blocked = ActiveDashboardVendors().Count(v => GetVendorDashboardStatus(v) == "Blocked");
            return overdue + " overdue payable records.\r\n" + duplicates + " duplicate vendor groups.\r\n" + blocked + " blocked vendors.";
        }

        private string BuildTopVendorsText()
        {
            DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return string.Join(Environment.NewLine, _dashboardPurchases.Where(p => p.PODate.Date >= monthStart).GroupBy(p => p.VendorID).OrderByDescending(g => g.Sum(p => p.TotalAmount)).Take(10).Select(g => VendorNameById(g.Key, g.First().VendorName) + " - " + IndiaFormatHelper.FormatCurrency(g.Sum(p => p.TotalAmount))));
        }

        private string BuildDocumentSummaryText()
        {
            return string.Join(Environment.NewLine, BuildDocumentCounts().Select(d => d.Name + ": " + d.Count));
        }

        private string BuildRenewalsText()
        {
            return string.Join(Environment.NewLine, BuildRenewalRows().Take(10).Select(r => r.Vendor + " - " + r.Document + " - " + r.Expiry.ToString("dd MMM yyyy")));
        }

        private string BuildActivitiesText()
        {
            return string.Join(Environment.NewLine, BuildActivities().Take(10));
        }

        private void ShowVendorDashboardMessage(string title, string message)
        {
            MessageBox.Show(string.IsNullOrWhiteSpace(message) ? "No records available." : message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private sealed class DashboardSlice
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public decimal ValueDecimal { get; set; }
            public Color Color { get; set; }
        }

        private sealed class DashboardDocCount
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public Color Color { get; set; }
        }

        private sealed class RenewalRow
        {
            public string Vendor { get; set; }
            public string Document { get; set; }
            public DateTime Expiry { get; set; }
            public int DaysLeft { get; set; }
            public string Status { get; set; }
        }

        private void BuildLeftPanel()
        {
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = White };
            left.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, left.Width - 1, 0, left.Width - 1, left.Height);
            };

            _searchSection = new Panel { Dock = DockStyle.Top, Height = 152, Padding = new Padding(12, 10, 12, 10), BackColor = White };
            Label railTitle = new Label
            {
                Text = "VENDOR MANAGEMENT",
                Location = new Point(12, 10),
                Size = new Size(260, 24),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            Panel searchWrap = new Panel { Location = new Point(12, 48), Size = new Size(276, 38), BackColor = White };
            searchWrap.Paint += (s, e) => DrawSearchBox(e.Graphics, searchWrap.ClientRectangle);
            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = White,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(12, 10),
                Width = 224
            };
            ConfigurePlaceholder(_txtSearch, "Search vendors, category, city...");
            _txtSearch.TextChanged += (s, e) => { UpdateSearchClear(); ApplyFilters(); };
            _btnClearSearch = new Button
            {
                Text = "x",
                FlatStyle = FlatStyle.Flat,
                BackColor = White,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Size = new Size(28, 24),
                Location = new Point(240, 7),
                Visible = false
            };
            _btnClearSearch.FlatAppearance.BorderSize = 0;
            _btnClearSearch.Click += (s, e) => _txtSearch.Text = string.Empty;
            searchWrap.Controls.Add(_txtSearch);
            searchWrap.Controls.Add(_btnClearSearch);

            Button filterButton = MakeActionButton("Filter", White, TextPrimary, 40, true);
            filterButton.Location = new Point(292, 48);
            filterButton.Click += (s, e) => _txtSearch.Focus();

            _chipFlow = new FlowLayoutPanel
            {
                Location = new Point(12, 100),
                Size = new Size(276, 36),
                BackColor = White,
                WrapContents = true,
                AutoScroll = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            _searchSection.Controls.Add(railTitle);
            _searchSection.Controls.Add(searchWrap);
            _searchSection.Controls.Add(filterButton);
            _searchSection.Controls.Add(_chipFlow);

            _duplicateBanner = new Panel { Dock = DockStyle.Top, Height = 0, BackColor = RedLightBg, Padding = new Padding(12, 7, 12, 7), Visible = false };
            _duplicateBanner.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(247, 193, 193)))
                    e.Graphics.DrawLine(pen, 0, _duplicateBanner.Height - 1, _duplicateBanner.Width, _duplicateBanner.Height - 1);
            };
            _lblDuplicateBanner = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = RedDark };
            _duplicateBanner.Controls.Add(_lblDuplicateBanner);

            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(8, 8, 8, 8) };
            _vendorListFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4, 0, 4, 8)
            };
            listWrap.Controls.Add(_vendorListFlow);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = White, Padding = new Padding(12, 0, 12, 8) };
            _lblListFooter = new Label { Dock = DockStyle.Fill, ForeColor = TextHint, Font = new Font("Segoe UI", 8.5f), Text = "Loading..." };
            _lnkClearSearch = new LinkLabel { Dock = DockStyle.Right, Width = 90, Text = "Clear search", LinkColor = Blue, TextAlign = ContentAlignment.MiddleRight, Visible = false };
            _lnkClearSearch.Click += (s, e) => _txtSearch.Text = string.Empty;
            footer.Controls.Add(_lblListFooter);
            footer.Controls.Add(_lnkClearSearch);

            left.Controls.Add(listWrap);
            left.Controls.Add(footer);
            left.Controls.Add(_duplicateBanner);
            left.Controls.Add(_searchSection);
            _split.Panel1.Controls.Add(left);
            _split.Panel1.Resize += (s, e) => UpdateSearchSectionLayout();
        }

        private void BuildRightPanel()
        {
            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            _topBar = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = PageBg, Padding = new Padding(36, 14, 24, 10) };
            BuildTopBar();

            _rightScroll = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, AutoScroll = true, Padding = new Padding(36, 12, 24, 20) };
            _rightScroll.HorizontalScroll.Enabled = false;
            _rightScroll.HorizontalScroll.Visible = false;
            _rightHost = new Panel { BackColor = PageBg, Location = new Point(_rightScroll.Padding.Left, _rightScroll.Padding.Top), Size = new Size(1000, 900) };
            _rightScroll.Controls.Add(_rightHost);
            _rightScroll.Resize += (s, e) =>
            {
                _rightHost.Location = new Point(_rightScroll.Padding.Left, _rightScroll.Padding.Top);
                if (_rightScroll.HorizontalScroll.Visible)
                    _rightScroll.HorizontalScroll.Visible = false;
                _rightScroll.HorizontalScroll.Value = 0;
            };

            BuildHeaderRow();
            BuildStatsRow();
            BuildWarningStrip();
            BuildCards();

            _rightHost.Controls.Add(_cardNotes);
            _rightHost.Controls.Add(_cardRecentPos);
            _rightHost.Controls.Add(_cardIdentity);
            _rightHost.Controls.Add(_warningStrip);
            _rightHost.Controls.Add(_statsRow);
            _rightHost.Controls.Add(_vendorHeaderRow);

            right.Controls.Add(_rightScroll);
            right.Controls.Add(_topBar);
            _split.Panel2.Controls.Add(right);
        }

        private void BuildTopBar()
        {
            Label title = new Label
            {
                Text = "New Vendor",
                Location = new Point(36, 16),
                Size = new Size(280, 30),
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = PageBg
            };

            _lblTopBadge = CreateBadgeLabel(90, TealLightBg, Color.FromArgb(15, 110, 86));
            _lblDuplicateBadge = CreateBadgeLabel(150, RedLightBg, RedDark);
            _lblDuplicateBadge.Visible = false;

            FlowLayoutPanel badgeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 0,
                Visible = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = _topBar.BackColor,
                Padding = new Padding(6, 10, 0, 0)
            };
            badgeFlow.Controls.Add(_lblTopBadge);
            badgeFlow.Controls.Add(_lblDuplicateBadge);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 870,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = _topBar.BackColor,
                Padding = new Padding(0, 4, 0, 0),
                AutoScroll = false
            };

            _btnMerge = MakeActionButton("Merge duplicates", White, RedDark, 118, true);
            _btnMerge.Visible = false;
            _btnMerge.Click += (s, e) => ShowMergeDialog();

            _btnTemplate = MakeActionButton("Template", White, TextPrimary, 82, true);
            _btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Vendors, FindForm());

            _btnImport = MakeActionButton("Import", White, TextPrimary, 78, true);
            _btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Vendors, FindForm());

            Button btnExport = MakeActionButton("Export", White, TextPrimary, 78, true);
            btnExport.Click += (s, e) => ExportCsv();

            Button btnRefresh = MakeActionButton("Refresh", White, TextPrimary, 78, true);
            btnRefresh.Click += async (s, e) => await RefreshAsync(true);

            Button btnForms = MakeActionButton("Forms", White, Blue, 82, true);
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Vendor Management", "Inventory / Purchases", null, "vendor purchase request goods received note invoice approval supplier parts consumed GST bank details");

            _btnWhatsApp = MakeActionButton("WhatsApp", White, WhatsAppColor(), 90, true);
            _btnWhatsApp.Click += (s, e) => ShowVendorWhatsAppAction();

            Button btnDashboard = MakeActionButton("<- Dashboard", White, TextPrimary, 108, true);
            btnDashboard.Click += async (s, e) => await BackToVendorDashboardAsync();

            Button btnNew = MakeActionButton("+ New Vendor", DS.Primary600, White, 112, false);
            btnNew.Click += async (s, e) => await BeginNewVendorAsync();

            actions.Controls.Add(btnNew);
            actions.Controls.Add(btnDashboard);
            actions.Controls.Add(btnRefresh);
            actions.Controls.Add(btnExport);
            actions.Controls.Add(_btnImport);
            actions.Controls.Add(_btnTemplate);
            actions.Controls.Add(btnForms);
            actions.Controls.Add(_btnWhatsApp);
            actions.Controls.Add(_btnMerge);

            Label breadcrumb = new Label
            {
                Text = "Vendor Management  >  New Vendor",
                Location = new Point(36, 48),
                Size = new Size(320, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = TextSecondary,
                BackColor = PageBg
            };
            _topBar.Controls.Add(breadcrumb);
            _topBar.Controls.Add(actions);
            _topBar.Controls.Add(badgeFlow);
            _topBar.Controls.Add(title);
        }

        private void BuildHeaderRow()
        {
            _vendorHeaderRow = new Panel { BackColor = White, Height = 72, Width = 900 };
            _vendorHeaderRow.Paint += (s, e) => DrawCardOutline(e.Graphics, _vendorHeaderRow.ClientRectangle);

            Panel textWrap = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(16, 10, 16, 10) };
            _lblHeaderName = new Label { Text = "Select a vendor", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 18f, FontStyle.Regular), ForeColor = TextPrimary };
            _lblHeaderMeta = new Label { Text = "Supplier - City - Added", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 10f), ForeColor = TextSecondary };
            textWrap.Controls.Add(_lblHeaderMeta);
            textWrap.Controls.Add(_lblHeaderName);

            TableLayoutPanel actions = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 430,
                BackColor = White,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(0, 18, 16, 0)
            };
            for (int i = 0; i < 4; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            _btnRaisePo = MakeActionButton("Raise PO", White, TextPrimary, 96, true);
            _btnRaisePo.Click += (s, e) => RaisePo();
            _btnViewPos = MakeActionButton("View POs", White, TextPrimary, 96, true);
            _btnViewPos.Click += (s, e) => ViewPurchaseOrders();
            _btnSave = MakeActionButton("Save", Teal, White, 96, false);
            _btnSave.Click += async (s, e) => await SaveVendorAsync(true);
            _btnArchive = MakeActionButton("Archive", White, RedDark, 96, true);
            _btnArchive.FlatAppearance.BorderColor = Color.FromArgb(247, 193, 193);
            _btnArchive.Click += async (s, e) => await ArchiveVendorAsync();

            foreach (Button button in new[] { _btnRaisePo, _btnViewPos, _btnSave, _btnArchive })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(4, 0, 4, 0);
            }
            actions.Controls.Add(_btnRaisePo, 0, 0);
            actions.Controls.Add(_btnViewPos, 1, 0);
            actions.Controls.Add(_btnArchive, 2, 0);
            actions.Controls.Add(_btnSave, 3, 0);

            _vendorHeaderRow.Controls.Add(actions);
            _vendorHeaderRow.Controls.Add(textWrap);
        }

        private void BuildStatsRow()
        {
            _statsRow = new Panel { BackColor = PageBg, Height = 86, Width = 900 };
            string[] labels = { "OUTSTANDING", "TOTAL PURCHASED", "OPEN POS", "CREDIT DAYS" };
            Label[] values = new Label[4];

            for (int i = 0; i < 4; i++)
            {
                Panel card = new Panel { BackColor = Surface, Size = new Size(205, 70), Location = new Point(i * 220, 0) };
                card.Paint += (s, e) => DrawRoundedSurface(e.Graphics, card.ClientRectangle, Surface, 8);
                Label lbl = new Label { Text = labels[i], Location = new Point(70, 12), Size = new Size(i == 3 ? 92 : 160, 14), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextSecondary };
                Label val = new Label { Text = "-", Location = new Point(70, 30), Size = new Size(i == 3 ? 72 : 150, 28), Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = i == 1 ? Teal : i == 0 ? Blue : TextPrimary };
                ModernIconKind kind = i == 0 ? ModernIconKind.Payment : i == 1 ? ModernIconKind.Purchase : i == 2 ? ModernIconKind.Document : ModernIconKind.Calendar;
                Label icon = ModernIconSystem.Badge(kind, 40, i == 0 ? BlueLightBg : i == 1 ? TealLightBg : i == 2 ? AmberLightBg : Color.FromArgb(219, 234, 254), i == 0 ? Blue : i == 1 ? Teal : i == 2 ? Amber : Blue, 12);
                icon.Location = new Point(18, 18);
                values[i] = val;
                card.Controls.Add(icon);
                card.Controls.Add(lbl);
                card.Controls.Add(val);
                if (i == 3)
                    AddStatsActions(card);
                _statsRow.Controls.Add(card);
            }

            _lblStatOutstanding = values[0];
            _lblStatPurchased = values[1];
            _lblStatOpenPos = values[2];
            _lblStatCreditDays = values[3];
        }

        private void AddStatsActions(Panel card)
        {
            if (_btnRaisePo == null || _btnArchive == null || _btnSave == null)
                return;

            foreach (Button button in new[] { _btnRaisePo, _btnArchive, _btnSave })
            {
                button.Dock = DockStyle.None;
                button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                button.Height = 32;
                button.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                button.Margin = Padding.Empty;
                card.Controls.Add(button);
                button.BringToFront();
            }
            card.Resize += (s, e) => LayoutStatsActions(card);
            card.HandleCreated += (s, e) => LayoutStatsActions(card);
            LayoutStatsActions(card);
        }

        private void LayoutStatsActions(Panel card)
        {
            int height = 30;
            int saveWidth = 64;
            int archiveWidth = 76;
            int raiseWidth = 82;
            int gap = 8;
            int totalWidth = raiseWidth + archiveWidth + saveWidth + (gap * 2);
            int preferredStartX = Math.Max(110, card.ClientSize.Width - totalWidth - 72);
            int maxStartX = Math.Max(78, card.ClientSize.Width - totalWidth - 36);
            int startX = Math.Min(preferredStartX, maxStartX);
            int y = 34;

            _btnRaisePo.SetBounds(startX, y, raiseWidth, height);
            _btnArchive.SetBounds(_btnRaisePo.Right + gap, y, archiveWidth, height);
            _btnSave.SetBounds(_btnArchive.Right + gap, y, saveWidth, height);
            foreach (Button button in new[] { _btnRaisePo, _btnArchive, _btnSave })
            {
                button.Height = height;
                button.Font = new Font("Segoe UI", 7.8f, FontStyle.Bold);
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.FlatAppearance.BorderSize = button == _btnRaisePo ? 1 : 0;
                button.FlatAppearance.BorderColor = button == _btnRaisePo ? Border : button.FlatAppearance.BorderColor;
                button.FlatAppearance.MouseOverBackColor = button.BackColor == White ? Surface : ControlPaint.Light(button.BackColor);
                button.FlatAppearance.MouseDownBackColor = button.BackColor == White ? BorderLight : ControlPaint.Dark(button.BackColor);
                DS.Rounded(button, 7);
            }
            _btnSave.BringToFront();
            _btnArchive.BringToFront();
            _btnRaisePo.BringToFront();
        }

        private void BuildWarningStrip()
        {
            _warningStrip = new Panel { BackColor = AmberLightBg, Height = 34, Width = 900, Visible = false, Padding = new Padding(10, 6, 10, 6) };
            _warningStrip.Paint += (s, e) => DrawRoundedSurface(e.Graphics, _warningStrip.ClientRectangle, AmberLightBg, 6);
            _lblWarningStrip = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(133, 79, 11) };
            _warningStrip.Controls.Add(_lblWarningStrip);
        }

        private void BuildCards()
        {
            _cardIdentity = CreateCard("vendor_details", "Vendor details", 410);
            _cardIdentity.AllowResize = false;
            _cardIdentity.ShowHeader = false;
            _cardContact = CreateCard("contact_details", "Contact details", 310);
            _cardTax = CreateCard("tax_compliance", "Tax & compliance", 320);
            _cardPayment = CreateCard("payment_terms", "Payment terms", 320);
            _cardRecentPos = CreateCard("recent_purchase_orders", "Recent purchase orders", 210);
            _cardNotes = CreateCard("vendor_notes", "Notes", 150);
            _cardRecentPos.AllowResize = false;
            _cardRecentPos.ShowHeader = false;
            _cardNotes.AllowResize = false;
            _cardNotes.ShowHeader = false;

            BuildIdentityCard();
            BuildContactCard();
            BuildTaxCard();
            BuildPaymentCard();
            ConsolidateVendorDetailCards();
            BuildRecentPoCard();
            BuildNotesCard();
        }

        private ResizableCard CreateCard(string key, string title, int height)
        {
            ResizableCard card = new ResizableCard
            {
                PageKey = "VendorManagement",
                CardKey = "vendor_" + key,
                CardTitle = title,
                BackgroundColor = White,
                BorderColor = Border,
                ResizeAxes = CardResizeAxes.HeightOnly,
                AllowResize = true,
                Size = new Size(420, height),
                MinimumSize = new Size(280, 140),
                BackColor = Color.Transparent
            };
            card.ContentPanel.AutoScroll = false;
            card.CardResizeComplete += (s, e) => LayoutDetailCards();
            card.CardResized += (s, e) => LayoutDetailCards();
            return card;
        }

        private void BuildIdentityCard()
        {
            Panel body = _cardIdentity.ContentPanel;
            PlaceLabel(body, "Vendor name", 0, 0);
            _txtVendorName = PlaceTextBox(body, 0, 20, 378);

            PlaceLabel(body, "Category", 0, 58);
            _cmbCategory = PlaceComboBox(body, 0, 78, 180, _categories);

            PlaceLabel(body, "Vendor type", 198, 58);
            _cmbVendorType = PlaceComboBox(body, 198, 78, 150, _vendorTypes);

            PlaceLabel(body, "Specialisation tags", 0, 118);
            _tagFlow = new FlowLayoutPanel { Location = new Point(0, 138), Size = new Size(378, 64), BackColor = White, AutoScroll = true };
            _lnkAddTag = new LinkLabel { Text = "+ Add tag", LinkColor = Blue, AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
            _lnkAddTag.Click += (s, e) => ShowTagEditor();
            _txtAddTag = new TextBox { Visible = false, Width = 120, Margin = new Padding(0, 2, 8, 0) };
            _txtAddTag.KeyDown += TagEditorKeyDown;
            _txtAddTag.LostFocus += (s, e) => CommitTagEditor();
            _tagFlow.Controls.Add(_lnkAddTag);
            _tagFlow.Controls.Add(_txtAddTag);
            body.Controls.Add(_tagFlow);

            PlaceLabel(body, "MSME registered", 0, 214);
            _cmbMsme = PlaceComboBox(body, 0, 234, 180, _msmeTypes);
            _cmbMsme.SelectedIndexChanged += (s, e) => UpdateMsmeVisibility();

            PlaceLabel(body, "MSME number", 198, 214);
            _txtMsmeNumber = PlaceTextBox(body, 198, 234, 180);

            _lblMsmeNote = new Label
            {
                Location = new Point(0, 270),
                Size = new Size(378, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = BlueDark,
                Text = "MSME vendors must be paid within 45 days",
                Visible = false
            };
            body.Controls.Add(_lblMsmeNote);
        }

        private void BuildContactCard()
        {
            Panel body = _cardContact.ContentPanel;
            PlaceLabel(body, "Primary phone", 0, 0);
            _txtPhone = PlaceTextBox(body, 0, 20, 170);

            PlaceLabel(body, "WhatsApp number", 198, 0);
            _txtWhatsApp = PlaceTextBox(body, 198, 20, 180);
            _lnkSamePhone = new LinkLabel { Text = "Same as phone", LinkColor = Blue, AutoSize = true, Location = new Point(278, 48) };
            _lnkSamePhone.Click += (s, e) => _txtWhatsApp.Text = _txtPhone.Text;
            body.Controls.Add(_lnkSamePhone);

            PlaceLabel(body, "Email", 0, 70);
            _txtEmail = PlaceTextBox(body, 0, 90, 378);

            PlaceLabel(body, "Address", 0, 128);
            _txtAddress = PlaceTextBox(body, 0, 148, 378);
            _txtAddress.Multiline = true;
            SetInputHostHeight(_txtAddress, 54);

            PlaceLabel(body, "City", 0, 214);
            _txtCity = PlaceTextBox(body, 0, 234, 180);

            PlaceLabel(body, "State", 198, 214);
            _cmbState = PlaceComboBox(body, 198, 234, 180, IndiaStateCatalog.Names.OrderBy(name => name).ToList());
        }

        private void BuildTaxCard()
        {
            Panel body = _cardTax.ContentPanel;
            PlaceLabel(body, "GST number", 0, 0);
            _txtGstin = PlaceTextBox(body, 0, 20, 160);
            _txtGstin.CharacterCasing = CharacterCasing.Upper;
            _txtGstin.TextChanged += (s, e) => UpdateGstinState();
            _lblGstinHint = new Label { Location = new Point(0, 48), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            body.Controls.Add(_lblGstinHint);

            PlaceLabel(body, "PAN number", 198, 0);
            _txtPan = PlaceTextBox(body, 198, 20, 180);
            _txtPan.CharacterCasing = CharacterCasing.Upper;
            _txtPan.TextChanged += (s, e) => UpdatePanState();
            _lblPanHint = new Label { Location = new Point(198, 48), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            body.Controls.Add(_lblPanHint);

            _lblGstinState = new Label { Location = new Point(0, 68), Size = new Size(378, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = BlueDark };
            body.Controls.Add(_lblGstinState);

            PlaceLabel(body, "GST registration type", 0, 98);
            _cmbGstType = PlaceComboBox(body, 0, 118, 180, _gstTypes);
            _cmbGstType.SelectedIndexChanged += (s, e) => UpdateGstTypeState();

            _chkTds = new CheckBox { Text = "TDS applicable", Location = new Point(198, 118), AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
            _chkTds.CheckedChanged += (s, e) => UpdateTdsVisibility();
            body.Controls.Add(_chkTds);

            PlaceLabel(body, "TDS section", 0, 170);
            _cmbTdsSection = PlaceComboBox(body, 0, 190, 120, _tdsSections);
            _cmbTdsSection.SelectedIndexChanged += (s, e) => ApplyTdsDefaults();

            PlaceLabel(body, "TDS rate %", 140, 170);
            _numTdsRate = new NumericUpDown { Location = new Point(140, 190), Size = new Size(80, 24), DecimalPlaces = 2, Maximum = 100, Minimum = 0, Font = new Font("Segoe UI", 9f) };
            body.Controls.Add(_numTdsRate);

            _chkRcm = new CheckBox { Text = "RCM applicable", Location = new Point(238, 192), AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
            body.Controls.Add(_chkRcm);

            _lblTaxNote = new Label { Location = new Point(0, 228), Size = new Size(378, 38), Font = new Font("Segoe UI", 8.5f), ForeColor = AmberDark };
            body.Controls.Add(_lblTaxNote);
        }

        private void BuildPaymentCard()
        {
            Panel body = _cardPayment.ContentPanel;
            PlaceLabel(body, "Default credit days", 0, 0);
            _numCreditDays = new NumericUpDown { Location = new Point(0, 20), Size = new Size(90, 24), Minimum = 0, Maximum = 180, Font = new Font("Segoe UI", 9f) };
            _numCreditDays.ValueChanged += (s, e) => UpdateCreditVisuals();
            body.Controls.Add(_numCreditDays);

            _creditTrack = new Panel { Location = new Point(108, 24), Size = new Size(270, 8), BackColor = BorderLight };
            _creditTrack.Paint += (s, e) => DrawRoundedSurface(e.Graphics, _creditTrack.ClientRectangle, BorderLight, 4);
            _creditFill = new Panel { Location = new Point(0, 0), Size = new Size(0, 8), BackColor = Amber };
            _creditFill.Paint += (s, e) => DrawRoundedSurface(e.Graphics, _creditFill.ClientRectangle, Amber, 4);
            _creditTrack.Controls.Add(_creditFill);
            body.Controls.Add(_creditTrack);

            _lblCreditHint = new Label { Location = new Point(0, 50), Size = new Size(200, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint, Text = "0 = pay on delivery" };
            _lblMsmeWarning = new Label { Location = new Point(0, 70), Size = new Size(378, 18), Font = new Font("Segoe UI", 8.5f), ForeColor = RedDark, Visible = false };
            body.Controls.Add(_lblCreditHint);
            body.Controls.Add(_lblMsmeWarning);

            PlaceLabel(body, "Preferred payment mode", 0, 102);
            _cmbPaymentMode = PlaceComboBox(body, 0, 122, 180, _paymentModes);

            PlaceLabel(body, "Bank account number", 0, 160);
            _txtBankAccount = PlaceTextBox(body, 0, 180, 180);

            PlaceLabel(body, "IFSC code", 198, 160);
            _txtIfsc = PlaceTextBox(body, 198, 180, 180);
            _txtIfsc.CharacterCasing = CharacterCasing.Upper;
            _txtIfsc.TextChanged += (s, e) => UpdateIfscState();
            _lblIfscHint = new Label { Location = new Point(198, 208), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            body.Controls.Add(_lblIfscHint);

            PlaceLabel(body, "Account holder name", 0, 238);
            _txtAccountName = PlaceTextBox(body, 0, 258, 180);

            PlaceLabel(body, "Bank name", 198, 238);
            _txtBankName = PlaceTextBox(body, 198, 258, 180);
        }

        private void ConsolidateVendorDetailCards()
        {
            Panel host = _cardIdentity.ContentPanel;
            host.AutoScroll = false;
            host.Controls.Add(MakeSectionTitle("Vendor details", 0, 0, 860, 16f));

            OffsetExistingControls(host, 0, 74);
            host.Controls.Add(MakeSectionTitle("Vendor identity", 0, 44, 390, 10f));
            host.Controls.Add(MakeSectionTitle("Contact details", 450, 44, 390, 10f));
            host.Controls.Add(MakeSectionTitle("Tax compliance", 0, 356, 390, 10f));
            host.Controls.Add(MakeSectionTitle("Payment terms", 450, 356, 390, 10f));

            MoveSectionControls(_cardContact.ContentPanel, host, 450, 74);
            MoveSectionControls(_cardTax.ContentPanel, host, 0, 386);
            MoveSectionControls(_cardPayment.ContentPanel, host, 450, 386);
            LayoutVendorDetailFields(host);
        }

        private void LayoutVendorDetailFields(Control host)
        {
            int identityX = 0;
            int contactX = 300;
            int taxX = 600;
            int paymentX = 900;
            int sectionY = 44;
            int fieldY = 78;
            int colW = 250;

            SetLabelBounds(host, "Vendor details", 0, 0, 0, 860, 28);
            SetLabelBounds(host, "Vendor identity", 0, identityX, sectionY, 270, 22);
            SetLabelBounds(host, "Contact details", 0, contactX, sectionY, 270, 22);
            SetLabelBounds(host, "Tax compliance", 0, taxX, sectionY, 270, 22);
            SetLabelBounds(host, "Payment terms", 0, paymentX, sectionY, 270, 22);

            MoveLabel(host, "Vendor name", identityX, fieldY, colW);
            SetFieldBounds(_txtVendorName, identityX, fieldY + 20, colW, 28);
            MoveLabel(host, "Category", identityX, fieldY + 70, 130);
            SetFieldBounds(_cmbCategory, identityX, fieldY + 90, 132, 28);
            MoveLabel(host, "Vendor type", identityX + 146, fieldY + 70, 124);
            SetFieldBounds(_cmbVendorType, identityX + 146, fieldY + 90, 104, 28);
            MoveLabel(host, "Specialisation tags", identityX, fieldY + 132, colW);
            _tagFlow.SetBounds(identityX, fieldY + 152, colW, 50);
            MoveLabel(host, "MSME registered", identityX, fieldY + 214, 132);
            SetFieldBounds(_cmbMsme, identityX, fieldY + 234, 132, 28);
            MoveLabel(host, "MSME number", identityX + 146, fieldY + 214, 124);
            SetFieldBounds(_txtMsmeNumber, identityX + 146, fieldY + 234, 104, 28);

            MoveLabel(host, "Primary phone", contactX, fieldY, 132);
            SetFieldBounds(_txtPhone, contactX, fieldY + 20, 132, 28);
            MoveLabel(host, "WhatsApp number", contactX + 146, fieldY, 132);
            SetFieldBounds(_txtWhatsApp, contactX + 146, fieldY + 20, 132, 28);
            _lnkSamePhone.Location = new Point(contactX + 196, fieldY + 50);
            MoveLabel(host, "Email", contactX, fieldY + 70, colW);
            SetFieldBounds(_txtEmail, contactX, fieldY + 90, colW, 28);
            MoveLabel(host, "Address", contactX, fieldY + 132, colW);
            SetFieldBounds(_txtAddress, contactX, fieldY + 152, colW, 54);
            MoveLabel(host, "City", contactX, fieldY + 214, 132);
            SetFieldBounds(_txtCity, contactX, fieldY + 234, 132, 28);
            MoveLabel(host, "State", contactX + 146, fieldY + 214, 132);
            SetFieldBounds(_cmbState, contactX + 146, fieldY + 234, 104, 28);

            MoveLabel(host, "GST number", taxX, fieldY, colW);
            SetFieldBounds(_txtGstin, taxX, fieldY + 20, colW, 28);
            _lblGstinHint.SetBounds(taxX, fieldY + 50, colW, 16);
            MoveLabel(host, "PAN number", taxX, fieldY + 70, colW);
            SetFieldBounds(_txtPan, taxX, fieldY + 90, colW, 28);
            _lblPanHint.SetBounds(taxX, fieldY + 120, colW, 16);
            _lblGstinState.SetBounds(taxX, fieldY + 136, colW, 16);
            MoveLabel(host, "GST registration type", taxX, fieldY + 164, 132);
            SetFieldBounds(_cmbGstType, taxX, fieldY + 184, 132, 28);
            _chkTds.Location = new Point(taxX + 148, fieldY + 188);
            MoveLabel(host, "TDS section", taxX, fieldY + 228, 132);
            SetFieldBounds(_cmbTdsSection, taxX, fieldY + 248, 118, 28);
            MoveLabel(host, "TDS rate %", taxX + 134, fieldY + 228, 92);
            SetFieldBounds(_numTdsRate, taxX + 134, fieldY + 248, 92, 28);
            _chkRcm.Location = new Point(taxX + 148, fieldY + 284);

            MoveLabel(host, "Default credit days", paymentX, fieldY, 150);
            SetFieldBounds(_numCreditDays, paymentX, fieldY + 20, 90, 28);
            _creditTrack.SetBounds(paymentX + 108, fieldY + 30, 140, 8);
            _lblCreditHint.SetBounds(paymentX, fieldY + 52, colW, 16);
            MoveLabel(host, "Preferred payment mode", paymentX, fieldY + 90, colW);
            SetFieldBounds(_cmbPaymentMode, paymentX, fieldY + 110, colW, 28);
            MoveLabel(host, "Bank account number", paymentX, fieldY + 152, 132);
            SetFieldBounds(_txtBankAccount, paymentX, fieldY + 172, 120, 28);
            MoveLabel(host, "IFSC code", paymentX + 146, fieldY + 152, 132);
            SetFieldBounds(_txtIfsc, paymentX + 136, fieldY + 172, 114, 28);
            _lblIfscHint.SetBounds(paymentX + 136, fieldY + 202, 114, 16);
            MoveLabel(host, "Account holder name", paymentX, fieldY + 224, 132);
            SetFieldBounds(_txtAccountName, paymentX, fieldY + 244, 120, 28);
            MoveLabel(host, "Bank name", paymentX + 146, fieldY + 224, 132);
            SetFieldBounds(_txtBankName, paymentX + 136, fieldY + 244, 114, 28);
        }

        private static void SetFieldBounds(Control control, int x, int y, int width, int height)
        {
            if (control == null)
                return;
            Control target = control.Parent != null && Equals(control.Parent.Tag, "vendor-input-host") ? control.Parent : control;
            target.SetBounds(x, y, width, height);
            if (control.Parent != null && Equals(control.Parent.Tag, "vendor-input-host"))
                control.Parent.Invalidate();
        }

        private static void MoveLabel(Control host, string text, int x, int y, int width)
        {
            SetLabelBounds(host, text, 0, x, y, width, 16);
        }

        private static void SetLabelBounds(Control host, string text, int occurrence, int x, int y, int width, int height)
        {
            int seen = 0;
            foreach (Label label in host.Controls.OfType<Label>())
            {
                if (!string.Equals(label.Text, text, StringComparison.Ordinal))
                    continue;
                if (seen++ != occurrence)
                    continue;
                label.SetBounds(x, y, width, height);
                return;
            }
        }

        private static Label MakeSectionTitle(string text, int x, int y, int width, float size)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, size > 12f ? 28 : 22),
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static void OffsetExistingControls(Control parent, int dx, int dy)
        {
            foreach (Control control in parent.Controls.Cast<Control>().ToList())
            {
                if (control is Label label && label.Text == "Vendor details")
                    continue;
                control.Location = new Point(control.Left + dx, control.Top + dy);
            }
        }

        private static void MoveSectionControls(Control source, Control target, int dx, int dy)
        {
            foreach (Control control in source.Controls.Cast<Control>().ToList())
            {
                source.Controls.Remove(control);
                control.Location = new Point(control.Left + dx, control.Top + dy);
                target.Controls.Add(control);
            }
        }

        private void BuildRecentPoCard()
        {
            Panel body = _cardRecentPos.ContentPanel;
            body.AutoScroll = false;
            Label title = new Label { Text = "Recent purchase orders", Location = new Point(0, 0), Size = new Size(260, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary };
            body.Controls.Add(title);
            LinkLabel lnkViewAll = new LinkLabel { Text = "View all ->", LinkColor = Blue, AutoSize = true, Location = new Point(0, 26) };
            lnkViewAll.Click += (s, e) => ViewPurchaseOrders();
            body.Controls.Add(lnkViewAll);

            _recentPoFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 54),
                Size = new Size(820, 92),
                AutoScroll = false,
                BackColor = White,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            body.Controls.Add(_recentPoFlow);

            _lblRecentPoFooter = new Label { Location = new Point(0, 148), Size = new Size(820, 18), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary };
            body.Controls.Add(_lblRecentPoFooter);
        }

        private void BuildNotesCard()
        {
            Panel body = _cardNotes.ContentPanel;
            Label title = new Label { Text = "Notes", Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft };
            _txtNotes = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.None,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                BackColor = White,
                Margin = Padding.Empty
            };
            ConfigurePlaceholder(_txtNotes, "Vendor notes, special terms, delivery preferences...");
            _txtNotes.Leave += async (s, e) => await AutoSaveNotesAsync();
            Panel noteHost = CreateInputHost(0, 0, 100, 100);
            noteHost.Dock = DockStyle.Fill;
            noteHost.Padding = new Padding(10, 8, 10, 8);
            noteHost.Controls.Add(_txtNotes);
            body.Controls.Add(noteHost);
            body.Controls.Add(title);
        }

        private async Task LoadInitialAsync()
        {
            if (_showDashboard)
                await RefreshDashboardAsync();
            else
                await RefreshAsync(false);
        }

        private async Task RefreshDashboardAsync()
        {
            try
            {
                List<VendorSummaryDto> summaries = null;
                List<DuplicateGroupDto> duplicates = null;
                List<PurchaseOrder> purchases = null;
                List<AMCContract> contracts = null;

                await Task.Run(() =>
                {
                    summaries = _vendorSvc.GetAllVendorsWithSummary();
                    duplicates = _vendorSvc.DetectDuplicates();
                    purchases = _purchaseSvc.GetAll();
                    contracts = _contractSvc.GetAllContracts();
                });

                _vendorSummaries = summaries ?? new List<VendorSummaryDto>();
                _duplicateGroups = duplicates ?? new List<DuplicateGroupDto>();
                _dashboardPurchases = purchases ?? new List<PurchaseOrder>();
                _dashboardContracts = contracts ?? new List<AMCContract>();
                RenderVendorDashboard();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.RefreshDashboardAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Vendor dashboard", ex);
            }
        }

        private async Task RefreshAsync(bool preserveSelection)
        {
            try
            {
                if (_showDashboard)
                {
                    await RefreshDashboardAsync();
                    return;
                }

                SetBusyState(true, "Loading vendors...");
                int? selectedVendorId = preserveSelection ? (int?)_currentVendor?.VendorID : null;
                List<VendorSummaryDto> summaries = null;
                List<DuplicateGroupDto> duplicates = null;

                await Task.Run(() =>
                {
                    summaries = _vendorSvc.GetAllVendorsWithSummary();
                    duplicates = _vendorSvc.DetectDuplicates();
                });

                _vendorSummaries = summaries ?? new List<VendorSummaryDto>();
                _duplicateGroups = duplicates ?? new List<DuplicateGroupDto>();
                UpdateTopBar();
                BuildFilterChips();
                UpdateDuplicateBanner();
                ApplyFilters();

                if (selectedVendorId.HasValue && _vendorSummaries.Any(v => v.VendorId == selectedVendorId.Value))
                    await LoadVendorDetailAsync(selectedVendorId.Value);
                else if (_vendorSummaries.Count > 0)
                    await LoadVendorDetailAsync(_vendorSummaries[0].VendorId);
                else
                    PrepareBlankVendor();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.RefreshAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Vendor refresh", ex);
                PrepareBlankVendor();
            }
            finally
            {
                SetBusyState(false, string.Empty);
            }
        }

        private void SetBusyState(bool busy, string message)
        {
            if (_lblListFooter != null)
                _lblListFooter.Text = busy ? message : _lblListFooter.Text;
            if (_btnSave != null)
                _btnSave.Enabled = !busy;
            if (_btnRaisePo != null)
                _btnRaisePo.Enabled = !busy;
            if (_btnArchive != null)
                _btnArchive.Enabled = !busy;
            if (_btnViewPos != null)
                _btnViewPos.Enabled = !busy;
        }

        private void UpdateTopBar()
        {
            _lblTopBadge.Text = _vendorSummaries.Count + " vendors";
            _lblDuplicateBadge.Text = _duplicateGroups.Count + " duplicates detected";
            _lblDuplicateBadge.Visible = _duplicateGroups.Count > 0;
            _btnMerge.Visible = _duplicateGroups.Count > 0;
        }

        private void UpdateDuplicateBanner()
        {
            DuplicateGroupDto topGroup = _duplicateGroups.FirstOrDefault();
            if (topGroup == null)
            {
                _duplicateBanner.Visible = false;
                _duplicateBanner.Height = 0;
                return;
            }

            _lblDuplicateBanner.Text = "Warning: " + topGroup.Vendors.First().VendorName + " appears " + topGroup.Vendors.Count + "x - merge needed";
            _duplicateBanner.Visible = true;
            _duplicateBanner.Height = 34;
        }

        private void BuildFilterChips()
        {
            _chipFlow.SuspendLayout();
            _chipFlow.Controls.Clear();

            int allCount = _vendorSummaries.Count(v => !v.IsArchived);
            int activeCount = _vendorSummaries.Count(v => v.IsActive && !v.IsArchived);
            int overdueCount = _vendorSummaries.Count(v => v.HasOverdue && !v.IsArchived);
            int msmeCount = _vendorSummaries.Count(v => !string.Equals(v.MSMERegistered, "No", StringComparison.OrdinalIgnoreCase) && !v.IsArchived);
            int archivedCount = _vendorSummaries.Count(v => v.IsArchived);

            AddFilterChip("All", allCount, TealLightBg, Color.FromArgb(15, 110, 86));
            if (activeCount > 0) AddFilterChip("Active", activeCount, White, TextPrimary);
            if (overdueCount > 0) AddFilterChip("Overdue", overdueCount, RedLightBg, RedDark);
            if (msmeCount > 0) AddFilterChip("MSME", msmeCount, BlueLightBg, BlueDark);
            if (archivedCount > 0) AddFilterChip("Archived", archivedCount, Surface, TextSecondary);
            _chipFlow.ResumeLayout();
            UpdateSearchSectionLayout();
        }

        private void UpdateSearchSectionLayout()
        {
            if (_searchSection == null || _chipFlow == null || _searchSection.IsDisposed)
                return;

            int availableWidth = Math.Max(220, _split.Panel1.ClientSize.Width - 24);
            Control searchWrap = _searchSection.Controls.OfType<Panel>().FirstOrDefault();
            if (searchWrap != null)
            {
                searchWrap.Location = new Point(12, 48);
                searchWrap.Width = Math.Max(180, availableWidth - 52);
                if (_txtSearch != null)
                    _txtSearch.Width = Math.Max(120, searchWrap.Width - 52);
                if (_btnClearSearch != null)
                    _btnClearSearch.Left = searchWrap.Width - _btnClearSearch.Width - 8;
                searchWrap.Invalidate();
            }

            Button filterButton = _searchSection.Controls.OfType<Button>().FirstOrDefault(b => b.Text == "Filter");
            if (filterButton != null && searchWrap != null)
                filterButton.Location = new Point(searchWrap.Right + 8, 48);

            _chipFlow.Location = new Point(12, 100);
            _chipFlow.Width = availableWidth;

            int x = 0;
            int rows = 1;
            foreach (Control chip in _chipFlow.Controls)
            {
                int chipWidth = chip.Width + chip.Margin.Horizontal;
                if (x > 0 && x + chipWidth > availableWidth)
                {
                    rows++;
                    x = 0;
                }
                x += chipWidth;
            }

            int rowHeight = 36;
            _chipFlow.Height = Math.Max(34, rows * rowHeight);
            _searchSection.Height = 112 + _chipFlow.Height;
        }

        private void AddFilterChip(string key, int count, Color bg, Color fg)
        {
            Button chip = new Button
            {
                AutoSize = false,
                Text = key + " (" + count + ")",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                BackColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? DS.Primary600 : bg,
                ForeColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? White : fg,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 8, 8),
                Tag = key,
                Height = 32,
                Width = Math.Max(92, TextRenderer.MeasureText(key + " (" + count + ")", new Font("Segoe UI", 8.75f, FontStyle.Bold)).Width + 28),
                TextAlign = ContentAlignment.MiddleCenter
            };
            chip.FlatAppearance.BorderSize = 1;
            chip.FlatAppearance.BorderColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? DS.Primary600 : Border;
            chip.FlatAppearance.MouseOverBackColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? DS.Primary700 : Surface;
            chip.Click += (s, e) =>
            {
                _activeFilter = Convert.ToString(((Button)s).Tag);
                BuildFilterChips();
                ApplyFilters();
            };
            _chipFlow.Controls.Add(chip);
        }

        private void ApplyFilters()
        {
            IEnumerable<VendorSummaryDto> query = _vendorSummaries;
            bool includeArchived = string.Equals(_activeFilter, "Archived", StringComparison.OrdinalIgnoreCase);
            if (!includeArchived)
                query = query.Where(v => !v.IsArchived);

            switch ((_activeFilter ?? string.Empty).ToUpperInvariant())
            {
                case "ACTIVE":
                    query = query.Where(v => v.IsActive && !v.IsArchived);
                    break;
                case "OVERDUE":
                    query = query.Where(v => v.HasOverdue && !v.IsArchived);
                    break;
                case "MSME":
                    query = query.Where(v => !string.Equals(v.MSMERegistered, "No", StringComparison.OrdinalIgnoreCase) && !v.IsArchived);
                    break;
                case "ARCHIVED":
                    query = query.Where(v => v.IsArchived);
                    break;
            }

            string term = GetSearchText();
            if (!string.IsNullOrWhiteSpace(term))
            {
                string search = term.Trim().ToUpperInvariant();
                query = query.Where(v =>
                    (v.VendorName ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.Category ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.City ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.Phone ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.VendorType ?? string.Empty).ToUpperInvariant().Contains(search));
            }

            RenderVendorList(query.ToList());
        }

        private void RenderVendorList(List<VendorSummaryDto> items)
        {
            _vendorListFlow.SuspendLayout();
            _vendorListFlow.Controls.Clear();

            if (items.Count == 0)
            {
                Panel empty = new Panel { Width = Math.Max(240, _vendorListFlow.ClientSize.Width - 30), Height = 420, Margin = new Padding(0, 20, 0, 0), BackColor = White };
                Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Vendor, 58, Color.FromArgb(238, 242, 255), Blue);
                icon.Location = new Point((empty.Width - 58) / 2, 150);
                Label lbl = new Label { Text = "No vendors found", Location = new Point(20, 212), Size = new Size(empty.Width - 40, 26), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter };
                Label sub = new Label { Text = "Try adjusting your search or create a new vendor.", Location = new Point(36, 240), Size = new Size(empty.Width - 72, 42), Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleCenter };
                Button create = MakeActionButton("+  New Vendor", White, Blue, 112, true);
                create.Location = new Point((empty.Width - create.Width) / 2, 302);
                create.Click += async (s, e) => await BeginNewVendorAsync();
                LinkLabel lnk = new LinkLabel { Text = "Clear search", Location = new Point(20, 350), Size = new Size(empty.Width - 40, 24), LinkColor = Blue, TextAlign = ContentAlignment.MiddleCenter, Visible = !string.IsNullOrWhiteSpace(GetSearchText()) };
                lnk.Click += (s, e) => _txtSearch.Text = string.Empty;
                empty.Resize += (s, e) =>
                {
                    icon.Left = (empty.Width - icon.Width) / 2;
                    lbl.Width = empty.Width - 40;
                    sub.Width = empty.Width - 72;
                    create.Left = (empty.Width - create.Width) / 2;
                    lnk.Width = empty.Width - 40;
                };
                empty.Controls.Add(icon);
                empty.Controls.Add(lbl);
                empty.Controls.Add(sub);
                empty.Controls.Add(create);
                empty.Controls.Add(lnk);
                _vendorListFlow.Controls.Add(empty);
                _lnkClearSearch.Visible = !string.IsNullOrWhiteSpace(GetSearchText());
                _lblListFooter.Text = "No vendors shown";
                _vendorListFlow.ResumeLayout();
                return;
            }

            foreach (VendorSummaryDto item in items)
                _vendorListFlow.Controls.Add(BuildVendorListItem(item));

            _lnkClearSearch.Visible = !string.IsNullOrWhiteSpace(GetSearchText());
            _lblListFooter.Text = items.Count + " vendors shown";
            _vendorListFlow.ResumeLayout();
        }

        private Control BuildVendorListItem(VendorSummaryDto item)
        {
            Panel panel = new Panel
            {
                Width = Math.Max(250, _vendorListFlow.ClientSize.Width - 32),
                Height = 76,
                BackColor = _currentVendor != null && _currentVendor.VendorID == item.VendorId ? TealLightBg : White,
                Margin = new Padding(0, 0, 0, 8),
                Tag = item,
                Cursor = Cursors.Hand
            };
            panel.Paint += (s, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (SolidBrush brush = new SolidBrush(panel.BackColor))
                    e.Graphics.FillRectangle(brush, rect);
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, rect);
                if (_currentVendor != null && _currentVendor.VendorID == item.VendorId)
                {
                    using (SolidBrush accent = new SolidBrush(Teal))
                        e.Graphics.FillRectangle(accent, new Rectangle(0, 0, 3, panel.Height));
                }
            };
            panel.MouseEnter += (s, e) =>
            {
                if (_currentVendor == null || _currentVendor.VendorID != item.VendorId)
                    panel.BackColor = Color.FromArgb(250, 250, 250);
                panel.Invalidate();
            };
            panel.MouseLeave += (s, e) =>
            {
                panel.BackColor = _currentVendor != null && _currentVendor.VendorID == item.VendorId ? TealLightBg : White;
                panel.Invalidate();
            };
            panel.Click += async (s, e) => await LoadVendorDetailAsync(item.VendorId);

            Label lblName = new Label { Text = item.VendorName, Location = new Point(12, 10), Size = new Size(170, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary };
            Label lblMeta = new Label { Text = JoinBullet(item.City, item.Category), Location = new Point(12, 32), Size = new Size(190, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            Label lblPhone = new Label { Text = string.IsNullOrWhiteSpace(item.Phone) ? "No phone" : item.Phone, Location = new Point(12, 52), Size = new Size(120, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            Label lblBalance = new Label
            {
                Text = item.OutstandingBalance > 0 ? IndiaFormatHelper.FormatCurrency(item.OutstandingBalance) + " due" : "No balance",
                Location = new Point(panel.Width - 140, 52),
                Size = new Size(128, 16),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = item.HasOverdue ? RedDark : item.OutstandingBalance > 0 ? AmberDark : TextHint,
                TextAlign = ContentAlignment.TopRight
            };
            Panel pill = CreateStatusPill(item);
            pill.Location = new Point(panel.Width - pill.Width - 12, 8);

            foreach (Control ctl in new Control[] { lblName, lblMeta, lblPhone, lblBalance, pill })
            {
                ctl.Click += async (s, e) => await LoadVendorDetailAsync(item.VendorId);
                panel.Controls.Add(ctl);
            }

            return panel;
        }

        private Panel CreateStatusPill(VendorSummaryDto item)
        {
            string text;
            Color bg;
            Color fg;
            if (item.IsArchived)
            {
                text = "Archived";
                bg = Surface;
                fg = TextHint;
            }
            else if (item.IsDuplicate)
            {
                text = "Duplicate";
                bg = RedLightBg;
                fg = RedDark;
            }
            else
            {
                text = "Active";
                bg = Color.FromArgb(234, 243, 222);
                fg = Color.FromArgb(39, 80, 10);
            }

            int width = Math.Max(64, TextRenderer.MeasureText(text, new Font("Segoe UI", 8.5f, FontStyle.Bold)).Width + 20);
            Panel panel = new Panel { Size = new Size(width, 22), BackColor = bg };
            panel.Paint += (s, e) => DrawRoundedSurface(e.Graphics, panel.ClientRectangle, bg, 10);
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = fg, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private async Task LoadVendorDetailAsync(int vendorId)
        {
            if (_showDashboard)
            {
                await OpenVendorEditorAsync(vendorId);
                return;
            }

            try
            {
                _lblHeaderName.Text = "Loading vendor...";
                VendorDetailDto detail = await Task.Run(() => _vendorSvc.GetVendorDetail(vendorId));
                if (detail == null)
                    return;
                _currentVendor = detail;
                _isNewMode = false;
                BindVendor(detail);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.LoadVendorDetailAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Loading vendor detail", ex);
            }
        }

        private async Task BeginNewVendorAsync()
        {
            if (_showDashboard)
            {
                await OpenVendorEditorAsync(null);
                return;
            }

            await Task.Yield();
            PrepareBlankVendor();
            _txtVendorName.Focus();
        }

        private async Task OpenVendorEditorAsync(int? vendorId)
        {
            _showDashboard = false;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            RestoreVendorInputChrome();
            await RefreshAsync(false);
            if (vendorId.HasValue)
                await LoadVendorDetailAsync(vendorId.Value);
            else
                await BeginNewVendorAsync();
        }

        private async Task BackToVendorDashboardAsync()
        {
            _showDashboard = true;
            _currentVendor = null;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            RestoreVendorInputChrome();
            await RefreshDashboardAsync();
        }

        private void PrepareBlankVendor()
        {
            _currentVendor = new VendorDetailDto
            {
                VendorType = "Supplier",
                DefaultCreditDays = 30,
                GSTRegistrationType = "Regular",
                MSMERegistered = "No",
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            _isNewMode = true;
            BindVendor(_currentVendor);
        }

        private void BindVendor(VendorDetailDto vendor)
        {
            _isBinding = true;
            try
            {
                ClearErrors();
                _lblHeaderName.Text = string.IsNullOrWhiteSpace(vendor.VendorName) ? "New vendor" : vendor.VendorName;
                _lblHeaderMeta.Text = JoinBullet(string.IsNullOrWhiteSpace(vendor.VendorType) ? "Supplier" : vendor.VendorType, string.IsNullOrWhiteSpace(vendor.City) ? "City pending" : vendor.City, "Added " + vendor.CreatedDate.Year);

                _txtVendorName.Text = vendor.VendorName ?? string.Empty;
                _cmbCategory.SelectedItem = !string.IsNullOrWhiteSpace(vendor.Category) ? vendor.Category : "General";
                _cmbVendorType.SelectedItem = !string.IsNullOrWhiteSpace(vendor.VendorType) ? vendor.VendorType : "Supplier";
                _cmbMsme.SelectedItem = !string.IsNullOrWhiteSpace(vendor.MSMERegistered) ? vendor.MSMERegistered : "No";
                _txtMsmeNumber.Text = vendor.MSMENumber ?? string.Empty;

                _txtPhone.Text = vendor.Phone ?? string.Empty;
                _txtWhatsApp.Text = vendor.WhatsAppNumber ?? string.Empty;
                _txtEmail.Text = vendor.Email ?? string.Empty;
                _txtAddress.Text = vendor.Address ?? string.Empty;
                _txtCity.Text = vendor.City ?? string.Empty;
                SetStateComboByCodeOrName(vendor.StateCode);

                _txtGstin.Text = vendor.GSTNumber ?? string.Empty;
                _txtPan.Text = vendor.PANNumber ?? string.Empty;
                _cmbGstType.SelectedItem = !string.IsNullOrWhiteSpace(vendor.GSTRegistrationType) ? vendor.GSTRegistrationType : "Regular";
                _chkTds.Checked = vendor.TDSApplicable;
                _cmbTdsSection.SelectedItem = !string.IsNullOrWhiteSpace(vendor.TDSSection) ? vendor.TDSSection : "194C";
                _numTdsRate.Value = Math.Min(_numTdsRate.Maximum, Math.Max(_numTdsRate.Minimum, vendor.TDSRate));
                _chkRcm.Checked = vendor.RCMApplicable;

                _numCreditDays.Value = Math.Min(_numCreditDays.Maximum, Math.Max(_numCreditDays.Minimum, vendor.DefaultCreditDays));
                _cmbPaymentMode.SelectedItem = !string.IsNullOrWhiteSpace(vendor.PreferredPaymentMode) ? vendor.PreferredPaymentMode : null;
                _txtBankAccount.Text = vendor.BankAccountNumber ?? string.Empty;
                _txtIfsc.Text = vendor.BankIFSC ?? string.Empty;
                _txtAccountName.Text = vendor.BankAccountName ?? string.Empty;
                _txtBankName.Text = vendor.BankName ?? string.Empty;
                _txtNotes.Text = vendor.Notes ?? string.Empty;

                RenderTags(ParseTags(vendor.SpecialisationTags));
                RenderRecentPurchaseOrders(vendor.RecentPOs, vendor.TotalPurchased);
                UpdateStats(vendor);
                UpdateWarnings(vendor);
                UpdateMsmeVisibility();
                UpdateGstinState();
                UpdatePanState();
                UpdateIfscState();
                UpdateGstTypeState();
                UpdateTdsVisibility();
                UpdateCreditVisuals();
                UpdateActionState();
            }
            finally
            {
                _isBinding = false;
                LayoutDetailCards();
            }
        }

        private void UpdateStats(VendorDetailDto vendor)
        {
            _lblStatOutstanding.Text = IndiaFormatHelper.FormatCurrency(vendor.OutstandingBalance);
            _lblStatOutstanding.ForeColor = vendor.OutstandingBalance > 0 ? RedDark : Teal;
            _lblStatPurchased.Text = IndiaFormatHelper.FormatCurrency(vendor.TotalPurchased);
            _lblStatPurchased.ForeColor = Blue;
            _lblStatOpenPos.Text = vendor.OpenPOCount.ToString();
            _lblStatOpenPos.ForeColor = vendor.OpenPOCount > 0 ? AmberDark : TextHint;
            _lblStatCreditDays.Text = vendor.DefaultCreditDays.ToString();
            _lblStatCreditDays.ForeColor = TextPrimary;
        }

        private void UpdateWarnings(Vendor vendor)
        {
            List<string> warnings = _vendorSvc.GetMissingFieldWarnings(vendor);
            if (warnings.Count == 0)
            {
                _warningStrip.Visible = false;
                return;
            }
            _lblWarningStrip.Text = string.Join(", ", warnings) + " are missing - add them for a complete vendor profile";
            _warningStrip.Visible = true;
        }

        private void RenderTags(List<string> tags)
        {
            _tagFlow.SuspendLayout();
            _tagFlow.Controls.Clear();
            foreach (string tag in tags.Take(8))
                _tagFlow.Controls.Add(CreateTagChip(tag));
            _tagFlow.Controls.Add(_lnkAddTag);
            _tagFlow.Controls.Add(_txtAddTag);
            _tagFlow.ResumeLayout();
        }

        private Control CreateTagChip(string tag)
        {
            Panel chip = new Panel { Height = 28, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = BlueLightBg, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 8, 8) };
            chip.Paint += (s, e) => DrawRoundedSurface(e.Graphics, chip.ClientRectangle, BlueLightBg, 6);
            Label lbl = new Label { AutoSize = true, Text = tag, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = BlueDark };
            LinkLabel lnk = new LinkLabel { AutoSize = true, Text = "x", LinkColor = RedDark, Margin = new Padding(6, 0, 0, 0) };
            lnk.Click += (s, e) =>
            {
                List<string> current = ParseTags(GetTagsText());
                current.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
                RenderTags(current);
            };
            chip.Controls.Add(lbl);
            chip.Controls.Add(lnk);
            lnk.Location = new Point(lbl.Right + 4, 6);
            chip.SizeChanged += (s, e) => lnk.Location = new Point(lbl.Right + 4, 6);
            return chip;
        }

        private void ShowTagEditor()
        {
            _txtAddTag.Visible = true;
            _txtAddTag.Text = string.Empty;
            _txtAddTag.Focus();
        }

        private void CommitTagEditor()
        {
            if (!_txtAddTag.Visible)
                return;
            string tag = (_txtAddTag.Text ?? string.Empty).Trim();
            _txtAddTag.Visible = false;
            if (string.IsNullOrWhiteSpace(tag))
                return;
            List<string> current = ParseTags(GetTagsText());
            if (!current.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) && current.Count < 8)
                current.Add(tag);
            RenderTags(current);
        }

        private void TagEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitTagEditor();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _txtAddTag.Visible = false;
            }
        }

        private void RenderRecentPurchaseOrders(List<PurchaseOrder> orders, decimal totalPurchased)
        {
            decimal advanceBalance = 0m;
            if (_currentVendor != null && _currentVendor.VendorID > 0)
            {
                try { advanceBalance = _vendorAdvanceSvc.GetVendorAdvanceBalance(_currentVendor.VendorID); } catch { advanceBalance = 0m; }
            }
            _recentPoFlow.SuspendLayout();
            _recentPoFlow.Controls.Clear();
            _recentPoFlow.Controls.Add(BuildRecentPoHeader());
            if (orders == null || orders.Count == 0)
            {
                _recentPoFlow.Controls.Add(BuildRecentPoEmptyState());
            }
            else
            {
                foreach (PurchaseOrder po in orders.OrderByDescending(p => p.PODate).Take(5))
                    _recentPoFlow.Controls.Add(BuildPurchaseOrderRow(po));
            }
            _lblRecentPoFooter.Text = "Total purchased   " + IndiaFormatHelper.FormatCurrency(totalPurchased)
                + "   |   Advance balance   " + IndiaFormatHelper.FormatCurrency(advanceBalance);
            _recentPoFlow.ResumeLayout();
        }

        private Control BuildRecentPoHeader()
        {
            Panel header = new Panel { Width = Math.Max(720, _recentPoFlow.Width - 4), Height = 30, BackColor = DS.Slate50, Margin = new Padding(0) };
            string[] labels = { "PO Number", "PO Date", "Status", "Amount (Rs)", "Outstanding (Rs)", "Credit Days", "Actions" };
            int[] xs = { 0, 170, 330, 475, 610, 760, 890 };
            int[] ws = { 150, 130, 120, 120, 135, 100, 120 };
            for (int i = 0; i < labels.Length; i++)
                header.Controls.Add(new Label { Text = labels[i], Location = new Point(xs[i], 8), Size = new Size(ws[i], 16), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextSecondary });
            return header;
        }

        private Control BuildRecentPoEmptyState()
        {
            Panel empty = new Panel { Width = Math.Max(720, _recentPoFlow.Width - 4), Height = 54, BackColor = White, Margin = new Padding(0) };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Purchase, 38, DS.Slate100, Blue);
            icon.Location = new Point(Math.Max(280, empty.Width / 2 - 86), 10);
            empty.Controls.Add(icon);
            empty.Controls.Add(new Label { Text = "No purchase orders yet for this vendor.", Location = new Point(icon.Right + 14, 17), Size = new Size(280, 18), Font = new Font("Segoe UI", 9f), ForeColor = TextHint });
            return empty;
        }

        private Control BuildPurchaseOrderRow(PurchaseOrder po)
        {
            Panel row = new Panel { Width = Math.Max(720, _recentPoFlow.Width - 25), Height = 52, BackColor = White, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand };
            row.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(BorderLight))
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            };
            row.Click += (s, e) => RecentDocumentOpenService.OpenPdf(this, po);
            LinkLabel lnkPo = new LinkLabel { Text = po.PONumber, Location = new Point(0, 2), Size = new Size(140, 18), LinkColor = Blue, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lnkPo.Click += (s, e) => RecentDocumentOpenService.OpenPdf(this, po);
            Label lblDate = new Label { Text = IndiaFormatHelper.FormatDate(po.PODate), Location = new Point(0, 24), Size = new Size(140, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            Label lblLink = new Label { Text = string.IsNullOrWhiteSpace(po.LinkedToLabel) ? (po.LinkedToType ?? "No linked record") : po.LinkedToLabel, Location = new Point(150, 12), Size = new Size(260, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            Label lblAmount = new Label { Text = IndiaFormatHelper.FormatCurrency(po.TotalAmount), Location = new Point(row.Width - 180, 8), Size = new Size(100, 18), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.TopRight };
            Panel pill = CreatePoStatusPill(po);
            pill.Location = new Point(row.Width - pill.Width - 8, 28);
            row.Controls.Add(lnkPo);
            row.Controls.Add(lblDate);
            row.Controls.Add(lblLink);
            row.Controls.Add(lblAmount);
            row.Controls.Add(pill);
            return row;
        }

        private Panel CreatePoStatusPill(PurchaseOrder po)
        {
            string text = po.Status ?? "Pending";
            if (po.IsOverdue)
                text = "Overdue " + Math.Max(0, (DateTime.Today - po.PayByDate.Date).Days) + "d";
            Color bg = RedLightBg;
            Color fg = RedDark;
            if (string.Equals(po.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                bg = Color.FromArgb(234, 243, 222);
                fg = Color.FromArgb(39, 80, 10);
            }
            else if (string.Equals(po.Status, "Partial", StringComparison.OrdinalIgnoreCase))
            {
                bg = AmberLightBg;
                fg = Color.FromArgb(122, 77, 0);
            }
            int width = Math.Max(72, TextRenderer.MeasureText(text, new Font("Segoe UI", 8.5f, FontStyle.Bold)).Width + 16);
            Panel panel = new Panel { Size = new Size(width, 20), BackColor = bg };
            panel.Paint += (s, e) => DrawRoundedSurface(e.Graphics, panel.ClientRectangle, bg, 10);
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = fg, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private void UpdateMsmeVisibility()
        {
            bool showMsme = !string.Equals(Convert.ToString(_cmbMsme.SelectedItem), "No", StringComparison.OrdinalIgnoreCase);
            _txtMsmeNumber.Visible = showMsme;
            _lblMsmeNote.Visible = showMsme;
            UpdateCreditVisuals();
        }

        private void UpdateGstinState()
        {
            string stateCode;
            bool valid = _vendorSvc.ValidateGSTIN(_txtGstin.Text, out stateCode);
            if (string.IsNullOrWhiteSpace(_txtGstin.Text))
            {
                _lblGstinHint.Text = string.Empty;
                _lblGstinState.Text = string.Empty;
                return;
            }
            _lblGstinHint.Text = valid ? "Valid" : "Invalid format";
            _lblGstinHint.ForeColor = valid ? Teal : RedDark;
            _lblGstinState.Text = valid ? "State code " + stateCode + " - " + IndiaStateCatalog.NormalizeStateName(StateNameByCode(stateCode)) : string.Empty;
            if (valid)
                SetStateComboByCodeOrName(stateCode);
        }

        private void UpdatePanState()
        {
            if (string.IsNullOrWhiteSpace(_txtPan.Text))
            {
                _lblPanHint.Text = string.Empty;
                return;
            }
            bool valid = _vendorSvc.ValidatePAN(_txtPan.Text);
            _lblPanHint.Text = valid ? "Valid" : "Invalid format";
            _lblPanHint.ForeColor = valid ? Teal : RedDark;
        }

        private void UpdateIfscState()
        {
            if (string.IsNullOrWhiteSpace(_txtIfsc.Text))
            {
                _lblIfscHint.Text = string.Empty;
                return;
            }
            bool valid = _vendorSvc.ValidateIFSC(_txtIfsc.Text);
            _lblIfscHint.Text = valid ? "Valid" : "Invalid format";
            _lblIfscHint.ForeColor = valid ? Teal : RedDark;
        }

        private void UpdateGstTypeState()
        {
            bool unregistered = string.Equals(Convert.ToString(_cmbGstType.SelectedItem), "Unregistered", StringComparison.OrdinalIgnoreCase);
            if (unregistered)
                _chkRcm.Checked = true;
            _lblTaxNote.Text = unregistered ? "RCM applies - you pay GST on behalf" : string.Empty;
        }

        private void UpdateTdsVisibility()
        {
            bool visible = _chkTds.Checked;
            _cmbTdsSection.Visible = visible;
            _numTdsRate.Visible = visible;
            ApplyTdsDefaults();
        }

        private void ApplyTdsDefaults()
        {
            if (!_chkTds.Checked)
                return;
            decimal rate;
            if (_tdsRates.TryGetValue(Convert.ToString(_cmbTdsSection.SelectedItem), out rate))
                _numTdsRate.Value = Math.Min(_numTdsRate.Maximum, rate);
        }

        private void UpdateCreditVisuals()
        {
            int width = (int)Math.Min(_creditTrack.Width, (_numCreditDays.Value / 90m) * _creditTrack.Width);
            _creditFill.Width = Math.Max(0, width);
            _lblMsmeWarning.Visible = !string.Equals(Convert.ToString(_cmbMsme.SelectedItem), "No", StringComparison.OrdinalIgnoreCase) && _numCreditDays.Value > 45;
            _lblMsmeWarning.Text = "MSME vendors: max 45 days under MSME Act";
        }

        private void UpdateActionState()
        {
            bool hasVendor = _currentVendor != null && _currentVendor.VendorID > 0;
            _btnRaisePo.Enabled = hasVendor && !_isNewMode;
            _btnViewPos.Enabled = hasVendor && !_isNewMode;
            _btnArchive.Enabled = hasVendor && !_isNewMode && !_currentVendor.IsArchived;
            if (_btnWhatsApp != null)
                _btnWhatsApp.Enabled = hasVendor && !_isNewMode;
        }

        private void ShowVendorWhatsAppAction()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;

            string name = string.IsNullOrWhiteSpace(_currentVendor.VendorName) ? "Vendor" : _currentVendor.VendorName;
            string message = "Hi " + name + ",\r\n\r\nSharing an update from ServoERP regarding purchases/vendor coordination. Please confirm when convenient.\r\n\r\nRegards,\r\nServoERP";
            WhatsAppQuickActionDialog.ShowFor(this, new WhatsAppQuickActionContext
            {
                Module = "Vendors",
                SourceId = _currentVendor.VendorID,
                ContactName = name,
                Phone = FirstNonEmpty(_currentVendor.WhatsAppNumber, _currentVendor.Phone),
                TemplateType = "Vendor update",
                Message = message,
                LinkedRecordType = "Vendor",
                LinkedRecord = name,
                LinkedRecordId = _currentVendor.VendorID
            });
        }

        private static Color WhatsAppColor()
        {
            return Color.FromArgb(22, 163, 74);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            return string.Empty;
        }

        private async Task SaveVendorAsync(bool showMessages)
        {
            try
            {
                VendorDetailDto vendor = BuildVendorFromForm();
                List<string> validation = ValidateVendor(vendor);
                if (validation.Count > 0)
                {
                    if (showMessages)
                        MessageBox.Show("Please fill: " + string.Join(", ", validation), "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_isNewMode)
                {
                    int newId = await Task.Run(() => _vendorSvc.Create(vendor));
                    await RefreshAsync(false);
                    await LoadVendorDetailAsync(newId);
                }
                else
                {
                    vendor.VendorID = _currentVendor.VendorID;
                    vendor.CreatedDate = _currentVendor.CreatedDate;
                    await Task.Run(() => _vendorSvc.Update(vendor));
                    await RefreshAsync(true);
                }
                if (showMessages)
                    MessageBox.Show("Vendor saved.", "Vendors", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.SaveVendorAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Saving vendor", ex);
            }
        }

        private async Task AutoSaveNotesAsync()
        {
            if (_isBinding || _isNewMode || _currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            try
            {
                VendorDetailDto vendor = BuildVendorFromForm();
                vendor.VendorID = _currentVendor.VendorID;
                vendor.CreatedDate = _currentVendor.CreatedDate;
                await Task.Run(() => _vendorSvc.Update(vendor));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.AutoSaveNotesAsync", ex);
            }
        }

        private async Task ArchiveVendorAsync()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            DialogResult result = MessageBox.Show("Archive " + _currentVendor.VendorName + "?", "Archive vendor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
            try
            {
                await Task.Run(() => _vendorSvc.ArchiveVendor(_currentVendor.VendorID));
                await RefreshAsync(false);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.ArchiveVendorAsync", ex);
                MessageBox.Show(ex.Message, "Archive blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RaisePo()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            _vendorSvc.RaiseQuickPO(_currentVendor.VendorID);
        }

        private void ViewPurchaseOrders()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            _vendorSvc.RaiseQuickPO(_currentVendor.VendorID);
        }

        private void ShowMergeDialog()
        {
            if (_duplicateGroups.Count == 0)
                return;
            using (MergeDuplicatesDialog dlg = new MergeDuplicatesDialog(_duplicateGroups))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.MergeSelections.Count > 0)
                {
                    try
                    {
                        foreach (KeyValuePair<int, List<int>> item in dlg.MergeSelections)
                            _vendorSvc.MergeDuplicates(item.Key, item.Value);
                        Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            BeginInvoke((Action)(async () => await RefreshAsync(true)));
                        });
                    }
                    catch (Exception ex)
                    {
                        AppRuntime.LogException("VendorForm.ShowMergeDialog", ex);
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Merging vendors", ex);
                    }
                }
            }
        }

        private void ExportCsv()
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "CSV files (*.csv)|*.csv";
                    dialog.FileName = "Vendors_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("VendorName,Category,VendorType,City,State,Phone,Email,GSTIN,PAN,CreditDays,OutstandingBalance,TotalPurchased,OpenPOs,IsActive,MSMERegistered,Tags");
                    foreach (VendorSummaryDto summary in _vendorSummaries.OrderBy(v => v.VendorName))
                    {
                        VendorDetailDto detail = _vendorSvc.GetVendorDetail(summary.VendorId);
                        sb.AppendLine(string.Join(",", Csv(detail.VendorName), Csv(detail.Category), Csv(detail.VendorType), Csv(detail.City), Csv(StateNameByCode(detail.StateCode)), Csv(detail.Phone), Csv(detail.Email), Csv(detail.GSTNumber), Csv(detail.PANNumber), Csv(detail.DefaultCreditDays.ToString()), Csv(IndiaFormatHelper.FormatCurrency(detail.OutstandingBalance)), Csv(IndiaFormatHelper.FormatCurrency(detail.TotalPurchased)), Csv(detail.OpenPOCount.ToString()), Csv(detail.IsActive ? "Yes" : "No"), Csv(detail.MSMERegistered), Csv(detail.SpecialisationTags)));
                    }
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    Process.Start(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.ExportCsv", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Export vendors", ex);
            }
        }

        private VendorDetailDto BuildVendorFromForm()
        {
            VendorDetailDto vendor = _currentVendor ?? new VendorDetailDto();
            vendor.VendorName = (_txtVendorName.Text ?? string.Empty).Trim();
            vendor.Category = Convert.ToString(_cmbCategory.SelectedItem);
            vendor.SpecialisationTags = GetTagsText();
            vendor.VendorType = Convert.ToString(_cmbVendorType.SelectedItem);
            vendor.MSMERegistered = Convert.ToString(_cmbMsme.SelectedItem);
            vendor.MSMENumber = (_txtMsmeNumber.Text ?? string.Empty).Trim();
            vendor.Phone = (_txtPhone.Text ?? string.Empty).Trim();
            vendor.WhatsAppNumber = (_txtWhatsApp.Text ?? string.Empty).Trim();
            vendor.Email = (_txtEmail.Text ?? string.Empty).Trim();
            vendor.Address = (_txtAddress.Text ?? string.Empty).Trim();
            vendor.City = (_txtCity.Text ?? string.Empty).Trim();
            vendor.StateCode = IndiaStateCatalog.GetCodeByName(Convert.ToString(_cmbState.SelectedItem));
            vendor.GSTNumber = (_txtGstin.Text ?? string.Empty).Trim().ToUpperInvariant();
            vendor.PANNumber = (_txtPan.Text ?? string.Empty).Trim().ToUpperInvariant();
            vendor.GSTRegistrationType = Convert.ToString(_cmbGstType.SelectedItem);
            vendor.TDSApplicable = _chkTds.Checked;
            vendor.TDSSection = vendor.TDSApplicable ? Convert.ToString(_cmbTdsSection.SelectedItem) : null;
            vendor.TDSRate = vendor.TDSApplicable ? _numTdsRate.Value : 0m;
            vendor.RCMApplicable = _chkRcm.Checked;
            vendor.DefaultCreditDays = (int)_numCreditDays.Value;
            vendor.PreferredPaymentMode = Convert.ToString(_cmbPaymentMode.SelectedItem);
            vendor.BankAccountNumber = (_txtBankAccount.Text ?? string.Empty).Trim();
            vendor.BankIFSC = (_txtIfsc.Text ?? string.Empty).Trim().ToUpperInvariant();
            vendor.BankAccountName = (_txtAccountName.Text ?? string.Empty).Trim();
            vendor.BankName = (_txtBankName.Text ?? string.Empty).Trim();
            vendor.Notes = GetControlText(_txtNotes);
            vendor.IsActive = true;
            vendor.IsArchived = _currentVendor != null && _currentVendor.IsArchived;
            vendor.TotalPurchased = _currentVendor?.TotalPurchased ?? 0m;
            vendor.CreatedDate = _currentVendor?.CreatedDate ?? DateTime.Now;
            _vendorSvc.OnGSTINChanged(vendor);
            return vendor;
        }

        private List<string> ValidateVendor(Vendor vendor)
        {
            ClearErrors();
            List<string> issues = new List<string>();
            if (string.IsNullOrWhiteSpace(vendor.VendorName))
            {
                issues.Add("Vendor name");
                _errors.SetError(_txtVendorName, "Vendor name is required.");
            }
            if (!string.IsNullOrWhiteSpace(vendor.GSTNumber) && !_vendorSvc.ValidateGSTIN(vendor.GSTNumber))
            {
                issues.Add("valid GSTIN");
                _errors.SetError(_txtGstin, "GSTIN format is invalid.");
            }
            if (!string.IsNullOrWhiteSpace(vendor.BankIFSC) && !_vendorSvc.ValidateIFSC(vendor.BankIFSC))
            {
                issues.Add("valid IFSC");
                _errors.SetError(_txtIfsc, "IFSC format is invalid.");
            }
            if (!string.IsNullOrWhiteSpace(vendor.PANNumber) && !_vendorSvc.ValidatePAN(vendor.PANNumber))
            {
                issues.Add("valid PAN");
                _errors.SetError(_txtPan, "PAN format is invalid.");
            }
            return issues;
        }

        private void ClearErrors()
        {
            _errors.Clear();
        }

        private void LayoutDetailCards()
        {
            if (_rightHost == null)
                return;
            int viewportWidth = Math.Max(0, _rightScroll.ClientSize.Width - _rightScroll.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
            int width = Math.Max(1160, viewportWidth);
            int y = 0;
            int gap = 14;
            int fullWidth = Math.Max(1160, width - 2);
            _vendorHeaderRow.Visible = false;
            _vendorHeaderRow.SetBounds(0, 0, fullWidth, 0);
            _statsRow.Location = new Point(0, y);
            _statsRow.Width = fullWidth;
            int actionCardWidth = Math.Max(460, (int)(fullWidth * 0.36));
            int statWidth = (fullWidth - actionCardWidth - 36) / 3;
            int statX = 0;
            for (int i = 0; i < _statsRow.Controls.Count; i++)
            {
                int cardWidth = i == 3 ? fullWidth - statX : statWidth;
                _statsRow.Controls[i].SetBounds(statX, 0, cardWidth, 70);
                statX += cardWidth + 12;
            }
            y += _statsRow.Height + gap;
            _warningStrip.Location = new Point(0, y);
            _warningStrip.Width = fullWidth;
            if (_warningStrip.Visible)
                y += _warningStrip.Height + gap;

            _cardIdentity.SetBounds(0, y, fullWidth, _cardIdentity.Height);
            _cardIdentity.ContentPanel.AutoScroll = false;
            y += _cardIdentity.Height + gap;
            _cardRecentPos.SetBounds(0, y, fullWidth, _cardRecentPos.Height);
            _cardRecentPos.ContentPanel.AutoScroll = false;
            _recentPoFlow.Width = _cardRecentPos.ContentPanel.ClientSize.Width - 4;
            _lblRecentPoFooter.Width = _cardRecentPos.ContentPanel.ClientSize.Width - 4;
            y += _cardRecentPos.Height + gap;
            _cardNotes.SetBounds(0, y, fullWidth, _cardNotes.Height);
            _cardNotes.ContentPanel.AutoScroll = false;
            y += _cardNotes.Height + gap;
            _rightHost.Size = new Size(fullWidth, y + 4);
            RestoreVendorInputChrome();
        }

        private void RestoreVendorInputChrome()
        {
            foreach (Control control in AllDescendants(this))
            {
                if (control is TextBox textBox)
                {
                    bool hosted = textBox.Parent != null && Equals(textBox.Parent.Tag, "vendor-input-host");
                    textBox.BorderStyle = hosted ? BorderStyle.None : BorderStyle.FixedSingle;
                    textBox.BackColor = textBox.ReadOnly ? Surface : White;
                    textBox.Height = textBox.Multiline ? textBox.Height : Math.Max(24, textBox.Height);
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.FlatStyle = FlatStyle.Flat;
                    comboBox.Height = Math.Max(28, comboBox.Height);
                }
                else if (control is NumericUpDown numeric)
                {
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    numeric.Height = Math.Max(28, numeric.Height);
                }
            }
        }

        private static IEnumerable<Control> AllDescendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in AllDescendants(child))
                    yield return descendant;
            }
        }

        private static Label CreateBadgeLabel(int width, Color bg, Color fg)
        {
            Label label = new Label { AutoSize = false, Width = width, Height = 22, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 8, 0) };
            label.Paint += (s, e) => DrawRoundedSurface(e.Graphics, label.ClientRectangle, bg, 11);
            return label;
        }

        private Button MakeActionButton(string text, Color bg, Color fg, int width, bool outline)
        {
            Button button = new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 8, 0) };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = outline ? Border : bg;
            button.FlatAppearance.MouseOverBackColor = outline ? Surface : ControlPaint.Dark(bg);
            return button;
        }

        private static void DrawRoundedSurface(Graphics graphics, Rectangle rect, Color fill, int radius)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle drawRect = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (GraphicsPath path = GetRoundedRect(drawRect, radius))
            using (SolidBrush brush = new SolidBrush(fill))
                graphics.FillPath(brush, path);
        }

        private static void DrawSearchBox(Graphics graphics, Rectangle rect)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle drawRect = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (GraphicsPath path = GetRoundedRect(drawRect, 8))
            using (SolidBrush brush = new SolidBrush(White))
            using (Pen pen = new Pen(Border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static void DrawCardOutline(Graphics graphics, Rectangle rect)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle drawRect = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (GraphicsPath path = GetRoundedRect(drawRect, 10))
            using (SolidBrush brush = new SolidBrush(White))
            using (Pen pen = new Pen(Border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ConfigurePlaceholder(TextBox box, string placeholder)
        {
            box.GotFocus += (s, e) =>
            {
                if (box.ForeColor == TextHint && box.Text == placeholder)
                {
                    box.Text = string.Empty;
                    box.ForeColor = TextPrimary;
                    if (box == _txtSearch) _searchPlaceholderActive = false;
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = placeholder;
                    box.ForeColor = TextHint;
                    if (box == _txtSearch) _searchPlaceholderActive = true;
                }
            };
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = placeholder;
                box.ForeColor = TextHint;
                if (box == _txtSearch) _searchPlaceholderActive = true;
            }
        }

        private string GetSearchText() => _searchPlaceholderActive ? string.Empty : _txtSearch.Text;
        private void UpdateSearchClear() { _btnClearSearch.Visible = !string.IsNullOrWhiteSpace(GetSearchText()); }
        private static string JoinBullet(params string[] parts) { return string.Join(" - ", parts.Where(p => !string.IsNullOrWhiteSpace(p))); }
        private static string Csv(string value) { string safe = (value ?? string.Empty).Replace("\"", "\"\""); return "\"" + safe + "\""; }

        private static Label PlaceLabel(Control parent, string text, int x, int y)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextSecondary };
            parent.Controls.Add(label);
            return label;
        }

        private static TextBox PlaceTextBox(Control parent, int x, int y, int width)
        {
            Panel host = CreateInputHost(x, y, width, 28);
            TextBox box = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                BorderStyle = BorderStyle.None,
                BackColor = White,
                Margin = Padding.Empty
            };
            host.Controls.Add(box);
            parent.Controls.Add(host);
            return box;
        }

        private static Panel CreateInputHost(int x, int y, int width, int height)
        {
            Panel host = new Panel
            {
                Tag = "vendor-input-host",
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = White,
                Padding = new Padding(8, 6, 8, 4)
            };
            host.Paint += (s, e) =>
            {
                Panel panel = (Panel)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 6))
                using (Pen pen = new Pen(DS.Slate300))
                    e.Graphics.DrawPath(pen, path);
            };
            return host;
        }

        private static void SetInputHostHeight(Control input, int height)
        {
            if (input?.Parent != null && Equals(input.Parent.Tag, "vendor-input-host"))
            {
                input.Parent.Height = height;
                input.Parent.Padding = new Padding(8, 6, 8, 6);
            }
        }

        private static ComboBox PlaceComboBox(Control parent, int x, int y, int width, List<string> source)
        {
            ComboBox combo = new ComboBox { Location = new Point(x, y), Size = new Size(width, 24), Font = new Font("Segoe UI", 9f), DropDownStyle = ComboBoxStyle.DropDownList };
            if (source != null) combo.Items.AddRange(source.Cast<object>().ToArray());
            parent.Controls.Add(combo);
            UIHelper.ApplyInputStyle(combo);
            return combo;
        }

        private void SetStateComboByCodeOrName(string stateCodeOrName)
        {
            string name = StateNameByCode(stateCodeOrName);
            if (string.IsNullOrWhiteSpace(name))
                name = IndiaStateCatalog.NormalizeStateName(stateCodeOrName);
            if (!string.IsNullOrWhiteSpace(name) && _cmbState.Items.Contains(name))
                _cmbState.SelectedItem = name;
            else if (_cmbState.Items.Count > 0)
                _cmbState.SelectedIndex = 0;
        }

        private string StateNameByCode(string stateCode)
        {
            if (string.IsNullOrWhiteSpace(stateCode))
                return string.Empty;
            foreach (string name in IndiaStateCatalog.Names)
            {
                if (string.Equals(IndiaStateCatalog.GetCodeByName(name), stateCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return string.Empty;
        }

        private List<string> ParseTags(string csv)
        {
            return (csv ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        }

        private string GetTagsText()
        {
            return string.Join(",", _tagFlow.Controls.OfType<Panel>().Select(p => p.Controls.OfType<Label>().FirstOrDefault()).Where(l => l != null).Select(l => l.Text.Trim()));
        }

        private static string GetControlText(TextBox textBox)
        {
            return textBox.ForeColor == TextHint ? string.Empty : textBox.Text;
        }

        private sealed class MergeDuplicatesDialog : Form
        {
            public Dictionary<int, List<int>> MergeSelections { get; } = new Dictionary<int, List<int>>();

            public MergeDuplicatesDialog(List<DuplicateGroupDto> groups)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Merge duplicate vendors";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                Width = 560;
                Height = 480;
                BackColor = White;

                Label subtitle = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 12, 16, 8), Text = "Select the master vendor to keep. All POs from duplicates will move to master.", Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary };
                FlowLayoutPanel body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = White, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 8, 16, 16) };
                foreach (DuplicateGroupDto group in groups)
                    body.Controls.Add(BuildGroup(group));
                Button btnClose = new Button { Text = "Done", Dock = DockStyle.Bottom, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Teal, ForeColor = White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => DialogResult = DialogResult.OK;
                Controls.Add(body);
                Controls.Add(btnClose);
                Controls.Add(subtitle);
            }

            private Control BuildGroup(DuplicateGroupDto group)
            {
                Panel panel = new Panel { Width = 500, Height = 38 + (group.Vendors.Count * 32) + 42, BackColor = White, Margin = new Padding(0, 0, 0, 12) };
                panel.Paint += (s, e) => DrawCardOutline(e.Graphics, panel.ClientRectangle);
                Label title = new Label { Text = group.NormalisedName + " (" + group.Vendors.Count + ")", Location = new Point(12, 10), Size = new Size(320, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary };
                panel.Controls.Add(title);
                List<RadioButton> radios = new List<RadioButton>();
                int y = 38;
                foreach (DuplicateVendorItemDto vendor in group.Vendors)
                {
                    RadioButton radio = new RadioButton { Text = vendor.VendorName + " - " + vendor.OpenPOCount + " POs - " + IndiaFormatHelper.FormatCurrency(vendor.OutstandingBalance), Location = new Point(16, y), Size = new Size(460, 20), Font = new Font("Segoe UI", 9f), Tag = vendor.VendorId };
                    if (radios.Count == 0) radio.Checked = true;
                    radios.Add(radio);
                    panel.Controls.Add(radio);
                    y += 28;
                }
                Button btnMerge = new Button { Text = "Merge this group", Location = new Point(16, y + 2), Size = new Size(128, 28), FlatStyle = FlatStyle.Flat, BackColor = White, ForeColor = TextPrimary, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
                btnMerge.FlatAppearance.BorderSize = 1;
                btnMerge.FlatAppearance.BorderColor = Border;
                btnMerge.Click += (s, e) =>
                {
                    RadioButton master = radios.FirstOrDefault(r => r.Checked);
                    if (master == null) return;
                    int masterId = Convert.ToInt32(master.Tag);
                    List<int> duplicateIds = radios.Select(r => Convert.ToInt32(r.Tag)).Where(id => id != masterId).ToList();
                    string masterName = group.Vendors.First(v => v.VendorId == masterId).VendorName;
                    DialogResult result = MessageBox.Show("Merge " + group.Vendors.Count + " vendors into " + masterName + "? " + duplicateIds.Count + " purchase orders will be reassigned. Duplicate records will be archived.", "Confirm merge", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;
                    MergeSelections[masterId] = duplicateIds;
                    btnMerge.Text = "Queued";
                    btnMerge.Enabled = false;
                };
                panel.Controls.Add(btnMerge);
                return panel;
            }
        }
    }
}

