using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Validation;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace HVAC_Pro_Desktop.Services
{
    public enum ExcelImportModule
    {
        Quotations,
        Invoices,
        Payments,
        Purchases,
        Jobs,
        Clients,
        Employees,
        Vendors,
        Sites
    }

    public sealed class ExcelImportResult
    {
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    public class ExcelImportService
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly ImportReviewService _importReview = new ImportReviewService();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();

        public void CreateTemplate(ExcelImportModule module, string filePath)
        {
            SessionManager.DemandPermission(ModuleKey(module), "View");
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add(module.ToString());
                string[] headers = GetHeaders(module);
                string[] sample = GetSampleRow(module);

                for (int i = 0; i < headers.Length; i++)
                {
                    sheet.Cells[1, i + 1].Value = headers[i];
                    sheet.Cells[2, i + 1].Value = i < sample.Length ? sample[i] : string.Empty;
                }

                using (var range = sheet.Cells[1, 1, 1, headers.Length])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(219, 234, 254));
                }

                sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory);
                package.SaveAs(new FileInfo(filePath));
            }
        }

        public ExcelImportResult Import(ExcelImportModule module, string filePath)
        {
            SessionManager.DemandPermission(ModuleKey(module), "Create");
            var result = new ExcelImportResult();
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                var sheet = package.Workbook.Worksheets.FirstOrDefault();
                if (sheet == null || sheet.Dimension == null)
                    return result;

                Dictionary<string, int> map = BuildColumnMap(sheet);
                ValidationResult headerReview = _importReview.ReviewHeaders(module.ToString(), map.Keys, GetRequiredHeaders(module));
                _validation.EnsureValid(headerReview, "Import header validation failed");
                for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                {
                    try
                    {
                        if (IsRowEmpty(sheet, row, map.Values))
                            continue;

                        bool imported = false;
                        switch (module)
                        {
                            case ExcelImportModule.Quotations:
                                imported = ImportQuotationRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Invoices:
                                imported = ImportInvoiceRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Payments:
                                imported = ImportPaymentRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Purchases:
                                imported = ImportPurchaseRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Jobs:
                                imported = ImportJobRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Clients:
                                imported = ImportClientRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Employees:
                                imported = ImportEmployeeRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Vendors:
                                imported = ImportVendorRow(conn, sheet, map, row, result);
                                break;
                            case ExcelImportModule.Sites:
                                imported = ImportSiteRow(conn, sheet, map, row, result);
                                break;
                        }

                        if (imported)
                            result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.Errors.Add("Row " + row + " - " + ex.Message);
                    }
                }
            }

            AppLogger.LogInfo("Excel import completed for " + module + " | success=" + result.SuccessCount + " | skipped=" + result.SkippedCount);
            return result;
        }

        private static Dictionary<string, int> BuildColumnMap(ExcelWorksheet sheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= sheet.Dimension.End.Column; col++)
            {
                string key = Convert.ToString(sheet.Cells[1, col].Value)?.Trim();
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                    map[key] = col;
            }

            return map;
        }

        private static bool IsRowEmpty(ExcelWorksheet sheet, int row, IEnumerable<int> columns)
        {
            foreach (int col in columns)
            {
                if (!string.IsNullOrWhiteSpace(Convert.ToString(sheet.Cells[row, col].Value)))
                    return false;
            }

            return true;
        }

        private static string GetCell(ExcelWorksheet sheet, int row, Dictionary<string, int> map, string header)
        {
            return map.ContainsKey(header) ? Convert.ToString(sheet.Cells[row, map[header]].Value)?.Trim() ?? string.Empty : string.Empty;
        }

        private static decimal GetDecimal(ExcelWorksheet sheet, int row, Dictionary<string, int> map, string header)
        {
            decimal value;
            return decimal.TryParse(GetCell(sheet, row, map, header), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                || decimal.TryParse(GetCell(sheet, row, map, header), NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                ? value
                : 0m;
        }

        private static DateTime GetDate(ExcelWorksheet sheet, int row, Dictionary<string, int> map, string header)
        {
            var raw = GetCell(sheet, row, map, header);
            DateTime value;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ||
                DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out value))
                return value;
            return DateTime.Today;
        }

        private static bool AddError(ExcelImportResult result, int row, string reason)
        {
            result.SkippedCount++;
            result.Errors.Add("Row " + row + " - " + reason);
            return false;
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private int? FindClientId(SqlConnection conn, string clientName)
        {
            return GetScalarInt(conn, "SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=@name AND ISNULL(IsActive,1)=1", new SqlParameter("@name", clientName));
        }

        private int? FindSiteId(SqlConnection conn, int clientId, string siteName)
        {
            return GetScalarInt(conn, "SELECT TOP 1 SiteID FROM ClientSites WHERE ClientID=@clientId AND SiteName=@siteName",
                new SqlParameter("@clientId", clientId),
                new SqlParameter("@siteName", siteName));
        }

        private int? FindVendorId(SqlConnection conn, string vendorName)
        {
            return GetScalarInt(conn, "SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=@name AND ISNULL(IsArchived,0)=0",
                new SqlParameter("@name", vendorName));
        }

        private int? FindEmployeeIdByName(SqlConnection conn, string employeeName)
        {
            return GetScalarInt(conn, "SELECT TOP 1 EmployeeID FROM Employees WHERE Name=@name OR EmployeeCode=@name",
                new SqlParameter("@name", employeeName));
        }

        private static int? GetScalarInt(SqlConnection conn, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
            }
        }

        private static void Execute(SqlConnection conn, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        private static string ModuleKey(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations: return "Quotations";
                case ExcelImportModule.Invoices: return "Invoices";
                case ExcelImportModule.Payments: return "Payments";
                case ExcelImportModule.Purchases: return "Purchases";
                case ExcelImportModule.Jobs: return "WorkOrders";
                case ExcelImportModule.Clients: return "Clients";
                case ExcelImportModule.Employees: return "Employees";
                case ExcelImportModule.Vendors: return "Vendors";
                case ExcelImportModule.Sites: return "Clients";
                default: return "Settings";
            }
        }

        private bool ImportQuotationRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string quotationNumber = GetCell(sheet, row, map, "QuotationNumber");
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string description = GetCell(sheet, row, map, "Description");
            decimal amount = GetDecimal(sheet, row, map, "Amount");
            string status = NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Draft";
            DateTime quotationDate = GetDate(sheet, row, map, "QuotationDate");
            DateTime validUntil = GetDate(sheet, row, map, "ValidUntil");

            if (string.IsNullOrWhiteSpace(quotationNumber))
                return AddError(result, row, "Missing required field: QuotationNumber");
            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");

            int? clientId = FindClientId(conn, clientName);
            if (!clientId.HasValue)
                return AddError(result, row, "Client not found: " + clientName);
            int? siteId = string.IsNullOrWhiteSpace(siteName) ? (int?)null : FindSiteId(conn, clientId.Value, siteName);
            if (!string.IsNullOrWhiteSpace(siteName) && !siteId.HasValue)
                return AddError(result, row, "Site not found: " + siteName);

            int? existingId = GetScalarInt(conn, "SELECT TOP 1 BidID FROM TenderBids WHERE QuotationNumber = @number", new SqlParameter("@number", quotationNumber));
            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE TenderBids SET ClientID=@clientId, SiteID=@siteId, TenderName=@title, BidValue=@value,
