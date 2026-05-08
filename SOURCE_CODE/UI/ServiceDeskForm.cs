using System;
using System.Collections.Generic;
using System.Drawing;
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
        private SplitContainer _split;
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
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                BackColor = Border
            };
            BuildLeft(_split.Panel1);
            BuildRight(_split.Panel2);
            Controls.Add(_split);
            Resize += (s, e) => EnsureSplitWidth();
            HandleCreated += (s, e) => BeginInvoke((Action)EnsureSplitWidth);
        }

        private void BuildLeft(Control parent)
        {
            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(16) };

            Panel header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = White, Padding = new Padding(12, 0, 12, 0) };
            header.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, header.Width - 1, header.Height - 1);
            DS.Rounded(header, 10);
            header.Controls.Add(new Label { Text = "INCIDENT OVERVIEW", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleLeft });

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(18, 0, 18, 10), ColumnCount = 4, BackColor = White };
            for (int i = 0; i < 4; i++)
                kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            _lblOpen = AddKpi(kpis, 0, "Open", Blue);
            _lblCritical = AddKpi(kpis, 1, "Critical", Red);
            _lblBreached = AddKpi(kpis, 2, "SLA Breached", Amber);
            _lblResolvedToday = AddKpi(kpis, 3, "Resolved Today", Teal);

            Panel filters = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(12, 8, 12, 8), BackColor = White };
            _txtSearch = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), BorderStyle = BorderStyle.FixedSingle };
            _txtSearch.TextChanged += (s, e) => BindGrid();
            _cmbFilter = new ComboBox { Dock = DockStyle.Left, Width = 92, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f) };
            _cmbFilter.Items.AddRange(new object[] { "All", "Open", "My Work", "Critical", "SLA Breached", "Resolved", "Closed" });
            _cmbFilter.SelectedIndex = 0;
            _cmbFilter.SelectedIndexChanged += (s, e) => BindGrid();
            Button filterButton = MakeButton("Filter", White, DS.Slate700, 58);
            filterButton.Dock = DockStyle.Right;
            filters.Controls.Add(_txtSearch);
            filters.Controls.Add(_cmbFilter);
            filters.Controls.Add(filterButton);

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
                AllowUserToDeleteRows = false
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "No", DataPropertyName = "IncidentNumber", Width = 92 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Priority", DataPropertyName = "Priority", Width = 76 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 92 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Client", DataPropertyName = "ClientName", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Issue", DataPropertyName = "ShortDescription", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.SelectionChanged += (s, e) => SelectCurrentGridIncident();
            _grid.CellFormatting += GridCellFormatting;
            StyleGrid(_grid);

            Panel gridCard = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(12) };
            gridCard.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, gridCard.Width - 1, gridCard.Height - 1);
            DS.Rounded(gridCard, 10);
            gridCard.Controls.Add(_grid);
            gridCard.Controls.Add(new Label { Text = "Recent Incidents", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = DS.Slate900 });
            left.Controls.Add(gridCard);
            left.Controls.Add(filters);
            left.Controls.Add(kpis);
            left.Controls.Add(header);
            parent.Controls.Add(left);
        }

        private void BuildRight(Control parent)
        {
            Panel right = new Panel { Dock = DockStyle.Fill, BackColor = PageBg, Padding = new Padding(18) };

            Panel top = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = White, Padding = new Padding(18, 10, 18, 10) };
            top.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, top.Width - 1, top.Height - 1);
            DS.Rounded(top, 10);
            Panel titleBlock = new Panel { Dock = DockStyle.Fill, BackColor = White };
            _lblIncidentNumber = new Label { Text = "New Incident", Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleLeft };
            _lblSla = new Label { Text = "Create a new service incident and assign it to the right team.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary, TextAlign = ContentAlignment.MiddleLeft };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 520, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            _btnSave = MakeButton("Save", Teal, White, 90);
            _btnCreateJob = MakeButton("Create Job", Blue, White, 110);
            _btnStart = MakeButton("Start Work", White, Blue, 100);
            _btnResolve = MakeButton("Resolve", White, Teal, 92);
            _btnClose = MakeButton("Close", White, Red, 82);
            actions.Controls.AddRange(new Control[] { _btnSave, _btnCreateJob, _btnClose, _btnResolve, _btnStart });
            _btnSave.Click += (s, e) => SaveIncident();
            _btnCreateJob.Click += (s, e) => CreateJob();
            _btnStart.Click += (s, e) => ChangeStatus("In Progress");
            _btnResolve.Click += (s, e) => ChangeStatus("Resolved");
            _btnClose.Click += (s, e) => ChangeStatus("Closed");
            titleBlock.Controls.Add(_lblSla);
            titleBlock.Controls.Add(_lblIncidentNumber);
            top.Controls.Add(titleBlock);
            top.Controls.Add(actions);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            tabs.TabPages.Add(BuildIncidentTab());
            tabs.TabPages.Add(BuildNotesTab());
            tabs.TabPages.Add(BuildEmailTab());

            _lblStatus = new Label { Dock = DockStyle.Bottom, Height = 24, Font = new Font("Segoe UI", 9f), ForeColor = TextSecondary };
            right.Controls.Add(tabs);
            right.Controls.Add(_lblStatus);
            right.Controls.Add(top);
            parent.Controls.Add(right);
        }

        private TabPage BuildIncidentTab()
        {
            TabPage page = new TabPage("Incident");
            Panel host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg, Padding = new Padding(0, 14, 0, 0) };
            TableLayoutPanel form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _cmbClient = AddCombo(form, 0, 0, "Client");
            _cmbSite = AddCombo(form, 1, 0, "Site");
            _txtCaller = AddText(form, 0, 1, "Caller name");
            _txtPhone = AddText(form, 1, 1, "Caller phone");
            _cmbCategory = AddCombo(form, 0, 2, "Category");
            _cmbEquipment = AddCombo(form, 1, 2, "Equipment");
            _cmbPriority = AddCombo(form, 0, 3, "Priority");
            _cmbStatus = AddCombo(form, 1, 3, "Status");
            _cmbAssigned = AddCombo(form, 0, 4, "Assigned technician");
            _txtSerial = AddText(form, 1, 4, "Asset / serial number");
            _txtShortDescription = AddText(form, 0, 5, "Short description");
            form.SetColumnSpan(form.GetControlFromPosition(0, 5), 2);
            _txtDescription = AddMultiText(form, 0, 6, "Description");
            form.SetColumnSpan(form.GetControlFromPosition(0, 6), 2);
            _txtRootCause = AddMultiText(form, 0, 7, "Root cause / resolution");
            form.SetColumnSpan(form.GetControlFromPosition(0, 7), 2);
            _lblLinkedJob = new Label { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Blue, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0) };

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
                Height = 34,
                BackColor = Color.FromArgb(239, 246, 255),
                ForeColor = Blue,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };
            host.Controls.Add(info);
            host.Controls.Add(form);
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

            _grid.DataSource = query.ToList();
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
            using (Form dialog = new Form())
            {
                dialog.AutoScaleMode = AutoScaleMode.Dpi;
                dialog.Text = "Admin Mail Keys";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ClientSize = new Size(560, 285);
                dialog.BackColor = White;

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

        private void LoadSitesForClient()
        {
            if (_binding)
                return;
            int? clientId = SelectedId(_cmbClient);
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem(0, ""));
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
            Panel panel = FieldPanel(label, 74);
            TextBox txt = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle };
            panel.Controls.Add(txt);
            panel.Controls.Add(MakeFieldLabel(label));
            grid.Controls.Add(panel, col, row);
            return txt;
        }

        private TextBox AddMultiText(TableLayoutPanel grid, int col, int row, string label)
        {
            Panel panel = FieldPanel(label, 118);
            TextBox txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle, ScrollBars = ScrollBars.Vertical };
            panel.Controls.Add(txt);
            panel.Controls.Add(MakeFieldLabel(label));
            grid.Controls.Add(panel, col, row);
            return txt;
        }

        private ComboBox AddCombo(TableLayoutPanel grid, int col, int row, string label)
        {
            Panel panel = FieldPanel(label, 74);
            ComboBox cmb = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
            panel.Controls.Add(cmb);
            panel.Controls.Add(MakeFieldLabel(label));
            grid.Controls.Add(panel, col, row);
            return cmb;
        }

        private Panel FieldPanel(string label, int height)
        {
            Panel panel = new Panel { Height = height, Dock = DockStyle.Top, Margin = new Padding(8), BackColor = PageBg };
            return panel;
        }

        private Label MakeFieldLabel(string label)
        {
            bool required = label == "Client" || label == "Site" || label == "Caller name" || label == "Caller phone" ||
                label == "Category" || label == "Equipment" || label == "Priority" || label == "Status" ||
                label == "Short description" || label == "Description";
            return new Label { Text = required ? label + " *" : label, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = required ? Red : TextSecondary };
        }

        private void EnsureSplitWidth()
        {
            if (_split == null || _split.Width <= 0)
                return;

            if (_split.Width > 900)
            {
                _split.Panel1MinSize = 260;
                _split.Panel2MinSize = 420;
            }

            int desired = Math.Min(520, Math.Max(420, _split.Width / 3));
            int maxAllowed = Math.Max(_split.Panel1MinSize, _split.Width - _split.Panel2MinSize);
            desired = Math.Min(desired, maxAllowed);
            desired = Math.Max(_split.Panel1MinSize, desired);
            if (desired >= _split.Panel1MinSize && desired <= _split.Width - _split.Panel2MinSize
                && (_split.SplitterDistance < 300 || Math.Abs(_split.SplitterDistance - desired) > 80))
            {
                try { _split.SplitterDistance = desired; } catch { }
            }
        }

        private Label AddKpi(TableLayoutPanel table, int col, string title, Color color)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(col == 0 ? 0 : 8, 0, 0, 0), BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(12) };
            card.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, card.Width - 1, card.Height - 1);
            Label value = new Label { Text = "0", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = color };
            card.Controls.Add(value);
            card.Controls.Add(new Label { Text = title.ToUpperInvariant(), Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = TextSecondary });
            table.Controls.Add(card, col, 0);
            return value;
        }

        private Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button { Text = text, Width = width, Height = 32, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(6, 0, 0, 0), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = back == White ? 1 : 0;
            return button;
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

