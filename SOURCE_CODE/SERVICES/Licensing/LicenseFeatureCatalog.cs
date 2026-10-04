using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public static class LicenseFeatureCatalog
    {
        private static readonly string[] AllModules =
        {
            "Dashboard", "Clients", "Contracts", "Invoices", "Payments", "Quotations", "Reports",
            "Settings", "Vendors", "Purchases", "Inventory", "Employees", "Attendance", "Payroll",
            "GeoIntelligence", "WorkOrders", "ServiceDesk", "MasterData"
        };

        public static List<string> GetModulesForPlan(LicensePlanType plan)
        {
            if (plan == LicensePlanType.Trial)
            {
                return new List<string>
                {
                    "Dashboard", "Clients", "Quotations", "Invoices", "Reports", "Settings", "GeoIntelligence", "MasterData"
                };
            }

            if (plan == LicensePlanType.Basic || plan == LicensePlanType.Standard)
            {
                return new List<string>
                {
                    "Dashboard", "Clients", "Contracts", "Invoices", "Payments", "Quotations", "Reports",
                    "Settings", "Vendors", "Purchases", "Inventory", "Employees", "Attendance", "GeoIntelligence", "WorkOrders", "ServiceDesk", "MasterData"
                };
            }

            if (plan == LicensePlanType.Pro)
                return new List<string>(AllModules);

            return new List<string>(AllModules);
        }

        public static bool IsModuleEnabled(LicenseSnapshot snapshot, string moduleKey)
        {
            if (snapshot == null)
                return string.Equals(moduleKey, "Settings", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(moduleKey, "Settings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleKey, "Reports", StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleKey, "Dashboard", StringComparison.OrdinalIgnoreCase))
                return true;

            List<string> modules = snapshot.EnabledModules == null || snapshot.EnabledModules.Count == 0
                ? GetModulesForPlan(snapshot.PlanType)
                : snapshot.EnabledModules;

            if (string.Equals(moduleKey, "Attendance", StringComparison.OrdinalIgnoreCase))
            {
                return modules.Exists(m => string.Equals(m, "Attendance", StringComparison.OrdinalIgnoreCase))
                    || modules.Exists(m => string.Equals(m, "Payroll", StringComparison.OrdinalIgnoreCase))
                    || modules.Exists(m => string.Equals(m, "Employees", StringComparison.OrdinalIgnoreCase));
            }

            // Site Monitor is a core operational workspace for every activated license.
            // Keep this compatibility rule independent of cached entitlement lists so
            // older client PCs receive the page immediately after updating.
            if (string.Equals(moduleKey, "GeoIntelligence", StringComparison.OrdinalIgnoreCase))
                return snapshot.Status != LicenseStatus.Missing && snapshot.Status != LicenseStatus.Tampered;

            return modules.Exists(m => string.Equals(m, moduleKey, StringComparison.OrdinalIgnoreCase));
        }
    }
}
