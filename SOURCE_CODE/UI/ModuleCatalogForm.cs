using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ModuleCatalogForm : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly ModuleCatalogService _service = new ModuleCatalogService();
        private DataGridView _grid;
        private TextBox _report;
        private Label _status;

        /// <summary>Initializes the module catalog and extension-readiness screen.</summary>
        public ModuleCatalogForm()
        {
            BuildLayout();
            LoadCatalog();
        }

        private void BuildLayout()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DS.BgPage;
            ClientSize = new Size(1080, 720);
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "ServoERP - Module Catalog";

            Controls.Add(new Label { Text = "Module Catalog", Location = new Point(24, 18), Size = new Size(460, 34), Font = DS.H1, ForeColor = DS.Slate900 });
            Controls.Add(new Label { Text = "Installed modules and extension-ready ideas inspired by mature open-source ERP patterns.", Location = new Point(26, 52), Size = new Size(920, 28), Font = DS.Body, ForeColor = DS.Slate600 });

            _grid = new DataGridView
            {
                Location = new Point(24, 96),
                Size = new Size(1032, 304),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            DS.StyleGrid(_grid);
            Controls.Add(_grid);

            _report = new TextBox
            {
                Location = new Point(24, 416),
                Size = new Size(1032, 198),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = DS.Mono,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = DS.Slate800
            };
            Controls.Add(_report);

            Button export = Button("Export Catalog", DS.Green600, Color.White, 140);
            export.Location = new Point(24, 634);
            export.Click += (s, e) => ExportCatalog();
            Controls.Add(export);

            Button copy = Button("Copy Text", DS.Primary600, Color.White, 110);
            copy.Location = new Point(178, 634);
            copy.Click += (s, e) => CopyReport();
            Controls.Add(copy);

            Button close = Button("Close", Color.White, DS.Slate700, 96);
            close.FlatAppearance.BorderColor = DS.Border;
            close.FlatAppearance.BorderSize = 1;
            close.Location = new Point(960, 634);
            close.Click += (s, e) => Close();
            Controls.Add(close);

            _status = new Label { Text = "Ready.", Location = new Point(310, 640), Size = new Size(620, 24), Font = DS.Small, ForeColor = DS.Slate600 };
            Controls.Add(_status);
        }

        private void LoadCatalog()
        {
            _grid.DataSource = _service.GetCatalog().Select(i => new
            {
                Module = i.ModuleName,
                i.Category,
                i.Status,
                Benefit = i.ClientBenefit,
                Pattern = i.OpenSourceInspiration,
                i.NextStep
            }).ToList();
            _report.Text = _service.BuildReport();
        }

        private void ExportCatalog()
        {
            try
            {
                string path = _service.ExportReport();
                _status.Text = "Catalog exported: " + path;
                _status.ForeColor = DS.Green600;
                Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                _status.Text = "Export failed: " + ex.Message;
                _status.ForeColor = DS.Red600;
            }
        }

        private void CopyReport()
        {
            if (UIHelper.TrySetClipboardText(this, _report.Text, BrandingService.WindowTitle("Module Catalog")))
            {
                _status.Text = "Catalog copied to clipboard.";
                _status.ForeColor = DS.Green600;
            }
        }

        private static Button Button(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button { Text = text, Size = new Size(width, 34), BackColor = backColor, ForeColor = foreColor, FlatStyle = FlatStyle.Flat, Font = DS.BodyBold, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
            return button;
        }
    }
}

