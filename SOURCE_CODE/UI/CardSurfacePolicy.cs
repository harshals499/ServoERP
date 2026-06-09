using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class CardSurfacePolicy
    {
        public const string ResetButtonName = "__servoerpResetLayoutButton";
        public const string QuotationTileHeaderName = "__servoerpQuoteDashTileHeader";
        public const string QuotationTileContentName = "__servoerpQuoteDashTileContent";

        public static bool IsDashboardLayoutCard(Control control)
        {
            if (!IsBaseCardCandidate(control))
                return false;
            if (control is DashboardDeptCard)
                return false;

            if (control is ResizableCard || control is DraggableCard)
                return true;

            if (control is GroupBox)
                return LooksExplicitlyLikeDashboardCard(control);

            Panel panel = control as Panel;
            if (panel == null)
                return false;

            string name = (panel.Name ?? string.Empty).ToUpperInvariant();
            string metadata = BuildMetadata(panel);
            if (ContainsAny(metadata, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BANNER", "CONTENT", "BODY", "ROOT", "HOST", "WORKSPACE", "CANVAS", "SCROLL", "WORKFLOW", "TRACKER", "STEPPER", "GUIDE"))
                return false;
            if (ContainsAny(metadata, "ROW", "CELL", "FIELD", "FILTER", "INPUT", "EDITOR", "FORM", "DETAIL", "SECTION", "STATUS", "BADGE", "PILL", "LIST", "GRID", "TABLE", "ACTIONS", "ACTION"))
                return false;
            if (LooksLikeWorkflowPanel(panel))
                return false;
            if (panel.Dock == DockStyle.Fill && (panel.Parent is Form || panel.Parent is UserControl || panel.Parent is SplitterPanel || panel.Parent is TabPage))
                return false;
            if (panel.Dock == DockStyle.Fill &&
                string.IsNullOrWhiteSpace(panel.Name) &&
                CountDescendants(panel, IsEditableInput) > 0 &&
                !LooksExplicitlyLikeDashboardCard(panel))
                return false;
            if (panel.Dock == DockStyle.Top && panel.Height <= 110)
                return false;
            if (panel.Top <= 12 && panel.Height <= 110 && ContainsToolbarChild(panel))
                return false;

            return LooksExplicitlyLikeDashboardCard(panel) || LooksLikeVisualCardSurface(panel);
        }

        public static bool IsContextMenuCard(Control control)
        {
            if (control == null || control.IsDisposed)
                return false;
            if (ContainsAny(BuildMetadata(control), "NO_CARD_SURFACE", "NO_DASHBOARD_RESIZE"))
                return false;
            if (control is FlowLayoutPanel || control is TableLayoutPanel || control is SplitContainer || control is SplitterPanel)
                return false;
            if (control is DashboardDeptCard)
                return true;

            if (IsUtilityControl(control) || control is Form || control is TabPage || control is DataGridView || control is ToolStrip)
                return false;
            if (HasQuotationDashboardTileAncestor(control) || IsEditorFieldContainer(control))
                return false;

            string typeName = control.GetType().Name;
            if (typeName.IndexOf("Card", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            Panel panel = control as Panel;
            if (panel == null)
                return false;

            string name = (panel.Name ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(name, "workflow", "tracker", "stepper", "guide"))
                return false;
            if (LooksLikeWorkflowPanel(panel))
                return false;
            if (name.Contains("card") || name.Contains("tile") || name.Contains("summary") || name.Contains("kpi") || name.Contains("widget"))
                return true;

            return IsExplicitCardTag(panel.Tag) || LooksLikeVisualCardSurface(panel);
        }

        public static bool IsUtilityControl(Control control)
        {
            if (control == null)
                return false;

            string name = control.Name ?? string.Empty;
            return string.Equals(name, ResetButtonName, StringComparison.Ordinal) ||
                   InputOutlineService.IsInputFrame(control) ||
                   string.Equals(name, CardResizeGripService.CornerGripName, StringComparison.Ordinal) ||
                   string.Equals(name, CardResizeGripService.HeightGripName, StringComparison.Ordinal) ||
                   string.Equals(name, CardResizeGripService.LockBadgeName, StringComparison.Ordinal) ||
                   string.Equals(name, QuotationTileHeaderName, StringComparison.Ordinal) ||
                   string.Equals(name, QuotationTileContentName, StringComparison.Ordinal);
        }

        public static bool ShouldSkipRightClickSurface(Control control)
        {
            return control is TextBoxBase
                || control is ComboBox
                || control is DataGridView
                || control is ListView
                || control is TreeView
                || control is DateTimePicker
                || control is NumericUpDown
                || control is Button
                || control is LinkLabel
                || control is TabControl
                || control is ToolStrip
                || control is RichTextBox
                || IsUtilityControl(control);
        }

        public static bool IsEditorFieldContainer(Control control)
        {
            if (control == null)
                return false;

            string name = ((control.Name ?? string.Empty) + " " + (control.Tag ?? string.Empty)).ToUpperInvariant();
            if (IsExplicitCardTag(control.Tag))
                return false;
            if (ContainsAny(name, "FIELD", "EDITOR", "INPUT", "FORMROW", "FORM_ROW"))
                return true;

            int inputCount = CountDescendants(control, IsEditableInput);
            if (inputCount == 0)
                return false;

            bool compact = control.Height <= 190;
            bool mostlyInputs = inputCount >= CountDescendants(control, c => c is Button || c is DataGridView || c is ListView);
            bool hasFieldLabel = control.Controls.OfType<Label>().Any(label => !string.IsNullOrWhiteSpace(label.Text) && label.Text.Length <= 80);
            return compact || (hasFieldLabel && mostlyInputs);
        }

        public static bool HasEditorFieldAncestor(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            while (parent != null)
            {
                if (IsEditorFieldContainer(parent))
                    return true;
                parent = parent.Parent;
            }

            return false;
        }

        public static bool IsResizableCardSurface(Control control)
        {
            return control is ResizableCard ||
                   string.Equals(control == null ? string.Empty : control.GetType().Name, "QuotationDashboardTile", StringComparison.Ordinal);
        }

        public static bool HasQuotationDashboardTileAncestor(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            while (parent != null)
            {
                if (parent is QuotationDashboardTile)
                    return true;
                parent = parent.Parent;
            }

            return false;
        }

        public static bool LooksLikeWorkflowPanel(Control control)
        {
            if (control == null || control.Height > 120)
                return false;

            string labels = string.Join(" ", control.Controls.OfType<Label>().Select(label => label.Text ?? string.Empty)).ToUpperInvariant();
            return ContainsAny(labels, "DRAFT", "QUOTE CREATED", "MATERIAL CHECK", "APPROVAL", "SENT TO CUSTOMER", "CUSTOMER ACCEPTED", "CONVERTED");
        }

        public static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
                return false;

            foreach (string token in tokens)
            {
                if (!string.IsNullOrEmpty(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public static int CountDescendants(Control control, Func<Control, bool> predicate)
        {
            if (control == null || predicate == null)
                return 0;

            int count = 0;
            foreach (Control child in control.Controls)
            {
                if (predicate(child))
                    count++;
                count += CountDescendants(child, predicate);
            }

            return count;
        }

        private static bool IsBaseCardCandidate(Control control)
        {
            if (control == null || control.IsDisposed || control is Form || control is UserControl || control is DataGridView || control is ToolStrip)
                return false;
            string metadata = BuildMetadata(control);
            if (ContainsAny(metadata, "NO_DASHBOARD_RESIZE"))
                return false;
            if (control is DashboardDeptCard)
                return false;
            if (IsFixedEditorSurface(control))
                return false;
            if (IsUtilityControl(control) || HasQuotationDashboardTileAncestor(control))
                return false;
            if (control is Button || control is TextBoxBase || control is ComboBox || control is DateTimePicker || control is NumericUpDown || control is LinkLabel || control is TabControl)
                return false;
            if (control is FlowLayoutPanel || control is TableLayoutPanel || control is SplitContainer || control is SplitterPanel)
                return false;
            if (control.Parent == null || control.Width < 120 || control.Height < 70)
                return false;
            if (control.Dock == DockStyle.Fill && !LooksExplicitlyLikeDashboardCard(control))
                return false;
            if (control.Width >= 800 && control.Height >= 500 && !LooksExplicitlyLikeDashboardCard(control))
                return false;
            if (control.Height <= 120 && ContainsAny(BuildLabelText(control), "SEARCH", "FILTER", "REVIEW", "LIST", "MANDATORY", "DETAILS", "MOVEMENT", "QUANTITY", "TRANSFER", "SELECTION", "SUPPLIER", "REQUEST", "PURCHASE", "STOCK"))
                return false;
            if (control is Panel && control.Controls.Count == 0 && !LooksExplicitlyLikeDashboardCard(control))
                return false;
            if (IsEditorFieldContainer(control) || HasEditorFieldAncestor(control))
                return false;

            return true;
        }

        private static bool IsFixedEditorSurface(Control control)
        {
            string metadata = BuildMetadata(control);
            return ContainsAny(metadata, "PAYMENT_EDITOR_SECTION", "QUOTATION_EDITOR_SECTION", "FIXED_EDITOR_SECTION", "NO_DASHBOARD_RESIZE");
        }

        private static bool LooksExplicitlyLikeDashboardCard(Control control)
        {
            if (control == null)
                return false;

            if (IsExplicitCardTag(control.Tag))
                return true;

            string metadata = BuildMetadata(control);
            return ContainsAny(metadata, "CARD", "TILE", "KPI", "WIDGET", "DASHCARD", "DASH_CARD", "DASH-CARD", "METRIC", "STATCARD", "STAT_CARD", "STAT-CARD", "SUMMARYCARD", "SUMMARY_CARD", "SUMMARY-CARD");
        }

        private static bool LooksLikeVisualCardSurface(Panel panel)
        {
            if (panel == null || panel.Parent == null)
                return false;

            if (panel.Width < 160 || panel.Height < 90)
                return false;

            string metadata = BuildMetadata(panel);
            if (ContainsAny(metadata, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BANNER", "CONTENT", "BODY", "ROOT", "HOST", "WORKSPACE", "CANVAS", "SCROLL", "WORKFLOW", "TRACKER", "STEPPER", "GUIDE"))
                return false;
            if (ContainsAny(metadata, "ROW", "CELL", "FIELD", "FILTER", "INPUT", "EDITOR", "FORMROW", "FORM_ROW", "STATUS", "BADGE", "PILL", "ACTIONS", "ACTION"))
                return false;

            bool whiteOrCardSurface =
                panel.BackColor == Color.White ||
                panel.BackColor == DS.White ||
                panel.BackColor == DS.BgCard ||
                panel.BackColor == SystemColors.Window;
            if (!whiteOrCardSurface)
                return false;

            bool bounded =
                panel.Dock != DockStyle.Fill ||
                panel.Margin != Padding.Empty ||
                panel.Padding != Padding.Empty ||
                panel.Parent is FlowLayoutPanel ||
                panel.Parent is TableLayoutPanel;
            if (!bounded)
                return false;

            int labels = CountDescendants(panel, c => c is Label);
            int buttons = CountDescendants(panel, c => c is Button);
            int grids = CountDescendants(panel, c => c is DataGridView || c is ListView || c is TreeView);
            int inputs = CountDescendants(panel, IsEditableInput);
            int nestedCards = panel.Controls.Cast<Control>().Count(IsDashboardLayoutCard);

            if (inputs > 0 && grids == 0 && nestedCards == 0)
                return false;

            bool hasCardContent = labels > 0 || buttons > 0 || grids > 0 || nestedCards > 0;
            bool mostlyEditor = inputs > 0 && inputs >= labels + buttons + grids;
            return hasCardContent && !mostlyEditor;
        }

        private static bool IsExplicitCardTag(object tag)
        {
            string value = tag == null ? string.Empty : tag.ToString();
            return string.Equals(value, "card", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "dashboard-card", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "dash-card", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "settings-card", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "metric-card", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "kpi-card", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildMetadata(Control control)
        {
            if (control == null)
                return string.Empty;

            return ((control.Name ?? string.Empty) + " " +
                    (control.Tag == null ? string.Empty : control.Tag.ToString()) + " " +
                    (control.Text ?? string.Empty) + " " +
                    control.GetType().Name).ToUpperInvariant();
        }

        private static string BuildLabelText(Control control)
        {
            if (control == null)
                return string.Empty;

            return string.Join(" ", control.Controls.OfType<Label>().Select(label => label.Text ?? string.Empty)).ToUpperInvariant();
        }

        private static bool ContainsToolbarChild(Control control)
        {
            if (control == null)
                return false;

            foreach (Control child in control.Controls)
            {
                if (child is Button || child is TextBoxBase || child is ComboBox || child is DateTimePicker || child is CheckBox)
                    return true;
            }

            return false;
        }

        private static bool IsEditableInput(Control control)
        {
            return control is TextBoxBase ||
                   control is ComboBox ||
                   control is DateTimePicker ||
                   control is NumericUpDown ||
                   control is CheckBox ||
                   control is RadioButton;
        }
    }
}
