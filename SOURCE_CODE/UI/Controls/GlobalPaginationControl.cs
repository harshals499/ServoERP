using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI.Controls
{
    /// <summary>Shared, responsive numbered pagination for ServoERP lists.</summary>
    public class GlobalPaginationControl : UserControl
    {
        private readonly Label _summaryLabel, _rowsLabel;
        private readonly ComboBox _pageSizeCombo;
        private readonly Button _previousButton, _nextButton;
        private readonly FlowLayoutPanel _pages;
        private readonly ToolTip _toolTip = new ToolTip();
        private PaginationState _state = new PaginationState();
        private bool _updating, _layingOut;
        private string _pageLayoutKey;
        public event EventHandler PageChanged;
        public event EventHandler PageSizeChanged;

        public GlobalPaginationControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Name = "GlobalPagination";
            Tag = "NO_GLOBAL_BUTTON_STYLE NO_CARD_SURFACE";
            BackColor = Color.White;
            Size = new Size(900, 44);
            MinimumSize = new Size(280, 44);
            _summaryLabel = new Label { Name = "PaginationSummary", TextAlign = ContentAlignment.MiddleLeft, Font = ModernERPTheme.Small, ForeColor = DS.Slate600 };
            _rowsLabel = new Label { Text = T("Rows per page"), TextAlign = ContentAlignment.MiddleLeft, Font = ModernERPTheme.Small, ForeColor = DS.Slate700 };
            _pageSizeCombo = new ComboBox { Name = "PaginationPageSize", DropDownStyle = ComboBoxStyle.DropDownList, Font = ModernERPTheme.Small, Tag = "CUSTOM_INPUT_SHELL", AccessibleName = T("Rows per page"), IntegralHeight = false };
            _pageSizeCombo.Items.AddRange(new object[] { "10", "25", "50", "100" });
            _pageSizeCombo.SelectedIndexChanged += PageSizeSelected;
            _previousButton = CreateButton(T("Previous"), 78, T("Previous page"));
            _previousButton.Name = "PaginationPrevious";
            _previousButton.Click += (s, e) => GoToPage(CurrentPage - 1);
            _nextButton = CreateButton(T("Next"), 62, T("Next page"));
            _nextButton.Name = "PaginationNext";
            _nextButton.Click += (s, e) => GoToPage(CurrentPage + 1);
            _pages = new FlowLayoutPanel { Name = "PaginationPages", WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent };
            Controls.AddRange(new Control[] { _summaryLabel, _rowsLabel, _pageSizeCombo, _previousButton, _pages, _nextButton });
            Resize += (s, e) => LayoutControls();
            SetState(1, 0, 10);
        }
        public int CurrentPage { get { return _state.CurrentPage; } }
        public int PageSize { get { return _state.PageSize; } }
        public int TotalRecords { get { return _state.TotalRecords; } }
        public int TotalPages { get { return _state.TotalPages; } }
        public int Skip { get { return _state.Skip; } }
        public int DisplayFrom { get { return _state.DisplayFrom; } }
        public int DisplayTo { get { return _state.DisplayTo; } }
        /// <summary>Updates the display without firing user-navigation events.</summary>
        public void SetState(int currentPage, int totalRecords, int pageSize)
        {
            _updating = true;
            try
            {
                _state = new PaginationState { CurrentPage = currentPage, TotalRecords = totalRecords, PageSize = pageSize }.Normalize();
                string value = PageSize.ToString(CultureInfo.InvariantCulture);
                if (!_pageSizeCombo.Items.Contains(value)) _pageSizeCombo.Items.Add(value);
                _pageSizeCombo.SelectedItem = value;
                _summaryLabel.Text = string.Format(CultureInfo.GetCultureInfo("en-IN"), "Showing {0:N0}–{1:N0} of {2:N0}", DisplayFrom, DisplayTo, TotalRecords);
                _toolTip.SetToolTip(_summaryLabel, _summaryLabel.Text);
                _previousButton.Enabled = TotalRecords > 0 && CurrentPage > 1;
                _nextButton.Enabled = TotalRecords > 0 && CurrentPage < TotalPages;
                foreach (var button in new[] { _previousButton, _nextButton })
                {
                    button.ForeColor = button.Enabled ? DS.Slate800 : DS.Slate400;
                    button.BackColor = button.Enabled ? Color.White : DS.Slate50;
                }
                LayoutControls();
            }
            finally { _updating = false; }
        }
        public int GetSkip() { return Skip; }
        public void ResetToFirstPage(int totalRecords) { SetState(1, totalRecords, PageSize); }
        public void GoToPage(int requestedPage)
        {
            int page = PaginationState.NormalizePage(requestedPage, TotalRecords, PageSize);
            if (page == CurrentPage) return;
            SetState(page, TotalRecords, PageSize);
            OnPageChanged();
        }
        private void PageSizeSelected(object sender, EventArgs e)
        {
            if (_updating) return;
            int size;
            if (!int.TryParse(Convert.ToString(_pageSizeCombo.SelectedItem), out size)) return;
            SetState(1, TotalRecords, size);
            OnPageSizeChanged();
        }
        private Button CreateButton(string text, int width, string accessibleName)
        {
            var button = new Button { Text = text, Size = new Size(width, 32), FlatStyle = FlatStyle.Flat, Font = ModernERPTheme.SmallBold, BackColor = Color.White, ForeColor = DS.Slate800, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 4, 0), UseVisualStyleBackColor = false, AccessibleName = accessibleName, Tag = "PAGINATION_BUTTON" };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.MouseOverBackColor = DS.Slate100;
            _toolTip.SetToolTip(button, accessibleName);
            return button;
        }
        private void BuildPageButtons(int capacity, int buttonWidth)
        {
            int total = TotalPages;
            var entries = new List<int>(); // Zero denotes an ellipsis.
            if (total <= capacity) entries.AddRange(Enumerable.Range(1, Math.Max(1, total)));
            else if (capacity >= 7)
            {
                int start = Math.Max(2, Math.Min(CurrentPage - 1, total - 3));
                int end = Math.Min(total - 1, start + 2);
                entries.Add(1);
                if (start > 2) entries.Add(0);
                entries.AddRange(Enumerable.Range(start, end - start + 1));
                if (end < total - 1) entries.Add(0);
                entries.Add(total);
            }
            else if (capacity >= 5)
            {
                if (CurrentPage <= 2) entries.AddRange(new[] { 1, 2, 3, 0, total });
                else if (CurrentPage >= total - 1) entries.AddRange(new[] { 1, 0, total - 2, total - 1, total });
                else entries.AddRange(new[] { 1, 0, CurrentPage, 0, total });
            }
            else if (capacity >= 3)
            {
                int start = Math.Max(1, Math.Min(CurrentPage - 1, total - 2));
                entries.AddRange(Enumerable.Range(start, 3));
            }
            else entries.Add(Math.Max(1, CurrentPage));
            string key = string.Join(",", entries) + ":" + CurrentPage + ":" + buttonWidth + ":" + (total == 0);
            if (key == _pageLayoutKey) return;
            string focusedName = _pages.Controls.Cast<Control>().FirstOrDefault(c => c.Focused)?.Name;
            _pageLayoutKey = key;
            _pages.SuspendLayout();
            while (_pages.Controls.Count > 0)
            {
                Control old = _pages.Controls[0];
                _pages.Controls.RemoveAt(0);
                _toolTip.SetToolTip(old, null);
                old.Dispose();
            }
            foreach (int page in entries)
            {
                if (page == 0)
                {
                    _pages.Controls.Add(new Label { Text = "…", Size = new Size(buttonWidth, 32), TextAlign = ContentAlignment.MiddleCenter, Font = ModernERPTheme.Small, ForeColor = DS.Slate600, Margin = new Padding(0, 0, 4, 0) });
                    continue;
                }
                int target = page;
                var button = CreateButton(page.ToString(CultureInfo.InvariantCulture), buttonWidth, T("Page") + " " + page);
                button.Name = "PaginationPage_" + page;
                button.Enabled = total > 0;
                if (page == CurrentPage && total > 0)
                {
                    button.BackColor = DS.Primary600;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = DS.Primary600;
                    button.FlatAppearance.MouseOverBackColor = DS.Primary700;
                    button.AccessibleDescription = "Current page";
                }
                button.Click += (s, e) => GoToPage(target);
                _pages.Controls.Add(button);
            }
            _pages.ResumeLayout(true);
            if (focusedName != null)
            {
                Control focus = _pages.Controls.Cast<Control>().FirstOrDefault(c => c.Name == focusedName)
                    ?? _pages.Controls.Cast<Control>().FirstOrDefault(c => c.Name == "PaginationPage_" + CurrentPage);
                if (focus != null) focus.Focus();
            }
        }
        private void LayoutControls()
        {
            if (_pages == null || _layingOut) return;
            _layingOut = true;
            try
            {
                int rowHeight = Math.Max(32, TextRenderer.MeasureText("Next", ModernERPTheme.SmallBold).Height + 12);
                const int pad = 6;
                int width = ClientSize.Width;
                int summaryWidth = TextRenderer.MeasureText(_summaryLabel.Text, _summaryLabel.Font).Width + 8;
                int rowsWidth = TextRenderer.MeasureText(T("Rows per page"), _rowsLabel.Font).Width + 6;
                int numberWidth = Math.Max(30, TextRenderer.MeasureText(Math.Max(1, TotalPages).ToString(), ModernERPTheme.SmallBold).Width + 14);
                bool stacked = width < summaryWidth + rowsWidth + 60 + 78 + 62 + (numberWidth + 4) * 5 + 48;
                int requiredHeight = stacked ? rowHeight * 2 + pad * 3 : rowHeight + pad * 2;
                if (Height != requiredHeight)
                {
                    Height = requiredHeight;
                    // WinForms may already be docking the parent when the footer wraps.
                    // Finish that pass, then redock using the new height.
                    if (IsHandleCreated && Parent != null)
                        BeginInvoke(new Action(() => { if (!IsDisposed && Parent != null) Parent.PerformLayout(this, "Height"); }));
                }
                _rowsLabel.Text = T(stacked && width < 400 ? "Rows" : "Rows per page");
                rowsWidth = TextRenderer.MeasureText(_rowsLabel.Text, _rowsLabel.Font).Width + 6;
                _summaryLabel.SetBounds(pad, pad, Math.Max(0, stacked ? width - rowsWidth - 72 - pad * 3 : summaryWidth), rowHeight);
                _summaryLabel.AutoEllipsis = true;
                _pageSizeCombo.Width = 60;
                int sizeX = stacked ? width - pad - 60 : pad + summaryWidth + 12 + rowsWidth + 6;
                _pageSizeCombo.Location = new Point(sizeX, pad + (rowHeight - _pageSizeCombo.Height) / 2);
                _rowsLabel.SetBounds(sizeX - rowsWidth - 6, pad, rowsWidth, rowHeight);
                int navY = stacked ? rowHeight + pad * 2 : pad;
                int navStart = stacked ? pad : _pageSizeCombo.Right + 12;
                int available = Math.Max(numberWidth + 4, width - pad - navStart - 78 - 62 - 12);
                BuildPageButtons(Math.Max(1, available / (numberWidth + 4)), numberWidth);
                int pagesWidth = _pages.Controls.Cast<Control>().Sum(c => c.Width + c.Margin.Horizontal);
                _nextButton.SetBounds(width - pad - 62, navY, 62, rowHeight);
                _pages.SetBounds(_nextButton.Left - 6 - pagesWidth, navY, pagesWidth, rowHeight);
                _previousButton.SetBounds(stacked ? pad : _pages.Left - 6 - 78, navY, 78, rowHeight);
                foreach (Control child in _pages.Controls) child.Height = rowHeight;
            }
            finally { _layingOut = false; }
        }
        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); LayoutControls(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Home)) { GoToPage(1); return true; }
            if (keyData == (Keys.Control | Keys.End)) { GoToPage(TotalPages); return true; }
            if (keyData == (Keys.Alt | Keys.Left)) { GoToPage(CurrentPage - 1); return true; }
            if (keyData == (Keys.Alt | Keys.Right)) { GoToPage(CurrentPage + 1); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void Dispose(bool disposing) { if (disposing) _toolTip.Dispose(); base.Dispose(disposing); }
        protected virtual void OnPageChanged() { PageChanged?.Invoke(this, EventArgs.Empty); }
        protected virtual void OnPageSizeChanged() { PageSizeChanged?.Invoke(this, EventArgs.Empty); }
        private static string T(string key) { return LanguageManager.Get(key); }
    }

    /// <summary>Finishes footer-host sizing after WinForms completes a nested docking pass.</summary>
    internal static class PaginationFooterLayout
    {
        internal static void Bind(GlobalPaginationControl pager, Action resizeHost)
        {
            bool queued = false;
            EventHandler update = (s, e) =>
            {
                resizeHost();
                if (queued || !pager.IsHandleCreated || pager.IsDisposed) return;
                queued = true;
                pager.BeginInvoke(new Action(() =>
                {
                    queued = false;
                    if (!pager.IsDisposed)
                    {
                        resizeHost();
                        // Redock the host itself as well as the control inside it.
                        for (Control parent = pager.Parent; parent != null; parent = parent.Parent)
                            parent.PerformLayout();
                    }
                }));
            };
            pager.SizeChanged += update;
            pager.HandleCreated += update;
            resizeHost();
        }
    }

    /// <summary>Pages existing unbound grids without copying rows or losing their action metadata.</summary>
    internal static class GridPaginationBinding
    {
        internal static void Attach(DataGridView grid, GlobalPaginationControl pager)
        {
            bool updating = false;
            Action refresh = () =>
            {
                if (updating || grid.IsDisposed || pager.IsDisposed) return;
                updating = true;
                try
                {
                    var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
                    pager.SetState(pager.CurrentPage, rows.Count, pager.PageSize);
                    int from = pager.Skip;
                    int to = from + pager.PageSize;
                    if (grid.CurrentCell != null && (grid.CurrentCell.RowIndex < from || grid.CurrentCell.RowIndex >= to))
                        grid.CurrentCell = null;
                    for (int i = 0; i < rows.Count; i++) rows[i].Visible = i >= from && i < to;
                }
                finally { updating = false; }
            };
            DataGridViewRowsAddedEventHandler added = (s, e) => refresh();
            DataGridViewRowsRemovedEventHandler removed = (s, e) => refresh();
            EventHandler navigated = (s, e) => refresh();
            DataGridViewBindingCompleteEventHandler bound = (s, e) => refresh();
            grid.RowsAdded += added;
            grid.RowsRemoved += removed;
            grid.DataBindingComplete += bound;
            grid.Sorted += navigated;
            pager.PageChanged += navigated;
            pager.PageSizeChanged += navigated;
            pager.Disposed += (s, e) =>
            {
                grid.RowsAdded -= added;
                grid.RowsRemoved -= removed;
                grid.DataBindingComplete -= bound;
                grid.Sorted -= navigated;
            };
            refresh();
        }
    }
}
