using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public class PayrollForm : DeferredPageControl
    {
        private readonly PayrollService _payrollService = new PayrollService();
        private readonly AttendanceService _attendanceService = new AttendanceService();
        private readonly PayslipService _payslipService = new PayslipService();
        private readonly PayrollReportService _reportService = new PayrollReportService();
        private readonly PayrollDataImportService _importService = new PayrollDataImportService();
        private readonly EmployeeService _employeeService = new EmployeeService();

        private ComboBox _cmbMonth;
        private ComboBox _cmbYear;
        private Label _lblStatus;
        private Button _btnImport;
        private TabControl _tabs;

        private DataGridView _gridProcess;
        private Label _lblSummaryEmployees;
        private Label _lblSummaryGross;
        private Label _lblSummaryNet;
        private Label _lblSummaryLiability;
        private Label _lblKpiEmployees;
        private Label _lblKpiGross;
        private Label _lblKpiDeductions;
        private Label _lblKpiNet;
        private Label _lblKpiLiability;
        private Button _btnGeneratePayslips;

        private TextBox _txtSalarySearch;
        private ListBox _lstSalaryEmployees;
        private NumericUpDown _numBasic;
        private NumericUpDown _numDa;
        private NumericUpDown _numHra;
        private NumericUpDown _numSpecial;
        private NumericUpDown _numConveyance;
        private NumericUpDown _numMedical;
        private NumericUpDown _numLta;
        private NumericUpDown _numOther;
        private DateTimePicker _dtStructureFrom;
        private DataGridView _gridSalaryHistory;
        private Label _lblSalaryValidation;

        private DataGridView _gridAttendance;
        private DataGridView _gridStatutory;

        private ListBox _lstDetailEmployees;
        private DataGridView _gridPayslipHistory;
        private DataGridView _gridTds;
        private DataGridView _gridLoans;
        private DataGridView _gridDetailSalaryHistory;

        private List<Employee> _employees = new List<Employee>();
        private bool _isInitializing;

        public PayrollForm()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 247, 250);
            BuildLayout();
            UIHelper.ApplyInputStyles(Controls);
            EnableDeferredLoad(
                () =>
                {
                    LoadEmployees();
                    RefreshAll();
                },
                ex => SetStatus("Payroll load error: " + ex.Message, Color.Firebrick));
        }

        private void BuildLayout()
        {
            _isInitializing = true;
            Controls.Clear();
            BackColor = DS.BgPage;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = DS.BgPage, Padding = new Padding(22, 14, 22, 10) };
            Label title = new Label
            {
                Text = "PAYROLL",
                Location = new Point(22, 12),
                Size = new Size(360, 30),
                ForeColor = DS.Slate900,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label subtitle = new Label
            {
                Text = "Manage and process employee payroll.",
                Location = new Point(23, 47),
                Size = new Size(440, 22),
                ForeColor = DS.Slate600,
                Font = DS.Body,
                TextAlign = ContentAlignment.MiddleLeft
            };

            FlowLayoutPanel headerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 654,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button btnMore = NewButton("More", Point.Empty, 92, Color.White);
            btnMore.ForeColor = DS.Slate700;
            btnMore.FlatAppearance.BorderSize = 1;
            btnMore.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnImport = NewButton("Import Historical Data", Point.Empty, 172, Color.White);
            _btnImport.ForeColor = DS.Slate700;
            _btnImport.FlatAppearance.BorderSize = 1;
            _btnImport.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnForms = NewButton("Forms", Point.Empty, 86, Color.White);
            btnForms.ForeColor = DS.Primary600;
            btnForms.FlatAppearance.BorderSize = 1;
            btnForms.FlatAppearance.BorderColor = DS.BorderStrong;
            ModernIconSystem.AddButtonIcon(btnForms, ModernIconKind.Document);
            Button btnLock = NewButton("Lock Payroll", Point.Empty, 118, Color.White);
            btnLock.ForeColor = DS.Slate700;
            btnLock.FlatAppearance.BorderSize = 1;
            btnLock.FlatAppearance.BorderColor = DS.BorderStrong;
            Button btnRun = NewButton("Run Payroll", Point.Empty, 118, DS.Primary600);
            foreach (Button button in new[] { btnMore, _btnImport, btnForms, btnLock, btnRun })
                button.Margin = new Padding(8, 0, 0, 0);
            btnRun.Click += (s, e) => RunPayroll();
            btnLock.Click += (s, e) => LockCurrentPayroll();
            _btnImport.Click += (s, e) => ImportHistoricalData();
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Payroll", "Payroll", null, "technician attendance leave request salary approval payroll job costing sheet payment receipt");
            btnMore.Click += (s, e) => ShowPayrollActionsMenu(btnMore, btnRun, btnLock, btnForms);
            headerActions.Controls.AddRange(new Control[] { btnMore, _btnImport, btnForms, btnLock, btnRun });
            header.Controls.AddRange(new Control[] { title, subtitle, headerActions });

            Panel workspace = new Panel { Dock = DockStyle.Fill, BackColor = DS.BgPage, Padding = new Padding(18, 0, 18, 18) };
            Panel shell = MakePayrollCard();
            shell.Dock = DockStyle.Fill;
            shell.Padding = new Padding(0);

            Panel periodStrip = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.White, Padding = new Padding(18, 14, 18, 14) };
            periodStrip.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, periodStrip.Height - 1, periodStrip.Width, periodStrip.Height - 1);
            };
            Label monthLabel = new Label { Text = "Payroll Month *", Location = new Point(18, 12), Size = new Size(160, 20), Font = DS.SmallBold, ForeColor = DS.Slate600 };
            _cmbMonth = NewCombo(new Point(18, 38), 150, Enumerable.Range(1, 12).Select(i => new DateTime(2000, i, 1).ToString("MMMM")).ToArray());
            _cmbMonth.SelectedIndex = DateTime.Today.Month - 1;
            _cmbYear = NewCombo(new Point(176, 38), 92, Enumerable.Range(DateTime.Today.Year - 3, 7).Select(y => y.ToString()).ToArray());
            _cmbYear.SelectedItem = DateTime.Today.Year.ToString();
            Label openBadge = new Label
            {
                Text = "Open",
                Location = new Point(286, 39),
                Size = new Size(74, 30),
                Font = DS.BodyBold,
                ForeColor = DS.Green600,
                BackColor = DS.Green50,
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(openBadge, 8);
            _lblStatus = new Label
            {
                Text = "Run payroll to calculate salaries for the selected month.",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(420, 35),
                Size = new Size(Math.Max(320, periodStrip.Width - 450), 36),
                Font = DS.Body,
                ForeColor = DS.Primary700,
                BackColor = DS.Primary50,
                Padding = new Padding(14, 8, 12, 0)
            };
            DS.Rounded(_lblStatus, 6);
            periodStrip.Resize += (s, e) =>
            {
                _lblStatus.Width = Math.Max(260, periodStrip.ClientSize.Width - _lblStatus.Left - 18);
            };
            periodStrip.Controls.AddRange(new Control[] { monthLabel, _cmbMonth, _cmbYear, openBadge, _lblStatus });

            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9) };
            _tabs.TabPages.Add(BuildProcessTab());
            _tabs.TabPages.Add(BuildSalaryTab());
            _tabs.TabPages.Add(BuildAttendanceTab());
            _tabs.TabPages.Add(BuildStatutoryTab());
            _tabs.TabPages.Add(BuildDetailsTab());

            shell.Controls.Add(_tabs);
            shell.Controls.Add(periodStrip);
            workspace.Controls.Add(shell);
            Controls.Add(workspace);
            Controls.Add(header);
            _isInitializing = false;
        }

        private void ShowPayrollActionsMenu(Control anchor, Button runButton, Button lockButton, Button formsButton)
        {
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddPayrollAction(menu, "Run Payroll", (s, e) => runButton.PerformClick());
            AddPayrollAction(menu, "Lock Payroll", (s, e) => lockButton.PerformClick());
            menu.Items.Add(new ToolStripSeparator());
            AddPayrollAction(menu, "Generate Payslip", (s, e) => GenerateAllPayslips());
            AddPayrollAction(menu, "Export Payroll Register", (s, e) => ExportPayrollRegister());
            AddPayrollAction(menu, "Import Historical Data", (s, e) => ImportHistoricalData());
            AddPayrollAction(menu, "Open Payroll Forms", (s, e) => formsButton.PerformClick());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void AddPayrollAction(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            menu.Items.Add(item);
        }

        private TabPage BuildProcessTab()
        {
            var tab = new TabPage("Process Payroll") { BackColor = DS.BgPage, Padding = new Padding(14) };

            Panel summary = new Panel { Dock = DockStyle.Bottom, Height = 92, BackColor = Color.White, Padding = new Padding(18, 16, 18, 16) };
            summary.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawLine(pen, 0, 0, summary.Width, 0);
            };
            _lblSummaryEmployees = AddSummary(summary, "Total Employees", new Point(20, 24));
            _lblSummaryGross = AddSummary(summary, "Total Gross", new Point(280, 24));
            _lblSummaryNet = AddSummary(summary, "Total Net Pay", new Point(560, 24));
            _lblSummaryLiability = AddSummary(summary, "Total Employer Liability", new Point(860, 24));

            Panel topButtons = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = DS.BgPage, Padding = new Padding(0, 8, 0, 10) };
            _btnGeneratePayslips = NewButton("Generate Payslip", new Point(0, 8), 148, DS.Primary600);
            _btnGeneratePayslips.AutoSize = false;
            Button btnExport = NewButton("Export Payroll Register", new Point(160, 8), 190, DS.Green600);
            Button btnRecalc = NewButton("Recalculate Selected", new Point(362, 8), 178, DS.Primary600);
            _btnGeneratePayslips.Click += (s, e) => GenerateAllPayslips();
            btnExport.Click += (s, e) => ExportPayrollRegister();
            btnRecalc.Click += (s, e) => RecalculateSelected();
            topButtons.Controls.AddRange(new Control[] { _btnGeneratePayslips, btnExport, btnRecalc });

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, Height = 104, BackColor = DS.BgPage, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 0, 0, 10) };
            for (int i = 0; i < 5; i++)
                kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            kpis.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            kpis.Controls.Add(MakePayrollKpi("Total Employees", "0", "Active Employees", DS.Primary600, out _lblKpiEmployees), 0, 0);
            kpis.Controls.Add(MakePayrollKpi("Total Gross", "₹0.00", "Gross payroll", DS.Green600, out _lblKpiGross), 1, 0);
            kpis.Controls.Add(MakePayrollKpi("Total Deductions", "₹0.00", "Statutory + other", DS.Amber500, out _lblKpiDeductions), 2, 0);
            kpis.Controls.Add(MakePayrollKpi("Total Net Pay", "₹0.00", "Payable to staff", DS.Primary600, out _lblKpiNet), 3, 0);
            kpis.Controls.Add(MakePayrollKpi("Employer Liability", "₹0.00", "Company contribution", Color.FromArgb(124, 58, 237), out _lblKpiLiability), 4, 0);

            _gridProcess = NewGrid();
            _gridProcess.Dock = DockStyle.Fill;
            _gridProcess.Columns.Add("EntryId", "EntryId");
            _gridProcess.Columns["EntryId"].Visible = false;
            foreach (string column in new[] { "Name", "Designation", "Days Present", "Gross", "EPF(Emp)", "ESI(Emp)", "TDS", "PT", "Deductions", "Net Pay" })
                _gridProcess.Columns.Add(column, column);
            _gridProcess.Columns["EPF(Emp)"].DefaultCellStyle.Format = "₹#,##0.00";
            _gridProcess.Columns["ESI(Emp)"].DefaultCellStyle.Format = "₹#,##0.00";
            _gridProcess.Columns["Net Pay"].DefaultCellStyle.Font = new Font(_gridProcess.Font, FontStyle.Bold);

            Panel gridWrap = MakePayrollCard();
            gridWrap.Dock = DockStyle.Fill;
            gridWrap.Padding = new Padding(0);
            gridWrap.Controls.Add(_gridProcess);

            tab.Controls.Add(gridWrap);
            tab.Controls.Add(summary);
            tab.Controls.Add(topButtons);
            tab.Controls.Add(kpis);
            return tab;
        }

        private TabPage BuildSalaryTab()
        {
            var tab = new TabPage("Salary Structures") { BackColor = BackColor };
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 300, BackColor = BackColor, Padding = new Padding(12) };
            split.Panel1.Padding = new Padding(0, 0, 8, 0);
            split.Panel2.Padding = new Padding(8, 0, 0, 0);

            var searchWrap = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 14, 14, 14) };
            searchWrap.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, searchWrap.Width - 1, searchWrap.Height - 1);
            };
            var employeesTitle = new Label { Text = "Employees", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            var employeesSubtitle = new Label { Text = "Search by employee name or code", Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.Gray };
            _txtSalarySearch = new TextBox { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10), Font = new Font("Segoe UI", 9), Height = 28 };
            _txtSalarySearch.TextChanged += (s, e) => BindEmployeeLists();
            _lstSalaryEmployees = new ListBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9) };
            _lstSalaryEmployees.SelectedIndexChanged += (s, e) => LoadSalaryDetails();
            searchWrap.Controls.Add(_lstSalaryEmployees);
            searchWrap.Controls.Add(_txtSalarySearch);
            searchWrap.Controls.Add(employeesSubtitle);
            searchWrap.Controls.Add(employeesTitle);
            split.Panel1.Controls.Add(searchWrap);

            Panel form = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, Padding = new Padding(18, 18, 18, 18) };
            form.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, form.Width - 1, form.Height - 1);
            };
            var titleLabel = new Label { Text = "Salary Structure Editor", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            var subtitleLabel = new Label { Text = "Update effective date and monthly earning components for the selected employee.", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.Gray };

            TableLayoutPanel fieldGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                Padding = new Padding(0, 8, 0, 0)
            };
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _dtStructureFrom = AddDateField(fieldGrid, "Effective From");
            _numBasic = AddAmountField(fieldGrid, "Basic Salary");
            _numDa = AddAmountField(fieldGrid, "DA");
            _numHra = AddAmountField(fieldGrid, "HRA");
            _numSpecial = AddAmountField(fieldGrid, "Special Allowance");
            _numConveyance = AddAmountField(fieldGrid, "Conveyance Allowance");
            _numMedical = AddAmountField(fieldGrid, "Medical Allowance");
            _numLta = AddAmountField(fieldGrid, "LTA");
            _numOther = AddAmountField(fieldGrid, "Other Allowances");
            _gridSalaryHistory = NewGrid();
            _gridSalaryHistory.Height = 260;
            _gridSalaryHistory.Dock = DockStyle.Top;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 14, 0, 0),
                WrapContents = false
            };
            Button btnAddStructure = NewButton("Add New Structure", new Point(0, 0), 136, Color.FromArgb(41, 128, 185));
            Button btnSaveStructure = NewButton("Save Structure", new Point(0, 0), 122, Color.FromArgb(39, 174, 96));
            btnAddStructure.Click += (s, e) => ClearSalaryForm();
            btnSaveStructure.Click += (s, e) => SaveSalaryStructure();
            actions.Controls.Add(btnAddStructure);
            actions.Controls.Add(btnSaveStructure);

            _lblSalaryValidation = new Label { Dock = DockStyle.Top, Height = 34, ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 8, FontStyle.Bold), Padding = new Padding(0, 10, 0, 0) };

            form.Controls.Add(_gridSalaryHistory);
            form.Controls.Add(_lblSalaryValidation);
            form.Controls.Add(actions);
            form.Controls.Add(fieldGrid);
            form.Controls.Add(subtitleLabel);
            form.Controls.Add(titleLabel);
            split.Panel2.Controls.Add(form);
            tab.Controls.Add(split);
            return tab;
        }

        private TabPage BuildAttendanceTab()
        {
            var tab = new TabPage("Attendance") { BackColor = BackColor };
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = BackColor };
            Button btnMarkAll = NewButton("Mark All Present", new Point(10, 6), 124, Color.FromArgb(41, 128, 185));
            Button btnImportCsv = NewButton("Import from CSV", new Point(140, 6), 118, Color.FromArgb(52, 152, 219));
            Button btnSave = NewButton("Save Attendance", new Point(264, 6), 116, Color.FromArgb(39, 174, 96));
            btnMarkAll.Click += (s, e) => MarkAllAttendancePresent();
            btnImportCsv.Click += (s, e) => ImportAttendanceCsv();
            btnSave.Click += (s, e) => SaveAttendanceGrid();
            toolbar.Controls.AddRange(new Control[] { btnMarkAll, btnImportCsv, btnSave });

            _gridAttendance = NewGrid();
            _gridAttendance.Dock = DockStyle.Fill;
            tab.Controls.Add(_gridAttendance);
            tab.Controls.Add(toolbar);
            return tab;
        }

        private TabPage BuildStatutoryTab()
        {
            var tab = new TabPage("Statutory Payments") { BackColor = BackColor };
            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = BackColor };
            Button btnMarkPaid = NewButton("Mark Paid", new Point(10, 6), 92, Color.FromArgb(39, 174, 96));
            Button btnEpf = NewButton("EPF ECR", new Point(108, 6), 86, Color.FromArgb(41, 128, 185));
            Button btnEsi = NewButton("ESI Statement", new Point(200, 6), 98, Color.FromArgb(52, 152, 219));
            Button btn24q = NewButton("Form 24Q Data", new Point(304, 6), 110, Color.FromArgb(142, 68, 173));
            Button btnPt = NewButton("PT Register", new Point(420, 6), 92, Color.FromArgb(230, 126, 34));
            btnMarkPaid.Click += (s, e) => MarkSelectedStatutoryPaid();
            btnEpf.Click += (s, e) => ExportEpf();
            btnEsi.Click += (s, e) => ExportEsi();
            btn24q.Click += (s, e) => Export24Q();
            btnPt.Click += (s, e) => ExportPt();
            toolbar.Controls.AddRange(new Control[] { btnMarkPaid, btnEpf, btnEsi, btn24q, btnPt });

            _gridStatutory = NewGrid();
            _gridStatutory.Dock = DockStyle.Fill;
            _gridStatutory.Columns.Add("PaymentId", "PaymentId");
            _gridStatutory.Columns["PaymentId"].Visible = false;
            foreach (string column in new[] { "Type", "Amount", "Due Date", "Status", "Reference", "Notes" })
                _gridStatutory.Columns.Add(column, column);
            tab.Controls.Add(_gridStatutory);
            tab.Controls.Add(toolbar);
            return tab;
        }

        private TabPage BuildDetailsTab()
        {
            var tab = new TabPage("Employee Payroll Details") { BackColor = BackColor };
            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 280 };
            _lstDetailEmployees = new ListBox { Dock = DockStyle.Fill };
            _lstDetailEmployees.SelectedIndexChanged += (s, e) => LoadEmployeeDetails();
            split.Panel1.Controls.Add(_lstDetailEmployees);

            TabControl detailTabs = new TabControl { Dock = DockStyle.Fill };
            _gridDetailSalaryHistory = NewGrid();
            _gridPayslipHistory = NewGrid();
            _gridTds = NewGrid();
            _gridLoans = NewGrid();
            detailTabs.TabPages.Add(NewGridTab("Salary History", _gridDetailSalaryHistory));
            detailTabs.TabPages.Add(NewGridTab("Payslip History", _gridPayslipHistory));
            detailTabs.TabPages.Add(NewGridTab("TDS Summary", _gridTds));

            TabPage loansTab = new TabPage("Loans / Advances") { BackColor = Color.White };
            Panel loanToolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White };
            Button btnLoan = NewButton("Add Loan", new Point(10, 6), 88, Color.FromArgb(41, 128, 185));
            Button btnAdvance = NewButton("Add Advance", new Point(104, 6), 98, Color.FromArgb(230, 126, 34));
            btnLoan.Click += (s, e) => AddLoan();
            btnAdvance.Click += (s, e) => AddAdvance();
            loanToolbar.Controls.AddRange(new Control[] { btnLoan, btnAdvance });
            _gridLoans.Dock = DockStyle.Fill;
            loansTab.Controls.Add(_gridLoans);
            loansTab.Controls.Add(loanToolbar);
            detailTabs.TabPages.Add(loansTab);

            TabPage form16Tab = new TabPage("Form 16") { BackColor = Color.White };
            Button btnForm16 = NewButton("Generate Form 16", new Point(20, 20), 132, Color.FromArgb(39, 174, 96));
            btnForm16.Click += (s, e) => GenerateForm16();
            form16Tab.Controls.Add(btnForm16);
            detailTabs.TabPages.Add(form16Tab);

            split.Panel2.Controls.Add(detailTabs);
            tab.Controls.Add(split);
            return tab;
        }

        private void LoadEmployees()
        {
            _employees = _employeeService.GetAll().OrderBy(e => e.Name).ToList();
            BindEmployeeLists();
        }

        private void BindEmployeeLists()
        {
            string term = (_txtSalarySearch?.Text ?? string.Empty).Trim();
            IEnumerable<Employee> filtered = _employees;
            if (!string.IsNullOrWhiteSpace(term))
                filtered = filtered.Where(e => (e.Name ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || (e.EmployeeCode ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            BindEmployeeList(_lstSalaryEmployees, filtered.ToList());
            BindEmployeeList(_lstDetailEmployees, filtered.ToList());
        }

        private void RefreshAll()
        {
            if (_isInitializing || _gridProcess == null || _gridAttendance == null || _gridStatutory == null || _btnImport == null)
                return;
            RefreshProcessTab();
            LoadAttendanceTab();
            RefreshStatutoryTab();
            _btnImport.Visible = string.Equals(SessionManager.CurrentUser?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase) && !_importService.IsHistoricalImportCompleted();
        }

        private void RefreshProcessTab()
        {
            if (_gridProcess == null || _lblSummaryEmployees == null || _lblSummaryGross == null || _lblSummaryNet == null || _lblSummaryLiability == null)
                return;
            _gridProcess.Rows.Clear();
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run == null)
            {
                SetStatus("No payroll run for selected month.", Color.Gray);
                UpdateSummary(new PayrollSummaryDto());
                return;
            }

            foreach (PayrollEntry entry in _payrollService.GetPayrollEntriesByRun(run.PayrollRunId))
            {
                _gridProcess.Rows.Add(entry.EntryId, entry.EmployeeName, entry.Designation, entry.DaysPresent.ToString("0.##"), IndiaFormatHelper.FormatCurrency(entry.GrossSalary), IndiaFormatHelper.FormatCurrency(entry.EPFEmployee), IndiaFormatHelper.FormatCurrency(entry.ESIEmployee), IndiaFormatHelper.FormatCurrency(entry.TDSDeducted), IndiaFormatHelper.FormatCurrency(entry.ProfessionalTax), IndiaFormatHelper.FormatCurrency(entry.TotalDeductions), IndiaFormatHelper.FormatCurrency(entry.NetSalary));
            }

            UpdateSummary(_payrollService.GetPayrollSummary(CurrentMonth, CurrentYear));
            SetStatus("Current payroll status: " + run.Status, string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase) ? Color.Firebrick : Color.Gray);
        }

        private void UpdateSummary(PayrollSummaryDto summary)
        {
            if (_lblSummaryEmployees == null || _lblSummaryGross == null || _lblSummaryNet == null || _lblSummaryLiability == null)
                return;
            summary = summary ?? new PayrollSummaryDto();
            _lblSummaryEmployees.Text = "Total Employees\n" + summary.TotalEmployees;
            _lblSummaryGross.Text = "Total Gross\n" + IndiaFormatHelper.FormatCurrency(summary.TotalGross);
            _lblSummaryNet.Text = "Total Net Pay\n" + IndiaFormatHelper.FormatCurrency(summary.TotalNet);
            _lblSummaryLiability.Text = "Total Employer Liability\n" + IndiaFormatHelper.FormatCurrency(summary.TotalEmployerLiability);

            decimal totalDeductions = Math.Max(0, summary.TotalGross - summary.TotalNet);
            if (_lblKpiEmployees != null)
                _lblKpiEmployees.Text = summary.TotalEmployees.ToString();
            if (_lblKpiGross != null)
                _lblKpiGross.Text = IndiaFormatHelper.FormatCurrency(summary.TotalGross);
            if (_lblKpiDeductions != null)
                _lblKpiDeductions.Text = IndiaFormatHelper.FormatCurrency(totalDeductions);
            if (_lblKpiNet != null)
                _lblKpiNet.Text = IndiaFormatHelper.FormatCurrency(summary.TotalNet);
            if (_lblKpiLiability != null)
                _lblKpiLiability.Text = IndiaFormatHelper.FormatCurrency(summary.TotalEmployerLiability);
        }

        private void RunPayroll()
        {
            ServiceResult<PayrollRun> result = _payrollService.ProcessMonthlyPayroll(CurrentMonth, CurrentYear);
            SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
            if (!result.Success)
                MessageBox.Show(result.Message, "Payroll", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshAll();
        }

        private void LockCurrentPayroll()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run == null)
                return;
            ServiceResult<bool> result = _payrollService.LockPayroll(run.PayrollRunId);
            SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
            RefreshAll();
        }

        private void ImportHistoricalData()
        {
            ServiceResult<PayrollImportReport> result = _importService.ImportFromSourceFolder();
            MessageBox.Show(result.Success ? result.Message : result.Message, result.Success ? "Import Complete" : "Import Failed", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            LoadEmployees();
            RefreshAll();
        }

        private async void GenerateAllPayslips()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run == null)
                return;
            using (Form progress = BuildBusyDialog("Generating payslips", "Payslips are being generated in the background. You can keep the app open while the batch finishes."))
            {
                ToggleBusyState(true);
                SetStatus("Generating payslips for " + CurrentMonth.ToString("00") + "/" + CurrentYear + "...", Color.FromArgb(41, 128, 185));
                progress.Show(this);
                progress.Update();
                ServiceResult<List<string>> result = await Task.Run(() => _payslipService.GenerateBatchPayslips(run.PayrollRunId));
                progress.Close();
                ToggleBusyState(false);
                SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
                RefreshProcessTab();
                if (!result.Success)
                    MessageBox.Show(result.Message, "Payslips", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExportPayrollRegister()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run == null)
                return;
            ServiceResult<string> result = _reportService.GeneratePayrollRegister(run.PayrollRunId);
            OpenFileIfExists(result);
        }

        private void RecalculateSelected()
        {
            if (_gridProcess.CurrentRow == null)
                return;
            int entryId = Convert.ToInt32(_gridProcess.CurrentRow.Cells["EntryId"].Value);
            ServiceResult<PayrollEntry> result = _payrollService.RecalculateSingleEmployee(entryId);
            SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
            RefreshProcessTab();
        }

        private void LoadSalaryDetails()
        {
            Employee employee = _lstSalaryEmployees.SelectedItem as Employee;
            if (employee == null)
                return;
            List<SalaryStructure> history = _payrollService.GetSalaryStructures(employee.EmployeeID);
            SalaryStructure current = history.FirstOrDefault();
            if (current != null)
            {
                _dtStructureFrom.Value = current.EffectiveFrom;
                _numBasic.Value = current.BasicSalary;
                _numDa.Value = current.DA;
                _numHra.Value = current.HRA;
                _numSpecial.Value = current.SpecialAllowance;
                _numConveyance.Value = current.ConveyanceAllowance;
                _numMedical.Value = current.MedicalAllowance;
                _numLta.Value = current.LTA;
                _numOther.Value = current.OtherAllowances;
            }

            BindSalaryHistoryGrid(_gridSalaryHistory, history);
        }

        private void ClearSalaryForm()
        {
            _dtStructureFrom.Value = DateTime.Today;
            foreach (NumericUpDown control in new[] { _numBasic, _numDa, _numHra, _numSpecial, _numConveyance, _numMedical, _numLta, _numOther })
                control.Value = 0;
            _lblSalaryValidation.Text = string.Empty;
        }

        private void SaveSalaryStructure()
        {
            Employee employee = _lstSalaryEmployees.SelectedItem as Employee;
            if (employee == null)
                return;
            var structure = new SalaryStructure
            {
                EmployeeId = employee.EmployeeID,
                EffectiveFrom = _dtStructureFrom.Value.Date,
                BasicSalary = _numBasic.Value,
                DA = _numDa.Value,
                HRA = _numHra.Value,
                SpecialAllowance = _numSpecial.Value,
                ConveyanceAllowance = _numConveyance.Value,
                MedicalAllowance = _numMedical.Value,
                LTA = _numLta.Value,
                OtherAllowances = _numOther.Value,
                IsActive = true
            };
            ServiceResult<int> result = _payrollService.SaveSalaryStructure(structure);
            _lblSalaryValidation.Text = result.Message;
            _lblSalaryValidation.ForeColor = result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick;
            LoadSalaryDetails();
        }

        private void LoadAttendanceTab()
        {
            if (_gridAttendance == null)
                return;
            _gridAttendance.Columns.Clear();
            _gridAttendance.Rows.Clear();
            _gridAttendance.Columns.Add("EmployeeId", "EmployeeId");
            _gridAttendance.Columns["EmployeeId"].Visible = false;
            _gridAttendance.Columns.Add("Employee", "Employee");
            for (int day = 1; day <= 31; day++)
            {
                var col = new DataGridViewComboBoxColumn { Name = "D" + day, HeaderText = day.ToString(), Width = 46, DataSource = new[] { "", "Present", "Absent", "Leave", "Holiday", "WeekOff", "HalfDay" } };
                _gridAttendance.Columns.Add(col);
            }

            Dictionary<string, AttendanceRecord> existing = _attendanceService.GetMonthlyAttendanceRecords(CurrentMonth, CurrentYear)
                .ToDictionary(a => a.EmployeeId + "|" + a.AttendanceDate.Day, a => a);
            foreach (Employee employee in _employees)
            {
                int row = _gridAttendance.Rows.Add(employee.EmployeeID, employee.Name);
                for (int day = 1; day <= DateTime.DaysInMonth(CurrentYear, CurrentMonth); day++)
                {
                    AttendanceRecord record;
                    if (existing.TryGetValue(employee.EmployeeID + "|" + day, out record))
                        _gridAttendance.Rows[row].Cells["D" + day].Value = record.Status;
                }
            }
        }

        private void MarkAllAttendancePresent()
        {
            foreach (DataGridViewRow row in _gridAttendance.Rows)
            {
                for (int day = 1; day <= DateTime.DaysInMonth(CurrentYear, CurrentMonth); day++)
                    row.Cells["D" + day].Value = "Present";
            }
        }

        private void SaveAttendanceGrid()
        {
            foreach (DataGridViewRow row in _gridAttendance.Rows)
            {
                if (row.IsNewRow)
                    continue;
                int employeeId = Convert.ToInt32(row.Cells["EmployeeId"].Value);
                for (int day = 1; day <= DateTime.DaysInMonth(CurrentYear, CurrentMonth); day++)
                {
                    string status = Convert.ToString(row.Cells["D" + day].Value);
                    if (string.IsNullOrWhiteSpace(status))
                        continue;
                    _attendanceService.SaveAttendanceRecord(new AttendanceRecord { EmployeeId = employeeId, AttendanceDate = new DateTime(CurrentYear, CurrentMonth, day), Status = status, OvertimeHours = 0m });
                }
            }
            SetStatus("Attendance saved.", Color.FromArgb(39, 174, 96));
        }

        private void ImportAttendanceCsv()
        {
            using (var dialog = new OpenFileDialog { Filter = "CSV Files|*.csv" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                ServiceResult<int> result = _attendanceService.ImportAttendanceFromCsv(dialog.FileName);
                SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
                LoadAttendanceTab();
            }
        }

        private void RefreshStatutoryTab()
        {
            if (_gridStatutory == null)
                return;
            _gridStatutory.Rows.Clear();
            foreach (StatutoryPayment payment in _payrollService.GetStatutoryPaymentsByMonth(CurrentMonth, CurrentYear))
                _gridStatutory.Rows.Add(payment.PaymentId, payment.PaymentType, IndiaFormatHelper.FormatCurrency(payment.Amount), IndiaFormatHelper.FormatDate(payment.DueDate), payment.Status, payment.ReferenceNumber, payment.Notes);
        }

        private void MarkSelectedStatutoryPaid()
        {
            if (_gridStatutory.CurrentRow == null)
                return;
            int paymentId = Convert.ToInt32(_gridStatutory.CurrentRow.Cells["PaymentId"].Value);
            string reference = PromptValue("Reference Number", "Enter reference number:");
            if (reference == null)
                return;
            _payrollService.MarkStatutoryPaymentPaid(paymentId, DateTime.Today, reference);
            RefreshStatutoryTab();
        }

        private void ExportEpf()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run != null) OpenFileIfExists(_reportService.GenerateEPFECR(run.PayrollRunId));
        }

        private void ExportEsi()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run != null) OpenFileIfExists(_reportService.GenerateESIContribution(run.PayrollRunId));
        }

        private void Export24Q()
        {
            int quarter = CurrentMonth <= 6 ? 1 : CurrentMonth <= 9 ? 2 : CurrentMonth <= 12 ? 3 : 4;
            OpenFileIfExists(_reportService.GenerateForm24QData(quarter, CurrentYear));
        }

        private void ExportPt()
        {
            OpenFileIfExists(_reportService.GeneratePTRegister(CurrentMonth, CurrentYear));
        }

        private void LoadEmployeeDetails()
        {
            Employee employee = _lstDetailEmployees.SelectedItem as Employee;
            if (employee == null)
                return;

            BindSalaryHistoryGrid(_gridDetailSalaryHistory, _payrollService.GetSalaryStructures(employee.EmployeeID));
            BindPayslipHistory(_payrollService.GetPayrollEntriesByEmployee(employee.EmployeeID));
            BindTdsGrid(_payrollService.GetTdsCalculationsByEmployee(employee.EmployeeID));
            BindLoansGrid(_payrollService.GetLoansByEmployee(employee.EmployeeID), _payrollService.GetAdvancesByEmployee(employee.EmployeeID));
        }

        private void AddLoan()
        {
            Employee employee = _lstDetailEmployees.SelectedItem as Employee;
            if (employee == null)
                return;
            if (!decimal.TryParse(PromptValue("Loan Amount", "Enter loan amount:"), out decimal amount))
                return;
            if (!decimal.TryParse(PromptValue("Monthly Deduction", "Enter monthly deduction:"), out decimal monthly))
                return;
            string purpose = PromptValue("Purpose", "Enter purpose:");
            _payrollService.SaveEmployeeLoan(new EmployeeLoan { EmployeeId = employee.EmployeeID, LoanAmount = amount, MonthlyDeduction = monthly, LoanDate = DateTime.Today, RemainingBalance = amount, Purpose = purpose, IsActive = true });
            LoadEmployeeDetails();
        }

        private void AddAdvance()
        {
            Employee employee = _lstDetailEmployees.SelectedItem as Employee;
            if (employee == null)
                return;
            if (!decimal.TryParse(PromptValue("Advance Amount", "Enter advance amount:"), out decimal amount))
                return;
            _payrollService.SaveSalaryAdvance(new SalaryAdvance { EmployeeId = employee.EmployeeID, AdvanceAmount = amount, AdvanceDate = DateTime.Today, RecoveryMonth = CurrentMonth, RecoveryYear = CurrentYear, Recovered = false });
            LoadEmployeeDetails();
        }

        private void GenerateForm16()
        {
            Employee employee = _lstDetailEmployees.SelectedItem as Employee;
            if (employee == null)
                return;
            string financialYear = PromptValue("Financial Year", "Enter financial year (e.g. 2025-26):");
            if (string.IsNullOrWhiteSpace(financialYear))
                return;
            OpenFileIfExists(_reportService.GenerateForm16(employee.EmployeeID, financialYear));
        }

        private void BindSalaryHistoryGrid(DataGridView grid, List<SalaryStructure> history)
        {
            grid.Columns.Clear();
            grid.Rows.Clear();
            foreach (string col in new[] { "Effective From", "Effective To", "Basic", "DA", "HRA", "Other", "Gross" })
                grid.Columns.Add(col, col);
            foreach (SalaryStructure row in history)
                grid.Rows.Add(IndiaFormatHelper.FormatDate(row.EffectiveFrom), IndiaFormatHelper.FormatDate(row.EffectiveTo), IndiaFormatHelper.FormatCurrency(row.BasicSalary), IndiaFormatHelper.FormatCurrency(row.DA), IndiaFormatHelper.FormatCurrency(row.HRA), IndiaFormatHelper.FormatCurrency(row.OtherAllowances), IndiaFormatHelper.FormatCurrency(row.GrossSalary));
        }

        private void BindPayslipHistory(List<PayrollEntry> entries)
        {
            _gridPayslipHistory.Columns.Clear();
            _gridPayslipHistory.Rows.Clear();
            foreach (string col in new[] { "Month", "Gross", "Net", "Payslip", "Action" })
                _gridPayslipHistory.Columns.Add(col, col);
            foreach (PayrollEntry row in entries)
                _gridPayslipHistory.Rows.Add(new DateTime(row.PayrollYear, row.PayrollMonth, 1).ToString("MMM yyyy"), IndiaFormatHelper.FormatCurrency(row.GrossSalary), IndiaFormatHelper.FormatCurrency(row.NetSalary), row.PayslipPath, "View");
            _gridPayslipHistory.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    string path = Convert.ToString(_gridPayslipHistory.Rows[e.RowIndex].Cells["Payslip"].Value);
                    if (File.Exists(path))
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            };
        }

        private void BindTdsGrid(List<TDSCalculation> rows)
        {
            _gridTds.Columns.Clear();
            _gridTds.Rows.Clear();
            foreach (string col in new[] { "FY", "Regime", "Estimated Income", "Taxable Income", "Annual Tax", "Monthly TDS" })
                _gridTds.Columns.Add(col, col);
            foreach (TDSCalculation row in rows)
                _gridTds.Rows.Add(row.FinancialYear, row.TaxRegime, IndiaFormatHelper.FormatCurrency(row.EstimatedAnnualIncome), IndiaFormatHelper.FormatCurrency(row.TaxableIncome), IndiaFormatHelper.FormatCurrency(row.AnnualTaxLiability), IndiaFormatHelper.FormatCurrency(row.MonthlyTDS));
        }

        private void BindLoansGrid(List<EmployeeLoan> loans, List<SalaryAdvance> advances)
        {
            _gridLoans.Columns.Clear();
            _gridLoans.Rows.Clear();
            foreach (string col in new[] { "Type", "Date", "Amount", "Monthly/Recovery", "Balance/Status" })
                _gridLoans.Columns.Add(col, col);
            foreach (EmployeeLoan loan in loans)
                _gridLoans.Rows.Add("Loan", IndiaFormatHelper.FormatDate(loan.LoanDate), IndiaFormatHelper.FormatCurrency(loan.LoanAmount), IndiaFormatHelper.FormatCurrency(loan.MonthlyDeduction), IndiaFormatHelper.FormatCurrency(loan.RemainingBalance));
            foreach (SalaryAdvance advance in advances)
                _gridLoans.Rows.Add("Advance", IndiaFormatHelper.FormatDate(advance.AdvanceDate), IndiaFormatHelper.FormatCurrency(advance.AdvanceAmount), advance.RecoveryMonth + "/" + advance.RecoveryYear, advance.Recovered ? "Recovered" : "Pending");
        }

        private void OpenFileIfExists(ServiceResult<string> result)
        {
            SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
            if (result.Success && File.Exists(result.Data))
                Process.Start(new ProcessStartInfo(result.Data) { UseShellExecute = true });
        }

        private int CurrentMonth => (_cmbMonth.SelectedIndex + 1);
        private int CurrentYear => int.TryParse(Convert.ToString(_cmbYear.SelectedItem), out int year) ? year : DateTime.Today.Year;

        private ComboBox NewCombo(Point location, int width, string[] items)
        {
            var combo = new ComboBox { Location = location, Width = width, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9) };
            combo.Items.AddRange(items);
            combo.SelectedIndexChanged += (s, e) =>
            {
                if (!_isInitializing)
                    RefreshAll();
            };
            return combo;
        }

        private Button NewButton(string text, Point location, int width, Color backColor)
        {
            bool light = backColor == Color.White || backColor.GetBrightness() > 0.92f;
            var button = new Button
            {
                Text = text,
                Location = location,
                Width = width,
                Height = 34,
                BackColor = backColor,
                ForeColor = light ? DS.Slate700 : Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = light ? 1 : 0;
            button.FlatAppearance.BorderColor = light ? DS.BorderStrong : backColor;
            button.FlatAppearance.MouseOverBackColor = light ? DS.BgCardHov : DS.Lighten(backColor, 0.08f);
            button.FlatAppearance.MouseDownBackColor = light ? DS.Slate100 : DS.Darken(backColor, 0.10f);
            DS.Rounded(button, DS.RadiusSm);
            return button;
        }

        private Panel MakePayrollCard()
        {
            Panel panel = new Panel
            {
                BackColor = Color.White,
                Padding = new Padding(16),
                Margin = new Padding(0, 0, 0, 12)
            };
            panel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            DS.Rounded(panel, DS.RadiusLg);
            return panel;
        }

        private Panel MakePayrollKpi(string title, string value, string subtitle, Color accent, out Label valueLabel)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 10, 0);
            card.Padding = new Padding(14, 10, 14, 10);

            Panel icon = new Panel { Location = new Point(14, 16), Size = new Size(40, 40), BackColor = DS.Lighten(accent, 0.82f) };
            DS.Rounded(icon, 10);
            icon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Font font = new Font("Segoe UI", 14f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(accent))
                    e.Graphics.DrawString("•", font, brush, new PointF(13, 8));
            };

            Label titleLabel = new Label { Text = title, Location = new Point(68, 12), Size = new Size(160, 18), Font = DS.Small, ForeColor = DS.Slate600, AutoEllipsis = true };
            Label metricValue = new Label { Text = value, Location = new Point(68, 32), Size = new Size(180, 28), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            valueLabel = metricValue;
            Label subLabel = new Label { Text = subtitle, Location = new Point(68, 61), Size = new Size(180, 18), Font = DS.Small, ForeColor = DS.Slate500, AutoEllipsis = true };
            card.Resize += (s, e) =>
            {
                int textWidth = Math.Max(80, card.ClientSize.Width - 82);
                titleLabel.Width = textWidth;
                metricValue.Width = textWidth;
                subLabel.Width = textWidth;
            };
            card.Controls.AddRange(new Control[] { icon, titleLabel, metricValue, subLabel });
            return card;
        }

        private DataGridView NewGrid()
        {
            DataGridView grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            GridTheme.Apply(grid);
            return grid;
        }

        private Label AddSummary(Control parent, string title, Point location)
        {
            var label = new Label { Location = location, Size = new Size(240, 46), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42) };
            parent.Controls.Add(label);
            return label;
        }

        private DateTimePicker AddDateField(TableLayoutPanel parent, string label)
        {
            parent.RowCount += 1;
            parent.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            int row = parent.RowCount - 1;
            parent.Controls.Add(BuildEditorLabel(label), 0, row);
            var picker = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9),
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd/MM/yyyy",
                Margin = new Padding(0, 4, 12, 4)
            };
            parent.Controls.Add(picker, 1, row);
            parent.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }, 2, row);
            parent.Controls.Add(new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) }, 3, row);
            return picker;
        }

        private NumericUpDown AddAmountField(TableLayoutPanel parent, string label)
        {
            bool useRightColumn = parent.RowCount > 0 && parent.GetControlFromPosition(2, parent.RowCount - 1) == null;
            int row;
            int labelColumn;
            int editorColumn;
            if (useRightColumn)
            {
                row = parent.RowCount - 1;
                labelColumn = 2;
                editorColumn = 3;
            }
            else
            {
                parent.RowCount += 1;
                parent.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                row = parent.RowCount - 1;
                labelColumn = 0;
                editorColumn = 1;
            }

            parent.Controls.Add(BuildEditorLabel(label), labelColumn, row);
            var amount = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 2,
                Maximum = 1000000,
                ThousandsSeparator = true,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 4, 12, 4)
            };
            parent.Controls.Add(amount, editorColumn, row);
            return amount;
        }

        private static Control BuildEditorLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(0, 4, 10, 4)
            };
        }

        private TabPage NewGridTab(string title, DataGridView grid)
        {
            var tab = new TabPage(title) { BackColor = Color.White };
            grid.Dock = DockStyle.Fill;
            tab.Controls.Add(grid);
            return tab;
        }

        private void BindEmployeeList(ListBox listBox, List<Employee> rows)
        {
            if (listBox == null)
                return;
            object selected = listBox.SelectedItem;
            listBox.DataSource = null;
            listBox.DisplayMember = "Name";
            listBox.ValueMember = "EmployeeID";
            listBox.DataSource = rows;
            if (selected is Employee employee)
            {
                Employee match = rows.FirstOrDefault(e => e.EmployeeID == employee.EmployeeID);
                if (match != null)
                    listBox.SelectedItem = match;
            }
        }

        private void SetStatus(string message, Color color)
        {
            if (_lblStatus == null)
                return;
            _lblStatus.Text = message;
            _lblStatus.ForeColor = color;
        }

        private void ToggleBusyState(bool isBusy)
        {
            UseWaitCursor = isBusy;
            if (_tabs != null)
                _tabs.Enabled = !isBusy;
            if (_cmbMonth != null)
                _cmbMonth.Enabled = !isBusy;
            if (_cmbYear != null)
                _cmbYear.Enabled = !isBusy;
            if (_btnImport != null)
                _btnImport.Enabled = !isBusy;
            if (_btnGeneratePayslips != null)
                _btnGeneratePayslips.Enabled = !isBusy;
        }

        private static Form BuildBusyDialog(string title, string message)
        {
            var form = new Form
            {
                Width = 420,
                Height = 150,
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                ShowInTaskbar = false,
                BackColor = Color.White
            };
            var label = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(16, 16, 16, 0),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(51, 65, 85),
                Text = message
            };
            var progress = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 14,
                Margin = new Padding(16),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28
            };
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 16) };
            panel.Controls.Add(progress);
            form.Controls.Add(panel);
            form.Controls.Add(label);
            return form;
        }

        private string PromptValue(string title, string prompt)
        {
            using (var form = new Form { Text = title, Width = 360, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var label = new Label { Text = prompt, Left = 12, Top = 14, Width = 320 };
                var text = new TextBox { Left = 12, Top = 40, Width = 320 };
                var ok = new Button { Text = "OK", Left = 176, Top = 72, Width = 72, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 260, Top = 72, Width = 72, DialogResult = DialogResult.Cancel };
                form.Controls.AddRange(new Control[] { label, text, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog(this) == DialogResult.OK ? text.Text.Trim() : null;
            }
        }
    }
}

