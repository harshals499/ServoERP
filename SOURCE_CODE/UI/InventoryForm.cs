using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class InventoryForm : DeferredPageControl
    {
        private readonly InventoryService _svc     = new InventoryService();
        private readonly VendorService    _vndSvc  = new VendorService();
        private readonly PurchaseService  _poSvc   = new PurchaseService();
        private readonly ToolTip _toolTip = new ToolTip();

        private FlowLayoutPanel _itemFlow;
        private Panel    _detail;
        private Panel    _selectedCard;

        private ComboBox      _cboName, _cboCategory, _cboUnit;
        private NumericUpDown _numStock, _numRate, _numReorder;
        private ComboBox      _cboVendor;
        private Label         _lblStatus, _lblStockValue;
        private Label         _lblTotalItems, _lblInStockItems, _lblLowStockItems, _lblOutStockItems, _lblTotalStockValue;
        private TextBox       _txtSearch;
        private ComboBox      _cboListMode;
        private Button        _btnReorder;
        private List<StockItem> _listSource = new List<StockItem>();
        private List<StockItem> _allItems = new List<StockItem>();
        private int _renderedCount;
        private const int ItemBatchSize = 60;

        private StockItem _current;
        private AutoCompleteStringCollection _itemSuggestions = new AutoCompleteStringCollection();

        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color DelRed = DS.Red600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color WarnOrange = DS.Amber500;

        public InventoryForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad(
                LoadInitialDataAsync,
                ex => { _lblStatus.Text = "Load error: " + ex.Message; _lblStatus.ForeColor = Color.Red; });
        }

        private void BuildLayout()
        {
            Controls.Clear();
            BackColor = DS.BgPage;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = DS.BgPage, Padding = new Padding(32, 22, 24, 10) };
            Label title = new Label { Text = "Inventory / Stock", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(32, 22), Size = new Size(420, 32) };
            Label sub = new Label { Text = "Monitor and manage your inventory items, stock levels, and pricing.", Font = new Font("Segoe UI", 9), ForeColor = DS.Slate600, Location = new Point(32, 58), Size = new Size(560, 22) };
            Button btnExport = MakeBtn("Export", Color.White, 92); btnExport.ForeColor = DS.Slate700; btnExport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnImport = MakeBtn("Import", Color.White, 92); btnImport.ForeColor = DS.Slate700; btnImport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnForms = MakeBtn("Forms", Color.White, 86); btnForms.ForeColor = InfoBlue; btnForms.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            Button btnNew = MakeBtn("+ Add Item", InfoBlue, 118);
            FlowLayoutPanel headerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 440,
                Height = 44,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = DS.BgPage,
                Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(0)
            };
            btnExport.Margin = new Padding(0, 0, 12, 0);
            btnImport.Margin = new Padding(0, 0, 12, 0);
            btnForms.Margin = new Padding(0, 0, 12, 0);
            btnNew.Margin = new Padding(0);
            headerActions.Controls.AddRange(new Control[] { btnExport, btnImport, btnForms, btnNew });
            header.Resize += (s, e) =>
            {
                int reserved = headerActions.Width + 48;
                title.Width = Math.Max(220, header.ClientSize.Width - reserved - title.Left);
                sub.Width = Math.Max(220, header.ClientSize.Width - reserved - sub.Left);
            };
            btnNew.Click += (s, e) => NewRecord();
            btnImport.Click += async (s, e) => await ImportInventoryCsvAsync();
            btnExport.Click += (s, e) => ExportInventoryCsv();
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Inventory", "Inventory", null, "spare parts requisition stock issue return goods received note inventory equipment asset parts usage");
            header.Controls.AddRange(new Control[] { title, sub, headerActions });

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, BackColor = DS.BgPage, Padding = new Padding(24, 8, 24, 14), ColumnCount = 5, RowCount = 1 };
            for (int i = 0; i < 5; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            kpis.Controls.Add(CreateInventoryMetric("Total Items", "0", ModernIconKind.Inventory, InfoBlue, out _lblTotalItems), 0, 0);
            kpis.Controls.Add(CreateInventoryMetric("In Stock Items", "0", ModernIconKind.Checklist, SaveGreen, out _lblInStockItems), 1, 0);
            kpis.Controls.Add(CreateInventoryMetric("Low Stock Items", "0", ModernIconKind.Alert, WarnOrange, out _lblLowStockItems), 2, 0);
            kpis.Controls.Add(CreateInventoryMetric("Out of Stock Items", "0", ModernIconKind.Alert, DelRed, out _lblOutStockItems), 3, 0);
            kpis.Controls.Add(CreateInventoryMetric("Total Stock Value", "₹ 0", ModernIconKind.Payment, InfoBlue, out _lblTotalStockValue), 4, 0);

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(24, 0, 24, 16) };
            Panel right = CreateModernCard("ITEM DETAILS");
            right.Dock = DockStyle.Right;
            right.Width = 390;
            right.MinimumSize = new Size(390, 0);
            right.Padding = new Padding(18, 44, 18, 14);

            _detail = new Panel { Dock = DockStyle.Top, Height = 360, AutoScroll = true, BackColor = Color.White };
            _detail.HorizontalScroll.Enabled = false;
            _detail.HorizontalScroll.Visible = false;
            BuildDetailPanel();
            Button saveItem = MakeBtn("Save Item", SaveGreen, 104);
            Button clearItem = MakeBtn("Clear", Color.White, 82);
            Button createPo = MakeBtn("Create PO", InfoBlue, 104);
            clearItem.ForeColor = DS.Slate700;
            clearItem.FlatAppearance.BorderColor = DS.BorderStrong;
            saveItem.Click += (s, e) => Save();
            clearItem.Click += (s, e) => NewRecord();
            createPo.Click += (s, e) => CreatePO();
            Panel quick = BuildInventoryQuickActions();
            right.Controls.Add(quick);
            right.Controls.Add(_detail);
            right.Controls.Add(BuildDetailActionBar(saveItem, clearItem, createPo));

            Panel mainCard = CreateModernCard(null);
            mainCard.Dock = DockStyle.Fill;
            mainCard.Padding = new Padding(16);

            Panel filters = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Color.White, Padding = new Padding(0, 4, 0, 0) };
            TableLayoutPanel filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            FlowLayoutPanel chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            Button btnAll = MakeFilterChip("All Items", true);
            Button btnLow = MakeFilterChip("Low Stock", false);
            Button btnOut = MakeFilterChip("Out of Stock", false);
            Button btnHealthy = MakeFilterChip("Healthy", false);
            btnAll.Click += (s, e) => { _cboListMode.SelectedItem = "All"; ApplyInventoryFilter(); };
            btnLow.Click += (s, e) => { _cboListMode.SelectedItem = "Low Stock"; ApplyInventoryFilter(); };
            btnOut.Click += (s, e) => { _cboListMode.SelectedItem = "Out of Stock"; ApplyInventoryFilter(); };
            btnHealthy.Click += (s, e) => { _cboListMode.SelectedItem = "Healthy"; ApplyInventoryFilter(); };
            chips.Controls.AddRange(new Control[] { btnAll, btnLow, btnOut, btnHealthy });

            Panel searchPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0), Padding = new Padding(0) };
            _txtSearch = new TextBox { Width = 310, Height = 30, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Text = "" };
            _txtSearch.TextChanged += (s, e) => ApplyInventoryFilter();
            _cboListMode = new ComboBox { Visible = false };
            _cboListMode.Items.AddRange(new object[] { "All", "Low Stock", "Out of Stock", "Healthy" });
            _cboListMode.SelectedIndex = 0;
            Button btnRefresh = MakeBtn("↻", Color.White, 38); btnRefresh.ForeColor = InfoBlue; btnRefresh.FlatAppearance.BorderColor = DS.BorderStrong;
            searchPanel.Resize += (s, e) =>
            {
                btnRefresh.Location = new Point(Math.Max(0, searchPanel.ClientSize.Width - btnRefresh.Width), 10);
                int searchWidth = Math.Max(170, Math.Min(310, btnRefresh.Left - 10));
                _txtSearch.Width = searchWidth;
                _txtSearch.Location = new Point(Math.Max(0, btnRefresh.Left - searchWidth - 10), 11);
            };
            btnRefresh.Click += (s, e) => LoadList();
            searchPanel.Controls.AddRange(new Control[] { _txtSearch, btnRefresh });
            _lblStatus = new Label { Dock = DockStyle.Fill, Height = 24, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate500, Text = "Loading inventory...", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 0, 0, 2) };
            filterLayout.Controls.Add(chips, 0, 0);
            filterLayout.Controls.Add(searchPanel, 1, 0);
            filterLayout.Controls.Add(_lblStatus, 0, 1);
            filterLayout.SetColumnSpan(_lblStatus, 2);
            filters.Controls.Add(filterLayout);
            filters.Controls.Add(_cboListMode);

            _itemFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0)
            };
            Panel listWrap = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            listWrap.Controls.Add(_itemFlow);
            mainCard.Controls.Add(BuildInventoryFooter());
            mainCard.Controls.Add(listWrap);
            mainCard.Controls.Add(BuildInventoryTableHeader());
            mainCard.Controls.Add(filters);

            body.Controls.Add(mainCard);
            body.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 18, BackColor = DS.BgPage });
            body.Controls.Add(right);

            Controls.Add(body);
            Controls.Add(kpis);
            Controls.Add(header);
        }

        private Panel BuildDetailActionBar(params Button[] buttons)
        {
            int actionWidth = 0;
            foreach (var button in buttons)
                actionWidth += button.Width + 10;
            actionWidth = Math.Max(120, actionWidth - 10);

            Panel bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = Color.White,
                Padding = new Padding(20, 12, 20, 12)
            };
            bar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200, 1))
                    e.Graphics.DrawLine(pen, 0, 0, bar.Width, 0);
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(actionWidth, 34),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            foreach (var button in buttons)
            {
                button.Margin = new Padding(0, 0, 10, 0);
                flow.Controls.Add(button);
            }
            if (flow.Controls.Count > 0)
                flow.Controls[flow.Controls.Count - 1].Margin = new Padding(0);

            Label hint = new Label
            {
                Text = "Review stock details above. Primary actions stay pinned here.",
                Dock = DockStyle.Left,
                Width = 360,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Action layoutActions = () =>
            {
                flow.Location = new Point(Math.Max(20, bar.ClientSize.Width - flow.Width - 20), 15);
                hint.Width = Math.Max(0, flow.Left - 40);
            };
            bar.Resize += (s, e) => layoutActions();
            layoutActions();

            bar.Controls.Add(flow);
            bar.Controls.Add(hint);
            return bar;
        }

        private Panel CreateModernCard(string title)
        {
            Panel card = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 12)
            };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            DS.Rounded(card, 10);
            if (!string.IsNullOrWhiteSpace(title))
            {
                card.Controls.Add(new Label
                {
                    Text = title,
                    Dock = DockStyle.Top,
                    Height = 30,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = InfoBlue,
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }
            return card;
        }

        private Panel CreateInventoryMetric(string label, string value, ModernIconKind icon, Color accent, out Label valueLabel)
        {
            Panel card = CreateModernCard(null);
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 10, 0);
            Label iconLabel = ModernIconSystem.Badge(icon, 42, DS.Lighten(accent, 0.72f), accent, 14);
            iconLabel.Location = new Point(18, 22);
            valueLabel = new Label { Text = value, Location = new Point(74, 20), Size = new Size(170, 28), Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label caption = new Label { Text = label, Location = new Point(74, 50), Size = new Size(180, 22), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate600 };
            card.Controls.AddRange(new Control[] { iconLabel, valueLabel, caption });
            return card;
        }

        private Button MakeFilterChip(string text, bool selected)
        {
            Button button = MakeBtn(text, selected ? InfoBlue : Color.White, 96);
            button.ForeColor = selected ? Color.White : (text == "Out of Stock" ? DelRed : text == "Low Stock" ? WarnOrange : SaveGreen);
            button.FlatAppearance.BorderColor = selected ? InfoBlue : DS.BorderStrong;
            button.Margin = new Padding(0, 10, 10, 0);
            return button;
        }

        private Panel BuildInventoryTableHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = DS.Slate50, Padding = new Padding(12, 8, 12, 0) };
            string[] cols = { "ITEM DETAILS", "UNIT", "CURRENT STOCK", "VALUE (₹)", "STATUS", "ACTIONS" };
            int[] widths = { 420, 100, 150, 150, 130, 120 };
            int x = 8;
            for (int i = 0; i < cols.Length; i++)
            {
                header.Controls.Add(new Label { Text = cols[i], Location = new Point(x, 8), Size = new Size(widths[i], 18), Font = DS.CaptionBold(), ForeColor = DS.Slate700 });
                x += widths[i];
            }
            return header;
        }

        private Panel BuildInventoryFooter()
        {
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = DS.Slate50, Padding = new Padding(16, 8, 16, 8) };
            footer.Controls.Add(new Label { Text = "Rows per page", Location = new Point(0, 10), Size = new Size(92, 20), Font = DS.Small, ForeColor = DS.Slate600 });
            ComboBox rows = new ComboBox { Location = new Point(100, 7), Size = new Size(62, 26), DropDownStyle = ComboBoxStyle.DropDownList, Font = DS.Small };
            rows.Items.AddRange(new object[] { "25", "50", "100" });
            rows.SelectedIndex = 0;
            footer.Controls.Add(rows);
            Label showing = new Label { Text = "Showing 0 of 0 items", Dock = DockStyle.Right, Width = 180, Font = DS.Small, ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleRight };
            footer.Controls.Add(showing);
            return footer;
        }

        private Panel BuildInventoryQuickActions()
        {
            Panel card = CreateModernCard("QUICK ACTIONS");
            card.Dock = DockStyle.Top;
            card.Height = 200;
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 36, 0, 0) };
            for (int i = 0; i < 2; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            Button adjust = MakeBtn("Stock Adjustment", Color.White, 140); adjust.ForeColor = InfoBlue; adjust.FlatAppearance.BorderColor = DS.BorderStrong;
            Button reorder = MakeBtn("Reorder Suggestions", Color.White, 150); reorder.ForeColor = DS.Primary600; reorder.FlatAppearance.BorderColor = DS.BorderStrong; _btnReorder = reorder;
            Button transfer = MakeBtn("Stock Transfer", Color.White, 130); transfer.ForeColor = InfoBlue; transfer.FlatAppearance.BorderColor = DS.BorderStrong;
            Button bulk = MakeBtn("Bulk Update", Color.White, 120); bulk.ForeColor = InfoBlue; bulk.FlatAppearance.BorderColor = DS.BorderStrong;
            Button print = MakeBtn("Print Stock Report", Color.White, 150); print.ForeColor = InfoBlue; print.FlatAppearance.BorderColor = DS.BorderStrong;
            Button value = MakeBtn("Stock Valuation", Color.White, 135); value.ForeColor = SaveGreen; value.FlatAppearance.BorderColor = DS.BorderStrong;
            foreach (Button button in new[] { adjust, reorder, transfer, bulk, print, value })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(4, 5, 4, 5);
                button.MinimumSize = new Size(166, 34);
                button.Font = new Font("Segoe UI", 7.8f, FontStyle.Bold);
            }
            adjust.Click += (s, e) => FocusStockAdjustment();
            reorder.Click += (s, e) => ShowReorderSuggestions();
            transfer.Click += (s, e) => ShowStockTransferDialog();
            bulk.Click += async (s, e) => await ImportInventoryCsvAsync();
            print.Click += (s, e) => PreviewStockReport();
            value.Click += (s, e) => PreviewStockValuation();
            _toolTip.SetToolTip(adjust, "Select an item, adjust Current Stock, then save.");
            _toolTip.SetToolTip(reorder, "Load low-stock items. Select one to create a purchase order.");
            _toolTip.SetToolTip(transfer, "Transfer selected item stock between operational locations.");
            _toolTip.SetToolTip(bulk, "Import a CSV file to create or update inventory items.");
            _toolTip.SetToolTip(print, "Open a printable stock report preview.");
            _toolTip.SetToolTip(value, "Open current stock valuation summary.");
            grid.Controls.Add(adjust, 0, 0); grid.Controls.Add(reorder, 1, 0);
            grid.Controls.Add(transfer, 0, 1); grid.Controls.Add(bulk, 1, 1);
            grid.Controls.Add(print, 0, 2); grid.Controls.Add(value, 1, 2);
            card.Controls.Add(grid);
            return card;
        }

        private void BuildDetailPanel()
        {
            int y = 10;

            _detail.Controls.Add(MakeSectionLabel("ITEM DETAILS", ref y));

            _cboName = AddComboField("Item Name *", ref y, ComboBoxStyle.DropDown);
            _cboName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cboName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _cboName.AutoCompleteCustomSource = _itemSuggestions;

            _cboCategory = AddComboField("Category", ref y, ComboBoxStyle.DropDownList);
            _cboCategory.Items.AddRange(new object[] { "Filters", "Refrigerant", "Compressors", "Valves", "Belts", "Electrical", "Copper", "Tools", "HVAC Spares", "General" });
            if (_cboCategory.Items.Count > 0) _cboCategory.SelectedIndex = 0;

            _cboUnit = AddComboField("Unit", ref y, ComboBoxStyle.DropDownList);
            _cboUnit.Items.AddRange(new object[] { "Nos", "Kg", "Ltr", "Mtr", "Set", "Kit", "Tin", "SQFT" });
            _cboUnit.SelectedIndex = 0;

            _detail.Controls.Add(MakeSectionLabel("STOCK & PRICING", ref y));

            _detail.Controls.Add(MakeLabel("Current Stock", new Point(0, y + 3)));
            _numStock = new NumericUpDown
            {
                Location = new Point(138, y), Width = 132,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2, Maximum = 99999
            };
            _detail.Controls.Add(_numStock);
            y += 30;

            _detail.Controls.Add(MakeLabel("Last Purchase Rate", new Point(0, y + 3)));
            _numRate = new NumericUpDown
            {
                Location = new Point(138, y), Width = 132,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2, Maximum = 999999
            };
            _detail.Controls.Add(_numRate);
            y += 30;

            _detail.Controls.Add(MakeLabel("Reorder Level", new Point(0, y + 3)));
            _numReorder = new NumericUpDown
            {
                Location = new Point(138, y), Width = 132,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2, Maximum = 99999
            };
            _detail.Controls.Add(_numReorder);
            y += 30;

            _lblStockValue = new Label
            {
                Location = new Point(138, y), AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = InfoBlue
            };
            _detail.Controls.Add(_lblStockValue);
            y += 30;

            _numStock.ValueChanged += UpdateStockValue;
            _numRate.ValueChanged  += UpdateStockValue;

            _detail.Controls.Add(MakeSectionLabel("PREFERRED VENDOR", ref y));
            _detail.Controls.Add(MakeLabel("Vendor", new Point(0, y + 3)));
            _cboVendor = new ComboBox
            {
                Location = new Point(138, y), Width = 132,
                Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboVendor.Items.Add(new Vendor { VendorID = 0, VendorName = "(None)" });
            _cboVendor.SelectedIndex = 0;
            _detail.Controls.Add(_cboVendor);
        }

        private async Task LoadInitialDataAsync()
        {
            SetStatus("Loading inventory...", Color.Gray);
            var sw = Stopwatch.StartNew();
            var fetch = Stopwatch.StartNew();
            var itemTask = Task.Run(() => _svc.GetAll());
            var vendorTask = Task.Run(() => _vndSvc.GetAll());
            await Task.WhenAll(itemTask, vendorTask);
            AppRuntime.LogTiming("Inventory.FetchInitialData", fetch.ElapsedMilliseconds, "items=" + itemTask.Result.Count + ";vendors=" + vendorTask.Result.Count);

            var bind = Stopwatch.StartNew();
            PopulateVendorDropdown(vendorTask.Result);
            BindInventoryList(itemTask.Result, false);
            LoadItemSuggestions(itemTask.Result);
            AppRuntime.LogTiming("Inventory.BindInitialData", bind.ElapsedMilliseconds, "items=" + itemTask.Result.Count);
            AppRuntime.LogTiming("Inventory.InitialLoad", sw.ElapsedMilliseconds, "items=" + itemTask.Result.Count + ";vendors=" + vendorTask.Result.Count);
        }

        private void PopulateVendorDropdown(List<Vendor> vendors)
        {
            _cboVendor.BeginUpdate();
            _cboVendor.Items.Clear();
            _cboVendor.Items.Add(new Vendor { VendorID = 0, VendorName = "(None)" });
            foreach (var vendor in vendors ?? new List<Vendor>())
                _cboVendor.Items.Add(vendor);
            _cboVendor.SelectedIndex = 0;
            _cboVendor.EndUpdate();
        }

        private void UpdateStockValue(object sender, EventArgs e)
        {
            decimal val = _numStock.Value * _numRate.Value;
            _lblStockValue.Text = "Stock Value: Rs " + val.ToString("N2");
        }

        private void LoadItemSuggestions(List<StockItem> items = null)
        {
            var sw = Stopwatch.StartNew();
            _itemSuggestions.Clear();
            try
            {
                foreach (var item in items ?? _listSource)
                {
                    if (!string.IsNullOrWhiteSpace(item.ItemName))
                        _itemSuggestions.Add(item.ItemName);
                }
            }
            catch { }
            AppRuntime.LogTiming("Inventory.LoadItemSuggestions", sw.ElapsedMilliseconds, "suggestions=" + _itemSuggestions.Count);
        }

        private void BindInventoryList(List<StockItem> items, bool forceWarn)
        {
            _listSource = items ?? new List<StockItem>();
            if (!forceWarn)
                _allItems = new List<StockItem>(_listSource);
            UpdateInventoryMetrics(_allItems.Count > 0 ? _allItems : _listSource);
            RenderItemBatch(reset: true, forceWarn: forceWarn);
            string suffix = forceWarn ? "low-stock items" : "items";
            SetStatus($"Showing {Math.Min(_renderedCount, _listSource.Count)} of {_listSource.Count} {suffix}.", forceWarn ? WarnOrange : Color.Gray);
        }

        private void ApplyInventoryFilter()
        {
            if (_allItems == null || _allItems.Count == 0)
                return;

            string term = (_txtSearch?.Text ?? string.Empty).Trim();
            string mode = _cboListMode?.SelectedItem?.ToString() ?? "All";
            IEnumerable<StockItem> query = _allItems;
            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(i =>
                    (i.ItemName ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (i.Category ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            if (mode == "Low Stock")
                query = query.Where(i => i.IsLowStock && i.CurrentStock > 0);
            else if (mode == "Out of Stock")
                query = query.Where(i => i.CurrentStock <= 0);
            else if (mode == "Healthy")
                query = query.Where(i => !i.IsLowStock && i.CurrentStock > 0);

            _listSource = query.ToList();
            RenderItemBatch(reset: true, forceWarn: mode == "Low Stock" || mode == "Out of Stock");
            SetStatus($"Showing {Math.Min(_renderedCount, _listSource.Count)} of {_listSource.Count} items.", Color.Gray);
        }

        private void UpdateInventoryMetrics(List<StockItem> items)
        {
            items = items ?? new List<StockItem>();
            int inStock = items.Count(i => i.CurrentStock > 0 && !i.IsLowStock);
            int low = items.Count(i => i.IsLowStock && i.CurrentStock > 0);
            int outOfStock = items.Count(i => i.CurrentStock <= 0);
            decimal value = items.Sum(i => i.StockValue);
            if (_lblTotalItems != null) _lblTotalItems.Text = items.Count.ToString("N0");
            if (_lblInStockItems != null) _lblInStockItems.Text = inStock.ToString("N0");
            if (_lblLowStockItems != null) _lblLowStockItems.Text = low.ToString("N0");
            if (_lblOutStockItems != null) _lblOutStockItems.Text = outOfStock.ToString("N0");
            if (_lblTotalStockValue != null) _lblTotalStockValue.Text = "₹ " + FormatLakhs(value);
        }

        private static string FormatLakhs(decimal value)
        {
            if (Math.Abs(value) >= 100000m)
                return (value / 100000m).ToString("0.##") + " L";
            return value.ToString("N0");
        }

        private async void LoadList()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                SetStatus("Refreshing inventory...", Color.Gray);
                var items = await Task.Run(() => _svc.GetAll());
                BindInventoryList(items, false);
                LoadItemSuggestions(items);
            }
            catch (Exception ex) { SetStatus("Load error: " + ex.Message, Color.Red); }
            AppRuntime.LogTiming("Inventory.LoadList", sw.ElapsedMilliseconds, "items=" + (_listSource?.Count ?? 0));
        }

        private async void LoadLowStock()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                SetStatus("Loading low-stock items...", WarnOrange);
                var items = await Task.Run(() => _svc.GetLowStock());
                BindInventoryList(items, true);
            }
            catch (Exception ex) { SetStatus("Load error: " + ex.Message, Color.Red); }
            AppRuntime.LogTiming("Inventory.LoadLowStock", sw.ElapsedMilliseconds, "items=" + (_listSource?.Count ?? 0));
        }

        private void RenderItemBatch(bool reset, bool forceWarn)
        {
            if (reset)
            {
                _renderedCount = 0;
                _itemFlow.SuspendLayout();
                _itemFlow.Controls.Clear();
                _itemFlow.ResumeLayout();
            }

            int start = _renderedCount;
            int end = Math.Min(start + ItemBatchSize, _listSource.Count);

            _itemFlow.SuspendLayout();
            if (reset && _listSource.Count == 0)
            {
                _itemFlow.Controls.Add(BuildInventoryEmptyState());
            }
            for (int i = start; i < end; i++)
                _itemFlow.Controls.Add(MakeItemCard(_listSource[i], forceWarn));
            _renderedCount = end;

            AddLoadMoreCard(
                _itemFlow,
                _renderedCount < _listSource.Count,
                () =>
                {
                    _itemFlow.Controls.RemoveAt(_itemFlow.Controls.Count - 1);
                    BeginInvoke((Action)(() => RenderItemBatch(false, forceWarn)));
                });
            _itemFlow.ResumeLayout();
        }

        private Panel BuildInventoryEmptyState()
        {
            Panel panel = new Panel
            {
                Width = Math.Max(760, _itemFlow?.ClientSize.Width > 20 ? _itemFlow.ClientSize.Width - 8 : 880),
                Height = 420,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Inventory, 72, Color.FromArgb(238, 242, 255), InfoBlue);
            icon.Location = new Point((panel.Width - icon.Width) / 2, 130);
            panel.Controls.Add(icon);
            panel.Controls.Add(new Label { Text = "No items found", Location = new Point(0, 218), Size = new Size(panel.Width, 28), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter });
            panel.Controls.Add(new Label { Text = "Add your first inventory item to get started.", Location = new Point(0, 248), Size = new Size(panel.Width, 24), Font = DS.Body, ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleCenter });
            Button add = MakeBtn("+  Add Item", InfoBlue, 118);
            add.Location = new Point((panel.Width - add.Width) / 2, 294);
            add.Click += (s, e) => NewRecord();
            panel.Controls.Add(add);
            panel.Resize += (s, e) =>
            {
                icon.Left = (panel.Width - icon.Width) / 2;
                foreach (Control child in panel.Controls.OfType<Label>())
                    if (child != icon)
                        child.Width = panel.Width;
                add.Left = (panel.Width - add.Width) / 2;
            };
            return panel;
        }

        private void AddLoadMoreCard(FlowLayoutPanel host, bool show, Action onClick)
        {
            if (!show)
                return;

            var btn = new Button
            {
                Text = "Load More",
                Width = 360,
                Height = 36,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = InfoBlue,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 10),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = DS.Slate200;
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (s, e) => onClick?.Invoke();
            host.Controls.Add(btn);
        }

        private Panel MakeItemCard(StockItem item, bool forceWarn)
        {
            string lastUpdatedText = (item.LastUpdated == default(DateTime))
                ? "-"
                : item.LastUpdated.ToString("dd MMM");
            bool warn = forceWarn || item.IsLowStock;
            bool outOfStock = item.CurrentStock <= 0;
            int rowWidth = Math.Max(760, _itemFlow?.ClientSize.Width > 20 ? _itemFlow.ClientSize.Width - 8 : 880);

            Panel card = new Panel
            {
                Width = rowWidth,
                Height = 62,
                BackColor = Color.White,
                Margin = new Padding(0),
                Cursor = Cursors.Hand,
                Tag = item
            };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(card == _selectedCard ? InfoBlue : DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, card.Height - 1, card.Width, card.Height - 1);
                if (card == _selectedCard)
                    using (SolidBrush brush = new SolidBrush(InfoBlue))
                        e.Graphics.FillRectangle(brush, 0, 0, 3, card.Height);
            };
            card.Click += (s, e) => SelectItemCard(card, item);

            Label name = new Label { Text = item.ItemName, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(18, 10), Width = 390, Height = 20 };
            Label category = new Label { Text = (item.Category ?? "General") + "  •  Updated " + lastUpdatedText, Font = new Font("Segoe UI", 8), ForeColor = DS.Slate500, Location = new Point(18, 32), Width = 360 };
            Label unit = new Label { Text = DisplayUnit(item.Unit), Font = new Font("Segoe UI", 9), ForeColor = DS.Slate900, Location = new Point(430, 20), Width = 90 };
            Label stock = new Label { Text = item.CurrentStock.ToString("N1"), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = outOfStock ? DelRed : warn ? WarnOrange : SaveGreen, Location = new Point(540, 20), Width = 120 };
            Label value = new Label { Text = item.StockValue.ToString("N2"), Font = new Font("Segoe UI", 9), ForeColor = DS.Slate900, Location = new Point(690, 20), Width = 120 };
            Label badge = new Label
            {
                Text = outOfStock ? "Out of Stock" : warn ? "Low Stock" : "Healthy",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = outOfStock ? DelRed : warn ? WarnOrange : SaveGreen,
                BackColor = outOfStock ? DS.Red50 : warn ? DS.Amber50 : DS.Green50,
                Location = new Point(Math.Max(815, rowWidth - 128), 18),
                Width = 104,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(badge, 12);
            foreach (Control control in new Control[] { name, category, unit, stock, value, badge })
            {
                control.Click += (s, e) => SelectItemCard(card, item);
                card.Controls.Add(control);
            }
            return card;
        }

        private void SelectItemCard(Panel card, StockItem item)
        {
            if (_selectedCard != null)
                _selectedCard.Invalidate();
            _selectedCard = card;
            _selectedCard.Invalidate();
            _current = item;
            PopulateDetail(item);
        }

        private void PopulateDetail(StockItem item)
        {
            _cboName.Text = item.ItemName ?? "";
            SelectComboByText(_cboCategory, item.Category);
            SelectComboByText(_cboUnit, DisplayUnit(item.Unit));
            _numStock.Value   = item.CurrentStock    > _numStock.Maximum   ? _numStock.Maximum   : item.CurrentStock;
            _numRate.Value    = item.LastPurchaseRate > _numRate.Maximum    ? _numRate.Maximum    : item.LastPurchaseRate;
            _numReorder.Value = item.ReorderLevel    > _numReorder.Maximum ? _numReorder.Maximum : item.ReorderLevel;
            _lblStockValue.Text = "Stock Value: Rs " + item.StockValue.ToString("N2");
            UpdateReorderButtonState(item);

            for (int i = 0; i < _cboVendor.Items.Count; i++)
            {
                var v = (Vendor)_cboVendor.Items[i];
                if (v.VendorID == (item.VendorID ?? 0)) { _cboVendor.SelectedIndex = i; break; }
            }
        }

        private void NewRecord()
        {
            _current = null;
            _cboName.Text = "";
            if (_cboCategory.Items.Count > 0) _cboCategory.SelectedIndex = 0;
            _cboUnit.SelectedItem = "Nos";
            _numStock.Value = 0; _numRate.Value = 0; _numReorder.Value = 5;
            _cboVendor.SelectedIndex = 0;
            _lblStockValue.Text = "";
            UpdateReorderButtonState(null);
            SetStatus("New item ready. Select details and save.", Color.Gray);
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_cboName.Text))
            { SetStatus("Item Name is required.", Color.Red); return; }

            try
            {
                var vendor = _cboVendor.SelectedItem as Vendor;
                var item = new StockItem
                {
                    ItemName         = _cboName.Text.Trim(),
                    Category         = _cboCategory.SelectedItem?.ToString() ?? "",
                    Unit             = NormalizeUnit(_cboUnit.SelectedItem?.ToString() ?? "Nos"),
                    CurrentStock     = _numStock.Value,
                    LastPurchaseRate = _numRate.Value,
                    ReorderLevel     = _numReorder.Value,
                    VendorID         = (vendor != null && vendor.VendorID > 0) ? vendor.VendorID : (int?)null,
                };

                if (_current == null)
                    _svc.Create(item);
                else
                {
                    item.ItemID = _current.ItemID;
                    _svc.Update(item);
                }
                LoadList();
                SetStatus("Saved successfully.", SaveGreen);
            }
            catch (Exception ex) { SetStatus("Save error: " + ex.Message, Color.Red); }
        }

        private void CreatePO()
        {
            if (_current == null || !_current.IsLowStock)
            {
                MessageBox.Show(
                    "Please select a low-stock item first.",
                    "Create PO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // â”€â”€ Mini PO creation dialog â”€â”€
            var dlg = new Form
            {
                Text            = "Create Purchase Order â€” " + _current.ItemName,
                Width           = 380,
                Height          = 280,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            int dy = 16;

            // Vendor
            dlg.Controls.Add(new Label
            {
                Text = "Vendor:", Location = new Point(12, dy + 3),
                Width = 110, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9)
            });
            var cboVendor = new ComboBox
            {
                Location = new Point(128, dy), Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9)
            };
            var vendors = _vndSvc.GetAll();
            foreach (var v in vendors) cboVendor.Items.Add(v);
            if (cboVendor.Items.Count > 0) cboVendor.SelectedIndex = 0;

            // Pre-select preferred vendor if set
            if (_current.VendorID.HasValue)
            {
                for (int i = 0; i < cboVendor.Items.Count; i++)
                    if (((Vendor)cboVendor.Items[i]).VendorID == _current.VendorID.Value)
                    { cboVendor.SelectedIndex = i; break; }
            }
            dlg.Controls.Add(cboVendor);
            dy += 36;

            // Quantity
            dlg.Controls.Add(new Label
            {
                Text = "Quantity:", Location = new Point(12, dy + 3),
                Width = 110, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9)
            });
            var numQty = new NumericUpDown
            {
                Location = new Point(128, dy), Width = 120,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2,
                Minimum = 1, Maximum = 99999,
                Value = Math.Max(1, _current.ReorderLevel * 2)
            };
            dlg.Controls.Add(numQty);
            dy += 36;

            // Estimated Rate
            dlg.Controls.Add(new Label
            {
                Text = "Est. Rate (Rs):", Location = new Point(12, dy + 3),
                Width = 110, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9)
            });
            var numRate = new NumericUpDown
            {
                Location = new Point(128, dy), Width = 120,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2,
                Minimum = 0, Maximum = 9999999,
                Value = _current.LastPurchaseRate > 0 ? _current.LastPurchaseRate : 0
            };
            dlg.Controls.Add(numRate);
            dy += 44;

            // OK / Cancel buttons
            var btnOK = new Button
            {
                Text = "Create PO", DialogResult = DialogResult.OK,
                Location = new Point(128, dy), Width = 110, Height = 30,
                BackColor = SaveGreen, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button
            {
                Text = "Cancel", DialogResult = DialogResult.Cancel,
                Location = new Point(248, dy), Width = 80, Height = 30,
                Font = new Font("Segoe UI", 9)
            };
            dlg.Controls.AddRange(new Control[] { btnOK, btnCancel });
            dlg.AcceptButton = btnOK;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (cboVendor.SelectedItem == null)
            {
                MessageBox.Show("Please select a vendor.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var selectedVendor = (Vendor)cboVendor.SelectedItem;
                decimal qty  = numQty.Value;
                decimal rate = numRate.Value;
                decimal total = qty * rate;
                string poNumber = "PO-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

                var po = new PurchaseOrder
                {
                    VendorID    = selectedVendor.VendorID,
                    PONumber    = poNumber,
                    PODate      = DateTime.Today,
                    TotalAmount = total,
                    Status      = "Pending",
                    Notes       = "Auto-created from low-stock alert"
                };
                po.LineItems.Add(new PurchaseLineItem
                {
                    Description = _current.ItemName,
                    Quantity    = qty,
                    Rate        = rate,
                    Amount      = total
                });

                _poSvc.Create(po);

                MessageBox.Show(
                    $"Purchase Order created: {poNumber}",
                    "PO Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                SetStatus("PO created: " + poNumber, SaveGreen);
            }
            catch (Exception ex) { SetStatus("PO creation error: " + ex.Message, Color.Red); }
        }

        private void FocusStockAdjustment()
        {
            if (_current == null)
            {
                SetStatus("Select an item to adjust stock.", WarnOrange);
                _txtSearch.Focus();
                return;
            }

            _numStock.Focus();
            _numStock.Select(0, _numStock.Text.Length);
            SetStatus("Update Current Stock and save the item.", InfoBlue);
        }

        private void ShowReorderSuggestions()
        {
            if (_current != null && _current.IsLowStock)
            {
                CreatePO();
                return;
            }

            LoadLowStock();
            SetStatus("Low-stock suggestions loaded. Select an item to create a PO.", WarnOrange);
        }

        private void ShowStockTransferDialog()
        {
            if (_current == null)
            {
                SetStatus("Select an item before transferring stock.", WarnOrange);
                _txtSearch.Focus();
                return;
            }

            using (var dialog = new Form())
            {
                dialog.Text = "Transfer Stock - " + _current.ItemName;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.BackColor = Color.White;
                dialog.ClientSize = new Size(470, 320);

                Label title = new Label { Text = _current.ItemName, Location = new Point(18, 14), Size = new Size(420, 24), Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = DS.Slate900 };
                Label stock = new Label { Text = "Available stock: " + _current.CurrentStock.ToString("N2") + " " + DisplayUnit(_current.Unit), Location = new Point(18, 42), Size = new Size(420, 20), Font = DS.Body, ForeColor = DS.Slate600 };
                NumericUpDown qty = new NumericUpDown { Location = new Point(150, 82), Width = 150, DecimalPlaces = 2, Minimum = 0.01m, Maximum = Math.Max(0.01m, _current.CurrentStock), Value = Math.Min(Math.Max(0.01m, _current.CurrentStock), 1m), Font = DS.Body };
                ComboBox from = new ComboBox { Location = new Point(150, 120), Width = 270, DropDownStyle = ComboBoxStyle.DropDown, Font = DS.Body };
                ComboBox to = new ComboBox { Location = new Point(150, 158), Width = 270, DropDownStyle = ComboBoxStyle.DropDown, Font = DS.Body };
                TextBox reference = new TextBox { Location = new Point(150, 196), Width = 270, Font = DS.Body };
                TextBox notes = new TextBox { Location = new Point(150, 234), Width = 270, Height = 44, Multiline = true, Font = DS.Body };

                from.Items.AddRange(new object[] { "Main Store", "Service Van", "Site Store", "Vendor Return", "Damaged Hold" });
                to.Items.AddRange(new object[] { "Main Store", "Service Van", "Site Store", "Vendor Return", "Damaged Hold" });
                from.Text = "Main Store";
                to.Text = "Service Van";

                dialog.Controls.Add(title);
                dialog.Controls.Add(stock);
                AddDialogLabel(dialog, "Quantity *", 82);
                AddDialogLabel(dialog, "From location *", 120);
                AddDialogLabel(dialog, "To location *", 158);
                AddDialogLabel(dialog, "Reference", 196);
                AddDialogLabel(dialog, "Notes", 234);
                dialog.Controls.Add(qty);
                dialog.Controls.Add(from);
                dialog.Controls.Add(to);
                dialog.Controls.Add(reference);
                dialog.Controls.Add(notes);

                Button cancel = DS.GhostBtn("Cancel", 96, 34);
                Button save = DS.PrimaryBtn("Transfer", 108, 34);
                cancel.Location = new Point(214, 286);
                save.Location = new Point(322, 286);
                cancel.Click += (s, e) => dialog.DialogResult = DialogResult.Cancel;
                save.Click += (s, e) =>
                {
                    try
                    {
                        _svc.TransferStock(_current.ItemID, qty.Value, from.Text, to.Text, reference.Text, notes.Text);
                        dialog.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(dialog, ex.Message, "Stock Transfer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };
                dialog.Controls.Add(cancel);
                dialog.Controls.Add(save);
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SetStatus("Stock transferred successfully.", SaveGreen);
                    LoadList();
                }
            }
        }

        private static void AddDialogLabel(Control parent, string text, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Location = new Point(18, y + 3),
                Size = new Size(120, 20),
                Font = DS.SmallBold,
                ForeColor = DS.Slate700,
                TextAlign = ContentAlignment.MiddleRight
            });
        }

        private void ExportInventoryCsv()
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "CSV files (*.csv)|*.csv";
                    dialog.FileName = "Inventory_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    List<StockItem> rows = (_allItems != null && _allItems.Count > 0 ? _allItems : _listSource) ?? new List<StockItem>();
                    var sb = new StringBuilder();
                    sb.AppendLine("ItemName,Category,Unit,CurrentStock,ReservedStock,AvailableStock,LastPurchaseRate,ReorderLevel,StockValue,VendorName,Status,LastUpdated");
                    foreach (StockItem item in rows.OrderBy(i => i.Category).ThenBy(i => i.ItemName))
                    {
                        sb.AppendLine(string.Join(",",
                            Csv(item.ItemName),
                            Csv(item.Category),
                            Csv(DisplayUnit(item.Unit)),
                            Csv(item.CurrentStock.ToString(CultureInfo.InvariantCulture)),
                            Csv(item.ReservedStock.ToString(CultureInfo.InvariantCulture)),
                            Csv(item.AvailableStock.ToString(CultureInfo.InvariantCulture)),
                            Csv(item.LastPurchaseRate.ToString(CultureInfo.InvariantCulture)),
                            Csv(item.ReorderLevel.ToString(CultureInfo.InvariantCulture)),
                            Csv(item.StockValue.ToString(CultureInfo.InvariantCulture)),
                            Csv(item.VendorName),
                            Csv(item.CurrentStock <= 0 ? "Out of Stock" : item.IsLowStock ? "Low Stock" : "Healthy"),
                            Csv(item.LastUpdated == default(DateTime) ? "" : item.LastUpdated.ToString("yyyy-MM-dd HH:mm"))));
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                    SetStatus("Exported inventory: " + Path.GetFileName(dialog.FileName), SaveGreen);
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.ExportInventoryCsv", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Export inventory", ex);
            }
        }

        private async Task ImportInventoryCsvAsync()
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    dialog.Title = "Import inventory CSV";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    SetStatus("Importing inventory...", InfoBlue);
                    int[] counts = new int[2];
                    List<string> errors = new List<string>();
                    await Task.Run(() => ImportInventoryRows(dialog.FileName, counts, errors));
                    LoadList();

                    string message = "Import complete. Created " + counts[0] + ", updated " + counts[1] + ".";
                    if (errors.Count > 0)
                        message += " Skipped " + errors.Count + " row(s).";
                    SetStatus(message, errors.Count > 0 ? WarnOrange : SaveGreen);
                    if (errors.Count > 0)
                    {
                        MessageBox.Show(this, message + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, errors.Take(15)), "Inventory Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.ImportInventoryCsvAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Import inventory", ex);
                SetStatus("Import error: " + ex.Message, Color.Red);
            }
        }

        private void ImportInventoryRows(string fileName, int[] counts, List<string> errors)
        {
            string[] lines = File.ReadAllLines(fileName);
            if (lines.Length == 0)
                return;

            Dictionary<string, int> map = BuildCsvHeaderMap(ParseCsvLine(lines[0]));
            List<StockItem> existing = _svc.GetAll();
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                try
                {
                    List<string> cells = ParseCsvLine(lines[i]);
                    string name = CsvValue(cells, map, "ItemName", "Item Name", "Name");
                    if (string.IsNullOrWhiteSpace(name))
                        throw new InvalidOperationException("Item name is required.");

                    StockItem item = new StockItem
                    {
                        ItemName = name.Trim(),
                        Category = CsvValue(cells, map, "Category"),
                        Unit = NormalizeUnit(CsvValue(cells, map, "Unit", "UOM")),
                        CurrentStock = ParseDecimal(CsvValue(cells, map, "CurrentStock", "Current Stock", "Stock")),
                        LastPurchaseRate = ParseDecimal(CsvValue(cells, map, "LastPurchaseRate", "Last Purchase Rate", "Rate")),
                        ReorderLevel = ParseDecimal(CsvValue(cells, map, "ReorderLevel", "Reorder Level")),
                    };

                    StockItem match = existing.FirstOrDefault(x => string.Equals(x.ItemName, item.ItemName, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                    {
                        item.ItemID = _svc.Create(item);
                        existing.Add(item);
                        counts[0]++;
                    }
                    else
                    {
                        item.ItemID = match.ItemID;
                        item.VendorID = match.VendorID;
                        _svc.Update(item);
                        counts[1]++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add("Row " + (i + 1) + ": " + ex.Message);
                }
            }
        }

        private void PreviewStockReport()
        {
            List<StockItem> rows = (_allItems != null && _allItems.Count > 0 ? _allItems : _listSource) ?? new List<StockItem>();
            string html = BuildInventoryHtml("Inventory Stock Report", rows);
            new HtmlPreviewDialog("Inventory Stock Report", html).ShowDialog(this);
        }

        private void PreviewStockValuation()
        {
            List<StockItem> rows = ((_allItems != null && _allItems.Count > 0 ? _allItems : _listSource) ?? new List<StockItem>())
                .OrderByDescending(i => i.StockValue)
                .ToList();
            string html = BuildInventoryHtml("Inventory Valuation", rows);
            new HtmlPreviewDialog("Inventory Valuation", html).ShowDialog(this);
        }

        private string BuildInventoryHtml(string title, List<StockItem> rows)
        {
            rows = rows ?? new List<StockItem>();
            decimal value = rows.Sum(i => i.StockValue);
            int low = rows.Count(i => i.IsLowStock && i.CurrentStock > 0);
            int outOfStock = rows.Count(i => i.CurrentStock <= 0);
            var sb = new StringBuilder();
            sb.Append("<html><head><style>");
            sb.Append("body{font-family:Segoe UI,Arial,sans-serif;color:#0f172a;margin:28px}h1{font-size:24px;margin:0 0 6px}.meta{color:#64748b;margin-bottom:18px}.cards{display:flex;gap:12px;margin-bottom:18px}.card{border:1px solid #e2e8f0;border-radius:10px;padding:12px 16px;min-width:150px}.label{color:#64748b;font-size:12px}.value{font-size:20px;font-weight:700}table{border-collapse:collapse;width:100%;font-size:12px}th{background:#f1f5f9;text-align:left}th,td{border:1px solid #e2e8f0;padding:8px}.right{text-align:right}.low{color:#d97706;font-weight:700}.out{color:#dc2626;font-weight:700}.ok{color:#16a34a;font-weight:700}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>").Append(Html(title)).Append("</h1>");
            sb.Append("<div class='meta'>Generated ").Append(DateTime.Now.ToString("dd MMM yyyy HH:mm")).Append("</div>");
            sb.Append("<div class='cards'>");
            sb.Append(KpiHtml("Items", rows.Count.ToString("N0")));
            sb.Append(KpiHtml("Low stock", low.ToString("N0")));
            sb.Append(KpiHtml("Out of stock", outOfStock.ToString("N0")));
            sb.Append(KpiHtml("Stock value", IndiaFormatHelper.FormatCurrency(value)));
            sb.Append("</div><table><tr><th>Item</th><th>Category</th><th>Unit</th><th class='right'>Stock</th><th class='right'>Rate</th><th class='right'>Value</th><th>Status</th><th>Vendor</th></tr>");
            foreach (StockItem item in rows)
            {
                string status = item.CurrentStock <= 0 ? "Out of Stock" : item.IsLowStock ? "Low Stock" : "Healthy";
                string cls = item.CurrentStock <= 0 ? "out" : item.IsLowStock ? "low" : "ok";
                sb.Append("<tr><td>").Append(Html(item.ItemName)).Append("</td><td>").Append(Html(item.Category)).Append("</td><td>").Append(Html(DisplayUnit(item.Unit))).Append("</td><td class='right'>").Append(item.CurrentStock.ToString("N2")).Append("</td><td class='right'>").Append(item.LastPurchaseRate.ToString("N2")).Append("</td><td class='right'>").Append(item.StockValue.ToString("N2")).Append("</td><td class='").Append(cls).Append("'>").Append(status).Append("</td><td>").Append(Html(item.VendorName)).Append("</td></tr>");
            }
            sb.Append("</table></body></html>");
            return sb.ToString();
        }

        private static string KpiHtml(string label, string value)
        {
            return "<div class='card'><div class='label'>" + Html(label) + "</div><div class='value'>" + Html(value) + "</div></div>";
        }

        private static Dictionary<string, int> BuildCsvHeaderMap(List<string> headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
                if (!map.ContainsKey(headers[i].Trim()))
                    map[headers[i].Trim()] = i;
            return map;
        }

        private static string CsvValue(List<string> cells, Dictionary<string, int> map, params string[] names)
        {
            foreach (string name in names)
            {
                int index;
                if (map.TryGetValue(name, out index) && index >= 0 && index < cells.Count)
                    return cells[index];
            }
            return string.Empty;
        }

        private static decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;
            value = value.Replace("Rs", string.Empty).Replace("INR", string.Empty).Replace(",", string.Empty).Trim();
            decimal parsed;
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) &&
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
                throw new InvalidOperationException("Invalid number: " + value);
            return parsed;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < (line ?? string.Empty).Length; i++)
            {
                char c = line[i];
                if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            values.Add(current.ToString());
            return values;
        }

        private static string Csv(string value)
        {
            string safe = (value ?? string.Empty).Replace("\"", "\"\"");
            return "\"" + safe + "\"";
        }

        private static string Html(string value)
        {
            return System.Web.HttpUtility.HtmlEncode(value ?? string.Empty);
        }

        private void SetStatus(string msg, Color c) { _lblStatus.Text = msg; _lblStatus.ForeColor = c; }

        private void UpdateReorderButtonState(StockItem item)
        {
            if (_btnReorder == null)
                return;

            bool enabled = item != null && item.CurrentStock < item.ReorderLevel;
            _btnReorder.Enabled = enabled;
            _btnReorder.ForeColor = enabled ? DS.Primary600 : Color.Gray;
        }

        private static string NormalizeUnit(string unit)
        {
            string normalized = (unit ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized == "NO" || normalized == "NOS.")
                return "NOS";
            return string.IsNullOrWhiteSpace(normalized) ? "NOS" : normalized;
        }

        private static string DisplayUnit(string unit)
        {
            switch (NormalizeUnit(unit))
            {
                case "NOS": return "Nos";
                case "KG": return "Kg";
                case "LTR": return "Ltr";
                case "MTR": return "Mtr";
                case "SQFT": return "Sqft";
                default: return NormalizeUnit(unit);
            }
        }

        private ComboBox AddComboField(string label, ref int y, ComboBoxStyle style)
        {
            _detail.Controls.Add(MakeLabel(label, new Point(0, y + 3)));
            var combo = new ComboBox
            {
                Location = new Point(138, y), Width = 132,
                Font = new Font("Segoe UI", 9), DropDownStyle = style
            };
            _detail.Controls.Add(combo);
            y += 30;
            return combo;
        }

        private void SelectComboByText(ComboBox combo, string value)
        {
            if (combo == null) return;
            string text = value ?? "";
            int index = combo.Items.IndexOf(text);
            if (index >= 0)
                combo.SelectedIndex = index;
            else if (combo.DropDownStyle != ComboBoxStyle.DropDownList)
                combo.Text = text;
        }

        private Label MakeSectionLabel(string text, ref int y)
        {
            y += 8;
            var lbl = new Label
            {
                Text = text, Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = InfoBlue, Location = new Point(0, y), Width = 270, Height = 22,
                BackColor = SectionBg, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            y += 28;
            return lbl;
        }

        private Label MakeLabel(string text, System.Drawing.Point loc) => new Label
        {
            Text = text, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray,
            Location = loc, Width = 126, TextAlign = ContentAlignment.MiddleRight
        };

        private Button MakeBtn(string text, Color bg, int width)
        {
            var btn = new Button
            {
                Text = text, Width = width, Height = 30, BackColor = bg, ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold), FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}

