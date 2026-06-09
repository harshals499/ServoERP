using System;
using System.Linq;
using System.Text;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Builds small, relevant ERP context summaries. Never send full database payloads to the model.
    /// </summary>
    public class AiContextProvider
    {
        public AiContextSnapshot BuildContext(string mode, string currentModule, string userPrompt)
        {
            var snapshot = new AiContextSnapshot
            {
                Mode = string.IsNullOrWhiteSpace(mode) ? "General ERP Help" : mode,
                CurrentModule = string.IsNullOrWhiteSpace(currentModule) ? "Unknown" : currentModule
            };

            var summary = new StringBuilder();
            AddGeneralSummary(snapshot, summary);
            AddModuleContext(snapshot, summary, currentModule, mode, userPrompt);
            snapshot.Summary = summary.ToString().Trim();
            return snapshot;
        }

        private void AddGeneralSummary(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Business overview", () =>
            {
                int clients = new ClientService().GetAllClients().Count;
                int jobs = new JobService().GetAll().Count;
                int delayed = new JobService().GetAll().Count(j => j.IsOverdue || (j.ScheduledDate.Date < DateTime.Today && !IsClosedJob(j.Status)));
                int invoices = new InvoiceService().GetAllInvoices().Count;
                var overdueInvoices = new InvoiceService().GetOverdueInvoices();
                int overdueInvoiceCount = overdueInvoices.Count;
                int overdueReceivableCount = overdueInvoices.Count(i => i.BalanceDue > 0.01m);
                int quotes = new TenderService().GetAll().Count;
                int toOrder = new InventoryService().GetLowStock().Count;
                return "Clients " + clients + ", Jobs " + jobs + ", Delayed jobs " + delayed + ", Invoices " + invoices + ", Overdue invoices " + overdueInvoiceCount + ", Overdue receivables with positive balance " + overdueReceivableCount + ", Quotations " + quotes + ", Materials to order " + toOrder + ".";
            });
        }

        private void AddModuleContext(AiContextSnapshot snapshot, StringBuilder summary, string currentModule, string mode, string prompt)
        {
            string text = ((currentModule ?? "") + " " + (mode ?? "") + " " + (prompt ?? "")).ToLowerInvariant();
            if (text.Contains("invoice") || text.Contains("payment") || text.Contains("dashboard"))
                AddInvoiceContext(snapshot, summary);
            if (text.Contains("quotation") || text.Contains("quote") || text.Contains("tender"))
                AddQuotationContext(snapshot, summary);
            if (text.Contains("client") || text.Contains("site"))
                AddClientContext(snapshot, summary);
            if (text.Contains("job") || text.Contains("technician") || text.Contains("dispatch") || text.Contains("delayed"))
                AddJobContext(snapshot, summary);
            if (text.Contains("inventory") || text.Contains("stock") || text.Contains("material"))
                AddInventoryContext(snapshot, summary);
            if (text.Contains("vendor") || text.Contains("purchase") || text.Contains("supplier"))
                AddVendorContext(snapshot, summary);
        }

        private void AddInvoiceContext(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Invoice context", () =>
            {
                var service = new InvoiceService();
                var recent = service.GetAllInvoices().OrderByDescending(i => i.InvoiceDate).Take(5).ToList();
                var openReceivables = service.GetPendingInvoices().Where(i => i.BalanceDue > 0.01m).ToList();
                decimal pending = openReceivables.Sum(i => i.BalanceDue);
                return "Open receivables with positive balance " + openReceivables.Count + ", total " + IndiaFormatHelper.FormatCurrency(pending) + ". Recent invoices: " + string.Join("; ", recent.Select(i => (i.InvoiceNumber ?? ("Invoice #" + i.InvoiceID)) + " " + (i.ClientName ?? "Client") + " " + i.PaymentStatus + " balance " + IndiaFormatHelper.FormatCurrency(i.BalanceDue)));
            });
        }

        private void AddQuotationContext(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Quotation context", () =>
            {
                var quotes = new TenderService().GetAll().OrderByDescending(q => q.DueDate).Take(5).ToList();
                return "Recent quotations: " + string.Join("; ", quotes.Select(q => (q.QuotationNumber ?? ("Quote #" + q.BidID)) + " " + (q.ClientName ?? "Client") + " " + q.Status + " " + (q.CommercialFlow ?? "Revenue") + " value " + IndiaFormatHelper.FormatCurrency(q.TotalWithGST > 0 ? q.TotalWithGST : q.BidValue)));
            });
        }

        private void AddClientContext(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Client/site context", () =>
            {
                var clients = new ClientService().GetAllClients().OrderByDescending(c => c.TotalAnnualValue).Take(5).ToList();
                return "Top client summaries: " + string.Join("; ", clients.Select(c => c.CompanyName + " " + c.City + " health " + c.HealthScore + " annual " + IndiaFormatHelper.FormatCurrency(c.TotalAnnualValue)));
            });
        }

        private void AddJobContext(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Job context", () =>
            {
                var jobs = new JobService().GetAll().Where(j => j.IsOverdue || (j.ScheduledDate.Date < DateTime.Today && !IsClosedJob(j.Status))).OrderBy(j => j.ScheduledDate).Take(6).ToList();
                return jobs.Count == 0 ? "No delayed jobs found." : "Delayed jobs: " + string.Join("; ", jobs.Select(j => (j.JobNumber ?? ("Job #" + j.JobID)) + " " + (j.ClientName ?? "Client") + " " + j.Status + " scheduled " + IndiaFormatHelper.FormatDate(j.ScheduledDate)));
            });
        }

        private void AddInventoryContext(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Inventory context", () =>
            {
                var low = new InventoryService().GetLowStock().OrderBy(i => i.AvailableStock).Take(6).ToList();
                return low.Count == 0 ? "No material ordering items found." : "Materials to plan with suppliers: " + string.Join("; ", low.Select(i => i.ItemName + " need " + i.ReorderLevel.ToString("0.##") + " " + i.Unit + " planned, supplier " + (string.IsNullOrWhiteSpace(i.VendorName) ? "not mapped" : i.VendorName)));
            });
        }

        private void AddVendorContext(AiContextSnapshot snapshot, StringBuilder summary)
        {
            TryAdd(snapshot, summary, "Supplier context", () =>
            {
                var vendors = new VendorService().GetAllVendorsWithSummary().Where(v => v.IsSupplier).OrderByDescending(v => v.TotalPurchased).Take(5).ToList();
                return "Supplier summary: " + string.Join("; ", vendors.Select(v => v.VendorName + " " + v.Category + " open POs " + v.OpenPOCount + " outstanding " + IndiaFormatHelper.FormatCurrency(v.OutstandingBalance)));
            });
        }

        private static void TryAdd(AiContextSnapshot snapshot, StringBuilder summary, string label, Func<string> build)
        {
            try
            {
                string value = build();
                if (string.IsNullOrWhiteSpace(value))
                    return;
                summary.AppendLine(label + ": " + value);
                snapshot.DataPoints.Add(label + ": " + value);
            }
            catch (Exception ex)
            {
                summary.AppendLine(label + ": unavailable (" + ex.Message + ")");
            }
        }

        private static bool IsClosedJob(string status)
        {
            return string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        }
    }
}
