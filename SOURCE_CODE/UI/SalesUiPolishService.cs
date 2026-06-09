using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Applies sales-specific visual polish without changing sales workflow logic.</summary>
    internal static class SalesUiPolishService
    {
        private static readonly HashSet<Control> BoundRoots = new HashSet<Control>();

        /// <summary>Applies the standard sales visual pass to a page or dialog.</summary>
        public static void Apply(Control root, string salesArea)
        {
            if (root == null || root.IsDisposed)
                return;

            BindRoot(root, salesArea);
            root.BackColor = DS.BgPage;
            UIHelper.ApplyInputStyles(root.Controls);
            UIHelper.ApplyButtonAlignment(root);
            UIHelper.ApplyGlobalScrollAndResize(root);
            SharedUiPrimitives.ApplyToTree(root);
            ApplySalesPolish(root, salesArea);
        }

        /// <summary>Applies the sales visual pass after a page rebuilds its child controls.</summary>
        public static void ApplyAfterRebuild(Control root, string salesArea)
        {
            Apply(root, salesArea);
            if (root == null || root.IsDisposed || !root.IsHandleCreated)
                return;

            root.BeginInvoke((Action)(() =>
            {
                if (!root.IsDisposed)
                    Apply(root, salesArea);
            }));
        }

        private static void BindRoot(Control root, string salesArea)
        {
            if (BoundRoots.Contains(root))
                return;

            BoundRoots.Add(root);
            root.Disposed += (s, e) => BoundRoots.Remove(root);
            root.ControlAdded += (s, e) => Apply(e.Control, salesArea);
        }

        private static void ApplySalesPolish(Control control, string salesArea)
        {
            if (control == null || control.IsDisposed)
                return;

            ApplySalesGrid(control as DataGridView);
            ApplySalesButton(control as Button);
            ApplySalesLabel(control as Label);
            ApplySalesPanel(control as Panel);
            ApplySalesTabControl(control as TabControl);

            foreach (Control child in control.Controls)
                ApplySalesPolish(child, salesArea);
        }

        private static void ApplySalesGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 36);
            grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, 40);
            grid.DefaultCellStyle.Font = DS.Body;
            grid.ColumnHeadersDefaultCellStyle.Font = DS.BodyBold;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.BackgroundColor = Color.White;
            grid.GridColor = DS.Slate200;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.ScrollBars = ScrollBars.Both;
        }

        private static void ApplySalesButton(Button button)
        {
            if (button == null || button.IsDisposed)
                return;

            string text = (button.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!IsCompactCommand(text))
                button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, 112), Math.Max(button.MinimumSize.Height, 36));
            button.Height = Math.Max(button.Height, 36);
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;

            if (!LooksDestructive(text))
                return;

            button.BackColor = Color.White;
            button.ForeColor = DS.Red600;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = DS.Lighten(DS.Red600, 0.55f);
            button.FlatAppearance.MouseOverBackColor = DS.Red50;
            button.FlatAppearance.MouseDownBackColor = DS.Lighten(DS.Red600, 0.82f);
        }

        private static void ApplySalesLabel(Label label)
        {
            if (label == null || label.IsDisposed)
                return;

            string text = (label.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!label.AutoSize && label.Width > 0)
                label.AutoEllipsis = true;

            if (label.Font != null && label.Font.Size < 8f)
                label.Font = new Font(label.Font.FontFamily, 8f, label.Font.Style);
        }

        private static void ApplySalesPanel(Panel panel)
        {
            if (panel == null || panel.IsDisposed)
                return;

            if (panel.BackColor == SystemColors.Control || panel.BackColor == Color.Empty)
                panel.BackColor = DS.BgPage;

            if (LooksLikeSalesCard(panel))
            {
                panel.BackColor = Color.White;
                DS.Rounded(panel, DS.RadiusMd);
            }
        }

        private static void ApplySalesTabControl(TabControl tabs)
        {
            if (tabs == null || tabs.IsDisposed)
                return;

            tabs.Font = DS.Body;
            foreach (TabPage page in tabs.TabPages)
                page.BackColor = DS.BgPage;
        }

        private static bool LooksLikeSalesCard(Panel panel)
        {
            string metadata = ((panel.Name ?? string.Empty) + " " + (panel.Tag == null ? string.Empty : panel.Tag.ToString())).ToLowerInvariant();
            if (metadata.IndexOf("sidebar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                metadata.IndexOf("nav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                metadata.IndexOf("toolbar", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            bool namedCard = ContainsAny(metadata, "card", "summary", "section", "detail", "actions", "workflow", "kpi", "invoice", "payment", "quote", "client");
            bool whiteSurface = panel.BackColor == Color.White || panel.BackColor == DS.White || panel.BackColor == DS.BgCard;
            bool hasContent = panel.Controls.OfType<Label>().Any() ||
                              panel.Controls.OfType<Button>().Any() ||
                              panel.Controls.OfType<DataGridView>().Any() ||
                              panel.Controls.OfType<TableLayoutPanel>().Any();
            return hasContent && (namedCard || whiteSurface);
        }

        private static bool LooksDestructive(string text)
        {
            string key = text.ToLowerInvariant();
            return ContainsAny(key, "delete", "remove", "void", "cancel invoice", "cancel quote");
        }

        private static bool IsCompactCommand(string text)
        {
            string key = (text ?? string.Empty).Trim();
            return key.Length <= 2 || key == "..." || key == "⋯" || key == "⋮";
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null)
                return false;

            foreach (string token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
