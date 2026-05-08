using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class MasterDataForm : DeferredPageControl
    {
        protected override bool EnableAutomaticLayoutScaling => false;

        private readonly MasterDataService _svc = new MasterDataService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly CompanyTemplateManager _templateManager = new CompanyTemplateManager();

        private TabControl _tabs;
        private DataGridView _statusGrid, _assetGrid, _docGrid, _rateGrid, _serverGrid, _importGrid;
        private ComboBox _assetClient, _assetSite, _docClient, _docType, _rateClient, _rateCategory, _serverType, _syncDirection;
        private TextBox _assetType, _assetTag, _assetBrand, _assetModel, _assetSerial, _assetCapacity, _assetLocation, _assetNotes;
        private DateTimePicker _assetInstall, _assetWarranty, _docExpiry, _rateEffective;
        private CheckBox _assetInstallOn, _assetWarrantyOn, _assetAmc, _docExpiryOn, _rateEmergency;
        private TextBox _docTitle, _docPath, _docNotes;
        private TextBox _rateName, _rateUnit, _rateNotes;
        private NumericUpDown _rateAmount, _rateGst, _serverPort;
        private TextBox _serverName, _serverHost, _serverDb, _serverApi, _serverUser, _serverSecret;
        private Label _status;
        private FlowLayoutPanel _hubFlow;
        private MasterDataSnapshot _lastSnapshot;
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<ClientSite> _sites = new List<ClientSite>();
        private ClientAsset _selectedAsset;
        private ServiceRateCard _selectedRate;
        private PrivateServerConnection _selectedConnection;

        private static readonly Color ActionBlue = DS.Indigo600;
        private static readonly Color SaveGreen = DS.Green500;
        private static readonly Color SoftTeal = DS.Indigo50;

        public MasterDataForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad(LoadAllAsync, ex => ShowStatus("Load error: " + ex.Message, true));
        }

        private void BuildLayout()
        {
            Controls.Clear();
            Panel root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                Padding = new Padding(0)
            };
            _hubFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = DS.BgPage,
                Padding = new Padding(18, 16, 18, 24)
            };
            _hubFlow.Resize += (s, e) => ResizeHubRows();

            root.Controls.Add(_hubFlow);
            root.Controls.Add(BuildHeader());
            Controls.Add(root);
            RenderHub(null);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Color.White, Padding = new Padding(18, 12, 18, 10) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            header.Controls.Add(new Label
            {
                Text = "Master Data",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = DS.Slate900,
                TextAlign = ContentAlignment.MiddleLeft,
            });
            header.Controls.Add(new Label
            {
                Text = "Upload, validate, map, sync, and use business data across ServoERP.",
                Dock = DockStyle.Bottom,
                Height = 20,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleLeft
            });
            return header;
        }

        private Control BuildToolbar()
        {
            Panel bar = new Panel { Dock = DockStyle.Fill, Height = 58, BackColor = DS.BgPage, Padding = new Padding(18, 10, 18, 8) };
            Button refresh = MakeButton("Refresh Data", ActionBlue, 120);
            refresh.Dock = DockStyle.Left;
            refresh.Click += async (s, e) => await LoadAllAsync();
            _status = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = DS.Slate500,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            bar.Controls.Add(refresh);
            bar.Controls.Add(_status);
            return bar;
        }

        private Control BuildTabs()
        {
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Padding = new Point(18, 8),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(150, 42),
                SizeMode = TabSizeMode.Fixed,
                BackColor = DS.BgPage
            };
            _tabs.DrawItem += DrawModernTab;
            _tabs.TabPages.Add(BuildSetupTab());
            _tabs.TabPages.Add(BuildAssetsTab());
            _tabs.TabPages.Add(BuildDocumentsTab());
            _tabs.TabPages.Add(BuildRatesTab());
            _tabs.TabPages.Add(BuildServerTab());
            _tabs.TabPages.Add(BuildImportsTab());
            return _tabs;
        }

        private void DrawModernTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = (TabControl)sender;
            Rectangle bounds = e.Bounds;
            bool selected = e.Index == tabs.SelectedIndex;
            Color back = selected ? Color.White : DS.BgPage;
            Color fore = selected ? DS.Primary700 : DS.Slate600;

            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, bounds, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(DS.Primary600))
                    e.Graphics.FillRectangle(accent, bounds.Left + 18, bounds.Bottom - 4, bounds.Width - 36, 3);
            }
        }

        private TabPage BuildSetupTab()
        {
            TabPage tab = new TabPage("Setup") { BackColor = DS.BgPage, Padding = new Padding(18) };
            _statusGrid = MakeGrid();
            tab.Controls.Add(_statusGrid);
            return tab;
        }

        private TabPage BuildAssetsTab()
        {
            TabPage tab = new TabPage("Assets") { BackColor = DS.BgPage, Padding = new Padding(18) };
            TableLayoutPanel split = MakeSplit();
            _assetGrid = MakeGrid();
            _assetGrid.SelectionChanged += (s, e) => SelectAssetFromGrid();
            split.Controls.Add(_assetGrid, 0, 0);

            FlowLayoutPanel form = MakeFormFlow();
            _assetClient = AddCombo(form, "Client *");
            _assetClient.SelectedIndexChanged += (s, e) => RefreshSiteCombo(_assetClient, _assetSite);
            _assetSite = AddCombo(form, "Site");
            _assetType = AddText(form, "Equipment type *");
            _assetTag = AddText(form, "Asset tag");
            _assetBrand = AddText(form, "Brand");
            _assetModel = AddText(form, "Model");
            _assetSerial = AddText(form, "Serial number");
            _assetCapacity = AddText(form, "Capacity / tonnage");
            _assetLocation = AddText(form, "Location");
            _assetInstall = AddDate(form, "Install date", out _assetInstallOn);
            _assetWarranty = AddDate(form, "Warranty expiry", out _assetWarrantyOn);
            _assetAmc = new CheckBox { Text = "Covered under AMC", Width = 220, Height = 24, Margin = new Padding(14, 6, 0, 0) };
            form.Controls.Add(_assetAmc);
            _assetNotes = AddText(form, "Notes", true);
            form.Controls.Add(ActionRow(
                MakeButton("New asset", ActionBlue, 100, (s, e) => ClearAssetForm()),
                MakeButton("Save asset", SaveGreen, 100, (s, e) => SaveAsset())));
            split.Controls.Add(form, 1, 0);
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildDocumentsTab()
        {
            TabPage tab = new TabPage("Documents") { BackColor = DS.BgPage, Padding = new Padding(18) };
            TableLayoutPanel split = MakeSplit();
            _docGrid = MakeGrid();
            _docGrid.CellDoubleClick += (s, e) => OpenSelectedDocument();
            split.Controls.Add(_docGrid, 0, 0);

            FlowLayoutPanel form = MakeFormFlow();
            _docClient = AddCombo(form, "Client");
            _docType = AddCombo(form, "Document type");
            _docType.Items.AddRange(new object[] { "AMC Contract", "Purchase Order", "License", "Insurance", "Tax Document", "Warranty", "Manual", "Drawing", "Certificate", "Other" });
            if (_docType.Items.Count > 0) _docType.SelectedIndex = 0;
            _docTitle = AddText(form, "Title *");
            _docPath = AddText(form, "File path *");
            form.Controls.Add(ActionRow(MakeButton("Choose file", ActionBlue, 112, (s, e) => ChooseDocumentFile())));
            _docExpiry = AddDate(form, "Expiry date", out _docExpiryOn);
            _docNotes = AddText(form, "Notes", true);
            form.Controls.Add(ActionRow(MakeButton("Save document", SaveGreen, 126, (s, e) => SaveDocument())));
            split.Controls.Add(form, 1, 0);
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildRatesTab()
        {
            TabPage tab = new TabPage("Rate Cards") { BackColor = DS.BgPage, Padding = new Padding(18) };
            TableLayoutPanel split = MakeSplit();
            _rateGrid = MakeGrid();
            _rateGrid.SelectionChanged += (s, e) => SelectRateFromGrid();
            split.Controls.Add(_rateGrid, 0, 0);

            FlowLayoutPanel form = MakeFormFlow();
            _rateClient = AddCombo(form, "Client specific");
            _rateCategory = AddCombo(form, "Category");
            _rateCategory.Items.AddRange(new object[] { "Labor", "Diagnostic", "Emergency", "AMC", "Travel", "Installation", "Repair", "Cleaning", "Other" });
            if (_rateCategory.Items.Count > 0) _rateCategory.SelectedIndex = 0;
            _rateName = AddText(form, "Service name *");
            _rateUnit = AddText(form, "Unit");
            _rateAmount = AddNumber(form, "Rate", 9999999, 2);
            _rateGst = AddNumber(form, "GST %", 100, 2);
            _rateGst.Value = 18;
            _rateEffective = AddDate(form, "Effective from", out CheckBox unused);
            unused.Checked = true;
            unused.Visible = false;
            _rateEmergency = new CheckBox { Text = "Emergency rate", Width = 220, Height = 24, Margin = new Padding(14, 6, 0, 0) };
            form.Controls.Add(_rateEmergency);
            _rateNotes = AddText(form, "Notes", true);
            form.Controls.Add(ActionRow(
                MakeButton("New rate", ActionBlue, 92, (s, e) => ClearRateForm()),
                MakeButton("Save rate", SaveGreen, 92, (s, e) => SaveRate())));
            split.Controls.Add(form, 1, 0);
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildServerTab()
        {
            TabPage tab = new TabPage("Server") { BackColor = DS.BgPage, Padding = new Padding(18) };
            TableLayoutPanel split = MakeSplit();
            _serverGrid = MakeGrid();
            _serverGrid.SelectionChanged += (s, e) => SelectConnectionFromGrid();
            split.Controls.Add(_serverGrid, 0, 0);

            FlowLayoutPanel form = MakeFormFlow();
            _serverName = AddText(form, "Connection name *");
            _serverType = AddCombo(form, "Server type");
            _serverType.Items.AddRange(new object[] { "SQL Server", "REST API", "SFTP", "Shared Folder" });
            _serverType.SelectedIndex = 0;
            _serverHost = AddText(form, "Host / IP");
            _serverPort = AddNumber(form, "Port", 65535, 0);
            _serverDb = AddText(form, "Database / API name");
            _serverApi = AddText(form, "API base URL");
            _serverUser = AddText(form, "Username");
            _serverSecret = AddText(form, "Password / API key");
            _serverSecret.UseSystemPasswordChar = true;
            _syncDirection = AddCombo(form, "Sync direction");
            _syncDirection.Items.AddRange(new object[] { "Import only", "Export only", "Two way" });
            _syncDirection.SelectedIndex = 0;
            form.Controls.Add(ActionRow(
                MakeButton("New connection", ActionBlue, 130, (s, e) => ClearConnectionForm()),
                MakeButton("Save connection", SaveGreen, 140, (s, e) => SaveConnection()),
                MakeButton("Test info", ActionBlue, 92, (s, e) => PreviewConnection())));
            split.Controls.Add(form, 1, 0);
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildImportsTab()
        {
            TabPage tab = new TabPage("Imports") { BackColor = DS.BgPage, Padding = new Padding(18) };
            Panel top = new Panel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(18), BackColor = SoftTeal };
            top.Controls.Add(new Label
            {
                Text = "Import batches will appear here. Next build step: Excel/CSV validation templates for clients, assets, contracts, pricing, inventory and employees.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(15, 118, 110),
                Font = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleLeft
            });
            _importGrid = MakeGrid();
            tab.Controls.Add(_importGrid);
            tab.Controls.Add(top);
            return tab;
        }

        private void RenderHub(MasterDataSnapshot snapshot)
        {
            if (_hubFlow == null)
                return;

            _lastSnapshot = snapshot;
            _hubFlow.SuspendLayout();
            _hubFlow.Controls.Clear();

            _hubFlow.Controls.Add(BuildHeroDropZone());
            _hubFlow.Controls.Add(BuildWorkflowStrip());
            _hubFlow.Controls.Add(BuildHubMainRow(snapshot));
            _hubFlow.Controls.Add(BuildRecentAndWarningsRow(snapshot));
            _hubFlow.Controls.Add(BuildOperationalFooter(snapshot));

            _hubFlow.ResumeLayout(true);
            ResizeHubRows();
        }

        private void ResizeHubRows()
        {
            if (_hubFlow == null)
                return;

            int width = Math.Max(980, _hubFlow.ClientSize.Width - 40);
            foreach (Control control in _hubFlow.Controls)
                control.Width = width;
        }

        private Control BuildHeroDropZone()
        {
            Panel panel = CreateHubCard(112);
            panel.AllowDrop = true;
            panel.DragEnter += HubDragEnter;
            panel.DragDrop += HubDragDrop;
            panel.Cursor = Cursors.Hand;
            panel.Click += (s, e) => ShowBulkImportMenu(panel);

            Label title = new Label
            {
                Text = "Data Control Center",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Location = new Point(24, 18),
                AutoSize = true
            };
            Label subtitle = new Label
            {
                Text = "Drop Excel, CSV, PDF, or vendor/client data exports here. ServoERP validates, maps, and syncs usable records into modules.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = DS.Slate600,
                Location = new Point(24, 54),
                Size = new Size(760, 42)
            };
            Button upload = MakeButton("Upload data", DS.Primary600, 126, (s, e) => ShowBulkImportMenu(panel));
            upload.Location = new Point(panel.Width - 340, 34);
            upload.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Button validate = MakeButton("Run validation", SaveGreen, 136, async (s, e) => await LoadAllAsync());
            validate.Location = new Point(panel.Width - 196, 34);
            validate.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            panel.Controls.Add(title);
            panel.Controls.Add(subtitle);
            panel.Controls.Add(upload);
            panel.Controls.Add(validate);
            return panel;
        }

        private Control BuildWorkflowStrip()
        {
            Panel panel = CreateHubCard(100);
            string[] steps = { "Upload", "Validate", "Map", "Sync", "Use Across App" };
            string[] captions = { "Excel, CSV, PDF, APIs", "required fields, duplicates", "columns to ERP modules", "clients, vendors, jobs", "quotes, invoices, PO" };
            for (int i = 0; i < steps.Length; i++)
            {
                int x = 24 + (i * 190);
                Panel badge = new Panel { Location = new Point(x, 22), Size = new Size(44, 44), BackColor = DS.Primary50 };
                DS.Rounded(badge, 22);
                badge.Controls.Add(new Label { Text = (i + 1).ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = DS.Primary700 });
                Label step = new Label { Text = steps[i], Location = new Point(x + 54, 20), Size = new Size(128, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 };
                Label caption = new Label { Text = captions[i], Location = new Point(x + 54, 44), Size = new Size(132, 34), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500 };
                panel.Controls.Add(badge);
                panel.Controls.Add(step);
                panel.Controls.Add(caption);
                if (i < steps.Length - 1)
                    panel.Controls.Add(new Label { Text = "->", Location = new Point(x + 164, 34), Size = new Size(26, 24), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate400 });
            }
            return panel;
        }

        private Control BuildHubMainRow(MasterDataSnapshot snapshot)
        {
            TableLayoutPanel row = new TableLayoutPanel { Height = 430, ColumnCount = 2, RowCount = 1, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 16) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            FlowLayoutPanel uploads = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), AutoScroll = true };
            uploads.Paint += (s, e) => DrawHubBorder(uploads, e);
            uploads.Controls.Add(SectionTitle("Smart upload cards", "Choose what you are onboarding. ServoERP will validate and route records to the right module."));
            uploads.Controls.Add(BuildUploadCard("Clients", Count(snapshot?.Clients), "Company lists, contact details, GST, city", ExcelImportModule.Clients, "clients"));
            uploads.Controls.Add(BuildUploadCard("Vendors", CountVendors(), "Supplier master, GST, payment terms, contact", ExcelImportModule.Vendors, "vendors"));
            uploads.Controls.Add(BuildUploadCard("Invoices", CountTable("Invoices"), "Past invoices, balances, due dates", ExcelImportModule.Invoices, "invoices"));
            uploads.Controls.Add(BuildUploadCard("Inventory", CountTable("StockItems"), "Parts, stock, reorder levels", null, "inventory"));
            uploads.Controls.Add(BuildUploadCard("Contracts", CountTable("AMCContracts"), "AMC terms, renewal dates, SLA data", null, "contracts"));
            uploads.Controls.Add(BuildUploadCard("Equipment / Assets", Count(snapshot?.Assets), "Client HVAC assets, serials, warranty", null, "assets"));
            uploads.Controls.Add(BuildUploadCard("Rate Cards", Count(snapshot?.Rates), "Labour, service pricing, emergency rates", null, "rates"));
            uploads.Controls.Add(BuildUploadCard("Documents / PDFs", Count(snapshot?.Documents), "Contracts, POs, manuals, certificates", null, "documents"));
            uploads.Controls.Add(BuildUploadCard("Company Document Templates", CountCompanyTemplates(), "Invoice, quotation, PO, letterhead, terms", null, "company-templates"));

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            actions.Paint += (s, e) => DrawHubBorder(actions, e);
            actions.Controls.Add(SectionTitle("Sync command center", "One-click checks before data is used across the ERP."));
            actions.Controls.Add(BuildActionTile("Refresh all data", "Reload counts and integration status", DS.Primary600, async () => await LoadAllAsync()));
            actions.Controls.Add(BuildActionTile("Download templates", "Get Excel formats for clean imports", ActionBlue, () => ShowTemplateMenu(actions)));
            actions.Controls.Add(BuildActionTile("Duplicate check", "Find repeated clients, vendors, quotes", Color.FromArgb(249, 115, 22), () => ShowDuplicateCheck()));
            actions.Controls.Add(BuildActionTile("Open import log", "Review recent sync batches", Color.FromArgb(124, 58, 237), () => ShowExistingTab(5)));
            actions.Controls.Add(BuildActionTile("Configure API/server", "Private server or API sync settings", DS.Red600, () => ShowExistingTab(4)));

            row.Controls.Add(uploads, 0, 0);
            row.Controls.Add(actions, 1, 0);
            return row;
        }

        private Control BuildRecentAndWarningsRow(MasterDataSnapshot snapshot)
        {
            TableLayoutPanel row = new TableLayoutPanel { Height = 255, ColumnCount = 2, RowCount = 1, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 16) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            FlowLayoutPanel warnings = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            warnings.Paint += (s, e) => DrawHubBorder(warnings, e);
            warnings.Controls.Add(SectionTitle("Missing data warnings", "Resolve these to make imports usable in quotations, POs, invoices, jobs, and reports."));
            foreach (MasterDataStatus status in (snapshot?.SetupStatus ?? new List<MasterDataStatus>()).Where(s => !s.IsComplete).Take(6))
                warnings.Controls.Add(BuildWarningRow(status.Category, status.NextAction));
            if (warnings.Controls.Count == 1)
                warnings.Controls.Add(BuildWarningRow("All required hubs have data", "Ready to use across modules"));

            FlowLayoutPanel recent = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            recent.Paint += (s, e) => DrawHubBorder(recent, e);
            recent.Controls.Add(SectionTitle("Recent imports and sync status", "Track what entered the ERP and what still needs validation."));
            List<DataImportBatch> batches = snapshot?.ImportBatches ?? new List<DataImportBatch>();
            foreach (DataImportBatch batch in batches.Take(5))
                recent.Controls.Add(BuildRecentImportRow(batch.SourceFile ?? batch.ImportType ?? "Import batch", batch.Status ?? "Pending", batch.SuccessRows, batch.ErrorRows));
            if (batches.Count == 0)
                recent.Controls.Add(BuildRecentImportRow("No recent imports", "Ready", 0, 0));

            row.Controls.Add(warnings, 0, 0);
            row.Controls.Add(recent, 1, 0);
            return row;
        }

        private Control BuildOperationalFooter(MasterDataSnapshot snapshot)
        {
            Panel panel = CreateHubCard(78);
            int complete = snapshot?.SetupStatus?.Count(s => s.IsComplete) ?? 0;
            int total = Math.Max(1, snapshot?.SetupStatus?.Count ?? 1);
            int health = (int)Math.Round((complete * 100m) / total);
            panel.Controls.Add(new Label { Text = "Integration health", Location = new Point(24, 16), Size = new Size(150, 20), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate700 });
            panel.Controls.Add(new Label { Text = health + "%", Location = new Point(24, 36), Size = new Size(90, 28), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = health >= 80 ? SaveGreen : Color.FromArgb(249, 115, 22) });
            panel.Controls.Add(new Label { Text = "Data is available to Clients, Vendors, Quotations, Invoices, Purchases, Jobs, Payroll, Reports.", Location = new Point(170, 28), Size = new Size(760, 26), Font = new Font("Segoe UI", 9f), ForeColor = DS.Slate600 });
            return panel;
        }

        private async Task LoadAllAsync()
        {
            ShowStatus("Loading master data...", false);
            MasterDataSnapshot snapshot = await Task.Run(() => new MasterDataSnapshot
            {
                Clients = _clientSvc.GetAllClientsIncludingInactive(),
                Sites = _siteSvc.GetAll(),
                SetupStatus = _svc.GetSetupStatus(),
                Assets = _svc.GetAssets(),
                Documents = _svc.GetDocuments(),
                Rates = _svc.GetRateCards(),
                Connections = _svc.GetPrivateServerConnections(),
                ImportBatches = _svc.GetImportBatches()
            });

            _clients = snapshot.Clients;
            _sites = snapshot.Sites;
            if (_assetClient != null) BindClients(_assetClient, true);
            if (_docClient != null) BindClients(_docClient, true);
            if (_rateClient != null) BindClients(_rateClient, true);
            if (_assetClient != null && _assetSite != null) RefreshSiteCombo(_assetClient, _assetSite);
            if (_statusGrid != null) _statusGrid.DataSource = snapshot.SetupStatus;
            if (_assetGrid != null) _assetGrid.DataSource = snapshot.Assets;
            if (_docGrid != null) _docGrid.DataSource = snapshot.Documents;
            if (_rateGrid != null) _rateGrid.DataSource = snapshot.Rates;
            if (_serverGrid != null) _serverGrid.DataSource = snapshot.Connections;
            if (_importGrid != null) _importGrid.DataSource = snapshot.ImportBatches;
            RenderHub(snapshot);
            ShowStatus("Master data refreshed.", false);
        }

        private async void SaveAsset()
        {
            try
            {
                ClientAsset asset = _selectedAsset ?? new ClientAsset();
                asset.ClientId = SelectedClientId(_assetClient) ?? 0;
                asset.SiteId = SelectedSiteId(_assetSite);
                asset.EquipmentType = _assetType.Text.Trim();
                asset.AssetTag = _assetTag.Text.Trim();
                asset.Brand = _assetBrand.Text.Trim();
                asset.ModelNumber = _assetModel.Text.Trim();
                asset.SerialNumber = _assetSerial.Text.Trim();
                asset.Capacity = _assetCapacity.Text.Trim();
                asset.LocationDetail = _assetLocation.Text.Trim();
                asset.InstallDate = _assetInstallOn.Checked ? (DateTime?)_assetInstall.Value.Date : null;
                asset.WarrantyExpiry = _assetWarrantyOn.Checked ? (DateTime?)_assetWarranty.Value.Date : null;
                asset.IsAmcCovered = _assetAmc.Checked;
                asset.MaintenanceFrequency = "Quarterly";
                asset.Notes = _assetNotes.Text.Trim();
                asset.IsActive = true;
                _svc.SaveAsset(asset);
                ClearAssetForm();
                await LoadAllAsync();
            }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Saving asset", ex); }
        }

        private async void SaveDocument()
        {
            try
            {
                var doc = new ClientDocument
                {
                    ClientId = SelectedClientId(_docClient),
                    DocumentType = Convert.ToString(_docType.SelectedItem),
                    Title = _docTitle.Text.Trim(),
                    ExpiryDate = _docExpiryOn.Checked ? (DateTime?)_docExpiry.Value.Date : null,
                    Notes = _docNotes.Text.Trim()
                };
                _svc.SaveDocument(doc, _docPath.Text.Trim());
                _docTitle.Clear();
                _docPath.Clear();
                _docNotes.Clear();
                await LoadAllAsync();
            }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Saving document", ex); }
        }

        private async void SaveRate()
        {
            try
            {
                ServiceRateCard rate = _selectedRate ?? new ServiceRateCard();
                rate.ClientId = SelectedClientId(_rateClient);
                rate.Category = Convert.ToString(_rateCategory.SelectedItem);
                rate.ServiceName = _rateName.Text.Trim();
                rate.Unit = string.IsNullOrWhiteSpace(_rateUnit.Text) ? "Job" : _rateUnit.Text.Trim();
                rate.Rate = _rateAmount.Value;
                rate.GstPercent = _rateGst.Value;
                rate.IsEmergencyRate = _rateEmergency.Checked;
                rate.EffectiveFrom = _rateEffective.Value.Date;
                rate.Notes = _rateNotes.Text.Trim();
                rate.IsActive = true;
                _svc.SaveRateCard(rate);
                ClearRateForm();
                await LoadAllAsync();
            }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Saving rate", ex); }
        }

        private async void SaveConnection()
        {
            try
            {
                PrivateServerConnection connection = _selectedConnection ?? new PrivateServerConnection();
                connection.ConnectionName = _serverName.Text.Trim();
                connection.ServerType = Convert.ToString(_serverType.SelectedItem);
                connection.Host = _serverHost.Text.Trim();
                connection.Port = _serverPort.Value > 0 ? (int?)Convert.ToInt32(_serverPort.Value) : null;
                connection.DatabaseName = _serverDb.Text.Trim();
                connection.ApiBaseUrl = _serverApi.Text.Trim();
                connection.Username = _serverUser.Text.Trim();
                connection.SyncDirection = Convert.ToString(_syncDirection.SelectedItem);
                _svc.SavePrivateServerConnection(connection, _serverSecret.Text);
                ClearConnectionForm();
                await LoadAllAsync();
            }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Saving connection", ex); }
        }

        private void SelectAssetFromGrid()
        {
            ClientAsset asset = CurrentRow<ClientAsset>(_assetGrid);
            if (asset == null) return;
            _selectedAsset = asset;
            SetComboValue(_assetClient, asset.ClientId);
            RefreshSiteCombo(_assetClient, _assetSite);
            SetComboValue(_assetSite, asset.SiteId);
            _assetType.Text = asset.EquipmentType ?? "";
            _assetTag.Text = asset.AssetTag ?? "";
            _assetBrand.Text = asset.Brand ?? "";
            _assetModel.Text = asset.ModelNumber ?? "";
            _assetSerial.Text = asset.SerialNumber ?? "";
            _assetCapacity.Text = asset.Capacity ?? "";
            _assetLocation.Text = asset.LocationDetail ?? "";
            _assetInstallOn.Checked = asset.InstallDate.HasValue;
            if (asset.InstallDate.HasValue) _assetInstall.Value = asset.InstallDate.Value;
            _assetWarrantyOn.Checked = asset.WarrantyExpiry.HasValue;
            if (asset.WarrantyExpiry.HasValue) _assetWarranty.Value = asset.WarrantyExpiry.Value;
            _assetAmc.Checked = asset.IsAmcCovered;
            _assetNotes.Text = asset.Notes ?? "";
        }

        private void SelectRateFromGrid()
        {
            ServiceRateCard rate = CurrentRow<ServiceRateCard>(_rateGrid);
            if (rate == null) return;
            _selectedRate = rate;
            SetComboValue(_rateClient, rate.ClientId);
            _rateCategory.SelectedItem = string.IsNullOrWhiteSpace(rate.Category) ? "Other" : rate.Category;
            _rateName.Text = rate.ServiceName ?? "";
            _rateUnit.Text = rate.Unit ?? "";
            _rateAmount.Value = Clamp(rate.Rate, _rateAmount.Maximum);
            _rateGst.Value = Clamp(rate.GstPercent, _rateGst.Maximum);
            _rateEmergency.Checked = rate.IsEmergencyRate;
            _rateEffective.Value = rate.EffectiveFrom == default(DateTime) ? DateTime.Today : rate.EffectiveFrom;
            _rateNotes.Text = rate.Notes ?? "";
        }

        private void SelectConnectionFromGrid()
        {
            PrivateServerConnection connection = CurrentRow<PrivateServerConnection>(_serverGrid);
            if (connection == null) return;
            _selectedConnection = connection;
            _serverName.Text = connection.ConnectionName ?? "";
            _serverType.SelectedItem = string.IsNullOrWhiteSpace(connection.ServerType) ? "SQL Server" : connection.ServerType;
            _serverHost.Text = connection.Host ?? "";
            _serverPort.Value = connection.Port.HasValue ? connection.Port.Value : 0;
            _serverDb.Text = connection.DatabaseName ?? "";
            _serverApi.Text = connection.ApiBaseUrl ?? "";
            _serverUser.Text = connection.Username ?? "";
            _serverSecret.Clear();
            _syncDirection.SelectedItem = string.IsNullOrWhiteSpace(connection.SyncDirection) ? "Import only" : connection.SyncDirection;
        }

        private void ChooseDocumentFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose document";
                dialog.Filter = "Documents|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.csv;*.png;*.jpg;*.jpeg|All files|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _docPath.Text = dialog.FileName;
                    if (string.IsNullOrWhiteSpace(_docTitle.Text))
                        _docTitle.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        private void OpenSelectedDocument()
        {
            ClientDocument doc = CurrentRow<ClientDocument>(_docGrid);
            if (doc == null || string.IsNullOrWhiteSpace(doc.FilePath))
                return;
            try { Process.Start(doc.FilePath); }
            catch (Exception ex) { AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Opening document", ex); }
        }

        private void PreviewConnection()
        {
            var connection = new PrivateServerConnection
            {
                ConnectionName = _serverName.Text,
                ServerType = Convert.ToString(_serverType.SelectedItem),
                Host = _serverHost.Text,
                Port = _serverPort.Value > 0 ? (int?)Convert.ToInt32(_serverPort.Value) : null,
                DatabaseName = _serverDb.Text,
                ApiBaseUrl = _serverApi.Text
            };
            MessageBox.Show(_svc.BuildConnectionPreview(connection), "Private server connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearAssetForm()
        {
            _selectedAsset = null;
            _assetType.Clear(); _assetTag.Clear(); _assetBrand.Clear(); _assetModel.Clear(); _assetSerial.Clear();
            _assetCapacity.Clear(); _assetLocation.Clear(); _assetNotes.Clear();
            _assetInstallOn.Checked = false; _assetWarrantyOn.Checked = false; _assetAmc.Checked = false;
        }

        private void ClearRateForm()
        {
            _selectedRate = null;
            _rateName.Clear(); _rateUnit.Clear(); _rateAmount.Value = 0; _rateGst.Value = 18; _rateEmergency.Checked = false; _rateNotes.Clear();
        }

        private void ClearConnectionForm()
        {
            _selectedConnection = null;
            _serverName.Clear(); _serverHost.Clear(); _serverPort.Value = 0; _serverDb.Clear(); _serverApi.Clear(); _serverUser.Clear(); _serverSecret.Clear();
        }

        private void BindClients(ComboBox combo, bool includeBlank)
        {
            if (combo == null) return;
            object previous = combo.SelectedValue;
            var items = new List<ComboItem>();
            if (includeBlank) items.Add(new ComboItem { Id = 0, Name = "(Any / not assigned)" });
            items.AddRange(_clients.Select(c => new ComboItem { Id = c.ClientID, Name = c.CompanyName }));
            combo.DisplayMember = "Name";
            combo.ValueMember = "Id";
            combo.DataSource = items;
            if (previous != null) SetComboValue(combo, previous);
        }

        private void RefreshSiteCombo(ComboBox clientCombo, ComboBox siteCombo)
        {
            if (siteCombo == null) return;
            int? clientId = SelectedClientId(clientCombo);
            var items = new List<ComboItem> { new ComboItem { Id = 0, Name = "(No site)" } };
            items.AddRange(_sites.Where(s => !clientId.HasValue || s.ClientID == clientId.Value)
                .Select(s => new ComboItem { Id = s.SiteID, Name = SiteService.GetDisplayName(s) }));
            siteCombo.DisplayMember = "Name";
            siteCombo.ValueMember = "Id";
            siteCombo.DataSource = items;
        }

        private static int? SelectedClientId(ComboBox combo)
        {
            if (combo == null || combo.SelectedValue == null) return null;
            int id = Convert.ToInt32(combo.SelectedValue);
            return id > 0 ? (int?)id : null;
        }

        private static int? SelectedSiteId(ComboBox combo)
        {
            if (combo == null || combo.SelectedValue == null) return null;
            int id = Convert.ToInt32(combo.SelectedValue);
            return id > 0 ? (int?)id : null;
        }

        private static void SetComboValue(ComboBox combo, object value)
        {
            try { combo.SelectedValue = value ?? 0; } catch { }
        }

        private static T CurrentRow<T>(DataGridView grid) where T : class
        {
            if (grid == null || grid.CurrentRow == null)
                return null;
            return grid.CurrentRow.DataBoundItem as T;
        }

        private static decimal Clamp(decimal value, decimal max)
        {
            if (value < 0) return 0;
            return value > max ? max : value;
        }

        private static TableLayoutPanel MakeSplit()
        {
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            return layout;
        }

        private static DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = true,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            DS.StyleGrid(grid);
            grid.Margin = new Padding(0, 0, 14, 0);
            return grid;
        }

        private static FlowLayoutPanel MakeFormFlow()
        {
            FlowLayoutPanel form = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20),
                BackColor = Color.White,
                Margin = new Padding(14, 0, 0, 0)
            };
            form.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, form.Width - 1, form.Height - 1);
            };
            return form;
        }

        private static TextBox AddText(FlowLayoutPanel form, string label, bool multiline = false)
        {
            form.Controls.Add(FieldLabel(label));
            TextBox box = new TextBox { Width = 300, Height = multiline ? 86 : 32, Multiline = multiline, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 10) };
            form.Controls.Add(box);
            return box;
        }

        private static ComboBox AddCombo(FlowLayoutPanel form, string label)
        {
            form.Controls.Add(FieldLabel(label));
            ComboBox combo = new ComboBox { Width = 300, Height = 32, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 10) };
            form.Controls.Add(combo);
            return combo;
        }

        private static NumericUpDown AddNumber(FlowLayoutPanel form, string label, decimal maximum, int decimals)
        {
            form.Controls.Add(FieldLabel(label));
            NumericUpDown num = new NumericUpDown { Width = 170, Height = 32, Maximum = maximum, DecimalPlaces = decimals, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 10) };
            form.Controls.Add(num);
            return num;
        }

        private static DateTimePicker AddDate(FlowLayoutPanel form, string label, out CheckBox enabled)
        {
            form.Controls.Add(FieldLabel(label));
            FlowLayoutPanel row = new FlowLayoutPanel { Width = 300, Height = 36, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
            enabled = new CheckBox { Width = 28, Height = 28, Margin = new Padding(0, 3, 8, 0) };
            DateTimePicker date = new DateTimePicker { Width = 200, Height = 32, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9.5f) };
            row.Controls.Add(enabled);
            row.Controls.Add(date);
            form.Controls.Add(row);
            return date;
        }

        private static Label FieldLabel(string text)
        {
            return new Label { Text = text, Width = 300, Height = 20, ForeColor = DS.Slate700, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold), Margin = new Padding(0, 8, 0, 3) };
        }

        private static FlowLayoutPanel ActionRow(params Button[] buttons)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { Width = 310, Height = 84, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0, 12, 0, 8) };
            foreach (Button button in buttons)
            {
                button.Margin = new Padding(0, 0, 8, 0);
                row.Controls.Add(button);
            }
            return row;
        }

        private Panel CreateHubCard(int height)
        {
            Panel panel = new Panel { Height = height, BackColor = Color.White, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(0) };
            panel.Paint += (s, e) => DrawHubBorder(panel, e);
            return panel;
        }

        private static void DrawHubBorder(Control control, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, control.Width - 1, control.Height - 1), 12))
            using (Pen pen = new Pen(DS.Border))
                e.Graphics.DrawPath(pen, path);
        }

        private static Control SectionTitle(string title, string subtitle)
        {
            Panel panel = new Panel { Width = 760, Height = 56, Margin = new Padding(0, 0, 0, 12), BackColor = Color.Transparent };
            panel.Controls.Add(new Label { Text = title, Location = new Point(0, 0), Size = new Size(680, 24), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900 });
            panel.Controls.Add(new Label { Text = subtitle, Location = new Point(0, 26), Size = new Size(720, 24), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate500 });
            return panel;
        }

        private Control BuildUploadCard(string title, int count, string description, ExcelImportModule? module, string key)
        {
            Panel card = new Panel { Width = 250, Height = 142, BackColor = DS.BgPage, Margin = new Padding(0, 0, 12, 12), Cursor = Cursors.Hand, Tag = module };
            card.AllowDrop = true;
            card.DragEnter += HubDragEnter;
            card.DragDrop += HubDragDrop;
            card.Paint += (s, e) => DrawHubBorder(card, e);
            card.Controls.Add(new Label { Text = title, Location = new Point(16, 14), Size = new Size(160, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            card.Controls.Add(new Label { Text = count.ToString("N0") + " records", Location = new Point(178, 16), Size = new Size(60, 18), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = count > 0 ? SaveGreen : Color.FromArgb(249, 115, 22) });
            card.Controls.Add(new Label { Text = description, Location = new Point(16, 42), Size = new Size(210, 38), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate600 });

            Button primary = new Button { Text = module.HasValue ? "Import" : ResolveCardAction(key), Location = new Point(16, 94), Size = new Size(94, 30), FlatStyle = FlatStyle.Flat, BackColor = module.HasValue ? DS.Primary600 : DS.Slate100, ForeColor = module.HasValue ? Color.White : DS.Slate800, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            primary.FlatAppearance.BorderSize = 0;
            primary.Click += (s, e) => RunCardAction(module, key);
            Button map = new Button { Text = "Map / Validate", Location = new Point(118, 94), Size = new Size(112, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = DS.Primary700, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            map.FlatAppearance.BorderColor = DS.Border;
            map.Click += (s, e) => ShowMappingHint(title);
            card.Controls.Add(primary);
            card.Controls.Add(map);
            card.Click += (s, e) => RunCardAction(module, key);
            return card;
        }

        private Control BuildActionTile(string title, string subtitle, Color color, Action action)
        {
            Panel tile = new Panel { Width = 300, Height = 72, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 12), Cursor = Cursors.Hand };
            tile.Paint += (s, e) => DrawHubBorder(tile, e);
            Panel mark = new Panel { Location = new Point(14, 18), Size = new Size(36, 36), BackColor = DS.Lighten(color, 0.82f) };
            DS.Rounded(mark, 10);
            mark.Controls.Add(new Label { Text = "↻", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = color });
            tile.Controls.Add(mark);
            tile.Controls.Add(new Label { Text = title, Location = new Point(62, 14), Size = new Size(205, 22), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate900 });
            tile.Controls.Add(new Label { Text = subtitle, Location = new Point(62, 38), Size = new Size(215, 22), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500 });
            tile.Click += (s, e) => action?.Invoke();
            foreach (Control child in tile.Controls)
                child.Click += (s, e) => action?.Invoke();
            return tile;
        }

        private Control BuildWarningRow(string title, string action)
        {
            Panel row = new Panel { Width = 460, Height = 42, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
            row.Controls.Add(new Label { Text = "!", Location = new Point(0, 6), Size = new Size(28, 28), BackColor = Color.FromArgb(255, 247, 237), ForeColor = Color.FromArgb(234, 88, 12), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
            row.Controls.Add(new Label { Text = title, Location = new Point(40, 2), Size = new Size(190, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            row.Controls.Add(new Label { Text = action, Location = new Point(40, 21), Size = new Size(340, 18), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500 });
            return row;
        }

        private Control BuildRecentImportRow(string source, string status, int success, int failed)
        {
            Panel row = new Panel { Width = 460, Height = 48, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
            row.Controls.Add(new Label { Text = source, Location = new Point(0, 2), Size = new Size(245, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            row.Controls.Add(new Label { Text = status, Location = new Point(260, 2), Size = new Size(100, 20), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = failed > 0 ? DS.Red600 : SaveGreen });
            row.Controls.Add(new Label { Text = success + " synced | " + failed + " issues", Location = new Point(0, 24), Size = new Size(240, 18), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500 });
            return row;
        }

        private void HubDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void HubDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            string file = files == null ? null : files.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(file))
                return;
            ShowDroppedFileRouter(file);
        }

        private void ShowDroppedFileRouter(string file)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (ExcelImportModule module in new[] { ExcelImportModule.Clients, ExcelImportModule.Vendors, ExcelImportModule.Invoices, ExcelImportModule.Purchases, ExcelImportModule.Quotations, ExcelImportModule.Sites, ExcelImportModule.Jobs, ExcelImportModule.Employees })
                menu.Items.Add("Import as " + module, null, (s, e) => ImportFileAs(module, file));
            menu.Show(this, PointToClient(Cursor.Position));
        }

        private void ImportFileAs(ExcelImportModule module, string file)
        {
            try
            {
                if (!string.Equals(System.IO.Path.GetExtension(file), ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "This importer currently accepts Excel .xlsx files. PDFs/CSV/API files can be parked as documents or routed through templates until parser support is added.", "Import mapping", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ExcelImportResult result = new ExcelImportService().Import(module, file);
                MessageBox.Show(this, "Import complete.\r\nSynced: " + result.SuccessCount + "\r\nIssues: " + result.SkippedCount, "Import results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Importing file", ex);
            }
        }

        private void ShowBulkImportMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (ExcelImportModule module in new[] { ExcelImportModule.Clients, ExcelImportModule.Vendors, ExcelImportModule.Invoices, ExcelImportModule.Purchases, ExcelImportModule.Quotations, ExcelImportModule.Sites, ExcelImportModule.Jobs, ExcelImportModule.Employees })
                menu.Items.Add(module.ToString(), null, (s, e) => ImportUiHelper.RunImport(module, FindForm()));
            menu.Show(owner, new Point(12, owner.Height - 8));
        }

        private void ShowTemplateMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (ExcelImportModule module in new[] { ExcelImportModule.Clients, ExcelImportModule.Vendors, ExcelImportModule.Invoices, ExcelImportModule.Purchases, ExcelImportModule.Quotations, ExcelImportModule.Sites, ExcelImportModule.Jobs, ExcelImportModule.Employees })
                menu.Items.Add(module + " template", null, (s, e) => ImportUiHelper.DownloadTemplate(module, FindForm()));
            menu.Show(owner, new Point(12, 42));
        }

        private void RunCardAction(ExcelImportModule? module, string key)
        {
            if (module.HasValue)
            {
                ImportUiHelper.RunImport(module.Value, FindForm());
                _ = LoadAllAsync();
                return;
            }

            if (key == "company-templates") ShowCompanyTemplateManager();
            else if (key == "assets") ShowExistingTab(1);
            else if (key == "documents") ShowExistingTab(2);
            else if (key == "rates") ShowExistingTab(3);
            else ShowMappingHint(key);
        }

        private void ShowExistingTab(int index)
        {
            using (Form dialog = new Form { Text = "Master data details", StartPosition = FormStartPosition.CenterParent, Size = new Size(1180, 760), BackColor = DS.BgPage })
            {
                Control tabs = BuildTabs();
                tabs.Dock = DockStyle.Fill;
                dialog.Controls.Add(tabs);
                _tabs.SelectedIndex = Math.Max(0, Math.Min(index, _tabs.TabPages.Count - 1));
                _ = LoadAllAsync();
                dialog.ShowDialog(this);
            }
        }

        private void ShowMappingHint(string subject)
        {
            MessageBox.Show(this, "Mapping workflow for " + subject + ":\r\n\r\n1. Upload an Excel template or export.\r\n2. Validate required columns.\r\n3. Resolve duplicates and missing links.\r\n4. Sync records into ERP modules.", "Map / Validate", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowDuplicateCheck()
        {
            MessageBox.Show(this, "Duplicate checks scan company/vendor names, GST numbers, quote numbers, invoice numbers, and PO numbers before sync. Detailed duplicate resolution will open from this hub.", "Duplicate detection", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string ResolveCardAction(string key)
        {
            if (key == "assets") return "Add assets";
            if (key == "documents") return "Upload";
            if (key == "rates") return "Manage";
            if (key == "company-templates") return "Open";
            return "Configure";
        }

        private static int Count<T>(ICollection<T> items)
        {
            return items == null ? 0 : items.Count;
        }

        private int CountTable(string name)
        {
            MasterDataStatus status = _lastSnapshot?.SetupStatus?.FirstOrDefault(s => string.Equals(NormalizeStatusName(s.Category), NormalizeStatusName(name), StringComparison.OrdinalIgnoreCase));
            return status == null ? 0 : status.Count;
        }

        private int CountVendors()
        {
            return CountTable("Vendors");
        }

        private int CountCompanyTemplates()
        {
            try { return _templateManager.GetTemplates().Count; }
            catch { return 0; }
        }

        private void ShowCompanyTemplateManager()
        {
            using (var dialog = new CompanyTemplateManagerDialog(_templateManager))
                dialog.ShowDialog(FindForm());
            RenderHub(_lastSnapshot);
        }

        private static string NormalizeStatusName(string value)
        {
            value = (value ?? string.Empty).Replace("Company profile", "CompanySettings").Replace("HVAC assets", "ClientAssets").Replace("Rate cards", "ServiceRateCards").Replace("Inventory", "StockItems");
            return value.Replace(" ", "").ToLowerInvariant();
        }

        private static Button MakeButton(string text, Color color, int width, EventHandler click = null)
        {
            Button button = new Button { Text = text, Width = Math.Max(width, 104), Height = 36, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = DS.Lighten(color, 0.08f);
            button.FlatAppearance.MouseDownBackColor = DS.Darken(color, 0.08f);
            if (click != null) button.Click += click;
            return button;
        }

        private void ShowStatus(string text, bool isError)
        {
            if (_status == null) return;
            _status.Text = text;
            _status.ForeColor = isError ? Color.FromArgb(185, 28, 28) : DS.Slate500;
        }

        private sealed class CompanyTemplateManagerDialog : Form
        {
            private readonly CompanyTemplateManager _manager;
            private readonly DocumentTemplateRenderer _renderer = new DocumentTemplateRenderer();
            private ListBox _list;
            private ComboBox _type;
            private Label _status;
            private Label _recognition;
            private TextBox _mapping;
            private WebBrowser _preview;
            private CheckBox _default;
            private CheckBox _useInvoice;
            private CheckBox _useQuote;
            private CheckBox _usePo;
            private CheckBox _useReport;
            private CompanyDocumentTemplate _selected;

            public CompanyTemplateManagerDialog(CompanyTemplateManager manager)
            {
                _manager = manager;
                Text = "Company Document Templates";
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(1240, 780);
                MinimumSize = new Size(1040, 680);
                BackColor = DS.BgPage;
                Font = new Font("Segoe UI", 9f);
                Build();
                RefreshTemplates();
            }

            private void Build()
            {
                Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.White, Padding = new Padding(22, 12, 22, 8) };
                header.Controls.Add(new Label { Text = "Company Document Templates", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = DS.Slate900 });
                header.Controls.Add(new Label { Text = "Upload real invoice, quotation, PO, delivery note, letterhead, contract, and report formats once. ServoERP recognizes, maps, and reuses them across document generation.", Dock = DockStyle.Bottom, Height = 24, ForeColor = DS.Slate600 });
                Controls.Add(header);

                TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(18), BackColor = DS.BgPage };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
                Controls.Add(root);

                root.Controls.Add(BuildUploadAndListPanel(), 0, 0);
                root.Controls.Add(BuildRecognitionPanel(), 1, 0);
                root.Controls.Add(BuildPreviewPanel(), 2, 0);
            }

            private Control BuildUploadAndListPanel()
            {
                Panel panel = CardPanel();
                Panel drop = new Panel { Dock = DockStyle.Top, Height = 154, BackColor = Color.White, AllowDrop = true, Cursor = Cursors.Hand, Padding = new Padding(16) };
                drop.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Pen pen = new Pen(Color.FromArgb(147, 197, 253), 2) { DashStyle = DashStyle.Dash })
                        e.Graphics.DrawRectangle(pen, 8, 8, drop.Width - 17, drop.Height - 17);
                };
                drop.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
                drop.DragDrop += (s, e) => UploadFiles((string[])e.Data.GetData(DataFormats.FileDrop));
                drop.Click += (s, e) => PickAndUpload();
                drop.Controls.Add(new Label { Text = "Drop company template here", Dock = DockStyle.Top, Height = 34, TextAlign = ContentAlignment.BottomCenter, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate900 });
                drop.Controls.Add(new Label { Text = "PDF, Word, Excel, CSV, PNG, JPG, invoice/quotation samples", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = DS.Slate600 });
                panel.Controls.Add(drop);

                FlowLayoutPanel row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 12, 12, 8), BackColor = Color.White };
                _type = new ComboBox { Width = 176, DropDownStyle = ComboBoxStyle.DropDownList };
                _type.Items.AddRange(Enum.GetNames(typeof(CompanyDocumentTemplateType)));
                _type.SelectedItem = CompanyDocumentTemplateType.Invoice.ToString();
                row.Controls.Add(_type);
                row.Controls.Add(MakeButton("Upload", DS.Primary600, 96, (s, e) => PickAndUpload()));
                panel.Controls.Add(row);

                _list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f), IntegralHeight = false };
                _list.SelectedIndexChanged += (s, e) => SelectCurrent();
                panel.Controls.Add(_list);

                _status = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(12, 0, 12, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = DS.Slate600 };
                panel.Controls.Add(_status);
                return panel;
            }

            private Control BuildRecognitionPanel()
            {
                Panel panel = CardPanel();
                panel.Padding = new Padding(16);
                panel.Controls.Add(new Label { Text = "Recognition and field mapping", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = DS.Slate900 });
                _recognition = new Label { Dock = DockStyle.Top, Height = 150, ForeColor = DS.Slate600, Font = new Font("Segoe UI", 9f) };
                panel.Controls.Add(_recognition);

                FlowLayoutPanel toggles = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, FlowDirection = FlowDirection.TopDown, WrapContents = true };
                _default = new CheckBox { Text = "Set as default for this document type", Width = 260 };
                _useInvoice = new CheckBox { Text = "Use for Invoices", Width = 150 };
                _useQuote = new CheckBox { Text = "Use for Quotations", Width = 150 };
                _usePo = new CheckBox { Text = "Use for POs", Width = 150 };
                _useReport = new CheckBox { Text = "Use for Reports", Width = 150 };
                toggles.Controls.AddRange(new Control[] { _default, _useInvoice, _useQuote, _usePo, _useReport });
                panel.Controls.Add(toggles);

                panel.Controls.Add(new Label { Text = "Manual mapping (Field=Placeholder per line)", Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate700 });
                _mapping = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle };
                panel.Controls.Add(_mapping);

                FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(0, 10, 0, 0) };
                actions.Controls.Add(MakeButton("Save mapping", SaveGreen, 120, (s, e) => SaveSelected()));
                actions.Controls.Add(MakeButton("Set default", DS.Primary600, 110, (s, e) => SetDefault()));
                actions.Controls.Add(MakeButton("Open file", DS.Slate700, 92, (s, e) => OpenSelected()));
                panel.Controls.Add(actions);
                return panel;
            }

            private Control BuildPreviewPanel()
            {
                Panel panel = CardPanel();
                panel.Padding = new Padding(12);
                panel.Controls.Add(new Label { Text = "Recognized template preview", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = DS.Slate900 });
                _preview = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };
                panel.Controls.Add(_preview);
                return panel;
            }

            private void PickAndUpload()
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Upload company document template";
                    dialog.Filter = "Business templates|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.csv;*.png;*.jpg;*.jpeg;*.bmp|All files|*.*";
                    dialog.Multiselect = true;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        UploadFiles(dialog.FileNames);
                }
            }

            private void UploadFiles(string[] files)
            {
                CompanyDocumentTemplateType selectedType = ParseType(_type?.SelectedItem?.ToString());
                foreach (string file in files ?? new string[0])
                    _manager.UploadTemplate(file, selectedType);
                RefreshTemplates();
                _status.Text = "Template uploaded, recognized, and ready for mapping.";
            }

            private void RefreshTemplates()
            {
                List<CompanyDocumentTemplate> templates = _manager.GetTemplates();
                _list.Items.Clear();
                foreach (CompanyDocumentTemplate template in templates)
                    _list.Items.Add(new TemplateListItem(template));
                if (_list.Items.Count > 0)
                    _list.SelectedIndex = 0;
                else
                    _preview.DocumentText = "<html><body style='font-family:Segoe UI;padding:24px'>Upload a company template to start.</body></html>";
            }

            private void SelectCurrent()
            {
                _selected = (_list.SelectedItem as TemplateListItem)?.Template;
                if (_selected == null)
                    return;

                _default.Checked = _selected.IsDefault;
                _useInvoice.Checked = _selected.UseForInvoices;
                _useQuote.Checked = _selected.UseForQuotations;
                _usePo.Checked = _selected.UseForPurchaseOrders;
                _useReport.Checked = _selected.UseForReports;
                _recognition.Text = BuildRecognitionText(_selected);
                _mapping.Text = string.Join(Environment.NewLine, (_selected.Mapping?.Fields ?? new Dictionary<string, string>()).Select(kv => kv.Key + "=" + kv.Value));
                _preview.DocumentText = _renderer.BuildPreviewHtml(_selected);
            }

            private void SaveSelected()
            {
                if (_selected == null)
                    return;

                _selected.IsDefault = _default.Checked;
                _selected.UseForInvoices = _useInvoice.Checked;
                _selected.UseForQuotations = _useQuote.Checked;
                _selected.UseForPurchaseOrders = _usePo.Checked;
                _selected.UseForReports = _useReport.Checked;
                _selected.Mapping.Fields.Clear();
                foreach (string line in (_mapping.Text ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0)
                        _selected.Mapping.Fields[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
                _manager.SaveTemplate(_selected);
                RefreshTemplates();
                _status.Text = "Template mapping saved.";
            }

            private void SetDefault()
            {
                if (_selected == null)
                    return;
                _manager.SetDefault(_selected.TemplateId);
                RefreshTemplates();
                _status.Text = "Default template updated.";
            }

            private void OpenSelected()
            {
                if (_selected != null && File.Exists(_selected.StoredFilePath))
                    Process.Start(new ProcessStartInfo(_selected.StoredFilePath) { UseShellExecute = true });
            }

            private static string BuildRecognitionText(CompanyDocumentTemplate template)
            {
                TemplateRecognitionResult r = template.Recognition ?? new TemplateRecognitionResult();
                return "Type: " + template.DocumentType + "   Confidence: " + r.Confidence + "%\r\n"
                    + "Logo: " + Yes(r.LogoDetected) + " | Header: " + Yes(r.HeaderDetected) + " | Footer: " + Yes(r.FooterDetected) + "\r\n"
                    + "Address: " + Yes(r.AddressDetected) + " | GST/VAT: " + Yes(r.TaxFieldsDetected) + " | Bank: " + Yes(r.BankDetailsDetected) + "\r\n"
                    + "Terms: " + Yes(r.TermsDetected) + " | Signature: " + Yes(r.SignatureAreaDetected) + " | Item table: " + Yes(r.ItemTableDetected) + "\r\n"
                    + string.Join("\r\n", r.Warnings ?? new List<string>());
            }

            private static string Yes(bool value) => value ? "Yes" : "Map";

            private static CompanyDocumentTemplateType ParseType(string value)
            {
                CompanyDocumentTemplateType type;
                return Enum.TryParse(value, out type) ? type : CompanyDocumentTemplateType.Other;
            }

            private static Panel CardPanel()
            {
                Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 14, 0) };
                panel.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(DS.Border))
                        e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                };
                return panel;
            }

            private sealed class TemplateListItem
            {
                public TemplateListItem(CompanyDocumentTemplate template) { Template = template; }
                public CompanyDocumentTemplate Template { get; }
                public override string ToString()
                {
                    return (Template.IsDefault ? "* " : "") + Template.DocumentType + " - " + Template.TemplateName;
                }
            }
        }

        private sealed class ComboItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private sealed class MasterDataSnapshot
        {
            public List<B2BClient> Clients { get; set; }
            public List<ClientSite> Sites { get; set; }
            public List<MasterDataStatus> SetupStatus { get; set; }
            public List<ClientAsset> Assets { get; set; }
            public List<ClientDocument> Documents { get; set; }
            public List<ServiceRateCard> Rates { get; set; }
            public List<PrivateServerConnection> Connections { get; set; }
            public List<DataImportBatch> ImportBatches { get; set; }
        }
    }
}

