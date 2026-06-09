using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Captures one equipment item covered under an AMC.</summary>
    public partial class AddAMCEquipmentForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly int _amcId;
        private TextBox _name;
        private TextBox _model;
        private TextBox _serial;
        private DateTimePicker _installDate;
        private TextBox _location;
        private TextBox _notes;
        private Button _save;

        public AddAMCEquipmentForm(int amcId)
        {
            _amcId = amcId;
            InitializeComponent();
            Text = "Add AMC Equipment";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(430, 390);
            BackColor = DS.BgPage;
            BuildLayout();
        }

        /// <summary>Builds the equipment entry dialog.</summary>
        private void BuildLayout()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(18), BackColor = DS.BgPage };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 5 ? 70 : 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            _name = new TextBox();
            _model = new TextBox();
            _serial = new TextBox();
            _installDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true, Checked = false };
            _location = new TextBox();
            _notes = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

            AddRow(grid, 0, "Equipment Name", _name);
            AddRow(grid, 1, "Model Number", _model);
            AddRow(grid, 2, "Serial Number", _serial);
            AddRow(grid, 3, "Install Date", _installDate);
            AddRow(grid, 4, "Location", _location);
            AddRow(grid, 5, "Notes", _notes);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = DS.BgPage };
            _save = MakeButton("Save", DS.Primary600, Color.White);
            Button cancel = MakeButton("Cancel", Color.White, DS.Slate900);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            _save.Click += (s, e) => SaveEquipment();
            buttons.Controls.Add(_save);
            buttons.Controls.Add(cancel);
            grid.Controls.Add(buttons, 0, 6);
            grid.SetColumnSpan(buttons, 2);
            Controls.Add(grid);
            UIHelper.ApplyInputStyles(Controls);
        }

        /// <summary>Adds one row to the form grid.</summary>
        private void AddRow(TableLayoutPanel grid, int row, string label, Control editor)
        {
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = DS.Slate900, AutoEllipsis = true }, 0, row);
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 4, 0, 4);
            grid.Controls.Add(editor, 1, row);
        }

        /// <summary>Saves equipment using a BackgroundWorker.</summary>
        private void SaveEquipment()
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                MessageBox.Show("Enter Equipment Name.", BrandingService.WindowTitle("AMC Equipment"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _save.Enabled = false;
            var worker = CreateWorker();
            worker.DoWork += (s, e) => InsertEquipment();
            worker.RunWorkerCompleted += (s, e) =>
            {
                if (e.Error != null)
                {
                    RunOnUI(() => _save.Enabled = true);
                    ShowError( "Equipment could not be saved. Please try again.", e.Error);
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

        /// <summary>Inserts the equipment row with parameterised SQL.</summary>
        private void InsertEquipment()
        {
            using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection())
            using (SqlCommand command = new SqlCommand(@"
INSERT INTO AMCEquipment
    (AMCID, EquipmentName, ModelNumber, SerialNumber, InstallDate, Location, Notes, CreatedAt)
VALUES
    (@AMCID, @EquipmentName, @ModelNumber, @SerialNumber, @InstallDate, @Location, @Notes, GETDATE());", connection))
            {
                command.Parameters.AddWithValue("@AMCID", _amcId);
                command.Parameters.AddWithValue("@EquipmentName", _name.Text.Trim());
                command.Parameters.AddWithValue("@ModelNumber", string.IsNullOrWhiteSpace(_model.Text) ? (object)DBNull.Value : _model.Text.Trim());
                command.Parameters.AddWithValue("@SerialNumber", string.IsNullOrWhiteSpace(_serial.Text) ? (object)DBNull.Value : _serial.Text.Trim());
                command.Parameters.AddWithValue("@InstallDate", _installDate.Checked ? (object)_installDate.Value.Date : DBNull.Value);
                command.Parameters.AddWithValue("@Location", string.IsNullOrWhiteSpace(_location.Text) ? (object)DBNull.Value : _location.Text.Trim());
                command.Parameters.AddWithValue("@Notes", string.IsNullOrWhiteSpace(_notes.Text) ? (object)DBNull.Value : _notes.Text.Trim());
                DatabaseConnectionFactory.Open(connection, "AddAMCEquipmentForm.InsertEquipment");
                command.ExecuteNonQuery();
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


