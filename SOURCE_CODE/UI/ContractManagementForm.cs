using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>
    /// Contract workspace with dashboard and new-contract form views.
    /// The host shell/sidebar owns navigation; this control only manages the contracts page body.
    /// </summary>
    public class ContractManagementForm : DeferredPageControl
    {
        private readonly ContractService _contractSvc = new ContractService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly SiteService _siteSvc = new SiteService();

        private readonly Dictionary<int, B2BClient> _clientsById = new Dictionary<int, B2BClient>();
        private readonly Dictionary<int, ClientSite> _sitesById = new Dictionary<int, ClientSite>();

        private Label _statusLabel;
        private TextBox _dashboardSearch;
        private ComboBox _statusPeriodFilter;
        private ComboBox _tableStatusFilter;
        private StatusBarChart _statusChart;
        private TypeDonutChart _typeChart;
        private FlowLayoutPanel _statsFlow;
        private TableLayoutPanel _recentTable;
        private FlowLayoutPanel _paginationFlow;
        private int _tablePage = 1;
        private const int PageSize = 5;

        private FlowLayoutPanel _contractListFlow;
        private TextBox _sidebarSearch;
        private string _sidebarFilter = "All Contracts";

        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private ComboBox _cmbType;
        private ComboBox _cmbStatus;
        private ComboBox _cmbFrequency;
        private DateTimePicker _dtpStart;
        private DateTimePicker _dtpEnd;
        private NumericUpDown _numMonthly;
        private NumericUpDown _numAnnual;
        private NumericUpDown _numSLAResponse;
        private NumericUpDown _numSLAUptime;
        private NumericUpDown _numSLARepair;
        private TextBox _txtNotes;
        private Label _summaryClient;
        private Label _summarySite;
        private Label _summaryType;
        private Label _summaryStatus;
        private Label _summaryStart;
        private Label _summaryEnd;
        private Label _summaryDuration;
        private Label _contractCountLabel;

        private AMCContract _current;
        private int? _siteFilterSiteId;
        private static int? PendingSiteNavigationSiteId;

        private static readonly Color PageBg = Color.FromArgb(246, 248, 252);
        private static readonly Color CardBg = Color.White;
        private static readonly Color Ink = Color.FromArgb(15, 23, 42);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);
        private static readonly Color Border = Color.FromArgb(226, 232, 240);
        private static readonly Color Blue = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(16, 185, 129);
        private static readonly Color Amber = Color.FromArgb(245, 158, 11);
        private static readonly Color Red = Color.FromArgb(239, 68, 68);
        private static readonly Color Purple = Color.FromArgb(124, 92, 255);

        private static readonly string[] DashboardStatuses = { "Draft", "Pending Approval", "Active", "Expired" };
        private static readonly string[] ContractTypes = { "Service Agreement", "NDA", "Vendor Agreement", "Employment Contract", "Others" };
        private static readonly string[] ContractStatuses = { "Active", "Draft", "Pending Approval", "Expiring Soon", "Expired", "On Hold", "Cancelled" };

        public ContractManagementForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildDashboardLayout();
            RecordDeletionUi.BindDeleteShortcut(this, () => DeleteCurrentContractAsync(), () => _current != null && _current.ContractID > 0);
            EnableDeferredLoad(
                () =>
                {
                    LoadReferenceData();
                    EnsureSeedContracts();
                    LoadReferenceData();
                    RefreshDashboard();
                },
                ex => SetStatus("Load error: " + ex.Message, Red));
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
            BuildDashboardLayout();
            RefreshDashboard();
        }

        private void BuildDashboardLayout()
        {
            Controls.Clear();
            BackColor = PageBg;

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = PageBg,
                Padding = new Padding(24, 20, 24, 20)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildDashboardHeader(), 0, 0);

            _statsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = PageBg,
                Padding = new Padding(0, 10, 0, 12)
            };
            root.Controls.Add(_statsFlow, 0, 1);

            TableLayoutPanel content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = PageBg,
                Margin = new Padding(0)
            };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 248));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            TableLayoutPanel charts = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = PageBg,
                Margin = new Padding(0, 0, 0, 16)
            };
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            charts.Controls.Add(BuildStatusChartCard(), 0, 0);
            charts.Controls.Add(BuildTypeChartCard(), 1, 0);

            content.Controls.Add(charts, 0, 0);
            content.Controls.Add(BuildRecentContractsCard(), 0, 1);
            root.Controls.Add(content, 0, 2);
            Controls.Add(root);
        }

        private Control BuildDashboardHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            header.Controls.Add(new Label
            {
                Text = "Contract Management",
                Location = new Point(0, 0),
                Size = new Size(420, 34),
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Ink
            });
            header.Controls.Add(new Label
            {
                Text = "Overview of all contracts",
                Location = new Point(1, 38),
                Size = new Size(420, 22),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Muted
            });

            Button newButton = MakeButton("+  New Contract", Blue, 150);
            newButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            newButton.Location = new Point(Math.Max(0, header.Width - 150), 6);
            newButton.Click += (s, e) => ShowNewContractPage(null);

            Label bell = new Label
            {
                Text = "!",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(0, header.Width - 210), 10),
                Size = new Size(32, 32),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Red,
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(bell, 16);

            _dashboardSearch = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(0, header.Width - 520), 7),
                Size = new Size(280, 32),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Search contracts...",
                ForeColor = Muted
            };
            AddPlaceholder(_dashboardSearch, "Search contracts...");
            _dashboardSearch.TextChanged += (s, e) => { _tablePage = 1; RefreshDashboardTablesOnly(); };

            _statusLabel = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(0, header.Width - 685), 12),
                Size = new Size(150, 24),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleRight
            };

            header.Controls.Add(_statusLabel);
            header.Controls.Add(_dashboardSearch);
            header.Controls.Add(bell);
            header.Controls.Add(newButton);
            header.Resize += (s, e) =>
            {
                newButton.Location = new Point(header.ClientSize.Width - 150, 6);
                bell.Location = new Point(header.ClientSize.Width - 208, 10);
                _dashboardSearch.Location = new Point(header.ClientSize.Width - 520, 7);
                _statusLabel.Location = new Point(header.ClientSize.Width - 685, 12);
            };
            return header;
        }

        private Control BuildStatusChartCard()
        {
            Panel card = MakeCard(new Padding(18, 14, 18, 18));
            card.Margin = new Padding(0, 0, 8, 0);
            card.Controls.Add(new Label
            {
                Text = "Contracts by Status",
                Location = new Point(18, 14),
                Size = new Size(240, 26),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Ink
            });
            _statusPeriodFilter = new ComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(140, 32)
            };
            _statusPeriodFilter.Items.AddRange(new object[] { "This Month", "This Quarter", "All Time" });
            _statusPeriodFilter.SelectedIndex = 2;
            _statusPeriodFilter.SelectedIndexChanged += (s, e) => RefreshDashboard();
            card.Controls.Add(_statusPeriodFilter);
            _statusChart = new StatusBarChart { Location = new Point(18, 54), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            card.Controls.Add(_statusChart);
            card.Resize += (s, e) =>
            {
                _statusPeriodFilter.Location = new Point(card.ClientSize.Width - 160, 12);
                _statusChart.Size = new Size(card.ClientSize.Width - 36, card.ClientSize.Height - 70);
            };
            return card;
        }

        private Control BuildTypeChartCard()
        {
            Panel card = MakeCard(new Padding(18, 14, 18, 18));
            card.Margin = new Padding(8, 0, 0, 0);
            card.Controls.Add(new Label
            {
                Text = "Contracts by Type",
                Location = new Point(18, 14),
                Size = new Size(240, 26),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Ink
            });
            _typeChart = new TypeDonutChart { Location = new Point(18, 52), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            card.Controls.Add(_typeChart);
            card.Resize += (s, e) => _typeChart.Size = new Size(card.ClientSize.Width - 36, card.ClientSize.Height - 68);
            return card;
        }

        private Control BuildRecentContractsCard()
        {
            Panel card = MakeCard(new Padding(18, 14, 18, 14));
            card.Controls.Add(new Label
            {
                Text = "Recent Contracts",
                Location = new Point(18, 14),
                Size = new Size(250, 26),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Ink
            });

            _tableStatusFilter = new ComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(136, 32)
            };
            _tableStatusFilter.Items.Add("All Status");
            foreach (string status in ContractStatuses) _tableStatusFilter.Items.Add(status);
            _tableStatusFilter.SelectedIndex = 0;
            _tableStatusFilter.SelectedIndexChanged += (s, e) => { _tablePage = 1; RefreshDashboardTablesOnly(); };
            card.Controls.Add(_tableStatusFilter);

            Button export = MakeButton("Export", Color.White, 92);
            export.ForeColor = Ink;
            export.FlatAppearance.BorderColor = Border;
            export.FlatAppearance.BorderSize = 1;
            export.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            export.Click += (s, e) => ExportVisibleContracts();
            card.Controls.Add(export);

            _recentTable = new TableLayoutPanel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(18, 58),
                ColumnCount = 8,
                RowCount = 6,
                BackColor = CardBg,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 11));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8));
            _recentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 4));
            card.Controls.Add(_recentTable);

            _paginationFlow = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Bottom,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Height = 38,
                BackColor = Color.Transparent
            };
            card.Controls.Add(_paginationFlow);

            card.Resize += (s, e) =>
            {
                _tableStatusFilter.Location = new Point(card.ClientSize.Width - 250, 12);
                export.Location = new Point(card.ClientSize.Width - 104, 12);
                _recentTable.Size = new Size(card.ClientSize.Width - 36, Math.Max(205, card.ClientSize.Height - 112));
                _paginationFlow.Width = 260;
                _paginationFlow.Location = new Point((card.ClientSize.Width - _paginationFlow.Width) / 2, card.ClientSize.Height - 44);
            };
            return card;
        }

        private void ShowNewContractPage(AMCContract existing)
        {
            _current = existing;
            if (existing == null)
                _sidebarFilter = "All Contracts";
            Controls.Clear();
            BackColor = PageBg;

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = PageBg,
                Padding = new Padding(22, 18, 22, 18)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildFormTopBar(), 0, 0);

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = PageBg
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            body.Controls.Add(BuildFormLeftPanel(), 0, 0);
            body.Controls.Add(BuildContractFormPanel(), 1, 0);
            body.Controls.Add(BuildSummaryPanel(), 2, 0);
            root.Controls.Add(body, 0, 1);
            Controls.Add(root);

            LoadReferenceData();
            LoadClientDropdown();
            if (existing != null)
                PopulateForm(existing);
            else
                ClearFormValues();
            RefreshSidebarList();
            UpdateLiveSummary();
        }

        private Control BuildFormTopBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            Label crumb = new Label
            {
                Text = "Contracts  >  New Contract",
                Location = new Point(150, 14),
                Size = new Size(240, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Ink
            };
            Button back = MakeButton("< Back to Dashboard", Color.White, 140);
            back.Location = new Point(0, 6);
            back.ForeColor = Ink;
            back.FlatAppearance.BorderColor = Border;
            back.FlatAppearance.BorderSize = 1;
            back.Click += (s, e) => BuildDashboardLayout();
            bar.Controls.Add(back);
            bar.Controls.Add(crumb);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Size = new Size(1032, 44),
                BackColor = Color.Transparent
            };
            Button newContract = MakeButton("+  New Contract", Blue, 130);
            Button save = MakeButton("Save", Green, 82);
            Button forms = MakeButton("Forms", Color.White, 82);
            Button whatsapp = MakeButton("WhatsApp", Green, 104);
            Button invoice = MakeButton("Create Invoice", Color.FromArgb(67, 56, 202), 126);
            Button review = MakeButton("Review", Blue, 94);
            Button sla = MakeButton("SLA Log", Color.FromArgb(88, 28, 135), 92);
            Button refresh = MakeButton("Refresh", Color.White, 92);
            Button delete = MakeButton("Delete", Red, 82);
            foreach (Button white in new[] { forms, refresh })
            {
                white.ForeColor = Ink;
                white.FlatAppearance.BorderColor = Border;
                white.FlatAppearance.BorderSize = 1;
            }
            newContract.Click += (s, e) => ClearFormValues();
            save.Click += BtnSave_Click;
            forms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Contracts / AMC", "Contracts", "HVAC", "AMC visit report preventive maintenance schedule warranty contract SLA renewal customer sign-off");
            whatsapp.Click += (s, e) => ShowContractWhatsAppAction();
            invoice.Click += BtnGenerateInvoice_Click;
            review.Click += BtnRenew_Click;
            sla.Click += BtnSLALog_Click;
            refresh.Click += (s, e) => { LoadReferenceData(); RefreshSidebarList(); UpdateContractCount(); };
            delete.Click += async (s, e) => await DeleteCurrentContractAsync();
            actions.Controls.AddRange(new Control[] { newContract, save, forms, whatsapp, invoice, review, sla, refresh, delete });
            bar.Controls.Add(actions);

            _contractCountLabel = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(120, 36),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleRight
            };
            bar.Controls.Add(_contractCountLabel);

            bar.Resize += (s, e) =>
            {
                _contractCountLabel.Location = new Point(bar.ClientSize.Width - 120, 6);
                actions.Location = new Point(Math.Max(280, _contractCountLabel.Left - actions.Width - 10), 6);
            };
            return bar;
        }

        private Control BuildFormLeftPanel()
        {
            Panel card = MakeCard(new Padding(14));
            card.Margin = new Padding(0, 0, 12, 0);
            card.Controls.Add(new Label
            {
                Text = "CONTRACTS",
                Location = new Point(16, 16),
                Size = new Size(190, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Blue
            });
            _sidebarSearch = new TextBox
            {
                Location = new Point(16, 50),
                Size = new Size(176, 30),
                Font = new Font("Segoe UI", 9f),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Search contracts...",
                ForeColor = Muted
            };
            AddPlaceholder(_sidebarSearch, "Search contracts...");
            _sidebarSearch.TextChanged += (s, e) => RefreshSidebarList();
            card.Controls.Add(_sidebarSearch);
            Button filter = MakeButton("", Color.White, 34);
            filter.Location = new Point(200, 49);
            filter.Image = ModernIconSystem.IconBitmap(ModernIconKind.Filter, 16, Blue);
            filter.FlatAppearance.BorderColor = Border;
            filter.FlatAppearance.BorderSize = 1;
            card.Controls.Add(filter);

            string[] tabs = { "All Contracts", "Active", "Expiring Soon", "Expired", "On Hold", "Cancelled" };
            int y = 94;
            foreach (string tab in tabs)
            {
                Button tabButton = new Button
                {
                    Text = tab,
                    Tag = tab,
                    Location = new Point(14, y),
                    Size = new Size(220, 28),
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    BackColor = tab == _sidebarFilter ? Color.FromArgb(239, 246, 255) : Color.White,
                    ForeColor = tab == _sidebarFilter ? Blue : Ink
                };
                tabButton.FlatAppearance.BorderSize = 0;
                tabButton.UseVisualStyleBackColor = false;
                tabButton.Click += (s, e) =>
                {
                    _sidebarFilter = (string)((Button)s).Tag;
                    ShowNewContractPage(_current);
                };
                card.Controls.Add(tabButton);
                y += 30;
            }

            Panel divider = new Panel { Location = new Point(0, y + 8), Size = new Size(260, 1), BackColor = Border, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            card.Controls.Add(divider);

            _contractListFlow = new FlowLayoutPanel
            {
                Location = new Point(8, y + 22),
                Size = new Size(238, 430),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.White
            };
            card.Controls.Add(_contractListFlow);
            card.Resize += (s, e) => _contractListFlow.Size = new Size(card.ClientSize.Width - 16, card.ClientSize.Height - _contractListFlow.Top - 12);
            return card;
        }

        private Control BuildContractFormPanel()
        {
            Panel scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg, Padding = new Padding(8, 0, 8, 0) };
            FlowLayoutPanel stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = PageBg
            };
            stack.Controls.Add(BuildDetailsSection());
            stack.Controls.Add(BuildValueSection());
            stack.Controls.Add(BuildSlaSection());
            scroller.Controls.Add(stack);
            scroller.Resize += (s, e) =>
            {
                int width = Math.Max(720, scroller.ClientSize.Width - 24);
                foreach (Control child in stack.Controls) child.Width = width;
                LayoutFormSections(width);
            };
            return scroller;
        }

        private GroupBox BuildDetailsSection()
        {
            GroupBox group = MakeGroup("CONTRACT DETAILS", 328);
            AddLabel(group, "Client *", 18, 42);
            _cmbClient = MakeCombo(group, 18, 62, 320);
            _cmbClient.SelectedIndexChanged += (s, e) =>
            {
                LoadSiteDropdown(((ComboItem)_cmbClient.SelectedItem)?.Id ?? 0);
                UpdateLiveSummary();
            };

            AddLabel(group, "Site *", 360, 42);
            _cmbSite = MakeCombo(group, 360, 62, 320);
            _cmbSite.SelectedIndexChanged += (s, e) => UpdateLiveSummary();

            AddLabel(group, "Contract Type *", 18, 112);
            _cmbType = MakeCombo(group, 18, 132, 320);
            _cmbType.Items.AddRange(new object[] { "AMC", "Service Agreement", "NDA", "Vendor Agreement", "Employment Contract" });
            _cmbType.SelectedIndexChanged += (s, e) => UpdateLiveSummary();

            AddLabel(group, "Status *", 360, 112);
            _cmbStatus = MakeCombo(group, 360, 132, 320);
            _cmbStatus.Items.AddRange(ContractStatuses.Cast<object>().ToArray());
            _cmbStatus.SelectedIndexChanged += (s, e) => UpdateLiveSummary();

            AddLabel(group, "Start Date *", 18, 184);
            _dtpStart = new DateTimePicker { Location = new Point(18, 204), Size = new Size(320, 30), Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9f) };
            _dtpStart.ValueChanged += (s, e) => UpdateLiveSummary();
            group.Controls.Add(_dtpStart);

            AddLabel(group, "End Date *", 360, 184);
            _dtpEnd = new DateTimePicker { Location = new Point(360, 204), Size = new Size(320, 30), Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9f) };
            _dtpEnd.ValueChanged += (s, e) => UpdateLiveSummary();
            group.Controls.Add(_dtpEnd);

            AddLabel(group, "Notes", 18, 248);
            _txtNotes = new TextBox { Location = new Point(18, 268), Size = new Size(662, 42), Multiline = true, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f) };
            group.Controls.Add(_txtNotes);
            return group;
        }

        private GroupBox BuildValueSection()
        {
            GroupBox group = MakeGroup("CONTRACT VALUE", 126);
            AddLabel(group, "Monthly Value (INR)", 18, 42);
            _numMonthly = NumBox(group, 18, 64, 320, 999999999);
            _numMonthly.DecimalPlaces = 2;
            _numMonthly.ThousandsSeparator = true;
            _numMonthly.ValueChanged += (s, e) =>
            {
                decimal annual = Math.Min(_numMonthly.Maximum, _numMonthly.Value * 12);
                if (_numAnnual.Value != annual) _numAnnual.Value = annual;
            };
            AddLabel(group, "Annual Value (INR)", 360, 42);
            _numAnnual = NumBox(group, 360, 64, 320, 999999999);
            _numAnnual.DecimalPlaces = 2;
            _numAnnual.ThousandsSeparator = true;
            return group;
        }

        private GroupBox BuildSlaSection()
        {
            GroupBox group = MakeGroup("SLA PARAMETERS", 176);
            AddLabel(group, "Response Time (Hours)", 18, 42);
            _numSLAResponse = NumBox(group, 18, 64, 200, 240);
            _numSLAResponse.Value = 8;
            AddLabel(group, "Uptime % Target", 246, 42);
            _numSLAUptime = NumBox(group, 246, 64, 200, 100);
            _numSLAUptime.DecimalPlaces = 1;
            _numSLAUptime.Value = 99;
            AddLabel(group, "Repair Time (Hours)", 474, 42);
            _numSLARepair = NumBox(group, 474, 64, 200, 240);
            _numSLARepair.Value = 24;
            AddLabel(group, "Maintenance Frequency", 18, 108);
            _cmbFrequency = MakeCombo(group, 18, 128, 320);
            _cmbFrequency.Items.AddRange(new object[] { "Monthly", "Weekly", "Quarterly", "Annually" });
            return group;
        }

        private Control BuildSummaryPanel()
        {
            Panel rail = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(12, 0, 0, 0) };
            Panel summary = MakeCard(new Padding(16));
            summary.Dock = DockStyle.Top;
            summary.Height = 300;
            summary.Controls.Add(new Label { Text = "CONTRACT SUMMARY", Location = new Point(18, 18), Size = new Size(200, 24), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Blue });
            _summaryClient = AddSummaryLine(summary, "Client", 62);
            _summarySite = AddSummaryLine(summary, "Site", 96);
            _summaryType = AddSummaryLine(summary, "Contract Type", 130);
            _summaryStatus = AddSummaryLine(summary, "Status", 164);
            _summaryStart = AddSummaryLine(summary, "Start Date", 198);
            _summaryEnd = AddSummaryLine(summary, "End Date", 232);
            _summaryDuration = AddSummaryLine(summary, "Duration", 266);

            Panel actions = MakeCard(new Padding(16));
            actions.Dock = DockStyle.Top;
            actions.Height = 112;
            actions.Margin = new Padding(0, 16, 0, 0);
            actions.Controls.Add(new Label { Text = "ACTIONS", Location = new Point(18, 18), Size = new Size(160, 24), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Blue });
            actions.Controls.Add(new Label
            {
                Text = "You can save the contract\r\nor create invoice once saved.",
                Location = new Point(20, 50),
                Size = new Size(205, 44),
                Font = new Font("Segoe UI", 9f),
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleCenter
            });
            rail.Controls.Add(actions);
            rail.Controls.Add(summary);
            return rail;
        }

        private void RefreshDashboard()
        {
            List<AMCContract> contracts = GetContracts();
            if (_siteFilterSiteId.HasValue)
                contracts = contracts.Where(c => c.SiteID == _siteFilterSiteId.Value).ToList();

            RefreshStats(contracts);
            RefreshCharts(contracts);
            RefreshDashboardTablesOnly();
            SetStatus(contracts.Count + " contracts.", Muted);
        }

        private void RefreshStats(List<AMCContract> contracts)
        {
            _statsFlow.Controls.Clear();
            int total = contracts.Count;
            int active = contracts.Count(c => IsStatus(c, "Active"));
            int expiring = contracts.Count(IsExpiringSoon);
            int expired = contracts.Count(c => GetDisplayStatus(c) == "Expired");
            string pct = total == 0 ? "0% of total" : Math.Round(active * 100m / total) + "% of total";
            _statsFlow.Controls.Add(MakeStatCard("Total Contracts", total.ToString(CultureInfo.InvariantCulture), "All time", Blue, ModernIconKind.Contract));
            _statsFlow.Controls.Add(MakeStatCard("Active Contracts", active.ToString(CultureInfo.InvariantCulture), pct, Green, ModernIconKind.Status));
            _statsFlow.Controls.Add(MakeStatCard("Expiring Soon", expiring.ToString(CultureInfo.InvariantCulture), "Next 30 days", Amber, ModernIconKind.Alert));
            _statsFlow.Controls.Add(MakeStatCard("Expired Contracts", expired.ToString(CultureInfo.InvariantCulture), "Action required", Red, ModernIconKind.Calendar));
            _statsFlow.Resize += (s, e) =>
            {
                int width = Math.Max(220, (_statsFlow.ClientSize.Width - 72) / 4);
                foreach (Control card in _statsFlow.Controls) card.Width = width;
            };
            int cardWidth = Math.Max(220, (_statsFlow.ClientSize.Width - 72) / 4);
            foreach (Control card in _statsFlow.Controls) card.Width = cardWidth;
        }

        private void RefreshCharts(List<AMCContract> contracts)
        {
            IEnumerable<AMCContract> filtered = contracts;
            string period = _statusPeriodFilter == null ? "All Time" : Convert.ToString(_statusPeriodFilter.SelectedItem);
            DateTime today = DateTime.Today;
            if (period == "This Month")
                filtered = filtered.Where(c => c.StartDate.Year == today.Year && c.StartDate.Month == today.Month);
            else if (period == "This Quarter")
            {
                int currentQuarter = ((today.Month - 1) / 3) + 1;
                filtered = filtered.Where(c => c.StartDate.Year == today.Year && ((c.StartDate.Month - 1) / 3) + 1 == currentQuarter);
            }

            Dictionary<string, int> statusCounts = DashboardStatuses.ToDictionary(s => s, s => 0);
            foreach (AMCContract contract in filtered)
            {
                string status = GetDisplayStatus(contract);
                if (statusCounts.ContainsKey(status)) statusCounts[status]++;
            }
            _statusChart.SetData(statusCounts);

            Dictionary<string, int> typeCounts = ContractTypes.ToDictionary(t => t, t => 0);
            foreach (AMCContract contract in contracts)
            {
                string type = NormalizeContractType(contract.ContractType);
                typeCounts[type]++;
            }
            _typeChart.SetData(typeCounts);
        }

        private void RefreshDashboardTablesOnly()
        {
            if (_recentTable == null) return;
            List<AMCContract> contracts = GetFilteredDashboardContracts();
            int totalPages = Math.Max(1, (int)Math.Ceiling(contracts.Count / (double)PageSize));
            if (_tablePage > totalPages) _tablePage = totalPages;
            List<AMCContract> page = contracts.Skip((_tablePage - 1) * PageSize).Take(PageSize).ToList();
            RenderRecentTable(page);
            RenderPagination(totalPages);
        }

        private List<AMCContract> GetFilteredDashboardContracts()
        {
            IEnumerable<AMCContract> contracts = GetContracts();
            if (_siteFilterSiteId.HasValue)
                contracts = contracts.Where(c => c.SiteID == _siteFilterSiteId.Value);
            string query = GetBoxText(_dashboardSearch, "Search contracts...");
            if (!string.IsNullOrWhiteSpace(query))
            {
                contracts = contracts.Where(c =>
                    ContractName(c).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    GetClientName(c.ClientID).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    NormalizeContractType(c.ContractType).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            string status = _tableStatusFilter == null ? "All Status" : Convert.ToString(_tableStatusFilter.SelectedItem);
            if (!string.IsNullOrWhiteSpace(status) && status != "All Status")
                contracts = contracts.Where(c => GetDisplayStatus(c) == status || IsStatus(c, status));
            return contracts.OrderByDescending(c => c.ContractID).ToList();
        }

        private void RenderRecentTable(List<AMCContract> page)
        {
            _recentTable.SuspendLayout();
            _recentTable.Controls.Clear();
            _recentTable.RowStyles.Clear();
            _recentTable.RowCount = 6;
            _recentTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            for (int i = 0; i < 5; i++) _recentTable.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            string[] headers = { "Contract Name", "Party", "Type", "Status", "Start Date", "End Date", "Value", "Actions" };
            for (int c = 0; c < headers.Length; c++)
                _recentTable.Controls.Add(MakeCell(headers[c], true, ContentAlignment.MiddleLeft), c, 0);

            for (int row = 0; row < 5; row++)
            {
                if (row >= page.Count)
                {
                    for (int c = 0; c < headers.Length; c++) _recentTable.Controls.Add(MakeCell("", false, ContentAlignment.MiddleLeft), c, row + 1);
                    continue;
                }

                AMCContract contract = page[row];
                _recentTable.Controls.Add(MakeCell(ContractName(contract), false, ContentAlignment.MiddleLeft), 0, row + 1);
                _recentTable.Controls.Add(MakeCell(GetClientName(contract.ClientID), false, ContentAlignment.MiddleLeft), 1, row + 1);
                _recentTable.Controls.Add(MakeCell(NormalizeContractType(contract.ContractType), false, ContentAlignment.MiddleLeft), 2, row + 1);
                _recentTable.Controls.Add(MakeStatusPill(GetDisplayStatus(contract)), 3, row + 1);
                _recentTable.Controls.Add(MakeCell(FormatDate(contract.StartDate), false, ContentAlignment.MiddleLeft), 4, row + 1);
                _recentTable.Controls.Add(MakeCell(FormatDate(contract.EndDate), false, ContentAlignment.MiddleLeft), 5, row + 1);
                _recentTable.Controls.Add(MakeCell(FormatCurrency(contract.AnnualValue > 0 ? contract.AnnualValue : contract.MonthlyValue * 12), false, ContentAlignment.MiddleLeft), 6, row + 1);
                _recentTable.Controls.Add(MakeActionsButton(contract), 7, row + 1);
            }
            _recentTable.ResumeLayout(true);
        }

        private void RenderPagination(int totalPages)
        {
            _paginationFlow.Controls.Clear();
            AddPageButton("<", Math.Max(1, _tablePage - 1), _tablePage > 1);
            for (int page = 1; page <= Math.Min(totalPages, 3); page++)
                AddPageButton(page.ToString(CultureInfo.InvariantCulture), page, true, page == _tablePage);
            if (totalPages > 4)
                _paginationFlow.Controls.Add(new Label { Text = "...", Width = 28, Height = 30, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Muted });
            if (totalPages > 3)
                AddPageButton(totalPages.ToString(CultureInfo.InvariantCulture), totalPages, true, totalPages == _tablePage);
            AddPageButton(">", Math.Min(totalPages, _tablePage + 1), _tablePage < totalPages);
        }

        private void AddPageButton(string text, int page, bool enabled, bool selected = false)
        {
            Button b = new Button
            {
                Text = text,
                Width = 32,
                Height = 30,
                Enabled = enabled,
                BackColor = selected ? Blue : Color.White,
                ForeColor = selected ? Color.White : Ink,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(3)
            };
            b.FlatAppearance.BorderColor = Border;
            b.Click += (s, e) => { _tablePage = page; RefreshDashboardTablesOnly(); };
            _paginationFlow.Controls.Add(b);
        }

        private void LoadReferenceData()
        {
            _clientsById.Clear();
            _sitesById.Clear();
            try
            {
                foreach (B2BClient client in _clientSvc.GetAllClients() ?? new List<B2BClient>())
                    _clientsById[client.ClientID] = client;
            }
            catch (Exception ex) { AppLogger.LogError("ContractManagementForm.LoadClients", ex); }

            try
            {
                foreach (ClientSite site in _siteSvc.GetAll() ?? new List<ClientSite>())
                    _sitesById[site.SiteID] = site;
            }
            catch (Exception ex) { AppLogger.LogError("ContractManagementForm.LoadSites", ex); }
        }

        private void LoadClientDropdown()
        {
            if (_cmbClient == null) return;
            _cmbClient.Items.Clear();
            _cmbClient.Items.Add(new ComboItem { Id = 0, Text = "-- Select Client --" });
            foreach (B2BClient client in _clientsById.Values.OrderBy(c => c.CompanyName))
                _cmbClient.Items.Add(new ComboItem { Id = client.ClientID, Text = client.CompanyName });
            _cmbClient.SelectedIndex = 0;
            LoadSiteDropdown(0);
        }

        private void LoadSiteDropdown(int clientId)
        {
            if (_cmbSite == null) return;
            int previous = ((_cmbSite.SelectedItem as ComboItem)?.Id).GetValueOrDefault();
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem { Id = 0, Text = "-- Select Site --" });
            foreach (ClientSite site in _sitesById.Values.Where(s => s.ClientID == clientId).OrderBy(SiteService.GetDisplayName))
                _cmbSite.Items.Add(new ComboItem { Id = site.SiteID, Text = SiteService.GetDisplayName(site) });
            _cmbSite.SelectedIndex = 0;
            if (previous > 0) SelectCombo(_cmbSite, previous);
        }

        private void RefreshSidebarList()
        {
            if (_contractListFlow == null) return;
            _contractListFlow.SuspendLayout();
            _contractListFlow.Controls.Clear();
            IEnumerable<AMCContract> contracts = GetContracts();
            string query = GetBoxText(_sidebarSearch, "Search contracts...");
            if (!string.IsNullOrWhiteSpace(query))
            {
                contracts = contracts.Where(c => ContractName(c).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 GetClientName(c.ClientID).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (_sidebarFilter != "All Contracts")
                contracts = contracts.Where(c => GetDisplayStatus(c) == _sidebarFilter || IsStatus(c, _sidebarFilter));

            List<AMCContract> list = contracts.OrderByDescending(c => c.ContractID).Take(30).ToList();
            if (list.Count == 0)
            {
                _contractListFlow.Controls.Add(BuildContractEmptyState());
            }
            else
            {
                foreach (AMCContract contract in list)
                    _contractListFlow.Controls.Add(MakeSidebarContractCard(contract));
            }
            _contractListFlow.ResumeLayout(true);
            UpdateContractCount();
        }

        private void UpdateContractCount()
        {
            if (_contractCountLabel != null)
                _contractCountLabel.Text = GetContracts().Count + " contracts.";
        }

        private void EnsureSeedContracts()
        {
            List<AMCContract> contracts = GetContracts();
            if (contracts.Count >= 8 || _clientsById.Count == 0)
                return;

            if (_sitesById.Count == 0)
            {
                try
                {
                    SiteRepository siteRepo = new SiteRepository();
                    foreach (B2BClient client in _clientsById.Values.OrderBy(c => c.CompanyName).Take(8))
                    {
                        siteRepo.Create(new ClientSite
                        {
                            ClientID = client.ClientID,
                            SiteName = client.CompanyName + " Main Site",
                            Address = client.BillingAddress,
                            City = client.City,
                            ACSystemCount = 4,
                            RefrigerationSystemCount = 1,
                            CoolingTowerCount = 1,
                            IsCritical = true
                        });
                    }
                    AppDataCache.RemovePrefix("sites:");
                    LoadReferenceData();
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("ContractManagementForm.EnsureSeedSites", ex);
                }
            }

            if (_sitesById.Count == 0)
                return;

            List<ClientSite> sites = _sitesById.Values
                .Where(s => _clientsById.ContainsKey(s.ClientID))
                .GroupBy(s => s.ClientID)
                .Select(g => g.First())
                .Take(8)
                .ToList();
            if (sites.Count == 0) return;

            string[] types = { "Service Agreement", "NDA", "Vendor Agreement", "Employment Contract", "Others", "Service Agreement", "Vendor Agreement", "NDA" };
            string[] statuses = { "Active", "Draft", "Pending Approval", "Expired", "On Hold", "Cancelled", "Expiring Soon", "Active" };
            decimal[] monthly = { 125000, 0, 87500, 50000, 64000, 45000, 92000, 30000 };
            DateTime today = DateTime.Today;

            try
            {
                ContractRepository repo = new ContractRepository();
                for (int i = contracts.Count; i < 8; i++)
                {
                    ClientSite site = sites[i % sites.Count];
                    DateTime start = today.AddMonths(-Math.Max(1, i + 1));
                    DateTime end = statuses[i] == "Expired" ? today.AddDays(-12) : statuses[i] == "Expiring Soon" ? today.AddDays(18) : start.AddYears(1);
                    repo.Create(new AMCContract
                    {
                        ClientID = site.ClientID,
                        SiteID = site.SiteID,
                        ContractType = types[i],
                        ContractStatus = statuses[i],
                        StartDate = start,
                        EndDate = end,
                        MonthlyValue = monthly[i],
                        AnnualValue = monthly[i] * 12,
                        SLAResponseTimeHours = i % 2 == 0 ? 8 : 4,
                        SLAUptimePercent = 99.0m,
                        SLARepairTimeHours = i % 2 == 0 ? 24 : 12,
                        MaintenanceFrequency = i % 3 == 0 ? "Quarterly" : "Monthly",
                        Notes = "Seed contract for dashboard analytics."
                    });
                }
                AppDataCache.RemovePrefix("contracts:");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ContractManagementForm.EnsureSeedContracts", ex);
            }
        }

        private List<AMCContract> GetContracts()
        {
            try { return _contractSvc.GetAllContracts() ?? new List<AMCContract>(); }
            catch (Exception ex)
            {
                AppLogger.LogError("ContractManagementForm.GetContracts", ex);
                return new List<AMCContract>();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                AMCContract contract = CollectForm();
                ValidateForm(contract);
                if (_current == null || _current.ContractID == 0)
                {
                    int id = _contractSvc.CreateContract(contract);
                    contract.ContractID = id;
                    _current = contract;
                    MessageBox.Show("Contract saved successfully.", "Contract Management", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    contract.ContractID = _current.ContractID;
                    _contractSvc.UpdateContract(contract);
                    MessageBox.Show("Contract updated successfully.", "Contract Management", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                BuildDashboardLayout();
                LoadReferenceData();
                RefreshDashboard();
                SetStatus("Contract saved successfully.", Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Contract validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async System.Threading.Tasks.Task DeleteCurrentContractAsync()
        {
            if (_current == null || _current.ContractID <= 0)
            {
                SetStatus("Select a saved contract to delete.", Red);
                return;
            }

            await DeleteContractAsync(_current);
        }

        private async System.Threading.Tasks.Task DeleteContractAsync(AMCContract contract)
        {
            if (contract == null || contract.ContractID <= 0)
                return;

            string label = "contract CON-" + contract.ContractID.ToString("0000", CultureInfo.InvariantCulture);
            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Contract",
                label,
                "Linked invoices, jobs, and purchase orders will be kept but unlinked. SLA log entries for this contract will be removed.");
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SetStatus("Deleting contract...", Red);
                await System.Threading.Tasks.Task.Run(() => _contractSvc.DeleteContract(contract.ContractID));
                _current = null;
                LoadReferenceData();
                BuildDashboardLayout();
                RefreshDashboard();
                SetStatus("Contract deleted.", Red);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ContractManagementForm.DeleteContractAsync", ex);
                MessageBox.Show(ex.Message, "Delete Contract", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("Delete failed: " + ex.Message, Red);
            }
        }

        private void ValidateForm(AMCContract contract)
        {
            if (contract.ClientID <= 0) throw new Exception("Please select Client.");
            if (contract.SiteID <= 0) throw new Exception("Please select Site.");
            if (string.IsNullOrWhiteSpace(contract.ContractType)) throw new Exception("Please select Contract Type.");
            if (string.IsNullOrWhiteSpace(contract.ContractStatus)) throw new Exception("Please select Status.");
            if (contract.EndDate <= contract.StartDate) throw new Exception("End Date must be after Start Date.");
        }

        private AMCContract CollectForm()
        {
            return new AMCContract
            {
                ContractID = _current == null ? 0 : _current.ContractID,
                ClientID = ((_cmbClient.SelectedItem as ComboItem)?.Id).GetValueOrDefault(),
                SiteID = ((_cmbSite.SelectedItem as ComboItem)?.Id).GetValueOrDefault(),
                ContractType = NormalizeContractType(Convert.ToString(_cmbType.SelectedItem)),
                ContractStatus = Convert.ToString(_cmbStatus.SelectedItem) ?? "Active",
                StartDate = _dtpStart.Value.Date,
                EndDate = _dtpEnd.Value.Date,
                MonthlyValue = _numMonthly.Value,
                AnnualValue = _numAnnual.Value,
                SLAResponseTimeHours = (int)_numSLAResponse.Value,
                SLAUptimePercent = _numSLAUptime.Value,
                SLARepairTimeHours = (int)_numSLARepair.Value,
                MaintenanceFrequency = Convert.ToString(_cmbFrequency.SelectedItem) ?? "Monthly",
                Notes = _txtNotes.Text.Trim()
            };
        }

        private void PopulateForm(AMCContract contract)
        {
            if (contract == null) return;
            SelectCombo(_cmbClient, contract.ClientID);
            LoadSiteDropdown(contract.ClientID);
            SelectCombo(_cmbSite, contract.SiteID);
            SelectComboText(_cmbType, NormalizeContractType(contract.ContractType));
            SelectComboText(_cmbStatus, string.IsNullOrWhiteSpace(contract.ContractStatus) ? "Active" : contract.ContractStatus);
            SelectComboText(_cmbFrequency, NormalizeFrequency(contract.MaintenanceFrequency));
            _dtpStart.Value = contract.StartDate == default(DateTime) ? DateTime.Today : contract.StartDate;
            _dtpEnd.Value = contract.EndDate == default(DateTime) ? DateTime.Today.AddYears(1) : contract.EndDate;
            _numMonthly.Value = Clamp(contract.MonthlyValue, _numMonthly.Minimum, _numMonthly.Maximum);
            _numAnnual.Value = Clamp(contract.AnnualValue, _numAnnual.Minimum, _numAnnual.Maximum);
            _numSLAResponse.Value = Clamp(contract.SLAResponseTimeHours, _numSLAResponse.Minimum, _numSLAResponse.Maximum);
            _numSLAUptime.Value = Clamp(contract.SLAUptimePercent, _numSLAUptime.Minimum, _numSLAUptime.Maximum);
            _numSLARepair.Value = Clamp(contract.SLARepairTimeHours, _numSLARepair.Minimum, _numSLARepair.Maximum);
            _txtNotes.Text = contract.Notes ?? string.Empty;
            UpdateLiveSummary();
        }

        private void ClearFormValues()
        {
            _current = null;
            if (_cmbClient.Items.Count > 0) _cmbClient.SelectedIndex = 0;
            if (_cmbSite.Items.Count > 0) _cmbSite.SelectedIndex = 0;
            SelectComboText(_cmbType, "AMC");
            SelectComboText(_cmbStatus, "Active");
            SelectComboText(_cmbFrequency, "Monthly");
            _dtpStart.Value = DateTime.Today;
            _dtpEnd.Value = DateTime.Today.AddYears(1);
            _numMonthly.Value = 0;
            _numAnnual.Value = 0;
            _numSLAResponse.Value = 8;
            _numSLAUptime.Value = 99;
            _numSLARepair.Value = 24;
            _txtNotes.Text = string.Empty;
            UpdateLiveSummary();
        }

        private void UpdateLiveSummary()
        {
            if (_summaryClient == null) return;
            string client = (_cmbClient.SelectedItem as ComboItem)?.Text;
            string site = (_cmbSite.SelectedItem as ComboItem)?.Text;
            string status = Convert.ToString(_cmbStatus.SelectedItem) ?? "-";
            _summaryClient.Text = string.IsNullOrWhiteSpace(client) || client.StartsWith("--") ? "-" : client;
            _summarySite.Text = string.IsNullOrWhiteSpace(site) || site.StartsWith("--") ? "-" : site;
            _summaryType.Text = Convert.ToString(_cmbType.SelectedItem) ?? "-";
            _summaryStatus.Text = status;
            _summaryStatus.ForeColor = StatusColor(status);
            _summaryStart.Text = FormatDate(_dtpStart.Value);
            _summaryEnd.Text = FormatDate(_dtpEnd.Value);
            int months = Math.Max(0, ((_dtpEnd.Value.Year - _dtpStart.Value.Year) * 12) + _dtpEnd.Value.Month - _dtpStart.Value.Month);
            _summaryDuration.Text = months <= 0 ? "-" : months + " months";
        }

        private void BtnGenerateInvoice_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please save or select a contract first.", "Create Invoice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Invoice invoice = new Invoice
                {
                    ContractID = _current.ContractID,
                    ClientID = _current.ClientID,
                    SiteID = _current.SiteID,
                    InvoiceNumber = "INV-" + DateTime.Now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture),
                    InvoiceDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(30),
                    SubTotal = _current.MonthlyValue,
                    GSTPercent = 18m,
                    TaxAmount = Math.Round(_current.MonthlyValue * 0.18m, 2),
                    TotalAmount = Math.Round(_current.MonthlyValue * 1.18m, 2),
                    BalanceDue = Math.Round(_current.MonthlyValue * 1.18m, 2),
                    PaidAmount = 0,
                    PaymentStatus = "Draft",
                    LineItems = new List<InvoiceLineItem>
                    {
                        new InvoiceLineItem { Description = "Contract service - " + DateTime.Today.ToString("MMMM yyyy", CultureInfo.InvariantCulture), Quantity = 1, Rate = _current.MonthlyValue, Amount = _current.MonthlyValue }
                    }
                };
                new InvoiceService().CreateInvoiceWithLineItems(invoice);
                MessageBox.Show("Invoice created for " + GetClientName(_current.ClientID) + ".", "Invoice Generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Create Invoice", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRenew_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please select a contract first.", "Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _dtpStart.Value = _current.EndDate.AddDays(1);
            _dtpEnd.Value = _current.EndDate.AddYears(1);
            SelectComboText(_cmbStatus, "Active");
            UpdateLiveSummary();
            MessageBox.Show("Renewal dates prepared. Review and save when ready.", "Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSLALog_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please select a contract first.", "SLA Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int count = new SLAService().GetAll().Count(log => log.ContractID == _current.ContractID);
            MessageBox.Show("Contract " + _current.ContractID + ": " + count + " SLA events logged.", "SLA Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowContractWhatsAppAction()
        {
            if (_current == null || _current.ContractID == 0)
            {
                MessageBox.Show("Please save or select a contract first.", "WhatsApp", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            B2BClient client = _clientsById.ContainsKey(_current.ClientID) ? _clientsById[_current.ClientID] : null;
            string clientName = client == null ? "Customer" : client.CompanyName;
            string message = "Hi " + clientName + ",\r\n\r\nYour " + NormalizeContractType(_current.ContractType) +
                " contract expires on " + FormatDate(_current.EndDate) + ". Renewal value is " +
                FormatCurrency(_current.AnnualValue > 0 ? _current.AnnualValue : _current.MonthlyValue * 12) +
                ". Please confirm renewal or let us know if you need any changes.\r\n\r\nRegards,\r\nServoERP";
            WhatsAppQuickActionDialog.ShowFor(this, new WhatsAppQuickActionContext
            {
                Module = "Contracts",
                SourceId = _current.ContractID,
                ContactName = clientName,
                Phone = client == null ? string.Empty : client.Phone,
                TemplateType = "Contract renewal reminder",
                Message = message,
                LinkedRecordType = "Contract",
                LinkedRecord = "CON-" + _current.ContractID.ToString("0000", CultureInfo.InvariantCulture),
                LinkedRecordId = _current.ContractID
            });
        }

        private void ExportVisibleContracts()
        {
            List<AMCContract> contracts = GetFilteredDashboardContracts();
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Contract Name,Party,Type,Status,Start Date,End Date,Value");
            foreach (AMCContract c in contracts)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(ContractName(c)),
                    Csv(GetClientName(c.ClientID)),
                    Csv(NormalizeContractType(c.ContractType)),
                    Csv(GetDisplayStatus(c)),
                    Csv(FormatDate(c.StartDate)),
                    Csv(FormatDate(c.EndDate)),
                    Csv((c.AnnualValue > 0 ? c.AnnualValue : c.MonthlyValue * 12).ToString(CultureInfo.InvariantCulture))
                }));
            }
            if (UIHelper.TrySetClipboardText(this, csv.ToString(), BrandingService.WindowTitle("Contract Export")))
                SetStatus("Visible contracts copied for export.", Green);
            else
                SetStatus("Copy failed. Please try again.", Red);
        }

        private Panel MakeStatCard(string label, string number, string sub, Color accent, ModernIconKind iconKind)
        {
            Panel card = MakeCard(new Padding(18));
            card.Dock = DockStyle.None;
            card.Width = 270;
            card.Height = 104;
            card.Margin = new Padding(0, 0, 12, 0);
            Panel badge = new Panel { Location = new Point(18, 20), Size = new Size(58, 58), BackColor = Color.FromArgb(235, 243, 255) };
            DS.Rounded(badge, 29);
            Label icon = new Label { Dock = DockStyle.Fill, Image = ModernIconSystem.IconBitmap(iconKind, 30, accent), ImageAlign = ContentAlignment.MiddleCenter };
            badge.Controls.Add(icon);
            card.Controls.Add(badge);
            card.Controls.Add(new Label { Text = label, Location = new Point(92, 18), Size = new Size(160, 22), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Ink });
            card.Controls.Add(new Label { Text = number, Location = new Point(92, 42), Size = new Size(160, 30), Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = Ink });
            card.Controls.Add(new Label { Text = sub, Location = new Point(92, 74), Size = new Size(160, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = accent });
            return card;
        }

        private Panel MakeCard(Padding padding)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = CardBg, Padding = padding };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (GraphicsPath path = DS.RoundedRect(rect, 10))
                using (SolidBrush brush = new SolidBrush(CardBg))
                using (Pen pen = new Pen(Border))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            return panel;
        }

        private GroupBox MakeGroup(string title, int height)
        {
            return new ModernContractGroupBox
            {
                Text = title,
                Width = 760,
                Height = height,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(14),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Blue,
                BackColor = PageBg
            };
        }

        private Button MakeButton(string text, Color bg, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = bg == Color.White ? Color.FromArgb(248, 250, 252) : DS.Lighten(bg, 0.07f);
            button.FlatAppearance.MouseDownBackColor = bg == Color.White ? Color.FromArgb(241, 245, 249) : DS.Darken(bg, 0.08f);
            DS.Rounded(button, 7);
            return button;
        }

        private Label MakeCell(string text, bool header, ContentAlignment align)
        {
            Label label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", header ? 8.5f : 8.2f, header ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = header ? Color.FromArgb(51, 65, 85) : Ink,
                TextAlign = align,
                AutoEllipsis = true,
                Padding = new Padding(8, 0, 4, 0),
                BackColor = header ? Color.FromArgb(248, 250, 252) : Color.White
            };
            label.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(241, 245, 249)))
                    e.Graphics.DrawLine(pen, 0, label.Height - 1, label.Width, label.Height - 1);
            };
            return label;
        }

        private Control MakeStatusPill(string status)
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8, 9, 8, 9) };
            Label pill = new Label
            {
                Text = status,
                AutoSize = false,
                Height = 23,
                Width = Math.Min(120, Math.Max(66, TextRenderer.MeasureText(status, new Font("Segoe UI", 8f, FontStyle.Bold)).Width + 18)),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = StatusColor(status),
                BackColor = StatusBackColor(status),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(pill, 11);
            host.Controls.Add(pill);
            return host;
        }

        private Control MakeActionsButton(AMCContract contract)
        {
            Button button = new Button
            {
                Text = "...",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Ink,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("View", null, (s, e) => ShowNewContractPage(contract));
            menu.Items.Add("Edit", null, (s, e) => ShowNewContractPage(contract));
            RecordDeletionUi.AddDeleteMenuItem(menu, async (s, e) => await DeleteContractAsync(contract));
            button.Click += (s, e) => menu.Show(button, new Point(0, button.Height));
            return button;
        }

        private Panel MakeSidebarContractCard(AMCContract contract)
        {
            Panel card = new Panel { Width = 220, Height = 66, Margin = new Padding(0, 0, 0, 8), BackColor = Color.White, Cursor = Cursors.Hand, Tag = contract };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, 0, card.Height - 1, card.Width, card.Height - 1);
            };
            Label name = new Label { Text = ContractName(contract), Location = new Point(8, 8), Size = new Size(170, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Ink, AutoEllipsis = true };
            Label meta = new Label { Text = FormatDate(contract.EndDate), Location = new Point(8, 32), Size = new Size(126, 20), Font = new Font("Segoe UI", 8f), ForeColor = Muted };
            Label status = new Label { Text = GetDisplayStatus(contract), Location = new Point(138, 32), Size = new Size(76, 20), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = StatusColor(GetDisplayStatus(contract)), TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };
            card.Controls.Add(name);
            card.Controls.Add(meta);
            card.Controls.Add(status);
            foreach (Control control in new Control[] { card, name, meta, status })
                control.Click += (s, e) => ShowNewContractPage(contract);
            return card;
        }

        private Panel BuildContractEmptyState()
        {
            Panel empty = new Panel { Width = 220, Height = 220, BackColor = Color.White };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Contract, 54, Color.FromArgb(239, 246, 255), Blue);
            icon.Location = new Point((empty.Width - icon.Width) / 2, 34);
            Label title = new Label { Text = "No contracts found", Location = new Point(10, 102), Size = new Size(200, 24), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Ink, TextAlign = ContentAlignment.MiddleCenter };
            Label helper = new Label { Text = "Create a contract or adjust filters.", Location = new Point(20, 132), Size = new Size(180, 44), Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, TextAlign = ContentAlignment.TopCenter };
            empty.Controls.Add(icon);
            empty.Controls.Add(title);
            empty.Controls.Add(helper);
            return empty;
        }

        private void LayoutFormSections(int width)
        {
            int inner = Math.Max(680, width - 48);
            int gap = 24;
            int col = (inner - gap) / 2;
            int x1 = 18;
            int x2 = x1 + col + gap;
            MoveControl(_cmbClient, x1, 62, col, 30);
            MoveControl(_cmbSite, x2, 62, col, 30);
            MoveControl(_cmbType, x1, 132, col, 30);
            MoveControl(_cmbStatus, x2, 132, col, 30);
            MoveControl(_dtpStart, x1, 204, col, 30);
            MoveControl(_dtpEnd, x2, 204, col, 30);
            MoveControl(_txtNotes, x1, 268, inner, 42);
            MoveLabel(_cmbClient?.Parent, "Client *", x1, 42, col);
            MoveLabel(_cmbSite?.Parent, "Site *", x2, 42, col);
            MoveLabel(_cmbType?.Parent, "Contract Type *", x1, 112, col);
            MoveLabel(_cmbStatus?.Parent, "Status *", x2, 112, col);
            MoveLabel(_dtpStart?.Parent, "Start Date *", x1, 184, col);
            MoveLabel(_dtpEnd?.Parent, "End Date *", x2, 184, col);
            MoveLabel(_txtNotes?.Parent, "Notes", x1, 248, inner);

            MoveControl(_numMonthly, x1, 64, col, 30);
            MoveControl(_numAnnual, x2, 64, col, 30);
            MoveLabel(_numMonthly?.Parent, "Monthly Value (INR)", x1, 42, col);
            MoveLabel(_numAnnual?.Parent, "Annual Value (INR)", x2, 42, col);

            int third = Math.Max(180, (inner - 48) / 3);
            int sx2 = x1 + third + 24;
            int sx3 = sx2 + third + 24;
            MoveControl(_numSLAResponse, x1, 64, third, 30);
            MoveControl(_numSLAUptime, sx2, 64, third, 30);
            MoveControl(_numSLARepair, sx3, 64, third, 30);
            MoveControl(_cmbFrequency, x1, 128, col, 30);
            MoveLabel(_numSLAResponse?.Parent, "Response Time (Hours)", x1, 42, third);
            MoveLabel(_numSLAUptime?.Parent, "Uptime % Target", sx2, 42, third);
            MoveLabel(_numSLARepair?.Parent, "Repair Time (Hours)", sx3, 42, third);
            MoveLabel(_cmbFrequency?.Parent, "Maintenance Frequency", x1, 108, col);
        }

        private void AddLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(180, 18), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(51, 65, 85) });
        }

        private ComboBox MakeCombo(Control parent, int x, int y, int width)
        {
            ComboBox combo = new ComboBox { Location = new Point(x, y), Size = new Size(width, 30), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f) };
            parent.Controls.Add(combo);
            return combo;
        }

        private NumericUpDown NumBox(Control parent, int x, int y, int width, decimal max)
        {
            NumericUpDown number = new NumericUpDown { Location = new Point(x, y), Size = new Size(width, 30), Minimum = 0, Maximum = max, Font = new Font("Segoe UI", 9f) };
            parent.Controls.Add(number);
            return number;
        }

        private Label AddSummaryLine(Control parent, string label, int y)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(18, y), Size = new Size(100, 20), Font = new Font("Segoe UI", 8.5f), ForeColor = Muted });
            Label value = new Label { Text = "-", Location = new Point(112, y), Size = new Size(118, 20), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Ink, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true };
            parent.Controls.Add(value);
            return value;
        }

        private static void MoveControl(Control control, int x, int y, int width, int height)
        {
            if (control == null) return;
            control.Location = new Point(x, y);
            control.Size = new Size(width, height);
        }

        private static void MoveLabel(Control parent, string text, int x, int y, int width)
        {
            if (parent == null) return;
            foreach (Control child in parent.Controls)
            {
                Label label = child as Label;
                if (label != null && label.Text == text)
                {
                    label.Location = new Point(x, y);
                    label.Size = new Size(width, 18);
                    return;
                }
            }
        }

        private string ContractName(AMCContract contract)
        {
            string party = GetClientName(contract.ClientID);
            string type = NormalizeContractType(contract.ContractType);
            return type + " - " + party;
        }

        private string GetClientName(int clientId)
        {
            B2BClient client;
            return _clientsById.TryGetValue(clientId, out client) && !string.IsNullOrWhiteSpace(client.CompanyName)
                ? client.CompanyName
                : "Client #" + clientId;
        }

        private string GetSiteName(int siteId)
        {
            ClientSite site;
            return _sitesById.TryGetValue(siteId, out site) ? SiteService.GetDisplayName(site) : "Site #" + siteId;
        }

        private static string NormalizeContractType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "Service Agreement";
            type = type.Trim();
            if (type.Equals("AMC", StringComparison.OrdinalIgnoreCase) || type.IndexOf("service", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Service Agreement";
            if (ContractTypes.Contains(type)) return type;
            return "Others";
        }

        private static string NormalizeFrequency(string frequency)
        {
            if (string.IsNullOrWhiteSpace(frequency)) return "Monthly";
            if (frequency.Equals("Yearly", StringComparison.OrdinalIgnoreCase)) return "Annually";
            if (frequency.Equals("Half-Yearly", StringComparison.OrdinalIgnoreCase)) return "Quarterly";
            return frequency;
        }

        private static string GetDisplayStatus(AMCContract contract)
        {
            if (contract == null) return "Draft";
            string status = string.IsNullOrWhiteSpace(contract.ContractStatus) ? "Draft" : contract.ContractStatus.Trim();
            if (contract.EndDate.Date < DateTime.Today) return "Expired";
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase) && IsExpiringSoon(contract)) return "Expiring Soon";
            return status;
        }

        private static bool IsExpiringSoon(AMCContract contract)
        {
            DateTime today = DateTime.Today;
            return contract.EndDate.Date >= today && contract.EndDate.Date <= today.AddDays(30) &&
                   !IsStatus(contract, "Expired") && !IsStatus(contract, "Cancelled");
        }

        private static bool IsStatus(AMCContract contract, string status)
        {
            return string.Equals(contract.ContractStatus, status, StringComparison.OrdinalIgnoreCase);
        }

        private static Color StatusColor(string status)
        {
            switch (status)
            {
                case "Active": return Color.FromArgb(5, 150, 105);
                case "Pending Approval": return Color.FromArgb(180, 83, 9);
                case "Expiring Soon": return Color.FromArgb(217, 119, 6);
                case "Expired": return Color.FromArgb(220, 38, 38);
                case "On Hold": return Color.FromArgb(79, 70, 229);
                case "Cancelled": return Color.FromArgb(71, 85, 105);
                default: return Blue;
            }
        }

        private static Color StatusBackColor(string status)
        {
            switch (status)
            {
                case "Active": return Color.FromArgb(209, 250, 229);
                case "Pending Approval": return Color.FromArgb(254, 243, 199);
                case "Expiring Soon": return Color.FromArgb(255, 237, 213);
                case "Expired": return Color.FromArgb(254, 226, 226);
                case "On Hold": return Color.FromArgb(224, 231, 255);
                case "Cancelled": return Color.FromArgb(241, 245, 249);
                default: return Color.FromArgb(239, 246, 255);
            }
        }

        private static string FormatDate(DateTime date)
        {
            return date == default(DateTime) ? "-" : date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        }

        private static string FormatCurrency(decimal value)
        {
            try { return IndiaFormatHelper.FormatCurrency(value); }
            catch { return string.Format(CultureInfo.GetCultureInfo("en-IN"), "₹ {0:N0}", value); }
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = message;
            _statusLabel.ForeColor = color;
        }

        private static string GetBoxText(TextBox box, string placeholder)
        {
            if (box == null || box.ForeColor == Muted || box.Text == placeholder) return string.Empty;
            return box.Text.Trim();
        }

        private static void AddPlaceholder(TextBox box, string placeholder)
        {
            box.GotFocus += (s, e) =>
            {
                if (box.Text == placeholder)
                {
                    box.Text = string.Empty;
                    box.ForeColor = Ink;
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = placeholder;
                    box.ForeColor = Muted;
                }
            };
        }

        private void SelectCombo(ComboBox combo, int id)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                ComboItem item = combo.Items[i] as ComboItem;
                if (item != null && item.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void SelectComboText(ComboBox combo, string text)
        {
            if (combo == null || combo.Items.Count == 0) return;
            int index = combo.Items.IndexOf(text);
            combo.SelectedIndex = index >= 0 ? index : 0;
        }

        private sealed class ComboItem
        {
            public int Id { get; set; }
            public string Text { get; set; }
            public override string ToString() { return Text; }
        }

        private sealed class ModernContractGroupBox : GroupBox
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(PageBg);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = DS.RoundedRect(rect, 10))
                using (SolidBrush brush = new SolidBrush(Color.White))
                using (Pen pen = new Pen(Border))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, Text ?? string.Empty, Font, new Rectangle(42, 14, Width - 60, 20), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                using (Image icon = ModernIconSystem.IconBitmap(ModernIconSystem.KindForTitle(Text), 16, ForeColor))
                    e.Graphics.DrawImage(icon, new Rectangle(18, 16, 16, 16));
            }
        }

        private sealed class StatusBarChart : Control
        {
            private Dictionary<string, int> _data = new Dictionary<string, int>();
            private readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>
            {
                { "Draft", Blue },
                { "Pending Approval", Amber },
                { "Active", Green },
                { "Expired", Red }
            };

            public void SetData(Dictionary<string, int> data)
            {
                _data = data ?? new Dictionary<string, int>();
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);
                int labelWidth = 125;
                int rightPad = 44;
                int top = 18;
                int rowHeight = 38;
                int max = Math.Max(1, _data.Values.DefaultIfEmpty(0).Max());
                using (Pen grid = new Pen(Color.FromArgb(241, 245, 249)))
                {
                    for (int i = 0; i <= 4; i++)
                    {
                        int x = labelWidth + (Width - labelWidth - rightPad) * i / 4;
                        e.Graphics.DrawLine(grid, x, top - 4, x, Height - 18);
                    }
                }
                int row = 0;
                foreach (string status in DashboardStatuses)
                {
                    int y = top + row * rowHeight;
                    int count = _data.ContainsKey(status) ? _data[status] : 0;
                    TextRenderer.DrawText(e.Graphics, status, new Font("Segoe UI", 9f, FontStyle.Bold), new Rectangle(0, y, labelWidth - 10, 22), Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    int barMax = Math.Max(1, Width - labelWidth - rightPad);
                    int barWidth = Math.Max(count > 0 ? 6 : 0, count * barMax / max);
                    Rectangle bar = new Rectangle(labelWidth, y + 4, barWidth, 18);
                    using (SolidBrush brush = new SolidBrush(_colors.ContainsKey(status) ? _colors[status] : Blue))
                        e.Graphics.FillRectangle(brush, bar);
                    TextRenderer.DrawText(e.Graphics, count.ToString(CultureInfo.InvariantCulture), new Font("Segoe UI", 9f, FontStyle.Bold), new Rectangle(labelWidth + barWidth + 8, y, 40, 22), Ink);
                    row++;
                }
            }
        }

        private sealed class TypeDonutChart : Control
        {
            private Dictionary<string, int> _data = new Dictionary<string, int>();
            private readonly Color[] _colors = { Blue, Green, Purple, Amber, Color.FromArgb(244, 63, 94) };

            public void SetData(Dictionary<string, int> data)
            {
                _data = data ?? new Dictionary<string, int>();
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(Color.White);
                int total = _data.Values.DefaultIfEmpty(0).Sum();
                int size = Math.Min(138, Math.Max(96, Height - 24));
                Rectangle donut = new Rectangle(22, Math.Max(20, (Height - size) / 2), size, size);
                float start = -90;
                int i = 0;
                foreach (string type in ContractTypes)
                {
                    int count = _data.ContainsKey(type) ? _data[type] : 0;
                    float sweep = total == 0 ? 0 : count * 360f / total;
                    using (Pen pen = new Pen(_colors[i % _colors.Length], 28))
                        e.Graphics.DrawArc(pen, donut, start, sweep);
                    start += sweep;
                    i++;
                }
                using (SolidBrush white = new SolidBrush(Color.White))
                    e.Graphics.FillEllipse(white, new Rectangle(donut.X + 30, donut.Y + 30, donut.Width - 60, donut.Height - 60));
                TextRenderer.DrawText(e.Graphics, total.ToString(CultureInfo.InvariantCulture), new Font("Segoe UI", 18f, FontStyle.Bold), new Rectangle(donut.X, donut.Y + donut.Height / 2 - 24, donut.Width, 30), Ink, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(e.Graphics, "Total", new Font("Segoe UI", 8.5f), new Rectangle(donut.X, donut.Y + donut.Height / 2 + 8, donut.Width, 20), Muted, TextFormatFlags.HorizontalCenter);

                int legendX = Math.Max(donut.Right + 40, 190);
                int legendY = Math.Max(20, (Height - 140) / 2);
                i = 0;
                foreach (string type in ContractTypes)
                {
                    int count = _data.ContainsKey(type) ? _data[type] : 0;
                    int pct = total == 0 ? 0 : (int)Math.Round(count * 100m / total);
                    using (SolidBrush brush = new SolidBrush(_colors[i % _colors.Length]))
                        e.Graphics.FillEllipse(brush, legendX, legendY + i * 28 + 5, 10, 10);
                    TextRenderer.DrawText(e.Graphics, type, new Font("Segoe UI", 9f), new Rectangle(legendX + 18, legendY + i * 28, Math.Max(120, Width - legendX - 102), 22), Ink, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(e.Graphics, count + " (" + pct + "%)", new Font("Segoe UI", 9f, FontStyle.Bold), new Rectangle(Width - 94, legendY + i * 28, 90, 22), Ink, TextFormatFlags.Right);
                    i++;
                }
            }
        }
    }
}
