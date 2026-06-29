using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class QuotationImportSmokeTests
    {
        public static string WriteReport(string filePath)
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string reportPath = Path.Combine(dir, "quotation-import-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var lines = new List<string>
            {
                "Quotation Import Smoke Test",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Source: " + filePath,
                string.Empty
            };

            try
            {
                Run(filePath, lines);
            }
            catch (Exception ex)
            {
                lines.Add("FAIL " + ex);
            }

            File.WriteAllLines(reportPath, lines);
            return reportPath;
        }

        private static void Run(string filePath, List<string> lines)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Quotation import smoke source file was not found.", filePath);

            if (!SessionManager.IsLoggedIn && !LocalLoginBypassService.TryStartSession(out string loginMessage))
                throw new UnauthorizedAccessException("Could not start local smoke-test session. " + loginMessage);

            var pipeline = new MasterDataIngestionPipeline();
            AutomatedImportPreview preview = pipeline.PreviewFile(filePath, ExcelImportModule.Quotations);
            lines.Add("Preview module: " + preview.DetectedModule);
            lines.Add("Preview sheet: " + preview.DetectedSheetName);
            lines.Add("Preview confidence: " + preview.DetectionConfidence);
            lines.Add("Preview canonical rows: " + preview.CanonicalRowCount);

            if (preview.DetectedModule != ExcelImportModule.Quotations)
                throw new InvalidOperationException("Expected quotation module, detected " + preview.DetectedModule + ".");
            if (preview.DetectionConfidence < 90)
                throw new InvalidOperationException("Expected high-confidence MSE quotation detection.");
            if (preview.CanonicalRowCount < 5)
                throw new InvalidOperationException("Expected item lines from the MSE quotation grid.");

            Dictionary<string, string> first = preview.SampleRows.FirstOrDefault();
            string quoteNumber = first != null && first.TryGetValue("QuotationNumber", out string number) ? number : string.Empty;
            if (string.IsNullOrWhiteSpace(quoteNumber))
                throw new InvalidOperationException("Preview did not expose a quotation number.");

            AutomatedImportResult result = pipeline.ImportFile(filePath, ExcelImportModule.Quotations, "Sent to Clients");
            lines.Add("Import success rows: " + result.SuccessCount);
            lines.Add("Import skipped rows: " + result.SkippedCount);
            foreach (string message in result.UserMessages)
                lines.Add("Message: " + message);
            foreach (string error in result.Errors)
                lines.Add("Import error: " + error);

            if (result.SuccessCount < preview.CanonicalRowCount)
                throw new InvalidOperationException("Import did not save all parsed quotation lines.");
            if (result.SkippedCount > 0)
                throw new InvalidOperationException("Import skipped one or more rows.");

            VerifySavedQuotation(quoteNumber, preview.CanonicalRowCount, lines);
            lines.Add("PASS MSE quotation workbook imported and saved with line items.");
        }

        private static void VerifySavedQuotation(string quotationNumber, int expectedLineCount, List<string> lines)
        {
            var db = new DatabaseManager();
            using (SqlConnection conn = db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1 q.BidID, q.ClientID, q.BidValue, q.TotalTaxableValue, q.TotalGSTAmount, q.TotalWithGST,
       (SELECT COUNT(1) FROM QuotationLineItems li WHERE li.TenderBidId = q.BidID) AS LineCount
FROM Quotations q
WHERE q.QuotationNumber = @number
ORDER BY q.BidID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@number", quotationNumber);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("Imported quotation was not found in SQL: " + quotationNumber);

                        int lineCount = Convert.ToInt32(reader["LineCount"]);
                        decimal totalWithGst = Convert.ToDecimal(reader["TotalWithGST"]);
                        lines.Add("Saved quotation: " + quotationNumber);
                        lines.Add("Saved total with GST: " + totalWithGst.ToString("0.##"));
                        lines.Add("Saved line count: " + lineCount);

                        if (lineCount != expectedLineCount)
                            throw new InvalidOperationException("Expected " + expectedLineCount + " line items, found " + lineCount + ".");
                        if (totalWithGst <= 0m)
                            throw new InvalidOperationException("Saved quotation total is zero.");
                    }
                }
            }
        }
    }
}
