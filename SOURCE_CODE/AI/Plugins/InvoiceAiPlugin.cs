using System;
using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only invoice helper. Future write workflows must use preview and confirmation.
    /// </summary>
    public class InvoiceAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var result = new AiPluginResult { Intent = "Invoice Assistant" };
            var invoices = new InvoiceService().GetAllInvoices().OrderByDescending(i => i.InvoiceDate).Take(8).ToList();
            result.Context = "Recent invoices: " + string.Join("; ", invoices.Select(i =>
                (i.InvoiceNumber ?? ("Invoice #" + i.InvoiceID)) + " | " + (i.ClientName ?? "Client unknown") + " | " + i.PaymentStatus + " | Total " + IndiaFormatHelper.FormatCurrency(i.TotalAmount) + " | Balance " + IndiaFormatHelper.FormatCurrency(i.BalanceDue)));
            result.SuggestedActions.Add(new AiSuggestedAction
            {
                Title = "Preview invoice explanation",
                Description = "Explains totals, balance, due date, and payment status. No invoice data is changed.",
                TargetModule = "Invoices",
                IsWriteAction = false
            });
            return result;
        }
    }
}
