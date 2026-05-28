using System.Text;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Central prompt policy for ServoERP Copilot.
    /// </summary>
    public class AiPromptBuilder
    {
        private const string SystemPrompt =
            "You are ServoERP Copilot, an internal HVAC ERP assistant. You help users understand clients, sites, jobs, quotations, invoices, vendors, inventory, payments, and business analytics. Use only the provided ERP context. If data is missing, ask the user to select a record or provide more details. Never invent business data. For any database update, only suggest the action and wait for user confirmation. Be concise and practical.";

        public string BuildPrompt(AiAssistantRequest request, AiContextSnapshot context, AiPluginResult pluginResult)
        {
            var sb = new StringBuilder();
            sb.AppendLine(SystemPrompt);
            sb.AppendLine();
            sb.AppendLine("Assistant mode: " + Safe(request?.Mode, "General ERP Help"));
            sb.AppendLine("Current ServoERP module/page: " + Safe(request?.CurrentModule, context?.CurrentModule ?? "Unknown"));
            sb.AppendLine();
            sb.AppendLine("ERP context summary:");
            sb.AppendLine(Safe(context?.Summary, "No ERP context available."));

            if (context != null && context.DataPoints != null && context.DataPoints.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Limited data points:");
                foreach (string point in context.DataPoints)
                    sb.AppendLine("- " + Safe(point, string.Empty));
            }

            if (pluginResult != null && !string.IsNullOrWhiteSpace(pluginResult.Context))
            {
                sb.AppendLine();
                sb.AppendLine("Intent-specific context:");
                sb.AppendLine(pluginResult.Context);
            }

            sb.AppendLine();
            sb.AppendLine("Response rules:");
            sb.AppendLine("- Use the ERP data above only.");
            sb.AppendLine("- Say what is missing when needed.");
            sb.AppendLine("- Never claim a record was created, updated, sent, or deleted.");
            sb.AppendLine("- Never say a reminder, invoice, quotation, or message is sent. Use draft-only wording.");
            sb.AppendLine("- Do not ask if you should send, update, create, or delete. Tell the user to review and use the existing ERP workflow.");
            sb.AppendLine("- For reminders, quotations, invoices, or jobs, provide a draft or checklist only.");
            sb.AppendLine("- Prefer short headings and bullet points.");
            sb.AppendLine();
            sb.AppendLine("User question:");
            sb.AppendLine(Safe(request?.UserMessage, string.Empty));
            return sb.ToString();
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
