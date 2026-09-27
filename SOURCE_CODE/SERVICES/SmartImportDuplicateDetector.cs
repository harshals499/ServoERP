using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using Dapper;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class SmartImportDuplicateScan
    {
        public int UploadDuplicateRows { get; set; }
        public int ExistingMatchRows { get; set; }
        public int AmbiguousMatchRows { get; set; }
        public int ExistingDuplicateGroups { get; set; }
        public List<string> Details { get; } = new List<string>();
        public List<SmartImportDuplicateGroup> Groups { get; } = new List<SmartImportDuplicateGroup>();
    }

    public sealed class SmartImportDuplicateGroup
    {
        public string MatchReason { get; set; }
        public List<SmartImportDuplicateRecord> Records { get; } = new List<SmartImportDuplicateRecord>();
    }

    public sealed class SmartImportDuplicateRecord
    {
        public string RecordId { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// Applies the same normalized identity rules to every Smart Upload card.
    /// This is deliberately advisory: the normal import transaction remains responsible
    /// for refreshing an existing record or safely rejecting an invalid row.
    /// </summary>
    public sealed class SmartImportDuplicateDetector
    {
        private readonly DatabaseManager _database = new DatabaseManager();

        public SmartImportDuplicateScan Scan(ExcelImportModule module, IList<Dictionary<string, string>> rows, bool includeDatabase)
        {
            var result = new SmartImportDuplicateScan();
            if (rows == null || rows.Count == 0)
                return result;

            ScanUpload(module, rows, result);
            if (includeDatabase)
            {
                try
                {
                    using (SqlConnection connection = _database.GetConnection())
                    {
                        connection.Open();
                        EnsureArchiveSchema(connection);
                        ScanDatabase(module, rows, LoadExistingRows(connection, module), result);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("SmartImportDuplicateDetector.ScanDatabase." + module, ex);
                    result.Details.Add("Existing-record duplicate scan was unavailable; workbook duplicates were still checked.");
                }
            }

            return result;
        }

        public SmartImportDuplicateScan ScanUploadOnly(ExcelImportModule module, IList<Dictionary<string, string>> rows)
        {
            return Scan(module, rows, false);
        }

        public SmartImportDuplicateScan ScanExisting(ExcelImportModule module)
        {
            var result = new SmartImportDuplicateScan();
            using (SqlConnection connection = _database.GetConnection())
            {
                connection.Open();
                EnsureArchiveSchema(connection);
                ScanDatabase(module, new List<Dictionary<string, string>>(), LoadExistingRows(connection, module), result);
            }
            return result;
        }

        private static void EnsureArchiveSchema(SqlConnection connection)
        {
            connection.Execute(@"IF OBJECT_ID(N'dbo.DuplicateMergeArchive',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.DuplicateMergeArchive(
  ArchiveID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  ModuleName NVARCHAR(50) NOT NULL, TableName SYSNAME NOT NULL, PrimaryKeyName SYSNAME NOT NULL,
  SurvivorRecordID NVARCHAR(50) NOT NULL, DuplicateRecordID NVARCHAR(50) NOT NULL,
  MergedAt DATETIME2 NOT NULL CONSTRAINT DF_DuplicateMergeArchive_MergedAt DEFAULT SYSUTCDATETIME(),
  MergedBy NVARCHAR(150) NULL,
  CONSTRAINT UQ_DuplicateMergeArchive UNIQUE(ModuleName,DuplicateRecordID)
 );
END");
        }

        private static void ScanUpload(ExcelImportModule module, IList<Dictionary<string, string>> rows, SmartImportDuplicateScan result)
        {
            var firstRowsByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var duplicateRows = new HashSet<int>();
            for (int index = 0; index < rows.Count; index++)
            {
                foreach (string key in BuildKeys(module, rows[index], false))
                {
                    int firstRow;
                    if (firstRowsByKey.TryGetValue(key, out firstRow))
                    {
                        if (duplicateRows.Add(index + 2) && result.Details.Count < 20)
                            result.Details.Add("Upload row " + (index + 2) + " matches row " + firstRow + " by " + KeyLabel(key) + ".");
                    }
                    else
                    {
                        firstRowsByKey[key] = index + 2;
                    }
                }
            }
            result.UploadDuplicateRows = duplicateRows.Count;
        }

        private static void ScanDatabase(ExcelImportModule module, IList<Dictionary<string, string>> rows, IList<Dictionary<string, string>> existingRows, SmartImportDuplicateScan result)
        {
            var recordsByKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (Dictionary<string, string> existing in existingRows)
            {
                string recordId = Value(existing, "RecordID");
                foreach (string key in BuildKeys(module, existing, true))
                {
                    HashSet<string> ids;
                    if (!recordsByKey.TryGetValue(key, out ids))
                    {
                        ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        recordsByKey[key] = ids;
                    }
                    ids.Add(recordId);
                }
            }

            // One pair of duplicate records can share several identities (for example both
            // phone and name). Count that record set once so the operator sees a real group
            // count rather than an inflated count of matching fields.
            var duplicateGroups = recordsByKey
                .Where(pair => pair.Value.Count > 1)
                .GroupBy(pair => string.Join(",", pair.Value.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            result.ExistingDuplicateGroups = duplicateGroups.Count;

            foreach (KeyValuePair<string, HashSet<string>> group in duplicateGroups)
            {
                var item = new SmartImportDuplicateGroup { MatchReason = KeyLabel(group.Key) };
                foreach (string id in group.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    Dictionary<string, string> source = existingRows.FirstOrDefault(row => string.Equals(Value(row, "RecordID"), id, StringComparison.OrdinalIgnoreCase));
                    item.Records.Add(new SmartImportDuplicateRecord
                    {
                        RecordId = id,
                        DisplayName = source == null ? ("Record #" + id) : Value(source, "DisplayName")
                    });
                }
                result.Groups.Add(item);
            }

            for (int index = 0; index < rows.Count; index++)
            {
                string matchedKey = null;
                HashSet<string> matchedIds = null;
                foreach (string key in BuildKeys(module, rows[index], false))
                {
                    HashSet<string> ids;
                    if (recordsByKey.TryGetValue(key, out ids))
                    {
                        matchedKey = key;
                        matchedIds = ids;
                        if (ids.Count > 1)
                            break;
                    }
                }

                if (matchedIds == null)
                    continue;

                result.ExistingMatchRows++;
                if (matchedIds.Count > 1)
                    result.AmbiguousMatchRows++;
                if (result.Details.Count < 20)
                {
                    string action = matchedIds.Count > 1
                        ? "matches " + matchedIds.Count + " existing records and requires review"
                        : "will refresh the existing record";
                    result.Details.Add("Upload row " + (index + 2) + " " + action + " by " + KeyLabel(matchedKey) + ".");
                }
            }

            foreach (KeyValuePair<string, HashSet<string>> group in duplicateGroups.Take(Math.Max(0, 20 - result.Details.Count)))
                result.Details.Add("Current database has " + group.Value.Count + " records sharing " + KeyLabel(group.Key) + ".");
        }

        internal static IList<Dictionary<string, string>> LoadExistingRows(SqlConnection connection, ExcelImportModule module)
        {
            string sql;
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    sql = "SELECT CONVERT(varchar(30), BidID) RecordID, QuotationNumber, QuotationNumber DisplayName FROM Quotations q WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Quotations' AND a.DuplicateRecordID=CONVERT(varchar(30),q.BidID))";
                    break;
                case ExcelImportModule.Invoices:
                    sql = "SELECT CONVERT(varchar(30), InvoiceID) RecordID, InvoiceNumber, InvoiceNumber DisplayName FROM Invoices i WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Invoices' AND a.DuplicateRecordID=CONVERT(varchar(30),i.InvoiceID))";
                    break;
                case ExcelImportModule.Payments:
                    sql = @"SELECT CONVERT(varchar(30), p.PaymentID) RecordID, p.PaymentDate, i.InvoiceNumber,
                                   p.AmountPaid, p.ReferenceNumber
                            , ISNULL(NULLIF(p.ReferenceNumber,''),'Payment #'+CONVERT(varchar(30),p.PaymentID)) DisplayName
                            FROM Payments p LEFT JOIN Invoices i ON i.InvoiceID=p.InvoiceID
                            WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Payments' AND a.DuplicateRecordID=CONVERT(varchar(30),p.PaymentID))";
                    break;
                case ExcelImportModule.Purchases:
                    sql = @"SELECT CONVERT(varchar(30), po.POID) RecordID, po.PONumber, po.PODate PurchaseDate,
                                   v.VendorName SupplierName, li.Description ItemDescription, po.PONumber DisplayName
                            FROM PurchaseOrders po
                            LEFT JOIN Vendors v ON v.VendorID=po.VendorID
                            LEFT JOIN PurchaseLineItems li ON li.POID=po.POID
                            WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Purchases' AND a.DuplicateRecordID=CONVERT(varchar(30),po.POID))";
                    break;
                case ExcelImportModule.Jobs:
                    sql = @"SELECT CONVERT(varchar(30), j.JobID) RecordID, c.CompanyName ClientName,
                                   s.SiteName, j.ScheduledDate, j.Description, ISNULL(NULLIF(j.Description,''),'Job #'+CONVERT(varchar(30),j.JobID)) DisplayName
                            FROM Jobs j
                            LEFT JOIN B2BClients c ON c.ClientID=j.ClientID
                            LEFT JOIN ClientSites s ON s.SiteID=j.SiteID
                            WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Jobs' AND a.DuplicateRecordID=CONVERT(varchar(30),j.JobID))";
                    break;
                case ExcelImportModule.Clients:
                    sql = @"SELECT CONVERT(varchar(30), ClientID) RecordID, CompanyName ClientName,
                                   Phone, Email, GSTNumber GSTIN, CompanyName DisplayName FROM B2BClients WHERE ISNULL(IsActive,1)=1";
                    break;
                case ExcelImportModule.Employees:
                    sql = @"SELECT CONVERT(varchar(30), EmployeeID) RecordID, EmployeeCode, Name EmployeeName,
                                   Phone, AadhaarNumber Aadhaar, PANNumber PAN, Name DisplayName FROM Employees e
                            WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Employees' AND a.DuplicateRecordID=CONVERT(varchar(30),e.EmployeeID))";
                    break;
                case ExcelImportModule.Vendors:
                    sql = @"SELECT CONVERT(varchar(30), VendorID) RecordID, VendorName SupplierName,
                                   Phone, Email, GSTNumber GSTIN, VendorName DisplayName FROM Vendors WHERE ISNULL(IsArchived,0)=0";
                    break;
                case ExcelImportModule.Sites:
                    sql = @"SELECT CONVERT(varchar(30), s.SiteID) RecordID, s.SiteName, c.CompanyName ClientName, s.SiteName DisplayName
                            FROM ClientSites s INNER JOIN B2BClients c ON c.ClientID=s.ClientID
                            WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive a WHERE a.ModuleName='Sites' AND a.DuplicateRecordID=CONVERT(varchar(30),s.SiteID))";
                    break;
                case ExcelImportModule.Inventory:
                    sql = "SELECT CONVERT(varchar(30), ItemID) RecordID, ItemName, ItemName DisplayName FROM StockItems WHERE ISNULL(IsActive,1)=1";
                    break;
                case ExcelImportModule.SupplierItemPrices:
                    sql = @"SELECT CONVERT(varchar(30), p.PriceID) RecordID, i.ItemName,
                                   v.VendorName SupplierName, p.EffectiveDate, i.ItemName+' / '+v.VendorName DisplayName
                            FROM SupplierItemPrices p
                            INNER JOIN StockItems i ON i.ItemID=p.ItemID
                            INNER JOIN Vendors v ON v.VendorID=p.VendorID WHERE ISNULL(p.IsActive,1)=1";
                    break;
                case ExcelImportModule.AMC:
                    sql = "SELECT CONVERT(varchar(30), ContractID) RecordID, AMCNumber ContractNumber, AMCNumber DisplayName FROM AMCContracts a WHERE NOT EXISTS (SELECT 1 FROM DuplicateMergeArchive d WHERE d.ModuleName='AMC' AND d.DuplicateRecordID=CONVERT(varchar(30),a.ContractID))";
                    break;
                default:
                    return new List<Dictionary<string, string>>();
            }

            var result = new List<Dictionary<string, string>>();
            foreach (IDictionary<string, object> row in connection.Query(sql).Cast<IDictionary<string, object>>())
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, object> value in row)
                    values[value.Key] = ToText(value.Value);
                result.Add(values);
            }
            return result;
        }

        internal static IList<string> BuildKeys(ExcelImportModule module, IDictionary<string, string> row, bool databaseRow)
        {
            var keys = new List<string>();
            switch (module)
            {
                case ExcelImportModule.Quotations:
                    Add(keys, "quotation number", Value(row, "QuotationNumber"));
                    break;
                case ExcelImportModule.Invoices:
                    Add(keys, "invoice number", Value(row, "InvoiceNumber"));
                    break;
                case ExcelImportModule.Payments:
                    Add(keys, "payment reference", Value(row, "ReferenceNumber"));
                    AddComposite(keys, "invoice/date/amount", row, "InvoiceNumber", "PaymentDate", "AmountPaid");
                    break;
                case ExcelImportModule.Purchases:
                    AddComposite(keys, "PO/item", row, "PONumber", "ItemDescription");
                    AddComposite(keys, "supplier/date/item", row, "SupplierName", "PurchaseDate", "ItemDescription");
                    break;
                case ExcelImportModule.Jobs:
                    AddComposite(keys, "client/site/date/work", row, "ClientName", "SiteName", "ScheduledDate", "Description");
                    break;
                case ExcelImportModule.Clients:
                    Add(keys, "GSTIN", Value(row, "GSTIN"));
                    AddPhone(keys, Value(row, "Phone"));
                    AddEmail(keys, Value(row, "Email"));
                    Add(keys, "client name", Value(row, "ClientName"));
                    break;
                case ExcelImportModule.Employees:
                    Add(keys, "employee code", Value(row, "EmployeeCode"));
                    Add(keys, "Aadhaar", Value(row, "Aadhaar"));
                    Add(keys, "PAN", Value(row, "PAN"));
                    AddPhone(keys, Value(row, "Phone"));
                    Add(keys, "employee name", Value(row, "EmployeeName"));
                    break;
                case ExcelImportModule.Vendors:
                    Add(keys, "GSTIN", Value(row, "GSTIN"));
                    AddPhone(keys, Value(row, "Phone"));
                    AddEmail(keys, Value(row, "Email"));
                    Add(keys, "supplier name", Value(row, "SupplierName"));
                    break;
                case ExcelImportModule.Sites:
                    AddComposite(keys, "client/site", row, "ClientName", "SiteName");
                    break;
                case ExcelImportModule.Inventory:
                    Add(keys, "item name", Value(row, "ItemName"));
                    break;
                case ExcelImportModule.SupplierItemPrices:
                    AddComposite(keys, "item/supplier/date", row, "ItemName", "SupplierName", "EffectiveDate");
                    break;
                case ExcelImportModule.AMC:
                    Add(keys, "AMC number", Value(row, "ContractNumber"));
                    break;
            }
            return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void Add(List<string> keys, string label, string value)
        {
            string normalized = Normalize(value);
            if (!string.IsNullOrWhiteSpace(normalized))
                keys.Add(label + "|" + normalized);
        }

        private static void AddPhone(List<string> keys, string value)
        {
            string digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length > 10)
                digits = digits.Substring(digits.Length - 10);
            if (digits.Length >= 7)
                keys.Add("phone|" + digits);
        }

        private static void AddEmail(List<string> keys, string value)
        {
            string email = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (email.Contains("@"))
                keys.Add("email|" + email);
        }

        private static void AddComposite(List<string> keys, string label, IDictionary<string, string> row, params string[] fields)
        {
            string[] values = fields.Select(field => Normalize(Value(row, field))).ToArray();
            if (values.All(value => !string.IsNullOrWhiteSpace(value)))
                keys.Add(label + "|" + string.Join("~", values));
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var builder = new StringBuilder();
            foreach (char character in value.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }
            return builder.ToString();
        }

        private static string Value(IDictionary<string, string> row, string key)
        {
            string value;
            return row != null && row.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string KeyLabel(string key)
        {
            int separator = (key ?? string.Empty).IndexOf('|');
            return separator < 0 ? key : key.Substring(0, separator);
        }

        private static string ToText(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;
            DateTime date;
            if (value is DateTime)
            {
                date = (DateTime)value;
                return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }
            decimal number;
            if (value is decimal)
            {
                number = (decimal)value;
                return number.ToString("0.####", CultureInfo.InvariantCulture);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