SubmittedDate=@quotationDate, DueDate=@validUntil, Status=@status, Notes=@notes,
TotalTaxableValue=@value, TotalWithGST=@value
WHERE BidID=@id",
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@title", (object)description ?? DBNull.Value),
                    new SqlParameter("@value", amount),
                    new SqlParameter("@quotationDate", quotationDate),
                    new SqlParameter("@validUntil", validUntil),
                    new SqlParameter("@status", status),
                    new SqlParameter("@notes", (object)description ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                Execute(conn, @"
INSERT INTO TenderBids (QuotationNumber, ClientID, SiteID, TenderName, BidValue, SubmittedDate, DueDate, Status, Notes, TotalTaxableValue, TotalWithGST)
VALUES (@number,@clientId,@siteId,@title,@value,@quotationDate,@validUntil,@status,@notes,@value,@value)",
                    new SqlParameter("@number", quotationNumber),
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@title", (object)description ?? DBNull.Value),
                    new SqlParameter("@value", amount),
                    new SqlParameter("@quotationDate", quotationDate),
                    new SqlParameter("@validUntil", validUntil),
                    new SqlParameter("@status", status),
                    new SqlParameter("@notes", (object)description ?? DBNull.Value));
            }

            return true;
        }

        private bool ImportInvoiceRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string invoiceNumber = GetCell(sheet, row, map, "InvoiceNumber");
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string description = GetCell(sheet, row, map, "Description");
            decimal amount = GetDecimal(sheet, row, map, "Amount");
            decimal taxAmount = GetDecimal(sheet, row, map, "TaxAmount");
            decimal totalAmount = GetDecimal(sheet, row, map, "TotalAmount");
            string status = NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Draft";
            DateTime invoiceDate = GetDate(sheet, row, map, "InvoiceDate");
            DateTime dueDate = GetDate(sheet, row, map, "DueDate");

            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return AddError(result, row, "Missing required field: InvoiceNumber");
            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");

            int? clientId = FindClientId(conn, clientName);
            if (!clientId.HasValue)
                return AddError(result, row, "Client not found: " + clientName);
            int? siteId = string.IsNullOrWhiteSpace(siteName) ? (int?)null : FindSiteId(conn, clientId.Value, siteName);
            if (!string.IsNullOrWhiteSpace(siteName) && !siteId.HasValue)
                return AddError(result, row, "Site not found: " + siteName);

            int? existingId = GetScalarInt(conn, "SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=@number", new SqlParameter("@number", invoiceNumber));
            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE Invoices SET ClientID=@clientId, SiteID=@siteId, InvoiceDate=@invoiceDate, DueDate=@dueDate,
