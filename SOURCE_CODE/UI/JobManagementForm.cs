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
    public class JobManagementForm : DeferredPageControl
    {
        private static readonly Color White = ColorTranslator.FromHtml("#FFFFFF");
        private static readonly Color PageBg = ColorTranslator.FromHtml("#F9F9F9");
        private static readonly Color Surface = ColorTranslator.FromHtml("#F7F7F7");
        private static readonly Color Border = ColorTranslator.FromHtml("#E8E8E8");
        private static readonly Color BorderLight = ColorTranslator.FromHtml("#F0F0F0");
        private static readonly Color TextPrimary = ColorTranslator.FromHtml("#1A1A1A");
        private static readonly Color TextSecondary = ColorTranslator.FromHtml("#6B6B6B");
        private static readonly Color TextHint = ColorTranslator.FromHtml("#9E9E9E");
        private static readonly Color Teal = ColorTranslator.FromHtml("#1D9E75");
        private static readonly Color TealDark = ColorTranslator.FromHtml("#0F6E56");
        private static readonly Color TealLightBg = ColorTranslator.FromHtml("#F0FAF5");
        private static readonly Color Amber = ColorTranslator.FromHtml("#EF9F27");
        private static readonly Color AmberDark = ColorTranslator.FromHtml("#BA7517");
        private static readonly Color AmberLightBg = ColorTranslator.FromHtml("#FFF8EC");
        private static readonly Color Red = ColorTranslator.FromHtml("#E24B4A");
        private static readonly Color RedDark = ColorTranslator.FromHtml("#A32D2D");
        private static readonly Color RedLightBg = ColorTranslator.FromHtml("#FFEAEA");
        private static readonly Color Blue = ColorTranslator.FromHtml("#185FA5");
        private static readonly Color BlueDark = ColorTranslator.FromHtml("#0C447C");
        private static readonly Color BlueLightBg = ColorTranslator.FromHtml("#F0F4FF");
        private static readonly Color Purple = ColorTranslator.FromHtml("#534AB7");
        private static readonly Color PurpleLight = ColorTranslator.FromHtml("#EEEDFE");

        private readonly JobService _jobSvc = new JobService();
        private readonly ClientService _clientSvc = new ClientService();
        private readonly SiteService _siteSvc = new SiteService();
        private readonly EmployeeService _employeeSvc = new EmployeeService();
        private readonly ContractService _contractSvc = new ContractService();
        private readonly InventoryService _inventorySvc = new InventoryService();
        private readonly SettingsService _settingsSvc = new SettingsService();

        private SplitContainer _split;
        private FlowLayoutPanel _jobListFlow;
        private FlowLayoutPanel _chipFlow;
        private TextBox _txtSearch;
        private Button _btnSearchClear;
        private Label _lblListStatus;

        private Panel _rightPanel;
        private Panel _topBar;
        private Panel _pipelineBar;
        private Panel _detailScroll;
        private Panel _cardsHost;

        private Label _lblJobNumber;
        private Label _lblJobTitle;
        private Label _lblMeta;
        private Button _btnCloseJob;
        private Button _btnPrintReport;
        private Button _btnSave;

        private readonly Dictionary<string, Panel> _pipelineStepPanels = new Dictionary<string, Panel>();
        private readonly List<string> _pipelineSteps = new List<string> { "Created", "Assigned", "InProgress", "ChecklistDone", "Closed", "Invoiced" };

        private Panel _cardJobDetails;
        private Panel _cardTechnician;
        private Panel _cardCost;
        private Panel _cardChecklist;
        private Panel _cardParts;
        private Panel _cardNudges;
        private Panel _cardActivity;
        private Panel _cardNotes;

        private TextBox _txtJobNo;
        private TextBox _txtJobTitle;
        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private ComboBox _cmbJobType;
        private ComboBox _cmbContract;
        private DateTimePicker _dtpScheduled;

        private ComboBox _cmbTechnician;
        private Label _lblTechAvatar;
        private Label _lblTechName;
        private Label _lblTechLoad;
        private Panel _techLoadBarFill;
        private ComboBox _cmbPriority;
        private ComboBox _cmbStatus;

        private Label _lblQuotedRevenue;
        private Label _lblLabourCost;
        private Label _lblPartsCost;
        private Label _lblTravelCost;
        private Label _lblProfit;
        private Label _lblMarginValue;
        private Panel _marginFill;
        private TextBox _txtQuotedRevenue;
        private TextBox _txtEstimatedLabour;

        private Label _lblChecklistCount;
        private FlowLayoutPanel _checklistFlow;
        private Panel _checklistAddPanel;
        private TextBox _txtNewChecklistItem;
        private Label _lblChecklistBanner;

        private FlowLayoutPanel _partsFlow;
        private Label _lblPartsTotal;
        private ComboBox _cmbPartSearch;
        private NumericUpDown _numPartQty;
        private Label _lblPartStockHint;
        private Panel _partsAddPanel;

        private FlowLayoutPanel _nudgesFlow;
        private FlowLayoutPanel _activityFlow;
        private LinkLabel _lnkViewAllActivity;
        private TextBox _txtNotes;

        private List<JobSummaryDto> _allJobs = new List<JobSummaryDto>();
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<ClientSite> _sitesForClient = new List<ClientSite>();
        private List<AMCContract> _contractsForClient = new List<AMCContract>();
        private List<Employee> _technicians = new List<Employee>();
        private List<StockItem> _inventory = new List<StockItem>();
        private JobDetailDto _currentDetail;
        private string _activeFilter = "All";
        private bool _isBinding;
        private bool _isNewMode = true;
        private bool _settingPlaceholder;
        private readonly Dictionary<Control, int> _cardExpandedHeights = new Dictionary<Control, int>();
        private readonly Dictionary<Control, int> _cardDefaultHeights = new Dictionary<Control, int>();
        private int _selectedJobId;

        public Action<int> OnOpenJobDetail { get; set; }

        private sealed class JobLoadSnapshot
        {
            public List<JobSummaryDto> Jobs { get; set; } = new List<JobSummaryDto>();
            public List<B2BClient> Clients { get; set; } = new List<B2BClient>();
            public List<Employee> Technicians { get; set; } = new List<Employee>();
            public List<StockItem> Inventory { get; set; } = new List<StockItem>();
            public bool TimedOut { get; set; }
        }

        public JobManagementForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            ShowJobEditor();
            RenderChecklistPreview(new List<JobChecklistItem>());
            RenderParts(new List<JobPartUsed>());
            RenderNudges(new List<NudgeDto>());
            RenderActivity(new List<JobActivityEntry>());
            RefreshHeader(null);
            RefreshCostPreview();
            UpdatePipelineBar("Created");
            LayoutCards();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad((Func<Task>)(async () => await LoadInitialAsync()), ex => AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Jobs screen", ex));
        }

        private void BuildLayout()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 1,
                BackColor = Border
            };

            BuildLeftPanel();
            BuildRightPanel();

            Controls.Add(_split);
            Resize += (s, e) =>
            {
                AdjustResponsiveLayout();
                LayoutCards();
            };
            if (IsHandleCreated)
                BeginInvoke((Action)(() =>
                {
                    AdjustResponsiveLayout();
                    LayoutCards();
                }));
        }

        private void AdjustResponsiveLayout()
        {
            if (_split == null)
                return;

            int width = _split.Width > 0 ? _split.Width : ClientSize.Width;
            if (width <= _split.SplitterWidth + 120)
                return;

            bool compact = width < 1180;
            int panel1Min = compact ? 200 : 300;
            int panel2Min = compact ? 360 : 560;
            int availableForPanels = width - _split.SplitterWidth - 4;

            if (panel1Min + panel2Min > availableForPanels)
            {
                panel1Min = Math.Max(160, Math.Min(panel1Min, availableForPanels / 3));
                panel2Min = Math.Max(220, availableForPanels - panel1Min);
            }

            panel1Min = Math.Max(0, Math.Min(panel1Min, availableForPanels - 1));
            panel2Min = Math.Max(0, Math.Min(panel2Min, availableForPanels - panel1Min));
            _split.Panel1MinSize = panel1Min;
            _split.Panel2MinSize = panel2Min;

            int desiredLeft = compact ? 250 : 380;
            int minLeft = _split.Panel1MinSize;
            int maxLeft = width - _split.Panel2MinSize - _split.SplitterWidth;
            if (maxLeft < minLeft)
                return;

            _split.SplitterDistance = Math.Max(minLeft, Math.Min(desiredLeft, maxLeft));
        }

        private void BuildLeftPanel()
        {
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = White };
            left.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, left.Width - 1, 0, left.Width - 1, left.Height);
            };

            Panel header = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = White, Padding = new Padding(14, 10, 14, 10) };
            header.Controls.Add(new Label
            {
                Text = "Jobs",
                Location = new Point(14, 10),
                Size = new Size(260, 28),
                Font = new Font("Segoe UI", 15f, FontStyle.Regular),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            });
            FlowLayoutPanel leftActions = new FlowLayoutPanel
            {
                Location = new Point(14, 50),
                Size = new Size(340, 58),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                BackColor = White,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button btnTemplate = MakeHeaderButton("Template", Blue, White, 82);
            btnTemplate.Click += (s, e) => ImportUiHelper.DownloadTemplate(ExcelImportModule.Jobs, FindForm());
            Button btnImport = MakeHeaderButton("Import", Amber, White, 74);
            btnImport.Click += (s, e) => ImportUiHelper.RunImport(ExcelImportModule.Jobs, FindForm());
            Button btnNew = MakeHeaderButton("+ New Job", Teal, White, 104);
            btnNew.Click += async (s, e) => await BeginNewJobAsync();
            leftActions.Controls.Add(btnTemplate);
            leftActions.Controls.Add(btnImport);
            leftActions.Controls.Add(btnNew);
            header.Controls.Add(leftActions);

            Panel searchWrap = new Panel { Dock = DockStyle.Top, Height = 58, Padding = new Padding(16, 8, 16, 8), BackColor = White };
            Panel searchBox = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(12, 8, 8, 8) };
            searchBox.Paint += (s, e) => DrawRoundedBorder(e.Graphics, searchBox.ClientRectangle, Surface, Surface, 6);
            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Surface,
                ForeColor = TextPrimary
            };
            ConfigurePlaceholder(_txtSearch, "Search jobs, clients, technicians...");
            _txtSearch.TextChanged += (s, e) => { UpdateSearchClear(); ApplyFilters(); };
            _btnSearchClear = new Button
            {
                Dock = DockStyle.Right,
                Width = 24,
                FlatStyle = FlatStyle.Flat,
                Text = "x",
                Font = new Font("Segoe UI", 11f),
                ForeColor = TextHint,
                BackColor = Surface,
                Visible = false
            };
            _btnSearchClear.FlatAppearance.BorderSize = 0;
            _btnSearchClear.Click += (s, e) => { _txtSearch.Text = string.Empty; ApplyFilters(); };
            searchBox.Controls.Add(_txtSearch);
            searchBox.Controls.Add(_btnSearchClear);
            searchWrap.Controls.Add(searchBox);

            Panel chipWrap = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(16, 0, 16, 6), BackColor = White };
            _chipFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = White, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };
            chipWrap.Controls.Add(_chipFlow);

            Panel statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = White, Padding = new Padding(16, 0, 16, 8) };
            _lblListStatus = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = TextHint, Text = "Loading..." };
            statusPanel.Controls.Add(_lblListStatus);

            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(8, 0, 8, 8) };
            _jobListFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = White,
                Padding = new Padding(8, 4, 8, 8)
            };
            listWrap.Controls.Add(_jobListFlow);

            left.Controls.Add(listWrap);
            left.Controls.Add(statusPanel);
            left.Controls.Add(chipWrap);
            left.Controls.Add(searchWrap);
            left.Controls.Add(header);
            _split.Panel1.Controls.Add(left);
        }

        private void BuildRightPanel()
        {
            _rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = PageBg };

            _topBar = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = White, Padding = new Padding(20, 10, 20, 10) };
            BuildTopBar();

            _pipelineBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = White, Padding = new Padding(20, 8, 20, 8) };
            BuildPipelineBar();

            _detailScroll = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, AutoScroll = true, Padding = new Padding(20, 16, 20, 20) };
            _cardsHost = new Panel { BackColor = PageBg, Location = new Point(0, 0), Size = new Size(900, 1200) };
            _detailScroll.Controls.Add(_cardsHost);
            _detailScroll.Resize += (s, e) => LayoutCards();

            BuildCards();

            _rightPanel.Controls.Add(_detailScroll);
            _rightPanel.Controls.Add(_pipelineBar);
            _rightPanel.Controls.Add(_topBar);
            _rightPanel.Visible = false;
            _split.Panel2.Controls.Add(_rightPanel);
        }

        private void BuildTopBar()
        {
            Panel textWrap = new Panel { Dock = DockStyle.Fill, BackColor = White };
            _lblJobNumber = new Label { Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 9f), ForeColor = TextHint, Text = "JOB" };
            _lblJobTitle = new Label { Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 16f, FontStyle.Regular), ForeColor = TextPrimary, Text = "New job" };
            _lblMeta = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, Text = "Status - Type - Technician - Date - Priority" };
            textWrap.Controls.Add(_lblMeta);
            textWrap.Controls.Add(_lblJobTitle);
            textWrap.Controls.Add(_lblJobNumber);

            FlowLayoutPanel actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 380,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = White,
                Padding = new Padding(0, 4, 0, 0)
            };

            _btnSave = MakeHeaderButton("Save", Teal, White, 86);
            _btnSave.Click += async (s, e) => await SaveAsync();
            _btnPrintReport = MakeHeaderButton("Print Report", Blue, White, 108);
            _btnPrintReport.Click += (s, e) => PrintReport();
            _btnCloseJob = MakeHeaderButton("Close Job", Red, White, 96);
            _btnCloseJob.Click += async (s, e) => await CloseJobAsync();
            actionFlow.Controls.Add(_btnSave);
            actionFlow.Controls.Add(_btnPrintReport);
            actionFlow.Controls.Add(_btnCloseJob);

            _topBar.Controls.Add(textWrap);
            _topBar.Controls.Add(actionFlow);
        }

        private void BuildPipelineBar()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = White
            };

            for (int i = 0; i < _pipelineSteps.Count; i++)
            {
                string step = _pipelineSteps[i];
                int stepWidth = Math.Max(120, TextRenderer.MeasureText(GetPipelineLabel(step), new Font("Segoe UI", 8.5f, FontStyle.Bold)).Width + 36);
                Panel stepPanel = new Panel { MinimumSize = new Size(stepWidth, 30), Width = stepWidth, Height = 30, BackColor = White, Cursor = Cursors.Hand, Tag = step, Margin = new Padding(0) };
                stepPanel.Paint += (s, e) => DrawPipelineStep(e.Graphics, stepPanel, step);
                stepPanel.Click += async (s, e) => await HandlePipelineStepClickAsync((string)stepPanel.Tag);
                foreach (Control child in stepPanel.Controls)
                    child.Click += async (s, e) => await HandlePipelineStepClickAsync(step);
                _pipelineStepPanels[step] = stepPanel;
                flow.Controls.Add(stepPanel);

                if (i < _pipelineSteps.Count - 1)
                {
                    flow.Controls.Add(new Label
                    {
                        Text = ">",
                        AutoSize = false,
                        Width = 20,
                        Height = 30,
                        Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                        ForeColor = TextHint,
                        TextAlign = ContentAlignment.MiddleCenter
                    });
                }
            }

            _pipelineBar.Controls.Add(flow);
        }

        private void BuildCards()
        {
            _cardJobDetails = CreateCard("Job details", out Panel jobDetailsBody);
            _cardTechnician = CreateCard("Technician", out Panel technicianBody);
            _cardCost = CreateCard("Cost vs revenue", out Panel costBody);
            _cardChecklist = CreateCard("Job checklist", out Panel checklistBody, out _lblChecklistCount);
            _cardParts = CreateCard("Parts used", out Panel partsBody);
            _cardNudges = CreateCard("Smart nudges", out Panel nudgesBody);
            _cardActivity = CreateCard("Activity log", out Panel activityBody);
            _cardNotes = CreateCard("Notes", out Panel notesBody);

            BuildJobDetailsCard(jobDetailsBody);
            BuildTechnicianCard(technicianBody);
            BuildCostCard(costBody);
            BuildChecklistCard(checklistBody);
            BuildPartsCard(partsBody);
            BuildNudgesCard(nudgesBody);
            BuildActivityCard(activityBody);
            BuildNotesCard(notesBody);

            _cardsHost.Controls.AddRange(new Control[]
            {
                _cardJobDetails, _cardTechnician, _cardCost,
                _cardChecklist, _cardParts,
                _cardNudges, _cardActivity,
                _cardNotes
            });

        }

        private void BuildJobDetailsCard(Panel body)
        {
            body.AutoScroll = true;
            body.Controls.Add(BuildFormRow("Client", out _cmbClient));
            body.Controls.Add(BuildFormRow("Site", out _cmbSite));
            body.Controls.Add(BuildFormRow("Job type", out _cmbJobType));
            body.Controls.Add(BuildFormRow("Linked contract", out _cmbContract));
            body.Controls.Add(BuildDateRow("Scheduled date", out _dtpScheduled));
            body.Controls.Add(BuildTextRow("Job number", out _txtJobNo, true));
            body.Controls.Add(BuildTextRow("Job title", out _txtJobTitle, false));

            _cmbClient.SelectedIndexChanged += async (s, e) =>
            {
                if (_isBinding) return;
                await LoadSitesAndContractsAsync();
            };
            _cmbJobType.SelectedIndexChanged += async (s, e) =>
            {
                if (_isBinding) return;
                await HandleJobTypeChangedAsync();
            };
        }

        private void BuildTechnicianCard(Panel body)
        {
            body.AutoScroll = true;
            Panel summary = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = White };
            _lblTechAvatar = new Label
            {
                Location = new Point(0, 2),
                Size = new Size(42, 42),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = White,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "--",
                BackColor = Teal
            };
            _lblTechAvatar.Paint += (s, e) =>
            {
                using (GraphicsPath path = BuildRoundedPath(new Rectangle(0, 0, _lblTechAvatar.Width - 1, _lblTechAvatar.Height - 1), 18))
                using (SolidBrush brush = new SolidBrush(_lblTechAvatar.BackColor))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            };
            _lblTechName = new Label { Location = new Point(56, 2), Size = new Size(240, 18), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary, Text = "Unassigned" };
            _lblTechLoad = new Label { Location = new Point(56, 22), Size = new Size(240, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, Text = "0 jobs this week" };
            Panel loadTrack = new Panel { Location = new Point(56, 44), Size = new Size(180, 5), BackColor = BorderLight };
            _techLoadBarFill = new Panel { Location = new Point(0, 0), Size = new Size(0, 5), BackColor = Teal };
            loadTrack.Controls.Add(_techLoadBarFill);
            summary.Controls.AddRange(new Control[] { _lblTechAvatar, _lblTechName, _lblTechLoad, loadTrack });

            body.Controls.Add(summary);
            body.Controls.Add(BuildFormRow("Status", out _cmbStatus));
            body.Controls.Add(BuildFormRow("Priority", out _cmbPriority));
            body.Controls.Add(BuildFormRow("Assign technician", out _cmbTechnician));

            _cmbPriority.Items.AddRange(new object[] { "Low", "Medium", "High", "Critical" });
            _cmbStatus.Items.AddRange(new object[] { "Created", "Assigned", "InProgress", "ChecklistDone", "Closed", "Invoiced" });
            _cmbTechnician.SelectedIndexChanged += async (s, e) => { if (!_isBinding) await RefreshTechnicianWorkloadAsync(); };
            _cmbStatus.SelectedIndexChanged += async (s, e) =>
            {
                if (_isBinding || _isNewMode || _currentDetail == null || _cmbStatus.SelectedItem == null) return;
                string selected = _cmbStatus.SelectedItem.ToString();
                if (!string.Equals(selected, NormalizePipeline(_currentDetail.Job.PipelineStatus), StringComparison.OrdinalIgnoreCase))
                    await HandlePipelineStepClickAsync(selected);
            };
        }

        private void BuildCostCard(Panel body)
        {
            body.AutoScroll = true;
            body.Controls.Add(BuildMetricRow("Quoted revenue", out _lblQuotedRevenue, Teal));
            body.Controls.Add(BuildMetricRow("Est. labour cost", out _lblLabourCost, TextPrimary));
            body.Controls.Add(BuildMetricRow("Parts cost", out _lblPartsCost, TextPrimary));
            body.Controls.Add(BuildMetricRow("Travel cost", out _lblTravelCost, TextPrimary));
            body.Controls.Add(BuildProfitBlock());
            body.Controls.Add(BuildMetricInputRow("Estimated labour cost", out _txtEstimatedLabour));
            body.Controls.Add(BuildMetricInputRow("Revenue input", out _txtQuotedRevenue));

            _txtQuotedRevenue.Leave += (s, e) => RefreshCostPreview();
            _txtEstimatedLabour.Leave += (s, e) => RefreshCostPreview();
        }

        private void BuildChecklistCard(Panel body)
        {
            body.AutoScroll = true;
            _checklistFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 4, 0, 4) };
            _checklistAddPanel = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = White };
            LinkLabel addLink = new LinkLabel { Text = "+ Add item", LinkColor = TealDark, ActiveLinkColor = Teal, AutoSize = true, Location = new Point(0, 8) };
            _txtNewChecklistItem = new TextBox { Visible = false, Width = 220, Location = new Point(82, 6), Font = new Font("Segoe UI", 9f) };
            _txtNewChecklistItem.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await AddChecklistItemAsync();
                }
            };
            addLink.Click += (s, e) => { _txtNewChecklistItem.Visible = true; _txtNewChecklistItem.Focus(); };
            _checklistAddPanel.Controls.Add(addLink);
            _checklistAddPanel.Controls.Add(_txtNewChecklistItem);

            _lblChecklistBanner = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                ForeColor = TealDark,
                BackColor = TealLightBg,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Visible = false
            };

            body.Controls.Add(_checklistFlow);
            body.Controls.Add(_lblChecklistBanner);
            body.Controls.Add(_checklistAddPanel);
        }

        private void BuildPartsCard(Panel body)
        {
            body.AutoScroll = true;
            Panel headerAction = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = White };
            _partsAddPanel = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = White };
            _cmbPartSearch = new ComboBox
            {
                Location = new Point(0, 6),
                Width = 250,
                Font = new Font("Segoe UI", 9f),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbPartSearch.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cmbPartSearch.AutoCompleteSource = AutoCompleteSource.ListItems;
            _cmbPartSearch.TextChanged += (s, e) => UpdatePartStockHint();
            _numPartQty = new NumericUpDown { Location = new Point(258, 6), Width = 70, DecimalPlaces = 3, Minimum = 0.001m, Maximum = 9999, Value = 1 };
            Button btnAddPart = MakeInlineButton("Add", Teal, 60);
            btnAddPart.Location = new Point(336, 5);
            btnAddPart.Click += async (s, e) => await AddPartAsync();
            _lblPartStockHint = new Label { Location = new Point(0, 34), Size = new Size(320, 18), ForeColor = TextHint, Font = new Font("Segoe UI", 8.5f) };
            _partsAddPanel.Controls.AddRange(new Control[] { _cmbPartSearch, _numPartQty, btnAddPart, _lblPartStockHint });

            _partsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 4, 0, 4) };
            _lblPartsTotal = new Label { Dock = DockStyle.Bottom, Height = 24, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleRight };

            headerAction.Controls.Add(_partsAddPanel);
            body.Controls.Add(_partsFlow);
            body.Controls.Add(_lblPartsTotal);
            body.Controls.Add(headerAction);
        }

        private void BuildNudgesCard(Panel body)
        {
            body.AutoScroll = true;
            _nudgesFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 4, 0, 4) };
            body.Controls.Add(_nudgesFlow);
        }

        private void BuildActivityCard(Panel body)
        {
            body.AutoScroll = true;
            _lnkViewAllActivity = new LinkLabel { Text = "View all", LinkColor = BlueDark, ActiveLinkColor = Blue, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _lnkViewAllActivity.Location = new Point(body.Width - 60, 0);
            _lnkViewAllActivity.Click += (s, e) => ShowFullActivityLog();
            body.Controls.Add(_lnkViewAllActivity);
            _activityFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = White, Padding = new Padding(0, 20, 0, 4) };
            body.Controls.Add(_activityFlow);
        }

        private void BuildNotesCard(Panel body)
        {
            body.AutoScroll = true;
            _txtNotes = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            ConfigurePlaceholder(_txtNotes, "Add job notes, observations, or client instructions...");
            _txtNotes.Leave += async (s, e) => await AutoSaveNotesAsync();
            body.Controls.Add(_txtNotes);
        }

        private async Task LoadInitialAsync()
        {
            SetListStatus("Loading jobs...");
            Task<JobLoadSnapshot> loadTask = Task.Run(() => new JobLoadSnapshot
            {
                Jobs = _jobSvc.GetAllJobsWithSummary(),
                Clients = _clientSvc.GetAllClients(),
                Technicians = _employeeSvc.GetActiveTechnicians(),
                Inventory = _inventorySvc.GetAll()
            });
            Task completed = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(6)));
            JobLoadSnapshot snapshot = completed == loadTask
                ? await loadTask
                : new JobLoadSnapshot { TimedOut = true };

            _allJobs = snapshot.Jobs ?? new List<JobSummaryDto>();
            _clients = snapshot.Clients ?? new List<B2BClient>();
            _technicians = snapshot.Technicians ?? new List<Employee>();
            _inventory = snapshot.Inventory ?? new List<StockItem>();

            BindLookups();
            BindPartInventory();
            RenderFilterChips();
            ApplyFilters();
            if (_allJobs.Count > 0)
                await LoadJobDetailAsync(_allJobs[0].JobId);
            else
                await BeginNewJobAsync();
            if (snapshot.TimedOut)
                SetListStatus("Job data is taking longer than expected.");
        }

        private void BindLookups()
        {
            _isBinding = true;
            try
            {
                _cmbClient.Items.Clear();
                _cmbClient.Items.Add(new LookupItem<int>(0, "-- Select client --"));
                foreach (B2BClient client in _clients.OrderBy(c => c.CompanyName))
                    _cmbClient.Items.Add(new LookupItem<int>(client.ClientID, client.CompanyName));
                if (_clients.Count == 0)
                    SetListStatus("No clients found. Add a client before creating a job.");
                _cmbClient.SelectedIndex = 0;

                _cmbTechnician.Items.Clear();
                _cmbTechnician.Items.Add(new LookupItem<int>(0, "-- Unassigned --"));
                foreach (Employee tech in _technicians.OrderBy(t => t.Name))
                    _cmbTechnician.Items.Add(new LookupItem<int>(tech.EmployeeID, tech.Name));
                _cmbTechnician.SelectedIndex = 0;

                _cmbPriority.SelectedIndex = 1;
                _cmbStatus.SelectedIndex = 0;

                _cmbJobType.Items.Clear();
                _cmbJobType.Items.AddRange(new object[] { "PM Visit", "Breakdown", "Installation", "AMC Visit", "Gas Charging", "General" });
                _cmbJobType.SelectedItem = "General";
            }
            finally
            {
                _isBinding = false;
            }
        }

        private void BindPartInventory()
        {
            _cmbPartSearch.Items.Clear();
            foreach (StockItem item in _inventory.OrderBy(i => i.ItemName))
                _cmbPartSearch.Items.Add(new LookupItem<int?>(item.ItemID, item.ItemName + " (" + item.AvailableStock.ToString("0.###") + " " + (item.Unit ?? "Nos") + ")"));
        }

        private async Task BeginNewJobAsync()
        {
            ShowJobEditor();
            _isNewMode = true;
            _currentDetail = null;
            _isBinding = true;
            try
            {
                _txtJobNo.Text = _jobSvc.GenerateJobNumber();
                _txtJobTitle.Text = "AC Installation at Site";
                _cmbClient.SelectedIndex = _cmbClient.Items.Count > 1 ? 1 : 0;
                _cmbSite.Items.Clear();
                _cmbSite.Items.Add(new LookupItem<int>(0, "-- Select site --"));
                _cmbSite.SelectedIndex = 0;
                _cmbContract.Items.Clear();
                _cmbContract.Items.Add(new LookupItem<int>(0, "-- No linked contract --"));
                _cmbContract.SelectedIndex = 0;
                _cmbTechnician.SelectedIndex = 0;
                _cmbPriority.SelectedItem = "Medium";
                _cmbStatus.SelectedItem = "Created";
                _cmbJobType.SelectedItem = "General";
                _dtpScheduled.Value = DateTime.Today;
                _txtQuotedRevenue.Text = "0";
                _txtEstimatedLabour.Text = "0";
                SetTextBoxValue(_txtNotes, "Add job notes, observations, or client instructions...", true);
            }
            finally
            {
                _isBinding = false;
            }

            if (GetSelectedId(_cmbClient) > 0)
                await LoadSitesAndContractsAsync();

            RenderChecklistPreview(_jobSvc.GetChecklistTemplates("General").Select(t => new JobChecklistItem { ItemText = t.ItemText, SortOrder = t.SortOrder }).ToList());
            RenderParts(new List<JobPartUsed>());
            RenderNudges(new List<NudgeDto>());
            RenderActivity(new List<JobActivityEntry>());
            RefreshHeader(null);
            RefreshCostPreview();
            UpdatePipelineBar("Created");
            await RefreshTechnicianWorkloadAsync();
            LayoutCards();
        }

        private async Task LoadSitesAndContractsAsync()
        {
            int clientId = GetSelectedId(_cmbClient);
            var payload = await Task.Run(() => new
            {
                Sites = clientId > 0 ? _siteSvc.GetByClientId(clientId) : new List<ClientSite>(),
                Contracts = clientId > 0 ? _contractSvc.GetContractsByClient(clientId) : new List<AMCContract>()
            });

            _sitesForClient = payload.Sites;
            _contractsForClient = payload.Contracts;

            _isBinding = true;
            try
            {
                _cmbSite.Items.Clear();
                _cmbSite.Items.Add(new LookupItem<int>(0, "-- Select site --"));
                foreach (ClientSite site in _sitesForClient.OrderBy(s => s.SiteName))
                    _cmbSite.Items.Add(new LookupItem<int>(site.SiteID, SiteService.GetDisplayName(site)));
                _cmbSite.SelectedIndex = 0;

                _cmbContract.Items.Clear();
                _cmbContract.Items.Add(new LookupItem<int>(0, "-- No linked contract --"));
                foreach (AMCContract contract in _contractsForClient.OrderBy(c => c.ContractID))
                    _cmbContract.Items.Add(new LookupItem<int>(contract.ContractID, "AMC-" + contract.ContractID + " - " + (contract.ContractType ?? "AMC")));
                _cmbContract.SelectedIndex = 0;
            }
            finally
            {
                _isBinding = false;
            }
            RefreshCostPreview();
        }

        private async Task LoadJobDetailAsync(int jobId)
        {
            ShowJobEditor();
            SetListStatus("Loading job...");
            JobDetailDto detail = await Task.Run(() => _jobSvc.GetJobDetail(jobId));
            if (detail == null)
                return;

            _currentDetail = detail;
            _isNewMode = false;
            BindDetail(detail);
            SetListStatus(_allJobs.Count + " jobs loaded");
        }

        private void ShowJobEditor()
        {
            if (_split != null && _split.Panel2Collapsed)
            {
                _split.Panel2Collapsed = false;
                AdjustResponsiveLayout();
            }

            if (_topBar != null)
                _topBar.Visible = true;
            if (_pipelineBar != null)
                _pipelineBar.Visible = true;
            if (_detailScroll != null && _cardsHost != null && !_detailScroll.Controls.Contains(_cardsHost))
            {
                _detailScroll.Controls.Clear();
                _detailScroll.Controls.Add(_cardsHost);
                LayoutCards();
            }
            if (_rightPanel != null && !_rightPanel.Visible)
                _rightPanel.Visible = true;
        }

        private void HideJobEditor()
        {
            _currentDetail = null;
            _isNewMode = false;
            ShowEmptyJobState();
        }

        private void ShowEmptyJobState()
        {
            if (_split != null && _split.Panel2Collapsed)
            {
                _split.Panel2Collapsed = false;
                AdjustResponsiveLayout();
            }

            if (_rightPanel != null)
                _rightPanel.Visible = true;
            if (_topBar != null)
                _topBar.Visible = false;
            if (_pipelineBar != null)
                _pipelineBar.Visible = false;
            if (_detailScroll == null)
                return;

            _detailScroll.Controls.Clear();
            Panel empty = new Panel
            {
                BackColor = White,
                Width = 420,
                Height = 160,
                Location = new Point(20, 20)
            };
            empty.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, empty.Width - 1, empty.Height - 1);
            };
            empty.Controls.Add(new Label
            {
                Text = "No job selected",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(24, 24),
                AutoSize = true
            });
            empty.Controls.Add(new Label
            {
                Text = "Select a job from the queue or create a new job to start scheduling work.",
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextSecondary,
                Location = new Point(24, 62),
                Size = new Size(350, 42)
            });
            Button create = MakeHeaderButton("+ New Job", Teal, White, 110);
            create.Location = new Point(24, 112);
            create.Click += async (s, e) => await BeginNewJobAsync();
            empty.Controls.Add(create);
            _detailScroll.Controls.Add(empty);
        }

        private void BindDetail(JobDetailDto detail)
        {
            _isBinding = true;
            try
            {
                Job job = detail.Job;
                _txtJobNo.Text = job.JobNumber ?? string.Empty;
                _txtJobTitle.Text = job.JobTitle ?? job.Title ?? string.Empty;
                SelectLookup(_cmbClient, job.ClientID);

                _sitesForClient = _siteSvc.GetByClientId(job.ClientID);
                _contractsForClient = _contractSvc.GetContractsByClient(job.ClientID);
                RebindSitesAndContracts(job.SiteID, job.LinkedContractId ?? 0);

                SelectLookup(_cmbTechnician, job.AssignedEmployeeID ?? 0);
                SelectText(_cmbPriority, job.Priority, "Medium");
                SelectText(_cmbStatus, NormalizePipeline(job.PipelineStatus), "Created");
                SelectText(_cmbJobType, job.JobType, "General");
                _dtpScheduled.Value = job.ScheduledDate == default(DateTime) ? DateTime.Today : job.ScheduledDate;
                _txtQuotedRevenue.Text = (job.QuotedRevenue > 0 ? job.QuotedRevenue : job.Revenue).ToString("0.##");
                _txtEstimatedLabour.Text = job.EstimatedCost.ToString("0.##");
                SetTextBoxValue(_txtNotes, job.Notes, false);
            }
            finally
            {
                _isBinding = false;
            }

            RenderChecklist(detail.ChecklistItems);
            RenderParts(detail.PartsUsed);
            RenderNudges(_jobSvc.GenerateNudges(detail.Job.JobID));
            RenderActivity(detail.ActivityLog);
            RefreshHeader(detail);
            RefreshCostPreview();
            UpdatePipelineBar(detail.Job.PipelineStatus);
            _ = RefreshTechnicianWorkloadAsync();
            LayoutCards();
        }

        private void RebindSitesAndContracts(int selectedSiteId, int selectedContractId)
        {
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new LookupItem<int>(0, "-- Select site --"));
            foreach (ClientSite site in _sitesForClient.OrderBy(s => s.SiteName))
                _cmbSite.Items.Add(new LookupItem<int>(site.SiteID, SiteService.GetDisplayName(site)));
            SelectLookup(_cmbSite, selectedSiteId);

            _cmbContract.Items.Clear();
            _cmbContract.Items.Add(new LookupItem<int>(0, "-- No linked contract --"));
            foreach (AMCContract contract in _contractsForClient.OrderBy(c => c.ContractID))
                _cmbContract.Items.Add(new LookupItem<int>(contract.ContractID, "AMC-" + contract.ContractID + " - " + (contract.ContractType ?? "AMC")));
            SelectLookup(_cmbContract, selectedContractId);
        }

        private void RenderFilterChips()
        {
            _chipFlow.Controls.Clear();
            AddChip("All", _allJobs.Count);
            AddChip("Pending", _allJobs.Count(j => j.PipelineStatus == "Created" || j.PipelineStatus == "Assigned"), AmberLightBg, AmberDark);
            AddChip("Active", _allJobs.Count(j => j.PipelineStatus == "InProgress" || j.PipelineStatus == "ChecklistDone"), TealLightBg, TealDark);
            AddChip("Done", _allJobs.Count(j => j.PipelineStatus == "Closed" || j.PipelineStatus == "Invoiced"), Surface, TextSecondary);
            AddChip("Overdue", _allJobs.Count(j => j.IsOverdue), RedLightBg, RedDark);
        }

        private void AddChip(string label, int count, Color? bg = null, Color? text = null)
        {
            if (!string.Equals(label, "All", StringComparison.OrdinalIgnoreCase) && count <= 0)
                return;

            Button chip = new Button
            {
                Text = label + " (" + count + ")",
                AutoSize = true,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? TealLightBg : (bg ?? White),
                ForeColor = string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? TealDark : (text ?? TextSecondary),
                Font = new Font("Segoe UI", 8.5f, string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? FontStyle.Bold : FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0),
                Tag = label
            };
            chip.FlatAppearance.BorderColor = string.Equals(_activeFilter, label, StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#9FE1CB") : Border;
            chip.FlatAppearance.BorderSize = 1;
            chip.Click += (s, e) =>
            {
                _activeFilter = (string)chip.Tag;
                RenderFilterChips();
                ApplyFilters();
            };
            _chipFlow.Controls.Add(chip);
        }

        private void ApplyFilters()
        {
            IEnumerable<JobSummaryDto> filtered = _allJobs;
            string search = GetSearchText();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(j =>
                    Contains(j.JobNumber, search) ||
                    Contains(j.JobTitle, search) ||
                    Contains(j.ClientName, search) ||
                    Contains(j.SiteName, search) ||
                    Contains(j.TechnicianName, search));
            }

            switch (_activeFilter)
            {
                case "Pending":
                    filtered = filtered.Where(j => j.PipelineStatus == "Created" || j.PipelineStatus == "Assigned");
                    break;
                case "Active":
                    filtered = filtered.Where(j => j.PipelineStatus == "InProgress" || j.PipelineStatus == "ChecklistDone");
                    break;
                case "Done":
                    filtered = filtered.Where(j => j.PipelineStatus == "Closed" || j.PipelineStatus == "Invoiced");
                    break;
                case "Overdue":
                    filtered = filtered.Where(j => j.IsOverdue);
                    break;
            }

            RenderJobList(filtered.ToList());
        }

        private void RenderJobList(List<JobSummaryDto> jobs)
        {
            _jobListFlow.SuspendLayout();
            _jobListFlow.Controls.Clear();
            if (jobs.Count == 0)
            {
                _jobListFlow.Controls.Add(new Label
                {
                    Text = "No jobs match this view.",
                    AutoSize = false,
                    Size = new Size(_split.Panel1.Width - 48, 40),
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = TextHint,
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }
            else
            {
                foreach (JobSummaryDto job in jobs)
                    _jobListFlow.Controls.Add(CreateJobListItem(job));
            }
            _jobListFlow.ResumeLayout();
            SetListStatus(jobs.Count + " jobs shown");
        }

        private Control CreateJobListItem(JobSummaryDto job)
        {
            bool isSelected = _selectedJobId == job.JobId || (_currentDetail != null && _currentDetail.Job.JobID == job.JobId);
            Panel card = new Panel
            {
                Width = _split.Panel1.Width - 42,
                Height = 86,
                BackColor = isSelected ? TealLightBg : White,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(14, 10, 14, 10),
                Cursor = Cursors.Hand,
                Tag = job
            };
            card.Paint += (s, e) =>
            {
                using (SolidBrush bg = new SolidBrush(card.BackColor))
                    e.Graphics.FillRectangle(bg, card.ClientRectangle);
                using (Pen pen = new Pen(card.BackColor == TealLightBg ? ColorTranslator.FromHtml("#9FE1CB") : Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                if (card.BackColor == TealLightBg)
                {
                    using (SolidBrush brush = new SolidBrush(Teal))
                        e.Graphics.FillRectangle(brush, 0, 0, 3, card.Height);
                }
            };

            Label lblJob = new Label { Text = job.JobNumber, Location = new Point(0, 0), Size = new Size(130, 16), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = TextPrimary };
            Label lblStatus = MakePill(GetPipelineLabel(job.PipelineStatus), GetStatusPillBack(job), GetStatusPillFore(job), 84);
            lblStatus.Location = new Point(card.Width - 100, 0);

            Label lblClient = new Label { Text = (job.ClientName ?? "-") + " / " + (job.SiteName ?? "-"), Location = new Point(0, 22), Size = new Size(card.Width - 100, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            Label lblJobType = MakePill(job.JobType ?? "General", GetJobTypeBack(job.JobType), GetJobTypeFore(job.JobType), 96);
            lblJobType.Location = new Point(0, 44);
            Label lblDate = new Label
            {
                Text = job.IsOverdue ? "Overdue " + Math.Max((DateTime.Today - job.ScheduledDate.Date).Days, 1) + "d" : IndiaFormatHelper.FormatDate(job.ScheduledDate),
                Location = new Point(card.Width - 100, 46),
                Size = new Size(90, 14),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = job.IsOverdue ? RedDark : TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            };

            Label lblAvatar = new Label
            {
                Text = GetInitials(job.TechnicianName),
                Location = new Point(0, 63),
                Size = new Size(32, 16),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = White,
                BackColor = GetTechnicianColor(job.TechnicianId ?? 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblAvatar.Paint += (s, e) =>
            {
                using (GraphicsPath path = BuildRoundedPath(new Rectangle(0, 0, lblAvatar.Width - 1, lblAvatar.Height - 1), 8))
                using (SolidBrush brush = new SolidBrush(lblAvatar.BackColor))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(brush, path);
                }
            };
            Label lblTech = new Label { Text = job.TechnicianName ?? "Unassigned", Location = new Point(40, 63), Size = new Size(150, 16), Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary };
            Label lblMargin = new Label
            {
                Text = job.EstimatedMarginPct > 0 ? job.EstimatedMarginPct.ToString("0.0") + "%" : "0%",
                Location = new Point(card.Width - 70, 63),
                Size = new Size(60, 16),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = GetMarginColor(job.EstimatedMarginPct),
                TextAlign = ContentAlignment.MiddleRight
            };

            card.Controls.AddRange(new Control[] { lblJob, lblStatus, lblClient, lblJobType, lblDate, lblAvatar, lblTech, lblMargin });
            AddClickable(card, () =>
            {
                _selectedJobId = job.JobId;
                if (OnOpenJobDetail != null)
                    OnOpenJobDetail(job.JobId);
                return Task.CompletedTask;
            });
            return card;
        }

        public void SelectJobFromNavigation(int jobId)
        {
            _selectedJobId = jobId;
            if (_jobListFlow != null)
                ApplyFilters();
        }

        private async Task SaveAsync()
        {
            try
            {
                Job job = CollectJobFromUi();
                if (_isNewMode)
                {
                    int newId = await Task.Run(() => _jobSvc.Create(job));
                    await ReloadJobsAsync(newId);
                }
                else
                {
                    job.JobID = _currentDetail.Job.JobID;
                    job.InvoiceId = _currentDetail.Job.InvoiceId;
                    job.CompletedDate = _currentDetail.Job.CompletedDate;
                    job.ClosedDate = _currentDetail.Job.ClosedDate;
                    await Task.Run(() => _jobSvc.Update(job));
                    await ReloadJobsAsync(job.JobID);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Saving job", ex);
            }
        }

        private async Task ReloadJobsAsync(int selectJobId)
        {
            _allJobs = await Task.Run(() => _jobSvc.GetAllJobsWithSummary());
            RenderFilterChips();
            ApplyFilters();
            await LoadJobDetailAsync(selectJobId);
        }

        private Job CollectJobFromUi()
        {
            int clientId = GetSelectedId(_cmbClient);
            int siteId = GetSelectedId(_cmbSite);
            if (clientId <= 0) throw new Exception("Client is required.");
            if (siteId <= 0) throw new Exception("Site is required.");
            if (string.IsNullOrWhiteSpace(_txtJobTitle.Text)) throw new Exception("Job title is required.");

            return new Job
            {
                JobNumber = _txtJobNo.Text.Trim(),
                ClientID = clientId,
                SiteID = siteId,
                Title = _txtJobTitle.Text.Trim(),
                JobTitle = _txtJobTitle.Text.Trim(),
                JobType = _cmbJobType.SelectedItem?.ToString() ?? "General",
                LinkedContractId = GetSelectedId(_cmbContract) > 0 ? (int?)GetSelectedId(_cmbContract) : null,
                ScheduledDate = _dtpScheduled.Value.Date,
                AssignedEmployeeID = GetSelectedId(_cmbTechnician) > 0 ? (int?)GetSelectedId(_cmbTechnician) : null,
                Priority = _cmbPriority.SelectedItem?.ToString() ?? "Medium",
                PipelineStatus = _cmbStatus.SelectedItem?.ToString() ?? "Created",
                QuotedRevenue = ParseMoney(_txtQuotedRevenue.Text),
                Revenue = ParseMoney(_txtQuotedRevenue.Text),
                EstimatedCost = ParseMoney(_txtEstimatedLabour.Text),
                Notes = GetTextValue(_txtNotes)
            };
        }

        private async Task HandleJobTypeChangedAsync()
        {
            string jobType = _cmbJobType.SelectedItem?.ToString() ?? "General";
            if (_isNewMode)
            {
                RenderChecklistPreview(_jobSvc.GetChecklistTemplates(jobType).Select(t => new JobChecklistItem { ItemText = t.ItemText, SortOrder = t.SortOrder }).ToList());
                ShowChecklistBanner("Checklist loaded for " + jobType);
                return;
            }

            if (_currentDetail != null && _currentDetail.ChecklistItems.Count > 0)
            {
                DialogResult confirm = MessageBox.Show("Replace the current checklist with the " + jobType + " template?", "Replace checklist", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                    return;
            }

            await Task.Run(() => _jobSvc.ApplyChecklistTemplate(_currentDetail.Job.JobID, jobType));
            await LoadJobDetailAsync(_currentDetail.Job.JobID);
            ShowChecklistBanner("Checklist loaded for " + jobType);
        }

        private async Task AddChecklistItemAsync()
        {
            string text = _txtNewChecklistItem.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (_isNewMode)
            {
                List<JobChecklistItem> preview = GetPreviewChecklistItems();
                preview.Add(new JobChecklistItem { ItemText = text, SortOrder = preview.Count + 1 });
                RenderChecklistPreview(preview);
            }
            else
            {
                await Task.Run(() => _jobSvc.AddChecklistItem(_currentDetail.Job.JobID, text));
                await LoadJobDetailAsync(_currentDetail.Job.JobID);
            }

            _txtNewChecklistItem.Clear();
            _txtNewChecklistItem.Visible = false;
        }

        private async Task AddPartAsync()
        {
            if (_isNewMode || _currentDetail == null)
            {
                MessageBox.Show("Save the job before adding parts.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string typed = _cmbPartSearch.Text?.Trim();
            LookupItem<int?> selected = _cmbPartSearch.SelectedItem as LookupItem<int?>;
            int? itemId = selected != null && selected.Value.HasValue ? selected.Value : (int?)null;
            await Task.Run(() => _jobSvc.AddPartUsed(_currentDetail.Job.JobID, itemId, _numPartQty.Value, typed));
            _cmbPartSearch.Text = string.Empty;
            _numPartQty.Value = 1;
            await LoadJobDetailAsync(_currentDetail.Job.JobID);
        }

        private async Task RefreshTechnicianWorkloadAsync()
        {
            int employeeId = GetSelectedId(_cmbTechnician);
            if (employeeId <= 0)
            {
                _lblTechName.Text = "Unassigned";
                _lblTechLoad.Text = "0 jobs this week";
                _lblTechAvatar.Text = "--";
                _lblTechAvatar.BackColor = Teal;
                _techLoadBarFill.Width = 0;
                return;
            }

            Employee tech = _technicians.FirstOrDefault(t => t.EmployeeID == employeeId) ?? _employeeSvc.GetById(employeeId);
            WorkloadDto workload = await Task.Run(() => _jobSvc.GetTechnicianWorkload(employeeId, GetWeekStart(DateTime.Today)));

            _lblTechName.Text = tech?.Name ?? "Technician";
            _lblTechLoad.Text = workload.JobCount + " jobs this week - " + workload.LoadPercent.ToString("0.#") + "% load";
            _lblTechAvatar.Text = GetInitials(tech?.Name);
            _lblTechAvatar.BackColor = GetTechnicianColor(employeeId);
            _techLoadBarFill.Width = Math.Max(0, Math.Min(180, (int)Math.Round(180m * (workload.LoadPercent / 100m))));
            _techLoadBarFill.BackColor = workload.LoadColour == "red" ? Red : (workload.LoadColour == "amber" ? Amber : Teal);
            _lblTechAvatar.Invalidate();
            RefreshHeader(_currentDetail);
        }

        private void RefreshHeader(JobDetailDto detail)
        {
            Job job = detail?.Job;
            _lblJobNumber.Text = job?.JobNumber ?? _txtJobNo.Text.Trim();
            _lblJobTitle.Text = job?.JobTitle ?? _txtJobTitle.Text.Trim();
            string status = NormalizePipeline(job?.PipelineStatus ?? (_cmbStatus.SelectedItem?.ToString() ?? "Created"));
            string jobType = job?.JobType ?? (_cmbJobType.SelectedItem?.ToString() ?? "General");
            string techName = GetSelectedText(_cmbTechnician, "Unassigned");
            string date = IndiaFormatHelper.FormatDate(job?.ScheduledDate ?? _dtpScheduled.Value);
            string priority = job?.Priority ?? (_cmbPriority.SelectedItem?.ToString() ?? "Medium");
            _lblMeta.Text = status + " - " + jobType + " - " + techName + " - " + date + " - " + priority;
            _btnCloseJob.Enabled = !_isNewMode;
            _btnPrintReport.Enabled = !_isNewMode;
            UpdatePipelineBar(status);
        }

        private void RefreshCostPreview()
        {
            decimal revenue = ParseMoney(_txtQuotedRevenue.Text);
            decimal labour = ParseMoney(_txtEstimatedLabour.Text);
            decimal parts = _currentDetail?.PartsCost ?? 0m;
            int siteId = GetSelectedId(_cmbSite);
            ClientSite site = _sitesForClient.FirstOrDefault(s => s.SiteID == siteId) ?? _currentDetail?.Site;
            decimal travel = site?.TravelRateINR ?? 0m;
            decimal profit = revenue - labour - parts - travel;
            decimal margin = revenue <= 0 ? 0m : Math.Round((profit / revenue) * 100m, 1);

            _lblQuotedRevenue.Text = IndiaFormatHelper.FormatCurrency(revenue);
            _lblLabourCost.Text = IndiaFormatHelper.FormatCurrency(labour);
            _lblPartsCost.Text = IndiaFormatHelper.FormatCurrency(parts);
            _lblTravelCost.Text = IndiaFormatHelper.FormatCurrency(travel);
            _lblProfit.Text = IndiaFormatHelper.FormatCurrency(profit);
            _lblProfit.ForeColor = profit >= 0 ? Teal : Red;
            _lblMarginValue.Text = margin.ToString("0.0") + "%";
            _lblMarginValue.ForeColor = GetMarginColor(margin);
            _marginFill.Width = Math.Max(0, Math.Min(220, (int)Math.Round(220m * (Math.Min(Math.Abs(margin), 100m) / 100m))));
            _marginFill.BackColor = margin >= 25m ? Teal : (margin >= 15m ? Amber : Red);
        }

        private void RenderChecklistPreview(List<JobChecklistItem> items)
        {
            _currentDetail = _currentDetail ?? new JobDetailDto { Job = new Job() };
            _currentDetail.ChecklistItems = items;
            _currentDetail.ChecklistCompletedCount = items.Count(i => i.IsCompleted);
            _currentDetail.ChecklistTotalCount = items.Count;
            RenderChecklist(items);
        }

        private List<JobChecklistItem> GetPreviewChecklistItems()
        {
            return _currentDetail?.ChecklistItems?.Select(i => new JobChecklistItem
            {
                ChecklistItemId = i.ChecklistItemId,
                JobId = i.JobId,
                ItemText = i.ItemText,
                IsCompleted = i.IsCompleted,
                CompletedBy = i.CompletedBy,
                CompletedDate = i.CompletedDate,
                SortOrder = i.SortOrder
            }).ToList() ?? new List<JobChecklistItem>();
        }

        private void RenderChecklist(List<JobChecklistItem> items)
        {
            _checklistFlow.SuspendLayout();
            _checklistFlow.Controls.Clear();
            foreach (JobChecklistItem item in items.OrderBy(i => i.SortOrder).ThenBy(i => i.ChecklistItemId))
            {
                Panel row = new Panel { Width = 430, Height = 30, Margin = new Padding(0, 0, 0, 6), BackColor = White };
                CheckBox check = new CheckBox { Location = new Point(0, 6), Size = new Size(18, 18), Checked = item.IsCompleted, Enabled = !_isNewMode && !item.IsCompleted };
                Label lbl = new Label
                {
                    Text = item.ItemText,
                    Location = new Point(28, 5),
                    Size = new Size(380, 18),
                    Font = new Font("Segoe UI", 9f, item.IsCompleted ? FontStyle.Strikeout : FontStyle.Regular),
                    ForeColor = item.IsCompleted ? TextHint : TextPrimary
                };
                if (!_isNewMode)
                {
                    check.CheckedChanged += async (s, e) =>
                    {
                        if (check.Checked && !item.IsCompleted)
                        {
                            await Task.Run(() => _jobSvc.CompleteChecklistItem(item.ChecklistItemId));
                            await LoadJobDetailAsync(_currentDetail.Job.JobID);
                            if (_currentDetail != null && _currentDetail.IsChecklistComplete)
                                ShowChecklistBanner("Checklist complete");
                        }
                    };
                }
                row.Controls.Add(check);
                row.Controls.Add(lbl);
                _checklistFlow.Controls.Add(row);
            }
            _lblChecklistCount.Text = items.Count(i => i.IsCompleted) + " / " + items.Count + " done";
            _lblChecklistCount.ForeColor = items.Count > 0 && items.All(i => i.IsCompleted) ? TealDark : TextSecondary;
            _checklistFlow.ResumeLayout();
        }

        private void RenderParts(List<JobPartUsed> parts)
        {
            _partsFlow.SuspendLayout();
            _partsFlow.Controls.Clear();
            foreach (JobPartUsed part in parts)
            {
                Panel row = new Panel { Width = 430, Height = 44, Margin = new Padding(0, 0, 0, 8), BackColor = White };
                Label lblName = new Label { Text = part.ItemDescription, Location = new Point(0, 0), Size = new Size(220, 16), Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary };
                Label lblMeta = new Label { Text = part.QuantityUsed.ToString("0.###") + " " + (part.Unit ?? "Nos"), Location = new Point(0, 20), Size = new Size(120, 14), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
                Label lblCost = new Label { Text = IndiaFormatHelper.FormatCurrency(part.TotalCost), Location = new Point(250, 0), Size = new Size(90, 16), TextAlign = ContentAlignment.TopRight, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary };
                Label pill = MakePill(GetStockLabel(part.StockStatus), GetStockBack(part.StockStatus), GetStockFore(part.StockStatus), 84);
                pill.Location = new Point(346, 10);
                row.Controls.AddRange(new Control[] { lblName, lblMeta, lblCost, pill });
                _partsFlow.Controls.Add(row);
            }
            decimal total = parts.Sum(p => p.TotalCost);
            _lblPartsTotal.Text = "Total parts cost  " + IndiaFormatHelper.FormatCurrency(total);
            _partsFlow.ResumeLayout();
        }

        private void RenderNudges(List<NudgeDto> nudges)
        {
            _nudgesFlow.SuspendLayout();
            _nudgesFlow.Controls.Clear();
            if (nudges == null || nudges.Count == 0)
            {
                _nudgesFlow.Controls.Add(new Label { Text = "No alerts - job is on track", AutoSize = false, Size = new Size(430, 24), Font = new Font("Segoe UI", 9f), ForeColor = TextHint });
            }
            else
            {
                foreach (NudgeDto nudge in nudges)
                    _nudgesFlow.Controls.Add(CreateNudgeStrip(nudge));
            }
            _nudgesFlow.ResumeLayout();
        }

        private void RenderActivity(List<JobActivityEntry> activities)
        {
            _activityFlow.SuspendLayout();
            _activityFlow.Controls.Clear();
            foreach (JobActivityEntry entry in activities)
            {
                Panel row = new Panel { Width = 430, Height = 42, Margin = new Padding(0, 0, 0, 8), BackColor = White };
                Panel dot = new Panel { Location = new Point(0, 7), Size = new Size(8, 8), BackColor = GetActivityColor(entry.ActivityType) };
                dot.Paint += (s, e) =>
                {
                    using (SolidBrush brush = new SolidBrush(dot.BackColor))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillEllipse(brush, 0, 0, 8, 8);
                    }
                };
                Label lblText = new Label { Text = entry.ActivityText, Location = new Point(18, 0), Size = new Size(390, 18), Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
                Label lblTime = new Label { Text = FormatActivityTime(entry.ActivityDate), Location = new Point(18, 18), Size = new Size(220, 14), Font = new Font("Segoe UI", 8f), ForeColor = TextHint };
                row.Controls.AddRange(new Control[] { dot, lblText, lblTime });
                _activityFlow.Controls.Add(row);
            }
            _activityFlow.ResumeLayout();
        }

        private async Task HandlePipelineStepClickAsync(string target)
        {
            if (_isNewMode || _currentDetail == null)
                return;

            string current = NormalizePipeline(_currentDetail.Job.PipelineStatus);
            target = NormalizePipeline(target);
            if (target == current)
                return;

            if (GetPipelineRank(target) - GetPipelineRank(current) > 1)
            {
                DialogResult skipConfirm = MessageBox.Show("Move job to " + GetPipelineLabel(target) + "?", "Advance pipeline", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (skipConfirm != DialogResult.Yes)
                    return;
            }

            try
            {
                await Task.Run(() => _jobSvc.AdvancePipeline(_currentDetail.Job.JobID, target));
                await ReloadJobsAsync(_currentDetail.Job.JobID);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Advancing pipeline", ex);
            }
        }

        private async Task CloseJobAsync()
        {
            if (_isNewMode || _currentDetail == null)
                return;

            int remaining = _currentDetail.ChecklistTotalCount - _currentDetail.ChecklistCompletedCount;
            if (remaining > 0)
            {
                DialogResult warning = MessageBox.Show(remaining + " checklist items are not completed. Close anyway?", "Incomplete checklist", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (warning != DialogResult.Yes)
                    return;
            }

            using (CloseJobDialog dialog = new CloseJobDialog(_currentDetail))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    int invoiceId = await Task.Run(() => _jobSvc.CloseJob(_currentDetail.Job.JobID, dialog.ActualRevenue, dialog.CloseNotes, dialog.GenerateInvoice));
                    await ReloadJobsAsync(_currentDetail.Job.JobID);
                    if (dialog.GenerateInvoice && invoiceId > 0)
                    {
                        MessageBox.Show("Invoice created successfully from this job.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Closing job", ex);
                }
            }
        }

        private void PrintReport()
        {
            if (_isNewMode || _currentDetail == null)
                return;

            try
            {
                string dir = @"C:\HVAC_PRO_MSE\REPORTS\Jobs";
                Directory.CreateDirectory(dir);
                string pdfPath = Path.Combine(dir, (_currentDetail.Job.JobNumber ?? "job-report") + ".pdf");
                ExportHtmlToPdf(BuildJobReportHtml(_currentDetail), pdfPath);
                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Printing job report", ex);
            }
        }

        private async Task AutoSaveNotesAsync()
        {
            if (_isNewMode || _currentDetail == null)
                return;
            try
            {
                string notes = GetTextValue(_txtNotes);
                await Task.Run(() => _jobSvc.UpdateNotes(_currentDetail.Job.JobID, notes));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("Job notes autosave", ex);
            }
        }

        private void ShowFullActivityLog()
        {
            if (_isNewMode || _currentDetail == null)
                return;

            List<JobActivityEntry> items = _jobSvc.GetJobDetail(_currentDetail.Job.JobID).ActivityLog;
            Form dlg = new Form
            {
                Text = "Activity log - " + (_currentDetail.Job.JobNumber ?? "Job"),
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(640, 420),
                BackColor = White,
                Font = new Font("Segoe UI", 9f)
            };

            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = "TimeText", FillWeight = 25 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "ActivityType", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Activity", DataPropertyName = "ActivityText", FillWeight = 60 });
            GridTheme.Apply(grid);
            grid.DataSource = items.Select(a => new { TimeText = FormatActivityTime(a.ActivityDate), a.ActivityType, a.ActivityText }).ToList();
            dlg.Controls.Add(grid);
            dlg.ShowDialog(this);
        }

        private void UpdatePipelineBar(string pipelineStatus)
        {
            string normalized = NormalizePipeline(pipelineStatus);
            foreach (string step in _pipelineSteps)
            {
                if (_pipelineStepPanels.TryGetValue(step, out Panel panel))
                    panel.Invalidate();
            }
        }

        private void LayoutCards()
        {
            if (_cardsHost == null || _detailScroll == null)
                return;

            int contentWidth = Math.Max(900, _detailScroll.ClientSize.Width - 24);
            _cardsHost.Width = contentWidth;

            int gap = 16;
            int rightWidth = Math.Max(340, (contentWidth - gap) / 3);
            int leftWidth = Math.Max(520, contentWidth - rightWidth - gap);
            int twoColWidth = (contentWidth - gap) / 2;
            int y = 0;

            int jobDetailsHeight = ResolveCardHeight(_cardJobDetails, 436);
            int technicianHeight = ResolveCardHeight(_cardTechnician, 210);
            int costHeight = ResolveCardHeight(_cardCost, 210);
            SetCardBounds(_cardJobDetails, 0, y, leftWidth, jobDetailsHeight);
            SetCardBounds(_cardTechnician, leftWidth + gap, y, rightWidth, technicianHeight);
            SetCardBounds(_cardCost, leftWidth + gap, y + technicianHeight + gap, rightWidth, costHeight);
            y += Math.Max(jobDetailsHeight, technicianHeight + gap + costHeight) + gap;

            int checklistHeight = ResolveCardHeight(_cardChecklist, 280);
            int partsHeight = ResolveCardHeight(_cardParts, 280);
            SetCardBounds(_cardChecklist, 0, y, twoColWidth, checklistHeight);
            SetCardBounds(_cardParts, twoColWidth + gap, y, twoColWidth, partsHeight);
            y += Math.Max(checklistHeight, partsHeight) + gap;

            int nudgesHeight = ResolveCardHeight(_cardNudges, 240);
            int activityHeight = ResolveCardHeight(_cardActivity, 240);
            SetCardBounds(_cardNudges, 0, y, twoColWidth, nudgesHeight);
            SetCardBounds(_cardActivity, twoColWidth + gap, y, twoColWidth, activityHeight);
            y += Math.Max(nudgesHeight, activityHeight) + gap;

            int notesHeight = ResolveCardHeight(_cardNotes, 170);
            SetCardBounds(_cardNotes, 0, y, contentWidth, notesHeight);
            _cardsHost.Height = y + notesHeight + gap;
        }

        private static void SetCardBounds(Control card, int x, int y, int width, int height)
        {
            card.Bounds = new Rectangle(x, y, width, height);
        }

        private int ResolveCardHeight(Control card, int defaultHeight)
        {
            _cardDefaultHeights[card] = defaultHeight;
            int expandedHeight;
            if (_cardExpandedHeights.TryGetValue(card, out expandedHeight))
                return Math.Max(defaultHeight, expandedHeight);

            return defaultHeight;
        }

        private int GetExpandedCardHeight(int defaultHeight)
        {
            return Math.Min(680, defaultHeight + Math.Max(120, defaultHeight / 2));
        }

        private void ToggleCardExpanded(Control card, Button button)
        {
            int defaultHeight;
            if (!_cardDefaultHeights.TryGetValue(card, out defaultHeight))
                defaultHeight = Math.Max(170, card.Height);

            int expandedHeight;
            if (_cardExpandedHeights.TryGetValue(card, out expandedHeight) && expandedHeight > defaultHeight)
            {
                _cardExpandedHeights.Remove(card);
                button.Text = "Extend";
            }
            else
            {
                _cardExpandedHeights[card] = GetExpandedCardHeight(defaultHeight);
                button.Text = "Collapse";
            }

            LayoutCards();
        }

        private void QueueCardOverflowHint(Panel body, Label overflowLabel)
        {
            if (IsDisposed)
                return;

            try
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)(() => UpdateCardOverflowHint(body, overflowLabel)));
                else
                    UpdateCardOverflowHint(body, overflowLabel);
            }
            catch
            {
            }
        }

        private static void UpdateCardOverflowHint(Panel body, Label overflowLabel)
        {
            bool overflow = body.VerticalScroll.Visible
                || body.HorizontalScroll.Visible
                || body.DisplayRectangle.Height > body.ClientSize.Height + 4
                || body.DisplayRectangle.Width > body.ClientSize.Width + 4;
            overflowLabel.Visible = false;
        }


        private Panel CreateCard(string title, out Panel body) => CreateCard(title, out body, out _);

        private Panel CreateCard(string title, out Panel body, out Label actionLabel)
        {
            Panel card = new Panel { BackColor = White };
            card.Paint += (s, e) => DrawRoundedBorder(e.Graphics, card.ClientRectangle, White, Border, 10);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = White, Padding = new Padding(16, 8, 16, 8) };
            header.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(BorderLight))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Label lblTitle = new Label { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft };
            actionLabel = new Label { Dock = DockStyle.Right, Width = 108, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight };
            Label overflowLabel = new Label { Dock = DockStyle.Right, Width = 0, Font = new Font("Segoe UI", 8f), ForeColor = TextHint, Text = string.Empty, TextAlign = ContentAlignment.MiddleRight, Visible = false };
            Button btnExtend = new Button
            {
                Dock = DockStyle.Right,
                Width = 0,
                Height = 24,
                Text = string.Empty,
                FlatStyle = FlatStyle.Flat,
                BackColor = White,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnExtend.FlatAppearance.BorderColor = Border;
            btnExtend.FlatAppearance.BorderSize = 1;
            btnExtend.FlatAppearance.MouseOverBackColor = Surface;
            Panel headerRight = new Panel { Dock = DockStyle.Right, Width = 118, BackColor = White };
            header.Resize += (s, e) =>
            {
                headerRight.Width = Math.Min(118, Math.Max(90, header.Width / 4));
            };
            headerRight.Controls.Add(btnExtend);
            headerRight.Controls.Add(overflowLabel);
            headerRight.Controls.Add(actionLabel);
            header.Controls.Add(headerRight);
            header.Controls.Add(lblTitle);

            body = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(16, 14, 16, 14), AutoScroll = true };
            Panel cardBody = body;
            cardBody.Layout += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            cardBody.Resize += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            cardBody.ControlAdded += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            cardBody.ControlRemoved += (s, e) => QueueCardOverflowHint(cardBody, overflowLabel);
            btnExtend.Click += (s, e) => ToggleCardExpanded(card, btnExtend);
            card.Controls.Add(cardBody);
            card.Controls.Add(header);
            return card;
        }

        private Control BuildFormRow(string label, out ComboBox combo)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = White };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 14, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary });
            combo = new ComboBox { Dock = DockStyle.Bottom, Height = 26, Font = new Font("Segoe UI", 9f), DropDownStyle = ComboBoxStyle.DropDownList };
            row.Controls.Add(combo);
            return row;
        }

        private Control BuildDateRow(string label, out DateTimePicker picker)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = White };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 14, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary });
            picker = new DateTimePicker { Dock = DockStyle.Bottom, Height = 26, Font = new Font("Segoe UI", 9f), Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            row.Controls.Add(picker);
            return row;
        }

        private Control BuildTextRow(string label, out TextBox textBox, bool readOnly)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = White };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 14, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary });
            textBox = new TextBox { Dock = DockStyle.Bottom, Height = 26, Font = new Font("Segoe UI", 9f), ReadOnly = readOnly, BackColor = readOnly ? Surface : White };
            row.Controls.Add(textBox);
            return row;
        }

        private Control BuildMetricRow(string label, out Label valueLabel, Color valueColor)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = White };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Left, Width = 150, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary });
            valueLabel = new Label { Dock = DockStyle.Right, Width = 170, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = valueColor, TextAlign = ContentAlignment.MiddleRight };
            row.Controls.Add(valueLabel);
            return row;
        }

        private Control BuildMetricInputRow(string label, out TextBox textBox)
        {
            Panel row = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = White };
            row.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary });
            textBox = new TextBox { Dock = DockStyle.Bottom, Height = 24, Font = new Font("Segoe UI", 9f) };
            row.Controls.Add(textBox);
            return row;
        }

        private Control BuildProfitBlock()
        {
            Panel wrap = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = White };
            Label profitLabel = new Label { Text = "Est. profit", Location = new Point(0, 0), Size = new Size(140, 14), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            _lblProfit = new Label { Location = new Point(0, 18), Size = new Size(220, 24), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Teal };
            Label marginLabel = new Label { Text = "Margin", Location = new Point(0, 52), Size = new Size(80, 14), Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary };
            _lblMarginValue = new Label { Location = new Point(250, 52), Size = new Size(70, 14), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = TealDark, TextAlign = ContentAlignment.MiddleRight };
            Panel track = new Panel { Location = new Point(0, 68), Size = new Size(220, 6), BackColor = BorderLight };
            _marginFill = new Panel { Location = new Point(0, 0), Size = new Size(0, 6), BackColor = Teal };
            track.Controls.Add(_marginFill);
            wrap.Controls.AddRange(new Control[] { profitLabel, _lblProfit, marginLabel, _lblMarginValue, track });
            return wrap;
        }

        private static Button MakeHeaderButton(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private static Button MakeInlineButton(string text, Color backColor, int width)
        {
            Button button = MakeHeaderButton(text, backColor, White, width);
            button.Height = 26;
            return button;
        }

        private static Label MakePill(string text, Color backColor, Color foreColor, int width)
        {
            return new Label
            {
                Text = text,
                Size = new Size(width, 20),
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        private Control CreateNudgeStrip(NudgeDto nudge)
        {
            Color borderColor = Blue;
            Color backColor = BlueLightBg;
            Color titleColor = BlueDark;
            Color bodyColor = Blue;
            if (nudge.NudgeType == "Success") { borderColor = Teal; backColor = TealLightBg; titleColor = TealDark; bodyColor = Teal; }
            else if (nudge.NudgeType == "Warning") { borderColor = Amber; backColor = AmberLightBg; titleColor = AmberDark; bodyColor = AmberDark; }
            else if (nudge.NudgeType == "Danger") { borderColor = Red; backColor = RedLightBg; titleColor = RedDark; bodyColor = Red; }

            Panel strip = new Panel { Width = 430, Height = 58, BackColor = backColor, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(12, 8, 12, 8) };
            strip.Paint += (s, e) =>
            {
                using (SolidBrush brush = new SolidBrush(borderColor))
                    e.Graphics.FillRectangle(brush, 0, 0, 3, strip.Height);
            };
            Label title = new Label { Text = nudge.Title, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = titleColor };
            Label body = new Label { Text = nudge.Body, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = bodyColor };
            strip.Controls.Add(body);
            strip.Controls.Add(title);
            return strip;
        }

        private void UpdateSearchClear()
        {
            _btnSearchClear.Visible = !string.IsNullOrWhiteSpace(GetSearchText());
        }

        private string GetSearchText()
        {
            const string placeholder = "Search jobs, clients, technicians...";
            string text = _txtSearch.Text.Trim();
            return string.Equals(text, placeholder, StringComparison.OrdinalIgnoreCase) || IsPlaceholder(_txtSearch, placeholder)
                ? string.Empty
                : text;
        }

        private void UpdatePartStockHint()
        {
            StockItem stock = _inventorySvc.GetByName(_cmbPartSearch.Text);
            _lblPartStockHint.Text = stock == null
                ? "Search inventory item name"
                : "Stock: " + stock.AvailableStock.ToString("0.###") + " " + (stock.Unit ?? "Nos") + " - Rate " + IndiaFormatHelper.FormatCurrency(stock.LastPurchaseRate);
        }

        private void ShowChecklistBanner(string text)
        {
            _lblChecklistBanner.Text = "  " + text;
            _lblChecklistBanner.Visible = true;
            Timer timer = new Timer { Interval = 1800 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                _lblChecklistBanner.Visible = false;
            };
            timer.Start();
        }

        private string BuildJobReportHtml(JobDetailDto detail)
        {
            IndiaCompanySettings settings = _settingsSvc.GetIndiaCompanySettings();
            StringBuilder checklist = new StringBuilder();
            foreach (JobChecklistItem item in detail.ChecklistItems)
                checklist.Append("<tr><td>").Append(item.IsCompleted ? "&#10003;" : "&#10007;").Append("</td><td>")
                    .Append(Html(item.ItemText)).Append("</td><td>").Append(item.CompletedDate.HasValue ? Html(item.CompletedDate.Value.ToString("dd/MM/yyyy hh:mm tt")) : "-").Append("</td></tr>");

            StringBuilder parts = new StringBuilder();
            foreach (JobPartUsed part in detail.PartsUsed)
                parts.Append("<tr><td>").Append(Html(part.ItemDescription)).Append("</td><td>").Append(part.QuantityUsed.ToString("0.###")).Append("</td><td>").Append(Html(part.Unit)).Append("</td><td>").Append(IndiaFormatHelper.FormatCurrency(part.TotalCost)).Append("</td></tr>");

            return "<html><head><meta charset='utf-8'/><style>"
                + "body{font-family:Segoe UI,Arial,sans-serif;color:#1A1A1A;padding:24px;}h1{font-size:22px;margin:0 0 4px;}h2{font-size:15px;margin:18px 0 8px;}table{width:100%;border-collapse:collapse;}td,th{padding:8px;border:1px solid #E8E8E8;font-size:12px;} .meta{margin:3px 0;font-size:12px;color:#6B6B6B;} .sign{margin-top:28px;display:flex;justify-content:space-between;} .line{margin-top:48px;border-top:1px solid #666;width:220px;}"
                + "</style></head><body>"
                + "<h1>" + Html(settings.CompanyName) + "</h1>"
                + "<div class='meta'>" + Html(settings.Address) + "</div>"
                + "<div class='meta'>GSTIN: " + Html(settings.GSTIN) + " | Phone: " + Html(settings.Phone) + "</div>"
                + "<h2>SERVICE REPORT</h2>"
                + "<div class='meta'>Job Number: " + Html(detail.Job.JobNumber) + " | Job Type: " + Html(detail.Job.JobType) + " | Date: " + Html(IndiaFormatHelper.FormatDate(detail.Job.ScheduledDate)) + " | Technician: " + Html(detail.Technician?.Name ?? "Unassigned") + "</div>"
                + "<div class='meta'>Client: " + Html(detail.Client?.CompanyName) + " | Site: " + Html(detail.Site?.SiteName) + " | Contract: " + Html(detail.Contract != null ? ("AMC-" + detail.Contract.ContractID) : "-") + "</div>"
                + "<h2>Checklist</h2><table><tr><th>Status</th><th>Item</th><th>Completion time</th></tr>" + checklist + "</table>"
                + "<h2>Parts used</h2><table><tr><th>Item</th><th>Qty</th><th>Unit</th><th>Cost</th></tr>" + parts + "</table>"
                + "<div class='meta'>Total parts cost: " + IndiaFormatHelper.FormatCurrency(detail.PartsCost) + "</div>"
                + "<h2>Cost summary</h2>"
                + "<div class='meta'>Quoted revenue: " + IndiaFormatHelper.FormatCurrency(detail.Job.QuotedRevenue) + "</div>"
                + "<div class='meta'>Estimated cost: " + IndiaFormatHelper.FormatCurrency(detail.LabourCost + detail.PartsCost + detail.TravelCost) + "</div>"
                + "<div class='meta'>Margin: " + detail.EstimatedMarginPct.ToString("0.0") + "%</div>"
                + "<h2>Notes</h2><div class='meta'>" + Html(detail.Job.Notes) + "</div>"
                + "<div class='sign'><div><div class='line'></div><div class='meta'>Technician signature</div></div><div><div class='line'></div><div class='meta'>Client signature</div></div></div>"
                + "<div class='meta' style='margin-top:24px;'>Generated by " + Html(BrandingService.AppName) + " - " + Html(BrandingService.Subtitle) + " - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "</div>"
                + "</body></html>";
        }

        private static void ExportHtmlToPdf(string html, string pdfPath)
        {
            string tempHtml = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(tempHtml, html);
            string browserPath = FindPdfBrowser();
            if (string.IsNullOrWhiteSpace(browserPath))
                throw new Exception("Microsoft Edge or Google Chrome is required to generate PDF output.");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = "--headless=new --disable-gpu --print-to-pdf=\"" + pdfPath + "\" \"" + new Uri(tempHtml).AbsoluteUri + "\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit(15000);
                if (!File.Exists(pdfPath))
                    throw new Exception("PDF generation did not complete.");
            }
        }

        private static string FindPdfBrowser()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
            };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static void DrawRoundedBorder(Graphics graphics, Rectangle bounds, Color fillColor, Color borderColor, int radius)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;
            Rectangle rect = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = BuildRoundedPath(rect, radius))
            using (SolidBrush brush = new SolidBrush(fillColor))
            using (Pen pen = new Pen(borderColor))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath BuildRoundedPath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawPipelineStep(Graphics graphics, Panel panel, string step)
        {
            string current = NormalizePipeline(_currentDetail?.Job?.PipelineStatus ?? (_cmbStatus.SelectedItem?.ToString() ?? "Created"));
            int stepRank = GetPipelineRank(step);
            int currentRank = GetPipelineRank(current);
            bool done = stepRank < currentRank;
            bool active = stepRank == currentRank;
            Color circleFill = done ? Teal : White;
            Color circleBorder = done || active ? Teal : Border;
            Color textColor = done || active ? TealDark : TextHint;

            if (active)
            {
                using (SolidBrush brush = new SolidBrush(TealLightBg))
                    graphics.FillRectangle(brush, panel.ClientRectangle);
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle circle = new Rectangle(0, 6, 18, 18);
            using (SolidBrush brush = new SolidBrush(circleFill))
            using (Pen pen = new Pen(circleBorder))
            {
                graphics.FillEllipse(brush, circle);
                graphics.DrawEllipse(pen, circle);
            }
            string text = done ? "✓" : stepRank.ToString();
            TextRenderer.DrawText(graphics, text, new Font("Segoe UI", 8f, FontStyle.Bold), circle, done ? White : textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(graphics, GetPipelineLabel(step), new Font("Segoe UI", 8.5f, done || active ? FontStyle.Bold : FontStyle.Regular), new Rectangle(24, 7, panel.Width - 24, 18), textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private static string GetPipelineLabel(string pipeline)
        {
            switch (NormalizePipeline(pipeline))
            {
                case "InProgress": return "In Progress";
                case "ChecklistDone": return "Checklist Done";
                default: return NormalizePipeline(pipeline);
            }
        }

        private static string NormalizePipeline(string pipeline)
        {
            string normalized = (pipeline ?? string.Empty).Replace(" ", string.Empty).Trim();
            switch (normalized.ToUpperInvariant())
            {
                case "CREATED": return "Created";
                case "ASSIGNED": return "Assigned";
                case "INPROGRESS": return "InProgress";
                case "CHECKLISTDONE": return "ChecklistDone";
                case "CLOSED": return "Closed";
                case "INVOICED": return "Invoiced";
                case "COMPLETED": return "Closed";
                default: return "Created";
            }
        }

        private static int GetPipelineRank(string pipeline)
        {
            switch (NormalizePipeline(pipeline))
            {
                case "Created": return 1;
                case "Assigned": return 2;
                case "InProgress": return 3;
                case "ChecklistDone": return 4;
                case "Closed": return 5;
                case "Invoiced": return 6;
                default: return 0;
            }
        }

        private static string GetStockLabel(string stockStatus)
        {
            switch ((stockStatus ?? string.Empty).Trim())
            {
                case "LowStock": return "Low stock";
                case "OutOfStock": return "Out of stock";
                default: return "In stock";
            }
        }

        private static Color GetStockBack(string stockStatus) => string.Equals(stockStatus, "InStock", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#EAF3DE") : RedLightBg;
        private static Color GetStockFore(string stockStatus) => string.Equals(stockStatus, "InStock", StringComparison.OrdinalIgnoreCase) ? ColorTranslator.FromHtml("#27500A") : RedDark;
        private static Color GetActivityColor(string activityType) => string.Equals(activityType, "Warning", StringComparison.OrdinalIgnoreCase) ? Amber : (string.Equals(activityType, "System", StringComparison.OrdinalIgnoreCase) ? Border : Teal);
        private static Color GetMarginColor(decimal pct) => pct >= 25m ? ColorTranslator.FromHtml("#3B6D11") : (pct >= 15m ? AmberDark : (pct <= 0m ? TextHint : RedDark));
        private static string GetInitials(string name) => string.IsNullOrWhiteSpace(name) ? "--" : string.Concat(name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0]))).PadRight(2, '-');

        private static Color GetTechnicianColor(int employeeId)
        {
            Color[] palette = { Teal, Blue, Purple, Amber, Red, TealDark, BlueDark };
            return palette[Math.Abs(employeeId) % palette.Length];
        }

        private static Color GetStatusPillBack(JobSummaryDto job)
        {
            if (job.IsOverdue) return RedLightBg;
            switch (NormalizePipeline(job.PipelineStatus))
            {
                case "Created":
                case "Assigned": return AmberLightBg;
                case "InProgress":
                case "ChecklistDone": return TealLightBg;
                case "Closed": return ColorTranslator.FromHtml("#EAF3DE");
                case "Invoiced": return BlueLightBg;
                default: return Surface;
            }
        }

        private static Color GetStatusPillFore(JobSummaryDto job)
        {
            if (job.IsOverdue) return RedDark;
            switch (NormalizePipeline(job.PipelineStatus))
            {
                case "Created":
                case "Assigned": return AmberDark;
                case "InProgress":
                case "ChecklistDone": return TealDark;
                case "Closed": return ColorTranslator.FromHtml("#27500A");
                case "Invoiced": return BlueDark;
                default: return TextSecondary;
            }
        }

        private static Color GetJobTypeBack(string jobType)
        {
            switch ((jobType ?? string.Empty).Trim())
            {
                case "PM Visit": return BlueLightBg;
                case "Breakdown": return RedLightBg;
                case "Installation": return PurpleLight;
                case "AMC Visit": return TealLightBg;
                case "Gas Charging": return AmberLightBg;
                default: return Surface;
            }
        }

        private static Color GetJobTypeFore(string jobType)
        {
            switch ((jobType ?? string.Empty).Trim())
            {
                case "PM Visit": return BlueDark;
                case "Breakdown": return RedDark;
                case "Installation": return ColorTranslator.FromHtml("#3C3489");
                case "AMC Visit": return ColorTranslator.FromHtml("#085041");
                case "Gas Charging": return AmberDark;
                default: return TextSecondary;
            }
        }

        private static bool Contains(string value, string search) => !string.IsNullOrWhiteSpace(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string FormatActivityTime(DateTime date) => date.Date == DateTime.Today ? "Today " + date.ToString("hh:mm tt") : date.ToString("dd MMM yyyy hh:mm tt");
        private void SetListStatus(string text) => _lblListStatus.Text = text;
        private static int GetSelectedId(ComboBox combo) => combo.SelectedItem is LookupItem<int> item ? item.Value : 0;
        private static string GetSelectedText(ComboBox combo, string fallback) => combo.SelectedItem?.ToString() ?? fallback;

        private static decimal ParseMoney(string text)
        {
            decimal value;
            return decimal.TryParse((text ?? string.Empty).Replace("₹", string.Empty).Replace("Rs", string.Empty).Replace(",", string.Empty).Trim(), out value) ? value : 0m;
        }

        private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty).Replace(Environment.NewLine, "<br/>");
        private static DateTime GetWeekStart(DateTime date) => date.Date.AddDays(-(int)((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7));
        private static void SelectText(ComboBox combo, string value, string fallback) { string target = string.IsNullOrWhiteSpace(value) ? fallback : value; for (int i = 0; i < combo.Items.Count; i++) if (string.Equals(combo.Items[i].ToString(), target, StringComparison.OrdinalIgnoreCase)) { combo.SelectedIndex = i; return; } if (combo.Items.Count > 0) combo.SelectedIndex = 0; }
        private static void SelectLookup(ComboBox combo, int id) { for (int i = 0; i < combo.Items.Count; i++) if (combo.Items[i] is LookupItem<int> item && item.Value == id) { combo.SelectedIndex = i; return; } if (combo.Items.Count > 0) combo.SelectedIndex = 0; }
        private static void SetTextBoxValue(TextBox textBox, string value, bool placeholder) { textBox.ForeColor = placeholder ? TextHint : TextPrimary; textBox.Text = placeholder ? value : (value ?? string.Empty); }
        private string GetTextValue(TextBox textBox) => IsPlaceholder(textBox, "Add job notes, observations, or client instructions...") ? string.Empty : textBox.Text.Trim();

        private void ConfigurePlaceholder(TextBox textBox, string placeholder)
        {
            textBox.Enter += (s, e) =>
            {
                if (_settingPlaceholder) return;
                if (IsPlaceholder(textBox, placeholder))
                {
                    _settingPlaceholder = true;
                    textBox.Text = string.Empty;
                    textBox.ForeColor = TextPrimary;
                    _settingPlaceholder = false;
                }
            };
            textBox.Leave += (s, e) =>
            {
                if (_settingPlaceholder) return;
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    _settingPlaceholder = true;
                    textBox.Text = placeholder;
                    textBox.ForeColor = TextHint;
                    _settingPlaceholder = false;
                }
            };
            textBox.Text = placeholder;
            textBox.ForeColor = TextHint;
        }

        private static bool IsPlaceholder(TextBox textBox, string placeholder) => textBox.ForeColor == TextHint && string.Equals(textBox.Text, placeholder, StringComparison.Ordinal);

        private static void AddClickable(Control root, Func<Task> onClick)
        {
            root.Click += async (s, e) => await onClick();
            foreach (Control child in root.Controls)
                AddClickable(child, onClick);
        }

        private class LookupItem<T>
        {
            public LookupItem(T value, string text) { Value = value; Text = text; }
            public T Value { get; private set; }
            public string Text { get; private set; }
            public override string ToString() => Text;
        }

        private class CloseJobDialog : Form
        {
            public decimal ActualRevenue { get; private set; }
            public string CloseNotes { get; private set; }
            public bool GenerateInvoice { get; private set; }

            public CloseJobDialog(JobDetailDto detail)
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Text = "Closing job: " + (detail.Job.JobTitle ?? detail.Job.Title ?? detail.Job.JobNumber);
                Size = new Size(560, 340);
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                BackColor = White;
                Font = new Font("Segoe UI", 9f);

                Label lblRevenue = new Label { Text = "Actual revenue", Location = new Point(20, 20), Size = new Size(120, 18) };
                TextBox txtRevenue = new TextBox { Location = new Point(20, 42), Width = 180, Text = (detail.Job.QuotedRevenue > 0 ? detail.Job.QuotedRevenue : detail.Job.Revenue).ToString("0.##") };
                Label lblHours = new Label { Text = "Hours worked", Location = new Point(220, 20), Size = new Size(120, 18) };
                NumericUpDown numHours = new NumericUpDown { Location = new Point(220, 42), Width = 120, DecimalPlaces = 1, Minimum = 0, Maximum = 1000, Value = 4 };
                Label lblParts = new Label { Text = "Parts used: " + detail.PartsUsed.Count + " - " + IndiaFormatHelper.FormatCurrency(detail.PartsCost), Location = new Point(20, 82), Size = new Size(360, 18), ForeColor = TextSecondary };
                Label lblNotes = new Label { Text = "Notes", Location = new Point(20, 112), Size = new Size(120, 18) };
                TextBox txtNotes = new TextBox { Location = new Point(20, 134), Width = 500, Height = 88, Multiline = true };

                Button btnCancel = MakeHeaderButton("Cancel", Surface, TextPrimary, 90);
                btnCancel.Location = new Point(20, 242);
                btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

                Button btnCloseOnly = MakeHeaderButton("Close Job Only", Teal, White, 132);
                btnCloseOnly.Location = new Point(160, 242);
                btnCloseOnly.Click += (s, e) =>
                {
                    ActualRevenue = ParseMoney(txtRevenue.Text);
                    CloseNotes = txtNotes.Text.Trim();
                    GenerateInvoice = false;
                    DialogResult = DialogResult.OK;
                };

                Button btnCloseInvoice = MakeHeaderButton("Close Job & Generate Invoice", Red, White, 226);
                btnCloseInvoice.Location = new Point(294, 242);
                btnCloseInvoice.Click += (s, e) =>
                {
                    ActualRevenue = ParseMoney(txtRevenue.Text);
                    CloseNotes = txtNotes.Text.Trim();
                    GenerateInvoice = true;
                    DialogResult = DialogResult.OK;
                };

                Controls.AddRange(new Control[] { lblRevenue, txtRevenue, lblHours, numHours, lblParts, lblNotes, txtNotes, btnCancel, btnCloseOnly, btnCloseInvoice });
                CancelButton = btnCancel;
                AcceptButton = btnCloseOnly;
            }
        }
    }
}

