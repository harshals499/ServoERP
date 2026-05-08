using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>
    /// ERP Contract Management: Left list | Right contract detail form.
    /// Contracts are always linked to a Client.
    /// </summary>
    public class ContractManagementForm : DeferredPageControl
    {
        // â”€â”€ Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly ContractService _contractSvc = new ContractService();
        private readonly ClientService   _clientSvc   = new ClientService();
        private readonly SiteService     _siteSvc     = new SiteService();

        // â”€â”€ List â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private FlowLayoutPanel _contractFlow;

        // â”€â”€ Header / form fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private ComboBox       _cmbClient, _cmbSite, _cmbStatus, _cmbFrequency, _cmbType;
        private DateTimePicker _dtpStart, _dtpEnd;
        private NumericUpDown  _numMonthly, _numAnnual;
        private NumericUpDown  _numSLAResponse, _numSLAUptime, _numSLARepair;
        private TextBox        _txtNotes;
        private Label          _lblStatus;

        // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static int? PendingSiteNavigationSiteId;
        private AMCContract _current;
        private Panel _selectedCard;
        private int? _siteFilterSiteId;
        private Button _btnNewContract;
        private Button _btnSaveContract;

        // â”€â”€ Colours â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color DelRed = DS.Red600;
        private static readonly Color AmberColor = DS.Amber500;
        private static readonly Color PurpleBtn = Color.FromArgb(79, 70, 229);

        public ContractManagementForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPermissions();
            EnableDeferredLoad(
                () =>
                {
                    LoadClientDropdown();
                    LoadContractList();
                },
                ex => { _lblStatus.Text = "Load error: " + ex.Message; _lblStatus.ForeColor = Color.Red; });
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LAYOUT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BuildLayout()
        {
            Controls.Clear();
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Header
            Panel header = new Panel { Dock = DockStyle.Fill, Height = 56, BackColor = HeaderBg, Padding = new Padding(16, 0, 0, 0) };
            header.Controls.Add(new Label { Text = "CONTRACT MANAGEMENT", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = DS.Slate900, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });

            // Toolbar
            Panel toolbar = new Panel { Dock = DockStyle.Fill, Height = 58, BackColor = Color.White, Padding = new Padding(16, 10, 16, 8) };
            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _btnNewContract     = MakeBtn("+ New Contract",   SaveGreen,  130);
            _btnSaveContract    = MakeBtn("Save",             SaveGreen,   80);
            Button btnInv     = MakeBtn("Create Invoice",   InfoBlue,   110);
            Button btnRenew   = MakeBtn("Renew",            AmberColor,  80);
            Button btnSLA     = MakeBtn("SLA Log",          PurpleBtn,   80);
            Button btnRef     = MakeBtn("Refresh",          InfoBlue,    90);

            _btnNewContract.Click   += BtnNew_Click;
            _btnSaveContract.Click  += BtnSave_Click;
            btnInv.Click   += BtnGenerateInvoice_Click;
            btnRenew.Click += BtnRenew_Click;
            btnSLA.Click   += BtnSLALog_Click;
            btnRef.Click   += (s, e) => LoadContractList();

            _lblStatus = new Label { AutoSize = false, Width = 220, Height = 36, Font = new Font("Segoe UI", 9), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(8, 0, 0, 0) };
            actionFlow.Controls.AddRange(new Control[] { _btnNewContract, _btnSaveContract, btnInv, btnRenew, btnSLA, btnRef, _lblStatus });
            toolbar.Controls.Add(actionFlow);

            // Split
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };

            Panel leftWrap = new Panel { Dock = DockStyle.Left, Width = 320, BackColor = Color.White };
            Label lblHdr   = new Label { Text = "ALL CONTRACTS", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = InfoBlue, BackColor = SectionBg, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
            _contractFlow  = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0), BackColor = Color.White };
            listWrap.Controls.Add(_contractFlow);
            leftWrap.Controls.Add(listWrap);
            leftWrap.Controls.Add(lblHdr);

            Panel rightWrap = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.BgPage, Padding = new Padding(16, 10, 16, 16) };
            BuildDetailForm(rightWrap);

            body.Controls.Add(rightWrap);
            body.Controls.Add(leftWrap);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(toolbar, 0, 1);
            root.Controls.Add(body, 0, 2);
            this.Controls.Add(root);
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("Contracts", this, _btnNewContract, _btnSaveContract, null);
        }

        private void BuildDetailForm(Panel container)
        {
            // Section: SLA
            GroupBox grpSLA = MakeGroup("SLA PARAMETERS");
            grpSLA.Dock = DockStyle.Top; grpSLA.Height = 120;

            AddLabel(grpSLA, "Response Time (Hours)", 8, 18);
            _numSLAResponse = NumBox(grpSLA, 0, 72, new Point(8, 34), 90);

            AddLabel(grpSLA, "Uptime % Target", 110, 18);
            _numSLAUptime = NumBox(grpSLA, 0, 100, new Point(110, 34), 90);
            _numSLAUptime.DecimalPlaces = 1;
            _numSLAUptime.Value = 99;

            AddLabel(grpSLA, "Repair Time (Hours)", 212, 18);
            _numSLARepair = NumBox(grpSLA, 0, 240, new Point(212, 34), 90);

            AddLabel(grpSLA, "Maintenance Frequency", 8, 68);
            _cmbFrequency = new ComboBox { Location = new Point(8, 84), Width = 160, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbFrequency.Items.AddRange(new object[] { "Monthly", "Quarterly", "Half-Yearly", "Yearly" });
            _cmbFrequency.SelectedIndex = 0;
            grpSLA.Controls.Add(_cmbFrequency);

            // Section: Financials
            GroupBox grpFin = MakeGroup("CONTRACT VALUE");
            grpFin.Dock = DockStyle.Top; grpFin.Height = 100;
            AddLabel(grpFin, "Monthly Value (INR)", 8, 18);
            _numMonthly = NumBox(grpFin, 0, 99999999, new Point(8, 34), 180);
            _numMonthly.DecimalPlaces = 2;
            _numMonthly.ValueChanged += (s, e) => _numAnnual.Value = _numMonthly.Value * 12;
            AddLabel(grpFin, "Annual Value (INR)", 200, 18);
            _numAnnual = NumBox(grpFin, 0, 99999999, new Point(200, 34), 180);
            _numAnnual.DecimalPlaces = 2;

            // Section: Header
            GroupBox grpHdr = MakeGroup("CONTRACT DETAILS");
            grpHdr.Dock = DockStyle.Top; grpHdr.Height = 290;

            AddLabel(grpHdr, "Client *", 8, 18);
            _cmbClient = new ComboBox { Location = new Point(8, 34), Width = 300, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbClient.SelectedIndexChanged += (s, e) => LoadSiteDropdown(((ComboItem)_cmbClient.SelectedItem)?.Id ?? 0);
            grpHdr.Controls.Add(_cmbClient);

            AddLabel(grpHdr, "Site *", 8, 72);
            _cmbSite = new ComboBox { Location = new Point(8, 88), Width = 300, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            grpHdr.Controls.Add(_cmbSite);

            AddLabel(grpHdr, "Contract Type", 8, 122);
            _cmbType = new ComboBox { Location = new Point(8, 138), Width = 140, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbType.Items.AddRange(new object[] { "AMC", "O&M", "CMC", "Warranty" });
            _cmbType.SelectedIndex = 0;
            grpHdr.Controls.Add(_cmbType);

            AddLabel(grpHdr, "Status", 160, 122);
            _cmbStatus = new ComboBox { Location = new Point(160, 138), Width = 130, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbStatus.Items.AddRange(new object[] { "Active", "Expired", "Suspended", "Pending" });
            _cmbStatus.SelectedIndex = 0;
            grpHdr.Controls.Add(_cmbStatus);

            AddLabel(grpHdr, "Start Date *", 8, 172);
            _dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(8, 188), Width = 160, Value = DateTime.Today };
            grpHdr.Controls.Add(_dtpStart);

            AddLabel(grpHdr, "End Date *", 180, 172);
            _dtpEnd = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(180, 188), Width = 160, Value = DateTime.Today.AddYears(1) };
            grpHdr.Controls.Add(_dtpEnd);

            AddLabel(grpHdr, "Notes", 8, 222);
            _txtNotes = new TextBox { Location = new Point(8, 238), Width = 380, Height = 40, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            grpHdr.Controls.Add(_txtNotes);

            Panel spacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = DS.BgPage };

            container.Controls.Add(grpSLA);
            container.Controls.Add(grpFin);
            container.Controls.Add(grpHdr);
            container.Controls.Add(spacer);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DATA
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void LoadClientDropdown()
        {
            _cmbClient.Items.Clear();
            _cmbClient.Items.Add(new ComboItem { Id = 0, Text = "-- Select Client --" });
            List<B2BClient> clients = new List<B2BClient>();
            try
            {
                clients = _clientSvc.GetAllClients();
                foreach (B2BClient c in clients)
                    _cmbClient.Items.Add(new ComboItem { Id = c.ClientID, Text = c.CompanyName });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ContractManagementForm.LoadClientDropdown", ex);
                ShowStatus("Could not load clients: " + ex.Message, Color.Red);
            }
            UIHelper.ShowEmptyClientsMessageIfNeeded(FindForm(), clients, "ContractManagementForm.LoadClientDropdown");
            _cmbClient.SelectedIndex = 0;
            LoadSiteDropdown(0);
        }

        private void LoadSiteDropdown(int clientId)
        {
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem { Id = 0, Text = "-- Select Site --" });
            if (clientId > 0)
            {
                try
                {
                    foreach (ClientSite s in _siteSvc.GetByClientId(clientId))
                        _cmbSite.Items.Add(new ComboItem { Id = s.SiteID, Text = SiteService.GetDisplayName(s) });
                }
                catch { }
            }
            _cmbSite.SelectedIndex = 0;
        }

        private void LoadContractList()
        {
            _contractFlow.SuspendLayout();
            _contractFlow.Controls.Clear();
            DateTime expiryThreshold = DateTime.Now.AddDays(90);
            try
            {
                var contracts = _contractSvc.GetAllContracts();
                if (_siteFilterSiteId.HasValue)
                    contracts = contracts.Where(c => c.SiteID == _siteFilterSiteId.Value).ToList();

                foreach (AMCContract c in contracts)
                {
                    string clientName;
                    try
                    {
                        var client = _clientSvc.GetClientById(c.ClientID);
                        clientName = client?.CompanyName ?? "Client #" + c.ClientID;
                    }
                    catch { clientName = "Client #" + c.ClientID; }

                    string displayStatus = ResolveDisplayStatus(c, expiryThreshold);
                    Color statusColor;
                    if (displayStatus == "Expiring Soon")
                    {
                        statusColor = Color.FromArgb(255, 140, 0);
                    }
                    else
                    {
                        switch (displayStatus)
                        {
                            case "Active":  statusColor = Color.FromArgb(46, 160, 67); break;
                            case "Expired": statusColor = Color.FromArgb(196, 43, 28); break;
                            default:        statusColor = Color.FromArgb(211, 84, 0);  break;
                        }
                    }

                    _contractFlow.Controls.Add(MakeContractCard(c, clientName, displayStatus, statusColor));
                }
                _contractFlow.ResumeLayout(true);
                ShowStatus(_contractFlow.Controls.Count + (_siteFilterSiteId.HasValue ? " contracts for selected site." : " contracts."), Color.Gray);
            }
            catch (Exception ex)
            {
                _contractFlow.ResumeLayout(true);
                ShowStatus("Error: " + ex.Message, Color.Red);
            }
        }

        private static string ResolveDisplayStatus(AMCContract contract, DateTime expiryThreshold)
        {
            if (contract == null)
                return "Pending";

            DateTime today = DateTime.Today;
            if (contract.EndDate.Date < today)
                return "Expired";

            string status = string.IsNullOrWhiteSpace(contract.ContractStatus) ? "Pending" : contract.ContractStatus.Trim();
            if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) &&
                contract.EndDate.Date <= expiryThreshold.Date)
                return "Expiring Soon";

            return status;
        }

        public static void QueueSiteNavigation(int siteId)
        {
            PendingSiteNavigationSiteId = siteId > 0 ? (int?)siteId : null;
        }

        public void ApplyNavigationRequest()
        {
            if (!PendingSiteNavigationSiteId.HasValue)
                return;

            _siteFilterSiteId = PendingSiteNavigationSiteId;
            PendingSiteNavigationSiteId = null;

            LoadContractList();

            int siteId = _siteFilterSiteId.Value;
            AMCContract contract = _contractSvc.GetAllContracts().FirstOrDefault(c => c.SiteID == siteId);
            if (contract != null)
            {
                SelectCombo(_cmbClient, contract.ClientID);
                LoadSiteDropdown(contract.ClientID);
                SelectCombo(_cmbSite, contract.SiteID);
                _current = contract;
                PopulateForm(contract);
                return;
            }

            ClientSite site = _siteSvc.GetById(siteId);
            if (site != null)
            {
                SelectCombo(_cmbClient, site.ClientID);
                LoadSiteDropdown(site.ClientID);
                SelectCombo(_cmbSite, site.SiteID);
                ShowStatus("No contracts found for " + site.SiteName + ".", AmberColor);
            }
        }

        private void SelectContract(AMCContract contract, Panel card)
        {
            if (_selectedCard != null)
                HighlightCard(_selectedCard, false);

            _selectedCard = card;
            HighlightCard(card, true);
            _current = contract;
            PopulateForm(_current);
        }

        private void PopulateForm(AMCContract c)
        {
            SelectCombo(_cmbClient, c.ClientID);
            LoadSiteDropdown(c.ClientID);
            SelectCombo(_cmbSite, c.SiteID);
            SelectComboText(_cmbType,      c.ContractType          ?? "AMC");
            SelectComboText(_cmbStatus,    c.ContractStatus        ?? "Active");
            SelectComboText(_cmbFrequency, c.MaintenanceFrequency  ?? "Monthly");
            _dtpStart.Value        = c.StartDate == default ? DateTime.Today : c.StartDate;
            _dtpEnd.Value          = c.EndDate   == default ? DateTime.Today.AddYears(1) : c.EndDate;
            _numMonthly.Value      = Math.Min(c.MonthlyValue, 99999999);
            _numAnnual.Value       = Math.Min(c.AnnualValue,  99999999);
            _numSLAResponse.Value  = Math.Min(c.SLAResponseTimeHours, 72);
            _numSLAUptime.Value    = Math.Min(c.SLAUptimePercent, 100);
            _numSLARepair.Value    = Math.Min(c.SLARepairTimeHours, 240);
            _txtNotes.Text         = c.Notes ?? "";
            ShowStatus("Loaded contract #" + c.ContractID, InfoBlue);
        }

        private AMCContract CollectForm()
        {
            AMCContract c = _current ?? new AMCContract();
            c.ClientID              = ((ComboItem)_cmbClient.SelectedItem)?.Id ?? 0;
            c.SiteID                = ((ComboItem)_cmbSite.SelectedItem)?.Id ?? 0;
            c.ContractType          = _cmbType.SelectedItem?.ToString() ?? "AMC";
            c.ContractStatus        = _cmbStatus.SelectedItem?.ToString() ?? "Active";
            c.MaintenanceFrequency  = _cmbFrequency.SelectedItem?.ToString() ?? "Monthly";
            c.StartDate             = _dtpStart.Value.Date;
            c.EndDate               = _dtpEnd.Value.Date;
            c.MonthlyValue          = _numMonthly.Value;
            c.AnnualValue           = _numAnnual.Value;
            c.SLAResponseTimeHours  = (int)_numSLAResponse.Value;
            c.SLAUptimePercent      = _numSLAUptime.Value;
            c.SLARepairTimeHours    = (int)_numSLARepair.Value;
            c.Notes                 = _txtNotes.Text.Trim();
            return c;
        }

        private void ClearForm()
        {
            _current = null;
            _cmbClient.SelectedIndex = 0;
            _cmbSite.SelectedIndex = 0;
            _cmbType.SelectedIndex = 0; _cmbStatus.SelectedIndex = 0; _cmbFrequency.SelectedIndex = 0;
            _dtpStart.Value = DateTime.Today; _dtpEnd.Value = DateTime.Today.AddYears(1);
            _numMonthly.Value = 0; _numAnnual.Value = 0;
            _numSLAResponse.Value = 4; _numSLAUptime.Value = 99; _numSLARepair.Value = 8;
            _txtNotes.Text = "";
            if (_selectedCard != null)
            {
                HighlightCard(_selectedCard, false);
                _selectedCard = null;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BtnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            _cmbClient.Focus();
            ShowStatus("New contract â€” fill and click Save.", Color.Gray);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                AMCContract c = CollectForm();
                if (c.ClientID == 0) throw new Exception("Please select a client.");
                if (c.SiteID == 0) throw new Exception("Please select a site.");
                if (c.EndDate <= c.StartDate) throw new Exception("End date must be after start date.");

                if (_current == null || _current.ContractID == 0)
                {
                    int id = _contractSvc.CreateContract(c);
                    c.ContractID = id;
                    _current = c;
                    ShowStatus("Contract created #" + id, SaveGreen);
                }
                else
                {
                    _contractSvc.UpdateContract(c);
                    ShowStatus("Contract saved #" + _current.ContractID, SaveGreen);
                }
                LoadContractList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnGenerateInvoice_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please select a contract first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                // Resolve client name for confirmation message
                string clientName = "Client #" + _current.ClientID;
                try
                {
                    var client = _clientSvc.GetClientById(_current.ClientID);
                    if (client != null) clientName = client.CompanyName;
                }
                catch { }

                decimal baseAmount = _current.MonthlyValue;
                decimal gstAmount  = Math.Round(baseAmount * 0.18m, 2);
                decimal total      = baseAmount + gstAmount;

                Invoice invoice = new Invoice
                {
                    ContractID    = _current.ContractID,
                    ClientID      = _current.ClientID,
                    SiteID        = _current.SiteID,
                    InvoiceNumber = "INV-" + DateTime.Now.ToString("yyyyMMdd"),
                    InvoiceDate   = DateTime.Today,
                    DueDate       = DateTime.Today.AddDays(30),
                    SubTotal      = baseAmount,
                    GSTPercent    = 18m,
                    TaxAmount     = gstAmount,
                    TotalAmount   = total,
                    BalanceDue    = total,
                    PaidAmount    = 0,
                    PaymentStatus = "Draft",
                    LineItems     = new System.Collections.Generic.List<InvoiceLineItem>
                    {
                        new InvoiceLineItem
                        {
                            Description = "AMC Monthly Service â€” " + DateTime.Today.ToString("MMMM yyyy"),
                            Quantity    = 1,
                            Rate        = baseAmount,
                            Amount      = baseAmount
                        }
                    }
                };

                var invSvc = new InvoiceService();
                invSvc.CreateInvoiceWithLineItems(invoice);

                MessageBox.Show(
                    "Invoice created for " + clientName + ".",
                    "Invoice Generated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ShowStatus("Invoice created for " + clientName + ".", SaveGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRenew_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please select a contract first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                AMCContract renewed = new AMCContract
                {
                    ClientID             = _current.ClientID,
                    SiteID               = _current.SiteID,
                    ContractType         = _current.ContractType,
                    MonthlyValue         = _current.MonthlyValue,
                    AnnualValue          = _current.AnnualValue,
                    ContractStatus       = "Active",
                    StartDate            = _current.EndDate.AddDays(1),
                    EndDate              = _current.EndDate.AddYears(1),
                    MaintenanceFrequency = _current.MaintenanceFrequency,
                    SLAResponseTimeHours = _current.SLAResponseTimeHours,
                    SLAUptimePercent     = _current.SLAUptimePercent,
                    SLARepairTimeHours   = _current.SLARepairTimeHours,
                    Notes                = _current.Notes
                };
                _contractSvc.CreateContract(renewed);
                MessageBox.Show(
                    "Contract renewed successfully.",
                    "Contract Renewed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ShowStatus("Contract renewed successfully.", SaveGreen);
                LoadContractList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSLALog_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please select a contract first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                int contractId = _current.ContractID;
                int count = new SLAService().GetAll()
                    .FindAll(log => log.ContractID == contractId)
                    .Count;
                MessageBox.Show(
                    string.Format("Contract {0}: {1} SLA events logged. Go to SLA Dashboard for details.", contractId, count),
                    "SLA Log",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void ShowStatus(string msg, Color color) { _lblStatus.Text = msg; _lblStatus.ForeColor = color; }

        private Panel MakeContractCard(AMCContract contract, string clientName, string displayStatus, Color statusColor)
        {
            Panel card = new Panel
            {
                Width = 318,
                Height = 84,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
                Padding = new Padding(14, 12, 14, 12),
                Tag = contract
            };

            card.Paint += (s, e) =>
            {
                using (Pen border = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(border, 0, card.Height - 1, card.Width, card.Height - 1);
            };

            Label lblClient = new Label
            {
                Text = clientName,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Location = new Point(14, 12),
                AutoSize = true
            };

            Label lblMeta = new Label
            {
                Text = string.Format("{0}  |  Ends {1:dd MMM yyyy}", contract.ContractType ?? "AMC", contract.EndDate),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                Location = new Point(14, 38),
                AutoSize = true
            };

            Label lblStatus = new Label
            {
                Text = string.IsNullOrWhiteSpace(displayStatus) ? "Pending" : displayStatus,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = statusColor,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            lblStatus.Location = new Point(card.Width - lblStatus.PreferredWidth - 16, 14);

            foreach (Control control in new Control[] { card, lblClient, lblMeta, lblStatus })
            {
                control.Click += (s, e) => SelectContract(contract, card);
            }

            card.Controls.Add(lblClient);
            card.Controls.Add(lblMeta);
            card.Controls.Add(lblStatus);
            return card;
        }

        private void HighlightCard(Panel card, bool selected)
        {
            card.BackColor = selected ? DS.Indigo50 : Color.White;
            foreach (Control child in card.Controls)
            {
                if (child is Label label && label.Font.Bold)
                    label.ForeColor = selected ? DS.Indigo600 : DS.Slate900;
            }
        }

        private GroupBox MakeGroup(string title) =>
            new GroupBox { Text = title, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = InfoBlue, BackColor = SectionBg, Padding = new Padding(8) };

        private void AddLabel(GroupBox parent, string text, int x, int y) =>
            parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) });

        private NumericUpDown NumBox(GroupBox parent, decimal min, decimal max, Point loc, int width)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, Location = loc, Width = width, Font = new Font("Segoe UI", 9) };
            parent.Controls.Add(n);
            return n;
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            Button b = new Button { Text = text, Width = Math.Max(width, 96), Height = 34, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 6) };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = DS.Lighten(bg, 0.08f);
            b.FlatAppearance.MouseDownBackColor = DS.Darken(bg, 0.08f);
            return b;
        }

        private void SelectCombo(ComboBox cmb, int id)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
                if ((cmb.Items[i] as ComboItem)?.Id == id) { cmb.SelectedIndex = i; return; }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private void SelectComboText(ComboBox cmb, string text)
        {
            int i = cmb.Items.IndexOf(text);
            cmb.SelectedIndex = i >= 0 ? i : 0;
        }

        private class ComboItem
        {
            public int    Id   { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text;
        }
    }
}

