using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models.Validation
{
    public sealed class DataQualityScore
    {
        public int Score { get; set; }
        public string Grade { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
    }
}