Subject=@subject, SubTotal=@amount, TaxAmount=@taxAmount, TotalAmount=@totalAmount, PaymentStatus=@status
WHERE InvoiceID=@id",
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@invoiceDate", invoiceDate),
                    new SqlParameter("@dueDate", dueDate),
                    new SqlParameter("@subject", (object)description ?? DBNull.Value),
                    new SqlParameter("@amount", amount),
                    new SqlParameter("@taxAmount", taxAmount),
                    new SqlParameter("@totalAmount", totalAmount),
                    new SqlParameter("@status", status),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                Execute(conn, @"
INSERT INTO Invoices (InvoiceNumber, ClientID, SiteID, InvoiceDate, DueDate, Subject, SubTotal, TaxAmount, TotalAmount, PaymentStatus)
VALUES (@number,@clientId,@siteId,@invoiceDate,@dueDate,@subject,@amount,@taxAmount,@totalAmount,@status)",
                    new SqlParameter("@number", invoiceNumber),
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@invoiceDate", invoiceDate),
                    new SqlParameter("@dueDate", dueDate),
                    new SqlParameter("@subject", (object)description ?? DBNull.Value),
                    new SqlParameter("@amount", amount),
                    new SqlParameter("@taxAmount", taxAmount),
                    new SqlParameter("@totalAmount", totalAmount),
                    new SqlParameter("@status", status));
            }

            return true;
        }

        private bool ImportPaymentRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string invoiceNumber = GetCell(sheet, row, map, "InvoiceNumber");
            string clientName = GetCell(sheet, row, map, "ClientName");
            decimal amountPaid = GetDecimal(sheet, row, map, "AmountPaid");
            string paymentMode = NullIfEmpty(GetCell(sheet, row, map, "PaymentMode")) ?? "Bank Transfer";
            string referenceNumber = GetCell(sheet, row, map, "ReferenceNumber");
            string notes = GetCell(sheet, row, map, "Notes");
            DateTime paymentDate = GetDate(sheet, row, map, "PaymentDate");

            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return AddError(result, row, "Missing required field: InvoiceNumber");

            int? invoiceId = GetScalarInt(conn, "SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=@number", new SqlParameter("@number", invoiceNumber));
            if (!invoiceId.HasValue)
                return AddError(result, row, "Invoice not found: " + invoiceNumber);
            int? clientId = !string.IsNullOrWhiteSpace(clientName)
                ? FindClientId(conn, clientName)
                : GetScalarInt(conn, "SELECT TOP 1 ClientID FROM Invoices WHERE InvoiceID=@id", new SqlParameter("@id", invoiceId.Value));
            if (!clientId.HasValue)
                return AddError(result, row, "Client not found: " + clientName);

            int? existingId = GetScalarInt(conn, @"
SELECT TOP 1 PaymentID
FROM Payments
WHERE InvoiceID=@invoiceId AND PaymentDate=@paymentDate AND AmountPaid=@amountPaid
  AND ISNULL(ReferenceNumber,'') = ISNULL(@referenceNumber,'')",
                new SqlParameter("@invoiceId", invoiceId.Value),
                new SqlParameter("@paymentDate", paymentDate),
                new SqlParameter("@amountPaid", amountPaid),
                new SqlParameter("@referenceNumber", (object)referenceNumber ?? DBNull.Value));

            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE Payments SET ClientID=@clientId, PaymentMode=@paymentMode, Notes=@notes
