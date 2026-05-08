using System;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class GlobalValidationEngine
    {
        public const decimal MaxReasonableMoney = 999999999m;

        public void EnsureValid(ValidationResult result, string context)
        {
            if (result == null || !result.HasErrors)
                return;

            string message = string.Join(Environment.NewLine, result.Issues
                .Where(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical)
                .Take(8)
                .Select(i => "- " + i.Message + (string.IsNullOrWhiteSpace(i.SuggestedFix) ? string.Empty : " Fix: " + i.SuggestedFix)));
            throw new InvalidOperationException((string.IsNullOrWhiteSpace(context) ? "Validation failed" : context) + Environment.NewLine + message);
        }

        public static string CleanText(string value, int maxLength = 255)
        {
            value = (value ?? string.Empty).Trim();
            value = Regex.Replace(value, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty);
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }

        public static bool IsValidEmail(string value)
        {
            return string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        public static bool IsValidPhone(string value)
        {
            return string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), @"^[0-9+\-\s()]{7,20}$");
        }

        public static bool IsValidGSTIN(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string gstin = value.Trim().ToUpperInvariant();
            return Regex.IsMatch(gstin, @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$")
                && HasValidGstinChecksum(gstin);
        }

        public static bool IsValidPAN(string value)
        {
            return string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim().ToUpperInvariant(), @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$");
        }

        public static bool IsReasonableDate(DateTime value)
        {
            return value == default(DateTime) || (value >= new DateTime(2000, 1, 1) && value <= DateTime.Today.AddYears(20));
        }

        private static bool HasValidGstinChecksum(string gstin)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (gstin == null || gstin.Length != 15)
                return false;

            int factor = 1;
            int sum = 0;
            for (int i = 0; i < 14; i++)
            {
                int codePoint = chars.IndexOf(gstin[i]);
                if (codePoint < 0)
                    return false;

                int product = codePoint * factor;
                sum += (product / 36) + (product % 36);
                factor = factor == 1 ? 2 : 1;
            }

            int checkCodePoint = (36 - (sum % 36)) % 36;
            return gstin[14] == chars[checkCodePoint];
        }
    }
}
