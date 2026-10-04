using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class InvoiceAnalyticsServiceSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            DateTime today = new DateTime(2026, 5, 20);
            var invoices = new List<Invoice>
            {
                new Invoice { InvoiceID = 1, InvoiceNumber = "INV-001", ClientName = "Alpha Cooling", SiteName = "Plant", InvoiceDate = new DateTime(2026, 5, 2), DueDate = today.AddDays(4), TotalAmount = 100000m, PaidAmount = 25000m, BalanceDue = 75000m, PaymentStatus = "Pending" },
                new Invoice { InvoiceID = 2, InvoiceNumber = "INV-002", ClientName = "Beta Towers", SiteName = "Office", InvoiceDate = new DateTime(2026, 5, 4), DueDate = today.AddDays(-5), TotalAmount = 50000m, PaidAmount = 0m, BalanceDue = 50000m, PaymentStatus = "Pending" },
                new Invoice { InvoiceID = 3, InvoiceNumber = "INV-003", ClientName = "Alpha Cooling", SiteName = "Plant", InvoiceDate = new DateTime(2026, 5, 8), DueDate = today.AddDays(-40), TotalAmount = 75000m, PaidAmount = 25000m, BalanceDue = 50000m, PaymentStatus = "Partial" },
                new Invoice { InvoiceID = 4, InvoiceNumber = "INV-004", ClientName = "Gamma Hospital", SiteName = "Main", InvoiceDate = new DateTime(2026, 5, 12), DueDate = today.AddDays(8), TotalAmount = 25000m, PaidAmount = 25000m, BalanceDue = 0m, PaymentStatus = "Paid" },
                new Invoice { InvoiceID = 5, InvoiceNumber = "INV-005", ClientName = "Delta Builders", SiteName = "Site", InvoiceDate = new DateTime(2026, 4, 10), DueDate = today.AddDays(-95), TotalAmount = 20000m, PaidAmount = 0m, BalanceDue = 20000m, PaymentStatus = "Draft" }
            };

            var contracts = new List<AMCContract>
            {
                new AMCContract { ContractID = 1, ClientID = 1, EndDate = new DateTime(2026, 5, 28), ContractStatus = "Active" }
            };

            var service = new InvoiceAnalyticsService();
            InvoiceDashboardSnapshot snapshot = service.BuildSnapshot(
                invoices,
                contracts,
                new InvoiceAnalyticsFilter { DateFrom = new DateTime(2026, 5, 1), DateTo = new DateTime(2026, 5, 31), Grouping = InvoiceAnalyticsGrouping.Week },
                today);

            if (snapshot.Kpis.TotalInvoices.Value != 4)
                throw new InvalidOperationException("Invoice count KPI did not respect the selected date range.");
            if (snapshot.Kpis.TotalAmount.Value != 250000m)
                throw new InvalidOperationException("Total amount KPI should sum grand totals in range.");
            if (snapshot.Kpis.PaidAmount.Value != 75000m)
                throw new InvalidOperationException("Paid amount KPI should sum paid amount in range.");
            if (snapshot.Kpis.PendingAmount.Value != 75000m)
                throw new InvalidOperationException("Pending amount KPI should include unpaid non-overdue balances only.");
            if (snapshot.Kpis.OverdueAmount.Value != 100000m)
                throw new InvalidOperationException("Overdue amount KPI should include unpaid overdue balances only.");
            passed.Add("invoice KPI totals are calculated from selected date range");

            if (!snapshot.Statuses.Any(s => s.Status == "Overdue" && s.Count == 2))
                throw new InvalidOperationException("Status donut should reclassify unpaid past-due invoices as Overdue.");
            if (snapshot.TopClients.First().ClientName != "Alpha Cooling" || snapshot.TopClients.First().Amount != 175000m)
                throw new InvalidOperationException("Top clients should group by client and sort by amount.");
            if (snapshot.RecentInvoices.First().InvoiceNumber != "INV-005" || !snapshot.RecentInvoices.Any(r => r.InvoiceNumber == "INV-004"))
                throw new InvalidOperationException("Invoice work queue should prioritize the oldest overdue balance while retaining recent paid invoices.");
            if (snapshot.AgingBuckets.First(b => b.Bucket == "31-60 Days").Amount != 50000m)
                throw new InvalidOperationException("Receivables aging should bucket unpaid balances by due date age.");
            if (snapshot.AgingBuckets.First(b => b.Bucket == "Not due").Amount != 75000m)
                throw new InvalidOperationException("Receivables aging should include open balances that are not yet due.");
            if (snapshot.OutstandingAmount != 195000m || snapshot.CashExpectedAmount != 195000m || snapshot.OverdueAmount != 120000m)
                throw new InvalidOperationException("Receivables cockpit totals should include all open invoices and the 30-day collection window.");
            if (snapshot.CollectionForecast.Count != 8 || snapshot.CollectionForecast.Sum(p => p.TotalAmount) != 195000m)
                throw new InvalidOperationException("Eight-week collection forecast should allocate each open balance exactly once.");
            if (!snapshot.ClientExposure.Any(r => r.ClientName == "Alpha Cooling" && r.OutstandingAmount == 125000m))
                throw new InvalidOperationException("Client concentration should be calculated from outstanding balances.");
            if (!snapshot.RecentInvoices.Any(r => r.InvoiceNumber == "INV-003" && r.Risk == "High" && r.DaysOverdue == 40))
                throw new InvalidOperationException("Invoice work queue should expose collection risk and days overdue.");
            if (!snapshot.Reminders.Any(r => r.IndexOf("pending approval", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("Dynamic reminders should include pending approval counts.");
            if (!snapshot.Reminders.Any(r => r.IndexOf("expiring this month", StringComparison.OrdinalIgnoreCase) >= 0))
                throw new InvalidOperationException("Dynamic reminders should include contracts expiring this month.");
            passed.Add("invoice charts, recent rows, aging, workflow, and reminders are derived from invoice data");

            InvoiceDashboardSnapshot empty = service.BuildSnapshot(
                new List<Invoice>(),
                new List<AMCContract>(),
                new InvoiceAnalyticsFilter { DateFrom = new DateTime(2026, 5, 1), DateTo = new DateTime(2026, 5, 31) },
                today);
            if (!empty.UsesDemoData)
                throw new InvalidOperationException("Empty invoice dashboard should still report empty-source state.");
            if (empty.Kpis.TotalInvoices.Value != 0m || empty.Kpis.TotalAmount.Value != 0m || empty.RecentInvoices.Any() || empty.TopClients.Any())
                throw new InvalidOperationException("Empty invoice dashboard must not show seeded amounts, clients, dates, or invoice numbers.");
            passed.Add("empty invoice dashboard stays empty without seeded business data");

            string englishNotice = UpdateService.BuildEnglishWhatsNewTextForTest("1.2.3", "[English]\n- Faster invoice dashboard.\n[मराठी]\n- मराठी मजकूर");
            if (englishNotice.IndexOf("Faster invoice dashboard", StringComparison.OrdinalIgnoreCase) < 0 || englishNotice.Any(c => c >= '\u0900' && c <= '\u097F'))
                throw new InvalidOperationException("Post-update release notes must display English content only.");
            passed.Add("post-update What's New notice is English-only");

            return passed;
        }
    }
}
