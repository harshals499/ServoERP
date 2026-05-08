using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private List<StockItem> _inventoryItems = new List<StockItem>();

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
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<InvoiceTemplate> _templates = new List<InvoiceTemplate>();
        private Button _btnNewInvoice;
        private Button _btnSaveInvoice;

        // â”€â”€ Colours â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private static readonly Color HeaderBg = DS.White;
        private static readonly Color SectionBg = DS.Slate50;
        private static readonly Color SaveGreen = DS.Teal600;
        private static readonly Color InfoBlue = DS.Primary600;
        private static readonly Color OrangeCol = DS.Amber500;

        public InvoiceForm()
        {
            this.Dock      = DockStyle.Fill;
            this.BackColor = DS.BgPage;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyInvoicePreviewSkin(Controls);
            ApplyPermissions();
            ClearForm();
            HandleCreated += (s, e) => QueueInitialLoad();
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
                    var invoices = _invSvc.GetAllInvoices()
                        .OrderByDescending(i => i.InvoiceDate)
                        .Take(120)
                        .ToList();

                    if (IsDisposed || !IsHandleCreated)
                        return;

                    dispatcher.Invoke((Action)(() =>
                    {
                        _inventoryItems = inventory ?? new List<StockItem>();
                        _clients = clients ?? _clients ?? new List<B2BClient>();
                        _templates = templates ?? new List<InvoiceTemplate>();
                        BindInventoryItems();
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
            if (!(_grid.Columns["Description"] is DataGridViewComboBoxColumn descColumn))
                return;

            descColumn.Items.Clear();
            foreach (var item in _inventoryItems.Select(i => i.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                descColumn.Items.Add(item);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LAYOUT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void BuildLayout()
        {
            Controls.Clear();
            BackColor = DS.BgPage;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = DS.BgPage, Padding = new Padding(24, 14, 24, 8) };
            header.Controls.Add(new Label { Text = "INVOICE MANAGEMENT", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = DS.Slate900, Location = new Point(24, 16), Size = new Size(420, 28) });
            header.Controls.Add(new Label { Text = "Create, manage and track customer invoices.", Font = new Font("Segoe UI", 9), ForeColor = DS.Slate600, Location = new Point(24, 48), Size = new Size(420, 20) });
            _btnNewInvoice = MakeBtn("New Invoice", InfoBlue, 126);
            _btnNewInvoice.MinimumSize = new Size(110, 0);
            Button btnPreview = MakeBtn("Preview", Color.White, 98); btnPreview.ForeColor = DS.Slate700; btnPreview.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnImport = MakeBtn("Import", Color.White, 104); btnImport.ForeColor = DS.Slate700; btnImport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnTemplate = MakeBtn("Template", Color.White, 112); btnTemplate.ForeColor = DS.Slate700; btnTemplate.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnMore = MakeBtn("⋮", Color.White, 42); btnMore.ForeColor = DS.Slate700; btnMore.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnNewInvoice.Anchor = btnPreview.Anchor = btnImport.Anchor = btnTemplate.Anchor = btnMore.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Resize += (s, e) =>
            {
                btnMore.Location = new Point(header.Width - 66, 22);
                btnTemplate.Location = new Point(btnMore.Left - 120, 22);
                btnImport.Location = new Point(btnTemplate.Left - 112, 22);
                _btnNewInvoice.Location = new Point(btnImport.Left - 134, 22);
            };
            _btnNewInvoice.Click += BtnNew_Click;
            btnPreview.Click += BtnPreview_Click;
            btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Invoices, FindForm());
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Invoices, FindForm());
            btnMore.Click += (s, e) => LoadInvoiceList();
            header.Controls.AddRange(new Control[] { _btnNewInvoice, btnImport, btnTemplate, btnMore });

            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(24, 0, 24, 14) };
            Panel workspace = new Panel { BackColor = DS.BgPage };
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = DS.BgPage,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMargin = new Size(0, 16), BackColor = DS.BgPage, Padding = new Padding(10, 0, 0, 0) };
            rightPanel.MouseEnter += (s, e) => rightPanel.Focus();
            rightPanel.Controls.Add(BuildInvoiceFooterCard());
            rightPanel.Controls.Add(BuildRecentActivityCard());
            rightPanel.Controls.Add(BuildInvoiceSummaryCard());
            rightPanel.Controls.Add(BuildQuickActionsCard());

            _documentHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(16, 16),
                BackColor = DS.BgPage,
                Padding = new Padding(0, 0, 0, 18),
                TabStop = true
            };
            _documentHost.MouseEnter += (s, e) => _documentHost.Focus();
            BuildInvoiceDocument(_documentHost);

            layout.Controls.Add(_documentHost, 0, 0);
            layout.Controls.Add(rightPanel, 1, 0);
            workspace.Controls.Add(layout);
            body.Controls.Add(workspace);
            body.Resize += (s, e) =>
            {
                int availableWidth = Math.Max(0, body.ClientSize.Width - body.Padding.Horizontal);
                int availableHeight = Math.Max(0, body.ClientSize.Height - body.Padding.Vertical);
                int targetWidth = Math.Min(availableWidth, 1340);
                workspace.SetBounds(body.Padding.Left, body.Padding.Top, targetWidth, availableHeight);
            };

            _invoiceFlow = new FlowLayoutPanel { Visible = false };
            Controls.Add(_invoiceFlow);
            Controls.Add(body);
            Controls.Add(header);
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
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
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

            _btnSaveInvoice = MakeBtn("Save Draft", Color.FromArgb(52, 152, 219), 110);
            Button btnFinalise = MakeBtn("Finalise", SaveGreen, 100);
            Button btnPayment = MakeBtn("Record Payment", Color.FromArgb(142, 68, 173), 130);
            Button btnPreview = MakeBtn("Preview", InfoBlue, 100);
            Button btnCompare = MakeBtn("Compare Format", Color.FromArgb(15, 118, 110), 125);

            _btnSaveInvoice.Margin = new Padding(0, 0, 10, 0);
            btnFinalise.Margin = new Padding(0, 0, 10, 0);
            btnPayment.Margin = new Padding(0, 0, 10, 0);
            btnPreview.Margin = new Padding(0, 0, 10, 0);
            btnCompare.Margin = new Padding(0);

            _btnSaveInvoice.Click += BtnSave_Click;
            btnFinalise.Click += BtnFinalise_Click;
            btnPayment.Click += BtnRecordPayment_Click;
            btnPreview.Click += BtnPreview_Click;
            btnCompare.Click += BtnCompare_Click;

            flow.Controls.AddRange(new Control[] { _btnSaveInvoice, btnFinalise, btnPayment, btnPreview, btnCompare });

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

        private void ApplyInvoicePreviewSkin(Control.ControlCollection controls)
        {
            foreach (Control child in controls.Cast<Control>().ToList())
            {
                if (child is TextBox || child is ComboBox || child is DateTimePicker || child is NumericUpDown)
                {
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
                using (Pen pen = new Pen(Color.FromArgb(203, 213, 225)))
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
                textBox.BorderStyle = BorderStyle.None;
            if (input is ComboBox combo)
            {
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.FlatStyle = FlatStyle.Flat;
                combo.DrawMode = DrawMode.OwnerDrawFixed;
                combo.DrawItem += PreviewCombo_DrawItem;
            }
            if (input is NumericUpDown numeric)
                numeric.BorderStyle = BorderStyle.None;

            host.Controls.Add(input);
            if (input is ComboBox comboDisplay)
            {
                Label textOverlay = new Label
                {
                    Text = comboDisplay.Text,
                    Location = new Point(8, 4),
                    Size = new Size(Math.Max(24, host.Width - 34), Math.Max(16, host.Height - 8)),
                    BackColor = Color.White,
                    ForeColor = DS.Slate900,
                    Font = new Font("Segoe UI", 8.5f),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Enabled = true
                };
                comboDisplay.SelectedIndexChanged += (s, e) => { textOverlay.Text = comboDisplay.Text; ClearComboSelection(comboDisplay); };
                comboDisplay.TextChanged += (s, e) => { textOverlay.Text = comboDisplay.Text; ClearComboSelection(comboDisplay); };
                comboDisplay.HandleCreated += (s, e) => comboDisplay.BeginInvoke((Action)(() => ClearComboSelection(comboDisplay)));
                textOverlay.Click += (s, e) => comboDisplay.DroppedDown = true;
                host.Controls.Add(textOverlay);
                textOverlay.BringToFront();
            }
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
            if (combo == null || combo.DropDownStyle == ComboBoxStyle.DropDownList)
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
                button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
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
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = InfoBlue,
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }
            return card;
        }

        private Panel BuildQuickActionsCard()
        {
            Panel card = CreateInvoiceCard("QUICK ACTIONS", 258);
            TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 42, 0, 8) };
            for (int i = 0; i < 2; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            _btnSaveInvoice = MakeSoftAction("Save Draft", InfoBlue);
            Button approve = MakeSoftAction("Send for Approval", SaveGreen);
            Button pdf = MakeSoftAction("Generate PDF", DS.Red600);
            Button email = MakeSoftAction("Email Invoice", DS.Primary600);
            Button receipt = MakeSoftAction("Convert to Receipt", SaveGreen);
            Button credit = MakeSoftAction("Create Credit Note", OrangeCol);
            _btnSaveInvoice.Click += BtnSave_Click;
            approve.Click += BtnFinalise_Click;
            pdf.Click += BtnPreview_Click;
            email.Click += (s, e) => MessageBox.Show("Email opens from the invoice preview toolbar. Generate PDF/Preview first, then choose Email.", "Email Invoice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            receipt.Click += BtnRecordPayment_Click;
            credit.Click += (s, e) => MessageBox.Show("Credit note workflow is not configured yet.", "Credit Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
            grid.Controls.Add(_btnSaveInvoice, 0, 0); grid.Controls.Add(approve, 1, 0);
            grid.Controls.Add(pdf, 0, 1); grid.Controls.Add(email, 1, 1);
            grid.Controls.Add(receipt, 0, 2); grid.Controls.Add(credit, 1, 2);
            card.Controls.Add(grid);
            foreach (Label label in card.Controls.OfType<Label>())
                label.BringToFront();
            return card;
        }

        private Button MakeSoftAction(string text, Color accent)
        {
            Button button = MakeBtn(text, DS.Lighten(accent, 0.82f), 120);
            button.Height = 62;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(5);
            button.ForeColor = accent;
            button.FlatAppearance.BorderColor = DS.Lighten(accent, 0.62f);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Font = new Font("Segoe UI", 8.2f, FontStyle.Bold);
            return button;
        }

        private Panel BuildInvoiceSummaryCard()
        {
            Panel card = CreateInvoiceCard("INVOICE SUMMARY", 270);
            card.AutoScroll = true;
            card.AutoScrollMargin = new Size(0, 10);
            card.MouseEnter += (s, e) => card.Focus();
            int y = 42;
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
            Panel line = new Panel { Location = new Point(16, y), Size = new Size(246, 1), BackColor = DS.Slate200 };
            card.Controls.Add(line);
        }

        private Label AddSummaryRow(Panel card, string label, string value, ref int y, Color color, bool bold = false)
        {
            card.Controls.Add(new Label { Text = label, Location = new Point(16, y), Size = new Size(130, 18), Font = new Font("Segoe UI", 8.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = DS.Slate700 });
            Label val = new Label { Text = value, Location = new Point(142, y), Size = new Size(120, 18), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", bold ? 10f : 8.5f, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color };
            card.Controls.Add(val);
            y += bold ? 28 : 24;
            return val;
        }

        private void AddDiscountRow(Panel card, ref int y)
        {
            card.Controls.Add(new Label { Text = "Discount", Location = new Point(16, y), Size = new Size(90, 18), Font = new Font("Segoe UI", 8.5f), ForeColor = DS.Slate700 });
            TextBox discount = new TextBox { Text = "0.00", Location = new Point(178, y - 3), Size = new Size(82, 24), Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Right };
            card.Controls.Add(discount);
            y += 28;
        }

        private Panel BuildRecentActivityCard()
        {
            Panel card = CreateInvoiceCard("RECENT ACTIVITY", 180);
            string[] rows =
            {
                "Invoice created\r\n05/05/2026 11:30 AM by Admin",
                "Draft saved\r\n05/05/2026 11:32 AM by Admin",
                "Client selected\r\n05/05/2026 11:35 AM by Admin"
            };
            int y = 42;
            foreach (string row in rows)
            {
                card.Controls.Add(new Label { Text = "●", Location = new Point(18, y + 2), Size = new Size(16, 18), ForeColor = SaveGreen });
                card.Controls.Add(new Label { Text = row, Location = new Point(40, y), Size = new Size(215, 34), Font = new Font("Segoe UI", 8), ForeColor = DS.Slate700 });
                y += 42;
            }
            return card;
        }

        private Panel BuildInvoiceFooterCard()
        {
            Panel card = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = DS.BgPage };
            _lblStatus = new Label { Text = "Last saved: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm tt"), Dock = DockStyle.Left, Width = 190, Font = new Font("Segoe UI", 8), ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleLeft };
            Button refresh = MakeBtn("↻ Refresh", DS.BgPage, 90);
            refresh.ForeColor = InfoBlue;
            refresh.FlatAppearance.BorderSize = 0;
            refresh.Dock = DockStyle.Right;
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
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
                {
                    e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
                    e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
                }
            };

            Label badge = new Label
            {
                Text = "GST SUMMARY",
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
            _cmbGstMode.SelectedIndexChanged += (s, e) => RecalculateSummary();

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
            int pad = 10;
            _documentPage = new Panel
            {
                Width = 900,
                Height = 842,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(18)
            };
            container.Controls.Add(_documentPage);
            container.Resize += (s, e) => CenterDocumentPage(container);
            CenterDocumentPage(container);

            // â”€â”€ Section: GST Summary â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            GroupBox grpGST = MakeGroup("TAX SUMMARY (GST)");
            grpGST.Dock = DockStyle.Top; grpGST.Height = 92;
            grpGST.Controls.Add(new Label
            {
                Text = "Live GST totals stay pinned below the page so Save Draft, Finalise, Record Payment, and Preview remain fully clickable.",
                Location = new Point(8, 28),
                Size = new Size(790, 34),
                Font = new Font("Segoe UI", 9),
                ForeColor = DS.Slate500
            });

            // â”€â”€ Section: Line Items â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            Panel grpLines = new Panel { Dock = DockStyle.Top, Height = 280, BackColor = SectionBg, Padding = new Padding(8) };
            Label lblLinesHdr = new Label { Text = "LINE ITEMS", Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = InfoBlue, BackColor = SectionBg, TextAlign = ContentAlignment.MiddleLeft };

            Panel lineBtns = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = SectionBg };
            Button btnAddLine = MakeBtn("+ Add Row", InfoBlue,  100);
            Button btnDelLine = MakeBtn("- Remove",  OrangeCol,  90);
            btnAddLine.Location = new Point(0, 2); btnDelLine.Location = new Point(108, 2);
            btnAddLine.Click += (s, e) => AddLineRow();
            btnDelLine.Click += (s, e) => RemoveLineRow();
            lineBtns.Controls.AddRange(new Control[] { btnAddLine, btnDelLine });

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 9), BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(245, 247, 250),
                RowHeadersVisible = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate700;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 242, 255);
            _grid.DefaultCellStyle.SelectionForeColor = DS.Slate900;
            _grid.DefaultCellStyle.BackColor = Color.White;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 255);
            _grid.DataError += Grid_DataError;
            var descColumn = new DataGridViewComboBoxColumn
            {
                Name = "Description",
                HeaderText = "Description",
                FillWeight = 34,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Standard
            };
            foreach (var item in _inventoryItems.Select(i => i.ItemName).Distinct().OrderBy(n => n))
                descColumn.Items.Add(item);
            _grid.Columns.Add(descColumn);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HSNCode",     HeaderText = "HSN Code",     FillWeight = 16 });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Unit", HeaderText = "Unit", FillWeight = 10, DataSource = new[] { "Nos", "No", "RM", "Set", "Kg", "Ltr" }, FlatStyle = FlatStyle.Standard });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity",    HeaderText = "Qty",          FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate",        HeaderText = "Rate (INR)",   FillWeight = 15, DefaultCellStyle = new DataGridViewCellStyle { Format = "₹#,##0.00", Alignment = DataGridViewContentAlignment.MiddleRight } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "GSTPercent",  HeaderText = "GST %",        FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "BillingType", HeaderText = "Type", FillWeight = 12, DataSource = new[] { "Billable", "Included" }, FlatStyle = FlatStyle.Standard });
            DataGridViewTextBoxColumn colAmt = new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount (INR)", FillWeight = 15 };
            colAmt.DefaultCellStyle.BackColor = Color.FromArgb(248, 252, 248);
            colAmt.DefaultCellStyle.Format = "₹#,##0.00";
            colAmt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns.Add(colAmt);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CoverageNote", HeaderText = "Coverage / Note", FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StockItemID", HeaderText = "StockItemID", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsStockItem", HeaderText = "IsStockItem", Visible = false });
            GridTheme.Apply(_grid);
            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.EditingControlShowing += Grid_EditingControlShowing;

            grpLines.Controls.Add(_grid);
            grpLines.Controls.Add(lineBtns);
            grpLines.Controls.Add(lblLinesHdr);

            // â”€â”€ Section: Header â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            GroupBox grpHdr = MakeGroup("INVOICE HEADER");
            grpHdr.Dock = DockStyle.None; grpHdr.Location = new Point(0, 0); grpHdr.Width = 872; grpHdr.Height = 770;
            Panel grpHdrHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 540,
                BackColor = Color.White,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 12),
                Padding = new Padding(0)
            };
            grpHdrHost.MouseEnter += (s, e) => grpHdrHost.Focus();
            grpHdrHost.Controls.Add(grpHdr);

            // Row 1: Invoice No (read-only) + Status
            AddLabel(grpHdr, "Invoice Number", 8, 14);
            _txtInvNo = new TextBox { Location = new Point(8, 30), Width = 180, ReadOnly = true, Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(240, 240, 240), BorderStyle = BorderStyle.FixedSingle };
            AddLabel(grpHdr, "Status", 200, 14);
            _cmbStatus = new ComboBox { Location = new Point(200, 30), Width = 130, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbStatus.Items.AddRange(new object[] { "Draft", "Pending", "Partial", "Paid", "Overdue" });
            grpHdr.Controls.AddRange(new Control[] { _txtInvNo, _cmbStatus });

            // Row 2: Invoice Date + Due Date
            AddLabel(grpHdr, "Invoice Date *", 8, 72);
            _dtpInvDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(8, 88), Width = 160, Height = 26, Value = DateTime.Today };
            AddLabel(grpHdr, "Due Date *", 180, 72);
            _dtpDueDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(180, 88), Width = 160, Height = 26, Value = DateTime.Today.AddDays(30) };
            grpHdr.Controls.AddRange(new Control[] { _dtpInvDate, _dtpDueDate });

            // Row 3: Client dropdown
            AddLabel(grpHdr, "Client *", 8, 128);
            _cmbClient = new ComboBox { Location = new Point(8, 144), Width = 360, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbClient.SelectedIndexChanged += CmbClient_Changed;
            grpHdr.Controls.Add(_cmbClient);

            // Row 4: Site dropdown
            AddLabel(grpHdr, "Site *", 8, 180);
            _cmbSite = new ComboBox { Location = new Point(8, 196), Width = 360, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            grpHdr.Controls.Add(_cmbSite);

            // Row 5: Contract dropdown (filtered by client)
            AddLabel(grpHdr, "Contract (optional)", 8, 228);
            _cmbContract = new ComboBox { Location = new Point(8, 244), Width = 360, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            grpHdr.Controls.Add(_cmbContract);

            AddLabel(grpHdr, "Use Template", 8, 278);
            _cmbTemplate = new ComboBox { Location = new Point(8, 294), Width = 230, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTemplate.SelectedIndexChanged += CmbTemplate_Changed;
            AddLabel(grpHdr, "Coverage", 250, 278);
            _cmbCoverageType = new ComboBox { Location = new Point(250, 294), Width = 180, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCoverageType.Items.AddRange(new object[] { "Billable Service", "Comprehensive AMC", "Non-Comprehensive AMC", "Warranty" });
            _cmbCoverageType.SelectedIndex = 0;
            AddLabel(grpHdr, "Warranty", 442, 278);
            _cmbWarrantyStatus = new ComboBox { Location = new Point(442, 294), Width = 160, Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbWarrantyStatus.Items.AddRange(new object[] { "Out of Warranty", "Under Warranty", "Under Contract" });
            _cmbWarrantyStatus.SelectedIndex = 0;
            AddLabel(grpHdr, "Warranty Expiry", 614, 278);
            _dtpWarrantyExpiry = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(614, 294), Width = 174, Value = DateTime.Today.AddYears(1), ShowCheckBox = true };
            grpHdr.Controls.AddRange(new Control[] { _cmbTemplate, _cmbCoverageType, _cmbWarrantyStatus, _dtpWarrantyExpiry });

            AddLabel(grpHdr, "Subject", 8, 334);
            _txtSubject = new TextBox { Location = new Point(8, 350), Width = 780, Height = 42, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            grpHdr.Controls.Add(_txtSubject);

            AddLabel(grpHdr, "Payment Terms", 8, 404);
            _txtPaymentTerms = new TextBox { Location = new Point(8, 420), Width = 180, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Text = "30 Days" };
            AddLabel(grpHdr, "Place of Supply", 200, 404);
            _txtPlaceOfSupply = new TextBox { Location = new Point(200, 420), Width = 180, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Text = "Maharashtra" };
            _txtPlaceOfSupply.TextChanged += (s, e) => AutoSelectGstModeFromPlaceOfSupply();
            AddLabel(grpHdr, "Next Service Due", 392, 404);
            _dtpNextServiceDue = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(392, 420), Width = 160, Value = DateTime.Today.AddMonths(3), ShowCheckBox = true };
            AddLabel(grpHdr, "Workflow Notes", 564, 404);
            _txtInventorySummary = new TextBox { Location = new Point(564, 420), Width = 224, Height = 56, Multiline = true, ReadOnly = true, Font = new Font("Segoe UI", 8.5f), BackColor = Color.FromArgb(248, 250, 252), BorderStyle = BorderStyle.FixedSingle };
            grpHdr.Controls.AddRange(new Control[] { _txtPaymentTerms, _txtPlaceOfSupply, _dtpNextServiceDue, _txtInventorySummary });

            AddLabel(grpHdr, "PO Number", 8, 490);
            _txtPONumber = new TextBox { Location = new Point(8, 506), Width = 220, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            AddLabel(grpHdr, "PO Date", 240, 490);
            _dtpPODate = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 9), Location = new Point(240, 506), Width = 140, Value = DateTime.Today };
            grpHdr.Controls.AddRange(new Control[] { _txtPONumber, _dtpPODate });

            AddLabel(grpHdr, "Send Invoice To", 8, 548);
            _txtSendInvoiceTo = new TextBox { Location = new Point(8, 564), Width = 380, Height = 84, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            grpHdr.Controls.Add(_txtSendInvoiceTo);

            AddLabel(grpHdr, "Notes", 408, 490);
            _txtNotes = new TextBox { Location = new Point(408, 506), Width = 380, Height = 142, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            grpHdr.Controls.Add(_txtNotes);

            AddLabel(grpHdr, "Smart Assistant", 8, 664);
            _txtNudges = new TextBox { Location = new Point(8, 680), Width = 780, Height = 64, Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(240, 253, 250), ForeColor = Color.FromArgb(15, 118, 110), Font = new Font("Segoe UI", 9, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle };
            grpHdr.Controls.Add(_txtNudges);

            GroupBox grpWorkflow = MakeGroup("HVAC WORKFLOW");
            grpWorkflow.Dock = DockStyle.Top; grpWorkflow.Height = 220;
            AddLabel(grpWorkflow, "Checklist / Tasks", 8, 20);
            _txtChecklist = new TextBox { Location = new Point(8, 36), Width = 252, Height = 156, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            AddLabel(grpWorkflow, "Asset / Equipment", 274, 20);
            _txtAssetDetails = new TextBox { Location = new Point(274, 36), Width = 252, Height = 156, Multiline = true, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            AddLabel(grpWorkflow, "Payment History", 540, 20);
            _txtPaymentHistory = new TextBox { Location = new Point(540, 36), Width = 248, Height = 156, Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(248, 250, 252), Font = new Font("Segoe UI", 8.5f), BorderStyle = BorderStyle.FixedSingle };
            grpWorkflow.Controls.AddRange(new Control[] { _txtChecklist, _txtAssetDetails, _txtPaymentHistory });

            Panel spacer = new Panel { Dock = DockStyle.Top, Height = pad, BackColor = DS.BgPage };

            // Stack (DockStyle.Top â€” add in reverse visual order)
            _documentPage.Controls.Add(grpGST);
            _documentPage.Controls.Add(grpLines);
            _documentPage.Controls.Add(grpWorkflow);
            _documentPage.Controls.Add(grpHdrHost);
            _documentPage.Controls.Add(spacer);
            ApplyInvoicePreviewLayout(grpHdrHost, grpHdr, grpWorkflow, grpGST, grpLines, spacer);
            container.AutoScrollMinSize = new Size(_documentPage.Width + 48, _documentPage.Height + 48);
        }

        private void ApplyInvoicePreviewLayout(Panel grpHdrHost, GroupBox grpHdr, GroupBox grpWorkflow, GroupBox grpGST, Panel grpLines, Panel spacer)
        {
            EnsureTaxControls();
            spacer.Visible = false;
            grpLines.Visible = false;
            grpLines.Height = 0;

            grpHdrHost.Dock = DockStyle.None;
            grpHdrHost.SetBounds(14, 12, 872, 520);
            grpHdrHost.BackColor = Color.White;
            grpHdrHost.AutoScroll = true;
            grpHdr.Dock = DockStyle.None;
            grpHdr.SetBounds(0, 0, 854, 558);
            grpHdr.BackColor = Color.White;
            grpHdr.ForeColor = InfoBlue;

            SetLabelPosition(grpHdr, "Invoice Number", 16, 24, 150);
            SetControlBounds(_txtInvNo, 16, 42, 220, 26);
            _txtInvNo.BackColor = Color.White;
            SetLabelPosition(grpHdr, "Status", 258, 24, 150);
            SetControlBounds(_cmbStatus, 258, 42, 150, 26);
            SetLabelPosition(grpHdr, "Invoice Date *", 520, 24, 150);
            SetControlBounds(_dtpInvDate, 520, 42, 160, 26);
            SetLabelPosition(grpHdr, "Due Date *", 714, 24, 150);
            SetControlBounds(_dtpDueDate, 714, 42, 140, 26);

            SetLabelPosition(grpHdr, "Client *", 16, 88, 150);
            SetControlBounds(_cmbClient, 16, 106, 300, 26);
            SetLabelPosition(grpHdr, "Site *", 332, 88, 150);
            SetControlBounds(_cmbSite, 332, 106, 280, 26);
            SetLabelPosition(grpHdr, "Contract (optional)", 628, 88, 160);
            SetControlBounds(_cmbContract, 628, 106, 226, 26);

            SetLabelPosition(grpHdr, "Use Template", 16, 152, 150);
            SetControlBounds(_cmbTemplate, 16, 170, 220, 26);
            SetLabelPosition(grpHdr, "Coverage", 258, 152, 150);
            SetControlBounds(_cmbCoverageType, 258, 170, 204, 26);
            SetLabelPosition(grpHdr, "Warranty", 482, 152, 150);
            SetControlBounds(_cmbWarrantyStatus, 482, 170, 170, 26);
            SetLabelPosition(grpHdr, "Warranty Expiry", 672, 152, 150);
            SetControlBounds(_dtpWarrantyExpiry, 672, 170, 182, 26);

            SetLabelPosition(grpHdr, "Subject", 16, 216, 150);
            SetControlBounds(_txtSubject, 16, 234, 838, 26);
            _txtSubject.Multiline = false;

            SetLabelPosition(grpHdr, "Payment Terms", 16, 280, 150);
            SetControlBounds(_txtPaymentTerms, 16, 298, 220, 26);
            SetLabelPosition(grpHdr, "Place of Supply", 258, 280, 150);
            SetControlBounds(_txtPlaceOfSupply, 258, 298, 204, 26);
            SetLabelPosition(grpHdr, "Next Service Due", 482, 280, 150);
            SetControlBounds(_dtpNextServiceDue, 482, 298, 170, 26);
            SetLabelPosition(grpHdr, "Workflow Notes", 672, 280, 150);
            SetControlBounds(_txtInventorySummary, 672, 298, 182, 38);
            _txtInventorySummary.ReadOnly = false;

            SetLabelPosition(grpHdr, "PO Number", 16, 344, 150);
            SetControlBounds(_txtPONumber, 16, 362, 220, 26);
            SetLabelPosition(grpHdr, "PO Date", 258, 344, 150);
            SetControlBounds(_dtpPODate, 258, 362, 190, 26);

            SetLabelPosition(grpHdr, "Send Invoice To", 16, 408, 150);
            SetControlBounds(_txtSendInvoiceTo, 16, 426, 440, 58);
            SetLabelPosition(grpHdr, "Notes", 482, 344, 150);
            SetControlBounds(_txtNotes, 482, 362, 372, 122);

            SetLabelPosition(grpHdr, "Smart Assistant", 16, 490, 150);
            SetControlBounds(_txtNudges, 16, 508, 838, 26);
            _txtNudges.BackColor = Color.FromArgb(239, 246, 255);
            _txtNudges.ForeColor = Color.FromArgb(30, 64, 175);
            _txtNudges.Multiline = false;

            grpWorkflow.Dock = DockStyle.None;
            grpWorkflow.SetBounds(14, 546, 872, 104);
            grpWorkflow.BackColor = Color.White;
            SetLabelPosition(grpWorkflow, "Checklist / Tasks", 16, 22, 160);
            SetControlBounds(_txtChecklist, 16, 40, 260, 54);
            SetLabelPosition(grpWorkflow, "Asset / Equipment", 302, 22, 160);
            SetControlBounds(_txtAssetDetails, 302, 40, 260, 54);
            SetLabelPosition(grpWorkflow, "Payment History", 588, 22, 160);
            SetControlBounds(_txtPaymentHistory, 588, 40, 266, 54);

            grpGST.Dock = DockStyle.None;
            grpGST.SetBounds(14, 664, 872, 150);
            grpGST.BackColor = Color.White;
            grpGST.Controls.Clear();
            _lblSubTotal = AddGstMetric(grpGST, "Sub Total", 16, 32, DS.Slate900);
            _lblGSTAmt = AddGstMetric(grpGST, "GST (18%)", 140, 32, DS.Slate900);
            _lblCGSTAmt = AddGstMetric(grpGST, "CGST", 264, 32, DS.Slate900);
            _lblSGSTAmt = AddGstMetric(grpGST, "SGST", 388, 32, DS.Slate900);
            _lblIGSTAmt = AddGstMetric(grpGST, "IGST", 512, 32, DS.Slate900);
            _lblRoundOffAmt = AddGstMetric(grpGST, "Round Off", 636, 32, DS.Slate900);
            _lblTotal = AddGstMetric(grpGST, "Grand Total", 760, 32, InfoBlue);
            _lblBalance = AddGstMetric(grpGST, "Balance Due", 760, 84, OrangeCol);

            AddLabel(grpGST, "GST Mode", 18, 104);
            SetControlBounds(_cmbGstMode, 88, 100, 116, 28);
            grpGST.Controls.Add(_cmbGstMode);
            AddLabel(grpGST, "Default GST %", 240, 104);
            SetControlBounds(_numGST, 338, 100, 96, 28);
            grpGST.Controls.Add(_numGST);
            AddLabel(grpGST, "Round Off", 470, 104);
            SetControlBounds(_numRoundOff, 548, 100, 112, 28);
            grpGST.Controls.Add(_numRoundOff);
        }

        private void EnsureTaxControls()
        {
            if (_cmbGstMode == null)
            {
                _cmbGstMode = new ComboBox { Font = new Font("Segoe UI", 8.5f), DropDownStyle = ComboBoxStyle.DropDownList };
                _cmbGstMode.Items.AddRange(new object[] { "IGST", "CGST+SGST" });
                _cmbGstMode.SelectedIndex = 1;
                _cmbGstMode.SelectedIndexChanged += (s, e) => RecalculateSummary();
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

        private Label AddGstMetric(Control parent, string title, int x, int y, Color color)
        {
            Panel box = new Panel { Location = new Point(x, y), Size = new Size(112, 46), BackColor = Color.FromArgb(248, 250, 252) };
            box.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
            };
            box.Controls.Add(new Label { Text = title, Location = new Point(8, 5), Size = new Size(98, 15), Font = new Font("Segoe UI", 7.5f), ForeColor = DS.Slate600 });
            Label value = new Label { Text = "₹0.00", Location = new Point(8, 21), Size = new Size(98, 19), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = color };
            box.Controls.Add(value);
            parent.Controls.Add(box);
            return value;
        }

        private void SetLabelPosition(Control parent, string text, int x, int y, int width)
        {
            foreach (Label label in parent.Controls.OfType<Label>())
            {
                if (string.Equals(label.Text, text, StringComparison.OrdinalIgnoreCase))
                {
                    label.Location = new Point(x, y);
                    label.Size = new Size(width, 16);
                    label.ForeColor = DS.Slate700;
                    return;
                }
            }
        }

        private void SetControlBounds(Control control, int x, int y, int width, int height)
        {
            if (control == null)
                return;
            control.SetBounds(x, y, width, height);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DATA LOAD
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void LoadClientDropdowns()
        {
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

            _cmbTemplate.Items.Clear();
            _cmbTemplate.Items.Add(new ComboItem { Id = 0, Text = "-- Select Template --" });
            foreach (InvoiceTemplate template in _templates.OrderBy(t => t.TemplateName))
                _cmbTemplate.Items.Add(new ComboItem { Id = template.TemplateID, Text = template.TemplateName, Tag = template.TemplateCode });
            _cmbTemplate.SelectedIndex = 0;
        }

        private void LoadContractDropdowns(int clientId)
        {
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
            if (_grid.CurrentCell == null || _grid.Columns[_grid.CurrentCell.ColumnIndex].Name != "Description")
                return;

            if (e.Control is ComboBox combo)
            {
                UIHelper.ApplyInputStyle(combo);
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
        }

        private void LoadSiteDropdowns(int clientId)
        {
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem { Id = 0, Text = "-- Select Site --" });
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
            try
            {
                var invoices = _invSvc.GetAllInvoices()
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(120)
                    .ToList();
                BindInvoiceList(invoices);
            }
            catch (Exception ex)
            {
                ShowStatus("Error: " + ex.Message, Color.Red);
            }
        }

        private void BindInvoiceList(IEnumerable<Invoice> invoices)
        {
            _invoiceFlow.SuspendLayout();
            _invoiceFlow.Controls.Clear();
            DateTime today = DateTime.Today;
            List<Invoice> invoiceList = (invoices ?? Enumerable.Empty<Invoice>()).ToList();
            try
            {
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
                _invoiceFlow.ResumeLayout(true);
                if (_current == null)
                {
                    if (invoiceList.Count > 0)
                        PopulateForm(_invSvc.GetInvoiceById(invoiceList[0].InvoiceID) ?? invoiceList[0]);
                    else
                        ClearForm();
                }
                ShowStatus(_invoiceFlow.Controls.Count + " invoices.", Color.Gray);
            }
            catch (Exception ex)
            {
                _invoiceFlow.ResumeLayout(true);
                ShowStatus("Error: " + ex.Message, Color.Red);
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
            _txtInventorySummary.Text = _invSvc.GetInventorySummary(inv);
            _txtNudges.Text = _invSvc.GetBehavioralNudges(inv);

            _updating = false;
            RecalculateSummary();
            ShowStatus("Loaded: " + inv.InvoiceNumber, InfoBlue);
        }

        private Invoice CollectForm()
        {
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
            inv.ServiceChecklist = _txtChecklist.Text.Trim();
            inv.AssetDetails = _txtAssetDetails.Text.Trim();

            ComboItem ci = (ComboItem)_cmbClient.SelectedItem;
            inv.ClientID = ci?.Id ?? 0;

            ComboItem cs = (ComboItem)_cmbSite.SelectedItem;
            inv.SiteID = cs?.Id ?? 0;

            ComboItem cc = (ComboItem)_cmbContract.SelectedItem;
            inv.ContractID = cc?.Id ?? 0;

            inv.GSTPercent = _numGST.Value;
            inv.LineItems  = CollectLineItems();
            return inv;
        }

        private List<InvoiceLineItem> CollectLineItems()
        {
            var list = new List<InvoiceLineItem>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                string desc = row.Cells["Description"].Value?.ToString().Trim() ?? "";
                if (string.IsNullOrEmpty(desc)) continue;
                string hsn  = row.Cells["HSNCode"].Value?.ToString().Trim() ?? "";
                string unit = row.Cells["Unit"].Value?.ToString().Trim() ?? "Nos";
                decimal qty  = TryParseDecimal(row.Cells["Quantity"].Value);
                decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
                decimal gst = TryParseDecimal(row.Cells["GSTPercent"].Value);
                bool isBillable = !string.Equals(row.Cells["BillingType"].Value?.ToString(), "Included", StringComparison.OrdinalIgnoreCase);
                list.Add(new InvoiceLineItem
                {
                    Description = desc,
                    HSNCode     = hsn,
                    Unit        = unit,
                    Quantity    = qty > 0 ? qty : 1,
                    Rate        = rate,
                    GSTPercent  = gst > 0 ? gst : _numGST.Value,
                    IsBillable  = isBillable,
                    CoverageNote = row.Cells["CoverageNote"].Value?.ToString().Trim(),
                    StockItemID = TryParseInt(row.Cells["StockItemID"].Value),
                    IsStockItem = string.Equals(row.Cells["IsStockItem"].Value?.ToString(), "1", StringComparison.OrdinalIgnoreCase),
                    Amount      = isBillable ? Math.Round((qty > 0 ? qty : 1m) * rate, 2) : 0m
                });
            }
            if (list.Count == 0)
                list.Add(new InvoiceLineItem
                {
                    Description = "HVAC installation service - May 2026",
                    HSNCode = "9987",
                    Unit = "Job",
                    Quantity = 1m,
                    Rate = 105805.08m,
                    GSTPercent = _numGST?.Value > 0 ? _numGST.Value : 18m,
                    IsBillable = true,
                    Amount = 105805.08m
                });
            return list;
        }

        private void ClearForm()
        {
            _updating = true;
            _current = null;
            _txtInvNo.Text = "INV-2026-05-0001";
            _dtpInvDate.Value = new DateTime(2026, 5, 5);
            _dtpDueDate.Value = new DateTime(2026, 6, 4);
            _txtNotes.Text = "Thank you for your business." + Environment.NewLine + Environment.NewLine + "Please call us for any queries.";
            _txtSubject.Text = "Invoice for HVAC Installation - May 2026";
            _txtPONumber.Text = string.Empty;
            _dtpPODate.Value = new DateTime(2026, 5, 5);
            _txtSendInvoiceTo.Text = "Accounts Payable" + Environment.NewLine + "ABC Cooling Solutions Pvt. Ltd." + Environment.NewLine + "accounts@abccooling.com";
            _txtPaymentTerms.Text = "30 Days";
            _txtPlaceOfSupply.Text = "Maharashtra";
            _txtChecklist.Text = "☑  Site Survey Completed" + Environment.NewLine + "☑  Material Delivered" + Environment.NewLine + "☑  Installation Completed" + Environment.NewLine + "☑  Testing & Commissioning Done";
            _txtAssetDetails.Text = "•  Daikin VRV IV System" + Environment.NewLine + "•  4 x Indoor Units" + Environment.NewLine + "•  1 x Outdoor Unit" + Environment.NewLine + "•  Control Panel";
            _txtPaymentHistory.Text = "Last Payment                                      ₹0.00" + Environment.NewLine + "Total Invoices                                          3" + Environment.NewLine + "Outstanding                                      ₹0.00" + Environment.NewLine + "Credit Limit                              ₹5,00,000.00";
            _txtInventorySummary.Text = "Installation completed successfully.";
            EnsurePreviewComboItem(_cmbClient, "ABC Cooling Solutions Pvt. Ltd.", true);
            EnsurePreviewComboItem(_cmbSite, "Main Site - Pune", true);
            EnsurePreviewComboItem(_cmbContract, "Annual HVAC Maintenance Contract", true);
            EnsurePreviewComboItem(_cmbTemplate, "Select Template", false);
            EnsurePreviewComboItem(_cmbCoverageType, "Billable Service", false);
            EnsurePreviewComboItem(_cmbWarrantyStatus, "Out of Warranty", false);
            if (_cmbTemplate.Items.Count > 0) _cmbTemplate.SelectedIndex = 0;
            if (_cmbCoverageType.Items.Count > 0) _cmbCoverageType.SelectedIndex = 0;
            if (_cmbWarrantyStatus.Items.Count > 0) _cmbWarrantyStatus.SelectedIndex = 0;
            AutoSelectGstModeFromPlaceOfSupply();
            _dtpWarrantyExpiry.Checked = false;
            _dtpNextServiceDue.Checked = false;
            _numRoundOff.Value = 0;
            if (_cmbStatus.Items.Count > 0) _cmbStatus.SelectedIndex = 0;   // Draft
            if (_cmbClient.Items.Count > 0 && _cmbClient.SelectedIndex < 0) _cmbClient.SelectedIndex = 0;
            EnsurePreviewComboItem(_cmbSite, "Main Site - Pune", true);
            EnsurePreviewComboItem(_cmbContract, "Annual HVAC Maintenance Contract", true);
            _numGST.Value = 18m;
            _grid.Rows.Clear();
            AddLineRow(new InvoiceLineItem { Description = "HVAC installation service - May 2026", HSNCode = "9987", Unit = "Job", Quantity = 1m, Rate = 105805.08m, GSTPercent = 18m, IsBillable = true, Amount = 105805.08m });
            RecalculateSummary();
            _txtInventorySummary.Text = "Installation completed successfully.";
            _txtNudges.Text = "ⓘ  All required details look good. You can generate and send the invoice.";
            if (_selectedCard != null)
            {
                HighlightCard(_selectedCard, false);
                _selectedCard = null;
            }
            _updating = false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LINE ITEMS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void AddLineRow()
        {
            AddLineRow(null);
        }

        private void AddLineRow(InvoiceLineItem item)
        {
            string description = item?.Description ?? "";
            string unit = string.IsNullOrWhiteSpace(item?.Unit) ? "Nos" : item.Unit;
            EnsureComboValue("Description", description);
            EnsureComboValue("Unit", unit);
            EnsureComboValue("BillingType", item != null && !item.IsBillable ? "Included" : "Billable");
            _grid.Rows.Add(
                description,
                item?.HSNCode ?? "",
                unit,
                (item?.Quantity ?? 1m).ToString("G"),
                (item?.Rate ?? 0m).ToString("0.00"),
                (item?.GSTPercent ?? (_numGST?.Value ?? 18m)).ToString("0.##"),
                item != null && !item.IsBillable ? "Included" : "Billable",
                (item?.Amount ?? 0m).ToString("N2"),
                item?.CoverageNote ?? "",
                item?.StockItemID?.ToString() ?? "",
                item?.IsStockItem == true ? "1" : "0");
            RecalculateSummary();
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
            StockItem stock = _inventoryItems.FirstOrDefault(i => string.Equals(i.ItemName, description, StringComparison.OrdinalIgnoreCase));
            if (stock != null)
            {
                if (string.IsNullOrWhiteSpace(row.Cells["HSNCode"].Value?.ToString()))
                    row.Cells["HSNCode"].Value = "9987";
                if (string.IsNullOrWhiteSpace(row.Cells["Unit"].Value?.ToString()))
                    row.Cells["Unit"].Value = stock.Unit;
                if (TryParseDecimal(row.Cells["Rate"].Value) <= 0)
                    row.Cells["Rate"].Value = stock.LastPurchaseRate.ToString("0.00");
                row.Cells["StockItemID"].Value = stock.ItemID;
                row.Cells["IsStockItem"].Value = "1";
            }
            decimal qty  = TryParseDecimal(row.Cells["Quantity"].Value);
            decimal rate = TryParseDecimal(row.Cells["Rate"].Value);
            bool isBillable = !string.Equals(row.Cells["BillingType"].Value?.ToString(), "Included", StringComparison.OrdinalIgnoreCase);
            row.Cells["Amount"].Value = (isBillable ? Math.Round(qty * rate, 2) : 0m).ToString("N2");
            RecalculateSummary();
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
            decimal sub = 0;
            decimal tax = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                decimal amount = TryParseDecimal(row.Cells["Amount"].Value);
                decimal gstPct = TryParseDecimal(row.Cells["GSTPercent"].Value);
                sub += amount;
                decimal rawTax = amount * ((gstPct <= 0 ? (_numGST?.Value ?? 18m) : gstPct) / 100m);
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
            _cmbClient.Focus();
            ShowStatus("New invoice â€” fill the form and click Save Draft.", Color.Gray);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                Invoice inv = CollectForm();
                if (_current == null || _current.InvoiceID == 0)
                {
                    int id = _invSvc.CreateInvoiceWithLineItems(inv);
                    inv.InvoiceID = id;
                    _current = _invSvc.GetInvoiceById(id);
                    PopulateForm(_current);
                    ShowStatus("Invoice saved: " + _current.InvoiceNumber, SaveGreen);
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
                }
                LoadInvoiceList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnFinalise_Click(object sender, EventArgs e)
        {
            if (_current == null || _current.InvoiceID == 0)
            { MessageBox.Show("Save the invoice first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try
            {
                Invoice invoice = CollectForm();
                invoice.InvoiceID = _current.InvoiceID;
                invoice.InvoiceNumber = _current.InvoiceNumber;
                invoice.PaidAmount = _current.PaidAmount;
                _invSvc.UpdateInvoiceWithLineItems(invoice);
                _invSvc.FinalizeInvoice(_current.InvoiceID);
                _current = _invSvc.GetInvoiceById(_current.InvoiceID);
                PopulateForm(_current);
                ShowStatus("Invoice finalised.", InfoBlue);
                LoadInvoiceList();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
                    MessageBox.Show(ex2.Message, "Payment Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            dlg.Controls.AddRange(new Control[] { lblAmt, numAmt, lblDate, dtpDate, lblMethod, cmbMethod, lblRef, txtRef, btnOK });

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ShowStatus("Payment recorded.", SaveGreen);
                LoadInvoiceList();
                // Refresh current invoice display
                if (_current != null)
                {
                    _current = _invSvc.GetInvoiceById(_current.InvoiceID);
                    if (_current != null) PopulateForm(_current);
                }
            }
        }

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            try
            {
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
                new HtmlPreviewDialog("Invoice Preview - " + (invoice.InvoiceNumber ?? "(draft)"), html).ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show(ex.Message, "Compare Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EVENT HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private void CmbClient_Changed(object sender, EventArgs e)
        {
            if (_updating)
                return;

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

            if (_cmbTemplate != null && _cmbTemplate.SelectedIndex > 0)
                ApplySelectedTemplate(false);
        }

        private void CmbTemplate_Changed(object sender, EventArgs e)
        {
            if (_updating)
                return;

            ApplySelectedTemplate(true);
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
                ShowStatus("Template error: " + ex.Message, Color.Red);
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
            invoice.AssetDetails = _txtAssetDetails.Text.Trim();
            invoice.ServiceChecklist = _txtChecklist.Text.Trim();
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

        private GroupBox MakeGroup(string title)
        {
            return new GroupBox { Text = title, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = InfoBlue, BackColor = SectionBg, Padding = new Padding(8) };
        }

        private Label MakeSummaryRow(GroupBox parent, string text, int x, int y, bool bold = false, Color? color = null)
        {
            Label lbl = new Label
            {
                Text      = text,
                AutoSize  = true,
                Location  = new Point(x, y),
                Font      = new Font("Segoe UI", bold ? 11 : 9, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = color ?? (bold ? DS.Indigo600 : Color.FromArgb(50, 50, 50))
            };
            parent.Controls.Add(lbl);
            return lbl;
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

        private void AddLabel(GroupBox parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) });
        }

        private Button MakeBtn(string text, Color bg, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 28, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
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

        private class InvoicePreviewDialog : Form
        {
            private readonly WebBrowser _browser = new WebBrowser();
            private readonly string _html;

            public InvoicePreviewDialog(string title, string html)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                _html = html;
                Text = "Invoice Preview - " + title;
                Width = 1100;
                Height = 760;
                StartPosition = FormStartPosition.CenterParent;

                Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
                Button btnPrint = new Button { Text = "Print", Width = 90, Height = 28, Location = new Point(10, 8), BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnSave = new Button { Text = "Save As", Width = 90, Height = 28, Location = new Point(110, 8), BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnMail = new Button { Text = "Email", Width = 90, Height = 28, Location = new Point(210, 8), BackColor = Color.FromArgb(15, 118, 110), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnWhatsApp = new Button { Text = "WhatsApp", Width = 100, Height = 28, Location = new Point(310, 8), BackColor = Color.FromArgb(22, 163, 74), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                Button btnRefresh = new Button { Text = "Refresh", Width = 90, Height = 28, Location = new Point(420, 8), BackColor = Color.FromArgb(99, 102, 241), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                btnPrint.FlatAppearance.BorderSize = 0;
                btnSave.FlatAppearance.BorderSize = 0;
                btnMail.FlatAppearance.BorderSize = 0;
                btnWhatsApp.FlatAppearance.BorderSize = 0;
                btnRefresh.FlatAppearance.BorderSize = 0;
                btnPrint.Click += (s, e) => _browser.ShowPrintDialog();
                btnRefresh.Click += (s, e) => _browser.DocumentText = _html;
                btnSave.Click += SaveHtml;
                btnMail.Click += (s, e) => OpenLink("mailto:?subject=" + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString("Please find the invoice preview attached / generated from " + BrandingService.AppName + "."));
                btnWhatsApp.Click += (s, e) => OpenLink("https://wa.me/?text=" + Uri.EscapeDataString(title + " is ready. Please review the invoice shared from " + BrandingService.AppName + "."));
                toolbar.Controls.AddRange(new Control[] { btnPrint, btnSave, btnMail, btnWhatsApp, btnRefresh });

                _browser.Dock = DockStyle.Fill;
                _browser.DocumentText = _html;

                Controls.Add(_browser);
                Controls.Add(toolbar);
            }

            private void SaveHtml(object sender, EventArgs e)
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "HTML Files (*.html)|*.html";
                    dlg.DefaultExt = "html";
                    dlg.AddExtension = true;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        File.WriteAllText(dlg.FileName, _html);
                        MessageBox.Show("Preview saved to " + dlg.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }

            private void OpenLink(string url)
            {
                try { Process.Start(url); } catch { }
            }
        }
    }
}

