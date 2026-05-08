using System.Linq;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class DataQualityEngine
    {
        public DataQualityScore Score(ValidationResult result)
        {
            result = result ?? new ValidationResult();
            int penalty = result.Issues.Sum(i =>
                i.Severity == ValidationSeverity.Critical ? 45 :
                i.Severity == ValidationSeverity.Error ? 25 :
                i.Severity == ValidationSeverity.Warning ? 8 : 2);
            int score = System.Math.Max(0, 100 - penalty);
            return new DataQualityScore
            {
                Score = score,
                Grade = score >= 90 ? "Excellent" : score >= 75 ? "Good" : score >= 55 ? "Needs Review" : "Blocked",
                Issues = result.Issues.ToList()
            };
        }
    }
}
