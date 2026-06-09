using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Detail view for one AMC contract with equipment, visit schedule, and service history.</summary>
    public partial class AMCDetailPage : BaseUserControl
    {
        private readonly int _amcId;
        private readonly Action _backAction;
        private readonly CultureInfo _india = new CultureInfo("en-IN");
        private Label _number;
        private Label _client;
        private Label _status;
        private Label _coverage;
        private FlowLayoutPanel _health;
        private DataGridView _equipmentGrid;
        private DataGridView _visitsGrid;
        private DataGridView _historyGrid;
        private Label _historyEmpty;
        private Button _markComplete;
        private DetailPayload _payload;

        private static readonly Color PageBg = Color.FromArgb(246, 248, 252);
        private static readonly Color Ink = Color.FromArgb(15, 23, 42);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(16, 185, 129);
        private static readonly Color Amber = Color.FromArgb(245, 158, 11);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Grey = Color.FromArgb(100, 116, 139);
        private static readonly Color DarkGrey = Color.FromArgb(51, 65, 85);

        public AMCDetailPage(int amcId, Action backAction)
        {
            _amcId = amcId;
            _backAction = backAction;
            InitializeComponent();
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            Load += (s, e) => LoadDetail();
        }

        /// <summary>Builds the detail page layout.</summary>
        private void BuildLayout()
        {
            Controls.Clear();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = PageBg, Padding = new Padding(24, 18, 24, 18) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildHeader(), 0, 0);
            _health = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = PageBg, Padding = new Padding(0, 8, 0, 12) };
            root.Controls.Add(_health, 0, 1);
            root.Controls.Add(BuildTabs(), 0, 2);
            Controls.Add(root);
        }

        /// <summary>Builds the page header.</summary>
        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Button back = MakeButton("< Back to AMC", Color.White, Ink, 128);
            back.Location = new Point(0, 2);
            back.FlatAppearance.BorderSize = 1;
            back.FlatAppearance.BorderColor = DS.Border;
            back.Click += (s, e) => _backAction?.Invoke();
            header.Controls.Add(back);

            _number = new Label { Text = "AMC", Location = new Point(0, 38), Size = new Size(220, 32), Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = Ink, AutoEllipsis = true };
            _client = new Label { Text = "-", Location = new Point(230, 44), Size = new Size(260, 24), Font = new Font("Segoe UI", 10f), ForeColor = Muted, AutoEllipsis = true };
            _status = MakeBadge("-", Grey, new Point(500, 44), 112);
            _coverage = MakeBadge("-", Blue, new Point(620, 44), 150);
            Button edit = MakeButton("Edit AMC", Blue, Color.White, 104);
            edit.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            edit.Location = new Point(header.Width - 104, 36);
            edit.Click += (s, e) => MessageBox.Show("Edit coming soon.", BrandingService.WindowTitle("AMC"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            header.Controls.Add(_number);
            header.Controls.Add(_client);
            header.Controls.Add(_status);
            header.Controls.Add(_coverage);
            header.Controls.Add(edit);
            header.Resize += (s, e) =>
            {
                edit.Location = new Point(Math.Max(0, header.ClientSize.Width - 104), 36);
                _client.Width = Math.Max(160, _status.Left - _client.Left - 10);
            };
            return header;
        }

        /// <summary>Builds the tabbed detail body.</summary>
        private Control BuildTabs()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            tabs.TabPages.Add(BuildEquipmentTab());
            tabs.TabPages.Add(BuildVisitsTab());
            tabs.TabPages.Add(BuildHistoryTab());
            return tabs;
        }

        /// <summary>Builds equipment register tab.</summary>
        private TabPage BuildEquipmentTab()
        {
            var tab = new TabPage("Equipment") { BackColor = Color.White };
            var header = BuildTabHeader("Equipment Covered", "+ Add Equipment");
            ((Button)header.Controls[1]).Click += (s, e) => AddEquipment();
            _equipmentGrid = MakeGrid();
            _equipmentGrid.Columns.Add("EquipmentName", "Equipment Name");
            _equipmentGrid.Columns.Add("ModelNumber", "Model Number");
            _equipmentGrid.Columns.Add("SerialNumber", "Serial Number");
            _equipmentGrid.Columns.Add("InstallDate", "Install Date");
            _equipmentGrid.Columns.Add("Location", "Location");
            _equipmentGrid.Columns.Add("Notes", "Notes");
            tab.Controls.Add(_equipmentGrid);
            tab.Controls.Add(header);
            LayoutTab(tab, header, _equipmentGrid);
            return tab;
        }

        /// <summary>Builds visit schedule tab.</summary>
        private TabPage BuildVisitsTab()
        {
            var tab = new TabPage("Visit Schedule") { BackColor = Color.White };
            var header = BuildTabHeader("Service Visit Schedule", "+ Mark Complete");
            _markComplete = (Button)header.Controls[1];
            _markComplete.Enabled = false;
            _markComplete.Click += (s, e) => MarkSelectedVisitComplete();
            _visitsGrid = MakeGrid();
            _visitsGrid.SelectionChanged += (s, e) => UpdateMarkCompleteState();
            _visitsGrid.Columns.Add("VisitID", "VisitID");
            _visitsGrid.Columns["VisitID"].Visible = false;
            _visitsGrid.Columns.Add("VisitNumber", "Visit #");
            _visitsGrid.Columns.Add("ScheduledDate", "Scheduled Date");
            _visitsGrid.Columns.Add("CompletedDate", "Completed Date");
            _visitsGrid.Columns.Add("TechnicianName", "Technician");
            _visitsGrid.Columns.Add("Status", "Status");
            _visitsGrid.Columns.Add("WorkDone", "Work Done");
            tab.Controls.Add(_visitsGrid);
            tab.Controls.Add(header);
            LayoutTab(tab, header, _visitsGrid);
            return tab;
        }

        /// <summary>Builds service history tab.</summary>
        private TabPage BuildHistoryTab()
        {
            var tab = new TabPage("Service History") { BackColor = Color.White };
            var title = new Label { Text = "Service History", Location = new Point(16, 14), Size = new Size(260, 28), Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Ink };
            _historyGrid = MakeGrid();
            _historyGrid.Columns.Add("VisitNumber", "Visit #");
            _historyGrid.Columns.Add("CompletedDate", "Completed Date");
            _historyGrid.Columns.Add("TechnicianName", "Technician");
            _historyGrid.Columns.Add("WorkDone", "Work Done");
            _historyGrid.Columns.Add("PartsUsed", "Parts Used");
            _historyEmpty = new Label { Text = "No completed visits yet.", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Muted, Font = new Font("Segoe UI", 11f), Visible = false };
            tab.Controls.Add(_historyGrid);
            tab.Controls.Add(_historyEmpty);
            tab.Controls.Add(title);
            tab.Resize += (s, e) =>
            {
                _historyGrid.Bounds = new Rectangle(16, 54, tab.ClientSize.Width - 32, tab.ClientSize.Height - 70);
                _historyEmpty.Bounds = _historyGrid.Bounds;
            };
            return tab;
        }

        /// <summary>Builds a tab header with an action button.</summary>
        private Panel BuildTabHeader(string title, string buttonText)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White };
            header.Controls.Add(new Label { Text = title, Location = new Point(16, 14), Size = new Size(280, 28), Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Ink });
            Button action = MakeButton(buttonText, Blue, Color.White, 140);
            action.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(action);
            header.Resize += (s, e) => action.Location = new Point(header.ClientSize.Width - action.Width - 16, 10);
            return header;
        }

        /// <summary>Applies standard tab layout.</summary>
        private void LayoutTab(TabPage tab, Control header, Control grid)
        {
            tab.Resize += (s, e) => grid.Bounds = new Rectangle(16, 58, tab.ClientSize.Width - 32, tab.ClientSize.Height - 74);
        }

        /// <summary>Starts loading all AMC detail data.</summary>
        private void LoadDetail()
        {
            var worker = CreateWorker();
            worker.DoWork += (s, e) => e.Result = LoadPayload();
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    ShowError( "Failed to load AMC detail. Please try again.", e.Error);
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("AMC"), "Loading AMC detail", e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(() =>
                {
                    BindPayload(e.Result as DetailPayload);
                });
            };
            worker.RunWorkerAsync();
        }

        /// <summary>Loads header, equipment, visit, and history data sequentially.</summary>
        private DetailPayload LoadPayload()
        {
            var payload = new DetailPayload();
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AMCDetailPage.LoadPayload");
                LoadHeader(connection, payload);
                if (!payload.NotFound)
                {
                    LoadEquipment(connection, payload);
                    LoadVisits(connection, payload);
                }
            }

            return payload;
        }

        /// <summary>Loads AMC header data.</summary>
        private void LoadHeader(SqlConnection connection, DetailPayload payload)
        {
            if (_amcId <= 0)
            {
                payload.NotFound = true;
                return;
            }

            try
            {
                using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1
    c.AMCNumber,
    ClientName = b.CompanyName,
    Status = ISNULL(c.Status, c.ContractStatus),
    CoverageType = ISNULL(c.CoverageType, c.AMCType),
    c.StartDate,
    c.EndDate,
    ContractValue = CASE WHEN ISNULL(c.ContractValue, 0) > 0 THEN c.ContractValue ELSE ISNULL(c.AnnualValue, 0) END,
    c.VisitsPerYear
FROM AMCContracts c
LEFT JOIN B2BClients b ON b.ClientID = c.ClientID
WHERE c.ContractID = @AMCID;", connection))
                {
                    command.Parameters.AddWithValue("@AMCID", _amcId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            payload.NotFound = true;
                            return;
                        }

                        payload.AMCNumber = ReadString(reader, "AMCNumber");
                        payload.ClientName = ReadString(reader, "ClientName");
                        payload.Status = DisplayStatus(ReadString(reader, "Status"), ReadDate(reader, "EndDate"));
                        payload.CoverageType = ReadString(reader, "CoverageType");
                        payload.StartDate = ReadDate(reader, "StartDate");
                        payload.EndDate = ReadDate(reader, "EndDate");
                        payload.ContractValue = ReadDecimal(reader, "ContractValue");
                        payload.VisitsPerYear = ReadInt(reader, "VisitsPerYear");
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                AppLogger.LogInfo("AMC detail header load failed for contract " + _amcId + ": " + ex.Message);
                payload.NotFound = true;
            }
        }

        /// <summary>Loads equipment rows.</summary>
        private void LoadEquipment(SqlConnection connection, DetailPayload payload)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT EquipmentName, ModelNumber, SerialNumber, InstallDate, Location, Notes
FROM AMCEquipment
WHERE AMCID = @AMCID
ORDER BY EquipmentID;", connection))
            {
                command.Parameters.AddWithValue("@AMCID", _amcId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        payload.Equipment.Add(new EquipmentRow
                        {
                            EquipmentName = ReadString(reader, "EquipmentName"),
                            ModelNumber = ReadString(reader, "ModelNumber"),
                            SerialNumber = ReadString(reader, "SerialNumber"),
                            InstallDate = ReadDate(reader, "InstallDate"),
                            Location = ReadString(reader, "Location"),
                            Notes = ReadString(reader, "Notes")
                        });
                    }
                }
            }
        }

        /// <summary>Loads visit rows.</summary>
        private void LoadVisits(SqlConnection connection, DetailPayload payload)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT VisitID, VisitNumber, ScheduledDate, CompletedDate, TechnicianName, Status, WorkDone, PartsUsed
