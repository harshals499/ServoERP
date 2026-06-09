using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace HVAC_Pro_Desktop.Services
{
    public static class PayrollFolderHelper
    {
        public const string PayslipRoot = @"C:\HVAC_PRO_MSE\PAYSLIPS";
        public const string PayrollExportRoot = @"C:\HVAC_PRO_MSE\PAYROLL_EXPORTS";
        public const string PayrollImportLogPath = @"C:\HVAC_PRO_MSE\LOGS\payroll_import.log";
        public const string SourcePayrollFolder = @"C:\HVAC_PRO_MSE\SOURCE_CODE\Payroll";

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(PayslipRoot);
            Directory.CreateDirectory(PayrollExportRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(PayrollImportLogPath) ?? @"C:\HVAC_PRO_MSE\LOGS");
        }

        public static string EnsurePayslipFolder(int year, int month)
        {
            string path = Path.Combine(PayslipRoot, year.ToString("0000"), month.ToString("00"));
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static class PayrollImportLogger
    {
        private static readonly object Sync = new object();

        public static void Log(string message)
        {
            try
            {
                PayrollFolderHelper.EnsureFolders();
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + " | " + (message ?? string.Empty) + Environment.NewLine;
                lock (Sync)
                {
                    File.AppendAllText(PayrollFolderHelper.PayrollImportLogPath, line, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }

    internal static class PayrollHtmlPdfExporter
    {
        public static void ExportHtmlToPdf(string html, string outputPath)
        {
            HtmlPdfExportService.ExportHtmlToPdf(html, outputPath);
        }
    }

    internal static class PayrollWordsHelper
    {
        private static readonly string[] Ones =
        {
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        };

        private static readonly string[] Tens =
        {
            "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
        };

        public static string ToIndianCurrencyWords(decimal amount)
        {
            long rupees = (long)Math.Floor(amount);
            int paise = (int)Math.Round((amount - rupees) * 100m, MidpointRounding.AwayFromZero);
            string words = ConvertNumber(rupees) + " Rupees";
            if (paise > 0)
                words += " and " + ConvertNumber(paise) + " Paise";
            return words + " Only";
        }

        private static string ConvertNumber(long number)
        {
            if (number < 20)
                return Ones[number];
            if (number < 100)
                return Tens[number / 10] + (number % 10 == 0 ? string.Empty : " " + ConvertNumber(number % 10));
            if (number < 1000)
                return ConvertNumber(number / 100) + " Hundred" + (number % 100 == 0 ? string.Empty : " " + ConvertNumber(number % 100));
            if (number < 100000)
                return ConvertNumber(number / 1000) + " Thousand" + (number % 1000 == 0 ? string.Empty : " " + ConvertNumber(number % 1000));
            if (number < 10000000)
                return ConvertNumber(number / 100000) + " Lakh" + (number % 100000 == 0 ? string.Empty : " " + ConvertNumber(number % 100000));
            return ConvertNumber(number / 10000000) + " Crore" + (number % 10000000 == 0 ? string.Empty : " " + ConvertNumber(number % 10000000));
        }
    }
}
