using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class JobWorkflowBoardForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly JobService _jobService = new JobService();
        private FlowLayoutPanel _board;
        private Label _status;
        private List<JobSummaryDto> _jobs = new List<JobSummaryDto>();

        /// <summary>Initializes the visual job workflow board.</summary>
        public JobWorkflowBoardForm()
        {
            BuildLayout();
            LoadJobs();
        }

        private void BuildLayout()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DS.BgPage;
            ClientSize = new Size(1180, 720);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "ServoERP - Jobs Workflow Board";

            Controls.Add(new Label { Text = "Jobs Workflow Board", Location = new Point(24, 18), Size = new Size(460, 34), Font = DS.H1, ForeColor = DS.Slate900 });
            Controls.Add(new Label { Text = "Kanban-style dispatch review for field-service work across the complete job pipeline.", Location = new Point(26, 52), Size = new Size(860, 28), Font = DS.Body, ForeColor = DS.Slate600 });

            Button refresh = Button("Refresh", DS.Primary600, Color.White, 96);
            refresh.Location = new Point(850, 24);
            refresh.Click += (s, e) => LoadJobs();
            Controls.Add(refresh);

            Button copy = Button("Copy Snapshot", DS.Green600, Color.White, 132);
            copy.Location = new Point(956, 24);
            copy.Click += (s, e) => CopySnapshot();
            Controls.Add(copy);

            _board = new FlowLayoutPanel
            {
                Location = new Point(24, 96),
                Size = new Size(1132, 560),
                BackColor = DS.BgPage,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight
            };
            Controls.Add(_board);

            _status = new Label { Text = "Ready.", Location = new Point(24, 668), Size = new Size(900, 24), Font = DS.Small, ForeColor = DS.Slate600 };
            Controls.Add(_status);

            Button close = Button("Close", Color.White, DS.Slate700, 96);
            close.FlatAppearance.BorderColor = DS.Border;
            close.FlatAppearance.BorderSize = 1;
            close.Location = new Point(1060, 664);
            close.Click += (s, e) => Close();
            Controls.Add(close);
        }

        private void LoadJobs()
        {
            try
            {
                _jobs = _jobService.GetAllJobsWithSummary() ?? new List<JobSummaryDto>();
                RenderBoard();
                _status.Text = "Loaded " + _jobs.Count + " job(s).";
                _status.ForeColor = DS.Green600;
            }
            catch (Exception ex)
            {
                _status.Text = "Could not load jobs: " + ex.Message;
                _status.ForeColor = DS.Red600;
            }
        }

        private void RenderBoard()
        {
            _board.SuspendLayout();
            _board.Controls.Clear();
            string[] stages = { "Created", "Assigned", "In Progress", "Checklist Done", "Closed", "Invoiced" };
            foreach (string stage in stages)
                _board.Controls.Add(BuildColumn(stage, JobsForStage(stage).ToList()));
            _board.ResumeLayout();
        }

        private Panel BuildColumn(string title, List<JobSummaryDto> jobs)
        {
            Panel column = new Panel { Size = new Size(178, 540), BackColor = Color.White, Margin = new Padding(0, 0, 12, 0) };
            column.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, column.Width - 1, column.Height - 1);
            };
            column.Controls.Add(new Label { Text = title + " (" + jobs.Count + ")", Location = new Point(12, 10), Size = new Size(150, 24), Font = DS.BodyBold, ForeColor = DS.Slate900, AutoEllipsis = true });
            FlowLayoutPanel list = new FlowLayoutPanel { Location = new Point(10, 42), Size = new Size(158, 486), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.White };
            foreach (JobSummaryDto job in jobs.Take(20))
                list.Controls.Add(BuildJobCard(job));
            if (jobs.Count == 0)
                list.Controls.Add(new Label { Text = "No jobs", Size = new Size(138, 34), Font = DS.Small, ForeColor = DS.Slate500, TextAlign = ContentAlignment.MiddleCenter });
            column.Controls.Add(list);
            return column;
        }

        private Control BuildJobCard(JobSummaryDto job)
        {
            Panel card = new Panel { Size = new Size(138, 92), BackColor = job.IsOverdue ? DS.Red50 : DS.Slate50, Margin = new Padding(0, 0, 0, 8) };
            card.Controls.Add(new Label { Text = job.JobNumber, Location = new Point(8, 6), Size = new Size(122, 16), Font = new Font("Consolas", 7.5f, FontStyle.Bold), ForeColor = DS.Primary700, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = First(job.JobTitle, "Untitled job"), Location = new Point(8, 24), Size = new Size(122, 28), Font = DS.SmallBold, ForeColor = DS.Slate900, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = First(job.ClientName, "No client"), Location = new Point(8, 54), Size = new Size(122, 16), Font = DS.Small, ForeColor = DS.Slate600, AutoEllipsis = true });
            card.Controls.Add(new Label { Text = IndiaFormatHelper.FormatDate(job.ScheduledDate), Location = new Point(8, 72), Size = new Size(122, 16), Font = DS.Small, ForeColor = job.IsOverdue ? DS.Red600 : DS.Slate500, AutoEllipsis = true });
            return card;
        }

        private IEnumerable<JobSummaryDto> JobsForStage(string stage)
        {
            string normalized = Normalize(stage);
            return _jobs.Where(j => Normalize(j.PipelineStatus) == normalized).OrderBy(j => j.ScheduledDate);
        }

        private void CopySnapshot()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("ServoERP Jobs Workflow Board");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            foreach (string stage in new[] { "Created", "Assigned", "In Progress", "Checklist Done", "Closed", "Invoiced" })
                builder.AppendLine(stage + ": " + JobsForStage(stage).Count());

            if (UIHelper.TrySetClipboardText(this, builder.ToString(), BrandingService.WindowTitle("Jobs Workflow Board")))
            {
                _status.Text = "Workflow snapshot copied.";
                _status.ForeColor = DS.Green600;
            }
        }

        private static string Normalize(string status)
        {
            string value = (status ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
            if (value == "INPROGRESS") return "InProgress";
            if (value == "CHECKLISTDONE") return "ChecklistDone";
            if (value == "CLOSED" || value == "COMPLETED") return "Closed";
            if (value == "INVOICED") return "Invoiced";
            if (value == "ASSIGNED") return "Assigned";
            return "Created";
        }

        private static string First(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static Button Button(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button { Text = text, Size = new Size(width, 34), BackColor = backColor, ForeColor = foreColor, FlatStyle = FlatStyle.Flat, Font = DS.BodyBold, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
            return button;
        }
    }
}

