using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class ServiceDeskForm : DeferredPageControl
    {
        private readonly ServiceDeskService _service = new ServiceDeskService();
        private readonly ClientService _clientService = new ClientService();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly MailIntegrationService _mailService = new MailIntegrationService();

        private readonly Color PageBg = DS.BgPage;
        private readonly Color White = DS.White;
        private readonly Color Border = DS.Border;
        private readonly Color TextPrimary = DS.Slate900;
        private readonly Color TextSecondary = DS.Slate500;
        private readonly Color Teal = DS.Teal500;
        private readonly Color Blue = DS.Primary600;
        private readonly Color Amber = DS.Amber500;
        private readonly Color Red = DS.Red500;
        private readonly Color SoftInfo = Color.FromArgb(239, 246, 255);

        private DataGridView _grid;
        private TextBox _txtSearch;
        private ComboBox _cmbFilter;
        private Label _lblOpen;
        private Label _lblCritical;
        private Label _lblBreached;
        private Label _lblResolvedToday;
        private Label _lblIncidentNumber;
        private Label _lblSla;
        private Label _lblLinkedJob;
        private Label _lblStatus;
        private TableLayoutPanel _workspace;
        private TextBox _txtCaller;
        private TextBox _txtPhone;
        private TextBox _txtShortDescription;
        private TextBox _txtDescription;
        private TextBox _txtSerial;
        private TextBox _txtRootCause;
        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private ComboBox _cmbAssigned;
        private ComboBox _cmbCategory;
        private ComboBox _cmbEquipment;
        private ComboBox _cmbPriority;
        private ComboBox _cmbStatus;
        private TextBox _txtWorkNote;
        private ComboBox _cmbNoteType;
        private FlowLayoutPanel _notesFlow;
        private FlowLayoutPanel _mailAccountsFlow;
        private FlowLayoutPanel _emailsFlow;
        private Button _btnSave;
        private Button _btnCreateJob;
        private Button _btnStart;
        private Button _btnResolve;
        private Button _btnClose;
        private Panel _emptyIncidentsPanel;
        private Label _lblEmptyIncidentsTitle;
        private Label _lblEmptyIncidentsHint;
        private Button _btnClearIncidentFilters;
        private Label _lblMeta;
        private Label _lblBreadcrumb;
        private Panel _incidentTabContent;

        private List<ServiceDeskIncident> _incidents = new List<ServiceDeskIncident>();
        private List<B2BClient> _clients = new List<B2BClient>();
        private List<Employee> _employees = new List<Employee>();
        private ServiceDeskSnapshot _snapshot;
        private ServiceDeskIncident _current;
        private bool _binding;

        public ServiceDeskForm()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            ApplyPermissions();
            EnableDeferredLoad((Func<Task>)(async () => await LoadDataAsync()), ex =>
            {
                AppLogger.LogError("ServiceDeskForm.Load", ex);
                SetStatus("Service Desk load failed: " + ex.Message, Red);
            });
        }

        protected override bool EnableAutomaticLayoutScaling => false;

        private void BuildLayout()
        {
            Controls.Clear();
            _workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 2,
                RowCount = 1
            };
            _workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            _workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
            _workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel leftHost = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, MinimumSize = new Size(320, 0) };
            Panel rightHost = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, MinimumSize = new Size(520, 0) };
            BuildLeft(leftHost);
            BuildRight(rightHost);
            _workspace.Controls.Add(leftHost, 0, 0);
            _workspace.Controls.Add(rightHost, 1, 0);
            Controls.Add(_workspace);
        }

        private void BuildLeft(Control parent)
        {
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(18, 22, 12, 18) };

            Panel overviewCard = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(14, 14, 14, 14) };
            overviewCard.Paint += (s, e) => PaintCardBorder(e.Graphics, overviewCard.ClientRectangle, 10);
            DS.Rounded(overviewCard, 10);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = White };
            header.Controls.Add(new Label { Text = "Incident Overview", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleLeft });
            Label collapse = ModernIconSystem.Icon(ModernIconKind.ChevronDown, 11, DS.Slate700);
            collapse.Dock = DockStyle.Right;
            collapse.Width = 24;
            header.Controls.Add(collapse);

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(0, 4, 0, 10), ColumnCount = 4, BackColor = White };
            for (int i = 0; i < 4; i++)
                kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _lblOpen = AddKpi(kpis, 0, "Open", Blue);
            _lblCritical = AddKpi(kpis, 1, "Critical", Red);
            _lblBreached = AddKpi(kpis, 2, "SLA Breached", Amber);
            _lblResolvedToday = AddKpi(kpis, 3, "Resolved", Teal);

            Panel filterRow = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 6, 0, 8), BackColor = White };
            TableLayoutPanel filterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = White, ColumnCount = 2, RowCount = 1 };
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Panel comboWrap = MakeInputHost();
            comboWrap.Dock = DockStyle.Fill;
            comboWrap.Margin = new Padding(0, 0, 8, 0);
            _cmbFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), FlatStyle = FlatStyle.Flat };
            _cmbFilter.Items.AddRange(new object[] { "All", "Open", "My Work", "Critical", "SLA Breached", "Resolved", "Closed" });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.SelectedIndexChanged += (s, e) => BindGrid();
            Button filterButton = MakeButton("Filter", White, DS.Slate700, 76);
            filterButton.Dock = DockStyle.Fill;
            filterButton.Margin = new Padding(0);
            ModernIconSystem.AddButtonIcon(filterButton, ModernIconKind.Filter);
            filterButton.Click += (s, e) =>
            {
                _cmbFilter.Focus();
                _cmbFilter.DroppedDown = true;
            };
            comboWrap.Controls.Add(_cmbFilter);
            filterLayout.Controls.Add(comboWrap, 0, 0);
            filterLayout.Controls.Add(filterButton, 1, 0);
            filterRow.Controls.Add(filterLayout);

            Panel recentHeader = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = White, Padding = new Padding(0, 8, 0, 8) };
            recentHeader.Controls.Add(new Label { Text = "Recent Incidents", Dock = DockStyle.Left, Width = 160, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleLeft });
            Panel searchWrap = MakeInputHost();
            searchWrap.Dock = DockStyle.Right;
            searchWrap.Width = 172;
            searchWrap.Padding = new Padding(34, 6, 10, 4);
            Label searchIcon = ModernIconSystem.Icon(ModernIconKind.Search, 12, DS.Slate500);
            searchIcon.Location = new Point(10, 7);
            searchIcon.Size = new Size(18, 20);
            searchWrap.Controls.Add(searchIcon);
            _txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.None, ForeColor = DS.Slate700, Text = "" };
            _txtSearch.TextChanged += (s, e) => BindGrid();
            searchWrap.Controls.Add(_txtSearch);
            recentHeader.Controls.Add(searchWrap);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ScrollBars = ScrollBars.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IncidentNumber", HeaderText = "No", DataPropertyName = "IncidentNumber", FillWeight = 17 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "Priority", DataPropertyName = "Priority", FillWeight = 18 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", DataPropertyName = "Status", FillWeight = 18 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ClientName", HeaderText = "Client", DataPropertyName = "ClientName", FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ShortDescription", HeaderText = "Issue", DataPropertyName = "ShortDescription", FillWeight = 25 });
            _grid.SelectionChanged += (s, e) => SelectCurrentGridIncident();
            _grid.CellFormatting += GridCellFormatting;
            StyleGrid(_grid);
            GridTheme.ApplyColumnPolicy(_grid, new[]
            {
                new GridColumnPolicy("IncidentNumber", 92, GridColumnPriority.Required),
                new GridColumnPolicy("Priority", 92, GridColumnPriority.Required),
                new GridColumnPolicy("Status", 92, GridColumnPriority.Required),
                new GridColumnPolicy("ClientName", 150, GridColumnPriority.Required),
                new GridColumnPolicy("ShortDescription", 260, GridColumnPriority.Required)
            });

            Panel gridHost = new Panel { Dock = DockStyle.Fill, BackColor = White };
            gridHost.Controls.Add(_grid);
            _emptyIncidentsPanel = BuildEmptyIncidentsPanel();
            gridHost.Controls.Add(_emptyIncidentsPanel);
            _emptyIncidentsPanel.BringToFront();

            overviewCard.Controls.Add(gridHost);
            overviewCard.Controls.Add(recentHeader);
            overviewCard.Controls.Add(filterRow);
            overviewCard.Controls.Add(kpis);
            overviewCard.Controls.Add(header);
            left.Controls.Add(overviewCard);
            parent.Controls.Add(left);
        }

        private void BuildRight(Control parent)
        {
            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(8, 18, 18, 16) };

            Panel pageTop = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = PageBg, Padding = new Padding(0, 0, 0, 6) };
            _lblBreadcrumb = new Label { Text = "Incidents  >  INC000001", Dock = DockStyle.Left, Width = 260, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold), ForeColor = Blue, TextAlign = ContentAlignment.MiddleLeft };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 710, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            _btnSave = MakeButton("Save", Teal, White, 90);
            _btnCreateJob = MakeButton("Create Job", Blue, White, 110);
            _btnStart = MakeButton("Start Work", White, Blue, 100);
            _btnResolve = MakeButton("Resolve", White, Teal, 92);
            _btnClose = MakeButton("Close", White, Red, 82);
            Button btnForms = MakeButton("Forms", White, Blue, 88);
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            btnForms.Click += (s, e) => OpenIncidentForms();
            actions.Controls.AddRange(new Control[] { _btnSave, _btnCreateJob, _btnClose, _btnResolve, _btnStart, btnForms });
            UIHelper.ApplyActionButton(_btnCreateJob, UiActionVariant.Primary);
            UIHelper.ApplyActionButton(_btnClose, UiActionVariant.Danger);
            _btnSave.Click += (s, e) => SaveIncident();
            _btnCreateJob.Click += (s, e) => CreateJob();
            _btnStart.Click += (s, e) => ChangeStatus("In Progress");
            _btnResolve.Click += (s, e) => ChangeStatus("Resolved");
            _btnClose.Click += (s, e) => ChangeStatus("Closed");
            pageTop.Controls.Add(actions);
            pageTop.Controls.Add(_lblBreadcrumb);

            Panel mainCard = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(0) };
            mainCard.Paint += (s, e) => PaintCardBorder(e.Graphics, mainCard.ClientRectangle, 10);
            DS.Rounded(mainCard, 10);

            Panel incidentHeader = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = White, Padding = new Padding(20, 12, 20, 0) };
            Control alertIcon = ModernIconSystem.Badge(ModernIconKind.Alert, 38, DS.Primary100, DS.Primary600, 16);
            alertIcon.Location = new Point(20, 18);
            _lblIncidentNumber = new Label { Text = "New Incident", Location = new Point(68, 16), Size = new Size(280, 30), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft };
            _lblSla = new Label { Text = "Create a new service incident and assign it to the right team.", Location = new Point(70, 48), Size = new Size(420, 20), Font = new Font("Segoe UI", 8.75f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
            incidentHeader.Controls.Add(alertIcon);
            incidentHeader.Controls.Add(_lblIncidentNumber);
            incidentHeader.Controls.Add(_lblSla);

            Panel tabShell = new Panel { Dock = DockStyle.Fill, BackColor = White };
            Panel tabStrip = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = White, Padding = new Padding(18, 0, 18, 0) };
            tabStrip.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, tabStrip.Height - 1, tabStrip.Width, tabStrip.Height - 1);
            FlowLayoutPanel tabButtons = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 470, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = White };
            _incidentTabContent = new Panel { Dock = DockStyle.Fill, BackColor = White };
            string[] tabNames = { "Incident", "Work notes", "Emails", "History", "Attachments" };
            List<Label> tabLabels = new List<Label>();
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i;
                Label tabLabel = new Label
                {
                    Text = tabNames[i],
                    Width = i == 4 ? 104 : 88,
                    Height = 38,
                    Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                    ForeColor = TextPrimary,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    Margin = Padding.Empty,
                    BackColor = White
                };
                tabLabel.Paint += (s, e) =>
                {
                    if (tabLabel.Tag as string == "active")
                    {
                        using (Pen pen = new Pen(Blue, 2))
                            e.Graphics.DrawLine(pen, 12, tabLabel.Height - 2, tabLabel.Width - 12, tabLabel.Height - 2);
                    }
                };
                tabLabel.Click += (s, e) => SelectIncidentTab(tabIndex, tabLabels);
                tabLabels.Add(tabLabel);
                tabButtons.Controls.Add(tabLabel);
            }
            Control[] tabPages =
            {
                ExtractTabContent(BuildIncidentTab()),
                ExtractTabContent(BuildNotesTab()),
                ExtractTabContent(BuildEmailTab()),
                new Panel { Dock = DockStyle.Fill, BackColor = White },
                new Panel { Dock = DockStyle.Fill, BackColor = White }
            };
            for (int i = tabPages.Length - 1; i >= 0; i--)
            {
                tabPages[i].Dock = DockStyle.Fill;
                tabPages[i].Tag = i;
                tabPages[i].Visible = false;
                _incidentTabContent.Controls.Add(tabPages[i]);
            }
            tabStrip.Controls.Add(tabButtons);
            tabShell.Controls.Add(_incidentTabContent);
            tabShell.Controls.Add(tabStrip);
            SelectIncidentTab(0, tabLabels);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = PageBg };
            _lblStatus = new Label { Dock = DockStyle.Left, Width = 360, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
            _lblMeta = new Label { Dock = DockStyle.Right, Width = 520, Font = new Font("Segoe UI", 8.5f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleRight };
            footer.Controls.Add(_lblMeta);
            footer.Controls.Add(_lblStatus);

            mainCard.Controls.Add(tabShell);
            mainCard.Controls.Add(incidentHeader);
            right.Controls.Add(mainCard);
            right.Controls.Add(footer);
            right.Controls.Add(pageTop);
            parent.Controls.Add(right);
        }

        private Control ExtractTabContent(TabPage tabPage)
        {
            if (tabPage.Controls.Count == 0)
                return new Panel { Dock = DockStyle.Fill, BackColor = White };

            Control content = tabPage.Controls[0];
            tabPage.Controls.Remove(content);
            content.Dock = DockStyle.Fill;
            return content;
        }

        private void SelectIncidentTab(int index, List<Label> tabLabels)
        {
            for (int i = 0; i < tabLabels.Count; i++)
            {
                tabLabels[i].Tag = i == index ? "active" : null;
                tabLabels[i].Font = new Font("Segoe UI", 9f, i == index ? FontStyle.Bold : FontStyle.Regular);
                tabLabels[i].ForeColor = i == index ? Blue : TextPrimary;
                tabLabels[i].Invalidate();
            }

            if (_incidentTabContent == null)
                return;

            foreach (Control content in _incidentTabContent.Controls)
            {
                bool selected = content.Tag is int tabIndex && tabIndex == index;
                content.Visible = selected;
                if (selected)
                    content.BringToFront();
            }
        }

        private TabPage BuildIncidentTab()
        {
            TabPage page = new TabPage("Incident");
            Panel host = new Panel { Dock = DockStyle.Fill, AutoScroll = false, BackColor = White, Padding = new Padding(16, 8, 16, 8) };

            Panel descriptionCard = MakeFormSection("Description", ModernIconKind.Document, 282, out Panel descriptionBody);
            TableLayoutPanel descriptionForm = MakeFormGrid(1);
            descriptionForm.RowCount = 3;
            descriptionForm.RowStyles.Clear();
            descriptionForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            descriptionForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            descriptionForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            _txtShortDescription = AddText(descriptionForm, 0, 0, "Short description");
            _txtDescription = AddMultiText(descriptionForm, 0, 1, "Description");
            _txtRootCause = AddMultiText(descriptionForm, 0, 2, "Root cause / resolution");
            descriptionBody.Controls.Add(descriptionForm);

            Panel detailsCard = MakeFormSection("Details", ModernIconKind.Checklist, 176, out Panel detailsBody);
            TableLayoutPanel detailsForm = MakeFormGrid(4);
            detailsForm.RowCount = 2;
            detailsForm.RowStyles.Clear();
            detailsForm.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            detailsForm.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            _cmbCategory = AddCombo(detailsForm, 0, 0, "Category");
            _cmbEquipment = AddCombo(detailsForm, 1, 0, "Equipment");
            _cmbPriority = AddCombo(detailsForm, 2, 0, "Priority");
            _cmbStatus = AddCombo(detailsForm, 3, 0, "Status");
            _cmbAssigned = AddCombo(detailsForm, 0, 1, "Assigned technician");
            detailsForm.SetColumnSpan(detailsForm.GetControlFromPosition(0, 1), 2);
            _txtSerial = AddText(detailsForm, 2, 1, "Asset / serial number");
            detailsForm.SetColumnSpan(detailsForm.GetControlFromPosition(2, 1), 2);
            detailsBody.Controls.Add(detailsForm);

            Panel callerCard = MakeFormSection("Caller && Location", ModernIconKind.User, 176, out Panel callerBody);
            TableLayoutPanel callerForm = MakeFormGrid(2);
            callerForm.RowCount = 2;
            callerForm.RowStyles.Clear();
            callerForm.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            callerForm.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            _cmbClient = AddCombo(callerForm, 0, 0, "Client");
            _cmbSite = AddCombo(callerForm, 1, 0, "Site");
            _txtCaller = AddText(callerForm, 0, 1, "Caller name");
            _txtPhone = AddText(callerForm, 1, 1, "Caller phone");
            callerBody.Controls.Add(callerForm);

            _lblLinkedJob = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Blue,
                BackColor = Color.FromArgb(241, 245, 255),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(34, 0, 0, 0)
            };
            _lblLinkedJob.Paint += (s, e) => DrawInfoIcon(e.Graphics, new Point(12, 11), Blue);

            _cmbClient.SelectedIndexChanged += (s, e) => LoadSitesForClient();
            _cmbPriority.SelectedIndexChanged += (s, e) => RefreshSlaPreview();

            _cmbCategory.Items.AddRange(new object[] { "AC Breakdown", "Chiller", "Electrical", "Plumbing", "AMC", "Installation", "Gas Charging", "Customer Complaint", "Emergency", "General" });
            _cmbEquipment.Items.AddRange(new object[] { "Split AC", "Cassette AC", "Ductable AC", "VRF/VRV", "Chiller", "Cooling Tower", "Pump", "Panel", "Other" });
            _cmbPriority.Items.AddRange(new object[] { "Low", "Medium", "High", "Critical" });
            _cmbStatus.Items.AddRange(new object[] { "New", "Assigned", "In Progress", "On Hold", "Resolved", "Closed" });

            Label info = new Label
            {
                Text = "Provide as much detail as possible to help the team resolve this issue faster.",
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(241, 245, 255),
                ForeColor = Blue,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(34, 0, 0, 0)
            };
            info.Paint += (s, e) => DrawInfoIcon(e.Graphics, new Point(12, 10), Blue);
            host.Controls.Add(info);
            host.Controls.Add(descriptionCard);
            host.Controls.Add(MakeSpacer(8));
            host.Controls.Add(detailsCard);
            host.Controls.Add(MakeSpacer(8));
            host.Controls.Add(callerCard);
            host.Controls.Add(MakeSpacer(8));
            host.Controls.Add(_lblLinkedJob);
            page.Controls.Add(host);
            return page;
        }

        private TabPage BuildNotesTab()
        {
            TabPage page = new TabPage("Work notes");
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(8) };
            Panel composer = new Panel { Dock = DockStyle.Bottom, Height = 118, BackColor = White, Padding = new Padding(12) };
            composer.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, composer.Width - 1, composer.Height - 1);
            _cmbNoteType = new ComboBox { Dock = DockStyle.Left, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbNoteType.Items.AddRange(new object[] { "Work note", "Customer update", "Internal note", "System" });
            _cmbNoteType.SelectedIndex = 0;
            Button btnAdd = MakeButton("Add Note", Teal, White, 96);
            btnAdd.Dock = DockStyle.Right;
            btnAdd.Click += (s, e) => AddNote();
            _txtWorkNote = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new Font("Segoe UI", 9.5f), Margin = new Padding(10, 0, 10, 0) };
            composer.Controls.Add(_txtWorkNote);
            composer.Controls.Add(btnAdd);
            composer.Controls.Add(_cmbNoteType);

            _notesFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = PageBg, Padding = new Padding(0, 0, 0, 8) };
            host.Controls.Add(_notesFlow);
            host.Controls.Add(composer);
            page.Controls.Add(host);
            return page;
        }

        private TabPage BuildEmailTab()
        {
            TabPage page = new TabPage("Emails");
            Panel host = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(8) };

            Panel top = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = White, Padding = new Padding(12) };
            top.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, top.Width - 1, top.Height - 1);
            Button btnOutlook = MakeButton("Login Outlook", Blue, White, 126);
            Button btnGmail = MakeButton("Login Gmail", Red, White, 114);
            Button btnSync = MakeButton("Sync Mail", Teal, White, 100);
            Button btnSetup = MakeButton("Admin Keys", White, TextPrimary, 104);
            btnOutlook.Dock = DockStyle.Left;
            btnGmail.Dock = DockStyle.Left;
            btnSync.Dock = DockStyle.Left;
            btnSetup.Dock = DockStyle.Right;
            btnOutlook.Click += async (s, e) => await ConnectMailAsync("Outlook");
            btnGmail.Click += async (s, e) => await ConnectMailAsync("Gmail");
            btnSync.Click += async (s, e) => await SyncMailAsync();
            btnSetup.Click += (s, e) => ShowMailSetupDialog();
            top.Controls.Add(btnSync);
            top.Controls.Add(btnGmail);
            top.Controls.Add(btnOutlook);
            top.Controls.Add(btnSetup);

            _mailAccountsFlow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 80, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = PageBg, Padding = new Padding(0, 8, 0, 0) };
            _emailsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = PageBg, Padding = new Padding(0, 8, 0, 8) };

            host.Controls.Add(_emailsFlow);
            host.Controls.Add(_mailAccountsFlow);
            host.Controls.Add(top);
            page.Controls.Add(host);
            return page;
        }

        private async Task LoadDataAsync()
        {
            SetStatus("Loading Service Desk...", Blue);
            List<B2BClient> clients = null;
            List<Employee> employees = null;
            List<ServiceDeskIncident> incidents = null;
            ServiceDeskSnapshot snapshot = null;

            var clientsTask = Task.Run(() => _clientService.GetAllClients());
            var employeesTask = Task.Run(() => _employeeService.GetActiveTechnicians());
            var incidentsTask = Task.Run(() => _service.GetAll());
            var snapshotTask = Task.Run(() => _service.GetSnapshot());

            await Task.WhenAll(clientsTask, employeesTask);
            clients = clientsTask.Result;
            employees = employeesTask.Result;

            if (IsDisposed)
                return;

            _clients = clients ?? new List<B2BClient>();
            _employees = employees ?? new List<Employee>();
            BindLookups();
            BindMailAccounts();

            await Task.WhenAll(incidentsTask, snapshotTask);
            incidents = incidentsTask.Result;
            snapshot = snapshotTask.Result;

            if (IsDisposed)
                return;

            _incidents = incidents ?? new List<ServiceDeskIncident>();
            _snapshot = snapshot;
            BindSnapshot();
            BindGrid();
            if (_incidents.Count > 0)
                LoadIncident(_incidents[0].IncidentId);
            else
                NewIncident();
            SetStatus("Service Desk ready.", Teal);
        }

        private void BindLookups()
        {
            _binding = true;
            _cmbClient.Items.Clear();
            _cmbClient.Items.Add(new ComboItem(0, ""));
            foreach (B2BClient client in _clients)
                _cmbClient.Items.Add(new ComboItem(client.ClientID, client.CompanyName));
            UIHelper.ShowEmptyClientsMessageIfNeeded(FindForm(), _clients, "ServiceDeskForm.BindLookups");
            _cmbClient.SelectedIndex = 0;

            _cmbAssigned.Items.Clear();
            _cmbAssigned.Items.Add(new ComboItem(0, ""));
            foreach (Employee employee in _employees)
                _cmbAssigned.Items.Add(new ComboItem(employee.EmployeeID, employee.Name));
            _cmbAssigned.SelectedIndex = 0;
            _binding = false;
        }

        private void BindSnapshot()
        {
            ServiceDeskSnapshot snap = _snapshot ?? _service.GetSnapshot();
            _lblOpen.Text = snap.OpenCount.ToString();
            _lblCritical.Text = snap.CriticalCount.ToString();
            _lblBreached.Text = snap.BreachedCount.ToString();
            _lblResolvedToday.Text = snap.ResolvedTodayCount.ToString();
        }

        private void BindGrid()
        {
            if (_grid == null)
                return;

            string search = (_txtSearch.Text ?? string.Empty).Trim();
            string filter = _cmbFilter.SelectedItem as string ?? "All";
            IEnumerable<ServiceDeskIncident> query = _incidents;

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(i => Contains(i.IncidentNumber, search) || Contains(i.ClientName, search) || Contains(i.ShortDescription, search) || Contains(i.AssignedEmployeeName, search));
            if (filter == "Open")
                query = query.Where(i => !IsClosed(i.Status));
            else if (filter == "My Work")
                query = query.Where(i => !IsClosed(i.Status) && Contains(i.AssignedEmployeeName, SessionManager.CurrentUser?.DisplayName));
            else if (filter == "Critical")
                query = query.Where(i => string.Equals(i.Priority, "Critical", StringComparison.OrdinalIgnoreCase));
            else if (filter == "SLA Breached")
                query = query.Where(i => i.SlaBreached);
            else if (filter == "Resolved")
                query = query.Where(i => string.Equals(i.Status, "Resolved", StringComparison.OrdinalIgnoreCase));
            else if (filter == "Closed")
                query = query.Where(i => string.Equals(i.Status, "Closed", StringComparison.OrdinalIgnoreCase));

            List<ServiceDeskIncident> rows = query.ToList();
            _grid.DataSource = rows;
            if (_emptyIncidentsPanel != null)
            {
                bool empty = rows.Count == 0;
                UpdateEmptyIncidentsPanel(empty);
                _grid.Visible = !empty;
                _emptyIncidentsPanel.Visible = empty;
                if (empty)
                    _emptyIncidentsPanel.BringToFront();
            }
        }

        private bool HasIncidentFilters()
        {
            string search = (_txtSearch?.Text ?? string.Empty).Trim();
            string filter = _cmbFilter?.SelectedItem as string ?? "All";
            return !string.IsNullOrWhiteSpace(search) || !string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);
        }

        private void ClearIncidentFilters()
        {
            if (_txtSearch == null || _cmbFilter == null)
                return;

            if (_txtSearch.TextLength > 0)
                _txtSearch.Clear();
            if (_cmbFilter.SelectedIndex != 0 && _cmbFilter.Items.Count > 0)
                _cmbFilter.SelectedIndex = 0;
            BindGrid();
            SetStatus("Incident filters cleared.", Blue);
        }

        private void UpdateEmptyIncidentsPanel(bool visible)
        {
            if (!visible || _lblEmptyIncidentsTitle == null || _lblEmptyIncidentsHint == null)
                return;

            bool filtered = HasIncidentFilters();
            _lblEmptyIncidentsTitle.Text = filtered ? "No matching incidents" : "No incidents yet";
            _lblEmptyIncidentsHint.Text = filtered
                ? "Clear the current search or filter to see the full queue."
                : "New incidents will appear here once they are logged.";
            if (_btnClearIncidentFilters != null)
                _btnClearIncidentFilters.Visible = filtered;
        }

        private void NewIncident()
        {
            _current = new ServiceDeskIncident
            {
                IncidentNumber = _service.GenerateIncidentNumber(),
                OpenedAt = DateTime.Now,
                Priority = "Medium",
                Status = "New",
                Category = "AC Breakdown",
                EquipmentType = "Split AC"
            };
            _current.SlaDueAt = ServiceDeskService.ComputeSlaDue(_current.Priority, _current.OpenedAt);
            BindIncident();
            _notesFlow.Controls.Clear();
            if (_emailsFlow != null)
                _emailsFlow.Controls.Clear();
            SetStatus("New incident draft.", Blue);
        }

        private void SelectCurrentGridIncident()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.DataBoundItem == null)
                return;
            ServiceDeskIncident row = _grid.CurrentRow.DataBoundItem as ServiceDeskIncident;
            if (row != null)
                LoadIncident(row.IncidentId);
        }

        private void LoadIncident(int id)
        {
            ServiceDeskDetail detail = _service.GetDetail(id);
            if (detail == null)
                return;
            _current = detail.Incident;
            BindIncident();
            BindNotes(detail.Notes);
            BindIncidentEmails();
        }

        private void BindIncident()
        {
            if (_current == null)
                return;

            _binding = true;
            _lblIncidentNumber.Text = _current.IncidentNumber;
            if (_lblBreadcrumb != null)
                _lblBreadcrumb.Text = "Incidents  >  " + _current.IncidentNumber;
            SelectComboById(_cmbClient, _current.ClientId ?? 0);
            LoadSitesForClient();
            SelectComboById(_cmbSite, _current.SiteId ?? 0);
            SelectComboById(_cmbAssigned, _current.AssignedEmployeeId ?? 0);
            SelectComboText(_cmbCategory, _current.Category);
            SelectComboText(_cmbEquipment, _current.EquipmentType);
            SelectComboText(_cmbPriority, _current.Priority);
            SelectComboText(_cmbStatus, _current.Status);
            _txtCaller.Text = _current.CallerName;
            _txtPhone.Text = _current.CallerPhone;
            _txtShortDescription.Text = _current.ShortDescription;
            _txtDescription.Text = _current.Description;
            _txtSerial.Text = _current.AssetSerialNumber;
            _txtRootCause.Text = _current.RootCause;
            _lblLinkedJob.Text = _current.LinkedJobId.HasValue ? "Linked work order: JOB-" + _current.LinkedJobId.Value : "No linked work order yet.";
            if (_lblMeta != null)
                _lblMeta.Text = "Created by " + (string.IsNullOrWhiteSpace(_current.CreatedByName) ? "Admin" : _current.CreatedByName) + "   |   " + _current.OpenedAt.ToString("dd MMM yyyy, HH:mm");
            RefreshSlaPreview();
            _binding = false;
        }

        private void SaveIncident()
        {
            try
            {
                ReadIncidentFromForm();
                int id = _service.Save(_current);
                _incidents = _service.GetAll();
                _snapshot = _service.GetSnapshot();
                BindSnapshot();
                BindGrid();
                LoadIncident(id);
                SetStatus("Incident saved.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ServiceDeskForm.SaveIncident", ex);
                SetStatus("Save failed: " + ex.Message, Red);
            }
        }

        private void CreateJob()
        {
            try
            {
                ReadIncidentFromForm();
                if (_current.IncidentId <= 0)
                    _current.IncidentId = _service.Save(_current);
                int jobId = _service.CreateJobFromIncident(_current);
                _incidents = _service.GetAll();
                BindGrid();
                LoadIncident(_current.IncidentId);
                SetStatus("Work order JOB-" + jobId + " created.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ServiceDeskForm.CreateJob", ex);
                SetStatus("Could not create job: " + ex.Message, Red);
            }
        }

        private void ChangeStatus(string status)
        {
            SelectComboText(_cmbStatus, status);
            SaveIncident();
        }

        private void AddNote()
        {
            if (_current == null || _current.IncidentId <= 0)
            {
                SetStatus("Save the incident before adding notes.", Red);
                return;
            }
            _service.AddNote(new ServiceDeskNote
            {
                IncidentId = _current.IncidentId,
                NoteType = _cmbNoteType.SelectedItem as string ?? "Work note",
                NoteText = _txtWorkNote.Text.Trim()
            });
            _txtWorkNote.Clear();
            LoadIncident(_current.IncidentId);
            SetStatus("Note added.", Teal);
        }

        private async Task ConnectMailAsync(string provider)
        {
            try
            {
                SetStatus("Opening " + provider + " sign-in...", Blue);
                await _mailService.ConnectAsync(provider);
                BindMailAccounts();
                SetStatus(provider + " connected.", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ServiceDeskForm.ConnectMail." + provider, ex);
                SetStatus(provider + " connect failed: " + ex.Message, Red);
                if (IsMailSetupMissing(ex))
                {
                    DialogResult result = MessageBox.Show(
                        "This ServoERP installation needs one-time " + provider + " app keys before browser login can start.\r\n\r\nThis is admin setup only. Staff will just use browser login after it is saved.\r\n\r\nOpen Admin Keys now?",
                        "Mail login setup required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);
                    if (result == DialogResult.Yes)
                        ShowMailSetupDialog();
                }
                else
                {
                    MessageBox.Show(provider + " connect failed:\r\n\r\n" + ex.Message, "Mail connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private async Task SyncMailAsync()
        {
            try
            {
                List<ConnectedMailAccount> accounts = _mailService.GetAccountsForCurrentUser();
                if (accounts.Count == 0)
                {
                    SetStatus("Connect Outlook or Gmail first.", Red);
                    MessageBox.Show("Connect an Outlook or Gmail account first, then run Sync Mail.", "Mail sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int created = 0;
                int updated = 0;
                int scanned = 0;
                foreach (ConnectedMailAccount account in accounts)
                {
                    SetStatus("Syncing " + account.Provider + " " + account.EmailAddress + "...", Blue);
                    MailSyncResult result = await _mailService.SyncAccountAsync(account.AccountId);
                    created += result.CreatedIncidents;
                    updated += result.UpdatedIncidents;
                    scanned += result.Scanned;
                }

                _incidents = _service.GetAll();
                _snapshot = _service.GetSnapshot();
                BindSnapshot();
                BindGrid();
                BindMailAccounts();
                BindIncidentEmails();
                SetStatus("Mail sync complete. Scanned " + scanned + ", created " + created + ", updated " + updated + ".", Teal);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ServiceDeskForm.SyncMail", ex);
                SetStatus("Mail sync failed: " + ex.Message, Red);
                MessageBox.Show("Mail sync failed:\r\n\r\n" + ex.Message, "Mail sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool IsMailSetupMissing(Exception ex)
        {
            return ex != null && ex.Message.IndexOf("ClientId", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowMailSetupDialog()
        {
            using (Form dialog = ServoModalForm.Create("Admin Mail Keys", 560, 285))
            {
                TableLayoutPanel layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18),
                    ColumnCount = 2,
                    RowCount = 7
                };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                for (int i = 1; i < 6; i++)
                    layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                Label intro = new Label
                {
                    Text = "One-time admin setup for browser login. After these keys are saved, users click Login Outlook or Login Gmail and the app remembers their mailbox.",
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = TextSecondary
                };
                layout.Controls.Add(intro, 0, 0);
                layout.SetColumnSpan(intro, 2);

                TextBox outlookTenant = AddMailSetupRow(layout, 1, "Outlook tenant", ConfigService.Get("Mail", "OutlookTenant", "common"));
                TextBox outlookClientId = AddMailSetupRow(layout, 2, "Outlook client ID", ConfigService.Get("Mail", "OutlookClientId", string.Empty));
                TextBox gmailClientId = AddMailSetupRow(layout, 3, "Gmail client ID", ConfigService.Get("Mail", "GmailClientId", string.Empty));
                TextBox gmailClientSecret = AddMailSetupRow(layout, 4, "Gmail client secret", ConfigService.Get("Mail", "GmailClientSecret", string.Empty));

                FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
                Button save = MakeButton("Save", Teal, White, 90);
                Button cancel = MakeButton("Cancel", White, TextPrimary, 90);
                save.DialogResult = DialogResult.OK;
                cancel.DialogResult = DialogResult.Cancel;
                actions.Controls.Add(save);
                actions.Controls.Add(cancel);
                layout.Controls.Add(actions, 0, 6);
                layout.SetColumnSpan(actions, 2);

                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                dialog.Controls.Add(layout);

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                ConfigService.Set("Mail", "OutlookTenant", string.IsNullOrWhiteSpace(outlookTenant.Text) ? "common" : outlookTenant.Text.Trim());
                ConfigService.Set("Mail", "OutlookClientId", outlookClientId.Text.Trim());
                ConfigService.Set("Mail", "GmailClientId", gmailClientId.Text.Trim());
                ConfigService.Set("Mail", "GmailClientSecret", gmailClientSecret.Text.Trim());
                SetStatus("Mail login keys saved. Users can login with Outlook or Gmail now.", Teal);
                MessageBox.Show("Mail login keys saved. Now click Login Outlook or Login Gmail.", "Mail login setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private TextBox AddMailSetupRow(TableLayoutPanel layout, int row, string labelText, string value)
        {
            Label label = new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary };
            TextBox textBox = new TextBox { Dock = DockStyle.Fill, Text = value ?? string.Empty, Font = new Font("Segoe UI", 9f), Margin = new Padding(0, 4, 0, 0) };
            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private void BindMailAccounts()
        {
            if (_mailAccountsFlow == null)
                return;

            _mailAccountsFlow.Controls.Clear();
            foreach (ConnectedMailAccount account in _mailService.GetAccountsForCurrentUser())
            {
                Panel card = new Panel { Width = 280, Height = 58, BackColor = White, Margin = new Padding(0, 0, 8, 8), Padding = new Padding(10) };
                card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, card.Width - 1, card.Height - 1);
                Button disconnect = new Button { Text = "x", Dock = DockStyle.Right, Width = 28, FlatStyle = FlatStyle.Flat, BackColor = White, ForeColor = Red };
                disconnect.FlatAppearance.BorderSize = 0;
                disconnect.Click += (s, e) =>
                {
                    _mailService.Disconnect(account.AccountId);
                    BindMailAccounts();
                    SetStatus("Mail account disconnected.", Teal);
                };
                card.Controls.Add(new Label { Text = account.Provider + " - " + account.EmailAddress + "\r\n" + (account.LastSyncStatus ?? "Connected"), Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary });
                card.Controls.Add(disconnect);
                _mailAccountsFlow.Controls.Add(card);
            }
        }

        private void BindIncidentEmails()
        {
            if (_emailsFlow == null)
                return;

            _emailsFlow.Controls.Clear();
            if (_current == null || _current.IncidentId <= 0)
                return;

            foreach (SyncedServiceDeskEmail email in _mailService.GetEmailsForIncident(_current.IncidentId))
            {
                Panel card = new Panel { Width = Math.Max(520, _emailsFlow.ClientSize.Width - 28), Height = 92, BackColor = White, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(12) };
                card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, card.Width - 1, card.Height - 1);
                card.Controls.Add(new Label { Text = email.BodyPreview, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), ForeColor = TextPrimary });
                card.Controls.Add(new Label { Text = email.Subject, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = TextPrimary });
                card.Controls.Add(new Label { Text = "From: " + (string.IsNullOrWhiteSpace(email.FromName) ? email.FromAddress : email.FromName + " <" + email.FromAddress + ">") + " | " + (email.ReceivedAtUtc.HasValue ? email.ReceivedAtUtc.Value.ToLocalTime().ToString("dd MMM HH:mm") : ""), Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8f), ForeColor = TextSecondary });
                _emailsFlow.Controls.Add(card);
            }
        }

        private void ReadIncidentFromForm()
        {
            if (_current == null)
                _current = new ServiceDeskIncident();
            _current.ClientId = SelectedId(_cmbClient);
            _current.SiteId = SelectedId(_cmbSite);
            _current.AssignedEmployeeId = SelectedId(_cmbAssigned);
            _current.CallerName = _txtCaller.Text.Trim();
            _current.CallerPhone = _txtPhone.Text.Trim();
            _current.Category = _cmbCategory.Text.Trim();
            _current.EquipmentType = _cmbEquipment.Text.Trim();
            _current.Priority = _cmbPriority.Text.Trim();
            _current.Status = _cmbStatus.Text.Trim();
            _current.ShortDescription = _txtShortDescription.Text.Trim();
            _current.Description = _txtDescription.Text.Trim();
            _current.AssetSerialNumber = _txtSerial.Text.Trim();
            _current.RootCause = _txtRootCause.Text.Trim();
            if (_current.OpenedAt == default(DateTime))
                _current.OpenedAt = DateTime.Now;
            _current.SlaDueAt = ServiceDeskService.ComputeSlaDue(_current.Priority, _current.OpenedAt);
        }

        private void OpenIncidentForms()
        {
            string query = string.Join(" ", new[]
            {
                "incident service report breakdown service call troubleshooting corrective action customer sign-off",
                _cmbCategory == null ? null : _cmbCategory.Text,
                _cmbEquipment == null ? null : _cmbEquipment.Text,
                _cmbPriority == null ? null : _cmbPriority.Text,
                _txtShortDescription == null ? null : _txtShortDescription.Text
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            FormTemplateWorkflowLauncher.Open(this, "Service Desk / Incidents", "Service Desk", "HVAC", query);
        }

        private void LoadSitesForClient()
        {
            if (_binding)
                return;
            int? clientId = SelectedId(_cmbClient);
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem(0, "No site / site not decided"));
            if (clientId.HasValue)
            {
                foreach (ClientSite site in _clientService.GetClientSites(clientId.Value))
                    _cmbSite.Items.Add(new ComboItem(site.SiteID, SiteService.GetDisplayName(site)));
            }
            _cmbSite.SelectedIndex = 0;
        }

        private void RefreshSlaPreview()
        {
            DateTime opened = _current == null || _current.OpenedAt == default(DateTime) ? DateTime.Now : _current.OpenedAt;
            DateTime due = ServiceDeskService.ComputeSlaDue(_cmbPriority.Text, opened);
            bool breached = DateTime.Now > due && !IsClosed(_cmbStatus.Text);
            _lblSla.Text = "SLA due: " + due.ToString("dd MMM yyyy, HH:mm") + (breached ? "  |  BREACHED" : "");
            _lblSla.ForeColor = breached ? Red : TextSecondary;
        }

        private void BindNotes(List<ServiceDeskNote> notes)
        {
            _notesFlow.Controls.Clear();
            foreach (ServiceDeskNote note in notes)
            {
                Panel panel = new Panel { Width = Math.Max(520, _notesFlow.ClientSize.Width - 28), Height = 78, BackColor = White, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(12) };
                panel.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, panel.Width - 1, panel.Height - 1);
                panel.Controls.Add(new Label { Text = note.NoteText, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = TextPrimary });
                panel.Controls.Add(new Label { Text = note.NoteType + " | " + note.CreatedByName + " | " + note.CreatedAt.ToString("dd MMM HH:mm"), Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = TextSecondary });
                _notesFlow.Controls.Add(panel);
            }
        }

        private TextBox AddText(TableLayoutPanel grid, int col, int row, string label)
        {
            Panel panel = FieldPanel(label, 64);
            Panel input = MakeInputHost();
            input.Dock = DockStyle.Top;
            input.Height = 34;
            input.Padding = label == "Caller phone" ? new Padding(34, 7, 10, 4) : new Padding(10, 7, 10, 4);
            if (label == "Caller phone")
            {
                Label phone = ModernIconSystem.Icon(ModernIconKind.Phone, 12, DS.Slate500);
                phone.Location = new Point(10, 7);
                phone.Size = new Size(18, 18);
                input.Controls.Add(phone);
            }
            TextBox txt = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.None, BackColor = White, ForeColor = TextPrimary };
            input.Controls.Add(txt);
            panel.Controls.Add(input);
            panel.Controls.Add(MakeFieldLabel(label));
            grid.Controls.Add(panel, col, row);
            return txt;
        }

        private TextBox AddMultiText(TableLayoutPanel grid, int col, int row, string label)
        {
            int height = label == "Description" ? 96 : 72;
            Panel panel = FieldPanel(label, height + 30);
            Panel input = MakeInputHost();
            input.Dock = DockStyle.Fill;
            input.Padding = new Padding(10, 7, 10, 7);
            TextBox txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.None, BackColor = White, ForeColor = TextPrimary };
            input.Controls.Add(txt);
            panel.Controls.Add(input);
            panel.Controls.Add(MakeFieldLabel(label));
            grid.Controls.Add(panel, col, row);
            return txt;
        }

        private ComboBox AddCombo(TableLayoutPanel grid, int col, int row, string label)
        {
            Panel panel = FieldPanel(label, 64);
            Panel input = MakeInputHost();
            input.Dock = DockStyle.Top;
            input.Height = 34;
            input.Padding = new Padding(8, 4, 8, 4);
            ComboBox cmb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), FlatStyle = FlatStyle.Flat, BackColor = White, ForeColor = TextPrimary };
            input.Controls.Add(cmb);
            panel.Controls.Add(input);
            panel.Controls.Add(MakeFieldLabel(label));
            grid.Controls.Add(panel, col, row);
            return cmb;
        }

        private Panel MakeFormSection(string title, ModernIconKind iconKind, int height, out Panel body)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Top,
                Height = height,
                BackColor = White,
                Padding = new Padding(16, 42, 16, 14)
            };
            card.Paint += (s, e) => PaintCardBorder(e.Graphics, card.ClientRectangle, 10);
            DS.Rounded(card, 10);

            Control icon = ModernIconSystem.Badge(iconKind, 24, DS.Primary50, DS.Primary600, 8);
            icon.Location = new Point(16, 12);
            Label heading = new Label
            {
                Text = title,
                Location = new Point(48, 14),
                Size = new Size(360, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = TextPrimary,
                BackColor = Color.Transparent
            };

            body = new Panel { Dock = DockStyle.Fill, BackColor = White };
            card.Controls.Add(body);
            card.Controls.Add(icon);
            card.Controls.Add(heading);
            heading.BringToFront();
            return card;
        }

        private TableLayoutPanel MakeFormGrid(int columns)
        {
            TableLayoutPanel form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = columns,
                Padding = Padding.Empty,
                BackColor = White
            };
            for (int i = 0; i < columns; i++)
                form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            return form;
        }

        private Panel MakeSpacer(int height)
        {
            return new Panel { Dock = DockStyle.Top, Height = height, BackColor = PageBg };
        }

        private Panel FieldPanel(string label, int height)
        {
            Panel panel = new Panel { Height = height, Dock = DockStyle.Fill, Margin = new Padding(8, 4, 8, 6), BackColor = White };
            return panel;
        }

        private Label MakeFieldLabel(string label)
        {
            bool required = label == "Client" || label == "Caller name" || label == "Caller phone" ||
                label == "Category" || label == "Equipment" || label == "Priority" || label == "Status" ||
                label == "Short description" || label == "Description";
            return new Label { Text = required ? label + " *" : label, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = required ? Red : TextSecondary };
        }

        private void EnsureSplitWidth()
        {
            if (_workspace == null || _workspace.Width <= 0)
                return;
        }

        private Label AddKpi(TableLayoutPanel table, int col, string title, Color color)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(col == 0 ? 0 : 6, 0, 0, 0), BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(10, 8, 10, 8) };
            card.Paint += (s, e) => PaintCardBorder(e.Graphics, card.ClientRectangle, 8);
            Label value = new Label { Text = "0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = color };
            card.Controls.Add(value);
            card.Controls.Add(new Label { Text = title.ToUpperInvariant(), Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = TextSecondary });
            table.Controls.Add(card, col, 0);
            return value;
        }

        private Panel MakeInputHost()
        {
            Panel panel = new Panel { BackColor = White, Padding = new Padding(10, 7, 10, 4) };
            panel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 6))
                using (Pen pen = new Pen(DS.BorderStrong))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(panel, 6);
            return panel;
        }

        private Panel BuildEmptyIncidentsPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BackColor = White, Visible = false };
            Panel content = new Panel { Size = new Size(230, 195), BackColor = Color.Transparent };
            content.Anchor = AnchorStyles.None;
            content.Location = new Point(95, 210);
            Panel icon = ModernIconSystem.EmptyStateIcon(ModernIconKind.Document, 70, Color.FromArgb(241, 245, 255), DS.Primary600);
            icon.Location = new Point(80, 0);
            _lblEmptyIncidentsTitle = new Label { Text = "No incidents yet", Location = new Point(0, 84), Size = new Size(230, 24), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate800, TextAlign = ContentAlignment.MiddleCenter };
            _lblEmptyIncidentsHint = new Label { Text = "New incidents will appear here once they are logged.", Location = new Point(0, 110), Size = new Size(230, 38), Font = new Font("Segoe UI", 8.25f), ForeColor = DS.Slate500, TextAlign = ContentAlignment.TopCenter };
            _btnClearIncidentFilters = MakeButton("Clear Filters", Color.FromArgb(239, 246, 255), DS.Primary700, 128);
            _btnClearIncidentFilters.Location = new Point(51, 154);
            _btnClearIncidentFilters.Visible = false;
            _btnClearIncidentFilters.Click += (s, e) => ClearIncidentFilters();
            content.Controls.Add(icon);
            content.Controls.Add(_lblEmptyIncidentsTitle);
            content.Controls.Add(_lblEmptyIncidentsHint);
            content.Controls.Add(_btnClearIncidentFilters);
            panel.Controls.Add(content);
            panel.Resize += (s, e) =>
            {
                content.Location = new Point(Math.Max(0, (panel.ClientSize.Width - content.Width) / 2), Math.Max(70, (panel.ClientSize.Height - content.Height) / 2));
            };
            return panel;
        }

        private void DrawIncidentTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = (TabControl)sender;
            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            using (SolidBrush brush = new SolidBrush(White))
                e.Graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, new Font("Segoe UI", 9f, selected ? FontStyle.Bold : FontStyle.Regular), bounds, selected ? Blue : TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Blue))
                    e.Graphics.FillRectangle(accent, bounds.Left + 12, bounds.Bottom - 3, bounds.Width - 24, 2);
            }
        }

        private void DrawInfoIcon(Graphics graphics, Point location, Color color)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(location, new Size(14, 14));
            using (SolidBrush brush = new SolidBrush(color))
                graphics.FillEllipse(brush, rect);
            TextRenderer.DrawText(graphics, "i", new Font("Segoe UI", 7f, FontStyle.Bold), rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 34, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            UIHelper.ApplyActionButton(button);
            return button;
        }

        private void PaintCardBorder(Graphics graphics, Rectangle bounds, int radius)
        {
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (GraphicsPath path = DS.RoundedRect(rect, radius))
            using (SolidBrush fill = new SolidBrush(White))
            using (Pen pen = new Pen(Border))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(pen, path);
            }
        }

        private void StyleGrid(DataGridView grid)
        {
            DS.StyleGrid(grid);
            grid.RowTemplate.Height = 32;
            grid.Cursor = Cursors.Hand;
        }

        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].DataPropertyName != "Priority" || e.Value == null)
                return;
            string value = e.Value.ToString();
            if (value == "Critical")
                e.CellStyle.ForeColor = Red;
            else if (value == "High")
                e.CellStyle.ForeColor = Amber;
            else
                e.CellStyle.ForeColor = TextPrimary;
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        private void ApplyPermissions()
        {
            PermissionUiHelper.ApplyModulePermissions("ServiceDesk", this, null, _btnSave, null);
        }

        private void SetStatus(string text, Color color)
        {
            if (_lblStatus == null)
                return;
            _lblStatus.Text = text;
            _lblStatus.ForeColor = color;
        }

        private static bool Contains(string value, string search) => (value ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool IsClosed(string status) => string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase);

        private static void SelectComboText(ComboBox combo, string text)
        {
            if (combo == null)
                return;
            int index = combo.FindStringExact(text ?? string.Empty);
            combo.SelectedIndex = index >= 0 ? index : (combo.Items.Count > 0 ? 0 : -1);
        }

        private static void SelectComboById(ComboBox combo, int id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                ComboItem item = combo.Items[i] as ComboItem;
                if (item != null && item.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static int? SelectedId(ComboBox combo)
        {
            ComboItem item = combo.SelectedItem as ComboItem;
            return item == null || item.Id <= 0 ? (int?)null : item.Id;
        }

        private sealed class ComboItem
        {
            public int Id { get; }
            public string Text { get; }
            public ComboItem(int id, string text) { Id = id; Text = text ?? string.Empty; }
            public override string ToString() => Text;
        }
    }
}

