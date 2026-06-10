using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using HVAC_Pro_Desktop.UI.Controls;

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
        private int _inventoryPage = 1;
        private int _inventoryPageSize = 25;
        private bool _inventoryForceWarn;
        private GlobalPaginationControl _inventoryPager;
        private BackgroundWorker _initialInventoryLoadWorker;

        private StockItem _current;
        private AutoCompleteStringCollection _itemSuggestions = new AutoCompleteStringCollection();

        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color DelRed = DS.Red600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color WarnOrange = DS.Amber500;

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public InventoryForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            QueueInitialInventoryLoad();
        }

        private void QueueInitialInventoryLoad()
        {
            if (_initialInventoryLoadWorker != null)
                return;

            SetStatus("Loading inventory...", Color.Gray);
            _initialInventoryLoadWorker = CreateWorker();
            _initialInventoryLoadWorker.DoWork += (s, e) =>
            {
                Stopwatch fetch = Stopwatch.StartNew();
                e.Result = new InventoryLoadSnapshot
                {
                    Items = _svc.GetAll() ?? new List<StockItem>(),
                    Vendors = SafeLoadSuppliersForDropdown()
                };
                AppRuntime.LogTiming("Inventory.FetchInitialData", fetch.ElapsedMilliseconds);
            };
            _initialInventoryLoadWorker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    AppRuntime.LogException("InventoryForm.InitialLoad", e.Error);
                    RunOnUI(() =>
                    {
                        BackgroundWorker worker = _initialInventoryLoadWorker;
                        _initialInventoryLoadWorker = null;
                        worker?.Dispose();
                        if (IsDisposed)
                            return;
                        SetStatus("Inventory load error. Click refresh to try again.", DelRed);
                        MarkDeferredLoadCompleted();
                    });
                    ShowError( "Failed to load inventory. Please try again.", e.Error);
                    return;
                }
                if (e.Cancelled)
                {
                    MarkDeferredLoadCompleted();
                    return;
                }

                RunOnUI(() =>
                {
                    BackgroundWorker worker = _initialInventoryLoadWorker;
                    _initialInventoryLoadWorker = null;
                    worker?.Dispose();
                    if (IsDisposed)
                        return;

                    Stopwatch bind = Stopwatch.StartNew();
                    InventoryLoadSnapshot snapshot = e.Result as InventoryLoadSnapshot ?? new InventoryLoadSnapshot();
                    List<StockItem> items = snapshot.Items ?? new List<StockItem>();
                    List<Vendor> vendors = snapshot.Vendors ?? new List<Vendor>();
                    PopulateVendorDropdown(vendors);
                    BindInventoryList(items, false);
                    LoadItemSuggestions(items);
                    AppRuntime.LogTiming("Inventory.BindInitialData", bind.ElapsedMilliseconds, "items=" + items.Count);
                    AppRuntime.LogTiming("Inventory.InitialLoad", bind.ElapsedMilliseconds, "items=" + items.Count + ";vendors=" + vendors.Count);
                    MarkDeferredLoadCompleted();
                });
            };
            _initialInventoryLoadWorker.RunWorkerAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _initialInventoryLoadWorker != null)
            {
                _initialInventoryLoadWorker.Dispose();
                _initialInventoryLoadWorker = null;
            }

            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            Stopwatch layoutWatch = Stopwatch.StartNew();
            Stopwatch phaseWatch = Stopwatch.StartNew();
            Controls.Clear();
            BackColor = DS.BgPage;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = DS.BgPage, Padding = new Padding(32, 22, 24, 10) };
            Label title = new Label { Text = "Materials / Procurement", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(32, 22), Size = new Size(420, 32) };
            Label sub = new Label { Text = "Manage material catalog, supplier links, purchase rates, and reorder planning.", Font = new Font("Segoe UI", 9), ForeColor = DS.Slate600, Location = new Point(32, 58), Size = new Size(620, 22) };
            Button btnExport = MakeBtn("Export CSV", Color.White, 104); btnExport.ForeColor = DS.Slate700; btnExport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnImport = MakeBtn("Import CSV", Color.White, 104); btnImport.ForeColor = DS.Slate700; btnImport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnForms = MakeBtn("Service Forms", Color.White, 108); btnForms.ForeColor = InfoBlue; btnForms.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            Button btnNew = MakeBtn("+ Add Item", InfoBlue, 118);
            Panel headerActions = new Panel
            {
                Dock = DockStyle.Right,
                Width = 540,
                BackColor = DS.BgPage,
                Padding = Padding.Empty,
                Margin = new Padding(0)
            };
            btnExport.Margin = Padding.Empty;
            btnImport.Margin = Padding.Empty;
            btnForms.Margin = Padding.Empty;
            btnNew.Margin = Padding.Empty;
            headerActions.Controls.AddRange(new Control[] { btnExport, btnImport, btnForms, btnNew });
            Action layoutHeaderActions = () =>
            {
                Button[] buttons = { btnExport, btnImport, btnForms, btnNew };
                int gap = 12;
                int total = buttons.Sum(button => button.Width) + (gap * (buttons.Length - 1));
                int x = Math.Max(0, headerActions.ClientSize.Width - total);
                int y = Math.Max(0, (headerActions.ClientSize.Height - btnNew.Height) / 2);
                foreach (Button button in buttons)
                {
                    button.Location = new Point(x, y);
                    x += button.Width + gap;
                }
            };
            headerActions.Resize += (s, e) => layoutHeaderActions();
            header.Resize += (s, e) =>
            {
                int reserved = headerActions.Width + 48;
                title.Width = Math.Max(220, header.ClientSize.Width - reserved - title.Left);
                sub.Width = Math.Max(220, header.ClientSize.Width - reserved - sub.Left);
                layoutHeaderActions();
            };
            layoutHeaderActions();
            btnNew.Click += (s, e) => NewRecord();
            btnImport.Click += async (s, e) => await ImportInventoryCsvAsync();
            btnExport.Click += (s, e) => ExportInventoryCsv();
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Materials / Procurement", "Purchases", null, "spare parts requisition purchase order supplier quote goods received note material usage");
            header.Controls.AddRange(new Control[] { title, sub, headerActions });
            AppRuntime.LogTiming("Inventory.BuildLayout.Header", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, BackColor = DS.BgPage, Padding = new Padding(24, 8, 24, 14), ColumnCount = 5, RowCount = 1 };
            for (int i = 0; i < 5; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            kpis.Controls.Add(CreateInventoryMetric("Total Items", "0", ModernIconKind.Inventory, InfoBlue, out _lblTotalItems), 0, 0);
            kpis.Controls.Add(CreateInventoryMetric("Supplier Linked", "0", ModernIconKind.Checklist, SaveGreen, out _lblInStockItems), 1, 0);
            kpis.Controls.Add(CreateInventoryMetric("To Order Items", "0", ModernIconKind.Alert, WarnOrange, out _lblLowStockItems), 2, 0);
            kpis.Controls.Add(CreateInventoryMetric("Needs Supplier", "0", ModernIconKind.Alert, DelRed, out _lblOutStockItems), 3, 0);
            kpis.Controls.Add(CreateInventoryMetric("Priced Items", "0", ModernIconKind.Payment, InfoBlue, out _lblTotalStockValue), 4, 0);
            AppRuntime.LogTiming("Inventory.BuildLayout.Kpis", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            Panel modeGuide = BuildInventoryModeGuide();
            AppRuntime.LogTiming("Inventory.BuildLayout.ModeGuide", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(24, 0, 24, 16) };
            Panel right = CreateModernCard("ITEM DETAILS");
            right.Dock = DockStyle.Right;
            right.Width = 440;
            right.MinimumSize = new Size(440, 0);
            right.Padding = new Padding(18, 44, 18, 14);

            _detail = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _detail.HorizontalScroll.Enabled = false;
            _detail.HorizontalScroll.Visible = false;
            BuildDetailPanel();
            AppRuntime.LogTiming("Inventory.BuildLayout.DetailPanel", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();
            Button saveItem = MakeBtn("Save Item", SaveGreen, 104);
            Button clearItem = MakeBtn("New Item", Color.White, 94);
            Button createPo = MakeBtn("Purchase Request", InfoBlue, 140);
            clearItem.ForeColor = DS.Slate700;
            clearItem.FlatAppearance.BorderColor = DS.BorderStrong;
            saveItem.Click += (s, e) => Save();
            clearItem.Click += (s, e) => NewRecord();
            createPo.Click += (s, e) => CreatePO();
            Panel quick = BuildInventoryQuickActions();
            quick.Dock = DockStyle.Bottom;
            right.Controls.Add(_detail);
            right.Controls.Add(quick);
            right.Controls.Add(BuildDetailActionBar(saveItem, clearItem, createPo));
            AppRuntime.LogTiming("Inventory.BuildLayout.RightPanel", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

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
            Button btnLow = MakeFilterChip("To Order", false);
            Button btnOut = MakeFilterChip("Supplier Linked", false);
            Button btnNeedsVendor = MakeFilterChip("Needs Supplier", false);
            btnAll.Click += (s, e) => { _cboListMode.SelectedItem = "All"; ApplyInventoryFilter(); };
            btnLow.Click += (s, e) => { _cboListMode.SelectedItem = "To Order"; ApplyInventoryFilter(); };
            btnOut.Click += (s, e) => { _cboListMode.SelectedItem = "Supplier Linked"; ApplyInventoryFilter(); };
            btnNeedsVendor.Click += (s, e) => { _cboListMode.SelectedItem = "Needs Supplier"; ApplyInventoryFilter(); };
            chips.Controls.AddRange(new Control[] { btnAll, btnLow, btnOut, btnNeedsVendor });

            Panel searchPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0), Padding = new Padding(0) };
            _txtSearch = new TextBox { Width = 310, Height = 30, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Text = "" };
            _txtSearch.TextChanged += (s, e) => ApplyInventoryFilter();
            _cboListMode = new ComboBox { Visible = false, FlatStyle = FlatStyle.Standard, Tag = "CUSTOM_INPUT_SHELL" };
            _cboListMode.Items.AddRange(new object[] { "All", "To Order", "Supplier Linked", "Needs Supplier" });
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
            AppRuntime.LogTiming("Inventory.BuildLayout.Filters", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

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
            AppRuntime.LogTiming("Inventory.BuildLayout.MainCard", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            body.Controls.Add(mainCard);
            body.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 18, BackColor = DS.BgPage });
            body.Controls.Add(right);

            Controls.Add(body);
            Controls.Add(modeGuide);
            Controls.Add(kpis);
            Controls.Add(header);
            AppRuntime.LogTiming("Inventory.BuildLayout.RootAdd", phaseWatch.ElapsedMilliseconds);
            AppRuntime.LogTiming("Inventory.BuildLayout.Total", layoutWatch.ElapsedMilliseconds);
        }

        private Panel BuildInventoryModeGuide()
        {
            Panel guide = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = DS.BgPage, Padding = new Padding(24, 0, 24, 10) };
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = DS.BgPage };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            grid.Controls.Add(BuildInventoryModeChip("Stock List", "Search, filter, and review material readiness.", InfoBlue), 0, 0);
            grid.Controls.Add(BuildInventoryModeChip("Item Details", "Only item name is mandatory to save.", SaveGreen), 1, 0);
            grid.Controls.Add(BuildInventoryModeChip("Movement", "Update quantity or transfer stock after selection.", WarnOrange), 2, 0);
            grid.Controls.Add(BuildInventoryModeChip("Supplier Request", "Create a purchase request when stock is low.", DelRed), 3, 0);
            guide.Controls.Add(grid);
            return guide;
        }

        private Panel BuildInventoryModeChip(string title, string subtitle, Color accent)
        {
            Panel chip = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), BackColor = Color.White, Padding = new Padding(10, 6, 10, 5) };
            chip.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, chip.Width - 1, chip.Height - 1);
            };
            DS.Rounded(chip, 8);
            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 18, Font = DS.SmallBold, ForeColor = accent };
            Label subtitleLabel = new Label { Text = subtitle, Dock = DockStyle.Fill, Font = DS.Caption, ForeColor = DS.Slate600 };
            chip.Controls.Add(subtitleLabel);
            chip.Controls.Add(titleLabel);
            return chip;
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
                Text = "Review material details above. Save, reset, and purchase request actions stay pinned here.",
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
            CardResizeGripService.Attach(card);
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
            button.ForeColor = selected ? Color.White : (text == "Needs Supplier" ? DelRed : text == "To Order" ? WarnOrange : SaveGreen);
            button.FlatAppearance.BorderColor = selected ? InfoBlue : DS.BorderStrong;
            button.Margin = new Padding(0, 10, 10, 0);
            return button;
        }

        private Panel BuildInventoryTableHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = DS.Slate50, Padding = new Padding(12, 8, 12, 0) };
            string[] cols = { "ITEM DETAILS", "UNIT", "CURRENT QTY", "VALUE (₹)", "STATUS", "ACTIONS" };
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
            _inventoryPager = new GlobalPaginationControl
            {
                Dock = DockStyle.Right,
                Width = 560,
                Height = 34,
                BackColor = DS.Slate50
            };
            _inventoryPager.PageChanged += (s, e) =>
            {
                _inventoryPage = _inventoryPager.CurrentPage;
                RenderItemBatch(false, _inventoryForceWarn);
            };
            _inventoryPager.PageSizeChanged += (s, e) =>
            {
                _inventoryPageSize = _inventoryPager.PageSize;
                _inventoryPage = 1;
                RenderItemBatch(false, _inventoryForceWarn);
            };
            _inventoryPager.SetState(_inventoryPage, 0, _inventoryPageSize);
            footer.Controls.Add(_inventoryPager);
            return footer;
        }

        private Panel BuildInventoryQuickActions()
        {
            Panel card = CreateModernCard("QUICK ACTIONS");
            card.Dock = DockStyle.Top;
            card.Height = 150;
            card.Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE";
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 36, 0, 0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Button adjust = MakeBtn("Update Quantity", Color.White, 140); adjust.ForeColor = InfoBlue; adjust.FlatAppearance.BorderColor = DS.BorderStrong;
            Button reorder = MakeBtn("Ordering Plan", Color.White, 150); reorder.ForeColor = DS.Primary600; reorder.FlatAppearance.BorderColor = DS.BorderStrong; _btnReorder = reorder;
            Button open = MakeBtn("More Actions", Color.White, 150); open.ForeColor = InfoBlue; open.FlatAppearance.BorderColor = DS.BorderStrong;
            Button delete = MakeBtn("Delete Item", Color.White, 150); delete.ForeColor = DelRed; delete.FlatAppearance.BorderColor = DS.Border;
            foreach (Button button in new[] { adjust, reorder, open, delete })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(4, 3, 4, 5);
                button.MinimumSize = new Size(130, 30);
                button.Font = new Font("Segoe UI", 7.8f, FontStyle.Bold);
            }
            adjust.Click += (s, e) => FocusStockAdjustment();
            reorder.Click += (s, e) => ShowReorderSuggestions();
            open.Click += (s, e) => ShowInventoryActionsMenu(open);
            delete.Click += (s, e) => DeleteCurrentItem();
            _toolTip.SetToolTip(adjust, "Select an item, update the current quantity, then save.");
            _toolTip.SetToolTip(reorder, "Load materials that need supplier ordering. Select one to create a purchase request.");
            _toolTip.SetToolTip(open, "Open bulk update, material report, and purchase valuation actions.");
            _toolTip.SetToolTip(delete, "Archive the selected material from active inventory without deleting historical usage.");
            grid.Controls.Add(adjust, 0, 0);
            grid.Controls.Add(reorder, 1, 0);
            grid.Controls.Add(open, 0, 1);
            grid.Controls.Add(delete, 1, 1);
            card.Controls.Add(grid);
            return card;
        }

        private void ShowInventoryActionsMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddInventoryAction(menu, "Bulk Update", async (s, e) => await ImportInventoryCsvAsync());
            AddInventoryAction(menu, "Print Material Report", (s, e) => PreviewStockReport());
            AddInventoryAction(menu, "Purchase Valuation", (s, e) => PreviewStockValuation());
            AddInventoryAction(menu, "Find Duplicate Items", (s, e) => ShowDuplicateItems());
            AddInventoryAction(menu, "Merge Duplicate Items", (s, e) => MergeDuplicateItems());
            menu.Items.Add(new ToolStripSeparator());
            AddInventoryAction(menu, "Delete Selected Item", (s, e) => DeleteCurrentItem());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void AddInventoryAction(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            menu.Items.Add(item);
        }

        private void ShowDuplicateItems()
        {
            try
            {
                List<InventoryDuplicateGroup> groups = _svc.FindDuplicateItems();
                if (groups.Count == 0)
                {
                    MessageBox.Show(this, "No duplicate active material items were found.", BrandingService.WindowTitle("Duplicate Items"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetStatus("No duplicate material items found.", SaveGreen);
                    return;
                }

                string summary = string.Join(Environment.NewLine + Environment.NewLine, groups.Take(12).Select(group =>
                    group.DuplicateKey + " (" + group.Count + " rows)" + Environment.NewLine +
                    string.Join(Environment.NewLine, group.Items.Select(item => "  #" + item.ItemID + " - " + item.ItemName + " | Qty " + item.CurrentStock.ToString("0.###") + " | Rate " + item.LastPurchaseRate.ToString("0.##")))));

                if (groups.Count > 12)
                    summary += Environment.NewLine + Environment.NewLine + "...and " + (groups.Count - 12) + " more duplicate group(s).";

                MessageBox.Show(this, summary, BrandingService.WindowTitle("Duplicate Items Found"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("Found " + groups.Count + " duplicate material group(s).", WarnOrange);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Finding duplicate inventory items", ex);
                SetStatus("Duplicate scan could not complete.", DelRed);
            }
        }

        private void MergeDuplicateItems()
        {
            try
            {
                List<InventoryDuplicateGroup> groups = _svc.FindDuplicateItems();
                if (groups.Count == 0)
                {
                    MessageBox.Show(this, "No duplicate active material items were found.", BrandingService.WindowTitle("Duplicate Items"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetStatus("No duplicate material items found.", SaveGreen);
                    return;
                }

                int duplicateRows = groups.Sum(group => Math.Max(0, group.Count - 1));
                DialogResult confirm = MessageBox.Show(
                    this,
                    "ServoERP found " + groups.Count + " duplicate material group(s), with " + duplicateRows + " duplicate row(s)." + Environment.NewLine + Environment.NewLine +
                    "The cleanup will keep the best master item, move linked stock/job/invoice/PO references to it, add duplicate stock quantities, and archive the duplicate rows from active inventory." + Environment.NewLine + Environment.NewLine +
                    "Continue?",
                    BrandingService.WindowTitle("Merge Duplicate Items"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                    return;

                InventoryDuplicateCleanupResult result = _svc.MergeDuplicateItems();
                LoadList();
                string message = "Duplicate cleanup complete. Groups: " + result.GroupsDetected + ", archived: " + result.ItemsArchived + ", references moved: " + result.ReferencesMoved + ".";
                MessageBox.Show(this, message, BrandingService.WindowTitle("Duplicate Items Merged"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus(message, SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Merging duplicate inventory items", ex);
                SetStatus("Duplicate cleanup could not complete.", DelRed);
            }
        }

        private void BuildDetailPanel()
        {
            int y = 10;

            _detail.Controls.Add(MakeSectionLabel("ITEM DETAILS", ref y));
            _detail.Controls.Add(new Label
            {
                Text = "Required: Item Name. Supplier, price, reorder level, and quantity can be added later.",
                Location = new Point(0, y),
                Size = new Size(270, 34),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate600
            });
            y += 42;

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

            _detail.Controls.Add(MakeSectionLabel("PURCHASE PRICING", ref y));

            _detail.Controls.Add(MakeLabel("Current Qty", new Point(0, y + 3)));
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
            _detail.Controls.Add(MakeLabel("Supplier", new Point(0, y + 3)));
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
            InventoryLoadSnapshot snapshot = await Task.Run(() => new InventoryLoadSnapshot
            {
                Items = _svc.GetAll() ?? new List<StockItem>(),
                Vendors = SafeLoadSuppliersForDropdown()
            });
            List<StockItem> items = snapshot.Items;
            List<Vendor> vendors = snapshot.Vendors;
            AppRuntime.LogTiming("Inventory.FetchInitialData", fetch.ElapsedMilliseconds, "items=" + items.Count + ";vendors=" + vendors.Count);

            var bind = Stopwatch.StartNew();
            PopulateVendorDropdown(vendors);
            BindInventoryList(items, false);
            LoadItemSuggestions(items);
            AppRuntime.LogTiming("Inventory.BindInitialData", bind.ElapsedMilliseconds, "items=" + items.Count);
            AppRuntime.LogTiming("Inventory.InitialLoad", sw.ElapsedMilliseconds, "items=" + items.Count + ";vendors=" + vendors.Count);
        }

        /// <summary>Loads suppliers for the preferred-supplier dropdown without blocking the material list.</summary>
        private List<Vendor> SafeLoadSuppliersForDropdown()
        {
            try
            {
                return _vndSvc.GetSuppliers() ?? new List<Vendor>();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.SafeLoadSuppliersForDropdown", ex);
                return new List<Vendor>();
            }
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
            _lblStockValue.Text = "Purchase Value: Rs " + val.ToString("N2");
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
            string suffix = forceWarn ? "items to order" : "items";
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
            if (mode == "To Order")
                query = query.Where(i => i.IsLowStock);
            else if (mode == "Supplier Linked")
                query = query.Where(i => !string.IsNullOrWhiteSpace(i.VendorName));
            else if (mode == "Needs Supplier")
                query = query.Where(i => string.IsNullOrWhiteSpace(i.VendorName));

            _listSource = query.ToList();
            RenderItemBatch(reset: true, forceWarn: mode == "To Order");
            SetStatus($"Showing {Math.Min(_renderedCount, _listSource.Count)} of {_listSource.Count} items.", Color.Gray);
        }

        private void UpdateInventoryMetrics(List<StockItem> items)
        {
            items = items ?? new List<StockItem>();
            int vendorLinked = items.Count(i => !string.IsNullOrWhiteSpace(i.VendorName));
            int toOrder = items.Count(i => i.IsLowStock);
            int needsVendor = items.Count(i => string.IsNullOrWhiteSpace(i.VendorName));
            int pricedItems = items.Count(i => i.LastPurchaseRate > 0);
            if (_lblTotalItems != null) _lblTotalItems.Text = items.Count.ToString("N0");
            if (_lblInStockItems != null) _lblInStockItems.Text = vendorLinked.ToString("N0");
            if (_lblLowStockItems != null) _lblLowStockItems.Text = toOrder.ToString("N0");
            if (_lblOutStockItems != null) _lblOutStockItems.Text = needsVendor.ToString("N0");
            if (_lblTotalStockValue != null) _lblTotalStockValue.Text = pricedItems.ToString("N0");
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
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Loading inventory summary", ex);
                SetStatus("Inventory summary could not load. Refresh and try again.", Color.Red);
            }
            AppRuntime.LogTiming("Inventory.LoadList", sw.ElapsedMilliseconds, "items=" + (_listSource?.Count ?? 0));
        }

        private async void LoadLowStock()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                SetStatus("Loading items to order...", WarnOrange);
                var items = await Task.Run(() => _svc.GetLowStock());
                BindInventoryList(items, true);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Loading procurement data", ex);
                SetStatus("Procurement data could not load. Refresh and try again.", Color.Red);
            }
            AppRuntime.LogTiming("Inventory.LoadLowStock", sw.ElapsedMilliseconds, "items=" + (_listSource?.Count ?? 0));
        }

        private void RenderItemBatch(bool reset, bool forceWarn)
        {
            _inventoryForceWarn = forceWarn;
            if (reset)
                _inventoryPage = 1;

            int total = _listSource?.Count ?? 0;
            int pageSize = Math.Max(1, _inventoryPageSize);
            _inventoryPage = PaginationState.NormalizePage(_inventoryPage, total, pageSize);
            List<StockItem> visibleItems = (_listSource ?? new List<StockItem>())
                .Skip((_inventoryPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            UiPerformanceService.WithSuspendedDrawing(_itemFlow, () =>
            {
                _itemFlow.Controls.Clear();
                if (total == 0)
                {
                    _itemFlow.Controls.Add(BuildInventoryEmptyState());
                }
                foreach (StockItem item in visibleItems)
                    _itemFlow.Controls.Add(MakeItemCard(item, forceWarn));

                _renderedCount = visibleItems.Count == 0 ? 0 : Math.Min(total, ((_inventoryPage - 1) * pageSize) + visibleItems.Count);
                if (_inventoryPager != null)
                    _inventoryPager.SetState(_inventoryPage, total, pageSize);
            });
        }

        private Panel BuildInventoryEmptyState()
        {
            Panel panel = new Panel
            {
                Width = Math.Max(760, _itemFlow?.ClientSize.Width > 20 ? _itemFlow.ClientSize.Width - 18 : 880),
                Height = 420,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Inventory, 72, Color.FromArgb(238, 242, 255), InfoBlue);
            icon.Location = new Point((panel.Width - icon.Width) / 2, 130);
            panel.Controls.Add(icon);
            panel.Controls.Add(new Label { Text = "No items found", Location = new Point(0, 218), Size = new Size(panel.Width, 28), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter });
            panel.Controls.Add(new Label { Text = "Add your first material item. Supplier, rate, and reorder settings can be completed later.", Location = new Point(0, 248), Size = new Size(panel.Width, 24), Font = DS.Body, ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleCenter });
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
            btn.FlatAppearance.BorderColor = DS.Border;
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
            bool needsVendor = string.IsNullOrWhiteSpace(item.VendorName);
            int rowWidth = Math.Max(760, _itemFlow?.ClientSize.Width > 20 ? _itemFlow.ClientSize.Width - 18 : 880);

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
            Label stock = new Label { Text = item.CurrentStock.ToString("N1"), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = warn ? WarnOrange : DS.Slate900, Location = new Point(540, 20), Width = 120 };
            Label value = new Label { Text = item.StockValue.ToString("N2"), Font = new Font("Segoe UI", 9), ForeColor = DS.Slate900, Location = new Point(690, 20), Width = 120 };
            Label badge = new Label
            {
                Text = warn ? "To Order" : needsVendor ? "Needs Supplier" : "Supplier Linked",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = warn ? WarnOrange : needsVendor ? DelRed : SaveGreen,
                BackColor = warn ? DS.Amber50 : needsVendor ? DS.Red50 : DS.Green50,
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
            _lblStockValue.Text = "Purchase Value: Rs " + item.StockValue.ToString("N2");
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

        private void DeleteCurrentItem()
        {
            if (_current == null || _current.ItemID <= 0)
            {
                SetStatus("Select a saved material item to delete.", WarnOrange);
                return;
            }

            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Inventory Item",
                _current.ItemName,
                "The item will be removed from active inventory lists. Historical purchases, jobs, invoices, and stock movements remain preserved.");
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                int deletedId = _current.ItemID;
                _svc.Delete(deletedId);
                _allItems.RemoveAll(i => i.ItemID == deletedId);
                _listSource.RemoveAll(i => i.ItemID == deletedId);
                NewRecord();
                ApplyInventoryFilter();
                LoadItemSuggestions(_allItems);
                SetStatus("Material item deleted from active inventory.", SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Deleting inventory item", ex);
                SetStatus("Inventory item could not be deleted. Refresh and try again.", DelRed);
            }
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

                int currentItemId = _current?.ItemID ?? 0;
                SetStatus("Saving material item...", Color.Gray);
                var worker = CreateWorker();
                worker.DoWork += (s, args) =>
                {
                    if (currentItemId <= 0)
                        _svc.Create(item);
                    else
                    {
                        item.ItemID = currentItemId;
                        _svc.Update(item);
                    }

                    args.Result = _svc.GetAll() ?? new List<StockItem>();
                };
                worker.RunWorkerCompleted += (s, args) =>
                {
                    if (args.Error != null)
                    {
                        ShowError( "Inventory item could not be saved. Check the form and try again.", args.Error);
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Saving inventory item", args.Error);
                        RunOnUI(() => SetStatus("Inventory item could not be saved. Check the form and try again.", Color.Red));
                        return;
                    }
                    if (args.Cancelled) return;

                    RunOnUI(() =>
                    {
                        List<StockItem> items = args.Result as List<StockItem> ?? new List<StockItem>();
                        BindInventoryList(items, false);
                        LoadItemSuggestions(items);
                        SetStatus("Material item saved. Next: update quantity, supplier, or create a purchase request when required.", SaveGreen);
                    });
                };
                worker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Saving inventory item", ex);
                SetStatus("Inventory item could not be saved. Check the form and try again.", Color.Red);
            }
        }

        private void CreatePO()
        {
            if (_current == null)
            {
                MessageBox.Show(
                    "Please select a material to request first.",
                    "Purchase Request",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var dlg = new Form
            {
                Text            = "Create Purchase Request - " + _current.ItemName,
                Width           = 380,
                Height          = 280,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            int dy = 16;

            // Supplier
            dlg.Controls.Add(new Label
            {
                Text = "Supplier:", Location = new Point(12, dy + 3),
                Width = 110, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9)
            });
            var cboVendor = new ComboBox
            {
                Location = new Point(128, dy), Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9)
            };
            var vendors = _vndSvc.GetSuppliers();
            foreach (var v in vendors) cboVendor.Items.Add(v);
            if (cboVendor.Items.Count > 0) cboVendor.SelectedIndex = 0;

            // Pre-select preferred supplier if set
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
                Text = "Request Qty:", Location = new Point(12, dy + 3),
                Width = 110, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9)
            });
            var numQty = new NumericUpDown
            {
                Location = new Point(128, dy), Width = 120,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2,
                Minimum = 1, Maximum = 99999,
                Value = Math.Max(1, _current.ReorderLevel > _current.CurrentStock ? _current.ReorderLevel - _current.CurrentStock : Math.Max(1, _current.ReorderLevel))
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

            var btnOK = new Button
            {
                Text = "Create Request", DialogResult = DialogResult.OK,
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
                MessageBox.Show("Please select a supplier.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var selectedVendor = (Vendor)cboVendor.SelectedItem;
                decimal qty  = numQty.Value;
                decimal rate = numRate.Value;
                decimal total = qty * rate;
                string poNumber = "PR-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

                var po = new PurchaseOrder
                {
                    VendorID    = selectedVendor.VendorID,
                    VendorName  = selectedVendor.VendorName,
                    VendorGSTIN = selectedVendor.GSTNumber,
                    PONumber    = poNumber,
                    PODate      = DateTime.Today,
                    TotalAmount = total,
                    Status      = "Draft",
                    Notes       = "Purchase request created from Materials / Procurement."
                };
                po.LineItems.Add(new PurchaseLineItem
                {
                    InventoryItemId = _current.ItemID,
                    Description = _current.ItemName,
                    UOM         = DisplayUnit(_current.Unit),
                    Quantity    = qty,
                    Rate        = rate,
                    Amount      = total
                });

                _poSvc.Create(po);

                MessageBox.Show(
                    $"Purchase request created: {poNumber}",
                    "Purchase Request Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                SetStatus("Purchase request created: " + poNumber, SaveGreen);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Creating purchase request", ex);
                SetStatus("Purchase request could not be created. Review the item and try again.", Color.Red);
            }
        }

        private void FocusStockAdjustment()
        {
            if (_current == null)
            {
                SetStatus("Select an item to update quantity.", WarnOrange);
                _txtSearch.Focus();
                return;
            }

            _numStock.Focus();
            _numStock.Select(0, _numStock.Text.Length);
            SetStatus("Update the current quantity and save the item.", InfoBlue);
        }

        private void ShowReorderSuggestions()
        {
            if (_current != null && _current.IsLowStock)
            {
                CreatePO();
                return;
            }

            LoadLowStock();
            SetStatus("Supplier ordering plan loaded. Select a material to create a purchase request.", WarnOrange);
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
                Label stock = new Label { Text = "Available quantity: " + _current.CurrentStock.ToString("N2") + " " + DisplayUnit(_current.Unit), Location = new Point(18, 42), Size = new Size(420, 20), Font = DS.Body, ForeColor = DS.Slate600 };
                NumericUpDown qty = new NumericUpDown { Location = new Point(150, 82), Width = 150, DecimalPlaces = 2, Minimum = 0.01m, Maximum = Math.Max(0.01m, _current.CurrentStock), Value = Math.Min(Math.Max(0.01m, _current.CurrentStock), 1m), Font = DS.Body };
                ComboBox from = new ComboBox { Location = new Point(150, 120), Width = 270, DropDownStyle = ComboBoxStyle.DropDown, Font = DS.Body };
                ComboBox to = new ComboBox { Location = new Point(150, 158), Width = 270, DropDownStyle = ComboBoxStyle.DropDown, Font = DS.Body };
                TextBox reference = new TextBox { Location = new Point(150, 196), Width = 270, Font = DS.Body };
                TextBox notes = new TextBox { Location = new Point(150, 234), Width = 270, Height = 44, Multiline = true, Font = DS.Body };

                from.Items.AddRange(new object[] { "Main Store", "Service Van", "Site Store", "Supplier Return", "Damaged Hold" });
                to.Items.AddRange(new object[] { "Main Store", "Service Van", "Site Store", "Supplier Return", "Damaged Hold" });
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
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Saving stock transfer", ex);
                    MessageBox.Show(dialog, "Stock transfer could not be saved. Review the quantities and try again.", "Stock Transfer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            sb.AppendLine("ItemName,Category,Unit,CurrentQty,ReservedQty,AvailableQty,LastPurchaseRate,ReorderLevel,PurchaseValue,VendorName,Status,LastUpdated");
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
                            Csv(InventoryProcurementStatus(item)),
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
                SetStatus("Inventory import could not complete. Review the file and try again.", Color.Red);
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
            string html = BuildInventoryHtml("Material Procurement Report", rows);
            new HtmlPreviewDialog("Material Procurement Report", html).ShowDialog(this);
        }

        private void PreviewStockValuation()
        {
            List<StockItem> rows = ((_allItems != null && _allItems.Count > 0 ? _allItems : _listSource) ?? new List<StockItem>())
                .OrderByDescending(i => i.StockValue)
                .ToList();
            string html = BuildInventoryHtml("Purchase Valuation", rows);
            new HtmlPreviewDialog("Purchase Valuation", html).ShowDialog(this);
        }

        private string BuildInventoryHtml(string title, List<StockItem> rows)
        {
            rows = rows ?? new List<StockItem>();
            decimal value = rows.Sum(i => i.StockValue);
            int toOrder = rows.Count(i => i.IsLowStock);
            int needsVendor = rows.Count(i => string.IsNullOrWhiteSpace(i.VendorName));
            var sb = new StringBuilder();
            sb.Append("<html><head><style>");
            sb.Append("body{font-family:Segoe UI,Arial,sans-serif;color:#0f172a;margin:28px}h1{font-size:24px;margin:0 0 6px}.meta{color:#64748b;margin-bottom:18px}.cards{display:flex;gap:12px;margin-bottom:18px}.card{border:1px solid #e2e8f0;border-radius:10px;padding:12px 16px;min-width:150px}.label{color:#64748b;font-size:12px}.value{font-size:20px;font-weight:700}table{border-collapse:collapse;width:100%;font-size:12px}th{background:#f1f5f9;text-align:left}th,td{border:1px solid #e2e8f0;padding:8px}.right{text-align:right}.low{color:#d97706;font-weight:700}.out{color:#dc2626;font-weight:700}.ok{color:#16a34a;font-weight:700}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>").Append(Html(title)).Append("</h1>");
            sb.Append("<div class='meta'>Generated ").Append(DateTime.Now.ToString("dd MMM yyyy HH:mm")).Append("</div>");
            sb.Append("<div class='cards'>");
            sb.Append(KpiHtml("Items", rows.Count.ToString("N0")));
            sb.Append(KpiHtml("To order", toOrder.ToString("N0")));
            sb.Append(KpiHtml("Needs supplier", needsVendor.ToString("N0")));
            sb.Append(KpiHtml("Purchase value", IndiaFormatHelper.FormatCurrency(value)));
            sb.Append("</div><table><tr><th>Item</th><th>Category</th><th>Unit</th><th class='right'>Current Qty</th><th class='right'>Rate</th><th class='right'>Value</th><th>Status</th><th>Supplier</th></tr>");
            foreach (StockItem item in rows)
            {
                string status = InventoryProcurementStatus(item);
                string cls = item.IsLowStock ? "low" : string.IsNullOrWhiteSpace(item.VendorName) ? "out" : "ok";
                sb.Append("<tr><td>").Append(Html(item.ItemName)).Append("</td><td>").Append(Html(item.Category)).Append("</td><td>").Append(Html(DisplayUnit(item.Unit))).Append("</td><td class='right'>").Append(item.CurrentStock.ToString("N2")).Append("</td><td class='right'>").Append(item.LastPurchaseRate.ToString("N2")).Append("</td><td class='right'>").Append(item.StockValue.ToString("N2")).Append("</td><td class='").Append(cls).Append("'>").Append(status).Append("</td><td>").Append(Html(item.VendorName)).Append("</td></tr>");
            }
            sb.Append("</table></body></html>");
            return sb.ToString();
        }

        private static string KpiHtml(string label, string value)
        {
            return "<div class='card'><div class='label'>" + Html(label) + "</div><div class='value'>" + Html(value) + "</div></div>";
        }

        private static string InventoryProcurementStatus(StockItem item)
        {
            if (item == null)
                return "Catalog";
            if (item.IsLowStock)
                return "To Order";
            return string.IsNullOrWhiteSpace(item.VendorName) ? "Needs Supplier" : "Supplier Linked";
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

        private sealed class InventoryLoadSnapshot
        {
            public List<StockItem> Items { get; set; }
            public List<Vendor> Vendors { get; set; }
        }
    }
}



