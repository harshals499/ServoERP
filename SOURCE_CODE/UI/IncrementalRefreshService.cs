using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public static class IncrementalRefreshService
    {
        public static bool TryUpdateBoundRow<T>(DataGridView grid, int rowId, T freshData, Func<T, int> idSelector)
        {
            if (grid == null || freshData == null || idSelector == null)
                return false;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row == null || row.IsNewRow)
                    continue;

                T rowData = row.DataBoundItem is T ? (T)row.DataBoundItem : default(T);
                if (Equals(rowData, default(T)))
                    continue;

                if (idSelector(rowData) != rowId)
                    continue;

                row.DataGridView?.InvalidateRow(row.Index);
                return true;
            }

            return false;
        }

        public static bool TryUpdateVirtualRow<T>(DataGridView grid, IList<T> visibleItems, int rowId, T freshData, Func<T, int> idSelector)
        {
            if (grid == null || visibleItems == null || freshData == null || idSelector == null)
                return false;

            int index = visibleItems
                .Select((item, itemIndex) => new { item, itemIndex })
                .Where(x => idSelector(x.item) == rowId)
                .Select(x => x.itemIndex)
                .DefaultIfEmpty(-1)
                .First();

            if (index < 0)
                return false;

            visibleItems[index] = freshData;
            grid.InvalidateRow(index);
            return true;
        }
    }
}
