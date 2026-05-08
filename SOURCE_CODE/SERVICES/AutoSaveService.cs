using System;
using System.Collections.Generic;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services.Recovery;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class AutoSaveService
    {
        private readonly FormStateRecoveryService _recovery = new FormStateRecoveryService();
        private readonly Dictionary<Control, Timer> _timers = new Dictionary<Control, Timer>();
        private readonly HashSet<Control> _attached = new HashSet<Control>();

        public void Attach(Control root, string formKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(formKey) || _attached.Contains(root))
                return;

            _attached.Add(root);
            var timer = new Timer { Interval = 1200 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Save(root, formKey);
            };
            _timers[root] = timer;

            AttachHandlers(root, () =>
            {
                if (root.IsDisposed)
                    return;
                timer.Stop();
                timer.Start();
            });

            root.Disposed += (s, e) =>
            {
                Save(root, formKey);
                timer.Dispose();
                _timers.Remove(root);
                _attached.Remove(root);
            };
        }

        public bool Restore(Control root, string formKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(formKey))
                return false;

            AutoSaveSnapshot snapshot;
            if (!_recovery.TryLoadState(formKey, out snapshot) || snapshot == null || snapshot.Values == null || snapshot.Values.Count == 0)
                return false;

            ApplyValues(root, snapshot.Values);
            return true;
        }

        public bool HasDraft(string formKey, out DateTime savedAt)
        {
            savedAt = DateTime.MinValue;
            AutoSaveSnapshot snapshot;
            if (!_recovery.TryLoadState(formKey, out snapshot) || snapshot == null || snapshot.Values == null || snapshot.Values.Count == 0)
                return false;

            savedAt = snapshot.SavedAt;
            return true;
        }

        public void Clear(string formKey)
        {
            _recovery.ClearState(formKey);
        }

        private void Save(Control root, string formKey)
        {
            try
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                CaptureValues(root, values);
                if (values.Count == 0 || !HasAnyMeaningfulValue(values))
                    return;

                _recovery.SaveState(formKey, new AutoSaveSnapshot
                {
                    SavedAt = DateTime.Now,
                    Values = values
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AutoSaveService.Save(" + formKey + ")", ex);
            }
        }

        private static void AttachHandlers(Control control, Action changed)
        {
            foreach (Control child in control.Controls)
            {
                if (IsDraftControl(child))
                {
                    TextBoxBase text = child as TextBoxBase;
                    if (text != null)
                        text.TextChanged += (s, e) => changed();
                    else if (child is ComboBox combo)
                        combo.SelectedIndexChanged += (s, e) => changed();
                    else if (child is CheckBox check)
                        check.CheckedChanged += (s, e) => changed();
                    else if (child is DateTimePicker picker)
                        picker.ValueChanged += (s, e) => changed();
                    else if (child is NumericUpDown number)
                        number.ValueChanged += (s, e) => changed();
                }

                AttachHandlers(child, changed);
            }
        }

        private static void CaptureValues(Control control, Dictionary<string, string> values)
        {
            foreach (Control child in control.Controls)
            {
                if (IsDraftControl(child) && !string.IsNullOrWhiteSpace(child.Name))
                {
                    if (child is TextBoxBase text)
                        values[child.Name] = text.Text;
                    else if (child is ComboBox combo)
                        values[child.Name] = combo.Text;
                    else if (child is CheckBox check)
                        values[child.Name] = check.Checked ? "1" : "0";
                    else if (child is DateTimePicker picker)
                        values[child.Name] = picker.Value.ToString("O");
                    else if (child is NumericUpDown number)
                        values[child.Name] = number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                CaptureValues(child, values);
            }
        }

        private static void ApplyValues(Control control, Dictionary<string, string> values)
        {
            foreach (Control child in control.Controls)
            {
                string value;
                if (IsDraftControl(child) && !string.IsNullOrWhiteSpace(child.Name) && values.TryGetValue(child.Name, out value))
                {
                    if (child is TextBoxBase text)
                        text.Text = value ?? string.Empty;
                    else if (child is ComboBox combo)
                        combo.Text = value ?? string.Empty;
                    else if (child is CheckBox check)
                        check.Checked = string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
                    else if (child is DateTimePicker picker && DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime date))
                        picker.Value = date < picker.MinDate ? picker.MinDate : (date > picker.MaxDate ? picker.MaxDate : date);
                    else if (child is NumericUpDown number && decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                        number.Value = Math.Min(number.Maximum, Math.Max(number.Minimum, parsed));
                }

                ApplyValues(child, values);
            }
        }

        private static bool IsDraftControl(Control control)
        {
            if (control == null || string.IsNullOrWhiteSpace(control.Name))
                return false;
            return control is TextBoxBase || control is ComboBox || control is CheckBox || control is DateTimePicker || control is NumericUpDown;
        }

        private static bool HasAnyMeaningfulValue(Dictionary<string, string> values)
        {
            foreach (string value in values.Values)
            {
                if (!string.IsNullOrWhiteSpace(value) && value != "0")
                    return true;
            }

            return false;
        }

        public sealed class AutoSaveSnapshot
        {
            public DateTime SavedAt { get; set; }
            public Dictionary<string, string> Values { get; set; }
        }
    }
}
