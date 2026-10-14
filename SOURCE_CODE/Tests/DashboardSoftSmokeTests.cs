using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class DashboardSoftSmokeTests
    {
        private static IEnumerable<Control> Children(Control root)
        {
            foreach (Control child in root.Controls) { yield return child; foreach (Control descendant in Children(child)) yield return descendant; }
        }
        private static Control Card(Control page, string name) => Children(page).Single(c => c.Name == name);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Rebuild(DashboardForm page) => typeof(DashboardForm).GetMethod("BuildShell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);

        internal static List<string> RunAll()
        {
            var previous = SessionManager.CurrentUser;
            var previousSession = SessionManager.CurrentSessionId;
            var previousExpiry = SessionManager.ExpiresAt;
            var results = new List<string>();
            string directory = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS", "dashboard-soft-1.1.506");
            Directory.CreateDirectory(directory);
            try
            {
                using (var page = new DashboardForm { Size = new Size(1440, 900) })
                {
                    page.LoadActionCenterPreviewForVisualTest(); page.CreateControl();
                    foreach (int width in new[] { 1440, 1000, 760 })
                    {
                        page.Size = new Size(width, 900); Rebuild(page); page.PerformLayout(); Application.DoEvents();
                        foreach (Button button in Children(page).OfType<Button>())
                            Require(button.Right <= button.Parent.ClientSize.Width && button.Bottom <= button.Parent.ClientSize.Height,
                                "Button clipped at width " + width + ": " + button.Text);
                        foreach (string name in new[] { "JobsTodayCard", "SlaRiskCard", "ReceivablesCard", "LowStockCard", "PriorityInboxCard", "TeamTodayCard", "ComingUpCard", "TodayJobsCard", "CollectionsCard", "RecentActivityCard" })
                            Require(!string.IsNullOrWhiteSpace(Card(page, name).AccessibleDescription), "Missing preview: " + name);
                        using (var bitmap = new Bitmap(width, 900)) { page.DrawToBitmap(bitmap, new Rectangle(0, 0, width, 900)); bitmap.Save(Path.Combine(directory, "dashboard-" + width + ".png")); }
                    }
                    results.Add("responsive layouts at 1440, 1000 and 760 pixels keep action buttons inside their containers");
                    results.Add("all ten operational cards expose descriptive live-record previews");
                    page.Size = new Size(1440, 900); Rebuild(page);
                    string shortcut = null; page.OnShortcut = value => shortcut = value;
                    Children(page).OfType<Button>().Single(b => b.Text == "+ New job").PerformClick();
                    Require(shortcut == DashboardForm.ShortcutNewJob, "New job shortcut did not route.");
                    var pager = Children(page).OfType<UI.Controls.GlobalPaginationControl>().Single();
                    pager.GoToPage(2); Application.DoEvents();
                    Require(Children(page).OfType<UI.Controls.GlobalPaginationControl>().Single().CurrentPage == 2, "Inbox next page did not persist.");
                    Require(Children(Card(page, "PriorityInboxCard")).OfType<Label>().Any(l => l.Text.IndexOf("invoice", StringComparison.OrdinalIgnoreCase) >= 0), "Second page did not reveal later work.");
                    results.Add("priority pagination reveals later work and persists the selected page");
                    typeof(DashboardForm).GetField("_actionFilter", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(page, "SLA risk"); Rebuild(page);
                    Require(Card(page, "PriorityInboxCard").AccessibleDescription.Contains("INC-2026-0091"), "SLA filter lost the urgent incident.");
                    results.Add("new-job shortcut routes through the existing callback and SLA drilldown includes urgent incidents");
                    var user = SessionManager.CurrentUser;
                    user.Permissions = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase) {
                        { "WorkOrders", new RolePermissionDto { CanView = true, CanCreate = false } } };
                    Rebuild(page);
                    Require(!Card(page, "ReceivablesCard").AccessibleDescription.Contains("Orion"), "Restricted invoice leaked in preview.");
                    Require(!Card(page, "LowStockCard").AccessibleDescription.Contains("Compressor"), "Restricted stock leaked in preview.");
                    Require(!Children(page).OfType<Button>().Any(b => b.Text == "+ New job"), "Create action ignored role permission.");
                    results.Add("restricted modules do not leak records through previews and create shortcuts honor role permissions");
                    user.RoleName = "Technician"; user.EmployeeId = 7; Rebuild(page);
                    Require(!Card(page, "JobsTodayCard").AccessibleDescription.Contains("JOB-1042"), "Technician preview leaked an unassigned job.");
                    results.Add("technician job previews contain only their own assigned jobs");
                    user.RoleName = "Manager"; user.Permissions.Clear();
                    foreach (string field in new[] { "_jobs", "_invoices", "_payments", "_inventory", "_quotations", "_purchaseOrders", "_serviceTickets", "_actionContracts" })
                        ((System.Collections.IList)typeof(DashboardForm).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page)).Clear();
                    typeof(DashboardForm).GetField("_actionInput", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(page, new ActionCenterInput());
                    Rebuild(page);
                    Require(Card(page, "PriorityInboxCard").AccessibleDescription.Contains("No matching records"), "Empty dashboard invented actionable work.");
                    Require(Card(page, "JobsTodayCard").AccessibleDescription.Contains("0 matching jobs"), "Empty dashboard invented scheduled jobs.");
                    results.Add("empty business data remains empty with clear card preview states");
                }
            }
            finally { SessionManager.SetSession(previous, previousSession, previousExpiry); }
            File.WriteAllLines(Path.Combine(directory, "smoke.txt"), results.Select(r => "PASS " + r));
            return results;
        }

        internal static void RunPreview()
        {
            using (var originalShell = new MainForm())
            using (var host = new Form { Text = "ServoERP - Soft Dashboard QA", WindowState = FormWindowState.Maximized, StartPosition = FormStartPosition.CenterScreen, Size = new Size(1920, 1080) })
            using (var page = new DashboardForm { Dock = DockStyle.Fill })
            {
                page.LoadActionCenterPreviewForVisualTest();
                // Reuse the actual sidebar control; no copied or rewritten sidebar markup.
                var sidebar = (Control)typeof(MainForm).GetField("_sidebar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(originalShell);
                originalShell.Controls.Remove(sidebar);
                host.Controls.Add(page); host.Controls.Add(sidebar);
                host.Shown += (s, e) =>
                {
                    var timer = new Timer { Interval = 1000 };
                    timer.Tick += (sender, args) =>
                    {
                        timer.Stop(); timer.Dispose();
                        string directory = @"C:\HVAC_PRO_MSE\TEST_RESULTS\dashboard-soft-1.1.506";
                        Directory.CreateDirectory(directory);
                        using (var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height))
                        { host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "full-dashboard.png")); }
                        File.WriteAllLines(Path.Combine(directory, "hosted-buttons.txt"), Children(page).OfType<Button>().Select(b => b.Text + " " + b.Bounds + " visible=" + b.Visible + " parent=" + b.Parent.ClientSize));
                    };
                    timer.Start();
                };
                Application.Run(host);
            }
        }
    }
}
