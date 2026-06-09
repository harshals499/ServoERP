using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI.Controls
{
    /// <summary>Reusable ServoERP pagination footer with page entry, page-size selection, and navigation buttons.</summary>
    public class GlobalPaginationControl : UserControl
    {
        private readonly Button _firstButton;
        private readonly Button _previousButton;
        private readonly Button _nextButton;
        private readonly Button _lastButton;
        private readonly TextBox _pageBox;
        private readonly Label _totalPagesLabel;
        private readonly Label _summaryLabel;
        private readonly ComboBox _pageSizeCombo;
        private readonly ToolTip _toolTip = new ToolTip();
        private bool _updating;
        private PaginationState _state = new PaginationState();

        public event EventHandler PageChanged;
        public event EventHandler PageSizeChanged;

        public GlobalPaginationControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            Height = 38;
            Width = 600;
            BackColor = Color.Transparent;
            MinimumSize = new Size(300, 36);

            _summaryLabel = new Label
            {
                AutoSize = false,
                Width = 190,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate600,
                Margin = new Padding(0, 0, 12, 0)
            };

            _pageSizeCombo = new ComboBox
            {
                Width = 58,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8f),
                FlatStyle = FlatStyle.Standard,
                IntegralHeight = false,
                Margin = new Padding(0, 0, 10, 0),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            _pageSizeCombo.Items.AddRange(new object[] { "10", "25", "50", "100" });
            _pageSizeCombo.SelectedIndexChanged += PageSizeComboSelectedIndexChanged;

            _firstButton = NavButton("<<", T("First page"));
            _previousButton = NavButton("<", T("Previous page"));
            _pageBox = new TextBox
            {
                Width = 44,
                Height = 26,
                TextAlign = HorizontalAlignment.Center,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4, 0, 4, 0),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            _pageBox.KeyDown += PageBoxKeyDown;
            _pageBox.Leave += (s, e) => CommitPageBox();
            _totalPagesLabel = new Label
            {
                Width = 56,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8f),
                ForeColor = DS.Slate600,
                Margin = new Padding(0, 0, 6, 0)
            };
            _nextButton = NavButton(">", T("Next page"));
            _lastButton = NavButton(">>", T("Last page"));

            _firstButton.Click += (s, e) => NavigateTo(1);
            _previousButton.Click += (s, e) => NavigateTo(_state.CurrentPage - 1);
            _nextButton.Click += (s, e) => NavigateTo(_state.CurrentPage + 1);
            _lastButton.Click += (s, e) => NavigateTo(Math.Max(1, _state.TotalPages));

            Controls.Add(_summaryLabel);
            Controls.Add(_pageSizeCombo);
            Controls.Add(_firstButton);
            Controls.Add(_previousButton);
            Controls.Add(_pageBox);
            Controls.Add(_totalPagesLabel);
            Controls.Add(_nextButton);
            Controls.Add(_lastButton);
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

        /// <summary>Applies a validated state without firing navigation events.</summary>
        public void SetState(int currentPage, int totalRecords, int pageSize)
        {
            _updating = true;
            _state = new PaginationState
            {
                CurrentPage = currentPage,
                PageSize = Math.Max(1, pageSize),
                TotalRecords = Math.Max(0, totalRecords)
            }.Normalize();

            EnsurePageSizeOption(_state.PageSize);
            _pageSizeCombo.SelectedItem = _state.PageSize.ToString(CultureInfo.InvariantCulture);
            _pageBox.Text = _state.TotalRecords <= 0 ? "0" : _state.CurrentPage.ToString(CultureInfo.InvariantCulture);
            _totalPagesLabel.Text = T("of") + " " + (_state.TotalRecords <= 0 ? "0" : _state.TotalPages.ToString(CultureInfo.InvariantCulture));
            _summaryLabel.Text = string.Format(CultureInfo.CurrentUICulture, T("Showing {0} to {1} of {2}"), _state.DisplayFrom, _state.DisplayTo, _state.TotalRecords);
            UpdateButtonStates();
            LayoutControls();
            _updating = false;
        }

        /// <summary>Returns a zero-based slice start for the current state.</summary>
        public int GetSkip()
        {
            return _state.Skip;
        }

        /// <summary>Resets to the first page while preserving page size.</summary>
        public void ResetToFirstPage(int totalRecords)
        {
            SetState(1, totalRecords, _state.PageSize);
        }

        private void PageSizeComboSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updating)
                return;

            int selectedSize;
            if (!int.TryParse(Convert.ToString(_pageSizeCombo.SelectedItem), out selectedSize))
                selectedSize = PaginationState.DefaultPageSize;

            _state.PageSize = Math.Max(1, selectedSize);
            _state.CurrentPage = 1;
            _state = _state.Normalize();
            SetState(_state.CurrentPage, _state.TotalRecords, _state.PageSize);
            OnPageSizeChanged();
        }

        private void PageBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            CommitPageBox();
        }

        private void CommitPageBox()
        {
            if (_updating)
                return;

            int requested;
            if (!int.TryParse((_pageBox.Text ?? string.Empty).Trim(), out requested))
            {
                SetState(_state.CurrentPage, _state.TotalRecords, _state.PageSize);
                return;
            }

            NavigateTo(requested);
        }

        /// <summary>Navigates to the requested page after clamping it to the valid page range.</summary>
        public void GoToPage(int requestedPage)
        {
            NavigateTo(requestedPage);
        }

        private void NavigateTo(int requestedPage)
        {
            int page = PaginationState.NormalizePage(requestedPage, _state.TotalRecords, _state.PageSize);
            if (page == _state.CurrentPage)
            {
                SetState(_state.CurrentPage, _state.TotalRecords, _state.PageSize);
                return;
            }

            _state.CurrentPage = page;
            SetState(_state.CurrentPage, _state.TotalRecords, _state.PageSize);
            OnPageChanged();
        }

        private Button NavButton(string text, string tooltip)
        {
            Button button = new Button
            {
                Text = text,
                Width = 36,
                Height = 28,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(2, 0, 2, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private void UpdateButtonStates()
        {
            bool hasPages = _state.TotalRecords > 0 && _state.TotalPages > 0;
            bool canPrevious = hasPages && _state.CurrentPage > 1;
            bool canNext = hasPages && _state.CurrentPage < _state.TotalPages;
            _firstButton.Enabled = canPrevious;
            _previousButton.Enabled = canPrevious;
            _nextButton.Enabled = canNext;
            _lastButton.Enabled = canNext;
            _pageBox.Enabled = hasPages;
            _pageSizeCombo.Enabled = true;
            ApplyEnabledState(_firstButton);
            ApplyEnabledState(_previousButton);
            ApplyEnabledState(_nextButton);
            ApplyEnabledState(_lastButton);
        }

        private void EnsurePageSizeOption(int pageSize)
        {
            string value = pageSize.ToString(CultureInfo.InvariantCulture);
            if (!_pageSizeCombo.Items.Cast<object>().Any(item => string.Equals(Convert.ToString(item), value, StringComparison.Ordinal)))
                _pageSizeCombo.Items.Add(value);
        }

        private void LayoutControls()
        {
            int available = Math.Max(0, ClientSize.Width);
            bool compact = available > 0 && available < GetVisibleWidth(true, true, true);
            bool hidePageSize = available > 0 && available < GetVisibleWidth(false, true, true);
            bool hideOuterButtons = available > 0 && available < 330;

            _summaryLabel.Visible = !compact;
            _pageSizeCombo.Visible = !hidePageSize;
            _firstButton.Visible = !hideOuterButtons;
            _lastButton.Visible = !hideOuterButtons;

            int usedWidth = GetVisibleWidth(_summaryLabel.Visible, _pageSizeCombo.Visible, _firstButton.Visible && _lastButton.Visible);
            int x = Math.Max(0, available - usedWidth);

            foreach (Control control in Controls)
            {
                if (!control.Visible)
                    continue;

                x += control.Margin.Left;
                int y = Math.Max(0, (ClientSize.Height - control.Height) / 2);
                control.Location = new Point(x, y);
                x += control.Width + control.Margin.Right;
            }
        }

        private int GetVisibleWidth(bool includeSummary, bool includePageSize, bool includeOuterButtons)
        {
            int width = 0;
            foreach (Control control in Controls)
            {
                bool visible = control.Visible;
                if (control == _summaryLabel)
                    visible = includeSummary;
                else if (control == _pageSizeCombo)
                    visible = includePageSize;
                else if (control == _firstButton || control == _lastButton)
                    visible = includeOuterButtons;

                if (visible)
                    width += control.Width + control.Margin.Left + control.Margin.Right;
            }

            return width;
        }

        private void ApplyEnabledState(Button button)
        {
            button.ForeColor = button.Enabled ? DS.Slate800 : DS.Slate400;
            button.BackColor = button.Enabled ? Color.White : DS.BgPage;
        }

        protected virtual void OnPageChanged()
        {
            EventHandler handler = PageChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected virtual void OnPageSizeChanged()
        {
            EventHandler handler = PageSizeChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private static string T(string key)
        {
            return LanguageManager.Get(key);
        }
    }
}
