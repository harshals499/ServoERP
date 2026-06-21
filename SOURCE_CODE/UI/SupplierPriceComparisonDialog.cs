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
        private readonly UnitMeasurementService _unitService = new UnitMeasurementService();
        private readonly int? _currentVendorId;
        private readonly DataGridView _grid;
        private readonly Label _itemTitle;
        private readonly Label _summary;
        private readonly Panel _topMask;
        private readonly Panel _headerPanel;
        private readonly Panel _emptyStatePanel;
        private readonly Button _applyLowestButton;
        private readonly Button _useButton;
        private readonly Button _closeButton;
        private List<SupplierOption> _options = new List<SupplierOption>();

        public SupplierOption SelectedOption { get; private set; }

        public SupplierPriceComparisonDialog(string itemDescription, string category, decimal quantity, VendorService vendorService = null, int? currentVendorId = null)
        {
            _itemDescription = itemDescription ?? string.Empty;
            _category = category ?? string.Empty;
            _quantity = quantity <= 0 ? 1m : quantity;
            _vendorService = vendorService ?? new VendorService();
            _currentVendorId = currentVendorId;

            Text = BrandingService.WindowTitle("Supplier Price Comparison");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = DS.Body;
            ClientSize = new Size(760, 470);

            _topMask = new Panel
            {
                Visible = false
            };

            _headerPanel = new Panel
            {
                Location = new Point(22, 36),
                Size = new Size(716, 84),
                BackColor = Color.White
            };

            Label title = new Label
            {
                Text = "Supplier Price Comparison",
                Location = new Point(0, 0),
                Size = new Size(700, 26),
                Font = DS.H2,
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };

            _itemTitle = new Label
            {
                Text = string.IsNullOrWhiteSpace(_itemDescription) ? "Select a material to compare supplier offers." : _itemDescription,
                Location = new Point(0, 30),
                Size = new Size(700, 24),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = DS.Slate800,
                AutoEllipsis = true
            };

            _summary = new Label
            {
                Location = new Point(0, 56),
                Size = new Size(700, 18),
                Font = DS.Body,
                ForeColor = DS.Slate600,
                AutoEllipsis = true
            };

            _headerPanel.Controls.Add(title);
            _headerPanel.Controls.Add(_itemTitle);
            _headerPanel.Controls.Add(_summary);

            _grid = new DataGridView
            {
                Location = new Point(22, 128),
                Size = new Size(716, 268),
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
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Supplier", HeaderText = "Supplier Name", FillWeight = 30 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate", HeaderText = "Unit Price", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "UOM", FillWeight = 9 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastPurchaseDate", HeaderText = "Last Purchase Date", FillWeight = 16 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "QtyAvailable", HeaderText = "Qty Available", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LeadDays", HeaderText = "Lead Days", FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "WeightedScore", HeaderText = "Score", FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewButtonColumn { Name = "Select", HeaderText = "", Text = "Select", UseColumnTextForButtonValue = true, FillWeight = 9 });
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellDoubleClick += (s, e) => UseSelectedOption();
            _grid.SelectionChanged += (s, e) => UpdateUseButton();
            DS.StyleGrid(_grid);

            _emptyStatePanel = new Panel
            {
                Location = new Point(22, 140),
                Size = new Size(716, 240),
                BackColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };

            Label emptyTitle = new Label
            {
                Text = "Supplier and price details are not available yet",
                Location = new Point(0, 82),
                Size = new Size(714, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = DS.Slate800
            };

            Label emptyHint = new Label
            {
                Text = "No saved supplier rate is available for this material yet. Add supplier price data or complete a purchase entry, then compare again.",
                Location = new Point(48, 116),
                Size = new Size(620, 40),
                TextAlign = ContentAlignment.TopCenter,
                Font = DS.Body,
                ForeColor = DS.Slate600
            };

            _emptyStatePanel.Controls.Add(emptyTitle);
            _emptyStatePanel.Controls.Add(emptyHint);

            _applyLowestButton = DS.GhostBtn("Apply Lowest Price", 146, 36);
            _applyLowestButton.Location = new Point(338, 416);
            _applyLowestButton.Click += (s, e) => ApplyLowestPrice();

            _useButton = DS.PrimaryBtn("Apply Selected", 126, 36);
            _useButton.Location = new Point(612, 416);
            _useButton.Click += (s, e) => UseSelectedOption();

            _closeButton = DS.GhostBtn("Close", 96, 36);
            _closeButton.Location = new Point(506, 416);
            _closeButton.DialogResult = DialogResult.Cancel;

            Controls.Add(_topMask);
            Controls.Add(_headerPanel);
            Controls.Add(_grid);
            Controls.Add(_emptyStatePanel);
            Controls.Add(_applyLowestButton);
            Controls.Add(_closeButton);
            Controls.Add(_useButton);

            _topMask.BringToFront();
            _headerPanel.BringToFront();

            AcceptButton = _useButton;
            CancelButton = _closeButton;
            Load += (s, e) => LoadOptions();
        }

        private void LoadOptions()
        {
            try
            {
                _options = _vendorService.GetSupplierOptions(_itemDescription, _category, _quantity)
                    .Where(o => o != null && o.VendorID > 0)
                    .OrderBy(o => o.WeightedScore)
                    .ThenBy(o => o.Rate <= 0 ? decimal.MaxValue : o.Rate)
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
                    option.VendorName,
                    IndiaFormatHelper.FormatCurrency(option.Rate),
                    _unitService.NormalizeForPickerDisplayOrDefault(option.Unit),
                    option.LastPurchaseDate.HasValue ? IndiaFormatHelper.FormatDate(option.LastPurchaseDate.Value) : "-",
                    option.QtyAvailable.ToString("0.###"),
                    option.LeadDays.HasValue ? option.LeadDays.Value.ToString() : "-",
                    option.WeightedScore.ToString("0.##"));
                DataGridViewRow gridRow = _grid.Rows[row];
                gridRow.Tag = option;
                bool isBest = ReferenceEquals(option, _options.OrderBy(o => o.WeightedScore).ThenBy(o => o.Rate).FirstOrDefault());
                if (isBest)
                {
                    gridRow.DefaultCellStyle.BackColor = Color.FromArgb(236, 253, 245);
                    gridRow.DefaultCellStyle.SelectionBackColor = Color.FromArgb(22, 163, 74);
                    gridRow.DefaultCellStyle.SelectionForeColor = Color.White;
                }
                if (_currentVendorId.HasValue && option.VendorID == _currentVendorId.Value)
                    gridRow.DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            }

            DataGridViewRow preferredRow = _grid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(r => (r.Tag as SupplierOption)?.VendorID == _currentVendorId);
            if (preferredRow != null)
            {
                preferredRow.Selected = true;
                _grid.CurrentCell = preferredRow.Cells["Supplier"];
            }
            else if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells["Supplier"];
            }

            SupplierOption best = _options.FirstOrDefault();
            if (best == null)
                _summary.Text = string.IsNullOrWhiteSpace(_category)
                    ? "Supplier and price details are not available for this material yet."
                    : "Supplier and price details are not available for this " + _category.Trim() + " material yet.";
            else
                _summary.Text = _options.Count.ToString("N0") + " supplier option" + (_options.Count == 1 ? string.Empty : "s") + " found. Best: "
                    + best.VendorName + " at " + IndiaFormatHelper.FormatCurrency(best.Rate) + " / "
                    + _unitService.NormalizeForPickerDisplayOrDefault(best.Unit) + " for "
                    + _quantity.ToString("0.##") + " qty"
                    + " | score " + best.WeightedScore.ToString("0.##")
                    + (best.OnTimeDeliveryRatePct.HasValue ? " | on-time " + best.OnTimeDeliveryRatePct.Value.ToString("0.#") + "%" : string.Empty)
                    + (best.StockCoveragePct.HasValue ? " | stock " + best.StockCoveragePct.Value.ToString("0.#") + "%" : string.Empty);

            _grid.Visible = _options.Count > 0;
            _emptyStatePanel.Visible = _options.Count == 0;

            UpdateUseButton();
            _applyLowestButton.Enabled = _options.Count > 0;
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

        private void ApplyLowestPrice()
        {
            SupplierOption option = _options
                .Where(o => o != null)
                .OrderBy(o => o.WeightedScore)
                .ThenBy(o => o.Rate <= 0m ? decimal.MaxValue : o.Rate)
                .ThenBy(o => o.VendorName)
                .FirstOrDefault();
            if (option == null)
                return;

            SelectedOption = option;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Select")
                return;

            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells["Supplier"];
            _grid.Rows[e.RowIndex].Selected = true;
            UseSelectedOption();
        }
    }
}
