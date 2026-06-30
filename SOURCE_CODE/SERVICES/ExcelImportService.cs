using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
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
        Sites,
        Inventory,
        SupplierItemPrices,
        AMC
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
        private readonly UnitMeasurementService _unitMeasurements = new UnitMeasurementService();

        public void CreateTemplate(ExcelImportModule module, string filePath)
        {
            SessionManager.DemandPermission(ModuleKey(module), "View");
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add(GetDisplayName(module));
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

        public ExcelImportResult Import(ExcelImportModule module, string filePath, ExcelImportExecutionOptions options = null)
        {
            options = options ?? new ExcelImportExecutionOptions();
            SessionManager.DemandPermission(ModuleKey(module), "Create");
            if (module == ExcelImportModule.AMC)
                DbHelper.EnsureAMCSchema();

            var result = new ExcelImportResult();
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                SqlTransaction transaction = options.UseTransaction ? conn.BeginTransaction() : null;
                var sheet = package.Workbook.Worksheets.FirstOrDefault();
                if (sheet == null || sheet.Dimension == null)
                    return result;

                Dictionary<string, int> map = BuildColumnMap(sheet);
                ValidationResult headerReview = _importReview.ReviewHeaders(GetDisplayName(module), map.Keys, GetRequiredHeaders(module));
                _validation.EnsureValid(headerReview, "Import header validation failed");
                EnsureImportSchema(conn, transaction, module);

                if (!options.SkipPreflight)
                {
                    ImportPreflightResult preflight = ImportPreflightService.ValidateRows(
                        module,
                        ExtractRows(sheet, map),
                    ImportPreflightService.LoadReferenceSnapshot(conn, transaction));
                    if (!preflight.CanImport)
                        throw new InvalidOperationException(preflight.ToUserMessage());
                }

                try
                {
                    if (module == ExcelImportModule.SupplierItemPrices)
                    {
                        ImportSupplierItemPriceWorkbook(conn, transaction, sheet, map, result, options);
                        transaction?.Commit();
                        InvalidateModuleCaches(module);
                        AppLogger.LogInfo("Excel import completed for " + module + " | success=" + result.SuccessCount + " | skipped=" + result.SkippedCount);
                        return result;
                    }

                    for (int row = 2; row <= sheet.Dimension.End.Row; row++)
                    {
                        try
                        {
                            if (IsRowEmpty(sheet, row, map.Values))
                                continue;

                            bool imported = false;
                            string savepoint = "ROW_" + row.ToString(CultureInfo.InvariantCulture);
                            if (transaction != null)
                                transaction.Save(savepoint);

                            switch (module)
                            {
                                case ExcelImportModule.Quotations:
                                    imported = ImportQuotationRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Invoices:
                                    imported = ImportInvoiceRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Payments:
                                    imported = ImportPaymentRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Purchases:
                                    imported = ImportPurchaseRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Jobs:
                                    imported = ImportJobRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Clients:
                                    imported = ImportClientRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Employees:
                                    imported = ImportEmployeeRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Vendors:
                                    imported = ImportVendorRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Sites:
                                    imported = ImportSiteRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.Inventory:
                                    imported = ImportInventoryRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                                case ExcelImportModule.AMC:
                                    imported = ImportAmcRow(conn, transaction, sheet, map, row, result, options);
                                    break;
                            }

                            if (imported)
                                result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            if (transaction != null)
                                transaction.Rollback("ROW_" + row.ToString(CultureInfo.InvariantCulture));
                            result.SkippedCount++;
                            result.Errors.Add("Row " + row + " - " + ex.Message);
                        }
                    }

                    transaction?.Commit();
                }
                catch
                {
                    transaction?.Rollback();
                    throw;
                }
            }

            InvalidateModuleCaches(module);
            AppLogger.LogInfo("Excel import completed for " + module + " | success=" + result.SuccessCount + " | skipped=" + result.SkippedCount);
            return result;
        }

        /// <summary>Keeps module pages in sync after imports that bypass service-layer cache invalidation.</summary>
        private static void InvalidateModuleCaches(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Vendors:
                case ExcelImportModule.Purchases:
                    AppDataCache.RemovePrefix("vendors:");
                    break;
                case ExcelImportModule.SupplierItemPrices:
                    AppDataCache.RemovePrefix("inventory:");
                    AppDataCache.RemovePrefix("vendors:");
                    break;
            }
        }

        /// <summary>Applies guarded import-time schema repairs required by older client databases.</summary>
        private static void EnsureImportSchema(SqlConnection conn, SqlTransaction transaction, ExcelImportModule module)
        {
            if (module == ExcelImportModule.Purchases || module == ExcelImportModule.Vendors)
            {
                Execute(conn, transaction, @"
IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL AND COL_LENGTH('dbo.Vendors', 'IsSupplier') IS NULL
BEGIN
    ALTER TABLE dbo.Vendors ADD IsSupplier BIT NOT NULL DEFAULT(1) WITH VALUES;
END

IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL AND COL_LENGTH('dbo.Vendors', 'IsServiceVendor') IS NULL
BEGIN
    ALTER TABLE dbo.Vendors ADD IsServiceVendor BIT NOT NULL DEFAULT(0) WITH VALUES;
END");
            }

            if (module == ExcelImportModule.SupplierItemPrices)
            {
                Execute(conn, transaction, @"
IF OBJECT_ID('dbo.SupplierItemPrices', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupplierItemPrices (
        PriceID INT PRIMARY KEY IDENTITY(1,1),
        VendorID INT NOT NULL FOREIGN KEY REFERENCES dbo.Vendors(VendorID),
        ItemName NVARCHAR(255) NOT NULL,
        Category NVARCHAR(100) NULL,
        Unit NVARCHAR(50) NULL,
        Rate DECIMAL(12,2) NOT NULL DEFAULT 0,
        Source NVARCHAR(100) NULL,
        EffectiveDate DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

IF COL_LENGTH('dbo.SupplierItemPrices', 'ItemID') IS NULL
    ALTER TABLE dbo.SupplierItemPrices ADD ItemID INT NULL;

IF COL_LENGTH('dbo.SupplierItemPrices', 'IsPreferred') IS NULL
    ALTER TABLE dbo.SupplierItemPrices ADD IsPreferred BIT NOT NULL CONSTRAINT DF_SupplierItemPrices_IsPreferred DEFAULT(0) WITH VALUES;

IF COL_LENGTH('dbo.SupplierItemPrices', 'IsActive') IS NULL
    ALTER TABLE dbo.SupplierItemPrices ADD IsActive BIT NOT NULL CONSTRAINT DF_SupplierItemPrices_IsActive DEFAULT(1) WITH VALUES;

IF COL_LENGTH('dbo.SupplierItemPrices', 'Notes') IS NULL
    ALTER TABLE dbo.SupplierItemPrices ADD Notes NVARCHAR(500) NULL;

IF OBJECT_ID('dbo.FK_SupplierItemPrices_StockItems_ItemID', 'F') IS NULL
   AND COL_LENGTH('dbo.SupplierItemPrices', 'ItemID') IS NOT NULL
BEGIN
    ALTER TABLE dbo.SupplierItemPrices
    WITH CHECK ADD CONSTRAINT FK_SupplierItemPrices_StockItems_ItemID
    FOREIGN KEY (ItemID) REFERENCES dbo.StockItems(ItemID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupplierItemPrices_ItemID' AND object_id = OBJECT_ID('dbo.SupplierItemPrices'))
    CREATE INDEX IX_SupplierItemPrices_ItemID ON dbo.SupplierItemPrices(ItemID, IsActive, VendorID);");
            }

            if (module == ExcelImportModule.Quotations)
                DbHelper.EnsureQuotationSchemaMigration();

            if (module != ExcelImportModule.Inventory)
                return;

            Execute(conn, transaction, @"
IF OBJECT_ID('dbo.StockItems', 'U') IS NOT NULL AND COL_LENGTH('dbo.StockItems', 'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.StockItems ADD IsActive BIT NOT NULL DEFAULT(1) WITH VALUES;
END

IF OBJECT_ID('dbo.StockItems', 'U') IS NOT NULL AND COL_LENGTH('dbo.StockItems', 'LastUpdated') IS NULL
BEGIN
    ALTER TABLE dbo.StockItems ADD LastUpdated DATETIME NOT NULL DEFAULT(GETDATE()) WITH VALUES;
END");
        }

        private static IEnumerable<IDictionary<string, string>> ExtractRows(ExcelWorksheet sheet, Dictionary<string, int> map)
        {
            if (sheet == null || sheet.Dimension == null || map == null)
                yield break;

            for (int row = 2; row <= sheet.Dimension.End.Row; row++)
            {
                if (IsRowEmpty(sheet, row, map.Values))
                    continue;

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, int> entry in map)
                    values[entry.Key] = Convert.ToString(sheet.Cells[row, entry.Value].Value, CultureInfo.InvariantCulture) ?? string.Empty;
                yield return values;
            }
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
            foreach (string candidate in GetAcceptedHeaderNames(header))
            {
                if (map.ContainsKey(candidate))
                    return Convert.ToString(sheet.Cells[row, map[candidate]].Value)?.Trim() ?? string.Empty;
            }

            return string.Empty;
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
            string[] indiaFormats = { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy", "dd MMM yyyy", "dd MMMM yyyy" };
            if (DateTime.TryParseExact(raw, indiaFormats, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out value) ||
                DateTime.TryParse(raw, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out value) ||
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ||
                DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out value))
                return value;
            return DateTime.Today;
        }

        private static int? GetNullableInt(ExcelWorksheet sheet, int row, Dictionary<string, int> map, string header)
        {
            int value;
            return int.TryParse(GetCell(sheet, row, map, header), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                || int.TryParse(GetCell(sheet, row, map, header), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                ? (int?)value
                : null;
        }

        private static bool GetBoolean(ExcelWorksheet sheet, int row, Dictionary<string, int> map, string header, bool defaultValue)
        {
            string raw = NullIfEmpty(GetCell(sheet, row, map, header));
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "y":
                case "yes":
                case "true":
                case "preferred":
                case "active":
                    return true;
                case "0":
                case "n":
                case "no":
                case "false":
                case "inactive":
                    return false;
                default:
                    return defaultValue;
            }
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

        private int? FindClientId(SqlConnection conn, SqlTransaction transaction, string clientName)
        {
            return GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=@name AND ISNULL(IsActive,1)=1", new SqlParameter("@name", clientName));
        }

        private int? FindSiteId(SqlConnection conn, SqlTransaction transaction, int clientId, string siteName)
        {
            return GetScalarInt(conn, transaction, "SELECT TOP 1 SiteID FROM ClientSites WHERE ClientID=@clientId AND SiteName=@siteName",
                new SqlParameter("@clientId", clientId),
                new SqlParameter("@siteName", siteName));
        }

        private int? FindVendorId(SqlConnection conn, SqlTransaction transaction, string vendorName)
        {
            return GetScalarInt(conn, transaction, "SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=@name AND ISNULL(IsArchived,0)=0",
                new SqlParameter("@name", vendorName));
        }

        private int? FindEmployeeIdByName(SqlConnection conn, SqlTransaction transaction, string employeeName)
        {
            return GetScalarInt(conn, transaction, "SELECT TOP 1 EmployeeID FROM Employees WHERE Name=@name OR EmployeeCode=@name",
                new SqlParameter("@name", employeeName));
        }

        private static int? GetScalarInt(SqlConnection conn, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
            }
        }

        private static int ExecuteScalarInt(SqlConnection conn, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static void Execute(SqlConnection conn, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
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
                case ExcelImportModule.Inventory: return "Inventory";
                case ExcelImportModule.SupplierItemPrices: return "Inventory";
                case ExcelImportModule.AMC: return "Contracts";
                default: return "Settings";
            }
        }

        public static string GetDisplayName(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Vendors: return "Suppliers";
                case ExcelImportModule.SupplierItemPrices: return "Supplier Price Book";
                case ExcelImportModule.AMC: return "AMC Contracts";
                default: return module.ToString();
            }
        }

        public static IEnumerable<string> GetAcceptedHeaderNames(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                yield break;

            yield return header;

            if (string.Equals(header, "SupplierName", StringComparison.OrdinalIgnoreCase))
                yield return "VendorName";
            else if (string.Equals(header, "VendorName", StringComparison.OrdinalIgnoreCase))
                yield return "SupplierName";
            else if (string.Equals(header, "SupplierID", StringComparison.OrdinalIgnoreCase))
                yield return "VendorID";
            else if (string.Equals(header, "VendorID", StringComparison.OrdinalIgnoreCase))
                yield return "SupplierID";
        }

        private sealed class SupplierItemImportRow
        {
            public int SourceRowNumber { get; set; }
            public int ItemID { get; set; }
            public string ItemName { get; set; }
            public string Category { get; set; }
            public string Unit { get; set; }
            public int VendorID { get; set; }
            public string VendorName { get; set; }
            public decimal Rate { get; set; }
            public DateTime EffectiveDate { get; set; }
            public bool IsPreferred { get; set; }
            public bool IsActive { get; set; }
            public string Source { get; set; }
            public string Notes { get; set; }
        }

        private ExcelImportResult ImportSupplierItemPriceWorkbook(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            var validRows = new List<SupplierItemImportRow>();

            for (int row = 2; row <= sheet.Dimension.End.Row; row++)
            {
                try
                {
                    if (IsRowEmpty(sheet, row, map.Values))
                        continue;

                    SupplierItemImportRow parsed = ParseSupplierItemImportRow(conn, transaction, sheet, map, row, result, options);
                    if (parsed != null)
                        validRows.Add(parsed);
                }
                catch (Exception ex)
                {
                    result.SkippedCount++;
                    result.Errors.Add("Row " + row + " - " + ex.Message);
                }
            }

            foreach (IGrouping<int, SupplierItemImportRow> group in validRows.GroupBy(r => r.ItemID))
            {
                string itemName = group.First().ItemName;
                string category = group.First().Category;
                string defaultUnit = NormalizeUnit(group.First().Unit, options);

                Execute(conn, transaction, @"
UPDATE dbo.SupplierItemPrices
SET IsActive = 0,
    IsPreferred = 0
WHERE ItemID = @itemId;",
                    new SqlParameter("@itemId", group.Key));

                List<SupplierItemImportRow> rowsForItem = group
                    .GroupBy(r => r.VendorID)
                    .Select(g =>
                    {
                        SupplierItemImportRow preferred = g.FirstOrDefault(x => x.IsPreferred && x.IsActive);
                        return preferred
                            ?? g.Where(x => x.IsActive).OrderBy(x => x.Rate).ThenBy(x => x.SourceRowNumber).FirstOrDefault()
                            ?? g.OrderBy(x => x.SourceRowNumber).First();
                    })
                    .Where(r => r != null)
                    .ToList();

                if (rowsForItem.Count > 1 && rowsForItem.All(r => !r.IsPreferred))
                    rowsForItem[0].IsPreferred = true;

                foreach (SupplierItemImportRow price in rowsForItem)
                {
                    Execute(conn, transaction, @"
INSERT INTO dbo.SupplierItemPrices
    (ItemID, VendorID, ItemName, Category, Unit, Rate, Source, EffectiveDate, IsPreferred, IsActive, Notes)
VALUES
    (@itemId, @vendorId, @itemName, @category, @unit, @rate, @source, @effectiveDate, @isPreferred, @isActive, @notes);",
                        new SqlParameter("@itemId", group.Key),
                        new SqlParameter("@vendorId", price.VendorID),
                        new SqlParameter("@itemName", itemName),
                        new SqlParameter("@category", (object)category ?? DBNull.Value),
                        new SqlParameter("@unit", string.IsNullOrWhiteSpace(price.Unit) ? defaultUnit : NormalizeUnit(price.Unit, options)),
                        new SqlParameter("@rate", price.Rate),
                        new SqlParameter("@source", (object)price.Source ?? DBNull.Value),
                        new SqlParameter("@effectiveDate", price.EffectiveDate == default(DateTime) ? DateTime.Now : price.EffectiveDate),
                        new SqlParameter("@isPreferred", price.IsPreferred),
                        new SqlParameter("@isActive", price.IsActive),
                        new SqlParameter("@notes", (object)price.Notes ?? DBNull.Value));
                    result.SuccessCount++;
                }

                SupplierItemImportRow preferredRow = rowsForItem.FirstOrDefault(r => r.IsPreferred) ?? rowsForItem.FirstOrDefault();
                if (preferredRow != null)
                {
                    Execute(conn, transaction, @"
UPDATE dbo.StockItems
SET VendorID = @vendorId,
    LastPurchaseRate = CASE WHEN @rate >= 0 THEN @rate ELSE LastPurchaseRate END,
    LastUpdated = GETDATE()
WHERE ItemID = @itemId AND ISNULL(IsActive, 1) = 1;",
                        new SqlParameter("@vendorId", preferredRow.VendorID),
                        new SqlParameter("@rate", preferredRow.Rate),
                        new SqlParameter("@itemId", group.Key));
                }
            }

            return result;
        }

        private SupplierItemImportRow ParseSupplierItemImportRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string itemName = GetCell(sheet, row, map, "ItemName");
            string category = GetCell(sheet, row, map, "Category");
            string unit = NullIfEmpty(GetCell(sheet, row, map, "Unit")) ?? "Nos";
            string vendorName = GetCell(sheet, row, map, "VendorName");
            string source = NullIfEmpty(GetCell(sheet, row, map, "Source")) ?? "Supplier item import";
            string notes = GetCell(sheet, row, map, "Notes");
            decimal rate = GetDecimal(sheet, row, map, "Rate");
            DateTime effectiveDate = GetDate(sheet, row, map, "EffectiveDate");
            int? itemId = GetNullableInt(sheet, row, map, "ItemID");
            int? vendorId = GetNullableInt(sheet, row, map, "VendorID");
            bool isPreferred = GetBoolean(sheet, row, map, "IsPreferred", false);
            bool isActive = GetBoolean(sheet, row, map, "IsActive", true);

            if (string.IsNullOrWhiteSpace(itemName))
            {
                AddError(result, row, "Missing required field: ItemName");
                return null;
            }

            if (string.IsNullOrWhiteSpace(vendorName) && !vendorId.HasValue)
            {
                AddError(result, row, "Missing required field: SupplierName");
                return null;
            }

            if (rate < 0m)
            {
                AddError(result, row, "Rate cannot be negative.");
                return null;
            }

            StockItem resolvedItem = ResolveStockItem(conn, transaction, itemId, itemName);
            if (resolvedItem == null || resolvedItem.ItemID <= 0)
            {
                AddError(result, row, "Material item was not found in inventory: " + itemName);
                return null;
            }

            int resolvedVendorId = vendorId.GetValueOrDefault();
            if (resolvedVendorId > 0)
            {
                string existingVendorName = GetVendorNameById(conn, transaction, resolvedVendorId);
                if (string.IsNullOrWhiteSpace(existingVendorName))
                    resolvedVendorId = 0;
                else if (string.IsNullOrWhiteSpace(vendorName))
                    vendorName = existingVendorName;
            }

            if (resolvedVendorId <= 0)
            {
                resolvedVendorId = EnsureVendorId(conn, transaction, vendorName, null, null, null, null, null, null, notes, options);
            }

            if (resolvedVendorId <= 0)
            {
                AddError(result, row, "Supplier could not be resolved: " + vendorName);
                return null;
            }

            return new SupplierItemImportRow
            {
                SourceRowNumber = row,
                ItemID = resolvedItem.ItemID,
                ItemName = resolvedItem.ItemName,
                Category = string.IsNullOrWhiteSpace(category) ? resolvedItem.Category : category,
                Unit = NormalizeUnit(unit, options),
                VendorID = resolvedVendorId,
                VendorName = vendorName,
                Rate = rate,
                EffectiveDate = effectiveDate == default(DateTime) ? DateTime.Now : effectiveDate,
                IsPreferred = isPreferred,
                IsActive = isActive,
                Source = source,
                Notes = notes
            };
        }

        private StockItem ResolveStockItem(SqlConnection conn, SqlTransaction transaction, int? itemId, string itemName)
        {
            if (itemId.HasValue && itemId.Value > 0)
            {
                using (SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1 ItemID, ItemName, Category, Unit, LastPurchaseRate, VendorID
FROM dbo.StockItems
WHERE ItemID = @id AND ISNULL(IsActive, 1) = 1;", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@id", itemId.Value);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new StockItem
                            {
                                ItemID = Convert.ToInt32(reader["ItemID"]),
                                ItemName = Convert.ToString(reader["ItemName"]),
                                Category = reader["Category"] == DBNull.Value ? null : Convert.ToString(reader["Category"]),
                                Unit = reader["Unit"] == DBNull.Value ? null : Convert.ToString(reader["Unit"]),
                                LastPurchaseRate = reader["LastPurchaseRate"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["LastPurchaseRate"]),
                                VendorID = reader["VendorID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["VendorID"])
                            };
                        }
                    }
                }
            }

            using (SqlCommand cmd = new SqlCommand(@"
SELECT TOP 1 ItemID, ItemName, Category, Unit, LastPurchaseRate, VendorID
FROM dbo.StockItems
WHERE ItemName = @name AND ISNULL(IsActive, 1) = 1;", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@name", itemName);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new StockItem
                        {
                            ItemID = Convert.ToInt32(reader["ItemID"]),
                            ItemName = Convert.ToString(reader["ItemName"]),
                            Category = reader["Category"] == DBNull.Value ? null : Convert.ToString(reader["Category"]),
                            Unit = reader["Unit"] == DBNull.Value ? null : Convert.ToString(reader["Unit"]),
                            LastPurchaseRate = reader["LastPurchaseRate"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["LastPurchaseRate"]),
                            VendorID = reader["VendorID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["VendorID"])
                        };
                    }
                }
            }

            return null;
        }

        private static string GetVendorNameById(SqlConnection conn, SqlTransaction transaction, int vendorId)
        {
            return GetScalarString(conn, transaction, "SELECT TOP 1 VendorName FROM dbo.Vendors WHERE VendorID = @id",
                new SqlParameter("@id", vendorId));
        }

        private static string GetScalarString(SqlConnection conn, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);
                object value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private int EnsureClientId(SqlConnection conn, SqlTransaction transaction, string clientName, string contactPerson, string phone, string email, string address, string city, string state, string gstin, ExcelImportExecutionOptions options)
        {
            int? existingId = FindClientId(conn, transaction, clientName);
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(gstin))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE GSTNumber=@gstin", new SqlParameter("@gstin", gstin));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(phone))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE Phone=@phone", new SqlParameter("@phone", phone));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(email))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE Email=@email", new SqlParameter("@email", email));

            string finalAddress = string.IsNullOrWhiteSpace(state) ? address : (address + (string.IsNullOrWhiteSpace(address) ? string.Empty : ", ") + state);
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE B2BClients
SET PrimaryContact=COALESCE(NULLIF(@contact,''), PrimaryContact),
    Phone=COALESCE(NULLIF(@phone,''), Phone),
    Email=COALESCE(NULLIF(@email,''), Email),
    BillingAddress=COALESCE(NULLIF(@address,''), BillingAddress),
    City=COALESCE(NULLIF(@city,''), City),
    GSTNumber=COALESCE(NULLIF(@gstin,''), GSTNumber)
