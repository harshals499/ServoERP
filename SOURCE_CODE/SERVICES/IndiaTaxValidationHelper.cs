using System;
using System.Text.RegularExpressions;

namespace HVAC_Pro_Desktop.Services
{
    public static class IndiaTaxValidationHelper
    {
        private static readonly Regex GstinRegex = new Regex(@"^\d{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled);
        private static readonly Regex PanRegex = new Regex(@"^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled);
        private static readonly Regex TanRegex = new Regex(@"^[A-Z]{4}[0-9]{5}[A-Z]$", RegexOptions.Compiled);

        public static string NormalizeTaxId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
        }

        public static bool IsValidGSTIN(string gstin)
        {
            string normalized = NormalizeTaxId(gstin);
            return normalized.Length == 15
                && GstinRegex.IsMatch(normalized)
                && IndiaStateCatalog.IsValidStateCode(normalized.Substring(0, 2));
        }

        public static bool IsValidPAN(string pan)
        {
            return PanRegex.IsMatch(NormalizeTaxId(pan));
        }

        public static bool IsValidTAN(string tan)
        {
            return TanRegex.IsMatch(NormalizeTaxId(tan));
        }

        public static void EnsureValidGSTIN(string gstin, string fieldName = "GSTIN")
        {
            if (!string.IsNullOrWhiteSpace(gstin) && !IsValidGSTIN(gstin))
                throw new Exception(fieldName + " must be a valid 15-character GSTIN.");
        }

        public static void EnsureValidPAN(string pan, string fieldName = "PAN")
        {
            if (!string.IsNullOrWhiteSpace(pan) && !IsValidPAN(pan))
                throw new Exception(fieldName + " must be a valid 10-character PAN.");
        }

        public static void EnsureValidTAN(string tan, string fieldName = "TAN")
        {
            if (!string.IsNullOrWhiteSpace(tan) && !IsValidTAN(tan))
                throw new Exception(fieldName + " must be a valid 10-character TAN.");
        }
    }
}
