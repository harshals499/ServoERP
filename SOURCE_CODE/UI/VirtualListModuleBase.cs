using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.UI.Controls;

namespace HVAC_Pro_Desktop.UI
{
    public abstract class VirtualListModuleBase<T> : UserControl
    {
        private readonly List<T> _items = new List<T>();
        private readonly List<T> _visibleItems = new List<T>();
        private readonly Label _statusLabel;
        private int _page = 1;
        private int _pageSize = PaginationState.DefaultPageSize;
        private int? _selectedRowId;

        protected VirtualListModuleBase()
        {
            BackColor = DS.BgPage;
            Grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                VirtualMode = true
            };
            Grid.CellValueNeeded += Grid_CellValueNeeded;
            Grid.SelectionChanged += Grid_SelectionChanged;

            Pager = new GlobalPaginationControl { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.White };
            Pager.PageChanged += (s, e) =>
            {
                _page = Pager.CurrentPage;
                RefreshVisibleItems();
            };
            Pager.PageSizeChanged += (s, e) =>
            {
                _pageSize = Pager.PageSize;
                _page = 1;
                RefreshVisibleItems();
            };

            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = DS.Slate500,
                Padding = new Padding(10, 0, 10, 6),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(Grid);
            Controls.Add(_statusLabel);
            Controls.Add(Pager);

            BuildColumns(Grid);
        }

        protected DataGridView Grid { get; private set; }

        public DataGridView ListGrid => Grid;

        protected GlobalPaginationControl Pager { get; private set; }

        protected Label StatusLabel => _statusLabel;

        protected IReadOnlyList<T> Items => _items;

        protected IReadOnlyList<T> VisibleItems => _visibleItems;

        public IList<T> VisibleItemsBuffer => _visibleItems;

        public event Action<T> RowSelected;

        protected abstract void BuildColumns(DataGridView grid);

        protected abstract int GetRowId(T item);

        protected abstract object GetCellValue(T item, string columnName);

        protected virtual string BuildStatusText(int visibleCount, int totalCount)
        {
            return visibleCount.ToString("N0") + " of " + totalCount.ToString("N0") + " records shown.";
        }

        protected void SetPagerVisible(bool visible)
        {
            Pager.Visible = visible;
        }

        protected void SetStatusVisible(bool visible)
        {
            _statusLabel.Visible = visible;
        }

        public void SetItems(IEnumerable<T> items)
        {
            _items.Clear();
            if (items != null)
                _items.AddRange(items);

            _page = PaginationState.NormalizePage(_page, _items.Count, Math.Max(1, _pageSize));
            RefreshVisibleItems();
        }

        public List<T> SnapshotItems()
        {
            return new List<T>(_items);
        }

        public void UpdateItem(int rowId, T freshItem)
        {
            int index = _items.FindIndex(item => GetRowId(item) == rowId);
            if (index >= 0)
                _items[index] = freshItem;

            int visibleIndex = _visibleItems.FindIndex(item => GetRowId(item) == rowId);
            if (visibleIndex >= 0)
            {
                _visibleItems[visibleIndex] = freshItem;
                Grid.InvalidateRow(visibleIndex);
            }
        }

        public int? GetSelectedRowId()
        {
            return _selectedRowId;
        }

        public void SetSelectedRowId(int? rowId)
        {
            _selectedRowId = rowId;
            SyncSelection();
        }

        public int GetVerticalScrollValue()
        {
            return Grid.FirstDisplayedScrollingRowIndex >= 0 ? Grid.FirstDisplayedScrollingRowIndex : 0;
        }

        public void RestoreScrollPosition(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Grid.RowCount)
                return;

            try
            {
                Grid.FirstDisplayedScrollingRowIndex = rowIndex;
            }
            catch
            {
            }
        }

        public ModuleState CaptureState(string pageKey)
        {
            return new ModuleState
            {
                PageKey = pageKey,
                SelectedRowId = _selectedRowId,
                ScrollPosition = GetVerticalScrollValue()
            };
        }

        public void RestoreState(ModuleState state)
        {
            if (state == null)
                return;

            _selectedRowId = state.SelectedRowId;
            RestoreScrollPosition(state.ScrollPosition);
            SyncSelection();
        }

        private void Grid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visibleItems.Count || e.ColumnIndex < 0 || e.ColumnIndex >= Grid.Columns.Count)
                return;

            T item = _visibleItems[e.RowIndex];
            e.Value = GetCellValue(item, Grid.Columns[e.ColumnIndex].Name);
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (Grid.CurrentCell == null || Grid.CurrentCell.RowIndex < 0 || Grid.CurrentCell.RowIndex >= _visibleItems.Count)
                return;

            T item = _visibleItems[Grid.CurrentCell.RowIndex];
            _selectedRowId = GetRowId(item);
            RowSelected?.Invoke(item);
            OnRowSelected(item);
        }

        protected virtual void OnRowSelected(T item)
        {
        }

        private void RefreshVisibleItems()
        {
            _visibleItems.Clear();
            int pageSize = Math.Max(1, _pageSize);
            int page = PaginationState.NormalizePage(_page, _items.Count, pageSize);
            _page = page;

            _visibleItems.AddRange(_items.Skip((page - 1) * pageSize).Take(pageSize));
            Grid.RowCount = _visibleItems.Count;
            Pager.SetState(page, _items.Count, pageSize);
            _statusLabel.Text = BuildStatusText(_visibleItems.Count, _items.Count);
            Grid.ClearSelection();
            SyncSelection();
            Grid.Invalidate();
        }

        private void SyncSelection()
        {
            if (!_selectedRowId.HasValue || _visibleItems.Count == 0)
                return;

            int rowIndex = _visibleItems.FindIndex(item => GetRowId(item) == _selectedRowId.Value);
            if (rowIndex < 0 || rowIndex >= Grid.RowCount)
                return;

            Grid.ClearSelection();
            Grid.CurrentCell = Grid.Rows[rowIndex].Cells.Cast<DataGridViewCell>().FirstOrDefault();
            if (Grid.CurrentCell != null)
                Grid.Rows[rowIndex].Selected = true;
        }
    }
}
