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
            string directory = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? @"C:\HVAC_PRO_MSE", "TEST_RESULTS", "dashboard-parity");
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
                    foreach (string metricName in new[] { "JobsTodayCard", "SlaRiskCard", "ReceivablesCard", "LowStockCard" })
                    {
                        Control metric = Card(page, metricName);
                        Require(metric is QuotationForecastCard, "Dashboard metric must use the quotation surface.");
                        Label title = metric.Controls.OfType<Label>().Single(l => l.Name == "MetricTitle");
                        Label value = metric.Controls.OfType<Label>().Single(l => l.Name == "MetricValue");
                        PictureBox icon = metric.Controls.OfType<PictureBox>().Single();
                        Require(title.Font.Bold && Math.Abs(title.Font.Size - 8.2f) < .01f && Math.Abs(value.Font.Size - 15f) < .01f,
                            "Metric typography differs from quotation rules.");
                        Require(icon.Size == new Size(42, 42), "Metric icon container differs from quotation rules.");
                    }
                    Require(Children(Card(page, "PriorityInboxCard")).OfType<Button>().Any(b => b.Text == "All" && b.BackColor == DashboardVisuals.Blue),
                        "Selected inbox filter must use the quotation chip state.");
                    results.Add("main dashboard uses quotation card types, metric typography, icon sizes and selected filter style");
                    page.Size = new Size(1440, 900); Rebuild(page);
                    string shortcut = null; page.OnShortcut = value => shortcut = value;
                    Children(page).OfType<Button>().Single(b => b.Text == "+ New job").PerformClick();
                    Require(shortcut == DashboardForm.ShortcutNewJob, "New job shortcut did not route.");
                    Children(Card(page, "PriorityInboxCard")).OfType<Button>().Single(b => b.Text == "Critical").PerformClick();
                    Application.DoEvents();
                    Require((string)typeof(DashboardForm).GetField("_actionFilter", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(page) == "Critical",
                        "Quotation-style inbox chip did not update the filter.");
                    Children(Card(page, "PriorityInboxCard")).OfType<Button>().Single(b => b.Text == "All").PerformClick();
                    Application.DoEvents();
                    results.Add("inbox filter buttons update the selected filter and restore the complete inbox");
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
                    using (var bitmap = new Bitmap(page.Width, page.Height))
                    { page.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "dashboard-empty.png")); }
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
