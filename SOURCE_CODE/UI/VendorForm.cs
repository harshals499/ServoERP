using System;
using System.Collections.Generic;
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

namespace HVAC_Pro_Desktop.UI
{
    public class VendorForm : DeferredPageControl
    {
        private const int VendorListMinWidth = 300;

        private static readonly Color White = DS.White;
        private static readonly Color PageBg = DS.BgPage;
        private static readonly Color Surface = DS.Slate50;
        private static readonly Color Border = DS.Border;
        private static readonly Color BorderLight = DS.Slate100;
        private static readonly Color TextPrimary = DS.Slate900;
        private static readonly Color TextSecondary = DS.Slate500;
        private static readonly Color TextHint = DS.Slate400;
        private static readonly Color Teal = DS.Teal500;
        private static readonly Color TealLightBg = DS.Teal50;
        private static readonly Color Amber = DS.Amber500;
        private static readonly Color AmberDark = Color.FromArgb(146, 64, 14);
        private static readonly Color AmberLightBg = DS.Amber50;
        private static readonly Color Red = DS.Red500;
        private static readonly Color RedDark = DS.Red600;
        private static readonly Color RedLightBg = DS.Red50;
        private static readonly Color Blue = DS.Primary600;
        private static readonly Color BlueDark = DS.Primary700;
        private static readonly Color BlueLightBg = DS.Primary50;

        private readonly VendorService _vendorSvc = new VendorService();
        private readonly VendorAdvancePaymentService _vendorAdvanceSvc = new VendorAdvancePaymentService();
        private readonly ErrorProvider _errors = new ErrorProvider();