FROM AMCVisits
WHERE AMCID = @AMCID
ORDER BY VisitNumber, ScheduledDate;", connection))
            {
                command.Parameters.AddWithValue("@AMCID", _amcId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var visit = new VisitRow
                        {
                            VisitID = ReadInt(reader, "VisitID"),
                            VisitNumber = ReadInt(reader, "VisitNumber"),
                            ScheduledDate = ReadDate(reader, "ScheduledDate"),
                            CompletedDate = ReadDate(reader, "CompletedDate"),
                            TechnicianName = ReadString(reader, "TechnicianName"),
                            Status = ReadString(reader, "Status"),
                            WorkDone = ReadString(reader, "WorkDone"),
                            PartsUsed = ReadString(reader, "PartsUsed")
                        };
                        payload.Visits.Add(visit);
                        if (string.Equals(visit.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                            payload.History.Add(visit);
                    }
                }
            }
        }

        /// <summary>Binds loaded detail data to the page.</summary>
        private void BindPayload(DetailPayload payload)
        {
            _payload = payload ?? new DetailPayload();
            if (_payload.NotFound)
            {
                if (_backAction != null)
                {
                    _backAction.Invoke();
                    return;
                }

                ShowNotFoundState();
                return;
            }

            _number.Text = string.IsNullOrWhiteSpace(_payload.AMCNumber) ? "AMC-" + _amcId.ToString("000", CultureInfo.InvariantCulture) : _payload.AMCNumber;
            _client.Text = _payload.ClientName;
            SetBadge(_status, _payload.Status, GetStatusColor(_payload.Status));
            SetBadge(_coverage, _payload.CoverageType, GetCoverageColor(_payload.CoverageType));
            BindHealth();
            BindEquipment();
            BindVisits();
            BindHistory();
        }

        /// <summary>Shows a non-modal stale-contract state for direct smoke/test construction.</summary>
        private void ShowNotFoundState()
        {
            Controls.Clear();
            var empty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "AMC contract is no longer available.",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Muted,
                BackColor = PageBg
            };
            Controls.Add(empty);
        }

        /// <summary>Binds the AMC health strip.</summary>
        private void BindHealth()
        {
            _health.Controls.Clear();
            AddHealthTile("Contract Period", FormatDateLong(_payload.StartDate) + " -> " + FormatDateLong(_payload.EndDate), false);
            AddHealthTile("Contract Value", _payload.ContractValue.ToString("C0", _india), false);
            int completed = _payload.Visits.Count(v => v.CompletedDate.HasValue || string.Equals(v.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            AddHealthTile("Visits Completed", completed.ToString(CultureInfo.InvariantCulture) + " of " + Math.Max(_payload.VisitsPerYear, _payload.Visits.Count).ToString(CultureInfo.InvariantCulture), false);
            DateTime? next = _payload.Visits.Where(v => string.Equals(v.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) || string.Equals(v.Status, "Rescheduled", StringComparison.OrdinalIgnoreCase)).Select(v => v.ScheduledDate).Where(d => d.HasValue).Select(d => d.Value.Date).DefaultIfEmpty(DateTime.MinValue).Min();
            bool hasNext = next.HasValue && next.Value != DateTime.MinValue;
            bool overdue = hasNext && next.Value.Date < DateTime.Today;
            AddHealthTile("Next Service Due", hasNext ? (overdue ? "Overdue" : next.Value.ToString("dd MMM yyyy", _india)) : "-", overdue);
        }

        /// <summary>Adds one health tile.</summary>
        private void AddHealthTile(string title, string value, bool alert)
        {
            var card = new Panel { Size = new Size(220, 84), BackColor = alert ? Color.FromArgb(254, 226, 226) : Color.White, Margin = new Padding(0, 0, 14, 0) };
            card.Paint += (s, e) => { using (var pen = new Pen(DS.Border)) e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1); };
            DS.Rounded(card, 8);
            card.Controls.Add(new Label { Text = title, Location = new Point(14, 12), Size = new Size(190, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Muted });
            card.Controls.Add(new Label { Text = value, Location = new Point(14, 38), Size = new Size(192, 28), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = alert ? Red : Ink });
            _health.Controls.Add(card);
        }

        /// <summary>Binds equipment grid.</summary>
        private void BindEquipment()
        {
            _equipmentGrid.Rows.Clear();
            foreach (EquipmentRow row in _payload.Equipment)
                _equipmentGrid.Rows.Add(row.EquipmentName, row.ModelNumber, row.SerialNumber, FormatDate(row.InstallDate), row.Location, row.Notes);
        }

        /// <summary>Binds visits grid.</summary>
        private void BindVisits()
        {
            _visitsGrid.Rows.Clear();
            foreach (VisitRow row in _payload.Visits)
            {
                int index = _visitsGrid.Rows.Add(row.VisitID, row.VisitNumber, FormatDate(row.ScheduledDate), FormatDate(row.CompletedDate), row.TechnicianName, row.Status, Truncate(row.WorkDone, 60));
                _visitsGrid.Rows[index].Cells["Status"].Style.ForeColor = GetStatusColor(row.Status);
            }
            UpdateMarkCompleteState();
        }

        /// <summary>Binds service history grid and empty state.</summary>
        private void BindHistory()
        {
            _historyGrid.Rows.Clear();
            foreach (VisitRow row in _payload.History)
                _historyGrid.Rows.Add(row.VisitNumber, FormatDate(row.CompletedDate), row.TechnicianName, row.WorkDone, row.PartsUsed);
            _historyEmpty.Visible = _payload.History.Count == 0;
            _historyGrid.Visible = _payload.History.Count > 0;
        }

        /// <summary>Opens add equipment dialog and reloads data after save.</summary>
        private void AddEquipment()
        {
            using (var form = new AddAMCEquipmentForm(_amcId))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    LoadDetail();
            }
        }

        /// <summary>Marks the selected scheduled visit complete.</summary>
        private void MarkSelectedVisitComplete()
        {
            int visitId = GetSelectedVisitId();
            if (visitId <= 0)
                return;

            using (var form = new MarkVisitCompleteForm(visitId))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    LoadDetail();
            }
        }

        /// <summary>Updates the mark complete button state.</summary>
        private void UpdateMarkCompleteState()
        {
            if (_markComplete == null)
                return;

            string status = GetSelectedVisitStatus();
            _markComplete.Enabled = string.Equals(status, "Scheduled", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Rescheduled", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns selected visit id.</summary>
        private int GetSelectedVisitId()
        {
            if (_visitsGrid.CurrentRow == null)
                return 0;

            object value = _visitsGrid.CurrentRow.Cells["VisitID"].Value;
            return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Returns selected visit status.</summary>
        private string GetSelectedVisitStatus()
        {
            if (_visitsGrid.CurrentRow == null)
                return string.Empty;

            object value = _visitsGrid.CurrentRow.Cells["Status"].Value;
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Creates a standard read-only grid.</summary>
        private DataGridView MakeGrid()
        {
            var grid = new DataGridView { ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            GridTheme.Apply(grid);
            return grid;
        }

        /// <summary>Creates a compact button.</summary>
        private Button MakeButton(string text, Color back, Color fore, int width)
        {
            var button = new Button { Text = text, Width = width, Height = 34, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            DS.Rounded(button, 6);
            return button;
        }

        /// <summary>Creates badge label.</summary>
        private Label MakeBadge(string text, Color color, Point location, int width)
        {
            var label = new Label { Text = text, Location = location, Size = new Size(width, 24), BackColor = color, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            DS.Rounded(label, 12);
            return label;
        }

        /// <summary>Updates a badge label.</summary>
        private void SetBadge(Label label, string text, Color color)
        {
            label.Text = string.IsNullOrWhiteSpace(text) ? "-" : text;
            label.BackColor = color;
        }

        /// <summary>Returns display status.</summary>
        private string DisplayStatus(string status, DateTime? endDate)
        {
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)) return "Cancelled";
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return "Draft";
            if (endDate.HasValue && endDate.Value.Date < DateTime.Today) return "Expired";
            if (endDate.HasValue && endDate.Value.Date <= DateTime.Today.AddDays(30)) return "Expiring Soon";
            return string.IsNullOrWhiteSpace(status) ? "Active" : status;
        }

        /// <summary>Maps status to colour.</summary>
        private Color GetStatusColor(string value)
        {
            if (string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase)) return Green;
            if (string.Equals(value, "Expiring Soon", StringComparison.OrdinalIgnoreCase)) return Amber;
            if (string.Equals(value, "Expired", StringComparison.OrdinalIgnoreCase)) return Red;
            if (string.Equals(value, "Completed", StringComparison.OrdinalIgnoreCase)) return Green;
            if (string.Equals(value, "Scheduled", StringComparison.OrdinalIgnoreCase)) return Blue;
            if (string.Equals(value, "Rescheduled", StringComparison.OrdinalIgnoreCase)) return Amber;
            if (string.Equals(value, "Missed", StringComparison.OrdinalIgnoreCase)) return Red;
            if (string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase)) return DarkGrey;
            return Grey;
        }

        /// <summary>Maps coverage type to colour.</summary>
        private Color GetCoverageColor(string value)
        {
            return string.Equals(value, "Non-Comprehensive", StringComparison.OrdinalIgnoreCase) ? Amber : Blue;
        }

        /// <summary>Formats date as DD/MM/YYYY.</summary>
        private string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("dd/MM/yyyy", _india) : string.Empty;
        }

        /// <summary>Formats date as DD MMM YYYY.</summary>
        private string FormatDateLong(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("dd MMM yyyy", _india) : "-";
        }

        /// <summary>Truncates long grid text.</summary>
        private string Truncate(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= length)
                return value ?? string.Empty;
            return value.Substring(0, length) + "...";
        }

        private static int ReadInt(SqlDataReader reader, string name) { object value = reader[name]; return value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        private static decimal ReadDecimal(SqlDataReader reader, string name) { object value = reader[name]; return value == DBNull.Value ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
        private static string ReadString(SqlDataReader reader, string name) { object value = reader[name]; return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture); }
        private static DateTime? ReadDate(SqlDataReader reader, string name) { object value = reader[name]; return value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value, CultureInfo.InvariantCulture); }

        private sealed class DetailPayload
        {
            public bool NotFound;
            public string AMCNumber;
            public string ClientName;
            public string Status;
            public string CoverageType;
            public DateTime? StartDate;
            public DateTime? EndDate;
            public decimal ContractValue;
            public int VisitsPerYear;
            public readonly List<EquipmentRow> Equipment = new List<EquipmentRow>();
            public readonly List<VisitRow> Visits = new List<VisitRow>();
            public readonly List<VisitRow> History = new List<VisitRow>();
        }

        private sealed class EquipmentRow
        {
            public string EquipmentName;
            public string ModelNumber;
            public string SerialNumber;
            public DateTime? InstallDate;
            public string Location;
            public string Notes;
        }

        private sealed class VisitRow
        {
            public int VisitID;
            public int VisitNumber;
            public DateTime? ScheduledDate;
            public DateTime? CompletedDate;
            public string TechnicianName;
            public string Status;
            public string WorkDone;
            public string PartsUsed;
        }
    }
}



