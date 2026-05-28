using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Controls;

namespace HVAC_Pro_Desktop.UI
{
    public enum UiActionVariant
    {
        Primary,
        Secondary,
        Success,
        Warning,
        Danger,
        Ghost
    }

    public static class UIHelper
    {
        private static readonly HashSet<Control> ScrollResizeConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> ScrollConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> ModernizedControls = new HashSet<Control>();
        private static readonly HashSet<Panel> ModernCardPanels = new HashSet<Panel>();
        private static readonly HashSet<string> EmptyClientMessageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> EmptyVendorMessageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void ApplyInputStyles(Control.ControlCollection controls)
        {
            if (controls == null)
                return;

            foreach (Control ctrl in controls)
            {
                ApplyInputStyle(ctrl);

                if (ctrl.HasChildren)
                    ApplyInputStyles(ctrl.Controls);
            }
        }

        public static void ApplyInputStyle(Control ctrl)
        {
            ApplyModernErpStyle(ctrl);

            if (ctrl is TextBox tb)
            {
                tb.BorderStyle = BorderStyle.None;
                tb.BackColor = tb.ReadOnly ? DS.Slate50 : Color.White;
                tb.ForeColor = DS.Slate900;
                tb.Font = DS.Body;
                if (!tb.Multiline)
                    tb.Height = Math.Max(tb.Height, 30);
            }
            else if (ctrl is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Color.White;
                cb.ForeColor = DS.Slate900;
                cb.Font = DS.Body;
                cb.Height = Math.Max(cb.Height, 30);
                cb.AutoCompleteSource = AutoCompleteSource.ListItems;
                cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            }
            else if (ctrl is DateTimePicker dtp)
            {
                if (dtp.Format == DateTimePickerFormat.Custom &&
                    string.IsNullOrEmpty(dtp.CustomFormat))
                {
                    dtp.Format = DateTimePickerFormat.Short;
                }

                dtp.Font = DS.Body;
                dtp.Height = Math.Max(dtp.Height, 30);
            }
            else if (ctrl is NumericUpDown nud)
            {
                nud.BorderStyle = BorderStyle.None;
                nud.BackColor = Color.White;
                nud.ForeColor = DS.Slate900;
                nud.Font = DS.Body;
                nud.Height = Math.Max(nud.Height, 30);
            }
        }

        private static void ApplyModernErpStyle(Control ctrl)
        {
            if (ctrl == null || ctrl.IsDisposed)
                return;

            if (ModernizedControls.Contains(ctrl))
                return;

            ModernizedControls.Add(ctrl);
            ctrl.Disposed += (s, e) => ModernizedControls.Remove(ctrl);

            if (ctrl is DataGridView grid)
            {
                GridTheme.Apply(grid);
                return;
            }

            if (ctrl is Button button)
            {
                ApplyModernButtonStyle(button);
                return;
            }

            if (ctrl is FlowLayoutPanel flow)
            {
                flow.BackColor = flow.BackColor == SystemColors.Control ? Color.Transparent : flow.BackColor;
                flow.Padding = flow.Padding == Padding.Empty ? new Padding(0) : flow.Padding;
                return;
            }

            if (ctrl is TableLayoutPanel table)
            {
                table.BackColor = table.BackColor == SystemColors.Control ? Color.Transparent : table.BackColor;
                table.Margin = table.Margin == Padding.Empty ? new Padding(0) : table.Margin;
                return;
            }

            if (ctrl is Form || ctrl is UserControl)
            {
                ctrl.BackColor = DS.BgPage;
                ctrl.Font = DS.Body;
                return;
            }

            if (ctrl is Panel panel)
            {
                ApplyModernPanelStyle(panel);
            }
        }

        private static void ApplyModernButtonStyle(Button button)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI", Math.Max(8.75f, button.Font.Size), FontStyle.Bold);
            button.Height = Math.Max(button.Height, 32);
            button.Padding = button.Padding == Padding.Empty ? new Padding(12, 0, 12, 0) : button.Padding;

            Color resolved = ResolveActionColor(button);
            if (resolved == Color.Empty)
            {
                bool light = button.BackColor == Color.Empty
                    || button.BackColor.ToArgb() == SystemColors.Control.ToArgb()
                    || button.BackColor.GetBrightness() > 0.92f;

                if (light)
                {
                    button.BackColor = Color.White;
                    button.ForeColor = DS.Slate700;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = DS.BorderStrong;
                    button.FlatAppearance.MouseOverBackColor = DS.BgCardHov;
                    button.FlatAppearance.MouseDownBackColor = DS.Slate100;
                }
            }
            else
            {
                button.BackColor = resolved;
                button.ForeColor = resolved == ModernERPTheme.Warning ? Color.FromArgb(69, 26, 3) : Color.White;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = ModernERPTheme.Lighten(resolved, 0.08f);
                button.FlatAppearance.MouseDownBackColor = ModernERPTheme.Darken(resolved, 0.10f);
            }

            DS.Rounded(button, DS.RadiusSm);
        }

