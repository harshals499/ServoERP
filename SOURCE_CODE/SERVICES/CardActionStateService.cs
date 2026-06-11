using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Persisted state for one card's GlobalCardContextMenu actions (favorite, hidden, deleted, etc.).</summary>
    public sealed class CardActionState
    {
        public string CardKey { get; set; }
        public string Title { get; set; }
        public bool Favorite { get; set; }
        public bool Hidden { get; set; }
        public bool Deleted { get; set; }
        public bool HasShortcut { get; set; }
        public bool Locked { get; set; }
        public string ClipboardState { get; set; }
        public string LastAction { get; set; }
        public DateTime LastActionAt { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
    }

    public sealed class CardActionPageState
    {
        public string PageKey { get; set; }
        public List<CardActionState> Cards { get; set; } = new List<CardActionState>();
    }

    /// <summary>Per-page JSON persistence for GlobalCardContextMenu action state (favorites, hide/delete, clipboard, etc.).</summary>
    public static class CardActionStateService
    {
        private static readonly object Sync = new object();

        public static string Root
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "layouts"); }
        }

        /// <summary>Loads all saved card action state records for a page.</summary>
        public static Dictionary<string, CardActionState> LoadPage(string pageKey)
        {
            lock (Sync)
            {
                try
                {
                    string path = GetPath(pageKey);
                    if (!File.Exists(path))
                        return new Dictionary<string, CardActionState>(StringComparer.OrdinalIgnoreCase);

                    CardActionPageState page = ReadPage(path);
                    return (page == null || page.Cards == null)
                        ? new Dictionary<string, CardActionState>(StringComparer.OrdinalIgnoreCase)
                        : page.Cards
                            .Where(card => card != null && !string.IsNullOrWhiteSpace(card.CardKey))
                            .GroupBy(card => card.CardKey, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("CardActionStateService.LoadPage(" + pageKey + ")", ex);
                    return new Dictionary<string, CardActionState>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>Loads one card's persisted action state, or a fresh default record if none exists.</summary>
        public static CardActionState LoadCard(string pageKey, string cardKey, string title)
        {
            Dictionary<string, CardActionState> cards = LoadPage(pageKey);
            CardActionState state;
            if (cards.TryGetValue(cardKey, out state) && state != null)
                return state;

            return new CardActionState
            {
                CardKey = cardKey,
                Title = title,
                ClipboardState = string.Empty,
                LastAction = string.Empty
            };
        }

        /// <summary>Saves or updates one card's action state record in the page JSON file.</summary>
        public static void Save(string pageKey, CardActionState state)
        {
            if (string.IsNullOrWhiteSpace(pageKey) || state == null || string.IsNullOrWhiteSpace(state.CardKey))
                return;

            lock (Sync)
            {
                try
                {
                    Dictionary<string, CardActionState> cards = LoadPage(pageKey);
                    state.LastActionAt = DateTime.Now;
                    cards[state.CardKey] = state;
                    SavePageCore(pageKey, cards.Values.ToList());
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("CardActionStateService.Save(" + pageKey + "/" + state.CardKey + ")", ex);
                }
            }
        }

        /// <summary>Returns the JSON file path used for one page's card action state.</summary>
        public static string GetPath(string pageKey)
        {
            string key = CleanFileName(string.IsNullOrWhiteSpace(pageKey) ? "Screen" : pageKey.Trim());
            return Path.Combine(Root, key + "CardActions.json");
        }

        private static void SavePageCore(string pageKey, List<CardActionState> cards)
        {
            Directory.CreateDirectory(Root);
            var page = new CardActionPageState
            {
                PageKey = pageKey,
                Cards = cards ?? new List<CardActionState>()
            };
            WritePage(GetPath(pageKey), page);
        }

        private static string CleanFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Replace(" ", string.Empty);
        }

        private static CardActionPageState ReadPage(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var serializer = new DataContractJsonSerializer(typeof(CardActionPageState));
                return serializer.ReadObject(stream) as CardActionPageState;
            }
        }

        private static void WritePage(string path, CardActionPageState page)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(CardActionPageState));
                serializer.WriteObject(stream, page);
                File.WriteAllText(path, Encoding.UTF8.GetString(stream.ToArray()));
            }
        }
    }
}
