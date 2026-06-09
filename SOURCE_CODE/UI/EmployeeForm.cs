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

        private List<EmployeeSummaryDto> _employeeSummaries = new List<EmployeeSummaryDto>();
        private List<EmployeeSkillDto> _expiringSkills = new List<EmployeeSkillDto>();
        private HashSet<int> _checkedInTodayEmployeeIds = new HashSet<int>();
        private Employee _currentEmployee;
        private EmployeeSalaryProfileDto _currentSalaryProfile;
        private byte[] _currentPhoto;
        private bool _suppressEmployeeFilterEvents;
        private int _tabDataEmployeeId;
        private bool _jobsLoaded;
        private bool _attendanceLoaded;
        private bool _skillsLoaded;
        private bool _documentsLoaded;
        private bool _payrollLoaded;
        private bool _initialLoadInProgress;

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
                BeginInitialLoad();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("EmployeeForm.FormLoadSafe", ex);
                SetStatus("Employee form failed to load: " + ex.Message, Red);
            }
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
            try { payload.ExpiringSkills = _employeeService.GetExpiringSkills(30) ?? new List<EmployeeSkillDto>(); }
            catch (Exception ex) { AppLogger.LogError("EmployeeForm.InitialLoad.ExpiringSkills", ex); }

            try { payload.Stats = _employeeService.GetDashboardStats() ?? new EmployeeDashboardStats(); }
            catch (Exception ex) { AppLogger.LogError("EmployeeForm.InitialLoad.Stats", ex); }

            payload.EmployeeTable = LoadEmployeeTable(string.Empty, "All", "All");
            payload.CheckedInTodayEmployeeIds = LoadCheckedInEmployeesTodaySet();
            try
            {
                payload.SiteNames = _siteService.GetAll()
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
                _gridEmployees.Rows[0].Selected = true;

            SetStatus("Employee module loaded.", TextSecondary);
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = PageBg, Padding = Padding.Empty };
            Panel titleStack = new Panel { BackColor = Color.Transparent, MinimumSize = new Size(320, 64) };
            Label titleLabel = new Label
            {
                Text = "Employee Operations",
                Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 0),
                Size = new Size(420, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Label subtitleLabel = new Label
            {
                Text = "Employees > Workforce profile > Payroll readiness",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = TextSecondary,
                Location = new Point(1, 38),
                Size = new Size(420, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            titleStack.Controls.Add(titleLabel);
            titleStack.Controls.Add(subtitleLabel);
            _btnNew = MakeButton("New Employee", Teal, Color.White, 118);
            _btnSave = MakeButton("Save", Teal, Color.White, 76);
            _btnDelete = MakeButton("Delete", Color.White, Red, 76);
            _btnExport = MakeButton("Export", Color.White, TextPrimary, 76);
            _btnImport = MakeButton("Import", Color.White, TextPrimary, 76);
            _btnTemplate = MakeButton("Template", Color.White, TextPrimary, 86);
            Button btnForms = MakeButton("Forms", Color.White, Blue, 76);
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            _btnWhatsapp = MakeButton("WhatsApp", Color.White, Blue, 92);

            _lblStatus = new Label
            {
                AutoSize = false,
                Location = new Point(1, 59),
                Size = new Size(420, 18),
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Panel buttonRail = new Panel
            {
                Name = "EmployeeHeaderButtonRail",
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Button[] headerButtons = { _btnDelete, _btnImport, btnForms, _btnTemplate, _btnExport, _btnWhatsapp, _btnNew, _btnSave };
            foreach (Button button in headerButtons)
            {
                button.AutoSize = false;
                button.Height = 34;
                button.Width = button == _btnNew ? 126 : 110;
                button.MinimumSize = new Size(button.Width, button.Height);
                button.Margin = Padding.Empty;
                button.Tag = ((button.Tag == null ? string.Empty : button.Tag + " ") + "FIXED_WIDTH").Trim();
            }
            buttonRail.Controls.AddRange(headerButtons);
            titleStack.Controls.Add(_lblStatus);
            header.Controls.Add(titleStack);
            header.Controls.Add(buttonRail);
            header.Resize += (s, e) => LayoutEmployeeHeader(header, titleStack, buttonRail, headerButtons);
            header.Layout += (s, e) => LayoutEmployeeHeader(header, titleStack, buttonRail, headerButtons);
            LayoutEmployeeHeader(header, titleStack, buttonRail, headerButtons);

            _lnkExpiringBanner = new LinkLabel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = AmberLight,
                LinkColor = Color.FromArgb(99, 56, 6),
                ActiveLinkColor = Color.FromArgb(99, 56, 6),
                VisitedLinkColor = Color.FromArgb(99, 56, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Visible = false
            };
            _lnkExpiringBanner.LinkClicked += (s, e) => ShowExpiringSkillsReview();

            Panel kpiStrip = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = PageBg, Padding = new Padding(20, 12, 20, 0) };
            TableLayoutPanel kpiTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++)
                kpiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _lblTotalEmployees = AddKpiCard(kpiTable, 0, "Total Employees", Blue);
            _lblActiveToday = AddKpiCard(kpiTable, 1, "Active Today", Teal);
            _lblOnDuty = AddKpiCard(kpiTable, 2, "On Duty", Amber);
            _lblOnLeave = AddKpiCard(kpiTable, 3, "On Leave", Red);
            kpiStrip.Controls.Add(kpiTable);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = Border,
                FixedPanel = FixedPanel.Panel1,
                Panel1MinSize = 300
            };
            split.HandleCreated += (s, e) => ApplyEmployeeSplitDistance(split);
            split.Resize += (s, e) => ApplyEmployeeSplitDistance(split);
            split.Panel1.BackColor = Color.White;
            split.Panel2.BackColor = PageBg;
            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            Controls.Add(split);
            Controls.Add(kpiStrip);
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
        }

        private void BuildLeftPanel(Control parent)
        {
            parent.Controls.Clear();

            Panel top = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Color.White, Padding = new Padding(14, 14, 14, 10) };
            _txtSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F), Width = 228 };
            Button clearFilters = MakeButton("Clear", Color.White, Blue, 80);
            clearFilters.FlatAppearance.BorderColor = Border;
            clearFilters.FlatAppearance.BorderSize = 1;
            _cmbClientFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Font = new Font("Segoe UI", 9F) };
            _cmbStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Font = new Font("Segoe UI", 9F) };

            Label lblSearch = new Label { Text = "Search employees", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(14, 6) };
            _txtSearch.Location = new Point(14, 24);
            clearFilters.Location = new Point(252, 23);
            Label lblClient = new Label { Text = "Client / Site", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(14, 58) };
            _cmbClientFilter.Location = new Point(14, 76);
            Label lblStatusFilter = new Label { Text = "Status", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(178, 58) };
            _cmbStatusFilter.Location = new Point(178, 76);
            top.Controls.AddRange(new Control[] { lblSearch, _txtSearch, clearFilters, lblClient, _cmbClientFilter, lblStatusFilter, _cmbStatusFilter });

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
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Code", DataPropertyName = "EmployeeCode", Width = 80 });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "EmployeeName", Width = 180 });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Designation", DataPropertyName = "Designation", Width = 130, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            _gridEmployees.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Client / Site", DataPropertyName = "ClientSite", Width = 160 });
            StyleDataGrid(_gridEmployees);

            parent.Controls.Add(_gridEmployees);
            parent.Controls.Add(top);

            _txtSearch.TextChanged += (s, e) => { if (!_suppressEmployeeFilterEvents) LoadEmployees(); };
            _cmbClientFilter.SelectedIndexChanged += (s, e) => { if (!_suppressEmployeeFilterEvents) LoadEmployees(); };
            _cmbStatusFilter.SelectedIndexChanged += (s, e) => { if (!_suppressEmployeeFilterEvents) LoadEmployees(); };
            clearFilters.Click += (s, e) => ClearEmployeeFilters();
            _gridEmployees.SelectionChanged += (s, e) => LoadSelectedEmployeeSafe();
            _gridEmployees.CellFormatting += GridEmployees_CellFormattingSafe;
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
                catch { }
            }
        }

        private void BuildRightPanel(Control parent)
        {
            parent.Controls.Clear();
            Panel wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 16), AutoScroll = false, BackColor = PageBg };
            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };
            _tabs.TabPages.Add(BuildOverviewTab());
            _tabs.TabPages.Add(BuildJobsTab());
            _tabs.TabPages.Add(BuildAttendanceTab());
            _tabs.TabPages.Add(BuildSkillsTab());
            _tabs.TabPages.Add(BuildDocumentsTab());
            _tabs.TabPages.Add(BuildPayrollTab());
            _tabs.SelectedIndexChanged += (s, e) => LoadCurrentEmployeeTabData();
            wrap.Controls.Add(_tabs);
            parent.Controls.Add(wrap);
        }

        private TabPage BuildOverviewTab()
        {
            TabPage page = new TabPage("Overview") { BackColor = PageBg };
            Panel content = MakeTabScrollHost();
            FlowLayoutPanel flow = MakeVerticalFlow();

            Panel photoCard = MakeCard("Profile photo");
            _picPhoto = new PictureBox
            {
                Width = 180,
                Height = 180,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                BackColor = Surface,
                Location = new Point(18, 18)
            };
            Button btnUploadPhoto = MakeButton("Upload Photo", Color.White, Blue, 120);
            btnUploadPhoto.Click += (s, e) => UploadPhoto();
            _picPhoto.Click += (s, e) => UploadPhoto();
            photoCard.Height = 238;
            Panel photoBody = GetCardBody(photoCard);
            photoBody.Controls.Add(_picPhoto);
            photoBody.Controls.Add(btnUploadPhoto);
            photoBody.Resize += (s, e) => LayoutProfilePhotoSection(photoBody, btnUploadPhoto);
            LayoutProfilePhotoSection(photoBody, btnUploadPhoto);
            flow.Controls.Add(photoCard);

            Panel fieldsCard = MakeCard("Employee profile");
            fieldsCard.Height = 840;
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(18, 18, 18, 18), Height = 760 };
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
            _cmbBloodGroup.Items.AddRange(new object[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" });
            _txtAadhaar = AddEditor(grid, 0, 4, "Aadhaar");
            _txtPan = AddEditor(grid, 1, 4, "PAN");
            _txtEmergencyName = AddEditor(grid, 0, 5, "Emergency contact name");
            _txtEmergencyPhone = AddEditor(grid, 1, 5, "Emergency contact phone");
            _dtpJoining = AddDateEditor(grid, 0, 6, "Joining date");
            _dtpProbationEnd = AddDateEditor(grid, 1, 6, "Probation end");
            _dtpConfirmation = AddDateEditor(grid, 0, 7, "Confirmation date");
            _dtpLastWorkingDay = AddDateEditor(grid, 1, 7, "Last working day");
            _cmbEmployeeStatus = AddComboEditor(grid, 0, 8, "Status");
            _cmbEmployeeStatus.Items.AddRange(new object[] { "Active", "Inactive", "Leave" });
            Panel rehirePanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8) };
            Label lblRehire = new Label { Text = "Rehire", AutoSize = true, ForeColor = TextSecondary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(0, 0) };
            _chkIsRehire = new CheckBox { Text = "Employee is a rehire", AutoSize = true, Location = new Point(0, 28), Font = new Font("Segoe UI", 9F), ForeColor = TextPrimary };
            rehirePanel.Controls.Add(lblRehire);
            rehirePanel.Controls.Add(_chkIsRehire);
            grid.Controls.Add(rehirePanel, 1, 8);
            GetCardBody(fieldsCard).Controls.Add(grid);
            flow.Controls.Add(fieldsCard);

            content.Controls.Add(flow);
            page.Controls.Add(content);
            return page;
        }

        private TabPage BuildJobsTab()
        {
            TabPage page = new TabPage("Jobs") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            Panel card = MakeCard("Assigned jobs");
            card.Dock = DockStyle.Fill;

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

            Panel body = GetCardBody(card);
            body.Controls.Add(_gridJobs);
            body.Controls.Add(stats);
            host.Controls.Add(card);
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
            TabPage page = new TabPage("Skills & Certifications") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            Panel card = MakeCard("Skills & certifications");
            card.Dock = DockStyle.Fill;

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

            Panel body = GetCardBody(card);
            body.Controls.Add(_gridSkills);
            body.Controls.Add(top);
            host.Controls.Add(card);
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
            TabPage page = new TabPage("Payroll") { BackColor = PageBg };
            Panel host = MakeTabScrollHost();
            FlowLayoutPanel flow = MakeVerticalFlow();

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
                    _gridEmployees.Rows[0].Selected = true;

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
                @"SELECT COUNT(*) FROM dbo.EmployeeAttendance
                  WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                    AND CheckInTime IS NOT NULL;",
                attendanceSafe: true).ToString();

            _lblOnLeave.Text = ExecuteScalarIntSafe(
                "EmployeeForm.LoadKpis.OnLeave",
                @"SELECT COUNT(*) FROM dbo.EmployeeAttendance
                  WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                    AND Status = 'Leave';",
                attendanceSafe: true).ToString();
        }

        private int ExecuteScalarIntSafe(string operation, string sql, bool attendanceSafe = false)
        {
            try
            {
                if (attendanceSafe && !EmployeeAttendanceTableExists())
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

        private bool EmployeeAttendanceTableExists()
        {
            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = 'EmployeeAttendance';", conn))
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
                        SELECT EmployeeID, EmployeeCode, Name AS EmployeeName, Designation, Department, ClientSite, Status
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
                    Status = row["Status"] == DBNull.Value ? string.Empty : Convert.ToString(row["Status"])
                });
            }

            _gridEmployees.DataSource = table;
            if (table.Rows.Count == 0 && HasEmployeeFilters(search, clientSite, status))
                SetStatus("No employees match current filters. Clear filters to show all employees.", Amber);
        }

        private void LoadCheckedInEmployeesToday()
        {
            _checkedInTodayEmployeeIds = LoadCheckedInEmployeesTodaySet();
        }

        /// <summary>Returns employees checked in today without touching UI state.</summary>
        private HashSet<int> LoadCheckedInEmployeesTodaySet()
        {
            var ids = new HashSet<int>();
            if (!EmployeeAttendanceTableExists())
                return ids;

            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT DISTINCT EmployeeID
                        FROM dbo.EmployeeAttendance
                        WHERE AttendanceDate = CAST(GETDATE() AS DATE)
                          AND CheckInTime IS NOT NULL;", conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            ids.Add(reader["EmployeeID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EmployeeID"]));
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

            if (_gridEmployees.Columns[e.ColumnIndex].HeaderText == "Designation")
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
        }

        private async Task SaveCurrentTabAsync()
        {
            if (_tabs.SelectedTab != null && _tabs.SelectedTab.Text == "Payroll")
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

            _tabs.SelectedIndex = 3;
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
            Panel card = new Panel { Width = 760, BackColor = Color.White, Margin = new Padding(0, 0, 0, 14), MinimumSize = new Size(340, 140) };
            card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, card.Width - 1, card.Height - 1);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.White };
            header.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(240, 240, 240)), 0, header.Height - 1, header.Width, header.Height - 1);
            Label lblTitle = new Label { Text = title, AutoSize = true, ForeColor = TextPrimary, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Location = new Point(16, 9) };
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

            int width = Math.Max(360, Math.Min(1180, flow.ClientSize.Width - flow.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 80));
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
            return new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 8, 10, 8), Padding = new Padding(0, 0, 0, 0) };
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
                    break;
                case 2:
                    if (!_attendanceLoaded)
                    {
                        RefreshAttendance();
                        _attendanceLoaded = true;
                    }
                    break;
                case 3:
                    if (!_skillsLoaded)
                    {
                        BindSkills();
                        _skillsLoaded = true;
                    }
                    break;
                case 4:
                    if (!_documentsLoaded)
                    {
                        BindDocuments();
                        _documentsLoaded = true;
                    }
                    break;
                case 5:
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
            SetStatus("New employee ready.", TextSecondary);
            _txtName.Focus();
        }

        private void DeleteCurrentEmployee()
        {
            if (_currentEmployee == null || _currentEmployee.EmployeeID <= 0)
            {
                SetStatus("Select an employee first.", Red);
                return;
            }

            if (MessageBox.Show("Mark " + _currentEmployee.Name + " as inactive?", "Employees", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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

        /// <summary>Positions the Employee dashboard header so action buttons never force the page header to balloon vertically.</summary>
        private void LayoutEmployeeHeader(Panel header, Panel titleStack, Panel buttonRail, Button[] headerButtons)
        {
            if (header == null || titleStack == null || buttonRail == null || headerButtons == null)
                return;

            const int outerPad = 24;
            const int gap = 8;
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

        private sealed class EmployeeInitialPayload
        {
            public EmployeeDashboardStats Stats { get; set; } = new EmployeeDashboardStats();
            public List<EmployeeSkillDto> ExpiringSkills { get; set; } = new List<EmployeeSkillDto>();
            public DataTable EmployeeTable { get; set; } = new DataTable();
            public HashSet<int> CheckedInTodayEmployeeIds { get; set; } = new HashSet<int>();
            public List<string> SiteNames { get; set; } = new List<string>();
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
                Width = 420;
                Height = 240;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;

                Label lblSkill = new Label { Text = "Skill Name", Left = 18, Top = 18, AutoSize = true };
                _txtSkill = new TextBox { Left = 18, Top = 38, Width = 360 };
                Label lblCert = new Label { Text = "Certification Number", Left = 18, Top = 74, AutoSize = true };
                _txtCertification = new TextBox { Left = 18, Top = 94, Width = 360 };
                Label lblExpiry = new Label { Text = "Expiry Date", Left = 18, Top = 130, AutoSize = true };
                _dtpExpiry = new DateTimePicker { Left = 18, Top = 150, Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true };
                Button btnSave = new Button { Text = "Save", Left = 220, Top = 178, Width = 74, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Cancel", Left = 304, Top = 178, Width = 74, DialogResult = DialogResult.Cancel };
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
                Width = 420;
                Height = 220;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;

                Label lblFile = new Label { Text = "File", Left = 18, Top = 18, AutoSize = true };
                Label lblFileValue = new Label { Text = fileName, Left = 18, Top = 38, AutoSize = true, Width = 360 };
                Label lblType = new Label { Text = "Document Type", Left = 18, Top = 72, AutoSize = true };
                _txtType = new TextBox { Left = 18, Top = 92, Width = 360 };
                Label lblExpiry = new Label { Text = "Expiry Date", Left = 18, Top = 126, AutoSize = true };
                _dtpExpiry = new DateTimePicker { Left = 18, Top = 146, Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true };
                Button btnSave = new Button { Text = "Upload", Left = 220, Top = 144, Width = 74, DialogResult = DialogResult.OK };
                Button btnCancel = new Button { Text = "Cancel", Left = 304, Top = 144, Width = 74, DialogResult = DialogResult.Cancel };
                AcceptButton = btnSave;
                CancelButton = btnCancel;
                Controls.AddRange(new Control[] { lblFile, lblFileValue, lblType, _txtType, lblExpiry, _dtpExpiry, btnSave, btnCancel });
            }
        }
    }
}


