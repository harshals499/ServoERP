using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class AgentSimulationPanel : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly AgentSimulationService _service = AgentSimulationService.Instance;
        private Label _lblTitle;
        private Label _lblStatus;
        private Label _lblScore;
        private Label _lblCoverage;
        private Label _lblRecords;
        private Label _lblIssues;
        private Label _lblApproach;
        private ProgressBar _progress;
        private ListBox _notes;
        private Button _btnStart;
        private Button _btnPause;
        private Button _btnResume;
        private Button _btnRunDay;
        private Button _btnReport;
        private Button _btnCleanup;

        public AgentSimulationPanel()
        {
            Text = "ServoERP Agent Simulation";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 620);
            MinimumSize = new Size(720, 520);
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", 9f);
            BuildLayout();
            _service.ProgressChanged += ServiceProgressChanged;
            _service.Completed += ServiceCompleted;
            FormClosed += (s, e) =>
            {
                _service.ProgressChanged -= ServiceProgressChanged;
                _service.Completed -= ServiceCompleted;
            };
            Bind(_service.CurrentState);
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = DS.White, Padding = new Padding(20, 12, 20, 0) };
            _lblTitle = new Label
            {
                Text = "Agent Simulation",
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
            _btnStart = DS.PrimaryBtn("Run Agent Simulation", 176, 34);
            _btnPause = DS.GhostBtn("Pause", 86, 34);
            _btnResume = DS.GhostBtn("Resume", 96, 34);
            _btnRunDay = DS.GhostBtn("Run 1 Day", 104, 34);
            _btnReport = DS.GhostBtn("Open Report", 118, 34);
            _btnCleanup = DS.DangerBtn("Delete Agent Data", 148, 34);
            _btnStart.Click += (s, e) => _service.StartOrResume();
            _btnPause.Click += (s, e) => _service.Pause();
            _btnResume.Click += (s, e) => _service.Resume();
            _btnRunDay.Click += (s, e) => _service.RunNextDay();
            _btnReport.Click += (s, e) => OpenReport();
            _btnCleanup.Click += (s, e) => ConfirmCleanup();
            actions.Controls.AddRange(new Control[] { _btnStart, _btnPause, _btnResume, _btnRunDay, _btnReport, _btnCleanup });

            TableLayoutPanel metrics = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 120,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(20, 8, 20, 8),
                BackColor = DS.BgPage
            };
            for (int i = 0; i < 4; i++)
                metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            _lblScore = Metric(metrics, "Score", 0);
            _lblCoverage = Metric(metrics, "Coverage", 1);
            _lblRecords = Metric(metrics, "Records", 2);
            _lblIssues = Metric(metrics, "Issues", 3);

            Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20), BackColor = DS.BgPage };
            _progress = new ProgressBar { Dock = DockStyle.Top, Height = 18, Minimum = 0, Maximum = 60 };
            _lblApproach = new Label
            {
                Dock = DockStyle.Top,
                Height = 54,
                ForeColor = DS.Slate600,
                Padding = new Padding(0, 12, 0, 0)
            };
            _notes = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                IntegralHeight = false
            };
            body.Controls.Add(_notes);
            body.Controls.Add(_lblApproach);
            body.Controls.Add(_progress);

            Controls.Add(body);
            Controls.Add(metrics);
            Controls.Add(actions);
            Controls.Add(header);
        }

        private Label Metric(TableLayoutPanel host, string title, int column)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DS.White,
                Margin = new Padding(0, 0, 12, 0),
                Padding = new Padding(14)
            };
            card.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            Label labelTitle = new Label
            {
                Text = title.ToUpperInvariant(),
                Dock = DockStyle.Top,
                Height = 22,
                Font = DS.SmallBold,
                ForeColor = DS.Slate500
            };
            Label value = new Label
            {
                Text = "-",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = DS.Primary600
            };
            card.Controls.Add(value);
            card.Controls.Add(labelTitle);
            host.Controls.Add(card, column, 0);
            return value;
        }

        private void ServiceProgressChanged(object sender, AgentSimulationProgressEventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => Bind(e.State)));
                return;
            }

            Bind(e.State);
        }

        private void ServiceCompleted(object sender, AgentSimulationCompletedEventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => ShowCompletion(e)));
                return;
            }

            ShowCompletion(e);
        }

        private void Bind(AgentSimulationState state)
        {
            if (state == null)
                return;

            _lblStatus.Text = "[AGENT SIM] Day " + state.SimulatedDay + "/" + state.MaxDays
                + " | " + state.SimulatedDate.ToString("dd/MM/yyyy")
                + " | Quotations " + Count(state, "Quotations")
                + " | Invoices " + Count(state, "Invoices")
                + " | Payments " + Count(state, "Payments")
                + " | Purchases " + Count(state, "Purchases")
                + " | Score " + state.Score;
            _lblScore.Text = state.Score + "/100";
            _lblCoverage.Text = state.Checklist.Count(kv => kv.Value) + "/" + state.Checklist.Count;
            _lblRecords.Text = state.Records.Count.ToString("N0");
            _lblIssues.Text = state.Issues.Count.ToString("N0");
            _lblApproach.Text = state.Approach;
            _progress.Value = Math.Max(_progress.Minimum, Math.Min(_progress.Maximum, state.SimulatedDay));
            _notes.BeginUpdate();
            _notes.Items.Clear();
            foreach (AgentSimulationNote note in state.Notes.OrderByDescending(n => n.LoggedAt).Take(120))
                _notes.Items.Add("Day " + note.SimulatedDay + " | " + note.Category + " | " + note.Message);
            foreach (AgentSimulationIssue issue in state.Issues.OrderByDescending(i => i.LoggedAt).Take(30))
                _notes.Items.Insert(0, "ISSUE " + issue.Severity + " | " + issue.Category + " | " + issue.Message);
            _notes.EndUpdate();

            _btnPause.Enabled = state.IsRunning && !state.IsPaused && !state.IsCompleted;
            _btnResume.Enabled = state.IsPaused && !state.IsCompleted;
            _btnRunDay.Enabled = state.IsRunning && !state.IsPaused && !state.IsCompleted;
            _btnReport.Enabled = state.Records.Count > 0 || state.IsCompleted;
            _btnCleanup.Enabled = state.Records.Count > 0;
        }

        private void ShowCompletion(AgentSimulationCompletedEventArgs e)
        {
            Bind(e.State);
            DialogResult keep = MessageBox.Show(
                "Agent simulation completed.\r\n\r\nReport:\r\n" + e.ReportPath + "\r\n\r\nKeep the [AGENT] simulation records for inspection?",
                "Agent Simulation Complete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
            if (keep == DialogResult.No)
                _service.DeleteCreatedAgentData();
        }

        private void OpenReport()
        {
            try
            {
                string path = _service.BuildLatestReport();
                if (File.Exists(path))
                    System.Diagnostics.Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open report:\r\n" + ex.Message, "Agent Simulation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ConfirmCleanup()
        {
            DialogResult confirm = MessageBox.Show(
                "Delete only the [AGENT] records tracked in AgentState.json?\r\n\r\nReal client records are not touched.",
                "Delete Agent Data",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm == DialogResult.Yes)
                _service.DeleteCreatedAgentData();
        }

        private static int Count(AgentSimulationState state, string module)
        {
            return state == null || state.Records == null
                ? 0
                : state.Records.Where(r => string.Equals(r.Module, module, StringComparison.OrdinalIgnoreCase)).Count();
        }
    }
}

