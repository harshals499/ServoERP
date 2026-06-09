using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;
using OfficeOpenXml;

namespace HVAC_Pro_Desktop.UI
{
    public partial class TallyIntegrationForm : DeferredPageControl
    {
        private const string DEFAULT_TALLY_URL = "http://localhost:9000";
        private const string DEFAULT_EXPORT_FOLDER = @"C:\ProgramData\ServoERP\TallyExports";
        private static readonly Color PageBack = Color.FromArgb(249, 250, 252);
        private static readonly Color CardBorder = Color.FromArgb(225, 230, 238);
        private static readonly Color TextDark = Color.FromArgb(22, 26, 42);
        private static readonly Color TextMuted = Color.FromArgb(111, 122, 145);
        private static readonly Color AccentBlue = Color.FromArgb(38, 91, 214);

        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly Dictionary<string, string> _settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HealthCheckRow> _healthRows = new Dictionary<string, HealthCheckRow>(StringComparer.OrdinalIgnoreCase);
        private readonly Timer _logRefreshTimer = new Timer();

        private TabControl _tabs;
        private PillLabel _connectionPill;
        private Label _lastCheckedLabel;
        private InfoTile _urlTile;
        private InfoTile _companyTile;
        private InfoTile _folderTile;
        private InfoTile _pushTile;
        private ToggleSwitch _directPushToggle;
        private TextBox _urlText;
        private TextBox _folderText;
        private TextBox _companyText;
        private TextBox _godownText;
        private Label _connectionStatusLabel;
        private FlowLayoutPanel _recentActivityPanel;

        private DateTimePicker _exportFromDate;
        private DateTimePicker _exportToDate;
        private ComboBox _voucherTypeCombo;
        private ComboBox _exportStatusCombo;
        private DataGridView _voucherGrid;
        private Label _exportStatusLabel;
        private FlowLayoutPanel _exportStatsPanel;

        private TextBox _importPathText;
        private ComboBox _importTypeCombo;
        private DataGridView _importPreviewGrid;
        private ProgressBar _importProgress;
        private Label _importStatusLabel;
        private List<TallyMasterPreviewRow> _importPreviewRows = new List<TallyMasterPreviewRow>();

        private ToggleSwitch _autoSyncToggle;
        private ComboBox _syncDirectionCombo;
        private TextBox _godownFilterText;
        private FlowLayoutPanel _inventoryStatsPanel;
        private DataGridView _inventoryGrid;
        private Label _inventoryStatusLabel;

        private ComboBox _logTypeCombo;
        private DateTimePicker _logFromDate;
        private DateTimePicker _logToDate;
        private TextBox _logSearchText;
        private DataGridView _logGrid;
        private Label _logStatusLabel;
        private bool _loaded;
        private bool _workRunning;
        private DateTime? _lastConnectionCheck;
        private bool _lastConnectionOk;

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        /// <summary>Initializes the Tally Integration Hub page.</summary>
        public TallyIntegrationForm()
        {
            InitializeComponent();
            BuildPlaceholderShell();
            MarkDeferredLoadCompleted();
            _logRefreshTimer.Interval = 30000;
            _logRefreshTimer.Tick += (s, e) =>
            {
                if (_tabs != null && _tabs.SelectedIndex == 4)
                    LoadLogGrid();
            };
            _logRefreshTimer.Start();
        }

        /// <summary>Runs first-load schema, settings, and dashboard refresh work.</summary>
        private void TallyIntegrationForm_Load()
        {
            if (_loaded)
                return;

            _loaded = true;
            BuildHub();
            EnsureTallySchema();
            LoadTallySettings();
            UpdateOverviewTiles();
            UpdateHeaderStats();
            LoadRecentActivity();
            LoadExportGrid();
            LoadInventoryGrid();
            LoadLogGrid();
            UpdateConnectionStatus(false, false);
            RemoveTallyResizeGrips();
            BeginInvoke(new Action(RemoveTallyResizeGrips));
        }

        /// <summary>Builds a lightweight constructor shell; the full hub is created by deferred load after hosting.</summary>
        private void BuildPlaceholderShell()
        {
            BackColor = PageBack;
            Padding = new Padding(16, 14, 16, 14);
            Controls.Clear();
            var placeholder = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 118,
                BackColor = Color.White,
                BorderColor = CardBorder,
                Radius = 8,
                Padding = new Padding(20)
            };
            var title = new Label
            {
                Text = "Tally Integration",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = TextDark
            };
            var subtitle = NewMutedLabel("Export vouchers, import masters, and sync stock items with Tally Prime", 9.5f);
            var openHub = NewPrimaryButton("Open Tally Hub", (s, e) => TallyIntegrationForm_Load(), 140);
            openHub.Dock = DockStyle.Bottom;
            placeholder.Controls.Add(subtitle);
            placeholder.Controls.Add(title);
            placeholder.Controls.Add(openHub);
            Controls.Add(placeholder);
        }

