using System;
using System.Globalization;

namespace HVAC_Pro_Desktop.Services
{
    public static class IndiaFormatHelper
    {
        private static readonly CultureInfo IndiaCulture = CultureInfo.GetCultureInfo("en-IN");
        private static readonly string[] Ones =
        {
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        };
        private static readonly string[] Tens =
        {
            "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
        };

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

        public static string ToRupeesOnlyWords(decimal amount)
        {
            long roundedRupees = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
            if (roundedRupees < 0)
                return "Minus " + ToRupeesOnlyWords(Math.Abs(roundedRupees));

            return ConvertIndianNumberToWords(roundedRupees) + " Rupees only";
        }

        public static string ConvertIndianNumberToWords(long number)
        {
            if (number == 0)
                return "Zero";
            if (number < 0)
                return "Minus " + ConvertIndianNumberToWords(Math.Abs(number));
            if (number < 20)
                return Ones[number];
            if (number < 100)
                return Tens[number / 10] + (number % 10 == 0 ? string.Empty : " " + ConvertIndianNumberToWords(number % 10));
            if (number < 1000)
                return ConvertIndianNumberToWords(number / 100) + " Hundred" + (number % 100 == 0 ? string.Empty : " and " + ConvertIndianNumberToWords(number % 100));
            if (number < 100000)
                return ConvertIndianNumberToWords(number / 1000) + " Thousand" + (number % 1000 == 0 ? string.Empty : " " + ConvertIndianNumberToWords(number % 1000));
            if (number < 10000000)
                return ConvertIndianNumberToWords(number / 100000) + " Lakh" + (number % 100000 == 0 ? string.Empty : " " + ConvertIndianNumberToWords(number % 100000));

            return ConvertIndianNumberToWords(number / 10000000) + " Crore" + (number % 10000000 == 0 ? string.Empty : " " + ConvertIndianNumberToWords(number % 10000000));
        }
    }
}
