using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class ImportReviewService
    {
        public ValidationResult ReviewHeaders(string module, IEnumerable<string> actualHeaders, IEnumerable<string> requiredHeaders)
        {
            var result = new ValidationResult();
            HashSet<string> actual = new HashSet<string>(actualHeaders ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
            foreach (string required in requiredHeaders ?? Enumerable.Empty<string>())
                if (!actual.Contains(required))
                    result.Add(ValidationSeverity.Error, module, required, "Import file is missing required column: " + required, "Download the latest import template.");
            return result;
        }

        public ValidationResult ReviewImportResult(string module, ExcelImportResult import)
        {
            var result = new ValidationResult();
            if (import == null)
                return result.Add(ValidationSeverity.Error, module, "Import", "Import result was not produced.");
            if (import.SkippedCount > 0)
                result.Add(ValidationSeverity.Warning, module, "SkippedRows", import.SkippedCount + " row(s) were skipped.", "Open import errors before using imported data.", true);
            if (import.SuccessCount == 0)
                result.Add(ValidationSeverity.Error, module, "SuccessCount", "No rows were imported.", "Fix the file and retry.");
            return result;
        }
    }
}