WHERE ClientID=@id",
                    new SqlParameter("@contact", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)finalAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Client: " + clientName);
                return existingId.Value;
            }

            using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO B2BClients (CompanyName, IndustryType, PrimaryContact, Phone, Email, BillingAddress, City, GSTNumber, IsActive, CustomerSince)
VALUES (@name,'Other',@contact,@phone,@email,@address,@city,@gstin,1,GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@name", clientName);
                cmd.Parameters.AddWithValue("@contact", (object)contactPerson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@phone", (object)phone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@email", (object)email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@address", (object)finalAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@city", (object)city ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@gstin", (object)gstin ?? DBNull.Value);
                int clientId = Convert.ToInt32(cmd.ExecuteScalar());
                options.Diagnostics?.CreatedClients.Add(clientName);
                return clientId;
            }
        }

        private int EnsureVendorId(SqlConnection conn, SqlTransaction transaction, string vendorName, string contactPerson, string phone, string email, string address, string city, string gstin, string notes, ExcelImportExecutionOptions options)
        {
            int? existingId = FindVendorId(conn, transaction, vendorName);
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(gstin))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 VendorID FROM Vendors WHERE GSTNumber=@gstin", new SqlParameter("@gstin", gstin));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(phone))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 VendorID FROM Vendors WHERE Phone=@phone", new SqlParameter("@phone", phone));

            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE Vendors
