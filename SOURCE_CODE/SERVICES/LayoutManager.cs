using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DashboardCardLayout
    {
        public string CardKey { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Order { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string ParentKey { get; set; }
        public bool Locked { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public sealed class DashboardPageLayout
    {
        public string PageKey { get; set; }
        public List<DashboardCardLayout> Cards { get; set; } = new List<DashboardCardLayout>();
    }

    public static class LayoutManager
    {
        private static readonly object Sync = new object();

        public static string LayoutRoot
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "layouts");
            }
        }

        /// <summary>Loads all saved card layout records for a page.</summary>
        public static Dictionary<string, DashboardCardLayout> LoadPage(string pageKey)
        {
            lock (Sync)
            {
                try
                {
                    string path = GetPath(pageKey);
                    if (!File.Exists(path))
                        return new Dictionary<string, DashboardCardLayout>(StringComparer.OrdinalIgnoreCase);

                    DashboardPageLayout page = ReadPage(path);
                    return (page == null || page.Cards == null)
                        ? new Dictionary<string, DashboardCardLayout>(StringComparer.OrdinalIgnoreCase)
                        : page.Cards
                            .Where(card => card != null && !string.IsNullOrWhiteSpace(card.CardKey))
                            .GroupBy(card => card.CardKey, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("LayoutManager.LoadPage(" + pageKey + ")", ex);
                    return new Dictionary<string, DashboardCardLayout>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>Saves or updates one card layout record in the page JSON file.</summary>
        public static void SaveCard(string pageKey, DashboardCardLayout layout)
        {
            if (string.IsNullOrWhiteSpace(pageKey) || layout == null || string.IsNullOrWhiteSpace(layout.CardKey))
                return;

            lock (Sync)
            {
                try
                {
                    Dictionary<string, DashboardCardLayout> cards = LoadPage(pageKey);
                    layout.SavedAt = DateTime.Now;
                    cards[layout.CardKey] = layout;
                    SavePageCore(pageKey, cards.Values.OrderBy(card => card.Order).ThenBy(card => card.Y).ThenBy(card => card.X).ToList());
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("LayoutManager.SaveCard(" + pageKey + "/" + layout.CardKey + ")", ex);
                }
            }
        }

        /// <summary>Deletes the saved layout JSON file for a page.</summary>
        public static void ResetPage(string pageKey)
        {
            lock (Sync)
            {
                try
                {
                    string path = GetPath(pageKey);
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("LayoutManager.ResetPage(" + pageKey + ")", ex);
                }
            }
        }

        /// <summary>Returns the JSON file path used for one page layout.</summary>
        public static string GetPath(string pageKey)
        {
            string key = CleanFileName(string.IsNullOrWhiteSpace(pageKey) ? "Screen" : pageKey.Trim());
            return Path.Combine(LayoutRoot, key + "Layout.json");
        }

        private static void SavePageCore(string pageKey, List<DashboardCardLayout> cards)
        {
            Directory.CreateDirectory(LayoutRoot);
            var page = new DashboardPageLayout
            {
                PageKey = pageKey,
                Cards = cards ?? new List<DashboardCardLayout>()
            };
            WritePage(GetPath(pageKey), page);
        }

        private static string CleanFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(" ", string.Empty);
        }

        private static DashboardPageLayout ReadPage(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var serializer = new DataContractJsonSerializer(typeof(DashboardPageLayout));
                return serializer.ReadObject(stream) as DashboardPageLayout;
            }
        }

        private static void WritePage(string path, DashboardPageLayout page)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(DashboardPageLayout));
                serializer.WriteObject(stream, page);
                File.WriteAllText(path, Encoding.UTF8.GetString(stream.ToArray()));
            }
        }
    }
}
