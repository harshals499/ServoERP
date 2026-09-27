using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.Models;
using OfficeOpenXml;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class ProfitabilityWorkbookImportService
    {
        private readonly InvoiceService _invoiceService = new InvoiceService();
        private readonly JobService _jobService = new JobService();

        public ProfitabilityImportPreview Preview(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Select an existing Excel workbook.", filePath);

            List<Invoice> invoices = _invoiceService.GetAllInvoices() ?? new List<Invoice>();
            List<Job> jobs = _jobService.GetAll() ?? new List<Job>();
            Dictionary<string, Invoice> invoiceByNumber = invoices
                .Where(i => !string.IsNullOrWhiteSpace(i.InvoiceNumber))
                .GroupBy(i => NormalizeReference(i.InvoiceNumber))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.InvoiceDate).First());

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                ExcelWorksheet sheet = FindProfitabilitySheet(package);
                if (sheet == null || sheet.Dimension == null)
                    throw new InvalidOperationException("The workbook does not contain a recognisable job P&L sheet.");

                int headerRow = FindHeaderRow(sheet);
                Dictionary<string, int> columns = ReadHeaders(sheet, headerRow);
                RequireHeader(columns, "Customer");
                RequireHeader(columns, "Invoice No.");

                var preview = new ProfitabilityImportPreview
                {
                    SourceFileName = Path.GetFileName(filePath),
                    WorksheetName = sheet.Name
                };

                for (int rowNumber = headerRow + 1; rowNumber <= sheet.Dimension.End.Row; rowNumber++)
                {
                    string customer = Text(sheet, rowNumber, Column(columns, "Customer"));
                    string invoiceNumber = Text(sheet, rowNumber, Column(columns, "Invoice No."));
                    if (string.IsNullOrWhiteSpace(customer) && string.IsNullOrWhiteSpace(invoiceNumber))
                        continue;

                    object rawRevenue = Value(sheet, rowNumber, Column(columns, "Invoice Amount"));
                    var row = new ProfitabilityImportRow
                    {
                        SourceRowNumber = rowNumber,
                        CustomerName = customer,
                        Description = Text(sheet, rowNumber, Column(columns, "Description")),
                        QuotationNumber = Text(sheet, rowNumber, Column(columns, "Quotation No.")),
                        QuotationAmount = DecimalValue(Value(sheet, rowNumber, Column(columns, "Quotation Amount"))),
                        CustomerPoNumber = Text(sheet, rowNumber, Column(columns, "P.O. Number")),
                        CustomerPoAmount = DecimalValue(Value(sheet, rowNumber, Column(columns, "PO Amount"))),
                        InvoiceNumber = invoiceNumber,
                        InvoiceDate = DateValue(Value(sheet, rowNumber, Column(columns, "Inv. Date"))),
                        TaxableRevenue = DecimalValue(rawRevenue),
                        VendorName = Text(sheet, rowNumber, Column(columns, "Vender Name")),
                        VendorCost = DecimalValue(Value(sheet, rowNumber, Column(columns, "Vender Rate"))),
                        WorkStatus = NormalizeStatus(Text(sheet, rowNumber, Column(columns, "Work Status"))),
                        PaymentStatus = NormalizePaymentStatus(Text(sheet, rowNumber, Column(columns, "Client Payments")))
                    };

                    string invoiceKey = NormalizeReference(invoiceNumber);
                    Invoice invoice;
                    if (!string.IsNullOrWhiteSpace(invoiceKey) && invoiceByNumber.TryGetValue(invoiceKey, out invoice))
                    {
                        row.MatchedInvoiceId = invoice.InvoiceID;
                        Job job = jobs.FirstOrDefault(j => j.InvoiceId == invoice.InvoiceID);
                        row.MatchedJobId = job == null ? (int?)null : job.JobID;
                    }

                    List<string> issues = Review(row, rawRevenue);
                    row.ReviewStatus = issues.Count == 0 && row.MatchedInvoiceId.HasValue ? "Matched" : "Review";
                    row.ReviewMessage = issues.Count == 0
                        ? (row.MatchedInvoiceId.HasValue ? "Invoice matched." : "Invoice not found in ServoERP.")
                        : string.Join(" ", issues);
                    if (!row.MatchedInvoiceId.HasValue && issues.All(i => i != "Invoice not found in ServoERP."))
                        row.ReviewMessage = (row.ReviewMessage + " Invoice not found in ServoERP.").Trim();

                    preview.Rows.Add(row);
                }

                preview.ValidRowCount = preview.Rows.Count;
                preview.MatchedRowCount = preview.Rows.Count(r => r.ReviewStatus == "Matched");
                preview.ReviewRowCount = preview.Rows.Count - preview.MatchedRowCount;
                return preview;
            }
        }

        private static ExcelWorksheet FindProfitabilitySheet(ExcelPackage package)
        {
            return package.Workbook.Worksheets.FirstOrDefault(s =>
                       s.Name.IndexOf("26-27", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.Name.IndexOf("P and L", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       s.Name.IndexOf("P&L", StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? package.Workbook.Worksheets.FirstOrDefault(s => s.Dimension != null && FindHeaderRow(s) > 0);
        }

        private static int FindHeaderRow(ExcelWorksheet sheet)
        {
            int limit = Math.Min(sheet.Dimension?.End.Row ?? 0, 20);
            for (int row = 1; row <= limit; row++)
            {
                bool hasCustomer = false;
                bool hasInvoice = false;
                for (int col = 1; col <= Math.Min(sheet.Dimension.End.Column, 40); col++)
                {
                    string value = Convert.ToString(sheet.Cells[row, col].Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    hasCustomer |= value.Trim().Equals("Customer", StringComparison.OrdinalIgnoreCase);
                    hasInvoice |= value.IndexOf("Invoice Amount", StringComparison.OrdinalIgnoreCase) >= 0 || value.Trim().Equals("Invoice No.", StringComparison.OrdinalIgnoreCase);
                }
                if (hasCustomer && hasInvoice)
                    return row;
            }
            return 0;
        }

        private static Dictionary<string, int> ReadHeaders(ExcelWorksheet sheet, int headerRow)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= sheet.Dimension.End.Column; col++)
            {
                string header = Convert.ToString(sheet.Cells[headerRow, col].Value, CultureInfo.InvariantCulture)?.Trim();
                if (!string.IsNullOrWhiteSpace(header) && !result.ContainsKey(header))
                    result.Add(header, col);
            }
            return result;
        }

        private static int Column(Dictionary<string, int> map, string name)
        {
            int column;
            return map.TryGetValue(name, out column) ? column : 0;
        }

        private static void RequireHeader(Dictionary<string, int> map, string name)
        {
            if (Column(map, name) == 0)
                throw new InvalidOperationException("Required column missing: " + name);
        }

        private static object Value(ExcelWorksheet sheet, int row, int column) => column <= 0 ? null : sheet.Cells[row, column].Value;
        private static string Text(ExcelWorksheet sheet, int row, int column) => Convert.ToString(Value(sheet, row, column), CultureInfo.InvariantCulture)?.Trim();

        private static decimal? DecimalValue(object value)
        {
            if (value == null || value is DateTime)
                return null;
            decimal parsed;
            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : (decimal?)null;
        }

        private static DateTime? DateValue(object value)
        {
            if (value is DateTime)
                return (DateTime)value;
            double serial;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out serial) && serial > 1d)
                return DateTime.FromOADate(serial);
            DateTime parsed;
            return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out parsed) ? parsed : (DateTime?)null;
        }

        private static List<string> Review(ProfitabilityImportRow row, object rawRevenue)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(row.InvoiceNumber)) issues.Add("Invoice number is missing.");
            if (!row.InvoiceDate.HasValue) issues.Add("Invoice date is missing or invalid.");
            if (rawRevenue is DateTime) issues.Add("Invoice amount contains a date.");
            else if (!row.TaxableRevenue.HasValue || row.TaxableRevenue <= 0m) issues.Add("Taxable invoice amount is missing.");
            if (!row.VendorCost.HasValue || row.VendorCost <= 0m) issues.Add("Direct cost is incomplete.");
            return issues;
        }

        private static string NormalizeReference(string value) => Regex.Replace((value ?? string.Empty).ToUpperInvariant(), "[^A-Z0-9]", string.Empty);

        private static string NormalizeStatus(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            return normalized.Equals("completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : normalized;
        }

        private static string NormalizePaymentStatus(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Equals("paid", StringComparison.OrdinalIgnoreCase)) return "Paid";
            if (normalized.Equals("unpaid", StringComparison.OrdinalIgnoreCase) || normalized.Equals("upaid", StringComparison.OrdinalIgnoreCase)) return "Unpaid";
            return normalized;
        }
    }
}
