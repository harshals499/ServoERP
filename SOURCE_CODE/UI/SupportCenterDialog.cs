using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class SupportCenterDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly SupportCenterService _service = new SupportCenterService();
        private readonly Panel _content = new Panel();
        private readonly TextBox _search = new TextBox();
        private readonly Dictionary<string, Button> _nav = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private string _activeSection = "Dashboard";
        private bool _refreshingLayout;
        private bool _layoutRefreshQueued;
        private const int COMPACT_WIDTH = 720;

        public event EventHandler CloseRequested;

        public SupportCenterDialog()
        {
            Text = "Help & Support Center";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(440, 560);
            Size = new Size(1180, 760);
            BackColor = DS.BgPage;
            Font = DS.Body;
            DoubleBuffered = true;
            Resize += (s, e) => QueueRefreshActiveSection();
            Shown += (s, e) => RefreshActiveSection();

            BuildShell();
            ShowDashboard();
        }

        private void BuildShell()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                BackColor = DS.White,
                Padding = new Padding(24, 18, 24, 16)
            };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border, 1))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            Label icon = ModernIconSystem.Badge(ModernIconKind.Service, 44, DS.Primary50, DS.Primary600, 12);
            icon.Location = new Point(24, 24);
            header.Controls.Add(icon);

            Label title = new Label
            {
                Text = "Help & Support Center",
                Font = DS.H1,
                ForeColor = DS.Slate900,
                Location = new Point(82, 20),
                Size = new Size(420, 30),
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            header.Controls.Add(title);

            Label subtitle = new Label
            {
                Text = "Knowledge base, health checks, diagnostics, updates, and troubleshooting tools.",
                Font = DS.Body,
                ForeColor = DS.Slate500,
                Location = new Point(84, 54),
                Size = new Size(560, 34),
                AutoSize = false,
                AutoEllipsis = true
            };
            header.Controls.Add(subtitle);

            Panel searchWrap = new Panel
            {
                Height = 38,
                Width = 330,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = DS.White,
                Padding = new Padding(38, 8, 12, 6)
            };
            searchWrap.Location = new Point(ClientSize.Width - searchWrap.Width - 24, 26);
            searchWrap.Paint += PaintBorder(DS.Border, DS.RadiusMd);
            searchWrap.Resize += (s, e) => DS.Rounded(searchWrap, DS.RadiusMd);
            DS.Rounded(searchWrap, DS.RadiusMd);
            header.Resize += (s, e) => searchWrap.Left = header.ClientSize.Width - searchWrap.Width - 24;
            Label searchIcon = ModernIconSystem.Icon(ModernIconKind.Search, 16, DS.Slate400);
            searchIcon.SetBounds(12, 8, 18, 18);
            searchWrap.Controls.Add(searchIcon);
            _search.BorderStyle = BorderStyle.None;
            _search.Dock = DockStyle.Fill;
            _search.Font = DS.Body;
            _search.ForeColor = DS.Slate900;
            _search.BackColor = DS.White;
            _search.TextChanged += (s, e) =>
            {
                if (_activeSection == "Knowledge Base")
                    ShowKnowledgeBase();
            };
            searchWrap.Controls.Add(_search);
            header.Controls.Add(searchWrap);

            Button close = DS.GhostBtn("X", 38, 34);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(header.ClientSize.Width - close.Width - 18, 28);
            close.Click += (s, e) => RequestClose();
            header.Resize += (s, e) =>
            {
                close.Left = header.ClientSize.Width - close.Width - 18;
                bool narrowDrawer = header.ClientSize.Width < 820;
                searchWrap.Visible = !narrowDrawer;
                if (narrowDrawer)
                {
                    title.Width = Math.Max(220, close.Left - title.Left - 12);
                    subtitle.Width = Math.Max(220, close.Left - subtitle.Left - 12);
                }
                else
                {
                    searchWrap.Width = Math.Min(300, Math.Max(220, close.Left - 280));
                    searchWrap.Left = Math.Max(160, close.Left - searchWrap.Width - 10);
                    title.Width = Math.Max(170, searchWrap.Left - title.Left - 12);
                    subtitle.Width = Math.Max(220, header.ClientSize.Width - subtitle.Left - 24);
                }
            };
            header.Controls.Add(close);
            close.BringToFront();
            bool initialNarrowDrawer = header.ClientSize.Width < 820;
            searchWrap.Visible = !initialNarrowDrawer;
            if (initialNarrowDrawer)
            {
                title.Width = Math.Max(220, close.Left - title.Left - 12);
                subtitle.Width = Math.Max(220, close.Left - subtitle.Left - 12);
            }
            else
            {
                searchWrap.Left = Math.Max(160, close.Left - searchWrap.Width - 10);
                title.Width = Math.Max(170, searchWrap.Left - title.Left - 12);
                subtitle.Width = Math.Max(220, header.ClientSize.Width - subtitle.Left - 24);
            }

            Panel nav = new Panel
            {
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = DS.White,
                Padding = new Padding(24, 10, 24, 10)
            };
            nav.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border, 1))
                    e.Graphics.DrawLine(pen, 0, nav.Height - 1, nav.Width, nav.Height - 1);
            };

            FlowLayoutPanel navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent
            };
            nav.Controls.Add(navFlow);
            AddNav(navFlow, "Dashboard", ModernIconKind.Analytics, ShowDashboard);
            AddNav(navFlow, "Knowledge Base", ModernIconKind.Document, ShowKnowledgeBase);
            AddNav(navFlow, "System Health", ModernIconKind.Security, ShowSystemHealth);
            AddNav(navFlow, "App Information", ModernIconKind.Settings, ShowAppInformation);
            Action layoutNav = () =>
            {
                bool narrow = nav.ClientSize.Width < 760;
                nav.Height = narrow ? 104 : 58;
                navFlow.WrapContents = narrow;
                navFlow.AutoScroll = narrow;
                foreach (Control control in navFlow.Controls)
                {
                    Button button = control as Button;
                    if (button == null)
                        continue;

                    button.Width = narrow ? Math.Max(132, (navFlow.ClientSize.Width - 24) / 2) : GetNavButtonWidth(button.Text);
                    button.Margin = new Padding(0, 0, narrow ? 8 : 10, narrow ? 8 : 0);
                }
            };
            nav.Resize += (s, e) => layoutNav();
            layoutNav();

            _content.Dock = DockStyle.Fill;
            _content.BackColor = DS.BgPage;
            _content.Padding = new Padding(24);
            _content.AutoScroll = true;

            Controls.Add(_content);
            Controls.Add(nav);
            Controls.Add(header);
        }

        private void RequestClose()
        {
            EventHandler handler = CloseRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
            else
                Close();
        }

        private bool IsCompactSupportLayout()
        {
            int contentWidth = _content == null ? ClientSize.Width : _content.ClientSize.Width;
            return ClientSize.Width < COMPACT_WIDTH || contentWidth < COMPACT_WIDTH - 80;
        }

        private void QueueRefreshActiveSection()
        {
            if (_refreshingLayout || _layoutRefreshQueued || !IsHandleCreated)
                return;

            _layoutRefreshQueued = true;
            BeginInvoke((Action)(() =>
            {
                _layoutRefreshQueued = false;
                RefreshActiveSection();
            }));
        }

        private void RefreshActiveSection()
        {
            if (_refreshingLayout || _content == null || _content.IsDisposed)
                return;

            _refreshingLayout = true;
            try
            {
                if (string.Equals(_activeSection, "Knowledge Base", StringComparison.OrdinalIgnoreCase))
                    ShowKnowledgeBase();
                else if (string.Equals(_activeSection, "System Health", StringComparison.OrdinalIgnoreCase))
                    ShowSystemHealth();
                else if (string.Equals(_activeSection, "App Information", StringComparison.OrdinalIgnoreCase))
                    ShowAppInformation();
                else
                    ShowDashboard();
            }
            finally
            {
                _refreshingLayout = false;
            }
        }

        private void AddNav(FlowLayoutPanel flow, string text, ModernIconKind iconKind, Action action)
        {
            Button button = DS.GhostBtn(text, GetNavButtonWidth(text), 36);
            button.Margin = new Padding(0, 0, 10, 0);
            ModernIconSystem.AddButtonIcon(button, iconKind);
            button.Click += (s, e) => action();
            flow.Controls.Add(button);
            _nav[text] = button;
        }

        private static int GetNavButtonWidth(string text)
        {
            return text == "Knowledge Base" ? 164 : (text == "System Health" ? 154 : (text == "App Information" ? 166 : 126));
        }

        private void SetActive(string section)
        {
            _activeSection = section;
            foreach (var pair in _nav)
            {
                bool active = string.Equals(pair.Key, section, StringComparison.OrdinalIgnoreCase);
                pair.Value.UseVisualStyleBackColor = false;
                pair.Value.BackColor = active ? DS.Primary600 : DS.White;
                pair.Value.ForeColor = active ? DS.White : DS.Slate700;
                pair.Value.FlatAppearance.BorderSize = active ? 0 : 1;
                pair.Value.FlatAppearance.MouseOverBackColor = active ? DS.Primary600 : DS.BgCardHov;
                pair.Value.FlatAppearance.MouseDownBackColor = active ? DS.Primary700 : DS.Slate100;
                ModernIconSystem.AddButtonIcon(pair.Value, pair.Key == "Dashboard" ? ModernIconKind.Analytics : pair.Key == "Knowledge Base" ? ModernIconKind.Document : pair.Key == "System Health" ? ModernIconKind.Security : ModernIconKind.Settings);
                pair.Value.Invalidate();
            }
        }

        private void ClearContent(string section)
        {
            SetActive(section);
            _content.SuspendLayout();
            _content.Controls.Clear();
        }

        private void EndContent()
        {
            _content.ResumeLayout();
        }

        private void ShowDashboard()
        {
            ClearContent("Dashboard");
            bool compact = IsCompactSupportLayout();
            TableLayoutPanel grid = ContentGrid(compact ? 1 : 2, compact ? 8 : 4);
            for (int i = 0; i < (compact ? 8 : 4); i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));

            AddGridCard(grid, compact, DashboardCard("Knowledge Base", "Search guided tutorials for daily ServoERP workflows.", ModernIconKind.Document, DS.Primary600, ShowKnowledgeBase), 0, 0, 0);
            AddGridCard(grid, compact, DashboardCard("System Health", "Run safe checks and self-healing tools.", ModernIconKind.Security, DS.Green600, ShowSystemHealth), 1, 1, 0);
            AddGridCard(grid, compact, DashboardCard("Troubleshooting", "Resolve layout, cache, database, and app file issues.", ModernIconKind.Alert, DS.Amber600, ShowSystemHealth), 2, 0, 1);
            AddGridCard(grid, compact, DashboardCard("Export Diagnostics", "Create a safe support ZIP with logs and environment details.", ModernIconKind.Export, DS.Teal600, async () => await RunStandaloneTool("Export Diagnostics Package", () => _service.ExportDiagnosticsPackage())), 3, 1, 1);
            AddGridCard(grid, compact, DashboardCard("App Information", "Review version, modules, database state, and update host.", ModernIconKind.Settings, DS.Slate700, ShowAppInformation), 4, 0, 2);
            AddGridCard(grid, compact, DashboardCard("Check Updates", "Check whether a newer ServoERP release is available.", ModernIconKind.Refresh, DS.Primary700, async () => await CheckUpdates()), 5, 1, 2);
            AddGridCard(grid, compact, DashboardCard("Contact Support", "Prepare an escalation brief with issue summary, page name, version, database, and machine context.", ModernIconKind.Email, DS.Teal600, ShowContactSupportBrief), 6, 0, 3);
            AddGridCard(grid, compact, DashboardCard("Recent Errors", "Review recent application errors from local logs.", ModernIconKind.Activity, DS.Red600, () => MessageBox.Show(this, RecentErrorsText(), "Recent Errors", MessageBoxButtons.OK, MessageBoxIcon.Information)), 7, 1, 3);
            _content.Controls.Add(grid);
            EndContent();
        }

        private void ShowKnowledgeBase()
        {
            ClearContent("Knowledge Base");
            bool compact = IsCompactSupportLayout();
            TableLayoutPanel split = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = compact ? Math.Max(760, _content.ClientSize.Height - 40) : Math.Max(520, _content.ClientSize.Height - 40),
                BackColor = DS.BgPage,
                ColumnCount = compact ? 1 : 2,
                RowCount = compact ? 2 : 1
            };
            if (compact)
            {
                split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                split.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
                split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            }
            else
            {
                split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
                split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
                split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            }

            Panel listCard = CardPanel();
            listCard.Padding = new Padding(18);
            listCard.Dock = DockStyle.Fill;
            listCard.Margin = compact ? new Padding(0, 0, 0, 12) : new Padding(0, 0, 12, 0);
            split.Controls.Add(listCard, 0, 0);

            Label listTitle = LabelText("Knowledge Base", DS.H2, DS.Slate900);
            listTitle.Dock = DockStyle.Top;
            listTitle.Height = 32;
            listCard.Controls.Add(listTitle);

            ListBox list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = DS.Body,
                BackColor = DS.White,
                ForeColor = DS.Slate900,
                IntegralHeight = false
            };
            List<SupportArticle> articles = _service.SearchArticles(_search.Text);
            foreach (SupportArticle article in articles)
                list.Items.Add(article.Category + "  |  " + article.Title);
            listCard.Controls.Add(list);
            list.BringToFront();

            Panel detail = CardPanel();
            detail.Padding = new Padding(22);
            detail.Dock = DockStyle.Fill;
            detail.Margin = compact ? new Padding(0, 12, 0, 0) : new Padding(12, 0, 0, 0);
            split.Controls.Add(detail, compact ? 0 : 1, compact ? 1 : 0);

            Action<int> render = index =>
            {
                detail.Controls.Clear();
                if (articles.Count == 0)
                {
                    AddEmptyArticleState(detail);
                    return;
                }

                SupportArticle article = articles[Math.Max(0, Math.Min(index, articles.Count - 1))];
                RenderArticle(detail, article);
            };
            list.SelectedIndexChanged += (s, e) => render(list.SelectedIndex);
            if (list.Items.Count > 0)
                list.SelectedIndex = 0;
            else
                render(0);

            _content.Controls.Add(split);
            EndContent();
        }

        private void ShowSystemHealth()
        {
            ClearContent("System Health");
            bool compact = IsCompactSupportLayout();
            TableLayoutPanel grid = ContentGrid(compact ? 1 : 2, compact ? 15 : 8);
            for (int i = 0; i < (compact ? 15 : 8); i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

            AddGridCard(grid, compact, ToolCard("Check Database", "Validate SQL Server connection without changing data.", ModernIconKind.Security, () => _service.CheckDatabase()), 0, 0, 0);
            AddGridCard(grid, compact, ToolCard("Backup Database", "Create a manual backup through the existing backup service.", ModernIconKind.Backup, () => _service.BackupDatabase()), 1, 1, 0);
            AddGridCard(grid, compact, ToolCard("Clear Cache", "Clear in-memory module data cache so pages reload fresh data.", ModernIconKind.Refresh, () => _service.ClearCache()), 2, 0, 1);
            AddGridCard(grid, compact, ToolCard("Reset Layout", "Reset saved card/page layouts for the current user.", ModernIconKind.Preference, () => _service.ResetLayout()), 3, 1, 1);
            AddGridCard(grid, compact, ToolCard("Verify App Files", "Check that core application files are present.", ModernIconKind.Checklist, () => _service.VerifyAppFiles()), 4, 0, 2);
            AddGridCard(grid, compact, ToolCard("Repair Config", "Ensure required ServoERP folders exist and are writable.", ModernIconKind.Settings, () => _service.RepairConfig()), 5, 1, 2);
            AddGridCard(grid, compact, ToolCard("Export Diagnostics Package", "Export safe logs, version, health, and layout summaries.", ModernIconKind.Export, () => _service.ExportDiagnosticsPackage()), 6, 0, 3);
            AddGridCard(grid, compact, ToolCard("Client Server Setup Wizard", "Detect server IP, SQL target, and generate client connection ZIP.", ModernIconKind.Settings, () => _service.GenerateClientServerSetupPackage()), 7, 1, 3);
            AddGridCard(grid, compact, ToolCard("Office Sync Health Monitor", "Show this PC, configured server, SQL reachability, version, backup, and row counts.", ModernIconKind.Activity, () => _service.CreateOfficeHealthReport()), 8, 0, 4);
            AddGridCard(grid, compact, ToolCard("Operations Command Center", "Create COO report for sales, jobs, AMC, vendors, inventory, technicians, and quotations.", ModernIconKind.Analytics, () => _service.CreateOperationsCommandCenterReport()), 9, 1, 4);
            AddGridCard(grid, compact, ToolCard("Material Price Intelligence", "Summarize supplier rates, price variance, quotation margins, and best supplier readiness.", ModernIconKind.Inventory, () => _service.CreateMaterialPriceIntelligenceReport()), 10, 0, 5);
            AddGridCard(grid, compact, ToolCard("Document Automation", "Audit letterhead, quotation, invoice, PO, AMC, delivery note templates and defaults.", ModernIconKind.Document, () => _service.CreateDocumentAutomationReport()), 11, 1, 5);
            AddGridCard(grid, compact, ToolCard("Fresh Client Deployment Mode", "Generate deployment report: clean database status, connection, import order, backup readiness.", ModernIconKind.Checklist, () => _service.CreateFreshClientDeploymentReport()), 12, 0, 6);
            AddGridCard(grid, compact, DataCleanRoomCard(), 13, 1, 6);

            Panel guidance = CardPanel();
            guidance.Padding = new Padding(20);
            guidance.Margin = new Padding(8);
            Label title = LabelText("Security Note", DS.H3, DS.Slate900);
            title.Dock = DockStyle.Top;
            title.Height = 28;
            Label text = LabelText("Diagnostics intentionally exclude passwords, API keys, raw connection strings, license keys, and customer financial data.", DS.Body, DS.Slate600);
            text.Dock = DockStyle.Fill;
            text.MaximumSize = new Size(460, 0);
            guidance.Controls.Add(text);
            guidance.Controls.Add(title);
            grid.Controls.Add(guidance, 0, compact ? 14 : 7);
            grid.SetColumnSpan(guidance, compact ? 1 : 2);

            _content.Controls.Add(grid);
            EndContent();
        }

        private Panel DataCleanRoomCard()
        {
            Panel card = CardPanel();
            card.Padding = new Padding(18);
            card.Margin = new Padding(8);

            Label icon = ModernIconSystem.Badge(ModernIconKind.Import, 38, DS.Primary50, DS.Primary600, 10);
            icon.SetBounds(18, 18, 38, 38);
            card.Controls.Add(icon);

            Label titleLabel = LabelText("Data Clean Room", DS.H3, DS.Slate900);
            titleLabel.SetBounds(70, 18, 310, 24);
            card.Controls.Add(titleLabel);

            Label summaryLabel = LabelText("Choose messy Excel, ZIP, or folder and generate a pre-upload classification report.", DS.Body, DS.Slate600);
            summaryLabel.SetBounds(70, 44, 390, 38);
            card.Controls.Add(summaryLabel);

            Label result = LabelText("Ready", DS.Small, DS.Slate500);
            result.SetBounds(18, 104, 360, 24);
            card.Controls.Add(result);

            Button run = DS.PrimaryBtn("Choose", 96, 34);
            run.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            run.Click += async (s, e) => await RunDataCleanRoom(result, run);
            card.Controls.Add(run);
            LayoutToolCard(card, titleLabel, summaryLabel, result, run);
            card.Resize += (s, e) => LayoutToolCard(card, titleLabel, summaryLabel, result, run);
            return card;
        }

        private async Task RunDataCleanRoom(Label status, Button button)
        {
            string selected = null;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose messy Excel/ZIP/PDF file for Data Clean Room";
                dialog.Filter = "Import source (*.xlsx;*.xls;*.csv;*.zip;*.pdf;*.docx)|*.xlsx;*.xls;*.csv;*.zip;*.pdf;*.docx|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    selected = dialog.FileName;
            }

            if (string.IsNullOrWhiteSpace(selected))
            {
                using (FolderBrowserDialog folder = new FolderBrowserDialog())
                {
                    folder.Description = "Or choose a folder containing client data files";
                    if (folder.ShowDialog(this) == DialogResult.OK)
                        selected = folder.SelectedPath;
                }
            }

            if (string.IsNullOrWhiteSpace(selected))
                return;

            await RunTool(() => _service.CreateDataCleanRoomReport(selected), status, button);
        }

        private void ShowAppInformation()
        {
            ClearContent("App Information");
            bool compact = IsCompactSupportLayout();
            TableLayoutPanel grid = ContentGrid(compact ? 1 : 2, compact ? 4 : 2);
            for (int i = 0; i < (compact ? 4 : 2); i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, compact ? 240 : (i == 0 ? 220 : 300)));

            AddGridCard(grid, compact, InfoCard("Application", new[]
            {
                "Version: " + ConfigService.GetAppVersion(),
                "Machine: " + Environment.MachineName,
                "OS: " + Environment.OSVersion,
                ".NET: " + Environment.Version
            }, ModernIconKind.Settings), 0, 0, 0);
            AddGridCard(grid, compact, InfoCard("Update Settings", new[]
            {
                "Version check: " + (ConfigService.IsVersionCheckEnabled() ? "Enabled" : "Disabled"),
                "Interval: " + ConfigService.GetVersionCheckIntervalHours() + " hour(s)",
                "Host: " + SafeHost(ConfigService.GetVersionCheckUrl())
            }, ModernIconKind.Refresh), 1, 1, 0);
            AddGridCard(grid, compact, InfoCard("Installed Modules", new[]
            {
                "Invoices, Vendors, Purchases, Clients, Contracts",
                "Jobs, Service Desk, Inventory, Payments, Payroll",
                "Reports, Settings, Master Data",
                "Integrations: TallyPrime, WhatsApp Cloud, Calendar, Cloud Backup, GST/e-Invoice"
            }, ModernIconKind.Inventory), 2, 0, 1);
            AddGridCard(grid, compact, InfoCard("Support Boundaries", new[]
            {
                "Enabled: knowledge base, system health tools, diagnostics export.",
                "Not enabled yet: live chat, WhatsApp support, remote desktop, cloud ticketing, AI assistant.",
                "Support diagnostics are local and non-invasive."
            }, ModernIconKind.Security), 3, 1, 1);
            _content.Controls.Add(grid);
            EndContent();
        }

        private Panel DashboardCard(string title, string summary, ModernIconKind iconKind, Color accent, Action action)
        {
            Panel card = CardPanel();
            card.Padding = new Padding(20);
            card.Margin = new Padding(8);

            Label icon = ModernIconSystem.Badge(iconKind, 42, DS.Lighten(accent, 0.84f), accent, 12);
            icon.SetBounds(20, 20, 42, 42);
            card.Controls.Add(icon);

            Label titleLabel = LabelText(title, DS.H3, DS.Slate900);
            titleLabel.SetBounds(78, 20, 330, 26);
            card.Controls.Add(titleLabel);

            Label summaryLabel = LabelText(summary, DS.Body, DS.Slate600);
            summaryLabel.SetBounds(78, 50, 420, 42);
            summaryLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            card.Controls.Add(summaryLabel);

            Button open = DS.PrimaryBtn("Open", 96, 34);
            open.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            open.Click += (s, e) => action();
            card.Controls.Add(open);
            LayoutActionCard(card, titleLabel, summaryLabel, open);
            card.Resize += (s, e) => LayoutActionCard(card, titleLabel, summaryLabel, open);
            return card;
        }

        private Panel ToolCard(string title, string summary, ModernIconKind iconKind, Func<SupportToolResult> action)
        {
            Panel card = CardPanel();
            card.Padding = new Padding(18);
            card.Margin = new Padding(8);

            Label icon = ModernIconSystem.Badge(iconKind, 38, DS.Primary50, DS.Primary600, 10);
            icon.SetBounds(18, 18, 38, 38);
            card.Controls.Add(icon);

            Label titleLabel = LabelText(title, DS.H3, DS.Slate900);
            titleLabel.SetBounds(70, 18, 310, 24);
            card.Controls.Add(titleLabel);

            Label summaryLabel = LabelText(summary, DS.Body, DS.Slate600);
            summaryLabel.SetBounds(70, 44, 390, 38);
            card.Controls.Add(summaryLabel);

            Label result = LabelText("Ready", DS.Small, DS.Slate500);
            result.SetBounds(18, 104, 360, 24);
            card.Controls.Add(result);

            Button run = DS.PrimaryBtn("Run", 90, 34);
            run.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            run.Click += async (s, e) => await RunTool(action, result, run);
            card.Controls.Add(run);
            LayoutToolCard(card, titleLabel, summaryLabel, result, run);
            card.Resize += (s, e) => LayoutToolCard(card, titleLabel, summaryLabel, result, run);
            return card;
        }

        private static void LayoutActionCard(Panel card, Label title, Label summary, Button button)
        {
            int right = Math.Max(160, card.ClientSize.Width - 96);
            title.SetBounds(78, 20, right, 26);
            summary.SetBounds(78, 50, right, 54);
            button.Location = new Point(Math.Max(18, card.ClientSize.Width - button.Width - 18), Math.Max(96, card.ClientSize.Height - button.Height - 18));
        }

        private static void LayoutToolCard(Panel card, Label title, Label summary, Label result, Button button)
        {
            int textWidth = Math.Max(150, card.ClientSize.Width - 90);
            title.SetBounds(70, 18, textWidth, 24);
            summary.SetBounds(70, 44, textWidth, 42);
            result.SetBounds(18, 102, Math.Max(120, card.ClientSize.Width - button.Width - 48), 34);
            button.Location = new Point(Math.Max(18, card.ClientSize.Width - button.Width - 18), Math.Max(96, card.ClientSize.Height - button.Height - 18));
        }

        private async Task RunTool(Func<SupportToolResult> action, Label status, Button button)
        {
            button.Enabled = false;
            status.Text = "Running...";
            status.ForeColor = DS.Slate600;
            Cursor = Cursors.WaitCursor;
            try
            {
                SupportToolResult result = await Task.Run(action);
                status.Text = (result.Success ? "Success: " : "Failed: ") + result.Message;
                status.ForeColor = result.Success ? DS.Green600 : DS.Red600;
                if (!string.IsNullOrWhiteSpace(result.OutputPath))
                    MessageBox.Show(this, result.Message + Environment.NewLine + result.OutputPath, result.Title, MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
                button.Enabled = true;
            }
        }

        private async Task RunStandaloneTool(string title, Func<SupportToolResult> action)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                SupportToolResult result = await Task.Run(action);
                MessageBox.Show(this, result.Message + (string.IsNullOrWhiteSpace(result.OutputPath) ? string.Empty : Environment.NewLine + result.OutputPath), title, MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task CheckUpdates()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                VersionCheckResult result = await VersionCheckService.CheckForUpdateAsync(true);
                string message = result.IsUpdateAvailable
                    ? "Update available: " + result.LatestVersion + Environment.NewLine + result.Notes
                    : "ServoERP is up to date. Current version: " + result.CurrentVersion;
                MessageBox.Show(this, message, "Check Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppLogger.LogInfo("Support Center: update check completed.");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ShowContactSupportBrief()
        {
            string brief = BuildSupportBriefText();
            using (Form dialog = new Form())
            {
                dialog.Text = BrandingService.WindowTitle("Contact Support");
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimumSize = new Size(720, 520);
                dialog.Size = new Size(820, 580);
                dialog.BackColor = DS.BgPage;
                dialog.Font = DS.Body;

                Panel header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = DS.White, Padding = new Padding(24, 18, 24, 14) };
                header.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(DS.Border, 1))
                        e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
                };
                dialog.Controls.Add(header);

                Label icon = ModernIconSystem.Badge(ModernIconKind.Email, 42, DS.Primary50, DS.Primary600, 12);
                icon.Location = new Point(24, 24);
                header.Controls.Add(icon);

                Label title = LabelText("Contact Support", DS.H2, DS.Slate900);
                title.SetBounds(82, 22, 520, 28);
                header.Controls.Add(title);

                Label subtitle = LabelText("Copy this brief into your approved support channel. ServoERP will not send anything automatically.", DS.Body, DS.Slate500);
                subtitle.SetBounds(82, 52, 660, 28);
                subtitle.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                header.Controls.Add(subtitle);

                TextBox briefBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 10f),
                    Text = brief,
                    Margin = new Padding(24)
                };

                Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24), BackColor = DS.BgPage };
                content.Controls.Add(briefBox);
                dialog.Controls.Add(content);
                content.BringToFront();

                FlowLayoutPanel actions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 72,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(24, 16, 24, 16),
                    BackColor = DS.White
                };
                dialog.Controls.Add(actions);

                Button close = DS.GhostBtn("Close", 100, 36);
                close.Click += (s, e) => dialog.Close();
                actions.Controls.Add(close);

                Button export = DS.GhostBtn("Export Diagnostics", 168, 36);
                ModernIconSystem.AddButtonIcon(export, ModernIconKind.Export);
                export.Click += async (s, e) => await RunStandaloneTool("Export Diagnostics Package", () => _service.ExportDiagnosticsPackage());
                actions.Controls.Add(export);

                Button copy = DS.PrimaryBtn("Copy Brief", 126, 36);
                ModernIconSystem.AddButtonIcon(copy, ModernIconKind.Document);
                copy.Click += (s, e) =>
                {
                    if (UIHelper.TrySetClipboardText(dialog, briefBox.Text, BrandingService.WindowTitle("Contact Support")))
                        MessageBox.Show(dialog, "Support brief copied to clipboard.", BrandingService.WindowTitle("Contact Support"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                actions.Controls.Add(copy);

                dialog.Shown += (s, e) =>
                {
                    briefBox.SelectionStart = 0;
                    briefBox.SelectionLength = 0;
                    copy.Focus();
                };

                dialog.ShowDialog(this);
            }
        }

        private static string BuildSupportBriefText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("ServoERP Support Brief");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine("App Version: " + ConfigService.GetAppVersion());
            builder.AppendLine("Machine: " + Environment.MachineName);
            builder.AppendLine("OS: " + Environment.OSVersion);
            builder.AppendLine(".NET: " + Environment.Version);
            builder.AppendLine("Current User Role: " + (SessionManager.CurrentUser == null ? "Not logged in" : SafeBriefValue(SessionManager.CurrentUser.RoleName)));
            builder.AppendLine();
            builder.AppendLine("Database");
            try
            {
                SqlConnectionStringBuilder connection = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
                builder.AppendLine("Server: " + SafeBriefValue(connection.DataSource));
                builder.AppendLine("Database: " + SafeBriefValue(connection.InitialCatalog));
                builder.AppendLine("Authentication: " + (connection.IntegratedSecurity ? "Windows" : "SQL user"));
            }
            catch (Exception ex)
            {
                builder.AppendLine("Database config unavailable: " + ex.Message);
            }
            builder.AppendLine();
            builder.AppendLine("Issue Summary");
            builder.AppendLine("- What were you trying to do?");
            builder.AppendLine("- What happened?");
            builder.AppendLine("- Which page/form was open?");
            builder.AppendLine("- Business impact: blocked / slowed / question only");
            builder.AppendLine("- Urgency: today / this week / planned");
            builder.AppendLine("- Screenshot attached: yes/no");
            builder.AppendLine();
            builder.AppendLine("Helpful Local Actions");
            builder.AppendLine("- Export Diagnostics from Help & Support if support requests logs.");
            builder.AppendLine("- Use Recent Errors to review the latest local exceptions.");
            builder.AppendLine("- This brief is local only; ServoERP does not send emails, messages, or tickets automatically.");
            return builder.ToString();
        }

        private Panel InfoCard(string title, IEnumerable<string> lines, ModernIconKind iconKind)
        {
            Panel card = CardPanel();
            card.Padding = new Padding(20);
            card.Margin = new Padding(8);

            Label icon = ModernIconSystem.Badge(iconKind, 40, DS.Primary50, DS.Primary600, 10);
            icon.SetBounds(20, 20, 40, 40);
            card.Controls.Add(icon);

            Label titleLabel = LabelText(title, DS.H3, DS.Slate900);
            titleLabel.SetBounds(76, 20, 360, 26);
            card.Controls.Add(titleLabel);

            Label body = LabelText(string.Join(Environment.NewLine, lines), DS.Body, DS.Slate600);
            body.SetBounds(76, 52, 500, 180);
            body.AutoSize = false;
            body.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            card.Controls.Add(body);
            LayoutInfoCard(card, titleLabel, body);
            card.Resize += (s, e) => LayoutInfoCard(card, titleLabel, body);
            return card;
        }

        private static void LayoutInfoCard(Panel card, Label title, Label body)
        {
            int textWidth = Math.Max(150, card.ClientSize.Width - 96);
            title.SetBounds(76, 20, textWidth, 26);
            body.SetBounds(76, 52, textWidth, Math.Max(80, card.ClientSize.Height - 72));
        }

        private void RenderArticle(Panel target, SupportArticle article)
        {
            Label badge = LabelText(article.Category.ToUpperInvariant(), DS.CaptionBold(), DS.Primary600);
            badge.Dock = DockStyle.Top;
            badge.Height = 22;
            target.Controls.Add(badge);

            Label title = LabelText(article.Title, DS.H2, DS.Slate900);
            title.Dock = DockStyle.Top;
            title.Height = 36;
            target.Controls.Add(title);

            Label summary = LabelText(article.Summary, DS.Body, DS.Slate600);
            summary.Dock = DockStyle.Top;
            summary.Height = 44;
            target.Controls.Add(summary);

            Panel steps = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = DS.White, Padding = new Padding(0, 8, 0, 0) };
            target.Controls.Add(steps);
            steps.BringToFront();

            int y = 10;
            for (int i = 0; i < article.Steps.Count; i++)
            {
                Label number = DS.StatusChipLabel((i + 1).ToString(), DS.Primary50, DS.Primary600, 30);
                number.SetBounds(0, y, 30, 24);
                steps.Controls.Add(number);

                Label step = LabelText(article.Steps[i], DS.Body, DS.Slate800);
                step.SetBounds(42, y + 2, Math.Max(120, steps.ClientSize.Width - 64), 38);
                step.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                steps.Controls.Add(step);
                y += 48;
            }
        }

        private void AddEmptyArticleState(Panel target)
        {
            Panel emptyIcon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Search, 64, DS.Primary50, DS.Primary600);
            emptyIcon.Location = new Point((target.Width - emptyIcon.Width) / 2, 150);
            emptyIcon.Anchor = AnchorStyles.Top;
            target.Controls.Add(emptyIcon);

            Label title = LabelText("No articles found", DS.H3, DS.Slate900);
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.SetBounds(0, 224, target.Width, 28);
            title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            target.Controls.Add(title);

            Label body = LabelText("Try searching for invoice, backup, update, job, vendor, or diagnostics.", DS.Body, DS.Slate500);
            body.TextAlign = ContentAlignment.MiddleCenter;
            body.SetBounds(0, 256, target.Width, 28);
            body.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            target.Controls.Add(body);
        }

        private static void AddGridCard(TableLayoutPanel grid, bool compact, Control card, int compactIndex, int normalColumn, int normalRow)
        {
            if (compact)
                grid.Controls.Add(card, 0, compactIndex);
            else
                grid.Controls.Add(card, normalColumn, normalRow);
        }

        private static TableLayoutPanel ContentGrid(int columns, int rows)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = columns,
                RowCount = rows,
                BackColor = DS.BgPage,
                Padding = new Padding(0)
            };
            for (int i = 0; i < columns; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            return grid;
        }

        private static Panel CardPanel()
        {
            Panel card = new Panel { BackColor = DS.White, Dock = DockStyle.Fill };
            card.Paint += PaintBorder(DS.Border, DS.RadiusXl);
            card.Resize += (s, e) => DS.Rounded(card, DS.RadiusXl);
            DS.Rounded(card, DS.RadiusXl);
            return card;
        }

        private static Label LabelText(string text, Font font, Color color)
        {
            return new Label
            {
                Text = text ?? string.Empty,
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                UseMnemonic = false
            };
        }

        private static PaintEventHandler PaintBorder(Color border, int radius)
        {
            return (s, e) =>
            {
                Control control = (Control)s;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(border, 1))
                using (System.Drawing.Drawing2D.GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius))
                    e.Graphics.DrawPath(pen, path);
            };
        }

        private static string SafeHost(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) ? uri.Host : "Not configured";
        }

        private static string SafeBriefValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string RecentErrorsText()
        {
            string path = @"C:\HVAC_PRO_MSE\LOGS\app.log";
            if (!System.IO.File.Exists(path))
                return "No app log was found.";

            string[] errors = System.IO.File.ReadAllLines(path)
                .Where(line => line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0)
                .Reverse()
                .Take(8)
                .Reverse()
                .ToArray();

            return errors.Length == 0 ? "No recent errors found in the app log." : string.Join(Environment.NewLine, errors);
        }
    }
}

