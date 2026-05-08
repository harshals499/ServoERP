using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

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
        private const int PaymentBatchSize = 60;

        // â”€â”€ Colours â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color OrangeCol = DS.Amber500;

        public PaymentForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPermissions();
            EnableDeferredLoad(
                LoadInitialDataAsync,
                ex => { _lblStatus.Text = "Load error: " + ex.Message; _lblStatus.ForeColor = Color.Red; });
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
            Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = HeaderBg, Padding = new Padding(18, 10, 18, 8) };
            Label title = new Label { Text = "PAYMENT RECORDING", Font = DS.H1, ForeColor = DS.Slate950, Location = new Point(18, 12), Width = 360, Height = 26 };
            Label subtitle = new Label { Text = "Record, reconcile and review customer invoice payments.", Font = DS.Body, ForeColor = DS.Slate600, Location = new Point(19, 42), Width = 460, Height = 20 };
            FlowLayoutPanel headerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 470,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0),
                BackColor = Color.Transparent
            };

            _btnNewPayment = MakeBtn("+ New Payment", DS.Primary600, 130);
            Button btnRefresh = MakeBtn("Refresh", DS.White, 88, DS.Slate700, DS.BorderStrong);
            Button btnImport = MakeBtn("Import", DS.White, 88, DS.Slate700, DS.BorderStrong);
            Button btnTemplate = MakeBtn("Template", DS.White, 100, DS.Slate700, DS.BorderStrong);
            _btnNewPayment.Click += (s, e) => StartNewPayment();
            btnRefresh.Click += (s, e) => LoadPaymentHistory();
            btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Payments, FindForm());
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Payments, FindForm());
            headerActions.Controls.AddRange(new Control[] { _btnNewPayment, btnRefresh, btnImport, btnTemplate });
            header.Controls.AddRange(new Control[] { title, subtitle, headerActions });

            _lblStatus = new Label { AutoSize = false, Font = DS.Small, ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleLeft };
            _btnSavePayment = MakeBtn("Save Payment", SaveGreen, 150);
            Button btnClear = MakeBtn("Clear Form", DS.Slate600, 120);
            _btnSavePayment.Click += BtnRecord_Click;
            btnClear.Click += (s, e) => ClearForm();

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.BgPage,
                Padding = new Padding(18),
                ColumnCount = 2,
                RowCount = 1
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel left = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = DS.BgPage, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 12, 0) };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 350f));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel paymentCard = MakeCard("PAYMENT DETAILS");
            BuildPaymentForm(paymentCard);
            Panel historyCard = MakeCard("PAYMENT HISTORY");
            BuildHistoryCard(historyCard);
            left.Controls.Add(paymentCard, 0, 0);
            left.Controls.Add(historyCard, 0, 1);

            TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = DS.BgPage, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 170f));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 250f));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
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
            container.Padding = new Padding(16, 42, 16, 16);

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
            container.Controls.Add(_cmbClient);

            AddFieldLabel(container, "Invoice *", 370, 82);
            _cmbInvoice = new ComboBox { Location = new Point(370, 102), Width = 360, Height = 26, Font = DS.Body, DropDownStyle = ComboBoxStyle.DropDown, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _cmbInvoice.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cmbInvoice.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _cmbInvoice.AutoCompleteCustomSource = _invoiceSource;
            _cmbInvoice.SelectedIndexChanged += CmbInvoice_Changed;
            container.Controls.Add(_cmbInvoice);

            AddFieldLabel(container, "Amount Paid (INR) *", 16, 146);
            _numAmount = new NumericUpDown { Location = new Point(16, 166), Width = 170, Height = 26, Font = DS.Body, Minimum = 0, Maximum = 99999999, DecimalPlaces = 2 };
            container.Controls.Add(_numAmount);

            AddFieldLabel(container, "Payment Date *", 210, 146);
            _dtpPayDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = DS.Body, Location = new Point(210, 166), Width = 170, Height = 26, Value = DateTime.Today };
            container.Controls.Add(_dtpPayDate);

            AddFieldLabel(container, "Payment Mode", 405, 146);
            _cmbMode = new ComboBox { Location = new Point(405, 166), Width = 180, Height = 26, Font = DS.Body, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMode.Items.AddRange(new object[] { "Bank Transfer", "NEFT/RTGS", "UPI", "Cash", "Cheque", "DD" });
            _cmbMode.SelectedIndex = 0;
            container.Controls.Add(_cmbMode);

            AddFieldLabel(container, "Reference / UTR / Cheque No.", 16, 210);
            _txtRef = new TextBox { Location = new Point(16, 230), Width = 330, Height = 24, Font = DS.Body, BorderStyle = BorderStyle.FixedSingle };
            container.Controls.Add(_txtRef);

            AddFieldLabel(container, "Notes", 370, 210);
            _txtNotes = new TextBox { Location = new Point(370, 230), Width = 360, Height = 58, Multiline = true, Font = DS.Body, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            container.Controls.Add(_txtNotes);
            container.Resize += (s, e) => ResizePaymentFields(container);
            ResizePaymentFields(container);
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

            TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _btnSavePayment.Dock = DockStyle.Fill;
            btnClear.Dock = DockStyle.Fill;
            _btnSavePayment.Margin = new Padding(0, 0, 0, 8);
            btnClear.Margin = new Padding(0, 0, 0, 8);
            actions.Controls.Add(_btnSavePayment, 0, 0);
            actions.Controls.Add(btnClear, 0, 1);
            container.Controls.Add(actions);
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
                foreach (Invoice inv in _invSvc.GetInvoicesForClient(clientId))
                {
                    if (inv.PaymentStatus == "Paid") continue;
                    _cmbInvoice.Items.Add(new ComboItem { Id = inv.InvoiceID, Text = inv.InvoiceNumber + " - " + IndiaFormatHelper.FormatCurrency(inv.BalanceDue) + " due  [" + inv.PaymentStatus + "]" });
                }
            }
            catch { }
            _cmbInvoice.SelectedIndex = 0;
            AppRuntime.LogTiming("Payments.LoadInvoiceDropdown", sw.ElapsedMilliseconds, "clientId=" + clientId + ";invoices=" + Math.Max(0, _cmbInvoice.Items.Count - 1));
        }

        private async void LoadPaymentHistory()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                ShowStatus("Refreshing payments...", Color.Gray);
                _allPayments = await Task.Run(() => _paySvc.GetAllPayments());
                _invoiceLookup = await Task.Run(() => _invSvc.GetAllInvoices());
            }
            catch { }
            ApplyClientFilter();
            AppRuntime.LogTiming("Payments.LoadPaymentHistory", sw.ElapsedMilliseconds, "payments=" + _allPayments.Count);
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
            if (reset)
            {
                _renderedPayments = 0;
                _historyFlow.SuspendLayout();
                _historyFlow.Controls.Clear();
                _historyFlow.ResumeLayout();
            }

            int start = _renderedPayments;
            int end = Math.Min(start + PaymentBatchSize, _filteredPayments.Count);

            _historyFlow.SuspendLayout();
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
                btn.FlatAppearance.BorderColor = DS.Slate200;
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += (s, e) =>
                {
                    _historyFlow.Controls.Remove(btn);
                    BeginInvoke((Action)(() => RenderPaymentBatch(false)));
                };
                _historyFlow.Controls.Add(btn);
            }

            _historyFlow.ResumeLayout();
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
            card.Controls.Add(new Label { Text = payment.ReferenceNumber ?? "-", Font = DS.Small, ForeColor = DS.Slate500, Location = new Point(430, 38), Width = Math.Max(120, cardWidth - 640), Height = 20, AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            card.Controls.Add(new Label { Text = IndiaFormatHelper.FormatCurrency(payment.AmountPaid), Font = new Font("Segoe UI", 11.5f, FontStyle.Bold), ForeColor = SaveGreen, Location = new Point(cardWidth - 178, 26), Width = 150, Height = 24, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right });
            return card;
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
            int rightWidth = Math.Max(220, container.ClientSize.Width - 386);
            if (_cmbInvoice != null) _cmbInvoice.Width = rightWidth;
            if (_txtNotes != null) _txtNotes.Width = rightWidth;
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
            ComboItem ci = _cmbClient.SelectedItem as ComboItem;
            LoadInvoiceDropdown(ci?.Id ?? 0);
            ResetInvoiceSummary();
        }

        private void CmbInvoice_Changed(object sender, EventArgs e)
        {
            ComboItem ci = _cmbInvoice.SelectedItem as ComboItem;
            if (ci == null || ci.Id == 0) { ResetInvoiceSummary(); return; }
            try
            {
                Invoice inv = _invoiceLookup.FirstOrDefault(i => i.InvoiceID == ci.Id) ?? _invSvc.GetInvoiceById(ci.Id);
                if (inv == null) return;
                decimal balance = inv.TotalAmount - inv.PaidAmount;
                _lblInvTotal.Text   = IndiaFormatHelper.FormatCurrency(inv.TotalAmount);
                _lblInvPaid.Text    = IndiaFormatHelper.FormatCurrency(inv.PaidAmount);
                _lblInvBalance.Text = IndiaFormatHelper.FormatCurrency(balance);
                _lblInvStatus.Text  = inv.PaymentStatus;
                _numAmount.Maximum  = balance + 0.01m > 0 ? balance + 0.01m : 0;
                _numAmount.Value    = balance > 0 ? balance : 0;
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
            try
            {
                ComboItem cClient  = _cmbClient.SelectedItem  as ComboItem;
                ComboItem cInvoice = _cmbInvoice.SelectedItem as ComboItem;

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

                int id = _paySvc.RecordPayment(pay);
                ShowStatus("Payment recorded: PAY #" + id, SaveGreen);
                ClearForm();
                LoadPaymentHistory();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearForm()
        {
            _cmbClient.SelectedIndex = 0;
            _cmbInvoice.Items.Clear();
            _numAmount.Value  = 0;
            _dtpPayDate.Value = DateTime.Today;
            _cmbMode.SelectedIndex = 0;
            _txtRef.Text   = "";
            _txtNotes.Text = "";
            ResetInvoiceSummary();
            ShowStatus("New payment entry ready.", Color.Gray);
        }

        private void StartNewPayment()
        {
            ClearForm();
            if (_cmbClient != null)
                _cmbClient.Focus();
            ShowStatus("New payment form opened. Select client, invoice and save.", Color.Gray);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void ShowStatus(string msg, Color color) { _lblStatus.Text = msg; _lblStatus.ForeColor = color; }

        private GroupBox MakeGroup(string title) =>
            new GroupBox { Text = title, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = InfoBlue, BackColor = SectionBg, Padding = new Padding(8) };

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

        private Panel MakeCard(string title)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16)
            };
            DS.Rounded(card, 10);
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            card.Controls.Add(new Label
            {
                Text = title,
                Font = DS.SmallBold,
                ForeColor = DS.Primary600,
                Location = new Point(16, 14),
                Width = 220,
                Height = 18
            });
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
            b.FlatAppearance.BorderSize = border == Color.Transparent ? 0 : 1;
            b.FlatAppearance.BorderColor = border == Color.Transparent ? bg : border;
            DS.Rounded(b, 6);
            return b;
        }

        private class ComboItem
        {
            public int    Id   { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text;
        }
    }
}


