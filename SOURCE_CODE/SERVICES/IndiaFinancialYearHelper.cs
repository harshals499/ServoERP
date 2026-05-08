using System;

namespace HVAC_Pro_Desktop.Services
{
    public static class IndiaFinancialYearHelper
    {
        public static DateTime GetFinancialYearStart(DateTime date)
        {
            int year = date.Month >= 4 ? date.Year : date.Year - 1;
            return new DateTime(year, 4, 1);
        }

        public static DateTime GetFinancialYearEnd(DateTime date)
        {
            return GetFinancialYearStart(date).AddYears(1).AddDays(-1);
        }

        public static string GetFinancialYearCode(DateTime date)
        {
            DateTime start = GetFinancialYearStart(date);
            int endYear = start.Year + 1;
            return start.Year + "-" + (endYear % 100).ToString("00");
        }

        public static string GetFinancialYearDisplay(DateTime date)
        {
            DateTime start = GetFinancialYearStart(date);
            DateTime end = GetFinancialYearEnd(date);
            return start.ToString("dd/MM/yyyy") + " - " + end.ToString("dd/MM/yyyy");
        }
    }
}