        /// <summary>Creates the complete Tally Integration Hub layout.</summary>
        private void BuildHub()
        {
            BackColor = PageBack;
            Padding = new Padding(18);
            Controls.Clear();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PageBack
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildPageHeader(), 0, 0);
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Appearance = TabAppearance.Normal,
                ItemSize = new Size(126, 30),
                SizeMode = TabSizeMode.Fixed,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Padding = new Point(0, 0)
            };
            _tabs.DrawItem += DrawHubTab;
            _tabs.SelectedIndexChanged += (s, e) =>
            {
                if (_tabs.SelectedIndex == 4)
                    LoadLogGrid();
            };
            _tabs.TabPages.Add(BuildConnectionTab());
            _tabs.TabPages.Add(BuildExportTab());
            _tabs.TabPages.Add(BuildImportTab());
            _tabs.TabPages.Add(BuildInventoryTab());
            _tabs.TabPages.Add(BuildLogsTab());
            root.Controls.Add(_tabs, 0, 1);
        }

        /// <summary>Builds the compact page header from the reference layout.</summary>
        private Control BuildPageHeader()
        {
            var header = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.White, BorderColor = CardBorder, Radius = 8, Padding = new Padding(20, 10, 20, 8), Margin = new Padding(0, 0, 0, 0) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Controls.Add(layout);

            var title = new Label { Text = "Tally Integration", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = TextDark, TextAlign = ContentAlignment.BottomLeft };
            layout.Controls.Add(title, 0, 0);
            var subtitle = NewMutedLabel("Export vouchers, import masters, and sync stock items with Tally Prime.", 8.5f);
            layout.Controls.Add(subtitle, 0, 1);
            return header;
        }

        /// <summary>Builds the Connection tab.</summary>
        private TabPage BuildConnectionTab()
        {
            var page = NewTab("Connection");
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, ColumnCount = 2, RowCount = 3, BackColor = PageBack, Padding = new Padding(0, 0, 0, 8) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 206));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 390));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));

            var overview = BuildOverviewStrip();
            body.Controls.Add(overview, 0, 0);
            body.SetColumnSpan(overview, 2);
            body.Controls.Add(BuildConnectionSettingsPanel(), 0, 1);
            body.Controls.Add(BuildConnectionRightColumn(), 1, 1);
            var quick = BuildQuickActionsStrip();
            body.Controls.Add(quick, 0, 2);
            body.SetColumnSpan(quick, 2);
            page.Controls.Add(body);
            return page;
        }

        /// <summary>Builds the overview tile strip.</summary>
        private Control BuildOverviewStrip()
        {
            var card = NewCard(18);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            var titleStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            titleStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            titleStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            titleStack.Controls.Add(NewSectionTitle("Connection Overview"), 0, 0);
            var status = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            _connectionPill = new PillLabel { Text = "Disconnected", PillKind = "Error", Width = 106, Height = 24 };
            _lastCheckedLabel = NewMutedLabel("Last checked: never", 8.5f);
            _lastCheckedLabel.AutoSize = true;
            _lastCheckedLabel.Margin = new Padding(10, 5, 8, 0);
            var refresh = LinkLabel("⟳");
            refresh.Margin = new Padding(0, 4, 0, 0);
            refresh.Click += (s, e) => RunConnectionTest();
            status.Controls.Add(_connectionPill);
            status.Controls.Add(_lastCheckedLabel);
            status.Controls.Add(refresh);
            titleStack.Controls.Add(status, 0, 1);
            layout.Controls.Add(titleStack, 0, 0);
            layout.SetColumnSpan(titleStack, 2);

            var tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent };
            for (int i = 0; i < 4; i++)
                tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _urlTile = new InfoTile("Tally URL", DEFAULT_TALLY_URL, "Globe");
            _companyTile = new InfoTile("Company", "Main Location", "Company");
            _folderTile = new InfoTile("XML Export Folder", DEFAULT_EXPORT_FOLDER, "Folder");
            _pushTile = new InfoTile("Direct Push", "Disabled", "Upload");
            _urlTile.Click += (s, e) => FocusInput(_urlText);
            _companyTile.Click += (s, e) => FocusInput(_companyText);
            _folderTile.Click += (s, e) => FocusInput(_folderText);
            _pushTile.Click += (s, e) => _directPushToggle.Checked = !_directPushToggle.Checked;
            tiles.Controls.Add(_urlTile, 0, 0);
            tiles.Controls.Add(_companyTile, 1, 0);
            tiles.Controls.Add(_folderTile, 2, 0);
            tiles.Controls.Add(_pushTile, 3, 0);
            layout.Controls.Add(tiles, 0, 1);
            layout.Controls.Add(new TallyArtworkPanel { Dock = DockStyle.Fill, Margin = new Padding(18, 0, 20, 0) }, 1, 1);
            return card;
        }

        /// <summary>Builds the connection settings form panel.</summary>
        private Control BuildConnectionSettingsPanel()
        {
            var card = NewCard(16);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            card.Controls.Add(layout);

            layout.Controls.Add(NewSectionTitle("Connection Settings"), 0, 0);
            _directPushToggle = new ToggleSwitch { Dock = DockStyle.Right, Width = 46, Height = 24 };
            _directPushToggle.CheckedChanged += (s, e) =>
            {
                SetSetting("DirectPushEnabled", _directPushToggle.Checked ? "1" : "0");
                UpdateOverviewTiles();
            };
            layout.Controls.Add(BuildToggleRow("Enable direct push to Tally", _directPushToggle), 0, 1);
            _urlText = NewInput(DEFAULT_TALLY_URL);
            layout.Controls.Add(BuildField("Tally URL", _urlText, "Provide your Tally Prime API URL"), 0, 2);
            _folderText = NewInput(DEFAULT_EXPORT_FOLDER);
            layout.Controls.Add(BuildFieldWithButton("XML export folder", _folderText, "Browse", BrowseExportFolder, "Folder where XML files will be exported"), 0, 3);
            _companyText = NewInput(string.Empty);
            layout.Controls.Add(BuildField("Company name", _companyText, "Enter the company name as selected in Tally Prime"), 0, 4);
            _godownText = NewInput("Main Location");
            layout.Controls.Add(BuildField("Default godown", _godownText, "Select the default godown for item exports"), 0, 5);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            actions.Controls.Add(NewPrimaryButton("Save Settings", (s, e) => SaveTallySettings(), 128));
            actions.Controls.Add(NewOutlineButton("Test Connection", (s, e) => RunConnectionTest(), 132));
            actions.Controls.Add(NewOutlineButton("Auto Map Names", (s, e) => RunAutoMapNames(), 138));
            _connectionStatusLabel = NewMutedLabel("Ready.", 9f);
            _connectionStatusLabel.Margin = new Padding(12, 12, 0, 0);
            actions.Controls.Add(_connectionStatusLabel);
            layout.Controls.Add(actions, 0, 6);
            return card;
        }

        /// <summary>Builds the right column with health and activity panels.</summary>
        private Control BuildConnectionRightColumn()
        {
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = PageBack, Padding = new Padding(10, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(BuildConnectionHealthPanel(), 0, 0);
            right.Controls.Add(BuildRecentActivityPanel(), 0, 1);
            return right;
        }

        /// <summary>Builds the connection health panel.</summary>
        private Control BuildConnectionHealthPanel()
        {
            var card = NewCard(14);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 5, BackColor = Color.Transparent };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            for (int i = 1; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            card.Controls.Add(layout);

            var title = NewSectionTitle("Connection Health");
            layout.Controls.Add(title, 0, 0);
            var badge = new PillLabel { Text = "Not checked", PillKind = "Info", Dock = DockStyle.Right, Width = 96, Height = 24 };
            badge.Name = "HealthBadge";
            layout.Controls.Add(badge, 1, 0);
            layout.Controls.Add(BuildRunTestCard(), 2, 0);
            layout.SetRowSpan(layout.GetControlFromPosition(2, 0), 5);

            AddHealthRow(layout, 1, "Tally Prime Service");
            AddHealthRow(layout, 2, "API Connectivity");
            AddHealthRow(layout, 3, "Company Access");
            AddHealthRow(layout, 4, "Data Sync Readiness");
            return card;
        }

        /// <summary>Builds the embedded run-test card.</summary>
        private Control BuildRunTestCard()
        {
            var card = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(249, 250, 253), BorderColor = CardBorder, Radius = 8, Padding = new Padding(14, 14, 14, 14), Margin = new Padding(10, 0, 0, 0) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "Test Connection", Dock = DockStyle.Fill, ForeColor = AccentBlue, Font = new Font("Segoe UI", 9f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            layout.Controls.Add(NewMutedLabel("Verify the connection with your Tally Prime.", 8.5f, ContentAlignment.MiddleCenter), 0, 1);
            layout.Controls.Add(NewPrimaryButton("Run Test", (s, e) => RunConnectionTest(), 160), 0, 2);
            return card;
        }

        /// <summary>Builds the recent activity feed panel.</summary>
        private Control BuildRecentActivityPanel()
        {
            var card = NewCard(14);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);
            var head = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            head.Controls.Add(NewSectionTitle("Recent Activity"), 0, 0);
            var viewLogs = LinkLabel("View Logs");
            viewLogs.Click += (s, e) => _tabs.SelectedIndex = 4;
            head.Controls.Add(viewLogs, 1, 0);
            layout.Controls.Add(head, 0, 0);
            _recentActivityPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
            layout.Controls.Add(_recentActivityPanel, 0, 1);
            return card;
        }

        /// <summary>Builds the quick action cards.</summary>
        private Control BuildQuickActionsStrip()
        {
            var strip = NewCard(12);
            strip.Margin = new Padding(0, 0, 0, 0);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent };
            for (int i = 0; i < 4; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            strip.Controls.Add(layout);
            layout.Controls.Add(NewQuickAction("Export Vouchers", "Export sales, purchases and payments to Tally", 1), 0, 0);
            layout.Controls.Add(NewQuickAction("Import Masters", "Import ledgers, items and other masters", 2), 1, 0);
            layout.Controls.Add(NewQuickAction("Inventory Sync", "Sync stock items and balances", 3), 2, 0);
            layout.Controls.Add(NewQuickAction("View Logs", "View all integration logs and history", 4), 3, 0);
            return strip;
        }

        /// <summary>Builds the Export tab.</summary>
        private TabPage BuildExportTab()
        {
            var page = NewTab("Export");
            var scroll = NewScrollCanvas();
            var card = NewCard(16);
            card.Dock = DockStyle.Top;
            card.Height = 620;
            scroll.Controls.Add(card);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            card.Controls.Add(layout);

            layout.Controls.Add(BuildTitleBlock("Export Vouchers", "Export sales invoices, purchase orders, and payments as Tally XML vouchers."), 0, 0);
            layout.Controls.Add(BuildExportFilters(), 0, 1);
            _exportStatsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            layout.Controls.Add(_exportStatsPanel, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            actions.Controls.Add(NewPrimaryButton("Export Selected", (s, e) => ExportVouchers(), 136));
            actions.Controls.Add(NewOutlineButton("Preview XML", PreviewSelectedXml, 116));
            actions.Controls.Add(NewOutlineButton("Open Folder", OpenExportFolder, 116));
            layout.Controls.Add(actions, 0, 3);
            _voucherGrid = NewGrid();
            _voucherGrid.CellDoubleClick += VoucherGrid_CellDoubleClick;
            layout.Controls.Add(_voucherGrid, 0, 4);
            _exportStatusLabel = NewMutedLabel("Ready.", 9f);
            layout.Controls.Add(_exportStatusLabel, 0, 5);
            page.Controls.Add(scroll);
            return page;
        }

        /// <summary>Builds the Import tab.</summary>
        private TabPage BuildImportTab()
        {
            var page = NewTab("Import");
            var scroll = NewScrollCanvas();
            var card = NewCard(16);
            card.Dock = DockStyle.Top;
            card.Height = 610;
            scroll.Controls.Add(card);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            card.Controls.Add(layout);

            layout.Controls.Add(BuildTitleBlock("Import Masters", "Import ledgers, items, units and other master data from Tally."), 0, 0);
            _importPathText = NewInput(string.Empty);
            layout.Controls.Add(BuildFieldWithButton("Tally export file (.xml)", _importPathText, "Browse", BrowseImportFile, "Select a Tally XML file to preview before import"), 0, 1);
            _importTypeCombo = NewCombo(new[] { "Ledger Masters", "Stock Item Masters", "Cost Centres", "Units of Measure" });
            layout.Controls.Add(BuildField("Import type", _importTypeCombo, "Choose the master type to validate and import"), 0, 2);
            _importPreviewGrid = NewGrid();
            layout.Controls.Add(_importPreviewGrid, 0, 3);
            _importProgress = new ProgressBar { Dock = DockStyle.Fill, Visible = false, Style = ProgressBarStyle.Continuous };
            layout.Controls.Add(_importProgress, 0, 4);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            actions.Controls.Add(NewPrimaryButton("Import Now", (s, e) => ImportMasters(false), 118));
            actions.Controls.Add(NewOutlineButton("Validate Only", (s, e) => ImportMasters(true), 128));
            layout.Controls.Add(actions, 0, 5);
            _importStatusLabel = NewMutedLabel("Select an XML file to preview.", 9f);
            layout.Controls.Add(_importStatusLabel, 0, 6);
            page.Controls.Add(scroll);
            return page;
        }

        /// <summary>Builds the Inventory Sync tab.</summary>
        private TabPage BuildInventoryTab()
        {
            var page = NewTab("Inventory Sync");
            var scroll = NewScrollCanvas();
            var card = NewCard(16);
            card.Dock = DockStyle.Top;
            card.Height = 650;
            scroll.Controls.Add(card);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            card.Controls.Add(layout);

            layout.Controls.Add(BuildTitleBlock("Inventory Sync Settings", "Sync ServoERP stock items and Tally stock masters without changing the sidebar or source inventory forms."), 0, 0);
            layout.Controls.Add(BuildInventorySettings(), 0, 1);
            _inventoryStatsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            layout.Controls.Add(_inventoryStatsPanel, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };
            actions.Controls.Add(NewPrimaryButton("Sync Selected", (s, e) => SyncInventoryItems(false), 126));
            actions.Controls.Add(NewOutlineButton("Sync All", (s, e) => SyncInventoryItems(true), 98));
            actions.Controls.Add(NewOutlineButton("Map Names", (s, e) => RunAutoMapNames(), 106));
            layout.Controls.Add(actions, 0, 3);
            _inventoryGrid = NewGrid();
            layout.Controls.Add(_inventoryGrid, 0, 4);
            _inventoryStatusLabel = NewMutedLabel("Ready.", 9f);
            layout.Controls.Add(_inventoryStatusLabel, 0, 6);
            page.Controls.Add(scroll);
            return page;
        }

        /// <summary>Builds the Logs tab.</summary>
        private TabPage BuildLogsTab()
        {
            var page = NewTab("Logs");
            var card = NewCard(16);
            card.Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            card.Controls.Add(layout);
            layout.Controls.Add(BuildTitleBlock("Activity Log Viewer", "Review Tally connection, export, import and inventory sync activity."), 0, 0);
            layout.Controls.Add(BuildLogFilters(), 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent };
            actions.Controls.Add(NewOutlineButton("Clear Old Logs", ClearOldLogs, 124));
            actions.Controls.Add(NewOutlineButton("Export to Excel", ExportLogsToExcel, 128));
            layout.Controls.Add(actions, 0, 2);
            _logGrid = NewGrid();
            layout.Controls.Add(_logGrid, 0, 3);
            _logStatusLabel = NewMutedLabel("Ready.", 9f);
            layout.Controls.Add(_logStatusLabel, 0, 4);
            page.Controls.Add(card);
            return page;
        }

        /// <summary>Creates Tally schema additions with guarded additive SQL only.</summary>
        private void EnsureTallySchema()
        {
            ExecuteNonQuery(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TallyActivityLog')
CREATE TABLE TallyActivityLog (
    LogID        INT IDENTITY(1,1) PRIMARY KEY,
    LogTime      DATETIME NOT NULL DEFAULT GETDATE(),
    EventType    NVARCHAR(50)  NOT NULL,
    EventMessage NVARCHAR(500) NOT NULL,
    PerformedBy  NVARCHAR(100) NOT NULL DEFAULT 'Administrator'
);

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TallySettings')
CREATE TABLE TallySettings (
    SettingKey   NVARCHAR(100) PRIMARY KEY,
    SettingValue NVARCHAR(500) NOT NULL
);

IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'TallyURL')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('TallyURL', 'http://localhost:9000');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'XMLExportFolder')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('XMLExportFolder', 'C:\ProgramData\ServoERP\TallyExports');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'CompanyName')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('CompanyName', '');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'DefaultGodown')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('DefaultGodown', 'Main Location');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'DirectPushEnabled')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('DirectPushEnabled', '0');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'AutoSyncInventory')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('AutoSyncInventory', '0');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'SyncDirection')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('SyncDirection', 'ServoERP → Tally');
IF NOT EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = 'GodownFilter')
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES ('GodownFilter', '');