        private readonly List<string> _categories = new List<string> { "General", "HVAC Equipment", "Copper & Pipe", "Electrical", "Refrigerant", "Tools", "Chemicals", "Other" };
        private readonly List<string> _vendorTypes = new List<string> { "Distributor", "Supplier", "Subcontractor", "Labour" };
        private readonly List<string> _msmeTypes = new List<string> { "No", "Yes-Micro", "Yes-Small", "Yes-Medium" };
        private readonly List<string> _gstTypes = new List<string> { "Regular", "Composition", "Unregistered" };
        private readonly List<string> _tdsSections = new List<string> { "194C", "194J", "194Q" };
        private readonly List<string> _paymentModes = new List<string> { "NEFT", "RTGS", "UPI", "Cheque", "Cash" };
        private readonly Dictionary<string, decimal> _tdsRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "194C", 2m },
            { "194J", 10m },
            { "194Q", 0.1m }
        };

        private SplitContainer _split;
        private FlowLayoutPanel _vendorListFlow;
        private FlowLayoutPanel _chipFlow;
        private Panel _searchSection;
        private Panel _duplicateBanner;
        private Label _lblDuplicateBanner;
        private TextBox _txtSearch;
        private Button _btnClearSearch;
        private Label _lblListFooter;
        private LinkLabel _lnkClearSearch;

        private Panel _topBar;
        private Label _lblTopBadge;
        private Label _lblDuplicateBadge;
        private Button _btnMerge;
        private Button _btnImport;
        private Button _btnTemplate;

        private Panel _rightScroll;
        private Panel _rightHost;
        private Panel _vendorHeaderRow;
        private Label _lblHeaderName;
        private Label _lblHeaderMeta;
        private Button _btnRaisePo;
        private Button _btnViewPos;
        private Button _btnSave;
        private Button _btnArchive;

        private Panel _statsRow;
        private Label _lblStatOutstanding;
        private Label _lblStatPurchased;
        private Label _lblStatOpenPos;
        private Label _lblStatCreditDays;

        private Panel _warningStrip;
        private Label _lblWarningStrip;

        private ResizableCard _cardIdentity;
        private ResizableCard _cardContact;
        private ResizableCard _cardTax;
        private ResizableCard _cardPayment;
        private ResizableCard _cardRecentPos;
        private ResizableCard _cardNotes;

        private TextBox _txtVendorName;
        private ComboBox _cmbCategory;
        private FlowLayoutPanel _tagFlow;
        private LinkLabel _lnkAddTag;
        private TextBox _txtAddTag;
        private ComboBox _cmbVendorType;
        private ComboBox _cmbMsme;
        private TextBox _txtMsmeNumber;
        private Label _lblMsmeNote;

        private TextBox _txtPhone;
        private TextBox _txtWhatsApp;
        private LinkLabel _lnkSamePhone;
        private TextBox _txtEmail;
        private TextBox _txtAddress;
        private TextBox _txtCity;
        private ComboBox _cmbState;

        private TextBox _txtGstin;
        private Label _lblGstinState;
        private Label _lblPanHint;
        private TextBox _txtPan;
        private ComboBox _cmbGstType;
        private CheckBox _chkTds;
        private ComboBox _cmbTdsSection;
        private NumericUpDown _numTdsRate;
        private CheckBox _chkRcm;
        private Label _lblTaxNote;
        private Label _lblGstinHint;
        private Label _lblIfscHint;

        private NumericUpDown _numCreditDays;
        private Panel _creditTrack;
        private Panel _creditFill;
        private Label _lblCreditHint;
        private Label _lblMsmeWarning;
        private ComboBox _cmbPaymentMode;
        private TextBox _txtBankAccount;
        private TextBox _txtIfsc;
        private TextBox _txtAccountName;
        private TextBox _txtBankName;

        private FlowLayoutPanel _recentPoFlow;
        private Label _lblRecentPoFooter;
        private TextBox _txtNotes;

        private List<VendorSummaryDto> _vendorSummaries = new List<VendorSummaryDto>();
        private List<DuplicateGroupDto> _duplicateGroups = new List<DuplicateGroupDto>();
        private VendorDetailDto _currentVendor;
        private bool _isBinding;
        private bool _isNewMode;
        private string _activeFilter = "All";
        private bool _searchPlaceholderActive;

        public VendorForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad((Func<Task>)(async () => await LoadInitialAsync()), ex => AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Vendor screen", ex));
        }

        private void BuildLayout()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = VendorListMinWidth,
                Panel1MinSize = VendorListMinWidth,
                SplitterWidth = 1,
                BackColor = Border
            };

            BuildLeftPanel();
            BuildRightPanel();

            Controls.Add(_split);
            Resize += (s, e) => LayoutDetailCards();
            Resize += (s, e) => EnsureVendorListWidth();
        }

        private void EnsureVendorListWidth()
        {
            if (_split == null || _split.IsDisposed)
                return;

            int maxDistance = _split.Width - _split.Panel2MinSize - _split.SplitterWidth;
            if (maxDistance >= VendorListMinWidth && _split.SplitterDistance < VendorListMinWidth)
                _split.SplitterDistance = VendorListMinWidth;
        }

        private void BuildLeftPanel()
        {
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = White };
            left.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, left.Width - 1, 0, left.Width - 1, left.Height);
            };

            _searchSection = new Panel { Dock = DockStyle.Top, Height = 118, Padding = new Padding(12, 10, 12, 10), BackColor = White };
            Panel searchWrap = new Panel { Location = new Point(12, 10), Size = new Size(276, 38), BackColor = White };
            searchWrap.Paint += (s, e) => DrawSearchBox(e.Graphics, searchWrap.ClientRectangle);
            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = White,
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(12, 10),
                Width = 224
            };
            ConfigurePlaceholder(_txtSearch, "Search vendors, category, city...");
            _txtSearch.TextChanged += (s, e) => { UpdateSearchClear(); ApplyFilters(); };
            _btnClearSearch = new Button
            {
                Text = "x",
                FlatStyle = FlatStyle.Flat,
                BackColor = White,
                ForeColor = TextHint,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Size = new Size(28, 24),
                Location = new Point(240, 7),
                Visible = false
            };
            _btnClearSearch.FlatAppearance.BorderSize = 0;
            _btnClearSearch.Click += (s, e) => _txtSearch.Text = string.Empty;
            searchWrap.Controls.Add(_txtSearch);
            searchWrap.Controls.Add(_btnClearSearch);

            _chipFlow = new FlowLayoutPanel
            {
                Location = new Point(12, 60),
                Size = new Size(276, 36),
                BackColor = White,
                WrapContents = true,
                AutoScroll = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            _searchSection.Controls.Add(searchWrap);
            _searchSection.Controls.Add(_chipFlow);

            _duplicateBanner = new Panel { Dock = DockStyle.Top, Height = 0, BackColor = RedLightBg, Padding = new Padding(12, 7, 12, 7), Visible = false };
            _duplicateBanner.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(247, 193, 193)))
                    e.Graphics.DrawLine(pen, 0, _duplicateBanner.Height - 1, _duplicateBanner.Width, _duplicateBanner.Height - 1);
            };
            _lblDuplicateBanner = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = RedDark };
            _duplicateBanner.Controls.Add(_lblDuplicateBanner);

            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(8, 8, 8, 8) };
            _vendorListFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = White,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4, 0, 4, 8)
            };
            listWrap.Controls.Add(_vendorListFlow);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = White, Padding = new Padding(12, 0, 12, 8) };
            _lblListFooter = new Label { Dock = DockStyle.Fill, ForeColor = TextHint, Font = new Font("Segoe UI", 8.5f), Text = "Loading..." };
            _lnkClearSearch = new LinkLabel { Dock = DockStyle.Right, Width = 90, Text = "Clear search", LinkColor = Blue, TextAlign = ContentAlignment.MiddleRight, Visible = false };
            _lnkClearSearch.Click += (s, e) => _txtSearch.Text = string.Empty;
            footer.Controls.Add(_lblListFooter);
            footer.Controls.Add(_lnkClearSearch);

            left.Controls.Add(listWrap);
            left.Controls.Add(footer);
            left.Controls.Add(_duplicateBanner);
            left.Controls.Add(_searchSection);
            _split.Panel1.Controls.Add(left);
            _split.Panel1.Resize += (s, e) => UpdateSearchSectionLayout();
        }

        private void BuildRightPanel()
        {
            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };
            _topBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(26, 82, 118), Padding = new Padding(16, 0, 16, 0) };
            BuildTopBar();

            _rightScroll = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, AutoScroll = true, Padding = new Padding(20, 16, 20, 20) };
            _rightHost = new Panel { BackColor = PageBg, Location = new Point(0, 0), Size = new Size(1000, 900) };
            _rightScroll.Controls.Add(_rightHost);

            BuildHeaderRow();
            BuildStatsRow();
            BuildWarningStrip();
            BuildCards();

            _rightHost.Controls.Add(_cardNotes);
            _rightHost.Controls.Add(_cardRecentPos);
            _rightHost.Controls.Add(_cardIdentity);
            _rightHost.Controls.Add(_warningStrip);
            _rightHost.Controls.Add(_statsRow);
            _rightHost.Controls.Add(_vendorHeaderRow);

            right.Controls.Add(_rightScroll);
            right.Controls.Add(_topBar);
            _split.Panel2.Controls.Add(right);
        }

        private void BuildTopBar()
        {
            Label title = new Label
            {
                Text = "VENDOR MANAGEMENT",
                Dock = DockStyle.Left,
                Width = 210,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = White,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblTopBadge = CreateBadgeLabel(90, TealLightBg, Color.FromArgb(15, 110, 86));
            _lblDuplicateBadge = CreateBadgeLabel(150, RedLightBg, RedDark);
            _lblDuplicateBadge.Visible = false;

            FlowLayoutPanel badgeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 270,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = _topBar.BackColor,
                Padding = new Padding(6, 17, 0, 0)
            };
            badgeFlow.Controls.Add(_lblTopBadge);
            badgeFlow.Controls.Add(_lblDuplicateBadge);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 600,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = _topBar.BackColor,
                Padding = new Padding(0, 13, 0, 0)
            };

            _btnMerge = MakeActionButton("Merge duplicates", White, RedDark, 118, true);
            _btnMerge.Visible = false;
            _btnMerge.Click += (s, e) => ShowMergeDialog();

            _btnTemplate = MakeActionButton("Template", White, TextPrimary, 82, true);
            _btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Vendors, FindForm());

            _btnImport = MakeActionButton("Import", White, TextPrimary, 78, true);
            _btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Vendors, FindForm());

            Button btnExport = MakeActionButton("Export", White, TextPrimary, 78, true);
            btnExport.Click += (s, e) => ExportCsv();

            Button btnRefresh = MakeActionButton("Refresh", White, TextPrimary, 78, true);
            btnRefresh.Click += async (s, e) => await RefreshAsync(true);

            Button btnNew = MakeActionButton("+ New Vendor", Teal, White, 104, false);
            btnNew.Click += async (s, e) => await BeginNewVendorAsync();

            actions.Controls.Add(_btnMerge);
            actions.Controls.Add(_btnTemplate);
            actions.Controls.Add(_btnImport);
            actions.Controls.Add(btnExport);
            actions.Controls.Add(btnRefresh);
            actions.Controls.Add(btnNew);

            _topBar.Controls.Add(actions);
            _topBar.Controls.Add(badgeFlow);
            _topBar.Controls.Add(title);
        }

        private void BuildHeaderRow()
        {
            _vendorHeaderRow = new Panel { BackColor = White, Height = 72, Width = 900 };
            _vendorHeaderRow.Paint += (s, e) => DrawCardOutline(e.Graphics, _vendorHeaderRow.ClientRectangle);

            Panel textWrap = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(16, 10, 16, 10) };
            _lblHeaderName = new Label { Text = "Select a vendor", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 18f, FontStyle.Regular), ForeColor = TextPrimary };
            _lblHeaderMeta = new Label { Text = "Supplier - City - Added", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 10f), ForeColor = TextSecondary };
            textWrap.Controls.Add(_lblHeaderMeta);
            textWrap.Controls.Add(_lblHeaderName);

            TableLayoutPanel actions = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 430,
                BackColor = White,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(0, 18, 16, 0)
            };
            for (int i = 0; i < 4; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

            _btnRaisePo = MakeActionButton("Raise PO", White, TextPrimary, 96, true);
            _btnRaisePo.Click += (s, e) => RaisePo();
            _btnViewPos = MakeActionButton("View POs", White, TextPrimary, 96, true);
            _btnViewPos.Click += (s, e) => ViewPurchaseOrders();
            _btnSave = MakeActionButton("Save", Teal, White, 96, false);
            _btnSave.Click += async (s, e) => await SaveVendorAsync(true);
            _btnArchive = MakeActionButton("Archive", White, RedDark, 96, true);
            _btnArchive.FlatAppearance.BorderColor = Color.FromArgb(247, 193, 193);
            _btnArchive.Click += async (s, e) => await ArchiveVendorAsync();

            foreach (Button button in new[] { _btnRaisePo, _btnViewPos, _btnSave, _btnArchive })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(4, 0, 4, 0);
            }
            actions.Controls.Add(_btnRaisePo, 0, 0);
            actions.Controls.Add(_btnViewPos, 1, 0);
            actions.Controls.Add(_btnArchive, 2, 0);
            actions.Controls.Add(_btnSave, 3, 0);

            _vendorHeaderRow.Controls.Add(actions);
            _vendorHeaderRow.Controls.Add(textWrap);
        }

        private void BuildStatsRow()
        {
            _statsRow = new Panel { BackColor = PageBg, Height = 86, Width = 900 };
            string[] labels = { "OUTSTANDING", "TOTAL PURCHASED", "OPEN POS", "CREDIT DAYS" };
            Label[] values = new Label[4];

            for (int i = 0; i < 4; i++)
            {
                Panel card = new Panel { BackColor = Surface, Size = new Size(205, 70), Location = new Point(i * 220, 0) };
                card.Paint += (s, e) => DrawRoundedSurface(e.Graphics, card.ClientRectangle, Surface, 8);
                Label lbl = new Label { Text = labels[i], Location = new Point(12, 10), Size = new Size(180, 14), Font = new Font("Segoe UI", 8f, FontStyle.Regular), ForeColor = TextSecondary };
                Label val = new Label { Text = "-", Location = new Point(12, 28), Size = new Size(180, 28), Font = new Font("Segoe UI", 17f, FontStyle.Regular), ForeColor = TextPrimary };
                values[i] = val;
                card.Controls.Add(lbl);
                card.Controls.Add(val);
                _statsRow.Controls.Add(card);
            }

            _lblStatOutstanding = values[0];
            _lblStatPurchased = values[1];
            _lblStatOpenPos = values[2];
            _lblStatCreditDays = values[3];
        }

        private void BuildWarningStrip()
        {
            _warningStrip = new Panel { BackColor = AmberLightBg, Height = 34, Width = 900, Visible = false, Padding = new Padding(10, 6, 10, 6) };
            _warningStrip.Paint += (s, e) => DrawRoundedSurface(e.Graphics, _warningStrip.ClientRectangle, AmberLightBg, 6);
            _lblWarningStrip = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(133, 79, 11) };
            _warningStrip.Controls.Add(_lblWarningStrip);
        }

        private void BuildCards()
        {
            _cardIdentity = CreateCard("vendor_details", "Vendor details", 720);
            _cardIdentity.AllowResize = false;
            _cardIdentity.ShowHeader = false;
            _cardContact = CreateCard("contact_details", "Contact details", 310);
            _cardTax = CreateCard("tax_compliance", "Tax & compliance", 320);
            _cardPayment = CreateCard("payment_terms", "Payment terms", 320);
            _cardRecentPos = CreateCard("recent_purchase_orders", "Recent purchase orders", 260);
            _cardNotes = CreateCard("vendor_notes", "Notes", 180);

            BuildIdentityCard();
            BuildContactCard();
            BuildTaxCard();
            BuildPaymentCard();
            ConsolidateVendorDetailCards();
            BuildRecentPoCard();
            BuildNotesCard();
        }

        private ResizableCard CreateCard(string key, string title, int height)
        {
            ResizableCard card = new ResizableCard
            {
                PageKey = "VendorManagement",
                CardKey = "vendor_" + key,
                CardTitle = title,
                BackgroundColor = White,
                BorderColor = Border,
                ResizeAxes = CardResizeAxes.HeightOnly,
                AllowResize = true,
                Size = new Size(420, height),
                MinimumSize = new Size(280, 140),
                BackColor = Color.Transparent
            };
            card.CardResizeComplete += (s, e) => LayoutDetailCards();
            card.CardResized += (s, e) => LayoutDetailCards();
            return card;
        }

        private void BuildIdentityCard()
        {
            Panel body = _cardIdentity.ContentPanel;
            PlaceLabel(body, "Vendor name", 0, 0);
            _txtVendorName = PlaceTextBox(body, 0, 20, 378);

            PlaceLabel(body, "Category", 0, 58);
            _cmbCategory = PlaceComboBox(body, 0, 78, 180, _categories);

            PlaceLabel(body, "Vendor type", 198, 58);
            _cmbVendorType = PlaceComboBox(body, 198, 78, 150, _vendorTypes);

            PlaceLabel(body, "Specialisation tags", 0, 118);
            _tagFlow = new FlowLayoutPanel { Location = new Point(0, 138), Size = new Size(378, 64), BackColor = White, AutoScroll = true };
            _lnkAddTag = new LinkLabel { Text = "+ Add tag", LinkColor = Blue, AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
            _lnkAddTag.Click += (s, e) => ShowTagEditor();
            _txtAddTag = new TextBox { Visible = false, Width = 120, Margin = new Padding(0, 2, 8, 0) };
            _txtAddTag.KeyDown += TagEditorKeyDown;
            _txtAddTag.LostFocus += (s, e) => CommitTagEditor();
            _tagFlow.Controls.Add(_lnkAddTag);
            _tagFlow.Controls.Add(_txtAddTag);
            body.Controls.Add(_tagFlow);

            PlaceLabel(body, "MSME registered", 0, 214);
            _cmbMsme = PlaceComboBox(body, 0, 234, 180, _msmeTypes);
            _cmbMsme.SelectedIndexChanged += (s, e) => UpdateMsmeVisibility();

            PlaceLabel(body, "MSME number", 198, 214);
            _txtMsmeNumber = PlaceTextBox(body, 198, 234, 180);

            _lblMsmeNote = new Label
            {
                Location = new Point(0, 270),
                Size = new Size(378, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = BlueDark,
                Text = "MSME vendors must be paid within 45 days",
                Visible = false
            };
            body.Controls.Add(_lblMsmeNote);
        }

        private void BuildContactCard()
        {
            Panel body = _cardContact.ContentPanel;
            PlaceLabel(body, "Primary phone", 0, 0);
            _txtPhone = PlaceTextBox(body, 0, 20, 170);

            PlaceLabel(body, "WhatsApp number", 198, 0);
            _txtWhatsApp = PlaceTextBox(body, 198, 20, 180);
            _lnkSamePhone = new LinkLabel { Text = "Same as phone", LinkColor = Blue, AutoSize = true, Location = new Point(278, 48) };
            _lnkSamePhone.Click += (s, e) => _txtWhatsApp.Text = _txtPhone.Text;
            body.Controls.Add(_lnkSamePhone);

            PlaceLabel(body, "Email", 0, 70);
            _txtEmail = PlaceTextBox(body, 0, 90, 378);

            PlaceLabel(body, "Address", 0, 128);
            _txtAddress = PlaceTextBox(body, 0, 148, 378);
            _txtAddress.Multiline = true;
            _txtAddress.Height = 54;

            PlaceLabel(body, "City", 0, 214);
            _txtCity = PlaceTextBox(body, 0, 234, 180);

            PlaceLabel(body, "State", 198, 214);
            _cmbState = PlaceComboBox(body, 198, 234, 180, IndiaStateCatalog.Names.OrderBy(name => name).ToList());
        }

        private void BuildTaxCard()
        {
            Panel body = _cardTax.ContentPanel;
            PlaceLabel(body, "GST number", 0, 0);
            _txtGstin = PlaceTextBox(body, 0, 20, 160);
            _txtGstin.CharacterCasing = CharacterCasing.Upper;
            _txtGstin.TextChanged += (s, e) => UpdateGstinState();
            _lblGstinHint = new Label { Location = new Point(0, 48), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            body.Controls.Add(_lblGstinHint);

            PlaceLabel(body, "PAN number", 198, 0);
            _txtPan = PlaceTextBox(body, 198, 20, 180);
            _txtPan.CharacterCasing = CharacterCasing.Upper;
            _txtPan.TextChanged += (s, e) => UpdatePanState();
            _lblPanHint = new Label { Location = new Point(198, 48), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            body.Controls.Add(_lblPanHint);

            _lblGstinState = new Label { Location = new Point(0, 68), Size = new Size(378, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = BlueDark };
            body.Controls.Add(_lblGstinState);

            PlaceLabel(body, "GST registration type", 0, 98);
            _cmbGstType = PlaceComboBox(body, 0, 118, 180, _gstTypes);
            _cmbGstType.SelectedIndexChanged += (s, e) => UpdateGstTypeState();

            _chkTds = new CheckBox { Text = "TDS applicable", Location = new Point(198, 118), AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
            _chkTds.CheckedChanged += (s, e) => UpdateTdsVisibility();
            body.Controls.Add(_chkTds);

            PlaceLabel(body, "TDS section", 0, 170);
            _cmbTdsSection = PlaceComboBox(body, 0, 190, 120, _tdsSections);
            _cmbTdsSection.SelectedIndexChanged += (s, e) => ApplyTdsDefaults();

            PlaceLabel(body, "TDS rate %", 140, 170);
            _numTdsRate = new NumericUpDown { Location = new Point(140, 190), Size = new Size(80, 24), DecimalPlaces = 2, Maximum = 100, Minimum = 0, Font = new Font("Segoe UI", 9f) };
            body.Controls.Add(_numTdsRate);

            _chkRcm = new CheckBox { Text = "RCM applicable", Location = new Point(238, 192), AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
            body.Controls.Add(_chkRcm);

            _lblTaxNote = new Label { Location = new Point(0, 228), Size = new Size(378, 38), Font = new Font("Segoe UI", 8.5f), ForeColor = AmberDark };
            body.Controls.Add(_lblTaxNote);
        }

        private void BuildPaymentCard()
        {
            Panel body = _cardPayment.ContentPanel;
            PlaceLabel(body, "Default credit days", 0, 0);
            _numCreditDays = new NumericUpDown { Location = new Point(0, 20), Size = new Size(90, 24), Minimum = 0, Maximum = 180, Font = new Font("Segoe UI", 9f) };
            _numCreditDays.ValueChanged += (s, e) => UpdateCreditVisuals();
            body.Controls.Add(_numCreditDays);

            _creditTrack = new Panel { Location = new Point(108, 24), Size = new Size(270, 8), BackColor = BorderLight };
            _creditTrack.Paint += (s, e) => DrawRoundedSurface(e.Graphics, _creditTrack.ClientRectangle, BorderLight, 4);
            _creditFill = new Panel { Location = new Point(0, 0), Size = new Size(0, 8), BackColor = Amber };
            _creditFill.Paint += (s, e) => DrawRoundedSurface(e.Graphics, _creditFill.ClientRectangle, Amber, 4);
            _creditTrack.Controls.Add(_creditFill);
            body.Controls.Add(_creditTrack);

            _lblCreditHint = new Label { Location = new Point(0, 50), Size = new Size(200, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint, Text = "0 = pay on delivery" };
            _lblMsmeWarning = new Label { Location = new Point(0, 70), Size = new Size(378, 18), Font = new Font("Segoe UI", 8.5f), ForeColor = RedDark, Visible = false };
            body.Controls.Add(_lblCreditHint);
            body.Controls.Add(_lblMsmeWarning);

            PlaceLabel(body, "Preferred payment mode", 0, 102);
            _cmbPaymentMode = PlaceComboBox(body, 0, 122, 180, _paymentModes);

            PlaceLabel(body, "Bank account number", 0, 160);
            _txtBankAccount = PlaceTextBox(body, 0, 180, 180);

            PlaceLabel(body, "IFSC code", 198, 160);
            _txtIfsc = PlaceTextBox(body, 198, 180, 180);
            _txtIfsc.CharacterCasing = CharacterCasing.Upper;
            _txtIfsc.TextChanged += (s, e) => UpdateIfscState();
            _lblIfscHint = new Label { Location = new Point(198, 208), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            body.Controls.Add(_lblIfscHint);

            PlaceLabel(body, "Account holder name", 0, 238);
            _txtAccountName = PlaceTextBox(body, 0, 258, 180);

            PlaceLabel(body, "Bank name", 198, 238);
            _txtBankName = PlaceTextBox(body, 198, 258, 180);
        }

        private void ConsolidateVendorDetailCards()
        {
            Panel host = _cardIdentity.ContentPanel;
            host.AutoScroll = false;
            host.Controls.Add(MakeSectionTitle("Vendor details", 0, 0, 860, 16f));

            OffsetExistingControls(host, 0, 74);
            host.Controls.Add(MakeSectionTitle("Vendor identity", 0, 44, 390, 10f));
            host.Controls.Add(MakeSectionTitle("Contact details", 450, 44, 390, 10f));
            host.Controls.Add(MakeSectionTitle("Tax compliance", 0, 356, 390, 10f));
            host.Controls.Add(MakeSectionTitle("Payment terms", 450, 356, 390, 10f));

            MoveSectionControls(_cardContact.ContentPanel, host, 450, 74);
            MoveSectionControls(_cardTax.ContentPanel, host, 0, 386);
            MoveSectionControls(_cardPayment.ContentPanel, host, 450, 386);
        }

        private static Label MakeSectionTitle(string text, int x, int y, int width, float size)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, size > 12f ? 28 : 22),
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static void OffsetExistingControls(Control parent, int dx, int dy)
        {
            foreach (Control control in parent.Controls.Cast<Control>().ToList())
            {
                if (control is Label label && label.Text == "Vendor details")
                    continue;
                control.Location = new Point(control.Left + dx, control.Top + dy);
            }
        }

        private static void MoveSectionControls(Control source, Control target, int dx, int dy)
        {
            foreach (Control control in source.Controls.Cast<Control>().ToList())
            {
                source.Controls.Remove(control);
                control.Location = new Point(control.Left + dx, control.Top + dy);
                target.Controls.Add(control);
            }
        }

        private void BuildRecentPoCard()
        {
            Panel body = _cardRecentPos.ContentPanel;
            LinkLabel lnkViewAll = new LinkLabel { Text = "View all ->", LinkColor = Blue, AutoSize = true, Location = new Point(0, 0) };
            lnkViewAll.Click += (s, e) => ViewPurchaseOrders();
            body.Controls.Add(lnkViewAll);

            _recentPoFlow = new FlowLayoutPanel
            {
                Location = new Point(0, 28),
                Size = new Size(820, 160),
                AutoScroll = true,
                BackColor = White,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            body.Controls.Add(_recentPoFlow);

            _lblRecentPoFooter = new Label { Location = new Point(0, 194), Size = new Size(820, 18), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary };
            body.Controls.Add(_lblRecentPoFooter);
        }

        private void BuildNotesCard()
        {
            Panel body = _cardNotes.ContentPanel;
            _txtNotes = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            ConfigurePlaceholder(_txtNotes, "Vendor notes, special terms, delivery preferences...");
            _txtNotes.Leave += async (s, e) => await AutoSaveNotesAsync();
            body.Controls.Add(_txtNotes);
        }

        private async Task LoadInitialAsync()
        {
            await RefreshAsync(false);
        }

        private async Task RefreshAsync(bool preserveSelection)
        {
            try
            {
                SetBusyState(true, "Loading vendors...");
                int? selectedVendorId = preserveSelection ? (int?)_currentVendor?.VendorID : null;
                List<VendorSummaryDto> summaries = null;
                List<DuplicateGroupDto> duplicates = null;

                await Task.Run(() =>
                {
                    summaries = _vendorSvc.GetAllVendorsWithSummary();
                    duplicates = _vendorSvc.DetectDuplicates();
                });

                _vendorSummaries = summaries ?? new List<VendorSummaryDto>();
                _duplicateGroups = duplicates ?? new List<DuplicateGroupDto>();
                UpdateTopBar();
                BuildFilterChips();
                UpdateDuplicateBanner();
                ApplyFilters();

                if (selectedVendorId.HasValue && _vendorSummaries.Any(v => v.VendorId == selectedVendorId.Value))
                    await LoadVendorDetailAsync(selectedVendorId.Value);
                else if (_vendorSummaries.Count > 0)
                    await LoadVendorDetailAsync(_vendorSummaries[0].VendorId);
                else
                    PrepareBlankVendor();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.RefreshAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Vendor refresh", ex);
                PrepareBlankVendor();
            }
            finally
            {
                SetBusyState(false, string.Empty);
            }
        }

        private void SetBusyState(bool busy, string message)
        {
            _lblListFooter.Text = busy ? message : _lblListFooter.Text;
            _btnSave.Enabled = !busy;
            _btnRaisePo.Enabled = !busy;
            _btnArchive.Enabled = !busy;
            _btnViewPos.Enabled = !busy;
        }

        private void UpdateTopBar()
        {
            _lblTopBadge.Text = _vendorSummaries.Count + " vendors";
            _lblDuplicateBadge.Text = _duplicateGroups.Count + " duplicates detected";
            _lblDuplicateBadge.Visible = _duplicateGroups.Count > 0;
            _btnMerge.Visible = _duplicateGroups.Count > 0;
        }

        private void UpdateDuplicateBanner()
        {
            DuplicateGroupDto topGroup = _duplicateGroups.FirstOrDefault();
            if (topGroup == null)
            {
                _duplicateBanner.Visible = false;
                _duplicateBanner.Height = 0;
                return;
            }

            _lblDuplicateBanner.Text = "Warning: " + topGroup.Vendors.First().VendorName + " appears " + topGroup.Vendors.Count + "x - merge needed";
            _duplicateBanner.Visible = true;
            _duplicateBanner.Height = 34;
        }

        private void BuildFilterChips()
        {
            _chipFlow.SuspendLayout();
            _chipFlow.Controls.Clear();

            int allCount = _vendorSummaries.Count(v => !v.IsArchived);
            int activeCount = _vendorSummaries.Count(v => v.IsActive && !v.IsArchived);
            int overdueCount = _vendorSummaries.Count(v => v.HasOverdue && !v.IsArchived);
            int msmeCount = _vendorSummaries.Count(v => !string.Equals(v.MSMERegistered, "No", StringComparison.OrdinalIgnoreCase) && !v.IsArchived);
            int archivedCount = _vendorSummaries.Count(v => v.IsArchived);

            AddFilterChip("All", allCount, TealLightBg, Color.FromArgb(15, 110, 86));
            if (activeCount > 0) AddFilterChip("Active", activeCount, White, TextPrimary);
            if (overdueCount > 0) AddFilterChip("Overdue", overdueCount, RedLightBg, RedDark);
            if (msmeCount > 0) AddFilterChip("MSME", msmeCount, BlueLightBg, BlueDark);
            if (archivedCount > 0) AddFilterChip("Archived", archivedCount, Surface, TextSecondary);
            _chipFlow.ResumeLayout();
            UpdateSearchSectionLayout();
        }

        private void UpdateSearchSectionLayout()
        {
            if (_searchSection == null || _chipFlow == null || _searchSection.IsDisposed)
                return;

            int availableWidth = Math.Max(220, _split.Panel1.ClientSize.Width - 24);
            Control searchWrap = _searchSection.Controls.OfType<Panel>().FirstOrDefault();
            if (searchWrap != null)
            {
                searchWrap.Location = new Point(12, 10);
                searchWrap.Width = availableWidth;
                if (_txtSearch != null)
                    _txtSearch.Width = Math.Max(120, searchWrap.Width - 52);
                if (_btnClearSearch != null)
                    _btnClearSearch.Left = searchWrap.Width - _btnClearSearch.Width - 8;
                searchWrap.Invalidate();
            }

            _chipFlow.Location = new Point(12, 60);
            _chipFlow.Width = availableWidth;

            int x = 0;
            int rows = 1;
            foreach (Control chip in _chipFlow.Controls)
            {
                int chipWidth = chip.Width + chip.Margin.Horizontal;
                if (x > 0 && x + chipWidth > availableWidth)
                {
                    rows++;
                    x = 0;
                }
                x += chipWidth;
            }

            int rowHeight = 36;
            _chipFlow.Height = Math.Max(34, rows * rowHeight);
            _searchSection.Height = 72 + _chipFlow.Height;
        }

        private void AddFilterChip(string key, int count, Color bg, Color fg)
        {
            Button chip = new Button
            {
                AutoSize = false,
                Text = key + " (" + count + ")",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                BackColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? DS.Primary600 : bg,
                ForeColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? White : fg,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 8, 8),
                Tag = key,
                Height = 32,
                Width = Math.Max(92, TextRenderer.MeasureText(key + " (" + count + ")", new Font("Segoe UI", 8.75f, FontStyle.Bold)).Width + 28),
                TextAlign = ContentAlignment.MiddleCenter
            };
            chip.FlatAppearance.BorderSize = 1;
            chip.FlatAppearance.BorderColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? DS.Primary600 : Border;
            chip.FlatAppearance.MouseOverBackColor = string.Equals(_activeFilter, key, StringComparison.OrdinalIgnoreCase) ? DS.Primary700 : Surface;
            chip.Click += (s, e) =>
            {
                _activeFilter = Convert.ToString(((Button)s).Tag);
                BuildFilterChips();
                ApplyFilters();
            };
            _chipFlow.Controls.Add(chip);
        }

        private void ApplyFilters()
        {
            IEnumerable<VendorSummaryDto> query = _vendorSummaries;
            bool includeArchived = string.Equals(_activeFilter, "Archived", StringComparison.OrdinalIgnoreCase);
            if (!includeArchived)
                query = query.Where(v => !v.IsArchived);

            switch ((_activeFilter ?? string.Empty).ToUpperInvariant())
            {
                case "ACTIVE":
                    query = query.Where(v => v.IsActive && !v.IsArchived);
                    break;
                case "OVERDUE":
                    query = query.Where(v => v.HasOverdue && !v.IsArchived);
                    break;
                case "MSME":
                    query = query.Where(v => !string.Equals(v.MSMERegistered, "No", StringComparison.OrdinalIgnoreCase) && !v.IsArchived);
                    break;
                case "ARCHIVED":
                    query = query.Where(v => v.IsArchived);
                    break;
            }

            string term = GetSearchText();
            if (!string.IsNullOrWhiteSpace(term))
            {
                string search = term.Trim().ToUpperInvariant();
                query = query.Where(v =>
                    (v.VendorName ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.Category ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.City ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.Phone ?? string.Empty).ToUpperInvariant().Contains(search) ||
                    (v.VendorType ?? string.Empty).ToUpperInvariant().Contains(search));
            }

            RenderVendorList(query.ToList());
        }

        private void RenderVendorList(List<VendorSummaryDto> items)
        {
            _vendorListFlow.SuspendLayout();
            _vendorListFlow.Controls.Clear();

            if (items.Count == 0)
            {
                Panel empty = new Panel { Width = Math.Max(240, _vendorListFlow.ClientSize.Width - 30), Height = 120, Margin = new Padding(0, 20, 0, 0), BackColor = White };
                Label lbl = new Label { Text = "No vendors match your search", Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 11f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleCenter };
                LinkLabel lnk = new LinkLabel { Text = "Clear search", Dock = DockStyle.Top, Height = 24, LinkColor = Blue, TextAlign = ContentAlignment.MiddleCenter };
                lnk.Click += (s, e) => _txtSearch.Text = string.Empty;
                empty.Controls.Add(lnk);
                empty.Controls.Add(lbl);
                _vendorListFlow.Controls.Add(empty);
                _lnkClearSearch.Visible = !string.IsNullOrWhiteSpace(GetSearchText());
                _lblListFooter.Text = "No vendors shown";
                _vendorListFlow.ResumeLayout();
                return;
            }

            foreach (VendorSummaryDto item in items)
                _vendorListFlow.Controls.Add(BuildVendorListItem(item));

            _lnkClearSearch.Visible = !string.IsNullOrWhiteSpace(GetSearchText());
            _lblListFooter.Text = items.Count + " vendors shown";
            _vendorListFlow.ResumeLayout();
        }

        private Control BuildVendorListItem(VendorSummaryDto item)
        {
            Panel panel = new Panel
            {
                Width = Math.Max(250, _vendorListFlow.ClientSize.Width - 32),
                Height = 76,
                BackColor = _currentVendor != null && _currentVendor.VendorID == item.VendorId ? TealLightBg : White,
                Margin = new Padding(0, 0, 0, 8),
                Tag = item,
                Cursor = Cursors.Hand
            };
            panel.Paint += (s, e) =>
            {
                Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                using (SolidBrush brush = new SolidBrush(panel.BackColor))
                    e.Graphics.FillRectangle(brush, rect);
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, rect);
                if (_currentVendor != null && _currentVendor.VendorID == item.VendorId)
                {
                    using (SolidBrush accent = new SolidBrush(Teal))
                        e.Graphics.FillRectangle(accent, new Rectangle(0, 0, 3, panel.Height));
                }
            };
            panel.MouseEnter += (s, e) =>
            {
                if (_currentVendor == null || _currentVendor.VendorID != item.VendorId)
                    panel.BackColor = Color.FromArgb(250, 250, 250);
                panel.Invalidate();
            };
            panel.MouseLeave += (s, e) =>
            {
                panel.BackColor = _currentVendor != null && _currentVendor.VendorID == item.VendorId ? TealLightBg : White;
                panel.Invalidate();
            };
            panel.Click += async (s, e) => await LoadVendorDetailAsync(item.VendorId);

            Label lblName = new Label { Text = item.VendorName, Location = new Point(12, 10), Size = new Size(170, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary };
            Label lblMeta = new Label { Text = JoinBullet(item.City, item.Category), Location = new Point(12, 32), Size = new Size(190, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            Label lblPhone = new Label { Text = string.IsNullOrWhiteSpace(item.Phone) ? "No phone" : item.Phone, Location = new Point(12, 52), Size = new Size(120, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            Label lblBalance = new Label
            {
                Text = item.OutstandingBalance > 0 ? IndiaFormatHelper.FormatCurrency(item.OutstandingBalance) + " due" : "No balance",
                Location = new Point(panel.Width - 140, 52),
                Size = new Size(128, 16),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = item.HasOverdue ? RedDark : item.OutstandingBalance > 0 ? AmberDark : TextHint,
                TextAlign = ContentAlignment.TopRight
            };
            Panel pill = CreateStatusPill(item);
            pill.Location = new Point(panel.Width - pill.Width - 12, 8);

            foreach (Control ctl in new Control[] { lblName, lblMeta, lblPhone, lblBalance, pill })
            {
                ctl.Click += async (s, e) => await LoadVendorDetailAsync(item.VendorId);
                panel.Controls.Add(ctl);
            }

            return panel;
        }

        private Panel CreateStatusPill(VendorSummaryDto item)
        {
            string text;
            Color bg;
            Color fg;
            if (item.IsArchived)
            {
                text = "Archived";
                bg = Surface;
                fg = TextHint;
            }
            else if (item.IsDuplicate)
            {
                text = "Duplicate";
                bg = RedLightBg;
                fg = RedDark;
            }
            else
            {
                text = "Active";
                bg = Color.FromArgb(234, 243, 222);
                fg = Color.FromArgb(39, 80, 10);
            }

            int width = Math.Max(64, TextRenderer.MeasureText(text, new Font("Segoe UI", 8.5f, FontStyle.Bold)).Width + 20);
            Panel panel = new Panel { Size = new Size(width, 22), BackColor = bg };
            panel.Paint += (s, e) => DrawRoundedSurface(e.Graphics, panel.ClientRectangle, bg, 10);
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = fg, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private async Task LoadVendorDetailAsync(int vendorId)
        {
            try
            {
                _lblHeaderName.Text = "Loading vendor...";
                VendorDetailDto detail = await Task.Run(() => _vendorSvc.GetVendorDetail(vendorId));
                if (detail == null)
                    return;
                _currentVendor = detail;
                _isNewMode = false;
                BindVendor(detail);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.LoadVendorDetailAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Loading vendor detail", ex);
            }
        }

        private async Task BeginNewVendorAsync()
        {
            await Task.Yield();
            PrepareBlankVendor();
            _txtVendorName.Focus();
        }

        private void PrepareBlankVendor()
        {
            _currentVendor = new VendorDetailDto
            {
                VendorType = "Supplier",
                DefaultCreditDays = 30,
                GSTRegistrationType = "Regular",
                MSMERegistered = "No",
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            _isNewMode = true;
            BindVendor(_currentVendor);
        }

        private void BindVendor(VendorDetailDto vendor)
        {
            _isBinding = true;
            try
            {
                ClearErrors();
                _lblHeaderName.Text = string.IsNullOrWhiteSpace(vendor.VendorName) ? "New vendor" : vendor.VendorName;
                _lblHeaderMeta.Text = JoinBullet(string.IsNullOrWhiteSpace(vendor.VendorType) ? "Supplier" : vendor.VendorType, string.IsNullOrWhiteSpace(vendor.City) ? "City pending" : vendor.City, "Added " + vendor.CreatedDate.Year);

                _txtVendorName.Text = vendor.VendorName ?? string.Empty;
                _cmbCategory.SelectedItem = !string.IsNullOrWhiteSpace(vendor.Category) ? vendor.Category : "General";
                _cmbVendorType.SelectedItem = !string.IsNullOrWhiteSpace(vendor.VendorType) ? vendor.VendorType : "Supplier";
                _cmbMsme.SelectedItem = !string.IsNullOrWhiteSpace(vendor.MSMERegistered) ? vendor.MSMERegistered : "No";
                _txtMsmeNumber.Text = vendor.MSMENumber ?? string.Empty;

                _txtPhone.Text = vendor.Phone ?? string.Empty;
                _txtWhatsApp.Text = vendor.WhatsAppNumber ?? string.Empty;
                _txtEmail.Text = vendor.Email ?? string.Empty;
                _txtAddress.Text = vendor.Address ?? string.Empty;
                _txtCity.Text = vendor.City ?? string.Empty;
                SetStateComboByCodeOrName(vendor.StateCode);

                _txtGstin.Text = vendor.GSTNumber ?? string.Empty;
                _txtPan.Text = vendor.PANNumber ?? string.Empty;
                _cmbGstType.SelectedItem = !string.IsNullOrWhiteSpace(vendor.GSTRegistrationType) ? vendor.GSTRegistrationType : "Regular";
                _chkTds.Checked = vendor.TDSApplicable;
                _cmbTdsSection.SelectedItem = !string.IsNullOrWhiteSpace(vendor.TDSSection) ? vendor.TDSSection : "194C";
                _numTdsRate.Value = Math.Min(_numTdsRate.Maximum, Math.Max(_numTdsRate.Minimum, vendor.TDSRate));
                _chkRcm.Checked = vendor.RCMApplicable;

                _numCreditDays.Value = Math.Min(_numCreditDays.Maximum, Math.Max(_numCreditDays.Minimum, vendor.DefaultCreditDays));
                _cmbPaymentMode.SelectedItem = !string.IsNullOrWhiteSpace(vendor.PreferredPaymentMode) ? vendor.PreferredPaymentMode : null;
                _txtBankAccount.Text = vendor.BankAccountNumber ?? string.Empty;
                _txtIfsc.Text = vendor.BankIFSC ?? string.Empty;
                _txtAccountName.Text = vendor.BankAccountName ?? string.Empty;
                _txtBankName.Text = vendor.BankName ?? string.Empty;
                _txtNotes.Text = vendor.Notes ?? string.Empty;

                RenderTags(ParseTags(vendor.SpecialisationTags));
                RenderRecentPurchaseOrders(vendor.RecentPOs, vendor.TotalPurchased);
                UpdateStats(vendor);
                UpdateWarnings(vendor);
                UpdateMsmeVisibility();
                UpdateGstinState();
                UpdatePanState();
                UpdateIfscState();
                UpdateGstTypeState();
                UpdateTdsVisibility();
                UpdateCreditVisuals();
                UpdateActionState();
            }
            finally
            {
                _isBinding = false;
                LayoutDetailCards();
            }
        }

        private void UpdateStats(VendorDetailDto vendor)
        {
            _lblStatOutstanding.Text = IndiaFormatHelper.FormatCurrency(vendor.OutstandingBalance);
            _lblStatOutstanding.ForeColor = vendor.OutstandingBalance > 0 ? RedDark : Teal;
            _lblStatPurchased.Text = IndiaFormatHelper.FormatCurrency(vendor.TotalPurchased);
            _lblStatPurchased.ForeColor = Blue;
            _lblStatOpenPos.Text = vendor.OpenPOCount.ToString();
            _lblStatOpenPos.ForeColor = vendor.OpenPOCount > 0 ? AmberDark : TextHint;
            _lblStatCreditDays.Text = vendor.DefaultCreditDays.ToString();
            _lblStatCreditDays.ForeColor = TextPrimary;
        }

        private void UpdateWarnings(Vendor vendor)
        {
            List<string> warnings = _vendorSvc.GetMissingFieldWarnings(vendor);
            if (warnings.Count == 0)
            {
                _warningStrip.Visible = false;
                return;
            }
            _lblWarningStrip.Text = string.Join(", ", warnings) + " are missing - add them for a complete vendor profile";
            _warningStrip.Visible = true;
        }

        private void RenderTags(List<string> tags)
        {
            _tagFlow.SuspendLayout();
            _tagFlow.Controls.Clear();
            foreach (string tag in tags.Take(8))
                _tagFlow.Controls.Add(CreateTagChip(tag));
            _tagFlow.Controls.Add(_lnkAddTag);
            _tagFlow.Controls.Add(_txtAddTag);
            _tagFlow.ResumeLayout();
        }

        private Control CreateTagChip(string tag)
        {
            Panel chip = new Panel { Height = 28, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = BlueLightBg, Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0, 0, 8, 8) };
            chip.Paint += (s, e) => DrawRoundedSurface(e.Graphics, chip.ClientRectangle, BlueLightBg, 6);
            Label lbl = new Label { AutoSize = true, Text = tag, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = BlueDark };
            LinkLabel lnk = new LinkLabel { AutoSize = true, Text = "x", LinkColor = RedDark, Margin = new Padding(6, 0, 0, 0) };
            lnk.Click += (s, e) =>
            {
                List<string> current = ParseTags(GetTagsText());
                current.RemoveAll(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
                RenderTags(current);
            };
            chip.Controls.Add(lbl);
            chip.Controls.Add(lnk);
            lnk.Location = new Point(lbl.Right + 4, 6);
            chip.SizeChanged += (s, e) => lnk.Location = new Point(lbl.Right + 4, 6);
            return chip;
        }

        private void ShowTagEditor()
        {
            _txtAddTag.Visible = true;
            _txtAddTag.Text = string.Empty;
            _txtAddTag.Focus();
        }

        private void CommitTagEditor()
        {
            if (!_txtAddTag.Visible)
                return;
            string tag = (_txtAddTag.Text ?? string.Empty).Trim();
            _txtAddTag.Visible = false;
            if (string.IsNullOrWhiteSpace(tag))
                return;
            List<string> current = ParseTags(GetTagsText());
            if (!current.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) && current.Count < 8)
                current.Add(tag);
            RenderTags(current);
        }

        private void TagEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitTagEditor();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _txtAddTag.Visible = false;
            }
        }

        private void RenderRecentPurchaseOrders(List<PurchaseOrder> orders, decimal totalPurchased)
        {
            decimal advanceBalance = 0m;
            if (_currentVendor != null && _currentVendor.VendorID > 0)
            {
                try { advanceBalance = _vendorAdvanceSvc.GetVendorAdvanceBalance(_currentVendor.VendorID); } catch { advanceBalance = 0m; }
            }
            _recentPoFlow.SuspendLayout();
            _recentPoFlow.Controls.Clear();
            if (orders == null || orders.Count == 0)
            {
                _recentPoFlow.Controls.Add(new Label { Text = "No purchase orders yet for this vendor", Width = Math.Max(600, _recentPoFlow.Width - 25), Height = 32, Font = new Font("Segoe UI", 10f), ForeColor = TextHint });
            }
            else
            {
                foreach (PurchaseOrder po in orders.OrderByDescending(p => p.PODate).Take(5))
                    _recentPoFlow.Controls.Add(BuildPurchaseOrderRow(po));
            }
            _lblRecentPoFooter.Text = "Total purchased   " + IndiaFormatHelper.FormatCurrency(totalPurchased)
                + "   |   Advance balance   " + IndiaFormatHelper.FormatCurrency(advanceBalance);
            _recentPoFlow.ResumeLayout();
        }

        private Control BuildPurchaseOrderRow(PurchaseOrder po)
        {
            Panel row = new Panel { Width = Math.Max(720, _recentPoFlow.Width - 25), Height = 52, BackColor = White, Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand };
            row.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(BorderLight))
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            };
            row.Click += (s, e) => ViewPurchaseOrders();
            LinkLabel lnkPo = new LinkLabel { Text = po.PONumber, Location = new Point(0, 2), Size = new Size(140, 18), LinkColor = Blue, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            lnkPo.Click += (s, e) => ViewPurchaseOrders();
            Label lblDate = new Label { Text = IndiaFormatHelper.FormatDate(po.PODate), Location = new Point(0, 24), Size = new Size(140, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint };
            Label lblLink = new Label { Text = string.IsNullOrWhiteSpace(po.LinkedToLabel) ? (po.LinkedToType ?? "No linked record") : po.LinkedToLabel, Location = new Point(150, 12), Size = new Size(260, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            Label lblAmount = new Label { Text = IndiaFormatHelper.FormatCurrency(po.TotalAmount), Location = new Point(row.Width - 180, 8), Size = new Size(100, 18), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.TopRight };
            Panel pill = CreatePoStatusPill(po);
            pill.Location = new Point(row.Width - pill.Width - 8, 28);
            row.Controls.Add(lnkPo);
            row.Controls.Add(lblDate);
            row.Controls.Add(lblLink);
            row.Controls.Add(lblAmount);
            row.Controls.Add(pill);
            return row;
        }

        private Panel CreatePoStatusPill(PurchaseOrder po)
        {
            string text = po.Status ?? "Pending";
            if (po.IsOverdue)
                text = "Overdue " + Math.Max(0, (DateTime.Today - po.PayByDate.Date).Days) + "d";
            Color bg = RedLightBg;
            Color fg = RedDark;
            if (string.Equals(po.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                bg = Color.FromArgb(234, 243, 222);
                fg = Color.FromArgb(39, 80, 10);
            }
            else if (string.Equals(po.Status, "Partial", StringComparison.OrdinalIgnoreCase))
            {
                bg = AmberLightBg;
                fg = Color.FromArgb(122, 77, 0);
            }
            int width = Math.Max(72, TextRenderer.MeasureText(text, new Font("Segoe UI", 8.5f, FontStyle.Bold)).Width + 16);
            Panel panel = new Panel { Size = new Size(width, 20), BackColor = bg };
            panel.Paint += (s, e) => DrawRoundedSurface(e.Graphics, panel.ClientRectangle, bg, 10);
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = fg, TextAlign = ContentAlignment.MiddleCenter });
            return panel;
        }

        private void UpdateMsmeVisibility()
        {
            bool showMsme = !string.Equals(Convert.ToString(_cmbMsme.SelectedItem), "No", StringComparison.OrdinalIgnoreCase);
            _txtMsmeNumber.Visible = showMsme;
            _lblMsmeNote.Visible = showMsme;
            UpdateCreditVisuals();
        }

        private void UpdateGstinState()
        {
            string stateCode;
            bool valid = _vendorSvc.ValidateGSTIN(_txtGstin.Text, out stateCode);
            if (string.IsNullOrWhiteSpace(_txtGstin.Text))
            {
                _lblGstinHint.Text = string.Empty;
                _lblGstinState.Text = string.Empty;
                return;
            }
            _lblGstinHint.Text = valid ? "Valid" : "Invalid format";
            _lblGstinHint.ForeColor = valid ? Teal : RedDark;
            _lblGstinState.Text = valid ? "State code " + stateCode + " - " + IndiaStateCatalog.NormalizeStateName(StateNameByCode(stateCode)) : string.Empty;
            if (valid)
                SetStateComboByCodeOrName(stateCode);
        }

        private void UpdatePanState()
        {
            if (string.IsNullOrWhiteSpace(_txtPan.Text))
            {
                _lblPanHint.Text = string.Empty;
                return;
            }
            bool valid = _vendorSvc.ValidatePAN(_txtPan.Text);
            _lblPanHint.Text = valid ? "Valid" : "Invalid format";
            _lblPanHint.ForeColor = valid ? Teal : RedDark;
        }

        private void UpdateIfscState()
        {
            if (string.IsNullOrWhiteSpace(_txtIfsc.Text))
            {
                _lblIfscHint.Text = string.Empty;
                return;
            }
            bool valid = _vendorSvc.ValidateIFSC(_txtIfsc.Text);
            _lblIfscHint.Text = valid ? "Valid" : "Invalid format";
            _lblIfscHint.ForeColor = valid ? Teal : RedDark;
        }

        private void UpdateGstTypeState()
        {
            bool unregistered = string.Equals(Convert.ToString(_cmbGstType.SelectedItem), "Unregistered", StringComparison.OrdinalIgnoreCase);
            if (unregistered)
                _chkRcm.Checked = true;
            _lblTaxNote.Text = unregistered ? "RCM applies - you pay GST on behalf" : string.Empty;
        }

        private void UpdateTdsVisibility()
        {
            bool visible = _chkTds.Checked;
            _cmbTdsSection.Visible = visible;
            _numTdsRate.Visible = visible;
            ApplyTdsDefaults();
        }

        private void ApplyTdsDefaults()
        {
            if (!_chkTds.Checked)
                return;
            decimal rate;
            if (_tdsRates.TryGetValue(Convert.ToString(_cmbTdsSection.SelectedItem), out rate))
                _numTdsRate.Value = Math.Min(_numTdsRate.Maximum, rate);
        }

        private void UpdateCreditVisuals()
        {
            int width = (int)Math.Min(_creditTrack.Width, (_numCreditDays.Value / 90m) * _creditTrack.Width);
            _creditFill.Width = Math.Max(0, width);
            _lblMsmeWarning.Visible = !string.Equals(Convert.ToString(_cmbMsme.SelectedItem), "No", StringComparison.OrdinalIgnoreCase) && _numCreditDays.Value > 45;
            _lblMsmeWarning.Text = "MSME vendors: max 45 days under MSME Act";
        }

        private void UpdateActionState()
        {
            bool hasVendor = _currentVendor != null && _currentVendor.VendorID > 0;
            _btnRaisePo.Enabled = hasVendor && !_isNewMode;
            _btnViewPos.Enabled = hasVendor && !_isNewMode;
            _btnArchive.Enabled = hasVendor && !_isNewMode && !_currentVendor.IsArchived;
        }

        private async Task SaveVendorAsync(bool showMessages)
        {
            try
            {
                VendorDetailDto vendor = BuildVendorFromForm();
                List<string> validation = ValidateVendor(vendor);
                if (validation.Count > 0)
                {
                    if (showMessages)
                        MessageBox.Show("Please fill: " + string.Join(", ", validation), "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_isNewMode)
                {
                    int newId = await Task.Run(() => _vendorSvc.Create(vendor));
                    await RefreshAsync(false);
                    await LoadVendorDetailAsync(newId);
                }
                else
                {
                    vendor.VendorID = _currentVendor.VendorID;
                    vendor.CreatedDate = _currentVendor.CreatedDate;
                    await Task.Run(() => _vendorSvc.Update(vendor));
                    await RefreshAsync(true);
                }
                if (showMessages)
                    MessageBox.Show("Vendor saved.", "Vendors", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.SaveVendorAsync", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Saving vendor", ex);
            }
        }

        private async Task AutoSaveNotesAsync()
        {
            if (_isBinding || _isNewMode || _currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            try
            {
                VendorDetailDto vendor = BuildVendorFromForm();
                vendor.VendorID = _currentVendor.VendorID;
                vendor.CreatedDate = _currentVendor.CreatedDate;
                await Task.Run(() => _vendorSvc.Update(vendor));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.AutoSaveNotesAsync", ex);
            }
        }

        private async Task ArchiveVendorAsync()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            DialogResult result = MessageBox.Show("Archive " + _currentVendor.VendorName + "?", "Archive vendor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
                return;
            try
            {
                await Task.Run(() => _vendorSvc.ArchiveVendor(_currentVendor.VendorID));
                await RefreshAsync(false);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.ArchiveVendorAsync", ex);
                MessageBox.Show(ex.Message, "Archive blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RaisePo()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            _vendorSvc.RaiseQuickPO(_currentVendor.VendorID);
        }

        private void ViewPurchaseOrders()
        {
            if (_currentVendor == null || _currentVendor.VendorID <= 0)
                return;
            _vendorSvc.RaiseQuickPO(_currentVendor.VendorID);
        }

        private void ShowMergeDialog()
        {
            if (_duplicateGroups.Count == 0)
                return;
            using (MergeDuplicatesDialog dlg = new MergeDuplicatesDialog(_duplicateGroups))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.MergeSelections.Count > 0)
                {
                    try
                    {
                        foreach (KeyValuePair<int, List<int>> item in dlg.MergeSelections)
                            _vendorSvc.MergeDuplicates(item.Key, item.Value);
                        Task.Run(async () =>
                        {
                            await Task.Delay(100);
                            BeginInvoke((Action)(async () => await RefreshAsync(true)));
                        });
                    }
                    catch (Exception ex)
                    {
                        AppRuntime.LogException("VendorForm.ShowMergeDialog", ex);
                        AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Merging vendors", ex);
                    }
                }
            }
        }

        private void ExportCsv()
        {
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "CSV files (*.csv)|*.csv";
                    dialog.FileName = "Vendors_" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("VendorName,Category,VendorType,City,State,Phone,Email,GSTIN,PAN,CreditDays,OutstandingBalance,TotalPurchased,OpenPOs,IsActive,MSMERegistered,Tags");
                    foreach (VendorSummaryDto summary in _vendorSummaries.OrderBy(v => v.VendorName))
                    {
                        VendorDetailDto detail = _vendorSvc.GetVendorDetail(summary.VendorId);
                        sb.AppendLine(string.Join(",", Csv(detail.VendorName), Csv(detail.Category), Csv(detail.VendorType), Csv(detail.City), Csv(StateNameByCode(detail.StateCode)), Csv(detail.Phone), Csv(detail.Email), Csv(detail.GSTNumber), Csv(detail.PANNumber), Csv(detail.DefaultCreditDays.ToString()), Csv(IndiaFormatHelper.FormatCurrency(detail.OutstandingBalance)), Csv(IndiaFormatHelper.FormatCurrency(detail.TotalPurchased)), Csv(detail.OpenPOCount.ToString()), Csv(detail.IsActive ? "Yes" : "No"), Csv(detail.MSMERegistered), Csv(detail.SpecialisationTags)));
                    }
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    Process.Start(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("VendorForm.ExportCsv", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Vendors"), "Export vendors", ex);
            }
        }

        private VendorDetailDto BuildVendorFromForm()
        {
            VendorDetailDto vendor = _currentVendor ?? new VendorDetailDto();
            vendor.VendorName = (_txtVendorName.Text ?? string.Empty).Trim();
            vendor.Category = Convert.ToString(_cmbCategory.SelectedItem);
            vendor.SpecialisationTags = GetTagsText();
            vendor.VendorType = Convert.ToString(_cmbVendorType.SelectedItem);
            vendor.MSMERegistered = Convert.ToString(_cmbMsme.SelectedItem);
            vendor.MSMENumber = (_txtMsmeNumber.Text ?? string.Empty).Trim();
            vendor.Phone = (_txtPhone.Text ?? string.Empty).Trim();
            vendor.WhatsAppNumber = (_txtWhatsApp.Text ?? string.Empty).Trim();
            vendor.Email = (_txtEmail.Text ?? string.Empty).Trim();
            vendor.Address = (_txtAddress.Text ?? string.Empty).Trim();
            vendor.City = (_txtCity.Text ?? string.Empty).Trim();
            vendor.StateCode = IndiaStateCatalog.GetCodeByName(Convert.ToString(_cmbState.SelectedItem));
            vendor.GSTNumber = (_txtGstin.Text ?? string.Empty).Trim().ToUpperInvariant();
            vendor.PANNumber = (_txtPan.Text ?? string.Empty).Trim().ToUpperInvariant();
            vendor.GSTRegistrationType = Convert.ToString(_cmbGstType.SelectedItem);
            vendor.TDSApplicable = _chkTds.Checked;
            vendor.TDSSection = vendor.TDSApplicable ? Convert.ToString(_cmbTdsSection.SelectedItem) : null;
            vendor.TDSRate = vendor.TDSApplicable ? _numTdsRate.Value : 0m;
            vendor.RCMApplicable = _chkRcm.Checked;
            vendor.DefaultCreditDays = (int)_numCreditDays.Value;
            vendor.PreferredPaymentMode = Convert.ToString(_cmbPaymentMode.SelectedItem);
            vendor.BankAccountNumber = (_txtBankAccount.Text ?? string.Empty).Trim();
            vendor.BankIFSC = (_txtIfsc.Text ?? string.Empty).Trim().ToUpperInvariant();
            vendor.BankAccountName = (_txtAccountName.Text ?? string.Empty).Trim();
            vendor.BankName = (_txtBankName.Text ?? string.Empty).Trim();
            vendor.Notes = GetControlText(_txtNotes);
            vendor.IsActive = true;
            vendor.IsArchived = _currentVendor != null && _currentVendor.IsArchived;
            vendor.TotalPurchased = _currentVendor?.TotalPurchased ?? 0m;
            vendor.CreatedDate = _currentVendor?.CreatedDate ?? DateTime.Now;
            _vendorSvc.OnGSTINChanged(vendor);
            return vendor;
        }

        private List<string> ValidateVendor(Vendor vendor)
        {
            ClearErrors();
            List<string> issues = new List<string>();
            if (string.IsNullOrWhiteSpace(vendor.VendorName))
            {
                issues.Add("Vendor name");
                _errors.SetError(_txtVendorName, "Vendor name is required.");
            }
            if (!string.IsNullOrWhiteSpace(vendor.GSTNumber) && !_vendorSvc.ValidateGSTIN(vendor.GSTNumber))
            {
                issues.Add("valid GSTIN");
                _errors.SetError(_txtGstin, "GSTIN format is invalid.");
            }
            if (!string.IsNullOrWhiteSpace(vendor.BankIFSC) && !_vendorSvc.ValidateIFSC(vendor.BankIFSC))
            {
                issues.Add("valid IFSC");
                _errors.SetError(_txtIfsc, "IFSC format is invalid.");
            }
            if (!string.IsNullOrWhiteSpace(vendor.PANNumber) && !_vendorSvc.ValidatePAN(vendor.PANNumber))
            {
                issues.Add("valid PAN");
                _errors.SetError(_txtPan, "PAN format is invalid.");
            }
            return issues;
        }

        private void ClearErrors()
        {
            _errors.Clear();
        }

        private void LayoutDetailCards()
        {
            if (_rightHost == null)
                return;
            int width = Math.Max(920, _rightScroll.ClientSize.Width - 24);
            int y = 0;
            int gap = 14;
            int fullWidth = Math.Max(920, width - 2);
            _vendorHeaderRow.Location = new Point(0, y);
            _vendorHeaderRow.Width = fullWidth;
            y += _vendorHeaderRow.Height + gap;
            _statsRow.Location = new Point(0, y);
            _statsRow.Width = fullWidth;
            int statWidth = (fullWidth - 36) / 4;
            for (int i = 0; i < _statsRow.Controls.Count; i++)
                _statsRow.Controls[i].SetBounds(i * (statWidth + 12), 0, statWidth, 70);
            y += _statsRow.Height + gap;
            _warningStrip.Location = new Point(0, y);
            _warningStrip.Width = fullWidth;
            if (_warningStrip.Visible)
                y += _warningStrip.Height + gap;

            _cardIdentity.SetBounds(0, y, fullWidth, _cardIdentity.Height);
            y += _cardIdentity.Height + gap;
            _cardRecentPos.SetBounds(0, y, fullWidth, _cardRecentPos.Height);
            _recentPoFlow.Width = _cardRecentPos.ContentPanel.ClientSize.Width - 4;
            _lblRecentPoFooter.Width = _cardRecentPos.ContentPanel.ClientSize.Width - 4;
            y += _cardRecentPos.Height + gap;
            _cardNotes.SetBounds(0, y, fullWidth, _cardNotes.Height);
            y += _cardNotes.Height + gap;
            _rightHost.Size = new Size(fullWidth, y + 4);
        }

        private static Label CreateBadgeLabel(int width, Color bg, Color fg)
        {
            Label label = new Label { AutoSize = false, Width = width, Height = 22, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 2, 8, 0) };
            label.Paint += (s, e) => DrawRoundedSurface(e.Graphics, label.ClientRectangle, bg, 11);
            return label;
        }

        private Button MakeActionButton(string text, Color bg, Color fg, int width, bool outline)
        {
            Button button = new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 0, 8, 0) };
            button.FlatAppearance.BorderSize = outline ? 1 : 0;
            button.FlatAppearance.BorderColor = outline ? Border : bg;
            button.FlatAppearance.MouseOverBackColor = outline ? Surface : ControlPaint.Dark(bg);
            return button;
        }

        private static void DrawRoundedSurface(Graphics graphics, Rectangle rect, Color fill, int radius)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle drawRect = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (GraphicsPath path = GetRoundedRect(drawRect, radius))
            using (SolidBrush brush = new SolidBrush(fill))
                graphics.FillPath(brush, path);
        }

        private static void DrawSearchBox(Graphics graphics, Rectangle rect)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle drawRect = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (GraphicsPath path = GetRoundedRect(drawRect, 8))
            using (SolidBrush brush = new SolidBrush(White))
            using (Pen pen = new Pen(Border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static void DrawCardOutline(Graphics graphics, Rectangle rect)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle drawRect = new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width - 1), Math.Max(1, rect.Height - 1));
            using (GraphicsPath path = GetRoundedRect(drawRect, 10))
            using (SolidBrush brush = new SolidBrush(White))
            using (Pen pen = new Pen(Border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ConfigurePlaceholder(TextBox box, string placeholder)
        {
            box.GotFocus += (s, e) =>
            {
                if (box.ForeColor == TextHint && box.Text == placeholder)
                {
                    box.Text = string.Empty;
                    box.ForeColor = TextPrimary;
                    if (box == _txtSearch) _searchPlaceholderActive = false;
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = placeholder;
                    box.ForeColor = TextHint;
                    if (box == _txtSearch) _searchPlaceholderActive = true;
                }
            };
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = placeholder;
                box.ForeColor = TextHint;
                if (box == _txtSearch) _searchPlaceholderActive = true;
            }
        }

        private string GetSearchText() => _searchPlaceholderActive ? string.Empty : _txtSearch.Text;
        private void UpdateSearchClear() { _btnClearSearch.Visible = !string.IsNullOrWhiteSpace(GetSearchText()); }
        private static string JoinBullet(params string[] parts) { return string.Join(" - ", parts.Where(p => !string.IsNullOrWhiteSpace(p))); }
        private static string Csv(string value) { string safe = (value ?? string.Empty).Replace("\"", "\"\""); return "\"" + safe + "\""; }

        private static Label PlaceLabel(Control parent, string text, int x, int y)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(180, 16), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextSecondary };
            parent.Controls.Add(label);
            return label;
        }

        private static TextBox PlaceTextBox(Control parent, int x, int y, int width)
        {
            TextBox box = new TextBox { Location = new Point(x, y), Size = new Size(width, 24), Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };
            parent.Controls.Add(box);
            return box;
        }

        private static ComboBox PlaceComboBox(Control parent, int x, int y, int width, List<string> source)
        {
            ComboBox combo = new ComboBox { Location = new Point(x, y), Size = new Size(width, 24), Font = new Font("Segoe UI", 9f), DropDownStyle = ComboBoxStyle.DropDownList };
            if (source != null) combo.Items.AddRange(source.Cast<object>().ToArray());
            parent.Controls.Add(combo);
            return combo;
        }

        private void SetStateComboByCodeOrName(string stateCodeOrName)
        {
            string name = StateNameByCode(stateCodeOrName);
            if (string.IsNullOrWhiteSpace(name))
                name = IndiaStateCatalog.NormalizeStateName(stateCodeOrName);
            if (!string.IsNullOrWhiteSpace(name) && _cmbState.Items.Contains(name))
                _cmbState.SelectedItem = name;
            else if (_cmbState.Items.Count > 0)
                _cmbState.SelectedIndex = 0;
        }

        private string StateNameByCode(string stateCode)
        {
            if (string.IsNullOrWhiteSpace(stateCode))
                return string.Empty;
            foreach (string name in IndiaStateCatalog.Names)
            {
                if (string.Equals(IndiaStateCatalog.GetCodeByName(name), stateCode.Trim(), StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return string.Empty;
        }

        private List<string> ParseTags(string csv)
        {
            return (csv ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        }

        private string GetTagsText()
        {
            return string.Join(",", _tagFlow.Controls.OfType<Panel>().Select(p => p.Controls.OfType<Label>().FirstOrDefault()).Where(l => l != null).Select(l => l.Text.Trim()));
        }

        private static string GetControlText(TextBox textBox)
        {
            return textBox.ForeColor == TextHint ? string.Empty : textBox.Text;
        }

        private sealed class MergeDuplicatesDialog : Form
        {
            public Dictionary<int, List<int>> MergeSelections { get; } = new Dictionary<int, List<int>>();

            public MergeDuplicatesDialog(List<DuplicateGroupDto> groups)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Merge duplicate vendors";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                Width = 560;
                Height = 480;
                BackColor = White;

                Label subtitle = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 12, 16, 8), Text = "Select the master vendor to keep. All POs from duplicates will move to master.", Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary };
                FlowLayoutPanel body = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = White, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 8, 16, 16) };
                foreach (DuplicateGroupDto group in groups)
                    body.Controls.Add(BuildGroup(group));
                Button btnClose = new Button { Text = "Done", Dock = DockStyle.Bottom, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Teal, ForeColor = White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => DialogResult = DialogResult.OK;
                Controls.Add(body);
                Controls.Add(btnClose);
                Controls.Add(subtitle);
            }

            private Control BuildGroup(DuplicateGroupDto group)
            {
                Panel panel = new Panel { Width = 500, Height = 38 + (group.Vendors.Count * 32) + 42, BackColor = White, Margin = new Padding(0, 0, 0, 12) };
                panel.Paint += (s, e) => DrawCardOutline(e.Graphics, panel.ClientRectangle);
                Label title = new Label { Text = group.NormalisedName + " (" + group.Vendors.Count + ")", Location = new Point(12, 10), Size = new Size(320, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary };
                panel.Controls.Add(title);
                List<RadioButton> radios = new List<RadioButton>();
                int y = 38;
                foreach (DuplicateVendorItemDto vendor in group.Vendors)
                {
                    RadioButton radio = new RadioButton { Text = vendor.VendorName + " - " + vendor.OpenPOCount + " POs - " + IndiaFormatHelper.FormatCurrency(vendor.OutstandingBalance), Location = new Point(16, y), Size = new Size(460, 20), Font = new Font("Segoe UI", 9f), Tag = vendor.VendorId };
                    if (radios.Count == 0) radio.Checked = true;
                    radios.Add(radio);
                    panel.Controls.Add(radio);
                    y += 28;
                }
                Button btnMerge = new Button { Text = "Merge this group", Location = new Point(16, y + 2), Size = new Size(128, 28), FlatStyle = FlatStyle.Flat, BackColor = White, ForeColor = TextPrimary, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
                btnMerge.FlatAppearance.BorderSize = 1;
                btnMerge.FlatAppearance.BorderColor = Border;
                btnMerge.Click += (s, e) =>
                {
                    RadioButton master = radios.FirstOrDefault(r => r.Checked);
                    if (master == null) return;
                    int masterId = Convert.ToInt32(master.Tag);
                    List<int> duplicateIds = radios.Select(r => Convert.ToInt32(r.Tag)).Where(id => id != masterId).ToList();
                    string masterName = group.Vendors.First(v => v.VendorId == masterId).VendorName;
                    DialogResult result = MessageBox.Show("Merge " + group.Vendors.Count + " vendors into " + masterName + "? " + duplicateIds.Count + " purchase orders will be reassigned. Duplicate records will be archived.", "Confirm merge", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;
                    MergeSelections[masterId] = duplicateIds;
                    btnMerge.Text = "Queued";
                    btnMerge.Enabled = false;
                };
                panel.Controls.Add(btnMerge);
                return panel;
            }
        }
    }
}