WHERE PaymentID=@id",
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@paymentMode", paymentMode),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                string paymentNumber = "PAY-IMP-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + row.ToString(CultureInfo.InvariantCulture);
                Execute(conn, @"
INSERT INTO Payments (PaymentNumber, InvoiceID, ClientID, AmountPaid, PaymentDate, PaymentMode, ReferenceNumber, Notes)
VALUES (@paymentNumber,@invoiceId,@clientId,@amountPaid,@paymentDate,@paymentMode,@referenceNumber,@notes)",
                    new SqlParameter("@paymentNumber", paymentNumber),
                    new SqlParameter("@invoiceId", invoiceId.Value),
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@amountPaid", amountPaid),
                    new SqlParameter("@paymentDate", paymentDate),
                    new SqlParameter("@paymentMode", paymentMode),
                    new SqlParameter("@referenceNumber", (object)referenceNumber ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value));
            }

            return true;
        }

        private bool ImportPurchaseRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string vendorName = GetCell(sheet, row, map, "VendorName");
            string itemDescription = GetCell(sheet, row, map, "ItemDescription");
            decimal quantity = GetDecimal(sheet, row, map, "Quantity");
            decimal unitPrice = GetDecimal(sheet, row, map, "UnitPrice");
            decimal totalAmount = GetDecimal(sheet, row, map, "TotalAmount");
            string notes = GetCell(sheet, row, map, "Notes");
            DateTime purchaseDate = GetDate(sheet, row, map, "PurchaseDate");

            if (string.IsNullOrWhiteSpace(vendorName))
                return AddError(result, row, "Missing required field: VendorName");

            int? vendorId = FindVendorId(conn, vendorName);
            if (!vendorId.HasValue)
                return AddError(result, row, "Vendor not found: " + vendorName);

            int? existingPoId = GetScalarInt(conn, @"
SELECT TOP 1 po.POID
FROM PurchaseOrders po
LEFT JOIN PurchaseLineItems li ON li.POID = po.POID
WHERE po.VendorID=@vendorId AND CAST(po.PODate AS DATE)=@poDate
  AND ISNULL(li.Description,'') = ISNULL(@item,'')
ORDER BY po.POID DESC",
                new SqlParameter("@vendorId", vendorId.Value),
                new SqlParameter("@poDate", purchaseDate.Date),
                new SqlParameter("@item", itemDescription ?? string.Empty));

            if (existingPoId.HasValue)
            {
                Execute(conn, "UPDATE PurchaseOrders SET TotalAmount=@total, Notes=@notes WHERE POID=@id",
                    new SqlParameter("@total", totalAmount),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@id", existingPoId.Value));

                Execute(conn, @"
UPDATE TOP (1) PurchaseLineItems
SET Description=@description, Quantity=@qty, Rate=@rate, Amount=@amount
WHERE POID=@id",
                    new SqlParameter("@description", (object)itemDescription ?? DBNull.Value),
                    new SqlParameter("@qty", quantity),
                    new SqlParameter("@rate", unitPrice),
                    new SqlParameter("@amount", totalAmount),
                    new SqlParameter("@id", existingPoId.Value));
            }
            else
            {
                string poNumber = "PO-IMP-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + row.ToString(CultureInfo.InvariantCulture);
                int poId;
                using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO PurchaseOrders (VendorID, PONumber, PODate, PayByDate, TotalAmount, Status, Notes)
VALUES (@vendorId,@poNumber,@poDate,DATEADD(day,30,@poDate),@totalAmount,'Pending',@notes);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@vendorId", vendorId.Value);
                    cmd.Parameters.AddWithValue("@poNumber", poNumber);
                    cmd.Parameters.AddWithValue("@poDate", purchaseDate);
                    cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                    cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                    poId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                Execute(conn, @"
INSERT INTO PurchaseLineItems (POID, Description, Quantity, UOM, Rate, Amount)
VALUES (@poId,@description,@qty,'Nos',@rate,@amount)",
                    new SqlParameter("@poId", poId),
                    new SqlParameter("@description", (object)itemDescription ?? DBNull.Value),
                    new SqlParameter("@qty", quantity),
                    new SqlParameter("@rate", unitPrice),
                    new SqlParameter("@amount", totalAmount));
            }

            return true;
        }

        private bool ImportJobRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string technicianName = GetCell(sheet, row, map, "TechnicianName");
            string jobType = NullIfEmpty(GetCell(sheet, row, map, "JobType")) ?? "General";
            string description = GetCell(sheet, row, map, "Description");
            string status = NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Pending";
            string priority = NullIfEmpty(GetCell(sheet, row, map, "Priority")) ?? "Medium";
            DateTime scheduledDate = GetDate(sheet, row, map, "ScheduledDate");

            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");
            if (string.IsNullOrWhiteSpace(siteName))
                return AddError(result, row, "Missing required field: SiteName");

            int? clientId = FindClientId(conn, clientName);
            if (!clientId.HasValue)
                return AddError(result, row, "Client not found: " + clientName);
            int? siteId = FindSiteId(conn, clientId.Value, siteName);
            if (!siteId.HasValue)
                return AddError(result, row, "Site not found: " + siteName);
            int? employeeId = string.IsNullOrWhiteSpace(technicianName) ? (int?)null : FindEmployeeIdByName(conn, technicianName);
            if (!string.IsNullOrWhiteSpace(technicianName) && !employeeId.HasValue)
                return AddError(result, row, "Employee not found: " + technicianName);

            int? existingId = GetScalarInt(conn, @"
SELECT TOP 1 JobID FROM Jobs
WHERE ClientID=@clientId AND SiteID=@siteId AND CAST(ScheduledDate AS DATE)=@scheduledDate
  AND ISNULL(Description,'')=ISNULL(@description,'')",
                new SqlParameter("@clientId", clientId.Value),
                new SqlParameter("@siteId", siteId.Value),
                new SqlParameter("@scheduledDate", scheduledDate.Date),
                new SqlParameter("@description", (object)description ?? DBNull.Value));

            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE Jobs SET AssignedEmployeeID=@employeeId, JobType=@jobType, Title=@title, JobTitle=@title,
