using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class InventoryRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        private const string BaseQuery = @"
            SELECT s.*, v.VendorName FROM StockItems s
            LEFT JOIN Vendors v ON s.VendorID = v.VendorID AND ISNULL(v.IsSupplier, 1) = 1
            WHERE ISNULL(s.IsActive, 1) = 1";

        public List<StockItem> GetAll()
        {
            var list = new List<StockItem>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " ORDER BY ISNULL(s.Category, ''), s.ItemName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public StockItem GetById(int id)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " AND s.ItemID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? Map(r) : null;
                }
            }
        }

        public List<StockItem> GetLowStock()
        {
            var list = new List<StockItem>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " AND s.CurrentStock <= s.ReorderLevel ORDER BY s.ItemName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(Map(r));
            }
            return list;
        }

        public int Create(StockItem item)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO StockItems (ItemName,Category,CurrentStock,Unit,LastPurchaseRate,ReorderLevel,VendorID)
                    VALUES (@n,@cat,@qty,@unit,@rate,@rl,@vid);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@n",    item.ItemName);
                    cmd.Parameters.AddWithValue("@cat",  (object)item.Category        ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@qty",  item.CurrentStock);
                    cmd.Parameters.AddWithValue("@unit", item.Unit ?? "Nos");
                    cmd.Parameters.AddWithValue("@rate", item.LastPurchaseRate);
                    cmd.Parameters.AddWithValue("@rl",   item.ReorderLevel);
                    cmd.Parameters.AddWithValue("@vid",  (object)item.VendorID         ?? DBNull.Value);
                    return (int)(decimal)cmd.ExecuteScalar();
                }
            }
        }

        public void Update(StockItem item)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE StockItems SET ItemName=@n,Category=@cat,CurrentStock=@qty,Unit=@unit,
                    LastPurchaseRate=@rate,ReorderLevel=@rl,VendorID=@vid,LastUpdated=GETDATE()
                    WHERE ItemID=@id AND ISNULL(IsActive, 1) = 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id",   item.ItemID);
                    cmd.Parameters.AddWithValue("@n",    item.ItemName);
                    cmd.Parameters.AddWithValue("@cat",  (object)item.Category        ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@qty",  item.CurrentStock);
                    cmd.Parameters.AddWithValue("@unit", item.Unit ?? "Nos");
                    cmd.Parameters.AddWithValue("@rate", item.LastPurchaseRate);
                    cmd.Parameters.AddWithValue("@rl",   item.ReorderLevel);
                    cmd.Parameters.AddWithValue("@vid",  (object)item.VendorID         ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Delete(int itemId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE StockItems SET IsActive = 0, LastUpdated = GETDATE() WHERE ItemID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", itemId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public decimal GetTotalStockValue()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT ISNULL(SUM(CurrentStock * LastPurchaseRate),0) FROM StockItems WHERE ISNULL(IsActive, 1) = 1", conn))
                    return (decimal)cmd.ExecuteScalar();
            }
        }

        public int GetLowStockCount()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM StockItems WHERE ISNULL(IsActive, 1) = 1 AND CurrentStock <= ReorderLevel", conn))
                    return (int)cmd.ExecuteScalar();
            }
        }

        public void AddStock(int itemId, decimal qty)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE StockItems SET CurrentStock = CurrentStock + @qty, LastUpdated = GETDATE() WHERE ItemID = @id AND ISNULL(IsActive, 1) = 1", conn))
                {
                    cmd.Parameters.AddWithValue("@qty", qty);
                    cmd.Parameters.AddWithValue("@id",  itemId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public int RecordMovement(StockMovement movement)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        EnsureStockItemsSchema(conn, tx);
                        StockItem item = GetByIdForUpdate(conn, tx, movement.ItemID);
                        if (item == null)
                            throw new InvalidOperationException("Inventory item not found.");

                        decimal signedQuantity = GetSignedQuantity(movement.MovementType, movement.Quantity);
                        decimal stockAfter = item.CurrentStock + signedQuantity;
                        if (stockAfter < 0)
                            throw new InvalidOperationException("Stock cannot go negative for " + item.ItemName + ". Available: " + item.CurrentStock.ToString("0.###") + ", requested: " + movement.Quantity.ToString("0.###") + ".");

                        using (SqlCommand update = new SqlCommand(
                            "UPDATE StockItems SET CurrentStock = @stock, LastUpdated = GETDATE() WHERE ItemID = @id", conn, tx))
                        {
                            update.Parameters.AddWithValue("@stock", stockAfter);
                            update.Parameters.AddWithValue("@id", movement.ItemID);
                            update.ExecuteNonQuery();
                        }

                        using (SqlCommand insert = new SqlCommand(@"
                            INSERT INTO StockMovements
                                (ItemID, MovementType, Quantity, StockBefore, StockAfter, FromLocation, ToLocation, ReferenceNo, Notes, CreatedByUserId, CreatedByName)
                            VALUES
                                (@itemId, @type, @qty, @before, @after, @from, @to, @ref, @notes, @userId, @userName);
                            SELECT SCOPE_IDENTITY();", conn, tx))
                        {
                            insert.Parameters.AddWithValue("@itemId", movement.ItemID);
                            insert.Parameters.AddWithValue("@type", movement.MovementType ?? "Adjustment");
                            insert.Parameters.AddWithValue("@qty", movement.Quantity);
                            insert.Parameters.AddWithValue("@before", item.CurrentStock);
                            insert.Parameters.AddWithValue("@after", stockAfter);
                            insert.Parameters.AddWithValue("@from", (object)movement.FromLocation ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@to", (object)movement.ToLocation ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@ref", (object)movement.ReferenceNo ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@notes", (object)movement.Notes ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@userId", (object)movement.CreatedByUserId ?? DBNull.Value);
                            insert.Parameters.AddWithValue("@userName", (object)movement.CreatedByName ?? DBNull.Value);
                            int id = (int)(decimal)insert.ExecuteScalar();
                            tx.Commit();
                            return id;
                        }
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<StockMovement> GetMovements(int itemId, int maxRows = 100)
        {
            var list = new List<StockMovement>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP (@max) m.*, s.ItemName
                    FROM StockMovements m
                    INNER JOIN StockItems s ON s.ItemID = m.ItemID
                    WHERE (@itemId <= 0 OR m.ItemID = @itemId)
                    ORDER BY m.CreatedDate DESC, m.MovementID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@max", Math.Max(1, maxRows));
                    cmd.Parameters.AddWithValue("@itemId", itemId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(MapMovement(r));
                }
            }
            return list;
        }

        public StockItem GetByName(string name)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                EnsureStockItemsSchema(conn, null);
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " AND s.ItemName LIKE @n ORDER BY s.ItemName", conn))
                {
                    cmd.Parameters.AddWithValue("@n", "%" + name + "%");
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? Map(r) : null;
                }
            }
        }

        public List<InventoryDuplicateGroup> FindDuplicateItems()
        {
            List<StockItem> items = GetAll();
            return items
                .GroupBy(item => NormalizeDuplicateKey(item.ItemName))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => new InventoryDuplicateGroup
                {
                    DuplicateKey = group.Key,
                    Items = group.OrderByDescending(ScoreDuplicateMaster).ThenBy(item => item.ItemID).ToList()
                })
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.DuplicateKey)
                .ToList();
        }

        public InventoryDuplicateCleanupResult MergeDuplicateItems()
        {
            var result = new InventoryDuplicateCleanupResult();
            List<InventoryDuplicateGroup> groups = FindDuplicateItems();
            result.GroupsDetected = groups.Count;

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (InventoryDuplicateGroup group in groups)
                        {
                            if (group.Items == null || group.Items.Count < 2)
                                continue;

                            StockItem master = group.Items[0];
                            List<StockItem> duplicates = group.Items.Skip(1).ToList();
                            foreach (StockItem duplicate in duplicates)
                            {
                                result.ReferencesMoved += MoveItemReferences(conn, tx, duplicate.ItemID, master.ItemID);
                                MergeStockValues(conn, tx, master.ItemID, duplicate);
                                ArchiveDuplicate(conn, tx, duplicate.ItemID);
                                result.ItemsArchived++;
                            }

                            result.Messages.Add(group.DuplicateKey + ": kept #" + master.ItemID + ", archived " + duplicates.Count + ".");
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }

            return result;
        }

        private static int MoveItemReferences(SqlConnection conn, SqlTransaction tx, int duplicateItemId, int masterItemId)
        {
            int moved = 0;
            moved += ExecuteReferenceMove(conn, tx, "StockMovements", "ItemID", duplicateItemId, masterItemId);
            moved += ExecuteReferenceMove(conn, tx, "InvoiceInventoryReservations", "StockItemID", duplicateItemId, masterItemId);
            moved += ExecuteReferenceMove(conn, tx, "InventoryUsageLog", "StockItemID", duplicateItemId, masterItemId);
            moved += ExecuteReferenceMove(conn, tx, "PurchaseLineItems", "InventoryItemId", duplicateItemId, masterItemId);
            moved += ExecuteReferenceMove(conn, tx, "JobPartsUsed", "InventoryItemId", duplicateItemId, masterItemId);
            moved += ExecuteReferenceMove(conn, tx, "QuotationLineItems", "InventoryItemId", duplicateItemId, masterItemId);
            return moved;
        }

        /// <summary>Repairs old client inventory schemas before queries that depend on active/archive columns.</summary>
        private static void EnsureStockItemsSchema(SqlConnection conn, SqlTransaction tx)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                IF OBJECT_ID('dbo.StockItems', 'U') IS NOT NULL AND COL_LENGTH('dbo.StockItems', 'IsActive') IS NULL
                BEGIN
                    ALTER TABLE dbo.StockItems ADD IsActive BIT NOT NULL DEFAULT(1) WITH VALUES;
                END

                IF OBJECT_ID('dbo.StockItems', 'U') IS NOT NULL AND COL_LENGTH('dbo.StockItems', 'LastUpdated') IS NULL
                BEGIN
                    ALTER TABLE dbo.StockItems ADD LastUpdated DATETIME NOT NULL DEFAULT(GETDATE()) WITH VALUES;
                END

                IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL AND COL_LENGTH('dbo.Vendors', 'IsSupplier') IS NULL
                BEGIN
                    ALTER TABLE dbo.Vendors ADD IsSupplier BIT NOT NULL DEFAULT(1) WITH VALUES;
                END

                IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL AND COL_LENGTH('dbo.Vendors', 'IsServiceVendor') IS NULL
                BEGIN
                    ALTER TABLE dbo.Vendors ADD IsServiceVendor BIT NOT NULL DEFAULT(0) WITH VALUES;
                END", conn, tx))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static int ExecuteReferenceMove(SqlConnection conn, SqlTransaction tx, string tableName, string columnName, int duplicateItemId, int masterItemId)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                IF OBJECT_ID(@tableName, 'U') IS NOT NULL
                   AND COL_LENGTH(@tableName, @columnName) IS NOT NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'UPDATE ' + QUOTENAME(@tableName) +
                        N' SET ' + QUOTENAME(@columnName) + N' = @masterId WHERE ' + QUOTENAME(@columnName) + N' = @duplicateId;';
                    EXEC sp_executesql @sql, N'@masterId INT, @duplicateId INT', @masterId, @duplicateId;
                END", conn, tx))
            {
                cmd.Parameters.AddWithValue("@tableName", tableName);
                cmd.Parameters.AddWithValue("@columnName", columnName);
                cmd.Parameters.AddWithValue("@masterId", masterItemId);
                cmd.Parameters.AddWithValue("@duplicateId", duplicateItemId);
                return cmd.ExecuteNonQuery();
            }
        }

        private static void MergeStockValues(SqlConnection conn, SqlTransaction tx, int masterItemId, StockItem duplicate)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE StockItems
                SET CurrentStock = ISNULL(CurrentStock, 0) + @stock,
                    ReservedStock = ISNULL(ReservedStock, 0) + @reserved,
                    LastPurchaseRate = CASE WHEN @rate > 0 THEN @rate ELSE LastPurchaseRate END,
                    ReorderLevel = CASE WHEN @reorder > ReorderLevel THEN @reorder ELSE ReorderLevel END,
                    LastUpdated = GETDATE()
                WHERE ItemID = @masterId;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@masterId", masterItemId);
                cmd.Parameters.AddWithValue("@stock", duplicate.CurrentStock);
                cmd.Parameters.AddWithValue("@reserved", duplicate.ReservedStock);
                cmd.Parameters.AddWithValue("@rate", duplicate.LastPurchaseRate);
                cmd.Parameters.AddWithValue("@reorder", duplicate.ReorderLevel);
                cmd.ExecuteNonQuery();
            }
        }

        private static void ArchiveDuplicate(SqlConnection conn, SqlTransaction tx, int duplicateItemId)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE StockItems
                SET IsActive = 0,
                    LastUpdated = GETDATE()
                WHERE ItemID = @duplicateId;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@duplicateId", duplicateItemId);
                cmd.ExecuteNonQuery();
            }
        }

        private static int ScoreDuplicateMaster(StockItem item)
        {
            int score = 0;
            if (item == null)
                return score;
            if (item.CurrentStock > 0) score += 20;
            if (item.LastPurchaseRate > 0) score += 10;
            if (item.VendorID.HasValue) score += 5;
            if (!string.IsNullOrWhiteSpace(item.Category)) score += 2;
            if (!string.IsNullOrWhiteSpace(item.Unit)) score += 1;
            return score;
        }

        private static string NormalizeDuplicateKey(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return string.Empty;

            return string.Join(" ", itemName.Trim().ToUpperInvariant().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static StockItem Map(SqlDataReader r) => new StockItem
        {
            ItemID           = ReadInt32(r, "ItemID"),
            ItemName         = ReadString(r, "ItemName"),
            Category         = ReadString(r, "Category"),
            CurrentStock     = ReadDecimal(r, "CurrentStock"),
            Unit             = ReadString(r, "Unit"),
            LastPurchaseRate = ReadDecimal(r, "LastPurchaseRate"),
            ReorderLevel     = ReadDecimal(r, "ReorderLevel"),
            ReservedStock    = ReadDecimal(r, "ReservedStock"),
            VendorID         = ReadNullableInt32(r, "VendorID"),
            VendorName       = ReadString(r, "VendorName"),
            LastUpdated      = ReadDateTime(r, "LastUpdated"),
            IsActive         = !HasColumn(r, "IsActive") || r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"]),
        };

        /// <summary>Reads a string column safely from mixed imported stock schemas.</summary>
        private static string ReadString(SqlDataReader reader, string columnName)
        {
            return !HasColumn(reader, columnName) || reader[columnName] == DBNull.Value ? null : Convert.ToString(reader[columnName]);
        }

        /// <summary>Reads an integer column safely from mixed imported stock schemas.</summary>
        private static int ReadInt32(SqlDataReader reader, string columnName)
        {
            return !HasColumn(reader, columnName) || reader[columnName] == DBNull.Value ? 0 : Convert.ToInt32(reader[columnName]);
        }

        /// <summary>Reads a nullable integer column safely from mixed imported stock schemas.</summary>
        private static int? ReadNullableInt32(SqlDataReader reader, string columnName)
        {
            return !HasColumn(reader, columnName) || reader[columnName] == DBNull.Value ? (int?)null : Convert.ToInt32(reader[columnName]);
        }

        /// <summary>Reads a decimal column safely from mixed imported stock schemas.</summary>
        private static decimal ReadDecimal(SqlDataReader reader, string columnName)
        {
            return !HasColumn(reader, columnName) || reader[columnName] == DBNull.Value ? 0m : Convert.ToDecimal(reader[columnName]);
        }

        /// <summary>Reads a DateTime column safely from mixed imported stock schemas.</summary>
        private static DateTime ReadDateTime(SqlDataReader reader, string columnName)
        {
            return !HasColumn(reader, columnName) || reader[columnName] == DBNull.Value ? DateTime.Now : Convert.ToDateTime(reader[columnName]);
        }

        /// <summary>Checks whether a data reader contains a column before compatibility mapping.</summary>
        private static bool HasColumn(SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static StockItem GetByIdForUpdate(SqlConnection conn, SqlTransaction tx, int id)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT s.*, v.VendorName
                FROM StockItems s WITH (UPDLOCK, ROWLOCK)
                LEFT JOIN Vendors v ON s.VendorID = v.VendorID AND ISNULL(v.IsSupplier, 1) = 1
                WHERE ISNULL(s.IsActive, 1) = 1 AND s.ItemID = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (SqlDataReader r = cmd.ExecuteReader())
                    return r.Read() ? Map(r) : null;
            }
        }

        private static decimal GetSignedQuantity(string movementType, decimal quantity)
        {
            string type = (movementType ?? string.Empty).Trim();
            if (type.Equals("TransferOut", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Issue", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("Decrease", StringComparison.OrdinalIgnoreCase))
                return -Math.Abs(quantity);
            return Math.Abs(quantity);
        }

        private static StockMovement MapMovement(SqlDataReader r)
        {
            return new StockMovement
            {
                MovementID = (int)r["MovementID"],
                ItemID = (int)r["ItemID"],
                ItemName = r["ItemName"] as string,
                MovementType = r["MovementType"] as string,
                Quantity = (decimal)r["Quantity"],
                StockBefore = (decimal)r["StockBefore"],
                StockAfter = (decimal)r["StockAfter"],
                FromLocation = r["FromLocation"] == DBNull.Value ? null : r["FromLocation"] as string,
                ToLocation = r["ToLocation"] == DBNull.Value ? null : r["ToLocation"] as string,
                ReferenceNo = r["ReferenceNo"] == DBNull.Value ? null : r["ReferenceNo"] as string,
                Notes = r["Notes"] == DBNull.Value ? null : r["Notes"] as string,
                CreatedByUserId = r["CreatedByUserId"] == DBNull.Value ? (int?)null : (int)r["CreatedByUserId"],
                CreatedByName = r["CreatedByName"] == DBNull.Value ? null : r["CreatedByName"] as string,
                CreatedDate = (DateTime)r["CreatedDate"]
            };
        }
    }
}
