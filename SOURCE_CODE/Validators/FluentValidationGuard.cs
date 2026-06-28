using System;
using FluentValidation;
using FluentValidation.Results;
using HVAC_Pro_Desktop.Services;

namespace ServoERP.Validators
{
    /// <summary>Bridges FluentValidation results into ServoERP validation exceptions and messages.</summary>
    public static class FluentValidationGuard
    {
        public static void EnsureValid<T>(IValidator<T> validator, T instance, string context = null)
        {
            string message;
            if (TryValidate(validator, instance, out message))
                return;

            AppLogger.LogInfo("Validation warning only: " + (string.IsNullOrWhiteSpace(context) ? "Validation" : context) + " | " + message.Replace(Environment.NewLine, " "));
        }

        public static bool TryValidate<T>(IValidator<T> validator, T instance, out string message)
        {
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            ValidationResult result = validator.Validate(instance);
            message = ValidationMessageFormatter.ToMessage(result);
            return result == null || result.IsValid;
        }
    }
}
