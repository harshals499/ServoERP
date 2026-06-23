using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class MasterLookupService
    {
        private readonly MasterLookupRepository _repo = new MasterLookupRepository();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public List<MasterLookupCategory> GetCategories(bool includeInactive = false)
        {
            string key = includeInactive ? "masterlookup:categories:all" : "masterlookup:categories:active";
            return AppDataCache.GetOrCreate(key, CacheTtl, () => _repo.GetCategories(includeInactive) ?? new List<MasterLookupCategory>()).ToList();
        }

        public List<MasterLookupValue> GetValues(string categoryKey, bool includeInactive = false)
        {
            string normalized = NormalizeKey(categoryKey);
            if (string.IsNullOrWhiteSpace(normalized))
                return new List<MasterLookupValue>();

            string key = "masterlookup:values:" + normalized + ":" + (includeInactive ? "all" : "active");
            return AppDataCache.GetOrCreate(key, CacheTtl, () => _repo.GetValues(normalized, includeInactive) ?? new List<MasterLookupValue>()).ToList();
        }

        public List<string> GetDisplayValues(string categoryKey, params string[] fallbackValues)
        {
            List<string> values = GetValues(categoryKey)
                .Where(v => !string.IsNullOrWhiteSpace(v.DisplayText))
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.DisplayText)
                .Select(v => v.DisplayText.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (values.Count == 0 && fallbackValues != null)
                values.AddRange(fallbackValues.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));

            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void BindCombo(ComboBox combo, string categoryKey, IEnumerable<string> fallbackValues, string preferredValue = null)
        {
            if (combo == null)
                return;

            string previous = !string.IsNullOrWhiteSpace(preferredValue) ? preferredValue : combo.Text;
            combo.Items.Clear();
            combo.Items.AddRange(GetDisplayValues(categoryKey, (fallbackValues ?? new string[0]).ToArray()).Cast<object>().ToArray());

            if (!string.IsNullOrWhiteSpace(previous))
                SelectComboValue(combo, previous);

            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        public int SaveCategory(MasterLookupCategory category)
        {
            ValidateCategory(category);
            int id = _repo.SaveCategory(category);
            AppDataCache.RemovePrefix("masterlookup:");
            return id;
        }

        public int SaveValue(MasterLookupValue value)
        {
            ValidateValue(value);
            int id = _repo.SaveValue(value);
            AppDataCache.RemovePrefix("masterlookup:");
            return id;
        }

        public static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static void SelectComboValue(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(combo.Items[i]), value, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void ValidateCategory(MasterLookupCategory category)
        {
            if (category == null)
                throw new InvalidOperationException("Lookup category is required.");
            if (string.IsNullOrWhiteSpace(category.CategoryKey))
                throw new InvalidOperationException("Category key is required.");
            if (string.IsNullOrWhiteSpace(category.ModuleKey))
                throw new InvalidOperationException("Module is required.");
            if (string.IsNullOrWhiteSpace(category.DisplayName))
                throw new InvalidOperationException("Display name is required.");
        }

        private static void ValidateValue(MasterLookupValue value)
        {
            if (value == null)
                throw new InvalidOperationException("Lookup value is required.");
            if (value.CategoryId <= 0)
                throw new InvalidOperationException("Category is required.");
            if (string.IsNullOrWhiteSpace(value.DisplayText))
                throw new InvalidOperationException("Display text is required.");
            if (string.IsNullOrWhiteSpace(value.ValueCode))
                value.ValueCode = value.DisplayText.Trim();
        }
    }
}
