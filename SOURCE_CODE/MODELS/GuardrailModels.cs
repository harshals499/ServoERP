using System;

namespace HVAC_Pro_Desktop.Models
{
    public enum GuardrailDecision { Allow, Warn, Block, OverrideRequired }

    public sealed class GuardrailResult
    {
        public GuardrailDecision Decision { get; set; }
        public string Message { get; set; }
        public string ModuleKey { get; set; }
        public int? RecordId { get; set; }
        public bool IsAllowed => Decision == GuardrailDecision.Allow || Decision == GuardrailDecision.Warn;
    }
}
