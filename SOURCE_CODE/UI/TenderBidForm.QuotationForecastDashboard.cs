using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class TenderBidForm
    {
        private readonly Dictionary<string, Label> _forecastKpiValues = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Label> _forecastKpiSubtitles = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private readonly List<QuotationRecentRow> _forecastRows = new List<QuotationRecentRow>();
        private readonly List<Button> _forecastActionChips = new List<Button>();
        private readonly List<Button> _forecastTableChips = new List<Button>();
        private QuotationPipelineForecastChart _forecastChart;
        private QuotationStageFunnelCard _forecastFunnel;
        private FlowLayoutPanel _forecastFollowUps;
        private FlowLayoutPanel _forecastInsights;
        private QuotationConfidenceBar _forecastConfidence;
        private DataGridView _forecastGrid;
        private TextBox _forecastSearch;
        private Label _forecastRowCount;
        private TableLayoutPanel _forecastRoot;
        private string _forecastActionFilter = "Overdue";
        private string _forecastTableFilter = "All";

        private Panel BuildQuotationForecastDashboardPanel()
        {
            _quotationDashboardPanel = new Panel
            {
                Name = "QuotationForecastDashboard",
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = QuotePageBg,
                Padding = new Padding(16, 8, 16, 14)
            };
            _forecastRoot = new TableLayoutPanel
            {
                Name = "QuotationForecastDashboardRoot",
                BackColor = QuotePageBg,
                ColumnCount = 1,
                RowCount = 5,
                Location = new Point(16, 8),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _forecastRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _forecastRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            _forecastRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            _forecastRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            _forecastRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            _forecastRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _forecastRoot.Controls.Add(BuildForecastFilterBar(), 0, 0);
            _forecastRoot.Controls.Add(BuildForecastKpis(), 0, 1);
            _forecastRoot.Controls.Add(BuildForecastAnalytics(), 0, 2);
            _forecastRoot.Controls.Add(BuildForecastActions(), 0, 3);
            _forecastRoot.Controls.Add(BuildForecastTable(), 0, 4);
            _quotationDashboardPanel.Controls.Add(_forecastRoot);
            _quotationDashboardPanel.Resize += (s, e) => LayoutForecastDashboard();
            _quotationDashboardPanel.HandleCreated += (s, e) =>
            {
                LayoutForecastDashboard();
                if (!_visualTestMode)
                    BeginInvoke((Action)RefreshQuotationDashboardSafe);
            };
            return _quotationDashboardPanel;
        }

        private void LayoutForecastDashboard()
        {
            if (_quotationDashboardPanel == null || _forecastRoot == null)
                return;
            int width = Math.Max(1080, _quotationDashboardPanel.ClientSize.Width - _quotationDashboardPanel.Padding.Horizontal);
            int height = Math.Max(700, _quotationDashboardPanel.ClientSize.Height - _quotationDashboardPanel.Padding.Vertical);
            _forecastRoot.Size = new Size(width, height);
            _quotationDashboardPanel.AutoScrollMinSize = new Size(width + 8, height + 8);
        }

        private Control BuildForecastFilterBar()
        {
            var bar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = QuotePageBg, ColumnCount = 6, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 5) };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
            bar.Controls.Add(new Label { Text = "Forecast & Pipeline", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = QuoteText, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _quoteDashCompany = ForecastCombo(184);
            _quoteDashCompany.Name = "QuotationDashboardCompanyFilter";
            _quoteDashCompany.Items.Add("All companies");
            _quoteDashCompany.SelectedIndex = 0;
            _quoteDashCompany.SelectedIndexChanged += QuoteDashboardCompany_SelectedIndexChanged;
            _quoteDashMonths = ForecastCombo(116);
            _quoteDashMonths.Name = "QuotationDashboardMonthFilter";
            _quoteDashMonths.Items.AddRange(new object[] { "3 months", "6 months", "12 months" });
            _quoteDashMonths.SelectedItem = "6 months";
            _quoteDashMonths.SelectedIndexChanged += (s, e) => RefreshQuotationDashboardSafe();
            Button refresh = ForecastButton("Refresh", 92, QuoteSurface, InfoBlue);
            ModernIconSystem.AddButtonIcon(refresh, ModernIconKind.Refresh);
            refresh.Click += (s, e) => RefreshQuotationDashboardSafe();
            Button create = ForecastButton("New Quotation", 142, InfoBlue, Color.White);
            create.Image = LucideIconService.GetIcon("plus.svg", 16, Color.White);
            create.TextImageRelation = TextImageRelation.ImageBeforeText;
            create.Click += (s, e) => NewRecord();
            _quoteDashStatus = new Label { Text = "Loading...", Width = 112, Height = 30, Font = new Font("Segoe UI", 8f), ForeColor = QuoteMuted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            Control[] controls = { _quoteDashCompany, _quoteDashMonths, refresh, create, _quoteDashStatus };
            for (int i = 0; i < controls.Length; i++)
            {
                controls[i].Dock = DockStyle.Fill;
                controls[i].Margin = new Padding(0, 0, 8, 0);
                bar.Controls.Add(controls[i], i + 1, 0);
            }
            return bar;
        }

        private Control BuildForecastKpis()
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = QuotePageBg, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 8) };
            for (int i = 0; i < 5; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            row.Controls.Add(ForecastKpi("pipeline", "Open Pipeline", ModernIconKind.Document, InfoBlue, Color.FromArgb(235, 243, 255)), 0, 0);
            row.Controls.Add(ForecastKpi("weighted", "Weighted Forecast", ModernIconKind.Analytics, SaveGreen, Color.FromArgb(232, 250, 243)), 1, 0);
            row.Controls.Add(ForecastKpi("winrate", "Win Rate", ModernIconKind.Status, CargoPurple, Color.FromArgb(242, 238, 255)), 2, 0);
            row.Controls.Add(ForecastKpi("cycle", "Avg. Sales Cycle", ModernIconKind.Calendar, Color.FromArgb(234, 88, 12), Color.FromArgb(255, 245, 235)), 3, 0);
            row.Controls.Add(ForecastKpi("expiring", "Expiring in 7 days", ModernIconKind.Alert, Color.FromArgb(220, 38, 38), Color.FromArgb(255, 236, 236)), 4, 0);
            return row;
        }

        private Control ForecastKpi(string key, string title, ModernIconKind icon, Color accent, Color iconBack)
        {
            var card = new QuotationForecastCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(70, 10, 8, 8) };
            card.Controls.Add(new PictureBox { Location = new Point(14, 17), Size = new Size(42, 42), BackColor = iconBack, Image = ModernIconSystem.IconBitmap(icon, 22, accent), SizeMode = PictureBoxSizeMode.CenterImage });
            Label titleLabel = new Label { Text = title, Location = new Point(70, 11), Height = 18, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), ForeColor = QuoteText, AutoEllipsis = true };
            Label value = new Label { Text = "—", Location = new Point(70, 31), Height = 27, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
            Label sub = new Label { Text = "Waiting for live data", Location = new Point(70, 63), Height = 17, Font = new Font("Segoe UI", 7.5f), ForeColor = QuoteMuted, AutoEllipsis = true };
            card.Controls.Add(titleLabel); card.Controls.Add(value); card.Controls.Add(sub);
            card.Resize += (s, e) => { int w = Math.Max(72, card.ClientSize.Width - 80); titleLabel.Width = w; value.Width = w; sub.Width = w; };
            _forecastKpiValues[key] = value; _forecastKpiSubtitles[key] = sub;
            return card;
        }

        private Control BuildForecastAnalytics()
        {
            var row = SplitRow(66f, 34f, 10);
            _forecastChart = new QuotationPipelineForecastChart { Dock = DockStyle.Fill, BackColor = QuoteSurface };
            _forecastFunnel = new QuotationStageFunnelCard { Dock = DockStyle.Fill, BackColor = QuoteSurface };
            row.Controls.Add(ForecastSection("Pipeline Forecast", _forecastChart, ModernIconKind.Analytics, InfoBlue, 10), 0, 0);
            row.Controls.Add(ForecastSection("Stage Funnel", _forecastFunnel, ModernIconKind.Filter, InfoBlue, 0), 1, 0);
            return row;
        }

        private Control BuildForecastActions()
        {
            var row = SplitRow(66f, 34f, 0);
            row.Controls.Add(BuildFollowUpCard(), 0, 0);
            row.Controls.Add(BuildInsightCard(), 1, 0);
            return row;
        }

        private TableLayoutPanel SplitRow(float left, float right, int bottomPadding)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = QuotePageBg, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, bottomPadding) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, left)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, right));
            return row;
        }

        private Control BuildFollowUpCard()
        {
            var card = new QuotationForecastCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 10), Padding = new Padding(12, 44, 12, 8) };
            AddSectionHeader(card, "Priority Follow-ups", ModernIconKind.Alert, Color.FromArgb(220, 38, 38));
            var chips = new FlowLayoutPanel { Location = new Point(176, 7), Height = 31, AutoSize = true, BackColor = Color.Transparent, WrapContents = false };
            foreach (string text in new[] { "Overdue", "Expiring", "High Value" })
            {
                Button chip = ForecastChip(text, text == "Overdue");
                chip.Tag = text.Replace(" ", string.Empty);
                chip.Click += (s, e) => { _forecastActionFilter = Convert.ToString(((Button)s).Tag); UpdateChips(_forecastActionChips, _forecastActionFilter); BindForecastFollowUps(); };
                _forecastActionChips.Add(chip); chips.Controls.Add(chip);
            }
            card.Controls.Add(chips);
            _forecastFollowUps = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, BackColor = QuoteSurface, Padding = Padding.Empty, Margin = Padding.Empty };
            _forecastFollowUps.Resize += (s, e) => ResizeFollowUps();
            card.Controls.Add(_forecastFollowUps);
            return card;
        }

        private Control BuildInsightCard()
        {
            var card = new QuotationForecastCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(12, 42, 12, 10) };
            AddSectionHeader(card, "Forecast Insights", ModernIconKind.Analytics, WarnOrange);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 65f)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 35f));
            _forecastInsights = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = QuoteSurface, WrapContents = false, Margin = Padding.Empty };
            _forecastInsights.Resize += (s, e) => ResizeInsightTiles();
            _forecastConfidence = new QuotationConfidenceBar { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) };
            body.Controls.Add(_forecastInsights, 0, 0); body.Controls.Add(_forecastConfidence, 0, 1); card.Controls.Add(body);
            return card;
        }

        private Control BuildForecastTable()
        {
            var card = new QuotationForecastCard { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 48, 0, 0) };
            AddSectionHeader(card, "Active Quotations", ModernIconKind.Document, InfoBlue);
            var chips = new FlowLayoutPanel { Location = new Point(170, 8), Height = 31, AutoSize = true, BackColor = Color.Transparent, WrapContents = false };
            foreach (string text in new[] { "All", "My Quotations", "Overdue", "Expiring", "High Value" })
            {
                Button chip = ForecastChip(text, text == "All"); chip.Tag = text;
                chip.Click += (s, e) => { _forecastTableFilter = Convert.ToString(((Button)s).Tag); UpdateChips(_forecastTableChips, _forecastTableFilter); BindForecastRows(); };
                _forecastTableChips.Add(chip); chips.Controls.Add(chip);
            }
            card.Controls.Add(chips);
            _forecastSearch = new TextBox { Width = 210, Height = 28, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = QuoteText };
            _forecastSearch.TextChanged += (s, e) => BindForecastRows();
            _forecastRowCount = new Label { Width = 70, Height = 28, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = QuoteMuted, TextAlign = ContentAlignment.MiddleRight };
            card.Controls.Add(_forecastSearch); card.Controls.Add(_forecastRowCount);
            card.Resize += (s, e) => { _forecastRowCount.Location = new Point(Math.Max(760, card.ClientSize.Width - 302), 9); _forecastSearch.Location = new Point(Math.Max(830, card.ClientSize.Width - 224), 9); };
            _forecastGrid = CreateForecastGrid(); card.Controls.Add(_forecastGrid);
            return card;
        }

        private DataGridView CreateForecastGrid()
        {
            var grid = new DataGridView { Name = "QuotationForecastGrid", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackgroundColor = QuoteSurface, RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 32 };
            GridTheme.Apply(grid); grid.RowTemplate.Height = 30; grid.GridColor = Color.FromArgb(232, 237, 244);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252); grid.ColumnHeadersDefaultCellStyle.ForeColor = QuoteMuted; grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.1f); grid.DefaultCellStyle.ForeColor = QuoteText; grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 243, 255); grid.DefaultCellStyle.SelectionForeColor = QuoteText;
            EnsureForecastGridColumns(grid);
            grid.CellFormatting += ForecastGridFormatting;
            grid.CellDoubleClick += async (s, e) => { int id = GridBidId(e.RowIndex); if (id > 0) await LoadQuotationFromDashboardAsync(id); };
            grid.CellContentClick += ForecastGridAction;
            return grid;
        }

        private void BindQuotationForecastDashboard(QuotationDashboardSnapshot snapshot)
        {
            _forecastRows.Clear(); _forecastRows.AddRange(snapshot.RecentQuotations ?? new List<QuotationRecentRow>());
            SetKpi("pipeline", CompactCurrency(snapshot.Kpis.RevenuePipeline.Value), snapshot.Kpis.TotalQuotations.Value.ToString("0") + " quotations in period");
            SetKpi("weighted", CompactCurrency(snapshot.Kpis.WeightedPipeline.Value), "Based on stage probability");
            SetKpi("winrate", snapshot.Kpis.WinRate.Value.ToString("0.#") + "%", "Won vs. closed quotations");
            SetKpi("cycle", snapshot.Kpis.AverageSalesCycle.Value.ToString("0.#") + " days", "From first quote to closure");
            List<QuotationActionItem> expiring = snapshot.ActionItems.Where(a => a.Category == "Expiring").GroupBy(a => a.BidId).Select(g => g.First()).ToList();
            SetKpi("expiring", expiring.Count.ToString(), CompactCurrency(expiring.Sum(a => a.Value)) + " at risk");
            _forecastChart.SetData(snapshot.ValueTrend); _forecastFunnel.SetData(snapshot.Funnel);
            BindForecastFollowUps(); BindForecastInsights(snapshot); BindForecastRows();
        }

        private void SetKpi(string key, string value, string subtitle)
        {
            Label label; if (_forecastKpiValues.TryGetValue(key, out label)) label.Text = value;
            if (_forecastKpiSubtitles.TryGetValue(key, out label)) label.Text = subtitle;
        }

        private void BindForecastFollowUps()
        {
            if (_forecastFollowUps == null) return;
            _forecastFollowUps.SuspendLayout(); _forecastFollowUps.Controls.Clear();
            IEnumerable<QuotationActionItem> items = (_quoteDashboardSnapshot == null ? Enumerable.Empty<QuotationActionItem>() : _quoteDashboardSnapshot.ActionItems).Where(a => string.Equals(a.Category, _forecastActionFilter, StringComparison.OrdinalIgnoreCase)).GroupBy(a => a.BidId).Select(g => g.First()).Take(5);
            foreach (QuotationActionItem item in items) _forecastFollowUps.Controls.Add(FollowUpRow(item));
            if (_forecastFollowUps.Controls.Count == 0) _forecastFollowUps.Controls.Add(new Label { Text = "Nothing needs attention in this queue.", Height = 42, Width = 420, ForeColor = QuoteMuted, Font = new Font("Segoe UI", 8.5f), TextAlign = ContentAlignment.MiddleCenter });
            _forecastFollowUps.ResumeLayout(true); ResizeFollowUps();
        }

        private Control FollowUpRow(QuotationActionItem item)
        {
            Color accent = item.Category == "Overdue" ? Color.FromArgb(220, 38, 38) : item.Category == "Expiring" ? WarnOrange : InfoBlue;
            var row = new Panel { Height = 58, BackColor = Color.FromArgb(249, 251, 253), Margin = new Padding(0, 0, 0, 5), Cursor = Cursors.Hand };
            row.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 3, BackColor = accent });
            Label customer = new Label { Text = item.ClientName, Location = new Point(12, 6), Height = 19, Font = new Font("Segoe UI", 8.4f, FontStyle.Bold), ForeColor = QuoteText, AutoEllipsis = true };
            Label detail = new Label { Text = item.QuotationNumber + "  •  " + item.Detail + "  •  " + IndiaFormatHelper.FormatCurrency(item.Value), Location = new Point(12, 29), Height = 18, Font = new Font("Segoe UI", 7.6f), ForeColor = QuoteMuted, AutoEllipsis = true };
            Button whatsapp = ForecastButton("WhatsApp", 88, QuoteSurface, SaveGreen); whatsapp.Height = 27; ModernIconSystem.AddButtonIcon(whatsapp, ModernIconKind.Phone);
            whatsapp.Click += async (s, e) => { await LoadQuotationFromDashboardAsync(item.BidId); ShowQuotationWhatsAppAction(); };
            Button open = ForecastButton("Open", 66, QuoteSurface, InfoBlue); open.Height = 27; open.Click += async (s, e) => await LoadQuotationFromDashboardAsync(item.BidId);
            row.Controls.Add(customer); row.Controls.Add(detail); row.Controls.Add(whatsapp); row.Controls.Add(open);
            row.Resize += (s, e) => { open.Location = new Point(row.ClientSize.Width - 72, 15); whatsapp.Location = new Point(open.Left - 94, 15); int w = Math.Max(120, whatsapp.Left - 20); customer.Width = w; detail.Width = w; };
            return row;
        }

        private void ResizeFollowUps()
        {
            if (_forecastFollowUps == null) return;
            int width = Math.Max(260, _forecastFollowUps.ClientSize.Width - (_forecastFollowUps.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2);
            foreach (Control row in _forecastFollowUps.Controls) row.Width = width;
        }

        private void BindForecastInsights(QuotationDashboardSnapshot snapshot)
        {
            _forecastInsights.Controls.Clear();
            int atRisk = snapshot.ActionItems.Where(a => a.Category == "Overdue" || a.Category == "Expiring").Select(a => a.BidId).Distinct().Count();
            decimal awaiting = snapshot.ActionItems.Where(a => a.Category == "Overdue").GroupBy(a => a.BidId).Sum(g => g.First().Value);
            _forecastInsights.Controls.Add(InsightTile(CompactCurrency(snapshot.Kpis.ExpectedRevenue.Value), "likely in 30 days", SaveGreen, Color.FromArgb(238, 250, 246)));
            _forecastInsights.Controls.Add(InsightTile(atRisk + " deals", "need attention", WarnOrange, Color.FromArgb(255, 248, 235)));
            _forecastInsights.Controls.Add(InsightTile(CompactCurrency(awaiting), "awaiting follow-up", InfoBlue, Color.FromArgb(238, 245, 255)));
            ResizeInsightTiles();
            decimal confidence = snapshot.Kpis.RevenuePipeline.Value <= 0m ? 0m : Math.Min(100m, snapshot.Kpis.WeightedPipeline.Value * 100m / snapshot.Kpis.RevenuePipeline.Value);
            _forecastConfidence.Value = (int)Math.Round(confidence);
        }

        private Control InsightTile(string value, string caption, Color accent, Color back)
        {
            var tile = new Panel { Height = 66, BackColor = back, Margin = new Padding(0, 0, 7, 0), Padding = new Padding(9, 8, 7, 5) };
            tile.Controls.Add(new Label { Text = value, Dock = DockStyle.Top, Height = 27, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true });
            tile.Controls.Add(new Label { Text = caption, Dock = DockStyle.Bottom, Height = 21, Font = new Font("Segoe UI", 7.2f), ForeColor = QuoteMuted, AutoEllipsis = true });
            return tile;
        }

        private void ResizeInsightTiles()
        {
            if (_forecastInsights == null || _forecastInsights.Controls.Count == 0) return;
            int width = Math.Max(82, (_forecastInsights.ClientSize.Width - 28) / 3);
            foreach (Control tile in _forecastInsights.Controls) tile.Width = width;
        }

        private void BindForecastRows()
        {
            if (_forecastGrid == null) return;
            EnsureForecastGridColumns(_forecastGrid);
            DateTime today = DateTime.Today; string search = (_forecastSearch == null ? "" : _forecastSearch.Text).Trim(); IEnumerable<QuotationRecentRow> rows = _forecastRows;
            if (_forecastTableFilter == "My Quotations") { string user = SessionManager.CurrentUser == null ? "" : SessionManager.CurrentUser.DisplayName; rows = rows.Where(r => !string.IsNullOrWhiteSpace(user) && string.Equals(r.OwnerName, user, StringComparison.OrdinalIgnoreCase)); }
            else if (_forecastTableFilter == "Overdue") rows = rows.Where(r => r.FollowUpDate.HasValue && r.FollowUpDate.Value.Date < today && !StatusClosed(r.Status));
            else if (_forecastTableFilter == "Expiring") rows = rows.Where(r => r.ValidTill.Date >= today && r.ValidTill.Date <= today.AddDays(7) && !StatusClosed(r.Status));
            else if (_forecastTableFilter == "High Value") rows = rows.Where(r => r.Value >= 50000m && !StatusClosed(r.Status));
            if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(r => Contains(r.QuotationNumber, search) || Contains(r.ClientName, search) || Contains(r.SiteName, search) || Contains(r.OwnerName, search));
            List<QuotationRecentRow> result = rows.ToList(); _forecastGrid.Rows.Clear();
            foreach (QuotationRecentRow row in result)
            {
                DateTime close = row.FollowUpDate ?? row.ValidTill; int age = Math.Max(0, (today - row.QuotationDate.Date).Days);
                _forecastGrid.Rows.Add(row.BidId, row.QuotationNumber, row.ClientName + " / " + row.SiteName, IndiaFormatHelper.FormatCurrency(row.Value), DisplayStage(row.Status), Probability(row.Status) + "%", IndiaFormatHelper.FormatDate(close), age + " days", row.OwnerName, NextAction(row, today), "•••");
            }
            _forecastRowCount.Text = result.Count + " shown";
        }

        private static void EnsureForecastGridColumns(DataGridView grid)
        {
            if (grid == null || grid.Columns.Count > 0)
                return;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BidId", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quotation", HeaderText = "Quotation", FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer", HeaderText = "Customer / Site", FillWeight = 185 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Value", FillWeight = 82 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stage", HeaderText = "Stage", FillWeight = 72 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Probability", HeaderText = "Probability", FillWeight = 68 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ExpectedClose", HeaderText = "Expected Close", FillWeight = 84 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Age", HeaderText = "Age", FillWeight = 56 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = "Owner", FillWeight = 78 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NextAction", HeaderText = "Next Action", FillWeight = 126 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Actions", HeaderText = "", Text = "•••", UseColumnTextForButtonValue = true, FillWeight = 38, FlatStyle = FlatStyle.Flat });
        }

        private void ForecastGridAction(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _forecastGrid.Columns[e.ColumnIndex].Name != "Actions") return;
            int id = GridBidId(e.RowIndex); if (id <= 0) return;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open PDF", null, (s, a) => RecentDocumentOpenService.OpenQuotationPdf(this, id));
            menu.Items.Add("Edit quotation", null, async (s, a) => await LoadQuotationFromDashboardAsync(id));
            menu.Items.Add("Create purchase order", null, async (s, a) => { await LoadQuotationFromDashboardAsync(id); await CreatePurchaseOrdersAsync(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete quotation", null, async (s, a) => await DeleteQuotationFromDashboardAsync(id));
            Rectangle cell = _forecastGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false); menu.Show(_forecastGrid, new Point(cell.Left, cell.Bottom));
        }

        private int GridBidId(int row)
        {
            if (_forecastGrid == null || row < 0 || row >= _forecastGrid.Rows.Count) return 0;
            int id; return int.TryParse(Convert.ToString(_forecastGrid.Rows[row].Cells["BidId"].Value), out id) ? id : 0;
        }

        private void ForecastGridFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _forecastGrid.Columns[e.ColumnIndex].Name != "Stage") return;
            e.CellStyle.ForeColor = StageColor(Convert.ToString(e.Value)); e.CellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        }

        private Control ForecastSection(string title, Control body, ModernIconKind icon, Color accent, int rightMargin)
        {
            var card = new QuotationForecastCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, rightMargin, 0), Padding = new Padding(12, 40, 12, 10) };
            AddSectionHeader(card, title, icon, accent); card.Controls.Add(body); return card;
        }

        private static void AddSectionHeader(Control card, string title, ModernIconKind icon, Color accent)
        {
            card.Controls.Add(new PictureBox { Location = new Point(12, 10), Size = new Size(20, 20), Image = ModernIconSystem.IconBitmap(icon, 17, accent), SizeMode = PictureBoxSizeMode.CenterImage, BackColor = Color.Transparent });
            card.Controls.Add(new Label { Text = title, Location = new Point(38, 8), Height = 24, Width = 240, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = QuoteText, TextAlign = ContentAlignment.MiddleLeft });
        }

        private static ComboBox ForecastCombo(int width) { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Height = 30, Font = new Font("Segoe UI", 8.5f), FlatStyle = FlatStyle.Flat, BackColor = Color.White }; }
        private static Button ForecastButton(string text, int width, Color back, Color fore) { Button b = new Button { Text = text, Width = width, Height = 30, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false }; b.FlatAppearance.BorderColor = Color.FromArgb(214, 222, 232); return b; }
        private static Button ForecastChip(string text, bool selected) { Button b = ForecastButton(text, Math.Max(66, TextRenderer.MeasureText(text, new Font("Segoe UI", 7.8f, FontStyle.Bold)).Width + 24), selected ? InfoBlue : Color.FromArgb(245, 248, 252), selected ? Color.White : QuoteMuted); b.Height = 27; b.Margin = new Padding(0, 0, 6, 0); b.FlatAppearance.BorderColor = selected ? InfoBlue : Color.FromArgb(226, 232, 240); return b; }
        private static void UpdateChips(IEnumerable<Button> chips, string selected) { foreach (Button b in chips) { bool active = string.Equals(Convert.ToString(b.Tag), selected, StringComparison.OrdinalIgnoreCase); b.BackColor = active ? InfoBlue : Color.FromArgb(245, 248, 252); b.ForeColor = active ? Color.White : QuoteMuted; b.FlatAppearance.BorderColor = active ? InfoBlue : Color.FromArgb(226, 232, 240); } }
        private static string CompactCurrency(decimal v) { decimal a = Math.Abs(v); if (a >= 10000000m) return "₹" + (v / 10000000m).ToString("0.##") + " Cr"; if (a >= 100000m) return "₹" + (v / 100000m).ToString("0.##") + " L"; return IndiaFormatHelper.FormatCurrency(v); }
        private static bool Contains(string value, string search) { return (value ?? "").IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) >= 0; }
        private static bool StatusClosed(string status) { string s = DisplayStage(status); return s == "Won" || s == "Lost"; }
        private static string DisplayStage(string status) { string s = (status ?? "Draft").Trim(); if (s.Equals("Converted", StringComparison.OrdinalIgnoreCase) || s.Equals("Won", StringComparison.OrdinalIgnoreCase)) return "Won"; if (s.Equals("Submitted", StringComparison.OrdinalIgnoreCase) || s.Equals("Analysed", StringComparison.OrdinalIgnoreCase) || s.Equals("Sent", StringComparison.OrdinalIgnoreCase)) return "Sent"; if (s.IndexOf("nego", StringComparison.OrdinalIgnoreCase) >= 0) return "Negotiation"; if (s.IndexOf("follow", StringComparison.OrdinalIgnoreCase) >= 0) return "Follow Up"; if (s.Equals("Lost", StringComparison.OrdinalIgnoreCase)) return "Lost"; return "Draft"; }
        private static int Probability(string status) { switch (DisplayStage(status)) { case "Won": return 100; case "Negotiation": return 65; case "Follow Up": return 45; case "Sent": return 25; case "Draft": return 10; default: return 0; } }
        private static Color StageColor(string stage) { switch (DisplayStage(stage)) { case "Won": return Color.FromArgb(22, 163, 74); case "Negotiation": return Color.FromArgb(217, 119, 6); case "Follow Up": return Color.FromArgb(124, 58, 237); case "Sent": return InfoBlue; case "Lost": return Color.FromArgb(220, 38, 38); default: return QuoteMuted; } }
        private static string NextAction(QuotationRecentRow row, DateTime today) { if (StatusClosed(row.Status)) return DisplayStage(row.Status) == "Won" ? "Convert to work order" : "Review loss reason"; if (row.FollowUpDate.HasValue && row.FollowUpDate.Value.Date < today) return "Follow up now"; if (row.ValidTill.Date <= today.AddDays(7)) return "Protect quote validity"; switch (DisplayStage(row.Status)) { case "Negotiation": return "Confirm commercial terms"; case "Sent": return "Request client response"; case "Follow Up": return "Schedule next contact"; default: return "Complete and send quote"; } }
    }

    internal sealed class QuotationForecastCard : Panel
    {
        public QuotationForecastCard() { BackColor = Color.White; DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (GraphicsPath p = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 9)) using (Pen pen = new Pen(Color.FromArgb(224, 230, 238))) e.Graphics.DrawPath(pen, p); }
        private static GraphicsPath Rounded(Rectangle r, int radius) { int d = radius * 2; GraphicsPath p = new GraphicsPath(); p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
    }

    internal sealed class QuotationPipelineForecastChart : Control
    {
        private List<QuotationTrendPoint> _data = new List<QuotationTrendPoint>();
        public QuotationPipelineForecastChart() { DoubleBuffered = true; }
        public void SetData(IEnumerable<QuotationTrendPoint> data) { List<QuotationTrendPoint> all = (data ?? Enumerable.Empty<QuotationTrendPoint>()).ToList(); _data = all.Skip(Math.Max(0, all.Count - 6)).ToList(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Color.White);
            if (_data.Count == 0) { QuotationDashboardPaint.DrawEmpty(e.Graphics, ClientRectangle, "No quotation forecast data"); return; }
            Rectangle plot = new Rectangle(48, 28, Math.Max(80, Width - 70), Math.Max(60, Height - 58)); decimal max = Math.Max(1m, _data.Max(p => Math.Max(p.Value, p.WeightedValue)));
            using (Pen grid = new Pen(Color.FromArgb(232, 237, 244))) using (Font font = new Font("Segoe UI", 7f)) using (Brush muted = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                for (int i = 0; i <= 3; i++) { int y = plot.Top + i * plot.Height / 3; e.Graphics.DrawLine(grid, plot.Left, y, plot.Right, y); e.Graphics.DrawString(Compact(max * (3 - i) / 3m), font, muted, 2, y - 7); }
                int slot = plot.Width / Math.Max(1, _data.Count); List<PointF> points = new List<PointF>();
                for (int i = 0; i < _data.Count; i++) { QuotationTrendPoint p = _data[i]; int bw = Math.Max(18, Math.Min(58, slot / 2)); int cx = plot.Left + i * slot + slot / 2; int h = (int)(p.Value / max * plot.Height); using (Brush bar = new SolidBrush(Color.FromArgb(190, 215, 250))) e.Graphics.FillRectangle(bar, cx - bw / 2, plot.Bottom - h, bw, h); points.Add(new PointF(cx, plot.Bottom - (float)(p.WeightedValue / max) * plot.Height)); SizeF sz = e.Graphics.MeasureString(p.Period, font); e.Graphics.DrawString(p.Period, font, muted, cx - sz.Width / 2, plot.Bottom + 6); }
                if (points.Count > 1) using (Pen line = new Pen(Color.FromArgb(37, 99, 235), 2.4f)) e.Graphics.DrawLines(line, points.ToArray());
                foreach (PointF p in points) { using (Brush dot = new SolidBrush(Color.FromArgb(37, 99, 235))) e.Graphics.FillEllipse(dot, p.X - 4, p.Y - 4, 8, 8); }
            }
            using (Font f = new Font("Segoe UI", 7.2f)) using (Brush t = new SolidBrush(Color.FromArgb(71, 85, 105))) using (Brush b = new SolidBrush(Color.FromArgb(190, 215, 250))) using (Pen l = new Pen(Color.FromArgb(37, 99, 235), 2f)) { int x = Math.Max(180, Width - 260); e.Graphics.FillRectangle(b, x, 5, 12, 8); e.Graphics.DrawString("Quoted value", f, t, x + 17, 1); e.Graphics.DrawLine(l, x + 100, 9, x + 118, 9); e.Graphics.DrawString("Weighted forecast", f, t, x + 123, 1); }
        }
        private static string Compact(decimal v) { if (v >= 10000000m) return "₹" + (v / 10000000m).ToString("0.#") + " Cr"; if (v >= 100000m) return "₹" + (v / 100000m).ToString("0.#") + " L"; return "₹" + v.ToString("0"); }
    }

    internal sealed class QuotationStageFunnelCard : Control
    {
        private List<QuotationFunnelStage> _data = new List<QuotationFunnelStage>();
        public QuotationStageFunnelCard() { DoubleBuffered = true; }
        public void SetData(IEnumerable<QuotationFunnelStage> data) { _data = (data ?? Enumerable.Empty<QuotationFunnelStage>()).Where(s => s.Stage == "Total Quotations" || s.Stage == "Sent" || s.Stage == "Negotiation" || s.Stage == "Converted").ToList(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Color.White); if (_data.Count == 0) { QuotationDashboardPaint.DrawEmpty(e.Graphics, ClientRectangle, "No stage data"); return; }
            int max = Math.Max(1, _data.Max(s => s.Count)); int cw = Math.Max(120, (int)(Width * .52)); int center = 12 + cw / 2; int y = 10; int h = Math.Max(25, (Height - 24) / _data.Count);
            using (Font bold = new Font("Segoe UI", 8f, FontStyle.Bold)) using (Font small = new Font("Segoe UI", 7.3f)) using (Brush text = new SolidBrush(Color.FromArgb(30, 41, 59))) foreach (QuotationFunnelStage s in _data) { int w = Math.Max(54, (int)(cw * (.38 + .62 * s.Count / max))); Point[] shape = { new Point(center - w / 2, y), new Point(center + w / 2, y), new Point(center + w / 2 - 10, y + h - 2), new Point(center - w / 2 + 10, y + h - 2) }; using (Brush fill = new SolidBrush(s.Color)) e.Graphics.FillPolygon(fill, shape); string count = s.Count.ToString(); SizeF size = e.Graphics.MeasureString(count, bold); e.Graphics.DrawString(count, bold, Brushes.White, center - size.Width / 2, y + (h - size.Height) / 2); string label = s.Stage == "Total Quotations" ? "All" : s.Stage == "Converted" ? "Won" : s.Stage; e.Graphics.DrawString(label, bold, text, cw + 30, y + 3); e.Graphics.DrawString(s.Percentage.ToString("0.#") + "%", small, Brushes.SlateGray, cw + 30, y + 20); y += h; }
        }
    }

    internal sealed class QuotationConfidenceBar : Control
    {
        private int _value; public int Value { get { return _value; } set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public QuotationConfidenceBar() { DoubleBuffered = true; MinimumSize = new Size(100, 40); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.Clear(Color.White); using (Font f = new Font("Segoe UI", 7.7f, FontStyle.Bold)) using (Brush text = new SolidBrush(Color.FromArgb(30, 41, 59))) e.Graphics.DrawString("Forecast confidence", f, text, 0, 0); Rectangle track = new Rectangle(0, 20, Math.Max(20, Width - 42), 10); using (Brush b = new SolidBrush(Color.FromArgb(226, 232, 240))) e.Graphics.FillRectangle(b, track); using (Brush b = new SolidBrush(Color.FromArgb(16, 185, 129))) e.Graphics.FillRectangle(b, new Rectangle(track.X, track.Y, (int)(track.Width * Value / 100f), track.Height)); using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold)) using (Brush b = new SolidBrush(Color.FromArgb(30, 41, 59))) e.Graphics.DrawString(Value + "%", f, b, Width - 39, 16); }
    }
}
