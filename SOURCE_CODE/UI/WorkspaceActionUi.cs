using System;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class WorkspaceActionUi
    {
        internal static void BindDashboardResize(Control host, Action refresh, Func<bool> canRefresh)
        {
            var timer = new Timer { Interval = 180 };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                if (!host.IsDisposed && canRefresh()) refresh();
            };
            host.Resize += (sender, args) => { timer.Stop(); timer.Start(); };
            host.Disposed += (sender, args) => timer.Dispose();
        }

        internal static Button CreateClearFilters(Action reset)
        {
            if (reset == null) throw new ArgumentNullException(nameof(reset));
            var button = DS.GhostBtn("Clear Filters", 118, 34);
            button.Name = "ClearWorkspaceFiltersButton";
            button.Click += (sender, args) => reset();
            return button;
        }

        internal static FlowLayoutPanel CreateSelectionActions(DataGridView grid)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, WrapContents = false };
            var select = DS.GhostBtn("Select all shown", 136, 32);
            var clear = DS.GhostBtn("Clear Selection", 124, 32);
            var count = new Label { AutoSize = true, Padding = new Padding(8), Text = "0 selected" };
            select.Name = "SelectAllShownButton";
            clear.Name = "ClearRowSelectionButton";
            select.Click += (sender, args) =>
            {
                if (!grid.MultiSelect) return;
                grid.ClearSelection();
                foreach (DataGridViewRow row in grid.Rows)
                    if (row.Visible && !row.IsNewRow) row.Selected = true;
            };
            clear.Click += (sender, args) => grid.ClearSelection();
            grid.SelectionChanged += (sender, args) => count.Text = grid.SelectedRows.Cast<DataGridViewRow>().Count(row => !row.IsNewRow) + " selected (shown records only)";
            panel.Controls.Add(select); panel.Controls.Add(clear); panel.Controls.Add(count);
            return panel;
        }

        internal static Button FindSaveButton(Control root)
        {
            var candidates = Descendants(root).OfType<Button>().Where(button => button.Visible && button.Enabled && IsSaveAction(button.Text)).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static bool IsSaveAction(string text)
        {
            string label = (text ?? string.Empty).Trim().TrimStart('+').Trim();
            return (label.Equals("Save", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Save ", StringComparison.OrdinalIgnoreCase))
                && !label.StartsWith("Save PDF", StringComparison.OrdinalIgnoreCase)
                && !label.StartsWith("Save View", StringComparison.OrdinalIgnoreCase)
                && !label.StartsWith("Save as", StringComparison.OrdinalIgnoreCase);
        }

        internal static System.Collections.Generic.IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in Descendants(child)) yield return descendant;
            }
        }
    }
}
