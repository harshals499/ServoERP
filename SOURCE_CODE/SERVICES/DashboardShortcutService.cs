using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.Services.Audit;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DashboardShortcutService
    {
        private static readonly object Sync = new object();
        private readonly AuditTrailService _audit = new AuditTrailService();

        /// <summary>Saves a dashboard shortcut or favorite for a ServoERP card.</summary>
        public string SaveShortcut(string title, string pageKey, string cardKey, string cardPath, string kind)
        {
            if (string.IsNullOrWhiteSpace(cardPath))
                throw new InvalidOperationException("Card shortcut path is required.");

            string safeKind = string.IsNullOrWhiteSpace(kind) ? "Shortcut" : kind.Trim();
            string safeTitle = string.IsNullOrWhiteSpace(title) ? "ServoERP card" : title.Trim();
            string line = Escape(safeKind) + "," +
                          Escape(safeTitle) + "," +
                          Escape(pageKey) + "," +
                          Escape(cardKey) + "," +
                          Escape(cardPath) + "," +
                          Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            lock (Sync)
            {
                Directory.CreateDirectory(GetRoot());
                string path = GetShortcutFilePath();
                List<string> lines = File.Exists(path)
                    ? File.ReadAllLines(path).ToList()
                    : new List<string> { "Kind,Title,PageKey,CardKey,CardPath,SavedAt" };

                string duplicateKey = "," + Escape(cardPath) + ",";
                if (!lines.Any(existing => existing.IndexOf(duplicateKey, StringComparison.OrdinalIgnoreCase) >= 0
                                           && existing.StartsWith(Escape(safeKind) + ",", StringComparison.OrdinalIgnoreCase)))
                {
                    lines.Add(line);
                    File.WriteAllLines(path, lines.ToArray());
                }

                _audit.Record("CREATE", "Dashboard", null, safeKind + " saved for " + safeTitle + " (" + cardPath + ")");
                return path;
            }
        }

        /// <summary>Removes a saved shortcut/favorite entry that matches the card path and kind.</summary>
        public bool RemoveShortcut(string cardPath, string kind)
        {
            if (string.IsNullOrWhiteSpace(cardPath))
                return false;

            string safeKind = string.IsNullOrWhiteSpace(kind) ? "Shortcut" : kind.Trim();

            lock (Sync)
            {
                string path = GetShortcutFilePath();
                if (!File.Exists(path))
                    return false;

                List<string> lines = File.ReadAllLines(path).ToList();
                if (lines.Count == 0)
                    return false;

                string duplicateKey = "," + Escape(cardPath) + ",";
                int removed = lines.RemoveAll(existing =>
                    existing.IndexOf(duplicateKey, StringComparison.OrdinalIgnoreCase) >= 0
                    && existing.StartsWith(Escape(safeKind) + ",", StringComparison.OrdinalIgnoreCase));

                if (removed == 0)
                    return false;

                File.WriteAllLines(path, lines.ToArray());
                _audit.Record("DELETE", "Dashboard", null, safeKind + " removed for " + cardPath);
                return true;
            }
        }

        /// <summary>Returns the local CSV file used for saved card shortcuts.</summary>
        public static string GetShortcutFilePath()
        {
            return Path.Combine(GetRoot(), "dashboard-shortcuts.csv");
        }

        private static string GetRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP");
        }

        private static string Escape(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
