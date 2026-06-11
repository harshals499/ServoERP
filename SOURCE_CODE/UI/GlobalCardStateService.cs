using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    /// <summary>Result of one GlobalCardContextMenu action, used for feedback, logging, and audit reporting.</summary>
    internal sealed class CardActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static CardActionResult Ok(string message)
        {
            return new CardActionResult { Success = true, Message = message };
        }

        public static CardActionResult Fail(string message)
        {
            return new CardActionResult { Success = false, Message = message };
        }
    }

    /// <summary>Persisted, per-card state (favorite/hidden/deleted/shortcut/clipboard/last action) for the global card context menu.</summary>
    internal static class GlobalCardStateService
    {
        private const string OverlayName = "__servoerpCardStateOverlay";
        private static readonly object Sync = new object();
        private static readonly Dictionary<Control, Control> Overlays = new Dictionary<Control, Control>();

        /// <summary>Loads the persisted action state for a card, or a fresh default record.</summary>
        public static CardActionState Load(GlobalCardContextInfo info)
        {
            return CardActionStateService.LoadCard(ResolvePageKey(info), ResolveCardKey(info), info == null ? string.Empty : info.Title);
        }

        /// <summary>Persists the action state for a card.</summary>
        public static void Save(GlobalCardContextInfo info, CardActionState state)
        {
            if (state == null)
                return;

            state.CardKey = ResolveCardKey(info);
            state.Title = info == null ? state.Title : info.Title;
            CardActionStateService.Save(ResolvePageKey(info), state);
        }

        public static bool IsFavorite(GlobalCardContextInfo info)
        {
            return Load(info).Favorite;
        }

        public static bool IsHidden(GlobalCardContextInfo info)
        {
            return Load(info).Hidden;
        }

        public static bool IsDeleted(GlobalCardContextInfo info)
        {
            return Load(info).Deleted;
        }

        public static string GetClipboardState(GlobalCardContextInfo info)
        {
            CardActionState state = Load(info);
            return state.ClipboardState ?? string.Empty;
        }

        /// <summary>Returns true when the card is locked, either via the dashboard layout service or persisted card state.</summary>
        public static bool IsLocked(GlobalCardContextInfo info)
        {
            if (info != null && info.Control != null && GlobalDashboardLayoutService.IsCardLocked(info.Control))
                return true;

            return Load(info).Locked;
        }

        /// <summary>Toggles the locked flag for a card, persists it, and syncs with the dashboard layout service when tracked.</summary>
        public static CardActionResult ToggleLock(GlobalCardContextInfo info)
        {
            CardActionState state = Load(info);
            bool locked = !IsLocked(info);
            state.Locked = locked;
            state.LastAction = locked ? "LOCK" : "UNLOCK";
            Save(info, state);

            Control control = info == null ? null : info.Control;
            if (control != null && !control.IsDisposed)
                GlobalDashboardLayoutService.SetCardLocked(control, locked);

            string title = DisplayTitle(info);
            return CardActionResult.Ok(locked
                ? title + " is locked. Right-click again and choose Unlock Card to resize or move it."
                : title + " is unlocked.");
        }

        /// <summary>Toggles the favorite flag for a card and persists the change.</summary>
        public static CardActionResult ToggleFavorite(GlobalCardContextInfo info, DashboardShortcutService shortcuts)
        {
            CardActionState state = Load(info);
            state.Favorite = !state.Favorite;
            state.LastAction = state.Favorite ? "ADD_FAVORITE" : "REMOVE_FAVORITE";
            Save(info, state);

            string title = DisplayTitle(info);
            if (state.Favorite)
            {
                shortcuts.SaveShortcut(info.Title, info.PageKey, info.CardKey, info.ToCardPath(), "Favorite");
                return CardActionResult.Ok(title + " added to favorites.");
            }

            shortcuts.RemoveShortcut(info.ToCardPath(), "Favorite");
            return CardActionResult.Ok(title + " removed from favorites.");
        }

        /// <summary>Marks a card as having a saved shortcut.</summary>
        public static void SetShortcut(GlobalCardContextInfo info, bool value, string action)
        {
            CardActionState state = Load(info);
            state.HasShortcut = value;
            state.LastAction = action;
            Save(info, state);
        }

        /// <summary>Persists a Cut/Copy clipboard marker for a card and copies its configuration text to the clipboard.</summary>
        public static CardActionResult SetClipboardState(GlobalCardContextInfo info, string clipboardState)
        {
            CardActionState state = Load(info);
            state.ClipboardState = clipboardState;
            state.LastAction = clipboardState.ToUpperInvariant();
            Save(info, state);

            string config = BuildCardConfigurationText(info, state);
            string prefix = string.Equals(clipboardState, "Cut", StringComparison.OrdinalIgnoreCase) ? "[CUT] " : "[COPY] ";
            Clipboard.SetText(prefix + config);

            string title = DisplayTitle(info);
            return string.Equals(clipboardState, "Cut", StringComparison.OrdinalIgnoreCase)
                ? CardActionResult.Ok(title + " marked for move. Card configuration copied to clipboard.")
                : CardActionResult.Ok(title + " configuration copied to clipboard.");
        }

        /// <summary>Records the last action performed on a card without changing other state.</summary>
        public static void RecordAction(GlobalCardContextInfo info, string action)
        {
            CardActionState state = Load(info);
            state.LastAction = action;
            Save(info, state);
        }

        /// <summary>Hides a card behind an overlay and persists the hidden flag.</summary>
        public static CardActionResult HideCard(GlobalCardContextInfo info)
        {
            return ApplyOverlay(info, hide: true);
        }

        /// <summary>Marks a card as deleted, hides it behind an overlay, and clears favorite/shortcut state.</summary>
        public static CardActionResult DeleteCard(GlobalCardContextInfo info)
        {
            return ApplyOverlay(info, hide: false);
        }

        /// <summary>Restores a hidden or deleted card by removing its overlay and clearing the hidden/deleted flags.</summary>
        public static CardActionResult RestoreCard(GlobalCardContextInfo info)
        {
            Control card = info == null ? null : info.Control;
            CardActionState state = Load(info);
            string title = DisplayTitle(info);

            if (!state.Hidden && !state.Deleted)
                return CardActionResult.Ok(title + " is already visible. Nothing to restore.");

            if (card != null && !card.IsDisposed)
            {
                Control overlay;
                lock (Sync)
                {
                    if (Overlays.TryGetValue(card, out overlay))
                        Overlays.Remove(card);
                }

                if (overlay != null && !overlay.IsDisposed)
                {
                    card.Controls.Remove(overlay);
                    overlay.Dispose();
                }
            }

            string previousStatus = state.Deleted ? "deleted" : "hidden";
            state.Hidden = false;
            state.Deleted = false;
            state.LastAction = "RESTORE";
            Save(info, state);
            return CardActionResult.Ok(title + " restored from " + previousStatus + " state.");
        }

        /// <summary>Builds the full Properties text for a card, including persisted state.</summary>
        public static string BuildPropertiesText(GlobalCardContextInfo info)
        {
            CardActionState state = Load(info);
            Control control = info == null ? null : info.Control;
            bool locked = IsLocked(info);
            string status = state.Deleted ? "Deleted" : (state.Hidden ? "Hidden" : "Active");
            string visibility = control != null && !control.Visible ? "Hidden" : (state.Hidden || state.Deleted ? "Hidden" : "Visible");
            string position = control == null ? "Unknown" : control.Left + ", " + control.Top;
            string size = control == null ? "Unknown" : control.Width + " x " + control.Height;
            string lastAction = string.IsNullOrWhiteSpace(state.LastAction) ? "None" : state.LastAction;
            string lastModified = state.LastActionAt == default(DateTime) ? "Never" : state.LastActionAt.ToString("yyyy-MM-dd HH:mm:ss");

            return "Card ID: " + (string.IsNullOrWhiteSpace(info.CardKey) ? "Not assigned" : info.CardKey) + Environment.NewLine +
                   "Title: " + DisplayTitle(info) + Environment.NewLine +
                   "Module: " + (string.IsNullOrWhiteSpace(info.PageKey) ? "Unknown" : info.PageKey) + Environment.NewLine +
                   "Path: " + info.ToCardPath() + Environment.NewLine +
                   "Status: " + status + Environment.NewLine +
                   "Visibility: " + visibility + Environment.NewLine +
                   "Position: " + position + Environment.NewLine +
                   "Size: " + size + Environment.NewLine +
                   "Locked: " + (locked ? "Yes" : "No") + Environment.NewLine +
                   "Favorite: " + (state.Favorite ? "Yes" : "No") + Environment.NewLine +
                   "Has Shortcut: " + (state.HasShortcut ? "Yes" : "No") + Environment.NewLine +
                   "Clipboard State: " + (string.IsNullOrWhiteSpace(state.ClipboardState) ? "None" : state.ClipboardState) + Environment.NewLine +
                   "Last Action: " + lastAction + Environment.NewLine +
                   "Last Modified: " + lastModified;
        }

        private static CardActionResult ApplyOverlay(GlobalCardContextInfo info, bool hide)
        {
            Control card = info == null ? null : info.Control;
            if (card == null || card.IsDisposed)
                return CardActionResult.Fail("Card control is not available.");

            CardActionState state = Load(info);
            string title = DisplayTitle(info);

            if (hide && state.Hidden)
                return CardActionResult.Ok(title + " is already hidden.");
            if (!hide && state.Deleted)
                return CardActionResult.Ok(title + " is already deleted.");

            string label = hide
                ? "Hidden: " + title + Environment.NewLine + "Right-click → Restore Card to bring it back."
                : "Deleted: " + title + Environment.NewLine + "Right-click → Restore Card to recover it.";
            Color backColor = hide ? DS.Slate100 : DS.Red50;
            Color foreColor = hide ? DS.Slate600 : DS.Red600;

            Control overlay;
            lock (Sync)
            {
                Overlays.TryGetValue(card, out overlay);
            }

            if (overlay == null || overlay.IsDisposed)
            {
                overlay = new Panel
                {
                    Name = OverlayName,
                    Dock = DockStyle.Fill,
                    BackColor = backColor,
                    Tag = "NO_CARD_SURFACE"
                };

                var label1 = new Label
                {
                    Name = OverlayName + "Label",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Tag = "NO_CARD_SURFACE",
                    Text = label,
                    ForeColor = foreColor
                };
                overlay.Controls.Add(label1);

                card.Controls.Add(overlay);
                overlay.BringToFront();

                lock (Sync)
                    Overlays[card] = overlay;

                card.Disposed += (s, e) =>
                {
                    lock (Sync)
                        Overlays.Remove(card);
                };
            }
            else
            {
                overlay.BackColor = backColor;
                Label existingLabel = overlay.Controls.OfType<Label>().FirstOrDefault();
                if (existingLabel != null)
                {
                    existingLabel.Text = label;
                    existingLabel.ForeColor = foreColor;
                }
                overlay.BringToFront();
            }

            if (hide)
            {
                state.Hidden = true;
                state.Deleted = false;
                state.LastAction = "HIDE";
            }
            else
            {
                state.Deleted = true;
                state.Hidden = false;
                state.Favorite = false;
                state.HasShortcut = false;
                state.LastAction = "DELETE";
            }

            Save(info, state);
            return CardActionResult.Ok(hide
                ? title + " hidden. Use Restore Card to bring it back."
                : title + " deleted from this layout. Use Restore Card to recover it.");
        }

        private static string BuildCardConfigurationText(GlobalCardContextInfo info, CardActionState state)
        {
            Control control = info == null ? null : info.Control;
            string size = control == null ? "Unknown" : control.Width + "x" + control.Height;
            string position = control == null ? "Unknown" : control.Left + "," + control.Top;
            bool locked = control != null && GlobalDashboardLayoutService.IsCardLocked(control);

            return "CardKey=" + ResolveCardKey(info) +
                   "; Title=" + DisplayTitle(info) +
                   "; Page=" + ResolvePageKey(info) +
                   "; Size=" + size +
                   "; Position=" + position +
                   "; Locked=" + locked +
                   "; Favorite=" + state.Favorite;
        }

        private static string DisplayTitle(GlobalCardContextInfo info)
        {
            return info != null && !string.IsNullOrWhiteSpace(info.Title) ? info.Title.Trim() : "ServoERP card";
        }

        private static string ResolvePageKey(GlobalCardContextInfo info)
        {
            return info != null && !string.IsNullOrWhiteSpace(info.PageKey) ? info.PageKey.Trim() : "Dashboard";
        }

        private static string ResolveCardKey(GlobalCardContextInfo info)
        {
            string key = info == null ? null : info.CardKey;
            if (string.IsNullOrWhiteSpace(key))
                key = info == null ? null : info.Title;
            if (string.IsNullOrWhiteSpace(key))
                key = info == null ? null : info.ControlType;
            if (string.IsNullOrWhiteSpace(key))
                key = "Card";

            char[] chars = key.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.').ToArray();
            string cleaned = new string(chars);
            return string.IsNullOrWhiteSpace(cleaned) ? "Card" : cleaned;
        }
    }
}
