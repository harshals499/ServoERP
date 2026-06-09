using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Controls;
using ServoERP.Validators;

namespace HVAC_Pro_Desktop.UI
{
    public class ClientManagementForm : DeferredPageControl
    {
        private readonly ClientService _clientService = new ClientService();
        private readonly ContractService _contractService = new ContractService();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly JobService _jobService = new JobService();
        private readonly SiteService _siteService = new SiteService();
        private readonly TenderService _tenderService = new TenderService();

        private readonly List<B2BClient> _clients = new List<B2BClient>();
        private readonly Dictionary<int, ClientCardControl> _cards = new Dictionary<int, ClientCardControl>();
        private readonly Dictionary<int, ClientCommandMetrics> _metricsCache = new Dictionary<int, ClientCommandMetrics>();

        private Panel _leftPanel;
        private Panel _rightPanel;
        private FlowLayoutPanel _clientList;
        private TextBox _searchBox;
        private GlobalPaginationControl _pagination;
        private ClientHeaderControl _header;
        private LifecyclePipelineControl _pipeline;
        private TableLayoutPanel _kpiGrid;
        private TabControl _tabs;
        private Label _footer;
        private Panel _dashboardHost;
        private TextBox _dashboardSearch;
        private TextBox _dashboardTableSearch;
        private ComboBox _dashboardStatusFilter;
        private DataGridView _dashboardClientsGrid;
        private Label _dashboardClientsShowingLabel;
        private GlobalPaginationControl _dashboardClientsPager;
        private readonly Timer _clientSearchTimer = new Timer();
        private readonly Timer _dashboardSearchTimer = new Timer();
        private string _dashboardStatus = "All Status";
        private string _dashboardSearchText = string.Empty;
        private string _dashboardTableSearchText = string.Empty;
        private string _dashboardSearchFocus = string.Empty;
        private int _dashboardSearchSelectionStart;
        private bool _dashboardClientsLoaded;
        private bool _renderingDashboard;
        private int _dashboardClientsPage = 1;
        private int _dashboardClientsPageSize = 10;
        private bool _bindingDashboardClientStatus;
        private readonly List<AMCContract> _dashboardContracts = new List<AMCContract>();
        private readonly List<Invoice> _dashboardInvoices = new List<Invoice>();
        private readonly List<TenderBid> _dashboardQuotes = new List<TenderBid>();
        private readonly List<ClientSite> _dashboardSites = new List<ClientSite>();

        private B2BClient _selectedClient;
        private int _metricsRequestVersion;
        private bool _searchPlaceholderActive = true;
        private bool _showDashboard = true;
        private int _clientPage = 1;
        private int _clientPageSize = 10;

        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public Action<int> OnNavigate { get; set; }
        public Action<int> OnOpenClientDetail { get; set; }

        public ClientManagementForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;
            ConfigureSearchTimers();
            BuildShell();
            if (!_showDashboard)
                RenderClientCards();
            EnableDeferredLoad(LoadClientsAsync, ex => AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Loading clients", ex));
        }

        /// <summary>Debounces client searches so the page does not rebuild after every typed character.</summary>
        private void ConfigureSearchTimers()
        {
            _clientSearchTimer.Interval = 240;
            _clientSearchTimer.Tick += (s, e) =>
            {
                _clientSearchTimer.Stop();
                RenderClientCards();
            };

            _dashboardSearchTimer.Interval = 280;
            _dashboardSearchTimer.Tick += (s, e) =>
            {
                _dashboardSearchTimer.Stop();
                if (_dashboardClientsGrid != null && !_dashboardClientsGrid.IsDisposed)
                    PopulateDashboardClientsGrid();
                else
                    RenderClientsDashboard();
            };
        }

        public void SelectClientFromNavigation(int clientId)
        {
            _showDashboard = false;
            BuildShell();
            ResetDeferredLoad();
            SelectClient(clientId);
            _ = LoadClientsAsync();
        }

        /// <summary>Returns the clients module to the dashboard view after closing a sub-page.</summary>
        public void ShowDashboardFromNavigation()
        {
            _showDashboard = true;
            _selectedClient = null;
            _dashboardClientsLoaded = false;
            BuildShell();
            ResetDeferredLoad();
            _ = LoadClientsAsync();
        }

        private void BuildShell()
        {
            Controls.Clear();
            if (_showDashboard)
            {
                BuildDashboardShell();
                SalesUiPolishService.ApplyAfterRebuild(this, "Clients");
                return;
            }

            _leftPanel = BuildLeftPanel();
            _rightPanel = BuildRightPanel();
            Controls.Add(_rightPanel);
            Controls.Add(_leftPanel);
            SalesUiPolishService.ApplyAfterRebuild(this, "Clients");
        }

        private Panel BuildLeftPanel()
        {
            var panel = new Panel { Dock = DockStyle.Left, Width = 326, BackColor = DS.BgPage, Padding = new Padding(18, 18, 12, 16) };
            panel.Paint += (s, e) =>
            {
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, panel.Width - 1, 0, panel.Width - 1, panel.Height);
            };

            Label title = new Label { Text = "Clients", Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = DS.Slate900 };

