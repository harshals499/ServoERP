using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public static class UIHelper
    {
        private static readonly HashSet<ComboBox> OutlinedComboBoxes = new HashSet<ComboBox>();
        private static readonly HashSet<Control> ComboOutlineParents = new HashSet<Control>();
        private static readonly HashSet<Control> ScrollResizeConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> ScrollConfiguredControls = new HashSet<Control>();
        private static readonly Dictionary<Control, PanelResizeState> ResizablePanels = new Dictionary<Control, PanelResizeState>();
        private static readonly HashSet<string> EmptyClientMessageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> EmptyVendorMessageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const int ResizeGripSize = 10;
        private const int MinPanelResizeWidth = 180;
        private const int MinPanelResizeHeight = 90;

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
            if (ctrl is TextBox tb)
            {
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.BackColor = tb.ReadOnly ? DS.Slate50 : Color.White;
            }
            else if (ctrl is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.System;
                cb.BackColor = Color.White;
                cb.ForeColor = DS.Slate900;
                cb.AutoCompleteSource = AutoCompleteSource.ListItems;
                cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                AttachComboOutline(cb);
            }
            else if (ctrl is DateTimePicker dtp)
            {
                if (dtp.Format == DateTimePickerFormat.Custom &&
                    string.IsNullOrEmpty(dtp.CustomFormat))
                {
                    dtp.Format = DateTimePickerFormat.Short;
                }
            }
            else if (ctrl is NumericUpDown nud)
            {
                nud.BorderStyle = BorderStyle.FixedSingle;
                nud.BackColor = Color.White;
            }
        }

        public static void AttachComboOutline(ComboBox comboBox)
        {
            if (comboBox == null || OutlinedComboBoxes.Contains(comboBox))
                return;

            OutlinedComboBoxes.Add(comboBox);
            comboBox.ParentChanged += (s, e) => AttachComboParentPainter(comboBox.Parent);
            comboBox.LocationChanged += (s, e) => comboBox.Parent?.Invalidate(comboBox.Bounds);
            comboBox.SizeChanged += (s, e) => comboBox.Parent?.Invalidate(comboBox.Bounds);
            comboBox.VisibleChanged += (s, e) => comboBox.Parent?.Invalidate(comboBox.Bounds);
            comboBox.Disposed += (s, e) => OutlinedComboBoxes.Remove(comboBox);
            AttachComboParentPainter(comboBox.Parent);
        }

        private static void AttachComboParentPainter(Control parent)
        {
            if (parent == null || ComboOutlineParents.Contains(parent))
                return;

            ComboOutlineParents.Add(parent);
            parent.Paint += (s, e) =>
            {
                Control host = s as Control;
                if (host == null)
                    return;

                using (Pen pen = new Pen(Color.Black, 1))
                {
                    foreach (Control child in host.Controls)
                    {
                        if (!(child is ComboBox combo) || !combo.Visible)
                            continue;

                        Rectangle bounds = combo.Bounds;
                        bounds.Width -= 1;
                        bounds.Height -= 1;
                        e.Graphics.DrawRectangle(pen, bounds);
                    }
                }
            };
            parent.Disposed += (s, e) => ComboOutlineParents.Remove(parent);
            parent.Invalidate();
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
            ConfigureResizablePanel(control);

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
                return flow.Dock == DockStyle.Fill || flow.AutoScroll;

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

        private static void ConfigureResizablePanel(Control control)
        {
            if (!(control is Panel panel) || panel is FlowLayoutPanel || panel is TableLayoutPanel)
                return;

            if (IsInsideDashboard(panel))
                return;

            if (ResizablePanels.ContainsKey(panel) || !ShouldAttachPanelResize(panel))
                return;

            var state = new PanelResizeState(panel, ResolveResizeAxes(panel));
            ResizablePanels[panel] = state;
            panel.Disposed += (s, e) => ResizablePanels.Remove(panel);
            panel.MouseDown += ResizablePanelMouseDown;
            panel.MouseMove += ResizablePanelMouseMove;
            panel.MouseUp += ResizablePanelMouseUp;
            panel.MouseLeave += ResizablePanelMouseLeave;
            panel.Paint += ResizablePanelPaint;
        }

        private static bool ShouldAttachPanelResize(Panel panel)
        {
            if (panel.Parent == null || !panel.Visible || !panel.HasChildren)
                return false;

            if (panel.Width < MinPanelResizeWidth || panel.Height < MinPanelResizeHeight)
                return false;

            if (panel.Dock == DockStyle.Fill)
                return false;

            string name = (panel.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BUTTON", "TABS", "TABBAR"))
                return false;

            bool whiteSurface = panel.BackColor == Color.White
                || panel.BackColor == DS.White
                || panel.BackColor == SystemColors.Window
                || panel.BackColor == Color.Empty;

            bool cardName = ContainsAny(name, "CARD", "PANEL", "SECTION", "SUMMARY", "DETAIL", "DETAILS", "KPI", "WIDGET", "FORM", "LIST");
            return whiteSurface || cardName;
        }

        private static PanelResizeAxes ResolveResizeAxes(Panel panel)
        {
            switch (panel.Dock)
            {
                case DockStyle.Top:
                case DockStyle.Bottom:
                    return PanelResizeAxes.HeightOnly;
                case DockStyle.Left:
                case DockStyle.Right:
                    return PanelResizeAxes.WidthOnly;
                default:
                    return PanelResizeAxes.Both;
            }
        }

        private static void ResizablePanelMouseDown(object sender, MouseEventArgs e)
        {
            if (!(sender is Panel panel) || e.Button != MouseButtons.Left)
                return;

            if (!ResizablePanels.TryGetValue(panel, out PanelResizeState state))
                return;

            PanelResizeDirection direction = HitTestPanelResize(panel, e.Location, state.Axes);
            if (direction == PanelResizeDirection.None)
                return;

            state.IsResizing = true;
            state.Direction = direction;
            state.StartMouse = Control.MousePosition;
            state.StartSize = panel.Size;
            panel.Capture = true;
        }

        private static void ResizablePanelMouseMove(object sender, MouseEventArgs e)
        {
            if (!(sender is Panel panel) || !ResizablePanels.TryGetValue(panel, out PanelResizeState state))
                return;

            if (!state.IsResizing)
            {
                PanelResizeDirection hover = HitTestPanelResize(panel, e.Location, state.Axes);
                panel.Cursor = CursorForPanelDirection(hover);
                return;
            }

            Point current = Control.MousePosition;
            int deltaX = current.X - state.StartMouse.X;
            int deltaY = current.Y - state.StartMouse.Y;
            int maxWidth = GetPanelMaxWidth(panel);
            int maxHeight = GetPanelMaxHeight(panel);

            int width = state.StartSize.Width;
            int height = state.StartSize.Height;

            if (state.Direction == PanelResizeDirection.Right || state.Direction == PanelResizeDirection.BottomRight)
                width = Math.Max(MinPanelResizeWidth, Math.Min(maxWidth, state.StartSize.Width + deltaX));

            if (state.Direction == PanelResizeDirection.Bottom || state.Direction == PanelResizeDirection.BottomRight)
                height = Math.Max(MinPanelResizeHeight, Math.Min(maxHeight, state.StartSize.Height + deltaY));

            if (state.Axes == PanelResizeAxes.HeightOnly)
                width = panel.Width;
            if (state.Axes == PanelResizeAxes.WidthOnly)
                height = panel.Height;

            panel.MinimumSize = new Size(Math.Min(panel.MinimumSize.Width, width), Math.Min(panel.MinimumSize.Height, height));
            panel.Size = new Size(width, height);
            panel.Parent?.PerformLayout();
            panel.Invalidate();
        }

        private static void ResizablePanelMouseUp(object sender, MouseEventArgs e)
        {
            FinishResizablePanel(sender as Panel);
        }

        private static void ResizablePanelMouseLeave(object sender, EventArgs e)
        {
            if (!(sender is Panel panel) || !ResizablePanels.TryGetValue(panel, out PanelResizeState state) || state.IsResizing)
                return;

            panel.Cursor = Cursors.Default;
        }

        private static void FinishResizablePanel(Panel panel)
        {
            if (panel == null || !ResizablePanels.TryGetValue(panel, out PanelResizeState state) || !state.IsResizing)
                return;

            state.IsResizing = false;
            state.Direction = PanelResizeDirection.None;
            panel.Capture = false;
            panel.Cursor = Cursors.Default;
        }

        private static void ResizablePanelPaint(object sender, PaintEventArgs e)
        {
            if (!(sender is Panel panel) || !ResizablePanels.ContainsKey(panel))
                return;

            using (Pen pen = new Pen(DS.Slate300, 1f))
            {
                int right = panel.Width - 6;
                int bottom = panel.Height - 6;
                e.Graphics.DrawLine(pen, right - 8, bottom, right, bottom - 8);
                e.Graphics.DrawLine(pen, right - 5, bottom, right, bottom - 5);
                e.Graphics.DrawLine(pen, right - 2, bottom, right, bottom - 2);
            }
        }

        private static PanelResizeDirection HitTestPanelResize(Panel panel, Point location, PanelResizeAxes axes)
        {
            bool nearRight = location.X >= panel.Width - ResizeGripSize;
            bool nearBottom = location.Y >= panel.Height - ResizeGripSize;

            if (axes == PanelResizeAxes.Both && nearRight && nearBottom)
                return PanelResizeDirection.BottomRight;
            if (axes != PanelResizeAxes.HeightOnly && nearRight)
                return PanelResizeDirection.Right;
            if (axes != PanelResizeAxes.WidthOnly && nearBottom)
                return PanelResizeDirection.Bottom;

            return PanelResizeDirection.None;
        }

        private static Cursor CursorForPanelDirection(PanelResizeDirection direction)
        {
            switch (direction)
            {
                case PanelResizeDirection.Right:
                    return Cursors.SizeWE;
                case PanelResizeDirection.Bottom:
                    return Cursors.SizeNS;
                case PanelResizeDirection.BottomRight:
                    return Cursors.SizeNWSE;
                default:
                    return Cursors.Default;
            }
        }

        private static int GetPanelMaxWidth(Panel panel)
        {
            Control parent = panel.Parent;
            if (parent == null)
                return Math.Max(MinPanelResizeWidth, panel.Width);

            bool parentScrolls = parent is ScrollableControl scrollable && scrollable.AutoScroll;
            int viewport = parent.ClientSize.Width > 0 ? parent.ClientSize.Width : 1600;
            int max = parentScrolls ? viewport * 2 : viewport - panel.Margin.Horizontal - 8;
            return Math.Max(MinPanelResizeWidth, Math.Min(6000, Math.Max(800, max)));
        }

        private static int GetPanelMaxHeight(Panel panel)
        {
            Control parent = panel.Parent;
            if (parent == null)
                return Math.Max(MinPanelResizeHeight, panel.Height);

            bool parentScrolls = parent is ScrollableControl scrollable && scrollable.AutoScroll;
            int viewport = parent.ClientSize.Height > 0 ? parent.ClientSize.Height : 1200;
            int max = parentScrolls ? viewport * 2 : viewport - panel.Margin.Vertical - 8;
            return Math.Max(MinPanelResizeHeight, Math.Min(3000, Math.Max(900, max)));
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

        private static bool IsInsideDashboard(Control control)
        {
            Control current = control;
            while (current != null)
            {
                if (current is DashboardForm)
                    return true;

                current = current.Parent;
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

        private enum PanelResizeAxes { Both, HeightOnly, WidthOnly }
        private enum PanelResizeDirection { None, Right, Bottom, BottomRight }

        private sealed class PanelResizeState
        {
            public PanelResizeState(Panel panel, PanelResizeAxes axes)
            {
                Panel = panel;
                Axes = axes;
            }

            public Panel Panel { get; }
            public PanelResizeAxes Axes { get; }
            public bool IsResizing { get; set; }
            public PanelResizeDirection Direction { get; set; }
            public Point StartMouse { get; set; }
            public Size StartSize { get; set; }
        }
    }
}
