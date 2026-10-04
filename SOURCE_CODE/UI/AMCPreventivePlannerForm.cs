using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Human-reviewed AMC visit calendar, capacity view, and unsent reminder drafts.</summary>
    public sealed class AMCPreventivePlannerForm : ServoFormBase
    {
        private readonly AmcPreventivePlannerService _service = new AmcPreventivePlannerService();
        private readonly AmcPreventivePlan _previewPlan;
        private AmcPreventivePlan _plan;
        private Label _contractMetric;
        private Label _proposedMetric;
        private Label _missedMetric;
        private Label _capacityMetric;
        private Label _status;
        private DataGridView _calendarGrid;
        private DataGridView _reminderGrid;
        private RichTextBox _reviewText;
        private Button _confirmButton;
        private bool _busy;

        public bool PlanApplied { get; private set; }

        public AMCPreventivePlannerForm() : this(null) { }

        internal AMCPreventivePlannerForm(AmcPreventivePlan previewPlan)
        {
            _previewPlan = previewPlan;
            BuildLayout();
            Shown += async (s, e) => await LoadPlanAsync();
        }

        private void BuildLayout()
        {
            Text = BrandingService.WindowTitle("AMC Preventive Planner");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1220, 790);
            MinimumSize = new Size(1000, 680);
            BackColor = DS.BgPage;
            Font = DS.Body;

            Panel header = new Panel { Name = "AmcPlannerHeader", Tag = "dialog no-global-actions", Dock = DockStyle.Top, Height = 108, BackColor = DS.White, Padding = new Padding(24, 14, 24, 10) };
            header.Controls.Add(new Label { Text = "AMC Preventive Planner", Location = new Point(24, 13), Size = new Size(540, 34), Font = DS.H1, ForeColor = DS.Slate900 });
            header.Controls.Add(new Label { Text = "Balance contract visits against field capacity, surface missed obligations, and prepare customer reminders.", Location = new Point(26, 48), Size = new Size(1080, 23), Font = DS.Body, ForeColor = DS.Slate600 });
            header.Controls.Add(new Label { Text = "Nothing is sent automatically. New visits are created only after dispatcher confirmation.", Location = new Point(26, 73), Size = new Size(960, 21), Font = DS.SmallBold, ForeColor = DS.Primary700 });

            TableLayoutPanel metrics = new TableLayoutPanel { Name = "AmcPlannerMetrics", Tag = "metric", Dock = DockStyle.Top, Height = 92, BackColor = DS.BgPage, Padding = new Padding(18, 12, 18, 8), ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _contractMetric = AddMetric(metrics, 0, "ACTIVE CONTRACTS", "–");
            _proposedMetric = AddMetric(metrics, 1, "PROPOSED VISITS", "–");
            _missedMetric = AddMetric(metrics, 2, "MISSED OBLIGATIONS", "–");
            _capacityMetric = AddMetric(metrics, 3, "CAPACITY FLAGS", "–");

            TabControl tabs = new TabControl { Name = "AmcPlannerTabs", Dock = DockStyle.Fill, Font = DS.Body, Padding = new Point(16, 8) };
            _calendarGrid = BuildCalendarGrid();
            _reminderGrid = BuildReminderGrid();
            tabs.TabPages.Add(BuildTab("1  Visit calendar", _calendarGrid));
            tabs.TabPages.Add(BuildTab("2  Reminder drafts", _reminderGrid));
            var reviewPage = new TabPage("3  Review & confirm") { BackColor = DS.White, Padding = new Padding(18) };
            _reviewText = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = DS.White, ForeColor = DS.Slate700, Font = DS.Body };
            reviewPage.Controls.Add(_reviewText);
            tabs.TabPages.Add(reviewPage);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = DS.White, Padding = new Padding(22, 12, 22, 10) };
            _status = new Label { Text = "Preparing the preventive plan…", Location = new Point(22, 22), Size = new Size(760, 24), Font = DS.SmallBold, ForeColor = DS.Slate600, AutoEllipsis = true };
            _confirmButton = MakeButton("Confirm plan", DS.Primary700, Color.White, 155);
            _confirmButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _confirmButton.Location = new Point(850, 12);
            _confirmButton.Enabled = false;
            _confirmButton.Click += async (s, e) => await ConfirmAsync();
            Button close = MakeButton("Close", DS.White, DS.Slate700, 95);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(1067, 12);
            close.Click += (s, e) => Close();
            footer.Controls.Add(_status);
            footer.Controls.Add(_confirmButton);
            footer.Controls.Add(close);
            footer.Resize += (s, e) =>
            {
                close.Left = footer.ClientSize.Width - close.Width - 22;
                _confirmButton.Left = close.Left - _confirmButton.Width - 12;
                _status.Width = Math.Max(160, _confirmButton.Left - _status.Left - 18);
            };

            Controls.Add(tabs);
            Controls.Add(footer);
            Controls.Add(metrics);
            Controls.Add(header);
        }

        private async Task LoadPlanAsync()
        {
            if (_plan != null) return;
            SetBusy(true, "Analysing AMC obligations and field capacity…");
            try
            {
                _plan = _previewPlan ?? await Task.Run(() => _service.BuildPlan());
                BindPlan();
                _status.Text = _plan.ProposedVisitCount == 0
                    ? "All active AMC obligations already have visit records."
                    : _plan.ProposedVisitCount + " proposed visit(s) are ready for dispatcher review.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AMCPreventivePlanner.Load", ex);
                _status.Text = "The preventive plan could not be prepared. No records were changed.";
                _status.ForeColor = DS.Red600;
            }
            finally { SetBusy(false, null); }
        }

        private void BindPlan()
        {
            _contractMetric.Text = _plan.ActiveContractCount.ToString();
            _proposedMetric.Text = _plan.ProposedVisitCount.ToString();
            _missedMetric.Text = _plan.MissedObligationCount.ToString();
            _capacityMetric.Text = _plan.CapacityConflictCount.ToString();
            _calendarGrid.DataSource = _plan.Obligations.OrderBy(value => value.PlannedDate).ThenBy(value => value.ClientName).ToList();
            _reminderGrid.DataSource = _plan.ReminderDrafts.OrderBy(value => value.ScheduledDate).ToList();
            _reviewText.Text = BuildReviewText(_plan);
        }

        private async Task ConfirmAsync()
        {
            if (_busy || _plan == null || (_plan.ProposedVisitCount == 0 && _plan.ReminderDrafts.Count == 0) || _previewPlan != null) return;
            if (!ServoConfirmDialog.Show(this, "Confirm the reviewed AMC preventive plan?",
                "This creates " + _plan.ProposedVisitCount + " missing visit(s) and saves " + _plan.ReminderDrafts.Count + " reminder(s) as Draft only. It will not send WhatsApp messages or emails.")) return;
            SetBusy(true, "Creating confirmed visits and saving unsent reminder drafts…");
            try
            {
                AmcPlannerApplyResult result = await Task.Run(() => _service.Confirm(_plan));
                PlanApplied = true;
                _confirmButton.Enabled = false;
                _status.Text = result.VisitsCreated + " visit(s) created; " + result.ReminderDraftsCreated + " reminder draft(s) saved. Nothing was sent.";
                _status.ForeColor = DS.Green600;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AMCPreventivePlanner.Confirm", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("AMC Preventive Planner"), "Creating AMC preventive visits", ex);
                _status.Text = "The plan was not completed. It is safe to review and retry.";
                _status.ForeColor = DS.Red600;
            }
            finally { SetBusy(false, null); }
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            _confirmButton.Enabled = !busy && !PlanApplied && _plan != null && (_plan.ProposedVisitCount > 0 || _plan.ReminderDrafts.Count > 0);
            if (!string.IsNullOrWhiteSpace(message)) _status.Text = message;
        }

        private static string BuildReviewText(AmcPreventivePlan plan)
        {
            return "Dispatcher confirmation summary\r\n\r\n" +
                   "• Review " + plan.ActiveContractCount + " active AMC contract(s).\r\n" +
                   "• Create " + plan.ProposedVisitCount + " missing preventive visit(s).\r\n" +
                   "• Daily planning capacity: " + plan.DailyCapacity + " combined open job/visit slot(s).\r\n" +
                   "• " + plan.MissedObligationCount + " missed obligation(s) require dispatcher follow-up.\r\n" +
                   "• " + plan.CapacityConflictCount + " proposal(s) remain above capacity and should be reviewed.\r\n" +
                   "• Prepare " + plan.ReminderDrafts.Count + " WhatsApp/email reminder draft(s). No message is auto-sent.\r\n\r\n" +
                   "Existing visits are never duplicated: confirmation checks each contract and visit number again inside one transaction.";
        }

        private static Label AddMetric(TableLayoutPanel host, int column, string title, string value)
        {
            Panel card = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Margin = new Padding(0, 0, column == 3 ? 0 : 10, 0), Padding = new Padding(14, 8, 14, 6) };
            card.Controls.Add(new Label { Text = title, Location = new Point(14, 8), Size = new Size(240, 18), Font = DS.SmallBold, ForeColor = DS.Slate600 });
            Label metric = new Label { Text = value, Location = new Point(14, 30), Size = new Size(240, 35), Font = DS.H2, ForeColor = DS.Slate900 };
            card.Controls.Add(metric);
            host.Controls.Add(card, column, 0);
            return metric;
        }

        private static TabPage BuildTab(string title, Control content)
        {
            var page = new TabPage(title) { BackColor = DS.White, Padding = new Padding(12) };
            page.Controls.Add(content);
            return page;
        }

        private static DataGridView BuildCalendarGrid()
        {
            DataGridView grid = BaseGrid("AmcPreventiveCalendarGrid");
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AmcNumber", HeaderText = "AMC", Width = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ClientName", HeaderText = "Customer", Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SiteName", HeaderText = "Site", Width = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "VisitNumber", HeaderText = "Visit", Width = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TargetDate", HeaderText = "Target", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PlannedDate", HeaderText = "Planned", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PlannedLoad", HeaderText = "Load", Width = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Status", Width = 130 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CapacityNote", HeaderText = "Capacity", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            return grid;
        }

        private static DataGridView BuildReminderGrid()
        {
            DataGridView grid = BaseGrid("AmcReminderDraftGrid");
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ClientName", HeaderText = "Customer", Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SiteName", HeaderText = "Site", Width = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ScheduledDate", HeaderText = "Visit date", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Channel", HeaderText = "Channel", Width = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Recipient", HeaderText = "Recipient", Width = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DraftText", HeaderText = "Unsent reminder draft", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "State", Width = 75 });
            return grid;
        }

        private static DataGridView BaseGrid(string name)
        {
            var grid = new DataGridView { Name = name, Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = DS.White, BorderStyle = BorderStyle.None };
            GridTheme.Apply(grid, fillWidth: false, alternateRows: true, rowHeight: 42);
            return grid;
        }

        private static Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button { Text = text, Size = new Size(width, 40), BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = DS.BodyBold, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = back == DS.White ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.Border;
            return button;
        }
    }
}
