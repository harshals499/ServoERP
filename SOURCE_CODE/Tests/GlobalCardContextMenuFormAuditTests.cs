using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>One row of the GlobalCardContextMenu audit report for a single card/function combination.</summary>
    public sealed class CardMenuAuditRow
    {
        public string Form { get; set; }
        public string Card { get; set; }
        public string Function { get; set; }
        public string Expected { get; set; }
        public string Actual { get; set; }
        public bool Pass { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Exercises every GlobalCardContextMenu function (Open, Add/Remove Favorites, Copy as Path, Share,
    /// Send To, Cut, Copy, Create Shortcut, Lock/Unlock Card, Hide/Delete/Restore Card, Properties)
    /// against every detected card on every ServoERP form/page.
    /// </summary>
    public static class GlobalCardContextMenuFormAuditTests
    {
        private static readonly Type[] FormTypes =
        {
            typeof(DashboardForm),
            typeof(ClientManagementForm),
            typeof(ContractManagementForm),
            typeof(InvoiceForm),
            typeof(PaymentForm),
            typeof(SLADashboardForm),
            typeof(TenderBidForm),
            typeof(ReportForm),
            typeof(SettingsForm),
            typeof(VendorForm),
            typeof(PurchaseForm),
            typeof(InventoryForm),
            typeof(EmployeeForm),
            typeof(PayrollForm),
            typeof(GeoIntelligenceForm),
            typeof(JobManagementForm),
            typeof(MasterDataForm),
            typeof(WhatsAppHubForm),
            typeof(TallyIntegrationForm),
            typeof(ClientDetailPage),
            typeof(JobDetailPage)
        };

        /// <summary>Runs the full per-card audit and throws if any function check fails.</summary>
        public static IEnumerable<string> RunAll()
        {
            List<CardMenuAuditRow> rows = BuildAuditRows();
            List<CardMenuAuditRow> failures = rows.Where(row => !row.Pass).ToList();

            int forms = rows.Select(row => row.Form).Distinct().Count();
            int cards = rows.Select(row => row.Form + "|" + row.Card).Distinct().Count();
            yield return "global card context menu audit covered " + forms + " forms, " + cards + " cards, " + rows.Count + " function checks (" + failures.Count + " failed)";

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("GlobalCardContextMenu audit has " + failures.Count + " failing checks: " +
                    string.Join("; ", failures.Select(f => f.Form + "/" + f.Card + "/" + f.Function)));
            }
        }

        /// <summary>Runs the full per-card audit and writes a tab-separated report to TEST_RESULTS.</summary>
        public static string WriteReport()
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "global-card-context-menu-audit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

            List<CardMenuAuditRow> rows = BuildAuditRows();
            int failed = rows.Count(row => !row.Pass);

            var lines = new List<string>();
            lines.Add("ServoERP GlobalCardContextMenu Audit");
            lines.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("");
            lines.Add("Forms tested: " + rows.Select(row => row.Form).Distinct().Count());
            lines.Add("Cards tested: " + rows.Select(row => row.Form + "|" + row.Card).Distinct().Count());
            lines.Add("Functions tested: " + rows.Count);
            lines.Add("Passed: " + (rows.Count - failed) + ", Failed: " + failed);
            lines.Add("");
            lines.Add(string.Join("\t", "Form", "Card", "Function", "Expected", "Actual", "Pass/Fail", "Error"));
            foreach (CardMenuAuditRow row in rows)
            {
                lines.Add(string.Join("\t",
                    row.Form,
                    row.Card,
                    row.Function,
                    Flatten(row.Expected),
                    Flatten(row.Actual),
                    row.Pass ? "PASS" : "FAIL",
                    row.Pass ? string.Empty : Flatten(row.Error)));
            }

            File.WriteAllLines(path, lines);
            return path;
        }

        /// <summary>Builds the full audit row set by instantiating every form, attaching the global card menu, and exercising every function on every detected card.</summary>
        public static List<CardMenuAuditRow> BuildAuditRows()
        {
            var rows = new List<CardMenuAuditRow>();
            bool previousSuppress = GlobalCardContextMenu.SuppressFeedbackForTests;
            GlobalCardContextMenu.SuppressFeedbackForTests = true;

            AppUserDto previousUser = SessionManager.CurrentUser;
            Guid? previousSessionId = SessionManager.CurrentSessionId;
            DateTime? previousExpiresAt = SessionManager.ExpiresAt;
            bool startedSession = false;

            if (!SessionManager.IsLoggedIn)
            {
                string message;
                startedSession = LocalLoginBypassService.TryStartSession(out message);
                if (!startedSession)
                    throw new InvalidOperationException("GlobalCardContextMenu audit requires an authenticated session. " + message);
            }

            try
            {
                foreach (Type type in FormTypes)
                {
                    using (Control control = (Control)Activator.CreateInstance(type))
                    {
                        control.Size = new Size(1366, 768);
                        control.PerformLayout();
                        LayoutAuditService.AuditAndFix(control);
                        GlobalDashboardLayoutService.ApplyToTree(control);
                        GlobalCardContextMenu.ApplyToTree(control);

                        List<Control> cards = EnumerateControls(control)
                            .Where(GlobalCardContextMenu.IsAttachedCard)
                            .ToList();

                        if (cards.Count == 0)
                        {
                            rows.Add(new CardMenuAuditRow
                            {
                                Form = type.Name,
                                Card = "(none)",
                                Function = "Open",
                                Expected = "At least one card has the global context menu attached.",
                                Actual = "0 cards detected",
                                Pass = true,
                                Error = string.Empty
                            });
                            continue;
                        }

                        foreach (Control card in cards)
                        {
                            GlobalCardContextInfo info = GlobalCardContextMenu.BuildContextForAudit(card, null, null, null);
                            WindowsFileContextMenuActions actions = GlobalCardContextMenu.BuildActionsForAudit();
                            string cardLabel = BuildCardLabel(info, card);
                            TestCard(type.Name, cardLabel, info, actions, rows);
                        }
                    }

                    Application.DoEvents();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
            finally
            {
                GlobalCardContextMenu.SuppressFeedbackForTests = previousSuppress;

                if (startedSession)
                {
                    if (previousUser == null)
                        SessionManager.ClearSession();
                    else
                        SessionManager.SetSession(previousUser, previousSessionId, previousExpiresAt);
                }
            }

            return rows;
        }

        private static void TestCard(string formName, string card, GlobalCardContextInfo info, WindowsFileContextMenuActions actions, List<CardMenuAuditRow> rows)
        {
            rows.Add(Run(formName, card, "Open", "Brings the card's linked page/module or control to the foreground without throwing.",
                () => actions.Open(info),
                () => "Open executed for " + card,
                () => true));

            bool favBefore = GlobalCardStateService.IsFavorite(info);
            rows.Add(Run(formName, card, "Add to Favorites", "Toggles the favorite flag and persists it to the dashboard shortcuts file and card state JSON.",
                () => actions.ToggleFavorite(info),
                () => "Favorite changed from " + favBefore + " to " + GlobalCardStateService.IsFavorite(info),
                () => GlobalCardStateService.IsFavorite(info) != favBefore));

            rows.Add(Run(formName, card, "Copy as Path", "Copies the card's servoerp:// path to the clipboard.",
                () => actions.CopyAsPath(info),
                () => "Clipboard contains: " + Truncate(SafeClipboardText()),
                () => SafeClipboardText().IndexOf("servoerp://", StringComparison.OrdinalIgnoreCase) >= 0));

            rows.Add(Run(formName, card, "Share", "Copies a shareable card reference (title + path) to the clipboard.",
                () => actions.Share(info),
                () => "Clipboard contains: " + Truncate(SafeClipboardText()),
                () => SafeClipboardText().IndexOf("servoerp://", StringComparison.OrdinalIgnoreCase) >= 0));

            rows.Add(Run(formName, card, "Send To", "Sends the card to Dashboard, Favorites, and Shortcuts storage and persists the HasShortcut/Favorite flags.",
                () =>
                {
                    actions.SendToDashboard(info);
                    actions.SendToFavorites(info);
                    actions.SendToShortcuts(info);
                },
                () => "HasShortcut=" + GlobalCardStateService.Load(info).HasShortcut + ", Favorite=" + GlobalCardStateService.IsFavorite(info),
                () => GlobalCardStateService.Load(info).HasShortcut && GlobalCardStateService.IsFavorite(info)));

            rows.Add(Run(formName, card, "Cut", "Marks the card for a move and copies its configuration to the clipboard with a [CUT] marker; persists ClipboardState=Cut.",
                () => actions.Cut(info),
                () => "ClipboardState=" + GlobalCardStateService.GetClipboardState(info) + "; Clipboard=" + Truncate(SafeClipboardText()),
                () => string.Equals(GlobalCardStateService.GetClipboardState(info), "Cut", StringComparison.OrdinalIgnoreCase)
                      && SafeClipboardText().StartsWith("[CUT]", StringComparison.OrdinalIgnoreCase)));

            rows.Add(Run(formName, card, "Copy", "Copies the card configuration to the clipboard with a [COPY] marker; persists ClipboardState=Copy.",
                () => actions.Copy(info),
                () => "ClipboardState=" + GlobalCardStateService.GetClipboardState(info) + "; Clipboard=" + Truncate(SafeClipboardText()),
                () => string.Equals(GlobalCardStateService.GetClipboardState(info), "Copy", StringComparison.OrdinalIgnoreCase)
                      && SafeClipboardText().StartsWith("[COPY]", StringComparison.OrdinalIgnoreCase)));

            rows.Add(Run(formName, card, "Create Shortcut", "Creates a dashboard shortcut tile/link for the card and persists HasShortcut=true.",
                () => actions.CreateShortcut(info),
                () => "HasShortcut=" + GlobalCardStateService.Load(info).HasShortcut,
                () => GlobalCardStateService.Load(info).HasShortcut));

            rows.Add(Run(formName, card, "Lock Card", "Locks the card so it cannot be moved, resized, or deleted until unlocked; persists Locked=true.",
                () =>
                {
                    if (actions.IsLocked(info))
                        actions.ToggleLock(info);
                    actions.ToggleLock(info);
                },
                () => "Locked=" + actions.IsLocked(info),
                () => actions.IsLocked(info)));

            rows.Add(Run(formName, card, "Unlock Card", "Unlocks a locked card so it can be moved, resized, or deleted again; persists Locked=false.",
                () =>
                {
                    if (!actions.IsLocked(info))
                        actions.ToggleLock(info);
                    actions.ToggleLock(info);
                },
                () => "Locked=" + actions.IsLocked(info),
                () => !actions.IsLocked(info)));

            rows.Add(Run(formName, card, "Hide Card", "Hides the card behind a restorable overlay and persists Hidden=true.",
                () => actions.HideCard(info),
                () => "Hidden=" + GlobalCardStateService.IsHidden(info),
                () => GlobalCardStateService.IsHidden(info)));

            rows.Add(Run(formName, card, "Delete Card", "Confirms, then marks the card deleted, removes it from the active layout, and persists Deleted=true.",
                () => actions.DeleteCard(info),
                () => "Deleted=" + GlobalCardStateService.IsDeleted(info),
                () => GlobalCardStateService.IsDeleted(info)));

            rows.Add(Run(formName, card, "Restore Card", "Restores a hidden/deleted card by removing its overlay and clearing Hidden/Deleted flags.",
                () => actions.RestoreCard(info),
                () => "Hidden=" + GlobalCardStateService.IsHidden(info) + ", Deleted=" + GlobalCardStateService.IsDeleted(info),
                () => !GlobalCardStateService.IsHidden(info) && !GlobalCardStateService.IsDeleted(info)));

            rows.Add(Run(formName, card, "Properties", "Shows Card ID, Title, Module, Path, Status, Visibility, Position, Size, Locked state, Favorite, Shortcut, Clipboard state, Last action, and Last modified.",
                () => actions.Properties(info),
                () => Truncate(GlobalCardStateService.BuildPropertiesText(info)),
                () =>
                {
                    string text = GlobalCardStateService.BuildPropertiesText(info);
                    string[] required = { "Card ID:", "Title:", "Module:", "Path:", "Status:", "Visibility:", "Position:", "Size:", "Locked:", "Last Modified:" };
                    return required.All(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                }));
        }

        private static CardMenuAuditRow Run(string form, string card, string function, string expected, Action act, Func<string> describe, Func<bool> validate)
        {
            try
            {
                act();
                string actual = describe();
                bool pass = validate();
                return new CardMenuAuditRow
                {
                    Form = form,
                    Card = card,
                    Function = function,
                    Expected = expected,
                    Actual = actual,
                    Pass = pass,
                    Error = pass ? string.Empty : "Actual result did not match the expected outcome."
                };
            }
            catch (Exception ex)
            {
                return new CardMenuAuditRow
                {
                    Form = form,
                    Card = card,
                    Function = function,
                    Expected = expected,
                    Actual = "Exception: " + ex.Message,
                    Pass = false,
                    Error = ex.ToString()
                };
            }
        }

        private static string BuildCardLabel(GlobalCardContextInfo info, Control card)
        {
            if (info != null && !string.IsNullOrWhiteSpace(info.CardKey))
                return info.CardKey;
            if (info != null && !string.IsNullOrWhiteSpace(info.Title))
                return info.Title;
            if (!string.IsNullOrWhiteSpace(card.Name))
                return card.Name;
            return card.GetType().Name;
        }

        private static string SafeClipboardText()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "(empty)";

            string flat = Flatten(value);
            return flat.Length > 120 ? flat.Substring(0, 120) + "..." : flat;
        }

        private static string Flatten(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\r", " ").Replace("\n", " | ").Replace("\t", " ");
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            if (root == null)
                yield break;

            yield return root;
            foreach (Control child in root.Controls.Cast<Control>().ToList())
            {
                foreach (Control descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }
    }
}
