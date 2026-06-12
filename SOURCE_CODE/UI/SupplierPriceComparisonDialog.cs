using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    public class SupplierPriceComparisonDialog : ServoFormBase
    {
        private readonly string _itemDescription;
        private readonly string _category;
        private readonly decimal _quantity;
        private readonly VendorService _vendorService;
        private readonly DataGridView _grid;
        private readonly Label _summary;
        private readonly Button _useButton;
        private readonly Button _closeButton;
        private List<SupplierOption> _options = new List<SupplierOption>();

        public SupplierOption SelectedOption { get; private set; }

        public SupplierPriceComparisonDialog(string itemDescription, string category, decimal quantity, VendorService vendorService = null)
        {
            _itemDescription = itemDescription ?? string.Empty;
            _category = category ?? string.Empty;
            _quantity = quantity <= 0 ? 1m : quantity;
            _vendorService = vendorService ?? new VendorService();

            Text = BrandingService.WindowTitle("Supplier Price Comparison");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = DS.Body;
            ClientSize = new Size(760, 470);

            Label title = new Label
            {
                Text = string.IsNullOrWhiteSpace(_itemDescription) ? "Compare Supplier Prices" : _itemDescription,
                Location = new Point(22, 18),
                Size = new Size(700, 26),
                Font = DS.H2,
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            _summary = new Label
            {
                Location = new Point(22, 48),
                Size = new Size(700, 20),
                Font = DS.Body,
                ForeColor = DS.Slate600,
                AutoEllipsis = true
            };

            _grid = new DataGridView
            {
                Location = new Point(22, 84),
                Size = new Size(716, 312),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rank", HeaderText = "#", FillWeight = 5 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier", FillWeight = 26 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Rate", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Unit", FillWeight = 9 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Estimated", HeaderText = "Est. Cost", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Source", FillWeight = 15 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Contact", HeaderText = "Contact", FillWeight = 17 });
            _grid.CellDoubleClick += (s, e) => UseSelectedOption();
            _grid.SelectionChanged += (s, e) => UpdateUseButton();
            DS.StyleGrid(_grid);

            _useButton = DS.PrimaryBtn("Use Supplier", 126, 36);
            _useButton.Location = new Point(594, 416);
            _useButton.Click += (s, e) => UseSelectedOption();

            _closeButton = DS.GhostBtn("Close", 96, 36);
            _closeButton.Location = new Point(486, 416);
            _closeButton.DialogResult = DialogResult.Cancel;

            Controls.Add(title);
            Controls.Add(_summary);
            Controls.Add(_grid);
            Controls.Add(_closeButton);
            Controls.Add(_useButton);

            AcceptButton = _useButton;
            CancelButton = _closeButton;
            Load += (s, e) => LoadOptions();
        }

        private void LoadOptions()
        {
            try
            {
                _options = _vendorService.GetSupplierOptions(_itemDescription, _category)
                    .Where(o => o != null && o.VendorID > 0)
                    .OrderBy(o => o.Rate <= 0 ? decimal.MaxValue : o.Rate)
                    .ThenBy(o => o.VendorName)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SupplierPriceComparisonDialog.LoadOptions", ex);
                _options = new List<SupplierOption>();
            }

            _grid.Rows.Clear();
            for (int i = 0; i < _options.Count; i++)
            {
                SupplierOption option = _options[i];
                int row = _grid.Rows.Add(
                    (i + 1).ToString("N0"),
                    option.VendorName,
                    IndiaFormatHelper.FormatCurrency(option.Rate),
                    string.IsNullOrWhiteSpace(option.Unit) ? "Nos" : option.Unit,
                    IndiaFormatHelper.FormatCurrency(option.EstimatedCost(_quantity)),
                    string.IsNullOrWhiteSpace(option.Source) ? "Supplier Price" : option.Source,
                    BuildContact(option));
                _grid.Rows[row].Tag = option;
            }

            if (_grid.Rows.Count > 0)
                _grid.Rows[0].Selected = true;

            SupplierOption best = _options.FirstOrDefault();
            if (best == null)
                _summary.Text = "No saved supplier price found yet. Add supplier price data or purchase history for this material.";
            else
                _summary.Text = "Best: " + best.VendorName + " at " + IndiaFormatHelper.FormatCurrency(best.Rate) + " / " + (string.IsNullOrWhiteSpace(best.Unit) ? "Nos" : best.Unit) + " for " + _quantity.ToString("0.##") + " qty.";

            UpdateUseButton();
        }

        private string BuildContact(SupplierOption option)
        {
            if (!string.IsNullOrWhiteSpace(option.Phone))
                return option.Phone;
            if (!string.IsNullOrWhiteSpace(option.Email))
                return option.Email;
            return "-";
        }

        private void UpdateUseButton()
        {
            _useButton.Enabled = GetSelectedOption() != null;
        }

        private SupplierOption GetSelectedOption()
        {
            if (_grid.CurrentRow != null)
                return _grid.CurrentRow.Tag as SupplierOption;
            if (_grid.SelectedRows.Count > 0)
                return _grid.SelectedRows[0].Tag as SupplierOption;
            return null;
        }

        private void UseSelectedOption()
        {
            SupplierOption option = GetSelectedOption();
            if (option == null)
                return;

            SelectedOption = option;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
