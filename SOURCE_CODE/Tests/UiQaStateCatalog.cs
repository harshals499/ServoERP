using System;
using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Tests
{
    public sealed class UiQaModule
    {
        public UiQaModule(string key, string typeName, int pageIndex, bool hasReference)
        {
            Key = key;
            TypeName = typeName;
            PageIndex = pageIndex;
            HasReference = hasReference;
        }

        public string Key { get; private set; }
        public string TypeName { get; private set; }
        public int PageIndex { get; private set; }
        public bool HasReference { get; private set; }
    }

    public sealed class UiQaState
    {
        public UiQaState(string key)
        {
            Key = key;
        }

        public string Key { get; private set; }
    }

    public static class UiQaStateCatalog
    {
        public static readonly string[] RequiredStateKeys =
        {
            "empty",
            "populated",
            "long-text",
            "modal",
            "scrolled"
        };

        public static readonly UiQaModule[] Modules =
        {
            new UiQaModule("Dashboard", "HVAC_Pro_Desktop.UI.DashboardForm", 0, false),
            new UiQaModule("Clients", "HVAC_Pro_Desktop.UI.ClientManagementForm", 1, true),
            new UiQaModule("Contracts", "HVAC_Pro_Desktop.UI.ContractManagementForm", 2, true),
            new UiQaModule("Invoices", "HVAC_Pro_Desktop.UI.InvoiceForm", 3, true),
            new UiQaModule("Payments", "HVAC_Pro_Desktop.UI.PaymentForm", 4, true),
            new UiQaModule("Quotations", "HVAC_Pro_Desktop.UI.TenderBidForm", 6, false),
            new UiQaModule("Reports", "HVAC_Pro_Desktop.UI.ReportForm", 7, true),
            new UiQaModule("Settings", "HVAC_Pro_Desktop.UI.SettingsForm", 8, true),
            new UiQaModule("Vendors", "HVAC_Pro_Desktop.UI.VendorForm", 9, true),
            new UiQaModule("Purchases", "HVAC_Pro_Desktop.UI.PurchaseForm", 10, true),
            new UiQaModule("Inventory", "HVAC_Pro_Desktop.UI.InventoryForm", 11, true),
            new UiQaModule("Employees", "HVAC_Pro_Desktop.UI.EmployeeForm", 12, false),
            new UiQaModule("Payroll", "HVAC_Pro_Desktop.UI.PayrollForm", 13, true),
            new UiQaModule("GeoIntelligence", "HVAC_Pro_Desktop.UI.GeoIntelligenceForm", 14, false),
            new UiQaModule("Jobs", "HVAC_Pro_Desktop.UI.JobManagementForm", 15, true),
            new UiQaModule("MasterData", "HVAC_Pro_Desktop.UI.MasterDataForm", 17, false),
            new UiQaModule("WhatsAppHub", "HVAC_Pro_Desktop.UI.WhatsAppHubForm", 18, false)
        };

        public static IEnumerable<Tuple<UiQaModule, string>> Matrix()
        {
            foreach (UiQaModule module in Modules)
                foreach (string state in RequiredStateKeys)
                    yield return Tuple.Create(module, state);
        }

        public static UiQaModule FindModule(string key)
        {
            return Modules.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
