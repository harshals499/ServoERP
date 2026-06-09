using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public static class GlobalStatusEditor
    {
        private static readonly HashSet<DataGridView> AttachedGrids = new HashSet<DataGridView>();
        private static readonly StatusUpdateService UpdateService = new StatusUpdateService();

        public static void Attach(DataGridView grid)
        {
            if (grid == null || AttachedGrids.Contains(grid))
                return;

            AttachedGrids.Add(grid);
            grid.Disposed += (s, e) => AttachedGrids.Remove(grid);
            grid.CellDoubleClick += Grid_CellDoubleClick;
            grid.KeyDown += Grid_KeyDown;
            grid.CellFormatting += Grid_CellFormatting;
            grid.CellMouseEnter += Grid_CellMouseEnter;
            grid.CellMouseLeave += Grid_CellMouseLeave;
        }

        public static bool IsEditableStatusCell(DataGridView grid, int rowIndex, int columnIndex)
        {
            return StatusOptionProvider.TryGetContext(grid, rowIndex, columnIndex, out StatusCellContext _);
        }

        private static void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (!IsEditableStatusCell(grid, e.RowIndex, e.ColumnIndex))
                return;

            ShowStatusMenu(grid, e.RowIndex, e.ColumnIndex);
        }

        private static void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid?.CurrentCell == null || grid.IsCurrentCellInEditMode)
                return;

            if (e.KeyCode != Keys.F2 && e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
                return;

            int rowIndex = grid.CurrentCell.RowIndex;
            int columnIndex = grid.CurrentCell.ColumnIndex;
            if (!IsEditableStatusCell(grid, rowIndex, columnIndex))
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            ShowStatusMenu(grid, rowIndex, columnIndex);
        }

        private static void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (!IsEditableStatusCell(grid, e.RowIndex, e.ColumnIndex))
                return;

            if (e.CellStyle != null)
            {
                e.CellStyle.Font = new Font(grid.Font, FontStyle.Bold);
                e.CellStyle.SelectionBackColor = GridTheme.RowSelected;
                e.CellStyle.SelectionForeColor = GridTheme.RowSelectedFore;
            }

            DataGridViewCell cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.ToolTipText = StatusOptionProvider.EditableTooltip;
        }

        private static void Grid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (IsEditableStatusCell(grid, e.RowIndex, e.ColumnIndex))
                grid.Cursor = Cursors.Hand;
        }

        private static void Grid_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid != null)
                grid.Cursor = Cursors.Default;
        }

        private static void ShowStatusMenu(DataGridView grid, int rowIndex, int columnIndex)
        {
            if (!StatusOptionProvider.TryGetContext(grid, rowIndex, columnIndex, out StatusCellContext context))
                return;

            Rectangle cellRect = grid.GetCellDisplayRectangle(columnIndex, rowIndex, true);
            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            foreach (StatusChoice choice in context.Choices)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(choice.DisplayText)
                {
                    Checked = choice.Matches(context.CurrentDisplayValue)
                };
                item.Click += (s, e) => ApplyStatusChoice(context, choice);
                menu.Items.Add(item);
            }

            menu.Closed += (s, e) => menu.Dispose();
            menu.Show(grid, new Point(cellRect.Left, cellRect.Bottom));
        }

        private static void ApplyStatusChoice(StatusCellContext context, StatusChoice choice)
        {
            if (context == null || choice == null)
                return;

            if (choice.Matches(context.CurrentDisplayValue))
                return;

            StatusUpdateResult result = UpdateService.UpdateStatus(context, choice.StoredValue);
            if (!result.Success)
            {
                ShowToast(context.Grid, result.Message, Color.FromArgb(220, 38, 38));
                return;
            }

            UpdateRowValue(context, result.DisplayValue, result.StoredValue);
            ShowToast(context.Grid, context.DisplayName + " status changed to " + result.DisplayValue + ".", DS.Teal600);
        }

        private static void UpdateRowValue(StatusCellContext context, string displayValue, string storedValue)
        {
            if (context?.Row == null || context.Column == null)
                return;

            context.Row.Cells[context.Column.Index].Value = displayValue;
            context.CurrentDisplayValue = displayValue;

            if (context.RowData != null)
            {
                PropertyInfo property = context.RowData.GetType().GetProperty(context.StatusPropertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.CanWrite)
                    property.SetValue(context.RowData, storedValue, null);
            }

            context.Grid?.InvalidateRow(context.Row.Index);
            context.Grid?.Refresh();
        }

        private static void ShowToast(Control owner, string message, Color accent)
        {
            Control host = owner?.FindForm() ?? owner;
            if (host != null && !host.IsDisposed)
                ToastNotification.Show(host, message, accent);
        }
    }
}
