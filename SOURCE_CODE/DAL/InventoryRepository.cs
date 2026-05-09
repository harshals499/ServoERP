using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class InventoryRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        private const string BaseQuery = @"
            SELECT s.*, v.VendorName FROM StockItems s
            LEFT JOIN Vendors v ON s.VendorID = v.VendorID";

        public List<StockItem> GetAll()
        {
            var list = new List<StockItem>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " ORDER BY s.Category, s.ItemName", conn))
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
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " WHERE s.ItemID = @id", conn))
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
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " WHERE s.CurrentStock <= s.ReorderLevel ORDER BY s.ItemName", conn))
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
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE StockItems SET ItemName=@n,Category=@cat,CurrentStock=@qty,Unit=@unit,
                    LastPurchaseRate=@rate,ReorderLevel=@rl,VendorID=@vid,LastUpdated=GETDATE()
                    WHERE ItemID=@id", conn))
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

        public decimal GetTotalStockValue()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT ISNULL(SUM(CurrentStock * LastPurchaseRate),0) FROM StockItems", conn))
                    return (decimal)cmd.ExecuteScalar();
            }
        }

        public int GetLowStockCount()
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM StockItems WHERE CurrentStock <= ReorderLevel", conn))
                    return (int)cmd.ExecuteScalar();
            }
        }

        public void AddStock(int itemId, decimal qty)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE StockItems SET CurrentStock = CurrentStock + @qty, LastUpdated = GETDATE() WHERE ItemID = @id", conn))
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
                using (SqlCommand cmd = new SqlCommand(
                    BaseQuery + " WHERE s.ItemName LIKE @n ORDER BY s.ItemName", conn))
                {
                    cmd.Parameters.AddWithValue("@n", "%" + name + "%");
                    using (SqlDataReader r = cmd.ExecuteReader())
                        return r.Read() ? Map(r) : null;
                }
            }
        }

        private static StockItem Map(SqlDataReader r) => new StockItem
        {
            ItemID           = (int)r["ItemID"],
            ItemName         = r["ItemName"]         as string,
            Category         = r["Category"]         as string,
            CurrentStock     = (decimal)r["CurrentStock"],
            Unit             = r["Unit"]             as string,
            LastPurchaseRate = (decimal)r["LastPurchaseRate"],
            ReorderLevel     = (decimal)r["ReorderLevel"],
            ReservedStock    = r["ReservedStock"] == DBNull.Value ? 0m : (decimal)r["ReservedStock"],
            VendorID         = r["VendorID"] == DBNull.Value ? (int?)null : (int)r["VendorID"],
            VendorName       = r["VendorName"] == DBNull.Value ? null : r["VendorName"] as string,
            LastUpdated      = (DateTime)r["LastUpdated"],
        };

        private static StockItem GetByIdForUpdate(SqlConnection conn, SqlTransaction tx, int id)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT s.*, v.VendorName
                FROM StockItems s WITH (UPDLOCK, ROWLOCK)
                LEFT JOIN Vendors v ON s.VendorID = v.VendorID
                WHERE s.ItemID = @id", conn, tx))
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
