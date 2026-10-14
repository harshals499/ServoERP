using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>
    /// Quotation dashboard surfaces are the application visual contract. This pass only
    /// changes appearance; module-owned layout, validation and business events remain intact.
    /// Watches every container so editors built inside tabs or nested dialogs inherit it.
    /// </summary>
    internal static class QuotationVisualPolicy
    {
        private sealed class State { internal bool Queued; internal bool Card; }
        private static readonly ConditionalWeakTable<Control, State> States =
            new ConditionalWeakTable<Control, State>();

        internal static void Attach(Control root)
        {
            State state;
            if (root != null && !States.TryGetValue(root, out state)) Apply(root);
        }

        internal static void Apply(Control root)
        {
            if (root == null || root.IsDisposed || IsShellNavigation(root)) return;
            State state;
            if (!States.TryGetValue(root, out state))
            {
                state = new State();
                States.Add(root, state);
                root.ControlAdded += ChildAdded;
                root.VisibleChanged += Changed;
                root.HandleCreated += Changed;
            }
            Style(root, state);
            foreach (Control child in root.Controls) Apply(child);
        }

        private static void ChildAdded(object sender, ControlEventArgs e)
        {
            // Constructors often add a card before populating it. Apply after the current
            // UI operation finishes, when labels and their module styles are complete.
            Queue(sender as Control);
        }
        private static void Changed(object sender, EventArgs e) { Queue(sender as Control); }
        private static void Queue(Control control)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated) return;
            State state;
            if (!States.TryGetValue(control, out state) || state.Queued) return;
            state.Queued = true;
            try
            {
                control.BeginInvoke((Action)(() =>
                {
                    state.Queued = false;
                    if (!control.IsDisposed) Apply(control);
                }));
            }
            catch (InvalidOperationException) { state.Queued = false; }
        }

        private static bool IsShellNavigation(Control control)
        {
            string name = (control.Name ?? "").ToLowerInvariant();
            return (control.Parent is MainForm && control.Dock == DockStyle.Left && control.BackColor.GetBrightness() < .6f) || name.Contains("sidebar") || name.Contains("navigationrail") ||
                name == "pnlmenu" || name == "panelsidebar";
        }
        private static bool IsNeutral(Color color)
        {
            if (color == Color.Transparent || color == Color.Empty) return false;
            return color.GetBrightness() > .9f ||
                color == SystemColors.Control || color == SystemColors.Window;
        }
        private static bool IsCard(Panel panel)
        {
            if (panel is FlowLayoutPanel || panel is TableLayoutPanel ||
                panel is SplitterPanel || InputOutlineService.IsInputFrame(panel)) return false;
            string name = (panel.Name ?? "").ToLowerInvariant();
            if (name.Contains("header") || name.Contains("footer") || name.Contains("body") ||
                name.Contains("host") || name.Contains("root") || name.Contains("toolbar")) return false;
            return panel is QuotationForecastCard || name.Contains("card") ||
                name.Contains("kpi") || name.Contains("summary") ||
                CardSurfacePolicy.IsContextMenuCard(panel) ||
                (Convert.ToString(panel.Tag).Contains("NO_DASHBOARD_RESIZE") &&
                 panel.BackColor == Color.White && panel.Width >= 160 && panel.Height >= 90);
        }
        private static void Style(Control control, State state)
        {
            if (control is Form || control is UserControl || control is TabPage)
                control.BackColor = DS.BgPage;
            var accordion = control as AccordionPanel;
            if (accordion != null)
            {
                accordion.BackColor = DS.BgCard;
                accordion.Padding = new Padding(1);
                accordion.HeaderTextColor = DS.Slate900;
                accordion.HeaderBorderColor = DS.Border;
                accordion.HeaderFontSize = 10;
                if (!state.Card)
                {
                    state.Card = true;
                    accordion.Paint += PaintCard;
                }
            }
            var panel = control as Panel;
            if (panel != null)
            {
                if (IsCard(panel))
                {
                    panel.BackColor = DS.BgCard;
                    panel.BorderStyle = BorderStyle.None;
                    if (!state.Card)
                    {
                        state.Card = true;
                        panel.Paint += PaintCard;
                    }
                }
                else if (IsNeutral(panel.BackColor))
                {
                    string name = (panel.Name ?? "").ToLowerInvariant();
                    panel.BackColor = panel is FlowLayoutPanel || panel is TableLayoutPanel ||
                        name.Contains("canvas") || name.Contains("root") ? DS.BgPage : DS.BgCard;
                }
            }
            var button = control as Button;
            if (button != null && !IsShellNavigation(button))
            {
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                if (button.BackColor.GetBrightness() < .6f) button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = button.BackColor.GetBrightness() < .6f
                    ? button.BackColor : DS.Border;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = button.BackColor.GetBrightness() < .6f
                    ? DS.Lighten(button.BackColor, .08f) : DS.Primary50;
            }
            var label = control as Label;
            if (label != null && IsNeutral(label.BackColor))
                label.BackColor = Color.Transparent;
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = DS.BgCard;
                grid.BorderStyle = BorderStyle.None;
                grid.GridColor = Color.FromArgb(232, 237, 244);
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = DS.Slate500;
                grid.DefaultCellStyle.SelectionBackColor = DS.Primary50;
                grid.DefaultCellStyle.SelectionForeColor = DS.Slate900;
                grid.AlternatingRowsDefaultCellStyle.BackColor = DS.BgSubtle;
                grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DS.Primary50;
                grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = DS.Slate900;
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    column.DefaultCellStyle.SelectionBackColor = DS.Primary50;
                    column.DefaultCellStyle.SelectionForeColor = DS.Slate900;
                }
            }
        }
        private static void PaintCard(object sender, PaintEventArgs e)
        {
            var panel = (Control)sender;
            DS.DrawCleanBorder(e.Graphics, panel.ClientRectangle, 9, DS.Border);
        }
    }
}

