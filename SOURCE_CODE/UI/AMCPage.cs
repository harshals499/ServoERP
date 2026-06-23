using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Dashboard page for AMC contracts with KPI cards, filters, and add/edit workflow.</summary>
    public partial class AMCPage : DeferredPageControl
    {
        private readonly CultureInfo _india = new CultureInfo("en-IN");
        private readonly List<AMCRow> _allRows = new List<AMCRow>();
        private FlowLayoutPanel _kpiFlow;
        private DataGridView _contractGrid;
        private Label _emptyLabel;
        private Label _totalValue;
        private Label _activeValue;
        private Label _expiringValue;
        private Label _expiredValue;
        private TextBox _searchBox;
        private ComboBox _statusFilter;
        private ComboBox _typeFilter;
        private ComboBox _renewalFilter;
        private Label _listCaption;
        private Button _btnAddAMC;
        private Button _btnImportAMC;
        private bool _loadInProgress;
        private bool _addAmcDialogOpen;
        private Control _dashboardShell;

        private static readonly Color PageBg = Color.FromArgb(246, 248, 252);
        private static readonly Color Ink = Color.FromArgb(15, 23, 42);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(16, 185, 129);
        private static readonly Color Amber = Color.FromArgb(245, 158, 11);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Grey = Color.FromArgb(100, 116, 139);
        private static readonly Color DarkGrey = Color.FromArgb(51, 65, 85);

        public AMCPage()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            var ctorWatch = System.Diagnostics.Stopwatch.StartNew();
            EnsureDashboardShell();
            AppRuntime.LogTiming("AMC.BuildLayout", ctorWatch.ElapsedMilliseconds);
            RegisterFirstPaintTiming("AMC.FirstPaint", ctorWatch);
            Load += (s, e) => QueueAMCDataLoad();
        }

        private void QueueAMCDataLoad()
        {
            var timer = new Timer { Interval = 1500 };
            timer.Tick += async (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!IsDisposed && Visible)
                    await LoadAMCDataAsync();
            };
            timer.Start();
        }

        /// <summary>Returns the cached AMC module to its dashboard state when users reopen it from navigation.</summary>
        public void ShowDashboardFromNavigation()
        {
            EnsureDashboardShell();
            _ = LoadAMCDataAsync();
        }

        private void EnsureDashboardShell()
        {
            if (_dashboardShell == null || _dashboardShell.IsDisposed)
                _dashboardShell = BuildLayout();

            Controls.Clear();
            Controls.Add(_dashboardShell);
        }

        /// <summary>Builds the dashboard shell, KPI strip, and scrollable cards area.</summary>
        private Control BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = PageBg,
                Padding = new Padding(24, 20, 24, 20)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildKpiStrip(), 0, 1);
            root.Controls.Add(BuildCardCanvas(), 0, 2);
            return root;
        }

        /// <summary>Creates the page header and Add AMC action.</summary>
        private Control BuildHeader()
        {
            _btnAddAMC = MakeButton("+ Add AMC", Blue, 132);
            _btnAddAMC.Name = "btnAddAMC";
            _btnAddAMC.Click += (s, e) => BeginOpenAddAMCForm();

            _btnImportAMC = MakeButton("Import Excel", Blue, 120);
            _btnImportAMC.Name = "btnImportAMC";
            _btnImportAMC.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.AMC, FindForm());

            return SharedPageHeader.Build(new SharedPageHeaderModel
            {
                Name = "AMCPageHeader",
                Mode = SharedPageHeaderMode.Dashboard,
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Title = "AMC Contracts",
                Subtitle = "Track annual maintenance contracts, renewal windows, visits, and covered equipment.",
                TitleWidth = 360,
                SubtitleWidth = 640,
                RightActions = new List<Control> { _btnImportAMC, _btnAddAMC }
            }).Header;
        }

        /// <summary>Creates the four KPI cards shown while data loads.</summary>
        private Control BuildKpiStrip()
        {
            _kpiFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                BackColor = PageBg,
                Padding = new Padding(0, 10, 0, 12)
            };
            _totalValue = AddKpi("Total AMC", "-", string.Empty);
            _activeValue = AddKpi("Active", "-", "Active");
            _expiringValue = AddKpi("Expiring Soon", "-", "Expiring Soon");
            _expiredValue = AddKpi("Expired", "-", "Expired");
            return _kpiFlow;
        }

        /// <summary>Adds one KPI card and returns its mutable value label.</summary>
        private Label AddKpi(string title, string value, string statusFilter)
        {
            Panel card = MakeCard(new Padding(18, 14, 18, 14));
            card.Size = new Size(210, 86);
            card.Margin = new Padding(0, 0, 14, 0);
            card.Cursor = Cursors.Hand;
            card.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(18, 14),
                Size = new Size(170, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Muted
            });
            var number = new Label
            {
                Text = value,
                Location = new Point(18, 38),
                Size = new Size(170, 34),
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = Ink
            };
            card.Controls.Add(number);
            card.Click += (s, e) => ApplyStatusQuickFilter(statusFilter);
            foreach (Control child in card.Controls)
                child.Click += (s, e) => ApplyStatusQuickFilter(statusFilter);
            _kpiFlow.Controls.Add(card);
            return number;
        }

        /// <summary>Creates the scrollable AMC card grid, filters, and empty state.</summary>
        private Control BuildCardCanvas()
        {
            Panel shell = MakeCard(new Padding(0));
            shell.Dock = DockStyle.Fill;
            _listCaption = new Label
            {
                Text = "AMC Contract List",
                Location = new Point(18, 14),
                Size = new Size(260, 28),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Ink,
                AutoEllipsis = true
            };
            shell.Controls.Add(_listCaption);

            _searchBox = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Text = "Search",
                ForeColor = Muted,
                Size = new Size(240, 30)
            };
            _searchBox.GotFocus += (s, e) =>
            {
                if (_searchBox.Text == "Search")
                {
                    _searchBox.Text = string.Empty;
                    _searchBox.ForeColor = Ink;
                }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_searchBox.Text))
                {
                    _searchBox.Text = "Search";
                    _searchBox.ForeColor = Muted;
                }
            };
            _searchBox.TextChanged += (s, e) => ApplyFilters();
            shell.Controls.Add(_searchBox);

            _statusFilter = BuildFilter(new[] { "All Status", "Active", "Expiring Soon", "Expired", "Draft", "Cancelled" });
            _statusFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
            shell.Controls.Add(_statusFilter);

            _typeFilter = BuildFilter(new[] { "All Types", "Comprehensive", "Non-Comprehensive", "Labour Only", "Preventive" });
            _typeFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
            shell.Controls.Add(_typeFilter);

            _renewalFilter = BuildFilter(new[] { "All Renewals", "Next 7 Days", "Next 30 Days", "Next 60 Days", "Expired" });
            _renewalFilter.Size = new Size(136, 30);
            _renewalFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
            shell.Controls.Add(_renewalFilter);

            _contractGrid = new DataGridView
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(18, 64),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White
            };
            ConfigureContractGrid();
            _contractGrid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _contractGrid.Columns[e.ColumnIndex].Name != "OpenAction")
                    return;

                if (_contractGrid.Rows[e.RowIndex].Tag is AMCRow row)
                    OpenDetailPage(row.ContractId);
            };
            _emptyLabel = new Label
            {
                Text = "No AMC contracts match the current view.",
                Font = new Font("Segoe UI", 11f),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            shell.Controls.Add(_contractGrid);
            shell.Controls.Add(_emptyLabel);
            shell.Resize += (s, e) => LayoutCardCanvas(shell);
            LayoutCardCanvas(shell);
            return shell;
        }

        /// <summary>Configures the operational AMC list grid.</summary>
        private void ConfigureContractGrid()
        {
            _contractGrid.Columns.Clear();
            _contractGrid.Columns.Add(MakeTextColumn("AMCNumber", "AMC No", 105));
            _contractGrid.Columns.Add(MakeTextColumn("ClientName", "Client", 170));
            _contractGrid.Columns.Add(MakeTextColumn("SiteName", "Site", 150));
            _contractGrid.Columns.Add(MakeTextColumn("Status", "Status", 95));
            _contractGrid.Columns.Add(MakeTextColumn("EndDate", "Ends", 95));
            _contractGrid.Columns.Add(MakeTextColumn("DaysLeft", "Days", 85));
            _contractGrid.Columns.Add(MakeTextColumn("Value", "Value", 110));
            _contractGrid.Columns.Add(MakeTextColumn("Visits", "Visits", 90));
            _contractGrid.Columns.Add(MakeTextColumn("NextVisit", "Next Visit", 120));
            var open = new DataGridViewButtonColumn
            {
                Name = "OpenAction",
                HeaderText = "Action",
                Text = "Open",
                UseColumnTextForButtonValue = true,
                FillWeight = 75,
                MinimumWidth = 70,
                FlatStyle = FlatStyle.Flat
            };
            _contractGrid.Columns.Add(open);
            _contractGrid.RowTemplate.Height = 44;
            _contractGrid.CellFormatting += ContractGrid_CellFormatting;
            GridTheme.Apply(_contractGrid);
            _contractGrid.Dock = DockStyle.None;
            _contractGrid.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
        }

        private static DataGridViewTextBoxColumn MakeTextColumn(string name, string header, int fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                FillWeight = fillWeight,
                MinimumWidth = 60
            };
        }

        /// <summary>Applies status and urgency styling to the operational AMC grid.</summary>
        private void ContractGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _contractGrid == null || e.RowIndex >= _contractGrid.Rows.Count)
                return;

            AMCRow row = _contractGrid.Rows[e.RowIndex].Tag as AMCRow;
            if (row == null)
                return;

            string columnName = _contractGrid.Columns[e.ColumnIndex].Name;
            if (columnName == "Status")
            {
                e.CellStyle.ForeColor = GetStatusColor(row.DisplayStatus);
                e.CellStyle.Font = new Font(_contractGrid.Font, FontStyle.Bold);
            }
            else if (columnName == "DaysLeft")
            {
                e.CellStyle.ForeColor = GetDaysLeftColor(row);
                e.CellStyle.Font = new Font(_contractGrid.Font, FontStyle.Bold);
            }
            else if (columnName == "Visits")
            {
                e.CellStyle.ForeColor = GetVisitProgressColor(row);
                e.CellStyle.Font = new Font(_contractGrid.Font, FontStyle.Bold);
            }
            else if (columnName == "Value" || columnName == "AMCNumber" || columnName == "ClientName")
            {
                e.CellStyle.Font = new Font(_contractGrid.Font, FontStyle.Bold);
            }
        }

        /// <summary>Keeps AMC filters and list content usable on compact module widths.</summary>
        private void LayoutCardCanvas(Control shell)
        {
            if (shell == null || _typeFilter == null || _statusFilter == null || _renewalFilter == null || _searchBox == null || _contractGrid == null || _emptyLabel == null)
                return;

            int width = Math.Max(360, shell.ClientSize.Width);
            int height = Math.Max(220, shell.ClientSize.Height);
            bool compact = width < 900;
            int filterTop = compact ? 48 : 15;
            int listTop = compact ? 94 : 64;

            _listCaption.Width = compact ? width - 36 : Math.Max(220, width - 720);
            _typeFilter.Location = new Point(Math.Max(18, width - 664), filterTop);
            _statusFilter.Location = new Point(Math.Max(18, width - 528), filterTop);
            _renewalFilter.Location = new Point(Math.Max(18, width - 390), filterTop);
            _searchBox.Location = new Point(Math.Max(18, width - 246), filterTop);

            if (compact)
            {
                _typeFilter.Location = new Point(18, filterTop);
                _statusFilter.Location = new Point(154, filterTop);
                _renewalFilter.Location = new Point(290, filterTop);
                _searchBox.Location = new Point(434, filterTop);
                _searchBox.Width = Math.Max(160, width - _searchBox.Left - 18);
            }

            _contractGrid.Location = new Point(18, listTop);
            _contractGrid.Size = new Size(Math.Max(120, width - 36), Math.Max(120, height - listTop - 18));
            _emptyLabel.Bounds = _contractGrid.Bounds;
        }

        /// <summary>Creates a dropdown filter.</summary>
        private ComboBox BuildFilter(IEnumerable<string> items)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(128, 30),
                Font = new Font("Segoe UI", 9f)
            };
            foreach (string item in items)
                combo.Items.Add(item);
            combo.SelectedIndex = 0;
            return combo;
        }

        /// <summary>Starts the async data load.</summary>
        private async Task LoadAMCDataAsync()
        {
            if (_loadInProgress)
                return;

            _loadInProgress = true;
            try
            {
                SetLoading();
                var fetchWatch = System.Diagnostics.Stopwatch.StartNew();
                AMCPayload payload = await Task.Run(() =>
                    AppDataCache.GetOrCreate("amc:dashboard-payload", TimeSpan.FromMinutes(2), LoadPayload));
                AppRuntime.LogTiming("AMC.FetchData", fetchWatch.ElapsedMilliseconds);
                var bindWatch = System.Diagnostics.Stopwatch.StartNew();
                BindPayload(payload ?? new AMCPayload());
                AppRuntime.LogTiming("AMC.BindData", bindWatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                ShowError("Failed to load AMC contracts. Please try again.", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("AMC"), "Loading AMC contracts", ex);
            }
            finally
            {
                _loadInProgress = false;
            }
        }

        /// <summary>Loads KPI counts and AMC card rows from SQL Server.</summary>
        private AMCPayload LoadPayload()
        {
            var payload = new AMCPayload();
            DbHelper.EnsureAMCSchema();
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AMCPage.LoadPayload");
                using (SqlCommand command = new SqlCommand(@"
SELECT
    Total = COUNT(1),
    Active = SUM(CASE WHEN ISNULL(Status, ContractStatus) = 'Active' AND EndDate >= CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END),
    ExpiringSoon = SUM(CASE WHEN ISNULL(Status, ContractStatus) = 'Expiring Soon'
        OR (ISNULL(Status, ContractStatus) = 'Active' AND EndDate BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(day, 30, CAST(GETDATE() AS DATE)))
        THEN 1 ELSE 0 END),
    Expired = SUM(CASE WHEN ISNULL(Status, ContractStatus) = 'Expired' OR EndDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END)
FROM AMCContracts;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        payload.Total = ReadInt(reader, "Total");
                        payload.Active = ReadInt(reader, "Active");
                        payload.ExpiringSoon = ReadInt(reader, "ExpiringSoon");
                        payload.Expired = ReadInt(reader, "Expired");
                    }
                }

                using (SqlCommand command = new SqlCommand(@"
SELECT
    c.ContractID,
    c.AMCNumber,
    ClientName = b.CompanyName,
    SiteName = s.SiteName,
    EquipmentDesc = c.EquipmentDesc,
    AMCType = ISNULL(c.AMCType, c.ContractType),
    CoverageType = ISNULL(c.CoverageType, c.AMCType),
    Status = ISNULL(c.Status, c.ContractStatus),
    c.StartDate,
    c.EndDate,
    ContractValue = CASE WHEN ISNULL(c.ContractValue, 0) > 0 THEN c.ContractValue ELSE ISNULL(c.AnnualValue, 0) END,
    c.BillingCycle,
    c.VisitsPerYear,
    EquipmentCount = ISNULL(e.EquipmentCount, 0),
    VisitsCompleted = ISNULL(v.VisitsCompleted, 0),
    VisitsScheduled = ISNULL(v.VisitsScheduled, 0),
    NextServiceDue = v.NextServiceDue,
    MissedVisits = ISNULL(v.MissedVisits, 0),
    OverdueScheduledVisits = ISNULL(v.OverdueScheduledVisits, 0)
FROM AMCContracts c
INNER JOIN B2BClients b ON b.ClientID = c.ClientID
LEFT JOIN ClientSites s ON s.SiteID = c.SiteID
LEFT JOIN (
    SELECT AMCID, EquipmentCount = COUNT(1)
    FROM AMCEquipment
    GROUP BY AMCID
) e ON e.AMCID = c.ContractID
LEFT JOIN (
    SELECT
        AMCID,
        VisitsCompleted = SUM(CASE WHEN CompletedDate IS NOT NULL OR Status = 'Completed' THEN 1 ELSE 0 END),
        VisitsScheduled = COUNT(1),
        NextServiceDue = MIN(CASE WHEN Status IN ('Scheduled', 'Rescheduled') THEN ScheduledDate ELSE NULL END),
        MissedVisits = SUM(CASE WHEN Status = 'Missed' THEN 1 ELSE 0 END),
        OverdueScheduledVisits = SUM(CASE WHEN Status IN ('Scheduled', 'Rescheduled') AND ScheduledDate < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END)
    FROM AMCVisits
    GROUP BY AMCID
) v ON v.AMCID = c.ContractID
ORDER BY c.EndDate ASC, c.ContractID DESC;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new AMCRow
                        {
                            ContractId = ReadInt(reader, "ContractID"),
                            AMCNumber = ReadString(reader, "AMCNumber"),
                            ClientName = ReadString(reader, "ClientName"),
                            SiteName = ReadString(reader, "SiteName"),
                            EquipmentDesc = ReadString(reader, "EquipmentDesc"),
                            AMCType = ReadString(reader, "AMCType"),
                            CoverageType = ReadString(reader, "CoverageType"),
                            Status = ReadString(reader, "Status"),
                            StartDate = ReadDate(reader, "StartDate"),
                            EndDate = ReadDate(reader, "EndDate"),
                            ContractValue = ReadDecimal(reader, "ContractValue"),
                            BillingCycle = ReadString(reader, "BillingCycle"),
                            VisitsPerYear = ReadInt(reader, "VisitsPerYear"),
                            EquipmentCount = ReadInt(reader, "EquipmentCount"),
                            VisitsCompleted = ReadInt(reader, "VisitsCompleted"),
                            VisitsScheduled = ReadInt(reader, "VisitsScheduled"),
                            NextServiceDue = ReadDate(reader, "NextServiceDue"),
                            MissedVisits = ReadInt(reader, "MissedVisits"),
                            OverdueScheduledVisits = ReadInt(reader, "OverdueScheduledVisits")
                        };
                        row.DisplayStatus = GetDisplayStatus(row.Status, row.EndDate);
                        payload.Rows.Add(row);
                    }
                }
            }

            return payload;
        }

        /// <summary>Binds the loaded KPI and card data to the dashboard.</summary>
        private void BindPayload(AMCPayload payload)
        {
            _totalValue.Text = payload.Total.ToString(CultureInfo.InvariantCulture);
            _activeValue.Text = payload.Active.ToString(CultureInfo.InvariantCulture);
            _expiringValue.Text = payload.ExpiringSoon.ToString(CultureInfo.InvariantCulture);
            _expiredValue.Text = payload.Expired.ToString(CultureInfo.InvariantCulture);

            _allRows.Clear();
            _allRows.AddRange(payload.Rows);
            ApplyFilters();
        }

        /// <summary>Applies the current dashboard filters to the loaded AMC rows.</summary>
        private void ApplyFilters()
        {
            if (_contractGrid == null)
                return;

            string search = _searchBox == null || _searchBox.Text == "Search" ? string.Empty : _searchBox.Text.Trim();
            string status = _statusFilter == null || _statusFilter.SelectedIndex <= 0 ? string.Empty : Convert.ToString(_statusFilter.SelectedItem, CultureInfo.InvariantCulture);
            string type = _typeFilter == null || _typeFilter.SelectedIndex <= 0 ? string.Empty : Convert.ToString(_typeFilter.SelectedItem, CultureInfo.InvariantCulture);

            List<AMCRow> rows = _allRows
                .Where(row => string.IsNullOrWhiteSpace(status) || string.Equals(row.DisplayStatus, status, StringComparison.OrdinalIgnoreCase))
                .Where(row => string.IsNullOrWhiteSpace(type) || string.Equals(row.AMCType, type, StringComparison.OrdinalIgnoreCase))
                .Where(row => MatchesRenewalWindow(row))
                .Where(row => MatchesSearch(row, search))
                .ToList();

            rows = rows
                .OrderBy(row => row.EndDate ?? DateTime.MaxValue)
                .ThenBy(row => row.ClientName)
                .ToList();

            _contractGrid.Rows.Clear();
            _contractGrid.SuspendLayout();
            try
            {
                foreach (AMCRow row in rows)
                {
                    int index = _contractGrid.Rows.Add(
                        DisplayAmcNumber(row),
                        string.IsNullOrWhiteSpace(row.ClientName) ? "-" : row.ClientName,
                        string.IsNullOrWhiteSpace(row.SiteName) ? "-" : row.SiteName,
                        row.DisplayStatus,
                        FormatDate(row.EndDate),
                        BuildDaysLeft(row),
                        row.ContractValue.ToString("C0", _india),
                        BuildVisitProgress(row).Replace(" visits done", ""),
                        BuildNextServiceDate(row),
                        "Open");
                    _contractGrid.Rows[index].Tag = row;
                }
            }
            finally
            {
                _contractGrid.ResumeLayout();
            }

            _listCaption.Text = "AMC Contract List (" + rows.Count.ToString(CultureInfo.InvariantCulture) + ")";
            _emptyLabel.Text = _allRows.Count == 0
                ? "No AMC contracts yet. Click '+ Add AMC' to create one."
                : "No AMC contracts match the current view.";
            _emptyLabel.Visible = rows.Count == 0;
            _contractGrid.Visible = rows.Count > 0;
        }

        /// <summary>Returns whether a row matches the user search text.</summary>
        private bool MatchesSearch(AMCRow row, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string haystack = string.Join(" ", new[]
            {
                row.AMCNumber,
                row.ClientName,
                row.SiteName,
                row.AMCType,
                row.DisplayStatus,
                row.EquipmentDesc
            });
            return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Returns whether a row matches the selected renewal timing filter.</summary>
        private bool MatchesRenewalWindow(AMCRow row)
        {
            if (_renewalFilter == null || _renewalFilter.SelectedIndex <= 0)
                return true;

            string filter = Convert.ToString(_renewalFilter.SelectedItem, CultureInfo.InvariantCulture);
            DateTime today = DateTime.Today;
            if (string.Equals(filter, "Expired", StringComparison.OrdinalIgnoreCase))
                return row.EndDate.HasValue && row.EndDate.Value.Date < today;

            int days = 0;
            if (string.Equals(filter, "Next 7 Days", StringComparison.OrdinalIgnoreCase)) days = 7;
            if (string.Equals(filter, "Next 30 Days", StringComparison.OrdinalIgnoreCase)) days = 30;
            if (string.Equals(filter, "Next 60 Days", StringComparison.OrdinalIgnoreCase)) days = 60;
            return days <= 0 || (row.EndDate.HasValue && row.EndDate.Value.Date >= today && row.EndDate.Value.Date <= today.AddDays(days));
        }

        /// <summary>Applies a KPI quick filter to the status dropdown.</summary>
        private void ApplyStatusQuickFilter(string status)
        {
            if (_statusFilter == null)
                return;

            string target = string.IsNullOrWhiteSpace(status) ? "All Status" : status;
            int index = _statusFilter.Items.IndexOf(target);
            if (index >= 0)
                _statusFilter.SelectedIndex = index;

            if (_renewalFilter != null && string.IsNullOrWhiteSpace(status))
                _renewalFilter.SelectedIndex = 0;

            ApplyFilters();
        }

        /// <summary>Shows loading placeholders on the dashboard.</summary>
        private void SetLoading()
        {
            _totalValue.Text = "-";
            _activeValue.Text = "-";
            _expiringValue.Text = "-";
            _expiredValue.Text = "-";
            _allRows.Clear();
            if (_contractGrid != null)
                _contractGrid.Rows.Clear();
            _emptyLabel.Visible = false;
            if (_contractGrid != null)
                _contractGrid.Visible = true;
        }

        /// <summary>Builds the list header row.</summary>
        private Control BuildListHeader()
        {
            Panel header = CreateListRowPanel(38, true);
            AddHeaderCell(header, "AMC No", 0);
            AddHeaderCell(header, "Client", 1);
            AddHeaderCell(header, "Site", 2);
            AddHeaderCell(header, "Status", 3);
            AddHeaderCell(header, "Ends", 4);
            AddHeaderCell(header, "Days", 5);
            AddHeaderCell(header, "Value", 6);
            AddHeaderCell(header, "Visits", 7);
            AddHeaderCell(header, "Next Visit", 8);
            AddHeaderCell(header, "Action", 9);
            LayoutListRow(header);
            return header;
        }

        /// <summary>Builds one operational AMC list row.</summary>
        private Control BuildAMCListRow(AMCRow row)
        {
            Panel shell = CreateListRowPanel(72, false);
            shell.Tag = row;

            AddValueCell(shell, DisplayAmcNumber(row), 0, Ink, FontStyle.Bold);
            AddValueCell(shell, row.ClientName, 1, Ink, FontStyle.Bold);
            AddValueCell(shell, string.IsNullOrWhiteSpace(row.SiteName) ? "-" : row.SiteName, 2, Muted, FontStyle.Regular);
            AddBadgeCell(shell, row.DisplayStatus, GetStatusColor(row.DisplayStatus), 3);
            AddValueCell(shell, FormatDate(row.EndDate), 4, Ink, FontStyle.Regular);
            AddValueCell(shell, BuildDaysLeft(row), 5, GetDaysLeftColor(row), FontStyle.Bold);
            AddValueCell(shell, row.ContractValue.ToString("C0", _india), 6, Ink, FontStyle.Bold);
            AddValueCell(shell, BuildVisitProgress(row).Replace(" visits done", ""), 7, GetVisitProgressColor(row), FontStyle.Bold);
            AddValueCell(shell, BuildNextServiceDate(row), 8, IsNextServiceOverdue(row) ? Red : Muted, FontStyle.Regular);

            Button open = MakeButton("Open", Color.White, 76);
            open.ForeColor = Blue;
            open.FlatAppearance.BorderColor = DS.Border;
            open.FlatAppearance.BorderSize = 1;
            open.Tag = 9;
            open.Click += (s, e) => OpenDetailPage(row.ContractId);
            shell.Controls.Add(open);

            LayoutListRow(shell);
            return shell;
        }

        /// <summary>Creates a lightweight list row panel.</summary>
        private Panel CreateListRowPanel(int height, bool header)
        {
            var shell = new Panel
            {
                Width = GetListRowWidth(),
                Height = height,
                BackColor = header ? Color.FromArgb(248, 250, 252) : Color.White,
                Margin = new Padding(0, 0, 0, header ? 2 : 1),
                Padding = new Padding(0)
            };
            shell.Resize += (s, e) => LayoutListRow(shell);
            shell.Paint += (s, e) =>
            {
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, shell.Height - 1, shell.Width, shell.Height - 1);
            };
            return shell;
        }

        private void AddHeaderCell(Panel parent, string text, int column)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Muted,
                AutoEllipsis = true,
                Tag = column
            });
        }

        private void AddValueCell(Panel parent, string text, int column, Color color, FontStyle style)
        {
            parent.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(text) ? "-" : text,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f, style),
                ForeColor = color,
                AutoEllipsis = true,
                Tag = column
            });
        }

        private void AddBadgeCell(Panel parent, string text, Color color, int column)
        {
            Label badge = MakeBadge(text, color, new Point(0, 0), 96);
            badge.Tag = column;
            parent.Controls.Add(badge);
        }

        /// <summary>Positions all cells in a lightweight AMC row.</summary>
        private void LayoutListRow(Panel row)
        {
            if (row == null || row.IsDisposed)
                return;

            int[] widths = BuildColumnWidths(Math.Max(720, row.ClientSize.Width));
            int[] lefts = new int[widths.Length];
            for (int i = 1; i < lefts.Length; i++)
                lefts[i] = lefts[i - 1] + widths[i - 1];

            foreach (Control child in row.Controls)
            {
                int column = child.Tag is int ? (int)child.Tag : 0;
                column = Math.Max(0, Math.Min(column, widths.Length - 1));
                int left = lefts[column] + 10;
                int width = Math.Max(30, widths[column] - 16);

                if (child is Button)
                {
                    child.Bounds = new Rectangle(left, Math.Max(8, (row.Height - 34) / 2), Math.Max(64, width), 34);
                }
                else if (child.BackColor != Color.Transparent && child.ForeColor == Color.White)
                {
                    child.Bounds = new Rectangle(left, Math.Max(8, (row.Height - 24) / 2), Math.Min(96, width), 24);
                }
                else
                {
                    child.Bounds = new Rectangle(left, 0, width, row.Height - 1);
                }
            }
        }

        /// <summary>Calculates column widths for the operational AMC list.</summary>
        private static int[] BuildColumnWidths(int totalWidth)
        {
            int[] percents = { 10, 16, 12, 10, 9, 7, 10, 8, 10, 8 };
            int[] widths = new int[percents.Length];
            int used = 0;
            for (int i = 0; i < percents.Length; i++)
            {
                widths[i] = Math.Max(52, totalWidth * percents[i] / 100);
                used += widths[i];
            }

            widths[widths.Length - 1] += Math.Max(0, totalWidth - used);
            return widths;
        }

        /// <summary>Defers modal launch so the AMC page finishes its click cycle before the form opens.</summary>
        private void BeginOpenAddAMCForm()
        {
            if (_addAmcDialogOpen)
                return;

            _addAmcDialogOpen = true;
            if (_btnAddAMC != null && !_btnAddAMC.IsDisposed)
                _btnAddAMC.Enabled = false;

            Action open = OpenAddAMCForm;
            if (IsHandleCreated)
                BeginInvoke(open);
            else
                open();
        }

        /// <summary>Opens the add AMC form as a lightweight modal and refreshes after save.</summary>
        private void OpenAddAMCForm()
        {
            int? createdContractId = null;
            bool saved = false;
            try
            {
                using (var form = new AddAMCForm())
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        saved = true;
                        createdContractId = form.LastSavedContractId;
                    }
                }
            }
            finally
            {
                _addAmcDialogOpen = false;
                if (_btnAddAMC != null && !_btnAddAMC.IsDisposed)
                    _btnAddAMC.Enabled = true;
            }

            if (!saved)
                return;

            if (createdContractId.HasValue && createdContractId.Value > 0)
            {
                // Navigate to the new contract; OpenDetailPage rebuilds the control tree so skip the async refresh.
                OpenDetailPage(createdContractId.Value);
            }
            else
            {
                _ = LoadAMCDataAsync();
            }
        }

        /// <summary>Navigates directly to the specified contract's detail page from an external caller.</summary>
        public void OpenContractById(int contractId)
        {
            if (contractId > 0)
                OpenDetailPage(contractId);
        }

        /// <summary>Opens the AMC detail page inside this module surface.</summary>
        private void OpenDetailPage(int contractId)
        {
            if (contractId <= 0)
            {
                _ = LoadAMCDataAsync();
                return;
            }

            Controls.Clear();
            Controls.Add(new AMCDetailPage(contractId, ShowDashboard, OpenEditAMCForm));
        }

        /// <summary>Returns from detail view to the refreshed AMC dashboard.</summary>
        private void ShowDashboard()
        {
            EnsureDashboardShell();
            _ = LoadAMCDataAsync();
        }

        /// <summary>Opens an existing AMC contract in edit mode and returns to detail after save.</summary>
        private void OpenEditAMCForm(int contractId)
        {
            if (contractId <= 0)
                return;

            bool saved = false;
            using (var form = new AddAMCForm(contractId))
            {
                saved = form.ShowDialog(this) == DialogResult.OK;
            }

            if (saved)
            {
                AppDataCache.Remove("amc:dashboard-payload");
                OpenDetailPage(contractId);
            }
        }

        /// <summary>Creates a shared styled button.</summary>
        private Button MakeButton(string text, Color back, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = back,
                ForeColor = back == Color.White ? Ink : Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            DS.Rounded(button, 6);
            return button;
        }

        /// <summary>Creates a white rounded card panel.</summary>
        private Panel MakeCard(Padding padding)
        {
            var panel = new Panel { BackColor = Color.White, Padding = padding };
            panel.Paint += (s, e) =>
            {
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            DS.Rounded(panel, 8);
            return panel;
        }

        /// <summary>Creates a compact coloured badge label.</summary>
        private Label MakeBadge(string text, Color color, Point location, int width)
        {
            var label = new Label
            {
                Text = string.IsNullOrWhiteSpace(text) ? "-" : text,
                Location = location,
                Size = new Size(width, 24),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = color,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };
            DS.Rounded(label, 12);
            return label;
        }

        /// <summary>Maps AMC type to badge colour.</summary>
        private Color GetTypeColor(string value)
        {
            if (string.Equals(value, "Comprehensive", StringComparison.OrdinalIgnoreCase)) return Blue;
            if (string.Equals(value, "Non-Comprehensive", StringComparison.OrdinalIgnoreCase)) return Amber;
            if (string.Equals(value, "Preventive", StringComparison.OrdinalIgnoreCase)) return Green;
            return Grey;
        }

        /// <summary>Maps AMC status to badge colour.</summary>
        private Color GetStatusColor(string value)
        {
            if (string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase)) return Green;
            if (string.Equals(value, "Expiring Soon", StringComparison.OrdinalIgnoreCase)) return Amber;
            if (string.Equals(value, "Expired", StringComparison.OrdinalIgnoreCase)) return Red;
            if (string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase)) return DarkGrey;
            return Grey;
        }

        /// <summary>Maps coverage type to badge colour.</summary>
        private Color GetCoverageColor(string value)
        {
            return string.Equals(value, "Non-Comprehensive", StringComparison.OrdinalIgnoreCase) ? Amber : Blue;
        }

        /// <summary>Returns AMC visit progress text.</summary>
        private string BuildVisitProgress(AMCRow row)
        {
            int total = row.VisitsScheduled > 0 ? row.VisitsScheduled : row.VisitsPerYear;
            return row.VisitsCompleted.ToString(CultureInfo.InvariantCulture) + " of " + total.ToString(CultureInfo.InvariantCulture) + " visits done";
        }

        /// <summary>Returns visit progress colour based on overdue/missed state.</summary>
        private Color GetVisitProgressColor(AMCRow row)
        {
            if (row.MissedVisits > 0)
                return Red;
            if (row.OverdueScheduledVisits > 0)
                return Amber;
            return Green;
        }

        /// <summary>Builds next service due text for a card.</summary>
        private string BuildNextServiceText(AMCRow row)
        {
            return row.NextServiceDue.HasValue
                ? "Next: " + row.NextServiceDue.Value.ToString("dd MMM yyyy", _india)
                : "Next: not scheduled";
        }

        /// <summary>Builds a compact next service date for the operational list.</summary>
        private string BuildNextServiceDate(AMCRow row)
        {
            return row.NextServiceDue.HasValue
                ? row.NextServiceDue.Value.ToString("dd/MM/yyyy", _india)
                : "Not scheduled";
        }

        /// <summary>Returns whether next service date is overdue.</summary>
        private bool IsNextServiceOverdue(AMCRow row)
        {
            return row.NextServiceDue.HasValue && row.NextServiceDue.Value.Date < DateTime.Today;
        }

        /// <summary>Returns the AMC number shown to the user.</summary>
        private static string DisplayAmcNumber(AMCRow row)
        {
            return string.IsNullOrWhiteSpace(row.AMCNumber)
                ? "AMC-" + row.ContractId.ToString("000", CultureInfo.InvariantCulture)
                : row.AMCNumber;
        }

        /// <summary>Builds the renewal countdown text.</summary>
        private string BuildDaysLeft(AMCRow row)
        {
            if (!row.EndDate.HasValue)
                return "-";

            int days = (row.EndDate.Value.Date - DateTime.Today).Days;
            if (days < 0)
                return Math.Abs(days).ToString(CultureInfo.InvariantCulture) + " overdue";
            if (days == 0)
                return "Today";
            return days.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Returns the urgency colour for the renewal countdown.</summary>
        private Color GetDaysLeftColor(AMCRow row)
        {
            if (!row.EndDate.HasValue)
                return Muted;

            int days = (row.EndDate.Value.Date - DateTime.Today).Days;
            if (days < 0)
                return Red;
            if (days <= 30)
                return Amber;
            return Green;
        }

        /// <summary>Returns the current list row width without causing horizontal scrollbars.</summary>
        private int GetListRowWidth()
        {
            return _contractGrid == null ? 1000 : Math.Max(720, _contractGrid.ClientSize.Width);
        }

        /// <summary>Keeps all operational rows aligned to the available list width.</summary>
        private void ResizeListRows()
        {
        }

        /// <summary>Returns the display status based on saved status and end date.</summary>
        private static string GetDisplayStatus(string status, DateTime? endDate)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)) return "Cancelled";
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return "Draft";
            if (endDate.HasValue && endDate.Value.Date < DateTime.Today) return "Expired";
            if (endDate.HasValue && endDate.Value.Date <= DateTime.Today.AddDays(30)) return "Expiring Soon";
            return string.IsNullOrWhiteSpace(status) ? "Active" : status;
        }

        /// <summary>Formats a nullable SQL date in Indian date style.</summary>
        private string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("dd/MM/yyyy", _india) : "-";
        }

        /// <summary>Reads a nullable integer from a data reader.</summary>
        private static int ReadInt(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a nullable decimal from a data reader.</summary>
        private static decimal ReadDecimal(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a nullable string from a data reader.</summary>
        private static string ReadString(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a nullable date from a data reader.</summary>
        private static DateTime? ReadDate(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }

        private sealed class AMCPayload
        {
            public int Total;
            public int Active;
            public int ExpiringSoon;
            public int Expired;
            public readonly List<AMCRow> Rows = new List<AMCRow>();
        }

        private sealed class AMCRow
        {
            public int ContractId;
            public string AMCNumber;
            public string ClientName;
            public string SiteName;
            public string EquipmentDesc;
            public string AMCType;
            public string CoverageType;
            public string Status;
            public string DisplayStatus;
            public DateTime? StartDate;
            public DateTime? EndDate;
            public decimal ContractValue;
            public string BillingCycle;
            public int VisitsPerYear;
            public int EquipmentCount;
            public int VisitsCompleted;
            public int VisitsScheduled;
            public DateTime? NextServiceDue;
            public int MissedVisits;
            public int OverdueScheduledVisits;
        }
    }
}


