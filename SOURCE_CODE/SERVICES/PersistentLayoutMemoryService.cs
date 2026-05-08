using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class PersistentLayoutMemoryService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "LayoutMemory");

        public void SaveWindow(Form form)
        {
            if (form == null || form.IsDisposed)
                return;

            Rectangle bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            var state = LoadState<WindowLayoutState>("window:" + form.GetType().FullName) ?? new WindowLayoutState();
            state.X = bounds.X;
            state.Y = bounds.Y;
            state.Width = bounds.Width;
            state.Height = bounds.Height;
            state.WindowState = form.WindowState == FormWindowState.Minimized ? FormWindowState.Normal.ToString() : form.WindowState.ToString();
            SaveState("window:" + form.GetType().FullName, state);
        }

        public void ApplyWindow(Form form)
        {
            if (form == null)
                return;

            WindowLayoutState state = LoadState<WindowLayoutState>("window:" + form.GetType().FullName);
            if (state == null || state.Width < 800 || state.Height < 500)
                return;

            Rectangle target = new Rectangle(state.X, state.Y, state.Width, state.Height);
            if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(target)))
                return;

            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = target;
            FormWindowState parsed;
            if (Enum.TryParse(state.WindowState, out parsed) && parsed != FormWindowState.Minimized)
                form.WindowState = parsed;
        }

        public void SavePage(Control root, string pageKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(pageKey))
                return;

            var state = new PageLayoutState();
            CaptureControls(root, state);
            if (state.Filters.Count == 0 && state.Splitters.Count == 0 && state.Grids.Count == 0)
                return;

            SaveState("page:" + pageKey, state);
        }

        public void ApplyPage(Control root, string pageKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(pageKey))
                return;

            PageLayoutState state = LoadState<PageLayoutState>("page:" + pageKey);
            if (state == null)
                return;

            ApplyControls(root, state);
        }

        public void SetShellValue(string key, string value)
        {
            ShellLayoutState state = LoadState<ShellLayoutState>("shell") ?? new ShellLayoutState();
            state.Values[key ?? string.Empty] = value ?? string.Empty;
            SaveState("shell", state);
        }

        public string GetShellValue(string key, string fallback = "")
        {
            ShellLayoutState state = LoadState<ShellLayoutState>("shell");
            if (state == null || key == null)
                return fallback;
            string value;
            return state.Values.TryGetValue(key, out value) ? value : fallback;
        }

        private void CaptureControls(Control root, PageLayoutState state)
        {
            foreach (Control child in root.Controls)
            {
                if (!string.IsNullOrWhiteSpace(child.Name))
                {
                    if (IsFilterControl(child))
                        state.Filters[child.Name] = GetControlValue(child);
                    else if (child is SplitContainer split)
                        state.Splitters[child.Name] = split.SplitterDistance;
                    else if (child is DataGridView grid)
                        state.Grids[child.Name] = grid.Columns
                            .Cast<DataGridViewColumn>()
                            .Where(col => !string.IsNullOrWhiteSpace(col.Name))
                            .ToDictionary(col => col.Name, col => col.Width, StringComparer.OrdinalIgnoreCase);
                }

                CaptureControls(child, state);
            }
        }

        private void ApplyControls(Control root, PageLayoutState state)
        {
            foreach (Control child in root.Controls)
            {
                if (!string.IsNullOrWhiteSpace(child.Name))
                {
                    string value;
                    if (IsFilterControl(child) && state.Filters.TryGetValue(child.Name, out value))
                        SetControlValue(child, value);
                    else if (child is SplitContainer split && state.Splitters.TryGetValue(child.Name, out int distance))
                    {
                        int min = split.Panel1MinSize;
                        int max = split.Orientation == Orientation.Vertical ? split.Width - split.Panel2MinSize : split.Height - split.Panel2MinSize;
                        if (distance >= min && distance <= max)
                            split.SplitterDistance = distance;
                    }
                    else if (child is DataGridView grid && state.Grids.TryGetValue(child.Name, out Dictionary<string, int> widths))
                    {
                        foreach (DataGridViewColumn col in grid.Columns)
                        {
                            int width;
                            if (!string.IsNullOrWhiteSpace(col.Name) && widths.TryGetValue(col.Name, out width))
                                col.Width = Math.Max(col.MinimumWidth, width);
                        }
                    }
                }

                ApplyControls(child, state);
            }
        }

        private static bool IsFilterControl(Control control)
        {
            if (!(control is TextBoxBase || control is ComboBox || control is CheckBox || control is DateTimePicker || control is NumericUpDown))
                return false;

            string key = ((control.Name ?? string.Empty) + " " + (control.Tag ?? string.Empty)).ToLowerInvariant();
            return key.Contains("filter")
                || key.Contains("search")
                || key.Contains("status")
                || key.Contains("category")
                || key.Contains("sort")
                || key.Contains("range")
                || key.Contains("from")
                || key.Contains("to")
                || key.Contains("date")
                || key.Contains("view");
        }

        private static string GetControlValue(Control control)
        {
            if (control is TextBoxBase text) return text.Text;
            if (control is ComboBox combo) return combo.Text;
            if (control is CheckBox check) return check.Checked ? "1" : "0";
            if (control is DateTimePicker picker) return picker.Value.ToString("O");
            if (control is NumericUpDown number) return number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static void SetControlValue(Control control, string value)
        {
            if (control is TextBoxBase text) text.Text = value ?? string.Empty;
            else if (control is ComboBox combo) combo.Text = value ?? string.Empty;
            else if (control is CheckBox check) check.Checked = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
            else if (control is DateTimePicker picker && DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime date))
                picker.Value = date < picker.MinDate ? picker.MinDate : (date > picker.MaxDate ? picker.MaxDate : date);
            else if (control is NumericUpDown number && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                number.Value = Math.Min(number.Maximum, Math.Max(number.Minimum, parsed));
        }

        private T LoadState<T>(string key) where T : class
        {
            try
            {
                string path = GetPath(key);
                return File.Exists(path) ? _serializer.Deserialize<T>(File.ReadAllText(path)) : null;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PersistentLayoutMemoryService.LoadState(" + key + ")", ex);
                return null;
            }
        }

        private void SaveState<T>(string key, T state)
        {
            try
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(GetPath(key), _serializer.Serialize(state));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PersistentLayoutMemoryService.SaveState(" + key + ")", ex);
            }
        }

        private string GetPath(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return Path.Combine(Root, key + ".json");
        }

        public sealed class WindowLayoutState
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string WindowState { get; set; }
        }

        public sealed class ShellLayoutState
        {
            public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class PageLayoutState
        {
            public Dictionary<string, string> Filters { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> Splitters { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, Dictionary<string, int>> Grids { get; set; } = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
