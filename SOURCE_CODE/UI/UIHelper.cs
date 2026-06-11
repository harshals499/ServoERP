using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
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

    public enum ButtonRole
    {
        Primary,
        Secondary,
        Danger,
        Neutral
    }

    public static class UIHelper
    {
        private static readonly HashSet<Control> ScrollResizeConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> ScrollConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> ModernizedControls = new HashSet<Control>();
        private static readonly HashSet<Control> ButtonAlignmentConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> FlowClampConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> OverlapGuardConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Button> TextFitConfiguredButtons = new HashSet<Button>();
        private static readonly HashSet<Button> ButtonFocusConfiguredButtons = new HashSet<Button>();
        private static readonly Dictionary<Button, Color> ButtonNormalBorderColors = new Dictionary<Button, Color>();
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

                InputOutlineService.ApplyToTree(ctrl);
            }
        }

        public static void ApplyInputStyle(Control ctrl)
        {
            if (IsInsideCustomInputShell(ctrl))
                return;

            ApplyModernErpStyle(ctrl);

            if (ctrl is TextBox tb)
            {
                tb.BorderStyle = IsInsideInputOutlineHost(tb) ? BorderStyle.None : BorderStyle.FixedSingle;
                tb.BackColor = ResolveInputBackColor(tb.Enabled, tb.ReadOnly);
                tb.ForeColor = ResolveInputForeColor(tb.Enabled, tb.ReadOnly);
                tb.Font = DS.Body;
                tb.MinimumSize = new Size(tb.MinimumSize.Width, tb.Multiline ? Math.Max(tb.MinimumSize.Height, 36) : 32);
                if (!tb.Multiline)
                    tb.Height = Math.Max(tb.Height, 32);
                if (tb.Margin == Padding.Empty)
                    tb.Margin = new Padding(0, 3, 0, 8);
                InputOutlineService.ApplyToTree(tb);
            }
            else if (ctrl is RichTextBox rtb)
            {
                rtb.BorderStyle = IsInsideInputOutlineHost(rtb) ? BorderStyle.None : BorderStyle.FixedSingle;
                rtb.BackColor = ResolveInputBackColor(rtb.Enabled, rtb.ReadOnly);
                rtb.ForeColor = ResolveInputForeColor(rtb.Enabled, rtb.ReadOnly);
                rtb.Font = DS.Body;
                rtb.MinimumSize = new Size(rtb.MinimumSize.Width, Math.Max(rtb.MinimumSize.Height, 36));
                if (rtb.Margin == Padding.Empty)
                    rtb.Margin = new Padding(0, 3, 0, 8);
                InputOutlineService.ApplyToTree(rtb);
            }
            else if (ctrl is ComboBox cb)
            {
                cb.FlatStyle = IsInsideInputOutlineHost(cb) ? FlatStyle.Flat : FlatStyle.Standard;
                cb.BackColor = cb.Enabled ? DS.BgInput : DS.InputDisabledBack;
                cb.ForeColor = cb.Enabled ? DS.InputText : DS.InputMutedText;
                cb.Font = DS.Body;
                cb.MinimumSize = new Size(cb.MinimumSize.Width, 32);
                cb.Height = Math.Max(cb.Height, 32);
                cb.AutoCompleteSource = AutoCompleteSource.ListItems;
                cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                if (cb.Margin == Padding.Empty)
                    cb.Margin = new Padding(0, 3, 0, 8);
                InputOutlineService.ApplyToTree(cb);
            }
            else if (ctrl is DateTimePicker dtp)
            {
                if (dtp.Format == DateTimePickerFormat.Custom &&
                    string.IsNullOrEmpty(dtp.CustomFormat))
                {
                    dtp.Format = DateTimePickerFormat.Short;
                }

                dtp.Font = DS.Body;
                dtp.CalendarForeColor = DS.InputText;
                dtp.CalendarMonthBackground = DS.BgInput;
                dtp.MinimumSize = new Size(dtp.MinimumSize.Width, 32);
                dtp.Height = Math.Max(dtp.Height, 32);
                if (dtp.Margin == Padding.Empty)
                    dtp.Margin = new Padding(0, 3, 0, 8);
                InputOutlineService.ApplyToTree(dtp);
            }
            else if (ctrl is NumericUpDown nud)
            {
                nud.BorderStyle = IsInsideInputOutlineHost(nud) ? BorderStyle.None : BorderStyle.FixedSingle;
                nud.BackColor = nud.Enabled ? DS.BgInput : DS.InputDisabledBack;
                nud.ForeColor = nud.Enabled ? DS.InputText : DS.InputMutedText;
                nud.Font = DS.Body;
                nud.MinimumSize = new Size(nud.MinimumSize.Width, 32);
                nud.Height = Math.Max(nud.Height, 32);
                if (nud.Margin == Padding.Empty)
                    nud.Margin = new Padding(0, 3, 0, 8);
                InputOutlineService.ApplyToTree(nud);
            }
        }

        private static Color ResolveInputBackColor(bool enabled, bool readOnly)
        {
            return enabled && !readOnly ? DS.BgInput : DS.InputDisabledBack;
        }

        private static bool IsInsideInputOutlineHost(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            if (parent == null || parent is Form || parent is UserControl || parent is TabPage)
                return false;

            string metadata = ((parent.Name ?? string.Empty) + " " + (parent.Tag == null ? string.Empty : parent.Tag.ToString())).ToUpperInvariant();
            return metadata.Contains("HOST") ||
                   metadata.Contains("FIELD") ||
                   metadata.Contains("SEARCH") ||
                   metadata.Contains("FILTER") ||
                   metadata.Contains("INPUT") ||
                   InputOutlineService.IsOutlinedInputHost(parent);
        }

        private static bool IsInsideCustomInputShell(Control control)
        {
            Control current = control;
            while (current != null)
            {
                string tag = current.Tag == null ? string.Empty : current.Tag.ToString();
                if (tag.IndexOf("CUSTOM_INPUT_SHELL", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                current = current.Parent;
            }

            return false;
        }

        private static Color ResolveInputForeColor(bool enabled, bool readOnly)
        {
            return enabled && !readOnly ? DS.InputText : DS.InputMutedText;
        }

        /// <summary>Normalizes button sizing and row alignment inside action areas.</summary>
        public static void ApplyButtonAlignment(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            ConfigureButtonAlignment(root);
        }

        private static void ConfigureButtonAlignment(Control control)
        {
            if (control == null || control.IsDisposed)
                return;

            if (!ButtonAlignmentConfiguredControls.Contains(control))
            {
                ButtonAlignmentConfiguredControls.Add(control);
                control.Disposed += (s, e) => ButtonAlignmentConfiguredControls.Remove(control);
                control.ControlAdded -= ControlAddedForButtonAlignment;
                control.ControlAdded += ControlAddedForButtonAlignment;
            }

            NormalizeButton(control as Button);
            NormalizeSharedTextStyle(control);
            NormalizeReadableLabel(control as Label);
            NormalizeTabControl(control as TabControl);
            NormalizeButtonFlow(control as FlowLayoutPanel);
            NormalizeButtonGrid(control as TableLayoutPanel);
            SharedUiPrimitives.ApplyToControl(control);

            foreach (Control child in control.Controls)
                ConfigureButtonAlignment(child);

            ApplyButtonContainerLayout(control);
            PreventTextButtonOverlap(control);
        }

        private static void ControlAddedForButtonAlignment(object sender, ControlEventArgs e)
        {
            ConfigureButtonAlignment(e.Control);
            NormalizeButtonFlow(sender as FlowLayoutPanel);
            NormalizeButtonGrid(sender as TableLayoutPanel);
            NormalizeTabControl(sender as TabControl);
            ApplyButtonContainerLayout(sender as Control);
        }

        private static void NormalizeSharedTextStyle(Control control)
        {
            if (control == null || control.IsDisposed || CardResizeGripService.IsResizeGrip(control))
                return;

            if (control is DataGridView || control is ToolStrip || control is PictureBox || control is WebBrowser)
                return;

            string text = (control.Text ?? string.Empty).Trim();
            bool textual = control is Label ||
                           control is Button ||
                           control is CheckBox ||
                           control is RadioButton ||
                           control is GroupBox ||
                           control is TabControl ||
                           control is TabPage;

            if (textual && !string.IsNullOrWhiteSpace(text) && !IsIconOnlyText(text))
            {
                Font target = ResolveSharedFont(control.Font, control);
                if (target != null && !SameFont(control.Font, target))
                    control.Font = target;
            }

            ApplyReadableForeColor(control);
        }

        private static Font ResolveSharedFont(Font current, Control control)
        {
            if (current == null)
                return DS.Body;

            string family = current.FontFamily.Name;
            if (string.Equals(family, "Consolas", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(family, "Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(family, "Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(family, "Marlett", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(family, "Webdings", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(family, "Wingdings", StringComparison.OrdinalIgnoreCase))
                return null;

            float size = current.Size;
            FontStyle style = current.Style;

            if (control is Button)
            {
                size = Math.Max(8.75f, Math.Min(size, 9f));
                style = FontStyle.Bold;
            }
            else if (control is CheckBox || control is RadioButton || control is GroupBox)
            {
                size = 9f;
            }
            else if (control is TabControl || control is TabPage)
            {
                size = 9f;
            }
            else if (control is Label)
            {
                if (size < 7.5f)
                    size = 7.5f;
                else if (size > 9.25f && size < 11f)
                    size = 9.5f;
            }

            return new Font("Segoe UI", size, style);
        }

        private static bool SameFont(Font left, Font right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.FontFamily.Name, right.FontFamily.Name, StringComparison.OrdinalIgnoreCase) &&
                   Math.Abs(left.Size - right.Size) < 0.01f &&
                   left.Style == right.Style;
        }

        private static void ApplyReadableForeColor(Control control)
        {
            if (control == null || control is TextBoxBase || control is ComboBox || control is DateTimePicker || control is NumericUpDown)
                return;

            Color back = ResolveEffectiveBackColor(control);
            if (back == Color.Empty || back == Color.Transparent)
                return;

            bool darkBack = back.GetBrightness() < 0.45f;
            bool darkText = control.ForeColor == Color.Empty ||
                            control.ForeColor == Color.Black ||
                            control.ForeColor == SystemColors.ControlText ||
                            control.ForeColor.GetBrightness() < 0.35f;

            if (darkBack && darkText)
                control.ForeColor = Color.White;
        }

        private static Color ResolveEffectiveBackColor(Control control)
        {
            Control current = control;
            while (current != null)
            {
                Color back = current.BackColor;
                if (back != Color.Empty && back != Color.Transparent)
                    return back;
                current = current.Parent;
            }

            return Color.Empty;
        }

        private static bool IsIconOnlyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            string trimmed = text.Trim();
            return trimmed.Length <= 2 ||
                   trimmed == "..." ||
                   trimmed == "â‹®" ||
                   trimmed == "⋮" ||
                   trimmed == "⋯" ||
                   trimmed == "<" ||
                   trimmed == ">" ||
                   trimmed == "|<" ||
                   trimmed == ">|";
        }

        private static void NormalizeReadableLabel(Label label)
        {
            if (label == null || label.IsDisposed || !label.Visible || CardResizeGripService.IsResizeGrip(label))
                return;

            string text = (label.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (label.Font.Size < 7.5f && !IsTinyUtilityLabel(label))
                label.Font = new Font(label.Font.FontFamily, 7.5f, label.Font.Style);

            if (label.AutoSize || label.Dock != DockStyle.None || label.Parent == null || label.Width <= 0)
                return;

            int measured = TextRenderer.MeasureText(text, label.Font).Width + 8;
            if (measured <= label.Width)
                return;

            int maxRight = Math.Max(label.Right, label.Parent.ClientSize.Width - label.Parent.Padding.Right - label.Margin.Right);
            int available = Math.Max(label.Width, maxRight - label.Left);
            int target = Math.Min(measured, available);
            if (target > label.Width)
                label.Width = target;

            label.AutoEllipsis = true;
        }

        private static bool IsTinyUtilityLabel(Label label)
        {
            string text = (label == null ? string.Empty : label.Text ?? string.Empty).Trim();
            return text.Length <= 3 || label.Height <= 12;
        }

        private static void NormalizeButton(Button button)
        {
            if (button == null || button.IsDisposed)
                return;
            if (IsSidebarNavButton(button))
                return;

            bool iconOnly = IsIconOnlyButton(button);
            ApplyButtonStyle(button, ResolveButtonRole(button));

            int targetHeight = iconOnly ? Math.Max(button.Height, 28) : Math.Max(button.Height, 36);
            int parentMaxHeight = GetButtonParentMaxHeight(button);
            if (parentMaxHeight > 0 && targetHeight > parentMaxHeight)
                targetHeight = parentMaxHeight;
            if (button.Height != targetHeight)
                button.Height = targetHeight;

            button.TextAlign = ContentAlignment.MiddleCenter;
            button.AutoEllipsis = false;
            if (iconOnly)
            {
                button.Padding = Padding.Empty;
                int iconWidth = (button.Text ?? string.Empty).Trim().Length >= 2 ? 42 : 36;
                if (button.Width < iconWidth)
                    button.Width = iconWidth;
            }
            else
            {
                button.Padding = new Padding(12, 0, 12, 0);
                if (!IsCompactUtilityButton(button) && !IsFixedWidthButton(button) && button.Width < 110)
                    button.Width = 110;
            }

            if (!TextFitConfiguredButtons.Contains(button))
            {
                TextFitConfiguredButtons.Add(button);
                button.Disposed += (s, e) => TextFitConfiguredButtons.Remove(button);
                button.TextChanged -= ButtonTextChangedForFit;
                button.TextChanged += ButtonTextChangedForFit;
                button.FontChanged -= ButtonTextChangedForFit;
                button.FontChanged += ButtonTextChangedForFit;
            }

            FitButtonText(button, iconOnly);
        }

        private static void ButtonTextChangedForFit(object sender, EventArgs e)
        {
            NormalizeButton(sender as Button);
            NormalizeButtonFlow((sender as Button)?.Parent as FlowLayoutPanel);
        }

        private static void FitButtonText(Button button, bool iconOnly)
        {
            if (button == null || button.IsDisposed || iconOnly)
                return;

            int requiredWidth = MeasureButtonWidth(button);
            if (requiredWidth <= 0)
                return;

            int requiredHeight = Math.Max(button.Height, TextRenderer.MeasureText(button.Text ?? string.Empty, button.Font).Height + 12);
            button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, requiredWidth), Math.Max(button.MinimumSize.Height, requiredHeight));

            if (button.Parent is FlowLayoutPanel)
            {
                if (CanFlowButtonGrow(button))
                {
                    button.AutoSize = true;
                    button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                }
                else
                {
                    button.AutoSize = false;
                    if (button.Width < requiredWidth)
                    {
                        button.Width = requiredWidth;
                        if (button.Parent is FlowLayoutPanel flow && flow.Height < button.Bottom + button.Margin.Bottom)
                            flow.WrapContents = true;
                    }
                }
                return;
            }

            if (button.Parent is TableLayoutPanel)
            {
                button.AutoSize = false;
                return;
            }

            bool horizontallyStretched = button.Dock == DockStyle.Fill
                || button.Dock == DockStyle.Top
                || button.Dock == DockStyle.Bottom
                || ((button.Anchor & AnchorStyles.Left) == AnchorStyles.Left && (button.Anchor & AnchorStyles.Right) == AnchorStyles.Right);
            if (!horizontallyStretched && button.Width < requiredWidth)
            {
                int targetWidth = requiredWidth;
                if (button.Parent != null && button.Parent.ClientSize.Width > 0)
                {
                    int available = button.Parent.ClientSize.Width - Math.Max(0, button.Left) - button.Margin.Right;
                    if (available > 0)
                        targetWidth = Math.Min(targetWidth, Math.Max(button.Width, available));
                }
                button.Width = targetWidth;
            }
        }

        private static bool IsFixedWidthButton(Button button)
        {
            string metadata = button == null || button.Tag == null ? string.Empty : button.Tag.ToString();
            return metadata.IndexOf("FIXED_WIDTH", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetButtonParentMaxHeight(Button button)
        {
            if (button == null || button.Parent == null || button.Parent.ClientSize.Height <= 0)
                return 0;

            int available = button.Parent.ClientSize.Height - Math.Max(0, button.Top) - button.Margin.Bottom;
            if (available <= 0 && (button.Parent is FlowLayoutPanel || button.Parent is TableLayoutPanel))
                available = button.Parent.ClientSize.Height - button.Margin.Vertical;

            return Math.Max(22, available);
        }

        private static bool CanFlowButtonGrow(Button button)
        {
            FlowLayoutPanel flow = button == null ? null : button.Parent as FlowLayoutPanel;
            if (flow == null)
                return false;

            string metadata = ((flow.Name ?? string.Empty) + " " + (flow.Tag == null ? string.Empty : flow.Tag.ToString())).ToUpperInvariant();
            if (ContainsAny(metadata, "PAGER", "PAGINATION", "TAB", "FILTER", "HEADER", "TOOLBAR", "STRIP", "LIBRARY"))
                return false;

            return flow.AutoSize && flow.ClientSize.Height >= 44;
        }

        private static bool HasFlowHorizontalRoom(Button button, int requiredWidth)
        {
            FlowLayoutPanel flow = button == null ? null : button.Parent as FlowLayoutPanel;
            if (flow == null || flow.ClientSize.Width <= 0)
                return false;

            int currentTotal = 0;
            foreach (Control child in flow.Controls)
            {
                if (!child.Visible)
                    continue;
                currentTotal += child.Width + child.Margin.Horizontal;
            }

            int projectedTotal = currentTotal - button.Width + requiredWidth;
            return projectedTotal <= flow.ClientSize.Width;
        }

        private static int MeasureButtonWidth(Button button)
        {
            string text = (button.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            Size textSize = TextRenderer.MeasureText(text, button.Font);
            int padding = Math.Max(18, button.Padding.Left + button.Padding.Right + 16);
            int imageWidth = button.Image == null ? 0 : button.Image.Width + Math.Max(6, button.ImageAlign == ContentAlignment.MiddleCenter ? 0 : 8);
            int minimum = (IsCompactUtilityButton(button) || IsFixedWidthButton(button)) ? 0 : 110;
            return Math.Max(Math.Max(button.Width, minimum), textSize.Width + padding + imageWidth);
        }

        /// <summary>Normalizes button containers and direct button groups to shared ERP spacing rules.</summary>
        public static void ApplyButtonContainerLayout(Control container)
        {
            if (container == null || container.IsDisposed)
                return;

            NormalizeButtonFlow(container as FlowLayoutPanel);
            NormalizeButtonGrid(container as TableLayoutPanel);
            NormalizeDirectButtonGroups(container);
            ApplyActionContainerPadding(container);
        }

        private static void NormalizeButtonFlow(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed)
                return;

            if (!FlowClampConfiguredControls.Contains(flow))
            {
                FlowClampConfiguredControls.Add(flow);
                flow.Disposed += (s, e) => FlowClampConfiguredControls.Remove(flow);
                flow.Layout += Flow_LayoutForButtonClamp;
            }

            List<Button> buttons = flow.Controls.OfType<Button>().Where(button => button.Visible && !IsIconOnlyButton(button)).ToList();
            if (buttons.Count == 0)
            {
                ClampFlowButtons(flow);
                return;
            }

            foreach (Button button in buttons)
            {
                if (button.Margin == Padding.Empty || button.Margin.Right < 8)
                    button.Margin = new Padding(0, 0, 8, 8);
                NormalizeButton(button);
            }
            ClampFlowButtons(flow);
        }

        private static void Flow_LayoutForButtonClamp(object sender, LayoutEventArgs e)
        {
            ClampFlowButtons(sender as FlowLayoutPanel);
        }

        private static void ClampFlowButtons(FlowLayoutPanel flow)
        {
            if (flow == null || flow.IsDisposed || flow.ClientSize.Height <= 0)
                return;

            foreach (Button button in flow.Controls.OfType<Button>().Where(button => button.Visible))
            {
                int availableHeight = flow.ClientSize.Height - Math.Max(0, button.Top) - button.Margin.Bottom;
                if (availableHeight > 0 && button.Height > availableHeight)
                    button.Height = Math.Max(22, availableHeight);

                int availableWidth = flow.ClientSize.Width - Math.Max(0, button.Left) - button.Margin.Right;
                if (availableWidth > 0 && button.Width > availableWidth)
                {
                    int minimumWidth = IsIconOnlyButton(button)
                        ? Math.Max(24, button.MinimumSize.Width)
                        : Math.Max(button.MinimumSize.Width, MeasureButtonWidth(button));
                    button.Width = Math.Max(minimumWidth, availableWidth);
                }
            }
        }

        private static void NormalizeButtonGrid(TableLayoutPanel table)
        {
            if (table == null || table.IsDisposed)
                return;

            List<Button> buttons = table.Controls.OfType<Button>().Where(button => button.Visible && !IsIconOnlyButton(button)).ToList();
            if (buttons.Count == 0)
                return;

            foreach (Button button in buttons)
            {
                if (button.Margin == Padding.Empty || button.Margin.Right < 8)
                    button.Margin = new Padding(0, 0, 8, 8);
                NormalizeButton(button);
            }
        }

        private static void RebuildButtonGrid(TableLayoutPanel table, List<Button> buttons, int columns)
        {
            if (table == null || buttons == null || buttons.Count == 0)
                return;

            columns = Math.Max(1, Math.Min(columns, buttons.Count));
            int rows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)columns));
            table.SuspendLayout();
            table.ColumnStyles.Clear();
            table.RowStyles.Clear();
            table.ColumnCount = columns;
            table.RowCount = rows;

            for (int i = 0; i < columns; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (int i = 0; i < rows; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

            for (int i = 0; i < buttons.Count; i++)
            {
                table.SetColumn(buttons[i], i % columns);
                table.SetRow(buttons[i], i / columns);
            }

            table.ResumeLayout(true);
        }

        private static void NormalizeTabControl(TabControl tabs)
        {
            if (tabs == null || tabs.IsDisposed || tabs.TabPages.Count == 0)
                return;

            int maxWidth = tabs.ItemSize.Width;
            int maxHeight = Math.Max(28, tabs.ItemSize.Height);
            int totalWidth = 0;
            foreach (TabPage page in tabs.TabPages)
            {
                string text = (page.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                int width = TextRenderer.MeasureText(text, tabs.Font).Width + 30;
                maxWidth = Math.Max(maxWidth, width);
                totalWidth += width;
            }

            if (maxWidth > tabs.ItemSize.Width)
            {
                tabs.SizeMode = TabSizeMode.Fixed;
                tabs.ItemSize = new Size(maxWidth, maxHeight);
            }

            if (totalWidth > tabs.ClientSize.Width && tabs.ClientSize.Width > 0)
                tabs.Multiline = true;
        }

        private static void NormalizeDirectButtonGroups(Control parent)
        {
            if (parent == null || parent.IsDisposed || parent.Controls.Count < 2)
                return;

            List<Button> buttons = parent.Controls.OfType<Button>().Where(button => button.Visible).ToList();
            if (buttons.Count < 2)
                return;

            if (parent is FlowLayoutPanel || parent is TableLayoutPanel)
            {
                NormalizeButtonRow(buttons);
                return;
            }

            foreach (List<Button> row in GroupButtonsByRow(buttons))
            {
                if (row.Count >= 2)
                    NormalizeButtonRow(row);
            }
        }

        private static IEnumerable<List<Button>> GroupButtonsByRow(List<Button> buttons)
        {
            foreach (Button button in buttons.OrderBy(button => button.Top).ThenBy(button => button.Left))
            {
                List<Button> row = buttons
                    .Where(other => Math.Abs(GetVerticalCenter(other) - GetVerticalCenter(button)) <= 8)
                    .OrderBy(other => other.Left)
                    .ToList();

                if (row.Count >= 2 && row[0] == button)
                    yield return row;
            }
        }

        private static void NormalizeButtonRow(List<Button> buttons)
        {
            if (buttons == null || buttons.Count < 2)
                return;

            int targetTop = buttons.Min(button => button.Top);
            int targetHeight = buttons.Max(button => Math.Max(IsIconOnlyButton(button) ? 26 : 34, button.Height));

            foreach (Button button in buttons)
            {
                NormalizeButton(button);
                if (!(button.Parent is FlowLayoutPanel) && !(button.Parent is TableLayoutPanel))
                    button.Top = targetTop;
                int parentMaxHeight = GetButtonParentMaxHeight(button);
                button.Height = parentMaxHeight > 0 ? Math.Min(targetHeight, parentMaxHeight) : targetHeight;
                if (button.Parent is TableLayoutPanel)
                    button.Margin = new Padding(0, 0, 8, 0);
                else if (button.Margin == Padding.Empty || button.Margin.Right < 6)
                    button.Margin = new Padding(0, 0, 8, 0);
            }

            if (!AlignFooterButtonRow(buttons))
                AlignInlineButtonRow(buttons);
        }

        private static int GetVerticalCenter(Control control)
        {
            return control.Top + control.Height / 2;
        }

        private static bool IsIconOnlyButton(Button button)
        {
            string text = (button == null ? string.Empty : button.Text ?? string.Empty).Trim();
            if (text.Length <= 2)
                return true;

            return text == "..." || text == "⋯" || text == "<" || text == ">" || text == "|<" || text == ">|";
        }

        private static bool IsCompactUtilityButton(Button button)
        {
            if (button == null)
                return true;

            if (IsIconOnlyButton(button))
                return true;

            string text = (button.Text ?? string.Empty).Trim();
            string metadata = ((button.Name ?? string.Empty) + " " +
                               (button.Tag == null ? string.Empty : button.Tag.ToString()) + " " +
                               (button.Parent == null ? string.Empty : button.Parent.Name ?? string.Empty) + " " +
                               (button.Parent == null || button.Parent.Tag == null ? string.Empty : button.Parent.Tag.ToString()))
                .ToUpperInvariant();

            if (ContainsAny(metadata, "PAGER", "PAGINATION", "TAB", "CHIP", "PIPELINE", "STEP", "STATUS", "BADGE", "AVATAR"))
                return true;

            return text.Length <= 3 && button.Width <= 72;
        }

        private static bool AlignFooterButtonRow(List<Button> buttons)
        {
            if (buttons == null || buttons.Count < 2)
                return false;

            Control parent = buttons[0].Parent;
            if (parent == null || parent is FlowLayoutPanel || parent is TableLayoutPanel || parent.ClientSize.Width <= 0)
                return false;

            bool footer = LooksLikeFooterButtonGroup(parent, buttons);
            if (!footer)
                return false;

            List<Button> ordered = buttons
                .OrderBy(button => GetButtonRoleSort(ResolveButtonRole(button)))
                .ThenBy(button => button.Left)
                .ToList();

            int totalWidth = ordered.Sum(button => button.Width) + ((ordered.Count - 1) * 8);
            int x = Math.Max(parent.Padding.Left, parent.ClientSize.Width - parent.Padding.Right - totalWidth);
            int y = ordered.Min(button => button.Top);
            foreach (Button button in ordered)
            {
                button.Location = new Point(x, y);
                x += button.Width + 8;
                button.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            }

            return true;
        }

        private static void AlignInlineButtonRow(List<Button> buttons)
        {
            if (buttons == null || buttons.Count < 2)
                return;

            Control parent = buttons[0].Parent;
            if (parent == null || parent is FlowLayoutPanel || parent is TableLayoutPanel || parent.ClientSize.Width <= 0)
                return;

            List<Button> ordered = buttons.OrderBy(button => button.Left).ToList();
            int gap = 8;
            for (int i = 1; i < ordered.Count; i++)
            {
                int minLeft = ordered[i - 1].Right + gap;
                if (ordered[i].Left < minLeft)
                    ordered[i].Left = minLeft;
            }

            int overflow = ordered.Last().Right - (parent.ClientSize.Width - parent.Padding.Right);
            if (overflow > 0)
            {
                int leftLimit = parent.Padding.Left;
                int totalWidth = ordered.Sum(button => button.Width) + gap * (ordered.Count - 1);
                if (totalWidth <= parent.ClientSize.Width - parent.Padding.Horizontal)
                {
                    int start = Math.Max(leftLimit, parent.ClientSize.Width - parent.Padding.Right - totalWidth);
                    foreach (Button button in ordered)
                    {
                        button.Left = start;
                        start += button.Width + gap;
                    }
                }
            }
        }

        private static bool LooksLikeFooterButtonGroup(Control parent, List<Button> buttons)
        {
            if (parent == null || buttons == null || buttons.Count == 0)
                return false;

            string metadata = ((parent.Name ?? string.Empty) + " " + (parent.Tag == null ? string.Empty : parent.Tag.ToString())).ToUpperInvariant();
            if (ContainsAny(metadata, "FOOTER", "DIALOG", "BOTTOM", "ACTIONS"))
                return true;

            Form form = parent.FindForm();
            int parentHeight = parent.ClientSize.Height > 0 ? parent.ClientSize.Height : parent.Height;
            int bottom = buttons.Max(button => button.Bottom);
            bool nearBottom = parentHeight > 0 && bottom >= parentHeight - 84;
            bool hasConfirmPair = buttons.Any(button => ResolveButtonRole(button) == ButtonRole.Primary) &&
                                  buttons.Any(button => ResolveButtonRole(button) == ButtonRole.Neutral);
            return nearBottom && (form != null && form.FormBorderStyle != FormBorderStyle.None || hasConfirmPair);
        }

        private static int GetButtonRoleSort(ButtonRole role)
        {
            switch (role)
            {
                case ButtonRole.Neutral: return 0;
                case ButtonRole.Secondary: return 1;
                case ButtonRole.Danger: return 2;
                default: return 3;
            }
        }

        private static void ApplyActionContainerPadding(Control container)
        {
            if (container == null || container.IsDisposed || container.Controls.Count == 0)
                return;

            if (!container.Controls.OfType<Button>().Any(button => button.Visible && !IsIconOnlyButton(button)))
                return;

            string metadata = ((container.Name ?? string.Empty) + " " + (container.Tag == null ? string.Empty : container.Tag.ToString())).ToUpperInvariant();
            bool actionSurface = ContainsAny(metadata, "ACTION", "FOOTER", "BUTTON", "TOOLBAR", "COMMAND", "QUICK") ||
                                 container.Controls.OfType<Button>().Count(button => button.Visible) >= 2;
            if (!actionSurface)
                return;

            if (container.Padding == Padding.Empty)
                container.Padding = new Padding(0, 0, 0, 0);
        }

        private static void PreventTextButtonOverlap(Control parent)
        {
            if (parent == null || parent.IsDisposed || parent.Controls.Count < 2)
                return;

            if (!OverlapGuardConfiguredControls.Contains(parent))
            {
                OverlapGuardConfiguredControls.Add(parent);
                parent.Disposed += (s, e) => OverlapGuardConfiguredControls.Remove(parent);
                parent.Resize += ParentResizeForOverlapGuard;
                parent.Layout += ParentLayoutForOverlapGuard;
            }

            if (parent is FlowLayoutPanel || parent is TableLayoutPanel || IsSidebarContainer(parent))
                return;

            List<Button> buttons = parent.Controls.OfType<Button>()
                .Where(button => button.Visible && !IsSidebarNavButton(button) && !IsIconOnlyButton(button))
                .ToList();
            if (buttons.Count == 0)
                return;

            foreach (Control textControl in parent.Controls.Cast<Control>().Where(IsOverlapSensitiveTextControl))
            {
                foreach (Button button in buttons)
                {
                    if (!VerticallyIntersects(textControl.Bounds, button.Bounds))
                        continue;

                    if (textControl.Left < button.Left && textControl.Right > button.Left - 8)
                    {
                        int targetWidth = button.Left - textControl.Left - 12;
                        if (targetWidth >= 36 && targetWidth < textControl.Width)
                        {
                            textControl.Width = targetWidth;
                            EnableTextEllipsis(textControl);
                        }
                    }
                    else if (button.Left < textControl.Left && button.Right > textControl.Left - 8)
                    {
                        int newLeft = button.Right + 8;
                        int available = parent.ClientSize.Width - parent.Padding.Right - newLeft;
                        if (available >= 36)
                        {
                            textControl.Left = newLeft;
                            textControl.Width = Math.Min(textControl.Width, available);
                            EnableTextEllipsis(textControl);
                        }
                    }
                }
            }
        }

        private static void ParentResizeForOverlapGuard(object sender, EventArgs e)
        {
            PreventTextButtonOverlap(sender as Control);
        }

        private static void ParentLayoutForOverlapGuard(object sender, LayoutEventArgs e)
        {
            PreventTextButtonOverlap(sender as Control);
        }

        private static bool IsOverlapSensitiveTextControl(Control control)
        {
            if (control == null || !control.Visible || CardResizeGripService.IsResizeGrip(control))
                return false;

            if (control is Button || control is TextBoxBase || control is ComboBox || control is DateTimePicker || control is NumericUpDown)
                return false;

            if (control.Dock != DockStyle.None || control.AutoSize)
                return false;

            string text = (control.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) || IsIconOnlyText(text))
                return false;

            return control is Label || control is LinkLabel || control is CheckBox || control is RadioButton;
        }

        private static void EnableTextEllipsis(Control control)
        {
            Label label = control as Label;
            if (label != null)
            {
                label.AutoEllipsis = true;
                return;
            }

            CheckBox checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.AutoEllipsis = true;
                return;
            }

            RadioButton radioButton = control as RadioButton;
            if (radioButton != null)
                radioButton.AutoEllipsis = true;
        }

        private static bool VerticallyIntersects(Rectangle left, Rectangle right)
        {
            return left.Top < right.Bottom && right.Top < left.Bottom;
        }

        private static bool IsSidebarContainer(Control control)
        {
            string metadata = ((control.Name ?? string.Empty) + " " + (control.Tag == null ? string.Empty : control.Tag.ToString())).ToUpperInvariant();
            return ContainsAny(metadata, "SIDEBAR", "NAV");
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
                if (IsSidebarNavButton(button))
                    return;
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
            ApplyButtonStyle(button, ResolveButtonRole(button));
        }

        public static UiActionVariant ResolveActionVariant(string text)
        {
            string key = (text ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(key, "delete", "void", "remove", "reset", "clear", "archive", "blacklist"))
                return UiActionVariant.Danger;
            if (ContainsAny(key, "convert to purchase order", "convert to invoice", "whatsapp follow-up"))
                return UiActionVariant.Secondary;
            if (ContainsAny(key, "save", "approve", "record payment", "post", "finalise", "finalize", "resolve"))
                return UiActionVariant.Primary;
            if (ContainsAny(key, "new", "create", "add", "generate", "submit", "dispatch", "send for approval"))
                return UiActionVariant.Primary;
            if (ContainsAny(key, "cancel", "close", "back", "done"))
                return UiActionVariant.Ghost;
            if (ContainsAny(key, "preview", "import", "template", "forms", "open", "upload", "refresh", "export", "print", "view", "test", "compare", "load"))
                return UiActionVariant.Secondary;
            return UiActionVariant.Secondary;
        }

        public static ButtonRole ResolveButtonRole(Button button)
        {
            if (IsSidebarNavButton(button))
                return ButtonRole.Secondary;

            string key = ((button == null ? string.Empty : button.Name ?? string.Empty) + " " +
                          (button == null ? string.Empty : button.Text ?? string.Empty))
                .ToLowerInvariant();

            if (ContainsAny(key, "delete", "reset", "remove", "clear", "void", "archive", "blacklist", "disconnect"))
                return ButtonRole.Danger;
            if (ContainsAny(key, "cancel", "close", "back", "done", "ok"))
                return ButtonRole.Neutral;
            if (ContainsAny(key, "save", "create", "add", "new", "generate", "submit", "approve", "post", "record payment", "resolve", "sync selected", "backup now"))
                return ButtonRole.Primary;
            if (ContainsAny(key, "open", "view", "test", "refresh", "export", "import", "print", "preview", "template", "forms", "upload", "browse", "load", "filter", "copy", "compare", "download"))
                return ButtonRole.Secondary;

            UiActionVariant variant = ResolveActionVariant(button == null ? string.Empty : button.Text);
            switch (variant)
            {
                case UiActionVariant.Danger:
                    return ButtonRole.Danger;
                case UiActionVariant.Ghost:
                    return ButtonRole.Neutral;
                case UiActionVariant.Primary:
                case UiActionVariant.Success:
                    return ButtonRole.Primary;
                default:
                    return ButtonRole.Secondary;
            }
        }

        public static void ApplyActionButton(Button button, UiActionVariant variant)
        {
            if (button == null)
                return;

            switch (variant)
            {
                case UiActionVariant.Primary:
                case UiActionVariant.Success:
                    ApplyButtonStyle(button, ButtonRole.Primary);
                    return;
                case UiActionVariant.Danger:
                    ApplyButtonStyle(button, ButtonRole.Danger);
                    return;
                case UiActionVariant.Ghost:
                    ApplyButtonStyle(button, ButtonRole.Neutral);
                    return;
                default:
                    ApplyButtonStyle(button, ButtonRole.Secondary);
                    return;
            }
        }

        public static void ApplyButtonStyle(Button button, ButtonRole role)
        {
            if (button == null || button.IsDisposed)
                return;
            if (IsSidebarNavButton(button))
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Segoe UI", Math.Max(9f, Math.Min(10f, button.Font.Size <= 0 ? 9f : button.Font.Size)), FontStyle.Bold);
            button.Height = Math.Max(button.Height, IsIconOnlyButton(button) ? 28 : 36);
            button.Padding = IsIconOnlyButton(button) ? Padding.Empty : new Padding(12, 0, 12, 0);

            Color bg;
            Color fg;
            Color border;
            Color hover;
            Color down;

            switch (role)
            {
                case ButtonRole.Danger:
                    bg = Color.FromArgb(220, 38, 38);
                    fg = Color.White;
                    border = bg;
                    hover = Color.FromArgb(185, 28, 28);
                    down = Color.FromArgb(153, 27, 27);
                    break;
                case ButtonRole.Neutral:
                    bg = Color.FromArgb(249, 250, 251);
                    fg = Color.FromArgb(55, 65, 81);
                    border = Color.FromArgb(209, 213, 219);
                    hover = DS.Slate200;
                    down = DS.Slate300;
                    break;
                case ButtonRole.Secondary:
                    bg = Color.White;
                    fg = Color.FromArgb(17, 24, 39);
                    border = DS.InputBorder;
                    hover = DS.BgCardHov;
                    down = DS.Slate300;
                    break;
                default:
                    bg = Color.FromArgb(37, 99, 235);
                    fg = Color.White;
                    border = bg;
                    hover = Color.FromArgb(29, 78, 216);
                    down = Color.FromArgb(30, 64, 175);
                    break;
            }

            button.BackColor = bg;
            button.ForeColor = fg;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = button.Focused ? Color.FromArgb(37, 99, 235) : border;
            button.FlatAppearance.MouseOverBackColor = hover;
            button.FlatAppearance.MouseDownBackColor = down;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextImageRelation = button.Image == null ? TextImageRelation.Overlay : TextImageRelation.ImageBeforeText;

            if (!IsIconOnlyButton(button) && !IsCompactUtilityButton(button))
            {
                button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, 110), Math.Max(button.MinimumSize.Height, 36));
                if (!IsFixedWidthButton(button) && button.Width < 110)
                    button.Width = 110;
            }

            ConfigureButtonFocusVisual(button, border);
            DS.Rounded(button, DS.RadiusSm);
        }

        public static void ApplyActionButton(Button button)
        {
            ApplyButtonStyle(button, ResolveButtonRole(button));
        }

        private static void ConfigureButtonFocusVisual(Button button, Color normalBorder)
        {
            if (button == null)
                return;
            if (IsSidebarNavButton(button))
                return;

            ButtonNormalBorderColors[button] = normalBorder;
            if (ButtonFocusConfiguredButtons.Contains(button))
                return;

            ButtonFocusConfiguredButtons.Add(button);
            button.Disposed += (s, e) =>
            {
                ButtonFocusConfiguredButtons.Remove(button);
                ButtonNormalBorderColors.Remove(button);
            };
            button.Enter += ButtonFocusChanged;
            button.Leave += ButtonFocusChanged;
        }

        private static void ButtonFocusChanged(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null || button.IsDisposed)
                return;

            Color normal = ExtractNormalButtonBorder(button);
            button.FlatAppearance.BorderColor = button.Focused ? Color.FromArgb(37, 99, 235) : normal;
            button.Invalidate();
        }

        private static Color ExtractNormalButtonBorder(Button button)
        {
            Color stored;
            if (button != null && ButtonNormalBorderColors.TryGetValue(button, out stored))
                return stored;

            string tag = button == null || button.Tag == null ? string.Empty : button.Tag.ToString();
            const string marker = "BUTTON_NORMAL_BORDER:";
            int index = tag.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                int start = index + marker.Length;
                int end = tag.IndexOf(' ', start);
                string raw = end > start ? tag.Substring(start, end - start) : tag.Substring(start);
                int argb;
                if (int.TryParse(raw, out argb))
                    return Color.FromArgb(argb);
            }

            return Color.FromArgb(209, 213, 219);
        }

        public static bool TrySetClipboardText(IWin32Window owner, string text, string title)
        {
            try
            {
                Clipboard.SetText(text ?? string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                string resolvedTitle = string.IsNullOrWhiteSpace(title) ? BrandingService.WindowTitle("Copy") : title;
                AppLogger.LogError("UIHelper.TrySetClipboardText", ex);
                MessageBox.Show(
                    owner,
                    "Windows clipboard is busy right now. Please try the copy action again.",
                    resolvedTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
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

        private static bool IsSidebarNavButton(Button button)
        {
            if (button == null)
                return false;

            return string.Equals(button.GetType().Name, "SidebarNavButton", StringComparison.Ordinal) ||
                   button.Tag is int && button.Parent != null && button.Parent.GetType().Name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   button.AccessibleName != null;
        }

        private static void ApplyModernPanelStyle(Panel panel)
        {
            if (panel == null)
                return;

            if (ShouldConvertLegacyPanelBackColor(panel))
                panel.BackColor = ShouldPaintCardSurface(panel) ? DS.BgCard : DS.BgPage;

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

            string metadata = ((panel.Name ?? string.Empty) + " " + (panel.Tag == null ? string.Empty : panel.Tag.ToString())).ToUpperInvariant();
            if (ContainsAny(metadata, "NO_CARD_SURFACE", "NO_INPUT_HOST", "NO_INPUT_OUTLINE_HOST"))
                return false;

            if (panel.Dock == DockStyle.Fill && panel.Parent is Form)
                return false;

            string name = (panel.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU", "BANNER"))
                return false;

            if (HasLocallyPaintedSurfaceTag(panel))
                return true;

            bool cardName = ContainsAny(name, "CARD", "SUMMARY", "KPI", "WIDGET", "METRIC", "STAT", "TILE");
            bool whiteSurface = DS.IsLegacyLightBackColor(panel.BackColor) || panel.BackColor == DS.BgCard;
            return panel.HasChildren && cardName && whiteSurface && !HasCardLikeChild(panel);
        }

        private static bool HasLocallyPaintedSurfaceTag(Control control)
        {
            string metadata = ((control == null ? string.Empty : control.Name ?? string.Empty) + " " +
                               (control == null || control.Tag == null ? string.Empty : control.Tag.ToString()))
                .ToUpperInvariant();
            return ContainsAny(metadata, "GLOBAL_CARD_SURFACE", "SERVO_CARD_SURFACE", "DASHBOARD-CARD", "DASHBOARD_CARD", "METRIC-CARD", "METRIC_CARD");
        }

        private static bool HasCardLikeChild(Control control)
        {
            if (control == null)
                return false;

            foreach (Control child in control.Controls)
            {
                string metadata = ((child.Name ?? string.Empty) + " " + (child.Tag == null ? string.Empty : child.Tag.ToString())).ToUpperInvariant();
                if (ContainsAny(metadata, "CARD", "SUMMARY", "KPI", "WIDGET", "METRIC", "STAT", "TILE"))
                    return true;
            }

            return false;
        }

        private static bool ShouldConvertLegacyPanelBackColor(Panel panel)
        {
            if (panel == null || !DS.IsLegacyLightBackColor(panel.BackColor))
                return false;

            string metadata = ((panel.Name ?? string.Empty) + " " + (panel.Tag == null ? string.Empty : panel.Tag.ToString())).ToUpperInvariant();
            if (ContainsAny(metadata, "SIDEBAR", "NAV", "MENU", "BANNER", "WEBVIEW", "MAP", "CHART", "PREVIEW", "PDF"))
                return false;

            return true;
        }

        private static void ModernCardPanelPaint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null || panel.Width < 4 || panel.Height < 4)
                return;

            DS.DrawCleanBorder(e.Graphics, panel.ClientRectangle, DS.RadiusLg, DS.Border);
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

            if (scrollable.AutoScroll && ShouldEnableAutoScroll(control))
            {
                scrollable.AutoScrollMargin = new Size(0, 24);
                if (!(control is FlowLayoutPanel))
                    SuppressPageHorizontalScroll(scrollable);
            }

            ScrollConfiguredControls.Add(control);
            control.Disposed += (s, e) => ScrollConfiguredControls.Remove(control);
        }

        private static void SuppressPageHorizontalScroll(ScrollableControl scrollable)
        {
            if (scrollable == null)
                return;

            scrollable.HorizontalScroll.Enabled = false;
            scrollable.HorizontalScroll.Visible = false;
            Size minSize = scrollable.AutoScrollMinSize;
            if (minSize.Width != 0)
                scrollable.AutoScrollMinSize = new Size(0, minSize.Height);
        }

        private static bool ShouldEnableAutoScroll(Control control)
        {
            if (control is DashboardForm)
                return false;

            if (control is FlowLayoutPanel flow)
                return !IsChromeOrToolbarControl(control) && flow.AutoScroll;

            if (control is TableLayoutPanel)
                return false;

            if (!(control is Panel))
                return false;

            if (control.Parent is TableLayoutPanel)
                return false;

            Panel parentPanel = control.Parent as Panel;
            if (parentPanel != null && IsLikelyCardContainer(parentPanel))
                return false;

            string name = (control.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "SIDEBAR", "NAV", "TOOLBAR", "HEADER", "FOOTER", "STRIP", "MENU"))
                return false;

            bool likelyPageContainer = control.Dock == DockStyle.Fill
                || ContainsAny(name, "CONTENT", "BODY", "MAIN", "PAGE", "WORKSPACE", "SCROLL", "CONTAINER");

            return likelyPageContainer;
        }

        private static bool IsLikelyCardContainer(Panel panel)
        {
            if (panel == null)
                return false;

            if (Equals(panel.Tag, "settings-card"))
                return true;

            string name = (panel.Name ?? string.Empty).ToUpperInvariant();
            if (ContainsAny(name, "CARD", "TILE", "SUMMARY", "WIDGET", "KPI"))
                return true;

            bool whiteSurface = DS.IsLegacyLightBackColor(panel.BackColor) || panel.BackColor == DS.BgCard;
            bool boundedSurface = panel.Dock != DockStyle.Fill || panel.Margin != Padding.Empty || panel.Padding != Padding.Empty;
            bool hasCardContent = panel.Controls.OfType<Label>().Any()
                || panel.Controls.OfType<Button>().Any()
                || panel.Controls.OfType<DataGridView>().Any()
                || panel.Controls.OfType<TableLayoutPanel>().Any();

            return whiteSurface && boundedSurface && hasCardContent;
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

        public static string BuildWorkflowHintText(params string[] steps)
        {
            List<string> cleanSteps = (steps ?? new string[0])
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .Select(step => step.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanSteps.Count < 2)
                return string.Empty;

            return "Typical flow: " + string.Join(" -> ", cleanSteps);
        }

    }
}
