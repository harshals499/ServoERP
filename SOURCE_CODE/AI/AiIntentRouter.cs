using System;
using HVAC_Pro_Desktop.AI.Plugins;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Routes common ERP prompts to read-only helper plugins.
    /// </summary>
    public class AiIntentRouter
    {
        public AiPluginResult Route(AiAssistantRequest request)
        {
            string text = ((request?.QuickAction ?? "") + " " + (request?.Mode ?? "") + " " + (request?.CurrentModule ?? "") + " " + (request?.UserMessage ?? "")).ToLowerInvariant();

            if (ContainsAny(text, "invoice", "explain invoice"))
                return new InvoiceAiPlugin().Build(request?.UserMessage);
            if (ContainsAny(text, "quotation", "quote", "draft quotation", "tender"))
                return new QuotationAiPlugin().Build(request?.UserMessage);
            if (ContainsAny(text, "delayed job", "job", "technician", "dispatch"))
                return new JobAiPlugin().Build(request?.UserMessage);
            if (ContainsAny(text, "client", "site", "site history"))
                return new ClientAiPlugin().Build(request?.UserMessage);
            if (ContainsAny(text, "inventory", "stock", "material"))
                return new InventoryAiPlugin().Build(request?.UserMessage);
            if (ContainsAny(text, "vendor", "supplier", "purchase"))
                return new VendorAiPlugin().Build(request?.UserMessage);
            if (ContainsAny(text, "payment reminder", "payment", "receivable", "overdue"))
                return new PaymentAiPlugin().Build(request?.UserMessage);

            return new AiPluginResult { Intent = "General ERP Help" };
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
