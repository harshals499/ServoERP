using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class RecordDeletionUi
    {
        public static DialogResult ConfirmPermanentDelete(IWin32Window owner, string recordType, string recordLabel, string impact)
        {
            string label = string.IsNullOrWhiteSpace(recordLabel) ? "this " + recordType.ToLowerInvariant() : recordLabel;
            string message = "Permanently delete " + label + "?";
            if (!string.IsNullOrWhiteSpace(impact))
                message += Environment.NewLine + Environment.NewLine + impact;
            message += Environment.NewLine + Environment.NewLine + "This cannot be undone.";

            return MessageBox.Show(owner, message, BrandingService.WindowTitle("Delete " + recordType), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        }

        public static ToolStripMenuItem AddDeleteMenuItem(ContextMenuStrip menu, EventHandler clickHandler)
        {
            if (menu == null)
                throw new ArgumentNullException(nameof(menu));
            if (clickHandler == null)
                throw new ArgumentNullException(nameof(clickHandler));

            ToolStripMenuItem item = new ToolStripMenuItem("Delete");
            item.ForeColor = Color.FromArgb(185, 28, 28);
            item.ShortcutKeys = Keys.Delete;
            item.ShowShortcutKeys = true;
            item.Click += clickHandler;
            menu.Items.Add(item);
            return item;
        }

        public static void BindDeleteShortcut(Control scope, Func<Task> deleteAsync, Func<bool> canDelete)
        {
            if (scope == null || deleteAsync == null)
                return;

            scope.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Delete || e.Control || e.Alt || e.Shift)
                    return;
                if (canDelete != null && !canDelete())
                    return;

                e.Handled = true;
                e.SuppressKeyPress = true;
                await deleteAsync();
            };
        }
    }
}
