using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.Services
{
    public static class LanguageManager
    {
        public const string English = "English";
        public const string Marathi = "मराठी";
        public const string Hindi = "हिन्दी";

        private static readonly System.Resources.ResourceManager Resources = new System.Resources.ResourceManager(
            "HVAC_Pro_Desktop.Resources.Localization.Strings", typeof(LanguageManager).Assembly) { IgnoreCase = true };
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, TextState> OriginalText =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, TextState>();
        private static readonly Lazy<Dictionary<string, string>> NeutralKeys = new Lazy<Dictionary<string, string>>(BuildNeutralKeys);
        private sealed class TextState
        {
            public string Source;
            public string Rendered;
            public bool Hooked;
            public bool Updating;
            public bool IsDataText;
            public readonly object AccessibleNameKey = new object();
            public readonly object AccessibleDescriptionKey = new object();
            public readonly object ToolTipKey = new object();
        }
        public static event EventHandler LanguageChanged;
        public static string CurrentLanguage { get; private set; } = English;

        /// <summary>Sets the active UI language and notifies open screens.</summary>
        public static void SetLanguage(string language)
        {
            SetLanguage(language, true);
        }

        /// <summary>Sets the active UI language with optional notification.</summary>
        public static void SetLanguage(string language, bool raiseEvent)
        {
            string normalized = Normalize(language);
            var culture = System.Globalization.CultureInfo.GetCultureInfo(
                normalized == Marathi ? "mr-IN" : normalized == Hindi ? "hi-IN" : "en-IN");
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            if (string.Equals(CurrentLanguage, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            CurrentLanguage = normalized;
            ApplyToOpenForms();
            if (raiseEvent)
                LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Returns the translated text for a key, falling back to English.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var culture = System.Globalization.CultureInfo.GetCultureInfo(
                CurrentLanguage == Marathi ? "mr-IN" : CurrentLanguage == Hindi ? "hi-IN" : "en-IN");
            return Resources.GetString(key, culture) ?? key;
        }

        // Translate complete validation lines, retaining bullets, line endings, and unknown details.
        // Never replace words inside interpolated business data or technical exception messages.
        internal static string GetMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || CurrentLanguage == English) return message;
            return System.Text.RegularExpressions.Regex.Replace(message, @"(?m)^(?<prefix> *- +)?(?<body>[^\r\n]+)",
                match => match.Groups["prefix"].Value + Get(match.Groups["body"].Value));
        }

        internal static string Format(string key, params object[] values)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), values);
        }

        internal static T DataText<T>(T control) where T : Control
        {
            OriginalText.GetValue(control, _ => new TextState()).IsDataText = true;
            return control;
        }

        internal static T DataTextItem<T>(T item) where T : ToolStripItem
        {
            OriginalText.GetValue(item, _ => new TextState()).IsDataText = true;
            return item;
        }

        internal static T Caption<T>(T control) where T : Control
        {
            SetCaption(control, control.Text);
            return control;
        }

        internal static T CaptionItem<T>(T item) where T : ToolStripItem
        {
            SetCaption(item, item.Text);
            return item;
        }

        internal static void SetCaption(Control control, string source)
        {
            var state = OriginalText.GetValue(control, _ => new TextState());
            state.IsDataText = false;
            state.Source = source ?? string.Empty;
            state.Rendered = Get(state.Source);
            state.Updating = true;
            try { control.Text = state.Rendered; }
            finally { state.Updating = false; }
        }

        internal static void SetCaption(ToolStripItem item, string source)
        {
            var state = OriginalText.GetValue(item, _ => new TextState());
            state.IsDataText = false;
            state.Source = source ?? string.Empty;
            state.Rendered = Get(state.Source);
            state.Updating = true;
            try { item.Text = state.Rendered; }
            finally { state.Updating = false; }
        }

        internal static void SetDataText(Control control, string value)
        {
            OriginalText.GetValue(control, _ => new TextState()).IsDataText = true;
            control.Text = value;
        }

        internal static void SetDataText(ToolStripItem item, string value)
        {
            OriginalText.GetValue(item, _ => new TextState()).IsDataText = true;
            item.Text = value;
        }

        /// <summary>Returns true when the active language needs a Devanagari-capable font.</summary>
        public static bool IsDevanagariLanguage()
        {
            return string.Equals(CurrentLanguage, Marathi, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(CurrentLanguage, Hindi, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns the preferred UI font family for the active language.</summary>
        public static string GetUiFontFamily()
        {
            return IsDevanagariLanguage() ? "Nirmala UI" : "Segoe UI";
        }

        /// <summary>Applies translated text and the current language font to a control tree.</summary>
        public static void ApplyFont(Control parent)
        {
            if (parent == null)
                return;

            ApplyTextRecursive(parent);
            ApplyFontRecursive(parent, GetUiFontFamily());
        }

        /// <summary>Applies translated text and current language font to a newly added control tree.</summary>
        public static void ApplyControlTree(Control parent)
        {
            ApplyFont(parent);
        }

        /// <summary>Applies the active language to every currently open WinForms window.</summary>
        public static void ApplyToOpenForms()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form == null || form.IsDisposed)
                    continue;

                ApplyControlTree(form);
            }
        }

        private static void ApplyFontRecursive(Control control, string family)
        {
            if (control == null)
                return;

            // Change only the application's normal text face. Preserve symbol/icon fonts,
            // monospace technical details, and other explicitly chosen control fonts.
            if (control.Font != null)
            {
                string currentFamily = control.Font.FontFamily.Name;
                bool standardTextFace = string.Equals(currentFamily, "Segoe UI", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentFamily, "Nirmala UI", StringComparison.OrdinalIgnoreCase);
                if (standardTextFace && !string.Equals(currentFamily, family, StringComparison.OrdinalIgnoreCase))
                    control.Font = new Font(family, control.Font.Size, control.Font.Style);
            }

            foreach (Control child in control.Controls)
                ApplyFontRecursive(child, family);
        }

        private static void ApplyTextRecursive(Control control)
        {
            if (control == null)
                return;

            var state = OriginalText.GetValue(control, _ => new TextState());
            if (!state.Hooked)
            {
                state.Hooked = true;
                control.TextChanged += OnPresentationTextChanged;
                control.ControlAdded += OnLocalizedControlAdded;
                if (control is DataGridView localizedGrid)
                    localizedGrid.ColumnAdded += (sender, args) => TranslateGridHeaders((DataGridView)sender);
                if (control is ToolStrip localizedStrip)
                    localizedStrip.ItemAdded += (sender, args) => TranslateToolStripItem(args.Item);
            }
            if (!string.IsNullOrWhiteSpace(control.AccessibleName))
                control.AccessibleName = Get(OriginalFor(state.AccessibleNameKey, control.AccessibleName));
            if (!string.IsNullOrWhiteSpace(control.AccessibleDescription))
                control.AccessibleDescription = Get(OriginalFor(state.AccessibleDescriptionKey, control.AccessibleDescription));
            TranslateControlText(control);

            DataGridView grid = control as DataGridView;
            if (grid != null)
                TranslateGridHeaders(grid);

            TabControl tabs = control as TabControl;
            if (tabs != null)
                foreach (TabPage page in tabs.TabPages)
                    TranslateControlText(page);

            if (control.ContextMenuStrip != null)
                foreach (ToolStripItem item in control.ContextMenuStrip.Items)
                    TranslateToolStripItem(item);

            MenuStrip menu = control as MenuStrip;
            if (menu != null)
                foreach (ToolStripItem item in menu.Items)
                    TranslateToolStripItem(item);

            ToolStrip toolStrip = control as ToolStrip;
            if (toolStrip != null)
                foreach (ToolStripItem item in toolStrip.Items)
                    TranslateToolStripItem(item);

            // Combo/list items are business values, not presentation resources. Never mutate them.

            foreach (Control child in control.Controls)
                ApplyTextRecursive(child);
        }

        private static void TranslateControlText(Control control)
        {
            if (control == null || string.IsNullOrWhiteSpace(control.Text))
                return;

            if (control is HVAC_Pro_Desktop.UI.Controls.ModernTextBox || control is HVAC_Pro_Desktop.UI.Controls.ModernERPSearchBox || control is TextBoxBase || control is ComboBox || control is ListControl || control is UpDownBase || control is DateTimePicker || control is DataGridView)
                return;

            var state = OriginalText.GetValue(control, _ => new TextState());
            if (state.Updating || state.IsDataText) return;
            state.Updating = true;
            try
            {
                string original = OriginalFor(control, control.Text);
                string translated = Get(original);
                if (control.Text != translated) control.Text = translated;
            }
            finally { state.Updating = false; }
        }

        private static void OnPresentationTextChanged(object sender, EventArgs e)
        {
            TranslateControlText(sender as Control);
        }

        private static void OnLocalizedControlAdded(object sender, ControlEventArgs e)
        {
            ApplyControlTree(e.Control);
        }

        private static void TranslateGridHeaders(DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.HeaderText))
                    continue;

                string original = OriginalFor(column, column.HeaderText);
                column.HeaderText = Get(original);
            }
        }

        private static void TranslateToolStripItem(ToolStripItem item)
        {
            if (item == null) return;
            var state = OriginalText.GetValue(item, _ => new TextState());
            if (state.Updating) return;
            state.Updating = true;
            try
            {
                if (!state.Hooked)
                {
                    state.Hooked = true;
                    item.TextChanged += (sender, args) => TranslateToolStripItem((ToolStripItem)sender);
                    if (item is ToolStripDropDownItem dropDownItem)
                        dropDownItem.DropDownOpening += (sender, args) =>
                        {
                            foreach (ToolStripItem child in ((ToolStripDropDownItem)sender).DropDownItems)
                                TranslateToolStripItem(child);
                        };
                }
                // ToolStripTextBox and ToolStripComboBox host editable/business values.
                if (item is ToolStripControlHost host) ApplyControlTree(host.Control);
                else if (!state.IsDataText && !string.IsNullOrWhiteSpace(item.Text)) item.Text = Get(OriginalFor(item, item.Text));
                if (!string.IsNullOrWhiteSpace(item.ToolTipText)) item.ToolTipText = Get(OriginalFor(state.ToolTipKey, item.ToolTipText));
                if (!string.IsNullOrWhiteSpace(item.AccessibleName)) item.AccessibleName = Get(OriginalFor(state.AccessibleNameKey, item.AccessibleName));
                if (!string.IsNullOrWhiteSpace(item.AccessibleDescription)) item.AccessibleDescription = Get(OriginalFor(state.AccessibleDescriptionKey, item.AccessibleDescription));
                if (item is ToolStripDropDownItem dropDown)
                    foreach (ToolStripItem child in dropDown.DropDownItems) TranslateToolStripItem(child);
            }
            finally { state.Updating = false; }
        }

        // Action routing must use neutral presentation identifiers, never translated captions.
        internal static string GetSourceText(object key, string currentText)
        {
            if (key == null) return currentText ?? string.Empty;
            TextState state;
            if (OriginalText.TryGetValue(key, out state) && !state.IsDataText && state.Source != null && string.Equals(currentText, state.Rendered, StringComparison.Ordinal))
                return state.Source;
            return CanonicalText(currentText ?? string.Empty);
        }

        private static Dictionary<string, string> BuildNeutralKeys()
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cultureName in new[] { "mr-IN", "hi-IN" })
            {
                var set = Resources.GetResourceSet(System.Globalization.CultureInfo.GetCultureInfo(cultureName), true, false);
                if (set == null) continue;
                foreach (System.Collections.DictionaryEntry entry in set)
                {
                    string value = entry.Value as string;
                    if (string.IsNullOrEmpty(value)) continue;
                    string existing;
                    if (keys.TryGetValue(value, out existing) && !string.Equals(existing, (string)entry.Key, StringComparison.OrdinalIgnoreCase))
                        ambiguous.Add(value);
                    else keys[value] = (string)entry.Key;
                }
            }
            // A shared translation is not sufficient evidence to infer an action identifier.
            foreach (string value in ambiguous) keys.Remove(value);
            return keys;
        }

        private static string CanonicalText(string text)
        {
            if (Resources.GetString(text, System.Globalization.CultureInfo.InvariantCulture) != null) return text;
            string source;
            return NeutralKeys.Value.TryGetValue(text, out source) ? source : text;
        }

        private static string OriginalFor(object key, string currentText)
        {
            var state = OriginalText.GetValue(key, _ => new TextState());
            if (state.Source == null || !string.Equals(currentText, state.Rendered, StringComparison.Ordinal))
                state.Source = CanonicalText(currentText ?? string.Empty);
            state.Rendered = Get(state.Source);
            return state.Source;
        }

        private static string Normalize(string language)
        {
            string value = (language ?? string.Empty).Trim();
            if (string.Equals(value, Marathi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Marathi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "mr-IN", StringComparison.OrdinalIgnoreCase))
                return Marathi;

            if (string.Equals(value, Hindi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Hindi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "hi-IN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "हिंदी", StringComparison.OrdinalIgnoreCase))
                return Hindi;

            return English;
        }
    }
}
