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
using ServoERP.Validators;

namespace HVAC_Pro_Desktop.UI
{
    public class InventoryForm : DeferredPageControl
    {
        private readonly InventoryService _svc     = new InventoryService();
        private readonly VendorService    _vndSvc  = new VendorService();
        private readonly PurchaseService  _poSvc   = new PurchaseService();
        private readonly UnitMeasurementService _unitSvc = new UnitMeasurementService();
        private readonly MasterLookupService _lookupSvc = new MasterLookupService();
        private readonly StockItemValidator _stockItemValidator = new StockItemValidator();
        private readonly ToolTip _toolTip = new ToolTip();

        private InventoryListModule _itemListModule;
        private Panel    _detail;
        private Panel    _selectedCard;

        private ComboBox      _cboName, _cboCategory, _cboUnit;
        private NumericUpDown _numStock, _numRate, _numReorder;
        private ComboBox      _cboVendor;
        private DataGridView  _gridSupplierPrices;
        private Button        _btnAddSupplierPrice;
        private Button        _btnRemoveSupplierPrice;
        private Button        _btnUsePreferredSupplier;
        private Button        _btnManageSupplierPrices;
        private Label         _lblStatus, _lblStockValue;
        private Label         _lblTotalItems, _lblInStockItems, _lblLowStockItems, _lblOutStockItems, _lblTotalStockValue;
        private TextBox       _txtSearch;
        private ComboBox      _cboListMode;
        private ComboBox      _cboCategoryFilter;
        private ComboBox      _cboSupplierFilter;
        private ComboBox      _cboStockStatusFilter;
        private ComboBox      _cboActivityFilter;
        private Button        _btnReorder;
        private Button        _btnFilterAll;
        private Button        _btnFilterToOrder;
        private Button        _btnFilterSupplierLinked;
        private Button        _btnFilterNeedsSupplier;
        private Button        _btnClearFilters;
        private Button        _btnCompareSuppliers;
        private Label         _lblSupplierSnapshotEyebrow;
        private Label         _lblSupplierSnapshotItem;
        private Label         _lblSupplierSnapshotSummary;
        private Label         _lblSupplierSnapshotDetail;
        private Label         _lblSupplierSnapshotRecommendation;
        private bool _inventorySearchPlaceholderActive = false;
        private List<StockItem> _listSource = new List<StockItem>();
        private List<StockItem> _allItems = new List<StockItem>();
        private bool _inventoryForceWarn;
        private int _inventoryPage = 1;
        private int _inventoryPageSize = 25;
        private GlobalPaginationControl _inventoryPager;
        private bool _initialInventoryLoadInProgress;
        private bool _isApplyingItemDefaults;
        private int _itemDefaultsRequestVersion;
        private bool _supplierPriceGridSyncInProgress;
        private bool _suppressInventoryItemDialog;
        private List<Vendor> _detailVendorChoices = new List<Vendor>();
        private List<StockItem> _detailSuggestionItems = new List<StockItem>();

        private StockItem _current;
        private readonly Dictionary<string, StockItem> _itemLookupByName = new Dictionary<string, StockItem>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SupplierOption> _bestSupplierByItemKey = new Dictionary<string, SupplierOption>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SupplierItemPrice> _currentSupplierPrices = new List<SupplierItemPrice>();

        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color DelRed = DS.Red600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color WarnOrange = DS.Amber500;
        private const int DetailLabelX = 8;
        private const int DetailLabelWidth = 128;
        private const int DetailInputX = 150;
        private const int DetailInputWidth = 220;
        private const int DetailSectionWidth = 370;

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public InventoryForm()
            : this(false)
        {
        }

        private InventoryForm(bool suppressInitialLoad)
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            if (!suppressInitialLoad)
            {
                EnableDeferredLoad(
                    (Func<Task>)(async () => await LoadInitialDataAsync()),
                    ex =>
                    {
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Inventory"), "Loading inventory", ex);
                        SetStatus("Inventory load error. Click refresh to try again.", DelRed);
                    });
            }
        }

        private async void QueueInitialInventoryLoad()
        {
            if (_initialInventoryLoadInProgress)
                return;

            _initialInventoryLoadInProgress = true;
            SetStatus("Loading inventory...", Color.Gray);
            try
            {
                bool succeeded = await RunSafeAsync("Loading inventory", async () =>
                {
                    Stopwatch fetch = Stopwatch.StartNew();
                    InventoryLoadSnapshot snapshot = await Task.Run(() => new InventoryLoadSnapshot
                    {
                        Items = _svc.GetAll() ?? new List<StockItem>(),
                        Vendors = SafeLoadSuppliersForDropdown()
                    });
                    AppRuntime.LogTiming("Inventory.FetchInitialData", fetch.ElapsedMilliseconds);
                    if (IsDisposed)
                        return;

                    Stopwatch bind = Stopwatch.StartNew();
                    List<StockItem> items = snapshot.Items ?? new List<StockItem>();
                    List<Vendor> vendors = snapshot.Vendors ?? new List<Vendor>();
                    PopulateVendorDropdown(vendors);
                    BindInventoryList(items, false);
                    LoadItemSuggestions(items);
                    AppRuntime.LogTiming("Inventory.BindInitialData", bind.ElapsedMilliseconds, "items=" + items.Count);
                    AppRuntime.LogTiming("Inventory.InitialLoad", bind.ElapsedMilliseconds, "items=" + items.Count + ";vendors=" + vendors.Count);
                    MarkDeferredLoadCompleted();
                });
                if (!succeeded && !IsDisposed)
                {
                    SetStatus("Inventory load error. Click refresh to try again.", DelRed);
                    MarkDeferredLoadCompleted();
                }
            }
            finally
            {
                _initialInventoryLoadInProgress = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        private void BuildLayout()
        {
            Stopwatch layoutWatch = Stopwatch.StartNew();
            Stopwatch phaseWatch = Stopwatch.StartNew();
            Controls.Clear();
            BackColor = DS.BgPage;

            Button btnHeaderRefresh = MakeBtn("Refresh", Color.White, 96); btnHeaderRefresh.ForeColor = InfoBlue; btnHeaderRefresh.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnExport = MakeBtn("Export CSV", Color.White, 104); btnExport.ForeColor = DS.Slate700; btnExport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnImport = MakeBtn("Import CSV", Color.White, 104); btnImport.ForeColor = DS.Slate700; btnImport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnSupplierPrices = MakeBtn("Supplier Prices", Color.White, 132); btnSupplierPrices.ForeColor = InfoBlue; btnSupplierPrices.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnForms = MakeBtn("Service Forms", Color.White, 108); btnForms.ForeColor = InfoBlue; btnForms.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            Button btnNew = MakeBtn("+ Add New Material", InfoBlue, 154);
            ModernIconSystem.AddButtonIcon(btnHeaderRefresh, ModernIconKind.Refresh);
            btnHeaderRefresh.Click += (s, e) => LoadList();
            btnNew.Click += (s, e) => ShowInventoryItemDetailsDialog(null, true);
            btnImport.Click += async (s, e) => await ImportInventoryCsvAsync();
            btnSupplierPrices.Click += (s, e) => ShowSupplierPriceImportMenu(btnSupplierPrices);
            btnExport.Click += (s, e) => ExportInventoryCsv();
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Materials / Procurement", "Purchases", null, "spare parts requisition purchase order supplier quote goods received note material usage");
            Panel header = SharedPageHeader.Build(new SharedPageHeaderModel
            {
                Name = "InventoryPageHeader",
                Mode = SharedPageHeaderMode.Dashboard,
                Dock = DockStyle.Top,
                BackColor = DS.BgPage,
                Title = "Materials / Job Procurement",
                Subtitle = "Manage the material catalog, preferred suppliers, buying rates, and job-by-job procurement readiness.",
                TitleWidth = 420,
                SubtitleWidth = 620,
                RightActions = new List<Control> { btnHeaderRefresh, btnExport, btnImport, btnSupplierPrices, btnForms, btnNew }
            }).Header;
            AppRuntime.LogTiming("Inventory.BuildLayout.Header", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 112, BackColor = DS.BgPage, Padding = new Padding(24, 8, 24, 14), ColumnCount = 5, RowCount = 1 };
            for (int i = 0; i < 5; i++) kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            kpis.Controls.Add(CreateInventoryMetric("Total Items", "0", ModernIconKind.Inventory, InfoBlue, out _lblTotalItems), 0, 0);
            kpis.Controls.Add(CreateInventoryMetric("Supplier Ready", "0", ModernIconKind.Checklist, SaveGreen, out _lblInStockItems), 1, 0);
            kpis.Controls.Add(CreateInventoryMetric("Procurement Required", "0", ModernIconKind.Alert, WarnOrange, out _lblLowStockItems), 2, 0);
            kpis.Controls.Add(CreateInventoryMetric("Needs Supplier", "0", ModernIconKind.Alert, DelRed, out _lblOutStockItems), 3, 0);
            kpis.Controls.Add(CreateInventoryMetric("Priced Items", "0", ModernIconKind.Payment, InfoBlue, out _lblTotalStockValue), 4, 0);
            AppRuntime.LogTiming("Inventory.BuildLayout.Kpis", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            Panel modeGuide = BuildInventoryModeGuide();
            AppRuntime.LogTiming("Inventory.BuildLayout.ModeGuide", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(24, 0, 24, 16) };

            Panel mainCard = CreateModernCard(null);
            mainCard.Dock = DockStyle.Fill;
            mainCard.Padding = new Padding(16);

            Panel filters = new Panel { Dock = DockStyle.Top, Height = 126, BackColor = Color.White, Padding = new Padding(0, 4, 0, 0) };
            TableLayoutPanel filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            FlowLayoutPanel chips = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.White,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _btnFilterAll = MakeFilterChip("All Items", true);
            _btnFilterToOrder = MakeFilterChip("Procurement Required", false);
            _btnFilterSupplierLinked = MakeFilterChip("Supplier Ready", false);
            _btnFilterNeedsSupplier = MakeFilterChip("Needs Supplier", false);
            _btnFilterAll.Click += (s, e) => { _cboListMode.SelectedItem = "All"; };
            _btnFilterToOrder.Click += (s, e) => { _cboListMode.SelectedItem = "Procurement Required"; };
            _btnFilterSupplierLinked.Click += (s, e) => { _cboListMode.SelectedItem = "Supplier Ready"; };
            _btnFilterNeedsSupplier.Click += (s, e) => { _cboListMode.SelectedItem = "Needs Supplier"; };
            chips.Controls.AddRange(new Control[] { _btnFilterAll, _btnFilterToOrder, _btnFilterSupplierLinked, _btnFilterNeedsSupplier });

            TableLayoutPanel toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 0, 2)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252f));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cboListMode = new ComboBox
            {
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Standard,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 4, 12, 4),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            _cboListMode.Items.AddRange(new object[] { "All", "Procurement Required", "Supplier Ready", "Needs Supplier" });
            _cboListMode.SelectedIndex = 0;
            _cboListMode.SelectedIndexChanged += (s, e) =>
            {
                UpdateInventoryFilterVisualState();
                ApplyInventoryFilter();
            };

            _cboCategoryFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Standard,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 4, 12, 4),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            _cboCategoryFilter.Items.Add("All Categories");
            _cboCategoryFilter.SelectedIndex = 0;
            _cboCategoryFilter.SelectedIndexChanged += (s, e) => ApplyInventoryFilter();

