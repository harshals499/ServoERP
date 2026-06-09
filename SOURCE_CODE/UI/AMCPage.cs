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
        private FlowLayoutPanel _cardFlow;
        private Label _emptyLabel;
        private Label _totalValue;
        private Label _activeValue;
        private Label _expiringValue;
        private Label _expiredValue;
        private TextBox _searchBox;
        private ComboBox _statusFilter;
        private ComboBox _typeFilter;
        private Label _listCaption;
        private Button _btnAddAMC;
        private bool _loadInProgress;
        private bool _addAmcDialogOpen;

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
            BuildLayout();
            Load += async (s, e) => await LoadAMCDataAsync();
        }

        /// <summary>Builds the dashboard shell, KPI strip, and scrollable cards area.</summary>
        private void BuildLayout()
        {
            Controls.Clear();
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
            Controls.Add(root);
        }

        /// <summary>Creates the page header and Add AMC action.</summary>
        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            header.Controls.Add(new Label
            {
                Text = "AMC Contracts",
                Location = new Point(0, 0),
                Size = new Size(360, 34),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Ink
            });
            header.Controls.Add(new Label
            {
                Text = "Track annual maintenance contracts, renewal windows, visits, and covered equipment.",
                Location = new Point(1, 38),
                Size = new Size(640, 22),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted
            });

            _btnAddAMC = MakeButton("+ Add AMC", Blue, 132);
            _btnAddAMC.Name = "btnAddAMC";
            _btnAddAMC.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnAddAMC.Location = new Point(Math.Max(0, header.Width - 132), 6);
            _btnAddAMC.Click += (s, e) => BeginOpenAddAMCForm();
            header.Controls.Add(_btnAddAMC);
            header.Resize += (s, e) => _btnAddAMC.Location = new Point(header.ClientSize.Width - 132, 6);
            return header;
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
            _totalValue = AddKpi("Total AMC", "-");
            _activeValue = AddKpi("Active", "-");
            _expiringValue = AddKpi("Expiring Soon", "-");
            _expiredValue = AddKpi("Expired", "-");
            return _kpiFlow;
        }

        /// <summary>Adds one KPI card and returns its mutable value label.</summary>
        private Label AddKpi(string title, string value)
        {
            Panel card = MakeCard(new Padding(18, 14, 18, 14));
            card.Size = new Size(210, 86);
            card.Margin = new Padding(0, 0, 14, 0);
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
                Text = "Search AMC, client, equipment...",
                ForeColor = Muted,
                Size = new Size(240, 30)
            };
            _searchBox.GotFocus += (s, e) =>
            {
                if (_searchBox.Text == "Search AMC, client, equipment...")
                {
                    _searchBox.Text = string.Empty;
                    _searchBox.ForeColor = Ink;
                }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_searchBox.Text))
                {
                    _searchBox.Text = "Search AMC, client, equipment...";
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

            _cardFlow = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(18, 64),
                AutoScroll = true,
                WrapContents = true,
                BackColor = Color.White,
                Padding = new Padding(0, 0, 6, 10)
            };
            _emptyLabel = new Label
            {
                Text = "No AMC contracts match the current view.",
                Font = new Font("Segoe UI", 11f),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            shell.Controls.Add(_cardFlow);
            shell.Controls.Add(_emptyLabel);
            shell.Resize += (s, e) => LayoutCardCanvas(shell);
            LayoutCardCanvas(shell);
            return shell;
        }

        /// <summary>Keeps AMC filters and list content usable on compact module widths.</summary>
        private void LayoutCardCanvas(Control shell)
        {
            if (shell == null || _typeFilter == null || _statusFilter == null || _searchBox == null || _cardFlow == null || _emptyLabel == null)
                return;

            int width = Math.Max(360, shell.ClientSize.Width);
            int height = Math.Max(220, shell.ClientSize.Height);
            bool compact = width < 760;
            int filterTop = compact ? 48 : 15;
            int listTop = compact ? 94 : 64;

            _listCaption.Width = compact ? width - 36 : Math.Max(220, width - 560);
            _typeFilter.Location = new Point(Math.Max(18, width - 520), filterTop);
            _statusFilter.Location = new Point(Math.Max(18, width - 384), filterTop);
            _searchBox.Location = new Point(Math.Max(18, width - 246), filterTop);

            if (compact)
            {
                _typeFilter.Location = new Point(18, filterTop);
                _statusFilter.Location = new Point(154, filterTop);
                _searchBox.Location = new Point(290, filterTop);
                _searchBox.Width = Math.Max(160, width - _searchBox.Left - 18);
            }

            _cardFlow.Location = new Point(18, listTop);
            _cardFlow.Size = new Size(Math.Max(120, width - 36), Math.Max(120, height - listTop - 18));
            _emptyLabel.Bounds = new Rectangle(18, listTop, Math.Max(120, width - 36), Math.Max(120, height - listTop - 18));
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
                AMCPayload payload = await Task.Run(() => LoadPayload());
                BindPayload(payload ?? new AMCPayload());
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
            if (_cardFlow == null)
                return;

            string search = _searchBox == null || _searchBox.Text == "Search AMC, client, equipment..." ? string.Empty : _searchBox.Text.Trim();
            string status = _statusFilter == null || _statusFilter.SelectedIndex <= 0 ? string.Empty : Convert.ToString(_statusFilter.SelectedItem, CultureInfo.InvariantCulture);
            string type = _typeFilter == null || _typeFilter.SelectedIndex <= 0 ? string.Empty : Convert.ToString(_typeFilter.SelectedItem, CultureInfo.InvariantCulture);

            List<AMCRow> rows = _allRows
                .Where(row => string.IsNullOrWhiteSpace(status) || string.Equals(row.DisplayStatus, status, StringComparison.OrdinalIgnoreCase))
                .Where(row => string.IsNullOrWhiteSpace(type) || string.Equals(row.AMCType, type, StringComparison.OrdinalIgnoreCase))
                .Where(row => MatchesSearch(row, search))
                .ToList();

            _cardFlow.Controls.Clear();
            foreach (AMCRow row in rows)
                _cardFlow.Controls.Add(BuildAMCCard(row));

            _listCaption.Text = "AMC Contract List (" + rows.Count.ToString(CultureInfo.InvariantCulture) + ")";
            _emptyLabel.Text = _allRows.Count == 0
                ? "No AMC contracts yet. Click '+ Add AMC' to create one."
                : "No AMC contracts match the current view.";
            _emptyLabel.Visible = rows.Count == 0;
            _cardFlow.Visible = rows.Count > 0;
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

        /// <summary>Shows loading placeholders on the dashboard.</summary>
        private void SetLoading()
        {
            _totalValue.Text = "-";
            _activeValue.Text = "-";
            _expiringValue.Text = "-";
            _expiredValue.Text = "-";
            _allRows.Clear();
            _cardFlow.Controls.Clear();
            _emptyLabel.Visible = false;
            _cardFlow.Visible = true;
        }

        /// <summary>Builds one AMC summary card.</summary>
        private Control BuildAMCCard(AMCRow row)
        {
            Panel card = MakeCard(new Padding(16));
            card.Size = new Size(352, 236);
            card.Margin = new Padding(0, 0, 14, 14);

            card.Controls.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(row.AMCNumber) ? "AMC-" + row.ContractId.ToString("000", CultureInfo.InvariantCulture) : row.AMCNumber,
                Location = new Point(16, 14),
                Size = new Size(190, 24),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Ink
            });
            card.Controls.Add(MakeBadge(row.AMCType, GetTypeColor(row.AMCType), new Point(214, 15), 118));
            card.Controls.Add(new Label { Text = row.ClientName, Location = new Point(16, 43), Size = new Size(312, 22), Font = new Font("Segoe UI", 9.5f), ForeColor = Muted });
            card.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(row.SiteName) ? "Site: -" : "Site: " + row.SiteName, Location = new Point(16, 65), Size = new Size(312, 20), Font = new Font("Segoe UI", 8.5f), ForeColor = Muted });
            card.Controls.Add(MakeBadge(row.DisplayStatus, GetStatusColor(row.DisplayStatus), new Point(16, 92), 112));
            card.Controls.Add(new Label { Text = FormatDate(row.StartDate) + " -> " + FormatDate(row.EndDate), Location = new Point(138, 94), Size = new Size(190, 20), Font = new Font("Segoe UI", 9f), ForeColor = Ink });
            card.Controls.Add(new Label { Text = row.ContractValue.ToString("C0", _india), Location = new Point(16, 126), Size = new Size(150, 22), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Ink });
            card.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(row.BillingCycle) ? "Annual" : row.BillingCycle, Location = new Point(172, 126), Size = new Size(90, 22), Font = new Font("Segoe UI", 9f), ForeColor = Muted });
            card.Controls.Add(new Label { Text = row.EquipmentCount.ToString(CultureInfo.InvariantCulture) + " units covered", Location = new Point(16, 150), Size = new Size(130, 20), Font = new Font("Segoe UI", 9f), ForeColor = Muted });
            card.Controls.Add(new Label { Text = BuildVisitProgress(row), Location = new Point(152, 150), Size = new Size(176, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = GetVisitProgressColor(row) });
            card.Controls.Add(new Label { Text = BuildNextServiceText(row), Location = new Point(16, 170), Size = new Size(170, 20), Font = new Font("Segoe UI", 8.5f), ForeColor = IsNextServiceOverdue(row) ? Red : Muted });
            card.Controls.Add(MakeBadge(string.IsNullOrWhiteSpace(row.CoverageType) ? "Comprehensive" : row.CoverageType, GetCoverageColor(row.CoverageType), new Point(202, 170), 126));

            Button view = MakeButton("View / Edit", Color.White, 104);
            view.ForeColor = Blue;
            view.FlatAppearance.BorderColor = DS.Border;
            view.FlatAppearance.BorderSize = 1;
            view.Location = new Point(16, 196);
            view.Click += (s, e) => OpenDetailPage(row.ContractId);
            card.Controls.Add(view);
            return card;
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
            Controls.Add(new AMCDetailPage(contractId, ShowDashboard));
        }

        /// <summary>Returns from detail view to the refreshed AMC dashboard.</summary>
        private void ShowDashboard()
        {
            BuildLayout();
            _ = LoadAMCDataAsync();
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
                TextAlign = ContentAlignment.MiddleCenter
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

        /// <summary>Returns whether next service date is overdue.</summary>
        private bool IsNextServiceOverdue(AMCRow row)
        {
            return row.NextServiceDue.HasValue && row.NextServiceDue.Value.Date < DateTime.Today;
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