SET Phone=COALESCE(NULLIF(@phone,''), Phone),
    Email=COALESCE(NULLIF(@email,''), Email),
    Address=COALESCE(NULLIF(@address,''), Address),
    City=COALESCE(NULLIF(@city,''), City),
    GSTNumber=COALESCE(NULLIF(@gstin,''), GSTNumber),
    Notes=COALESCE(NULLIF(@notes,''), Notes),
    Category=COALESCE(NULLIF(@contactPerson,''), Category),
    IsSupplier=1
WHERE VendorID=@id",
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)address ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@contactPerson", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Supplier: " + vendorName);
                return existingId.Value;
            }

            using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO Vendors (VendorName, Phone, Email, Address, City, GSTNumber, Notes, Category, IsActive, IsSupplier, IsServiceVendor)
VALUES (@name,@phone,@email,@address,@city,@gstin,@notes,@category,1,1,0);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@name", vendorName);
                cmd.Parameters.AddWithValue("@phone", (object)phone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@email", (object)email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@address", (object)address ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@city", (object)city ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@gstin", (object)gstin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@category", (object)contactPerson ?? DBNull.Value);
                int vendorId = Convert.ToInt32(cmd.ExecuteScalar());
                options.Diagnostics?.CreatedVendors.Add(vendorName);
                return vendorId;
            }
        }

        private int EnsureSiteId(SqlConnection conn, SqlTransaction transaction, int clientId, string clientName, string siteName, string address, string city, string contactPerson, string phone, string siteType, string notes, ExcelImportExecutionOptions options)
        {
            int? existingId = FindSiteId(conn, transaction, clientId, siteName);
            string fullAddress = address;
            if (!string.IsNullOrWhiteSpace(contactPerson) || !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(siteType) || !string.IsNullOrWhiteSpace(notes))
            {
                fullAddress = (fullAddress ?? string.Empty)
                    + (string.IsNullOrWhiteSpace(fullAddress) ? string.Empty : Environment.NewLine)
                    + string.Join(" | ", new[] { contactPerson, phone, siteType, notes }.Where(x => !string.IsNullOrWhiteSpace(x)));
            }

            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE ClientSites
SET Address=COALESCE(NULLIF(@address,''), Address), City=COALESCE(NULLIF(@city,''), City)
WHERE SiteID=@id",
                    new SqlParameter("@address", (object)fullAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Site: " + clientName + " / " + siteName);
                return existingId.Value;
            }

            using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO ClientSites (ClientID, SiteName, Address, City, ACSystemCount, RefrigerationSystemCount, CoolingTowerCount, IsCritical)
VALUES (@clientId,@siteName,@address,@city,0,0,0,0);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@clientId", clientId);
                cmd.Parameters.AddWithValue("@siteName", siteName);
                cmd.Parameters.AddWithValue("@address", (object)fullAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@city", (object)city ?? DBNull.Value);
                int siteId = Convert.ToInt32(cmd.ExecuteScalar());
                options.Diagnostics?.CreatedSites.Add(clientName + " / " + siteName);
                return siteId;
            }
        }

        private int EnsureEmployeeId(SqlConnection conn, SqlTransaction transaction, string employeeCode, string employeeName, string designation, string department, string phone, string whatsApp, string status, ExcelImportExecutionOptions options)
        {
            int? existingId = !string.IsNullOrWhiteSpace(employeeCode)
                ? GetScalarInt(conn, transaction, "SELECT TOP 1 EmployeeID FROM Employees WHERE EmployeeCode=@code", new SqlParameter("@code", employeeCode))
                : null;
            if (!existingId.HasValue)
                existingId = FindEmployeeIdByName(conn, transaction, employeeName);
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(phone))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 EmployeeID FROM Employees WHERE Phone=@phone", new SqlParameter("@phone", phone));

            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE Employees
SET Name=COALESCE(NULLIF(@name,''), Name),
    Designation=COALESCE(NULLIF(@designation,''), Designation),
    Department=COALESCE(NULLIF(@department,''), Department),
    Phone=COALESCE(NULLIF(@phone,''), Phone),
    WhatsAppNumber=COALESCE(NULLIF(@whatsapp,''), WhatsAppNumber),
    Status=COALESCE(NULLIF(@status,''), Status)
