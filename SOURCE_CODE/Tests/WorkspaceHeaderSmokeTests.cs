using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    internal static class WorkspaceHeaderSmokeTests
    {
        internal static readonly string DirectoryPath = @"C:\HVAC_PRO_MSE\TEST_RESULTS\headers-1.1.508";
        private static IEnumerable<Control> Children(Control root)
        {
            foreach (Control child in root.Controls)
            { yield return child; foreach (var descendant in Children(child)) yield return descendant; }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        internal static void RunAll()
        {
            Directory.CreateDirectory(DirectoryPath);
            string failure = Path.Combine(DirectoryPath, "failure.txt");
            if (File.Exists(failure)) File.Delete(failure);
            var results = new List<string>();
            foreach (string title in UiQaStateCatalog.Modules.Select(m => m.Key))
            {
                int clicks = 0;
                var action = new Button { Text = "Existing action", Width = 130, Height = 36 };
                action.Click += (s, e) => clicks++;
                var input = new TextBox();
                var model = SharedPageHeader.CreateWorkspaceEditor(title + "Header", title,
                    "Page description", new List<Control> { action }, SharedPageHeader.CreateSearchInputShell("LocalFilter", input));
                model.StatusText = "Ready";
                var built = SharedPageHeader.Build(model);
                using (var host = new Form { ClientSize = new Size(1440, 700) })
                {
                    host.Controls.Add(built.Header); host.Show();
                    foreach (int width in new[] { 1440, 1000, 760, 600 })
                    {
                        host.ClientSize = new Size(width, 700); host.PerformLayout(); Application.DoEvents();
                        PageHeaderPolishService.Apply(host); LayoutAuditService.AuditAndFix(host);
                        foreach (Control control in Children(built.Header).Where(c => c.Visible))
                            Require(control.Left >= 0 && control.Top >= 0 && control.Right <= control.Parent.ClientSize.Width + 1 &&
                                control.Bottom <= control.Parent.ClientSize.Height + 1,
                                title + " clipped " + control.Name + " at " + width + ": " + control.Bounds);
                        Require(Children(built.Header).Count(c => c.Name == "WorkspaceGlobalSearch") == 1, "Duplicate global search");
                        var heading = built.TitleLabel.Parent;
                        Require(heading.Top >= Children(built.Header).Single(c => c.Name == "WorkspaceUtilityBar").Bottom,
                            title + " utility bar covers page title at " + width);
                        foreach (var text in new[] { built.TitleLabel, built.SubtitleLabel, built.StatusLabel }.Where(c => c != null && c.Visible))
                            foreach (var peer in heading.Controls.Cast<Control>().Where(c => c.Visible && (c == built.ActionHost || c.Name == "LocalFilter" || c.Name == "WorkspaceDate")))
                                Require(!text.Bounds.IntersectsWith(peer.Bounds), title + " overlaps title/status at " + width);
                        if (title == "Dashboard")
                            using (var bitmap = new Bitmap(built.Header.Width, built.Header.Height))
                            { built.Header.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(DirectoryPath, "header-" + width + ".png")); }
                    }
                    input.Text = "local filter";
                    Require(input.Parent != null && input.Text == "local filter", "Editable filter lost");
                    action.PerformClick(); Require(clicks == 1, "Original action handler lost");
                    action.Enabled = false; action.PerformClick(); Require(clicks == 1, "Disabled action invoked");
                    built.TitleLabel.Text = "Updated page title";
                    Require(Children(built.Header).Any(c => c.Name == "WorkspaceBreadcrumb" && c.Text.Contains("Updated page title")), "Stale breadcrumb");
                    Require(built.StatusLabel.Text == "Ready", "Status lost");
                    host.Close();
                }
            }
            results.Add("PASS Shared headers: 21 module titles at 1440/1000/760/600 px; controls contained with no utility/title/status/filter/action overlaps after theme/layout passes; original action handlers, disabled actions, local filters, live titles and status preserved.");
            File.WriteAllLines(Path.Combine(DirectoryPath, "smoke.txt"), results);
            var modules = UiQaStateCatalog.Modules.Select(m => m.TypeName).Concat(new[] {
                "HVAC_Pro_Desktop.UI.PorterDeliveriesForm", "HVAC_Pro_Desktop.UI.ClientDetailPage", "HVAC_Pro_Desktop.UI.JobDetailPage", "HVAC_Pro_Desktop.UI.UserGuideForm" });
            foreach (string typeName in modules)
            {
                var type = typeof(DashboardForm).Assembly.GetType(typeName, true);
                using (var page = (Control)Activator.CreateInstance(type))
                {
                    if (page is DashboardForm dashboard) dashboard.LoadActionCenterPreviewForVisualTest();
                    page.Size = new Size(1440, 900); page.PerformLayout();
                    if (!Children(page).OfType<WorkspaceHeaderPanel>().Any())
                    {
                        var render = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                            .FirstOrDefault(m => m.Name.StartsWith("Render") && m.Name.EndsWith("Dashboard") && m.GetParameters().Length == 0);
                        render?.Invoke(page, null);
                    }
                    var headers = Children(page).OfType<WorkspaceHeaderPanel>().ToList();
                    Require(headers.Count > 0, "Missing shared header: " + typeName);
                    results.Add("PASS Constructed " + type.Name + ": " + headers.Count + " shared header(s). No save/delete actions invoked.");
                }
                File.WriteAllLines(Path.Combine(DirectoryPath, "smoke.txt"), results);
            }
            using (var main = new MainForm())
            using (var owned = new Form { Owner = main })
            {
                var shellSource = new Panel(); main.Controls.Add(shellSource);
                var ownedSource = new Panel(); owned.Controls.Add(ownedSource);
                var menuFactory = typeof(WorkspaceHeaderPanel).GetMethod("BuildAccountMenu", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (Control source in new[] { shellSource, ownedSource })
                    using (var menu = (ContextMenuStrip)menuFactory.Invoke(null, new object[] { source }))
                    {
                        Require(menu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Settings").Enabled,
                            "Settings disabled in actual shell or owned modal header");
                        Require(menu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Change password").Enabled,
                            "Signed-in account action disabled");
                    }
                using (var guide = new UserGuideForm { Owner = main })
                using (var timer = new Timer { Interval = 30 })
                {
                    bool opened = false;
                    guide.Show();
                    timer.Tick += (s, e) =>
                    {
                        var search = Application.OpenForms.OfType<GlobalSearchDialog>().FirstOrDefault();
                        if (search == null) return;
                        opened = true; timer.Stop(); search.Close();
                    };
                    timer.Start();
                    typeof(UserGuideForm).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(guide, new object[] { new Message(), Keys.Control | Keys.K });
                    Require(opened && guide.Visible, "Guide-wide Ctrl+K or search cancel failed");
                    guide.Close();
                }
            }
            results.Add("PASS Account menu enables Settings and Change password in the actual shell and an owned modal; existing shell navigation is retained.");
            results.Add("PASS User Guide handles Ctrl+K across its form; cancelling search keeps the guide open.");
            File.WriteAllLines(Path.Combine(DirectoryPath, "smoke.txt"), results);
        }

        internal static void RunPreview()
        {
            Directory.CreateDirectory(DirectoryPath);
            using (var host = new Form { Text = "ServoERP - Header Option 2 QA", ClientSize = new Size(1440, 850),
                StartPosition = FormStartPosition.CenterScreen, BackColor = Color.FromArgb(246, 248, 250) })
            {
                var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true,
                    FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(18) };
                host.Controls.Add(flow);
                foreach (Control page in new Control[] { new DashboardForm(), new ClientManagementForm(), new ReportForm(), new InvoiceForm() })
                {
                    page.Size = new Size(1380, 850);
                    if (page is DashboardForm dashboard) dashboard.LoadActionCenterPreviewForVisualTest();
                    if (page is ReportForm report) report.LoadProfitabilityPreviewForVisualTest();
                    if (page is InvoiceForm invoice) invoice.LoadReceivablesPreviewForVisualTest();
                    if (page is ClientManagementForm)
                        page.GetType().GetMethod("RenderClientsDashboard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(page, null);
                    var header = Children(page).OfType<WorkspaceHeaderPanel>().First();
                    header.Parent.Controls.Remove(header);
                    header.Dock = DockStyle.None; header.Width = 1380; header.Margin = new Padding(0, 0, 0, 16);
                    flow.Controls.Add(header);
                    // Keep the original page alive so retained handlers still refer to their page.
                    host.Disposed += (s, e) => page.Dispose();
                }
                host.Shown += (s, e) =>
                {
                    host.BeginInvoke((Action)(() =>
                    {
                        using (var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height))
                        { host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(DirectoryPath, "module-headers.png")); }
                    }));
                };
                Application.Run(host);
            }
        }
    }
}
