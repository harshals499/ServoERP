using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class JobDetailPage : BaseUserControl
    {
        private static readonly Color White = Color.White;
        private static readonly Color PageBg = Color.FromArgb(248, 250, 252);
        private static readonly Color Border = DS.Border;
        private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private static readonly Color Teal = Color.FromArgb(29, 158, 117);
        private static readonly Color Blue = Color.FromArgb(24, 95, 165);
        private static readonly Color Red = Color.FromArgb(226, 75, 74);

        private readonly JobService _jobService = new JobService();
        private readonly ClientService _clientService = new ClientService();
        private readonly SiteService _siteService = new SiteService();
        private readonly ContractService _contractService = new ContractService();
        private readonly EmployeeService _employeeService = new EmployeeService();

        private Label _lblJobNumber;
        private FlowLayoutPanel _pipeline;
        private FlowLayoutPanel _leftStack;
        private FlowLayoutPanel _rightStack;
        private TextBox _txtTitle;
        private ComboBox _cmbType;
        private DateTimePicker _dtpScheduled;
        private ComboBox _cmbClient;
        private ComboBox _cmbSite;
        private ComboBox _cmbContract;
        private ComboBox _cmbTechnician;
        private ComboBox _cmbPriority;
        private TextBox _txtLabour;
        private TextBox _txtTravel;
        private Label _lblParts;
        private Label _lblProfit;
        private Label _lblMargin;
        private DataGridView _gridChecklist;
        private DataGridView _gridParts;
        private AccordionPanel _checklistAccordion;
        private AccordionPanel _partsAccordion;

        private JobDetailDto _detail;
        private bool _binding;

        public int JobId { get; set; }
        public Action<int> OnBackToJobs { get; set; }

        public JobDetailPage()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
        }

        /// <summary>Applies shared load behaviour and keeps table-cell inputs visibly outlined.</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyVisibleInputFallbacks(this);
        }

        public void LoadJob()
        {
            if (JobId <= 0)
                throw new InvalidOperationException("Job id is required.");

            try
            {
                Job job = _jobService.GetById(JobId);
                _detail = _jobService.GetJobDetail(JobId);
                if (_detail == null && job == null)
                    throw new Exception("Job not found.");
                if (_detail == null)
                    _detail = new JobDetailDto { Job = job };

                BindJob();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("JobDetailPage.LoadJob", ex);
                throw;
            }
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = White, Padding = new Padding(16, 12, 16, 10) };
            top.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, top.Height - 1, top.Width, top.Height - 1);

            Button btnBack = MakeButton("<- Back to Jobs", White, TextPrimary, 126);
            btnBack.Dock = DockStyle.Left;
            btnBack.FlatAppearance.BorderColor = Border;
            btnBack.Click += (s, e) => OnBackToJobs?.Invoke(JobId);

            _lblJobNumber = new Label
            {
                Dock = DockStyle.Fill,
                Text = "JOB",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0)
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 340,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = White,
                Padding = new Padding(0, 4, 0, 0)
            };
            Button btnClose = MakeButton("Close Job", Red, White, 96);
            Button btnPrint = MakeButton("Print Report", Blue, White, 112);
            Button btnSave = MakeButton("Save", Teal, White, 86);
            btnSave.Click += (s, e) => SaveJob();
            btnPrint.Click += (s, e) => PrintReport();
            btnClose.Click += (s, e) => CloseJob();
            actions.Controls.AddRange(new Control[] { btnClose, btnPrint, btnSave });

            top.Controls.Add(_lblJobNumber);
            top.Controls.Add(actions);
            top.Controls.Add(btnBack);

            _pipeline = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = White,
                Padding = new Padding(20, 10, 20, 8),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true
            };

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                ColumnCount = 2,
                Padding = new Padding(16)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280f));

            _leftStack = MakeStack();
            _rightStack = MakeStack();
            body.Controls.Add(_leftStack, 0, 0);
            body.Controls.Add(_rightStack, 1, 0);

            BuildLeftColumn();
            BuildRightColumn();
            ApplyVisibleInputFallbacks(body);

            Controls.Add(body);
            Controls.Add(_pipeline);
            Controls.Add(top);
            ApplyVisibleInputFallbacks(this);
        }

        private void BuildLeftColumn()
        {
            Panel detailsBody;
            Control details = MakeCard("Job details", out detailsBody, 244);
            TableLayoutPanel form = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 6, Height = 210 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _txtTitle = new TextBox { Dock = DockStyle.Fill };
            _cmbType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbType.Items.AddRange(new object[] { "PM Visit", "Breakdown", "Installation", "AMC Visit", "Gas Charging", "General" });
            _dtpScheduled = new DateTimePicker { Dock = DockStyle.Left, Width = 150, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
            _cmbClient = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbSite = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbContract = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbClient.SelectedIndexChanged += (s, e) =>
            {
                if (_binding)
                    return;
                int clientId = GetSelectedId(_cmbClient);
                BindSitesAndContracts(clientId, 0, 0);
            };

            AddFormRow(form, 0, "Title", _txtTitle);
            AddFormRow(form, 1, "Type", _cmbType);
            AddFormRow(form, 2, "Scheduled Date", _dtpScheduled);
            AddFormRow(form, 3, "Client", _cmbClient);
            AddFormRow(form, 4, "Site", _cmbSite);
            AddFormRow(form, 5, "Linked Contract", _cmbContract);
            detailsBody.Controls.Add(form);

            Panel checklistBody;
            Control checklist = MakeCard("Job checklist", out checklistBody, 234);
            _gridChecklist = MakeGrid();
            _gridChecklist.Columns.Add("Item", "Item");
            _gridChecklist.Columns.Add("Done", "Done");
            _gridChecklist.Columns.Add("CompletedBy", "Completed By");
            _gridChecklist.Columns.Add("CompletedDate", "Completed Date");
            checklistBody.Controls.Add(_gridChecklist);

            Panel partsBody;
            Control parts = MakeCard("Parts used", out partsBody, 204);
            _gridParts = MakeGrid();
            _gridParts.Columns.Add("Item", "Item");
            _gridParts.Columns.Add("Qty", "Qty");
            _gridParts.Columns.Add("Unit", "Unit");
            _gridParts.Columns.Add("Cost", "Cost");
            partsBody.Controls.Add(_gridParts);

            _leftStack.Controls.Add(details);
            _leftStack.Controls.Add(checklist);
            _leftStack.Controls.Add(parts);
        }

        private void BuildRightColumn()
        {
            Panel techBody;
            Control tech = MakeCard("Technician", out techBody, 82);
            _cmbTechnician = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            techBody.Controls.Add(WrapEditor(_cmbTechnician));

            Panel priorityBody;
            Control priority = MakeCard("Priority", out priorityBody, 62);
            _cmbPriority = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbPriority.Items.AddRange(new object[] { "Low", "Medium", "High" });
            priorityBody.Controls.Add(WrapEditor(_cmbPriority));

            Panel costBody;
            Control cost = MakeCard("Job cost", out costBody, 194);
            TableLayoutPanel table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 5, Height = 170 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            _txtTravel = AddCostRow(table, 0, "Travel", true);
            _txtLabour = AddCostRow(table, 1, "Labour", true);
            _lblParts = AddValueRow(table, 2, "Parts");
            _lblProfit = AddValueRow(table, 3, "Est. Profit");
            _lblMargin = AddValueRow(table, 4, "Margin");
            costBody.Controls.Add(table);

            _rightStack.Controls.Add(tech);
            _rightStack.Controls.Add(priority);
            _rightStack.Controls.Add(cost);
        }

        private void BindJob()
        {
            Job job = _detail.Job;
            _binding = true;
            try
            {
                _lblJobNumber.Text = job.JobNumber ?? ("JOB-" + job.JobID);
                BindClients(job.ClientID);
                BindSitesAndContracts(job.ClientID, job.SiteID, job.LinkedContractId ?? 0);
                BindTechnicians(job.AssignedEmployeeID ?? 0);

                _txtTitle.Text = job.JobTitle ?? job.Title ?? string.Empty;
                SelectText(_cmbType, string.IsNullOrWhiteSpace(job.JobType) ? "General" : job.JobType);
                _dtpScheduled.Value = job.ScheduledDate == default(DateTime) ? DateTime.Today : job.ScheduledDate;
                SelectText(_cmbPriority, string.IsNullOrWhiteSpace(job.Priority) ? "Medium" : job.Priority);
                _txtTravel.Text = _detail.TravelCost.ToString("0.##");
                _txtLabour.Text = _detail.LabourCost.ToString("0.##");
                _lblParts.Text = IndiaFormatHelper.FormatCurrency(_detail.PartsCost);
                _lblProfit.Text = IndiaFormatHelper.FormatCurrency(_detail.EstimatedProfit);
                _lblMargin.Text = _detail.EstimatedMarginPct.ToString("0.##") + "%";
                _lblMargin.ForeColor = _detail.EstimatedMarginPct < 15m ? Red : Teal;
            }
            finally
            {
                _binding = false;
            }

            RenderPipeline(job.PipelineStatus);
            RenderChecklist();
            RenderParts();
        }

        private void BindClients(int selectedClientId)
        {
            _cmbClient.Items.Clear();
            List<B2BClient> clients = _clientService.GetAllClients();
            foreach (B2BClient client in clients.OrderBy(c => c.CompanyName))
                _cmbClient.Items.Add(new ComboItem(client.ClientID, client.CompanyName));
            UIHelper.ShowEmptyClientsMessageIfNeeded(FindForm(), clients, "JobDetailPage.BindClients");
            SelectValue(_cmbClient, selectedClientId);
        }

        private void BindSitesAndContracts(int clientId, int selectedSiteId, int selectedContractId)
        {
            _cmbSite.Items.Clear();
            _cmbSite.Items.Add(new ComboItem(0, "-- No site / site not decided --"));
            foreach (ClientSite site in _siteService.GetByClientId(clientId).OrderBy(s => s.SiteName))
                _cmbSite.Items.Add(new ComboItem(site.SiteID, SiteService.GetDisplayName(site)));
            SelectValue(_cmbSite, selectedSiteId);

            _cmbContract.Items.Clear();
            _cmbContract.Items.Add(new ComboItem(0, "-- No linked contract --"));
            foreach (AMCContract contract in _contractService.GetContractsByClient(clientId).OrderBy(c => c.ContractID))
                _cmbContract.Items.Add(new ComboItem(contract.ContractID, "AMC-" + contract.ContractID + " - " + (contract.ContractType ?? "Contract")));
            SelectValue(_cmbContract, selectedContractId);
        }

        private void BindTechnicians(int selectedEmployeeId)
        {
            _cmbTechnician.Items.Clear();
            _cmbTechnician.Items.Add(new ComboItem(0, "-- Unassigned --"));
            foreach (Employee employee in _employeeService.GetActiveTechnicians().OrderBy(e => e.Name))
                _cmbTechnician.Items.Add(new ComboItem(employee.EmployeeID, employee.Name));
            SelectValue(_cmbTechnician, selectedEmployeeId);
        }

        private void RenderPipeline(string currentStatus)
        {
            _pipeline.Controls.Clear();
            string current = NormalizePipeline(currentStatus);
            string[] steps = { "Created", "Assigned", "InProgress", "ChecklistDone", "Closed", "Invoiced" };
            for (int i = 0; i < steps.Length; i++)
            {
                bool active = string.Equals(steps[i], current, StringComparison.OrdinalIgnoreCase);
                Label step = new Label
                {
                    AutoSize = false,
                    Width = i == 2 || i == 3 ? 134 : 96,
                    Height = 30,
                    Text = GetPipelineLabel(steps[i]),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = active ? Color.FromArgb(232, 248, 241) : White,
                    ForeColor = active ? Teal : TextSecondary,
                    Font = new Font("Segoe UI", 8.5f, active ? FontStyle.Bold : FontStyle.Regular),
                    Margin = new Padding(0, 0, 6, 0)
                };
                step.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(active ? Teal : Border), 0, 0, step.Width - 1, step.Height - 1);
                _pipeline.Controls.Add(step);
            }
        }

        private void RenderChecklist()
        {
            _gridChecklist.Rows.Clear();
            int completed = 0;
            int total = 0;
            foreach (JobChecklistItem item in _detail.ChecklistItems.OrderBy(i => i.SortOrder))
            {
                total++;
                if (item.IsCompleted)
                    completed++;
                _gridChecklist.Rows.Add(
                    item.ItemText,
                    item.IsCompleted ? "Yes" : "No",
                    item.CompletedBy ?? string.Empty,
                    IndiaFormatHelper.FormatDate(item.CompletedDate));
            }
            if (_checklistAccordion != null)
            {
                _checklistAccordion.ShowBadge = true;
                _checklistAccordion.BadgeText = completed + "/" + total + " done";
            }
        }

        private void RenderParts()
        {
            _gridParts.Rows.Clear();
            foreach (JobPartUsed part in _detail.PartsUsed)
            {
                _gridParts.Rows.Add(
                    part.ItemDescription,
                    part.QuantityUsed.ToString("0.###"),
                    part.Unit,
                    IndiaFormatHelper.FormatCurrency(part.TotalCost));
            }
            if (_partsAccordion != null)
            {
                _partsAccordion.ShowBadge = true;
                _partsAccordion.BadgeText = _detail.PartsUsed.Count + " parts";
            }
        }

        private void SaveJob()
        {
            try
            {
                Job job = _detail.Job;
                job.JobTitle = (_txtTitle.Text ?? string.Empty).Trim();
                job.Title = job.JobTitle;
                job.JobType = _cmbType.SelectedItem == null ? "General" : _cmbType.SelectedItem.ToString();
                job.ScheduledDate = _dtpScheduled.Value.Date;
                job.ClientID = GetSelectedId(_cmbClient);
                job.SiteID = GetSelectedId(_cmbSite);
                int contractId = GetSelectedId(_cmbContract);
                job.LinkedContractId = contractId > 0 ? (int?)contractId : null;
                int techId = GetSelectedId(_cmbTechnician);
                job.AssignedEmployeeID = techId > 0 ? (int?)techId : null;
                job.Priority = _cmbPriority.SelectedItem == null ? "Medium" : _cmbPriority.SelectedItem.ToString();
                job.EstimatedCost = ParseMoney(_txtLabour.Text);
                job.Revenue = job.QuotedRevenue;

                _jobService.Update(job);
                LoadJob();
                MessageBox.Show("Job saved.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("JobDetailPage.SaveJob", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Saving job", ex);
            }
        }

        private void CloseJob()
        {
            if (_detail == null || _detail.Job == null)
                return;

            DialogResult confirm = MessageBox.Show(
                "Close job " + (_detail.Job.JobNumber ?? string.Empty) + "?",
                "Close Job",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                decimal revenue = _detail.Job.ActualRevenue > 0 ? _detail.Job.ActualRevenue : Math.Max(_detail.Job.QuotedRevenue, _detail.Job.Revenue);
                _jobService.CloseJob(JobId, revenue, "Closed from Job detail page.", false);
                LoadJob();
                MessageBox.Show("Job closed successfully.", "Jobs", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("JobDetailPage.CloseJob", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Closing job", ex);
            }
        }

        private void PrintReport()
        {
            try
            {
                Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\REPORTS\Jobs");
                string safeName = string.IsNullOrWhiteSpace(_detail.Job.JobNumber) ? "job-" + JobId : _detail.Job.JobNumber;
                string path = Path.Combine(@"C:\HVAC_PRO_MSE\REPORTS\Jobs", safeName + ".html");
                File.WriteAllText(path, BuildReportHtml(), Encoding.UTF8);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("JobDetailPage.PrintReport", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Printing report", ex);
            }
        }

        private string BuildReportHtml()
        {
            Job job = _detail.Job;
            return "<html><head><meta charset='utf-8'><title>" + Html(job.JobNumber) + "</title>"
                + "<style>body{font-family:Segoe UI,Arial;margin:32px;color:#111827}h1{margin:0 0 4px}table{border-collapse:collapse;width:100%;margin-top:18px}td,th{border:1px solid #d1d5db;padding:8px;text-align:left}.muted{color:#64748b}.num{text-align:right}</style>"
                + "</head><body><h1>" + Html(job.JobNumber) + "</h1><div class='muted'>" + Html(job.JobTitle ?? job.Title) + "</div>"
                + "<table><tr><th>Status</th><td>" + Html(GetPipelineLabel(job.PipelineStatus)) + "</td><th>Scheduled</th><td>" + Html(IndiaFormatHelper.FormatDate(job.ScheduledDate)) + "</td></tr>"
                + "<tr><th>Client</th><td>" + Html(_detail.Client?.CompanyName) + "</td><th>Site</th><td>" + Html(_detail.Site?.SiteName) + "</td></tr>"
                + "<tr><th>Technician</th><td>" + Html(_detail.Technician?.Name ?? "Unassigned") + "</td><th>Priority</th><td>" + Html(job.Priority) + "</td></tr></table>"
                + "<table><tr><th>Travel</th><th>Parts</th><th>Labour</th><th>Est. Profit</th><th>Margin</th></tr><tr>"
                + "<td class='num'>" + Html(IndiaFormatHelper.FormatCurrency(_detail.TravelCost)) + "</td>"
                + "<td class='num'>" + Html(IndiaFormatHelper.FormatCurrency(_detail.PartsCost)) + "</td>"
                + "<td class='num'>" + Html(IndiaFormatHelper.FormatCurrency(_detail.LabourCost)) + "</td>"
                + "<td class='num'>" + Html(IndiaFormatHelper.FormatCurrency(_detail.EstimatedProfit)) + "</td>"
                + "<td class='num'>" + Html(_detail.EstimatedMarginPct.ToString("0.##") + "%") + "</td></tr></table></body></html>";
        }

        private static FlowLayoutPanel MakeStack()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = PageBg,
                Padding = new Padding(0, 0, 10, 20)
            };
        }

        /// <summary>Restores native borders for editable inputs in the job detail editor.</summary>
        private static void ApplyVisibleInputFallbacks(Control root)
        {
            if (root == null)
                return;

            foreach (Control child in root.Controls)
            {
                TextBoxBase textBox = child as TextBoxBase;
                if (textBox != null && textBox.BorderStyle == BorderStyle.None)
                    textBox.BorderStyle = BorderStyle.FixedSingle;

                ComboBox combo = child as ComboBox;
                if (combo != null && combo.FlatStyle == FlatStyle.Flat)
                    combo.FlatStyle = FlatStyle.Standard;

                NumericUpDown numeric = child as NumericUpDown;
                if (numeric != null && numeric.BorderStyle == BorderStyle.None)
                    numeric.BorderStyle = BorderStyle.FixedSingle;

                ApplyVisibleInputFallbacks(child);
            }
        }

        private Control MakeCard(string title, out Panel body, int contentHeight)
        {
            AccordionPanel card = new AccordionPanel
            {
                Width = 700,
                Height = 36 + contentHeight,
                HeaderText = title,
                HeaderTextColor = TextPrimary,
                HeaderBorderColor = Border,
                HeaderFontSize = 10,
                BackColor = White,
                Margin = new Padding(0, 0, 0, 14)
            };
            body = card.ContentPanel;
            body.BackColor = White;
            body.Padding = new Padding(14, 10, 14, 14);
            body.Tag = "NO_CARD_SURFACE";
            card.Resize += (s, e) => { if (card.Parent != null) card.Width = Math.Max(240, card.Parent.ClientSize.Width - 28); };
            if (string.Equals(title, "Job checklist", StringComparison.OrdinalIgnoreCase))
            {
                _checklistAccordion = card;
                card.ShowBadge = true;
                card.BadgeText = "0/0 done";
            }
            if (string.Equals(title, "Parts used", StringComparison.OrdinalIgnoreCase))
            {
                _partsAccordion = card;
                card.ShowBadge = true;
                card.BadgeText = "0 parts";
            }
            card.SetExpanded(true);
            return card;
        }

        private static Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(6, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        private static DataGridView MakeGrid()
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            GridTheme.Apply(grid);
            return grid;
        }

        private static void AddFormRow(TableLayoutPanel table, int row, string label, Control editor)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextSecondary }, 0, row);
            table.Controls.Add(WrapEditor(editor), 1, row);
        }

        private TextBox AddCostRow(TableLayoutPanel table, int row, string label, bool editable)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextSecondary }, 0, row);
            TextBox box = new TextBox { Dock = DockStyle.Fill, ReadOnly = !editable, TextAlign = HorizontalAlignment.Right };
            table.Controls.Add(WrapEditor(box), 1, row);
            return box;
        }

        /// <summary>Wraps editor controls in an outline host recognised by the shared input painter.</summary>
        private static Panel WrapEditor(Control editor)
        {
            Panel host = new Panel
            {
                Name = "JobDetailFieldHost",
                Dock = DockStyle.Fill,
                BackColor = DS.BgInput,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(0, 1, 0, 1)
            };

            if (editor != null)
            {
                editor.Dock = DockStyle.Fill;
                host.Controls.Add(editor);
            }

            return host;
        }

        private Label AddValueRow(TableLayoutPanel table, int row, string label)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextSecondary }, 0, row);
            Label value = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = TextPrimary, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            table.Controls.Add(value, 1, row);
            return value;
        }

        private static void SelectValue(ComboBox combo, int value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                ComboItem item = combo.Items[i] as ComboItem;
                if (item != null && item.Value == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static void SelectText(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0)
                combo.SelectedIndex = combo.Items.Count - 1;
        }

        private static int GetSelectedId(ComboBox combo)
        {
            ComboItem item = combo.SelectedItem as ComboItem;
            return item == null ? 0 : item.Value;
        }

        private static decimal ParseMoney(string text)
        {
            decimal value;
            string cleaned = (text ?? string.Empty).Replace(",", string.Empty).Replace("₹", string.Empty).Trim();
            return decimal.TryParse(cleaned, out value) ? value : 0m;
        }

        private static string NormalizePipeline(string value)
        {
            string normalized = (value ?? string.Empty).Replace(" ", string.Empty).Trim();
            switch (normalized.ToUpperInvariant())
            {
                case "ASSIGNED": return "Assigned";
                case "INPROGRESS": return "InProgress";
                case "CHECKLISTDONE": return "ChecklistDone";
                case "CLOSED": return "Closed";
                case "INVOICED": return "Invoiced";
                default: return "Created";
            }
        }

        private static string GetPipelineLabel(string value)
        {
            switch (NormalizePipeline(value))
            {
                case "InProgress": return "In Progress";
                case "ChecklistDone": return "Checklist Done";
                case "Invoiced": return "Invoice";
                default: return NormalizePipeline(value);
            }
        }

        private static string Html(string value)
        {
            return System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }

        private sealed class ComboItem
        {
            public ComboItem(int value, string text)
            {
                Value = value;
                Text = text;
            }

            public int Value { get; private set; }
            public string Text { get; private set; }
            public override string ToString()
            {
                return Text;
            }
        }
    }
}


