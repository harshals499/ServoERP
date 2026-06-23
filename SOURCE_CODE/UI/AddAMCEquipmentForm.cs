using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Captures one equipment item covered under an AMC.</summary>
    public partial class AddAMCEquipmentForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly int _amcId;
        private ComboBox _name;
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

            _name = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            _model = new TextBox();
            _serial = new TextBox();
            _installDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", ShowCheckBox = true, Checked = false };
            _location = new TextBox();
            _notes = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };

            AddRow(grid, 0, "Equipment Name *", _name);
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
            LoadEquipmentListAsync();
        }

        /// <summary>Loads active inventory items into the equipment selector without blocking the dialog.</summary>
        private async void LoadEquipmentListAsync()
        {
            if (_name == null || IsDisposed)
                return;

            _name.Enabled = false;
            _name.Items.Clear();
            _name.Items.Add("Loading material list...");
            _name.SelectedIndex = 0;

            try
            {
                List<string> equipmentOptions = await Task.Run(() => LoadEquipmentOptions()) ?? new List<string>();
                if (IsDisposed)
                    return;

                _name.BeginUpdate();
                try
                {
                    _name.Items.Clear();
                    _name.Text = string.Empty;
                    foreach (string option in equipmentOptions)
                    {
                        if (!string.IsNullOrWhiteSpace(option))
                            _name.Items.Add(option);
                    }
                }
                finally
                {
                    _name.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo("AddAMCEquipmentForm inventory equipment load failed: " + ex.GetType().Name + ": " + ex.Message);
                if (!IsDisposed)
                {
                    _name.Items.Clear();
                    _name.Text = string.Empty;
                }
            }
            finally
            {
                if (!IsDisposed)
                    _name.Enabled = true;
            }
        }

        /// <summary>Returns distinct active inventory item names for AMC equipment selection.</summary>
        private List<string> LoadEquipmentOptions()
        {
            var options = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new InventoryService().GetAll();
            if (items == null)
                return options;

            foreach (var item in items)
            {
                string name = item == null ? string.Empty : item.ItemName;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                name = name.Trim();
                if (seen.Add(name))
                    options.Add(name);
            }

            options.Sort(StringComparer.CurrentCultureIgnoreCase);
            return options;
        }

        /// <summary>Adds one row to the form grid.</summary>
        private void AddRow(TableLayoutPanel grid, int row, string label, Control editor)
        {
            grid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = label.IndexOf('*') >= 0 ? DS.Primary600 : DS.Slate900, AutoEllipsis = true }, 0, row);
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 4, 0, 4);
            grid.Controls.Add(editor, 1, row);
        }

        /// <summary>Saves equipment asynchronously without blocking the modal.</summary>
        private async void SaveEquipment()
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                MessageBox.Show("Enter Equipment Name.", BrandingService.WindowTitle("AMC Equipment"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _save.Enabled = false;
            try
            {
                await Task.Run(() => InsertEquipment());

                RunOnUI(() =>
                {
                    _save.Enabled = true;
                    DialogResult = DialogResult.OK;
                    Close();
                });
            }
            catch (Exception ex)
            {
                RunOnUI(() => _save.Enabled = true);
                ShowError("Equipment could not be saved. Please try again.", ex);
            }
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


