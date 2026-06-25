using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class GeoIntelligenceForm : DeferredPageControl
    {
        private readonly JobService _jobService = new JobService();
        private readonly EmployeeService _employeeService = new EmployeeService();

        private readonly Color PageBg = Color.FromArgb(246, 248, 251);
        private readonly Color White = Color.White;
        private readonly Color Border = DS.Border;
        private readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
        private readonly Color Muted = Color.FromArgb(100, 116, 139);
        private readonly Color Primary = Color.FromArgb(67, 56, 202);
        private readonly Color Blue = Color.FromArgb(37, 99, 235);
        private readonly Color Info = Color.FromArgb(6, 182, 212);
        private readonly Color Success = Color.FromArgb(16, 185, 129);
        private readonly Color Warning = Color.FromArgb(245, 158, 11);
        private readonly Color Danger = Color.FromArgb(239, 68, 68);
        private readonly Color MapAvailable = Color.FromArgb(34, 197, 94);
        private readonly Color MapOnJob = Color.FromArgb(59, 130, 246);
        private readonly Color MapTraveling = Color.FromArgb(245, 158, 11);
        private readonly Color MapBusy = Color.FromArgb(239, 68, 68);
        private readonly Color MapOffline = Color.FromArgb(156, 163, 175);

        private ComboBox _cmbLocation;
        private CheckBox _chkAutoRefresh;
        private Label _autoRefreshPulse;
        private Button _btnRefresh;
        private Button _btnViewAllJobs;
        private TextBox _txtSearch;
        private ComboBox _cmbType;
        private ComboBox _cmbPriority;
        private DispatchJobListModule _jobListModule;
        private FlowLayoutPanel _techList;
        private Panel _timelinePanel;
        private Label _lblStatus;
        private Label _lblTechTitle;
        private ComboBox _cmbTechnicianDesignationFilter;

        private Label _kpiUnassigned;
        private Label _kpiToday;
        private Label _kpiOverdue;
        private Label _kpiProgress;
        private Label _kpiCompleted;
        private Label _kpiTechnicians;
        private Label _siteKpiActiveSites;
        private Label _siteKpiOpenIssues;
        private Label _siteKpiCriticalSites;
        private Label _siteKpiDuePm;
        private Label _siteKpiSlaRisk;
        private Label _siteKpiOffline;
        private Label _siteKpiHealth;
        private Label _siteKpiVisits;
        private Label _siteDistributionCenter;
        private Label _siteRevenueTotal;
        private Label _siteRevenueTop;
        private Label _siteRevenueLow;
        private Label _slaComplianceLabel;
        private Label _slaTotalTickets;
        private Label _slaMetTickets;
        private Label _slaBreachedTickets;
        private Panel _siteDistributionChart;
        private Panel _technicianPresenceChart;
        private Panel _slaGaugePanel;
        private Panel _siteHealthTrendPanel;
        private DataGridView _regionGrid;
        private DataGridView _maintenanceGrid;
        private DataGridView _problemGrid;
        private DataGridView _attentionGrid;
        private TableLayoutPanel _equipmentGrid;
        private TableLayoutPanel _technicianPresenceList;

        private Label _detailJobNumber;
        private Label _detailBadge;
        private Label _detailTitle;
        private Label _detailSla;
        private Label _detailClient;
        private Label _detailSite;
        private Label _detailAddress;
        private Label _detailScheduleBanner;
        private TabControl _detailTabs;
        private ComboBox _cmbAssignTechnician;
        private ComboBox _cmbDetailStatus;
        private DateTimePicker _dtpSchedule;
        private Label _lblScheduleWarning;
        private TextBox _txtProblem;
        private Label _lblSuggestedTech;
        private Label _lblJobInfo;
        private Panel _jobInformationCard;
        private TableLayoutPanel _quickActionsPanel;

        private readonly List<Button> _queueTabs = new List<Button>();
        private readonly Dictionary<Button, string> _queueTabKeys = new Dictionary<Button, string>();
        private readonly List<Button> _jobActionButtons = new List<Button>();
        private readonly List<Panel> _jobCards = new List<Panel>();
        private readonly List<Panel> _techCards = new List<Panel>();
        private readonly Timer _autoRefreshTimer = new Timer();
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };
        private const int TechnicianCardWidth = 220;
        private const int TechnicianCardHeight = 132;

        private List<JobSummaryDto> _jobs = new List<JobSummaryDto>();
        private List<Employee> _technicians = new List<Employee>();
        private List<JobSummaryDto> _visibleJobs = new List<JobSummaryDto>();
        private JobSummaryDto _selectedJob;
        private Employee _selectedTechnician;
        private bool _syncingJobSelection;
        private string _activeQueue = "All";
        private bool _binding;
        private bool _usingFallbackJobs;
        private Timer _initialDispatchLoadTimer;
        private bool _siteMonitorLayout;

        public Action<int> OnNavigate { get; set; }
        public Action<int> OnOpenClientSite { get; set; }
        public Action<int> OnOpenJobDetail { get; set; }

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public GeoIntelligenceForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            _autoRefreshTimer.Interval = 30000;
            _autoRefreshTimer.Tick += (s, e) => QueueLoadDispatchData();
            Load += (s, e) => QueueInitialDispatchLoad();
        }

        private void QueueInitialDispatchLoad()
        {
            if (_initialDispatchLoadTimer != null)
                return;

            _initialDispatchLoadTimer = new Timer { Interval = 750 };
            _initialDispatchLoadTimer.Tick += (s, e) =>
            {
                _initialDispatchLoadTimer.Stop();
                _initialDispatchLoadTimer.Dispose();
                _initialDispatchLoadTimer = null;
                QueueLoadDispatchData();
            };
            _initialDispatchLoadTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoRefreshTimer.Stop();
                _autoRefreshTimer.Dispose();
                if (_initialDispatchLoadTimer != null)
                {
                    _initialDispatchLoadTimer.Stop();
                    _initialDispatchLoadTimer.Dispose();
                    _initialDispatchLoadTimer = null;
                }
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            Controls.Clear();
            _siteMonitorLayout = true;

            Panel scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PageBg,
                Padding = new Padding(18)
            };
            Controls.Add(scroll);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = PageBg,
                ColumnCount = 1,
                RowCount = 3
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 860));
            scroll.Controls.Add(root);
            scroll.Resize += (s, e) => root.Width = Math.Max(1120, scroll.ClientSize.Width - scroll.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            root.Width = Math.Max(1120, scroll.ClientSize.Width - scroll.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);

            root.Controls.Add(BuildSiteMonitorHeader(), 0, 0);
            root.Controls.Add(BuildSiteMonitorKpis(), 0, 1);
            root.Controls.Add(BuildSiteMonitorDashboard(), 0, 2);
        }

        private Control BuildSiteMonitorHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 2,
                RowCount = 1
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));

            Panel copy = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Label title = new Label
            {
                Text = "Site Monitor",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoEllipsis = true
            };
            Label subtitle = new Label
            {
                Text = "Real-time overview of all customer sites and operations",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            };
            copy.Controls.Add(subtitle);
            copy.Controls.Add(title);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = PageBg,
                Padding = new Padding(0, 4, 0, 0)
            };
            Button filters = MakeSiteToolbarButton("Filters", ModernIconKind.Filter, 96);
            filters.Click += (s, e) => OpenDispatchFilters();
            _chkAutoRefresh = new CheckBox
            {
                Text = "Auto Refresh: On",
                Checked = true,
                Appearance = Appearance.Button,
                Width = 156,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = TextSecondary,
                BackColor = White,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(8, 0, 0, 0)
            };
            _chkAutoRefresh.FlatAppearance.BorderColor = Border;
            _chkAutoRefresh.CheckedChanged += (s, e) => UpdateAutoRefreshState();
            Button date = MakeSiteToolbarButton(DateTime.Today.AddDays(-6).ToString("MMM d") + " - " + DateTime.Today.ToString("MMM d, yyyy"), ModernIconKind.Calendar, 206);
            actions.Controls.Add(filters);
            actions.Controls.Add(_chkAutoRefresh);
            actions.Controls.Add(date);

            _lblStatus = new Label
            {
                Text = "Site Monitor ready.",
                AutoSize = false,
                Width = 1,
                Height = 1,
                Visible = false
            };
            actions.Controls.Add(_lblStatus);

            header.Controls.Add(copy, 0, 0);
            header.Controls.Add(actions, 1, 0);
            return header;
        }

        private Control BuildSiteMonitorKpis()
        {
            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 8,
                RowCount = 1,
                Padding = new Padding(0, 10, 0, 12)
            };
            for (int i = 0; i < 8; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));

            _siteKpiActiveSites = AddSiteKpi(row, 0, "active_sites", "Active Sites", "All customer sites", ModernIconKind.Company, Blue);
            _siteKpiOpenIssues = AddSiteKpi(row, 1, "open_issues", "Open Issues", "Require attention", ModernIconKind.Alert, Danger);
            _siteKpiCriticalSites = AddSiteKpi(row, 2, "critical_sites", "Critical Sites", "High priority sites", ModernIconKind.Security, Danger);
            _siteKpiDuePm = AddSiteKpi(row, 3, "due_pm", "Due for PM", "Next 30 days", ModernIconKind.Calendar, Color.FromArgb(249, 115, 22));
            _siteKpiSlaRisk = AddSiteKpi(row, 4, "sla_risk", "SLA Risk", "At risk", ModernIconKind.Activity, Warning);
            _siteKpiOffline = AddSiteKpi(row, 5, "equipment_offline", "Equipment Offline", "Not reporting", ModernIconKind.Analytics, Color.FromArgb(147, 51, 234));
            _siteKpiHealth = AddSiteKpi(row, 6, "site_health", "Site Health", "Live score", ModernIconKind.Status, Success);
            _siteKpiVisits = AddSiteKpi(row, 7, "site_visits", "Site Visits", "This week", ModernIconKind.Technician, Blue);
            return row;
        }

        private Label AddSiteKpi(TableLayoutPanel row, int column, string detailKey, string title, string sub, ModernIconKind icon, Color accent)
        {
            Panel card = CreateCard();
            card.Margin = new Padding(column == 0 ? 0 : 6, 0, column == 7 ? 0 : 6, 0);
            card.Padding = new Padding(12, 10, 10, 8);
            AttachSiteMonitorDrilldown(card, detailKey);

            Label badge = ModernIconSystem.Badge(icon, 42, Lighten(accent, 0.84f), accent, 12);
            badge.Dock = DockStyle.Left;
            badge.Width = 48;
            Label value = new Label
            {
                Text = "0",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoEllipsis = true
            };
            Label label = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoEllipsis = true
            };
            Label small = new Label
            {
                Text = sub,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 7.7f),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            };
            Panel text = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(10, 0, 0, 0) };
            text.Controls.Add(small);
            text.Controls.Add(label);
            text.Controls.Add(value);
            card.Controls.Add(text);
            card.Controls.Add(badge);
            AttachSiteMonitorDrilldown(card, detailKey);
            row.Controls.Add(card, column, 0);
            return value;
        }

        private Control BuildSiteMonitorDashboard()
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 3,
                RowCount = 3
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 34f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 30f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 36f));

            grid.Controls.Add(BuildSiteStatusCard(), 0, 0);
            grid.Controls.Add(BuildRegionCard(), 1, 0);
            grid.Controls.Add(BuildUpcomingMaintenanceCard(), 2, 0);
            grid.Controls.Add(BuildProblematicSitesCard(), 0, 1);
            grid.Controls.Add(BuildImmediateAttentionCard(), 1, 1);
            grid.Controls.Add(BuildEquipmentSummaryCard(), 2, 1);
            grid.Controls.Add(BuildTechnicianPresenceCard(), 0, 2);
            grid.Controls.Add(BuildRevenueCard(), 1, 2);
            TableLayoutPanel rightBottom = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBg, ColumnCount = 2, RowCount = 1 };
            rightBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47f));
            rightBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53f));
            rightBottom.Controls.Add(BuildSlaPerformanceCard(), 0, 0);
            rightBottom.Controls.Add(BuildHealthTrendCard(), 1, 0);
            grid.Controls.Add(rightBottom, 2, 2);
            return grid;
        }

        private Panel BuildDashboardCard(string detailKey, string title, ModernIconKind icon, Color accent)
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 12, 12);
            card.Padding = new Padding(16, 46, 16, 14);
            AttachSiteMonitorDrilldown(card, detailKey);
            Label heading = new Label
            {
                Name = "SiteMonitorCardHeading",
                Text = title,
                Dock = DockStyle.None,
                Location = new Point(16, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Width = Math.Max(120, card.Width - 32),
                Height = 34,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextPrimary,
                BackColor = White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0)
            };
            card.Controls.Add(heading);
            heading.BringToFront();
            card.ControlAdded += (s, e) =>
            {
                AttachSiteMonitorDrilldown(e.Control, detailKey);
                heading.BringToFront();
            };
            card.Resize += (s, e) => heading.Width = Math.Max(120, card.ClientSize.Width - 32);
            return card;
        }

        private Control BuildSiteStatusCard()
        {
            Panel card = BuildDashboardCard("status_distribution", "Site Status Distribution", ModernIconKind.Status, Success);
            _siteDistributionChart = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = White };
            _siteDistributionChart.Paint += DrawSiteDistribution;
            Panel legend = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(14, 26, 0, 0) };
            _siteDistributionCenter = new Label { Text = "0\r\nTotal Sites", Dock = DockStyle.Bottom, Height = 52, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter };
            _siteDistributionChart.Controls.Add(_siteDistributionCenter);
            card.Controls.Add(legend);
            card.Controls.Add(_siteDistributionChart);
            card.Tag = legend;
            return card;
        }

        private Control BuildRegionCard()
        {
            Panel card = BuildDashboardCard("regions", "Sites by Region", ModernIconKind.Location, Blue);
            _regionGrid = MakeSiteGrid();
            _regionGrid.Columns.Add("Region", "Region");
            _regionGrid.Columns.Add("Sites", "Sites");
            _regionGrid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            card.Controls.Add(_regionGrid);
            return card;
        }

        private Control BuildUpcomingMaintenanceCard()
        {
            Panel card = BuildDashboardCard("maintenance", "Upcoming Maintenance (Next 7 Days)", ModernIconKind.Calendar, Blue);
            _maintenanceGrid = MakeSiteGrid();
            _maintenanceGrid.Columns.Add("Site", "Site");
            _maintenanceGrid.Columns.Add("Date", "Date");
            _maintenanceGrid.Columns.Add("Type", "Type");
            card.Controls.Add(_maintenanceGrid);
            return card;
        }

        private Control BuildProblematicSitesCard()
        {
            Panel card = BuildDashboardCard("problematic_sites", "Most Problematic Sites", ModernIconKind.Alert, Danger);
            _problemGrid = MakeSiteGrid();
            _problemGrid.Columns.Add("Site", "Site");
            _problemGrid.Columns.Add("Open", "Open Tickets");
            _problemGrid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            card.Controls.Add(_problemGrid);
            return card;
        }

        private Control BuildImmediateAttentionCard()
        {
            Panel card = BuildDashboardCard("immediate_attention", "Sites Requiring Immediate Attention", ModernIconKind.Alert, Danger);
            _attentionGrid = MakeSiteGrid();
            _attentionGrid.Columns.Add("Site", "Site");
            _attentionGrid.Columns.Add("Issue", "Issue");
            _attentionGrid.Columns.Add("SLA", "SLA Remaining");
            card.Controls.Add(_attentionGrid);
            return card;
        }

        private Control BuildEquipmentSummaryCard()
        {
            Panel card = BuildDashboardCard("equipment_summary", "Equipment Summary", ModernIconKind.Inventory, Blue);
            _equipmentGrid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = White, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 4, 0, 0) };
            for (int i = 0; i < 3; i++) _equipmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int i = 0; i < 2; i++) _equipmentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            card.Controls.Add(_equipmentGrid);
            return card;
        }

        private Control BuildTechnicianPresenceCard()
        {
            Panel card = BuildDashboardCard("technician_presence", "Technician Presence", ModernIconKind.Technician, Blue);
            _technicianPresenceChart = new Panel { Dock = DockStyle.Right, Width = 150, BackColor = White };
            _technicianPresenceChart.Paint += DrawTechnicianPresence;
            _technicianPresenceList = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = White, ColumnCount = 2, RowCount = 4 };
            _technicianPresenceList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
            _technicianPresenceList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            card.Controls.Add(_technicianPresenceList);
            card.Controls.Add(_technicianPresenceChart);
            return card;
        }

        private Control BuildRevenueCard()
        {
            Panel card = BuildDashboardCard("site_revenue", "Site Revenue (This Month)", ModernIconKind.Money, Success);
            TableLayoutPanel table = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = White, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 12, 0, 0) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            for (int i = 0; i < 3; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            _siteRevenueTotal = AddRevenueRow(table, 0, "Total Site Revenue");
            _siteRevenueTop = AddRevenueRow(table, 1, "Top Revenue Site");
            _siteRevenueLow = AddRevenueRow(table, 2, "Lowest Revenue Site");
            card.Controls.Add(table);
            return card;
        }

        private Control BuildSlaPerformanceCard()
        {
            Panel card = BuildDashboardCard("sla_performance", "SLA Performance", ModernIconKind.Activity, Color.FromArgb(147, 51, 234));
            card.Margin = new Padding(0, 0, 12, 12);
            _slaGaugePanel = new Panel { Dock = DockStyle.Left, Width = 140, BackColor = White };
            _slaGaugePanel.Paint += DrawSlaGauge;
            _slaComplianceLabel = new Label { Text = "0%\r\nSLA Compliance", Dock = DockStyle.Bottom, Height = 52, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter };
            _slaGaugePanel.Controls.Add(_slaComplianceLabel);
            TableLayoutPanel stats = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = White, ColumnCount = 2, RowCount = 3, Padding = new Padding(8, 28, 0, 0) };
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            _slaTotalTickets = AddMiniStat(stats, 0, "Total Jobs", TextPrimary);
            _slaMetTickets = AddMiniStat(stats, 1, "Met SLA", Success);
            _slaBreachedTickets = AddMiniStat(stats, 2, "Breached", Danger);
            card.Controls.Add(stats);
            card.Controls.Add(_slaGaugePanel);
            return card;
        }

        private Control BuildHealthTrendCard()
        {
            Panel card = BuildDashboardCard("health_trend", "Site Health Trend", ModernIconKind.Activity, Success);
            card.Margin = new Padding(0, 0, 0, 12);
            _siteHealthTrendPanel = new Panel { Dock = DockStyle.Fill, BackColor = White };
            _siteHealthTrendPanel.Paint += DrawSiteHealthTrend;
            card.Controls.Add(_siteHealthTrendPanel);
            return card;
        }

        private Label AddRevenueRow(TableLayoutPanel table, int row, string label)
        {
            Label name = new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.4f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            Label value = new Label { Text = "Rs 0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.4f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };
            table.Controls.Add(name, 0, row);
            table.Controls.Add(value, 1, row);
            return value;
        }

        private Label AddMiniStat(TableLayoutPanel table, int row, string label, Color color)
        {
            Label name = new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.2f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
            Label value = new Label { Text = "0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.2f, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.MiddleRight };
            table.Controls.Add(name, 0, row);
            table.Controls.Add(value, 1, row);
            return value;
        }

        private DataGridView MakeSiteGrid()
        {
            DataGridView grid = MakeSmallGrid();
            grid.BackgroundColor = White;
            grid.ColumnHeadersHeight = 30;
            grid.RowTemplate.Height = 29;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            return grid;
        }

        private void AttachSiteMonitorDrilldown(Control control, string detailKey)
        {
            if (control == null || string.IsNullOrWhiteSpace(detailKey))
                return;

            bool alreadyAttached = string.Equals(Convert.ToString(control.Tag), detailKey, StringComparison.OrdinalIgnoreCase);
            if (!alreadyAttached)
            {
                control.Tag = detailKey;
                control.Cursor = Cursors.Hand;
                control.Click += (s, e) => OpenSiteMonitorDetail(detailKey);
                DataGridView grid = control as DataGridView;
                if (grid != null)
                    grid.CellDoubleClick += (s, e) => OpenSiteMonitorDetail(detailKey);
            }

            foreach (Control child in control.Controls)
                AttachSiteMonitorDrilldown(child, detailKey);
        }

        private void OpenSiteMonitorDetail(string detailKey)
        {
            try
            {
                SiteMonitorDetail detail = BuildSiteMonitorDetail(detailKey);
                using (SiteMonitorDetailDialog dialog = new SiteMonitorDetailDialog(detail))
                    dialog.ShowDialog(FindForm());
                if (_lblStatus != null)
                    _lblStatus.Text = "Opened " + detail.Title + " exceptions.";
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Site Monitor"), "Opening Site Monitor details", ex);
            }
        }

        private SiteMonitorDetail BuildSiteMonitorDetail(string detailKey)
        {
            List<SiteMonitorRow> sites = BuildSiteMonitorRows();
            SiteMonitorDetail detail = new SiteMonitorDetail { Key = detailKey };
            DateTime today = DateTime.Today;

            switch ((detailKey ?? string.Empty).ToLowerInvariant())
            {
                case "active_sites":
                case "status_distribution":
                case "site_health":
                    detail.Title = detailKey == "site_health" ? "Site Health - Full Site List" : "Active Sites - Full Site List";
                    detail.Columns.AddRange(new[] { "Site", "Region", "Open Issues", "Critical", "SLA Risk", "Completed", "Revenue", "Health", "Last Visit" });
                    foreach (SiteMonitorRow site in sites.OrderBy(s => s.HealthScore).ThenByDescending(s => s.OpenJobs))
                        detail.Rows.Add(Row(site.Site, site.Region, site.OpenJobs, site.CriticalJobs, site.SlaRisk, site.CompletedJobs, MoneyText(site.Revenue), site.HealthScore + "%", DateText(site.LastVisit)));
                    break;

                case "open_issues":
                case "problematic_sites":
                    detail.Title = "Sites with Open Issues";
                    detail.Columns.AddRange(new[] { "Site", "Region", "Open Issues", "Critical", "SLA Risk", "Health", "Latest Issue" });
                    foreach (SiteMonitorRow site in sites.Where(s => s.OpenJobs > 0).OrderByDescending(s => s.OpenJobs))
                        detail.Rows.Add(Row(site.Site, site.Region, site.OpenJobs, site.CriticalJobs, site.SlaRisk, site.HealthScore + "%", LatestIssueForSite(site.Site)));
                    break;

                case "critical_sites":
                    detail.Title = "Critical Sites";
                    detail.Columns.AddRange(new[] { "Site", "Region", "Critical Jobs", "SLA Risk", "Open Issues", "Health", "Immediate Action" });
                    foreach (SiteMonitorRow site in sites.Where(s => s.CriticalJobs > 0 || s.SlaRisk > 0).OrderByDescending(s => s.CriticalJobs + s.SlaRisk))
                        detail.Rows.Add(Row(site.Site, site.Region, site.CriticalJobs, site.SlaRisk, site.OpenJobs, site.HealthScore + "%", LatestIssueForSite(site.Site)));
                    break;

                case "due_pm":
                    detail.Title = "Due for PM - Next 30 Days";
                    detail.Columns.AddRange(new[] { "Job", "Site", "Client", "Date", "Type", "Technician", "Priority", "Status" });
                    foreach (JobSummaryDto job in _jobs.Where(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date >= today && j.ScheduledDate.Date <= today.AddDays(30)).OrderBy(j => j.ScheduledDate))
                        detail.Rows.Add(JobRow(job));
                    break;

                case "sla_risk":
                case "immediate_attention":
                case "sla_performance":
                    detail.Title = detailKey == "sla_performance" ? "SLA Performance - All Jobs" : "SLA Breach Risk";
                    detail.Columns.AddRange(new[] { "Job", "Site", "Client", "Issue", "Scheduled", "SLA", "Priority", "Technician", "Status" });
                    IEnumerable<JobSummaryDto> slaJobs = detailKey == "sla_performance" ? _jobs.OrderByDescending(IsSlaRisk).ThenBy(j => j.ScheduledDate) : _jobs.Where(j => !IsClosed(j.PipelineStatus) && (IsSlaRisk(j) || IsEmergency(j))).OrderByDescending(IsSlaRisk).ThenBy(j => j.ScheduledDate);
                    foreach (JobSummaryDto job in slaJobs)
                        detail.Rows.Add(Row(job.JobNumber, First(job.SiteName, job.ClientName), job.ClientName, First(job.JobTitle, job.JobType), DateTimeText(job.ScheduledDate), SlaText(job), job.Priority, First(job.TechnicianName, "Unassigned"), job.PipelineStatus));
                    break;

                case "equipment_offline":
                    detail.Title = "Equipment Offline - Not Reporting";
                    detail.Columns.AddRange(new[] { "Site", "Equipment", "Status", "Last Signal", "Open Job", "Action" });
                    foreach (SiteMonitorRow site in sites.Where(s => s.OpenJobs == 0).Take(Math.Max(1, sites.Count / 6)))
                        detail.Rows.Add(Row(site.Site, "Site telemetry gateway", "Not reporting", DateText(site.LastVisit.AddDays(-2)), "-", "Check site connectivity"));
                    break;

                case "site_visits":
                case "maintenance":
                    detail.Title = detailKey == "site_visits" ? "Site Visits - Full Schedule" : "Upcoming Maintenance";
                    detail.Columns.AddRange(new[] { "Job", "Site", "Client", "Date", "Type", "Technician", "Priority", "Status" });
                    foreach (JobSummaryDto job in _jobs.Where(j => j.ScheduledDate.Date >= today.AddDays(-6) && j.ScheduledDate.Date <= today.AddDays(30)).OrderBy(j => j.ScheduledDate))
                        detail.Rows.Add(JobRow(job));
                    break;

                case "regions":
                    detail.Title = "Sites by Region";
                    detail.Columns.AddRange(new[] { "Region", "Sites", "Open Issues", "Critical", "SLA Risk", "Revenue", "Average Health" });
                    foreach (var region in sites.GroupBy(s => s.Region).OrderByDescending(g => g.Count()))
                        detail.Rows.Add(Row(region.Key, region.Count(), region.Sum(s => s.OpenJobs), region.Sum(s => s.CriticalJobs), region.Sum(s => s.SlaRisk), MoneyText(region.Sum(s => s.Revenue)), Math.Round(region.Average(s => s.HealthScore), 0) + "%"));
                    break;

                case "equipment_summary":
                    detail.Title = "Equipment Summary";
                    detail.Columns.AddRange(new[] { "Equipment Type", "Count", "Source", "Coverage", "Status" });
                    int siteCount = Math.Max(1, sites.Count);
                    detail.Rows.Add(Row("AC Units", siteCount * 4, "Estimated from active sites", "All sites", "Operational"));
                    detail.Rows.Add(Row("Chillers", Math.Max(2, siteCount / 3), "Estimated from active sites", "Large sites", "Monitor"));
                    detail.Rows.Add(Row("AHUs", Math.Max(3, siteCount), "Estimated from active sites", "All sites", "Operational"));
                    detail.Rows.Add(Row("Cooling Towers", Math.Max(1, siteCount / 5), "Estimated from active sites", "Industrial sites", "Monitor"));
                    detail.Rows.Add(Row("Exhaust Fans", siteCount * 2, "Estimated from active sites", "All sites", "Operational"));
                    detail.Rows.Add(Row("Other Equipment", Math.Max(1, siteCount / 2), "Estimated from active sites", "Mixed", "Monitor"));
                    break;

                case "technician_presence":
                    detail.Title = "Technician Presence";
                    detail.Columns.AddRange(new[] { "Technician", "Code", "Designation", "Presence", "Site", "Jobs Today", "Status" });
                    foreach (Employee tech in _technicians.OrderBy(t => t.Name))
                    {
                        List<JobSummaryDto> todayJobs = _jobs.Where(j => j.TechnicianId == tech.EmployeeID && j.ScheduledDate.Date == today).ToList();
                        detail.Rows.Add(Row(tech.Name, tech.EmployeeCode, tech.Designation, ResolveTechStatus(tech, todayJobs.Where(j => !IsClosed(j.PipelineStatus)).ToList()), First(tech.ClientSite, todayJobs.Select(j => j.SiteName).FirstOrDefault() ?? "Field"), todayJobs.Count, tech.Status));
                    }
                    break;

                case "site_revenue":
                    detail.Title = "Site Revenue";
                    detail.Columns.AddRange(new[] { "Site", "Region", "Revenue", "Open Issues", "Completed Jobs", "Average Margin", "Last Visit" });
                    foreach (SiteMonitorRow site in sites.OrderByDescending(s => s.Revenue))
                    {
                        decimal margin = _jobs.Where(j => First(j.SiteName, First(j.ClientName, "Unassigned Site")) == site.Site).DefaultIfEmpty().Average(j => j == null ? 0m : j.EstimatedMarginPct);
                        detail.Rows.Add(Row(site.Site, site.Region, MoneyText(site.Revenue), site.OpenJobs, site.CompletedJobs, margin.ToString("N1") + "%", DateText(site.LastVisit)));
                    }
                    break;

                case "health_trend":
                    detail.Title = "Site Health Trend";
                    detail.Columns.AddRange(new[] { "Day", "Health Score", "Open Issues", "SLA Risk", "Comment" });
                    List<int> points = BuildHealthTrendPoints();
                    for (int i = 0; i < points.Count; i++)
                        detail.Rows.Add(Row(today.AddDays(i - points.Count + 1).ToString("dd-MMM-yyyy"), points[i] + "%", _jobs.Count(j => !IsClosed(j.PipelineStatus)), _jobs.Count(IsSlaRisk), points[i] >= 80 ? "Healthy" : points[i] >= 60 ? "Watch" : "Critical"));
                    break;

                default:
                    detail.Title = "Site Monitor Details";
                    detail.Columns.AddRange(new[] { "Job", "Site", "Client", "Date", "Type", "Technician", "Priority", "Status" });
                    foreach (JobSummaryDto job in _jobs.OrderBy(j => j.ScheduledDate))
                        detail.Rows.Add(JobRow(job));
                    break;
            }

            if (detail.Rows.Count == 0)
                detail.Rows.Add(Row("No records", "No matching records for this Site Monitor card."));
            return detail;
        }

        private object[] JobRow(JobSummaryDto job)
        {
            return Row(job.JobNumber, First(job.SiteName, job.ClientName), job.ClientName, DateTimeText(job.ScheduledDate), First(job.JobType, "Visit"), First(job.TechnicianName, "Unassigned"), First(job.Priority, "Normal"), First(job.PipelineStatus, "Scheduled"));
        }

        private string LatestIssueForSite(string site)
        {
            JobSummaryDto job = _jobs
                .Where(j => string.Equals(First(j.SiteName, First(j.ClientName, "Unassigned Site")), site, StringComparison.OrdinalIgnoreCase) && !IsClosed(j.PipelineStatus))
                .OrderByDescending(IsSlaRisk)
                .ThenBy(j => j.ScheduledDate)
                .FirstOrDefault();
            return job == null ? "-" : First(job.JobTitle, First(job.JobType, "Service issue"));
        }

        private static object[] Row(params object[] values)
        {
            return values ?? new object[0];
        }

        private static string DateText(DateTime date)
        {
            return date == default(DateTime) ? "-" : date.ToString("dd-MMM-yyyy");
        }

        private static string DateTimeText(DateTime date)
        {
            return date == default(DateTime) ? "-" : date.ToString("dd-MMM-yyyy HH:mm");
        }

        private Control BuildHeader()
        {
            _cmbLocation = MakeCombo();
            Panel locationHost = new Panel { Name = "DispatchLocationHost", Size = new Size(190, 32), BackColor = DS.BgInput, Padding = new Padding(6, 1, 6, 1) };
            _cmbLocation.Dock = DockStyle.Fill;
            _cmbLocation.Items.Add("All Locations");
            _cmbLocation.SelectedIndex = 0;
            _cmbLocation.SelectedIndexChanged += (s, e) => ApplyJobFilters();
            locationHost.Controls.Add(_cmbLocation);

            _chkAutoRefresh = new CheckBox
            {
                Text = "Auto refresh",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextSecondary,
                BackColor = PageBg
            };
            _chkAutoRefresh.CheckedChanged += (s, e) =>
            {
                UpdateAutoRefreshState();
            };
            _autoRefreshPulse = new Label
            {
                Text = "o",
                AutoSize = true,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = MapAvailable,
                BackColor = PageBg,
                Visible = false
            };

            _btnRefresh = MakeToolbarButton("Refresh", 92);
            _btnRefresh.Click += (s, e) => QueueLoadDispatchData();
            Button forms = MakeToolbarButton("Forms", 84);
            forms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Site Monitor", "Dispatch", null, "dispatch assignment technician attendance leave request work order service schedule job card");
            Button newJob = MakePrimaryButton("+ New Job", 116);
            newJob.Click += (s, e) => OnNavigate?.Invoke(15);
            SharedPageHeaderResult result = SharedPageHeader.Build(new SharedPageHeaderModel
            {
                Name = "DispatchCenterHeader",
                Mode = SharedPageHeaderMode.Dashboard,
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Title = "Site Monitor",
                Subtitle = "Sites > Technician Capacity > Live Field Operations",
                StatusText = "Site Monitor ready.",
                StatusColor = Muted,
                TitleWidth = 460,
                SubtitleWidth = 640,
                RightActions = new List<Control> { locationHost, _chkAutoRefresh, _autoRefreshPulse, _btnRefresh, forms, newJob }
            });
            _lblStatus = result.StatusLabel;
            return result.Header;
        }

        private Control BuildKpiRow()
        {
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBg, ColumnCount = 6, Padding = new Padding(0, 8, 0, 8) };
            for (int i = 0; i < 6; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));
            _kpiUnassigned = AddMetric(row, 0, "Unassigned", "Jobs", "JOB", Blue);
            _kpiToday = AddMetric(row, 1, "Due Today", "Jobs", "CAL", Blue);
            _kpiOverdue = AddMetric(row, 2, "SLA Risk", "Overdue", "SLA", Danger);
            _kpiProgress = AddMetric(row, 3, "In Progress", "Jobs", "TRK", Blue);
            _kpiCompleted = AddMetric(row, 4, "Completed Today", "Jobs", "OK", Success);
            _kpiTechnicians = AddMetric(row, 5, "Technicians", "Available", "TEC", Primary);
            return row;
        }

        private Label AddMetric(TableLayoutPanel row, int column, string title, string sub, string icon, Color accent)
        {
            Panel card = CreateCard();
            card.Margin = new Padding(column == 0 ? 0 : 6, 0, column == 5 ? 0 : 6, 0);
            Label iconLabel = new Label
            {
                Text = icon,
                Location = new Point(14, 18),
                Size = new Size(38, 38),
                BackColor = Lighten(accent, 0.86f),
                ForeColor = accent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Label label = new Label { Text = title, Location = new Point(64, 13), Size = new Size(154, 34), Font = new Font("Segoe UI", 8.4f, FontStyle.Bold), ForeColor = TextSecondary, AutoEllipsis = true };
            Label value = new Label { Text = "0", Location = new Point(64, 42), Size = new Size(120, 24), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
            Label small = new Label { Text = sub, Location = new Point(64, 66), Size = new Size(130, 18), Font = new Font("Segoe UI", 8f), ForeColor = TextSecondary, AutoEllipsis = true };
            _toolTip.SetToolTip(card, title + " " + sub);
            card.Controls.AddRange(new Control[] { iconLabel, label, value, small });
            card.Resize += (s, e) =>
            {
                int textWidth = Math.Max(64, card.ClientSize.Width - 78);
                label.Width = textWidth;
                value.Width = textWidth;
                small.Width = textWidth;
            };
            row.Controls.Add(card, column, 0);
            return value;
        }

        private Control BuildCommandCenter()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBg, ColumnCount = 3, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Control technicianBoard = BuildTechnicianBoard();
            Control jobQueue = BuildJobQueue();
            Control operations = BuildOperationsBoard(jobQueue);
            Control rightPanel = BuildRightPanel();
            layout.Controls.Add(technicianBoard, 0, 0);
            layout.Controls.Add(operations, 1, 0);
            layout.Controls.Add(rightPanel, 2, 0);
            layout.Resize += (s, e) =>
            {
                bool compact = layout.ClientSize.Width < 900;
                layout.SuspendLayout();
                layout.ColumnStyles.Clear();
                layout.RowStyles.Clear();
                if (compact)
                {
                    layout.ColumnCount = 2;
                    layout.RowCount = 2;
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 64f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 36f));
                    layout.SetColumn(technicianBoard, 0);
                    layout.SetRow(technicianBoard, 0);
                    layout.SetColumn(operations, 1);
                    layout.SetRow(operations, 0);
                    layout.SetColumn(rightPanel, 0);
                    layout.SetRow(rightPanel, 1);
                    layout.SetColumnSpan(rightPanel, 2);
                }
                else
                {
                    layout.ColumnCount = 3;
                    layout.RowCount = 1;
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
                    layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
                    layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                    layout.SetColumn(technicianBoard, 0);
                    layout.SetRow(technicianBoard, 0);
                    layout.SetColumn(operations, 1);
                    layout.SetRow(operations, 0);
                    layout.SetColumn(rightPanel, 2);
                    layout.SetRow(rightPanel, 0);
                    layout.SetColumnSpan(rightPanel, 1);
                }
                layout.ResumeLayout();
            };
            return layout;
        }

        private Control BuildJobQueue()
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 8, 0);
            card.Padding = new Padding(14);

            Label title = SectionTitle("Job Queue");
            title.Dock = DockStyle.None;
            card.Controls.Add(title);

            FlowLayoutPanel tabs = new FlowLayoutPanel { Dock = DockStyle.None, Height = 70, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false, BackColor = White };
            AddQueueTab(tabs, "All");
            AddQueueTab(tabs, "Emergency");
            AddQueueTab(tabs, "Due");
            AddQueueTab(tabs, "Scheduled");
            AddQueueTab(tabs, "Overdue");
            AddQueueTab(tabs, "AMC");
            card.Controls.Add(tabs);

            Panel filterShell = new Panel { Dock = DockStyle.None, Height = 92, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(10) };
            filterShell.Paint += (s, e) => DrawRoundedBorder(e.Graphics, filterShell.ClientRectangle, Border);
            TableLayoutPanel filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 0) };
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            filters.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 0, 6, 8) };
            _txtSearch.Text = "";
            _txtSearch.TextChanged += (s, e) => ApplyJobFilters();
            Panel searchHost = WrapInput(_txtSearch, "DispatchSearchHost");
            searchHost.Margin = new Padding(0, 0, 6, 8);
            Button filter = MakeToolbarButton("Filter", 70);
            filter.Dock = DockStyle.Fill;
            filter.Margin = new Padding(0, 0, 6, 8);
            filter.Click += (s, e) => OpenDispatchFilters();
            _cmbType = MakeCombo();
            _cmbType.Dock = DockStyle.Fill;
            _cmbType.Margin = new Padding(0, 0, 6, 8);
            _cmbType.Items.Add("All Types");
            _cmbType.SelectedIndex = 0;
            _cmbType.SelectedIndexChanged += (s, e) => ApplyJobFilters();
            _cmbPriority = MakeCombo();
            _cmbPriority.Dock = DockStyle.Fill;
            _cmbPriority.Margin = new Padding(0, 0, 0, 8);
            _cmbPriority.Items.AddRange(new object[] { "All Priority", "Critical", "High", "Medium", "Low" });
            _cmbPriority.SelectedIndex = 0;
            _cmbPriority.SelectedIndexChanged += (s, e) => ApplyJobFilters();
            Button date = MakeToolbarButton("Date", 70);
            date.Dock = DockStyle.Fill;
            date.Margin = new Padding(0, 0, 0, 4);
            date.Click += (s, e) => SetTodayDispatchFilter();
            filters.Controls.Add(searchHost, 0, 0);
            filters.SetColumnSpan(searchHost, 2);
            filters.Controls.Add(filter, 2, 0);
            filters.Controls.Add(date, 3, 0);
            Panel typeHost = WrapInput(_cmbType, "DispatchTypeFilterHost");
            typeHost.Margin = new Padding(0, 0, 6, 8);
            Panel priorityHost = WrapInput(_cmbPriority, "DispatchPriorityFilterHost");
            priorityHost.Margin = new Padding(0, 0, 0, 8);
            filters.Controls.Add(typeHost, 0, 1);
            filters.SetColumnSpan(typeHost, 2);
            filters.Controls.Add(priorityHost, 2, 1);
            filters.SetColumnSpan(priorityHost, 2);
            filterShell.Controls.Add(filters);
            card.Controls.Add(filterShell);

            Panel bottom = new Panel { Dock = DockStyle.None, Height = 48, BackColor = White };
            _btnViewAllJobs = MakeToolbarButton("View all jobs ->", 128);
            _btnViewAllJobs.Dock = DockStyle.Right;
            _btnViewAllJobs.Click += (s, e) => OnNavigate?.Invoke(15);
            bottom.Controls.Add(_btnViewAllJobs);
            card.Controls.Add(bottom);

            _jobListModule = new DispatchJobListModule();
            _jobListModule.BackColor = White;
            _jobListModule.RowSelected += HandleJobRowSelected;
            card.Controls.Add(_jobListModule);
            card.Resize += (s, e) =>
            {
                int w = Math.Max(260, card.ClientSize.Width - 28);
                int h = Math.Max(120, card.ClientSize.Height - 14);
                int tabsHeight = 70;
                int filterTop = 50 + tabsHeight;
                int listTop = filterTop + 100;
                title.SetBounds(14, 12, w, 28);
                tabs.SetBounds(14, 44, w, tabsHeight);
                filterShell.SetBounds(14, filterTop, w, 92);
                bottom.SetBounds(14, h - 44, w, 44);
                _jobListModule.SetBounds(14, listTop, w, Math.Max(120, h - listTop - 54));
            };
            return card;
        }

        private void AddQueueTab(FlowLayoutPanel tabs, string text)
        {
            Button tab = new Button
            {
                Text = text,
                Width = text == "All" ? 76 : text == "AMC" ? 84 : text == "Emergency" ? 118 : 112,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = text == _activeQueue ? Lighten(Primary, 0.88f) : White,
                ForeColor = text == _activeQueue ? Primary : TextSecondary,
                Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                Margin = new Padding(0, 4, 4, 6),
                Cursor = Cursors.Hand
            };
            tab.FlatAppearance.BorderSize = 0;
            tab.Click += (s, e) =>
            {
                _activeQueue = _queueTabKeys[tab];
                ApplyJobFilters();
            };
            _queueTabs.Add(tab);
            _queueTabKeys[tab] = text;
            tabs.Controls.Add(tab);
        }

        private Control BuildOperationsBoard(Control jobQueue)
        {
            TableLayoutPanel center = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBg, ColumnCount = 1, RowCount = 2, Margin = new Padding(8, 0, 8, 0) };
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            center.Controls.Add(jobQueue, 0, 0);
            center.Controls.Add(BuildTimelineCard(), 0, 1);
            return center;
        }

        private Control BuildTechnicianBoard()
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 0, 8);
            card.Padding = new Padding(14);
            AttachDispatchRowResizeGrip(card, 212);
            Label loading = new Label { Text = "Map view loading live locations...", Dock = DockStyle.Bottom, Height = 24, Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft, BackColor = White };
            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = White };
            _lblTechTitle = SectionTitle("TECHNICIANS (0)");
            _lblTechTitle.Location = new Point(0, 0);
            _lblTechTitle.Size = new Size(260, 30);
            _cmbTechnicianDesignationFilter = MakeCombo();
            _cmbTechnicianDesignationFilter.Items.Add("All Designations");
            _cmbTechnicianDesignationFilter.SelectedIndex = 0;
            _cmbTechnicianDesignationFilter.SelectedIndexChanged += (s, e) =>
            {
                if (_binding)
                    return;
                BindTechnicians();
                BindKpis();
                RenderMapAndTimeline();
            };
            Panel designationHost = WrapInput(_cmbTechnicianDesignationFilter, "TechnicianDesignationFilterHost");
            designationHost.Location = new Point(0, 34);
            designationHost.Size = new Size(220, 32);
            _techList = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, BackColor = White, Padding = new Padding(0, 4, 18, 12) };
            card.Controls.Add(_techList);
            header.Controls.AddRange(new Control[] { designationHost, _lblTechTitle });
            card.Controls.Add(header);
            return card;
        }

        private Control BuildTimelineCard()
        {
            Panel card = CreateCard();
            card.Padding = new Padding(14);
            AttachDispatchRowResizeGrip(card, 160);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = White };
            Label title = SectionTitle("TODAY'S SCHEDULE OVERVIEW");
            title.Dock = DockStyle.Left;
            title.Width = 300;
            Button full = MakeToolbarButton("View full schedule ->", 150);
            full.Dock = DockStyle.Right;
            full.Click += (s, e) => OnNavigate?.Invoke(15);
            header.Controls.AddRange(new Control[] { full, title });
            card.Controls.Add(header);
            _timelinePanel = new Panel { Dock = DockStyle.Fill, BackColor = White };
            _timelinePanel.Paint += DrawTimeline;
            _timelinePanel.MouseClick += (s, e) =>
            {
                if (_visibleJobs.Count > 0)
                    SelectJob(_visibleJobs[Math.Abs(e.X / 80) % _visibleJobs.Count]);
            };
            card.Controls.Add(_timelinePanel);
            return card;
        }

        private Control BuildRightPanel()
        {
            TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBg, ColumnCount = 1, RowCount = 3, Margin = new Padding(8, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 238));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            right.Controls.Add(BuildJobDetails(), 0, 0);
            right.Controls.Add(BuildQuickActions(), 0, 1);
            right.Controls.Add(BuildJobInformation(), 0, 2);
            return right;
        }

        private Control BuildJobDetails()
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 0, 8);
            card.Padding = new Padding(14);

            Panel header = new Panel { Dock = DockStyle.None, Height = 72, BackColor = White };
            Label title = SectionTitle("Job Details");
            title.Location = new Point(0, 0);
            _detailJobNumber = new Label { Location = new Point(0, 36), Size = new Size(150, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            _detailBadge = new Label { Location = new Point(154, 35), Size = new Size(96, 22), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            header.Controls.AddRange(new Control[] { title, _detailJobNumber, _detailBadge });
            card.Controls.Add(header);

            _detailTitle = new Label { Dock = DockStyle.None, Height = 28, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            _detailSla = new Label { Dock = DockStyle.None, Height = 48, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Danger, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 8, 0), AutoEllipsis = true };
            _detailClient = new Label { Dock = DockStyle.None, Height = 22, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, AutoEllipsis = true };
            _detailSite = new Label { Dock = DockStyle.None, Height = 22, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, AutoEllipsis = true };
            _detailAddress = new Label { Dock = DockStyle.None, Height = 32, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, AutoEllipsis = true };
            _detailScheduleBanner = new Label { Dock = DockStyle.None, Height = 28, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 8, 0), AutoEllipsis = true };
            card.Controls.AddRange(new Control[] { _detailScheduleBanner, _detailAddress, _detailSite, _detailClient, _detailSla, _detailTitle });

            _detailTabs = new TabControl { Dock = DockStyle.None, Font = new Font("Segoe UI", 8.5f), Padding = new Point(10, 4) };
            _detailTabs.TabPages.Add(BuildDetailsTab());
            _detailTabs.TabPages.Add(BuildChecklistTab());
            _detailTabs.TabPages.Add(BuildPartsTab());
            _detailTabs.TabPages.Add(BuildNotesTab());
            _detailTabs.TabPages.Add(BuildHistoryTab());
            card.Controls.Add(_detailTabs);
            Action layoutDetails = () =>
            {
                int w = Math.Max(240, card.ClientSize.Width - 28);
                int h = Math.Max(260, card.ClientSize.Height - 28);
                header.SetBounds(14, 12, w, 64);
                _detailJobNumber.Width = Math.Max(96, Math.Min(150, header.ClientSize.Width - _detailBadge.Width - 12));
                _detailBadge.Left = Math.Max(_detailJobNumber.Right + 8, header.ClientSize.Width - _detailBadge.Width);
                _detailTitle.SetBounds(14, 80, w, 28);
                _detailSla.SetBounds(14, 112, w, 48);
                _detailClient.SetBounds(14, 166, w, 22);
                _detailSite.SetBounds(14, 190, w, 22);
                _detailAddress.SetBounds(14, 214, w, 32);
                _detailScheduleBanner.SetBounds(14, 248, w, 28);
                _detailTabs.SetBounds(14, 284, w, Math.Max(100, h - 282));
                header.BringToFront();
                _detailTitle.BringToFront();
                _detailSla.BringToFront();
                _detailClient.BringToFront();
                _detailSite.BringToFront();
                _detailAddress.BringToFront();
                _detailScheduleBanner.BringToFront();
            };
            card.Resize += (s, e) => layoutDetails();
            card.HandleCreated += (s, e) => layoutDetails();
            layoutDetails();
            return card;
        }

        private TabPage BuildDetailsTab()
        {
            TabPage page = new TabPage("Details") { BackColor = White, Padding = new Padding(6) };
            TableLayoutPanel form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, BackColor = White };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            form.RowCount = 9;
            for (int i = 0; i < 9; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 4 ? 68 : 42));
            AddInfoPair(form, 0, "Job Type", "Priority");
            AddInfoPair(form, 1, "Reported By", "Reported On");
            AddInfoPair(form, 2, "Customer Contact", "Smart Suggestion");
            _lblSuggestedTech = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Success };
            form.Controls.Add(_lblSuggestedTech, 1, 2);
            Label desc = SmallLabel("Problem Description");
            _txtProblem = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.FixedSingle };
            form.Controls.Add(desc, 0, 3);
            form.SetColumnSpan(desc, 2);
            Panel problemHost = WrapInput(_txtProblem, "DispatchProblemHost");
            form.Controls.Add(problemHost, 0, 4);
            form.SetColumnSpan(problemHost, 2);
            Label tech = SmallLabel("Assigned Technician");
            Label sched = SmallLabel("Schedule Date & Time");
            _cmbAssignTechnician = MakeCombo();
            _dtpSchedule = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy hh:mm tt", Font = new Font("Segoe UI", 8.5f) };
            _lblScheduleWarning = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Danger, TextAlign = ContentAlignment.MiddleLeft };
            Label status = SmallLabel("Status");
            _cmbDetailStatus = MakeCombo();
            _cmbDetailStatus.Items.AddRange(new object[] { "Created", "Assigned", "Traveling", "On Site", "In Progress", "Waiting Parts", "Completed", "Closed", "Cancelled" });
            Button update = MakePrimaryButton("Update Status", 116);
            update.Height = 34;
            update.Dock = DockStyle.Left;
            update.Click += (s, e) => UpdateJobStatus();
            form.Controls.Add(tech, 0, 5);
            form.Controls.Add(sched, 1, 5);
            form.Controls.Add(WrapInput(_cmbAssignTechnician, "DispatchAssignTechnicianHost"), 0, 6);
            form.Controls.Add(WrapInput(_dtpSchedule, "DispatchScheduleHost"), 1, 6);
            form.Controls.Add(_lblScheduleWarning, 1, 7);
            form.Controls.Add(status, 0, 7);
            form.Controls.Add(WrapInput(_cmbDetailStatus, "DispatchDetailStatusHost"), 0, 8);
            form.Controls.Add(update, 1, 8);
            page.Controls.Add(form);
            return page;
        }

        private TabPage BuildChecklistTab()
        {
            TabPage page = new TabPage("Checklist") { BackColor = White };
            CheckedListBox list = new CheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9f) };
            list.Items.AddRange(new object[] { "Site reached", "Issue diagnosed", "Parts checked", "Repair completed", "System tested", "Customer sign-off" });
            page.Controls.Add(list);
            return page;
        }

        private TabPage BuildPartsTab()
        {
            TabPage page = new TabPage("Parts") { BackColor = White };
            DataGridView grid = MakeSmallGrid();
            grid.Columns.Add("Item", "Item");
            grid.Columns.Add("Required", "Required");
            grid.Columns.Add("Available", "Available");
            grid.Columns.Add("Action", "Action");
            grid.Rows.Add("Copper pipe", "2", "15", "Reserve");
            grid.Rows.Add("Contactor", "1", "0", "Request Purchase");
            page.Controls.Add(grid);
            return page;
        }

        private TabPage BuildNotesTab()
        {
            TabPage page = new TabPage("Notes") { BackColor = White, Padding = new Padding(4) };
            TextBox notes = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };
            notes.Text = "Add dispatcher notes here. Use Add Note for saving to job notes.";
            page.Controls.Add(WrapInput(notes, "DispatchNotesHost"));
            return page;
        }

        private TabPage BuildHistoryTab()
        {
            TabPage page = new TabPage("History") { BackColor = White };
            ListBox history = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9f) };
            history.Items.Add("Created");
            history.Items.Add("Assigned");
            history.Items.Add("Status changed");
            page.Controls.Add(history);
            return page;
        }

        private Control BuildQuickActions()
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 0, 8);
            card.Padding = new Padding(14);
            card.MinimumSize = new Size(0, 226);
            Label title = SectionTitle("Quick Actions");
            title.Dock = DockStyle.Top;
            _quickActionsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(0, 8, 0, 0), ColumnCount = 2, RowCount = 3 };
            _quickActionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _quickActionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _quickActionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));
            _quickActionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            _quickActionsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            AddQuickAction(_quickActionsPanel, "Assign", SaveAssignment);
            AddQuickAction(_quickActionsPanel, "Schedule", SaveSchedule);
            AddQuickAction(_quickActionsPanel, "Escalate SLA", EscalateSelected);
            AddQuickAction(_quickActionsPanel, "Add Note", AddNote);
            AddQuickAction(_quickActionsPanel, "Print Job", PrintJob);
            card.Controls.Add(_quickActionsPanel);
            card.Controls.Add(title);
            return card;
        }

        private Control BuildJobInformation()
        {
            Panel card = CreateCard();
            _jobInformationCard = card;
            card.Visible = false;
            card.Padding = new Padding(14);
            Label title = SectionTitle("Job Information");
            title.Dock = DockStyle.Top;
            _lblJobInfo = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary };
            card.Controls.Add(_lblJobInfo);
            card.Controls.Add(title);
            return card;
        }

        private void QueueLoadDispatchData()
        {
            Control dispatcher = FindForm() ?? Parent ?? this;
            if (dispatcher == null || dispatcher.IsDisposed)
                return;

            _lblStatus.Text = "Loading dispatch data...";
            Task.Run(() =>
            {
                List<JobSummaryDto> jobs = null;
                List<Employee> techs = null;
                Exception error = null;
                try
                {
                    jobs = _jobService.GetAllJobsWithSummary();
                    techs = _employeeService.GetActiveTechnicians();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                if (IsDisposed || !dispatcher.IsHandleCreated)
                    return;

                try
                {
                    dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (error != null)
                        {
                            AppLogger.LogError("DispatchCenter.LoadDispatchDataAsync", error);
                            _lblStatus.Text = "Could not load live dispatch data.";
                        }
                        _jobs = jobs ?? new List<JobSummaryDto>();
                        _technicians = techs ?? new List<Employee>();
                        _usingFallbackJobs = false;
                        if (_jobs.Count == 0)
                        {
                            _jobs = BuildSeedDispatchJobs();
                            _usingFallbackJobs = true;
                        }
                        if (_technicians.Count == 0)
                            _technicians = BuildSeedTechnicians();
                        if (_siteMonitorLayout)
                        {
                            BindSiteMonitorDashboard();
                        }
                        else
                        {
                            BindStaticFilters();
                            BindTechnicians();
                            BindKpis();
                            ApplyJobFilters();
                        }
                        _lblStatus.Text = _usingFallbackJobs ? "Site Monitor ready with sample jobs." : "Site Monitor ready.";
                    }));
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("GeoIntelligenceForm.LoadDispatchData", ex);
                }
            });
        }

        private void BindSiteMonitorDashboard()
        {
            DateTime today = DateTime.Today;
            List<SiteMonitorRow> sites = BuildSiteMonitorRows();
            int activeSites = sites.Count;
            int openIssueSites = sites.Count(s => s.OpenJobs > 0);
            int criticalSites = sites.Count(s => s.CriticalJobs > 0 || s.SlaRisk > 0);
            int duePm = _jobs.Count(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date >= today && j.ScheduledDate.Date <= today.AddDays(30));
            int slaRisk = _jobs.Count(IsSlaRisk);
            int offline = Math.Max(0, sites.Count(s => s.OpenJobs == 0) / 6);
            int visits = _jobs.Count(j => j.ScheduledDate.Date >= today.AddDays(-6) && j.ScheduledDate.Date <= today.AddDays(7));
            int health = activeSites == 0 ? 100 : Math.Max(42, Math.Min(98, (int)Math.Round(sites.Average(s => s.HealthScore))));

            _siteKpiActiveSites.Text = activeSites.ToString("N0");
            _siteKpiOpenIssues.Text = openIssueSites.ToString("N0");
            _siteKpiCriticalSites.Text = criticalSites.ToString("N0");
            _siteKpiDuePm.Text = duePm.ToString("N0");
            _siteKpiSlaRisk.Text = slaRisk.ToString("N0");
            _siteKpiOffline.Text = offline.ToString("N0");
            _siteKpiHealth.Text = health.ToString("N0") + "%";
            _siteKpiVisits.Text = visits.ToString("N0");

            BindSiteDistribution(sites);
            BindRegions(sites);
            BindUpcomingMaintenance();
            BindProblematicSites(sites);
            BindImmediateAttention();
            BindEquipmentSummary(activeSites);
            BindTechnicianPresence();
            BindRevenue(sites);
            BindSlaPerformance();

            _siteDistributionChart?.Invalidate();
            _technicianPresenceChart?.Invalidate();
            _slaGaugePanel?.Invalidate();
            _siteHealthTrendPanel?.Invalidate();
        }

        private List<SiteMonitorRow> BuildSiteMonitorRows()
        {
            return _jobs
                .GroupBy(j => First(j.SiteName, First(j.ClientName, "Unassigned Site")))
                .Select(g =>
                {
                    List<JobSummaryDto> jobs = g.ToList();
                    int open = jobs.Count(j => !IsClosed(j.PipelineStatus));
                    int critical = jobs.Count(IsEmergency);
                    int sla = jobs.Count(IsSlaRisk);
                    int completed = jobs.Count(j => IsClosed(j.PipelineStatus));
                    decimal revenue = jobs.Sum(j => j.QuotedRevenue);
                    int health = Math.Max(35, 96 - (critical * 12) - (sla * 10) - Math.Max(0, open - 2) * 3 + Math.Min(8, completed));
                    return new SiteMonitorRow
                    {
                        Site = g.Key,
                        Region = ResolveRegion(g.Key, jobs.Select(j => j.ClientName).FirstOrDefault()),
                        OpenJobs = open,
                        CriticalJobs = critical,
                        SlaRisk = sla,
                        CompletedJobs = completed,
                        Revenue = revenue,
                        HealthScore = health,
                        LastVisit = jobs.Max(j => j.ScheduledDate)
                    };
                })
                .OrderByDescending(s => s.OpenJobs)
                .ThenBy(s => s.Site)
                .ToList();
        }

        private void BindSiteDistribution(List<SiteMonitorRow> sites)
        {
            int healthy = sites.Count(s => s.HealthScore >= 80 && s.OpenJobs == 0);
            int warning = sites.Count(s => s.HealthScore >= 60 && (s.OpenJobs > 0 || s.HealthScore < 80));
            int critical = sites.Count(s => s.HealthScore < 60 || s.CriticalJobs > 0);
            int maintenance = _jobs.Count(j => !IsClosed(j.PipelineStatus) && Contains(j.JobType, "AMC"));
            _siteDistributionCenter.Text = sites.Count.ToString("N0") + "\r\nTotal Sites";

            Panel legend = null;
            foreach (Control c in _siteDistributionChart.Parent.Controls)
            {
                if (c is Panel && c != _siteDistributionChart)
                    legend = c as Panel;
            }
            if (legend != null)
            {
                legend.Controls.Clear();
                AddLegendRow(legend, 0, "Healthy", healthy, sites.Count, Success);
                AddLegendRow(legend, 1, "Warning", warning, sites.Count, Warning);
                AddLegendRow(legend, 2, "Critical", critical, sites.Count, Danger);
                AddLegendRow(legend, 3, "Maintenance Due", maintenance, Math.Max(1, _jobs.Count), Blue);
            }
        }

        private void BindRegions(List<SiteMonitorRow> sites)
        {
            _regionGrid.Rows.Clear();
            foreach (var region in sites.GroupBy(s => s.Region).Select(g => new { Region = g.Key, Count = g.Count() }).OrderByDescending(r => r.Count).Take(7))
                _regionGrid.Rows.Add(region.Region, region.Count.ToString("N0") + " Sites");
        }

        private void BindUpcomingMaintenance()
        {
            _maintenanceGrid.Rows.Clear();
            foreach (JobSummaryDto job in _jobs.Where(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date >= DateTime.Today && j.ScheduledDate.Date <= DateTime.Today.AddDays(7)).OrderBy(j => j.ScheduledDate).Take(8))
                _maintenanceGrid.Rows.Add(First(job.SiteName, job.ClientName), job.ScheduledDate.ToString("dd-MMM-yyyy"), Contains(job.JobType, "AMC") ? "PM Visit" : First(job.JobType, "Visit"));
        }

        private void BindProblematicSites(List<SiteMonitorRow> sites)
        {
            _problemGrid.Rows.Clear();
            foreach (SiteMonitorRow site in sites.Where(s => s.OpenJobs > 0).OrderByDescending(s => s.OpenJobs).Take(7))
                _problemGrid.Rows.Add(site.Site, site.OpenJobs.ToString("N0"));
        }

        private void BindImmediateAttention()
        {
            _attentionGrid.Rows.Clear();
            foreach (JobSummaryDto job in _jobs.Where(j => !IsClosed(j.PipelineStatus) && (IsSlaRisk(j) || IsEmergency(j))).OrderByDescending(IsSlaRisk).ThenBy(j => j.ScheduledDate).Take(7))
                _attentionGrid.Rows.Add(First(job.SiteName, job.ClientName), First(job.JobTitle, First(job.JobType, "Service issue")), SlaText(job));
        }

        private void BindEquipmentSummary(int activeSites)
        {
            _equipmentGrid.Controls.Clear();
            int baseCount = Math.Max(1, activeSites);
            AddEquipmentTile("AC Units", baseCount * 4, ModernIconKind.Service, Blue, 0, 0);
            AddEquipmentTile("Chillers", Math.Max(2, baseCount / 3), ModernIconKind.Inventory, TextSecondary, 1, 0);
            AddEquipmentTile("AHUs", Math.Max(3, baseCount), ModernIconKind.Parts, TextSecondary, 2, 0);
            AddEquipmentTile("Cooling Towers", Math.Max(1, baseCount / 5), ModernIconKind.Activity, Blue, 0, 1);
            AddEquipmentTile("Exhaust Fans", baseCount * 2, ModernIconKind.Settings, TextSecondary, 1, 1);
            AddEquipmentTile("Other Equipment", Math.Max(1, baseCount / 2), ModernIconKind.EmptyBox, TextSecondary, 2, 1);
        }

        private void BindTechnicianPresence()
        {
            _technicianPresenceList.Controls.Clear();
            _technicianPresenceList.RowStyles.Clear();
            string[] labels = { "On Site", "Traveling", "Available", "On Leave" };
            Color[] colors = { Success, Blue, Color.FromArgb(34, 197, 94), TextSecondary };
            for (int i = 0; i < labels.Length; i++)
            {
                int count = CountTechniciansByPresence(labels[i]);
                Label label = new Label { Text = labels[i], Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.4f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
                Label value = new Label { Text = count.ToString("N0"), Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = colors[i], TextAlign = ContentAlignment.MiddleRight };
                _technicianPresenceList.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
                _technicianPresenceList.Controls.Add(label, 0, i);
                _technicianPresenceList.Controls.Add(value, 1, i);
            }
        }

        private void BindRevenue(List<SiteMonitorRow> sites)
        {
            decimal total = sites.Sum(s => s.Revenue);
            SiteMonitorRow top = sites.OrderByDescending(s => s.Revenue).FirstOrDefault();
            SiteMonitorRow low = sites.Where(s => s.Revenue > 0m).OrderBy(s => s.Revenue).FirstOrDefault();
            _siteRevenueTotal.Text = MoneyText(total);
            _siteRevenueTop.Text = (top == null ? "-" : MoneyText(top.Revenue) + "  " + TrimForWidth(top.Site, 18));
            _siteRevenueLow.Text = (low == null ? "-" : MoneyText(low.Revenue) + "  " + TrimForWidth(low.Site, 18));
        }

        private void BindSlaPerformance()
        {
            int total = _jobs.Count(j => !IsClosed(j.PipelineStatus) || j.ScheduledDate.Date >= DateTime.Today.AddDays(-30));
            int breached = _jobs.Count(IsSlaRisk);
            int met = Math.Max(0, total - breached);
            int pct = total == 0 ? 100 : Math.Max(0, Math.Min(100, (int)Math.Round(met * 100m / total)));
            _slaComplianceLabel.Text = pct.ToString("N0") + "%\r\nSLA Compliance";
            _slaTotalTickets.Text = total.ToString("N0");
            _slaMetTickets.Text = met.ToString("N0");
            _slaBreachedTickets.Text = breached.ToString("N0");
        }

        private void AddLegendRow(Panel legend, int row, string label, int count, int total, Color color)
        {
            int top = row * 34;
            Label dot = new Label { Text = "●", Location = new Point(2, top), Size = new Size(22, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter };
            int pct = total <= 0 ? 0 : (int)Math.Round(count * 100m / total);
            Label text = new Label { Text = label, Location = new Point(28, top + 2), Size = new Size(128, 22), Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary, AutoEllipsis = true };
            Label value = new Label { Text = count.ToString("N0") + " (" + pct + "%)", Location = new Point(160, top + 2), Size = new Size(86, 22), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight };
            legend.Controls.Add(dot);
            legend.Controls.Add(text);
            legend.Controls.Add(value);
        }

        private void AddEquipmentTile(string title, int count, ModernIconKind icon, Color accent, int column, int row)
        {
            Panel tile = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 250, 252), Margin = new Padding(5), Padding = new Padding(10) };
            tile.Paint += (s, e) => DrawRoundedBorder(e.Graphics, tile.ClientRectangle, Border);
            Label badge = ModernIconSystem.Badge(icon, 32, Lighten(accent, 0.88f), accent, 8);
            badge.Dock = DockStyle.Left;
            badge.Width = 42;
            Label value = new Label { Text = count.ToString("N0"), Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            Label label = new Label { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7.8f), ForeColor = TextPrimary, AutoEllipsis = true };
            Panel text = new Panel { Dock = DockStyle.Fill, BackColor = tile.BackColor, Padding = new Padding(8, 0, 0, 0) };
            text.Controls.Add(label);
            text.Controls.Add(value);
            tile.Controls.Add(text);
            tile.Controls.Add(badge);
            _equipmentGrid.Controls.Add(tile, column, row);
        }

        private void BindStaticFilters()
        {
            _binding = true;
            string previousLocation = Convert.ToString(_cmbLocation.SelectedItem ?? "All Locations");
            _cmbLocation.Items.Clear();
            _cmbLocation.Items.Add("All Locations");
            foreach (string site in _jobs.Select(j => First(j.SiteName, "")).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).Take(80))
                _cmbLocation.Items.Add(site);
            SelectComboText(_cmbLocation, previousLocation);
            if (_cmbLocation.SelectedIndex < 0) _cmbLocation.SelectedIndex = 0;

            string previousType = Convert.ToString(_cmbType.SelectedItem ?? "All Types");
            _cmbType.Items.Clear();
            _cmbType.Items.Add("All Types");
            foreach (string type in _jobs.Select(j => First(j.JobType, "")).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
                _cmbType.Items.Add(type);
            SelectComboText(_cmbType, previousType);
            if (_cmbType.SelectedIndex < 0) _cmbType.SelectedIndex = 0;

            string previousDesignation = Convert.ToString(_cmbTechnicianDesignationFilter?.SelectedItem ?? "All Designations");
            if (_cmbTechnicianDesignationFilter != null)
            {
                _cmbTechnicianDesignationFilter.Items.Clear();
                _cmbTechnicianDesignationFilter.Items.Add("All Designations");
                foreach (string designation in _technicians.Select(GetTechnicianDesignationFilterLabel).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
                    _cmbTechnicianDesignationFilter.Items.Add(designation);
                SelectComboText(_cmbTechnicianDesignationFilter, previousDesignation);
                if (_cmbTechnicianDesignationFilter.SelectedIndex < 0)
                    _cmbTechnicianDesignationFilter.SelectedIndex = 0;
            }

            _cmbAssignTechnician.Items.Clear();
            _cmbAssignTechnician.Items.Add(new ComboItem(0, "Unassigned"));
            foreach (Employee tech in SortDispatchTechnicians(GetVisibleTechnicians()))
                _cmbAssignTechnician.Items.Add(new ComboItem(tech.EmployeeID, FormatTechnicianOption(tech)));
            _binding = false;
        }

        private void BindKpis()
        {
            DateTime today = DateTime.Today;
            _kpiUnassigned.Text = _jobs.Count(j => !j.TechnicianId.HasValue || j.TechnicianId <= 0).ToString();
            _kpiToday.Text = _jobs.Count(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date == today).ToString();
            _kpiOverdue.Text = _jobs.Count(IsSlaRisk).ToString();
            _kpiProgress.Text = _jobs.Count(j => NormalizeStatus(j.PipelineStatus) == "In Progress").ToString();
            _kpiCompleted.Text = _jobs.Count(j => IsClosed(j.PipelineStatus) && j.ScheduledDate.Date == today).ToString();
            _kpiTechnicians.Text = GetVisibleTechnicians().Count(t => ResolveTechStatus(t, _jobs.Where(j => j.TechnicianId == t.EmployeeID && j.ScheduledDate.Date == today && !IsClosed(j.PipelineStatus)).ToList()) == "Available").ToString();
            foreach (Button tab in _queueTabs)
                tab.Text = _queueTabKeys[tab] + " (" + CountForQueue(_queueTabKeys[tab]).ToString() + ")";
        }

        private void ApplyJobFilters()
        {
            if (_binding || _jobListModule == null)
                return;

            string search = (_txtSearch?.Text ?? "").Trim();
            string type = Convert.ToString(_cmbType?.SelectedItem ?? "All Types");
            string priority = Convert.ToString(_cmbPriority?.SelectedItem ?? "All Priority");
            string location = Convert.ToString(_cmbLocation?.SelectedItem ?? "All Locations");

            IEnumerable<JobSummaryDto> query = _jobs;
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(j => Contains(j.JobNumber, search) || Contains(j.JobTitle, search) || Contains(j.ClientName, search) || Contains(j.SiteName, search) || Contains(j.TechnicianName, search));
            if (type != "All Types")
                query = query.Where(j => string.Equals(First(j.JobType, ""), type, StringComparison.OrdinalIgnoreCase));
            if (priority != "All Priority")
                query = query.Where(j => string.Equals(First(j.Priority, ""), priority, StringComparison.OrdinalIgnoreCase));
            if (location != "All Locations")
                query = query.Where(j => Contains(j.SiteName, location) || Contains(j.ClientName, location));

            query = ApplyQueue(query, _activeQueue);
            _visibleJobs = query
                .OrderByDescending(j => IsEmergency(j))
                .ThenByDescending(j => j.IsOverdue)
                .ThenBy(j => PriorityRank(j.Priority))
                .ThenBy(j => j.ScheduledDate)
                .ToList();
            RenderJobCards();
            RenderMapAndTimeline();
            UpdateTabStyles();
            UpdateViewAllJobsButton();
            if (_selectedJob == null || !_visibleJobs.Any(j => j.JobId == _selectedJob.JobId))
                SelectJob(_visibleJobs.FirstOrDefault());
        }

        private void BindTechnicians()
        {
            _techList.Controls.Clear();
            _techCards.Clear();
            List<Employee> visibleTechnicians = GetVisibleTechnicians();
            _lblTechTitle.Text = "TECHNICIANS (" + visibleTechnicians.Count + ")";
            int width = Math.Max(TechnicianCardWidth, _techList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
            foreach (Employee tech in SortDispatchTechnicians(visibleTechnicians))
            {
                Panel card = CreateTechnicianCard(tech);
                card.Width = width;
                _techCards.Add(card);
                _techList.Controls.Add(card);
            }
        }

        private Panel CreateTechnicianCard(Employee tech)
        {
            var todayJobs = _jobs.Where(j => j.TechnicianId == tech.EmployeeID && j.ScheduledDate.Date == DateTime.Today).ToList();
            int completed = todayJobs.Count(j => IsClosed(j.PipelineStatus));
            int progress = todayJobs.Count > 0 ? (int)Math.Round(completed * 100m / todayJobs.Count) : 0;
            string status = ResolveTechStatus(tech, todayJobs.Where(j => !IsClosed(j.PipelineStatus)).ToList());
            Color accent = TechStatusColor(status);
            string techName = DisplayTechnicianName(tech, todayJobs);
            string roleText = EmployeeService.GetDispatchTechnicianRole(tech);
            string siteText = First(tech.ClientSite, todayJobs.Select(j => j.SiteName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "Field");
            Panel card = new Panel { Width = TechnicianCardWidth, Height = TechnicianCardHeight, BackColor = White, Margin = new Padding(0, 0, 10, 8), Cursor = Cursors.Hand, Tag = tech };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, Border);
            AttachTechnicianCardResizeGrip(card);
            Label avatar = new Label { Text = Initials(techName), Location = new Point(10, 12), Size = new Size(32, 32), BackColor = Lighten(accent, 0.82f), ForeColor = accent, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            Label name = new Label { Text = techName, Location = new Point(50, 10), Size = new Size(150, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            Label stat = new Label { Text = "● " + status, Location = new Point(50, 30), Size = new Size(96, 18), Font = new Font("Segoe UI", 8f), ForeColor = accent };
            Label role = new Label { Text = roleText, Location = new Point(10, 52), Size = new Size(190, 17), Font = new Font("Segoe UI", 7.6f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            Label site = new Label { Text = siteText, Location = new Point(10, 70), Size = new Size(190, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextSecondary, AutoEllipsis = true };
            Label current = new Label { Text = todayJobs.Count + " jobs today, " + completed + " done", Location = new Point(10, 90), Size = new Size(190, 16), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = TextPrimary };
            Panel barBg = new Panel { Location = new Point(10, 114), Size = new Size(148, 5), BackColor = Color.FromArgb(226, 232, 240) };
            Panel bar = new Panel { Location = new Point(10, 114), Size = new Size(progress == 0 ? 0 : Math.Max(4, (int)Math.Round(148m * progress / 100m)), 5), BackColor = accent };
            Label pct = new Label { Text = progress + "%", Location = new Point(166, 108), Size = new Size(36, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextSecondary };
            card.Controls.AddRange(new Control[] { avatar, name, stat, role, site, current, barBg, bar, pct });
            _toolTip.SetToolTip(card, techName + "\r\n" + roleText + "\r\n" + siteText);
            _toolTip.SetToolTip(name, techName);
            _toolTip.SetToolTip(role, roleText);
            _toolTip.SetToolTip(site, siteText);
            card.Click += (s, e) => SelectTechnician(tech);
            foreach (Control child in card.Controls) child.Click += (s, e) => SelectTechnician(tech);
            return card;
        }

        private void RenderJobCards()
        {
            if (_jobListModule == null)
                return;

            _jobListModule.SetItems(_visibleJobs.Take(80).ToList());
            SyncJobSelection();
        }

        private void ResizeJobCards()
        {
            if (_jobListModule == null)
                return;
        }

        private bool HasDispatchFilters()
        {
            string search = (_txtSearch?.Text ?? string.Empty).Trim();
            string type = Convert.ToString(_cmbType?.SelectedItem ?? "All Types");
            string priority = Convert.ToString(_cmbPriority?.SelectedItem ?? "All Priority");
            return !string.IsNullOrWhiteSpace(search)
                || !string.Equals(type, "All Types", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(priority, "All Priority", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_activeQueue, "All", StringComparison.OrdinalIgnoreCase);
        }

        private void ClearDispatchFilters()
        {
            if (_txtSearch != null)
                _txtSearch.Clear();
            if (_cmbType != null && _cmbType.Items.Count > 0)
                _cmbType.SelectedIndex = 0;
            if (_cmbPriority != null && _cmbPriority.Items.Count > 0)
                _cmbPriority.SelectedIndex = 0;
            _activeQueue = "All";
            foreach (Button tab in _queueTabs)
            {
                string key = _queueTabKeys.ContainsKey(tab) ? _queueTabKeys[tab] : string.Empty;
                bool active = key == _activeQueue;
                tab.BackColor = active ? Lighten(Primary, 0.88f) : White;
                tab.ForeColor = active ? Primary : TextSecondary;
            }
            ApplyJobFilters();
            SetStatus("Dispatch filters cleared.", Info);
        }

        private Control CreateEmptyState(string title, string subtitle, bool showClearFilters)
        {
            int width = _jobListModule == null ? 300 : Math.Max(280, _jobListModule.ClientSize.Width - 26);
            Panel empty = new Panel { Width = width, Height = showClearFilters ? 170 : 138, BackColor = Color.FromArgb(248, 250, 252), Margin = new Padding(0, 8, 0, 8) };
            empty.Paint += (s, e) =>
            {
                DrawRoundedBorder(e.Graphics, empty.ClientRectangle, Border);
                using (Brush b = new SolidBrush(Lighten(Primary, 0.84f))) e.Graphics.FillEllipse(b, 20, 18, 42, 42);
                using (Brush b = new SolidBrush(Primary)) e.Graphics.DrawString("?", new Font("Segoe UI", 16f, FontStyle.Bold), b, 34, 24);
            };
            var titleLabel = new Label { Text = title, Location = new Point(76, 24), Size = new Size(empty.Width - 96, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            var subtitleLabel = new Label { Text = subtitle, Location = new Point(76, 50), Size = new Size(empty.Width - 96, 42), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, AutoEllipsis = true };
            empty.Controls.Add(titleLabel);
            empty.Controls.Add(subtitleLabel);
            Button clear = null;
            if (showClearFilters)
            {
                clear = MakeToolbarButton("Clear Filters", 118);
                clear.Location = new Point(76, 104);
                clear.Click += (s, e) => ClearDispatchFilters();
                empty.Controls.Add(clear);
            }
            empty.Resize += (s, e) =>
            {
                int textWidth = Math.Max(160, empty.ClientSize.Width - 96);
                titleLabel.Width = textWidth;
                subtitleLabel.Width = textWidth;
                if (clear != null)
                    clear.Location = new Point(76, empty.ClientSize.Height - clear.Height - 22);
            };
            return empty;
        }

        private Panel CreateJobCard(JobSummaryDto job)
        {
            Color accent = PriorityColor(job.Priority);
            bool selected = _selectedJob != null && _selectedJob.JobId == job.JobId;
            int width = _jobListModule == null ? 320 : Math.Max(300, _jobListModule.ClientSize.Width - 26);
            Panel card = new Panel { Width = width, Height = 112, BackColor = selected ? Lighten(Primary, 0.93f) : White, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand, Tag = job };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, selected ? Primary : Border);
            Label number = new Label { Text = First(job.JobNumber, "JOB"), Location = new Point(12, 12), Size = new Size(132, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Blue, AutoEllipsis = true };
            Label badge = CreateBadge(QueueLabel(job), StatusColor(job), new Point(card.Width - 110, 10), 92);
            badge.Tag = "QueueBadge";
            Label title = new Label { Text = First(job.JobTitle, "Service job"), Location = new Point(12, 34), Size = new Size(card.Width - 166, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true, Tag = "QueueTitle" };
            Label client = new Label { Text = First(job.ClientName, "No client"), Location = new Point(12, 56), Size = new Size(card.Width - 24, 16), Font = new Font("Segoe UI", 7.8f), ForeColor = TextSecondary, AutoEllipsis = true, Tag = "QueueClient" };
            Label site = new Label { Text = First(job.SiteName, "No site"), Location = new Point(12, 76), Size = new Size(card.Width - 180, 16), Font = new Font("Segoe UI", 7.8f), ForeColor = TextSecondary, AutoEllipsis = true, Tag = "QueueSite" };
            Label priority = new Label { Text = First(job.Priority, "Medium"), Location = new Point(card.Width - 110, 54), Size = new Size(96, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = accent, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, Tag = "QueuePriority" };
            Label sla = new Label { Text = SlaText(job), Location = new Point(card.Width - 148, 76), Size = new Size(134, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = SlaColor(job), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, Tag = "QueueSla" };
            Label tech = new Label { Text = First(job.TechnicianName, "Unassigned"), Location = new Point(card.Width - 148, 94), Size = new Size(134, 16), Font = new Font("Segoe UI", 7.6f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, Tag = "QueueTech" };
            card.Controls.AddRange(new Control[] { number, badge, title, client, site, priority, sla, tech });
            card.Click += (s, e) => SelectJob(job);
            foreach (Control child in card.Controls) child.Click += (s, e) => SelectJob(job);
            return card;
        }

        private void SelectJob(JobSummaryDto job)
        {
            _selectedJob = job;
            SyncJobSelection();
            LoadJobDetails(job);
        }

        private void SyncJobSelection()
        {
            if (_jobListModule == null)
                return;

            int? selectedId = _selectedJob == null ? (int?)null : _selectedJob.JobId;
            if (_jobListModule.GetSelectedRowId() == selectedId)
                return;

            _syncingJobSelection = true;
            try
            {
                _jobListModule.SetSelectedRowId(selectedId);
            }
            finally
            {
                _syncingJobSelection = false;
            }
        }

        private void HandleJobRowSelected(JobSummaryDto job)
        {
            if (_syncingJobSelection || job == null)
                return;

            SelectJob(job);
        }

        private void SelectTechnician(Employee tech)
        {
            _selectedTechnician = tech;
            foreach (Panel card in _techCards)
            {
                card.BackColor = card.Tag == tech ? Lighten(Primary, 0.93f) : White;
                card.Invalidate();
            }
            if (tech != null)
                SelectComboById(_cmbAssignTechnician, tech.EmployeeID);
        }

        private void LoadJobDetails(JobSummaryDto job)
        {
            _binding = true;
            if (job == null)
            {
                _detailJobNumber.Text = "No job selected";
                _detailBadge.Text = "";
                _detailTitle.Text = "Select a job from the queue";
                _detailSla.Text = "";
                _detailSla.BackColor = White;
                _detailClient.Text = "";
                _detailSite.Text = "";
                _detailAddress.Text = "";
                if (_detailScheduleBanner != null) _detailScheduleBanner.Text = "";
                _txtProblem.Text = "";
                _lblSuggestedTech.Text = "";
                _lblJobInfo.Text = "";
                if (_lblScheduleWarning != null) _lblScheduleWarning.Text = "";
                if (_detailTabs != null)
                {
                    _detailTabs.Enabled = false;
                    _detailTabs.Visible = false;
                }
                if (_jobInformationCard != null) _jobInformationCard.Visible = false;
                SetQuickActionsEnabled(false);
                _binding = false;
                return;
            }

            if (_detailTabs != null)
            {
                _detailTabs.Enabled = true;
                _detailTabs.Visible = true;
            }
            if (_jobInformationCard != null) _jobInformationCard.Visible = true;
            SetQuickActionsEnabled(true);

            _detailJobNumber.Text = First(job.JobNumber, "JOB");
            _detailBadge.Text = QueueLabel(job).ToUpperInvariant();
            _detailBadge.BackColor = Lighten(StatusColor(job), 0.86f);
            _detailBadge.ForeColor = StatusColor(job);
            _detailTitle.Text = First(job.JobTitle, "Service job");
            bool breached = SlaText(job).IndexOf("breached", StringComparison.OrdinalIgnoreCase) >= 0;
            _detailSla.Text = breached ? "SLA BREACHED\r\nImmediate dispatcher action required" : "SLA WINDOW\r\n" + SlaText(job);
            _detailSla.ForeColor = SlaColor(job);
            _detailSla.BackColor = breached ? Lighten(Danger, 0.88f) : Lighten(SlaColor(job), 0.9f);
            _detailClient.Text = "Client: " + First(job.ClientName, "No client");
            _detailSite.Text = "Site: " + First(job.SiteName, "No site");
            _detailAddress.Text = "Address: " + First(job.SiteName, "Field location");
            bool pastSchedule = job.ScheduledDate != default(DateTime) && job.ScheduledDate < DateTime.Now && !IsClosed(job.PipelineStatus);
            if (_detailScheduleBanner != null)
            {
                _detailScheduleBanner.Text = "Schedule: " + (job.ScheduledDate == default(DateTime) ? "Pending" : job.ScheduledDate.ToString("dd/MM/yyyy hh:mm tt")) + (pastSchedule ? "  - Past scheduled time" : "");
                _detailScheduleBanner.ForeColor = pastSchedule ? Danger : TextSecondary;
                _detailScheduleBanner.BackColor = pastSchedule ? Lighten(Danger, 0.9f) : Color.FromArgb(248, 250, 252);
            }
            _txtProblem.Text = First(job.Notes, "AC in server room is not cooling. Temperature is high.");
            SelectComboById(_cmbAssignTechnician, job.TechnicianId ?? 0);
            SelectComboText(_cmbDetailStatus, NormalizeStatus(job.PipelineStatus));
            _dtpSchedule.Value = job.ScheduledDate == default(DateTime) ? DateTime.Now : job.ScheduledDate;
            if (_lblScheduleWarning != null)
            {
                _lblScheduleWarning.Text = pastSchedule ? "Past scheduled time" : "";
                _dtpSchedule.CalendarTitleBackColor = pastSchedule ? Danger : Primary;
                _dtpSchedule.CalendarForeColor = pastSchedule ? Danger : TextPrimary;
            }
            Employee suggestion = SuggestTechnician(job);
            _lblSuggestedTech.Text = suggestion != null ? "Suggested: " + suggestion.Name : "Suggested: none";

            string info = "Client: " + First(job.ClientName, "No client") + "\r\n\r\n"
                + "Site: " + First(job.SiteName, "No site") + "\r\n\r\n"
                + "Type: " + First(job.JobType, "Service") + "\r\n\r\n"
                + "Priority: " + First(job.Priority, "Normal") + "\r\n\r\n"
                + "Scheduled: " + (job.ScheduledDate == default(DateTime) ? "Pending" : job.ScheduledDate.ToString("dd/MM/yyyy hh:mm tt")) + "\r\n\r\n"
                + "Assigned To: " + First(job.TechnicianName, "Unassigned");
            _lblJobInfo.Text = info;
            _binding = false;
        }

        private void SaveAssignment()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Add or select a real job to save assignment.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Job job = _jobService.GetById(_selectedJob.JobId);
                if (job == null)
                    throw new Exception("Job not found.");

                int techId = SelectedId(_cmbAssignTechnician);
                string status = Convert.ToString(_cmbDetailStatus.SelectedItem ?? "");
                if ((status == "Assigned" || status == "In Progress" || status == "Traveling" || status == "On Site") && techId <= 0)
                    throw new Exception("Select a technician before assigning or starting the job.");

                job.AssignedEmployeeID = techId > 0 ? (int?)techId : null;
                job.ScheduledDate = _dtpSchedule.Value;
                job.PipelineStatus = ToPipelineStatus(status);
                job.Status = status == "In Progress" ? "In Progress" : status;
                _jobService.Update(job);
                _jobService.LogActivity(job.JobID, "Dispatch assignment updated.", "Info");
                QueueLoadDispatchData();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DispatchCenter.SaveAssignment", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Site Monitor"), "Saving dispatch assignment", ex);
                MessageBox.Show("Assignment could not be saved. Review the job status and technician, then try again.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateJobStatus()
        {
            SaveAssignment();
        }

        private void SaveSchedule()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Add or select a real job to update the schedule.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Job job = _jobService.GetById(_selectedJob.JobId);
                if (job == null)
                    throw new Exception("Job not found.");

                job.ScheduledDate = _dtpSchedule.Value;
                _jobService.Update(job);
                _jobService.LogActivity(job.JobID, "Job schedule updated from Site Monitor.", "Info");
                QueueLoadDispatchData();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DispatchCenter.SaveSchedule", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Site Monitor"), "Updating job schedule", ex);
                MessageBox.Show("Schedule could not be saved. Review the date/time, then try again.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EscalateSelected()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Escalation is available for real jobs only.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Job job = _jobService.GetById(_selectedJob.JobId);
                job.Priority = "Critical";
                _jobService.Update(job);
                _jobService.LogActivity(job.JobID, "Job escalated from Site Monitor.", "Warning");
                QueueLoadDispatchData();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Site Monitor"), "Escalating job", ex);
                MessageBox.Show("Job could not be escalated right now. Refresh the site monitor list and try again.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddNote()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Notes can be saved on real jobs.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string text = PromptText("Add dispatcher note", "");
            if (string.IsNullOrWhiteSpace(text))
                return;
            Job job = _jobService.GetById(_selectedJob.JobId);
            string notes = (job.Notes ?? "") + Environment.NewLine + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " - " + text.Trim();
            _jobService.UpdateNotes(job.JobID, notes);
            _jobService.LogActivity(job.JobID, "Dispatcher note added.", "Info");
            QueueLoadDispatchData();
        }

        private void PrintJob()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (OnOpenJobDetail != null && !_usingFallbackJobs)
                OnOpenJobDetail(_selectedJob.JobId);
            else
                MessageBox.Show("Print job uses the existing job detail/print workflow. Open a real job to print.", "Site Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Employee SuggestTechnician(JobSummaryDto job)
        {
            List<Employee> candidates = GetVisibleTechnicians();
            if (candidates.Count == 0)
                candidates = _technicians ?? new List<Employee>();

            return candidates
                .OrderBy(t => _jobs.Count(j => j.TechnicianId == t.EmployeeID && j.ScheduledDate.Date == DateTime.Today && !IsClosed(j.PipelineStatus)))
                .ThenBy(t => t.Name)
                .FirstOrDefault();
        }

        private void RenderMapAndTimeline()
        {
            _timelinePanel?.Invalidate();
        }

        private void DrawTimeline(object sender, PaintEventArgs e)
        {
            Panel p = (Panel)sender;
            e.Graphics.Clear(White);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int top = 10;
            int techNameW = 142;
            int timeW = 72;
            int statusW = 84;
            var techRows = GetVisibleTechnicians().Take(5).ToList();
            if (techRows.Count == 0)
            {
                using (Brush b = new SolidBrush(Muted))
                    e.Graphics.DrawString("No technicians found.", new Font("Segoe UI", 9f, FontStyle.Bold), b, 12, 18);
                return;
            }
            for (int r = 0; r < techRows.Count; r++)
            {
                int y = top + r * 46;
                List<JobSummaryDto> assigned = _jobs.Where(j => j.TechnicianId == techRows[r].EmployeeID && j.ScheduledDate.Date == DateTime.Today).OrderBy(j => j.ScheduledDate).Take(2).ToList();
                using (Brush b = new SolidBrush(TextPrimary))
                    e.Graphics.DrawString(TrimForWidth(DisplayTechnicianName(techRows[r], assigned), 20), new Font("Segoe UI", 8.2f, FontStyle.Bold), b, 8, y + 6);
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, 8, y + 43, p.Width - 8, y + 43);

                if (assigned.Count == 0)
                {
                    using (Brush b = new SolidBrush(Muted))
                        e.Graphics.DrawString("No jobs assigned today", new Font("Segoe UI", 8f), b, techNameW, y + 7);
                    continue;
                }

                for (int j = 0; j < assigned.Count; j++)
                {
                    JobSummaryDto job = assigned[j];
                    int rowY = y + 4 + j * 18;
                    int titleX = techNameW + timeW + 8;
                    int siteX = Math.Min(p.Width - statusW - 130, titleX + 138);
                    using (Brush b = new SolidBrush(Muted))
                        e.Graphics.DrawString(job.ScheduledDate.ToString("hh:mm tt"), new Font("Segoe UI", 7.8f, FontStyle.Bold), b, techNameW, rowY);
                    using (Brush b = new SolidBrush(TextPrimary))
                        e.Graphics.DrawString(TrimForWidth(First(job.JobTitle, "Service job"), 20), new Font("Segoe UI", 7.8f, FontStyle.Bold), b, titleX, rowY);
                    using (Brush b = new SolidBrush(TextSecondary))
                        e.Graphics.DrawString(TrimForWidth(First(job.SiteName, "Field"), 18), new Font("Segoe UI", 7.8f), b, siteX, rowY);
                    Rectangle badge = new Rectangle(p.Width - statusW - 12, rowY - 1, statusW, 16);
                    using (Brush br = new SolidBrush(Lighten(StatusColor(job), 0.86f))) e.Graphics.FillRectangle(br, badge);
                    using (Brush br = new SolidBrush(StatusColor(job))) e.Graphics.DrawString(TrimForWidth(NormalizeStatus(job.PipelineStatus), 10), new Font("Segoe UI", 6.8f, FontStyle.Bold), br, badge.X + 5, badge.Y + 1);
                }
            }
        }

        private void AddQuickAction(TableLayoutPanel parent, string text, Action action)
        {
            int index = parent.Controls.Count;
            int columns = Math.Max(1, parent.ColumnCount);
            int column = index % columns;
            int row = index / columns;
            Button b = MakeToolbarButton(text, 132);
            b.Dock = DockStyle.Fill;
            b.Height = 42;
            b.Margin = new Padding(column == columns - 1 ? 0 : 8, 0, 0, row >= parent.RowCount - 1 ? 0 : 8);
            b.AutoEllipsis = true;
            b.Click += (s, e) => action();
            _jobActionButtons.Add(b);
            parent.Controls.Add(b, column, row);
        }

        private Panel CreateCard()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(12) };
            p.Paint += (s, e) => DrawRoundedBorder(e.Graphics, p.ClientRectangle, Border);
            return p;
        }

        private void AttachDispatchRowResizeGrip(Panel card, int minimumHeight)
        {
            if (card == null)
                return;

            card.MinimumSize = new Size(Math.Max(card.MinimumSize.Width, 260), Math.Max(card.MinimumSize.Height, minimumHeight));
            Action<Control, Size> resizeRow = (control, size) => ResizeDispatchTableRow(control, size, minimumHeight);
            CardResizeGripService.Attach(card, resizeRow, resizeRow);
        }

        private void AttachTechnicianCardResizeGrip(Panel card)
        {
            if (card == null)
                return;

            card.MinimumSize = new Size(TechnicianCardWidth, TechnicianCardHeight);
            CardResizeGripService.Attach(card, (control, size) =>
            {
                if (_techList != null && !_techList.IsDisposed)
                    _techList.PerformLayout();
            });
        }

        private void ResizeDispatchTableRow(Control control, Size size, int minimumHeight)
        {
            TableLayoutPanel table = control == null ? null : control.Parent as TableLayoutPanel;
            if (table == null)
                return;

            int row = table.GetRow(control);
            if (row < 0 || row >= table.RowStyles.Count)
                return;

            int targetHeight = Math.Max(minimumHeight, size.Height + control.Margin.Vertical);
            table.RowStyles[row].SizeType = SizeType.Absolute;
            table.RowStyles[row].Height = targetHeight;
            table.PerformLayout();
        }

        private Label SectionTitle(string text)
        {
            return new Label { Text = text, Height = 28, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        }

        private Button MakePrimaryButton(string text, int width)
        {
            Button b = MakeToolbarButton(text, width);
            b.BackColor = Primary;
            b.ForeColor = White;
            b.FlatAppearance.BorderColor = Primary;
            return b;
        }

        private Button MakeToolbarButton(string text, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 36, BackColor = White, ForeColor = TextPrimary, Font = new Font("Segoe UI", 9f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), AutoEllipsis = true };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            UIHelper.ApplyActionButton(b);
            return b;
        }

        private Button MakeSiteToolbarButton(string text, ModernIconKind icon, int width)
        {
            Button button = MakeToolbarButton(text, width);
            button.Height = 36;
            button.Image = ModernIconSystem.IconBitmap(icon, 15, TextSecondary);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(8, 0, 8, 0);
            return button;
        }

        private ComboBox MakeCombo()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System, Font = new Font("Segoe UI", 9f), Height = 30 };
        }

        /// <summary>Wraps an editable input in a shared visible-outline host.</summary>
        private Panel WrapInput(Control input, string name)
        {
            Panel host = new Panel { Name = name, Dock = DockStyle.Fill, BackColor = DS.BgInput, Padding = new Padding(6, 1, 6, 1) };
            if (input != null)
            {
                input.Dock = DockStyle.Fill;
                input.Margin = Padding.Empty;
                host.Controls.Add(input);
            }
            return host;
        }

        private DataGridView MakeSmallGrid()
        {
            DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = White, BorderStyle = BorderStyle.None, RowHeadersVisible = false, AllowUserToAddRows = false, ReadOnly = true };
            GridTheme.Apply(grid);
            return grid;
        }

        private Label CreateBadge(string text, Color color, Point location, int width)
        {
            return new Label { Text = text.ToUpperInvariant(), Location = location, Size = new Size(width, 20), BackColor = Lighten(color, 0.86f), ForeColor = color, Font = new Font("Segoe UI", 7f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
        }

        private void AddInfoPair(TableLayoutPanel form, int row, string left, string right)
        {
            form.Controls.Add(SmallLabel(left), 0, row);
            form.Controls.Add(SmallLabel(right), 1, row);
        }

        private Label SmallLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true };
        }

        private static void DrawRoundedBorder(Graphics g, Rectangle rect, Color color)
        {
            rect.Width -= 1;
            rect.Height -= 1;
            using (Pen pen = new Pen(color))
                g.DrawRectangle(pen, rect);
        }

        private static Color Lighten(Color color, float amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                color.R + (int)((255 - color.R) * amount),
                color.G + (int)((255 - color.G) * amount),
                color.B + (int)((255 - color.B) * amount));
        }

        private void DrawSiteDistribution(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            List<SiteMonitorRow> rows = BuildSiteMonitorRows();
            int healthy = Math.Max(1, rows.Count(s => s.HealthScore >= 80 && s.OpenJobs == 0));
            int warning = Math.Max(1, rows.Count(s => s.HealthScore >= 60 && s.OpenJobs > 0));
            int critical = Math.Max(1, rows.Count(s => s.HealthScore < 60 || s.CriticalJobs > 0));
            int maintenance = Math.Max(1, _jobs.Count(j => Contains(j.JobType, "AMC")));
            DrawDonut(e.Graphics, new Rectangle(26, 34, 142, 142), new[] { healthy, warning, critical, maintenance }, new[] { Success, Warning, Danger, Blue }, 34);
        }

        private void DrawTechnicianPresence(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int onSite = CountTechniciansByPresence("On Site");
            int traveling = CountTechniciansByPresence("Traveling");
            int available = CountTechniciansByPresence("Available");
            int leave = CountTechniciansByPresence("On Leave");
            Rectangle rect = new Rectangle(Math.Max(8, (panel.Width - 112) / 2), 28, 112, 112);
            DrawDonut(e.Graphics, rect, new[] { onSite, traveling, available, leave }, new[] { Success, Blue, Color.FromArgb(34, 197, 94), TextSecondary }, 25);
            using (Brush brush = new SolidBrush(TextPrimary))
            using (Font font = new Font("Segoe UI", 14f, FontStyle.Bold))
                e.Graphics.DrawString(_technicians.Count.ToString("N0"), font, brush, rect.X + 42, rect.Y + 38);
            using (Brush brush = new SolidBrush(TextSecondary))
            using (Font font = new Font("Segoe UI", 8f))
                e.Graphics.DrawString("Total", font, brush, rect.X + 42, rect.Y + 62);
        }

        private void DrawSlaGauge(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int total = Math.Max(1, _jobs.Count);
            int breached = _jobs.Count(IsSlaRisk);
            int pct = Math.Max(0, Math.Min(100, (int)Math.Round((total - breached) * 100m / total)));
            Rectangle rect = new Rectangle(18, 34, 104, 104);
            using (Pen bg = new Pen(Color.FromArgb(226, 232, 240), 12))
                e.Graphics.DrawArc(bg, rect, 180, 180);
            using (Pen fg = new Pen(Color.FromArgb(147, 51, 234), 12))
                e.Graphics.DrawArc(fg, rect, 180, 180 * pct / 100);
        }

        private void DrawSiteHealthTrend(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle area = new Rectangle(18, 22, Math.Max(120, panel.Width - 34), Math.Max(96, panel.Height - 54));
            using (Pen grid = new Pen(Color.FromArgb(226, 232, 240)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = area.Top + (area.Height * i / 4);
                    e.Graphics.DrawLine(grid, area.Left, y, area.Right, y);
                }
            }
            List<int> points = BuildHealthTrendPoints();
            if (points.Count < 2)
                return;
            Point[] path = points.Select((p, i) => new Point(area.Left + (area.Width * i / Math.Max(1, points.Count - 1)), area.Bottom - (area.Height * p / 100))).ToArray();
            using (Pen pen = new Pen(Success, 2.2f))
                e.Graphics.DrawLines(pen, path);
            foreach (Point point in path.Where((p, i) => i % 5 == 0 || i == path.Length - 1))
            {
                using (Brush brush = new SolidBrush(Success))
                    e.Graphics.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
            }
            using (Brush brush = new SolidBrush(TextSecondary))
            using (Font font = new Font("Segoe UI", 7.5f))
            {
                e.Graphics.DrawString("100%", font, brush, area.Left, area.Top - 16);
                e.Graphics.DrawString("0%", font, brush, area.Left, area.Bottom + 4);
            }
        }

        private static void DrawDonut(Graphics graphics, Rectangle rect, int[] values, Color[] colors, int thickness)
        {
            int total = Math.Max(1, values.Sum());
            float start = -90f;
            for (int i = 0; i < values.Length; i++)
            {
                float sweep = values[i] * 360f / total;
                using (Pen pen = new Pen(colors[i], thickness))
                    graphics.DrawArc(pen, rect, start, sweep);
                start += sweep;
            }
        }

        private List<int> BuildHealthTrendPoints()
        {
            int risk = _jobs.Count(IsSlaRisk);
            int open = _jobs.Count(j => !IsClosed(j.PipelineStatus));
            int baseHealth = Math.Max(55, 88 - risk * 3 - Math.Max(0, open - 10));
            List<int> points = new List<int>();
            for (int i = 0; i < 28; i++)
            {
                int wave = (int)Math.Round(Math.Sin(i / 3.0) * 5);
                points.Add(Math.Max(40, Math.Min(96, baseHealth - 6 + i / 2 + wave)));
            }
            return points;
        }

        private string ResolveRegion(string site, string client)
        {
            string text = ((site ?? "") + " " + (client ?? "")).ToLowerInvariant();
            if (text.Contains("mumbai") || text.Contains("mahad") || text.Contains("navi")) return "Mumbai";
            if (text.Contains("pune")) return "Pune";
            if (text.Contains("nashik")) return "Nashik";
            if (text.Contains("delhi") || text.Contains("gurgaon")) return "Delhi NCR";
            if (text.Contains("bangalore") || text.Contains("bengaluru")) return "Bengaluru";
            if (text.Contains("chennai")) return "Chennai";
            return "Others";
        }

        private int CountTechniciansByPresence(string status)
        {
            DateTime today = DateTime.Today;
            if (status == "On Site")
                return _technicians.Count(t => _jobs.Any(j => j.TechnicianId == t.EmployeeID && j.ScheduledDate.Date == today && NormalizeStatus(j.PipelineStatus) == "In Progress"));
            if (status == "Traveling")
                return _technicians.Count(t => _jobs.Any(j => j.TechnicianId == t.EmployeeID && j.ScheduledDate.Date == today && (NormalizeStatus(j.PipelineStatus) == "Traveling" || NormalizeStatus(j.PipelineStatus) == "Assigned")));
            if (status == "On Leave")
                return Math.Max(0, _technicians.Count(t => Contains(t.Status, "Leave") || Contains(t.Status, "Inactive")));
            return Math.Max(0, _technicians.Count - CountTechniciansByPresence("On Site") - CountTechniciansByPresence("Traveling") - CountTechniciansByPresence("On Leave"));
        }

        private static string MoneyText(decimal value)
        {
            return IndiaFormatHelper.FormatCurrency(value);
        }

        private List<JobSummaryDto> BuildSeedDispatchJobs()
        {
            DateTime today = DateTime.Today;
            DateTime yesterday = today.AddDays(-1);
            return new List<JobSummaryDto>
            {
                SeedJob(9001, "JOB-001", "AC Servicing", "AMC", "Unassigned", "Normal", today.AddHours(9), "BLUE JET", "BLUE JET - Main", null, null, false),
                SeedJob(9002, "JOB-002", "Chiller Repair", "Emergency", "In Progress", "High", today.AddHours(10), "SOLARA", "SOLARA - Roof", 1, "Kashee Chauhan", false),
                SeedJob(9003, "JOB-003", "Preventive Maintenance", "Scheduled", "Scheduled", "Normal", today.AddHours(10.5), "Deccan AC6", "Deccan_AC6 - Floor 3", 2, "Manoj Gopinathan Nair", false),
                SeedJob(9004, "JOB-004", "Cooling Tower Inspection", "AMC", "Overdue", "Normal", yesterday.AddHours(11), "ABC Corp", "ABC - Terrace", 3, "Adilhusen Jakirhusen Shaikh", true),
                SeedJob(9005, "JOB-005", "VRF System Check", "Emergency", "SLA Risk", "Critical", today.AddHours(12), "Elite Components", "Elite - Server Room", null, null, false),
                SeedJob(9006, "JOB-006", "Filter Replacement", "AMC", "Completed", "Low", today.AddHours(8.5), "HVAC World", "HVAC - Lobby", 4, "Mahmed Sufiyan Shaikh", false),
                SeedJob(9007, "JOB-007", "Duct Cleaning", "Scheduled", "Scheduled", "Normal", today.AddHours(14), "Thermo Supplies", "Thermo - Warehouse", 1, "Kashee Chauhan", false),
                SeedJob(9008, "JOB-008", "Compressor Fault", "Emergency", "Unassigned", "High", today.AddHours(15), "CoolTech", "CoolTech - Plant", null, null, false),
                SeedJob(9009, "JOB-009", "Annual Checkup", "AMC", "Due", "Normal", today.AddHours(16), "Global Ent.", "Global - Office", 2, "Manoj Gopinathan Nair", false),
                SeedJob(9010, "JOB-010", "Gas Refill", "Scheduled", "In Progress", "Normal", today.AddHours(13), "Logistics Partner", "LP - Cold Storage", 3, "Adilhusen Jakirhusen Shaikh", false)
            };
        }

        private JobSummaryDto SeedJob(int id, string number, string title, string type, string status, string priority, DateTime scheduled, string client, string site, int? techId, string techName, bool overdue)
        {
            return new JobSummaryDto
            {
                JobId = id,
                JobNumber = number,
                JobTitle = title,
                JobType = type,
                PipelineStatus = status,
                Priority = priority,
                ScheduledDate = scheduled,
                ClientName = client,
                SiteName = site,
                TechnicianId = techId,
                TechnicianName = techName,
                IsOverdue = overdue,
                Notes = title + " for " + client
            };
        }

        private List<Employee> BuildSeedTechnicians()
        {
            return new List<Employee>
            {
                new Employee { EmployeeID = 1, EmployeeCode = "TEC-001", Name = "Kashee Chauhan", ClientSite = "BLUE JET", Status = "Active", Designation = "Technician" },
                new Employee { EmployeeID = 2, EmployeeCode = "TEC-002", Name = "Manoj Gopinathan Nair", ClientSite = "SOLARA", Status = "Active", Designation = "Technician" },
                new Employee { EmployeeID = 3, EmployeeCode = "TEC-003", Name = "Adilhusen Jakirhusen Shaikh", ClientSite = "Deccan_AC6", Status = "Active", Designation = "Technician" },
                new Employee { EmployeeID = 4, EmployeeCode = "TEC-004", Name = "Mahmed Sufiyan Shaikh", ClientSite = "Deccan Fine Chemicals", Status = "Active", Designation = "Technician" },
                new Employee { EmployeeID = 5, EmployeeCode = "TEC-005", Name = "Ravi Kulkarni", ClientSite = "HVAC World", Status = "Active", Designation = "Technician" },
                new Employee { EmployeeID = 6, EmployeeCode = "TEC-006", Name = "Priya Desai", ClientSite = "ABC Corp", Status = "Inactive", Designation = "Technician" }
            };
        }

        private static string TrimForWidth(string value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxChars)
                return value ?? "";
            return value.Substring(0, Math.Max(1, maxChars - 3)) + "...";
        }

        private IEnumerable<JobSummaryDto> ApplyQueue(IEnumerable<JobSummaryDto> query, string queue)
        {
            DateTime today = DateTime.Today;
            switch (queue)
            {
                case "Emergency": return query.Where(IsEmergency);
                case "Due":
                case "Due Today": return query.Where(j => !IsClosed(j.PipelineStatus) && NormalizeStatus(j.PipelineStatus) == "Due");
                case "Scheduled": return query.Where(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date >= today && !IsEmergency(j) && (NormalizeStatus(j.PipelineStatus) == "Scheduled" || Contains(j.JobType, "Scheduled")));
                case "Overdue": return query.Where(j => j.IsOverdue || (j.ScheduledDate.Date < today && !IsClosed(j.PipelineStatus)));
                case "AMC": return query.Where(j => Contains(j.JobType, "AMC") || Contains(j.JobTitle, "AMC"));
                default: return query;
            }
        }

        private int CountForQueue(string queue) => ApplyQueue(_jobs, queue).Count();
        private bool IsEmergency(JobSummaryDto job) => string.Equals(job.Priority, "Critical", StringComparison.OrdinalIgnoreCase) || string.Equals(job.Priority, "High", StringComparison.OrdinalIgnoreCase) || Contains(job.JobType, "Emergency") || Contains(job.PipelineStatus, "Emergency") || NormalizeStatus(job.PipelineStatus) == "SLA Risk";
        private bool IsSlaRisk(JobSummaryDto job) => job.IsOverdue || NormalizeStatus(job.PipelineStatus) == "SLA Risk" || (IsEmergency(job) && job.ScheduledDate != default(DateTime) && job.ScheduledDate <= DateTime.Now.AddHours(2) && !IsClosed(job.PipelineStatus));
        private bool IsClosed(string status) => NormalizeStatus(status) == "Completed" || NormalizeStatus(status) == "Closed" || NormalizeStatus(status) == "Cancelled";
        private string NormalizeStatus(string status)
        {
            string s = (status ?? "").Replace(" ", "").Trim().ToUpperInvariant();
            if (s == "INPROGRESS") return "In Progress";
            if (s == "CHECKLISTDONE") return "Completed";
            if (s == "INVOICED") return "Closed";
            if (s == "ASSIGNED") return "Assigned";
            if (s == "CREATED") return "Created";
            if (s == "CLOSED") return "Closed";
            if (s == "COMPLETED") return "Completed";
            if (s == "SCHEDULED") return "Scheduled";
            if (s == "DUE") return "Due";
            if (s == "OVERDUE") return "Overdue";
            if (s == "SLARISK") return "SLA Risk";
            return string.IsNullOrWhiteSpace(status) ? "Created" : status;
        }
        private string ToPipelineStatus(string status)
        {
            switch (status)
            {
                case "In Progress":
                case "On Site":
                case "Traveling": return "InProgress";
                case "Completed": return "ChecklistDone";
                case "Closed": return "Closed";
                case "Cancelled": return "Closed";
                case "Assigned": return "Assigned";
                default: return "Created";
            }
        }
        private string QueueLabel(JobSummaryDto job)
        {
            if (IsEmergency(job)) return "Emergency";
            if (job.IsOverdue || job.ScheduledDate.Date < DateTime.Today) return "Overdue";
            if (NormalizeStatus(job.PipelineStatus) == "Due") return "Due";
            return Contains(job.JobType, "AMC") || Contains(job.JobTitle, "AMC") ? "AMC" : "Scheduled";
        }
        private Color StatusColor(JobSummaryDto job)
        {
            string label = QueueLabel(job);
            if (label == "Emergency" || label == "Overdue") return Danger;
            if (label == "Due") return Warning;
            if (label == "AMC") return Info;
            return Blue;
        }
        private Color PriorityColor(string priority)
        {
            if (string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase) || string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase)) return Danger;
            if (string.Equals(priority, "Medium", StringComparison.OrdinalIgnoreCase)) return Warning;
            return Success;
        }
        private Color SlaColor(JobSummaryDto job)
        {
            if (IsSlaRisk(job)) return Danger;
            if ((job.ScheduledDate - DateTime.Now).TotalHours < 2) return Warning;
            return Success;
        }
        private string SlaText(JobSummaryDto job)
        {
            if (job.ScheduledDate == default(DateTime)) return "Schedule pending";
            if (IsSlaRisk(job)) return "SLA breached";
            if (job.ScheduledDate.Date == DateTime.Today && job.ScheduledDate < DateTime.Now) return "Scheduled today";
            TimeSpan left = job.ScheduledDate.AddHours(2) - DateTime.Now;
            if (left.TotalSeconds <= 0) return "SLA breached";
            return string.Format("{0:00}:{1:00}:{2:00} time left", (int)left.TotalHours, left.Minutes, left.Seconds);
        }
        private int PriorityRank(string priority)
        {
            if (string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(priority, "Medium", StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }
        private string ResolveTechStatus(Employee tech, List<JobSummaryDto> assigned)
        {
            if (!string.Equals(tech.Status, "Active", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(tech.Status)) return "Offline";
            if (assigned.Any(j => NormalizeStatus(j.PipelineStatus) == "In Progress")) return "On Job";
            if (assigned.Count >= 4) return "Busy";
            if (assigned.Any()) return "Traveling";
            return "Available";
        }
        private Color TechStatusColor(string status)
        {
            switch (status)
            {
                case "Available": return MapAvailable;
                case "On Job": return MapOnJob;
                case "Traveling": return MapTraveling;
                case "Busy": return MapBusy;
                default: return MapOffline;
            }
        }
        private IEnumerable<Employee> SortDispatchTechnicians(IEnumerable<Employee> technicians)
        {
            return (technicians ?? new List<Employee>())
                .OrderBy(EmployeeService.GetDispatchTechnicianSortRank)
                .ThenBy(EmployeeService.GetDispatchTechnicianRole)
                .ThenBy(t => t.Name ?? string.Empty);
        }

        private List<Employee> GetVisibleTechnicians()
        {
            IEnumerable<Employee> query = _technicians ?? new List<Employee>();
            string designation = Convert.ToString(_cmbTechnicianDesignationFilter?.SelectedItem ?? "All Designations");
            if (!string.IsNullOrWhiteSpace(designation) && !string.Equals(designation, "All Designations", StringComparison.OrdinalIgnoreCase))
                query = query.Where(t => string.Equals(GetTechnicianDesignationFilterLabel(t), designation, StringComparison.OrdinalIgnoreCase));
            return SortDispatchTechnicians(query).ToList();
        }

        private static string GetTechnicianDesignationFilterLabel(Employee tech)
        {
            if (tech == null)
                return string.Empty;

            string designation = (tech.Designation ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(designation))
                return designation;

            string nature = (tech.NatureOfWork ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nature))
                return nature;

            string department = (tech.Department ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(department) ? "Technician" : department;
        }
        private string FormatTechnicianOption(Employee tech)
        {
            if (tech == null)
                return "Unassigned";

            string name = DisplayTechnicianName(tech);
            string role = EmployeeService.GetDispatchTechnicianRole(tech);
            return string.IsNullOrWhiteSpace(role) || string.Equals(role, "Technician", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + " - " + role;
        }
        private string DisplayTechnicianName(Employee tech, List<JobSummaryDto> assigned = null)
        {
            string name = First(tech?.Name, "");
            if (LooksLikeCompanyName(name))
                name = "";
            if (string.IsNullOrWhiteSpace(name) && assigned != null)
                name = assigned.Select(j => j.TechnicianName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && !LooksLikeCompanyName(n));
            if (string.IsNullOrWhiteSpace(name))
                name = !string.IsNullOrWhiteSpace(tech?.Designation) && !LooksLikeCompanyName(tech.Designation) && !string.Equals(tech.Designation, "Technician", StringComparison.OrdinalIgnoreCase)
                    ? tech.Designation
                    : "Technician " + Math.Abs(tech?.EmployeeID ?? 0).ToString();
            return name;
        }
        private bool LooksLikeCompanyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.ToUpperInvariant();
            return v.Contains(" PVT") || v.Contains(" LTD") || v.Contains(" LIMITED") || v.Contains(" CHEMICAL") || v.Contains(" ENTERPRISE") || v.Contains(" INDUSTRIES") || v.Contains("_CREATE") || v.Count(char.IsDigit) > 5;
        }
        private void UpdateTabStyles()
        {
            foreach (Button tab in _queueTabs)
            {
                bool active = _queueTabKeys[tab] == _activeQueue;
                tab.BackColor = active ? Lighten(Primary, 0.88f) : White;
                tab.ForeColor = active ? Primary : TextSecondary;
            }
        }
        private static bool Contains(string haystack, string needle) => !string.IsNullOrWhiteSpace(haystack) && !string.IsNullOrWhiteSpace(needle) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string First(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "AD";
            string[] parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("", parts.Take(2).Select(p => p.Substring(0, 1))).ToUpperInvariant();
        }
        private int SelectedId(ComboBox combo) => combo?.SelectedItem is ComboItem item ? item.Id : 0;
        private void SelectComboById(ComboBox combo, int id)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
                if (combo.Items[i] is ComboItem item && item.Id == id) { combo.SelectedIndex = i; return; }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
        private void SelectComboText(ComboBox combo, string text)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
                if (string.Equals(Convert.ToString(combo.Items[i]), text, StringComparison.OrdinalIgnoreCase)) { combo.SelectedIndex = i; return; }
        }

        private sealed class SiteMonitorRow
        {
            public string Site { get; set; }
            public string Region { get; set; }
            public int OpenJobs { get; set; }
            public int CriticalJobs { get; set; }
            public int SlaRisk { get; set; }
            public int CompletedJobs { get; set; }
            public decimal Revenue { get; set; }
            public int HealthScore { get; set; }
            public DateTime LastVisit { get; set; }
        }

        private sealed class SiteMonitorDetail
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public List<string> Columns { get; private set; } = new List<string>();
            public List<object[]> Rows { get; private set; } = new List<object[]>();
        }

        private sealed class SiteMonitorDetailDialog : ServoERP.Infrastructure.ServoFormBase
        {
            private readonly SiteMonitorDetail _detail;
            private readonly DataGridView _grid;

            public SiteMonitorDetailDialog(SiteMonitorDetail detail)
            {
                _detail = detail ?? new SiteMonitorDetail { Title = "Site Monitor Details" };
                Text = _detail.Title;
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(980, 620);
                Size = new Size(1180, 720);
                BackColor = Color.FromArgb(246, 248, 251);
                Padding = new Padding(18);

                Panel footer = BuildFooter();
                Panel header = BuildHeaderPanel();
                _grid = BuildGrid();
                Controls.Add(_grid);
                Controls.Add(header);
                Controls.Add(footer);
                BindGrid();
            }

            private Panel BuildHeaderPanel()
            {
                Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = BackColor, Padding = new Padding(0, 0, 0, 12) };
                Label title = new Label
                {
                    Text = _detail.Title,
                    Dock = DockStyle.Top,
                    Height = 34,
                    Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42),
                    AutoEllipsis = true
                };
                Label subtitle = new Label
                {
                    Text = _detail.Rows.Count.ToString("N0") + " full records behind this Site Monitor card",
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    AutoEllipsis = true
                };
                header.Controls.Add(subtitle);
                header.Controls.Add(title);
                return header;
            }

            private DataGridView BuildGrid()
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
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.None,
                    EnableHeadersVisualStyles = false
                };
                GridTheme.Apply(grid);
                return grid;
            }

            private Panel BuildFooter()
            {
                Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = BackColor, Padding = new Padding(0, 12, 0, 0) };
                TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Right, Width = 250, ColumnCount = 2, RowCount = 1, BackColor = BackColor };
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                Button export = DialogButton("Export CSV", Color.FromArgb(16, 185, 129));
                Button close = DialogButton("Close", Color.FromArgb(37, 99, 235));
                close.DialogResult = DialogResult.OK;
                export.Click += (s, e) => ExportGrid();
                actions.Controls.Add(export, 0, 0);
                actions.Controls.Add(close, 1, 0);
                footer.Controls.Add(actions);
                return footer;
            }

            private static Button DialogButton(string text, Color color)
            {
                Button button = new Button
                {
                    Text = text,
                    Dock = DockStyle.Fill,
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
            }

            private void ExportGrid()
            {
                using (SaveFileDialog dialog = new SaveFileDialog { FileName = SafeFileName(_detail.Title) + "_" + DateTime.Today.ToString("yyyyMMdd") + ".csv", Filter = "CSV|*.csv" })
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
                string name = string.IsNullOrWhiteSpace(value) ? "SiteMonitorDetails" : value;
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    name = name.Replace(invalid, '_');
                return name.Replace(' ', '_');
            }
        }

        private sealed class DispatchJobListModule : VirtualListModuleBase<JobSummaryDto>
        {
            public DispatchJobListModule()
            {
                ListGrid.ColumnHeadersHeight = 32;
                ListGrid.RowTemplate.Height = 34;
                ListGrid.BackgroundColor = Color.White;
            }

            protected override void BuildColumns(DataGridView grid)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "JobNumber", HeaderText = "Job", Width = 96 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", FillWeight = 42f, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Client", HeaderText = "Client", Width = 118 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "Priority", Width = 72 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Technician", HeaderText = "Technician", Width = 108 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sla", HeaderText = "SLA", Width = 88 });
            }

            protected override int GetRowId(JobSummaryDto item)
            {
                return item?.JobId ?? 0;
            }

            protected override object GetCellValue(JobSummaryDto item, string columnName)
            {
                if (item == null)
                    return string.Empty;

                switch (columnName)
                {
                    case "JobNumber":
                        return string.IsNullOrWhiteSpace(item.JobNumber) ? "JOB" : item.JobNumber;
                    case "Title":
                        return string.IsNullOrWhiteSpace(item.JobTitle) ? "Service job" : item.JobTitle;
                    case "Client":
                        return item.ClientName ?? string.Empty;
                    case "Priority":
                        return item.Priority ?? string.Empty;
                    case "Technician":
                        return string.IsNullOrWhiteSpace(item.TechnicianName) ? "Unassigned" : item.TechnicianName;
                    case "Sla":
                        return item.ScheduledDate == default(DateTime) ? "-" : item.ScheduledDate.ToString("dd MMM HH:mm");
                    default:
                        return string.Empty;
                }
            }

            protected override string BuildStatusText(int visibleCount, int totalCount)
            {
                return totalCount == 0 ? "No dispatch jobs." : visibleCount.ToString("N0") + " of " + totalCount.ToString("N0") + " jobs shown.";
            }
        }

        private void OpenDispatchFilters()
        {
            if (_cmbType != null && _cmbType.Enabled)
            {
                _cmbType.Focus();
                _cmbType.DroppedDown = true;
            }
        }

        private void SetTodayDispatchFilter()
        {
            _txtSearch.Text = DateTime.Today.ToString("dd/MM/yyyy");
            ApplyJobFilters();
            SetStatus("Filtered dispatch queue to today's date.", Info);
        }

        private void UpdateAutoRefreshState()
        {
            bool active = _chkAutoRefresh != null && _chkAutoRefresh.Checked;
            if (active) _autoRefreshTimer.Start();
            else _autoRefreshTimer.Stop();

            if (_autoRefreshPulse != null) _autoRefreshPulse.Visible = active;
            if (_btnRefresh != null)
            {
                _btnRefresh.Enabled = !active;
                _btnRefresh.Text = active ? "Auto (30s)" : "Refresh";
                _btnRefresh.ForeColor = active ? Muted : TextPrimary;
                _btnRefresh.Cursor = active ? Cursors.No : Cursors.Hand;
            }
        }

        private void UpdateViewAllJobsButton()
        {
            if (_btnViewAllJobs == null)
                return;

            bool hasJobs = _visibleJobs != null && _visibleJobs.Count > 0;
            _btnViewAllJobs.Enabled = hasJobs;
            _btnViewAllJobs.ForeColor = hasJobs ? TextPrimary : Muted;
            _btnViewAllJobs.Cursor = hasJobs ? Cursors.Hand : Cursors.No;
        }

        private void SetQuickActionsEnabled(bool enabled)
        {
            foreach (Button button in _jobActionButtons)
            {
                button.Enabled = enabled;
                button.ForeColor = enabled ? TextPrimary : Muted;
                button.Cursor = enabled ? Cursors.Hand : Cursors.No;
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (_lblStatus == null)
                return;

            _lblStatus.Text = text;
            _lblStatus.ForeColor = color;
            _lblStatus.Visible = true;
        }

        private string PromptText(string title, string initial)
        {
            using (Form dialog = ServoModalForm.Create(title, 420, 150))
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                input.Multiline = true;
                input.Text = initial ?? string.Empty;
                input.Location = new Point(12, 12);
                input.Size = new Size(396, 82);
                input.Font = new Font("Segoe UI", 9f);
                input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
                ok.Text = "Save";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(222, 108);
                ok.Size = new Size(88, 32);
                ok.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                ok.AutoEllipsis = true;
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(320, 108);
                cancel.Size = new Size(88, 32);
                cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                cancel.AutoEllipsis = true;
                dialog.Controls.AddRange(new Control[] { input, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(FindForm()) == DialogResult.OK ? input.Text : string.Empty;
            }
        }

        private class ComboItem
        {
            public int Id { get; }
            public string Text { get; }
            public ComboItem(int id, string text)
            {
                Id = id;
                Text = text;
            }
            public override string ToString() => Text;
        }

    }
}
