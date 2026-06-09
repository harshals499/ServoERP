using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class FormTemplatePickerDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly FormTemplateLibraryService _library = new FormTemplateLibraryService();
        private readonly string _workflowName;
        private readonly string _moduleHint;
        private readonly string _tradeHint;
        private readonly string _queryHint;
        private readonly List<FieldServiceFormTemplate> _templates = new List<FieldServiceFormTemplate>();

        private DataGridView _grid;
        private TextBox _txtSearch;
        private ComboBox _cmbTrade;
        private Label _lblSummary;

        public FormTemplatePickerDialog(string workflowName, string moduleHint, string tradeHint, string queryHint)
        {
            _workflowName = string.IsNullOrWhiteSpace(workflowName) ? "Workflow" : workflowName;
            _moduleHint = moduleHint ?? string.Empty;
            _tradeHint = tradeHint ?? string.Empty;
            _queryHint = queryHint ?? string.Empty;

            Text = "ServoERP Forms - " + _workflowName;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(980, 620);
            Size = new Size(1120, 680);
            BackColor = DS.BgPage;
            Font = new Font("Segoe UI", 9f);

            BuildLayout();
            Load += (s, e) => LoadTemplates();
        }

        private void BuildLayout()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = DS.BgPage, Padding = new Padding(22, 16, 22, 10) };
            Label title = new Label
            {
                Text = "Field-Service Form Templates",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = DS.Slate900
            };
            Label subtitle = new Label
            {
                Text = _workflowName + " workflow library. Open a master template or copy a working version for this record.",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9f),
                ForeColor = DS.Slate600
            };
            _lblSummary = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500
            };
            header.Controls.Add(_lblSummary);
            header.Controls.Add(subtitle);
            header.Controls.Add(title);

            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White, Padding = new Padding(18, 10, 18, 8) };
            filterBar.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(DS.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, filterBar.Width - 1), Math.Max(0, filterBar.Height - 1));
            };
            _txtSearch = new TextBox
            {
                Width = 420,
                Height = 30,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                Text = _queryHint
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilters();
            _cmbTrade = new ComboBox
            {
                Left = 438,
                Width = 210,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f)
            };
            _cmbTrade.SelectedIndexChanged += (s, e) => ApplyFilters();
            Button openFolder = MakeButton("Open Library", Color.White, DS.Slate700, 112);
            openFolder.Left = 664;
            openFolder.Click += (s, e) => OpenPath(_library.RootFolder);
            Button openZip = MakeButton("Open ZIP", Color.White, DS.Slate700, 96);
            openZip.Left = 784;
            openZip.Click += (s, e) => OpenPath(_library.ZipPath);
            filterBar.Resize += (s, e) =>
            {
                openZip.Left = filterBar.ClientSize.Width - openZip.Width - 18;
                openFolder.Left = openZip.Left - openFolder.Width - 8;
                _cmbTrade.Left = openFolder.Left - _cmbTrade.Width - 16;
                _txtSearch.Width = Math.Max(260, _cmbTrade.Left - 28);
            };
            filterBar.Controls.Add(_txtSearch);
            filterBar.Controls.Add(_cmbTrade);
            filterBar.Controls.Add(openFolder);
            filterBar.Controls.Add(openZip);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = DS.Border,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 34 },
                Font = new Font("Segoe UI", 8.75f)
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = DS.Slate50;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate700;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trade", DataPropertyName = "Trade", Width = 125 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Form", DataPropertyName = "FormName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 260 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ServoERP Module", DataPropertyName = "RecommendedServoErpModule", Width = 170 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Customer Sign", DataPropertyName = "RequiresCustomerSignature", Width = 96 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Tech Sign", DataPropertyName = "RequiresTechnicianSignature", Width = 78 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Readings", DataPropertyName = "RequiresReadings", Width = 70 });
            _grid.CellDoubleClick += (s, e) => OpenSelectedTemplate();

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = DS.BgPage, Padding = new Padding(18, 12, 18, 14) };
            Button close = MakeButton("Close", Color.White, DS.Slate700, 94);
            close.Dock = DockStyle.Right;
            close.Click += (s, e) => Close();
            Button copy = MakeButton("Copy For Workflow", DS.Primary600, Color.White, 156);
            copy.Dock = DockStyle.Right;
            copy.Margin = new Padding(0, 0, 10, 0);
            copy.Click += (s, e) => CopySelectedTemplate();
            Button open = MakeButton("Open Template", DS.Teal600, Color.White, 132);
            open.Dock = DockStyle.Right;
            open.Margin = new Padding(0, 0, 10, 0);
            open.Click += (s, e) => OpenSelectedTemplate();
            footer.Controls.Add(close);
            footer.Controls.Add(copy);
            footer.Controls.Add(open);

            Controls.Add(_grid);
            Controls.Add(footer);
            Controls.Add(filterBar);
            Controls.Add(header);
        }

        private void LoadTemplates()
        {
            if (!_library.IsAvailable)
            {
                _lblSummary.Text = "Template library is not available at " + _library.RootFolder;
                return;
            }

            _templates.Clear();
            _templates.AddRange(_library.GetTemplates().OrderBy(t => t.Trade).ThenBy(t => t.FormName));

            _cmbTrade.Items.Clear();
            _cmbTrade.Items.Add("All trades");
            foreach (string trade in _templates.Select(t => t.Trade).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t))
                _cmbTrade.Items.Add(trade);
            int tradeIndex = string.IsNullOrWhiteSpace(_tradeHint) ? -1 : _cmbTrade.FindStringExact(_tradeHint);
            _cmbTrade.SelectedIndex = tradeIndex > 0 ? tradeIndex : 0;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_grid == null)
                return;

            string query = (_txtSearch == null ? string.Empty : _txtSearch.Text ?? string.Empty).Trim();
            string trade = (_cmbTrade != null && _cmbTrade.SelectedIndex > 0) ? Convert.ToString(_cmbTrade.SelectedItem) : string.Empty;
            IEnumerable<FieldServiceFormTemplate> filtered = _templates;
            if (!string.IsNullOrWhiteSpace(trade))
                filtered = filtered.Where(t => string.Equals(t.Trade, trade, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_moduleHint)
                && filtered.Any(t => Contains(t.RecommendedServoErpModule, _moduleHint)))
                filtered = filtered.Where(t => Contains(t.RecommendedServoErpModule, _moduleHint));
            if (!string.IsNullOrWhiteSpace(query))
            {
                string[] terms = query.Split(new[] { ' ', ',', ';', '/', '\\', '-' }, StringSplitOptions.RemoveEmptyEntries);
                filtered = filtered.Where(t => terms.Any(term => Matches(t, term)));
            }

            List<FieldServiceFormTemplate> list = filtered.ToList();
            _grid.DataSource = list;
            _lblSummary.Text = list.Count + " templates shown"
                + (string.IsNullOrWhiteSpace(_moduleHint) ? string.Empty : " · module: " + _moduleHint)
                + (string.IsNullOrWhiteSpace(_tradeHint) ? string.Empty : " · trade: " + _tradeHint);
        }

        private static bool Matches(FieldServiceFormTemplate template, string term)
        {
            return Contains(template.FormName, term)
                || Contains(template.Trade, term)
                || Contains(template.Category, term)
                || Contains(template.RecommendedServoErpModule, term)
                || Contains(template.RecommendedFields, term)
                || Contains(template.FileName, term);
        }

        private static bool Contains(string source, string term)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private FieldServiceFormTemplate SelectedTemplate()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.DataBoundItem as FieldServiceFormTemplate;
        }

        private void OpenSelectedTemplate()
        {
            FieldServiceFormTemplate template = SelectedTemplate();
            if (template == null)
            {
                MessageBox.Show(this, "Select a template first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenPath(template.FullPath);
        }

        private void CopySelectedTemplate()
        {
            FieldServiceFormTemplate template = SelectedTemplate();
            if (template == null)
            {
                MessageBox.Show(this, "Select a template first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string targetRoot = Path.Combine(@"C:\HVAC_PRO_MSE\FORM_TEMPLATE_WORKING_COPIES", SafeFolder(_workflowName), DateTime.Today.ToString("yyyyMMdd"));
            string copied = _library.CopyTemplateToWorkingFolder(template, targetRoot);
            MessageBox.Show(this, "Working copy created:\r\n\r\n" + copied, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenPath(targetRoot);
        }

        private static string SafeFolder(string text)
        {
            string safe = new string((text ?? "Workflow").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
            return string.IsNullOrWhiteSpace(safe) ? "Workflow" : safe;
        }

        private static void OpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                MessageBox.Show("Path was not found:\r\n\r\n" + path, "ServoERP Forms", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 8, 0)
            };
            button.FlatAppearance.BorderColor = back == Color.White ? DS.BorderStrong : back;
            button.FlatAppearance.BorderSize = 1;
            DS.Rounded(button, 7);
            return button;
        }
    }

    public static class FormTemplateWorkflowLauncher
    {
        public static void Open(Control owner, string workflowName, string moduleHint, string tradeHint, string queryHint)
        {
            IWin32Window window = owner == null ? null : owner.FindForm();
            using (var dialog = new FormTemplatePickerDialog(workflowName, moduleHint, tradeHint, queryHint))
            {
                if (window == null)
                    dialog.ShowDialog();
                else
                    dialog.ShowDialog(window);
            }
        }
    }
}

