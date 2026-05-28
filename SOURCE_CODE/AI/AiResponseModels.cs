using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Shared DTOs for ServoERP Copilot. Keep these models provider-neutral.
    /// </summary>
    public class AiAssistantRequest
    {
        public string UserMessage { get; set; }
        public string Mode { get; set; }
        public string CurrentModule { get; set; }
        public string QuickAction { get; set; }
    }

    public class AiAssistantResponse
    {
        public string Answer { get; set; }
        public string Provider { get; set; }
        public string Model { get; set; }
        public bool IsError { get; set; }
        public bool RequiresConfirmation { get; set; }
        public List<AiSuggestedAction> SuggestedActions { get; set; } = new List<AiSuggestedAction>();
    }

    public class AiSuggestedAction
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TargetModule { get; set; }
        public bool IsWriteAction { get; set; }
    }

    public class AiContextSnapshot
    {
        public string CurrentModule { get; set; }
        public string Mode { get; set; }
        public string Summary { get; set; }
        public List<string> DataPoints { get; set; } = new List<string>();
    }

    public class AiPluginResult
    {
        public string Intent { get; set; }
        public string Context { get; set; }
        public List<AiSuggestedAction> SuggestedActions { get; set; } = new List<AiSuggestedAction>();
    }
}
