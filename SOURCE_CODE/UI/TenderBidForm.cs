using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public partial class TenderBidForm : DeferredPageControl
    {
        private readonly TenderService _svc = new TenderService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly VendorService _vendorSvc = new VendorService();
        private readonly DocumentTemplateService _docTemplateSvc = new DocumentTemplateService();

        private FlowLayoutPanel _quoteFlow;
        private FlowLayoutPanel _renewalFlow;
        private TextBox _txtQuoteNo;
        private TextBox _txtTitle;
        private ComboBox _cboClient;
        private ComboBox _cboSite;
        private ComboBox _cboValidity;
        private ComboBox _cboStatus;
        private ComboBox _cboCommercialFlow;
        private ComboBox _cboCustomerDocStatus;
        private ComboBox _cboSupplierDocStatus;
        private DateTimePicker _dtpDate;
        private DateTimePicker _dtpDue;
        private DateTimePicker _dtpRequiredBy;
        private TextBox _txtNotes;
        private DataGridView _grid;
        private ComboBox _cmbCategoryFilter;
        private TextBox _txtItemSearch;
        private Label _lblLineItemCount;
        private FlowLayoutPanel _suggestionFlow;
        private Label _lblStatus;
        private Label _lblTaxable;
        private Label _lblGst;
        private Label _lblTotal;
        private Label _lblMargin;
        private Label _lblLastSaved;
        private Label _lblAlertShortfall;
        private Label _lblAlertMargin;
        private Label _lblAlertExpiry;
        private Label _lblAlertSupplier;
        private Label _lblAlertSite;
        private NumericUpDown _numDiscount;
        private Label _lblLineItemsEmptyState;
        private Label _lblKpiQuoteValue;
        private Label _lblKpiMargin;
        private Label _lblKpiMarginSub;
        private Label _lblKpiApproval;
        private Label _lblKpiApprovalSub;
        private Label _lblKpiProbability;
        private Label _lblKpiProbabilitySub;
        private Label _lblKpiValidity;
        private Label _lblKpiValiditySub;
        private Label _lblKpiSaved;
        private Label _lblKpiSavedSub;
        private Label _quoteDetailsStatusPill;
        private Panel _workflowPanel;

        private readonly List<Button> _filledQuoteButtons = new List<Button>();
        private readonly List<Button> _secondaryQuoteButtons = new List<Button>();

        private readonly List<B2BClient> _clients = new List<B2BClient>();
        private readonly List<StockItem> _inventoryItems = new List<StockItem>();
        private readonly List<Vendor> _vendors = new List<Vendor>();
        private readonly List<TenderBidLineItem> _lineItems = new List<TenderBidLineItem>();
        private TenderBid _current;
        private Panel _selectedCard;
        private bool _loading;
        private bool _updatingGrid;
        private int _inventorySelectionRow = -1;
        private StockItem _inventorySelectionItem;
        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };
        public Action<int> OnNavigate { get; set; }
        private Button _btnNewQuote;
        private Button _btnSaveQuote;
        private Button _btnBackToQuoteDashboard;
        private Panel _quotationWorkspacePanel;

        private static readonly Color QuotePageBg = Color.FromArgb(246, 248, 251);
        private static readonly Color QuoteSurface = Color.White;
        private static readonly Color QuoteText = Color.FromArgb(17, 24, 39);
        private static readonly Color QuoteMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color HeaderBg = Color.FromArgb(15, 23, 42);
        private static readonly Color SaveGreen = Color.FromArgb(13, 148, 136);
        private static readonly Color InfoBlue = Color.FromArgb(37, 99, 235);
        private static readonly Color WarnOrange = Color.FromArgb(245, 158, 11);
        private static readonly Color CargoPurple = Color.FromArgb(79, 70, 229);
        private static readonly Color BorderColor = Color.FromArgb(221, 227, 234);
        private static readonly Color InputFill = Color.FromArgb(248, 250, 252);

        public TenderBidForm()
        {
            Dock = DockStyle.Fill;
            BackColor = QuotePageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyQuotationVisualFixes();
            ApplyPermissions();
            HandleCreated += (s, e) => BeginInvoke((Action)(() =>
            {
                ApplyLineItemsGridLayout();
                ApplyQuotationVisualFixes();
            }));
            HandleCreated += async (s, e) => await InitializeAsync();
            ParentChanged += async (s, e) => await InitializeAsync();
            Load += async (s, e) =>
            {
                BeginInvoke((Action)(() =>
                {
                    ApplyLineItemsGridLayout();
                    ApplyQuotationVisualFixes();
                }));
                await InitializeAsync();
            };
        }

        private async Task InitializeAsync()
        {
            if (Parent == null)
                return;

            if (_loading)
                return;

            _loading = true;
            SetStatus("Loading quotations...", DS.Slate500);
            try
            {
                var inventoryTask = Task.Run(() => _inventorySvc.GetAll());
                var clientsTask = Task.Run(() => _clientSvc.GetAllClientsIncludingInactive());
                var vendorsTask = Task.Run(() => _vendorSvc.GetAll());
                var quotesTask = Task.Run(() => _svc.GetAll().OrderByDescending(q => q.RequiredByDate ?? q.DueDate).Take(80).ToList());

                var clients = await clientsTask;
                var vendors = await vendorsTask;
                _clients.Clear();
                _clients.AddRange(clients ?? Enumerable.Empty<B2BClient>());
                _vendors.Clear();
                _vendors.AddRange(vendors ?? Enumerable.Empty<Vendor>());
                BindSupplierColumn();
                BindClients();
                UIHelper.ShowEmptyVendorsMessageIfNeeded(FindForm(), _vendors, "TenderBidForm.InitializeAsync");

                var inventory = await inventoryTask;
                _inventoryItems.Clear();
                _inventoryItems.AddRange(inventory ?? Enumerable.Empty<StockItem>());
                BindCategoryFilter();
                BindInventoryItems();

                var quotes = await quotesTask;
                BindQuoteList(quotes);
                BindRenewalAlerts();
                NewRecord(false);
                SetStatus("Quotation workspace ready.", DS.Slate500);
            }
            catch (Exception ex)
            {
                SetStatus("Load error: " + ex.Message, Color.Firebrick);
            }
            finally
            {
                _loading = false;
            }
        }

        private void BuildLayout()
        {
            BackColor = QuotePageBg;

            Panel hiddenDataHost = new Panel { Visible = false, Width = 1, Height = 1 };
            _quoteFlow = new FlowLayoutPanel();
            _renewalFlow = new FlowLayoutPanel();
            hiddenDataHost.Controls.Add(_quoteFlow);
            hiddenDataHost.Controls.Add(_renewalFlow);
            Controls.Add(hiddenDataHost);

            Panel header = BuildErpHeader();
            Panel quotationDashboard = BuildQuotationDashboardPanel();

            _quotationWorkspacePanel = new Panel { Dock = DockStyle.Fill, BackColor = QuotePageBg, Visible = false };
            TableLayoutPanel workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = QuotePageBg,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(24, 4, 24, 24)
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel mainHost = new Panel { Dock = DockStyle.Fill, BackColor = QuotePageBg, Margin = new Padding(0, 0, 12, 0), MinimumSize = new Size(650, 0) };
            Panel summaryHost = new Panel { Dock = DockStyle.Fill, BackColor = QuotePageBg, Margin = new Padding(12, 0, 0, 0), MinimumSize = new Size(330, 0) };
            mainHost.Controls.Add(BuildMainWorkspace());
            summaryHost.Controls.Add(BuildRightSummaryPanel());
            workspace.Controls.Add(mainHost, 0, 0);
            workspace.Controls.Add(summaryHost, 1, 0);

            _quotationWorkspacePanel.Controls.Add(workspace);
            Controls.Add(_quotationWorkspacePanel);
            Controls.Add(quotationDashboard);
            Controls.Add(header);
            ShowQuotationDashboard();
        }

        private void ShowQuotationDashboard()
        {
            if (_quotationDashboardPanel != null)
            {
                _quotationDashboardPanel.Dock = DockStyle.Fill;
                _quotationDashboardPanel.Visible = true;
                _quotationDashboardPanel.BringToFront();
            }
            if (_quotationWorkspacePanel != null)
                _quotationWorkspacePanel.Visible = false;
            if (_btnBackToQuoteDashboard != null)
                _btnBackToQuoteDashboard.Visible = false;
        }

        private void ShowQuotationEditor()
        {
            if (_quotationDashboardPanel != null)
                _quotationDashboardPanel.Visible = false;
            if (_quotationWorkspacePanel != null)
            {
                _quotationWorkspacePanel.Visible = true;
                _quotationWorkspacePanel.BringToFront();
            }
            if (_btnBackToQuoteDashboard != null)
                _btnBackToQuoteDashboard.Visible = true;
        }

        private Panel BuildLeftPanel()
        {
            Panel left = new Panel { Dock = DockStyle.Left, Width = 312, BackColor = QuoteSurface };
            left.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawLine(p, left.Width - 1, 0, left.Width - 1, left.Height);
            };

            Label title = new Label
            {
                Text = "QUOTATION TRACKING",
                Dock = DockStyle.Top,
                Height = 42,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = HeaderBg,
                Padding = new Padding(24, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label hint = new Label
            {
                Text = "Track customer quote status, due dates, approvals, and follow-up urgency.",
                Dock = DockStyle.Top,
                Height = 42,
                Font = new Font("Segoe UI", 8),
                ForeColor = QuoteMuted,
                Padding = new Padding(24, 0, 24, 8)
            };

            // â”€â”€ SplitContainer: quotes on top, renewal alerts on bottom â”€â”€
            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Horizontal,
                SplitterWidth = 4,
                BackColor     = QuoteSurface,
                FixedPanel    = FixedPanel.Panel2
            };
            bool splitPositioned = false;
            split.Layout += (s, e) =>
            {
                if (splitPositioned || split.Height < 50) return;
                try
                {
                    split.Panel1MinSize = 60;
                    split.Panel2MinSize = 180;
                    int target = Math.Max(60, split.Height - 190 - split.SplitterWidth);
                    split.SplitterDistance = target;
                    splitPositioned = true;
                }
                catch { }
            };

            // â”€â”€ Panel1: scrollable quote list â”€â”€
            Panel listWrap = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = QuoteSurface };
            _quoteFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = QuoteSurface,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            listWrap.Controls.Add(_quoteFlow);
            split.Panel1.Controls.Add(listWrap);

            // â”€â”€ Panel2: renewal alerts (always visible) â”€â”€
            Panel renewalWrap = new Panel { Dock = DockStyle.Fill, BackColor = QuoteSurface, Padding = new Padding(0, 6, 0, 0) };
            renewalWrap.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawLine(p, 0, 0, renewalWrap.Width, 0);
            };
            renewalWrap.Controls.Add(new Label
            {
                Text = "Follow-up alerts",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = WarnOrange,
                Padding = new Padding(24, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            });
            _renewalFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(16, 0, 16, 8),
                BackColor = QuoteSurface
            };
            renewalWrap.Controls.Add(_renewalFlow);
            split.Panel2.Controls.Add(renewalWrap);

            left.Controls.Add(split);
            left.Controls.Add(hint);
            left.Controls.Add(title);
            return left;
        }

        private Panel BuildErpHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = QuotePageBg, Padding = new Padding(24, 16, 24, 10) };
            header.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawLine(p, 24, header.Height - 1, header.Width - 24, header.Height - 1);
            };

            Label title = new Label { Text = "Quotations", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = QuoteText, Location = new Point(24, 12), AutoSize = true };
            Label subtitle = new Label { Text = "Create, price, approve, and convert customer quotations.", Font = new Font("Segoe UI", 9), ForeColor = QuoteMuted, Location = new Point(25, 45), AutoSize = true };
            Label breadcrumb = new Label { Text = "Dashboard   >   Quotations   >   New Quotation", Font = new Font("Segoe UI", 8.5f), ForeColor = QuoteMuted, Location = new Point(25, 74), AutoSize = true };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(breadcrumb);

            _lblStatus = new Label { Text = "", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = QuoteMuted, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(760, 56) };
            header.Controls.Add(_lblStatus);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Height = 42,
                Width = 900,
                Location = new Point(Math.Max(300, Width - 890), 18),
                BackColor = Color.Transparent
            };
            header.Resize += (s, e) => actions.Location = new Point(Math.Max(420, header.Width - actions.Width - 24), 18);

            _btnBackToQuoteDashboard = MakeOutlineBtn("< Back to Dashboard", 150);
            _btnNewQuote = MakeBtn("+  New Quotation", InfoBlue, 142);
            Button btnPreview = MakeOutlineBtn("Preview", 86);
            Button btnImport = MakeOutlineBtn("Import", 86);
            Button btnUploadDoc = MakeOutlineBtn("Upload PDF", 104);
            Button btnOpenDoc = MakeOutlineBtn("Open PDF", 96);
            Button btnMore = MakeOutlineBtn("...", 36);
            RegisterFilledButton(_btnNewQuote, CargoPurple);
            RegisterSecondaryButton(btnPreview);
            RegisterSecondaryButton(btnImport);
            RegisterSecondaryButton(btnUploadDoc);
            RegisterSecondaryButton(btnOpenDoc);
            RegisterSecondaryButton(btnMore);
            RegisterSecondaryButton(_btnBackToQuoteDashboard);
            _btnBackToQuoteDashboard.Visible = false;
            _btnBackToQuoteDashboard.Click += (s, e) => ShowQuotationDashboard();
            _btnNewQuote.Click += (s, e) => NewRecord();
            btnPreview.Click += (s, e) => PreviewQuotation();
            btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Quotations, FindForm());
            btnUploadDoc.Click += (s, e) => UploadCompanyPdf();
            btnOpenDoc.Click += (s, e) => OpenCompanyPdf();
            btnMore.Click += (s, e) => CompareQuotation();
            actions.Controls.AddRange(new Control[] { _btnBackToQuoteDashboard, _btnNewQuote, btnPreview, btnImport, btnUploadDoc, btnOpenDoc, btnMore });
            header.Controls.Add(actions);
            return header;
        }

        private Panel BuildKpiRow()
        {
            Panel row = new Panel { Height = 104, BackColor = QuotePageBg, Margin = new Padding(0, 0, 0, 12) };
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = QuotePageBg, Padding = new Padding(0, 0, 0, 6) };
            flow.Controls.Add(BuildLiveKpiCard("₹", "Quote Value (Incl. GST)", out _lblKpiQuoteValue, out Label quoteSub, CargoPurple));
            quoteSub.Text = "View breakdown →";
            flow.Controls.Add(BuildLiveKpiCard("↗", "Expected Margin", out _lblKpiMargin, out _lblKpiMarginSub, SaveGreen));
            flow.Controls.Add(BuildLiveKpiCard("CLIP", "Approval Status", out _lblKpiApproval, out _lblKpiApprovalSub, WarnOrange));
            flow.Controls.Add(BuildLiveKpiCard("◎", "Probability to Close", out _lblKpiProbability, out _lblKpiProbabilitySub, InfoBlue));
            flow.Controls.Add(BuildLiveKpiCard("CAL", "Validity", out _lblKpiValidity, out _lblKpiValiditySub, CargoPurple));
            flow.Controls.Add(BuildLiveKpiCard("CLK", "Last Saved", out _lblKpiSaved, out _lblKpiSavedSub, CargoPurple));
            row.Controls.Add(flow);
            return row;
        }

        private Panel BuildLiveKpiCard(string icon, string caption, out Label valueLabel, out Label subLabel, Color accent)
        {
            Panel card = MakeCard(178, 86);
            card.Margin = new Padding(0, 0, 10, 0);
            Label iconLabel = new Label { Text = icon, Location = new Point(12, 13), Size = new Size(42, 42), BackColor = DS.Lighten(accent, 0.82f), ForeColor = accent, Font = new Font("Segoe UI", 8, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(iconLabel, 8);
            card.Controls.Add(iconLabel);
            card.Controls.Add(new Label { Text = caption, Location = new Point(64, 12), Size = new Size(102, 18), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = QuoteMuted, AutoEllipsis = true });
            valueLabel = new Label { Text = "₹0.00", Location = new Point(64, 31), Size = new Size(104, 22), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = QuoteText, AutoEllipsis = true };
            subLabel = new Label { Text = "", Location = new Point(64, 56), Size = new Size(104, 18), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
            card.Controls.Add(valueLabel);
            card.Controls.Add(subLabel);
            return card;
        }

        private Panel BuildQuotationWorkflowCard()
        {
            Panel card = MakeCard(900, 132);
            card.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(new Label { Text = "Quotation Workflow", Location = new Point(18, 14), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = QuoteText });
            _workflowPanel = new Panel { Location = new Point(18, 42), Size = new Size(card.Width - 36, 82), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, BackColor = QuoteSurface };
            card.Controls.Add(_workflowPanel);
            card.Resize += (s, e) => { _workflowPanel.Width = card.Width - 36; RenderWorkflowStepper(); };
            return card;
        }

        private Panel BuildMainWorkspace()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = QuotePageBg };
            flow.Controls.Add(BuildKpiRow());
            flow.Controls.Add(BuildQuotationWorkflowCard());
            flow.Controls.Add(BuildQuoteDetailsCard());
            flow.Controls.Add(BuildModernLineItemsCard());
            flow.Controls.Add(BuildFollowUpCard());
            flow.Resize += (s, e) =>
            {
                int width = Math.Max(760, flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
                foreach (Control control in flow.Controls)
                    control.Width = width;
            };
            return flow;
        }

        private Panel BuildQuoteDetailsCard()
        {
            Panel card = MakeCard(900, 224);
            card.Margin = new Padding(0, 0, 0, 16);
            Label title = new Label { Text = "Quote Details", Location = new Point(18, 16), AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = QuoteText };
            _quoteDetailsStatusPill = new Label { Text = "DRAFT", Location = new Point(130, 17), Size = new Size(70, 22), BackColor = Color.FromArgb(219, 234, 254), ForeColor = InfoBlue, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(_quoteDetailsStatusPill, 7);
            card.Controls.Add(title);
            card.Controls.Add(_quoteDetailsStatusPill);

            TableLayoutPanel fields = new TableLayoutPanel { Location = new Point(18, 52), Width = card.Width - 36, Height = 156, ColumnCount = 5, RowCount = 3, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            _txtQuoteNo = MakeTextBox(false);
            _txtQuoteNo.Text = string.Empty;
            _cboClient = MakeCombo(true);
            _cboClient.SelectedIndexChanged += (s, e) => LoadSites();
            _cboSite = MakeCombo(true);
            _txtTitle = MakeTextBox(false);
            _txtTitle.Text = string.Empty;
            _cboValidity = MakeCombo(true);
            _cboValidity.Items.AddRange(new object[] { "15 Days", "30 Days", "45 Days", "60 Days", "90 Days" });
            _cboValidity.SelectedIndex = 1;
            _cboValidity.SelectedIndexChanged += (s, e) => UpdateDueDate();
            _cboStatus = MakeCombo(true);
            _cboStatus.Items.AddRange(new object[] { "Draft", "Material Check", "Approval", "Sent", "Accepted", "Converted" });
            _cboStatus.SelectedIndex = 0;
            _cboStatus.SelectedIndexChanged += (s, e) => { RefreshSummary(); RenderWorkflowStepper(); };
            _cboCommercialFlow = MakeCombo(true);
            _cboCommercialFlow.Items.AddRange(new object[] { "Revenue", "Revenue + Procurement", "Procurement Only" });
            _cboCommercialFlow.SelectedIndex = 0;
            _cboCustomerDocStatus = MakeCombo(true);
            _cboCustomerDocStatus.Items.AddRange(new object[] { "Quote Draft", "Quote Sent", "PO Received", "Quote Accepted", "Invoice Created", "Invoice Sent", "Payment Received", "Job Created", "Closed" });
            _cboCustomerDocStatus.SelectedIndex = 0;
            _cboSupplierDocStatus = MakeCombo(true);
            _cboSupplierDocStatus.Items.AddRange(new object[] { "Not Required", "Supplier Quote Needed", "Supplier Quote Sent", "Supplier Quote Received", "PO Draft", "PO Sent", "Materials Received", "Vendor Paid", "Closed" });
            _cboSupplierDocStatus.SelectedIndex = 0;
            _dtpDate = MakeDatePicker();
            _dtpDate.Value = DateTime.Today;
            _dtpDate.ValueChanged += (s, e) => UpdateDueDate();
            _dtpDue = MakeDatePicker();
            _dtpDue.Value = DateTime.Today.AddDays(30);
            _dtpRequiredBy = MakeDatePicker();
            _dtpRequiredBy.Value = DateTime.Today.AddDays(7);

            AddLabeledControl(fields, 0, 0, "Quote Number", _txtQuoteNo);
            AddLabeledControl(fields, 1, 0, "Client", _cboClient);
            AddLabeledControl(fields, 2, 0, "Site", _cboSite);
            AddLabeledControl(fields, 3, 0, "Project / Quote Title", _txtTitle);
            AddLabeledControl(fields, 0, 1, "Date", _dtpDate);
            AddLabeledControl(fields, 1, 1, "Due Date", _dtpDue);
            AddLabeledControl(fields, 2, 1, "Validity", _cboValidity);
            AddLabeledControl(fields, 3, 1, "Required By", _dtpRequiredBy);
            AddLabeledControl(fields, 4, 1, "Status", _cboStatus);
            AddLabeledControl(fields, 0, 2, "Commercial Flow", _cboCommercialFlow);
            AddLabeledControl(fields, 1, 2, "Customer Side", _cboCustomerDocStatus);
            AddLabeledControl(fields, 2, 2, "Supplier Side", _cboSupplierDocStatus);
            fields.SetColumnSpan(fields.GetControlFromPosition(2, 2), 2);
            card.Controls.Add(fields);

            card.Resize += (s, e) =>
            {
                fields.Width = card.Width - 36;
            };
            return card;
        }

        private Panel BuildWorkflowTracker()
        {
            Panel panel = MakeCard(820, 92);
            string[] titles = { "Draft", "Material Check", "Approval", "Sent", "Accepted", "Converted" };
            string[] subtitles = { "Quote created", "Verify stock \u00B7 prices", "Internal approval", "Sent to customer", "Customer accepted", "PO / Invoice / Job" };
            panel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(203, 213, 225), 2))
                    e.Graphics.DrawLine(pen, 80, 34, Math.Max(80, panel.Width - 80), 34);
            };
            panel.Resize += (s, e) => LayoutWorkflowSteps(panel, titles, subtitles);
            LayoutWorkflowSteps(panel, titles, subtitles);
            return panel;
        }

        private void LayoutWorkflowSteps(Panel panel, string[] titles, string[] subtitles)
        {
            panel.Controls.Clear();
            int count = titles.Length;
            int usable = Math.Max(600, panel.Width - 80);
            for (int i = 0; i < count; i++)
            {
                int x = 40 + (count == 1 ? 0 : (usable * i) / (count - 1));
                Color color = i == 0 ? InfoBlue : Color.FromArgb(226, 232, 240);
                Color text = i == 0 ? InfoBlue : DS.Slate600;
                Label dot = new Label { Text = i == 0 ? "/" : (i + 1).ToString(), Size = new Size(34, 34), Location = new Point(x - 17, 17), BackColor = color, ForeColor = i == 0 ? Color.White : DS.Slate700, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
                DS.Rounded(dot, 17);
                panel.Controls.Add(dot);
                panel.Controls.Add(new Label { Text = titles[i], Location = new Point(x - 54, 54), Size = new Size(108, 16), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = text, TextAlign = ContentAlignment.MiddleCenter });
                panel.Controls.Add(new Label { Text = subtitles[i], Location = new Point(x - 70, 70), Size = new Size(140, 15), Font = new Font("Segoe UI", 7.2f), ForeColor = QuoteMuted, TextAlign = ContentAlignment.MiddleCenter });
            }
        }

        private void RenderWorkflowStepper()
        {
            if (_workflowPanel == null)
                return;

            _workflowPanel.Controls.Clear();
            string[] titles = { "Draft", "Material Check", "Approval", "Sent", "Accepted", "Converted" };
            string[] subtitles = { "Quote created", "Check stock + cost", "Approval", "Customer quote sent", "Customer PO received", "Invoice / PO / Job" };
            int active = ResolveWorkflowStep(_cboStatus?.SelectedItem?.ToString());
            int width = Math.Max(640, _workflowPanel.ClientSize.Width);
            int usable = Math.Max(560, width - 90);
            for (int i = 0; i < titles.Length - 1; i++)
            {
                int x1 = 45 + usable * i / (titles.Length - 1);
                int x2 = 45 + usable * (i + 1) / (titles.Length - 1);
                Panel line = new Panel
                {
                    Location = new Point(x1 + 18, 16),
                    Size = new Size(Math.Max(20, x2 - x1 - 36), 2),
                    BackColor = i < active ? InfoBlue : Color.FromArgb(203, 213, 225)
                };
                _workflowPanel.Controls.Add(line);
            }

            for (int i = 0; i < titles.Length; i++)
            {
                int x = 45 + usable * i / (titles.Length - 1);
                bool complete = i < active;
                bool current = i == active;
                Label dot = new Label
                {
                    Text = (i + 1).ToString(CultureInfo.InvariantCulture),
                    Size = new Size(30, 30),
                    Location = new Point(x - 15, 2),
                    BackColor = complete || current ? InfoBlue : Color.White,
                    ForeColor = complete || current ? Color.White : QuoteMuted,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                DS.Rounded(dot, 15);
                dot.Paint += (s, e) =>
                {
                    if (!complete && !current)
                    {
                        using (Pen pen = new Pen(Color.FromArgb(203, 213, 225)))
                            e.Graphics.DrawEllipse(pen, 1, 1, dot.Width - 3, dot.Height - 3);
                    }
                };
                _workflowPanel.Controls.Add(dot);
                _workflowPanel.Controls.Add(new Label { Text = titles[i], Location = new Point(x - 52, 34), Size = new Size(104, 16), Font = new Font("Segoe UI", 7.4f, FontStyle.Bold), ForeColor = current ? InfoBlue : QuoteText, TextAlign = ContentAlignment.MiddleCenter });
                _workflowPanel.Controls.Add(new Label { Text = subtitles[i], Location = new Point(x - 64, 50), Size = new Size(128, 16), Font = new Font("Segoe UI", 6.8f), ForeColor = QuoteMuted, TextAlign = ContentAlignment.MiddleCenter });
            }
        }

        private static int ResolveWorkflowStep(string status)
        {
            switch ((status ?? "Draft").Trim())
            {
                case "Material Check": return 1;
                case "Approval": return 2;
                case "Sent": return 3;
                case "Accepted": return 4;
                case "Converted": return 5;
                default: return 0;
            }
        }

        private Panel BuildModernLineItemsCard()
        {
            Panel card = MakeCard(900, 520);
            card.Margin = new Padding(0, 0, 0, 16);

            Button addItem = MakeBtn("+  Add Item", InfoBlue, 130);
            Button addLabour = MakeOutlineBtn("+  Add Service Labour", 190);
            Button bulk = MakeOutlineBtn("Bulk Actions  v", 150);
            foreach (Button button in new[] { addItem, addLabour, bulk })
                button.Height = 42;

            Label filterLabel = new Label { Text = "Filter by Category", AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = QuoteMuted, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _cmbCategoryFilter = new ComboBox { Width = 150, Height = 32, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _cmbCategoryFilter.Items.Add("All");
            _cmbCategoryFilter.SelectedIndex = 0;
            _cmbCategoryFilter.SelectedIndexChanged += (s, e) => BindInventoryItems();
            Panel searchPanel = CreateSearchPanel();
            searchPanel.Location = new Point(card.Width - searchPanel.Width - 14, 16);
            addItem.Location = new Point(14, 16);
            addLabour.Location = new Point(156, 16);
            bulk.Location = new Point(358, 16);
            filterLabel.Location = new Point(searchPanel.Left - 158, 4);
            _cmbCategoryFilter.Location = new Point(searchPanel.Left - 158, 20);
            addItem.Click += (s, e) => AddLineItem();
            addLabour.Click += (s, e) => AddServiceLabourLine();
            bulk.Click += (s, e) => ShowBulkActionsMenu(bulk);
            card.Controls.AddRange(new Control[] { addItem, addLabour, bulk, filterLabel, _cmbCategoryFilter, searchPanel });

            Panel gridHost = new Panel
            {
                Name = "pnlLineItemsGridHost",
                Location = new Point(14, 126),
                Width = card.Width - 28,
                Height = 322,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                BackColor = Color.White
            };
            card.Controls.Add(gridHost);

            _grid = new DataGridView
            {
                Tag = "SkipGlobalGridTheme",
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Both,
                ColumnHeadersHeight = 58,
                RowTemplate = { Height = 62 }
            };
            StyleQuotationGrid(_grid);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sr", HeaderText = "#", Width = 36, MinimumWidth = 34, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "ItemDescription", HeaderText = "Item / Service", Width = 170, MinimumWidth = 150, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox, DisplayStyleForCurrentCellOnly = true, FlatStyle = FlatStyle.Standard });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", Width = 70, MinimumWidth = 66 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qty", Width = 42, MinimumWidth = 40 });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Unit", HeaderText = "Unit", Width = 45, MinimumWidth = 42, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing, FlatStyle = FlatStyle.Flat, DataSource = new[] { "Nos", "Mtr", "Kg", "Job", "Set", "Sqft", "Hrs", "Ltr", "Lot" } });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Supplier", HeaderText = "Supplier", Width = 80, MinimumWidth = 72, DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing, FlatStyle = FlatStyle.Flat });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostPerUnit", HeaderText = "Cost (" + "\u20B9" + ")", Width = 66, MinimumWidth = 62 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SellPrice", HeaderText = "Sell Price (" + "\u20B9" + ")", Width = 74, MinimumWidth = 68 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MarginPct", HeaderText = "Margin %", Width = 62, MinimumWidth = 58 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gst", HeaderText = "GST %", Width = 48, MinimumWidth = 46 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stock", HeaderText = "Stock", Width = 50, MinimumWidth = 48 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Shortfall", HeaderText = "Shortfall", Width = 64, MinimumWidth = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LineTotal", HeaderText = "Total (" + "\u20B9" + ")", Width = 76, MinimumWidth = 72 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actions", HeaderText = "Actions", Width = 50, MinimumWidth = 48, ReadOnly = true });
            foreach (DataGridViewColumn column in _grid.Columns)
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            foreach (string name in new[] { "Qty", "CostPerUnit", "SellPrice", "MarginPct", "Gst", "Stock", "Shortfall", "LineTotal" })
                _grid.Columns[name].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Sr"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns["Category"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns["Actions"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            SetFillWeight("Sr", 40);
            SetFillWeight("ItemDescription", 135);
            SetFillWeight("Category", 82);
            SetFillWeight("Qty", 46);
            SetFillWeight("Unit", 52);
            SetFillWeight("Supplier", 104);
            SetFillWeight("CostPerUnit", 72);
            SetFillWeight("SellPrice", 80);
            SetFillWeight("MarginPct", 68);
            SetFillWeight("Gst", 54);
            SetFillWeight("Stock", 54);
            SetFillWeight("Shortfall", 66);
            SetFillWeight("LineTotal", 94);
            SetFillWeight("Actions", 70);
            ApplyQuotationGridColumnSizing();
            _grid.CellEndEdit += async (s, e) => await HandleGridCellEndEditAsync(e.RowIndex, e.ColumnIndex);
            _grid.CellValueChanged += (s, e) => HandleGridValueChange(e.RowIndex, e.ColumnIndex);
            _grid.CellContentClick += (s, e) => HandleGridButtonClick(e.RowIndex, e.ColumnIndex);
            _grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "ItemDescription")
                    BeginEditItemCell(e.RowIndex, true);
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "Actions")
                    HandleGridButtonClick(e.RowIndex, e.ColumnIndex);
            };
            _grid.EditingControlShowing += Grid_EditingControlShowing;
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (!_updatingGrid && _grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.DataError += (s, e) => { e.ThrowException = false; e.Cancel = false; };
            gridHost.Controls.Add(_grid);
            _lblLineItemsEmptyState = new Label
            {
                Text = "No more items. Click + Add Item to continue.",
                AutoSize = false,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f),
                ForeColor = QuoteMuted,
                BackColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            gridHost.Controls.Add(_lblLineItemsEmptyState);
            _lblLineItemsEmptyState.BringToFront();

            _lblLineItemCount = new Label { Text = "Showing 0 to 0 of 0 items", Location = new Point(24, 476), AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = QuoteMuted, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            ComboBox pageSize = new ComboBox { Width = 86, Height = 34, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), Anchor = AnchorStyles.Bottom };
            pageSize.Items.AddRange(new object[] { "10", "25", "50" });
            pageSize.SelectedIndex = 0;
            Label perPage = new Label { Text = "per page", AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = QuoteMuted, Anchor = AnchorStyles.Bottom };
            Button first = MakeOutlineBtn("|<", 38);
            Button prev = MakeOutlineBtn("<", 38);
            Button current = MakeBtn("1", InfoBlue, 38);
            Button next = MakeOutlineBtn(">", 38);
            Button last = MakeOutlineBtn(">|", 38);
            foreach (Button nav in new[] { first, prev, current, next, last })
                nav.Height = 34;
            card.Controls.AddRange(new Control[] { _lblLineItemCount, pageSize, perPage, first, prev, current, next, last });
            pageSize.Location = new Point((card.Width / 2) - 50, card.Height - 48);
            perPage.Location = new Point(pageSize.Right + 12, card.Height - 40);
            int initialNavY = card.Height - 48;
            last.Location = new Point(card.Width - 52, initialNavY);
            next.Location = new Point(last.Left - 46, initialNavY);
            current.Location = new Point(next.Left - 46, initialNavY);
            prev.Location = new Point(current.Left - 46, initialNavY);
            first.Location = new Point(prev.Left - 46, initialNavY);
            card.Resize += (s, e) =>
            {
                int searchWidth = Math.Min(220, Math.Max(160, card.Width / 5));
                searchPanel.Size = new Size(searchWidth, 42);
                searchPanel.Location = new Point(card.Width - searchWidth - 14, 16);
                filterLabel.Location = new Point(searchPanel.Left - 158, 4);
                _cmbCategoryFilter.Location = new Point(searchPanel.Left - 158, 20);
                ApplyLineItemsGridLayout();
                _lblLineItemCount.Location = new Point(24, card.Height - 42);
                pageSize.Location = new Point((card.Width / 2) - 50, card.Height - 48);
                perPage.Location = new Point(pageSize.Right + 12, card.Height - 40);
                int navY = card.Height - 48;
                last.Location = new Point(card.Width - 52, navY);
                next.Location = new Point(last.Left - 46, navY);
                current.Location = new Point(next.Left - 46, navY);
                prev.Location = new Point(current.Left - 46, navY);
                first.Location = new Point(prev.Left - 46, navY);
            };
            return card;
        }

        private void ApplyLineItemsGridLayout()
        {
            if (_grid == null || _grid.Parent == null || _grid.IsDisposed)
                return;

            Control host = _grid.Parent;
            Control card = host.Parent ?? host;
            if (host.Name == "pnlLineItemsGridHost")
            {
                host.Location = new Point(14, 126);
                host.Size = new Size(Math.Max(300, card.ClientSize.Width - 28), Math.Max(220, card.ClientSize.Height - 182));
                _grid.Dock = DockStyle.Fill;
                _grid.Location = Point.Empty;
                LayoutLineItemsEmptyState(host);
            }
            else
            {
                _grid.Dock = DockStyle.None;
                _grid.Location = new Point(14, 126);
                _grid.Width = Math.Max(300, card.ClientSize.Width - 28);
                _grid.Height = Math.Max(220, card.ClientSize.Height - 182);
                _grid.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
                LayoutLineItemsEmptyState(card);
            }
        }

        private void LayoutLineItemsEmptyState(Control host)
        {
            if (_lblLineItemsEmptyState == null || host == null)
                return;

            int y = Math.Min(Math.Max(82, _grid.ColumnHeadersHeight + (_grid.RowTemplate?.Height ?? 30) + 22), Math.Max(82, host.Height - 50));
            _lblLineItemsEmptyState.Location = new Point(16, y);
            _lblLineItemsEmptyState.Width = Math.Max(200, host.ClientSize.Width - 32);
            _lblLineItemsEmptyState.BringToFront();
        }

        private void UpdateLineItemsEmptyState()
        {
            if (_lblLineItemsEmptyState == null)
                return;

            bool show = _lineItems.Count <= 1;
            _lblLineItemsEmptyState.Visible = show;
            if (show)
                LayoutLineItemsEmptyState(_lblLineItemsEmptyState.Parent);
            _grid?.Invalidate();
        }

        private Panel CreateSearchPanel()
        {
            Panel panel = new Panel { Width = 220, Height = 42, BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            DS.Rounded(panel, 9);
            panel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            Label icon = new Label
            {
                Text = "\uE721",
                Location = new Point(14, 11),
                Size = new Size(20, 20),
                Font = new Font("Segoe MDL2 Assets", 10f),
                ForeColor = QuoteMuted,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _txtItemSearch = new TextBox
            {
                Text = "Search items...",
                BorderStyle = BorderStyle.None,
                Location = new Point(42, 12),
                Width = panel.Width - 54,
                Height = 22,
                Font = new Font("Segoe UI", 9f),
                ForeColor = QuoteMuted,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            _txtItemSearch.GotFocus += (s, e) =>
            {
                if (_txtItemSearch.ForeColor == QuoteMuted && _txtItemSearch.Text == "Search items...")
                {
                    _txtItemSearch.Clear();
                    _txtItemSearch.ForeColor = QuoteText;
                }
            };
            _txtItemSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtItemSearch.Text))
                {
                    _txtItemSearch.Text = "Search items...";
                    _txtItemSearch.ForeColor = QuoteMuted;
                }
            };
            _txtItemSearch.TextChanged += (s, e) =>
            {
                if (!(_txtItemSearch.ForeColor == QuoteMuted && _txtItemSearch.Text == "Search items..."))
                    BindInventoryItems();
            };
            panel.Controls.Add(icon);
            panel.Controls.Add(_txtItemSearch);
            return panel;
        }

        private void SetFillWeight(string columnName, float weight)
        {
            if (_grid != null && _grid.Columns.Contains(columnName))
                _grid.Columns[columnName].FillWeight = weight;
        }

        private Panel BuildFollowUpCard()
        {
            Panel card = MakeCard(900, 150);
            card.Margin = new Padding(0, 0, 0, 20);
            card.Controls.Add(new Label { Text = "Customer Follow-up", Location = new Point(18, 16), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = QuoteText });
            TableLayoutPanel grid = new TableLayoutPanel { Location = new Point(18, 50), Width = card.Width - 36, Height = 80, ColumnCount = 5, RowCount = 1, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            for (int i = 0; i < 5; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i == 4 ? 32 : 17));
            AddReadonlyField(grid, 0, "Follow-up Date", "20/05/2026");
            AddReadonlyField(grid, 1, "Last Contacted", "15/05/2026");
            AddReadonlyField(grid, 2, "Next Action", "Send Technical Drawings");
            AddReadonlyField(grid, 3, "Status", "Sent");
            AddReadonlyField(grid, 4, "Notes", "Customer requested revised layout. Awaiting confirmation.");
            card.Controls.Add(grid);
            card.Resize += (s, e) => grid.Width = card.Width - 36;
            return card;
        }

        private Panel BuildRightSummaryPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = QuotePageBg,
                ColumnCount = 1,
                RowCount = 4
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 27f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 22f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 46f));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 5f));

            Panel summary = BuildQuoteSummaryCard();
            Panel alerts = BuildSmartAlertsCard();
            Panel actions = BuildQuickActionsCard();
            summary.Dock = DockStyle.Fill;
            alerts.Dock = DockStyle.Fill;
            actions.Dock = DockStyle.Fill;
            summary.Margin = new Padding(0, 0, 0, 10);
            alerts.Margin = new Padding(0, 0, 0, 10);
            actions.Margin = new Padding(0, 0, 0, 8);

            _lblLastSaved = new Label { Text = "Last saved: 05/05/2026 11:30 AM        Refresh", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = InfoBlue, TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand };
            _lblLastSaved.Click += async (s, e) => await InitializeAsync();
            panel.Controls.Add(summary, 0, 0);
            panel.Controls.Add(alerts, 0, 1);
            panel.Controls.Add(actions, 0, 2);
            panel.Controls.Add(_lblLastSaved, 0, 3);
            return panel;
        }

        private Panel BuildQuoteSummaryCard()
        {
            Panel card = MakeCard(310, 188);
            card.Margin = new Padding(0);
            card.Controls.Add(new Label { Text = "Quote Summary", Location = new Point(16, 12), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = QuoteText });
            _lblTaxable = AddSummaryRow(card, "Subtotal (Excl. GST)", IndiaFormatHelper.FormatCurrency(105805.08m), 36, false);
            _lblGst = AddSummaryRow(card, "GST (18%)", IndiaFormatHelper.FormatCurrency(19044.92m), 58, false);
            card.Controls.Add(new Label { Text = "Discount (%)", Location = new Point(16, 82), AutoSize = false, Width = 118, Height = 20, Font = new Font("Segoe UI", 8.5f), ForeColor = QuoteText });
            _numDiscount = new NumericUpDown { Location = new Point(150, 78), Width = 128, DecimalPlaces = 2, Maximum = 9999999, Font = new Font("Segoe UI", 8.5f), TextAlign = HorizontalAlignment.Right, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _numDiscount.ValueChanged += (s, e) => RefreshSummary();
            card.Controls.Add(_numDiscount);
            _lblTotal = AddSummaryRow(card, "Total (Incl. GST)", IndiaFormatHelper.FormatCurrency(124850m), 110, true);
            _lblMargin = AddSummaryRow(card, "Gross Margin", "28.65%", 136, false);
            _lblMargin.ForeColor = SaveGreen;
            card.Resize += (s, e) =>
            {
                if (_numDiscount != null)
                {
                    _numDiscount.Width = Math.Max(112, card.ClientSize.Width - 182);
                    _numDiscount.Left = Math.Max(150, card.ClientSize.Width - _numDiscount.Width - 32);
                }
            };
            return card;
        }

        private Panel BuildSmartAlertsCard()
        {
            Panel card = MakeCard(310, 142);
            card.Margin = new Padding(0);
            card.Controls.Add(new Label { Text = "Smart Alerts", Location = new Point(16, 12), AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = QuoteText });
            _lblAlertShortfall = AddAlert(card, 34, "!", "1 item has stock shortfall", WarnOrange);
            _lblAlertMargin = AddAlert(card, 52, "!", "Low margin on 1 item", WarnOrange);
            _lblAlertExpiry = AddAlert(card, 70, "i", "Quote validity expires in 30 days", InfoBlue);
            _lblAlertSupplier = AddAlert(card, 88, "OK", "All items have supplier prices", SaveGreen);
            _lblAlertSite = AddAlert(card, 106, "OK", "Client site is selected", SaveGreen);
            return card;
        }

        private Panel BuildQuickActionsCard()
        {
            Panel card = MakeCard(310, 362);
            card.Margin = new Padding(0, 0, 0, 14);
            card.Controls.Add(new Label { Text = "Quick Actions", Location = new Point(16, 16), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = QuoteText });
            _btnSaveQuote = MakeBtn("Save Draft", SaveGreen, 270);
            Button approval = MakeBtn("Send for Approval", InfoBlue, 270);
            Button actions = MakeOutlineBtn("Open Quote Actions", 270);
            Label hint = new Label
            {
                Text = "PDFs, supplier POs, invoices, jobs,\r\nWhatsApp follow-ups, and delete live here.",
                Location = new Point(18, 150),
                Size = new Size(270, 70),
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = QuoteMuted
            };
            Button[] buttons = { _btnSaveQuote, approval, actions };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Height = 34;
                buttons[i].Location = new Point(18, 44 + (i * 40));
            }
            RegisterFilledButton(_btnSaveQuote, SaveGreen);
            RegisterFilledButton(approval, InfoBlue);
            RegisterSecondaryButton(actions);
            _btnSaveQuote.Click += async (s, e) => await SaveAsync();
            approval.Click += (s, e) => SendForApproval();
            actions.Click += (s, e) => ShowQuoteActionsMenu(actions);
            card.Controls.AddRange(buttons);
            card.Controls.Add(hint);
            card.Resize += (s, e) =>
            {
                int width = Math.Max(180, card.ClientSize.Width - 36);
                foreach (Button button in buttons)
                    button.Width = width;
                hint.Width = width;
            };
            return card;
        }

        private void ShowQuoteActionsMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddQuoteAction(menu, "Generate PDF", () => PrintQuotationToPdf());
            AddQuoteAction(menu, "Send Supplier PO", CreatePurchaseOrdersAsync);
            AddQuoteAction(menu, "Create Customer Invoice", CreateInvoiceAsync);
            AddQuoteAction(menu, "Create Revenue Job", CreateDispatchJobAsync);
            AddQuoteAction(menu, "WhatsApp Follow-up", () => ShowQuotationWhatsAppAction());
            menu.Items.Add(new ToolStripSeparator());
            AddQuoteAction(menu, "Delete Quote", DeleteCurrentQuoteAsync);
            menu.Show(owner, new Point(0, owner.Height + 2));
        }

        private static void AddQuoteAction(ContextMenuStrip menu, string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        private static void AddQuoteAction(ContextMenuStrip menu, string text, Func<Task> action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += async (s, e) => await action();
            menu.Items.Add(item);
        }

        private void ShowQuotationWhatsAppAction()
        {
            TenderBid bid = _current ?? BuildDraftHeaderOnly();
            ComboItem selectedClient = _cboClient.SelectedItem as ComboItem;
            B2BClient client = selectedClient == null ? null : selectedClient.Tag as B2BClient;
            string clientName = !string.IsNullOrWhiteSpace(bid.ClientName) ? bid.ClientName : (selectedClient == null ? "Customer" : selectedClient.Text);
            string quoteNo = string.IsNullOrWhiteSpace(bid.QuotationNumber) ? "the quotation" : bid.QuotationNumber;
            string amount = IndiaFormatHelper.FormatCurrency(bid.TotalWithGST > 0 ? bid.TotalWithGST : bid.BidValue);
            string message = "Hi " + clientName + ",\r\n\r\nFollowing up on quotation " + quoteNo + " for " + amount + ". Please review and confirm if we should proceed.\r\n\r\nRegards,\r\nServoERP";

            WhatsAppQuickActionDialog.ShowFor(this, new WhatsAppQuickActionContext
            {
                Module = "Quotations",
                SourceId = bid.BidID,
                ContactName = clientName,
                Phone = client == null ? string.Empty : client.Phone,
                TemplateType = "Quotation follow-up",
                Message = message,
                LinkedRecordType = "Quotation",
                LinkedRecord = quoteNo,
                LinkedRecordId = bid.BidID
            });
        }

        private Panel BuildRightPanel()
        {
            Panel right = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = QuotePageBg, Padding = new Padding(24, 20, 24, 24) };
            Panel page = new Panel { Dock = DockStyle.Top, Width = 950, Height = 1390, BackColor = QuoteSurface, Padding = new Padding(24) };
            DS.Rounded(page, 12);
            page.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, page.Width - 1, page.Height - 1);
            };
            right.Controls.Add(page);

            int y = 10;
            page.Controls.Add(new Label
            {
                Text = "Customer quotation details",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = QuoteText,
                Location = new Point(0, y),
                AutoSize = true
            });
            page.Controls.Add(new Label
            {
                Text = "Build a quote, confirm material readiness, and push the won quote into purchase, dispatch, and invoice workflows.",
                Font = new Font("Segoe UI", 9),
                ForeColor = QuoteMuted,
                Location = new Point(0, y + 30),
                AutoSize = true
            });
            y += 62;

            Panel tracking = BuildTrackingStrip();
            tracking.Location = new Point(0, y);
            page.Controls.Add(tracking);
            y += tracking.Height + 16;

            Panel headerGrid = BuildHeaderSection();
            headerGrid.Location = new Point(0, y);
            page.Controls.Add(headerGrid);
            y += headerGrid.Height + 16;

            Panel lineSection = BuildLineSection();
            lineSection.Location = new Point(0, y);
            page.Controls.Add(lineSection);
            y += lineSection.Height + 16;

            Panel summary = BuildSummarySection();
            summary.Location = new Point(0, y);
            page.Controls.Add(summary);
            y += summary.Height + 16;

            Panel suggestions = BuildSuggestionsSection();
            suggestions.Location = new Point(0, y);
            page.Controls.Add(suggestions);
            y += suggestions.Height + 16;

            Panel notes = BuildNotesSection();
            notes.Location = new Point(0, y);
            page.Controls.Add(notes);
            y += notes.Height + 16;

            Panel footer = BuildFooterSection();
            footer.Location = new Point(0, y);
            page.Controls.Add(footer);
            return right;
        }
        private Panel BuildHeaderSection()
        {
            Panel panel = CreateSectionPanel("Customer and quote details", 900, 184);
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Location = new Point(14, 38),
                Width = 860,
                Height = 130,
                ColumnCount = 3,
                RowCount = 3
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int i = 0; i < 3; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _txtQuoteNo = MakeTextBox(false);
            _cboClient = MakeCombo(true);
            _cboClient.SelectedIndexChanged += (s, e) => LoadSites();
            _cboSite = MakeCombo(true);
            _txtTitle = MakeTextBox(false);
            _cboValidity = MakeCombo(true);
            _cboValidity.Items.AddRange(new object[] { "10 Days", "30 Days", "60 Days", "90 Days" });
            _cboValidity.SelectedIndex = 1;
            _cboValidity.SelectedIndexChanged += (s, e) => UpdateDueDate();
            _cboStatus = MakeCombo(true);
            _cboStatus.Items.AddRange(new object[] { "Draft", "Analysed", "Sent", "Won", "Lost" });
            _cboStatus.SelectedIndex = 0;
            _dtpDate = MakeDatePicker();
            _dtpDate.ValueChanged += (s, e) => UpdateDueDate();
            _dtpDue = MakeDatePicker();
            _dtpRequiredBy = MakeDatePicker();

            AddLabeledControl(grid, 0, 0, "Quote Number", _txtQuoteNo);
            AddLabeledControl(grid, 1, 0, "Client", _cboClient);
            AddLabeledControl(grid, 2, 0, "Site", _cboSite);
            AddLabeledControl(grid, 0, 1, "Project / Quote", _txtTitle);
            AddLabeledControl(grid, 1, 1, "Validity", _cboValidity);
            AddLabeledControl(grid, 2, 1, "Status", _cboStatus);
            AddLabeledControl(grid, 0, 2, "Date", _dtpDate);
            AddLabeledControl(grid, 1, 2, "Due Date", _dtpDue);
            AddLabeledControl(grid, 2, 2, "Required By", _dtpRequiredBy);
            panel.Controls.Add(grid);
            return panel;
        }

        private Panel BuildTrackingStrip()
        {
            Panel panel = new Panel { Width = 900, Height = 94, BackColor = QuoteSurface };
            DS.Rounded(panel, 12);
            panel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            AddTrackingStep(panel, new Point(16, 16), 204, "01", "Quote details", "Customer, site, validity", InfoBlue);
            AddTrackingStep(panel, new Point(236, 16), 204, "02", "Material readiness", "Supplier and stock check", SaveGreen);
            AddTrackingStep(panel, new Point(456, 16), 204, "03", "Operations handoff", "PO and dispatch prep", WarnOrange);
            AddTrackingStep(panel, new Point(676, 16), 204, "04", "Invoice close", "Convert quote to billing", CargoPurple);
            return panel;
        }

        private static void AddTrackingStep(Control parent, Point location, int width, string step, string title, string subtitle, Color accent)
        {
            Panel card = new Panel { Location = location, Size = new Size(width, 62), BackColor = Color.FromArgb(249, 250, 251) };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            Label dot = new Label
            {
                Text = step,
                Location = new Point(10, 14),
                Size = new Size(34, 34),
                BackColor = accent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(dot, 17);
            card.Controls.Add(dot);
            card.Controls.Add(new Label { Text = title, Location = new Point(54, 10), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = QuoteText });
            card.Controls.Add(new Label { Text = subtitle, Location = new Point(54, 33), Size = new Size(width - 64, 20), Font = new Font("Segoe UI", 8), ForeColor = QuoteMuted });
            parent.Controls.Add(card);
        }

        private Panel BuildLineSection()
        {
            Panel panel = CreateSectionPanel("Quotation line items", 900, 450);

            // â”€â”€ Category filter bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            var filterBar = new Panel { Location = new Point(14, 40), Width = 860, Height = 30 };
            filterBar.Controls.Add(new Label
            {
                Text = "Filter by Category:",
                Location = new Point(0, 7),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = QuoteMuted
            });
            _cmbCategoryFilter = new ComboBox
            {
                Location = new Point(128, 4),
                Width = 180,
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbCategoryFilter.Items.Add("All");
            _cmbCategoryFilter.SelectedIndex = 0;
            _cmbCategoryFilter.SelectedIndexChanged += (s, e) => BindInventoryItems();
            filterBar.Controls.Add(_cmbCategoryFilter);
            panel.Controls.Add(filterBar);

            _grid = new DataGridView
            {
                Location = new Point(14, 80),
                Width = 860,
                Height = 320,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                ColumnHeadersHeight = 34,
                RowTemplate = { Height = 32 }
            };
            StyleQuotationGrid(_grid);
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "ItemDescription", HeaderText = "Item", Width = 220, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox, FlatStyle = FlatStyle.Standard });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qty", Width = 60 });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Unit", HeaderText = "Unit", Width = 60, DataSource = new[] { "Nos", "Mtr", "Kg", "Job", "Set", "Sqft", "Hrs", "Ltr" } });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Supplier", HeaderText = "Supplier", Width = 150, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox, FlatStyle = FlatStyle.Standard });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CostPerUnit", HeaderText = "Cost/Unit", Width = 75 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SellPrice", HeaderText = "Sell Price", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MarginPct", HeaderText = "Margin%", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stock", HeaderText = "Stock", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Shortfall", HeaderText = "Shortfall", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HsnSac", HeaderText = "HSN/SAC", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gst", HeaderText = "GST%", Width = 50 });
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Delete", HeaderText = "", Width = 30, Text = "X", UseColumnTextForButtonValue = true });
            ApplyQuotationGridColumnSizing();
            _grid.CellEndEdit += async (s, e) => await HandleGridCellEndEditAsync(e.RowIndex, e.ColumnIndex);
            _grid.CellValueChanged += (s, e) => HandleGridValueChange(e.RowIndex, e.ColumnIndex);
            _grid.CellContentClick += (s, e) => HandleGridButtonClick(e.RowIndex, e.ColumnIndex);
            _grid.EditingControlShowing += Grid_EditingControlShowing;
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (!_updatingGrid && _grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.DataError += (s, e) => { e.ThrowException = false; e.Cancel = false; };
            panel.Controls.Add(_grid);

            Button addRow = MakeBtn("+  Add item", InfoBlue, 120);
            addRow.Location = new Point(14, 408);
            addRow.Click += (s, e) => AddLineItem();
            panel.Controls.Add(addRow);
            return panel;
        }

        private Panel BuildSummarySection()
        {
            Panel panel = CreateSectionPanel("Commercial summary", 900, 108);
            _lblTaxable = MakeMetricCard(panel, new Point(14, 40), 190, "Taxable Value");
            _lblGst = MakeMetricCard(panel, new Point(230, 40), 190, "GST");
            _lblTotal = MakeMetricCard(panel, new Point(446, 40), 190, "Total incl. GST");
            _lblMargin = MakeMetricCard(panel, new Point(662, 40), 190, "Avg Margin %");
            return panel;
        }

        private Panel BuildSuggestionsSection()
        {
            Panel panel = CreateSectionPanel("Tracking and pricing alerts", 900, 228);
            Button refresh = MakeBtn("Refresh", DS.Slate600, 88);
            refresh.Location = new Point(780, 8);
            refresh.Click += (s, e) => RefreshSuggestions();
            panel.Controls.Add(refresh);
            _suggestionFlow = new FlowLayoutPanel
            {
                Location = new Point(14, 40),
                Width = 860,
                Height = 170,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = QuoteSurface
            };
            panel.Controls.Add(_suggestionFlow);
            return panel;
        }

        private Panel BuildNotesSection()
        {
            Panel panel = CreateSectionPanel("Commercial notes", 900, 140);
            _txtNotes = new TextBox
            {
                Location = new Point(14, 40),
                Width = 860,
                Height = 80,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };
            panel.Controls.Add(_txtNotes);
            return panel;
        }

        private Panel BuildFooterSection()
        {
            Panel panel = new Panel { Width = 900, Height = 52, BackColor = Color.White };
            Button btnCancel = MakeBtn("Cancel", Color.FromArgb(100, 100, 100), 92);
            Button btnPreview = MakeBtn("Preview", InfoBlue, 92);
            Button btnPdf = MakeBtn("Print PDF", Color.FromArgb(55, 65, 81), 102);
            _btnSaveQuote = MakeBtn("Save", SaveGreen, 92);
            btnCancel.Location = new Point(498, 8);
            btnPreview.Location = new Point(598, 8);
            btnPdf.Location = new Point(698, 8);
            _btnSaveQuote.Location = new Point(810, 8);
            btnCancel.Click += (s, e) => PopulateCurrent(null);
            btnPreview.Click += (s, e) => PreviewQuotation();
            btnPdf.Click += (s, e) => PrintQuotationToPdf();
            _btnSaveQuote.Click += async (s, e) => await SaveAsync();
            panel.Controls.AddRange(new Control[] { btnCancel, btnPreview, btnPdf, _btnSaveQuote });
            return panel;
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("Quotations", this, _btnNewQuote, _btnSaveQuote, null);
            EnableQuotationLineEditing();
        }

        private void EnableQuotationLineEditing()
        {
            if (_grid == null)
                return;

            _grid.ReadOnly = false;
            foreach (DataGridViewColumn column in _grid.Columns)
            {
                column.ReadOnly = column.Name == "Sr"
                    || column.Name == "Edit"
                    || column.Name == "Delete"
                    || column.Name == "Actions";
            }
        }

        private async Task LoadFromTemplateAsync()
        {
            try
            {
                int bidId = await EnsureSavedAsync();
                List<QuoteTemplate> templates = _svc.GetQuoteTemplates();
                using (var dlg = new TemplatePickerDialog(templates))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedTemplateId <= 0)
                        return;
                    SetStatus("Loading template...", InfoBlue);
                    _current = await Task.Run(() => _svc.LoadTemplateIntoTender(bidId, dlg.SelectedTemplateId));
                }
                PopulateCurrent(_current);
                await RefreshListsAsync();
                SetStatus("Template loaded and analysed.", SaveGreen);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
            }
        }

        private async Task CreatePurchaseOrdersAsync()
        {
            try
            {
                int bidId = await EnsureSavedAsync();
                List<string> poNumbers = await Task.Run(() => _svc.CreatePOsFromQuotation(bidId));
                _current = await Task.Run(() => _svc.GetByIdDetailed(bidId));
                PopulateCurrent(_current);
                await RefreshListsAsync();
                MessageBox.Show(poNumbers.Count == 0 ? "No supplier PO was required." : "Supplier PO sent/created:\n\n" + string.Join("\n", poNumbers), "Supplier Purchase Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus(poNumbers.Count == 0 ? "No supplier-side PO required." : "Supplier-side purchase order created.", SaveGreen);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
            }
        }

        private async Task CreateInvoiceAsync()
        {
            try
            {
                int bidId = await EnsureSavedAsync();
                Invoice invoice = await Task.Run(() => _svc.CreateInvoiceFromQuotation(bidId));
                string templateCopy = _docTemplateSvc.CopyQuotationTemplateForDocument(invoice.InvoiceNumber, ResolveDocumentOutputFolder());
                _current = await Task.Run(() => _svc.GetByIdDetailed(bidId));
                PopulateCurrent(_current);
                await RefreshListsAsync();
                string templateLine = string.IsNullOrWhiteSpace(templateCopy) ? "" : "\n\nCompany PDF copy:\n" + templateCopy;
                MessageBox.Show("Customer invoice generated.\n\nInvoice: " + invoice.InvoiceNumber + "\nAmount: " + IndiaFormatHelper.FormatCurrency(invoice.TotalAmount) + templateLine, "Customer Invoice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("Customer-side invoice draft generated from quotation.", SaveGreen);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
            }
        }
        private async Task SaveAsync()
        {
            try
            {
                TenderBid bid = CollectBidFromForm();
                string previousStatus = _current?.Status;
                SetStatus("Saving quotation...", InfoBlue);
                int bidId = await Task.Run(() => _svc.SaveTenderBid(bid));
                _current = await Task.Run(() => _svc.GetByIdDetailed(bidId));
                PopulateCurrent(_current);
                await RefreshListsAsync();
                if ((_current.Status == "Won" || _current.Status == "Lost") && !string.Equals(previousStatus, _current.Status, StringComparison.OrdinalIgnoreCase))
                    await Task.Run(() => _svc.RecordClientPriceMemory(_current.BidID, _current.Status == "Won"));
                SetStatus("Quotation saved.", SaveGreen);
            }
            catch (Exception ex)
            {
                SetStatus("Save error: " + ex.Message, Color.Firebrick);
            }
        }

        private async Task<int> EnsureSavedAsync()
        {
            TenderBid bid = CollectBidFromForm();
            if (_current != null && _current.BidID > 0)
                bid.BidID = _current.BidID;
            int bidId = await Task.Run(() => _svc.SaveTenderBid(bid));
            _current = await Task.Run(() => _svc.GetByIdDetailed(bidId));
            PopulateCurrent(_current);
            return bidId;
        }

        private async Task RefreshListsAsync()
        {
            var quotes = await Task.Run(() => _svc.GetAll().OrderByDescending(q => q.RequiredByDate ?? q.DueDate).Take(80).ToList());
            BindQuoteList(quotes);
            BindRenewalAlerts();
            RefreshQuotationDashboardSafe();
        }

        private async Task DeleteCurrentQuoteAsync()
        {
            if (_current == null || _current.BidID <= 0)
            {
                SetStatus("Select a saved quotation to delete.", WarnOrange);
                return;
            }

            string quoteNo = string.IsNullOrWhiteSpace(_current.QuotationNumber) ? "this quotation" : _current.QuotationNumber;
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
                await Task.Run(() => _svc.Delete(_current.BidID));
                NewRecord();
                await RefreshListsAsync();
                SetStatus("Quotation deleted.", Color.FromArgb(220, 38, 38));
            }
            catch (Exception ex)
            {
                SetStatus("Delete failed: " + ex.Message, Color.Firebrick);
                MessageBox.Show("Delete quotation failed: " + ex.Message, "Delete Quotation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BindClients()
        {
            _cboClient.Items.Clear();
            foreach (B2BClient client in _clients)
                _cboClient.Items.Add(new ComboItem { Id = client.ClientID, Text = client.CompanyName, Tag = client });
            UIHelper.ShowEmptyClientsMessageIfNeeded(FindForm(), _clients, "TenderBidForm.BindClients");
        }

        private void LoadSites()
        {
            _cboSite.Items.Clear();
            ComboItem client = _cboClient.SelectedItem as ComboItem;
            if (client == null)
                return;
            foreach (ClientSite site in _siteSvc.GetAll().Where(s => s.ClientID == client.Id).OrderBy(s => s.SiteName))
                _cboSite.Items.Add(new ComboItem { Id = site.SiteID, Text = SiteService.GetDisplayName(site), Tag = site });
            if (_cboSite.Items.Count > 0)
                _cboSite.SelectedIndex = 0;
        }

        private void BindQuoteList(IEnumerable<TenderBid> quotes)
        {
            _quoteFlow.SuspendLayout();
            _quoteFlow.Controls.Clear();
            foreach (TenderBid quote in quotes ?? Enumerable.Empty<TenderBid>())
                _quoteFlow.Controls.Add(MakeQuoteCard(quote));
            _quoteFlow.ResumeLayout(true);
        }

        private void BindRenewalAlerts()
        {
            _renewalFlow.SuspendLayout();
            _renewalFlow.Controls.Clear();
            foreach (TenderBid quote in _svc.GetRenewalAlerts())
            {
                Panel item = new Panel { Width = 248, Height = 34, BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
                item.Controls.Add(new Label { Text = quote.QuotationNumber, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(0, 2), AutoSize = true });
                item.Controls.Add(new Label { Text = IndiaFormatHelper.FormatDate(quote.DueDate), Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(185, 28, 28), Location = new Point(164, 3), AutoSize = true });
                item.Controls.Add(new Label { Text = (quote.ClientName ?? "Client") + " / " + (quote.SiteName ?? "Site"), Font = new Font("Segoe UI", 8), ForeColor = DS.Slate500, Location = new Point(0, 18), AutoSize = true });
                _renewalFlow.Controls.Add(item);
            }
            _renewalFlow.ResumeLayout(true);
        }

        private Panel MakeQuoteCard(TenderBid quote)
        {
            Color accent = ResolveQuoteAccent(quote);
            Panel card = new Panel
            {
                Width = 300,
                Height = 94,
                BackColor = QuoteSurface,
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
                Padding = new Padding(14, 12, 14, 12),
                Tag = quote
            };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                using (SolidBrush brush = new SolidBrush(accent))
                    e.Graphics.FillRectangle(brush, 0, 0, 4, card.Height);
                using (Pen border = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
            };

            Label lblNo = new Label { Text = string.IsNullOrWhiteSpace(quote.QuotationNumber) ? "Draft Quote" : quote.QuotationNumber, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = QuoteText, AutoSize = true, Location = new Point(14, 8) };
            Label lblClient = new Label { Text = ((quote.ClientName ?? "") + " / " + (quote.SiteName ?? "")).Trim(' ', '/'), Font = new Font("Segoe UI", 8.5f), ForeColor = QuoteMuted, Location = new Point(14, 32), Size = new Size(210, 18) };
            Label lblTitle = new Label { Text = string.IsNullOrWhiteSpace(quote.TenderName) ? (quote.ItemName ?? "Untitled quotation") : quote.TenderName, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700, Location = new Point(14, 54), Size = new Size(220, 18) };
            Label lblStatus = new Label
            {
                Text = (quote.Status ?? "Draft").ToUpperInvariant(),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = accent,
                BackColor = DS.Lighten(accent, 0.88f),
                AutoSize = false,
                Size = new Size(72, 22),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(lblStatus, 11);
            lblStatus.Location = new Point(card.Width - lblStatus.Width - 14, 8);
            Label lblDue = new Label { Text = "Due " + IndiaFormatHelper.FormatDate(quote.DueDate), Font = new Font("Segoe UI", 8), ForeColor = QuoteMuted, AutoSize = true, Location = new Point(card.Width - 102, 54) };

            foreach (Control control in new Control[] { card, lblNo, lblClient, lblTitle, lblStatus, lblDue })
                control.Click += async (s, e) => await SelectQuoteAsync(quote, card);

            card.Controls.Add(lblNo);
            card.Controls.Add(lblClient);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblStatus);
            card.Controls.Add(lblDue);
            return card;
        }

        private async Task SelectQuoteAsync(TenderBid quote, Panel card)
        {
            if (_selectedCard != null)
                HighlightCard(_selectedCard, false);
            _selectedCard = card;
            HighlightCard(card, true);
            SetStatus("Loading quotation...", InfoBlue);
            _current = await Task.Run(() => _svc.GetByIdDetailed(quote.BidID));
            PopulateCurrent(_current);
            ShowQuotationEditor();
            SetStatus("Quotation loaded.", DS.Slate500);
        }

        private void HighlightCard(Panel card, bool selected)
        {
            card.BackColor = selected ? Color.FromArgb(239, 246, 255) : QuoteSurface;
            card.Padding = selected ? new Padding(14, 11, 14, 11) : new Padding(14, 12, 14, 12);
            card.Invalidate();
        }

        private static Color ResolveQuoteAccent(TenderBid quote)
        {
            string status = (quote?.Status ?? "Draft").Trim();
            if (string.Equals(status, "Won", StringComparison.OrdinalIgnoreCase))
                return SaveGreen;
            if (string.Equals(status, "Lost", StringComparison.OrdinalIgnoreCase))
                return DS.Red600;
            if (quote != null && quote.DueDate.Date < DateTime.Today)
                return DS.Red600;
            if (quote != null && quote.DueDate.Date <= DateTime.Today.AddDays(7))
                return WarnOrange;
            if (string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase))
                return CargoPurple;
            return InfoBlue;
        }

        private void PopulateCurrent(TenderBid bid)
        {
            if (bid == null)
            {
                NewRecord();
                return;
            }

            _txtQuoteNo.Text = bid.QuotationNumber ?? string.Empty;
            _txtTitle.Text = bid.TenderName ?? string.Empty;
            SelectCombo(_cboClient, bid.ClientID);
            LoadSites();
            SelectCombo(_cboSite, bid.SiteID);
            SelectComboByText(_cboStatus, NormalizeWorkflowStatus(bid.Status));
            SelectComboByText(_cboCommercialFlow, string.IsNullOrWhiteSpace(bid.CommercialFlow) ? "Revenue" : bid.CommercialFlow);
            SelectComboByText(_cboCustomerDocStatus, string.IsNullOrWhiteSpace(bid.CustomerDocumentStatus) ? "Quote Draft" : bid.CustomerDocumentStatus);
            SelectComboByText(_cboSupplierDocStatus, string.IsNullOrWhiteSpace(bid.SupplierDocumentStatus) ? "Not Required" : bid.SupplierDocumentStatus);
            SelectComboByText(_cboValidity, Math.Max(1, (bid.DueDate.Date - (bid.SubmittedDate ?? DateTime.Today).Date).Days) + " Days");
            _dtpDate.Value = bid.SubmittedDate ?? DateTime.Today;
            _dtpDue.Value = bid.DueDate == default(DateTime) ? DateTime.Today.AddDays(30) : bid.DueDate;
            _dtpRequiredBy.Value = bid.RequiredByDate ?? DateTime.Today.AddDays(7);
            _txtNotes.Text = bid.Notes ?? string.Empty;
            _lineItems.Clear();
            _lineItems.AddRange(bid.LineItems ?? new List<TenderBidLineItem>());
            if (_lineItems.Count == 0)
                _lineItems.Add(new TenderBidLineItem { Quantity = 1m, Unit = "Nos", GSTRatePct = 18m });
            RefreshGrid();
            RefreshSummary();
            RefreshSuggestions(bid.Suggestions ?? _svc.GenerateSuggestions(bid));
        }

        private void NewRecord(bool showEditor = true)
        {
            _current = null;
            if (_selectedCard != null)
                HighlightCard(_selectedCard, false);
            _selectedCard = null;
            _txtQuoteNo.Text = string.Empty;
            _txtTitle.Text = string.Empty;
            if (_cboClient.Items.Count > 0) _cboClient.SelectedIndex = 0;
            LoadSites();
            if (_cboSite.Items.Count > 0) _cboSite.SelectedIndex = 0;
            _cboValidity.SelectedIndex = 1;
            _cboStatus.SelectedIndex = 0;
            if (_cboCommercialFlow != null) _cboCommercialFlow.SelectedIndex = 0;
            if (_cboCustomerDocStatus != null) _cboCustomerDocStatus.SelectedIndex = 0;
            if (_cboSupplierDocStatus != null) _cboSupplierDocStatus.SelectedIndex = 0;
            _txtQuoteNo.Text = GenerateNextQuoteNumber();
            _dtpDate.Value = DateTime.Today;
            UpdateDueDate();
            _dtpRequiredBy.Value = DateTime.Today.AddDays(7);
            if (_txtNotes != null)
                _txtNotes.Text = string.Empty;
            _lineItems.Clear();
            _lineItems.Add(new TenderBidLineItem { Quantity = 1m, Unit = "Nos", GSTRatePct = 18m });
            RefreshGrid();
            RefreshSummary();
            RefreshSuggestions();
            if (showEditor)
                ShowQuotationEditor();
            SetStatus("New quotation ready.", DS.Slate500);
        }

        private static List<TenderBidLineItem> BuildFallbackQuotationRows()
        {
            return new List<TenderBidLineItem>
            {
                MakeFallbackLine("Daikin VRV IV Outdoor Unit RXYQ14AY1", "HVAC", 1m, "Nos", "Daikin India", 85000m, 115000m, 2m, 0m),
                MakeFallbackLine("Daikin VRV IV Indoor Unit FXFQ32AV16", "HVAC", 3m, "Nos", "Daikin India", 18500m, 24500m, 2m, 1m),
                MakeFallbackLine("Copper Pipe 3/8\"", "Material", 20m, "Mtr", "Jindal", 450m, 650m, 15m, 5m),
                MakeFallbackLine("Installation Labour", "Service", 1m, "Lot", "-", 5000m, 8000m, 0m, 0m)
            };
        }

        private static TenderBidLineItem MakeFallbackLine(string item, string category, decimal qty, string unit, string supplier, decimal cost, decimal sell, decimal stock, decimal shortfall)
        {
            decimal taxable = Math.Round(qty * sell, 2);
            return new TenderBidLineItem
            {
                ItemDescription = item,
                Category = category,
                Quantity = qty,
                Unit = unit,
                BestSupplierName = supplier == "-" ? string.Empty : supplier,
                CostPerUnit = cost,
                SellPricePerUnit = sell,
                GSTRatePct = 18m,
                StockAvailable = stock,
                Shortfall = shortfall,
                TaxableLineTotal = taxable,
                GSTAmount = Math.Round(taxable * 0.18m, 2),
                MarginPct = sell <= 0 ? 0m : Math.Round(((sell - cost) / sell) * 100m, 2),
                AnalysisStatus = "Manual",
                AnalysisNotes = "Demo fallback row. Select an inventory item to analyse live supplier/stock."
            };
        }

        private TenderBid CollectBidFromForm()
        {
            ComboItem client = _cboClient.SelectedItem as ComboItem;
            ComboItem site = _cboSite.SelectedItem as ComboItem;
            if (client == null || client.Id <= 0) throw new Exception("Please select a client.");
            if (site == null || site.Id <= 0) throw new Exception("Please select a site.");
            if (string.IsNullOrWhiteSpace(_txtQuoteNo.Text)) throw new Exception("Quotation number is required.");
            if (string.IsNullOrWhiteSpace(_txtTitle.Text)) throw new Exception("Quotation / project name is required.");
            SyncGridToModel();
            if (_lineItems.All(li => string.IsNullOrWhiteSpace(li.ItemDescription))) throw new Exception("Add at least one line item.");

            TenderBid bid = _current != null ? new TenderBid { BidID = _current.BidID } : new TenderBid();
            bid.QuotationNumber = _txtQuoteNo.Text.Trim();
            bid.TenderName = _txtTitle.Text.Trim();
            bid.ClientID = client.Id;
            bid.ClientName = client.Text;
            bid.SiteID = site.Id;
            bid.SiteName = site.Text;
            bid.SubmittedDate = _dtpDate.Value.Date;
            bid.DueDate = _dtpDue.Value.Date;
            bid.RequiredByDate = _dtpRequiredBy.Value.Date;
            bid.Status = _cboStatus.SelectedItem?.ToString() ?? "Draft";
            bid.CommercialFlow = _cboCommercialFlow?.SelectedItem?.ToString() ?? "Revenue";
            bid.CustomerDocumentStatus = _cboCustomerDocStatus?.SelectedItem?.ToString() ?? "Quote Draft";
            bid.SupplierDocumentStatus = _cboSupplierDocStatus?.SelectedItem?.ToString() ?? "Not Required";
            bid.Notes = _txtNotes.Text.Trim();
            bid.TemplateId = _current?.TemplateId;
            bid.LineItems = _lineItems.Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription)).Select((li, index) => CloneLine(li, index)).ToList();
            bid.IsMultiLine = bid.LineItems.Count > 1;
            return _svc.AnalyseTenderDraft(bid);
        }
        private async Task HandleGridCellEndEditAsync(int rowIndex, int columnIndex)
        {
            if (_updatingGrid || rowIndex < 0 || rowIndex >= _lineItems.Count || columnIndex < 0 || columnIndex >= _grid.Columns.Count)
                return;

            string column = _grid.Columns[columnIndex].Name;
            SyncRowToModel(rowIndex, column);
            if (column == "Qty" || column == "Unit")
                WarnIfIntegerUnitHasDecimal(rowIndex);
            if (column == "ItemDescription")
            {
                if (_lineItems[rowIndex].InventoryItemId.HasValue)
                    await AnalyseRowAsync(rowIndex);
                else
                {
                    // Free text allowed â€” refresh visuals without analysis
                    RefreshRow(rowIndex);
                    RefreshSummary();
                    RefreshSuggestions();
                }
            }
            else if (column == "Qty")
            {
                if (_lineItems[rowIndex].InventoryItemId.HasValue)
                    await AnalyseRowAsync(rowIndex);
                else
                    RecalculateRow(rowIndex);
            }
            else if (column == "SellPrice")
            {
                _lineItems[rowIndex].IsSellPriceManual = true;
                RecalculateRow(rowIndex);
            }
            else if (IsManualLineEditColumn(column))
            {
                _lineItems[rowIndex].AnalysisStatus = "Manual";
                RecalculateRow(rowIndex);
                RefreshSuggestions();
            }
            else if (column == "Supplier")
            {
                ApplySelectedSupplier(rowIndex, true);
            }
        }

        private void HandleGridValueChange(int rowIndex, int columnIndex)
        {
            if (_updatingGrid || rowIndex < 0 || rowIndex >= _lineItems.Count || columnIndex < 0 || columnIndex >= _grid.Columns.Count)
                return;
            string column = _grid.Columns[columnIndex].Name;
            if (column == "Unit" || IsManualLineEditColumn(column))
            {
                SyncRowToModel(rowIndex, column);
                if (column == "Unit")
                    WarnIfIntegerUnitHasDecimal(rowIndex);
            }
            if (column == "Supplier")
                ApplySelectedSupplier(rowIndex, true);
        }

        private void HandleGridButtonClick(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count)
                return;
            string columnName = _grid.Columns[columnIndex].Name;
            if (columnName == "Actions")
            {
                ShowLineItemActions(rowIndex, columnIndex);
                return;
            }
            if (columnName == "Edit")
            {
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells["ItemDescription"];
                _grid.BeginEdit(true);
                return;
            }
            if (columnName != "Delete")
                return;
            DeleteLineItem(rowIndex);
        }

        private void ShowLineItemActions(int rowIndex, int columnIndex)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9f);
            menu.Items.Add("Edit", null, (s, e) =>
            {
                if (rowIndex >= 0 && rowIndex < _grid.Rows.Count)
                {
                    _grid.CurrentCell = _grid.Rows[rowIndex].Cells["ItemDescription"];
                    _grid.BeginEdit(true);
                }
            });
            menu.Items.Add("Delete", null, (s, e) => DeleteLineItem(rowIndex));
            Rectangle cellBounds = _grid.GetCellDisplayRectangle(columnIndex, rowIndex, true);
            menu.Show(_grid, new Point(cellBounds.Left, cellBounds.Bottom));
        }

        private void DeleteLineItem(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count)
                return;
            _lineItems.RemoveAt(rowIndex);
            if (_lineItems.Count == 0)
                _lineItems.Add(new TenderBidLineItem { Quantity = 1m, Unit = "Nos", GSTRatePct = 18m, AnalysisStatus = "Pending" });
            RefreshGrid();
            RefreshSummary();
            RefreshSuggestions();
        }

        private void WarnIfIntegerUnitHasDecimal(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count)
                return;

            TenderBidLineItem line = _lineItems[rowIndex];
            string unit = (line.Unit ?? string.Empty).Trim();
            if ((string.Equals(unit, "Nos", StringComparison.OrdinalIgnoreCase) || string.Equals(unit, "Unit", StringComparison.OrdinalIgnoreCase))
                && line.Quantity != decimal.Truncate(line.Quantity))
            {
                MessageBox.Show("Quantity should be a whole number for Nos/Unit items.", "Quantity validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task AnalyseRowAsync(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count)
                return;

            TenderBidLineItem draftLine = CloneLine(_lineItems[rowIndex], rowIndex);
            if (string.IsNullOrWhiteSpace(draftLine.ItemDescription) || !draftLine.InventoryItemId.HasValue)
                return;

            int? selectedSupplierId = draftLine.BestSupplierId;
            string selectedSupplierName = draftLine.BestSupplierName;
            _grid.Rows[rowIndex].Cells["MarginPct"].Value = "Analysing...";
            try
            {
                TenderBid draftBid = BuildDraftHeaderOnly();
                TenderBidLineItem analysed = await Task.Run(() => _svc.AnalyseTenderLineItem(draftBid, draftLine));
                if (selectedSupplierId.HasValue)
                {
                    analysed.BestSupplierId = selectedSupplierId;
                    analysed.BestSupplierName = selectedSupplierName;
                    ApplySupplierRate(analysed);
                }
                _lineItems[rowIndex] = analysed;
                RecalculateRow(rowIndex);
                RefreshSuggestions();
                SetStatus("Line analysed.", InfoBlue);
            }
            catch (Exception ex)
            {
                _lineItems[rowIndex].AnalysisStatus = "Failed";
                _lineItems[rowIndex].AnalysisNotes = ex.Message;
                RefreshRow(rowIndex);
                SetStatus("Analysis warning: " + ex.Message, WarnOrange);
            }
        }

        private void RecalculateRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count)
                return;
            TenderBidLineItem line = _lineItems[rowIndex];
            line.Quantity = line.Quantity <= 0 ? 1m : line.Quantity;
            line.TaxableLineTotal = Math.Round(line.Quantity * line.SellPricePerUnit, 2);
            line.GSTAmount = Math.Round(line.TaxableLineTotal * (line.GSTRatePct / 100m), 2);
            if (line.SellPricePerUnit > 0m && line.CostPerUnit > 0m)
                line.MarginPct = Math.Round(((line.SellPricePerUnit - line.CostPerUnit) / line.SellPricePerUnit) * 100m, 2);
            RefreshRow(rowIndex);
            RefreshSummary();
        }

        private void AddLineItem()
        {
            SyncGridToModel();
            _lineItems.Add(new TenderBidLineItem { Quantity = 1m, Unit = "Nos", GSTRatePct = 18m, AnalysisStatus = "Pending" });
            RefreshGrid();
            BeginEditItemCell(_lineItems.Count - 1, true);
        }

        private void BeginEditItemCell(int rowIndex, bool openDropdown)
        {
            if (_grid == null || rowIndex < 0 || rowIndex >= _grid.Rows.Count || !_grid.Columns.Contains("ItemDescription"))
                return;

            BeginInvoke((Action)(() =>
            {
                if (_grid == null || _grid.IsDisposed || rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                    return;

                EnsureInventoryComboCell(rowIndex);
                _grid.CurrentCell = _grid.Rows[rowIndex].Cells["ItemDescription"];
                _grid.BeginEdit(true);

                if (!openDropdown || !(_grid.EditingControl is ComboBox combo))
                    return;

                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
                if (combo.Items.Count > 0)
                    combo.DroppedDown = true;
            }));
        }

        private void RefreshGrid()
        {
            if (_grid == null)
                return;

            _updatingGrid = true;
            try
            {
                _grid.Rows.Clear();
                for (int i = 0; i < _lineItems.Count; i++)
                {
                    _grid.Rows.Add();
                    RefreshRow(i);
                }
                if (_lblLineItemCount != null)
                    _lblLineItemCount.Text = string.Format(CultureInfo.InvariantCulture, "Showing 1 to {0} of {0} items", _lineItems.Count);
                UpdateLineItemsEmptyState();
            }
            finally
            {
                _updatingGrid = false;
            }
        }

        private void RefreshRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count || rowIndex >= _grid.Rows.Count)
                return;
            bool wasUpdating = _updatingGrid;
            _updatingGrid = true;
            try
            {
                TenderBidLineItem line = _lineItems[rowIndex];
                DataGridViewRow row = _grid.Rows[rowIndex];
                EnsureInventoryComboCell(rowIndex);
                EnsureSupplierComboCell(rowIndex);
                StockItem selectedItem = ResolveInventoryItem(line.InventoryItemId, line.ItemDescription);
                SetCellValue(row, "Sr", (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
                row.Cells["ItemDescription"].Value = selectedItem?.ItemName ?? line.ItemDescription;
                SetCellValue(row, "Category", string.IsNullOrWhiteSpace(line.Category) ? InferDisplayCategory(line) : line.Category);
                row.Cells["Qty"].Value = line.Quantity.ToString("0.###", CultureInfo.InvariantCulture);
                row.Cells["Unit"].Value = string.IsNullOrWhiteSpace(selectedItem?.Unit) ? (string.IsNullOrWhiteSpace(line.Unit) ? "Nos" : line.Unit) : selectedItem.Unit;
                row.Cells["Supplier"].Value = line.BestSupplierName ?? string.Empty;
                row.Cells["CostPerUnit"].Value = line.CostPerUnit.ToString("0.00", CultureInfo.InvariantCulture);
                row.Cells["SellPrice"].Value = line.SellPricePerUnit.ToString("0.00", CultureInfo.InvariantCulture);
                row.Cells["MarginPct"].Value = line.AnalysisStatus == "Failed" ? "Failed" : line.MarginPct.ToString("0.##", CultureInfo.InvariantCulture);
                row.Cells["Gst"].Value = line.GSTRatePct.ToString("0.##", CultureInfo.InvariantCulture);
                row.Cells["Stock"].Value = line.StockAvailable.ToString("0.###", CultureInfo.InvariantCulture);
                row.Cells["Shortfall"].Value = line.Shortfall.ToString("0.###", CultureInfo.InvariantCulture);
                SetCellValue(row, "HsnSac", line.HsnSacCode ?? string.Empty);
                SetCellValue(row, "LineTotal", line.TaxableLineTotal.ToString("0.00", CultureInfo.InvariantCulture));
                SetCellValue(row, "Actions", "\u22EE");
                row.Cells["CostPerUnit"].Style.ForeColor = QuoteText;
                ApplyCategoryStyle(row.Cells["Category"], Convert.ToString(row.Cells["Category"].Value));
                row.Cells["ItemDescription"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                row.Cells["LineTotal"].Style.ForeColor = QuoteText;
                ApplyMarginStyle(row.Cells["MarginPct"], line);
                ApplyStockStyle(row.Cells["Stock"], row.Cells["Shortfall"], line);
            }
            finally
            {
                _updatingGrid = wasUpdating;
            }
        }

        private void SyncGridToModel()
        {
            for (int i = 0; i < _grid.Rows.Count && i < _lineItems.Count; i++)
                SyncRowToModel(i);
        }

        private void SyncRowToModel(int rowIndex, string editedColumn = null)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count || rowIndex >= _grid.Rows.Count)
                return;
            DataGridViewRow row = _grid.Rows[rowIndex];
            TenderBidLineItem line = _lineItems[rowIndex];
            line.SortOrder = rowIndex;
            string itemText = GetCellText(row, "ItemDescription").Trim();
            StockItem selectedItem = null;
            if (_inventorySelectionRow == rowIndex && _inventorySelectionItem != null && string.Equals(_inventorySelectionItem.ItemName, itemText, StringComparison.OrdinalIgnoreCase))
                selectedItem = _inventorySelectionItem;
            if (selectedItem == null)
                selectedItem = ResolveInventoryItem(null, itemText);

            line.ItemDescription = selectedItem?.ItemName ?? itemText ?? string.Empty;
            if (selectedItem != null)
            {
                line.InventoryItemId = selectedItem.ItemID;
                line.Unit = string.IsNullOrWhiteSpace(selectedItem.Unit) ? "Nos" : selectedItem.Unit;
                line.Category = selectedItem.Category;
                if (line.AnalysisStatus == "Manual")
                    line.AnalysisStatus = "Pending";
                line.AnalysisNotes = string.Empty;
            }
            else
            {
                line.InventoryItemId = null;
                // preserve free text â€” do not clear ItemDescription
                line.AnalysisStatus = string.IsNullOrWhiteSpace(line.ItemDescription) ? "Pending" : "Manual";
                line.AnalysisNotes = string.Empty;
            }
            if (HasCell(row, "Category"))
                line.Category = GetCellText(row, "Category");
            if (HasCell(row, "HsnSac"))
                line.HsnSacCode = GetCellText(row, "HsnSac");
            line.Quantity = ParseDecimal(row.Cells["Qty"].Value, line.Quantity <= 0 ? 1m : line.Quantity);
            if (!line.InventoryItemId.HasValue)
                line.Unit = Convert.ToString(row.Cells["Unit"].Value) ?? "Nos";
            line.CostPerUnit = ParseDecimal(row.Cells["CostPerUnit"].Value, line.CostPerUnit);
            decimal newSell = ParseDecimal(row.Cells["SellPrice"].Value, line.SellPricePerUnit);
            if (Math.Abs(newSell - line.SellPricePerUnit) > 0.009m)
                line.IsSellPriceManual = true;
            line.SellPricePerUnit = newSell;
            line.GSTRatePct = ParseDecimal(row.Cells["Gst"].Value, line.GSTRatePct);
            line.StockAvailable = ParseDecimal(row.Cells["Stock"].Value, line.StockAvailable);
            line.Shortfall = ParseDecimal(row.Cells["Shortfall"].Value, line.Shortfall);
            line.MarginPct = ParseDecimal(row.Cells["MarginPct"].Value, line.MarginPct);
            if (string.Equals(editedColumn, "LineTotal", StringComparison.OrdinalIgnoreCase) && HasCell(row, "LineTotal"))
            {
                decimal editedTotal = ParseDecimal(row.Cells["LineTotal"].Value, line.TaxableLineTotal);
                if (line.Quantity > 0m)
                {
                    line.SellPricePerUnit = Math.Round(editedTotal / line.Quantity, 2);
                    line.IsSellPriceManual = true;
                }
            }
            if (string.Equals(editedColumn, "MarginPct", StringComparison.OrdinalIgnoreCase))
            {
                if (line.CostPerUnit > 0m && line.MarginPct < 99.99m)
                {
                    line.SellPricePerUnit = Math.Round(line.CostPerUnit / (1m - (line.MarginPct / 100m)), 2);
                    line.IsSellPriceManual = true;
                }
            }
            if (IsManualLineEditColumn(editedColumn))
                line.AnalysisStatus = "Manual";
            ApplySelectedSupplier(rowIndex, false);
            _inventorySelectionRow = -1;
            _inventorySelectionItem = null;
        }

        private void RefreshSummary()
        {
            if (_loading || _txtQuoteNo == null || _txtTitle == null || _dtpDate == null || _dtpDue == null)
                return;
            TenderBid bid = BuildDraftHeaderOnly();
            bid.LineItems = _lineItems.Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription)).Select((li, index) => CloneLine(li, index)).ToList();
            if (bid.LineItems.Count == 0)
            {
                SetMetric(_lblTaxable, "Taxable Value", IndiaFormatHelper.FormatCurrency(0));
                SetMetric(_lblGst, "GST", IndiaFormatHelper.FormatCurrency(0));
                SetMetric(_lblTotal, "Total incl. GST", IndiaFormatHelper.FormatCurrency(0));
                SetMetric(_lblMargin, "Avg Margin %", "0.00%");
                UpdateLiveQuotationHeaderStats(0m, 0m, 0m, 0m);
                RefreshSmartAlerts(bid);
                return;
            }
            bid = _svc.AnalyseTenderDraft(bid);
            decimal discountPercent = _numDiscount == null ? 0m : _numDiscount.Value;
            decimal discountAmount = Math.Round(bid.TotalTaxableValue * (discountPercent / 100m), 2);
            decimal total = Math.Max(0m, bid.TotalTaxableValue - discountAmount + bid.TotalGSTAmount);
            SetMetric(_lblTaxable, "Subtotal (Excl. GST)", IndiaFormatHelper.FormatCurrency(bid.TotalTaxableValue));
            SetMetric(_lblGst, "GST (18%)", IndiaFormatHelper.FormatCurrency(bid.TotalGSTAmount));
            SetMetric(_lblTotal, "Total (Incl. GST)", IndiaFormatHelper.FormatCurrency(total));
            SetMetric(_lblMargin, "Gross Margin", bid.AverageMarginPct.ToString("0.##", CultureInfo.InvariantCulture) + "%");
            if (_lblMargin != null)
                _lblMargin.ForeColor = bid.AverageMarginPct >= 25m ? SaveGreen : bid.AverageMarginPct >= 15m ? WarnOrange : DS.Red600;
            UpdateLiveQuotationHeaderStats(total, bid.TotalTaxableValue, bid.TotalGSTAmount, bid.AverageMarginPct);
            RefreshSmartAlerts(bid);
        }

        private void UpdateLiveQuotationHeaderStats(decimal total, decimal subtotal, decimal gst, decimal margin)
        {
            if (_lblKpiQuoteValue != null)
                _lblKpiQuoteValue.Text = IndiaFormatHelper.FormatCurrency(total);
            if (_lblKpiMargin != null)
                _lblKpiMargin.Text = margin.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            if (_lblKpiMarginSub != null)
            {
                decimal cost = _lineItems.Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription)).Sum(li => li.CostPerUnit * li.Quantity);
                _lblKpiMarginSub.Text = IndiaFormatHelper.FormatCurrency(Math.Max(0m, subtotal - cost));
                _lblKpiMarginSub.ForeColor = margin >= 20m ? SaveGreen : margin >= 10m ? WarnOrange : DS.Red600;
            }
            string status = _cboStatus?.SelectedItem?.ToString() ?? "Draft";
            if (_lblKpiApproval != null)
                _lblKpiApproval.Text = status;
            if (_lblKpiApprovalSub != null)
            {
                _lblKpiApprovalSub.Text = status == "Draft" ? "Not Sent" : status;
                _lblKpiApprovalSub.ForeColor = ResolveQuoteStatusColor(status);
            }
            if (_quoteDetailsStatusPill != null)
            {
                _quoteDetailsStatusPill.Text = status.ToUpperInvariant();
                _quoteDetailsStatusPill.ForeColor = ResolveQuoteStatusColor(status);
                _quoteDetailsStatusPill.BackColor = DS.Lighten(ResolveQuoteStatusColor(status), 0.86f);
                _quoteDetailsStatusPill.Width = Math.Max(70, TextRenderer.MeasureText(_quoteDetailsStatusPill.Text, _quoteDetailsStatusPill.Font).Width + 18);
            }
            decimal probability = ResolveCloseProbability(status, margin);
            if (_lblKpiProbability != null)
                _lblKpiProbability.Text = probability.ToString("0", CultureInfo.InvariantCulture) + "%";
            if (_lblKpiProbabilitySub != null)
            {
                _lblKpiProbabilitySub.Text = probability >= 70m ? "High" : probability >= 35m ? "Medium" : "Low";
                _lblKpiProbabilitySub.ForeColor = probability >= 70m ? SaveGreen : probability >= 35m ? WarnOrange : DS.Red600;
            }
            int days = Math.Max(0, (_dtpDue?.Value.Date ?? DateTime.Today).Subtract(_dtpDate?.Value.Date ?? DateTime.Today).Days);
            if (_lblKpiValidity != null)
                _lblKpiValidity.Text = days + " Days";
            if (_lblKpiValiditySub != null)
                _lblKpiValiditySub.Text = "Expires on " + (_dtpDue?.Value.Date ?? DateTime.Today).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (_lblKpiSaved != null)
                _lblKpiSaved.Text = _current == null ? "Just now" : "Saved";
            if (_lblKpiSavedSub != null)
                _lblKpiSavedSub.Text = "by " + (SessionManager.CurrentUser?.DisplayName ?? Environment.UserName);
            if (_lblLastSaved != null)
                _lblLastSaved.Text = "Last saved: Just now        Help & Support";
            RenderWorkflowStepper();
        }

        private void BindInventoryItems()
        {
            if (!(_grid.Columns["ItemDescription"] is DataGridViewComboBoxColumn comboColumn))
                return;

            string selectedCategory = _cmbCategoryFilter?.SelectedItem?.ToString();
            bool filterAll = string.IsNullOrEmpty(selectedCategory) || selectedCategory == "All";
            IEnumerable<StockItem> source = filterAll
                ? _inventoryItems
                : _inventoryItems.Where(i => string.Equals(i.Category, selectedCategory, StringComparison.OrdinalIgnoreCase));
            string search = GetItemSearchText();
            if (!string.IsNullOrWhiteSpace(search))
            {
                source = source.Where(i =>
                    (!string.IsNullOrWhiteSpace(i.ItemName) && i.ItemName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(i.Category) && i.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(i.Unit) && i.Unit.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            comboColumn.Items.Clear();
            foreach (string item in source.Select(i => i.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                comboColumn.Items.Add(item);
        }

        private string GetItemSearchText()
        {
            if (_txtItemSearch == null)
                return string.Empty;

            string text = (_txtItemSearch.Text ?? string.Empty).Trim();
            if (string.Equals(text, "Search items...", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            if (_txtItemSearch.ForeColor == QuoteMuted)
                return string.Empty;
            return text;
        }

        private void BindSupplierColumn()
        {
            if (!(_grid.Columns["Supplier"] is DataGridViewComboBoxColumn comboColumn))
                return;

            comboColumn.Items.Clear();
            comboColumn.Items.Add(string.Empty);
            foreach (string vendorName in _vendors
                .Select(v => v.VendorName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n))
                comboColumn.Items.Add(vendorName);
        }

        private void EnsureSupplierComboCell(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return;
            if (!(_grid.Rows[rowIndex].Cells["Supplier"] is DataGridViewComboBoxCell cell))
                return;

            HashSet<string> values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            cell.Items.Clear();
            cell.Items.Add(string.Empty);
            foreach (Vendor vendor in _vendors.OrderBy(v => v.VendorName))
            {
                if (!string.IsNullOrWhiteSpace(vendor.VendorName) && values.Add(vendor.VendorName))
                    cell.Items.Add(vendor.VendorName);
            }

            string current = _lineItems[rowIndex].BestSupplierName;
            if (!string.IsNullOrWhiteSpace(current) && values.Add(current))
                cell.Items.Add(current);
        }

        private void ApplySelectedSupplier(int rowIndex, bool refresh)
        {
            if (rowIndex < 0 || rowIndex >= _lineItems.Count || rowIndex >= _grid.Rows.Count)
                return;

            string supplierName = Convert.ToString(_grid.Rows[rowIndex].Cells["Supplier"].Value)?.Trim();
            TenderBidLineItem line = _lineItems[rowIndex];
            if (string.IsNullOrWhiteSpace(supplierName))
            {
                line.BestSupplierId = null;
                line.BestSupplierName = string.Empty;
                if (refresh)
                    RecalculateRow(rowIndex);
                return;
            }

            Vendor vendor = _vendors.FirstOrDefault(v => string.Equals(v.VendorName, supplierName, StringComparison.OrdinalIgnoreCase));
            line.BestSupplierId = vendor?.VendorID ?? line.BestSupplierId;
            line.BestSupplierName = supplierName;
            if (refresh)
                ApplySupplierRate(line);
            if (refresh)
                RecalculateRow(rowIndex);
        }

        private void ApplySupplierRate(TenderBidLineItem line)
        {
            if (line == null || !line.BestSupplierId.HasValue || string.IsNullOrWhiteSpace(line.ItemDescription))
                return;

            SupplierOption option = _vendorSvc
                .GetSupplierOptions(line.ItemDescription, line.Category)
                .FirstOrDefault(o => o.VendorID == line.BestSupplierId.Value);
            if (option == null)
                return;

            if (option.Rate > 0)
                line.CostPerUnit = option.Rate;
            if (!string.IsNullOrWhiteSpace(option.Unit))
                line.Unit = option.Unit;
        }

        private void BindCategoryFilter()
        {
            if (_cmbCategoryFilter == null)
                return;
            string previous = _cmbCategoryFilter.SelectedItem?.ToString();
            _cmbCategoryFilter.Items.Clear();
            _cmbCategoryFilter.Items.Add("All");
            foreach (string cat in _inventoryItems
                .Select(i => i.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c))
                _cmbCategoryFilter.Items.Add(cat);
            int idx = _cmbCategoryFilter.Items.IndexOf(previous ?? "All");
            _cmbCategoryFilter.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void AddServiceLabourLine()
        {
            SyncGridToModel();
            _lineItems.Add(new TenderBidLineItem
            {
                ItemDescription = "Installation Labour",
                Category = "Service",
                Quantity = 1m,
                Unit = "Lot",
                CostPerUnit = 5000m,
                SellPricePerUnit = 8000m,
                GSTRatePct = 18m,
                AnalysisStatus = "Manual",
                AnalysisNotes = "Service labour line added manually."
            });
            TenderBidLineItem line = _lineItems[_lineItems.Count - 1];
            line.TaxableLineTotal = Math.Round(line.Quantity * line.SellPricePerUnit, 2);
            line.GSTAmount = Math.Round(line.TaxableLineTotal * (line.GSTRatePct / 100m), 2);
            line.MarginPct = line.SellPricePerUnit <= 0m ? 0m : Math.Round(((line.SellPricePerUnit - line.CostPerUnit) / line.SellPricePerUnit) * 100m, 2);
            RefreshGrid();
            RefreshSummary();
            RefreshSuggestions();
        }

        private void RefreshSmartAlerts(TenderBid bid)
        {
            if (bid == null || bid.LineItems == null)
                return;

            int shortfall = bid.LineItems.Count(li => li.Shortfall > 0m);
            int lowMargin = bid.LineItems.Count(li => li.SellPricePerUnit > 0m && li.MarginPct < 15m);
            bool suppliersOk = bid.LineItems.All(li => li.IsInternalLabour || li.AnalysisStatus == "Manual" || li.BestSupplierId.HasValue || li.Shortfall <= 0m);
            bool siteSelected = bid.SiteID > 0;
            int days = Math.Max(0, (bid.DueDate.Date - DateTime.Today).Days);
            if (_lblAlertShortfall != null) _lblAlertShortfall.Text = (shortfall == 1 ? "1 item has stock shortfall" : shortfall + " items have stock shortfall");
            if (_lblAlertMargin != null) _lblAlertMargin.Text = (lowMargin == 1 ? "Low margin on 1 item" : lowMargin + " low-margin items");
            if (_lblAlertExpiry != null) _lblAlertExpiry.Text = "Quote validity expires in " + days + " days";
            if (_lblAlertSupplier != null) _lblAlertSupplier.Text = suppliersOk ? "All items have supplier prices" : "Some items need supplier prices";
            if (_lblAlertSite != null) _lblAlertSite.Text = siteSelected ? "Client site is selected" : "Select a client site";
        }

        private void SendForApproval()
        {
            if (_cboStatus != null)
                SelectComboByText(_cboStatus, "Approval");
            SetStatus("Quotation marked for approval review.", InfoBlue);
        }

        private void ShowBulkActionsMenu(Control owner)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Analyse all lines", null, async (s, e) => await AnalyseAllLinesAsync());
            menu.Items.Add("Add labour line", null, (s, e) => AddServiceLabourLine());
            menu.Items.Add("Clear empty lines", null, (s, e) => ClearEmptyQuotationLines());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Clear all lines", null, (s, e) =>
            {
                if (MessageBox.Show("Clear all quotation line items?", "Bulk Actions", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                _lineItems.Clear();
                _lineItems.Add(new TenderBidLineItem { Quantity = 1m, Unit = "Nos", GSTRatePct = 18m, AnalysisStatus = "Pending" });
                RefreshGrid();
                RefreshSummary();
                RefreshSuggestions();
                SetStatus("Quotation lines cleared.", WarnOrange);
            });
            menu.Show(owner, new Point(0, owner.Height + 4));
        }

        private async Task AnalyseAllLinesAsync()
        {
            SyncGridToModel();
            int count = 0;
            for (int i = 0; i < _lineItems.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(_lineItems[i].ItemDescription))
                    continue;
                TenderBid draft = BuildDraftHeaderOnly();
                TenderBidLineItem analysed = await Task.Run(() => _svc.AnalyseTenderLineItem(draft, CloneLine(_lineItems[i], i)));
                _lineItems[i] = analysed;
                count++;
            }
            RefreshGrid();
            RefreshSummary();
            RefreshSuggestions();
            SetStatus(count + " quotation line(s) analysed.", SaveGreen);
        }

        private void ClearEmptyQuotationLines()
        {
            SyncGridToModel();
            int before = _lineItems.Count;
            _lineItems.RemoveAll(li => string.IsNullOrWhiteSpace(li.ItemDescription));
            if (_lineItems.Count == 0)
                _lineItems.Add(new TenderBidLineItem { Quantity = 1m, Unit = "Nos", GSTRatePct = 18m, AnalysisStatus = "Pending" });
            RefreshGrid();
            RefreshSummary();
            RefreshSuggestions();
            SetStatus("Removed " + Math.Max(0, before - _lineItems.Count) + " empty quotation line(s).", InfoBlue);
        }

        private void CreateDispatchJobStub()
        {
            SetStatus("Use Create Dispatch Job to hand this quotation to Jobs.", InfoBlue);
        }

        private async Task CreateDispatchJobAsync()
        {
            try
            {
                int bidId = await EnsureSavedAsync();
                Job job = await Task.Run(() => _svc.CreateDispatchJobFromQuotation(bidId));
                _current = await Task.Run(() => _svc.GetByIdDetailed(bidId));
                PopulateCurrent(_current);
                await RefreshListsAsync();
                SetStatus("Revenue job created: " + job.JobNumber, SaveGreen);
                if (MessageBox.Show("Revenue job created.\r\n\r\n" + job.JobNumber + "\r\n\r\nOpen Jobs now?", "Create Revenue Job", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OnNavigate?.Invoke(15);
            }
            catch (Exception ex)
            {
                SetStatus("Dispatch job failed: " + ex.Message, Color.Firebrick);
            }
        }

        private void Grid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_grid.CurrentCell == null)
                return;

            string columnName = _grid.Columns[_grid.CurrentCell.ColumnIndex].Name;
            if (columnName != "ItemDescription" && columnName != "Supplier")
                return;

            if (e.Control is ComboBox combo)
            {
                UIHelper.ApplyInputStyle(combo);
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
                if (columnName == "ItemDescription")
                {
                    combo.SelectionChangeCommitted -= InventoryCombo_SelectionChangeCommitted;
                    combo.SelectionChangeCommitted += InventoryCombo_SelectionChangeCommitted;
                }
            }
        }

        private void InventoryCombo_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (_grid.CurrentCell == null || _grid.CurrentCell.RowIndex < 0)
                return;

            if (sender is ComboBox combo)
            {
                _inventorySelectionRow = _grid.CurrentCell.RowIndex;
                _inventorySelectionItem = _inventoryItems.FirstOrDefault(item =>
                    string.Equals(item.ItemName, combo.Text, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void EnsureInventoryComboCell(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                return;

            if (!(_grid.Rows[rowIndex].Cells["ItemDescription"] is DataGridViewComboBoxCell cell))
                return;

            string selectedCategory = _cmbCategoryFilter?.SelectedItem?.ToString();
            bool filterAll = string.IsNullOrEmpty(selectedCategory) || selectedCategory == "All";
            IEnumerable<StockItem> source = filterAll
                ? _inventoryItems
                : _inventoryItems.Where(i => string.Equals(i.Category, selectedCategory, StringComparison.OrdinalIgnoreCase));
            string search = GetItemSearchText();
            if (!string.IsNullOrWhiteSpace(search))
            {
                source = source.Where(i =>
                    (!string.IsNullOrWhiteSpace(i.ItemName) && i.ItemName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(i.Category) && i.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(i.Unit) && i.Unit.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            cell.Items.Clear();
            foreach (string item in source.Select(i => i.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                cell.Items.Add(item);

            string current = _lineItems[rowIndex].ItemDescription;
            if (!string.IsNullOrWhiteSpace(current) && !cell.Items.Contains(current))
                cell.Items.Add(current);
        }

        private StockItem ResolveInventoryItem(int? inventoryItemId, string itemText)
        {
            if (inventoryItemId.HasValue)
            {
                StockItem byId = _inventoryItems.FirstOrDefault(item => item.ItemID == inventoryItemId.Value);
                if (byId != null)
                    return byId;
            }

            if (string.IsNullOrWhiteSpace(itemText))
                return null;

            return _inventoryItems.FirstOrDefault(item => string.Equals(item.ItemName, itemText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshSuggestions()
        {
            if (_suggestionFlow == null || _txtQuoteNo == null || _dtpDate == null || _dtpDue == null)
                return;
            TenderBid bid = BuildDraftHeaderOnly();
            bid.LineItems = _lineItems.Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription)).Select((li, index) => CloneLine(li, index)).ToList();
            RefreshSuggestions(_svc.GenerateSuggestions(bid));
        }

        private void RefreshSuggestions(IEnumerable<string> suggestions)
        {
            if (_suggestionFlow == null || _suggestionFlow.IsDisposed)
                return;
            _suggestionFlow.Controls.Clear();
            foreach (string suggestion in (suggestions ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                bool alert = suggestion.StartsWith("Warning:") || suggestion.StartsWith("Shortfall:");
                Panel row = new Panel { Width = 832, Height = 42, BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
                row.Controls.Add(new Panel { Width = 5, Dock = DockStyle.Left, BackColor = alert ? Color.FromArgb(185, 28, 28) : SaveGreen });
                row.Controls.Add(new Label { Text = suggestion, Dock = DockStyle.Fill, Padding = new Padding(12, 10, 10, 10), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate900 });
                _suggestionFlow.Controls.Add(row);
            }
            if (_suggestionFlow.Controls.Count == 0)
                _suggestionFlow.Controls.Add(new Label { Text = "Analyse line items to see supplier, shortfall, and pricing suggestions.", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate500, Padding = new Padding(0, 8, 0, 0) });
        }

        private void CompareQuotation()
        {
            try
            {
                TenderBid bid = CollectBidFromForm();
                string report = "Quotation readiness:\n\n" + _svc.BuildQuotationComparison(bid);
                MessageBox.Show(report, "Compare Quotation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
            }
        }

        private void PreviewQuotation()
        {
            try
            {
                TenderBid bid;
                try
                {
                    bid = CollectBidFromForm();
                }
                catch
                {
                    SyncGridToModel();
                    bid = BuildDraftHeaderOnly();
                    bid.QuotationNumber = string.IsNullOrWhiteSpace(bid.QuotationNumber) ? "DRAFT-QUOTE-PREVIEW" : bid.QuotationNumber;
                    bid.TenderName = string.IsNullOrWhiteSpace(bid.TenderName) ? "Draft quotation preview" : bid.TenderName;
                    bid.ClientName = string.IsNullOrWhiteSpace(bid.ClientName) ? "Draft Client" : bid.ClientName;
                    bid.LineItems = _lineItems.Where(li => !string.IsNullOrWhiteSpace(li.ItemDescription)).Select((li, index) => CloneLine(li, index)).ToList();
                    if (bid.LineItems.Count == 0)
                        bid.LineItems.Add(new TenderBidLineItem { ItemDescription = "Draft service / material line", HsnSacCode = "9987", Unit = "Nos", Quantity = 1, SellPricePerUnit = 0, GSTRatePct = 18 });
                    bid.IsMultiLine = true;
                }
                string html = _svc.BuildQuotationDocumentHtml(bid);
                new HtmlPreviewDialog("Quotation Preview - " + (bid.QuotationNumber ?? "Draft Quote"), html).ShowDialog(this);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
                MessageBox.Show(ex.Message, "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PrintQuotationToPdf()
        {
            try
            {
                TenderBid bid = CollectBidFromForm();
                string html = _svc.BuildQuotationDocumentHtml(bid);
                using (var dlg = new SaveFileDialog())
                {
                    dlg.InitialDirectory = Directory.Exists(@"C:\HVAC_PRO_MSE\Invoice") ? @"C:\HVAC_PRO_MSE\Invoice" : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    dlg.Filter = "PDF Files (*.pdf)|*.pdf";
                    dlg.DefaultExt = "pdf";
                    dlg.AddExtension = true;
                    dlg.FileName = (string.IsNullOrWhiteSpace(bid.QuotationNumber) ? "quotation" : bid.QuotationNumber) + ".pdf";
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        ExportHtmlToPdf(html, dlg.FileName);
                        string templateCopy = _docTemplateSvc.CopyQuotationTemplateForDocument(Path.GetFileNameWithoutExtension(dlg.FileName), Path.GetDirectoryName(dlg.FileName));
                        SetStatus("Quotation PDF saved to " + dlg.FileName, SaveGreen);
                        if (!string.IsNullOrWhiteSpace(templateCopy))
                            MessageBox.Show("Quotation PDF saved.\n\nCompany PDF copy also prepared:\n" + templateCopy, "Quotation PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
            }
        }

        private void UploadCompanyPdf()
        {
            try
            {
                string path = DocumentTemplateService.UploadQuotationTemplateWithDialog(this);
                if (string.IsNullOrWhiteSpace(path))
                    return;
                SetStatus("Company quotation PDF uploaded.", SaveGreen);
                MessageBox.Show("Company quotation PDF uploaded.\n\n" + path, "Upload Document", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Upload error: " + ex.Message, Color.Firebrick);
            }
        }

        private void UploadDigitalSignature()
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Upload authorised signature image";
                    dialog.Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                    dialog.CheckFileExists = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    string folder = Path.GetDirectoryName(DocumentBranding.AuthorizedSignaturePath);
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    using (Bitmap bitmap = new Bitmap(dialog.FileName))
                    {
                        bitmap.Save(DocumentBranding.AuthorizedSignaturePath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    SetStatus("Digital signature saved. It will appear in quotation previews and PDFs.", SaveGreen);
                    MessageBox.Show("Digital signature saved.\n\nIt will appear in the Authorised Signatory section when you preview or print quotations.", "Digital Signature", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Signature upload failed: " + ex.Message, Color.Firebrick);
            }
        }

        private void OpenCompanyPdf()
        {
            try
            {
                _docTemplateSvc.OpenTemplate();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, Color.Firebrick);
            }
        }

        private static string ResolveDocumentOutputFolder()
        {
            string folder = @"C:\HVAC_PRO_MSE\Invoice";
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        private static bool IsManualLineEditColumn(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return false;
            return columnName == "Category"
                || columnName == "CostPerUnit"
                || columnName == "MarginPct"
                || columnName == "Gst"
                || columnName == "Stock"
                || columnName == "Shortfall"
                || columnName == "HsnSac"
                || columnName == "LineTotal";
        }

        private static bool HasCell(DataGridViewRow row, string columnName)
        {
            return row != null
                && row.DataGridView != null
                && row.DataGridView.Columns.Contains(columnName)
                && row.Cells[columnName] != null;
        }

        private static string GetCellText(DataGridViewRow row, string columnName)
        {
            return HasCell(row, columnName)
                ? Convert.ToString(row.Cells[columnName].Value) ?? string.Empty
                : string.Empty;
        }

        private static void SetCellValue(DataGridViewRow row, string columnName, object value)
        {
            if (HasCell(row, columnName))
                row.Cells[columnName].Value = value;
        }

        private TenderBid BuildDraftHeaderOnly()
        {
            ComboItem client = _cboClient?.SelectedItem as ComboItem;
            ComboItem site = _cboSite?.SelectedItem as ComboItem;
            DateTime quoteDate = _dtpDate?.Value.Date ?? DateTime.Today;
            return new TenderBid
            {
                BidID = _current?.BidID ?? 0,
                QuotationNumber = (_txtQuoteNo?.Text ?? string.Empty).Trim(),
                TenderName = (_txtTitle?.Text ?? string.Empty).Trim(),
                ClientID = client?.Id ?? 0,
                ClientName = client?.Text,
                SiteID = site?.Id ?? 0,
                SiteName = site?.Text,
                SubmittedDate = quoteDate,
                DueDate = _dtpDue?.Value.Date ?? quoteDate.AddDays(30),
                RequiredByDate = _dtpRequiredBy?.Value.Date,
                Status = _cboStatus?.SelectedItem?.ToString() ?? "Draft",
                CommercialFlow = _cboCommercialFlow?.SelectedItem?.ToString() ?? "Revenue",
                CustomerDocumentStatus = _cboCustomerDocStatus?.SelectedItem?.ToString() ?? "Quote Draft",
                SupplierDocumentStatus = _cboSupplierDocStatus?.SelectedItem?.ToString() ?? "Not Required",
                Notes = (_txtNotes?.Text ?? string.Empty).Trim(),
                TemplateId = _current?.TemplateId
            };
        }

        private static TenderBidLineItem CloneLine(TenderBidLineItem source, int sortOrder)
        {
            return new TenderBidLineItem
            {
                LineItemId = source.LineItemId,
                TenderBidId = source.TenderBidId,
                SortOrder = sortOrder,
                Category = source.Category,
                InventoryItemId = source.InventoryItemId,
                ItemDescription = source.ItemDescription,
                Quantity = source.Quantity,
                Unit = source.Unit,
                HsnSacCode = source.HsnSacCode,
                GSTRatePct = source.GSTRatePct,
                BestSupplierId = source.BestSupplierId,
                BestSupplierName = source.BestSupplierName,
                CostPerUnit = source.CostPerUnit,
                SellPricePerUnit = source.SellPricePerUnit,
                TaxableLineTotal = source.TaxableLineTotal,
                GSTAmount = source.GSTAmount,
                MarginPct = source.MarginPct,
                StockAvailable = source.StockAvailable,
                Shortfall = source.Shortfall,
                IsInternalLabour = source.IsInternalLabour,
                AnalysisStatus = source.AnalysisStatus,
                AnalysisNotes = source.AnalysisNotes,
                IsSellPriceManual = source.IsSellPriceManual,
                CreatedDate = source.CreatedDate,
                ModifiedDate = source.ModifiedDate,
                PriceMemoryApplied = source.PriceMemoryApplied,
                PriceMemoryDate = source.PriceMemoryDate,
                MinimumRecommendedPrice = source.MinimumRecommendedPrice,
                SuggestedSellPrice = source.SuggestedSellPrice
            };
        }
        private static decimal ParseDecimal(object value, decimal fallback)
        {
            return decimal.TryParse(Convert.ToString(value), out decimal parsed) ? parsed : fallback;
        }

        private void UpdateDueDate()
        {
            int days = 30;
            if (_cboValidity.SelectedItem != null)
                int.TryParse(_cboValidity.SelectedItem.ToString().Split(' ')[0], out days);
            _dtpDue.Value = _dtpDate.Value.Date.AddDays(days <= 0 ? 30 : days);
        }

        private void SelectCombo(ComboBox combo, int id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
                if (combo.Items[i] is ComboItem item && item.Id == id)
                    combo.SelectedIndex = i;
        }

        private void SelectComboByText(ComboBox combo, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            for (int i = 0; i < combo.Items.Count; i++)
                if (string.Equals(combo.Items[i].ToString(), text, StringComparison.OrdinalIgnoreCase))
                    combo.SelectedIndex = i;
        }

        private string GenerateNextQuoteNumber()
        {
            string prefix = "QT-" + DateTime.Today.ToString("ddMM", CultureInfo.InvariantCulture) + "-";
            int count = 0;
            try
            {
                count = _svc.GetAll().Count(q => !string.IsNullOrWhiteSpace(q.QuotationNumber) && q.QuotationNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }
            catch { }
            return prefix + (count + 1).ToString("0000", CultureInfo.InvariantCulture);
        }

        private static string NormalizeWorkflowStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "Draft";
            if (status.Equals("Analysed", StringComparison.OrdinalIgnoreCase)) return "Material Check";
            if (status.Equals("Submitted", StringComparison.OrdinalIgnoreCase)) return "Sent";
            if (status.Equals("Won", StringComparison.OrdinalIgnoreCase)) return "Accepted";
            if (status.Equals("Lost", StringComparison.OrdinalIgnoreCase)) return "Draft";
            return status;
        }

        private static Color ResolveQuoteStatusColor(string status)
        {
            switch ((status ?? "Draft").Trim())
            {
                case "Material Check": return WarnOrange;
                case "Approval": return Color.FromArgb(217, 119, 6);
                case "Sent": return CargoPurple;
                case "Accepted": return SaveGreen;
                case "Converted": return InfoBlue;
                default: return InfoBlue;
            }
        }

        private static decimal ResolveCloseProbability(string status, decimal margin)
        {
            decimal baseScore;
            switch ((status ?? "Draft").Trim())
            {
                case "Converted": baseScore = 100m; break;
                case "Accepted": baseScore = 85m; break;
                case "Sent": baseScore = 55m; break;
                case "Approval": baseScore = 35m; break;
                case "Material Check": baseScore = 20m; break;
                default: baseScore = 10m; break;
            }
            if (margin >= 25m) baseScore += 8m;
            else if (margin < 10m) baseScore -= 8m;
            return Math.Max(0m, Math.Min(100m, baseScore));
        }

        private void ApplyMarginStyle(DataGridViewCell cell, TenderBidLineItem line)
        {
            if (IsEmptyQuotationLine(line) || (line.CostPerUnit <= 0m && line.SellPricePerUnit <= 0m))
            {
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = DS.Slate500;
                cell.Style.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                return;
            }

            if (line.AnalysisStatus == "Failed")
            {
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = Color.FromArgb(220, 38, 38);
                cell.Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                return;
            }
            if (line.MarginPct >= 25m)
            {
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = Color.FromArgb(22, 163, 74);
            }
            else if (line.MarginPct >= 15m)
            {
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = WarnOrange;
            }
            else
            {
                cell.Style.BackColor = Color.White;
                cell.Style.ForeColor = Color.FromArgb(220, 38, 38);
            }
            cell.Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        private static void ApplyStockStyle(DataGridViewCell stockCell, DataGridViewCell shortfallCell, TenderBidLineItem line)
        {
            bool empty = IsEmptyQuotationLine(line);
            bool hasShortfall = !empty && line.Shortfall > 0m;
            bool stockWarning = !empty && line.StockAvailable < line.Quantity;
            stockCell.Style.ForeColor = stockWarning ? Color.FromArgb(220, 38, 38) : (empty ? DS.Slate500 : Color.FromArgb(22, 163, 74));
            shortfallCell.Style.ForeColor = hasShortfall ? Color.FromArgb(220, 38, 38) : DS.Slate500;
            stockCell.Style.Font = new Font("Segoe UI", 9f, stockWarning ? FontStyle.Bold : FontStyle.Regular);
            shortfallCell.Style.Font = new Font("Segoe UI", 9f, hasShortfall ? FontStyle.Bold : FontStyle.Regular);
        }

        private static bool IsEmptyQuotationLine(TenderBidLineItem line)
        {
            return line == null
                || (!line.InventoryItemId.HasValue
                    && string.IsNullOrWhiteSpace(line.ItemDescription)
                    && line.CostPerUnit <= 0m
                    && line.SellPricePerUnit <= 0m
                    && line.StockAvailable <= 0m
                    && line.Shortfall <= 0m);
        }

        private static void ApplyCategoryStyle(DataGridViewCell cell, string category)
        {
            string value = (category ?? string.Empty).Trim().ToLowerInvariant();
            Color back = Color.FromArgb(239, 246, 255);
            Color fore = InfoBlue;
            if (value.Contains("service") || value.Contains("labour"))
            {
                back = Color.FromArgb(220, 252, 231);
                fore = Color.FromArgb(21, 128, 61);
            }
            else if (value.Contains("elect") || value.Contains("light"))
            {
                back = Color.FromArgb(243, 232, 255);
                fore = Color.FromArgb(109, 40, 217);
            }
            else if (value.Contains("plumb") || value.Contains("pipe"))
            {
                back = Color.FromArgb(220, 252, 231);
                fore = Color.FromArgb(21, 128, 61);
            }
            cell.Style.BackColor = back;
            cell.Style.ForeColor = fore;
            cell.Style.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private static void StyleQuotationGrid(DataGridView grid)
        {
            DS.StyleGrid(grid);
            grid.Dock = DockStyle.None;
            grid.ReadOnly = false;
            grid.BackgroundColor = Color.White;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 30;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            grid.DefaultCellStyle.SelectionForeColor = QuoteText;
        }

        private static Panel CreateSectionPanel(string title, int width, int height)
        {
            Panel panel = new Panel { Width = width, Height = height, BackColor = QuoteSurface };
            DS.Rounded(panel, 12);
            panel.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            panel.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = QuoteText, Location = new Point(14, 10), AutoSize = true });
            return panel;
        }

        private static Panel MakeCard(int width, int height)
        {
            Panel panel = new Panel { Width = width, Height = height, BackColor = QuoteSurface };
            DS.Rounded(panel, 10);
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            return panel;
        }

        private static Label AddSummaryRow(Control parent, string label, string value, int y, bool total)
        {
            parent.Controls.Add(new Label { Text = label == "Discount" ? "Discount (%)" : label, Location = new Point(16, y), AutoSize = false, Width = 126, Height = 20, Font = new Font("Segoe UI", 8.5f, total ? FontStyle.Bold : FontStyle.Regular), ForeColor = total ? InfoBlue : QuoteText });
            Label valueLabel = new Label { Text = value, Location = new Point(150, y), Size = new Size(Math.Max(112, parent.ClientSize.Width - 182), 22), Anchor = AnchorStyles.Top | AnchorStyles.Right, Font = new Font("Segoe UI", total ? 11f : 8.5f, total ? FontStyle.Bold : FontStyle.Regular), ForeColor = total ? InfoBlue : QuoteText, TextAlign = ContentAlignment.MiddleRight };
            parent.Controls.Add(valueLabel);
            return valueLabel;
        }

        private static Label AddAlert(Control parent, int y, string mark, string text, Color color)
        {
            Label icon = new Label { Text = mark, Location = new Point(16, y), Size = new Size(18, 18), BackColor = DS.Lighten(color, 0.86f), ForeColor = color, Font = new Font("Segoe UI", 7.2f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(icon, 5);
            parent.Controls.Add(icon);
            Label label = new Label { Text = text, Location = new Point(44, y + 1), Size = new Size(234, 16), Font = new Font("Segoe UI", 8.2f), ForeColor = QuoteText };
            parent.Controls.Add(label);
            return label;
        }

        private void AddReadonlyField(TableLayoutPanel grid, int col, string label, string value)
        {
            Panel wrap = new Panel { Dock = DockStyle.Fill };
            wrap.Controls.Add(new Label { Text = label, Location = new Point(0, 0), AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = QuoteMuted });
            bool notes = string.Equals(label, "Notes", StringComparison.OrdinalIgnoreCase);
            TextBox box = new TextBox
            {
                Text = value,
                Location = new Point(0, 20),
                Width = notes ? 280 : 190,
                Height = notes ? 50 : 30,
                ReadOnly = true,
                Multiline = notes,
                WordWrap = notes,
                ScrollBars = notes ? ScrollBars.Vertical : ScrollBars.None,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = InputFill,
                ForeColor = QuoteText,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            _toolTip.SetToolTip(box, value);
            _toolTip.SetToolTip(wrap, value);
            wrap.Controls.Add(box);
            grid.Controls.Add(wrap, col, 0);
        }

        private static string InferDisplayCategory(TenderBidLineItem line)
        {
            string text = ((line?.Category ?? string.Empty) + " " + (line?.ItemDescription ?? string.Empty)).ToLowerInvariant();
            if (text.Contains("labour") || text.Contains("service")) return "Service";
            if (text.Contains("copper") || text.Contains("pipe") || text.Contains("material")) return "Material";
            if (text.Contains("vrv") || text.Contains("hvac") || text.Contains("ac")) return "HVAC";
            return "General";
        }

        private static Label MakeMetricCard(Control parent, Point location, int width, string caption)
        {
            Panel card = new Panel { Location = location, Width = width, Height = 52, BackColor = Color.FromArgb(248, 250, 252) };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(BorderColor))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            card.Controls.Add(new Label { Text = caption, Font = new Font("Segoe UI", 8), ForeColor = QuoteMuted, Location = new Point(10, 7), AutoSize = true });
            Label value = new Label { Text = IndiaFormatHelper.FormatCurrency(0), Tag = "MetricValue", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = QuoteText, Location = new Point(10, 24), AutoSize = true };
            card.Controls.Add(value);
            parent.Controls.Add(card);
            return value;
        }

        private static void SetMetric(Label valueLabel, string caption, string value)
        {
            valueLabel.Text = value;
            if (Equals(valueLabel.Tag, "MetricValue") && valueLabel.Parent != null && valueLabel.Parent.Controls.Count > 0 && valueLabel.Parent.Controls[0] is Label captionLabel)
                captionLabel.Text = caption;
        }

        private static void AddLabeledControl(TableLayoutPanel grid, int col, int row, string label, Control control)
        {
            Panel wrap = new Panel { Dock = DockStyle.Fill };
            wrap.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 8), ForeColor = QuoteMuted, Location = new Point(0, 0), AutoSize = true });
            control.Location = new Point(0, 16);
            wrap.Controls.Add(control);
            grid.Controls.Add(wrap, col, row);
        }

        private static TextBox MakeTextBox(bool readOnly)
        {
            return new TextBox { Width = 250, ReadOnly = readOnly, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9), BackColor = InputFill, ForeColor = QuoteText };
        }

        private static ComboBox MakeCombo(bool dropDownList)
        {
            return new ComboBox { Width = 250, DropDownStyle = dropDownList ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown, Font = new Font("Segoe UI", 9), BackColor = InputFill, ForeColor = QuoteText, FlatStyle = FlatStyle.Flat };
        }

        private static DateTimePicker MakeDatePicker()
        {
            return new DateTimePicker { Width = 250, Font = new Font("Segoe UI", 9), Format = DateTimePickerFormat.Short };
        }

        private void RegisterFilledButton(Button button, Color color)
        {
            if (button == null)
                return;

            button.Tag = color;
            if (!_filledQuoteButtons.Contains(button))
                _filledQuoteButtons.Add(button);
        }

        private void RegisterSecondaryButton(Button button)
        {
            if (button == null)
                return;

            if (!_secondaryQuoteButtons.Contains(button))
                _secondaryQuoteButtons.Add(button);
        }

        private void ApplyQuotationVisualFixes()
        {
            ApplyInputFillRecursive(this);
            foreach (Button button in _filledQuoteButtons)
            {
                Color color = button.Tag is Color ? (Color)button.Tag : CargoPurple;
                ApplyFilledButton(button, color);
            }

            foreach (Button button in _secondaryQuoteButtons)
                ApplySecondaryButton(button);

            if (_grid != null)
            {
                _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                _grid.ScrollBars = ScrollBars.Both;
                ApplyQuotationGridColumnSizing();
            }

            UpdateLineItemsEmptyState();
        }

        private void ApplyQuotationGridColumnSizing()
        {
            if (_grid == null || _grid.Columns.Count == 0)
                return;

            GridTheme.ApplyColumnPolicy(_grid, new[]
            {
                new GridColumnPolicy("Sr", 44, GridColumnPriority.Required),
                new GridColumnPolicy("ItemDescription", 150, GridColumnPriority.Required),
                new GridColumnPolicy("Category", 90, GridColumnPriority.Secondary),
                new GridColumnPolicy("Qty", 56, GridColumnPriority.Required),
                new GridColumnPolicy("Unit", 64, GridColumnPriority.Secondary),
                new GridColumnPolicy("Supplier", 120, GridColumnPriority.Secondary),
                new GridColumnPolicy("CostPerUnit", 90, GridColumnPriority.Required),
                new GridColumnPolicy("SellPrice", 100, GridColumnPriority.Required),
                new GridColumnPolicy("MarginPct", 86, GridColumnPriority.Required),
                new GridColumnPolicy("Gst", 70, GridColumnPriority.Secondary),
                new GridColumnPolicy("Stock", 72, GridColumnPriority.Required),
                new GridColumnPolicy("Shortfall", 92, GridColumnPriority.Required),
                new GridColumnPolicy("LineTotal", 105, GridColumnPriority.Required),
                new GridColumnPolicy("Actions", 80, GridColumnPriority.Optional),
                new GridColumnPolicy("HsnSac", 86, GridColumnPriority.Secondary),
                new GridColumnPolicy("Delete", 58, GridColumnPriority.Optional)
            });
        }

        private static void ApplyInputFillRecursive(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is TextBox textBox)
                {
                    textBox.BorderStyle = BorderStyle.None;
                    textBox.BackColor = InputFill;
                    textBox.ForeColor = QuoteText;
                }
                else if (child is ComboBox comboBox)
                {
                    comboBox.FlatStyle = FlatStyle.Flat;
                    comboBox.BackColor = InputFill;
                    comboBox.ForeColor = QuoteText;
                }
                else if (child is NumericUpDown numeric)
                {
                    numeric.BorderStyle = BorderStyle.None;
                    numeric.BackColor = InputFill;
                    numeric.ForeColor = QuoteText;
                }

                if (child.HasChildren)
                    ApplyInputFillRecursive(child);
            }
        }

        private static void ApplyFilledButton(Button button, Color color)
        {
            UIHelper.ApplyActionButton(button);
        }

        private static void ApplySecondaryButton(Button button)
        {
            UIHelper.ApplyActionButton(button, UiActionVariant.Secondary);
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 34, BackColor = bg, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            UIHelper.ApplyActionButton(button);
            return button;
        }

        private Button MakeOutlineBtn(string text, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 34, BackColor = Color.White, ForeColor = QuoteText, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            UIHelper.ApplyActionButton(button, UiActionVariant.Secondary);
            return button;
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text = text;
            _lblStatus.ForeColor = color;
        }

        private static void ExportHtmlToPdf(string html, string outputPath)
        {
            string[] candidates =
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            };
            string edgePath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(edgePath))
                throw new Exception("Microsoft Edge is required for PDF export.");

            string tempHtml = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(tempHtml, html);
            try
            {
                string fileUrl = new Uri(tempHtml).AbsoluteUri;
                using (Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = edgePath,
                    Arguments = "--headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf=\"" + outputPath + "\" \"" + fileUrl + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    process.WaitForExit(20000);
                    if (!File.Exists(outputPath))
                        throw new Exception("PDF export did not complete.");
                }
            }
            finally
            {
                if (File.Exists(tempHtml)) File.Delete(tempHtml);
            }
        }

        private sealed class ComboItem
        {
            public int Id { get; set; }
            public string Text { get; set; }
            public object Tag { get; set; }
            public override string ToString() => Text;
        }

        private sealed class TemplatePickerDialog : Form
        {
            private readonly ListBox _list = new ListBox();
            public int SelectedTemplateId => _list.SelectedItem is QuoteTemplate template ? template.TemplateId : 0;

            public TemplatePickerDialog(IEnumerable<QuoteTemplate> templates)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Select Template";
                Width = 480;
                Height = 360;
                StartPosition = FormStartPosition.CenterParent;
                _list.Dock = DockStyle.Fill;
                _list.Font = new Font("Segoe UI", 9);
                _list.DisplayMember = "TemplateName";
                foreach (QuoteTemplate template in templates ?? Enumerable.Empty<QuoteTemplate>())
                    _list.Items.Add(template);
                Controls.Add(_list);
                Controls.Add(new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 52,
                    Controls =
                    {
                        new Button { Text = "Cancel", Width = 90, Height = 32, Left = 272, Top = 10, DialogResult = DialogResult.Cancel },
                        new Button { Text = "Use Template", Width = 110, Height = 32, Left = 368, Top = 10, DialogResult = DialogResult.OK }
                    }
                });
                AcceptButton = Controls[1].Controls[1] as Button;
                CancelButton = Controls[1].Controls[0] as Button;
            }
        }

        private sealed class QuotationPreviewDialog : Form
        {
            public QuotationPreviewDialog(string title, string html)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = title;
                Width = 1100;
                Height = 760;
                StartPosition = FormStartPosition.CenterParent;
                WebBrowser browser = new WebBrowser { Dock = DockStyle.Fill, DocumentText = html };
                Controls.Add(browser);
            }
        }
    }
}

