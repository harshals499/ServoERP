using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>
    /// Dashboard for the local-first ServoERP Brain "AI Dev Team" - shows agent
    /// status, task pipeline progress, logs, file changes, build/test results,
    /// and lets the user submit/retry/stop tasks and open final reports.
    /// </summary>
    public sealed class DevTeamDashboardForm : ServoFormBase
    {
        private readonly DevTeamBrainService _service = DevTeamBrainService.Instance;

        private Label _lblTitle;
        private Label _lblStatus;
        private Button _btnNewTask;
        private Button _btnRetry;
        private Button _btnStop;
        private Button _btnOpenReport;
        private Button _btnRefresh;

        private DataGridView _gridAgents;
        private DataGridView _gridTasks;

        private ListBox _stepsList;
        private ListBox _logList;
        private ListBox _filesList;
        private ListBox _testsList;

        private DevTeamBrainSnapshot _snapshot;
        private int? _selectedTaskId;

        public DevTeamDashboardForm()
        {
            Text = "ServoERP Brain - Dev Team Dashboard";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 760);
            MinimumSize = new Size(980, 640);
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", 9f);

            BuildLayout();

            _service.ProgressChanged += ServiceProgressChanged;
            FormClosed += (s, e) =>
            {
                _service.ProgressChanged -= ServiceProgressChanged;
                _service.StopPolling();
            };

            if (_service.DatabaseExists)
            {
                _service.StartPolling(2000);
            }
            else
            {
                _lblStatus.Text = "ServoERP Brain database not found. Run 'python ServoERPBrain/memory/init_db.py' once to initialize it.";
            }
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = DS.White, Padding = new Padding(20, 12, 20, 0) };
            _lblTitle = new Label
            {
                Text = "Dev Team Dashboard",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            _lblStatus = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = DS.Slate600
            };
            header.Controls.Add(_lblStatus);
            header.Controls.Add(_lblTitle);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(20, 10, 20, 0),
                BackColor = DS.BgPage,
                WrapContents = true
            };
            _btnNewTask = DS.PrimaryBtn("New Task", 120, 34);
            _btnRetry = DS.GhostBtn("Retry", 100, 34);
            _btnStop = DS.GhostBtn("Stop", 100, 34);
            _btnOpenReport = DS.GhostBtn("Open Report", 130, 34);
            _btnRefresh = DS.GhostBtn("Refresh", 100, 34);
            _btnNewTask.Click += (s, e) => SubmitNewTask();
            _btnRetry.Click += (s, e) => RetrySelectedTask();
            _btnStop.Click += (s, e) => StopSelectedTask();
            _btnOpenReport.Click += (s, e) => OpenSelectedReport();
            _btnRefresh.Click += (s, e) => RefreshNow();
            actions.Controls.AddRange(new Control[] { _btnNewTask, _btnRetry, _btnStop, _btnOpenReport, _btnRefresh });

            SplitContainer mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                BackColor = DS.BgPage
            };

            SplitContainer leftSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = DS.BgPage
            };

            Panel agentsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 10, 10), BackColor = DS.BgPage };
            agentsPanel.Controls.Add(SectionLabel("Agents"));
            _gridAgents = new DataGridView();
            BuildAgentsGrid();
            agentsPanel.Controls.Add(_gridAgents);
            agentsPanel.Controls.SetChildIndex(agentsPanel.Controls[0], 0);

            Panel tasksPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 10, 10), BackColor = DS.BgPage };
            tasksPanel.Controls.Add(SectionLabel("Tasks"));
            _gridTasks = new DataGridView();
            BuildTasksGrid();
            _gridTasks.SelectionChanged += (s, e) => OnTaskSelectionChanged();
            tasksPanel.Controls.Add(_gridTasks);
            tasksPanel.Controls.SetChildIndex(tasksPanel.Controls[0], 0);

            leftSplit.Panel1.Controls.Add(agentsPanel);
            leftSplit.Panel2.Controls.Add(tasksPanel);

            Panel detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 20, 10), BackColor = DS.BgPage };
            TabControl tabs = new TabControl { Dock = DockStyle.Fill, Font = DS.Body };

            TabPage stepsTab = new TabPage("Pipeline Steps");
            _stepsList = MakeListBox();
            stepsTab.Controls.Add(_stepsList);

            TabPage logsTab = new TabPage("Logs");
            _logList = MakeListBox();
            logsTab.Controls.Add(_logList);

            TabPage filesTab = new TabPage("Files Changed");
            _filesList = MakeListBox();
            filesTab.Controls.Add(_filesList);

            TabPage testsTab = new TabPage("Build / Test Results");
            _testsList = MakeListBox();
            testsTab.Controls.Add(_testsList);

            tabs.TabPages.Add(stepsTab);
            tabs.TabPages.Add(logsTab);
            tabs.TabPages.Add(filesTab);
            tabs.TabPages.Add(testsTab);

            detailPanel.Controls.Add(tabs);
            detailPanel.Controls.Add(SectionLabel("Task Detail"));
            detailPanel.Controls.SetChildIndex(detailPanel.Controls[0], 0);

            mainSplit.Panel1.Controls.Add(leftSplit);
            mainSplit.Panel2.Controls.Add(detailPanel);

            Controls.Add(mainSplit);
            Controls.Add(actions);
            Controls.Add(header);

            // Set proportions after controls are added so SplitterDistance is valid.
            Shown += (s, e) =>
            {
                try
                {
                    mainSplit.SplitterDistance = (int)(mainSplit.Width * 0.5);
                    leftSplit.SplitterDistance = (int)(leftSplit.Height * 0.35);
                }
                catch (InvalidOperationException)
                {
                    // splitter bounds not ready yet - ignore, defaults apply
                }
            };
        }

        private static Label SectionLabel(string text)
        {
            Label label = new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 28,
                Font = DS.H3,
                ForeColor = DS.Slate900
            };
            return label;
        }

        private static ListBox MakeListBox()
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                Font = DS.Mono,
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
        }

        private void BuildAgentsGrid()
        {
            _gridAgents.Columns.Add("Name", "Agent");
            _gridAgents.Columns.Add("Role", "Role");
            _gridAgents.Columns.Add("Status", "Status");
            _gridAgents.Columns.Add("CurrentTask", "Current Task");
            _gridAgents.Columns.Add("LastAction", "Last Action");
            _gridAgents.Columns.Add("UpdatedAt", "Updated");
            GridTheme.Apply(_gridAgents, fillWidth: true, alternateRows: true, rowHeight: 30);
            GridTheme.ShowEmptyState(_gridAgents, "No agents yet.", "Run init_db.py to seed the 8 Dev Team agents.");
        }

        private void BuildTasksGrid()
        {
            _gridTasks.Columns.Add("Id", "ID");
            _gridTasks.Columns.Add("Description", "Description");
            _gridTasks.Columns.Add("Status", "Status");
            _gridTasks.Columns.Add("HumanReview", "Human Review");
            _gridTasks.Columns.Add("BuildStatus", "Build");
            _gridTasks.Columns.Add("TestStatus", "Test");
            _gridTasks.Columns.Add("UpdatedAt", "Updated");
            GridTheme.Apply(_gridTasks, fillWidth: true, alternateRows: true, rowHeight: 30);
            GridTheme.ShowEmptyState(_gridTasks, "No tasks yet.", "Use New Task to send work to the Dev Team.");
        }

        // ---------------------------------------------------------------
        // Service events / refresh
        // ---------------------------------------------------------------

        private void ServiceProgressChanged(object sender, DevTeamBrainProgressEventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => Bind(e.Snapshot)));
                return;
            }

            Bind(e.Snapshot);
        }

        private void RefreshNow()
        {
            try
            {
                Bind(_service.GetSnapshot());
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamDashboardForm.RefreshNow");
            }
        }

        private void Bind(DevTeamBrainSnapshot snapshot)
        {
            if (snapshot == null || IsDisposed)
                return;

            _snapshot = snapshot;

            int working = snapshot.Agents.Count(a => a.Status != "idle");
            int running = snapshot.Tasks.Count(t => t.Status == "running" || t.Status == "pending");
            _lblStatus.Text = $"{snapshot.Agents.Count} agents | {working} active | {snapshot.Tasks.Count} tasks | {running} running";

            int? previouslySelected = _selectedTaskId;

            _gridAgents.Rows.Clear();
            foreach (DevTeamAgentStatus agent in snapshot.Agents)
            {
                _gridAgents.Rows.Add(
                    agent.Name,
                    agent.Role,
                    agent.Status,
                    agent.CurrentTaskId.HasValue ? agent.CurrentTaskId.Value.ToString() : "-",
                    agent.LastAction ?? "-",
                    agent.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"));
            }

            _gridTasks.Rows.Clear();
            foreach (DevTeamTask task in snapshot.Tasks)
            {
                int rowIndex = _gridTasks.Rows.Add(
                    task.Id,
                    task.Description,
                    task.Status,
                    task.RequiresHumanReview ? "Yes" : "No",
                    task.BuildStatus,
                    task.TestStatus,
                    task.UpdatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"));

                if (previouslySelected.HasValue && task.Id == previouslySelected.Value)
                    _gridTasks.Rows[rowIndex].Selected = true;
            }

            UpdateActionButtons();

            if (_selectedTaskId.HasValue)
                LoadTaskDetail(_selectedTaskId.Value);
        }

        private void OnTaskSelectionChanged()
        {
            DevTeamTask task = SelectedTask();
            _selectedTaskId = task?.Id;
            UpdateActionButtons();
            if (_selectedTaskId.HasValue)
                LoadTaskDetail(_selectedTaskId.Value);
            else
            {
                _stepsList.Items.Clear();
                _logList.Items.Clear();
                _filesList.Items.Clear();
                _testsList.Items.Clear();
            }
        }

        private DevTeamTask SelectedTask()
        {
            if (_gridTasks.SelectedRows.Count == 0 || _snapshot == null)
                return null;

            object idValue = _gridTasks.SelectedRows[0].Cells["Id"].Value;
            if (idValue == null || !int.TryParse(idValue.ToString(), out int id))
                return null;

            return _snapshot.Tasks.FirstOrDefault(t => t.Id == id);
        }

        private void UpdateActionButtons()
        {
            DevTeamTask task = SelectedTask();
            _btnRetry.Enabled = task != null;
            _btnStop.Enabled = task != null && (task.Status == "running" || task.Status == "pending");
            _btnOpenReport.Enabled = task != null && !string.IsNullOrWhiteSpace(task.ReportPath);
        }

        private void LoadTaskDetail(int taskId)
        {
            try
            {
                DevTeamTaskDetail detail = _service.GetTaskDetail(taskId);

                _stepsList.BeginUpdate();
                _stepsList.Items.Clear();
                foreach (DevTeamTaskStep step in detail.Steps)
                {
                    _stepsList.Items.Add(
                        $"#{step.StepOrder} {step.AgentName} - {step.Action} - {step.Status}" +
                        (string.IsNullOrWhiteSpace(step.Summary) ? "" : $"\r\n    {Truncate(step.Summary, 400)}"));
                }
                _stepsList.EndUpdate();

                _logList.BeginUpdate();
                _logList.Items.Clear();
                foreach (DevTeamLogEntry log in detail.Logs)
                {
                    _logList.Items.Add($"[{log.CreatedAt.ToLocalTime():HH:mm:ss}] [{log.AgentName}] [{log.Level}] {log.Message}");
                }
                _logList.EndUpdate();

                _filesList.BeginUpdate();
                _filesList.Items.Clear();
                foreach (DevTeamFileChange fc in detail.FileChanges)
                {
                    _filesList.Items.Add($"{fc.ChangeType} {fc.FilePath} (staged: {fc.StagedPath ?? "-"}, applied: {fc.Applied})");
                }
                _filesList.EndUpdate();

                _testsList.BeginUpdate();
                _testsList.Items.Clear();
                foreach (DevTeamTestResult tr in detail.TestResults)
                {
                    _testsList.Items.Add($"{tr.Kind}: {(tr.Passed ? "pass" : "fail")} - {Truncate(tr.OutputSummary, 300)}");
                }
                _testsList.EndUpdate();
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamDashboardForm.LoadTaskDetail");
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        // ---------------------------------------------------------------
        // Actions
        // ---------------------------------------------------------------

        private void SubmitNewTask()
        {
            if (!_service.DatabaseExists)
            {
                MessageBox.Show(this,
                    "ServoERP Brain database not found. Run 'python ServoERPBrain/memory/init_db.py' once to initialize it.",
                    "Dev Team Dashboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (NewTaskDialog dialog = new NewTaskDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string description = dialog.TaskDescription;
                if (string.IsNullOrWhiteSpace(description))
                    return;

                try
                {
                    int taskId = _service.SubmitTask(description);
                    _selectedTaskId = taskId;
                    RefreshNow();
                }
                catch (Exception ex)
                {
                    ExceptionLogger.Log(ex, "DevTeamDashboardForm.SubmitNewTask");
                    MessageBox.Show(this, "Failed to submit task:\r\n" + ex.Message, "Dev Team Dashboard",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void RetrySelectedTask()
        {
            DevTeamTask task = SelectedTask();
            if (task == null)
                return;

            try
            {
                int newTaskId = _service.RetryTask(task.Id);
                _selectedTaskId = newTaskId;
                RefreshNow();
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamDashboardForm.RetrySelectedTask");
                MessageBox.Show(this, "Failed to retry task:\r\n" + ex.Message, "Dev Team Dashboard",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopSelectedTask()
        {
            DevTeamTask task = SelectedTask();
            if (task == null)
                return;

            try
            {
                _service.StopTask(task.Id);
                RefreshNow();
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamDashboardForm.StopSelectedTask");
            }
        }

        private void OpenSelectedReport()
        {
            DevTeamTask task = SelectedTask();
            if (task == null || string.IsNullOrWhiteSpace(task.ReportPath))
                return;

            try
            {
                string fullPath = _service.GetReportFullPath(task.ReportPath);
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                    Process.Start("notepad.exe", fullPath);
                else
                    MessageBox.Show(this, "Report file not found:\r\n" + fullPath, "Dev Team Dashboard",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                ExceptionLogger.Log(ex, "DevTeamDashboardForm.OpenSelectedReport");
                MessageBox.Show(this, "Unable to open report:\r\n" + ex.Message, "Dev Team Dashboard",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Small modal dialog for entering a new task description.</summary>
        private sealed class NewTaskDialog : Form
        {
            private readonly TextBox _text;

            public string TaskDescription => _text.Text.Trim();

            public NewTaskDialog()
            {
                Text = "New Dev Team Task";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(480, 220);
                BackColor = DS.White;
                Font = new Font("Segoe UI", 9f);

                Label label = new Label
                {
                    Text = "Describe the task for the Dev Team (Product Manager will route it to the right agents):",
                    Location = new Point(16, 14),
                    Size = new Size(448, 40),
                    ForeColor = DS.Slate700
                };

                _text = new TextBox
                {
                    Multiline = true,
                    Location = new Point(16, 56),
                    Size = new Size(448, 110),
                    ScrollBars = ScrollBars.Vertical,
                    AcceptsReturn = true,
                    BorderStyle = BorderStyle.FixedSingle
                };

                Button ok = DS.PrimaryBtn("Submit", 100, 34);
                ok.Location = new Point(280, 176);
                ok.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(_text.Text))
                    {
                        MessageBox.Show(this, "Please enter a task description.", "New Dev Team Task",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    DialogResult = DialogResult.OK;
                };

                Button cancel = DS.GhostBtn("Cancel", 90, 34);
                cancel.Location = new Point(384, 176);
                cancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

                Controls.Add(label);
                Controls.Add(_text);
                Controls.Add(ok);
                Controls.Add(cancel);

                AcceptButton = null;
                CancelButton = cancel;
            }
        }
    }
}
