using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Marks one scheduled AMC visit as completed.</summary>
    public partial class MarkVisitCompleteForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly int _visitId;
        private DateTimePicker _completedDate;
        private TextBox _technician;
        private TextBox _workDone;
        private TextBox _partsUsed;
        private Button _save;

        public MarkVisitCompleteForm(int visitId)
        {
            _visitId = visitId;
            InitializeComponent();
            Text = "Mark Visit Complete";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(430, 340);
            BackColor = DS.BgPage;
            BuildLayout();
        }

        /// <summary>Builds the visit completion dialog.</summary>
        private void BuildLayout()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(18), BackColor = DS.BgPage };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            _completedDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Value = DateTime.Today };
            _technician = new TextBox();
            _workDone = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            _partsUsed = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            AddRow(grid, 0, "Completed Date", _completedDate);
            AddRow(grid, 1, "Technician Name", _technician);
            AddRow(grid, 2, "Work Done", _workDone);
            AddRow(grid, 3, "Parts Used (leave blank if Non-Comprehensive)", _partsUsed);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = DS.BgPage };
            _save = MakeButton("Save", DS.Primary600, Color.White);
            Button cancel = MakeButton("Cancel", Color.White, DS.Slate900);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _save.Click += (s, e) => SaveVisit();
            buttons.Controls.Add(_save);
            buttons.Controls.Add(cancel);
            grid.Controls.Add(buttons, 0, 4);
            grid.SetColumnSpan(buttons, 2);
            Controls.Add(grid);
            UIHelper.ApplyInputStyles(Controls);
        }

        /// <summary>Adds one row to the form grid.</summary>
        private void AddRow(TableLayoutPanel grid, int row, string label, Control editor)
        {
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true }, 0, row);
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 4, 0, 4);
            grid.Controls.Add(editor, 1, row);
        }

        /// <summary>Saves visit completion using a BackgroundWorker.</summary>
        private void SaveVisit()
        {
            if (string.IsNullOrWhiteSpace(_technician.Text) || string.IsNullOrWhiteSpace(_workDone.Text))
            {
                MessageBox.Show("Enter Technician Name and Work Done.", BrandingService.WindowTitle("AMC Visit"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _save.Enabled = false;
            var worker = CreateWorker();
            worker.DoWork += (s, e) => UpdateVisit();
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    RunOnUI(() => _save.Enabled = true);
                    ShowError( "Visit could not be marked complete. Please try again.", e.Error);
                    return;
                }
                if (e.Cancelled) return;

                RunOnUI(() =>
                {
                    _save.Enabled = true;
                    DialogResult = DialogResult.OK;
                    Close();
                });
            };
            worker.RunWorkerAsync();
        }

        /// <summary>Updates the visit row with completion data.</summary>
        private void UpdateVisit()
        {
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            using (SqlCommand command = new SqlCommand(@"
UPDATE AMCVisits
SET CompletedDate = @CompletedDate,
    TechnicianName = @TechnicianName,
    WorkDone = @WorkDone,
    PartsUsed = @PartsUsed,
    Status = 'Completed',
    UpdatedAt = GETDATE()
WHERE VisitID = @VisitID;", connection))
            {
                command.Parameters.AddWithValue("@CompletedDate", _completedDate.Value.Date);
                command.Parameters.AddWithValue("@TechnicianName", _technician.Text.Trim());
                command.Parameters.AddWithValue("@WorkDone", _workDone.Text.Trim());
                command.Parameters.AddWithValue("@PartsUsed", string.IsNullOrWhiteSpace(_partsUsed.Text) ? (object)DBNull.Value : _partsUsed.Text.Trim());
                command.Parameters.AddWithValue("@VisitID", _visitId);
                DatabaseConnectionFactory.Open(connection, "MarkVisitCompleteForm.UpdateVisit");
                if (command.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("Visit was not found.");
            }
        }

        /// <summary>Creates a compact dialog button.</summary>
        private Button MakeButton(string text, Color back, Color fore)
        {
            var button = new Button { Text = text, Width = 92, Height = 32, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat };
            button.FlatAppearance.BorderSize = back == Color.White ? 1 : 0;
            button.FlatAppearance.BorderColor = DS.Border;
            DS.Rounded(button, 6);
            return button;
        }
    }
}


