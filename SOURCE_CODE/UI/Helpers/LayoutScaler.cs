using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI.Helpers
{
    public static class LayoutScaler
    {
        public const string DisplayFitAuto = "Auto";
        public const string DisplayFitIdeaPad = "IdeaPad";
        public const string DisplayFitStandard = "Standard";

        private static readonly HashSet<int> FontScaledControls = new HashSet<int>();
        private static readonly HashSet<string> GlobalScaledControls = new HashSet<string>();
        private static readonly HashSet<int> LayoutScaledControls = new HashSet<int>();
        private static readonly object Sync = new object();
        private static readonly int[] UiScaleOptions = { 85, 90, 100, 110, 125 };

        public static string GetDisplayFitMode()
        {
            try
            {
                return NormalizeDisplayFitMode(ConfigService.Get("App", "DisplayFitMode", DisplayFitAuto));
            }
            catch
            {
                return DisplayFitAuto;
            }
        }

        public static void SetDisplayFitMode(string mode)
        {
            ConfigService.Set("App", "DisplayFitMode", NormalizeDisplayFitMode(mode));
        }

        public static int[] GetUiScaleOptions()
        {
            return (int[])UiScaleOptions.Clone();
        }

        public static int GetUiScalePercent()
        {
            try
            {
                string value = ConfigService.Get("App", "UiScalePercent", "100");
                int percent;
                if (!int.TryParse(value, out percent))
                    percent = 100;

                return ClampUiScalePercent(percent);
            }
            catch
            {
                return 100;
            }
        }

        public static void SetUiScalePercent(int percent)
        {
            ConfigService.Set("App", "UiScalePercent", ClampUiScalePercent(percent).ToString());
        }

        public static float GetUiScaleFactor()
        {
            return GetUiScalePercent() / 100f;
        }

        public static void ApplyGlobalScale(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            float factor = GetUiScaleFactor();
            if (Math.Abs(factor - 1f) < 0.01f)
                return;

            try
            {
                root.SuspendLayout();
                ApplyGlobalScaleCore(root, factor, true);
            }
            catch (Exception ex)
            {
                LogScalingError("LayoutScaler.ApplyGlobalScale", ex);
            }
            finally
            {
                try
                {
                    root.ResumeLayout(true);
                }
                catch
                {
                }
            }
        }

        private static int ClampUiScalePercent(int percent)
        {
            if (percent < 85)
                return 85;
            if (percent > 125)
                return 125;

            int closest = UiScaleOptions[0];
            int distance = Math.Abs(percent - closest);
            foreach (int option in UiScaleOptions)
            {
                int candidateDistance = Math.Abs(percent - option);
                if (candidateDistance < distance)
                {
                    closest = option;
                    distance = candidateDistance;
                }
            }

            return closest;
        }

        public static string NormalizeDisplayFitMode(string mode)
        {
            if (string.Equals(mode, DisplayFitIdeaPad, StringComparison.OrdinalIgnoreCase))
                return DisplayFitIdeaPad;
            if (string.Equals(mode, DisplayFitStandard, StringComparison.OrdinalIgnoreCase))
                return DisplayFitStandard;
            return DisplayFitAuto;
        }

        public static bool IsLaptopFitModeEnabled(Control context = null)
        {
            string mode = GetDisplayFitMode();
            if (mode == DisplayFitIdeaPad)
                return true;
            if (mode == DisplayFitStandard)
                return false;

            Rectangle workArea = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : SystemInformation.WorkingArea;
            float dpi = 96f;
            try
            {
                if (context != null && !context.IsDisposed)
                {
                    using (Graphics graphics = context.CreateGraphics())
                        dpi = graphics.DpiX;
                }
                else
                {
                    using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                        dpi = graphics.DpiX;
                }
            }
            catch
            {
            }

            return workArea.Width <= 1366 || workArea.Height <= 760 ||
                   (workArea.Width <= 1536 && workArea.Height <= 864) ||
                   dpi >= 120f;
        }

        public static void ApplyDisplayFit(Control root)
        {
            if (root == null || root.IsDisposed || !IsLaptopFitModeEnabled(root))
                return;

            ScaleControl(root);

            Form form = root as Form;
            if (form == null)
                form = root.FindForm();
            if (form == null || form.IsDisposed)
                return;

            Rectangle workArea = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : SystemInformation.WorkingArea;
            form.MinimumSize = new Size(
                Math.Min(Math.Max(860, form.MinimumSize.Width), Math.Max(860, workArea.Width - 80)),
                Math.Min(Math.Max(540, form.MinimumSize.Height), Math.Max(540, workArea.Height - 80)));

            if (form.WindowState == FormWindowState.Normal)
            {
                int width = Math.Min(form.Width, Math.Max(900, workArea.Width - 32));
                int height = Math.Min(form.Height, Math.Max(560, workArea.Height - 32));
                form.Size = new Size(width, height);
            }
        }

        public static void ScaleControl(Control root)
        {
            if (root == null)
                return;

            try
            {
                ScaleControlCore(root);
            }
            catch (Exception ex)
            {
                LogScalingError("LayoutScaler.ScaleControl", ex);
            }
        }

        public static void ScaleFont(Control root, float baseDpi)
        {
            if (root == null || baseDpi <= 0)
                return;

            try
            {
                float currentDpi;
                using (Graphics graphics = root.CreateGraphics())
                    currentDpi = graphics.DpiX;

                if (Math.Abs(currentDpi - baseDpi) < 1f)
                    return;

                float factor = currentDpi / baseDpi;
                ScaleFontCore(root, factor);
            }
            catch (Exception ex)
            {
                LogScalingError("LayoutScaler.ScaleFont", ex);
            }
        }

        private static void ScaleControlCore(Control control)
        {
            if (control == null || control.IsDisposed)
                return;

            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(control);
            lock (Sync)
            {
                if (LayoutScaledControls.Contains(key))
                    return;
                LayoutScaledControls.Add(key);
            }

            control.Disposed += (s, e) =>
            {
                lock (Sync)
                {
                    LayoutScaledControls.Remove(key);
                }
            };

            bool laptopFit = IsLaptopFitModeEnabled(control);

            Button button = control as Button;
            if (button != null)
            {
                button.AutoSize = false;
                int desiredWidth = GetTextWidth(button.Text, button.Font, (laptopFit ? 18 : 28) + button.Padding.Left + button.Padding.Right);
                button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, desiredWidth), Math.Max(button.MinimumSize.Height, button.Height));
                if (button.Width < desiredWidth && CanGrowWidth(button))
                    button.Width = desiredWidth;
            }

            Label label = control as Label;
            if (label != null)
            {
                label.AutoEllipsis = laptopFit;
                int desiredWidth = GetTextWidth(label.Text, label.Font, 8);
                int desiredHeight = Math.Max(label.MinimumSize.Height, TextRenderer.MeasureText(label.Text ?? string.Empty, label.Font).Height + 2);
                label.MinimumSize = new Size(Math.Max(label.MinimumSize.Width, desiredWidth), desiredHeight);

                if (!laptopFit && label.Dock == DockStyle.None && label.Anchor == AnchorStyles.Top)
                {
                    label.AutoSize = true;
                }
                else if (CanGrowWidth(label) && label.Width < desiredWidth)
                {
                    label.Width = desiredWidth;
                }
            }

            Panel panel = control as Panel;
            if (panel != null && !(panel is FlowLayoutPanel) && !(panel is TableLayoutPanel))
                DockOnlyChildPanel(panel);

            TableLayoutPanel table = control as TableLayoutPanel;
            if (table != null)
                DockOnlyChildPanel(table);

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                if (laptopFit)
                {
                    grid.RowTemplate.Height = Math.Min(Math.Max(grid.RowTemplate.Height, 28), 32);
                    grid.ColumnHeadersHeight = Math.Min(Math.Max(grid.ColumnHeadersHeight, 30), 34);
                    grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                }
            }

            TabControl tabs = control as TabControl;
            if (tabs != null)
            {
                foreach (TabPage page in tabs.TabPages)
                {
                    int pad = laptopFit ? 6 : 8;
                    if (page.Padding.Left < pad || page.Padding.Top < pad)
                        page.Padding = new Padding(Math.Max(pad, page.Padding.Left), Math.Max(pad, page.Padding.Top), Math.Max(pad, page.Padding.Right), Math.Max(pad, page.Padding.Bottom));
                }
            }

            FlowLayoutPanel flow = control as FlowLayoutPanel;
            if (flow != null)
                KeepButtonRowsVisible(flow);

            foreach (Control child in control.Controls)
                ScaleControlCore(child);
        }

        private static void ApplyGlobalScaleCore(Control control, float factor, bool isRoot)
        {
            if (control == null || control.IsDisposed)
                return;

            int hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(control);
            string key = hash.ToString(CultureInfo.InvariantCulture) + ":" + GetUiScalePercent().ToString(CultureInfo.InvariantCulture);
            lock (Sync)
            {
                if (GlobalScaledControls.Contains(key))
                    return;
                GlobalScaledControls.Add(key);
            }

            control.Disposed += (s, e) =>
            {
                lock (Sync)
                {
                    GlobalScaledControls.Remove(key);
                }
            };

            ScaleControlMetrics(control, factor, isRoot);

            foreach (Control child in control.Controls)
                ApplyGlobalScaleCore(child, factor, false);
        }

        private static void ScaleControlMetrics(Control control, float factor, bool isRoot)
        {
            if (control == null || factor <= 0)
                return;

            ScaleFontForUi(control, factor);

            if (!isRoot)
            {
                if (control.Dock == DockStyle.None)
                {
                    control.Left = ScaleInt(control.Left, factor);
                    control.Top = ScaleInt(control.Top, factor);
                    control.Width = ScaleInt(control.Width, factor);
                    control.Height = ScaleInt(control.Height, factor);
                }
                else
                {
                    if (control.Dock == DockStyle.Top || control.Dock == DockStyle.Bottom)
                        control.Height = ScaleInt(control.Height, factor);
                    else if (control.Dock == DockStyle.Left || control.Dock == DockStyle.Right)
                        control.Width = ScaleInt(control.Width, factor);
                }
            }

            control.Margin = ScalePadding(control.Margin, factor);
            control.Padding = ScalePadding(control.Padding, factor);
            control.MinimumSize = ScaleSize(control.MinimumSize, factor);
            control.MaximumSize = ScaleSize(control.MaximumSize, factor);

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.RowTemplate.Height = ScaleInt(grid.RowTemplate.Height, factor);
                grid.ColumnHeadersHeight = ScaleInt(grid.ColumnHeadersHeight, factor);
            }

            TabControl tabs = control as TabControl;
            if (tabs != null)
                tabs.ItemSize = ScaleSize(tabs.ItemSize, factor);

            TableLayoutPanel table = control as TableLayoutPanel;
            if (table != null)
                ScaleTableStyles(table, factor);
        }

        private static void ScaleFontForUi(Control control, float factor)
        {
            Font font = control.Font;
            if (font == null || LooksLikeIconFont(font))
                return;

            float newSize = Math.Max(6.5f, Math.Min(28f, font.SizeInPoints * factor));
            if (Math.Abs(newSize - font.SizeInPoints) > 0.01f)
                control.Font = new Font(font.FontFamily, newSize, font.Style, GraphicsUnit.Point);
        }

        private static void ScaleTableStyles(TableLayoutPanel table, float factor)
        {
            foreach (RowStyle row in table.RowStyles)
            {
                if (row.SizeType == SizeType.Absolute)
                    row.Height = ScaleFloat(row.Height, factor);
            }

            foreach (ColumnStyle column in table.ColumnStyles)
            {
                if (column.SizeType == SizeType.Absolute)
                    column.Width = ScaleFloat(column.Width, factor);
            }
        }

        private static Padding ScalePadding(Padding padding, float factor)
        {
            return new Padding(
                ScaleInt(padding.Left, factor),
                ScaleInt(padding.Top, factor),
                ScaleInt(padding.Right, factor),
                ScaleInt(padding.Bottom, factor));
        }

        private static Size ScaleSize(Size size, float factor)
        {
            if (size.IsEmpty)
                return size;

            return new Size(ScaleInt(size.Width, factor), ScaleInt(size.Height, factor));
        }

        private static int ScaleInt(int value, float factor)
        {
            if (value <= 0)
                return value;

            return Math.Max(1, (int)Math.Round(value * factor));
        }

        private static float ScaleFloat(float value, float factor)
        {
            if (value <= 0)
                return value;

            return Math.Max(1f, value * factor);
        }

        private static void KeepButtonRowsVisible(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed)
                return;

            bool horizontal = flow.FlowDirection == FlowDirection.LeftToRight ||
                              flow.FlowDirection == FlowDirection.RightToLeft;
            if (!horizontal)
                return;

            var buttons = new List<Button>();
            foreach (Control child in flow.Controls)
            {
                Button button = child as Button;
                if (button != null && button.Visible)
                    buttons.Add(button);
            }

            if (buttons.Count < 2)
            {
                if (!flow.WrapContents)
                    flow.AutoScroll = true;
                return;
            }

            int rowHeight = 0;
            int totalWidth = flow.Padding.Left + flow.Padding.Right;
            foreach (Button button in buttons)
            {
                rowHeight = Math.Max(rowHeight, button.Height + button.Margin.Top + button.Margin.Bottom);
                totalWidth += button.Width + button.Margin.Left + button.Margin.Right;
            }

            if (rowHeight <= 0)
                return;

            int availableWidth = flow.ClientSize.Width;
            if (availableWidth <= 0 && flow.Parent != null)
                availableWidth = Math.Max(0, flow.Parent.ClientSize.Width - flow.Left - flow.Margin.Horizontal);

            int rows = availableWidth > 0 ? Math.Max(1, (int)Math.Ceiling(totalWidth / (double)Math.Max(1, availableWidth))) : 1;
            int desiredHeight = flow.Padding.Top + flow.Padding.Bottom + (rowHeight * rows) + 4;

            flow.AutoScroll = false;
            flow.WrapContents = true;

            if (flow.Dock == DockStyle.Top || flow.Dock == DockStyle.Bottom || flow.Dock == DockStyle.None || flow.Dock == DockStyle.Right || flow.Dock == DockStyle.Left)
                flow.Height = Math.Max(flow.Height, desiredHeight);

            Control parent = flow.Parent;
            if (parent != null && parent.Dock != DockStyle.Fill && parent.Height < flow.Top + desiredHeight + parent.Padding.Bottom)
                parent.Height = flow.Top + desiredHeight + parent.Padding.Bottom;
        }

        private static int GetTextWidth(string text, Font font, int padding)
        {
            if (string.IsNullOrEmpty(text))
                return padding;

            return TextRenderer.MeasureText(text, font).Width + padding;
        }

        private static bool CanGrowWidth(Control control)
        {
            if (control.Dock == DockStyle.Fill || control.Dock == DockStyle.Top || control.Dock == DockStyle.Bottom)
                return false;

            return control.Parent == null || control.Parent.Controls.Count <= 24;
        }

        private static void DockOnlyChildPanel(Control panel)
        {
            Control parent = panel.Parent;
            if (parent == null || panel.Dock != DockStyle.None || parent.Controls.Count != 1)
                return;

            ScrollableControl scrollableParent = parent as ScrollableControl;
            if (scrollableParent != null && scrollableParent.AutoScroll)
                return;

            if (panel.Width > 0 && panel.Height > 0)
                return;

            panel.Dock = DockStyle.Fill;
        }

        private static void ScaleFontCore(Control control, float factor)
        {
            int key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(control);
            lock (Sync)
            {
                if (FontScaledControls.Contains(key))
                    return;
                FontScaledControls.Add(key);
            }

            Font font = control.Font;
            if (font != null && !font.Bold && !LooksLikeIconFont(font))
            {
                float newSize = Math.Max(7f, font.SizeInPoints * factor);
                control.Font = new Font(font.FontFamily, newSize, font.Style, GraphicsUnit.Point);
            }

            foreach (Control child in control.Controls)
                ScaleFontCore(child, factor);
        }

        private static bool LooksLikeIconFont(Font font)
        {
            string name = font.Name ?? string.Empty;
            return name.IndexOf("Marlett", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Segoe MDL2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Webdings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Wingdings", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void LogScalingError(string context, Exception ex)
        {
            try
            {
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "scaling-" + DateTime.Now.ToString("yyyyMMdd") + ".log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + context + Environment.NewLine + ex + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
