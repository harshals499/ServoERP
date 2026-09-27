using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class InvoiceReceivablesDashboard : Panel
    {
        private static readonly Color Page = Color.FromArgb(246, 249, 253);
        private static readonly Color Surface = Color.White;
        private static readonly Color TextColor = Color.FromArgb(22, 41, 72);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);
        private static readonly Color Blue = Color.FromArgb(25, 111, 211);
        private static readonly Color Green = Color.FromArgb(16, 163, 92);
        private static readonly Color Amber = Color.FromArgb(245, 158, 11);
        private static readonly Color Red = Color.FromArgb(220, 38, 38);
        private static readonly Color Border = Color.FromArgb(218, 226, 237);

        private readonly Dictionary<string, Label> _kpiValues = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Label> _kpiNotes = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
        private readonly List<InvoiceRecentRow> _rows = new List<InvoiceRecentRow>();
        private readonly TableLayoutPanel _root;
        private readonly InvoiceCollectionForecastChart _forecastChart;
        private readonly InvoiceAgingLadderChart _agingChart;
        private readonly DataGridView _queueGrid;
        private readonly ComboBox _queueFilter;
        private readonly TextBox _search;
        private readonly Label _rowCount;
        private readonly FlowLayoutPanel _followUps;
        private readonly FlowLayoutPanel _clientExposure;
        private InvoiceDashboardSnapshot _snapshot = new InvoiceDashboardSnapshot();
        private bool _compactQueue;

        public event Action RefreshRequested;
        public event Action<int> OpenInvoiceRequested;
        public event Action<int> RecordPaymentRequested;
        public event Action<int> SendReminderRequested;

        public InvoiceReceivablesDashboard()
        {
            Name = "InvoiceReceivablesDashboard";
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Page;
            Padding = new Padding(0, 2, 0, 10);

            _root = new TableLayoutPanel
            {
                Name = "InvoiceReceivablesDashboardRoot",
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Page,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Location = Point.Empty
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 282));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 526));

            _root.Controls.Add(BuildKpiRow(), 0, 0);

            var analytics = SplitRow(68f, 32f, 10);
            _forecastChart = new InvoiceCollectionForecastChart { Dock = DockStyle.Fill };
            _agingChart = new InvoiceAgingLadderChart { Dock = DockStyle.Fill };
            analytics.Controls.Add(Section("8-week collection forecast", "Expected receipts by payment risk", _forecastChart, 10), 0, 0);
            analytics.Controls.Add(Section("Ageing ladder", "Outstanding receivables by age", _agingChart, 0), 1, 0);
            _root.Controls.Add(analytics, 0, 1);

            var operations = SplitRow(78f, 22f, 0);
            Panel queueCard = Card(new Padding(0, 50, 0, 0), new Padding(0, 0, 10, 0));
            AddSectionTitle(queueCard, "Invoice work queue", "Prioritized by age, balance and payment risk");
            _queueFilter = Combo(108);
            _queueFilter.Items.AddRange(new object[] { "All", "Overdue", "Due soon", "Not due", "Paid" });
            _queueFilter.SelectedIndex = 0;
            _queueFilter.SelectedIndexChanged += (s, e) => BindQueue();
            _search = new TextBox { Width = 184, Height = 28, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, Text = "Search invoices..." };
            _search.TextChanged += (s, e) => BindQueue();
            _search.Enter += (s, e) =>
            {
                if (_search.Text == "Search invoices...")
                {
                    _search.Text = string.Empty;
                    _search.ForeColor = TextColor;
                }
            };
            _search.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_search.Text))
                {
                    _search.Text = "Search invoices...";
                    _search.ForeColor = Muted;
                }
            };
            _rowCount = new Label { Width = 78, Height = 28, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Muted, TextAlign = ContentAlignment.MiddleRight };
            Button refresh = Button("Refresh", 82, Surface, Blue);
            refresh.Click += (s, e) => RefreshRequested?.Invoke();
            queueCard.Controls.Add(_queueFilter);
            queueCard.Controls.Add(_search);
            queueCard.Controls.Add(_rowCount);
            queueCard.Controls.Add(refresh);
            queueCard.Resize += (s, e) =>
            {
                refresh.Location = new Point(Math.Max(420, queueCard.ClientSize.Width - 88), 12);
                _search.Location = new Point(refresh.Left - _search.Width - 8, 12);
                _queueFilter.Location = new Point(_search.Left - _queueFilter.Width - 8, 12);
                _rowCount.Location = new Point(_queueFilter.Left - _rowCount.Width - 6, 12);
            };
            _queueGrid = BuildQueueGrid();
            queueCard.Controls.Add(_queueGrid);
            operations.Controls.Add(queueCard, 0, 0);

            var rail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Page };
            rail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rail.RowStyles.Add(new RowStyle(SizeType.Percent, 57f));
            rail.RowStyles.Add(new RowStyle(SizeType.Percent, 43f));
            Panel followCard = Card(new Padding(12, 48, 12, 8), new Padding(0, 0, 0, 8));
            AddSectionTitle(followCard, "Today's follow-ups", "Recommended collection actions");
            _followUps = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false, BackColor = Surface, Margin = Padding.Empty, Padding = Padding.Empty };
            _followUps.Resize += (s, e) => ResizeRailRows(_followUps);
            followCard.Controls.Add(_followUps);
            Panel clientCard = Card(new Padding(12, 48, 12, 10), Padding.Empty);
            AddSectionTitle(clientCard, "Client concentration", "Share of total outstanding");
            _clientExposure = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false, BackColor = Surface, Margin = Padding.Empty, Padding = Padding.Empty };
            _clientExposure.Resize += (s, e) => ResizeRailRows(_clientExposure);
            clientCard.Controls.Add(_clientExposure);
            rail.Controls.Add(followCard, 0, 0);
            rail.Controls.Add(clientCard, 0, 1);
            operations.Controls.Add(rail, 1, 0);
            _root.Controls.Add(operations, 0, 2);
            Controls.Add(_root);

            Resize += (s, e) => LayoutDashboard();
            HandleCreated += (s, e) => LayoutDashboard();
        }

        public InvoiceAnalyticsFilter CreateFilter()
        {
            DateTime today = DateTime.Today;
            return new InvoiceAnalyticsFilter
            {
                DateFrom = today.AddMonths(-12).Date,
                DateTo = today.Date.AddDays(56),
                Grouping = InvoiceAnalyticsGrouping.Week
            };
        }

        public void BindSnapshot(InvoiceDashboardSnapshot snapshot)
        {
            _snapshot = snapshot ?? new InvoiceDashboardSnapshot();
            _rows.Clear();
            _rows.AddRange(_snapshot.RecentInvoices ?? new List<InvoiceRecentRow>());
            SetKpi("expected", CompactCurrency(_snapshot.CashExpectedAmount), "Due within the next 30 days");
            SetKpi("collected", CompactCurrency(_snapshot.Kpis.PaidAmount.Value), "Paid in the selected period");
            SetKpi("risk", CompactCurrency(_snapshot.AtRiskAmount), "High-risk open balances");
            SetKpi("overdue", CompactCurrency(_snapshot.OverdueAmount), "Past due and still outstanding");
            SetKpi("dso", _snapshot.DaysSalesOutstanding.ToString("0") + " days", "Average collection period");
            _forecastChart.SetData(_snapshot.CollectionForecast);
            _agingChart.SetData(_snapshot.AgingBuckets);
            BindQueue();
            BindFollowUps();
            BindClientExposure();
        }

        private Control BuildKpiRow()
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 10), BackColor = Page };
            for (int i = 0; i < 5; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            row.Controls.Add(Kpi("expected", "Cash expected", Blue, Color.FromArgb(234, 243, 255), "CAL"), 0, 0);
            row.Controls.Add(Kpi("collected", "Collected", Green, Color.FromArgb(232, 249, 241), "₹"), 1, 0);
            row.Controls.Add(Kpi("risk", "At risk", Amber, Color.FromArgb(255, 247, 229), "!"), 2, 0);
            row.Controls.Add(Kpi("overdue", "Overdue", Red, Color.FromArgb(254, 236, 236), "!"), 3, 0);
            row.Controls.Add(Kpi("dso", "DSO", Blue, Color.FromArgb(234, 243, 255), "DAY"), 4, 0);
            return row;
        }

        private Control Kpi(string key, string title, Color accent, Color badgeBack, string badgeText)
        {
            Panel card = Card(new Padding(14, 10, 58, 8), new Padding(0, 0, 10, 0));
            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 19, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), ForeColor = TextColor, AutoEllipsis = true };
            Label value = new Label { Text = "—", Dock = DockStyle.Top, Height = 31, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
            Label note = new Label { Text = "Waiting for live data", Dock = DockStyle.Bottom, Height = 18, Font = new Font("Segoe UI", 7.5f), ForeColor = Muted, AutoEllipsis = true };
            Label badge = new Label { Text = badgeText, Size = new Size(38, 38), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = badgeBack, ForeColor = accent, Font = new Font("Segoe UI", 7.2f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            card.Controls.Add(note);
            card.Controls.Add(value);
            card.Controls.Add(titleLabel);
            card.Controls.Add(badge);
            card.Resize += (s, e) => badge.Location = new Point(card.ClientSize.Width - 50, 17);
            _kpiValues[key] = value;
            _kpiNotes[key] = note;
            return card;
        }

        private void SetKpi(string key, string value, string note)
        {
            if (_kpiValues.TryGetValue(key, out Label valueLabel)) valueLabel.Text = value;
            if (_kpiNotes.TryGetValue(key, out Label noteLabel)) noteLabel.Text = note;
        }

        private Control Section(string title, string subtitle, Control body, int rightMargin)
        {
            Panel card = Card(new Padding(12, 48, 12, 10), new Padding(0, 0, rightMargin, 0));
            AddSectionTitle(card, title, subtitle);
            card.Controls.Add(body);
            return card;
        }

        private static void AddSectionTitle(Control card, string title, string subtitle)
        {
            card.Controls.Add(new Label { Text = title, Location = new Point(14, 8), Size = new Size(300, 20), Font = new Font("Segoe UI", 9.6f, FontStyle.Bold), ForeColor = TextColor, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = subtitle, Location = new Point(14, 28), Size = new Size(340, 17), Font = new Font("Segoe UI", 7.5f), ForeColor = Muted, AutoEllipsis = true });
        }

        private DataGridView BuildQueueGrid()
        {
            var grid = new DataGridView
            {
                Name = "InvoiceWorkQueueGrid",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Surface,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 34
            };
            GridTheme.Apply(grid);
            grid.RowTemplate.Height = 32;
            grid.GridColor = Color.FromArgb(232, 237, 244);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 8f);
            grid.DefaultCellStyle.ForeColor = TextColor;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 243, 255);
            grid.DefaultCellStyle.SelectionForeColor = TextColor;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "InvoiceId", Visible = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Client", HeaderText = "Client", FillWeight = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Invoice", HeaderText = "Invoice No.", FillWeight = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DueDate", HeaderText = "Due Date", FillWeight = 72 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Outstanding", HeaderText = "Outstanding", FillWeight = 82 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Age", HeaderText = "Age", FillWeight = 48 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Risk", HeaderText = "Risk", FillWeight = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NextAction", HeaderText = "Next action", FillWeight = 112 });
            grid.Columns.Add(ActionColumn("Open", "Open", 50));
            grid.Columns.Add(ActionColumn("Payment", "Record payment", 82));
            grid.Columns.Add(ActionColumn("Reminder", "Send reminder", 80));
            grid.CellContentClick += QueueGridCellContentClick;
            grid.CellDoubleClick += (s, e) => { int id = RowInvoiceId(e.RowIndex); if (id > 0) OpenInvoiceRequested?.Invoke(id); };
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Risk") return;
                string risk = Convert.ToString(e.Value);
                e.CellStyle.ForeColor = risk == "High" ? Red : risk == "Medium" ? Color.FromArgb(217, 119, 6) : Green;
                e.CellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            };
            return grid;
        }

        private static DataGridViewButtonColumn ActionColumn(string name, string text, float weight)
        {
            return new DataGridViewButtonColumn { Name = name, HeaderText = string.Empty, Text = text, UseColumnTextForButtonValue = true, FillWeight = weight, FlatStyle = FlatStyle.Flat };
        }

        private void QueueGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            int id = RowInvoiceId(e.RowIndex);
            if (id <= 0) return;
            string column = _queueGrid.Columns[e.ColumnIndex].Name;
            if (column == "Open" && _compactQueue) ShowQueueActions(e.RowIndex, e.ColumnIndex, id);
            else if (column == "Open") OpenInvoiceRequested?.Invoke(id);
            else if (column == "Payment") RecordPaymentRequested?.Invoke(id);
            else if (column == "Reminder") SendReminderRequested?.Invoke(id);
        }

        private void ShowQueueActions(int rowIndex, int columnIndex, int invoiceId)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Open invoice", null, (s, e) => OpenInvoiceRequested?.Invoke(invoiceId));
            menu.Items.Add("Record payment", null, (s, e) => RecordPaymentRequested?.Invoke(invoiceId));
            menu.Items.Add("Send reminder", null, (s, e) => SendReminderRequested?.Invoke(invoiceId));
            Rectangle cell = _queueGrid.GetCellDisplayRectangle(columnIndex, rowIndex, false);
            menu.Show(_queueGrid, new Point(cell.Left, cell.Bottom));
        }

        private int RowInvoiceId(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _queueGrid.Rows.Count) return 0;
            return int.TryParse(Convert.ToString(_queueGrid.Rows[rowIndex].Cells["InvoiceId"].Value), out int id) ? id : 0;
        }

        private void BindQueue()
        {
            if (_queueGrid == null) return;
            IEnumerable<InvoiceRecentRow> rows = _rows;
            string filter = Convert.ToString(_queueFilter?.SelectedItem) ?? "All";
            if (filter == "Overdue") rows = rows.Where(r => r.BalanceDue > 0m && r.DueDate.Date < DateTime.Today);
            else if (filter == "Due soon") rows = rows.Where(r => r.BalanceDue > 0m && r.DueDate.Date >= DateTime.Today && r.DueDate.Date <= DateTime.Today.AddDays(7));
            else if (filter == "Not due") rows = rows.Where(r => r.BalanceDue > 0m && r.DueDate.Date > DateTime.Today.AddDays(7));
            else if (filter == "Paid") rows = rows.Where(r => r.BalanceDue <= 0m || string.Equals(r.Status, "Paid", StringComparison.OrdinalIgnoreCase));
            string search = (_search?.Text ?? string.Empty).Trim();
            if (search == "Search invoices...") search = string.Empty;
            if (search.Length > 0) rows = rows.Where(r => Contains(r.ClientName, search) || Contains(r.InvoiceNumber, search) || Contains(r.SiteName, search));
            List<InvoiceRecentRow> result = rows.Take(50).ToList();
            _queueGrid.Rows.Clear();
            foreach (InvoiceRecentRow row in result)
            {
                string age = row.DaysOverdue > 0 ? row.DaysOverdue + " d late" : row.BalanceDue <= 0m ? "Paid" : Math.Max(0, (row.DueDate.Date - DateTime.Today).Days) + " d";
                _queueGrid.Rows.Add(row.InvoiceId, row.ClientName, row.InvoiceNumber, IndiaFormatHelper.FormatDate(row.DueDate), IndiaFormatHelper.FormatCurrency(row.BalanceDue), age, row.Risk, row.NextAction, "Open", "Record payment", "Send reminder");
            }
            if (_rowCount != null) _rowCount.Text = result.Count + " shown";
        }

        private void BindFollowUps()
        {
            _followUps.SuspendLayout();
            _followUps.Controls.Clear();
            List<InvoiceRecentRow> items = _rows.Where(r => r.BalanceDue > 0m).OrderByDescending(r => r.Risk == "High").ThenByDescending(r => r.DaysOverdue).ThenByDescending(r => r.BalanceDue).Take(5).ToList();
            for (int i = 0; i < items.Count; i++)
            {
                InvoiceRecentRow row = items[i];
                Color accent = row.Risk == "High" ? Red : row.Risk == "Medium" ? Amber : Green;
                Panel item = new Panel { Height = 54, BackColor = Color.FromArgb(249, 251, 253), Margin = new Padding(0, 0, 0, 5), Cursor = Cursors.Hand, Tag = row.InvoiceId };
                item.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 3, BackColor = accent });
                item.Controls.Add(new Label { Text = DateTime.Today.AddHours(10).AddMinutes(i * 75).ToString("HH:mm"), Location = new Point(11, 8), Size = new Size(44, 17), Font = new Font("Segoe UI", 7.2f, FontStyle.Bold), ForeColor = Muted });
                item.Controls.Add(new Label { Text = row.ClientName, Location = new Point(58, 6), Size = new Size(190, 19), Font = new Font("Segoe UI", 8.1f, FontStyle.Bold), ForeColor = TextColor, AutoEllipsis = true });
                item.Controls.Add(new Label { Text = row.NextAction + " • " + row.InvoiceNumber, Location = new Point(58, 28), Size = new Size(220, 18), Font = new Font("Segoe UI", 7.2f), ForeColor = Muted, AutoEllipsis = true });
                item.Click += (s, e) => OpenInvoiceRequested?.Invoke((int)((Control)s).Tag);
                _followUps.Controls.Add(item);
            }
            if (_followUps.Controls.Count == 0) _followUps.Controls.Add(EmptyLabel("No collection follow-ups are due."));
            _followUps.ResumeLayout(true);
            ResizeRailRows(_followUps);
        }

        private void BindClientExposure()
        {
            _clientExposure.SuspendLayout();
            _clientExposure.Controls.Clear();
            foreach (InvoiceClientExposureRow row in _snapshot.ClientExposure ?? new List<InvoiceClientExposureRow>())
            {
                Panel item = new Panel { Height = 38, BackColor = Surface, Margin = Padding.Empty, Tag = row };
                Label name = new Label { Text = row.ClientName, Location = new Point(0, 2), Size = new Size(150, 17), Font = new Font("Segoe UI", 7.4f), ForeColor = TextColor, AutoEllipsis = true };
                Panel track = new Panel { Location = new Point(0, 23), Size = new Size(170, 7), BackColor = Color.FromArgb(226, 232, 240) };
                track.Controls.Add(new Panel { Dock = DockStyle.Left, Width = Math.Max(2, (int)(170 * Math.Min(100m, row.SharePercent) / 100m)), BackColor = Blue });
                Label share = new Label { Text = row.SharePercent.ToString("0.#") + "%", Location = new Point(178, 1), Size = new Size(42, 18), Font = new Font("Segoe UI", 7.4f, FontStyle.Bold), ForeColor = Muted, TextAlign = ContentAlignment.MiddleRight };
                item.Controls.Add(name); item.Controls.Add(track); item.Controls.Add(share);
                _clientExposure.Controls.Add(item);
            }
            if (_clientExposure.Controls.Count == 0) _clientExposure.Controls.Add(EmptyLabel("No outstanding client balances."));
            _clientExposure.ResumeLayout(true);
            ResizeRailRows(_clientExposure);
        }

        private void ResizeRailRows(FlowLayoutPanel host)
        {
            if (host == null) return;
            int width = Math.Max(180, host.ClientSize.Width - 4);
            foreach (Control row in host.Controls) row.Width = width;
        }

        private void LayoutDashboard()
        {
            int width = Math.Max(1180, ClientSize.Width - Padding.Horizontal);
            int height = Math.Max(920, ClientSize.Height - Padding.Vertical);
            _root.Size = new Size(width, height);
            AutoScrollMinSize = new Size(width + 4, height + 4);
            ConfigureQueueForWidth(ClientSize.Width);
        }

        private void ConfigureQueueForWidth(int width)
        {
            if (_queueGrid == null || _queueGrid.Columns.Count == 0)
                return;
            bool compact = width < 1360;
            if (_compactQueue == compact)
                return;
            _compactQueue = compact;
            _queueGrid.Columns["Payment"].Visible = !compact;
            _queueGrid.Columns["Reminder"].Visible = !compact;
            var open = _queueGrid.Columns["Open"] as DataGridViewButtonColumn;
            if (open != null)
            {
                open.Text = compact ? "Actions" : "Open";
                open.FillWeight = compact ? 64 : 50;
            }
            _queueGrid.Invalidate();
        }

        private static TableLayoutPanel SplitRow(float left, float right, int bottomPadding)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, bottomPadding), BackColor = Page };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, left));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, right));
            return row;
        }

        private static Panel Card(Padding padding, Padding margin)
        {
            return new ReceivablesCard { Dock = DockStyle.Fill, BackColor = Surface, Padding = padding, Margin = margin };
        }

        private static ComboBox Combo(int width)
        {
            return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Height = 28, Font = new Font("Segoe UI", 8.2f), FlatStyle = FlatStyle.Flat, BackColor = Surface };
        }

        private static Button Button(string text, int width, Color back, Color fore)
        {
            var button = new Button { Text = text, Width = width, Height = 28, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = Border;
            return button;
        }

        private static Label EmptyLabel(string text)
        {
            return new Label { Text = text, Height = 48, ForeColor = Muted, Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleCenter };
        }

        private static string CompactCurrency(decimal value)
        {
            decimal absolute = Math.Abs(value);
            if (absolute >= 10000000m) return "₹" + (value / 10000000m).ToString("0.##") + " Cr";
            if (absolute >= 100000m) return "₹" + (value / 100000m).ToString("0.##") + " L";
            return IndiaFormatHelper.FormatCurrency(value);
        }

        private static bool Contains(string value, string search)
        {
            return (value ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal sealed class ReceivablesCard : Panel
    {
        public ReceivablesCard() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 9))
            using (Pen pen = new Pen(Color.FromArgb(218, 226, 237)))
                e.Graphics.DrawPath(pen, path);
        }
        private static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class InvoiceCollectionForecastChart : Control
    {
        private List<InvoiceCollectionForecastPoint> _data = new List<InvoiceCollectionForecastPoint>();
        public InvoiceCollectionForecastChart() { DoubleBuffered = true; BackColor = Color.White; }
        public void SetData(IEnumerable<InvoiceCollectionForecastPoint> data) { _data = (data ?? Enumerable.Empty<InvoiceCollectionForecastPoint>()).ToList(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Color.White);
            Rectangle plot = new Rectangle(56, 28, Math.Max(100, Width - 76), Math.Max(70, Height - 62));
            decimal max = Math.Max(1m, _data.Select(p => p.TotalAmount).DefaultIfEmpty(1m).Max());
            using (Pen grid = new Pen(Color.FromArgb(232, 237, 244)))
            using (Font font = new Font("Segoe UI", 7f))
            using (Brush muted = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Top + i * plot.Height / 4;
                    e.Graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                    e.Graphics.DrawString(Compact(max * (4 - i) / 4m), font, muted, 0, y - 7);
                }
                if (_data.Count == 0)
                {
                    e.Graphics.DrawString("No open receivables in this forecast window", new Font("Segoe UI", 9f), muted, plot.Left + 20, plot.Top + plot.Height / 2);
                    return;
                }
                int slot = plot.Width / _data.Count;
                for (int i = 0; i < _data.Count; i++)
                {
                    InvoiceCollectionForecastPoint point = _data[i];
                    int barWidth = Math.Max(20, Math.Min(58, slot / 2));
                    int x = plot.Left + i * slot + (slot - barWidth) / 2;
                    int y = plot.Bottom;
                    y = DrawSegment(e.Graphics, x, y, barWidth, point.OnTimeAmount, max, plot.Height, Color.FromArgb(34, 181, 115));
                    y = DrawSegment(e.Graphics, x, y, barWidth, point.DueSoonAmount, max, plot.Height, Color.FromArgb(245, 166, 35));
                    DrawSegment(e.Graphics, x, y, barWidth, point.OverdueAmount, max, plot.Height, Color.FromArgb(232, 66, 66));
                    string label = point.WeekStart.ToString("dd MMM");
                    SizeF size = e.Graphics.MeasureString(label, font);
                    e.Graphics.DrawString(label, font, muted, x + barWidth / 2f - size.Width / 2f, plot.Bottom + 7);
                }
            }
            DrawLegend(e.Graphics);
        }
        private static int DrawSegment(Graphics graphics, int x, int bottom, int width, decimal amount, decimal max, int plotHeight, Color color)
        {
            int height = amount <= 0m ? 0 : Math.Max(2, (int)(amount / max * plotHeight));
            if (height > 0) using (Brush brush = new SolidBrush(color)) graphics.FillRectangle(brush, x, bottom - height, width, height);
            return bottom - height;
        }
        private static void DrawLegend(Graphics graphics)
        {
            string[] labels = { "On time", "Due soon", "Overdue" };
            Color[] colors = { Color.FromArgb(34, 181, 115), Color.FromArgb(245, 166, 35), Color.FromArgb(232, 66, 66) };
            int x = 62;
            using (Font font = new Font("Segoe UI", 7f)) using (Brush text = new SolidBrush(Color.FromArgb(100, 116, 139)))
            for (int i = 0; i < labels.Length; i++)
            {
                using (Brush fill = new SolidBrush(colors[i])) graphics.FillRectangle(fill, x, 5, 10, 8);
                graphics.DrawString(labels[i], font, text, x + 14, 1);
                x += 78;
            }
        }
        private static string Compact(decimal value) { if (value >= 10000000m) return "₹" + (value / 10000000m).ToString("0.#") + "Cr"; if (value >= 100000m) return "₹" + (value / 100000m).ToString("0.#") + "L"; return "₹" + value.ToString("0"); }
    }

    internal sealed class InvoiceAgingLadderChart : Control
    {
        private List<InvoiceAgingBucket> _data = new List<InvoiceAgingBucket>();
        public InvoiceAgingLadderChart() { DoubleBuffered = true; BackColor = Color.White; }
        public void SetData(IEnumerable<InvoiceAgingBucket> data) { _data = (data ?? Enumerable.Empty<InvoiceAgingBucket>()).ToList(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Color.White);
            decimal total = _data.Sum(r => r.Amount);
            decimal max = Math.Max(1m, _data.Select(r => r.Amount).DefaultIfEmpty(1m).Max());
            Color[] colors = { Color.FromArgb(34, 181, 115), Color.FromArgb(245, 166, 35), Color.FromArgb(249, 115, 22), Color.FromArgb(239, 68, 68), Color.FromArgb(185, 28, 28) };
            using (Font label = new Font("Segoe UI", 7.4f)) using (Font value = new Font("Segoe UI", 7.4f, FontStyle.Bold)) using (Brush text = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                int y = 14;
                for (int i = 0; i < _data.Count; i++)
                {
                    InvoiceAgingBucket row = _data[i];
                    e.Graphics.DrawString(row.Bucket, label, text, 0, y + 3);
                    int barX = 76;
                    int barMax = Math.Max(40, Width - 190);
                    int barWidth = row.Amount <= 0m ? 2 : Math.Max(4, (int)(barMax * row.Amount / max));
                    using (Brush fill = new SolidBrush(colors[Math.Min(i, colors.Length - 1)])) e.Graphics.FillRectangle(fill, barX, y + 2, barWidth, 16);
                    string amount = Compact(row.Amount);
                    e.Graphics.DrawString(amount, value, text, Width - 106, y + 3);
                    string share = total <= 0m ? "0%" : Math.Round(row.Amount * 100m / total, 0) + "%";
                    e.Graphics.DrawString(share, label, text, Width - 42, y + 3);
                    y += 35;
                }
            }
        }
        private static string Compact(decimal value) { if (value >= 10000000m) return "₹" + (value / 10000000m).ToString("0.#") + "Cr"; if (value >= 100000m) return "₹" + (value / 100000m).ToString("0.#") + "L"; return "₹" + value.ToString("0"); }
    }
}
