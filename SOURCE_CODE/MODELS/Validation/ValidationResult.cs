using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Models.Validation
{
    public sealed class ValidationResult
    {
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
        public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);

        public ValidationResult Add(ValidationSeverity severity, string module, string field, string message, string suggestedFix = null, bool canIgnore = false)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = severity,
                Module = module,
                Field = field,
                Message = message,
                SuggestedFix = suggestedFix,
                CanIgnore = canIgnore
            });
            return this;
        }

        public void Merge(ValidationResult other)
        {
            if (other != null)
                Issues.AddRange(other.Issues);
        }
    }
}