        public static UiActionVariant ResolveActionVariant(string text)
        {
            string key = (text ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(key, "delete", "void", "remove", "close job"))
                return UiActionVariant.Danger;
            if (ContainsAny(key, "convert to purchase order", "convert to invoice", "whatsapp follow-up"))
                return UiActionVariant.Secondary;
            if (ContainsAny(key, "save", "approve", "record payment", "post", "finalise", "finalize", "resolve"))
                return UiActionVariant.Success;
            if (ContainsAny(key, "new", "create", "generate", "dispatch", "send for approval"))
                return UiActionVariant.Primary;
            if (ContainsAny(key, "hold", "renew", "remind", "warning"))
                return UiActionVariant.Warning;
            if (ContainsAny(key, "preview", "import", "template", "forms", "open", "upload", "refresh", "clear", "compare"))
                return UiActionVariant.Secondary;
            return UiActionVariant.Secondary;
        }

        public static void ApplyActionButton(Button button, UiActionVariant variant)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI", Math.Max(8.75f, button.Font.Size), FontStyle.Bold);
            button.Height = Math.Max(button.Height, 32);
            button.Padding = button.Padding == Padding.Empty ? new Padding(12, 0, 12, 0) : button.Padding;

            Color bg;
            Color fg = Color.White;
            Color border = Color.Transparent;

            switch (variant)
            {
                case UiActionVariant.Success:
                    bg = ModernERPTheme.Success;
                    break;
                case UiActionVariant.Warning:
                    bg = ModernERPTheme.Warning;
                    fg = Color.FromArgb(69, 26, 3);
                    break;
                case UiActionVariant.Danger:
                    bg = ModernERPTheme.Danger;
                    break;
                case UiActionVariant.Ghost:
                    bg = Color.Transparent;
                    fg = DS.Slate700;
                    border = DS.BorderStrong;
                    break;
                case UiActionVariant.Secondary:
                    bg = Color.White;
                    fg = DS.Slate700;
                    border = DS.BorderStrong;
                    break;
                default:
                    bg = ModernERPTheme.Primary;
                    break;
            }

            button.BackColor = bg;
            button.ForeColor = fg;
            button.FlatAppearance.BorderSize = border == Color.Transparent ? 0 : 1;
            button.FlatAppearance.BorderColor = border == Color.Transparent ? bg : border;
            button.FlatAppearance.MouseOverBackColor = variant == UiActionVariant.Secondary || variant == UiActionVariant.Ghost
                ? DS.BgCardHov
                : ModernERPTheme.Lighten(bg, 0.08f);
            button.FlatAppearance.MouseDownBackColor = variant == UiActionVariant.Secondary || variant == UiActionVariant.Ghost
                ? DS.Slate100
                : ModernERPTheme.Darken(bg, 0.10f);