            _txtSearch = new TextBox { Dock = DockStyle.Fill, Height = 30, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Text = string.Empty, ForeColor = DS.Slate900, Margin = new Padding(0, 4, 12, 4) };
            _txtSearch.TextChanged += (s, e) => ApplyInventoryFilter();
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    ResetInventorySearchPlaceholder();
                    ApplyInventoryFilter();
                    e.Handled = true;
                }
            };
            _txtSearch.GotFocus += (s, e) =>
            {
                if (_inventorySearchPlaceholderActive)
                {
                    _inventorySearchPlaceholderActive = false;
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = DS.Slate900;
                }
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _inventorySearchPlaceholderActive = false;
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = DS.Slate900;
                }
            };
            _btnClearFilters = MakeBtn("Clear Filters", Color.White, 116); _btnClearFilters.ForeColor = InfoBlue; _btnClearFilters.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnClearFilters.Dock = DockStyle.Fill;
            _btnClearFilters.Margin = new Padding(0, 4, 0, 4);
            _btnClearFilters.Click += (s, e) => ResetInventoryFilters();
            Button btnRefresh = MakeBtn("Refresh", Color.White, 118); btnRefresh.ForeColor = InfoBlue; btnRefresh.FlatAppearance.BorderColor = DS.BorderStrong;
            btnRefresh.Dock = DockStyle.Fill;
            btnRefresh.Margin = new Padding(0, 4, 0, 4);
            btnRefresh.Click += (s, e) => LoadList();
            toolbar.Controls.Add(_cboListMode, 0, 0);
            toolbar.Controls.Add(_cboCategoryFilter, 1, 0);
            toolbar.Controls.Add(_txtSearch, 2, 0);
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.White
            };
            _btnClearFilters.Dock = DockStyle.None;
            btnRefresh.Dock = DockStyle.None;
            _btnClearFilters.Width = 116;
            btnRefresh.Width = 118;
            actions.Controls.Add(btnRefresh);
            actions.Controls.Add(_btnClearFilters);
            toolbar.Controls.Add(actions, 3, 0);

            TableLayoutPanel secondaryFilters = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 0, 2)
            };
            secondaryFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            secondaryFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            secondaryFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            secondaryFilters.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cboSupplierFilter = CreateInventoryFilterCombo("All Suppliers", new[] { "All Suppliers" });
            _cboStockStatusFilter = CreateInventoryFilterCombo("All Material Modes", new[] { "All Material Modes", "Buffer Available", "Direct Purchase", "Reserved For Jobs", "Needs Supplier" });
            _cboActivityFilter = CreateInventoryFilterCombo("All Activity", new[] { "All Activity", "High Value", "Recently Updated", "Dormant 90+ Days", "Unpriced" });
            _cboSupplierFilter.SelectedIndexChanged += (s, e) => ApplyInventoryFilter();
            _cboStockStatusFilter.SelectedIndexChanged += (s, e) => ApplyInventoryFilter();
            _cboActivityFilter.SelectedIndexChanged += (s, e) => ApplyInventoryFilter();
            secondaryFilters.Controls.Add(_cboSupplierFilter, 0, 0);
            secondaryFilters.Controls.Add(_cboStockStatusFilter, 1, 0);
            secondaryFilters.Controls.Add(_cboActivityFilter, 2, 0);
            _lblStatus = new Label { Dock = DockStyle.Fill, Height = 24, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate500, Text = "Loading inventory...", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 0, 0, 2) };
            filterLayout.Controls.Add(chips, 0, 0);
            filterLayout.Controls.Add(toolbar, 0, 1);
            filterLayout.Controls.Add(secondaryFilters, 0, 2);
            filterLayout.Controls.Add(_lblStatus, 0, 3);
            filters.Controls.Add(filterLayout);
            AppRuntime.LogTiming("Inventory.BuildLayout.Filters", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            _itemListModule = new InventoryListModule();
            _itemListModule.Dock = DockStyle.Fill;
            _itemListModule.RowSelected += item => SelectItem(item);
            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            listWrap.Controls.Add(_itemListModule);
            mainCard.Controls.Add(listWrap);
            mainCard.Controls.Add(BuildInventoryTableHeader());
            mainCard.Controls.Add(filters);
            AppRuntime.LogTiming("Inventory.BuildLayout.MainCard", phaseWatch.ElapsedMilliseconds);
            phaseWatch.Restart();

            body.Controls.Add(mainCard);

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

            grid.Controls.Add(BuildInventoryModeChip("Catalog", "Search, filter, and review procurement readiness.", InfoBlue), 0, 0);
            grid.Controls.Add(BuildInventoryModeChip("Add / Edit Material", "Open material details from the button or a selected row.", SaveGreen), 1, 0);
            grid.Controls.Add(BuildInventoryModeChip("Job Planning", "Capture buying rates, planning quantity, and field availability.", WarnOrange), 2, 0);
            grid.Controls.Add(BuildInventoryModeChip("Supplier Request", "Create a purchase request when a job needs material.", DelRed), 3, 0);
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
                Height = 44,
                BackColor = Color.White,
                Padding = new Padding(16, 6, 16, 6)
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

            Action layoutActions = () =>
            {
                flow.Location = new Point(Math.Max(16, bar.ClientSize.Width - flow.Width - 16), Math.Max(5, (bar.ClientSize.Height - flow.Height) / 2));
            };
            bar.Resize += (s, e) => layoutActions();
            layoutActions();

            bar.Controls.Add(flow);
            return bar;
        }

        private void ShowInventoryItemDetailsDialog(StockItem item, bool createNew)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = createNew ? "Add New Material" : "Item Details";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(590, 820);
                dialog.MinimumSize = new Size(520, 620);
                dialog.BackColor = DS.BgPage;
                dialog.Padding = new Padding(18);
                dialog.FormClosed += (s, e) => ResetInventoryDetailEditorRefs();

                Panel shell = CreateModernCard(createNew ? "ADD NEW MATERIAL" : "ITEM DETAILS");
                shell.Dock = DockStyle.Fill;
                shell.Padding = new Padding(18, 44, 18, 14);
                shell.Tag = "NO_INPUT_HOST NO_INPUT_OUTLINE_HOST";

                _detail = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Tag = "NO_INPUT_HOST NO_INPUT_OUTLINE_HOST" };
                _detail.HorizontalScroll.Enabled = false;
                _detail.HorizontalScroll.Visible = false;
                BuildDetailPanel();
                PopulateVendorDropdown(_detailVendorChoices);
                LoadItemSuggestions(_detailSuggestionItems);

                Button saveItem = MakeBtn("Save Item", SaveGreen, 104);
                Button clearItem = MakeBtn("New Material", Color.White, 116);
                Button createPo = MakeBtn("Purchase Request", InfoBlue, 140);
                clearItem.ForeColor = DS.Slate700;
                clearItem.FlatAppearance.BorderColor = DS.BorderStrong;
                saveItem.Click += (s, e) => Save();
                clearItem.Click += (s, e) => NewRecord();
                createPo.Click += (s, e) => CreatePO();

                shell.Controls.Add(_detail);
                shell.Controls.Add(BuildDetailActionBar(saveItem, clearItem, createPo));
                dialog.Controls.Add(shell);

                if (createNew)
                    NewRecord();
                else if (item != null)
                {
                    _current = item;
                    PopulateDetail(item);
                }

                dialog.ShowDialog(this);
            }
        }

        private void ResetInventoryDetailEditorRefs()
        {
            _detail = null;
            _cboName = null;
            _cboCategory = null;
            _cboUnit = null;
            _numStock = null;
            _numRate = null;
            _numReorder = null;
            _cboVendor = null;
            _gridSupplierPrices = null;
            _btnAddSupplierPrice = null;
            _btnRemoveSupplierPrice = null;
            _btnUsePreferredSupplier = null;
            _btnManageSupplierPrices = null;
            _lblStockValue = null;
            _lblSupplierSnapshotEyebrow = null;
            _lblSupplierSnapshotItem = null;
            _lblSupplierSnapshotSummary = null;
            _lblSupplierSnapshotDetail = null;
            _lblSupplierSnapshotRecommendation = null;
            _btnCompareSuppliers = null;
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
                Panel headerHost = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    BackColor = Color.White,
                    Padding = new Padding(0, 4, 0, 2)
                };
                headerHost.Controls.Add(new Label
                {
                    Text = title,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = InfoBlue,
                    TextAlign = ContentAlignment.MiddleLeft
                });
                card.Controls.Add(headerHost);
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
            button.ForeColor = selected ? Color.White : (text == "Needs Supplier" ? DelRed : text == "Procurement Required" ? WarnOrange : SaveGreen);
            button.FlatAppearance.BorderColor = selected ? InfoBlue : DS.BorderStrong;
            button.Margin = new Padding(0, 10, 10, 0);
            return button;
        }

        private Panel BuildInventoryTableHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = DS.Slate50, Padding = new Padding(12, 10, 12, 2) };
            string[] cols = { "ITEM DETAILS", "UNIT", "BUFFER QTY", "VALUE (₹)", "STATUS", "ACTIONS" };
            int[] widths = { 420, 100, 150, 150, 130, 120 };
            int x = 8;
            for (int i = 0; i < cols.Length; i++)
            {
                header.Controls.Add(new Label { Text = cols[i], Location = new Point(x, 10), Size = new Size(widths[i], 20), Font = DS.CaptionBold(), ForeColor = DS.Slate700 });
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
            card.Height = 132;
            card.Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE";
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0, 8, 0, 0),
                Margin = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            Button adjust = MakeBtn("Update Quantity", Color.White, 140); adjust.ForeColor = InfoBlue; adjust.FlatAppearance.BorderColor = DS.BorderStrong;
            Button reorder = MakeBtn("Procurement Queue", Color.White, 150); reorder.ForeColor = DS.Primary600; reorder.FlatAppearance.BorderColor = DS.BorderStrong; _btnReorder = reorder;
            Button open = MakeBtn("More Actions", Color.White, 150); open.ForeColor = InfoBlue; open.FlatAppearance.BorderColor = DS.BorderStrong;
            Button delete = MakeBtn("Delete Item", Color.White, 150); delete.ForeColor = DelRed; delete.FlatAppearance.BorderColor = DS.Border;
            foreach (Button button in new[] { adjust, reorder, open, delete })
            {
                button.Dock = DockStyle.Fill;
                button.AutoEllipsis = false;
                button.Margin = new Padding(6, 4, 6, 6);
                button.MinimumSize = new Size(122, 34);
                button.Height = 34;
                button.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
                button.TextAlign = ContentAlignment.MiddleCenter;
            }
            adjust.Click += (s, e) => FocusStockAdjustment();
            reorder.Click += (s, e) => ShowReorderSuggestions();
            open.Click += (s, e) => ShowInventoryActionsMenu(open);
            delete.Click += (s, e) => DeleteCurrentItem();
            _toolTip.SetToolTip(adjust, "Select an item, update the current quantity, then save.");
            _toolTip.SetToolTip(reorder, "Load materials that are likely to need supplier procurement for upcoming jobs.");
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
            AddInventoryAction(menu, "Import Supplier Price Book", (s, e) => ImportUiHelper.RunImport(ExcelImportModule.SupplierItemPrices, FindForm()));
            AddInventoryAction(menu, "Download Supplier Price Template", (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.SupplierItemPrices, FindForm()));
            AddInventoryAction(menu, "Print Material Report", (s, e) => PreviewStockReport());
            AddInventoryAction(menu, "Purchase Valuation", (s, e) => PreviewStockValuation());
            AddInventoryAction(menu, "Find Duplicate Items", (s, e) => ShowDuplicateItems());
            AddInventoryAction(menu, "Merge Duplicate Items", (s, e) => MergeDuplicateItems());
            menu.Items.Add(new ToolStripSeparator());
            AddInventoryAction(menu, "Delete Selected Item", (s, e) => DeleteCurrentItem());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void ShowSupplierPriceImportMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddInventoryAction(menu, "Import Supplier Price Book", (s, e) => ImportUiHelper.RunImport(ExcelImportModule.SupplierItemPrices, FindForm()));
            AddInventoryAction(menu, "Download Supplier Price Template", (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.SupplierItemPrices, FindForm()));
            menu.Show(anchor, new Point(0, anchor.Height + 2));
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
                bool confirm = ServoERP.Infrastructure.ServoConfirmDialog.Show(
                    this,
                    "Merge duplicate material items",
                    "ServoERP found " + groups.Count + " duplicate material group(s), with " + duplicateRows + " duplicate row(s). The cleanup keeps the best master item, moves linked stock/job/invoice/PO references, adds duplicate stock quantities, and archives duplicate active rows.");
                if (!confirm)
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
            int y = 8;

            _detail.Controls.Add(new Label
            {
                Text = "Required: Item Name. Supplier, buying rate, planning quantity, and field quantity can be added later.",
                Location = new Point(DetailLabelX, y),
                Size = new Size(DetailSectionWidth, 34),
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate600
            });
            y += 42;

            _cboName = AddComboField("Item Name *", ref y, ComboBoxStyle.DropDown);
            _cboName.IntegralHeight = false;
            _cboName.MaxDropDownItems = 12;
            _cboName.DropDownHeight = 320;
            _cboName.DropDownWidth = Math.Max(_cboName.Width, 320);
            _cboName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cboName.AutoCompleteSource = AutoCompleteSource.ListItems;
            _cboName.SelectionChangeCommitted += (s, e) => QueueApplyInventoryItemDefaults();
            _cboName.Validated += (s, e) => QueueApplyInventoryItemDefaults();
            _cboName.Leave += (s, e) => QueueApplyInventoryItemDefaults();

            _cboCategory = AddComboField("Category", ref y, ComboBoxStyle.DropDownList);
            _lookupSvc.BindCombo(_cboCategory, "Inventory.Category", new[] { "Filters", "Refrigerant", "Compressors", "Valves", "Belts", "Electrical", "Copper", "Tools", "HVAC Spares", "General" });
            if (_cboCategory.Items.Count > 0) _cboCategory.SelectedIndex = 0;

            _cboUnit = AddComboField("Unit", ref y, ComboBoxStyle.DropDownList);
            _cboUnit.Items.AddRange(_unitSvc.GetDisplayUnits().Cast<object>().ToArray());
            _cboUnit.DropDownWidth = Math.Max(_cboUnit.Width, 280);
            SelectComboByText(_cboUnit, _unitSvc.NormalizeForPickerDisplayOrDefault(UnitMeasurementService.DefaultCode));

            _detail.Controls.Add(MakeSectionLabel("PURCHASE PRICING", ref y));

            _detail.Controls.Add(MakeLabel("Current Qty", new Point(DetailLabelX, y + 3)));
            _numStock = new NumericUpDown
            {
                Location = new Point(DetailInputX, y), Width = DetailInputWidth,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2, Maximum = 99999
            };
            _detail.Controls.Add(_numStock);
            y += 30;

            _detail.Controls.Add(MakeLabel("Last Purchase Rate", new Point(DetailLabelX, y + 3)));
            _numRate = new NumericUpDown
            {
                Location = new Point(DetailInputX, y), Width = DetailInputWidth,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2, Maximum = 999999
            };
            _detail.Controls.Add(_numRate);
            y += 30;

            _detail.Controls.Add(MakeLabel("Typical Purchase Qty", new Point(DetailLabelX, y + 3)));
            _numReorder = new NumericUpDown
            {
                Location = new Point(DetailInputX, y), Width = DetailInputWidth,
                Font = new Font("Segoe UI", 9), DecimalPlaces = 2, Maximum = 99999
            };
            _detail.Controls.Add(_numReorder);
            y += 30;

            _lblStockValue = new Label
            {
                Location = new Point(DetailInputX, y), AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = InfoBlue
            };
            _detail.Controls.Add(_lblStockValue);
            y += 30;

            _numStock.ValueChanged += UpdateStockValue;
            _numRate.ValueChanged  += UpdateStockValue;

            _detail.Controls.Add(MakeSectionLabel("PREFERRED VENDOR", ref y));
            _detail.Controls.Add(MakeLabel("Supplier", new Point(DetailLabelX, y + 3)));
            _cboVendor = new ComboBox
            {
                Location = new Point(DetailInputX, y), Width = DetailInputWidth,
                Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboVendor.Items.Add(new Vendor { VendorID = 0, VendorName = "(None)" });
            _cboVendor.SelectedIndex = 0;
            _detail.Controls.Add(_cboVendor);
            y += 36;

            _detail.Controls.Add(MakeSectionLabel("SUPPLIER PRICE BOOK", ref y));
            Label supplierPriceInfo = new Label
            {
                Text = "Add supplier rates for comparison and mark one preferred vendor.",
                Location = new Point(DetailLabelX, y),
                Size = new Size(220, 20),
                Font = new Font("Segoe UI", 8.4f),
                ForeColor = DS.Slate600
            };
            _detail.Controls.Add(supplierPriceInfo);
            _btnManageSupplierPrices = MakeBtn("Manage Supplier Rates", Color.White, 142);
            _btnManageSupplierPrices.ForeColor = InfoBlue;
            _btnManageSupplierPrices.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnManageSupplierPrices.Location = new Point(DetailSectionWidth - 132, y - 4);
            _btnManageSupplierPrices.Click += (s, e) => OpenSupplierPriceBookDialog();
            _detail.Controls.Add(_btnManageSupplierPrices);
            y += 24;

            Panel supplierPriceGridHost = new Panel
            {
                Location = new Point(DetailLabelX, y),
                Size = new Size(DetailSectionWidth, 136),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Tag = "NO_INPUT_HOST NO_INPUT_OUTLINE_HOST"
            };
            _gridSupplierPrices = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                EditMode = DataGridViewEditMode.EditOnEnter,
                Margin = Padding.Empty
            };
            _gridSupplierPrices.ColumnHeadersHeight = 26;
            _gridSupplierPrices.RowTemplate.Height = 22;
            _gridSupplierPrices.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "SupplierVendorID",
                HeaderText = "Supplier",
                Width = 122,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat,
                DisplayMember = "VendorName",
                ValueMember = "VendorID"
            });
            _gridSupplierPrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierRate", HeaderText = "Rate", Width = 64 });
            _gridSupplierPrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierUnit", HeaderText = "Unit", Width = 52 });
            _gridSupplierPrices.Columns.Add(new DataGridViewCheckBoxColumn { Name = "SupplierPreferred", HeaderText = "Pref", Width = 52 });
            _gridSupplierPrices.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierNotes", HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _gridSupplierPrices.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridSupplierPrices.IsCurrentCellDirty)
                    _gridSupplierPrices.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _gridSupplierPrices.CellValueChanged += SupplierPriceGrid_CellValueChanged;
            _gridSupplierPrices.DataError += (s, e) => e.ThrowException = false;
            supplierPriceGridHost.Controls.Add(_gridSupplierPrices);
            _detail.Controls.Add(supplierPriceGridHost);
            y += 144;

            _btnAddSupplierPrice = MakeBtn("+ Add Supplier", Color.White, 112);
            _btnAddSupplierPrice.ForeColor = InfoBlue;
            _btnAddSupplierPrice.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnAddSupplierPrice.Location = new Point(DetailLabelX, y);
            _btnAddSupplierPrice.Click += (s, e) => AddSupplierPriceRow();
            _detail.Controls.Add(_btnAddSupplierPrice);

            _btnRemoveSupplierPrice = MakeBtn("Remove Row", Color.White, 96);
            _btnRemoveSupplierPrice.ForeColor = DS.Slate700;
            _btnRemoveSupplierPrice.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnRemoveSupplierPrice.Location = new Point(DetailLabelX + 118, y);
            _btnRemoveSupplierPrice.Click += (s, e) => RemoveSelectedSupplierPriceRow();
            _detail.Controls.Add(_btnRemoveSupplierPrice);

            _btnUsePreferredSupplier = MakeBtn("Use Preferred", Color.White, 126);
            _btnUsePreferredSupplier.ForeColor = SaveGreen;
            _btnUsePreferredSupplier.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnUsePreferredSupplier.Location = new Point(DetailLabelX + 220, y);
            _btnUsePreferredSupplier.Click += (s, e) => UpsertPreferredSupplierPriceRowFromForm();
            _detail.Controls.Add(_btnUsePreferredSupplier);
            y += 36;

            Panel snapshot = new Panel
            {
                Location = new Point(DetailLabelX, y),
                Size = new Size(DetailSectionWidth, 158),
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(14, 12, 14, 12)
            };
            snapshot.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.BorderStrong))
                    e.Graphics.DrawRectangle(pen, 0, 0, snapshot.Width - 1, snapshot.Height - 1);
            };
            _lblSupplierSnapshotEyebrow = new Label { Text = "SUPPLIER SNAPSHOT", Location = new Point(14, 12), Size = new Size(200, 18), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = DS.Slate500 };
            _lblSupplierSnapshotItem = new Label { Text = "Select a material to compare offers", Location = new Point(14, 34), Size = new Size(330, 20), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            _lblSupplierSnapshotSummary = new Label { Text = "Best supplier, live offer count, and price guidance appear here.", Location = new Point(14, 56), Size = new Size(330, 32), Font = new Font("Segoe UI", 8.6f), ForeColor = DS.Slate600 };
            _lblSupplierSnapshotDetail = new Label { Text = "Choose a material to see recent supplier history.", Location = new Point(14, 90), Size = new Size(330, 18), Font = new Font("Segoe UI", 8.2f), ForeColor = DS.Slate500, AutoEllipsis = true };
            _lblSupplierSnapshotRecommendation = new Label { Text = string.Empty, Location = new Point(14, 110), Size = new Size(196, 36), Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), ForeColor = InfoBlue };
            _btnCompareSuppliers = MakeBtn("Compare Suppliers", Color.White, 136);
            _btnCompareSuppliers.ForeColor = Color.FromArgb(17, 24, 39);
            _btnCompareSuppliers.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219);
            _btnCompareSuppliers.Location = new Point(218, 112);
            _btnCompareSuppliers.Enabled = false;
            _btnCompareSuppliers.Click += (s, e) => OpenInventorySupplierComparison();
            snapshot.Controls.AddRange(new Control[] { _lblSupplierSnapshotEyebrow, _lblSupplierSnapshotItem, _lblSupplierSnapshotSummary, _lblSupplierSnapshotDetail, _lblSupplierSnapshotRecommendation, _btnCompareSuppliers });
            _detail.Controls.Add(snapshot);
            UpdateInventorySupplierSnapshot(null);
        }

        private void BindSupplierPriceVendorColumn(List<Vendor> vendors)
        {
            if (_gridSupplierPrices == null || _gridSupplierPrices.IsDisposed)
                return;

            var comboColumn = _gridSupplierPrices.Columns["SupplierVendorID"] as DataGridViewComboBoxColumn;
            if (comboColumn == null)
                return;

            List<Vendor> choices = new List<Vendor> { new Vendor { VendorID = 0, VendorName = "(Select supplier)" } };
            choices.AddRange((vendors ?? new List<Vendor>()).Where(v => v != null).OrderBy(v => v.VendorName).ToList());
            comboColumn.DataSource = choices;
            comboColumn.DisplayMember = "VendorName";
            comboColumn.ValueMember = "VendorID";
        }

        private void LoadSupplierPrices(StockItem item)
        {
            List<SupplierItemPrice> prices = new List<SupplierItemPrice>();
            if (item != null && item.ItemID > 0)
            {
                try
                {
                    prices = _svc.GetSupplierPrices(item.ItemID) ?? new List<SupplierItemPrice>();
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("InventoryForm.LoadSupplierPrices", ex);
                }
            }

            if ((prices == null || prices.Count == 0) && item != null && item.VendorID.GetValueOrDefault() > 0)
            {
                prices = new List<SupplierItemPrice>
                {
                    new SupplierItemPrice
                    {
                        ItemID = item.ItemID,
                        VendorID = item.VendorID.Value,
                        VendorName = item.VendorName,
                        ItemName = item.ItemName,
                        Category = item.Category,
                        Unit = DisplayUnit(item.Unit),
                        Rate = item.LastPurchaseRate,
                        IsPreferred = true,
                        Source = "Current item setup"
                    }
                };
            }

            BindSupplierPriceBook(prices);
        }

        private void BindSupplierPriceBook(IEnumerable<SupplierItemPrice> prices)
        {
            if (_gridSupplierPrices == null || _gridSupplierPrices.IsDisposed)
                return;

            _supplierPriceGridSyncInProgress = true;
            try
            {
                _currentSupplierPrices.Clear();
                _currentSupplierPrices.AddRange((prices ?? Enumerable.Empty<SupplierItemPrice>()).Where(p => p != null));
                _gridSupplierPrices.Rows.Clear();
                foreach (SupplierItemPrice price in _currentSupplierPrices)
                    AddSupplierPriceRow(price, false);
            }
            finally
            {
                _supplierPriceGridSyncInProgress = false;
            }

            if (_gridSupplierPrices.Rows.Count == 0)
                AddSupplierPriceRow();

            RefreshPreferredSupplierFromPriceBook(false);
        }

        private void AddSupplierPriceRow(SupplierItemPrice price = null, bool focusNewRow = true)
        {
            if (_gridSupplierPrices == null || _gridSupplierPrices.IsDisposed)
                return;

            int index = _gridSupplierPrices.Rows.Add();
            DataGridViewRow row = _gridSupplierPrices.Rows[index];
            row.Cells["SupplierVendorID"].Value = price?.VendorID > 0 ? (object)price.VendorID : 0;
            row.Cells["SupplierRate"].Value = price == null ? string.Empty : price.Rate.ToString("0.##");
            row.Cells["SupplierUnit"].Value = string.IsNullOrWhiteSpace(price?.Unit) ? DisplayUnit(_cboUnit?.Text) : price.Unit;
            row.Cells["SupplierPreferred"].Value = price != null && price.IsPreferred;
            row.Cells["SupplierNotes"].Value = price?.Notes ?? string.Empty;
            row.Tag = price;

            if (focusNewRow)
            {
                _gridSupplierPrices.ClearSelection();
                row.Selected = true;
                _gridSupplierPrices.CurrentCell = row.Cells["SupplierVendorID"];
            }
        }

        private void RemoveSelectedSupplierPriceRow()
        {
            if (_gridSupplierPrices == null || _gridSupplierPrices.IsDisposed || _gridSupplierPrices.SelectedRows.Count == 0)
            {
                SetStatus("Select a supplier row to remove.", WarnOrange);
                return;
            }

            foreach (DataGridViewRow row in _gridSupplierPrices.SelectedRows)
            {
                if (!row.IsNewRow)
                    _gridSupplierPrices.Rows.Remove(row);
            }

            if (_gridSupplierPrices.Rows.Count == 0)
                AddSupplierPriceRow();

            RefreshPreferredSupplierFromPriceBook(false);
        }

        private void UpsertPreferredSupplierPriceRowFromForm()
        {
            Vendor vendor = _cboVendor.SelectedItem as Vendor;
            if (vendor == null || vendor.VendorID <= 0)
            {
                SetStatus("Choose a preferred supplier first, then add it to the supplier price book.", WarnOrange);
                return;
            }

            foreach (DataGridViewRow row in _gridSupplierPrices.Rows)
            {
                if (TryParseInt(row.Cells["SupplierVendorID"].Value) == vendor.VendorID)
                {
                    row.Cells["SupplierRate"].Value = _numRate.Value.ToString("0.##");
                    row.Cells["SupplierUnit"].Value = DisplayUnit(_cboUnit?.Text);
                    row.Cells["SupplierPreferred"].Value = true;
                    row.Cells["SupplierNotes"].Value = string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["SupplierNotes"].Value))
                        ? "Preferred supplier from item details"
                        : row.Cells["SupplierNotes"].Value;
                    RefreshPreferredSupplierFromPriceBook(true);
                    SetStatus("Preferred supplier updated in the supplier price book.", SaveGreen);
                    return;
                }
            }

            AddSupplierPriceRow(new SupplierItemPrice
            {
                VendorID = vendor.VendorID,
                VendorName = vendor.VendorName,
                Rate = _numRate.Value,
                Unit = DisplayUnit(_cboUnit?.Text),
                IsPreferred = true,
                Notes = "Preferred supplier from item details"
            });
            RefreshPreferredSupplierFromPriceBook(true);
            SetStatus("Preferred supplier added to the supplier price book.", SaveGreen);
        }

        private void OpenSupplierPriceBookDialog()
        {
            using (Form dialog = ServoModalForm.Create("Manage Supplier Rates", 820, 560))
            {
                Panel shell = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = DS.BgPage,
                    Padding = new Padding(18),
                    Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE NO_INPUT_HOST NO_INPUT_OUTLINE_HOST"
                };

                Panel headerCard = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 86,
                    BackColor = Color.White,
                    Padding = new Padding(18, 16, 18, 14),
                    Margin = Padding.Empty,
                    Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE NO_INPUT_HOST NO_INPUT_OUTLINE_HOST"
                };
                headerCard.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(DS.Border))
                        e.Graphics.DrawRectangle(pen, 0, 0, headerCard.Width - 1, headerCard.Height - 1);
                };
                DS.Rounded(headerCard, 10);

                Label eyebrow = new Label
                {
                    Text = "SUPPLIER PRICE BOOK",
                    Dock = DockStyle.Top,
                    Height = 18,
                    Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
                    ForeColor = InfoBlue
                };
                Label title = new Label
                {
                    Text = string.IsNullOrWhiteSpace(_cboName?.Text) ? "New material supplier rates" : _cboName.Text.Trim(),
                    Dock = DockStyle.Top,
                    Height = 28,
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                    ForeColor = DS.Slate900
                };
                Label hint = new Label
                {
                    Text = "Add supplier rate options, choose one preferred vendor, and keep procurement defaults clean and consistent.",
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8.8f),
                    ForeColor = DS.Slate600
                };
                headerCard.Controls.Add(hint);
                headerCard.Controls.Add(title);
                headerCard.Controls.Add(eyebrow);

                DataGridView grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.None,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    MultiSelect = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoGenerateColumns = false,
                    EditMode = DataGridViewEditMode.EditOnEnter,
                    ColumnHeadersHeight = 30,
                    Margin = Padding.Empty,
                    Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE NO_INPUT_HOST NO_INPUT_OUTLINE_HOST"
                };
                grid.RowTemplate.Height = 26;

                Panel gridCard = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    Padding = new Padding(0),
                    Margin = new Padding(0, 14, 0, 0),
                    Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE NO_INPUT_HOST NO_INPUT_OUTLINE_HOST"
                };
                gridCard.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(DS.BorderStrong))
                        e.Graphics.DrawRectangle(pen, 0, 0, gridCard.Width - 1, gridCard.Height - 1);
                };
                DS.Rounded(gridCard, 10);

                var vendorColumn = new DataGridViewComboBoxColumn
                {
                    Name = "SupplierVendorID",
                    HeaderText = "Supplier",
                    Width = 220,
                    DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                    FlatStyle = FlatStyle.Flat,
                    DisplayMember = "VendorName",
                    ValueMember = "VendorID"
                };
                vendorColumn.DataSource = _cboVendor.Items.OfType<Vendor>().Where(v => v != null).ToList();
                grid.Columns.Add(vendorColumn);
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierRate", HeaderText = "Rate (₹)", Width = 96 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierUnit", HeaderText = "Unit", Width = 82 });
                grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "SupplierPreferred", HeaderText = "Preferred", Width = 92 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierNotes", HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                grid.CurrentCellDirtyStateChanged += (s, e) =>
                {
                    if (grid.IsCurrentCellDirty)
                        grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                };
                grid.DataError += (s, e) => e.ThrowException = false;
                grid.CellValueChanged += (s, e) =>
                {
                    if (e.RowIndex < 0 || e.ColumnIndex < 0)
                        return;

                    if (!string.Equals(grid.Columns[e.ColumnIndex].Name, "SupplierPreferred", StringComparison.OrdinalIgnoreCase))
                        return;

                    bool isPreferred = TryParseBool(grid.Rows[e.RowIndex].Cells["SupplierPreferred"].Value);
                    if (!isPreferred)
                        return;

                    for (int i = 0; i < grid.Rows.Count; i++)
                        grid.Rows[i].Cells["SupplierPreferred"].Value = i == e.RowIndex;
                };

                Action<SupplierItemPrice> addDialogRow = price =>
                {
                    int index = grid.Rows.Add();
                    DataGridViewRow row = grid.Rows[index];
                    row.Cells["SupplierVendorID"].Value = price?.VendorID > 0 ? (object)price.VendorID : 0;
                    row.Cells["SupplierRate"].Value = price == null ? string.Empty : price.Rate.ToString("0.##");
                    row.Cells["SupplierUnit"].Value = string.IsNullOrWhiteSpace(price?.Unit) ? DisplayUnit(_cboUnit?.Text) : price.Unit;
                    row.Cells["SupplierPreferred"].Value = price != null && price.IsPreferred;
                    row.Cells["SupplierNotes"].Value = price?.Notes ?? string.Empty;
                };

                Func<List<SupplierItemPrice>> readDialogRows = () =>
                {
                    var rows = new List<SupplierItemPrice>();
                    foreach (DataGridViewRow row in grid.Rows)
                    {
                        int vendorId = TryParseInt(row.Cells["SupplierVendorID"].Value);
                        if (vendorId <= 0)
                            continue;

                        Vendor vendor = FindVendorById(vendorId);
                        rows.Add(new SupplierItemPrice
                        {
                            ItemID = _current?.ItemID,
                            VendorID = vendorId,
                            VendorName = vendor?.VendorName,
                            ItemName = _cboName.Text.Trim(),
                            Category = _cboCategory.SelectedItem?.ToString() ?? string.Empty,
                            Unit = Convert.ToString(row.Cells["SupplierUnit"].Value),
                            Rate = Math.Max(0m, ParseDecimalSafe(row.Cells["SupplierRate"].Value)),
                            IsPreferred = TryParseBool(row.Cells["SupplierPreferred"].Value),
                            Notes = Convert.ToString(row.Cells["SupplierNotes"].Value),
                            Source = "Item details",
                            EffectiveDate = DateTime.Now
                        });
                    }

                    if (rows.Count > 1 && rows.All(r => !r.IsPreferred))
                        rows[0].IsPreferred = true;
                    return rows;
                };

                foreach (SupplierItemPrice price in ReadSupplierPricesFromGrid())
                    addDialogRow(price);
                if (grid.Rows.Count == 0)
                    addDialogRow(null);

                gridCard.Controls.Add(grid);

                Panel footer = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 98,
                    BackColor = DS.BgPage,
                    Padding = new Padding(0, 14, 0, 0),
                    Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE NO_INPUT_HOST NO_INPUT_OUTLINE_HOST"
                };

                Panel actionBar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 38,
                    BackColor = DS.BgPage
                };

                Button add = MakeBtn("+ Add Supplier", Color.White, 118);
                add.ForeColor = InfoBlue;
                add.FlatAppearance.BorderColor = DS.BorderStrong;
                add.Click += (s, e) =>
                {
                    addDialogRow(null);
                    if (grid.Rows.Count > 0)
                        grid.CurrentCell = grid.Rows[grid.Rows.Count - 1].Cells["SupplierVendorID"];
                };

                Button remove = MakeBtn("Remove Row", Color.White, 104);
                remove.ForeColor = DS.Slate700;
                remove.FlatAppearance.BorderColor = DS.BorderStrong;
                remove.Click += (s, e) =>
                {
                    if (grid.SelectedRows.Count == 0)
                        return;

                    foreach (DataGridViewRow row in grid.SelectedRows)
                    {
                        if (!row.IsNewRow)
                            grid.Rows.Remove(row);
                    }

                    if (grid.Rows.Count == 0)
                        addDialogRow(null);
                };

                Button usePreferred = MakeBtn("Use Preferred Above", Color.White, 140);
                usePreferred.ForeColor = SaveGreen;
                usePreferred.FlatAppearance.BorderColor = DS.BorderStrong;
                usePreferred.Click += (s, e) =>
                {
                    Vendor vendor = _cboVendor.SelectedItem as Vendor;
                    if (vendor == null || vendor.VendorID <= 0)
                        return;

                    int rowIndex = -1;
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        if (TryParseInt(grid.Rows[i].Cells["SupplierVendorID"].Value) == vendor.VendorID)
                        {
                            rowIndex = i;
                            break;
                        }
                    }

                    if (rowIndex < 0)
                    {
                        addDialogRow(new SupplierItemPrice
                        {
                            VendorID = vendor.VendorID,
                            VendorName = vendor.VendorName,
                            Rate = _numRate.Value,
                            Unit = DisplayUnit(_cboUnit?.Text),
                            IsPreferred = true,
                            Notes = "Preferred supplier from item details"
                        });
                        rowIndex = grid.Rows.Count - 1;
                    }

                    DataGridViewRow row = grid.Rows[rowIndex];
                    row.Cells["SupplierVendorID"].Value = vendor.VendorID;
                    row.Cells["SupplierRate"].Value = _numRate.Value.ToString("0.##");
                    row.Cells["SupplierUnit"].Value = DisplayUnit(_cboUnit?.Text);
                    row.Cells["SupplierPreferred"].Value = true;
                    row.Cells["SupplierNotes"].Value = "Preferred supplier from item details";
                };

                Label footerHint = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 18,
                    Text = "Tip: keep one preferred supplier so purchase requests and comparisons stay predictable.",
                    Font = new Font("Segoe UI", 8.2f),
                    ForeColor = DS.Slate500
                };

                Button ok = MakeBtn("Apply", SaveGreen, 100);
                ok.Click += (s, e) =>
                {
                    BindSupplierPriceBook(readDialogRows());
                    RefreshPreferredSupplierFromPriceBook(true);
                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                };

                Button cancel = MakeBtn("Cancel", Color.White, 90);
                cancel.ForeColor = DS.Slate700;
                cancel.FlatAppearance.BorderColor = DS.BorderStrong;
                cancel.Click += (s, e) =>
                {
                    dialog.DialogResult = DialogResult.Cancel;
                    dialog.Close();
                };

                actionBar.Controls.AddRange(new Control[] { add, remove, usePreferred, cancel, ok });
                Action layoutFooterButtons = () =>
                {
                    int leftX = 0;
                    add.Location = new Point(leftX, 1);
                    leftX += add.Width + 10;
                    remove.Location = new Point(leftX, 1);
                    leftX += remove.Width + 10;
                    usePreferred.Location = new Point(leftX, 1);

                    int rightX = actionBar.ClientSize.Width - ok.Width;
                    ok.Location = new Point(Math.Max(0, rightX), 1);
                    cancel.Location = new Point(Math.Max(0, ok.Left - 10 - cancel.Width), 1);
                };
                actionBar.Resize += (s, e) => layoutFooterButtons();
                layoutFooterButtons();
                footer.Controls.Add(actionBar);
                footer.Controls.Add(footerHint);

                shell.Controls.Add(gridCard);
                shell.Controls.Add(footer);
                shell.Controls.Add(headerCard);
                dialog.Controls.Add(shell);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                dialog.ShowDialog(this);
            }
        }

        private List<SupplierItemPrice> ReadSupplierPricesFromGrid()
        {
            var prices = new List<SupplierItemPrice>();
            if (_gridSupplierPrices == null || _gridSupplierPrices.IsDisposed)
                return prices;

            foreach (DataGridViewRow row in _gridSupplierPrices.Rows)
            {
                int vendorId = TryParseInt(row.Cells["SupplierVendorID"].Value);
                decimal rate = ParseDecimalSafe(row.Cells["SupplierRate"].Value);
                string unit = Convert.ToString(row.Cells["SupplierUnit"].Value);
                string notes = Convert.ToString(row.Cells["SupplierNotes"].Value);
                bool isPreferred = TryParseBool(row.Cells["SupplierPreferred"].Value);
                if (vendorId <= 0)
                    continue;

                Vendor vendor = FindVendorById(vendorId);
                prices.Add(new SupplierItemPrice
                {
                    ItemID = _current?.ItemID,
                    VendorID = vendorId,
                    VendorName = vendor?.VendorName,
                    ItemName = _cboName.Text.Trim(),
                    Category = _cboCategory.SelectedItem?.ToString() ?? string.Empty,
                    Unit = string.IsNullOrWhiteSpace(unit) ? DisplayUnit(_cboUnit?.Text) : unit.Trim(),
                    Rate = Math.Max(0m, rate),
                    IsPreferred = isPreferred,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                    Source = "Item details",
                    EffectiveDate = DateTime.Now
                });
            }

            if (prices.Count > 1 && prices.All(p => !p.IsPreferred))
                prices[0].IsPreferred = true;

            return prices;
        }

        private void SupplierPriceGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_supplierPriceGridSyncInProgress || e.RowIndex < 0 || _gridSupplierPrices == null)
                return;

            if (string.Equals(_gridSupplierPrices.Columns[e.ColumnIndex].Name, "SupplierPreferred", StringComparison.OrdinalIgnoreCase))
            {
                bool isPreferred = TryParseBool(_gridSupplierPrices.Rows[e.RowIndex].Cells["SupplierPreferred"].Value);
                if (isPreferred)
                    MarkSupplierPriceRowAsPreferred(e.RowIndex);
            }

            RefreshPreferredSupplierFromPriceBook(false);
        }

        private void MarkSupplierPriceRowAsPreferred(int preferredRowIndex)
        {
            if (_gridSupplierPrices == null || preferredRowIndex < 0 || preferredRowIndex >= _gridSupplierPrices.Rows.Count)
                return;

            _supplierPriceGridSyncInProgress = true;
            try
            {
                for (int i = 0; i < _gridSupplierPrices.Rows.Count; i++)
                    _gridSupplierPrices.Rows[i].Cells["SupplierPreferred"].Value = i == preferredRowIndex;
            }
            finally
            {
                _supplierPriceGridSyncInProgress = false;
            }
        }

        private void RefreshPreferredSupplierFromPriceBook(bool alwaysApplyRate)
        {
            List<SupplierItemPrice> prices = ReadSupplierPricesFromGrid();
            SupplierItemPrice preferred = prices.FirstOrDefault(p => p.IsPreferred) ?? prices.FirstOrDefault();
            if (preferred == null)
                return;

            SelectInventoryVendorById(preferred.VendorID);
            if (_numRate != null && (alwaysApplyRate || _numRate.Value <= 0m || ((_cboVendor.SelectedItem as Vendor)?.VendorID ?? 0) == preferred.VendorID))
                _numRate.Value = Math.Max(_numRate.Minimum, Math.Min(_numRate.Maximum, preferred.Rate));
            if (_cboUnit != null && !string.IsNullOrWhiteSpace(preferred.Unit))
                SelectComboByText(_cboUnit, _unitSvc.NormalizeForPickerDisplayOrDefault(preferred.Unit));
        }

        private Vendor FindVendorById(int vendorId)
        {
            if (_cboVendor == null || vendorId <= 0)
                return null;

            foreach (object item in _cboVendor.Items)
            {
                Vendor vendor = item as Vendor;
                if (vendor != null && vendor.VendorID == vendorId)
                    return vendor;
            }

            return null;
        }

        private static int TryParseInt(object value)
        {
            int parsed;
            return value != null && int.TryParse(Convert.ToString(value), out parsed) ? parsed : 0;
        }

        private static decimal ParseDecimalSafe(object value)
        {
            decimal parsed;
            return decimal.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.CurrentCulture, out parsed)
                || decimal.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0m;
        }

        private static bool TryParseBool(object value)
        {
            bool parsed;
            return value != null && bool.TryParse(Convert.ToString(value), out parsed) && parsed;
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
            _detailVendorChoices = (vendors ?? new List<Vendor>()).Where(v => v != null).OrderBy(v => v.VendorName).ToList();
            if (_cboVendor == null || _cboVendor.IsDisposed)
            {
                PopulateSupplierFilterOptions();
                return;
            }
            _cboVendor.BeginUpdate();
            _cboVendor.Items.Clear();
            _cboVendor.Items.Add(new Vendor { VendorID = 0, VendorName = "(None)" });
            foreach (var vendor in _detailVendorChoices)
                _cboVendor.Items.Add(vendor);
            _cboVendor.SelectedIndex = 0;
            BindSupplierPriceVendorColumn(_detailVendorChoices);
            _cboVendor.EndUpdate();
            PopulateSupplierFilterOptions();
        }

        private void UpdateStockValue(object sender, EventArgs e)
        {
            if (_numStock == null || _numRate == null || _lblStockValue == null)
                return;
            decimal val = _numStock.Value * _numRate.Value;
            _lblStockValue.Text = "Reference Value: Rs " + val.ToString("N2");
        }

        private void LoadItemSuggestions(List<StockItem> items = null)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                _detailSuggestionItems = new List<StockItem>(items ?? _listSource ?? new List<StockItem>());
                List<string> names = new List<string>();
                _itemLookupByName.Clear();
                _bestSupplierByItemKey.Clear();
                foreach (var item in _detailSuggestionItems)
                {
                    string name = item?.ItemName?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (!_itemLookupByName.ContainsKey(name))
                    {
                        _itemLookupByName[name] = item;
                        names.Add(name);
                    }
                }

                names.Sort(StringComparer.CurrentCultureIgnoreCase);
                if (_cboName != null && !_cboName.IsDisposed)
                {
                    string currentText = _cboName.Text;
                    _cboName.BeginUpdate();
                    _cboName.Items.Clear();
                    if (names.Count > 0)
                        _cboName.Items.AddRange(names.Cast<object>().ToArray());
                    _cboName.Text = currentText;
                    _cboName.SelectionStart = _cboName.Text == null ? 0 : _cboName.Text.Length;
                    _cboName.SelectionLength = 0;
                    _cboName.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("InventoryForm.LoadItemSuggestions", ex);
            }
            AppRuntime.LogTiming("Inventory.LoadItemSuggestions", sw.ElapsedMilliseconds, "suggestions=" + _itemLookupByName.Count);
        }

        private void BindInventoryList(List<StockItem> items, bool forceWarn)
        {
            _listSource = items ?? new List<StockItem>();
            if (!forceWarn)
                _allItems = new List<StockItem>(_listSource);
            PopulateCategoryFilterOptions();
            PopulateSupplierFilterOptions();
            UpdateInventoryFilterVisualState();
            UpdateInventoryMetrics(_allItems.Count > 0 ? _allItems : _listSource);
            _inventoryForceWarn = forceWarn;
            if (_itemListModule != null && !_itemListModule.IsDisposed)
            {
                SetInventoryItemsSilently(_listSource);
                if (_current != null && _current.ItemID > 0)
                    SetInventorySelectedRowSilently(_current.ItemID);
                else if (_listSource.Count > 0)
                    _current = _listSource[0];
            }
            string suffix = forceWarn ? "procurement-ready items" : "catalog items";
            SetStatus($"Showing {_listSource.Count} {suffix}.", forceWarn ? WarnOrange : Color.Gray);
        }

        private void ApplyInventoryFilter()
        {
            if (_allItems == null || _allItems.Count == 0)
                return;

            string mode = _cboListMode?.SelectedItem?.ToString() ?? "All";
            string category = _cboCategoryFilter?.SelectedItem?.ToString() ?? "All Categories";
            string supplier = _cboSupplierFilter?.SelectedItem?.ToString() ?? "All Suppliers";
            string stockState = _cboStockStatusFilter?.SelectedItem?.ToString() ?? "All Material Modes";
            string activity = _cboActivityFilter?.SelectedItem?.ToString() ?? "All Activity";
            _listSource = BuildFilteredInventoryItems();
            UpdateInventoryMetrics(_listSource);
            _inventoryForceWarn = mode == "Procurement Required";
            if (_itemListModule != null && !_itemListModule.IsDisposed)
            {
                SetInventoryItemsSilently(_listSource);
                if (_current != null && _current.ItemID > 0)
                    SetInventorySelectedRowSilently(_current.ItemID);
                else if (_listSource.Count > 0)
                    _current = _listSource[0];
            }
            string statusSuffix = BuildInventoryResultSuffix(category, supplier, stockState, activity);
            Color statusColor = mode == "Procurement Required" || string.Equals(stockState, "Direct Purchase", StringComparison.OrdinalIgnoreCase) ? WarnOrange : Color.Gray;
            SetStatus($"Showing {_listSource.Count} {statusSuffix}.", statusColor);
        }

        private List<StockItem> BuildFilteredInventoryItems()
        {
            string term = GetInventorySearchText();
            string mode = _cboListMode?.SelectedItem?.ToString() ?? "All";
            string category = _cboCategoryFilter?.SelectedItem?.ToString() ?? "All Categories";
            string supplier = _cboSupplierFilter?.SelectedItem?.ToString() ?? "All Suppliers";
            string stockState = _cboStockStatusFilter?.SelectedItem?.ToString() ?? "All Material Modes";
            string activity = _cboActivityFilter?.SelectedItem?.ToString() ?? "All Activity";

            IEnumerable<StockItem> query = _allItems ?? new List<StockItem>();
            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(i =>
                    (i.ItemName ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (i.Category ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (i.VendorName ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.Equals(category, "All Categories", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => string.Equals(i.Category ?? string.Empty, category, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(supplier, "All Suppliers", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(supplier, "(No Supplier)", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(i => string.IsNullOrWhiteSpace(i.VendorName));
                else
                    query = query.Where(i => string.Equals(i.VendorName ?? string.Empty, supplier, StringComparison.OrdinalIgnoreCase));
            }
            if (mode == "Procurement Required")
                query = query.Where(IsProcurementRequired);
            else if (mode == "Supplier Ready")
                query = query.Where(i => !string.IsNullOrWhiteSpace(i.VendorName));
            else if (mode == "Needs Supplier")
                query = query.Where(i => string.IsNullOrWhiteSpace(i.VendorName));
            if (string.Equals(stockState, "Buffer Available", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.AvailableStock > 0m);
            else if (string.Equals(stockState, "Direct Purchase", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.AvailableStock <= 0m && !string.IsNullOrWhiteSpace(i.VendorName));
            else if (string.Equals(stockState, "Reserved For Jobs", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.ReservedStock > 0m);
            else if (string.Equals(stockState, "Needs Supplier", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => string.IsNullOrWhiteSpace(i.VendorName));
            if (string.Equals(activity, "High Value", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.StockValue >= 10000m);
            else if (string.Equals(activity, "Recently Updated", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.LastUpdated != default(DateTime) && i.LastUpdated.Date >= DateTime.Today.AddDays(-30));
            else if (string.Equals(activity, "Dormant 90+ Days", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.LastUpdated == default(DateTime) || i.LastUpdated.Date < DateTime.Today.AddDays(-90));
            else if (string.Equals(activity, "Unpriced", StringComparison.OrdinalIgnoreCase))
                query = query.Where(i => i.LastPurchaseRate <= 0m);

            return query.ToList();
        }

        private void ApplyInventoryItemMutation(StockItem freshItem, bool isNewItem)
        {
            if (freshItem == null)
                return;

            int existingIndex = _allItems.FindIndex(i => i.ItemID == freshItem.ItemID);
            if (existingIndex >= 0)
                _allItems[existingIndex] = freshItem;
            else
                _allItems.Insert(0, freshItem);

            _listSource = BuildFilteredInventoryItems();
            UpdateInventoryMetrics(_listSource);

            bool updatedVisibleRow = false;
            if (_itemListModule != null && !_itemListModule.IsDisposed)
            {
                _itemListModule.UpdateItem(freshItem.ItemID, freshItem);
                updatedVisibleRow = IncrementalRefreshService.TryUpdateVirtualRow(
                    _itemListModule.ListGrid,
                    _itemListModule.VisibleItemsBuffer,
                    freshItem.ItemID,
                    freshItem,
                    item => item.ItemID);

                if (isNewItem || !updatedVisibleRow || !_listSource.Any(i => i.ItemID == freshItem.ItemID))
                    SetInventoryItemsSilently(_listSource);

                SetInventorySelectedRowSilently(freshItem.ItemID);
            }

            _current = freshItem;
            PopulateDetail(freshItem);
        }

        private string GetInventorySearchText()
        {
            if (_txtSearch == null || _inventorySearchPlaceholderActive)
                return string.Empty;

            return (_txtSearch.Text ?? string.Empty).Trim();
        }

        private void PopulateCategoryFilterOptions()
        {
            if (_cboCategoryFilter == null)
                return;

            string previous = _cboCategoryFilter.SelectedItem?.ToString() ?? "All Categories";
            List<string> categories = (_allItems ?? new List<StockItem>())
                .Select(i => (i.Category ?? string.Empty).Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            _cboCategoryFilter.BeginUpdate();
            try
            {
                _cboCategoryFilter.Items.Clear();
                _cboCategoryFilter.Items.Add("All Categories");
                foreach (string category in categories)
                    _cboCategoryFilter.Items.Add(category);
                SelectComboByText(_cboCategoryFilter, previous);
                if (_cboCategoryFilter.SelectedIndex < 0)
                    _cboCategoryFilter.SelectedIndex = 0;
            }
            finally
            {
                _cboCategoryFilter.EndUpdate();
            }
        }

        private void PopulateSupplierFilterOptions()
        {
            if (_cboSupplierFilter == null)
                return;

            string previous = _cboSupplierFilter.SelectedItem?.ToString() ?? "All Suppliers";
            List<string> suppliers = (_allItems ?? new List<StockItem>())
                .Select(i => (i.VendorName ?? string.Empty).Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            _cboSupplierFilter.BeginUpdate();
            try
            {
                _cboSupplierFilter.Items.Clear();
                _cboSupplierFilter.Items.Add("All Suppliers");
                foreach (string supplier in suppliers)
                    _cboSupplierFilter.Items.Add(supplier);
                _cboSupplierFilter.Items.Add("(No Supplier)");
                SelectComboByText(_cboSupplierFilter, previous);
                if (_cboSupplierFilter.SelectedIndex < 0)
                    _cboSupplierFilter.SelectedIndex = 0;
            }
            finally
            {
                _cboSupplierFilter.EndUpdate();
            }
        }

        private void UpdateInventoryFilterVisualState()
        {
            string mode = _cboListMode?.SelectedItem?.ToString() ?? "All";
            ApplyFilterChipState(_btnFilterAll, mode == "All");
            ApplyFilterChipState(_btnFilterToOrder, mode == "Procurement Required");
            ApplyFilterChipState(_btnFilterSupplierLinked, mode == "Supplier Ready");
            ApplyFilterChipState(_btnFilterNeedsSupplier, mode == "Needs Supplier");
        }

        private void ApplyFilterChipState(Button button, bool selected)
        {
            if (button == null)
                return;

            button.BackColor = selected ? InfoBlue : Color.White;
            button.ForeColor = selected ? Color.White : (button.Text == "Needs Supplier" ? DelRed : button.Text == "Procurement Required" ? WarnOrange : SaveGreen);
            button.FlatAppearance.BorderColor = selected ? InfoBlue : DS.BorderStrong;
        }

        private void UpdateInventoryMetrics(List<StockItem> items)
        {
            items = items ?? new List<StockItem>();
            int vendorLinked = items.Count(i => !string.IsNullOrWhiteSpace(i.VendorName));
            int toOrder = items.Count(IsProcurementRequired);
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
                SetStatus("Loading procurement queue...", WarnOrange);
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

        private Panel BuildInventoryEmptyState()
        {
            bool hasFilters = HasActiveInventoryFilters();
            Panel panel = new Panel
            {
                Width = Math.Max(760, _itemListModule?.ClientSize.Width > 20 ? _itemListModule.ClientSize.Width - 18 : 880),
                Height = 420,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Inventory, 72, Color.FromArgb(238, 242, 255), InfoBlue);
            icon.Location = new Point((panel.Width - icon.Width) / 2, 130);
            panel.Controls.Add(icon);
            panel.Controls.Add(new Label { Text = hasFilters ? "No materials match the current filters" : "No items found", Location = new Point(0, 218), Size = new Size(panel.Width, 28), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter });
            panel.Controls.Add(new Label { Text = hasFilters ? "Clear one or more filters to broaden the search, or refresh to load the latest catalog." : "Add your first material item. Supplier, buying rate, and planning quantity can be completed later.", Location = new Point(0, 248), Size = new Size(panel.Width, 24), Font = DS.Body, ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleCenter });
            Button add = MakeBtn(hasFilters ? "Clear Filters" : "+  Add Item", InfoBlue, 118);
            add.Location = new Point((panel.Width - add.Width) / 2, 294);
            add.Click += (s, e) =>
            {
                if (hasFilters)
                    ResetInventoryFilters();
                else
                    ShowInventoryItemDetailsDialog(null, true);
            };
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

        private void RenderItemBatch(bool reset, bool forceWarn)
        {
            _inventoryForceWarn = forceWarn;
            SetInventoryItemsSilently(_listSource);
        }

        private Panel MakeItemCard(StockItem item, bool forceWarn)
        {
            string lastUpdatedText = (item.LastUpdated == default(DateTime))
                ? "-"
                : item.LastUpdated.ToString("dd MMM");
            bool warn = forceWarn || IsProcurementRequired(item);
            bool needsVendor = string.IsNullOrWhiteSpace(item.VendorName);
            int availableWidth = _itemListModule?.ClientSize.Width ?? ClientSize.Width;
            int rowWidth = Math.Max(760, availableWidth > 20 ? availableWidth - 18 : 880);

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
                Text = warn ? "Procurement Required" : needsVendor ? "Needs Supplier" : "Supplier Ready",
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
            ShowInventoryItemDetailsDialog(item, false);
        }

        private void SelectItem(StockItem item)
        {
            if (item == null)
                return;

            _current = item;
            if (_suppressInventoryItemDialog)
            {
                if (_cboName != null && !_cboName.IsDisposed)
                    PopulateDetail(item);
                return;
            }
            ShowInventoryItemDetailsDialog(item, false);
        }

        private void SetInventoryItemsSilently(List<StockItem> items)
        {
            if (_itemListModule == null || _itemListModule.IsDisposed)
                return;

            bool previous = _suppressInventoryItemDialog;
            _suppressInventoryItemDialog = true;
            try
            {
                _itemListModule.SetItems(items ?? new List<StockItem>());
            }
            finally
            {
                _suppressInventoryItemDialog = previous;
            }
        }

        private void SetInventorySelectedRowSilently(int itemId)
        {
            if (_itemListModule == null || _itemListModule.IsDisposed || itemId <= 0)
                return;

            bool previous = _suppressInventoryItemDialog;
            _suppressInventoryItemDialog = true;
            try
            {
                _itemListModule.SetSelectedRowId(itemId);
            }
            finally
            {
                _suppressInventoryItemDialog = previous;
            }
        }

        private void PopulateDetail(StockItem item)
        {
            if (_cboName == null || _cboName.IsDisposed || item == null)
                return;
            _cboName.Text = item.ItemName ?? "";
            SelectComboByText(_cboCategory, item.Category);
            SelectComboByText(_cboUnit, _unitSvc.NormalizeForPickerDisplayOrDefault(item.Unit));
            _numStock.Value   = item.CurrentStock    > _numStock.Maximum   ? _numStock.Maximum   : item.CurrentStock;
            _numRate.Value    = item.LastPurchaseRate > _numRate.Maximum    ? _numRate.Maximum    : item.LastPurchaseRate;
            _numReorder.Value = item.ReorderLevel    > _numReorder.Maximum ? _numReorder.Maximum : item.ReorderLevel;
            _lblStockValue.Text = "Reference Value: Rs " + item.StockValue.ToString("N2");
            UpdateReorderButtonState(item);
            SelectInventoryVendorById(item.VendorID ?? 0);
            LoadSupplierPrices(item);
            UpdateInventorySupplierSnapshot(item);
        }

        private void NewRecord()
        {
            _current = null;
            if (_cboName == null || _cboName.IsDisposed)
                return;
            _cboName.Text = "";
            if (_cboCategory.Items.Count > 0) _cboCategory.SelectedIndex = 0;
            SelectComboByText(_cboUnit, _unitSvc.NormalizeForPickerDisplayOrDefault(UnitMeasurementService.DefaultCode));
            _numStock.Value = 0; _numRate.Value = 0; _numReorder.Value = 1;
            _cboVendor.SelectedIndex = 0;
            _lblStockValue.Text = "";
            BindSupplierPriceBook(null);
            UpdateReorderButtonState(null);
            UpdateInventorySupplierSnapshot(null);
            SetStatus("New material ready. Add supplier and buying context when available.", Color.Gray);
        }

        private async void QueueApplyInventoryItemDefaults()
        {
            if (IsDisposed || _cboName == null || _cboName.IsDisposed)
                return;

            string itemName = _cboName?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(itemName))
                return;

            int requestVersion = ++_itemDefaultsRequestVersion;
            try
            {
                await Task.Delay(120);
                if (requestVersion != _itemDefaultsRequestVersion || IsDisposed)
                    return;

                await ApplyInventoryItemDefaultsFromSelectionAsync(itemName, requestVersion);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.QueueApplyInventoryItemDefaults", ex);
            }
        }

        private async Task ApplyInventoryItemDefaultsFromSelectionAsync(string itemName, int requestVersion)
        {
            if (_isApplyingItemDefaults && requestVersion != _itemDefaultsRequestVersion)
                return;

            _isApplyingItemDefaults = true;
            try
            {
                StockItem matchedItem = TryGetInventoryItemByName(itemName);
                ApplyMatchedInventoryItemDefaults(matchedItem);

                string category = matchedItem?.Category ?? _cboCategory?.Text;
                SupplierOption best = await GetBestSupplierOptionCachedAsync(itemName, category);
                if (requestVersion != _itemDefaultsRequestVersion || IsDisposed)
                    return;

                if (!string.Equals(_cboName?.Text?.Trim(), itemName, StringComparison.OrdinalIgnoreCase))
                    return;

                ApplyBestSupplierDefaults(best);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.ApplyInventoryItemDefaultsFromSelection", ex);
            }
            finally
            {
                _isApplyingItemDefaults = false;
            }
        }

        private StockItem TryGetInventoryItemByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return null;

            StockItem item;
            if (_itemLookupByName.TryGetValue(itemName.Trim(), out item))
                return item;

            item = (_allItems ?? _listSource ?? new List<StockItem>())
                .FirstOrDefault(candidate => string.Equals(candidate.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                _itemLookupByName[itemName.Trim()] = item;
            return item;
        }

        private void ApplyMatchedInventoryItemDefaults(StockItem matchedItem)
        {
            if (matchedItem == null)
                return;

            if (!string.IsNullOrWhiteSpace(matchedItem.Category))
                SelectComboByText(_cboCategory, matchedItem.Category);

            if (!string.IsNullOrWhiteSpace(matchedItem.Unit))
                SelectComboByText(_cboUnit, _unitSvc.NormalizeForPickerDisplayOrDefault(matchedItem.Unit));

            if (matchedItem.LastPurchaseRate > 0 && _numRate != null && _numRate.Value <= 0)
                _numRate.Value = Math.Min(_numRate.Maximum, matchedItem.LastPurchaseRate);

            if (matchedItem.VendorID.GetValueOrDefault() > 0)
                SelectInventoryVendorById(matchedItem.VendorID.Value);

            if (matchedItem.ItemID > 0 && _currentSupplierPrices.Count == 0)
                LoadSupplierPrices(matchedItem);
        }

        private async Task<SupplierOption> GetBestSupplierOptionCachedAsync(string itemName, string category)
        {
            string cacheKey = (itemName ?? string.Empty).Trim() + "|" + (category ?? string.Empty).Trim();
            SupplierOption cached;
            if (_bestSupplierByItemKey.TryGetValue(cacheKey, out cached))
                return cached;

            SupplierOption best = await Task.Run(() => _vndSvc.GetBestSupplierForItem(itemName, 1m, category));
            _bestSupplierByItemKey[cacheKey] = best;
            return best;
        }

        private void ApplyBestSupplierDefaults(SupplierOption best)
        {
            if (best == null)
                return;

            bool vendorSelected = SelectInventoryVendorById(best.VendorID);
            if (_numRate != null && (_numRate.Value <= 0m || vendorSelected))
                _numRate.Value = Math.Max(_numRate.Minimum, Math.Min(_numRate.Maximum, best.Rate));

            if (_cboUnit != null && !string.IsNullOrWhiteSpace(best.Unit))
                SelectComboByText(_cboUnit, _unitSvc.NormalizeForPickerDisplayOrDefault(best.Unit));

            UpsertSupplierOptionInPriceBook(best, false);
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

        private async void Save()
        {
            if (string.IsNullOrWhiteSpace(_cboName.Text))
            { SetStatus("Item Name is required.", Color.Red); return; }

            try
            {
                List<SupplierItemPrice> supplierPrices = ReadSupplierPricesFromGrid();
                var vendor = _cboVendor.SelectedItem as Vendor;
                var item = new StockItem
                {
                    ItemName         = _cboName.Text.Trim(),
                    Category         = _cboCategory.SelectedItem?.ToString() ?? "",
                    Unit             = NormalizeUnit(_cboUnit.SelectedItem?.ToString() ?? UnitMeasurementService.DefaultCode),
                    CurrentStock     = _numStock.Value,
                    LastPurchaseRate = _numRate.Value,
                    ReorderLevel     = _numReorder.Value,
                    VendorID         = (vendor != null && vendor.VendorID > 0) ? vendor.VendorID : (int?)null,
                };

                SupplierItemPrice preferredSupplierPrice = supplierPrices.FirstOrDefault(p => p.IsPreferred) ?? supplierPrices.FirstOrDefault();
                if (preferredSupplierPrice != null)
                {
                    item.VendorID = preferredSupplierPrice.VendorID;
                    if (preferredSupplierPrice.Rate > 0m)
                        item.LastPurchaseRate = preferredSupplierPrice.Rate;
                    if (!string.IsNullOrWhiteSpace(preferredSupplierPrice.Unit))
                        item.Unit = NormalizeUnit(preferredSupplierPrice.Unit);
                }

                if (!TryValidate(item, _stockItemValidator, BrandingService.WindowTitle("Inventory"), () => _cboName.Focus()))
                {
                    SetStatus("Check required inventory fields and try again.", Color.Red);
                    return;
                }

                int currentItemId = _current?.ItemID ?? 0;
                SetStatus("Saving material item...", Color.Gray);
                bool succeeded = await RunSafeAsync("Saving inventory item", async () =>
                {
                    StockItem freshItem = await Task.Run(() =>
                    {
                        int persistedId;
                        if (currentItemId <= 0)
                            persistedId = _svc.Create(item);
                        else
                        {
                            item.ItemID = currentItemId;
                            _svc.Update(item);
                            persistedId = currentItemId;
                        }

                        _svc.SaveSupplierPrices(persistedId, item.ItemName, item.Category, supplierPrices);

                        return persistedId > 0 ? _svc.GetById(persistedId) : null;
                    });

                    RunOnUI(() =>
                    {
                        ApplyInventoryItemMutation(freshItem, currentItemId <= 0);
                        LoadSupplierPrices(freshItem);
                        LoadItemSuggestions(_allItems);
                        SetStatus("Material item saved. Next: update quantity, supplier, or create a purchase request when required.", SaveGreen);
                    });
                });
                if (!succeeded)
                    SetStatus("Inventory item could not be saved. Check the form and try again.", Color.Red);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.Save", ex);
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

            var dlg = ServoModalForm.Create("Create Purchase Request - " + _current.ItemName, 460, 350);

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

            Action<int> selectVendorById = vendorId =>
            {
                if (vendorId <= 0)
                    return;
                for (int i = 0; i < cboVendor.Items.Count; i++)
                {
                    Vendor vendor = cboVendor.Items[i] as Vendor;
                    if (vendor != null && vendor.VendorID == vendorId)
                    {
                        cboVendor.SelectedIndex = i;
                        return;
                    }
                }
            };

            // Pre-select preferred supplier if set
            if (_current.VendorID.HasValue)
            {
                selectVendorById(_current.VendorID.Value);
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
                Value = Math.Max(1, _current.ReorderLevel > 0 ? _current.ReorderLevel : 1)
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

            var lblSupplierInsight = new Label
            {
                Text = "Supplier comparison will use saved prices and purchase history.",
                Location = new Point(24, dy),
                Width = 400,
                Height = 34,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate600
            };
            var btnCompare = new Button
            {
                Text = "Compare Suppliers",
                Location = new Point(128, dy + 40),
                Width = 150,
                Height = 30,
                BackColor = DS.Primary600,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnCompare.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(lblSupplierInsight);
            dlg.Controls.Add(btnCompare);

            Action<SupplierOption> applySupplierOption = option =>
            {
                if (option == null)
                    return;
                selectVendorById(option.VendorID);
                numRate.Value = Math.Max(numRate.Minimum, Math.Min(numRate.Maximum, option.Rate));
                lblSupplierInsight.Text = "Selected: " + option.VendorName + " at " + IndiaFormatHelper.FormatCurrency(option.Rate) + " / " + (string.IsNullOrWhiteSpace(option.Unit) ? DisplayUnit(_current.Unit) : DisplayUnit(option.Unit)) + ".";
                lblSupplierInsight.ForeColor = SaveGreen;
            };

            try
            {
                SupplierOption best = _vndSvc.GetBestSupplierForItem(_current.ItemName, numQty.Value, _current.Category);
                if (best != null)
                    applySupplierOption(best);
                else
                    lblSupplierInsight.Text = "Supplier and price details are not available for this material yet. You can still create the request and enter the rate manually.";
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("InventoryForm.CreatePO.SupplierComparison", ex);
                lblSupplierInsight.Text = "Supplier comparison could not load. You can still create the request.";
                lblSupplierInsight.ForeColor = WarnOrange;
            }

            btnCompare.Click += (s, e) =>
            {
                using (var comparison = new SupplierPriceComparisonDialog(_current.ItemName, _current.Category, numQty.Value, _vndSvc))
                {
                    if (comparison.ShowDialog(dlg) == DialogResult.OK && comparison.SelectedOption != null)
                        applySupplierOption(comparison.SelectedOption);
                }
            };

            dy += 84;

            var btnOK = new Button
            {
                Text = "Create Request", DialogResult = DialogResult.OK,
                Location = new Point(208, dy), Width = 120, Height = 30,
                BackColor = SaveGreen, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button
            {
                Text = "Cancel", DialogResult = DialogResult.Cancel,
                Location = new Point(338, dy), Width = 80, Height = 30,
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
                    Notes       = "Purchase request created from Materials / Job Procurement."
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

        private void OpenInventorySupplierComparison()
        {
            if (_current == null)
            {
                SetStatus("Select a material before comparing suppliers.", WarnOrange);
                return;
            }

            decimal quantity = _current.ReorderLevel > 0m ? Math.Max(1m, _current.ReorderLevel) : 1m;
            using (var dialog = new SupplierPriceComparisonDialog(_current.ItemName, _current.Category, quantity, _vndSvc, _current.VendorID))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedOption == null)
                    return;

                SelectInventoryVendorById(dialog.SelectedOption.VendorID);
                if (_numRate != null)
                    _numRate.Value = Math.Max(_numRate.Minimum, Math.Min(_numRate.Maximum, dialog.SelectedOption.Rate));
                UpsertSupplierOptionInPriceBook(dialog.SelectedOption, true);
                UpdateInventorySupplierSnapshot(_current);
                SetStatus("Supplier recommendation applied: " + dialog.SelectedOption.VendorName + ".", SaveGreen);
            }
        }

        private void UpsertSupplierOptionInPriceBook(SupplierOption option, bool markPreferred)
        {
            if (option == null || option.VendorID <= 0 || _gridSupplierPrices == null)
                return;

            foreach (DataGridViewRow row in _gridSupplierPrices.Rows)
            {
                if (TryParseInt(row.Cells["SupplierVendorID"].Value) != option.VendorID)
                    continue;

                row.Cells["SupplierRate"].Value = option.Rate.ToString("0.##");
                row.Cells["SupplierUnit"].Value = string.IsNullOrWhiteSpace(option.Unit) ? DisplayUnit(_cboUnit?.Text) : option.Unit;
                if (markPreferred)
                    row.Cells["SupplierPreferred"].Value = true;
                row.Cells["SupplierNotes"].Value = string.IsNullOrWhiteSpace(option.Source) ? "Supplier comparison" : option.Source;
                if (markPreferred)
                    RefreshPreferredSupplierFromPriceBook(true);
                return;
            }

            AddSupplierPriceRow(new SupplierItemPrice
            {
                VendorID = option.VendorID,
                VendorName = option.VendorName,
                Rate = option.Rate,
                Unit = string.IsNullOrWhiteSpace(option.Unit) ? DisplayUnit(_cboUnit?.Text) : option.Unit,
                IsPreferred = markPreferred,
                Notes = string.IsNullOrWhiteSpace(option.Source) ? "Supplier comparison" : option.Source
            }, false);
            if (markPreferred)
                RefreshPreferredSupplierFromPriceBook(true);
        }

        private void ShowReorderSuggestions()
        {
            if (_current != null)
            {
                CreatePO();
                return;
            }

            LoadLowStock();
            SetStatus("Procurement queue loaded. Select a material to create a purchase request.", WarnOrange);
        }

        private void ShowStockTransferDialog()
        {
            if (_current == null)
            {
                SetStatus("Select an item before transferring stock.", WarnOrange);
                _txtSearch.Focus();
                return;
            }

            using (var dialog = ServoModalForm.Create("Transfer Stock - " + _current.ItemName, 470, 320))
            {
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
            int toOrder = rows.Count(IsProcurementRequired);
            int needsVendor = rows.Count(i => string.IsNullOrWhiteSpace(i.VendorName));
            var sb = new StringBuilder();
            sb.Append("<html><head><style>");
            sb.Append("body{font-family:Segoe UI,Arial,sans-serif;color:#0f172a;margin:28px}h1{font-size:24px;margin:0 0 6px}.meta{color:#64748b;margin-bottom:18px}.cards{display:flex;gap:12px;margin-bottom:18px}.card{border:1px solid #e2e8f0;border-radius:10px;padding:12px 16px;min-width:150px}.label{color:#64748b;font-size:12px}.value{font-size:20px;font-weight:700}table{border-collapse:collapse;width:100%;font-size:12px}th{background:#f1f5f9;text-align:left}th,td{border:1px solid #e2e8f0;padding:8px}.right{text-align:right}.low{color:#d97706;font-weight:700}.out{color:#dc2626;font-weight:700}.ok{color:#16a34a;font-weight:700}");
            sb.Append("</style></head><body>");
            sb.Append("<h1>").Append(Html(title)).Append("</h1>");
            sb.Append("<div class='meta'>Generated ").Append(DateTime.Now.ToString("dd MMM yyyy HH:mm")).Append("</div>");
            sb.Append("<div class='cards'>");
            sb.Append(KpiHtml("Items", rows.Count.ToString("N0")));
            sb.Append(KpiHtml("Procurement required", toOrder.ToString("N0")));
            sb.Append(KpiHtml("Needs supplier", needsVendor.ToString("N0")));
            sb.Append(KpiHtml("Purchase value", IndiaFormatHelper.FormatCurrency(value)));
            sb.Append("</div><table><tr><th>Item</th><th>Category</th><th>Unit</th><th class='right'>Current Qty</th><th class='right'>Rate</th><th class='right'>Value</th><th>Status</th><th>Supplier</th></tr>");
            foreach (StockItem item in rows)
            {
                string status = InventoryProcurementStatus(item);
                string cls = IsProcurementRequired(item) ? "low" : string.IsNullOrWhiteSpace(item.VendorName) ? "out" : "ok";
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
            if (IsProcurementRequired(item))
                return "Procurement Required";
            return string.IsNullOrWhiteSpace(item.VendorName) ? "Needs Supplier" : "Supplier Ready";
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

        private void SetStatus(string msg, Color c)
        {
            if (_lblStatus == null || _lblStatus.IsDisposed)
                return;
            _lblStatus.Text = msg;
            _lblStatus.ForeColor = c;
        }

        private void UpdateReorderButtonState(StockItem item)
        {
            if (_btnReorder == null)
                return;

            bool enabled = item != null && !string.IsNullOrWhiteSpace(item.ItemName);
            _btnReorder.Enabled = enabled;
            _btnReorder.ForeColor = enabled ? DS.Primary600 : Color.Gray;
        }

        private string NormalizeUnit(string unit)
        {
            return _unitSvc.NormalizeForStorage(unit);
        }

        private string DisplayUnit(string unit)
        {
            return _unitSvc.NormalizeForDisplayOrDefault(unit);
        }

        private ComboBox CreateInventoryFilterCombo(string defaultText, IEnumerable<string> items)
        {
            ComboBox combo = new ComboBox
            {
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Standard,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 4, 12, 4),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            foreach (string item in items ?? Enumerable.Empty<string>())
                combo.Items.Add(item);
            if (combo.Items.Count == 0)
                combo.Items.Add(defaultText);
            combo.SelectedIndex = 0;
            return combo;
        }

        private void ResetInventoryFilters()
        {
            if (_cboListMode != null)
                _cboListMode.SelectedIndex = 0;
            if (_cboCategoryFilter != null)
                _cboCategoryFilter.SelectedIndex = 0;
            if (_cboSupplierFilter != null)
                _cboSupplierFilter.SelectedIndex = 0;
            if (_cboStockStatusFilter != null)
                _cboStockStatusFilter.SelectedIndex = 0;
            if (_cboActivityFilter != null)
                _cboActivityFilter.SelectedIndex = 0;

            ResetInventorySearchPlaceholder();
            ApplyInventoryFilter();
            SetStatus("Inventory filters cleared. Showing the full material catalog.", Color.Gray);
        }

        private void ResetInventorySearchPlaceholder()
        {
            if (_txtSearch == null)
                return;

            _inventorySearchPlaceholderActive = false;
            _txtSearch.Text = string.Empty;
            _txtSearch.ForeColor = DS.Slate900;
        }

        private bool HasActiveInventoryFilters()
        {
            return !string.IsNullOrWhiteSpace(GetInventorySearchText())
                || !string.Equals(_cboListMode?.SelectedItem?.ToString() ?? "All", "All", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_cboCategoryFilter?.SelectedItem?.ToString() ?? "All Categories", "All Categories", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_cboSupplierFilter?.SelectedItem?.ToString() ?? "All Suppliers", "All Suppliers", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_cboStockStatusFilter?.SelectedItem?.ToString() ?? "All Material Modes", "All Material Modes", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(_cboActivityFilter?.SelectedItem?.ToString() ?? "All Activity", "All Activity", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildInventoryResultSuffix(string category, string supplier, string stockState, string activity)
        {
            List<string> tags = new List<string>();
            if (!string.Equals(category, "All Categories", StringComparison.OrdinalIgnoreCase))
                tags.Add(category);
            if (!string.Equals(supplier, "All Suppliers", StringComparison.OrdinalIgnoreCase))
                tags.Add(supplier);
            if (!string.Equals(stockState, "All Material Modes", StringComparison.OrdinalIgnoreCase))
                tags.Add(stockState);
            if (!string.Equals(activity, "All Activity", StringComparison.OrdinalIgnoreCase))
                tags.Add(activity);
            return tags.Count == 0 ? "items" : string.Join(", ", tags) + " items";
        }

        private static bool IsProcurementRequired(StockItem item)
        {
            if (item == null)
                return false;

            return item.AvailableStock <= 0m || item.IsLowStock;
        }

        private void UpdateInventorySupplierSnapshot(StockItem item)
        {
            if (_lblSupplierSnapshotEyebrow == null || _lblSupplierSnapshotItem == null || _lblSupplierSnapshotSummary == null || _lblSupplierSnapshotDetail == null || _lblSupplierSnapshotRecommendation == null || _btnCompareSuppliers == null)
                return;

            SupplierSnapshotSummary summary;
            if (item == null || string.IsNullOrWhiteSpace(item.ItemName))
            {
                summary = SupplierSnapshotFormatter.CreatePrompt(
                    "Select a material to compare offers",
                    "Best supplier, live offer count, and price guidance appear here.",
                    "Choose a material to see recent supplier history.");
            }
            else
            {
                try
                {
                    decimal comparisonQuantity = item.ReorderLevel > 0m ? Math.Max(1m, item.ReorderLevel) : 1m;
                    List<SupplierOption> options = _vndSvc.GetSupplierOptions(item.ItemName, item.Category);
                    summary = SupplierSnapshotFormatter.CreateSummary(item.ItemName, comparisonQuantity, options, DisplayUnit);
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("InventoryForm.UpdateInventorySupplierSnapshot", ex);
                    summary = SupplierSnapshotFormatter.CreatePrompt(
                        item.ItemName,
                        "Supplier offers could not be analyzed right now.",
                        "Open comparison to retry supplier analysis.");
                }
            }

            _lblSupplierSnapshotEyebrow.Text = summary.EyebrowText;
            _lblSupplierSnapshotEyebrow.ForeColor = summary.HasOptions ? (summary.HasMultipleOptions ? InfoBlue : SaveGreen) : DS.Slate500;
            _lblSupplierSnapshotItem.Text = summary.ItemText;
            _lblSupplierSnapshotSummary.Text = summary.SummaryText;
            _lblSupplierSnapshotSummary.ForeColor = summary.HasOptions ? DS.Slate900 : DS.Slate600;
            _lblSupplierSnapshotDetail.Text = summary.DetailText;
            _lblSupplierSnapshotDetail.ForeColor = summary.HasOptions ? InfoBlue : DS.Slate500;
            _lblSupplierSnapshotRecommendation.Text = summary.RecommendationText;
            _lblSupplierSnapshotRecommendation.Visible = !string.IsNullOrWhiteSpace(summary.RecommendationText);
            _btnCompareSuppliers.Enabled = item != null && !string.IsNullOrWhiteSpace(item.ItemName);
            _toolTip.SetToolTip(_btnCompareSuppliers, summary.TooltipText ?? "Compare supplier prices for this material");
        }

        private bool SelectInventoryVendorById(int vendorId)
        {
            if (_cboVendor == null || vendorId <= 0)
                return false;

            for (int i = 0; i < _cboVendor.Items.Count; i++)
            {
                Vendor vendor = _cboVendor.Items[i] as Vendor;
                if (vendor != null && vendor.VendorID == vendorId)
                {
                    _cboVendor.SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static void EnsureComboItem(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrWhiteSpace(value))
                return;

            bool exists = combo.Items.Cast<object>().Any(item => string.Equals(item?.ToString(), value, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                combo.Items.Add(value);
        }

        private ComboBox AddComboField(string label, ref int y, ComboBoxStyle style)
        {
            _detail.Controls.Add(MakeLabel(label, new Point(DetailLabelX, y + 3)));
            var combo = new ComboBox
            {
                Location = new Point(DetailInputX, y), Width = DetailInputWidth,
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
            int index = -1;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i]?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
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
                ForeColor = InfoBlue, Location = new Point(DetailLabelX, y), Width = DetailSectionWidth, Height = 22,
                BackColor = SectionBg, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            y += 28;
            return lbl;
        }

        private Label MakeLabel(string text, System.Drawing.Point loc) => new Label
        {
            Text = text, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.Gray,
            Location = loc, Width = DetailLabelWidth, TextAlign = ContentAlignment.MiddleRight
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

        private sealed class InventoryListModule : VirtualListModuleBase<StockItem>
        {
            public InventoryListModule()
            {
                BackColor = Color.White;
                Grid.BorderStyle = BorderStyle.None;
                Grid.BackgroundColor = Color.White;
                Grid.GridColor = DS.Border;
                Grid.EnableHeadersVisualStyles = false;
                Grid.ColumnHeadersDefaultCellStyle.BackColor = Color.White;
                Grid.ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate600;
                Grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                Grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.75f);
                Grid.DefaultCellStyle.BackColor = Color.White;
                Grid.DefaultCellStyle.ForeColor = DS.Slate900;
                Grid.DefaultCellStyle.SelectionBackColor = DS.Indigo50;
                Grid.DefaultCellStyle.SelectionForeColor = DS.Slate900;
                Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                Grid.RowTemplate.Height = 38;
                SetStatusVisible(false);
            }

            protected override void BuildColumns(DataGridView grid)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Material", FillWeight = 30f });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 16f });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Unit", FillWeight = 10f });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentStock", HeaderText = "Stock", FillWeight = 12f });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StockValue", HeaderText = "Value", FillWeight = 14f });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "VendorName", HeaderText = "Supplier", FillWeight = 18f });
            }

            protected override int GetRowId(StockItem item)
            {
                return item == null ? 0 : item.ItemID;
            }

            protected override object GetCellValue(StockItem item, string columnName)
            {
                if (item == null)
                    return string.Empty;

                switch (columnName)
                {
                    case "ItemName":
                        return item.ItemName ?? string.Empty;
                    case "Category":
                        return item.Category ?? "General";
                    case "Unit":
                        return item.Unit ?? UnitMeasurementService.DefaultCode;
                    case "CurrentStock":
                        return item.CurrentStock.ToString("N1");
                    case "StockValue":
                        return item.StockValue.ToString("N2");
                    case "VendorName":
                        return string.IsNullOrWhiteSpace(item.VendorName) ? "Needs Supplier" : item.VendorName;
                    default:
                        return string.Empty;
                }
            }
        }

        private sealed class InventoryLoadSnapshot
        {
            public List<StockItem> Items { get; set; }
            public List<Vendor> Vendors { get; set; }
        }
    }
}



