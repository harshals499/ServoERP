using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class Invoice
    {
        public int    InvoiceID     { get; set; }
        public int    ContractID    { get; set; }
        public int    ClientID      { get; set; }   // direct FK for fast queries
        public int    SiteID        { get; set; }
        public int?   QuotationBidID{ get; set; }
        public string InvoiceNumber { get; set; }   // INV-2025-04-00001
        public string InvoiceTitle  { get; set; } = "TAX INVOICE";
        public string TemplateCode  { get; set; }
        public string WorkflowType  { get; set; }
        public string Subject       { get; set; }
        public string PONumber      { get; set; }
        public DateTime? PODate     { get; set; }
        public string SendInvoiceTo { get; set; }
        public string CertificationNote { get; set; }
        public string GSTMode       { get; set; } = "IGST";
        public string PaymentTerms  { get; set; }
        public string PlaceOfSupply { get; set; }
        public decimal RoundOff     { get; set; }
        public decimal CGSTAmount   { get; set; }
        public decimal SGSTAmount   { get; set; }
        public decimal IGSTAmount   { get; set; }
        public string ContractCoverageType { get; set; }
        public string ServiceChecklist { get; set; }
        public string AssetDetails { get; set; }
        public string WarrantyStatus { get; set; }
        public DateTime? WarrantyExpiry { get; set; }
        public DateTime? PreventiveVisitDate { get; set; }
        public DateTime? NextServiceDueDate { get; set; }
        public string InventoryReservationStatus { get; set; }

        public DateTime  InvoiceDate  { get; set; }
        public DateTime  DueDate      { get; set; }
        public DateTime? PaymentDate  { get; set; }

        public decimal SubTotal    { get; set; }
        public decimal GSTPercent  { get; set; } = 18m;
        public decimal TaxAmount   { get; set; }   // GSTPercent % of SubTotal
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount  { get; set; }
        public decimal BalanceDue  { get; set; }   // TotalAmount - PaidAmount

        // Draft → Pending → Partial → Paid | Overdue
        public string PaymentStatus { get; set; } = "Draft";

        public string Notes { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }

        // Joined / display fields (not stored)
        public string ClientName         { get; set; }
        public string SiteName           { get; set; }
        public string ContractDescription{ get; set; }

        // Child table
        public List<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    }
}