Description=@description, Status=@status, Priority=@priority, ScheduledDate=@scheduledDate
WHERE JobID=@id",
                    new SqlParameter("@employeeId", (object)employeeId ?? DBNull.Value),
                    new SqlParameter("@jobType", jobType),
                    new SqlParameter("@title", (object)description ?? DBNull.Value),
                    new SqlParameter("@description", (object)description ?? DBNull.Value),
                    new SqlParameter("@status", status),
                    new SqlParameter("@priority", priority),
                    new SqlParameter("@scheduledDate", scheduledDate),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                string jobNumber = "JOB-IMP-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + row.ToString(CultureInfo.InvariantCulture);
                Execute(conn, @"
INSERT INTO Jobs (JobNumber, ClientID, SiteID, Title, JobTitle, Description, AssignedEmployeeID, ScheduledDate, Priority, Status, JobType)
VALUES (@jobNumber,@clientId,@siteId,@title,@title,@description,@employeeId,@scheduledDate,@priority,@status,@jobType)",
                    new SqlParameter("@jobNumber", jobNumber),
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@siteId", siteId.Value),
                    new SqlParameter("@title", (object)description ?? DBNull.Value),
                    new SqlParameter("@description", (object)description ?? DBNull.Value),
                    new SqlParameter("@employeeId", (object)employeeId ?? DBNull.Value),
                    new SqlParameter("@scheduledDate", scheduledDate),
                    new SqlParameter("@priority", priority),
                    new SqlParameter("@status", status),
                    new SqlParameter("@jobType", jobType));
            }

            return true;
        }

        private bool ImportClientRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string clientName = GetCell(sheet, row, map, "ClientName");
            string contactPerson = GetCell(sheet, row, map, "ContactPerson");
            string phone = GetCell(sheet, row, map, "Phone");
            string email = GetCell(sheet, row, map, "Email");
            string address = GetCell(sheet, row, map, "Address");
            string city = GetCell(sheet, row, map, "City");
            string state = GetCell(sheet, row, map, "State");
            string gstin = GetCell(sheet, row, map, "GSTIN");
            string notes = GetCell(sheet, row, map, "Notes");

            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");

            string finalAddress = string.IsNullOrWhiteSpace(state) ? address : (address + (string.IsNullOrWhiteSpace(address) ? string.Empty : ", ") + state);
            int? existingId = GetScalarInt(conn, "SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=@name", new SqlParameter("@name", clientName));
            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE B2BClients
