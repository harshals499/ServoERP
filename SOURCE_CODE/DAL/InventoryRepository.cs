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
    }
}
