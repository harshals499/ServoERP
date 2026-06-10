using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    /// <summary>
    /// Payment Recording Module.
    /// Layout (top to bottom):
    ///   1. Page header
    ///   2. Toolbar
    ///   3. Client filter bar  (DockStyle.Top, Height=40)
    ///   4. Payment history    (DockStyle.Top, Height=280) â€” DataGridView
    ///   5. Payment entry form (DockStyle.Fill) â€” scrollable panel
    /// </summary>
    public class PaymentForm : DeferredPageControl
    {
        // â”€â”€ Services â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private readonly PaymentService _paySvc    = new PaymentService();
        private readonly InvoiceService _invSvc    = new InvoiceService();
        private readonly ClientService  _clientSvc = new ClientService();

        // â”€â”€ History grid â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private FlowLayoutPanel _historyFlow;

        // â”€â”€ Client filter â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private ComboBox _cboClient;
        private AutoCompleteStringCollection _clientFilterSource = new AutoCompleteStringCollection();

        // â”€â”€ Payment entry form fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private ComboBox       _cmbClient, _cmbInvoice, _cmbMode;
        private DateTimePicker _dtpPayDate;
        private NumericUpDown  _numAmount;
        private TextBox        _txtRef, _txtNotes;
        private Panel _payClientHost, _payInvoiceHost, _payAmountHost, _payDateHost, _payModeHost, _payRefHost, _payNotesHost;
        private AutoCompleteStringCollection _clientEntrySource = new AutoCompleteStringCollection();
        private AutoCompleteStringCollection _invoiceSource = new AutoCompleteStringCollection();

        // â”€â”€ Invoice info labels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Label _lblInvTotal, _lblInvPaid, _lblInvBalance, _lblInvStatus;

        // â”€â”€ Status â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private Label _lblStatus;
        private Button _btnNewPayment;
        private Button _btnSavePayment;

        // â”€â”€ Cached data for filtering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private List<Payment> _allPayments = new List<Payment>();
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<Invoice> _invoiceLookup = new List<Invoice>();
        private List<Payment> _filteredPayments = new List<Payment>();
        private int _renderedPayments;
        private bool _invoiceDropdownLoading;
        private bool _recordPaymentInProgress;
        private const int PaymentBatchSize = 60;

        // â”€â”€ Colours â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color OrangeCol = DS.Amber500;
        private static readonly Color PayPageBg = Color.FromArgb(246, 248, 252);
        private static readonly Color PaySurface = Color.White;
        private static readonly Color PayText = Color.FromArgb(15, 23, 42);
        private static readonly Color PayMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color PayBorder = DS.Border;
        private static readonly Color PayRed = Color.FromArgb(239, 68, 68);
        private static readonly Color PayPurple = Color.FromArgb(124, 58, 237);

        private static bool UsePaymentsOverview { get { return true; } }
        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;
        private List<PaymentTxn> _overviewTransactions = new List<PaymentTxn>();
        private List<ReceivableRow> _overviewReceivables = new List<ReceivableRow>();
        private DateTime _overviewStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _overviewEnd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));
        private string _overviewTab = "All Transactions";
        private int _overviewPage = 1;
        private bool _showOverview = true;
        private int _overviewPageSize = 10;
        private FlowLayoutPanel _payStatsFlow;
        private PayCashFlowChart _payCashChart;
        private PayDonutChart _payDonutChart;
        private TableLayoutPanel _agingTable;
        private FlowLayoutPanel _methodList;
        private TableLayoutPanel _txnTable;
        private GlobalPaginationControl _txnPager;
        private FlowLayoutPanel _settlementsList;
        private FlowLayoutPanel _alertsList;
        private TextBox _txnSearch;
        private ComboBox _txnTypeFilter;
        private Label _dateRangeLabel;
        private bool _paymentsOverviewLoading;
        private Timer _paymentsOverviewLoadTimer;

        public PaymentForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            SalesUiPolishService.ApplyAfterRebuild(this, "Payments");
            ApplyPaymentActionHierarchy(Controls);
            ApplyPermissions();
            NormalizePaymentEditorCards();
            Func<Task> loader = UsePaymentsOverview && _showOverview ? (Func<Task>)LoadPaymentsOverviewAsync : LoadInitialDataAsync;
            EnableDeferredLoad(
                loader,
                ex =>
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Payments"), "Loading payments", ex);
                    if (_lblStatus != null)
                    {
                        _lblStatus.Text = "Payments could not load. Refresh and try again.";
                        _lblStatus.ForeColor = Color.Red;
                    }
                });
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            NormalizePaymentEditorCards();
            BeginInvoke((Action)NormalizePaymentEditorCards);
        }

        private Task LoadPaymentsOverviewAsync()
        {
            QueuePaymentsOverviewLoad();
            return Task.CompletedTask;
        }

        private void QueuePaymentsOverviewLoad()
        {
            if (_paymentsOverviewLoading || _paymentsOverviewLoadTimer != null)
                return;

            _paymentsOverviewLoadTimer = new Timer { Interval = 1200 };
            _paymentsOverviewLoadTimer.Tick += (s, e) =>
            {
                _paymentsOverviewLoadTimer.Stop();
                _paymentsOverviewLoadTimer.Dispose();
                _paymentsOverviewLoadTimer = null;
                if (_showOverview && Visible && !IsDisposed)
                    StartPaymentsOverviewLoad();
            };
            _paymentsOverviewLoadTimer.Start();
        }

        private void StartPaymentsOverviewLoad()
        {
            if (_paymentsOverviewLoading)
                return;

            _paymentsOverviewLoading = true;
            if (_lblStatus != null)
                ShowStatus("Loading payment overview...", Color.Gray);

            var worker = CreateWorker();
            worker.DoWork += (s, e) =>
            {
                e.Result = new PaymentOverviewSnapshot
                {
                    Payments = _paySvc.GetAllPayments() ?? new List<Payment>(),
                    Invoices = _invSvc.GetAllInvoices() ?? new List<Invoice>()
                };
            };
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    RunOnUI(() =>
                    {
                        _paymentsOverviewLoading = false;
                        ShowStatus("Payments could not load. Refresh and try again.", PayRed);
                    });
                    ShowError( "Failed to load payment overview. Please try again.", e.Error);
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Payments"), "Loading payment overview", e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(() =>
                {
                    _paymentsOverviewLoading = false;
                    PaymentOverviewSnapshot snapshot = e.Result as PaymentOverviewSnapshot ?? new PaymentOverviewSnapshot();
                    _allPayments = snapshot.Payments ?? new List<Payment>();
                    _invoiceLookup = snapshot.Invoices ?? new List<Invoice>();
                    BuildOverviewData();
                    if (_showOverview && !IsDisposed)
                        RefreshPaymentsOverview();
                });
            };
            worker.RunWorkerAsync();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && _showOverview && !_paymentsOverviewLoading && (_overviewTransactions == null || _overviewTransactions.Count == 0))
                QueuePaymentsOverviewLoad();
        }

        private async Task LoadInitialDataAsync()
        {
            ShowStatus("Loading payments...", Color.Gray);
            var sw = Stopwatch.StartNew();
            var fetch = Stopwatch.StartNew();
            var clientsTask = Task.Run(() => _clientSvc.GetAllClients());
            var paymentsTask = Task.Run(() => _paySvc.GetAllPayments());
            var invoicesTask = Task.Run(() => _invSvc.GetAllInvoices());

            _clients = await clientsTask;
            BindClientDropdowns(_clients);

            await Task.WhenAll(paymentsTask, invoicesTask);
            _allPayments = paymentsTask.Result;
            _invoiceLookup = invoicesTask.Result;
            AppRuntime.LogTiming("Payments.FetchInitialData", fetch.ElapsedMilliseconds, "clients=" + _clients.Count + ";payments=" + _allPayments.Count + ";invoices=" + _invoiceLookup.Count);

            ApplyClientFilter();
            AppRuntime.LogTiming("Payments.InitialLoad", sw.ElapsedMilliseconds, "payments=" + _allPayments.Count);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LAYOUT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BuildLayout()
        {
            if (UsePaymentsOverview && _showOverview)
            {
                BuildPaymentsOverviewLayout();
                return;
            }

            Panel header = new Panel { Dock = DockStyle.Top, Height = 94, BackColor = DS.BgPage, Padding = new Padding(28, 16, 28, 10) };
            Label title = new Label { Text = "Payment Recording", Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = DS.Slate950, Location = new Point(28, 18), Width = 390, Height = 30 };
            Label subtitle = new Label { Text = "Record, reconcile and review customer invoice payments.", Font = DS.Body, ForeColor = DS.Slate600, Location = new Point(29, 53), Width = 520, Height = 22 };
            Panel headerActions = new Panel
            {
                Dock = DockStyle.Right,
                Width = 768,
                Height = 46,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };

            _btnNewPayment = MakeBtn("+ New Payment", DS.Primary600, 130);
            Button btnRefresh = MakeBtn("Refresh", DS.White, 88, DS.Slate700, DS.BorderStrong);
            Button btnImport = MakeBtn("Import Excel", DS.White, 104, DS.Slate700, DS.BorderStrong);
            Button btnTemplate = MakeBtn("Excel Template", DS.White, 116, DS.Slate700, DS.BorderStrong);
            Button btnForms = MakeBtn("Service Forms", DS.White, 108, DS.Primary600, DS.BorderStrong);
            Button btnBackToOverview = MakeBtn("Back to Overview", DS.White, 132, DS.Slate700, DS.BorderStrong);
            Button[] headerButtons = { _btnNewPayment, btnRefresh, btnImport, btnTemplate, btnForms, btnBackToOverview };
            foreach (Button button in headerButtons)
            {
                button.Margin = Padding.Empty;
                button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                button.Tag = ((button.Tag == null ? string.Empty : button.Tag + " ") + "FIXED_WIDTH").Trim();
            }
            ModernIconSystem.AddButtonIcon(_btnNewPayment, ModernIconKind.Payment);
            ModernIconSystem.AddButtonIcon(btnRefresh, ModernIconKind.Refresh);
            ModernIconSystem.AddButtonIcon(btnImport, ModernIconKind.Import);
            ModernIconSystem.AddButtonIcon(btnTemplate, ModernIconKind.Document);
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            _btnNewPayment.Click += (s, e) => StartNewPayment();
            btnBackToOverview.Click += async (s, e) => await BackToPaymentsOverviewAsync();
            btnRefresh.Click += (s, e) => LoadPaymentHistory();
            btnImport.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(btnImport, ExcelImportModule.Payments, FindForm());
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Payments, FindForm());
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Payments", "Finance / Payments", null, "payment receipt collections follow-up invoice approval credit note customer sign-off");
            headerActions.Controls.AddRange(headerButtons.Cast<Control>().ToArray());
            Action layoutHeaderButtons = () =>
            {
                int actionRight = headerActions.ClientSize.Width;
                foreach (Button button in headerButtons)
                {
                    actionRight -= button.Width;
                    button.Location = new Point(Math.Max(0, actionRight), 10);
                    actionRight -= 10;
                }
            };
            headerActions.Resize += (s, e) => layoutHeaderButtons();
            layoutHeaderButtons();
            header.Controls.AddRange(new Control[] { title, subtitle, headerActions });

            _lblStatus = new Label { AutoSize = false, Font = DS.Small, ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleLeft };
            _btnSavePayment = MakeBtn("Save Payment", SaveGreen, 150);
            Button btnClear = MakeBtn("Clear Form", DS.Slate600, 120);
            ModernIconSystem.AddButtonIcon(_btnSavePayment, ModernIconKind.Save);
            _btnSavePayment.Click += BtnRecord_Click;
            btnClear.Click += (s, e) => ClearForm();

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                Padding = new Padding(28, 0, 28, 22),
                ColumnCount = 2,
                RowCount = 1
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel left = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = DS.BgPage, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 12, 0) };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));

            Panel paymentCard = MakeCard("PAYMENT DETAILS");
            BuildPaymentForm(paymentCard);
            Panel historyCard = MakeCard("PAYMENT HISTORY");
            BuildHistoryCard(historyCard);
            left.Controls.Add(paymentCard, 0, 0);
            left.Controls.Add(historyCard, 0, 1);

            TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = DS.BgPage, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 24f));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 34f));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            Panel actionCard = MakeCard("QUICK ACTIONS");
            BuildQuickActionsCard(actionCard, btnClear);
            Panel summaryCard = MakeCard("INVOICE SUMMARY");
            BuildInvoiceSummaryCard(summaryCard);
            Panel statusCard = MakeCard("RECENT STATUS");
            BuildStatusCard(statusCard);
            right.Controls.Add(actionCard, 0, 0);
            right.Controls.Add(summaryCard, 0, 1);
            right.Controls.Add(statusCard, 0, 2);

            body.Controls.Add(left, 0, 0);
            body.Controls.Add(right, 1, 0);
            this.Controls.Add(body);
            this.Controls.Add(header);
        }

        private void BuildPaymentsOverviewLayout()
        {
            Controls.Clear();
            BackColor = PayPageBg;
            Panel root = new Panel { Dock = DockStyle.Fill, BackColor = PayPageBg, Padding = new Padding(22, 18, 22, 16) };
            Control header = BuildPaymentsOverviewHeader();
            header.Dock = DockStyle.Fill;

            TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = PayPageBg };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel main = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PayPageBg, Padding = new Padding(0, 0, 10, 0) };
            FlowLayoutPanel stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = PayPageBg };
            _payStatsFlow = new FlowLayoutPanel { Height = 98, WrapContents = false, AutoScroll = false, BackColor = PayPageBg, Padding = new Padding(0, 2, 0, 4) };
            stack.Controls.Add(_payStatsFlow);
            stack.Controls.Add(BuildPaymentsWorkflowRow());
            stack.Controls.Add(BuildPaymentsChartsRow());
            stack.Controls.Add(BuildTransactionsCard());
            main.Controls.Add(stack);
            main.Resize += (s, e) =>
            {
                int width = Math.Max(980, main.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);
                foreach (Control child in stack.Controls)
                    child.Width = width;
            };

            body.Controls.Add(main, 0, 0);
            body.Controls.Add(BuildPaymentsRightSidebar(), 1, 0);
            TableLayoutPanel shell = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = PayPageBg, ColumnCount = 1, RowCount = 2 };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            shell.Controls.Add(header, 0, 0);
            shell.Controls.Add(body, 0, 1);
            root.Controls.Add(shell);
            Controls.Add(root);
            BuildOverviewData();
            if (_showOverview && IsHandleCreated)
                BeginInvoke((Action)(() => { if (_showOverview) RefreshPaymentsOverview(); }));
            else
                HandleCreated += PaymentsOverviewHandleCreated;
        }

        private void PaymentsOverviewHandleCreated(object sender, EventArgs e)
        {
            HandleCreated -= PaymentsOverviewHandleCreated;
            if (_showOverview)
                BeginInvoke((Action)(() => { if (_showOverview) RefreshPaymentsOverview(); }));
        }

        private Control BuildPaymentsOverviewHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = PayPageBg, Padding = new Padding(0, 0, 0, 8) };
            Label title = new Label { Text = "Payments Management", Location = new Point(0, 0), Size = new Size(360, 28), Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = PayText, BackColor = PayPageBg };
            Label subtitle = new Label { Text = "Track receipts, receivables, refunds, and supplier payments.", Location = new Point(1, 31), Size = new Size(560, 20), Font = new Font("Segoe UI", 8.8f), ForeColor = PayMuted, BackColor = PayPageBg };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            _dateRangeLabel = MakePayHeaderButton(_overviewStart.ToString("dd MMM yyyy") + " - " + _overviewEnd.ToString("dd MMM yyyy"), 196, PayText);
            _dateRangeLabel.Click += (s, e) => SelectPaymentDateRange();
            Label filters = MakePayHeaderButton("Filters", 82, PayText);
            filters.Click += (s, e) => OpenPaymentFilters();
            Label import = MakePayHeaderButton("Import", 82, InfoBlue);
            import.Click += (s, e) => ImportUiHelper.ShowDirectionalImportMenu(import, ExcelImportModule.Payments, FindForm());
            Label report = MakePayHeaderButton("Download Report", 132, InfoBlue);
            report.Click += (s, e) => ExportPaymentsCsv();
            Button newPayment = MakePayButton("+ New Payment", InfoBlue, 126);
            newPayment.Click += async (s, e) => await OpenNewPaymentFormAsync();
            Label bell = MakePayIcon("!", Color.FromArgb(254, 226, 226), PayRed);
            bell.Cursor = Cursors.Hand;
            bell.Click += (s, e) => ShowPaymentAlerts();
            Panel user = BuildSessionUserPanel();
            header.Controls.AddRange(new Control[] { _dateRangeLabel, filters, import, report, newPayment, bell, user });
            Action layoutHeaderControls = () =>
            {
                int top = header.Width < 1260 ? 38 : 4;
                user.Visible = header.Width >= 1220;
                user.Location = new Point(header.Width - user.Width, top);
                bell.Location = new Point((user.Visible ? user.Left : header.Width) - 38, top + 1);
                newPayment.Location = new Point(bell.Left - 134, top);
                report.Location = new Point(newPayment.Left - 140, top + 1);
                import.Location = new Point(report.Left - 90, top + 1);
                filters.Location = new Point(import.Left - 90, top + 1);
                _dateRangeLabel.Location = new Point(filters.Left - 206, top + 1);
                bool showAllActions = _dateRangeLabel.Left > title.Right + 16;
                _dateRangeLabel.Visible = showAllActions;
                filters.Visible = showAllActions;
                import.Visible = showAllActions;
                report.Visible = showAllActions;
                bell.Visible = showAllActions;
            };
            header.Resize += (s, e) => layoutHeaderControls();
            layoutHeaderControls();
            return header;
        }

        private Panel BuildSessionUserPanel()
        {
            AppUserDto user = SessionManager.CurrentUser;
            string name = !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName : (!string.IsNullOrWhiteSpace(user?.Username) ? user.Username : Environment.UserName);
            string role = !string.IsNullOrWhiteSpace(user?.RoleName) ? user.RoleName : "User";
            string initials = string.Concat(name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
            if (string.IsNullOrWhiteSpace(initials)) initials = "U";
            Panel panel = new Panel { Size = new Size(150, 38), BackColor = PayPageBg };
            panel.Controls.Add(new Label { Text = initials, Location = new Point(0, 4), Size = new Size(30, 30), BackColor = Color.FromArgb(219, 234, 254), ForeColor = InfoBlue, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter });
            panel.Controls.Add(new Label { Text = name, Location = new Point(38, 1), Size = new Size(92, 17), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = PayText, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = role, Location = new Point(38, 18), Size = new Size(80, 15), Font = new Font("Segoe UI", 7.2f), ForeColor = PayMuted, AutoEllipsis = true });
            panel.Controls.Add(new Label { Text = "v", Location = new Point(132, 8), Size = new Size(14, 18), ForeColor = PayMuted, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private Control BuildPaymentsWorkflowRow()
        {
            Panel card = MakePayCard("Payment Workflow");
            card.Dock = DockStyle.None;
            card.Height = 300;
            card.Margin = new Padding(0, 0, 0, 12);
            Control workflow = SharedUiPrimitives.BuildDirectionalWorkflowLayout(
                "Supplier Workflow",
                "Sent to Suppliers",
                Enumerable.Empty<string>(),
                "Received from Suppliers",
                Enumerable.Empty<string>(),
                "Client Workflow",
                "Sent to Clients",
                BuildPaymentInvoiceLines(false),
                "Received from Clients",
                BuildPaymentReceiptLines());
            workflow.Location = new Point(10, 42);
            workflow.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
            card.Controls.Add(workflow);
            card.Resize += (s, e) => workflow.Size = new Size(Math.Max(120, card.ClientSize.Width - 20), Math.Max(120, card.ClientSize.Height - 54));
            return card;
        }

        private IEnumerable<string> BuildPaymentReceiptLines()
        {
            return (_allPayments ?? new List<Payment>())
                .OrderByDescending(payment => payment.PaymentDate)
                .Take(4)
                .Select(payment => Safe(payment.PaymentNumber, "Receipt") + " - " + Safe(payment.ClientName, "Client") + " - " + IndiaFormatHelper.FormatCurrency(payment.AmountPaid));
        }

        private IEnumerable<string> BuildPaymentInvoiceLines(bool paid)
        {
            return (_invoiceLookup ?? new List<Invoice>())
                .Where(invoice => paid ? invoice.TotalAmount - invoice.PaidAmount <= 0.01m : invoice.TotalAmount - invoice.PaidAmount > 0.01m)
                .OrderByDescending(invoice => invoice.InvoiceDate)
                .Take(4)
                .Select(invoice => Safe(invoice.InvoiceNumber, "Invoice") + " - " + Safe(invoice.ClientName, "Client") + " - " + IndiaFormatHelper.FormatCurrency(Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount)));
        }

        private Control BuildPaymentsChartsRow()
        {
            TableLayoutPanel row = new TableLayoutPanel { Height = 238, ColumnCount = 4, RowCount = 1, BackColor = PayPageBg, Margin = new Padding(0, 0, 0, 12) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            Panel cash = MakePayCard("Cash Flow Trend");
            _payCashChart = new PayCashFlowChart { Location = new Point(14, 48), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            Label cashLegend = new Label { Text = "● Receipts    ● Payments    ● Net Cash Flow", Location = new Point(18, 30), Size = new Size(280, 18), Font = new Font("Segoe UI", 7.5f), ForeColor = PayMuted, AutoEllipsis = true };
            Label cashFilter = MakeSmallFilter("This Month", cash);
            cash.Controls.Add(cashLegend);
            cash.Controls.Add(cashFilter);
            cash.Controls.Add(_payCashChart);
            cash.Resize += (s, e) =>
            {
                cashLegend.Width = Math.Max(120, cash.ClientSize.Width - 142);
                cashFilter.Visible = cash.ClientSize.Width >= 300;
                _payCashChart.Size = new Size(Math.Max(120, cash.Width - 28), Math.Max(110, cash.Height - 62));
            };

            Panel donut = MakePayCard("Receipts vs Payments");
            _payDonutChart = new PayDonutChart { Location = new Point(12, 42), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            donut.Controls.Add(MakeSmallFilter("This Month", donut));
            donut.Controls.Add(_payDonutChart);
            donut.Resize += (s, e) => _payDonutChart.Size = new Size(donut.Width - 24, donut.Height - 52);

            Panel aging = MakePayCard("Aging Summary (Receivables)  i");
            _agingTable = new TableLayoutPanel { Location = new Point(14, 44), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom, ColumnCount = 3, RowCount = 6, BackColor = Color.White };
            _agingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            _agingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            _agingTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            aging.Controls.Add(_agingTable);
            aging.Resize += (s, e) => _agingTable.Size = new Size(aging.Width - 28, aging.Height - 58);

            Panel methods = MakePayCard("Payment Methods (Receipts)  i");
            methods.Controls.Add(MakeSmallFilter("This Month", methods));
            _methodList = new FlowLayoutPanel { Location = new Point(14, 44), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
            methods.Controls.Add(_methodList);
            methods.Resize += (s, e) => _methodList.Size = new Size(methods.Width - 28, methods.Height - 58);

            row.Controls.Add(cash, 0, 0);
            row.Controls.Add(donut, 1, 0);
            row.Controls.Add(aging, 2, 0);
            row.Controls.Add(methods, 3, 0);
            return row;
        }

        private Control BuildTransactionsCard()
        {
            Panel card = MakePayCard("");
            card.Height = 384;
            card.Margin = new Padding(0, 0, 0, 14);
            string[] tabs = { "All Transactions", "Receipts", "Payments Made", "Refunds" };
            int x = 14;
            foreach (string tab in tabs)
            {
                Label label = new Label { Text = tab, Location = new Point(x, 12), Size = new Size(tab == "All Transactions" ? 110 : 92, 26), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = tab == _overviewTab ? InfoBlue : PayMuted, Cursor = Cursors.Hand };
                string captured = tab;
                label.Click += (s, e) => { _overviewTab = captured; _overviewPage = 1; RefreshTransactionsTable(); };
                card.Controls.Add(label);
                x += label.Width + 14;
            }
            Panel txnTypeHost = new Panel { Name = "TxnTypeFilterHost", Size = new Size(110, 32), BackColor = DS.BgInput, Padding = new Padding(6, 1, 6, 1) };
            _txnTypeFilter = new ComboBox { Name = "TxnTypeFilter", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f) };
            _txnTypeFilter.Items.AddRange(new object[] { "All Types", "Receipt", "Payment Made", "Refund" });
            _txnTypeFilter.SelectedIndex = 0;
            _txnTypeFilter.SelectedIndexChanged += (s, e) => { _overviewPage = 1; RefreshTransactionsTable(); };
            txnTypeHost.Controls.Add(_txnTypeFilter);
            Panel txnSearchHost = new Panel { Name = "TxnSearchHost", Size = new Size(210, 32), BackColor = DS.BgInput, Padding = new Padding(8, 2, 8, 2) };
            _txnSearch = new TextBox { Name = "TxnSearchTextBox", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8f), BorderStyle = BorderStyle.None, Text = "Search by reference, customer, invoice...", ForeColor = DS.Slate400 };
            AddDashboardPlaceholder(_txnSearch, "Search by reference, customer, invoice...");
            _txnSearch.TextChanged += (s, e) => { _overviewPage = 1; RefreshTransactionsTable(); };
            txnSearchHost.Controls.Add(_txnSearch);
            Button export = MakePayOutlineButton("Export", 76);
            export.Click += (s, e) => ExportPaymentsCsv();
            _txnTable = new TableLayoutPanel { ColumnCount = 9, RowCount = 9, Location = new Point(14, 76), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom, BackColor = Color.White };
            int[] widths = { 10, 11, 13, 16, 13, 10, 10, 10, 7 };
            foreach (int width in widths) _txnTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            _txnPager = new GlobalPaginationControl { Height = 34, Width = 560, BackColor = Color.Transparent };
            _txnPager.PageChanged += (s, e) => { _overviewPage = _txnPager.CurrentPage; RefreshTransactionsTable(); };
            _txnPager.PageSizeChanged += (s, e) => { _overviewPageSize = _txnPager.PageSize; _overviewPage = 1; RefreshTransactionsTable(); };
            card.Controls.AddRange(new Control[] { txnTypeHost, txnSearchHost, export, _txnTable, _txnPager });
            card.Resize += (s, e) =>
            {
                export.Location = new Point(card.Width - 92, 44);
                txnSearchHost.Location = new Point(export.Left - 220, 42);
                txnTypeHost.Location = new Point(txnSearchHost.Left - 120, 42);
                _txnTable.Size = new Size(card.Width - 28, card.Height - 126);
                _txnPager.Location = new Point(card.Width - _txnPager.Width - 16, card.Height - 40);
            };
            return card;
        }

        private Control BuildPaymentsRightSidebar()
        {
            FlowLayoutPanel side = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8, 0, 0, 0), BackColor = PayPageBg };
            Panel settlements = MakePayCard("Upcoming Settlements");
            settlements.Dock = DockStyle.None; settlements.Width = 314; settlements.Height = 214;
            Label viewSettlements = new Label { Text = "View All", Location = new Point(254, 14), Size = new Size(50, 18), ForeColor = InfoBlue, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Cursor = Cursors.Hand };
            viewSettlements.Click += (s, e) => ShowUpcomingSettlements();
            settlements.Controls.Add(viewSettlements);
            _settlementsList = new FlowLayoutPanel { Location = new Point(14, 42), Size = new Size(286, 160), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
            settlements.Controls.Add(_settlementsList);

            Panel quick = MakePayCard("Quick Actions");
            quick.Dock = DockStyle.None; quick.Width = 314; quick.Height = 178;
            AddPayQuickButton(quick, "Record Receipt", 14, 42, 138, SaveGreen);
            AddPayQuickButton(quick, "Make Payment", 162, 42, 138, PayRed);
            AddPayQuickButton(quick, "Add Refund", 14, 80, 138, InfoBlue);
            AddPayQuickButton(quick, "Create Link", 162, 80, 138, PayPurple);
            AddPayQuickButton(quick, "Reconcile Bank", 14, 118, 138, PayMuted);
            AddPayQuickButton(quick, "View Reports", 162, 118, 138, OrangeCol);

            Panel alerts = MakePayCard("Smart Alerts  i");
            alerts.Dock = DockStyle.None; alerts.Width = 314; alerts.Height = 180;
            Label viewAlerts = new Label { Text = "View All", Location = new Point(254, 14), Size = new Size(50, 18), ForeColor = InfoBlue, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Cursor = Cursors.Hand };
            viewAlerts.Click += (s, e) => ShowPaymentAlerts();
            alerts.Controls.Add(viewAlerts);
            _alertsList = new FlowLayoutPanel { Location = new Point(14, 42), Size = new Size(286, 126), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
            alerts.Controls.Add(_alertsList);
            side.Controls.Add(settlements);
            side.Controls.Add(quick);
            side.Controls.Add(alerts);
            return side;
        }

        private void BuildOverviewData()
        {
            DateTime today = DateTime.Today;
            _overviewTransactions = new List<PaymentTxn>();
            int index = 1;
            foreach (Payment payment in _allPayments ?? new List<Payment>())
            {
                _overviewTransactions.Add(new PaymentTxn
                {
                    Id = "R" + payment.PaymentID,
                    ReferenceNo = string.IsNullOrWhiteSpace(payment.PaymentNumber) ? "RCPT-" + index.ToString("000000") : payment.PaymentNumber.Replace("PAY", "RCPT"),
                    Type = "Receipt",
                    Date = payment.PaymentDate == default ? today : payment.PaymentDate,
                    CustomerVendor = Safe(payment.ClientName, "Customer #" + payment.ClientID),
                    InvoiceBillNo = Safe(payment.InvoiceNumber, "INV-" + payment.InvoiceID.ToString("0000")),
                    Mode = NormalizePaymentMode(payment.PaymentMode),
                    Amount = payment.AmountPaid,
                    Status = "Paid"
                });
                index++;
            }

            _overviewReceivables = (_invoiceLookup ?? new List<Invoice>())
                .Where(i => (i.TotalAmount - i.PaidAmount) > 0.01m)
                .Select(i => new ReceivableRow
                {
                    CustomerName = Safe(i.ClientName, "Customer #" + i.ClientID),
                    InvoiceNo = Safe(i.InvoiceNumber, "INV-" + i.InvoiceID.ToString("0000")),
                    Amount = i.TotalAmount,
                    DueDate = i.DueDate == default ? today.AddDays(7) : i.DueDate,
                    PaidAmount = i.PaidAmount,
                    AgingDays = Math.Max(0, (today - i.InvoiceDate.Date).Days)
                }).ToList();
        }

        private void RefreshPaymentsOverview()
        {
            List<PaymentTxn> period = GetPeriodTransactions(_overviewStart, _overviewEnd);
            decimal receipts = period.Where(t => t.Type == "Receipt").Sum(t => t.Amount);
            decimal payments = period.Where(t => t.Type == "Payment Made").Sum(t => t.Amount);
            decimal refunds = period.Where(t => t.Type == "Refund").Sum(t => t.Amount);
            decimal outstanding = _overviewReceivables.Sum(r => Math.Max(0m, r.Amount - r.PaidAmount));
            decimal overdue = _overviewReceivables.Where(r => r.DueDate.Date < DateTime.Today).Sum(r => Math.Max(0m, r.Amount - r.PaidAmount));

            _payStatsFlow.Controls.Clear();
            _payStatsFlow.Controls.Add(MakePayStat("Total Receipts", CompactCurrency(receipts), TrendText(receipts, GetPreviousTotal("Receipt")), SaveGreen, ModernIconKind.Import));
            _payStatsFlow.Controls.Add(MakePayStat("Total Payments (Made)", CompactCurrency(payments), TrendText(payments, GetPreviousTotal("Payment Made")), PayRed, ModernIconKind.Export));
            _payStatsFlow.Controls.Add(MakePayStat("Net Cash Flow", CompactCurrency(receipts - payments), TrendText(receipts - payments, GetPreviousTotal("Receipt") - GetPreviousTotal("Payment Made")), InfoBlue, ModernIconKind.Payment));
            _payStatsFlow.Controls.Add(MakePayStat("Outstanding Receivables", CompactCurrency(outstanding), "↓ 5.6% vs last month", PayPurple, ModernIconKind.Calendar));
            _payStatsFlow.Controls.Add(MakePayStat("Overdue Amount", CompactCurrency(overdue), "↓ 2.3% vs last month", OrangeCol, ModernIconKind.Alert));

            _payCashChart.SetData(BuildDailyTrend());
            _payDonutChart.SetData(receipts, payments);
            RefreshAgingTable();
            RefreshMethodList(period);
            RefreshTransactionsTable();
            RefreshPaymentsSidebar();
        }

        private void RefreshAgingTable()
        {
            _agingTable.Controls.Clear();
            _agingTable.RowStyles.Clear();
            for (int i = 0; i < 6; i++) _agingTable.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            string[] headers = { "Aging Bucket", "Amount (₹)", "% Total" };
            for (int i = 0; i < headers.Length; i++) _agingTable.Controls.Add(MakePayCell(headers[i], true, PayText), i, 0);
            var buckets = new[]
            {
                new { Label = "0 - 30 Days", Rows = _overviewReceivables.Where(r => r.AgingDays <= 30).ToList() },
                new { Label = "31 - 60 Days", Rows = _overviewReceivables.Where(r => r.AgingDays > 30 && r.AgingDays <= 60).ToList() },
                new { Label = "61 - 90 Days", Rows = _overviewReceivables.Where(r => r.AgingDays > 60 && r.AgingDays <= 90).ToList() },
                new { Label = "90+ Days", Rows = _overviewReceivables.Where(r => r.AgingDays > 90).ToList() }
            };
            decimal total = Math.Max(1m, _overviewReceivables.Sum(r => Math.Max(0m, r.Amount - r.PaidAmount)));
            int row = 1;
            foreach (var bucket in buckets)
            {
                decimal amount = bucket.Rows.Sum(r => Math.Max(0m, r.Amount - r.PaidAmount));
                decimal pct = Math.Round(amount * 100m / total, 1);
                _agingTable.Controls.Add(MakePayCell(bucket.Label, false, PayText), 0, row);
                _agingTable.Controls.Add(MakePayCell(CompactCurrency(amount), false, PayText), 1, row);
                _agingTable.Controls.Add(MakePercentBadge(pct), 2, row);
                row++;
            }
            _agingTable.Controls.Add(MakePayCell("Total", true, PayText), 0, 5);
            _agingTable.Controls.Add(MakePayCell(CompactCurrency(total), true, PayText), 1, 5);
            _agingTable.Controls.Add(MakePayCell("100%", true, PayText), 2, 5);
        }

        private void RefreshMethodList(List<PaymentTxn> period)
        {
            _methodList.Controls.Clear();
            var receipts = period.Where(t => t.Type == "Receipt").ToList();
            decimal total = Math.Max(1m, receipts.Sum(t => t.Amount));
            string[] modes = { "Bank Transfer", "UPI", "Card", "Cash", "Wallet" };
            Color[] colors = { InfoBlue, PayPurple, OrangeCol, SaveGreen, Color.FromArgb(20, 184, 166) };
            for (int i = 0; i < modes.Length; i++)
            {
                decimal amount = receipts.Where(t => t.Mode == modes[i]).Sum(t => t.Amount);
                decimal pct = Math.Round(amount * 100m / total, 1);
                _methodList.Controls.Add(MakeMethodRow(modes[i], amount, pct, colors[i]));
            }
        }

        private void RefreshTransactionsTable()
        {
            List<PaymentTxn> rows = GetFilteredTransactions();
            int pageSize = Math.Max(1, _overviewPageSize);
            _overviewPage = PaginationState.NormalizePage(_overviewPage, rows.Count, pageSize);
            List<PaymentTxn> page = rows.Skip((_overviewPage - 1) * pageSize).Take(pageSize).ToList();
            _txnTable.SuspendLayout();
            _txnTable.Controls.Clear();
            _txnTable.RowStyles.Clear();
            _txnTable.RowCount = pageSize + 1;
            _txnTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            for (int i = 0; i < pageSize; i++) _txnTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / pageSize));
            string[] headers = { "Date", "Type", "Reference No.", "Customer / Supplier", "Invoice / Bill No.", "Mode", "Amount (₹)", "Status", "Actions" };
            for (int i = 0; i < headers.Length; i++) _txnTable.Controls.Add(MakePayCell(headers[i], true, PayText), i, 0);
            for (int r = 0; r < pageSize; r++)
            {
                if (r >= page.Count)
                {
                    for (int c = 0; c < headers.Length; c++) _txnTable.Controls.Add(MakePayCell("", false, PayText), c, r + 1);
                    continue;
                }
                PaymentTxn txn = page[r];
                _txnTable.Controls.Add(MakePayCell(txn.Date.ToString("dd MMM yyyy"), false, PayText), 0, r + 1);
                _txnTable.Controls.Add(MakeTypeBadge(txn.Type), 1, r + 1);
                _txnTable.Controls.Add(MakePayCell(txn.ReferenceNo, false, PayMuted), 2, r + 1);
                _txnTable.Controls.Add(MakePayCell(txn.CustomerVendor, false, PayText), 3, r + 1);
                _txnTable.Controls.Add(MakePayCell(txn.InvoiceBillNo, false, InfoBlue), 4, r + 1);
                _txnTable.Controls.Add(MakePayCell(txn.Mode, false, PayText), 5, r + 1);
                _txnTable.Controls.Add(MakePayCell(IndiaFormatHelper.FormatCurrency(txn.Amount), false, PayText), 6, r + 1);
                _txnTable.Controls.Add(MakeStatusBadge(txn.Status), 7, r + 1);
                _txnTable.Controls.Add(MakePayCell("👁   ↓", false, PayMuted), 8, r + 1);
            }
            _txnTable.ResumeLayout(true);
            _txnPager.SetState(_overviewPage, rows.Count, pageSize);
        }

        private void RefreshPaymentsSidebar()
        {
            _settlementsList.Controls.Clear();
            foreach (ReceivableRow r in _overviewReceivables.Where(r => r.DueDate >= DateTime.Today && r.DueDate <= DateTime.Today.AddDays(14)).OrderBy(r => r.DueDate).Take(5))
                _settlementsList.Controls.Add(MakeSettlementRow(r));

            _alertsList.Controls.Clear();
            int oldOverdue = _overviewReceivables.Count(r => r.DueDate < DateTime.Today && r.AgingDays > 90);
            int pending = _overviewTransactions.Count(t => t.Status == "Pending");
            _alertsList.Controls.Add(MakeAlertRow("●", oldOverdue + " invoices are overdue for more than 90 days", PayRed));
            _alertsList.Controls.Add(MakeAlertRow("●", pending + " payments are pending approval", OrangeCol));
            _alertsList.Controls.Add(MakeAlertRow("●", "Bank statement imported successfully", SaveGreen));
        }

        private List<PaymentTxn> GetFilteredTransactions()
        {
            IEnumerable<PaymentTxn> query = _overviewTransactions.Where(t => t.Date.Date >= _overviewStart.Date && t.Date.Date <= _overviewEnd.Date);
            if (_overviewTab == "Receipts") query = query.Where(t => t.Type == "Receipt");
            else if (_overviewTab == "Payments Made") query = query.Where(t => t.Type == "Payment Made");
            else if (_overviewTab == "Refunds") query = query.Where(t => t.Type == "Refund");
            string type = _txnTypeFilter?.SelectedItem?.ToString() ?? "All Types";
            if (type != "All Types") query = query.Where(t => t.Type == type);
            string search = GetTextFilter(_txnSearch, "Search by reference, customer, invoice...");
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => (t.ReferenceNo ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (t.CustomerVendor ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (t.InvoiceBillNo ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            return query.OrderByDescending(t => t.Date).ToList();
        }

        private List<PaymentTxn> GetPeriodTransactions(DateTime start, DateTime end)
        {
            return _overviewTransactions.Where(t => t.Date.Date >= start.Date && t.Date.Date <= end.Date).ToList();
        }

        private decimal GetPreviousTotal(string type)
        {
            int days = Math.Max(1, (_overviewEnd.Date - _overviewStart.Date).Days + 1);
            DateTime end = _overviewStart.AddDays(-1);
            DateTime start = end.AddDays(-(days - 1));
            return _overviewTransactions.Where(t => t.Type == type && t.Date.Date >= start && t.Date.Date <= end).Sum(t => t.Amount);
        }

        private List<PayTrendPoint> BuildDailyTrend()
        {
            var points = new List<PayTrendPoint>();
            for (DateTime day = _overviewStart.Date; day <= _overviewEnd.Date; day = day.AddDays(1))
            {
                decimal receipts = _overviewTransactions.Where(t => t.Date.Date == day && t.Type == "Receipt").Sum(t => t.Amount);
                decimal payments = _overviewTransactions.Where(t => t.Date.Date == day && t.Type == "Payment Made").Sum(t => t.Amount);
                points.Add(new PayTrendPoint { Date = day, Receipts = receipts, Payments = payments, Net = receipts - payments });
            }
            return points;
        }

        private static string TrendText(decimal current, decimal previous)
        {
            decimal pct = previous == 0 ? (current == 0 ? 0 : 100) : Math.Round((current - previous) * 100m / Math.Abs(previous), 1);
            return (pct >= 0 ? "↑ " : "↓ ") + Math.Abs(pct).ToString("0.#") + "% vs last month";
        }

        private static string NormalizePaymentMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return "Bank Transfer";
            mode = mode.Trim();
            if (mode.Equals("NEFT/RTGS", StringComparison.OrdinalIgnoreCase) || mode.Equals("Cheque", StringComparison.OrdinalIgnoreCase) || mode.Equals("DD", StringComparison.OrdinalIgnoreCase)) return "Bank Transfer";
            if (mode.Equals("Card", StringComparison.OrdinalIgnoreCase) || mode.Equals("Cash", StringComparison.OrdinalIgnoreCase) || mode.Equals("Wallet", StringComparison.OrdinalIgnoreCase) || mode.Equals("UPI", StringComparison.OrdinalIgnoreCase)) return mode;
            return "Bank Transfer";
        }

        private Panel MakePayCard(string title)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = PaySurface, Margin = new Padding(0, 0, 10, 0) };
            panel.MinimumSize = GetPaymentDashboardCardMinimum(title);
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(PaySurface))
                using (Pen pen = new Pen(PayBorder))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            if (!string.IsNullOrWhiteSpace(title))
                panel.Controls.Add(new Label { Text = title, Location = new Point(14, 12), Size = new Size(230, 22), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = PayText });
            AttachPaymentDashboardResize(panel);
            return panel;
        }

        private static Size GetPaymentDashboardCardMinimum(string title)
        {
            if (string.Equals(title, "Cash Flow Trend", StringComparison.OrdinalIgnoreCase))
                return new Size(330, 170);
            if (string.Equals(title, "Receipts vs Payments", StringComparison.OrdinalIgnoreCase))
                return new Size(260, 170);
            if (!string.IsNullOrWhiteSpace(title) && title.IndexOf("Aging Summary", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Size(270, 170);
            if (!string.IsNullOrWhiteSpace(title) && title.IndexOf("Payment Methods", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Size(250, 170);
            return new Size(180, 96);
        }

        private void AttachPaymentDashboardResize(Panel card)
        {
            CardResizeGripService.Attach(card, ApplyPaymentDashboardCardSize, ApplyPaymentDashboardCardSize);
        }

        private void ApplyPaymentDashboardCardSize(Control card, Size size)
        {
            if (card == null || card.Parent == null)
                return;

            TableLayoutPanel table = card.Parent as TableLayoutPanel;
            if (table != null)
            {
                int targetHeight = Math.Max(card.MinimumSize.Height, size.Height);
                int targetWidth = Math.Max(card.MinimumSize.Width, size.Width);
                if (table.RowCount == 1 && table.Height < targetHeight)
                    table.Height = targetHeight;
                if (table.RowCount == 1 && table.RowStyles.Count > 0)
                    table.RowStyles[0] = new RowStyle(SizeType.Absolute, targetHeight);

                int column = table.GetColumn(card);
                if (column >= 0 && column < table.ColumnStyles.Count && size.Width > 0)
                    table.ColumnStyles[column] = new ColumnStyle(SizeType.Absolute, targetWidth);
            }
            else if (card.Parent is FlowLayoutPanel)
            {
                card.Dock = DockStyle.None;
                card.Size = new Size(Math.Max(card.MinimumSize.Width, size.Width), Math.Max(card.MinimumSize.Height, size.Height));
            }

            card.Parent.PerformLayout();
        }

        private Panel MakePayStat(string label, string value, string trend, Color accent, ModernIconKind icon)
        {
            Panel card = MakePayCard("");
            card.Dock = DockStyle.None;
            card.Width = 210;
            card.Height = 88;
            card.Margin = new Padding(0, 0, 10, 0);
            Label badge = new Label { Location = new Point(14, 18), Size = new Size(40, 40), BackColor = DS.Lighten(accent, 0.84f), Image = ModernIconSystem.IconBitmap(icon, 20, accent), ImageAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(badge, 12);
            card.Controls.Add(badge);
            card.Controls.Add(new Label { Text = label, Location = new Point(68, 13), Size = new Size(130, 18), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = PayMuted, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = value, Location = new Point(68, 32), Size = new Size(130, 25), Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = PayText, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = trend, Location = new Point(68, 60), Size = new Size(130, 18), Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = trend.StartsWith("↓") ? PayRed : SaveGreen, AutoEllipsis = true });
            return card;
        }

        private Label MakePayHeaderButton(string text, int width, Color fore)
        {
            return new Label { Text = text, Size = new Size(width, 32), BackColor = Color.White, ForeColor = fore, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, AutoEllipsis = true };
        }

        private Label MakePayIcon(string text, Color back, Color fore)
        {
            Label label = new Label { Text = text, Size = new Size(30, 30), BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 9f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(label, 8);
            return label;
        }

        private Button MakePayButton(string text, Color back, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 32, BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.3f, FontStyle.Bold), UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = 0;
            DS.Rounded(b, 7);
            return b;
        }

        private Button MakePayOutlineButton(string text, int width)
        {
            Button b = MakePayButton(text, Color.White, width);
            b.ForeColor = InfoBlue;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = PayBorder;
            return b;
        }

        private Label MakeSmallFilter(string text, Control parent)
        {
            Label filter = MakePayHeaderButton(text + "  v", 96, PayText);
            filter.Size = new Size(96, 26);
            filter.Location = new Point(Math.Max(12, parent.Width - 110), 12);
            parent.Resize += (s, e) => filter.Location = new Point(parent.Width - 110, 12);
            return filter;
        }

        private Label MakePayCell(string text, bool header, Color fore)
        {
            Label label = new Label { Text = text, Dock = DockStyle.Fill, BackColor = header ? Color.FromArgb(248, 250, 252) : Color.White, ForeColor = fore, Font = new Font("Segoe UI", header ? 7.5f : 7.3f, header ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(5, 0, 2, 0), AutoEllipsis = true };
            label.Paint += (s, e) => { using (Pen pen = new Pen(Color.FromArgb(241, 245, 249))) e.Graphics.DrawLine(pen, 0, label.Height - 1, label.Width, label.Height - 1); };
            return label;
        }

        private Control MakeTypeBadge(string type)
        {
            Color color = type == "Receipt" ? SaveGreen : type == "Refund" ? PayPurple : PayRed;
            return MakePill(type, color);
        }

        private Control MakeStatusBadge(string status)
        {
            Color color = status == "Paid" ? SaveGreen : status == "Refunded" ? PayPurple : status == "Overdue" ? PayRed : OrangeCol;
            return MakePill(status, color);
        }

        private Control MakePill(string text, Color color)
        {
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(4, 6, 4, 6) };
            Label pill = new Label { Text = text, Width = 86, Height = 21, BackColor = DS.Lighten(color, 0.84f), ForeColor = color, Font = new Font("Segoe UI", 6.8f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            DS.Rounded(pill, 10);
            host.Controls.Add(pill);
            return host;
        }

        private Control MakePercentBadge(decimal pct)
        {
            Color color = pct <= 30 ? SaveGreen : pct <= 50 ? OrangeCol : pct <= 70 ? Color.FromArgb(234, 88, 12) : PayRed;
            return MakePill(pct.ToString("0.#") + "%", color);
        }

        private Control MakeMethodRow(string method, decimal amount, decimal pct, Color color)
        {
            Panel row = new Panel { Width = 210, Height = 30, BackColor = Color.White, Margin = new Padding(0, 0, 0, 5) };
            row.Controls.Add(new Label { Text = "■", Location = new Point(0, 5), Size = new Size(18, 18), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter });
            row.Controls.Add(new Label { Text = method, Location = new Point(20, 4), Size = new Size(78, 18), ForeColor = PayText, Font = new Font("Segoe UI", 7.6f, FontStyle.Bold), AutoEllipsis = true });
            Panel barBg = new Panel { Location = new Point(100, 11), Size = new Size(52, 5), BackColor = Color.FromArgb(226, 232, 240) };
            Panel bar = new Panel { Location = new Point(0, 0), Size = new Size((int)Math.Max(2, Math.Min(52, pct * 52m / 100m)), 5), BackColor = color };
            barBg.Controls.Add(bar);
            row.Controls.Add(barBg);
            row.Controls.Add(new Label { Text = CompactCurrency(amount), Location = new Point(154, 1), Size = new Size(56, 15), ForeColor = PayText, Font = new Font("Segoe UI", 7.2f), TextAlign = ContentAlignment.MiddleRight });
            row.Controls.Add(new Label { Text = pct.ToString("0.#") + "%", Location = new Point(154, 15), Size = new Size(56, 14), ForeColor = PayMuted, Font = new Font("Segoe UI", 7f), TextAlign = ContentAlignment.MiddleRight });
            return row;
        }

        private Control MakeSettlementRow(ReceivableRow r)
        {
            Panel row = new Panel { Width = 244, Height = 30, BackColor = Color.White, Margin = new Padding(0, 0, 0, 5) };
            int days = (r.DueDate.Date - DateTime.Today).Days;
            row.Controls.Add(new Label { Text = "▣", Location = new Point(0, 5), Size = new Size(20, 18), ForeColor = days <= 1 ? PayRed : OrangeCol });
            row.Controls.Add(new Label { Text = r.CustomerName, Location = new Point(22, 0), Size = new Size(92, 15), Font = new Font("Segoe UI", 7.3f, FontStyle.Bold), ForeColor = PayText, AutoEllipsis = true });
            row.Controls.Add(new Label { Text = r.InvoiceNo, Location = new Point(22, 15), Size = new Size(92, 14), Font = new Font("Segoe UI", 6.8f), ForeColor = PayMuted, AutoEllipsis = true });
            row.Controls.Add(new Label { Text = CompactCurrency(Math.Max(0m, r.Amount - r.PaidAmount)), Location = new Point(118, 1), Size = new Size(62, 15), Font = new Font("Segoe UI", 7.2f, FontStyle.Bold), ForeColor = PayText, TextAlign = ContentAlignment.MiddleRight });
            row.Controls.Add(new Label { Text = days <= 1 ? "Due Tomorrow" : "Due in " + days + " Days", Location = new Point(182, 14), Size = new Size(62, 14), Font = new Font("Segoe UI", 6.6f), ForeColor = days <= 1 ? PayRed : OrangeCol, TextAlign = ContentAlignment.MiddleRight });
            return row;
        }

        private Control MakeAlertRow(string icon, string text, Color color)
        {
            Panel row = new Panel { Width = 244, Height = 28, BackColor = Color.White, Margin = new Padding(0, 0, 0, 6) };
            row.Controls.Add(new Label { Text = icon, Location = new Point(0, 4), Size = new Size(18, 18), ForeColor = color, TextAlign = ContentAlignment.MiddleCenter });
            row.Controls.Add(new Label { Text = text, Location = new Point(22, 3), Size = new Size(210, 20), ForeColor = PayText, Font = new Font("Segoe UI", 7.2f), AutoEllipsis = true });
            return row;
        }

        private void AddPayQuickButton(Panel parent, string text, int x, int y, int width, Color color)
        {
            Button b = MakePayButton(text, Color.White, width);
            b.Tag = "FIXED_WIDTH";
            b.Location = new Point(x, y);
            b.Height = 28;
            b.ForeColor = color;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = DS.Lighten(color, 0.55f);
            b.Click += (s, e) => HandlePaymentQuickAction(text);
            parent.Controls.Add(b);
        }

        private void HandlePaymentQuickAction(string text)
        {
            if (text.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Make", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Refund", StringComparison.OrdinalIgnoreCase) >= 0)
                _ = OpenNewPaymentFormAsync();
            else if (text.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                MessageBox.Show("Create Payment Link uses the connected payment gateway. Configure gateway credentials in Settings before generating live links.", "Create Payment Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (text.IndexOf("Reconcile", StringComparison.OrdinalIgnoreCase) >= 0)
                MessageBox.Show("Bank reconciliation opens after a bank statement is imported.", "Reconcile Bank", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (text.IndexOf("Reports", StringComparison.OrdinalIgnoreCase) >= 0)
                ExportPaymentsCsv();
        }

        private async Task OpenNewPaymentFormAsync()
        {
            _showOverview = false;
            Controls.Clear();
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            SalesUiPolishService.ApplyAfterRebuild(this, "Payments");
            ApplyPaymentActionHierarchy(Controls);
            ApplyPermissions();
            await LoadInitialDataAsync();
            StartNewPayment();
        }

        private async Task BackToPaymentsOverviewAsync()
        {
            _showOverview = true;
            Controls.Clear();
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            SalesUiPolishService.ApplyAfterRebuild(this, "Payments");
            ApplyPaymentActionHierarchy(Controls);
            ApplyPermissions();
            await LoadPaymentsOverviewAsync();
        }

        private void SelectPaymentDateRange()
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("This Month", null, (s, e) => SetOverviewRange(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1)));
            menu.Items.Add("Last Month", null, (s, e) =>
            {
                DateTime start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
                SetOverviewRange(start, start.AddMonths(1).AddDays(-1));
            });
            menu.Items.Add("This Year", null, (s, e) => SetOverviewRange(new DateTime(DateTime.Today.Year, 1, 1), new DateTime(DateTime.Today.Year, 12, 31)));
            menu.Show(_dateRangeLabel, new Point(0, _dateRangeLabel.Height));
        }

        private void SetOverviewRange(DateTime start, DateTime end)
        {
            _overviewStart = start.Date;
            _overviewEnd = end.Date;
            if (_dateRangeLabel != null)
                _dateRangeLabel.Text = _overviewStart.ToString("dd MMM yyyy") + " - " + _overviewEnd.ToString("dd MMM yyyy");
            _overviewPage = 1;
            RefreshPaymentsOverview();
        }

        private void OpenPaymentFilters()
        {
            if (_txnTypeFilter != null)
            {
                _txnTypeFilter.Focus();
                _txnTypeFilter.DroppedDown = true;
            }
        }

        private void ShowUpcomingSettlements()
        {
            string body = string.Join(Environment.NewLine, _overviewReceivables.Where(r => r.DueDate >= DateTime.Today).OrderBy(r => r.DueDate).Take(20).Select(r => r.CustomerName + " - " + r.InvoiceNo + " - " + IndiaFormatHelper.FormatCurrency(Math.Max(0m, r.Amount - r.PaidAmount)) + " - due " + r.DueDate.ToString("dd MMM yyyy")));
            MessageBox.Show(string.IsNullOrWhiteSpace(body) ? "No upcoming settlements found." : body, "Upcoming Settlements", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPaymentAlerts()
        {
            int oldOverdue = _overviewReceivables.Count(r => r.DueDate < DateTime.Today && r.AgingDays > 90);
            int pending = _overviewTransactions.Count(t => t.Status == "Pending");
            MessageBox.Show(oldOverdue + " invoices are overdue for more than 90 days" + Environment.NewLine + pending + " payments are pending approval" + Environment.NewLine + "Bank statement imported successfully", "Payment Alerts", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportPaymentsCsv()
        {
            List<PaymentTxn> rows = GetFilteredTransactions();
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV files (*.csv)|*.csv";
                dlg.FileName = "payments-" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                StringBuilder csv = new StringBuilder();
                csv.AppendLine("Date,Type,Reference No,Customer / Supplier,Invoice / Bill No,Mode,Amount,Status");
                foreach (PaymentTxn txn in rows)
                    csv.AppendLine(string.Join(",", new[] { Csv(txn.Date.ToString("dd MMM yyyy")), Csv(txn.Type), Csv(txn.ReferenceNo), Csv(txn.CustomerVendor), Csv(txn.InvoiceBillNo), Csv(txn.Mode), Csv(txn.Amount.ToString("0.##")), Csv(txn.Status) }));
                File.WriteAllText(dlg.FileName, csv.ToString(), Encoding.UTF8);
                MessageBox.Show("Payments exported to CSV.", "Payments", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void AddPayPageButton(string text, int page, bool enabled, bool selected = false)
        {
            Button b = new Button { Text = text, Width = 30, Height = 28, Enabled = enabled, BackColor = selected ? InfoBlue : Color.White, ForeColor = selected ? Color.White : PayText, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(2) };
            b.FlatAppearance.BorderColor = PayBorder;
            b.Click += (s, e) => { _overviewPage = page; RefreshTransactionsTable(); };
            _txnPager.Controls.Add(b);
        }

        private void AddDashboardPlaceholder(TextBox textBox, string placeholder)
        {
            textBox.GotFocus += (s, e) =>
            {
                if (textBox.Text == placeholder)
                {
                    textBox.Text = string.Empty;
                    textBox.ForeColor = PayText;
                }
            };
            textBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    textBox.Text = placeholder;
                    textBox.ForeColor = DS.Slate400;
                }
            };
        }

        private static string GetTextFilter(TextBox textBox, string placeholder)
        {
            string text = textBox?.Text ?? string.Empty;
            return text == placeholder ? string.Empty : text.Trim();
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string CompactCurrency(decimal value)
        {
            decimal abs = Math.Abs(value);
            if (abs >= 10000000m) return "₹" + (value / 10000000m).ToString("0.##") + "Cr";
            if (abs >= 100000m) return "₹" + (value / 100000m).ToString("0.##") + "L";
            return IndiaFormatHelper.FormatCurrency(value);
        }

        private static string Csv(string value)
        {
            value = value ?? "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class PaymentTxn
        {
            public string Id;
            public string ReferenceNo;
            public string Type;
            public DateTime Date;
            public string CustomerVendor;
            public string InvoiceBillNo;
            public string Mode;
            public decimal Amount;
            public string Status;
        }

        private sealed class ReceivableRow
        {
            public string CustomerName;
            public string InvoiceNo;
            public decimal Amount;
            public DateTime DueDate;
            public decimal PaidAmount;
            public int AgingDays;
        }

        private sealed class PayTrendPoint
        {
            public DateTime Date;
            public decimal Receipts;
            public decimal Payments;
            public decimal Net;
        }

        private sealed class PayCashFlowChart : Control
        {
            private List<PayTrendPoint> _points = new List<PayTrendPoint>();
            public void SetData(List<PayTrendPoint> points) { _points = points ?? new List<PayTrendPoint>(); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle plot = new Rectangle(36, 12, Math.Max(20, Width - 48), Math.Max(20, Height - 30));
                decimal max = Math.Max(1m, _points.SelectMany(p => new[] { p.Receipts, p.Payments, Math.Abs(p.Net) }).DefaultIfEmpty(1m).Max());
                using (Pen grid = new Pen(DS.Border))
                using (Brush text = new SolidBrush(PayMuted))
                {
                    for (int i = 0; i <= 4; i++)
                    {
                        int y = plot.Bottom - (plot.Height * i / 4);
                        e.Graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                        e.Graphics.DrawString(i == 0 ? "₹0" : "₹" + (Math.Round(max * i / 4 / 100000m)).ToString("0") + "L", new Font("Segoe UI", 6.5f), text, 0, y - 7);
                    }
                }
                DrawPayLine(e.Graphics, plot, max, p => p.Receipts, SaveGreen);
                DrawPayLine(e.Graphics, plot, max, p => p.Payments, PayRed);
                DrawPayLine(e.Graphics, plot, max, p => p.Net, InfoBlue);
                if (_points.Count > 0)
                {
                    using (Brush b = new SolidBrush(PayMuted))
                    using (Font f = new Font("Segoe UI", 6.5f))
                    {
                        for (int i = 0; i < _points.Count; i += Math.Max(1, _points.Count / 5))
                        {
                            int x = plot.Left + (plot.Width * i / Math.Max(1, _points.Count - 1));
                            e.Graphics.DrawString(_points[i].Date.Day + " " + _points[i].Date.ToString("MMM"), f, b, x - 12, plot.Bottom + 2);
                        }
                    }
                }
            }
            private void DrawPayLine(Graphics g, Rectangle plot, decimal max, Func<PayTrendPoint, decimal> selector, Color color)
            {
                if (_points.Count < 2) return;
                PointF[] pts = _points.Select((p, i) => new PointF(plot.Left + (plot.Width * i / (float)Math.Max(1, _points.Count - 1)), plot.Bottom - (float)(Math.Max(0, selector(p)) / max) * plot.Height)).ToArray();
                using (Pen pen = new Pen(color, 2f)) g.DrawLines(pen, pts);
                using (Brush brush = new SolidBrush(color))
                    foreach (PointF pt in pts.Where((p, i) => i % Math.Max(1, _points.Count / 6) == 0))
                        g.FillEllipse(brush, pt.X - 3, pt.Y - 3, 6, 6);
            }
        }

        private sealed class PayDonutChart : Control
        {
            private decimal _receipts;
            private decimal _payments;
            public void SetData(decimal receipts, decimal payments) { _receipts = receipts; _payments = payments; Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                decimal total = Math.Max(1m, _receipts + _payments);
                Rectangle pie = new Rectangle(18, 48, Math.Min(118, Height - 70), Math.Min(118, Height - 70));
                float rSweep = (float)(_receipts * 360m / total);
                using (Pen green = new Pen(SaveGreen, 20f))
                using (Pen red = new Pen(PayRed, 20f))
                {
                    e.Graphics.DrawArc(green, pie, -90, rSweep);
                    e.Graphics.DrawArc(red, pie, -90 + rSweep, 360 - rSweep);
                }
                using (Font bold = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (Font small = new Font("Segoe UI", 7f))
                using (Brush text = new SolidBrush(PayText))
                using (Brush muted = new SolidBrush(PayMuted))
                {
                    e.Graphics.DrawString("Total", small, muted, pie.Left + 40, pie.Top + 38);
                    e.Graphics.DrawString(CompactCurrency(total), bold, text, pie.Left + 26, pie.Top + 54);
                    e.Graphics.DrawString("● Receipts  " + CompactCurrency(_receipts), small, new SolidBrush(SaveGreen), pie.Right + 16, pie.Top + 24);
                    e.Graphics.DrawString("● Payments  " + CompactCurrency(_payments), small, new SolidBrush(PayRed), pie.Right + 16, pie.Top + 54);
                }
            }
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("Payments", this, _btnNewPayment, _btnSavePayment, null);
        }

        private Panel BuildEntryActionBar(params Button[] buttons)
        {
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
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            foreach (var button in buttons)
            {
                if (button == _btnSavePayment)
                    button.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                button.Margin = new Padding(0, 0, 10, 0);
                flow.Controls.Add(button);
            }
            if (flow.Controls.Count > 0)
                flow.Controls[flow.Controls.Count - 1].Margin = new Padding(0);

            Label hint = new Label
            {
                Text = "Review invoice balance above. Record and clear actions stay pinned here.",
                Dock = DockStyle.Left,
                Width = 420,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.MiddleLeft
            };
            bar.Controls.Add(flow);
            bar.Controls.Add(hint);
            return bar;
        }

        private void BuildPaymentForm(Panel container)
        {
            container.Padding = new Padding(16, 58, 16, 16);

            Label hint = new Label
            {
                Text = "Select a client and unpaid invoice, then record the received amount.",
                Font = DS.Body,
                ForeColor = DS.Slate600,
                Location = new Point(16, 44),
                Width = 560,
                Height = 22
            };
            container.Controls.Add(hint);

            AddFieldLabel(container, "Client *", 16, 82);
            _cmbClient = new ComboBox { Location = new Point(16, 102), Width = 330, Height = 26, Font = DS.Body, DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _cmbClient.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cmbClient.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _cmbClient.AutoCompleteCustomSource = _clientEntrySource;
            _cmbClient.SelectedIndexChanged += CmbClient_Changed;
            _payClientHost = AddPaymentInputHost(container, _cmbClient);

            AddFieldLabel(container, "Invoice *", 370, 82);
            _cmbInvoice = new ComboBox { Location = new Point(370, 102), Width = 360, Height = 26, Font = DS.Body, DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _cmbInvoice.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cmbInvoice.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _cmbInvoice.AutoCompleteCustomSource = _invoiceSource;
            _cmbInvoice.SelectedIndexChanged += CmbInvoice_Changed;
            _payInvoiceHost = AddPaymentInputHost(container, _cmbInvoice);

            AddFieldLabel(container, "Amount Paid (INR) *", 16, 146);
            _numAmount = new NumericUpDown { Location = new Point(16, 166), Width = 170, Height = 26, Font = DS.Body, Minimum = 0, Maximum = 99999999, DecimalPlaces = 2 };
            _payAmountHost = AddPaymentInputHost(container, _numAmount);

            AddFieldLabel(container, "Payment Date *", 210, 146);
            _dtpPayDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = DS.Body, Location = new Point(210, 166), Width = 170, Height = 26, Value = DateTime.Today };
            _payDateHost = AddPaymentInputHost(container, _dtpPayDate);

            AddFieldLabel(container, "Payment Mode", 405, 146);
            _cmbMode = new ComboBox { Location = new Point(405, 166), Width = 180, Height = 26, Font = DS.Body, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMode.Items.AddRange(new object[] { "Bank Transfer", "NEFT/RTGS", "UPI", "Cash", "Cheque", "DD" });
            _cmbMode.SelectedIndex = 0;
            _payModeHost = AddPaymentInputHost(container, _cmbMode);

            AddFieldLabel(container, "Reference / UTR / Cheque No.", 16, 210);
            _txtRef = new TextBox { Location = new Point(16, 230), Width = 330, Height = 24, Font = DS.Body, BorderStyle = BorderStyle.FixedSingle };
            _payRefHost = AddPaymentInputHost(container, _txtRef);

            AddFieldLabel(container, "Notes", 370, 210);
            _txtNotes = new TextBox { Location = new Point(370, 230), Width = 360, Height = 58, Multiline = true, Font = DS.Body, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            _payNotesHost = AddPaymentInputHost(container, _txtNotes);
            container.Resize += (s, e) => ResizePaymentFields(container);
            ResizePaymentFields(container);
            UIHelper.ApplyInputStyles(container.Controls);
        }

        private void BuildHistoryCard(Panel container)
        {
            container.Padding = new Padding(16, 42, 16, 16);

            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White };
            Label lblFilter = new Label { Text = "Filter by client", AutoSize = true, Font = DS.BodyBold, ForeColor = DS.Slate700, Location = new Point(0, 8) };
            _cboClient = new ComboBox { Location = new Point(112, 4), Width = 280, Height = 26, Font = DS.Body, DropDownStyle = ComboBoxStyle.DropDown };
            _cboClient.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cboClient.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _cboClient.AutoCompleteCustomSource = _clientFilterSource;
            _cboClient.SelectedIndexChanged += CboClientFilter_Changed;
            filterBar.Controls.AddRange(new Control[] { lblFilter, _cboClient });

            _historyFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(0, 4, 4, 0)
            };
            _historyFlow.Resize += (s, e) => ResizeHistoryCards();

            container.Controls.Add(_historyFlow);
            container.Controls.Add(filterBar);
        }

        private void BuildQuickActionsCard(Panel container, Button btnClear)
        {
            container.Padding = new Padding(16, 42, 16, 16);

            TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.White };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Button paymentActions = MakeBtn("Open Payment Actions", InfoBlue, 180);
            ModernIconSystem.AddButtonIcon(paymentActions, ModernIconKind.ChevronDown);
            _btnSavePayment.Dock = DockStyle.Fill;
            paymentActions.Dock = DockStyle.Fill;
            _btnSavePayment.Margin = new Padding(0, 0, 0, 8);
            paymentActions.Margin = new Padding(0, 0, 0, 8);
            _btnSavePayment.Tag = ((_btnSavePayment.Tag == null ? string.Empty : _btnSavePayment.Tag + " ") + "FIXED_WIDTH").Trim();
            paymentActions.Tag = ((paymentActions.Tag == null ? string.Empty : paymentActions.Tag + " ") + "FIXED_WIDTH").Trim();
            paymentActions.Click += (s, e) => ShowPaymentActionsMenu(paymentActions, btnClear);
            Label hint = new Label
            {
                Text = "Clear, refresh, export, import, templates, and forms live here.",
                Dock = DockStyle.Fill,
                Font = DS.Small,
                ForeColor = DS.Slate500,
                TextAlign = ContentAlignment.TopLeft
            };
            actions.Controls.Add(_btnSavePayment, 0, 0);
            actions.Controls.Add(paymentActions, 0, 1);
            actions.Controls.Add(hint, 0, 2);
            container.Controls.Add(actions);
        }

        private void ShowPaymentActionsMenu(Control anchor, Button clearButton)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddPaymentAction(menu, "Clear Form", (s, e) => clearButton.PerformClick());
            AddPaymentAction(menu, "Refresh History", (s, e) => LoadPaymentHistory());
            AddPaymentAction(menu, "Export Payments", (s, e) => ExportPaymentsCsv());
            menu.Items.Add(new ToolStripSeparator());
            AddPaymentAction(menu, "Import Payments", (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Payments, FindForm()));
            AddPaymentAction(menu, "Download Template", (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Payments, FindForm()));
            AddPaymentAction(menu, "Open Payment Forms", (s, e) => FormTemplateWorkflowLauncher.Open(this, "Payments", "Finance / Payments", null, "payment receipt collections follow-up invoice approval credit note customer sign-off"));
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void AddPaymentAction(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            menu.Items.Add(item);
        }

        private void BuildInvoiceSummaryCard(Panel container)
        {
            container.Padding = new Padding(16, 44, 16, 16);
            _lblInvTotal = SummaryLine(container, "Invoice Total", IndiaFormatHelper.FormatCurrency(0), 48, DS.Slate900, false);
            _lblInvPaid = SummaryLine(container, "Already Paid", IndiaFormatHelper.FormatCurrency(0), 88, DS.Slate900, false);
            _lblInvBalance = SummaryLine(container, "Balance Due", IndiaFormatHelper.FormatCurrency(0), 134, OrangeCol, true);
            _lblInvStatus = SummaryLine(container, "Status", "-", 188, DS.Slate700, false);
        }

        private void BuildStatusCard(Panel container)
        {
            container.Padding = new Padding(16, 42, 16, 16);
            _lblStatus.Dock = DockStyle.Top;
            _lblStatus.Height = 44;
            _lblStatus.Text = "Payments will appear after the initial load.";
            container.Controls.Add(_lblStatus);

            Label note = new Label
            {
                Text = "Select invoice, verify balance, then save.",
                Dock = DockStyle.Top,
                Height = 58,
                Font = DS.Small,
                ForeColor = DS.Slate500
            };
            container.Controls.Add(note);
        }

        private Label SummaryLine(Panel parent, string label, string value, int y, Color valueColor, bool emphasize)
        {
            parent.Controls.Add(new Label { Text = label, Font = DS.Small, ForeColor = DS.Slate600, Location = new Point(16, y), Width = 120, Height = 18 });
            Label amount = new Label
            {
                Text = value,
                Font = emphasize ? new Font("Segoe UI", 13.5f, FontStyle.Bold) : DS.BodyBold,
                ForeColor = valueColor,
                Location = new Point(16, y + 18),
                Width = parent.Width - 32,
                Height = emphasize ? 28 : 22,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };
            parent.Controls.Add(amount);
            return amount;
        }

        private void NormalizePaymentEditorCards()
        {
            RemovePaymentEditorResizeGrips(this);
            NormalizePaymentActionButtons();
        }

        private void RemovePaymentEditorResizeGrips(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            foreach (Control control in root.Controls.Cast<Control>().ToList())
            {
                string metadata = ((control.Name ?? string.Empty) + " " + (control.Tag == null ? string.Empty : control.Tag.ToString())).ToUpperInvariant();
                if (metadata.Contains("PAYMENT_EDITOR_SECTION"))
                {
                    CardResizeGripService.Detach(control);
                    RemoveResizeGripChildren(control);
                }

                RemovePaymentEditorResizeGrips(control);
            }
        }

        private static void RemoveResizeGripChildren(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            foreach (Control child in root.Controls.Cast<Control>().ToList())
            {
                if (string.Equals(child.Name, CardResizeGripService.CornerGripName, StringComparison.Ordinal) ||
                    string.Equals(child.Name, CardResizeGripService.HeightGripName, StringComparison.Ordinal) ||
                    string.Equals(child.Name, CardResizeGripService.LockBadgeName, StringComparison.Ordinal))
                {
                    root.Controls.Remove(child);
                    child.Dispose();
                    continue;
                }

                RemoveResizeGripChildren(child);
            }
        }

        private void NormalizePaymentActionButtons()
        {
            if (_btnSavePayment != null && !_btnSavePayment.IsDisposed)
            {
                _btnSavePayment.BackColor = SaveGreen;
                _btnSavePayment.ForeColor = Color.White;
                _btnSavePayment.FlatStyle = FlatStyle.Flat;
                _btnSavePayment.FlatAppearance.BorderSize = 0;
                _btnSavePayment.TextAlign = ContentAlignment.MiddleCenter;
                _btnSavePayment.Height = 34;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DATA LOAD
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BindClientDropdowns(List<B2BClient> clients)
        {
            clients = clients ?? new List<B2BClient>();
            _cboClient.Items.Clear();
            _clientFilterSource.Clear();
            _cboClient.Items.Add(new ComboItem { Id = 0, Text = "(All Clients)" });
            _cmbClient.Items.Clear();
            _clientEntrySource.Clear();
            _cmbClient.Items.Add(new ComboItem { Id = 0, Text = "-- Select Client --" });
            foreach (B2BClient c in clients)
            {
                _cboClient.Items.Add(new ComboItem { Id = c.ClientID, Text = c.CompanyName });
                _cmbClient.Items.Add(new ComboItem { Id = c.ClientID, Text = c.CompanyName });
                _clientFilterSource.Add(c.CompanyName);
                _clientEntrySource.Add(c.CompanyName);
            }
            UIHelper.ShowEmptyClientsMessageIfNeeded(FindForm(), clients, "PaymentForm.BindClientDropdowns");
            _cboClient.SelectedIndex = 0;
            _cmbClient.SelectedIndex = 0;
        }

        private void LoadInvoiceDropdown(int clientId)
        {
            var sw = Stopwatch.StartNew();
            _cmbInvoice.Items.Clear();
            _invoiceSource.Clear();
            _cmbInvoice.Items.Add(new ComboItem { Id = 0, Text = "-- Select Invoice --" });
            if (clientId <= 0) { _cmbInvoice.SelectedIndex = 0; return; }
            try
            {
                List<Invoice> invoices = (_invoiceLookup ?? new List<Invoice>())
                    .Where(inv => inv.ClientID == clientId)
                    .ToList();
                if (invoices.Count > 0)
                {
                    PopulateInvoiceDropdown(invoices);
                    AppRuntime.LogTiming("Payments.LoadInvoiceDropdown", sw.ElapsedMilliseconds, "clientId=" + clientId + ";invoices=" + Math.Max(0, _cmbInvoice.Items.Count - 1) + ";source=cache");
                    return;
                }

                if (_invoiceDropdownLoading)
                    return;

                _invoiceDropdownLoading = true;
                ShowStatus("Loading client invoices...", Color.Gray);
                var worker = CreateWorker();
                worker.DoWork += (s, args) => args.Result = _invSvc.GetInvoicesForClient(clientId) ?? new List<Invoice>();
                worker.RunWorkerCompleted += (s, args) =>
                {
                    if (args.Error != null)
                    {
                        AppRuntime.LogException("PaymentForm.LoadInvoiceDropdown", args.Error);
                        RunOnUI(() =>
                        {
                            _invoiceDropdownLoading = false;
                            ShowStatus("Client invoices could not load. Refresh and try again.", PayRed);
                        });
                        ShowError( "Failed to load client invoices. Please try again.", args.Error);
                        return;
                    }
                    if (args.Cancelled) return;

                    RunOnUI(() =>
                    {
                        _invoiceDropdownLoading = false;
                        PopulateInvoiceDropdown(args.Result as List<Invoice>);
                        AppRuntime.LogTiming("Payments.LoadInvoiceDropdown", sw.ElapsedMilliseconds, "clientId=" + clientId + ";invoices=" + Math.Max(0, _cmbInvoice.Items.Count - 1) + ";source=db");
                    });
                };
                worker.RunWorkerAsync();
                return;
            }
            catch { }
            _cmbInvoice.SelectedIndex = 0;
            AppRuntime.LogTiming("Payments.LoadInvoiceDropdown", sw.ElapsedMilliseconds, "clientId=" + clientId + ";invoices=" + Math.Max(0, _cmbInvoice.Items.Count - 1));
        }

        private void PopulateInvoiceDropdown(IEnumerable<Invoice> invoices)
        {
            foreach (Invoice inv in invoices ?? Enumerable.Empty<Invoice>())
            {
                if (inv == null || inv.InvoiceID <= 0) continue;
                decimal balance = inv.BalanceDue > 0 ? inv.BalanceDue : inv.TotalAmount - inv.PaidAmount;
                if (string.Equals(inv.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) || balance <= 0.01m) continue;
                string text = inv.InvoiceNumber + " - " + IndiaFormatHelper.FormatCurrency(balance) + " due  [" + inv.PaymentStatus + "]";
                _cmbInvoice.Items.Add(new ComboItem { Id = inv.InvoiceID, Text = text });
                _invoiceSource.Add(text);
            }
            _cmbInvoice.SelectedIndex = _cmbInvoice.Items.Count > 1 ? 1 : 0;
        }

        private void LoadPaymentHistory()
        {
            var sw = Stopwatch.StartNew();
            ShowStatus("Refreshing payments...", Color.Gray);
            var worker = CreateWorker();
            worker.DoWork += (s, args) =>
            {
                args.Result = new PaymentRecordSnapshot
                {
                    Payments = _paySvc.GetAllPayments() ?? new List<Payment>(),
                    Invoices = _invSvc.GetAllInvoices() ?? new List<Invoice>()
                };
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                if (args.Error == null && !args.Cancelled)
                {
                    PaymentRecordSnapshot snapshot = args.Result as PaymentRecordSnapshot ?? new PaymentRecordSnapshot();
                    _allPayments = snapshot.Payments ?? new List<Payment>();
                    _invoiceLookup = snapshot.Invoices ?? new List<Invoice>();
                }
                ApplyClientFilter();
                AppRuntime.LogTiming("Payments.LoadPaymentHistory", sw.ElapsedMilliseconds, "payments=" + _allPayments.Count);
            };
            worker.RunWorkerAsync();
        }

        private void ApplyClientFilter()
        {
            var sw = Stopwatch.StartNew();
            ComboItem selected = _cboClient?.SelectedItem as ComboItem;
            int filterClientId = selected?.Id ?? 0;

            // Build invoiceâ†’client map when filtering by client
            Dictionary<int, int> invoiceClientMap = null;
            if (filterClientId > 0)
            {
                invoiceClientMap = _invoiceLookup.GroupBy(i => i.InvoiceID).ToDictionary(g => g.Key, g => g.First().ClientID);
            }

            _filteredPayments = new List<Payment>();
            foreach (Payment p in _allPayments)
            {
                // Apply client filter: check payment.ClientID first, fall back to invoice map
                if (filterClientId > 0)
                {
                    int payClientId = p.ClientID;
                    if (payClientId <= 0 && invoiceClientMap != null)
                        invoiceClientMap.TryGetValue(p.InvoiceID, out payClientId);
                    if (payClientId != filterClientId)
                        continue;
                }

                _filteredPayments.Add(p);
            }
            RenderPaymentBatch(true);
            ShowStatus(_filteredPayments.Count + " payments recorded.", Color.Gray);
            AppRuntime.LogTiming("Payments.ApplyClientFilter", sw.ElapsedMilliseconds, "payments=" + _filteredPayments.Count + ";clientId=" + filterClientId);
        }

        private void RenderPaymentBatch(bool reset)
        {
            UiPerformanceService.WithSuspendedDrawing(_historyFlow, () =>
            {
                if (reset)
                {
                    _renderedPayments = 0;
                    _historyFlow.Controls.Clear();
                    if (_filteredPayments.Count == 0)
                        _historyFlow.Controls.Add(BuildPaymentEmptyState());
                }

                int start = _renderedPayments;
                int end = Math.Min(start + PaymentBatchSize, _filteredPayments.Count);
                for (int i = start; i < end; i++)
                    _historyFlow.Controls.Add(MakePaymentCard(_filteredPayments[i]));
                _renderedPayments = end;

                if (_renderedPayments < _filteredPayments.Count)
                {
                    var btn = new Button
                    {
                        Text = "Load More",
                        Width = Math.Max(280, _historyFlow.ClientSize.Width - 30),
                        Height = 36,
                        BackColor = Color.FromArgb(241, 245, 249),
                        ForeColor = InfoBlue,
                        FlatStyle = FlatStyle.Flat,
                        Margin = new Padding(0, 0, 0, 10),
                        Cursor = Cursors.Hand
                    };
                    btn.FlatAppearance.BorderColor = DS.Border;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.Click += (s, e) =>
                    {
                        _historyFlow.Controls.Remove(btn);
                        BeginInvoke((Action)(() => RenderPaymentBatch(false)));
                    };
                    _historyFlow.Controls.Add(btn);
                }
            });
        }

        private Panel BuildPaymentEmptyState()
        {
            Panel empty = new Panel
            {
                Width = Math.Max(520, _historyFlow?.ClientSize.Width > 20 ? _historyFlow.ClientSize.Width - 12 : 760),
                Height = 230,
                BackColor = Color.White,
                Margin = new Padding(0, 12, 0, 0)
            };
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Payment, 82, DS.Indigo50, InfoBlue);
            icon.Location = new Point((empty.Width - icon.Width) / 2, 30);
            empty.Controls.Add(icon);
            empty.Controls.Add(new Label { Text = "No payment records found", Location = new Point(0, 126), Size = new Size(empty.Width, 28), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter });
            empty.Controls.Add(new Label { Text = "Payment history will appear here once recorded.", Location = new Point(0, 156), Size = new Size(empty.Width, 24), Font = DS.Body, ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter });
            empty.Resize += (s, e) =>
            {
                icon.Left = (empty.Width - icon.Width) / 2;
                foreach (Control child in empty.Controls.OfType<Label>())
                    child.Width = empty.Width;
            };
            return empty;
        }

        private Panel MakePaymentCard(Payment payment)
        {
            int cardWidth = Math.Max(520, _historyFlow != null ? _historyFlow.ClientSize.Width - 30 : 860);
            Panel card = new Panel
            {
                Width = cardWidth,
                Height = 82,
                BackColor = DS.Slate50,
                Margin = new Padding(0, 0, 0, 10)
            };
            DS.Rounded(card, 8);
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            card.Controls.Add(new Label { Text = payment.PaymentNumber ?? "-", Font = DS.BodyBold, ForeColor = DS.Slate950, Location = new Point(14, 12), Width = 170, Height = 20, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = payment.ClientName ?? "-", Font = DS.Small, ForeColor = DS.Slate700, Location = new Point(14, 38), Width = 230, Height = 20, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = payment.InvoiceNumber ?? "-", Font = DS.Small, ForeColor = InfoBlue, Location = new Point(260, 12), Width = 150, Height = 20, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = payment.PaymentMode ?? "-", Font = DS.Small, ForeColor = DS.Slate700, Location = new Point(260, 38), Width = 140, Height = 20, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = payment.PaymentDate.ToString("dd/MM/yyyy"), Font = DS.Small, ForeColor = DS.Slate500, Location = new Point(430, 12), Width = 120, Height = 20 });
            card.Controls.Add(new Label { Text = payment.ReferenceNumber ?? "-", Font = DS.Small, ForeColor = DS.Slate500, Location = new Point(430, 38), Width = Math.Max(120, cardWidth - 720), Height = 20, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            card.Controls.Add(new Label { Text = IndiaFormatHelper.FormatCurrency(payment.AmountPaid), Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = SaveGreen, Location = new Point(cardWidth - 248, 26), Width = 150, Height = 24, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right });
            Button delete = new Button
            {
                Text = "Delete",
                Location = new Point(cardWidth - 82, 25),
                Size = new Size(64, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.White,
                ForeColor = PayRed,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            delete.FlatAppearance.BorderColor = DS.Border;
            delete.FlatAppearance.BorderSize = 1;
            delete.Click += (s, e) => DeletePayment(payment);
            card.Controls.Add(delete);
            return card;
        }

        private void DeletePayment(Payment payment)
        {
            if (payment == null || payment.PaymentID <= 0)
                return;

            string label = string.IsNullOrWhiteSpace(payment.PaymentNumber) ? "payment #" + payment.PaymentID : payment.PaymentNumber;
            DialogResult confirm = RecordDeletionUi.ConfirmPermanentDelete(
                FindForm(),
                "Payment",
                label,
                "The linked invoice paid amount, balance, and payment status will be recalculated.");
            if (confirm != DialogResult.Yes)
                return;

            ShowStatus("Deleting payment...", Color.Gray);
            var worker = CreateWorker();
            worker.DoWork += (s, args) =>
            {
                _paySvc.DeletePayment(payment.PaymentID);
                args.Result = new PaymentRecordSnapshot
                {
                    Payments = _paySvc.GetAllPayments() ?? new List<Payment>(),
                    Invoices = _invSvc.GetAllInvoices() ?? new List<Invoice>()
                };
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                if (args.Error != null)
                {
                    ShowError("Payment could not be deleted. Refresh and try again.", args.Error);
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Payments"), "Deleting payment", args.Error);
                    ShowStatus("Delete failed. Refresh and try again.", PayRed);
                    return;
                }

                PaymentRecordSnapshot snapshot = args.Result as PaymentRecordSnapshot ?? new PaymentRecordSnapshot();
                _allPayments = snapshot.Payments ?? new List<Payment>();
                _invoiceLookup = snapshot.Invoices ?? new List<Invoice>();
                ApplyClientFilter();
                ComboItem selectedClient = ResolveComboItem(_cmbClient);
                LoadInvoiceDropdown(selectedClient?.Id ?? 0);
                ShowStatus("Payment deleted and invoice balance recalculated.", SaveGreen);
            };
            worker.RunWorkerAsync();
        }

        private void ResizeHistoryCards()
        {
            if (_historyFlow == null) return;
            int width = Math.Max(280, _historyFlow.ClientSize.Width - 30);
            foreach (Control control in _historyFlow.Controls)
                control.Width = width;
        }

        private void ResizePaymentFields(Control container)
        {
            if (container == null || container.Width <= 0) return;
            int maxFormWidth = 980;
            int usable = Math.Max(620, Math.Min(maxFormWidth, container.ClientSize.Width - 44));
            int gap = 22;
            int col = Math.Max(270, (usable - gap) / 2);
            int leftX = Math.Max(16, (container.ClientSize.Width - usable) / 2);
            int rightX = leftX + col + gap;
            int third = Math.Max(142, (usable - gap * 2) / 3);

            MoveLabel(container, "Client *", leftX, 82, col);
            SetPaymentInputBounds(_payClientHost, _cmbClient, leftX, 104, col, 38);
            MoveLabel(container, "Invoice *", rightX, 82, col);
            SetPaymentInputBounds(_payInvoiceHost, _cmbInvoice, rightX, 104, col, 38);

            MoveLabel(container, "Amount Paid (INR) *", leftX, 148, third);
            SetPaymentInputBounds(_payAmountHost, _numAmount, leftX, 170, third, 38);
            MoveLabel(container, "Payment Date *", leftX + third + gap, 148, third);
            SetPaymentInputBounds(_payDateHost, _dtpPayDate, leftX + third + gap, 170, third, 38);
            MoveLabel(container, "Payment Mode", leftX + (third + gap) * 2, 148, third);
            SetPaymentInputBounds(_payModeHost, _cmbMode, leftX + (third + gap) * 2, 170, third, 38);

            MoveLabel(container, "Reference / UTR / Cheque No.", leftX, 214, col);
            SetPaymentInputBounds(_payRefHost, _txtRef, leftX, 236, col, 38);
            MoveLabel(container, "Notes", rightX, 214, col);
            SetPaymentInputBounds(_payNotesHost, _txtNotes, rightX, 236, col, 64);
            container.Invalidate();
        }

        private Panel AddPaymentInputHost(Control parent, Control input)
        {
            Panel host = new Panel
            {
                BackColor = Color.White,
                Tag = "CUSTOM_INPUT_SHELL",
                Size = new Size(Math.Max(120, input.Width), input is TextBox && ((TextBox)input).Multiline ? 64 : 38)
            };
            host.Paint += PaintPaymentInputHost;
            input.Parent = host;
            input.BackColor = Color.White;
            if (input is TextBox)
                ((TextBox)input).BorderStyle = BorderStyle.None;
            if (input is ComboBox)
                ((ComboBox)input).FlatStyle = FlatStyle.Flat;
            if (input is NumericUpDown)
                ((NumericUpDown)input).BorderStyle = BorderStyle.None;

            input.GotFocus += (s, e) => host.Invalidate();
            input.LostFocus += (s, e) => host.Invalidate();
            parent.Controls.Add(host);
            return host;
        }

        private void SetPaymentInputBounds(Panel host, Control input, int x, int y, int width, int height)
        {
            if (host == null || input == null)
                return;

            host.SetBounds(x, y, width, height);
            int horizontalPad = input is DateTimePicker ? 8 : 12;
            int topPad = input is TextBox && ((TextBox)input).Multiline ? 8 : 7;
            int innerHeight = Math.Max(22, height - topPad - 6);
            input.SetBounds(horizontalPad, topPad, Math.Max(20, width - horizontalPad * 2), innerHeight);
            host.Invalidate();
        }

        private void PaintPaymentInputHost(object sender, PaintEventArgs e)
        {
            Panel host = sender as Panel;
            if (host == null)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, host.Width - 1, host.Height - 1);
            bool focused = host.Controls.Cast<Control>().Any(c => c.Focused || c.ContainsFocus);
            using (GraphicsPath path = DS.RoundedRect(rect, 7))
            using (SolidBrush brush = new SolidBrush(Color.White))
            using (Pen pen = new Pen(focused ? DS.FocusBlue : DS.InputBorder, focused ? 2f : 1f))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static void MoveLabel(Control parent, string text, int x, int y, int width)
        {
            Label label = parent.Controls.OfType<Label>().FirstOrDefault(l => string.Equals(l.Text, text, StringComparison.Ordinal));
            if (label != null)
                label.SetBounds(x, y, width, label.Height);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EVENTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void CboClientFilter_Changed(object sender, EventArgs e)
        {
            ApplyClientFilter();
        }

        private void CmbClient_Changed(object sender, EventArgs e)
        {
            ComboItem ci = ResolveComboItem(_cmbClient);
            if (ci != null && _cmbClient.SelectedItem == null)
                _cmbClient.SelectedItem = ci;
            LoadInvoiceDropdown(ci?.Id ?? 0);
            ResetInvoiceSummary();
        }

        private void CmbInvoice_Changed(object sender, EventArgs e)
        {
            ComboItem ci = ResolveComboItem(_cmbInvoice);
            if (ci != null && _cmbInvoice.SelectedItem == null)
                _cmbInvoice.SelectedItem = ci;
            if (ci == null || ci.Id == 0) { ResetInvoiceSummary(); return; }
            try
            {
                Invoice inv = _invoiceLookup.FirstOrDefault(i => i.InvoiceID == ci.Id) ?? _invSvc.GetInvoiceById(ci.Id);
                if (inv == null) return;
                decimal balance = inv.BalanceDue > 0 ? inv.BalanceDue : inv.TotalAmount - inv.PaidAmount;
                _lblInvTotal.Text   = IndiaFormatHelper.FormatCurrency(inv.TotalAmount);
                _lblInvPaid.Text    = IndiaFormatHelper.FormatCurrency(inv.PaidAmount);
                _lblInvBalance.Text = IndiaFormatHelper.FormatCurrency(balance);
                _lblInvStatus.Text  = inv.PaymentStatus;
                _numAmount.Maximum  = 99999999m;
                if (_numAmount.Value <= 0 || _numAmount.Value > _numAmount.Maximum)
                    _numAmount.Value = balance > 0 ? Math.Min(balance, _numAmount.Maximum) : 0;
            }
            catch { }
        }

        private void ResetInvoiceSummary()
        {
            _lblInvTotal.Text   = IndiaFormatHelper.FormatCurrency(0);
            _lblInvPaid.Text    = IndiaFormatHelper.FormatCurrency(0);
            _lblInvBalance.Text = IndiaFormatHelper.FormatCurrency(0);
            _lblInvStatus.Text  = "-";
            _numAmount.Value    = 0;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  RECORD PAYMENT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BtnRecord_Click(object sender, EventArgs e)
        {
            if (_recordPaymentInProgress)
                return;

            try
            {
                ComboItem cClient  = ResolveComboItem(_cmbClient);
                ComboItem cInvoice = ResolveComboItem(_cmbInvoice);
                if ((cInvoice == null || cInvoice.Id <= 0) && _cmbInvoice.Items.Count == 2)
                    cInvoice = _cmbInvoice.Items[1] as ComboItem;
                Invoice selectedInvoice = cInvoice != null && cInvoice.Id > 0
                    ? (_invoiceLookup.FirstOrDefault(i => i.InvoiceID == cInvoice.Id) ?? _invSvc.GetInvoiceById(cInvoice.Id))
                    : null;
                if ((cClient == null || cClient.Id <= 0) && selectedInvoice != null)
                    cClient = FindClientItem(selectedInvoice.ClientID);
                if (cClient == null || cClient.Id <= 0)
                    throw new Exception("Select a valid client before recording payment.");
                if (cInvoice == null || cInvoice.Id <= 0)
                    throw new Exception("Select an outstanding invoice before recording payment.");
                if (_numAmount.Value <= 0)
                    throw new Exception("Enter a payment amount greater than zero.");

                Payment pay = new Payment
                {
                    ClientID        = cClient?.Id  ?? 0,
                    InvoiceID       = cInvoice?.Id ?? 0,
                    AmountPaid      = _numAmount.Value,
                    PaymentDate     = _dtpPayDate.Value.Date,
                    PaymentMode     = _cmbMode.SelectedItem?.ToString() ?? "Bank Transfer",
                    ReferenceNumber = _txtRef.Text.Trim(),
                    Notes           = _txtNotes.Text.Trim()
                };

                _recordPaymentInProgress = true;
                if (_btnSavePayment != null)
                    _btnSavePayment.Enabled = false;
                ShowStatus("Recording payment...", Color.Gray);
                var worker = CreateWorker();
                worker.DoWork += (s, args) =>
                {
                    int id = _paySvc.RecordPayment(pay);
                    args.Result = new PaymentRecordSnapshot
                    {
                        PaymentId = id,
                        Payments = _paySvc.GetAllPayments() ?? new List<Payment>(),
                        Invoices = _invSvc.GetAllInvoices() ?? new List<Invoice>()
                    };
                };
                worker.RunWorkerCompleted += (s, args) =>
                {
                    if (args.Error != null)
                    {
                        RunOnUI(() =>
                        {
                            _recordPaymentInProgress = false;
                            if (_btnSavePayment != null)
                                _btnSavePayment.Enabled = true;
                            ShowStatus("Payment could not be recorded. Check the details and try again.", PayRed);
                        });
                        ShowError( "Payment could not be recorded. Check the details and try again.", args.Error);
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Payments"), "Recording payment", args.Error);
                        return;
                    }
                    if (args.Cancelled) return;

                    RunOnUI(() =>
                    {
                        _recordPaymentInProgress = false;
                        if (_btnSavePayment != null)
                            _btnSavePayment.Enabled = true;

                        PaymentRecordSnapshot snapshot = args.Result as PaymentRecordSnapshot;
                        if (snapshot != null)
                        {
                            _allPayments = snapshot.Payments ?? new List<Payment>();
                            _invoiceLookup = snapshot.Invoices ?? new List<Invoice>();
                            ClearForm();
                            ApplyClientFilter();
                            ShowStatus("Payment recorded: PAY #" + snapshot.PaymentId + ". Next: review the updated invoice balance or record the next receipt.", SaveGreen);
                        }
                    });
                };
                worker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                _recordPaymentInProgress = false;
                if (_btnSavePayment != null)
                    _btnSavePayment.Enabled = true;
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Payments"), "Recording payment", ex);
                ShowStatus("Payment could not be recorded. Check the details and try again.", PayRed);
            }
        }

        private void ClearForm()
        {
            _cmbClient.SelectedIndex = 0;
            _cmbInvoice.Items.Clear();
            _cmbInvoice.Items.Add(new ComboItem { Id = 0, Text = "-- Select Invoice --" });
            _cmbInvoice.SelectedIndex = 0;
            _numAmount.Value  = 0;
            _dtpPayDate.Value = DateTime.Today;
            _cmbMode.SelectedIndex = 0;
            _txtRef.Text   = "";
            _txtNotes.Text = "";
            ResetInvoiceSummary();
            ShowStatus("New payment entry ready. Select client, invoice, and amount to continue.", Color.Gray);
        }

        private void StartNewPayment()
        {
            ClearForm();
            if (_cmbClient != null)
                _cmbClient.Focus();
            ShowStatus("New payment form opened. Select client, invoice, payment mode, and save.", Color.Gray);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void ShowStatus(string msg, Color color) { _lblStatus.Text = msg; _lblStatus.ForeColor = color; }

        private ComboItem ResolveComboItem(ComboBox combo)
        {
            if (combo == null)
                return null;
            ComboItem selected = combo.SelectedItem as ComboItem;
            if (selected != null)
                return selected;
            string text = (combo.Text ?? string.Empty).Trim();
            if (text.Length == 0)
                return null;
            foreach (object item in combo.Items)
            {
                ComboItem candidate = item as ComboItem;
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.Text, text, StringComparison.OrdinalIgnoreCase) ||
                    candidate.Text.StartsWith(text, StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith(candidate.Text, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private ComboItem FindClientItem(int clientId)
        {
            foreach (object item in _cmbClient.Items)
            {
                ComboItem candidate = item as ComboItem;
                if (candidate != null && candidate.Id == clientId)
                    return candidate;
            }
            return null;
        }

        private GroupBox MakeGroup(string title) =>
            new ModernPaymentGroupBox { Text = title, Font = new Font("Segoe UI", 8.25f, FontStyle.Bold), ForeColor = InfoBlue, BackColor = DS.White, Padding = new Padding(10) };

        private Label InfoLabel(GroupBox parent, string text, int x, int y, Color? color = null, bool bold = false)
        {
            bool amountLabel = text.StartsWith("Invoice Total", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Already Paid", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Balance Due", StringComparison.OrdinalIgnoreCase);
            Label lbl = new Label { Text = text, AutoSize = true, Location = new Point(x, y), Font = amountLabel ? new Font("Segoe UI", 14, FontStyle.Bold) : new Font("Segoe UI", bold ? 10 : 9, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color ?? Color.FromArgb(50, 50, 50) };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private void AddLabel(GroupBox parent, string text, int x, int y) =>
            parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) });

        private sealed class ModernPaymentGroupBox : GroupBox
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = new Rectangle(0, 9, Width - 1, Height - 10);
                using (GraphicsPath path = DS.RoundedRect(rect, 10))
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);

                Size textSize = TextRenderer.MeasureText(Text ?? string.Empty, Font);
                Rectangle textRect = new Rectangle(14, 0, Math.Max(90, textSize.Width + 16), 20);
                using (Brush brush = new SolidBrush(BackColor))
                    e.Graphics.FillRectangle(brush, textRect);
                TextRenderer.DrawText(
                    e.Graphics,
                    Text ?? string.Empty,
                    Font,
                    new Rectangle(18, 0, Math.Max(70, textSize.Width + 4), 20),
                    ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private Panel MakeCard(string title)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16),
                Tag = "PAYMENT_EDITOR_SECTION"
            };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using (GraphicsPath path = DS.RoundedRect(rect, 8))
                using (SolidBrush brush = new SolidBrush(Color.White))
                using (Pen pen = new Pen(DS.InputBorder))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            };
            card.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = DS.Primary600,
                BackColor = Color.White,
                Location = new Point(46, 14),
                Width = 260,
                Height = 22
            });
            Label titleIcon = ModernIconSystem.Badge(ModernIconSystem.KindForTitle(title), 22, DS.Indigo50, DS.Primary600, 8);
            titleIcon.Location = new Point(16, 13);
            card.Controls.Add(titleIcon);
            return card;
        }

        private void AddFieldLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(x, y),
                Font = DS.SmallBold,
                ForeColor = DS.Slate700
            });
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            return MakeBtn(text, bg, width, Color.White, Color.Transparent);
        }

        private Button MakeBtn(string text, Color bg, int width, Color fg, Color border)
        {
            Button b = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = bg,
                ForeColor = fg,
                Font = DS.BodyBold,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            UIHelper.ApplyActionButton(b);
            return b;
        }

        private void ApplyPaymentActionHierarchy(Control.ControlCollection controls)
        {
            foreach (Control child in controls)
            {
                if (child is Button button)
                    UIHelper.ApplyActionButton(button);

                if (child.HasChildren)
                    ApplyPaymentActionHierarchy(child.Controls);
            }
        }

        private class ComboItem
        {
            public int    Id   { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text;
        }

        private sealed class PaymentRecordSnapshot
        {
            public int PaymentId { get; set; }
            public List<Payment> Payments { get; set; }
            public List<Invoice> Invoices { get; set; }
        }

        private sealed class PaymentOverviewSnapshot
        {
            public List<Payment> Payments { get; set; }
            public List<Invoice> Invoices { get; set; }
        }
    }
}




