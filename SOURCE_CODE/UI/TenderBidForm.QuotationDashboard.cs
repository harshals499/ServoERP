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
        private QuotationStatusDonut _quoteStatusDonut;
        private QuotationFunnelChart _quoteFunnelChart;
        private DataGridView _quoteRecentGrid;
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
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildQuoteDashboardHeader(), 0, 0);
            _quoteDashKpis = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuotePageBg, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 4, 0, 2) };
            root.Controls.Add(_quoteDashKpis, 0, 1);
            root.Controls.Add(BuildQuoteDashboardCardBoard(), 0, 2);

            _quotationDashboardPanel.Controls.Add(root);
            _quotationDashboardPanel.HandleCreated += (s, e) =>
            {
                ApplySavedQuotationDashboardLayout();
                RefreshQuotationDashboardSafe();
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
            _quoteStatusDonut = new QuotationStatusDonut { Dock = DockStyle.Fill };
            _quoteFunnelChart = new QuotationFunnelChart { Dock = DockStyle.Fill };
            _quoteTopClients = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4), BackColor = QuoteSurface };
            _quoteRecentGrid = CreateRecentQuotationGrid();
            _quoteBusinessCards = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
            _quoteValueTrend = new QuotationValueTrendChart { Dock = DockStyle.Fill };
            _quoteTopItems = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4), BackColor = QuoteSurface };
            _quoteFollowUps = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4), BackColor = QuoteSurface };
            _quoteLostDonut = new QuotationLostReasonDonut { Dock = DockStyle.Fill };
            _quoteInsights = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
            FlowLayoutPanel quick = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = true, AutoScroll = true, Padding = new Padding(4) };
            quick.Controls.Add(QuickAction("New Quotation", () => NewRecord()));
            quick.Controls.Add(QuickAction("Create from Template", () => NewRecord()));
            quick.Controls.Add(QuickAction("Create Customer Invoice", async () => await CreateInvoiceAsync()));
            quick.Controls.Add(QuickAction("Send Supplier PO", async () => await CreatePurchaseOrdersAsync()));
            quick.Controls.Add(QuickAction("Duplicate Quotation", () => DuplicateCurrentQuotation()));
            quick.Controls.Add(QuickAction("Quotation Report", () => OnNavigate?.Invoke(12)));
            quick.Controls.Add(QuickAction("Export Data", () => MessageBox.Show("Use the quotation PDF/export tools after selecting a quotation.", "Export Data", MessageBoxButtons.OK, MessageBoxIcon.Information)));

            AddDashboardCard("overview", "Overview", _quoteOverviewChart, 430, 206, "Large");
            AddDashboardCard("status", "Status", _quoteStatusDonut, 360, 206, "Medium");
            AddDashboardCard("funnel", "Conversion", _quoteFunnelChart, 360, 206, "Medium");
            AddDashboardCard("top_clients", "Top Clients", _quoteTopClients, 360, 206, "Medium");
            AddDashboardCard("recent", "Recent Quotations", _quoteRecentGrid, 720, 226, "Full width");
            AddDashboardCard("business", "Business Health", _quoteBusinessCards, 360, 226, "Medium");
            AddDashboardCard("trend", "Value Trend", _quoteValueTrend, 430, 196, "Large");
            AddDashboardCard("top_items", "Top Items", _quoteTopItems, 360, 196, "Medium");
            AddDashboardCard("followups", "Follow Ups", _quoteFollowUps, 360, 196, "Medium");
            AddDashboardCard("lost_reasons", "Lost Reasons", _quoteLostDonut, 360, 196, "Medium");
            AddDashboardCard("insights", "Insights", _quoteInsights, 520, 168, "Large");
            AddDashboardCard("quick_actions", "Quick Actions", quick, 450, 168, "Large");

            _quoteDashboardCards.Resize += (s, e) => LayoutQuotationDashboardCards();
            ApplySavedQuotationDashboardOrder();
            return _quoteDashboardCards;
        }

        private Control BuildQuoteDashboardHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = QuotePageBg };
            header.Controls.Add(new Label
            {
                Text = "Quotation Dashboard",
                Location = new Point(0, 4),
                AutoSize = true,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = QuoteText
            });
            header.Controls.Add(new Label
            {
                Text = "Overview of all quotations and business analytics",
                Location = new Point(0, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = QuoteMuted
            });

            Button more = MakeDashButton("...", 38, QuoteSurface, QuoteText);
            more.Location = new Point(header.Width - 40, 10);
            more.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            more.Click += (s, e) => MessageBox.Show("Quotation dashboard actions are available from the quick action cards below.", "Quotation Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Button newQuote = MakeDashButton("+ New Quotation", 154, CargoPurple, Color.White);
            newQuote.Location = new Point(header.Width - 202, 10);
            newQuote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            newQuote.Click += (s, e) => NewRecord();

            Button refresh = MakeDashButton("Refresh", 82, QuoteSurface, InfoBlue);
            refresh.Location = new Point(header.Width - 292, 10);
            refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refresh.Click += (s, e) => RefreshQuotationDashboardSafe();

            _quoteDashGroup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 94, Height = 30, Location = new Point(header.Width - 520, 12), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _quoteDashGroup.Items.AddRange(new object[] { "Week", "Day", "Month" });
            _quoteDashGroup.SelectedIndex = 0;
            _quoteDashGroup.SelectedIndexChanged += (s, e) => RefreshQuotationDashboardSafe();

            DateTime today = DateTime.Today;
            _quoteDashFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 112, Height = 30, Value = new DateTime(today.Year, today.Month, 1), Location = new Point(header.Width - 760, 12), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _quoteDashTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 112, Height = 30, Value = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)), Location = new Point(header.Width - 640, 12), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _quoteDashFrom.ValueChanged += (s, e) => RefreshQuotationDashboardSafe();
            _quoteDashTo.ValueChanged += (s, e) => RefreshQuotationDashboardSafe();

            _quoteDashStatus = new Label { AutoSize = true, ForeColor = QuoteMuted, Font = new Font("Segoe UI", 8), Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(header.Width - 910, 18), Text = "Loading..." };

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
            grid.GridColor = Color.FromArgb(226, 232, 240);
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

            _quoteOverviewChart.SetData(snapshot.Overview);
            _quoteStatusDonut.SetData(snapshot.Statuses);
            _quoteFunnelChart.SetData(snapshot.Funnel);
            _quoteValueTrend.SetData(snapshot.ValueTrend);
            _quoteLostDonut.SetData(snapshot.LostReasons);
            BindTopClients(snapshot.TopClients);
            BindRecentQuotations(snapshot.RecentQuotations);
            BindBusinessCards(snapshot.Kpis);
            BindTopItems(snapshot.TopItems);
            BindFollowUps(snapshot.UpcomingFollowUps);
            BindInsights(snapshot.Insights);
        }

        private void AddKpi(QuotationKpi kpi, bool currency, string format)
        {
            Panel card = new Panel { Width = 168, Height = 78, BackColor = QuoteSurface, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(12, 10, 10, 8) };
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
            foreach (QuotationRecentRow row in rows ?? new List<QuotationRecentRow>())
                _quoteRecentGrid.Rows.Add(row.BidId, row.QuotationNumber, row.ClientName, row.SiteName, IndiaFormatHelper.FormatDate(row.QuotationDate), IndiaFormatHelper.FormatCurrency(row.Value), row.CommercialFlow, row.CustomerDocumentStatus, row.SupplierDocumentStatus, row.Status);
            _quoteRecentGrid.Invalidate();
        }

        private void QuoteRecentGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string column = _quoteRecentGrid.Columns[e.ColumnIndex].Name;
            if (column == "Pdf" || column == "Edit" || column == "Convert")
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
            if (column != "Pdf" && column != "Edit" && column != "Convert")
                return;
            int bidId;
            if (!int.TryParse(Convert.ToString(_quoteRecentGrid.Rows[e.RowIndex].Cells["BidId"].Value), out bidId) || bidId <= 0)
                return;
            if (column == "Pdf")
            {
                RecentDocumentOpenService.OpenQuotationPdf(this, bidId);
                return;
            }

            await LoadQuotationFromDashboardAsync(bidId);
            if (column == "Convert")
                await CreateInvoiceAsync();
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
                MinimumSize = new Size(340, 148),
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
            int third = Math.Max(340, (usable - gap * 2) / 3);
            int half = Math.Max(430, (usable - gap) / 2);
            foreach (QuotationDashboardTile card in _quoteDashboardCards.Controls.OfType<QuotationDashboardTile>())
            {
                Size baseSize;
                if (!_quoteDashboardBaseSizes.TryGetValue(card.CardKey ?? string.Empty, out baseSize))
                    baseSize = card.Size;

                bool fullWidth = IsAny(card.CardKey, "quote_dash_recent");
                bool wide = IsAny(card.CardKey, "quote_dash_overview", "quote_dash_trend", "quote_dash_business", "quote_dash_insights", "quote_dash_quick_actions");
                int targetWidth = fullWidth ? usable : wide ? half : third;
                targetWidth = Math.Max(card.MinimumSize.Width, Math.Min(card.MaximumSize.Width, targetWidth));

                if (Math.Abs(card.Width - targetWidth) > 4)
                    card.Width = targetWidth;

                if (card.Height < baseSize.Height)
                    card.Height = baseSize.Height;
            }
            int contentHeight = 0;
            foreach (Control child in _quoteDashboardCards.Controls)
                contentHeight = Math.Max(contentHeight, child.Bottom + child.Margin.Bottom + 18);
            _quoteDashboardCards.AutoScrollMinSize = new Size(0, Math.Max(_quoteDashboardCards.ClientSize.Height, contentHeight));
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
            Button button = MakeDashButton(text, 142, QuoteSurface, InfoBlue);
            button.Height = 42;
            button.Margin = new Padding(0, 0, 10, 8);
            button.Click += (s, e) => action();
            return button;
        }

        private Button QuickAction(string text, Func<Task> action)
        {
            Button button = MakeDashButton(text, 142, QuoteSurface, InfoBlue);
            button.Height = 42;
            button.Margin = new Padding(0, 0, 10, 8);
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
        private Size _small;
        private Size _medium;
        private Size _large;
        private bool _resizing;
        private Point _resizeStart;
        private Size _resizeStartSize;

        public event EventHandler<MouseEventArgs> DragRequested;
        public event EventHandler ResizeCompleted;

        public QuotationDashboardTile()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Padding = new Padding(1);

            _header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.White, Cursor = Cursors.SizeAll, Padding = new Padding(0, 0, 8, 0) };
            _title = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
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
            _sizeButton.FlatAppearance.BorderColor = Color.FromArgb(221, 227, 234);
            _sizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            _sizeButton.Click += SizeButton_Click;
            _header.Controls.Add(_title);
            _header.Controls.Add(_sizeButton);
            _header.MouseDown += Header_MouseDown;
            _title.MouseDown += Header_MouseDown;

            ContentHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12, 6, 12, 12) };
            _grip = new Panel
            {
                Width = 1,
                Height = 1,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Cursor = Cursors.SizeNWSE,
                BackColor = Color.White,
                Visible = false
            };
            _grip.MouseDown += Grip_MouseDown;
            _grip.MouseMove += Grip_MouseMove;
            _grip.MouseUp += Grip_MouseUp;

            Controls.Add(_grip);
            Controls.Add(ContentHost);
            Controls.Add(_header);
            Resize += (s, e) =>
            {
                _sizeButton.Margin = new Padding(0, 5, 0, 5);
                _grip.Location = new Point(Math.Max(1, Width - _grip.Width - 2), Math.Max(1, Height - _grip.Height - 2));
            };
        }

        public string CardKey { get; set; }
        public string CardTitle { get; set; }
        public string Title
        {
            get { return _title.Text; }
            set { _title.Text = value ?? string.Empty; }
        }
        public Color BorderColor { get; set; } = Color.FromArgb(221, 227, 234);
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
            _resizeStart = Control.MousePosition;
            _resizeStartSize = Size;
            _grip.Capture = true;
        }

        private void Grip_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizing)
                return;
            Point now = Control.MousePosition;
            int width = Math.Max(MinimumSize.Width, Math.Min(MaximumSize.Width, _resizeStartSize.Width + now.X - _resizeStart.X));
            int height = Math.Max(MinimumSize.Height, Math.Min(MaximumSize.Height, _resizeStartSize.Height + now.Y - _resizeStart.Y));
            Size = new Size(width, height);
            Parent?.PerformLayout();
        }

        private void Grip_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_resizing)
                return;
            _resizing = false;
            _grip.Capture = false;
            ResizeCompleted?.Invoke(this, EventArgs.Empty);
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
            using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 2f))
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
            using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 2f))
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