            Panel searchWrap = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White, Padding = new Padding(12, 10, 12, 0), Margin = new Padding(0, 0, 0, 10) };
            searchWrap.Paint += (s, e) => DrawRoundedBorder(e.Graphics, searchWrap.ClientRectangle, DS.Border, 7);
            _searchBox = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Text = "Search by name, phone, email...", ForeColor = DS.Slate500, Font = new Font("Segoe UI", 9f), BackColor = Color.White };
            _searchBox.GotFocus += (s, e) =>
            {
                if (_searchPlaceholderActive)
                {
                    _searchBox.Text = string.Empty;
                    _searchBox.ForeColor = DS.Slate900;
                    _searchPlaceholderActive = false;
                }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_searchBox.Text))
                {
                    _searchPlaceholderActive = true;
                    _searchBox.Text = "Search by name, phone, email...";
                    _searchBox.ForeColor = DS.Slate500;
                }
            };
            _searchBox.TextChanged += (s, e) =>
            {
                if (_searchPlaceholderActive)
                    return;

                ScheduleClientSearch();
            };
            searchWrap.Controls.Add(_searchBox);

            Button newClient = PrimaryButton("+ New Client", 42);
            newClient.Dock = DockStyle.Top;
            newClient.Click += (s, e) => ShowClientEditor(null, "New Client");
            Button forms = PrimaryButton("Service Forms", 38);
            forms.Dock = DockStyle.Top;
            forms.BackColor = Color.White;
            forms.ForeColor = DS.Primary600;
            forms.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(forms, ModernIconKind.Document);
            forms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Clients / Sites", "Clients", null, "site survey equipment inventory asset history customer feedback complaint handover sign-off client site");

            _pagination = new GlobalPaginationControl { Dock = DockStyle.Bottom, Height = 38, BackColor = DS.BgPage };
            _pagination.PageChanged += (s, e) =>
            {
                _clientPage = _pagination.CurrentPage;
                RenderClientCards();
            };
            _pagination.PageSizeChanged += (s, e) =>
            {
                _clientPageSize = _pagination.PageSize;
                _clientPage = 1;
                RenderClientCards();
            };
            _clientList = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = DS.BgPage, Padding = new Padding(0, 12, 6, 8) };
            _clientList.HorizontalScroll.Enabled = false;
            _clientList.HorizontalScroll.Visible = false;
            _clientList.Resize += (s, e) => ResizeClientCards();

            panel.Controls.Add(_clientList);
            panel.Controls.Add(_pagination);
            panel.Controls.Add(forms);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = DS.BgPage });
            panel.Controls.Add(newClient);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = DS.BgPage });
            panel.Controls.Add(searchWrap);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = DS.BgPage });
            panel.Controls.Add(title);
            return panel;
        }

        /// <summary>Schedules the classic client-card search render.</summary>
        private void ScheduleClientSearch()
        {
            _clientPage = 1;
            _clientSearchTimer.Stop();
            _clientSearchTimer.Start();
        }

        private Panel BuildRightPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(16, 16, 18, 14), AutoScroll = true };
            panel.HorizontalScroll.Enabled = false;
            panel.HorizontalScroll.Visible = false;

            Panel content = new Panel { Dock = DockStyle.Top, BackColor = DS.BgPage, Height = 820 };
            content.Resize += (s, e) => LayoutRightContent(content);

            _header = new ClientHeaderControl { Location = new Point(0, 0) };
            _header.Dock = DockStyle.None;
            _header.Height = 94;
            _header.AddJobClicked += (s, e) =>
            {
                if (_selectedClient != null && _selectedClient.ClientID > 0)
                    WorkflowLaunchContext.SetJobDraft(_selectedClient.ClientID, 0);
                OnNavigate?.Invoke(15);
                ShowToast("Opened Jobs. Start a new service job for " + SelectedName() + ".");
            };
            _header.CreateInvoiceClicked += (s, e) =>
            {
                OnNavigate?.Invoke(3);
                ShowToast("Opened Invoices. Create the invoice for " + SelectedName() + ".");
            };
            _header.EditProfileClicked += (s, e) =>
            {
                if (_selectedClient != null && _selectedClient.ClientID > 0)
                    OnOpenClientDetail?.Invoke(_selectedClient.ClientID);
            };
            _header.MoreClicked += (s, e) => ShowMoreMenu();
            _header.StatusClicked += (s, e) => ShowClientStatusMenu(_selectedClient, _header.StatusAnchor);

            _pipeline = new LifecyclePipelineControl { Location = new Point(0, 110) };
            _pipeline.Dock = DockStyle.None;
            _pipeline.Height = 112;
            _pipeline.StageClicked += (s, stage) => ChangeStage(stage);

            _kpiGrid = new TableLayoutPanel { Location = new Point(0, 234), Height = 104, ColumnCount = 4, RowCount = 1, BackColor = DS.BgPage };
            for (int i = 0; i < 4; i++)
                _kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _kpiGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _tabs = new TabControl { Location = new Point(0, 354), Height = 410, Font = new Font("Segoe UI", 9f), Appearance = TabAppearance.Normal };
            foreach (string tab in new[] { "Details", "Notes", "Financials", "Documents" })
                _tabs.TabPages.Add(new TabPage(tab) { Name = tab, BackColor = Color.White });
            _tabs.SelectedIndexChanged += (s, e) => RenderSelectedTab();

            _footer = new Label { Location = new Point(0, 778), Height = 24, Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleLeft };

            content.Controls.Add(_footer);
            content.Controls.Add(_tabs);
            content.Controls.Add(_kpiGrid);
            content.Controls.Add(_pipeline);
            content.Controls.Add(_header);
            panel.Controls.Add(content);
            panel.Resize += (s, e) =>
            {
                content.Width = Math.Max(640, panel.ClientSize.Width - panel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
                content.Height = Math.Max(820, _footer.Bottom + 18);
                panel.AutoScrollMinSize = new Size(0, content.Height + panel.Padding.Vertical + 8);
                LayoutRightContent(content);
            };
            content.Width = Math.Max(640, panel.ClientSize.Width - panel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
            content.Height = Math.Max(820, _footer.Bottom + 18);
            panel.AutoScrollMinSize = new Size(0, content.Height + panel.Padding.Vertical + 8);
            return panel;
        }

        private void LayoutRightContent(Panel content)
        {
            int width = Math.Max(640, content.ClientSize.Width);
            _header.Width = width;
            _pipeline.Width = width;
            _kpiGrid.Width = width;
            _tabs.Width = width;
            _footer.Width = width;
            foreach (Control control in _kpiGrid.Controls)
                control.Margin = new Padding(0, 0, 10, 0);
        }

        private async Task LoadClientsAsync()
        {
            List<B2BClient> loaded = null;
            List<AMCContract> contracts = null;
            List<Invoice> invoices = null;
            List<TenderBid> quotes = null;
            List<ClientSite> sites = null;
            try
            {
                await Task.Run(() =>
                {
                    if (_showDashboard)
                    {
                        AppDataCache.RemovePrefix("clients:");
                        AppDataCache.RemovePrefix("contracts:");
                        AppDataCache.RemovePrefix("invoices:");
                        AppDataCache.RemovePrefix("sites:");
                    }
                    loaded = _clientService.GetAllClientsIncludingInactive().OrderBy(c => c.CompanyName).ToList();
                    if (_showDashboard)
                    {
                        try { contracts = _contractService.GetAllContracts(); } catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadDashboard.Contracts", ex); contracts = new List<AMCContract>(); }
                        try { invoices = _invoiceService.GetAllInvoices(); } catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadDashboard.Invoices", ex); invoices = new List<Invoice>(); }
                        try { quotes = _tenderService.GetAll(); } catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadDashboard.Quotes", ex); quotes = new List<TenderBid>(); }
                        try { sites = _siteService.GetAll(); } catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadDashboard.Sites", ex); sites = new List<ClientSite>(); }
                    }
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientManagementForm.LoadClients", ex);
                try
                {
                    AppDataCache.RemovePrefix("clients:");
                    loaded = await Task.Run(() => _clientService.GetAllClients().OrderBy(c => c.CompanyName).ToList());
                }
                catch (Exception inner) { AppLogger.LogError("ClientManagementForm.LoadClients.Fallback", inner); }
            }

            if (IsDisposed)
                return;

            if (_showDashboard)
                _dashboardClientsLoaded = true;

            if (loaded == null || loaded.Count == 0)
            {
                _clients.Clear();
                _selectedClient = null;
                if (_showDashboard)
                {
                    _dashboardContracts.Clear();
                    _dashboardInvoices.Clear();
                    _dashboardQuotes.Clear();
                    _dashboardSites.Clear();
                    RenderClientsDashboard();
                }
                else
                {
                    RenderClientCards();
                }
                return;
            }

            int selectedId = _selectedClient == null ? loaded[0].ClientID : _selectedClient.ClientID;
            _clients.Clear();
            _clients.AddRange(loaded);
            if (_showDashboard)
            {
                _dashboardContracts.Clear();
                _dashboardContracts.AddRange(contracts ?? new List<AMCContract>());
                _dashboardInvoices.Clear();
                _dashboardInvoices.AddRange(invoices ?? new List<Invoice>());
                _dashboardQuotes.Clear();
                _dashboardQuotes.AddRange(quotes ?? new List<TenderBid>());
                _dashboardSites.Clear();
                _dashboardSites.AddRange(sites ?? new List<ClientSite>());
                RenderClientsDashboard();
                return;
            }
            _metricsCache.Clear();
            RenderClientCards();
            SelectClient(_clients.Any(c => c.ClientID == selectedId) ? selectedId : _clients[0].ClientID);
        }

        private void RenderClientCards()
        {
            if (_clientList == null)
                return;

            _clientList.SuspendLayout();
            _clientList.Controls.Clear();
            _cards.Clear();

            List<B2BClient> filtered = FilteredClients().ToList();
            int pageSize = Math.Max(1, _clientPageSize);
            _clientPage = PaginationState.NormalizePage(_clientPage, filtered.Count, pageSize);
            foreach (B2BClient client in filtered.Skip((_clientPage - 1) * pageSize).Take(pageSize))
            {
                ClientCardControl card = new ClientCardControl();
                card.Width = ClientCardWidth();
                card.Bind(client.ClientID, Initials(client.CompanyName), Safe(client.CompanyName, "Client"), Safe(client.IndustryType, "HVAC Services"), Safe(client.City, "Mumbai"), GetClientStatus(client), ProgressFor(client));
                card.Click += (s, e) => SelectClient(((ClientCardControl)s).ClientId);
                card.StatusClicked += (s, e) => ShowClientStatusMenu(client, ((ClientCardControl)s).StatusAnchor);
                _cards[client.ClientID] = card;
                _clientList.Controls.Add(card);
            }
            if (filtered.Count == 0)
                _clientList.Controls.Add(BuildClientEmptyState());
            _clientList.ResumeLayout();
            RenderPagination(filtered.Count);
            HighlightSelectedCard();
        }

        private Control BuildClientEmptyState()
        {
            Panel empty = new Panel
            {
                Width = ClientCardWidth(),
                Height = 360,
                BackColor = Color.White,
                Margin = new Padding(0, 20, 0, 0)
            };
            Label icon = new Label
            {
                Text = "□",
                Size = new Size(58, 52),
                Location = new Point((empty.Width - 58) / 2, 110),
                BackColor = DS.Primary50,
                ForeColor = DS.Primary600,
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(icon, 12);
            Label title = new Label { Text = "No clients added yet", Location = new Point(20, 174), Size = new Size(empty.Width - 40, 26), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter };
            Label message = new Label { Text = "Create the client first, then add sites, contacts, contracts, jobs, and invoices." + Environment.NewLine + UIHelper.BuildWorkflowHintText("Client", "Site", "Job", "Quotation", "Invoice", "Payment"), Location = new Point(34, 202), Size = new Size(empty.Width - 68, 66), Font = DS.Body, ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter };
            Button create = DS.GhostBtn("+ New Client", 112, 32);
            create.Location = new Point((empty.Width - create.Width) / 2, 278);
            create.Click += (s, e) => ShowClientEditor(null, "New Client");
            empty.Resize += (s, e) =>
            {
                icon.Left = (empty.Width - icon.Width) / 2;
                title.Width = empty.Width - 40;
                message.Width = empty.Width - 68;
                create.Left = (empty.Width - create.Width) / 2;
            };
            empty.Controls.AddRange(new Control[] { icon, title, message, create });
            return empty;
        }

        private void BuildDashboardShell()
        {
            _dashboardHost = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, AutoScroll = true, Padding = new Padding(22, 16, 22, 22) };
            _dashboardHost.AutoScrollMinSize = new Size(0, 1240);
            Controls.Add(_dashboardHost);
        }

        private void RenderClientsDashboard()
        {
            if (_dashboardHost == null || _dashboardHost.IsDisposed)
                return;

            _renderingDashboard = true;
            _dashboardHost.SuspendLayout();
            try
            {
                _dashboardHost.Controls.Clear();
                int contentHeight = 1280;
                Panel content = new Panel { BackColor = DS.BgPage, Location = new Point(22, 16), Size = new Size(Math.Max(960, _dashboardHost.ClientSize.Width - 58), contentHeight) };
                _dashboardHost.AutoScrollMinSize = new Size(0, contentHeight + 44);
                _dashboardHost.HorizontalScroll.Enabled = false;
                _dashboardHost.HorizontalScroll.Visible = false;
                _dashboardHost.Controls.Add(content);

                Control header = BuildClientsDashboardHeader(content.Width);
                header.Location = new Point(0, 0);
                content.Controls.Add(header);

                FlowLayoutPanel stats = new FlowLayoutPanel { Location = new Point(0, 74), Size = new Size(content.Width, 96), BackColor = DS.BgPage, WrapContents = false, AutoScroll = false };
                int cardWidth = Math.Max(206, (content.Width - 64) / 5);
                foreach (Control card in BuildClientStatCards(cardWidth))
                    stats.Controls.Add(card);
                content.Controls.Add(stats);

                TableLayoutPanel top = new TableLayoutPanel { Location = new Point(0, 188), Size = new Size(content.Width, 238), BackColor = DS.BgPage, ColumnCount = 4, RowCount = 1 };
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
                top.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                top.Controls.Add(BuildClientStatusCard(), 0, 0);
                top.Controls.Add(BuildClientTypeCard(), 1, 0);
                top.Controls.Add(BuildClientSummaryCard(), 2, 0);
                content.Controls.Add(top);

                TableLayoutPanel middle = new TableLayoutPanel { Location = new Point(0, 444), Size = new Size(content.Width, 520), BackColor = DS.BgPage, ColumnCount = 2, RowCount = 1 };
                middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76f));
                middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
                middle.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                middle.Controls.Add(BuildAllClientsCard(), 0, 0);
                middle.Controls.Add(BuildClientDashboardSidebar(), 1, 0);
                content.Controls.Add(middle);

                TableLayoutPanel bottom = new TableLayoutPanel { Location = new Point(0, 982), Size = new Size(content.Width, 230), BackColor = DS.BgPage, ColumnCount = 2, RowCount = 1 };
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                bottom.Controls.Add(BuildClientRenewalsCard(), 0, 0);
                bottom.Controls.Add(BuildTopClientsRevenueCard(), 1, 0);
                content.Controls.Add(bottom);
            }
            finally
            {
                _dashboardHost.ResumeLayout();
                _renderingDashboard = false;
            }
            RestoreDashboardSearchFocus();
        }

        private Control BuildClientsDashboardHeader(int width)
        {
            Panel header = new Panel { Size = new Size(width, 58), BackColor = DS.BgPage };
            header.Controls.Add(new Label { Text = "Clients Management", Location = new Point(0, 0), Size = new Size(330, 28), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = DS.Slate900 });
            header.Controls.Add(new Label { Text = "Manage clients first; sites, contacts, jobs, and invoices can be added when details are ready.", Location = new Point(1, 30), Size = new Size(680, 18), Font = new Font("Segoe UI", 8.8f), ForeColor = DS.Slate500 });

            _dashboardSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate900, Text = DashboardSearchText(), Size = new Size(340, 30) };
            ConfigureDashboardPlaceholder(_dashboardSearch, "Search clients by name, email, phone, or company...");
            _dashboardSearch.Enter += (s, e) => _dashboardSearchFocus = "global";
            _dashboardSearch.TextChanged += (s, e) => HandleDashboardSearchChanged(_dashboardSearch, "global", "Search clients by name, email, phone, or company...");
            Button filters = DashboardButton("Filter Clients", Color.White, DS.Slate900, 112, true);
            filters.Click += (s, e) => { if (_dashboardStatusFilter != null) _dashboardStatusFilter.DroppedDown = true; };
            Button add = DashboardButton("+ Add Client  v", DS.Primary600, Color.White, 136, false);
            add.Click += (s, e) =>
            {
                ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
                menu.Items.Add("Add Client", null, (mi, ev) => OpenClientEditor(null, "New Client"));
                menu.Items.Add("Import Clients", null, (mi, ev) => ImportUiHelper.RunImport(ExcelImportModule.Clients, FindForm(), result => RefreshClientsAfterImport()));
                menu.Items.Add("Add Contact", null, (mi, ev) => ShowActionModal("Add Contact", "Open a client record first, then add contact details.", "Contact name", ""));
                menu.Show(add, new Point(0, add.Height));
            };
            Label bell = new Label { Text = "!", Size = new Size(30, 30), BackColor = DS.Red50, ForeColor = DS.Red600, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            bell.Click += (s, e) => ShowToast(BuildClientAlertsText());
            Panel user = BuildSessionUserPanel();
            header.Controls.AddRange(new Control[] { _dashboardSearch, filters, add, bell, user });
            Action layoutHeaderControls = () =>
            {
                user.Location = new Point(header.Width - user.Width, 2);
                bell.Location = new Point(user.Left - 38, 2);
                add.Location = new Point(bell.Left - add.Width - 10, 1);
                filters.Location = new Point(add.Left - filters.Width - 12, 1);
                int searchWidth = Math.Min(340, Math.Max(220, filters.Left - 374));
                _dashboardSearch.Size = new Size(searchWidth, 30);
                _dashboardSearch.Location = new Point(Math.Max(360, filters.Left - searchWidth - 12), 1);
            };
            header.Resize += (s, e) => layoutHeaderControls();
            layoutHeaderControls();
            return header;
        }

        /// <summary>Clears client dashboard caches and reloads visible client counts after Excel import.</summary>
        private void RefreshClientsAfterImport()
        {
            if (IsDisposed)
                return;

            AppDataCache.RemovePrefix("clients:");
            AppDataCache.RemovePrefix("contracts:");
            AppDataCache.RemovePrefix("invoices:");
            AppDataCache.RemovePrefix("sites:");
            _dashboardClientsLoaded = false;
            _dashboardClientsPage = 1;
            _ = LoadClientsAsync();
        }

        private IEnumerable<Control> BuildClientStatCards(int width)
        {
            bool loaded = !_showDashboard || _dashboardClientsLoaded;
            int total = _clients.Count;
            int active = _clients.Count(c => GetClientStatus(c) == "Active");
            int clientContracts = _dashboardContracts.Count(c => _clients.Any(cl => cl.ClientID == c.ClientID));
            decimal outstanding = _dashboardInvoices.Where(i => !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).Sum(i => Math.Max(0m, i.BalanceDue));
            int renewals = UpcomingClientRenewals().Count();
            yield return DashboardStatCard(width, "Total Clients", loaded ? total.ToString() : "-", "All registered clients", DS.Primary50, DS.Primary600);
            yield return DashboardStatCard(width, "Active Clients", loaded ? active.ToString() : "-", "Currently active clients", DS.Green50, DS.Green600);
            yield return DashboardStatCard(width, "Total Contracts", loaded ? clientContracts.ToString() : "-", "All client contracts", Color.FromArgb(245, 243, 255), Color.FromArgb(124, 58, 237));
            yield return DashboardStatCard(width, "Outstanding Balance", loaded ? IndiaFormatHelper.FormatCurrency(outstanding) : "-", "Total outstanding amount", DS.Amber50, DS.Amber600);
            yield return DashboardStatCard(width, "Upcoming Renewals", loaded ? renewals.ToString() : "-", "Contracts up for renewal", DS.Red50, DS.Red600);
        }

        private Panel BuildClientStatusCard()
        {
            Panel card = DashboardCard("Client Status Overview", "...", (s, e) => ShowToast("Status overview uses live client records."));
            var slices = new List<DashSlice>
            {
                new DashSlice("Active", _clients.Count(c => GetClientStatus(c) == "Active"), DS.Primary600),
                new DashSlice("Inactive", _clients.Count(c => GetClientStatus(c) == "Inactive"), DS.Green600),
                new DashSlice("Prospect", _clients.Count(c => GetClientStatus(c) == "Prospect"), DS.Amber500),
                new DashSlice("On Hold", _clients.Count(c => GetClientStatus(c) == "On Hold"), DS.Amber600),
                new DashSlice("Blacklisted", _clients.Count(c => GetClientStatus(c) == "Blacklisted"), DS.Red500)
            };
            List<DashSlice> chartSlices = slices.Where(s => s.Value > 0).ToList();
            if (!_dashboardClientsLoaded || _clients.Count == 0 || chartSlices.Count == 0) AddEmptyState(card, "No data available", "Client status data will appear here.", 58);
            else
            {
                card.Controls.Add(Donut(chartSlices, _clients.Count.ToString(), "Total", new Point(20, 54), new Size(126, 126)));
                AddLegend(card, slices, 164, 58, _clients.Count);
            }
            return card;
        }

        private Panel BuildClientTypeCard()
        {
            Panel card = DashboardCard("Clients by Type", "i", (s, e) => ShowToast("Client type is derived from Industry Type."));
            string[] types = { "Residential", "Commercial", "Industrial", "Government", "Other" };
            Color[] colors = { DS.Primary500, DS.Primary700, DS.Slate900, DS.Teal600, DS.Slate400 };
            var slices = types.Select((t, i) => new DashSlice(t, _clients.Count(c => GetClientType(c) == t), colors[i])).ToList();
            List<DashSlice> chartSlices = slices.Where(s => s.Value > 0).ToList();
            if (!_dashboardClientsLoaded || _clients.Count == 0 || chartSlices.Count == 0) AddEmptyState(card, "No data available", "Client type data will appear here.", 58);
            else
            {
                card.Controls.Add(Donut(chartSlices, _clients.Count.ToString(), "Total", new Point(20, 54), new Size(126, 126)));
                AddLegend(card, slices, 164, 58, _clients.Count);
            }
            return card;
        }

        private Panel BuildClientSummaryCard()
        {
            Panel card = DashboardCard("Client Summary", null, null);
            var rows = new[]
            {
                Tuple.Create("Total Contacts", _clients.Sum(c => c.Contacts == null ? 0 : c.Contacts.Count).ToString()),
                Tuple.Create("Service Sites", _dashboardSites.Count.ToString()),
                Tuple.Create("Active Contracts", _dashboardContracts.Count(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase)).ToString()),
                Tuple.Create("Open Invoices", _dashboardInvoices.Count(i => !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)).ToString()),
                Tuple.Create("Pending Quotes", _dashboardQuotes.Count(q => string.Equals(q.Status, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(q.Status, "Sent", StringComparison.OrdinalIgnoreCase) || string.Equals(q.Status, "Submitted", StringComparison.OrdinalIgnoreCase)).ToString()),
                Tuple.Create("Support Tickets", "-")
            };
            int y = 48;
            foreach (var row in rows)
            {
                card.Controls.Add(new Label { Text = row.Item1, Location = new Point(16, y), Size = new Size(150, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate800 });
                card.Controls.Add(new Label { Text = row.Item2, Location = new Point(card.Width - 64, y), Size = new Size(42, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleRight });
                y += 28;
            }
            return card;
        }

        private Panel BuildAllClientsCard()
        {
            Panel card = DashboardCard("All Clients", null, null);
            card.Size = new Size(900, 350);

            Panel toolbar = new Panel { Location = new Point(16, 40), Size = new Size(card.Width - 32, 38), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.White };
            _dashboardTableSearch = new TextBox { BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate900, Text = DashboardTableSearchText(), Size = new Size(220, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            ConfigureDashboardPlaceholder(_dashboardTableSearch, "Search clients...");
            _dashboardTableSearch.Enter += (s, e) => _dashboardSearchFocus = "table";
            _dashboardTableSearch.TextChanged += (s, e) => HandleDashboardSearchChanged(_dashboardTableSearch, "table", "Search clients...");

            _dashboardStatusFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 8.5f), Size = new Size(150, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _dashboardStatusFilter.Items.AddRange(new object[] { "All Status", "Active", "Inactive", "Prospect", "On Hold", "Blacklisted" });
            int statusIndex = Math.Max(0, _dashboardStatusFilter.Items.IndexOf(_dashboardStatus));
            _dashboardStatusFilter.SelectedIndex = statusIndex;
            _dashboardStatusFilter.SelectedIndexChanged += (s, e) => { _dashboardStatus = Convert.ToString(_dashboardStatusFilter.SelectedItem); _dashboardClientsPage = 1; PopulateDashboardClientsGrid(); };
            Button export = DashboardButton("Export", Color.White, DS.Slate900, 76, true);
            export.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            export.Click += (s, e) => ExportDashboardClientsCsv();
            toolbar.Controls.Add(_dashboardTableSearch);
            toolbar.Controls.Add(_dashboardStatusFilter);
            toolbar.Controls.Add(export);
            Action layoutToolbar = () =>
            {
                export.Location = new Point(toolbar.Width - export.Width, 2);
                _dashboardStatusFilter.Location = new Point(export.Left - _dashboardStatusFilter.Width - 10, 2);
                _dashboardTableSearch.Location = new Point(_dashboardStatusFilter.Left - _dashboardTableSearch.Width - 10, 2);
            };
            toolbar.Resize += (s, e) => layoutToolbar();
            layoutToolbar();
            card.Controls.Add(toolbar);

            _dashboardClientsGrid = new DataGridView
            {
                Location = new Point(16, 90),
                Size = new Size(card.Width - 32, 204),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            ConfigureDashboardClientsGrid();
            GridTheme.Apply(_dashboardClientsGrid);
            _dashboardClientsGrid.Dock = DockStyle.None;
            _dashboardClientsShowingLabel = new Label { Location = new Point(16, 308), Size = new Size(360, 18), Font = new Font("Segoe UI", 7.8f), ForeColor = DS.Slate500 };
            _dashboardClientsPager = new GlobalPaginationControl
            {
                Location = new Point(card.Width - 576, 304),
                Size = new Size(560, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White
            };
            _dashboardClientsPager.PageChanged += (s, e) =>
            {
                _dashboardClientsPage = _dashboardClientsPager.CurrentPage;
                PopulateDashboardClientsGrid();
            };
            _dashboardClientsPager.PageSizeChanged += (s, e) =>
            {
                _dashboardClientsPageSize = _dashboardClientsPager.PageSize;
                _dashboardClientsPage = 1;
                PopulateDashboardClientsGrid();
            };
            card.Controls.Add(_dashboardClientsGrid);
            card.Controls.Add(_dashboardClientsShowingLabel);
            card.Controls.Add(_dashboardClientsPager);
            card.Resize += (s, e) =>
            {
                toolbar.Width = Math.Max(120, card.ClientSize.Width - 32);
                _dashboardClientsGrid.Size = new Size(Math.Max(120, card.ClientSize.Width - 32), Math.Max(140, card.ClientSize.Height - 146));
                _dashboardClientsShowingLabel.Location = new Point(16, Math.Max(300, card.ClientSize.Height - 42));
                _dashboardClientsPager.Width = Math.Max(360, Math.Min(560, card.ClientSize.Width - 420));
                _dashboardClientsPager.Location = new Point(card.ClientSize.Width - _dashboardClientsPager.Width - 16, Math.Max(298, card.ClientSize.Height - 44));
            };
            PopulateDashboardClientsGrid();
            return card;
        }

        private Panel BuildClientDashboardSidebar()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(10, 0, 0, 0) };
            TableLayoutPanel stack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = DS.BgPage };
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 252f));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            stack.Controls.Add(BuildClientGroupsCard(), 0, 0);
            stack.Controls.Add(BuildClientQuickActionsCard(), 0, 1);
            stack.Controls.Add(BuildClientSmartAlertsCard(), 0, 2);
            panel.Controls.Add(stack);
            return panel;
        }

        private Panel BuildClientGroupsCard()
        {
            Panel card = DashboardCard("Top Client Groups", null, null);
            var groups = _clients.GroupBy(c => Safe(GetClientType(c), "Other")).Select(g => new { Name = g.Key, Count = g.Count(), Revenue = _dashboardInvoices.Where(i => g.Any(c => c.ClientID == i.ClientID)).Sum(i => i.TotalAmount) }).OrderByDescending(g => g.Revenue).Take(4).ToList();
            if (groups.Count == 0)
            {
                AddEmptyState(card, "No data available.", "Client groups will appear here.", 52);
                return card;
            }
            int y = 46;
            foreach (var g in groups)
            {
                card.Controls.Add(new Label { Text = g.Name, Location = new Point(16, y), Size = new Size(116, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = g.Count + " clients", Location = new Point(134, y), Size = new Size(66, 18), Font = new Font("Segoe UI", 7.2f), ForeColor = DS.Slate500 });
                card.Controls.Add(new Label { Text = IndiaFormatHelper.FormatCurrency(g.Revenue), Location = new Point(card.Width - 92, y), Size = new Size(72, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.2f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleRight });
                y += 27;
            }
            return card;
        }

        private Panel BuildClientQuickActionsCard()
        {
            Panel card = DashboardCard("Quick Actions", null, null);
            var actions = new[]
            {
                Tuple.Create("+", "Add Client"),
                Tuple.Create("@", "Add Contact"),
                Tuple.Create("C", "Create Contract"),
                Tuple.Create("Q", "New Quote"),
                Tuple.Create("Cal", "Schedule Service"),
                Tuple.Create("Msg", "Send Message")
            };
            for (int i = 0; i < actions.Length; i++)
            {
                Button b = DashboardButton(actions[i].Item2, Color.White, DS.Slate900, 128, true);
                b.Size = new Size(128, 42);
                b.Location = new Point(16 + (i % 2) * 136, 42 + (i / 2) * 46);
                b.Font = new Font("Segoe UI", 8.1f, FontStyle.Bold);
                b.TextAlign = ContentAlignment.MiddleCenter;
                string action = actions[i].Item2;
                b.Click += (s, e) => HandleClientQuickAction(action);
                card.Controls.Add(b);
            }
            card.Resize += (s, e) =>
            {
                int gap = 8;
                bool singleColumn = card.ClientSize.Width < 276;
                int columns = singleColumn ? 1 : 2;
                int width = Math.Max(112, (card.ClientSize.Width - 32 - (gap * (columns - 1))) / columns);
                for (int i = 0; i < actions.Length; i++)
                {
                    Button b = card.Controls.OfType<Button>().ElementAtOrDefault(i);
                    if (b == null)
                        continue;
                    b.Width = width;
                    b.Location = new Point(16 + (i % columns) * (width + gap), 42 + (i / columns) * 46);
                }
            };
            return card;
        }

        private Panel BuildClientSmartAlertsCard()
        {
            Panel card = DashboardCard("Smart Alerts", null, null);
            List<Tuple<string, Color>> alerts = BuildClientAlerts();
            if (alerts.Count == 0)
            {
                AddEmptyState(card, "No alerts at this time.", "Important client alerts will appear here.", 54);
                return card;
            }
            int y = 46;
            foreach (var alert in alerts)
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(16, y + 2), Size = new Size(16, 16), ForeColor = alert.Item2 });
                card.Controls.Add(new Label { Text = alert.Item1, Location = new Point(36, y), Size = new Size(card.Width - 54, 20), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate800, AutoEllipsis = true });
                y += 24;
            }
            return card;
        }

        private Panel BuildClientRenewalsCard()
        {
            Panel card = DashboardCard("Upcoming Renewals", "View All", (s, e) => { OnNavigate?.Invoke(2); ShowToast("Opened Contracts. Review upcoming renewals from the contracts dashboard."); });
            var rows = UpcomingClientRenewals().Take(5).ToList();
            if (rows.Count == 0)
            {
                AddEmptyState(card, "No upcoming renewals.", "Renewal information will appear here.", 70);
                return card;
            }
            TableLayoutPanel table = new TableLayoutPanel { Location = new Point(16, 48), Size = new Size(card.Width - 32, 144), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, ColumnCount = 5, RowCount = 1, BackColor = Color.White };
            foreach (float w in new[] { 25f, 25f, 18f, 14f, 18f }) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            string[] heads = { "Client", "Contract", "Renewal Date", "Days Left", "Amount" };
            for (int i = 0; i < heads.Length; i++) table.Controls.Add(TableLabel(heads[i], true, DS.Slate900), i, 0);
            foreach (var row in rows)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
                int r = table.RowCount++;
                table.Controls.Add(TableLabel(ClientName(row.ClientID), false, DS.Slate900), 0, r);
                table.Controls.Add(TableLabel(Safe(row.ContractType, "Contract"), false, DS.Slate600), 1, r);
                table.Controls.Add(TableLabel(row.EndDate.ToString("dd MMM yyyy"), false, DS.Slate900), 2, r);
                table.Controls.Add(TableLabel(((row.EndDate.Date - DateTime.Today).Days).ToString(), false, RenewalColor(row.EndDate)), 3, r);
                table.Controls.Add(TableLabel(IndiaFormatHelper.FormatCurrency(row.AnnualValue), false, DS.Slate900), 4, r);
            }
            card.Controls.Add(table);
            return card;
        }

        private Panel BuildTopClientsRevenueCard()
        {
            Panel card = DashboardCard("Top Clients by Revenue", "View Report", (s, e) => { OnNavigate?.Invoke(7); ShowToast("Opened Reports. Use invoice and client filters to review revenue."); });
            var rows = _dashboardInvoices.GroupBy(i => i.ClientID).Select(g => new { ClientId = g.Key, Amount = g.Sum(i => i.TotalAmount) }).Where(r => r.Amount > 0).OrderByDescending(r => r.Amount).Take(6).ToList();
            if (rows.Count == 0)
            {
                AddEmptyState(card, "No revenue data yet.", "Revenue data will appear here once invoices are recorded.", 70);
                return card;
            }
            decimal max = rows.Max(r => r.Amount);
            int y = 50, rank = 1;
            foreach (var row in rows)
            {
                card.Controls.Add(new Label { Text = rank.ToString(), Location = new Point(18, y), Size = new Size(18, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate500 });
                card.Controls.Add(new Label { Text = ClientName(row.ClientId), Location = new Point(42, y), Size = new Size(170, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
                Panel track = new Panel { Location = new Point(218, y + 7), Size = new Size(card.Width - 340, 5), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, BackColor = DS.Slate100 };
                track.Controls.Add(new Panel { Dock = DockStyle.Left, Width = (int)(track.Width * (row.Amount / max)), BackColor = DS.Primary600 });
                card.Controls.Add(track);
                card.Controls.Add(new Label { Text = IndiaFormatHelper.FormatCurrency(row.Amount), Location = new Point(card.Width - 108, y), Size = new Size(88, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleRight });
                y += 27;
                rank++;
            }
            return card;
        }

        private int ClientCardWidth()
        {
            int width = _clientList == null || _clientList.ClientSize.Width <= 0 ? 278 : _clientList.ClientSize.Width - 28;
            return Math.Max(238, width);
        }

        private IEnumerable<B2BClient> DashboardFilteredClients()
        {
            IEnumerable<B2BClient> query = _clients;
            string status = !string.IsNullOrWhiteSpace(_dashboardStatus) ? _dashboardStatus : (_dashboardStatusFilter == null ? "All Status" : Convert.ToString(_dashboardStatusFilter.SelectedItem));
            if (!string.IsNullOrWhiteSpace(status) && status != "All Status")
                query = query.Where(c => GetClientStatus(c) == status);
            string globalSearch = DashboardSearchText();
            string tableSearch = DashboardTableSearchText();
            if (!string.IsNullOrWhiteSpace(globalSearch))
            {
                string needle = globalSearch.Trim().ToUpperInvariant();
                query = query.Where(c => string.Join(" ", c.CompanyName, c.Email, c.Phone, c.PrimaryContact, c.IndustryType, c.BillingAddress, c.City).ToUpperInvariant().Contains(needle));
            }
            if (!string.IsNullOrWhiteSpace(tableSearch))
            {
                string needle = tableSearch.Trim().ToUpperInvariant();
                query = query.Where(c => string.Join(" ", c.CompanyName, c.Email, c.Phone, c.PrimaryContact, c.IndustryType, c.BillingAddress, c.City).ToUpperInvariant().Contains(needle));
            }
            return query.OrderBy(c => Safe(c.CompanyName, "Client"));
        }

        private string DashboardSearchText()
        {
            return _dashboardSearchText ?? string.Empty;
        }

        private string DashboardTableSearchText()
        {
            return _dashboardTableSearchText ?? string.Empty;
        }

        private void ConfigureDashboardPlaceholder(TextBox box, string placeholder)
        {
            if (box == null)
                return;

            if (string.Equals((box.Text ?? string.Empty).Trim(), placeholder, StringComparison.OrdinalIgnoreCase))
                box.Text = string.Empty;

            box.ForeColor = DS.Slate900;
            box.HandleCreated += (s, e) => SetCueBanner(box, placeholder);
            if (box.IsHandleCreated)
                SetCueBanner(box, placeholder);
        }

        /// <summary>Applies native placeholder text without inserting it into the editable value.</summary>
        private static void SetCueBanner(TextBox box, string placeholder)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated)
                return;

            SendMessage(box.Handle, EM_SETCUEBANNER, new IntPtr(1), placeholder ?? string.Empty);
        }

        /// <summary>Captures dashboard search text and schedules one filtered render after typing settles.</summary>
        private void HandleDashboardSearchChanged(TextBox box, string focusKey, string placeholder)
        {
            if (_renderingDashboard || box == null)
                return;

            _dashboardSearchFocus = focusKey;
            _dashboardSearchSelectionStart = box.SelectionStart;
            string text = IsDashboardPlaceholder(box, placeholder) ? string.Empty : (box.Text ?? string.Empty);
            if (focusKey == "table")
                _dashboardTableSearchText = text;
            else
                _dashboardSearchText = text;

            _dashboardClientsPage = 1;
            _dashboardSearchTimer.Stop();
            _dashboardSearchTimer.Start();
        }

        /// <summary>Restores the active dashboard search box after a debounced dashboard rebuild.</summary>
        private void RestoreDashboardSearchFocus()
        {
            if (string.IsNullOrWhiteSpace(_dashboardSearchFocus) || !IsHandleCreated || IsDisposed)
                return;

            SafeClientBeginInvoke(() =>
            {
                TextBox box = _dashboardSearchFocus == "table" ? _dashboardTableSearch : _dashboardSearch;
                if (box == null || box.IsDisposed)
                    return;

                box.Focus();
                int start = Math.Max(0, Math.Min(_dashboardSearchSelectionStart, box.TextLength));
                box.SelectionStart = start;
                box.SelectionLength = 0;
            }, "ClientManagementForm.RestoreDashboardSearchFocus");
        }

        /// <summary>Queues UI work only while the client page still has a valid window handle.</summary>
        private void SafeClientBeginInvoke(Action action, string source)
        {
            if (action == null || IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (!IsDisposed)
                        action();
                }));
            }
            catch (InvalidOperationException ex) { AppLogger.LogError(source, ex); }
        }

        private bool IsDashboardPlaceholder(TextBox box, string placeholder)
        {
            if (box == null)
                return true;

            string text = (box.Text ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text)
                   || string.Equals(text, placeholder, StringComparison.OrdinalIgnoreCase);
        }

        private string GetClientStatus(B2BClient client)
        {
            if (client == null) return "Inactive";
            string stage = client.RelationshipStage ?? string.Empty;
            if (stage.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0) return "Blacklisted";
            if (!client.IsActive) return stage.IndexOf("hold", StringComparison.OrdinalIgnoreCase) >= 0 ? "On Hold" : "Inactive";
            if (stage.IndexOf("prospect", StringComparison.OrdinalIgnoreCase) >= 0 || stage.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0) return "Prospect";
            return client.IsActive ? "Active" : "Inactive";
        }

        /// <summary>Returns the supported client lifecycle statuses shared by grids, cards, and editors.</summary>
        private static string[] ClientStatusOptions()
        {
            return new[] { "Active", "Prospect", "On Hold", "Inactive", "Blacklisted" };
        }

        private string GetClientType(B2BClient client)
        {
            string type = (client?.IndustryType ?? string.Empty).Trim().ToUpperInvariant();
            if (type.Length == 0) return "Other";
            if (type == "RESIDENTIAL" || type.Contains("RES") || type.Contains("HOME") || type.Contains("APARTMENT")) return "Residential";
            if (type == "GOVERNMENT" || type.Contains("GOV") || type.Contains("PUBLIC") || type.Contains("MUNICIPAL")) return "Government";
            if (type == "INDUSTRIAL" || type.Contains("IND") || type.Contains("MANUF") || type.Contains("PLANT") || type.Contains("FACTORY") || type.Contains("PHARMA")) return "Industrial";
            if (type == "COMMERCIAL" || type.Contains("COM") || type.Contains("BANK") || type.Contains("OFFICE") || type.Contains("HOTEL") || type.Contains("HOSPITAL") || type.Contains("HEALTH") || type.Contains("IT") || type.Contains("RETAIL")) return "Commercial";
            return "Other";
        }

        private Color StatusColor(string status)
        {
            if (status == "Active") return DS.Green600;
            if (status == "Prospect") return DS.Amber500;
            if (status == "On Hold") return DS.Amber600;
            if (status == "Blacklisted") return DS.Red500;
            return DS.Slate400;
        }

        private DateTime LastClientActivity(B2BClient client)
        {
            if (client == null) return DateTime.MinValue;
            DateTime last = client.CustomerSince == default(DateTime) ? DateTime.MinValue : client.CustomerSince;
            DateTime invoice = _dashboardInvoices.Where(i => i.ClientID == client.ClientID).Select(i => i.ModifiedDate ?? i.PaymentDate ?? i.InvoiceDate).DefaultIfEmpty(DateTime.MinValue).Max();
            DateTime quote = _dashboardQuotes.Where(q => q.ClientID == client.ClientID).Select(q => q.ModifiedDate ?? q.SubmittedDate ?? q.DueDate).DefaultIfEmpty(DateTime.MinValue).Max();
            DateTime contract = _dashboardContracts.Where(c => c.ClientID == client.ClientID).Select(c => c.ModifiedDate ?? c.StartDate).DefaultIfEmpty(DateTime.MinValue).Max();
            return new[] { last, invoice, quote, contract }.Max();
        }

        private IEnumerable<AMCContract> UpcomingClientRenewals()
        {
            DateTime today = DateTime.Today;
            return _dashboardContracts.Where(c => _clients.Any(cl => cl.ClientID == c.ClientID) && c.EndDate.Date >= today && c.EndDate.Date <= today.AddDays(60)).OrderBy(c => c.EndDate);
        }

        private Color RenewalColor(DateTime date)
        {
            int days = (date.Date - DateTime.Today).Days;
            if (days <= 7) return DS.Red600;
            if (days <= 15) return DS.Amber600;
            if (days <= 30) return DS.Amber500;
            return DS.Green600;
        }

        private string ClientName(int clientId)
        {
            B2BClient client = _clients.FirstOrDefault(c => c.ClientID == clientId);
            return Safe(client?.CompanyName, "Client #" + clientId);
        }

        private Panel DashboardCard(string title, string linkText, EventHandler linkClick)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, Size = new Size(320, 220), BackColor = Color.White, Margin = new Padding(0, 0, 10, 0) };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(Color.White))
                using (Pen pen = new Pen(DS.Border))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            card.Controls.Add(new Label { Text = title, Location = new Point(16, 12), Size = new Size(260, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            if (!string.IsNullOrWhiteSpace(linkText))
            {
                LinkLabel link = new LinkLabel { Text = linkText, Location = new Point(card.Width - 86, 14), Size = new Size(72, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, LinkColor = DS.Primary600, TextAlign = ContentAlignment.TopRight, Font = new Font("Segoe UI", 7.8f) };
                if (linkClick != null) link.Click += linkClick;
                card.Controls.Add(link);
            }
            return card;
        }

        private Button DashboardButton(string text, Color bg, Color fg, int width, bool outline)
        {
            Button button = new Button { Text = text, Size = new Size(width, 30), BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.Border;
            return button;
        }

        private Control DashboardStatCard(int width, string label, string value, string subLabel, Color iconBg, Color iconFg)
        {
            Panel card = new Panel { Size = new Size(width, 86), BackColor = Color.White, Margin = new Padding(0, 0, 12, 0) };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, DS.Border, 8);
            card.Controls.Add(new Label { Text = "□", Location = new Point(16, 22), Size = new Size(42, 42), BackColor = iconBg, ForeColor = iconFg, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15f, FontStyle.Bold) });
            card.Controls.Add(new Label { Text = label, Location = new Point(74, 16), Size = new Size(width - 92, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = value, Location = new Point(74, 36), Size = new Size(width - 92, 24), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = subLabel, Location = new Point(74, 62), Size = new Size(width - 92, 18), Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate500, AutoEllipsis = true });
            return card;
        }

        private Panel Donut(List<DashSlice> slices, string center, string subtitle, Point location, Size size)
        {
            Panel donut = new Panel { Location = location, Size = size, BackColor = Color.White };
            donut.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int total = Math.Max(1, slices.Sum(sliceItem => sliceItem.Value));
                Rectangle rect = new Rectangle(8, 8, donut.Width - 16, donut.Height - 16);
                float start = -90f;
                foreach (DashSlice slice in slices.Where(sliceItem => sliceItem.Value > 0))
                {
                    float sweep = 360f * slice.Value / total;
                    using (Pen pen = new Pen(slice.Color, 18f)) e.Graphics.DrawArc(pen, rect, start, sweep);
                    start += sweep;
                }
                if (slices.All(sliceItem => sliceItem.Value == 0))
                {
                    using (Pen pen = new Pen(DS.Slate100, 18f)) e.Graphics.DrawArc(pen, rect, 0, 360);
                }
                using (SolidBrush brush = new SolidBrush(Color.White)) e.Graphics.FillEllipse(brush, new Rectangle(34, 34, donut.Width - 68, donut.Height - 68));
                TextRenderer.DrawText(e.Graphics, center, new Font("Segoe UI", 11f, FontStyle.Bold), new Rectangle(20, 48, donut.Width - 40, 22), DS.Slate900, TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(e.Graphics, subtitle, new Font("Segoe UI", 7f), new Rectangle(20, 70, donut.Width - 40, 18), DS.Slate500, TextFormatFlags.HorizontalCenter);
            };
            return donut;
        }

        private void AddLegend(Panel card, List<DashSlice> slices, int x, int y, int total)
        {
            foreach (DashSlice slice in slices)
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(x, y), Size = new Size(16, 16), ForeColor = slice.Color });
                card.Controls.Add(new Label { Text = slice.Name, Location = new Point(x + 18, y), Size = new Size(112, 16), Font = new Font("Segoe UI", 7.4f), ForeColor = DS.Slate800, AutoEllipsis = true });
                card.Controls.Add(new Label { Text = slice.Value == 0 ? "-" : slice.Value.ToString(), Location = new Point(card.Width - 52, y), Size = new Size(30, 16), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.4f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleRight });
                y += 28;
            }
        }

        private Label TableLabel(string text, bool header, Color color)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", header ? 7.6f : 7.4f, header ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(4, 0, 4, 0), BackColor = header ? DS.Slate50 : Color.White };
        }

        /// <summary>Builds stable All Clients grid columns once so search can update rows without recreating the textbox.</summary>
        private void ConfigureDashboardClientsGrid()
        {
            if (_dashboardClientsGrid == null)
                return;

            _dashboardClientsGrid.Columns.Clear();
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ClientId", HeaderText = "ID", Visible = false });
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ClientName", HeaderText = "Client Name", FillWeight = 22 });
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", FillWeight = 10 });
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrimaryContact", HeaderText = "Primary Contact", FillWeight = 13 });
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Phone", HeaderText = "Phone", FillWeight = 10 });
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Email", HeaderText = "Email", FillWeight = 16 });
            DataGridViewComboBoxColumn statusColumn = new DataGridViewComboBoxColumn
            {
                Name = "Status",
                HeaderText = "Status",
                FillWeight = 8,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                DisplayStyleForCurrentCellOnly = false
            };
            statusColumn.Items.AddRange(ClientStatusOptions().Cast<object>().ToArray());
            _dashboardClientsGrid.Columns.Add(statusColumn);
            _dashboardClientsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastActivity", HeaderText = "Last Update", FillWeight = 9 });
            _dashboardClientsGrid.Columns.Add(new DataGridViewButtonColumn { Name = "View", HeaderText = "", Text = "View", UseColumnTextForButtonValue = true, FillWeight = 6 });
            _dashboardClientsGrid.Columns.Add(new DataGridViewButtonColumn { Name = "More", HeaderText = "", Text = "...", UseColumnTextForButtonValue = true, FillWeight = 4 });
            _dashboardClientsGrid.CellContentClick -= DashboardClientsGridCellContentClick;
            _dashboardClientsGrid.CellClick -= DashboardClientsGridCellClick;
            _dashboardClientsGrid.CellValueChanged -= DashboardClientsGridCellValueChanged;
            _dashboardClientsGrid.CurrentCellDirtyStateChanged -= DashboardClientsGridCurrentCellDirtyStateChanged;
            _dashboardClientsGrid.DataError -= DashboardClientsGridDataError;
            _dashboardClientsGrid.CellContentClick += DashboardClientsGridCellContentClick;
            _dashboardClientsGrid.CellClick += DashboardClientsGridCellClick;
            _dashboardClientsGrid.CellValueChanged += DashboardClientsGridCellValueChanged;
            _dashboardClientsGrid.CurrentCellDirtyStateChanged += DashboardClientsGridCurrentCellDirtyStateChanged;
            _dashboardClientsGrid.DataError += DashboardClientsGridDataError;
        }

        /// <summary>Refreshes All Clients grid rows in place after search or status filtering.</summary>
        private void PopulateDashboardClientsGrid()
        {
            if (_dashboardClientsGrid == null || _dashboardClientsGrid.IsDisposed)
                return;

            List<B2BClient> filtered = DashboardFilteredClients().ToList();
            int pageSize = Math.Max(1, _dashboardClientsPageSize);
            _dashboardClientsPage = PaginationState.NormalizePage(_dashboardClientsPage, filtered.Count, pageSize);
            List<B2BClient> page = filtered.Skip((_dashboardClientsPage - 1) * pageSize).Take(pageSize).ToList();
            _dashboardClientsGrid.SuspendLayout();
            _bindingDashboardClientStatus = true;
            try
            {
                _dashboardClientsGrid.Rows.Clear();
                foreach (B2BClient client in page)
                {
                    string status = GetClientStatus(client);
                    DateTime last = LastClientActivity(client);
                    int rowIndex = _dashboardClientsGrid.Rows.Add(
                        client.ClientID,
                        Safe(client.CompanyName, "Client"),
                        GetClientType(client),
                        Safe(client.PrimaryContact, "-"),
                        Safe(client.Phone, "-"),
                        Safe(client.Email, "-"),
                        status,
                        last == DateTime.MinValue ? "-" : last.ToString("dd MMM yyyy"));
                    _dashboardClientsGrid.Rows[rowIndex].Tag = client.ClientID;
                    _dashboardClientsGrid.Rows[rowIndex].Cells["Status"].Style.ForeColor = StatusColor(status);
                    _dashboardClientsGrid.Rows[rowIndex].Cells["Status"].ToolTipText = "Click to change client status";
                }
            }
            finally
            {
                _bindingDashboardClientStatus = false;
                _dashboardClientsGrid.ResumeLayout();
            }

            if (_dashboardClientsShowingLabel != null && !_dashboardClientsShowingLabel.IsDisposed)
            {
                string suffix = string.IsNullOrWhiteSpace(DashboardTableSearchText()) && (_dashboardStatus ?? "All Status") == "All Status"
                    ? "entries"
                    : "matching entries";
                _dashboardClientsShowingLabel.Text = filtered.Count + " " + suffix;
            }
            if (_dashboardClientsPager != null && !_dashboardClientsPager.IsDisposed)
                _dashboardClientsPager.SetState(_dashboardClientsPage, filtered.Count, pageSize);
        }

        /// <summary>Handles All Clients grid actions without forcing a dashboard rebuild.</summary>
        private void DashboardClientsGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _dashboardClientsGrid == null)
                return;

            int clientId = Convert.ToInt32(_dashboardClientsGrid.Rows[e.RowIndex].Cells["ClientId"].Value);
            B2BClient client = _clients.FirstOrDefault(c => c.ClientID == clientId);
            if (client == null)
                return;

            string columnName = _dashboardClientsGrid.Columns[e.ColumnIndex].Name;
            if (columnName == "View")
            {
                OnOpenClientDetail?.Invoke(client.ClientID);
            }
            else if (columnName == "More")
            {
                ShowClientDashboardRowMenu(client, _dashboardClientsGrid);
            }
        }

        /// <summary>Opens the status dropdown immediately when a client status cell is clicked.</summary>
        private void DashboardClientsGridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _dashboardClientsGrid == null || e.ColumnIndex < 0)
                return;

            if (_dashboardClientsGrid.Columns[e.ColumnIndex].Name != "Status")
                return;

            _dashboardClientsGrid.BeginEdit(true);
            ComboBox editor = _dashboardClientsGrid.EditingControl as ComboBox;
            if (editor != null)
                editor.DroppedDown = true;
        }

        /// <summary>Commits client status dropdown changes as soon as a value is selected.</summary>
        private void DashboardClientsGridCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_dashboardClientsGrid != null && _dashboardClientsGrid.IsCurrentCellDirty)
                _dashboardClientsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        /// <summary>Persists status dropdown changes from the All Clients dashboard grid.</summary>
        private void DashboardClientsGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_bindingDashboardClientStatus || e.RowIndex < 0 || _dashboardClientsGrid == null || e.ColumnIndex < 0)
                return;

            if (_dashboardClientsGrid.Columns[e.ColumnIndex].Name != "Status")
                return;

            DataGridViewRow row = _dashboardClientsGrid.Rows[e.RowIndex];
            int clientId = Convert.ToInt32(row.Cells["ClientId"].Value);
            B2BClient client = _clients.FirstOrDefault(c => c.ClientID == clientId);
            if (client == null)
                return;

            SetClientLifecycleStatus(client, Convert.ToString(row.Cells["Status"].Value));
        }

        /// <summary>Suppresses transient combo-box parse errors while dashboard rows are being rebound.</summary>
        private void DashboardClientsGridDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
        }

        private void AddClientRow(TableLayoutPanel table, B2BClient client)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            int row = table.RowCount++;
            string status = GetClientStatus(client);
            table.Controls.Add(new CheckBox { Dock = DockStyle.Fill, BackColor = Color.White }, 0, row);
            LinkLabel name = new LinkLabel { Text = Safe(client.CompanyName, "Client"), Dock = DockStyle.Fill, LinkColor = DS.Primary700, Font = new Font("Segoe UI", 7.6f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, LinkBehavior = LinkBehavior.HoverUnderline };
            name.Click += (s, e) => OpenClientEditor(client, "Edit Client");
            table.Controls.Add(name, 1, row);
            table.Controls.Add(TableLabel(GetClientType(client), false, DS.Slate800), 2, row);
            table.Controls.Add(TableLabel(Safe(client.PrimaryContact, "-"), false, DS.Slate800), 3, row);
            table.Controls.Add(TableLabel(Safe(client.Phone, "-"), false, DS.Slate800), 4, row);
            table.Controls.Add(TableLabel(Safe(client.Email, "-"), false, DS.Slate800), 5, row);
            table.Controls.Add(MakeClientStatusDropdown(client, status), 6, row);
            DateTime last = LastClientActivity(client);
            table.Controls.Add(TableLabel(last == DateTime.MinValue ? "-" : last.ToString("dd MMM yyyy"), false, DS.Slate500), 7, row);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, WrapContents = false, Padding = new Padding(0, 4, 0, 0), Margin = Padding.Empty };
            Button view = DashboardActionIconButton("View client", false);
            view.Click += (s, e) => OnOpenClientDetail?.Invoke(client.ClientID);
            Button menu = DashboardActionIconButton("More actions", true);
            menu.Click += (s, e) => ShowClientDashboardRowMenu(client, menu);
            actions.Controls.Add(view);
            actions.Controls.Add(menu);
            table.Controls.Add(actions, 8, row);
        }

        private Button DashboardActionIconButton(string title, bool dots)
        {
            Button button = new Button
            {
                Size = new Size(26, 24),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = title,
                AccessibleDescription = title,
                Margin = new Padding(0, 0, 4, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = DS.Slate100;
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color color = DS.Slate600;
                using (Pen pen = new Pen(color, 1.4f))
                using (SolidBrush brush = new SolidBrush(color))
                {
                    if (dots)
                    {
                        int cx = button.Width / 2;
                        for (int i = 0; i < 3; i++)
                            e.Graphics.FillEllipse(brush, cx - 2, 6 + (i * 6), 4, 4);
                    }
                    else
                    {
                        Rectangle eye = new Rectangle(5, 7, button.Width - 10, 10);
                        Point left = new Point(eye.Left, eye.Top + eye.Height / 2);
                        Point right = new Point(eye.Right, eye.Top + eye.Height / 2);
                        e.Graphics.DrawBezier(pen, left, new Point(eye.Left + 5, eye.Top), new Point(eye.Right - 5, eye.Top), right);
                        e.Graphics.DrawBezier(pen, left, new Point(eye.Left + 5, eye.Bottom), new Point(eye.Right - 5, eye.Bottom), right);
                        e.Graphics.FillEllipse(brush, eye.Left + eye.Width / 2 - 2, eye.Top + eye.Height / 2 - 2, 4, 4);
                    }
                }
            };
            return button;
        }

        private void AddEmptyState(Panel card, string title, string subtitle, int y)
        {
            Label icon = new Label { Text = "□", Location = new Point((card.Width - 58) / 2, y), Size = new Size(58, 48), Anchor = AnchorStyles.Top, Font = new Font("Segoe UI", 22f, FontStyle.Bold), ForeColor = DS.Slate300, TextAlign = ContentAlignment.MiddleCenter };
            Label head = new Label { Text = title, Location = new Point(24, y + 56), Size = new Size(card.Width - 48, 22), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter };
            Label sub = new Label { Text = subtitle, Location = new Point(32, y + 80), Size = new Size(card.Width - 64, 36), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", 7.8f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter };
            card.Controls.AddRange(new Control[] { icon, head, sub });
        }

        private Panel BuildSessionUserPanel()
        {
            AppUserDto user = SessionManager.CurrentUser;
            string name = !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName : (!string.IsNullOrWhiteSpace(user?.Username) ? user.Username : Environment.UserName);
            string role = !string.IsNullOrWhiteSpace(user?.RoleName) ? user.RoleName : "User";
            Panel panel = new Panel { Size = new Size(150, 38), BackColor = DS.BgPage };
            string initials = Initials(name);
            panel.Controls.Add(new Label { Text = initials, Location = new Point(0, 4), Size = new Size(30, 30), BackColor = DS.Primary50, ForeColor = DS.Primary600, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 7.8f, FontStyle.Bold) });
            panel.Controls.Add(new Label { Text = name, Location = new Point(38, 1), Size = new Size(92, 17), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = role, Location = new Point(38, 18), Size = new Size(84, 15), Font = new Font("Segoe UI", 7.2f), ForeColor = DS.Slate500, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = "v", Location = new Point(132, 8), Size = new Size(14, 18), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private void ResizeClientCards()
        {
            int width = ClientCardWidth();
            foreach (Control control in _clientList.Controls)
                control.Width = width;
        }

        private void OpenClientEditor(B2BClient client, string title)
        {
            _showDashboard = false;
            BuildShell();
            _clients.Clear();
            _clients.AddRange(_clientService.GetAllClientsIncludingInactive().OrderBy(c => c.CompanyName));
            RenderClientCards();
            if (client == null)
                ShowClientEditor(null, title);
            else
            {
                B2BClient actual = _clients.FirstOrDefault(c => c.ClientID == client.ClientID) ?? client;
                SelectClient(actual.ClientID);
                ShowClientEditor(actual, title);
            }
        }

        private void ShowClientDashboardRowMenu(B2BClient client, Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("View", null, (s, e) => OnOpenClientDetail?.Invoke(client.ClientID));
            menu.Items.Add("Edit", null, (s, e) => OpenClientEditor(client, "Edit Client"));
            menu.Items.Add("Add Contact", null, (s, e) => ShowActionModal("Add Contact", "Add a contact for " + Safe(client.CompanyName, "client"), "Contact name", Safe(client.PrimaryContact, "")));
            ToolStripMenuItem contractItem = new ToolStripMenuItem("New Contract", null, (s, e) => OnNavigate?.Invoke(2)) { Enabled = client.IsActive };
            ToolStripMenuItem quoteItem = new ToolStripMenuItem("New Quote", null, (s, e) => OnNavigate?.Invoke(6)) { Enabled = client.IsActive };
            menu.Items.Add(contractItem);
            menu.Items.Add(quoteItem);
            ToolStripMenuItem statusMenu = new ToolStripMenuItem("Change Status");
            foreach (string status in ClientStatusOptions())
            {
                ToolStripMenuItem statusItem = new ToolStripMenuItem(status, null, (s, e) => SetClientLifecycleStatus(client, ((ToolStripMenuItem)s).Text));
                statusItem.Checked = string.Equals(GetClientStatus(client), status, StringComparison.OrdinalIgnoreCase);
                statusMenu.DropDownItems.Add(statusItem);
            }
            menu.Items.Add(statusMenu);
            menu.Items.Add(client.IsActive ? "Mark Inactive" : "Activate Client", null, (s, e) => SetClientActiveStatus(client, !client.IsActive));
            menu.Items.Add("Put On Hold", null, (s, e) => SetClientLifecycleStatus(client, "On Hold", false));
            RecordDeletionUi.AddDeleteMenuItem(menu, async (s, e) => await DeleteClientAsync(client));
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        /// <summary>Shows a compact lifecycle menu from a client status badge or status bar.</summary>
        private void ShowClientStatusMenu(B2BClient client, Control anchor)
        {
            if (client == null || anchor == null || anchor.IsDisposed)
                return;

            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            foreach (string status in ClientStatusOptions())
            {
                ToolStripMenuItem item = new ToolStripMenuItem(status, null, (s, e) => SetClientLifecycleStatus(client, ((ToolStripMenuItem)s).Text));
                item.Checked = string.Equals(GetClientStatus(client), status, StringComparison.OrdinalIgnoreCase);
                menu.Items.Add(item);
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        /// <summary>Creates the client status dropdown used in dashboard table rows.</summary>
        private ComboBox MakeClientStatusDropdown(B2BClient client, string selectedStatus)
        {
            ComboBox box = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = DS.Slate900,
                Font = new Font("Segoe UI", 7.4f),
                Margin = new Padding(3, 4, 3, 4),
                Cursor = Cursors.Hand
            };
            box.Items.AddRange(ClientStatusOptions().Cast<object>().ToArray());
            int index = box.Items.IndexOf(string.IsNullOrWhiteSpace(selectedStatus) ? "Active" : selectedStatus);
            box.SelectedIndex = index >= 0 ? index : 0;
            box.SelectedIndexChanged += (s, e) => SetClientLifecycleStatus(client, Convert.ToString(box.SelectedItem));
            return box;
        }

        /// <summary>Updates a client's active flag and reloads every visible client surface.</summary>
        private void SetClientActiveStatus(B2BClient client, bool active)
        {
            SetClientLifecycleStatus(client, active ? "Active" : "Inactive", active);
        }

        /// <summary>Persists a client lifecycle status and refreshes dashboard/list caches.</summary>
        private void SetClientLifecycleStatus(B2BClient client, string relationshipStage, bool active)
        {
            SetClientLifecycleStatus(client, relationshipStage);
        }

        /// <summary>Persists a client lifecycle status and refreshes every visible client surface.</summary>
        private void SetClientLifecycleStatus(B2BClient client, string relationshipStage)
        {
            if (client == null || client.ClientID <= 0)
                return;

            try
            {
                string status = string.IsNullOrWhiteSpace(relationshipStage) ? "Active" : relationshipStage.Trim();
                ApplyClientLifecycleLocal(client, status);
                _clientService.UpdateLifecycleStatus(client.ClientID, status);
                AppDataCache.RemovePrefix("clients:");
                ShowToast(Safe(client.CompanyName, "Client") + " is now " + GetClientStatus(client) + ".");
                _ = LoadClientsAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Updating client status", ex);
            }
        }

        /// <summary>Applies lifecycle status semantics to an in-memory client row.</summary>
        private static void ApplyClientLifecycleLocal(B2BClient client, string status)
        {
            if (client == null)
                return;

            string value = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
            client.RelationshipStage = value;
            client.IsActive = value == "Active" || value == "Prospect";
        }

        private async Task DeleteClientAsync(B2BClient client)
        {
            if (client == null || client.ClientID <= 0)
                return;

            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Client",
                Safe(client.CompanyName, "selected client"),
                "The client will be marked inactive. Existing jobs, invoices, payments, contracts, and sites remain preserved.");
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                await Task.Run(() => _clientService.DeleteClient(client.ClientID));
                if (_selectedClient != null && _selectedClient.ClientID == client.ClientID)
                    _selectedClient = null;
                _clients.RemoveAll(c => c.ClientID == client.ClientID);
                RenderClientsDashboard();
                ShowToast("Client deleted from active list.");
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Deleting client", ex);
            }
        }

        private void HandleClientQuickAction(string action)
        {
            if (action == "Add Client")
                OpenClientEditor(null, "New Client");
            else if (action == "Create Contract")
                OnNavigate?.Invoke(2);
            else if (action == "New Quote")
                OnNavigate?.Invoke(6);
            else if (action == "Send Message")
                ShowActionModal("Send Message", "Prepare a client communication.", "Message", "Hello, sharing an update from ServoERP.");
            else if (action == "Schedule Service")
                ShowActionModal("Schedule Service", "Schedule a service activity.", "Service title", "Preventive maintenance visit");
            else
                ShowActionModal(action, "Select a client row to continue.", "Name", "");
        }

        private List<Tuple<string, Color>> BuildClientAlerts()
        {
            var alerts = new List<Tuple<string, Color>>();
            int overdueInvoices = _dashboardInvoices.Count(i => i.DueDate.Date < DateTime.Today && !string.Equals(i.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) && i.BalanceDue > 0);
            int expiring = _dashboardContracts.Count(c => c.EndDate.Date >= DateTime.Today && c.EndDate.Date <= DateTime.Today.AddDays(30));
            int newClients = _clients.Count(c => c.CustomerSince.Date >= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            int pendingQuotes = _dashboardQuotes.Count(q => string.Equals(q.Status, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(q.Status, "Sent", StringComparison.OrdinalIgnoreCase) || string.Equals(q.Status, "Submitted", StringComparison.OrdinalIgnoreCase));
            if (overdueInvoices > 0) alerts.Add(Tuple.Create(overdueInvoices + " invoice" + (overdueInvoices == 1 ? " is" : "s are") + " overdue", DS.Red600));
            if (expiring > 0) alerts.Add(Tuple.Create(expiring + " contract" + (expiring == 1 ? "" : "s") + " expiring within 30 days", DS.Amber600));
            if (newClients > 0) alerts.Add(Tuple.Create(newClients + " new client" + (newClients == 1 ? "" : "s") + " added this month", DS.Primary600));
            if (pendingQuotes > 0) alerts.Add(Tuple.Create(pendingQuotes + " quote" + (pendingQuotes == 1 ? "" : "s") + " pending client response", DS.Green600));
            return alerts;
        }

        private string BuildClientAlertsText()
        {
            List<Tuple<string, Color>> alerts = BuildClientAlerts();
            return alerts.Count == 0 ? "No alerts at this time." : string.Join(Environment.NewLine, alerts.Select(a => a.Item1));
        }

        private string BuildRenewalText()
        {
            return string.Join(Environment.NewLine, UpcomingClientRenewals().Take(12).Select(c => ClientName(c.ClientID) + " - " + Safe(c.ContractType, "Contract") + " - " + c.EndDate.ToString("dd MMM yyyy")));
        }

        private void ExportDashboardClientsCsv()
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "CSV files (*.csv)|*.csv";
                    dialog.FileName = "Clients_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Client Name,Type,Primary Contact,Phone,Email,Address,Status,Last Update");
                    foreach (B2BClient c in DashboardFilteredClients())
                    {
                        DateTime last = LastClientActivity(c);
                        sb.AppendLine(string.Join(",", Csv(Safe(c.CompanyName, "")), Csv(GetClientType(c)), Csv(Safe(c.PrimaryContact, "")), Csv(Safe(c.Phone, "")), Csv(Safe(c.Email, "")), Csv(Safe(c.BillingAddress, "")), Csv(GetClientStatus(c)), Csv(last == DateTime.MinValue ? "-" : last.ToString("dd MMM yyyy"))));
                    }
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    ShowToast("Clients exported.");
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Export clients", ex);
            }
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private sealed class DashSlice
        {
            public DashSlice(string name, int value, Color color)
            {
                Name = name;
                Value = value;
                Color = color;
            }
            public string Name { get; private set; }
            public int Value { get; private set; }
            public Color Color { get; private set; }
        }

        private IEnumerable<B2BClient> FilteredClients()
        {
            string search = CurrentClientSearchText().ToLowerInvariant();
            IEnumerable<B2BClient> filtered = _clients;
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(c => string.Join(" ", c.CompanyName, c.Phone, c.Email, c.PrimaryContact, c.IndustryType, c.BillingAddress, c.City)
                    .ToLowerInvariant()
                    .Contains(search));
            }
            return filtered;
        }

        /// <summary>Returns the active classic client search text, excluding placeholder copy.</summary>
        private string CurrentClientSearchText()
        {
            if (_searchBox == null || _searchPlaceholderActive)
                return string.Empty;

            return (_searchBox.Text ?? string.Empty).Trim();
        }

        private void RenderPagination(int total)
        {
            if (_pagination == null)
                return;

            _pagination.SetState(_clientPage, total, _clientPageSize);
        }

        private void SelectClient(int clientId)
        {
            _selectedClient = _clients.FirstOrDefault(c => c.ClientID == clientId);
            if (_selectedClient == null)
                return;

            HighlightSelectedCard();
            RenderSelectedClient();
            LoadMetricsAsync(_selectedClient.ClientID);
        }

        private void HighlightSelectedCard()
        {
            foreach (KeyValuePair<int, ClientCardControl> item in _cards)
                item.Value.IsSelected = _selectedClient != null && item.Key == _selectedClient.ClientID;
        }

        private void RenderSelectedClient()
        {
            if (_selectedClient == null)
                return;

            _header.Bind(Initials(_selectedClient.CompanyName), Safe(_selectedClient.CompanyName, "Client"), Safe(_selectedClient.IndustryType, "HVAC Services"), Safe(_selectedClient.City, "Mumbai"), GetClientStatus(_selectedClient));
            _pipeline.Bind(NormalizeStage(_selectedClient.RelationshipStage, _selectedClient.IsActive));
            RenderKpis(_metricsCache.ContainsKey(_selectedClient.ClientID) ? _metricsCache[_selectedClient.ClientID] : ClientCommandMetrics.Loading(_selectedClient));
            RenderSelectedTab();
            _footer.Text = "Client created on " + SafeDate(_selectedClient.CustomerSince) + "  -  Last updated by Admin";
        }

        private void LoadMetricsAsync(int clientId)
        {
            int version = ++_metricsRequestVersion;
            Task.Run(() => LoadMetrics(clientId)).ContinueWith(task =>
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                SafeClientBeginInvoke(() =>
                {
                    if (IsDisposed || _selectedClient == null || _selectedClient.ClientID != clientId || version != _metricsRequestVersion)
                        return;
                    ClientCommandMetrics metrics = task.Status == TaskStatus.RanToCompletion ? task.Result : ClientCommandMetrics.Empty(_selectedClient);
                    _metricsCache[clientId] = metrics;
                    RenderKpis(metrics);
                    RenderSelectedTab();
                }, "ClientManagementForm.LoadMetricsAsync");
            });
        }

        private ClientCommandMetrics LoadMetrics(int clientId)
        {
            B2BClient client = _clients.FirstOrDefault(c => c.ClientID == clientId) ?? _selectedClient;
            var metrics = ClientCommandMetrics.Empty(client);
            try
            {
                List<Job> jobs = _jobService.GetAll().Where(j => j.ClientID == clientId).ToList();
                metrics.OpenJobs = jobs.Count(j => !IsClosedJob(j));
                metrics.JobValue = jobs.Sum(j => Math.Max(j.QuotedRevenue, Math.Max(j.ActualRevenue, j.Revenue)));
            }
            catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadMetrics.Jobs", ex); }

            try
            {
                List<AMCContract> contracts = _contractService.GetContractsByClient(clientId);
                metrics.ActiveContracts = contracts.Count(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase) && c.EndDate >= DateTime.Today);
                metrics.ContractValue = contracts.Where(c => string.Equals(c.ContractStatus, "Active", StringComparison.OrdinalIgnoreCase)).Sum(c => c.AnnualValue);
            }
            catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadMetrics.Contracts", ex); }

            try
            {
                List<Invoice> invoices = _invoiceService.GetInvoicesForClient(clientId);
                metrics.OutstandingAmount = invoices.Sum(i => i.BalanceDue);
                metrics.InvoiceValue = invoices.Sum(i => i.TotalAmount);
            }
            catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadMetrics.Invoices", ex); }

            metrics.OpenJobsText = metrics.OpenJobs.ToString();
            metrics.ActiveContractsText = metrics.ActiveContracts.ToString();
            return metrics;
        }

        private void RenderKpis(ClientCommandMetrics metrics)
        {
            _kpiGrid.Controls.Clear();
            AddKpi("Open Jobs", metrics.OpenJobsText, "View Jobs", "J", DS.Slate900, () => OnNavigate?.Invoke(15));
            AddKpi("Active Contracts", metrics.ActiveContractsText, "View Contracts", "C", DS.Primary700, () => OnNavigate?.Invoke(2));
            AddKpi("Outstanding Amount", FormatMoney(metrics.OutstandingAmount), "View Invoices", "₹", metrics.OutstandingAmount > 0 ? DS.Red600 : DS.Green600, () => OnNavigate?.Invoke(3));
            AddKpi("Pipeline Value", FormatMoney(metrics.TotalExpectedValue), "View Financials", "₹", DS.Primary600, () => _tabs.SelectedTab = _tabs.TabPages["Financials"] ?? _tabs.TabPages[0]);
        }

        private void AddKpi(string label, string value, string link, string icon, Color color, Action action)
        {
            var card = new KpiCardControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
            card.Bind(label, value, link, icon, color);
            card.ActionClicked += (s, e) => action?.Invoke();
            _kpiGrid.Controls.Add(card);
        }

        private void RenderSelectedTab()
        {
            if (_selectedClient == null || _tabs == null)
                return;
            TabPage page = _tabs.SelectedTab ?? _tabs.TabPages[0];
            page.Controls.Clear();
            if (page.Text == "Details")
                BuildDetailsTab(page);
            else if (page.Text == "Notes")
                BuildNotesTab(page);
            else if (page.Text == "Financials")
                BuildFinancialsTab(page);
            else
                BuildDocumentsTab(page);
        }

        private void BuildDetailsTab(TabPage page)
        {
            page.Controls.Add(InfoGrid(new[]
            {
                Pair("Company", Safe(_selectedClient.CompanyName, "-")),
                Pair("Contact", Safe(_selectedClient.PrimaryContact, "Facilities Manager")),
                Pair("Phone", Safe(_selectedClient.Phone, "+91 98200 41000")),
                Pair("Email", Safe(_selectedClient.Email, "facility@" + Slug(_selectedClient.CompanyName) + ".in")),
                Pair("GSTIN", Safe(_selectedClient.GSTNumber, "27ABCDE1234F1Z0")),
                Pair("Client Type", Safe(_selectedClient.IndustryType, "Commercial HVAC")),
                Pair("Address", Safe(_selectedClient.BillingAddress, "-")),
                Pair("City", Safe(_selectedClient.City, "Mumbai")),
                Pair("Lifecycle", NormalizeStage(_selectedClient.RelationshipStage, _selectedClient.IsActive))
            }));
        }

        private void BuildNotesTab(TabPage page)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18), BackColor = Color.White };
            panel.Controls.Add(NoteCard("Site Access", "Security gate pass required for weekend maintenance. Coordinate with facility desk before dispatch."));
            panel.Controls.Add(NoteCard("Billing", "Client prefers GST invoice copy by email and original hard copy with service report."));
            panel.Controls.Add(NoteCard("Equipment", "Critical assets include VRF outdoor units, AHUs, panel ACs, and cold-room compressors."));
            page.Controls.Add(panel);
        }

        private void BuildFinancialsTab(TabPage page)
        {
            ClientCommandMetrics metrics = CurrentMetrics();
            page.Controls.Add(InfoGrid(new[]
            {
                Pair("Outstanding Amount", FormatMoney(metrics.OutstandingAmount)),
                Pair("Invoice Value", FormatMoney(metrics.InvoiceValue)),
                Pair("Contract Value", FormatMoney(metrics.ContractValue)),
                Pair("Open Job Value", FormatMoney(metrics.JobValue)),
                Pair("Credit Limit", FormatMoney(_selectedClient.CreditLimit <= 0 ? 500000m : _selectedClient.CreditLimit)),
                Pair("Payment Terms", (_selectedClient.PaymentTermsDays <= 0 ? 30 : _selectedClient.PaymentTermsDays) + " days")
            }));
        }

        private void BuildDocumentsTab(TabPage page)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18), BackColor = Color.White };
            panel.Controls.Add(DocumentRow("GST Certificate", "Verified copy stored for invoicing."));
            panel.Controls.Add(DocumentRow("AMC Agreement", "Annual maintenance contract PDF."));
            panel.Controls.Add(DocumentRow("Service Reports", "Latest preventive maintenance reports."));
            page.Controls.Add(panel);
        }

        private Control InfoGrid(IEnumerable<Tuple<string, string>> values)
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 300, ColumnCount = 2, Padding = new Padding(20), BackColor = Color.White };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            int row = 0;
            foreach (Tuple<string, string> value in values)
            {
                int col = row % 2;
                int actualRow = row / 2;
                if (col == 0)
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
                Panel cell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
                cell.Controls.Add(new Label { Text = value.Item2, Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = DS.Slate900 });
                cell.Controls.Add(new Label { Text = value.Item1, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500 });
                grid.Controls.Add(cell, col, actualRow);
                row++;
            }
            return grid;
        }

        private void ShowClientEditor(B2BClient client, string title)
        {
            B2BClient target = client == null ? new B2BClient { IsActive = true, RelationshipStage = "Prospect", CustomerSince = DateTime.Today } : CloneClient(client);
            using (Form form = Modal(title, 560, 780))
            {
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                form.Height = Math.Min(form.Height, Math.Max(560, workArea.Height - 80));
                form.AutoScroll = true;
                form.AutoScrollMinSize = new Size(0, 742);
                form.Padding = new Padding(24);
                form.BackColor = Color.White;
                form.Controls.Add(new Label
                {
                    Text = title,
                    Location = new Point(24, 20),
                    Size = new Size(420, 30),
                    Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                    ForeColor = DS.Slate900
                });
                Button close = new Button
                {
                    Text = "X",
                    Location = new Point(504, 20),
                    Size = new Size(28, 28),
                    BackColor = Color.White,
                    ForeColor = DS.Slate900,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 12f)
                };
                close.FlatAppearance.BorderSize = 0;
                close.Click += (s, e) => form.Close();
                form.Controls.Add(close);
                form.Controls.Add(new Label
                {
                    Text = "CLIENT INFORMATION",
                    Location = new Point(24, 68),
                    Size = new Size(220, 18),
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    ForeColor = DS.Primary600
                });

                TextBox company = ModalIconField(form, "□", "Company name *", target.CompanyName, 96);
                TextBox contact = ModalIconField(form, "@", "Contact person", target.PrimaryContact, 156);
                TextBox phone = ModalIconField(form, "T", "Phone", target.Phone, 216);
                TextBox email = ModalIconField(form, "@", "Email", target.Email, 276);
                TextBox gst = ModalIconField(form, "%", "GSTIN", target.GSTNumber, 336);
                TextBox address = ModalIconField(form, "#", "Billing / registered address", target.BillingAddress, 396, true, 72);
                TextBox city = ModalIconField(form, "C", "City", target.City, 496);
                TextBox state = ModalIconField(form, "S", "State", "", 556);
                ComboBox status = ModalStatusField(form, "Status", GetClientStatus(target), 616);
                Button cancel = DS.GhostBtn("Cancel", 92, 36);
                cancel.Location = new Point(24, 680);
                cancel.Click += (s, e) => form.Close();
                Button dashboard = DS.GhostBtn("Client Dashboard", 148, 36);
                dashboard.Location = new Point(126, 680);
                dashboard.Click += (s, e) =>
                {
                    form.Close();
                    ShowDashboardFromNavigation();
                };
                Button save = PrimaryButton("Save", 36);
                save.Width = 112;
                save.Location = new Point(420, 680);
                save.Click += async (s, e) =>
                {
                    string gstValue = gst.Text.Trim().ToUpperInvariant();
                    target.CompanyName = CleanClientInput(company);
                    target.PrimaryContact = CleanClientInput(contact);
                    target.Phone = CleanClientInput(phone);
                    target.Email = CleanClientInput(email);
                    target.GSTNumber = gstValue;
                    target.BillingAddress = CleanClientInput(address);
                    target.City = CleanClientInput(city);
                    target.GeocodeAddress = BuildClientGeocodeAddress(target.BillingAddress, target.City, CleanClientInput(state));
                    ApplyClientEditorStatus(target, Convert.ToString(status.SelectedItem));
                    if (string.IsNullOrWhiteSpace(target.IndustryType)) target.IndustryType = "Commercial HVAC";
                    var validation = new B2BClientValidator().Validate(target);
                    if (!validation.IsValid)
                    {
                        MessageBox.Show(form, ValidationMessageFormatter.ToMessage(validation), BrandingService.WindowTitle("Clients"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        company.Focus();
                        return;
                    }
                    B2BClient saveTarget = target;
                    save.Enabled = false;
                    cancel.Enabled = false;
                    dashboard.Enabled = false;
                    close.Enabled = false;
                    save.Text = "Saving...";
                    try
                    {
                        target = await Task.Run(() =>
                        {
                            B2BClient clientToSave = saveTarget;
                            if (clientToSave.ClientID <= 0)
                                clientToSave.ClientID = _clientService.CreateClient(clientToSave);
                            else
                                _clientService.UpdateClient(clientToSave);
                            return clientToSave;
                        });
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError("ClientManagementForm.SaveClient", ex);
                        if (IsClientSaveValidationError(ex))
                        {
                            MessageBox.Show(form, BuildClientSaveValidationMessage(ex), BrandingService.WindowTitle("Clients"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            company.Focus();
                        }
                        else
                        {
                            AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Saving client", ex);
                        }
                    }
                    finally
                    {
                        if (!form.IsDisposed)
                        {
                            save.Enabled = true;
                            cancel.Enabled = true;
                            dashboard.Enabled = true;
                            close.Enabled = true;
                            save.Text = "Save";
                        }
                    }
                };
                form.Controls.Add(cancel);
                form.Controls.Add(dashboard);
                form.Controls.Add(save);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshAfterClientEditorSave(target);
                    ShowToast(title + " saved. Next: add a site, contact, or service job.");
                }
            }
        }

        private void RefreshAfterClientEditorSave(B2BClient target)
        {
            if (target == null || target.ClientID <= 0)
                return;

            try
            {
                _clients.RemoveAll(c => c.ClientID == target.ClientID);
                _clients.Add(target);
                _clients.Sort((a, b) => string.Compare(Safe(a == null ? null : a.CompanyName, string.Empty), Safe(b == null ? null : b.CompanyName, string.Empty), StringComparison.OrdinalIgnoreCase));

                if (_showDashboard || _clientList == null || _clientList.IsDisposed || _tabs == null || _tabs.IsDisposed || _header == null || _header.IsDisposed)
                {
                    _dashboardClientsLoaded = true;
                    RenderClientsDashboard();
                    _ = LoadClientsAsync();
                    return;
                }

                RenderClientCards();
                SelectClient(target.ClientID);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientManagementForm.RefreshAfterClientEditorSave", ex);
                try
                {
                    ResetDeferredLoad();
                    _ = LoadClientsAsync();
                }
                catch { }
            }
        }

        private static string CleanClientInput(TextBox box)
        {
            return box == null || box.Text == null ? string.Empty : box.Text.Trim();
        }

        /// <summary>Returns true for client save validation failures that should be shown without the generic screen-error wrapper.</summary>
        private static bool IsClientSaveValidationError(Exception ex)
        {
            return ex is InvalidOperationException
                && !string.IsNullOrWhiteSpace(ex.Message)
                && ex.Message.IndexOf("Client validation failed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Builds a direct user-facing validation message for failed client saves.</summary>
        private static string BuildClientSaveValidationMessage(Exception ex)
        {
            if (ex == null || string.IsNullOrWhiteSpace(ex.Message))
                return "Client could not be saved. Check duplicate client details and try again.";

            return ex.Message.Trim();
        }

        private static string BuildClientGeocodeAddress(params string[] parts)
        {
            string value = string.Join(", ", (parts ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
            return value.Length <= 500 ? value : value.Substring(0, 500);
        }

        /// <summary>Applies the client editor lifecycle dropdown to the model before saving.</summary>
        private static void ApplyClientEditorStatus(B2BClient target, string status)
        {
            if (target == null)
                return;

            ApplyClientLifecycleLocal(target, status);
        }

        private TextBox ModalIconField(Form form, string icon, string label, string value, int y, bool multiline = false, int fieldHeight = 28)
        {
            int actualHeight = multiline ? Math.Max(56, fieldHeight) : 28;
            Panel iconBox = new Panel
            {
                Location = new Point(24, y + 11),
                Size = new Size(42, multiline ? Math.Min(64, actualHeight) : 42),
                BackColor = DS.Primary50
            };
            DS.Rounded(iconBox, 7);
            iconBox.Controls.Add(new Label
            {
                Text = icon,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DS.Primary600,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold)
            });
            form.Controls.Add(iconBox);

            form.Controls.Add(new Label
            {
                Text = label,
                Location = new Point(82, y),
                Size = new Size(400, 18),
                ForeColor = DS.Slate700,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            });

            TextBox box = new TextBox
            {
                Text = value ?? string.Empty,
                Location = new Point(82, y + 22),
                Size = new Size(420, actualHeight),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                Multiline = multiline,
                AcceptsReturn = multiline,
                ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
            };
            form.Controls.Add(box);
            return box;
        }

        /// <summary>Creates the client lifecycle status selector used by the client editor.</summary>
        private ComboBox ModalStatusField(Form form, string label, string selected, int y)
        {
            form.Controls.Add(new Label
            {
                Text = label,
                Location = new Point(82, y),
                Size = new Size(400, 18),
                ForeColor = DS.Slate700,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            });

            ComboBox box = new ComboBox
            {
                Location = new Point(82, y + 22),
                Size = new Size(420, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White,
                ForeColor = DS.Slate900
            };
            box.Items.AddRange(ClientStatusOptions().Cast<object>().ToArray());
            int index = box.Items.IndexOf(string.IsNullOrWhiteSpace(selected) ? "Active" : selected);
            box.SelectedIndex = index >= 0 ? index : 0;
            form.Controls.Add(box);
            return box;
        }

        private void ShowActionModal(string title, string message, string fieldLabel, string defaultValue)
        {
            using (Form form = Modal(title, 420, 250))
            {
                form.Controls.Add(new Label { Text = message, Location = new Point(24, 24), Size = new Size(350, 36), ForeColor = DS.Slate700 });
                TextBox box = ModalField(form, fieldLabel, defaultValue, 76);
                Button save = PrimaryButton("Create", 36);
                save.Location = new Point(284, 158);
                save.Click += (s, e) => { form.DialogResult = DialogResult.OK; form.Close(); };
                form.Controls.Add(save);
                if (form.ShowDialog(this) == DialogResult.OK)
                    ShowToast(title + " queued: " + box.Text);
            }
        }

        private void ShowDashboardListModal(string title, IEnumerable<string> lines)
        {
            using (Form form = Modal(title, 560, 420))
            {
                TextBox list = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Location = new Point(24, 24),
                    Size = new Size(form.ClientSize.Width - 64, form.ClientSize.Height - 100),
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 9f),
                    Text = string.Join(Environment.NewLine, (lines ?? Enumerable.Empty<string>()).Where(line => !string.IsNullOrWhiteSpace(line)).DefaultIfEmpty("No records to show."))
                };
                Button close = PrimaryButton("Close", 34);
                close.Width = 92;
                close.Location = new Point(form.ClientSize.Width - 124, form.ClientSize.Height - 64);
                close.Click += (s, e) => form.Close();
                form.Controls.Add(list);
                form.Controls.Add(close);
                form.ShowDialog(this);
            }
        }

        private void ShowMoreMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open full profile", null, (s, e) => OnOpenClientDetail?.Invoke(_selectedClient.ClientID));
            menu.Items.Add("Open WhatsApp Chat", null, (s, e) => ShowClientWhatsAppAction("General update"));
            menu.Items.Add("Copy WhatsApp message", null, (s, e) => ShowClientWhatsAppAction("General update"));
            menu.Items.Add("Generate invoice reminder message", null, (s, e) => ShowClientWhatsAppAction("Payment reminder"));
            menu.Items.Add("Generate quotation follow-up message", null, (s, e) => ShowClientWhatsAppAction("Quotation follow-up"));
            menu.Items.Add("Generate AMC renewal reminder", null, (s, e) => ShowClientWhatsAppAction("AMC renewal reminder"));
            menu.Items.Add("Generate job update message", null, (s, e) => ShowClientWhatsAppAction("Job update"));
            menu.Items.Add("Mark inactive", null, (s, e) => { ChangeStage("Inactive"); });
            menu.Items.Add("Refresh metrics", null, (s, e) => LoadMetricsAsync(_selectedClient.ClientID));
            menu.Show(_header, new Point(_header.Width - 100, 72));
        }

        private void ShowClientWhatsAppAction(string templateType)
        {
            if (_selectedClient == null)
                return;

            string name = Safe(_selectedClient.CompanyName, "Customer");
            string message = "Hi " + name + ",\r\n\r\nSharing an update from ServoERP.\r\n\r\nRegards,\r\nServoERP";
            string recordType = "Client";
            string linkedRecord = name;
            if (templateType.IndexOf("Payment", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recordType = "Invoice";
                linkedRecord = "Invoice reminder";
                message = "Hi " + name + ",\r\n\r\nThis is a reminder for your pending invoice. Please arrange payment when possible.\r\n\r\nRegards,\r\nServoERP";
            }
            else if (templateType.IndexOf("Quotation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recordType = "Quotation";
                linkedRecord = "Quotation follow-up";
                message = "Hi " + name + ",\r\n\r\nFollowing up on the quotation shared with you. Please review and confirm if we should proceed.\r\n\r\nRegards,\r\nServoERP";
            }
            else if (templateType.IndexOf("AMC", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recordType = "AMC";
                linkedRecord = "AMC renewal";
                message = "Hi " + name + ",\r\n\r\nYour AMC is due for renewal. Please confirm a suitable time to discuss renewal and service continuity.\r\n\r\nRegards,\r\nServoERP";
            }
            else if (templateType.IndexOf("Job", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recordType = "Job";
                linkedRecord = "Service job update";
                message = "Hi " + name + ",\r\n\r\nSharing an update on your service job. Our team will keep you posted on the next step.\r\n\r\nRegards,\r\nServoERP";
            }

            WhatsAppQuickActionDialog.ShowFor(this, new WhatsAppQuickActionContext
            {
                Module = "Clients",
                SourceId = _selectedClient.ClientID,
                ContactName = name,
                Phone = _selectedClient.Phone,
                TemplateType = templateType,
                Message = message,
                LinkedRecordType = recordType,
                LinkedRecord = linkedRecord,
                LinkedRecordId = _selectedClient.ClientID
            });
        }

        private void ChangeStage(string stage)
        {
            if (_selectedClient == null)
                return;
            ApplyClientLifecycleLocal(_selectedClient, stage);
            try { if (_selectedClient.ClientID > 0) _clientService.UpdateLifecycleStatus(_selectedClient.ClientID, stage); }
            catch (Exception ex) { AppLogger.LogError("ClientManagementForm.ChangeStage", ex); }
            _pipeline.Bind(stage);
            RenderClientCards();
            ShowToast("Lifecycle moved to " + stage);
        }

        private static bool IsClosedJob(Job job)
        {
            string status = (job.Status ?? job.PipelineStatus ?? string.Empty).ToLowerInvariant();
            return status.Contains("closed") || status.Contains("complete") || status.Contains("cancel");
        }

        private ClientCommandMetrics CurrentMetrics()
        {
            if (_selectedClient != null && _metricsCache.ContainsKey(_selectedClient.ClientID))
                return _metricsCache[_selectedClient.ClientID];
            return ClientCommandMetrics.Empty(_selectedClient);
        }

        private static int ProgressFor(B2BClient client)
        {
            int score = client.HealthScore > 0 ? client.HealthScore : (client.IsActive ? 72 : 32);
            return (int)Math.Round(52 * Math.Max(10, Math.Min(100, score)) / 100d);
        }

        private Button PrimaryButton(string text, int height)
        {
            Button button = new Button { Text = text, Height = height, BackColor = DS.Primary600, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = DS.Primary500;
            button.FlatAppearance.MouseDownBackColor = DS.Primary700;
            DS.Rounded(button, 7);
            return button;
        }

        private static Tuple<string, string> Pair(string label, string value) => Tuple.Create(label, value);

        private static string Safe(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        private string SelectedName() => _selectedClient == null ? "client" : Safe(_selectedClient.CompanyName, "client");
        private static string SafeDate(DateTime date) => date == default(DateTime) ? DateTime.Today.ToString("dd MMM yyyy") : date.ToString("dd MMM yyyy");
        private static string Initials(string name) => string.Join("", (name ?? "CL").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p.Substring(0, 1).ToUpperInvariant())).PadRight(2, 'C').Substring(0, 2);
        private static string Slug(string value) => new string((value ?? "client").ToLowerInvariant().Where(char.IsLetterOrDigit).Take(16).ToArray());
        private static string NormalizeStage(string stage, bool active) => active ? (string.IsNullOrWhiteSpace(stage) ? "Active AMC" : stage) : "Inactive";
        private static string FormatMoney(decimal value) => "Rs " + value.ToString("N0");
        private void ShowToast(string message)
        {
            MessageBox.Show(this, message, BrandingService.WindowTitle("Clients"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void DrawRoundedBorder(Graphics graphics, Rectangle bounds, Color color, int radius)
        {
            bounds.Width -= 1;
            bounds.Height -= 1;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(bounds, radius))
            using (Pen pen = new Pen(color))
                graphics.DrawPath(pen, path);
        }

        private static Label NoteCard(string title, string body)
        {
            return new Label { Text = title + Environment.NewLine + body, Width = 680, Height = 72, Padding = new Padding(12), BackColor = DS.Slate50, ForeColor = DS.Slate800, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 0, 0, 10) };
        }

        private static Label DocumentRow(string title, string body)
        {
            return new Label { Text = title + "    " + body, Width = 720, Height = 42, Padding = new Padding(12, 12, 12, 0), BackColor = Color.White, ForeColor = DS.Slate800, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 0, 0, 8), BorderStyle = BorderStyle.FixedSingle };
        }

        private Form Modal(string title, int width, int height)
        {
            return new Form { Text = BrandingService.WindowTitle(title), StartPosition = FormStartPosition.CenterParent, Width = width, Height = height, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, BackColor = Color.White, Font = Font };
        }

        private TextBox ModalField(Form form, string label, string value, int y)
        {
            form.Controls.Add(new Label { Text = label, Location = new Point(24, y), Size = new Size(360, 18), ForeColor = DS.Slate600, Font = new Font("Segoe UI", 8f, FontStyle.Bold) });
            TextBox box = new TextBox { Text = value ?? string.Empty, Location = new Point(24, y + 20), Size = new Size(form.ClientSize.Width - 56, 24), BorderStyle = BorderStyle.FixedSingle };
            form.Controls.Add(box);
            return box;
        }

        private static B2BClient CloneClient(B2BClient c)
        {
            return new B2BClient { ClientID = c.ClientID, CompanyName = c.CompanyName, IndustryType = c.IndustryType, PrimaryContact = c.PrimaryContact, SecondaryContact = c.SecondaryContact, Phone = c.Phone, Email = c.Email, GSTNumber = c.GSTNumber, PANNumber = c.PANNumber, PaymentTermsDays = c.PaymentTermsDays, CreditLimit = c.CreditLimit, BillingAddress = c.BillingAddress, City = c.City, RelationshipStage = c.RelationshipStage, Tags = c.Tags, HealthScore = c.HealthScore, Notes = c.Notes, CustomerSince = c.CustomerSince, IsActive = c.IsActive };
        }

        private sealed class ClientCommandMetrics
        {
            public int OpenJobs { get; set; }
            public int ActiveContracts { get; set; }
            public decimal OutstandingAmount { get; set; }
            public decimal JobValue { get; set; }
            public decimal ContractValue { get; set; }
            public decimal InvoiceValue { get; set; }
            public string OpenJobsText { get; set; }
            public string ActiveContractsText { get; set; }
            public decimal TotalExpectedValue => Math.Max(250000m, JobValue) + Math.Max(250000m, ContractValue) + Math.Max(75000m, InvoiceValue / 10m);

            public static ClientCommandMetrics Loading(B2BClient client) => new ClientCommandMetrics { OpenJobsText = "...", ActiveContractsText = "..." };
            public static ClientCommandMetrics Empty(B2BClient client)
            {
                return new ClientCommandMetrics { OpenJobsText = "0", ActiveContractsText = "0" };
            }
        }
    }
}