SET PrimaryContact=@contact, Phone=@phone, Email=@email, BillingAddress=@address, City=@city, GSTNumber=@gstin, IndustryType=COALESCE(NULLIF(@notes,''), IndustryType)
WHERE ClientID=@id",
                    new SqlParameter("@contact", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)finalAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                Execute(conn, @"
INSERT INTO B2BClients (CompanyName, PrimaryContact, Phone, Email, BillingAddress, City, GSTNumber, IsActive, CustomerSince)
VALUES (@name,@contact,@phone,@email,@address,@city,@gstin,1,GETDATE())",
                    new SqlParameter("@name", clientName),
                    new SqlParameter("@contact", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)finalAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value));
            }

            return true;
        }

        private bool ImportEmployeeRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string employeeCode = GetCell(sheet, row, map, "EmployeeCode");
            string employeeName = GetCell(sheet, row, map, "EmployeeName");
            string designation = GetCell(sheet, row, map, "Designation");
            string department = GetCell(sheet, row, map, "Department");
            string phone = GetCell(sheet, row, map, "Phone");
            string whatsApp = GetCell(sheet, row, map, "WhatsApp");
            string bloodGroup = GetCell(sheet, row, map, "BloodGroup");
            string aadhaar = GetCell(sheet, row, map, "Aadhaar");
            string pan = GetCell(sheet, row, map, "PAN");
            string status = NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Active";
            DateTime joiningDate = GetDate(sheet, row, map, "JoiningDate");

            if (string.IsNullOrWhiteSpace(employeeCode))
                return AddError(result, row, "Missing required field: EmployeeCode");
            if (string.IsNullOrWhiteSpace(employeeName))
                return AddError(result, row, "Missing required field: EmployeeName");

            int? existingId = GetScalarInt(conn, "SELECT TOP 1 EmployeeID FROM Employees WHERE EmployeeCode=@code", new SqlParameter("@code", employeeCode));
            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE Employees SET Name=@name, Designation=@designation, Department=@department, Phone=@phone,
