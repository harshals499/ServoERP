using System.Linq;
using FluentValidation.Results;

namespace ServoERP.Validators
{
    /// <summary>Formats FluentValidation failures for ServoERP form message boxes.</summary>
    public static class ValidationMessageFormatter
    {
        /// <summary>Returns a newline-separated business-friendly validation message.</summary>
        public static string ToMessage(ValidationResult result)
        {
            if (result == null || result.IsValid)
                return string.Empty;

            return string.Join("\r\n", result.Errors.Select(error => "- " + error.ErrorMessage));
        }
    }
}
