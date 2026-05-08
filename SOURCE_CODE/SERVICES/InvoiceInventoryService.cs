using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class InvoiceInventoryService
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly InventoryService _inventorySvc = new InventoryService();

        public void SyncDraftReservations(int invoiceId, IEnumerable<InvoiceLineItem> lineItems)
        {
            if (invoiceId <= 0)
                return;

            var target = (lineItems ?? new List<InvoiceLineItem>())
                .Where(i => i != null && i.IsStockItem && i.StockItemID.HasValue && i.Quantity > 0)
                .GroupBy(i => i.StockItemID.Value)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var existing = new Dictionary<int, decimal>();
                    using (var cmd = new SqlCommand("SELECT StockItemID, QuantityReserved FROM InvoiceInventoryReservations WHERE InvoiceID = @invoiceId AND Status = 'DraftReserved'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                existing[Convert.ToInt32(reader["StockItemID"])] = Convert.ToDecimal(reader["QuantityReserved"]);
                        }
                    }

                    foreach (var itemId in existing.Keys.Union(target.Keys).Distinct().ToList())
                    {
                        decimal current = existing.ContainsKey(itemId) ? existing[itemId] : 0m;
                        decimal desired = target.ContainsKey(itemId) ? target[itemId] : 0m;
                        decimal delta = desired - current;

                        if (delta != 0)
                        {
                            using (var stockCmd = new SqlCommand("UPDATE StockItems SET ReservedStock = ISNULL(ReservedStock,0) + @delta, LastUpdated = GETDATE() WHERE ItemID = @itemId", conn, tx))
                            {
                                stockCmd.Parameters.AddWithValue("@delta", delta);
                                stockCmd.Parameters.AddWithValue("@itemId", itemId);
                                stockCmd.ExecuteNonQuery();
                            }

                            if (desired <= 0)
                            {
                                using (var deleteCmd = new SqlCommand("DELETE FROM InvoiceInventoryReservations WHERE InvoiceID = @invoiceId AND StockItemID = @itemId AND Status = 'DraftReserved'", conn, tx))
                                {
                                    deleteCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                                    deleteCmd.Parameters.AddWithValue("@itemId", itemId);
                                    deleteCmd.ExecuteNonQuery();
                                }

                                LogUsage(conn, tx, invoiceId, itemId, Math.Abs(delta), "Released", "Draft reservation removed.");
                            }
                            else if (current <= 0)
                            {
                                using (var insertCmd = new SqlCommand(@"
                                    INSERT INTO InvoiceInventoryReservations (InvoiceID, StockItemID, QuantityReserved, QuantityIssued, Status, UpdatedDate)
                                    VALUES (@invoiceId, @itemId, @reserved, 0, 'DraftReserved', GETDATE())", conn, tx))
                                {
                                    insertCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                                    insertCmd.Parameters.AddWithValue("@itemId", itemId);
                                    insertCmd.Parameters.AddWithValue("@reserved", desired);
                                    insertCmd.ExecuteNonQuery();
                                }

                                LogUsage(conn, tx, invoiceId, itemId, desired, "Reserved", "Draft reservation created.");
                            }
                            else
                            {
                                using (var updateCmd = new SqlCommand(@"
                                    UPDATE InvoiceInventoryReservations
                                    SET QuantityReserved = @reserved, UpdatedDate = GETDATE()
                                    WHERE InvoiceID = @invoiceId AND StockItemID = @itemId AND Status = 'DraftReserved'", conn, tx))
                                {
                                    updateCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                                    updateCmd.Parameters.AddWithValue("@itemId", itemId);
                                    updateCmd.Parameters.AddWithValue("@reserved", desired);
                                    updateCmd.ExecuteNonQuery();
                                }

                                LogUsage(conn, tx, invoiceId, itemId, Math.Abs(delta), delta > 0 ? "Reserved" : "Released", "Draft reservation adjusted.");
                            }
                        }
                    }

                    tx.Commit();
                }
            }

            AppDataCache.RemovePrefix("inventory:");
        }

        public void FinalizeReservations(int invoiceId)
        {
            if (invoiceId <= 0)
                return;

            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var reservations = GetReservations(conn, tx, invoiceId, "DraftReserved");
                    foreach (var reservation in reservations)
                    {
                        using (var stockCmd = new SqlCommand(@"
                            UPDATE StockItems
                            SET ReservedStock = ISNULL(ReservedStock,0) - @qty,
                                CurrentStock = CurrentStock - @qty,
                                LastUpdated = GETDATE()
                            WHERE ItemID = @itemId", conn, tx))
                        {
                            stockCmd.Parameters.AddWithValue("@qty", reservation.QuantityReserved);
                            stockCmd.Parameters.AddWithValue("@itemId", reservation.StockItemID);
                            stockCmd.ExecuteNonQuery();
                        }

                        using (var updateCmd = new SqlCommand(@"
                            UPDATE InvoiceInventoryReservations
                            SET QuantityIssued = QuantityReserved,
                                QuantityReserved = 0,
                                Status = 'Issued',
                                UpdatedDate = GETDATE()
                            WHERE ReservationID = @id", conn, tx))
                        {
                            updateCmd.Parameters.AddWithValue("@id", reservation.ReservationID);
                            updateCmd.ExecuteNonQuery();
                        }

                        LogUsage(conn, tx, invoiceId, reservation.StockItemID, reservation.QuantityReserved, "Issued", "Stock deducted on invoice finalisation.");
                    }

                    tx.Commit();
                }
            }

            AppDataCache.RemovePrefix("inventory:");
        }

        public void CancelReservations(int invoiceId)
        {
            if (invoiceId <= 0)
                return;

            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    var reservations = GetReservations(conn, tx, invoiceId, null);
                    foreach (var reservation in reservations)
                    {
                        if (string.Equals(reservation.Status, "DraftReserved", StringComparison.OrdinalIgnoreCase) && reservation.QuantityReserved > 0)
                        {
                            using (var stockCmd = new SqlCommand("UPDATE StockItems SET ReservedStock = ISNULL(ReservedStock,0) - @qty, LastUpdated = GETDATE() WHERE ItemID = @itemId", conn, tx))
                            {
                                stockCmd.Parameters.AddWithValue("@qty", reservation.QuantityReserved);
                                stockCmd.Parameters.AddWithValue("@itemId", reservation.StockItemID);
                                stockCmd.ExecuteNonQuery();
                            }

                            LogUsage(conn, tx, invoiceId, reservation.StockItemID, reservation.QuantityReserved, "Released", "Reservation released on invoice cancellation.");
                        }

                        if (string.Equals(reservation.Status, "Issued", StringComparison.OrdinalIgnoreCase) && reservation.QuantityIssued > 0)
                        {
                            using (var stockCmd = new SqlCommand("UPDATE StockItems SET CurrentStock = CurrentStock + @qty, LastUpdated = GETDATE() WHERE ItemID = @itemId", conn, tx))
                            {
                                stockCmd.Parameters.AddWithValue("@qty", reservation.QuantityIssued);
                                stockCmd.Parameters.AddWithValue("@itemId", reservation.StockItemID);
                                stockCmd.ExecuteNonQuery();
                            }

                            LogUsage(conn, tx, invoiceId, reservation.StockItemID, reservation.QuantityIssued, "Restored", "Issued stock restored on invoice cancellation.");
                        }

                        using (var updateCmd = new SqlCommand(@"
                            UPDATE InvoiceInventoryReservations
                            SET QuantityReserved = 0,
                                QuantityIssued = 0,
                                Status = 'Cancelled',
                                UpdatedDate = GETDATE()
                            WHERE ReservationID = @id", conn, tx))
                        {
                            updateCmd.Parameters.AddWithValue("@id", reservation.ReservationID);
                            updateCmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }
            }

            AppDataCache.RemovePrefix("inventory:");
        }

        public string BuildAvailabilitySummary(IEnumerable<InvoiceLineItem> lineItems)
        {
            var lines = new List<string>();
            foreach (var item in (lineItems ?? Enumerable.Empty<InvoiceLineItem>()).Where(i => i != null && i.IsStockItem && i.StockItemID.HasValue))
            {
                var stock = _inventorySvc.GetById(item.StockItemID.Value);
                if (stock == null)
                {
                    lines.Add(item.Description + ": not linked to stock master.");
                    continue;
                }

                decimal requested = item.Quantity;
                decimal available = stock.AvailableStock;
                if (available >= requested)
                    lines.Add(stock.ItemName + ": reserve " + requested.ToString("N2") + " " + stock.Unit + " from available " + available.ToString("N2") + ".");
                else
                    lines.Add(stock.ItemName + ": shortage " + (requested - available).ToString("N2") + " " + stock.Unit + " (available " + available.ToString("N2") + ").");
            }

            return lines.Count == 0 ? "No stock-linked invoice items in this draft." : string.Join(Environment.NewLine, lines);
        }

        public List<InvoiceInventoryReservation> GetReservations(int invoiceId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                return GetReservations(conn, null, invoiceId, null);
            }
        }

        private List<InvoiceInventoryReservation> GetReservations(SqlConnection conn, SqlTransaction tx, int invoiceId, string status)
        {
            var list = new List<InvoiceInventoryReservation>();
            var sql = new StringBuilder();
            sql.Append(@"SELECT r.*, s.ItemName
                         FROM InvoiceInventoryReservations r
                         LEFT JOIN StockItems s ON r.StockItemID = s.ItemID
                         WHERE r.InvoiceID = @invoiceId");
            if (!string.IsNullOrWhiteSpace(status))
                sql.Append(" AND r.Status = @status");

            using (var cmd = new SqlCommand(sql.ToString(), conn, tx))
            {
                cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                if (!string.IsNullOrWhiteSpace(status))
                    cmd.Parameters.AddWithValue("@status", status);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new InvoiceInventoryReservation
                        {
                            ReservationID = Convert.ToInt32(reader["ReservationID"]),
                            InvoiceID = Convert.ToInt32(reader["InvoiceID"]),
                            StockItemID = Convert.ToInt32(reader["StockItemID"]),
                            QuantityReserved = Convert.ToDecimal(reader["QuantityReserved"]),
                            QuantityIssued = Convert.ToDecimal(reader["QuantityIssued"]),
                            Status = reader["Status"] as string,
                            UpdatedDate = Convert.ToDateTime(reader["UpdatedDate"]),
                            ItemName = reader["ItemName"] as string
                        });
                    }
                }
            }

            return list;
        }

        private void LogUsage(SqlConnection conn, SqlTransaction tx, int invoiceId, int stockItemId, decimal quantity, string action, string notes)
        {
            if (quantity <= 0)
                return;

            using (var cmd = new SqlCommand(@"
                INSERT INTO InventoryUsageLog (InvoiceID, StockItemID, Quantity, UsageAction, Notes, LoggedAt)
                VALUES (@invoiceId, @stockItemId, @quantity, @action, @notes, GETDATE())", conn, tx))
            {
                cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                cmd.Parameters.AddWithValue("@stockItemId", stockItemId);
                cmd.Parameters.AddWithValue("@quantity", quantity);
                cmd.Parameters.AddWithValue("@action", action ?? string.Empty);
                cmd.Parameters.AddWithValue("@notes", notes ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
