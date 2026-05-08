using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class ClientManagementForm : DeferredPageControl
    {
        private const int PageSize = 7;
        private readonly ClientService _clientService = new ClientService();
        private readonly ContractService _contractService = new ContractService();
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly JobService _jobService = new JobService();

        private readonly List<B2BClient> _clients = new List<B2BClient>();
        private readonly Dictionary<int, ClientCardControl> _cards = new Dictionary<int, ClientCardControl>();
        private readonly Dictionary<int, ClientCommandMetrics> _metricsCache = new Dictionary<int, ClientCommandMetrics>();
        private readonly Dictionary<int, List<ActivityTimelineItem>> _activityCache = new Dictionary<int, List<ActivityTimelineItem>>();

        private Panel _leftPanel;
        private Panel _rightPanel;
        private FlowLayoutPanel _clientList;
        private TextBox _searchBox;
        private FlowLayoutPanel _pagination;
        private ClientHeaderControl _header;
        private LifecyclePipelineControl _pipeline;
        private TableLayoutPanel _kpiGrid;
        private TabControl _tabs;
        private ActivityTimelineControl _timeline;
        private ClientSummaryTableControl _summary;
        private Label _footer;

        private B2BClient _selectedClient;
        private int _pageIndex;
        private int _metricsRequestVersion;
        private int _activityRequestVersion;
        private bool _searchPlaceholderActive = true;

        public Action<int> OnNavigate { get; set; }
        public Action<int> OnOpenClientDetail { get; set; }

        public ClientManagementForm()
        {
            Dock = DockStyle.Fill;
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", 9f);
            BuildShell();
            RenderClientCards();
            EnableDeferredLoad(LoadClientsAsync, ex => ShowToast("Could not load clients: " + ex.Message));
        }

        public void SelectClientFromNavigation(int clientId)
        {
            ResetDeferredLoad();
            SelectClient(clientId);
            _ = LoadClientsAsync();
        }

        private void BuildShell()
        {
            Controls.Clear();
            _leftPanel = BuildLeftPanel();
            _rightPanel = BuildRightPanel();
            Controls.Add(_rightPanel);
            Controls.Add(_leftPanel);
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
                _pageIndex = 0;
                RenderClientCards();
            };
            searchWrap.Controls.Add(_searchBox);

            Button newClient = PrimaryButton("+ New Client", 42);
            newClient.Dock = DockStyle.Top;
            newClient.Click += (s, e) => ShowClientEditor(null, "New Client");

            _pagination = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = DS.BgPage, Padding = new Padding(0, 6, 0, 0) };
            _clientList = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = DS.BgPage, Padding = new Padding(0, 12, 6, 8) };
            _clientList.Resize += (s, e) => ResizeClientCards();

            panel.Controls.Add(_clientList);
            panel.Controls.Add(_pagination);
            panel.Controls.Add(newClient);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = DS.BgPage });
            panel.Controls.Add(searchWrap);
            panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = DS.BgPage });
            panel.Controls.Add(title);
            return panel;
        }

        private Panel BuildRightPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(16, 16, 18, 14), AutoScroll = false };

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage };
            content.Resize += (s, e) => LayoutRightContent(content);

            _header = new ClientHeaderControl { Location = new Point(0, 0) };
            _header.Dock = DockStyle.None;
            _header.Height = 94;
            _header.LogActivityClicked += (s, e) => ShowActivityModal();
            _header.AddJobClicked += (s, e) => ShowActionModal("Add Job", "Create HVAC job for " + SelectedName(), "Job title", "Preventive maintenance visit");
            _header.CreateInvoiceClicked += (s, e) => ShowActionModal("Create Invoice", "Create invoice for " + SelectedName(), "Invoice subject", "AMC service invoice");
            _header.EditProfileClicked += (s, e) => ShowClientEditor(_selectedClient, "Edit Profile");
            _header.MoreClicked += (s, e) => ShowMoreMenu();

            _pipeline = new LifecyclePipelineControl { Location = new Point(0, 110) };
            _pipeline.Dock = DockStyle.None;
            _pipeline.Height = 112;
            _pipeline.StageClicked += (s, stage) => ChangeStage(stage);

            _kpiGrid = new TableLayoutPanel { Location = new Point(0, 234), Height = 104, ColumnCount = 4, RowCount = 1, BackColor = DS.BgPage };
            for (int i = 0; i < 4; i++)
                _kpiGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _kpiGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _tabs = new TabControl { Location = new Point(0, 354), Height = 410, Font = new Font("Segoe UI", 9f), Appearance = TabAppearance.Normal };
            foreach (string tab in new[] { "Activity", "Details", "Notes", "Financials", "Documents" })
                _tabs.TabPages.Add(new TabPage(tab) { Name = tab, BackColor = Color.White });
            _tabs.SelectedIndexChanged += (s, e) => RenderSelectedTab();

            _footer = new Label { Location = new Point(0, 778), Height = 24, Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleLeft };

            content.Controls.Add(_footer);
            content.Controls.Add(_tabs);
            content.Controls.Add(_kpiGrid);
            content.Controls.Add(_pipeline);
            content.Controls.Add(_header);
            panel.Controls.Add(content);
            return panel;
        }

        private void LayoutRightContent(Panel content)
        {
            int width = Math.Max(760, content.Parent == null ? content.Width : content.Parent.ClientSize.Width - 18);
            content.Width = width;
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
            try
            {
                loaded = await Task.Run(() => _clientService.GetAllClientsIncludingInactive().OrderBy(c => c.CompanyName).ToList());
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ClientManagementForm.LoadClients", ex);
                try { loaded = await Task.Run(() => _clientService.GetAllClients().OrderBy(c => c.CompanyName).ToList()); }
                catch (Exception inner) { AppLogger.LogError("ClientManagementForm.LoadClients.Fallback", inner); }
            }

            if (IsDisposed)
                return;

            if (loaded == null || loaded.Count == 0)
            {
                _clients.Clear();
                _selectedClient = null;
                RenderClientCards();
                return;
            }

            int selectedId = _selectedClient == null ? loaded[0].ClientID : _selectedClient.ClientID;
            _clients.Clear();
            _clients.AddRange(loaded);
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
            int maxPage = Math.Max(0, (int)Math.Ceiling(filtered.Count / (double)PageSize) - 1);
            _pageIndex = Math.Max(0, Math.Min(_pageIndex, maxPage));
            foreach (B2BClient client in filtered.Skip(_pageIndex * PageSize).Take(PageSize))
            {
                ClientCardControl card = new ClientCardControl();
                card.Width = ClientCardWidth();
                card.Bind(client.ClientID, Initials(client.CompanyName), Safe(client.CompanyName, "Client"), Safe(client.IndustryType, "HVAC Services"), Safe(client.City, "Mumbai"), client.IsActive, ProgressFor(client));
                card.Click += (s, e) => SelectClient(((ClientCardControl)s).ClientId);
                _cards[client.ClientID] = card;
                _clientList.Controls.Add(card);
            }
            _clientList.ResumeLayout();
            RenderPagination(filtered.Count);
            HighlightSelectedCard();
        }

        private int ClientCardWidth()
        {
            int width = _clientList == null || _clientList.ClientSize.Width <= 0 ? 278 : _clientList.ClientSize.Width - 28;
            return Math.Max(238, width);
        }

        private void ResizeClientCards()
        {
            int width = ClientCardWidth();
            foreach (Control control in _clientList.Controls)
                control.Width = width;
        }

        private IEnumerable<B2BClient> FilteredClients()
        {
            string search = _searchBox == null || _searchPlaceholderActive ? string.Empty : (_searchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            IEnumerable<B2BClient> filtered = _clients;
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(c => string.Join(" ", c.CompanyName, c.Phone, c.Email, c.PrimaryContact, c.IndustryType, c.City)
                    .ToLowerInvariant()
                    .Contains(search));
            }
            return filtered;
        }

        private void RenderPagination(int total)
        {
            _pagination.Controls.Clear();
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            Button prev = PageButton("<", _pageIndex > 0);
            prev.Click += (s, e) => { if (_pageIndex > 0) { _pageIndex--; RenderClientCards(); } };
            _pagination.Controls.Add(prev);
            for (int i = 0; i < Math.Min(pages, 5); i++)
            {
                int page = i;
                Button btn = PageButton((i + 1).ToString(), true);
                btn.BackColor = page == _pageIndex ? DS.Primary600 : Color.White;
                btn.ForeColor = page == _pageIndex ? Color.White : DS.Slate800;
                btn.Click += (s, e) => { _pageIndex = page; RenderClientCards(); };
                _pagination.Controls.Add(btn);
            }
            if (pages > 5)
                _pagination.Controls.Add(new Label { Text = "... " + pages, Width = 48, Height = 28, TextAlign = ContentAlignment.MiddleCenter, ForeColor = DS.Slate600 });
            Button next = PageButton(">", _pageIndex < pages - 1);
            next.Click += (s, e) => { if (_pageIndex < pages - 1) { _pageIndex++; RenderClientCards(); } };
            _pagination.Controls.Add(next);
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

            _header.Bind(Initials(_selectedClient.CompanyName), Safe(_selectedClient.CompanyName, "Client"), Safe(_selectedClient.IndustryType, "HVAC Services"), Safe(_selectedClient.City, "Mumbai"), _selectedClient.IsActive);
            _pipeline.Bind(NormalizeStage(_selectedClient.RelationshipStage, _selectedClient.IsActive));
            RenderKpis(_metricsCache.ContainsKey(_selectedClient.ClientID) ? _metricsCache[_selectedClient.ClientID] : ClientCommandMetrics.Loading(_selectedClient));
            RenderSelectedTab();
            _footer.Text = "Client created on " + SafeDate(_selectedClient.CustomerSince) + "  -  Last updated by Admin";
            LoadActivitiesAsync(_selectedClient.ClientID);
        }

        private void LoadMetricsAsync(int clientId)
        {
            int version = ++_metricsRequestVersion;
            Task.Run(() => LoadMetrics(clientId)).ContinueWith(task =>
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed || _selectedClient == null || _selectedClient.ClientID != clientId || version != _metricsRequestVersion)
                        return;
                    ClientCommandMetrics metrics = task.Status == TaskStatus.RanToCompletion ? task.Result : ClientCommandMetrics.Empty(_selectedClient);
                    _metricsCache[clientId] = metrics;
                    RenderKpis(metrics);
                    RenderSelectedTab();
                }));
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

            try { metrics.ActivityScore = _clientService.ComputeHealthScore(clientId); }
            catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadMetrics.Score", ex); }
            if (metrics.ActivityScore <= 0)
                metrics.ActivityScore = client == null || client.HealthScore <= 0 ? 85 : client.HealthScore;
            metrics.OpenJobsText = metrics.OpenJobs.ToString();
            metrics.ActiveContractsText = metrics.ActiveContracts.ToString();
            metrics.ActivityScoreText = metrics.ActivityScore + "/100";
            return metrics;
        }

        private void RenderKpis(ClientCommandMetrics metrics)
        {
            _kpiGrid.Controls.Clear();
            AddKpi("Open Jobs", metrics.OpenJobsText, "View Jobs", "□", DS.Slate900, () => OnNavigate?.Invoke(15));
            AddKpi("Active Contracts", metrics.ActiveContractsText, "View Contracts", "◇", DS.Primary700, () => OnNavigate?.Invoke(2));
            AddKpi("Outstanding Amount", FormatMoney(metrics.OutstandingAmount), "View Invoices", "▤", metrics.OutstandingAmount > 0 ? DS.Red600 : DS.Green600, () => OnNavigate?.Invoke(3));
            AddKpi("Recent Activity Score", metrics.ActivityScoreText, "View Details", "↗", DS.Primary600, () => _tabs.SelectedTab = _tabs.TabPages["Activity"] ?? _tabs.TabPages[0]);
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
            if (page.Text == "Activity")
                BuildActivityTab(page);
            else if (page.Text == "Details")
                BuildDetailsTab(page);
            else if (page.Text == "Notes")
                BuildNotesTab(page);
            else if (page.Text == "Financials")
                BuildFinancialsTab(page);
            else
                BuildDocumentsTab(page);
        }

        private void BuildActivityTab(TabPage page)
        {
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            _timeline = new ActivityTimelineControl();
            _summary = new ClientSummaryTableControl();
            _timeline.Bind(BuildActivities());
            ClientCommandMetrics metrics = CurrentMetrics();
            _summary.Bind(BuildSummaryRows(metrics), FormatMoney(metrics.TotalExpectedValue));
            split.Controls.Add(_timeline, 0, 0);
            split.Controls.Add(_summary, 1, 0);
            page.Controls.Add(split);
        }

        private IEnumerable<ActivityTimelineItem> BuildActivities()
        {
            if (_selectedClient != null && _activityCache.ContainsKey(_selectedClient.ClientID))
                return _activityCache[_selectedClient.ClientID];

            return BuildSampleActivities();
        }

        private void LoadActivitiesAsync(int clientId)
        {
            if (clientId <= 0 || _activityCache.ContainsKey(clientId))
                return;

            int version = ++_activityRequestVersion;
            Task.Run(() =>
            {
                var items = new List<ActivityTimelineItem>();
                try
                {
                    foreach (ClientActivity activity in _clientService.GetActivities(clientId, "All").Take(8))
                    {
                        string type = activity.ActivityType;
                        items.Add(new ActivityTimelineItem
                        {
                            Icon = ActivityIcon(type),
                            IconBackColor = ActivityColor(type),
                            Title = Safe(activity.Title, type),
                            Description = Safe(activity.Detail, "Client activity logged."),
                            When = activity.CreatedAt.ToString("dd MMM yyyy  -  hh:mm tt"),
                            ActionText = "View Details",
                            Action = () => ShowToast("Opening activity details")
                        });
                    }
                }
                catch (Exception ex) { AppLogger.LogError("ClientManagementForm.LoadActivitiesAsync", ex); }
                return items.Count == 0 ? BuildSampleActivities().ToList() : items;
            }).ContinueWith(task =>
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed || _selectedClient == null || _selectedClient.ClientID != clientId || version != _activityRequestVersion)
                        return;
                    _activityCache[clientId] = task.Status == TaskStatus.RanToCompletion ? task.Result : BuildSampleActivities().ToList();
                    if (_tabs.SelectedTab != null && _tabs.SelectedTab.Text == "Activity")
                        RenderSelectedTab();
                }));
            });
        }

        private IEnumerable<ActivityTimelineItem> BuildSampleActivities()
        {
            var items = new List<ActivityTimelineItem>();
            items.Add(new ActivityTimelineItem { Icon = "SR", IconBackColor = DS.Primary600, Title = "Service Request in need", When = "15 May 2026  -  10:30 AM", Description = "Industrial HVAC inspection requested for " + SelectedName(), ActionText = "View Details", Action = () => ShowToast("Service request opened") });
            items.Add(new ActivityTimelineItem { Icon = "PR", IconBackColor = DS.Green600, Title = "Proposal in need", When = "19 Apr 2026  -  02:15 PM", Description = "AMC renewal proposal pending approval.", ActionText = "View Proposal", Action = () => OnNavigate?.Invoke(6) });
            items.Add(new ActivityTimelineItem { Icon = "IN", IconBackColor = DS.Amber600, Title = "Service Summary", When = "28 Mar 2026  -  03:20 PM", Description = "Preventive maintenance visit completed at Mumbai site.", ActionText = "View Summary", Action = () => ShowToast("Summary opened") });
            items.Add(new ActivityTimelineItem { Icon = "PM", IconBackColor = DS.Teal600, Title = "Payment follow-up", When = "13 Mar 2026  -  11:45 AM", Description = "Accounts team requested invoice ledger copy.", ActionText = "View Ledger", Action = () => OnNavigate?.Invoke(4) });
            return items;
        }

        private IEnumerable<ClientSummaryRow> BuildSummaryRows(ClientCommandMetrics metrics)
        {
            string client = SelectedName();
            return new[]
            {
                new ClientSummaryRow { Stage = "Prospect", Details = client, ExpectedValue = FormatMoney(Math.Max(250000m, metrics.JobValue)) },
                new ClientSummaryRow { Stage = "Qualified", Details = "Site survey complete", ExpectedValue = FormatMoney(Math.Max(180000m, metrics.JobValue / 2m)) },
                new ClientSummaryRow { Stage = "Active AMC", Details = "Active contracts", ExpectedValue = FormatMoney(Math.Max(250000m, metrics.ContractValue)) },
                new ClientSummaryRow { Stage = "Renewal", Details = "Renewal due pipeline", ExpectedValue = FormatMoney(Math.Max(125000m, metrics.ContractValue / 4m)) },
                new ClientSummaryRow { Stage = "Inactive", Details = "Reactivation value", ExpectedValue = FormatMoney(75000m) }
            };
        }

        private void BuildDetailsTab(TabPage page)
        {
            page.Controls.Add(InfoGrid(new[]
            {
                Pair("Company", Safe(_selectedClient.CompanyName, "-")),
                Pair("Contact", Safe(_selectedClient.PrimaryContact, "Facilities Manager")),
                Pair("Phone", Safe(_selectedClient.Phone, "+91 98200 41000")),
                Pair("Email", Safe(_selectedClient.Email, "facility@" + Slug(_selectedClient.CompanyName) + ".in")),
                Pair("GSTIN", Safe(_selectedClient.GSTNumber, "27ABCDE1234F1Z5")),
                Pair("Client Type", Safe(_selectedClient.IndustryType, "Commercial HVAC")),
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
            using (Form form = Modal(title, 500, 610))
            {
                TextBox company = ModalField(form, "Company name", target.CompanyName, 24);
                TextBox contact = ModalField(form, "Contact person", target.PrimaryContact, 84);
                TextBox phone = ModalField(form, "Phone", target.Phone, 144);
                TextBox email = ModalField(form, "Email", target.Email, 204);
                TextBox gst = ModalField(form, "GSTIN", target.GSTNumber, 264);
                TextBox address = ModalField(form, "Address", target.BillingAddress, 324);
                TextBox city = ModalField(form, "City", target.City, 384);
                TextBox state = ModalField(form, "State", "", 444);
                Button save = PrimaryButton("Save", 36);
                save.Location = new Point(360, 522);
                save.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(company.Text))
                    {
                        MessageBox.Show("Company name is required.", "Clients", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        company.Focus();
                        return;
                    }
                    string gstValue = gst.Text.Trim().ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(gstValue) && !System.Text.RegularExpressions.Regex.IsMatch(gstValue, @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$"))
                    {
                        MessageBox.Show("GSTIN format is invalid. Example: 27ABCDE1234F1Z5", "Clients", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        gst.Focus();
                        return;
                    }
                    target.CompanyName = company.Text.Trim();
                    target.PrimaryContact = contact.Text.Trim();
                    target.Phone = phone.Text.Trim();
                    target.Email = email.Text.Trim();
                    target.GSTNumber = gstValue;
                    target.BillingAddress = address.Text.Trim();
                    target.City = city.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(state.Text))
                        target.GeocodeAddress = string.Join(", ", new[] { target.BillingAddress, target.City, state.Text.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (string.IsNullOrWhiteSpace(target.IndustryType)) target.IndustryType = "Commercial HVAC";
                    try
                    {
                        if (target.ClientID <= 0)
                            target.ClientID = _clientService.CreateClient(target);
                        else
                            _clientService.UpdateClient(target);
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                    catch (Exception ex)
                    {
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Saving client", ex);
                    }
                };
                form.Controls.Add(save);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _clients.RemoveAll(c => c.ClientID == target.ClientID);
                    _clients.Add(target);
                    _clients.Sort((a, b) => string.Compare(a.CompanyName, b.CompanyName, StringComparison.OrdinalIgnoreCase));
                    RenderClientCards();
                    SelectClient(target.ClientID);
                    ShowToast(title + " saved");
                }
            }
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

        private void ShowActivityModal()
        {
            if (_selectedClient == null)
                return;
            using (Form form = Modal("Log Activity", 430, 300))
            {
                TextBox title = ModalField(form, "Activity title", "Client follow-up", 24);
                TextBox detail = ModalField(form, "Details", "Spoke with facility manager about AMC schedule.", 84);
                Button save = PrimaryButton("Log Activity", 38);
                save.Location = new Point(268, 202);
                save.Click += (s, e) =>
                {
                    try
                    {
                        _clientService.LogActivity(new ClientActivity { ClientId = _selectedClient.ClientID, ActivityType = "Call", Title = title.Text.Trim(), Detail = detail.Text.Trim(), CreatedAt = DateTime.Now });
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                    catch (Exception ex) { AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Clients"), "Logging activity", ex); }
                };
                form.Controls.Add(save);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RenderSelectedTab();
                    ShowToast("Activity logged");
                }
            }
        }

        private void ShowMoreMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open full profile", null, (s, e) => OnOpenClientDetail?.Invoke(_selectedClient.ClientID));
            menu.Items.Add("Mark inactive", null, (s, e) => { ChangeStage("Inactive"); });
            menu.Items.Add("Refresh metrics", null, (s, e) => LoadMetricsAsync(_selectedClient.ClientID));
            menu.Show(_header, new Point(_header.Width - 100, 72));
        }

        private void ChangeStage(string stage)
        {
            if (_selectedClient == null)
                return;
            _selectedClient.RelationshipStage = stage;
            _selectedClient.IsActive = stage != "Inactive";
            try { if (_selectedClient.ClientID > 0) _clientService.UpdateClient(_selectedClient); }
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

        private Button PageButton(string text, bool enabled)
        {
            Button button = new Button { Text = text, Width = 30, Height = 28, Enabled = enabled, BackColor = Color.White, ForeColor = DS.Slate800, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 6, 0), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = DS.Border;
            DS.Rounded(button, 6);
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
        private static string ActivityIcon(string type) => string.IsNullOrWhiteSpace(type) ? "AC" : type.Trim().Substring(0, Math.Min(2, type.Trim().Length)).ToUpperInvariant();
        private static Color ActivityColor(string type) => (type ?? "").ToLowerInvariant().Contains("invoice") ? DS.Amber600 : (type ?? "").ToLowerInvariant().Contains("job") ? DS.Green600 : DS.Primary600;

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
            public int ActivityScore { get; set; }
            public decimal JobValue { get; set; }
            public decimal ContractValue { get; set; }
            public decimal InvoiceValue { get; set; }
            public string OpenJobsText { get; set; }
            public string ActiveContractsText { get; set; }
            public string ActivityScoreText { get; set; }
            public decimal TotalExpectedValue => Math.Max(250000m, JobValue) + Math.Max(250000m, ContractValue) + Math.Max(75000m, InvoiceValue / 10m);

            public static ClientCommandMetrics Loading(B2BClient client) => new ClientCommandMetrics { OpenJobsText = "...", ActiveContractsText = "...", ActivityScoreText = "..." };
            public static ClientCommandMetrics Empty(B2BClient client)
            {
                int score = client == null || client.HealthScore <= 0 ? 85 : client.HealthScore;
                return new ClientCommandMetrics { OpenJobsText = "0", ActiveContractsText = "0", ActivityScore = score, ActivityScoreText = score + "/100" };
            }
        }
    }
}
