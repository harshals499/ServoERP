using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class InputOutlineService
    {
        private const string FrameNamePrefix = "__servoerpInputFrame";
        private static readonly HashSet<Control> ConfiguredControls = new HashSet<Control>();
        private static readonly HashSet<Control> OutlinedHosts = new HashSet<Control>();
        private static readonly HashSet<Control> FocusFrameParents = new HashSet<Control>();
        private static readonly Dictionary<Control, InputFrame> InputFrames = new Dictionary<Control, InputFrame>();
        private static readonly HashSet<Form> WatchedForms = new HashSet<Form>();
        private static Timer GlobalWatcherTimer;
        private static bool GlobalWatcherInstalled;

        public static bool IsInputFrame(Control control)
        {
            return control != null &&
                   !string.IsNullOrEmpty(control.Name) &&
                   control.Name.StartsWith(FrameNamePrefix, StringComparison.Ordinal);
        }

        /// <summary>Installs app-wide input theming for normal pages, subpages, and modal forms.</summary>
        public static void InstallGlobalApplicationWatcher()
        {
            if (GlobalWatcherInstalled)
                return;

            GlobalWatcherInstalled = true;
            Application.Idle += (s, e) => ApplyToOpenForms();
            GlobalWatcherTimer = new Timer { Interval = 750 };
            GlobalWatcherTimer.Tick += (s, e) => ApplyToOpenForms();
            GlobalWatcherTimer.Start();
        }

        public static void ApplyToTree(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            ConfigureControl(root);

            foreach (Control child in root.Controls)
                ApplyToTree(child);
        }

        public static bool IsOutlinedInputHost(Control control)
        {
            return control != null && OutlinedHosts.Contains(control);
        }

        private static void ConfigureControl(Control control)
        {
            if (control == null || control.IsDisposed)
                return;
            if (IsInputFrame(control))
                return;

            if (!ConfiguredControls.Contains(control))
            {
                ConfiguredControls.Add(control);
                control.Disposed += (s, e) =>
                {
                    ConfiguredControls.Remove(control);
                    OutlinedHosts.Remove(control);
                    FocusFrameParents.Remove(control);
                };
                control.ControlAdded -= ControlAdded;
                control.ControlAdded += ControlAdded;
            }

            ConfigureInput(control);
            ConfigureCheckableInput(control as CheckBox);
            AttachNativeFocusFrame(control);
            AttachVisibleInputFrame(control);

            if (IsInputHost(control))
                AttachHostOutline(control);
        }

        private static void ControlAdded(object sender, ControlEventArgs e)
        {
            ApplyToTree(e.Control);
            Control parent = sender as Control;
            if (parent != null && IsInputHost(parent))
                AttachHostOutline(parent);
            if (parent != null)
                AttachParentFocusPainter(parent);
        }

        private static void ConfigureInput(Control control)
        {
            if (IsInsideCustomInputShell(control))
                return;

            TextBoxBase text = control as TextBoxBase;
            if (text != null)
            {
                text.BackColor = text.Enabled && !text.ReadOnly ? Color.White : DS.InputDisabledBack;
                text.ForeColor = text.Enabled && !text.ReadOnly ? DS.InputText : DS.InputMutedText;
                text.MinimumSize = new Size(text.MinimumSize.Width, text.Multiline ? Math.Max(text.MinimumSize.Height, 36) : 32);
                text.BorderStyle = HasInputOutlineHostCandidate(text) ? BorderStyle.None : BorderStyle.FixedSingle;
                if (!text.Multiline && text.Height < 32)
                    text.Height = 32;
                return;
            }

            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                combo.BackColor = combo.Enabled ? Color.White : DS.InputDisabledBack;
                combo.ForeColor = combo.Enabled ? DS.InputText : DS.InputMutedText;
                combo.FlatStyle = HasInputOutlineHostCandidate(combo) ? FlatStyle.Flat : FlatStyle.Standard;
                combo.MinimumSize = new Size(combo.MinimumSize.Width, 32);
                combo.Height = Math.Max(combo.Height, 32);
                return;
            }

            NumericUpDown numeric = control as NumericUpDown;
            if (numeric != null)
            {
                numeric.BackColor = numeric.Enabled ? Color.White : DS.InputDisabledBack;
                numeric.ForeColor = numeric.Enabled ? DS.InputText : DS.InputMutedText;
                numeric.MinimumSize = new Size(numeric.MinimumSize.Width, 32);
                numeric.BorderStyle = HasInputOutlineHostCandidate(numeric) ? BorderStyle.None : BorderStyle.FixedSingle;
                numeric.Height = Math.Max(numeric.Height, 32);
                return;
            }

            DateTimePicker datePicker = control as DateTimePicker;
            if (datePicker != null)
            {
                datePicker.CalendarForeColor = DS.InputText;
                datePicker.CalendarMonthBackground = Color.White;
                datePicker.MinimumSize = new Size(datePicker.MinimumSize.Width, 32);
                datePicker.Height = Math.Max(datePicker.Height, 32);
            }
        }

        private static void ConfigureCheckableInput(CheckBox checkBox)
        {
            if (checkBox == null || checkBox.IsDisposed || IsInsideCustomInputShell(checkBox))
                return;

            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.ForeColor = checkBox.Enabled ? DS.Slate900 : DS.InputMutedText;
            checkBox.Font = DS.Body;
            checkBox.BackColor = ResolveReadableBackColor(checkBox);
            checkBox.FlatAppearance.BorderColor = DS.InputBorder;
            checkBox.FlatAppearance.CheckedBackColor = DS.Primary50;
            checkBox.FlatAppearance.MouseOverBackColor = DS.Slate50;
            checkBox.FlatAppearance.MouseDownBackColor = DS.Slate100;
            checkBox.Padding = new Padding(2, 0, 0, 0);
        }

        private static void AttachNativeFocusFrame(Control control)
        {
            if (!IsEditableInput(control) || control.Parent == null || ShouldSkipInputHost(control.Parent) || FindInputHost(control) != null)
                return;

            control.Enter -= InputFocusChanged;
            control.Enter += InputFocusChanged;
            control.Leave -= InputFocusChanged;
            control.Leave += InputFocusChanged;
            control.EnabledChanged -= InputStateChanged;
            control.EnabledChanged += InputStateChanged;

            TextBoxBase text = control as TextBoxBase;
            if (text != null)
            {
                text.ReadOnlyChanged -= InputStateChanged;
                text.ReadOnlyChanged += InputStateChanged;
            }

            AttachParentFocusPainter(control.Parent);
            EnsureFieldRowCanShowInput(control);
        }

        private static void AttachVisibleInputFrame(Control control)
        {
            if (!IsEditableInput(control) || control.Parent == null || IsInsideCustomInputShell(control))
                return;
            if (control.Parent is DataGridView || control.Parent is ToolStrip || control is ToolStrip)
                return;

            // TableLayoutPanel assigns every direct child to a cell and grows (adds rows/columns)
            // when controls are added without an explicit cell position. The 4 frame panels below
            // are added with no assigned cell, so on a fully-populated grid (e.g. AddAMCForm's
            // 13-row x 2-col layout) every input's frame forces 4 new auto-grown rows, and the
            // resulting Resize/Layout cascade across all frames made forms with grid layouts take
            // 50+ seconds to open. Inputs already get a native border from ApplyInputStyle/
            // ConfigureInput in this case, so skip the cosmetic overlay frame entirely.
            if (control.Parent is TableLayoutPanel)
                return;

            InputFrame frame;
            if (!InputFrames.TryGetValue(control, out frame) || frame == null || frame.Parent != control.Parent)
            {
                RemoveVisibleInputFrame(control);
                frame = InputFrame.Create(control);
                InputFrames[control] = frame;

                control.Disposed += InputFrameControlChanged;
                control.ParentChanged += InputFrameControlChanged;
                control.LocationChanged += InputFrameControlChanged;
                control.SizeChanged += InputFrameControlChanged;
                control.VisibleChanged += InputFrameControlChanged;
                control.EnabledChanged += InputFrameControlChanged;
                control.Enter += InputFrameControlChanged;
                control.Leave += InputFrameControlChanged;
                control.MouseEnter += InputFrameControlChanged;
                control.MouseLeave += InputFrameControlChanged;

                TextBoxBase text = control as TextBoxBase;
                if (text != null)
                    text.ReadOnlyChanged += InputFrameControlChanged;

                control.Parent.Resize -= InputFrameParentChanged;
                control.Parent.Resize += InputFrameParentChanged;
                control.Parent.Layout -= InputFrameParentLayoutChanged;
                control.Parent.Layout += InputFrameParentLayoutChanged;
                control.Parent.ControlAdded -= ControlAdded;
                control.Parent.ControlAdded += ControlAdded;
                control.Parent.Disposed -= InputFrameParentDisposed;
                control.Parent.Disposed += InputFrameParentDisposed;
            }

            frame.Update();
            EnsureFieldRowCanShowInput(control);
        }

        private static void RemoveVisibleInputFrame(Control control)
        {
            if (control == null)
                return;

            InputFrame frame;
            if (!InputFrames.TryGetValue(control, out frame) || frame == null)
                return;

            frame.Dispose();
            InputFrames.Remove(control);
        }

        private static void InputFrameControlChanged(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null || control.IsDisposed || control.Parent == null)
            {
                RemoveVisibleInputFrame(control);
                return;
            }

            ConfigureInput(control);
            AttachVisibleInputFrame(control);
        }

        private static void InputFrameParentChanged(object sender, EventArgs e)
        {
            RefreshFramesForParent(sender as Control);
        }

        private static void InputFrameParentLayoutChanged(object sender, LayoutEventArgs e)
        {
            RefreshFramesForParent(sender as Control);
        }

        private static void InputFrameParentDisposed(object sender, EventArgs e)
        {
            Control parent = sender as Control;
            if (parent == null)
                return;

            foreach (Control input in InputFrames.Keys.ToList())
            {
                InputFrame frame = InputFrames[input];
                if (frame != null && frame.Parent == parent)
                    RemoveVisibleInputFrame(input);
            }
        }

        private static void RefreshFramesForParent(Control parent)
        {
            if (parent == null || parent.IsDisposed)
                return;

            foreach (Control input in InputFrames.Keys.ToList())
            {
                if (input != null && !input.IsDisposed && input.Parent == parent)
                    InputFrames[input].Update();
            }
        }

        private static void EnsureFieldRowCanShowInput(Control control)
        {
            if (control == null || control.Parent == null || control.Parent is Form || control.Parent is UserControl || control.Parent is TabPage)
                return;

            Control parent = control.Parent;
            bool hasLabel = parent.Controls.OfType<Label>().Any(label => !string.IsNullOrWhiteSpace(label.Text));
            if (!hasLabel || parent.Height <= 0 || parent.Height >= 54)
                return;

            bool fieldRow = parent.Dock == DockStyle.Top || parent.Dock == DockStyle.Bottom || parent.Height <= 48;
            if (fieldRow)
                parent.Height = 54;
        }

        private static void AttachParentFocusPainter(Control parent)
        {
            if (parent == null || parent.IsDisposed || FocusFrameParents.Contains(parent))
                return;

            FocusFrameParents.Add(parent);
            parent.Paint -= ParentPaintInputFrames;
            parent.Paint += ParentPaintInputFrames;
            parent.ControlAdded -= ControlAdded;
            parent.ControlAdded += ControlAdded;
            parent.Disposed += (s, e) => FocusFrameParents.Remove(parent);
            parent.Invalidate();
        }

        private static void ParentPaintInputFrames(object sender, PaintEventArgs e)
        {
            Control parent = sender as Control;
            if (parent == null || parent.IsDisposed)
                return;

            foreach (Control child in parent.Controls)
            {
                if (!child.Visible || !IsEditableInput(child) || FindInputHost(child) != null)
                    continue;

                bool focused = ContainsFocus(child);
                bool disabled = !child.Enabled || IsReadOnlyInput(child);
                Color border = focused ? DS.FocusBlue : DS.InputBorder;
                Color fill = disabled ? DS.InputDisabledBack : Color.White;
                Rectangle rect = child.Bounds;
                if (rect.Width <= 2 || rect.Height <= 2)
                    continue;

                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = DS.RoundedRect(rect, 7))
                using (SolidBrush brush = new SolidBrush(fill))
                using (Pen pen = new Pen(border, focused ? 2f : 1f))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private static void InputFocusChanged(object sender, EventArgs e)
        {
            InvalidateInputFrame(sender as Control);
        }

        private static void InputStateChanged(object sender, EventArgs e)
        {
            Control control = sender as Control;
            ConfigureInput(control);
            InvalidateInputFrame(control);
        }

        private static void InvalidateInputFrame(Control control)
        {
            if (control == null)
                return;

            Control host = FindInputHost(control);
            if (host != null)
            {
                host.Invalidate();
                return;
            }

            if (control.Parent != null)
                control.Parent.Invalidate(GetInvalidationBounds(control));

            InputFrame frame;
            if (InputFrames.TryGetValue(control, out frame) && frame != null)
                frame.Update();
        }

        private static Rectangle GetInvalidationBounds(Control control)
        {
            Rectangle bounds = control.Bounds;
            bounds.Inflate(3, 3);
            return bounds;
        }

        private static Control FindInputHost(Control input)
        {
            Control parent = input == null ? null : input.Parent;
            while (parent != null)
            {
                if (IsInputHost(parent))
                    return parent;

                if (parent is Form || parent is UserControl || parent is TabPage)
                    return null;

                parent = parent.Parent;
            }

            return null;
        }

        // Caches the (expensive, recursive) IsInputHost verdict for a control, keyed alongside the
        // direct child count at the time it was computed. ApplyToTree revisits the same controls
        // many times (once per ancestor's tree-walk plus the top-level pass), and each call used to
        // re-run two full subtree scans (CountDescendants) regardless of whether anything changed.
        // On forms with many inputs (e.g. AddAMCForm's 13-row grid) that quadratic re-scanning was
        // the dominant cost of opening the form. The child count is a safe invalidation key here
        // because the only structural mutations this service performs are adding the 4 frame panels
        // directly under an input's parent.
        private static readonly Dictionary<Control, KeyValuePair<int, bool>> InputHostCache = new Dictionary<Control, KeyValuePair<int, bool>>();

        private static bool IsInputHost(Control control)
        {
            if (control == null || control is Form || control is UserControl || control is TabPage)
                return false;

            if (ShouldSkipInputHost(control))
                return false;

            if (IsInsideCustomInputShell(control))
                return false;

            if (control is DataGridView || control is FlowLayoutPanel || control is TableLayoutPanel || control is SplitContainer || control is SplitterPanel)
                return false;

            int childCount = control.Controls.Count;
            KeyValuePair<int, bool> cached;
            if (InputHostCache.TryGetValue(control, out cached) && cached.Key == childCount)
                return cached.Value;

            int editableInputs = CountDescendants(control, IsEditableInput);
            bool result;
            if (editableInputs == 0)
            {
                result = false;
            }
            else
            {
                int blockingControls = CountDescendants(control, c => c is DataGridView || c is ListView || c is TreeView);
                if (blockingControls > 0)
                {
                    result = false;
                }
                else
                {
                    string name = ((control.Name ?? string.Empty) + " " + Convert.ToString(control.Tag)).ToUpperInvariant();
                    bool namedLikeField = ContainsAny(name, "INPUT", "FIELD", "SEARCH", "FILTER", "WRAP", "HOST", "BOX", "EDITOR");
                    bool compact = control.Height > 0 && control.Height <= 120;
                    bool fieldLabel = control.Controls.OfType<Label>().Any(label => !string.IsNullOrWhiteSpace(label.Text) && label.Text.Length <= 90);
                    bool inputDominant = editableInputs >= CountDescendants(control, c => c is Button || c is PictureBox);
                    bool whiteSurface = control.BackColor == Color.White || control.BackColor == DS.White || control.BackColor == DS.BgInput || control.BackColor == SystemColors.Window;

                    result = whiteSurface && inputDominant && (compact || namedLikeField || fieldLabel);
                }
            }

            if (!InputHostCache.ContainsKey(control))
            {
                control.Disposed += (s, e) => InputHostCache.Remove(control);
            }
            InputHostCache[control] = new KeyValuePair<int, bool>(childCount, result);
            return result;
        }

        private static bool ShouldSkipInputHost(Control control)
        {
            if (control == null)
                return false;

            string metadata = ((control.Name ?? string.Empty) + " " + (control.Tag == null ? string.Empty : control.Tag.ToString())).ToUpperInvariant();
            return ContainsAny(metadata, "NO_INPUT_HOST", "NO_INPUT_OUTLINE_HOST");
        }

        private static void AttachHostOutline(Control host)
        {
            if (host == null || OutlinedHosts.Contains(host))
                return;

            OutlinedHosts.Add(host);
            host.Paint += HostPaint;
            host.Resize += HostInvalidate;
            host.GotFocus += HostInvalidate;
            host.LostFocus += HostInvalidate;
            foreach (Control child in host.Controls)
            {
                child.GotFocus += HostChildFocusChanged;
                child.LostFocus += HostChildFocusChanged;
            }
            host.Invalidate();
        }

        private static void HostPaint(object sender, PaintEventArgs e)
        {
            Control host = sender as Control;
            if (host == null || host.Width <= 2 || host.Height <= 2)
                return;

            bool focused = ContainsFocus(host);
            Color border = focused ? DS.FocusBlue : DS.InputBorder;
            Rectangle rect = new Rectangle(0, 0, host.Width - 1, host.Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = DS.RoundedRect(rect, 7))
            using (Pen pen = new Pen(border, 1))
                e.Graphics.DrawPath(pen, path);
        }

        private static bool ContainsFocus(Control control)
        {
            if (control == null)
                return false;

            if (control.Focused)
                return true;

            foreach (Control child in control.Controls)
            {
                if (ContainsFocus(child))
                    return true;
            }

            return false;
        }

        private static void HostInvalidate(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control != null)
                control.Invalidate();
        }

        private static void HostChildFocusChanged(object sender, EventArgs e)
        {
            Control child = sender as Control;
            if (child != null && child.Parent != null)
                child.Parent.Invalidate();
        }

        private static bool IsEditableInput(Control control)
        {
            return control is TextBoxBase ||
                   control is ComboBox ||
                   control is DateTimePicker ||
                   control is NumericUpDown;
        }

        private static void ApplyToOpenForms()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form == null || form.IsDisposed)
                    continue;

                if (!WatchedForms.Contains(form))
                {
                    WatchedForms.Add(form);
                    form.Disposed += (s, e) => WatchedForms.Remove(form);
                    form.ControlAdded += OpenFormControlAdded;
                    form.Shown += (s, e) => ApplyToTree(form);
                    form.Activated += (s, e) => ApplyToTree(form);
                }

                ApplyToTree(form);
            }
        }

        private static void OpenFormControlAdded(object sender, ControlEventArgs e)
        {
            ApplyToTree(e.Control);
        }

        private static bool IsReadOnlyInput(Control control)
        {
            TextBoxBase text = control as TextBoxBase;
            if (text != null)
                return text.ReadOnly;

            return false;
        }

        private static int CountDescendants(Control control, Func<Control, bool> predicate)
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

        /// <summary>Returns true when an input has a parent that can paint the shared outline.</summary>
        private static bool HasInputOutlineHostCandidate(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            return parent != null && (IsInputHost(parent) || IsOutlinedInputHost(parent));
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

        private static Color ResolveReadableBackColor(Control control)
        {
            Control current = control;
            while (current != null)
            {
                if (current.BackColor != Color.Empty && current.BackColor != Color.Transparent)
                    return current.BackColor;
                current = current.Parent;
            }

            return Color.White;
        }

        private sealed class InputFrame : IDisposable
        {
            private readonly Control _input;
            private readonly Panel _top;
            private readonly Panel _right;
            private readonly Panel _bottom;
            private readonly Panel _left;

            private InputFrame(Control input, Panel top, Panel right, Panel bottom, Panel left)
            {
                _input = input;
                _top = top;
                _right = right;
                _bottom = bottom;
                _left = left;
            }

            public Control Parent
            {
                get { return _top == null ? null : _top.Parent; }
            }

            public static InputFrame Create(Control input)
            {
                Control parent = input.Parent;
                Panel top = MakeEdge("Top");
                Panel right = MakeEdge("Right");
                Panel bottom = MakeEdge("Bottom");
                Panel left = MakeEdge("Left");
                parent.Controls.AddRange(new Control[] { top, right, bottom, left });
                var frame = new InputFrame(input, top, right, bottom, left);
                frame.Update();
                return frame;
            }

            public void Update()
            {
                if (_input == null || _input.IsDisposed || _input.Parent == null)
                {
                    Dispose();
                    return;
                }

                bool visible = _input.Visible && _input.Width > 2 && _input.Height > 2;
                Color color = ResolveBorderColor(_input);
                int thickness = ContainsFocus(_input) ? 2 : 1;
                Rectangle bounds = _input.Bounds;

                bool topChanged = SetEdge(_top, new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness), color, visible);
                bool bottomChanged = SetEdge(_bottom, new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness), color, visible);
                bool leftChanged = SetEdge(_left, new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height), color, visible);
                bool rightChanged = SetEdge(_right, new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height), color, visible);

                // Only re-order Z-order (an expensive Win32 SetWindowPos call) when something
                // actually changed. Without this, every redundant ApplyToTree pass over the
                // same already-up-to-date frame still issued 4 BringToFront calls per input,
                // which compounds badly on forms with many inputs (e.g. AddAMCForm).
                if (topChanged)
                    _top.BringToFront();
                if (rightChanged)
                    _right.BringToFront();
                if (bottomChanged)
                    _bottom.BringToFront();
                if (leftChanged)
                    _left.BringToFront();
            }

            public void Dispose()
            {
                DisposeEdge(_top);
                DisposeEdge(_right);
                DisposeEdge(_bottom);
                DisposeEdge(_left);
            }

            private static Panel MakeEdge(string suffix)
            {
                return new Panel
                {
                    Name = FrameNamePrefix + suffix,
                    Tag = "INPUT_OUTLINE_FRAME",
                    BackColor = DS.InputBorder,
                    Enabled = false,
                    Visible = false,
                    Margin = Padding.Empty
                };
            }

            /// <summary>Applies the edge's bounds/color/visibility, returning true only if something actually changed.</summary>
            private static bool SetEdge(Panel edge, Rectangle bounds, Color color, bool visible)
            {
                if (edge == null || edge.IsDisposed)
                    return false;

                bool changed = edge.Bounds != bounds || edge.BackColor != color || edge.Visible != visible;
                if (!changed)
                    return false;

                edge.Bounds = bounds;
                edge.BackColor = color;
                edge.Visible = visible;
                return true;
            }

            private static void DisposeEdge(Control edge)
            {
                if (edge == null || edge.IsDisposed)
                    return;

                edge.Dispose();
            }

            private static Color ResolveBorderColor(Control input)
            {
                if (input == null || !input.Enabled || IsReadOnlyInput(input))
                    return DS.Slate200;

                return ContainsFocus(input) ? DS.FocusBlue : DS.InputBorder;
            }
        }
    }
}
