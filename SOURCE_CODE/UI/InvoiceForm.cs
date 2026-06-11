using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI.Controls;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.DAL;
using System.Linq;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>
    /// Full ERP Invoice form:
    ///   Header (invoice no / dates / client / contract / status)
    ///   Line Items â€” DataGridView (Description, Qty, Rate, Amount)
    ///   GST Summary section
    ///   Action buttons
    /// Left list panel + right document panel via SplitContainer.
    /// </summary>
    public class InvoiceForm : DeferredPageControl
    {
        // â”€â”€ Services / DAL â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly InvoiceService  _invSvc      = new InvoiceService();
        private readonly ClientService   _clientSvc   = new ClientService();
        private readonly ContractService _contractSvc = new ContractService();
        private readonly SiteService     _siteSvc     = new SiteService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly PaymentService _paymentSvc = new PaymentService();
        private readonly MasterDataService _masterDataSvc = new MasterDataService();
        private readonly HsnSacMasterService _hsnSvc = new HsnSacMasterService();
        private readonly InvoiceAnalyticsService _invoiceAnalyticsSvc = new InvoiceAnalyticsService();
        private readonly ToolTip _toolTip = new ToolTip();

        // â”€â”€ List panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private FlowLayoutPanel _invoiceFlow;

        // â”€â”€ Header fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private TextBox        _txtInvNo;
        private DateTimePicker _dtpInvDate, _dtpDueDate, _dtpPODate;
        private ComboBox       _cmbClient, _cmbSite, _cmbContract, _cmbStatus, _cmbTemplate, _cmbGstMode, _cmbCoverageType, _cmbWarrantyStatus;
        private TextBox        _txtNotes, _txtSubject, _txtPONumber, _txtSendInvoiceTo;
        private TextBox        _txtNudges, _txtPaymentTerms, _txtPlaceOfSupply, _txtChecklist, _txtAssetDetails, _txtPaymentHistory, _txtInventorySummary;
        private DateTimePicker _dtpWarrantyExpiry, _dtpNextServiceDue;

        // â”€â”€ Line items grid â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private DataGridView _grid;
        private DataGridView _gridChecklist, _gridAssets, _gridPaymentHistory;
        private List<StockItem> _inventoryItems = new List<StockItem>();
        private List<ServiceRateCard> _serviceRateCards = new List<ServiceRateCard>();
        private List<ClientAsset> _clientAssets = new List<ClientAsset>();
        private List<HsnSacMasterEntry> _hsnSacEntries = new List<HsnSacMasterEntry>();
        private List<InvoiceCatalogItem> _invoiceCatalog = new List<InvoiceCatalogItem>();
        private FlowLayoutPanel _itemSourceTabs;
        private string _activeItemSource = "All";
        private Button _btnAddInvoiceLine, _btnAddChecklistRow, _btnAddAssetRow, _btnRecordWorkflowPayment;

        // â”€â”€ GST controls â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private NumericUpDown _numGST;
        private NumericUpDown _numRoundOff;
        private Panel _documentHost;
        private Panel _documentPage;

        // â”€â”€ GST summary labels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Label _lblSubTotal, _lblGSTAmt, _lblTotal, _lblBalance, _lblCGSTAmt, _lblSGSTAmt, _lblIGSTAmt, _lblRoundOffAmt;
        private Label _lblTaxableSummary, _lblAmountPaidSummary;
        private Label _lblRightSubTotal, _lblRightGST, _lblRightTotal, _lblRightBalance;

        // â”€â”€ Status bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Label _lblStatus;

        // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Invoice _current;   // null = new
        private bool    _updating;  // suppress grid change re-entrancy
        private Panel   _selectedCard;
        private bool    _initialLoadQueued;
        private bool    _dataInitialized;
        private bool    _invoiceListRefreshing;
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<InvoiceTemplate> _templates = new List<InvoiceTemplate>();
        private Button _btnNewInvoice;
        private Button _btnSaveInvoice;
        private Panel _invoiceDashboardPanel;
        private Panel _invoiceWorkspacePanel;
        private Panel _invoiceWorkflowCard;
        private TableLayoutPanel _invoiceWorkflowTable;
        private DateTimePicker _invoiceDashFromPicker;
        private DateTimePicker _invoiceDashToPicker;
        private ComboBox _invoiceDashGroupingCombo;
        private InvoiceDashboardSnapshot _invoiceDashboardSnapshot;
        private bool _invoiceDashboardRefreshing;
        private bool _invoiceDashboardLayingOut;
        private readonly Dictionary<string, int> _invoiceDashboardCardHeights = new Dictionary<string, int>();
        private readonly HashSet<ComboBox> _invoiceComboBoxes = new HashSet<ComboBox>();
        private readonly Queue<Action> _deferredDropdownActions = new Queue<Action>();
        private bool _summaryRecalcPending;
        private bool _editabilityPending;
        private int _lastProcessedClientId = -1;

        // â”€â”€ Colours â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color OrangeCol = DS.Amber500;

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

        public InvoiceForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            var ctorWatch = System.Diagnostics.Stopwatch.StartNew();
            BuildLayout();
            AppRuntime.LogTiming("Invoice.Ctor.BuildLayout", ctorWatch.ElapsedMilliseconds); ctorWatch.Restart();
            UIHelper.ApplyInputStyles(Controls);
            AppRuntime.LogTiming("Invoice.Ctor.ApplyInputStyles", ctorWatch.ElapsedMilliseconds); ctorWatch.Restart();
            SalesUiPolishService.ApplyAfterRebuild(this, "Invoices");
            AppRuntime.LogTiming("Invoice.Ctor.SalesPolish", ctorWatch.ElapsedMilliseconds); ctorWatch.Restart();
            ApplyInvoicePreviewSkin(Controls);
            AppRuntime.LogTiming("Invoice.Ctor.PreviewSkin", ctorWatch.ElapsedMilliseconds); ctorWatch.Restart();
            RestoreModernInvoiceInputStyles(Controls);
            RestoreInvoiceEditorInputState();
            ApplyPermissions();
            AppRuntime.LogTiming("Invoice.Ctor.RestoreAndPermissions", ctorWatch.ElapsedMilliseconds); ctorWatch.Restart();
            if (SessionManager.HasPermission("Invoices", "Create") || SessionManager.HasPermission("Invoices", "Edit"))
                RestoreInvoiceEditorInputState();
            ShowInvoiceDashboard();
            RecordDeletionUi.BindDeleteShortcut(this, () => { DeleteCurrentInvoice(); return Task.FromResult(0); }, () => _current != null && _current.InvoiceID > 0);
            HandleCreated += (s, e) =>
            {
                QueueInitialLoad();
                BeginInvoke((Action)RestoreInvoiceEditorInputState);
            };
            ParentChanged += (s, e) => QueueInitialLoad();
            Load += (s, e) => QueueInitialLoad();
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                    QueueInitialLoad();
            };
        }

        private void QueueInitialLoad()
        {
            Control dispatcher = FindForm() ?? Parent;
            if (_initialLoadQueued || _dataInitialized || dispatcher == null || !dispatcher.IsHandleCreated)
                return;

            _initialLoadQueued = true;
            ShowStatus("Loading invoices...", Color.Gray);
            Task.Run(() =>
            {
                try
                {
                    var clients = _clientSvc.GetAllClients();
                    if (!IsDisposed && dispatcher.IsHandleCreated)
                    {
                        dispatcher.Invoke((Action)(() =>
                        {
                            _clients = clients ?? new List<B2BClient>();
                            LoadClientDropdowns();
                        }));
                    }

                    var templates = _invSvc.GetActiveTemplates();
                    var inventory = _inventorySvc.GetAll();
                    var rateCards = _masterDataSvc.GetRateCards();
                    var assets = _masterDataSvc.GetAssets();
                    var hsnSac = _hsnSvc.GetAll();
                    var invoices = _invSvc.GetAllInvoices()
                        .OrderByDescending(i => i.InvoiceDate)
                        .Take(120)
                        .ToList();

                    if (IsDisposed || !IsHandleCreated)
                        return;

                    // Heavy, control-free work stays on this background thread: the
                    // 1,500-item catalog rebuild (per-item HSN/GST resolution) and the
                    // checklist-template DB round-trips used to freeze the UI for ~30s
                    // when they ran inside the Invoke below.
                    _inventoryItems = inventory ?? new List<StockItem>();
                    _serviceRateCards = rateCards ?? new List<ServiceRateCard>();
                    _clientAssets = assets ?? new List<ClientAsset>();
                    _hsnSacEntries = hsnSac ?? new List<HsnSacMasterEntry>();
                    RebuildInvoiceCatalog();
                    List<string> checklistSuggestions = BuildChecklistSuggestions().ToList();

                    if (IsDisposed || !IsHandleCreated)
                        return;

                    dispatcher.Invoke((Action)(() =>
                    {
                        _clients = clients ?? _clients ?? new List<B2BClient>();
                        _templates = templates ?? new List<InvoiceTemplate>();
                        BindInventoryItems();
                        BindWorkflowPickers(checklistSuggestions);
                        LoadClientDropdowns();
                        BindTemplateDropdown();
                        BindInvoiceList(invoices);
                        _dataInitialized = true;
                    }));
                }
                finally
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        try { dispatcher.Invoke((Action)(() => _initialLoadQueued = false)); }
                        catch { }
                    }
                }
            });
        }

        private void BindInventoryItems()
        {
            if (IsInvoiceDropdownOpen())
                return;

            if (!(_grid.Columns["Description"] is DataGridViewComboBoxColumn descColumn))
                return;

            descColumn.Items.Clear();
            object[] names = GetCatalogForActiveSource()
                .Select(i => i.Description)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .Cast<object>()
                .ToArray();
            descColumn.Items.AddRange(names);
        }

        private IEnumerable<InvoiceCatalogItem> GetCatalogForActiveSource()
        {
            if (string.Equals(_activeItemSource, "All", StringComparison.OrdinalIgnoreCase))
                return _invoiceCatalog;
            return _invoiceCatalog.Where(i => string.Equals(i.Source, _activeItemSource, StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Category, _activeItemSource, StringComparison.OrdinalIgnoreCase));
        }

        private void RegisterInvoiceEditorCombos()
        {
            RegisterInvoiceComboBox(_cmbStatus);
            RegisterInvoiceComboBox(_cmbClient);
            RegisterInvoiceComboBox(_cmbSite);
            RegisterInvoiceComboBox(_cmbContract);
            RegisterInvoiceComboBox(_cmbTemplate);
            RegisterInvoiceComboBox(_cmbGstMode);
            RegisterInvoiceComboBox(_cmbCoverageType);
            RegisterInvoiceComboBox(_cmbWarrantyStatus);
            RegisterInvoiceComboBox(_invoiceDashGroupingCombo);
        }

        private void RegisterInvoiceComboBox(ComboBox combo)
        {
            if (combo == null || _invoiceComboBoxes.Contains(combo))
                return;

            _invoiceComboBoxes.Add(combo);
            combo.IntegralHeight = false;
            combo.MaxDropDownItems = Math.Max(combo.MaxDropDownItems, 12);
            combo.DropDownHeight = Math.Max(combo.DropDownHeight, combo.ItemHeight * combo.MaxDropDownItems + 2);
            combo.DropDown += InvoiceCombo_DropDown;
            combo.DropDownClosed += InvoiceCombo_DropDownClosed;
            combo.Disposed += (s, e) =>
            {
                ComboBox disposed = s as ComboBox;
                if (disposed != null)
                    _invoiceComboBoxes.Remove(disposed);
            };
        }

        private void InvoiceCombo_DropDown(object sender, EventArgs e)
        {
        }

        private void InvoiceCombo_DropDownClosed(object sender, EventArgs e)
        {
            BeginInvoke((Action)FlushDeferredDropdownWork);
        }

        private bool IsInvoiceDropdownOpen()
        {
            return _invoiceComboBoxes.Any(combo => combo != null && !combo.IsDisposed && combo.DroppedDown)
                || (_grid != null && _grid.EditingControl is ComboBox gridCombo && gridCombo.DroppedDown)
                || (_gridChecklist != null && _gridChecklist.EditingControl is ComboBox checklistCombo && checklistCombo.DroppedDown)
                || (_gridAssets != null && _gridAssets.EditingControl is ComboBox assetsCombo && assetsCombo.DroppedDown);
        }

        private void FocusPanelIfNoDropdownOpen(Control panel)
        {
        }

        private void RequestRecalculateSummary()
        {
            if (IsInvoiceDropdownOpen())
            {
                _summaryRecalcPending = true;
                return;
            }

            RecalculateSummary();
        }

        private void RequestApplyInvoiceEditability()
        {
            if (IsInvoiceDropdownOpen())
            {
                _editabilityPending = true;
                return;
            }

            ApplyInvoiceEditability();
        }

        private void FlushDeferredDropdownWork()
        {
            if (IsDisposed || IsInvoiceDropdownOpen())
                return;

            while (_deferredDropdownActions.Count > 0)
            {
                Action action = _deferredDropdownActions.Dequeue();
                action?.Invoke();
                if (IsInvoiceDropdownOpen())
                    return;
            }

            if (_editabilityPending)
            {
                _editabilityPending = false;
                ApplyInvoiceEditability();
            }

            if (_summaryRecalcPending)
            {
                _summaryRecalcPending = false;
                RecalculateSummary();
            }
        }

        private void RunAfterDropdownClosed(Action action)
        {
            if (action == null)
                return;

            if (!IsInvoiceDropdownOpen())
            {
                action();
                return;
            }

            _deferredDropdownActions.Enqueue(action);
        }

        private void RebuildInvoiceCatalog()
        {
            var items = new List<InvoiceCatalogItem>();
            foreach (StockItem stock in _inventoryItems ?? new List<StockItem>())
            {
                if (string.IsNullOrWhiteSpace(stock.ItemName))
                    continue;
                string category = NormalizeItemCategory(stock.Category, "Material");
                items.Add(new InvoiceCatalogItem
                {
                    Source = "Materials",
                    Description = stock.ItemName,
                    Category = category,
                    HsnSacCode = ResolveHsnSac(stock.ItemName, category, true),
                    Unit = string.IsNullOrWhiteSpace(stock.Unit) ? "Nos" : stock.Unit,
                    Rate = stock.LastPurchaseRate,
                    GstPercent = ResolveGstPercent(stock.ItemName, category, true),
                    TaxType = "Taxable",
                    Notes = BuildStockNote(stock),
                    StockItemId = stock.ItemID,
                    IsStockItem = true
                });
            }

            foreach (ServiceRateCard rate in (_serviceRateCards ?? new List<ServiceRateCard>()).Where(r => r.IsActive))
            {
                string category = NormalizeItemCategory(rate.Category, "Service");
                items.Add(new InvoiceCatalogItem
                {
                    Source = category == "Labour" ? "Labour" : "Services",
                    Description = rate.ServiceName,
                    Category = category,
                    HsnSacCode = ResolveHsnSac(rate.ServiceName, category, false),
                    Unit = string.IsNullOrWhiteSpace(rate.Unit) ? "Job" : rate.Unit,
                    Rate = rate.Rate,
                    GstPercent = rate.GstPercent <= 0 ? ResolveGstPercent(rate.ServiceName, category, false) : rate.GstPercent,
                    TaxType = rate.GstPercent <= 0 ? "Nil Rated" : "Taxable",
                    Notes = rate.Notes,
                    IsStockItem = false
                });
            }

            AddFallbackCatalogItems(items);
            _invoiceCatalog = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Description))
                .GroupBy(i => i.Description.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(i => i.Source)
                .ThenBy(i => i.Description)
                .ToList();
        }

        private void AddFallbackCatalogItems(List<InvoiceCatalogItem> items)
        {
            AddFallbackCatalogItem(items, "Materials", "Split AC Indoor Unit", "Material", "8415", "Nos", 0m, 18m);
            AddFallbackCatalogItem(items, "Materials", "Split AC Outdoor Unit", "Material", "8415", "Nos", 0m, 18m);
            AddFallbackCatalogItem(items, "Materials", "Copper Pipe", "Material", "7411", "Mtr", 0m, 18m);
            AddFallbackCatalogItem(items, "Materials", "Drain Pipe", "Material", "3917", "Mtr", 0m, 18m);
            AddFallbackCatalogItem(items, "Materials", "Insulation", "Material", "4009", "Mtr", 0m, 18m);
            AddFallbackCatalogItem(items, "Spare", "Capacitor replacement", "Spare", "8532", "Nos", 0m, 18m);
            AddFallbackCatalogItem(items, "Spare", "Contactor relay", "Spare", "8536", "Nos", 0m, 18m);
            AddFallbackCatalogItem(items, "Services", "Service charge", "Service", "998719", "Visit", 0m, 18m);
            AddFallbackCatalogItem(items, "Services", "Gas refill / charging", "Service", "998719", "Job", 0m, 18m);
            AddFallbackCatalogItem(items, "Labour", "Installation labour", "Labour", "998519", "Job", 0m, 18m);
            AddFallbackCatalogItem(items, "AMC / Contract", "AMC preventive visit", "AMC", "998719", "Visit", 0m, 18m);
        }

        private static void AddFallbackCatalogItem(List<InvoiceCatalogItem> items, string source, string description, string category, string hsnSac, string unit, decimal rate, decimal gst)
        {
            if (items.Any(i => string.Equals(i.Description, description, StringComparison.OrdinalIgnoreCase)))
                return;
            items.Add(new InvoiceCatalogItem
            {
                Source = source,
                Description = description,
                Category = category,
                HsnSacCode = hsnSac,
                Unit = unit,
                Rate = rate,
                GstPercent = gst,
                TaxType = gst <= 0 ? "Nil Rated" : "Taxable",
                Notes = source
            });
        }

        private string ResolveHsnSac(string description, string category, bool material)
        {
            HsnSacMasterEntry entry = FindHsnSacEntry(description, category, material);
            if (entry != null)
                return entry.Code;
            if (string.Equals(category, "Labour", StringComparison.OrdinalIgnoreCase)) return "998519";
            if (string.Equals(category, "AMC", StringComparison.OrdinalIgnoreCase) || string.Equals(category, "Service", StringComparison.OrdinalIgnoreCase)) return "998719";
            return material ? "8415" : "998719";
        }

        private decimal ResolveGstPercent(string description, string category, bool material)
        {
            HsnSacMasterEntry entry = FindHsnSacEntry(description, category, material);
            return entry == null || entry.TaxRate <= 0 ? 18m : entry.TaxRate;
        }

        private HsnSacMasterEntry FindHsnSacEntry(string description, string category, bool material)
        {
            IEnumerable<HsnSacMasterEntry> entries = (_hsnSacEntries ?? new List<HsnSacMasterEntry>()).Where(e => e.IsActive);
            string probe = ((description ?? string.Empty) + " " + (category ?? string.Empty)).ToLowerInvariant();
            HsnSacMasterEntry match = entries.FirstOrDefault(e =>
                (!string.IsNullOrWhiteSpace(e.BusinessCategory) && probe.Contains(e.BusinessCategory.ToLowerInvariant())) ||
                (!string.IsNullOrWhiteSpace(FirstWord(e.Description)) && probe.Contains(FirstWord(e.Description).ToLowerInvariant())));
            if (match != null)
                return match;
            return entries.FirstOrDefault(e => string.Equals(e.CodeType, material ? "HSN" : "SAC", StringComparison.OrdinalIgnoreCase) && e.IsDefault)
                ?? entries.FirstOrDefault(e => string.Equals(e.CodeType, material ? "HSN" : "SAC", StringComparison.OrdinalIgnoreCase));
        }

        private static string FirstWord(string value)
        {
            return (value ?? string.Empty).Split(new[] { ' ', ',', '/', '-' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        }

        private static string NormalizeItemCategory(string category, string fallback)
        {
            string value = (category ?? string.Empty).Trim();
            if (value.Length == 0)
                return fallback;
            string probe = value.ToLowerInvariant();
            if (probe.Contains("labour") || probe.Contains("labor")) return "Labour";
            if (probe.Contains("amc") || probe.Contains("contract")) return "AMC";
            if (probe.Contains("service")) return "Service";
            if (probe.Contains("spare")) return "Spare";
            return fallback == "Service" ? "Service" : "Material";
        }

        private static string BuildStockNote(StockItem stock)
        {
            if (stock == null)
                return string.Empty;
            return "Available: " + stock.AvailableStock.ToString("0.###") + " " + (stock.Unit ?? "Nos");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LAYOUT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BuildLayout()
        {
            Controls.Clear();
            BackColor = DS.BgPage;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.White, Padding = new Padding(28, 14, 28, 10) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Label pageTitle = new Label { Text = "Invoices", Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = DS.Slate950, Location = new Point(28, 15), Size = new Size(360, 28), AutoEllipsis = true };
            Label pageSubtitle = new Label { Text = "GST billing, receivables and customer invoice workflow.", Font = new Font("Segoe UI", 8.8f), ForeColor = DS.Slate600, Location = new Point(29, 46), Size = new Size(520, 20), AutoEllipsis = true };
            header.Controls.AddRange(new Control[] { pageTitle, pageSubtitle });

            _btnNewInvoice = MakeBtn("+  New Invoice", InfoBlue, 138);
            _btnNewInvoice.MinimumSize = new Size(110, 0);
            Button btnSettings = MakeBtn("⚙", Color.White, 42); btnSettings.ForeColor = DS.Slate700; btnSettings.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnForms = MakeBtn("Forms", Color.White, 86); btnForms.ForeColor = DS.Primary600; btnForms.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            Button btnImport = MakeBtn("Import", Color.White, 104); btnImport.ForeColor = DS.Slate700; btnImport.FlatAppearance.BorderColor = DS.BorderStrong;

            FlowLayoutPanel actionRail = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.White,
                Size = new Size(500, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            foreach (Button button in new[] { _btnNewInvoice, btnSettings, btnForms, btnImport })
                button.Margin = new Padding(10, 1, 0, 1);

            Action layoutHeaderActions = () =>
            {
                actionRail.Width = Math.Min(500, Math.Max(360, header.Width - 610));
                actionRail.Location = new Point(header.Width - actionRail.Width - 28, 22);
                pageTitle.Width = Math.Max(260, actionRail.Left - 56);
                pageSubtitle.Width = Math.Max(320, actionRail.Left - 56);
            };
            header.Resize += (s, e) => layoutHeaderActions();
            _btnNewInvoice.Click += BtnNew_Click;
            btnSettings.Click += (s, e) => MessageBox.Show("Invoice settings are available from Settings and Templates.", "Invoice Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Invoice Management", "Finance / Payments", null, "invoice payment receipt credit note GST approval customer sign-off");
            btnImport.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(btnImport, ExcelImportModule.Invoices, FindForm());
            actionRail.Controls.AddRange(new Control[] { _btnNewInvoice, btnSettings, btnForms, btnImport });
            header.Controls.Add(actionRail);
            layoutHeaderActions();

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(24, 12, 24, 16), Tag = "NO_CARD_SURFACE" };
            _invoiceDashboardPanel = BuildInvoiceModuleDashboard();
            _invoiceDashboardPanel.Dock = DockStyle.Top;
            _invoiceWorkspacePanel = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Visible = false };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = DS.BgPage,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMargin = new Size(0, 16), BackColor = DS.BgPage, Padding = new Padding(18, 0, 0, 0) };
            rightPanel.MouseEnter += (s, e) => FocusPanelIfNoDropdownOpen(rightPanel);
            Panel footerCard = BuildInvoiceFooterCard();
            Panel summaryCard = BuildInvoiceSummaryCard();
            Panel quickActionsCard = BuildQuickActionsCard();
            rightPanel.Controls.Add(footerCard);
            rightPanel.Controls.Add(summaryCard);
            rightPanel.Controls.Add(quickActionsCard);
            CardResizeGripService.Attach(summaryCard);
            CardResizeGripService.Attach(quickActionsCard);

            _documentHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(16, 16),
                BackColor = DS.BgPage,
                Padding = new Padding(0, 0, 0, 18),
                TabStop = true
            };
            _documentHost.MouseEnter += (s, e) => FocusPanelIfNoDropdownOpen(_documentHost);
            BuildInvoiceDocument(_documentHost);

            layout.Controls.Add(_documentHost, 0, 0);
            layout.Controls.Add(rightPanel, 1, 0);
            _invoiceWorkspacePanel.Controls.Add(layout);
            body.Controls.Add(_invoiceWorkspacePanel);
            body.Controls.Add(_invoiceDashboardPanel);

            _invoiceFlow = new FlowLayoutPanel { Visible = false };
            Controls.Add(_invoiceFlow);
            Controls.Add(body);
            Controls.Add(header);
            ShowInvoiceDashboard();
        }

        private void ShowInvoiceDashboard()
        {
            if (_invoiceDashboardPanel != null)
            {
                _invoiceDashboardPanel.Dock = DockStyle.Fill;
                _invoiceDashboardPanel.Visible = true;
                _invoiceDashboardPanel.BringToFront();
            }
            if (_invoiceWorkspacePanel != null)
                _invoiceWorkspacePanel.Visible = false;
        }

        private void ShowInvoiceEditor()
        {
            if (_invoiceDashboardPanel != null)
                _invoiceDashboardPanel.Visible = false;
            if (_invoiceWorkspacePanel != null)
            {
                _invoiceWorkspacePanel.Visible = true;
                _invoiceWorkspacePanel.BringToFront();
            }
        }

        private Panel BuildInvoiceActionBar()
        {
            Panel actionBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = Color.White,
                Padding = new Padding(20, 12, 20, 12)
            };

            actionBar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, 0, actionBar.Width, 0);
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent
            };

            Button btnBackToDashboard = MakeBtn("<- Back to Dashboard", Color.White, 142);
            btnBackToDashboard.ForeColor = DS.Slate700;
            btnBackToDashboard.FlatAppearance.BorderColor = DS.Border;
            _btnSaveInvoice = MakeBtn("Save Draft", Color.FromArgb(52, 152, 219), 110);
            Button btnFinalise = MakeBtn("Finalise", SaveGreen, 100);
            Button btnPayment = MakeBtn("Record Payment", Color.FromArgb(142, 68, 173), 130);
            Button btnPreview = MakeBtn("Preview", InfoBlue, 100);
            Button btnCompare = MakeBtn("Compare Format", Color.FromArgb(15, 118, 110), 125);

            btnBackToDashboard.Margin = new Padding(0, 0, 10, 0);
            _btnSaveInvoice.Margin = new Padding(0, 0, 10, 0);
            btnFinalise.Margin = new Padding(0, 0, 10, 0);
            btnPayment.Margin = new Padding(0, 0, 10, 0);
            btnPreview.Margin = new Padding(0, 0, 10, 0);
            btnCompare.Margin = new Padding(0);

            btnBackToDashboard.Click += (s, e) => ShowInvoiceDashboard();
            _btnSaveInvoice.Click += BtnSave_Click;
            btnFinalise.Click += BtnFinalise_Click;
            btnPayment.Click += BtnRecordPayment_Click;
            btnPreview.Click += BtnPreview_Click;
            btnCompare.Click += BtnCompare_Click;

            flow.Controls.AddRange(new Control[] { btnBackToDashboard, _btnSaveInvoice, btnFinalise, btnPayment, btnPreview, btnCompare });

            Label hint = new Label
            {
                Text = "Tax summary stays visible above. Actions remain accessible here.",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 360,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleLeft
            };

            actionBar.Controls.Add(flow);
            actionBar.Controls.Add(hint);
            return actionBar;
        }

        private Panel BuildInvoiceModuleDashboard()
        {
            DateTime today = DateTime.Today;
            _invoiceDashboardSnapshot = new InvoiceDashboardSnapshot
            {
                DateFrom = new DateTime(today.Year, today.Month, 1),
                DateTo = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)),
                Grouping = InvoiceAnalyticsGrouping.Week
            };

            Panel host = new Panel
            {
                Height = 430,
                BackColor = DS.BgPage,
                Padding = new Padding(0, 0, 0, 12)
            };
            host.Resize += (s, e) => LayoutInvoiceDashboard(host);

            _invoiceDashFromPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = _invoiceDashboardSnapshot.DateFrom, Width = 126, Tag = "dash-filter" };
            _invoiceDashToPicker = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = _invoiceDashboardSnapshot.DateTo, Width = 126, Tag = "dash-filter" };
            _invoiceDashGroupingCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 86, Tag = "dash-filter" };
            _invoiceDashGroupingCombo.Items.AddRange(new object[] { "Day", "Week", "Month" });
            _invoiceDashGroupingCombo.SelectedItem = _invoiceDashboardSnapshot.Grouping.ToString();
            _invoiceDashFromPicker.ValueChanged += (s, e) => RefreshInvoiceModuleDashboard(host);
            _invoiceDashToPicker.ValueChanged += (s, e) => RefreshInvoiceModuleDashboard(host);
            _invoiceDashGroupingCombo.SelectedIndexChanged += (s, e) => RunAfterDropdownClosed(() => RefreshInvoiceModuleDashboard(host));
            RegisterInvoiceComboBox(_invoiceDashGroupingCombo);

            PopulateInvoiceDashboardCards(host);
            LayoutInvoiceDashboard(host);
            host.HandleCreated += (s, e) => BeginInvoke((Action)(() => RefreshInvoiceModuleDashboard(host)));
            return host;
        }

        private void RefreshInvoiceModuleDashboard(Panel host)
        {
            if (_invoiceDashboardRefreshing || host == null || _invoiceDashFromPicker == null || _invoiceDashToPicker == null)
                return;

            _invoiceDashboardRefreshing = true;
            InvoiceAnalyticsGrouping grouping = InvoiceAnalyticsGrouping.Week;
            string selected = _invoiceDashGroupingCombo?.SelectedItem?.ToString() ?? "Week";
            if (selected.Equals("Day", StringComparison.OrdinalIgnoreCase)) grouping = InvoiceAnalyticsGrouping.Day;
            if (selected.Equals("Month", StringComparison.OrdinalIgnoreCase)) grouping = InvoiceAnalyticsGrouping.Month;
            InvoiceAnalyticsFilter filter = new InvoiceAnalyticsFilter
            {
                DateFrom = _invoiceDashFromPicker.Value.Date,
                DateTo = _invoiceDashToPicker.Value.Date,
                Grouping = grouping
            };

            // BuildSnapshot reloads every invoice and contract from SQL; running it
            // on the UI thread froze the app for 20+ seconds on first open.
            Task.Run(() =>
            {
                InvoiceDashboardSnapshot snapshot = null;
                try { snapshot = _invoiceAnalyticsSvc.BuildSnapshot(filter); }
                catch (Exception ex) { AppRuntime.LogException("InvoiceForm.RefreshInvoiceModuleDashboard", ex); }

                if (IsDisposed || !IsHandleCreated || host.IsDisposed)
                {
                    _invoiceDashboardRefreshing = false;
                    return;
                }

                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (snapshot != null && !host.IsDisposed)
                            {
                                _invoiceDashboardSnapshot = snapshot;
                                foreach (Control child in host.Controls.Cast<Control>().Where(c => Convert.ToString(c.Tag) == "dash-card").ToList())
                                    host.Controls.Remove(child);
                                PopulateInvoiceDashboardCards(host);
                                LayoutInvoiceDashboard(host);
                            }
                        }
                        finally
                        {
                            _invoiceDashboardRefreshing = false;
                        }
                    }));
                }
                catch
                {
                    _invoiceDashboardRefreshing = false;
                }
            });
        }

        private void PopulateInvoiceDashboardCards(Panel host)
        {
            InvoiceKpi[] kpis =
            {
                _invoiceDashboardSnapshot.Kpis.TotalInvoices,
                _invoiceDashboardSnapshot.Kpis.TotalAmount,
                _invoiceDashboardSnapshot.Kpis.PaidAmount,
                _invoiceDashboardSnapshot.Kpis.PendingAmount,
                _invoiceDashboardSnapshot.Kpis.OverdueAmount
            };
            Color[] accents = { DS.Primary600, DS.Green600, DS.Teal600, DS.Amber500, DS.Red600 };
            string[] icons = { "INV", "Rs", "OK", "P", "!" };
            for (int i = 0; i < kpis.Length; i++)
                host.Controls.Add(BuildInvoiceDashKpiCard(kpis[i], accents[i], icons[i], i == 0, "invoiceDashKpi" + i));

            Panel overview = MakeInvoiceDashCard();
            overview.Name = "invoiceDashOverview";
            overview.Tag = "dash-card";
            overview.Controls.Add(new Label { Text = "Invoice Overview", Location = new Point(16, 12), Size = new Size(180, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            overview.Controls.Add(new InvoiceOverviewChart { Snapshot = _invoiceDashboardSnapshot, Location = new Point(12, 42), Size = new Size(520, 170), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom });
            host.Controls.Add(overview);

            Panel status = MakeInvoiceDashCard();
            status.Name = "invoiceDashDistribution";
            status.Tag = "dash-card";
            status.Controls.Add(new Label { Text = "Invoices by Status", Location = new Point(16, 12), Size = new Size(180, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            status.Controls.Add(new InvoiceStatusDonut { Snapshot = _invoiceDashboardSnapshot, Location = new Point(10, 42), Size = new Size(390, 170), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom });
            host.Controls.Add(status);

            Panel recent = MakeInvoiceDashCard();
            recent.Name = "invoiceDashRecent";
            recent.Tag = "dash-card";
            recent.Controls.Add(new Label { Text = "Recent Invoices", Location = new Point(16, 12), Size = new Size(180, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            DataGridView grid = BuildInvoiceDashRecentGrid();
            recent.Controls.Add(grid);
            host.Controls.Add(recent);

            Panel side = MakeInvoiceDashCard();
            side.Name = "invoiceDashReceivables";
            side.Tag = "dash-card";
            side.Controls.Add(new Label { Text = "Receivables & Actions", Location = new Point(16, 12), Size = new Size(220, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            decimal overdue = _invoiceDashboardSnapshot.AgingBuckets.Sum(b => b.Amount);
            side.Controls.Add(new Label { Text = "Total overdue", Location = new Point(18, 45), Size = new Size(160, 18), Font = new Font("Segoe UI", 8.25f), ForeColor = DS.Slate600 });
            side.Controls.Add(new Label { Text = IndiaFormatHelper.FormatCurrency(overdue), Location = new Point(18, 66), Size = new Size(220, 28), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = DS.Red600 });
            Button action = MakeBtn("Create Invoice", InfoBlue, 132);
            action.Location = new Point(18, 122);
            action.Click += BtnNew_Click;
            side.Controls.Add(action);
            int y = 168;
            foreach (string reminder in _invoiceDashboardSnapshot.Reminders.Take(3))
            {
                side.Controls.Add(new Label { Text = "• " + reminder, Location = new Point(18, y), Size = new Size(310, 20), Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate700 });
                y += 22;
            }
            host.Controls.Add(side);

            host.Controls.Add(BuildInvoiceWorkflowCard());
        }

        private Panel BuildInvoiceWorkflowCard()
        {
            Panel workflow = MakeInvoiceDashCard();
            workflow.Name = "invoiceDashWorkflow";
            workflow.Tag = "dash-card";
            workflow.Controls.Add(new Label { Text = "Invoice Workflow", Location = new Point(16, 12), Size = new Size(220, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            Control body = SharedUiPrimitives.BuildDirectionalWorkflowLayout(
                "Supplier Workflow",
                "Sent to Suppliers",
                Enumerable.Empty<string>(),
                "Received from Suppliers",
                Enumerable.Empty<string>(),
                "Client Workflow",
                "Sent to Clients",
                BuildInvoiceWorkflowLines(row => !IsInvoicePaid(row.Status)),
                "Received from Clients",
                BuildInvoiceWorkflowLines(row => IsInvoicePaid(row.Status)));
            body.Location = new Point(10, 42);
            body.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            workflow.Controls.Add(body);
            workflow.Resize += (s, e) => body.Size = new Size(Math.Max(120, workflow.ClientSize.Width - 20), Math.Max(120, workflow.ClientSize.Height - 54));
            return workflow;
        }

        private IEnumerable<string> BuildInvoiceWorkflowLines(Func<InvoiceRecentRow, bool> predicate)
        {
            return (_invoiceDashboardSnapshot?.RecentInvoices ?? Enumerable.Empty<InvoiceRecentRow>())
                .Where(predicate)
                .OrderByDescending(row => row.InvoiceDate)
                .Take(4)
                .Select(row => (string.IsNullOrWhiteSpace(row.InvoiceNumber) ? "Invoice" : row.InvoiceNumber) + " - " + (string.IsNullOrWhiteSpace(row.ClientName) ? "Client" : row.ClientName) + " - " + row.Status);
        }

        private static bool IsInvoicePaid(string status)
        {
            string key = (status ?? string.Empty).Trim();
            return key.Equals("Paid", StringComparison.OrdinalIgnoreCase) || key.Equals("Closed", StringComparison.OrdinalIgnoreCase) || key.Equals("Received", StringComparison.OrdinalIgnoreCase);
        }

        private Panel BuildInvoiceDashKpiCard(InvoiceKpi kpi, Color accent, string icon, bool numberOnly, string cardName)
        {
            Panel card = MakeInvoiceDashCard();
            card.Name = cardName;
            card.Tag = "dash-card";
            card.Height = 92;
            card.Controls.Add(new Label { Text = kpi.Title, Location = new Point(14, 14), Size = new Size(160, 18), Font = new Font("Segoe UI", 8.25f, FontStyle.Bold), ForeColor = DS.Slate600 });
            card.Controls.Add(new Label { Text = numberOnly ? ((int)kpi.Value).ToString("N0") : IndiaFormatHelper.FormatCurrency(kpi.Value), Location = new Point(14, 38), Size = new Size(190, 24), Font = new Font("Segoe UI", 12.5f, FontStyle.Bold), ForeColor = DS.Slate900 });
            card.Controls.Add(new Label { Text = (kpi.MonthOverMonthPercent >= 0 ? "+ " : "- ") + Math.Abs(kpi.MonthOverMonthPercent).ToString("0.#") + "% from last period", Location = new Point(14, 66), Size = new Size(180, 18), Font = new Font("Segoe UI", 7.75f, FontStyle.Bold), ForeColor = kpi.MonthOverMonthPercent >= 0 ? DS.Green600 : DS.Red600 });
            Label badge = new Label { Text = icon, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(card.Width - 54, 25), Size = new Size(40, 40), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = DS.Primary50, ForeColor = accent, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            DS.Rounded(badge, 20);
            card.Controls.Add(badge);
            return card;
        }

        private DataGridView BuildInvoiceDashRecentGrid()
        {
            DataGridView grid = new DataGridView
            {
                Location = new Point(0, 42),
                Height = 170,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Cursor = Cursors.Hand,
                ColumnHeadersHeight = 34,
                RowTemplate = { Height = 30 }
            };
            grid.CellClick += InvoiceDashRecentGrid_CellClick;
            grid.CellContentClick += InvoiceDashRecentGrid_CellContentClick;
            grid.CellDoubleClick += InvoiceDashRecentGrid_CellDoubleClick;
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "InvoiceId", HeaderText = "Id", Visible = false });
            grid.Columns.Add("Invoice", "Invoice No.");
            grid.Columns.Add("Client", "Client");
            grid.Columns.Add("Date", "Date");
            grid.Columns.Add("Amount", "Amount");
            grid.Columns.Add("Status", "Status");
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Pdf", HeaderText = "", Text = "PDF", UseColumnTextForButtonValue = true, FillWeight = 45 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "Delete", UseColumnTextForButtonValue = true, FillWeight = 55 });
            foreach (InvoiceRecentRow row in _invoiceDashboardSnapshot.RecentInvoices.Take(5))
                grid.Rows.Add(row.InvoiceId, row.InvoiceNumber, row.ClientName, row.InvoiceDate.ToString("dd/MM/yyyy"), IndiaFormatHelper.FormatCurrency(row.Amount), row.Status);
            GridTheme.Apply(grid);
            grid.Dock = DockStyle.None;
            return grid;
        }

        private void InvoiceDashRecentGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name == "Pdf" || grid.Columns[e.ColumnIndex].Name == "Delete" || GlobalStatusEditor.IsEditableStatusCell(grid, e.RowIndex, e.ColumnIndex))
                return;

            OpenRecentInvoicePdf(grid, e.RowIndex);
        }

        private void InvoiceDashRecentGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            string columnName = grid.Columns[e.ColumnIndex].Name;
            if (columnName == "Pdf")
                OpenRecentInvoicePdf(grid, e.RowIndex);
            else if (columnName == "Delete")
                DeleteRecentInvoice(grid, e.RowIndex);
        }

        private void InvoiceDashRecentGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name == "Pdf" || grid.Columns[e.ColumnIndex].Name == "Delete" || GlobalStatusEditor.IsEditableStatusCell(grid, e.RowIndex, e.ColumnIndex))
                return;
            OpenRecentInvoicePdf(grid, e.RowIndex);
        }

        private void OpenRecentInvoicePdf(DataGridView grid, int rowIndex)
        {
            int invoiceId;
            if (grid == null || rowIndex < 0 || rowIndex >= grid.Rows.Count || !int.TryParse(Convert.ToString(grid.Rows[rowIndex].Cells["InvoiceId"].Value), out invoiceId) || invoiceId <= 0)
                return;
            RecentDocumentOpenService.OpenInvoicePdf(this, invoiceId);
        }

        private void DeleteRecentInvoice(DataGridView grid, int rowIndex)
        {
            int invoiceId;
            if (grid == null || rowIndex < 0 || rowIndex >= grid.Rows.Count || !int.TryParse(Convert.ToString(grid.Rows[rowIndex].Cells["InvoiceId"].Value), out invoiceId) || invoiceId <= 0)
                return;

            Invoice target = _invSvc.GetInvoiceById(invoiceId);
            if (target == null)
            {
                ShowStatus("Invoice not found. Refresh and try again.", Color.Firebrick);
                return;
            }

            DeleteInvoice(target, _current != null && _current.InvoiceID == invoiceId);
        }

        private Panel MakeInvoiceDashCard()
        {
            Panel card = new Panel { BackColor = Color.White, Tag = "dash-card" };
            card.Paint += (s, e) =>
            {
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(card, 8);
            CardResizeGripService.Attach(card, InvoiceDashboardCardResizeComplete, InvoiceDashboardCardResizeChanging);
            return card;
        }

        private void InvoiceDashboardCardResizeChanging(Control card, Size size)
        {
            SaveInvoiceDashboardCardHeight(card, size.Height);
            if (!_invoiceDashboardLayingOut && _invoiceDashboardPanel != null)
                LayoutInvoiceDashboard(_invoiceDashboardPanel);
        }

        private void InvoiceDashboardCardResizeComplete(Control card, Size size)
        {
            SaveInvoiceDashboardCardHeight(card, size.Height);
            if (_invoiceDashboardPanel != null)
                LayoutInvoiceDashboard(_invoiceDashboardPanel);
        }

        private void SaveInvoiceDashboardCardHeight(Control card, int height)
        {
            string key = GetInvoiceDashboardCardKey(card);
            if (!string.IsNullOrWhiteSpace(key))
                _invoiceDashboardCardHeights[key] = Math.Max(96, height);
        }

        private int GetInvoiceDashboardCardHeight(Control card, int defaultHeight)
        {
            string key = GetInvoiceDashboardCardKey(card);
            if (!string.IsNullOrWhiteSpace(key) && _invoiceDashboardCardHeights.TryGetValue(key, out int savedHeight))
                return Math.Max(96, savedHeight);
            return defaultHeight;
        }

        private static string GetInvoiceDashboardCardKey(Control card)
        {
            return card == null ? null : card.Name;
        }

        private void LayoutInvoiceDashboard(Panel host)
        {
            if (host == null || _invoiceDashboardSnapshot == null)
                return;

            bool compact = host.Width < 1100;
            int gap = 10;
            int top = 0;

            Control create = host.Controls.Cast<Control>().FirstOrDefault(c => Convert.ToString(c.Tag) == "dash-create");
            Control refresh = host.Controls.Cast<Control>().FirstOrDefault(c => Convert.ToString(c.Tag) == "dash-refresh");
            if (refresh != null && create != null)
            {
                refresh.Location = new Point(host.Width - refresh.Width, 18);
                create.Location = new Point(refresh.Left - create.Width - 10, 18);
                if (_invoiceDashGroupingCombo != null) _invoiceDashGroupingCombo.Location = new Point(create.Left - _invoiceDashGroupingCombo.Width - 10, 20);
                if (_invoiceDashToPicker != null) _invoiceDashToPicker.Location = new Point(_invoiceDashGroupingCombo.Left - _invoiceDashToPicker.Width - 8, 20);
                if (_invoiceDashFromPicker != null) _invoiceDashFromPicker.Location = new Point(_invoiceDashToPicker.Left - _invoiceDashFromPicker.Width - 8, 20);
            }

            var cards = host.Controls.Cast<Control>().Where(c => Convert.ToString(c.Tag) == "dash-card").ToList();
            _invoiceDashboardLayingOut = true;
            try
            {
                int kpiCols = compact ? 3 : 5;
                int kpiWidth = Math.Max(170, (host.Width - gap * (kpiCols - 1)) / kpiCols);
                int kpiY = top;
                int kpiCount = Math.Min(5, cards.Count);
                for (int rowStart = 0; rowStart < kpiCount; rowStart += kpiCols)
                {
                    int rowCount = Math.Min(kpiCols, kpiCount - rowStart);
                    int rowHeight = 92;
                    for (int i = rowStart; i < rowStart + rowCount; i++)
                        rowHeight = Math.Max(rowHeight, GetInvoiceDashboardCardHeight(cards[i], 92));

                    for (int i = rowStart; i < rowStart + rowCount; i++)
                        cards[i].SetBounds((i - rowStart) * (kpiWidth + gap), kpiY, kpiWidth, rowHeight);
                    kpiY += rowHeight + gap;
                }

                int chartTop = kpiY;
                int leftW = compact ? host.Width : (int)((host.Width - gap) * 0.58);
                int rightW = compact ? host.Width : host.Width - leftW - gap;
                if (cards.Count > 5)
                    cards[5].SetBounds(0, chartTop, leftW, GetInvoiceDashboardCardHeight(cards[5], 232));
                if (cards.Count > 6)
                    cards[6].SetBounds(compact ? 0 : leftW + gap, compact ? cards[5].Bottom + gap : chartTop, rightW, GetInvoiceDashboardCardHeight(cards[6], 232));

                int lowerTop = compact
                    ? (cards.Count > 6 ? cards[6].Bottom + gap : chartTop)
                    : Math.Max(cards.Count > 5 ? cards[5].Bottom : chartTop, cards.Count > 6 ? cards[6].Bottom : chartTop) + gap;
                if (cards.Count > 7)
                    cards[7].SetBounds(0, lowerTop, leftW, GetInvoiceDashboardCardHeight(cards[7], 222));
                if (cards.Count > 8)
                    cards[8].SetBounds(compact ? 0 : leftW + gap, compact ? cards[7].Bottom + gap : lowerTop, rightW, GetInvoiceDashboardCardHeight(cards[8], 222));

                int workflowTop = compact
                    ? (cards.Count > 8 ? cards[8].Bottom + gap : lowerTop)
                    : Math.Max(cards.Count > 7 ? cards[7].Bottom : lowerTop, cards.Count > 8 ? cards[8].Bottom : lowerTop) + gap;
                if (cards.Count > 9)
                    cards[9].SetBounds(0, workflowTop, host.Width, GetInvoiceDashboardCardHeight(cards[9], 300));

                foreach (Panel card in cards.OfType<Panel>())
                {
                    foreach (Control child in card.Controls)
                    {
                        if (CardResizeGripService.IsResizeGrip(child))
                            continue;
                        if (child is InvoiceOverviewChart || child is InvoiceStatusDonut)
                            child.Size = new Size(card.Width - 24, Math.Max(40, card.Height - 54));
                        if (child is DataGridView grid)
                            grid.Size = new Size(card.Width, Math.Max(40, card.Height - 52));
                    }
                }

                int bottom = cards.Count == 0 ? top : cards.Max(c => c.Bottom);
                host.Height = Math.Max(compact ? 1220 : 646, bottom + 18);
            }
            finally
            {
                _invoiceDashboardLayingOut = false;
            }
        }

        private void ApplyInvoicePreviewSkin(Control.ControlCollection controls)
        {
            foreach (Control child in controls.Cast<Control>().ToList())
            {
                if (child is TextBox || child is ComboBox || child is DateTimePicker || child is NumericUpDown)
                {
                    if (!ShouldSkipPreviewWrap(child))
                        WrapPreviewInput(child);
                    continue;
                }

                if (child is Button button)
                    StylePreviewButton(button);

                if (child is GroupBox group)
                {
                    group.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    group.ForeColor = InfoBlue;
                    group.BackColor = Color.White;
                }

                ApplyInvoicePreviewSkin(child.Controls);
            }
        }

        private bool ShouldSkipPreviewWrap(Control input)
        {
            Control parent = input.Parent;
            while (parent != null)
            {
                string tag = parent.Tag as string;
                if (tag == "invoice-content-surface" || tag == "invoice-no-preview-wrap")
                    return true;
                parent = parent.Parent;
            }
            return false;
        }

        private void RestoreModernInvoiceInputStyles(Control.ControlCollection controls)
        {
            foreach (Control child in controls.Cast<Control>().ToList())
            {
                if (ShouldSkipPreviewWrap(child))
                    RestoreInvoiceContentSurfaceControl(child);

                if (child.Parent != null && child.Parent.Tag as string == "invoice-input-host")
                {
                    StyleModernInput(child);
                    child.Margin = Padding.Empty;
                    child.Dock = DockStyle.Fill;
                }

                RestoreModernInvoiceInputStyles(child.Controls);
            }
        }

        private void RestoreInvoiceContentSurfaceControl(Control child)
        {
            if (child is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = child.Parent != null && child.Parent.Tag as string == "invoice-no-preview-wrap"
                    ? child.Parent.BackColor
                    : Color.FromArgb(248, 250, 252);
                textBox.ForeColor = DS.Slate900;
                textBox.Margin = Padding.Empty;
            }

            if (child is ComboBox combo)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = Color.White;
            }

            if (child is NumericUpDown numeric)
            {
                numeric.BorderStyle = BorderStyle.None;
                numeric.BackColor = Color.White;
            }
        }

        private void WrapPreviewInput(Control input)
        {
            Control parent = input.Parent;
            if (parent == null || parent.Tag as string == "invoice-input-host" || input is DataGridView)
                return;

            Rectangle bounds = input.Bounds;
            int index = parent.Controls.GetChildIndex(input);
            parent.Controls.Remove(input);

            Panel host = new Panel
            {
                Tag = "invoice-input-host",
                Location = bounds.Location,
                Size = bounds.Size,
                BackColor = Color.White,
                Margin = input.Margin
            };
            host.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, host.Width - 1, host.Height - 1);
                using (GraphicsPath path = CreateRoundedPath(rect, 4))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };

            input.Location = input is DateTimePicker || input is ComboBox || input is NumericUpDown
                ? new Point(4, Math.Max(1, (host.Height - input.Height) / 2))
                : new Point(8, input is TextBox tb && tb.Multiline ? 6 : Math.Max(2, (host.Height - input.Height) / 2));
            input.Width = Math.Max(24, host.Width - (input.Left * 2));
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            input.Font = new Font("Segoe UI", 8.5f, input.Font.Style);
            input.BackColor = Color.White;
            input.ForeColor = DS.Slate900;

            if (input is TextBox textBox)
                textBox.BorderStyle = BorderStyle.FixedSingle;
            if (input is ComboBox combo)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem += PreviewCombo_DrawItem;
                RegisterInvoiceComboBox(combo);
            }
            if (input is NumericUpDown numeric)
                numeric.BorderStyle = BorderStyle.None;

            host.Controls.Add(input);
            parent.Controls.Add(host);
            parent.Controls.SetChildIndex(host, index);
        }

        private void PreviewCombo_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (!(sender is ComboBox combo) || e.Index < 0)
                return;

            e.DrawBackground();
            string text = combo.Items[e.Index]?.ToString() ?? "";
            Color color = (e.State & DrawItemState.Selected) == DrawItemState.Selected ? Color.White : DS.Slate900;
            TextRenderer.DrawText(e.Graphics, text, combo.Font, e.Bounds, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void ClearComboSelection(ComboBox combo)
        {
            if (combo == null || combo.DropDownStyle == ComboBoxStyle.DropDownList || combo.DroppedDown)
                return;
            combo.SelectionStart = combo.Text.Length;
            combo.SelectionLength = 0;
        }

        private void StylePreviewButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            if (button.BackColor == Color.White)
                button.FlatAppearance.BorderColor = DS.Border;
            DS.Rounded(button, 5);
        }

        private GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Panel CreateInvoiceCard(string title, int height)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = Color.White,
                Padding = new Padding(20),
                Margin = new Padding(0, 0, 0, 12),
                Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE"
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(card, 10);
            card.HandleCreated += (s, e) => CardResizeGripService.Attach(card);
            if (!string.IsNullOrWhiteSpace(title))
            {
                Label titleIcon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 24, DS.Indigo50, InfoBlue, 8);
                titleIcon.Location = new Point(20, 16);
                card.Controls.Add(titleIcon);
                card.Controls.Add(new Label
                {
                    Text = ToInvoiceCardTitle(title),
                    Location = new Point(52, 17),
                    Size = new Size(260, 24),
                    Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                    ForeColor = DS.Slate900,
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }
            return card;
        }

        private string ToInvoiceCardTitle(string title)
        {
            if (string.Equals(title, "QUICK ACTIONS", StringComparison.OrdinalIgnoreCase))
                return "Quick Actions";
            if (string.Equals(title, "INVOICE SUMMARY", StringComparison.OrdinalIgnoreCase))
                return "Invoice Summary";
            return title;
        }

        private Panel BuildQuickActionsCard()
        {
            Panel card = CreateInvoiceCard("QUICK ACTIONS", 190);
            Label quickHint = new Label
            {
                Text = "Save the draft, then open invoice operations when needed.",
                Location = new Point(20, 48),
                Size = new Size(card.Width - 40, 34),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.15f),
                ForeColor = DS.Slate600
            };

            _btnSaveInvoice = MakeSoftAction("Save Draft", InfoBlue);
            _btnSaveInvoice.Location = new Point(20, 88);
            _btnSaveInvoice.Size = new Size(Math.Max(20, card.Width - 40), 38);
            _btnSaveInvoice.Dock = DockStyle.None;
            _btnSaveInvoice.Margin = Padding.Empty;
            _btnSaveInvoice.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _btnSaveInvoice.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            ModernIconSystem.AddButtonIcon(_btnSaveInvoice, ModernIconKind.Save);
            _btnSaveInvoice.Click += BtnSave_Click;

            Button openActions = MakeSoftAction("Open Invoice Actions", DS.Slate700);
            openActions.Location = new Point(20, 134);
            openActions.Size = new Size(Math.Max(20, card.Width - 40), 34);
            openActions.Dock = DockStyle.None;
            openActions.Margin = Padding.Empty;
            openActions.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            openActions.Font = new Font("Segoe UI", 8.4f, FontStyle.Bold);
            ModernIconSystem.AddButtonIcon(openActions, ModernIconKind.ChevronDown);
            openActions.Click += (s, e) => ShowInvoiceQuickActionsMenu(openActions);

            void layoutActions()
            {
                int targetWidth = Math.Max(1, card.ClientSize.Width - 40);
                quickHint.Size = new Size(targetWidth, quickHint.Height);
                _btnSaveInvoice.Width = targetWidth;
                openActions.Width = targetWidth;
            }

            card.Resize += (s, e) => layoutActions();

            card.Controls.Add(_btnSaveInvoice);
            card.Controls.Add(openActions);
            card.Controls.Add(quickHint);
            layoutActions();
            foreach (Label label in card.Controls.OfType<Label>())
                label.BringToFront();
            return card;
        }

        private void ShowInvoiceQuickActionsMenu(Control anchor)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddInvoiceActionMenuItem(menu, "Send for Approval", (s, e) => BtnFinalise_Click(s, e));
            AddInvoiceActionMenuItem(menu, "Generate PDF", (s, e) => BtnPreview_Click(s, e));
            AddInvoiceActionMenuItem(menu, "Email Invoice", (s, e) => EmailInvoiceFromCurrent());
            AddInvoiceActionMenuItem(menu, "Convert to Receipt", (s, e) => BtnRecordPayment_Click(s, e));
            AddInvoiceActionMenuItem(menu, "Create Credit Note", (s, e) => BtnCreateCreditNote_Click(s, e));
            AddInvoiceActionMenuItem(menu, "WhatsApp Reminder", (s, e) => ShowInvoiceWhatsAppAction());
            menu.Items.Add(new ToolStripSeparator());
            AddInvoiceActionMenuItem(menu, "Delete Invoice", (s, e) => DeleteCurrentInvoice());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void AddInvoiceActionMenuItem(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            menu.Items.Add(item);
        }

        private void ShowInvoiceWhatsAppAction()
        {
            Invoice invoice = _current ?? CollectForm();
            string clientName = invoice.ClientName;
            if (string.IsNullOrWhiteSpace(clientName) && _cmbClient != null && _cmbClient.SelectedItem is ComboItem selectedClient)
                clientName = selectedClient.Text;
            if (string.IsNullOrWhiteSpace(clientName))
                clientName = "Customer";

            B2BClient client = _clients.FirstOrDefault(c => c.ClientID == invoice.ClientID);
            string invoiceNo = string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? "the invoice" : invoice.InvoiceNumber;
            string amount = IndiaFormatHelper.FormatCurrency(invoice.TotalAmount > 0 ? invoice.TotalAmount : invoice.BalanceDue);
            string message = "Hi " + clientName + ",\r\n\r\nThis is a reminder for invoice " + invoiceNo + " amount " + amount + " due on " + invoice.DueDate.ToString("dd MMM yyyy") + ". Please arrange payment when possible.\r\n\r\nRegards,\r\nServoERP";

            WhatsAppQuickActionDialog.ShowFor(this, new WhatsAppQuickActionContext
            {
                Module = "Invoices",
                SourceId = invoice.InvoiceID,
                ContactName = clientName,
                Phone = client == null ? string.Empty : client.Phone,
                TemplateType = "Invoice reminder",
                Message = message,
                LinkedRecordType = "Invoice",
                LinkedRecord = invoiceNo,
                LinkedRecordId = invoice.InvoiceID
            });
        }

        private void DeleteCurrentInvoice()
        {
            DeleteInvoice(_current, true);
        }

        private void DeleteInvoice(Invoice target, bool clearIfCurrent)
        {
            if (target == null || target.InvoiceID <= 0)
            {
                ShowStatus("Select a saved invoice to delete.", OrangeCol);
                return;
            }

            string invoiceNo = string.IsNullOrWhiteSpace(target.InvoiceNumber) ? "this invoice" : "invoice " + target.InvoiceNumber;
            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Invoice",
                invoiceNo,
                "Line items, payments, inventory reservations, and record links for this invoice will also be removed.");
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                SetBusy("Deleting invoice...");
                int deletedId = target.InvoiceID;
                _invSvc.DeleteInvoice(deletedId);
                if (clearIfCurrent || (_current != null && _current.InvoiceID == deletedId))
                    ClearForm();
                LoadInvoiceList(true);
                if (_invoiceDashboardPanel != null)
                    RefreshInvoiceModuleDashboard(_invoiceDashboardPanel);
                ShowStatus("Invoice deleted.", DS.Red600);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Deleting invoice", ex);
                ShowStatus("Invoice could not be deleted. Refresh and try again.", Color.Firebrick);
            }
            finally
            {
                SetBusy(null);
            }
        }

        private Button MakeSoftAction(string text, Color accent)
        {
            Button button = MakeBtn(text, DS.Lighten(accent, 0.82f), 120);
            button.Height = 58;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(6, 5, 6, 5);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = new Font("Segoe UI", 8.4f, FontStyle.Bold);
            UIHelper.ApplyActionButton(button);
            return button;
        }

        private Panel BuildInvoiceSummaryCard()
        {
            Panel card = CreateInvoiceCard("INVOICE SUMMARY", 270);
            card.AutoScroll = true;
            card.AutoScrollMargin = new Size(0, 10);
            int y = 48;
            _lblRightSubTotal = AddSummaryRow(card, "Sub Total (Excl. GST)", "₹0.00", ref y, DS.Slate900);
            AddDiscountRow(card, ref y);
            _lblTaxableSummary = AddSummaryRow(card, "Taxable Amount", "₹0.00", ref y, DS.Slate900);
            _lblRightGST = AddSummaryRow(card, "GST (18%)", "₹0.00", ref y, DS.Slate900);
            y += 8;
            AddDividerLine(card, y);
            y += 18;
            _lblRightTotal = AddSummaryRow(card, "Total (Incl. GST)", "₹0.00", ref y, InfoBlue, true);
            _lblAmountPaidSummary = AddSummaryRow(card, "Amount Paid", "₹0.00", ref y, DS.Slate700);
            _lblRightBalance = AddSummaryRow(card, "Balance Due", "₹0.00", ref y, OrangeCol, true);
            _lblCGSTAmt = new Label();
            _lblSGSTAmt = new Label();
            _lblIGSTAmt = new Label();
            _lblRoundOffAmt = new Label();
            return card;
        }

        private void AddDividerLine(Panel card, int y)
        {
            Panel line = new Panel { Location = new Point(20, y), Size = new Size(310, 1), BackColor = DS.Slate200 };
            card.Controls.Add(line);
        }

        private Label AddSummaryRow(Panel card, string label, string value, ref int y, Color color, bool bold = false)
        {
            card.Controls.Add(new Label { Text = label, Location = new Point(20, y), Size = new Size(170, 18), Font = new Font("Segoe UI", 8.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = DS.Slate700 });
            Label val = new Label { Text = value, Location = new Point(190, y), Size = new Size(140, 18), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", bold ? 10f : 8.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color };
            card.Controls.Add(val);
            y += bold ? 28 : 24;
            return val;
        }

        private void AddDiscountRow(Panel card, ref int y)
        {
            card.Controls.Add(new Label { Text = "Discount", Location = new Point(20, y), Size = new Size(120, 18), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700 });
            TextBox discount = new TextBox { Text = "0.00", Location = new Point(230, y - 3), Size = new Size(100, 24), Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Right };
            card.Controls.Add(discount);
            y += 28;
        }

        private Panel BuildInvoiceFooterCard()
        {
            Panel card = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = DS.BgPage };
            _lblStatus = new Label { Text = "Last saved: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm tt"), Dock = DockStyle.Left, Width = 190, Font = new Font("Segoe UI", 8), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleLeft };
            Button refresh = MakeBtn("Refresh", DS.BgPage, 90);
            UIHelper.ApplyButtonStyle(refresh, ButtonRole.Secondary);
            refresh.Dock = DockStyle.Right;
            ModernIconSystem.AddButtonIcon(refresh, ModernIconKind.Refresh);
            refresh.Click += (s, e) => LoadInvoiceList();
            card.Controls.Add(refresh);
            card.Controls.Add(_lblStatus);
            return card;
        }

        private Panel BuildStickyTaxPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 108,
                BackColor = Color.White,
                Padding = new Padding(18, 10, 18, 10)
            };

            panel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
                    e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
                }
            };

            Label badge = new Label
            {
                Text = "GST Summary",
                AutoSize = false,
                Width = 118,
                Height = 28,
                BackColor = SectionBg,
                ForeColor = InfoBlue,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(18, 12)
            };

            panel.Controls.Add(badge);

            _lblSubTotal = MakeStickyValue(panel, "Sub Total", 150, 12);
            _lblGSTAmt = MakeStickyValue(panel, "GST", 310, 12);
            _lblCGSTAmt = MakeStickyValue(panel, "CGST", 470, 12);
            _lblSGSTAmt = MakeStickyValue(panel, "SGST", 630, 12);
            _lblIGSTAmt = MakeStickyValue(panel, "IGST", 790, 12);
            _lblRoundOffAmt = MakeStickyValue(panel, "Round Off", 950, 12);
            _lblTotal = MakeStickyValue(panel, "Grand Total", 1110, 12, true);
            _lblBalance = MakeStickyValue(panel, "Balance Due", 1270, 12, true, OrangeCol);

            Label lblGstMode = new Label { Text = "GST Mode", AutoSize = true, Location = new Point(22, 56), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = DS.Slate500 };
            _cmbGstMode = new ComboBox { Location = new Point(22, 72), Width = 140, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbGstMode.Items.AddRange(new object[] { "IGST", "CGST+SGST" });
            _cmbGstMode.SelectedIndex = 1;
            _cmbGstMode.SelectedIndexChanged += (s, e) => RequestRecalculateSummary();
            RegisterInvoiceComboBox(_cmbGstMode);

            Label lblGstPct = new Label { Text = "Default GST %", AutoSize = true, Location = new Point(178, 56), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = DS.Slate500 };
            _numGST = new NumericUpDown { Location = new Point(178, 72), Width = 92, Font = new Font("Segoe UI", 9), Minimum = 0, Maximum = 28, DecimalPlaces = 1, Value = 18m };
            _numGST.ValueChanged += (s, e) => RecalculateSummary();

            Label lblRound = new Label { Text = "Round Off", AutoSize = true, Location = new Point(286, 56), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = DS.Slate500 };
            _numRoundOff = new NumericUpDown { Location = new Point(286, 72), Width = 110, Font = new Font("Segoe UI", 9), Minimum = -99999, Maximum = 99999, DecimalPlaces = 2, Increment = 0.01m };
            _numRoundOff.ValueChanged += (s, e) => RecalculateSummary();

            panel.Controls.AddRange(new Control[] { lblGstMode, _cmbGstMode, lblGstPct, _numGST, lblRound, _numRoundOff });
            return panel;
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("Invoices", this, _btnNewInvoice, _btnSaveInvoice, null);
        }

        private void BuildInvoiceDocument(Panel container)
        {
            _documentPage = new ModernCard
            {
                Width = Math.Max(720, container.ClientSize.Width - 18),
                Height = 1180,
                BackColor = Color.White,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE"
            };
            container.Controls.Add(_documentPage);
            container.Resize += (s, e) =>
            {
                _documentPage.Width = Math.Max(720, container.ClientSize.Width - 18);
                _documentPage.Left = 0;
                container.AutoScrollPosition = new Point(0, Math.Abs(container.AutoScrollPosition.Y));
                container.AutoScrollMinSize = new Size(0, _documentPage.Height + 24);
            };

            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White, Padding = new Padding(18, 12, 18, 8) };
            Button back = MakeGhostAction("←  Back to Invoices", 132);
            back.Dock = DockStyle.Left;
            back.Click += (s, e) => LoadInvoiceList();
            Label saved = new Label { Text = "Last saved a few seconds ago", Dock = DockStyle.Right, Width = 190, Font = new Font("Segoe UI", 8f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleRight };
            StatusChip chip = new StatusChip { Text = "DRAFT", Dock = DockStyle.Right, Margin = new Padding(8, 0, 0, 0) };
            topBar.Controls.Add(back);
            topBar.Controls.Add(saved);
            topBar.Controls.Add(chip);

            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20, 12, 20, 18),
                Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE"
            };

            Panel titleRow = new Panel { Height = 36, BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
            Label titleIcon = ModernIconSystem.Badge(ModernIconKind.Invoice, 26, Color.FromArgb(239, 246, 255), InfoBlue, 10);
            titleIcon.Location = new Point(0, 5);
            titleRow.Controls.Add(titleIcon);
            titleRow.Controls.Add(new Label { Text = "Invoice Details", Location = new Point(34, 3), Size = new Size(240, 24), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = DS.Slate900 });

            TableLayoutPanel grid = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 7,
                Height = 432,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(0),
                Tag = "invoice-responsive-width"
            };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            int[] rowHeights = { 58, 58, 58, 56, 56, 56, 90 };
            foreach (int rowHeight in rowHeights)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));

            CreateInvoiceControls();
            AddModernField(grid, "Invoice Number", _txtInvNo, 0, 0, false, 1);
            AddModernField(grid, "Status", _cmbStatus, 1, 0, false, 1);
            AddModernField(grid, "Invoice Date", _dtpInvDate, 2, 0, true, 1);
            AddModernField(grid, "Due Date", _dtpDueDate, 3, 0, true, 1);
            AddModernField(grid, "Client", _cmbClient, 0, 1, true, 2);
            AddModernField(grid, "Site (optional)", _cmbSite, 2, 1, false, 1);
            AddModernField(grid, "Contract (optional)", _cmbContract, 3, 1, false, 1);
            AddModernField(grid, "Use Template", _cmbTemplate, 0, 2, false, 1);
            AddModernField(grid, "Coverage", _cmbCoverageType, 1, 2, false, 1);
            AddModernField(grid, "Warranty", _cmbWarrantyStatus, 2, 2, false, 1);
            AddModernField(grid, "Warranty Expiry", _dtpWarrantyExpiry, 3, 2, false, 1);
            AddModernField(grid, "Subject", _txtSubject, 0, 3, false, 4);
            AddModernField(grid, "Payment Terms", _txtPaymentTerms, 0, 4, false, 1);
            AddModernField(grid, "Place of Supply", _txtPlaceOfSupply, 1, 4, false, 1);
            AddModernField(grid, "Next Service Due", _dtpNextServiceDue, 2, 4, false, 1);
            AddModernField(grid, "Workflow Notes", _txtInventorySummary, 3, 4, false, 1);
            AddModernField(grid, "PO Number", _txtPONumber, 0, 5, false, 1);
            AddModernField(grid, "PO Date", _dtpPODate, 1, 5, false, 1);
            AddModernField(grid, "Notes", _txtNotes, 2, 5, false, 2, 2);
            AddModernField(grid, "Send Invoice To", _txtSendInvoiceTo, 0, 6, false, 2);

            _txtNudges = new TextBox { Text = "Required: client and line items. Site, contract, PO, warranty, and service notes can be added later.", ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(30, 64, 175), BackColor = Color.FromArgb(238, 242, 255), Tag = "CUSTOM_INPUT_SHELL" };
            Panel smartBar = new Panel { Height = 28, BackColor = Color.FromArgb(238, 242, 255), Margin = new Padding(0, 0, 0, 12), Padding = new Padding(12, 6, 12, 4), Tag = "invoice-no-preview-wrap" };
            DS.Rounded(smartBar, 8);
            smartBar.Controls.Add(_txtNudges);
            _txtNudges.Dock = DockStyle.Fill;

            BuildHiddenLineGrid();
            Panel workflow = BuildModernWorkflowSection();
            Panel tax = BuildModernTaxSection();
            Panel lineItems = BuildInvoiceLineItemsSection();

            DockInvoiceSection(content, tax);
            DockInvoiceGap(content, 12);
            DockInvoiceSection(content, lineItems);
            DockInvoiceGap(content, 12);
            DockInvoiceSection(content, workflow);
            CardResizeGripService.Attach(workflow, InvoiceWorkflowCardResizeComplete, InvoiceWorkflowCardResizeChanging);
            DockInvoiceGap(content, 12);
            DockInvoiceSection(content, smartBar);
            DockInvoiceGap(content, 8);
            DockInvoiceSection(content, grid);
            DockInvoiceGap(content, 6);
            DockInvoiceSection(content, titleRow);
            _documentPage.Controls.Add(content);
            _documentPage.Controls.Add(topBar);
            container.AutoScrollPosition = new Point(0, 0);
            container.AutoScrollMinSize = new Size(0, _documentPage.Height + 24);
        }

        private void DockInvoiceSection(Panel parent, Control section)
        {
            section.Dock = DockStyle.Top;
            section.Margin = Padding.Empty;
            parent.Controls.Add(section);
        }

        private void DockInvoiceGap(Panel parent, int height)
        {
            parent.Controls.Add(new Panel { Dock = DockStyle.Top, Height = height, BackColor = Color.White });
        }

        private void CreateInvoiceControls()
        {
            _txtInvNo = new TextBox { ReadOnly = true, Text = "(auto-generated)" };
            _cmbStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbStatus.Items.AddRange(new object[] { "Draft", "Pending", "Approved", "Partial", "Paid", "Overdue" });
            _cmbStatus.SelectedIndexChanged += (s, e) => RequestApplyInvoiceEditability();
            _dtpInvDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today };
            _dtpDueDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddDays(30) };
            _cmbClient = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbClient.SelectionChangeCommitted += CmbClient_SelectionChangeCommitted;
            _cmbClient.DropDown += (s, e) => _lastProcessedClientId = GetSelectedComboId(_cmbClient);
            _cmbClient.DropDownClosed += (s, e) => ProcessClientSelectionIfChanged();
            _cmbSite = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbContract = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTemplate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTemplate.SelectionChangeCommitted += CmbTemplate_SelectionChangeCommitted;
            _cmbCoverageType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCoverageType.Items.AddRange(new object[] { "Billable Service", "Comprehensive AMC", "Non-Comprehensive AMC", "Warranty" });
            _cmbWarrantyStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbWarrantyStatus.Items.AddRange(new object[] { "Out of Warranty", "Under Warranty", "Under Contract" });
            _dtpWarrantyExpiry = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddYears(1), ShowCheckBox = true };
            _txtSubject = new TextBox();
            _txtPaymentTerms = new TextBox { Text = "30 Days" };
            _txtPlaceOfSupply = new TextBox { Text = "Maharashtra" };
            _txtPlaceOfSupply.TextChanged += (s, e) => AutoSelectGstModeFromPlaceOfSupply();
            _dtpNextServiceDue = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today.AddMonths(3), ShowCheckBox = true };
            _txtInventorySummary = new TextBox { Multiline = true };
            _txtPONumber = new TextBox();
            _dtpPODate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today };
            _txtNotes = new TextBox { Multiline = true };
            _txtSendInvoiceTo = new TextBox { Multiline = true };

            EnsureTaxControls();
            RegisterInvoiceEditorCombos();
        }

        private void AddModernField(TableLayoutPanel grid, string label, Control input, int column, int row, bool required, int columnSpan, int rowSpan = 1)
        {
            Panel field = BuildModernField(label, input, required);
            grid.Controls.Add(field, column, row);
            if (columnSpan > 1)
                grid.SetColumnSpan(field, columnSpan);
            if (rowSpan > 1)
                grid.SetRowSpan(field, rowSpan);
        }

        private Panel BuildModernField(string label, Control input, bool required)
        {
            Panel field = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 0, 14, 10) };
            Label lbl = new Label
            {
                Text = required ? label + " *" : label,
                Dock = DockStyle.Top,
                Height = 19,
                Font = new Font("Segoe UI", 7.9f, FontStyle.Bold),
                ForeColor = required ? Color.FromArgb(185, 28, 28) : DS.Slate700
            };
            Panel host = new Panel { Tag = "invoice-input-host", Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10, 5, 9, 5) };
            host.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = ModernCard.RoundedRect(new Rectangle(0, 0, host.Width - 1, host.Height - 1), 6))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(host, 6);
            StyleModernInput(input);
            input.Dock = DockStyle.Fill;
            host.Controls.Add(input);
            field.Controls.Add(host);
            field.Controls.Add(lbl);
            return field;
        }

        private void StyleModernInput(Control input)
        {
            input.Font = new Font("Segoe UI", 8.5f);
            input.ForeColor = DS.Slate900;
            input.BackColor = SystemColors.Window;
            if (input is TextBox textBox)
                textBox.BorderStyle = BorderStyle.FixedSingle;
            if (input is ComboBox combo)
            {
                combo.FlatStyle = FlatStyle.Flat;
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                RegisterInvoiceComboBox(combo);
            }
            if (input is NumericUpDown numeric)
                numeric.BorderStyle = BorderStyle.None;
        }

        /// <summary>
        /// Keeps invoice editor inputs visually editable after shared polish passes.
        /// </summary>
        private void RestoreInvoiceEditorInputState()
        {
            foreach (Control input in GetInvoiceEditorInputs())
            {
                if (input == null || input.IsDisposed)
                    continue;

                input.Enabled = true;
                input.BackColor = SystemColors.Window;
                input.ForeColor = DS.Slate900;

                if (input is TextBox textBox)
                {
                    textBox.BackColor = SystemColors.Window;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    if (!ReferenceEquals(textBox, _txtInvNo))
                        textBox.ReadOnly = false;
                }
                else if (input is ComboBox combo)
                {
                    combo.BackColor = SystemColors.Window;
                    combo.FlatStyle = FlatStyle.Flat;
                }
                else if (input is DateTimePicker picker)
                {
                    picker.CalendarMonthBackground = SystemColors.Window;
                    picker.CalendarTitleBackColor = SystemColors.Window;
                    picker.CalendarForeColor = DS.Slate900;
                    picker.CalendarTitleForeColor = DS.Slate900;
                }
                else if (input is RichTextBox richText)
                {
                    richText.BackColor = SystemColors.Window;
                    richText.ReadOnly = false;
                    richText.BorderStyle = BorderStyle.FixedSingle;
                }

                Control host = input.Parent;
                if (host != null && host.Tag as string == "invoice-input-host")
                    host.BackColor = SystemColors.Window;
            }

            if (_txtInvNo != null)
            {
                _txtInvNo.ReadOnly = true;
                _txtInvNo.BackColor = SystemColors.Window;
            }
        }

        /// <summary>
        /// Returns only the editable invoice detail controls, excluding summary/actions.
        /// </summary>
        private IEnumerable<Control> GetInvoiceEditorInputs()
        {
            Control[] controls =
            {
                _txtInvNo,
                _cmbStatus,
                _dtpInvDate,
                _dtpDueDate,
                _cmbClient,
                _cmbSite,
                _cmbContract,
                _cmbTemplate,
                _cmbCoverageType,
                _cmbWarrantyStatus,
                _dtpWarrantyExpiry,
                _txtSubject,
                _txtPaymentTerms,
                _txtPlaceOfSupply,
                _dtpNextServiceDue,
                _txtInventorySummary,
                _txtPONumber,
                _dtpPODate,
                _txtNotes,
                _txtSendInvoiceTo
            };

            return controls.Where(control => control != null);
        }

        private Button MakeGhostAction(string text, int width)
        {
            ModernButton button = new ModernButton
            {
                Text = text,
                Width = width,
                BackColor = Color.White,
                ForeColor = DS.Slate700,
                FlatStyle = FlatStyle.Flat
            };
            UIHelper.ApplyActionButton(button, UiActionVariant.Secondary);
            return button;
        }

        private Panel BuildModernWorkflowSection()
        {
            ModernCard card = new ModernCard
            {
                Name = "InvoiceHvacOpsCard",
                Height = 276,
                MinimumSize = new Size(760, 220),
                Padding = new Padding(16),
                Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE"
            };
            _invoiceWorkflowCard = card;
            Panel workflowTitle = BuildInvoiceSectionTitle("HVAC WORKFLOW", ModernIconKind.Service);
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.White,
                Padding = new Padding(0, 6, 0, 0),
                Tag = "invoice-workflow-table"
            };
            _invoiceWorkflowTable = table;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
            _txtChecklist = new TextBox { Visible = false };
            _txtAssetDetails = new TextBox { Visible = false };
            _txtPaymentHistory = new TextBox { Visible = false };
            _gridChecklist = CreateWorkflowGrid("Task");
            _gridAssets = CreateWorkflowGrid("Asset / Equipment");
            _gridPaymentHistory = CreateWorkflowGrid("Payment");
            _gridPaymentHistory.ReadOnly = true;
            _gridPaymentHistory.AllowUserToAddRows = false;
            if (_gridPaymentHistory.Columns["Delete"] != null)
                _gridPaymentHistory.Columns["Delete"].Visible = false;
            Panel checklistCard = BuildMiniInfoCard("Checklist / Tasks", _gridChecklist, ModernIconKind.Checklist, out _btnAddChecklistRow);
            Panel assetCard = BuildMiniInfoCard("Asset / Equipment", _gridAssets, ModernIconKind.Inventory, out _btnAddAssetRow);
            Panel paymentCard = BuildMiniInfoCard("Payment History", _gridPaymentHistory, ModernIconKind.Payment, out _btnRecordWorkflowPayment);
            table.Controls.Add(checklistCard, 0, 0);
            table.Controls.Add(assetCard, 1, 0);
            table.Controls.Add(paymentCard, 2, 0);
            CardResizeGripService.Attach(checklistCard, InvoiceWorkflowMiniCardResizeComplete, InvoiceWorkflowMiniCardResizeChanging);
            CardResizeGripService.Attach(assetCard, InvoiceWorkflowMiniCardResizeComplete, InvoiceWorkflowMiniCardResizeChanging);
            CardResizeGripService.Attach(paymentCard, InvoiceWorkflowMiniCardResizeComplete, InvoiceWorkflowMiniCardResizeChanging);
            _btnAddChecklistRow.Click += (s, e) => AddWorkflowRow(_gridChecklist, "New task");
            _btnAddAssetRow.Click += (s, e) => AddWorkflowRow(_gridAssets, "New equipment");
            _btnRecordWorkflowPayment.Text = "Record";
            _btnRecordWorkflowPayment.Click += BtnRecordPayment_Click;
            card.Controls.Add(table);
            card.Controls.Add(workflowTitle);
            card.Controls.Add(_txtChecklist);
            card.Controls.Add(_txtAssetDetails);
            card.Controls.Add(_txtPaymentHistory);
            card.Resize += (s, e) => LayoutInvoiceWorkflowSection();
            CardResizeGripService.Attach(card, InvoiceWorkflowCardResizeComplete, InvoiceWorkflowCardResizeChanging);
            LayoutInvoiceWorkflowSection();
            return card;
        }

        private DataGridView CreateWorkflowGrid(string textHeader)
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToResizeRows = false,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                Font = new Font("Segoe UI", 8.25f),
                ColumnHeadersHeight = 22,
                RowTemplate = { Height = 24 }
            };
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Text", HeaderText = textHeader, FillWeight = 84, FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "X", UseColumnTextForButtonValue = true, FillWeight = 16 });
            grid.CellContentClick += WorkflowGrid_CellContentClick;
            grid.EditingControlShowing += WorkflowGrid_EditingControlShowing;
            grid.DataError += Grid_DataError;
            GridTheme.Apply(grid);
            return grid;
        }

        private Panel BuildMiniInfoCard(string title, DataGridView body, ModernIconKind iconKind, out Button actionButton)
        {
            Panel panel = new Panel
            {
                Name = "InvoiceWorkflow" + Regex.Replace(title ?? "Card", @"[^A-Za-z0-9]", string.Empty),
                Tag = "NO_DASHBOARD_RESIZE NO_CARD_SURFACE",
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(250, 140),
                MinimumSize = new Size(220, 118),
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(12, 8, 12, 8)
            };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = ModernCard.RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 1, RowCount = 2, Padding = Padding.Empty, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Panel titleRow = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
            Label icon = ModernIconSystem.Icon(iconKind, 14, InfoBlue);
            icon.Dock = DockStyle.Left;
            icon.Width = 18;
            actionButton = MakeGhostAction("+ Add", 58);
            actionButton.Dock = DockStyle.Right;
            actionButton.Height = 22;
            actionButton.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            titleRow.Controls.Add(actionButton);
            titleRow.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7.9f, FontStyle.Bold), ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleLeft });
            titleRow.Controls.Add(icon);
            layout.Controls.Add(titleRow, 0, 0);
            layout.Controls.Add(body, 0, 1);
            panel.Controls.Add(layout);
            CardResizeGripService.Attach(panel, InvoiceWorkflowMiniCardResizeComplete, InvoiceWorkflowMiniCardResizeChanging);
            return panel;
        }

        /// <summary>Keeps the HVAC workflow card and inner mini cards usable while they are resized.</summary>
        private void LayoutInvoiceWorkflowSection()
        {
            if (_invoiceWorkflowCard == null || _invoiceWorkflowCard.IsDisposed || _invoiceWorkflowTable == null || _invoiceWorkflowTable.IsDisposed)
                return;

            int tableHeight = Math.Max(96, _invoiceWorkflowCard.ClientSize.Height - _invoiceWorkflowCard.Padding.Vertical - 30);
            _invoiceWorkflowTable.Height = tableHeight;

            foreach (Panel miniCard in _invoiceWorkflowTable.Controls.OfType<Panel>())
            {
                if (CardResizeGripService.IsResizeGrip(miniCard))
                    continue;

                int cellWidth = Math.Max(220, (_invoiceWorkflowTable.ClientSize.Width - _invoiceWorkflowTable.Padding.Horizontal - 36) / 3);
                int preferredHeight = CardResizeGripService.PreferredHeight(miniCard, Math.Max(118, tableHeight - 8));
                miniCard.Width = cellWidth;
                miniCard.Height = Math.Min(Math.Max(118, preferredHeight), Math.Max(118, tableHeight - 2));
            }

            ResizeDocumentPageForInvoiceContent();
        }

        /// <summary>Refreshes the invoice document page scroll bounds after a workflow resize.</summary>
        private void ResizeDocumentPageForInvoiceContent()
        {
            if (_documentPage == null || _documentPage.IsDisposed)
                return;

            int bottom = 0;
            foreach (Control child in _documentPage.Controls)
                bottom = Math.Max(bottom, child.Bottom);

            _documentPage.Height = Math.Max(_documentPage.Height, bottom + 16);
            ScrollableControl scrollHost = _documentPage.Parent as ScrollableControl;
            if (scrollHost != null)
                scrollHost.AutoScrollMinSize = new Size(0, _documentPage.Height + 24);
        }

        /// <summary>Applies live sizing while the outer HVAC workflow card is dragged.</summary>
        private void InvoiceWorkflowCardResizeChanging(Control card, Size size)
        {
            if (card != null)
                card.Height = Math.Max(174, size.Height);
            LayoutInvoiceWorkflowSection();
        }

        /// <summary>Finalizes the outer HVAC workflow card size after resize.</summary>
        private void InvoiceWorkflowCardResizeComplete(Control card, Size size)
        {
            InvoiceWorkflowCardResizeChanging(card, size);
        }

        /// <summary>Applies live sizing while an inner HVAC workflow mini card is dragged.</summary>
        private void InvoiceWorkflowMiniCardResizeChanging(Control card, Size size)
        {
            if (card != null)
                card.Height = Math.Max(88, size.Height);

            int requiredCardHeight = RequiredInvoiceWorkflowHeight();
            if (_invoiceWorkflowCard != null && !_invoiceWorkflowCard.IsDisposed && requiredCardHeight > _invoiceWorkflowCard.Height)
                _invoiceWorkflowCard.Height = requiredCardHeight;

            LayoutInvoiceWorkflowSection();
        }

        /// <summary>Finalizes an inner HVAC workflow mini card size after resize.</summary>
        private void InvoiceWorkflowMiniCardResizeComplete(Control card, Size size)
        {
            InvoiceWorkflowMiniCardResizeChanging(card, size);
        }

        /// <summary>Returns the outer workflow height needed to fit the tallest inner card.</summary>
        private int RequiredInvoiceWorkflowHeight()
        {
            if (_invoiceWorkflowTable == null || _invoiceWorkflowTable.IsDisposed)
                return 232;

            int maxMiniHeight = _invoiceWorkflowTable.Controls.OfType<Panel>()
                .Where(panel => !CardResizeGripService.IsResizeGrip(panel))
                .Select(panel => CardResizeGripService.PreferredHeight(panel, panel.Height))
                .DefaultIfEmpty(112)
                .Max();
            return Math.Max(174, maxMiniHeight + 58);
        }

        private Panel BuildModernTaxSection()
        {
            ModernCard card = new ModernCard { Height = 196, Padding = new Padding(16), Tag = "invoice-no-preview-wrap NO_DASHBOARD_RESIZE NO_CARD_SURFACE" };
            Panel title = BuildInvoiceSectionTitle("TAX SUMMARY (GST)", ModernIconKind.Tax);

            FlowLayoutPanel metrics = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 66, WrapContents = true, BackColor = Color.White, Padding = new Padding(0, 0, 0, 0) };
            _lblSubTotal = AddModernTaxBox(metrics, "Sub Total", "₹0.00", DS.Slate900);
            _lblCGSTAmt = AddModernTaxBox(metrics, "CGST (18%)", "₹0.00", DS.Slate900);
            _lblSGSTAmt = AddModernTaxBox(metrics, "SGST (18%)", "₹0.00", DS.Slate900);
            _lblIGSTAmt = AddModernTaxBox(metrics, "IGST (18%)", "₹0.00", DS.Slate900);
            _lblRoundOffAmt = AddModernTaxBox(metrics, "Round Off", "₹0.00", DS.Slate900);
            _lblTotal = AddModernTaxBox(metrics, "Grand Total", "₹0.00", InfoBlue);
            _lblBalance = AddModernTaxBox(metrics, "Balance Due", "₹0.00", OrangeCol);

            TableLayoutPanel controls = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 6, BackColor = Color.White, Padding = new Padding(0, 6, 0, 0) };
            for (int i = 0; i < 6; i++)
                controls.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.Absolute : SizeType.Percent, i % 2 == 0 ? 96 : 33f));
            controls.Controls.Add(new Label { Text = "GST Mode", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0) }, 0, 0);
            controls.Controls.Add(BuildTaxInputHost(_cmbGstMode), 1, 0);
            controls.Controls.Add(new Label { Text = "Default GST %", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0) }, 2, 0);
            controls.Controls.Add(BuildTaxInputHost(_numGST), 3, 0);
            controls.Controls.Add(new Label { Text = "Round Off", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0) }, 4, 0);
            controls.Controls.Add(BuildTaxInputHost(_numRoundOff), 5, 0);

            card.Controls.Add(controls);
            card.Controls.Add(metrics);
            card.Controls.Add(title);
            return card;
        }

        private Panel BuildInvoiceLineItemsSection()
        {
            ModernCard card = new ModernCard
            {
                Height = 430,
                MinimumSize = new Size(760, 220),
                Padding = new Padding(16),
                Tag = "invoice-no-preview-wrap NO_DASHBOARD_RESIZE NO_CARD_SURFACE"
            };

            Panel title = BuildInvoiceSectionTitle("INVOICE ITEMS", ModernIconKind.Inventory);

            _itemSourceTabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, BackColor = Color.White, WrapContents = false, Padding = new Padding(0, 3, 0, 2) };
            foreach (string source in new[] { "All", "Materials", "Services", "Labour", "AMC / Contract", "Custom Item" })
                _itemSourceTabs.Controls.Add(MakeItemSourceTab(source));

            Panel actionRow = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White, Padding = new Padding(0, 4, 0, 4) };
            FlowLayoutPanel addLineButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 32, BackColor = Color.White, WrapContents = false };
            addLineButtons.FlowDirection = FlowDirection.LeftToRight;

            _btnAddInvoiceLine = MakeGhostAction("+ Add Material", 116);
            _btnAddInvoiceLine.Dock = DockStyle.Left;
            _btnAddInvoiceLine.Click += (s, e) => AddLineRowForSource("Materials");
            Button addService = MakeGhostAction("+ Add Service", 112);
            addService.Dock = DockStyle.Left;
            addService.Click += (s, e) => AddLineRowForSource("Services");
            Button addLabour = MakeGhostAction("+ Add Labour", 110);
            addLabour.Dock = DockStyle.Left;
            addLabour.Click += (s, e) => AddLineRowForSource("Labour");
            Button addCustom = MakeGhostAction("+ Add Custom Item", 140);
            addCustom.Dock = DockStyle.Left;
            addCustom.Click += (s, e) => AddLineRowForSource("Custom Item");

            _btnAddInvoiceLine.Margin = new Padding(0, 0, 10, 0);
            addService.Margin = new Padding(0, 0, 10, 0);
            addLabour.Margin = new Padding(0, 0, 10, 0);
            addCustom.Margin = new Padding(0, 0, 10, 0);

            addLineButtons.Controls.Add(_btnAddInvoiceLine);
            addLineButtons.Controls.Add(addService);
            addLineButtons.Controls.Add(addLabour);
            addLineButtons.Controls.Add(addCustom);
            actionRow.Controls.Add(addLineButtons);

            if (_grid != null)
            {
                _grid.Dock = DockStyle.Fill;
                _grid.Visible = true;
                card.Controls.Add(_grid);
            }
            card.Controls.Add(actionRow);
            card.Controls.Add(_itemSourceTabs);
            card.Controls.Add(title);
            RefreshItemSourceTabs();
            return card;
        }

        private Button MakeItemSourceTab(string source)
        {
            Button button = MakeGhostAction(source, source == "AMC / Contract" ? 124 : 96);
            button.Height = 26;
            button.Margin = new Padding(0, 0, 8, 0);
            button.Tag = source;
            button.Click += (s, e) =>
            {
                _activeItemSource = source;
                RefreshItemSourceTabs();
                BindInventoryItems();
                ShowStatus("Invoice item source: " + source, InfoBlue);
            };
            return button;
        }

        private void RefreshItemSourceTabs()
        {
            if (_itemSourceTabs == null)
                return;
            foreach (Button button in _itemSourceTabs.Controls.OfType<Button>())
            {
                bool active = string.Equals(button.Tag?.ToString(), _activeItemSource, StringComparison.OrdinalIgnoreCase);
                button.BackColor = active ? DS.Indigo50 : Color.White;
                button.ForeColor = active ? DS.Indigo600 : DS.Slate700;
            }
        }

        private Panel BuildInvoiceSectionTitle(string text, ModernIconKind iconKind)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.Transparent };
            Label icon = ModernIconSystem.Icon(iconKind, 15, InfoBlue);
            icon.Dock = DockStyle.Left;
            icon.Width = 20;
            row.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.25f, FontStyle.Bold), ForeColor = InfoBlue, TextAlign = ContentAlignment.MiddleLeft });
            row.Controls.Add(icon);
            return row;
        }

        private Label AddModernTaxBox(FlowLayoutPanel parent, string title, string value, Color color)
        {
            TaxSummaryBox box = new TaxSummaryBox(title, value, color) { Margin = new Padding(0, 0, 12, 0) };
            parent.Controls.Add(box);
            return box.ValueLabel;
        }

        private Panel BuildTaxInputHost(Control input)
        {
            Panel host = new Panel { Tag = "invoice-input-host", Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8, 5, 8, 5), Margin = new Padding(0, 0, 14, 0) };
            host.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = ModernCard.RoundedRect(new Rectangle(0, 0, host.Width - 1, host.Height - 1), 6))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            StyleModernInput(input);
            input.Dock = DockStyle.Fill;
            host.Controls.Add(input);
            return host;
        }

        private void BuildHiddenLineGrid()
        {
            _grid = new DataGridView
            {
                Visible = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Description", HeaderText = "Description", FlatStyle = FlatStyle.Flat, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HSNCode", HeaderText = "HSN/SAC" });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Category", HeaderText = "Category", FlatStyle = FlatStyle.Flat });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Unit", HeaderText = "Unit", FlatStyle = FlatStyle.Flat });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Rate (INR)" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DiscountPercent", HeaderText = "Discount %" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "GSTPercent", HeaderText = "GST %" });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "TaxType", HeaderText = "Tax Type", FlatStyle = FlatStyle.Flat });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount (INR)" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CoverageNote", HeaderText = "Notes" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StockItemID", HeaderText = "StockItemID", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsStockItem", HeaderText = "IsStockItem", Visible = false });
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Text = "Delete", UseColumnTextForButtonValue = true });
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Duplicate", HeaderText = "", Text = "Copy", UseColumnTextForButtonValue = true });
            SeedLineGridCombos();
            GridTheme.ApplyColumnPolicy(_grid, new[]
            {
                new GridColumnPolicy("Description", 220, GridColumnPriority.Required),
                new GridColumnPolicy("HSNCode", 90, GridColumnPriority.Secondary),
                new GridColumnPolicy("Category", 96, GridColumnPriority.Required),
                new GridColumnPolicy("Unit", 64, GridColumnPriority.Secondary),
                new GridColumnPolicy("Quantity", 70, GridColumnPriority.Required),
                new GridColumnPolicy("Rate", 100, GridColumnPriority.Required),
                new GridColumnPolicy("DiscountPercent", 82, GridColumnPriority.Required),
                new GridColumnPolicy("GSTPercent", 78, GridColumnPriority.Secondary),
                new GridColumnPolicy("TaxType", 92, GridColumnPriority.Secondary),
                new GridColumnPolicy("Amount", 120, GridColumnPriority.Required),
                new GridColumnPolicy("CoverageNote", 180, GridColumnPriority.Optional),
                new GridColumnPolicy("Delete", 72, GridColumnPriority.Required),
                new GridColumnPolicy("Duplicate", 72, GridColumnPriority.Required)
            });
            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.CellValueChanged += (s, e) => { if (!_updating && e.RowIndex >= 0) RecalculateLineRow(_grid.Rows[e.RowIndex]); };
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellValidating += Grid_CellValidating;
            _grid.EditingControlShowing += Grid_EditingControlShowing;
            _grid.DataError += Grid_DataError;
            GridTheme.Apply(_grid);
        }

        private void SeedLineGridCombos()
        {
            EnsureComboItems("Category", new[] { "Material", "Service", "Labour", "AMC", "Spare", "Custom" });
            EnsureComboItems("Unit", new[] { "Nos", "Mtr", "Kg", "Ltr", "Job", "Visit", "Hour", "Day", "Set", "Lot" });
            EnsureComboItems("TaxType", new[] { "Taxable", "Nil Rated", "Exempt", "Out of Scope" });
        }

        private void EnsureTaxControls()
        {
            if (_cmbGstMode == null)
            {
                _cmbGstMode = new ComboBox { Font = new Font("Segoe UI", 8.5f), DropDownStyle = ComboBoxStyle.DropDownList };
                _cmbGstMode.Items.AddRange(new object[] { "IGST", "CGST+SGST" });
                _cmbGstMode.SelectedIndex = 1;
                _cmbGstMode.SelectedIndexChanged += (s, e) => RequestRecalculateSummary();
                RegisterInvoiceComboBox(_cmbGstMode);
            }
            if (_numGST == null)
            {
                _numGST = new NumericUpDown { Font = new Font("Segoe UI", 8.5f), Minimum = 0, Maximum = 28, DecimalPlaces = 2, Value = 18m, BorderStyle = BorderStyle.FixedSingle };
                _numGST.ValueChanged += (s, e) => RecalculateSummary();
            }
            if (_numRoundOff == null)
            {
                _numRoundOff = new NumericUpDown { Font = new Font("Segoe UI", 8.5f), Minimum = -99999, Maximum = 99999, DecimalPlaces = 2, Increment = 0.01m, BorderStyle = BorderStyle.FixedSingle };
                _numRoundOff.ValueChanged += (s, e) => RecalculateSummary();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DATA LOAD
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void LoadClientDropdowns()
        {
            if (_cmbClient != null && _cmbClient.DroppedDown)
            {
                RunAfterDropdownClosed(LoadClientDropdowns);
                return;
            }

            _cmbClient.Items.Clear();
            _cmbClient.Items.Add(new ComboItem { Id = 0, Text = "-- Select Client --" });
            foreach (B2BClient c in _clients)
                _cmbClient.Items.Add(new ComboItem { Id = c.ClientID, Text = c.CompanyName });
            UIHelper.ShowEmptyClientsMessageIfNeeded(FindForm(), _clients, "InvoiceForm.LoadClientDropdowns");
            if (_cmbClient.Items.Count > 0) _cmbClient.SelectedIndex = 0;
        }

        private void BindTemplateDropdown()
        {
            if (_cmbTemplate == null)
                return;
            if (_cmbTemplate.DroppedDown)
            {
                RunAfterDropdownClosed(BindTemplateDropdown);
                return;
            }

            _cmbTemplate.Items.Clear();
            _cmbTemplate.Items.Add(new ComboItem { Id = 0, Text = "-- Select Template --" });
            foreach (InvoiceTemplate template in _templates.OrderBy(t => t.TemplateName))
                _cmbTemplate.Items.Add(new ComboItem { Id = template.TemplateID, Text = template.TemplateName, Tag = template.TemplateCode });
            _cmbTemplate.SelectedIndex = 0;
        }

        private void LoadContractDropdowns(int clientId)
        {
            if (_cmbContract != null && _cmbContract.DroppedDown)
            {
                RunAfterDropdownClosed(() => LoadContractDropdowns(clientId));
                return;
            }

            _cmbContract.Items.Clear();
            _cmbContract.Items.Add(new ComboItem { Id = 0, Text = "-- No Contract --" });
            if (clientId <= 0) { _cmbContract.SelectedIndex = 0; return; }
            try
            {
                foreach (AMCContract c in _contractSvc.GetContractsByClient(clientId))
                    _cmbContract.Items.Add(new ComboItem { Id = c.ContractID, Text = "Contract #" + c.ContractID + "  [" + c.ContractStatus + "]" });
            }
            catch { }
            _cmbContract.SelectedIndex = 0;
        }

        private void Grid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_grid.CurrentCell == null)
                return;

            if (e.Control is ComboBox combo)
            {
                UIHelper.ApplyInputStyle(combo);
                string columnName = _grid.Columns[_grid.CurrentCell.ColumnIndex].Name;
                combo.DropDownStyle = columnName == "Description" ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList;
                if (columnName == "Description")
                {
                    combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    combo.AutoCompleteSource = AutoCompleteSource.ListItems;
                }
                else
                {
                    combo.AutoCompleteMode = AutoCompleteMode.None;
                    combo.AutoCompleteSource = AutoCompleteSource.None;
                }
                RegisterInvoiceComboBox(combo);
            }
        }

        private void LoadSiteDropdowns(int clientId)
        {
            if (_cmbSite != null && _cmbSite.DroppedDown)
            {
                RunAfterDropdownClosed(() => LoadSiteDropdowns(clientId));
                return;
            }

            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem { Id = 0, Text = "-- No site / site not decided --" });
            if (clientId > 0)
            {
                try
                {
                    foreach (ClientSite s in _siteSvc.GetByClientId(clientId))
                        _cmbSite.Items.Add(new ComboItem { Id = s.SiteID, Text = SiteService.GetDisplayName(s) });
                }
                catch { }
            }
            _cmbSite.SelectedIndex = 0;
        }

        private void LoadInvoiceList()
        {
            LoadInvoiceList(false);
        }

        private void LoadInvoiceList(bool forceRefresh)
        {
            if (forceRefresh)
                _invoiceListRefreshing = false;
            if (_invoiceListRefreshing)
                return;

            _invoiceListRefreshing = true;
            ShowInvoiceDashboard();
            SetBusy("Refreshing invoices...");

            var worker = CreateWorker();
            worker.DoWork += (s, args) =>
            {
                var invoices = _invSvc.GetAllInvoices()
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.InvoiceID)
                    .Take(120)
                    .ToList();
                Invoice firstDetail = null;
                if (_current == null && invoices.Count > 0)
                    firstDetail = _invSvc.GetInvoiceById(invoices[0].InvoiceID) ?? invoices[0];
                args.Result = new InvoiceListSnapshot { Invoices = invoices, FirstDetail = firstDetail };
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                if (args.Error != null)
                {
                    RunOnUI(() =>
                    {
                        _invoiceListRefreshing = false;
                        SetBusy(null);
                        ShowStatus("Invoices could not load. Refresh and try again.", Color.Red);
                    });
                    ShowError( "Failed to load invoices. Please try again.", args.Error);
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Loading invoices", args.Error);
                    return;
                }
                if (args.Cancelled) return;

                RunOnUI(() =>
                {
                    _invoiceListRefreshing = false;
                    SetBusy(null);
                    InvoiceListSnapshot snapshot = args.Result as InvoiceListSnapshot ?? new InvoiceListSnapshot();
                    BindInvoiceList(snapshot.Invoices, snapshot.FirstDetail);
                });
            };
            worker.RunWorkerAsync();
        }

        private void BindInvoiceList(IEnumerable<Invoice> invoices)
        {
            BindInvoiceList(invoices, null);
        }

        private void BindInvoiceList(IEnumerable<Invoice> invoices, Invoice firstDetail)
        {
            DateTime today = DateTime.Today;
            List<Invoice> invoiceList = (invoices ?? Enumerable.Empty<Invoice>()).ToList();
            try
            {
                UiPerformanceService.WithSuspendedDrawing(_invoiceFlow, () =>
                {
                    _invoiceFlow.Controls.Clear();
                    foreach (Invoice inv in invoiceList)
                    {
                        string status = inv.PaymentStatus ?? "";
                        bool isOverdue = status == "Overdue" ||
                                         (status == "Pending" && inv.DueDate < today);
                        Color statusColor;
                        if (status == "Paid")
                            statusColor = Color.ForestGreen;
                        else if (isOverdue)
                            statusColor = Color.Red;
                        else if (status == "Draft")
                            statusColor = Color.Gray;
                        else if (status == "Pending")
                            statusColor = Color.DarkBlue;
                        else
                            statusColor = Color.DarkBlue;

                        _invoiceFlow.Controls.Add(MakeInvoiceCard(inv, statusColor));
                    }
                });
                if (_current == null)
                {
                    if (invoiceList.Count > 0)
                        PopulateForm(firstDetail ?? invoiceList[0]);
                    else
                        ClearForm();
                }
                ShowStatus(_invoiceFlow.Controls.Count + " invoices.", Color.Gray);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Rendering invoice list", ex);
                ShowStatus("Invoice list could not be shown. Refresh and try again.", Color.Red);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  FORM POPULATION / COLLECT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void SelectInvoice(Invoice invoice, Panel card)
        {
            if (_selectedCard != null)
                HighlightCard(_selectedCard, false);

            _selectedCard = card;
            HighlightCard(card, true);
            _current = _invSvc.GetInvoiceById(invoice.InvoiceID);
            PopulateForm(_current);
            ShowInvoiceEditor();
        }

        private void PopulateForm(Invoice inv)
        {
            _updating = true;
            _txtInvNo.Text    = inv.InvoiceNumber ?? "";
            _dtpInvDate.Value = inv.InvoiceDate == default ? DateTime.Today : inv.InvoiceDate;
            _dtpDueDate.Value = inv.DueDate == default ? DateTime.Today.AddDays(30) : inv.DueDate;
            _txtNotes.Text    = inv.Notes ?? "";
            SelectComboByTag(_cmbTemplate, inv.TemplateCode);
            SelectComboByText(_cmbCoverageType, string.IsNullOrWhiteSpace(inv.ContractCoverageType) ? "Billable Service" : inv.ContractCoverageType);
            SelectComboByText(_cmbWarrantyStatus, string.IsNullOrWhiteSpace(inv.WarrantyStatus) ? "Out of Warranty" : inv.WarrantyStatus);
            _dtpWarrantyExpiry.Checked = inv.WarrantyExpiry.HasValue;
            _dtpWarrantyExpiry.Value = inv.WarrantyExpiry ?? DateTime.Today;
            _txtPaymentTerms.Text = inv.PaymentTerms ?? "30 Days";
            _txtPlaceOfSupply.Text = inv.PlaceOfSupply ?? "Maharashtra";
            _dtpNextServiceDue.Checked = inv.NextServiceDueDate.HasValue;
            _dtpNextServiceDue.Value = inv.NextServiceDueDate ?? DateTime.Today.AddMonths(3);
            SelectComboByText(_cmbGstMode, string.IsNullOrWhiteSpace(inv.GSTMode) ? "IGST" : inv.GSTMode);
            AutoSelectGstModeFromPlaceOfSupply();
            _numRoundOff.Value = inv.RoundOff;
            _txtChecklist.Text = inv.ServiceChecklist ?? "";
            _txtAssetDetails.Text = inv.AssetDetails ?? "";
            PopulateWorkflowGrid(_gridChecklist, inv.ServiceChecklist);
            PopulateWorkflowGrid(_gridAssets, inv.AssetDetails);

            // GST %
            decimal gstPct = inv.GSTPercent > 0 ? inv.GSTPercent : 18m;
            _numGST.Value = Math.Min(Math.Max(gstPct, 0), 28);

            // Status
            int si = _cmbStatus.Items.IndexOf(inv.PaymentStatus ?? "Draft");
            _cmbStatus.SelectedIndex = si >= 0 ? si : 0;

            // Client
            SelectCombo(_cmbClient, inv.ClientID);
            LoadSiteDropdowns(inv.ClientID);
            SelectCombo(_cmbSite, inv.SiteID);
            LoadContractDropdowns(inv.ClientID);
            SelectCombo(_cmbContract, inv.ContractID);

            // Line items
            _grid.Rows.Clear();
            if (inv.LineItems != null)
                foreach (var li in inv.LineItems)
                    AddLineRow(li);

            if (_grid.Rows.Count == 0) AddLineRow();

            _txtSubject.Text = inv.Subject ?? "";
            _txtPONumber.Text = inv.PONumber ?? "";
            _dtpPODate.Value = inv.PODate ?? inv.InvoiceDate;
            _txtSendInvoiceTo.Text = inv.SendInvoiceTo ?? "";
            _txtPaymentHistory.Text = BuildPaymentHistory(inv.InvoiceID);
            PopulatePaymentHistoryGrid(_txtPaymentHistory.Text);
            _txtInventorySummary.Text = _invSvc.GetInventorySummary(inv);
            _txtNudges.Text = _invSvc.GetBehavioralNudges(inv);

            _updating = false;
            RecalculateSummary();
            ApplyInvoiceEditability();
            ShowStatus("Loaded: " + inv.InvoiceNumber, InfoBlue);
        }

        private Invoice CollectForm()
        {
            CommitInvoiceLineGridEdits();
            Invoice inv = _current ?? new Invoice();
            inv.InvoiceDate   = _dtpInvDate.Value.Date;
            inv.DueDate       = _dtpDueDate.Value.Date;
            inv.Notes         = _txtNotes.Text.Trim();
            inv.PaymentStatus = _cmbStatus.SelectedItem?.ToString() ?? "Draft";
            inv.Subject       = _txtSubject.Text.Trim();
            inv.PONumber      = _txtPONumber.Text.Trim();
            inv.PODate        = _dtpPODate.Value.Date;
            inv.SendInvoiceTo = _txtSendInvoiceTo.Text.Trim();
            inv.TemplateCode = (_cmbTemplate.SelectedItem as ComboItem)?.Tag ?? "";
            inv.WorkflowType = (_cmbTemplate.SelectedItem as ComboItem)?.Text ?? "";
            inv.PaymentTerms = _txtPaymentTerms.Text.Trim();
            inv.PlaceOfSupply = _txtPlaceOfSupply.Text.Trim();
            inv.GSTMode = _cmbGstMode.SelectedItem?.ToString() ?? "IGST";
            inv.RoundOff = _numRoundOff.Value;
            inv.ContractCoverageType = _cmbCoverageType.SelectedItem?.ToString() ?? "Billable Service";
            inv.WarrantyStatus = _cmbWarrantyStatus.SelectedItem?.ToString() ?? "Out of Warranty";
            inv.WarrantyExpiry = _dtpWarrantyExpiry.Checked ? _dtpWarrantyExpiry.Value.Date : (DateTime?)null;
            inv.NextServiceDueDate = _dtpNextServiceDue.Checked ? _dtpNextServiceDue.Value.Date : (DateTime?)null;
            inv.ServiceChecklist = CollectWorkflowRows(_gridChecklist);
            inv.AssetDetails = CollectWorkflowRows(_gridAssets);
            _txtChecklist.Text = inv.ServiceChecklist;
            _txtAssetDetails.Text = inv.AssetDetails;

            ComboItem ci = (ComboItem)_cmbClient.SelectedItem;
            inv.ClientID = ci?.Id ?? 0;

            ComboItem cs = (ComboItem)_cmbSite.SelectedItem;
            inv.SiteID = cs?.Id ?? 0;

            ComboItem cc = (ComboItem)_cmbContract.SelectedItem;
            inv.ContractID = cc?.Id ?? 0;

            inv.GSTPercent = _numGST.Value;
            inv.LineItems  = CollectLineItems();
            ApplyCurrentTotalsToInvoice(inv);
            return inv;
        }

        private void ApplyCurrentTotalsToInvoice(Invoice inv)
        {
            if (inv == null)
                return;

            decimal sub = 0m;
            decimal tax = 0m;
            foreach (InvoiceLineItem item in inv.LineItems ?? new List<InvoiceLineItem>())
            {
                item.GSTPercent = item.GSTPercent <= 0 ? (_numGST?.Value ?? 18m) : item.GSTPercent;
                item.Quantity = item.Quantity <= 0 ? 1m : item.Quantity;
                item.DiscountPercent = Math.Min(Math.Max(item.DiscountPercent, 0m), 100m);
                decimal gross = Math.Round(item.Quantity * item.Rate, 2);
                item.Amount = item.IsBillable ? Math.Round(gross - (gross * item.DiscountPercent / 100m), 2) : 0m;
                item.TaxAmount = item.IsBillable ? Math.Round(item.Amount * (item.GSTPercent / 100m), 2) : 0m;
                sub += item.Amount;
                tax += item.TaxAmount;
            }

            inv.SubTotal = sub;
            inv.TaxAmount = tax;
            inv.RoundOff = _numRoundOff?.Value ?? inv.RoundOff;
            inv.GSTMode = _cmbGstMode?.SelectedItem?.ToString() ?? inv.GSTMode ?? "IGST";
            if (string.Equals(inv.GSTMode, "CGST+SGST", StringComparison.OrdinalIgnoreCase))
            {
                inv.CGSTAmount = Math.Round(tax / 2m, 2);
                inv.SGSTAmount = tax - inv.CGSTAmount;
                inv.IGSTAmount = 0m;
            }
            else
            {
                inv.CGSTAmount = 0m;
                inv.SGSTAmount = 0m;
                inv.IGSTAmount = tax;
            }
            inv.TotalAmount = inv.SubTotal + inv.TaxAmount + inv.RoundOff;
            inv.BalanceDue = Math.Max(inv.TotalAmount - inv.PaidAmount, 0m);
        }

        private List<InvoiceLineItem> CollectLineItems()
        {
            var list = new List<InvoiceLineItem>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row == null || row.IsNewRow || !IsMeaningfulInvoiceLineRow(row))
                    continue;

                string desc = row.Cells["Description"].Value?.ToString().Trim() ?? "";
                string hsn  = row.Cells["HSNCode"].Value?.ToString().Trim() ?? "";
                string category = row.Cells["Category"].Value?.ToString().Trim() ?? "Service";
                string unit = row.Cells["Unit"].Value?.ToString().Trim() ?? "Nos";
                decimal qty  = TryParseDecimal(row.Cells["Quantity"].Value);
                decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
                decimal discount = TryParseDecimal(row.Cells["DiscountPercent"].Value);
                decimal gst = TryParseDecimal(row.Cells["GSTPercent"].Value);
                string taxType = row.Cells["TaxType"].Value?.ToString().Trim() ?? "Taxable";
                bool isBillable = !string.Equals(taxType, "Out of Scope", StringComparison.OrdinalIgnoreCase);
                decimal gross = Math.Round((qty > 0 ? qty : 1m) * rate, 2);
                decimal amount = isBillable ? Math.Round(gross - (gross * Math.Min(Math.Max(discount, 0m), 100m) / 100m), 2) : 0m;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = ResolveInvoiceLineDescription(category);
                list.Add(new InvoiceLineItem
                {
                    Description = desc,
                    HSNCode     = hsn,
                    Category    = category,
                    Unit        = unit,
                    Quantity    = qty > 0 ? qty : 1,
                    Rate        = rate,
                    DiscountPercent = Math.Min(Math.Max(discount, 0m), 100m),
                    GSTPercent  = gst > 0 ? gst : _numGST.Value,
                    TaxType     = taxType,
                    IsBillable  = isBillable,
                    CoverageNote = row.Cells["CoverageNote"].Value?.ToString().Trim(),
                    StockItemID = TryParseInt(row.Cells["StockItemID"].Value),
                    IsStockItem = string.Equals(row.Cells["IsStockItem"].Value?.ToString(), "1", StringComparison.OrdinalIgnoreCase),
                    Amount      = amount
                });
            }
            return list;
        }

        private void CommitInvoiceLineGridEdits()
        {
            if (_grid == null || _grid.IsDisposed)
                return;

            try
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                if (_grid.IsCurrentCellInEditMode)
                    _grid.EndEdit(DataGridViewDataErrorContexts.Commit);
                BindingContext[_grid.DataSource]?.EndCurrentEdit();
            }
            catch
            {
                // Validation will surface any unresolved row issue to the user.
            }
        }

        private bool IsMeaningfulInvoiceLineRow(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
                return false;

            string desc = row.Cells["Description"].Value?.ToString().Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(desc))
                return true;

            decimal qty = TryParseDecimal(row.Cells["Quantity"].Value);
            decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
            decimal amount = TryParseDecimal(row.Cells["Amount"].Value);
            decimal gst = TryParseDecimal(row.Cells["GSTPercent"].Value);
            string hsn = row.Cells["HSNCode"].Value?.ToString().Trim() ?? "";
            string coverage = row.Cells["CoverageNote"].Value?.ToString().Trim() ?? "";
            return rate > 0m || amount > 0m || qty > 1m || gst > 0m
                || !string.IsNullOrWhiteSpace(hsn)
                || !string.IsNullOrWhiteSpace(coverage);
        }

        private static string ResolveInvoiceLineDescription(string category)
        {
            if (string.Equals(category, "Material", StringComparison.OrdinalIgnoreCase))
                return "Material charges";
            if (string.Equals(category, "Labour", StringComparison.OrdinalIgnoreCase))
                return "Labour charges";
            return "Service charges";
        }

        private void ClearForm()
        {
            _updating = true;
            _current = null;
            _txtInvNo.Text = "(auto-generated)";
            _dtpInvDate.Value = DateTime.Today;
            _dtpDueDate.Value = DateTime.Today.AddDays(30);
            _txtNotes.Text = string.Empty;
            _txtSubject.Text = string.Empty;
            _txtPONumber.Text = string.Empty;
            _dtpPODate.Value = DateTime.Today;
            _txtSendInvoiceTo.Text = string.Empty;
            _txtPaymentTerms.Text = "30 Days";
            _txtPlaceOfSupply.Text = "Maharashtra";
            _txtChecklist.Text = string.Empty;
            _txtAssetDetails.Text = string.Empty;
            _txtPaymentHistory.Text = "No payments recorded yet.";
            PopulateWorkflowGrid(_gridChecklist, string.Empty);
            PopulateWorkflowGrid(_gridAssets, string.Empty);
            PopulatePaymentHistoryGrid("No payments recorded yet.");
            _txtInventorySummary.Text = "No stock reservation yet.";
            if (_cmbTemplate.Items.Count > 0) _cmbTemplate.SelectedIndex = 0;
            if (_cmbCoverageType.Items.Count > 0) _cmbCoverageType.SelectedIndex = 0;
            if (_cmbWarrantyStatus.Items.Count > 0) _cmbWarrantyStatus.SelectedIndex = 0;
            AutoSelectGstModeFromPlaceOfSupply();
            _dtpWarrantyExpiry.Checked = false;
            _dtpNextServiceDue.Checked = false;
            _numRoundOff.Value = 0;
            if (_cmbStatus.Items.Count > 0) _cmbStatus.SelectedIndex = 0;   // Draft
            if (_cmbClient.Items.Count > 0) _cmbClient.SelectedIndex = 0;
            if (_cmbSite.Items.Count > 0) _cmbSite.SelectedIndex = 0;
            if (_cmbContract.Items.Count > 0) _cmbContract.SelectedIndex = 0;
            _numGST.Value = 18m;
            _grid.Rows.Clear();
            AddLineRow();
            RecalculateSummary();
            _txtNudges.Text = "Select a client and invoice line items before saving. Add a site only when the exact service location is known.";
            if (_selectedCard != null)
            {
                HighlightCard(_selectedCard, false);
                _selectedCard = null;
            }
            _updating = false;
            ApplyInvoiceEditability();
        }

        private void PopulateWorkflowGrid(DataGridView grid, string serializedRows)
        {
            if (grid == null)
                return;

            grid.Rows.Clear();
            foreach (string line in SplitWorkflowRows(serializedRows))
            {
                EnsureWorkflowComboValue(grid, line);
                grid.Rows.Add(line);
            }
            if (grid.Rows.Count == 0)
                grid.Rows.Add(string.Empty);
        }

        private void BindWorkflowPickers(IEnumerable<string> checklistSuggestions = null)
        {
            BindWorkflowCombo(_gridChecklist, checklistSuggestions ?? BuildChecklistSuggestions());
            BindWorkflowCombo(_gridAssets, BuildAssetSuggestions());
        }

        private void BindWorkflowCombo(DataGridView grid, IEnumerable<string> suggestions)
        {
            if (grid == null || !(grid.Columns["Text"] is DataGridViewComboBoxColumn combo))
                return;

            combo.Items.Clear();
            object[] values = (suggestions ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .Cast<object>()
                .ToArray();
            combo.Items.AddRange(values);
        }

        private IEnumerable<string> BuildChecklistSuggestions()
        {
            var rows = new List<string>();
            foreach (string jobType in new[] { "PM Visit", "Breakdown", "Installation", "AMC Visit", "Gas Charging", "General" })
            {
                try
                {
                    rows.AddRange(new JobService().GetChecklistTemplates(jobType).Select(t => t.ItemText));
                }
                catch { }
            }
            rows.AddRange(new[]
            {
                "Site Survey Completed",
                "Material Delivered",
                "Installation Completed",
                "Testing & Commissioning Done",
                "Filter cleaning completed",
                "Gas pressure checked",
                "Client sign-off pending"
            });
            return rows;
        }

        private IEnumerable<string> BuildAssetSuggestions()
        {
            var rows = new List<string>();
            foreach (ClientAsset asset in _clientAssets ?? new List<ClientAsset>())
            {
                string label = string.Join(" ", new[] { asset.AssetTag, asset.Brand, asset.ModelNumber, asset.EquipmentType, asset.Capacity }
                    .Where(v => !string.IsNullOrWhiteSpace(v)));
                if (!string.IsNullOrWhiteSpace(label))
                    rows.Add(label);
            }
            rows.AddRange(new[]
            {
                "Daikin VRV IV System",
                "Split AC indoor unit",
                "Split AC outdoor unit",
                "Control panel",
                "Copper pipe run",
                "Ductable unit"
            });
            return rows;
        }

        private IEnumerable<string> SplitWorkflowRows(string serializedRows)
        {
            return (serializedRows ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(row => row.Trim())
                .Where(row => row.Length > 0);
        }

        private string CollectWorkflowRows(DataGridView grid)
        {
            if (grid == null)
                return string.Empty;

            List<string> rows = new List<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                    continue;
                string text = row.Cells["Text"].Value?.ToString().Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    rows.Add(text);
            }
            return string.Join(Environment.NewLine, rows);
        }

        private void AddWorkflowRow(DataGridView grid, string defaultText)
        {
            if (grid == null || IsInvoiceLocked())
                return;

            EnsureWorkflowComboValue(grid, defaultText);
            int index = grid.Rows.Add(defaultText);
            grid.CurrentCell = grid.Rows[index].Cells["Text"];
            grid.BeginEdit(true);
        }

        private void EnsureWorkflowComboValue(DataGridView grid, string value)
        {
            if (grid == null || string.IsNullOrWhiteSpace(value) || !(grid.Columns["Text"] is DataGridViewComboBoxColumn combo))
                return;
            bool exists = combo.Items.Cast<object>().Any(i => string.Equals(i?.ToString(), value, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                combo.Items.Add(value);
        }

        private void WorkflowGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox combo)
            {
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
                RegisterInvoiceComboBox(combo);
            }
        }

        private void WorkflowGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0 || IsInvoiceLocked())
                return;

            if (grid.Columns[e.ColumnIndex].Name == "Delete")
            {
                if (grid.Rows.Count > 1)
                    grid.Rows.RemoveAt(e.RowIndex);
                else
                    grid.Rows[e.RowIndex].Cells["Text"].Value = string.Empty;
            }
        }

        private void PopulatePaymentHistoryGrid(string historyText)
        {
            if (_gridPaymentHistory == null)
                return;

            _gridPaymentHistory.Rows.Clear();
            IEnumerable<string> rows = SplitWorkflowRows(historyText);
            foreach (string row in rows)
                _gridPaymentHistory.Rows.Add(row);
            if (_gridPaymentHistory.Rows.Count == 0)
                _gridPaymentHistory.Rows.Add("No payments recorded yet.");
        }

        private bool IsInvoiceLocked()
        {
            string status = _cmbStatus?.SelectedItem?.ToString() ?? _current?.PaymentStatus ?? "Draft";
            return string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Paid", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyInvoiceEditability()
        {
            if (IsInvoiceDropdownOpen())
            {
                _editabilityPending = true;
                return;
            }

            bool locked = IsInvoiceLocked();
            foreach (DataGridView grid in new[] { _gridChecklist, _gridAssets, _grid })
            {
                if (grid == null)
                    continue;
                grid.ReadOnly = locked;
                grid.AllowUserToDeleteRows = !locked;
            }
            if (_btnAddChecklistRow != null) _btnAddChecklistRow.Enabled = !locked;
            if (_btnAddAssetRow != null) _btnAddAssetRow.Enabled = !locked;
            if (_btnAddInvoiceLine != null) _btnAddInvoiceLine.Enabled = !locked;
            if (_numGST != null) _numGST.Enabled = !locked;
            if (_numRoundOff != null) _numRoundOff.Enabled = !locked;
            if (_cmbGstMode != null) _cmbGstMode.Enabled = !locked;
            RestoreInvoiceEditorInputState();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LINE ITEMS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void AddLineRow()
        {
            AddLineRow(null);
        }

        private void AddLineRowForSource(string source)
        {
            _activeItemSource = source == "Custom Item" ? "Custom Item" : source;
            RefreshItemSourceTabs();
            BindInventoryItems();
            InvoiceCatalogItem pick = null;
            if (!string.Equals(source, "Custom Item", StringComparison.OrdinalIgnoreCase))
                pick = GetCatalogForActiveSource().FirstOrDefault();
            AddLineRow(pick == null ? null : CatalogToLineItem(pick));
            BeginEditDescriptionCell(_grid.Rows.Count - 1);
        }

        private void AddLineRow(InvoiceLineItem item)
        {
            string description = item?.Description ?? "";
            string category = string.IsNullOrWhiteSpace(item?.Category) ? "Service" : item.Category;
            string unit = string.IsNullOrWhiteSpace(item?.Unit) ? "Nos" : item.Unit;
            EnsureComboValue("Description", description);
            EnsureComboValue("Category", category);
            EnsureComboValue("Unit", unit);
            EnsureComboValue("TaxType", string.IsNullOrWhiteSpace(item?.TaxType) ? "Taxable" : item.TaxType);
            _grid.Rows.Add(
                description,
                item?.HSNCode ?? "",
                category,
                unit,
                (item?.Quantity ?? 1m).ToString("G"),
                (item?.Rate ?? 0m).ToString("0.00"),
                (item?.DiscountPercent ?? 0m).ToString("0.##"),
                (item?.GSTPercent ?? (_numGST?.Value ?? 18m)).ToString("0.##"),
                string.IsNullOrWhiteSpace(item?.TaxType) ? "Taxable" : item.TaxType,
                (item?.Amount ?? 0m).ToString("N2"),
                item?.CoverageNote ?? "",
                item?.StockItemID?.ToString() ?? "",
                item?.IsStockItem == true ? "1" : "0");
            RecalculateSummary();
        }

        private InvoiceLineItem CatalogToLineItem(InvoiceCatalogItem item)
        {
            if (item == null)
                return null;

            decimal gst = item.GstPercent <= 0 ? (_numGST?.Value ?? 18m) : item.GstPercent;
            decimal amount = Math.Round(item.Rate, 2);
            return new InvoiceLineItem
            {
                Description = item.Description,
                HSNCode = item.HsnSacCode,
                Category = string.IsNullOrWhiteSpace(item.Category) ? "Service" : item.Category,
                Unit = string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit,
                Quantity = 1m,
                Rate = item.Rate,
                DiscountPercent = 0m,
                GSTPercent = gst,
                TaxType = string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType,
                TaxAmount = string.Equals(item.TaxType, "Taxable", StringComparison.OrdinalIgnoreCase) ? Math.Round(amount * gst / 100m, 2) : 0m,
                IsStockItem = item.IsStockItem,
                StockItemID = item.StockItemId,
                IsBillable = !string.Equals(item.TaxType, "Out of Scope", StringComparison.OrdinalIgnoreCase),
                CoverageNote = item.Notes,
                Amount = amount
            };
        }

        private void BeginEditDescriptionCell(int rowIndex)
        {
            if (_grid == null || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return;

            BeginInvoke((Action)(() =>
            {
                if (_grid == null || _grid.IsDisposed || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                    return;
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells["Description"];
                _grid.BeginEdit(true);
                if (_grid.EditingControl is ComboBox combo)
                    combo.DroppedDown = true;
            }));
        }

        private void AutoSelectGstModeFromPlaceOfSupply()
        {
            if (_cmbGstMode == null || _cmbGstMode.Items.Count == 0)
                return;

            string place = (_txtPlaceOfSupply == null ? string.Empty : _txtPlaceOfSupply.Text) ?? string.Empty;
            bool intraState = place.IndexOf("Maharashtra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              place.IndexOf("MH", StringComparison.OrdinalIgnoreCase) >= 0;
            SelectComboByText(_cmbGstMode, intraState ? "CGST+SGST" : "IGST");
            RecalculateSummary();
        }

        private void RemoveLineRow()
        {
            if (_grid.SelectedRows.Count > 0 && _grid.Rows.Count > 1)
                _grid.Rows.RemoveAt(_grid.SelectedRows[0].Index);
            RecalculateSummary();
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_updating) return;
            DataGridViewRow row = _grid.Rows[e.RowIndex];
            string description = row.Cells["Description"].Value?.ToString() ?? "";
            InvoiceCatalogItem catalog = _invoiceCatalog.FirstOrDefault(i => string.Equals(i.Description, description, StringComparison.OrdinalIgnoreCase));
            if (catalog != null)
                ApplyCatalogItemToRow(row, catalog);
            decimal qty  = TryParseDecimal(row.Cells["Quantity"].Value);
            decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
            string taxType = row.Cells["TaxType"].Value?.ToString() ?? "Taxable";
            bool isBillable = !string.Equals(taxType, "Out of Scope", StringComparison.OrdinalIgnoreCase);
            decimal discount = Math.Min(Math.Max(TryParseDecimal(row.Cells["DiscountPercent"].Value), 0m), 100m);
            decimal gross = Math.Round((qty > 0 ? qty : 1m) * rate, 2);
            _updating = true;
            row.Cells["DiscountPercent"].Value = discount.ToString("0.##");
            row.Cells["Amount"].Value = (isBillable ? Math.Round(gross - (gross * discount / 100m), 2) : 0m).ToString("N2");
            _updating = false;
            RecalculateSummary();
        }

        private void Grid_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_grid == null || e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            string column = _grid.Columns[e.ColumnIndex].Name;
            if (column == "Description" || column == "Category" || column == "Unit" || column == "TaxType")
                EnsureComboValue(column, Convert.ToString(e.FormattedValue));
        }

        private void ApplyCatalogItemToRow(DataGridViewRow row, InvoiceCatalogItem item)
        {
            if (row == null || item == null)
                return;

            _updating = true;
            try
            {
                EnsureComboValue("Description", item.Description);
                EnsureComboValue("Category", item.Category);
                EnsureComboValue("Unit", item.Unit);
                EnsureComboValue("TaxType", item.TaxType);
                row.Cells["Description"].Value = item.Description;
                row.Cells["HSNCode"].Value = item.HsnSacCode;
                row.Cells["Category"].Value = string.IsNullOrWhiteSpace(item.Category) ? "Service" : item.Category;
                row.Cells["Unit"].Value = string.IsNullOrWhiteSpace(item.Unit) ? "Nos" : item.Unit;
                if (TryParseDecimal(row.Cells["Rate"].Value) <= 0)
                    row.Cells["Rate"].Value = item.Rate.ToString("0.00");
                row.Cells["GSTPercent"].Value = (item.GstPercent <= 0 ? (_numGST?.Value ?? 18m) : item.GstPercent).ToString("0.##");
                row.Cells["TaxType"].Value = string.IsNullOrWhiteSpace(item.TaxType) ? "Taxable" : item.TaxType;
                row.Cells["CoverageNote"].Value = item.Notes ?? string.Empty;
                row.Cells["StockItemID"].Value = item.StockItemId?.ToString() ?? string.Empty;
                row.Cells["IsStockItem"].Value = item.IsStockItem ? "1" : "0";
            }
            finally
            {
                _updating = false;
            }
        }

        private void RecalculateLineRow(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow || _updating)
                return;

            decimal qty = TryParseDecimal(row.Cells["Quantity"].Value);
            decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
            decimal discount = Math.Min(Math.Max(TryParseDecimal(row.Cells["DiscountPercent"].Value), 0m), 100m);
            string taxType = row.Cells["TaxType"].Value?.ToString() ?? "Taxable";
            bool isBillable = !string.Equals(taxType, "Out of Scope", StringComparison.OrdinalIgnoreCase);
            decimal gross = Math.Round((qty > 0 ? qty : 1m) * rate, 2);
            row.Cells["Amount"].Value = (isBillable ? Math.Round(gross - (gross * discount / 100m), 2) : 0m).ToString("N2");
            RecalculateSummary();
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _grid == null || e.ColumnIndex < 0)
                return;

            if (_grid.Columns[e.ColumnIndex].Name == "Delete")
            {
                if (_grid.Rows.Count > 1)
                    _grid.Rows.RemoveAt(e.RowIndex);
                else
                    ClearInvoiceLineRow(_grid.Rows[e.RowIndex]);
                RecalculateSummary();
            }
            else if (_grid.Columns[e.ColumnIndex].Name == "Duplicate")
            {
                InvoiceLineItem copy = RowToLineItem(_grid.Rows[e.RowIndex]);
                AddLineRow(copy);
            }
        }

        private InvoiceLineItem RowToLineItem(DataGridViewRow row)
        {
            if (row == null)
                return null;
            decimal qty = TryParseDecimal(row.Cells["Quantity"].Value);
            decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
            decimal discount = Math.Min(Math.Max(TryParseDecimal(row.Cells["DiscountPercent"].Value), 0m), 100m);
            decimal amount = TryParseDecimal(row.Cells["Amount"].Value);
            string category = row.Cells["Category"].Value?.ToString() ?? "Service";
            string description = row.Cells["Description"].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description) && IsMeaningfulInvoiceLineRow(row))
                description = ResolveInvoiceLineDescription(category);
            return new InvoiceLineItem
            {
                Description = description,
                HSNCode = row.Cells["HSNCode"].Value?.ToString() ?? string.Empty,
                Category = category,
                Unit = row.Cells["Unit"].Value?.ToString() ?? "Nos",
                Quantity = qty <= 0 ? 1m : qty,
                Rate = rate,
                DiscountPercent = discount,
                GSTPercent = TryParseDecimal(row.Cells["GSTPercent"].Value),
                TaxType = row.Cells["TaxType"].Value?.ToString() ?? "Taxable",
                CoverageNote = row.Cells["CoverageNote"].Value?.ToString() ?? string.Empty,
                StockItemID = TryParseInt(row.Cells["StockItemID"].Value),
                IsStockItem = string.Equals(row.Cells["IsStockItem"].Value?.ToString(), "1", StringComparison.OrdinalIgnoreCase),
                IsBillable = !string.Equals(row.Cells["TaxType"].Value?.ToString(), "Out of Scope", StringComparison.OrdinalIgnoreCase),
                Amount = amount
            };
        }

        private void ClearInvoiceLineRow(DataGridViewRow row)
        {
            if (row == null)
                return;

            foreach (DataGridViewCell cell in row.Cells)
                if (cell.OwningColumn.Name != "Delete")
                    cell.Value = null;
            row.Cells["Unit"].Value = "Nos";
            row.Cells["Category"].Value = "Service";
            row.Cells["Quantity"].Value = "1";
            row.Cells["Rate"].Value = "0.00";
            row.Cells["DiscountPercent"].Value = "0";
            row.Cells["GSTPercent"].Value = (_numGST?.Value ?? 18m).ToString("0.##");
            row.Cells["TaxType"].Value = "Taxable";
            row.Cells["Amount"].Value = "0.00";
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            e.Cancel = false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  GST SUMMARY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void RecalculateSummary()
        {
            if (IsInvoiceDropdownOpen())
            {
                _summaryRecalcPending = true;
                return;
            }

            decimal sub = 0;
            decimal tax = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                decimal amount = TryParseDecimal(row.Cells["Amount"].Value);
                decimal gstPct = TryParseDecimal(row.Cells["GSTPercent"].Value);
                string taxType = row.Cells["TaxType"].Value?.ToString() ?? "Taxable";
                sub += amount;
                bool taxable = string.Equals(taxType, "Taxable", StringComparison.OrdinalIgnoreCase);
                decimal rawTax = taxable ? amount * ((gstPct <= 0 ? (_numGST?.Value ?? 18m) : gstPct) / 100m) : 0m;
                tax += Math.Ceiling(rawTax * 100m) / 100m;
            }

            decimal roundOff = _numRoundOff?.Value ?? 0m;
            decimal total   = sub + tax + roundOff;
            decimal paid    = _current?.PaidAmount ?? 0;
            decimal bal     = Math.Max(total - paid, 0m);
            decimal cgst = 0m;
            decimal sgst = 0m;
            decimal igst = tax;
            if (string.Equals(_cmbGstMode?.SelectedItem?.ToString(), "CGST+SGST", StringComparison.OrdinalIgnoreCase))
            {
                cgst = Math.Round(tax / 2m, 2);
                sgst = tax - cgst;
                igst = 0m;
            }

            if (_lblSubTotal != null) _lblSubTotal.Text = IndiaFormatHelper.FormatCurrency(sub);
            if (_lblRightSubTotal != null) _lblRightSubTotal.Text = IndiaFormatHelper.FormatCurrency(sub);
            if (_lblTaxableSummary != null) _lblTaxableSummary.Text = IndiaFormatHelper.FormatCurrency(sub);
            if (_lblGSTAmt != null) _lblGSTAmt.Text = IndiaFormatHelper.FormatCurrency(tax);
            if (_lblRightGST != null) _lblRightGST.Text = IndiaFormatHelper.FormatCurrency(tax);
            if (_lblCGSTAmt != null) _lblCGSTAmt.Text  = IndiaFormatHelper.FormatCurrency(cgst);
            if (_lblSGSTAmt != null) _lblSGSTAmt.Text  = IndiaFormatHelper.FormatCurrency(sgst);
            if (_lblIGSTAmt != null) _lblIGSTAmt.Text  = IndiaFormatHelper.FormatCurrency(igst);
            if (_lblRoundOffAmt != null) _lblRoundOffAmt.Text = roundOff >= 0 ? IndiaFormatHelper.FormatCurrency(roundOff) : "- " + IndiaFormatHelper.FormatCurrency(Math.Abs(roundOff));
            if (_lblTotal != null) _lblTotal.Text    = IndiaFormatHelper.FormatCurrency(total);
            if (_lblRightTotal != null) _lblRightTotal.Text = IndiaFormatHelper.FormatCurrency(total);
            if (_lblAmountPaidSummary != null) _lblAmountPaidSummary.Text = IndiaFormatHelper.FormatCurrency(paid);
            if (_lblBalance != null) _lblBalance.Text  = IndiaFormatHelper.FormatCurrency(bal);
            if (_lblRightBalance != null) _lblRightBalance.Text = IndiaFormatHelper.FormatCurrency(bal);
            if (_txtInventorySummary != null && _current != null)
                _txtInventorySummary.Text = _invSvc.GetInventorySummary(BuildPreviewInvoiceSafe());
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BUTTON HANDLERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BtnNew_Click(object sender, EventArgs e)
        {
            ClearForm();
            ShowInvoiceEditor();
            OpenClientDropdown();
            ShowStatus("New invoice â€” fill the form and click Save Draft.", Color.Gray);
        }

        private void OpenClientDropdown()
        {
            if (_cmbClient == null || _cmbClient.IsDisposed || !_cmbClient.Enabled)
                return;

            _cmbClient.Focus();
            BeginInvoke((Action)(() =>
            {
                if (_cmbClient == null || _cmbClient.IsDisposed || !_cmbClient.Enabled)
                    return;

                _cmbClient.DropDownHeight = Math.Max(_cmbClient.DropDownHeight, _cmbClient.ItemHeight * Math.Max(8, _cmbClient.MaxDropDownItems) + 2);
                _cmbClient.DroppedDown = true;
            }));
        }

        /// <summary>Opens the invoice editor with a fresh draft from external navigation.</summary>
        public void OpenNewInvoiceFromShortcut()
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)OpenNewInvoiceFromShortcut);
                return;
            }

            QueueInitialLoad();
            BtnNew_Click(this, EventArgs.Empty);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateInvoiceForm(true))
                    return;
                SetBusy("Saving draft...");
                Invoice inv = CollectForm();
                if (_current == null || _current.InvoiceID == 0)
                {
                    int id = _invSvc.CreateInvoiceWithLineItems(inv);
                    inv.InvoiceID = id;
                    _current = _invSvc.GetInvoiceById(id);
                    PopulateForm(_current);
                    ShowStatus("Invoice saved: " + _current.InvoiceNumber, SaveGreen);
                    LogInvoiceActivity("invoice created");
                    LogInvoiceActivity("draft saved");
                    ToastNotification.Show(this, "Invoice draft saved.", SaveGreen);
                }
                else
                {
                    inv.InvoiceID = _current.InvoiceID;
                    inv.InvoiceNumber = _current.InvoiceNumber;
                    inv.PaidAmount = _current.PaidAmount;
                    _invSvc.UpdateInvoiceWithLineItems(inv);
                    _current = _invSvc.GetInvoiceById(_current.InvoiceID);
                    PopulateForm(_current);
                    ShowStatus("Invoice updated.", SaveGreen);
                    LogInvoiceActivity("draft saved");
                    ToastNotification.Show(this, "Invoice updated.", SaveGreen);
                }
                LoadInvoiceList(true);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Saving invoice", ex);
                ShowStatus("Invoice could not be saved. Review the form and try again.", Color.Red);
            }
            finally
            {
                SetBusy(null);
            }
        }

        private void BtnFinalise_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.InvoiceID == 0)
            { MessageBox.Show("Save the invoice first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try
            {
                if (!ValidateInvoiceForm(true))
                    return;
                SetBusy("Sending for approval...");
                Invoice invoice = CollectForm();
                invoice.InvoiceID = _current.InvoiceID;
                invoice.InvoiceNumber = _current.InvoiceNumber;
                invoice.PaidAmount = _current.PaidAmount;
                _invSvc.UpdateInvoiceWithLineItems(invoice);
                _invSvc.FinalizeInvoice(_current.InvoiceID);
                _current = _invSvc.GetInvoiceById(_current.InvoiceID);
                PopulateForm(_current);
                ShowStatus("Invoice finalised.", InfoBlue);
                LogInvoiceActivity("status changed");
                ToastNotification.Show(this, "Invoice sent for approval.", InfoBlue);
                LoadInvoiceList(true);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Finalising invoice", ex);
                ShowStatus("Invoice could not be finalised. Review the draft and try again.", Color.Red);
            }
            finally { SetBusy(null); }
        }

        private void BtnRecordPayment_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.InvoiceID == 0)
            {
                MessageBox.Show("Select an invoice first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Build inline payment dialog
            Form dlg = new Form
            {
                Text            = "Record Payment â€” " + _current.InvoiceNumber,
                Width           = 400,
                Height          = 300,
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            int lx = 16, fy = 16, fw = 340;

            Label lblAmt = new Label { Text = "Amount (INR):", AutoSize = true, Location = new Point(lx, fy), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            NumericUpDown numAmt = new NumericUpDown { Location = new Point(lx, fy + 18), Width = fw, Font = new Font("Segoe UI", 9), Minimum = 0.01m, Maximum = 99999999m, DecimalPlaces = 2, Value = Math.Max(_current.BalanceDue, 0.01m) };

            Label lblDate = new Label { Text = "Payment Date:", AutoSize = true, Location = new Point(lx, fy + 52), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            DateTimePicker dtpDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Location = new Point(lx, fy + 70), Width = fw, Value = DateTime.Today, Font = new Font("Segoe UI", 9) };

            Label lblMethod = new Label { Text = "Payment Method:", AutoSize = true, Location = new Point(lx, fy + 104), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            ComboBox cmbMethod = new ComboBox { Location = new Point(lx, fy + 122), Width = fw, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMethod.Items.AddRange(new object[] { "Cash", "Cheque", "NEFT", "UPI", "Bank Transfer", "NEFT/RTGS", "DD" });
            cmbMethod.SelectedIndex = 0;

            Label lblRef = new Label { Text = "Reference / UTR:", AutoSize = true, Location = new Point(lx, fy + 156), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            TextBox txtRef = new TextBox { Location = new Point(lx, fy + 174), Width = fw, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };

            Button btnOK = new Button
            {
                Text      = "Record Payment",
                Location  = new Point(lx, fy + 210),
                Width     = 160,
                Height    = 30,
                BackColor = SaveGreen,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.None
            };
            btnOK.FlatAppearance.BorderSize = 0;

            int capturedInvoiceId = _current.InvoiceID;
            int capturedClientId  = _current.ClientID;
            btnOK.Click += (s2, e2) =>
            {
                try
                {
                    Payment pay = new Payment
                    {
                        InvoiceID       = capturedInvoiceId,
                        ClientID        = capturedClientId,
                        AmountPaid      = numAmt.Value,
                        PaymentDate     = dtpDate.Value.Date,
                        PaymentMode     = cmbMethod.SelectedItem?.ToString() ?? "Cash",
                        ReferenceNumber = txtRef.Text.Trim()
                    };
                    new PaymentService().RecordPayment(pay);
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                }
                catch (Exception ex2)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Recording payment", ex2);
                    MessageBox.Show("Payment could not be recorded. Check the invoice, amount, and payment mode, then try again.", "Payment Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            dlg.Controls.AddRange(new Control[] { lblAmt, numAmt, lblDate, dtpDate, lblMethod, cmbMethod, lblRef, txtRef, btnOK });

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ShowStatus("Payment recorded.", SaveGreen);
                LogInvoiceActivity("receipt converted");
                ToastNotification.Show(this, "Receipt/payment recorded.", SaveGreen);
                LoadInvoiceList(true);
                // Refresh current invoice display
                if (_current != null)
                {
                    _current = _invSvc.GetInvoiceById(_current.InvoiceID);
                    if (_current != null) PopulateForm(_current);
                }
            }
        }

        private void BtnCreateCreditNote_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.InvoiceID == 0)
            {
                MessageBox.Show("Select an invoice first.", "Credit Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            decimal balance = Math.Max(0m, _current.BalanceDue);
            if (balance <= 0)
            {
                MessageBox.Show("This invoice has no open balance to credit.", "Credit Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (Form dlg = new Form())
            {
                dlg.AutoScaleMode = AutoScaleMode.Dpi;
                dlg.Text = "Create Credit Note - " + _current.InvoiceNumber;
                dlg.Width = 430;
                dlg.Height = 255;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                Label lblAmount = new Label { Text = "Credit amount (max " + IndiaFormatHelper.FormatCurrency(balance) + ")", Location = new Point(16, 18), Width = 360, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
                NumericUpDown numAmount = new NumericUpDown { Location = new Point(16, 42), Width = 360, DecimalPlaces = 2, Minimum = 0.01m, Maximum = Math.Max(0.01m, balance), Value = balance, Font = new Font("Segoe UI", 9) };
                Label lblReason = new Label { Text = "Reason", Location = new Point(16, 82), Width = 200, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
                TextBox txtReason = new TextBox { Location = new Point(16, 106), Width = 360, Height = 48, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
                Button btnCreate = MakeBtn("Create", SaveGreen, 92);
                Button btnCancel = MakeBtn("Cancel", Color.Gray, 92);
                btnCreate.Location = new Point(184, 168);
                btnCancel.Location = new Point(284, 168);
                btnCreate.DialogResult = DialogResult.OK;
                btnCancel.DialogResult = DialogResult.Cancel;
                dlg.Controls.AddRange(new Control[] { lblAmount, numAmount, lblReason, txtReason, btnCreate, btnCancel });
                dlg.AcceptButton = btnCreate;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    Invoice creditNote = _invSvc.CreateCreditNoteForInvoice(_current.InvoiceID, numAmount.Value, txtReason.Text.Trim());
                    _current = _invSvc.GetInvoiceById(_current.InvoiceID);
                    PopulateForm(_current);
                    LoadInvoiceList(true);
                    ShowStatus("Credit note created: " + creditNote.InvoiceNumber, SaveGreen);
                    LogInvoiceActivity("credit note created");
                    ToastNotification.Show(this, "Credit note created.", SaveGreen);
                    MessageBox.Show("Credit note created.\r\n\r\n" + creditNote.InvoiceNumber + "\r\nAmount: " + IndiaFormatHelper.FormatCurrency(creditNote.TotalAmount), "Credit Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Creating credit note", ex);
                    ShowStatus("Credit note could not be created. Review the amount and try again.", Color.Red);
                }
            }
        }

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateInvoiceForm(true))
                    return;
                SetBusy("Generating invoice PDF preview...");
                Invoice invoice;
                try
                {
                    invoice = BuildPreviewInvoice();
                }
                catch
                {
                    invoice = BuildPreviewInvoiceSafe();
                    invoice.InvoiceNumber = string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? "DRAFT-PREVIEW" : invoice.InvoiceNumber;
                    invoice.InvoiceTitle = string.IsNullOrWhiteSpace(invoice.InvoiceTitle) ? "TAX INVOICE" : invoice.InvoiceTitle;
                    invoice.InvoiceDate = invoice.InvoiceDate == default(DateTime) ? DateTime.Today : invoice.InvoiceDate;
                    invoice.DueDate = invoice.DueDate == default(DateTime) ? DateTime.Today.AddDays(30) : invoice.DueDate;
                    invoice.ClientName = string.IsNullOrWhiteSpace(invoice.ClientName) ? "Draft Client" : invoice.ClientName;
                    invoice.Subject = string.IsNullOrWhiteSpace(invoice.Subject) ? "Draft invoice preview" : invoice.Subject;
                    invoice.GSTMode = string.IsNullOrWhiteSpace(invoice.GSTMode) ? (_txtPlaceOfSupply != null && _txtPlaceOfSupply.Text.IndexOf("Maharashtra", StringComparison.OrdinalIgnoreCase) >= 0 ? "CGST+SGST" : "IGST") : invoice.GSTMode;
                    invoice.GSTPercent = invoice.GSTPercent <= 0 ? 18m : invoice.GSTPercent;
                    if (invoice.LineItems == null) invoice.LineItems = new List<InvoiceLineItem>();
                    if (invoice.LineItems.Count == 0)
                        invoice.LineItems.Add(new InvoiceLineItem { Description = "Draft service / material line", HSNCode = "9987", Unit = "Nos", Quantity = 1, Rate = 0, GSTPercent = 18, IsBillable = true });
                }
                string html = _invSvc.BuildInvoiceHtml(invoice);
                LogInvoiceActivity("PDF generated");
                ToastNotification.Show(this, "Invoice preview generated.", InfoBlue);
                new HtmlPreviewDialog("Invoice Preview - " + (invoice.InvoiceNumber ?? "(draft)"), html).ShowDialog(this);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Previewing invoice", ex);
                ShowStatus("Invoice preview could not be generated. Review the invoice and try again.", Color.Red);
            }
            finally
            {
                SetBusy(null);
            }
        }

        private void BtnCompare_Click(object sender, EventArgs e)
        {
            try
            {
                var invoice = BuildPreviewInvoice();
                string report = "Template comparison against TEVA invoice format:" + Environment.NewLine + Environment.NewLine
                    + _invSvc.BuildTemplateComparison(invoice);
                MessageBox.Show(report, "Invoice Format Comparison", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Comparing invoice format", ex);
                ShowStatus("Invoice comparison could not be generated. Review the invoice and try again.", Color.Red);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EVENT HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void CmbClient_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (_updating)
                return;

            RunAfterDropdownClosed(ProcessClientSelectionIfChanged);
        }

        private void ProcessClientSelectionIfChanged()
        {
            if (_updating || _cmbClient == null || _cmbClient.DroppedDown)
                return;

            int selectedClientId = GetSelectedComboId(_cmbClient);
            if (selectedClientId == _lastProcessedClientId)
                return;

            _lastProcessedClientId = selectedClientId;

            ComboItem ci = _cmbClient.SelectedItem as ComboItem;
            LoadSiteDropdowns(ci?.Id ?? 0);
            LoadContractDropdowns(ci?.Id ?? 0);
            if (ci != null && ci.Id > 0 && string.IsNullOrWhiteSpace(_txtSendInvoiceTo.Text))
            {
                try
                {
                    var client = _clientSvc.GetClientById(ci.Id);
                    if (client != null)
                    {
                        _txtSendInvoiceTo.Text = client.CompanyName + (string.IsNullOrWhiteSpace(client.BillingAddress) ? "" : Environment.NewLine + client.BillingAddress);
                        if (string.IsNullOrWhiteSpace(_txtSubject.Text))
                            _txtSubject.Text = "Supply / service invoice at " + client.CompanyName + (_cmbSite.SelectedItem is ComboItem site && site.Id > 0 ? " - " + site.Text : "");
                    }
                }
                catch { }
            }
            if (ci != null && ci.Id > 0)
                LogInvoiceActivity("client selected");

            if (_cmbTemplate != null && _cmbTemplate.SelectedIndex > 0)
                ApplySelectedTemplate(false);
        }

        private void CmbTemplate_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (_updating)
                return;

            RunAfterDropdownClosed(() => ApplySelectedTemplate(true));
        }

        private int GetSelectedComboId(ComboBox combo)
        {
            return (combo?.SelectedItem as ComboItem)?.Id ?? 0;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void ApplySelectedTemplate(bool replaceLineItems)
        {
            ComboItem templateItem = _cmbTemplate.SelectedItem as ComboItem;
            ComboItem clientItem = _cmbClient.SelectedItem as ComboItem;
            ComboItem siteItem = _cmbSite.SelectedItem as ComboItem;
            ComboItem contractItem = _cmbContract.SelectedItem as ComboItem;

            if (templateItem == null || string.IsNullOrWhiteSpace(templateItem.Tag))
                return;

            try
            {
                Invoice templateInvoice = _invSvc.BuildInvoiceFromTemplate(templateItem.Tag, clientItem?.Id ?? 0, siteItem?.Id ?? 0, contractItem?.Id ?? 0);
                _updating = true;
                _txtSubject.Text = templateInvoice.Subject ?? _txtSubject.Text;
                _txtNotes.Text = templateInvoice.Notes ?? _txtNotes.Text;
                _txtChecklist.Text = templateInvoice.ServiceChecklist ?? "";
                _txtAssetDetails.Text = templateInvoice.AssetDetails ?? "";
                PopulateWorkflowGrid(_gridChecklist, templateInvoice.ServiceChecklist);
                PopulateWorkflowGrid(_gridAssets, templateInvoice.AssetDetails);
                _txtPaymentTerms.Text = templateInvoice.PaymentTerms ?? _txtPaymentTerms.Text;
                _txtPlaceOfSupply.Text = templateInvoice.PlaceOfSupply ?? _txtPlaceOfSupply.Text;
                SelectComboByText(_cmbCoverageType, templateInvoice.ContractCoverageType);
                SelectComboByText(_cmbWarrantyStatus, templateInvoice.WarrantyStatus);
                SelectComboByText(_cmbGstMode, templateInvoice.GSTMode);
                _numGST.Value = Math.Min(Math.Max(templateInvoice.GSTPercent, 0), 28);
                _dtpWarrantyExpiry.Checked = templateInvoice.WarrantyExpiry.HasValue;
                if (templateInvoice.WarrantyExpiry.HasValue)
                    _dtpWarrantyExpiry.Value = templateInvoice.WarrantyExpiry.Value;
                _dtpNextServiceDue.Checked = templateInvoice.NextServiceDueDate.HasValue;
                if (templateInvoice.NextServiceDueDate.HasValue)
                    _dtpNextServiceDue.Value = templateInvoice.NextServiceDueDate.Value;
                if (replaceLineItems)
                {
                    _grid.Rows.Clear();
                    foreach (InvoiceLineItem line in templateInvoice.LineItems ?? new List<InvoiceLineItem>())
                        AddLineRow(line);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Applying invoice template", ex);
                ShowStatus("Template could not be applied. Review the selection and try again.", Color.Red);
            }
            finally
            {
                _updating = false;
                RecalculateSummary();
                _txtNudges.Text = _invSvc.GetBehavioralNudges(BuildPreviewInvoiceSafe());
            }
        }

        private decimal TryParseDecimal(object val)
        {
            if (val == null) return 0;
            decimal result;
            string text = val.ToString();
            if (decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out result))
                return result;
            return decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private int TryParseInt(object value)
        {
            if (value == null)
                return 0;
            return int.TryParse(value.ToString(), out int parsed) ? parsed : 0;
        }

        private void SelectCombo(ComboBox cmb, int id)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
                if ((cmb.Items[i] as ComboItem)?.Id == id)
                { cmb.SelectedIndex = i; return; }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private void SelectComboByText(ComboBox combo, string text)
        {
            if (combo == null || string.IsNullOrWhiteSpace(text))
                return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                string itemText = combo.Items[i] is ComboItem item ? item.Text : combo.Items[i]?.ToString();
                if (string.Equals(itemText, text, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectComboByTag(ComboBox combo, string tag)
        {
            if (combo == null)
                return;

            for (int i = 0; i < combo.Items.Count; i++)
            {
                ComboItem item = combo.Items[i] as ComboItem;
                if (string.Equals(item?.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private void ShowStatus(string msg, Color color)
        {
            _lblStatus.Text = msg; _lblStatus.ForeColor = color;
        }

        private void SetBusy(string message)
        {
            bool busy = !string.IsNullOrWhiteSpace(message);
            if (busy)
                ShowStatus(message, DS.Slate600);
            if (_btnSaveInvoice != null)
                _btnSaveInvoice.Enabled = !busy;
            if (_btnNewInvoice != null)
                _btnNewInvoice.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private bool ValidateInvoiceForm(bool showMessage)
        {
            List<string> errors = new List<string>();
            ComboItem client = _cmbClient?.SelectedItem as ComboItem;

            if (client == null || client.Id <= 0)
                errors.Add("Client is required.");
            if (_dtpInvDate == null)
                errors.Add("Invoice date is required.");
            if (_dtpDueDate == null)
                errors.Add("Due date is required.");
            if (_dtpInvDate != null && _dtpDueDate != null && _dtpDueDate.Value.Date < _dtpInvDate.Value.Date)
                errors.Add("Due date must be after or equal to invoice date.");
            if (_numGST != null && _numGST.Value < 0)
                errors.Add("GST values cannot be negative.");
            if (_numRoundOff != null && _numRoundOff.Value < -99999)
                errors.Add("Round off value is invalid.");

            string sendTo = _txtSendInvoiceTo?.Text ?? string.Empty;
            Match email = Regex.Match(sendTo, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
            if (sendTo.Contains("@") && !email.Success)
                errors.Add("Send Invoice To contains an invalid email address.");

            Invoice preview = BuildPreviewInvoiceSafe();
            if (preview.LineItems == null || preview.LineItems.Count == 0)
                errors.Add("Add at least one invoice line item.");
            if (preview.TotalAmount < 0 || preview.BalanceDue < 0)
                errors.Add("Invoice total cannot be negative.");

            if (errors.Count == 0)
                return true;

            string message = string.Join(Environment.NewLine, errors);
            ShowStatus(errors[0], Color.Red);
            ToastNotification.Show(this, errors[0], Color.FromArgb(239, 68, 68));
            if (showMessage)
                MessageBox.Show(message, "Invoice Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private void LogInvoiceActivity(string activity)
        {
            try
            {
                AppLogger.LogInfo("Invoice activity: " + activity + " | " + (_current?.InvoiceNumber ?? _txtInvNo?.Text ?? "draft"));
            }
            catch
            {
            }
        }

        private void EmailInvoiceFromCurrent()
        {
            try
            {
                if (!ValidateInvoiceForm(true))
                    return;
                SetBusy("Preparing invoice email...");
                Invoice invoice = BuildPreviewInvoiceSafe();
                if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                    invoice.InvoiceNumber = _current?.InvoiceNumber ?? _txtInvNo.Text.Trim();
                string html = _invSvc.BuildInvoiceHtml(invoice);
                LogInvoiceActivity("invoice emailed");
                ToastNotification.Show(this, "Invoice email prepared.", InfoBlue);
                new HtmlPreviewDialog("Email Invoice - " + (invoice.InvoiceNumber ?? "(draft)"), html).ShowDialog(this);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Preparing invoice email", ex);
                ShowStatus("Invoice email could not be prepared. Please review the invoice and try again.", Color.Red);
            }
            finally
            {
                SetBusy(null);
            }
        }

        private string BuildPaymentHistory(int invoiceId)
        {
            if (invoiceId <= 0)
                return "No payments recorded yet.";

            try
            {
                var payments = _paymentSvc.GetPaymentsForInvoice(invoiceId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToList();
                if (payments.Count == 0)
                    return "No payments recorded yet.";

                return string.Join(Environment.NewLine, payments.Select(p =>
                    p.PaymentDate.ToString("dd MMM yyyy") + " | " + (p.PaymentMode ?? "-") + " | INR " + p.AmountPaid.ToString("N2") + (string.IsNullOrWhiteSpace(p.ReferenceNumber) ? "" : " | Ref: " + p.ReferenceNumber)));
            }
            catch
            {
                return "Payment history unavailable.";
            }
        }

        private Invoice BuildPreviewInvoiceSafe()
        {
            try
            {
                return CollectForm();
            }
            catch
            {
                return new Invoice { LineItems = new List<InvoiceLineItem>() };
            }
        }

        private Invoice BuildPreviewInvoice()
        {
            Invoice invoice = CollectForm();
            invoice.InvoiceNumber = string.IsNullOrWhiteSpace(_txtInvNo.Text) || _txtInvNo.Text == "(auto-generated)"
                ? (_current?.InvoiceNumber ?? "DRAFT-PREVIEW")
                : _txtInvNo.Text.Trim();
            invoice.ClientName = (_cmbClient.SelectedItem as ComboItem)?.Text ?? "";
            invoice.SiteName = (_cmbSite.SelectedItem as ComboItem)?.Text ?? "";
            invoice.PaymentStatus = _cmbStatus.SelectedItem?.ToString() ?? "Draft";
            invoice.PaidAmount = _current?.PaidAmount ?? 0;
            invoice.BalanceDue = Math.Max(invoice.TotalAmount - invoice.PaidAmount, 0);
            if (string.IsNullOrWhiteSpace(invoice.Subject) && !string.IsNullOrWhiteSpace(invoice.ClientName))
                invoice.Subject = "Supply / service invoice for " + invoice.ClientName + (string.IsNullOrWhiteSpace(invoice.SiteName) ? "" : " - " + invoice.SiteName);
            invoice.AssetDetails = CollectWorkflowRows(_gridAssets);
            invoice.ServiceChecklist = CollectWorkflowRows(_gridChecklist);
            invoice.PaymentTerms = _txtPaymentTerms.Text.Trim();
            invoice.PlaceOfSupply = _txtPlaceOfSupply.Text.Trim();
            if (_txtNudges != null)
                _txtNudges.Text = _invSvc.GetBehavioralNudges(invoice);
            return invoice;
        }

        private void EnsureComboValue(string columnName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (!(_grid.Columns[columnName] is DataGridViewComboBoxColumn comboColumn))
                return;

            if (comboColumn.DataSource is IEnumerable<object> boundItems)
            {
                var values = boundItems.Select(i => i?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (!values.Any(i => string.Equals(i, value, StringComparison.OrdinalIgnoreCase)))
                {
                    values.Add(value);
                    comboColumn.DataSource = values;
                }
                return;
            }

            bool exists = comboColumn.Items.Cast<object>().Any(i => string.Equals(i?.ToString(), value, StringComparison.OrdinalIgnoreCase));
            if (!exists)
                comboColumn.Items.Add(value);
        }

        private void EnsureComboItems(string columnName, IEnumerable<string> values)
        {
            foreach (string value in values ?? Enumerable.Empty<string>())
                EnsureComboValue(columnName, value);
        }

        private void EnsurePreviewComboItem(ComboBox combo, string text, bool useComboItem)
        {
            if (combo == null || string.IsNullOrWhiteSpace(text))
                return;

            object existing = combo.Items.Cast<object>().FirstOrDefault(i => string.Equals(i?.ToString(), text, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = useComboItem ? (object)new ComboItem { Id = 0, Text = text } : text;
                combo.Items.Add(existing);
            }

            combo.SelectedItem = existing;
            ClearComboSelection(combo);
        }

        private void CenterDocumentPage(Panel container)
        {
            if (_documentPage == null)
                return;

            Point scroll = container.AutoScrollPosition;
            int available = container.ClientSize.Width;
            int preferredLeft = available > 1500
                ? 28
                : Math.Max((available - _documentPage.Width) / 2, 18);
            _documentPage.Left = preferredLeft + scroll.X;
            _documentPage.Top = 8 + scroll.Y;
        }

        private Panel MakeInvoiceCard(Invoice invoice, Color statusColor)
        {
            Panel card = new Panel
            {
                Width = 308,
                Height = 88,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
                Padding = new Padding(14, 12, 14, 12),
                Tag = invoice
            };

            card.Paint += (s, e) =>
            {
                using (Pen border = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(border, 0, card.Height - 1, card.Width, card.Height - 1);
            };

            Label lblNumber = new Label
            {
                Text = invoice.InvoiceNumber ?? "(draft)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = DS.Slate900,
                Location = new Point(14, 12),
                AutoSize = true
            };

            Label lblClient = new Label
            {
                Text = string.IsNullOrWhiteSpace(invoice.ClientName) ? "Unassigned client" : invoice.ClientName,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                Location = new Point(14, 36),
                AutoSize = true
            };

            Label lblAmount = new Label
            {
                Text = "INR " + invoice.TotalAmount.ToString("N0"),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = DS.Slate700,
                Location = new Point(14, 58),
                AutoSize = true
            };

            Label lblStatus = new Label
            {
                Text = string.IsNullOrWhiteSpace(invoice.PaymentStatus) ? "Pending" : invoice.PaymentStatus,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = statusColor,
                AutoSize = true
            };
            lblStatus.Location = new Point(card.Width - lblStatus.PreferredWidth - 16, 14);

            foreach (Control control in new Control[] { card, lblNumber, lblClient, lblAmount, lblStatus })
            {
                control.Click += (s, e) => SelectInvoice(invoice, card);
            }

            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Open", null, (s, e) => SelectInvoice(invoice, card));
            RecordDeletionUi.AddDeleteMenuItem(menu, (s, e) => DeleteInvoice(invoice, _current != null && _current.InvoiceID == invoice.InvoiceID));
            card.ContextMenuStrip = menu;
            foreach (Control control in new Control[] { lblNumber, lblClient, lblAmount, lblStatus })
                control.ContextMenuStrip = menu;

            card.Controls.Add(lblNumber);
            card.Controls.Add(lblClient);
            card.Controls.Add(lblAmount);
            card.Controls.Add(lblStatus);
            return card;
        }

        private void HighlightCard(Panel card, bool selected)
        {
            card.BackColor = selected ? DS.Indigo50 : Color.White;
            foreach (Control child in card.Controls)
            {
                if (child is Label label && label.Font.Bold)
                    label.ForeColor = selected ? DS.Indigo600 : DS.Slate900;
            }
        }

        private Label MakeStickyValue(Panel parent, string title, int x, int y, bool bold = false, Color? accent = null)
        {
            Panel box = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(148, 40),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            box.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
            };

            Label label = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = DS.Slate500,
                AutoSize = true,
                Location = new Point(8, 6)
            };
            Label value = new Label
            {
                Text = "INR 0.00",
                Font = new Font("Segoe UI", bold ? 9.5f : 9f, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = accent ?? DS.Slate900,
                AutoSize = true,
                Location = new Point(8, 20)
            };
            box.Controls.Add(label);
            box.Controls.Add(value);
            parent.Controls.Add(box);
            return value;
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 32, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            UIHelper.ApplyActionButton(b);
            return b;
        }

        // â”€â”€ Inner helper: combo item with Id + display text â”€â”€
        private class ComboItem
        {
            public int    Id   { get; set; }
            public string Text { get; set; }
            public string Tag  { get; set; }
            public override string ToString() => Text;
        }

        private class InvoiceCatalogItem
        {
            public string Source { get; set; }
            public string Description { get; set; }
            public string HsnSacCode { get; set; }
            public string Category { get; set; }
            public string Unit { get; set; }
            public decimal Rate { get; set; }
            public decimal GstPercent { get; set; }
            public string TaxType { get; set; }
            public string Notes { get; set; }
            public int? StockItemId { get; set; }
            public bool IsStockItem { get; set; }
        }

        private sealed class InvoiceListSnapshot
        {
            public List<Invoice> Invoices { get; set; }
            public Invoice FirstDetail { get; set; }
        }

    }
}