            DS.Rounded(button, DS.RadiusSm);
        }

        public static void ApplyActionButton(Button button)
        {
            ApplyActionButton(button, ResolveActionVariant(button == null ? string.Empty : button.Text));
        }

        private static Color ResolveActionColor(Button button)
        {
            string key = ((button.Name ?? string.Empty) + " " + (button.Text ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(key, "delete", "remove", "archive", "void"))
                return ModernERPTheme.Danger;
            if (ContainsAny(key, "save", "submit", "post", "approve", "resolve", "paid", "complete"))
                return ModernERPTheme.Success;
            if (ContainsAny(key, "edit", "new", "add", "create", "generate", "import", "export", "print", "refresh", "search", "view", "open", "install"))
                return ModernERPTheme.Primary;
            if (ContainsAny(key, "warn", "renew", "remind", "hold"))
                return ModernERPTheme.Warning;

            return Color.Empty;
        }

        private static void ApplyModernPanelStyle(Panel panel)
        {
            if (panel == null)
                return;

            if (panel.BackColor.ToArgb() == SystemColors.Control.ToArgb())
                panel.BackColor = DS.BgPage;

            if (!ShouldPaintCardSurface(panel))
                return;

            if (ModernCardPanels.Contains(panel))
                return;

            ModernCardPanels.Add(panel);
            panel.Disposed += (s, e) => ModernCardPanels.Remove(panel);
            panel.Paint += ModernCardPanelPaint;
            panel.Resize += (s, e) => panel.Invalidate();
        }

        private static bool ShouldPaintCardSurface(Panel panel)
        {
            if (panel is FlowLayoutPanel || panel is TableLayoutPanel)
                return false;

            if (panel.Dock == DockStyle.Fill && panel.Parent is Form)
                return false;

            string name = (panel.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BANNER"))
                return false;

            bool cardName = ContainsAny(name, "CARD", "PANEL", "SECTION", "SUMMARY", "DETAIL", "DETAILS", "KPI", "WIDGET", "FORM", "LIST", "FILTER");
            bool whiteSurface = panel.BackColor == Color.White || panel.BackColor == DS.White || panel.BackColor == SystemColors.Window;
            return panel.HasChildren && (cardName || whiteSurface);
        }

        private static void ModernCardPanelPaint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null || panel.Width < 4 || panel.Height < 4)
                return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using (GraphicsPath path = ModernERPTheme.RoundedRect(rect, DS.RadiusLg))
            using (Pen pen = new Pen(DS.Border))
                e.Graphics.DrawPath(pen, path);
        }

        public static void AttachComboOutline(ComboBox comboBox)
        {
        }

        public static void ApplyGlobalScrollAndResize(Control root)
        {
            if (root == null)
                return;

            ConfigureScrollAndResize(root);
        }

        private static void ConfigureScrollAndResize(Control control)
        {
            if (control == null || control.IsDisposed)
                return;

            if (ScrollResizeConfiguredControls.Contains(control))
                return;

            ScrollResizeConfiguredControls.Add(control);
            control.Disposed += (s, e) => ScrollResizeConfiguredControls.Remove(control);

            ConfigureScrollableControl(control);
            ConfigureDataGrid(control);
            ConfigureResizableCard(control);

            control.ControlAdded -= ControlAddedForScrollAndResize;
            control.ControlAdded += ControlAddedForScrollAndResize;

            foreach (Control child in control.Controls)
                ConfigureScrollAndResize(child);
        }

        private static void ControlAddedForScrollAndResize(object sender, ControlEventArgs e)
        {
            ConfigureScrollAndResize(e.Control);
        }

        private static void ConfigureScrollableControl(Control control)
        {
            if (!(control is ScrollableControl scrollable) || ScrollConfiguredControls.Contains(control))
                return;

            if (ShouldEnableAutoScroll(control))
            {
                scrollable.AutoScroll = true;
                scrollable.AutoScrollMargin = new Size(24, 24);
            }

            ScrollConfiguredControls.Add(control);
            control.Disposed += (s, e) => ScrollConfiguredControls.Remove(control);
        }

        private static bool ShouldEnableAutoScroll(Control control)
        {
            if (control is DashboardForm)
                return false;

            if (control is Form || control is UserControl || control is TabPage || control is SplitterPanel)
                return true;

            if (control is FlowLayoutPanel flow)
                return !IsChromeOrToolbarControl(control) && (flow.Dock == DockStyle.Fill || flow.AutoScroll);

            if (control is TableLayoutPanel)
                return false;

            if (!(control is Panel))
                return false;

            if (control.Parent is TableLayoutPanel)
                return false;

            string name = (control.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU"))
                return false;

            bool likelyPageContainer = control.Dock == DockStyle.Fill
                || ContainsAny(name, "CONTENT", "BODY", "MAIN", "PAGE", "WORKSPACE", "SCROLL", "CONTAINER");

            return likelyPageContainer;
        }

        private static bool IsChromeOrToolbarControl(Control control)
        {
            if (control == null)
                return false;

            string name = (control.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BREADCRUMB", "TABBAR"))
                return true;

            Control parent = control.Parent;
            string parentName = parent == null ? string.Empty : (parent.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(parentName, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BREADCRUMB", "TABBAR"))
                return true;

            bool compactToolbar = control.Height > 0 && control.Height <= 72 && ContainsToolbarChild(control);
            return compactToolbar && control.Dock != DockStyle.Fill;
        }

        private static bool ContainsToolbarChild(Control control)
        {
            if (control == null)
                return false;

            foreach (Control child in control.Controls)
            {
                if (child is Button || child is TextBox || child is ComboBox || child is DateTimePicker || child is CheckBox)
                    return true;
            }

            return false;
        }

        private static void ConfigureDataGrid(Control control)
        {
            if (!(control is DataGridView grid))
                return;

            grid.ScrollBars = ScrollBars.Both;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            DS.StyleGrid(grid);
        }

        private static void ConfigureResizableCard(Control control)
        {
            if (!(control is ResizableCard card))
                return;

            card.AllowResize = true;
            card.ContentPanel.AutoScroll = true;
            card.MaximumSize = new Size(6000, 3000);
        }

        private static bool ContainsAny(string value, params string[] tokens)
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

        public static void ShowEmptyClientsMessageIfNeeded(IWin32Window owner, ICollection<B2BClient> clients, string sourceKey)
        {
            if (clients != null && clients.Count > 0)
                return;

            string key = string.IsNullOrWhiteSpace(sourceKey) ? "clients" : sourceKey.Trim();
            if (EmptyClientMessageKeys.Contains(key))
                return;

            EmptyClientMessageKeys.Add(key);
            AppLogger.LogInfo("No clients found while loading " + key + ".");
        }

        public static void ShowEmptyVendorsMessageIfNeeded(IWin32Window owner, ICollection<Vendor> vendors, string sourceKey)
        {
            if (vendors != null && vendors.Count > 0)
                return;

            string key = string.IsNullOrWhiteSpace(sourceKey) ? "vendors" : sourceKey.Trim();
            if (EmptyVendorMessageKeys.Contains(key))
                return;

            EmptyVendorMessageKeys.Add(key);
            AppLogger.LogInfo("No suppliers/vendors found while loading " + key + ".");
        }

    }
}
