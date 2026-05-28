using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only quotation helper. Drafts text and pricing checks without saving records.
    /// </summary>
    public class QuotationAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var quotes = new TenderService().GetAll().OrderByDescending(q => q.DueDate).Take(8).ToList();
            var result = new AiPluginResult { Intent = "Quotation Assistant" };
            result.Context = "Recent quotations: " + string.Join("; ", quotes.Select(q =>
                (q.QuotationNumber ?? ("Quote #" + q.BidID)) + " | " + (q.ClientName ?? "Client unknown") + " | " + q.Status + " | Flow " + (q.CommercialFlow ?? "Revenue") + " | Value " + IndiaFormatHelper.FormatCurrency(q.TotalWithGST > 0 ? q.TotalWithGST : q.BidValue)));
            result.SuggestedActions.Add(new AiSuggestedAction
            {
                Title = "Draft quotation only",
                Description = "Creates suggested wording, scope, assumptions, and follow-up notes. The quotation is not saved automatically.",
                TargetModule = "Quotations",
                IsWriteAction = true
            });
            return result;
        }
    }
}
