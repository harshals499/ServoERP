using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Main ServoERP assistant orchestration service. Uses built-in ERP rules and context only.
    /// </summary>
    public class AiAssistantService
    {
        private readonly AiContextProvider _contextProvider = new AiContextProvider();
        private readonly AiIntentRouter _intentRouter = new AiIntentRouter();

        public async Task<bool> IsLocalAiReachableAsync(CancellationToken cancellationToken)
        {
            AiProviderConfig config = AiProviderConfig.Load();
            await Task.Yield();
            return config.Enabled;
        }

        public async Task<AiAssistantResponse> AskAsync(AiAssistantRequest request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            AiProviderConfig config = AiProviderConfig.Load();
            if (!config.Enabled)
            {
                return Error("ServoERP Assistant is disabled in Settings.", config);
            }

            try
            {
                var context = _contextProvider.BuildContext(request?.Mode, request?.CurrentModule, request?.UserMessage);
                var plugin = _intentRouter.Route(request);
                string answer = BuildBuiltInAnswer(request, context, plugin);

                var response = new AiAssistantResponse
                {
                    Answer = answer,
                    Provider = config.Provider,
                    Model = config.ModelName,
                    RequiresConfirmation = plugin != null && plugin.SuggestedActions.Exists(a => a.IsWriteAction)
                };
                if (plugin != null && plugin.SuggestedActions != null)
                    response.SuggestedActions.AddRange(plugin.SuggestedActions);
                return response;
            }
            catch (OperationCanceledException)
            {
                return Error("Assistant request cancelled.", config);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Assistant.Copilot.Error", ex);
                return Error("Assistant request failed: " + ex.Message, config);
            }
        }

        private string BuildBuiltInAnswer(AiAssistantRequest request, AiContextSnapshot context, AiPluginResult plugin)
        {
            var sb = new StringBuilder();
            string mode = Safe(request?.Mode, "General ERP Help");
            string module = Safe(request?.CurrentModule, context?.CurrentModule ?? "Unknown");
            string message = Safe(request?.UserMessage, string.Empty);

            sb.AppendLine("ServoERP Assistant");
            sb.AppendLine();
            sb.AppendLine("Mode: " + mode);
            sb.AppendLine("Page context: " + module);

            if (plugin != null && !string.IsNullOrWhiteSpace(plugin.Intent))
                sb.AppendLine("Detected intent: " + plugin.Intent);

            sb.AppendLine();
            sb.AppendLine("What I can do now:");
            sb.AppendLine("- Explain the current page and business numbers.");
            sb.AppendLine("- Summarize clients, invoices, jobs, quotations, vendors, materials, and payments from ServoERP context.");
            sb.AppendLine("- Draft preview-only text for quotations, payment reminders, vendor RFQs, and job follow-ups.");
            sb.AppendLine("- Point you to the right module action without creating, sending, deleting, or updating records automatically.");

            if (context != null && context.DataPoints != null && context.DataPoints.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Current ERP signals:");
                foreach (string point in context.DataPoints.Take(6))
                    sb.AppendLine("- " + point);
            }

            if (plugin != null && !string.IsNullOrWhiteSpace(plugin.Context))
            {
                sb.AppendLine();
                sb.AppendLine("Relevant module context:");
                sb.AppendLine(plugin.Context);
            }

            sb.AppendLine();
            sb.AppendLine("Suggested next steps:");
            AppendNextSteps(sb, message, mode, module, plugin);
            return sb.ToString().Trim();
        }

        private static void AppendNextSteps(StringBuilder sb, string message, string mode, string module, AiPluginResult plugin)
        {
            string text = ((message ?? string.Empty) + " " + (mode ?? string.Empty) + " " + (module ?? string.Empty) + " " + (plugin?.Intent ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(text, "payment", "receivable", "overdue"))
            {
                sb.AppendLine("- Open Payments or Invoices, filter unpaid customer balances, then use WhatsApp Hub or email workflow for a reviewed reminder.");
                sb.AppendLine("- Do not chase invoices with zero or negative balance.");
                return;
            }
            if (ContainsAny(text, "quotation", "quote", "tender"))
            {
                sb.AppendLine("- Open Quotations, confirm client/site/scope, then prepare a draft with materials, labor, taxes, exclusions, and validity.");
                sb.AppendLine("- Use vendor RFQ wording only as a preview before sending.");
                return;
            }
            if (ContainsAny(text, "material", "inventory", "supplier", "vendor", "purchase"))
            {
                sb.AppendLine("- Open Materials / Procurement, check To Order and Needs Supplier, then create a PO from the selected item.");
                sb.AppendLine("- Keep supplier choice tied to vendor history, purchase rate, and availability.");
                return;
            }
            if (ContainsAny(text, "job", "technician", "dispatch", "delay"))
            {
                sb.AppendLine("- Open Dispatch Center or Jobs, sort delayed work by schedule date, then call the customer or assign technician follow-up.");
                sb.AppendLine("- Check whether material ordering or invoice/payment dependency is blocking the job.");
                return;
            }
            if (ContainsAny(text, "client", "site"))
            {
                sb.AppendLine("- Open Clients, select the company/site, then review contacts, open jobs, active contracts, and invoices.");
                sb.AppendLine("- Use the summary as a call preparation checklist.");
                return;
            }

            sb.AppendLine("- Use the page-specific buttons or suggested prompts to narrow the task.");
            sb.AppendLine("- Select a record first when you need record-specific help.");
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

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static AiAssistantResponse Error(string message, AiProviderConfig config)
        {
            return new AiAssistantResponse
            {
                IsError = true,
                Answer = message,
                Provider = config?.Provider ?? "Built-in",
                Model = config?.ModelName ?? "ServoERP Bot"
            };
        }
    }
}
