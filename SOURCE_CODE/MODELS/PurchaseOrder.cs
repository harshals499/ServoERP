using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class PurchaseOrder
    {
        public int      POID        { get; set; }
        public int      VendorID    { get; set; }
        public int      ClientID    { get; set; }
        public int      SiteID      { get; set; }
        public int?     RelatedContractID { get; set; }
        public int?     RecommendedByBidID { get; set; }
        public string   VendorName  { get; set; }
        public string   VendorGSTIN { get; set; }
        public string   ClientName  { get; set; }
        public string   SiteName    { get; set; }
        public string   PONumber    { get; set; }
        public DateTime PODate      { get; set; }
        public DateTime PayByDate   { get; set; }
        public string   VendorInvoiceNumber { get; set; }
        public string   LinkedToType { get; set; }
        public int?     LinkedToId   { get; set; }
        public string   LinkedToLabel { get; set; }
        public string   DeliveryMode { get; set; }
        public int?     AssignedTechnicianId { get; set; }
        public string   AssignedTechnicianName { get; set; }
        public string   DeliveryAddress { get; set; }
        public bool     AddToClientInvoice { get; set; }
        public bool     PendingChargeCreated { get; set; }
        public string   ReceiptImagePath { get; set; }
        public string   PdfPath { get; set; }
        public bool     PriceVarianceFlag { get; set; }
        public int?     CreatedByUserId { get; set; }
        public string   CreatedByName { get; set; }
        public DateTime? CreatedByDate { get; set; }
        public int?     ModifiedByUserId { get; set; }
        public string   ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public decimal  TotalAmount { get; set; }
        public decimal  PaidAmount  { get; set; }
        public bool     IsPaymentCompleted => IsPaymentCompletedStatus(Status);
        public decimal  BalanceDue  => IsPaymentCompleted ? 0m : Math.Max(0m, TotalAmount - PaidAmount);
        public bool     IsOverdue   => !IsPaymentCompleted && BalanceDue > 0.01m && PayByDate.Date < DateTime.Today;
        public int      AgeDays     => Math.Max(0, (DateTime.Today - PODate.Date).Days);
        public string   Status      { get; set; }
        public string   PaymentReference { get; set; }
        public string   ComparisonNotes { get; set; }
        public string   Notes       { get; set; }
        public DateTime CreatedDate { get; set; }

        public List<PurchaseLineItem> LineItems { get; set; } = new List<PurchaseLineItem>();

        /// <summary>Returns true when a purchase-order status means vendor payment is complete.</summary>
        public static bool IsPaymentCompletedStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            string normalized = status.Trim();
            return string.Equals(normalized, "Fully Received", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Received", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Closed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Applies the ServoERP rule that fully received purchase orders are fully paid.</summary>
        public void ApplyPaymentCompletionRule()
        {
            if (IsPaymentCompleted)
                PaidAmount = TotalAmount;
        }
    }

    public class PurchaseLineItem
    {
        public int     LineItemID  { get; set; }
        public int     POID        { get; set; }
        public int?    InventoryItemId { get; set; }
        public string  Description { get; set; }
        public string  HsnSacCode  { get; set; }
        public decimal Quantity    { get; set; }
        public string  UOM         { get; set; }
        public decimal Rate        { get; set; }
        public decimal GSTRate     { get; set; }
        public decimal CGSTRate    { get; set; }
        public decimal SGSTRate    { get; set; }
        public decimal IGSTRate    { get; set; }
        public string  JobLink     { get; set; }
        public int?    LinkedWorkOrderId { get; set; }
        public string  LinkedWorkOrderName { get; set; }
        public decimal PriceVariance { get; set; }
        public decimal HistoricalRate { get; set; }
        public decimal Amount      { get; set; }
    }

    public class VendorPayableGroup
    {
        public int VendorID { get; set; }
        public string VendorName { get; set; }
        public decimal TotalOutstanding { get; set; }
        public int OverdueCount { get; set; }
        public List<PurchaseOrder> Purchases { get; set; } = new List<PurchaseOrder>();
    }

    public class VendorAdvancePayment
    {
        public int AdvancePaymentId { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public int? POID { get; set; }
        public string PONumber { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public decimal AppliedAmount { get; set; }
        public decimal Balance => string.Equals(TransactionType, "Advance", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(0m, Amount - AppliedAmount)
            : 0m;
        public DateTime TransactionDate { get; set; }
        public string PaymentMode { get; set; }
        public string ReferenceNumber { get; set; }
        public string Notes { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
