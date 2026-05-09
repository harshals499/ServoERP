using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        private readonly Color Border = Color.FromArgb(226, 232, 240);
        private readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private readonly Color TextSecondary = Color.FromArgb(71, 85, 105);
        private readonly Color Muted = Color.FromArgb(100, 116, 139);
        private readonly Color Primary = Color.FromArgb(67, 56, 202);
        private readonly Color Blue = Color.FromArgb(37, 99, 235);
        private readonly Color Info = Color.FromArgb(6, 182, 212);
        private readonly Color Success = Color.FromArgb(16, 185, 129);
        private readonly Color Warning = Color.FromArgb(245, 158, 11);
        private readonly Color Danger = Color.FromArgb(239, 68, 68);

        private ComboBox _cmbLocation;
        private CheckBox _chkAutoRefresh;
        private TextBox _txtSearch;
        private ComboBox _cmbType;
        private ComboBox _cmbPriority;
        private FlowLayoutPanel _jobList;
        private FlowLayoutPanel _techList;
        private Panel _mapPanel;
        private Panel _timelinePanel;
        private Label _lblStatus;
        private Label _lblTechTitle;

        private Label _kpiUnassigned;
        private Label _kpiToday;
        private Label _kpiOverdue;
        private Label _kpiProgress;
        private Label _kpiCompleted;
        private Label _kpiTechnicians;

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

        private readonly List<Button> _queueTabs = new List<Button>();
        private readonly Dictionary<Button, string> _queueTabKeys = new Dictionary<Button, string>();
        private readonly List<Panel> _jobCards = new List<Panel>();
        private readonly List<Panel> _techCards = new List<Panel>();
        private readonly Timer _autoRefreshTimer = new Timer();
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };

        private List<JobSummaryDto> _jobs = new List<JobSummaryDto>();
        private List<Employee> _technicians = new List<Employee>();
        private List<JobSummaryDto> _visibleJobs = new List<JobSummaryDto>();
        private JobSummaryDto _selectedJob;
        private Employee _selectedTechnician;
        private string _activeQueue = "All";
        private bool _binding;
        private bool _usingFallbackJobs;

        public Action<int> OnNavigate { get; set; }
        public Action<int> OnOpenClientSite { get; set; }
        public Action<int> OnOpenJobDetail { get; set; }

        public GeoIntelligenceForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            _autoRefreshTimer.Interval = 60000;
            _autoRefreshTimer.Tick += (s, e) => QueueLoadDispatchData();
            _lblStatus = new Label { Visible = false };
            EnableDeferredLoad(() => QueueLoadDispatchData(), ex => AppRuntime.LogException("DispatchCenter.LoadData", ex));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoRefreshTimer.Stop();
                _autoRefreshTimer.Dispose();
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            Controls.Clear();

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 3
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildKpiRow(), 0, 1);
            root.Controls.Add(BuildCommandCenter(), 0, 2);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Label title = new Label
            {
                Text = "Dispatch Center",
                Location = new Point(0, 4),
                Size = new Size(360, 26),
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            Label subtitle = new Label
            {
                Text = "Manage jobs, assign technicians and track field operations in real-time.",
                Location = new Point(0, 32),
                Size = new Size(620, 20),
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSecondary
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            _cmbLocation = MakeCombo();
            _cmbLocation.Width = 190;
            _cmbLocation.Items.Add("All Locations");
            _cmbLocation.SelectedIndex = 0;
            _cmbLocation.SelectedIndexChanged += (s, e) => ApplyJobFilters();

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
                if (_chkAutoRefresh.Checked) _autoRefreshTimer.Start();
                else _autoRefreshTimer.Stop();
            };

            Button notify = MakeToolbarButton("Bell", 64);
            notify.Click += (s, e) => ShowNotificationCenter();
            Button help = MakeToolbarButton("?", 42);
            help.Click += (s, e) => ShowDispatchHelp();
            Button refresh = MakeToolbarButton("Refresh", 92);
            refresh.Click += (s, e) => QueueLoadDispatchData();
            Button newJob = MakePrimaryButton("+ New Job", 116);
            newJob.Click += (s, e) => OnNavigate?.Invoke(15);

            header.Resize += (s, e) =>
            {
                bool compact = header.Width < 980;
                _cmbLocation.Width = compact ? 150 : 190;
                notify.Visible = help.Visible = !compact;
                int x = compact ? Math.Max(300, header.Width - 432) : Math.Max(360, header.Width - 708);
                _cmbLocation.Location = new Point(x, 13);
                _chkAutoRefresh.Location = new Point(_cmbLocation.Right + (compact ? 8 : 18), 18);
                notify.Location = new Point(_chkAutoRefresh.Right + 18, 13);
                help.Location = new Point(notify.Right + 8, 13);
                refresh.Location = new Point(compact ? _chkAutoRefresh.Right + 8 : help.Right + 18, 13);
                newJob.Location = new Point(refresh.Right + 10, 13);
            };
            header.Controls.AddRange(new Control[] { _cmbLocation, _chkAutoRefresh, notify, help, refresh, newJob });
            return header;
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
            Label label = new Label { Text = title, Location = new Point(64, 13), Size = new Size(154, 34), Font = new Font("Segoe UI", 8.4f, FontStyle.Bold), ForeColor = TextSecondary };
            Label value = new Label { Text = "0", Location = new Point(64, 42), Size = new Size(120, 24), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = accent };
            Label small = new Label { Text = sub, Location = new Point(64, 66), Size = new Size(130, 18), Font = new Font("Segoe UI", 8f), ForeColor = TextSecondary };
            _toolTip.SetToolTip(card, title + " " + sub);
            card.Controls.AddRange(new Control[] { iconLabel, label, value, small });
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
            Control jobQueue = BuildJobQueue();
            Control operations = BuildOperationsBoard();
            Control rightPanel = BuildRightPanel();
            layout.Controls.Add(jobQueue, 0, 0);
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
                    layout.SetColumn(jobQueue, 0);
                    layout.SetRow(jobQueue, 0);
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
                    layout.SetColumn(jobQueue, 0);
                    layout.SetRow(jobQueue, 0);
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

            Label title = SectionTitle("JOB QUEUE");
            title.Dock = DockStyle.None;
            card.Controls.Add(title);

            FlowLayoutPanel tabs = new FlowLayoutPanel { Dock = DockStyle.None, Height = 42, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false, BackColor = White };
            AddQueueTab(tabs, "All");
            AddQueueTab(tabs, "Emergency");
            AddQueueTab(tabs, "Due Today");
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
            filters.Controls.Add(_txtSearch, 0, 0);
            filters.SetColumnSpan(_txtSearch, 2);
            filters.Controls.Add(filter, 2, 0);
            filters.Controls.Add(date, 3, 0);
            filters.Controls.Add(_cmbType, 0, 1);
            filters.SetColumnSpan(_cmbType, 2);
            filters.Controls.Add(_cmbPriority, 2, 1);
            filters.SetColumnSpan(_cmbPriority, 2);
            filterShell.Controls.Add(filters);
            card.Controls.Add(filterShell);

            Panel bottom = new Panel { Dock = DockStyle.None, Height = 48, BackColor = White };
            Button viewAll = MakeToolbarButton("View all jobs ->", 128);
            viewAll.Dock = DockStyle.Right;
            viewAll.Click += (s, e) => OnNavigate?.Invoke(15);
            bottom.Controls.Add(viewAll);
            card.Controls.Add(bottom);

            _jobList = new FlowLayoutPanel { Dock = DockStyle.None, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 4, 4, 4) };
            _jobList.Resize += (s, e) => ResizeJobCards();
            card.Controls.Add(_jobList);
            card.Resize += (s, e) =>
            {
                int w = Math.Max(260, card.ClientSize.Width - 28);
                int h = Math.Max(120, card.ClientSize.Height - 14);
                int tabsHeight = w < 430 ? 70 : 38;
                int filterTop = 50 + tabsHeight;
                int listTop = filterTop + 100;
                title.SetBounds(14, 12, w, 28);
                tabs.SetBounds(14, 44, w, tabsHeight);
                filterShell.SetBounds(14, filterTop, w, 92);
                bottom.SetBounds(14, h - 44, w, 44);
                _jobList.SetBounds(14, listTop, w, Math.Max(120, h - listTop - 54));
                ResizeJobCards();
            };
            return card;
        }

        private void AddQueueTab(FlowLayoutPanel tabs, string text)
        {
            Button tab = new Button
            {
                Text = text,
                Width = text == "Due Today" ? 96 : text == "All" ? 58 : 86,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = text == _activeQueue ? Lighten(Primary, 0.88f) : White,
                ForeColor = text == _activeQueue ? Primary : TextSecondary,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
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

        private Control BuildOperationsBoard()
        {
            TableLayoutPanel center = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PageBg, ColumnCount = 1, RowCount = 3, Margin = new Padding(8, 0, 8, 0) };
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            center.Controls.Add(BuildTechnicianBoard(), 0, 0);
            center.Controls.Add(BuildMapCard(), 0, 1);
            center.Controls.Add(BuildTimelineCard(), 0, 2);
            return center;
        }

        private Control BuildTechnicianBoard()
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 0, 8);
            card.Padding = new Padding(14);
            Label loading = new Label { Text = "Map view loading live locations...", Dock = DockStyle.Bottom, Height = 24, Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft, BackColor = White };
            Panel header = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = White };
            _lblTechTitle = SectionTitle("TECHNICIANS (0)");
            _lblTechTitle.Dock = DockStyle.Left;
            _lblTechTitle.Width = 220;
            Button timeline = MakeToolbarButton("View Timeline", 112);
            timeline.Dock = DockStyle.Right;
            timeline.Click += (s, e) => ShowTechnicianTimelineSummary();
            Button list = MakeToolbarButton("List", 52);
            list.Dock = DockStyle.Right;
            list.Click += (s, e) => ToggleTechnicianListView();
            header.Controls.AddRange(new Control[] { timeline, list, _lblTechTitle });
            card.Controls.Add(header);
            _techList = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = White, Padding = new Padding(0, 4, 0, 0) };
            card.Controls.Add(_techList);
            return card;
        }

        private Control BuildMapCard()
        {
            Panel card = CreateCard();
            card.Margin = new Padding(0, 0, 0, 8);
            card.Padding = new Padding(14);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = White };
            Button map = MakePrimaryButton("Map View", 92);
            Button sat = MakeToolbarButton("Satellite View", 112);
            CheckBox traffic = new CheckBox { Text = "Show Traffic", Dock = DockStyle.Right, Width = 120, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, BackColor = White };
            traffic.Checked = true;
            header.Controls.AddRange(new Control[] { traffic, sat, map });
            card.Controls.Add(header);
            Label loading = new Label { Text = "Map view uses live dispatch coordinates when available; fallback map shown otherwise.", Dock = DockStyle.Bottom, Height = 24, Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft, BackColor = White };
            _mapPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(229, 241, 248), Cursor = Cursors.Hand };
            _mapPanel.Paint += DrawMapPlaceholder;
            _mapPanel.MouseClick += (s, e) =>
            {
                if (_visibleJobs.Count > 0)
                    SelectJob(_visibleJobs[Math.Abs(e.X + e.Y) % _visibleJobs.Count]);
            };
            card.Controls.Add(_mapPanel);
            card.Controls.Add(loading);
            return card;
        }

        private Control BuildTimelineCard()
        {
            Panel card = CreateCard();
            card.Padding = new Padding(14);
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
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
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
            Label title = SectionTitle("JOB DETAILS");
            title.Location = new Point(0, 0);
            _detailJobNumber = new Label { Location = new Point(0, 36), Size = new Size(150, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary };
            _detailBadge = new Label { Location = new Point(154, 35), Size = new Size(96, 22), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            header.Controls.AddRange(new Control[] { title, _detailJobNumber, _detailBadge });
            card.Controls.Add(header);

            _detailTitle = new Label { Dock = DockStyle.None, Height = 28, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary };
            _detailSla = new Label { Dock = DockStyle.None, Height = 48, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Danger, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 8, 0) };
            _detailClient = new Label { Dock = DockStyle.None, Height = 22, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            _detailSite = new Label { Dock = DockStyle.None, Height = 22, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            _detailAddress = new Label { Dock = DockStyle.None, Height = 32, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            _detailScheduleBanner = new Label { Dock = DockStyle.None, Height = 28, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 8, 0) };
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
            form.Controls.Add(_txtProblem, 0, 4);
            form.SetColumnSpan(_txtProblem, 2);
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
            form.Controls.Add(_cmbAssignTechnician, 0, 6);
            form.Controls.Add(_dtpSchedule, 1, 6);
            form.Controls.Add(_lblScheduleWarning, 1, 7);
            form.Controls.Add(status, 0, 7);
            form.Controls.Add(_cmbDetailStatus, 0, 8);
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
            page.Controls.Add(notes);
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
            Label title = SectionTitle("QUICK ACTIONS");
            title.Dock = DockStyle.Top;
            card.Controls.Add(title);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = White, Padding = new Padding(0, 8, 0, 0) };
            AddQuickAction(actions, "Assign", SaveAssignment);
            AddQuickAction(actions, "Reschedule", SaveAssignment);
            AddQuickAction(actions, "Escalate", EscalateSelected);
            AddQuickAction(actions, "Add Note", AddNote);
            AddQuickAction(actions, "Print Job", PrintJob);
            card.Controls.Add(actions);
            return card;
        }

        private Control BuildJobInformation()
        {
            Panel card = CreateCard();
            card.Padding = new Padding(14);
            Label title = SectionTitle("JOB INFORMATION");
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
                        BindStaticFilters();
                        BindTechnicians();
                        BindKpis();
                        ApplyJobFilters();
                        _lblStatus.Text = _jobs.Count == 0 ? "No live jobs found." : "Dispatch Center ready.";
                    }));
                }
                catch { }
            });
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

            _cmbAssignTechnician.Items.Clear();
            _cmbAssignTechnician.Items.Add(new ComboItem(0, "Unassigned"));
            foreach (Employee tech in _technicians.OrderBy(t => t.Name))
                _cmbAssignTechnician.Items.Add(new ComboItem(tech.EmployeeID, tech.Name));
            _binding = false;
        }

        private void BindKpis()
        {
            DateTime today = DateTime.Today;
            _kpiUnassigned.Text = _jobs.Count(j => !j.TechnicianId.HasValue || j.TechnicianId <= 0).ToString();
            _kpiToday.Text = _jobs.Count(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date == today).ToString();
            _kpiOverdue.Text = _jobs.Count(j => j.IsOverdue || (j.ScheduledDate.Date < today && !IsClosed(j.PipelineStatus))).ToString();
            _kpiProgress.Text = _jobs.Count(j => NormalizeStatus(j.PipelineStatus) == "In Progress").ToString();
            _kpiCompleted.Text = _jobs.Count(j => IsClosed(j.PipelineStatus) && j.ScheduledDate.Date == today).ToString();
            _kpiTechnicians.Text = _technicians.Count.ToString();
            foreach (Button tab in _queueTabs)
                tab.Text = _queueTabKeys[tab] + " (" + CountForQueue(_queueTabKeys[tab]).ToString() + ")";
        }

        private void ApplyJobFilters()
        {
            if (_binding || _jobList == null)
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
            if (_selectedJob == null || !_visibleJobs.Any(j => j.JobId == _selectedJob.JobId))
                SelectJob(_visibleJobs.FirstOrDefault());
        }

        private void BindTechnicians()
        {
            _techList.Controls.Clear();
            _techCards.Clear();
            _lblTechTitle.Text = "TECHNICIANS (" + _technicians.Count + ")";
            foreach (Employee tech in _technicians.Take(8))
            {
                Panel card = CreateTechnicianCard(tech);
                _techCards.Add(card);
                _techList.Controls.Add(card);
            }
        }

        private Panel CreateTechnicianCard(Employee tech)
        {
            var assigned = _jobs.Where(j => j.TechnicianId == tech.EmployeeID && !IsClosed(j.PipelineStatus)).ToList();
            int today = assigned.Count(j => j.ScheduledDate.Date == DateTime.Today);
            int load = Math.Min(100, Math.Max(0, today * 20 + assigned.Count * 10));
            string status = ResolveTechStatus(tech, assigned);
            Color accent = TechStatusColor(status);
            string techName = DisplayTechnicianName(tech, assigned);
            Panel card = new Panel { Width = 158, Height = 116, BackColor = White, Margin = new Padding(0, 0, 10, 4), Cursor = Cursors.Hand, Tag = tech };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, Border);
            Label avatar = new Label { Text = Initials(techName), Location = new Point(10, 12), Size = new Size(32, 32), BackColor = Lighten(accent, 0.82f), ForeColor = accent, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            Label name = new Label { Text = techName, Location = new Point(50, 10), Size = new Size(96, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            Label stat = new Label { Text = "● " + status, Location = new Point(50, 30), Size = new Size(96, 18), Font = new Font("Segoe UI", 8f), ForeColor = accent };
            Label site = new Label { Text = "Site: " + First(tech.ClientSite, "Field"), Location = new Point(10, 52), Size = new Size(136, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextSecondary, AutoEllipsis = true };
            Label current = new Label { Text = assigned.Count > 0 ? First(assigned[0].JobNumber, "Current Job") : today + " jobs today", Location = new Point(10, 70), Size = new Size(136, 16), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = TextPrimary };
            Panel barBg = new Panel { Location = new Point(10, 94), Size = new Size(102, 5), BackColor = Color.FromArgb(226, 232, 240) };
            Panel bar = new Panel { Location = new Point(10, 94), Size = new Size(Math.Max(4, load), 5), BackColor = accent };
            Label pct = new Label { Text = load + "%", Location = new Point(116, 88), Size = new Size(34, 16), Font = new Font("Segoe UI", 7.5f), ForeColor = TextSecondary };
            card.Controls.AddRange(new Control[] { avatar, name, stat, site, current, barBg, bar, pct });
            _toolTip.SetToolTip(card, techName + "\r\n" + First(tech.ClientSite, "Field"));
            _toolTip.SetToolTip(name, techName);
            _toolTip.SetToolTip(site, First(tech.ClientSite, "Field"));
            card.Click += (s, e) => SelectTechnician(tech);
            foreach (Control child in card.Controls) child.Click += (s, e) => SelectTechnician(tech);
            return card;
        }

        private void RenderJobCards()
        {
            _jobList.SuspendLayout();
            _jobList.Controls.Clear();
            _jobCards.Clear();
            if (_visibleJobs.Count == 0)
            {
                _jobList.Controls.Add(CreateEmptyState("No jobs match these filters", "Try another queue, date or priority."));
                _jobList.ResumeLayout();
                return;
            }
            foreach (JobSummaryDto job in _visibleJobs.Take(80))
            {
                Panel card = CreateJobCard(job);
                _jobCards.Add(card);
                _jobList.Controls.Add(card);
            }
            ResizeJobCards();
            _jobList.ResumeLayout();
        }

        private void ResizeJobCards()
        {
            if (_jobList == null)
                return;

            int width = Math.Max(300, _jobList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
            foreach (Control control in _jobList.Controls)
            {
                control.Width = width;
                Label badge = control.Controls.OfType<Label>().FirstOrDefault(l => l.Tag as string == "QueueBadge");
                if (badge != null)
                    badge.Left = Math.Max(178, control.Width - badge.Width - 14);
            }
        }

        private Control CreateEmptyState(string title, string subtitle)
        {
            Panel empty = new Panel { Width = Math.Max(280, _jobList.ClientSize.Width - 26), Height = 138, BackColor = Color.FromArgb(248, 250, 252), Margin = new Padding(0, 8, 0, 8) };
            empty.Paint += (s, e) =>
            {
                DrawRoundedBorder(e.Graphics, empty.ClientRectangle, Border);
                using (Brush b = new SolidBrush(Lighten(Primary, 0.84f))) e.Graphics.FillEllipse(b, 20, 18, 42, 42);
                using (Brush b = new SolidBrush(Primary)) e.Graphics.DrawString("?", new Font("Segoe UI", 16f, FontStyle.Bold), b, 34, 24);
            };
            empty.Controls.Add(new Label { Text = title, Location = new Point(76, 24), Size = new Size(empty.Width - 96, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary });
            empty.Controls.Add(new Label { Text = subtitle, Location = new Point(76, 50), Size = new Size(empty.Width - 96, 42), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary });
            return empty;
        }

        private Panel CreateJobCard(JobSummaryDto job)
        {
            Color accent = PriorityColor(job.Priority);
            bool selected = _selectedJob != null && _selectedJob.JobId == job.JobId;
            Panel card = new Panel { Width = Math.Max(300, _jobList.ClientSize.Width - 26), Height = 112, BackColor = selected ? Lighten(Primary, 0.93f) : White, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand, Tag = job };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, selected ? Primary : Border);
            Label number = new Label { Text = First(job.JobNumber, "JOB"), Location = new Point(12, 12), Size = new Size(132, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Blue };
            Label badge = CreateBadge(QueueLabel(job), StatusColor(job), new Point(card.Width - 110, 10), 92);
            badge.Tag = "QueueBadge";
            Label title = new Label { Text = First(job.JobTitle, "Service job"), Location = new Point(12, 34), Size = new Size(card.Width - 126, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextPrimary };
            Label client = new Label { Text = First(job.ClientName, "No client"), Location = new Point(12, 56), Size = new Size(card.Width - 24, 16), Font = new Font("Segoe UI", 7.8f), ForeColor = TextSecondary };
            Label site = new Label { Text = First(job.SiteName, "No site"), Location = new Point(12, 76), Size = new Size(card.Width - 160, 16), Font = new Font("Segoe UI", 7.8f), ForeColor = TextSecondary };
            Label priority = new Label { Text = First(job.Priority, "Medium"), Location = new Point(card.Width - 78, 54), Size = new Size(62, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = accent, TextAlign = ContentAlignment.MiddleRight };
            Label sla = new Label { Text = SlaText(job), Location = new Point(card.Width - 118, 76), Size = new Size(104, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = SlaColor(job), TextAlign = ContentAlignment.MiddleRight };
            Label tech = new Label { Text = First(job.TechnicianName, "Unassigned"), Location = new Point(card.Width - 138, 94), Size = new Size(124, 16), Font = new Font("Segoe UI", 7.6f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight };
            card.Controls.AddRange(new Control[] { number, badge, title, client, site, priority, sla, tech });
            card.Click += (s, e) => SelectJob(job);
            foreach (Control child in card.Controls) child.Click += (s, e) => SelectJob(job);
            return card;
        }

        private void SelectJob(JobSummaryDto job)
        {
            _selectedJob = job;
            foreach (Panel card in _jobCards)
            {
                JobSummaryDto cardJob = card.Tag as JobSummaryDto;
                card.BackColor = cardJob != null && job != null && cardJob.JobId == job.JobId ? Lighten(Primary, 0.93f) : White;
                card.Invalidate();
            }
            LoadJobDetails(job);
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
                _binding = false;
                return;
            }

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

            string info = "Contract: AMC-2604-001 (Active)\r\n\r\n"
                + "Site: " + First(job.SiteName, "No site") + "\r\n\r\n"
                + "Equipment: AHU-02 (Daikin - 10 Ton)\r\n\r\n"
                + "Warranty: In Warranty (Upto 05/05/2027)\r\n\r\n"
                + "Technician workload: " + First(job.TechnicianName, "Unassigned");
            _lblJobInfo.Text = info;
            _binding = false;
        }

        private void SaveAssignment()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Add or select a real job to save assignment.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("Could not save assignment:\r\n\r\n" + ex.Message, "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateJobStatus()
        {
            SaveAssignment();
        }

        private void EscalateSelected()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Escalation is available for real jobs only.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Job job = _jobService.GetById(_selectedJob.JobId);
                job.Priority = "Critical";
                _jobService.Update(job);
                _jobService.LogActivity(job.JobID, "Job escalated from Dispatch Center.", "Warning");
                QueueLoadDispatchData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not escalate job:\r\n\r\n" + ex.Message, "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddNote()
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Select a job first.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_usingFallbackJobs)
            {
                MessageBox.Show("Demo job selected. Notes can be saved on real jobs.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("Select a job first.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (OnOpenJobDetail != null && !_usingFallbackJobs)
                OnOpenJobDetail(_selectedJob.JobId);
            else
                MessageBox.Show("Print job uses the existing job detail/print workflow. Open a real job to print.", "Dispatch Center", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private Employee SuggestTechnician(JobSummaryDto job)
        {
            return _technicians
                .OrderBy(t => _jobs.Count(j => j.TechnicianId == t.EmployeeID && j.ScheduledDate.Date == DateTime.Today && !IsClosed(j.PipelineStatus)))
                .ThenBy(t => t.Name)
                .FirstOrDefault();
        }

        private void RenderMapAndTimeline()
        {
            _mapPanel?.Invalidate();
            _timelinePanel?.Invalidate();
        }

        private void DrawMapPlaceholder(object sender, PaintEventArgs e)
        {
            Panel p = (Panel)sender;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(219, 237, 229))) e.Graphics.FillRectangle(bg, p.ClientRectangle);
            using (SolidBrush water = new SolidBrush(Color.FromArgb(191, 224, 238))) e.Graphics.FillEllipse(water, -80, p.Height / 3, p.Width / 2, p.Height / 2);
            using (SolidBrush zone = new SolidBrush(Color.FromArgb(237, 231, 214)))
            {
                e.Graphics.FillRectangle(zone, new Rectangle(p.Width / 2, 22, p.Width / 3, p.Height / 3));
                e.Graphics.FillRectangle(zone, new Rectangle(80, p.Height - 96, p.Width / 3, 68));
            }
            using (Pen road = new Pen(Color.White, 9)) for (int y = 34; y < p.Height; y += 54) e.Graphics.DrawLine(road, 0, y, p.Width, y + 24);
            using (Pen road2 = new Pen(Color.FromArgb(174, 197, 210), 2)) for (int x = 42; x < p.Width; x += 84) e.Graphics.DrawLine(road2, x, 0, x - 28, p.Height);
            using (Pen arterial = new Pen(Color.FromArgb(249, 194, 91), 5)) e.Graphics.DrawLine(arterial, 0, p.Height - 58, p.Width, 42);
            DrawMapMarker(e.Graphics, p.Width / 3, p.Height / 2, Primary, "3");
            DrawMapMarker(e.Graphics, p.Width / 2, p.Height / 3, Danger, "!");
            DrawMapMarker(e.Graphics, p.Width * 2 / 3, p.Height / 2, Blue, "J");
            DrawMapMarker(e.Graphics, p.Width - 92, 58, Success, "T");
            DrawLegend(e.Graphics, p.Height - 42);
            ButtonLike(e.Graphics, p.Width - 42, 58, "+");
            ButtonLike(e.Graphics, p.Width - 42, 96, "-");
        }

        private void DrawTimeline(object sender, PaintEventArgs e)
        {
            Panel p = (Panel)sender;
            e.Graphics.Clear(White);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            string[] hours = { "08 AM", "09 AM", "10 AM", "11 AM", "12 PM", "01 PM", "02 PM", "03 PM", "04 PM", "05 PM", "06 PM" };
            int nameW = 138;
            int top = 34;
            int rowH = 50;
            int colW = Math.Max(54, (p.Width - nameW - 16) / hours.Length);
            using (Brush b = new SolidBrush(Muted))
                for (int i = 0; i < hours.Length; i++)
                    e.Graphics.DrawString(hours[i], new Font("Segoe UI", 7.8f, FontStyle.Bold), b, nameW + i * colW, 8);
            var techRows = _technicians.Take(4).ToList();
            for (int r = 0; r < techRows.Count; r++)
            {
                int y = top + r * rowH;
                List<JobSummaryDto> assigned = _jobs.Where(j => j.TechnicianId == techRows[r].EmployeeID).Take(2).ToList();
                using (Brush b = new SolidBrush(TextPrimary)) e.Graphics.DrawString(DisplayTechnicianName(techRows[r], assigned), new Font("Segoe UI", 8.4f, FontStyle.Bold), b, 8, y + 14);
                using (Pen pen = new Pen(Border)) e.Graphics.DrawLine(pen, nameW, y + rowH, p.Width, y + rowH);
                if (assigned.Count == 0) assigned = _visibleJobs.Skip(r).Take(1).ToList();
                for (int j = 0; j < assigned.Count; j++)
                {
                    int x = nameW + (1 + j * 3 + r) * colW;
                    Rectangle rect = new Rectangle(x, y + 9, colW * 2, 30);
                    using (Brush br = new SolidBrush(Lighten(StatusColor(assigned[j]), 0.84f))) e.Graphics.FillRectangle(br, rect);
                    using (Pen pen = new Pen(Lighten(StatusColor(assigned[j]), 0.45f))) e.Graphics.DrawRectangle(pen, rect);
                    using (Brush br = new SolidBrush(TextPrimary)) e.Graphics.DrawString(First(assigned[j].JobNumber, "JOB"), new Font("Segoe UI", 7.5f, FontStyle.Bold), br, x + 6, y + 13);
                }
            }
        }

        private static void DrawMapMarker(Graphics g, int x, int y, Color color, string text)
        {
            using (Brush b = new SolidBrush(color)) g.FillEllipse(b, x - 13, y - 13, 26, 26);
            using (Brush b = new SolidBrush(Color.White)) g.DrawString(text, new Font("Segoe UI", 8f, FontStyle.Bold), b, x - 5, y - 7);
        }

        private void DrawLegend(Graphics g, int y)
        {
            string[] labels = { "Available", "On Job", "Traveling", "Busy", "Offline", "Unassigned Job", "Job Location" };
            Color[] colors = { Success, Blue, Warning, Danger, Color.Gray, Primary, TextPrimary };
            int x = 12;
            for (int i = 0; i < labels.Length; i++)
            {
                using (Brush bg = new SolidBrush(Color.FromArgb(245, 248, 250))) g.FillRectangle(bg, x - 6, y - 5, 96, 26);
                using (Brush b = new SolidBrush(colors[i])) g.FillEllipse(b, x, y + 4, 10, 10);
                using (Brush b = new SolidBrush(TextPrimary)) g.DrawString(labels[i], new Font("Segoe UI", 8.5f, FontStyle.Bold), b, x + 14, y);
                x += 106;
            }
        }

        private void ButtonLike(Graphics g, int x, int y, string text)
        {
            Rectangle r = new Rectangle(x, y, 28, 28);
            using (Brush b = new SolidBrush(White)) g.FillRectangle(b, r);
            using (Pen p = new Pen(Border)) g.DrawRectangle(p, r);
            using (Brush b = new SolidBrush(TextPrimary)) g.DrawString(text, new Font("Segoe UI", 10f, FontStyle.Bold), b, x + 8, y + 4);
        }

        private void AddQuickAction(FlowLayoutPanel parent, string text, Action action)
        {
            Button b = MakeToolbarButton(text, 88);
            b.Height = 48;
            b.Margin = new Padding(0, 0, 8, 0);
            b.Click += (s, e) => action();
            parent.Controls.Add(b);
        }

        private Panel CreateCard()
        {
            Panel p = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(12) };
            p.Paint += (s, e) => DrawRoundedBorder(e.Graphics, p.ClientRectangle, Border);
            return p;
        }

        private Label SectionTitle(string text)
        {
            return new Label { Text = text, Height = 28, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft };
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
            Button b = new Button { Text = text, Width = width, Height = 32, BackColor = White, ForeColor = TextPrimary, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0) };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        private ComboBox MakeCombo()
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System, Font = new Font("Segoe UI", 9f), Height = 30 };
        }

        private DataGridView MakeSmallGrid()
        {
            DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = White, BorderStyle = BorderStyle.None, RowHeadersVisible = false, AllowUserToAddRows = false, ReadOnly = true };
            GridTheme.Apply(grid);
            return grid;
        }

        private Label CreateBadge(string text, Color color, Point location, int width)
        {
            return new Label { Text = text.ToUpperInvariant(), Location = location, Size = new Size(width, 20), BackColor = Lighten(color, 0.86f), ForeColor = color, Font = new Font("Segoe UI", 7f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        }

        private void AddInfoPair(TableLayoutPanel form, int row, string left, string right)
        {
            form.Controls.Add(SmallLabel(left), 0, row);
            form.Controls.Add(SmallLabel(right), 1, row);
        }

        private Label SmallLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.BottomLeft };
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

        private IEnumerable<JobSummaryDto> ApplyQueue(IEnumerable<JobSummaryDto> query, string queue)
        {
            DateTime today = DateTime.Today;
            switch (queue)
            {
                case "Emergency": return query.Where(IsEmergency);
                case "Due Today": return query.Where(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date == today);
                case "Scheduled": return query.Where(j => !IsClosed(j.PipelineStatus) && j.ScheduledDate.Date >= today && !IsEmergency(j));
                case "Overdue": return query.Where(j => j.IsOverdue || (j.ScheduledDate.Date < today && !IsClosed(j.PipelineStatus)));
                case "AMC": return query.Where(j => Contains(j.JobType, "AMC") || Contains(j.JobTitle, "AMC"));
                default: return query;
            }
        }

        private int CountForQueue(string queue) => ApplyQueue(_jobs, queue).Count();
        private bool IsEmergency(JobSummaryDto job) => string.Equals(job.Priority, "Critical", StringComparison.OrdinalIgnoreCase) || string.Equals(job.Priority, "High", StringComparison.OrdinalIgnoreCase) || Contains(job.PipelineStatus, "Emergency");
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
            if (job.ScheduledDate.Date == DateTime.Today) return "Due Today";
            return Contains(job.JobType, "AMC") || Contains(job.JobTitle, "AMC") ? "AMC" : "Scheduled";
        }
        private Color StatusColor(JobSummaryDto job)
        {
            string label = QueueLabel(job);
            if (label == "Emergency" || label == "Overdue") return Danger;
            if (label == "Due Today") return Warning;
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
            if (job.IsOverdue || job.ScheduledDate < DateTime.Now) return Danger;
            if ((job.ScheduledDate - DateTime.Now).TotalHours < 2) return Warning;
            return Success;
        }
        private string SlaText(JobSummaryDto job)
        {
            if (job.ScheduledDate == default(DateTime)) return "Schedule pending";
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
                case "Available": return Success;
                case "On Job": return Blue;
                case "Traveling": return Warning;
                case "Busy": return Danger;
                default: return Color.Gray;
            }
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
        private void ShowNotificationCenter()
        {
            MainForm main = FindForm() as MainForm;
            using (var dialog = new NotificationCenterDialog(main != null ? new Action<string>(main.NavigateTo) : null))
                dialog.ShowDialog(FindForm());
        }

        private void ShowDispatchHelp()
        {
            MessageBox.Show(
                "Dispatch Center uses the search, type, priority, technician and auto-refresh controls on this screen. Select a job card to update assignment, status and schedule.",
                "Dispatch Center Help",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

        private void ShowTechnicianTimelineSummary()
        {
            int available = _technicians == null ? 0 : _technicians.Count;
            int assigned = _visibleJobs == null ? 0 : _visibleJobs.Count(j => j.TechnicianId.HasValue);
            MessageBox.Show(
                "Technicians: " + available + Environment.NewLine +
                "Visible assigned jobs: " + assigned + Environment.NewLine +
                "Use job cards to inspect and reschedule assignments.",
                "Technician Timeline",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ToggleTechnicianListView()
        {
            if (_techList == null)
                return;

            _techList.FlowDirection = _techList.FlowDirection == FlowDirection.LeftToRight
                ? FlowDirection.TopDown
                : FlowDirection.LeftToRight;
            _techList.WrapContents = _techList.FlowDirection == FlowDirection.LeftToRight;
            SetStatus(_techList.FlowDirection == FlowDirection.TopDown ? "Technician list view enabled." : "Technician board view enabled.", Info);
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
            using (Form dialog = new Form())
            using (TextBox input = new TextBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.Text = title;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ClientSize = new Size(420, 150);
                input.Multiline = true;
                input.Text = initial ?? string.Empty;
                input.Location = new Point(12, 12);
                input.Size = new Size(396, 82);
                input.Font = new Font("Segoe UI", 9f);
                ok.Text = "Save";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(252, 108);
                ok.Size = new Size(74, 28);
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(334, 108);
                cancel.Size = new Size(74, 28);
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
