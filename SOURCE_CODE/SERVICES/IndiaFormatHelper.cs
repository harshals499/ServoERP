using System;
using System.Globalization;

namespace HVAC_Pro_Desktop.Services
{
    public static class IndiaFormatHelper
    {
        private static readonly CultureInfo IndiaCulture = CultureInfo.GetCultureInfo("en-IN");

        public static string FormatCurrency(decimal amount)
        {
            return "\u20B9" + amount.ToString("N2", IndiaCulture);
        }

        public static string FormatNumber(decimal amount, int decimals = 2)
        {
            return amount.ToString("N" + decimals, IndiaCulture);
        }

        public static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
        }
    }
}
