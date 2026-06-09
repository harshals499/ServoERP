using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Form for creating or editing an AMC contract.</summary>
    public partial class AddAMCForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly CultureInfo _india = new CultureInfo("en-IN");
        private readonly int? _contractId;
        private int? _lastSavedContractId;
        private TextBox _txtAMCNumber;
        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private TextBox _txtEquipment;
        private ComboBox _cmbAMCType;
        private ComboBox _cmbCoverageType;
        private DateTimePicker _dtpStart;
        private DateTimePicker _dtpEnd;
        private NumericUpDown _numValue;
        private ComboBox _cmbBillingCycle;
        private NumericUpDown _numVisits;
        private ComboBox _cmbStatus;
        private TextBox _txtNotes;
        private Button _btnSave;
        private Button _btnRenew;
        private Label _summaryNumber;
        private Label _summaryClient;
        private Label _summaryType;
        private Label _summaryPeriod;
        private Label _summaryValue;
        private Label _summaryBilling;
        private Label _summaryVisits;
        private Label _summaryStatus;
        private Label _titleLabel;
        private bool _loadInProgress;
        private bool _saveInProgress;
        private bool _bindingReferenceData;
        private int _siteLoadRequestId;
        private AMCInput _loadedInput;
        private int? _pendingSiteId;
        private CancellationTokenSource _referenceLoadCancellation;

        private static readonly object ClientCacheLock = new object();
        private static readonly TimeSpan ClientCacheTtl = TimeSpan.FromMinutes(3);
        private const int ReferenceLoadTimeoutMilliseconds = 5000;
        private const int ReferenceCommandTimeoutSeconds = 5;
        private static DateTime _clientCacheLoadedUtc = DateTime.MinValue;
        private static List<LookupItem> _clientCache;

        private static readonly Color PageBg = Color.FromArgb(246, 248, 252);
        private static readonly Color Ink = Color.FromArgb(15, 23, 42);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(16, 185, 129);

        public AddAMCForm() : this(null)
        {
        }

        public AddAMCForm(int contractId) : this((int?)contractId)
        {
        }

        private AddAMCForm(int? contractId)
        {
            _contractId = contractId;
            InitializeComponent();
            DoubleBuffered = true;
            Text = _contractId.HasValue ? "Edit AMC Contract" : "New AMC Contract";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(980, 700);
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
            Shown += (s, e) =>
            {
                if (IsOffscreenSmokeHost())
                    return;

                BeginInvoke((Action)(() => _ = BeginLoadReferenceDataAsync()));
            };
            FormClosed += (s, e) => CancelReferenceLoad();
        }

        private bool IsOffscreenSmokeHost()
        {
            return !ShowInTaskbar && Location.X < -10000 && Location.Y < -10000;
        }

        /// <summary>Builds the two-panel AMC entry layout.</summary>
        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = PageBg,
                Padding = new Padding(22, 18, 22, 18)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            root.Controls.Add(BuildHeader(), 0, 0);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = PageBg };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            body.Controls.Add(BuildFormPanel(), 0, 0);
            body.Controls.Add(BuildSummaryPanel(), 1, 0);
            root.Controls.Add(body, 0, 1);
            root.Controls.Add(BuildButtonBar(), 0, 2);
            Controls.Add(root);
        }

        /// <summary>Creates the form header.</summary>
        private Control BuildHeader()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            _titleLabel = new Label
            {
                Text = _contractId.HasValue ? "Edit AMC Contract" : "New AMC Contract",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                ForeColor = Ink
            };
            panel.Controls.Add(_titleLabel);
            return panel;
        }

        /// <summary>Creates the left-side field panel.</summary>
        private Control BuildFormPanel()
        {
            Panel card = MakeCard();
            card.Margin = new Padding(0, 0, 14, 0);
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 13,
                Padding = new Padding(18),
                BackColor = Color.White
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 13; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 5 || i == 12 ? 68 : 40));

            _txtAMCNumber = new TextBox { MaxLength = 30, ReadOnly = false, Enabled = true };
            _cmbClient = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbSite = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _txtEquipment = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            _cmbAMCType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCoverageType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today };
            _dtpEnd = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddYears(1) };
            _numValue = new NumericUpDown { Minimum = 0, Maximum = 999999999, DecimalPlaces = 2, ThousandsSeparator = true };
            _cmbBillingCycle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _numVisits = new NumericUpDown { Minimum = 1, Maximum = 52, Value = 2 };
            _cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _txtNotes = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

            _cmbAMCType.Items.AddRange(new object[] { "Comprehensive", "Non-Comprehensive", "Labour Only", "Preventive" });
            _cmbCoverageType.Items.AddRange(new object[] { "Comprehensive", "Non-Comprehensive" });
            _cmbBillingCycle.Items.AddRange(new object[] { "Monthly", "Quarterly", "Half-Yearly", "Annual" });
            _cmbStatus.Items.AddRange(new object[] { "Draft", "Active", "Cancelled" });
            _cmbAMCType.SelectedIndex = 0;
            _cmbCoverageType.SelectedIndex = 0;
            _cmbBillingCycle.SelectedIndex = 3;
            _cmbStatus.SelectedIndex = 1;

            AddRow(grid, 0, "AMC Number", _txtAMCNumber);
            AddRow(grid, 1, "Client", _cmbClient);
            AddRow(grid, 2, "Site", _cmbSite);
            AddRow(grid, 3, "AMC Type", _cmbAMCType);
            AddRow(grid, 4, "Coverage Type", _cmbCoverageType);
            AddRow(grid, 5, "Equipment Covered", _txtEquipment);
            AddRow(grid, 6, "Start Date", _dtpStart);
            AddRow(grid, 7, "End Date", _dtpEnd);
            AddRow(grid, 8, "Contract Value (INR)", _numValue);
            AddRow(grid, 9, "Billing Cycle", _cmbBillingCycle);
            AddRow(grid, 10, "Visits Per Year", _numVisits);
            AddRow(grid, 11, "Status", _cmbStatus);
            AddRow(grid, 12, "Notes", _txtNotes);
            card.Controls.Add(grid);

            _cmbClient.SelectedIndexChanged += async (s, e) =>
            {
                if (_loadInProgress || _bindingReferenceData)
                {
                    UpdateSummary();
                    return;
                }

                await BeginLoadSitesAsync();
                UpdateSummary();
            };
            foreach (Control control in new Control[] { _txtAMCNumber, _cmbClient, _cmbSite, _txtEquipment, _cmbAMCType, _cmbCoverageType, _dtpStart, _dtpEnd, _numValue, _cmbBillingCycle, _numVisits, _cmbStatus, _txtNotes })
            {
                control.TextChanged += (s, e) => UpdateSummary();
                if (control is ComboBox combo) combo.SelectedIndexChanged += (s, e) => UpdateSummary();
                if (control is DateTimePicker picker) picker.ValueChanged += (s, e) => UpdateSummary();
                if (control is NumericUpDown numeric) numeric.ValueChanged += (s, e) => UpdateSummary();
            }

            UIHelper.ApplyInputStyles(card.Controls);
            return card;
        }

        /// <summary>Adds one label/control row to the form grid.</summary>
        private void AddRow(TableLayoutPanel grid, int row, string labelText, Control editor)
        {
            var label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Ink
            };
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 4, 0, 4);
            grid.Controls.Add(label, 0, row);
            grid.Controls.Add(editor, 1, row);
        }

        /// <summary>Creates the right-side live summary card.</summary>
        private Control BuildSummaryPanel()
        {
            Panel card = MakeCard();
            card.Controls.Add(new Label
            {
                Text = "AMC Summary",
                Location = new Point(18, 18),
                Size = new Size(240, 28),
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Ink
            });

            _summaryNumber = AddSummaryRow(card, 62, "AMC Number");
            _summaryClient = AddSummaryRow(card, 106, "Client");
            _summaryType = AddSummaryRow(card, 150, "AMC Type");
            _summaryPeriod = AddSummaryRow(card, 194, "Period");
            _summaryValue = AddSummaryRow(card, 254, "Contract Value");
            _summaryBilling = AddSummaryRow(card, 298, "Billing Cycle");
            _summaryVisits = AddSummaryRow(card, 342, "Visits/Year");
            _summaryStatus = AddSummaryRow(card, 386, "Status");
            return card;
        }

        /// <summary>Adds one row to the summary panel.</summary>
        private Label AddSummaryRow(Control parent, int top, string title)
        {
            parent.Controls.Add(new Label { Text = title, Location = new Point(18, top), Size = new Size(230, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Muted });
            var value = new Label { Text = "-", Location = new Point(18, top + 18), Size = new Size(280, 26), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Ink };
            parent.Controls.Add(value);
            return value;
        }

        /// <summary>Creates the bottom Save, Renew, and Cancel actions.</summary>
        private Control BuildButtonBar()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Button cancel = MakeButton("Cancel", Color.White, 100);
            cancel.ForeColor = Ink;
            cancel.FlatAppearance.BorderSize = 1;
            cancel.FlatAppearance.BorderColor = DS.Border;
            cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnRenew = MakeButton("Renew 1 Year", Green, 118);
            _btnRenew.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRenew.Visible = _contractId.HasValue;
            _btnRenew.Click += (s, e) => RenewOneYear();

            _btnSave = MakeButton(_contractId.HasValue ? "Save Changes" : "Save AMC", Blue, 126);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += (s, e) => SaveAMC();

            panel.Controls.Add(cancel);
            panel.Controls.Add(_btnRenew);
            panel.Controls.Add(_btnSave);
            panel.Resize += (s, e) =>
            {
                _btnSave.Location = new Point(panel.ClientSize.Width - _btnSave.Width, 12);
                _btnRenew.Location = new Point(_btnSave.Left - _btnRenew.Width - 10, 12);
                cancel.Location = new Point(_btnRenew.Left - cancel.Width - 10, 12);
            };
            return panel;
        }

        /// <summary>Starts loading clients, sites, next AMC number, and edit data.</summary>
        private async Task BeginLoadReferenceDataAsync()
        {
            if (_loadInProgress || IsDisposed)
                return;

            _loadInProgress = true;
            SetReferenceLoadingState(true);
            CancellationTokenSource previous = _referenceLoadCancellation;
            if (previous != null)
            {
                previous.Cancel();
                previous.Dispose();
            }

            _referenceLoadCancellation = new CancellationTokenSource();
            CancellationToken token = _referenceLoadCancellation.Token;
            try
            {
                Task<ReferencePayload> loadTask = Task.Run(() => LoadReferenceData(), token);
                Task completed = await Task.WhenAny(loadTask, Task.Delay(ReferenceLoadTimeoutMilliseconds, token));
                if (completed != loadTask)
                    throw new TimeoutException("AMC reference data load timed out.");

                ReferencePayload payload = await loadTask;
                if (!IsDisposed && !token.IsCancellationRequested)
                    BindReferenceData(payload);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException ex)
            {
                if (!IsDisposed)
                {
                    AppLogger.LogInfo("AddAMCForm reference load timed out: " + ex.Message);
                    SetReferenceLoadFailedState("AMC setup is taking too long. Check SQL Server and reopen Add AMC.");
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    AppLogger.LogInfo("AddAMCForm reference load failed: " + ex.GetType().Name + ": " + ex.Message);
                    SetReferenceLoadFailedState("AMC setup could not load. Check SQL Server and reopen Add AMC.");
                }
            }
            finally
            {
                if (!IsDisposed)
                    SetReferenceLoadingState(false);
                _loadInProgress = false;
            }
        }

        /// <summary>Gets the contract id created or updated during the last successful save.</summary>
        public int? LastSavedContractId
        {
            get { return _lastSavedContractId; }
        }

        private void CancelReferenceLoad()
        {
            CancellationTokenSource current = _referenceLoadCancellation;
            _referenceLoadCancellation = null;
            if (current == null)
                return;

            current.Cancel();
            current.Dispose();
        }

        private void SetReferenceLoadingState(bool isLoading)
        {
            if (_cmbClient == null || _cmbSite == null)
                return;

            _cmbClient.Enabled = !isLoading;
            _cmbSite.Enabled = !isLoading;
            _btnSave.Enabled = !isLoading;
            if (isLoading)
            {
                _cmbClient.DataSource = null;
                _cmbClient.Items.Clear();
                _cmbClient.Items.Add("Loading clients...");
                _cmbClient.SelectedIndex = 0;
                _cmbSite.DataSource = null;
                _cmbSite.Items.Clear();
                _cmbSite.Items.Add("Loading sites...");
                _cmbSite.SelectedIndex = 0;
            }
        }

        private void SetReferenceLoadFailedState(string message)
        {
            if (_cmbClient == null || _cmbSite == null || _btnSave == null)
                return;

            _cmbClient.DataSource = null;
            _cmbClient.Items.Clear();
            _cmbClient.Items.Add(message);
            _cmbClient.SelectedIndex = 0;
            _cmbClient.Enabled = false;
            _cmbSite.DataSource = null;
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add("Sites unavailable");
            _cmbSite.SelectedIndex = 0;
            _cmbSite.Enabled = false;
            _btnSave.Enabled = false;
        }

        /// <summary>Loads the next AMC number, active clients, and existing AMC data when editing.</summary>
        private ReferencePayload LoadReferenceData()
        {
            var payload = new ReferencePayload();
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AddAMCForm.LoadReferenceData");
                using (SqlCommand command = new SqlCommand("SELECT TOP 1 AMCNumber FROM AMCContracts WHERE AMCNumber IS NOT NULL ORDER BY ContractID DESC;", connection))
                {
                    command.CommandTimeout = ReferenceCommandTimeoutSeconds;
                    payload.NextAMCNumber = GenerateNextAMCNumber(command.ExecuteScalar() as string);
                }

                payload.Clients.AddRange(GetCachedClients(connection));

                if (_contractId.HasValue)
                {
                    payload.Existing = LoadExisting(connection, _contractId.Value);
                    if (payload.Existing == null)
                        AppLogger.LogInfo("AddAMCForm loaded in edit mode but contract " + _contractId.Value + " was not found. Opening in add mode.");
                }
            }

            return payload;
        }

        /// <summary>Returns active clients from a short-lived cache so opening Add AMC stays instant.</summary>
        private static List<LookupItem> GetCachedClients(SqlConnection connection)
        {
            lock (ClientCacheLock)
            {
                if (_clientCache != null && DateTime.UtcNow - _clientCacheLoadedUtc < ClientCacheTtl)
                    return CloneLookupItems(_clientCache);
            }

            var clients = new List<LookupItem>();
            using (SqlCommand command = new SqlCommand("SELECT ClientID, CompanyName FROM B2BClients WHERE ISNULL(IsActive, 1) = 1 ORDER BY CompanyName;", connection))
            {
                command.CommandTimeout = ReferenceCommandTimeoutSeconds;
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        clients.Add(new LookupItem(ReadInt(reader, "ClientID"), ReadString(reader, "CompanyName")));
                }
            }

            lock (ClientCacheLock)
            {
                _clientCache = CloneLookupItems(clients);
                _clientCacheLoadedUtc = DateTime.UtcNow;
            }

            return clients;
        }

        private static List<LookupItem> CloneLookupItems(List<LookupItem> source)
        {
            var clone = new List<LookupItem>();
            if (source == null)
                return clone;

            foreach (LookupItem item in source)
                clone.Add(new LookupItem(item.Id, item.Text));
            return clone;
        }

        /// <summary>Loads one existing AMC record for edit mode.</summary>
        private AMCInput LoadExisting(SqlConnection connection, int contractId)
        {
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1
    ContractID,
    AMCNumber,
    ClientID,
    SiteID,
    EquipmentDesc,
    AMCType = ISNULL(AMCType, ContractType),
    StartDate,
    EndDate,
    ContractValue = CASE WHEN ISNULL(ContractValue, 0) > 0 THEN ContractValue ELSE ISNULL(AnnualValue, 0) END,
    BillingCycle = ISNULL(BillingCycle, MaintenanceFrequency),
    CoverageType,
    VisitsPerYear,
    Status = ISNULL(Status, ContractStatus),
    Notes
FROM AMCContracts
WHERE ContractID = @ContractID;", connection))
            {
                command.CommandTimeout = ReferenceCommandTimeoutSeconds;
                command.Parameters.AddWithValue("@ContractID", contractId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return new AMCInput
                    {
                        ContractId = ReadInt(reader, "ContractID"),
                        AMCNumber = ReadString(reader, "AMCNumber"),
                        ClientId = ReadInt(reader, "ClientID"),
                        SiteId = ReadNullableInt(reader, "SiteID"),
                        EquipmentDesc = ReadString(reader, "EquipmentDesc"),
                        AMCType = ReadString(reader, "AMCType"),
                        StartDate = ReadDateValue(reader, "StartDate", DateTime.Today),
                        EndDate = ReadDateValue(reader, "EndDate", DateTime.Today.AddYears(1)),
                        ContractValue = ReadDecimal(reader, "ContractValue"),
                        BillingCycle = ReadString(reader, "BillingCycle"),
                        CoverageType = ReadString(reader, "CoverageType"),
                        VisitsPerYear = Math.Max(1, ReadInt(reader, "VisitsPerYear")),
                        Status = ReadString(reader, "Status"),
                        Notes = ReadString(reader, "Notes")
                    };
                }
            }
        }

        /// <summary>Binds loaded clients, next AMC number, and edit data to controls.</summary>
        private void BindReferenceData(ReferencePayload payload)
        {
            if (payload == null)
                payload = new ReferencePayload();

            _bindingReferenceData = true;
            try
            {
                _loadedInput = payload.Existing;
                _txtAMCNumber.Text = _loadedInput == null || string.IsNullOrWhiteSpace(_loadedInput.AMCNumber)
                    ? payload.NextAMCNumber
                    : _loadedInput.AMCNumber;
                _cmbClient.DataSource = payload.Clients;
                _cmbClient.DisplayMember = "Text";
                _cmbClient.ValueMember = "Id";

                if (_loadedInput != null)
                {
                    SelectLookup(_cmbClient, _loadedInput.ClientId);
                    _pendingSiteId = _loadedInput.SiteId;
                    SelectText(_cmbAMCType, _loadedInput.AMCType);
                    SelectText(_cmbCoverageType, string.IsNullOrWhiteSpace(_loadedInput.CoverageType) ? "Comprehensive" : _loadedInput.CoverageType);
                    _dtpStart.Value = _loadedInput.StartDate;
                    _dtpEnd.Value = _loadedInput.EndDate;
                    _numValue.Value = Math.Max(_numValue.Minimum, Math.Min(_numValue.Maximum, _loadedInput.ContractValue));
                    SelectText(_cmbBillingCycle, string.IsNullOrWhiteSpace(_loadedInput.BillingCycle) ? "Annual" : _loadedInput.BillingCycle);
                    _numVisits.Value = Math.Max(_numVisits.Minimum, Math.Min(_numVisits.Maximum, _loadedInput.VisitsPerYear));
                    SelectText(_cmbStatus, string.IsNullOrWhiteSpace(_loadedInput.Status) ? "Active" : NormalizeEditableStatus(_loadedInput.Status));
                    _txtEquipment.Text = _loadedInput.EquipmentDesc ?? string.Empty;
                    _txtNotes.Text = _loadedInput.Notes ?? string.Empty;
                }
                else if (_cmbClient.Items.Count > 0)
                {
                    _cmbClient.SelectedIndex = 0;
                }
            }
            finally
            {
                _bindingReferenceData = false;
            }

            _ = BeginLoadSitesAsync();
            UpdateSummary();
        }

        /// <summary>Loads sites for the selected client asynchronously.</summary>
        private async Task BeginLoadSitesAsync()
        {
            int clientId = GetSelectedClientId();
            int requestId = ++_siteLoadRequestId;
            try
            {
                _cmbSite.Enabled = false;
                List<LookupItem> sites = await Task.Run(() => LoadSites(clientId)) ?? new List<LookupItem>();
                if (requestId != _siteLoadRequestId || IsDisposed)
                    return;

                sites.Insert(0, new LookupItem(0, "-- No site --"));
                _cmbSite.DataSource = sites;
                _cmbSite.DisplayMember = "Text";
                _cmbSite.ValueMember = "Id";
                SelectLookup(_cmbSite, _pendingSiteId ?? 0);
                _pendingSiteId = null;
                UpdateSummary();
            }
            catch (Exception ex)
            {
                ShowError("Failed to load client sites. Please try again.", ex);
            }
            finally
            {
                if (!IsDisposed && requestId == _siteLoadRequestId)
                    _cmbSite.Enabled = true;
            }
        }

        /// <summary>Loads client sites from SQL Server.</summary>
        private List<LookupItem> LoadSites(int clientId)
        {
            var sites = new List<LookupItem>();
            if (clientId <= 0)
                return sites;

            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(connection, "AddAMCForm.LoadSites");
                using (SqlCommand command = new SqlCommand("SELECT SiteID, SiteName FROM ClientSites WHERE ClientID = @ClientID ORDER BY SiteName;", connection))
                {
                    command.CommandTimeout = ReferenceCommandTimeoutSeconds;
                    command.Parameters.AddWithValue("@ClientID", clientId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            sites.Add(new LookupItem(ReadInt(reader, "SiteID"), ReadString(reader, "SiteName")));
                    }
                }
            }

            return sites;
        }

        /// <summary>Generates the next AMC number in AMC-YYYY-NNN format.</summary>
        private string GenerateNextAMCNumber(string lastNumber)
        {
            int year = DateTime.Today.Year;
            int next = 1;
            if (!string.IsNullOrWhiteSpace(lastNumber))
            {
                string[] parts = lastNumber.Split('-');
                int parsedYear;
                int parsedSequence;
                if (parts.Length == 3 && int.TryParse(parts[1], out parsedYear) && parsedYear == year && int.TryParse(parts[2], out parsedSequence))
                    next = parsedSequence + 1;
            }

            return "AMC-" + year.ToString(CultureInfo.InvariantCulture) + "-" + next.ToString("000", CultureInfo.InvariantCulture);
        }

        /// <summary>Moves the AMC period forward by one year from the current end date.</summary>
        private void RenewOneYear()
        {
            DateTime nextStart = _dtpEnd.Value.Date.AddDays(1);
            _dtpStart.Value = nextStart;
            _dtpEnd.Value = nextStart.AddYears(1).AddDays(-1);
            SelectText(_cmbStatus, "Active");
            UpdateSummary();
        }

        /// <summary>Validates and starts saving the AMC record.</summary>
        private async void SaveAMC()
        {
            if (_saveInProgress)
                return;

            string validation = ValidateForm();
            if (!string.IsNullOrWhiteSpace(validation))
            {
                MessageBox.Show(validation, BrandingService.WindowTitle("AMC Validation"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AMCInput input = BuildInput();
            _btnSave.Enabled = false;
            _btnRenew.Enabled = false;
            _btnSave.Text = "Saving...";
            _saveInProgress = true;
            try
            {
                _lastSavedContractId = null;
                int savedContractId = await Task.Run(() => SaveInput(input));
                _lastSavedContractId = savedContractId;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (DuplicateAMCNumberException ex)
            {
                MessageBox.Show(this, ex.Message, BrandingService.WindowTitle("Duplicate AMC Number"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtAMCNumber.Focus();
            }
            catch (Exception ex)
            {
                ShowError("AMC could not be saved. Please check the details and try again.", ex);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _btnSave.Enabled = true;
                    _btnRenew.Enabled = true;
                    _btnSave.Text = _contractId.HasValue ? "Save Changes" : "Save AMC";
                }
                _saveInProgress = false;
            }
        }

        /// <summary>Returns validation text or an empty string when the form is valid.</summary>
        private string ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(_txtAMCNumber.Text))
                return "Enter an AMC Number.";
            if (_txtAMCNumber.Text.Trim().Length > 30)
                return "AMC Number must be 30 characters or fewer.";
            if (GetSelectedClientId() <= 0)
                return "Select a client.";
            if (_dtpEnd.Value.Date <= _dtpStart.Value.Date)
                return "End Date must be after Start Date.";
            if (_numValue.Value < 0)
                return "Contract Value must be zero or higher.";
            if (_cmbAMCType.SelectedItem == null)
                return "Select an AMC Type.";
            return string.Empty;
        }

        /// <summary>Builds a save payload from current UI values.</summary>
        private AMCInput BuildInput()
        {
            return new AMCInput
            {
                ContractId = _contractId ?? 0,
                AMCNumber = _txtAMCNumber.Text.Trim(),
                ClientId = GetSelectedClientId(),
                SiteId = GetSelectedSiteId(),
                EquipmentDesc = _txtEquipment.Text.Trim(),
                AMCType = Convert.ToString(_cmbAMCType.SelectedItem, CultureInfo.InvariantCulture),
                CoverageType = Convert.ToString(_cmbCoverageType.SelectedItem, CultureInfo.InvariantCulture),
                StartDate = _dtpStart.Value.Date,
                EndDate = _dtpEnd.Value.Date,
                ContractValue = _numValue.Value,
                BillingCycle = Convert.ToString(_cmbBillingCycle.SelectedItem, CultureInfo.InvariantCulture),
                VisitsPerYear = (int)_numVisits.Value,
                Status = Convert.ToString(_cmbStatus.SelectedItem, CultureInfo.InvariantCulture),
                Notes = _txtNotes.Text.Trim()
            };
        }

        /// <summary>Inserts or updates the AMC record using parameterised SQL.</summary>
        private int SaveInput(AMCInput input)
        {
            DbHelper.EnsureAMCSchema();
            EnsureUniqueAMCNumber(input);
            if (_contractId.HasValue)
            {
                bool updated = UpdateInput(input);
                if (updated)
                    return input.ContractId;

                AppLogger.LogInfo("AMC contract " + _contractId.Value + " was not found. Saving as a new contract.");
                input.ContractId = 0;
                int replacementContractId = InsertInput(input);
                AMCVisitScheduler.GenerateVisitSchedule(replacementContractId, input.StartDate, input.EndDate, input.VisitsPerYear);
                return replacementContractId;
            }

            int contractId = InsertInput(input);
            AMCVisitScheduler.GenerateVisitSchedule(contractId, input.StartDate, input.EndDate, input.VisitsPerYear);
            return contractId;
        }

        /// <summary>Inserts a new AMC record using parameterised SQL.</summary>
        private int InsertInput(AMCInput input)
        {
            try
            {
                using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
                using (SqlCommand command = new SqlCommand(@"
INSERT INTO AMCContracts
    (AMCNumber, ClientID, SiteID, EquipmentDesc, AMCType, StartDate, EndDate,
     ContractValue, BillingCycle, CoverageType, VisitsPerYear, Status, Notes, CreatedAt, UpdatedAt,
     MonthlyValue, AnnualValue, ContractStatus, MaintenanceFrequency, ContractType)
VALUES
    (@AMCNumber, @ClientID, @SiteID, @EquipmentDesc, @AMCType, @StartDate, @EndDate,
     @ContractValue, @BillingCycle, @CoverageType, @VisitsPerYear, @Status, @Notes, GETDATE(), GETDATE(),
     @MonthlyValue, @AnnualValue, @ContractStatus, @MaintenanceFrequency, @ContractType);
SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                AddSaveParameters(command, input);
                DatabaseConnectionFactory.Open(connection, "AddAMCForm.InsertInput");
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            }
            catch (SqlException ex)
            {
                if (IsUniqueViolation(ex))
                    throw new DuplicateAMCNumberException(input.AMCNumber);
                throw;
            }
        }

        /// <summary>Updates an existing AMC record using parameterised SQL.</summary>
        private bool UpdateInput(AMCInput input)
        {
            try
            {
                using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
                using (SqlCommand command = new SqlCommand(@"
UPDATE AMCContracts
SET AMCNumber = @AMCNumber,
    ClientID = @ClientID,
    SiteID = @SiteID,
    EquipmentDesc = @EquipmentDesc,
    AMCType = @AMCType,
    StartDate = @StartDate,
    EndDate = @EndDate,
    ContractValue = @ContractValue,
    BillingCycle = @BillingCycle,
    CoverageType = @CoverageType,
    VisitsPerYear = @VisitsPerYear,
    Status = @Status,
    Notes = @Notes,
    UpdatedAt = GETDATE(),
    MonthlyValue = @MonthlyValue,
    AnnualValue = @AnnualValue,
    ContractStatus = @ContractStatus,
    MaintenanceFrequency = @MaintenanceFrequency,
    ContractType = @ContractType,
    ModifiedDate = GETDATE()
WHERE ContractID = @ContractID;", connection))
                {
                    AddSaveParameters(command, input);
                    command.Parameters.AddWithValue("@ContractID", input.ContractId);
                    DatabaseConnectionFactory.Open(connection, "AddAMCForm.UpdateInput");
                    return command.ExecuteNonQuery() > 0;
            }
            }
            catch (SqlException ex)
            {
                if (IsUniqueViolation(ex))
                    throw new DuplicateAMCNumberException(input.AMCNumber);
                throw;
            }
        }

        /// <summary>Checks that the entered AMC number is not already used by another contract.</summary>
        private void EnsureUniqueAMCNumber(AMCInput input)
        {
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1 ContractID
FROM dbo.AMCContracts
WHERE AMCNumber = @AMCNumber
  AND ContractID <> @ContractID;", connection))
            {
                command.Parameters.AddWithValue("@AMCNumber", input.AMCNumber);
                command.Parameters.AddWithValue("@ContractID", input.ContractId);
                DatabaseConnectionFactory.Open(connection, "AddAMCForm.EnsureUniqueAMCNumber");
                object existing = command.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                    throw new DuplicateAMCNumberException(input.AMCNumber);
            }
        }

        /// <summary>Adds common insert/update parameters to the save command.</summary>
        private void AddSaveParameters(SqlCommand command, AMCInput input)
        {
            command.Parameters.AddWithValue("@AMCNumber", input.AMCNumber);
            command.Parameters.AddWithValue("@ClientID", input.ClientId);
            command.Parameters.AddWithValue("@SiteID", input.SiteId.HasValue ? (object)input.SiteId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@EquipmentDesc", string.IsNullOrWhiteSpace(input.EquipmentDesc) ? (object)DBNull.Value : input.EquipmentDesc);
            command.Parameters.AddWithValue("@AMCType", input.AMCType ?? string.Empty);
            command.Parameters.AddWithValue("@StartDate", input.StartDate);
            command.Parameters.AddWithValue("@EndDate", input.EndDate);
            command.Parameters.AddWithValue("@ContractValue", input.ContractValue);
            command.Parameters.AddWithValue("@BillingCycle", input.BillingCycle ?? "Annual");
            command.Parameters.AddWithValue("@CoverageType", input.CoverageType ?? "Comprehensive");
            command.Parameters.AddWithValue("@VisitsPerYear", input.VisitsPerYear);
            command.Parameters.AddWithValue("@Status", input.Status ?? "Active");
            command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(input.Notes) ? (object)DBNull.Value : input.Notes);
            command.Parameters.AddWithValue("@MonthlyValue", CalculateMonthlyValue(input.ContractValue, input.BillingCycle));
            command.Parameters.AddWithValue("@AnnualValue", input.ContractValue);
            command.Parameters.AddWithValue("@ContractStatus", input.Status ?? "Active");
            command.Parameters.AddWithValue("@MaintenanceFrequency", input.BillingCycle ?? "Annual");
            command.Parameters.AddWithValue("@ContractType", input.AMCType ?? "AMC");
        }

        /// <summary>Calculates the monthly value used by legacy contract reports.</summary>
        private decimal CalculateMonthlyValue(decimal contractValue, string billingCycle)
        {
            if (string.Equals(billingCycle, "Monthly", StringComparison.OrdinalIgnoreCase)) return contractValue;
            if (string.Equals(billingCycle, "Quarterly", StringComparison.OrdinalIgnoreCase)) return contractValue / 3m;
            if (string.Equals(billingCycle, "Half-Yearly", StringComparison.OrdinalIgnoreCase)) return contractValue / 6m;
            return contractValue / 12m;
        }

        /// <summary>Updates the live summary card.</summary>
        private void UpdateSummary()
        {
            if (_summaryNumber == null)
                return;

            string client = (_cmbClient.SelectedItem as LookupItem)?.Text ?? "-";
            string type = Convert.ToString(_cmbAMCType.SelectedItem, CultureInfo.InvariantCulture);
            string billing = Convert.ToString(_cmbBillingCycle.SelectedItem, CultureInfo.InvariantCulture);
            string status = Convert.ToString(_cmbStatus.SelectedItem, CultureInfo.InvariantCulture);
            int months = Math.Max(0, ((_dtpEnd.Value.Year - _dtpStart.Value.Year) * 12) + _dtpEnd.Value.Month - _dtpStart.Value.Month);

            _summaryNumber.Text = string.IsNullOrWhiteSpace(_txtAMCNumber.Text) ? "-" : _txtAMCNumber.Text;
            _summaryClient.Text = string.IsNullOrWhiteSpace(client) ? "-" : client;
            _summaryType.Text = string.IsNullOrWhiteSpace(type) ? "-" : type;
            _summaryPeriod.Text = _dtpStart.Value.ToString("dd/MM/yyyy", _india) + " -> " + _dtpEnd.Value.ToString("dd/MM/yyyy", _india) + " (" + months.ToString(CultureInfo.InvariantCulture) + " months)";
            _summaryValue.Text = _numValue.Value.ToString("C0", _india);
            _summaryBilling.Text = string.IsNullOrWhiteSpace(billing) ? "-" : billing;
            _summaryVisits.Text = _numVisits.Value.ToString(CultureInfo.InvariantCulture);
            _summaryStatus.Text = string.IsNullOrWhiteSpace(status) ? "-" : status;
        }

        /// <summary>Returns the selected client id.</summary>
        private int GetSelectedClientId()
        {
            if (_cmbClient.SelectedItem is LookupItem item)
                return item.Id;
            return 0;
        }

        /// <summary>Returns the selected site id or null.</summary>
        private int? GetSelectedSiteId()
        {
            if (_cmbSite.SelectedItem is LookupItem item && item.Id > 0)
                return item.Id;
            return null;
        }

        /// <summary>Selects a lookup item in a dropdown by id.</summary>
        private void SelectLookup(ComboBox combo, int id)
        {
            if (combo == null)
                return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is LookupItem item && item.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        /// <summary>Selects a dropdown item by display text.</summary>
        private void SelectText(ComboBox combo, string text)
        {
            if (combo == null)
                return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(combo.Items[i], CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        /// <summary>Normalises derived dashboard statuses to editable statuses.</summary>
        private string NormalizeEditableStatus(string status)
        {
            if (string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase)) return "Active";
            if (string.Equals(status, "Expiring Soon", StringComparison.OrdinalIgnoreCase)) return "Active";
            return status;
        }

        /// <summary>Creates a shared rounded button.</summary>
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

        /// <summary>Creates a bordered white card panel.</summary>
        private Panel MakeCard()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            panel.Paint += (s, e) =>
            {
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            DS.Rounded(panel, 8);
            return panel;
        }

        /// <summary>Reads an integer safely from a data reader.</summary>
        private static int ReadInt(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a nullable integer safely from a data reader.</summary>
        private static int? ReadNullableInt(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a decimal safely from a data reader.</summary>
        private static decimal ReadDecimal(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a string safely from a data reader.</summary>
        private static string ReadString(SqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads a date safely from a data reader.</summary>
        private static DateTime ReadDateValue(SqlDataReader reader, string name, DateTime fallback)
        {
            object value = reader[name];
            return value == DBNull.Value ? fallback : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Returns true when SQL Server rejected a duplicate unique key.</summary>
        private static bool IsUniqueViolation(SqlException ex)
        {
            foreach (SqlError error in ex.Errors)
            {
                if (error.Number == 2601 || error.Number == 2627)
                    return true;
            }

            return false;
        }

        private sealed class DuplicateAMCNumberException : Exception
        {
            public DuplicateAMCNumberException(string amcNumber)
                : base("AMC Number '" + amcNumber + "' already exists. Enter a different AMC Number and save again.")
            {
            }
        }

        private sealed class ReferencePayload
        {
            public string NextAMCNumber;
            public AMCInput Existing;
            public readonly List<LookupItem> Clients = new List<LookupItem>();
        }

        private sealed class LookupItem
        {
            public LookupItem(int id, string text)
            {
                Id = id;
                Text = text ?? string.Empty;
            }

            public int Id { get; private set; }
            public string Text { get; private set; }
            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class AMCInput
        {
            public int ContractId;
            public string AMCNumber;
            public int ClientId;
            public int? SiteId;
            public string EquipmentDesc;
            public string AMCType;
            public string CoverageType;
            public DateTime StartDate;
            public DateTime EndDate;
            public decimal ContractValue;
            public string BillingCycle;
            public int VisitsPerYear;
            public string Status;
            public string Notes;
        }
    }
}


