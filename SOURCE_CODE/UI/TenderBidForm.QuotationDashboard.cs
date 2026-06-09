using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class TenderBidForm
    {
        private readonly QuotationAnalyticsService _quotationAnalyticsSvc = new QuotationAnalyticsService();
        private const string QuotationDashboardPageKey = "quotation_dashboard";
        private const string QuotationDashboardOrderPath = @"C:\HVAC_PRO_MSE\CONFIG\quotation_dashboard_order.txt";
        private Panel _quotationDashboardPanel;
        private DateTimePicker _quoteDashFrom;
        private DateTimePicker _quoteDashTo;
        private ComboBox _quoteDashGroup;
        private Label _quoteDashStatus;
        private FlowLayoutPanel _quoteDashKpis;
        private FlowLayoutPanel _quoteDashboardCards;
        private QuotationDashboardTile _quoteDashboardDragCard;
        private readonly Dictionary<string, Size> _quoteDashboardBaseSizes = new Dictionary<string, Size>();
        private QuotationOverviewChart _quoteOverviewChart;
        private DataGridView _quoteRecentGrid = null;
        private FlowLayoutPanel _quoteVendorReceived;
        private FlowLayoutPanel _quoteClientSent;
        private FlowLayoutPanel _quoteVendorSent;
        private FlowLayoutPanel _quoteClientReceived;
        private FlowLayoutPanel _quoteAttachedWork;
        private FlowLayoutPanel _quoteTopClients;
        private FlowLayoutPanel _quoteBusinessCards;
        private QuotationValueTrendChart _quoteValueTrend;
        private FlowLayoutPanel _quoteTopItems;
        private FlowLayoutPanel _quoteFollowUps;
        private QuotationLostReasonDonut _quoteLostDonut;
        private FlowLayoutPanel _quoteInsights;

        private Panel BuildQuotationDashboardPanel()
        {
            _quotationDashboardPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = QuotePageBg,
                Padding = new Padding(24, 10, 24, 10)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = QuotePageBg,
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _quoteDashKpis = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuotePageBg, WrapContents = false, AutoScroll = false, Padding = new Padding(0, 4, 0, 2) };
            _quoteDashKpis.Resize += (s, e) => LayoutQuotationDashboardKpis();
            root.Controls.Add(_quoteDashKpis, 0, 0);
            root.Controls.Add(BuildQuoteDashboardCardBoard(), 0, 1);
            _quoteDashStatus = new Label { Visible = false, Text = "Loading..." };

            _quotationDashboardPanel.Controls.Add(root);
            _quotationDashboardPanel.HandleCreated += (s, e) =>
            {
                ApplySavedQuotationDashboardLayout();
                BeginInvoke((Action)RefreshQuotationDashboardSafe);
            };
            return _quotationDashboardPanel;
        }

        private Control BuildQuoteDashboardCardBoard()
        {
            _quoteDashboardCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = QuotePageBg,
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(0, 4, 0, 18),
                AllowDrop = true
            };
            _quoteDashboardCards.DragEnter += QuoteDashboardCards_DragEnter;
            _quoteDashboardCards.DragDrop += QuoteDashboardCards_DragDrop;

            _quoteOverviewChart = new QuotationOverviewChart { Dock = DockStyle.Fill };
            _quoteVendorReceived = CreateRecentQuoteFlow();
            _quoteClientSent = CreateRecentQuoteFlow();
            _quoteVendorSent = CreateRecentQuoteFlow();
            _quoteClientReceived = CreateRecentQuoteFlow();
            _quoteAttachedWork = CreateRecentQuoteFlow();
            _quoteTopClients = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4), BackColor = QuoteSurface };
            _quoteBusinessCards = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
            _quoteValueTrend = new QuotationValueTrendChart { Dock = DockStyle.Fill };
            _quoteTopItems = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4), BackColor = QuoteSurface };
            _quoteFollowUps = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4), BackColor = QuoteSurface };
            _quoteLostDonut = new QuotationLostReasonDonut { Dock = DockStyle.Fill };
            _quoteInsights = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
            FlowLayoutPanel quick = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = true, AutoScroll = true, Padding = new Padding(4), Tag = "TOOLBAR" };
            quick.Controls.Add(QuickAction("New Quotation", () => NewRecord()));
            quick.Controls.Add(QuickAction("Create from Template", () => NewRecord()));
            quick.Controls.Add(QuickAction("Create Customer Invoice", async () => await CreateInvoiceAsync()));
            quick.Controls.Add(QuickAction("Send Supplier PO", async () => await CreatePurchaseOrdersAsync()));
            quick.Controls.Add(QuickAction("Duplicate Quotation", () => DuplicateCurrentQuotation()));
            quick.Controls.Add(QuickAction("Quotation Report", () => OnNavigate?.Invoke(12)));
            quick.Controls.Add(QuickAction("Export Data", () => MessageBox.Show("Use the quotation PDF/export tools after selecting a quotation.", "Export Data", MessageBoxButtons.OK, MessageBoxIcon.Information)));

            AddDashboardCard("vendor_workflow", "Supplier Workflow", BuildQuotationWorkflowSection("Sent to Suppliers", _quoteVendorSent, "Received from Suppliers", _quoteVendorReceived), 360, 452, "Workflow");
            AddDashboardCard("client_workflow", "Client Workflow", BuildQuotationWorkflowSection("Sent to Clients", _quoteClientSent, "Received from Clients", _quoteClientReceived), 360, 452, "Workflow");
            AddDashboardCard("work_attached", "Work Attached", _quoteAttachedWork, 360, 452, "Workflow");
            AddDashboardCard("overview", "Overview", _quoteOverviewChart, 560, 216, "Large");
            AddDashboardCard("business", "Business Health", _quoteBusinessCards, 560, 214, "Large");
            AddDashboardCard("trend", "Value Trend", _quoteValueTrend, 560, 214, "Large");
            AddDashboardCard("top_clients", "Top Clients", _quoteTopClients, 280, 196, "Compact");
            AddDashboardCard("top_items", "Top Items", _quoteTopItems, 280, 196, "Compact");
            AddDashboardCard("followups", "Follow Ups", _quoteFollowUps, 280, 196, "Compact");
            AddDashboardCard("lost_reasons", "Lost Reasons", _quoteLostDonut, 280, 196, "Compact");
            AddDashboardCard("insights", "Insights", _quoteInsights, 560, 178, "Large");
            AddDashboardCard("quick_actions", "Quick Actions", quick, 560, 178, "Large");

            _quoteDashboardCards.Resize += (s, e) => LayoutQuotationDashboardCards();
            ApplySavedQuotationDashboardOrder();
            return _quoteDashboardCards;
        }

        private Control BuildQuoteDashboardHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 8, 16, 8) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(BorderColor))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            header.Controls.Add(new Label
            {
                Text = "Quotation Dashboard",
                Location = new Point(16, 8),
                Size = new Size(300, 24),
                Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
                ForeColor = QuoteText,
                AutoEllipsis = true
            });
            header.Controls.Add(new Label
            {
                Text = "Overview of all quotations and business analytics",
                Location = new Point(17, 34),
                Size = new Size(420, 18),
                Font = new Font("Segoe UI", 9),
                ForeColor = QuoteMuted,
                AutoEllipsis = true
            });

            Button more = MakeDashButton("...", 38, QuoteSurface, QuoteText);
            more.Location = new Point(header.Width - 56, 12);
            more.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            more.Click += (s, e) => MessageBox.Show("Quotation dashboard actions are available from the quick action cards below.", "Quotation Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Button newQuote = MakeDashButton("+ New Quotation", 154, CargoPurple, Color.White);
            newQuote.Location = new Point(header.Width - 218, 12);
            newQuote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            newQuote.Click += (s, e) => NewRecord();

            Button refresh = MakeDashButton("Refresh", 82, QuoteSurface, InfoBlue);
            refresh.Location = new Point(header.Width - 308, 12);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Click += (s, e) => RefreshQuotationDashboardSafe();

            _quoteDashGroup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 94, Height = 30, Location = new Point(header.Width - 536, 14), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _quoteDashGroup.Items.AddRange(new object[] { "Week", "Day", "Month" });
            _quoteDashGroup.SelectedIndex = 0;
            _quoteDashGroup.SelectedIndexChanged += (s, e) => RefreshQuotationDashboardSafe();

            DateTime today = DateTime.Today;
            _quoteDashFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 112, Height = 30, Value = new DateTime(today.Year, today.Month, 1), Location = new Point(header.Width - 776, 14), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _quoteDashTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 112, Height = 30, Value = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)), Location = new Point(header.Width - 656, 14), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _quoteDashFrom.ValueChanged += (s, e) => RefreshQuotationDashboardSafe();
            _quoteDashTo.ValueChanged += (s, e) => RefreshQuotationDashboardSafe();

            _quoteDashStatus = new Label { AutoSize = true, ForeColor = QuoteMuted, Font = new Font("Segoe UI", 8), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(header.Width - 926, 20), Text = "Loading..." };

            header.Controls.AddRange(new Control[] { _quoteDashStatus, _quoteDashFrom, _quoteDashTo, _quoteDashGroup, refresh, newQuote, more });
            return header;
        }

        private DataGridView CreateRecentQuotationGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = QuoteSurface,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Cursor = Cursors.Hand
            };
            GridTheme.Apply(grid);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 28;
            grid.RowTemplate.Height = 30;
            grid.GridColor = DS.Border;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = QuoteMuted;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.4f, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.4f);
            grid.DefaultCellStyle.ForeColor = QuoteText;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            grid.DefaultCellStyle.SelectionForeColor = QuoteText;
            grid.Paint += (s, e) =>
            {
                if (grid.Rows.Count > 0)
                    return;

                Rectangle bounds = new Rectangle(0, grid.ColumnHeadersHeight + 8, grid.Width, Math.Max(40, grid.Height - grid.ColumnHeadersHeight - 12));
                QuotationDashboardPaint.DrawGridEmpty(e.Graphics, bounds, "No quotations in this range", "New quotations will appear here after they are saved.");
            };
            grid.CellClick += QuoteRecentGrid_CellClick;
            grid.CellContentClick += QuoteRecentGrid_CellContentClick;
            return grid;
        }

        private void RefreshQuotationDashboardSafe()
        {
            if (_quotationDashboardPanel == null || _quotationDashboardPanel.IsDisposed)
                return;
            try
            {
                var filter = new QuotationAnalyticsFilter
                {
                    DateFrom = _quoteDashFrom?.Value.Date,
                    DateTo = _quoteDashTo?.Value.Date,
                    Grouping = ParseGrouping(_quoteDashGroup?.Text)
                };
                QuotationDashboardSnapshot snapshot = _quotationAnalyticsSvc.BuildSnapshot(filter);
                BindQuotationDashboard(snapshot);
            }
            catch (Exception ex)
            {
                if (_quoteDashStatus != null)
                {
                    _quoteDashStatus.Text = "Dashboard error: " + ex.Message;
                    _quoteDashStatus.ForeColor = Color.Firebrick;
                }
            }
        }

        private void BindQuotationDashboard(QuotationDashboardSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            _quoteDashStatus.Text = snapshot.UsesDemoData ? "No live quotation data" : "Live quotation data";
            _quoteDashStatus.ForeColor = snapshot.UsesDemoData ? WarnOrange : SaveGreen;

            _quoteDashKpis.Controls.Clear();
            AddKpi(snapshot.Kpis.TotalQuotations, false, "0");
            AddKpi(snapshot.Kpis.TotalValue, true, "N2");
            AddKpi(snapshot.Kpis.ConvertedValue, true, "N2");
            AddKpi(snapshot.Kpis.ConversionRate, false, "0.0'%'");
            AddKpi(snapshot.Kpis.PendingValue, true, "N2");
            AddKpi(snapshot.Kpis.AverageQuotationValue, true, "N2");
            LayoutQuotationDashboardKpis();

            _quoteOverviewChart.SetData(snapshot.Overview);
            _quoteValueTrend.SetData(snapshot.ValueTrend);
            _quoteLostDonut.SetData(snapshot.LostReasons);
            BindTopClients(snapshot.TopClients);
            BindRecentQuotationCards(snapshot.RecentQuotations);
            BindBusinessCards(snapshot.Kpis);
            BindTopItems(snapshot.TopItems);
            BindFollowUps(snapshot.UpcomingFollowUps);
            BindInsights(snapshot.Insights);
        }

        private void AddKpi(QuotationKpi kpi, bool currency, string format)
        {
            Panel card = new Panel { Width = 168, Height = 78, MinimumSize = new Size(148, 78), BackColor = QuoteSurface, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(12, 10, 10, 8) };
            DS.Rounded(card, 10);
            card.Paint += (s, e) => { using (Pen p = new Pen(BorderColor)) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1); };
            card.Controls.Add(new Label { Text = kpi.Title, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = QuoteMuted, AutoSize = true, Location = new Point(12, 9) });
            string value = currency ? IndiaFormatHelper.FormatCurrency(kpi.Value) : kpi.Value.ToString(format);
            card.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = QuoteText, Size = new Size(144, 21), Location = new Point(12, 31), AutoEllipsis = true });
            Color changeColor = kpi.MonthOverMonthPercent >= 0m ? SaveGreen : Color.FromArgb(220, 38, 38);
            string arrow = kpi.MonthOverMonthPercent >= 0m ? "^ " : "v ";
            card.Controls.Add(new Label { Text = arrow + Math.Abs(kpi.MonthOverMonthPercent).ToString("0.#") + "% from last month", Font = new Font("Segoe UI", 7.6f), ForeColor = changeColor, Size = new Size(144, 16), Location = new Point(12, 57), AutoEllipsis = true });
            _quoteDashKpis.Controls.Add(card);
        }

        private void LayoutQuotationDashboardKpis()
        {
            if (_quoteDashKpis == null || _quoteDashKpis.ClientSize.Width < 360 || _quoteDashKpis.Controls.Count == 0)
                return;

            const int gap = 8;
            int count = _quoteDashKpis.Controls.Count;
            int usable = Math.Max(360, _quoteDashKpis.ClientSize.Width - 2);
            int width = Math.Max(148, (usable - count * gap) / count);
            foreach (Control card in _quoteDashKpis.Controls)
            {
                card.Width = width;
                card.Height = 78;
                card.Margin = new Padding(0, 0, gap, 0);
            }

            _quoteDashKpis.PerformLayout();
        }

        private FlowLayoutPanel CreateRecentQuoteFlow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4, 2, 4, 4),
                BackColor = QuoteSurface
            };
        }

        private Control BuildQuotationWorkflowSection(string topTitle, FlowLayoutPanel topFlow, string bottomTitle, FlowLayoutPanel bottomFlow)
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = QuoteSurface,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0)
            };
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            table.Controls.Add(BuildQuotationWorkflowBucket(topTitle, topFlow), 0, 0);
            table.Controls.Add(BuildQuotationWorkflowBucket(bottomTitle, bottomFlow), 0, 1);
            return table;
        }

        private Control BuildQuotationWorkflowBucket(string title, FlowLayoutPanel flow)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = QuoteSurface,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(0, 0, 0, 0)
            };

            var header = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = QuoteText,
                Padding = new Padding(2, 2, 0, 0),
                AutoEllipsis = true
            };
            flow.Dock = DockStyle.Fill;
            flow.Padding = new Padding(2, 2, 4, 4);
            panel.Controls.Add(flow);
            panel.Controls.Add(header);
            return panel;
        }

        private void BindRecentQuotationCards(List<QuotationRecentRow> rows)
        {
            List<QuotationRecentRow> safeRows = rows ?? new List<QuotationRecentRow>();
            BindQuoteWorkflowList(
                _quoteVendorReceived,
                safeRows.Where(IsVendorReceivedQuotation).Take(5).ToList(),
                "No vendor quotations received yet.");
            BindQuoteWorkflowList(
                _quoteClientSent,
                safeRows.Where(IsClientSentQuotation).Take(5).ToList(),
                "No quotations sent to clients yet.");
            BindQuoteWorkflowList(
                _quoteVendorSent,
                safeRows.Where(IsVendorSentQuotation).Take(5).ToList(),
                "No vendor quotation requests sent yet.");
            BindQuoteWorkflowList(
                _quoteClientReceived,
                safeRows.Where(IsClientReceivedQuotation).Take(5).ToList(),
                "No client responses received yet.");
            BindQuoteWorkflowList(
                _quoteAttachedWork,
                safeRows.Where(IsWorkAttachedQuotation).Take(5).ToList(),
                "No attached work found yet.");
        }

        private void BindQuoteWorkflowList(FlowLayoutPanel flow, List<QuotationRecentRow> rows, string emptyText)
        {
            if (flow == null)
                return;

            flow.Controls.Clear();
            foreach (QuotationRecentRow row in rows ?? new List<QuotationRecentRow>())
                flow.Controls.Add(QuoteWorkflowRow(row));

            if (flow.Controls.Count == 0)
                flow.Controls.Add(EmptyLabel(emptyText));

            ResizeQuoteWorkflowRows(flow);
            flow.Resize -= QuoteWorkflowFlow_Resize;
            flow.Resize += QuoteWorkflowFlow_Resize;
        }

        private void QuoteWorkflowFlow_Resize(object sender, EventArgs e)
        {
            ResizeQuoteWorkflowRows(sender as FlowLayoutPanel);
        }

        private static void ResizeQuoteWorkflowRows(FlowLayoutPanel flow)
        {
            if (flow == null || flow.ClientSize.Width <= 0)
                return;

            int width = Math.Max(220, flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - flow.Padding.Horizontal - 8);
            foreach (Panel card in flow.Controls.OfType<Panel>())
                card.Width = width;
        }

        private Control QuoteWorkflowRow(QuotationRecentRow row)
        {
            Panel card = new Panel
            {
                Width = 280,
                Height = 54,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(10, 7, 10, 6),
                Cursor = Cursors.Hand,
                Tag = row
            };
            DS.Rounded(card, 8);
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            Label title = new Label
            {
                Text = SafeQuoteText(row.QuotationNumber, "Quotation") + "  -  " + SafeQuoteText(row.ClientName, "Client"),
                Location = new Point(10, 6),
                Size = new Size(card.Width - 20, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                ForeColor = QuoteText,
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };
            Label meta = new Label
            {
                Text = BuildQuoteWorkflowMeta(row),
                Location = new Point(10, 27),
                Size = new Size(card.Width - 20, 17),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = QuoteMuted,
                AutoEllipsis = true,
                Cursor = Cursors.Hand
            };
            card.Controls.Add(title);
            card.Controls.Add(meta);
            card.Click += QuoteWorkflowRow_Click;
            title.Click += QuoteWorkflowRow_Click;
            meta.Click += QuoteWorkflowRow_Click;
            return card;
        }

        private async void QuoteWorkflowRow_Click(object sender, EventArgs e)
        {
            Control source = sender as Control;
            while (source != null && !(source.Tag is QuotationRecentRow))
                source = source.Parent;

            QuotationRecentRow row = source == null ? null : source.Tag as QuotationRecentRow;
            if (row == null || row.BidId <= 0)
                return;

            await LoadQuotationFromDashboardAsync(row.BidId);
        }

        private static string BuildQuoteWorkflowMeta(QuotationRecentRow row)
        {
            if (row == null)
                return string.Empty;

            string status = SafeQuoteText(row.Status, "Draft");
            string date = row.FollowUpDate.HasValue
                ? "Follow-up " + IndiaFormatHelper.FormatDate(row.FollowUpDate.Value)
                : IndiaFormatHelper.FormatDate(row.QuotationDate);
            return status + " | " + IndiaFormatHelper.FormatCurrency(row.Value) + " | " + date;
        }

        private static bool IsVendorReceivedQuotation(QuotationRecentRow row)
        {
            string supplier = NormalizeQuoteText(row == null ? null : row.SupplierDocumentStatus);
            return ContainsAnyQuoteText(supplier, "received", "vendor quote", "supplier quote", "quote received", "rate received", "available", "selected");
        }

        private static bool IsClientSentQuotation(QuotationRecentRow row)
        {
            string customer = NormalizeQuoteText(row == null ? null : row.CustomerDocumentStatus);
            string status = NormalizeQuoteText(row == null ? null : row.Status);
            return ContainsAnyQuoteText(customer, "sent", "submitted", "shared", "emailed", "issued", "delivered") ||
                   ContainsAnyQuoteText(status, "sent", "submitted", "negotiation", "follow up");
        }

        private static bool IsVendorSentQuotation(QuotationRecentRow row)
        {
            string supplier = NormalizeQuoteText(row == null ? null : row.SupplierDocumentStatus);
            return ContainsAnyQuoteText(supplier, "sent", "requested", "needed", "supplier quote needed", "supplier quote requested", "rfq", "vendor request", "po sent");
        }

        private static bool IsClientReceivedQuotation(QuotationRecentRow row)
        {
            string customer = NormalizeQuoteText(row == null ? null : row.CustomerDocumentStatus);
            string status = NormalizeQuoteText(row == null ? null : row.Status);
            return ContainsAnyQuoteText(customer, "received", "accepted", "approved", "client response", "customer response", "confirmation received") ||
                   ContainsAnyQuoteText(status, "accepted", "approved", "won", "converted");
        }

        private static bool IsWorkAttachedQuotation(QuotationRecentRow row)
        {
            string flow = NormalizeQuoteText(row == null ? null : row.CommercialFlow);
            string customer = NormalizeQuoteText(row == null ? null : row.CustomerDocumentStatus);
            string status = NormalizeQuoteText(row == null ? null : row.Status);
            return ContainsAnyQuoteText(flow, "service", "job", "work", "mixed") ||
                   ContainsAnyQuoteText(customer, "accepted", "approved", "work") ||
                   ContainsAnyQuoteText(status, "won", "converted", "approved");
        }

        private static string NormalizeQuoteText(string text)
        {
            return (text ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string SafeQuoteText(string text, string fallback)
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }

        private static bool ContainsAnyQuoteText(string text, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(text) || needles == null)
                return false;

            return needles.Any(needle => text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void BindRecentQuotations(List<QuotationRecentRow> rows)
        {
            _quoteRecentGrid.Columns.Clear();
            _quoteRecentGrid.Rows.Clear();
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BidId", HeaderText = "Id", Visible = false });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quotation", HeaderText = "Quotation No.", FillWeight = 120 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Client", HeaderText = "Client", FillWeight = 140 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Site", HeaderText = "Site / Project", FillWeight = 110 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", FillWeight = 80 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Value", FillWeight = 90 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Flow", HeaderText = "Flow", FillWeight = 105 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "Customer Side", FillWeight = 110 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier Side", FillWeight = 110 });
            _quoteRecentGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 80 });
            _quoteRecentGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Pdf", HeaderText = "", Text = "PDF", UseColumnTextForButtonValue = true, FillWeight = 50 });
            _quoteRecentGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Edit", HeaderText = "", Text = "Edit", UseColumnTextForButtonValue = true, FillWeight = 50 });
            _quoteRecentGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Convert", HeaderText = "", Text = "Invoice", UseColumnTextForButtonValue = true, FillWeight = 60 });
            _quoteRecentGrid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "Delete", UseColumnTextForButtonValue = true, FillWeight = 60 });
            foreach (QuotationRecentRow row in rows ?? new List<QuotationRecentRow>())
                _quoteRecentGrid.Rows.Add(row.BidId, row.QuotationNumber, row.ClientName, row.SiteName, IndiaFormatHelper.FormatDate(row.QuotationDate), IndiaFormatHelper.FormatCurrency(row.Value), row.CommercialFlow, row.CustomerDocumentStatus, row.SupplierDocumentStatus, row.Status);
            _quoteRecentGrid.Invalidate();
        }

        private void QuoteRecentGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string column = _quoteRecentGrid.Columns[e.ColumnIndex].Name;
            if (column == "Pdf" || column == "Edit" || column == "Convert" || column == "Delete" || GlobalStatusEditor.IsEditableStatusCell(_quoteRecentGrid, e.RowIndex, e.ColumnIndex))
                return;

            int bidId;
            if (!int.TryParse(Convert.ToString(_quoteRecentGrid.Rows[e.RowIndex].Cells["BidId"].Value), out bidId) || bidId <= 0)
                return;

            RecentDocumentOpenService.OpenQuotationPdf(this, bidId);
        }

        private async void QuoteRecentGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            string column = _quoteRecentGrid.Columns[e.ColumnIndex].Name;
            if (column != "Pdf" && column != "Edit" && column != "Convert" && column != "Delete")
                return;
            int bidId;
            if (!int.TryParse(Convert.ToString(_quoteRecentGrid.Rows[e.RowIndex].Cells["BidId"].Value), out bidId) || bidId <= 0)
                return;
            if (column == "Pdf")
            {
                RecentDocumentOpenService.OpenQuotationPdf(this, bidId);
                return;
            }
            if (column == "Delete")
            {
                await DeleteQuotationFromDashboardAsync(bidId);
                return;
            }

            await LoadQuotationFromDashboardAsync(bidId);
            if (column == "Convert")
                await CreateInvoiceAsync();
        }

        private async Task DeleteQuotationFromDashboardAsync(int bidId)
        {
            HVAC_Pro_Desktop.Models.TenderBid quote = await Task.Run(() => _svc.GetById(bidId));
            if (quote == null)
            {
                SetStatus("Quotation not found. Refresh and try again.", Color.Firebrick);
                return;
            }

            string quoteNo = string.IsNullOrWhiteSpace(quote.QuotationNumber) ? "this quotation" : quote.QuotationNumber;
            DialogResult confirm = MessageBox.Show(
                "Permanently delete " + quoteNo + " including quotation line items and links from generated invoices or POs?\r\n\r\nThis cannot be undone.",
                "Delete Quotation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SetStatus("Deleting quotation...", InfoBlue);
                await Task.Run(() => _svc.Delete(bidId));
                if (_current != null && _current.BidID == bidId)
                    NewRecord(false);
                await RefreshListsAsync();
                SetStatus("Quotation deleted.", Color.FromArgb(220, 38, 38));
            }
            catch (Exception ex)
            {
                SetStatus("Delete failed: " + ex.Message, Color.Firebrick);
                MessageBox.Show("Delete quotation failed: " + ex.Message, "Delete Quotation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadQuotationFromDashboardAsync(int bidId)
        {
            SetStatus("Loading quotation...", InfoBlue);
            _current = await Task.Run(() => _svc.GetByIdDetailed(bidId));
            PopulateCurrent(_current);
            ShowQuotationEditor();
            SetStatus("Quotation loaded.", DS.Slate500);
        }

        private void DuplicateCurrentQuotation()
        {
            if (_current == null || _current.BidID <= 0)
            {
                NewRecord();
                return;
            }
            string title = _txtTitle.Text;
            var clonedItems = _lineItems.Select(item => new Models.TenderBidLineItem
            {
                Category = item.Category,
                ItemDescription = item.ItemDescription,
                Quantity = item.Quantity,
                Unit = item.Unit,
                HsnSacCode = item.HsnSacCode,
                GSTRatePct = item.GSTRatePct,
                SellPricePerUnit = item.SellPricePerUnit,
                CostPerUnit = item.CostPerUnit,
                TaxableLineTotal = item.TaxableLineTotal,
                GSTAmount = item.GSTAmount,
                MarginPct = item.MarginPct,
                IsInternalLabour = item.IsInternalLabour,
                AnalysisNotes = item.AnalysisNotes
            }).ToList();
            NewRecord();
            _txtTitle.Text = string.IsNullOrWhiteSpace(title) ? "Duplicated quotation" : title + " - Copy";
            _lineItems.Clear();
            _lineItems.AddRange(clonedItems);
            RefreshGrid();
            RefreshSummary();
        }

        private void BindTopClients(List<QuotationTopClientRow> rows)
        {
            _quoteTopClients.Controls.Clear();
            int index = 1;
            foreach (QuotationTopClientRow row in rows ?? new List<QuotationTopClientRow>())
                _quoteTopClients.Controls.Add(MiniRow(index++ + ". " + row.ClientName, IndiaFormatHelper.FormatCurrency(row.Amount), 190));
            if (_quoteTopClients.Controls.Count == 0)
                _quoteTopClients.Controls.Add(EmptyLabel("No client value in range."));
        }

        private void BindBusinessCards(QuotationKpiSet kpis)
        {
            _quoteBusinessCards.Controls.Clear();
            _quoteBusinessCards.Controls.Add(SmallMetric("Win Rate", kpis.WinRate.Value.ToString("0.0") + "%", kpis.WinRate.MonthOverMonthPercent));
            _quoteBusinessCards.Controls.Add(SmallMetric("Avg. Sales Cycle", kpis.AverageSalesCycle.Value.ToString("0.0") + " days", kpis.AverageSalesCycle.MonthOverMonthPercent));
            _quoteBusinessCards.Controls.Add(SmallMetric("Repeat Client Rate", kpis.RepeatClientRate.Value.ToString("0.0") + "%", kpis.RepeatClientRate.MonthOverMonthPercent));
            _quoteBusinessCards.Controls.Add(SmallMetric("Revenue Pipeline", IndiaFormatHelper.FormatCurrency(kpis.RevenuePipeline.Value), kpis.RevenuePipeline.MonthOverMonthPercent));
            _quoteBusinessCards.Controls.Add(SmallMetric("Weighted Pipeline", IndiaFormatHelper.FormatCurrency(kpis.WeightedPipeline.Value), kpis.WeightedPipeline.MonthOverMonthPercent));
            _quoteBusinessCards.Controls.Add(SmallMetric("Expected Revenue", IndiaFormatHelper.FormatCurrency(kpis.ExpectedRevenue.Value), kpis.ExpectedRevenue.MonthOverMonthPercent));
        }

        private void BindTopItems(List<QuotationTopItemRow> rows)
        {
            _quoteTopItems.Controls.Clear();
            foreach (QuotationTopItemRow row in rows ?? new List<QuotationTopItemRow>())
                _quoteTopItems.Controls.Add(MiniRow(row.ItemName, IndiaFormatHelper.FormatCurrency(row.TotalValue), 220));
            if (_quoteTopItems.Controls.Count == 0)
                _quoteTopItems.Controls.Add(EmptyLabel("No quotation items in range."));
        }

        private void BindFollowUps(List<QuotationFollowUpRow> rows)
        {
            _quoteFollowUps.Controls.Clear();
            foreach (QuotationFollowUpRow row in rows ?? new List<QuotationFollowUpRow>())
                _quoteFollowUps.Controls.Add(MiniRow(IndiaFormatHelper.FormatDate(row.FollowUpDate) + "  " + row.ClientName, row.QuotationNumber, 250));
            if (_quoteFollowUps.Controls.Count == 0)
                _quoteFollowUps.Controls.Add(EmptyLabel("No upcoming follow ups."));
        }

        private void BindInsights(List<QuotationInsight> insights)
        {
            _quoteInsights.Controls.Clear();
            foreach (QuotationInsight insight in insights ?? new List<QuotationInsight>())
            {
                Panel tile = new Panel { Width = 230, Height = 52, BackColor = DS.Lighten(insight.Color, 0.88f), Margin = new Padding(0, 0, 10, 0), Padding = new Padding(12, 7, 12, 6) };
                DS.Rounded(tile, 8);
                tile.Controls.Add(new Label { Text = insight.Title, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = insight.Color, Location = new Point(12, 6), AutoSize = true });
                tile.Controls.Add(new Label { Text = insight.Text, Font = new Font("Segoe UI", 8), ForeColor = QuoteText, Location = new Point(12, 25), Size = new Size(220, 18) });
                _quoteInsights.Controls.Add(tile);
            }
        }

        private Panel Card(string title, Control content)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = QuoteSurface, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(12, 30, 12, 10) };
            DS.Rounded(card, 10);
            card.Paint += (s, e) => { using (Pen p = new Pen(BorderColor)) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1); };
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = QuoteText, Location = new Point(12, 9), AutoSize = true });
            card.Controls.Add(content);
            return card;
        }

        private QuotationDashboardTile AddDashboardCard(string key, string title, Control content, int width, int height, string preset)
        {
            var card = new QuotationDashboardTile
            {
                CardKey = "quote_dash_" + key,
                CardTitle = string.Empty,
                Title = title,
                Size = new Size(width, height),
                MinimumSize = new Size(260, 148),
                MaximumSize = new Size(1600, 900),
                BorderColor = BorderColor,
                BackgroundColor = QuoteSurface,
                Margin = new Padding(0, 0, 12, 12)
            };
            card.ConfigurePresets(
                new Size(width, height),
                new Size(Math.Max(width, 520), Math.Max(height + 40, 240)),
                new Size(1040, Math.Max(height + 120, 320)));
            content.Dock = DockStyle.Fill;
            card.ContentHost.Controls.Add(content);
            card.DragRequested += QuoteDashboardCard_DragRequested;
            card.ResizeCompleted += (s, e) =>
            {
                _quoteDashboardCards?.PerformLayout();
            };

            _quoteDashboardCards.Controls.Add(card);
            _quoteDashboardBaseSizes[card.CardKey] = new Size(width, height);
            LayoutQuotationDashboardCards();
            return card;
        }

        private void QuoteDashboardCard_DragRequested(object sender, MouseEventArgs e)
        {
            var card = sender as QuotationDashboardTile;
            if (card == null || _quoteDashboardCards == null)
                return;

            _quoteDashboardDragCard = card;
            card.DoDragDrop(card.CardKey ?? string.Empty, DragDropEffects.Move);
        }

        private void QuoteDashboardCards_DragEnter(object sender, DragEventArgs e)
        {
            if (_quoteDashboardDragCard != null)
                e.Effect = DragDropEffects.Move;
        }

        private void QuoteDashboardCards_DragDrop(object sender, DragEventArgs e)
        {
            if (_quoteDashboardCards == null || _quoteDashboardDragCard == null)
                return;

            Point point = _quoteDashboardCards.PointToClient(new Point(e.X, e.Y));
            int newIndex = _quoteDashboardCards.Controls.Count - 1;
            for (int i = 0; i < _quoteDashboardCards.Controls.Count; i++)
            {
                Control candidate = _quoteDashboardCards.Controls[i];
                if (candidate == _quoteDashboardDragCard)
                    continue;

                Rectangle bounds = candidate.Bounds;
                bool beforeByRow = point.Y < bounds.Top + bounds.Height / 2;
                bool beforeByColumn = point.Y <= bounds.Bottom && point.X < bounds.Left + bounds.Width / 2;
                if (beforeByRow || beforeByColumn)
                {
                    newIndex = i;
                    break;
                }
            }

            _quoteDashboardCards.Controls.SetChildIndex(_quoteDashboardDragCard, Math.Max(0, newIndex));
            _quoteDashboardCards.PerformLayout();
            SaveQuotationDashboardOrder();
            _quoteDashboardDragCard = null;
        }

        private void LayoutQuotationDashboardCards()
        {
            if (_quoteDashboardCards == null || _quoteDashboardCards.ClientSize.Width < 420)
                return;

            int usable = Math.Max(360, _quoteDashboardCards.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 18);
            int gap = 12;
            bool wideCanvas = usable >= 1060;
            int quarter = wideCanvas ? Math.Max(260, (usable - (gap * 4)) / 4) : 0;
            int third = Math.Max(260, (usable - gap * 3) / 3);
            int workflow = wideCanvas ? Math.Max(340, (usable - (gap * 3)) / 3) : third;
            int half = wideCanvas ? (quarter * 2) + gap : Math.Max(360, (usable - gap * 2) / 2);
            int full = Math.Max(360, usable - gap);

            foreach (QuotationDashboardTile card in _quoteDashboardCards.Controls.OfType<QuotationDashboardTile>())
            {
                Size baseSize;
                if (!_quoteDashboardBaseSizes.TryGetValue(card.CardKey ?? string.Empty, out baseSize))
                    baseSize = card.Size;

                bool fullWidth = IsAny(card.CardKey, "quote_dash_recent");
                bool workflowCard = IsAny(card.CardKey, "quote_dash_vendor_workflow", "quote_dash_client_workflow", "quote_dash_work_attached");
                bool wide = IsAny(card.CardKey, "quote_dash_overview", "quote_dash_trend", "quote_dash_business", "quote_dash_insights", "quote_dash_quick_actions");
                int targetWidth = fullWidth ? full : workflowCard ? workflow : wide ? half : (wideCanvas ? quarter : third);
                targetWidth = Math.Max(card.MinimumSize.Width, Math.Min(card.MaximumSize.Width, targetWidth));

                if (Math.Abs(card.Width - targetWidth) > 4)
                    card.Width = targetWidth;

                int targetHeight = ResolveQuotationDashboardCardHeight(card.CardKey, baseSize.Height, wideCanvas);
                if (Math.Abs(card.Height - targetHeight) > 4)
                    card.Height = targetHeight;
            }
            int contentHeight = 0;
            foreach (Control child in _quoteDashboardCards.Controls)
                contentHeight = Math.Max(contentHeight, child.Bottom + child.Margin.Bottom + 18);
            _quoteDashboardCards.AutoScrollMinSize = new Size(0, Math.Max(_quoteDashboardCards.ClientSize.Height, contentHeight));
        }

        private static int ResolveQuotationDashboardCardHeight(string cardKey, int baseHeight, bool wideCanvas)
        {
            if (IsAny(cardKey, "quote_dash_recent"))
                return wideCanvas ? 244 : Math.Max(baseHeight, 232);
            if (IsAny(cardKey, "quote_dash_vendor_workflow", "quote_dash_client_workflow", "quote_dash_work_attached"))
                return Math.Max(baseHeight, 452);
            if (IsAny(cardKey, "quote_dash_overview"))
                return wideCanvas ? 216 : Math.Max(baseHeight, 206);
            if (IsAny(cardKey, "quote_dash_business", "quote_dash_trend"))
                return wideCanvas ? 214 : Math.Max(baseHeight, 206);
            if (IsAny(cardKey, "quote_dash_insights", "quote_dash_quick_actions"))
                return wideCanvas ? 178 : Math.Max(baseHeight, 172);

            return wideCanvas ? 196 : Math.Max(baseHeight, 190);
        }

        private static bool IsAny(string value, params string[] options)
        {
            return options != null && options.Any(option => string.Equals(value ?? string.Empty, option, StringComparison.OrdinalIgnoreCase));
        }

        private void ApplySavedQuotationDashboardLayout()
        {
            LayoutQuotationDashboardCards();
        }

        private void ApplySavedQuotationDashboardOrder()
        {
            try
            {
                if (_quoteDashboardCards == null || !File.Exists(QuotationDashboardOrderPath))
                    return;

                string[] keys = File.ReadAllText(QuotationDashboardOrderPath)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .ToArray();
                string[] requiredCards = { "quote_dash_vendor_workflow", "quote_dash_client_workflow", "quote_dash_work_attached" };
                if (requiredCards.Any(required => !keys.Contains(required, StringComparer.OrdinalIgnoreCase)))
                    return;

                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    QuotationDashboardTile card = _quoteDashboardCards.Controls
                        .OfType<QuotationDashboardTile>()
                        .FirstOrDefault(c => string.Equals(c.CardKey, keys[i], StringComparison.OrdinalIgnoreCase));
                    if (card != null)
                        _quoteDashboardCards.Controls.SetChildIndex(card, 0);
                }
            }
            catch
            {
            }
        }

        private void SaveQuotationDashboardOrder()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QuotationDashboardOrderPath));
                string order = string.Join(",", _quoteDashboardCards.Controls.OfType<QuotationDashboardTile>().Select(c => c.CardKey));
                File.WriteAllText(QuotationDashboardOrderPath, order);
            }
            catch
            {
            }
        }

        private Button MakeDashButton(string text, int width, Color back, Color fore)
        {
            Button button = new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = back == QuoteSurface ? BorderColor : back;
            return button;
        }

        private Button QuickAction(string text, Action action)
        {
            Button button = MakeDashButton(text, 190, QuoteSurface, InfoBlue);
            button.Height = 42;
            button.AutoSize = false;
            button.MinimumSize = new Size(190, 42);
            button.MaximumSize = new Size(190, 42);
            button.Margin = new Padding(0, 0, 10, 8);
            button.Tag = "FIXED_WIDTH";
            button.Click += (s, e) => action();
            return button;
        }

        private Button QuickAction(string text, Func<Task> action)
        {
            Button button = MakeDashButton(text, 190, QuoteSurface, InfoBlue);
            button.Height = 42;
            button.AutoSize = false;
            button.MinimumSize = new Size(190, 42);
            button.MaximumSize = new Size(190, 42);
            button.Margin = new Padding(0, 0, 10, 8);
            button.Tag = "FIXED_WIDTH";
            button.Click += async (s, e) => await action();
            return button;
        }

        private Control MiniRow(string left, string right, int width)
        {
            Panel row = new Panel { Width = width, Height = 24, BackColor = QuoteSurface, Margin = new Padding(0, 0, 0, 4) };
            row.Paint += (s, e) =>
            {
                using (Font leftFont = new Font("Segoe UI", 8))
                using (Font rightFont = new Font("Segoe UI", 8, FontStyle.Bold))
                using (Brush leftBrush = new SolidBrush(QuoteText))
                using (Brush rightBrush = new SolidBrush(InfoBlue))
                using (StringFormat leftFormat = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                using (StringFormat rightFormat = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                {
                    e.Graphics.DrawString(left, leftFont, leftBrush, new RectangleF(0, 4, width - 104, 18), leftFormat);
                    e.Graphics.DrawString(right, rightFont, rightBrush, new RectangleF(width - 104, 4, 102, 18), rightFormat);
                }
            };
            return row;
        }

        private Control SmallMetric(string title, string value, decimal change)
        {
            Panel card = new Panel { Width = 142, Height = 62, BackColor = Color.FromArgb(250, 252, 255), Margin = new Padding(0, 0, 10, 8), Padding = new Padding(10, 6, 8, 6) };
            DS.Rounded(card, 8);
            card.Paint += (s, e) => { using (Pen p = new Pen(BorderColor)) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1); };
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = QuoteMuted, AutoSize = true, Location = new Point(10, 6) });
            card.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = QuoteText, Size = new Size(120, 20), Location = new Point(10, 24) });
            card.Controls.Add(new Label { Text = (change >= 0 ? "^ " : "v ") + Math.Abs(change).ToString("0.#") + "%", Font = new Font("Segoe UI", 7.5f), ForeColor = change >= 0 ? SaveGreen : DS.Red600, AutoSize = true, Location = new Point(10, 45) });
            return card;
        }

        private Label EmptyLabel(string text)
        {
            return new Label { Text = text, Font = new Font("Segoe UI", 8), ForeColor = QuoteMuted, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        }

        private QuotationAnalyticsGrouping ParseGrouping(string text)
        {
            if (string.Equals(text, "Day", StringComparison.OrdinalIgnoreCase))
                return QuotationAnalyticsGrouping.Day;
            if (string.Equals(text, "Month", StringComparison.OrdinalIgnoreCase))
                return QuotationAnalyticsGrouping.Month;
            return QuotationAnalyticsGrouping.Week;
        }
    }

    internal sealed class QuotationDashboardTile : Panel
    {
        private readonly Panel _header;
        private readonly Label _title;
        private readonly Button _sizeButton;
        private readonly Panel _grip;
        private readonly Panel _heightGrip;
        private Size _small;
        private Size _medium;
        private Size _large;
        private bool _resizing;
        private bool _heightOnly;
        private Point _resizeStart;
        private Size _resizeStartSize;

        public event EventHandler<MouseEventArgs> DragRequested;
        public event EventHandler ResizeCompleted;

        public QuotationDashboardTile()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Padding = new Padding(1);

            _header = new Panel
            {
                Name = CardSurfacePolicy.QuotationTileHeaderName,
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = Color.White,
                Cursor = Cursors.SizeAll,
                Padding = new Padding(0, 0, 8, 0)
            };
            _title = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                AutoEllipsis = true
            };
            _sizeButton = new Button
            {
                Text = "Resize",
                Dock = DockStyle.Right,
                Width = 0,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _sizeButton.FlatAppearance.BorderColor = DS.Border;
            _sizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            _sizeButton.Click += SizeButton_Click;
            _header.Controls.Add(_title);
            _header.Controls.Add(_sizeButton);
            _header.MouseDown += Header_MouseDown;
            _title.MouseDown += Header_MouseDown;

            ContentHost = new Panel
            {
                Name = CardSurfacePolicy.QuotationTileContentName,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10, 2, 10, 10)
            };
            _grip = new Panel
            {
                Name = CardResizeGripService.CornerGripName,
                Width = CardResizeGripService.CornerGripSize,
                Height = CardResizeGripService.CornerGripSize,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Cursor = Cursors.SizeNWSE,
                BackColor = Color.White
            };
            _grip.Paint += CornerGrip_Paint;
            _grip.MouseDown += Grip_MouseDown;
            _grip.MouseMove += Grip_MouseMove;
            _grip.MouseUp += Grip_MouseUp;

            _heightGrip = new Panel
            {
                Name = CardResizeGripService.HeightGripName,
                Width = CardResizeGripService.HeightGripWidth,
                Height = CardResizeGripService.HeightGripHeight,
                Anchor = AnchorStyles.Bottom,
                Cursor = Cursors.SizeNS,
                BackColor = Color.White
            };
            _heightGrip.Paint += HeightGrip_Paint;
            _heightGrip.MouseDown += HeightGrip_MouseDown;
            _heightGrip.MouseMove += Grip_MouseMove;
            _heightGrip.MouseUp += Grip_MouseUp;

            Controls.Add(_grip);
            Controls.Add(_heightGrip);
            Controls.Add(ContentHost);
            Controls.Add(_header);
            Resize += (s, e) =>
            {
                _sizeButton.Margin = new Padding(0, 5, 0, 5);
                PositionResizeGrips();
            };
            PositionResizeGrips();
        }

        public string CardKey { get; set; }
        public string CardTitle { get; set; }
        public string Title
        {
            get { return _title.Text; }
            set { _title.Text = value ?? string.Empty; }
        }
        public Color BorderColor { get; set; } = DS.Border;
        public Color BackgroundColor { get; set; } = Color.White;
        public Panel ContentHost { get; }

        public void ConfigurePresets(Size small, Size medium, Size large)
        {
            _small = small;
            _medium = medium;
            _large = large;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackgroundColor))
                e.Graphics.FillRectangle(brush, ClientRectangle);
            using (Pen pen = new Pen(BorderColor))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                DragRequested?.Invoke(this, e);
        }

        private void SizeButton_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Small", null, (s, args) => ApplyPreset(_small));
            menu.Items.Add("Medium", null, (s, args) => ApplyPreset(_medium));
            menu.Items.Add("Large", null, (s, args) => ApplyPreset(_large));
            menu.Show(_sizeButton, new Point(0, _sizeButton.Height));
        }

        private void ApplyPreset(Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
                return;
            Size = new Size(
                Math.Max(MinimumSize.Width, Math.Min(MaximumSize.Width, size.Width)),
                Math.Max(MinimumSize.Height, Math.Min(MaximumSize.Height, size.Height)));
            Parent?.PerformLayout();
            ResizeCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void Grip_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            _resizing = true;
            _heightOnly = false;
            _resizeStart = Control.MousePosition;
            _resizeStartSize = Size;
            _grip.Capture = true;
        }

        private void HeightGrip_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            _resizing = true;
            _heightOnly = true;
            _resizeStart = Control.MousePosition;
            _resizeStartSize = Size;
            _heightGrip.Capture = true;
        }

        private void Grip_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizing)
                return;
            Point now = Control.MousePosition;
            int width = _heightOnly
                ? _resizeStartSize.Width
                : Math.Max(MinimumSize.Width, Math.Min(MaximumSize.Width, _resizeStartSize.Width + now.X - _resizeStart.X));
            int height = Math.Max(MinimumSize.Height, Math.Min(MaximumSize.Height, _resizeStartSize.Height + now.Y - _resizeStart.Y));
            Size = new Size(width, height);
            Parent?.PerformLayout();
            PositionResizeGrips();
        }

        private void Grip_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_resizing)
                return;
            _resizing = false;
            _heightOnly = false;
            _grip.Capture = false;
            _heightGrip.Capture = false;
            ResizeCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void PositionResizeGrips()
        {
            if (!_grip.IsDisposed)
            {
                CardResizeGripService.PositionCornerGrip(this, _grip, 2);
            }

            if (!_heightGrip.IsDisposed)
            {
                CardResizeGripService.PositionHeightGrip(this, _heightGrip, 2);
            }
        }

        private void CornerGrip_Paint(object sender, PaintEventArgs e)
        {
            CardResizeGripService.PaintCornerGrip(_grip, e);
        }

        private void HeightGrip_Paint(object sender, PaintEventArgs e)
        {
            CardResizeGripService.PaintHeightGrip(_heightGrip, e);
        }
    }

    internal class QuotationOverviewChart : Control
    {
        private List<QuotationOverviewPoint> _data = new List<QuotationOverviewPoint>();
        public void SetData(List<QuotationOverviewPoint> data) { _data = data ?? new List<QuotationOverviewPoint>(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.White);
            if (!_data.Any()) { DrawEmpty(e.Graphics); return; }
            Rectangle area = new Rectangle(34, 18, Width - 52, Height - 42);
            int max = Math.Max(1, _data.Max(p => Math.Max(p.TotalCount, p.ConvertedCount)));
            using (Pen grid = new Pen(Color.FromArgb(229, 235, 245)))
                for (int i = 0; i <= 3; i++) e.Graphics.DrawLine(grid, area.Left, area.Top + i * area.Height / 3, area.Right, area.Top + i * area.Height / 3);
            int slot = Math.Max(24, area.Width / Math.Max(1, _data.Count));
            for (int i = 0; i < _data.Count; i++)
            {
                int x = area.Left + i * slot + slot / 4;
                DrawBar(e.Graphics, x, area.Bottom, 8, area.Height * _data[i].TotalCount / max, Color.FromArgb(59, 130, 246));
                DrawBar(e.Graphics, x + 10, area.Bottom, 8, area.Height * _data[i].ConvertedCount / max, Color.FromArgb(34, 197, 94));
                DrawBar(e.Graphics, x + 20, area.Bottom, 8, area.Height * _data[i].PendingCount / max, Color.FromArgb(249, 115, 22));
                e.Graphics.DrawString(_data[i].Period, new Font("Segoe UI", 6.5f), Brushes.Gray, x - 4, area.Bottom + 4);
            }
        }
        private static void DrawBar(Graphics g, int x, int bottom, int width, int height, Color color) { using (Brush b = new SolidBrush(color)) g.FillRectangle(b, x, bottom - height, width, height); }
        private void DrawEmpty(Graphics g) { QuotationDashboardPaint.DrawEmpty(g, ClientRectangle, "No quotation data"); }
    }

    internal class QuotationValueTrendChart : Control
    {
        private List<QuotationTrendPoint> _data = new List<QuotationTrendPoint>();
        public void SetData(List<QuotationTrendPoint> data) { _data = data ?? new List<QuotationTrendPoint>(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.White);
            if (_data.Count < 2) { QuotationDashboardPaint.DrawEmpty(e.Graphics, ClientRectangle, "No value trend"); return; }
            Rectangle area = new Rectangle(32, 18, Width - 48, Height - 42);
            decimal max = Math.Max(1m, _data.Max(p => p.Value));
            PointF[] points = _data.Select((p, i) => new PointF(area.Left + i * area.Width / Math.Max(1, _data.Count - 1), area.Bottom - (float)(p.Value / max) * area.Height)).ToArray();
            using (Pen grid = new Pen(Color.FromArgb(229, 235, 245)))
                for (int i = 0; i <= 3; i++) e.Graphics.DrawLine(grid, area.Left, area.Top + i * area.Height / 3, area.Right, area.Top + i * area.Height / 3);
            using (Pen line = new Pen(Color.FromArgb(124, 58, 237), 2))
                e.Graphics.DrawLines(line, points);
            foreach (PointF p in points)
                using (Brush b = new SolidBrush(Color.FromArgb(124, 58, 237))) e.Graphics.FillEllipse(b, p.X - 2, p.Y - 2, 4, 4);
        }
    }

    internal class QuotationStatusDonut : Control
    {
        private List<QuotationStatusSlice> _data = new List<QuotationStatusSlice>();
        public void SetData(List<QuotationStatusSlice> data) { _data = data ?? new List<QuotationStatusSlice>(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.White);
            int total = _data.Sum(s => s.Count);
            if (total == 0) { QuotationDashboardPaint.DrawEmptyDonut(e.Graphics, ClientRectangle, "No status data"); return; }
            Rectangle pie = new Rectangle(18, 28, Math.Min(Width / 2, Height - 42), Math.Min(Width / 2, Height - 42));
            float start = -90;
            foreach (QuotationStatusSlice s in _data.Where(s => s.Count > 0))
            {
                float sweep = (float)(s.Count * 360.0 / total);
                using (Brush b = new SolidBrush(s.Color)) e.Graphics.FillPie(b, pie, start, sweep);
                start += sweep;
            }
            using (Brush b = new SolidBrush(Color.White)) e.Graphics.FillEllipse(b, pie.Left + pie.Width / 4, pie.Top + pie.Height / 4, pie.Width / 2, pie.Height / 2);
            e.Graphics.DrawString(total.ToString(), new Font("Segoe UI", 14, FontStyle.Bold), Brushes.Black, pie.Left + pie.Width / 2 - 14, pie.Top + pie.Height / 2 - 18);
            int y = 30;
            foreach (QuotationStatusSlice s in _data)
            {
                using (Brush b = new SolidBrush(s.Color)) e.Graphics.FillEllipse(b, Width - 116, y + 4, 7, 7);
                e.Graphics.DrawString(s.Status + "  " + s.Count, new Font("Segoe UI", 7.5f), Brushes.DimGray, Width - 104, y);
                y += 16;
            }
        }
    }

    internal class QuotationLostReasonDonut : Control
    {
        private List<QuotationLostReasonSlice> _data = new List<QuotationLostReasonSlice>();
        public void SetData(List<QuotationLostReasonSlice> data) { _data = data ?? new List<QuotationLostReasonSlice>(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.White);
            int total = _data.Sum(s => s.Count);
            if (total == 0) { QuotationDashboardPaint.DrawEmptyDonut(e.Graphics, ClientRectangle, "No lost quotations"); return; }
            Rectangle pie = new Rectangle(18, 30, Math.Min(Width / 2, Height - 54), Math.Min(Width / 2, Height - 54));
            float start = -90;
            foreach (QuotationLostReasonSlice s in _data.Where(s => s.Count > 0))
            {
                float sweep = (float)(s.Count * 360.0 / total);
                using (Brush b = new SolidBrush(s.Color)) e.Graphics.FillPie(b, pie, start, sweep);
                start += sweep;
            }
            using (Brush b = new SolidBrush(Color.White)) e.Graphics.FillEllipse(b, pie.Left + pie.Width / 4, pie.Top + pie.Height / 4, pie.Width / 2, pie.Height / 2);
            e.Graphics.DrawString(total.ToString(), new Font("Segoe UI", 13, FontStyle.Bold), Brushes.Black, pie.Left + pie.Width / 2 - 10, pie.Top + pie.Height / 2 - 16);
            int y = 34;
            foreach (QuotationLostReasonSlice s in _data.Where(x => x.Count > 0))
            {
                using (Brush b = new SolidBrush(s.Color)) e.Graphics.FillEllipse(b, Width - 122, y + 4, 7, 7);
                e.Graphics.DrawString(s.Reason + " " + s.Percentage.ToString("0") + "%", new Font("Segoe UI", 7.2f), Brushes.DimGray, Width - 110, y);
                y += 18;
            }
        }
    }

    internal class QuotationFunnelChart : Control
    {
        private List<QuotationFunnelStage> _data = new List<QuotationFunnelStage>();
        public void SetData(List<QuotationFunnelStage> data) { _data = data ?? new List<QuotationFunnelStage>(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.White);
            if (!_data.Any()) { QuotationDashboardPaint.DrawEmpty(e.Graphics, ClientRectangle, "No funnel data"); return; }
            int max = Math.Max(1, _data.Max(d => d.Count));
            int center = Width / 2 - 20;
            int y = 28;
            int h = Math.Max(18, (Height - 54) / _data.Count);
            foreach (QuotationFunnelStage stage in _data)
            {
                int w = Math.Max(42, (int)((Width * 0.58) * stage.Count / max));
                Point[] shape =
                {
                    new Point(center - w / 2, y),
                    new Point(center + w / 2, y),
                    new Point(center + w / 2 - 10, y + h - 3),
                    new Point(center - w / 2 + 10, y + h - 3)
                };
                using (Brush b = new SolidBrush(stage.Color)) e.Graphics.FillPolygon(b, shape);
                e.Graphics.DrawString(stage.Count + "  " + stage.Stage, new Font("Segoe UI", 7.5f), Brushes.DimGray, center + Width / 4, y + 3);
                y += h;
            }
        }
    }

    internal static class QuotationDashboardPaint
    {
        public static void DrawEmpty(Graphics g, Rectangle bounds, string text)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle icon = new Rectangle(bounds.Left + bounds.Width / 2 - 20, bounds.Top + bounds.Height / 2 - 34, 40, 32);
            using (Pen pen = new Pen(DS.Border, 1f))
            {
                g.DrawLine(pen, icon.Left + 6, icon.Bottom - 4, icon.Right - 6, icon.Bottom - 4);
                g.DrawRectangle(pen, icon.Left + 10, icon.Top + 13, 5, 13);
                g.DrawRectangle(pen, icon.Left + 18, icon.Top + 6, 5, 20);
                g.DrawRectangle(pen, icon.Left + 26, icon.Top + 16, 5, 10);
            }
            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(text, font, brush, new RectangleF(bounds.Left + 12, icon.Bottom + 8, bounds.Width - 24, 24), format);
            }
        }

        public static void DrawEmptyDonut(Graphics g, Rectangle bounds, string text)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = Math.Min(94, Math.Max(58, Math.Min(bounds.Width, bounds.Height) - 64));
            Rectangle ring = new Rectangle(bounds.Left + bounds.Width / 2 - size / 2, bounds.Top + bounds.Height / 2 - size / 2 - 14, size, size);
            using (Pen pen = new Pen(Color.FromArgb(229, 235, 245), Math.Max(10, size / 8)))
                g.DrawEllipse(pen, ring);
            using (Font font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(text, font, brush, new RectangleF(bounds.Left + 12, ring.Bottom + 12, bounds.Width - 24, 24), format);
            }
        }

        public static void DrawGridEmpty(Graphics g, Rectangle bounds, string title, string subtitle)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            int iconSize = 42;
            int totalHeight = 86;
            int top = bounds.Top + Math.Max(4, (bounds.Height - totalHeight) / 2);
            Rectangle icon = new Rectangle(bounds.Left + bounds.Width / 2 - iconSize / 2, top, iconSize, iconSize);
            using (Pen pen = new Pen(DS.Border, 1f))
            {
                g.DrawRectangle(pen, icon.Left + 9, icon.Top + 5, 24, 31);
                g.DrawLine(pen, icon.Left + 15, icon.Top + 15, icon.Right - 12, icon.Top + 15);
                g.DrawLine(pen, icon.Left + 15, icon.Top + 23, icon.Right - 12, icon.Top + 23);
                g.DrawLine(pen, icon.Left + 15, icon.Top + 31, icon.Right - 18, icon.Top + 31);
            }

            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
            {
                using (Font font = new Font("Segoe UI", 8.8f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.FromArgb(71, 85, 105)))
                    g.DrawString(title, font, brush, new RectangleF(bounds.Left + 20, icon.Bottom + 8, bounds.Width - 40, 20), format);

                using (Font font = new Font("Segoe UI", 8f))
                using (Brush brush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    g.DrawString(subtitle, font, brush, new RectangleF(bounds.Left + 20, icon.Bottom + 28, bounds.Width - 40, 22), format);
            }
        }
    }
}
