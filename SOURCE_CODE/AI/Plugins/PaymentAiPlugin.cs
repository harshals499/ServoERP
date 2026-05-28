using System;
using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only payment helper. Reminder drafts are previews only.
    /// </summary>
    public class PaymentAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var overdue = new InvoiceService().GetOverdueInvoices()
                .Where(i => i.BalanceDue > 0.01m)
                .OrderBy(i => i.DueDate)
                .Take(10)
                .ToList();
            var result = new AiPluginResult { Intent = "Payment Assistant" };
            result.Context = overdue.Count == 0
                ? "No overdue customer invoices with a positive balance were found. Do not draft a collection reminder unless the user selects a real receivable."
                : "Overdue receivables: " + string.Join("; ", overdue.Select(i => (i.InvoiceNumber ?? ("Invoice #" + i.InvoiceID)) + " | " + (i.ClientName ?? "Client unknown") + " | Due " + IndiaFormatHelper.FormatDate(i.DueDate) + " | Balance " + IndiaFormatHelper.FormatCurrency(i.BalanceDue)));
            result.SuggestedActions.Add(new AiSuggestedAction
            {
                Title = "Draft payment reminder",
                Description = "Drafts reminder wording only. Nothing is sent from AI; review and use an existing communication workflow.",
                TargetModule = "Payments",
                IsWriteAction = true
            });
            return result;
        }
    }
}
