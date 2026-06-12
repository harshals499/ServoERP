using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class NotificationCenterDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private const int PageSize = 10;

        private readonly Action<string> _navigate;
        private readonly NotificationCenterService _service = new NotificationCenterService();
        private readonly DataGridView _grid;
        private readonly Label _summary;
        private readonly FlowLayoutPanel _pager;
        private readonly VScrollBar _scrollBar;
        private readonly Button _openButton;
        private readonly Button _dismissButton;
        private readonly Button _refreshButton;

        private List<FoundationNotification> _notifications = new List<FoundationNotification>();
        private int _pageIndex;

        public NotificationCenterDialog(Action<string> navigate)
        {
            _navigate = navigate;
            Text = BrandingService.WindowTitle("Alerts & Notifications");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 560);
            MinimumSize = new Size(760, 420);
            BackColor = Color.White;
            Padding = new Padding(10);

            _grid = BuildGrid();
            _grid.CellDoubleClick += (s, e) => OpenSelected();
            _grid.SelectionChanged += (s, e) => UpdateActionState();
            _grid.CellPainting += Grid_CellPainting;

            _scrollBar = new VScrollBar
            {
                Name = "vScrollNotificationRows",
                Dock = DockStyle.Right,
                Width = SystemInformation.VerticalScrollBarWidth,
                Minimum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = true
            };
            _scrollBar.ValueChanged += (s, e) =>
            {
                if (_scrollBar.Value != _pageIndex)
                {
                    _pageIndex = _scrollBar.Value;
                    RenderPage(false);
                }
            };

            _summary = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 330,
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f),
                ForeColor = DS.Slate700,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _pager = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Dock = DockStyle.None,
                Anchor = AnchorStyles.None,
                BackColor = Color.White,
                Margin = new Padding(0)
            };

            _refreshButton = MakeActionButton("Refresh", (s, e) => LoadNotifications());
            _dismissButton = MakeActionButton("Dismiss", (s, e) => DismissSelected());
            _openButton = MakePrimaryActionButton("Open", (s, e) => OpenSelected());

            Panel bottom = BuildBottomBar();
            Panel gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };
            gridHost.Controls.Add(_grid);
            gridHost.Controls.Add(_scrollBar);
            Controls.Add(gridHost);
            Controls.Add(bottom);

            Load += (s, e) => LoadNotifications();
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                ColumnHeadersHeight = 34,
                RowTemplate = { Height = 28 },
                ScrollBars = ScrollBars.Vertical,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(239, 242, 247),
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f),
                ForeColor = DS.Slate900
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate700;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 250, 252);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = DS.Slate700;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
            grid.DefaultCellStyle.SelectionForeColor = DS.Slate900;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "Priority", Width = 98, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", Width = 150, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reference", HeaderText = "Reference / Description", Width = 245, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", HeaderText = "Details", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", Width = 120, SortMode = DataGridViewColumnSortMode.NotSortable });
            return grid;
        }

        private Panel BuildBottomBar()
        {
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White,
                Padding = new Padding(0, 8, 0, 4)
            };

            Panel top = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.White };
            Panel pagerHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            pagerHost.Controls.Add(_pager);
            pagerHost.Resize += (s, e) => CenterPager(pagerHost);
            top.Controls.Add(pagerHost);
            top.Controls.Add(_summary);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0),
                BackColor = Color.White
            };
            actions.Controls.Add(_openButton);
            actions.Controls.Add(_dismissButton);
            actions.Controls.Add(_refreshButton);

            bottom.Controls.Add(actions);
            bottom.Controls.Add(top);
            return bottom;
        }

        private void LoadNotifications()
        {
            try
            {
                _notifications = _service.GetActiveNotifications(120);
                _pageIndex = Math.Min(_pageIndex, Math.Max(0, PageCount() - 1));
                RenderPage(true);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("NotificationCenterDialog.LoadNotifications", ex);
                _notifications = new List<FoundationNotification>();
                _grid.Rows.Clear();
                _summary.Text = "Notifications could not be loaded.";
                MessageBox.Show(this, "Notifications could not be loaded. Please try Refresh again.", BrandingService.WindowTitle("Notifications"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RenderPage(bool syncScrollBar = true)
        {
            _grid.Rows.Clear();
            List<FoundationNotification> page = _notifications
                .Skip(_pageIndex * PageSize)
                .Take(PageSize)
                .ToList();

            foreach (FoundationNotification n in page)
            {
                int rowIndex = _grid.Rows.Add(
                    Safe(n.Severity, "Low"),
                    Safe(n.Module, "General"),
                    Safe(n.Title, "Notification"),
                    Safe(n.Detail, "Needs attention"),
                    n.CreatedAt.ToString("dd MMM yyyy"));
                _grid.Rows[rowIndex].Tag = n;
            }

            if (_grid.Rows.Count > 0)
                _grid.Rows[0].Selected = true;

            UpdateSummary();
            BuildPager();
            SyncScrollBar(syncScrollBar);
            UpdateActionState();
        }

        private void SyncScrollBar(bool syncValue)
        {
            int pages = PageCount();
            _scrollBar.Maximum = Math.Max(0, pages - 1);
            _scrollBar.Enabled = pages > 1;

            if (syncValue)
                _scrollBar.Value = Math.Min(_scrollBar.Maximum, Math.Max(_scrollBar.Minimum, _pageIndex));
        }

        private void UpdateSummary()
        {
            if (_notifications.Count == 0)
            {
                _summary.Text = "No active notifications.";
                return;
            }

            int start = (_pageIndex * PageSize) + 1;
            int end = Math.Min(_notifications.Count, start + PageSize - 1);
            _summary.Text = "Showing " + start.ToString() + " to " + end.ToString() + " of " + _notifications.Count.ToString() + " notifications";
        }

        private void BuildPager()
        {
            _pager.SuspendLayout();
            try
            {
                _pager.Controls.Clear();
                int pages = PageCount();
                if (pages <= 1)
                    return;

                _pager.Controls.Add(MakePageButton("<", Math.Max(0, _pageIndex - 1), _pageIndex > 0));
                for (int i = 0; i < pages; i++)
                {
                    if (i >= 4 && i < pages - 1)
                    {
                        if (i == 4)
                            _pager.Controls.Add(MakeEllipsis());
                        continue;
                    }

                    _pager.Controls.Add(MakePageButton((i + 1).ToString(), i, true));
                }
                _pager.Controls.Add(MakePageButton(">", Math.Min(pages - 1, _pageIndex + 1), _pageIndex < pages - 1));
            }
            finally
            {
                _pager.ResumeLayout(true);
                if (_pager.Parent != null)
                    CenterPager(_pager.Parent);
            }
        }

        private void CenterPager(Control host)
        {
            if (host == null || _pager == null)
                return;

            _pager.Location = new Point(Math.Max(0, (host.Width - _pager.Width) / 2), 3);
        }

        private Button MakePageButton(string text, int pageIndex, bool enabled)
        {
            Button button = new Button
            {
                Text = text,
                Width = 26,
                Height = 26,
                Enabled = enabled,
                FlatStyle = FlatStyle.Flat,
                BackColor = pageIndex == _pageIndex && text.All(char.IsDigit) ? Color.FromArgb(99, 64, 218) : Color.White,
                ForeColor = pageIndex == _pageIndex && text.All(char.IsDigit) ? Color.White : DS.Slate700,
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.4f, FontStyle.Bold),
                Margin = new Padding(3, 0, 3, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            button.Click += (s, e) =>
            {
                _pageIndex = pageIndex;
                RenderPage(true);
            };
            return button;
        }

        private static Label MakeEllipsis()
        {
            return new Label
            {
                Text = "...",
                Width = 22,
                Height = 26,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = DS.Slate500,
                Margin = new Padding(2, 0, 2, 0)
            };
        }

        private void OpenSelected()
        {
            FoundationNotification selected = SelectedNotification();
            if (selected == null)
                return;

            Hide();
            NavigationHelper.OpenNotification(this, selected, _navigate);
            Close();
        }

        private void DismissSelected()
        {
            FoundationNotification selected = SelectedNotification();
            if (selected == null)
                return;

            _service.Dismiss(selected);
            LoadNotifications();
        }

        private FoundationNotification SelectedNotification()
        {
            if (_grid.SelectedRows.Count == 0)
                return null;

            return _grid.SelectedRows[0].Tag as FoundationNotification;
        }

        private void UpdateActionState()
        {
            bool hasSelection = SelectedNotification() != null;
            _openButton.Enabled = hasSelection;
            _dismissButton.Enabled = hasSelection;
        }

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (_grid.Columns[e.ColumnIndex].Name == "Priority")
            {
                e.Handled = true;
                PaintPriorityCell(e);
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "Category")
            {
                e.Handled = true;
                PaintCategoryCell(e);
                return;
            }
        }

        private void PaintPriorityCell(DataGridViewCellPaintingEventArgs e)
        {
            string severity = Convert.ToString(e.Value);
            Color color = SeverityColor(severity);
            e.PaintBackground(e.CellBounds, true);

            Rectangle dot = new Rectangle(e.CellBounds.Left + 12, e.CellBounds.Top + 11, 7, 7);
            using (Brush brush = new SolidBrush(color))
                e.Graphics.FillEllipse(brush, dot);

            Rectangle pill = new Rectangle(e.CellBounds.Left + 30, e.CellBounds.Top + 6, 48, 18);
            using (GraphicsPath path = DS.RoundedRect(pill, 9))
            using (Brush brush = new SolidBrush(Blend(color, 0.90f)))
                e.Graphics.FillPath(brush, path);

            using (Font font = new Font(LanguageManager.GetUiFontFamily(), 7.2f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Safe(severity, "Low"), font, pill, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        }

        private void PaintCategoryCell(DataGridViewCellPaintingEventArgs e)
        {
            string module = Convert.ToString(e.Value);
            Color color = CategoryColor(module);
            e.PaintBackground(e.CellBounds, true);

            Rectangle icon = new Rectangle(e.CellBounds.Left + 10, e.CellBounds.Top + 8, 12, 12);
            using (Pen pen = new Pen(color, 1.5f))
                e.Graphics.DrawRectangle(pen, icon);
            using (Brush brush = new SolidBrush(color))
                e.Graphics.FillRectangle(brush, icon.Left + 4, icon.Top + 4, 4, 4);

            Rectangle text = new Rectangle(e.CellBounds.Left + 30, e.CellBounds.Top + 1, e.CellBounds.Width - 34, e.CellBounds.Height - 2);
            TextRenderer.DrawText(e.Graphics, Safe(module, "General"), _grid.Font, text, DS.Slate900, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        }

        private static Button MakeActionButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = 116,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = DS.Slate800,
                Font = new Font(LanguageManager.GetUiFontFamily(), 8.2f, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = DS.Border;
            button.FlatAppearance.BorderSize = 1;
            button.Click += onClick;
            return button;
        }

        private static Button MakePrimaryActionButton(string text, EventHandler onClick)
        {
            var button = MakeActionButton(text, onClick);
            button.BackColor = Color.FromArgb(99, 64, 218);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Color.FromArgb(99, 64, 218);
            return button;
        }

        private int PageCount()
        {
            return Math.Max(1, (int)Math.Ceiling(_notifications.Count / (double)PageSize));
        }

        private static string Safe(string text, string fallback)
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }

        private static Color SeverityColor(string severity)
        {
            string value = Safe(severity, string.Empty).ToUpperInvariant();
            if (value == "CRITICAL" || value == "HIGH") return Color.FromArgb(220, 38, 38);
            if (value == "MEDIUM") return Color.FromArgb(249, 115, 22);
            if (value == "LOW") return DS.Primary600;
            return DS.Slate500;
        }

        private static Color CategoryColor(string module)
        {
            string value = Safe(module, string.Empty).ToUpperInvariant();
            if (value.Contains("INVENTORY")) return Color.FromArgb(249, 115, 22);
            if (value.Contains("INVOICE") || value.Contains("PAYMENT")) return Color.FromArgb(16, 185, 129);
            if (value.Contains("TECH") || value.Contains("EMPLOYEE")) return Color.FromArgb(124, 58, 237);
            if (value.Contains("SERVICE")) return Color.FromArgb(20, 184, 166);
            return Color.FromArgb(37, 99, 235);
        }

        private static Color Blend(Color color, float amount)
        {
            return Color.FromArgb(
                color.R + (int)((255 - color.R) * amount),
                color.G + (int)((255 - color.G) * amount),
                color.B + (int)((255 - color.B) * amount));
        }
    }
}
