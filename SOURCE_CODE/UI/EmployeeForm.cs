using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Integrations;

namespace HVAC_Pro_Desktop.UI
{
    public class EmployeeForm : DeferredPageControl
    {
        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly SiteService _siteService = new SiteService();
        private readonly PayrollService _payrollService = new PayrollService();
        private readonly DatabaseManager _db = new DatabaseManager();

        private readonly Color PageBg = DS.BgPage;
        private readonly Color CardBg = DS.White;
        private readonly Color Border = DS.Border;
        private readonly Color Surface = DS.Slate50;
        private readonly Color Teal = DS.Teal600;
        private readonly Color TealLight = DS.Teal50;
        private readonly Color Amber = DS.Amber500;
        private readonly Color AmberLight = DS.Amber50;
        private readonly Color Red = DS.Red500;
        private readonly Color Blue = DS.Primary600;
        private readonly Color TextPrimary = DS.Slate900;
        private readonly Color TextSecondary = DS.Slate500;
        private readonly Color TextHint = DS.Slate400;
        private const int PageEdgeGap = 8;
        private const int SectionGap = 8;
        private const int CardGap = 8;
        private const int InnerPadding = 12;

        private List<EmployeeSummaryDto> _employeeSummaries = new List<EmployeeSummaryDto>();
        private List<EmployeeSkillDto> _expiringSkills = new List<EmployeeSkillDto>();
        private HashSet<int> _checkedInTodayEmployeeIds = new HashSet<int>();
        private Employee _currentEmployee;
        private EmployeeSalaryProfileDto _currentSalaryProfile;
        private byte[] _currentPhoto;
        private bool _suppressEmployeeFilterEvents;
        private readonly Timer _employeeSearchDebounceTimer = new Timer();
        private int _tabDataEmployeeId;
        private bool _jobsLoaded;
        private bool _attendanceLoaded;
        private bool _skillsLoaded;
        private bool _documentsLoaded;
        private bool _payrollLoaded;
        private bool _initialLoadInProgress;
        private int _pendingRestoreEmployeeId;
        private int _pendingRestoreTabIndex;
        private DataTable _currentEmployeeTable;

        private Button _btnNew;
        private Button _btnSave;
        private Button _btnDelete;
        private Button _btnExport;
        private Button _btnImport;
        private Button _btnTemplate;
        private Button _btnWhatsapp;
        private LinkLabel _lnkExpiringBanner;
        private Label _lblTotalEmployees;
        private Label _lblActiveToday;
        private Label _lblOnDuty;
        private Label _lblOnLeave;
        private Label _lblStatus;
        private TextBox _txtSearch;
        private ComboBox _cmbClientFilter;
        private ComboBox _cmbStatusFilter;
        private DataGridView _gridEmployees;
        private TabControl _tabs;
        private Label _lblVisibleEmployees;
        private Label _lblNeedsFollowUp;
        private Label _lblCheckedInNow;
        private Label _lblActionMissingKyc;
        private Label _lblActionMissingEmergency;
        private Label _lblActionProbationDue;
        private Label _lblActionPayrollBlocked;
        private Label _lblReadinessHeadline;
        private Label _lblReadinessDetail;
        private Label _lblHeroEmployeeName;
        private Label _lblHeroEmployeeMeta;
        private Label _lblHeroStatusChip;
        private Label _lblHeroSiteChip;
        private Label _lblHeroReadinessChip;
        private Label _lblHeroPayrollChip;
        private Label _lblHeroContactChip;
        private Label _lblProfileChecklist;
        private Label _lblProfileHint;
        private Label _lblPaySnapshotGross;
        private Label _lblPaySnapshotNet;
        private Panel _contentHost;
        private Panel _dashboardSurface;
        private Panel _workspaceSurface;
        private Label _lblDashboardReadyCount;
        private Label _lblDashboardNeedsActionCount;
        private Label _lblDashboardCheckedInCount;
        private Label _lblDashboardExpiringCount;
        private Label _lblDashboardCoverageHeadline;
        private Label _lblDashboardCoverageText;
        private Label _lblDashboardCoverageRate;
        private Label _lblDashboardCoverageAssignedMeta;
        private Label _lblDashboardCoverageUnassignedMeta;
        private Label _lblDashboardCoverageTopSiteMeta;
        private Label _lblDashboardAttentionHeadline;
        private Label _lblDashboardAttentionText;
        private Label _lblDashboardCurrentSelection;
        private FlowLayoutPanel _dashboardCoverageList;
        private FlowLayoutPanel _dashboardAttentionList;
        private DataGridView _gridDashboardRoster;
        private TextBox _txtDashboardSearch;
        private ComboBox _cmbDashboardPageSize;

        private PictureBox _picPhoto;
        private TextBox _txtCode;
        private TextBox _txtName;
        private TextBox _txtDesignation;
        private TextBox _txtDepartment;
        private ComboBox _cmbSite;
        private TextBox _txtPhone;
        private TextBox _txtWhatsapp;
        private ComboBox _cmbBloodGroup;
        private TextBox _txtAadhaar;
        private TextBox _txtPan;
        private TextBox _txtEmergencyName;
        private TextBox _txtEmergencyPhone;
        private DateTimePicker _dtpJoining;
        private DateTimePicker _dtpProbationEnd;
        private DateTimePicker _dtpConfirmation;
        private DateTimePicker _dtpLastWorkingDay;
        private ComboBox _cmbEmployeeStatus;
        private CheckBox _chkIsRehire;

        private Label _lblJobsTotal;
        private Label _lblJobsCompleted;
        private Label _lblAverageClosure;
        private DataGridView _gridJobs;

        private DateTimePicker _dtpAttendanceMonth;
        private Label _lblPresentDays;
        private Label _lblAbsentDays;
        private Label _lblLateDays;
        private Label _lblLeaveDays;
        private DataGridView _gridAttendance;

        private Label _lblSkillAlert;
        private Button _btnAddSkill;
        private DataGridView _gridSkills;

        private Button _btnUploadDocument;
        private DataGridView _gridDocuments;

        private TextBox _txtBasicSalary;
        private TextBox _txtHra;
        private TextBox _txtAllowances;
        private TextBox _txtPfDeduction;
        private TextBox _txtEsicDeduction;
        private DateTimePicker _dtpSalaryEffectiveFrom;
        private Label _lblGrossSalary;
        private Label _lblNetSalary;
        private DataGridView _gridAdvances;
        private Button _btnGenerateSalarySlip;

        public EmployeeForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            _employeeSearchDebounceTimer.Interval = 280;
            _employeeSearchDebounceTimer.Tick += EmployeeSearchDebounceTimer_Tick;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPermissions();
            EnableDeferredLoad(() => FormLoadSafe(), ex =>
            {
                AppLogger.LogError("EmployeeForm.Load", ex);
                SetStatus("Employee load failed: " + ex.Message, Red);
            });
        }

        private void FormLoadSafe()
        {
            try
            {
                QueueInitialLoad();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.FormLoadSafe", ex);
                SetStatus("Employee form failed to load: " + ex.Message, Red);
            }
        }

