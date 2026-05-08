using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services.Audit;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class VendorAdvancePaymentService
    {
        private readonly DbExecutor _db = new DbExecutor();
        private readonly AuditTrailService _audit = new AuditTrailService();

        public int RecordAdvance(int vendorId, decimal amount, DateTime paymentDate, string paymentMode, string referenceNumber, string notes)
        {
            SessionManager.DemandPermission("Payments", "Create");
            if (vendorId <= 0) throw new InvalidOperationException("Vendor is required.");
            if (amount <= 0) throw new InvalidOperationException("Advance amount must be greater than zero.");

            int id = Insert(vendorId, null, "Advance", amount, 0m, paymentDate, paymentMode, referenceNumber, notes);
            SessionManager.LogAction("CREATE", "Payments", id, "Vendor advance recorded");
            _audit.Record("CREATE", "Payments", id, "Vendor advance recorded for vendor #" + vendorId + " amount " + amount.ToString("N2"));
            AppDataCache.RemovePrefix("purchases:");
            return id;
        }

        public int RecordRefund(int vendorId, decimal amount, DateTime refundDate, string paymentMode, string referenceNumber, string notes)
        {
            SessionManager.DemandPermission("Payments", "Create");
            if (vendorId <= 0) throw new InvalidOperationException("Vendor is required.");
            if (amount <= 0) throw new InvalidOperationException("Refund amount must be greater than zero.");
            if (amount > GetVendorAdvanceBalance(vendorId) + 0.01m)
                throw new InvalidOperationException("Refund exceeds available vendor advance balance.");

            int id = Insert(vendorId, null, "Refund", amount, 0m, refundDate, paymentMode, referenceNumber, notes);
            SessionManager.LogAction("REFUND", "Payments", id, "Vendor advance refund recorded");
            _audit.Record("REFUND", "Payments", id, "Vendor advance refund for vendor #" + vendorId + " amount " + amount.ToString("N2"));
            AppDataCache.RemovePrefix("purchases:");
            return id;
        }

        public decimal ApplyAvailableAdvanceToPurchaseOrder(PurchaseOrder po)
        {
            if (po == null || po.POID <= 0 || po.VendorID <= 0 || po.BalanceDue <= 0m)
                return 0m;

            decimal remaining = po.BalanceDue;
            decimal applied = 0m;
            foreach (VendorAdvancePayment advance in GetOpenAdvances(po.VendorID))
            {
                if (remaining <= 0.01m) break;
                decimal use = Math.Min(advance.Balance, remaining);
                if (use <= 0m) continue;

                _db.Execute("UPDATE VendorAdvancePayments SET AppliedAmount = AppliedAmount + @amount WHERE AdvancePaymentId=@id",
                    DbExecutor.Param("@amount", use),
                    DbExecutor.Param("@id", advance.AdvancePaymentId));
                Insert(po.VendorID, po.POID, "Adjustment", use, 0m, DateTime.Today, "Advance Adjustment", po.PONumber, "Applied advance #" + advance.AdvancePaymentId + " to PO " + po.PONumber);
                remaining -= use;
                applied += use;
            }

            if (applied > 0m)
            {
                SessionManager.LogAction("ADJUST", "Payments", po.POID, "Vendor advance applied to purchase order");
                _audit.Record("ADJUST", "Payments", po.POID, "Applied vendor advance " + applied.ToString("N2") + " to PO " + po.PONumber);
                AppDataCache.RemovePrefix("purchases:");
            }

            return applied;
        }

        public decimal GetVendorAdvanceBalance(int vendorId)
        {
            if (vendorId <= 0) return 0m;
            return _db.Scalar<decimal>(@"
SELECT ISNULL(SUM(CASE
    WHEN TransactionType='Advance' THEN Amount - AppliedAmount
    WHEN TransactionType='Refund' THEN -Amount
    ELSE 0 END), 0)
FROM VendorAdvancePayments
WHERE VendorId=@vendorId",
                DbExecutor.Param("@vendorId", vendorId));
        }

        public List<VendorAdvancePayment> GetByVendor(int vendorId)
        {
            return Query("WHERE vap.VendorId=@vendorId ORDER BY vap.TransactionDate DESC, vap.AdvancePaymentId DESC",
                DbExecutor.Param("@vendorId", vendorId));
        }

        public List<VendorAdvancePayment> GetAll()
        {
            return Query("ORDER BY vap.TransactionDate DESC, vap.AdvancePaymentId DESC");
        }

        private List<VendorAdvancePayment> GetOpenAdvances(int vendorId)
        {
            return Query("WHERE vap.VendorId=@vendorId AND vap.TransactionType='Advance' AND vap.Amount > vap.AppliedAmount ORDER BY vap.TransactionDate, vap.AdvancePaymentId",
                DbExecutor.Param("@vendorId", vendorId));
        }

        private int Insert(int vendorId, int? poId, string type, decimal amount, decimal appliedAmount, DateTime date, string mode, string reference, string notes)
        {
            return _db.Scalar<int>(@"
INSERT INTO VendorAdvancePayments
    (VendorId, POID, TransactionType, Amount, AppliedAmount, TransactionDate, PaymentMode, ReferenceNumber, Notes, CreatedByUserId, CreatedByName)
VALUES
    (@vendorId, @poId, @type, @amount, @applied, @date, @mode, @reference, @notes, @userId, @userName);
SELECT CAST(SCOPE_IDENTITY() AS INT);",
                DbExecutor.Param("@vendorId", vendorId),
                DbExecutor.Param("@poId", poId.HasValue ? (object)poId.Value : DBNull.Value),
                DbExecutor.Param("@type", type),
                DbExecutor.Param("@amount", amount),
                DbExecutor.Param("@applied", appliedAmount),
                DbExecutor.Param("@date", date == default(DateTime) ? DateTime.Today : date.Date),
                DbExecutor.Param("@mode", string.IsNullOrWhiteSpace(mode) ? DBNull.Value : (object)mode.Trim()),
                DbExecutor.Param("@reference", string.IsNullOrWhiteSpace(reference) ? DBNull.Value : (object)reference.Trim()),
                DbExecutor.Param("@notes", string.IsNullOrWhiteSpace(notes) ? DBNull.Value : (object)notes.Trim()),
                DbExecutor.Param("@userId", SessionManager.CurrentUser == null ? (object)DBNull.Value : SessionManager.CurrentUser.UserId),
                DbExecutor.Param("@userName", SessionManager.CurrentUser == null ? (object)DBNull.Value : SessionManager.CurrentUser.DisplayName));
        }

        private List<VendorAdvancePayment> Query(string suffix, params SqlParameter[] parameters)
        {
            string sql = @"
SELECT vap.*, v.VendorName, po.PONumber
FROM VendorAdvancePayments vap
LEFT JOIN Vendors v ON vap.VendorId = v.VendorID
LEFT JOIN PurchaseOrders po ON vap.POID = po.POID
" + suffix;
            return _db.Query(sql, Map, parameters);
        }

        private static VendorAdvancePayment Map(SqlDataReader reader)
        {
            return new VendorAdvancePayment
            {
                AdvancePaymentId = Convert.ToInt32(reader["AdvancePaymentId"]),
                VendorId = Convert.ToInt32(reader["VendorId"]),
                VendorName = Convert.ToString(reader["VendorName"]),
                POID = reader["POID"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["POID"]),
                PONumber = Convert.ToString(reader["PONumber"]),
                TransactionType = Convert.ToString(reader["TransactionType"]),
                Amount = Convert.ToDecimal(reader["Amount"]),
                AppliedAmount = Convert.ToDecimal(reader["AppliedAmount"]),
                TransactionDate = Convert.ToDateTime(reader["TransactionDate"]),
                PaymentMode = Convert.ToString(reader["PaymentMode"]),
                ReferenceNumber = Convert.ToString(reader["ReferenceNumber"]),
                Notes = Convert.ToString(reader["Notes"]),
                CreatedByUserId = reader["CreatedByUserId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CreatedByUserId"]),
                CreatedByName = Convert.ToString(reader["CreatedByName"]),
                CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
            };
        }
    }
}
