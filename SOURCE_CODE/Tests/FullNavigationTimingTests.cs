using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class FullNavigationTimingTests
    {
        private const int PageLoadTimeoutMs = 30000;

        public static string WriteReport()
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "navigation-timing-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

            var lines = new List<string>
            {
                "ServoERP Navigation Timing Report",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ""
            };
            File.WriteAllLines(path, lines);

            try
            {
                foreach (string line in RunAll())
                {
                    lines.Add(line);
                    File.WriteAllLines(path, lines);
                }
            }
            catch (Exception ex)
            {
                lines.Add("FAIL Navigation timing aborted: " + ex);
                File.WriteAllLines(path, lines);
            }

            File.WriteAllLines(path, lines);
            return path;
        }

        public static IEnumerable<string> RunAll()
        {
            if (!SessionManager.IsLoggedIn && !LocalLoginBypassService.TryStartSession(out string loginMessage))
                throw new InvalidOperationException("Navigation timing needs the authorized local login bypass. " + loginMessage);

            using (var form = new MainForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(40, 40);
                form.Size = new Size(1440, 860);
                form.Show();
                PumpUi(750);

                foreach (NavSpec spec in NavSpec.All().OrderByDescending(p => p.Index == 8).ThenBy(p => p.Index))
                {
                    foreach (string line in TimePage(form, spec))
                        yield return line;
                }

                form.Close();
            }
        }

        private static IEnumerable<string> TimePage(MainForm form, NavSpec spec)
        {
            yield return "START " + spec.Label;
            Stopwatch sw = Stopwatch.StartNew();
            form.NavigateTo(spec.Index);
            yield return "TRACE " + spec.Label + " NavigateTo returned in " + sw.ElapsedMilliseconds + " ms";
            Control page = WaitForCurrentPage(form, spec, PageLoadTimeoutMs);
            sw.Stop();

            if (page == null)
            {
                yield return "FAIL " + spec.Label + " did not open within " + PageLoadTimeoutMs + " ms";
                yield break;
            }

            string loadError = FindStatusError(page);
            if (!string.IsNullOrWhiteSpace(loadError))
            {
                yield return "FAIL " + spec.Label + " opened in " + sw.ElapsedMilliseconds + " ms but reported: " + loadError;
                yield break;
            }

            yield return "PASS " + spec.Label + " opened completely in " + sw.ElapsedMilliseconds + " ms";

            foreach (string tabLine in TimeTabs(page, spec.Label))
                yield return tabLine;

            foreach (string clickLine in TimeSafeClicks(page, spec.Label))
                yield return clickLine;
        }

        private static Control WaitForCurrentPage(MainForm form, NavSpec spec, int timeoutMs)
        {
            Stopwatch wait = Stopwatch.StartNew();
            Control page = null;
            while (wait.ElapsedMilliseconds < timeoutMs)
            {
                page = GetVisibleContentPage(form, spec);
                if (page != null)
                {
                    bool expected = spec.ControlType == null || spec.ControlType.IsInstanceOfType(page);
                    bool loaded = !(page is DeferredPageControl deferred) ||
                                  !deferred.HasDeferredLoad ||
                                  (deferred.DeferredLoadCompleted && !deferred.DeferredLoadQueued);
                    if (expected && loaded)
                    {
                        page.PerformLayout();
                        PumpUi(250);
                        return page;
                    }
                }

                PumpUi(100);
            }

            return page;
        }

        private static Control GetVisibleContentPage(MainForm form, NavSpec spec)
        {
            FieldInfo field = typeof(MainForm).GetField("_content", BindingFlags.Instance | BindingFlags.NonPublic);
            Panel content = field == null ? null : field.GetValue(form) as Panel;
            if (content == null)
                return null;

            Control expectedPage = content.Controls
                .Cast<Control>()
                .FirstOrDefault(control => control.Visible && (spec.ControlType == null || spec.ControlType.IsInstanceOfType(control)));
            if (expectedPage != null)
                return expectedPage;

            return content.Controls
                .Cast<Control>()
                .FirstOrDefault(control => control.Visible && control.Dock == DockStyle.Fill);
        }

        private static IEnumerable<string> TimeTabs(Control page, string pageLabel)
        {
            foreach (TabControl tabs in EnumerateControls(page).OfType<TabControl>())
            {
                for (int i = 0; i < tabs.TabPages.Count; i++)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    tabs.SelectedIndex = i;
                    tabs.PerformLayout();
                    PumpUi(250);
                    sw.Stop();

                    string tabError = FindStatusError(page);
                    string label = pageLabel + " > " + tabs.TabPages[i].Text;
                    if (!string.IsNullOrWhiteSpace(tabError))
                        yield return "FAIL " + label + " selected in " + sw.ElapsedMilliseconds + " ms but reported: " + tabError;
                    else
                        yield return "PASS " + label + " selected in " + sw.ElapsedMilliseconds + " ms";
                }
            }
        }

        private static IEnumerable<string> TimeSafeClicks(Control page, string pageLabel)
        {
            var results = new List<string>();
            foreach (Button button in EnumerateControls(page).OfType<Button>().Where(IsSafeClickButton).Take(16))
            {
                string label = pageLabel + " > " + Clean(button.Text);
                Stopwatch sw = Stopwatch.StartNew();
                try
                {
                    button.PerformClick();
                    PumpUi(300);
                    sw.Stop();
                    results.Add("PASS " + label + " safe click completed in " + sw.ElapsedMilliseconds + " ms");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add("FAIL " + label + " safe click failed in " + sw.ElapsedMilliseconds + " ms: " + ex.Message);
                }
            }

            return results;
        }

        private static string FindStatusError(Control root)
        {
            foreach (Label label in EnumerateControls(root).OfType<Label>())
            {
                string text = (label.Text ?? string.Empty).Trim();
                if (text.StartsWith("Load error:", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
                    return text;
            }

            return string.Empty;
        }

        private static bool IsSafeClickButton(Button button)
        {
            if (button == null || !button.Visible || !button.Enabled)
                return false;

            string key = Clean(string.IsNullOrWhiteSpace(button.Text) ? button.Name : button.Text);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string[] unsafeWords =
            {
                "SAVE", "DELETE", "REMOVE", "ARCHIVE", "MERGE", "IMPORT", "UPLOAD", "EXPORT", "DOWNLOAD",
                "PAY", "PAYMENT", "RUN", "PROCESS", "RESTORE", "FRESH", "BACKUP", "ACTIVATE", "LICENSE",
                "EMAIL", "WHATSAPP", "PRINT", "PREVIEW", "SEND", "SYNC", "CREATE", "ADD", "NEW", "RECORD",
                "INSTALL", "UPDATE", "LOCATE", "OPEN"
            };
            if (unsafeWords.Any(word => key.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                return false;

            string[] safeExact =
            {
                "ALL", "ACTIVE", "PENDING", "DRAFT", "PAID", "UNPAID", "CLOSED",
                "DASHBOARD", "OVERVIEW", "BACK", "BACKTODASHBOARD", "BACKTOOVERVIEW",
                "CLEAR", "RESETFILTERS", "REFRESH", "TODAY", "THISMONTH"
            };

            return safeExact.Any(safe => string.Equals(key, safe, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<Control> EnumerateControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control descendant in EnumerateControls(child))
                    yield return descendant;
            }
        }

        private static string Clean(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static void PumpUi(int milliseconds)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            while (sw.ElapsedMilliseconds < milliseconds);
        }

        private sealed class NavSpec
        {
            private NavSpec(int index, string label, Type controlType)
            {
                Index = index;
                Label = label;
                ControlType = controlType;
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
            public Type ControlType { get; private set; }

            public static IEnumerable<NavSpec> All()
            {
                yield return new NavSpec(0, "Dashboard", typeof(DashboardForm));
                yield return new NavSpec(1, "Clients", typeof(ClientManagementForm));
                yield return new NavSpec(2, "Contracts", typeof(ContractManagementForm));
                yield return new NavSpec(3, "Invoices", typeof(InvoiceForm));
                yield return new NavSpec(4, "Payments", typeof(PaymentForm));
                yield return new NavSpec(5, "SLA Dashboard", typeof(SLADashboardForm));
                yield return new NavSpec(6, "Quotations", typeof(TenderBidForm));
                yield return new NavSpec(7, "Reports", typeof(ReportForm));
                yield return new NavSpec(8, "Settings", typeof(SettingsForm));
                yield return new NavSpec(9, "Suppliers", typeof(VendorForm));
                yield return new NavSpec(10, "Purchases", typeof(PurchaseForm));
                yield return new NavSpec(11, "Inventory", typeof(InventoryForm));
                yield return new NavSpec(12, "Employees", typeof(EmployeeForm));
                yield return new NavSpec(13, "Payroll", typeof(PayrollForm));
                yield return new NavSpec(14, "Dispatch Center", typeof(GeoIntelligenceForm));
                yield return new NavSpec(15, "Jobs", typeof(JobManagementForm));
                yield return new NavSpec(17, "Master Data", typeof(MasterDataForm));
                yield return new NavSpec(18, "WhatsApp Hub", typeof(WhatsAppHubForm));
                yield return new NavSpec(19, "Tally", typeof(TallyIntegrationForm));
                yield return new NavSpec(20, "Vendors", typeof(VendorForm));
                yield return new NavSpec(21, "AMC", typeof(AMCPage));
            }
        }
    }
}
