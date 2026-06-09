using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class PaymentRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private const int DefaultRecentRows = 1000;

        // ── READ ─────────────────────────────────────────────
        public List<Payment> GetAll()
        {
            return GetRecent(DefaultRecentRows);
        }

        public List<Payment> GetRecent(int maxRows)
        {
            var list = new List<Payment>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT TOP (@maxRows) p.*, i.InvoiceNumber, c.CompanyName AS ClientName
                    FROM Payments p
                    JOIN Invoices i ON p.InvoiceID = i.InvoiceID
                    JOIN B2BClients c ON p.ClientID = c.ClientID
                    ORDER BY p.PaymentDate DESC, p.PaymentID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@maxRows", Math.Max(1, maxRows));
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public List<Payment> GetByInvoiceId(int invoiceId)
        {
            var list = new List<Payment>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT p.*, i.InvoiceNumber, c.CompanyName AS ClientName
                    FROM Payments p
                    JOIN Invoices i ON p.InvoiceID = i.InvoiceID
                    JOIN B2BClients c ON p.ClientID = c.ClientID
                    WHERE p.InvoiceID = @id ORDER BY p.PaymentDate DESC, p.PaymentID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", invoiceId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public List<Payment> GetByClientId(int clientId)
        {
            var list = new List<Payment>();
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT p.*, i.InvoiceNumber, c.CompanyName AS ClientName
                    FROM Payments p
                    JOIN Invoices i ON p.InvoiceID = i.InvoiceID
                    JOIN B2BClients c ON p.ClientID = c.ClientID
                    WHERE p.ClientID = @id ORDER BY p.PaymentDate DESC, p.PaymentID DESC";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", clientId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(Map(r));
                }
            }
            return list;
        }

        public Payment GetById(int paymentId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT p.*, i.InvoiceNumber, c.CompanyName AS ClientName
                    FROM Payments p
                    JOIN Invoices i ON p.InvoiceID = i.InvoiceID
                    JOIN B2BClients c ON p.ClientID = c.ClientID
                    WHERE p.PaymentID = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", paymentId);
                    using (var r = cmd.ExecuteReader())
                        return r.Read() ? Map(r) : null;
                }
            }
        }

        public decimal GetTotalPaidForInvoice(int invoiceId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(SUM(AmountPaid),0) FROM Payments WHERE InvoiceID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", invoiceId);
                    return (decimal)cmd.ExecuteScalar();
                }
            }
        }

        // ── CREATE ───────────────────────────────────────────
        public int Create(Payment p)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO Payments
                        (PaymentNumber,InvoiceID,ClientID,AmountPaid,PaymentDate,
                         PaymentMode,ReferenceNumber,Notes,CreatedDate,CreatedByUserId,CreatedByName)
                    VALUES
                        (@num,@inv,@client,@amt,@date,
                         @mode,@ref,@notes,GETDATE(),@createdByUserId,@createdByName);
                    SELECT SCOPE_IDENTITY();";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@num",    p.PaymentNumber ?? GeneratePaymentNumber());
                    cmd.Parameters.AddWithValue("@inv",    p.InvoiceID);
                    cmd.Parameters.AddWithValue("@client", p.ClientID);
                    SqlParameter amountParameter = cmd.Parameters.Add("@amt", SqlDbType.Decimal);
                    amountParameter.Precision = 12;
                    amountParameter.Scale = 2;
                    amountParameter.Value = p.AmountPaid;
                    cmd.Parameters.AddWithValue("@date",   p.PaymentDate);
                    cmd.Parameters.AddWithValue("@mode",   p.PaymentMode ?? "Bank Transfer");
                    cmd.Parameters.AddWithValue("@ref",    p.ReferenceNumber ?? "");
                    cmd.Parameters.AddWithValue("@notes",  p.Notes ?? "");
                    cmd.Parameters.AddWithValue("@createdByUserId", p.CreatedByUserId.HasValue ? (object)p.CreatedByUserId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(p.CreatedByName) ? (object)DBNull.Value : p.CreatedByName);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // ── SUMMARY ──────────────────────────────────────────
        public void Delete(int paymentId)
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("DELETE FROM Payments WHERE PaymentID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", paymentId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public decimal GetTotalCollectedThisMonth()
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT ISNULL(SUM(AmountPaid),0) FROM Payments
                    WHERE MONTH(PaymentDate)=MONTH(GETDATE()) AND YEAR(PaymentDate)=YEAR(GETDATE())";
                using (var cmd = new SqlCommand(sql, conn))
                    return (decimal)cmd.ExecuteScalar();
            }
        }

        // ── HELPERS ──────────────────────────────────────────
        public string GeneratePaymentNumber()
        {
            using (var conn = _db.GetConnection())
            {
                conn.Open();
                string prefix = "PAY-" + DateTime.Now.ToString("yyyy-MM");
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Payments WHERE PaymentNumber LIKE @p", conn))
                {
                    cmd.Parameters.AddWithValue("@p", prefix + "-%");
                    int count = (int)cmd.ExecuteScalar();
                    return $"{prefix}-{(count + 1):D5}";
                }
            }
        }

        private Payment Map(SqlDataReader r) => new Payment
        {
            PaymentID       = (int)r["PaymentID"],
            PaymentNumber   = r["PaymentNumber"].ToString(),
            InvoiceID       = (int)r["InvoiceID"],
            ClientID        = (int)r["ClientID"],
            AmountPaid      = (decimal)r["AmountPaid"],
            PaymentDate     = (DateTime)r["PaymentDate"],
            PaymentMode     = r["PaymentMode"].ToString(),
            ReferenceNumber = r["ReferenceNumber"].ToString(),
            Notes           = r["Notes"].ToString(),
            CreatedDate     = r["CreatedDate"] != DBNull.Value ? (DateTime)r["CreatedDate"] : DateTime.Now,
            CreatedByUserId = r["CreatedByUserId"] == DBNull.Value ? (int?)null : (int)r["CreatedByUserId"],
            CreatedByName = r["CreatedByName"] == DBNull.Value ? null : r["CreatedByName"].ToString(),
            ModifiedByUserId = r["ModifiedByUserId"] == DBNull.Value ? (int?)null : (int)r["ModifiedByUserId"],
            ModifiedByName = r["ModifiedByName"] == DBNull.Value ? null : r["ModifiedByName"].ToString(),
            ModifiedDate = r["ModifiedDate"] == DBNull.Value ? (DateTime?)null : (DateTime)r["ModifiedDate"],
            InvoiceNumber   = r["InvoiceNumber"].ToString(),
            ClientName      = r["ClientName"].ToString()
        };
    }
}