WhatsAppNumber=@whatsapp, BloodGroup=@bloodGroup, AadhaarNumber=@aadhaar, PANNumber=@pan, JoiningDate=@joiningDate, Status=@status
WHERE EmployeeID=@id",
                    new SqlParameter("@name", employeeName),
                    new SqlParameter("@designation", (object)designation ?? DBNull.Value),
                    new SqlParameter("@department", (object)department ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@whatsapp", (object)whatsApp ?? DBNull.Value),
                    new SqlParameter("@bloodGroup", (object)bloodGroup ?? DBNull.Value),
                    new SqlParameter("@aadhaar", (object)aadhaar ?? DBNull.Value),
                    new SqlParameter("@pan", (object)pan ?? DBNull.Value),
                    new SqlParameter("@joiningDate", joiningDate),
                    new SqlParameter("@status", status),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                Execute(conn, @"
INSERT INTO Employees (EmployeeCode, Name, Designation, Department, Phone, WhatsAppNumber, BloodGroup, AadhaarNumber, PANNumber, JoiningDate, Status)
VALUES (@code,@name,@designation,@department,@phone,@whatsapp,@bloodGroup,@aadhaar,@pan,@joiningDate,@status)",
                    new SqlParameter("@code", employeeCode),
                    new SqlParameter("@name", employeeName),
                    new SqlParameter("@designation", (object)designation ?? DBNull.Value),
                    new SqlParameter("@department", (object)department ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@whatsapp", (object)whatsApp ?? DBNull.Value),
                    new SqlParameter("@bloodGroup", (object)bloodGroup ?? DBNull.Value),
                    new SqlParameter("@aadhaar", (object)aadhaar ?? DBNull.Value),
                    new SqlParameter("@pan", (object)pan ?? DBNull.Value),
                    new SqlParameter("@joiningDate", joiningDate),
                    new SqlParameter("@status", status));
            }

            return true;
        }

        private bool ImportVendorRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string vendorName = GetCell(sheet, row, map, "VendorName");
            string contactPerson = GetCell(sheet, row, map, "ContactPerson");
            string phone = GetCell(sheet, row, map, "Phone");
            string email = GetCell(sheet, row, map, "Email");
            string address = GetCell(sheet, row, map, "Address");
            string city = GetCell(sheet, row, map, "City");
            string gstin = GetCell(sheet, row, map, "GSTIN");
            string notes = GetCell(sheet, row, map, "Notes");

            if (string.IsNullOrWhiteSpace(vendorName))
                return AddError(result, row, "Missing required field: VendorName");

            int? existingId = GetScalarInt(conn, "SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=@name", new SqlParameter("@name", vendorName));
            if (existingId.HasValue)
            {
                Execute(conn, @"
UPDATE Vendors SET Phone=@phone, Email=@email, Address=@address, City=@city, GSTNumber=@gstin,
Notes=@notes, Category=COALESCE(NULLIF(@contactPerson,''), Category)
WHERE VendorID=@id",
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)address ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@contactPerson", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                Execute(conn, @"
INSERT INTO Vendors (VendorName, Phone, Email, Address, City, GSTNumber, Notes, Category, IsActive)
VALUES (@name,@phone,@email,@address,@city,@gstin,@notes,@category,1)",
                    new SqlParameter("@name", vendorName),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)address ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@category", (object)contactPerson ?? DBNull.Value));
            }

            return true;
        }

        private bool ImportSiteRow(SqlConnection conn, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result)
        {
            string siteName = GetCell(sheet, row, map, "SiteName");
            string clientName = GetCell(sheet, row, map, "ClientName");
            string address = GetCell(sheet, row, map, "Address");
            string city = GetCell(sheet, row, map, "City");
            string contactPerson = GetCell(sheet, row, map, "ContactPerson");
            string phone = GetCell(sheet, row, map, "Phone");
            string siteType = GetCell(sheet, row, map, "SiteType");
            string notes = GetCell(sheet, row, map, "Notes");

            if (string.IsNullOrWhiteSpace(siteName))
                return AddError(result, row, "Missing required field: SiteName");
            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");

            int? clientId = FindClientId(conn, clientName);
            if (!clientId.HasValue)
                return AddError(result, row, "Client not found: " + clientName);

            string fullAddress = address;
            if (!string.IsNullOrWhiteSpace(contactPerson) || !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(siteType) || !string.IsNullOrWhiteSpace(notes))
            {
                fullAddress = (fullAddress ?? string.Empty)
                    + (string.IsNullOrWhiteSpace(fullAddress) ? string.Empty : Environment.NewLine)
                    + string.Join(" | ", new[] { contactPerson, phone, siteType, notes }.Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            int? existingId = GetScalarInt(conn, "SELECT TOP 1 SiteID FROM ClientSites WHERE ClientID=@clientId AND SiteName=@siteName",
                new SqlParameter("@clientId", clientId.Value),
                new SqlParameter("@siteName", siteName));

            if (existingId.HasValue)
            {
                Execute(conn, "UPDATE ClientSites SET Address=@address, City=@city WHERE SiteID=@id",
                    new SqlParameter("@address", (object)fullAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                Execute(conn, @"
INSERT INTO ClientSites (ClientID, SiteName, Address, City, ACSystemCount, RefrigerationSystemCount, CoolingTowerCount, IsCritical)
VALUES (@clientId,@siteName,@address,@city,0,0,0,0)",
                    new SqlParameter("@clientId", clientId.Value),
                    new SqlParameter("@siteName", siteName),
                    new SqlParameter("@address", (object)fullAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value));
            }

            return true;
        }

        private static string[] GetHeaders(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    return new[] { "QuotationNumber", "QuotationDate", "ClientName", "SiteName", "Description", "Amount", "Status", "ValidUntil" };
                case ExcelImportModule.Invoices:
                    return new[] { "InvoiceNumber", "InvoiceDate", "ClientName", "SiteName", "Description", "Amount", "TaxAmount", "TotalAmount", "Status", "DueDate" };
                case ExcelImportModule.Payments:
                    return new[] { "PaymentDate", "InvoiceNumber", "ClientName", "AmountPaid", "PaymentMode", "ReferenceNumber", "Notes" };
                case ExcelImportModule.Purchases:
                    return new[] { "PurchaseDate", "VendorName", "ItemDescription", "Quantity", "UnitPrice", "TotalAmount", "Notes" };
                case ExcelImportModule.Jobs:
                    return new[] { "JobDate", "ClientName", "SiteName", "TechnicianName", "JobType", "Description", "Status", "Priority", "ScheduledDate" };
                case ExcelImportModule.Clients:
                    return new[] { "ClientName", "ContactPerson", "Phone", "Email", "Address", "City", "State", "GSTIN", "Notes" };
                case ExcelImportModule.Employees:
                    return new[] { "EmployeeCode", "EmployeeName", "Designation", "Department", "Phone", "WhatsApp", "BloodGroup", "Aadhaar", "PAN", "JoiningDate", "Status" };
                case ExcelImportModule.Vendors:
                    return new[] { "VendorName", "ContactPerson", "Phone", "Email", "Address", "City", "GSTIN", "Notes" };
                case ExcelImportModule.Sites:
                    return new[] { "SiteName", "ClientName", "Address", "City", "ContactPerson", "Phone", "SiteType", "Notes" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string[] GetRequiredHeaders(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    return new[] { "ClientName", "Description", "Amount" };
                case ExcelImportModule.Invoices:
                    return new[] { "InvoiceNumber", "ClientName", "Description", "Amount", "TotalAmount" };
                case ExcelImportModule.Payments:
                    return new[] { "PaymentDate", "InvoiceNumber", "AmountPaid" };
                case ExcelImportModule.Purchases:
                    return new[] { "VendorName", "ItemDescription", "Quantity", "UnitPrice" };
                case ExcelImportModule.Jobs:
                    return new[] { "ClientName", "SiteName", "JobType", "Description" };
                case ExcelImportModule.Clients:
                    return new[] { "ClientName" };
                case ExcelImportModule.Employees:
                    return new[] { "EmployeeCode", "EmployeeName" };
                case ExcelImportModule.Vendors:
                    return new[] { "VendorName" };
                case ExcelImportModule.Sites:
                    return new[] { "SiteName", "ClientName" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string[] GetSampleRow(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    return new[] { "QTN-2026-04-0001", "17/04/2026", "ABC Corp", "Main Plant", "Quarterly AMC quotation", "25000", "Draft", "30/04/2026" };
                case ExcelImportModule.Invoices:
                    return new[] { "INV-2026-04-0001", "17/04/2026", "ABC Corp", "Main Plant", "Service invoice", "10000", "1800", "11800", "Pending", "02/05/2026" };
                case ExcelImportModule.Payments:
                    return new[] { "17/04/2026", "INV-2026-04-0001", "ABC Corp", "11800", "NEFT", "UTR12345", "April collection" };
                case ExcelImportModule.Purchases:
                    return new[] { "17/04/2026", "Cool Parts Pvt Ltd", "Copper pipe", "10", "500", "5000", "Urgent stock" };
                case ExcelImportModule.Jobs:
                    return new[] { "17/04/2026", "ABC Corp", "Main Plant", "Ramesh Patil", "PM Visit", "Quarterly maintenance", "Pending", "Medium", "18/04/2026" };
                case ExcelImportModule.Clients:
                    return new[] { "ABC Corp", "Anita Sharma", "9876543210", "ops@abccorp.in", "Thane MIDC", "Thane", "Maharashtra", "27ABCDE1234F1Z5", "Priority account" };
                case ExcelImportModule.Employees:
                    return new[] { "MSE001", "Ramesh Patil", "Technician", "Service", "9876543210", "9876543210", "B+", "123412341234", "ABCDE1234F", "01/04/2026", "Active" };
                case ExcelImportModule.Vendors:
                    return new[] { "Cool Parts Pvt Ltd", "Rajesh Shah", "9988776655", "sales@coolparts.in", "Bhiwandi", "Mumbai", "27ABCDE1234F1Z5", "Fast supplier" };
                case ExcelImportModule.Sites:
                    return new[] { "Main Plant", "ABC Corp", "Industrial Area", "Thane", "Anita Sharma", "9876543210", "Industrial", "Primary service site" };
                default:
                    return Array.Empty<string>();
            }
        }
    }
}

