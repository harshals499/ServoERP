using System;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class SavedListView
    {
        public string SearchText { get; set; }
        public string StatusFilter { get; set; }
        public string TypeFilter { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public sealed class SavedViewService
    {
        private const string JobsDefaultViewKey = "SavedView.Jobs.Default";

        /// <summary>Saves the current Jobs dashboard filter view.</summary>
        public void SaveJobsDefaultView(string searchText, string statusFilter, string typeFilter)
        {
            string value = Escape(searchText) + "|" + Escape(statusFilter) + "|" + Escape(typeFilter) + "|" + DateTime.Now.ToString("yyyyMMddHHmmss");
            DbSettings.Set(JobsDefaultViewKey, value);
        }

        /// <summary>Loads the saved Jobs dashboard filter view.</summary>
        public SavedListView LoadJobsDefaultView()
        {
            string value = DbSettings.Get(JobsDefaultViewKey, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string[] parts = value.Split('|');
            if (parts.Length < 3)
                return null;

            DateTime savedAt;
            if (parts.Length < 4 || !DateTime.TryParseExact(parts[3], "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out savedAt))
                savedAt = DateTime.Now;

            return new SavedListView
            {
                SearchText = Unescape(parts[0]),
                StatusFilter = Unescape(parts[1]),
                TypeFilter = Unescape(parts[2]),
                SavedAt = savedAt
            };
        }

        /// <summary>Clears the saved Jobs dashboard filter view.</summary>
        public void ClearJobsDefaultView()
        {
            DbSettings.Set(JobsDefaultViewKey, string.Empty);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("%", "%25").Replace("|", "%7C");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty).Replace("%7C", "|").Replace("%25", "%");
        }
    }
}
