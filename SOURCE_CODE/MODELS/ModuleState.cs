using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HVAC_Pro_Desktop.Models
{
    [DataContract]
    public sealed class ModuleState
    {
        [DataMember(Order = 1)]
        public string PageKey { get; set; }

        [DataMember(Order = 2)]
        public string FilterText { get; set; }

        [DataMember(Order = 3)]
        public int? SelectedRowId { get; set; }

        [DataMember(Order = 4)]
        public int ScrollPosition { get; set; }

        [DataMember(Order = 5)]
        public string ActiveTab { get; set; }

        [DataMember(Order = 6)]
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string GetValue(string key, string defaultValue = "")
        {
            if (string.IsNullOrWhiteSpace(key) || Values == null)
                return defaultValue;

            string value;
            return Values.TryGetValue(key, out value) ? value : defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            int value;
            return int.TryParse(GetValue(key), out value) ? value : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            bool value;
            return bool.TryParse(GetValue(key), out value) ? value : defaultValue;
        }

        public void SetValue(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (Values == null)
                Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Values[key] = value == null ? string.Empty : Convert.ToString(value);
        }
    }
}
