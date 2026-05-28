using System.Linq;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI.Plugins
{
    /// <summary>
    /// Read-only client and site summarizer.
    /// </summary>
    public class ClientAiPlugin
    {
        public AiPluginResult Build(string prompt)
        {
            var clients = new ClientService().GetAllClients().OrderByDescending(c => c.TotalAnnualValue).Take(8).ToList();
            var result = new AiPluginResult { Intent = "Client Assistant" };
            result.Context = "Client snapshot: " + string.Join("; ", clients.Select(c =>
                c.CompanyName + " | " + c.City + " | Health " + c.HealthScore + " | Annual " + IndiaFormatHelper.FormatCurrency(c.TotalAnnualValue) + " | Stage " + (c.RelationshipStage ?? "-")));
            result.SuggestedActions.Add(new AiSuggestedAction
            {
                Title = "Summarize client/site history",
                Description = "Summarizes available client profile, risk, and service context. No client data is changed.",
                TargetModule = "Clients",
                IsWriteAction = false
            });
            return result;
        }
    }
}
