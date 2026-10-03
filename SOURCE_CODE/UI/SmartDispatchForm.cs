using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Explainable, human-confirmed technician recommendation workspace.</summary>
    public sealed class SmartDispatchForm : ServoFormBase
    {
        private readonly SmartDispatchService _dispatchService = new SmartDispatchService();
        private readonly JobService _jobService = new JobService();
        private readonly int? _initialJobId;
        private ComboBox _jobPicker;
        private Button _refreshButton;
        private Button _assignButton;
        private Label _jobContext;
        private Label _readiness;
        private Label _status;
        private DataGridView _recommendations;
        private RichTextBox _explanation;
        private SmartDispatchPlan _plan;
        private bool _loading;

        public event EventHandler AssignmentCompleted;

        public SmartDispatchForm() : this(null)
        {
        }

        public SmartDispatchForm(int? initialJobId)
        {
            _initialJobId = initialJobId;
            BuildLayout();
            Shown += async (s, e) => await LoadJobsAsync();
        }

        private void BuildLayout()
        {
            Text = BrandingService.WindowTitle("Smart Dispatch");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 760);
            MinimumSize = new Size(980, 640);
            BackColor = DS.BgPage;
            Font = DS.Body;
            AutoScaleMode = AutoScaleMode.Dpi;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = DS.White, Padding = new Padding(24, 16, 24, 12) };
            header.Controls.Add(new Label
            {
                Text = "Smart Dispatch",
                Location = new Point(24, 14),
                Size = new Size(300, 30),
                Font = DS.H1,
                ForeColor = DS.Slate900
            });
            header.Controls.Add(new Label
            {
                Text = "Compare explainable technician recommendations. ServoERP never assigns automatically.",
                Location = new Point(26, 46),
                Size = new Size(620, 24),
                Font = DS.Body,
                ForeColor = DS.Slate600
            });

            _jobPicker = new ComboBox
            {
                Name = "SmartDispatchJobPicker",
                Location = new Point(650, 22),
                Size = new Size(300, 34),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = DS.Body
            };
            _jobPicker.SelectedIndexChanged += async (s, e) => await LoadPlanAsync();
            header.Controls.Add(_jobPicker);

            _refreshButton = MakeButton("Refresh", DS.White, DS.Primary700, 92);
            _refreshButton.Location = new Point(964, 22);
            _refreshButton.Click += async (s, e) => await LoadJobsAsync();
            header.Controls.Add(_refreshButton);

            _jobContext = new Label
            {
                Text = "Select an open job.",
                Location = new Point(26, 80),
                Size = new Size(760, 22),
                Font = DS.SmallBold,
                ForeColor = DS.Slate800,
                AutoEllipsis = true
            };
            header.Controls.Add(_jobContext);
            _readiness = new Label
            {
                Location = new Point(794, 80),
                Size = new Size(320, 22),
                Font = DS.Small,
                ForeColor = DS.Slate600,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            };
            header.Controls.Add(_readiness);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 760,
                SplitterWidth = 6,
                BackColor = DS.BgPage,
                Padding = new Padding(20, 16, 20, 8)
            };

            Panel gridCard = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(14) };
            gridCard.Controls.Add(new Label
            {
                Text = "Recommended technicians",
                Dock = DockStyle.Top,
                Height = 34,
                Font = DS.H3,
                ForeColor = DS.Slate900
            });
            _recommendations = new DataGridView
            {
                Name = "SmartDispatchRecommendationsGrid",
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = DS.White,
                BorderStyle = BorderStyle.None
            };
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Rank", HeaderText = "#", Width = 42 });
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmployeeName", HeaderText = "Technician", FillWeight = 145, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Role", HeaderText = "Role", FillWeight = 105, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Score", HeaderText = "Fit", Width = 58 });
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Availability", HeaderText = "Availability", Width = 108 });
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Workload", HeaderText = "Workload", Width = 124 });
            _recommendations.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Distance", HeaderText = "Distance", Width = 92 });
            GridTheme.Apply(_recommendations, fillWidth: false, alternateRows: true, rowHeight: 38);
            _recommendations.SelectionChanged += (s, e) => ShowSelectedExplanation();
            _recommendations.CellFormatting += RecommendationCellFormatting;
            gridCard.Controls.Add(_recommendations);
            gridCard.Controls.SetChildIndex(_recommendations, 1);
            split.Panel1.Controls.Add(gridCard);

            Panel detailCard = new Panel { Dock = DockStyle.Fill, BackColor = DS.White, Padding = new Padding(16) };
            detailCard.Controls.Add(new Label
            {
                Text = "Why this recommendation",
                Dock = DockStyle.Top,
                Height = 34,
                Font = DS.H3,
                ForeColor = DS.Slate900
            });
            _explanation = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = DS.White,
                ForeColor = DS.Slate700,
                Font = DS.Body,
                DetectUrls = false,
                TabStop = false
            };
            detailCard.Controls.Add(_explanation);
            detailCard.Controls.SetChildIndex(_explanation, 1);
            split.Panel2.Controls.Add(detailCard);

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = DS.White, Padding = new Padding(24, 16, 24, 12) };
            _status = new Label
            {
                Text = "Ready.",
                Location = new Point(24, 24),
                Size = new Size(680, 24),
                Font = DS.Small,
                ForeColor = DS.Slate600,
                AutoEllipsis = true
            };
            footer.Controls.Add(_status);
            _assignButton = MakeButton("Assign selected", DS.Primary600, Color.White, 150);
            _assignButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _assignButton.Location = new Point(850, 16);
            _assignButton.Enabled = false;
            _assignButton.Click += async (s, e) => await AssignSelectedAsync();
            footer.Controls.Add(_assignButton);
            Button close = MakeButton("Close", DS.White, DS.Slate700, 100);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(1012, 16);
            close.Click += (s, e) => Close();
            footer.Controls.Add(close);
            footer.Resize += (s, e) =>
            {
                close.Left = footer.ClientSize.Width - close.Width - 24;
                _assignButton.Left = close.Left - _assignButton.Width - 12;
            };

            Controls.Add(split);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private async Task LoadJobsAsync()
        {
            if (_loading) return;
            bool loadPlan = false;
            SetBusy(true, "Loading dispatch work...");
            try
            {
                List<JobSummaryDto> jobs = await Task.Run(() => _jobService.GetAllJobsWithSummary() ?? new List<JobSummaryDto>());
                List<DispatchJobItem> choices = jobs
                    .Where(j => !IsTerminal(j.PipelineStatus))
                    .OrderBy(j => j.TechnicianId.HasValue)
                    .ThenByDescending(j => j.IsOverdue)
                    .ThenBy(j => j.ScheduledDate)
                    .Select(j => new DispatchJobItem(j))
                    .ToList();

                int? preferred = _initialJobId ?? SelectedJobId();
                _jobPicker.BeginUpdate();
                _jobPicker.Items.Clear();
                foreach (DispatchJobItem item in choices) _jobPicker.Items.Add(item);
                _jobPicker.EndUpdate();

                if (choices.Count == 0)
                {
                    _jobContext.Text = "No open jobs are available for dispatch.";
                    _readiness.Text = string.Empty;
                    BindRecommendations(new List<SmartDispatchRecommendation>());
                    return;
                }

                int selectedIndex = preferred.HasValue ? choices.FindIndex(j => j.JobId == preferred.Value) : 0;
                _jobPicker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
                loadPlan = _jobPicker.SelectedIndex >= 0;
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Smart Dispatch"), "Loading dispatch recommendations", ex);
                SetStatus("Could not load dispatch recommendations.", DS.Red600);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
            if (loadPlan)
                await LoadPlanAsync();
        }

        private async Task LoadPlanAsync()
        {
            int? jobId = SelectedJobId();
            if (!jobId.HasValue || _loading) return;
            SetBusy(true, "Ranking technicians...");
            try
            {
                _plan = await Task.Run(() => _dispatchService.BuildPlan(jobId.Value));
                Job job = _plan.Job;
                _jobContext.Text = First(job.JobNumber, "Job #" + job.JobID) + " | " + First(job.JobTitle, "Untitled job") + " | " +
                                   First(job.ClientName, "Client") + " / " + First(job.SiteName, "No site") + " | " + _plan.Urgency;
                _readiness.Text = _plan.ReadinessSummary;
                BindRecommendations(_plan.Recommendations);
                string message = "Ranked " + _plan.Recommendations.Count + " active technician(s).";
                if (_plan.Warnings.Count > 0) message += " " + _plan.Warnings[0];
                SetStatus(message, _plan.Warnings.Count > 0 ? DS.Amber600 : DS.Green600);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Smart Dispatch"), "Ranking technicians", ex);
                BindRecommendations(new List<SmartDispatchRecommendation>());
                SetStatus("Recommendation failed. No assignment was changed.", DS.Red600);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
        }

        private void BindRecommendations(List<SmartDispatchRecommendation> items)
        {
            _recommendations.DataSource = null;
            _recommendations.DataSource = items ?? new List<SmartDispatchRecommendation>();
            if (_recommendations.Rows.Count > 0)
            {
                _recommendations.ClearSelection();
                _recommendations.Rows[0].Selected = true;
                _recommendations.CurrentCell = _recommendations.Rows[0].Cells[1];
            }
            ShowSelectedExplanation();
        }

        private void ShowSelectedExplanation()
        {
            SmartDispatchRecommendation recommendation = SelectedRecommendation();
            _assignButton.Enabled = !_loading && recommendation != null && recommendation.CanAssign;
            if (recommendation == null)
            {
                _explanation.Text = "Select a technician to see the score explanation.";
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine(recommendation.EmployeeName);
            builder.AppendLine(recommendation.Role + " | " + recommendation.Score + "/100 " + recommendation.Fit + " fit");
            builder.AppendLine();
            foreach (string reason in recommendation.Reasons) builder.AppendLine("• " + reason);
            if (recommendation.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Review before assigning:");
                foreach (string warning in recommendation.Warnings) builder.AppendLine("• " + warning);
            }
            if (_plan != null && _plan.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Job readiness:");
                foreach (string warning in _plan.Warnings) builder.AppendLine("• " + warning);
            }
            builder.AppendLine();
            builder.AppendLine("The score is decision support, not an automatic assignment.");
            _explanation.Text = builder.ToString();
        }

        private async Task AssignSelectedAsync()
        {
            SmartDispatchRecommendation recommendation = SelectedRecommendation();
            int? jobId = SelectedJobId();
            if (recommendation == null || !jobId.HasValue || _plan == null) return;

            string jobNumber = First(_plan.Job.JobNumber, "Job #" + _plan.Job.JobID);
            bool confirmed = ServoConfirmDialog.Show(this,
                "Assign " + recommendation.EmployeeName + " to " + jobNumber + "?",
                "Fit score: " + recommendation.Score + "/100. Workload: " + recommendation.Workload + ". " +
                "This updates the job assignment and moves a Created job to Assigned. No other records are changed.");
            if (!confirmed) return;

            SetBusy(true, "Assigning technician...");
            bool assigned = false;
            try
            {
                await Task.Run(() => _dispatchService.Assign(jobId.Value, recommendation.EmployeeId, recommendation.Score));
                SetStatus(recommendation.EmployeeName + " assigned to " + jobNumber + ".", DS.Green600);
                AssignmentCompleted?.Invoke(this, EventArgs.Empty);
                assigned = true;
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Smart Dispatch"), "Assigning technician", ex);
                SetStatus("Assignment failed. The job was not changed.", DS.Red600);
            }
            finally
            {
                SetBusy(false, _status.Text);
            }
            if (assigned)
                await LoadJobsAsync();
        }

        private void RecommendationCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            if (_recommendations.Rows[e.RowIndex].DataBoundItem is SmartDispatchRecommendation item)
            {
                if (_recommendations.Columns[e.ColumnIndex].DataPropertyName == "Score")
                {
                    e.Value = item.Score + "%";
                    e.CellStyle.ForeColor = item.Score >= 75 ? DS.Green600 : item.Score >= 60 ? DS.Primary700 : item.Score >= 45 ? DS.Amber600 : DS.Red600;
                    e.CellStyle.Font = DS.BodyBold;
                }
                if (item.OnLeaveToday)
                    e.CellStyle.BackColor = DS.Red50;
            }
        }

        private SmartDispatchRecommendation SelectedRecommendation()
        {
            return _recommendations.CurrentRow == null ? null : _recommendations.CurrentRow.DataBoundItem as SmartDispatchRecommendation;
        }

        private int? SelectedJobId()
        {
            DispatchJobItem item = _jobPicker.SelectedItem as DispatchJobItem;
            return item == null ? (int?)null : item.JobId;
        }

        private void SetBusy(bool busy, string message)
        {
            _loading = busy;
            _jobPicker.Enabled = !busy;
            _refreshButton.Enabled = !busy;
            _assignButton.Enabled = !busy && SelectedRecommendation()?.CanAssign == true;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (!string.IsNullOrWhiteSpace(message)) SetStatus(message, busy ? DS.Primary700 : _status.ForeColor);
        }

        private void SetStatus(string message, Color color)
        {
            _status.Text = message ?? string.Empty;
            _status.ForeColor = color;
        }

        private static bool IsTerminal(string stage)
        {
            string value = (stage ?? string.Empty).Replace(" ", string.Empty);
            return value.Equals("Closed", StringComparison.OrdinalIgnoreCase) || value.Equals("Invoiced", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("Completed", StringComparison.OrdinalIgnoreCase) || value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button
            {
                Text = text,
                Size = new Size(width, 38),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = DS.BodyBold,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = back == DS.White ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.Border;
            return button;
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private sealed class DispatchJobItem
        {
            public DispatchJobItem(JobSummaryDto job)
            {
                JobId = job.JobId;
                string assignment = string.IsNullOrWhiteSpace(job.TechnicianName) ? "Unassigned" : job.TechnicianName;
                Text = First(job.JobNumber, "Job #" + job.JobId) + " — " + First(job.JobTitle, "Untitled job") + " — " + assignment;
            }

            public int JobId { get; private set; }
            public string Text { get; private set; }
            public override string ToString() => Text;
        }
    }
}
