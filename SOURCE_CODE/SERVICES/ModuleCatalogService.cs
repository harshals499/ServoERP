using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class ModuleCatalogItem
    {
        public string ModuleName { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public string ClientBenefit { get; set; }
        public string OpenSourceInspiration { get; set; }
        public string NextStep { get; set; }
    }

    public sealed class ModuleCatalogService
    {
        /// <summary>Returns the built-in module catalog and extension-readiness roadmap.</summary>
        public List<ModuleCatalogItem> GetCatalog()
        {
            return new List<ModuleCatalogItem>
            {
                Item("Dashboard", "Core ERP", "Installed", "Daily operational command center.", "ERPNext Desk / Odoo dashboards", "Keep KPI cards business-first for Indian HVAC operators."),
                Item("Clients, Sites & Contacts", "CRM", "Installed", "One source for customer locations and decision makers.", "ERPNext CRM", "Add reusable saved views for sales and AMC teams."),
                Item("Jobs Workflow Board", "Field Service", "Installed", "Visual board for Created, Assigned, In Progress, Checklist Done, Closed, and Invoiced work.", "Kanban service boards", "Use board review in morning dispatch meetings."),
                Item("Inventory & Procurement Catalog", "Materials", "Installed", "Supplier-ordering view of parts, rates, and purchase readiness.", "Odoo Inventory / Purchase", "Add vendor scorecards and reorder packs."),
                Item("GST Invoicing", "Accounting", "Installed", "GST-ready billing with Indian tax fields and PDF output.", "ERPNext Accounts", "Keep tax logic locked and audit-friendly."),
                Item("Backup & Recovery", "Operations", "Installed", "Client-owned network, local, and external-drive database backups.", "Self-hosted ERP backup tools", "Review backup log weekly."),
                Item("Open Source & Licenses", "Compliance", "Installed", "Procurement-ready dependency disclosure.", "Open source notice centers", "Export disclosures during enterprise review."),
                Item("Compliance Export Pack", "Compliance", "Installed", "One-click ZIP with legal, module, license, and local diagnostics reports.", "Audit evidence packs", "Attach to onboarding and IT handover."),
                Item("Saved List Views", "Productivity", "Installed", "Save job dashboard search, status, and type filters.", "ERP saved filters", "Extend to Clients, Invoices, Purchases, and Inventory."),
                Item("Extension Marketplace", "Platform", "Ready for Future", "Catalog structure for optional modules without rebuilding navigation.", "Odoo Apps", "Keep client-side only; no remote marketplace calls until approved.")
            }
            .OrderBy(i => i.Category)
            .ThenBy(i => i.ModuleName)
            .ToList();
        }

        /// <summary>Builds a plain text module catalog report.</summary>
        public string BuildReport()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("ServoERP Module Catalog");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            builder.AppendLine();
            builder.AppendLine("This catalog lists installed modules and extension-ready ideas inspired by mature open-source ERP patterns.");
            builder.AppendLine("No client business data is included.");
            builder.AppendLine();

            foreach (ModuleCatalogItem item in GetCatalog())
            {
                builder.AppendLine(item.ModuleName);
                builder.AppendLine("Category    : " + item.Category);
                builder.AppendLine("Status      : " + item.Status);
                builder.AppendLine("Benefit     : " + item.ClientBenefit);
                builder.AppendLine("Inspiration : " + item.OpenSourceInspiration);
                builder.AppendLine("Next Step   : " + item.NextStep);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>Exports the module catalog report to the diagnostics folder.</summary>
        public string ExportReport()
        {
            string folder = Path.Combine(@"C:\HVAC_PRO_MSE", "DIAGNOSTICS");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "servoerp-module-catalog-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(path, BuildReport(), Encoding.UTF8);
            return path;
        }

        private static ModuleCatalogItem Item(string moduleName, string category, string status, string benefit, string inspiration, string nextStep)
        {
            return new ModuleCatalogItem
            {
                ModuleName = moduleName,
                Category = category,
                Status = status,
                ClientBenefit = benefit,
                OpenSourceInspiration = inspiration,
                NextStep = nextStep
            };
        }
    }
}
