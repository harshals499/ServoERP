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

    internal static class GlobalCardContextMenu
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<Control> AttachedCards = new HashSet<Control>();
        private static readonly HashSet<Control> AttachedControls = new HashSet<Control>();
        private static readonly HashSet<ContextMenuStrip> ExtendedMenus = new HashSet<ContextMenuStrip>();
        private static readonly DashboardShortcutService ShortcutService = new DashboardShortcutService();
        private static readonly AuditTrailService Audit = new AuditTrailService();

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
            AddStandardMenuItem(menu, "Open", card, ctx => FocusCard(ctx), true);
            AddStandardMenuItem(menu, "Add to Favorites", card, SaveFavorite);
            AddStandardMenuItem(menu, "Copy as path", card, CopyCardPath);
            AddStandardMenuItem(menu, "Cut", card, ctx => CopyCardText(ctx, "Cut"));
            AddStandardMenuItem(menu, "Copy", card, ctx => CopyCardText(ctx, "Copy"));
            AddStandardMenuItem(menu, "Create shortcut", card, SaveShortcut);
            AddStandardMenuItem(menu, "Share", card, ShareCard);
            AddStandardMenuItem(menu, "Lock Card", card, ToggleCardLock);
            AddStandardMenuItem(menu, "Properties", card, ShowProperties);
            menu.Opening += (s, e) => RefreshLockMenuText(menu, card);
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
                Open = ctx =>
                {
                    if (open != null)
                        open();
                    else
                        FocusCard(ctx);
                },
                Copy = ctx => CopyCardText(ctx, "Copy"),
                Cut = ctx => CopyCardText(ctx, "Cut"),
                Share = ShareCard,
                CopyAsPath = CopyCardPath,
                AddToFavorites = SaveFavorite,
                CreateShortcut = SaveShortcut,
                SendToDesktop = SaveShortcut,
                SendToEmail = ShareCard,
                IsLocked = ctx => IsCardLocked(ctx),
                ToggleLock = ToggleCardLock,
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

        private static void FocusCard(object context)
        {
            ExecuteCardAction(context, "OPEN", "View", info =>
            {
                Control control = info.Control;
                if (control == null || control.IsDisposed)
                    return;

                control.BringToFront();
                if (control.CanFocus)
                    control.Focus();
            }, false);
        }

        private static void CopyCardText(object context, string action)
        {
            ExecuteCardAction(context, action.ToUpperInvariant(), "View", info =>
            {
                Clipboard.SetText(info.ToString());
                Debug.WriteLine("Global card menu " + action + ": " + info);
                ShowActionFeedback(action, "Copied card details to clipboard.");
            });
        }

        private static void CopyCardPath(object context)
        {
            ExecuteCardAction(context, "COPY_PATH", "View", info =>
            {
                string path = info.ToCardPath();
                Clipboard.SetText(path);
                Debug.WriteLine("Global card menu Copy as path: " + path);
                ShowActionFeedback("Copy as path", "Copied card path to clipboard.");
            });
        }

        private static void ShowProperties(object context)
        {
            ExecuteCardAction(context, "PROPERTIES", "View", info =>
            {
                MessageBox.Show(
                    info.ToPropertiesText(),
                    BrandingService.WindowTitle("Card Properties"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }, false);
        }

        private static bool IsCardLocked(object context)
        {
            var info = context as GlobalCardContextInfo;
            return info != null && GlobalDashboardLayoutService.IsCardLocked(info.Control);
        }

        private static void ToggleCardLock(object context)
        {
            ExecuteCardAction(context, "LOCK", "View", info =>
            {
                if (info.Control == null || info.Control.IsDisposed)
                    return;

                bool locked = GlobalDashboardLayoutService.ToggleCardLock(info.Control);
                string title = string.IsNullOrWhiteSpace(info.Title) ? "card" : info.Title;
                MessageBox.Show(
                    locked ? title + " is locked. Right-click again and choose Unlock Card to resize or move it." : title + " is unlocked.",
                    BrandingService.WindowTitle("Card Layout"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });
        }

        private static void RefreshLockMenuText(ContextMenuStrip menu, Control card)
        {
            if (menu == null || card == null)
                return;

            foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
            {
                if (string.Equals(item.Text, "Lock Card", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Text, "Unlock Card", StringComparison.OrdinalIgnoreCase))
                {
                    item.Text = GlobalDashboardLayoutService.IsCardLocked(card) ? "Unlock Card" : "Lock Card";
                    return;
                }
            }
        }

        private static void SaveFavorite(object context)
        {
            SaveDashboardShortcut(context, "Favorite");
        }

        private static void SaveShortcut(object context)
        {
            SaveDashboardShortcut(context, "Shortcut");
        }

        private static void SaveDashboardShortcut(object context, string kind)
        {
            ExecuteCardAction(context, kind.ToUpperInvariant(), "View", info =>
            {
                string path = ShortcutService.SaveShortcut(info.Title, info.PageKey, info.CardKey, info.ToCardPath(), kind);
                ShowActionFeedback(kind, kind + " saved for " + info.Title + "." + Environment.NewLine + path);
            });
        }

        private static void ShareCard(object context)
        {
            ExecuteCardAction(context, "SHARE", "View", info =>
            {
                string shareText = info.Title + Environment.NewLine + info.ToCardPath();
                Clipboard.SetText(shareText);
                ShowActionFeedback("Share", "Share link copied to clipboard.");
            });
        }

        private static void ExecuteCardAction(object context, string action, string permission, Action<GlobalCardContextInfo> execute, bool showSuccessAudit = true)
        {
            var info = context as GlobalCardContextInfo;
            if (info == null)
                info = new GlobalCardContextInfo { Title = "ServoERP card", PageKey = "Dashboard", CardKey = "Card" };

            try
            {
                string module = string.IsNullOrWhiteSpace(info.PageKey) ? "Dashboard" : info.PageKey;
                SessionManager.DemandPermission(module, permission);
                execute(info);
                Audit.Record(action, module, null, "Card action executed for " + info.ToCardPath());
                if (showSuccessAudit)
                    Debug.WriteLine("Global card menu " + action + ": " + info.ToCardPath());
            }
            catch (Exception ex)
            {
                AppLogger.LogError("GlobalCardContextMenu." + action, ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Card Menu"), action + " card", ex);
            }
        }

        private static void ShowActionFeedback(string title, string message)
        {
            MessageBox.Show(
                message,
                BrandingService.WindowTitle(title),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
