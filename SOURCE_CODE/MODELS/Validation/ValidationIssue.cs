namespace HVAC_Pro_Desktop.Models.Validation
{
    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Module { get; set; }
        public string Field { get; set; }
        public string Message { get; set; }
        public string SuggestedFix { get; set; }
        public bool CanIgnore { get; set; }

        public override string ToString()
        {
            return Severity + " | " + Field + " | " + Message;
        }
    }
}
