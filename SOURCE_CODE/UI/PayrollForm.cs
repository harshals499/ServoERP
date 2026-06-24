using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
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
        private Label _lblMonthBadge;
        private Button _btnImport;
        private TabControl _tabs;
        private Button _btnGenerateSelectedPayslip;
        private Label _lblStepImportAttendance;
        private Label _lblStepReviewExceptions;
        private Label _lblStepRunPayroll;
        private Label _lblStepVerifyStatutory;
        private Label _lblStepLockPayroll;

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
        private Label _lblDashPayrollStatus;
        private Label _lblDashPayrollStatusDetail;
        private Label _lblDashAttendanceExceptions;
        private Label _lblDashAttendanceExceptionsDetail;
        private Label _lblDashSalaryGaps;
        private Label _lblDashSalaryGapsDetail;
        private Label _lblDashPendingStatutory;
        private Label _lblDashPendingStatutoryDetail;
        private Label _lblDashPayslipsPending;
        private Label _lblDashPayslipsPendingDetail;
        private Label _lblCloseHeadline;
        private Label _lblCloseAction;
        private Label _lblDashCoverage;
        private Label _lblDashCoverageDetail;
        private Label _lblDashRecoveryPressure;
        private Label _lblDashRecoveryPressureDetail;
        private Label _lblDashComplianceTiming;
        private Label _lblDashComplianceTimingDetail;
        private Label _lblDashOvertimeRisk;
        private Label _lblDashOvertimeRiskDetail;
        private PayrollInsightCardBindings _attendanceInsightCard;
        private PayrollInsightCardBindings _salaryInsightCard;
        private PayrollInsightCardBindings _lateAbsentInsightCard;
        private PayrollInsightCardBindings _payslipInsightCard;
        private PayrollQualityPulseBar _qualityPulseBar;
        private Button _btnGeneratePayslips;

        private TextBox _txtSalarySearch;
        private TextBox _txtDetailSearch;
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

        private DataGridView _gridStatutory;

        private ListBox _lstDetailEmployees;
        private DataGridView _gridPayslipHistory;
        private DataGridView _gridTds;
        private DataGridView _gridLoans;
        private DataGridView _gridDetailSalaryHistory;
        private DataGridView _gridSalaryComponents;
        private DataGridView _gridForm16;
        private Label _lblSalaryEmployeeName;
        private Label _lblSalaryAvatar;
        private Label _lblSalaryEmployeeRole;
        private Label _lblSalaryGross;
        private Label _lblSalaryEffectiveFrom;
        private Label _lblSalaryStatus;
        private Label _lblDetailEmployeeName;
        private Label _lblDetailAvatar;
        private Label _lblDetailEmployeeRole;
        private Label _lblDetailGross;
        private Label _lblDetailEffectiveFrom;
        private Label _lblDetailStatus;
        private Label _lblDetailGrossTotal;
        private Label _lblTdsDeducted;
        private Label _lblTdsPaid;
        private Label _lblTdsPending;

        private List<Employee> _employees = new List<Employee>();
        private bool _isInitializing;

        protected override bool EnableAutomaticLayoutScaling => false;
        protected override bool EnableMainScrollCanvas => false;
        protected override bool SuppressAutomaticChildPolish => true;

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

            Button btnMore = NewButton("More", Point.Empty, 92, Color.White);
            btnMore.ForeColor = DS.Slate700;
            btnMore.FlatAppearance.BorderSize = 1;
            btnMore.FlatAppearance.BorderColor = DS.BorderStrong;
            _btnImport = NewButton("Import Payroll Excel", Point.Empty, 172, Color.White);
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
            Button[] headerButtons = { btnMore, _btnImport, btnForms, btnLock, btnRun };
            foreach (Button button in headerButtons)
            {
                button.Margin = Padding.Empty;
                button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                button.Tag = ((button.Tag == null ? string.Empty : button.Tag + " ") + "FIXED_WIDTH").Trim();
            }
            btnRun.Click += (s, e) => RunPayroll();
            btnLock.Click += (s, e) => LockCurrentPayroll();
            _btnImport.Click += (s, e) => ImportPayrollFiles();
            btnForms.Click += (s, e) => FormTemplateWorkflowLauncher.Open(this, "Payroll", "Payroll", null, "technician attendance leave request salary approval payroll job costing sheet payment receipt");
            btnMore.Click += (s, e) => ShowPayrollActionsMenu(btnMore, btnRun, btnLock, btnForms);
            Panel header = SharedPageHeader.Build(new SharedPageHeaderModel
            {
                Name = "PayrollPageHeader",
                Mode = SharedPageHeaderMode.Editor,
                Dock = DockStyle.Top,
                BackColor = DS.BgPage,
                Title = "Payroll Dashboard",
                Subtitle = "Workforce pay control, compliance review, and month-close execution.",
                TitleWidth = 360,
                SubtitleWidth = 440,
                RightActions = headerButtons.Cast<Control>().ToList()
            }).Header;

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
            _lblMonthBadge = new Label
            {
                Text = "Open",
                Location = new Point(286, 39),
                Size = new Size(74, 30),
                Font = DS.BodyBold,
                ForeColor = DS.Green600,
                BackColor = DS.Green50,
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(_lblMonthBadge, 8);
            _lblStatus = new Label
            {
                Text = "Run payroll to calculate salaries for the selected month.",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(420, 35),
                Size = new Size(Math.Max(320, periodStrip.Width - 450), 36),
                Font = DS.Body,
                ForeColor = DS.Primary700,
                BackColor = DS.Primary50,
                Padding = new Padding(14, 8, 12, 0),
                AutoEllipsis = true
            };
            DS.Rounded(_lblStatus, 6);
            periodStrip.Resize += (s, e) =>
            {
                _lblStatus.Width = Math.Max(260, periodStrip.ClientSize.Width - _lblStatus.Left - 18);
            };
            periodStrip.Controls.AddRange(new Control[] { monthLabel, _cmbMonth, _cmbYear, _lblMonthBadge, _lblStatus });

            _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9) };
            _tabs.TabPages.Add(BuildDashboardTab());
            _tabs.TabPages.Add(BuildProcessTab());
            _tabs.TabPages.Add(BuildSalaryTab());
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
            AddPayrollAction(menu, "Import Payroll / Attendance Excel", (s, e) => ImportPayrollFiles());
            AddPayrollAction(menu, "Import Historical Folder Data", (s, e) => ImportHistoricalData());
            AddPayrollAction(menu, "Open Payroll Forms", (s, e) => formsButton.PerformClick());
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        public void ShowDashboardFromNavigation()
        {
            SelectPayrollTab(0);
        }

        private void AddPayrollAction(ContextMenuStrip menu, string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            menu.Items.Add(item);
        }

        private TabPage BuildDashboardTab()
        {
            var tab = new TabPage("Dashboard") { BackColor = Color.White, Padding = new Padding(12) };
            Panel canvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };

            Panel cockpit = BuildPayrollCloseCockpit();

            TableLayoutPanel kpis = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 0, 0, 8) };
            for (int i = 0; i < 5; i++)
                kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            kpis.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            WorkforceMetricCardResult employeeCard = WorkforceModuleVisuals.CreateMetricCard("TOTAL EMPLOYEES", "0", "Active employees", DS.Primary600, true);
            _lblKpiEmployees = employeeCard.ValueLabel;
            WorkforceMetricCardResult grossCard = WorkforceModuleVisuals.CreateMetricCard("TOTAL GROSS", "₹0.00", "Gross payroll", DS.Green600);
            _lblKpiGross = grossCard.ValueLabel;
            WorkforceMetricCardResult deductionCard = WorkforceModuleVisuals.CreateMetricCard("TOTAL DEDUCTIONS", "₹0.00", "Statutory + other", DS.Amber500);
            _lblKpiDeductions = deductionCard.ValueLabel;
            WorkforceMetricCardResult netCard = WorkforceModuleVisuals.CreateMetricCard("TOTAL NET PAY", "₹0.00", "Payable to staff", DS.Primary700);
            _lblKpiNet = netCard.ValueLabel;
            WorkforceMetricCardResult liabilityCard = WorkforceModuleVisuals.CreateMetricCard("EMPLOYER LIABILITY", "₹0.00", "Company contribution", Color.FromArgb(124, 58, 237));
            _lblKpiLiability = liabilityCard.ValueLabel;
            MakeCardClickable(employeeCard.Card, () => SelectPayrollTab(4));
            MakeCardClickable(grossCard.Card, ExportPayrollRegister);
            MakeCardClickable(deductionCard.Card, () => SelectPayrollTab(3));
            MakeCardClickable(netCard.Card, ExportPayrollRegister);
            MakeCardClickable(liabilityCard.Card, () => SelectPayrollTab(3));
            kpis.Controls.Add(employeeCard.Card, 0, 0);
            kpis.Controls.Add(grossCard.Card, 1, 0);
            kpis.Controls.Add(deductionCard.Card, 2, 0);
            kpis.Controls.Add(netCard.Card, 3, 0);
            kpis.Controls.Add(liabilityCard.Card, 4, 0);

            Panel cockpitIntro = BuildDashboardSectionIntro(
                "Close Command Center",
                "Read the month state first, then work down the blockers in the order that leads cleanly into payroll execution and final lock.");

            TableLayoutPanel cockpitBoard = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 0, 0, 8) };
            cockpitBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            cockpitBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
            cockpitBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
            cockpitBoard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cockpitBoard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Panel closeCockpit = BuildCloseCockpitCard(out _lblDashPayrollStatus, out _lblCloseHeadline, out _lblDashPayrollStatusDetail, out _lblCloseAction);
            cockpitBoard.Controls.Add(closeCockpit, 0, 0);
            cockpitBoard.SetRowSpan(closeCockpit, 2);
            MakeCardClickable(closeCockpit, () => SelectPayrollTab(1));
            cockpitBoard.Controls.Add(MakeAccountantCard("Attendance Review", "Checking", "People missing or exception-heavy before run", DS.Amber600, out _lblDashAttendanceExceptions, out _lblDashAttendanceExceptionsDetail, () => ShowDashboardCardReport("Attendance Review", _lblDashAttendanceExceptions, _lblDashAttendanceExceptionsDetail, DS.Amber600)), 1, 0);
            cockpitBoard.Controls.Add(MakeAccountantCard("Salary Setup", "Checking", "Employees missing structure", DS.Red600, out _lblDashSalaryGaps, out _lblDashSalaryGapsDetail, () => SelectPayrollTab(2)), 2, 0);
            cockpitBoard.Controls.Add(MakeAccountantCard("Statutory Queue", "Checking", "EPF, ESI, PT, and TDS pending review", Color.FromArgb(124, 58, 237), out _lblDashPendingStatutory, out _lblDashPendingStatutoryDetail, () => SelectPayrollTab(3)), 1, 1);
            cockpitBoard.Controls.Add(MakeAccountantCard("Payslip Readiness", "Checking", "Employees still waiting for payslips", DS.Primary600, out _lblDashPayslipsPending, out _lblDashPayslipsPendingDetail, () => SelectPayrollTab(4)), 2, 1);

            Panel whoNeedsActionIntro = BuildDashboardSectionIntro(
                "Exception Queues",
                "These are the real people and issues standing between review, clean calculation, statutory follow-through, and employee communication.");

            TableLayoutPanel whoNeedsActionCards = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 0, 0, 8) };
            for (int i = 0; i < 4; i++)
                whoNeedsActionCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            whoNeedsActionCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            whoNeedsActionCards.Controls.Add(BuildCompactInsightQueueCard("Attendance Blockers", "Missing rows, leave, absences, or half-days", DS.Amber600, out _attendanceInsightCard), 0, 0);
            whoNeedsActionCards.Controls.Add(BuildCompactInsightQueueCard("Salary Setup Blockers", "Employees needing valid salary structure", DS.Red600, out _salaryInsightCard), 1, 0);
            whoNeedsActionCards.Controls.Add(BuildCompactInsightQueueCard("Late / Absent Watch", "Punctuality or unexplained absence risk", DS.Primary600, out _lateAbsentInsightCard), 2, 0);
            whoNeedsActionCards.Controls.Add(BuildCompactInsightQueueCard("Payslip Follow-up", "Payslips pending after processing", DS.Green600, out _payslipInsightCard), 3, 0);

            Panel signalIntro = BuildDashboardSectionIntro(
                "Control Signals",
                "These supporting signals help the payroll operator judge data quality, due-date pressure, and attendance risk without leaving the close cockpit.");

            TableLayoutPanel signalCards = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.White, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 0, 0, 4) };
            for (int i = 0; i < 4; i++)
                signalCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            signalCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            signalCards.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            signalCards.Controls.Add(MakeAccountantCard("Attendance Coverage", "Checking", "How complete the month attendance dataset looks", DS.Primary600, out _lblDashCoverage, out _lblDashCoverageDetail, () => ShowDashboardCardReport("Attendance Coverage", _lblDashCoverage, _lblDashCoverageDetail, DS.Primary600)), 0, 0);
            signalCards.Controls.Add(MakeAccountantCard("Recoveries", "Checking", "Loans and advances flowing into this payroll", Color.FromArgb(14, 116, 144), out _lblDashRecoveryPressure, out _lblDashRecoveryPressureDetail, () => SelectPayrollTab(4)), 1, 0);
            signalCards.Controls.Add(MakeAccountantCard("Next Due", "Checking", "Statutory timing pressure for this period", Color.FromArgb(124, 58, 237), out _lblDashComplianceTiming, out _lblDashComplianceTimingDetail, () => SelectPayrollTab(3)), 2, 0);
            signalCards.Controls.Add(MakeAccountantCard("Overtime Watch", "Checking", "Overtime and incomplete log pressure", DS.Amber600, out _lblDashOvertimeRisk, out _lblDashOvertimeRiskDetail, () => ShowDashboardCardReport("Overtime Watch", _lblDashOvertimeRisk, _lblDashOvertimeRiskDetail, DS.Amber600)), 3, 0);
            signalCards.Controls.Add(BuildQualityPulseCard(), 4, 0);

            canvas.Controls.Add(signalCards);
            canvas.Controls.Add(signalIntro);
            canvas.Controls.Add(whoNeedsActionCards);
            canvas.Controls.Add(whoNeedsActionIntro);
            canvas.Controls.Add(cockpitBoard);
            canvas.Controls.Add(cockpitIntro);
            canvas.Controls.Add(kpis);
            canvas.Controls.Add(cockpit);
            tab.Controls.Add(canvas);
            return tab;
        }

        private TabPage BuildProcessTab()
        {
            var tab = new TabPage("Run Payroll") { BackColor = DS.BgPage, Padding = new Padding(14) };

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
            _btnGeneratePayslips = NewButton("Generate All Payslips", new Point(0, 8), 170, DS.Primary600);
            _btnGeneratePayslips.AutoSize = false;
            _btnGenerateSelectedPayslip = NewButton("Generate Selected", new Point(182, 8), 160, DS.Green600);
            _btnGenerateSelectedPayslip.AutoSize = false;
            Button btnExport = NewButton("Export Payroll Register", new Point(354, 8), 190, DS.Green600);
            Button btnRecalc = NewButton("Recalculate Selected", new Point(556, 8), 178, DS.Primary600);
            _btnGeneratePayslips.Click += (s, e) => GenerateAllPayslips();
            _btnGenerateSelectedPayslip.Click += (s, e) => GenerateSelectedPayslip();
            btnExport.Click += (s, e) => ExportPayrollRegister();
            btnRecalc.Click += (s, e) => RecalculateSelected();
            topButtons.Controls.AddRange(new Control[] { _btnGeneratePayslips, _btnGenerateSelectedPayslip, btnExport, btnRecalc });

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

            Panel processGuide = MakePayrollCard();
            processGuide.Dock = DockStyle.Top;
            processGuide.Height = 66;
            processGuide.Padding = new Padding(16, 14, 16, 12);
            Label processGuideTitle = new Label { Text = "Run Workspace", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label processGuideBody = new Label
            {
                Text = "This sub-page is for payroll execution after the dashboard review is complete. Recalculate employee rows, export the register, and generate payslips here.",
                Dock = DockStyle.Fill,
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            processGuide.Controls.Add(processGuideBody);
            processGuide.Controls.Add(processGuideTitle);

            tab.Controls.Add(gridWrap);
            tab.Controls.Add(summary);
            tab.Controls.Add(topButtons);
            tab.Controls.Add(processGuide);
            return tab;
        }

        private TabPage BuildSalaryTab()
        {
            var tab = new TabPage("Salary Structures") { BackColor = Color.White, Padding = new Padding(12) };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, ColumnCount = 2, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _txtSalarySearch = null;
            _lstSalaryEmployees = new ListBox();
            _lstSalaryEmployees.SelectedIndexChanged += (s, e) => LoadSalaryDetails();
            Panel leftPanel = BuildEmployeePickerPanel(out _txtSalarySearch, _lstSalaryEmployees);
            _txtSalarySearch.TextChanged += (s, e) => BindEmployeeLists();

            Panel editor = MakePayrollCard();
            editor.Dock = DockStyle.Fill;
            editor.Padding = new Padding(16);
            editor.Margin = new Padding(8, 0, 0, 0);
            editor.AutoScroll = true;

            Label titleLabel = new Label { Text = "Salary Structure Editor", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label subtitleLabel = new Label { Text = "Update effective date and monthly earning components for the selected employee.", Dock = DockStyle.Top, Height = 24, Font = DS.Small, ForeColor = DS.Slate600 };
            Panel summary = BuildEmployeeSummaryCard(true);

            Panel formCard = MakePayrollCard();
            formCard.Dock = DockStyle.Top;
            formCard.Height = 278;
            formCard.Padding = new Padding(16, 16, 16, 12);
            TableLayoutPanel fieldGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 178, ColumnCount = 4, RowCount = 5, BackColor = Color.White };
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
            fieldGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < 5; i++)
                fieldGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            _dtStructureFrom = AddModernDateField(fieldGrid, "Effective From *", 0, 0);
            _numBasic = AddModernAmountField(fieldGrid, "Basic Salary *", 0, 1);
            _numHra = AddModernAmountField(fieldGrid, "HRA", 0, 2);
            _numConveyance = AddModernAmountField(fieldGrid, "Conveyance Allowance", 0, 3);
            _numLta = AddModernAmountField(fieldGrid, "LTA", 0, 4);
            _numDa = AddModernAmountField(fieldGrid, "DA", 2, 0);
            _numSpecial = AddModernAmountField(fieldGrid, "Special Allowance", 2, 1);
            _numMedical = AddModernAmountField(fieldGrid, "Medical Allowance", 2, 2);
            _numOther = AddModernAmountField(fieldGrid, "Other Allowances", 2, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 10, 0, 0), WrapContents = false };
            Button btnAddStructure = NewButton("Add New Structure", Point.Empty, 160, DS.Primary600);
            Button btnSaveStructure = NewButton("Save Structure", Point.Empty, 138, Color.White);
            btnSaveStructure.ForeColor = DS.Primary600;
            btnSaveStructure.FlatAppearance.BorderColor = DS.Primary600;
            ModernIconSystem.AddButtonIcon(btnAddStructure, ModernIconKind.User);
            ModernIconSystem.AddButtonIcon(btnSaveStructure, ModernIconKind.Save);
            btnAddStructure.Click += (s, e) => ClearSalaryForm();
            btnSaveStructure.Click += (s, e) => SaveSalaryStructure();
            actions.Controls.Add(btnAddStructure);
            actions.Controls.Add(btnSaveStructure);
            _lblSalaryValidation = new Label { Dock = DockStyle.Top, Height = 28, ForeColor = Color.Firebrick, Font = DS.SmallBold, Padding = new Padding(0, 6, 0, 0) };
            formCard.Controls.Add(_lblSalaryValidation);
            formCard.Controls.Add(actions);
            formCard.Controls.Add(fieldGrid);

            _gridSalaryHistory = NewGrid();
            ConfigureModernGrid(_gridSalaryHistory);
            Panel historyCard = BuildTableCard("Salary Structure History", _gridSalaryHistory, true);

            editor.Controls.Add(historyCard);
            editor.Controls.Add(formCard);
            editor.Controls.Add(summary);
            editor.Controls.Add(subtitleLabel);
            editor.Controls.Add(titleLabel);
            layout.Controls.Add(leftPanel, 0, 0);
            layout.Controls.Add(editor, 1, 0);
            tab.Controls.Add(layout);
            return tab;
        }

        private Panel BuildSalaryStructureGuide()
        {
            Panel guide = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(14, 8, 14, 8) };
            guide.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, guide.Width - 1, guide.Height - 1);
            };
            Label title = new Label
            {
                Text = "Required fields: Employee, Effective From, Basic Salary.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label hint = new Label
            {
                Text = "Allowances can stay zero. Save creates a new effective salary structure for future payroll runs.",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate600
            };
            guide.Controls.Add(hint);
            guide.Controls.Add(title);
            return guide;
        }

        private Panel BuildEmployeePickerPanel(out TextBox searchBox, ListBox listBox)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.Padding = new Padding(14, 14, 14, 12);
            card.Margin = new Padding(0);

            Label title = new Label { Text = "Employees", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label subtitle = new Label { Text = "Search by employee name or code", Dock = DockStyle.Top, Height = 20, Font = DS.Small, ForeColor = DS.Slate600 };

            Panel searchRow = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White, Padding = new Padding(0, 10, 0, 0) };
            Panel searchWrap = new Panel { Location = new Point(0, 4), Size = new Size(292, 34), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            searchWrap.Paint += (s, e) => PaintBorder(searchWrap, e.Graphics, DS.BorderStrong, 7);
            Label searchIcon = LucideIcon("search.svg", ModernIconKind.Search, 15, DS.Slate500);
            searchIcon.Location = new Point(10, 9);
            searchIcon.Size = new Size(16, 16);
            TextBox localSearchBox = new TextBox { BorderStyle = BorderStyle.None, Location = new Point(34, 8), Size = new Size(238, 20), Font = new Font("Segoe UI", 9f), ForeColor = DS.Slate700, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            searchWrap.Controls.Add(localSearchBox);
            searchWrap.Controls.Add(searchIcon);
            searchRow.Resize += (s, e) =>
            {
                searchWrap.Width = Math.Max(120, searchRow.ClientSize.Width - 8);
                localSearchBox.Width = Math.Max(60, searchWrap.ClientSize.Width - localSearchBox.Left - 16);
            };
            searchRow.Controls.Add(searchWrap);

            ConfigureEmployeeList(listBox);
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.White, Padding = new Padding(0, 10, 0, 0) };
            footer.Paint += (s, e) => PaintBorder(footer, e.Graphics, DS.Border, 7);
            Label total = new Label { Text = "Total Employees", Location = new Point(12, 17), Size = new Size(140, 18), Font = DS.SmallBold, ForeColor = DS.Slate700 };
            Label totalValue = new Label { Name = "TotalEmployeesValue", Text = "0", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(290, 17), Size = new Size(44, 18), Font = DS.SmallBold, ForeColor = DS.Primary600, TextAlign = ContentAlignment.MiddleRight };
            footer.Resize += (s, e) => totalValue.Left = Math.Max(total.Right + 8, footer.ClientSize.Width - totalValue.Width - 12);
            footer.Controls.Add(totalValue);
            footer.Controls.Add(total);

            card.Controls.Add(listBox);
            card.Controls.Add(footer);
            card.Controls.Add(searchRow);
            card.Controls.Add(subtitle);
            card.Controls.Add(title);
            searchBox = localSearchBox;
            return card;
        }

        private Panel BuildEmployeeSummaryCard(bool salaryPage)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Top;
            card.Height = 78;
            card.Padding = new Padding(14, 12, 14, 10);

            Label avatar = new Label { Name = salaryPage ? "SalaryAvatar" : "DetailAvatar", Location = new Point(14, 17), Size = new Size(38, 38), Text = "--", Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.White, BackColor = DS.Primary600, TextAlign = ContentAlignment.MiddleCenter };
            DS.Rounded(avatar, 19);
            Label name = new Label { Location = new Point(64, 15), Size = new Size(360, 20), Font = new Font("Segoe UI", 9.75f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            Label role = new Label { Location = new Point(64, 39), Size = new Size(300, 18), Font = DS.Small, ForeColor = DS.Slate700, AutoEllipsis = true };
            Label gross = BuildSummaryMetric("Current Gross", "₹0.00", ModernIconKind.Money);
            Label effective = BuildSummaryMetric("Effective From", "-", ModernIconKind.Calendar);
            Label status = BuildSummaryMetric("Status", "• Active", ModernIconKind.Status);
            card.Controls.AddRange(new Control[] { avatar, name, role, gross, effective, status });
            card.Resize += (s, e) =>
            {
                int metricWidth = Math.Max(128, (card.ClientSize.Width - 470) / 3);
                status.Size = new Size(metricWidth, 48);
                effective.Size = new Size(metricWidth, 48);
                gross.Size = new Size(metricWidth, 48);
                status.Location = new Point(card.ClientSize.Width - status.Width - 14, 15);
                effective.Location = new Point(status.Left - effective.Width - 10, 15);
                gross.Location = new Point(effective.Left - gross.Width - 10, 15);
                name.Width = Math.Max(180, gross.Left - name.Left - 12);
                role.Width = name.Width;
            };

            if (salaryPage)
            {
                _lblSalaryAvatar = avatar;
                _lblSalaryEmployeeName = name;
                _lblSalaryEmployeeRole = role;
                _lblSalaryGross = gross;
                _lblSalaryEffectiveFrom = effective;
                _lblSalaryStatus = status;
            }
            else
            {
                _lblDetailAvatar = avatar;
                _lblDetailEmployeeName = name;
                _lblDetailEmployeeRole = role;
                _lblDetailGross = gross;
                _lblDetailEffectiveFrom = effective;
                _lblDetailStatus = status;
            }

            return card;
        }

        private Label BuildSummaryMetric(string title, string value, ModernIconKind icon)
        {
            Label label = new Label
            {
                Text = title + Environment.NewLine + value,
                Font = DS.Small,
                ForeColor = DS.Slate700,
                BackColor = DS.Slate50,
                Padding = new Padding(12, 6, 8, 0)
            };
            DS.Rounded(label, 7);
            return label;
        }

        private Panel BuildTableCard(string title, DataGridView grid, bool withPager)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Top;
            card.Height = withPager ? 216 : 252;
            card.Padding = new Padding(14, 12, 14, 12);
            Label heading = new Label { Text = title, Dock = DockStyle.Top, Height = string.IsNullOrWhiteSpace(title) ? 0 : 26, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = DS.Slate900 };
            grid.Dock = DockStyle.Fill;
            card.Controls.Add(grid);
            if (withPager)
                card.Controls.Add(BuildPagerFooter());
            if (!string.IsNullOrWhiteSpace(title))
                card.Controls.Add(heading);
            return card;
        }

        private Panel BuildPagerFooter()
        {
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.White };
            Label showing = new Label { Text = "Showing 1 to 1 of 1 entries", Location = new Point(0, 15), Size = new Size(180, 18), Font = DS.Small, ForeColor = DS.Slate600, AutoEllipsis = true };
            Button prev = NewPagerButton("<");
            Button page = NewPagerButton("1");
            Button next = NewPagerButton(">");
            ComboBox perPage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = DS.Small, Width = 86, Height = 28 };
            perPage.Items.AddRange(new object[] { "10 / page", "25 / page", "50 / page" });
            perPage.SelectedIndex = 0;
            footer.Resize += (s, e) =>
            {
                const int gap = 6;
                int pagerWidth = prev.Width + page.Width + next.Width + perPage.Width + gap * 3;
                int start = Math.Max(0, footer.ClientSize.Width - pagerWidth);
                prev.Location = new Point(start, 8);
                page.Location = new Point(prev.Right + gap, 8);
                next.Location = new Point(page.Right + gap, 8);
                perPage.Location = new Point(next.Right + gap, 8);
                showing.Width = Math.Max(0, start - 10);
                showing.Visible = showing.Width >= 90;
            };
            footer.Controls.AddRange(new Control[] { showing, prev, page, next, perPage });
            return footer;
        }

        private Button NewPagerButton(string text)
        {
            Button button = new Button { Text = text, Size = new Size(34, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = DS.Slate600, Font = DS.SmallBold, TextAlign = ContentAlignment.MiddleCenter, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = DS.BorderStrong;
            DS.Rounded(button, 6);
            return button;
        }

        private TabPage BuildModernGridTab(string title, DataGridView grid, bool withPager)
        {
            TabPage tab = new TabPage(title) { BackColor = Color.White, Padding = new Padding(12) };
            Panel card = BuildTableCard(string.Empty, grid, withPager);
            card.Dock = DockStyle.Fill;
            tab.Controls.Add(card);
            return tab;
        }

        private TabPage BuildForm16Tab()
        {
            TabPage tab = new TabPage("Form 16") { BackColor = Color.White, Padding = new Padding(12) };
            _gridForm16 = NewGrid();
            ConfigureModernGrid(_gridForm16);
            Panel card = BuildTableCard(string.Empty, _gridForm16, true);
            card.Dock = DockStyle.Fill;
            Button generate = NewButton("Generate Form 16", Point.Empty, 148, DS.Primary600);
            ModernIconSystem.AddButtonIcon(generate, ModernIconKind.Document);
            generate.Location = new Point(12, 12);
            generate.Click += (s, e) => GenerateForm16();
            _gridForm16.CellClick += (s, e) => HandleForm16GridAction(e.RowIndex, e.ColumnIndex);
            tab.Controls.Add(generate);
            tab.Controls.Add(card);
            generate.BringToFront();
            return tab;
        }

        private TabPage BuildTdsSummaryTab()
        {
            TabPage tab = new TabPage("TDS Summary") { BackColor = Color.White, Padding = new Padding(12) };
            TableLayoutPanel cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 58, ColumnCount = 3, BackColor = Color.White };
            for (int i = 0; i < 3; i++)
                cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cards.Controls.Add(BuildMiniSummary("Total TDS Deducted", out _lblTdsDeducted), 0, 0);
            cards.Controls.Add(BuildMiniSummary("TDS Paid", out _lblTdsPaid), 1, 0);
            cards.Controls.Add(BuildMiniSummary("Pending", out _lblTdsPending), 2, 0);
            Panel table = BuildTableCard(string.Empty, _gridTds, true);
            table.Dock = DockStyle.Fill;
            tab.Controls.Add(table);
            tab.Controls.Add(cards);
            return tab;
        }

        private Panel BuildMiniSummary(string title, out Label value)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 8, 8);
            card.Padding = new Padding(12, 8, 12, 8);
            Label label = new Label { Text = title, Dock = DockStyle.Top, Height = 17, Font = DS.Small, ForeColor = DS.Slate600 };
            value = new Label { Text = "₹0.00", Dock = DockStyle.Top, Height = 24, Font = DS.BodyBold, ForeColor = DS.Slate900 };
            card.Controls.Add(value);
            card.Controls.Add(label);
            return card;
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
            var tab = new TabPage("Employee Details") { BackColor = Color.White, Padding = new Padding(12) };
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, ColumnCount = 2, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _lstDetailEmployees = new ListBox();
            _lstDetailEmployees.SelectedIndexChanged += (s, e) => LoadEmployeeDetails();
            Panel leftPanel = BuildEmployeePickerPanel(out _txtDetailSearch, _lstDetailEmployees);
            _txtDetailSearch.TextChanged += (s, e) => BindEmployeeLists();

            Panel workspace = MakePayrollCard();
            workspace.Dock = DockStyle.Fill;
            workspace.Margin = new Padding(8, 0, 0, 0);
            workspace.Padding = new Padding(16);
            workspace.AutoScroll = true;
            _gridDetailSalaryHistory = NewGrid();
            _gridPayslipHistory = NewGrid();
            _gridPayslipHistory.CellClick += (s, e) => HandlePayslipHistoryAction(e.RowIndex, e.ColumnIndex);
            _gridPayslipHistory.CellDoubleClick += (s, e) => HandlePayslipHistoryAction(e.RowIndex);
            _gridTds = NewGrid();
            _gridTds.CellClick += (s, e) => HandleTdsGridAction(e.RowIndex, e.ColumnIndex);
            _gridLoans = NewGrid();
            _gridLoans.CellClick += (s, e) => HandleLoansGridAction(e.RowIndex, e.ColumnIndex);
            ConfigureModernGrid(_gridLoans);
            ConfigureModernGrid(_gridDetailSalaryHistory);
            ConfigureModernGrid(_gridPayslipHistory);
            ConfigureModernGrid(_gridTds);

            Panel summary = BuildEmployeeSummaryCard(false);
            FlowLayoutPanel recoveryActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 4), WrapContents = false };
            Button btnAddLoan = NewButton("Create Loan", Point.Empty, 122, DS.Primary600);
            Button btnAddAdvance = NewButton("Create Advance", Point.Empty, 142, Color.White);
            btnAddAdvance.ForeColor = DS.Primary600;
            btnAddAdvance.FlatAppearance.BorderColor = DS.Primary600;
            ModernIconSystem.AddButtonIcon(btnAddLoan, ModernIconKind.Money);
            ModernIconSystem.AddButtonIcon(btnAddAdvance, ModernIconKind.Money);
            btnAddLoan.Click += (s, e) => AddLoan();
            btnAddAdvance.Click += (s, e) => AddAdvance();
            recoveryActions.Controls.Add(btnAddLoan);
            recoveryActions.Controls.Add(btnAddAdvance);
            TabControl detailTabs = new TabControl { Dock = DockStyle.Top, Height = 248, Font = new Font("Segoe UI", 8.75f, FontStyle.Bold) };
            detailTabs.TabPages.Add(BuildModernGridTab("Loans / Advances", _gridLoans, true));
            detailTabs.TabPages.Add(BuildForm16Tab());
            detailTabs.TabPages.Add(BuildModernGridTab("Salary History", _gridDetailSalaryHistory, true));
            detailTabs.TabPages.Add(BuildModernGridTab("Payslip History", _gridPayslipHistory, true));
            detailTabs.TabPages.Add(BuildTdsSummaryTab());

            Panel salaryDetails = BuildTableCard("Salary Structure Details", NewSalaryComponentGrid(), false);
            _lblDetailGrossTotal = new Label { Dock = DockStyle.Bottom, Height = 34, Font = DS.BodyBold, ForeColor = DS.Primary700, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 7, 14, 0), BackColor = DS.Slate50 };
            salaryDetails.Controls.Add(_lblDetailGrossTotal);

            workspace.Controls.Add(salaryDetails);
            workspace.Controls.Add(detailTabs);
            workspace.Controls.Add(recoveryActions);
            workspace.Controls.Add(summary);
            layout.Controls.Add(leftPanel, 0, 0);
            layout.Controls.Add(workspace, 1, 0);
            tab.Controls.Add(layout);
            return tab;
        }

        private void LoadEmployees()
        {
            _employees = _employeeService.GetAll().OrderBy(e => e.Name).ToList();
            BindEmployeeLists();
        }

        private void BindEmployeeLists()
        {
            BindEmployeeList(_lstSalaryEmployees, FilterEmployees(_txtSalarySearch == null ? string.Empty : _txtSalarySearch.Text));
            BindEmployeeList(_lstDetailEmployees, FilterEmployees(_txtDetailSearch == null ? string.Empty : _txtDetailSearch.Text));
        }

        private List<Employee> FilterEmployees(string term)
        {
            term = (term ?? string.Empty).Trim();
            IEnumerable<Employee> filtered = _employees;
            if (!string.IsNullOrWhiteSpace(term))
                filtered = filtered.Where(e => (e.Name ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || (e.EmployeeCode ?? string.Empty).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            return filtered.ToList();
        }

        private void RefreshAll()
        {
            if (_isInitializing || _gridProcess == null || _gridStatutory == null || _btnImport == null)
                return;
            RefreshProcessTab();
            RefreshStatutoryTab();
            UpdateWorkflowChecklist();
            _btnImport.Visible = true;
        }

        private void RefreshProcessTab()
        {
            if (_gridProcess == null || _lblSummaryEmployees == null || _lblSummaryGross == null || _lblSummaryNet == null || _lblSummaryLiability == null)
                return;
            _gridProcess.Rows.Clear();
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            List<PayrollEntry> entries = run == null ? new List<PayrollEntry>() : _payrollService.GetPayrollEntriesByRun(run.PayrollRunId);
            List<StatutoryPayment> statutoryRows = _payrollService.GetStatutoryPaymentsByMonth(CurrentMonth, CurrentYear) ?? new List<StatutoryPayment>();
            List<AttendanceRecord> attendanceRows = _attendanceService.GetMonthlyAttendanceRecords(CurrentMonth, CurrentYear) ?? new List<AttendanceRecord>();
            PayrollDashboardMetrics metrics = BuildDashboardMetrics(run, entries, statutoryRows, attendanceRows);
            string reconciliationBanner = GetAttendanceReconciliationBanner();
            if (run == null)
            {
                int activeEmployees = _employees.Count(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase));
                UpdateSummary(new PayrollSummaryDto { TotalEmployees = activeEmployees });
                UpdateAccountantDashboard(metrics, run);
                SetMonthBadge("Open", DS.Green600, DS.Green50);
                SetStatus(string.IsNullOrWhiteSpace(reconciliationBanner) ? BuildPeriodBannerMessage(run, metrics, CurrentMonth, CurrentYear) : reconciliationBanner,
                    string.IsNullOrWhiteSpace(reconciliationBanner) ? GetPeriodBannerColor(run, metrics) : DS.Amber600);
                return;
            }

            foreach (PayrollEntry entry in entries)
            {
                _gridProcess.Rows.Add(entry.EntryId, entry.EmployeeName, entry.Designation, entry.DaysPresent.ToString("0.##"), IndiaFormatHelper.FormatCurrency(entry.GrossSalary), IndiaFormatHelper.FormatCurrency(entry.EPFEmployee), IndiaFormatHelper.FormatCurrency(entry.ESIEmployee), IndiaFormatHelper.FormatCurrency(entry.TDSDeducted), IndiaFormatHelper.FormatCurrency(entry.ProfessionalTax), IndiaFormatHelper.FormatCurrency(entry.TotalDeductions), IndiaFormatHelper.FormatCurrency(entry.NetSalary));
            }

            UpdateSummary(_payrollService.GetPayrollSummary(CurrentMonth, CurrentYear));
            UpdateAccountantDashboard(metrics, run);
            bool isLocked = string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase);
            SetMonthBadge(isLocked ? "Locked" : "Open", isLocked ? DS.Red600 : DS.Green600, isLocked ? DS.Red50 : DS.Green50);
            SetStatus(string.IsNullOrWhiteSpace(reconciliationBanner) ? BuildPeriodBannerMessage(run, metrics, CurrentMonth, CurrentYear) : reconciliationBanner,
                string.IsNullOrWhiteSpace(reconciliationBanner) ? GetPeriodBannerColor(run, metrics) : DS.Amber600);
        }

        private string GetAttendanceReconciliationBanner()
        {
            try
            {
                return _attendanceService.GetSourceReconciliationBanner(CurrentMonth, CurrentYear);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PayrollForm.GetAttendanceReconciliationBanner", ex);
                return string.Empty;
            }
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

        private PayrollDashboardMetrics BuildDashboardMetrics(PayrollRun run, List<PayrollEntry> entries, List<StatutoryPayment> statutoryRows, List<AttendanceRecord> attendanceRows)
        {
            int activeEmployees = _employees.Count(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase));
            HashSet<int> activeEmployeeIds = new HashSet<int>(_employees.Where(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase)).Select(e => e.EmployeeID));
            Dictionary<int, Employee> employeeLookup = _employees
                .GroupBy(e => e.EmployeeID)
                .ToDictionary(group => group.Key, group => group.First());
            Dictionary<int, string> employeeNames = _employees
                .GroupBy(e => e.EmployeeID)
                .ToDictionary(group => group.Key, group => string.IsNullOrWhiteSpace(group.First().Name) ? "Employee " + group.Key : group.First().Name);
            List<AttendanceRecord> scopedAttendanceRows = (attendanceRows ?? new List<AttendanceRecord>())
                .Where(a => activeEmployeeIds.Contains(a.EmployeeId))
                .ToList();
            List<EmployeeSummaryDto> employeeSummaries = _employeeService.GetEmployeeSummaries()
                .Where(e => !e.IsInactive && activeEmployeeIds.Contains(e.EmployeeID))
                .ToList();
            foreach (EmployeeSummaryDto summary in employeeSummaries)
            {
                if (!employeeNames.ContainsKey(summary.EmployeeID) && !string.IsNullOrWhiteSpace(summary.Name))
                    employeeNames[summary.EmployeeID] = summary.Name;
            }
            Dictionary<int, List<EmployeeAttendanceDayDto>> liveAttendanceByEmployee = GetLiveAttendanceByEmployee(employeeSummaries);
            bool hasLiveAttendanceData = liveAttendanceByEmployee.Values.Any(rows => rows.Count > 0);
            HashSet<int> payrollAttendanceEmployeeIds = new HashSet<int>(scopedAttendanceRows.Select(a => a.EmployeeId));
            HashSet<int> liveAttendanceEmployeeIds = new HashSet<int>(liveAttendanceByEmployee.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key));
            HashSet<int> attendanceEmployeeIds = hasLiveAttendanceData ? liveAttendanceEmployeeIds : payrollAttendanceEmployeeIds;
            List<int> salaryGapEmployeeIds = new List<int>();
            foreach (Employee employee in _employees)
            {
                if (!activeEmployeeIds.Contains(employee.EmployeeID))
                    continue;

                if (!_payrollService.GetSalaryStructures(employee.EmployeeID).Any())
                    salaryGapEmployeeIds.Add(employee.EmployeeID);
            }

            int salaryMissing = salaryGapEmployeeIds.Count;
            decimal recoveries = (entries ?? new List<PayrollEntry>()).Sum(e => e.LoanDeduction + e.AdvanceDeduction);
            int payslipsPending = run == null ? 0 : (entries ?? new List<PayrollEntry>()).Count(e => !e.PayslipGenerated);
            List<StatutoryPayment> pendingStatutoryRows = (statutoryRows ?? new List<StatutoryPayment>())
                .Where(p => !string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.DueDate)
                .ToList();
            List<int> attendanceExceptionIds = activeEmployeeIds
                .Where(employeeId => !attendanceEmployeeIds.Contains(employeeId))
                .ToList();
            List<int> lateEmployeeIds = hasLiveAttendanceData
                ? liveAttendanceByEmployee
                    .Where(pair => pair.Value.Any(day => string.Equals(NormalizeAttendanceStatus(day.Status), "LATE", StringComparison.Ordinal)))
                    .Select(pair => pair.Key)
                    .ToList()
                : scopedAttendanceRows
                    .Where(a => string.Equals(NormalizeAttendanceStatus(a.Status), "LATE", StringComparison.Ordinal))
                    .Select(a => a.EmployeeId)
                    .Distinct()
                    .ToList();
            List<int> absentEmployeeIds = hasLiveAttendanceData
                ? liveAttendanceByEmployee
                    .Where(pair => pair.Value.Any(day => string.Equals(NormalizeAttendanceStatus(day.Status), "ABSENT", StringComparison.Ordinal)))
                    .Select(pair => pair.Key)
                    .ToList()
                : scopedAttendanceRows
                    .Where(a => string.Equals(NormalizeAttendanceStatus(a.Status), "ABSENT", StringComparison.Ordinal))
                    .Select(a => a.EmployeeId)
                    .Distinct()
                    .ToList();
            int presentDays = hasLiveAttendanceData
                ? liveAttendanceByEmployee.Values.Sum(rows => rows.Count(day => string.Equals(NormalizeAttendanceStatus(day.Status), "PRESENT", StringComparison.Ordinal)))
                : scopedAttendanceRows.Count(a => string.Equals(NormalizeAttendanceStatus(a.Status), "PRESENT", StringComparison.Ordinal));
            int halfDays = hasLiveAttendanceData
                ? liveAttendanceByEmployee.Values.Sum(rows => rows.Count(day => string.Equals(NormalizeAttendanceStatus(day.Status), "HALFDAY", StringComparison.Ordinal)))
                : scopedAttendanceRows.Count(a => string.Equals(NormalizeAttendanceStatus(a.Status), "HALFDAY", StringComparison.Ordinal));
            int lateDays = hasLiveAttendanceData
                ? liveAttendanceByEmployee.Values.Sum(rows => rows.Count(day => string.Equals(NormalizeAttendanceStatus(day.Status), "LATE", StringComparison.Ordinal)))
                : scopedAttendanceRows.Count(a => string.Equals(NormalizeAttendanceStatus(a.Status), "LATE", StringComparison.Ordinal));
            int absentDays = hasLiveAttendanceData
                ? liveAttendanceByEmployee.Values.Sum(rows => rows.Count(day => string.Equals(NormalizeAttendanceStatus(day.Status), "ABSENT", StringComparison.Ordinal)))
                : scopedAttendanceRows.Count(a => string.Equals(NormalizeAttendanceStatus(a.Status), "ABSENT", StringComparison.Ordinal));
            int leaveDays = hasLiveAttendanceData
                ? liveAttendanceByEmployee.Values.Sum(rows => rows.Count(day => string.Equals(NormalizeAttendanceStatus(day.Status), "LEAVE", StringComparison.Ordinal)))
                : scopedAttendanceRows.Count(a => string.Equals(NormalizeAttendanceStatus(a.Status), "LEAVE", StringComparison.Ordinal));
            int attendedDays = presentDays + halfDays + lateDays;
            int scheduledDays = attendedDays + absentDays + leaveDays;
            decimal punctualityRate = attendedDays == 0 ? 100m : ((presentDays + halfDays) * 100m) / attendedDays;
            decimal absenteeismRate = scheduledDays == 0 ? 0m : (absentDays * 100m) / scheduledDays;
            decimal overtimeHours = scopedAttendanceRows.Sum(a => a.OvertimeHours);
            DateTime referenceDate = CurrentMonth == DateTime.Today.Month && CurrentYear == DateTime.Today.Year
                ? DateTime.Today
                : new DateTime(CurrentYear, CurrentMonth, 1);
            HashSet<int> upcomingLeaveEmployees = new HashSet<int>();
            int upcomingLeaveDays = 0;
            if (hasLiveAttendanceData)
            {
                foreach (KeyValuePair<int, List<EmployeeAttendanceDayDto>> pair in liveAttendanceByEmployee)
                {
                    foreach (EmployeeAttendanceDayDto row in pair.Value.Where(day => string.Equals(NormalizeAttendanceStatus(day.Status), "LEAVE", StringComparison.Ordinal) && day.AttendanceDate.Date >= referenceDate.Date))
                    {
                        upcomingLeaveDays++;
                        upcomingLeaveEmployees.Add(pair.Key);
                    }
                }
            }
            else
            {
                foreach (AttendanceRecord row in scopedAttendanceRows.Where(a => string.Equals(NormalizeAttendanceStatus(a.Status), "LEAVE", StringComparison.Ordinal) && a.AttendanceDate.Date >= referenceDate.Date))
                {
                    upcomingLeaveDays++;
                    upcomingLeaveEmployees.Add(row.EmployeeId);
                }
            }
            List<EmployeeSummaryDto> checkedInToday = employeeSummaries.Where(e => e.CheckedInToday).ToList();
            int onLeaveToday = employeeSummaries.Count(e => e.OnLeaveToday);
            int availableForCallout = 0;
            int deployedCrew = 0;
            int activeFieldJobs = 0;
            foreach (EmployeeSummaryDto summary in checkedInToday)
            {
                List<EmployeeJobSummaryDto> jobs = _employeeService.GetEmployeeJobs(summary.EmployeeID) ?? new List<EmployeeJobSummaryDto>();
                List<EmployeeJobSummaryDto> openJobs = jobs.Where(job => IsOpenFieldJobStatus(job.Status)).ToList();
                if (openJobs.Count == 0)
                    availableForCallout++;
                else
                    deployedCrew++;
                activeFieldJobs += openJobs.Count;
            }
            decimal deploymentUtilizationRate = checkedInToday.Count == 0 ? 0m : (deployedCrew * 100m) / checkedInToday.Count;
            int overworkedEmployees = 0;
            int underutilizedEmployees = 0;
            int ghostCheckoutAlerts = 0;
            List<int> overworkedEmployeeIds = new List<int>();
            List<int> underutilizedEmployeeIds = new List<int>();
            List<int> ghostCheckoutEmployeeIds = new List<int>();
            if (hasLiveAttendanceData)
            {
                foreach (KeyValuePair<int, List<EmployeeAttendanceDayDto>> pair in liveAttendanceByEmployee)
                {
                    List<EmployeeAttendanceDayDto> rows = pair.Value;
                    if (rows.Count == 0)
                    {
                        underutilizedEmployees++;
                        underutilizedEmployeeIds.Add(pair.Key);
                        continue;
                    }

                    decimal totalHours = rows.Sum(day => day.HoursWorked);
                    int recordedDays = rows.Count(day =>
                        string.Equals(NormalizeAttendanceStatus(day.Status), "PRESENT", StringComparison.Ordinal)
                        || string.Equals(NormalizeAttendanceStatus(day.Status), "LATE", StringComparison.Ordinal)
                        || string.Equals(NormalizeAttendanceStatus(day.Status), "HALFDAY", StringComparison.Ordinal));
                    decimal averageHours = recordedDays == 0 ? 0m : totalHours / recordedDays;
                    if (recordedDays >= 5 && averageHours >= 10m)
                    {
                        overworkedEmployees++;
                        overworkedEmployeeIds.Add(pair.Key);
                    }
                    else if (recordedDays > 0 && averageHours < 6m)
                    {
                        underutilizedEmployees++;
                        underutilizedEmployeeIds.Add(pair.Key);
                    }

                    if (rows.Any(day => day.CheckInTime.HasValue && !day.CheckOutTime.HasValue && day.AttendanceDate.Date < DateTime.Today.Date))
                    {
                        ghostCheckoutAlerts++;
                        ghostCheckoutEmployeeIds.Add(pair.Key);
                    }
                }
            }
            else
            {
                underutilizedEmployees = Math.Max(0, activeEmployees - attendanceEmployeeIds.Count);
                underutilizedEmployeeIds = attendanceExceptionIds.ToList();
            }
            List<EmployeeSummaryDto> onLeaveEmployees = employeeSummaries.Where(e => e.OnLeaveToday).ToList();
            List<string> availableNowNames = new List<string>();
            List<string> fieldQueueNames = new List<string>();
            foreach (EmployeeSummaryDto summary in checkedInToday)
            {
                List<EmployeeJobSummaryDto> jobs = _employeeService.GetEmployeeJobs(summary.EmployeeID) ?? new List<EmployeeJobSummaryDto>();
                if (jobs.Any(job => IsOpenFieldJobStatus(job.Status)))
                    fieldQueueNames.Add(summary.Name);
                else
                    availableNowNames.Add(summary.Name);
            }

            int attendanceExceptions = attendanceExceptionIds.Count;
            HashSet<int> leaveReviewEmployeeIds = hasLiveAttendanceData
                ? new HashSet<int>(liveAttendanceByEmployee
                    .Where(pair => pair.Value.Any(day => string.Equals(NormalizeAttendanceStatus(day.Status), "LEAVE", StringComparison.Ordinal)))
                    .Select(pair => pair.Key))
                : new HashSet<int>(scopedAttendanceRows
                    .Where(a => string.Equals(NormalizeAttendanceStatus(a.Status), "LEAVE", StringComparison.Ordinal))
                    .Select(a => a.EmployeeId));
            HashSet<int> halfDayReviewEmployeeIds = hasLiveAttendanceData
                ? new HashSet<int>(liveAttendanceByEmployee
                    .Where(pair => pair.Value.Any(day => string.Equals(NormalizeAttendanceStatus(day.Status), "HALFDAY", StringComparison.Ordinal)))
                    .Select(pair => pair.Key))
                : new HashSet<int>(scopedAttendanceRows
                    .Where(a => string.Equals(NormalizeAttendanceStatus(a.Status), "HALFDAY", StringComparison.Ordinal))
                    .Select(a => a.EmployeeId));
            HashSet<int> missingBankEmployeeIds = new HashSet<int>(_employees
                .Where(employee => activeEmployeeIds.Contains(employee.EmployeeID)
                    && (string.IsNullOrWhiteSpace(employee.BankAccountNumber ?? employee.BankAccount)
                        || string.IsNullOrWhiteSpace(employee.BankIFSC ?? employee.IFSCCode)))
                .Select(employee => employee.EmployeeID));
            HashSet<int> kycGapEmployeeIds = new HashSet<int>(_employees
                .Where(employee => activeEmployeeIds.Contains(employee.EmployeeID)
                    && (string.IsNullOrWhiteSpace(employee.PANNumber ?? employee.PAN)
                        || string.IsNullOrWhiteSpace(employee.AadhaarNumber)
                        || string.IsNullOrWhiteSpace(employee.TaxRegime)))
                .Select(employee => employee.EmployeeID));
            HashSet<int> salaryInsightEmployeeIds = new HashSet<int>(salaryGapEmployeeIds);
            salaryInsightEmployeeIds.UnionWith(missingBankEmployeeIds);
            salaryInsightEmployeeIds.UnionWith(kycGapEmployeeIds);
            List<PayrollInsightRow> attendanceRowsPreview = BuildAttendanceInsightRows(employeeLookup, employeeNames, ghostCheckoutEmployeeIds, attendanceExceptionIds, leaveReviewEmployeeIds, halfDayReviewEmployeeIds);
            List<PayrollInsightRow> salaryRowsPreview = BuildSalaryInsightRows(employeeLookup, employeeNames, salaryGapEmployeeIds, missingBankEmployeeIds, kycGapEmployeeIds);
            List<PayrollInsightRow> lateAbsentRowsPreview = BuildLateAbsentInsightRows(employeeLookup, employeeNames, lateEmployeeIds, absentEmployeeIds);
            List<PayrollInsightRow> payslipRowsPreview = BuildPayslipInsightRows(employeeLookup, employeeNames, entries ?? new List<PayrollEntry>());

            return new PayrollDashboardMetrics
            {
                ActiveEmployees = activeEmployees,
                EmployeesIncluded = run == null ? activeEmployees : (entries ?? new List<PayrollEntry>()).Count,
                AttendanceExceptions = attendanceExceptions,
                SalarySetupGaps = salaryMissing,
                PendingStatutoryCount = pendingStatutoryRows.Count,
                PendingStatutoryAmount = pendingStatutoryRows.Sum(p => p.Amount),
                PayslipsPending = payslipsPending,
                RecoveriesThisMonth = recoveries,
                NextStatutoryDueDate = pendingStatutoryRows.Select(p => (DateTime?)p.DueDate).FirstOrDefault(),
                PunctualityRate = decimal.Round(punctualityRate, 1),
                AbsenteeismRate = decimal.Round(absenteeismRate, 1),
                LeaveDays = leaveDays,
                MonthlyOvertimeHours = decimal.Round(overtimeHours, 1),
                CheckedInToday = checkedInToday.Count,
                OnLeaveToday = onLeaveToday,
                AvailableForCallout = availableForCallout,
                ActiveFieldJobs = activeFieldJobs,
                DeploymentUtilizationRate = decimal.Round(deploymentUtilizationRate, 1),
                UpcomingLeaveDays = upcomingLeaveDays,
                UpcomingLeaveEmployees = upcomingLeaveEmployees.Count,
                UnderutilizedEmployees = Math.Max(0, underutilizedEmployees),
                OverworkedEmployees = overworkedEmployees,
                GhostCheckoutAlerts = ghostCheckoutAlerts,
                AttendanceCoverageRate = activeEmployees == 0 ? 100m : decimal.Round((attendanceEmployeeIds.Count * 100m) / activeEmployees, 1),
                UsesLiveAttendance = hasLiveAttendanceData,
                EmployeesWithAttendance = attendanceEmployeeIds.Count,
                LateEmployees = lateEmployeeIds.Count,
                AbsentEmployees = absentEmployeeIds.Count,
                AttendanceExceptionPeople = SummarizeEmployeeNames(attendanceExceptionIds.Select(id => GetEmployeeName(employeeNames, id))),
                AttendanceExceptionRoster = BuildRosterText(attendanceExceptionIds.Select(id => GetEmployeeName(employeeNames, id)), "No attendance blockers right now", 5),
                AttendanceExceptionRosterFull = BuildRosterText(attendanceExceptionIds.Select(id => GetEmployeeName(employeeNames, id)), "No attendance blockers right now", int.MaxValue),
                SalaryGapPeople = SummarizeEmployeeNames(salaryGapEmployeeIds.Select(id => GetEmployeeName(employeeNames, id))),
                SalaryGapRoster = BuildRosterText(salaryGapEmployeeIds.Select(id => GetEmployeeName(employeeNames, id)), "No salary structure blockers", 5),
                SalaryGapRosterFull = BuildRosterText(salaryGapEmployeeIds.Select(id => GetEmployeeName(employeeNames, id)), "No salary structure blockers", int.MaxValue),
                PendingStatutoryItems = SummarizePlainList(pendingStatutoryRows.Select(row => row.PaymentType), 4, "All dues cleared"),
                PayslipPendingPeople = SummarizeEmployeeNames((entries ?? new List<PayrollEntry>())
                    .Where(e => !e.PayslipGenerated)
                    .Select(e => string.IsNullOrWhiteSpace(e.EmployeeName) ? GetEmployeeName(employeeNames, e.EmployeeId) : e.EmployeeName)),
                PayslipPendingRoster = BuildRosterText((entries ?? new List<PayrollEntry>())
                    .Where(e => !e.PayslipGenerated)
                    .Select(e => string.IsNullOrWhiteSpace(e.EmployeeName) ? GetEmployeeName(employeeNames, e.EmployeeId) : e.EmployeeName), "All payslips are already generated", 5),
                PayslipPendingRosterFull = BuildRosterText((entries ?? new List<PayrollEntry>())
                    .Where(e => !e.PayslipGenerated)
                    .Select(e => string.IsNullOrWhiteSpace(e.EmployeeName) ? GetEmployeeName(employeeNames, e.EmployeeId) : e.EmployeeName), "All payslips are already generated", int.MaxValue),
                LatePeople = SummarizeEmployeeNames(lateEmployeeIds.Select(id => GetEmployeeName(employeeNames, id))),
                LateRoster = BuildRosterText(lateEmployeeIds.Select(id => GetEmployeeName(employeeNames, id)), "No late employees flagged", 5),
                LateRosterFull = BuildRosterText(lateEmployeeIds.Select(id => GetEmployeeName(employeeNames, id)), "No late employees flagged", int.MaxValue),
                AbsentPeople = SummarizeEmployeeNames(absentEmployeeIds.Select(id => GetEmployeeName(employeeNames, id))),
                AbsentRoster = BuildRosterText(absentEmployeeIds.Select(id => GetEmployeeName(employeeNames, id)), "No absent employees flagged", 5),
                AbsentRosterFull = BuildRosterText(absentEmployeeIds.Select(id => GetEmployeeName(employeeNames, id)), "No absent employees flagged", int.MaxValue),
                OnLeaveTodayPeople = SummarizeEmployeeNames(onLeaveEmployees.Select(e => e.Name)),
                OnLeaveTodayRoster = BuildRosterText(onLeaveEmployees.Select(e => e.Name), "No technicians are on leave today"),
                AvailableNowPeople = SummarizeEmployeeNames(availableNowNames),
                AvailableNowRoster = BuildRosterText(availableNowNames, "No spare crew is currently available"),
                FieldQueuePeople = SummarizeEmployeeNames(fieldQueueNames),
                FieldQueueRoster = BuildRosterText(fieldQueueNames, "No live crew is tied to an open field job"),
                WorkPatternPeople = SummarizeEmployeeNames(
                    (ghostCheckoutEmployeeIds.Any() ? ghostCheckoutEmployeeIds :
                    (overworkedEmployeeIds.Any() ? overworkedEmployeeIds : underutilizedEmployeeIds))
                    .Select(id => GetEmployeeName(employeeNames, id))),
                AttendanceInsight = new PayrollInsightCardState
                {
                    Status = attendanceExceptions == 0 ? "Clear" : attendanceExceptions + " employee(s) to review",
                    EmptyState = "No attendance blockers right now.",
                    FullDetail = BuildInsightRosterDetail(attendanceRowsPreview, "No attendance blockers right now."),
                    Badges = BuildInsightBadges(
                        new PayrollInsightBadge("Missing rows", attendanceExceptionIds.Count, DS.Amber600),
                        new PayrollInsightBadge("Leave review", leaveReviewEmployeeIds.Count, Color.FromArgb(202, 138, 4)),
                        new PayrollInsightBadge("Half-day", halfDayReviewEmployeeIds.Count, Color.FromArgb(217, 119, 6)),
                        new PayrollInsightBadge("Punch-out", ghostCheckoutEmployeeIds.Count, Color.FromArgb(180, 83, 9))),
                    Rows = attendanceRowsPreview
                },
                SalaryInsight = new PayrollInsightCardState
                {
                    Status = salaryInsightEmployeeIds.Count == 0 ? "Ready" : salaryInsightEmployeeIds.Count + " salary blocker(s)",
                    EmptyState = "No salary setup blockers.",
                    FullDetail = BuildInsightRosterDetail(salaryRowsPreview, "No salary setup blockers."),
                    Badges = BuildInsightBadges(
                        new PayrollInsightBadge("Missing structure", salaryGapEmployeeIds.Count, DS.Red600),
                        new PayrollInsightBadge("Bank details", missingBankEmployeeIds.Count, Color.FromArgb(220, 38, 38)),
                        new PayrollInsightBadge("Tax or KYC", kycGapEmployeeIds.Count, Color.FromArgb(190, 24, 93))),
                    Rows = salaryRowsPreview
                },
                LateAbsentInsight = new PayrollInsightCardState
                {
                    Status = lateEmployeeIds.Count == 0 && absentEmployeeIds.Count == 0 ? "No punctuality escalations" : (lateEmployeeIds.Count + absentEmployeeIds.Count) + " escalation(s) surfaced",
                    EmptyState = "No late or absent employees flagged.",
                    FullDetail = BuildInsightRosterDetail(lateAbsentRowsPreview, "No late or absent employees flagged."),
                    Badges = BuildInsightBadges(
                        new PayrollInsightBadge("Late arrival", lateEmployeeIds.Count, DS.Primary600),
                        new PayrollInsightBadge("Unexcused absent", absentEmployeeIds.Count, DS.Red600),
                        new PayrollInsightBadge("Checked in", checkedInToday.Count, DS.Green600)),
                    Rows = lateAbsentRowsPreview
                },
                PayslipInsight = new PayrollInsightCardState
                {
                    Status = payslipsPending == 0 ? "Complete" : payslipsPending + " employee(s) pending",
                    EmptyState = "All payslips are already generated.",
                    FullDetail = BuildInsightRosterDetail(payslipRowsPreview, "All payslips are already generated."),
                    Badges = BuildInsightBadges(
                        new PayrollInsightBadge("Pending slips", payslipsPending, DS.Green600),
                        new PayrollInsightBadge("Recovery check", (entries ?? new List<PayrollEntry>()).Count(e => !e.PayslipGenerated && (e.LoanDeduction > 0m || e.AdvanceDeduction > 0m)), Color.FromArgb(5, 150, 105)),
                        new PayrollInsightBadge("Bank check", (entries ?? new List<PayrollEntry>()).Count(e => !e.PayslipGenerated && (string.IsNullOrWhiteSpace(e.BankAccount) || string.IsNullOrWhiteSpace(e.BankIFSC))), Color.FromArgb(13, 148, 136))),
                    Rows = payslipRowsPreview
                }
            };
        }

        private int CountSalarySetupGaps(HashSet<int> activeEmployeeIds)
        {
            if (activeEmployeeIds == null || activeEmployeeIds.Count == 0)
                return 0;

            int missing = 0;
            foreach (Employee employee in _employees)
            {
                if (!activeEmployeeIds.Contains(employee.EmployeeID))
                    continue;

                if (!_payrollService.GetSalaryStructures(employee.EmployeeID).Any())
                    missing++;
            }

            return missing;
        }

        private void UpdateAccountantDashboard(PayrollDashboardMetrics metrics, PayrollRun run)
        {
            if (_lblDashPayrollStatus == null)
                return;

            metrics = metrics ?? new PayrollDashboardMetrics();
            string status = run == null ? "Open" : (run.Status ?? "Open");
            _lblDashPayrollStatus.Text = InterpretPayrollStatus(status, metrics);
            if (_lblCloseHeadline != null)
                _lblCloseHeadline.Text = BuildCloseHeadline(run, metrics);
            _lblDashPayrollStatusDetail.Text = BuildPayrollStatusDetail(run, metrics, CurrentMonth, CurrentYear);
            if (_lblCloseAction != null)
                _lblCloseAction.Text = BuildCloseAction(run, metrics);

            _lblDashAttendanceExceptions.Text = InterpretAttendanceReview(metrics);
            _lblDashAttendanceExceptionsDetail.Text = metrics.AttendanceExceptions == 0
                ? (metrics.UsesLiveAttendance ? "Live attendance looks complete across the active team" : "Payroll attendance complete across the active team")
                : metrics.AttendanceExceptionPeople;

            _lblDashSalaryGaps.Text = metrics.SalarySetupGaps == 0 ? "Ready" : (metrics.SalarySetupGaps <= 2 ? "Watch" : "Blocked");
            _lblDashSalaryGapsDetail.Text = metrics.SalarySetupGaps == 0 ? "No blocked employees" : metrics.SalaryGapPeople;

            _lblDashPendingStatutory.Text = metrics.PendingStatutoryCount == 0 ? "Clear" : (metrics.PendingStatutoryCount <= 2 ? "Due Soon" : "Queued");
            _lblDashPendingStatutoryDetail.Text = metrics.PendingStatutoryCount == 0
                ? "Nothing unpaid"
                : metrics.PendingStatutoryItems + " | " + IndiaFormatHelper.FormatCurrency(metrics.PendingStatutoryAmount);

            _lblDashPayslipsPending.Text = metrics.PayslipsPending == 0 ? "Complete" : (metrics.PayslipsPending <= 10 ? "Pending" : "Not Ready");
            _lblDashPayslipsPendingDetail.Text = metrics.PayslipsPending == 0 ? "All payslips generated" : metrics.PayslipPendingPeople;

            if (_lblDashCoverage != null)
            {
                _lblDashCoverage.Text = InterpretDataCoverage(metrics.AttendanceCoverageRate);
                _lblDashCoverageDetail.Text = FormatPercent(metrics.AttendanceCoverageRate) + " coverage | " + metrics.EmployeesWithAttendance + "/" + metrics.ActiveEmployees + " employees";
            }

            if (_lblDashRecoveryPressure != null)
            {
                _lblDashRecoveryPressure.Text = InterpretRecoveryPressure(metrics.RecoveriesThisMonth);
                _lblDashRecoveryPressureDetail.Text = IndiaFormatHelper.FormatCurrency(metrics.RecoveriesThisMonth) + " total recoveries flowing into this run";
            }

            if (_lblDashComplianceTiming != null)
            {
                _lblDashComplianceTiming.Text = InterpretComplianceTiming(metrics.NextStatutoryDueDate);
                _lblDashComplianceTimingDetail.Text = metrics.NextStatutoryDueDate.HasValue
                    ? "Next due " + IndiaFormatHelper.FormatDate(metrics.NextStatutoryDueDate.Value)
                    : "No unpaid statutory items are waiting";
            }

            if (_lblDashOvertimeRisk != null)
            {
                _lblDashOvertimeRisk.Text = InterpretOvertimeRisk(metrics.MonthlyOvertimeHours, metrics.GhostCheckoutAlerts);
                _lblDashOvertimeRiskDetail.Text = metrics.GhostCheckoutAlerts > 0
                    ? metrics.GhostCheckoutAlerts + " incomplete check-out log(s) need review"
                    : metrics.MonthlyOvertimeHours.ToString("0.#") + " overtime hour(s) recorded this month";
            }

            ApplyInsightCard(_attendanceInsightCard, metrics.AttendanceInsight);
            ApplyInsightCard(_salaryInsightCard, metrics.SalaryInsight);
            ApplyInsightCard(_lateAbsentInsightCard, metrics.LateAbsentInsight);
            ApplyInsightCard(_payslipInsightCard, metrics.PayslipInsight);

            UpdateDashboardCharts(metrics);
        }

        private Dictionary<int, List<EmployeeAttendanceDayDto>> GetLiveAttendanceByEmployee(List<EmployeeSummaryDto> employeeSummaries)
        {
            var result = new Dictionary<int, List<EmployeeAttendanceDayDto>>();
            foreach (EmployeeSummaryDto employee in employeeSummaries ?? new List<EmployeeSummaryDto>())
                result[employee.EmployeeID] = _employeeService.GetEmployeeAttendance(employee.EmployeeID, CurrentYear, CurrentMonth) ?? new List<EmployeeAttendanceDayDto>();
            return result;
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

        private void ImportPayrollFiles()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Import Payroll or Attendance Excel",
                Filter = "Payroll Excel or CSV|*.xlsx;*.xls;*.csv|Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*",
                Multiselect = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
                    return;

                ServiceResult<PayrollImportReport> result = _importService.ImportFiles(dialog.FileNames, CurrentMonth, CurrentYear);
                SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);

                string details = BuildImportSummary(result);
                MessageBox.Show(details, result.Success ? "Payroll Import Complete" : "Payroll Import Failed", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                if (result.Success)
                {
                    LoadEmployees();
                    RefreshAll();
                }
            }
        }

        private void ImportHistoricalData()
        {
            ServiceResult<PayrollImportReport> result = _importService.ImportFromSourceFolder();
            SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
            MessageBox.Show(BuildImportSummary(result), result.Success ? "Import Complete" : "Import Failed", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            LoadEmployees();
            RefreshAll();
        }

        private void GenerateAllPayslips()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            if (run == null)
                return;
            using (Form progress = BuildBusyDialog("Generating payslips", "Payslips are being generated. Please wait while the batch finishes."))
            {
                ToggleBusyState(true);
                SetStatus("Generating payslips for " + CurrentMonth.ToString("00") + "/" + CurrentYear + "...", Color.FromArgb(41, 128, 185));
                ServiceResult<List<string>> result = null;
                progress.Shown += async (s, e) =>
                {
                    try
                    {
                        result = await Task.Run(() => _payslipService.GenerateBatchPayslips(run.PayrollRunId));
                    }
                    finally
                    {
                        progress.Close();
                    }
                };

                progress.ShowDialog(this);
                ToggleBusyState(false);
                if (result == null)
                    return;

                SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
                RefreshProcessTab();
                if (!result.Success)
                    MessageBox.Show(result.Message, "Payslips", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void GenerateSelectedPayslip()
        {
            if (_gridProcess.CurrentRow == null)
                return;

            int entryId = Convert.ToInt32(_gridProcess.CurrentRow.Cells["EntryId"].Value);
            string employeeName = Convert.ToString(_gridProcess.CurrentRow.Cells["Name"].Value);
            GenerateSinglePayslip(entryId, employeeName);
        }

        private void GenerateSinglePayslip(int entryId, string employeeName)
        {
            string personLabel = string.IsNullOrWhiteSpace(employeeName) ? "the selected employee" : employeeName.Trim();

            using (Form progress = BuildBusyDialog("Generating payslip", "Generating a payslip for " + personLabel + ". Please wait."))
            {
                ToggleBusyState(true);
                SetStatus("Generating payslip for " + personLabel + "...", Color.FromArgb(41, 128, 185));
                ServiceResult<string> result = null;
                progress.Shown += async (s, e) =>
                {
                    try
                    {
                        result = await Task.Run(() => _payslipService.GeneratePayslip(entryId));
                    }
                    finally
                    {
                        progress.Close();
                    }
                };

                progress.ShowDialog(this);
                ToggleBusyState(false);
                if (result == null)
                    return;

                RefreshProcessTab();
                LoadEmployeeDetails();
                OpenFileIfExists(result);
                if (!result.Success)
                    MessageBox.Show(result.Message, "Payslip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            {
                UpdateSalarySelectionSummary(null, null);
                return;
            }
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
            UpdateSalarySelectionSummary(employee, current);
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
            {
                _lblSalaryValidation.Text = "Select an employee before saving the salary structure.";
                _lblSalaryValidation.ForeColor = Color.Firebrick;
                return;
            }
            if (_numBasic.Value <= 0)
            {
                _lblSalaryValidation.Text = "Basic Salary is required and must be greater than zero.";
                _lblSalaryValidation.ForeColor = Color.Firebrick;
                _numBasic.Focus();
                return;
            }
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
            {
                UpdateDetailEmployeeSummary(null);
                return;
            }

            BindSalaryHistoryGrid(_gridDetailSalaryHistory, _payrollService.GetSalaryStructures(employee.EmployeeID));
            BindSalaryComponentGrid(_payrollService.GetSalaryStructures(employee.EmployeeID).FirstOrDefault());
            BindPayslipHistory(_payrollService.GetPayrollEntriesByEmployee(employee.EmployeeID));
            List<TDSCalculation> tdsRows = _payrollService.GetTdsCalculationsByEmployee(employee.EmployeeID);
            BindTdsGrid(tdsRows);
            BindForm16Grid(tdsRows);
            BindLoansGrid(_payrollService.GetLoansByEmployee(employee.EmployeeID), _payrollService.GetAdvancesByEmployee(employee.EmployeeID));
            UpdateDetailEmployeeSummary(employee);
        }

        private void AddLoan()
        {
            Employee employee = _lstDetailEmployees.SelectedItem as Employee;
            if (employee == null)
            {
                MessageBox.Show("Select an employee before creating a loan.", "Employee required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
            {
                MessageBox.Show("Select an employee before creating a salary advance.", "Employee required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
            foreach (string col in new[] { "Effective From", "Effective To", "Basic", "DA", "HRA", "Other Allowances", "Gross Salary", "Actions" })
                grid.Columns.Add(col, col);
            foreach (SalaryStructure row in history)
                grid.Rows.Add(IndiaFormatHelper.FormatDate(row.EffectiveFrom), IndiaFormatHelper.FormatDate(row.EffectiveTo), IndiaFormatHelper.FormatCurrency(row.BasicSalary), IndiaFormatHelper.FormatCurrency(row.DA), IndiaFormatHelper.FormatCurrency(row.HRA), IndiaFormatHelper.FormatCurrency(row.OtherAllowances), IndiaFormatHelper.FormatCurrency(row.GrossSalary), "View  Edit  Delete");
            PolishPayrollTable(grid, "Actions", new[] { "Basic", "DA", "HRA", "Other Allowances", "Gross Salary" });
        }

        private void BindSalaryComponentGrid(SalaryStructure current)
        {
            if (_gridSalaryComponents == null)
                return;
            _gridSalaryComponents.Rows.Clear();
            current = current ?? new SalaryStructure();
            _gridSalaryComponents.Rows.Add("Basic", "Earning", "Fixed", current.BasicSalary.ToString("N2"));
            _gridSalaryComponents.Rows.Add("DA", "Earning", "Fixed", current.DA.ToString("N2"));
            _gridSalaryComponents.Rows.Add("HRA", "Earning", "Fixed", current.HRA.ToString("N2"));
            _gridSalaryComponents.Rows.Add("Special Allowance", "Earning", "Fixed", current.SpecialAllowance.ToString("N2"));
            _gridSalaryComponents.Rows.Add("Conveyance Allowance", "Earning", "Fixed", current.ConveyanceAllowance.ToString("N2"));
            _gridSalaryComponents.Rows.Add("Medical Allowance", "Earning", "Fixed", current.MedicalAllowance.ToString("N2"));
            _gridSalaryComponents.Rows.Add("LTA", "Earning", "Fixed", current.LTA.ToString("N2"));
            _gridSalaryComponents.Rows.Add("Other Allowances", "Earning", "Fixed", current.OtherAllowances.ToString("N2"));
            PolishPayrollTable(_gridSalaryComponents, null, new[] { "Amount" });
            if (_lblDetailGrossTotal != null)
                _lblDetailGrossTotal.Text = "Total Gross Salary    " + IndiaFormatHelper.FormatCurrency(current.GrossSalary);
        }

        private void BindPayslipHistory(List<PayrollEntry> entries)
        {
            _gridPayslipHistory.Columns.Clear();
            _gridPayslipHistory.Rows.Clear();
            _gridPayslipHistory.Columns.Add("EntryId", "EntryId");
            _gridPayslipHistory.Columns["EntryId"].Visible = false;
            foreach (string col in new[] { "Month", "Generated On", "Net Pay", "PayslipPath", "Download" })
                _gridPayslipHistory.Columns.Add(col, col);
            _gridPayslipHistory.Columns["PayslipPath"].Visible = false;
            foreach (PayrollEntry row in entries)
                _gridPayslipHistory.Rows.Add(
                    row.EntryId,
                    new DateTime(row.PayrollYear, row.PayrollMonth, 1).ToString("MMM yyyy"),
                    row.PayslipGenerated ? "Generated" : "-",
                    IndiaFormatHelper.FormatCurrency(row.NetSalary),
                    row.PayslipPath,
                    File.Exists(row.PayslipPath) ? "Download" : "Generate");
            PolishPayrollTable(_gridPayslipHistory, "Download", new[] { "Net Pay" });
        }

        private void HandlePayslipHistoryAction(int rowIndex)
        {
            HandlePayslipHistoryAction(rowIndex, -1);
        }

        private void HandlePayslipHistoryAction(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridPayslipHistory.Rows.Count)
                return;
            if (columnIndex >= 0 && _gridPayslipHistory.Columns[columnIndex].Name != "Download")
                return;

            DataGridViewRow row = _gridPayslipHistory.Rows[rowIndex];
            string path = Convert.ToString(row.Cells["PayslipPath"].Value);
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return;
            }

            int entryId = Convert.ToInt32(row.Cells["EntryId"].Value);
            string month = Convert.ToString(row.Cells["Month"].Value);
            GenerateSinglePayslip(entryId, "the payslip for " + month);
        }

        private void BindTdsGrid(List<TDSCalculation> rows)
        {
            _gridTds.Columns.Clear();
            _gridTds.Rows.Clear();
            foreach (string col in new[] { "Financial Year", "TDS Deducted", "TDS Paid", "Pending", "Actions" })
                _gridTds.Columns.Add(col, col);
            decimal deducted = 0m;
            decimal paid = 0m;
            foreach (TDSCalculation row in rows)
            {
                decimal pending = Math.Max(0m, row.AnnualTaxLiability - row.TDSPaidToDate);
                deducted += row.AnnualTaxLiability;
                paid += row.TDSPaidToDate;
                _gridTds.Rows.Add(row.FinancialYear, IndiaFormatHelper.FormatCurrency(row.AnnualTaxLiability), IndiaFormatHelper.FormatCurrency(row.TDSPaidToDate), IndiaFormatHelper.FormatCurrency(pending), "View");
            }
            PolishPayrollTable(_gridTds, "Actions", new[] { "TDS Deducted", "TDS Paid", "Pending" });
            if (_lblTdsDeducted != null)
                _lblTdsDeducted.Text = IndiaFormatHelper.FormatCurrency(deducted);
            if (_lblTdsPaid != null)
                _lblTdsPaid.Text = IndiaFormatHelper.FormatCurrency(paid);
            if (_lblTdsPending != null)
                _lblTdsPending.Text = IndiaFormatHelper.FormatCurrency(Math.Max(0m, deducted - paid));
        }

        private void HandleTdsGridAction(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridTds.Rows.Count || columnIndex < 0 || _gridTds.Columns[columnIndex].Name != "Actions")
                return;

            DataGridViewRow row = _gridTds.Rows[rowIndex];
            string detail = "Financial Year: " + Convert.ToString(row.Cells["Financial Year"].Value)
                + Environment.NewLine + "TDS Deducted: " + Convert.ToString(row.Cells["TDS Deducted"].Value)
                + Environment.NewLine + "TDS Paid: " + Convert.ToString(row.Cells["TDS Paid"].Value)
                + Environment.NewLine + "Pending: " + Convert.ToString(row.Cells["Pending"].Value);
            ShowRosterDetailDialog("TDS Report", "Employee tax summary", detail, DS.Primary600);
        }

        private void BindLoansGrid(List<EmployeeLoan> loans, List<SalaryAdvance> advances)
        {
            _gridLoans.Columns.Clear();
            _gridLoans.Rows.Clear();
            foreach (string col in new[] { "Effective From", "Effective To", "Type", "Description", "Monthly Deduction", "Outstanding Balance", "Actions" })
                _gridLoans.Columns.Add(col, col);
            foreach (EmployeeLoan loan in loans)
                _gridLoans.Rows.Add(IndiaFormatHelper.FormatDate(loan.LoanDate), "-", "Loan", loan.Purpose, IndiaFormatHelper.FormatCurrency(loan.MonthlyDeduction), IndiaFormatHelper.FormatCurrency(loan.RemainingBalance), "View  Edit  Delete");
            foreach (SalaryAdvance advance in advances)
                _gridLoans.Rows.Add(IndiaFormatHelper.FormatDate(advance.AdvanceDate), "-", "Advance", "Salary advance", IndiaFormatHelper.FormatCurrency(advance.AdvanceAmount), advance.Recovered ? "Recovered" : IndiaFormatHelper.FormatCurrency(advance.AdvanceAmount), "View  Edit  Delete");
            PolishPayrollTable(_gridLoans, "Actions", new[] { "Monthly Deduction", "Outstanding Balance" });
        }

        private void HandleLoansGridAction(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridLoans.Rows.Count || columnIndex < 0 || _gridLoans.Columns[columnIndex].Name != "Actions")
                return;

            DataGridViewRow row = _gridLoans.Rows[rowIndex];
            string detail = "Type: " + Convert.ToString(row.Cells["Type"].Value)
                + Environment.NewLine + "Description: " + Convert.ToString(row.Cells["Description"].Value)
                + Environment.NewLine + "Effective From: " + Convert.ToString(row.Cells["Effective From"].Value)
                + Environment.NewLine + "Monthly Deduction: " + Convert.ToString(row.Cells["Monthly Deduction"].Value)
                + Environment.NewLine + "Outstanding Balance: " + Convert.ToString(row.Cells["Outstanding Balance"].Value);
            ShowRosterDetailDialog("Recovery Report", "Loan / advance detail", detail, DS.Primary600);
        }

        private void BindForm16Grid(List<TDSCalculation> rows)
        {
            if (_gridForm16 == null)
                return;
            _gridForm16.Columns.Clear();
            _gridForm16.Rows.Clear();
            foreach (string col in new[] { "Financial Year", "Form 16 Generated On", "Download" })
                _gridForm16.Columns.Add(col, col);
            foreach (TDSCalculation row in rows ?? new List<TDSCalculation>())
                _gridForm16.Rows.Add(row.FinancialYear, "-", "Download");
            PolishPayrollTable(_gridForm16, "Download", null);
        }

        private void HandleForm16GridAction(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridForm16.Rows.Count || columnIndex < 0 || _gridForm16.Columns[columnIndex].Name != "Download")
                return;

            Employee employee = _lstDetailEmployees.SelectedItem as Employee;
            if (employee == null)
                return;

            string financialYear = Convert.ToString(_gridForm16.Rows[rowIndex].Cells["Financial Year"].Value);
            if (!string.IsNullOrWhiteSpace(financialYear))
                OpenFileIfExists(_reportService.GenerateForm16(employee.EmployeeID, financialYear));
        }

        private void UpdateWorkflowChecklist()
        {
            PayrollRun run = _payrollService.GetPayrollRun(CurrentMonth, CurrentYear);
            List<StatutoryPayment> statutoryRows = _payrollService.GetStatutoryPaymentsByMonth(CurrentMonth, CurrentYear) ?? new List<StatutoryPayment>();
            List<AttendanceRecord> attendanceRows = _attendanceService.GetMonthlyAttendanceRecords(CurrentMonth, CurrentYear) ?? new List<AttendanceRecord>();
            List<PayrollEntry> entries = run == null ? new List<PayrollEntry>() : _payrollService.GetPayrollEntriesByRun(run.PayrollRunId);
            PayrollDashboardMetrics metrics = BuildDashboardMetrics(run, entries, statutoryRows, attendanceRows);
            bool isLocked = run != null && string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase);

            SetWorkflowStep(_lblStepImportAttendance, metrics.AttendanceExceptions == 0 ? "Ready" : metrics.AttendanceExceptions + " employee(s) missing", metrics.AttendanceExceptions == 0 ? DS.Green600 : DS.Amber600);
            SetWorkflowStep(_lblStepReviewExceptions, metrics.SalarySetupGaps == 0 ? "No salary blockers" : metrics.SalarySetupGaps + " salary setup gap(s)", metrics.SalarySetupGaps == 0 ? DS.Green600 : DS.Red600);
            SetWorkflowStep(_lblStepRunPayroll, run == null ? "Pending run" : "Run " + run.Status, run == null ? DS.Amber600 : DS.Green600);
            SetWorkflowStep(_lblStepVerifyStatutory, run == null ? "Run payroll first" : (metrics.PendingStatutoryCount == 0 ? "All reviewed" : metrics.PendingStatutoryCount + " pending item(s)"), run == null ? DS.Slate500 : (metrics.PendingStatutoryCount == 0 ? DS.Green600 : DS.Amber600));
            SetWorkflowStep(_lblStepLockPayroll, isLocked ? "Month locked" : "Lock after review", isLocked ? DS.Red600 : DS.Slate500);
        }

        private void UpdateSalarySelectionSummary(Employee employee, SalaryStructure current)
        {
            if (employee == null)
            {
                UpdateEmployeeSummaryLabels(_lblSalaryAvatar, _lblSalaryEmployeeName, _lblSalaryEmployeeRole, _lblSalaryGross, _lblSalaryEffectiveFrom, _lblSalaryStatus, null, null);
                return;
            }

            UpdateEmployeeSummaryLabels(_lblSalaryAvatar, _lblSalaryEmployeeName, _lblSalaryEmployeeRole, _lblSalaryGross, _lblSalaryEffectiveFrom, _lblSalaryStatus, employee, current);
        }

        private void UpdateDetailEmployeeSummary(Employee employee)
        {
            if (employee == null)
            {
                UpdateEmployeeSummaryLabels(_lblDetailAvatar, _lblDetailEmployeeName, _lblDetailEmployeeRole, _lblDetailGross, _lblDetailEffectiveFrom, _lblDetailStatus, null, null);
                return;
            }

            SalaryStructure current = _payrollService.GetSalaryStructures(employee.EmployeeID).FirstOrDefault();
            UpdateEmployeeSummaryLabels(_lblDetailAvatar, _lblDetailEmployeeName, _lblDetailEmployeeRole, _lblDetailGross, _lblDetailEffectiveFrom, _lblDetailStatus, employee, current);
        }

        private void UpdateEmployeeSummaryLabels(Label avatarLabel, Label nameLabel, Label roleLabel, Label grossLabel, Label effectiveLabel, Label statusLabel, Employee employee, SalaryStructure current)
        {
            if (nameLabel == null || roleLabel == null || grossLabel == null || effectiveLabel == null || statusLabel == null)
                return;

            if (employee == null)
            {
                if (avatarLabel != null)
                    avatarLabel.Text = "--";
                nameLabel.Text = "Select an employee";
                roleLabel.Text = "Employee details will appear here";
                SetSummaryMetric(grossLabel, "Current Gross", "₹0.00", DS.Primary600);
                SetSummaryMetric(effectiveLabel, "Effective From", "-", DS.Green600);
                SetSummaryMetric(statusLabel, "Status", "-", DS.Slate600);
                return;
            }

            if (avatarLabel != null)
            {
                avatarLabel.Text = GetEmployeeInitials(employee);
                avatarLabel.BackColor = EmployeeAvatarColor(employee.EmployeeID);
            }
            nameLabel.Text = (employee.Name ?? "Employee") + " (" + (employee.EmployeeCode ?? "-") + ")";
            roleLabel.Text = string.IsNullOrWhiteSpace(employee.Designation) ? "Role not set" : employee.Designation;
            SetSummaryMetric(grossLabel, "Current Gross", current == null ? "₹0.00" : IndiaFormatHelper.FormatCurrency(current.GrossSalary), DS.Primary600);
            SetSummaryMetric(effectiveLabel, "Effective From", current == null ? "-" : IndiaFormatHelper.FormatDate(current.EffectiveFrom), DS.Green600);
            bool active = string.Equals(employee.Status, "Active", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(employee.Status);
            SetSummaryMetric(statusLabel, "Status", active ? "• Active" : "• Inactive", active ? DS.Green600 : DS.Slate600);
        }

        private void SetSummaryMetric(Label label, string title, string value, Color valueColor)
        {
            label.Text = title + Environment.NewLine + value;
            label.ForeColor = valueColor == DS.Slate600 ? DS.Slate700 : valueColor;
        }

        private void OpenFileIfExists(ServiceResult<string> result)
        {
            SetStatus(result.Message, result.Success ? Color.FromArgb(39, 174, 96) : Color.Firebrick);
            if (result.Success && File.Exists(result.Data))
                Process.Start(new ProcessStartInfo(result.Data) { UseShellExecute = true });
        }

        private static string BuildImportSummary(ServiceResult<PayrollImportReport> result)
        {
            if (result == null)
                return "Payroll import could not be completed.";

            if (!result.Success || result.Data == null)
                return result.Message ?? "Payroll import failed.";

            PayrollImportReport report = result.Data;
            var lines = new List<string>
            {
                result.Message,
                string.Empty,
                "Files processed: " + report.FilesProcessed,
                "Payroll entries imported: " + report.PayrollEntriesImported,
                "Attendance records imported: " + report.AttendanceRecordsImported,
                "Employees matched: " + report.EmployeesMatched,
                "New employees created: " + report.NewEmployeesCreated,
                "Salary structures imported: " + report.SalaryStructuresImported
            };

            if (report.Warnings.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Warnings:");
                foreach (string warning in report.Warnings.Take(5))
                    lines.Add("- " + warning);
                if (report.Warnings.Count > 5)
                    lines.Add("- " + (report.Warnings.Count - 5) + " more warning(s).");
            }

            return string.Join(Environment.NewLine, lines);
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
            button.AutoEllipsis = true;
            button.FlatAppearance.BorderSize = light ? 1 : 0;
            button.FlatAppearance.BorderColor = light ? DS.BorderStrong : backColor;
            button.FlatAppearance.MouseOverBackColor = light ? DS.BgCardHov : DS.Lighten(backColor, 0.08f);
            button.FlatAppearance.MouseDownBackColor = light ? DS.Slate100 : DS.Darken(backColor, 0.10f);
            DS.Rounded(button, DS.RadiusSm);
            return button;
        }

        private Panel BuildPayrollCloseCockpit()
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Top;
            card.Height = 116;
            card.Padding = new Padding(16, 12, 16, 12);

            Panel left = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0, 0, 12, 0) };
            Label title = new Label
            {
                Text = "Payroll Close Cockpit",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label subtitle = new Label
            {
                Text = "Close the selected payroll period from one blocker-led command surface.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label body = new Label
            {
                Text = "Start with readiness, clear the employee blockers, then move into payroll execution only when the month is clean enough to close with confidence.",
                Dock = DockStyle.Top,
                Height = 30,
                Font = DS.Small,
                ForeColor = DS.Slate600
            };
            FlowLayoutPanel chips = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 28, Padding = Padding.Empty, WrapContents = false };
            chips.Controls.Add(WorkforceModuleVisuals.CreateChip("Exception-first review", Color.FromArgb(255, 247, 237), DS.Amber600));
            chips.Controls.Add(WorkforceModuleVisuals.CreateChip("Statutory due dates visible", Color.FromArgb(245, 243, 255), Color.FromArgb(109, 40, 217)));
            chips.Controls.Add(WorkforceModuleVisuals.CreateChip("Run only when clean", Color.FromArgb(239, 246, 255), DS.Primary700));
            left.Controls.Add(chips);
            left.Controls.Add(body);
            left.Controls.Add(subtitle);
            left.Controls.Add(title);

            Panel workflow = BuildWorkflowStrip();
            workflow.Dock = DockStyle.Right;
            workflow.Width = 720;
            workflow.Height = 84;
            workflow.BackColor = Color.White;
            workflow.Padding = new Padding(4, 4, 0, 0);
            card.Resize += (s, e) => workflow.Width = Math.Min(760, Math.Max(520, card.ClientSize.Width / 2));

            card.Controls.Add(left);
            card.Controls.Add(workflow);
            return card;
        }

        private Panel BuildWorkflowStrip()
        {
            Panel workflowStrip = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Color.White, Padding = new Padding(0, 4, 0, 4) };
            TableLayoutPanel workflowGrid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 5, RowCount = 1 };
            for (int i = 0; i < 5; i++)
                workflowGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            workflowGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            workflowGrid.Controls.Add(BuildWorkflowStepCard("1. Attendance", "Import or review the month attendance.", out _lblStepImportAttendance), 0, 0);
            workflowGrid.Controls.Add(BuildWorkflowStepCard("2. Exceptions", "Clear missing salary or attendance gaps.", out _lblStepReviewExceptions), 1, 0);
            workflowGrid.Controls.Add(BuildWorkflowStepCard("3. Run Payroll", "Calculate payroll for the selected month.", out _lblStepRunPayroll), 2, 0);
            workflowGrid.Controls.Add(BuildWorkflowStepCard("4. Statutory", "Check EPF, ESI, PT, and TDS output.", out _lblStepVerifyStatutory), 3, 0);
            workflowGrid.Controls.Add(BuildWorkflowStepCard("5. Lock", "Freeze the month after validation.", out _lblStepLockPayroll), 4, 0);
            workflowStrip.Controls.Add(workflowGrid);
            return workflowStrip;
        }

        private Panel BuildWorkflowStepCard(string title, string hint, out Label detailLabel)
        {
            Panel card = new Panel { BackColor = Color.White };
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 68);
            card.Margin = new Padding(0, 0, 8, 0);
            card.Padding = new Padding(4, 2, 4, 2);
            Label number = new Label
            {
                Text = title.Split('.')[0],
                Dock = DockStyle.Top,
                Height = 24,
                Width = 24,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            number.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(DS.Primary600))
                    e.Graphics.FillEllipse(brush, new Rectangle((number.Width - 22) / 2, 1, 22, 22));
                TextRenderer.DrawText(e.Graphics, number.Text, number.Font, new Rectangle(0, 0, number.Width, number.Height), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            Label titleLabel = new Label { Text = title.Contains(". ") ? title.Substring(title.IndexOf(". ", StringComparison.Ordinal) + 2) : title, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8.2F, FontStyle.Bold), ForeColor = DS.Slate900, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            detailLabel = new Label { Text = hint, Dock = DockStyle.Top, Height = 18, Font = DS.Small, ForeColor = DS.Slate600, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            card.Controls.Add(detailLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(number);
            return card;
        }

        private Panel BuildCloseCockpitCard(out Label statusLabel, out Label headlineLabel, out Label detailLabel, out Label actionLabel)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.MinimumSize = new Size(0, 156);
            card.Margin = new Padding(0, 0, 10, 8);
            card.Padding = new Padding(16, 14, 16, 14);

            Label eyebrow = new Label
            {
                Text = "CURRENT PERIOD",
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = DS.Primary600
            };

            Label status = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };

            Label headline = new Label
            {
                Text = "Review the blockers before calculating this month.",
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = DS.Slate700,
                Padding = new Padding(0, 2, 0, 0)
            };

            Label detail = new Label
            {
                Text = "Payroll readiness will appear here after the month is assessed.",
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Font = DS.Small,
                ForeColor = DS.Slate600,
                Padding = new Padding(0, 6, 0, 0)
            };

            Panel actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = DS.Primary50,
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 8, 0, 0)
            };
            DS.Rounded(actionPanel, DS.RadiusMd);

            Label actionTitle = new Label
            {
                Text = "Next recommended action",
                Dock = DockStyle.Top,
                Height = 16,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = DS.Primary700
            };

            Label action = new Label
            {
                Text = "Clear attendance and salary blockers before running payroll.",
                Dock = DockStyle.Fill,
                Font = DS.SmallBold,
                ForeColor = DS.Slate700
            };

            actionPanel.Controls.Add(action);
            actionPanel.Controls.Add(actionTitle);

            card.Controls.Add(actionPanel);
            card.Controls.Add(detail);
            card.Controls.Add(headline);
            card.Controls.Add(status);
            card.Controls.Add(eyebrow);

            statusLabel = status;
            headlineLabel = headline;
            detailLabel = detail;
            actionLabel = action;
            return card;
        }

        private Panel MakeAccountantCard(string title, string value, string subtitle, Color accent, out Label valueLabel, out Label subtitleLabel, Action cardAction = null)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 88);
            card.Margin = new Padding(0, 0, 10, 8);
            card.Padding = new Padding(12, 10, 12, 10);

            Panel icon = new Panel { Location = new Point(12, 18), Size = new Size(30, 30), BackColor = DS.Lighten(accent, 0.84f) };
            DS.Rounded(icon, 7);
            icon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Font font = new Font("Segoe UI", 13f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(accent))
                    e.Graphics.DrawString("•", font, brush, new PointF(7, 3));
            };

            Label titleLabel = new Label { Text = title, Location = new Point(58, 14), Size = new Size(180, 18), Font = DS.Small, ForeColor = DS.Slate600, AutoEllipsis = true };
            Label metricValue = new Label { Text = value, Location = new Point(58, 36), Size = new Size(210, 24), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            Label detail = new Label { Text = subtitle, Location = new Point(58, 64), Size = new Size(210, 16), Font = DS.Small, ForeColor = DS.Slate500, AutoEllipsis = true };
            valueLabel = metricValue;
            subtitleLabel = detail;
            card.Resize += (s, e) =>
            {
                int textWidth = Math.Max(90, card.ClientSize.Width - 64);
                titleLabel.Width = textWidth;
                metricValue.Width = textWidth;
                detail.Width = textWidth;
                detail.Height = Math.Max(18, TextRenderer.MeasureText(detail.Text ?? string.Empty, detail.Font, new Size(textWidth, 0), TextFormatFlags.WordBreak).Height);
                card.Height = Math.Max(card.MinimumSize.Height, detail.Bottom + 8);
            };
            card.Controls.AddRange(new Control[] { icon, titleLabel, metricValue, detail });
            MakeCardClickable(card, cardAction ?? (() => ShowDashboardCardReport(title, metricValue, detail, accent)));
            return card;
        }

        private Panel BuildDashboardSectionIntro(string title, string body)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.White,
                Padding = new Padding(0, 2, 0, 4)
            };
            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 18, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label bodyLabel = new Label { Text = body, Dock = DockStyle.Fill, Font = DS.Small, ForeColor = DS.Slate500 };
            panel.Controls.Add(bodyLabel);
            panel.Controls.Add(titleLabel);
            return panel;
        }

        private Panel BuildChartCard(string title, string body, Control chart)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Margin = new Padding(0, 0, 12, 12);
            card.Padding = new Padding(18, 16, 18, 18);

            Label titleLabel = new Label { Text = title, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.75f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label bodyLabel = new Label { Text = body, Dock = DockStyle.Top, Height = 34, Font = DS.Small, ForeColor = DS.Slate500 };
            chart.Dock = DockStyle.Top;
            chart.Height = 220;

            card.Controls.Add(chart);
            card.Controls.Add(bodyLabel);
            card.Controls.Add(titleLabel);
            return card;
        }

        private Panel BuildRosterCard(string title, string hint, Color accent, out Label statusLabel, out Label bodyLabel)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 172);
            card.Margin = new Padding(0, 0, 12, 12);
            card.Padding = new Padding(18, 16, 18, 16);

            Panel accentBar = new Panel
            {
                Location = new Point(18, 18),
                Size = new Size(6, 44),
                BackColor = accent
            };
            DS.Rounded(accentBar, 3);

            Label titleLabel = new Label
            {
                Text = title,
                Location = new Point(34, 16),
                Size = new Size(260, 22),
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            Label hintLabel = new Label
            {
                Text = hint,
                Location = new Point(34, 42),
                Size = new Size(320, 34),
                Font = DS.Small,
                ForeColor = DS.Slate500
            };

            Label status = new Label
            {
                Text = "Checking",
                Location = new Point(18, 88),
                Size = new Size(320, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = accent
            };

            Label body = new Label
            {
                Text = "Waiting for payroll data...",
                Location = new Point(18, 116),
                Size = new Size(320, 40),
                Font = DS.Small,
                ForeColor = DS.Slate600
            };

            Button viewMore = new Button
            {
                Text = "View more",
                Size = new Size(88, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = accent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            viewMore.FlatAppearance.BorderSize = 1;
            viewMore.FlatAppearance.BorderColor = DS.Lighten(accent, 0.5f);
            viewMore.FlatAppearance.MouseOverBackColor = DS.Lighten(accent, 0.92f);
            viewMore.FlatAppearance.MouseDownBackColor = DS.Lighten(accent, 0.88f);
            DS.Rounded(viewMore, 8);
            viewMore.Click += (s, e) => ShowRosterDetailDialog(title, status.Text, Convert.ToString(body.Tag) ?? body.Text, accent);

            card.Resize += (s, e) =>
            {
                int innerWidth = Math.Max(160, card.ClientSize.Width - 36);
                int bodyWidth = Math.Max(140, innerWidth - 16);
                titleLabel.Width = Math.Max(120, innerWidth - 16);
                hintLabel.Width = Math.Max(140, innerWidth - 16);
                hintLabel.Height = Math.Max(34, TextRenderer.MeasureText(hintLabel.Text ?? string.Empty, hintLabel.Font, new Size(hintLabel.Width, 0), TextFormatFlags.WordBreak).Height);
                status.Width = bodyWidth;
                body.Width = bodyWidth;
                body.Height = Math.Max(40, TextRenderer.MeasureText(body.Text ?? string.Empty, body.Font, new Size(bodyWidth, 0), TextFormatFlags.WordBreak).Height);
                body.Top = status.Bottom + 6;
                viewMore.Location = new Point(Math.Max(18, card.ClientSize.Width - viewMore.Width - 18), Math.Max(body.Bottom + 10, card.ClientSize.Height - viewMore.Height - 16));
                card.Height = Math.Max(card.MinimumSize.Height, viewMore.Bottom + 16);
            };

            card.Controls.AddRange(new Control[] { accentBar, titleLabel, hintLabel, status, body, viewMore });
            MakeCardClickable(card, () => viewMore.PerformClick());
            statusLabel = status;
            bodyLabel = body;
            return card;
        }

        private Panel BuildInsightRosterCard(string title, string hint, Color accent, out PayrollInsightCardBindings bindings)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 238);
            card.Margin = new Padding(0, 0, 12, 12);
            card.Padding = new Padding(18, 16, 18, 16);

            Panel accentBar = new Panel { Location = new Point(18, 18), Size = new Size(6, 44), BackColor = accent };
            DS.Rounded(accentBar, 3);

            Label titleLabel = new Label
            {
                Text = title,
                Location = new Point(34, 16),
                Size = new Size(280, 22),
                Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            Label hintLabel = new Label
            {
                Text = hint,
                Location = new Point(34, 42),
                Size = new Size(420, 34),
                Font = DS.Small,
                ForeColor = DS.Slate500
            };

            Label statusLabel = new Label
            {
                Text = "Checking",
                Location = new Point(18, 86),
                Size = new Size(320, 22),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = accent
            };

            FlowLayoutPanel badgeFlow = new FlowLayoutPanel
            {
                Location = new Point(18, 116),
                Size = new Size(420, 34),
                WrapContents = true,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            Label[] badgeLabels = new Label[3];
            for (int i = 0; i < badgeLabels.Length; i++)
            {
                Label badge = new Label
                {
                    Text = "Waiting",
                    AutoSize = true,
                    MinimumSize = new Size(0, 24),
                    Margin = new Padding(0, 0, 8, 8),
                    Padding = new Padding(10, 5, 10, 5),
                    Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                    ForeColor = DS.Slate700,
                    BackColor = DS.Slate100
                };
                DS.Rounded(badge, 12);
                badgeFlow.Controls.Add(badge);
                badgeLabels[i] = badge;
            }

            Panel gridPanel = new Panel
            {
                Location = new Point(18, 158),
                Size = new Size(440, 98),
                BackColor = Color.White
            };

            Label employeeHeader = new Label { Text = "Employee", Location = new Point(0, 0), Size = new Size(160, 16), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate500 };
            Label reasonHeader = new Label { Text = "Reason", Location = new Point(166, 0), Size = new Size(120, 16), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate500 };
            Label impactHeader = new Label { Text = "Context", Location = new Point(292, 0), Size = new Size(130, 16), Font = new Font("Segoe UI", 7.8f, FontStyle.Bold), ForeColor = DS.Slate500 };
            gridPanel.Controls.AddRange(new Control[] { employeeHeader, reasonHeader, impactHeader });

            PayrollInsightRowBindings[] rows = new PayrollInsightRowBindings[3];
            for (int i = 0; i < rows.Length; i++)
            {
                Panel rowPanel = new Panel
                {
                    Location = new Point(0, 22 + (i * 24)),
                    Size = new Size(440, 22),
                    BackColor = i % 2 == 0 ? Color.White : DS.Slate50
                };
                Label employeeLabel = new Label { Location = new Point(0, 3), Size = new Size(160, 16), Font = DS.Small, ForeColor = DS.Slate800, AutoEllipsis = true };
                Label reasonLabel = new Label { Location = new Point(166, 2), Size = new Size(120, 18), Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
                Label contextLabel = new Label { Location = new Point(292, 3), Size = new Size(142, 16), Font = DS.Small, ForeColor = DS.Slate600, AutoEllipsis = true };
                rowPanel.Controls.AddRange(new Control[] { employeeLabel, reasonLabel, contextLabel });
                gridPanel.Controls.Add(rowPanel);
                rows[i] = new PayrollInsightRowBindings { RowPanel = rowPanel, EmployeeLabel = employeeLabel, ReasonLabel = reasonLabel, ContextLabel = contextLabel };
            }

            Label emptyStateLabel = new Label
            {
                Text = "Waiting for payroll data...",
                Location = new Point(0, 28),
                Size = new Size(420, 40),
                Font = DS.Small,
                ForeColor = DS.Slate500,
                Visible = false
            };
            gridPanel.Controls.Add(emptyStateLabel);

            Button viewMore = new Button
            {
                Text = "View more",
                Size = new Size(88, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = accent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            viewMore.FlatAppearance.BorderSize = 1;
            viewMore.FlatAppearance.BorderColor = DS.Lighten(accent, 0.5f);
            viewMore.FlatAppearance.MouseOverBackColor = DS.Lighten(accent, 0.92f);
            viewMore.FlatAppearance.MouseDownBackColor = DS.Lighten(accent, 0.88f);
            DS.Rounded(viewMore, 8);
            viewMore.Click += (s, e) => ShowRosterDetailDialog(title, statusLabel.Text, Convert.ToString(viewMore.Tag) ?? emptyStateLabel.Text, accent);

            card.Resize += (s, e) =>
            {
                int innerWidth = Math.Max(200, card.ClientSize.Width - 36);
                int gridWidth = Math.Max(260, innerWidth);
                titleLabel.Width = Math.Max(120, innerWidth - 16);
                hintLabel.Width = Math.Max(180, innerWidth - 16);
                hintLabel.Height = Math.Max(34, TextRenderer.MeasureText(hintLabel.Text ?? string.Empty, hintLabel.Font, new Size(hintLabel.Width, 0), TextFormatFlags.WordBreak).Height);
                statusLabel.Top = hintLabel.Bottom + 10;
                statusLabel.Width = Math.Max(160, innerWidth - 110);
                badgeFlow.Top = statusLabel.Bottom + 8;
                badgeFlow.Width = gridWidth;
                badgeFlow.Height = 34;
                gridPanel.Top = badgeFlow.Bottom + 8;
                gridPanel.Width = gridWidth;
                employeeHeader.Width = Math.Max(110, (int)(gridWidth * 0.37f));
                reasonHeader.Left = employeeHeader.Right + 8;
                reasonHeader.Width = Math.Max(92, (int)(gridWidth * 0.27f));
                impactHeader.Left = reasonHeader.Right + 8;
                impactHeader.Width = Math.Max(90, gridWidth - impactHeader.Left - 6);
                foreach (PayrollInsightRowBindings row in rows)
                {
                    row.RowPanel.Width = gridWidth;
                    row.EmployeeLabel.Width = employeeHeader.Width;
                    row.ReasonLabel.Left = reasonHeader.Left;
                    row.ReasonLabel.Width = reasonHeader.Width;
                    row.ContextLabel.Left = impactHeader.Left;
                    row.ContextLabel.Width = impactHeader.Width;
                }
                emptyStateLabel.Width = gridWidth - 8;
                viewMore.Location = new Point(Math.Max(18, card.ClientSize.Width - viewMore.Width - 18), gridPanel.Bottom + 10);
                card.Height = Math.Max(card.MinimumSize.Height, viewMore.Bottom + 16);
            };

            card.Controls.AddRange(new Control[] { accentBar, titleLabel, hintLabel, statusLabel, badgeFlow, gridPanel, viewMore });
            MakeCardClickable(card, () => viewMore.PerformClick());
            bindings = new PayrollInsightCardBindings
            {
                StatusLabel = statusLabel,
                EmptyStateLabel = emptyStateLabel,
                BadgeLabels = badgeLabels,
                Rows = rows,
                ViewMoreButton = viewMore
            };
            return card;
        }

        private Panel BuildCompactInsightQueueCard(string title, string hint, Color accent, out PayrollInsightCardBindings bindings)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 118);
            card.Margin = new Padding(0, 0, 10, 10);
            card.Padding = new Padding(14, 12, 14, 12);

            Panel accentBar = new Panel { Location = new Point(14, 14), Size = new Size(4, 54), BackColor = accent };
            DS.Rounded(accentBar, 2);
            Label titleLabel = new Label { Text = title, Location = new Point(28, 12), Size = new Size(220, 18), Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            Label hintLabel = new Label { Text = hint, Location = new Point(28, 32), Size = new Size(220, 18), Font = DS.Small, ForeColor = DS.Slate500, AutoEllipsis = true };
            Label statusLabel = new Label { Text = "Checking", Location = new Point(28, 61), Size = new Size(150, 24), Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = accent, AutoEllipsis = true };
            Label emptyStateLabel = new Label { Text = "Waiting for payroll data...", Visible = false };

            Label[] badgeLabels = new Label[3];
            FlowLayoutPanel badges = new FlowLayoutPanel
            {
                Location = new Point(28, 88),
                Size = new Size(180, 24),
                WrapContents = false,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            for (int i = 0; i < badgeLabels.Length; i++)
            {
                Label badge = new Label
                {
                    Text = "0",
                    AutoSize = true,
                    MinimumSize = new Size(0, 20),
                    Margin = new Padding(0, 0, 6, 0),
                    Padding = new Padding(8, 3, 8, 3),
                    Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
                    ForeColor = DS.Slate700,
                    BackColor = DS.Slate100,
                    Visible = false
                };
                DS.Rounded(badge, 10);
                badges.Controls.Add(badge);
                badgeLabels[i] = badge;
            }

            Button viewMore = new Button
            {
                Text = "View details",
                Size = new Size(96, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            viewMore.FlatAppearance.BorderSize = 1;
            viewMore.FlatAppearance.BorderColor = DS.BorderStrong;
            viewMore.FlatAppearance.MouseOverBackColor = DS.Slate50;
            DS.Rounded(viewMore, 6);
            viewMore.Click += (s, e) => ShowRosterDetailDialog(title, statusLabel.Text, Convert.ToString(viewMore.Tag) ?? emptyStateLabel.Text, accent);

            PayrollInsightRowBindings[] rows = new PayrollInsightRowBindings[0];
            card.Resize += (s, e) =>
            {
                int innerWidth = Math.Max(120, card.ClientSize.Width - 42);
                titleLabel.Width = innerWidth;
                hintLabel.Width = innerWidth;
                statusLabel.Width = Math.Max(100, innerWidth - viewMore.Width - 8);
                badges.Width = innerWidth;
                viewMore.Location = new Point(Math.Max(28, card.ClientSize.Width - viewMore.Width - 14), 82);
                card.Height = Math.Max(card.MinimumSize.Height, 120);
            };

            card.Controls.AddRange(new Control[] { accentBar, titleLabel, hintLabel, statusLabel, badges, viewMore });
            MakeCardClickable(card, () => viewMore.PerformClick());
            bindings = new PayrollInsightCardBindings
            {
                StatusLabel = statusLabel,
                EmptyStateLabel = emptyStateLabel,
                BadgeLabels = badgeLabels,
                Rows = rows,
                ViewMoreButton = viewMore
            };
            return card;
        }

        private Panel BuildQualityPulseCard()
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 112);
            card.Margin = new Padding(0, 0, 10, 10);
            card.Padding = new Padding(14, 12, 14, 12);

            Label title = new Label { Text = "Payroll Quality Pulse", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), ForeColor = DS.Slate900 };
            Label hint = new Label { Text = "Punctuality, absence, leave, and overtime pressure.", Dock = DockStyle.Top, Height = 18, Font = DS.Small, ForeColor = DS.Slate500 };
            _qualityPulseBar = new PayrollQualityPulseBar { Dock = DockStyle.Top, Height = 22, Margin = new Padding(0, 8, 0, 0), BackColor = DS.Slate100 };
            FlowLayoutPanel legend = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 24, Padding = new Padding(0, 8, 0, 0), WrapContents = false };
            legend.Controls.Add(BuildPulseLegend("Punctuality", DS.Green600));
            legend.Controls.Add(BuildPulseLegend("Absence", DS.Red500));
            legend.Controls.Add(BuildPulseLegend("Leave", DS.Slate400));
            legend.Controls.Add(BuildPulseLegend("Overtime", Color.FromArgb(148, 163, 184)));

            card.Controls.Add(legend);
            card.Controls.Add(_qualityPulseBar);
            card.Controls.Add(hint);
            card.Controls.Add(title);
            MakeCardClickable(card, () => ShowDashboardCardReport("Payroll Quality Pulse", title, hint, DS.Primary600));
            return card;
        }

        private void SelectPayrollTab(int index)
        {
            if (_tabs == null || index < 0 || index >= _tabs.TabPages.Count)
                return;

            _tabs.SelectedIndex = index;
        }

        private void ShowDashboardCardReport(string title, Label valueLabel, Label detailLabel, Color accent)
        {
            string value = valueLabel == null ? "No value available." : valueLabel.Text;
            string detail = detailLabel == null ? string.Empty : detailLabel.Text;
            string report = "Period: " + new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM yyyy")
                + Environment.NewLine + Environment.NewLine
                + value
                + Environment.NewLine + Environment.NewLine
                + detail;
            ShowRosterDetailDialog(title, "Payroll report", report, accent);
        }

        private static void MakeCardClickable(Control card, Action action)
        {
            if (card == null || action == null)
                return;

            EventHandler handler = (s, e) => action();
            ApplyCardClickBehavior(card, handler);
        }

        private static void ApplyCardClickBehavior(Control control, EventHandler handler)
        {
            if (control == null || handler == null || control is Button)
                return;

            control.Cursor = Cursors.Hand;
            control.Click += handler;
            foreach (Control child in control.Controls)
                ApplyCardClickBehavior(child, handler);
        }

        private static Control BuildPulseLegend(string text, Color color)
        {
            Panel item = new Panel { Width = 82, Height = 18, Margin = new Padding(0, 0, 6, 0), BackColor = Color.White };
            Panel dot = new Panel { Location = new Point(0, 6), Size = new Size(7, 7), BackColor = color };
            DS.Rounded(dot, 4);
            Label label = new Label { Text = text, Location = new Point(11, 1), Size = new Size(68, 16), Font = new Font("Segoe UI", 7.6f), ForeColor = DS.Slate600, AutoEllipsis = true };
            item.Controls.Add(label);
            item.Controls.Add(dot);
            return item;
        }

        private Chart BuildCompliancePulseChart(out Chart chart)
        {
            chart = CreateDashboardChart();
            var series = new Series("Compliance")
            {
                ChartType = SeriesChartType.Column,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            series.Points.Add(new DataPoint { AxisLabel = "Punctuality", YValues = new[] { 0d }, Color = DS.Green600 });
            series.Points.Add(new DataPoint { AxisLabel = "Absence", YValues = new[] { 0d }, Color = DS.Red500 });
            series.Points.Add(new DataPoint { AxisLabel = "Leave", YValues = new[] { 0d }, Color = DS.Amber500 });
            series.Points.Add(new DataPoint { AxisLabel = "Overtime", YValues = new[] { 0d }, Color = Color.FromArgb(124, 58, 237) });
            chart.Series.Add(series);
            chart.ChartAreas[0].AxisY.Maximum = 100;
            return chart;
        }

        private Chart BuildPayrollBlockersChart()
        {
            Chart chart = CreateDashboardChart();
            var series = new Series("Blockers")
            {
                ChartType = SeriesChartType.Doughnut,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                LabelForeColor = DS.Slate700
            };
            series["PieLabelStyle"] = "Disabled";
            series.Points.Add(new DataPoint { AxisLabel = "Attendance", YValues = new[] { 1d }, Color = DS.Amber500 });
            series.Points.Add(new DataPoint { AxisLabel = "Salary", YValues = new[] { 1d }, Color = DS.Red500 });
            series.Points.Add(new DataPoint { AxisLabel = "Statutory", YValues = new[] { 1d }, Color = Color.FromArgb(124, 58, 237) });
            series.Points.Add(new DataPoint { AxisLabel = "Payslips", YValues = new[] { 1d }, Color = DS.Primary600 });
            chart.Series.Add(series);
            chart.Legends[0].Enabled = true;
            chart.Legends[0].Docking = Docking.Bottom;
            return chart;
        }

        private Chart BuildFieldCapacityChart(out Chart chart)
        {
            chart = CreateDashboardChart();
            var series = new Series("Capacity")
            {
                ChartType = SeriesChartType.Pie,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                LabelForeColor = DS.Slate700
            };
            series["PieLabelStyle"] = "Disabled";
            series.Points.Add(new DataPoint { AxisLabel = "Deployed", YValues = new[] { 1d }, Color = DS.Primary600 });
            series.Points.Add(new DataPoint { AxisLabel = "Available", YValues = new[] { 1d }, Color = DS.Green600 });
            series.Points.Add(new DataPoint { AxisLabel = "On Leave", YValues = new[] { 1d }, Color = DS.Amber500 });
            series.Points.Add(new DataPoint { AxisLabel = "No Attendance", YValues = new[] { 1d }, Color = DS.Slate300 });
            chart.Series.Add(series);
            chart.Legends[0].Enabled = true;
            chart.Legends[0].Docking = Docking.Bottom;
            return chart;
        }

        private Chart BuildCoverageLoadChart()
        {
            Chart chart = CreateDashboardChart();
            var series = new Series("Coverage")
            {
                ChartType = SeriesChartType.Bar,
                IsValueShownAsLabel = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            series.Points.Add(new DataPoint { AxisLabel = "Attendance Coverage", YValues = new[] { 0d }, Color = DS.Green600 });
            series.Points.Add(new DataPoint { AxisLabel = "Deployment", YValues = new[] { 0d }, Color = DS.Primary600 });
            series.Points.Add(new DataPoint { AxisLabel = "Leave Risk", YValues = new[] { 0d }, Color = DS.Red500 });
            chart.Series.Add(series);
            chart.ChartAreas[0].AxisX.Maximum = 100;
            return chart;
        }

        private Chart CreateDashboardChart()
        {
            Chart chart = new SafeDashboardChart
            {
                BackColor = Color.White,
                Palette = ChartColorPalette.None,
                Margin = new Padding(0, 8, 0, 0),
                MinimumSize = new Size(1, 1)
            };

            ChartArea area = new ChartArea("Main");
            area.BackColor = Color.White;
            area.AxisX.LineColor = DS.Border;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.LabelStyle.ForeColor = DS.Slate600;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisY.LineColor = DS.Border;
            area.AxisY.MajorGrid.LineColor = DS.Slate100;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            area.AxisY.LabelStyle.ForeColor = DS.Slate500;
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8f);
            area.AxisY.Minimum = 0;
            chart.ChartAreas.Add(area);

            Legend legend = new Legend("Legend")
            {
                BackColor = Color.White,
                ForeColor = DS.Slate600,
                Font = new Font("Segoe UI", 8f),
                Enabled = false
            };
            chart.Legends.Add(legend);
            return chart;
        }

        private sealed class SafeDashboardChart : Chart
        {
            protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
            {
                base.SetBoundsCore(x, y, Math.Max(1, width), Math.Max(1, height), specified);
            }
        }

        private sealed class PayrollQualityPulseBar : Panel
        {
            private decimal _punctuality;
            private decimal _absence;
            private decimal _leave;
            private decimal _overtime;

            public void SetValues(decimal punctuality, decimal absence, decimal leave, decimal overtime)
            {
                _punctuality = ClampPercent(punctuality);
                _absence = ClampPercent(absence);
                _leave = ClampPercent(leave);
                _overtime = ClampPercent(overtime);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 2, Math.Max(1, Width - 1), Math.Max(1, Height - 5));
                using (Brush background = new SolidBrush(DS.Slate100))
                    e.Graphics.FillRectangle(background, bounds);

                decimal riskTotal = Math.Max(0m, _absence + _leave + _overtime);
                decimal punctualityWidth = Math.Max(8m, Math.Min(100m, _punctuality));
                decimal remaining = Math.Max(0m, 100m - punctualityWidth);
                decimal absenceWidth = riskTotal <= 0m ? 0m : remaining * (_absence / riskTotal);
                decimal leaveWidth = riskTotal <= 0m ? 0m : remaining * (_leave / riskTotal);
                decimal overtimeWidth = Math.Max(0m, remaining - absenceWidth - leaveWidth);

                int left = bounds.Left;
                DrawPulseSegment(e.Graphics, ref left, bounds, punctualityWidth, DS.Green600, FormatPercent(_punctuality));
                DrawPulseSegment(e.Graphics, ref left, bounds, absenceWidth, DS.Red500, FormatPercent(_absence));
                DrawPulseSegment(e.Graphics, ref left, bounds, leaveWidth, DS.Slate400, FormatPercent(_leave));
                DrawPulseSegment(e.Graphics, ref left, bounds, overtimeWidth, Color.FromArgb(148, 163, 184), FormatPercent(_overtime));
            }

            private static void DrawPulseSegment(Graphics graphics, ref int left, Rectangle bounds, decimal percent, Color color, string label)
            {
                int width = (int)Math.Round(bounds.Width * (double)(percent / 100m));
                if (width <= 0)
                    return;

                Rectangle segment = new Rectangle(left, bounds.Top, Math.Min(width, bounds.Right - left), bounds.Height);
                using (Brush brush = new SolidBrush(color))
                    graphics.FillRectangle(brush, segment);
                if (segment.Width >= 42)
                    TextRenderer.DrawText(graphics, label, new Font("Segoe UI", 7.5f, FontStyle.Bold), segment, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                left += segment.Width;
            }

            private static decimal ClampPercent(decimal value)
            {
                if (value < 0m)
                    return 0m;
                if (value > 100m)
                    return 100m;
                return value;
            }
        }

        private void UpdateDashboardCharts(PayrollDashboardMetrics metrics)
        {
            UpdateCompliancePulseChart(metrics);
            UpdatePayrollBlockersChart(metrics);
        }

        private void UpdateCompliancePulseChart(PayrollDashboardMetrics metrics)
        {
            if (_qualityPulseBar != null)
            {
                metrics = metrics ?? new PayrollDashboardMetrics();
                _qualityPulseBar.SetValues(
                    metrics.PunctualityRate,
                    Math.Min(100m, metrics.AbsenteeismRate),
                    Math.Min(100m, metrics.LeaveDays * 6m),
                    Math.Min(100m, metrics.MonthlyOvertimeHours * 3m));
            }

        }

        private void UpdatePayrollBlockersChart(PayrollDashboardMetrics metrics)
        {
            Chart chart = FindDashboardChartBySeriesName("Blockers");
            if (chart == null || chart.Series.Count == 0)
                return;

            Series series = chart.Series[0];
            SetPiePoint(series.Points[0], Math.Max(0, metrics.AttendanceExceptions), "Attendance");
            SetPiePoint(series.Points[1], Math.Max(0, metrics.SalarySetupGaps), "Salary");
            SetPiePoint(series.Points[2], Math.Max(0, metrics.PendingStatutoryCount), "Statutory");
            SetPiePoint(series.Points[3], Math.Max(0, metrics.PayslipsPending), "Payslips");
        }

        private void UpdateCoverageLoadChart(PayrollDashboardMetrics metrics)
        {
            Chart chart = FindDashboardChartBySeriesName("Coverage");
            if (chart == null || chart.Series.Count == 0)
                return;

            Series series = chart.Series[0];
            UpdateChartPoint(series.Points[0], (double)metrics.AttendanceCoverageRate, FormatPercent(metrics.AttendanceCoverageRate));
            UpdateChartPoint(series.Points[1], (double)metrics.DeploymentUtilizationRate, FormatPercent(metrics.DeploymentUtilizationRate));
            decimal leaveRisk = metrics.ActiveEmployees == 0 ? 0m : Math.Min(100m, (metrics.UpcomingLeaveEmployees * 100m) / metrics.ActiveEmployees);
            UpdateChartPoint(series.Points[2], (double)leaveRisk, metrics.UpcomingLeaveEmployees + " emp");
        }

        private Chart FindDashboardChartBySeriesName(string seriesName)
        {
            foreach (Control control in Controls)
            {
                Chart chart = FindChartRecursive(control, seriesName);
                if (chart != null)
                    return chart;
            }
            return null;
        }

        private static Chart FindChartRecursive(Control root, string seriesName)
        {
            Chart chart = root as Chart;
            if (chart != null && chart.Series.OfType<Series>().Any(series => string.Equals(series.Name, seriesName, StringComparison.OrdinalIgnoreCase)))
                return chart;

            foreach (Control child in root.Controls)
            {
                Chart match = FindChartRecursive(child, seriesName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static void UpdateChartPoint(DataPoint point, double value, string label)
        {
            point.YValues = new[] { Math.Max(0d, value) };
            point.Label = label;
        }

        private static void SetPiePoint(DataPoint point, int value, string axisLabel)
        {
            point.AxisLabel = axisLabel;
            point.YValues = new[] { (double)Math.Max(0, value == 0 ? 1 : value) };
            point.LegendText = axisLabel + " (" + value + ")";
            point.Label = string.Empty;
        }

        private static void UpdateRosterCard(Label statusLabel, Label bodyLabel, string status, string body, string fullBody = null)
        {
            if (statusLabel == null || bodyLabel == null)
                return;

            statusLabel.Text = status;
            bodyLabel.Text = body;
            bodyLabel.Tag = string.IsNullOrWhiteSpace(fullBody) ? body : fullBody;
        }

        private static void ApplyInsightCard(PayrollInsightCardBindings bindings, PayrollInsightCardState state)
        {
            if (bindings == null)
                return;

            state = state ?? new PayrollInsightCardState();
            bindings.StatusLabel.Text = string.IsNullOrWhiteSpace(state.Status) ? "Checking" : state.Status;

            List<PayrollInsightBadge> badges = state.Badges ?? new List<PayrollInsightBadge>();
            for (int i = 0; i < bindings.BadgeLabels.Length; i++)
            {
                Label badgeLabel = bindings.BadgeLabels[i];
                PayrollInsightBadge badge = i < badges.Count ? badges[i] : null;
                if (badge == null)
                {
                    badgeLabel.Visible = false;
                    continue;
                }

                badgeLabel.Visible = true;
                badgeLabel.Text = badge.Label + " " + badge.Count;
                badgeLabel.ForeColor = badge.Accent;
                badgeLabel.BackColor = DS.Lighten(badge.Accent, 0.9f);
            }

            List<PayrollInsightRow> rows = state.Rows ?? new List<PayrollInsightRow>();
            bool hasRows = rows.Count > 0;
            bindings.EmptyStateLabel.Text = string.IsNullOrWhiteSpace(state.EmptyState) ? "No current items." : state.EmptyState;
            bindings.EmptyStateLabel.Visible = !hasRows;
            foreach (PayrollInsightRowBindings rowBinding in bindings.Rows)
                rowBinding.RowPanel.Visible = false;

            for (int i = 0; i < bindings.Rows.Length; i++)
            {
                if (i >= rows.Count)
                    continue;

                PayrollInsightRow row = rows[i];
                PayrollInsightRowBindings rowBinding = bindings.Rows[i];
                rowBinding.RowPanel.Visible = true;
                rowBinding.EmployeeLabel.Text = row.Employee;
                rowBinding.ReasonLabel.Text = row.Reason;
                rowBinding.ContextLabel.Text = row.Context;
            }

            bindings.ViewMoreButton.Tag = string.IsNullOrWhiteSpace(state.FullDetail) ? bindings.EmptyStateLabel.Text : state.FullDetail;
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

        private void ConfigureModernGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = DS.Border;
            grid.EnableHeadersVisualStyles = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = true;
            grid.MultiSelect = false;
            grid.ReadOnly = true;
            grid.ColumnHeadersDefaultCellStyle.BackColor = DS.Slate50;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate900;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.ColumnHeadersHeight = 38;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5f);
            grid.DefaultCellStyle.ForeColor = DS.Slate700;
            grid.DefaultCellStyle.SelectionBackColor = DS.Primary50;
            grid.DefaultCellStyle.SelectionForeColor = DS.Slate900;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 255);
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.RowTemplate.Height = 38;
        }

        private void PolishPayrollTable(DataGridView grid, string actionColumnName, IEnumerable<string> moneyColumns)
        {
            if (grid == null)
                return;

            ConfigureModernGrid(grid);
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                column.MinimumWidth = 84;
            }

            foreach (string columnName in moneyColumns ?? Enumerable.Empty<string>())
            {
                if (!grid.Columns.Contains(columnName))
                    continue;
                grid.Columns[columnName].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid.Columns[columnName].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (!string.IsNullOrWhiteSpace(actionColumnName) && grid.Columns.Contains(actionColumnName))
            {
                DataGridViewColumn action = grid.Columns[actionColumnName];
                action.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                action.Width = 132;
                action.MinimumWidth = 112;
                action.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                action.DefaultCellStyle.ForeColor = DS.Primary600;
                action.DefaultCellStyle.Font = new Font("Segoe UI", 8.4f, FontStyle.Bold);
            }
        }

        private void ConfigureEmployeeList(ListBox listBox)
        {
            listBox.Dock = DockStyle.Fill;
            listBox.BorderStyle = BorderStyle.None;
            listBox.BackColor = Color.White;
            listBox.Font = new Font("Segoe UI", 8.75f);
            listBox.DrawMode = DrawMode.OwnerDrawFixed;
            listBox.ItemHeight = 50;
            listBox.IntegralHeight = false;
            listBox.DrawItem += DrawEmployeeListItem;
        }

        private void DrawEmployeeListItem(object sender, DrawItemEventArgs e)
        {
            ListBox list = sender as ListBox;
            if (list == null || e.Index < 0 || e.Index >= list.Items.Count)
                return;

            Employee employee = list.Items[e.Index] as Employee;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle bounds = e.Bounds;
            Color back = selected ? Color.FromArgb(232, 241, 255) : Color.White;
            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, bounds);
            using (Pen pen = new Pen(DS.Border))
                e.Graphics.DrawLine(pen, bounds.Left + 10, bounds.Bottom - 1, bounds.Right - 8, bounds.Bottom - 1);
            if (selected)
            {
                using (Pen pen = new Pen(Color.FromArgb(147, 197, 253), 1))
                    e.Graphics.DrawRectangle(pen, bounds.Left + 4, bounds.Top + 4, bounds.Width - 10, bounds.Height - 9);
            }

            string initials = GetEmployeeInitials(employee);
            Color avatarColor = EmployeeAvatarColor(employee == null ? e.Index : employee.EmployeeID);
            Rectangle avatar = new Rectangle(bounds.Left + 14, bounds.Top + 11, 28, 28);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(avatarColor))
                e.Graphics.FillEllipse(brush, avatar);
            TextRenderer.DrawText(e.Graphics, initials, new Font("Segoe UI", 7.7f, FontStyle.Bold), avatar, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            string name = employee == null ? "Employee" : (employee.Name ?? "Employee");
            string code = employee == null ? string.Empty : (employee.EmployeeCode ?? string.Empty);
            TextRenderer.DrawText(e.Graphics, name, new Font("Segoe UI", 8.6f, FontStyle.Bold), new Rectangle(bounds.Left + 52, bounds.Top + 9, bounds.Width - 96, 18), DS.Slate900, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, code, new Font("Segoe UI", 7.8f), new Rectangle(bounds.Left + 52, bounds.Top + 29, bounds.Width - 96, 16), DS.Slate600, TextFormatFlags.EndEllipsis);

            Label icon = null;
            string glyph = selected ? "✓" : "›";
            Color glyphColor = selected ? DS.Primary600 : DS.Slate600;
            TextRenderer.DrawText(e.Graphics, glyph, new Font("Segoe UI", selected ? 10f : 15f, FontStyle.Bold), new Rectangle(bounds.Right - 34, bounds.Top + 14, 20, 22), glyphColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (icon != null)
                icon.Dispose();
        }

        private DateTimePicker AddModernDateField(TableLayoutPanel parent, string label, int labelColumn, int row)
        {
            Label title = NewFieldLabel(label);
            DateTimePicker picker = new DateTimePicker { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.75f), Format = DateTimePickerFormat.Short, Margin = new Padding(0, 2, 12, 4) };
            parent.Controls.Add(title, labelColumn, row);
            parent.Controls.Add(picker, labelColumn + 1, row);
            return picker;
        }

        private NumericUpDown AddModernAmountField(TableLayoutPanel parent, string label, int labelColumn, int row)
        {
            Label title = NewFieldLabel(label);
            NumericUpDown input = new NumericUpDown { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.75f), DecimalPlaces = 2, Maximum = 10000000, ThousandsSeparator = true, Margin = new Padding(0, 2, 12, 4) };
            parent.Controls.Add(title, labelColumn, row);
            parent.Controls.Add(input, labelColumn + 1, row);
            return input;
        }

        private Label NewFieldLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, Font = DS.SmallBold, ForeColor = DS.Slate700, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 2, 10, 4) };
        }

        private DataGridView NewSalaryComponentGrid()
        {
            _gridSalaryComponents = NewGrid();
            ConfigureModernGrid(_gridSalaryComponents);
            _gridSalaryComponents.Columns.Add("Component", "Component");
            _gridSalaryComponents.Columns.Add("Type", "Earnings / Deductions");
            _gridSalaryComponents.Columns.Add("Calc", "Calculation Type");
            _gridSalaryComponents.Columns.Add("Amount", "Amount (₹)");
            return _gridSalaryComponents;
        }

        private Label LucideIcon(string fileName, ModernIconKind fallback, int size, Color color)
        {
            Label label = ModernIconSystem.Icon(fallback, size, color);
            label.Image = LucideIconService.GetIcon(fileName, size, color);
            label.Text = string.Empty;
            return label;
        }

        private Button NewIconButton(string fileName, ModernIconKind fallback, string tooltip)
        {
            Button button = new Button { Size = new Size(34, 34), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = DS.BorderStrong;
            button.Image = LucideIconService.GetIcon(fileName, 16, DS.Slate700);
            new ToolTip().SetToolTip(button, tooltip);
            DS.Rounded(button, 7);
            return button;
        }

        private static void PaintBorder(Control control, Graphics graphics, Color color, int radius)
        {
            using (Pen pen = new Pen(color))
                graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        }

        private static string GetEmployeeInitials(Employee employee)
        {
            string name = employee == null ? string.Empty : (employee.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return "NA";
            string[] parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }

        private static Color EmployeeAvatarColor(int seed)
        {
            Color[] colors =
            {
                DS.Primary600,
                Color.FromArgb(5, 150, 105),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(30, 64, 175),
                Color.FromArgb(124, 58, 237)
            };
            return colors[Math.Abs(seed) % colors.Length];
        }

        private sealed class PayrollDashboardMetrics
        {
            public int ActiveEmployees { get; set; }
            public int EmployeesIncluded { get; set; }
            public int AttendanceExceptions { get; set; }
            public int SalarySetupGaps { get; set; }
            public int PendingStatutoryCount { get; set; }
            public decimal PendingStatutoryAmount { get; set; }
            public int PayslipsPending { get; set; }
            public decimal RecoveriesThisMonth { get; set; }
            public DateTime? NextStatutoryDueDate { get; set; }
            public decimal PunctualityRate { get; set; }
            public decimal AbsenteeismRate { get; set; }
            public decimal LeaveDays { get; set; }
            public decimal MonthlyOvertimeHours { get; set; }
            public int CheckedInToday { get; set; }
            public int OnLeaveToday { get; set; }
            public int AvailableForCallout { get; set; }
            public int ActiveFieldJobs { get; set; }
            public decimal DeploymentUtilizationRate { get; set; }
            public int UpcomingLeaveDays { get; set; }
            public int UpcomingLeaveEmployees { get; set; }
            public int UnderutilizedEmployees { get; set; }
            public int OverworkedEmployees { get; set; }
            public int GhostCheckoutAlerts { get; set; }
            public decimal AttendanceCoverageRate { get; set; }
            public bool UsesLiveAttendance { get; set; }
            public int EmployeesWithAttendance { get; set; }
            public int LateEmployees { get; set; }
            public int AbsentEmployees { get; set; }
            public string AttendanceExceptionPeople { get; set; }
            public string AttendanceExceptionRoster { get; set; }
            public string AttendanceExceptionRosterFull { get; set; }
            public string SalaryGapPeople { get; set; }
            public string SalaryGapRoster { get; set; }
            public string SalaryGapRosterFull { get; set; }
            public string PendingStatutoryItems { get; set; }
            public string PayslipPendingPeople { get; set; }
            public string PayslipPendingRoster { get; set; }
            public string PayslipPendingRosterFull { get; set; }
            public string LatePeople { get; set; }
            public string LateRoster { get; set; }
            public string LateRosterFull { get; set; }
            public string AbsentPeople { get; set; }
            public string AbsentRoster { get; set; }
            public string AbsentRosterFull { get; set; }
            public string OnLeaveTodayPeople { get; set; }
            public string OnLeaveTodayRoster { get; set; }
            public string AvailableNowPeople { get; set; }
            public string AvailableNowRoster { get; set; }
            public string FieldQueuePeople { get; set; }
            public string FieldQueueRoster { get; set; }
            public string WorkPatternPeople { get; set; }
            public PayrollInsightCardState AttendanceInsight { get; set; }
            public PayrollInsightCardState SalaryInsight { get; set; }
            public PayrollInsightCardState LateAbsentInsight { get; set; }
            public PayrollInsightCardState PayslipInsight { get; set; }
        }

        private sealed class PayrollInsightCardState
        {
            public string Status { get; set; }
            public string EmptyState { get; set; }
            public string FullDetail { get; set; }
            public List<PayrollInsightBadge> Badges { get; set; } = new List<PayrollInsightBadge>();
            public List<PayrollInsightRow> Rows { get; set; } = new List<PayrollInsightRow>();
        }

        private sealed class PayrollInsightBadge
        {
            public PayrollInsightBadge(string label, int count, Color accent)
            {
                Label = label;
                Count = count;
                Accent = accent;
            }

            public string Label { get; private set; }
            public int Count { get; private set; }
            public Color Accent { get; private set; }
        }

        private sealed class PayrollInsightRow
        {
            public string Employee { get; set; }
            public string Reason { get; set; }
            public string Context { get; set; }
        }

        private sealed class PayrollExceptionDialogRow
        {
            public string Employee { get; set; }
            public string Reason { get; set; }
            public string Context { get; set; }
        }

        private sealed class PayrollInsightCardBindings
        {
            public Label StatusLabel { get; set; }
            public Label EmptyStateLabel { get; set; }
            public Label[] BadgeLabels { get; set; }
            public PayrollInsightRowBindings[] Rows { get; set; }
            public Button ViewMoreButton { get; set; }
        }

        private sealed class PayrollInsightRowBindings
        {
            public Panel RowPanel { get; set; }
            public Label EmployeeLabel { get; set; }
            public Label ReasonLabel { get; set; }
            public Label ContextLabel { get; set; }
        }

        private Panel MakePayrollKpi(string title, string value, string subtitle, Color accent, out Label valueLabel)
        {
            Panel card = MakePayrollCard();
            card.Dock = DockStyle.Fill;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.MinimumSize = new Size(0, 96);
            card.Margin = new Padding(0, 0, 12, 0);
            card.Padding = new Padding(18, 14, 18, 14);

            Panel icon = new Panel { Location = new Point(18, 18), Size = new Size(42, 42), BackColor = DS.Lighten(accent, 0.82f) };
            DS.Rounded(icon, 10);
            icon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Font font = new Font("Segoe UI", 14f, FontStyle.Bold))
                using (Brush brush = new SolidBrush(accent))
                    e.Graphics.DrawString("•", font, brush, new PointF(13, 8));
            };

            Label titleLabel = new Label { Text = title, Location = new Point(74, 16), Size = new Size(160, 18), Font = DS.Small, ForeColor = DS.Slate600, AutoEllipsis = true };
            Label metricValue = new Label { Text = value, Location = new Point(74, 40), Size = new Size(180, 30), Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true };
            valueLabel = metricValue;
            Label subLabel = new Label { Text = subtitle, Location = new Point(74, 74), Size = new Size(180, 20), Font = DS.Small, ForeColor = DS.Slate500, AutoEllipsis = true };
            card.Resize += (s, e) =>
            {
                int textWidth = Math.Max(80, card.ClientSize.Width - 82);
                titleLabel.Width = textWidth;
                metricValue.Width = textWidth;
                subLabel.Width = textWidth;
                card.Height = Math.Max(card.MinimumSize.Height, subLabel.Bottom + 14);
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

        private void SetWorkflowStep(Label label, string text, Color color)
        {
            if (label == null)
                return;

            label.Text = text;
            label.ForeColor = color;
        }

        private void SetMonthBadge(string text, Color foreColor, Color backColor)
        {
            if (_lblMonthBadge == null)
                return;

            _lblMonthBadge.Text = text;
            _lblMonthBadge.ForeColor = foreColor;
            _lblMonthBadge.BackColor = backColor;
        }

        private static bool IsAttendanceExceptionStatus(string status)
        {
            string normalized = (status ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, "Absent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Leave", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "HalfDay", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAttendanceStatus(string status)
        {
            return (status ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
        }

        private static string FormatPercent(decimal value)
        {
            return value.ToString("0.#") + "%";
        }

        private static string InterpretPayrollStatus(string status, PayrollDashboardMetrics metrics)
        {
            if (string.Equals(status, "Locked", StringComparison.OrdinalIgnoreCase))
                return "Closed";
            if (metrics.AttendanceExceptions > 0 || metrics.SalarySetupGaps > 0)
                return "Blocked";
            if (metrics.PendingStatutoryCount > 0 || metrics.PayslipsPending > 0)
                return "In Review";
            return "Ready";
        }

        private static string InterpretCoverage(PayrollDashboardMetrics metrics)
        {
            if (metrics.ActiveEmployees == 0)
                return "Empty";
            decimal ratio = metrics.ActiveEmployees == 0 ? 0m : (metrics.EmployeesIncluded * 100m) / metrics.ActiveEmployees;
            if (ratio >= 99m)
                return "Full";
            if (ratio >= 90m)
                return "Near Full";
            return "Gap";
        }

        private static string InterpretAttendanceReview(PayrollDashboardMetrics metrics)
        {
            if (metrics.AttendanceExceptions == 0)
                return "Clear";
            if (metrics.AttendanceExceptions <= 3)
                return "Watch";
            return "Action";
        }

        private static string InterpretRecoveryPressure(decimal recoveries)
        {
            if (recoveries <= 0m)
                return "Light";
            if (recoveries < 10000m)
                return "Normal";
            if (recoveries < 50000m)
                return "Heavy";
            return "High";
        }

        private static string InterpretComplianceTiming(DateTime? dueDate)
        {
            if (!dueDate.HasValue)
                return "Clear";
            int days = (dueDate.Value.Date - DateTime.Today.Date).Days;
            if (days <= 2)
                return "Urgent";
            if (days <= 7)
                return "Soon";
            return "Scheduled";
        }

        private static string InterpretPunctuality(decimal punctualityRate)
        {
            if (punctualityRate >= 97m)
                return "Strong";
            if (punctualityRate >= 90m)
                return "Stable";
            if (punctualityRate >= 80m)
                return "Watch";
            return "Weak";
        }

        private static string InterpretAbsenteeism(decimal absenteeismRate)
        {
            if (absenteeismRate <= 2m)
                return "Healthy";
            if (absenteeismRate <= 5m)
                return "Watch";
            return "High";
        }

        private static string InterpretLeaveLoad(decimal leaveDays)
        {
            if (leaveDays <= 0m)
                return "Light";
            if (leaveDays <= 5m)
                return "Normal";
            if (leaveDays <= 12m)
                return "Busy";
            return "Heavy";
        }

        private static string InterpretOvertimeRisk(decimal overtimeHours, int ghostCheckoutAlerts)
        {
            if (ghostCheckoutAlerts > 0)
                return "Check Logs";
            if (overtimeHours <= 8m)
                return "Light";
            if (overtimeHours <= 24m)
                return "Watch";
            return "High";
        }

        private static string InterpretDeploymentBalance(decimal deploymentUtilizationRate)
        {
            if (deploymentUtilizationRate <= 0m)
                return "Idle";
            if (deploymentUtilizationRate < 50m)
                return "Open";
            if (deploymentUtilizationRate <= 85m)
                return "Balanced";
            return "Tight";
        }

        private static string InterpretDataCoverage(decimal attendanceCoverageRate)
        {
            if (attendanceCoverageRate >= 98m)
                return "Complete";
            if (attendanceCoverageRate >= 85m)
                return "Usable";
            if (attendanceCoverageRate >= 60m)
                return "Patchy";
            return "Thin";
        }

        private static string BuildPayrollStatusDetail(PayrollRun run, PayrollDashboardMetrics metrics, int month, int year)
        {
            if (run == null)
                return "Run pending for " + new DateTime(year, month, 1).ToString("MMMM yyyy");
            if (string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                return "Closed on " + IndiaFormatHelper.FormatDate(run.RunDate);
            if (metrics.AttendanceExceptions > 0)
                return metrics.AttendanceExceptionPeople;
            if (metrics.SalarySetupGaps > 0)
                return metrics.SalaryGapPeople;
            if (metrics.PendingStatutoryCount > 0)
                return metrics.PendingStatutoryItems;
            return "Processed " + IndiaFormatHelper.FormatDate(run.RunDate);
        }

        private static string BuildCloseHeadline(PayrollRun run, PayrollDashboardMetrics metrics)
        {
            if (run != null && string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                return "This payroll period is locked and ready for archive, register export, and audit review.";
            if (metrics.AttendanceExceptions > 0)
                return "Attendance data still has payroll blockers that should be resolved before calculation.";
            if (metrics.SalarySetupGaps > 0)
                return "Salary setup is incomplete for part of the active team, so this period is not calculation-safe yet.";
            if (run == null)
                return "The period is staged for calculation once attendance, salary, and statutory prechecks are clean.";
            if (metrics.PendingStatutoryCount > 0 || metrics.PayslipsPending > 0)
                return "Payroll has been processed, but close-out still needs statutory or employee communication follow-through.";
            return "The month is calculation-ready and is positioned for final lock once the operator completes the last review.";
        }

        private static string BuildCloseAction(PayrollRun run, PayrollDashboardMetrics metrics)
        {
            if (run != null && string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                return "Review outputs, export statutory files if needed, and move on to the next payroll month.";
            if (metrics.AttendanceExceptions > 0)
                return "Open Attendance and resolve missing rows, absences, leave, and half-day exceptions first.";
            if (metrics.SalarySetupGaps > 0)
                return "Finish missing salary structures so every active employee can be calculated cleanly.";
            if (run == null)
                return "Run payroll once the attendance and salary queues are under control.";
            if (metrics.PendingStatutoryCount > 0)
                return "Review the statutory queue next so due items are understood before final lock.";
            if (metrics.PayslipsPending > 0)
                return "Generate the remaining payslips and clear employee communication before lock.";
            return "Lock payroll after the final verification and archive the period with confidence.";
        }

        private static string BuildPeriodBannerMessage(PayrollRun run, PayrollDashboardMetrics metrics, int month, int year)
        {
            string periodName = new DateTime(year, month, 1).ToString("MMMM yyyy");
            if (run == null)
                return "No payroll run exists for " + periodName + ". Clear attendance and salary blockers, then calculate the month.";
            if (string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                return "Payroll is locked for " + periodName + ". Outputs are ready for export, audit review, and archive.";
            if (metrics.AttendanceExceptions > 0)
                return metrics.AttendanceExceptions + " attendance blocker(s) still need review before " + periodName + " can be closed cleanly.";
            if (metrics.SalarySetupGaps > 0)
                return metrics.SalarySetupGaps + " salary setup blocker(s) still need review before " + periodName + " can be closed cleanly.";
            if (metrics.PendingStatutoryCount > 0)
                return metrics.PendingStatutoryCount + " statutory item(s) still need review before final lock for " + periodName + ".";
            if (metrics.PayslipsPending > 0)
                return metrics.PayslipsPending + " payslip(s) still need generation before employee communication is complete for " + periodName + ".";
            return "Payroll is processed for " + periodName + ". Final review is clear and the period can be locked.";
        }

        private static Color GetPeriodBannerColor(PayrollRun run, PayrollDashboardMetrics metrics)
        {
            if (run != null && string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                return DS.Green600;
            if (metrics.AttendanceExceptions > 0 || metrics.SalarySetupGaps > 0 || metrics.PendingStatutoryCount > 0 || metrics.PayslipsPending > 0)
                return DS.Amber600;
            if (run == null)
                return DS.Slate600;
            return DS.Primary700;
        }

        private static string GetEmployeeName(Dictionary<int, string> employeeNames, int employeeId)
        {
            string name;
            if (employeeNames != null && employeeNames.TryGetValue(employeeId, out name) && !string.IsNullOrWhiteSpace(name))
                return name;

            return "Employee " + employeeId;
        }

        private static string SummarizeEmployeeNames(IEnumerable<string> names, int max = 3)
        {
            return SummarizePlainList(names, max, "No specific employee flagged");
        }

        private static string BuildRosterText(IEnumerable<string> items, string emptyText, int max = 5)
        {
            List<string> cleaned = (items ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (cleaned.Count == 0)
                return emptyText;

            List<string> lines = cleaned.Take(max).Select(item => "- " + item).ToList();
            if (cleaned.Count > max)
                lines.Add("+ " + (cleaned.Count - max) + " more");
            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildLateAbsentRoster(PayrollDashboardMetrics metrics, bool full = false)
        {
            if (metrics == null)
                return "No late or absent employees flagged";

            List<string> lines = new List<string>();
            if (metrics.LateEmployees > 0)
            {
                lines.Add("Late");
                lines.Add(full ? metrics.LateRosterFull : metrics.LateRoster);
            }

            if (metrics.AbsentEmployees > 0)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);
                lines.Add("Absent");
                lines.Add(full ? metrics.AbsentRosterFull : metrics.AbsentRoster);
            }

            return lines.Count == 0 ? "No late or absent employees flagged" : string.Join(Environment.NewLine, lines);
        }

        private static List<PayrollInsightBadge> BuildInsightBadges(params PayrollInsightBadge[] badges)
        {
            return (badges ?? new PayrollInsightBadge[0])
                .Where(badge => badge != null && badge.Count > 0)
                .Take(3)
                .ToList();
        }

        private static List<PayrollInsightRow> BuildAttendanceInsightRows(
            Dictionary<int, Employee> employeeLookup,
            Dictionary<int, string> employeeNames,
            IEnumerable<int> ghostCheckoutEmployeeIds,
            IEnumerable<int> attendanceExceptionIds,
            IEnumerable<int> leaveReviewEmployeeIds,
            IEnumerable<int> halfDayReviewEmployeeIds)
        {
            List<PayrollInsightRow> rows = new List<PayrollInsightRow>();
            HashSet<int> seen = new HashSet<int>();
            AddInsightRows(rows, seen, ghostCheckoutEmployeeIds, employeeLookup, employeeNames, "Missing punch-out", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, "Yesterday still open"));
            AddInsightRows(rows, seen, attendanceExceptionIds, employeeLookup, employeeNames, "No attendance row", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, employee == null ? null : employee.Designation));
            AddInsightRows(rows, seen, leaveReviewEmployeeIds, employeeLookup, employeeNames, "Leave posted", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, "Review before payroll"));
            AddInsightRows(rows, seen, halfDayReviewEmployeeIds, employeeLookup, employeeNames, "Half-day conflict", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, employee == null ? null : employee.Department));
            return rows.Take(6).ToList();
        }

        private static List<PayrollInsightRow> BuildSalaryInsightRows(
            Dictionary<int, Employee> employeeLookup,
            Dictionary<int, string> employeeNames,
            IEnumerable<int> salaryGapEmployeeIds,
            IEnumerable<int> missingBankEmployeeIds,
            IEnumerable<int> kycGapEmployeeIds)
        {
            List<PayrollInsightRow> rows = new List<PayrollInsightRow>();
            HashSet<int> seen = new HashSet<int>();
            AddInsightRows(rows, seen, salaryGapEmployeeIds, employeeLookup, employeeNames, "No CTC structure", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, employee == null ? null : employee.Designation));
            AddInsightRows(rows, seen, missingBankEmployeeIds, employeeLookup, employeeNames, "Bank details missing", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, "Account or IFSC"));
            AddInsightRows(rows, seen, kycGapEmployeeIds, employeeLookup, employeeNames, "Tax or KYC missing", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, BuildKycGapSummary(employee)));
            return rows.Take(6).ToList();
        }

        private static List<PayrollInsightRow> BuildLateAbsentInsightRows(
            Dictionary<int, Employee> employeeLookup,
            Dictionary<int, string> employeeNames,
            IEnumerable<int> lateEmployeeIds,
            IEnumerable<int> absentEmployeeIds)
        {
            List<PayrollInsightRow> rows = new List<PayrollInsightRow>();
            HashSet<int> seen = new HashSet<int>();
            AddInsightRows(rows, seen, absentEmployeeIds, employeeLookup, employeeNames, "Absent today", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, employee == null ? null : employee.Department));
            AddInsightRows(rows, seen, lateEmployeeIds, employeeLookup, employeeNames, "Late arrival", employee => JoinInsightContext(employee == null ? null : employee.ClientSite, employee == null ? null : employee.Designation));
            return rows.Take(6).ToList();
        }

        private static List<PayrollInsightRow> BuildPayslipInsightRows(
            Dictionary<int, Employee> employeeLookup,
            Dictionary<int, string> employeeNames,
            List<PayrollEntry> entries)
        {
            List<PayrollInsightRow> rows = new List<PayrollInsightRow>();
            foreach (PayrollEntry entry in (entries ?? new List<PayrollEntry>())
                .Where(e => !e.PayslipGenerated)
                .OrderByDescending(e => string.IsNullOrWhiteSpace(e.BankAccount) || string.IsNullOrWhiteSpace(e.BankIFSC))
                .ThenByDescending(e => e.LoanDeduction + e.AdvanceDeduction)
                .ThenBy(e => string.IsNullOrWhiteSpace(e.EmployeeName) ? GetEmployeeName(employeeNames, e.EmployeeId) : e.EmployeeName))
            {
                Employee employee;
                employeeLookup.TryGetValue(entry.EmployeeId, out employee);
                string reason;
                if (string.IsNullOrWhiteSpace(entry.BankAccount) || string.IsNullOrWhiteSpace(entry.BankIFSC))
                    reason = "Bank check pending";
                else if (entry.LoanDeduction > 0m || entry.AdvanceDeduction > 0m)
                    reason = "Recovery review";
                else
                    reason = "Payslip not generated";

                rows.Add(new PayrollInsightRow
                {
                    Employee = string.IsNullOrWhiteSpace(entry.EmployeeName) ? GetEmployeeName(employeeNames, entry.EmployeeId) : entry.EmployeeName,
                    Reason = reason,
                    Context = JoinInsightContext(employee == null ? null : employee.ClientSite, IndiaFormatHelper.FormatCurrency(entry.NetSalary))
                });
            }

            return rows.Take(6).ToList();
        }

        private static void AddInsightRows(
            List<PayrollInsightRow> rows,
            HashSet<int> seen,
            IEnumerable<int> employeeIds,
            Dictionary<int, Employee> employeeLookup,
            Dictionary<int, string> employeeNames,
            string reason,
            Func<Employee, string> contextBuilder)
        {
            foreach (int employeeId in employeeIds ?? Enumerable.Empty<int>())
            {
                if (!seen.Add(employeeId))
                    continue;

                Employee employee;
                employeeLookup.TryGetValue(employeeId, out employee);
                rows.Add(new PayrollInsightRow
                {
                    Employee = GetEmployeeName(employeeNames, employeeId),
                    Reason = reason,
                    Context = contextBuilder == null ? string.Empty : contextBuilder(employee)
                });
            }
        }

        private static string BuildInsightRosterDetail(IEnumerable<PayrollInsightRow> rows, string emptyText)
        {
            List<PayrollInsightRow> cleaned = (rows ?? Enumerable.Empty<PayrollInsightRow>()).Where(row => row != null).ToList();
            if (cleaned.Count == 0)
                return emptyText;

            return string.Join(Environment.NewLine, cleaned.Select(row => "- " + row.Employee + " | " + row.Reason + " | " + row.Context));
        }

        private static string BuildKycGapSummary(Employee employee)
        {
            if (employee == null)
                return "PAN, Aadhaar, or tax regime";

            List<string> gaps = new List<string>();
            if (string.IsNullOrWhiteSpace(employee.PANNumber ?? employee.PAN))
                gaps.Add("PAN");
            if (string.IsNullOrWhiteSpace(employee.AadhaarNumber))
                gaps.Add("Aadhaar");
            if (string.IsNullOrWhiteSpace(employee.TaxRegime))
                gaps.Add("Tax regime");
            return gaps.Count == 0 ? "KYC review" : string.Join(", ", gaps);
        }

        private static string JoinInsightContext(params string[] parts)
        {
            List<string> cleaned = (parts ?? new string[0])
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return cleaned.Count == 0 ? "Needs review" : string.Join(" | ", cleaned);
        }

        private static string SummarizePlainList(IEnumerable<string> items, int max, string emptyText)
        {
            List<string> cleaned = (items ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (cleaned.Count == 0)
                return emptyText;
            if (cleaned.Count <= max)
                return string.Join(", ", cleaned);
            return string.Join(", ", cleaned.Take(max)) + " +" + (cleaned.Count - max);
        }

        private static bool IsOpenFieldJobStatus(string status)
        {
            string normalized = (status ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return true;

            return !string.Equals(normalized, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "Resolved", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalized, "Invoiced", StringComparison.OrdinalIgnoreCase);
        }

        private void ShowRosterDetailDialog(string title, string status, string detail, Color accent)
        {
            using (var dialog = ServoModalForm.Create(title, 980, 680))
            {
                dialog.StartPosition = FormStartPosition.CenterParent;

                List<PayrollExceptionDialogRow> rows = ParseExceptionDialogRows(detail);
                int total = ExtractLeadingCount(status);
                if (total <= 0)
                    total = rows.Count;

                Panel shell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = DS.BgPage };
                Panel card = MakePayrollCard();
                card.Dock = DockStyle.Fill;
                card.Padding = new Padding(22, 22, 22, 18);

                Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.White };
                Panel iconCircle = new Panel
                {
                    Location = new Point(0, 6),
                    Size = new Size(50, 50),
                    BackColor = Color.FromArgb(235, 242, 255)
                };
                DS.Rounded(iconCircle, 25);
                Label icon = new Label
                {
                    Text = GetExceptionDialogIconText(title),
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    ForeColor = DS.Primary600,
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                iconCircle.Controls.Add(icon);

                Label titleLabel = new Label
                {
                    Location = new Point(66, 8),
                    Size = new Size(760, 38),
                    Text = title,
                    Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                    ForeColor = DS.Slate900,
                    AutoEllipsis = true
                };
                header.Controls.Add(titleLabel);
                header.Controls.Add(iconCircle);

                Panel alert = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 54,
                    BackColor = DS.Amber50,
                    Margin = Padding.Empty,
                    Padding = new Padding(16, 0, 16, 0)
                };
                DS.Rounded(alert, 8);
                alert.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(Color.FromArgb(253, 186, 116)))
                        e.Graphics.DrawRectangle(pen, 0, 0, alert.Width - 1, alert.Height - 1);
                };
                Label alertIcon = new Label
                {
                    Text = "!",
                    Dock = DockStyle.Left,
                    Width = 34,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = DS.Amber600
                };
                Label statusLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = status,
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = DS.Amber600,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                };
                alert.Controls.Add(statusLabel);
                alert.Controls.Add(alertIcon);

                FlowLayoutPanel list = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.White,
                    AutoScroll = true,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    Padding = new Padding(0, 10, 4, 10),
                    Margin = Padding.Empty
                };
                DS.Rounded(list, 8);
                list.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(DS.Slate200))
                        e.Graphics.DrawRectangle(pen, 0, 0, list.Width - 1, list.Height - 1);
                };

                if (rows.Count == 0)
                {
                    Label empty = new Label
                    {
                        Text = string.IsNullOrWhiteSpace(detail) ? "No records available." : detail,
                        Width = 820,
                        Height = 80,
                        Margin = new Padding(12),
                        Font = new Font("Segoe UI", 10f),
                        ForeColor = DS.Slate600,
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    list.Controls.Add(empty);
                }
                else
                {
                    foreach (PayrollExceptionDialogRow row in rows)
                        list.Controls.Add(BuildExceptionDialogRow(row, accent));
                }

                Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(12, 12, 12, 8), BackColor = Color.White };
                footer.Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(DS.Slate200))
                        e.Graphics.DrawRectangle(pen, 0, 0, footer.Width - 1, footer.Height - 1);
                };
                DS.Rounded(footer, 8);

                Label totalPrefixLabel = new Label
                {
                    Text = "Total:",
                    Location = new Point(24, 18),
                    Size = new Size(54, 32),
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = DS.Slate600,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                Label totalNumberLabel = new Label
                {
                    Text = total.ToString("N0"),
                    Location = new Point(78, 14),
                    Size = new Size(82, 38),
                    Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    ForeColor = DS.Primary600,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true
                };
                Label totalSuffixLabel = new Label
                {
                    Text = "employee(s)",
                    Location = new Point(154, 18),
                    Size = new Size(130, 32),
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = DS.Slate600,
                    TextAlign = ContentAlignment.MiddleLeft
                };

                Panel closeButton = new Panel
                {
                    Size = new Size(142, 46),
                    BackColor = DS.Primary600,
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };
                DS.Rounded(closeButton, DS.RadiusSm);
                Label closeText = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "Close",
                    Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                closeButton.Controls.Add(closeText);
                closeButton.Click += (s, e) => dialog.Close();
                closeText.Click += (s, e) => dialog.Close();
                closeButton.MouseEnter += (s, e) => closeButton.BackColor = DS.Lighten(DS.Primary600, 0.08f);
                closeButton.MouseLeave += (s, e) => closeButton.BackColor = DS.Primary600;
                closeButton.MouseDown += (s, e) => closeButton.BackColor = DS.Darken(DS.Primary600, 0.10f);
                closeButton.MouseUp += (s, e) => closeButton.BackColor = DS.Lighten(DS.Primary600, 0.08f);
                footer.Controls.Add(closeButton);
                footer.Controls.Add(totalSuffixLabel);
                footer.Controls.Add(totalNumberLabel);
                footer.Controls.Add(totalPrefixLabel);
                footer.Resize += (s, e) =>
                {
                    closeButton.Location = new Point(Math.Max(0, footer.ClientSize.Width - closeButton.Width - 12), 14);
                };

                list.Resize += (s, e) =>
                {
                    int rowWidth = Math.Max(420, list.ClientSize.Width - 28);
                    foreach (Control control in list.Controls)
                        control.Width = rowWidth;
                };

                Panel spacer = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = Color.White };
                Panel footerSpacer = new Panel { Dock = DockStyle.Bottom, Height = 14, BackColor = Color.White };
                card.Controls.Add(list);
                card.Controls.Add(footerSpacer);
                card.Controls.Add(footer);
                card.Controls.Add(spacer);
                card.Controls.Add(alert);
                card.Controls.Add(header);
                shell.Controls.Add(card);
                dialog.Controls.Add(shell);
                dialog.ShowDialog(this);
            }
        }

        private static Panel BuildExceptionDialogRow(PayrollExceptionDialogRow row, Color accent)
        {
            Panel panel = new Panel
            {
                Height = 74,
                Width = 820,
                Margin = new Padding(10, 0, 10, 0),
                Padding = new Padding(14, 8, 14, 8),
                BackColor = Color.White
            };
            panel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Slate200))
                    e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
            };

            Color avatarBack = GetExceptionAvatarBackColor(row.Employee);
            Label avatar = new Label
            {
                Text = GetInitials(row.Employee),
                Location = new Point(14, 13),
                Size = new Size(48, 48),
                BackColor = avatarBack,
                ForeColor = GetExceptionAvatarTextColor(avatarBack),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            DS.Rounded(avatar, 24);

            Label name = new Label
            {
                Text = string.IsNullOrWhiteSpace(row.Employee) ? "Blank" : row.Employee,
                Location = new Point(78, 12),
                Size = new Size(460, 24),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            Label context = new Label
            {
                Text = BuildExceptionDialogContext(row),
                Location = new Point(78, 38),
                Size = new Size(560, 22),
                Font = new Font("Segoe UI", 9.25f),
                ForeColor = DS.Slate700,
                AutoEllipsis = true
            };

            Label badge = new Label
            {
                Text = row.Reason,
                Size = new Size(178, 32),
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true
            };
            ApplyExceptionBadgeStyle(badge, row.Reason, accent);
            panel.Resize += (s, e) =>
            {
                int badgeLeft = Math.Max(560, panel.ClientSize.Width - badge.Width - 48);
                badge.Location = new Point(badgeLeft, 21);
                name.Width = Math.Max(200, badgeLeft - name.Left - 18);
                context.Width = Math.Max(200, badgeLeft - context.Left - 18);
            };

            panel.Controls.Add(badge);
            panel.Controls.Add(context);
            panel.Controls.Add(name);
            panel.Controls.Add(avatar);
            return panel;
        }

        private static void ApplyExceptionBadgeStyle(Label badge, string reason, Color accent)
        {
            string normalized = (reason ?? string.Empty).ToLowerInvariant();
            if (normalized.Contains("review") || normalized.Contains("pending"))
            {
                badge.BackColor = DS.Amber50;
                badge.ForeColor = DS.Amber600;
            }
            else if (normalized.Contains("generated") || normalized.Contains("ready"))
            {
                badge.BackColor = DS.Green50;
                badge.ForeColor = DS.Green600;
            }
            else
            {
                badge.BackColor = DS.Red50;
                badge.ForeColor = DS.Red600;
            }
            DS.Rounded(badge, 7);
        }

        private static string BuildExceptionDialogContext(PayrollExceptionDialogRow row)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(row.Reason))
                parts.Add(row.Reason);
            if (!string.IsNullOrWhiteSpace(row.Context))
                parts.Add(row.Context);
            return parts.Count == 0 ? "Needs review" : string.Join("   |   ", parts);
        }

        private static List<PayrollExceptionDialogRow> ParseExceptionDialogRows(string detail)
        {
            List<PayrollExceptionDialogRow> rows = new List<PayrollExceptionDialogRow>();
            foreach (string rawLine in (detail ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("-", StringComparison.Ordinal))
                    line = line.Substring(1).Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('|').Select(part => part.Trim()).ToArray();
                if (parts.Length == 1)
                {
                    rows.Add(new PayrollExceptionDialogRow { Employee = parts[0], Reason = "Needs review", Context = string.Empty });
                }
                else
                {
                    rows.Add(new PayrollExceptionDialogRow
                    {
                        Employee = parts[0],
                        Reason = string.IsNullOrWhiteSpace(parts[1]) ? "Needs review" : parts[1],
                        Context = string.Join("   |   ", parts.Skip(2).Where(part => !string.IsNullOrWhiteSpace(part)))
                    });
                }
            }
            return rows;
        }

        private static int ExtractLeadingCount(string status)
        {
            string digits = new string((status ?? string.Empty).TakeWhile(char.IsDigit).ToArray());
            int value;
            return int.TryParse(digits, out value) ? value : 0;
        }

        private static string GetExceptionDialogIconText(string title)
        {
            string value = title ?? string.Empty;
            if (value.IndexOf("Attendance", StringComparison.OrdinalIgnoreCase) >= 0)
                return "AT";
            if (value.IndexOf("Salary", StringComparison.OrdinalIgnoreCase) >= 0)
                return "SA";
            if (value.IndexOf("Payslip", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PS";
            if (value.IndexOf("Late", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("Absent", StringComparison.OrdinalIgnoreCase) >= 0)
                return "LA";
            if (value.IndexOf("TDS", StringComparison.OrdinalIgnoreCase) >= 0)
                return "TD";
            return "EX";
        }

        private static Color GetExceptionAvatarBackColor(string value)
        {
            Color[] colors =
            {
                DS.Primary100,
                Color.FromArgb(237, 233, 254),
                Color.FromArgb(220, 252, 231),
                Color.FromArgb(254, 243, 199),
                Color.FromArgb(207, 250, 254),
                DS.Slate100
            };
            int hash = Math.Abs((value ?? string.Empty).GetHashCode());
            return colors[hash % colors.Length];
        }

        private static Color GetExceptionAvatarTextColor(Color backColor)
        {
            if (backColor.ToArgb() == DS.Primary100.ToArgb())
                return DS.Primary600;
            if (backColor.ToArgb() == DS.Slate100.ToArgb())
                return DS.Slate700;
            if (backColor.G > backColor.R && backColor.G > backColor.B)
                return DS.Green600;
            if (backColor.R > 245 && backColor.G > 230)
                return DS.Amber600;
            if (backColor.B > backColor.R)
                return Color.FromArgb(109, 40, 217);
            return DS.Teal600;
        }

        private static string GetInitials(string name)
        {
            string cleaned = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleaned) || string.Equals(cleaned, "Blank", StringComparison.OrdinalIgnoreCase))
                return "--";

            string[] parts = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
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
            bool required = (text ?? string.Empty).Contains("*");
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = required ? DS.Primary700 : Color.FromArgb(51, 65, 85),
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
            if (listBox.SelectedItem == null && rows.Count > 0)
                listBox.SelectedIndex = 0;
            UpdateEmployeePickerTotal(listBox, rows.Count);
            listBox.Invalidate();
        }

        private void UpdateEmployeePickerTotal(ListBox listBox, int count)
        {
            if (listBox == null)
                return;
            Control parent = listBox.Parent;
            while (parent != null)
            {
                Label value = parent.Controls.Find("TotalEmployeesValue", true).OfType<Label>().FirstOrDefault();
                if (value != null)
                {
                    value.Text = count.ToString();
                    return;
                }
                parent = parent.Parent;
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
            if (_btnGenerateSelectedPayslip != null)
                _btnGenerateSelectedPayslip.Enabled = !isBusy;
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
                Text = message,
                AutoEllipsis = true
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
            using (var form = ServoModalForm.Create(title, 360, 150))
            {
                var label = new Label { Text = prompt, Left = 12, Top = 14, Width = 320, Height = 22, AutoEllipsis = true };
                var text = new TextBox { Left = 12, Top = 42, Width = 320, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                var ok = new Button { Text = "OK", Left = 176, Top = 78, Width = 72, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
                var cancel = new Button { Text = "Cancel", Left = 260, Top = 78, Width = 72, DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
                form.Controls.AddRange(new Control[] { label, text, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog(this) == DialogResult.OK ? text.Text.Trim() : null;
            }
        }
    }
}