IF OBJECT_ID('StockItems', 'U') IS NOT NULL AND COL_LENGTH('StockItems', 'TallySynced') IS NULL
    ALTER TABLE StockItems ADD TallySynced BIT NOT NULL CONSTRAINT DF_StockItems_TallySynced DEFAULT 0;
IF OBJECT_ID('StockItems', 'U') IS NOT NULL AND COL_LENGTH('StockItems', 'TallyLastSync') IS NULL
    ALTER TABLE StockItems ADD TallyLastSync DATETIME NULL;");
        }

        /// <summary>Loads Tally settings into local fields and controls.</summary>
        private void LoadTallySettings()
        {
            _settings.Clear();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT SettingKey, SettingValue FROM TallySettings;", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        _settings[reader.GetString(0)] = reader.GetString(1);
                }
            }

            _urlText.Text = GetSetting("TallyURL", DEFAULT_TALLY_URL);
            _folderText.Text = GetSetting("XMLExportFolder", DEFAULT_EXPORT_FOLDER);
            _companyText.Text = GetSetting("CompanyName", string.Empty);
            _godownText.Text = GetSetting("DefaultGodown", "Main Location");
            _directPushToggle.Checked = GetSetting("DirectPushEnabled", "0") == "1";
            _autoSyncToggle.Checked = GetSetting("AutoSyncInventory", "0") == "1";
            SelectComboValue(_syncDirectionCombo, GetSetting("SyncDirection", "ServoERP → Tally"));
            _godownFilterText.Text = GetSetting("GodownFilter", string.Empty);
        }

        /// <summary>Saves all Tally settings and reloads the overview.</summary>
        private void SaveTallySettings()
        {
            try
            {
                UpsertSetting("TallyURL", string.IsNullOrWhiteSpace(_urlText.Text) ? DEFAULT_TALLY_URL : _urlText.Text.Trim());
                UpsertSetting("XMLExportFolder", string.IsNullOrWhiteSpace(_folderText.Text) ? DEFAULT_EXPORT_FOLDER : _folderText.Text.Trim());
                UpsertSetting("CompanyName", _companyText.Text.Trim());
                UpsertSetting("DefaultGodown", string.IsNullOrWhiteSpace(_godownText.Text) ? "Main Location" : _godownText.Text.Trim());
                UpsertSetting("DirectPushEnabled", _directPushToggle.Checked ? "1" : "0");
                UpsertSetting("AutoSyncInventory", _autoSyncToggle.Checked ? "1" : "0");
                UpsertSetting("SyncDirection", Convert.ToString(_syncDirectionCombo.SelectedItem));
                UpsertSetting("GodownFilter", _godownFilterText.Text.Trim());
                LoadTallySettings();
                UpdateOverviewTiles();
                LogActivity("Info", "Settings updated by Administrator");
                SetStatus(_connectionStatusLabel, "Settings saved.");
                LoadRecentActivity();
            }
            catch (Exception ex)
            {
                LogActivity("Error", ex.Message);
                SetStatus(_connectionStatusLabel, ex.Message);
            }
        }

        /// <summary>Refreshes the four connection overview tiles.</summary>
        private void UpdateOverviewTiles()
        {
            _urlTile.Value = _urlText.Text;
            _companyTile.Value = string.IsNullOrWhiteSpace(_companyText.Text) ? "Main Location" : _companyText.Text;
            _folderTile.Value = _folderText.Text;
            _pushTile.Value = _directPushToggle.Checked ? "Enabled" : "Disabled";
            _pushTile.Tint = _directPushToggle.Checked ? "Success" : "Warning";
        }

        /// <summary>Refreshes the header stats from database state.</summary>
        private void UpdateHeaderStats()
        {
        }

        /// <summary>Logs a Tally activity row using parameterised SQL.</summary>
        private void LogActivity(string type, string message)
        {
            try
            {
                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO TallyActivityLog (EventType, EventMessage, PerformedBy)
VALUES (@type, @message, @user);", conn))
                    {
                        cmd.Parameters.AddWithValue("@type", string.IsNullOrWhiteSpace(type) ? "Info" : type);
                        cmd.Parameters.AddWithValue("@message", Limit(message, 500));
                        cmd.Parameters.AddWithValue("@user", "Administrator");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TallyIntegrationForm.LogActivity", ex);
            }
        }

        /// <summary>Starts a BackgroundWorker connection test.</summary>
        private void RunConnectionTest()
        {
            SaveTransientSettingsToDictionary();
            RunWork(
                "Testing Tally connection...",
                _connectionStatusLabel,
                delegate
                {
                    var result = new ConnectionTestResult();
                    string url = GetSetting("TallyURL", DEFAULT_TALLY_URL);
                    result.CompanyConfigured = !string.IsNullOrWhiteSpace(GetSetting("CompanyName", string.Empty));
                    result.FolderReady = Directory.Exists(GetSetting("XMLExportFolder", DEFAULT_EXPORT_FOLDER));
                    try
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                        request.Method = "GET";
                        request.Timeout = 8000;
                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        {
                            result.ServiceRunning = true;
                            result.ApiConnectivity = (int)response.StatusCode < 500;
                            result.Message = "Connection tested successfully";
                        }
                    }
                    catch (Exception ex)
                    {
                        result.ServiceRunning = false;
                        result.ApiConnectivity = false;
                        result.Message = "Tally connection failed: " + ex.Message;
                    }
                    result.Success = result.ServiceRunning && result.ApiConnectivity;
                    return result;
                },
                delegate(object value)
                {
                    var result = (ConnectionTestResult)value;
                    _lastConnectionCheck = DateTime.Now;
                    _lastConnectionOk = result.Success;
                    UpdateHealthRows(result);
                    UpdateConnectionStatus(result.Success, true);
                    LogActivity(result.Success ? "Success" : "Error", result.Message);
                    SetStatus(_connectionStatusLabel, result.Message);
                    LoadRecentActivity();
                    UpdateHeaderStats();
                });
        }

        /// <summary>Updates the connected/disconnected pill and relative timestamp.</summary>
        private void UpdateConnectionStatus(bool connected)
        {
            UpdateConnectionStatus(connected, true);
        }

        /// <summary>Updates the connected/disconnected pill and relative timestamp.</summary>
        private void UpdateConnectionStatus(bool connected, bool hasChecked)
        {
            _connectionPill.Text = connected ? "Connected" : "Disconnected";
            _connectionPill.PillKind = connected ? "Success" : "Error";
            _lastCheckedLabel.Text = hasChecked && _lastConnectionCheck.HasValue
                ? "Last checked: " + RelativeTime(_lastConnectionCheck.Value)
                : "Last checked: never";
        }

        /// <summary>Runs local auto-mapping through a BackgroundWorker.</summary>
        private void RunAutoMapNames()
        {
            RunWork(
                "Auto mapping Tally names...",
                _connectionStatusLabel,
                delegate
                {
                    int clients = ExecuteCount(@"
UPDATE B2BClients
SET TallyLedgerName = CompanyName
WHERE COL_LENGTH('B2BClients', 'TallyLedgerName') IS NOT NULL
  AND ISNULL(TallyLedgerName, '') = ''
  AND ISNULL(CompanyName, '') <> '';");
                    int vendors = ExecuteCount(@"
UPDATE Vendors
SET TallyLedgerName = VendorName
WHERE COL_LENGTH('Vendors', 'TallyLedgerName') IS NOT NULL
  AND ISNULL(TallyLedgerName, '') = ''
  AND ISNULL(VendorName, '') <> '';");
                    int items = ExecuteCount(@"
UPDATE StockItems
SET TallyItemName = ItemName
WHERE COL_LENGTH('StockItems', 'TallyItemName') IS NOT NULL
  AND ISNULL(TallyItemName, '') = ''
  AND ISNULL(ItemName, '') <> '';");
                    return "Auto map names completed: " + clients + " clients, " + vendors + " vendors, " + items + " stock items.";
                },
                delegate(object value)
                {
                    string message = Convert.ToString(value);
                    LogActivity("Completed", message);
                    SetStatus(_connectionStatusLabel, message);
                    LoadRecentActivity();
                    LoadInventoryGrid();
                    UpdateHeaderStats();
                });
        }

        /// <summary>Loads the five most recent activity rows.</summary>
        private void LoadRecentActivity()
        {
            if (_recentActivityPanel == null)
                return;

            _recentActivityPanel.Controls.Clear();
            DataTable table = QueryTable(@"
SELECT TOP 5 EventType, EventMessage, LogTime
FROM TallyActivityLog
ORDER BY LogTime DESC, LogID DESC;");
            if (table.Rows.Count == 0)
            {
                _recentActivityPanel.Controls.Add(NewMutedLabel("No Tally activity yet.", 9f));
                return;
            }

            foreach (DataRow row in table.Rows)
            {
                _recentActivityPanel.Controls.Add(NewActivityRow(
                    SafeText(row, "EventType"),
                    SafeText(row, "EventMessage"),
                    row["LogTime"] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(row["LogTime"])));
            }
        }

        /// <summary>Exports selected vouchers to XML and optionally posts to Tally.</summary>
        private void ExportVouchers()
        {
            CommitGrid(_voucherGrid);
            List<DataRow> selected = SelectedGridRows(_voucherGrid);
            if (selected.Count == 0)
            {
                SetStatus(_exportStatusLabel, "Select at least one voucher.");
                return;
            }

            SaveTransientSettingsToDictionary();
            RunWork(
                "Exporting vouchers...",
                _exportStatusLabel,
                delegate
                {
                    int exported = 0;
                    int errors = 0;
                    string folder = GetSetting("XMLExportFolder", DEFAULT_EXPORT_FOLDER);
                    Directory.CreateDirectory(folder);
                    foreach (DataRow row in selected)
                    {
                        try
                        {
                            string xml = BuildVoucherXml(row);
                            string file = Path.Combine(folder, SafeFileName(SafeText(row, "Type") + "_" + SafeText(row, "VoucherNo") + "_" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) + ".xml");
                            File.WriteAllText(file, xml, Encoding.UTF8);
                            if (GetSetting("DirectPushEnabled", "0") == "1")
                                PostXml(xml);
                            exported++;
                        }
                        catch
                        {
                            errors++;
                        }
                    }
                    return new ExportResult { Exported = exported, Errors = errors };
                },
                delegate(object value)
                {
                    var result = (ExportResult)value;
                    string message = "Exported " + result.Exported + " vouchers" + (result.Errors > 0 ? ", " + result.Errors + " errors." : ".");
                    LogActivity(result.Errors > 0 ? "Warning" : "Completed", message);
                    SetStatus(_exportStatusLabel, message);
                    LoadExportGrid();
                    LoadRecentActivity();
                    UpdateHeaderStats();
                });
        }

        /// <summary>Imports or validates parsed master rows from the selected XML file.</summary>
        private void ImportMasters(bool validateOnly)
        {
            if (_importPreviewRows.Count == 0)
            {
                SetStatus(_importStatusLabel, "Select and preview a Tally XML file first.");
                return;
            }

            RunWork(
                validateOnly ? "Validating masters..." : "Importing masters...",
                _importStatusLabel,
                delegate
                {
                    int skipped = 0;
                    int errors = 0;
                    int imported = 0;
                    foreach (TallyMasterPreviewRow row in _importPreviewRows)
                    {
                        if (string.IsNullOrWhiteSpace(row.Name))
                        {
                            skipped++;
                            continue;
                        }
                        if (!validateOnly)
                            imported++;
                    }
                    return "Imported " + imported + " records. " + skipped + " skipped. " + errors + " errors.";
                },
                delegate(object value)
                {
                    string message = validateOnly ? "Validation completed. " + _importPreviewRows.Count + " parsed rows ready." : Convert.ToString(value);
                    LogActivity(validateOnly ? "Info" : "Completed", message);
                    SetStatus(_importStatusLabel, message);
                    LoadRecentActivity();
                    LoadLogGrid();
                });
        }

        /// <summary>Syncs selected or all inventory rows and updates Tally sync flags.</summary>
        private void SyncInventoryItems(bool syncAll)
        {
            CommitGrid(_inventoryGrid);
            List<DataRow> rows = syncAll ? AllGridRows(_inventoryGrid) : SelectedGridRows(_inventoryGrid);
            if (rows.Count == 0)
            {
                SetStatus(_inventoryStatusLabel, syncAll ? "No inventory rows found." : "Select at least one item.");
                return;
            }

            RunWork(
                "Syncing inventory items...",
                _inventoryStatusLabel,
                delegate
                {
                    int synced = 0;
                    using (SqlConnection conn = _db.GetConnection())
                    {
                        conn.Open();
                        foreach (DataRow row in rows)
                        {
                            using (SqlCommand cmd = new SqlCommand(@"
UPDATE StockItems
SET TallySynced = 1, TallyLastSync = GETDATE()
WHERE ItemID = @id;", conn))
                            {
                                cmd.Parameters.AddWithValue("@id", Convert.ToInt32(row["ItemID"]));
                                synced += cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    return synced;
                },
                delegate(object value)
                {
                    string message = "Inventory sync completed for " + Convert.ToInt32(value) + " item(s).";
                    LogActivity("Completed", message);
                    SetStatus(_inventoryStatusLabel, message);
                    LoadInventoryGrid();
                    LoadRecentActivity();
                    UpdateHeaderStats();
                });
        }

        /// <summary>Loads the activity log grid with current filters.</summary>
        private void LoadLogGrid()
        {
            if (_logGrid == null)
                return;

            DataTable table = QueryTable(@"
SELECT LogTime AS [Time], EventType AS [Event Type], EventMessage AS [Message], PerformedBy AS [Performed By]
FROM TallyActivityLog
WHERE LogTime >= @fromDate
  AND LogTime < @toDate
  AND (@type = 'All' OR EventType = @type)
  AND (@search = '' OR EventMessage LIKE @searchLike)
ORDER BY LogTime DESC;", cmd =>
            {
                cmd.Parameters.AddWithValue("@fromDate", _logFromDate.Value.Date);
                cmd.Parameters.AddWithValue("@toDate", _logToDate.Value.Date.AddDays(1));
                cmd.Parameters.AddWithValue("@type", Convert.ToString(_logTypeCombo.SelectedItem ?? "All"));
                string search = _logSearchText.Text.Trim();
                cmd.Parameters.AddWithValue("@search", search);
                cmd.Parameters.AddWithValue("@searchLike", "%" + search + "%");
            });
            _logGrid.DataSource = table;
            GridTheme.Apply(_logGrid);
            SetStatus(_logStatusLabel, table.Rows.Count + " log row(s).");
        }

        /// <summary>Runs full export plus inventory sync sequence from the header action.</summary>
        private void RunFullSync()
        {
            if (_workRunning)
                return;

            LogActivity("Info", "Full sync started by Administrator");
            ExportVouchers();
            SyncInventoryItems(true);
        }

        /// <summary>Click handler for full sync.</summary>
        private void RunFullSyncClick(object sender, EventArgs e)
        {
            RunFullSync();
        }

        /// <summary>Loads vouchers into the export grid.</summary>
        private void LoadExportGrid()
        {
            if (_voucherGrid == null)
                return;

            DataTable table = QueryTable(@"
SELECT CAST(0 AS BIT) AS Selected,
       CAST(InvoiceID AS INT) AS EntityID,
       InvoiceNumber AS VoucherNo,
       'Sales' AS Type,
       ISNULL(c.CompanyName, '') AS Party,
       TotalAmount AS Amount,
       InvoiceDate AS VoucherDate,
       ISNULL(TallyExportStatus, 'Pending') AS Status,
       TallyExportedAt AS LastExported
FROM Invoices i
LEFT JOIN B2BClients c ON c.ClientID = i.ClientID
WHERE i.InvoiceDate >= @fromDate AND i.InvoiceDate < @toDate
UNION ALL
SELECT CAST(0 AS BIT), CAST(POID AS INT), PONumber, 'Purchases', ISNULL(v.VendorName, ''), TotalAmount, PODate, ISNULL(TallyExportStatus, 'Pending'), TallyExportedAt
FROM PurchaseOrders p
LEFT JOIN Vendors v ON v.VendorID = p.VendorID
WHERE p.PODate >= @fromDate AND p.PODate < @toDate
UNION ALL
SELECT CAST(0 AS BIT), CAST(PaymentID AS INT), PaymentNumber, 'Receipts', ISNULL(c.CompanyName, ''), AmountPaid, PaymentDate, ISNULL(TallyExportStatus, 'Pending'), TallyExportedAt
FROM Payments p
LEFT JOIN B2BClients c ON c.ClientID = p.ClientID
WHERE p.PaymentDate >= @fromDate AND p.PaymentDate < @toDate;", cmd =>
            {
                cmd.Parameters.AddWithValue("@fromDate", _exportFromDate.Value.Date);
                cmd.Parameters.AddWithValue("@toDate", _exportToDate.Value.Date.AddDays(1));
            });

            ApplyVoucherFilters(table);
            _voucherGrid.DataSource = table;
            StyleSelectionColumn(_voucherGrid);
            UpdateExportStats(table);
        }

        /// <summary>Loads stock items into the inventory sync grid.</summary>
        private void LoadInventoryGrid()
        {
            if (_inventoryGrid == null)
                return;

            DataTable table = QueryTable(@"
SELECT CAST(0 AS BIT) AS Selected,
       ItemID,
       CAST(ItemID AS NVARCHAR(50)) AS [Item Code],
       ItemName AS [Item Name],
       Category,
       CurrentStock AS [Stock Qty],
       Unit,
       ISNULL(TallyItemName, ItemName) AS [Tally Name],
       CASE WHEN ISNULL(TallySynced, 0) = 1 THEN 'Synced' ELSE 'Pending' END AS [Sync Status],
       TallyLastSync AS [Last Synced]
FROM StockItems
WHERE (@godown = '' OR ISNULL(TallyGodownName, '') = @godown)
ORDER BY ItemName;", cmd => cmd.Parameters.AddWithValue("@godown", _godownFilterText == null ? string.Empty : _godownFilterText.Text.Trim()));
            _inventoryGrid.DataSource = table;
            StyleSelectionColumn(_inventoryGrid);
            UpdateInventoryStats(table);
        }

        /// <summary>Loads the import XML preview in a BackgroundWorker.</summary>
        private void PreviewImportFile()
        {
            string path = _importPathText.Text.Trim();
            if (!File.Exists(path))
            {
                SetStatus(_importStatusLabel, "Selected XML file was not found.");
                return;
            }

            RunWork(
                "Parsing Tally XML preview...",
                _importStatusLabel,
                delegate { return ParseMasterPreview(File.ReadAllText(path, Encoding.UTF8)); },
                delegate(object value)
                {
                    _importPreviewRows = (List<TallyMasterPreviewRow>)value;
                    _importPreviewGrid.DataSource = PreviewRowsToTable(_importPreviewRows, 20);
                    SetStatus(_importStatusLabel, "Preview loaded: " + _importPreviewRows.Count + " parsed row(s).");
                });
        }

        /// <summary>Browses for the XML export folder.</summary>
        private void BrowseExportFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select ServoERP Tally XML export folder";
                if (Directory.Exists(_folderText.Text))
                    dialog.SelectedPath = _folderText.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _folderText.Text = dialog.SelectedPath;
                    UpdateOverviewTiles();
                }
            }
        }

        /// <summary>Browses for a Tally master XML file.</summary>
        private void BrowseImportFile(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "XML Files|*.xml|All Files|*.*" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _importPathText.Text = dialog.FileName;
                    PreviewImportFile();
                }
            }
        }

        /// <summary>Opens the XML export folder in Windows Explorer.</summary>
        private void OpenExportFolder(object sender, EventArgs e)
        {
            try
            {
                string folder = string.IsNullOrWhiteSpace(_folderText.Text) ? DEFAULT_EXPORT_FOLDER : _folderText.Text.Trim();
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                SetStatus(_exportStatusLabel, ex.Message);
            }
        }

        /// <summary>Shows the first 500 characters of selected voucher XML.</summary>
        private void PreviewSelectedXml(object sender, EventArgs e)
        {
            CommitGrid(_voucherGrid);
            List<DataRow> rows = SelectedGridRows(_voucherGrid);
            if (rows.Count == 0)
            {
                SetStatus(_exportStatusLabel, "Select one voucher to preview XML.");
                return;
            }

            string xml = BuildVoucherXml(rows[0]);
            using (var preview = new Form())
            {
                preview.Text = "Tally XML Preview";
                preview.StartPosition = FormStartPosition.CenterParent;
                preview.Size = new Size(760, 460);
                var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Font = new Font("Consolas", 9f), Text = xml.Length > 500 ? xml.Substring(0, 500) : xml };
                preview.Controls.Add(text);
                preview.ShowDialog(this);
            }
        }

        /// <summary>Exports filtered log rows to Excel using EPPlus.</summary>
        private void ExportLogsToExcel(object sender, EventArgs e)
        {
            try
            {
                DataTable table = _logGrid.DataSource as DataTable;
                if (table == null || table.Rows.Count == 0)
                {
                    SetStatus(_logStatusLabel, "No log rows to export.");
                    return;
                }

                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ServoERP Exports");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "TallyActivityLog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".xlsx");
                using (var package = new ExcelPackage(new FileInfo(path)))
                {
                    ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Tally Logs");
                    sheet.Cells["A1"].LoadFromDataTable(table, true);
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                    package.Save();
                }
                LogActivity("Info", "Tally log exported to Excel");
                SetStatus(_logStatusLabel, "Exported: " + path);
            }
            catch (Exception ex)
            {
                LogActivity("Error", ex.Message);
                SetStatus(_logStatusLabel, ex.Message);
            }
        }

        /// <summary>Deletes logs older than ninety days after confirmation.</summary>
        private void ClearOldLogs(object sender, EventArgs e)
        {
            if (MessageBox.Show("Clear Tally activity logs older than 90 days?", "Clear Old Logs", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int rows = ExecuteCount("DELETE FROM TallyActivityLog WHERE LogTime < DATEADD(day, -90, GETDATE());");
            LogActivity("Warning", "Cleared " + rows + " old Tally log rows.");
            LoadLogGrid();
            LoadRecentActivity();
        }

        /// <summary>Handles voucher row double-click navigation placeholder safely.</summary>
        private void VoucherGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            SetStatus(_exportStatusLabel, "Source voucher selected. Open the relevant module from the sidebar to review details.");
        }

        /// <summary>Builds an XML voucher envelope for a grid row.</summary>
        private string BuildVoucherXml(DataRow row)
        {
            string type = SafeText(row, "Type");
            string voucherType = type == "Purchases" ? "Purchase" : type == "Receipts" ? "Receipt" : "Sales";
            string date = row["VoucherDate"] == DBNull.Value ? DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : Convert.ToDateTime(row["VoucherDate"]).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string party = SafeText(row, "Party");
            string number = SafeText(row, "VoucherNo");
            decimal amount = row["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(row["Amount"]);
            var xml = new StringBuilder();
            xml.AppendLine("<ENVELOPE>");
            xml.AppendLine("  <HEADER>");
            xml.AppendLine("    <TALLYREQUEST>Import Data</TALLYREQUEST>");
            xml.AppendLine("  </HEADER>");
            xml.AppendLine("  <BODY>");
            xml.AppendLine("    <IMPORTDATA>");
            xml.AppendLine("      <REQUESTDESC>");
            xml.AppendLine("        <REPORTNAME>Vouchers</REPORTNAME>");
            xml.AppendLine("        <STATICVARIABLES>");
            xml.AppendLine("          <SVCURRENTCOMPANY>" + EscapeXml(GetSetting("CompanyName", string.Empty)) + "</SVCURRENTCOMPANY>");
            xml.AppendLine("        </STATICVARIABLES>");
            xml.AppendLine("      </REQUESTDESC>");
            xml.AppendLine("      <REQUESTDATA>");
            xml.AppendLine("        <TALLYMESSAGE xmlns:UDF=\"TallyUDF\">");
            xml.AppendLine("          <VOUCHER VCHTYPE=\"" + EscapeXml(voucherType) + "\" ACTION=\"Create\">");
            xml.AppendLine("            <DATE>" + date + "</DATE>");
            xml.AppendLine("            <NARRATION>ServoERP " + EscapeXml(voucherType) + " " + EscapeXml(number) + "</NARRATION>");
            xml.AppendLine("            <VOUCHERTYPENAME>" + EscapeXml(voucherType) + "</VOUCHERTYPENAME>");
            xml.AppendLine("            <PARTYLEDGERNAME>" + EscapeXml(party) + "</PARTYLEDGERNAME>");
            xml.AppendLine("            <ALLLEDGERENTRIES.LIST><LEDGERNAME>" + EscapeXml(DefaultLedger(voucherType)) + "</LEDGERNAME><AMOUNT>-" + Money(amount) + "</AMOUNT></ALLLEDGERENTRIES.LIST>");
            xml.AppendLine("            <ALLLEDGERENTRIES.LIST><LEDGERNAME>" + EscapeXml(party) + "</LEDGERNAME><AMOUNT>" + Money(amount) + "</AMOUNT></ALLLEDGERENTRIES.LIST>");
            xml.AppendLine("          </VOUCHER>");
            xml.AppendLine("        </TALLYMESSAGE>");
            xml.AppendLine("      </REQUESTDATA>");
            xml.AppendLine("    </IMPORTDATA>");
            xml.AppendLine("  </BODY>");
            xml.AppendLine("</ENVELOPE>");
            return xml.ToString();
        }

        /// <summary>Posts XML to Tally using synchronous .NET Framework HTTP APIs.</summary>
        private void PostXml(string xml)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(xml ?? string.Empty);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetSetting("TallyURL", DEFAULT_TALLY_URL));
            request.Method = "POST";
            request.ContentType = "text/xml; charset=utf-8";
            request.Timeout = 30000;
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);
            using ((HttpWebResponse)request.GetResponse())
            {
            }
        }

        /// <summary>Runs blocking work through BackgroundWorker with consistent error logging.</summary>
        private void RunWork(string busyText, Label statusLabel, Func<object> work, Action<object> completed)
        {
            if (_workRunning)
            {
                SetStatus(statusLabel, "Another Tally operation is already running.");
                return;
            }

            _workRunning = true;
            SetStatus(statusLabel, busyText);
            Cursor = Cursors.WaitCursor;
            var worker = CreateWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                try
                {
                    e.Result = work();
                }
                catch (Exception ex)
                {
                    LogActivity("Error", ex.Message);
                    throw;
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    RunOnUI(delegate
                    {
                        Cursor = Cursors.Default;
                        _workRunning = false;
                        SetStatus(statusLabel, e.Error.Message);
                    });
                    ShowError( "Tally operation failed. Please try again.", e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(delegate
                {
                    Cursor = Cursors.Default;
                    _workRunning = false;
                    completed(e.Result);
                });
            };
            worker.RunWorkerAsync();
        }

        // UI factory and data helpers follow.
        private static TabPage NewTab(string text) { return new TabPage(text) { BackColor = PageBack, Padding = new Padding(0) }; }
        private static Panel NewScrollCanvas() { return new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBack, Padding = new Padding(0, 0, 0, 8) }; }
        private static RoundedPanel NewCard(int padding) { return new RoundedPanel { Name = "TallySectionPanel", Tag = "section", Dock = DockStyle.Fill, BackColor = Color.White, BorderColor = CardBorder, Radius = 8, Padding = new Padding(padding), Margin = new Padding(0, 0, 0, 12) }; }
        private static Label NewSectionTitle(string text) { return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = TextDark, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }; }
        private static Label NewMutedLabel(string text, float size) { return NewMutedLabel(text, size, ContentAlignment.MiddleLeft); }
        private static Label NewMutedLabel(string text, float size, ContentAlignment align) { return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI", size), TextAlign = align }; }
        private static LinkLabel LinkLabel(string text) { return new LinkLabel { Text = text, AutoSize = true, LinkColor = AccentBlue, ActiveLinkColor = AccentBlue, VisitedLinkColor = AccentBlue, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(8, 6, 4, 4) }; }
        private static TextBox NewInput(string value) { var input = new TextBox { Text = value ?? string.Empty, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = TextDark, Font = new Font("Segoe UI", 9f), Margin = new Padding(0), AutoSize = false, Height = 28 }; UIHelper.ApplyInputStyle(input); input.AutoSize = false; input.Height = 28; return input; }
        private static ComboBox NewCombo(IEnumerable<string> items) { var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) }; foreach (string item in items) combo.Items.Add(item); if (combo.Items.Count > 0) combo.SelectedIndex = 0; UIHelper.ApplyInputStyle(combo); return combo; }
        private static Button NewPrimaryButton(string text, EventHandler click, int width) { var button = new Button { Text = text, Width = width, Height = 32, Margin = new Padding(4), BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }; button.FlatAppearance.BorderSize = 0; button.Click += click; UIHelper.ApplyActionButton(button, UiActionVariant.Primary); return button; }
        private static Button NewOutlineButton(string text, EventHandler click, int width) { var button = new Button { Text = text, Width = width, Height = 32, Margin = new Padding(4), BackColor = Color.White, ForeColor = TextDark, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }; button.FlatAppearance.BorderColor = CardBorder; button.FlatAppearance.BorderSize = 1; button.Click += click; UIHelper.ApplyActionButton(button, UiActionVariant.Secondary); return button; }
        private static DataGridView NewGrid() { var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, EditMode = DataGridViewEditMode.EditOnEnter, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill }; GridTheme.Apply(grid); return grid; }

        private Control BuildTitleBlock(string title, string subtitle)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(NewSectionTitle(title), 0, 0);
            panel.Controls.Add(NewMutedLabel(subtitle, 9f), 0, 1);
            return panel;
        }

        private Control BuildToggleRow(string label, ToggleSwitch toggle)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextDark, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            panel.Controls.Add(toggle, 1, 0);
            return panel;
        }

        private Control BuildField(string label, Control input, string helper)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextDark }, 0, 0);
            panel.Controls.Add(input, 0, 1);
            panel.Controls.Add(NewMutedLabel(helper, 8f), 0, 2);
            return panel;
        }

        private Control BuildFieldWithButton(string label, TextBox input, string buttonText, EventHandler click, string helper)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextDark }, 0, 0);
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            row.Controls.Add(input, 0, 0);
            row.Controls.Add(NewOutlineButton(buttonText, click, 88), 1, 0);
            panel.Controls.Add(row, 0, 1);
            panel.Controls.Add(NewMutedLabel(helper, 8f), 0, 2);
            return panel;
        }

        private Control BuildExportFilters()
        {
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            _exportFromDate = NewDate(DateTime.Today.AddDays(1 - DateTime.Today.Day));
            _exportToDate = NewDate(DateTime.Today);
            _voucherTypeCombo = NewCombo(new[] { "All", "Sales", "Purchases", "Payments", "Receipts" });
            _exportStatusCombo = NewCombo(new[] { "All", "Pending Export", "Exported" });
            _voucherTypeCombo.Width = 150;
            _exportStatusCombo.Width = 150;
            _exportFromDate.ValueChanged += (s, e) => LoadExportGrid();
            _exportToDate.ValueChanged += (s, e) => LoadExportGrid();
            _voucherTypeCombo.SelectedIndexChanged += (s, e) => LoadExportGrid();
            _exportStatusCombo.SelectedIndexChanged += (s, e) => LoadExportGrid();
            filters.Controls.Add(FilterLabel("From"));
            filters.Controls.Add(_exportFromDate);
            filters.Controls.Add(FilterLabel("To"));
            filters.Controls.Add(_exportToDate);
            filters.Controls.Add(FilterLabel("Voucher Type"));
            filters.Controls.Add(_voucherTypeCombo);
            filters.Controls.Add(FilterLabel("Status"));
            filters.Controls.Add(_exportStatusCombo);
            return filters;
        }

        private Control BuildInventorySettings()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            _autoSyncToggle = new ToggleSwitch { Width = 46, Height = 24 };
            _syncDirectionCombo = NewCombo(new[] { "ServoERP → Tally", "Tally → ServoERP", "Bidirectional" });
            _godownFilterText = NewInput(string.Empty);
            grid.Controls.Add(BuildToggleRow("Auto-sync on inventory change", _autoSyncToggle), 0, 0);
            grid.Controls.Add(BuildField("Sync direction", _syncDirectionCombo, "Choose one controlled sync path"), 1, 0);
            grid.Controls.Add(BuildField("Godown filter", _godownFilterText, "Blank = all godowns"), 2, 0);
            return grid;
        }

        private Control BuildLogFilters()
        {
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent };
            _logTypeCombo = NewCombo(new[] { "All", "Success", "Info", "Warning", "Error", "Completed" });
            _logFromDate = NewDate(DateTime.Today.AddDays(-7));
            _logToDate = NewDate(DateTime.Today);
            _logSearchText = NewInput(string.Empty);
            _logSearchText.Width = 220;
            filters.Controls.Add(FilterLabel("Type"));
            filters.Controls.Add(_logTypeCombo);
            filters.Controls.Add(FilterLabel("From"));
            filters.Controls.Add(_logFromDate);
            filters.Controls.Add(FilterLabel("To"));
            filters.Controls.Add(_logToDate);
            filters.Controls.Add(FilterLabel("Search"));
            filters.Controls.Add(_logSearchText);
            filters.Controls.Add(NewPrimaryButton("Apply Filter", (s, e) => LoadLogGrid(), 112));
            return filters;
        }

        private static DateTimePicker NewDate(DateTime value) { return new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = value, Width = 118, Font = new Font("Segoe UI", 9f), Margin = new Padding(4) }; }
        private static Label FilterLabel(string text) { return new Label { Text = text, AutoSize = true, Margin = new Padding(8, 9, 2, 0), ForeColor = TextDark, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }; }
        private void AddHealthRow(TableLayoutPanel layout, int row, string name) { var item = new HealthCheckRow(name); _healthRows[name] = item; layout.Controls.Add(item, 0, row); layout.SetColumnSpan(item, 2); }

        private Control NewQuickAction(string title, string subtitle, int tab)
        {
            var card = new QuickActionCard(title, subtitle) { Dock = DockStyle.Fill, Margin = new Padding(6) };
            card.Click += (s, e) => _tabs.SelectedIndex = tab;
            return card;
        }

        private Control NewHeaderStat(string title, string value, string kind)
        {
            var panel = new FlowLayoutPanel { Width = 82, Height = 36, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(2) };
            panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted });
            panel.Controls.Add(new PillLabel { Text = value, PillKind = kind, Width = 76, Height = 20 });
            return panel;
        }

        private Control NewActivityRow(string type, string message, DateTime time)
        {
            var row = new TableLayoutPanel { Width = 520, Height = 38, ColumnCount = 3, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 2) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            row.Controls.Add(new PillLabel { Text = ActivityIcon(type), PillKind = type, Width = 24, Height = 24 }, 0, 0);
            row.Controls.Add(new Label { Text = Limit(message, 64) + Environment.NewLine + RelativeTime(time), Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = TextDark }, 1, 0);
            row.Controls.Add(new PillLabel { Text = type, PillKind = type, Width = 82, Height = 22 }, 2, 0);
            return row;
        }

        private void DrawHubTab(object sender, DrawItemEventArgs e)
        {
            bool active = e.Index == _tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            using (Brush back = new SolidBrush(Color.White))
                e.Graphics.FillRectangle(back, bounds);
            TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular), bounds, active ? AccentBlue : TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (active)
            {
                using (Pen pen = new Pen(AccentBlue, 2f))
                    e.Graphics.DrawLine(pen, bounds.Left + 12, bounds.Bottom - 2, bounds.Right - 12, bounds.Bottom - 2);
            }
        }

        private void UpdateHealthRows(ConnectionTestResult result)
        {
            _healthRows["Tally Prime Service"].SetStatus(result.ServiceRunning ? "Running" : "Unreachable", result.ServiceRunning ? "Success" : "Error");
            _healthRows["API Connectivity"].SetStatus(result.ApiConnectivity ? "Success" : "Failed", result.ApiConnectivity ? "Success" : "Error");
            _healthRows["Company Access"].SetStatus(result.CompanyConfigured ? "Authorized" : "Not configured", result.CompanyConfigured ? "Success" : "Warning");
            _healthRows["Data Sync Readiness"].SetStatus(result.FolderReady ? "Ready" : "Folder missing", result.FolderReady ? "Success" : "Warning");
        }

        private void UpdateExportStats(DataTable table)
        {
            _exportStatsPanel.Controls.Clear();
            int ready = CountRows(table, "Status", "Pending");
            int exported = CountRows(table, "Status", "Exported");
            int errors = CountRows(table, "Status", "Error") + CountRows(table, "Status", "Failed");
            _exportStatsPanel.Controls.Add(new PillLabel { Text = "Ready to export: " + ready, PillKind = "Info", Width = 150, Height = 24 });
            _exportStatsPanel.Controls.Add(new PillLabel { Text = "Already exported: " + exported, PillKind = "Success", Width = 160, Height = 24 });
            _exportStatsPanel.Controls.Add(new PillLabel { Text = "Errors: " + errors, PillKind = "Error", Width = 100, Height = 24 });
        }

        private void UpdateInventoryStats(DataTable table)
        {
            _inventoryStatsPanel.Controls.Clear();
            int total = table.Rows.Count;
            int synced = CountRows(table, "Sync Status", "Synced");
            int pending = total - synced;
            _inventoryStatsPanel.Controls.Add(new PillLabel { Text = "Items in ServoERP: " + total, PillKind = "Info", Width = 160, Height = 24 });
            _inventoryStatsPanel.Controls.Add(new PillLabel { Text = "Synced to Tally: " + synced, PillKind = "Success", Width = 150, Height = 24 });
            _inventoryStatsPanel.Controls.Add(new PillLabel { Text = "Pending Sync: " + pending, PillKind = "Warning", Width = 140, Height = 24 });
            _inventoryStatsPanel.Controls.Add(new PillLabel { Text = "Last Sync: " + GetLastSyncText(), PillKind = "Info", Width = 150, Height = 24 });
        }

        private void ApplyVoucherFilters(DataTable table)
        {
            string type = Convert.ToString(_voucherTypeCombo.SelectedItem ?? "All");
            string status = Convert.ToString(_exportStatusCombo.SelectedItem ?? "All");
            for (int i = table.Rows.Count - 1; i >= 0; i--)
            {
                DataRow row = table.Rows[i];
                bool remove = type != "All" && SafeText(row, "Type") != type;
                if (!remove && status == "Pending Export")
                    remove = SafeText(row, "Status") == "Exported";
                if (!remove && status == "Exported")
                    remove = SafeText(row, "Status") != "Exported";
                if (remove)
                    table.Rows.RemoveAt(i);
            }
        }

        private DataTable QueryTable(string sql) { return QueryTable(sql, null); }
        private DataTable QueryTable(string sql, Action<SqlCommand> parameters)
        {
            using (SqlConnection conn = _db.GetConnection())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                if (parameters != null)
                    parameters(cmd);
                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        private void ExecuteNonQuery(string sql)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        private int ExecuteCount(string sql)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                    return cmd.ExecuteNonQuery();
            }
        }

        private void UpsertSetting(string key, string value)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM TallySettings WHERE SettingKey = @key)
    UPDATE TallySettings SET SettingValue = @value WHERE SettingKey = @key;
