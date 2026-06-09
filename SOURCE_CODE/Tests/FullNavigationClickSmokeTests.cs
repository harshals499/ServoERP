using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class FullNavigationClickSmokeTests
    {
        public static List<string> RunAll()
        {
            var results = new List<string>();
            foreach (PageSpec page in PageSpec.AllPages())
                results.Add(TestPage(page));

            foreach (PageSpec page in PageSpec.DirectDetailPages())
                results.Add(TestPage(page));

            results.Add(TestClientAddressSaveRefresh());
            return results;
        }

        private static string TestClientAddressSaveRefresh()
        {
            using (var clients = new ClientManagementForm())
            {
                clients.Size = new Size(1366, 768);
                clients.CreateControl();
                clients.PerformLayout();
                MethodInfo method = typeof(ClientManagementForm).GetMethod("RefreshAfterClientEditorSave", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                    throw new InvalidOperationException("Client save refresh helper is missing.");

                method.Invoke(clients, new object[]
                {
                    new B2BClient
                    {
                        ClientID = 999999,
                        CompanyName = "Deccan Chemicals Test",
                        PrimaryContact = "02646-661091",
                        Phone = "6356700564",
                        Email = "arun.nair@deccanchemicals.com",
                        BillingAddress = "GIDC Industrial Estate, Ankleshwar, Bharuch, Gujarat, INDIA.",
                        City = "Ankleshwar",
                        GeocodeAddress = "GIDC Industrial Estate, Ankleshwar, Gujarat",
                        IndustryType = "Commercial HVAC",
                        RelationshipStage = "Prospect",
                        CustomerSince = DateTime.Today,
                        IsActive = true
                    }
                });
                Application.DoEvents();
                return "client address save refresh tested from dashboard without screen-action error";
            }
        }

        private static string TestPage(PageSpec page)
        {
            using (Control control = (Control)Activator.CreateInstance(page.ControlType))
            {
                control.Name = page.Key;
                control.Size = new Size(1366, 768);
                control.CreateControl();
                control.PerformLayout();
                Application.DoEvents();

                int tabClicks = ClickAllTabs(control);
                int safeClicks = ClickSafeControls(control);
                control.Size = new Size(1920, 1080);
                control.PerformLayout();
                Application.DoEvents();

                return "navigation click tested " + page.IndexLabel + " " + page.Key + " -> " + page.ControlType.Name +
                       " (tabs=" + tabClicks + ", safeClicks=" + safeClicks + ")" +
                       (page.RouteRetired ? " [retired route form-only check]" : string.Empty);
            }
        }

        private static int ClickAllTabs(Control root)
        {
            int count = 0;
            foreach (TabControl tabs in EnumerateControls(root).OfType<TabControl>())
            {
                for (int i = 0; i < tabs.TabPages.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    tabs.PerformLayout();
                    Application.DoEvents();
                    count++;
                }
            }

            return count;
        }

        private static int ClickSafeControls(Control root)
        {
            int count = 0;
            foreach (Button button in EnumerateControls(root).OfType<Button>().Where(IsSafeClickButton).Take(12))
            {
                button.PerformClick();
                Application.DoEvents();
                count++;
            }

            return count;
        }

        private static bool IsSafeClickButton(Button button)
        {
            if (button == null || !button.Visible || !button.Enabled)
                return false;

            string text = Clean(button.Text);
            string name = Clean(button.Name);
            string key = string.IsNullOrWhiteSpace(text) ? name : text;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string[] unsafeWords =
            {
                "SAVE", "DELETE", "REMOVE", "ARCHIVE", "MERGE", "IMPORT", "UPLOAD", "EXPORT", "DOWNLOAD",
                "PAY", "PAYMENT", "RUN", "PROCESS", "RESTORE", "FRESH", "BACKUP", "ACTIVATE", "LICENSE",
                "EMAIL", "WHATSAPP", "PRINT", "PREVIEW", "SEND", "SYNC", "CREATE", "ADD", "NEW", "RECORD"
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

        private static string Clean(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
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

        private sealed class PageSpec
        {
            private PageSpec(int index, string key, Type controlType, bool routeRetired)
            {
                Index = index;
                Key = key;
                ControlType = controlType;
                RouteRetired = routeRetired;
            }

            public int Index { get; private set; }
            public string Key { get; private set; }
            public Type ControlType { get; private set; }
            public bool RouteRetired { get; private set; }
            public string IndexLabel => Index >= 0 ? "#" + Index : "direct";

            public static IEnumerable<PageSpec> AllPages()
            {
                yield return new PageSpec(0, "Dashboard", typeof(DashboardForm), false);
                yield return new PageSpec(1, "Clients", typeof(ClientManagementForm), false);
                yield return new PageSpec(2, "Contracts", typeof(ContractManagementForm), false);
                yield return new PageSpec(3, "Invoices", typeof(InvoiceForm), false);
                yield return new PageSpec(4, "Payments", typeof(PaymentForm), false);
                yield return new PageSpec(5, "SLA Dashboard", typeof(SLADashboardForm), false);
                yield return new PageSpec(6, "Quotations", typeof(TenderBidForm), false);
                yield return new PageSpec(7, "Reports", typeof(ReportForm), false);
                yield return new PageSpec(8, "Settings", typeof(SettingsForm), false);
                yield return new PageSpec(9, "Vendors", typeof(VendorForm), false);
                yield return new PageSpec(10, "Purchases", typeof(PurchaseForm), false);
                yield return new PageSpec(11, "Inventory", typeof(InventoryForm), false);
                yield return new PageSpec(12, "Employees", typeof(EmployeeForm), false);
                yield return new PageSpec(13, "Payroll", typeof(PayrollForm), false);
                yield return new PageSpec(14, "Dispatch Center", typeof(GeoIntelligenceForm), false);
                yield return new PageSpec(15, "Jobs", typeof(JobManagementForm), false);
                yield return new PageSpec(16, "Retired Service Desk", typeof(ServiceDeskForm), true);
                yield return new PageSpec(17, "Master Data", typeof(MasterDataForm), false);
                yield return new PageSpec(18, "WhatsApp Hub", typeof(WhatsAppHubForm), false);
                yield return new PageSpec(19, "Tally", typeof(TallyIntegrationForm), false);
                yield return new PageSpec(20, "AMC", typeof(AMCPage), false);
            }

            public static IEnumerable<PageSpec> DirectDetailPages()
            {
                yield return new PageSpec(-1, "Client Detail", typeof(ClientDetailPage), false);
                yield return new PageSpec(-1, "Job Detail", typeof(JobDetailPage), false);
            }
        }
    }
}