        private void QueueInitialLoad()
        {
            var timer = new Timer { Interval = 1500 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!IsDisposed && Visible)
                    BeginInitialLoad();
            };
            timer.Start();
        }

        /// <summary>Starts the first employee page load asynchronously so navigation stays responsive.</summary>
        private async void BeginInitialLoad()
        {
            if (_initialLoadInProgress)
                return;

            _initialLoadInProgress = true;
            try
            {
                SetStatus("Loading employee module...", TextSecondary);
                EmployeeInitialPayload payload = await Task.Run(() => LoadInitialPayload());
                BindInitialPayload(payload);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.InitialLoad", ex);
                SetStatus("Employee load failed: " + ex.Message, Red);
            }
            finally
            {
                _initialLoadInProgress = false;
            }
        }

        /// <summary>Loads initial employee data without touching UI controls.</summary>
        private EmployeeInitialPayload LoadInitialPayload()
        {
            var payload = new EmployeeInitialPayload();
            TimeSpan ttl = TimeSpan.FromMinutes(2);
            try { payload.ExpiringSkills = AppDataCache.GetOrCreate("employees:expiring-skills:30", ttl, () => _employeeService.GetExpiringSkills(30) ?? new List<EmployeeSkillDto>()).ToList(); }
            catch (Exception ex) { AppLogger.LogError("EmployeeForm.InitialLoad.ExpiringSkills", ex); }

            try { payload.Stats = AppDataCache.GetOrCreate("employees:dashboard-stats", ttl, () => _employeeService.GetDashboardStats() ?? new EmployeeDashboardStats()); }
            catch (Exception ex) { AppLogger.LogError("EmployeeForm.InitialLoad.Stats", ex); }

            try { payload.AttendanceReconciliationBanner = new AttendanceService().GetSourceReconciliationBanner(DateTime.Today.Month, DateTime.Today.Year); }
            catch (Exception ex) { AppLogger.LogError("EmployeeForm.InitialLoad.AttendanceReconciliation", ex); }

            payload.EmployeeTable = LoadEmployeeTable(string.Empty, "All", "All");
            payload.CheckedInTodayEmployeeIds = LoadCheckedInEmployeesTodaySet();
            try
            {
                payload.SiteNames = AppDataCache.GetOrCreate("sites:all", ttl, () => _siteService.GetAll() ?? new List<ClientSite>())
                    .Select(SiteService.GetDisplayName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
            }
            catch (Exception ex) { AppLogger.LogError("EmployeeForm.InitialLoad.Sites", ex); }

            return payload;
        }

        /// <summary>Binds the initial employee payload after the async load completes.</summary>
        private void BindInitialPayload(EmployeeInitialPayload payload)
        {
            payload = payload ?? new EmployeeInitialPayload();
            _pendingRestoreEmployeeId = _currentEmployee == null ? 0 : _currentEmployee.EmployeeID;
            _pendingRestoreTabIndex = _tabs == null ? 0 : _tabs.SelectedIndex;
            _currentEmployee = null;
            _currentSalaryProfile = new EmployeeSalaryProfileDto { EffectiveFrom = DateTime.Today };
            _currentPhoto = null;
            ClearCurrentEmployeeView();

            _expiringSkills = payload.ExpiringSkills ?? new List<EmployeeSkillDto>();
            _checkedInTodayEmployeeIds = payload.CheckedInTodayEmployeeIds ?? new HashSet<int>();
            _lblTotalEmployees.Text = payload.Stats.TotalEmployees.ToString();
            _lblActiveToday.Text = payload.Stats.ActiveToday.ToString();
            _lblOnDuty.Text = payload.Stats.OnDuty.ToString();
            _lblOnLeave.Text = payload.Stats.OnLeave.ToString();
            UpdateExpiringBanner();
            BindEmployeeTable(payload.EmployeeTable, string.Empty, "All", "All");
            UpdateEmployeeDashboard(payload.Stats, payload.EmployeeTable);

            _suppressEmployeeFilterEvents = true;
            try
            {
                PopulateLeftFilters();
                PopulateSiteOptions(payload.SiteNames);
            }
            finally
            {
                _suppressEmployeeFilterEvents = false;
            }

            if (_gridEmployees.Rows.Count > 0)
            {
                if (_pendingRestoreEmployeeId > 0)
                    SelectEmployeeRow(_pendingRestoreEmployeeId);

                if (_gridEmployees.CurrentRow == null)
                    _gridEmployees.Rows[0].Selected = true;
            }

            if (_tabs != null)
                _tabs.SelectedIndex = Math.Max(0, Math.Min(_pendingRestoreTabIndex, _tabs.TabPages.Count - 1));

            if (!string.IsNullOrWhiteSpace(payload.AttendanceReconciliationBanner))
                SetStatus(payload.AttendanceReconciliationBanner, Amber);
            else
                SetStatus("Employee module loaded.", TextSecondary);
        }

        private void BuildLayout()
        {
            Controls.Clear();

            _btnNew = MakeButton("New Employee", Teal, Color.White, 142);
            _btnSave = MakeButton("Save", Blue, Color.White, 96);
            _btnDelete = MakeButton("Delete", Color.White, Red, 76);
            _btnExport = MakeButton("Export", Color.White, TextPrimary, 112);
            _btnImport = MakeButton("Import", Color.White, TextPrimary, 112);
            _btnTemplate = MakeButton("Template", Color.White, TextPrimary, 86);
            Button btnForms = MakeButton("Forms", Color.White, Blue, 76);
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            _btnWhatsapp = MakeButton("WhatsApp", Color.White, Blue, 92);
            Button btnFilters = MakeButton("Filters", Color.White, TextPrimary, 112);
            ModernIconSystem.AddButtonIcon(btnFilters, ModernIconKind.Filter);
            ModernIconSystem.AddButtonIcon(_btnImport, ModernIconKind.Import);
            ModernIconSystem.AddButtonIcon(_btnExport, ModernIconKind.Export);
            ModernIconSystem.AddButtonIcon(_btnNew, ModernIconKind.User);
            ModernIconSystem.AddButtonIcon(_btnSave, ModernIconKind.Save);
            _txtDashboardSearch = new TextBox
            {
                Width = 300,
                Height = 30,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                ForeColor = TextPrimary,
                Tag = "CUSTOM_INPUT_SHELL FIXED_WIDTH"
            };
            _txtDashboardSearch.TextChanged += (s, e) => RefreshDashboardRosterGrid();
            Panel searchShell = new Panel
            {
                Width = 340,
                Height = 34,
                BackColor = Color.White,
                Margin = Padding.Empty,
                Tag = "CUSTOM_INPUT_SHELL FIXED_WIDTH"
            };
            Label searchIcon = ModernIconSystem.Icon(ModernIconKind.Search, 14, TextSecondary);
            searchIcon.Location = new Point(8, 6);
            searchIcon.Size = new Size(20, 20);
            _txtDashboardSearch.Location = new Point(32, 3);
            _txtDashboardSearch.BorderStyle = BorderStyle.None;
            searchShell.Controls.Add(_txtDashboardSearch);
            searchShell.Controls.Add(searchIcon);
            btnFilters.Click += (s, e) => ShowEmployeeWorkspace();

            Control[] headerButtons = { btnFilters, _btnImport, _btnExport, _btnNew, _btnSave };
            foreach (Control action in headerButtons)
            {
                Button button = action as Button;
                if (button == null)
                    continue;
                button.AutoSize = false;
                button.Height = 34;
                button.Width = Math.Max(button.Width, GetEmployeeHeaderButtonMinWidth(button));
                button.MinimumSize = new Size(button.Width, button.Height);
                button.Margin = Padding.Empty;
                button.Tag = ((button.Tag == null ? string.Empty : button.Tag + " ") + "FIXED_WIDTH").Trim();
                ApplyEmployeeHeaderButtonSpacing(button);
            }
            SharedPageHeaderModel headerModel = SharedPageHeader.CreateWorkspaceEditor(
                "EmployeePageHeader",
                "Employees",
                "Manage your organization's employees, roles and work assignments.",
                new List<Control>(headerButtons),
                null,
                "HR workspace ready.",
                TextSecondary);
            headerModel.CompactBreakpoint = 1420;
            headerModel.CompactHeight = 132;
            SharedPageHeaderResult headerResult = SharedPageHeader.Build(headerModel);
            Panel header = headerResult.Header;
            header.Tag = ((header.Tag == null ? string.Empty : header.Tag + " ") + "custom-header-actions").Trim();
            _lblStatus = headerResult.StatusLabel;

            _lnkExpiringBanner = new LinkLabel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = AmberLight,
                LinkColor = Color.FromArgb(99, 56, 6),
                ActiveLinkColor = Color.FromArgb(99, 56, 6),
                VisitedLinkColor = Color.FromArgb(99, 56, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Visible = false
            };
            _lnkExpiringBanner.LinkClicked += (s, e) => ShowExpiringSkillsReview();

            Panel kpiStrip = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = PageBg, Padding = new Padding(PageEdgeGap, SectionGap, PageEdgeGap, 0) };
            TableLayoutPanel kpiTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++)
                kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _lblTotalEmployees = AddDashboardHeroKpiCard(kpiTable, 0, "Total Employees", "Overall roster size", Color.FromArgb(45, 59, 77), Color.White, Color.FromArgb(125, 229, 190));
            _lblActiveToday = AddDashboardHeroKpiCard(kpiTable, 1, "Active Today", "Profiles currently active", Color.FromArgb(18, 127, 122), Color.White, Color.FromArgb(140, 244, 221));
            _lblOnDuty = AddDashboardHeroKpiCard(kpiTable, 2, "On Duty", "People available now", Color.FromArgb(230, 178, 86), Color.White, Color.FromArgb(255, 233, 182));
            _lblOnLeave = AddDashboardHeroKpiCard(kpiTable, 3, "On Leave", "Approved leave load", Color.FromArgb(195, 93, 92), Color.White, Color.FromArgb(249, 204, 203));
            kpiStrip.Controls.Add(kpiTable);

            _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            _dashboardSurface = BuildDashboardSurface();
            _workspaceSurface = BuildWorkspaceSurface();
            _contentHost.Controls.Add(_workspaceSurface);
            _contentHost.Controls.Add(_dashboardSurface);

            Controls.Add(_contentHost);
            Controls.Add(_lnkExpiringBanner);
            Controls.Add(header);

            _btnNew.Click += (s, e) => NewEmployee();
            _btnSave.Click += async (s, e) => await SaveCurrentTabAsync();
            _btnDelete.Click += (s, e) => DeleteCurrentEmployee();
            _btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Employees, FindForm());
            _btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Employees, FindForm());
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Employees", "Employees", null, "technician attendance leave request skill certification customer sign-off service report workforce");
            _btnExport.Click += (s, e) => ExportEmployees();
            _btnWhatsapp.Click += (s, e) => OpenWhatsapp();

            ShowEmployeeDashboard();
        }

        private Panel BuildDashboardSurface()
        {
            return BuildLightweightEmployeeDashboardSurface();
        }

        private Panel BuildLightweightEmployeeDashboardSurface()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(PageEdgeGap, SectionGap, PageEdgeGap, PageEdgeGap) };

            Panel summaryStrip = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.White };
            DS.Rounded(summaryStrip, DS.RadiusMd);
            TableLayoutPanel summaryTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, BackColor = Color.White };
            for (int i = 0; i < 6; i++)
                summaryTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6667F));
            _lblTotalEmployees = AddLightweightEmployeeMetric(summaryTable, 0, "Total Employees", ModernIconKind.User, DS.Primary600, DS.Primary50);
            _lblActiveToday = AddLightweightEmployeeMetric(summaryTable, 1, "Active", ModernIconKind.Status, Teal, TealLight);
            _lblOnDuty = AddLightweightEmployeeMetric(summaryTable, 2, "On Duty", ModernIconKind.Job, Amber, AmberLight);
            _lblOnLeave = AddLightweightEmployeeMetric(summaryTable, 3, "On Leave", ModernIconKind.Calendar, Red, Color.FromArgb(254, 242, 242));
            _lblDashboardCheckedInCount = AddLightweightEmployeeMetric(summaryTable, 4, "Checked In", ModernIconKind.Technician, Blue, DS.Primary50);
            _lblDashboardExpiringCount = AddLightweightEmployeeMetric(summaryTable, 5, "Expiring Skills", ModernIconKind.Security, Color.FromArgb(124, 58, 237), Color.FromArgb(245, 243, 255));
            summaryStrip.Controls.Add(summaryTable);

            Panel tableShell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };
            DS.Rounded(tableShell, DS.RadiusMd);

            _gridDashboardRoster = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", DataPropertyName = "RowNumber", Width = 48 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", DataPropertyName = "Name", Width = 230 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", DataPropertyName = "Designation", Width = 150 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Client / Site", DataPropertyName = "ClientSite", Width = 190 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "PresenceState", Width = 120 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Readiness", DataPropertyName = "ReadinessState", Width = 140 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mobile", DataPropertyName = "Mobile", Width = 150 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Email", DataPropertyName = "Email", Width = 220 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Actions", DataPropertyName = "Actions", Width = 110 });
            StyleLightweightRosterGrid(_gridDashboardRoster);
            _gridDashboardRoster.SelectionChanged += (s, e) => UpdateDashboardCurrentSelection();
            _gridDashboardRoster.DoubleClick += (s, e) => OpenSelectedDashboardEmployee();
            _gridDashboardRoster.CellFormatting += GridDashboardRoster_CellFormatting;

            Panel pager = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Color.White, Padding = new Padding(12, 6, 12, 6) };
            _lblDashboardCurrentSelection = new Label
            {
                Text = "Showing employees",
                Location = new Point(12, 11),
                Size = new Size(420, 20),
                Font = new Font("Segoe UI", 8.8F),
                ForeColor = TextSecondary
            };
            _cmbDashboardPageSize = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                Width = 110,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _cmbDashboardPageSize.Items.AddRange(new object[] { "15 / page", "25 / page", "50 / page", "All rows" });
            _cmbDashboardPageSize.SelectedIndex = 0;
            _cmbDashboardPageSize.SelectedIndexChanged += (s, e) => RefreshDashboardRosterGrid();
            pager.Resize += (s, e) => _cmbDashboardPageSize.Left = Math.Max(12, pager.ClientSize.Width - _cmbDashboardPageSize.Width - 12);
            pager.Controls.Add(_lblDashboardCurrentSelection);
            pager.Controls.Add(_cmbDashboardPageSize);

            tableShell.Controls.Add(_gridDashboardRoster);
            tableShell.Controls.Add(pager);
            host.Controls.Add(tableShell);
            host.Controls.Add(summaryStrip);
            return host;
        }

        private Label AddLightweightEmployeeMetric(TableLayoutPanel table, int column, string title, ModernIconKind iconKind, Color iconColor, Color iconBackColor)
        {
            Panel cell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 12, 12, 8), Margin = Padding.Empty };
            Label icon = ModernIconSystem.Badge(iconKind, 34, iconBackColor, iconColor, 17);
            icon.Location = new Point(16, 20);
            Label titleLabel = new Label
            {
                Text = title,
                Location = new Point(58, 14),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            };
            Label valueLabel = new Label
            {
                Text = "0",
                Location = new Point(58, 34),
                Size = new Size(150, 30),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Panel separator = new Panel { Dock = DockStyle.Right, Width = column == 5 ? 0 : 1, BackColor = Border };
            cell.Controls.Add(valueLabel);
            cell.Controls.Add(titleLabel);
            cell.Controls.Add(icon);
            cell.Controls.Add(separator);
            table.Controls.Add(cell, column, 0);
            return valueLabel;
        }

        private void StyleLightweightRosterGrid(DataGridView grid)
        {
            GridTheme.Apply(grid);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 42;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.SelectionBackColor = DS.Primary50;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextSecondary;
        }

        private void RefreshDashboardRosterGrid()
        {
            UpdateEmployeeDashboard(null, _currentEmployeeTable);
        }

        private static string BuildEmployeeEmail(EmployeeSummaryDto employee)
        {
            string code = employee == null ? string.Empty : (employee.EmployeeCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                string name = employee == null ? string.Empty : (employee.Name ?? string.Empty).Trim().ToLowerInvariant();
                code = new string(name.Where(char.IsLetterOrDigit).ToArray());
            }
            return string.IsNullOrWhiteSpace(code) ? "-" : code.ToLowerInvariant() + "@servoerp.com";
        }

        private Panel BuildLegacyDashboardSurface()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(PageEdgeGap, SectionGap, PageEdgeGap, PageEdgeGap), AutoScroll = true };
            Label heroTitle;
            Label heroDetail;
            FlowLayoutPanel heroChips;
            Panel hero = WorkforceModuleVisuals.CreateHeroCard(
                "WORKFORCE COMMAND CENTER",
                "Start in the dashboard, spot readiness risks early, then open a single employee workspace only when action is required.",
                "This first layer is the management view for Madhusuman Enterprises: deployment coverage, follow-up pressure, and roster launch readiness in one place.",
                Blue,
                Enumerable.Empty<Control>(),
                out heroTitle,
                out heroDetail,
                out heroChips);
            hero.Dock = DockStyle.Top;
            hero.Height = 152;
            heroChips.Controls.Add(WorkforceModuleVisuals.CreateChip("Dashboard-first workflow", Color.FromArgb(239, 246, 255), DS.Primary700));
            heroChips.Controls.Add(WorkforceModuleVisuals.CreateChip("Live readiness pulse", Color.FromArgb(232, 245, 233), DS.Green600));
            heroChips.Controls.Add(WorkforceModuleVisuals.CreateChip("Second-layer employee workspace", Color.FromArgb(255, 247, 237), DS.Amber600));

            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Panel readinessCard = MakeCard("Workforce readiness pulse");
            readinessCard.Dock = DockStyle.Fill;
            readinessCard.Height = 168;
            Panel readinessBody = GetCardBody(readinessCard);
            TableLayoutPanel readinessGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 86,
                ColumnCount = 4,
                Padding = new Padding(0, 4, 0, 0)
            };
            for (int i = 0; i < 4; i++)
                readinessGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _lblDashboardReadyCount = AddDashboardRibbonCard(readinessGrid, 0, "Ready", Teal, ModernIconKind.Checklist);
            _lblDashboardNeedsActionCount = AddDashboardRibbonCard(readinessGrid, 1, "Needs Action", Red, ModernIconKind.Alert);
            _lblDashboardCheckedInCount = AddDashboardRibbonCard(readinessGrid, 2, "Checked In", Blue, ModernIconKind.Status);
            _lblDashboardExpiringCount = AddDashboardRibbonCard(readinessGrid, 3, "Expiring Skills", Amber, ModernIconKind.Calendar);
            readinessBody.Controls.Add(readinessGrid);
            Label readinessCaption = new Label
            {
                Text = "Use this row as the management pulse: overall readiness, live presence, and expiring compliance records before drilling into a single employee.",
                Location = new Point(12, 98),
                Size = new Size(980, 40),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            readinessBody.Controls.Add(readinessCaption);
            readinessBody.Resize += (s, e) => readinessCaption.Width = Math.Max(420, readinessBody.ClientSize.Width - 24);

            Panel coverageCard = MakeCard("Deployment coverage");
            coverageCard.Dock = DockStyle.Fill;
            coverageCard.Height = 236;
            Panel coverageBody = GetCardBody(coverageCard);
            coverageBody.Padding = new Padding(12, 10, 12, 12);
            _lblDashboardCoverageHeadline = new Label
            {
                Text = "Coverage summary appears after the employee list loads.",
                Location = new Point(12, 12),
                Size = new Size(500, 24),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            _lblDashboardCoverageText = new Label
            {
                Text = "Visible site load and uncovered roster will appear here.",
                Location = new Point(12, 38),
                Size = new Size(500, 18),
                Font = new Font("Segoe UI", 8.9F),
                ForeColor = TextSecondary
            };
            Panel coverageSummary = CreateDeploymentCoverageSummaryPanel();

            Panel deploymentVisual = CreateDeploymentCoveragePanel();
            coverageBody.Controls.Add(_lblDashboardCoverageHeadline);
            coverageBody.Controls.Add(_lblDashboardCoverageText);
            coverageBody.Controls.Add(coverageSummary);
            coverageBody.Controls.Add(deploymentVisual);
            coverageBody.Resize += (s, e) =>
            {
                int visualWidth = Math.Min(360, Math.Max(280, coverageBody.ClientSize.Width / 2 - 4));
                int textWidth = Math.Max(260, coverageBody.ClientSize.Width - visualWidth - 36);
                _lblDashboardCoverageHeadline.Width = textWidth;
                _lblDashboardCoverageText.Width = textWidth;
                coverageSummary.Width = Math.Max(236, Math.Min(286, textWidth));
                deploymentVisual.SetBounds(coverageBody.ClientSize.Width - visualWidth - 12, 34, visualWidth, 176);
            };

            Panel attentionCard = MakeCard("Immediate follow-up");
            attentionCard.Dock = DockStyle.Fill;
            attentionCard.Height = 228;
            Panel attentionBody = GetCardBody(attentionCard);
            attentionBody.Padding = new Padding(12, 10, 12, 12);
            _lblDashboardAttentionHeadline = new Label
            {
                Text = "Follow-up queue appears after the employee list loads.",
                Location = new Point(12, 12),
                Size = new Size(460, 24),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            _lblDashboardAttentionText = new Label
            {
                Text = "Employee profile, KYC, emergency, and compliance issues will surface here.",
                Location = new Point(12, 40),
                Size = new Size(460, 18),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            _dashboardAttentionList = new FlowLayoutPanel
            {
                Location = new Point(12, 64),
                Size = new Size(470, 128),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            attentionBody.Controls.Add(_lblDashboardAttentionHeadline);
            attentionBody.Controls.Add(_lblDashboardAttentionText);
            attentionBody.Controls.Add(_dashboardAttentionList);
            attentionBody.Resize += (s, e) =>
            {
                int innerWidth = Math.Max(320, attentionBody.ClientSize.Width - 24);
                _lblDashboardAttentionHeadline.Width = innerWidth;
                _lblDashboardAttentionText.Width = innerWidth;
                _dashboardAttentionList.Width = innerWidth;
            };

            Panel rosterCard = MakeCard("Roster launchpad");
            rosterCard.Dock = DockStyle.Fill;
            rosterCard.Height = 470;
            Panel rosterBody = GetCardBody(rosterCard);
            Panel rosterTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.White
            };
            Label rosterIntro = new Label
            {
                Text = "Review the people most likely to need action or immediate access, then jump into the detailed employee workspace from the dashboard.",
                Location = new Point(12, 8),
                Size = new Size(900, 32),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            _lblDashboardCurrentSelection = new Label
            {
                Text = "Selected workspace row: choose a person from the roster.",
                Location = new Point(12, 34),
                Size = new Size(820, 18),
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
                ForeColor = Blue
            };
            Button btnOpenSelectedWorkspace = MakeButton("View Employee Workspace", Color.White, TextPrimary, 200);
            btnOpenSelectedWorkspace.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenSelectedWorkspace.Location = new Point(778, 12);
            btnOpenSelectedWorkspace.FlatAppearance.BorderColor = Border;
            btnOpenSelectedWorkspace.FlatAppearance.BorderSize = 1;
            ModernIconSystem.AddButtonIcon(btnOpenSelectedWorkspace, ModernIconKind.User);
            btnOpenSelectedWorkspace.Click += (s, e) => OpenSelectedDashboardEmployee();
            Button btnOpenPayrollFromDashboard = MakeButton("View Payroll Workspace", Color.White, TextPrimary, 186);
            btnOpenPayrollFromDashboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenPayrollFromDashboard.Location = new Point(986, 12);
            btnOpenPayrollFromDashboard.FlatAppearance.BorderColor = Border;
            btnOpenPayrollFromDashboard.FlatAppearance.BorderSize = 1;
            ModernIconSystem.AddButtonIcon(btnOpenPayrollFromDashboard, ModernIconKind.Payment);
            btnOpenPayrollFromDashboard.Click += (s, e) => (FindForm() as MainForm)?.NavigateTo("Payroll");
            rosterTop.Resize += (s, e) =>
            {
                btnOpenPayrollFromDashboard.Left = Math.Max(760, rosterTop.ClientSize.Width - btnOpenPayrollFromDashboard.Width - 12);
                btnOpenSelectedWorkspace.Left = btnOpenPayrollFromDashboard.Left - btnOpenSelectedWorkspace.Width - 10;
                _lblDashboardCurrentSelection.Width = Math.Max(320, btnOpenSelectedWorkspace.Left - 22);
                rosterIntro.Width = Math.Max(420, btnOpenSelectedWorkspace.Left - 22);
            };
            rosterTop.Controls.Add(rosterIntro);
            rosterTop.Controls.Add(_lblDashboardCurrentSelection);
            rosterTop.Controls.Add(btnOpenSelectedWorkspace);
            rosterTop.Controls.Add(btnOpenPayrollFromDashboard);

            _gridDashboardRoster = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                ReadOnly = true
            };
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", DataPropertyName = "Name", Width = 200 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", DataPropertyName = "Designation", Width = 150 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Client / Site", DataPropertyName = "ClientSite", Width = 170 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Today", DataPropertyName = "PresenceState", Width = 110 });
            _gridDashboardRoster.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Readiness", DataPropertyName = "ReadinessState", Width = 170, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            StyleDataGrid(_gridDashboardRoster);
            _gridDashboardRoster.SelectionChanged += (s, e) => UpdateDashboardCurrentSelection();
            _gridDashboardRoster.DoubleClick += (s, e) => OpenSelectedDashboardEmployee();
            _gridDashboardRoster.CellFormatting += GridDashboardRoster_CellFormatting;
            rosterBody.Controls.Add(_gridDashboardRoster);
            rosterBody.Controls.Add(rosterTop);

            grid.Controls.Add(readinessCard, 0, 0);
            grid.SetColumnSpan(readinessCard, 2);
            grid.Controls.Add(coverageCard, 0, 1);
            grid.Controls.Add(attentionCard, 1, 1);
            grid.Controls.Add(rosterCard, 0, 2);
            grid.SetColumnSpan(rosterCard, 2);

            host.Controls.Add(grid);
            host.Controls.Add(hero);
            return host;
        }

        private Panel BuildWorkspaceSurface()
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Visible = false };
            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = PageBg, Padding = new Padding(PageEdgeGap, SectionGap, PageEdgeGap, 6) };
            Button backButton = MakeButton("Back to Dashboard", Color.White, Blue, 156);
            backButton.Location = new Point(0, 12);
            backButton.Click += (s, e) => ShowEmployeeDashboard();
            Button attendanceButton = MakeButton("Attendance Workspace", Color.White, Blue, 164);
            attendanceButton.Click += (s, e) => (FindForm() as MainForm)?.NavigateTo("Attendance");
            Button payrollButton = MakeButton("Payroll Workspace", Teal, Color.White, 154);
            payrollButton.Click += (s, e) => (FindForm() as MainForm)?.NavigateTo("Payroll");
            Label helper = new Label
            {
                Text = "Employee workspace: search, review, and edit one person across profile, work, compliance, and pay from a single second-layer view.",
                Location = new Point(166, 4),
                Size = new Size(760, 40),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            topBar.Resize += (s, e) =>
            {
                payrollButton.Location = new Point(Math.Max(540, topBar.ClientSize.Width - payrollButton.Width), 12);
                attendanceButton.Location = new Point(Math.Max(360, payrollButton.Left - attendanceButton.Width - 8), 12);
                helper.Width = Math.Max(260, attendanceButton.Left - helper.Left - 16);
            };
            topBar.Controls.Add(backButton);
            topBar.Controls.Add(helper);
            topBar.Controls.Add(attendanceButton);
            topBar.Controls.Add(payrollButton);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                FixedPanel = FixedPanel.Panel1,
                Panel1MinSize = 300
            };
            split.HandleCreated += (s, e) => ApplyEmployeeSplitDistance(split);
            split.Resize += (s, e) => ApplyEmployeeSplitDistance(split);
            split.Panel1.BackColor = Color.White;
            split.Panel2.BackColor = PageBg;
            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            host.Controls.Add(split);
            host.Controls.Add(topBar);
            return host;
        }

        private void BuildLeftPanel(Control parent)
        {
            parent.Controls.Clear();

            Panel card = MakeCard("Roster navigator");
            card.Dock = DockStyle.Fill;
            Panel cardBody = GetCardBody(card);
            cardBody.Padding = new Padding(0);

            Panel top = new Panel { Dock = DockStyle.Top, Height = 156, BackColor = Color.White, Padding = new Padding(12, 12, 12, 8) };
            _txtSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F), Width = 228 };
            Button clearFilters = MakeButton("Clear", Color.White, Blue, 80);
            clearFilters.FlatAppearance.BorderColor = Border;
            clearFilters.FlatAppearance.BorderSize = 1;
            _cmbClientFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Font = new Font("Segoe UI", 9F) };
            _cmbStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Font = new Font("Segoe UI", 9F) };

            Label lblSearch = new Label { Text = "Find people", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(14, 6) };
            Label lblSubtext = new Label { Text = "Scan readiness, use filters, then open one employee workspace.", AutoSize = true, ForeColor = TextHint, Font = new Font("Segoe UI", 8.5F), Location = new Point(14, 24) };
            _txtSearch.Location = new Point(14, 46);
            clearFilters.Location = new Point(250, 45);
            Label lblClient = new Label { Text = "Client / Site", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(14, 80) };
            _cmbClientFilter.Location = new Point(14, 98);
            Label lblStatusFilter = new Label { Text = "Status", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(178, 80) };
            _cmbStatusFilter.Location = new Point(178, 98);
            _lblVisibleEmployees = CreateLeftMetric(top, 14, 130, "Visible");
            _lblNeedsFollowUp = CreateLeftMetric(top, 132, 130, "Needs Action");
            _lblCheckedInNow = CreateLeftMetric(top, 250, 130, "Checked In");
            top.Controls.AddRange(new Control[] { lblSearch, lblSubtext, _txtSearch, clearFilters, lblClient, _cmbClientFilter, lblStatusFilter, _cmbStatusFilter });

            _gridEmployees = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ReadOnly = true,
                MinimumSize = new Size(320, 420)
            };
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "", Width = 34, Name = "StatusDot" });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", DataPropertyName = "EmployeeName", Width = 180 });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", DataPropertyName = "Designation", Width = 120, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Client / Site", DataPropertyName = "ClientSite", Width = 150 });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Readiness", DataPropertyName = "NeedsAction", Width = 150 });
            StyleDataGrid(_gridEmployees);

            parent.Controls.Add(_gridEmployees);
            parent.Controls.Add(top);

            _txtSearch.TextChanged += (s, e) => QueueEmployeeSearch();
            _cmbClientFilter.SelectedIndexChanged += (s, e) => { if (!_suppressEmployeeFilterEvents) LoadEmployees(); };
            _cmbStatusFilter.SelectedIndexChanged += (s, e) => { if (!_suppressEmployeeFilterEvents) LoadEmployees(); };
            clearFilters.Click += (s, e) => ClearEmployeeFilters();
            _gridEmployees.SelectionChanged += (s, e) => LoadSelectedEmployeeSafe();
            _gridEmployees.CellFormatting += GridEmployees_CellFormattingSafe;

            cardBody.Controls.Add(_gridEmployees);
            cardBody.Controls.Add(top);
            parent.Controls.Add(card);
        }

        private void ApplyEmployeeSplitDistance(SplitContainer split)
        {
            if (split == null || split.IsDisposed || split.Width <= 0)
                return;

            const int desiredRight = 620;
            int maxLeft = Math.Max(split.Panel1MinSize, split.Width - desiredRight - split.SplitterWidth);
            int target = Math.Min(460, maxLeft);
            target = Math.Max(split.Panel1MinSize, target);
            if (target > 0 && target != split.SplitterDistance)
            {
                try { split.SplitterDistance = target; }
                catch (Exception ex) { AppLogger.LogError("EmployeeForm.ApplyEmployeeSplitDistance", ex); }
            }
        }

        private void BuildRightPanel(Control parent)
        {
            parent.Controls.Clear();
            Panel wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(PageEdgeGap, SectionGap, PageEdgeGap, PageEdgeGap), AutoScroll = false, BackColor = PageBg };
            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };
            _tabs.TabPages.Add(BuildOverviewTab());
            _tabs.TabPages.Add(BuildJobsTab());
            _tabs.TabPages.Add(BuildSkillsTab());
            _tabs.TabPages.Add(BuildPayrollTab());
            _tabs.SelectedIndexChanged += (s, e) => LoadCurrentEmployeeTabData();
            wrap.Controls.Add(_tabs);
            parent.Controls.Add(wrap);
        }

        private void ShowEmployeeDashboard()
        {
            if (_dashboardSurface != null)
                _dashboardSurface.Visible = true;
            if (_workspaceSurface != null)
                _workspaceSurface.Visible = false;
            if (_btnSave != null) _btnSave.Enabled = false;
            if (_btnDelete != null) _btnDelete.Enabled = false;
            if (_btnWhatsapp != null) _btnWhatsapp.Enabled = false;
            SetStatus("Employee dashboard ready.", TextSecondary);
        }

        private void ShowEmployeeWorkspace()
        {
            if (_dashboardSurface != null)
                _dashboardSurface.Visible = false;
            if (_workspaceSurface != null)
                _workspaceSurface.Visible = true;
            if (_btnSave != null) _btnSave.Enabled = true;
            if (_btnDelete != null) _btnDelete.Enabled = true;
            if (_btnWhatsapp != null) _btnWhatsapp.Enabled = _currentEmployee != null && !string.IsNullOrWhiteSpace(_currentEmployee.WhatsAppNumber);

            if (_gridEmployees != null && _gridEmployees.Rows.Count > 0)
            {
                if (_gridEmployees.CurrentRow == null)
                    _gridEmployees.Rows[0].Selected = true;
                LoadSelectedEmployeeSafe();
            }

            SetStatus("Employee workspace opened.", TextSecondary);
        }

        private void OpenSelectedDashboardEmployee()
        {
            if (_gridDashboardRoster == null || _gridDashboardRoster.CurrentRow == null)
            {
                ShowEmployeeWorkspace();
                return;
            }

            DashboardRosterRow summary = _gridDashboardRoster.CurrentRow.DataBoundItem as DashboardRosterRow;
            ShowEmployeeWorkspace();
            if (summary != null && summary.EmployeeID > 0)
                SelectEmployeeRow(summary.EmployeeID);
        }

        private TabPage BuildOverviewTab()
        {
            TabPage page = new TabPage("Profile") { BackColor = PageBg };
            Panel content = MakeTabScrollHost();
            FlowLayoutPanel flow = MakeVerticalFlow();

            Panel heroCard = MakeCard("Employee workspace");
            heroCard.Height = 332;
            Panel heroBody = GetCardBody(heroCard);
            heroBody.Padding = new Padding(InnerPadding);

            _picPhoto = new PictureBox
            {
                Width = 116,
                Height = 116,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.None,
                Cursor = Cursors.Hand,
                BackColor = Surface,
                Location = new Point(18, 18)
            };
            _picPhoto.Click += (s, e) => UploadPhoto();

            _lblHeroEmployeeName = new Label
            {
                Text = "Select an employee",
                Location = new Point(154, 18),
                Size = new Size(420, 30),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoEllipsis = true
            };
            _lblHeroEmployeeMeta = new Label
            {
                Text = "Choose a person from the left to review profile, work, compliance, and pay.",
                Location = new Point(154, 52),
                Size = new Size(520, 20),
                Font = new Font("Segoe UI", 9F),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            };
            _lblHeroStatusChip = BuildSummaryChip("Status", TextSecondary, DS.Slate100);
            _lblHeroSiteChip = BuildSummaryChip("Site pending", DS.Primary700, DS.Primary50);
            _lblHeroReadinessChip = BuildSummaryChip("Readiness unknown", DS.Amber600, DS.Amber50);
            _lblHeroPayrollChip = BuildSummaryChip("Payroll pending", DS.Red600, Color.FromArgb(254, 242, 242));
            _lblHeroContactChip = BuildSummaryChip("Contact not verified", DS.Slate600, DS.Slate100);

            _lblReadinessHeadline = new Label
            {
                Text = "Profile readiness",
                Location = new Point(154, 118),
                Size = new Size(240, 22),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextPrimary,
                AutoEllipsis = true
            };
            _lblReadinessDetail = new Label
            {
                Text = "KYC, emergency contact, site assignment, and salary setup are reviewed here.",
                Location = new Point(154, 142),
                Size = new Size(520, 18),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            };
            _lblProfileChecklist = new Label
            {
                Text = "No checklist generated yet.",
                Location = new Point(154, 168),
                Size = new Size(560, 18),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextHint,
                AutoEllipsis = true
            };
            _lblProfileHint = new Label
            {
                Text = "Use Work, Compliance, and Pay to move from profile maintenance into action.",
                Location = new Point(154, 190),
                Size = new Size(560, 18),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = TextHint,
                AutoEllipsis = true
            };

            Button btnUploadPhoto = MakeButton("Upload Photo", Color.White, Blue, 120);
            btnUploadPhoto.Click += (s, e) => UploadPhoto();

            Button btnOpenWork = MakeButton("Open Work", Color.White, Blue, 108);
            btnOpenWork.Click += (s, e) => _tabs.SelectedIndex = 1;
            Button btnOpenCompliance = MakeButton("Open Compliance", Color.White, Blue, 128);
            btnOpenCompliance.Click += (s, e) => _tabs.SelectedIndex = 2;
            Button btnOpenPay = MakeButton("Pay Snapshot", Color.White, Blue, 114);
            btnOpenPay.Click += (s, e) => _tabs.SelectedIndex = 3;
            Button btnPayrollWorkspace = MakeButton("Payroll Workspace", Teal, Color.White, 148);
            btnPayrollWorkspace.Click += (s, e) => (FindForm() as MainForm)?.NavigateTo("Payroll");

            heroBody.Controls.AddRange(new Control[]
            {
                _picPhoto,
                _lblHeroEmployeeName,
                _lblHeroEmployeeMeta,
                _lblHeroStatusChip,
                _lblHeroSiteChip,
                _lblHeroReadinessChip,
                _lblHeroPayrollChip,
                _lblHeroContactChip,
                _lblReadinessHeadline,
                _lblReadinessDetail,
                _lblProfileChecklist,
                _lblProfileHint,
                btnUploadPhoto,
                btnOpenWork,
                btnOpenCompliance,
                btnOpenPay,
                btnPayrollWorkspace
            });
            heroBody.Resize += (s, e) => LayoutProfileHero(heroBody, btnUploadPhoto, btnOpenWork, btnOpenCompliance, btnOpenPay, btnPayrollWorkspace);
            LayoutProfileHero(heroBody, btnUploadPhoto, btnOpenWork, btnOpenCompliance, btnOpenPay, btnPayrollWorkspace);
            flow.Controls.Add(heroCard);

            Panel fieldsCard = MakeCard("Employee profile");
            fieldsCard.Height = 840;
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(InnerPadding), Height = 736 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 10; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));

            _txtCode = AddEditor(grid, 0, 0, "Employee code");
            _txtName = AddEditor(grid, 1, 0, "Employee name");
            _txtDesignation = AddEditor(grid, 0, 1, "Designation");
            _txtDepartment = AddEditor(grid, 1, 1, "Department");
            _cmbSite = AddComboEditor(grid, 0, 2, "Client / Site");
            _txtPhone = AddEditor(grid, 1, 2, "Phone");
            _txtWhatsapp = AddEditor(grid, 0, 3, "WhatsApp");
            _cmbBloodGroup = AddComboEditor(grid, 1, 3, "Blood group");
            new MasterLookupService().BindCombo(_cmbBloodGroup, "HR.BloodGroup", new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" });
            _txtAadhaar = AddEditor(grid, 0, 4, "Aadhaar");
            _txtPan = AddEditor(grid, 1, 4, "PAN");
            _txtEmergencyName = AddEditor(grid, 0, 5, "Emergency contact name");
            _txtEmergencyPhone = AddEditor(grid, 1, 5, "Emergency contact phone");
            _dtpJoining = AddDateEditor(grid, 0, 6, "Joining date");
            _dtpProbationEnd = AddDateEditor(grid, 1, 6, "Probation end");
            _dtpConfirmation = AddDateEditor(grid, 0, 7, "Confirmation date");
            _dtpLastWorkingDay = AddDateEditor(grid, 1, 7, "Last working day");
            _cmbEmployeeStatus = AddComboEditor(grid, 0, 8, "Status");
            new MasterLookupService().BindCombo(_cmbEmployeeStatus, "HR.EmployeeStatus", new[] { "Active", "Inactive", "Leave" }, "Active");
            Panel rehirePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8) };
            Label lblRehire = new Label { Text = "Rehire", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(0, 0) };
            _chkIsRehire = new CheckBox { Text = "Employee is a rehire", AutoSize = true, Location = new Point(0, 28), Font = new Font("Segoe UI", 9F), ForeColor = TextPrimary };
            rehirePanel.Controls.Add(lblRehire);
            rehirePanel.Controls.Add(_chkIsRehire);
            grid.Controls.Add(rehirePanel, 1, 8);
            GetCardBody(fieldsCard).Controls.Add(grid);
            flow.Controls.Add(fieldsCard);

            Panel paySnapshotCard = MakeCard("Pay snapshot");
            paySnapshotCard.Height = 168;
            Panel payBody = GetCardBody(paySnapshotCard);
            payBody.Padding = new Padding(InnerPadding);
            Label lblPayIntro = new Label
            {
                Text = "Keep payroll editing in the dedicated Pay workflow. This summary keeps HR aware without forcing payroll context into every profile edit.",
                Location = new Point(18, 18),
                Size = new Size(660, 36),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            Label lblGrossTitle = new Label { Text = "Gross Salary", Location = new Point(18, 72), Size = new Size(120, 18), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TextSecondary };
            _lblPaySnapshotGross = new Label { Text = IndiaFormatHelper.FormatCurrency(0), Location = new Point(18, 92), Size = new Size(180, 28), Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Teal };
            Label lblNetTitle = new Label { Text = "Net Salary", Location = new Point(236, 72), Size = new Size(120, 18), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TextSecondary };
            _lblPaySnapshotNet = new Label { Text = IndiaFormatHelper.FormatCurrency(0), Location = new Point(236, 92), Size = new Size(180, 28), Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = Blue };
            payBody.Controls.AddRange(new Control[] { lblPayIntro, lblGrossTitle, _lblPaySnapshotGross, lblNetTitle, _lblPaySnapshotNet });
            flow.Controls.Add(paySnapshotCard);

            content.Controls.Add(flow);
            page.Controls.Add(content);
            return page;
        }

        private TabPage BuildJobsTab()
        {
            TabPage page = new TabPage("Work") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            FlowLayoutPanel flow = MakeVerticalFlow();

            Panel intro = MakeCard("Work summary");
            intro.Height = 154;
            Panel introBody = GetCardBody(intro);
            introBody.Padding = new Padding(InnerPadding, 12, InnerPadding, 12);
            Label lblIntroTitle = new Label
            {
                Text = "Field output and attendance now live together.",
                Location = new Point(18, 16),
                Size = new Size(520, 24),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            Label lblIntroDetail = new Label
            {
                Text = "Managers can review assigned work, recent attendance, and closure rhythm in one place instead of switching tabs.",
                Location = new Point(18, 44),
                Size = new Size(620, 36),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            introBody.Controls.AddRange(new Control[] { lblIntroTitle, lblIntroDetail });
            flow.Controls.Add(intro);

            Panel jobsCard = MakeCard("Assigned jobs");
            jobsCard.Height = 360;

            Panel stats = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White };
            _lblJobsTotal = CreateMiniStat(stats, 18, "Total jobs");
            _lblJobsCompleted = CreateMiniStat(stats, 220, "Completed");
            _lblAverageClosure = CreateMiniStat(stats, 422, "Avg closure days");

            _gridJobs = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Job ID", DataPropertyName = "JobID", Width = 70 });
            _gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Site", DataPropertyName = "Site", Width = 180 });
            _gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Job Type", DataPropertyName = "JobType", Width = 130 });
            _gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Assigned Date", DataPropertyName = "AssignedDate", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 110 });
            _gridJobs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Closed Date", DataPropertyName = "ClosedDate", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            StyleDataGrid(_gridJobs);
            _gridJobs.DataBindingComplete += GridJobs_DataBindingComplete;

            Panel jobsBody = GetCardBody(jobsCard);
            jobsBody.Controls.Add(_gridJobs);
            jobsBody.Controls.Add(stats);
            flow.Controls.Add(jobsCard);

            Panel attendanceCard = MakeCard("Attendance review");
            attendanceCard.Height = 360;

            Panel top = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.White };
            Label lblMonth = new Label { Text = "Month / year", AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TextSecondary, Location = new Point(18, 14) };
            _dtpAttendanceMonth = new DateTimePicker
            {
                CustomFormat = "MMMM yyyy",
                Format = DateTimePickerFormat.Custom,
                ShowUpDown = true,
                Location = new Point(18, 34),
                Width = 180,
                Font = new Font("Segoe UI", 9.5F)
            };
            _dtpAttendanceMonth.ValueChanged += (s, e) => RefreshAttendance();
            _lblPresentDays = CreateMiniStat(top, 240, "Present");
            _lblAbsentDays = CreateMiniStat(top, 420, "Absent");
            _lblLateDays = CreateMiniStat(top, 600, "Late");
            _lblLeaveDays = CreateMiniStat(top, 780, "Leave");
            top.Controls.Add(lblMonth);
            top.Controls.Add(_dtpAttendanceMonth);

            _gridAttendance = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "AttendanceDate", Width = 115, MinimumWidth = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Check In", DataPropertyName = "CheckInTime", Width = 105, MinimumWidth = 95 });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Check Out", DataPropertyName = "CheckOutTime", Width = 110, MinimumWidth = 100 });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hours Worked", DataPropertyName = "HoursWorked", Width = 130, MinimumWidth = 125 });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 110, MinimumWidth = 95 });
            StyleDataGrid(_gridAttendance);
            _gridAttendance.DataBindingComplete += GridAttendance_DataBindingComplete;

            Panel attendanceBody = GetCardBody(attendanceCard);
            attendanceBody.Controls.Add(_gridAttendance);
            attendanceBody.Controls.Add(top);
            flow.Controls.Add(attendanceCard);

            host.Controls.Add(flow);
            page.Controls.Add(host);
            return page;
        }

        private TabPage BuildAttendanceTab()
        {
            TabPage page = new TabPage("Attendance") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            Panel card = MakeCard("Attendance");
            card.Dock = DockStyle.Fill;

            Panel top = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Color.White };
            Label lblMonth = new Label { Text = "Month / year", AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = TextSecondary, Location = new Point(18, 14) };
            _dtpAttendanceMonth = new DateTimePicker
            {
                CustomFormat = "MMMM yyyy",
                Format = DateTimePickerFormat.Custom,
                ShowUpDown = true,
                Location = new Point(18, 34),
                Width = 180,
                Font = new Font("Segoe UI", 9.5F)
            };
            _dtpAttendanceMonth.ValueChanged += (s, e) => RefreshAttendance();
            _lblPresentDays = CreateMiniStat(top, 240, "Present");
            _lblAbsentDays = CreateMiniStat(top, 420, "Absent");
            _lblLateDays = CreateMiniStat(top, 600, "Late");
            _lblLeaveDays = CreateMiniStat(top, 780, "Leave");
            top.Controls.Add(lblMonth);
            top.Controls.Add(_dtpAttendanceMonth);

            _gridAttendance = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", DataPropertyName = "AttendanceDate", Width = 115, MinimumWidth = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Check In", DataPropertyName = "CheckInTime", Width = 105, MinimumWidth = 95 });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Check Out", DataPropertyName = "CheckOutTime", Width = 110, MinimumWidth = 100 });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hours Worked", DataPropertyName = "HoursWorked", Width = 130, MinimumWidth = 125 });
            _gridAttendance.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 110, MinimumWidth = 95 });
            StyleDataGrid(_gridAttendance);
            _gridAttendance.DataBindingComplete += GridAttendance_DataBindingComplete;

            Panel body = GetCardBody(card);
            body.Controls.Add(_gridAttendance);
            body.Controls.Add(top);
            host.Controls.Add(card);
            page.Controls.Add(host);
            return page;
        }

        private TabPage BuildSkillsTab()
        {
            TabPage page = new TabPage("Compliance") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            FlowLayoutPanel flow = MakeVerticalFlow();

            Panel actionCard = MakeCard("HR action queue");
            actionCard.Height = 224;
            Panel actionBody = GetCardBody(actionCard);
            TableLayoutPanel actionGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 92,
                ColumnCount = 4,
                Padding = new Padding(18, 18, 18, 8)
            };
            for (int i = 0; i < 4; i++)
                actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _lblActionMissingKyc = AddKpiCard(actionGrid, 0, "Missing KYC", Red);
            _lblActionMissingEmergency = AddKpiCard(actionGrid, 1, "Emergency Gaps", Amber);
            _lblActionProbationDue = AddKpiCard(actionGrid, 2, "Probation Review", Blue);
            _lblActionPayrollBlocked = AddKpiCard(actionGrid, 3, "Payroll Blocked", Red);
            actionBody.Controls.Add(actionGrid);
            flow.Controls.Add(actionCard);

            Panel skillsCard = MakeCard("Skills & certifications");
            skillsCard.Height = 320;

            Panel top = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.White };
            _lblSkillAlert = new Label { AutoSize = true, ForeColor = Red, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(18, 18) };
            _btnAddSkill = MakeButton("Add Skill", Teal, Color.White, 100);
            _btnAddSkill.Location = new Point(18, 40);
            _btnAddSkill.Click += (s, e) => AddSkill();
            top.Controls.Add(_lblSkillAlert);
            top.Controls.Add(_btnAddSkill);

            _gridSkills = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridSkills.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Skill Name", DataPropertyName = "SkillName", Width = 180 });
            _gridSkills.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Certification Number", DataPropertyName = "CertificationNumber", Width = 160 });
            _gridSkills.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry Date", DataPropertyName = "ExpiryDate", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridSkills.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Expired", DataPropertyName = "IsExpired", Width = 70 });
            StyleDataGrid(_gridSkills);
            _gridSkills.DataBindingComplete += GridSkills_DataBindingComplete;

            Panel skillsBody = GetCardBody(skillsCard);
            skillsBody.Controls.Add(_gridSkills);
            skillsBody.Controls.Add(top);
            flow.Controls.Add(skillsCard);

            Panel documentsCard = MakeCard("Employee documents");
            documentsCard.Height = 320;

            Panel documentsTop = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White };
            _btnUploadDocument = MakeButton("Upload Document", Teal, Color.White, 140);
            _btnUploadDocument.Location = new Point(18, 16);
            _btnUploadDocument.Click += (s, e) => UploadDocument();
            documentsTop.Controls.Add(_btnUploadDocument);

            _gridDocuments = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Document Type", DataPropertyName = "DocumentType", Width = 150 });
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File Name", DataPropertyName = "FileName", Width = 220 });
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Uploaded On", DataPropertyName = "UploadedOn", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" } });
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry Date", DataPropertyName = "ExpiryDate", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridDocuments.Columns.Add(new DataGridViewButtonColumn { HeaderText = "", Text = "Download", UseColumnTextForButtonValue = true, Width = 90 });
            StyleDataGrid(_gridDocuments);
            _gridDocuments.CellContentClick += GridDocuments_CellContentClick;

            Panel documentsBody = GetCardBody(documentsCard);
            documentsBody.Controls.Add(_gridDocuments);
            documentsBody.Controls.Add(documentsTop);
            flow.Controls.Add(documentsCard);

            host.Controls.Add(flow);
            page.Controls.Add(host);
            return page;
        }

        private TabPage BuildDocumentsTab()
        {
            TabPage page = new TabPage("Documents") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            Panel card = MakeCard("Employee documents");
            card.Dock = DockStyle.Fill;

            Panel top = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White };
            _btnUploadDocument = MakeButton("Upload Document", Teal, Color.White, 140);
            _btnUploadDocument.Location = new Point(18, 16);
            _btnUploadDocument.Click += (s, e) => UploadDocument();
            top.Controls.Add(_btnUploadDocument);

            _gridDocuments = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Document Type", DataPropertyName = "DocumentType", Width = 150 });
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File Name", DataPropertyName = "FileName", Width = 220 });
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Uploaded On", DataPropertyName = "UploadedOn", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" } });
            _gridDocuments.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry Date", DataPropertyName = "ExpiryDate", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridDocuments.Columns.Add(new DataGridViewButtonColumn { HeaderText = "", Text = "Download", UseColumnTextForButtonValue = true, Width = 90 });
            StyleDataGrid(_gridDocuments);
            _gridDocuments.CellContentClick += GridDocuments_CellContentClick;

            Panel body = GetCardBody(card);
            body.Controls.Add(_gridDocuments);
            body.Controls.Add(top);
            host.Controls.Add(card);
            page.Controls.Add(host);
            return page;
        }

        private TabPage BuildPayrollTab()
        {
            TabPage page = new TabPage("Pay") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            FlowLayoutPanel flow = MakeVerticalFlow();

            Panel introCard = MakeCard("Pay operations");
            introCard.Height = 170;
            Panel introBody = GetCardBody(introCard);
            introBody.Padding = new Padding(18, 16, 18, 16);
            Label lblPayOpsTitle = new Label
            {
                Text = "Payroll editing is separated from the employee profile.",
                Location = new Point(18, 16),
                Size = new Size(540, 24),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            Label lblPayOpsDetail = new Label
            {
                Text = "This tab handles structure and slip generation. Use the main Payroll workspace when you need month-wide processing, locking, or statutory review.",
                Location = new Point(18, 46),
                Size = new Size(680, 40),
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = TextSecondary
            };
            Button btnOpenPayrollWorkspace = MakeButton("Open Payroll Workspace", Color.White, Blue, 214);
            btnOpenPayrollWorkspace.Location = new Point(18, 100);
            btnOpenPayrollWorkspace.Click += (s, e) => (FindForm() as MainForm)?.NavigateTo("Payroll");
            introBody.Resize += (s, e) =>
            {
                lblPayOpsTitle.Width = Math.Max(320, introBody.ClientSize.Width - 36);
                lblPayOpsDetail.Width = Math.Max(360, introBody.ClientSize.Width - 36);
            };
            introBody.Controls.AddRange(new Control[] { lblPayOpsTitle, lblPayOpsDetail, btnOpenPayrollWorkspace });
            flow.Controls.Add(introCard);

            Panel salaryCard = MakeCard("Current salary structure");
            salaryCard.Height = 380;
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(18), AutoSize = false, Height = 320 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 4; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            _txtBasicSalary = AddEditor(grid, 0, 0, "Basic salary");
            _txtHra = AddEditor(grid, 1, 0, "HRA");
            _txtAllowances = AddEditor(grid, 0, 1, "Allowances");
            _txtPfDeduction = AddEditor(grid, 1, 1, "PF deduction");
            _txtEsicDeduction = AddEditor(grid, 0, 2, "ESIC deduction");
            _dtpSalaryEffectiveFrom = AddDateEditor(grid, 1, 2, "Effective from");
            Panel calcPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8) };
            Label lblGrossTitle = new Label { Text = "Gross Salary", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(0, 2) };
            _lblGrossSalary = new Label { Text = IndiaFormatHelper.FormatCurrency(0), AutoSize = true, ForeColor = Teal, Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(0, 22) };
            Label lblNetTitle = new Label { Text = "Net Salary", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(0, 62) };
            _lblNetSalary = new Label { Text = IndiaFormatHelper.FormatCurrency(0), AutoSize = true, ForeColor = Blue, Font = new Font("Segoe UI", 14F, FontStyle.Bold), Location = new Point(0, 82) };
            calcPanel.Controls.AddRange(new Control[] { lblGrossTitle, _lblGrossSalary, lblNetTitle, _lblNetSalary });
            grid.Controls.Add(calcPanel, 1, 3);
            GetCardBody(salaryCard).Controls.Add(grid);
            flow.Controls.Add(salaryCard);

            Panel advancesCard = MakeCard("Salary advances");
            advancesCard.Height = 260;
            _gridAdvances = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _gridAdvances.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Advance Date", DataPropertyName = "AdvanceDate", Width = 125, MinimumWidth = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _gridAdvances.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Amount", DataPropertyName = "AdvanceAmount", Width = 130, MinimumWidth = 120 });
            _gridAdvances.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Recovery Month", DataPropertyName = "RecoveryMonth", Width = 145, MinimumWidth = 135 });
            _gridAdvances.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Recovered Amount", DataPropertyName = "Recovered", Width = 150, MinimumWidth = 145 });
            StyleDataGrid(_gridAdvances);
            GetCardBody(advancesCard).Controls.Add(_gridAdvances);
            flow.Controls.Add(advancesCard);

            Panel actionPanel = new Panel { Width = 760, Height = 46, BackColor = PageBg, Margin = new Padding(0, 0, 0, 0), MinimumSize = new Size(360, 46) };
            _btnGenerateSalarySlip = MakeButton("Generate Salary Slip", Teal, Color.White, 170);
            _btnGenerateSalarySlip.Location = new Point(0, 8);
            _btnGenerateSalarySlip.Click += async (s, e) => await GenerateSalarySlipAsync();
            actionPanel.Controls.Add(_btnGenerateSalarySlip);
            flow.Controls.Add(actionPanel);

            AttachMoneyRecalc(_txtBasicSalary, _txtHra, _txtAllowances, _txtPfDeduction, _txtEsicDeduction);
            host.Controls.Add(flow);
            page.Controls.Add(host);
            return page;
        }

        private void LoadData()
        {
            try
            {
                _pendingRestoreEmployeeId = _currentEmployee == null ? 0 : _currentEmployee.EmployeeID;
                _pendingRestoreTabIndex = _tabs == null ? 0 : _tabs.SelectedIndex;
                _currentEmployee = null;
                _currentSalaryProfile = new EmployeeSalaryProfileDto { EffectiveFrom = DateTime.Today };
                _currentPhoto = null;
                ClearCurrentEmployeeView();

                try
                {
                    _expiringSkills = _employeeService.GetExpiringSkills(30) ?? new List<EmployeeSkillDto>();
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("EmployeeForm.LoadData.ExpiringSkills", ex);
                    _expiringSkills = new List<EmployeeSkillDto>();
                }

                LoadKpis();
                UpdateExpiringBanner();
                LoadEmployees();
                _suppressEmployeeFilterEvents = true;
                try
                {
                    PopulateLeftFilters();
                    PopulateSiteOptions();
                }
                finally
                {
                    _suppressEmployeeFilterEvents = false;
                }

                if (_gridEmployees.Rows.Count > 0)
                {
                    if (_pendingRestoreEmployeeId > 0)
                        SelectEmployeeRow(_pendingRestoreEmployeeId);

                    if (_gridEmployees.CurrentRow == null)
                        _gridEmployees.Rows[0].Selected = true;
                }

                if (_tabs != null)
                    _tabs.SelectedIndex = Math.Max(0, Math.Min(_pendingRestoreTabIndex, _tabs.TabPages.Count - 1));

                SetStatus("Employee module loaded.", TextSecondary);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadData", ex);
                SetStatus("Load failed: " + ex.Message, Red);
            }
        }

        private void LoadKpis()
        {
            _lblTotalEmployees.Text = ExecuteScalarIntSafe(
                "EmployeeForm.LoadKpis.TotalEmployees",
                "SELECT COUNT(*) FROM dbo.Employees").ToString();

            _lblActiveToday.Text = ExecuteScalarIntSafe(
                "EmployeeForm.LoadKpis.ActiveToday",
                "SELECT COUNT(*) FROM dbo.Employees WHERE Status = 'Active'").ToString();

            _lblOnDuty.Text = ExecuteScalarIntSafe(
                "EmployeeForm.LoadKpis.OnDuty",
                @"SELECT COUNT(*) FROM dbo.AttendanceRecords
                  WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                    AND Status IN ('Present', 'Late', 'HalfDay');",
                attendanceSafe: true).ToString();

            _lblOnLeave.Text = ExecuteScalarIntSafe(
                "EmployeeForm.LoadKpis.OnLeave",
                @"SELECT COUNT(*) FROM dbo.AttendanceRecords
                  WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                    AND Status = 'Leave';",
                attendanceSafe: true).ToString();
        }

        private int ExecuteScalarIntSafe(string operation, string sql, bool attendanceSafe = false)
        {
            try
            {
                if (attendanceSafe && !AttendanceRecordsTableExists())
                    return 0;

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(operation, ex);
                SetStatus("Could not load employee dashboard metrics: " + ex.Message, Red);
                return 0;
            }
        }

        private bool AttendanceRecordsTableExists()
        {
            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = 'AttendanceRecords';", conn))
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private void LoadEmployees()
        {
            try
            {
                string search = (_txtSearch.Text ?? string.Empty).Trim();
                string clientSite = _cmbClientFilter.SelectedItem as string ?? "All";
                string status = _cmbStatusFilter.SelectedItem as string ?? "All";

                DataTable table = LoadEmployeeTable(search, clientSite, status);
                LoadCheckedInEmployeesToday();
                BindEmployeeTable(table, search, clientSite, status);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadEmployees", ex);
                _gridEmployees.DataSource = null;
                SetStatus("Could not load employees: " + ex.Message, Red);
            }
        }

        /// <summary>Loads employee grid rows using the real Employees.Name column.</summary>
        private DataTable LoadEmployeeTable(string search, string clientSite, string status)
        {
            DataTable table = new DataTable();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                        SELECT EmployeeID, EmployeeCode, Name AS EmployeeName, Designation, Department, ClientSite, Phone, WhatsAppNumber, Status,
                               PANNumber, AadhaarNumber, EmergencyContactName, EmergencyContactPhone, ProbationEndDate, ConfirmationDate
                        FROM dbo.Employees
                        WHERE (@search = ''
                               OR Name LIKE '%' + @search + '%'
                               OR EmployeeCode LIKE '%' + @search + '%'
                               OR Designation LIKE '%' + @search + '%'
                               OR ClientSite LIKE '%' + @search + '%')
                          AND (@clientSite = '' OR ClientSite = @clientSite)
                          AND (@status = '' OR Status = @status)
                        ORDER BY Name ASC;", conn))
                {
                    cmd.Parameters.AddWithValue("@search", search ?? string.Empty);
                    cmd.Parameters.AddWithValue("@clientSite", string.Equals(clientSite, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : (clientSite ?? string.Empty));
                    cmd.Parameters.AddWithValue("@status", string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : (status ?? string.Empty));

                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        adapter.Fill(table);
                }
            }

            return table;
        }

        /// <summary>Binds employee grid rows and refreshes the in-memory summary list.</summary>
        private void BindEmployeeTable(DataTable table, string search, string clientSite, string status)
        {
            table = table ?? new DataTable();
            EnsureNeedsActionColumn(table);
            _currentEmployeeTable = table;
            _employeeSummaries = new List<EmployeeSummaryDto>();
            foreach (DataRow row in table.Rows)
            {
                _employeeSummaries.Add(new EmployeeSummaryDto
                {
                    EmployeeID = row["EmployeeID"] == DBNull.Value ? 0 : Convert.ToInt32(row["EmployeeID"]),
                    EmployeeCode = row["EmployeeCode"] == DBNull.Value ? string.Empty : Convert.ToString(row["EmployeeCode"]),
                    Name = row["EmployeeName"] == DBNull.Value ? string.Empty : Convert.ToString(row["EmployeeName"]),
                    Designation = row["Designation"] == DBNull.Value ? string.Empty : Convert.ToString(row["Designation"]),
                    Department = row["Department"] == DBNull.Value ? string.Empty : Convert.ToString(row["Department"]),
                    ClientSite = row["ClientSite"] == DBNull.Value ? string.Empty : Convert.ToString(row["ClientSite"]),
                    Phone = row["Phone"] == DBNull.Value ? string.Empty : Convert.ToString(row["Phone"]),
                    Status = row["Status"] == DBNull.Value ? string.Empty : Convert.ToString(row["Status"])
                });
            }

            _gridEmployees.DataSource = table;
            UpdateHrActionQueue(table);
            UpdateLeftWorkspaceSummary(table);
            UpdateEmployeeDashboard(null, table);
            if (table.Rows.Count == 0 && HasEmployeeFilters(search, clientSite, status))
                SetStatus("No employees match current filters. Clear filters to show all employees.", Amber);
        }

        private void UpdateEmployeeDashboard(EmployeeDashboardStats stats, DataTable table)
        {
            table = table ?? _currentEmployeeTable ?? new DataTable();
            stats = stats ?? new EmployeeDashboardStats
            {
                TotalEmployees = _employeeSummaries.Count,
                ActiveToday = _employeeSummaries.Count(x => !string.Equals(x.Status, "Inactive", StringComparison.OrdinalIgnoreCase)),
                OnDuty = _employeeSummaries.Count(x => x.CheckedInToday),
                OnLeave = _employeeSummaries.Count(x => x.OnLeaveToday)
            };

            int readyCount = 0;
            int needsActionCount = 0;
            var attentionLines = new List<string>();
            foreach (DataRow row in table.Rows)
            {
                string issue = GetRowString(row, "NeedsAction");
                string employeeName = GetRowString(row, "EmployeeName");
                if (string.Equals(issue, "Ready", StringComparison.OrdinalIgnoreCase))
                    readyCount++;
                else
                {
                    needsActionCount++;
                    if (attentionLines.Count < 4)
                        attentionLines.Add(employeeName + ": " + issue);
                }
            }

            if (_lblDashboardReadyCount != null) _lblDashboardReadyCount.Text = readyCount.ToString();
            if (_lblDashboardNeedsActionCount != null) _lblDashboardNeedsActionCount.Text = needsActionCount.ToString();
            if (_lblDashboardCheckedInCount != null) _lblDashboardCheckedInCount.Text = stats.OnDuty.ToString();
            if (_lblDashboardExpiringCount != null) _lblDashboardExpiringCount.Text = _expiringSkills.Count.ToString();

            if (_lblDashboardCoverageText != null)
            {
                int assignedSites = _employeeSummaries.Count(x => !string.IsNullOrWhiteSpace(x.ClientSite));
                int unassignedSites = Math.Max(0, _employeeSummaries.Count - assignedSites);
                int distinctSites = _employeeSummaries
                    .Where(x => !string.IsNullOrWhiteSpace(x.ClientSite))
                    .Select(x => x.ClientSite)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var siteGroups = _employeeSummaries
                    .Where(x => !string.IsNullOrWhiteSpace(x.ClientSite))
                    .GroupBy(x => x.ClientSite)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Take(4)
                    .Select(g => new { Site = g.Key, Count = g.Count() })
                    .ToList();
                if (_lblDashboardCoverageHeadline != null)
                {
                    _lblDashboardCoverageHeadline.Text = distinctSites > 0
                        ? distinctSites + " active sites | " + assignedSites + " assigned people visible."
                        : "Site coverage has not been assigned yet.";
                }
                _lblDashboardCoverageText.Text = siteGroups.Any()
                    ? "Busiest visible sites by roster load."
                    : "No site assignments are visible yet. Use the employee workspace to assign people to active client sites.";
                int totalVisible = Math.Max(1, _employeeSummaries.Count);
                int coverageRate = (int)Math.Round((assignedSites / (double)totalVisible) * 100d);
                if (_lblDashboardCoverageRate != null)
                    _lblDashboardCoverageRate.Text = coverageRate + "%";
                if (_lblDashboardCoverageAssignedMeta != null)
                    _lblDashboardCoverageAssignedMeta.Text = "Assigned: " + assignedSites + " people";
                if (_lblDashboardCoverageUnassignedMeta != null)
                    _lblDashboardCoverageUnassignedMeta.Text = "Unassigned: " + unassignedSites + " people";
                if (_lblDashboardCoverageTopSiteMeta != null)
                    _lblDashboardCoverageTopSiteMeta.Text = siteGroups.Any()
                        ? "Top site: " + siteGroups[0].Site + Environment.NewLine + siteGroups[0].Count + " people"
                        : "Top site: Waiting for roster";
                if (_dashboardCoverageList != null)
                {
                    _dashboardCoverageList.SuspendLayout();
                    _dashboardCoverageList.Controls.Clear();
                    if (siteGroups.Any())
                    {
                        int maxCount = siteGroups.Max(x => x.Count);
                        Color[] accents =
                        {
                            Blue,
                            Teal,
                            Amber,
                            Color.FromArgb(195, 93, 92)
                        };
                        for (int i = 0; i < siteGroups.Count; i++)
                        {
                            var group = siteGroups[i];
                            _dashboardCoverageList.Controls.Add(CreateCoverageBarRow(group.Site, group.Count, maxCount, accents[Math.Min(i, accents.Length - 1)]));
                        }
                    }
                    else
                    {
                        _dashboardCoverageList.Controls.Add(CreateCoverageBarRow("No active site assignments", 0, 1, Border));
                    }
                    _dashboardCoverageList.ResumeLayout();
                }
            }

            if (_lblDashboardAttentionText != null)
            {
                var expiringLines = _expiringSkills
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.EmployeeName))
                    .Take(Math.Max(0, 4 - attentionLines.Count))
                    .Select(x => x.EmployeeName + ": " + x.SkillName + " expiring");
                List<string> lines = attentionLines.Concat(expiringLines).Take(4).ToList();
                if (_lblDashboardAttentionHeadline != null)
                {
                    _lblDashboardAttentionHeadline.Text = lines.Count == 0
                        ? "No urgent HR blockers are visible right now."
                        : lines.Count + " immediate follow-up signal(s) surfaced from the current roster.";
                }
                _lblDashboardAttentionText.Text = lines.Count == 0
                    ? "Profiles, compliance items, and payroll prerequisites look stable from this dashboard pass."
                    : "Priority queue for the current shift and HR handoff.";
                PopulateDashboardAttentionList(lines);
            }

            if (_gridDashboardRoster != null)
            {
                string dashboardSearch = (_txtDashboardSearch == null ? string.Empty : (_txtDashboardSearch.Text ?? string.Empty)).Trim();
                int pageSize = 15;
                bool showAllRows = false;
                if (_cmbDashboardPageSize != null && _cmbDashboardPageSize.SelectedItem != null)
                {
                    string selectedPageSize = Convert.ToString(_cmbDashboardPageSize.SelectedItem);
                    showAllRows = selectedPageSize.IndexOf("All", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!showAllRows)
                    {
                        int.TryParse(new string((selectedPageSize ?? string.Empty).TakeWhile(char.IsDigit).ToArray()), out pageSize);
                        if (pageSize <= 0) pageSize = 15;
                    }
                }
                var filteredSummaries = _employeeSummaries
                    .Where(x => string.IsNullOrWhiteSpace(dashboardSearch)
                        || ((x.Name ?? string.Empty) + " " + (x.EmployeeCode ?? string.Empty) + " " + (x.Designation ?? string.Empty) + " " + (x.ClientSite ?? string.Empty)).IndexOf(dashboardSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(x => x.CheckedInToday)
                    .ThenBy(x => x.OnLeaveToday)
                    .ThenBy(x => x.Name)
                    .ToList();
                List<EmployeeSummaryDto> visibleSummaries = showAllRows ? filteredSummaries : filteredSummaries.Take(pageSize).ToList();
                int rowNumber = 1;
                _gridDashboardRoster.DataSource = visibleSummaries
                    .Select(x => new DashboardRosterRow
                    {
                        RowNumber = rowNumber++,
                        EmployeeID = x.EmployeeID,
                        Name = x.Name,
                        Designation = x.Designation,
                        ClientSite = x.ClientSite,
                        PresenceState = x.OnLeaveToday ? "Leave" : (x.CheckedInToday ? "Checked in" : (string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase) ? "Active" : x.Status)),
                        ReadinessState = GetEmployeeReadinessText(x.EmployeeID),
                        Mobile = string.IsNullOrWhiteSpace(x.Phone) ? "-" : x.Phone,
                        Email = BuildEmployeeEmail(x),
                        Actions = "View  Edit"
                    })
                    .ToList();
                if (_lblDashboardCurrentSelection != null)
                {
                    if (visibleSummaries.Count == 0)
                        _lblDashboardCurrentSelection.Text = "Showing 0 of " + filteredSummaries.Count + " matching employee(s)";
                    else
                        _lblDashboardCurrentSelection.Text = "Showing 1 to " + visibleSummaries.Count + " of " + filteredSummaries.Count + " matching employee(s)";
                }
            }

            UpdateDashboardCurrentSelection();
        }

        private void UpdateDashboardCurrentSelection()
        {
            if (_lblDashboardCurrentSelection == null)
                return;
            if (_cmbDashboardPageSize != null && _gridDashboardRoster != null)
                return;

            DashboardRosterRow rosterSelection = _gridDashboardRoster == null || _gridDashboardRoster.CurrentRow == null
                ? null
                : _gridDashboardRoster.CurrentRow.DataBoundItem as DashboardRosterRow;

            if (_currentEmployee == null && rosterSelection == null)
            {
                _lblDashboardCurrentSelection.Text = "Current workspace pick: choose a person from the employee workspace when you need to edit details.";
                return;
            }

            int employeeId = 0;
            string employeeName;
            string site;

            if (_currentEmployee != null)
            {
                employeeId = _currentEmployee.EmployeeID;
                employeeName = string.IsNullOrWhiteSpace(_currentEmployee.Name) ? "Unnamed employee" : _currentEmployee.Name;
                site = string.IsNullOrWhiteSpace(_currentEmployee.ClientSite) ? "site pending" : _currentEmployee.ClientSite;
            }
            else
            {
                employeeId = rosterSelection.EmployeeID;
                employeeName = string.IsNullOrWhiteSpace(rosterSelection.Name) ? "Unnamed employee" : rosterSelection.Name;
                site = string.IsNullOrWhiteSpace(rosterSelection.ClientSite) ? "site pending" : rosterSelection.ClientSite;
            }

            string readiness = GetEmployeeReadinessText(employeeId);
            _lblDashboardCurrentSelection.Text = "Current workspace pick: " + employeeName + " | " + site;
            _lblDashboardCurrentSelection.Text += " | " + readiness;
        }

        private void PopulateDashboardAttentionList(List<string> lines)
        {
            if (_dashboardAttentionList == null)
                return;

            _dashboardAttentionList.SuspendLayout();
            _dashboardAttentionList.Controls.Clear();
            lines = lines ?? new List<string>();
            string[] times = { "12:48 AM", "12:18 AM", "12:15 AM", "12:12 AM" };

            for (int i = 0; i < lines.Count; i++)
                _dashboardAttentionList.Controls.Add(CreateAttentionRow(lines[i], times[Math.Min(i, times.Length - 1)], i == 0));

            if (lines.Count == 0)
                _dashboardAttentionList.Controls.Add(CreateAttentionRow("No active follow-up queue.", "Stable", false));

            _dashboardAttentionList.ResumeLayout();
        }

        private Control CreateAttentionRow(string text, string timeText, bool highPriority)
        {
            Panel row = new Panel
            {
                Width = 448,
                Height = 24,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.White
            };

            Panel dot = new Panel
            {
                Location = new Point(0, 7),
                Size = new Size(10, 10),
                BackColor = highPriority ? Red : Amber
            };
            DS.Rounded(dot, 5);

            Panel card = new Panel
            {
                Location = new Point(18, 0),
                Size = new Size(430, 24),
                BackColor = Color.White
            };
            card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Color.FromArgb(226, 230, 238)), 0, 0, card.Width - 1, card.Height - 1);

            Label lblText = new Label
            {
                Text = text,
                Location = new Point(10, 4),
                Size = new Size(330, 16),
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = TextPrimary
            };
            Label lblTime = new Label
            {
                Text = timeText,
                Location = new Point(356, 4),
                Size = new Size(62, 16),
                Font = new Font("Segoe UI", 7.75F),
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            };

            card.Controls.Add(lblText);
            card.Controls.Add(lblTime);
            row.Controls.Add(dot);
            row.Controls.Add(card);
            return row;
        }

        private string GetEmployeeReadinessText(int employeeId)
        {
            if (employeeId <= 0 || _currentEmployeeTable == null)
                return "Review profile, compliance, and pay details.";

            foreach (DataRow row in _currentEmployeeTable.Rows)
            {
                if (row["EmployeeID"] != DBNull.Value && Convert.ToInt32(row["EmployeeID"]) == employeeId)
                {
                    string issue = GetRowString(row, "NeedsAction");
                    return string.IsNullOrWhiteSpace(issue) ? "Ready" : issue;
                }
            }

            return "Review profile, compliance, and pay details.";
        }

        private void EnsureNeedsActionColumn(DataTable table)
        {
            if (table == null)
                return;

            if (!table.Columns.Contains("NeedsAction"))
                table.Columns.Add("NeedsAction", typeof(string));

            foreach (DataRow row in table.Rows)
                row["NeedsAction"] = BuildEmployeeIssueSummary(row);
        }

        private void UpdateHrActionQueue(DataTable table)
        {
            if (_lblActionMissingKyc == null || _lblActionMissingEmergency == null || _lblActionProbationDue == null || _lblActionPayrollBlocked == null)
                return;

            table = table ?? new DataTable();
            int missingKyc = 0;
            int missingEmergency = 0;
            int probationDue = 0;
            int payrollBlocked = 0;

            foreach (DataRow row in table.Rows)
            {
                bool activeLike = !string.Equals(GetRowString(row, "Status"), "Inactive", StringComparison.OrdinalIgnoreCase);
                bool missingPan = string.IsNullOrWhiteSpace(GetRowString(row, "PANNumber"));
                bool missingAadhaar = string.IsNullOrWhiteSpace(GetRowString(row, "AadhaarNumber"));
                bool missingEmergencyPhone = string.IsNullOrWhiteSpace(GetRowString(row, "EmergencyContactPhone"));
                bool missingEmergencyName = string.IsNullOrWhiteSpace(GetRowString(row, "EmergencyContactName"));
                DateTime? probationEnd = GetRowDate(row, "ProbationEndDate");
                DateTime? confirmationDate = GetRowDate(row, "ConfirmationDate");

                if (activeLike && (missingPan || missingAadhaar))
                    missingKyc++;
                if (activeLike && (missingEmergencyPhone || missingEmergencyName))
                    missingEmergency++;
                if (activeLike && probationEnd.HasValue && probationEnd.Value.Date <= DateTime.Today && !confirmationDate.HasValue)
                    probationDue++;
                if (activeLike && (missingPan || missingAadhaar || missingEmergencyPhone || missingEmergencyName))
                    payrollBlocked++;
            }

            _lblActionMissingKyc.Text = missingKyc.ToString();
            _lblActionMissingEmergency.Text = missingEmergency.ToString();
            _lblActionProbationDue.Text = probationDue.ToString();
            _lblActionPayrollBlocked.Text = payrollBlocked.ToString();
        }

        private string BuildEmployeeIssueSummary(DataRow row)
        {
            if (row == null)
                return string.Empty;

            var issues = new List<string>();
            string status = GetRowString(row, "Status");
            if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
                return "Inactive record";

            if (string.IsNullOrWhiteSpace(GetRowString(row, "PANNumber")) || string.IsNullOrWhiteSpace(GetRowString(row, "AadhaarNumber")))
                issues.Add("KYC");
            if (string.IsNullOrWhiteSpace(GetRowString(row, "EmergencyContactName")) || string.IsNullOrWhiteSpace(GetRowString(row, "EmergencyContactPhone")))
                issues.Add("Emergency");
            DateTime? probationEnd = GetRowDate(row, "ProbationEndDate");
            DateTime? confirmationDate = GetRowDate(row, "ConfirmationDate");
            if (probationEnd.HasValue && probationEnd.Value.Date <= DateTime.Today && !confirmationDate.HasValue)
                issues.Add("Probation");
            if (string.IsNullOrWhiteSpace(GetRowString(row, "ClientSite")))
                issues.Add("Site");

            return issues.Count == 0 ? "Ready" : string.Join(" + ", issues);
        }

        private void LoadCheckedInEmployeesToday()
        {
            _checkedInTodayEmployeeIds = LoadCheckedInEmployeesTodaySet();
        }

        /// <summary>Returns employees checked in today without touching UI state.</summary>
        private HashSet<int> LoadCheckedInEmployeesTodaySet()
        {
            var ids = new HashSet<int>();
            if (!AttendanceRecordsTableExists())
                return ids;

            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT DISTINCT EmployeeId
                        FROM dbo.AttendanceRecords
                        WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                          AND Status IN ('Present', 'Late', 'HalfDay');", conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            ids.Add(reader["EmployeeId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EmployeeId"]));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadCheckedInEmployeesToday", ex);
            }

            return ids;
        }

        private void GridEmployees_CellFormattingSafe(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (_gridEmployees.Rows[e.RowIndex].DataBoundItem is DataRowView tooltipRowView)
            {
                string issueSummary = Convert.ToString(tooltipRowView.Row["NeedsAction"]) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(issueSummary))
                    _gridEmployees.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = issueSummary;
            }

            string headerText = _gridEmployees.Columns[e.ColumnIndex].HeaderText ?? string.Empty;
            if (headerText == "Role" || headerText == "Employee")
            {
                string text = Convert.ToString(e.Value) ?? string.Empty;
                _gridEmployees.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = text;
                return;
            }

            if (e.ColumnIndex != 0)
                return;

            DataGridViewRow row = _gridEmployees.Rows[e.RowIndex];
            if (!(row.DataBoundItem is DataRowView rowView))
                return;

            int employeeId = rowView.Row["EmployeeID"] == DBNull.Value ? 0 : Convert.ToInt32(rowView.Row["EmployeeID"]);
            string status = Convert.ToString(rowView.Row["Status"]) ?? string.Empty;

            DataGridViewCell cell = row.Cells[e.ColumnIndex];
            e.Value = "\u25CF";
            e.FormattingApplied = true;
            if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
                cell.Style.ForeColor = TextHint;
            else if (_checkedInTodayEmployeeIds.Contains(employeeId))
                cell.Style.ForeColor = Teal;
            else
            cell.Style.ForeColor = Amber;
            cell.Style.Font = new Font("Segoe UI Symbol", 14F, FontStyle.Bold);
        }

        private void GridDashboardRoster_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_gridDashboardRoster == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string propertyName = _gridDashboardRoster.Columns[e.ColumnIndex].DataPropertyName ?? string.Empty;
            if (string.Equals(propertyName, "PresenceState", StringComparison.OrdinalIgnoreCase))
            {
                string text = Convert.ToString(e.Value) ?? string.Empty;
                e.CellStyle.ForeColor = string.Equals(text, "Active", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Checked in", StringComparison.OrdinalIgnoreCase)
                    ? Teal
                    : (string.Equals(text, "Leave", StringComparison.OrdinalIgnoreCase) ? Red : TextPrimary);
                e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            }
            else if (string.Equals(propertyName, "ReadinessState", StringComparison.OrdinalIgnoreCase))
            {
                string text = Convert.ToString(e.Value) ?? string.Empty;
                e.CellStyle.ForeColor = string.Equals(text, "Ready", StringComparison.OrdinalIgnoreCase) ? Teal : Amber;
                e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            }
        }

        private void LoadSelectedEmployeeSafe()
        {
            try
            {
                if (_gridEmployees.CurrentRow == null || !(_gridEmployees.CurrentRow.DataBoundItem is DataRowView rowView))
                    return;

                int employeeId = rowView.Row["EmployeeID"] == DBNull.Value ? 0 : Convert.ToInt32(rowView.Row["EmployeeID"]);
                if (employeeId <= 0)
                    return;

                LoadEmployeeDetailsSafe(employeeId);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadSelectedEmployeeSafe", ex);
                SetStatus("Could not load selected employee: " + ex.Message, Red);
            }
        }

        private void LoadEmployeeDetailsSafe(int employeeId)
        {
            try
            {
                _tabDataEmployeeId = 0;
                _jobsLoaded = false;
                _attendanceLoaded = false;
                _skillsLoaded = false;
                _documentsLoaded = false;
                _payrollLoaded = false;

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP 1
                            EmployeeID,
                            EmployeeCode,
                            Name AS EmployeeName,
                            Designation,
                            Department,
                            ClientSite,
                            Phone,
                            WhatsAppNumber,
                            BloodGroup,
                            AadhaarNumber,
                            PANNumber,
                            EmergencyContactName,
                            EmergencyContactPhone,
                            JoiningDate,
                            ProbationEndDate,
                            ConfirmationDate,
                            LastWorkingDay,
                            Status,
                            IsRehire,
                            Photo
                        FROM dbo.Employees
                        WHERE EmployeeID = @employeeId;", conn))
                    {
                        cmd.Parameters.AddWithValue("@employeeId", employeeId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                ClearCurrentEmployeeView();
                                SetStatus("Employee not found.", Red);
                                return;
                            }

                            _currentEmployee = new Employee
                            {
                                EmployeeID = employeeId,
                                EmployeeCode = reader["EmployeeCode"] == DBNull.Value ? string.Empty : Convert.ToString(reader["EmployeeCode"]),
                                Name = reader["EmployeeName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["EmployeeName"]),
                                Designation = reader["Designation"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Designation"]),
                                Department = reader["Department"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Department"]),
                                ClientSite = reader["ClientSite"] == DBNull.Value ? string.Empty : Convert.ToString(reader["ClientSite"]),
                                Phone = reader["Phone"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Phone"]),
                                WhatsAppNumber = reader["WhatsAppNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["WhatsAppNumber"]),
                                BloodGroup = reader["BloodGroup"] == DBNull.Value ? string.Empty : Convert.ToString(reader["BloodGroup"]),
                                AadhaarNumber = reader["AadhaarNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["AadhaarNumber"]),
                                PANNumber = reader["PANNumber"] == DBNull.Value ? string.Empty : Convert.ToString(reader["PANNumber"]),
                                EmergencyContactName = reader["EmergencyContactName"] == DBNull.Value ? string.Empty : Convert.ToString(reader["EmergencyContactName"]),
                                EmergencyContactPhone = reader["EmergencyContactPhone"] == DBNull.Value ? string.Empty : Convert.ToString(reader["EmergencyContactPhone"]),
                                JoiningDate = reader["JoiningDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["JoiningDate"]),
                                DateOfJoining = reader["JoiningDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["JoiningDate"]),
                                ProbationEndDate = reader["ProbationEndDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ProbationEndDate"]),
                                ConfirmationDate = reader["ConfirmationDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["ConfirmationDate"]),
                                LastWorkingDay = reader["LastWorkingDay"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["LastWorkingDay"]),
                                Status = reader["Status"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Status"]),
                                IsRehire = reader["IsRehire"] != DBNull.Value && Convert.ToBoolean(reader["IsRehire"]),
                                Photo = reader["Photo"] == DBNull.Value ? null : (byte[])reader["Photo"]
                            };
                        }
                    }
                }

                _currentPhoto = _currentEmployee?.Photo;
                _currentSalaryProfile = new EmployeeSalaryProfileDto { EmployeeID = employeeId, EffectiveFrom = DateTime.Today };
                BindOverviewSafe();
                ClearDeferredTabData();
                LoadCurrentEmployeeTabData();
                UpdateSelectedEmployeeReadiness();
                SetStatus("Loaded " + (_currentEmployee?.Name ?? string.Empty), TextSecondary);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadEmployeeDetailsSafe", ex);
                SetStatus("Detail load failed: " + ex.Message, Red);
            }
        }

        private void BindOverviewSafe()
        {
            if (_currentEmployee == null)
            {
                ClearOverviewFields();
                return;
            }

            _txtCode.Text = _currentEmployee.EmployeeCode ?? string.Empty;
            _txtName.Text = _currentEmployee.Name ?? string.Empty;
            _txtDesignation.Text = _currentEmployee.Designation ?? string.Empty;
            _txtDepartment.Text = _currentEmployee.Department ?? string.Empty;
            _cmbSite.Text = _currentEmployee.ClientSite ?? string.Empty;
            _txtPhone.Text = _currentEmployee.Phone ?? string.Empty;
            _txtWhatsapp.Text = _currentEmployee.WhatsAppNumber ?? string.Empty;
            _cmbBloodGroup.Text = _currentEmployee.BloodGroup ?? string.Empty;
            _txtAadhaar.Text = _currentEmployee.AadhaarNumber ?? string.Empty;
            _txtPan.Text = _currentEmployee.PANNumber ?? string.Empty;
            _txtEmergencyName.Text = _currentEmployee.EmergencyContactName ?? string.Empty;
            _txtEmergencyPhone.Text = _currentEmployee.EmergencyContactPhone ?? string.Empty;
            SetDatePicker(_dtpJoining, _currentEmployee.JoiningDate ?? _currentEmployee.DateOfJoining);
            SetDatePicker(_dtpProbationEnd, _currentEmployee.ProbationEndDate);
            SetDatePicker(_dtpConfirmation, _currentEmployee.ConfirmationDate);
            SetDatePicker(_dtpLastWorkingDay, _currentEmployee.LastWorkingDay);
            _cmbEmployeeStatus.Text = _currentEmployee.Status ?? string.Empty;
            _chkIsRehire.Checked = _currentEmployee.IsRehire;
            _picPhoto.Image = ToImage(_currentPhoto);
            if (_btnWhatsapp != null)
                _btnWhatsapp.Enabled = !string.IsNullOrWhiteSpace(_currentEmployee.WhatsAppNumber);
            UpdateSelectedEmployeeReadiness();
            UpdateDashboardCurrentSelection();
        }

        private void ClearCurrentEmployeeView()
        {
            _tabDataEmployeeId = 0;
            _jobsLoaded = false;
            _attendanceLoaded = false;
            _skillsLoaded = false;
            _documentsLoaded = false;
            _payrollLoaded = false;
            ClearOverviewFields();
            ClearDeferredTabData();
            if (_btnWhatsapp != null)
                _btnWhatsapp.Enabled = false;
            UpdateDashboardCurrentSelection();
        }

        private void ClearDeferredTabData()
        {
            _gridJobs.DataSource = null;
            _gridAttendance.DataSource = null;
            _gridSkills.DataSource = null;
            _gridDocuments.DataSource = null;
            _gridAdvances.DataSource = null;
            _lblJobsTotal.Text = "0";
            _lblJobsCompleted.Text = "0";
            _lblAverageClosure.Text = "0";
            _lblPresentDays.Text = "0";
            _lblAbsentDays.Text = "0";
            _lblLateDays.Text = "0";
            _lblLeaveDays.Text = "0";
            _lblSkillAlert.Text = "No certifications loaded.";
            _lblSkillAlert.ForeColor = TextHint;
            _txtBasicSalary.Text = "0.00";
            _txtHra.Text = "0.00";
            _txtAllowances.Text = "0.00";
            _txtPfDeduction.Text = "0.00";
            _txtEsicDeduction.Text = "0.00";
            SetDatePicker(_dtpSalaryEffectiveFrom, DateTime.Today);
            RecalculateSalaryLabels();
        }

        private void ClearOverviewFields()
        {
            _txtCode.Text = string.Empty;
            _txtName.Text = string.Empty;
            _txtDesignation.Text = string.Empty;
            _txtDepartment.Text = string.Empty;
            _cmbSite.Text = string.Empty;
            _txtPhone.Text = string.Empty;
            _txtWhatsapp.Text = string.Empty;
            _cmbBloodGroup.Text = string.Empty;
            _txtAadhaar.Text = string.Empty;
            _txtPan.Text = string.Empty;
            _txtEmergencyName.Text = string.Empty;
            _txtEmergencyPhone.Text = string.Empty;
            SetDatePicker(_dtpJoining, null);
            SetDatePicker(_dtpProbationEnd, null);
            SetDatePicker(_dtpConfirmation, null);
            SetDatePicker(_dtpLastWorkingDay, null);
            _cmbEmployeeStatus.Text = "Active";
            _chkIsRehire.Checked = false;
            _picPhoto.Image = ToImage(null);
            UpdateSelectedEmployeeReadiness();
        }

        private async Task SaveCurrentTabAsync()
        {
            if (_tabs != null && _tabs.SelectedIndex == 3)
            {
                SaveSalaryProfile();
                await Task.CompletedTask;
                return;
            }

            SaveOverview();
            await Task.CompletedTask;
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("Employees", this, _btnNew, _btnSave, _btnDelete);
        }

        private void SetStatus(string message, Color color)
        {
            if (_lblStatus == null)
                return;

            _lblStatus.Text = message;
            _lblStatus.ForeColor = color;
        }
        private void GridJobs_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in _gridJobs.Rows)
            {
                EmployeeJobSummaryDto job = row.DataBoundItem as EmployeeJobSummaryDto;
                if (job == null)
                    continue;

                bool overdueOpen = !IsClosedStatus(job.Status) && job.AssignedDate.Date <= DateTime.Today.AddDays(-7);
                if (overdueOpen)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 238, 238);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(132, 45, 45);
                }
            }
        }

        private void GridAttendance_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in _gridAttendance.Rows)
            {
                EmployeeAttendanceDayDto attendance = row.DataBoundItem as EmployeeAttendanceDayDto;
                if (attendance == null)
                    continue;

                switch ((attendance.Status ?? string.Empty).Trim())
                {
                    case "Present":
                        row.DefaultCellStyle.BackColor = TealLight;
                        break;
                    case "Absent":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 234, 234);
                        break;
                    case "Late":
                        row.DefaultCellStyle.BackColor = AmberLight;
                        break;
                    case "Leave":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(240, 244, 255);
                        break;
                }
            }
        }

        private void GridSkills_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in _gridSkills.Rows)
            {
                EmployeeSkillDto skill = row.DataBoundItem as EmployeeSkillDto;
                if (skill != null && skill.IsExpired)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 234, 234);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(132, 45, 45);
                }
            }
        }

        private void GridDocuments_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _gridDocuments.Columns.Count - 1)
                return;

            EmployeeDocumentDto doc = _gridDocuments.Rows[e.RowIndex].DataBoundItem as EmployeeDocumentDto;
            if (doc == null)
                return;

            try
            {
                EmployeeDocumentDto fullDoc = _employeeService.GetDocumentById(doc.DocumentID);
                if (fullDoc == null || fullDoc.FileData == null)
                {
                    SetStatus("Document data not found.", Red);
                    return;
                }

                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.FileName = fullDoc.FileName;
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;
                    File.WriteAllBytes(dialog.FileName, fullDoc.FileData);
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.GridDocuments_CellContentClick", ex);
                SetStatus("Download failed: " + ex.Message, Red);
            }
        }

        private void UpdateExpiringBanner()
        {
            if (_expiringSkills.Count <= 0)
            {
                _lnkExpiringBanner.Visible = false;
                return;
            }

            _lnkExpiringBanner.Text = _expiringSkills.Count + " certifications expiring within 30 days. Click to review.";
            _lnkExpiringBanner.Visible = true;
        }

        private void ShowExpiringSkillsReview()
        {
            if (_expiringSkills.Count == 0)
                return;

            _tabs.SelectedIndex = 2;
            string message = string.Join(Environment.NewLine, _expiringSkills.Take(12).Select(x =>
                (x.EmployeeName ?? "Employee") + " - " + x.SkillName + " - " + IndiaFormatHelper.FormatDate(x.ExpiryDate)));
            MessageBox.Show(message, "Expiring certifications", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SelectEmployeeRow(int employeeId)
        {
            foreach (DataGridViewRow row in _gridEmployees.Rows)
            {
                EmployeeSummaryDto summary = row.DataBoundItem as EmployeeSummaryDto;
                if (summary != null && summary.EmployeeID == employeeId)
                {
                    row.Selected = true;
                    _gridEmployees.CurrentCell = row.Cells[1];
                    break;
                }
            }
        }

        private void RecalculateSalaryLabels()
        {
            decimal basic = ParseMoney(_txtBasicSalary.Text);
            decimal hra = ParseMoney(_txtHra.Text);
            decimal allowances = ParseMoney(_txtAllowances.Text);
            decimal pf = ParseMoney(_txtPfDeduction.Text);
            decimal esic = ParseMoney(_txtEsicDeduction.Text);
            decimal gross = basic + hra + allowances;
            decimal net = gross - pf - esic;
            _lblGrossSalary.Text = IndiaFormatHelper.FormatCurrency(gross);
            _lblNetSalary.Text = IndiaFormatHelper.FormatCurrency(net);
            if (_lblPaySnapshotGross != null)
                _lblPaySnapshotGross.Text = IndiaFormatHelper.FormatCurrency(gross);
            if (_lblPaySnapshotNet != null)
                _lblPaySnapshotNet.Text = IndiaFormatHelper.FormatCurrency(net);
        }

        private void UpdateSelectedEmployeeReadiness()
        {
            if (_lblReadinessHeadline == null || _lblReadinessDetail == null)
                return;

            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                _lblHeroEmployeeName.Text = "Select an employee";
                _lblHeroEmployeeMeta.Text = "Choose a person from the left to review profile, work, compliance, and pay.";
                ApplySummaryChip(_lblHeroStatusChip, "Status pending", TextSecondary, DS.Slate100);
                ApplySummaryChip(_lblHeroSiteChip, "Site pending", DS.Primary700, DS.Primary50);
                ApplySummaryChip(_lblHeroReadinessChip, "Readiness unknown", DS.Amber600, DS.Amber50);
                ApplySummaryChip(_lblHeroPayrollChip, "Payroll pending", DS.Red600, Color.FromArgb(254, 242, 242));
                ApplySummaryChip(_lblHeroContactChip, "Contact not verified", DS.Slate600, DS.Slate100);
                _lblReadinessHeadline.Text = "Select an employee to review HR readiness.";
                _lblReadinessDetail.Text = "This queue highlights KYC gaps, emergency contact issues, probation follow-up, and payroll blockers.";
                _lblReadinessHeadline.ForeColor = TextPrimary;
                _lblProfileChecklist.Text = "Checklist appears here after you select an employee.";
                _lblProfileHint.Text = "Use Work, Compliance, and Pay to move from profile maintenance into action.";
                return;
            }

            var blockers = new List<string>();
            var checklist = new List<string>();
            if (string.IsNullOrWhiteSpace(_currentEmployee.PANNumber) && string.IsNullOrWhiteSpace(_currentEmployee.PAN))
                blockers.Add("PAN missing");
            else
                checklist.Add("PAN");
            if (string.IsNullOrWhiteSpace(_currentEmployee.AadhaarNumber))
                blockers.Add("Aadhaar missing");
            else
                checklist.Add("Aadhaar");
            if (string.IsNullOrWhiteSpace(_currentEmployee.EmergencyContactName) || string.IsNullOrWhiteSpace(_currentEmployee.EmergencyContactPhone))
                blockers.Add("Emergency contact incomplete");
            else
                checklist.Add("Emergency");
            if (string.IsNullOrWhiteSpace(_currentEmployee.ClientSite))
                blockers.Add("Client / Site missing");
            else
                checklist.Add("Site");
            if ((_currentSalaryProfile?.GrossSalary ?? 0m) <= 0m)
                blockers.Add("Salary structure pending");
            else
                checklist.Add("Salary");
            if (_currentEmployee.ProbationEndDate.HasValue && _currentEmployee.ProbationEndDate.Value.Date <= DateTime.Today && !_currentEmployee.ConfirmationDate.HasValue)
                blockers.Add("Probation review due");
            if (string.Equals(_currentEmployee.Status, "Inactive", StringComparison.OrdinalIgnoreCase) && !_currentEmployee.LastWorkingDay.HasValue)
                blockers.Add("Last working day missing");

            string designation = string.IsNullOrWhiteSpace(_currentEmployee.Designation) ? "Role not set" : _currentEmployee.Designation;
            string department = string.IsNullOrWhiteSpace(_currentEmployee.Department) ? "Department pending" : _currentEmployee.Department;
            string code = string.IsNullOrWhiteSpace(_currentEmployee.EmployeeCode) ? "Code pending" : _currentEmployee.EmployeeCode;
            string status = string.IsNullOrWhiteSpace(_currentEmployee.Status) ? "Active" : _currentEmployee.Status;
            string site = string.IsNullOrWhiteSpace(_currentEmployee.ClientSite) ? "Site pending" : _currentEmployee.ClientSite;
            string contact = !string.IsNullOrWhiteSpace(_currentEmployee.WhatsAppNumber)
                ? "WhatsApp verified"
                : (!string.IsNullOrWhiteSpace(_currentEmployee.Phone) ? "Phone saved" : "Contact not verified");
            bool payrollReady = (_currentSalaryProfile?.GrossSalary ?? 0m) > 0m;

            _lblHeroEmployeeName.Text = string.IsNullOrWhiteSpace(_currentEmployee.Name) ? "Unnamed employee" : _currentEmployee.Name;
            _lblHeroEmployeeMeta.Text = code + "  |  " + designation + "  |  " + department;
            ApplySummaryChip(_lblHeroStatusChip, "Status: " + status, string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ? Teal : TextSecondary, string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) ? TealLight : DS.Slate100);
            ApplySummaryChip(_lblHeroSiteChip, string.IsNullOrWhiteSpace(_currentEmployee.ClientSite) ? "Site pending" : "Site: " + site, string.IsNullOrWhiteSpace(_currentEmployee.ClientSite) ? DS.Primary700 : Blue, string.IsNullOrWhiteSpace(_currentEmployee.ClientSite) ? DS.Primary50 : Color.FromArgb(239, 246, 255));
            ApplySummaryChip(_lblHeroReadinessChip, blockers.Count == 0 ? "Ready for HR handoff" : blockers.Count + " follow-up item(s)", blockers.Count == 0 ? Teal : Red, blockers.Count == 0 ? TealLight : Color.FromArgb(255, 241, 242));
            ApplySummaryChip(_lblHeroPayrollChip, payrollReady ? "Payroll structure ready" : "Payroll pending", payrollReady ? Blue : Red, payrollReady ? Color.FromArgb(239, 246, 255) : Color.FromArgb(254, 242, 242));
            ApplySummaryChip(_lblHeroContactChip, contact, !string.IsNullOrWhiteSpace(_currentEmployee.WhatsAppNumber) || !string.IsNullOrWhiteSpace(_currentEmployee.Phone) ? DS.Slate700 : DS.Slate600, !string.IsNullOrWhiteSpace(_currentEmployee.WhatsAppNumber) || !string.IsNullOrWhiteSpace(_currentEmployee.Phone) ? DS.Slate50 : DS.Slate100);

            if (blockers.Count == 0)
            {
                _lblReadinessHeadline.Text = (_currentEmployee.Name ?? "Employee") + " is ready for payroll and HR follow-up.";
                _lblReadinessHeadline.ForeColor = Teal;
                _lblReadinessDetail.Text = "KYC, emergency contact, site assignment, and salary setup look complete from this page.";
                _lblProfileChecklist.Text = checklist.Count == 0 ? "Readiness checklist is complete." : "Ready checklist: " + string.Join(", ", checklist);
                _lblProfileHint.Text = "Use Work for assignments and attendance, Compliance for certificates and documents, and Pay for salary actions.";
                return;
            }

            _lblReadinessHeadline.Text = (_currentEmployee.Name ?? "Employee") + " needs " + blockers.Count + " follow-up item(s).";
            _lblReadinessHeadline.ForeColor = Red;
            _lblReadinessDetail.Text = string.Join(" | ", blockers);
            _lblProfileChecklist.Text = checklist.Count == 0
                ? "No readiness checkpoints are complete yet."
                : "Completed checkpoints: " + string.Join(", ", checklist);
            _lblProfileHint.Text = "Start with the blockers above, then use Compliance and Pay to finish this employee setup.";
        }

        private void AttachMoneyRecalc(params TextBox[] textBoxes)
        {
            foreach (TextBox textBox in textBoxes)
                textBox.TextChanged += (s, e) => RecalculateSalaryLabels();
        }

        private void StyleDataGrid(DataGridView grid)
        {
            GridTheme.Apply(grid);
        }

        private Panel MakeTabScrollHost()
        {
            return new Panel { Dock = DockStyle.Fill, AutoScroll = false, Padding = new Padding(0), BackColor = PageBg };
        }

        private FlowLayoutPanel MakeVerticalFlow()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoSize = false,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            flow.ControlAdded += (s, e) => ResizeFlowChildren(flow);
            flow.Resize += (s, e) => ResizeFlowChildren(flow);
            return flow;
        }

        private Panel MakeCard(string title)
        {
            Panel card = new Panel { Width = 760, BackColor = Color.White, Margin = new Padding(0, 0, 0, CardGap), MinimumSize = new Size(340, 120) };
            DS.Rounded(card, DS.RadiusLg);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(223, 229, 238)))
                    e.Graphics.DrawPath(pen, DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), DS.RadiusLg));
            };
            Panel header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.White };
            header.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(240, 240, 240)), 12, header.Height - 1, header.Width - 12, header.Height - 1);
            Label lblTitle = new Label { Text = title, AutoSize = true, ForeColor = TextPrimary, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Location = new Point(12, 8) };
            header.Controls.Add(lblTitle);
            Panel body = new Panel
            {
                Name = "BodyHost",
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            card.Controls.Add(body);
            card.Controls.Add(header);
            return card;
        }

        private void ResizeFlowChildren(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed)
                return;

            int width = Math.Max(360, Math.Min(1180, flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 28));
            foreach (Control child in flow.Controls)
            {
                if (child.Dock == DockStyle.Fill)
                    continue;
                child.Width = width;
                child.Left = 0;
            }
        }

        private static Panel GetCardBody(Panel card)
        {
            return card.Controls["BodyHost"] as Panel ?? card;
        }

        private void LayoutProfilePhotoSection(Panel photoBody, Button uploadButton)
        {
            if (photoBody == null || uploadButton == null || _picPhoto == null)
                return;

            int top = 18;
            int left = 18;
            _picPhoto.SetBounds(left, top, 180, 180);

            int buttonLeft = _picPhoto.Right + 18;
            int availableWidth = photoBody.ClientSize.Width - buttonLeft - 18;
            uploadButton.SetBounds(buttonLeft, top, Math.Max(120, Math.Min(150, availableWidth)), 32);
        }

        private Label CreateMiniStat(Control parent, int x, string title)
        {
            Label lblTitle = new Label { Text = title.ToUpperInvariant(), AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8F, FontStyle.Bold), Location = new Point(x, 10) };
            Label lblValue = new Label { Text = "0", AutoSize = true, ForeColor = TextPrimary, Font = new Font("Segoe UI", 16F, FontStyle.Bold), Location = new Point(x, 26) };
            parent.Controls.Add(lblTitle);
            parent.Controls.Add(lblValue);
            return lblValue;
        }

        private TextBox AddEditor(TableLayoutPanel grid, int column, int row, string label)
        {
            Panel panel = MakeFieldPanel();
            Label lbl = MakeFieldLabel(label);
            TextBox txt = new TextBox { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.5F), BorderStyle = BorderStyle.FixedSingle, Margin = Padding.Empty };
            panel.Controls.Add(txt);
            panel.Controls.Add(lbl);
            grid.Controls.Add(panel, column, row);
            return txt;
        }

        private ComboBox AddComboEditor(TableLayoutPanel grid, int column, int row, string label)
        {
            Panel panel = MakeFieldPanel();
            Label lbl = MakeFieldLabel(label);
            ComboBox cmb = new ComboBox { Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI", 9.5F), DropDownStyle = ComboBoxStyle.DropDown, Margin = Padding.Empty };
            cmb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmb.AutoCompleteSource = AutoCompleteSource.ListItems;
            panel.Controls.Add(cmb);
            panel.Controls.Add(lbl);
            grid.Controls.Add(panel, column, row);
            return cmb;
        }

        private DateTimePicker AddDateEditor(TableLayoutPanel grid, int column, int row, string label)
        {
            Panel panel = MakeFieldPanel();
            Label lbl = MakeFieldLabel(label);
            DateTimePicker dtp = new DateTimePicker
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 9.5F),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy",
                ShowCheckBox = true,
                Margin = Padding.Empty
            };
            panel.Controls.Add(dtp);
            panel.Controls.Add(lbl);
            grid.Controls.Add(panel, column, row);
            return dtp;
        }

        private Panel MakeFieldPanel()
        {
            return new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 6, 8, 6), Padding = new Padding(0) };
        }

        private Label MakeFieldLabel(string label)
        {
            return new Label
            {
                Text = label,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 0, 0, 4)
            };
        }

        private static DateTime? GetDate(DateTimePicker picker)
        {
            return picker.Checked ? (DateTime?)picker.Value.Date : null;
        }

        private static void SetDatePicker(DateTimePicker picker, DateTime? value)
        {
            picker.Checked = value.HasValue;
            picker.Value = value ?? DateTime.Today;
        }

        private static Image ToImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return CreateDefaultAvatar();
            using (MemoryStream stream = new MemoryStream(bytes))
            using (Image image = Image.FromStream(stream))
                return new Bitmap(image);
        }

        private static Image CreateDefaultAvatar()
        {
            Bitmap avatar = new Bitmap(96, 96);
            using (Graphics g = Graphics.FromImage(avatar))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(241, 245, 249));
                using (Brush head = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    g.FillEllipse(head, 34, 22, 28, 28);
                using (Brush body = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    g.FillEllipse(body, 22, 54, 52, 36);
            }
            return avatar;
        }

        private static bool IsClosedStatus(string status)
        {
            string value = (status ?? string.Empty).Trim();
            return string.Equals(value, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Invoiced", StringComparison.OrdinalIgnoreCase);
        }

        private static decimal ParseMoney(string text)
        {
            decimal value;
            return decimal.TryParse(text, out value) ? value : 0m;
        }

        private static string GetRowString(DataRow row, string columnName)
        {
            return row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value
                ? string.Empty
                : Convert.ToString(row[columnName]);
        }

        private static DateTime? GetRowDate(DataRow row, string columnName)
        {
            if (row == null || !row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return null;

            DateTime value;
            return DateTime.TryParse(Convert.ToString(row[columnName]), out value) ? (DateTime?)value : null;
        }

        private void PopulateLeftFilters()
        {
            string clientSite = _cmbClientFilter.SelectedItem as string;
            string status = _cmbStatusFilter.SelectedItem as string;

            _cmbClientFilter.Items.Clear();
            _cmbClientFilter.Items.Add("All");
            foreach (string item in _employeeSummaries.Select(x => x.ClientSite).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
                _cmbClientFilter.Items.Add(item);
            _cmbClientFilter.SelectedItem = !string.IsNullOrWhiteSpace(clientSite) && _cmbClientFilter.Items.Contains(clientSite) ? clientSite : "All";

            _cmbStatusFilter.Items.Clear();
            _cmbStatusFilter.Items.AddRange(new object[] { "All", "Active", "Inactive", "Leave" });
            _cmbStatusFilter.SelectedItem = !string.IsNullOrWhiteSpace(status) && _cmbStatusFilter.Items.Contains(status) ? status : "All";
        }

        private void PopulateSiteOptions()
        {
            List<string> siteNames = null;
            try
            {
                siteNames = _siteService.GetAll()
                    .Select(SiteService.GetDisplayName)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.PopulateSiteOptions", ex);
            }

            PopulateSiteOptions(siteNames);
        }

        /// <summary>Populates the employee site picker from preloaded site names and employee summaries.</summary>
        private void PopulateSiteOptions(IEnumerable<string> siteNames)
        {
            string current = _cmbSite?.Text ?? string.Empty;
            _cmbSite.Items.Clear();
            foreach (string site in (siteNames ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
            {
                if (_cmbSite.Items.IndexOf(site) < 0)
                    _cmbSite.Items.Add(site);
            }

            foreach (string site in _employeeSummaries.Select(x => x.ClientSite).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
            {
                if (_cmbSite.Items.IndexOf(site) < 0)
                    _cmbSite.Items.Add(site);
            }

            _cmbSite.Text = current;
        }

        private void ApplyEmployeeFilter()
        {
            LoadEmployees();
        }

        private void ClearEmployeeFilters()
        {
            _suppressEmployeeFilterEvents = true;
            try
            {
                _txtSearch.Text = string.Empty;
                if (_cmbClientFilter.Items.Contains("All"))
                    _cmbClientFilter.SelectedItem = "All";
                if (_cmbStatusFilter.Items.Contains("All"))
                    _cmbStatusFilter.SelectedItem = "All";
            }
            finally
            {
                _suppressEmployeeFilterEvents = false;
            }
            LoadEmployees();
        }

        private static bool HasEmployeeFilters(string search, string department, string status)
        {
            return !string.IsNullOrWhiteSpace(search) ||
                   (!string.IsNullOrWhiteSpace(department) && !string.Equals(department, "All", StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase));
        }

        private void LoadSelectedEmployee()
        {
            EmployeeSummaryDto summary = _gridEmployees.CurrentRow == null ? null : _gridEmployees.CurrentRow.DataBoundItem as EmployeeSummaryDto;
            if (summary == null)
                return;

            LoadEmployeeDetails(summary.EmployeeID);
        }

        private void LoadEmployeeDetails(int employeeId)
        {
            try
            {
                _currentEmployee = _employeeService.GetById(employeeId);
                if (_currentEmployee == null)
                    return;

                _currentPhoto = _currentEmployee.Photo;
                _currentSalaryProfile = _employeeService.GetSalaryProfile(employeeId);

                BindOverview();
                BindJobs();
                RefreshAttendance();
                BindSkills();
                BindDocuments();
                BindPayroll();
                SetStatus("Loaded " + _currentEmployee.Name, TextSecondary);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadEmployeeDetails", ex);
                SetStatus("Detail load failed: " + ex.Message, Red);
            }
        }

        private void BindOverview()
        {
            _txtCode.Text = _currentEmployee.EmployeeCode ?? string.Empty;
            _txtName.Text = _currentEmployee.Name ?? string.Empty;
            _txtDesignation.Text = _currentEmployee.Designation ?? string.Empty;
            _txtDepartment.Text = _currentEmployee.Department ?? string.Empty;
            _cmbSite.Text = _currentEmployee.ClientSite ?? string.Empty;
            _txtPhone.Text = _currentEmployee.Phone ?? string.Empty;
            _txtWhatsapp.Text = _currentEmployee.WhatsAppNumber ?? string.Empty;
            _cmbBloodGroup.Text = _currentEmployee.BloodGroup ?? string.Empty;
            _txtAadhaar.Text = _currentEmployee.AadhaarNumber ?? string.Empty;
            _txtPan.Text = _currentEmployee.PANNumber ?? _currentEmployee.PAN ?? string.Empty;
            _txtEmergencyName.Text = _currentEmployee.EmergencyContactName ?? string.Empty;
            _txtEmergencyPhone.Text = _currentEmployee.EmergencyContactPhone ?? string.Empty;
            SetDatePicker(_dtpJoining, _currentEmployee.JoiningDate ?? _currentEmployee.DateOfJoining);
            SetDatePicker(_dtpProbationEnd, _currentEmployee.ProbationEndDate);
            SetDatePicker(_dtpConfirmation, _currentEmployee.ConfirmationDate);
            SetDatePicker(_dtpLastWorkingDay, _currentEmployee.LastWorkingDay);
            _cmbEmployeeStatus.Text = string.IsNullOrWhiteSpace(_currentEmployee.Status) ? "Active" : _currentEmployee.Status;
            _chkIsRehire.Checked = _currentEmployee.IsRehire;
            _picPhoto.Image = ToImage(_currentPhoto);
        }

        private void BindJobs()
        {
            if (_currentEmployee == null)
            {
                _gridJobs.DataSource = null;
                _lblJobsTotal.Text = "0";
                _lblJobsCompleted.Text = "0";
                _lblAverageClosure.Text = "0";
                return;
            }

            List<EmployeeJobSummaryDto> jobs = _employeeService.GetEmployeeJobs(_currentEmployee.EmployeeID);
            _gridJobs.DataSource = jobs;
            _lblJobsTotal.Text = jobs.Count.ToString();
            _lblJobsCompleted.Text = jobs.Count(x => IsClosedStatus(x.Status)).ToString();
            _lblAverageClosure.Text = jobs.Count == 0 ? "0" : Math.Round(jobs.Average(x => x.ClosureDays), 1).ToString("0.0");
        }

        private void LoadCurrentEmployeeTabData()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0 || _tabs == null)
                return;

            if (_tabDataEmployeeId != _currentEmployee.EmployeeID)
            {
                _tabDataEmployeeId = _currentEmployee.EmployeeID;
                _jobsLoaded = false;
                _attendanceLoaded = false;
                _skillsLoaded = false;
                _documentsLoaded = false;
                _payrollLoaded = false;
            }

            switch (_tabs.SelectedIndex)
            {
                case 1:
                    if (!_jobsLoaded)
                    {
                        BindJobs();
                        _jobsLoaded = true;
                    }
                    if (!_attendanceLoaded)
                    {
                        RefreshAttendance();
                        _attendanceLoaded = true;
                    }
                    break;
                case 2:
                    if (!_skillsLoaded)
                    {
                        BindSkills();
                        _skillsLoaded = true;
                    }
                    if (!_documentsLoaded)
                    {
                        BindDocuments();
                        _documentsLoaded = true;
                    }
                    break;
                case 3:
                    if (!_payrollLoaded)
                    {
                        LoadSalaryProfileForCurrentEmployee();
                        BindPayroll();
                        _payrollLoaded = true;
                    }
                    break;
            }
        }

        private void LoadSalaryProfileForCurrentEmployee()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
                return;

            try
            {
                _currentSalaryProfile = _employeeService.GetSalaryProfile(_currentEmployee.EmployeeID) ?? new EmployeeSalaryProfileDto { EmployeeID = _currentEmployee.EmployeeID, EffectiveFrom = DateTime.Today };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.LoadSalaryProfileForCurrentEmployee", ex);
                _currentSalaryProfile = new EmployeeSalaryProfileDto { EmployeeID = _currentEmployee.EmployeeID, EffectiveFrom = DateTime.Today };
            }
        }

        private void RefreshAttendance()
        {
            if (_currentEmployee == null)
            {
                _gridAttendance.DataSource = null;
                _lblPresentDays.Text = "0";
                _lblAbsentDays.Text = "0";
                _lblLateDays.Text = "0";
                _lblLeaveDays.Text = "0";
                return;
            }

            int month = _dtpAttendanceMonth.Value.Month;
            int year = _dtpAttendanceMonth.Value.Year;
            List<EmployeeAttendanceDayDto> attendance = _employeeService.GetEmployeeAttendance(_currentEmployee.EmployeeID, year, month);
            EmployeeAttendanceSummaryDto summary = _employeeService.GetEmployeeAttendanceSummary(_currentEmployee.EmployeeID, year, month);
            _gridAttendance.DataSource = attendance;
            _lblPresentDays.Text = summary.PresentDays.ToString();
            _lblAbsentDays.Text = summary.AbsentDays.ToString();
            _lblLateDays.Text = summary.LateDays.ToString();
            _lblLeaveDays.Text = summary.LeaveDays.ToString();
        }

        private void BindSkills()
        {
            if (_currentEmployee == null)
            {
                _gridSkills.DataSource = null;
                _lblSkillAlert.Text = "No employee selected.";
                _lblSkillAlert.ForeColor = TextHint;
                return;
            }

            List<EmployeeSkillDto> skills = _employeeService.GetEmployeeSkills(_currentEmployee.EmployeeID);
            _gridSkills.DataSource = skills;
            int expiring = skills.Count(x => x.ExpiresWithinThirtyDays);
            _lblSkillAlert.Text = expiring > 0 ? expiring + " certifications expiring within 30 days" : "All certifications are current.";
            _lblSkillAlert.ForeColor = expiring > 0 ? Red : Teal;
        }

        private void BindDocuments()
        {
            if (_currentEmployee == null)
            {
                _gridDocuments.DataSource = null;
                return;
            }

            _gridDocuments.DataSource = _employeeService.GetEmployeeDocuments(_currentEmployee.EmployeeID);
        }

        private void BindPayroll()
        {
            if (_currentSalaryProfile == null)
                _currentSalaryProfile = new EmployeeSalaryProfileDto { EffectiveFrom = DateTime.Today };

            _txtBasicSalary.Text = _currentSalaryProfile.BasicSalary.ToString("0.00");
            _txtHra.Text = _currentSalaryProfile.HRA.ToString("0.00");
            _txtAllowances.Text = _currentSalaryProfile.Allowances.ToString("0.00");
            _txtPfDeduction.Text = _currentSalaryProfile.PFDeduction.ToString("0.00");
            _txtEsicDeduction.Text = _currentSalaryProfile.ESICDeduction.ToString("0.00");
            SetDatePicker(_dtpSalaryEffectiveFrom, _currentSalaryProfile.EffectiveFrom);
            _gridAdvances.DataSource = _currentEmployee == null ? null : _payrollService.GetAdvancesByEmployee(_currentEmployee.EmployeeID);
            RecalculateSalaryLabels();
            UpdateSelectedEmployeeReadiness();
        }

        private void SaveOverview()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                SetStatus("Employee name is required.", Red);
                _tabs.SelectedIndex = 0;
                _txtName.Focus();
                return;
            }

            try
            {
                Employee employee = _currentEmployee ?? new Employee();
                employee.EmployeeCode = string.IsNullOrWhiteSpace(_txtCode.Text) ? _employeeService.GenerateNextEmployeeCode() : _txtCode.Text.Trim().ToUpperInvariant();
                employee.Name = _txtName.Text.Trim();
                employee.Designation = _txtDesignation.Text.Trim();
                employee.Department = _txtDepartment.Text.Trim();
                employee.ClientSite = _cmbSite.Text.Trim();
                employee.Phone = _txtPhone.Text.Trim();
                employee.WhatsAppNumber = _txtWhatsapp.Text.Trim();
                employee.BloodGroup = _cmbBloodGroup.Text.Trim();
                employee.AadhaarNumber = _txtAadhaar.Text.Trim();
                employee.PANNumber = _txtPan.Text.Trim().ToUpperInvariant();
                employee.EmergencyContactName = _txtEmergencyName.Text.Trim();
                employee.EmergencyContactPhone = _txtEmergencyPhone.Text.Trim();
                employee.JoiningDate = GetDate(_dtpJoining);
                employee.DateOfJoining = GetDate(_dtpJoining);
                employee.ProbationEndDate = GetDate(_dtpProbationEnd);
                employee.ConfirmationDate = GetDate(_dtpConfirmation);
                employee.LastWorkingDay = GetDate(_dtpLastWorkingDay);
                employee.Status = string.IsNullOrWhiteSpace(_cmbEmployeeStatus.Text) ? "Active" : _cmbEmployeeStatus.Text.Trim();
                employee.IsRehire = _chkIsRehire.Checked;
                employee.Photo = _currentPhoto;

                if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
                {
                    employee.EmployeeID = _employeeService.Create(employee);
                }
                else
                {
                    employee.EmployeeID = _currentEmployee.EmployeeID;
                    _employeeService.Update(employee);
                }

                _currentEmployee = _employeeService.GetById(employee.EmployeeID);
                _txtCode.Text = _currentEmployee.EmployeeCode;
                LoadData();
                SelectEmployeeRow(employee.EmployeeID);
                SetStatus("Employee saved successfully.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.SaveOverview", ex);
                SetStatus("Save failed: " + ex.Message, Red);
            }
        }

        private void SaveSalaryProfile()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                SetStatus("Save the employee profile before salary details.", Red);
                return;
            }

            try
            {
                EmployeeSalaryProfileDto profile = _currentSalaryProfile ?? new EmployeeSalaryProfileDto();
                profile.EmployeeID = _currentEmployee.EmployeeID;
                profile.BasicSalary = ParseMoney(_txtBasicSalary.Text);
                profile.HRA = ParseMoney(_txtHra.Text);
                profile.Allowances = ParseMoney(_txtAllowances.Text);
                profile.PFDeduction = ParseMoney(_txtPfDeduction.Text);
                profile.ESICDeduction = ParseMoney(_txtEsicDeduction.Text);
                profile.EffectiveFrom = GetDate(_dtpSalaryEffectiveFrom) ?? DateTime.Today;
                profile.SalaryID = _employeeService.SaveSalaryProfile(profile);
                _currentSalaryProfile = _employeeService.GetSalaryProfile(_currentEmployee.EmployeeID);
                RecalculateSalaryLabels();
                SetStatus("Salary structure saved.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.SaveSalaryProfile", ex);
                SetStatus("Salary save failed: " + ex.Message, Red);
            }
        }

        private void NewEmployee()
        {
            ShowEmployeeWorkspace();
            _currentEmployee = null;
            _currentSalaryProfile = new EmployeeSalaryProfileDto { EffectiveFrom = DateTime.Today };
            _currentPhoto = null;
            _gridEmployees.ClearSelection();
            _txtCode.Text = _employeeService.GenerateNextEmployeeCode();
            _txtName.Clear();
            _txtDesignation.Clear();
            _txtDepartment.Clear();
            _cmbSite.Text = string.Empty;
            _txtPhone.Clear();
            _txtWhatsapp.Clear();
            _cmbBloodGroup.Text = string.Empty;
            _txtAadhaar.Clear();
            _txtPan.Clear();
            _txtEmergencyName.Clear();
            _txtEmergencyPhone.Clear();
            SetDatePicker(_dtpJoining, DateTime.Today);
            SetDatePicker(_dtpProbationEnd, null);
            SetDatePicker(_dtpConfirmation, null);
            SetDatePicker(_dtpLastWorkingDay, null);
            _cmbEmployeeStatus.Text = "Active";
            _chkIsRehire.Checked = false;
            _picPhoto.Image = ToImage(null);
            _gridJobs.DataSource = null;
            _gridAttendance.DataSource = null;
            _gridSkills.DataSource = null;
            _gridDocuments.DataSource = null;
            _gridAdvances.DataSource = null;
            _lblJobsTotal.Text = "0";
            _lblJobsCompleted.Text = "0";
            _lblAverageClosure.Text = "0";
            _lblPresentDays.Text = "0";
            _lblAbsentDays.Text = "0";
            _lblLateDays.Text = "0";
            _lblLeaveDays.Text = "0";
            _lblSkillAlert.Text = "No certifications loaded.";
            BindPayroll();
            _tabs.SelectedIndex = 0;
            UpdateDashboardCurrentSelection();
            SetStatus("New employee ready.", TextSecondary);
            _txtName.Focus();
        }

        private void EmployeeSearchDebounceTimer_Tick(object sender, EventArgs e)
        {
            _employeeSearchDebounceTimer.Stop();
            if (!_suppressEmployeeFilterEvents)
                LoadEmployees();
        }

        private void QueueEmployeeSearch()
        {
            if (_suppressEmployeeFilterEvents)
                return;

            _employeeSearchDebounceTimer.Stop();
            _employeeSearchDebounceTimer.Start();
        }

        private Label CreateLeftMetric(Control parent, int x, int y, string caption)
        {
            Panel card = new Panel
            {
                Parent = parent,
                BackColor = Surface,
                Location = new Point(x, y),
                Size = new Size(108, 28)
            };

            Label valueLabel = new Label
            {
                Text = "0",
                AutoSize = false,
                Location = new Point(8, 4),
                Size = new Size(30, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label captionLabel = new Label
            {
                Text = caption,
                AutoSize = false,
                Location = new Point(36, 5),
                Size = new Size(66, 16),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            card.Controls.Add(valueLabel);
            card.Controls.Add(captionLabel);
            parent.Controls.Add(card);
            return valueLabel;
        }

        private Label BuildSummaryChip(string text, Color foreColor, Color backColor)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                ForeColor = foreColor,
                BackColor = backColor,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                Padding = new Padding(10, 5, 10, 5),
                Margin = Padding.Empty
            };
        }

        private void ApplySummaryChip(Label chip, string text, Color foreColor, Color backColor)
        {
            if (chip == null)
                return;

            chip.Text = text;
            chip.ForeColor = foreColor;
            chip.BackColor = backColor;
        }

        private void LayoutProfileHero(Panel heroBody, params Button[] actions)
        {
            if (heroBody == null || heroBody.IsDisposed)
                return;

            int chipTop = 84;
            int left = 154;
            int maxWidth = Math.Max(420, heroBody.ClientSize.Width - left - 18);
            _lblHeroEmployeeName.Width = maxWidth;
            _lblHeroEmployeeMeta.Width = maxWidth;
            _lblReadinessHeadline.Width = maxWidth;
            _lblReadinessDetail.Width = maxWidth;
            _lblProfileChecklist.Width = maxWidth;
            _lblProfileHint.Width = maxWidth;

            Label[] chips = { _lblHeroStatusChip, _lblHeroSiteChip, _lblHeroReadinessChip, _lblHeroPayrollChip, _lblHeroContactChip };
            int chipsBottom = chipTop;
            foreach (Label chip in chips)
            {
                if (chip == null)
                    continue;

                if (left + chip.Width > heroBody.ClientSize.Width - 24)
                {
                    left = 154;
                    chipTop += chip.Height + 8;
                }

                chip.Location = new Point(left, chipTop);
                left += chip.Width + 8;
                chipsBottom = Math.Max(chipsBottom, chip.Bottom);
            }

            int contentTop = chipsBottom + 14;
            _lblReadinessHeadline.Location = new Point(154, contentTop);
            _lblReadinessDetail.Location = new Point(154, _lblReadinessHeadline.Bottom + 4);
            _lblProfileChecklist.Location = new Point(154, _lblReadinessDetail.Bottom + 6);
            _lblProfileHint.Location = new Point(154, _lblProfileChecklist.Bottom + 6);

            int buttonLeft = 18;
            int buttonTop = Math.Max(_lblProfileHint.Bottom + 16, heroBody.ClientSize.Height - 74);
            foreach (Button button in actions)
            {
                if (button == null)
                    continue;

                if (buttonLeft + button.Width > heroBody.ClientSize.Width - 24)
                {
                    buttonLeft = 18;
                    buttonTop += button.Height + 8;
                }

                button.Location = new Point(buttonLeft, buttonTop);
                buttonLeft += button.Width + 10;
            }
        }

        private void UpdateLeftWorkspaceSummary(DataTable table)
        {
            if (_lblVisibleEmployees == null || _lblNeedsFollowUp == null || _lblCheckedInNow == null)
                return;

            table = table ?? new DataTable();
            int visible = table.Rows.Count;
            int needsFollowUp = 0;
            int checkedIn = 0;

            foreach (DataRow row in table.Rows)
            {
                string needsAction = GetRowString(row, "NeedsAction");
                if (!string.Equals(needsAction, "Ready", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(needsAction, "Inactive record", StringComparison.OrdinalIgnoreCase))
                    needsFollowUp++;

                int employeeId = row["EmployeeID"] == DBNull.Value ? 0 : Convert.ToInt32(row["EmployeeID"]);
                if (_checkedInTodayEmployeeIds.Contains(employeeId))
                    checkedIn++;
            }

            _lblVisibleEmployees.Text = visible.ToString();
            _lblNeedsFollowUp.Text = needsFollowUp.ToString();
            _lblCheckedInNow.Text = checkedIn.ToString();
        }

        private void DeleteCurrentEmployee()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                SetStatus("Select an employee first.", Red);
                return;
            }

            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(
                    this,
                    "Mark employee inactive",
                    _currentEmployee.Name + " will be hidden from active employee lists. Existing jobs, attendance, payroll, and audit history remain available."))
                return;

            try
            {
                _employeeService.SoftDelete(_currentEmployee.EmployeeID);
                LoadData();
                SetStatus("Employee marked inactive.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.DeleteCurrentEmployee", ex);
                SetStatus("Delete failed: " + ex.Message, Red);
            }
        }

        private void ExportEmployees()
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Excel-compatible CSV (*.csv)|*.csv";
                    dialog.FileName = "Employees_" + DateTime.Now.ToString("yyyyMMdd") + ".csv";
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;

                    EmployeeExportService.ExportEmployeeList(dialog.FileName, _employeeSummaries);
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.ExportEmployees", ex);
                SetStatus("Export failed: " + ex.Message, Red);
            }
        }

        private void OpenWhatsapp()
        {
            if (_currentEmployee == null || string.IsNullOrWhiteSpace(_currentEmployee.WhatsAppNumber))
            {
                SetStatus("No WhatsApp number saved for this employee.", Red);
                return;
            }

            string digits = new string((_currentEmployee.WhatsAppNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                SetStatus("WhatsApp number is invalid.", Red);
                return;
            }

            string text = "Hello " + (_currentEmployee.Name ?? "Team") + ", this is a ServoERP message from " + BrandingService.AppName + ".";
            Process.Start(new ProcessStartInfo("https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(text)) { UseShellExecute = true });
            SetStatus("WhatsApp opened. Review and send manually.", Teal);
        }

        private void UploadPhoto()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                _currentPhoto = File.ReadAllBytes(dialog.FileName);
                _picPhoto.Image = ToImage(_currentPhoto);
            }
        }

        private void AddSkill()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                SetStatus("Save the employee before adding skills.", Red);
                return;
            }

            using (EmployeeSkillDialog dialog = new EmployeeSkillDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    _employeeService.SaveSkill(new EmployeeSkillDto
                    {
                        EmployeeID = _currentEmployee.EmployeeID,
                        SkillName = dialog.SkillName,
                        CertificationNumber = dialog.CertificationNumber,
                        ExpiryDate = dialog.ExpiryDate
                    });
                    BindSkills();
                    LoadData();
                    SetStatus("Skill added.", Teal);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("EmployeeForm.AddSkill", ex);
                    SetStatus("Could not add skill: " + ex.Message, Red);
                }
            }
        }

        private void UploadDocument()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                SetStatus("Save the employee before uploading documents.", Red);
                return;
            }

            using (OpenFileDialog fileDialog = new OpenFileDialog())
            {
                fileDialog.Filter = "All Files|*.*";
                if (fileDialog.ShowDialog() != DialogResult.OK)
                    return;

                using (EmployeeDocumentDialog dialog = new EmployeeDocumentDialog(Path.GetFileName(fileDialog.FileName)))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    try
                    {
                        _employeeService.SaveDocument(new EmployeeDocumentDto
                        {
                            EmployeeID = _currentEmployee.EmployeeID,
                            DocumentType = dialog.DocumentType,
                            FileName = Path.GetFileName(fileDialog.FileName),
                            FileData = File.ReadAllBytes(fileDialog.FileName),
                            ExpiryDate = dialog.ExpiryDate
                        });
                        BindDocuments();
                        SetStatus("Document uploaded.", Teal);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError("EmployeeForm.UploadDocument", ex);
                        SetStatus("Document upload failed: " + ex.Message, Red);
                    }
                }
            }
        }

        private async Task GenerateSalarySlipAsync()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                SetStatus("Save the employee before generating a salary slip.", Red);
                return;
            }

            try
            {
                SaveSalaryProfile();
                string pdfPath = await EmployeeSalarySlipService.GenerateSalarySlipPdfAsync(
                    _currentEmployee,
                    _currentSalaryProfile,
                    _payrollService.GetAdvancesByEmployee(_currentEmployee.EmployeeID),
                    DateTime.Today.ToString("MMMM yyyy"));
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                SetStatus("Salary slip generated.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.GenerateSalarySlipAsync", ex);
                SetStatus("Salary slip failed: " + ex.Message, Red);
            }
        }

        private Button MakeButton(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
            button.FlatAppearance.MouseOverBackColor = backColor == Color.White ? Surface : ControlPaint.Light(backColor);
            button.FlatAppearance.MouseDownBackColor = backColor == Color.White ? Border : ControlPaint.Dark(backColor);
            DS.Rounded(button, 8);
            return button;
        }

        private static int GetEmployeeHeaderButtonMinWidth(Button button)
        {
            if (button == null)
                return 110;

            string text = (button.Text ?? string.Empty).Trim();
            if (text.IndexOf("New Employee", StringComparison.OrdinalIgnoreCase) >= 0)
                return 142;
            if (text.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0)
                return 118;
            if (text.IndexOf("Refresh", StringComparison.OrdinalIgnoreCase) >= 0)
                return 118;
            if (text.IndexOf("Import", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Export", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Filters", StringComparison.OrdinalIgnoreCase) >= 0)
                return 112;

            return 104;
        }

        private static void ApplyEmployeeHeaderButtonSpacing(Button button)
        {
            if (button == null)
                return;

            button.AutoEllipsis = true;
            button.Padding = new Padding(12, 0, 12, 0);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
        }

        /// <summary>Positions the Employee dashboard header so action buttons never force the page header to balloon vertically.</summary>
        private void LayoutEmployeeHeader(Panel header, Panel titleStack, Panel buttonRail, Button[] headerButtons)
        {
            if (header == null || titleStack == null || buttonRail == null || headerButtons == null)
                return;

            const int outerPad = 24;
            int gap = SharedUiPrimitives.HeaderActionGap;
            bool compact = header.ClientSize.Width > 0 && header.ClientSize.Width < 1380;
            int targetHeaderHeight = compact ? 118 : 92;
            if (header.Height != targetHeaderHeight)
                header.Height = targetHeaderHeight;

            int railWidth = compact
                ? Math.Min(560, Math.Max(360, header.ClientSize.Width - outerPad * 2))
                : Math.Min(980, Math.Max(560, header.ClientSize.Width - 520));
            int railHeight = compact ? 76 : 38;
            buttonRail.SetBounds(
                Math.Max(outerPad, header.ClientSize.Width - outerPad - railWidth),
                compact ? 14 : 16,
                railWidth,
                railHeight);

            int titleRight = compact ? header.ClientSize.Width - outerPad : buttonRail.Left - 18;
            titleStack.SetBounds(outerPad, 12, Math.Max(320, titleRight - outerPad), compact ? 92 : 70);
            foreach (Control child in titleStack.Controls)
                child.Width = Math.Max(120, titleStack.ClientSize.Width - child.Left);

            for (int i = 0; i < headerButtons.Length; i++)
            {
                Button button = headerButtons[i];
                if (button == null)
                    continue;

                button.Width = button == _btnNew ? 126 : 110;
                button.Height = 34;
                button.MinimumSize = new Size(button.Width, button.Height);
            }

            if (compact)
            {
                LayoutHeaderButtonRow(headerButtons, 0, 4, 0, gap);
                LayoutHeaderButtonRow(headerButtons, 4, headerButtons.Length - 4, 40, gap);
            }
            else
            {
                int x = 0;
                foreach (Button button in headerButtons)
                {
                    if (button == null || !button.Visible)
                        continue;

                    button.SetBounds(x, 0, button.Width, 34);
                    x += button.Width + gap;
                }
            }

            buttonRail.Visible = true;
            buttonRail.BringToFront();
        }

        /// <summary>Places a contiguous row of Employee header buttons inside the fixed header rail.</summary>
        private void LayoutHeaderButtonRow(Button[] buttons, int startIndex, int count, int top, int gap)
        {
            if (buttons == null || startIndex >= buttons.Length || count <= 0)
                return;

            int x = 0;
            int end = Math.Min(buttons.Length, startIndex + count);
            for (int i = startIndex; i < end; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.Visible)
                    continue;

                button.SetBounds(x, top, button.Width, 34);
                x += button.Width + gap;
            }
        }

        private Label AddKpiCard(TableLayoutPanel table, int column, string title, Color valueColor)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Margin = new Padding(column == 0 ? 0 : 10, 0, 0, 0), Padding = new Padding(14, 12, 14, 12) };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(card, 8);
            Label lblTitle = new Label { Text = title.ToUpperInvariant(), ForeColor = TextSecondary, Font = new Font("Segoe UI", 8F, FontStyle.Bold), AutoSize = true, Location = new Point(14, 12) };
            Label lblValue = new Label { Text = "0", ForeColor = valueColor, Font = new Font("Segoe UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(14, 30) };
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblValue);
            table.Controls.Add(card, column, 0);
            return lblValue;
        }

        private Label AddDashboardHeroKpiCard(TableLayoutPanel table, int column, string title, string subtitle, Color backColor, Color foreColor, Color accentColor)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = backColor,
                Margin = new Padding(column == 0 ? 0 : 12, 0, 0, 0),
                Padding = new Padding(14, 12, 14, 10)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (Brush fill = new SolidBrush(backColor))
                using (Pen pen = new Pen(Color.FromArgb(80, 0, 0, 0)))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }

                using (Pen accentPen = new Pen(accentColor, 2.4f))
                    e.Graphics.DrawLine(accentPen, 14, card.Height - 12, 58, card.Height - 12);
            };
            DS.Rounded(card, 8);

            Label lblTitle = new Label
            {
                Text = title.ToUpperInvariant(),
                ForeColor = Color.FromArgb(235, foreColor),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 10)
            };
            Label lblValue = new Label
            {
                Text = "0",
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 30)
            };
            Label lblSubtitle = new Label
            {
                Text = subtitle,
                ForeColor = Color.FromArgb(210, foreColor),
                Font = new Font("Segoe UI", 7.6F),
                AutoSize = true,
                Location = new Point(14, 56)
            };
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblValue);
            card.Controls.Add(lblSubtitle);
            table.Controls.Add(card, column, 0);
            return lblValue;
        }

        private Panel CreateDeploymentCoverageSummaryPanel()
        {
            Panel card = new Panel
            {
                BackColor = Color.FromArgb(249, 251, 254),
                Location = new Point(18, 78),
                Size = new Size(286, 126)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12))
                using (Brush fill = new SolidBrush(Color.FromArgb(249, 251, 254)))
                using (Pen pen = new Pen(Color.FromArgb(225, 230, 238)))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }
                using (Pen divider = new Pen(Color.FromArgb(229, 234, 242)))
                    e.Graphics.DrawLine(divider, 128, 18, 128, card.Height - 18);
            };
            DS.Rounded(card, 12);

            Label summaryTitle = new Label
            {
                Text = "COVERAGE RATE",
                Location = new Point(16, 18),
                Size = new Size(96, 16),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = TextSecondary
            };
            _lblDashboardCoverageRate = new Label
            {
                Text = "0%",
                Location = new Point(16, 34),
                Size = new Size(96, 34),
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Blue
            };
            Label summaryHint = new Label
            {
                Text = "Visible roster with site assignment",
                Location = new Point(16, 74),
                Size = new Size(96, 32),
                Font = new Font("Segoe UI", 8F),
                ForeColor = TextSecondary
            };
            _lblDashboardCoverageAssignedMeta = new Label
            {
                Text = "Assigned: 0",
                Location = new Point(146, 22),
                Size = new Size(120, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            _lblDashboardCoverageUnassignedMeta = new Label
            {
                Text = "Unassigned: 0",
                Location = new Point(146, 50),
                Size = new Size(120, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Red
            };
            _lblDashboardCoverageTopSiteMeta = new Label
            {
                Text = "Top site: Waiting for roster",
                Location = new Point(146, 80),
                Size = new Size(124, 32),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Teal
            };
            card.Controls.Add(summaryTitle);
            card.Controls.Add(_lblDashboardCoverageRate);
            card.Controls.Add(summaryHint);
            card.Controls.Add(_lblDashboardCoverageAssignedMeta);
            card.Controls.Add(_lblDashboardCoverageUnassignedMeta);
            card.Controls.Add(_lblDashboardCoverageTopSiteMeta);
            return card;
        }

        private Panel CreateDeploymentCoveragePanel()
        {
            Panel card = new Panel
            {
                BackColor = Color.FromArgb(248, 250, 253),
                Size = new Size(340, 176)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12))
                using (Brush fill = new SolidBrush(Color.FromArgb(248, 250, 253)))
                using (Pen pen = new Pen(Color.FromArgb(221, 227, 236)))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            DS.Rounded(card, 12);

            Label title = new Label
            {
                Text = "Site load",
                Location = new Point(16, 14),
                Size = new Size(140, 18),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            Label subtitle = new Label
            {
                Text = "Busiest visible sites by roster load.",
                Location = new Point(16, 34),
                Size = new Size(300, 28),
                Font = new Font("Segoe UI", 8F),
                ForeColor = TextSecondary
            };
            _dashboardCoverageList = new FlowLayoutPanel
            {
                Location = new Point(16, 72),
                Size = new Size(308, 92),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent
            };
            card.Controls.Add(title);
            card.Controls.Add(subtitle);
            card.Controls.Add(_dashboardCoverageList);
            return card;
        }

        private Panel CreateCoverageBarRow(string siteName, int count, int maxCount, Color accentColor)
        {
            Panel row = new Panel
            {
                Size = new Size(306, 22),
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.Transparent
            };
            Label lblSite = new Label
            {
                Text = siteName,
                Location = new Point(0, 0),
                Size = new Size(152, 16),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            Label lblCount = new Label
            {
                Text = count.ToString(),
                Location = new Point(254, 0),
                Size = new Size(52, 16),
                TextAlign = ContentAlignment.TopRight,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = TextSecondary
            };
            Panel track = new Panel
            {
                Location = new Point(0, 18),
                Size = new Size(306, 4),
                BackColor = Color.FromArgb(231, 236, 244)
            };
            int fillWidth = maxCount <= 0 ? 0 : Math.Max(22, (int)Math.Round((count / (double)maxCount) * track.Width));
            Panel fill = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(Math.Min(track.Width, fillWidth), 4),
                BackColor = accentColor
            };
            track.Controls.Add(fill);
            row.Controls.Add(lblSite);
            row.Controls.Add(lblCount);
            row.Controls.Add(track);
            return row;
        }

        private Label AddDashboardRibbonCard(TableLayoutPanel table, int column, string title, Color valueColor, ModernIconKind iconKind)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(column == 0 ? 0 : 10, 0, 0, 0),
                Padding = new Padding(12, 10, 12, 10)
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawPath(pen, path);
                using (Pen accentPen = new Pen(Color.FromArgb(190, valueColor), 2.2f))
                    e.Graphics.DrawLine(accentPen, 12, card.Height - 10, 70, card.Height - 10);
            };
            DS.Rounded(card, 8);

            Label lblTitle = new Label
            {
                Text = title.ToUpperInvariant(),
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(12, 12)
            };
            Label lblValue = new Label
            {
                Text = "0",
                ForeColor = valueColor,
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(12, 32)
            };
            Label badge = ModernIconSystem.Badge(iconKind, 14, Color.FromArgb(32, valueColor), valueColor, 8);
            badge.Location = new Point(230, 12);
            badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            card.Resize += (s, e) => badge.Left = Math.Max(180, card.ClientSize.Width - badge.Width - 12);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblValue);
            card.Controls.Add(badge);
            table.Controls.Add(card, column, 0);
            return lblValue;
        }

        private Control CreateLegendPill(string text, Color color)
        {
            Panel pill = new Panel
            {
                Size = new Size(42, 14),
                Margin = new Padding(0, 0, 6, 6),
                BackColor = Color.White
            };
            pill.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush fill = new SolidBrush(Color.FromArgb(28, color)))
                    e.Graphics.FillRectangle(fill, 0, 0, 42, 14);
                using (Brush dotBrush = new SolidBrush(color))
                    e.Graphics.FillEllipse(dotBrush, 4, 4, 6, 6);
            };
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(14, 0),
                Size = new Size(24, 14),
                Font = new Font("Segoe UI", 7.2F, FontStyle.Bold),
                ForeColor = TextPrimary
            };
            pill.Controls.Add(lbl);
            return pill;
        }

        private sealed class EmployeeInitialPayload
        {
            public EmployeeDashboardStats Stats { get; set; } = new EmployeeDashboardStats();
            public List<EmployeeSkillDto> ExpiringSkills { get; set; } = new List<EmployeeSkillDto>();
            public DataTable EmployeeTable { get; set; } = new DataTable();
            public HashSet<int> CheckedInTodayEmployeeIds { get; set; } = new HashSet<int>();
            public List<string> SiteNames { get; set; } = new List<string>();
            public string AttendanceReconciliationBanner { get; set; }
        }

        private sealed class DashboardRosterRow
        {
            public int RowNumber { get; set; }
            public int EmployeeID { get; set; }
            public string Name { get; set; }
            public string Designation { get; set; }
            public string ClientSite { get; set; }
            public string PresenceState { get; set; }
            public string ReadinessState { get; set; }
            public string Mobile { get; set; }
            public string Email { get; set; }
            public string Actions { get; set; }
        }

        private sealed class EmployeeSkillDialog : ServoERP.Infrastructure.ServoFormBase
        {
            private readonly TextBox _txtSkill;
            private readonly TextBox _txtCertification;
            private readonly DateTimePicker _dtpExpiry;

            public string SkillName => _txtSkill.Text.Trim();
            public string CertificationNumber => _txtCertification.Text.Trim();
            public DateTime? ExpiryDate => _dtpExpiry.Checked ? (DateTime?)_dtpExpiry.Value.Date : null;

            public EmployeeSkillDialog()
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Add Skill";
                ClientSize = new Size(420, 250);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;

                Label lblSkill = DialogLabel("Skill Name *", 18);
                _txtSkill = new TextBox { Left = 18, Top = 38, Width = 360, TabIndex = 0 };
                Label lblCert = DialogLabel("Certification Number", 74);
                _txtCertification = new TextBox { Left = 18, Top = 94, Width = 360, TabIndex = 1 };
                Label lblExpiry = DialogLabel("Expiry Date", 130);
                _dtpExpiry = new DateTimePicker { Left = 18, Top = 150, Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true, TabIndex = 2 };
                Button btnSave = new Button { Text = "Save", Left = 220, Top = 198, Width = 74, DialogResult = DialogResult.OK, TabIndex = 3 };
                Button btnCancel = new Button { Text = "Cancel", Left = 304, Top = 198, Width = 74, DialogResult = DialogResult.Cancel, TabIndex = 4 };
                AcceptButton = btnSave;
                CancelButton = btnCancel;
                Controls.AddRange(new Control[] { lblSkill, _txtSkill, lblCert, _txtCertification, lblExpiry, _dtpExpiry, btnSave, btnCancel });
            }
        }

        private sealed class EmployeeDocumentDialog : ServoERP.Infrastructure.ServoFormBase
        {
            private readonly TextBox _txtType;
            private readonly DateTimePicker _dtpExpiry;

            public string DocumentType => _txtType.Text.Trim();
            public DateTime? ExpiryDate => _dtpExpiry.Checked ? (DateTime?)_dtpExpiry.Value.Date : null;

            public EmployeeDocumentDialog(string fileName)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Upload Document";
                ClientSize = new Size(420, 246);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;

                Label lblFile = DialogLabel("File", 18);
                Label lblFileValue = new Label { Text = fileName, Left = 18, Top = 38, Width = 360, Height = 20, AutoSize = false, AutoEllipsis = true };
                Label lblType = DialogLabel("Document Type *", 74);
                _txtType = new TextBox { Left = 18, Top = 94, Width = 360, TabIndex = 0 };
                Label lblExpiry = DialogLabel("Expiry Date", 130);
                _dtpExpiry = new DateTimePicker { Left = 18, Top = 150, Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true, TabIndex = 1 };
                Button btnSave = new Button { Text = "Upload", Left = 220, Top = 196, Width = 74, DialogResult = DialogResult.OK, TabIndex = 2 };
                Button btnCancel = new Button { Text = "Cancel", Left = 304, Top = 196, Width = 74, DialogResult = DialogResult.Cancel, TabIndex = 3 };
                AcceptButton = btnSave;
                CancelButton = btnCancel;
                Controls.AddRange(new Control[] { lblFile, lblFileValue, lblType, _txtType, lblExpiry, _dtpExpiry, btnSave, btnCancel });
            }
        }

        private static Label DialogLabel(string text, int top)
        {
            return new Label
            {
                Text = text,
                Left = 18,
                Top = top,
                Width = 360,
                Height = 18,
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = text.Contains("*") ? DS.Primary600 : Color.FromArgb(80, 80, 80)
            };
        }
    }
}


