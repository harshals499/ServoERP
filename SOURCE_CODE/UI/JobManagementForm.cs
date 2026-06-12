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
    public class JobManagementForm : DeferredPageControl
    {
        private static readonly Color White = ColorTranslator.FromHtml("#FFFFFF");
        private static readonly Color PageBg = DS.BgPage;
        private static readonly Color Surface = DS.Slate50;
        private static readonly Color Border = DS.Border;
        private static readonly Color BorderLight = DS.Slate100;
        private static readonly Color TextPrimary = DS.Slate900;
        private static readonly Color TextSecondary = DS.Slate600;
        private static readonly Color TextHint = DS.Slate400;
        private static readonly Color Teal = DS.Teal600;
        private static readonly Color TealDark = DS.Darken(DS.Teal600, 0.12f);
        private static readonly Color TealLightBg = DS.Teal50;
        private static readonly Color Amber = DS.Amber500;
        private static readonly Color AmberDark = Color.FromArgb(180, 83, 9);
        private static readonly Color AmberLightBg = DS.Amber50;
        private static readonly Color Red = DS.Red500;
        private static readonly Color RedDark = DS.Red600;
        private static readonly Color RedLightBg = DS.Red50;
        private static readonly Color Blue = DS.Primary600;
        private static readonly Color BlueDark = DS.Primary700;
        private static readonly Color BlueLightBg = DS.Indigo50;
        private static readonly Color Purple = DS.Indigo600;
        private static readonly Color PurpleLight = DS.Indigo50;

        private readonly JobService _jobSvc = new JobService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly ContractService _contractSvc = new ContractService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly SettingsService _settingsSvc = new SettingsService();

        private SplitContainer _split;
        private FlowLayoutPanel _jobListFlow;
        private FlowLayoutPanel _chipFlow;
        private TextBox _txtSearch;
        private Button _btnSearchClear;
        private Label _lblListStatus;

        private Panel _rightPanel;
        private Panel _topBar;
        private Panel _pipelineBar;
        private Panel _detailScroll;
        private Panel _cardsHost;

        private Label _lblJobNumber;
        private Label _lblJobTitle;
        private Label _lblMeta;
        private Button _btnCloseJob;
        private Button _btnPrintReport;
        private Button _btnSave;

        private readonly Dictionary<string, Panel> _pipelineStepPanels = new Dictionary<string, Panel>();
        private readonly List<string> _pipelineSteps = new List<string> { "Created", "Assigned", "InProgress", "ChecklistDone", "Closed", "Invoiced" };

        private Panel _cardJobDetails;
        private Panel _cardTechnician;
        private Panel _cardChecklist;
        private Panel _cardParts;
        private Panel _cardNudges;
        private Panel _cardNotes;

        private TextBox _txtJobNo;
        private TextBox _txtJobTitle;
        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private Button _btnAddSite;
        private ComboBox _cmbJobType;
        private ComboBox _cmbContract;
        private DateTimePicker _dtpScheduled;

        private ComboBox _cmbTechnician;
        private ComboBox _cmbPriority;
        private ComboBox _cmbStatus;

        private Label _lblChecklistCount;
        private FlowLayoutPanel _checklistFlow;
        private Panel _checklistAddPanel;
        private TextBox _txtNewChecklistItem;
        private Label _lblChecklistBanner;

        private FlowLayoutPanel _partsFlow;
        private Label _lblPartsTotal;
        private ComboBox _cmbPartSearch;
        private NumericUpDown _numPartQty;
        private NumericUpDown _numPartRate;
        private Label _lblPartStockHint;
        private Panel _partsAddPanel;

        private FlowLayoutPanel _nudgesFlow;
        private TextBox _txtNotes;

        private List<JobSummaryDto> _allJobs = new List<JobSummaryDto>();
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<ClientSite> _sitesForClient = new List<ClientSite>();
        private List<AMCContract> _contractsForClient = new List<AMCContract>();
        private List<Employee> _technicians = new List<Employee>();
        private List<StockItem> _inventory = new List<StockItem>();
        private JobDetailDto _currentDetail;
        private string _activeFilter = "All";
        private bool _isBinding;
        private bool _isNewMode = true;
        private bool _settingPlaceholder;
        private readonly Dictionary<Control, int> _cardExpandedHeights = new Dictionary<Control, int>();
        private readonly Dictionary<Control, int> _cardDefaultHeights = new Dictionary<Control, int>();
        private int _selectedJobId;
        private bool _showDashboard = true;
        private Panel _dashboardHost;
        private TextBox _dashboardSearch;
        private ComboBox _dashboardStatusFilter;
        private ComboBox _dashboardTypeFilter;
        private string _dashboardSearchValue = string.Empty;
        private string _dashboardStatusValue = "All Status";
        private string _dashboardTypeValue = "All Types";
        private int _dashboardPage = 1;
        private int _dashboardPageSize = 10;
        private bool _shortcutNewJobRequested;
        private Timer _initialJobsLoadTimer;

        public Action<int> OnOpenJobDetail { get; set; }

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        private sealed class JobLoadSnapshot
        {
            public List<JobSummaryDto> Jobs { get; set; } = new List<JobSummaryDto>();
            public List<B2BClient> Clients { get; set; } = new List<B2BClient>();
            public List<Employee> Technicians { get; set; } = new List<Employee>();
            public List<StockItem> Inventory { get; set; } = new List<StockItem>();
            public bool TimedOut { get; set; }
        }

        public JobManagementForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            RecordDeletionUi.BindDeleteShortcut(this, () => DeleteCurrentJobAsync(), () => !_showDashboard && !_isNewMode && _currentDetail != null && _currentDetail.Job != null);
            Load += (s, e) => QueueInitialJobsLoad();
        }

        private void QueueInitialJobsLoad()
        {
            if (_initialJobsLoadTimer != null)
                return;

            _initialJobsLoadTimer = new Timer { Interval = 750 };
            _initialJobsLoadTimer.Tick += async (s, e) =>
            {
                _initialJobsLoadTimer.Stop();
                _initialJobsLoadTimer.Dispose();
                _initialJobsLoadTimer = null;
                await LoadInitialAsync();
            };
            _initialJobsLoadTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _initialJobsLoadTimer != null)
            {
                _initialJobsLoadTimer.Stop();
                _initialJobsLoadTimer.Dispose();
                _initialJobsLoadTimer = null;
            }

            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            Controls.Clear();
            if (_showDashboard)
            {
                BuildJobsDashboardLayout();
                return;
            }

            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 1,
                BackColor = Border
            };

            BuildLeftPanel();
            BuildRightPanel();

            Controls.Add(_split);
            Resize += (s, e) =>
            {
                AdjustResponsiveLayout();
                LayoutCards();
            };
            if (IsHandleCreated)
                BeginInvoke((Action)(() =>
                {
                    AdjustResponsiveLayout();
                    LayoutCards();
                }));
        }

        private void AdjustResponsiveLayout()
        {
            if (_split == null)
                return;

            int width = _split.Width > 0 ? _split.Width : ClientSize.Width;
            if (width <= _split.SplitterWidth + 120)
                return;

            bool compact = width < 1180;
            int panel1Min = compact ? 200 : 300;
            int panel2Min = compact ? 360 : 560;
            int availableForPanels = width - _split.SplitterWidth - 4;

            if (panel1Min + panel2Min > availableForPanels)
            {
                panel1Min = Math.Max(160, Math.Min(panel1Min, availableForPanels / 3));
                panel2Min = Math.Max(220, availableForPanels - panel1Min);
            }

            panel1Min = Math.Max(0, Math.Min(panel1Min, availableForPanels - 1));
            panel2Min = Math.Max(0, Math.Min(panel2Min, availableForPanels - panel1Min));
            _split.Panel1MinSize = panel1Min;
            _split.Panel2MinSize = panel2Min;

            int desiredLeft = compact ? 250 : 380;
            int minLeft = _split.Panel1MinSize;
            int maxLeft = width - _split.Panel2MinSize - _split.SplitterWidth;
            if (maxLeft < minLeft)
                return;

            _split.SplitterDistance = Math.Max(minLeft, Math.Min(desiredLeft, maxLeft));
        }

        private void BuildJobsDashboardLayout()
        {
            _dashboardHost = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, AutoScroll = true, Padding = new Padding(18, 14, 18, 18) };
            Controls.Add(_dashboardHost);
        }

        private void RenderJobsDashboard()
        {
            if (_dashboardHost == null || _dashboardHost.IsDisposed)
                return;

            _dashboardHost.SuspendLayout();
            _dashboardHost.Controls.Clear();
            Panel content = new Panel { BackColor = PageBg, Location = new Point(18, 14), Size = new Size(Math.Max(960, _dashboardHost.ClientSize.Width - 48), 1080) };
            _dashboardHost.AutoScrollMinSize = new Size(0, content.Height + 42);
            _dashboardHost.HorizontalScroll.Enabled = false;
            _dashboardHost.HorizontalScroll.Visible = false;
            _dashboardHost.Controls.Add(content);

            Control header = BuildJobsDashboardHeader(content.Width);
            header.Location = new Point(0, 0);
            content.Controls.Add(header);

            int headerBottom = header.Bottom + 10;
            FlowLayoutPanel stats = new FlowLayoutPanel { Location = new Point(0, headerBottom), Size = new Size(content.Width, 94), BackColor = PageBg, WrapContents = false, AutoScroll = true };
            int statWidth = Math.Max(178, (content.Width - 60) / 6);
            foreach (Control stat in BuildJobStatCards(statWidth))
                stats.Controls.Add(stat);
            content.Controls.Add(stats);

            int chartsTop = stats.Bottom + 18;
            TableLayoutPanel charts = new TableLayoutPanel { Location = new Point(0, chartsTop), Size = new Size(content.Width, 232), BackColor = PageBg, ColumnCount = 4, RowCount = 1 };
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
            charts.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            charts.Controls.Add(BuildJobsDonutCard("Jobs by Status", StatusSlices()), 0, 0);
            charts.Controls.Add(BuildJobsDonutCard("Jobs by Type", TypeSlices()), 1, 0);
            charts.Controls.Add(BuildJobsDonutCard("Jobs by Priority", PrioritySlices()), 2, 0);
            charts.Controls.Add(BuildUpcomingJobsCard(), 3, 0);
            content.Controls.Add(charts);

            int middleTop = charts.Bottom + 18;
            TableLayoutPanel middle = new TableLayoutPanel { Location = new Point(0, middleTop), Size = new Size(content.Width, 380), BackColor = PageBg, ColumnCount = 2, RowCount = 1 };
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80f));
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            middle.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            middle.Controls.Add(BuildAllJobsCard(), 0, 0);
            middle.Controls.Add(BuildJobsSidebar(), 1, 0);
            content.Controls.Add(middle);

            int bottomTop = middle.Bottom + 18;
            TableLayoutPanel bottom = new TableLayoutPanel { Location = new Point(0, bottomTop), Size = new Size(content.Width, 210), BackColor = PageBg, ColumnCount = 2, RowCount = 1 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            bottom.Controls.Add(BuildJobsByAssigneeCard(), 0, 0);
            bottom.Controls.Add(BuildJobsByLocationCard(), 1, 0);
            content.Controls.Add(bottom);

            _dashboardHost.ResumeLayout();
        }

        private Control BuildJobsDashboardHeader(int width)
        {
            Panel header = new Panel { Name = "JobsDashboardHeader", Size = new Size(width, width < 1280 ? 104 : 64), BackColor = PageBg };
            Label title = new Label { Text = "Jobs Dashboard", Location = new Point(0, 0), Size = new Size(320, 28), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
            Label subtitle = new Label { Text = "Monitor jobs, technicians, and service status. Client is required; site can be selected later.", Location = new Point(1, 30), Size = new Size(660, 18), Font = new Font("Segoe UI", 8.8f), ForeColor = TextSecondary, AutoEllipsis = true };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            _dashboardSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary, Size = new Size(280, 30), Text = _dashboardSearchValue };
            _dashboardSearch.MinimumSize = new Size(260, 30);
            ConfigureDashboardPlaceholder(_dashboardSearch, "Search jobs, client, location, or ID...");
            _dashboardSearch.TextChanged += (s, e) => { CaptureDashboardFilterState(); _dashboardPage = 1; RenderJobsDashboard(); };
            Button filters = DashboardButton("Filter Jobs", White, TextPrimary, 102, true);
            filters.MinimumSize = new Size(84, 30);
            filters.Click += (s, e) => { if (_dashboardStatusFilter != null) _dashboardStatusFilter.DroppedDown = true; };
            Button reports = DashboardButton("View Reports", White, TextPrimary, 114, true);
            reports.MinimumSize = new Size(88, 30);
            reports.Click += (s, e) => ShowDashboardMessage("Reports", "Job reports will use the live filtered job data.");
            Button add = DashboardButton("+ Add New Job", Blue, White, 146, false);
            add.MinimumSize = new Size(146, 30);
            add.Click += (s, e) =>
            {
                ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
                menu.Items.Add("Add New Job", null, async (mi, ev) => await BeginNewJobAsync());
                menu.Items.Add("Open Job Forms", null, (mi, ev) => OpenJobForms());
                menu.Items.Add("Bulk Import", null, (mi, ev) => ImportUiHelper.RunImport(ExcelImportModule.Jobs, FindForm()));
                menu.Show(add, new Point(0, add.Height));
            };
            Button more = DashboardButton("⋮", White, TextPrimary, 34, true);
            more.MinimumSize = new Size(34, 30);
            more.Click += (s, e) => ShowDashboardMessage("Jobs", "Use quick actions to assign technicians, open job forms, export filtered jobs, or review scheduling.");
            Panel toolbar = new Panel
            {
                Name = "JobsDashboardHeaderActionRail",
                Width = 678,
                Height = 38,
                Padding = new Padding(0, 1, 0, 0),
                BackColor = PageBg
            };
            foreach (Control control in new Control[] { _dashboardSearch, filters, reports, add, more })
            {
                control.Margin = Padding.Empty;
                control.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                control.Tag = ((control.Tag == null ? string.Empty : control.Tag + " ") + "FIXED_WIDTH").Trim();
            }
            Control[] toolbarItems = { _dashboardSearch, filters, reports, add, more };
            toolbar.Controls.AddRange(toolbarItems);
            header.Controls.Add(toolbar);
            bool layoutBusy = false;
            Action layoutToolbar = () =>
            {
                if (layoutBusy)
                    return;

                layoutBusy = true;
                try
                {
                    bool compact = header.ClientSize.Width < 1280;
                    header.Height = compact ? 104 : 64;
                    if (compact)
                    {
                        title.Size = new Size(Math.Max(260, header.ClientSize.Width - 40), 28);
                        subtitle.Size = new Size(Math.Max(260, header.ClientSize.Width - 40), 18);
                        _dashboardSearch.Width = 260;
                        toolbar.SetBounds(0, 58, Math.Min(704, Math.Max(340, header.ClientSize.Width)), 38);
                    }
                    else
                    {
                        _dashboardSearch.Width = 280;
                        int toolbarWidth = 704;
                        toolbar.SetBounds(Math.Max(0, header.ClientSize.Width - toolbarWidth), 0, toolbarWidth, 38);
                        title.Size = new Size(Math.Max(280, toolbar.Left - 18), 28);
                        subtitle.Size = new Size(Math.Max(280, toolbar.Left - 18), 18);
                    }

                    int x = 0;
                    foreach (Control control in toolbarItems)
                    {
                        control.Location = new Point(x, 1);
                        x += control.Width + 6;
                    }

                    toolbar.BringToFront();
                }
                finally
                {
                    layoutBusy = false;
                }
            };
            header.Resize += (s, e) =>
            {
                layoutToolbar();
            };
            toolbar.Layout += (s, e) => layoutToolbar();
            layoutToolbar();
            return header;
        }

        private IEnumerable<Control> BuildJobStatCards(int width)
        {
            int count = _allJobs.Count;
            yield return DashboardStat(width, "Total Jobs", count == 0 ? "—" : count.ToString(), "All time", BlueLightBg, Blue);
            yield return DashboardStat(width, "Open Jobs", count == 0 ? "—" : CountStatus("Open").ToString(), "Currently open", DS.Green50, DS.Green600);
            yield return DashboardStat(width, "In Progress", count == 0 ? "—" : CountStatus("In Progress").ToString(), "Currently in progress", AmberLightBg, Amber);
            yield return DashboardStat(width, "Completed", count == 0 ? "—" : CountStatus("Completed").ToString(), "All completed", TealLightBg, Teal);
            yield return DashboardStat(width, "On Hold", count == 0 ? "—" : CountStatus("On Hold").ToString(), "Currently on hold", PurpleLight, Purple);
            yield return DashboardStat(width, "Cancelled", count == 0 ? "—" : CountStatus("Cancelled").ToString(), "All cancelled", Surface, TextSecondary);
        }

        private Panel BuildJobsDonutCard(string title, List<DashSlice> slices)
        {
            Panel card = DashboardCard(title + "  ⓘ", null, null);
            if (_allJobs.Count == 0)
            {
                card.Controls.Add(Donut(slices, "No data", "available", new Point(30, 52), new Size(132, 132)));
                AddEmptyState(card, "No data available", "", 92);
            }
            else
            {
                card.Controls.Add(Donut(slices, _allJobs.Count.ToString(), "Total", new Point(24, 52), new Size(132, 132)));
                AddLegend(card, slices, 182, 58);
            }
            return card;
        }

        private Panel BuildUpcomingJobsCard()
        {
            Panel card = DashboardCard("Upcoming Jobs  ⓘ", "View all", (s, e) => ShowDashboardMessage("Upcoming Jobs", BuildUpcomingJobsText()));
            List<JobSummaryDto> jobs = UpcomingJobs().Take(5).ToList();
            if (jobs.Count == 0)
            {
                AddEmptyState(card, "No upcoming jobs.", "Scheduled jobs will appear here.", 58);
                return card;
            }
            int y = 46;
            foreach (JobSummaryDto job in jobs)
            {
                card.Controls.Add(new Label { Text = job.JobNumber, Location = new Point(16, y), Size = new Size(120, 16), Font = new Font("Consolas", 7.5f, FontStyle.Bold), ForeColor = Blue, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = job.JobTitle, Location = new Point(16, y + 16), Size = new Size(card.Width - 34, 16), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 7.3f), ForeColor = TextPrimary, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = IndiaFormatHelper.FormatDate(job.ScheduledDate), Location = new Point(card.Width - 92, y), Size = new Size(74, 16), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.2f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight });
                y += 36;
            }
            return card;
        }

        private Panel BuildAllJobsCard()
        {
            Panel card = DashboardCard("All Jobs", null, null);
            card.Size = new Size(900, 320);
            _dashboardSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8f), ForeColor = TextPrimary, Location = new Point(card.Width - 668, 38), Size = new Size(150, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right, Text = _dashboardSearchValue };
            ConfigureDashboardPlaceholder(_dashboardSearch, "Search jobs...");
            _dashboardSearch.TextChanged += (s, e) => { CaptureDashboardFilterState(); _dashboardPage = 1; RenderJobsDashboard(); };
            _dashboardStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8f), Location = new Point(card.Width - 508, 38), Size = new Size(118, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _dashboardStatusFilter.Items.AddRange(new object[] { "All Status", "Open", "In Progress", "Completed", "On Hold", "Cancelled" });
            SelectDashboardCombo(_dashboardStatusFilter, _dashboardStatusValue, "All Status");
            _dashboardStatusFilter.SelectedIndexChanged += (s, e) => { CaptureDashboardFilterState(); _dashboardPage = 1; RenderJobsDashboard(); };
            _dashboardTypeFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8f), Location = new Point(card.Width - 380, 38), Size = new Size(110, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _dashboardTypeFilter.Items.AddRange(new object[] { "All Types", "Installation", "Maintenance", "Repair", "Inspection", "Other" });
            SelectDashboardCombo(_dashboardTypeFilter, _dashboardTypeValue, "All Types");
            _dashboardTypeFilter.SelectedIndexChanged += (s, e) => { CaptureDashboardFilterState(); _dashboardPage = 1; RenderJobsDashboard(); };
            Button saved = DashboardButton("Saved View", White, TextPrimary, 98, true);
            saved.Location = new Point(card.Width - 260, 36);
            saved.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            saved.Click += (s, e) => ShowSavedViewMenu(saved);
            Button columns = DashboardButton("Columns", White, TextPrimary, 86, true);
            columns.Location = new Point(card.Width - 96, 36);
            columns.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            columns.Click += (s, e) => ShowDashboardColumnsMenu(columns);
            card.Controls.AddRange(new Control[] { _dashboardSearch, _dashboardStatusFilter, _dashboardTypeFilter, saved, columns });

            List<JobSummaryDto> filtered = DashboardFilteredJobs().ToList();
            if (filtered.Count == 0)
            {
                AddEmptyState(card, "No jobs found.", "Add a new job to get started.", 106);
                Button add = DashboardButton("+ Add New Job", White, Blue, 118, true);
                add.Location = new Point((card.Width - add.Width) / 2, 214);
                add.Anchor = AnchorStyles.Top;
                add.Click += async (s, e) => await BeginNewJobAsync();
                card.Controls.Add(add);
                GlobalPaginationControl emptyPager = BuildJobsPagination(card, 0, Math.Max(1, _dashboardPageSize));
                card.Controls.Add(emptyPager);
                return card;
            }

            TableLayoutPanel table = new TableLayoutPanel { Location = new Point(16, 76), Size = new Size(card.Width - 32, 184), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, BackColor = White, ColumnCount = 11, RowCount = 1 };
            foreach (float w in new[] { 4f, 11f, 16f, 13f, 9f, 9f, 8f, 10f, 8f, 9f, 3f })
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            string[] heads = { "", "Job Number", "Job Title", "Client", "Type", "Status", "Priority", "Scheduled Date", "Due", "Assigned To", "Actions" };
            for (int i = 0; i < heads.Length; i++)
                table.Controls.Add(TableLabel(heads[i], true, TextPrimary), i, 0);
            int pageSize = Math.Max(1, _dashboardPageSize);
            _dashboardPage = PaginationState.NormalizePage(_dashboardPage, filtered.Count, pageSize);
            foreach (JobSummaryDto job in filtered.Skip((_dashboardPage - 1) * pageSize).Take(pageSize))
                AddJobTableRow(table, job);
            card.Controls.Add(table);
            GlobalPaginationControl pager = BuildJobsPagination(card, filtered.Count, pageSize);
            card.Controls.Add(pager);
            return card;
        }

        /// <summary>Builds the shared jobs dashboard pagination footer.</summary>
        private GlobalPaginationControl BuildJobsPagination(Control card, int totalRows, int pageSize)
        {
            GlobalPaginationControl pager = new GlobalPaginationControl
            {
                Location = new Point(card.Width - 576, 286),
                Size = new Size(560, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = White
            };
            pager.PageChanged += (s, e) => { _dashboardPage = pager.CurrentPage; RenderJobsDashboard(); };
            pager.PageSizeChanged += (s, e) => { _dashboardPageSize = pager.PageSize; _dashboardPage = 1; RenderJobsDashboard(); };
            pager.SetState(_dashboardPage, totalRows, pageSize);
            return pager;
        }

        private Panel BuildJobsSidebar()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(10, 0, 0, 0) };
            TableLayoutPanel stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = PageBg };
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));
            stack.Controls.Add(BuildQuickActionsCard(), 0, 0);
            stack.Controls.Add(BuildJobsSummaryCard(), 0, 1);
            host.Controls.Add(stack);
            return host;
        }

        private Panel BuildJobsSummaryCard()
        {
            Panel card = DashboardCard("Jobs Summary  ⓘ", null, null);
            int y = 46;
            var rows = new[]
            {
                Tuple.Create("Overdue Jobs", _allJobs.Count == 0 ? "—" : _allJobs.Count(j => j.ScheduledDate.Date < DateTime.Today && !IsClosedDashboardStatus(j)).ToString()),
                Tuple.Create("Jobs Due Today", _allJobs.Count == 0 ? "—" : _allJobs.Count(j => j.ScheduledDate.Date == DateTime.Today).ToString()),
                Tuple.Create("Jobs Due This Week", _allJobs.Count == 0 ? "—" : _allJobs.Count(j => j.ScheduledDate.Date >= DateTime.Today && j.ScheduledDate.Date <= DateTime.Today.AddDays(7)).ToString()),
                Tuple.Create("Jobs Due This Month", _allJobs.Count == 0 ? "—" : _allJobs.Count(j => j.ScheduledDate.Month == DateTime.Today.Month && j.ScheduledDate.Year == DateTime.Today.Year).ToString()),
                Tuple.Create("Unassigned Jobs", _allJobs.Count == 0 ? "—" : _allJobs.Count(j => !j.TechnicianId.HasValue).ToString())
            };
            foreach (var row in rows)
            {
                card.Controls.Add(new Label { Text = row.Item1, Location = new Point(16, y), Size = new Size(135, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextPrimary });
                card.Controls.Add(new Label { Text = row.Item2, Location = new Point(card.Width - 52, y), Size = new Size(30, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight });
                y += 28;
            }
            return card;
        }

        private Panel BuildQuickActionsCard()
        {
            Panel card = DashboardCard("Quick Actions", null, null);
            card.Controls.Add(new Label
            {
                Text = "Create, import, plan, review.",
                Location = new Point(16, 44),
                Size = new Size(card.Width - 32, 38),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 7.8f),
                ForeColor = TextSecondary
            });
            Button open = DashboardButton("More Actions", Blue, White, 130, false);
            open.Location = new Point(16, 92);
            open.Size = new Size(card.Width - 32, 32);
            open.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            open.Click += (s, e) => ShowDashboardQuickActionsMenu(open);
            card.Controls.Add(open);
            return card;
        }

        private void ShowDashboardQuickActionsMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            var actions = new[]
            {
                Tuple.Create("Add New Job", "Add New Job"),
                Tuple.Create("Job Templates", "Job Templates"),
                Tuple.Create("Bulk Create", "Bulk Create"),
                Tuple.Create("Workflow Board", "Workflow Board"),
                Tuple.Create("Schedule Board", "Schedule Board"),
                Tuple.Create("Resource Planner", "Resource Planner"),
                Tuple.Create("View Reports", "Reports")
            };
            foreach (var action in actions)
            {
                string command = action.Item2;
                menu.Items.Add(action.Item1, null, async (s, e) => await HandleDashboardQuickAction(command));
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private Panel BuildJobsByAssigneeCard()
        {
            Panel card = DashboardCard("Jobs by Assignee  ⓘ", "View report", (s, e) => ShowDashboardMessage("Assignees", BuildAssigneeText()));
            var rows = _allJobs.Where(j => !string.IsNullOrWhiteSpace(j.TechnicianName)).GroupBy(j => j.TechnicianName).Select(g => new { Name = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).Take(6).ToList();
            if (rows.Count == 0) { AddEmptyState(card, "No data available", "Job assignee data will appear here.", 58); return card; }
            AddRankRows(card, rows.Select(r => Tuple.Create(r.Name, r.Count)).ToList(), Blue);
            return card;
        }

        private Panel BuildJobsByLocationCard()
        {
            Panel card = DashboardCard("Jobs by Location  ⓘ", "View report", (s, e) => ShowDashboardMessage("Locations", BuildLocationText()));
            var rows = _allJobs.Where(j => !string.IsNullOrWhiteSpace(j.SiteName)).GroupBy(j => j.SiteName).Select(g => new { Name = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).Take(6).ToList();
            if (rows.Count == 0) { AddEmptyState(card, "No data available", "Location data will appear here.", 58); return card; }
            AddRankRows(card, rows.Select(r => Tuple.Create(r.Name, r.Count)).ToList(), Teal);
            return card;
        }

        private IEnumerable<JobSummaryDto> DashboardFilteredJobs()
        {
            IEnumerable<JobSummaryDto> query = _allJobs;
            string search = DashboardSearchText();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(j => Contains(j.JobNumber, search) || Contains(j.JobTitle, search) || Contains(j.ClientName, search) || Contains(j.SiteName, search) || Contains(j.TechnicianName, search));
            string status = string.IsNullOrWhiteSpace(_dashboardStatusValue) ? "All Status" : _dashboardStatusValue;
            if (!string.IsNullOrWhiteSpace(status) && status != "All Status")
                query = query.Where(j => DashboardStatus(j) == status);
            string type = string.IsNullOrWhiteSpace(_dashboardTypeValue) ? "All Types" : _dashboardTypeValue;
            if (!string.IsNullOrWhiteSpace(type) && type != "All Types")
                query = query.Where(j => DashboardType(j) == type);
            return query.OrderByDescending(j => j.ScheduledDate);
        }

        private string DashboardSearchText()
        {
            if (_dashboardSearch == null || _dashboardSearch.ForeColor == TextHint)
                return _dashboardSearchValue ?? string.Empty;
            return _dashboardSearch.Text ?? _dashboardSearchValue ?? string.Empty;
        }

        private void ConfigureDashboardPlaceholder(TextBox box, string placeholder)
        {
            if (box == null) return;
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = placeholder;
                box.ForeColor = TextHint;
            }
            box.GotFocus += (s, e) =>
            {
                if (box.ForeColor == TextHint && box.Text == placeholder)
                {
                    box.Text = string.Empty;
                    box.ForeColor = TextPrimary;
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = placeholder;
                    box.ForeColor = TextHint;
                }
            };
        }

        private string DashboardStatus(JobSummaryDto job)
        {
            string value = NormalizePipeline(job?.PipelineStatus);
            if (value == "InProgress" || value == "ChecklistDone") return "In Progress";
            if (value == "Closed" || value == "Invoiced") return "Completed";
            string raw = job?.PipelineStatus ?? string.Empty;
            if (raw.IndexOf("hold", StringComparison.OrdinalIgnoreCase) >= 0) return "On Hold";
            if (raw.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0) return "Cancelled";
            return "Open";
        }

        private string DashboardType(JobSummaryDto job)
        {
            string type = (job?.JobType ?? string.Empty).ToUpperInvariant();
            if (type.Contains("INSTALL")) return "Installation";
            if (type.Contains("AMC") || type.Contains("PM") || type.Contains("MAINT")) return "Maintenance";
            if (type.Contains("BREAK") || type.Contains("REPAIR")) return "Repair";
            if (type.Contains("INSPECT")) return "Inspection";
            return "Other";
        }

        private string DashboardPriority(JobSummaryDto job)
        {
            string priority = (job?.Priority ?? string.Empty).ToUpperInvariant();
            if (priority.Contains("CRIT")) return "Critical";
            if (priority.Contains("HIGH")) return "High";
            if (priority.Contains("LOW")) return "Low";
            return "Medium";
        }

        private int CountStatus(string status) => _allJobs.Count(j => DashboardStatus(j) == status);
        private bool IsClosedDashboardStatus(JobSummaryDto job) => DashboardStatus(job) == "Completed" || DashboardStatus(job) == "Cancelled";

        private List<DashSlice> StatusSlices() => new List<DashSlice>
        {
            new DashSlice("Open", CountStatus("Open"), Blue),
            new DashSlice("In Progress", CountStatus("In Progress"), Amber),
            new DashSlice("Completed", CountStatus("Completed"), Teal),
            new DashSlice("On Hold", CountStatus("On Hold"), Purple),
            new DashSlice("Cancelled", CountStatus("Cancelled"), Red)
        };

        private List<DashSlice> TypeSlices() => new[] { "Installation", "Maintenance", "Repair", "Inspection", "Other" }
            .Select(t => new DashSlice(t, _allJobs.Count(j => DashboardType(j) == t), t == "Installation" ? Blue : t == "Maintenance" ? Teal : t == "Repair" ? Amber : t == "Inspection" ? Purple : TextHint)).ToList();

        private List<DashSlice> PrioritySlices() => new[] { "Critical", "High", "Medium", "Low" }
            .Select(p => new DashSlice(p, _allJobs.Count(j => DashboardPriority(j) == p), p == "Critical" ? Red : p == "High" ? AmberDark : p == "Medium" ? Amber : Teal)).ToList();

        private IEnumerable<JobSummaryDto> UpcomingJobs()
        {
            return _allJobs.Where(j => j.ScheduledDate.Date >= DateTime.Today && j.ScheduledDate.Date <= DateTime.Today.AddDays(7) && DashboardStatus(j) != "Completed")
                .OrderBy(j => j.ScheduledDate);
        }

        private Panel DashboardCard(string title, string linkText, EventHandler linkClick)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, Size = new Size(320, 220), BackColor = White, Margin = new Padding(0, 0, 10, 0) };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, White, Border, 8);
            card.Controls.Add(new Label { Text = title, Location = new Point(16, 12), Size = new Size(260, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary });
            if (!string.IsNullOrWhiteSpace(linkText))
            {
                LinkLabel link = new LinkLabel { Text = linkText, Location = new Point(card.Width - 86, 14), Size = new Size(72, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, LinkColor = Blue, TextAlign = ContentAlignment.TopRight, Font = new Font("Segoe UI", 7.8f) };
                if (linkClick != null) link.Click += linkClick;
                card.Controls.Add(link);
            }
            return card;
        }

        private Button DashboardButton(string text, Color bg, Color fg, int width, bool outline)
        {
            Button button = new Button { Text = text, Size = new Size(width, 30), BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = Border;
            return button;
        }

        private Control DashboardStat(int width, string label, string value, string sub, Color iconBg, Color iconFg)
        {
            Panel card = new Panel { Size = new Size(width, 84), BackColor = White, Margin = new Padding(0, 0, 10, 0) };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, White, Border, 8);
            card.Controls.Add(new Label { Text = "□", Location = new Point(16, 20), Size = new Size(42, 42), BackColor = iconBg, ForeColor = iconFg, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15f, FontStyle.Bold) });
            card.Controls.Add(new Label { Text = label, Location = new Point(72, 14), Size = new Size(width - 88, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = value, Location = new Point(72, 34), Size = new Size(width - 88, 24), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = sub, Location = new Point(72, 60), Size = new Size(width - 88, 18), Font = new Font("Segoe UI", 7.4f), ForeColor = TextSecondary, AutoEllipsis = true });
            return card;
        }

        private Panel Donut(List<DashSlice> slices, string center, string subtitle, Point location, Size size)
        {
            Panel donut = new Panel { Location = location, Size = size, BackColor = White };
            donut.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int total = Math.Max(1, slices.Sum(slice => slice.Value));
                Rectangle rect = new Rectangle(8, 8, donut.Width - 16, donut.Height - 16);
                float start = -90f;
                foreach (DashSlice slice in slices.Where(slice => slice.Value > 0))
                {
                    float sweep = 360f * slice.Value / total;
                    using (Pen pen = new Pen(slice.Color, 18f)) e.Graphics.DrawArc(pen, rect, start, sweep);
                    start += sweep;
                }
                if (slices.All(slice => slice.Value == 0))
                    using (Pen pen = new Pen(BorderLight, 18f)) e.Graphics.DrawArc(pen, rect, 0, 360);
                using (SolidBrush brush = new SolidBrush(White)) e.Graphics.FillEllipse(brush, new Rectangle(34, 34, donut.Width - 68, donut.Height - 68));
                TextRenderer.DrawText(e.Graphics, center, new Font("Segoe UI", 9f, FontStyle.Bold), new Rectangle(18, 48, donut.Width - 36, 20), TextPrimary, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(e.Graphics, subtitle, new Font("Segoe UI", 7f), new Rectangle(18, 68, donut.Width - 36, 18), TextSecondary, TextFormatFlags.HorizontalCenter);
            };
            return donut;
        }

        private void AddLegend(Panel card, List<DashSlice> slices, int x, int y)
        {
            foreach (DashSlice slice in slices)
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(x, y), Size = new Size(16, 16), ForeColor = slice.Color });
                card.Controls.Add(new Label { Text = slice.Name, Location = new Point(x + 18, y), Size = new Size(104, 16), Font = new Font("Segoe UI", 7.3f), ForeColor = TextPrimary, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = slice.Value == 0 ? "—" : slice.Value.ToString(), Location = new Point(card.Width - 54, y), Size = new Size(30, 16), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.3f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight });
                y += 28;
            }
        }

        private Label TableLabel(string text, bool header, Color color)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, BackColor = header ? Surface : White, ForeColor = color, Font = new Font("Segoe UI", header ? 7.5f : 7.3f, header ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(4, 0, 4, 0) };
        }

        private void AddJobTableRow(TableLayoutPanel table, JobSummaryDto job)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            int row = table.RowCount++;
            table.Controls.Add(new CheckBox { Dock = DockStyle.Fill, BackColor = White }, 0, row);
            LinkLabel link = new LinkLabel { Text = job.JobNumber, Dock = DockStyle.Fill, LinkColor = Blue, Font = new Font("Consolas", 7.4f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            link.Click += async (s, e) => await OpenExistingJobFromDashboardAsync(job.JobId);
            table.Controls.Add(link, 1, row);
            table.Controls.Add(TableLabel(job.JobTitle, false, TextPrimary), 2, row);
            table.Controls.Add(TableLabel(job.ClientName, false, TextPrimary), 3, row);
            table.Controls.Add(TableLabel(DashboardType(job), false, TextSecondary), 4, row);
            table.Controls.Add(TableLabel(DashboardStatus(job), false, StatusColor(DashboardStatus(job))), 5, row);
            table.Controls.Add(TableLabel(DashboardPriority(job), false, PriorityColor(DashboardPriority(job))), 6, row);
            table.Controls.Add(TableLabel(IndiaFormatHelper.FormatDate(job.ScheduledDate), false, TextPrimary), 7, row);
            table.Controls.Add(TableLabel(GetJobDueText(job), false, GetJobDueColor(job)), 8, row);
            table.Controls.Add(TableLabel(string.IsNullOrWhiteSpace(job.TechnicianName) ? "Unassigned" : job.TechnicianName, false, string.IsNullOrWhiteSpace(job.TechnicianName) ? TextHint : TextPrimary), 9, row);
            Button menu = DashboardButton("⋯", White, TextPrimary, 28, true);
            menu.Click += (s, e) => ShowJobRowMenu(job, menu);
            table.Controls.Add(menu, 10, row);
        }

        private void AddEmptyState(Panel card, string title, string subtitle, int y)
        {
            Label icon = new Label { Text = "□", Location = new Point((card.Width - 58) / 2, y), Size = new Size(58, 42), Anchor = AnchorStyles.Top, Font = new Font("Segoe UI", 22f, FontStyle.Bold), ForeColor = TextHint, TextAlign = ContentAlignment.MiddleCenter };
            Label head = new Label { Text = title, Location = new Point(24, y + 50), Size = new Size(card.Width - 48, 20), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.6f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter };
            Label sub = new Label { Text = subtitle, Location = new Point(32, y + 74), Size = new Size(card.Width - 64, 34), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.6f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleCenter };
            card.Controls.AddRange(new Control[] { icon, head, sub });
        }

        private Color StatusColor(string status) => status == "Open" ? Blue : status == "In Progress" ? Amber : status == "Completed" ? Teal : status == "On Hold" ? Purple : Red;
        private Color PriorityColor(string priority) => priority == "Critical" ? Red : priority == "High" ? AmberDark : priority == "Medium" ? Amber : Teal;

        private string GetJobDueText(JobSummaryDto job)
        {
            if (job == null || IsClosedDashboardStatus(job))
                return "-";

            int days = (DateTime.Today - job.ScheduledDate.Date).Days;
            if (days < 0)
                return "In " + Math.Abs(days).ToString() + "d";
            if (days == 0)
                return "Today";
            return days.ToString() + "d old";
        }

        private Color GetJobDueColor(JobSummaryDto job)
        {
            if (job == null || IsClosedDashboardStatus(job))
                return TextHint;

            int days = (DateTime.Today - job.ScheduledDate.Date).Days;
            if (days > 7)
                return Red;
            if (days >= 3)
                return AmberDark;
            return Teal;
        }

        private async Task OpenExistingJobFromDashboardAsync(int jobId)
        {
            _showDashboard = false;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            BindLookups();
            BindPartInventory();
            RenderFilterChips();
            ApplyFilters();
            await LoadJobDetailAsync(jobId);
        }

        private void ShowJobRowMenu(JobSummaryDto job, Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("View", null, async (s, e) => await OpenExistingJobFromDashboardAsync(job.JobId));
            menu.Items.Add("Edit", null, async (s, e) => await OpenExistingJobFromDashboardAsync(job.JobId));
            menu.Items.Add("Assign Technician", null, (s, e) => ShowDashboardMessage("Assign Technician", "Open the job and select a technician."));
            menu.Items.Add("Change Status", null, (s, e) => ShowDashboardMessage("Change Status", "Open the job and update its status."));
            menu.Items.Add("Duplicate", null, (s, e) => ShowDashboardMessage("Duplicate Job", "Open the job and save it as a new work order."));
            RecordDeletionUi.AddDeleteMenuItem(menu, async (s, e) => await DeleteDashboardJobAsync(job));
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private async Task DeleteDashboardJobAsync(JobSummaryDto job)
        {
            if (job == null || job.JobId <= 0)
                return;

            string jobLabel = string.IsNullOrWhiteSpace(job.JobNumber) ? "this job" : job.JobNumber;
            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Job",
                jobLabel,
                "Checklist items, parts, and pending charges for this job will also be removed.");
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SetListStatus("Deleting job...");
                await Task.Run(() => _jobSvc.Delete(job.JobId));
                if (_selectedJobId == job.JobId)
                    _selectedJobId = 0;
                _currentDetail = _currentDetail != null && _currentDetail.Job != null && _currentDetail.Job.JobID == job.JobId ? null : _currentDetail;
                _allJobs = await Task.Run(() => _jobSvc.GetAllJobsWithSummary()) ?? new List<JobSummaryDto>();
                if (_showDashboard)
                {
                    RenderJobsDashboard();
                }
                else
                {
                    RenderFilterChips();
                    ApplyFilters();
                }
                SetListStatus("Job deleted.");
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Deleting job", ex);
                SetListStatus("Delete failed. Please refresh and try again.");
            }
        }

        private async Task HandleDashboardQuickAction(string action)
        {
            if (action == "Add New Job")
                await BeginNewJobAsync();
            else if (action == "Job Templates")
                OpenJobForms();
            else if (action == "Bulk Create")
                ImportUiHelper.RunImport(ExcelImportModule.Jobs, FindForm());
            else if (action == "Workflow Board")
                OpenWorkflowBoard();
            else if (action == "Schedule Board")
                ShowDashboardMessage("Schedule Board", BuildScheduleBoardText());
            else if (action == "Resource Planner")
                ShowDashboardMessage("Resource Planner", BuildResourcePlannerText());
            else if (action == "Reports")
                ShowDashboardMessage("Reports", BuildScheduleBoardText());
            else
                ShowDashboardMessage(action, "No dashboard action is configured for " + action + ".");
        }

        private void AddRankRows(Panel card, List<Tuple<string, int>> rows, Color color)
        {
            int max = Math.Max(1, rows.Max(r => r.Item2));
            int y = 48;
            foreach (Tuple<string, int> row in rows)
            {
                card.Controls.Add(new Label { Text = GetInitials(row.Item1), Location = new Point(16, y), Size = new Size(30, 20), BackColor = DS.Lighten(color, 0.65f), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 7f, FontStyle.Bold) });
                card.Controls.Add(new Label { Text = row.Item1, Location = new Point(54, y), Size = new Size(150, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true });
                Panel track = new Panel { Location = new Point(212, y + 7), Size = new Size(Math.Max(20, card.Width - 288), 5), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, BackColor = BorderLight };
                track.Controls.Add(new Panel { Dock = DockStyle.Left, Width = (int)(track.Width * (row.Item2 / (double)max)), BackColor = color });
                card.Controls.Add(track);
                card.Controls.Add(new Label { Text = row.Item2.ToString(), Location = new Point(card.Width - 48, y), Size = new Size(28, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.6f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight });
                y += 28;
            }
        }

        private string BuildUpcomingJobsText() => string.Join(Environment.NewLine, UpcomingJobs().Take(10).Select(j => j.JobNumber + " - " + j.JobTitle + " - " + IndiaFormatHelper.FormatDate(j.ScheduledDate)));
        private string BuildAssigneeText() => string.Join(Environment.NewLine, _allJobs.GroupBy(j => string.IsNullOrWhiteSpace(j.TechnicianName) ? "Unassigned" : j.TechnicianName).OrderByDescending(g => g.Count()).Select(g => g.Key + ": " + g.Count()));
        private string BuildLocationText() => string.Join(Environment.NewLine, _allJobs.GroupBy(j => string.IsNullOrWhiteSpace(j.SiteName) ? "No site" : j.SiteName).OrderByDescending(g => g.Count()).Select(g => g.Key + ": " + g.Count()));
        private string BuildScheduleBoardText()
        {
            List<JobSummaryDto> jobs = _allJobs ?? new List<JobSummaryDto>();
            if (jobs.Count == 0)
                return "No jobs are available for scheduling.";

            DateTime today = DateTime.Today;
            List<string> lines = new List<string>();
            int overdue = jobs.Count(j => j.ScheduledDate.Date < today && !IsClosedDashboardStatus(j));
            int todayCount = jobs.Count(j => j.ScheduledDate.Date == today && !IsClosedDashboardStatus(j));
            int weekCount = jobs.Count(j => j.ScheduledDate.Date >= today && j.ScheduledDate.Date <= today.AddDays(7) && !IsClosedDashboardStatus(j));

            lines.Add("Dispatch snapshot");
            lines.Add("Overdue: " + overdue + " | Today: " + todayCount + " | Next 7 days: " + weekCount);
            lines.Add(string.Empty);

            List<JobSummaryDto> upcoming = jobs
                .Where(j => j.ScheduledDate.Date >= today && !IsClosedDashboardStatus(j))
                .OrderBy(j => j.ScheduledDate)
                .ThenBy(j => j.JobNumber)
                .Take(10)
                .ToList();

            if (upcoming.Count == 0)
            {
                lines.Add("No upcoming open jobs.");
            }
            else
            {
                lines.Add("Next scheduled jobs:");
                lines.AddRange(upcoming.Select(j =>
                    IndiaFormatHelper.FormatDate(j.ScheduledDate) + " - " +
                    FirstNonEmpty(j.JobNumber, "Job") + " - " +
                    FirstNonEmpty(j.ClientName, "No client") + " - " +
                    (string.IsNullOrWhiteSpace(j.TechnicianName) ? "Unassigned" : j.TechnicianName)));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string BuildResourcePlannerText()
        {
            List<JobSummaryDto> jobs = _allJobs ?? new List<JobSummaryDto>();
            if (jobs.Count == 0)
                return "No jobs are available for resource planning.";

            DateTime today = DateTime.Today;
            List<string> lines = new List<string>();
            lines.Add("Technician workload");

            var workload = jobs
                .Where(j => !IsClosedDashboardStatus(j))
                .GroupBy(j => string.IsNullOrWhiteSpace(j.TechnicianName) ? "Unassigned" : j.TechnicianName)
                .Select(g => new
                {
                    Name = g.Key,
                    Open = g.Count(),
                    Overdue = g.Count(j => j.ScheduledDate.Date < today),
                    NextDate = g.Where(j => j.ScheduledDate.Date >= today).Select(j => (DateTime?)j.ScheduledDate.Date).OrderBy(d => d).FirstOrDefault()
                })
                .OrderByDescending(x => x.Open)
                .ThenByDescending(x => x.Overdue)
                .ThenBy(x => x.Name)
                .Take(10)
                .ToList();

            if (workload.Count == 0)
            {
                lines.Add("No open technician workload.");
            }
            else
            {
                foreach (var row in workload)
                {
                    string next = row.NextDate.HasValue ? IndiaFormatHelper.FormatDate(row.NextDate.Value) : "No upcoming date";
                    lines.Add(row.Name + ": " + row.Open + " open, " + row.Overdue + " overdue, next " + next);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void ShowDashboardMessage(string title, string text) => MessageBox.Show(this, string.IsNullOrWhiteSpace(text) ? "No records available." : text, BrandingService.WindowTitle(title), MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void ShowDashboardColumnsMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Visible columns", null, (s, e) => ShowDashboardMessage("Visible Columns", BuildJobColumnSummary()));
            menu.Items.Add("Copy filtered jobs", null, (s, e) => CopyFilteredJobsToClipboard());
            menu.Items.Add("Reset filters", null, (s, e) => ResetDashboardFilters());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void ShowSavedViewMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Save current view", null, (s, e) => SaveCurrentDashboardView());
            menu.Items.Add("Apply saved view", null, (s, e) => ApplySavedDashboardView());
            menu.Items.Add("Clear saved view", null, (s, e) => ClearSavedDashboardView());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private string BuildJobColumnSummary()
        {
            int total = _allJobs == null ? 0 : _allJobs.Count;
            int filtered = DashboardFilteredJobs().Count();
            return "Visible columns:" + Environment.NewLine +
                   "Job Number, Job Title, Client, Type, Status, Priority, Scheduled Date, Assigned To, Actions" +
                   Environment.NewLine + Environment.NewLine +
                   "Showing " + filtered + " of " + total + " jobs after current filters.";
        }

        private void CopyFilteredJobsToClipboard()
        {
            List<JobSummaryDto> jobs = DashboardFilteredJobs().ToList();
            if (jobs.Count == 0)
            {
                ShowDashboardMessage("Copy Jobs", "No filtered jobs are available to copy.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Job Number\tJob Title\tClient\tType\tStatus\tPriority\tScheduled Date\tAssigned To");
            foreach (JobSummaryDto job in jobs)
            {
                sb.AppendLine(string.Join("\t", new[]
                {
                    CleanClipboardCell(job.JobNumber),
                    CleanClipboardCell(job.JobTitle),
                    CleanClipboardCell(job.ClientName),
                    CleanClipboardCell(DashboardType(job)),
                    CleanClipboardCell(DashboardStatus(job)),
                    CleanClipboardCell(DashboardPriority(job)),
                    CleanClipboardCell(IndiaFormatHelper.FormatDate(job.ScheduledDate)),
                    CleanClipboardCell(string.IsNullOrWhiteSpace(job.TechnicianName) ? "Unassigned" : job.TechnicianName)
                }));
            }

            if (UIHelper.TrySetClipboardText(this, sb.ToString(), BrandingService.WindowTitle("Copy Jobs")))
                ShowDashboardMessage("Copy Jobs", "Copied " + jobs.Count + " filtered job row(s) to the clipboard.");
        }

        private static string CleanClipboardCell(string value)
        {
            return (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private void ResetDashboardFilters()
        {
            _dashboardSearchValue = string.Empty;
            _dashboardStatusValue = "All Status";
            _dashboardTypeValue = "All Types";
            if (_dashboardSearch != null)
                _dashboardSearch.Text = string.Empty;
            if (_dashboardStatusFilter != null && _dashboardStatusFilter.Items.Count > 0)
                _dashboardStatusFilter.SelectedIndex = 0;
            if (_dashboardTypeFilter != null && _dashboardTypeFilter.Items.Count > 0)
                _dashboardTypeFilter.SelectedIndex = 0;

            _dashboardPage = 1;
            RenderJobsDashboard();
        }

        private void SaveCurrentDashboardView()
        {
            CaptureDashboardFilterState();
            new SavedViewService().SaveJobsDefaultView(_dashboardSearchValue, _dashboardStatusValue, _dashboardTypeValue);
            ShowDashboardMessage("Saved View", "Current Jobs dashboard filters were saved.");
        }

        private void ApplySavedDashboardView()
        {
            SavedListView view = new SavedViewService().LoadJobsDefaultView();
            if (view == null)
            {
                ShowDashboardMessage("Saved View", "No saved Jobs dashboard view found.");
                return;
            }

            _dashboardSearchValue = view.SearchText ?? string.Empty;
            _dashboardStatusValue = string.IsNullOrWhiteSpace(view.StatusFilter) ? "All Status" : view.StatusFilter;
            _dashboardTypeValue = string.IsNullOrWhiteSpace(view.TypeFilter) ? "All Types" : view.TypeFilter;
            _dashboardPage = 1;
            RenderJobsDashboard();
        }

        private void ClearSavedDashboardView()
        {
            new SavedViewService().ClearJobsDefaultView();
            ShowDashboardMessage("Saved View", "Saved Jobs dashboard view cleared.");
        }

        private void CaptureDashboardFilterState()
        {
            if (_dashboardSearch != null && _dashboardSearch.ForeColor != TextHint)
                _dashboardSearchValue = _dashboardSearch.Text ?? string.Empty;
            if (_dashboardStatusFilter != null && _dashboardStatusFilter.SelectedItem != null)
                _dashboardStatusValue = _dashboardStatusFilter.SelectedItem.ToString();
            if (_dashboardTypeFilter != null && _dashboardTypeFilter.SelectedItem != null)
                _dashboardTypeValue = _dashboardTypeFilter.SelectedItem.ToString();
        }

        private static void SelectDashboardCombo(ComboBox combo, string value, string fallback)
        {
            string target = string.IsNullOrWhiteSpace(value) ? fallback : value;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i].ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private void OpenWorkflowBoard()
        {
            using (var form = new JobWorkflowBoardForm())
                form.ShowDialog(FindForm());
        }

        private sealed class DashSlice
        {
            public DashSlice(string name, int value, Color color)
            {
                Name = name;
                Value = value;
                Color = color;
            }
            public string Name { get; private set; }
            public int Value { get; private set; }
            public Color Color { get; private set; }
        }

        private void BuildLeftPanel()
        {
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = White };
            left.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, left.Width - 1, 0, left.Width - 1, left.Height);
            };

            Panel header = new Panel { Dock = DockStyle.Top, Height = 228, BackColor = White, Padding = new Padding(14, 10, 14, 10) };
            header.Controls.Add(new Label
            {
                Text = "Jobs",
                Location = new Point(14, 10),
                Size = new Size(260, 26),
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            });
            header.Controls.Add(new Label
            {
                Text = "Dispatch queue and field work orders",
                Location = new Point(14, 38),
                Size = new Size(320, 18),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.2f),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            });
            TableLayoutPanel leftActions = new TableLayoutPanel
            {
                Location = new Point(14, 66),
                Size = new Size(340, 150),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = White,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0)
            };
            leftActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            leftActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            leftActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            leftActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            Button btnTemplate = MakeHeaderButton("Excel Template", Blue, White, 112);
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Jobs, FindForm());
            Button btnImport = MakeHeaderButton("Import", Amber, White, 74);
            btnImport.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(btnImport, ExcelImportModule.Jobs, FindForm());
            Button btnFormsLeft = MakeHeaderButton("Forms", Color.White, Blue, 78);
            btnFormsLeft.FlatAppearance.BorderColor = Border;
            ModernIconSystem.AddButtonIcon(btnFormsLeft, ModernIconKind.Document);
            btnFormsLeft.Click += (s, e) => OpenJobForms();
            Button btnNew = MakeHeaderButton("+ New Job", Teal, White, 104);
            btnNew.Click += async (s, e) => await BeginNewJobAsync();
            foreach (Button actionButton in new[] { btnTemplate, btnImport, btnFormsLeft, btnNew })
            {
                actionButton.Dock = DockStyle.Fill;
                actionButton.Margin = new Padding(0, 0, 0, 6);
                actionButton.Height = 30;
            }
            btnNew.Margin = new Padding(0);
            leftActions.Controls.Add(btnNew, 0, 0);
            leftActions.Controls.Add(btnTemplate, 0, 1);
            leftActions.Controls.Add(btnImport, 0, 2);
            leftActions.Controls.Add(btnFormsLeft, 0, 3);
            header.Controls.Add(leftActions);

            Panel searchWrap = new Panel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(16, 8, 16, 8), BackColor = White };
            Panel searchBox = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(12, 8, 8, 8) };
            searchBox.Paint += (s, e) => DrawRoundedBorder(e.Graphics, searchBox.ClientRectangle, Surface, Surface, 6);
            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Surface,
                ForeColor = TextPrimary
            };
            ConfigurePlaceholder(_txtSearch, "Search jobs, clients, technicians...");
            _txtSearch.TextChanged += (s, e) => { UpdateSearchClear(); ApplyFilters(); };
            _btnSearchClear = new Button
            {
                Dock = DockStyle.Right,
                Width = 24,
                FlatStyle = FlatStyle.Flat,
                Text = "x",
                Font = new Font("Segoe UI", 11f),
                ForeColor = TextHint,
                BackColor = Surface,
                Visible = false
            };
            _btnSearchClear.FlatAppearance.BorderSize = 0;
            _btnSearchClear.Click += (s, e) => { _txtSearch.Text = string.Empty; ApplyFilters(); };
            searchBox.Controls.Add(_txtSearch);
            searchBox.Controls.Add(_btnSearchClear);
            searchWrap.Controls.Add(searchBox);

            Panel chipWrap = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(16, 0, 16, 6), BackColor = White };
            _chipFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = White, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };
            chipWrap.Controls.Add(_chipFlow);

            Panel statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = White, Padding = new Padding(16, 0, 16, 8) };
            _lblListStatus = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint, Text = "Loading..." };
            statusPanel.Controls.Add(_lblListStatus);

            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(8, 0, 8, 8) };
            _jobListFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = White,
                Padding = new Padding(8, 4, 8, 8)
            };
            _jobListFlow.Resize += (s, e) => ResizeJobListItems();
            listWrap.Controls.Add(_jobListFlow);

            left.Controls.Add(listWrap);
            left.Controls.Add(statusPanel);
            left.Controls.Add(chipWrap);
            left.Controls.Add(searchWrap);
            left.Controls.Add(header);
            _split.Panel1.Controls.Add(left);
        }

        private void BuildRightPanel()
        {
            _rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };

            _topBar = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = White, Padding = new Padding(22, 14, 22, 14) };
            BuildTopBar();

            _pipelineBar = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = White, Padding = new Padding(22, 12, 22, 12) };
            BuildPipelineBar();

            _detailScroll = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, AutoScroll = true, Padding = new Padding(24, 22, 24, 28) };
            _detailScroll.HorizontalScroll.Enabled = false;
            _detailScroll.HorizontalScroll.Visible = false;
            _cardsHost = new Panel { BackColor = PageBg, Location = new Point(0, 0), Size = new Size(980, 2200) };
            _detailScroll.Controls.Add(_cardsHost);
            _detailScroll.Resize += (s, e) => LayoutCards();

            BuildCards();

            _rightPanel.Controls.Add(_detailScroll);
            _rightPanel.Controls.Add(_pipelineBar);
            _rightPanel.Controls.Add(_topBar);
            _rightPanel.Visible = false;
            _split.Panel2.Controls.Add(_rightPanel);
        }

        private void BuildTopBar()
        {
            Panel textWrap = new Panel { Dock = DockStyle.Fill, BackColor = White };
            textWrap.MinimumSize = new Size(320, 0);
            textWrap.Resize += (s, e) => LayoutJobHeaderText(textWrap);

            _lblJobNumber = new Label { Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint, Text = "JOB", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            _lblJobTitle = new Label { Font = new Font("Segoe UI", 15.5f, FontStyle.Regular), ForeColor = TextPrimary, Text = "New job", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            _lblMeta = new Label { Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, Text = "Status - Type - Technician - Date - Priority", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            textWrap.Controls.Add(_lblMeta);
            textWrap.Controls.Add(_lblJobTitle);
            textWrap.Controls.Add(_lblJobNumber);

            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 620,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = White,
                Padding = new Padding(0, 4, 0, 0),
                AutoScroll = false
            };

            Button btnBackToDashboard = MakeHeaderButton("<- Back to Dashboard", Color.White, TextPrimary, 124);
            btnBackToDashboard.FlatAppearance.BorderColor = Border;
            btnBackToDashboard.Click += async (s, e) =>
            {
                _showDashboard = true;
                BuildLayout();
                await LoadInitialAsync();
            };
            _btnSave = MakeHeaderButton("Save", Teal, White, 86);
            _btnSave.Click += async (s, e) => await SaveAsync();
            _btnPrintReport = MakeHeaderButton("Print Report", Blue, White, 108);
            _btnPrintReport.Click += (s, e) => PrintReport();
            Button btnCalendar = MakeHeaderButton("Calendar", Color.FromArgb(79, 70, 229), White, 96);
            btnCalendar.Click += (s, e) => ExportCalendarEvent();
            Button btnForms = MakeHeaderButton("Forms", Color.White, Blue, 82);
            btnForms.FlatAppearance.BorderColor = Border;
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            btnForms.Click += (s, e) => OpenJobForms();
            Button btnWhatsApp = MakeHeaderButton("WhatsApp", Teal, White, 102);
            ModernIconSystem.AddButtonIcon(btnWhatsApp, ModernIconKind.Phone);
            btnWhatsApp.Click += (s, e) => ShowJobWhatsAppAction();
            _btnCloseJob = MakeHeaderButton("Close Job", Red, White, 96);
            _btnCloseJob.Click += async (s, e) => await CloseJobAsync();
            Button btnDelete = MakeHeaderButton("Delete", Red, White, 82);
            btnDelete.Click += async (s, e) => await DeleteCurrentJobAsync();
            actionFlow.Controls.Add(btnBackToDashboard);
            actionFlow.Controls.Add(_btnSave);
            actionFlow.Controls.Add(_btnPrintReport);
            actionFlow.Controls.Add(btnCalendar);
            actionFlow.Controls.Add(btnForms);
            actionFlow.Controls.Add(btnWhatsApp);
            actionFlow.Controls.Add(_btnCloseJob);
            actionFlow.Controls.Add(btnDelete);

            _topBar.Controls.Add(textWrap);
            _topBar.Controls.Add(actionFlow);
            _topBar.Resize += (s, e) => LayoutJobTopBar(textWrap, actionFlow);
            LayoutJobTopBar(textWrap, actionFlow);
            LayoutJobHeaderText(textWrap);
        }

        private void LayoutJobTopBar(Panel textWrap, FlowLayoutPanel actionFlow)
        {
            if (_topBar == null || textWrap == null || actionFlow == null)
                return;

            int clientWidth = _topBar.ClientSize.Width - _topBar.Padding.Left - _topBar.Padding.Right;
            if (clientWidth <= 0)
                return;

            int titleMinimum = Math.Min(420, Math.Max(300, clientWidth / 3));
            int actionWidth = Math.Max(180, clientWidth - titleMinimum - 18);
            actionWidth = Math.Min(838, actionWidth);
            actionFlow.Width = actionWidth;
            LayoutJobHeaderText(textWrap);
        }

        private void LayoutJobHeaderText(Panel textWrap)
        {
            if (textWrap == null)
                return;

            int width = Math.Max(120, textWrap.ClientSize.Width - 8);
            _lblJobNumber?.SetBounds(0, 0, width, 18);
            _lblJobTitle?.SetBounds(0, 20, width, 30);
            _lblMeta?.SetBounds(0, 54, width, 18);
        }

        private void BuildPipelineBar()
        {
            TableLayoutPanel flow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = _pipelineSteps.Count,
                RowCount = 1,
                BackColor = White,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < _pipelineSteps.Count; i++)
                flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _pipelineSteps.Count));
            flow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            for (int i = 0; i < _pipelineSteps.Count; i++)
            {
                string step = _pipelineSteps[i];
                Panel stepPanel = new Panel { Dock = DockStyle.Fill, Height = 34, BackColor = White, Cursor = Cursors.Hand, Tag = step, Margin = new Padding(0, 0, 8, 0) };
                stepPanel.Paint += (s, e) => DrawPipelineStep(e.Graphics, stepPanel, step);
                stepPanel.Click += async (s, e) => await HandlePipelineStepClickAsync((string)stepPanel.Tag);
                foreach (Control child in stepPanel.Controls)
                    child.Click += async (s, e) => await HandlePipelineStepClickAsync(step);
                _pipelineStepPanels[step] = stepPanel;
                flow.Controls.Add(stepPanel, i, 0);
            }

            _pipelineBar.Controls.Add(flow);
        }

        private void BuildCards()
        {
            _cardJobDetails = CreateCard("Job details", out Panel jobDetailsBody);
            _cardTechnician = CreateCard("Technician", out Panel technicianBody);
            _cardChecklist = CreateCard("Job checklist", out Panel checklistBody, out _lblChecklistCount);
            _cardParts = CreateCard("Parts used", out Panel partsBody);
            _cardNudges = CreateCard("Smart nudges", out Panel nudgesBody);
            _cardNotes = CreateCard("Notes", out Panel notesBody);

            BuildJobDetailsCard(jobDetailsBody);
            BuildTechnicianCard(technicianBody);
            BuildChecklistCard(checklistBody);
            BuildPartsCard(partsBody);
            BuildNudgesCard(nudgesBody);
            BuildNotesCard(notesBody);

            _cardsHost.Controls.AddRange(new Control[]
            {
                _cardJobDetails, _cardTechnician,
                _cardChecklist, _cardParts,
                _cardNudges,
                _cardNotes
            });

        }

        private void BuildJobDetailsCard(Panel body)
        {
            body.AutoScroll = false;
            // Top-docked rows render in reverse insertion order; keep Client visually above Site.
            body.Controls.Add(BuildSiteFormRow("Site (optional)", out _cmbSite, out _btnAddSite));
            body.Controls.Add(BuildFormRow("Client", out _cmbClient));
            body.Controls.Add(BuildFormRow("Job type", out _cmbJobType));
            body.Controls.Add(BuildFormRow("Linked contract", out _cmbContract));
            body.Controls.Add(BuildDateRow("Scheduled date", out _dtpScheduled));
            body.Controls.Add(BuildTextRow("Job number", out _txtJobNo, true));
            body.Controls.Add(BuildTextRow("Job title", out _txtJobTitle, false));

            _cmbClient.SelectedIndexChanged += async (s, e) =>
            {
                if (_isBinding) return;
                await LoadSitesAndContractsAsync();
            };
            _btnAddSite.Click += async (s, e) => await AddSiteForSelectedClientAsync();
            _cmbJobType.SelectedIndexChanged += async (s, e) =>
            {
                if (_isBinding) return;
                await HandleJobTypeChangedAsync();
            };
        }

        private void BuildTechnicianCard(Panel body)
        {
            body.AutoScroll = false;
            body.Controls.Add(BuildFormRow("Status", out _cmbStatus));
            body.Controls.Add(BuildFormRow("Priority", out _cmbPriority));
            body.Controls.Add(BuildFormRow("Assign technician", out _cmbTechnician));

            _cmbPriority.Items.AddRange(new object[] { "Low", "Medium", "High", "Critical" });
            _cmbStatus.Items.AddRange(new object[] { "Created", "Assigned", "InProgress", "ChecklistDone", "Closed", "Invoiced" });
            _cmbTechnician.SelectedIndexChanged += async (s, e) => { if (!_isBinding) await RefreshTechnicianWorkloadAsync(); };
            _cmbStatus.SelectedIndexChanged += async (s, e) =>
            {
                if (_isBinding || _isNewMode || _currentDetail == null || _cmbStatus.SelectedItem == null) return;
                string selected = _cmbStatus.SelectedItem.ToString();
                if (!string.Equals(selected, NormalizePipeline(_currentDetail.Job.PipelineStatus), StringComparison.OrdinalIgnoreCase))
                    await HandlePipelineStepClickAsync(selected);
            };
        }

        private void BuildChecklistCard(Panel body)
        {
            body.AutoScroll = false;
            _checklistFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 6, 0, 8) };
            _checklistAddPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = White, Padding = new Padding(0, 12, 0, 0), Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            LinkLabel addLink = new LinkLabel { Text = "+ Add item", LinkColor = TealDark, ActiveLinkColor = Teal, AutoSize = true, Location = new Point(0, 20) };
            _txtNewChecklistItem = new TextBox { Visible = false, Width = 360, Location = new Point(92, 14), Font = new Font("Segoe UI", 9.5f) };
            _txtNewChecklistItem.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await AddChecklistItemAsync();
                }
            };
            addLink.Click += (s, e) => { _txtNewChecklistItem.Visible = true; _txtNewChecklistItem.Focus(); };
            _checklistAddPanel.Resize += (s, e) =>
            {
                int width = Math.Max(220, _checklistAddPanel.ClientSize.Width - 110);
                _txtNewChecklistItem.SetBounds(92, 14, width, 32);
            };
            _checklistAddPanel.Controls.Add(addLink);
            _checklistAddPanel.Controls.Add(_txtNewChecklistItem);

            _lblChecklistBanner = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                ForeColor = TealDark,
                BackColor = TealLightBg,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Visible = false
            };

            body.Controls.Add(_checklistFlow);
            body.Controls.Add(_lblChecklistBanner);
            body.Controls.Add(_checklistAddPanel);
        }

        private void BuildPartsCard(Panel body)
        {
            body.AutoScroll = false;
            Panel headerAction = new Panel { Dock = DockStyle.Bottom, Height = 104, BackColor = White, Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            _partsAddPanel = new Panel { Dock = DockStyle.Top, Height = 98, BackColor = White, Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            Label materialHeading = MakePartInputHeading("Material");
            Label qtyHeading = MakePartInputHeading("Qty");
            Label rateHeading = MakePartInputHeading("Rate");
            Label actionHeading = MakePartInputHeading("Action");
            _cmbPartSearch = new ComboBox
            {
                Location = new Point(0, 28),
                Width = 380,
                Font = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbPartSearch.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cmbPartSearch.AutoCompleteSource = AutoCompleteSource.ListItems;
            _cmbPartSearch.TextChanged += (s, e) => UpdatePartStockHint();
            _cmbPartSearch.SelectedIndexChanged += (s, e) => UpdatePartStockHint();
            _numPartQty = new NumericUpDown { Location = new Point(392, 28), Width = 96, DecimalPlaces = 3, Minimum = 0.001m, Maximum = 9999, Value = 1 };
            _numPartRate = new NumericUpDown { Location = new Point(502, 28), Width = 112, DecimalPlaces = 2, Minimum = 0, Maximum = 9999999, Value = 0, ThousandsSeparator = true };
            Button btnAddPart = MakeInlineButton("Add", Teal, 86);
            btnAddPart.Location = new Point(628, 27);
            btnAddPart.Click += async (s, e) => await AddPartAsync();
            _lblPartStockHint = new Label { Location = new Point(0, 66), Size = new Size(520, 20), ForeColor = TextHint, Font = new Font("Segoe UI", 8.8f) };
            _partsAddPanel.Resize += (s, e) =>
            {
                int addWidth = 92;
                int qtyWidth = 104;
                int rateWidth = 116;
                int gap = 12;
                int available = Math.Max(320, _partsAddPanel.ClientSize.Width);
                int comboWidth = Math.Max(220, available - addWidth - qtyWidth - rateWidth - (gap * 3));
                materialHeading.SetBounds(0, 6, comboWidth, 18);
                qtyHeading.SetBounds(comboWidth + gap, 6, qtyWidth, 18);
                rateHeading.SetBounds(comboWidth + qtyWidth + (gap * 2), 6, rateWidth, 18);
                actionHeading.SetBounds(comboWidth + qtyWidth + rateWidth + (gap * 3), 6, addWidth, 18);
                _cmbPartSearch.SetBounds(0, 28, comboWidth, 34);
                _numPartQty.SetBounds(comboWidth + gap, 28, qtyWidth, 34);
                _numPartRate.SetBounds(comboWidth + qtyWidth + (gap * 2), 28, rateWidth, 34);
                btnAddPart.SetBounds(comboWidth + qtyWidth + rateWidth + (gap * 3), 27, addWidth, 36);
                _lblPartStockHint.SetBounds(0, 70, Math.Max(260, available - 8), 20);
            };
            _partsAddPanel.Controls.AddRange(new Control[] { materialHeading, qtyHeading, rateHeading, actionHeading, _cmbPartSearch, _numPartQty, _numPartRate, btnAddPart, _lblPartStockHint });

            _partsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 6, 0, 8) };
            _lblPartsTotal = new Label { Dock = DockStyle.Bottom, Height = 34, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight };

            headerAction.Controls.Add(_partsAddPanel);
            body.Controls.Add(_partsFlow);
            body.Controls.Add(_lblPartsTotal);
            body.Controls.Add(headerAction);
        }

        private Label MakePartInputHeading(string text)
        {
            return new Label
            {
                Text = text,
                Location = new Point(0, 6),
                Size = new Size(90, 18),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void BuildNudgesCard(Panel body)
        {
            body.AutoScroll = false;
            _nudgesFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 6, 0, 8) };
            body.Controls.Add(_nudgesFlow);
        }

        private void BuildNotesCard(Panel body)
        {
            body.AutoScroll = false;
            _txtNotes = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None
            };
            ConfigurePlaceholder(_txtNotes, "Add job notes, observations, or client instructions...");
            _txtNotes.Leave += async (s, e) => await AutoSaveNotesAsync();
            body.Controls.Add(_txtNotes);
        }

        private async Task LoadInitialAsync()
        {
            if (!_showDashboard)
                SetListStatus("Loading jobs...");
            Task<JobLoadSnapshot> loadTask = Task.Run(() => new JobLoadSnapshot
            {
                Jobs = _jobSvc.GetAllJobsWithSummary(),
                Clients = _clientSvc.GetAllClients(),
                Technicians = _employeeSvc.GetActiveTechnicians(),
                Inventory = _inventorySvc.GetAll()
            });
            Task completed = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(6)));
            JobLoadSnapshot snapshot = completed == loadTask
                ? await loadTask
                : new JobLoadSnapshot { TimedOut = true };

            _allJobs = snapshot.Jobs ?? new List<JobSummaryDto>();
            _clients = snapshot.Clients ?? new List<B2BClient>();
            _technicians = snapshot.Technicians ?? new List<Employee>();
            _inventory = snapshot.Inventory ?? new List<StockItem>();

            if (_shortcutNewJobRequested)
                return;

            if (_showDashboard)
            {
                RenderJobsDashboard();
                return;
            }

            BindLookups();
            BindPartInventory();
            RenderFilterChips();
            ApplyFilters();
            if (_allJobs.Count > 0)
                await LoadJobDetailAsync(_allJobs[0].JobId);
            else
                await BeginNewJobAsync();
            if (snapshot.TimedOut)
                SetListStatus("Job data is taking longer than expected.");
        }

        private void BindLookups()
        {
            _isBinding = true;
            try
            {
                _cmbClient.Items.Clear();
                _cmbClient.Items.Add(new LookupItem<int>(0, "-- Select client --"));
                foreach (B2BClient client in _clients.OrderBy(c => c.CompanyName))
                    _cmbClient.Items.Add(new LookupItem<int>(client.ClientID, client.CompanyName));
                if (_clients.Count == 0)
                    SetListStatus("No clients found. Add a client before creating a job.");
                _cmbClient.SelectedIndex = 0;

                _cmbTechnician.Items.Clear();
                _cmbTechnician.Items.Add(new LookupItem<int>(0, "-- Unassigned --"));
                foreach (Employee tech in _technicians.OrderBy(t => t.Name))
                    _cmbTechnician.Items.Add(new LookupItem<int>(tech.EmployeeID, tech.Name));
                _cmbTechnician.SelectedIndex = 0;

                _cmbPriority.SelectedIndex = 1;
                _cmbStatus.SelectedIndex = 0;

                _cmbJobType.Items.Clear();
                _cmbJobType.Items.AddRange(new object[] { "PM Visit", "Breakdown", "Installation", "AMC Visit", "Gas Charging", "General" });
                _cmbJobType.SelectedItem = "General";
            }
            finally
            {
                _isBinding = false;
            }
        }

        private void BindPartInventory()
        {
            _cmbPartSearch.Items.Clear();
            foreach (StockItem item in _inventory.OrderBy(i => i.ItemName))
                _cmbPartSearch.Items.Add(new LookupItem<int?>(item.ItemID, BuildPartLookupText(item)));
        }

        /// <summary>Opens the job editor with a fresh draft from external navigation.</summary>
        public async void OpenNewJobFromShortcut()
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)OpenNewJobFromShortcut);
                return;
            }

            try
            {
                _shortcutNewJobRequested = true;
                await EnsureShortcutDataAsync();
                if (!_showDashboard && _cmbClient != null)
                {
                    BindLookups();
                    BindPartInventory();
                    RenderFilterChips();
                    ApplyFilters();
                }

                await BeginNewJobAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Opening new job", ex);
            }
        }

        private async Task EnsureShortcutDataAsync()
        {
            if (_clients.Count > 0 && _technicians.Count > 0 && _inventory.Count > 0)
                return;

            JobLoadSnapshot snapshot = await Task.Run(() => new JobLoadSnapshot
            {
                Jobs = _jobSvc.GetAllJobsWithSummary(),
                Clients = _clientSvc.GetAllClients(),
                Technicians = _employeeSvc.GetActiveTechnicians(),
                Inventory = _inventorySvc.GetAll()
            });

            _allJobs = snapshot.Jobs ?? new List<JobSummaryDto>();
            _clients = snapshot.Clients ?? new List<B2BClient>();
            _technicians = snapshot.Technicians ?? new List<Employee>();
            _inventory = snapshot.Inventory ?? new List<StockItem>();
        }

        private async Task BeginNewJobAsync()
        {
            JobDraftLaunchContext launchContext = WorkflowLaunchContext.TakeJobDraft();

            if (_showDashboard)
            {
                _showDashboard = false;
                BuildLayout();
                UIHelper.ApplyInputStyles(Controls);
                BindLookups();
                BindPartInventory();
                RenderFilterChips();
                ApplyFilters();
            }

            ShowJobEditor();
            _isNewMode = true;
            _currentDetail = null;
            _isBinding = true;
            try
                {
                    _txtJobNo.Text = _jobSvc.GenerateJobNumber();
                    _txtJobTitle.Text = "AC Installation at Site";
                    _cmbClient.SelectedIndex = _cmbClient.Items.Count > 1 ? 1 : 0;
                    _cmbSite.Items.Clear();
                    _cmbSite.Items.Add(new LookupItem<int>(0, "-- No site / site not decided --"));
                    _cmbSite.SelectedIndex = 0;
                    _cmbContract.Items.Clear();
                    _cmbContract.Items.Add(new LookupItem<int>(0, "-- No linked contract --"));
                _cmbContract.SelectedIndex = 0;
                _cmbTechnician.SelectedIndex = 0;
                _cmbPriority.SelectedItem = "Medium";
                _cmbStatus.SelectedItem = "Created";
                _cmbJobType.SelectedItem = "General";
                _dtpScheduled.Value = DateTime.Today;
                SetTextBoxValue(_txtNotes, "Add job notes, observations, or client instructions...", true);
            }
            finally
            {
                _isBinding = false;
            }

            if (launchContext != null && launchContext.ClientId > 0)
            {
                _isBinding = true;
                try
                {
                    SelectLookup(_cmbClient, launchContext.ClientId);
                }
                finally
                {
                    _isBinding = false;
                }

                await LoadSitesAndContractsAsync();

                if (launchContext.SiteId > 0)
                {
                    _isBinding = true;
                    try
                    {
                        SelectLookup(_cmbSite, launchContext.SiteId);
                    }
                    finally
                    {
                        _isBinding = false;
                    }
                }
            }
            else if (GetSelectedId(_cmbClient) > 0)
            {
                await LoadSitesAndContractsAsync();
            }

            RenderChecklistPreview(_jobSvc.GetChecklistTemplates("General").Select(t => new JobChecklistItem { ItemText = t.ItemText, SortOrder = t.SortOrder }).ToList());
            RenderParts(new List<JobPartUsed>());
            RenderNudges(new List<NudgeDto>());
            RefreshHeader(null);
            UpdatePipelineBar("Created");
            await RefreshTechnicianWorkloadAsync();
            LayoutCards();
            ResetEditorScrollToTop();
        }

        private async Task LoadSitesAndContractsAsync()
        {
            int clientId = GetSelectedId(_cmbClient);
            var payload = await Task.Run(() => new
            {
                Sites = clientId > 0 ? _siteSvc.GetByClientId(clientId) : new List<ClientSite>(),
                Contracts = clientId > 0 ? _contractSvc.GetContractsByClient(clientId) : new List<AMCContract>()
            });

            _sitesForClient = payload.Sites;
            _contractsForClient = payload.Contracts;

            _isBinding = true;
            try
            {
                _cmbSite.Items.Clear();
                _cmbSite.Items.Add(new LookupItem<int>(0, "-- No site / site not decided --"));
                foreach (ClientSite site in _sitesForClient.OrderBy(s => s.SiteName))
                    _cmbSite.Items.Add(new LookupItem<int>(site.SiteID, SiteService.GetDisplayName(site)));
                _cmbSite.SelectedIndex = 0;

                _cmbContract.Items.Clear();
                _cmbContract.Items.Add(new LookupItem<int>(0, "-- No linked contract --"));
                foreach (AMCContract contract in _contractsForClient.OrderBy(c => c.ContractID))
                    _cmbContract.Items.Add(new LookupItem<int>(contract.ContractID, "AMC-" + contract.ContractID + " - " + (contract.ContractType ?? "AMC")));
                _cmbContract.SelectedIndex = 0;
            }
            finally
            {
                _isBinding = false;
            }
        }

        /// <summary>Creates a site for the selected client and selects it on the job form.</summary>
        private async Task AddSiteForSelectedClientAsync()
        {
            int clientId = GetSelectedId(_cmbClient);
            if (clientId <= 0)
            {
                MessageBox.Show("Select a client before adding a site.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string siteName = PromptSiteName();
            if (string.IsNullOrWhiteSpace(siteName))
                return;

            try
            {
                B2BClient client = _clients.FirstOrDefault(c => c.ClientID == clientId);
                ClientSite site = new ClientSite
                {
                    ClientID = clientId,
                    SiteName = siteName.Trim(),
                    City = client == null ? string.Empty : client.City
                };
                int siteId = await Task.Run(() => _siteSvc.Create(site));
                _sitesForClient = await Task.Run(() => _siteSvc.GetByClientId(clientId));
                _contractsForClient = await Task.Run(() => _contractSvc.GetContractsByClient(clientId));
                _isBinding = true;
                try
                {
                    RebindSitesAndContracts(siteId, GetSelectedId(_cmbContract));
                }
                finally
                {
                    _isBinding = false;
                }
                SetListStatus("Site added and selected.");
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Adding site", ex);
            }
        }

        /// <summary>Prompts for a new service site name.</summary>
        private string PromptSiteName()
        {
            using (Form dialog = ServoModalForm.Create("Add site", 380, 154))
            {
                Label label = new Label { Text = "Site name", Location = new Point(16, 16), Size = new Size(320, 20), Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
                TextBox input = new TextBox { Location = new Point(16, 42), Size = new Size(332, 24), Font = new Font("Segoe UI", 9f) };
                Button ok = MakeHeaderButton("Add", Teal, White, 78);
                ok.Location = new Point(188, 78);
                ok.DialogResult = DialogResult.OK;
                Button cancel = MakeHeaderButton("Cancel", Color.White, TextPrimary, 82);
                cancel.FlatAppearance.BorderColor = Border;
                cancel.FlatAppearance.BorderSize = 1;
                cancel.Location = new Point(270, 78);
                cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.AddRange(new Control[] { label, input, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(FindForm()) == DialogResult.OK ? input.Text.Trim() : string.Empty;
            }
        }

        private async Task LoadJobDetailAsync(int jobId)
        {
            ShowJobEditor();
            SetListStatus("Loading job...");
            JobDetailDto detail = await Task.Run(() => _jobSvc.GetJobDetail(jobId));
            if (detail == null)
                return;

            _currentDetail = detail;
            _isNewMode = false;
            BindDetail(detail);
            SetListStatus(_allJobs.Count + " jobs loaded");
        }

        private void ShowJobEditor()
        {
            if (_split != null && _split.Panel2Collapsed)
            {
                _split.Panel2Collapsed = false;
                AdjustResponsiveLayout();
            }

            if (_topBar != null)
                _topBar.Visible = true;
            if (_pipelineBar != null)
                _pipelineBar.Visible = true;
            if (_detailScroll != null && _cardsHost != null && !_detailScroll.Controls.Contains(_cardsHost))
            {
                _detailScroll.Controls.Clear();
                _detailScroll.Controls.Add(_cardsHost);
                LayoutCards();
            }
            if (_rightPanel != null && !_rightPanel.Visible)
                _rightPanel.Visible = true;
        }

        private void HideJobEditor()
        {
            _currentDetail = null;
            _isNewMode = false;
            ShowEmptyJobState();
        }

        private void ShowEmptyJobState()
        {
            if (_split != null && _split.Panel2Collapsed)
            {
                _split.Panel2Collapsed = false;
                AdjustResponsiveLayout();
            }

            if (_rightPanel != null)
                _rightPanel.Visible = true;
            if (_topBar != null)
                _topBar.Visible = false;
            if (_pipelineBar != null)
                _pipelineBar.Visible = false;
            if (_detailScroll == null)
                return;

            _detailScroll.Controls.Clear();
            Panel empty = new Panel
            {
                BackColor = White,
                Width = 420,
                Height = 160,
                Location = new Point(20, 20)
            };
            empty.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, empty.Width - 1, empty.Height - 1);
            };
            empty.Controls.Add(new Label
            {
                Text = "No job selected",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(24, 24),
                AutoSize = true
            });
            empty.Controls.Add(new Label
            {
                Text = "Create a job after selecting a client. Site can stay blank until the service location is confirmed.",
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSecondary,
                Location = new Point(24, 62),
                Size = new Size(350, 42)
            });
            Button create = MakeHeaderButton("+ New Job", Teal, White, 110);
            create.Location = new Point(24, 112);
            create.Click += async (s, e) => await BeginNewJobAsync();
            empty.Controls.Add(create);
            _detailScroll.Controls.Add(empty);
        }

        private void BindDetail(JobDetailDto detail)
        {
            _isBinding = true;
            try
            {
                Job job = detail.Job;
                _txtJobNo.Text = job.JobNumber ?? string.Empty;
                _txtJobTitle.Text = job.JobTitle ?? job.Title ?? string.Empty;
                SelectLookup(_cmbClient, job.ClientID);

                _sitesForClient = _siteSvc.GetByClientId(job.ClientID);
                _contractsForClient = _contractSvc.GetContractsByClient(job.ClientID);
                RebindSitesAndContracts(job.SiteID, job.LinkedContractId ?? 0);

                SelectLookup(_cmbTechnician, job.AssignedEmployeeID ?? 0);
                SelectText(_cmbPriority, job.Priority, "Medium");
                SelectText(_cmbStatus, NormalizePipeline(job.PipelineStatus), "Created");
                SelectText(_cmbJobType, job.JobType, "General");
                _dtpScheduled.Value = job.ScheduledDate == default(DateTime) ? DateTime.Today : job.ScheduledDate;
                SetTextBoxValue(_txtNotes, job.Notes, false);
            }
            finally
            {
                _isBinding = false;
            }

            RenderChecklist(detail.ChecklistItems);
            RenderParts(detail.PartsUsed);
            RenderNudges(_jobSvc.GenerateNudges(detail.Job.JobID));
            RefreshHeader(detail);
            UpdatePipelineBar(detail.Job.PipelineStatus);
            _ = RefreshTechnicianWorkloadAsync();
            LayoutCards();
        }

        private void RebindSitesAndContracts(int selectedSiteId, int selectedContractId)
        {
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new LookupItem<int>(0, "-- No site / site not decided --"));
            foreach (ClientSite site in _sitesForClient.OrderBy(s => s.SiteName))
                _cmbSite.Items.Add(new LookupItem<int>(site.SiteID, SiteService.GetDisplayName(site)));
            SelectLookup(_cmbSite, selectedSiteId);

            _cmbContract.Items.Clear();
            _cmbContract.Items.Add(new LookupItem<int>(0, "-- No linked contract --"));
            foreach (AMCContract contract in _contractsForClient.OrderBy(c => c.ContractID))
                _cmbContract.Items.Add(new LookupItem<int>(contract.ContractID, "AMC-" + contract.ContractID + " - " + (contract.ContractType ?? "AMC")));
            SelectLookup(_cmbContract, selectedContractId);
        }

        private void RenderFilterChips()
        {
            _chipFlow.Controls.Clear();
            AddChip("All", _allJobs.Count);
            AddChip("Pending", _allJobs.Count(j => j.PipelineStatus == "Created" || j.PipelineStatus == "Assigned"), AmberLightBg, AmberDark);
            AddChip("Active", _allJobs.Count(j => j.PipelineStatus == "InProgress" || j.PipelineStatus == "ChecklistDone"), TealLightBg, TealDark);
            AddChip("Done", _allJobs.Count(j => j.PipelineStatus == "Closed" || j.PipelineStatus == "Invoiced"), Surface, TextSecondary);
            AddChip("Overdue", _allJobs.Count(j => j.IsOverdue), RedLightBg, RedDark);
        }

        private void AddChip(string label, int count, Color? bg = null, Color? text = null)
        {
            if (!string.Equals(label, "All", StringComparison.OrdinalIgnoreCase) && count <= 0)
                return;

            Button chip = new Button
            {
                Text = label + " (" + count + ")",
                AutoSize = true,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? TealLightBg : (bg ?? White),
                ForeColor = string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? TealDark : (text ?? TextSecondary),
                Font = new Font("Segoe UI", 8.5f, string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? FontStyle.Bold : FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0),
                Tag = label
            };
            chip.FlatAppearance.BorderColor = string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#9FE1CB") : Border;
            chip.FlatAppearance.BorderSize = 1;
            chip.Click += (s, e) =>
            {
                _activeFilter = (string)chip.Tag;
                RenderFilterChips();
                ApplyFilters();
            };
            _chipFlow.Controls.Add(chip);
        }

        private void ApplyFilters()
        {
            if (_jobListFlow == null || _jobListFlow.IsDisposed)
                return;

            IEnumerable<JobSummaryDto> filtered = _allJobs ?? new List<JobSummaryDto>();
            string search = GetSearchText();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(j =>
                    Contains(j.JobNumber, search) ||
                    Contains(j.JobTitle, search) ||
                    Contains(j.ClientName, search) ||
                    Contains(j.SiteName, search) ||
                    Contains(j.TechnicianName, search));
            }

            switch (_activeFilter)
            {
                case "Pending":
                    filtered = filtered.Where(j => j.PipelineStatus == "Created" || j.PipelineStatus == "Assigned");
                    break;
                case "Active":
                    filtered = filtered.Where(j => j.PipelineStatus == "InProgress" || j.PipelineStatus == "ChecklistDone");
                    break;
                case "Done":
                    filtered = filtered.Where(j => j.PipelineStatus == "Closed" || j.PipelineStatus == "Invoiced");
                    break;
                case "Overdue":
                    filtered = filtered.Where(j => j.IsOverdue);
                    break;
            }

            RenderJobList(filtered.ToList());
        }

        private void RenderJobList(List<JobSummaryDto> jobs)
        {
            _jobListFlow.SuspendLayout();
            _jobListFlow.Controls.Clear();
            int itemWidth = GetJobListItemWidth();
            if (jobs.Count == 0)
            {
                _jobListFlow.Controls.Add(BuildJobEmptyState(ModernIconKind.Job, "No jobs match this view", "Adjust filters or create a new job.", itemWidth, 150));
            }
            else
            {
                foreach (JobSummaryDto job in jobs)
                    _jobListFlow.Controls.Add(CreateJobListItem(job, itemWidth));
            }
            _jobListFlow.ResumeLayout();
            SetListStatus(jobs.Count + " jobs shown");
        }

        private int GetJobListItemWidth()
        {
            int viewportWidth = _jobListFlow == null ? 0 : _jobListFlow.ClientSize.Width - _jobListFlow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 6;
            int splitWidth = _split == null ? 0 : _split.Panel1.Width - 42;
            return Math.Max(260, Math.Max(viewportWidth, splitWidth));
        }

        private void ResizeJobListItems()
        {
            if (_jobListFlow == null || _jobListFlow.IsDisposed)
                return;

            int itemWidth = GetJobListItemWidth();
            foreach (Control item in _jobListFlow.Controls)
            {
                item.Width = itemWidth;
                item.Invalidate();
            }
        }

        private Control CreateJobListItem(JobSummaryDto job, int itemWidth)
        {
            bool isSelected = _selectedJobId == job.JobId || (_currentDetail != null && _currentDetail.Job != null && _currentDetail.Job.JobID == job.JobId);
            Panel card = new Panel
            {
                Width = itemWidth,
                Height = 86,
                BackColor = isSelected ? TealLightBg : White,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(14, 10, 14, 10),
                Cursor = Cursors.Hand,
                Tag = job
            };
            card.Paint += (s, e) =>
            {
                using (SolidBrush bg = new SolidBrush(card.BackColor))
                    e.Graphics.FillRectangle(bg, card.ClientRectangle);
                using (Pen pen = new Pen(card.BackColor == TealLightBg ? ColorTranslator.FromHtml("#9FE1CB") : Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                if (card.BackColor == TealLightBg)
                {
                    using (SolidBrush brush = new SolidBrush(Teal))
                        e.Graphics.FillRectangle(brush, 0, 0, 3, card.Height);
                }
            };

            Label lblJob = new Label { Text = job.JobNumber, Location = new Point(0, 0), Size = new Size(130, 16), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary };
            Label lblStatus = MakePill(GetPipelineLabel(job.PipelineStatus), GetStatusPillBack(job), GetStatusPillFore(job), 84);
            lblStatus.Location = new Point(card.Width - 100, 0);

            Label lblClient = new Label { Text = (job.ClientName ?? "-") + " / " + (job.SiteName ?? "-"), Location = new Point(0, 22), Size = new Size(card.Width - 100, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            Label lblJobType = MakePill(job.JobType ?? "General", GetJobTypeBack(job.JobType), GetJobTypeFore(job.JobType), 96);
            lblJobType.Location = new Point(0, 44);
            Label lblDate = new Label
            {
                Text = job.IsOverdue ? "Overdue " + Math.Max((DateTime.Today - job.ScheduledDate.Date).Days, 1) + "d" : IndiaFormatHelper.FormatDate(job.ScheduledDate),
                Location = new Point(card.Width - 100, 46),
                Size = new Size(90, 14),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = job.IsOverdue ? RedDark : TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            };

            Label lblAvatar = new Label
            {
                Text = GetInitials(job.TechnicianName),
                Location = new Point(0, 63),
                Size = new Size(32, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = White,
                BackColor = GetTechnicianColor(job.TechnicianId ?? 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblAvatar.Paint += (s, e) =>
            {
                using (GraphicsPath path = BuildRoundedPath(new Rectangle(0, 0, lblAvatar.Width - 1, lblAvatar.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(lblAvatar.BackColor))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            };
            Label lblTech = new Label { Text = job.TechnicianName ?? "Unassigned", Location = new Point(40, 63), Size = new Size(150, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary };
            Label lblMargin = new Label
            {
                Text = job.EstimatedMarginPct > 0 ? job.EstimatedMarginPct.ToString("0.0") + "%" : "0%",
                Location = new Point(card.Width - 70, 63),
                Size = new Size(60, 16),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = GetMarginColor(job.EstimatedMarginPct),
                TextAlign = ContentAlignment.MiddleRight
            };

            card.Controls.AddRange(new Control[] { lblJob, lblStatus, lblClient, lblJobType, lblDate, lblAvatar, lblTech, lblMargin });
            AddClickable(card, () =>
            {
                _selectedJobId = job.JobId;
                if (OnOpenJobDetail != null)
                    OnOpenJobDetail(job.JobId);
                return Task.CompletedTask;
            });
            return card;
        }

        public void SelectJobFromNavigation(int jobId)
        {
            _selectedJobId = jobId;
            if (_jobListFlow != null)
                ApplyFilters();
        }

        private async Task SaveAsync()
        {
            try
            {
                Job job = CollectJobFromUi();
                bool created = _isNewMode;
                if (_isNewMode)
                {
                    int newId = await Task.Run(() => _jobSvc.Create(job));
                    if (newId < 0)
                    {
                        SetListStatus("Job saved locally. It will sync automatically when the office SQL Server is back.");
                        return;
                    }
                    await ReloadJobsAsync(newId);
                }
                else
                {
                    job.JobID = _currentDetail.Job.JobID;
                    job.InvoiceId = _currentDetail.Job.InvoiceId;
                    job.CompletedDate = _currentDetail.Job.CompletedDate;
                    job.ClosedDate = _currentDetail.Job.ClosedDate;
                    job.QuotedRevenue = _currentDetail.Job.QuotedRevenue;
                    job.Revenue = _currentDetail.Job.Revenue;
                    job.EstimatedCost = _currentDetail.Job.EstimatedCost;
                    await Task.Run(() => _jobSvc.Update(job));
                    await ReloadJobsAsync(job.JobID);
                }

                SetListStatus(created
                    ? "Job saved. Next: assign technician, add parts, or create a quotation."
                    : "Job updated. Review technician, checklist, and invoice status.");
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Saving job", ex);
            }
        }

        private async Task ReloadJobsAsync(int selectJobId)
        {
            _allJobs = await Task.Run(() => _jobSvc.GetAllJobsWithSummary());
            RenderFilterChips();
            ApplyFilters();
            await LoadJobDetailAsync(selectJobId);
        }

        private async Task DeleteCurrentJobAsync()
        {
            if (_isNewMode || _currentDetail == null || _currentDetail.Job == null || _currentDetail.Job.JobID <= 0)
            {
                SetListStatus("Select a saved job to delete.");
                return;
            }

            Job job = _currentDetail.Job;
            string jobLabel = string.IsNullOrWhiteSpace(job.JobNumber) ? "this job" : job.JobNumber;
            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Job",
                jobLabel,
                "Checklist items, parts, and pending charges for this job will also be removed.");
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SetListStatus("Deleting job...");
                await Task.Run(() => _jobSvc.Delete(job.JobID));
                _currentDetail = null;
                _selectedJobId = 0;
                _allJobs = await Task.Run(() => _jobSvc.GetAllJobsWithSummary()) ?? new List<JobSummaryDto>();
                RenderFilterChips();
                ApplyFilters();
                if (_allJobs.Count > 0)
                    await LoadJobDetailAsync(_allJobs[0].JobId);
                else
                    await BeginNewJobAsync();
                SetListStatus("Job deleted.");
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Deleting job", ex);
                SetListStatus("Delete failed. Please refresh and try again.");
            }
        }

        private Job CollectJobFromUi()
        {
            int clientId = GetSelectedId(_cmbClient);
            int siteId = GetSelectedId(_cmbSite);
            if (clientId <= 0) throw new Exception("Client is required.");
            if (string.IsNullOrWhiteSpace(_txtJobTitle.Text)) throw new Exception("Job title is required.");

            return new Job
            {
                JobNumber = _txtJobNo.Text.Trim(),
                ClientID = clientId,
                SiteID = siteId,
                Title = _txtJobTitle.Text.Trim(),
                JobTitle = _txtJobTitle.Text.Trim(),
                JobType = _cmbJobType.SelectedItem?.ToString() ?? "General",
                LinkedContractId = GetSelectedId(_cmbContract) > 0 ? (int?)GetSelectedId(_cmbContract) : null,
                ScheduledDate = _dtpScheduled.Value.Date,
                AssignedEmployeeID = GetSelectedId(_cmbTechnician) > 0 ? (int?)GetSelectedId(_cmbTechnician) : null,
                Priority = _cmbPriority.SelectedItem?.ToString() ?? "Medium",
                PipelineStatus = _cmbStatus.SelectedItem?.ToString() ?? "Created",
                Notes = GetTextValue(_txtNotes)
            };
        }

        private async Task HandleJobTypeChangedAsync()
        {
            string jobType = _cmbJobType.SelectedItem?.ToString() ?? "General";
            if (_isNewMode)
            {
                RenderChecklistPreview(_jobSvc.GetChecklistTemplates(jobType).Select(t => new JobChecklistItem { ItemText = t.ItemText, SortOrder = t.SortOrder }).ToList());
                ShowChecklistBanner("Checklist loaded for " + jobType);
                return;
            }

            if (_currentDetail != null && _currentDetail.ChecklistItems.Count > 0)
            {
                DialogResult confirm = MessageBox.Show("Replace the current checklist with the " + jobType + " template?", "Replace checklist", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                    return;
            }

            await Task.Run(() => _jobSvc.ApplyChecklistTemplate(_currentDetail.Job.JobID, jobType));
            await LoadJobDetailAsync(_currentDetail.Job.JobID);
            ShowChecklistBanner("Checklist loaded for " + jobType);
        }

        private async Task AddChecklistItemAsync()
        {
            string text = _txtNewChecklistItem.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (_isNewMode)
            {
                List<JobChecklistItem> preview = GetPreviewChecklistItems();
                preview.Add(new JobChecklistItem { ItemText = text, SortOrder = preview.Count + 1 });
                RenderChecklistPreview(preview);
            }
            else
            {
                await Task.Run(() => _jobSvc.AddChecklistItem(_currentDetail.Job.JobID, text));
                await LoadJobDetailAsync(_currentDetail.Job.JobID);
            }

            _txtNewChecklistItem.Clear();
            _txtNewChecklistItem.Visible = false;
        }

        private async Task AddPartAsync()
        {
            if (_isNewMode || _currentDetail == null)
            {
                MessageBox.Show("Save the job before adding parts.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                StockItem selectedStock = ResolveSelectedPartStock();
                int? itemId = selectedStock == null ? (int?)null : selectedStock.ItemID;
                string typed = selectedStock == null ? _cmbPartSearch.Text?.Trim() : selectedStock.ItemName;
                decimal unitRate = _numPartRate == null ? 0m : _numPartRate.Value;

                JobPartUsed addedPart = await Task.Run(() => _jobSvc.AddPartUsed(_currentDetail.Job.JobID, itemId, _numPartQty.Value, typed, unitRate));
                if (addedPart != null && string.Equals(addedPart.StockStatus, "PendingSync", StringComparison.OrdinalIgnoreCase))
                {
                    _cmbPartSearch.Text = string.Empty;
                    _numPartQty.Value = 1;
                    if (_numPartRate != null)
                        _numPartRate.Value = 0;
                    UpdatePartStockHint();
                    ShowChecklistBanner("Material saved locally. Stock will update when the office SQL Server syncs.");
                    return;
                }
                _inventory = await Task.Run(() => _inventorySvc.GetAll());
                BindPartInventory();
                _cmbPartSearch.Text = string.Empty;
                _numPartQty.Value = 1;
                if (_numPartRate != null)
                    _numPartRate.Value = 0;
                UpdatePartStockHint();
                await LoadJobDetailAsync(_currentDetail.Job.JobID);
                if (addedPart != null && !string.Equals(addedPart.StockStatus, "InStock", StringComparison.OrdinalIgnoreCase))
                    ShowChecklistBanner("Material added, but stock is " + (addedPart.StockStatus ?? "low") + ".");
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Adding material to job", ex);
            }
        }

        private async Task RefreshTechnicianWorkloadAsync()
        {
            await Task.CompletedTask;
            RefreshHeader(_currentDetail);
        }

        private void RefreshHeader(JobDetailDto detail)
        {
            Job job = detail?.Job;
            _lblJobNumber.Text = job?.JobNumber ?? _txtJobNo.Text.Trim();
            _lblJobTitle.Text = job?.JobTitle ?? _txtJobTitle.Text.Trim();
            string status = NormalizePipeline(job?.PipelineStatus ?? (_cmbStatus.SelectedItem?.ToString() ?? "Created"));
            string jobType = job?.JobType ?? (_cmbJobType.SelectedItem?.ToString() ?? "General");
            string techName = GetSelectedText(_cmbTechnician, "Unassigned");
            DateTime headerDate = DateTime.Today;
            if (_dtpScheduled != null && _dtpScheduled.Value.Year > 1900)
                headerDate = _dtpScheduled.Value;
            if (job != null && job.ScheduledDate.Year > 1900)
                headerDate = job.ScheduledDate;
            string date = IndiaFormatHelper.FormatDate(headerDate);
            string priority = job?.Priority ?? (_cmbPriority.SelectedItem?.ToString() ?? "Medium");
            _lblMeta.Text = status + " - " + jobType + " - " + techName + " - " + date + " - " + priority;
            _btnCloseJob.Enabled = !_isNewMode;
            _btnPrintReport.Enabled = !_isNewMode;
            UpdatePipelineBar(status);
        }

        private void RenderChecklistPreview(List<JobChecklistItem> items)
        {
            _currentDetail = _currentDetail ?? new JobDetailDto { Job = new Job() };
            _currentDetail.ChecklistItems = items;
            _currentDetail.ChecklistCompletedCount = items.Count(i => i.IsCompleted);
            _currentDetail.ChecklistTotalCount = items.Count;
            RenderChecklist(items);
        }

        private List<JobChecklistItem> GetPreviewChecklistItems()
        {
            return _currentDetail?.ChecklistItems?.Select(i => new JobChecklistItem
            {
                ChecklistItemId = i.ChecklistItemId,
                JobId = i.JobId,
                ItemText = i.ItemText,
                IsCompleted = i.IsCompleted,
                CompletedBy = i.CompletedBy,
                CompletedDate = i.CompletedDate,
                SortOrder = i.SortOrder
            }).ToList() ?? new List<JobChecklistItem>();
        }

        private void RenderChecklist(List<JobChecklistItem> items)
        {
            _checklistFlow.SuspendLayout();
            _checklistFlow.Controls.Clear();
            int checklistWidth = Math.Max(360, _checklistFlow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
            if (items == null || items.Count == 0)
            {
                _checklistFlow.Controls.Add(BuildJobEmptyState(ModernIconKind.Checklist, "No checklist items", "Add job tasks or load a template.", checklistWidth, 170));
                _lblChecklistCount.Text = "0 / 0 done";
                _lblChecklistCount.ForeColor = TextSecondary;
                _checklistFlow.ResumeLayout();
                return;
            }
            foreach (JobChecklistItem item in items.OrderBy(i => i.SortOrder).ThenBy(i => i.ChecklistItemId))
            {
                Panel row = new Panel { Width = checklistWidth, Height = 42, Margin = new Padding(0, 0, 0, 8), BackColor = White };
                CheckBox check = new CheckBox { Location = new Point(0, 11), Size = new Size(18, 18), Checked = item.IsCompleted, Enabled = !_isNewMode && !item.IsCompleted };
                Label lbl = new Label
                {
                    Text = item.ItemText,
                    Location = new Point(30, 8),
                    Size = new Size(Math.Max(220, checklistWidth - 42), 24),
                    Font = new Font("Segoe UI", 9.5f, item.IsCompleted ? FontStyle.Strikeout : FontStyle.Regular),
                    ForeColor = item.IsCompleted ? TextHint : TextPrimary,
                    AutoEllipsis = true
                };
                if (!_isNewMode)
                {
                    check.CheckedChanged += async (s, e) =>
                    {
                        if (check.Checked && !item.IsCompleted)
                        {
                            await Task.Run(() => _jobSvc.CompleteChecklistItem(item.ChecklistItemId));
                            await LoadJobDetailAsync(_currentDetail.Job.JobID);
                            if (_currentDetail != null && _currentDetail.IsChecklistComplete)
                                ShowChecklistBanner("Checklist complete");
                        }
                    };
                }
                row.Controls.Add(check);
                row.Controls.Add(lbl);
                _checklistFlow.Controls.Add(row);
            }
            _lblChecklistCount.Text = items.Count(i => i.IsCompleted) + " / " + items.Count + " done";
            _lblChecklistCount.ForeColor = items.Count > 0 && items.All(i => i.IsCompleted) ? TealDark : TextSecondary;
            _checklistFlow.ResumeLayout();
        }

        private void RenderParts(List<JobPartUsed> parts)
        {
            _partsFlow.SuspendLayout();
            _partsFlow.Controls.Clear();
            int partsWidth = Math.Max(360, _partsFlow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
            if (parts == null || parts.Count == 0)
            {
                _partsFlow.Controls.Add(BuildJobEmptyState(ModernIconKind.Parts, "No parts used yet", "Add material items used for this job.", partsWidth, 170));
                _lblPartsTotal.Text = "Total parts cost  " + IndiaFormatHelper.FormatCurrency(0);
                _partsFlow.ResumeLayout();
                return;
            }
            foreach (JobPartUsed part in parts)
            {
                Panel row = new Panel { Width = partsWidth, Height = 56, Margin = new Padding(0, 0, 0, 10), BackColor = White };
                Label lblName = new Label { Text = part.ItemDescription, Location = new Point(0, 4), Size = new Size(Math.Max(220, partsWidth - 260), 20), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary, AutoEllipsis = true };
                Label lblMeta = new Label { Text = part.QuantityUsed.ToString("0.###") + " " + (part.Unit ?? "Nos"), Location = new Point(0, 28), Size = new Size(180, 18), Font = new Font("Segoe UI", 8.8f), ForeColor = TextSecondary };
                Label lblCost = new Label { Text = IndiaFormatHelper.FormatCurrency(part.TotalCost), Location = new Point(Math.Max(210, partsWidth - 198), 4), Size = new Size(100, 20), TextAlign = ContentAlignment.TopRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary };
                Label pill = MakePill(GetStockLabel(part.StockStatus), GetStockBack(part.StockStatus), GetStockFore(part.StockStatus), 84);
                pill.Location = new Point(Math.Max(316, partsWidth - 90), 16);
                row.Controls.AddRange(new Control[] { lblName, lblMeta, lblCost, pill });
                _partsFlow.Controls.Add(row);
            }
            decimal total = parts.Sum(p => p.TotalCost);
            _lblPartsTotal.Text = "Total parts cost  " + IndiaFormatHelper.FormatCurrency(total);
            _partsFlow.ResumeLayout();
        }

        private void RenderNudges(List<NudgeDto> nudges)
        {
            _nudgesFlow.SuspendLayout();
            _nudgesFlow.Controls.Clear();
            int nudgeWidth = Math.Max(360, _nudgesFlow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
            if (nudges == null || nudges.Count == 0)
            {
                _nudgesFlow.Controls.Add(BuildJobEmptyState(ModernIconKind.Alert, "No alerts", "This job is currently on track.", nudgeWidth, 150));
            }
            else
            {
                foreach (NudgeDto nudge in nudges)
                    _nudgesFlow.Controls.Add(CreateNudgeStrip(nudge, nudgeWidth));
            }
            _nudgesFlow.ResumeLayout();
        }

        private static Panel BuildJobEmptyState(ModernIconKind kind, string title, string helper, int width, int height)
        {
            Panel empty = new Panel { Width = Math.Max(220, width), Height = height, BackColor = White, Margin = new Padding(0, 4, 0, 4) };
            Panel icon = ModernIconSystem.EmptyStateIcon(kind, 42, DS.Indigo50, Blue);
            icon.Location = new Point((empty.Width - icon.Width) / 2, 12);
            Label titleLabel = new Label { Text = title, Location = new Point(8, 62), Size = new Size(empty.Width - 16, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter };
            Label helperLabel = new Label { Text = helper, Location = new Point(8, 86), Size = new Size(empty.Width - 16, 34), Font = new Font("Segoe UI", 8.4f), ForeColor = TextHint, TextAlign = ContentAlignment.TopCenter };
            empty.Controls.Add(icon);
            empty.Controls.Add(titleLabel);
            empty.Controls.Add(helperLabel);
            empty.Resize += (s, e) =>
            {
                icon.Left = (empty.Width - icon.Width) / 2;
                titleLabel.Width = empty.Width - 16;
                helperLabel.Width = empty.Width - 16;
            };
            return empty;
        }

        private async Task HandlePipelineStepClickAsync(string target)
        {
            if (_isNewMode || _currentDetail == null)
                return;

            string current = NormalizePipeline(_currentDetail.Job.PipelineStatus);
            target = NormalizePipeline(target);
            if (target == current)
                return;

            if (GetPipelineRank(target) - GetPipelineRank(current) > 1)
            {
                DialogResult skipConfirm = MessageBox.Show("Move job to " + GetPipelineLabel(target) + "?", "Advance pipeline", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (skipConfirm != DialogResult.Yes)
                {
                    ResetStatusComboToCurrent();
                    return;
                }
            }

            string validationMessage;
            if (!CanAdvancePipelineFromForm(current, target, out validationMessage))
            {
                MessageBox.Show(validationMessage, "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetStatusComboToCurrent();
                return;
            }

            try
            {
                int selectedTechnicianId = GetSelectedId(_cmbTechnician);
                await Task.Run(() =>
                {
                    if (selectedTechnicianId > 0 && _currentDetail.Job.AssignedEmployeeID.GetValueOrDefault() != selectedTechnicianId)
                    {
                        Job job = _jobSvc.GetById(_currentDetail.Job.JobID);
                        if (job == null)
                            throw new InvalidOperationException("Job not found.");

                        job.AssignedEmployeeID = selectedTechnicianId;
                        _jobSvc.Update(job);
                    }

                    _jobSvc.AdvancePipeline(_currentDetail.Job.JobID, target);
                });
                await ReloadJobsAsync(_currentDetail.Job.JobID);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetStatusComboToCurrent();
            }
        }

        private bool CanAdvancePipelineFromForm(string current, string target, out string message)
        {
            message = string.Empty;
            if (_currentDetail == null || _currentDetail.Job == null)
                return false;

            int currentRank = GetPipelineRank(current);
            int targetRank = GetPipelineRank(target);
            if (targetRank <= currentRank)
                return true;

            int selectedTechnicianId = GetSelectedId(_cmbTechnician);
            bool hasTechnician = selectedTechnicianId > 0 || _currentDetail.Job.AssignedEmployeeID.HasValue;
            if (!hasTechnician && targetRank >= GetPipelineRank("Assigned"))
            {
                message = "Assign a technician before moving this job forward.";
                return false;
            }

            if (targetRank >= GetPipelineRank("ChecklistDone"))
            {
                int total = _currentDetail.ChecklistTotalCount;
                int completed = _currentDetail.ChecklistCompletedCount;
                if (total == 0 || completed < total)
                {
                    message = "Complete the checklist before moving to Checklist Done.";
                    return false;
                }
            }

            if (target == "Invoiced" && !_currentDetail.Job.InvoiceId.HasValue)
            {
                message = "Create an invoice before moving this job to Invoiced.";
                return false;
            }

            return true;
        }

        private void ResetStatusComboToCurrent()
        {
            if (_cmbStatus == null || _currentDetail == null || _currentDetail.Job == null)
                return;

            _isBinding = true;
            try
            {
                SelectText(_cmbStatus, NormalizePipeline(_currentDetail.Job.PipelineStatus), "Created");
            }
            finally
            {
                _isBinding = false;
            }
        }

        private async Task CloseJobAsync()
        {
            if (_isNewMode || _currentDetail == null)
                return;

            int remaining = _currentDetail.ChecklistTotalCount - _currentDetail.ChecklistCompletedCount;
            if (remaining > 0)
            {
                DialogResult warning = MessageBox.Show(remaining + " checklist items are not completed. Close anyway?", "Incomplete checklist", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (warning != DialogResult.Yes)
                    return;
            }

            using (CloseJobDialog dialog = new CloseJobDialog(_currentDetail))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    int invoiceId = await Task.Run(() => _jobSvc.CloseJob(_currentDetail.Job.JobID, dialog.ActualRevenue, dialog.CloseNotes, dialog.GenerateInvoice));
                    await ReloadJobsAsync(_currentDetail.Job.JobID);
                    if (dialog.GenerateInvoice && invoiceId > 0)
                    {
                        MessageBox.Show("Invoice created successfully from this job.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Closing job", ex);
                }
            }
        }

        private void PrintReport()
        {
            if (_isNewMode || _currentDetail == null)
                return;

            try
            {
                string dir = @"C:\HVAC_PRO_MSE\REPORTS\Jobs";
                Directory.CreateDirectory(dir);
                string pdfPath = Path.Combine(dir, (_currentDetail.Job.JobNumber ?? "job-report") + ".pdf");
                ExportHtmlToPdf(BuildJobReportHtml(_currentDetail), pdfPath);
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Printing job report", ex);
            }
        }

        private void ExportCalendarEvent()
        {
            if (_currentDetail == null || _currentDetail.Job == null)
            {
                MessageBox.Show("Select or save a job before exporting a calendar event.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            IntegrationOperationResult result = new CalendarDispatchIntegrationService().ExportJobIcs(_currentDetail.Job);
            if (!result.Success)
            {
                MessageBox.Show("Calendar export failed: " + result.Message, "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show("Calendar event exported to " + result.LocalPath, "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
            try { Process.Start(new ProcessStartInfo(result.LocalPath) { UseShellExecute = true }); }
            catch (Exception ex) { AppLogger.LogError("JobManagementForm.ExportCalendarEvent.Open", ex); }
        }

        private void ShowJobWhatsAppAction()
        {
            if (_currentDetail == null || _currentDetail.Job == null || _currentDetail.Job.JobID == 0)
            {
                MessageBox.Show("Select or save a job before preparing a WhatsApp update.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Job job = _currentDetail.Job;
            string clientName = _currentDetail.Client == null || string.IsNullOrWhiteSpace(_currentDetail.Client.CompanyName)
                ? FirstNonEmpty(job.ClientName, "Customer")
                : _currentDetail.Client.CompanyName;
            string phone = _currentDetail.Client == null ? string.Empty : _currentDetail.Client.Phone;
            string jobNo = FirstNonEmpty(job.JobNumber, "JOB-" + job.JobID.ToString("0000"));
            string technician = _currentDetail.Technician == null || string.IsNullOrWhiteSpace(_currentDetail.Technician.Name)
                ? FirstNonEmpty(job.AssignedEmployeeName, "Assigned technician")
                : _currentDetail.Technician.Name;
            string status = FirstNonEmpty(job.PipelineStatus, job.Status, "Created");
            string message = "Hi " + clientName + ",\r\n\r\nUpdate for service job " + jobNo
                + ": current status is " + status + ". Technician: " + technician
                + ". Scheduled date: " + job.ScheduledDate.ToString("dd MMM yyyy") + ".\r\n\r\nRegards,\r\nServoERP";

            WhatsAppQuickActionDialog.ShowFor(this, new WhatsAppQuickActionContext
            {
                Module = "Jobs",
                SourceId = job.JobID,
                ContactName = clientName,
                Phone = phone,
                TemplateType = "Job update",
                Message = message,
                LinkedRecordType = "Job",
                LinkedRecord = jobNo,
                LinkedRecordId = job.JobID
            });
        }

        private void OpenJobForms()
        {
            string query = "job card work order service completion checklist parts used customer sign-off";
            if (_currentDetail != null && _currentDetail.Job != null)
            {
                query = string.Join(" ", new[]
                {
                    query,
                    _currentDetail.Job.Title,
                    _currentDetail.Job.JobType,
                    _currentDetail.Job.Priority,
                    _currentDetail.Job.Status
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            FormTemplateWorkflowLauncher.Open(this, "Jobs", "Jobs", "HVAC", query);
        }

        private async Task AutoSaveNotesAsync()
        {
            if (_isNewMode || _currentDetail == null)
                return;
            try
            {
                string notes = GetTextValue(_txtNotes);
                await Task.Run(() => _jobSvc.UpdateNotes(_currentDetail.Job.JobID, notes));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("Job notes autosave", ex);
            }
        }

        private void UpdatePipelineBar(string pipelineStatus)
        {
            string normalized = NormalizePipeline(pipelineStatus);
            foreach (string step in _pipelineSteps)
            {
                if (_pipelineStepPanels.TryGetValue(step, out Panel panel))
                    panel.Invalidate();
            }
        }

        private void LayoutCards()
        {
            if (_cardsHost == null || _detailScroll == null)
                return;

            int contentWidth = Math.Max(760, _detailScroll.ClientSize.Width - _detailScroll.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 24);
            _cardsHost.Width = contentWidth;

            int gap = 22;
            int y = 0;

            int jobDetailsHeight = ResolveCardHeight(_cardJobDetails, 560);
            SetCardBounds(_cardJobDetails, 0, y, contentWidth, jobDetailsHeight);
            y += jobDetailsHeight + gap;

            int technicianHeight = ResolveCardHeight(_cardTechnician, 300);
            SetCardBounds(_cardTechnician, 0, y, contentWidth, technicianHeight);
            y += technicianHeight + gap;

            int checklistHeight = ResolveCardHeight(_cardChecklist, 360);
            SetCardBounds(_cardChecklist, 0, y, contentWidth, checklistHeight);
            y += checklistHeight + gap;

            int partsHeight = ResolveCardHeight(_cardParts, 360);
            SetCardBounds(_cardParts, 0, y, contentWidth, partsHeight);
            y += partsHeight + gap;

            int nudgesHeight = ResolveCardHeight(_cardNudges, 260);
            SetCardBounds(_cardNudges, 0, y, contentWidth, nudgesHeight);
            y += nudgesHeight + gap;

            int notesHeight = ResolveCardHeight(_cardNotes, 220);
            SetCardBounds(_cardNotes, 0, y, contentWidth, notesHeight);
            _cardsHost.Height = y + notesHeight + gap;
            _detailScroll.AutoScrollMinSize = new Size(0, _cardsHost.Height + _detailScroll.Padding.Vertical + 24);
            _detailScroll.HorizontalScroll.Value = 0;
            _detailScroll.HorizontalScroll.Maximum = 0;
            RemoveJobEditorOverlayArtifacts();
        }

        private void ResetEditorScrollToTop()
        {
            if (_detailScroll == null || _cardsHost == null)
                return;

            _detailScroll.AutoScrollPosition = Point.Empty;
            _cardsHost.Location = new Point(0, 0);
        }

        private static void SetCardBounds(Control card, int x, int y, int width, int height)
        {
            card.Bounds = new Rectangle(x, y, width, height);
        }

        private void RemoveJobEditorOverlayArtifacts()
        {
            RemoveJobEditorOverlayArtifacts(_rightPanel);
            RemoveJobEditorOverlayArtifacts(_detailScroll);
            RemoveJobEditorOverlayArtifacts(_cardsHost);
        }

        private static void RemoveJobEditorOverlayArtifacts(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            foreach (Control child in root.Controls.Cast<Control>().ToList())
            {
                if (IsJobEditorOverlayArtifact(child))
                {
                    root.Controls.Remove(child);
                    child.Dispose();
                    continue;
                }

                CardResizeGripService.Detach(child);
                RemoveJobEditorOverlayArtifacts(child);
            }
        }

        private static bool IsJobEditorOverlayArtifact(Control control)
        {
            string name = control == null ? string.Empty : (control.Name ?? string.Empty);
            return string.Equals(name, CardResizeGripService.CornerGripName, StringComparison.Ordinal) ||
                   string.Equals(name, CardResizeGripService.HeightGripName, StringComparison.Ordinal) ||
                   string.Equals(name, CardResizeGripService.LockBadgeName, StringComparison.Ordinal);
        }

        private int ResolveCardHeight(Control card, int defaultHeight)
        {
            _cardDefaultHeights[card] = defaultHeight;
            int expandedHeight;
            if (_cardExpandedHeights.TryGetValue(card, out expandedHeight))
                return Math.Max(defaultHeight, expandedHeight);

            return defaultHeight;
        }

        private int GetExpandedCardHeight(int defaultHeight)
        {
            return Math.Min(680, defaultHeight + Math.Max(120, defaultHeight / 2));
        }

        private void ToggleCardExpanded(Control card, Button button)
        {
            int defaultHeight;
            if (!_cardDefaultHeights.TryGetValue(card, out defaultHeight))
                defaultHeight = Math.Max(170, card.Height);

            int expandedHeight;
            if (_cardExpandedHeights.TryGetValue(card, out expandedHeight) && expandedHeight > defaultHeight)
            {
                _cardExpandedHeights.Remove(card);
                button.Text = "Extend";
            }
            else
            {
                _cardExpandedHeights[card] = GetExpandedCardHeight(defaultHeight);
                button.Text = "Collapse";
            }

            LayoutCards();
        }

        private void QueueCardOverflowHint(Panel body, Label overflowLabel)
        {
            if (IsDisposed)
                return;

            try
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)(() => UpdateCardOverflowHint(body, overflowLabel)));
                else
                    UpdateCardOverflowHint(body, overflowLabel);
            }
            catch
            {
            }
        }

        private static void UpdateCardOverflowHint(Panel body, Label overflowLabel)
        {
            bool overflow = body.VerticalScroll.Visible
                || body.HorizontalScroll.Visible
                || body.DisplayRectangle.Height > body.ClientSize.Height + 4
                || body.DisplayRectangle.Width > body.ClientSize.Width + 4;
            overflowLabel.Visible = false;
        }


        private Panel CreateCard(string title, out Panel body) => CreateCard(title, out body, out _);

        private Panel CreateCard(string title, out Panel body, out Label actionLabel)
        {
            Panel card = new Panel { BackColor = White, Tag = "FIXED_EDITOR_SECTION NO_DASHBOARD_RESIZE JOB_EDITOR_CARD" };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, White, Border, 10);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = White, Padding = new Padding(20, 10, 20, 10) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(BorderLight))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Panel titleWrap = new Panel { Dock = DockStyle.Fill, BackColor = White };
            Label lblTitle = new Label { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            Label titleIcon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 26, DS.Indigo50, Blue, 8);
            titleIcon.Dock = DockStyle.Left;
            titleWrap.Controls.Add(lblTitle);
            titleWrap.Controls.Add(titleIcon);
            actionLabel = new Label { Dock = DockStyle.Right, Width = 140, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight };
            Label overflowLabel = new Label { Dock = DockStyle.Right, Width = 0, Font = new Font("Segoe UI", 8f), ForeColor = TextHint, Text = string.Empty, TextAlign = ContentAlignment.MiddleRight, Visible = false };
            Button btnExtend = new Button
            {
                Dock = DockStyle.Right,
                Width = 0,
                Height = 24,
                Text = string.Empty,
                FlatStyle = FlatStyle.Flat,
                BackColor = White,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnExtend.FlatAppearance.BorderColor = Border;
            btnExtend.FlatAppearance.BorderSize = 1;
            btnExtend.FlatAppearance.MouseOverBackColor = Surface;
            Panel headerRight = new Panel { Dock = DockStyle.Right, Width = 152, BackColor = White };
            header.Resize += (s, e) =>
            {
                headerRight.Width = Math.Min(170, Math.Max(120, header.Width / 5));
            };
            headerRight.Controls.Add(btnExtend);
            headerRight.Controls.Add(overflowLabel);
            headerRight.Controls.Add(actionLabel);
            headerRight.Visible = false;
            actionLabel.Visible = false;
            Label actionText = actionLabel;
            actionText.TextChanged += (s, e) =>
            {
                bool showAction = !string.IsNullOrWhiteSpace(actionText.Text);
                actionText.Visible = showAction;
                headerRight.Visible = showAction;
            };
            header.Controls.Add(headerRight);
            header.Controls.Add(titleWrap);

            body = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(22, 18, 22, 22), AutoScroll = false, Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            Panel cardBody = body;
            cardBody.Layout += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            cardBody.Resize += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            cardBody.ControlAdded += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            cardBody.ControlRemoved += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            btnExtend.Click += (s, e) => ToggleCardExpanded(card, btnExtend);
            card.Controls.Add(cardBody);
            card.Controls.Add(header);
            return card;
        }

        private void HandleCardResizeComplete(Control card, Size size)
        {
            if (card == null)
                return;

            int defaultHeight = card.Height;
            if (_cardDefaultHeights.ContainsKey(card))
                defaultHeight = _cardDefaultHeights[card];

            _cardExpandedHeights[card] = Math.Max(defaultHeight, size.Height);
            LayoutCards();
        }

        private Control BuildFormRow(string label, out ComboBox combo)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = White, Padding = new Padding(0, 0, 0, 10), Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft });
            combo = new ComboBox { Dock = DockStyle.Bottom, Height = 32, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
            combo.MinimumSize = new Size(0, 32);
            row.Controls.Add(combo);
            return row;
        }

        private Control BuildSiteFormRow(string label, out ComboBox combo, out Button addButton)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = White, Padding = new Padding(0, 0, 0, 10), Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft });
            Panel inputRow = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = White, Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            addButton = MakeInlineButton("+ Add", Blue, 86);
            addButton.Dock = DockStyle.Right;
            addButton.Margin = Padding.Empty;
            combo = new ComboBox { Dock = DockStyle.Fill, Height = 32, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
            combo.MinimumSize = new Size(0, 32);
            inputRow.Controls.Add(combo);
            inputRow.Controls.Add(addButton);
            row.Controls.Add(inputRow);
            return row;
        }

        private Control BuildDateRow(string label, out DateTimePicker picker)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = White, Padding = new Padding(0, 0, 0, 10), Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft });
            picker = new DateTimePicker { Dock = DockStyle.Bottom, Height = 32, Font = new Font("Segoe UI", 9.5f), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            picker.MinimumSize = new Size(0, 32);
            row.Controls.Add(picker);
            return row;
        }

        private Control BuildTextRow(string label, out TextBox textBox, bool readOnly)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = White, Padding = new Padding(0, 0, 0, 10), Tag = "NO_INPUT_HOST NO_CARD_SURFACE" };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft });
            textBox = new TextBox { Dock = DockStyle.Bottom, Height = 32, Font = new Font("Segoe UI", 9.5f), ReadOnly = readOnly, BackColor = readOnly ? Surface : White };
            textBox.MinimumSize = new Size(0, 32);
            row.Controls.Add(textBox);
            return row;
        }

        private static Button MakeHeaderButton(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static Button MakeInlineButton(string text, Color backColor, int width)
        {
            Button button = MakeHeaderButton(text, backColor, White, width);
            button.Height = 34;
            return button;
        }

        private static Label MakePill(string text, Color backColor, Color foreColor, int width)
        {
            return new Label
            {
                Text = text,
                Size = new Size(width, 20),
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Control CreateNudgeStrip(NudgeDto nudge, int width)
        {
            Color borderColor = Blue;
            Color backColor = BlueLightBg;
            Color titleColor = BlueDark;
            Color bodyColor = Blue;
            if (nudge.NudgeType == "Success") { borderColor = Teal; backColor = TealLightBg; titleColor = TealDark; bodyColor = Teal; }
            else if (nudge.NudgeType == "Warning") { borderColor = Amber; backColor = AmberLightBg; titleColor = AmberDark; bodyColor = AmberDark; }
            else if (nudge.NudgeType == "Danger") { borderColor = Red; backColor = RedLightBg; titleColor = RedDark; bodyColor = Red; }

            Panel strip = new Panel { Width = Math.Max(360, width), Height = 74, BackColor = backColor, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(14, 10, 14, 10) };
            strip.Paint += (s, e) =>
            {
                using (SolidBrush brush = new SolidBrush(borderColor))
                    e.Graphics.FillRectangle(brush, 0, 0, 3, strip.Height);
            };
            Label title = new Label { Text = nudge.Title, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = titleColor, AutoEllipsis = true };
            Label body = new Label { Text = nudge.Body, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = bodyColor, AutoEllipsis = true };
            strip.Controls.Add(body);
            strip.Controls.Add(title);
            return strip;
        }

        private void UpdateSearchClear()
        {
            _btnSearchClear.Visible = !string.IsNullOrWhiteSpace(GetSearchText());
        }

        private string GetSearchText()
        {
            const string placeholder = "Search jobs, clients, technicians...";
            string text = _txtSearch.Text.Trim();
            return string.Equals(text, placeholder, StringComparison.OrdinalIgnoreCase) || IsPlaceholder(_txtSearch, placeholder)
                ? string.Empty
                : text;
        }

        private void UpdatePartStockHint()
        {
            StockItem stock = ResolveSelectedPartStock();
            if (stock == null)
            {
                _lblPartStockHint.Text = "Search material item name";
                return;
            }

            if (_numPartRate != null && !_numPartRate.Focused)
            {
                decimal rate = Math.Max(_numPartRate.Minimum, Math.Min(_numPartRate.Maximum, stock.LastPurchaseRate));
                _numPartRate.Value = rate;
            }

            _lblPartStockHint.Text = "Available qty: " + stock.AvailableStock.ToString("0.###") + " " + (stock.Unit ?? "Nos")
                + " - Rate " + IndiaFormatHelper.FormatCurrency(stock.LastPurchaseRate)
                + " (edit Rate to update globally)";
        }

        private StockItem ResolveSelectedPartStock()
        {
            if (_cmbPartSearch == null)
                return null;

            LookupItem<int?> selected = _cmbPartSearch.SelectedItem as LookupItem<int?>;
            if (selected != null && selected.Value.HasValue)
            {
                StockItem byId = _inventory.FirstOrDefault(i => i.ItemID == selected.Value.Value);
                if (byId != null)
                    return byId;

                return _inventorySvc.GetById(selected.Value.Value);
            }

            string text = (_cmbPartSearch.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            StockItem exactName = _inventory.FirstOrDefault(i => string.Equals(i.ItemName, text, StringComparison.OrdinalIgnoreCase));
            if (exactName != null)
                return exactName;

            StockItem exactDisplay = _inventory.FirstOrDefault(i => string.Equals(BuildPartLookupText(i), text, StringComparison.OrdinalIgnoreCase));
            if (exactDisplay != null)
                return exactDisplay;

            return _inventory.FirstOrDefault(i => i.ItemName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildPartLookupText(StockItem item)
        {
            if (item == null)
                return string.Empty;

            return item.ItemName + " (" + item.AvailableStock.ToString("0.###") + " " + (item.Unit ?? "Nos") + ")";
        }

        private void ShowChecklistBanner(string text)
        {
            _lblChecklistBanner.Text = "  " + text;
            _lblChecklistBanner.Visible = true;
            Timer timer = new Timer { Interval = 1800 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                _lblChecklistBanner.Visible = false;
            };
            timer.Start();
        }

        private string BuildJobReportHtml(JobDetailDto detail)
        {
            IndiaCompanySettings settings = _settingsSvc.GetIndiaCompanySettings();
            StringBuilder checklist = new StringBuilder();
            foreach (JobChecklistItem item in detail.ChecklistItems)
                checklist.Append("<tr><td>").Append(item.IsCompleted ? "&#10003;" : "&#10007;").Append("</td><td>")
                    .Append(Html(item.ItemText)).Append("</td><td>").Append(item.CompletedDate.HasValue ? Html(item.CompletedDate.Value.ToString("dd/MM/yyyy hh:mm tt")) : "-").Append("</td></tr>");

            StringBuilder parts = new StringBuilder();
            foreach (JobPartUsed part in detail.PartsUsed)
                parts.Append("<tr><td>").Append(Html(part.ItemDescription)).Append("</td><td>").Append(part.QuantityUsed.ToString("0.###")).Append("</td><td>").Append(Html(part.Unit)).Append("</td><td>").Append(IndiaFormatHelper.FormatCurrency(part.TotalCost)).Append("</td></tr>");

            return "<html><head><meta charset='utf-8'/><style>"
                + "body{font-family:Segoe UI,Arial,sans-serif;color:#1A1A1A;padding:24px;}h1{font-size:22px;margin:0 0 4px;}h2{font-size:15px;margin:18px 0 8px;}table{width:100%;border-collapse:collapse;}td,th{padding:8px;border:1px solid #E8E8E8;font-size:12px;} .meta{margin:3px 0;font-size:12px;color:#6B6B6B;} .sign{margin-top:28px;display:flex;justify-content:space-between;} .line{margin-top:48px;border-top:1px solid #666;width:220px;}"
                + "</style></head><body>"
                + "<h1>" + Html(settings.CompanyName) + "</h1>"
                + "<div class='meta'>" + Html(settings.Address) + "</div>"
                + "<div class='meta'>GSTIN: " + Html(settings.GSTIN) + " | Phone: " + Html(settings.Phone) + "</div>"
                + "<h2>SERVICE REPORT</h2>"
                + "<div class='meta'>Job Number: " + Html(detail.Job.JobNumber) + " | Job Type: " + Html(detail.Job.JobType) + " | Date: " + Html(IndiaFormatHelper.FormatDate(detail.Job.ScheduledDate)) + " | Technician: " + Html(detail.Technician?.Name ?? "Unassigned") + "</div>"
                + "<div class='meta'>Client: " + Html(detail.Client?.CompanyName) + " | Site: " + Html(detail.Site?.SiteName) + " | Contract: " + Html(detail.Contract != null ? ("AMC-" + detail.Contract.ContractID) : "-") + "</div>"
                + "<h2>Checklist</h2><table><tr><th>Status</th><th>Item</th><th>Completion time</th></tr>" + checklist + "</table>"
                + "<h2>Parts used</h2><table><tr><th>Item</th><th>Qty</th><th>Unit</th><th>Cost</th></tr>" + parts + "</table>"
                + "<div class='meta'>Total parts cost: " + IndiaFormatHelper.FormatCurrency(detail.PartsCost) + "</div>"
                + "<h2>Cost summary</h2>"
                + "<div class='meta'>Quoted revenue: " + IndiaFormatHelper.FormatCurrency(detail.Job.QuotedRevenue) + "</div>"
                + "<div class='meta'>Estimated cost: " + IndiaFormatHelper.FormatCurrency(detail.LabourCost + detail.PartsCost + detail.TravelCost) + "</div>"
                + "<div class='meta'>Margin: " + detail.EstimatedMarginPct.ToString("0.0") + "%</div>"
                + "<h2>Notes</h2><div class='meta'>" + Html(detail.Job.Notes) + "</div>"
                + "<div class='sign'><div><div class='line'></div><div class='meta'>Technician signature</div></div><div><div class='line'></div><div class='meta'>Client signature</div></div></div>"
                + "<div class='meta' style='margin-top:24px;'>Generated by " + Html(BrandingService.AppName) + " - " + Html(BrandingService.Subtitle) + " - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "</div>"
                + "</body></html>";
        }

        private static void ExportHtmlToPdf(string html, string pdfPath)
        {
            HtmlPdfExportService.ExportHtmlToPdf(html, pdfPath);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return string.Empty;
        }

        private static void DrawRoundedBorder(Graphics graphics, Rectangle bounds, Color fillColor, Color borderColor, int radius)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;
            Rectangle rect = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = BuildRoundedPath(rect, radius))
            using (SolidBrush brush = new SolidBrush(fillColor))
            using (Pen pen = new Pen(borderColor))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawPipelineStep(Graphics graphics, Panel panel, string step)
        {
            string current = NormalizePipeline(_currentDetail?.Job?.PipelineStatus ?? (_cmbStatus.SelectedItem?.ToString() ?? "Created"));
            int stepRank = GetPipelineRank(step);
            int currentRank = GetPipelineRank(current);
            bool done = stepRank < currentRank;
            bool active = stepRank == currentRank;
            Color circleFill = done ? Teal : White;
            Color circleBorder = done || active ? Teal : Border;
            Color textColor = done || active ? TealDark : TextHint;

            if (active)
            {
                using (SolidBrush brush = new SolidBrush(TealLightBg))
                    graphics.FillRectangle(brush, panel.ClientRectangle);
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle circle = new Rectangle(0, 6, 18, 18);
            using (SolidBrush brush = new SolidBrush(circleFill))
            using (Pen pen = new Pen(circleBorder))
            {
                graphics.FillEllipse(brush, circle);
                graphics.DrawEllipse(pen, circle);
            }
            string text = done ? "✓" : stepRank.ToString();
            TextRenderer.DrawText(graphics, text, new Font("Segoe UI", 8f, FontStyle.Bold), circle, done ? White : textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(graphics, GetPipelineLabel(step), new Font("Segoe UI", 8.5f, done || active ? FontStyle.Bold : FontStyle.Regular), new Rectangle(24, 7, panel.Width - 24, 18), textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private static string GetPipelineLabel(string pipeline)
        {
            switch (NormalizePipeline(pipeline))
            {
                case "InProgress": return "In Progress";
                case "ChecklistDone": return "Checklist Done";
                default: return NormalizePipeline(pipeline);
            }
        }

        private static string NormalizePipeline(string pipeline)
        {
            string normalized = (pipeline ?? string.Empty).Replace(" ", string.Empty).Trim();
            switch (normalized.ToUpperInvariant())
            {
                case "CREATED": return "Created";
                case "ASSIGNED": return "Assigned";
                case "INPROGRESS": return "InProgress";
                case "CHECKLISTDONE": return "ChecklistDone";
                case "CLOSED": return "Closed";
                case "INVOICED": return "Invoiced";
                case "COMPLETED": return "Closed";
                default: return "Created";
            }
        }

        private static int GetPipelineRank(string pipeline)
        {
            switch (NormalizePipeline(pipeline))
            {
                case "Created": return 1;
                case "Assigned": return 2;
                case "InProgress": return 3;
                case "ChecklistDone": return 4;
                case "Closed": return 5;
                case "Invoiced": return 6;
                default: return 0;
            }
        }

        private static string GetStockLabel(string stockStatus)
        {
            switch ((stockStatus ?? string.Empty).Trim())
            {
                case "LowStock": return "Order soon";
                case "OutOfStock": return "Supplier needed";
                default: return "Planned";
            }
        }

        private static Color GetStockBack(string stockStatus) => string.Equals(stockStatus, "InStock", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#EAF3DE") : RedLightBg;
        private static Color GetStockFore(string stockStatus) => string.Equals(stockStatus, "InStock", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#27500A") : RedDark;
        private static Color GetMarginColor(decimal pct) => pct >= 25m ? ColorTranslator.FromHtml("#3B6D11") : (pct >= 15m ? AmberDark : (pct <= 0m ? TextHint : RedDark));
        private static string GetInitials(string name) => string.IsNullOrWhiteSpace(name) ? "--" : string.Concat(name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0]))).PadRight(2, '-');

        private static Color GetTechnicianColor(int employeeId)
        {
            Color[] palette = { Teal, Blue, Purple, Amber, Red, TealDark, BlueDark };
            return palette[Math.Abs(employeeId) % palette.Length];
        }

        private static Color GetStatusPillBack(JobSummaryDto job)
        {
            if (job.IsOverdue) return RedLightBg;
            switch (NormalizePipeline(job.PipelineStatus))
            {
                case "Created":
                case "Assigned": return AmberLightBg;
                case "InProgress":
                case "ChecklistDone": return TealLightBg;
                case "Closed": return ColorTranslator.FromHtml("#EAF3DE");
                case "Invoiced": return BlueLightBg;
                default: return Surface;
            }
        }

        private static Color GetStatusPillFore(JobSummaryDto job)
        {
            if (job.IsOverdue) return RedDark;
            switch (NormalizePipeline(job.PipelineStatus))
            {
                case "Created":
                case "Assigned": return AmberDark;
                case "InProgress":
                case "ChecklistDone": return TealDark;
                case "Closed": return ColorTranslator.FromHtml("#27500A");
                case "Invoiced": return BlueDark;
                default: return TextSecondary;
            }
        }

        private static Color GetJobTypeBack(string jobType)
        {
            switch ((jobType ?? string.Empty).Trim())
            {
                case "PM Visit": return BlueLightBg;
                case "Breakdown": return RedLightBg;
                case "Installation": return PurpleLight;
                case "AMC Visit": return TealLightBg;
                case "Gas Charging": return AmberLightBg;
                default: return Surface;
            }
        }

        private static Color GetJobTypeFore(string jobType)
        {
            switch ((jobType ?? string.Empty).Trim())
            {
                case "PM Visit": return BlueDark;
                case "Breakdown": return RedDark;
                case "Installation": return ColorTranslator.FromHtml("#3C3489");
                case "AMC Visit": return ColorTranslator.FromHtml("#085041");
                case "Gas Charging": return AmberDark;
                default: return TextSecondary;
            }
        }

        private static bool Contains(string value, string search) => !string.IsNullOrWhiteSpace(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        private void SetListStatus(string text)
        {
            if (_lblListStatus == null || _lblListStatus.IsDisposed)
                return;
            _lblListStatus.Text = text;
        }
        private static int GetSelectedId(ComboBox combo) => combo.SelectedItem is LookupItem<int> item ? item.Value : 0;
        private static string GetSelectedText(ComboBox combo, string fallback) => combo.SelectedItem?.ToString() ?? fallback;

        private static decimal ParseMoney(string text)
        {
            decimal value;
            return decimal.TryParse((text ?? string.Empty).Replace("₹", string.Empty).Replace("Rs", string.Empty).Replace(",", string.Empty).Trim(), out value) ? value : 0m;
        }

        private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty).Replace(Environment.NewLine, "<br/>");
        private static DateTime GetWeekStart(DateTime date) => date.Date.AddDays(-(int)((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7));
        private static void SelectText(ComboBox combo, string value, string fallback) { string target = string.IsNullOrWhiteSpace(value) ? fallback : value; for (int i = 0; i < combo.Items.Count; i++) if (string.Equals(combo.Items[i].ToString(), target, StringComparison.OrdinalIgnoreCase)) { combo.SelectedIndex = i; return; } if (combo.Items.Count > 0) combo.SelectedIndex = 0; }
        private static void SelectLookup(ComboBox combo, int id) { for (int i = 0; i < combo.Items.Count; i++) if (combo.Items[i] is LookupItem<int> item && item.Value == id) { combo.SelectedIndex = i; return; } if (combo.Items.Count > 0) combo.SelectedIndex = 0; }
        private static void SetTextBoxValue(TextBox textBox, string value, bool placeholder) { textBox.ForeColor = placeholder ? TextHint : TextPrimary; textBox.Text = placeholder ? value : (value ?? string.Empty); }
        private string GetTextValue(TextBox textBox) => IsPlaceholder(textBox, "Add job notes, observations, or client instructions...") ? string.Empty : textBox.Text.Trim();

        private void ConfigurePlaceholder(TextBox textBox, string placeholder)
        {
            textBox.Enter += (s, e) =>
            {
                if (_settingPlaceholder) return;
                if (IsPlaceholder(textBox, placeholder))
                {
                    _settingPlaceholder = true;
                    textBox.Text = string.Empty;
                    textBox.ForeColor = TextPrimary;
                    _settingPlaceholder = false;
                }
            };
            textBox.Leave += (s, e) =>
            {
                if (_settingPlaceholder) return;
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    _settingPlaceholder = true;
                    textBox.Text = placeholder;
                    textBox.ForeColor = TextHint;
                    _settingPlaceholder = false;
                }
            };
            textBox.Text = placeholder;
            textBox.ForeColor = TextHint;
        }

        private static bool IsPlaceholder(TextBox textBox, string placeholder) => textBox.ForeColor == TextHint && string.Equals(textBox.Text, placeholder, StringComparison.Ordinal);

        private static void AddClickable(Control root, Func<Task> onClick)
        {
            root.Click += async (s, e) => await onClick();
            foreach (Control child in root.Controls)
                AddClickable(child, onClick);
        }

        private class LookupItem<T>
        {
            public LookupItem(T value, string text) { Value = value; Text = text; }
            public T Value { get; private set; }
            public string Text { get; private set; }
            public override string ToString() => Text;
        }

        private class CloseJobDialog : ServoERP.Infrastructure.ServoFormBase
        {
            public decimal ActualRevenue { get; private set; }
            public string CloseNotes { get; private set; }
            public bool GenerateInvoice { get; private set; }

            public CloseJobDialog(JobDetailDto detail)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Closing job: " + (detail.Job.JobTitle ?? detail.Job.Title ?? detail.Job.JobNumber);
                Size = new Size(560, 340);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = White;
                Font = new Font("Segoe UI", 9f);

                Label lblRevenue = new Label { Text = "Actual revenue", Location = new Point(20, 20), Size = new Size(120, 18) };
                TextBox txtRevenue = new TextBox { Location = new Point(20, 42), Width = 180, Text = (detail.Job.QuotedRevenue > 0 ? detail.Job.QuotedRevenue : detail.Job.Revenue).ToString("0.##") };
                Label lblHours = new Label { Text = "Hours worked", Location = new Point(220, 20), Size = new Size(120, 18) };
                NumericUpDown numHours = new NumericUpDown { Location = new Point(220, 42), Width = 120, DecimalPlaces = 1, Minimum = 0, Maximum = 1000, Value = 4 };
                Label lblParts = new Label { Text = "Parts used: " + detail.PartsUsed.Count + " - " + IndiaFormatHelper.FormatCurrency(detail.PartsCost), Location = new Point(20, 82), Size = new Size(360, 18), ForeColor = TextSecondary };
                Label lblNotes = new Label { Text = "Notes", Location = new Point(20, 112), Size = new Size(120, 18) };
                TextBox txtNotes = new TextBox { Location = new Point(20, 134), Width = 500, Height = 88, Multiline = true };

                Button btnCancel = MakeHeaderButton("Cancel", Surface, TextPrimary, 90);
                btnCancel.Location = new Point(20, 242);
                btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

                Button btnCloseOnly = MakeHeaderButton("Close Job Only", Teal, White, 132);
                btnCloseOnly.Location = new Point(160, 242);
                btnCloseOnly.Click += (s, e) =>
                {
                    ActualRevenue = ParseMoney(txtRevenue.Text);
                    CloseNotes = txtNotes.Text.Trim();
                    GenerateInvoice = false;
                    DialogResult = DialogResult.OK;
                };

                Button btnCloseInvoice = MakeHeaderButton("Close Job & Generate Invoice", Red, White, 226);
                btnCloseInvoice.Location = new Point(294, 242);
                btnCloseInvoice.Click += (s, e) =>
                {
                    ActualRevenue = ParseMoney(txtRevenue.Text);
                    CloseNotes = txtNotes.Text.Trim();
                    GenerateInvoice = true;
                    DialogResult = DialogResult.OK;
                };

                Controls.AddRange(new Control[] { lblRevenue, txtRevenue, lblHours, numHours, lblParts, lblNotes, txtNotes, btnCancel, btnCloseOnly, btnCloseInvoice });
                CancelButton = btnCancel;
                AcceptButton = btnCloseOnly;
            }
        }
    }
}


