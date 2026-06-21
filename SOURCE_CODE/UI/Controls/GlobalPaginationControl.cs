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
        private readonly FlowLayoutPanel _rowFlow;
        private readonly Button _firstButton;
        private readonly Button _previousButton;
        private readonly Button _nextButton;
        private readonly Button _lastButton;
        private readonly TextBox _pageBox;
        private readonly Label _pageLabel;
        private readonly Label _totalPagesLabel;
        private readonly Label _summaryLabel;
        private readonly Label _rowsLabel;
        private readonly Label _leftSeparatorLabel;
        private readonly Label _rightSeparatorLabel;
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
            Width = 760;
            BackColor = Color.Transparent;
            MinimumSize = new Size(280, 40);

            _rowFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0)
            };

            _summaryLabel = new Label
            {
                AutoSize = false,
                Width = 160,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ModernERPTheme.Small,
                ForeColor = DS.Slate600,
                Margin = new Padding(0, 0, 10, 0)
            };

            _leftSeparatorLabel = CreateInlineLabel("|", 10, FontStyle.Bold, DS.Slate400, new Padding(0, 0, 10, 0));

            _pageSizeCombo = new ComboBox
            {
                Width = 68,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ModernERPTheme.Small,
                FlatStyle = FlatStyle.Standard,
                IntegralHeight = false,
                Margin = new Padding(6, 0, 0, 0),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            _pageSizeCombo.Items.AddRange(new object[] { "10", "25", "50", "100" });
            _pageSizeCombo.SelectedIndexChanged += PageSizeComboSelectedIndexChanged;

            _firstButton = NavButton("<<", T("First page"));
            _previousButton = NavButton("<", T("Previous page"));
            _pageLabel = CreateInlineLabel(T("Page"), 34, FontStyle.Regular, DS.Slate700, new Padding(6, 0, 4, 0));
            _pageBox = new TextBox
            {
                Width = 42,
                Height = 26,
                TextAlign = HorizontalAlignment.Center,
                Font = ModernERPTheme.SmallBold,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 4, 0),
                Tag = "CUSTOM_INPUT_SHELL"
            };
            _pageBox.KeyDown += PageBoxKeyDown;
            _pageBox.Leave += (s, e) => CommitPageBox();
            _totalPagesLabel = new Label
            {
                Width = 56,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ModernERPTheme.Small,
                ForeColor = DS.Slate600,
                Margin = new Padding(0, 0, 6, 0)
            };
            _nextButton = NavButton(">", T("Next page"));
            _lastButton = NavButton(">>", T("Last page"));
            _rightSeparatorLabel = CreateInlineLabel("|", 10, FontStyle.Bold, DS.Slate400, new Padding(4, 0, 10, 0));
            _rowsLabel = CreateInlineLabel(T("Rows:"), 38, FontStyle.Regular, DS.Slate700, new Padding(0, 0, 4, 0));

            _firstButton.Click += (s, e) => NavigateTo(1);
            _previousButton.Click += (s, e) => NavigateTo(_state.CurrentPage - 1);
            _nextButton.Click += (s, e) => NavigateTo(_state.CurrentPage + 1);
            _lastButton.Click += (s, e) => NavigateTo(Math.Max(1, _state.TotalPages));

            _rowFlow.Controls.Add(_summaryLabel);
            _rowFlow.Controls.Add(_leftSeparatorLabel);
            _rowFlow.Controls.Add(_firstButton);
            _rowFlow.Controls.Add(_previousButton);
            _rowFlow.Controls.Add(_pageLabel);
            _rowFlow.Controls.Add(_pageBox);
            _rowFlow.Controls.Add(_totalPagesLabel);
            _rowFlow.Controls.Add(_nextButton);
            _rowFlow.Controls.Add(_lastButton);
            _rowFlow.Controls.Add(_rightSeparatorLabel);
            _rowFlow.Controls.Add(_rowsLabel);
            _rowFlow.Controls.Add(_pageSizeCombo);

            Controls.Add(_rowFlow);
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
            _summaryLabel.Text = BuildSummaryText();
            UpdateResponsiveWidths();
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
                Width = 34,
                Height = 28,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                FlatStyle = FlatStyle.Flat,
                Font = ModernERPTheme.SmallBold,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 4, 0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            _toolTip.SetToolTip(button, tooltip);
            return button;
        }

        private static Label CreateInlineLabel(string text, int width, FontStyle style, Color foreColor, Padding margin)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = width,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8f, style),
                ForeColor = foreColor,
                Margin = margin
            };
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
            UpdateResponsiveWidths();
            int x = Math.Max(0, (ClientSize.Width - _rowFlow.Width) / 2);
            int y = Math.Max(0, (ClientSize.Height - _rowFlow.Height) / 2);
            _rowFlow.Location = new Point(x, y);
        }

        private void ApplyEnabledState(Button button)
        {
            button.ForeColor = button.Enabled ? DS.Slate800 : DS.Slate400;
            button.BackColor = button.Enabled ? Color.White : DS.BgPage;
        }

        private string BuildSummaryText()
        {
            if (_state.TotalRecords <= 0)
                return "Showing 0-0 of 0";

            return string.Format(
                CultureInfo.CurrentUICulture,
                "Showing {0}-{1} of {2}",
                _state.DisplayFrom,
                _state.DisplayTo,
                _state.TotalRecords);
        }

        private void UpdateResponsiveWidths()
        {
            int available = Math.Max(0, ClientSize.Width);
            bool veryCompact = available > 0 && available <= 340;
            bool compact = available > 0 && available <= 460;

            _summaryLabel.Visible = !compact;
            _leftSeparatorLabel.Visible = !compact;
            _rightSeparatorLabel.Visible = !compact;
            _pageLabel.Visible = !veryCompact;
            _rowsLabel.Visible = !veryCompact;

            int navWidth = veryCompact ? 28 : 34;
            int navMargin = veryCompact ? 2 : 4;
            foreach (Button button in new[] { _firstButton, _previousButton, _nextButton, _lastButton })
            {
                button.Width = navWidth;
                button.Margin = new Padding(0, 0, navMargin, 0);
            }

            _pageBox.Width = veryCompact ? 34 : 42;
            _totalPagesLabel.Width = veryCompact ? 34 : compact ? 42 : 56;
            _pageSizeCombo.Width = veryCompact ? 52 : compact ? 58 : 68;
            _pageSizeCombo.Margin = new Padding(veryCompact ? 2 : 6, 0, 0, 0);

            _pageLabel.Width = veryCompact ? 0 : compact ? 28 : 34;
            _rowsLabel.Width = veryCompact ? 0 : compact ? 30 : 38;
            _leftSeparatorLabel.Width = compact ? 0 : 10;
            _rightSeparatorLabel.Width = compact ? 0 : 10;

            int preferredSummaryWidth = Math.Max(96, Math.Min(230, TextRenderer.MeasureText(_summaryLabel.Text ?? string.Empty, _summaryLabel.Font).Width + 10));
            int reservedWidth = VisibleWidth(_leftSeparatorLabel)
                + VisibleWidth(_firstButton)
                + VisibleWidth(_previousButton)
                + VisibleWidth(_pageLabel)
                + VisibleWidth(_pageBox)
                + VisibleWidth(_totalPagesLabel)
                + VisibleWidth(_nextButton)
                + VisibleWidth(_lastButton)
                + VisibleWidth(_rightSeparatorLabel)
                + VisibleWidth(_rowsLabel)
                + VisibleWidth(_pageSizeCombo);

            int summaryWidth = Math.Max(0, available - reservedWidth - 8);
            _summaryLabel.Width = compact ? 0 : Math.Max(96, Math.Min(preferredSummaryWidth, summaryWidth));
        }

        private static int VisibleWidth(Control control)
        {
            if (control == null || !control.Visible)
                return 0;

            return control.Width + control.Margin.Horizontal;
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
