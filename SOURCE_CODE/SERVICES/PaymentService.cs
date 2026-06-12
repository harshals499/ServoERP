using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Audit;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class PaymentService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
        private readonly PaymentRepository _paymentRepo = new PaymentRepository();
        private readonly InvoiceRepository _invoiceRepo = new InvoiceRepository();
        private readonly PurchaseRepository _purchaseRepo = new PurchaseRepository();
        private readonly PurchaseService _purchaseService = new PurchaseService();
        private readonly VendorAdvancePaymentService _vendorAdvanceService = new VendorAdvancePaymentService();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();
        private readonly AuditTrailService _audit = new AuditTrailService();

        // ── READ ─────────────────────────────────────────────
        public List<Payment> GetAllPayments()
        {
            return AppDataCache.GetOrCreate("payments:all", CacheTtl, _paymentRepo.GetAll);
        }

        public List<Payment> GetPaymentsForInvoice(int invoiceId)
        {
            return _paymentRepo.GetByInvoiceId(invoiceId);
        }

        public List<Payment> GetPaymentsForClient(int clientId)
        {
            return _paymentRepo.GetByClientId(clientId);
        }

        public decimal GetTotalCollectedThisMonth()
        {
            return _paymentRepo.GetTotalCollectedThisMonth();
        }

        // ── RECORD PAYMENT ───────────────────────────────────
        /// <summary>
        /// Records a payment against an invoice and automatically
        /// recalculates PaidAmount / BalanceDue / PaymentStatus.
        /// </summary>
        public int RecordPayment(Payment payment)
        {
            SessionManager.DemandPermission("Payments", "Create");
            if (payment == null)
                throw new Exception("Payment details are missing.");
            if (payment.PaymentDate == default)
                payment.PaymentDate = DateTime.Today;

            try
            {
                // Fetch invoice to check balance
                Invoice inv = _invoiceRepo.GetById(payment.InvoiceID);
                if (inv == null)
                    throw new Exception("Invoice not found.");
                payment.ClientID = inv.ClientID;
                ValidatePaymentForSave(payment);

                decimal alreadyPaid = _paymentRepo.GetTotalPaidForInvoice(payment.InvoiceID);
                decimal remaining   = inv.TotalAmount - alreadyPaid;

                if (payment.AmountPaid > remaining + 0.01m)   // 1-paisa tolerance for rounding
                    throw new Exception(
                        $"Amount paid (₹{payment.AmountPaid:N2}) exceeds the outstanding balance " +
                        $"(₹{remaining:N2}) for this invoice.");

                // Save payment
                if (SessionManager.IsLoggedIn)
                {
                    payment.CreatedByUserId = SessionManager.CurrentUser.UserId;
                    payment.CreatedByName = SessionManager.CurrentUser.DisplayName;
                }
                payment.PaymentNumber = _paymentRepo.GeneratePaymentNumber();
                int paymentId = _paymentRepo.Create(payment);

                // Recalculate invoice status
                RecalculateInvoiceStatus(payment.InvoiceID);
                AppDataCache.RemovePrefix("payments:");
                AppDataCache.RemovePrefix("invoices:");
                SessionManager.LogAction("CREATE", "Payments", paymentId, "Payment recorded");
                _audit.Record("CREATE", "Payments", paymentId, "Payment " + payment.PaymentNumber + " recorded against invoice #" + payment.InvoiceID + " for " + payment.AmountPaid.ToString("N2"));

                return paymentId;
            }
            catch (Exception ex) when (OfflineSyncService.ShouldQueue(ex))
            {
                OfflineQueueResult queued = OfflineSyncService.Queue("Payments", "RecordDraft", payment, payment.InvoiceID, true, ex.Message);
                AppDataCache.RemovePrefix("payments:");
                AppDataCache.RemovePrefix("invoices:");
                return queued.LocalId;
            }
        }

        // ── STATUS RECALCULATION ─────────────────────────────
        /// <summary>
        /// Recomputes PaidAmount, BalanceDue, and PaymentStatus for an invoice
        /// based on the sum of all linked payment records.
        /// </summary>
        public void DeletePayment(int paymentId)
        {
            SessionManager.DemandPermission("Payments", "Delete");
            Payment payment = _paymentRepo.GetById(paymentId);
            if (payment == null)
                throw new Exception("Payment record not found.");

            _paymentRepo.Delete(paymentId);
            RecalculateInvoiceStatus(payment.InvoiceID);
            AppDataCache.RemovePrefix("payments:");
            AppDataCache.RemovePrefix("invoices:");
            SessionManager.LogAction("DELETE", "Payments", paymentId, "Payment deleted");
            _audit.Record("DELETE", "Payments", paymentId, "Payment " + (payment.PaymentNumber ?? paymentId.ToString()) + " deleted and invoice payment status recalculated");
        }

        public void RecalculateInvoiceStatus(int invoiceId)
        {
            Invoice inv = _invoiceRepo.GetById(invoiceId);
            if (inv == null) return;

            decimal totalPaid = _paymentRepo.GetTotalPaidForInvoice(invoiceId);
            decimal balance   = inv.TotalAmount - totalPaid;

            string status;
            if (totalPaid <= 0)
                status = inv.DueDate < DateTime.Today ? "Overdue" : "Pending";
            else if (balance <= 0.01m)   // fully paid
                status = "Paid";
            else
                status = "Partial";

            _invoiceRepo.UpdatePaymentStatus(invoiceId, totalPaid, status);
            AppDataCache.RemovePrefix("invoices:");
            _audit.Record("RECALCULATE", "Invoices", invoiceId, "Invoice payment status recalculated as " + status + " with paid amount " + totalPaid.ToString("N2"));
        }

        // ── CASH FLOW SUMMARY ─────────────────────────────────
        public decimal GetTotalPaidForInvoice(int invoiceId)
        {
            return _paymentRepo.GetTotalPaidForInvoice(invoiceId);
        }

        public void BatchPayPurchaseOrders(IEnumerable<int> poIds, string referenceNumber)
        {
            SessionManager.DemandPermission("Payments", "Create");
            List<int> ids = poIds == null ? new List<int>() : new List<int>(poIds);
            decimal advanceApplied = 0m;
            foreach (int poId in ids)
            {
                PurchaseOrder po = _purchaseService.GetById(poId);
                advanceApplied += _vendorAdvanceService.ApplyAvailableAdvanceToPurchaseOrder(po);
            }

            string finalReference = referenceNumber;
            if (advanceApplied > 0m)
                finalReference = (string.IsNullOrWhiteSpace(finalReference) ? string.Empty : finalReference.Trim() + " | ") + "Advance adjusted " + advanceApplied.ToString("N2");
            _purchaseRepo.BatchMarkPaid(ids, finalReference);
            AppDataCache.RemovePrefix("purchases:");
            int count = ids.Count;
            _audit.Record("PAY", "Purchases", null, "Batch paid " + count + " purchase order(s). Reference: " + (finalReference ?? string.Empty));
        }

        private void ValidatePaymentForSave(Payment payment)
        {
            ValidationResult result = _businessRules.ValidatePayment(payment);
            _validation.EnsureValid(result, "Payment validation failed");
        }
    }
}
