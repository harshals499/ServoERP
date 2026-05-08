using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.UI.Helpers;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class CardLayoutService
    {
        private static readonly object DefaultSync = new object();
        private static readonly Dictionary<string, CardDefaultSize> DefaultSizes = new Dictionary<string, CardDefaultSize>(StringComparer.OrdinalIgnoreCase);
        private const string LogPath = @"C:\HVAC_PRO_MSE\LOGS\card_layouts.log";

        public static int ResolveCurrentUserId()
        {
            return SessionManager.IsLoggedIn && SessionManager.CurrentUser != null
                ? SessionManager.CurrentUser.UserId
                : 0;
        }

        public static void RegisterDefaultSize(string pageKey, string cardKey, Size size, string preset)
        {
            if (string.IsNullOrWhiteSpace(pageKey) || string.IsNullOrWhiteSpace(cardKey) || size.Width <= 0 || size.Height <= 0)
                return;

            lock (DefaultSync)
            {
                if (!DefaultSizes.ContainsKey(cardKey))
                {
                    DefaultSizes[cardKey] = new CardDefaultSize
                    {
                        Size = size,
                        SizePreset = string.IsNullOrWhiteSpace(preset) ? "Medium" : preset
                    };
                }
            }
        }

        public Dictionary<string, CardLayoutDto> GetPageLayout(int userId, string pageKey)
        {
            var results = new Dictionary<string, CardLayoutDto>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(pageKey))
                return results;

            try
            {
                using (var conn = new DatabaseManager().GetConnection())
                using (var cmd = new SqlCommand(@"
SELECT PageKey, CardKey, Width, Height, SizePreset, SavedAt
FROM UserCardLayouts
WHERE PageKey = @PageKey AND UserId IN (@UserId, 0)
ORDER BY CASE WHEN UserId = @UserId THEN 0 ELSE 1 END, SavedAt DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@PageKey", pageKey);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    conn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string cardKey = Convert.ToString(reader["CardKey"]);
                            if (string.IsNullOrWhiteSpace(cardKey) || results.ContainsKey(cardKey))
                                continue;

                            results[cardKey] = new CardLayoutDto
                            {
                                PageKey = Convert.ToString(reader["PageKey"]),
                                CardKey = cardKey,
                                Width = reader["Width"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Width"]),
                                Height = reader["Height"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Height"]),
                                SizePreset = Convert.ToString(reader["SizePreset"]),
                                SavedAt = reader["SavedAt"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["SavedAt"])
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("CardLayoutService.GetPageLayout(" + pageKey + ")", ex);
                Log("GetPageLayout failed for " + pageKey + ": " + ex.Message);
            }

            return results;
        }

        public void SaveCardLayout(int userId, string pageKey, string cardKey, int width, int height, string preset)
        {
            if (string.IsNullOrWhiteSpace(pageKey) || string.IsNullOrWhiteSpace(cardKey))
                return;

            try
            {
                using (var conn = new DatabaseManager().GetConnection())
                using (var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM UserCardLayouts WHERE UserId = @UserId AND PageKey = @PageKey AND CardKey = @CardKey)
BEGIN
    UPDATE UserCardLayouts
    SET Width = @Width,
        Height = @Height,
        SizePreset = @SizePreset,
        SavedAt = GETDATE()
    WHERE UserId = @UserId AND PageKey = @PageKey AND CardKey = @CardKey
END
ELSE
BEGIN
    INSERT INTO UserCardLayouts (UserId, PageKey, CardKey, Width, Height, SizePreset, SavedAt)
    VALUES (@UserId, @PageKey, @CardKey, @Width, @Height, @SizePreset, GETDATE())
END", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@PageKey", pageKey);
                    cmd.Parameters.AddWithValue("@CardKey", cardKey);
                    cmd.Parameters.AddWithValue("@Width", Math.Max(0, width));
                    cmd.Parameters.AddWithValue("@Height", Math.Max(0, height));
                    cmd.Parameters.AddWithValue("@SizePreset", (object)(preset ?? "Custom"));
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                Log("Layout saved: " + pageKey + "/" + cardKey + " " + width + "x" + height + " [" + (preset ?? "Custom") + "]");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("CardLayoutService.SaveCardLayout(" + pageKey + "/" + cardKey + ")", ex);
                Log("SaveCardLayout failed for " + pageKey + "/" + cardKey + ": " + ex.Message);
            }
        }

        public void SavePageLayout(int userId, string pageKey, List<CardLayoutDto> layouts)
        {
            if (string.IsNullOrWhiteSpace(pageKey) || layouts == null)
                return;

            try
            {
                using (var conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        foreach (CardLayoutDto layout in layouts.Where(l => l != null && !string.IsNullOrWhiteSpace(l.CardKey)))
                        {
                            using (var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM UserCardLayouts WHERE UserId = @UserId AND PageKey = @PageKey AND CardKey = @CardKey)
BEGIN
    UPDATE UserCardLayouts
    SET Width = @Width,
        Height = @Height,
        SizePreset = @SizePreset,
        SavedAt = GETDATE()
    WHERE UserId = @UserId AND PageKey = @PageKey AND CardKey = @CardKey
END
ELSE
BEGIN
    INSERT INTO UserCardLayouts (UserId, PageKey, CardKey, Width, Height, SizePreset, SavedAt)
    VALUES (@UserId, @PageKey, @CardKey, @Width, @Height, @SizePreset, GETDATE())
END", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@PageKey", pageKey);
                                cmd.Parameters.AddWithValue("@CardKey", layout.CardKey);
                                cmd.Parameters.AddWithValue("@Width", Math.Max(0, layout.Width));
                                cmd.Parameters.AddWithValue("@Height", Math.Max(0, layout.Height));
                                cmd.Parameters.AddWithValue("@SizePreset", (object)(layout.SizePreset ?? "Custom"));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                    }
                }

                Log("Page layout saved: " + pageKey + " [" + layouts.Count + " card(s)]");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("CardLayoutService.SavePageLayout(" + pageKey + ")", ex);
                Log("SavePageLayout failed for " + pageKey + ": " + ex.Message);
            }
        }

        public void ResetCardLayout(int userId, string pageKey, string cardKey)
        {
            ExecuteDelete(userId, pageKey, cardKey);
        }

        public void ResetPageLayout(int userId, string pageKey)
        {
            ExecuteDelete(userId, pageKey, null);
        }

        public Dictionary<string, CardDefaultSize> GetDefaultSizes()
        {
            lock (DefaultSync)
            {
                return DefaultSizes.ToDictionary(
                    pair => pair.Key,
                    pair => new CardDefaultSize
                    {
                        Size = pair.Value.Size,
                        SizePreset = pair.Value.SizePreset
                    },
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        public void ApplyLayoutToPage(Control page, string pageKey, int userId)
        {
            if (page == null || string.IsNullOrWhiteSpace(pageKey))
                return;

            Dictionary<string, CardLayoutDto> layouts = GetPageLayout(userId, pageKey);
            Dictionary<string, CardDefaultSize> defaults = GetDefaultSizes();
            float layoutScale = LayoutScaler.GetUiScaleFactor();

            try
            {
                using (Graphics graphics = page.CreateGraphics())
                    layoutScale *= Math.Max(1f, graphics.DpiX / 96f);
            }
            catch
            {
                layoutScale = Math.Max(0.85f, layoutScale);
            }

            foreach (ResizableCard card in EnumerateCards(page).Where(card => string.Equals(card.PageKey, pageKey, StringComparison.OrdinalIgnoreCase)))
            {
                CardDefaultSize defaultSize;
                bool hasDefault = defaults.TryGetValue(card.CardKey ?? string.Empty, out defaultSize);
                int defaultWidth = hasDefault ? Math.Max(card.MinCardWidth, (int)Math.Round(defaultSize.Size.Width * layoutScale)) : card.Width;
                int defaultHeight = hasDefault ? Math.Max(card.MinCardHeight, (int)Math.Round(defaultSize.Size.Height * layoutScale)) : card.Height;

                CardLayoutDto persisted;
                if (layouts.TryGetValue(card.CardKey ?? string.Empty, out persisted))
                {
                    int width = persisted.Width > 0 ? Math.Max(defaultWidth, (int)Math.Round(persisted.Width * layoutScale)) : defaultWidth;
                    int height = persisted.Height > 0 ? Math.Max(defaultHeight, (int)Math.Round(persisted.Height * layoutScale)) : defaultHeight;
                    card.ApplyPersistedLayout(new Size(width, height), persisted.SizePreset);
                    continue;
                }

                if (hasDefault)
                    card.ApplyPersistedLayout(defaultSize.Size, defaultSize.SizePreset);
            }
        }

        public static IEnumerable<ResizableCard> EnumerateCards(Control root)
        {
            if (root == null)
                yield break;

            foreach (Control child in root.Controls)
            {
                ResizableCard card = child as ResizableCard;
                if (card != null)
                    yield return card;

                foreach (ResizableCard nested in EnumerateCards(child))
                    yield return nested;
            }
        }

        public static Task SaveCardLayoutAsync(ResizableCard card)
        {
            if (card == null)
                return Task.CompletedTask;

            int userId = ResolveCurrentUserId();
            return Task.Run(() => new CardLayoutService().SaveCardLayout(userId, card.PageKey, card.CardKey, card.Width, card.Height, card.SizePreset));
        }

        private void ExecuteDelete(int userId, string pageKey, string cardKey)
        {
            if (string.IsNullOrWhiteSpace(pageKey))
                return;

            try
            {
                using (var conn = new DatabaseManager().GetConnection())
                using (var cmd = new SqlCommand(cardKey == null
                    ? "DELETE FROM UserCardLayouts WHERE UserId = @UserId AND PageKey = @PageKey"
                    : "DELETE FROM UserCardLayouts WHERE UserId = @UserId AND PageKey = @PageKey AND CardKey = @CardKey", conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@PageKey", pageKey);
                    if (cardKey != null)
                        cmd.Parameters.AddWithValue("@CardKey", cardKey);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                Log((cardKey == null ? "Page layout reset: " + pageKey : "Card layout reset: " + pageKey + "/" + cardKey));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("CardLayoutService.ExecuteDelete(" + pageKey + ")", ex);
                Log("Reset failed for " + pageKey + (cardKey == null ? string.Empty : "/" + cardKey) + ": " + ex.Message);
            }
        }

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? @"C:\HVAC_PRO_MSE\LOGS");
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
