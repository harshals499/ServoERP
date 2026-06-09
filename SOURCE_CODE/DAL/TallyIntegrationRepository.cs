using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public sealed class TallyIntegrationRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public List<TallyLedgerMapping> GetLedgerMappings()
        {
            var list = new List<TallyLedgerMapping>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT MappingID, MappingType, ServoKey, TallyLedgerName, TallyMasterId, IsDefault, UpdatedAt
                    FROM TallyLedgerMappings
                    ORDER BY MappingType, IsDefault DESC, ServoKey, TallyLedgerName;", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(MapLedgerMapping(reader));
                }
            }
            return list;
        }

        public void SaveLedgerMapping(TallyLedgerMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingID = @id)
                    BEGIN
                        UPDATE TallyLedgerMappings
                        SET MappingType = @type,
                            ServoKey = @servoKey,
                            TallyLedgerName = @ledger,
                            TallyMasterId = @masterId,
                            IsDefault = @isDefault,
                            UpdatedAt = GETDATE()
                        WHERE MappingID = @id
                    END
                    ELSE
                    BEGIN
                        INSERT INTO TallyLedgerMappings
                            (MappingType, ServoKey, TallyLedgerName, TallyMasterId, IsDefault, UpdatedAt)
                        VALUES
                            (@type, @servoKey, @ledger, @masterId, @isDefault, GETDATE())
                    END", conn))
                {
                    AddMappingParameters(cmd, mapping);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string ResolveLedger(string mappingType, string servoKey, string fallback)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP 1 TallyLedgerName
                    FROM TallyLedgerMappings
                    WHERE MappingType = @type
                      AND (ServoKey = @servoKey OR IsDefault = 1 OR ISNULL(ServoKey, '') = '')
                    ORDER BY CASE WHEN ServoKey = @servoKey THEN 0 WHEN IsDefault = 1 THEN 1 ELSE 2 END, UpdatedAt DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@type", mappingType ?? string.Empty);
                    cmd.Parameters.AddWithValue("@servoKey", servoKey ?? string.Empty);
                    object value = cmd.ExecuteScalar();
                    string ledger = value == null || value == DBNull.Value ? string.Empty : value.ToString();
                    return string.IsNullOrWhiteSpace(ledger) ? fallback : ledger.Trim();
                }
            }
        }

        public List<TallyExportCandidate> GetInvoiceCandidates(DateTime fromDate, DateTime toDate, bool includeExported)
        {
            var list = new List<TallyExportCandidate>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT i.InvoiceID, i.InvoiceNumber, i.InvoiceDate, i.TotalAmount, i.PaymentStatus,
                           ISNULL(i.TallyExportStatus, 'Pending') AS TallyExportStatus,
                           c.CompanyName, c.GSTNumber, c.TallyLedgerName
                    FROM Invoices i
                    LEFT JOIN B2BClients c ON c.ClientID = i.ClientID
                    WHERE i.InvoiceDate >= @fromDate
                      AND i.InvoiceDate < @toDate
                      AND (@includeExported = 1 OR ISNULL(i.TallyExportStatus, '') <> 'Exported')
                    ORDER BY i.InvoiceDate DESC, i.InvoiceID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Date.AddDays(1));
                    cmd.Parameters.AddWithValue("@includeExported", includeExported);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(MapCandidate(reader, "Invoice", "InvoiceID", "InvoiceNumber", "InvoiceDate", "TotalAmount", "CompanyName", "TallyLedgerName", BuildInvoiceMissingFields(reader)));
                    }
                }
            }
            return list;
        }

        public List<TallyExportCandidate> GetPaymentCandidates(DateTime fromDate, DateTime toDate, bool includeExported)
        {
            var list = new List<TallyExportCandidate>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.PaymentID, p.PaymentNumber, p.PaymentDate, p.AmountPaid, p.PaymentMode,
                           ISNULL(p.TallyExportStatus, 'Pending') AS TallyExportStatus,
                           c.CompanyName, c.TallyLedgerName, i.InvoiceNumber
                    FROM Payments p
                    INNER JOIN B2BClients c ON c.ClientID = p.ClientID
                    INNER JOIN Invoices i ON i.InvoiceID = p.InvoiceID
                    WHERE p.PaymentDate >= @fromDate
                      AND p.PaymentDate < @toDate
                      AND (@includeExported = 1 OR ISNULL(p.TallyExportStatus, '') <> 'Exported')
                    ORDER BY p.PaymentDate DESC, p.PaymentID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Date.AddDays(1));
                    cmd.Parameters.AddWithValue("@includeExported", includeExported);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(MapCandidate(reader, "Payment", "PaymentID", "PaymentNumber", "PaymentDate", "AmountPaid", "CompanyName", "TallyLedgerName", BuildPaymentMissingFields(reader)));
                    }
                }
            }
            return list;
        }

        public List<TallyExportCandidate> GetPurchaseCandidates(DateTime fromDate, DateTime toDate, bool includeExported)
        {
            var list = new List<TallyExportCandidate>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT p.POID, p.PONumber, p.PODate, p.TotalAmount, p.Status,
                           ISNULL(p.TallyExportStatus, 'Pending') AS TallyExportStatus,
                           v.VendorName, v.GSTNumber, v.TallyLedgerName
                    FROM PurchaseOrders p
                    INNER JOIN Vendors v ON v.VendorID = p.VendorID
                    WHERE p.PODate >= @fromDate
                      AND p.PODate < @toDate
                      AND (@includeExported = 1 OR ISNULL(p.TallyExportStatus, '') <> 'Exported')
                    ORDER BY p.PODate DESC, p.POID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Date.AddDays(1));
                    cmd.Parameters.AddWithValue("@includeExported", includeExported);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(MapCandidate(reader, "PurchaseOrder", "POID", "PONumber", "PODate", "TotalAmount", "VendorName", "TallyLedgerName", BuildPurchaseMissingFields(reader)));
                    }
                }
            }
            return list;
        }

        public DataTable GetStockItemsForSync()
        {
            using (SqlConnection conn = _db.GetConnection())
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT ItemID, ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel,
                       HSNCode, TallyItemName, TallyUnitName, TallyStockGroupName, TallyGodownName,
                       ISNULL(TallySyncStatus, 'NotMapped') AS TallySyncStatus, TallyLastSyncedAt, TallySyncError
                FROM StockItems
                WHERE ISNULL(IsActive, 1) = 1
                ORDER BY ISNULL(TallySyncStatus, 'NotMapped'), ItemName;", conn))
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        public List<TallySyncLogEntry> GetRecentLogs(int maxRows)
        {
            var logs = new List<TallySyncLogEntry>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP (@maxRows) SyncLogID, Direction, EntityType, EntityID, Operation, Status, Message, LocalXmlPath, RawResponse, CreatedAt
                    FROM TallySyncLog
                    ORDER BY CreatedAt DESC, SyncLogID DESC;", conn))
                {
                    cmd.Parameters.AddWithValue("@maxRows", Math.Max(1, maxRows));
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            logs.Add(MapLog(reader));
                    }
                }
            }
            return logs;
        }

        public int StartImportBatch(string importType, string sourceMode)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO TallyImportBatches
                        (ImportType, SourceMode, StartedAt, CreatedCount, UpdatedCount, SkippedCount, ErrorCount)
                    VALUES
                        (@type, @source, GETDATE(), 0, 0, 0, 0);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@type", importType ?? string.Empty);
                    cmd.Parameters.AddWithValue("@source", sourceMode ?? string.Empty);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void CompleteImportBatch(int batchId, TallyImportSummary summary)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE TallyImportBatches
                    SET CompletedAt = GETDATE(),
                        CreatedCount = @created,
                        UpdatedCount = @updated,
                        SkippedCount = @skipped,
                        ErrorCount = @errors
                    WHERE ImportBatchID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", batchId);
                    cmd.Parameters.AddWithValue("@created", summary?.CreatedCount ?? 0);
                    cmd.Parameters.AddWithValue("@updated", summary?.UpdatedCount ?? 0);
                    cmd.Parameters.AddWithValue("@skipped", summary?.SkippedCount ?? 0);
                    cmd.Parameters.AddWithValue("@errors", summary?.ErrorCount ?? 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ApplyMasterRecord(TallyMasterRecord record, TallyImportSummary summary)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Name))
                return;

            if (string.Equals(record.MasterType, "Ledger", StringComparison.OrdinalIgnoreCase))
                ApplyLedgerMaster(record, summary);
            else if (string.Equals(record.MasterType, "StockItem", StringComparison.OrdinalIgnoreCase))
                ApplyStockItemMaster(record, summary);
            else
                summary.SkippedCount++;
        }

        public void MarkEntityExportResult(string entityType, int entityId, bool success, string message, string rawResponse, string localXmlPath)
        {
            string status = success ? "Exported" : "Failed";
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    UpdateEntityStatus(conn, tx, entityType, entityId, status, message);
                    InsertLog(conn, tx, "Push", entityType, entityId, "Export", success ? "Success" : "Failed", message, localXmlPath, rawResponse);
                    tx.Commit();
                }
            }
        }

        public void MarkStockSyncResult(int itemId, bool success, string message, string rawResponse, string localXmlPath)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE StockItems
                        SET TallySyncStatus = @status,
                            TallyLastSyncedAt = CASE WHEN @success = 1 THEN GETDATE() ELSE TallyLastSyncedAt END,
                            TallySyncError = CASE WHEN @success = 1 THEN NULL ELSE @message END
                        WHERE ItemID = @id;", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", itemId);
                        cmd.Parameters.AddWithValue("@success", success);
                        cmd.Parameters.AddWithValue("@status", success ? "Exported" : "Failed");
                        cmd.Parameters.AddWithValue("@message", string.IsNullOrWhiteSpace(message) ? (object)DBNull.Value : message);
                        cmd.ExecuteNonQuery();
                    }
                    InsertLog(conn, tx, "Sync", "StockItem", itemId, "Export", success ? "Success" : "Failed", message, localXmlPath, rawResponse);
                    tx.Commit();
                }
            }
        }

        public void Log(string direction, string entityType, int? entityId, string operation, string status, string message, string localXmlPath, string rawResponse)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                InsertLog(conn, null, direction, entityType, entityId, operation, status, message, localXmlPath, rawResponse);
            }
        }

        public int AutoMapClients()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE B2BClients
                    SET TallyLedgerName = CompanyName,
                        TallySyncStatus = 'Mapped',
                        TallyLastSyncedAt = GETDATE(),
                        TallySyncError = NULL
                    WHERE ISNULL(TallyLedgerName, '') = ''
                      AND ISNULL(CompanyName, '') <> '';", conn))
                    return cmd.ExecuteNonQuery();
            }
        }

        public int AutoMapVendors()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE Vendors
                    SET TallyLedgerName = VendorName,
                        TallySyncStatus = 'Mapped',
                        TallyLastSyncedAt = GETDATE(),
                        TallySyncError = NULL
                    WHERE ISNULL(TallyLedgerName, '') = ''
                      AND ISNULL(VendorName, '') <> '';", conn))
                    return cmd.ExecuteNonQuery();
            }
        }

        public int AutoMapStockItems()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE StockItems
                    SET TallyItemName = ItemName,
                        TallyUnitName = Unit,
                        TallyStockGroupName = ISNULL(NULLIF(Category, ''), 'Primary'),
                        TallyGodownName = ISNULL(NULLIF(TallyGodownName, ''), 'Main Location'),
                        TallySyncStatus = 'Mapped',
                        TallyLastSyncedAt = GETDATE(),
                        TallySyncError = NULL
                    WHERE ISNULL(TallyItemName, '') = ''
                      AND ISNULL(ItemName, '') <> '';", conn))
                    return cmd.ExecuteNonQuery();
            }
        }

        public DataRow GetStockItemRow(int itemId)
        {
            using (SqlConnection conn = _db.GetConnection())
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP 1 ItemID, ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel,
                       HSNCode, TallyItemName, TallyUnitName, TallyStockGroupName, TallyGodownName
                FROM StockItems
                WHERE ItemID = @id;", conn))
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@id", itemId);
                var table = new DataTable();
                adapter.Fill(table);
                return table.Rows.Count == 0 ? null : table.Rows[0];
            }
        }

        private void ApplyLedgerMaster(TallyMasterRecord record, TallyImportSummary summary)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                int updated = 0;
                using (SqlCommand client = new SqlCommand(@"
                    UPDATE B2BClients
                    SET TallyLedgerName = @name,
                        TallyGuid = @guid,
                        TallyMasterId = @masterId,
                        TallySyncStatus = 'Mapped',
                        TallyLastSyncedAt = GETDATE(),
                        TallySyncError = NULL
                    WHERE CompanyName = @name OR (ISNULL(GSTNumber, '') <> '' AND GSTNumber = @gstin);", conn))
                {
                    AddMasterParameters(client, record);
                    updated += client.ExecuteNonQuery();
                }
                using (SqlCommand vendor = new SqlCommand(@"
                    UPDATE Vendors
                    SET TallyLedgerName = @name,
                        TallyGuid = @guid,
                        TallyMasterId = @masterId,
                        TallySyncStatus = 'Mapped',
                        TallyLastSyncedAt = GETDATE(),
                        TallySyncError = NULL
                    WHERE VendorName = @name OR (ISNULL(GSTNumber, '') <> '' AND GSTNumber = @gstin);", conn))
                {
                    AddMasterParameters(vendor, record);
                    updated += vendor.ExecuteNonQuery();
                }
                if (updated > 0)
                    summary.UpdatedCount += updated;
                else
                    summary.SkippedCount++;
            }
        }

        private void ApplyStockItemMaster(TallyMasterRecord record, TallyImportSummary summary)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand update = new SqlCommand(@"
                    UPDATE StockItems
                    SET TallyItemName = @name,
                        TallyGuid = @guid,
                        TallyMasterId = @masterId,
                        TallyUnitName = @unit,
                        TallyStockGroupName = @parent,
                        HSNCode = CASE WHEN ISNULL(HSNCode, '') = '' THEN @hsn ELSE HSNCode END,
                        TallySyncStatus = 'Mapped',
                        TallyLastSyncedAt = GETDATE(),
                        TallySyncError = NULL
                    WHERE ItemName = @name OR TallyItemName = @name;", conn))
                {
                    AddMasterParameters(update, record);
                    int rows = update.ExecuteNonQuery();
                    if (rows > 0)
                    {
                        summary.UpdatedCount += rows;
                        return;
                    }
                }
                using (SqlCommand insert = new SqlCommand(@"
                    INSERT INTO StockItems
                        (ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel,
                         HSNCode, TallyItemName, TallyGuid, TallyMasterId, TallyUnitName, TallyStockGroupName, TallySyncStatus, TallyLastSyncedAt)
                    VALUES
                        (@name, @parent, @qty, @unit, @rate, 5,
                         @hsn, @name, @guid, @masterId, @unit, @parent, 'Imported', GETDATE());", conn))
                {
                    AddMasterParameters(insert, record);
                    insert.Parameters.AddWithValue("@qty", record.ClosingQuantity);
                    insert.Parameters.AddWithValue("@rate", record.Rate);
                    insert.ExecuteNonQuery();
                    summary.CreatedCount++;
                }
            }
        }

        private static void UpdateEntityStatus(SqlConnection conn, SqlTransaction tx, string entityType, int entityId, string status, string message)
        {
            string sql;
            if (string.Equals(entityType, "Invoice", StringComparison.OrdinalIgnoreCase))
                sql = "UPDATE Invoices SET TallyExportStatus = @status, TallyExportedAt = CASE WHEN @status = 'Exported' THEN GETDATE() ELSE TallyExportedAt END, TallyExportError = CASE WHEN @status = 'Exported' THEN NULL ELSE @message END WHERE InvoiceID = @id;";
            else if (string.Equals(entityType, "Payment", StringComparison.OrdinalIgnoreCase))
                sql = "UPDATE Payments SET TallyExportStatus = @status, TallyExportedAt = CASE WHEN @status = 'Exported' THEN GETDATE() ELSE TallyExportedAt END, TallyExportError = CASE WHEN @status = 'Exported' THEN NULL ELSE @message END WHERE PaymentID = @id;";
            else if (string.Equals(entityType, "PurchaseOrder", StringComparison.OrdinalIgnoreCase))
                sql = "UPDATE PurchaseOrders SET TallyExportStatus = @status, TallyExportedAt = CASE WHEN @status = 'Exported' THEN GETDATE() ELSE TallyExportedAt END, TallyExportError = CASE WHEN @status = 'Exported' THEN NULL ELSE @message END WHERE POID = @id;";
            else
                return;

            using (SqlCommand cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", entityId);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@message", string.IsNullOrWhiteSpace(message) ? (object)DBNull.Value : message);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertLog(SqlConnection conn, SqlTransaction tx, string direction, string entityType, int? entityId, string operation, string status, string message, string localXmlPath, string rawResponse)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO TallySyncLog
                    (Direction, EntityType, EntityID, Operation, Status, Message, LocalXmlPath, RawResponse, CreatedAt)
                VALUES
                    (@direction, @entityType, @entityId, @operation, @status, @message, @path, @raw, GETDATE());", conn, tx))
            {
                cmd.Parameters.AddWithValue("@direction", direction ?? string.Empty);
                cmd.Parameters.AddWithValue("@entityType", entityType ?? string.Empty);
                cmd.Parameters.AddWithValue("@entityId", entityId.HasValue ? (object)entityId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@operation", operation ?? string.Empty);
                cmd.Parameters.AddWithValue("@status", status ?? string.Empty);
                cmd.Parameters.AddWithValue("@message", string.IsNullOrWhiteSpace(message) ? (object)DBNull.Value : message);
                cmd.Parameters.AddWithValue("@path", string.IsNullOrWhiteSpace(localXmlPath) ? (object)DBNull.Value : localXmlPath);
                cmd.Parameters.AddWithValue("@raw", string.IsNullOrWhiteSpace(rawResponse) ? (object)DBNull.Value : rawResponse);
                cmd.ExecuteNonQuery();
            }
        }

        private static TallyLedgerMapping MapLedgerMapping(SqlDataReader reader)
        {
            return new TallyLedgerMapping
            {
                MappingID = Convert.ToInt32(reader["MappingID"]),
                MappingType = reader["MappingType"].ToString(),
                ServoKey = reader["ServoKey"] == DBNull.Value ? string.Empty : reader["ServoKey"].ToString(),
                TallyLedgerName = reader["TallyLedgerName"].ToString(),
                TallyMasterId = reader["TallyMasterId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["TallyMasterId"]),
                IsDefault = Convert.ToBoolean(reader["IsDefault"]),
                UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"])
            };
        }

        private static TallyExportCandidate MapCandidate(SqlDataReader reader, string entityType, string idColumn, string numberColumn, string dateColumn, string amountColumn, string partyColumn, string tallyPartyColumn, string missingFields)
        {
            return new TallyExportCandidate
            {
                EntityID = Convert.ToInt32(reader[idColumn]),
                EntityType = entityType,
                Number = reader[numberColumn] == DBNull.Value ? string.Empty : reader[numberColumn].ToString(),
                PartyName = reader[partyColumn] == DBNull.Value ? string.Empty : reader[partyColumn].ToString(),
                TallyPartyName = reader[tallyPartyColumn] == DBNull.Value ? string.Empty : reader[tallyPartyColumn].ToString(),
                VoucherDate = reader[dateColumn] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(reader[dateColumn]),
                Amount = reader[amountColumn] == DBNull.Value ? 0m : Convert.ToDecimal(reader[amountColumn]),
                Status = HasColumn(reader, "PaymentStatus") ? reader["PaymentStatus"].ToString() : HasColumn(reader, "Status") ? reader["Status"].ToString() : string.Empty,
                TallyExportStatus = reader["TallyExportStatus"].ToString(),
                MissingFields = missingFields
            };
        }

        private static TallySyncLogEntry MapLog(SqlDataReader reader)
        {
            return new TallySyncLogEntry
            {
                SyncLogID = Convert.ToInt32(reader["SyncLogID"]),
                Direction = reader["Direction"].ToString(),
                EntityType = reader["EntityType"].ToString(),
                EntityID = reader["EntityID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["EntityID"]),
                Operation = reader["Operation"].ToString(),
                Status = reader["Status"].ToString(),
                Message = reader["Message"] == DBNull.Value ? string.Empty : reader["Message"].ToString(),
                LocalXmlPath = reader["LocalXmlPath"] == DBNull.Value ? string.Empty : reader["LocalXmlPath"].ToString(),
                RawResponse = reader["RawResponse"] == DBNull.Value ? string.Empty : reader["RawResponse"].ToString(),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
            };
        }

        private static bool HasColumn(IDataRecord reader, string column)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (string.Equals(reader.GetName(i), column, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string BuildInvoiceMissingFields(SqlDataReader reader)
        {
            var missing = new List<string>();
            if (reader["TallyLedgerName"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["TallyLedgerName"].ToString()))
                missing.Add("client Tally ledger");
            if (reader["InvoiceNumber"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["InvoiceNumber"].ToString()))
                missing.Add("invoice number");
            return string.Join(", ", missing);
        }

        private static string BuildPaymentMissingFields(SqlDataReader reader)
        {
            var missing = new List<string>();
            if (reader["TallyLedgerName"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["TallyLedgerName"].ToString()))
                missing.Add("client Tally ledger");
            if (reader["PaymentMode"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["PaymentMode"].ToString()))
                missing.Add("payment mode");
            return string.Join(", ", missing);
        }

        private static string BuildPurchaseMissingFields(SqlDataReader reader)
        {
            var missing = new List<string>();
            if (reader["TallyLedgerName"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["TallyLedgerName"].ToString()))
                missing.Add("vendor Tally ledger");
            if (reader["PONumber"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["PONumber"].ToString()))
                missing.Add("purchase number");
            return string.Join(", ", missing);
        }

        private static void AddMappingParameters(SqlCommand cmd, TallyLedgerMapping mapping)
        {
            cmd.Parameters.AddWithValue("@id", mapping.MappingID);
            cmd.Parameters.AddWithValue("@type", mapping.MappingType ?? string.Empty);
            cmd.Parameters.AddWithValue("@servoKey", string.IsNullOrWhiteSpace(mapping.ServoKey) ? (object)DBNull.Value : mapping.ServoKey.Trim());
            cmd.Parameters.AddWithValue("@ledger", mapping.TallyLedgerName ?? string.Empty);
            cmd.Parameters.AddWithValue("@masterId", mapping.TallyMasterId.HasValue ? (object)mapping.TallyMasterId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@isDefault", mapping.IsDefault);
        }

        private static void AddMasterParameters(SqlCommand cmd, TallyMasterRecord record)
        {
            cmd.Parameters.AddWithValue("@name", record.Name ?? string.Empty);
            cmd.Parameters.AddWithValue("@parent", string.IsNullOrWhiteSpace(record.Parent) ? (object)DBNull.Value : record.Parent);
            cmd.Parameters.AddWithValue("@guid", string.IsNullOrWhiteSpace(record.Guid) ? (object)DBNull.Value : record.Guid);
            cmd.Parameters.AddWithValue("@masterId", record.MasterId.HasValue ? (object)record.MasterId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@gstin", string.IsNullOrWhiteSpace(record.Gstin) ? (object)DBNull.Value : record.Gstin);
            cmd.Parameters.AddWithValue("@unit", string.IsNullOrWhiteSpace(record.Unit) ? (object)DBNull.Value : record.Unit);
            cmd.Parameters.AddWithValue("@hsn", string.IsNullOrWhiteSpace(record.HsnCode) ? (object)DBNull.Value : record.HsnCode);
        }
    }
}
