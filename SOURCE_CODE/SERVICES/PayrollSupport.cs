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
        public static string ToIndianCurrencyWords(decimal amount)
        {
            return IndiaFormatHelper.ToRupeesOnlyWords(amount);
        }
    }
}
