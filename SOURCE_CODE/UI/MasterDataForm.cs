using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        private readonly MasterDataService _svc = new MasterDataService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly MasterLookupService _lookupSvc = new MasterLookupService();
        private readonly CompanyTemplateManager _templateManager = new CompanyTemplateManager();
        private readonly FormTemplateLibraryService _formTemplateLibrary = new FormTemplateLibraryService();

        private TabControl _tabs;
        private DataGridView _statusGrid, _assetGrid, _docGrid, _rateGrid, _serverGrid, _importGrid, _lookupCategoryGrid, _lookupValueGrid;
        private ComboBox _assetClient, _assetSite, _docClient, _docType, _rateClient, _rateCategory, _serverType, _syncDirection, _lookupValueCategory;
        private TextBox _assetType, _assetTag, _assetBrand, _assetModel, _assetSerial, _assetCapacity, _assetLocation, _assetNotes;
        private DateTimePicker _assetInstall, _assetWarranty, _docExpiry, _rateEffective;
        private CheckBox _assetInstallOn, _assetWarrantyOn, _assetAmc, _docExpiryOn, _rateEmergency;
        private TextBox _docTitle, _docPath, _docNotes;
        private TextBox _rateName, _rateUnit, _rateNotes;
        private NumericUpDown _rateAmount, _rateGst, _serverPort;
        private TextBox _serverName, _serverHost, _serverDb, _serverApi, _serverUser, _serverSecret;
        private TextBox _lookupKey, _lookupModule, _lookupName, _lookupDescription, _lookupValueCode, _lookupValueText, _lookupValueDescription;
        private NumericUpDown _lookupSort, _lookupValueSort;
        private CheckBox _lookupActive, _lookupValueDefault, _lookupValueActive;
        private Label _status;
        private FlowLayoutPanel _hubFlow;
        private MasterDataSnapshot _lastSnapshot;
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<ClientSite> _sites = new List<ClientSite>();
        private bool _masterDataLoading;
        private Timer _masterDataLoadTimer;
        private ClientAsset _selectedAsset;
        private ServiceRateCard _selectedRate;
        private PrivateServerConnection _selectedConnection;
        private MasterLookupCategory _selectedLookupCategory;
        private MasterLookupValue _selectedLookupValue;

        private static readonly Color ActionBlue = DS.Indigo600;
        private static readonly Color SaveGreen = DS.Green500;
        private static readonly Color SoftTeal = DS.Indigo50;

        public MasterDataForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            DashboardRefreshService.RefreshRequested += DashboardRefreshService_RefreshRequested;
            EnableDeferredLoad(LoadAllAsync, ex => ShowStatus("Master data could not be loaded. Refresh setup checks and try again.", true));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DashboardRefreshService.RefreshRequested -= DashboardRefreshService_RefreshRequested;

            base.Dispose(disposing);
        }

        private void DashboardRefreshService_RefreshRequested(object sender, DashboardRefreshEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated || !Visible)
                return;

            BeginInvoke((Action)QueueMasterDataLoad);
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
                Padding = new Padding(18, 18, 18, 18)
            };
            _hubFlow.Resize += (s, e) => ResizeHubRows();

            root.Controls.Add(_hubFlow);
            root.Controls.Add(BuildHeader());
            Controls.Add(root);
            RenderHub(null);
        }

        private Control BuildHeader()
        {
            Button upload = MakeButton("Import Excel", Color.White, 138);
            upload.AutoEllipsis = true;
            upload.Click += (s, e) => ShowBulkImportMenu(upload);
            upload.ForeColor = DS.Slate800;
            upload.FlatAppearance.BorderSize = 1;
            upload.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(upload, ModernIconKind.Import);

            Button validate = MakeButton("Refresh Setup Checks", DS.Primary600, 174, async (s, e) => await LoadAllAsync());
            validate.AutoEllipsis = true;
            ModernIconSystem.AddButtonIcon(validate, ModernIconKind.Security);
            return SharedPageHeader.Build(new SharedPageHeaderModel
            {
                Name = "MasterDataPageHeader",
                Mode = SharedPageHeaderMode.Editor,
                Dock = DockStyle.Top,
                BackColor = DS.BgPage,
                Padding = new Padding(18, 16, 18, 10),
                Title = "Master Data",
                Subtitle = "Import one Excel file or a folder of workbooks and let ServoERP detect, clean, link, and sync the data automatically.",
                TitleWidth = 460,
                SubtitleWidth = 620,
                AllowCompactWrap = true,
                RightActions = new List<Control> { upload, validate }
            }).Header;
        }

        private Control BuildToolbar()
        {
            Panel bar = new Panel { Dock = DockStyle.Fill, Height = 58, BackColor = DS.BgPage, Padding = new Padding(18, 10, 18, 8) };
            Button refresh = MakeButton("Refresh Master Data", ActionBlue, 152);
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
            _tabs.TabPages.Add(BuildLookupsTab());
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
                MakeButton("Save asset", SaveGreen, 100, (s, e) => SaveAsset()),
                MakeButton("Deactivate", Color.White, 112, (s, e) => DeactivateSelectedAsset())));
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
            form.Controls.Add(ActionRow(
                MakeButton("Save document", SaveGreen, 126, (s, e) => SaveDocument()),
                MakeButton("Remove registration", Color.White, 166, (s, e) => RemoveSelectedDocument())));
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
                MakeButton("Save rate", SaveGreen, 92, (s, e) => SaveRate()),
                MakeButton("Deactivate", Color.White, 112, (s, e) => DeactivateSelectedRate())));
            split.Controls.Add(form, 1, 0);
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildLookupsTab()
        {
            TabPage tab = new TabPage("Lookups") { BackColor = DS.BgPage, Padding = new Padding(18) };
            TableLayoutPanel outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));

            TableLayoutPanel categories = MakeSplit();
            categories.ColumnStyles[0].Width = 58;
            _lookupCategoryGrid = MakeGrid();
            _lookupCategoryGrid.SelectionChanged += (s, e) => SelectLookupCategoryFromGrid();
            categories.Controls.Add(_lookupCategoryGrid, 0, 0);
            FlowLayoutPanel categoryForm = MakeFormFlow();
            _lookupKey = AddText(categoryForm, "Category key *");
            _lookupModule = AddText(categoryForm, "Module *");
            _lookupName = AddText(categoryForm, "Display name *");
            _lookupDescription = AddText(categoryForm, "Description", true);
            _lookupSort = AddNumber(categoryForm, "Sort order", 9999, 0);
            _lookupActive = new CheckBox { Text = "Active", Checked = true, Width = 220, Height = 24, Margin = new Padding(14, 6, 0, 0) };
            categoryForm.Controls.Add(_lookupActive);
            categoryForm.Controls.Add(ActionRow(
                MakeButton("New category", ActionBlue, 116, (s, e) => ClearLookupCategoryForm()),
                MakeButton("Save category", SaveGreen, 126, (s, e) => SaveLookupCategory()),
                MakeButton("Deactivate", Color.White, 112, (s, e) => DeactivateSelectedLookupCategory())));
            categories.Controls.Add(categoryForm, 1, 0);

            TableLayoutPanel values = MakeSplit();
            values.ColumnStyles[0].Width = 56;
            _lookupValueGrid = MakeGrid();
            _lookupValueGrid.SelectionChanged += (s, e) => SelectLookupValueFromGrid();
            values.Controls.Add(_lookupValueGrid, 0, 0);
            FlowLayoutPanel valueForm = MakeFormFlow();
            _lookupValueCategory = AddCombo(valueForm, "Category *");
            _lookupValueCategory.SelectedIndexChanged += (s, e) => LoadLookupValuesForSelectedCategory();
            _lookupValueCode = AddText(valueForm, "Value code *");
            _lookupValueText = AddText(valueForm, "Display text *");
            _lookupValueDescription = AddText(valueForm, "Description", true);
            _lookupValueSort = AddNumber(valueForm, "Sort order", 9999, 0);
            _lookupValueDefault = new CheckBox { Text = "Default value", Width = 220, Height = 24, Margin = new Padding(14, 6, 0, 0) };
            _lookupValueActive = new CheckBox { Text = "Active", Checked = true, Width = 220, Height = 24, Margin = new Padding(14, 6, 0, 0) };
            valueForm.Controls.Add(_lookupValueDefault);
            valueForm.Controls.Add(_lookupValueActive);
            valueForm.Controls.Add(ActionRow(
                MakeButton("New value", ActionBlue, 96, (s, e) => ClearLookupValueForm()),
                MakeButton("Save value", SaveGreen, 104, (s, e) => SaveLookupValue()),
                MakeButton("Deactivate", Color.White, 112, (s, e) => DeactivateSelectedLookupValue())));
            values.Controls.Add(valueForm, 1, 0);

            outer.Controls.Add(categories, 0, 0);
            outer.Controls.Add(values, 1, 0);
            tab.Controls.Add(outer);
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
                MakeButton("Deactivate", Color.White, 112, (s, e) => DeactivateSelectedConnection()),
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
                Text = "Recent import batches appear here. ServoERP now auto-detects the worksheet, maps columns, fixes safe data issues, and logs skipped rows for review.",
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
            _hubFlow.Controls.Add(BuildLookupCardsSection(snapshot));
            _hubFlow.Controls.Add(BuildTipBar());

            _hubFlow.ResumeLayout(true);
            ResizeHubRows();
        }

        private void ResizeHubRows()
        {
            if (_hubFlow == null)
                return;

            int width = Math.Max(720, _hubFlow.ClientSize.Width - _hubFlow.Padding.Left - _hubFlow.Padding.Right - 6);
            foreach (Control control in _hubFlow.Controls)
                control.Width = width;
        }

        private Control BuildHeroDropZone()
        {
            Panel panel = CreateHubCard(172);
            panel.AllowDrop = true;
            panel.DragEnter += HubDragEnter;
            panel.DragDrop += HubDragDrop;
            panel.Cursor = Cursors.Hand;
            panel.Click += (s, e) => ImportUiHelper.RunImport(FindForm());

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(24, 20, 24, 20),
                ColumnCount = 4,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel heroIcon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Backup, 86, DS.Primary50, DS.Primary700);
            heroIcon.Anchor = AnchorStyles.Left;
            layout.Controls.Add(heroIcon, 0, 0);

            Panel textBlock = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
            textBlock.Controls.Add(new Label
            {
                Text = "Data Control Center",
                Location = new Point(0, 0),
                Size = new Size(560, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = DS.Slate900
            });
            textBlock.Controls.Add(new Label
            {
                Text = "Drop an Excel workbook here.\r\nServoERP detects the data type, cleans messy columns,\r\nlinks master data, and imports safe rows automatically.",
                Location = new Point(0, 52),
                Size = new Size(520, 86),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = DS.Slate600
            });
            layout.Controls.Add(textBlock, 1, 0);

            TableLayoutPanel mini = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            mini.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mini.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            mini.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mini.Controls.Add(BuildHeroMiniBlock(ModernIconKind.Security, "Safe Import", "Clean and verify automatically", DS.Green50, DS.Green600), 0, 0);
            mini.Controls.Add(BuildHeroMiniBlock(ModernIconKind.Refresh, "Smart Sync", "Detect once, use everywhere", Color.FromArgb(245, 243, 255), Color.FromArgb(124, 58, 237)), 1, 0);
            layout.Controls.Add(mini, 2, 0);

            Control visual = BuildHeroVisual();
            layout.Controls.Add(visual, 3, 0);
            panel.Controls.Add(layout);
            return panel;
        }

        private Control BuildWorkflowStrip()
        {
            Panel panel = CreateHubCard(104);
            string[] steps = { "Upload", "Detect", "Clean", "Sync", "Use Across App" };
            string[] captions = { "Excel workbook", "Module + sheet", "Columns, links, defaults", "Safe rows only", "Quotes, invoices, jobs" };
            ModernIconKind[] icons = { ModernIconKind.Import, ModernIconKind.Security, ModernIconKind.Analytics, ModernIconKind.Refresh, ModernIconKind.Status };

            TableLayoutPanel strip = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(18, 12, 18, 12), BackColor = Color.Transparent };
            for (int i = 0; i < steps.Length; i++)
            {
                strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
                strip.Controls.Add(BuildWorkflowStep(i + 1, steps[i], captions[i], icons[i]), i, 0);
            }
            strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            panel.Controls.Add(strip);
            return panel;
        }

        private Control BuildHubMainRow(MasterDataSnapshot snapshot)
        {
            TableLayoutPanel row = new TableLayoutPanel { Height = 420, ColumnCount = 2, RowCount = 1, BackColor = DS.BgPage, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel uploads = CreateSurfaceCard(new Padding(18));
            FlowLayoutPanel uploadGrid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = Padding.Empty,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            uploadGrid.Resize += (s, e) => ResizeUploadCards(uploadGrid);
            Control uploadTitle = SectionTitle("Smart upload cards", "Choose what you are onboarding. ServoERP will validate and route records to the right module.");
            uploadGrid.Controls.Add(uploadTitle);
            foreach (ExcelImportModule module in ImportableModules())
                uploadGrid.Controls.Add(BuildUploadCard(GetUploadTitle(module), CountUploadRecords(module), GetUploadDescription(module), module, GetUploadKey(module)));
            uploads.Controls.Add(uploadGrid);
            ResizeUploadCards(uploadGrid);

            Panel actions = CreateSurfaceCard(new Padding(18)) as Panel;
            actions.Margin = new Padding(14, 0, 0, 0);
            TableLayoutPanel actionGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(0),
                Margin = Padding.Empty
            };
            actionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
            for (int i = 0; i < 5; i++)
                actionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 20f));
            Control actionTitle = SectionTitle("Sync command center", "One-click checks before data is used across the ERP.");
            actionTitle.Dock = DockStyle.Fill;
            actionGrid.Controls.Add(actionTitle, 0, 0);
            AddActionGridTile(actionGrid, BuildActionTile("Refresh all data", "Reload counts and integration status", ModernIconKind.Refresh, DS.Primary600, async () => await LoadAllAsync()), 1);
            AddActionGridTile(actionGrid, BuildActionTile("Download templates", "Get Excel formats for clean imports", ModernIconKind.Import, SaveGreen, () => ShowTemplateMenu(actions)), 2);
            AddActionGridTile(actionGrid, BuildActionTile("Duplicate check", "Find repeated clients, vendors, quotes", ModernIconKind.Filter, Color.FromArgb(249, 115, 22), () => ShowDuplicateCheck()), 3);
            AddActionGridTile(actionGrid, BuildActionTile("Open import log", "Review recent sync batches", ModernIconKind.Document, Color.FromArgb(124, 58, 237), () => ShowExistingTab(6)), 4);
            AddActionGridTile(actionGrid, BuildActionTile("Integration status", "Check API and app connectivity", ModernIconKind.Status, DS.Teal600, () => ShowExistingTab(5)), 5);
            actions.Controls.Add(actionGrid);

            row.Controls.Add(uploads, 0, 0);
            row.Controls.Add(actions, 1, 0);
            return row;
        }

        private Control BuildLookupCardsSection(MasterDataSnapshot snapshot)
        {
            Panel section = CreateSurfaceCard(new Padding(18)) as Panel;
            section.Height = 396;
            section.Margin = new Padding(0, 0, 0, 14);

            FlowLayoutPanel grid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = Padding.Empty,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            grid.Resize += (s, e) => ResizeLookupCards(grid);

            grid.Controls.Add(SectionTitle("Reference cards", "Manage missing master-data categories used by HR, payroll, service, purchase, inventory and sales documents."));
            foreach (LookupCardDefinition definition in LookupCardDefinitions())
                grid.Controls.Add(BuildLookupCard(definition, snapshot));

            section.Controls.Add(grid);
            ResizeLookupCards(grid);
            return section;
        }

        private Control BuildLookupCard(LookupCardDefinition definition, MasterDataSnapshot snapshot)
        {
            List<MasterLookupValue> values = (snapshot?.LookupValues ?? new List<MasterLookupValue>())
                .Where(v => string.Equals(v.CategoryKey, definition.CategoryKey, StringComparison.OrdinalIgnoreCase) && v.IsActive)
                .ToList();

            Panel card = new Panel
            {
                Width = 236,
                Height = 96,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 12, 12),
                Padding = new Padding(12),
                Cursor = Cursors.Hand,
                Tag = definition.CategoryKey
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12))
                using (SolidBrush fill = new SolidBrush(Color.White))
                using (Pen pen = new Pen(DS.Border))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(pen, path);
                }
                using (SolidBrush accent = new SolidBrush(Color.FromArgb(24, definition.Color)))
                    e.Graphics.FillRectangle(accent, 0, 0, 4, card.Height);
            };

            Control icon = ModernIconSystem.Badge(definition.Icon, 34, Color.FromArgb(245, 247, 251), definition.Color, 17);
            icon.Location = new Point(12, 14);
            icon.Cursor = Cursors.Hand;
            icon.Click += (s, e) => OpenLookupCard(definition.CategoryKey);

            Label title = new Label
            {
                Text = definition.Title,
                Location = new Point(56, 12),
                Size = new Size(142, 22),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            Label module = new Label
            {
                Text = definition.Module,
                Location = new Point(56, 35),
                Size = new Size(116, 18),
                Font = new Font("Segoe UI", 8.1f, FontStyle.Bold),
                ForeColor = definition.Color,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            Label count = new Label
            {
                Text = values.Count.ToString("N0") + " values",
                Location = new Point(56, 58),
                Size = new Size(96, 20),
                Font = new Font("Segoe UI", 8.6f),
                ForeColor = DS.Slate600,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            Label arrow = new Label
            {
                Text = ">",
                Location = new Point(card.Width - 30, 35),
                Size = new Size(18, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = DS.Slate400,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            foreach (Control child in new Control[] { title, module, count, arrow })
                child.Click += (s, e) => OpenLookupCard(definition.CategoryKey);
            card.Click += (s, e) => OpenLookupCard(definition.CategoryKey);
            card.Controls.AddRange(new Control[] { icon, title, module, count, arrow });
            return card;
        }

        private Control BuildTipBar()
        {
            Panel panel = CreateHubCard(58);
            panel.Margin = new Padding(0, 0, 0, 0);
            TableLayoutPanel row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(18, 11, 18, 11), BackColor = Color.Transparent };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            row.Controls.Add(ModernIconSystem.Badge(ModernIconKind.Alert, 28, DS.Primary100, DS.Primary700, 14), 0, 0);
            row.Controls.Add(new Label
            {
                Text = "Tip: Keep your master data clean and up to date for accurate reporting and smarter automation across ServoERP.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            }, 1, 0);
            panel.Controls.Add(row);
            return panel;
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
            panel.Controls.Add(new Label { Text = "Data is available to Clients, Sites, Vendors, Inventory, Purchases, Invoices, Payments, Quotations, Jobs, Employees, and Reports.", Location = new Point(170, 28), Size = new Size(900, 26), Font = new Font("Segoe UI", 9f), ForeColor = DS.Slate600 });
            return panel;
        }

        private Task LoadAllAsync()
        {
            QueueMasterDataLoad();
            return Task.CompletedTask;
        }

        private void QueueMasterDataLoad()
        {
            if (_masterDataLoading || _masterDataLoadTimer != null)
                return;

            _masterDataLoadTimer = new Timer { Interval = 1500 };
            _masterDataLoadTimer.Tick += (s, e) =>
            {
                _masterDataLoadTimer.Stop();
                _masterDataLoadTimer.Dispose();
                _masterDataLoadTimer = null;
                if (Visible && !IsDisposed)
                    StartMasterDataLoad();
            };
            _masterDataLoadTimer.Start();
        }

        private async void StartMasterDataLoad()
        {
            if (_masterDataLoading)
                return;

            _masterDataLoading = true;
            ShowStatus("Loading master data...", false);
            try
            {
                MasterDataSnapshot snapshot = await Task.Run(() =>
                {
                    TimeSpan ttl = TimeSpan.FromMinutes(2);
                    return new MasterDataSnapshot
                    {
                        Clients = _clientSvc.GetAllClientsIncludingInactive().ToList(),
                        Sites = _siteSvc.GetAll().ToList(),
                        SetupStatus = AppDataCache.GetOrCreate("masterdata:setup-status", ttl, () => _svc.GetSetupStatus() ?? new List<MasterDataStatus>()).ToList(),
                        Assets = AppDataCache.GetOrCreate("masterdata:assets", ttl, () => _svc.GetAssets() ?? new List<ClientAsset>()).ToList(),
                        Documents = AppDataCache.GetOrCreate("masterdata:documents", ttl, () => _svc.GetDocuments() ?? new List<ClientDocument>()).ToList(),
                        Rates = AppDataCache.GetOrCreate("masterdata:rates", ttl, () => _svc.GetRateCards() ?? new List<ServiceRateCard>()).ToList(),
                        Connections = AppDataCache.GetOrCreate("masterdata:connections", ttl, () => _svc.GetPrivateServerConnections() ?? new List<PrivateServerConnection>()).ToList(),
                        ImportBatches = AppDataCache.GetOrCreate("masterdata:import-batches", ttl, () => _svc.GetImportBatches() ?? new List<DataImportBatch>()).ToList(),
                        LookupCategories = _lookupSvc.GetCategories(true),
                        LookupValues = _lookupSvc.GetCategories(true)
                            .SelectMany(c => _lookupSvc.GetValues(c.CategoryKey, true))
                            .ToList()
                    };
                });

                RunOnUI(() =>
                {
                    _masterDataLoading = false;
                    snapshot = snapshot ?? new MasterDataSnapshot();
                    _lastSnapshot = snapshot;
                    _clients = snapshot.Clients ?? new List<B2BClient>();
                    _sites = snapshot.Sites ?? new List<ClientSite>();
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
                    if (_lookupCategoryGrid != null) _lookupCategoryGrid.DataSource = snapshot.LookupCategories;
                    BindLookupCategoryCombo(snapshot.LookupCategories);
                    LoadLookupValuesForSelectedCategory();
                    RenderHub(snapshot);
                    ShowStatus("Master data refreshed.", false);
                });
            }
            catch (Exception ex)
            {
                RunOnUI(() =>
                {
                    _masterDataLoading = false;
                    ShowStatus("Master data could not be loaded. Refresh setup checks and try again.", true);
                });
                ShowError("Failed to load master data. Please try again.", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Loading master data", ex);
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !_masterDataLoading && _lastSnapshot == null)
                QueueMasterDataLoad();
        }

        private void BindLookupCategoryCombo(List<MasterLookupCategory> categories)
        {
            if (_lookupValueCategory == null)
                return;

            int previousId = (_lookupValueCategory.SelectedItem as MasterLookupCategory)?.CategoryId ?? _selectedLookupCategory?.CategoryId ?? 0;
            var source = (categories ?? new List<MasterLookupCategory>())
                .OrderBy(c => c.ModuleKey)
                .ThenBy(c => c.SortOrder)
                .ThenBy(c => c.DisplayName)
                .ToList();
            _lookupValueCategory.DataSource = source;
            _lookupValueCategory.DisplayMember = "DisplayName";
            _lookupValueCategory.ValueMember = "CategoryId";
            if (previousId > 0)
            {
                for (int i = 0; i < _lookupValueCategory.Items.Count; i++)
                {
                    if ((_lookupValueCategory.Items[i] as MasterLookupCategory)?.CategoryId == previousId)
                    {
                        _lookupValueCategory.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void SelectLookupCategoryFromGrid()
        {
            MasterLookupCategory category = CurrentRow<MasterLookupCategory>(_lookupCategoryGrid);
            if (category == null)
                return;

            _selectedLookupCategory = category;
            _lookupKey.Text = category.CategoryKey ?? string.Empty;
            _lookupKey.ReadOnly = category.IsSystem || category.CategoryId > 0;
            _lookupModule.Text = category.ModuleKey ?? string.Empty;
            _lookupName.Text = category.DisplayName ?? string.Empty;
            _lookupDescription.Text = category.Description ?? string.Empty;
            _lookupSort.Value = Math.Max(_lookupSort.Minimum, Math.Min(_lookupSort.Maximum, category.SortOrder));
            _lookupActive.Checked = category.IsActive;
            SelectLookupCategoryInValueCombo(category.CategoryId);
            LoadLookupValuesForSelectedCategory();
        }

        private void SelectLookupCategoryInValueCombo(int categoryId)
        {
            if (_lookupValueCategory == null || categoryId <= 0)
                return;

            for (int i = 0; i < _lookupValueCategory.Items.Count; i++)
            {
                if ((_lookupValueCategory.Items[i] as MasterLookupCategory)?.CategoryId == categoryId)
                {
                    _lookupValueCategory.SelectedIndex = i;
                    return;
                }
            }
        }

        private void OpenLookupCard(string categoryKey)
        {
            ShowExistingTab(4);
            if (string.IsNullOrWhiteSpace(categoryKey))
                return;

            if (_lookupCategoryGrid != null)
            {
                foreach (DataGridViewRow row in _lookupCategoryGrid.Rows)
                {
                    MasterLookupCategory category = row.DataBoundItem as MasterLookupCategory;
                    if (category == null || !string.Equals(category.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    _lookupCategoryGrid.ClearSelection();
                    row.Selected = true;
                    _lookupCategoryGrid.CurrentCell = row.Cells[0];
                    SelectLookupCategoryFromGrid();
                    return;
                }
            }

            MasterLookupCategory fallback = (_lastSnapshot?.LookupCategories ?? new List<MasterLookupCategory>())
                .FirstOrDefault(c => string.Equals(c.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase));
            if (fallback != null)
            {
                _selectedLookupCategory = fallback;
                SelectLookupCategoryInValueCombo(fallback.CategoryId);
                LoadLookupValuesForSelectedCategory();
            }
        }

        private void SelectLookupValueFromGrid()
        {
            MasterLookupValue value = CurrentRow<MasterLookupValue>(_lookupValueGrid);
            if (value == null)
                return;

            _selectedLookupValue = value;
            SelectLookupCategoryInValueCombo(value.CategoryId);
            _lookupValueCode.Text = value.ValueCode ?? string.Empty;
            _lookupValueText.Text = value.DisplayText ?? string.Empty;
            _lookupValueDescription.Text = value.Description ?? string.Empty;
            _lookupValueSort.Value = Math.Max(_lookupValueSort.Minimum, Math.Min(_lookupValueSort.Maximum, value.SortOrder));
            _lookupValueDefault.Checked = value.IsDefault;
            _lookupValueActive.Checked = value.IsActive;
        }

        private void LoadLookupValuesForSelectedCategory()
        {
            if (_lookupValueGrid == null || _lookupValueCategory == null)
                return;

            MasterLookupCategory category = _lookupValueCategory.SelectedItem as MasterLookupCategory;
            if (category == null)
            {
                _lookupValueGrid.DataSource = new List<MasterLookupValue>();
                return;
            }

            _lookupValueGrid.DataSource = _lookupSvc.GetValues(category.CategoryKey, true);
        }

        private void ClearLookupCategoryForm()
        {
            _selectedLookupCategory = null;
            _lookupKey.ReadOnly = false;
            _lookupKey.Text = string.Empty;
            _lookupModule.Text = string.Empty;
            _lookupName.Text = string.Empty;
            _lookupDescription.Text = string.Empty;
            _lookupSort.Value = 0;
            _lookupActive.Checked = true;
        }

        private void ClearLookupValueForm()
        {
            _selectedLookupValue = null;
            _lookupValueCode.Text = string.Empty;
            _lookupValueText.Text = string.Empty;
            _lookupValueDescription.Text = string.Empty;
            _lookupValueSort.Value = 0;
            _lookupValueDefault.Checked = false;
            _lookupValueActive.Checked = true;
        }

        private async void SaveLookupCategory()
        {
            try
            {
                MasterLookupCategory category = _selectedLookupCategory ?? new MasterLookupCategory();
                category.CategoryKey = _lookupKey.Text.Trim();
                category.ModuleKey = _lookupModule.Text.Trim();
                category.DisplayName = _lookupName.Text.Trim();
                category.Description = _lookupDescription.Text.Trim();
                category.SortOrder = (int)_lookupSort.Value;
                category.IsActive = _lookupActive.Checked;
                int id = await Task.Run(() => _lookupSvc.SaveCategory(category));
                ShowStatus("Lookup category saved.", false);
                SessionManager.LogAction(category.CategoryId > 0 ? "EDIT" : "CREATE", "MasterData", id, "Lookup category saved");
                QueueMasterDataLoad();
            }
            catch (Exception ex)
            {
                ShowError("Lookup category could not be saved.", ex);
            }
        }

        private async void SaveLookupValue()
        {
            try
            {
                MasterLookupCategory category = _lookupValueCategory.SelectedItem as MasterLookupCategory;
                if (category == null)
                    throw new InvalidOperationException("Select a lookup category first.");

                MasterLookupValue value = _selectedLookupValue ?? new MasterLookupValue();
                value.CategoryId = category.CategoryId;
                value.ValueCode = _lookupValueCode.Text.Trim();
                value.DisplayText = _lookupValueText.Text.Trim();
                value.Description = _lookupValueDescription.Text.Trim();
                value.SortOrder = (int)_lookupValueSort.Value;
                value.IsDefault = _lookupValueDefault.Checked;
                value.IsActive = _lookupValueActive.Checked;
                int id = await Task.Run(() => _lookupSvc.SaveValue(value));
                ShowStatus("Lookup value saved.", false);
                SessionManager.LogAction(value.ValueId > 0 ? "EDIT" : "CREATE", "MasterData", id, "Lookup value saved");
                QueueMasterDataLoad();
            }
            catch (Exception ex)
            {
                ShowError("Lookup value could not be saved.", ex);
            }
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

        private async void DeactivateSelectedAsset()
        {
            ClientAsset asset = _selectedAsset ?? CurrentRow<ClientAsset>(_assetGrid);
            if (asset == null || asset.AssetId <= 0)
            {
                ShowStatus("Select an asset first.", true);
                return;
            }
            bool makeActive = !asset.IsActive;
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, (makeActive ? "Restore" : "Deactivate") + " this asset?", "The asset history stays in ServoERP. Inactive assets stop appearing as active equipment choices."))
                return;
            await Task.Run(() => _svc.SetAssetActive(asset.AssetId, makeActive));
            ClearAssetForm();
            await LoadAllAsync();
            ShowStatus(makeActive ? "Asset restored." : "Asset deactivated.", false);
        }

        private async void RemoveSelectedDocument()
        {
            ClientDocument doc = CurrentRow<ClientDocument>(_docGrid);
            if (doc == null || doc.DocumentId <= 0)
            {
                ShowStatus("Select a document first.", true);
                return;
            }
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, "Remove this document registration?", "The uploaded file is retained on disk. This only removes the document from Master Data."))
                return;
            await Task.Run(() => _svc.DeleteDocumentRegistration(doc.DocumentId));
            _docTitle.Clear();
            _docPath.Clear();
            _docNotes.Clear();
            await LoadAllAsync();
            ShowStatus("Document registration removed.", false);
        }

        private async void DeactivateSelectedRate()
        {
            ServiceRateCard rate = _selectedRate ?? CurrentRow<ServiceRateCard>(_rateGrid);
            if (rate == null || rate.RateId <= 0)
            {
                ShowStatus("Select a service rate first.", true);
                return;
            }
            bool makeActive = !rate.IsActive;
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, (makeActive ? "Restore" : "Deactivate") + " this rate?", "Inactive rates stop appearing as active service pricing choices."))
                return;
            await Task.Run(() => _svc.SetRateActive(rate.RateId, makeActive));
            ClearRateForm();
            await LoadAllAsync();
            ShowStatus(makeActive ? "Service rate restored." : "Service rate deactivated.", false);
        }

        private async void DeactivateSelectedConnection()
        {
            PrivateServerConnection connection = _selectedConnection ?? CurrentRow<PrivateServerConnection>(_serverGrid);
            if (connection == null || connection.ConnectionId <= 0)
            {
                ShowStatus("Select a connection first.", true);
                return;
            }
            bool makeActive = !connection.IsActive;
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, (makeActive ? "Restore" : "Deactivate") + " this connection?", "Inactive connections remain saved but are not treated as active integration targets."))
                return;
            await Task.Run(() => _svc.SetPrivateServerConnectionActive(connection.ConnectionId, makeActive));
            ClearConnectionForm();
            await LoadAllAsync();
            ShowStatus(makeActive ? "Connection restored." : "Connection deactivated.", false);
        }

        private async void DeactivateSelectedLookupCategory()
        {
            MasterLookupCategory category = _selectedLookupCategory ?? CurrentRow<MasterLookupCategory>(_lookupCategoryGrid);
            if (category == null || category.CategoryId <= 0)
            {
                ShowStatus("Select a lookup category first.", true);
                return;
            }
            bool makeActive = !category.IsActive;
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, (makeActive ? "Restore" : "Deactivate") + " this lookup category?", "Inactive lookup categories stay in the database but stop appearing in active choice lists."))
                return;
            await Task.Run(() => _lookupSvc.SetCategoryActive(category.CategoryId, makeActive));
            ClearLookupCategoryForm();
            await LoadAllAsync();
            ShowStatus(makeActive ? "Lookup category restored." : "Lookup category deactivated.", false);
        }

        private async void DeactivateSelectedLookupValue()
        {
            MasterLookupValue value = _selectedLookupValue ?? CurrentRow<MasterLookupValue>(_lookupValueGrid);
            if (value == null || value.ValueId <= 0)
            {
                ShowStatus("Select a lookup value first.", true);
                return;
            }
            bool makeActive = !value.IsActive;
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, (makeActive ? "Restore" : "Deactivate") + " this lookup value?", "Inactive values stay available for old records but stop appearing as active choices."))
                return;
            await Task.Run(() => _lookupSvc.SetValueActive(value.ValueId, makeActive));
            ClearLookupValueForm();
            await LoadAllAsync();
            ShowStatus(makeActive ? "Lookup value restored." : "Lookup value deactivated.", false);
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
            RecentDocumentOpenService.OpenStoredFile(this, doc.FilePath, BrandingService.WindowTitle("Master Data"));
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
            try
            {
                combo.SelectedValue = value ?? 0;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MasterDataForm.SetComboValue", ex);
            }
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
            form.Resize += (s, e) => ResizeFormFlow(form);
            return form;
        }

        private static TextBox AddText(FlowLayoutPanel form, string label, bool multiline = false)
        {
            form.Controls.Add(FieldLabel(label));
            TextBox box = new TextBox { Width = 300, Height = multiline ? 86 : 32, Multiline = multiline, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 10), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            form.Controls.Add(box);
            return box;
        }

        private static ComboBox AddCombo(FlowLayoutPanel form, string label)
        {
            form.Controls.Add(FieldLabel(label));
            ComboBox combo = new ComboBox { Width = 300, Height = 32, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 10), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            form.Controls.Add(combo);
            return combo;
        }

        private static NumericUpDown AddNumber(FlowLayoutPanel form, string label, decimal maximum, int decimals)
        {
            form.Controls.Add(FieldLabel(label));
            NumericUpDown num = new NumericUpDown { Width = 170, Height = 32, Maximum = maximum, DecimalPlaces = decimals, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(0, 0, 0, 10), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            form.Controls.Add(num);
            return num;
        }

        private static DateTimePicker AddDate(FlowLayoutPanel form, string label, out CheckBox enabled)
        {
            form.Controls.Add(FieldLabel(label));
            FlowLayoutPanel row = new FlowLayoutPanel { Width = 300, Height = 36, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 10), Tag = "DateFieldRow" };
            enabled = new CheckBox { Width = 28, Height = 28, Margin = new Padding(0, 3, 8, 0) };
            DateTimePicker date = new DateTimePicker { Width = 200, Height = 32, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9.5f), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            row.Controls.Add(enabled);
            row.Controls.Add(date);
            form.Controls.Add(row);
            return date;
        }

        private static Label FieldLabel(string text)
        {
            bool required = (text ?? string.Empty).Contains("*");
            return new Label { Text = text, Width = 300, Height = 20, ForeColor = required ? DS.Primary700 : DS.Slate700, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold), Margin = new Padding(0, 8, 0, 3), AutoEllipsis = true };
        }

        private static FlowLayoutPanel ActionRow(params Button[] buttons)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { Width = 310, Height = 84, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0, 12, 0, 8), Tag = "ActionRow" };
            foreach (Button button in buttons)
            {
                button.Margin = new Padding(0, 0, 8, 0);
                button.AutoEllipsis = true;
                row.Controls.Add(button);
            }
            return row;
        }

        private static void ResizeFormFlow(FlowLayoutPanel form)
        {
            if (form == null || form.IsDisposed)
                return;

            int width = Math.Max(220, Math.Min(330, form.ClientSize.Width - form.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8));
            foreach (Control control in form.Controls)
            {
                if (control is Label || control is TextBox || control is ComboBox)
                {
                    control.Width = width;
                    continue;
                }

                NumericUpDown number = control as NumericUpDown;
                if (number != null)
                {
                    number.Width = Math.Min(width, 190);
                    continue;
                }

                FlowLayoutPanel row = control as FlowLayoutPanel;
                if (row == null)
                    continue;

                row.Width = Math.Max(width, 220);
                if (Convert.ToString(row.Tag) == "DateFieldRow")
                {
                    DateTimePicker date = row.Controls.OfType<DateTimePicker>().FirstOrDefault();
                    if (date != null)
                        date.Width = Math.Max(150, row.Width - 44);
                }
            }
        }

        private Panel CreateHubCard(int height)
        {
            Panel panel = new Panel { Height = height, BackColor = Color.White, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(0) };
            panel.Paint += (s, e) => DrawHubBorder(panel, e);
            return panel;
        }

        private static Panel CreateSurfaceCard(Padding padding)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = padding, Margin = new Padding(0) };
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
            Panel panel = new Panel { Width = 760, Height = 60, Margin = new Padding(0, 0, 0, 10), BackColor = Color.Transparent, Tag = "SectionTitle" };
            var titleLabel = new Label { Text = title, Location = new Point(0, 0), Size = new Size(680, 26), Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            var subtitleLabel = new Label { Text = subtitle, Location = new Point(0, 30), Size = new Size(720, 24), Font = new Font("Segoe UI", 8.75f), ForeColor = DS.Slate600, AutoEllipsis = true };
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(subtitleLabel);
            panel.Resize += (s, e) =>
            {
                titleLabel.Width = Math.Max(180, panel.ClientSize.Width - 8);
                subtitleLabel.Width = Math.Max(180, panel.ClientSize.Width - 8);
            };
            return panel;
        }

        private Control BuildUploadCard(string title, int count, string description, ExcelImportModule? module, string key)
        {
            Panel card = new Panel { Width = 220, Height = 146, BackColor = DS.Slate50, Margin = new Padding(0, 0, 12, 12), Cursor = Cursors.Hand, Tag = module };
            card.AllowDrop = true;
            card.DragEnter += HubDragEnter;
            card.DragDrop += HubDragDrop;
            card.Paint += (s, e) => DrawHubBorder(card, e);
            ModernIconKind iconKind = ModernIconSystem.KindForTitle(title);
            Color accent = UploadAccent(key);
            card.Controls.Add(ModernIconSystem.Badge(iconKind, 38, DS.Lighten(accent, 0.84f), accent, 10));
            card.Controls[0].Location = new Point(14, 14);
            card.Controls.Add(new Label { Text = ShortUploadTitle(title), Location = new Point(60, 14), Size = new Size(92, 22), Font = new Font("Segoe UI", 8.75f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = count.ToString("N0") + " recs", Location = new Point(card.Width - 75, 16), Size = new Size(64, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = count > 0 ? SaveGreen : Color.FromArgb(249, 115, 22), AutoEllipsis = true });
            card.Controls.Add(new Label { Text = description, Location = new Point(60, 40), Size = new Size(card.Width - 74, 42), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 7.8f), ForeColor = DS.Slate600, AutoEllipsis = true });

            Button primary = new Button { Text = module.HasValue ? "Import" : ResolveCardAction(key), Location = new Point(14, 102), Size = new Size(96, 32), FlatStyle = FlatStyle.Flat, BackColor = module.HasValue ? DS.Primary600 : DS.Slate100, ForeColor = module.HasValue ? Color.White : DS.Slate800, Font = new Font("Segoe UI", 7.75f, FontStyle.Bold), Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            primary.AutoEllipsis = true;
            primary.FlatAppearance.BorderSize = 0;
            DS.Rounded(primary, DS.RadiusSm);
            primary.Click += (s, e) => RunCardAction(module, key);
            Button map = new Button { Text = "Auto Sync", Location = new Point(128, 102), Size = new Size(78, 32), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = DS.Slate800, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            map.AutoEllipsis = true;
            map.FlatAppearance.BorderColor = DS.Border;
            DS.Rounded(map, DS.RadiusSm);
            map.Click += (s, e) => RunMappingAction(module, key);
            card.Controls.Add(primary);
            card.Controls.Add(map);
            card.Resize += (s, e) =>
            {
                primary.Location = new Point(14, card.Height - 44);
                map.Location = new Point(card.Width - map.Width - 14, card.Height - 44);
            };
            card.Click += (s, e) => RunCardAction(module, key);
            return card;
        }

        private void ResizeLookupCards(FlowLayoutPanel grid)
        {
            if (grid == null || grid.IsDisposed)
                return;

            int width = Math.Max(220, grid.ClientSize.Width - grid.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
            Control title = grid.Controls.Cast<Control>().FirstOrDefault(c => Convert.ToString(c.Tag) == "SectionTitle");
            if (title != null)
                title.Width = width;

            int columns = width >= 1180 ? 5 : width >= 930 ? 4 : width >= 690 ? 3 : 2;
            int cardWidth = Math.Max(204, (width - ((columns - 1) * 12)) / columns);
            foreach (Control card in grid.Controls)
            {
                if (Convert.ToString(card.Tag) == "SectionTitle")
                    continue;
                card.Width = cardWidth;
            }
        }

        private static string ShortUploadTitle(string title)
        {
            switch ((title ?? string.Empty).Trim())
            {
                case "Equipment / Assets": return "Equipment";
                case "Documents / PDFs": return "Documents";
                case "Company Document": return "Company";
                default: return title;
            }
        }

        private Control BuildActionTile(string title, string subtitle, ModernIconKind iconKind, Color color, Action action)
        {
            Panel tile = new Panel { Width = 300, Height = 58, BackColor = DS.Slate50, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand, Tag = "ActionTile" };
            tile.Paint += (s, e) => DrawHubBorder(tile, e);
            Label mark = ModernIconSystem.Badge(iconKind, 34, DS.Lighten(color, 0.84f), color, 9);
            mark.Location = new Point(14, 12);
            tile.Controls.Add(mark);
            tile.Controls.Add(new Label { Text = title, Location = new Point(62, 9), Size = new Size(205, 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            tile.Controls.Add(new Label { Text = subtitle, Location = new Point(62, 31), Size = new Size(205, 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 7.8f), ForeColor = DS.Slate500, AutoEllipsis = true });
            Label chevron = ModernIconSystem.Icon(ModernIconKind.ChevronDown, 14, DS.Slate500);
            chevron.Text = ">";
            chevron.Location = new Point(tile.Width - 28, 17);
            chevron.Size = new Size(18, 24);
            chevron.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            tile.Controls.Add(chevron);
            tile.Click += (s, e) => action?.Invoke();
            foreach (Control child in tile.Controls)
                child.Click += (s, e) => action?.Invoke();
            return tile;
        }

        private Control BuildHeroMiniBlock(ModernIconKind iconKind, string title, string subtitle, Color backColor, Color foreColor)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(8, 20, 8, 20) };
            Control icon = ModernIconSystem.Badge(iconKind, 42, backColor, foreColor, 12);
            icon.Location = new Point(8, 28);
            panel.Controls.Add(icon);
            panel.Controls.Add(new Label { Text = title, Location = new Point(62, 24), Size = new Size(140, 22), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = subtitle, Location = new Point(62, 48), Size = new Size(142, 22), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate600, AutoEllipsis = true });
            return panel;
        }

        private Control BuildHeroVisual()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(6, 0, 0, 0) };
            Panel doc = new Panel { Size = new Size(96, 82), Location = new Point(18, 8), BackColor = DS.Primary50 };
            DS.Rounded(doc, 12);
            doc.Controls.Add(new Label { Text = "XLS", Location = new Point(10, 10), Size = new Size(42, 22), BackColor = SaveGreen, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8f, FontStyle.Bold) });
            doc.Controls.Add(new Label { Text = "Data\nSync", Location = new Point(18, 38), Size = new Size(70, 38), ForeColor = DS.Primary700, Font = new Font("Segoe UI", 10f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            Panel cloud = ModernIconSystem.EmptyStateIcon(ModernIconKind.Import, 48, DS.Green50, SaveGreen);
            cloud.Location = new Point(104, 50);
            panel.Controls.Add(doc);
            panel.Controls.Add(cloud);
            return panel;
        }

        private Control BuildWorkflowStep(int number, string title, string caption, ModernIconKind iconKind)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0, 0, 10, 0) };
            Panel numberBadge = new Panel { Size = new Size(30, 30), Location = new Point(2, 28), BackColor = Color.White };
            numberBadge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, 29, 29), 15))
                using (Pen pen = new Pen(DS.Primary100))
                    e.Graphics.DrawPath(pen, path);
            };
            numberBadge.Controls.Add(new Label { Text = number.ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Primary700 });
            panel.Controls.Add(numberBadge);
            Control icon = ModernIconSystem.Badge(iconKind, 48, number == 2 ? DS.Amber50 : number == 3 ? Color.FromArgb(245, 243, 255) : number == 5 ? DS.Green50 : DS.Primary50, number == 2 ? DS.Amber600 : number == 3 ? Color.FromArgb(124, 58, 237) : number == 5 ? SaveGreen : DS.Primary700, 14);
            icon.Location = new Point(44, 20);
            panel.Controls.Add(icon);
            panel.Controls.Add(new Label { Text = title, Location = new Point(104, 20), Size = new Size(128, 24), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = caption, Location = new Point(104, 46), Size = new Size(142, 42), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate600, AutoEllipsis = true });
            return panel;
        }

        private static void ResizeUploadCards(FlowLayoutPanel flow)
        {
            if (flow == null)
                return;

            int available = Math.Max(720, flow.ClientSize.Width - 4);
            int columns = available >= 920 ? 4 : available >= 690 ? 3 : 2;
            int gap = 12;
            int cardWidth = Math.Max(190, (available - (columns - 1) * gap - 2) / columns);
            foreach (Control control in flow.Controls)
            {
                if ((control.Tag as string) == "SectionTitle")
                {
                    control.Width = available;
                    continue;
                }

                control.Width = cardWidth;
            }
        }

        private static void ResizeActionTiles(FlowLayoutPanel flow)
        {
            if (flow == null)
                return;

            int width = Math.Max(260, flow.ClientSize.Width - 4);
            foreach (Control control in flow.Controls)
                control.Width = width;
        }

        private static void AddUploadGridCard(TableLayoutPanel grid, Control card, int column, int row)
        {
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, column == 3 ? 0 : 12, row == 2 ? 0 : 12);
            grid.Controls.Add(card, column, row);
        }

        private static void AddActionGridTile(TableLayoutPanel grid, Control tile, int row)
        {
            tile.Dock = DockStyle.Fill;
            tile.Margin = new Padding(0, 0, 0, row == 5 ? 0 : 8);
            grid.Controls.Add(tile, 0, row);
        }

        private static Color UploadAccent(string key)
        {
            switch ((key ?? string.Empty).ToLowerInvariant())
            {
                case "clients": return DS.Teal600;
                case "vendors": return SaveGreen;
                case "sites": return Color.FromArgb(14, 165, 233);
                case "invoices": return DS.Primary600;
                case "payments": return Color.FromArgb(22, 163, 74);
                case "purchases": return Color.FromArgb(217, 119, 6);
                case "quotations": return Color.FromArgb(99, 102, 241);
                case "jobs": return Color.FromArgb(220, 38, 38);
                case "employees": return Color.FromArgb(8, 145, 178);
                case "inventory": return Color.FromArgb(124, 58, 237);
                case "contracts": return SaveGreen;
                case "assets": return Color.FromArgb(249, 115, 22);
                case "rates": return DS.Teal600;
                case "documents": return DS.Red600;
                case "company-templates": return DS.Primary700;
                default: return DS.Primary600;
            }
        }

        private Control BuildWarningRow(string title, string action)
        {
            Panel row = new Panel { Width = 460, Height = 42, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
            row.Controls.Add(new Label { Text = "!", Location = new Point(0, 6), Size = new Size(28, 28), BackColor = Color.FromArgb(255, 247, 237), ForeColor = Color.FromArgb(234, 88, 12), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
            var titleLabel = new Label { Text = title, Location = new Point(40, 2), Size = new Size(190, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            var actionLabel = new Label { Text = action, Location = new Point(40, 21), Size = new Size(340, 18), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500, AutoEllipsis = true };
            row.Controls.Add(titleLabel);
            row.Controls.Add(actionLabel);
            row.Resize += (s, e) =>
            {
                titleLabel.Width = Math.Max(120, row.ClientSize.Width - 48);
                actionLabel.Width = Math.Max(120, row.ClientSize.Width - 48);
            };
            return row;
        }

        private Control BuildRecentImportRow(string source, string status, int success, int failed)
        {
            Panel row = new Panel { Width = 460, Height = 48, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 8) };
            var sourceLabel = new Label { Text = source, Location = new Point(0, 2), Size = new Size(245, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            var statusLabel = new Label { Text = status, Location = new Point(260, 2), Size = new Size(100, 20), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = failed > 0 ? DS.Red600 : SaveGreen, AutoEllipsis = true };
            var countLabel = new Label { Text = success + " synced | " + failed + " issues", Location = new Point(0, 24), Size = new Size(240, 18), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500, AutoEllipsis = true };
            row.Controls.Add(sourceLabel);
            row.Controls.Add(statusLabel);
            row.Controls.Add(countLabel);
            row.Resize += (s, e) =>
            {
                int statusWidth = Math.Min(110, Math.Max(80, row.ClientSize.Width / 4));
                statusLabel.SetBounds(Math.Max(0, row.ClientSize.Width - statusWidth - 8), 2, statusWidth, 20);
                sourceLabel.Width = Math.Max(120, statusLabel.Left - 12);
                countLabel.Width = Math.Max(120, row.ClientSize.Width - 8);
            };
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
            if (Directory.Exists(file))
                ShowDroppedFolderRouter(file);
            else
                ShowDroppedFileRouter(file);
        }

        private void ShowDroppedFileRouter(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
                return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Auto-detect this Excel file", null, (s, e) => ImportUiHelper.RunImportFile(file, null, ResolveDialogOwner()));
            menu.Items.Add(new ToolStripSeparator());
            foreach (ExcelImportModule module in ImportableModules())
                menu.Items.Add("Import as " + GetUploadTitle(module), null, (s, e) => ImportFileAs(module, file));

            if (!TryShowMenu(menu, this, PointToClient(Cursor.Position)))
                ShowStatus("Master Data page is refreshing. Please try the import again.", true);
        }

        private void ShowDroppedFolderRouter(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Auto-detect Excel files in this folder", null, (s, e) => ImportFolderAs(null, folder));
            menu.Items.Add(new ToolStripSeparator());
            foreach (ExcelImportModule module in ImportableModules())
                menu.Items.Add("Import folder as " + GetUploadTitle(module), null, (s, e) => ImportFolderAs(module, folder));

            if (!TryShowMenu(menu, this, PointToClient(Cursor.Position)))
                ShowStatus("Master Data page is refreshing. Please try the folder import again.", true);
        }

        private void ImportFileAs(ExcelImportModule module, string file)
        {
            try
            {
                ImportUiHelper.RunImportFile(file, module, ResolveDialogOwner());
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Importing file", ex);
            }
        }

        private void ImportFolderAs(ExcelImportModule? module, string folder)
        {
            try
            {
                ImportUiHelper.RunImportFolder(module, folder, ResolveDialogOwner());
                _ = LoadAllAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Importing folder", ex);
            }
        }

        private void ShowBulkImportMenu(Control owner)
        {
            if (!CanUseControl(owner))
            {
                ShowStatus("Master Data page is refreshing. Please try again.", true);
                return;
            }

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Auto-detect from Excel", null, (s, e) => ImportUiHelper.RunImport(ResolveDialogOwner()));
            menu.Items.Add("Auto-detect from Folder", null, (s, e) => RunFolderImport(null));
            menu.Items.Add(new ToolStripSeparator());
            foreach (ExcelImportModule module in ImportableModules())
            {
                menu.Items.Add("Upload " + GetUploadTitle(module), null, (s, e) => RunModuleImport(module));
                menu.Items.Add("Upload " + GetUploadTitle(module) + " Folder", null, (s, e) => RunFolderImport(module));
            }

            if (!TryShowMenu(menu, owner, new Point(12, owner.Height - 8)))
                ShowStatus("Master Data page is refreshing. Please try again.", true);
        }

        private void ShowTemplateMenu(Control owner)
        {
            if (!CanUseControl(owner))
            {
                ShowStatus("Master Data page is refreshing. Please try again.", true);
                return;
            }

            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (ExcelImportModule module in ImportableModules())
                menu.Items.Add(GetUploadTitle(module) + " template", null, (s, e) => ImportUiHelper.DownloadTemplate(module, ResolveDialogOwner()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Open field-service form library", null, (s, e) => OpenFieldServiceFormLibrary());
            menu.Items.Add("Open field-service template ZIP", null, (s, e) => OpenFieldServiceTemplateZip());
            menu.Items.Add("Show field-service library summary", null, (s, e) => ShowFieldServiceLibrarySummary());

            if (!TryShowMenu(menu, owner, new Point(12, 42)))
                ShowStatus("Master Data page is refreshing. Please try again.", true);
        }

        private void OpenFieldServiceFormLibrary()
        {
            try
            {
                if (!_formTemplateLibrary.IsAvailable)
                {
                    MessageBox.Show(this, "Field-service form library was not found at:\r\n" + _formTemplateLibrary.RootFolder, "Form Template Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo(_formTemplateLibrary.RootFolder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Opening form template library", ex);
            }
        }

        private void OpenFieldServiceTemplateZip()
        {
            try
            {
                if (!File.Exists(_formTemplateLibrary.ZipPath))
                {
                    MessageBox.Show(this, "Field-service template ZIP was not found at:\r\n" + _formTemplateLibrary.ZipPath, "Form Template Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo(_formTemplateLibrary.ZipPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Opening form template ZIP", ex);
            }
        }

        private void ShowFieldServiceLibrarySummary()
        {
            try
            {
                if (!_formTemplateLibrary.IsAvailable)
                {
                    MessageBox.Show(this, "Field-service form library has not been generated yet.", "Form Template Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string body = _formTemplateLibrary.BuildSummary() + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, _formTemplateLibrary.CountByTrade().Select(kv => kv.Key + ": " + kv.Value))
                    + Environment.NewLine + Environment.NewLine
                    + "Use these templates in Jobs, Service Desk, Contracts, Inventory, Purchases, Finance, and Compliance workflows.";

                MessageBox.Show(this, body, "Field-Service Form Template Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Reading form template library", ex);
            }
        }

        private void RunCardAction(ExcelImportModule? module, string key)
        {
            if (module.HasValue)
            {
                RunModuleImport(module.Value);
                return;
            }

            if (key == "company-templates") ShowCompanyTemplateManager();
            else if (key == "assets") ShowExistingTab(1);
            else if (key == "documents") ShowExistingTab(2);
            else if (key == "rates") ShowExistingTab(3);
            else ShowMappingHint(key);
        }

        private void RunMappingAction(ExcelImportModule? module, string key)
        {
            if (module.HasValue)
            {
                ImportUiHelper.RunImport(module.Value, ResolveDialogOwner());
                _ = LoadAllAsync();
                return;
            }

            ShowMappingHint(key);
        }

        private void RunModuleImport(ExcelImportModule module)
        {
            ImportUiHelper.RunImport(module, ResolveDialogOwner());
            _ = LoadAllAsync();
        }

        private void RunFolderImport(ExcelImportModule? module)
        {
            ImportUiHelper.RunImportFolder(module, ResolveDialogOwner());
            _ = LoadAllAsync();
        }

        private Form ResolveDialogOwner()
        {
            Form form = FindForm();
            return form != null && !form.IsDisposed ? form : null;
        }

        private static bool CanUseControl(Control control)
        {
            return control != null
                && !control.IsDisposed
                && control.IsHandleCreated
                && control.Visible;
        }

        private static bool TryShowMenu(ContextMenuStrip menu, Control owner, Point location)
        {
            if (menu == null || !CanUseControl(owner))
                return false;

            menu.Show(owner, location);
            return true;
        }

        private static ExcelImportModule[] ImportableModules()
        {
            return new[]
            {
                ExcelImportModule.Clients,
                ExcelImportModule.Vendors,
                ExcelImportModule.Sites,
                ExcelImportModule.Inventory,
                ExcelImportModule.SupplierItemPrices,
                ExcelImportModule.Purchases,
                ExcelImportModule.Invoices,
                ExcelImportModule.Payments,
                ExcelImportModule.Quotations,
                ExcelImportModule.Jobs,
                ExcelImportModule.Employees,
                ExcelImportModule.AMC
            };
        }

        /// <summary>Returns the user-facing upload title for a supported Excel import module.</summary>
        private static string GetUploadTitle(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Clients: return "Clients";
                case ExcelImportModule.Vendors: return ExcelImportService.GetDisplayName(module);
                case ExcelImportModule.Sites: return "Sites";
                case ExcelImportModule.Inventory: return "Inventory";
                case ExcelImportModule.SupplierItemPrices: return ExcelImportService.GetDisplayName(module);
                case ExcelImportModule.Purchases: return "Purchases";
                case ExcelImportModule.Invoices: return "Invoices";
                case ExcelImportModule.Payments: return "Payments";
                case ExcelImportModule.Quotations: return "Quotations";
                case ExcelImportModule.Jobs: return "Jobs";
                case ExcelImportModule.Employees: return "Employees";
                case ExcelImportModule.AMC: return ExcelImportService.GetDisplayName(module);
                default: return module.ToString();
            }
        }

        /// <summary>Returns the short upload card description for a supported Excel import module.</summary>
        private static string GetUploadDescription(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Clients: return "Customer master, GST, contacts";
                case ExcelImportModule.Vendors: return "Supplier master, GST, contacts";
                case ExcelImportModule.Sites: return "Client sites, city, service contacts";
                case ExcelImportModule.Inventory: return "Parts, buying rates, planning quantities";
                case ExcelImportModule.SupplierItemPrices: return "Supplier links, preferred rates, buying history";
                case ExcelImportModule.Purchases: return "Supplier bills, items, totals";
                case ExcelImportModule.Invoices: return "Past invoices, due dates, status";
                case ExcelImportModule.Payments: return "Collections, UTR, modes, notes";
                case ExcelImportModule.Quotations: return "Quotes, validity, client offers";
                case ExcelImportModule.Jobs: return "Service calls, technician, priority";
                case ExcelImportModule.Employees: return "Staff profiles, phone, ID details";
                case ExcelImportModule.AMC: return "Maintenance contracts, dates, clients";
                default: return "Excel data upload";
            }
        }

        /// <summary>Returns the upload accent key for a supported Excel import module.</summary>
        private static string GetUploadKey(ExcelImportModule module)
        {
            return module.ToString().ToLowerInvariant();
        }

        /// <summary>Returns the current record count for a supported Excel import module.</summary>
        private int CountUploadRecords(ExcelImportModule module)
        {
            if (_lastSnapshot == null)
                return 0;

            try
            {
                string key = "masterdata:upload-count:" + module;
                return AppDataCache.GetOrCreate(key, TimeSpan.FromMinutes(2), () => _svc.GetUploadRecordCount(module));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MasterDataForm.CountUploadRecords." + module, ex);
                return 0;
            }
        }

        private void ShowExistingTab(int index)
        {
            using (Form dialog = ServoModalForm.Create("Master data details", 1180, 760))
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
            MessageBox.Show(this, "ServoERP now handles mapping for " + subject + " automatically.\r\n\r\nUpload the Excel file and the app will detect columns, clean values, create safe defaults, and skip only unsafe rows.", "Automatic import", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowDuplicateCheck()
        {
            MessageBox.Show(this, "Duplicate checks now run automatically using GST numbers, phone, email, names, invoice numbers, and PO patterns. Existing records are refreshed safely and uncertain duplicates are skipped with a simple reason.", "Duplicate detection", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private int CountCompanyTemplates()
        {
            if (_lastSnapshot == null)
                return 0;

            try { return AppDataCache.GetOrCreate("masterdata:company-template-count", TimeSpan.FromMinutes(2), () => _templateManager.GetTemplates().Count); }
            catch { return 0; }
        }

        private void ShowCompanyTemplateManager()
        {
            using (var dialog = new CompanyTemplateManagerDialog(_templateManager))
                dialog.ShowDialog(FindForm());
            RenderHub(_lastSnapshot);
        }

        private static Button MakeButton(string text, Color color, int width, EventHandler click = null)
        {
            bool light = color == Color.White || color.GetBrightness() > 0.92f;
            Button button = new Button
            {
                Text = text,
                Width = Math.Max(width, 104),
                Height = 36,
                BackColor = color,
                ForeColor = light ? DS.Slate800 : Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = light ? 1 : 0;
            button.FlatAppearance.BorderColor = light ? DS.BorderStrong : color;
            button.FlatAppearance.MouseOverBackColor = light ? DS.BgCardHov : DS.Lighten(color, 0.08f);
            button.FlatAppearance.MouseDownBackColor = light ? DS.Slate100 : DS.Darken(color, 0.08f);
            if (click != null) button.Click += click;
            return button;
        }

        private void ShowStatus(string text, bool isError)
        {
            if (_status == null) return;
            _status.Text = text;
            _status.ForeColor = isError ? Color.FromArgb(185, 28, 28) : DS.Slate500;
        }

        private sealed class CompanyTemplateManagerDialog : ServoERP.Infrastructure.ServoFormBase
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
                Size = new Size(1320, 820);
                MinimumSize = new Size(1180, 720);
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
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
                Controls.Add(root);

                root.Controls.Add(BuildUploadAndListPanel(), 0, 0);
                root.Controls.Add(BuildRecognitionPanel(), 1, 0);
                root.Controls.Add(BuildPreviewPanel(), 2, 0);
            }

            private Control BuildUploadAndListPanel()
            {
                Panel panel = CardPanel();
                panel.Padding = new Padding(12);

                TableLayoutPanel layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    BackColor = Color.White
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 146f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
                panel.Controls.Add(layout);

                _status = new Label { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = DS.Slate600 };

                Panel drop = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AllowDrop = true, Cursor = Cursors.Hand, Padding = new Padding(16) };
                drop.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (Pen pen = new Pen(Color.FromArgb(147, 197, 253), 2) { DashStyle = DashStyle.Dash })
                        e.Graphics.DrawRectangle(pen, 8, 8, drop.Width - 17, drop.Height - 17);
                };
                drop.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
                drop.DragDrop += (s, e) => UploadFiles((string[])e.Data.GetData(DataFormats.FileDrop));
                drop.Click += (s, e) => PickAndUpload();
                drop.Controls.Add(new Label { Text = "PDF, Word, Excel, CSV, PNG, JPG, invoice/quotation samples", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = DS.Slate600 });
                drop.Controls.Add(new Label { Text = "Drop company template here", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.BottomCenter, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate900 });

                FlowLayoutPanel row = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 8), BackColor = Color.White, WrapContents = false };
                _type = new ComboBox { Width = 176, DropDownStyle = ComboBoxStyle.DropDownList };
                _type.Items.AddRange(Enum.GetNames(typeof(CompanyDocumentTemplateType)));
                _type.SelectedItem = CompanyDocumentTemplateType.Other.ToString();
                row.Controls.Add(_type);
                row.Controls.Add(MakeButton("Upload", DS.Primary600, 96, (s, e) => PickAndUpload()));

                _list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f), IntegralHeight = false, HorizontalScrollbar = true };
                _list.SelectedIndexChanged += (s, e) => SelectCurrent();
                layout.Controls.Add(drop, 0, 0);
                layout.Controls.Add(row, 0, 1);
                layout.Controls.Add(_list, 0, 2);
                layout.Controls.Add(_status, 0, 3);
                return panel;
            }

            private Control BuildRecognitionPanel()
            {
                Panel panel = CardPanel();
                panel.Padding = new Padding(16);

                TableLayoutPanel layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 6,
                    BackColor = Color.White
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
                panel.Controls.Add(layout);

                FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
                actions.Controls.Add(MakeButton("Save mapping", SaveGreen, 120, (s, e) => SaveSelected()));
                actions.Controls.Add(MakeButton("Set default", DS.Primary600, 110, (s, e) => SetDefault()));
                actions.Controls.Add(MakeButton("Open file", DS.Slate700, 96, (s, e) => OpenSelected()));
                actions.Controls.Add(MakeButton("Remove", Color.White, 96, (s, e) => RemoveSelected()));

                _mapping = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.FixedSingle };
                Label mappingTitle = new Label { Text = "Manual mapping (Field=Placeholder per line)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate700 };

                FlowLayoutPanel toggles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 8, 0, 4) };
                _default = new CheckBox { Text = "Default for this document type", Width = 230, Height = 26 };
                _useInvoice = new CheckBox { Text = "Use for Invoices", Width = 150, Height = 26 };
                _useQuote = new CheckBox { Text = "Use for Quotations", Width = 170, Height = 26 };
                _usePo = new CheckBox { Text = "Use for POs", Width = 130, Height = 26 };
                _useReport = new CheckBox { Text = "Use for Reports", Width = 150, Height = 26 };
                toggles.Controls.AddRange(new Control[] { _default, _useInvoice, _useQuote, _usePo, _useReport });

                _recognition = new Label { Dock = DockStyle.Fill, ForeColor = DS.Slate600, Font = new Font("Segoe UI", 9f) };

                layout.Controls.Add(new Label { Text = "Recognition and field mapping", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = DS.Slate900 }, 0, 0);
                layout.Controls.Add(_recognition, 0, 1);
                layout.Controls.Add(toggles, 0, 2);
                layout.Controls.Add(mappingTitle, 0, 3);
                layout.Controls.Add(_mapping, 0, 4);
                layout.Controls.Add(actions, 0, 5);
                return panel;
            }

            private Control BuildPreviewPanel()
            {
                Panel panel = CardPanel();
                panel.Padding = new Padding(12);
                TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                panel.Controls.Add(layout);

                layout.Controls.Add(new Label { Text = "Recognized template preview", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = DS.Slate900 }, 0, 0);
                _preview = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };
                layout.Controls.Add(_preview, 0, 1);
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
                    _manager.UploadTemplate(file, ResolveUploadType(file, selectedType));
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
                if (_selected.UseForQuotations && !_selected.UseForInvoices)
                    _selected.DocumentType = CompanyDocumentTemplateType.Quotation;
                else if (_selected.UseForInvoices && !_selected.UseForQuotations)
                    _selected.DocumentType = CompanyDocumentTemplateType.Invoice;
                else if (_selected.UseForPurchaseOrders)
                    _selected.DocumentType = CompanyDocumentTemplateType.PurchaseOrder;
                else if (_selected.UseForReports)
                    _selected.DocumentType = CompanyDocumentTemplateType.Report;
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

            private void RemoveSelected()
            {
                if (_selected == null)
                    return;
                if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, "Remove this company template?", "The stored file remains on disk. This removes the template from ServoERP's active template list."))
                    return;

                _manager.RemoveTemplate(_selected.TemplateId);
                _selected = null;
                RefreshTemplates();
                _status.Text = "Template removed from the active list.";
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

            private static CompanyDocumentTemplateType ResolveUploadType(string file, CompanyDocumentTemplateType selectedType)
            {
                string name = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
                if (ContainsAny(name, "quotation", "quote", "tender")) return CompanyDocumentTemplateType.Quotation;
                if (ContainsAny(name, "invoice", "tax invoice")) return CompanyDocumentTemplateType.Invoice;
                if (ContainsAny(name, "purchase", "po")) return CompanyDocumentTemplateType.PurchaseOrder;
                if (ContainsAny(name, "delivery", "challan")) return CompanyDocumentTemplateType.DeliveryNote;
                if (ContainsAny(name, "letterhead", "header")) return CompanyDocumentTemplateType.Letterhead;
                if (ContainsAny(name, "contract", "amc")) return CompanyDocumentTemplateType.Contract;
                if (ContainsAny(name, "report")) return CompanyDocumentTemplateType.Report;
                return selectedType;
            }

            private static bool ContainsAny(string value, params string[] needles)
            {
                value = value ?? string.Empty;
                foreach (string needle in needles)
                    if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                return false;
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

        private sealed class LookupCardDefinition
        {
            public LookupCardDefinition(string module, string title, string categoryKey, ModernIconKind icon, Color color)
            {
                Module = module;
                Title = title;
                CategoryKey = categoryKey;
                Icon = icon;
                Color = color;
            }

            public string Module { get; private set; }
            public string Title { get; private set; }
            public string CategoryKey { get; private set; }
            public ModernIconKind Icon { get; private set; }
            public Color Color { get; private set; }
        }

        private static IEnumerable<LookupCardDefinition> LookupCardDefinitions()
        {
            Color hr = DS.Primary600;
            Color attendance = DS.Teal600;
            Color payroll = DS.Green600;
            Color jobs = Color.FromArgb(124, 58, 237);
            Color service = Color.FromArgb(14, 116, 144);
            Color amc = Color.FromArgb(249, 115, 22);
            Color inventory = DS.Amber600;
            Color purchase = DS.Red500;
            Color sales = DS.Indigo600;

            return new[]
            {
                new LookupCardDefinition("HR", "Blood Groups", "HR.BloodGroup", ModernIconKind.User, hr),
                new LookupCardDefinition("HR", "Employee Status", "HR.EmployeeStatus", ModernIconKind.Status, hr),
                new LookupCardDefinition("HR", "Employment Types", "HR.EmploymentType", ModernIconKind.Document, hr),
                new LookupCardDefinition("HR", "Tax Regimes", "HR.TaxRegime", ModernIconKind.Security, hr),
                new LookupCardDefinition("Attendance", "Attendance Status", "Attendance.Status", ModernIconKind.Calendar, attendance),
                new LookupCardDefinition("Attendance", "Leave Types", "Attendance.LeaveType", ModernIconKind.Calendar, attendance),
                new LookupCardDefinition("Payroll", "Salary Components", "Payroll.SalaryComponent", ModernIconKind.Payment, payroll),
                new LookupCardDefinition("Payroll", "Payment Modes", "Payroll.PaymentMode", ModernIconKind.Payment, payroll),
                new LookupCardDefinition("Jobs", "Job Types", "Jobs.JobType", ModernIconKind.Job, jobs),
                new LookupCardDefinition("Jobs", "Job Priorities", "Jobs.Priority", ModernIconKind.Alert, jobs),
                new LookupCardDefinition("Jobs", "Job Status", "Jobs.Status", ModernIconKind.Status, jobs),
                new LookupCardDefinition("Service Desk", "Ticket Categories", "ServiceDesk.Category", ModernIconKind.Service, service),
                new LookupCardDefinition("Service Desk", "Equipment Types", "ServiceDesk.EquipmentType", ModernIconKind.Parts, service),
                new LookupCardDefinition("Service Desk", "Ticket Status", "ServiceDesk.Status", ModernIconKind.Status, service),
                new LookupCardDefinition("AMC", "AMC Types", "AMC.Type", ModernIconKind.Document, amc),
                new LookupCardDefinition("AMC", "Coverage Types", "AMC.CoverageType", ModernIconKind.Security, amc),
                new LookupCardDefinition("AMC", "Billing Cycles", "AMC.BillingCycle", ModernIconKind.Calendar, amc),
                new LookupCardDefinition("AMC", "AMC Status", "AMC.Status", ModernIconKind.Status, amc),
                new LookupCardDefinition("Inventory", "Stock Categories", "Inventory.Category", ModernIconKind.Inventory, inventory),
                new LookupCardDefinition("Inventory", "Godowns", "Inventory.Godown", ModernIconKind.Inventory, inventory),
                new LookupCardDefinition("Purchase", "Purchase Status", "Purchase.Status", ModernIconKind.Purchase, purchase),
                new LookupCardDefinition("Purchase", "Linked Types", "Purchase.LinkedType", ModernIconKind.Preference, purchase),
                new LookupCardDefinition("Sales", "Invoice Status", "Sales.InvoiceStatus", ModernIconKind.Invoice, sales),
                new LookupCardDefinition("Sales", "GST Modes", "Sales.GstMode", ModernIconKind.Tax, sales),
                new LookupCardDefinition("Sales", "Coverage Types", "Sales.CoverageType", ModernIconKind.Security, sales),
                new LookupCardDefinition("Sales", "Warranty Status", "Sales.WarrantyStatus", ModernIconKind.Status, sales),
                new LookupCardDefinition("Sales", "Quotation Status", "Sales.QuotationStatus", ModernIconKind.Document, sales)
            };
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
            public List<MasterLookupCategory> LookupCategories { get; set; }
            public List<MasterLookupValue> LookupValues { get; set; }
        }
    }
}



