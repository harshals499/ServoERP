using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class DashboardCommandCenterSmokeTests
    {
        public static List<string> RunAll()
        {
            var passed = new List<string>();
            var service = new DashboardCommandCenterService();

            DashboardCommandCenterSnapshot demo = service.BuildSnapshot(new DashboardAnalyticsInput(), new DashboardAnalyticsFilter());
            if (!demo.UsesDemoData)
                throw new InvalidOperationException("Empty dashboard input should report empty-source state.");
            if (demo.Charts.Count < 8)
                throw new InvalidOperationException("Command Center should expose the required analytics chart set.");
            if (demo.Details.Values.Any(rows => rows.Any()) || demo.Forecasts.Any(item => item.Value.StartsWith("Rs ", StringComparison.OrdinalIgnoreCase) && item.Value != "Rs 0"))
                throw new InvalidOperationException("Empty dashboard input must not show seeded amounts, customers, dates, or document numbers.");
            passed.Add("empty dashboard input stays empty without seeded business data");

            DateTime today = DateTime.Today;
            var input = new DashboardAnalyticsInput
            {
                Invoices = new List<Invoice>
                {
                    new Invoice { InvoiceID = 1, InvoiceNumber = "INV-A", ClientName = "Alpha", SiteName = "Plant", InvoiceDate = today.AddDays(-3), DueDate = today.AddDays(12), TotalAmount = 100000m, PaidAmount = 40000m, BalanceDue = 60000m, PaymentStatus = "Pending" },
                    new Invoice { InvoiceID = 2, InvoiceNumber = "INV-B", ClientName = "Beta", SiteName = "Tower", InvoiceDate = today.AddDays(-2), DueDate = today.AddDays(20), TotalAmount = 50000m, PaidAmount = 50000m, BalanceDue = 0m, PaymentStatus = "Paid" }
                },
                Payments = new List<Payment>
                {
                    new Payment { PaymentID = 1, PaymentNumber = "PAY-A", ClientName = "Alpha", PaymentDate = today.AddDays(-1), AmountPaid = 40000m, PaymentMode = "UPI" }
                },
                Jobs = new List<Job>
                {
                    new Job { JobID = 1, JobNumber = "JOB-A", ClientName = "Alpha", SiteName = "Plant", Status = "Pending", ScheduledDate = today, AssignedEmployeeName = "Tech One", QuotedRevenue = 25000m },
                    new Job { JobID = 2, JobNumber = "JOB-B", ClientName = "Beta", SiteName = "Tower", Status = "Completed", ScheduledDate = today.AddDays(-5), CompletedDate = today.AddDays(-2), AssignedEmployeeName = "Tech Two", Revenue = 35000m }
                },
                InventoryItems = new List<StockItem>
                {
                    new StockItem { ItemID = 1, ItemName = "Filter", CurrentStock = 2m, ReorderLevel = 5m, LastPurchaseRate = 500m, VendorName = "Supplier One", LastUpdated = today }
                },
                PurchaseOrders = new List<PurchaseOrder>
                {
                    new PurchaseOrder { POID = 1, PONumber = "PO-OPEN", ClientName = "Alpha", SiteName = "Plant", VendorName = "Supplier One", PODate = today.AddDays(-2), PayByDate = today.AddDays(7), TotalAmount = 20000m, PaidAmount = 0m, Status = "Pending" },
                    new PurchaseOrder { POID = 2, PONumber = "PO-FULL", ClientName = "Alpha", SiteName = "Plant", VendorName = "Supplier One", PODate = today.AddDays(-8), PayByDate = today.AddDays(-3), TotalAmount = 50000m, PaidAmount = 0m, Status = "Fully Received" }
                }
            };

            DashboardCommandCenterSnapshot filtered = service.BuildSnapshot(input, new DashboardAnalyticsFilter { Customer = "Alpha" });
            if (filtered.UsesDemoData)
                throw new InvalidOperationException("Real dashboard input should not use demo analytics.");
            if (!filtered.Details["pending_invoices"].Any(row => row.Reference == "INV-A"))
                throw new InvalidOperationException("Filtered pending invoice detail was not preserved.");
            if (filtered.Details["revenue"].Any(row => row.Customer == "Beta"))
                throw new InvalidOperationException("Customer filter did not apply to revenue details.");
            if (!filtered.Details["supplier_dues"].Any(row => row.Reference == "PO-OPEN") || filtered.Details["supplier_dues"].Any(row => row.Reference == "PO-FULL"))
                throw new InvalidOperationException("Fully received purchase orders must not appear in supplier dues.");
            passed.Add("dashboard cross-filtering drives metrics, charts, and drilldown details");

            return passed;
        }
    }
}
