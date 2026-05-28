using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only vendor comparison helper.
    /// </summary>
    public class VendorAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var vendors = new VendorService().GetAllVendorsWithSummary().OrderByDescending(v => v.TotalPurchased).Take(8).ToList();
            var result = new AiPluginResult { Intent = "Vendor Assistant" };
            result.Context = "Vendor snapshot: " + string.Join("; ", vendors.Select(v =>
                v.VendorName + " | " + v.Category + " | Open POs " + v.OpenPOCount + " | Outstanding " + IndiaFormatHelper.FormatCurrency(v.OutstandingBalance) + " | Overdue " + (v.HasOverdue ? "Yes" : "No")));
            result.SuggestedActions.Add(new AiSuggestedAction
            {
                Title = "Suggest vendor shortlist",
                Description = "Recommends a shortlist from visible vendor summaries. No PO is created.",
                TargetModule = "Vendors",
                IsWriteAction = false
            });
            return result;
        }
    }
}