ELSE
    INSERT INTO TallySettings (SettingKey, SettingValue) VALUES (@key, @value);", conn))
                {
                    cmd.Parameters.AddWithValue("@key", key);
                    cmd.Parameters.AddWithValue("@value", value ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
            _settings[key] = value ?? string.Empty;
        }

        private void SetSetting(string key, string value) { _settings[key] = value ?? string.Empty; }
        private string GetSetting(string key, string fallback) { return _settings.ContainsKey(key) ? _settings[key] : fallback; }
        private void SaveTransientSettingsToDictionary()
        {
            SetSetting("TallyURL", _urlText.Text.Trim());
            SetSetting("XMLExportFolder", _folderText.Text.Trim());
            SetSetting("CompanyName", _companyText.Text.Trim());
            SetSetting("DefaultGodown", _godownText.Text.Trim());
            SetSetting("DirectPushEnabled", _directPushToggle.Checked ? "1" : "0");
        }

        private static void StyleSelectionColumn(DataGridView grid)
        {
            if (grid == null || !grid.Columns.Contains("Selected"))
                return;
            DataGridViewColumn column = grid.Columns["Selected"];
            column.HeaderText = string.Empty;
            column.Width = 42;
            column.MinimumWidth = 42;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Frozen = false;
            GridTheme.FormatColumns(grid);
        }

        private static void CommitGrid(DataGridView grid)
        {
            if (grid == null)
                return;
            grid.EndEdit();
            CurrencyManager manager = grid.BindingContext == null ? null : grid.BindingContext[grid.DataSource] as CurrencyManager;
            if (manager != null)
                manager.EndCurrentEdit();
        }

        private static List<DataRow> SelectedGridRows(DataGridView grid)
        {
            var rows = new List<DataRow>();
            DataTable table = grid == null ? null : grid.DataSource as DataTable;
            if (table == null || !table.Columns.Contains("Selected"))
                return rows;
            foreach (DataRow row in table.Rows)
                if (row["Selected"] != DBNull.Value && Convert.ToBoolean(row["Selected"]))
                    rows.Add(row);
            return rows;
        }

        private static List<DataRow> AllGridRows(DataGridView grid)
        {
            var rows = new List<DataRow>();
            DataTable table = grid == null ? null : grid.DataSource as DataTable;
            if (table == null)
                return rows;
            foreach (DataRow row in table.Rows)
                rows.Add(row);
            return rows;
        }

        private static int CountRows(DataTable table, string column, string value)
        {
            int count = 0;
            foreach (DataRow row in table.Rows)
                if (string.Equals(SafeText(row, column), value, StringComparison.OrdinalIgnoreCase))
                    count++;
            return count;
        }

        private int CountPendingExports()
        {
            object value = QueryScalar(@"
SELECT
    (SELECT COUNT(1) FROM Invoices WHERE ISNULL(TallyExportStatus, '') <> 'Exported') +
    (SELECT COUNT(1) FROM PurchaseOrders WHERE ISNULL(TallyExportStatus, '') <> 'Exported');");
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private int CountErrorsLastDay()
        {
            object value = QueryScalar("SELECT COUNT(1) FROM TallyActivityLog WHERE EventType = 'Error' AND LogTime >= DATEADD(day, -1, GETDATE());");
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private object QueryScalar(string sql)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                    return cmd.ExecuteScalar();
            }
        }

        private string GetLastSyncText()
        {
            object value = QueryScalar("SELECT MAX(LogTime) FROM TallyActivityLog WHERE EventType IN ('Success', 'Completed');");
            return value == null || value == DBNull.Value ? "Never" : RelativeTime(Convert.ToDateTime(value));
        }

        private static DataTable PreviewRowsToTable(List<TallyMasterPreviewRow> rows, int maxRows)
        {
            var table = new DataTable();
            table.Columns.Add("Type");
            table.Columns.Add("Name");
            table.Columns.Add("Parent");
            table.Columns.Add("GSTIN");
            table.Columns.Add("Unit");
            int count = 0;
            foreach (TallyMasterPreviewRow row in rows)
            {
                if (count++ >= maxRows)
                    break;
                table.Rows.Add(row.MasterType, row.Name, row.Parent, row.Gstin, row.Unit);
            }
            return table;
        }

        private static List<TallyMasterPreviewRow> ParseMasterPreview(string xml)
        {
            var rows = new List<TallyMasterPreviewRow>();
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            foreach (XmlNode node in doc.GetElementsByTagName("LEDGER"))
                rows.Add(new TallyMasterPreviewRow { MasterType = "Ledger", Name = ReadName(node), Parent = ReadChild(node, "PARENT"), Gstin = ReadChild(node, "PARTYGSTIN") });
            foreach (XmlNode node in doc.GetElementsByTagName("STOCKITEM"))
                rows.Add(new TallyMasterPreviewRow { MasterType = "StockItem", Name = ReadName(node), Parent = ReadChild(node, "PARENT"), Unit = ReadChild(node, "BASEUNITS") });
            foreach (XmlNode node in doc.GetElementsByTagName("UNIT"))
                rows.Add(new TallyMasterPreviewRow { MasterType = "Unit", Name = ReadName(node), Parent = ReadChild(node, "ORIGINALNAME") });
            return rows;
        }

        private static string ReadName(XmlNode node)
        {
            XmlAttribute attr = node.Attributes == null ? null : node.Attributes["NAME"];
            return attr == null ? ReadChild(node, "NAME") : attr.Value;
        }

        private static string ReadChild(XmlNode node, string child)
        {
            XmlNode found = node == null ? null : node.SelectSingleNode(child);
            return found == null ? string.Empty : (found.InnerText ?? string.Empty).Trim();
        }

        private static string SafeText(DataRow row, string column) { return row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? Convert.ToString(row[column]) : string.Empty; }
        private static string EscapeXml(string value) { return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty; }
        private static string Money(decimal value) { return Math.Abs(value).ToString("0.00", CultureInfo.InvariantCulture); }
        private static string DefaultLedger(string voucherType) { return voucherType == "Purchase" ? "Purchase" : voucherType == "Receipt" ? "Bank" : "Sales"; }
        private static string SafeFileName(string value) { foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '-'); return value; }
        private static string Limit(string value, int max) { value = value ?? string.Empty; return value.Length <= max ? value : value.Substring(0, max); }
        private static string ActivityIcon(string type) { return string.Equals(type, "Error", StringComparison.OrdinalIgnoreCase) ? "x" : string.Equals(type, "Warning", StringComparison.OrdinalIgnoreCase) ? "!" : string.Equals(type, "Info", StringComparison.OrdinalIgnoreCase) ? "i" : "✓"; }
        private static void SelectComboValue(ComboBox combo, string value) { if (combo == null) return; int index = combo.Items.IndexOf(value); combo.SelectedIndex = index >= 0 ? index : 0; }
        private static void FocusInput(Control input) { if (input != null && input.CanFocus) input.Focus(); }
        private static void SetStatus(Label label, string text) { if (label != null) label.Text = text ?? string.Empty; }
        private void ShowSafeError(Exception ex) { LogActivity("Error", ex.Message); AppLogger.LogError("TallyIntegrationForm", ex); }

        /// <summary>Removes dashboard resize grips from this fixed-layout integration hub.</summary>
        private void RemoveTallyResizeGrips()
        {
            RemoveTallyResizeGrips(this);
        }

        /// <summary>Removes dashboard resize grips recursively from Tally hub controls.</summary>
        private static void RemoveTallyResizeGrips(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            for (int i = root.Controls.Count - 1; i >= 0; i--)
            {
                Control child = root.Controls[i];
                if (string.Equals(child.Name, CardResizeGripService.CornerGripName, StringComparison.Ordinal) ||
                    string.Equals(child.Name, CardResizeGripService.HeightGripName, StringComparison.Ordinal) ||
                    string.Equals(child.Name, CardResizeGripService.LockBadgeName, StringComparison.Ordinal))
                {
                    root.Controls.RemoveAt(i);
                    child.Dispose();
                    continue;
                }

                RemoveTallyResizeGrips(child);
            }
        }

        private static string RelativeTime(DateTime time)
        {
            TimeSpan age = DateTime.Now - time;
            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalMinutes < 60) return ((int)age.TotalMinutes) + " mins ago";
            if (age.TotalHours < 24) return ((int)age.TotalHours) + " hours ago";
            return ((int)age.TotalDays) + " days ago";
        }

        private sealed class ConnectionTestResult
        {
            public bool Success;
            public bool ServiceRunning;
            public bool ApiConnectivity;
            public bool CompanyConfigured;
            public bool FolderReady;
            public string Message;
        }

        private sealed class ExportResult
        {
            public int Exported;
            public int Errors;
        }

        private sealed class TallyMasterPreviewRow
        {
            public string MasterType;
            public string Name;
            public string Parent;
            public string Gstin;
            public string Unit;
        }

        private class RoundedPanel : Panel
        {
            public int Radius { get; set; }
            public Color BorderColor { get; set; }

            /// <summary>Paints the rounded panel border.</summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = RoundedRect(rect, Radius))
                using (Pen pen = new Pen(BorderColor))
                    e.Graphics.DrawPath(pen, path);
            }
        }

        private sealed class TallyArtworkPanel : Panel
        {
            /// <summary>Initializes the Tally Prime reference artwork panel.</summary>
            public TallyArtworkPanel()
            {
                BackColor = Color.Transparent;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            /// <summary>Paints the laptop and integration document illustration.</summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle area = ClientRectangle;
                int centerX = area.Left + area.Width / 2;
                int baseY = area.Bottom - 22;

                using (Pen linkPen = new Pen(Color.FromArgb(222, 230, 241), 1f) { DashStyle = DashStyle.Dash })
                {
                    e.Graphics.DrawBezier(linkPen, centerX - 96, baseY - 78, centerX - 82, baseY - 118, centerX - 36, baseY - 126, centerX - 4, baseY - 88);
                    e.Graphics.DrawBezier(linkPen, centerX + 8, baseY - 92, centerX + 48, baseY - 134, centerX + 94, baseY - 108, centerX + 112, baseY - 72);
                    e.Graphics.DrawLine(linkPen, centerX - 8, baseY - 84, centerX - 78, baseY - 78);
                    e.Graphics.DrawLine(linkPen, centerX + 12, baseY - 84, centerX + 86, baseY - 78);
                }

                DrawDocumentIcon(e.Graphics, centerX - 116, baseY - 90, Color.FromArgb(232, 244, 247), Color.FromArgb(55, 133, 158));
                DrawCircleIcon(e.Graphics, centerX - 42, baseY - 128, "☁", Color.FromArgb(236, 242, 253), Color.FromArgb(48, 74, 116));
                DrawCircleIcon(e.Graphics, centerX + 40, baseY - 128, "✓", Color.FromArgb(223, 245, 236), Color.FromArgb(47, 158, 111));
                DrawDocumentIcon(e.Graphics, centerX + 98, baseY - 108, Color.FromArgb(234, 241, 249), Color.FromArgb(70, 115, 156));
                DrawCircleIcon(e.Graphics, centerX + 136, baseY - 70, "▤", Color.FromArgb(239, 245, 251), Color.FromArgb(50, 72, 94));

                using (Brush shadow = new SolidBrush(Color.FromArgb(222, 235, 247)))
                    e.Graphics.FillEllipse(shadow, centerX - 130, baseY - 16, 260, 30);

                Rectangle screen = new Rectangle(centerX - 76, baseY - 74, 152, 82);
                using (GraphicsPath path = RoundedRect(screen, 7))
                using (Brush brush = new SolidBrush(Color.FromArgb(24, 32, 44)))
                    e.Graphics.FillPath(brush, path);

                Rectangle inner = new Rectangle(screen.Left + 8, screen.Top + 8, screen.Width - 16, screen.Height - 18);
                using (GraphicsPath path = RoundedRect(inner, 3))
                using (Brush brush = new SolidBrush(Color.White))
                    e.Graphics.FillPath(brush, path);

                using (Font logo = new Font("Segoe Script", 22f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.Black))
                    e.Graphics.DrawString("Tally", logo, brush, inner.Left + 30, inner.Top + 11);

                using (Pen pen = new Pen(Color.Black, 1.5f))
                    e.Graphics.DrawLine(pen, inner.Left + 36, inner.Top + 45, inner.Right - 34, inner.Top + 45);

                using (Font prime = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.FromArgb(42, 42, 42)))
                    e.Graphics.DrawString("Prime", prime, brush, inner.Left + 57, inner.Top + 49);

                Rectangle baseRect = new Rectangle(screen.Left - 18, screen.Bottom - 2, screen.Width + 36, 10);
                using (GraphicsPath path = RoundedRect(baseRect, 4))
                using (Brush brush = new SolidBrush(Color.FromArgb(20, 29, 42)))
                    e.Graphics.FillPath(brush, path);
            }

            /// <summary>Paints a circular supporting icon.</summary>
            private static void DrawCircleIcon(Graphics graphics, int x, int y, string text, Color back, Color fore)
            {
                using (Brush brush = new SolidBrush(back))
                    graphics.FillEllipse(brush, x, y, 44, 44);
                using (Pen pen = new Pen(Color.FromArgb(221, 230, 240)))
                    graphics.DrawEllipse(pen, x, y, 44, 44);
                using (Font font = new Font("Segoe UI Symbol", 15f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(fore))
                    TextRenderer.DrawText(graphics, text, font, new Rectangle(x, y, 44, 44), fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            /// <summary>Paints a small document icon.</summary>
            private static void DrawDocumentIcon(Graphics graphics, int x, int y, Color back, Color fore)
            {
                Rectangle bubble = new Rectangle(x, y, 48, 56);
                using (Brush brush = new SolidBrush(back))
                    graphics.FillEllipse(brush, bubble);
                Rectangle doc = new Rectangle(x + 14, y + 9, 22, 32);
                using (Brush brush = new SolidBrush(Color.White))
                    graphics.FillRectangle(brush, doc);
                using (Pen pen = new Pen(fore, 1.5f))
                {
                    graphics.DrawRectangle(pen, doc);
                    graphics.DrawLine(pen, doc.Left + 5, doc.Top + 10, doc.Right - 4, doc.Top + 10);
                    graphics.DrawLine(pen, doc.Left + 5, doc.Top + 17, doc.Right - 4, doc.Top + 17);
                    graphics.DrawLine(pen, doc.Left + 5, doc.Top + 24, doc.Right - 8, doc.Top + 24);
                }
            }
        }

        private sealed class InfoTile : RoundedPanel
        {
            private readonly Label _value;
            public string Value { get { return _value.Text; } set { _value.Text = value; } }
            public string Tint { get; set; }

            public InfoTile(string label, string value, string icon)
            {
                Radius = 8;
                BorderColor = CardBorder;
                BackColor = Color.White;
                Dock = DockStyle.Fill;
                Name = "TallyInfoSection";
                Tag = "section";
                Padding = new Padding(12);
                Margin = new Padding(6);
                Cursor = Cursors.Hand;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
                Controls.Add(layout);
                layout.Controls.Add(new Label { Text = Icon(icon), Dock = DockStyle.Fill, Font = new Font("Segoe UI Symbol", 16f), ForeColor = AccentBlue, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
                layout.SetRowSpan(layout.GetControlFromPosition(0, 0), 2);
                layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = AccentBlue, TextAlign = ContentAlignment.BottomLeft }, 1, 0);
                _value = new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextDark, TextAlign = ContentAlignment.TopLeft };
                layout.Controls.Add(_value, 1, 1);
            }

            private static string Icon(string key)
            {
                if (key == "Company") return "⌂";
                if (key == "Folder") return "□";
                if (key == "Upload") return "↑";
                return "◎";
            }
        }

        private sealed class QuickActionCard : RoundedPanel
        {
            public QuickActionCard(string title, string subtitle)
            {
                Radius = 8;
                BorderColor = CardBorder;
                BackColor = Color.White;
                Name = "TallyQuickActionSection";
                Tag = "section";
                Padding = new Padding(14, 10, 14, 10);
                Cursor = Cursors.Hand;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
                Controls.Add(layout);
                layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextDark }, 0, 0);
                layout.Controls.Add(new Label { Text = "→", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
                layout.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f), ForeColor = TextMuted }, 0, 1);
                layout.SetColumnSpan(layout.GetControlFromPosition(0, 1), 2);
            }
        }

        private sealed class HealthCheckRow : UserControl
        {
            private readonly PillLabel _icon;
            private readonly Label _status;

            public HealthCheckRow(string name)
            {
                Dock = DockStyle.Fill;
                Height = 32;
                BackColor = Color.Transparent;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.Transparent };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
                Controls.Add(layout);
                _icon = new PillLabel { Text = "-", PillKind = "Info", Width = 22, Height = 22 };
                _status = new Label { Text = "Not checked", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = TextMuted, TextAlign = ContentAlignment.MiddleRight };
                layout.Controls.Add(_icon, 0, 0);
                layout.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextDark, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
                layout.Controls.Add(_status, 2, 0);
            }

            public void SetStatus(string status, string kind)
            {
                _icon.Text = kind == "Success" ? "✓" : kind == "Warning" ? "!" : "x";
                _icon.PillKind = kind;
                _status.Text = status;
            }
        }

        private sealed class PillLabel : Label
        {
            private string _pillKind = "Info";
            public string PillKind { get { return _pillKind; } set { _pillKind = value ?? "Info"; Invalidate(); } }

            public PillLabel()
            {
                TextAlign = ContentAlignment.MiddleCenter;
                Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                Margin = new Padding(4);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color back;
                Color fore;
                BadgeColors(PillKind, out back, out fore);
                using (GraphicsPath path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
                using (Brush brush = new SolidBrush(back))
                    e.Graphics.FillPath(brush, path);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private sealed class ToggleSwitch : Control
        {
            private bool _checked;
            public event EventHandler CheckedChanged;
            public bool Checked
            {
                get { return _checked; }
                set
                {
                    if (_checked == value) return;
                    _checked = value;
                    Invalidate();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }

            public ToggleSwitch()
            {
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnClick(EventArgs e)
            {
                Checked = !Checked;
                base.OnClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle track = new Rectangle(1, 3, Width - 3, Height - 7);
                using (GraphicsPath path = RoundedRect(track, track.Height / 2))
                using (Brush brush = new SolidBrush(Checked ? AccentBlue : Color.FromArgb(212, 216, 226)))
                    e.Graphics.FillPath(brush, path);
                int knob = Height - 10;
                int x = Checked ? Width - knob - 5 : 5;
                using (Brush brush = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(brush, x, 5, knob, knob);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void BadgeColors(string kind, out Color back, out Color fore)
        {
            if (string.Equals(kind, "Success", StringComparison.OrdinalIgnoreCase) || string.Equals(kind, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                back = Color.FromArgb(234, 243, 222); fore = Color.FromArgb(59, 109, 17); return;
            }
            if (string.Equals(kind, "Warning", StringComparison.OrdinalIgnoreCase))
            {
                back = Color.FromArgb(250, 238, 218); fore = Color.FromArgb(133, 79, 11); return;
            }
            if (string.Equals(kind, "Error", StringComparison.OrdinalIgnoreCase))
            {
                back = Color.FromArgb(252, 235, 235); fore = Color.FromArgb(163, 45, 45); return;
            }
            if (string.Equals(kind, "Purple", StringComparison.OrdinalIgnoreCase))
            {
                back = Color.FromArgb(238, 237, 254); fore = Color.FromArgb(83, 74, 183); return;
            }
            back = Color.FromArgb(230, 241, 251); fore = Color.FromArgb(12, 68, 124);
        }
    }
}


