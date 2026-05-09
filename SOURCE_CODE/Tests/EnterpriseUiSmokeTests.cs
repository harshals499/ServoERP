using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class EnterpriseUiSmokeTests
    {
        public static List<string> RunAll()
        {
            var results = new List<string>();
            Type[] moduleTypes =
            {
                typeof(DashboardForm),
                typeof(InvoiceForm),
                typeof(ServiceDeskForm),
                typeof(InventoryForm),
                typeof(PurchaseForm),
                typeof(ClientManagementForm),
                typeof(VendorForm),
                typeof(PaymentForm),
                typeof(ContractManagementForm),
                typeof(JobManagementForm),
                typeof(PayrollForm),
                typeof(ReportForm),
                typeof(SettingsForm),
                typeof(MasterDataForm)
            };

            foreach (Type type in moduleTypes)
            {
                using (Control control = (Control)Activator.CreateInstance(type))
                {
                    control.CreateControl();
                    control.Size = new Size(1366, 768);
                    EnsureNoDeadButtons(control, type.Name);
                    results.Add(type.Name + " instantiated and scanned");
                }
            }

            return results;
        }

        public static string WriteReport()
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "enterprise-ui-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var lines = new List<string>();
            lines.Add("ServoERP Enterprise UI Smoke Test");
            lines.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("");
            try
            {
                foreach (string result in RunAll())
                    lines.Add("PASS " + result);
            }
            catch (Exception ex)
            {
                lines.Add("FAIL " + ex);
                File.WriteAllLines(path, lines);
                return path;
            }

            File.WriteAllLines(path, lines);
            return path;
        }

        private static void EnsureNoDeadButtons(Control root, string moduleName)
        {
            foreach (Button button in EnumerateControls(root).OfType<Button>())
            {
                string text = (button.Text ?? string.Empty).Trim();
                if (button.Enabled && !HasClickHandler(button) && !IsContainerButton(button))
                    throw new InvalidOperationException(moduleName + " has an enabled button without a click handler: " + text);
            }
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

        private static bool HasClickHandler(Button button)
        {
            // WinForms keeps event handlers in a private event list. Reflection is used only in this diagnostic test path.
            object eventClick = typeof(Control)
                .GetField("EventClick", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null);
            object events = typeof(Component)
                .GetProperty("Events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(button, null);
            if (eventClick == null || events == null)
                return true;

            Delegate handler = events.GetType()
                .GetProperty("Item", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(events, new[] { eventClick }) as Delegate;
            return handler != null;
        }

        private static bool IsContainerButton(Button button)
        {
            string text = (button.Text ?? string.Empty).Trim();
            return text.Length == 0 || text == "..." || text == "⋮";
        }
    }
}
