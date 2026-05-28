using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class IntegrationOperationResult
    {
        public bool Success { get; set; }
        public string Provider { get; set; }
        public string Operation { get; set; }
        public string Message { get; set; }
        public string ReferenceId { get; set; }
        public string LocalPath { get; set; }
        public string RawResponse { get; set; }

        public static IntegrationOperationResult Ok(string provider, string operation, string message)
        {
            return new IntegrationOperationResult
            {
                Success = true,
                Provider = provider,
                Operation = operation,
                Message = message
            };
        }

        public static IntegrationOperationResult Fail(string provider, string operation, string message)
        {
            return new IntegrationOperationResult
            {
                Success = false,
                Provider = provider,
                Operation = operation,
                Message = message
            };
        }
    }

    public sealed class WhatsAppTemplateComponent
    {
        public string Type { get; set; }
        public List<string> TextParameters { get; set; } = new List<string>();
    }

    public sealed class CalendarDispatchEvent
    {
        public string Subject { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string OrganizerEmail { get; set; }
        public string AttendeeEmail { get; set; }
        public string ExternalId { get; set; }
    }

    public sealed class GstEinvoiceParty
    {
        public string Gstin { get; set; }
        public string LegalName { get; set; }
        public string TradeName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Location { get; set; }
        public string PinCode { get; set; }
        public string StateCode { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }

    public sealed class GstEinvoiceLine
    {
        public string Description { get; set; }
        public string HsnCode { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GstRate { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public sealed class GstEinvoicePayload
    {
        public string DocumentType { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime DocumentDate { get; set; }
        public string SupplyType { get; set; }
        public string GstMode { get; set; }
        public GstEinvoiceParty Seller { get; set; }
        public GstEinvoiceParty Buyer { get; set; }
        public List<GstEinvoiceLine> Items { get; set; } = new List<GstEinvoiceLine>();
        public decimal TotalTaxableAmount { get; set; }
        public decimal TotalCgstAmount { get; set; }
        public decimal TotalSgstAmount { get; set; }
        public decimal TotalIgstAmount { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
    }

    public sealed class FieldServiceFormTemplate
    {
        public string Category { get; set; }
        public string Trade { get; set; }
        public string FormName { get; set; }
        public string FileName { get; set; }
        public string SourceType { get; set; }
        public string SourceUrl { get; set; }
        public string RecommendedServoErpModule { get; set; }
        public string RecommendedFields { get; set; }
        public bool MobileSuitable { get; set; }
        public bool RequiresCustomerSignature { get; set; }
        public bool RequiresTechnicianSignature { get; set; }
        public bool RequiresPhotoUpload { get; set; }
        public bool RequiresReadings { get; set; }
        public bool ComplianceCritical { get; set; }
        public string FullPath { get; set; }
    }
}
