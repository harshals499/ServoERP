using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Audit;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class GlobalCardContextInfo
    {
        public string Title { get; set; }
        public string PageKey { get; set; }
        public string CardKey { get; set; }
        public string ControlType { get; set; }
        public Control Control { get; set; }

        /// <summary>Builds a stable ServoERP card path for support and QA.</summary>
        public string ToCardPath()
        {
            string page = string.IsNullOrWhiteSpace(PageKey) ? "UnknownPage" : CleanPathPart(PageKey);
            string key = string.IsNullOrWhiteSpace(CardKey) ? CleanPathPart(Title) : CleanPathPart(CardKey);
            if (string.IsNullOrWhiteSpace(key))
                key = string.IsNullOrWhiteSpace(ControlType) ? "Card" : CleanPathPart(ControlType);
            return "servoerp://card/" + page + "/" + key;
        }

        /// <summary>Builds a readable properties block for the global card menu.</summary>
        public string ToPropertiesText()
        {
            string size = Control == null ? "Unknown" : Control.Width + " x " + Control.Height;
            string name = Control == null ? string.Empty : Control.Name;
            return "Title: " + (string.IsNullOrWhiteSpace(Title) ? "ServoERP card" : Title) + Environment.NewLine +
                   "Page: " + (string.IsNullOrWhiteSpace(PageKey) ? "Unknown" : PageKey) + Environment.NewLine +
                   "Card key: " + (string.IsNullOrWhiteSpace(CardKey) ? "Not assigned" : CardKey) + Environment.NewLine +
                   "Control: " + (string.IsNullOrWhiteSpace(ControlType) ? "Unknown" : ControlType) + Environment.NewLine +
                   "Name: " + (string.IsNullOrWhiteSpace(name) ? "Not assigned" : name) + Environment.NewLine +
                   "Size: " + size + Environment.NewLine +
                   "Path: " + ToCardPath();
        }

        /// <summary>Returns a readable label for clipboard and diagnostic actions.</summary>
        public override string ToString()
        {
            string title = string.IsNullOrWhiteSpace(Title) ? "ServoERP card" : Title.Trim();
            string page = string.IsNullOrWhiteSpace(PageKey) ? string.Empty : " | Page: " + PageKey.Trim();
            string key = string.IsNullOrWhiteSpace(CardKey) ? string.Empty : " | Key: " + CardKey.Trim();
            return title + page + key;
        }

        private static string CleanPathPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] chars = value.Trim()
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
                .ToArray();
            return new string(chars);
        }
    }

    /// <summary>
    /// Single global, reusable right-click context menu (GlobalCardContextMenu) attached to every
    /// ServoERP card. Every menu item below is wired to a real, persisted, logged action:
    /// Open, Add/Remove Favorites, Copy as Path, Share, Send To (Dashboard/Favorites/Shortcuts),
    /// Cut, Copy, Create Shortcut, Lock/Unlock Card, Hide Card, Delete Card, Restore Card, Properties.
    /// </summary>
    internal static class GlobalCardContextMenu
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<Control> AttachedCards = new HashSet<Control>();
        private static readonly HashSet<Control> AttachedControls = new HashSet<Control>();
        private static readonly HashSet<ContextMenuStrip> ExtendedMenus = new HashSet<ContextMenuStrip>();
        private static readonly DashboardShortcutService ShortcutService = new DashboardShortcutService();
        private static readonly AuditTrailService Audit = new AuditTrailService();

        /// <summary>When true, suppresses feedback/confirmation MessageBoxes so automated audits can run unattended.</summary>
        public static bool SuppressFeedbackForTests { get; set; }

        /// <summary>Scans a form or page and attaches the global card context menu to detected cards.</summary>
        public static void ApplyToTree(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            foreach (Control control in EnumerateControls(root).ToList())
            {
                if (control is ResizableCard resizable)
                {
                    ExtendResizableCardMenu(resizable);
                    continue;
                }

                if (IsCardLike(control))
                    AttachCard(control);
            }
        }

        /// <summary>Attaches the global card context menu to one known card and safe child surfaces.</summary>
        public static void AttachCard(Control card, string title = null, string pageKey = null, string cardKey = null, Action open = null)
        {
            if (card == null || card.IsDisposed)
                return;

            lock (Sync)
            {
                if (AttachedCards.Contains(card))
                {
                    CardResizeGripService.Attach(card);
                    return;
                }

                AttachedCards.Add(card);
            }

            CardResizeGripService.Attach(card);
            card.Disposed += AttachedCardDisposed;
            AttachCardTree(card, card, title, pageKey, cardKey, open);
        }

        /// <summary>Returns the number of card roots with the global menu attached under the supplied root.</summary>
        public static int CountAttachedCards(Control root)
        {
            if (root == null)
                return 0;

            lock (Sync)
            {
                return EnumerateControls(root).Count(control => AttachedCards.Contains(control) || IsResizableMenuExtended(control));
            }
        }

        /// <summary>Returns true when the control root has the global card menu attached.</summary>
        public static bool IsAttachedCard(Control control)
        {
            if (control == null)
                return false;
            if (control is Panel && control.Height <= 100)
                return false;
            if (control is Panel && control.Width >= 800 && control.Height >= 500)
                return false;
            if (control is Panel && control.Height <= 120 && CardSurfacePolicy.ContainsAny(BuildAttachedLabelText(control), "SEARCH", "FILTER", "REVIEW", "LIST", "MANDATORY", "DETAILS", "MOVEMENT", "QUANTITY", "TRANSFER", "SELECTION", "SUPPLIER", "REQUEST", "PURCHASE", "STOCK"))
                return false;

            lock (Sync)
            {
                if (IsResizableMenuExtended(control))
                    return true;

                if (!AttachedCards.Contains(control) || !IsCardLike(control))
                    return false;

                CardResizeGripService.Attach(control);
                return true;
            }
        }

        /// <summary>Returns the number of card-like controls detected under the supplied root.</summary>
        public static int CountDetectedCards(Control root)
        {
            if (root == null)
                return 0;

            return EnumerateControls(root).Count(control => IsCardLike(control) || control is ResizableCard);
        }

        /// <summary>Builds the menu context for a card and exposes the wired action set for diagnostics/audits.</summary>
        public static GlobalCardContextInfo BuildContextForAudit(Control card, string title, string pageKey, string cardKey)
        {
            return BuildContext(card, title, pageKey, cardKey);
        }

        /// <summary>Builds the wired action set for a card for diagnostics/audits.</summary>
        public static WindowsFileContextMenuActions BuildActionsForAudit(Action open = null)
        {
            return BuildActions(open);
        }

        private static void AttachCardTree(Control card, Control current, string title, string pageKey, string cardKey, Action open)
        {
            if (current == null || current.IsDisposed || ShouldSkipRightClickSurface(current))
                return;

            if (current != card && (IsCardLike(current) || current is ResizableCard))
                return;

            bool newlyAttached = false;
            lock (Sync)
            {
                if (!AttachedControls.Contains(current))
                {
                    AttachedControls.Add(current);
                    newlyAttached = true;
                    current.Disposed += AttachedControlDisposed;
                    WindowsFileContextMenu.AttachControlOnly(current, () => BuildContext(card, title, pageKey, cardKey), BuildActions(open));
                }
            }

            foreach (Control child in current.Controls)
                AttachCardTree(card, child, title, pageKey, cardKey, open);

            if (newlyAttached)
                current.ControlAdded += (s, e) => AttachCardTree(card, e.Control, title, pageKey, cardKey, open);
        }

        private static void ExtendResizableCardMenu(ResizableCard card)
        {
            if (card == null || card.IsDisposed || card.ContextMenuStrip == null)
                return;

            lock (Sync)
            {
                if (ExtendedMenus.Contains(card.ContextMenuStrip))
                    return;

                ExtendedMenus.Add(card.ContextMenuStrip);
            }

            card.Disposed += (s, e) =>
            {
                lock (Sync)
                    ExtendedMenus.Remove(card.ContextMenuStrip);
            };

            ContextMenuStrip menu = card.ContextMenuStrip;
            menu.Items.Add(new ToolStripSeparator());
            AddStandardMenuItem(menu, "Open", card, FocusCard, true);
            AddStandardMenuItem(menu, "Add to Favorites", card, ToggleFavorite);
            AddStandardMenuItem(menu, "Copy as Path", card, CopyCardPath);
            AddStandardMenuItem(menu, "Share", card, ShareCard);
            AddStandardMenuItem(menu, "Send to Dashboard", card, ctx => SendTo(ctx, "Dashboard"));
            AddStandardMenuItem(menu, "Send to Favorites", card, ctx => SendTo(ctx, "Favorite"));
            AddStandardMenuItem(menu, "Send to Shortcuts", card, ctx => SendTo(ctx, "Shortcut"));
            AddStandardMenuItem(menu, "Cut", card, ctx => CopyCard(ctx, "Cut"));
            AddStandardMenuItem(menu, "Copy", card, ctx => CopyCard(ctx, "Copy"));
            AddStandardMenuItem(menu, "Create Shortcut", card, SaveShortcut);
            AddStandardMenuItem(menu, "Lock Card", card, ToggleCardLock);
            AddStandardMenuItem(menu, "Hide Card", card, HideCard);
            AddStandardMenuItem(menu, "Delete Card", card, DeleteCard, false, DS.Red600);
            AddStandardMenuItem(menu, "Restore Card", card, RestoreCard);
            AddStandardMenuItem(menu, "Properties", card, ShowProperties);
            menu.Opening += (s, e) => RefreshDynamicMenuText(menu, card);
        }

        private static void AddStandardMenuItem(ContextMenuStrip menu, string text, ResizableCard card, Action<object> action, bool bold = false, Color? foreColor = null)
        {
            var item = new ToolStripMenuItem(text)
            {
                Font = bold ? new Font(menu.Font, FontStyle.Bold) : menu.Font,
                ForeColor = foreColor ?? SystemColors.ControlText
            };
            item.Click += (s, e) => action(BuildContext(card, card.CardTitle, card.PageKey, card.CardKey));
            menu.Items.Add(item);
        }

        private static WindowsFileContextMenuActions BuildActions(Action open)
        {
            return new WindowsFileContextMenuActions
            {
                Open = ctx => OpenCard(ctx, open),
                IsFavorite = IsCardFavorite,
                ToggleFavorite = ToggleFavorite,
                Copy = ctx => CopyCard(ctx, "Copy"),
                Cut = ctx => CopyCard(ctx, "Cut"),
                Share = ShareCard,
                CopyAsPath = CopyCardPath,
                CreateShortcut = SaveShortcut,
                SendToDashboard = ctx => SendTo(ctx, "Dashboard"),
                SendToFavorites = ctx => SendTo(ctx, "Favorite"),
                SendToShortcuts = ctx => SendTo(ctx, "Shortcut"),
                IsLocked = IsCardLocked,
                ToggleLock = ToggleCardLock,
                HideCard = HideCard,
                DeleteCard = DeleteCard,
                RestoreCard = RestoreCard,
                Properties = ShowProperties
            };
        }

        private static GlobalCardContextInfo BuildContext(Control card, string title, string pageKey, string cardKey)
        {
            var resizable = card as ResizableCard;
            return new GlobalCardContextInfo
            {
                Control = card,
                Title = FirstNonEmpty(title, resizable == null ? null : resizable.CardTitle, FindTitle(card), card == null ? null : card.Name),
                PageKey = FirstNonEmpty(pageKey, resizable == null ? null : resizable.PageKey, card == null || card.FindForm() == null ? null : card.FindForm().GetType().Name),
                CardKey = FirstNonEmpty(cardKey, resizable == null ? null : resizable.CardKey, card == null ? null : card.Name),
                ControlType = card == null ? string.Empty : card.GetType().Name
            };
        }

        private static string FindTitle(Control root)
        {
            if (root == null)
                return string.Empty;

            foreach (Label label in EnumerateControls(root).OfType<Label>())
            {
                string text = (label.Text ?? string.Empty).Trim();
                if (text.Length >= 2 && text.Length <= 80)
                    return text;
            }

            foreach (Button button in EnumerateControls(root).OfType<Button>())
            {
                string text = (button.Text ?? string.Empty).Trim();
                if (text.Length >= 2 && text.Length <= 80)
                    return text;
            }

            return string.Empty;
        }

        private static bool IsCardLike(Control control)
        {
            return CardSurfacePolicy.IsContextMenuCard(control);
        }

        private static bool ShouldSkipRightClickSurface(Control control)
        {
            return CardSurfacePolicy.ShouldSkipRightClickSurface(control);
        }

        // ---------------------------------------------------------------
        // Menu actions: Open, Favorites, Copy as Path, Share, Send To,
        // Cut/Copy, Create Shortcut, Lock/Unlock, Hide/Delete/Restore, Properties
        // ---------------------------------------------------------------

        private static void OpenCard(object context, Action open)
        {
            ExecuteCardAction(context, "OPEN", "View", info =>
            {
                if (open != null)
                    open();
                else
                    FocusCardInternal(info);

                return CardActionResult.Ok("Opened " + DisplayTitle(info) + ".");
            });
        }

        private static void FocusCard(object context)
        {
            ExecuteCardAction(context, "OPEN", "View", info =>
            {
                FocusCardInternal(info);
                return CardActionResult.Ok("Opened " + DisplayTitle(info) + ".");
            });
        }

        private static void FocusCardInternal(GlobalCardContextInfo info)
        {
            Control control = info == null ? null : info.Control;
            if (control == null || control.IsDisposed)
                return;

            control.BringToFront();
            if (control.CanFocus)
                control.Focus();
        }

        private static void ToggleFavorite(object context)
        {
            ExecuteCardAction(context, "FAVORITE", "View", info => GlobalCardStateService.ToggleFavorite(info, ShortcutService), "Favorites");
        }

        private static void CopyCardPath(object context)
        {
            ExecuteCardAction(context, "COPY_PATH", "View", info =>
            {
                string path = info.ToCardPath();
                SetClipboardText(path);
                Debug.WriteLine("Global card menu Copy as Path: " + path);
                return CardActionResult.Ok("Copied card path to clipboard: " + path);
            }, "Copy as Path");
        }

        private static void ShareCard(object context)
        {
            ExecuteCardAction(context, "SHARE", "View", info =>
            {
                string shareText = DisplayTitle(info) + Environment.NewLine + info.ToCardPath();
                SetClipboardText(shareText);
                return CardActionResult.Ok("Share link copied to clipboard for " + DisplayTitle(info) + ".");
            }, "Share");
        }

        private static void SendTo(object context, string kind)
        {
            ExecuteCardAction(context, "SEND_TO_" + kind.ToUpperInvariant(), "View", info =>
            {
                string path = ShortcutService.SaveShortcut(info.Title, info.PageKey, info.CardKey, info.ToCardPath(), kind);

                if (string.Equals(kind, "Favorite", StringComparison.OrdinalIgnoreCase))
                {
                    CardActionState state = GlobalCardStateService.Load(info);
                    state.Favorite = true;
                    GlobalCardStateService.Save(info, state);
                }
                else if (string.Equals(kind, "Shortcut", StringComparison.OrdinalIgnoreCase))
                {
                    GlobalCardStateService.SetShortcut(info, true, "SEND_TO_SHORTCUT");
                }

                return CardActionResult.Ok(DisplayTitle(info) + " sent to " + kind + "." + Environment.NewLine + path);
            }, "Send To");
        }

        private static void CopyCard(object context, string clipboardState)
        {
            ExecuteCardAction(context, clipboardState.ToUpperInvariant(), "View", info => GlobalCardStateService.SetClipboardState(info, clipboardState), clipboardState);
        }

        private static void SaveShortcut(object context)
        {
            ExecuteCardAction(context, "CREATE_SHORTCUT", "View", info =>
            {
                string path = ShortcutService.SaveShortcut(info.Title, info.PageKey, info.CardKey, info.ToCardPath(), "Shortcut");
                GlobalCardStateService.SetShortcut(info, true, "CREATE_SHORTCUT");
                return CardActionResult.Ok("Shortcut saved for " + DisplayTitle(info) + "." + Environment.NewLine + path);
            }, "Create Shortcut");
        }

        private static bool IsCardFavorite(object context)
        {
            var info = context as GlobalCardContextInfo;
            return info != null && GlobalCardStateService.IsFavorite(info);
        }

        private static bool IsCardLocked(object context)
        {
            var info = context as GlobalCardContextInfo;
            return info != null && GlobalCardStateService.IsLocked(info);
        }

        private static void ToggleCardLock(object context)
        {
            ExecuteCardAction(context, "LOCK", "View", info =>
            {
                if (info.Control == null || info.Control.IsDisposed)
                    return CardActionResult.Fail("Card control is not available.");

                return GlobalCardStateService.ToggleLock(info);
            }, "Card Layout");
        }

        private static void HideCard(object context)
        {
            ExecuteCardAction(context, "HIDE", "View", GlobalCardStateService.HideCard, "Hide Card");
        }

        private static void DeleteCard(object context)
        {
            var info = context as GlobalCardContextInfo;
            string title = info != null && !string.IsNullOrWhiteSpace(info.Title) ? info.Title.Trim() : "this card";

            if (!SuppressFeedbackForTests)
            {
                DialogResult confirm = MessageBox.Show(
                    "Delete " + title + "? It will be removed from this layout, but you can bring it back later with Restore Card.",
                    BrandingService.WindowTitle("Delete Card"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;
            }

            ExecuteCardAction(context, "DELETE", "View", GlobalCardStateService.DeleteCard, "Delete Card");
        }

        private static void RestoreCard(object context)
        {
            ExecuteCardAction(context, "RESTORE", "View", GlobalCardStateService.RestoreCard, "Restore Card");
        }

        private static void ShowProperties(object context)
        {
            ExecuteCardAction(context, "PROPERTIES", "View", info =>
            {
                string text = GlobalCardStateService.BuildPropertiesText(info);
                if (!SuppressFeedbackForTests)
                {
                    MessageBox.Show(
                        text,
                        BrandingService.WindowTitle("Card Properties"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return CardActionResult.Ok(text);
            });
        }

        private static void RefreshDynamicMenuText(ContextMenuStrip menu, ResizableCard card)
        {
            if (menu == null || card == null)
                return;

            GlobalCardContextInfo info = BuildContext(card, card.CardTitle, card.PageKey, card.CardKey);
            bool locked = GlobalCardStateService.IsLocked(info);
            bool favorite = GlobalCardStateService.IsFavorite(info);

            foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
            {
                if (string.Equals(item.Text, "Lock Card", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Text, "Unlock Card", StringComparison.OrdinalIgnoreCase))
                {
                    item.Text = locked ? "Unlock Card" : "Lock Card";
                }
                else if (string.Equals(item.Text, "Add to Favorites", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Text, "Remove from Favorites", StringComparison.OrdinalIgnoreCase))
                {
                    item.Text = favorite ? "Remove from Favorites" : "Add to Favorites";
                }
            }
        }

        // ---------------------------------------------------------------
        // Shared execution wrapper: permission check, persistence, audit
        // log, app log, and success/failure feedback for every action.
        // ---------------------------------------------------------------

        private static void ExecuteCardAction(object context, string action, string permission, Func<GlobalCardContextInfo, CardActionResult> execute, string feedbackTitle = null)
        {
            var info = context as GlobalCardContextInfo;
            if (info == null)
                info = new GlobalCardContextInfo { Title = "ServoERP card", PageKey = "Dashboard", CardKey = "Card" };

            try
            {
                string module = ResolvePermissionModule(info.PageKey);
                SessionManager.DemandPermission(module, permission);

                CardActionResult result = execute(info) ?? CardActionResult.Ok("Done.");
                GlobalCardStateService.RecordAction(info, action);

                Audit.Record(action, module, null, (result.Success ? "OK" : "FAIL") + ": " + result.Message + " (" + info.ToCardPath() + ")");
                AppLogger.LogInfo("GlobalCardContextMenu." + action + " -> " + (result.Success ? "SUCCESS" : "FAILED") + ": " + result.Message);
                Debug.WriteLine("Global card menu " + action + ": " + result.Message);

                if (!string.IsNullOrWhiteSpace(feedbackTitle))
                    ShowActionFeedback(feedbackTitle, result.Message, result.Success);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GlobalCardContextMenu." + action, ex);
                if (!SuppressFeedbackForTests)
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Card Menu"), action + " card", ex);
            }
        }

        private static void ShowActionFeedback(string title, string message, bool success = true)
        {
            if (SuppressFeedbackForTests)
                return;

            MessageBox.Show(
                message,
                BrandingService.WindowTitle(title),
                MessageBoxButtons.OK,
                success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static string DisplayTitle(GlobalCardContextInfo info)
        {
            return info != null && !string.IsNullOrWhiteSpace(info.Title) ? info.Title.Trim() : "ServoERP card";
        }

        private static void SetClipboardText(string text)
        {
            Clipboard.SetDataObject(text ?? string.Empty, true, 5, 100);
        }

        private static string ResolvePermissionModule(string pageKey)
        {
            if (string.IsNullOrWhiteSpace(pageKey))
                return "Dashboard";

            string key = pageKey.Trim();
            if (string.Equals(key, "ReportsCommandCenter", StringComparison.OrdinalIgnoreCase))
                return "Reports";

            return key;
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            if (root == null)
                yield break;

            yield return root;
            foreach (Control child in root.Controls)
            {
                foreach (Control descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static bool IsResizableMenuExtended(Control control)
        {
            var card = control as ResizableCard;
            return card != null && card.ContextMenuStrip != null && ExtendedMenus.Contains(card.ContextMenuStrip);
        }

        private static string BuildAttachedLabelText(Control control)
        {
            if (control == null)
                return string.Empty;

            return string.Join(" ", control.Controls.OfType<Label>().Select(label => label.Text ?? string.Empty)).ToUpperInvariant();
        }

        private static void AttachedCardDisposed(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            lock (Sync)
                AttachedCards.Remove(control);
        }

        private static void AttachedControlDisposed(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            lock (Sync)
                AttachedControls.Remove(control);
        }
    }
}