WHERE EmployeeID=@id",
                    new SqlParameter("@name", (object)employeeName ?? DBNull.Value),
                    new SqlParameter("@designation", (object)designation ?? DBNull.Value),
                    new SqlParameter("@department", (object)department ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@whatsapp", (object)whatsApp ?? DBNull.Value),
                    new SqlParameter("@status", (object)status ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Employee: " + employeeName);
                return existingId.Value;
            }

            string safeCode = !string.IsNullOrWhiteSpace(employeeCode)
                ? employeeCode
                : "EMP-AUTO-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO Employees (EmployeeCode, Name, Designation, Department, Phone, WhatsAppNumber, JoiningDate, Status)
VALUES (@code,@name,@designation,@department,@phone,@whatsapp,GETDATE(),@status);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@code", safeCode);
                cmd.Parameters.AddWithValue("@name", employeeName);
                cmd.Parameters.AddWithValue("@designation", (object)designation ?? "Technician");
                cmd.Parameters.AddWithValue("@department", (object)department ?? "Service");
                cmd.Parameters.AddWithValue("@phone", (object)phone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@whatsapp", (object)whatsApp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@status", (object)status ?? "Active");
                int employeeId = Convert.ToInt32(cmd.ExecuteScalar());
                options.Diagnostics?.CreatedEmployees.Add(employeeName);
                return employeeId;
            }
        }

        private string NormalizeModuleStatus(ExcelImportModule module, string status, ExcelImportExecutionOptions options)
        {
            string value = (status ?? string.Empty).Trim();
            string normalized = value;
            string lower = value.ToLowerInvariant();
            switch (module)
            {
                case ExcelImportModule.Jobs:
                    if (lower.Contains("assign")) normalized = "Assigned";
                    else if (lower.Contains("progress")) normalized = "In Progress";
                    else if (lower.Contains("hold")) normalized = "On Hold";
                    else if (lower.Contains("complete") || lower.Contains("close")) normalized = "Completed";
                    else if (lower.Contains("cancel")) normalized = "Cancelled";
                    else normalized = "New";
                    break;
                case ExcelImportModule.Invoices:
                    if (lower.Contains("part")) normalized = "Partially Paid";
                    else if (lower.Contains("paid")) normalized = "Paid";
                    else if (lower.Contains("overdue")) normalized = "Overdue";
                    else if (lower.Contains("sent")) normalized = "Sent";
                    else if (lower.Contains("cancel")) normalized = "Cancelled";
                    else normalized = "Draft";
                    break;
                case ExcelImportModule.Quotations:
                    if (lower.Contains("accept")) normalized = "Accepted";
                    else if (lower.Contains("reject")) normalized = "Rejected";
                    else if (lower.Contains("expire")) normalized = "Expired";
                    else if (lower.Contains("sent")) normalized = "Sent";
                    else normalized = "Draft";
                    break;
                case ExcelImportModule.Employees:
                    if (lower.Contains("leave")) normalized = "On Leave";
                    else if (lower.Contains("terminate")) normalized = "Terminated";
                    else if (lower.Contains("inactive")) normalized = "Inactive";
                    else normalized = "Active";
                    break;
                case ExcelImportModule.AMC:
                    if (lower.Contains("cancel")) normalized = "Cancelled";
                    else if (lower.Contains("draft")) normalized = "Draft";
                    else if (lower.Contains("expire")) normalized = "Expired";
                    else if (lower.Contains("inactive")) normalized = "Inactive";
                    else normalized = "Active";
                    break;
                default:
                    break;
            }

            if (!string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase))
                options.Diagnostics?.NormalizedStatuses.Add(value + " -> " + normalized);
            return normalized;
        }

        private string NormalizeUnit(string unit, ExcelImportExecutionOptions options)
        {
            string value = (unit ?? string.Empty).Trim();
            string normalized = _unitMeasurements.NormalizeForStorage(string.IsNullOrWhiteSpace(value)
                ? UnitMeasurementService.DefaultCode
                : value);

            if (!string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase))
                options.Diagnostics?.NormalizedUnits.Add(value + " -> " + normalized);
            return string.IsNullOrWhiteSpace(normalized) ? UnitMeasurementService.DefaultCode : normalized;
        }

        private bool ImportQuotationRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string quotationNumber = GetCell(sheet, row, map, "QuotationNumber");
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string description = GetCell(sheet, row, map, "Description");
            string notes = NullIfEmpty(GetCell(sheet, row, map, "Notes")) ?? description;
            decimal amount = GetDecimal(sheet, row, map, "Amount");
            decimal taxableAmount = GetDecimal(sheet, row, map, "TaxableAmount");
            bool hasLineItems = HasQuotationLineItem(sheet, row, map);
            string status = NormalizeModuleStatus(ExcelImportModule.Quotations, NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Draft", options);
            DateTime quotationDate = GetDate(sheet, row, map, "QuotationDate");
            DateTime validUntil = GetDate(sheet, row, map, "ValidUntil");
            QuotationImportWorkflow workflow = ResolveQuotationImportWorkflow(options);

            if (string.IsNullOrWhiteSpace(quotationNumber))
                return AddError(result, row, "Missing required field: QuotationNumber");
            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");
            if (string.IsNullOrWhiteSpace(description))
                return AddError(result, row, "Missing required field: Description");

            int clientId = EnsureClientId(conn, transaction, clientName, null, null, null, null, null, null, null, options);
            int? siteId = string.IsNullOrWhiteSpace(siteName) ? (int?)null : EnsureSiteId(conn, transaction, clientId, clientName, siteName, null, null, null, null, null, null, options);

            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 BidID FROM Quotations WHERE QuotationNumber = @number", new SqlParameter("@number", quotationNumber));
            int quotationId;
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE Quotations SET ClientID=@clientId, SiteID=@siteId, TenderName=@title, BidValue=@value,
SubmittedDate=@quotationDate, DueDate=@validUntil, Status=@status, Notes=@notes,
IsMultiLine=CASE WHEN @hasLineItems=1 THEN 1 ELSE ISNULL(IsMultiLine,0) END,
TotalTaxableValue=CASE WHEN @taxableValue > 0 THEN @taxableValue ELSE @value END,
TotalGSTAmount=CASE WHEN @taxableValue > 0 THEN @value - @taxableValue ELSE ISNULL(TotalGSTAmount,0) END,
TotalWithGST=@value,
CommercialFlow=@commercialFlow, CustomerDocumentStatus=@customerDocStatus,
SupplierDocumentStatus=@supplierDocStatus, FlowNotes=@flowNotes
WHERE BidID=@id",
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@title", (object)description ?? DBNull.Value),
                    new SqlParameter("@value", amount),
                    new SqlParameter("@taxableValue", taxableAmount),
                    new SqlParameter("@hasLineItems", hasLineItems ? 1 : 0),
                    new SqlParameter("@quotationDate", quotationDate),
                    new SqlParameter("@validUntil", validUntil),
                    new SqlParameter("@status", status),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@commercialFlow", workflow.CommercialFlow),
                    new SqlParameter("@customerDocStatus", workflow.CustomerDocumentStatus),
                    new SqlParameter("@supplierDocStatus", workflow.SupplierDocumentStatus),
                    new SqlParameter("@flowNotes", workflow.FlowNotes),
                    new SqlParameter("@id", existingId.Value));
                quotationId = existingId.Value;
            }
            else
            {
                quotationId = ExecuteScalarInt(conn, transaction, @"
INSERT INTO Quotations (QuotationNumber, ClientID, SiteID, TenderName, BidValue, SubmittedDate, DueDate, Status, Notes, TotalTaxableValue, TotalWithGST,
TotalGSTAmount, IsMultiLine, CommercialFlow, CustomerDocumentStatus, SupplierDocumentStatus, FlowNotes)
VALUES (@number,@clientId,@siteId,@title,@value,@quotationDate,@validUntil,@status,@notes,CASE WHEN @taxableValue > 0 THEN @taxableValue ELSE @value END,@value,
CASE WHEN @taxableValue > 0 THEN @value - @taxableValue ELSE 0 END,@hasLineItems,
@commercialFlow,@customerDocStatus,@supplierDocStatus,@flowNotes);
SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new SqlParameter("@number", quotationNumber),
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@title", (object)description ?? DBNull.Value),
                    new SqlParameter("@value", amount),
                    new SqlParameter("@taxableValue", taxableAmount),
                    new SqlParameter("@hasLineItems", hasLineItems ? 1 : 0),
                    new SqlParameter("@quotationDate", quotationDate),
                    new SqlParameter("@validUntil", validUntil),
                    new SqlParameter("@status", status),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@commercialFlow", workflow.CommercialFlow),
                    new SqlParameter("@customerDocStatus", workflow.CustomerDocumentStatus),
                    new SqlParameter("@supplierDocStatus", workflow.SupplierDocumentStatus),
                    new SqlParameter("@flowNotes", workflow.FlowNotes));
            }

            ImportQuotationLineItem(conn, transaction, sheet, map, row, quotationId, options);
            return true;
        }

        private static bool HasQuotationLineItem(ExcelWorksheet sheet, int row, Dictionary<string, int> map)
        {
            return !string.IsNullOrWhiteSpace(GetCell(sheet, row, map, "LineDescription"));
        }

        private static void ImportQuotationLineItem(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, int quotationId, ExcelImportExecutionOptions options)
        {
            string description = GetCell(sheet, row, map, "LineDescription");
            if (string.IsNullOrWhiteSpace(description))
                return;

            EnsureQuotationLineItemDescriptionCapacity(conn, transaction);

            string resetKey = quotationId.ToString(CultureInfo.InvariantCulture);
            if (!options.QuotationLineItemsReset.Contains(resetKey))
            {
                Execute(conn, transaction, "DELETE FROM QuotationLineItems WHERE TenderBidId=@id", new SqlParameter("@id", quotationId));
                options.QuotationLineItemsReset.Add(resetKey);
            }

            decimal quantity = GetDecimal(sheet, row, map, "LineQuantity");
            if (quantity <= 0m)
                quantity = 1m;
            string unit = NullIfEmpty(GetCell(sheet, row, map, "LineUnit")) ?? UnitMeasurementService.DefaultCode;
            decimal rate = GetDecimal(sheet, row, map, "LineRate");
            decimal lineAmount = GetDecimal(sheet, row, map, "LineAmount");
            if (lineAmount <= 0m)
                lineAmount = Math.Round(quantity * rate, 2);
            if (rate <= 0m && quantity > 0m)
                rate = Math.Round(lineAmount / quantity, 2);
            decimal gstRate = GetDecimal(sheet, row, map, "LineGSTRate");
            if (gstRate <= 0m)
                gstRate = 18m;
            decimal gstAmount = Math.Round(lineAmount * gstRate / 100m, 2);
            int sortOrder = GetScalarInt(conn, transaction, "SELECT COUNT(1) FROM QuotationLineItems WHERE TenderBidId=@id", new SqlParameter("@id", quotationId)) ?? 0;

            Execute(conn, transaction, @"
INSERT INTO QuotationLineItems
(TenderBidId, SortOrder, Category, ItemDescription, Quantity, Unit, HsnSacCode, GSTRatePct,
 CostPerUnit, SellPricePerUnit, TaxableLineTotal, GSTAmount, MarginPct, StockAvailable, Shortfall,
 IsInternalLabour, AnalysisStatus, AnalysisNotes, IsSellPriceManual, CreatedDate, ModifiedDate)
VALUES
(@TenderBidId, @SortOrder, @Category, @ItemDescription, @Quantity, @Unit, @HsnSacCode, @GSTRatePct,
 @CostPerUnit, @SellPricePerUnit, @TaxableLineTotal, @GSTAmount, @MarginPct, 0, 0,
 0, @AnalysisStatus, @AnalysisNotes, 1, GETDATE(), GETDATE())",
                new SqlParameter("@TenderBidId", quotationId),
                new SqlParameter("@SortOrder", sortOrder),
                new SqlParameter("@Category", "Imported"),
                new SqlParameter("@ItemDescription", description.Trim()),
                new SqlParameter("@Quantity", quantity),
                new SqlParameter("@Unit", unit),
                new SqlParameter("@HsnSacCode", DBNull.Value),
                DecimalParameter("@GSTRatePct", gstRate),
                DecimalParameter("@CostPerUnit", 0m),
                DecimalParameter("@SellPricePerUnit", rate),
                DecimalParameter("@TaxableLineTotal", lineAmount),
                DecimalParameter("@GSTAmount", gstAmount),
                DecimalParameter("@MarginPct", 0m),
                new SqlParameter("@AnalysisStatus", "Imported"),
                new SqlParameter("@AnalysisNotes", "Imported from quotation workbook"));
        }

        private static void EnsureQuotationLineItemDescriptionCapacity(SqlConnection conn, SqlTransaction transaction)
        {
            Execute(conn, transaction, @"
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.QuotationLineItems')
      AND name = 'ItemDescription'
      AND max_length > 0
      AND max_length < 2000
)
BEGIN
    ALTER TABLE dbo.QuotationLineItems ALTER COLUMN ItemDescription NVARCHAR(1000) NOT NULL;
END");
        }

        private static SqlParameter DecimalParameter(string name, decimal value)
        {
            return new SqlParameter(name, System.Data.SqlDbType.Decimal)
            {
                Precision = 18,
                Scale = 2,
                Value = value
            };
        }

        private static QuotationImportWorkflow ResolveQuotationImportWorkflow(ExcelImportExecutionOptions options)
        {
            string direction = (options == null ? null : options.QuotationImportDirection) ?? string.Empty;
            if (direction.IndexOf("received", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (direction.IndexOf("supplier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 direction.IndexOf("vendor", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return new QuotationImportWorkflow
                {
                    CommercialFlow = "Revenue + Procurement",
                    CustomerDocumentStatus = "Quote Draft",
                    SupplierDocumentStatus = "Supplier Quote Received",
                    FlowNotes = "Imported as Received from Suppliers"
                };
            }

            if (direction.IndexOf("sent", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (direction.IndexOf("supplier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 direction.IndexOf("vendor", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return new QuotationImportWorkflow
                {
                    CommercialFlow = "Revenue + Procurement",
                    CustomerDocumentStatus = "Quote Draft",
                    SupplierDocumentStatus = "Supplier Quote Requested",
                    FlowNotes = "Imported as Sent to Suppliers"
                };
            }

            if (direction.IndexOf("received", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (direction.IndexOf("client", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 direction.IndexOf("customer", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return new QuotationImportWorkflow
                {
                    CommercialFlow = "Revenue",
                    CustomerDocumentStatus = "Client Response Received",
                    SupplierDocumentStatus = "Not Required",
                    FlowNotes = "Imported as Received from Clients"
                };
            }

            return new QuotationImportWorkflow
            {
                CommercialFlow = "Revenue",
                CustomerDocumentStatus = "Quote Sent",
                SupplierDocumentStatus = "Not Required",
                FlowNotes = "Imported as Sent to Clients"
            };
        }

        private sealed class QuotationImportWorkflow
        {
            public string CommercialFlow { get; set; }
            public string CustomerDocumentStatus { get; set; }
            public string SupplierDocumentStatus { get; set; }
            public string FlowNotes { get; set; }
        }

        private bool ImportInvoiceRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string invoiceNumber = GetCell(sheet, row, map, "InvoiceNumber");
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string description = GetCell(sheet, row, map, "Description");
            decimal amount = GetDecimal(sheet, row, map, "Amount");
            decimal taxAmount = GetDecimal(sheet, row, map, "TaxAmount");
            decimal totalAmount = GetDecimal(sheet, row, map, "TotalAmount");
            string status = NormalizeModuleStatus(ExcelImportModule.Invoices, NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Draft", options);
            DateTime invoiceDate = GetDate(sheet, row, map, "InvoiceDate");
            DateTime dueDate = GetDate(sheet, row, map, "DueDate");

            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return AddError(result, row, "Missing required field: InvoiceNumber");
            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");
            if (string.IsNullOrWhiteSpace(description))
                return AddError(result, row, "Missing required field: Description");

            int clientId = EnsureClientId(conn, transaction, clientName, null, null, null, null, null, null, null, options);
            int? siteId = string.IsNullOrWhiteSpace(siteName) ? (int?)null : EnsureSiteId(conn, transaction, clientId, clientName, siteName, null, null, null, null, null, null, options);

            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=@number", new SqlParameter("@number", invoiceNumber));
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE Invoices SET ClientID=@clientId, SiteID=@siteId, InvoiceDate=@invoiceDate, DueDate=@dueDate,
Subject=@subject, SubTotal=@amount, TaxAmount=@taxAmount, TotalAmount=@totalAmount, PaymentStatus=@status
WHERE InvoiceID=@id",
                    new SqlParameter("@clientId", clientId),
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
                Execute(conn, transaction, @"
INSERT INTO Invoices (InvoiceNumber, ClientID, SiteID, InvoiceDate, DueDate, Subject, SubTotal, TaxAmount, TotalAmount, PaymentStatus)
VALUES (@number,@clientId,@siteId,@invoiceDate,@dueDate,@subject,@amount,@taxAmount,@totalAmount,@status)",
                    new SqlParameter("@number", invoiceNumber),
                    new SqlParameter("@clientId", clientId),
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

        private bool ImportPaymentRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
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
            if (amountPaid <= 0m)
                return AddError(result, row, "AmountPaid must be greater than zero.");

            int? invoiceId = GetScalarInt(conn, transaction, "SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=@number", new SqlParameter("@number", invoiceNumber));
            if (!invoiceId.HasValue)
                return AddError(result, row, "Invoice not found: " + invoiceNumber);
            int clientId = !string.IsNullOrWhiteSpace(clientName)
                ? EnsureClientId(conn, transaction, clientName, null, null, null, null, null, null, null, options)
                : GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM Invoices WHERE InvoiceID=@id", new SqlParameter("@id", invoiceId.Value)).GetValueOrDefault();
            if (clientId <= 0)
                return AddError(result, row, "Client could not be resolved for invoice " + invoiceNumber);

            int? existingId = GetScalarInt(conn, transaction, @"
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
                Execute(conn, transaction, @"
UPDATE Payments SET ClientID=@clientId, PaymentMode=@paymentMode, Notes=@notes
WHERE PaymentID=@id",
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@paymentMode", paymentMode),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
            }
            else
            {
                string paymentNumber = "PAY-IMP-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + row.ToString(CultureInfo.InvariantCulture);
                Execute(conn, transaction, @"
INSERT INTO Payments (PaymentNumber, InvoiceID, ClientID, AmountPaid, PaymentDate, PaymentMode, ReferenceNumber, Notes)
VALUES (@paymentNumber,@invoiceId,@clientId,@amountPaid,@paymentDate,@paymentMode,@referenceNumber,@notes)",
                    new SqlParameter("@paymentNumber", paymentNumber),
                    new SqlParameter("@invoiceId", invoiceId.Value),
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@amountPaid", amountPaid),
                    new SqlParameter("@paymentDate", paymentDate),
                    new SqlParameter("@paymentMode", paymentMode),
                    new SqlParameter("@referenceNumber", (object)referenceNumber ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value));
            }

            return true;
        }

        private bool ImportPurchaseRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string poNumber = GetCell(sheet, row, map, "PONumber");
            string vendorName = GetCell(sheet, row, map, "VendorName");
            string itemDescription = GetCell(sheet, row, map, "ItemDescription");
            decimal quantity = GetDecimal(sheet, row, map, "Quantity");
            decimal unitPrice = GetDecimal(sheet, row, map, "UnitPrice");
            decimal totalAmount = GetDecimal(sheet, row, map, "TotalAmount");
            string unit = NormalizeUnit(NullIfEmpty(GetCell(sheet, row, map, "Unit")) ?? "Nos", options);
            string notes = GetCell(sheet, row, map, "Notes");
            DateTime purchaseDate = GetDate(sheet, row, map, "PurchaseDate");

            if (string.IsNullOrWhiteSpace(vendorName))
                return AddError(result, row, "Missing required field: SupplierName");
            if (string.IsNullOrWhiteSpace(itemDescription))
                return AddError(result, row, "Missing required field: ItemDescription");
            if (quantity <= 0m)
                return AddError(result, row, "Quantity must be greater than zero.");
            if (unitPrice <= 0m)
                return AddError(result, row, "UnitPrice must be greater than zero.");

            int vendorId = EnsureVendorId(conn, transaction, vendorName, null, null, null, null, null, null, notes, options);

            int? existingPoId = !string.IsNullOrWhiteSpace(poNumber)
                ? GetScalarInt(conn, transaction, "SELECT TOP 1 POID FROM PurchaseOrders WHERE PONumber=@poNumber ORDER BY POID DESC", new SqlParameter("@poNumber", poNumber))
                : GetScalarInt(conn, transaction, @"
SELECT TOP 1 po.POID
FROM PurchaseOrders po
LEFT JOIN PurchaseLineItems li ON li.POID = po.POID
WHERE po.VendorID=@vendorId AND CAST(po.PODate AS DATE)=@poDate
  AND ISNULL(li.Description,'') = ISNULL(@item,'')
ORDER BY po.POID DESC",
                new SqlParameter("@vendorId", vendorId),
                new SqlParameter("@poDate", purchaseDate.Date),
                new SqlParameter("@item", itemDescription ?? string.Empty));

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                int poId = existingPoId.GetValueOrDefault();
                if (poId <= 0)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO PurchaseOrders (VendorID, PONumber, PODate, PayByDate, TotalAmount, Status, Notes)
VALUES (@vendorId,@poNumber,@poDate,DATEADD(day,30,@poDate),0,'Pending',@notes);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@vendorId", vendorId);
                        cmd.Parameters.AddWithValue("@poNumber", poNumber);
                        cmd.Parameters.AddWithValue("@poDate", purchaseDate);
                        cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                        poId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
                else
                {
                    Execute(conn, transaction, @"
UPDATE PurchaseOrders
SET VendorID=@vendorId, PODate=@poDate, PayByDate=COALESCE(PayByDate, DATEADD(day,30,@poDate)),
    Notes=COALESCE(NULLIF(@notes,''), Notes)
WHERE POID=@id",
                        new SqlParameter("@vendorId", vendorId),
                        new SqlParameter("@poDate", purchaseDate),
                        new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                        new SqlParameter("@id", poId));
                }

                string resetKey = poNumber.Trim();
                if (!options.PurchaseLineItemsReset.Contains(resetKey))
                {
                    Execute(conn, transaction, "DELETE FROM PurchaseLineItems WHERE POID=@id", new SqlParameter("@id", poId));
                    options.PurchaseLineItemsReset.Add(resetKey);
                }

                Execute(conn, transaction, @"
INSERT INTO PurchaseLineItems (POID, Description, Quantity, UOM, Rate, Amount)
VALUES (@poId,@description,@qty,@unit,@rate,@amount)",
                    new SqlParameter("@poId", poId),
                    new SqlParameter("@description", (object)itemDescription ?? DBNull.Value),
                    new SqlParameter("@qty", quantity),
                    new SqlParameter("@unit", unit),
                    new SqlParameter("@rate", unitPrice),
                    new SqlParameter("@amount", totalAmount));

                Execute(conn, transaction, @"
UPDATE PurchaseOrders
SET TotalAmount = COALESCE((SELECT SUM(Amount) FROM PurchaseLineItems WHERE POID=@id), @total)
WHERE POID=@id",
                    new SqlParameter("@id", poId),
                    new SqlParameter("@total", totalAmount));

                return true;
            }

            if (existingPoId.HasValue)
            {
                Execute(conn, transaction, "UPDATE PurchaseOrders SET TotalAmount=@total, Notes=COALESCE(NULLIF(@notes,''), Notes) WHERE POID=@id",
                    new SqlParameter("@total", totalAmount),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@id", existingPoId.Value));

                Execute(conn, transaction, @"
UPDATE TOP (1) PurchaseLineItems
SET Description=@description, Quantity=@qty, UOM=@unit, Rate=@rate, Amount=@amount
WHERE POID=@id",
                    new SqlParameter("@description", (object)itemDescription ?? DBNull.Value),
                    new SqlParameter("@qty", quantity),
                    new SqlParameter("@unit", unit),
                    new SqlParameter("@rate", unitPrice),
                    new SqlParameter("@amount", totalAmount),
                    new SqlParameter("@id", existingPoId.Value));
            }
            else
            {
                poNumber = "PO-IMP-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + row.ToString(CultureInfo.InvariantCulture);
                int poId;
                using (SqlCommand cmd = new SqlCommand(@"
INSERT INTO PurchaseOrders (VendorID, PONumber, PODate, PayByDate, TotalAmount, Status, Notes)
VALUES (@vendorId,@poNumber,@poDate,DATEADD(day,30,@poDate),@totalAmount,'Pending',@notes);
SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@vendorId", vendorId);
                    cmd.Parameters.AddWithValue("@poNumber", poNumber);
                    cmd.Parameters.AddWithValue("@poDate", purchaseDate);
                    cmd.Parameters.AddWithValue("@totalAmount", totalAmount);
                    cmd.Parameters.AddWithValue("@notes", (object)notes ?? DBNull.Value);
                    poId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                Execute(conn, transaction, @"
INSERT INTO PurchaseLineItems (POID, Description, Quantity, UOM, Rate, Amount)
VALUES (@poId,@description,@qty,@unit,@rate,@amount)",
                    new SqlParameter("@poId", poId),
                    new SqlParameter("@description", (object)itemDescription ?? DBNull.Value),
                    new SqlParameter("@qty", quantity),
                    new SqlParameter("@unit", unit),
                    new SqlParameter("@rate", unitPrice),
                    new SqlParameter("@amount", totalAmount));
            }

            return true;
        }

        private bool ImportJobRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string technicianName = GetCell(sheet, row, map, "TechnicianName");
            string jobType = NullIfEmpty(GetCell(sheet, row, map, "JobType")) ?? "General";
            string description = GetCell(sheet, row, map, "Description");
            string status = NormalizeModuleStatus(ExcelImportModule.Jobs, NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "New", options);
            string priority = NullIfEmpty(GetCell(sheet, row, map, "Priority")) ?? "Medium";
            DateTime scheduledDate = GetDate(sheet, row, map, "ScheduledDate");

            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");
            if (string.IsNullOrWhiteSpace(description))
                return AddError(result, row, "Missing required field: Description");

            int clientId = EnsureClientId(conn, transaction, clientName, null, null, null, null, null, null, null, options);
            int? siteId = string.IsNullOrWhiteSpace(siteName) ? (int?)null : EnsureSiteId(conn, transaction, clientId, clientName, siteName, null, null, null, null, null, null, options);
            int? employeeId = string.IsNullOrWhiteSpace(technicianName) ? (int?)null : EnsureEmployeeId(conn, transaction, null, technicianName, "Technician", "Service", null, null, "Active", options);

            int? existingId = GetScalarInt(conn, transaction, @"
SELECT TOP 1 JobID FROM Jobs
WHERE ClientID=@clientId AND ISNULL(SiteID,0)=ISNULL(@siteId,0) AND CAST(ScheduledDate AS DATE)=@scheduledDate
  AND ISNULL(Description,'')=ISNULL(@description,'')",
                new SqlParameter("@clientId", clientId),
                new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                new SqlParameter("@scheduledDate", scheduledDate.Date),
                new SqlParameter("@description", (object)description ?? DBNull.Value));

            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
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
                Execute(conn, transaction, @"
INSERT INTO Jobs (JobNumber, ClientID, SiteID, Title, JobTitle, Description, AssignedEmployeeID, ScheduledDate, Priority, Status, JobType)
VALUES (@jobNumber,@clientId,@siteId,@title,@title,@description,@employeeId,@scheduledDate,@priority,@status,@jobType)",
                    new SqlParameter("@jobNumber", jobNumber),
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
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

        private bool ImportClientRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string clientName = GetCell(sheet, row, map, "ClientName");
            string contactPerson = GetCell(sheet, row, map, "ContactPerson");
            string phone = GetCell(sheet, row, map, "Phone");
            string email = GetCell(sheet, row, map, "Email");
            string address = GetCell(sheet, row, map, "Address");
            string city = GetCell(sheet, row, map, "City");
            string state = GetCell(sheet, row, map, "State");
            string gstin = GetCell(sheet, row, map, "GSTIN");
            string rawType = GetCell(sheet, row, map, "ClientType");
            if (string.IsNullOrWhiteSpace(rawType)) rawType = GetCell(sheet, row, map, "Client Type");
            if (string.IsNullOrWhiteSpace(rawType)) rawType = GetCell(sheet, row, map, "Type");
            if (string.IsNullOrWhiteSpace(rawType)) rawType = GetCell(sheet, row, map, "IndustryType");
            if (string.IsNullOrWhiteSpace(rawType)) rawType = GetCell(sheet, row, map, "Industry");
            string clientType = NormalizeClientType(rawType);
            bool hasClientType = !string.IsNullOrWhiteSpace(rawType);

            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");

            string finalAddress = string.IsNullOrWhiteSpace(state) ? address : (address + (string.IsNullOrWhiteSpace(address) ? string.Empty : ", ") + state);
            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=@name", new SqlParameter("@name", clientName));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(gstin))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE GSTNumber=@gstin", new SqlParameter("@gstin", gstin));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(phone))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ClientID FROM B2BClients WHERE Phone=@phone", new SqlParameter("@phone", phone));
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE B2BClients
SET PrimaryContact=COALESCE(NULLIF(@contact,''), PrimaryContact),
    Phone=COALESCE(NULLIF(@phone,''), Phone),
    Email=COALESCE(NULLIF(@email,''), Email),
    BillingAddress=COALESCE(NULLIF(@address,''), BillingAddress),
    City=COALESCE(NULLIF(@city,''), City),
    GSTNumber=COALESCE(NULLIF(@gstin,''), GSTNumber),
    IndustryType=CASE WHEN @hasClientType=1 THEN @clientType ELSE IndustryType END
WHERE ClientID=@id",
                    new SqlParameter("@contact", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)finalAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@clientType", clientType),
                    new SqlParameter("@hasClientType", hasClientType ? 1 : 0),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Client: " + clientName);
            }
            else
            {
                Execute(conn, transaction, @"
INSERT INTO B2BClients (CompanyName, IndustryType, PrimaryContact, Phone, Email, BillingAddress, City, GSTNumber, IsActive, CustomerSince)
VALUES (@name,@clientType,@contact,@phone,@email,@address,@city,@gstin,1,GETDATE())",
                    new SqlParameter("@name", clientName),
                    new SqlParameter("@clientType", clientType),
                    new SqlParameter("@contact", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)finalAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value));
                options.Diagnostics?.CreatedClients.Add(clientName);
            }

            return true;
        }

        private static string NormalizeClientType(string raw)
        {
            string value = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0) return "Other";
            if (value.Contains("res") || value.Contains("home") || value.Contains("apartment")) return "Residential";
            if (value.Contains("gov") || value.Contains("public") || value.Contains("municipal")) return "Government";
            if (value.Contains("industrial") || value.Contains("manufact") || value.Contains("plant") || value.Contains("factory") || value.Contains("pharma")) return "Industrial";
            if (value.Contains("commercial") || value.Contains("office") || value.Contains("bank") || value.Contains("hotel") || value.Contains("hospital") || value.Contains("health") || value.Contains("retail") || value == "it") return "Commercial";
            if (value.Contains("other")) return "Other";
            return "Other";
        }

        private bool ImportEmployeeRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
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
            string status = NormalizeModuleStatus(ExcelImportModule.Employees, NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Active", options);
            DateTime joiningDate = GetDate(sheet, row, map, "JoiningDate");

            if (string.IsNullOrWhiteSpace(employeeCode))
                return AddError(result, row, "Missing required field: EmployeeCode");
            if (string.IsNullOrWhiteSpace(employeeName))
                return AddError(result, row, "Missing required field: EmployeeName");

            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 EmployeeID FROM Employees WHERE EmployeeCode=@code", new SqlParameter("@code", employeeCode));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(phone))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 EmployeeID FROM Employees WHERE Phone=@phone", new SqlParameter("@phone", phone));
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
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
                options.Diagnostics?.DuplicateRefreshes.Add("Employee: " + employeeName);
            }
            else
            {
                Execute(conn, transaction, @"
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
                options.Diagnostics?.CreatedEmployees.Add(employeeName);
            }

            return true;
        }

        private bool ImportVendorRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
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
                return AddError(result, row, "Missing required field: SupplierName");

            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=@name", new SqlParameter("@name", vendorName));
            if (!existingId.HasValue && !string.IsNullOrWhiteSpace(gstin))
                existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 VendorID FROM Vendors WHERE GSTNumber=@gstin", new SqlParameter("@gstin", gstin));
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE Vendors SET Phone=@phone, Email=@email, Address=@address, City=@city, GSTNumber=@gstin,
Notes=@notes, Category=COALESCE(NULLIF(@contactPerson,''), Category), IsSupplier=1
WHERE VendorID=@id",
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)address ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@contactPerson", (object)contactPerson ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Supplier: " + vendorName);
            }
            else
            {
                Execute(conn, transaction, @"
INSERT INTO Vendors (VendorName, Phone, Email, Address, City, GSTNumber, Notes, Category, IsActive, IsSupplier, IsServiceVendor)
VALUES (@name,@phone,@email,@address,@city,@gstin,@notes,@category,1,1,0)",
                    new SqlParameter("@name", vendorName),
                    new SqlParameter("@phone", (object)phone ?? DBNull.Value),
                    new SqlParameter("@email", (object)email ?? DBNull.Value),
                    new SqlParameter("@address", (object)address ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@gstin", (object)gstin ?? DBNull.Value),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@category", (object)contactPerson ?? DBNull.Value));
                options.Diagnostics?.CreatedVendors.Add(vendorName);
            }

            return true;
        }

        private bool ImportSiteRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
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

            int clientId = EnsureClientId(conn, transaction, clientName, contactPerson, phone, null, address, city, null, null, options);
            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 SiteID FROM ClientSites WHERE ClientID=@clientId AND SiteName=@siteName",
                new SqlParameter("@clientId", clientId),
                new SqlParameter("@siteName", siteName));

            if (existingId.HasValue)
            {
                string fullAddress = address;
                if (!string.IsNullOrWhiteSpace(contactPerson) || !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(siteType) || !string.IsNullOrWhiteSpace(notes))
                {
                    fullAddress = (fullAddress ?? string.Empty)
                        + (string.IsNullOrWhiteSpace(fullAddress) ? string.Empty : Environment.NewLine)
                        + string.Join(" | ", new[] { contactPerson, phone, siteType, notes }.Where(x => !string.IsNullOrWhiteSpace(x)));
                }
                Execute(conn, transaction, "UPDATE ClientSites SET Address=@address, City=@city WHERE SiteID=@id",
                    new SqlParameter("@address", (object)fullAddress ?? DBNull.Value),
                    new SqlParameter("@city", (object)city ?? DBNull.Value),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Site: " + clientName + " / " + siteName);
            }
            else
            {
                EnsureSiteId(conn, transaction, clientId, clientName, siteName, address, city, contactPerson, phone, siteType, notes, options);
            }

            return true;
        }

        private bool ImportInventoryRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string itemName = GetCell(sheet, row, map, "ItemName");
            string category = GetCell(sheet, row, map, "Category");
            string unit = NormalizeUnit(NullIfEmpty(GetCell(sheet, row, map, "Unit")) ?? "Nos", options);
            decimal currentStock = GetDecimal(sheet, row, map, "CurrentStock");
            decimal lastPurchaseRate = GetDecimal(sheet, row, map, "LastPurchaseRate");
            decimal reorderLevel = GetDecimal(sheet, row, map, "ReorderLevel");

            if (string.IsNullOrWhiteSpace(itemName))
                return AddError(result, row, "Missing required field: ItemName");
            if (currentStock < 0)
                return AddError(result, row, "CurrentStock cannot be negative.");
            if (lastPurchaseRate < 0)
                return AddError(result, row, "LastPurchaseRate cannot be negative.");
            if (reorderLevel < 0)
                return AddError(result, row, "ReorderLevel cannot be negative.");

            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ItemID FROM StockItems WHERE ItemName=@name AND ISNULL(IsActive,1)=1", new SqlParameter("@name", itemName));
            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE StockItems
SET Category=@category, CurrentStock=@stock, Unit=@unit, LastPurchaseRate=@rate,
    ReorderLevel=@reorder, LastUpdated=GETDATE()
WHERE ItemID=@id AND ISNULL(IsActive,1)=1",
                    new SqlParameter("@category", (object)category ?? DBNull.Value),
                    new SqlParameter("@stock", currentStock),
                    new SqlParameter("@unit", unit),
                    new SqlParameter("@rate", lastPurchaseRate),
                    new SqlParameter("@reorder", reorderLevel),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("Inventory: " + itemName);
            }
            else
            {
                Execute(conn, transaction, @"
INSERT INTO StockItems (ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel, IsActive, LastUpdated)
VALUES (@name, @category, @stock, @unit, @rate, @reorder, 1, GETDATE())",
                    new SqlParameter("@name", itemName),
                    new SqlParameter("@category", (object)category ?? DBNull.Value),
                    new SqlParameter("@stock", currentStock),
                    new SqlParameter("@unit", unit),
                    new SqlParameter("@rate", lastPurchaseRate),
                    new SqlParameter("@reorder", reorderLevel));
            }

            return true;
        }

        private bool ImportAmcRow(SqlConnection conn, SqlTransaction transaction, ExcelWorksheet sheet, Dictionary<string, int> map, int row, ExcelImportResult result, ExcelImportExecutionOptions options)
        {
            string contractNumber = GetCell(sheet, row, map, "ContractNumber");
            string clientName = GetCell(sheet, row, map, "ClientName");
            string siteName = GetCell(sheet, row, map, "SiteName");
            string equipmentType = NullIfEmpty(GetCell(sheet, row, map, "EquipmentType")) ?? "HVAC Equipment";
            string notes = GetCell(sheet, row, map, "Notes");
            decimal contractValue = GetDecimal(sheet, row, map, "ContractValue");
            string status = NormalizeModuleStatus(ExcelImportModule.AMC, NullIfEmpty(GetCell(sheet, row, map, "Status")) ?? "Active", options);
            DateTime startDate = GetDate(sheet, row, map, "ContractStartDate");
            DateTime endDate = GetDate(sheet, row, map, "ContractEndDate");

            if (string.IsNullOrWhiteSpace(contractNumber))
                return AddError(result, row, "Missing required field: ContractNumber");
            if (string.IsNullOrWhiteSpace(clientName))
                return AddError(result, row, "Missing required field: ClientName");
            if (contractValue < 0m)
                return AddError(result, row, "ContractValue cannot be negative.");
            if (endDate < startDate)
                endDate = startDate.AddYears(1).AddDays(-1);

            const string amcType = "Comprehensive";
            const string billingCycle = "Annual";
            const string coverageType = "Comprehensive";
            const int visitsPerYear = 2;
            const string maintenanceFrequency = "Annual";
            const string contractType = "AMC";
            decimal monthlyValue = contractValue / 12m;

            int clientId = EnsureClientId(conn, transaction, clientName, null, null, null, null, null, null, null, options);
            int? siteId = string.IsNullOrWhiteSpace(siteName) ? (int?)null : EnsureSiteId(conn, transaction, clientId, clientName, siteName, null, null, null, null, null, notes, options);

            int? existingId = GetScalarInt(conn, transaction, "SELECT TOP 1 ContractID FROM AMCContracts WHERE AMCNumber=@number",
                new SqlParameter("@number", contractNumber));

            if (existingId.HasValue)
            {
                Execute(conn, transaction, @"
UPDATE AMCContracts
SET ClientID=@clientId,
    SiteID=@siteId,
    EquipmentDesc=@equipment,
    AMCType=@amcType,
    StartDate=@startDate,
    EndDate=@endDate,
    ContractValue=@contractValue,
    BillingCycle=@billingCycle,
    CoverageType=@coverageType,
    VisitsPerYear=@visitsPerYear,
    Status=@status,
    Notes=@notes,
    UpdatedAt=GETDATE(),
    MonthlyValue=@monthlyValue,
    AnnualValue=@annualValue,
    ContractStatus=@status,
    MaintenanceFrequency=@maintenanceFrequency,
    ContractType=@contractType
WHERE ContractID=@id",
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@equipment", equipmentType),
                    new SqlParameter("@amcType", amcType),
                    new SqlParameter("@startDate", startDate),
                    new SqlParameter("@endDate", endDate),
                    new SqlParameter("@contractValue", contractValue),
                    new SqlParameter("@billingCycle", billingCycle),
                    new SqlParameter("@coverageType", coverageType),
                    new SqlParameter("@visitsPerYear", visitsPerYear),
                    new SqlParameter("@status", status),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@monthlyValue", monthlyValue),
                    new SqlParameter("@annualValue", contractValue),
                    new SqlParameter("@maintenanceFrequency", maintenanceFrequency),
                    new SqlParameter("@contractType", contractType),
                    new SqlParameter("@id", existingId.Value));
                options.Diagnostics?.DuplicateRefreshes.Add("AMC: " + contractNumber);
            }
            else
            {
                Execute(conn, transaction, @"
INSERT INTO AMCContracts
    (AMCNumber, ClientID, SiteID, EquipmentDesc, AMCType, StartDate, EndDate,
     ContractValue, BillingCycle, CoverageType, VisitsPerYear, Status, Notes, CreatedAt, UpdatedAt,
     MonthlyValue, AnnualValue, ContractStatus, MaintenanceFrequency, ContractType)
VALUES
    (@contractNumber, @clientId, @siteId, @equipment, @amcType, @startDate, @endDate,
     @contractValue, @billingCycle, @coverageType, @visitsPerYear, @status, @notes, GETDATE(), GETDATE(),
     @monthlyValue, @annualValue, @status, @maintenanceFrequency, @contractType)",
                    new SqlParameter("@contractNumber", contractNumber),
                    new SqlParameter("@clientId", clientId),
                    new SqlParameter("@siteId", (object)siteId ?? DBNull.Value),
                    new SqlParameter("@equipment", equipmentType),
                    new SqlParameter("@amcType", amcType),
                    new SqlParameter("@startDate", startDate),
                    new SqlParameter("@endDate", endDate),
                    new SqlParameter("@contractValue", contractValue),
                    new SqlParameter("@billingCycle", billingCycle),
                    new SqlParameter("@coverageType", coverageType),
                    new SqlParameter("@visitsPerYear", visitsPerYear),
                    new SqlParameter("@status", status),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value),
                    new SqlParameter("@monthlyValue", monthlyValue),
                    new SqlParameter("@annualValue", contractValue),
                    new SqlParameter("@maintenanceFrequency", maintenanceFrequency),
                    new SqlParameter("@contractType", contractType));
            }

            return true;
        }

        public static string[] GetHeaders(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    return new[] { "QuotationNumber", "QuotationDate", "ClientName", "SiteName", "Description", "Amount", "TaxableAmount", "Status", "ValidUntil", "LineDescription", "LineQuantity", "LineUnit", "LineRate", "LineAmount", "LineGSTRate", "Notes" };
                case ExcelImportModule.Invoices:
                    return new[] { "InvoiceNumber", "InvoiceDate", "ClientName", "SiteName", "Description", "Amount", "TaxAmount", "TotalAmount", "Status", "DueDate" };
                case ExcelImportModule.Payments:
                    return new[] { "PaymentDate", "InvoiceNumber", "ClientName", "AmountPaid", "PaymentMode", "ReferenceNumber", "Notes" };
                case ExcelImportModule.Purchases:
                    return new[] { "PurchaseDate", "PONumber", "SupplierName", "ItemDescription", "Quantity", "Unit", "UnitPrice", "TotalAmount", "Notes" };
                case ExcelImportModule.Jobs:
                    return new[] { "JobDate", "ClientName", "SiteName", "TechnicianName", "JobType", "Description", "Status", "Priority", "ScheduledDate" };
                case ExcelImportModule.Clients:
                    return new[] { "ClientName", "ContactPerson", "Phone", "Email", "Address", "City", "State", "GSTIN", "Notes" };
                case ExcelImportModule.Employees:
                    return new[] { "EmployeeCode", "EmployeeName", "Designation", "Department", "Phone", "WhatsApp", "BloodGroup", "Aadhaar", "PAN", "JoiningDate", "Status" };
                case ExcelImportModule.Vendors:
                    return new[] { "SupplierName", "ContactPerson", "Phone", "Email", "Address", "City", "GSTIN", "Notes" };
                case ExcelImportModule.Sites:
                    return new[] { "SiteName", "ClientName", "Address", "City", "ContactPerson", "Phone", "SiteType", "Notes" };
                case ExcelImportModule.Inventory:
                    return new[] { "ItemName", "Category", "CurrentStock", "Unit", "LastPurchaseRate", "ReorderLevel", "StockValue", "Notes" };
                case ExcelImportModule.SupplierItemPrices:
                    return new[] { "ItemID", "ItemName", "Category", "Unit", "SupplierID", "SupplierName", "Rate", "EffectiveDate", "IsPreferred", "IsActive", "Source", "Notes" };
                case ExcelImportModule.AMC:
                    return new[] { "ContractNumber", "ClientName", "SiteName", "ContractStartDate", "ContractEndDate", "ContractValue", "Status", "EquipmentType", "Notes" };
                default:
                    return Array.Empty<string>();
            }
        }

        public static string[] GetRequiredHeaders(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    return new[] { "QuotationNumber", "ClientName", "Description", "Amount" };
                case ExcelImportModule.Invoices:
                    return new[] { "InvoiceNumber", "ClientName", "Description", "Amount", "TotalAmount" };
                case ExcelImportModule.Payments:
                    return new[] { "PaymentDate", "InvoiceNumber", "AmountPaid" };
                case ExcelImportModule.Purchases:
                    return new[] { "SupplierName", "ItemDescription", "Quantity", "UnitPrice" };
                case ExcelImportModule.Jobs:
                    return new[] { "ClientName", "JobType", "Description" };
                case ExcelImportModule.Clients:
                    return new[] { "ClientName" };
                case ExcelImportModule.Employees:
                    return new[] { "EmployeeCode", "EmployeeName" };
                case ExcelImportModule.Vendors:
                    return new[] { "SupplierName" };
                case ExcelImportModule.Sites:
                    return new[] { "SiteName", "ClientName" };
                case ExcelImportModule.Inventory:
                    return new[] { "ItemName" };
                case ExcelImportModule.SupplierItemPrices:
                    return new[] { "ItemName", "SupplierName", "Rate" };
                case ExcelImportModule.AMC:
                    return new[] { "ContractNumber", "ClientName" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string[] GetSampleRow(ExcelImportModule module)
        {
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    return new[] { "QTN-2026-04-0001", "17/04/2026", "ABC Corp", "Main Plant", "Quarterly AMC quotation", "29500", "25000", "Draft", "30/04/2026", "Service charges", "1", "Nos", "25000", "25000", "18", "Imported quotation notes" };
                case ExcelImportModule.Invoices:
                    return new[] { "INV-2026-04-0001", "17/04/2026", "ABC Corp", "Main Plant", "Service invoice", "10000", "1800", "11800", "Pending", "02/05/2026" };
                case ExcelImportModule.Payments:
                    return new[] { "17/04/2026", "INV-2026-04-0001", "ABC Corp", "11800", "NEFT", "UTR12345", "April collection" };
                case ExcelImportModule.Purchases:
                    return new[] { "17/04/2026", "Cool Parts Pvt Ltd", "Copper pipe", "10", "500", "5000", "Urgent stock" };
                case ExcelImportModule.Jobs:
                    return new[] { "17/04/2026", "ABC Corp", "Main Plant", "Ramesh Patil", "PM Visit", "Quarterly maintenance", "Pending", "Medium", "18/04/2026" };
                case ExcelImportModule.Clients:
                    return new[] { "ABC Corp", "Anita Sharma", "9876543210", "ops@abccorp.in", "Thane MIDC", "Thane", "Maharashtra", "27ABCDE1234F1Z0", "Priority account" };
                case ExcelImportModule.Employees:
                    return new[] { "MSE001", "Ramesh Patil", "Technician", "Service", "9876543210", "9876543210", "B+", "123412341234", "ABCDE1234F", "01/04/2026", "Active" };
                case ExcelImportModule.Vendors:
                    return new[] { "Cool Parts Pvt Ltd", "Rajesh Shah", "9988776655", "sales@coolparts.in", "Bhiwandi", "Mumbai", "27ABCDE1234F1Z0", "Fast supplier" };
                case ExcelImportModule.Sites:
                    return new[] { "Main Plant", "ABC Corp", "Industrial Area", "Thane", "Anita Sharma", "9876543210", "Industrial", "Primary service site" };
                case ExcelImportModule.Inventory:
                    return new[] { "Copper Pipe 1/2 inch", "Copper", "25", "Mtr", "320", "5", "8000", "Opening stock" };
                case ExcelImportModule.SupplierItemPrices:
                    return new[] { "101", "Copper Pipe 1/2 inch", "Copper", "Mtr", "", "Cool Parts Pvt Ltd", "320", "22/06/2026", "Yes", "Yes", "Legacy supplier list", "Preferred supplier for copper pipe" };
                case ExcelImportModule.AMC:
                    return new[] { "AMC-2026-001", "ABC Corp", "Main Plant", "01/04/2026", "31/03/2027", "50000", "Active", "HVAC Unit", "Annual maintenance" };
                default:
                    return Array.Empty<string>();
            }
        }
    }
}

