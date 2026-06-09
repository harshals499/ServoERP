using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.DAL;
using OfficeOpenXml;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class AutomatedImportResult
    {
        public int BatchId { get; set; }
        public ExcelImportModule DetectedModule { get; set; }
        public string DetectedSheetName { get; set; }
        public int DetectionConfidence { get; set; }
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public string SummaryTitle { get; set; }
        public List<string> UserMessages { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> CreatedDefaults { get; } = new List<string>();
        public Dictionary<string, string> ColumnMappings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ExcelImportExecutionOptions
    {
        public bool SkipPreflight { get; set; } = true;
        public bool AutoResolveReferences { get; set; } = true;
        public bool UseTransaction { get; set; } = true;
        public ExcelImportDiagnostics Diagnostics { get; set; }
        public string QuotationImportDirection { get; set; }
    }

    public sealed class ExcelImportDiagnostics
    {
        public HashSet<string> CreatedClients { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CreatedVendors { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CreatedSites { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CreatedEmployees { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DuplicateRefreshes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> NormalizedStatuses { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> NormalizedUnits { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public List<string> BuildSummaryLines()
        {
            var lines = new List<string>();
            if (CreatedClients.Count > 0) lines.Add("Created clients: " + string.Join(", ", CreatedClients.OrderBy(x => x)));
            if (CreatedVendors.Count > 0) lines.Add("Created vendors: " + string.Join(", ", CreatedVendors.OrderBy(x => x)));
            if (CreatedSites.Count > 0) lines.Add("Created sites: " + string.Join(", ", CreatedSites.OrderBy(x => x)));
            if (CreatedEmployees.Count > 0) lines.Add("Created employees: " + string.Join(", ", CreatedEmployees.OrderBy(x => x)));
            if (DuplicateRefreshes.Count > 0) lines.Add("Updated existing records: " + string.Join(", ", DuplicateRefreshes.OrderBy(x => x).Take(30)));
            if (NormalizedStatuses.Count > 0) lines.Add("Normalized statuses: " + string.Join(", ", NormalizedStatuses.OrderBy(x => x).Take(30)));
            if (NormalizedUnits.Count > 0) lines.Add("Normalized units: " + string.Join(", ", NormalizedUnits.OrderBy(x => x).Take(30)));
            return lines;
        }
    }

    public sealed class MasterDataIngestionPipeline
    {
        private readonly ExcelFileReaderService _reader = new ExcelFileReaderService();
        private readonly DataTypeDetectionService _detector = new DataTypeDetectionService();
        private readonly AutoColumnMappingService _mapper = new AutoColumnMappingService();
        private readonly DataCleaningService _cleaner = new DataCleaningService();
        private readonly ImportAuditLogService _audit = new ImportAuditLogService();
        private readonly ExcelImportService _importService = new ExcelImportService();

        public AutomatedImportResult ImportFile(string filePath, ExcelImportModule? preferredModule = null)
        {
            return ImportFile(filePath, preferredModule, null);
        }

        /// <summary>Imports a workbook and applies optional quotation workflow direction selected by the user.</summary>
        public AutomatedImportResult ImportFile(string filePath, ExcelImportModule? preferredModule, string quotationImportDirection)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Import file was not found.", filePath);

            ExcelWorkbookImportData workbook = _reader.Read(filePath);
            DataTypeDetectionResult detection = _detector.Detect(workbook, preferredModule);
            if (detection == null || detection.Sheet == null)
                throw new InvalidOperationException("ServoERP could not understand this Excel file. Please use a workbook with clear column headings such as Client Name, Vendor Name, Invoice Number, Job Type, or Item Name.");

            ColumnMappingResult mapping = _mapper.Map(detection.Module, detection.Sheet.Headers);
            List<Dictionary<string, string>> canonicalRows = BuildCanonicalRows(detection.Module, detection.Sheet, mapping);
            if (canonicalRows.Count == 0)
                throw new InvalidOperationException("The selected workbook does not contain any usable data rows after cleaning.");

            int batchId = _audit.StartBatch(detection.Module.ToString(), filePath, detection.Sheet.Name, detection.Confidence, mapping.MappedColumns);
            string stagedFile = null;
            try
            {
                stagedFile = WriteCanonicalWorkbook(detection.Module, canonicalRows);
                var diagnostics = new ExcelImportDiagnostics();
                ExcelImportResult import = _importService.Import(detection.Module, stagedFile, new ExcelImportExecutionOptions
                {
                    SkipPreflight = true,
                    AutoResolveReferences = true,
                    UseTransaction = true,
                    Diagnostics = diagnostics,
                    QuotationImportDirection = detection.Module == ExcelImportModule.Quotations ? quotationImportDirection : null
                });

                _audit.LogErrors(batchId, import.Errors);
                _audit.CompleteBatch(batchId, import.SuccessCount + import.SkippedCount, import.SuccessCount, import.SkippedCount, detection, mapping, diagnostics, canonicalRows.Count, import.Errors);

                var result = new AutomatedImportResult
                {
                    BatchId = batchId,
                    DetectedModule = detection.Module,
                    DetectedSheetName = detection.Sheet.Name,
                    DetectionConfidence = detection.Confidence,
                    SuccessCount = import.SuccessCount,
                    SkippedCount = import.SkippedCount,
                    SummaryTitle = "Import complete"
                };

                foreach (KeyValuePair<string, string> entry in mapping.MappedColumns)
                    result.ColumnMappings[entry.Key] = entry.Value;

                foreach (string line in diagnostics.BuildSummaryLines())
                    result.CreatedDefaults.Add(line);

                result.UserMessages.Add(import.SuccessCount + " rows imported or refreshed in " + detection.Module + ".");
                if (import.SkippedCount > 0)
                    result.UserMessages.Add(import.SkippedCount + " rows were skipped safely.");
                if (detection.Confidence < 65)
                    result.UserMessages.Add("The file format was unusual. ServoERP used best-effort detection and skipped unsafe rows.");
                result.Errors.AddRange(import.Errors.Take(20));
                return result;
            }
            catch (Exception ex)
            {
                _audit.FailBatch(batchId, ex);
                throw;
            }
            finally
            {
                TryDelete(stagedFile);
            }
        }

        private List<Dictionary<string, string>> BuildCanonicalRows(ExcelImportModule module, ExcelSheetImportData sheet, ColumnMappingResult mapping)
        {
            var rows = new List<Dictionary<string, string>>();
            foreach (Dictionary<string, string> sourceRow in sheet.Rows)
            {
                Dictionary<string, string> canonical = _cleaner.CreateCanonicalRow(module, sourceRow, mapping);
                if (!_cleaner.IsMeaningfulRow(module, canonical))
                    continue;
                rows.Add(canonical);
            }

            return rows;
        }

        private static string WriteCanonicalWorkbook(ExcelImportModule module, List<Dictionary<string, string>> rows)
        {
            string[] headers = ExcelImportService.GetHeaders(module);
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImportStage");
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, module + "_AutoStage_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".xlsx");

            using (var package = new ExcelPackage())
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets.Add(module.ToString());
                for (int col = 0; col < headers.Length; col++)
                    sheet.Cells[1, col + 1].Value = headers[col];

                for (int row = 0; row < rows.Count; row++)
                {
                    Dictionary<string, string> values = rows[row];
                    for (int col = 0; col < headers.Length; col++)
                    {
                        string value;
                        if (values.TryGetValue(headers[col], out value) && !string.IsNullOrWhiteSpace(value))
                            sheet.Cells[row + 2, col + 1].Value = value;
                    }
                }

                if (sheet.Dimension != null)
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }

            return filePath;
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }
    }

    internal sealed class DataTypeDetectionResult
    {
        public ExcelImportModule Module { get; set; }
        public ExcelSheetImportData Sheet { get; set; }
        public int Confidence { get; set; }
    }

    internal sealed class ColumnMappingResult
    {
        public Dictionary<string, string> MappedColumns { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ExcelWorkbookImportData
    {
        public List<ExcelSheetImportData> Sheets { get; } = new List<ExcelSheetImportData>();
    }

    internal sealed class ExcelSheetImportData
    {
        public string Name { get; set; }
        public List<string> Headers { get; } = new List<string>();
        public List<Dictionary<string, string>> Rows { get; } = new List<Dictionary<string, string>>();
    }

    internal sealed class ExcelFileReaderService
    {
        public ExcelWorkbookImportData Read(string filePath)
        {
            string extension = Path.GetExtension(filePath) ?? string.Empty;
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return ReadXlsx(filePath);
            if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
                return ReadXls(filePath);

            throw new InvalidOperationException("ServoERP currently accepts Excel workbooks in .xlsx or .xls format.");
        }

        private ExcelWorkbookImportData ReadXlsx(string filePath)
        {
            var workbook = new ExcelWorkbookImportData();
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                foreach (ExcelWorksheet sheet in package.Workbook.Worksheets)
                {
                    if (sheet == null || sheet.Dimension == null)
                        continue;

                    ExcelSheetImportData data = ExtractSheet(
                        sheet.Name,
                        (row, col) => Convert.ToString(sheet.Cells[row, col].Value, CultureInfo.InvariantCulture),
                        sheet.Dimension.End.Row,
                        sheet.Dimension.End.Column);

                    if (data.Headers.Count > 0 && data.Rows.Count > 0)
                        workbook.Sheets.Add(data);
                }
            }

            return workbook;
        }

        private ExcelWorkbookImportData ReadXls(string filePath)
        {
            var workbook = new ExcelWorkbookImportData();
            var builder = new OleDbConnectionStringBuilder
            {
                Provider = "Microsoft.ACE.OLEDB.12.0",
                DataSource = filePath
            };
            builder["Extended Properties"] = "Excel 8.0;HDR=NO;IMEX=1";

            using (var conn = new OleDbConnection(builder.ConnectionString))
            {
                conn.Open();
                DataTable tables = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                foreach (DataRow table in tables.Rows)
                {
                    string sheetName = Convert.ToString(table["TABLE_NAME"]);
                    if (string.IsNullOrWhiteSpace(sheetName) || !sheetName.EndsWith("$", StringComparison.OrdinalIgnoreCase) && !sheetName.EndsWith("$'", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (var adapter = new OleDbDataAdapter("SELECT * FROM [" + EscapeSheetIdentifier(sheetName) + "]", conn))
                    {
                        var dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        ExcelSheetImportData data = ExtractSheet(
                            sheetName.Trim('\''),
                            (row, col) => row - 1 < dataTable.Rows.Count && col - 1 < dataTable.Columns.Count ? Convert.ToString(dataTable.Rows[row - 1][col - 1], CultureInfo.InvariantCulture) : null,
                            dataTable.Rows.Count,
                            dataTable.Columns.Count);

                        if (data.Headers.Count > 0 && data.Rows.Count > 0)
                            workbook.Sheets.Add(data);
                    }
                }
            }

            return workbook;
        }

        private static string EscapeSheetIdentifier(string sheetName)
        {
            return (sheetName ?? string.Empty).Replace("]", "]]");
        }

        private ExcelSheetImportData ExtractSheet(string sheetName, Func<int, int, string> readCell, int totalRows, int totalColumns)
        {
            var sheet = new ExcelSheetImportData { Name = sheetName };
            int headerRow = FindHeaderRow(readCell, totalRows, totalColumns);
            if (headerRow <= 0)
                return sheet;

            for (int col = 1; col <= totalColumns; col++)
            {
                string header = CleanHeader(readCell(headerRow, col));
                if (!string.IsNullOrWhiteSpace(header))
                    sheet.Headers.Add(header);
            }

            if (sheet.Headers.Count == 0)
                return sheet;

            for (int row = headerRow + 1; row <= totalRows; row++)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int nonEmpty = 0;
                for (int col = 1; col <= sheet.Headers.Count; col++)
                {
                    string value = CleanCell(readCell(row, col));
                    if (!string.IsNullOrWhiteSpace(value))
                        nonEmpty++;
                    values[sheet.Headers[col - 1]] = value;
                }

                if (nonEmpty == 0 || LooksLikeSummaryRow(values.Values))
                    continue;

                sheet.Rows.Add(values);
            }

            return sheet;
        }

        private static int FindHeaderRow(Func<int, int, string> readCell, int totalRows, int totalColumns)
        {
            int bestRow = 0;
            int bestScore = 0;
            int scanLimit = Math.Min(totalRows, 8);
            for (int row = 1; row <= scanLimit; row++)
            {
                int score = 0;
                for (int col = 1; col <= totalColumns; col++)
                {
                    string value = CleanHeader(readCell(row, col));
                    if (!string.IsNullOrWhiteSpace(value))
                        score += value.Length > 1 ? 2 : 1;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRow = row;
                }
            }

            return bestRow;
        }

        private static bool LooksLikeSummaryRow(IEnumerable<string> values)
        {
            string joined = string.Join(" ", values.Where(v => !string.IsNullOrWhiteSpace(v))).Trim().ToLowerInvariant();
            if (joined.Length == 0)
                return true;
            return joined.StartsWith("total") || joined.StartsWith("grand total") || joined.StartsWith("summary") || joined.StartsWith("notes:");
        }

        private static string CleanHeader(string value)
        {
            string cleaned = CleanCell(value);
            return cleaned.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string CleanCell(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string cleaned = Regex.Replace(value, @"[\u0000-\u001F]+", " ");
            cleaned = cleaned.Replace('\u00A0', ' ');
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
            return cleaned;
        }
    }

    internal sealed class DataTypeDetectionService
    {
        private readonly Dictionary<ExcelImportModule, string[]> _signals = new Dictionary<ExcelImportModule, string[]>
        {
            { ExcelImportModule.Clients, new[] { "clientname", "customername", "companyname", "gstin", "phone", "billingaddress" } },
            { ExcelImportModule.Vendors, new[] { "vendorname", "suppliername", "gstin", "paymentterms", "creditdays", "phone" } },
            { ExcelImportModule.Inventory, new[] { "itemname", "productname", "materialname", "sku", "itemcode", "unit", "rate", "hsncode" } },
            { ExcelImportModule.Sites, new[] { "sitename", "clientname", "siteaddress", "location", "city" } },
            { ExcelImportModule.Employees, new[] { "employeename", "employeecode", "designation", "department", "salary", "phone" } },
            { ExcelImportModule.Invoices, new[] { "invoicenumber", "invoicedate", "taxamount", "totalamount", "duedate" } },
            { ExcelImportModule.Quotations, new[] { "quotationnumber", "validuntil", "bidvalue", "quotationdate", "description" } },
            { ExcelImportModule.Purchases, new[] { "vendorname", "itemdescription", "quantity", "unitprice", "purchasedate" } },
            { ExcelImportModule.Payments, new[] { "paymentdate", "invoice", "amountpaid", "paymentmode", "reference" } },
            { ExcelImportModule.Jobs, new[] { "jobtype", "technician", "scheduleddate", "priority", "description" } }
        };

        public DataTypeDetectionResult Detect(ExcelWorkbookImportData workbook, ExcelImportModule? preferredModule)
        {
            DataTypeDetectionResult best = null;
            foreach (ExcelSheetImportData sheet in workbook.Sheets)
            {
                Dictionary<string, string> normalized = sheet.Headers.ToDictionary(h => h, Normalize, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<ExcelImportModule, string[]> entry in _signals)
                {
                    int score = 0;
                    foreach (string header in normalized.Values)
                    {
                        foreach (string signal in entry.Value)
                        {
                            if (header == signal) score += 15;
                            else if (header.Contains(signal) || signal.Contains(header)) score += 8;
                        }
                    }

                    if (preferredModule.HasValue && entry.Key == preferredModule.Value)
                        score += 20;

                    if (best == null || score > best.Confidence)
                    {
                        best = new DataTypeDetectionResult
                        {
                            Module = entry.Key,
                            Sheet = sheet,
                            Confidence = Math.Min(100, score)
                        };
                    }
                }
            }

            if (best != null && best.Confidence < 20 && preferredModule.HasValue && workbook.Sheets.Count > 0)
            {
                best.Module = preferredModule.Value;
                best.Sheet = workbook.Sheets[0];
                best.Confidence = 45;
            }

            return best;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
            }

            return builder.ToString();
        }
    }

    internal sealed class AutoColumnMappingService
    {
        private readonly Dictionary<ExcelImportModule, Dictionary<string, string[]>> _aliases = new Dictionary<ExcelImportModule, Dictionary<string, string[]>>
        {
            { ExcelImportModule.Clients, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ClientName", new[] { "Client Name", "Customer Name", "Company Name", "Party Name", "Customer", "Client", "Company" } },
                    { "ContactPerson", new[] { "Contact Person", "Owner", "Primary Contact", "Contact" } },
                    { "Phone", new[] { "Mobile", "Phone", "Contact No", "Contact Number", "Mobile No" } },
                    { "Email", new[] { "Email ID", "Mail", "Email" } },
                    { "Address", new[] { "Address", "Billing Address", "Office Address", "Registered Address" } },
                    { "City", new[] { "City", "Town" } },
                    { "State", new[] { "State", "Region" } },
                    { "GSTIN", new[] { "GST No", "GSTIN", "GST Number", "Tax Number" } },
                    { "Notes", new[] { "Notes", "Remarks", "Comments" } }
                }
            },
            { ExcelImportModule.Vendors, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "VendorName", new[] { "Vendor Name", "Supplier Name", "Party Name", "Supplier", "Vendor" } },
                    { "ContactPerson", new[] { "Contact Person", "Primary Contact", "Owner" } },
                    { "Phone", new[] { "Mobile", "Phone", "Contact No" } },
                    { "Email", new[] { "Email", "Email ID" } },
                    { "Address", new[] { "Address", "Office Address" } },
                    { "City", new[] { "City", "Town" } },
                    { "GSTIN", new[] { "GSTIN", "GST No", "GST Number" } },
                    { "Notes", new[] { "Notes", "Payment Terms", "Credit Days", "Comments" } }
                }
            },
            { ExcelImportModule.Inventory, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ItemName", new[] { "Item", "Product", "Material Name", "Description", "Product Name", "Item Name" } },
                    { "Category", new[] { "Category", "Group", "Item Group", "Type" } },
                    { "CurrentStock", new[] { "Current Stock", "Stock", "Qty", "Quantity", "Closing Stock" } },
                    { "Unit", new[] { "UOM", "Unit", "Unit Of Measure", "Measure" } },
                    { "LastPurchaseRate", new[] { "Rate", "Price", "Unit Price", "Last Purchase Rate", "Purchase Rate" } },
                    { "ReorderLevel", new[] { "Reorder Level", "Min Stock", "Minimum Stock", "Safety Stock" } },
                    { "StockValue", new[] { "Stock Value", "Value", "Amount" } },
                    { "Notes", new[] { "Notes", "Remarks", "Comments" } }
                }
            },
            { ExcelImportModule.Sites, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "SiteName", new[] { "Site", "Site Name", "Project Site", "Location Name", "Location" } },
                    { "ClientName", new[] { "Client", "Customer", "Company", "Client Name", "Customer Name" } },
                    { "Address", new[] { "Address", "Site Address", "Location", "Site Location" } },
                    { "City", new[] { "City", "Town" } },
                    { "ContactPerson", new[] { "Contact Person", "Site Incharge", "Contact" } },
                    { "Phone", new[] { "Phone", "Mobile", "Contact No" } },
                    { "SiteType", new[] { "Site Type", "Project Type", "Type" } },
                    { "Notes", new[] { "Notes", "Remarks", "Comments" } }
                }
            },
            { ExcelImportModule.Employees, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "EmployeeCode", new[] { "Employee Code", "Emp Code", "Staff Code" } },
                    { "EmployeeName", new[] { "Employee", "Employee Name", "Staff Name", "Name" } },
                    { "Designation", new[] { "Role", "Designation", "Department Role" } },
                    { "Department", new[] { "Department", "Team" } },
                    { "Phone", new[] { "Phone", "Mobile", "Contact No" } },
                    { "WhatsApp", new[] { "WhatsApp", "Whatsapp Number", "WA Number" } },
                    { "BloodGroup", new[] { "Blood Group", "BloodGroup" } },
                    { "Aadhaar", new[] { "Aadhaar", "Aadhar", "UID" } },
                    { "PAN", new[] { "PAN", "Pan Number" } },
                    { "JoiningDate", new[] { "Joining Date", "Date Of Joining", "DOJ" } },
                    { "Status", new[] { "Status", "Employee Status" } }
                }
            },
            { ExcelImportModule.Invoices, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "InvoiceNumber", new[] { "Invoice Number", "Invoice No", "Bill No" } },
                    { "InvoiceDate", new[] { "Invoice Date", "Bill Date", "Date" } },
                    { "ClientName", new[] { "Client Name", "Customer Name", "Company Name" } },
                    { "SiteName", new[] { "Site Name", "Location" } },
                    { "Description", new[] { "Description", "Narration", "Subject", "Item Description" } },
                    { "Amount", new[] { "Amount", "Taxable Amount", "Sub Total", "Subtotal" } },
                    { "TaxAmount", new[] { "Tax Amount", "GST Amount", "GST", "Tax" } },
                    { "TotalAmount", new[] { "Total Amount", "Grand Total", "Invoice Total" } },
                    { "Status", new[] { "Status", "Payment Status" } },
                    { "DueDate", new[] { "Due Date", "Payment Due", "Due On" } }
                }
            },
            { ExcelImportModule.Quotations, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "QuotationNumber", new[] { "Quotation Number", "Quotation No", "Quote No" } },
                    { "QuotationDate", new[] { "Quotation Date", "Quote Date", "Date" } },
                    { "ClientName", new[] { "Client Name", "Customer Name", "Company Name" } },
                    { "SiteName", new[] { "Site Name", "Location" } },
                    { "Description", new[] { "Description", "Narration", "Subject" } },
                    { "Amount", new[] { "Amount", "Quote Value", "Value" } },
                    { "Status", new[] { "Status", "Quotation Status" } },
                    { "ValidUntil", new[] { "Valid Until", "Expiry Date", "Valid To" } }
                }
            },
            { ExcelImportModule.Purchases, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "PurchaseDate", new[] { "Purchase Date", "PO Date", "Date" } },
                    { "VendorName", new[] { "Vendor Name", "Supplier Name", "Vendor" } },
                    { "ItemDescription", new[] { "Item", "Description", "Material Name", "Product Name" } },
                    { "Quantity", new[] { "Qty", "Quantity", "Purchase Qty" } },
                    { "UnitPrice", new[] { "Unit Price", "Rate", "Price" } },
                    { "TotalAmount", new[] { "Total Amount", "Line Amount", "Amount" } },
                    { "Notes", new[] { "Notes", "Remarks", "Comments" } }
                }
            },
            { ExcelImportModule.Payments, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "PaymentDate", new[] { "Payment Date", "Receipt Date", "Date" } },
                    { "InvoiceNumber", new[] { "Invoice Number", "Invoice No", "Bill No" } },
                    { "ClientName", new[] { "Client Name", "Customer Name", "Company Name" } },
                    { "AmountPaid", new[] { "Amount Paid", "Received Amount", "Receipt Amount", "Amount" } },
                    { "PaymentMode", new[] { "Payment Mode", "Mode", "Collection Mode" } },
                    { "ReferenceNumber", new[] { "Reference Number", "UTR", "Cheque No", "Reference" } },
                    { "Notes", new[] { "Notes", "Remarks", "Narration" } }
                }
            },
            { ExcelImportModule.Jobs, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "JobDate", new[] { "Job Date", "Complaint Date", "Date" } },
                    { "ClientName", new[] { "Client Name", "Customer Name", "Company Name" } },
                    { "SiteName", new[] { "Site Name", "Location" } },
                    { "TechnicianName", new[] { "Technician Name", "Engineer", "Assigned To", "Technician" } },
                    { "JobType", new[] { "Job Type", "Call Type", "Service Type" } },
                    { "Description", new[] { "Description", "Complaint", "Work Details", "Issue" } },
                    { "Status", new[] { "Status", "Job Status" } },
                    { "Priority", new[] { "Priority", "Urgency" } },
                    { "ScheduledDate", new[] { "Scheduled Date", "Visit Date", "Plan Date" } }
                }
            }
        };

        public ColumnMappingResult Map(ExcelImportModule module, List<string> sourceHeaders)
        {
            var result = new ColumnMappingResult();
            string[] targetHeaders = ExcelImportService.GetHeaders(module);
            foreach (string targetHeader in targetHeaders)
            {
                string match = FindBestMatch(module, targetHeader, sourceHeaders);
                if (!string.IsNullOrWhiteSpace(match))
                    result.MappedColumns[targetHeader] = match;
            }

            return result;
        }

        private string FindBestMatch(ExcelImportModule module, string targetHeader, List<string> sourceHeaders)
        {
            var candidates = new List<string> { targetHeader };
            Dictionary<string, string[]> aliases;
            if (_aliases.TryGetValue(module, out aliases))
            {
                string[] values;
                if (aliases.TryGetValue(targetHeader, out values))
                    candidates.AddRange(values);
            }

            foreach (string candidate in candidates)
            {
                string normalizedCandidate = Normalize(candidate);
                string exact = sourceHeaders.FirstOrDefault(header => Normalize(header) == normalizedCandidate);
                if (!string.IsNullOrWhiteSpace(exact))
                    return exact;
            }

            foreach (string source in sourceHeaders)
            {
                string normalizedSource = Normalize(source);
                foreach (string candidate in candidates)
                {
                    string normalizedCandidate = Normalize(candidate);
                    if (normalizedSource.Contains(normalizedCandidate) || normalizedCandidate.Contains(normalizedSource))
                        return source;
                }
            }

            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
            }

            return builder.ToString();
        }
    }

    internal sealed class DataCleaningService
    {
        public Dictionary<string, string> CreateCanonicalRow(ExcelImportModule module, Dictionary<string, string> sourceRow, ColumnMappingResult mapping)
        {
            var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string header in ExcelImportService.GetHeaders(module))
            {
                string sourceHeader;
                string raw = mapping.MappedColumns.TryGetValue(header, out sourceHeader) && sourceRow.ContainsKey(sourceHeader)
                    ? sourceRow[sourceHeader]
                    : string.Empty;
                canonical[header] = NormalizeValue(module, header, raw, canonical);
            }

            InferMissingValues(module, canonical);
            return canonical;
        }

        public bool IsMeaningfulRow(ExcelImportModule module, Dictionary<string, string> row)
        {
            foreach (string field in ExcelImportService.GetRequiredHeaders(module))
            {
                string value;
                if (row.TryGetValue(field, out value) && !string.IsNullOrWhiteSpace(value))
                    return true;
            }

            return row.Values.Any(value => !string.IsNullOrWhiteSpace(value));
        }

        private string NormalizeValue(ExcelImportModule module, string field, string raw, Dictionary<string, string> row)
        {
            string value = Cleanup(raw);
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (field.IndexOf("Date", StringComparison.OrdinalIgnoreCase) >= 0 || field.Equals("ValidUntil", StringComparison.OrdinalIgnoreCase))
                return NormalizeDate(value);

            if (field.IndexOf("Amount", StringComparison.OrdinalIgnoreCase) >= 0 || field.IndexOf("Rate", StringComparison.OrdinalIgnoreCase) >= 0 || field.Equals("Quantity", StringComparison.OrdinalIgnoreCase) || field.Equals("CurrentStock", StringComparison.OrdinalIgnoreCase) || field.Equals("ReorderLevel", StringComparison.OrdinalIgnoreCase))
                return NormalizeDecimal(value);

            if (field.Equals("Phone", StringComparison.OrdinalIgnoreCase) || field.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase))
                return NormalizePhone(value);

            if (field.Equals("GSTIN", StringComparison.OrdinalIgnoreCase))
                return NormalizeGstin(value);

            if (field.Equals("Unit", StringComparison.OrdinalIgnoreCase))
                return NormalizeUnit(value);

            if (field.Equals("Status", StringComparison.OrdinalIgnoreCase))
                return NormalizeStatus(module, value);

            if (field.EndsWith("Name", StringComparison.OrdinalIgnoreCase) || field.Equals("City", StringComparison.OrdinalIgnoreCase) || field.Equals("State", StringComparison.OrdinalIgnoreCase) || field.Equals("Designation", StringComparison.OrdinalIgnoreCase) || field.Equals("Department", StringComparison.OrdinalIgnoreCase) || field.Equals("Category", StringComparison.OrdinalIgnoreCase))
                return ToTitleCase(value);

            return value;
        }

        private void InferMissingValues(ExcelImportModule module, Dictionary<string, string> row)
        {
            if (module == ExcelImportModule.Invoices)
            {
                if (string.IsNullOrWhiteSpace(row["TotalAmount"]))
                    row["TotalAmount"] = NormalizeDecimal(SafeDecimal(row["Amount"]) + SafeDecimal(row["TaxAmount"]));
                if (string.IsNullOrWhiteSpace(row["DueDate"]))
                    row["DueDate"] = ShiftDate(row["InvoiceDate"], 30);
            }
            else if (module == ExcelImportModule.Quotations)
            {
                if (string.IsNullOrWhiteSpace(row["ValidUntil"]))
                    row["ValidUntil"] = ShiftDate(row["QuotationDate"], 30);
            }
            else if (module == ExcelImportModule.Purchases)
            {
                if (string.IsNullOrWhiteSpace(row["TotalAmount"]))
                    row["TotalAmount"] = NormalizeDecimal(SafeDecimal(row["Quantity"]) * SafeDecimal(row["UnitPrice"]));
            }
            else if (module == ExcelImportModule.Jobs)
            {
                if (string.IsNullOrWhiteSpace(row["ScheduledDate"]))
                    row["ScheduledDate"] = !string.IsNullOrWhiteSpace(row["JobDate"]) ? row["JobDate"] : DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            else if (module == ExcelImportModule.Inventory)
            {
                if (string.IsNullOrWhiteSpace(row["StockValue"]))
                    row["StockValue"] = NormalizeDecimal(SafeDecimal(row["CurrentStock"]) * SafeDecimal(row["LastPurchaseRate"]));
            }
        }

        private static string Cleanup(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string cleaned = Regex.Replace(value, @"[\u0000-\u001F]+", " ");
            cleaned = cleaned.Replace('\u00A0', ' ');
            cleaned = Regex.Replace(cleaned, "rs\\.", string.Empty, RegexOptions.IgnoreCase);
            cleaned = cleaned.Replace("₹", string.Empty);
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
            return cleaned;
        }

        private static string NormalizePhone(string value)
        {
            string digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("91", StringComparison.OrdinalIgnoreCase) && digits.Length > 10)
                digits = digits.Substring(digits.Length - 10);
            if (digits.Length > 10)
                digits = digits.Substring(digits.Length - 10);
            return digits;
        }

        private static string NormalizeGstin(string value)
        {
            return Regex.Replace(value.ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
        }

        private static string NormalizeDecimal(string value)
        {
            string cleaned = value.Replace(",", string.Empty);
            cleaned = cleaned.Replace("%", string.Empty);
            cleaned = Regex.Replace(cleaned, "gst", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, "rs", string.Empty, RegexOptions.IgnoreCase);
            decimal parsed;
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ||
                decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("en-IN"), out parsed))
                return parsed.ToString("0.##", CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static string NormalizeDecimal(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string NormalizeDate(string value)
        {
            DateTime parsed;
            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd MMM yyyy", "dd MMMM yyyy" };
            if (DateTime.TryParseExact(value, formats, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out parsed) ||
                DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out parsed) ||
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static string NormalizeUnit(string value)
        {
            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "pcs" || normalized == "piece" || normalized == "pieces")
                return "PCS";
            if (normalized == "nos" || normalized == "no" || normalized == "number" || normalized == "numbers")
                return "Nos";
            if (normalized == "meter" || normalized == "meters" || normalized == "mtr")
                return "Mtr";
            if (normalized == "kg" || normalized == "kgs" || normalized == "kilogram")
                return "KG";
            return ToTitleCase(value);
        }

        private static string NormalizeStatus(ExcelImportModule module, string value)
        {
            string normalized = value.Trim().ToLowerInvariant();
            switch (module)
            {
                case ExcelImportModule.Jobs:
                    if (normalized.Contains("assign")) return "Assigned";
                    if (normalized.Contains("progress")) return "In Progress";
                    if (normalized.Contains("hold")) return "On Hold";
                    if (normalized.Contains("complete") || normalized.Contains("closed")) return "Completed";
                    if (normalized.Contains("cancel")) return "Cancelled";
                    return "New";
                case ExcelImportModule.Invoices:
                    if (normalized.Contains("part")) return "Partially Paid";
                    if (normalized == "paid" || normalized.Contains("settled")) return "Paid";
                    if (normalized.Contains("overdue")) return "Overdue";
                    if (normalized.Contains("sent")) return "Sent";
                    if (normalized.Contains("cancel")) return "Cancelled";
                    return "Draft";
                case ExcelImportModule.Quotations:
                    if (normalized.Contains("accept")) return "Accepted";
                    if (normalized.Contains("reject")) return "Rejected";
                    if (normalized.Contains("expire")) return "Expired";
                    if (normalized.Contains("sent")) return "Sent";
                    return "Draft";
                case ExcelImportModule.Purchases:
                    if (normalized.Contains("receive") && normalized.Contains("part")) return "Partially Received";
                    if (normalized.Contains("receive")) return "Received";
                    if (normalized.Contains("order")) return "Ordered";
                    if (normalized.Contains("cancel")) return "Cancelled";
                    return "Draft";
                case ExcelImportModule.Employees:
                    if (normalized.Contains("leave")) return "On Leave";
                    if (normalized.Contains("terminate")) return "Terminated";
                    if (normalized.Contains("inactive")) return "Inactive";
                    return "Active";
                default:
                    return ToTitleCase(value);
            }
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return CultureInfo.GetCultureInfo("en-IN").TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
        }

        private static decimal SafeDecimal(string value)
        {
            decimal parsed;
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : 0m;
        }

        private static string ShiftDate(string dateValue, int days)
        {
            DateTime parsed;
            if (DateTime.TryParseExact(dateValue, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.AddDays(days).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return string.Empty;
        }
    }

    internal sealed class ImportAuditLogService
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public int StartBatch(string importType, string sourceFile, string sheetName, int confidence, IDictionary<string, string> mappings)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO DataImportBatches (ImportType, SourceFile, Status, StartedAt, ImportedBy, Notes)
VALUES (@type, @file, 'Processing', GETDATE(), @user, @notes);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@type", importType ?? "Unknown");
                    cmd.Parameters.AddWithValue("@file", sourceFile ?? string.Empty);
                    cmd.Parameters.AddWithValue("@user", SessionManager.IsLoggedIn ? (SessionManager.CurrentUser.DisplayName ?? SessionManager.CurrentUser.Username) : Environment.UserName);
                    cmd.Parameters.AddWithValue("@notes", _json.Serialize(new Dictionary<string, object>
                    {
                        { "sheet", sheetName },
                        { "confidence", confidence },
                        { "mappings", mappings }
                    }));
                    return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
            }
        }

        public void LogErrors(int batchId, IEnumerable<string> errors)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                foreach (string error in errors.Take(200))
                {
                    int rowNumber = 0;
                    string message = error ?? string.Empty;
                    Match match = Regex.Match(message, @"Row\s+(?<row>\d+)\s*-\s*(?<message>.+)$");
                    if (match.Success)
                    {
                        int.TryParse(match.Groups["row"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowNumber);
                        message = match.Groups["message"].Value;
                    }

                    using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO DataImportErrors (BatchId, RowNumber, ColumnName, ErrorMessage, RawValue)
VALUES (@batchId, @rowNumber, NULL, @message, NULL);", conn))
                    {
                        cmd.Parameters.AddWithValue("@batchId", batchId);
                        cmd.Parameters.AddWithValue("@rowNumber", rowNumber);
                        cmd.Parameters.AddWithValue("@message", message);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void CompleteBatch(int batchId, int totalRows, int successRows, int errorRows, DataTypeDetectionResult detection, ColumnMappingResult mapping, ExcelImportDiagnostics diagnostics, int stagedRowCount, IEnumerable<string> errors)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
UPDATE DataImportBatches
SET Status='Completed', TotalRows=@totalRows, SuccessRows=@successRows, ErrorRows=@errorRows,
    CompletedAt=GETDATE(), Notes=@notes
WHERE BatchId=@batchId", conn))
                {
                    cmd.Parameters.AddWithValue("@batchId", batchId);
                    cmd.Parameters.AddWithValue("@totalRows", totalRows);
                    cmd.Parameters.AddWithValue("@successRows", successRows);
                    cmd.Parameters.AddWithValue("@errorRows", errorRows);
                    cmd.Parameters.AddWithValue("@notes", _json.Serialize(new Dictionary<string, object>
                    {
                        { "detectedModule", detection.Module.ToString() },
                        { "sheet", detection.Sheet.Name },
                        { "confidence", detection.Confidence },
                        { "stagedRows", stagedRowCount },
                        { "mappedColumns", mapping.MappedColumns },
                        { "diagnostics", diagnostics.BuildSummaryLines() },
                        { "sampleErrors", errors.Take(20).ToArray() }
                    }));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void FailBatch(int batchId, Exception ex)
        {
            if (batchId <= 0)
                return;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
UPDATE DataImportBatches
SET Status='Failed', CompletedAt=GETDATE(), Notes=@notes
WHERE BatchId=@batchId", conn))
                {
                    cmd.Parameters.AddWithValue("@batchId", batchId);
                    cmd.Parameters.AddWithValue("@notes", ex == null ? "Import failed." : ex.Message);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
