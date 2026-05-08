using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public enum CompanyDocumentTemplateType
    {
        Invoice,
        Quotation,
        PurchaseOrder,
        DeliveryNote,
        Letterhead,
        Contract,
        Report,
        TermsAndConditions,
        Other
    }

    public sealed class CompanyDocumentTemplate
    {
        public string TemplateId { get; set; }
        public string CompanyKey { get; set; }
        public string TemplateName { get; set; }
        public CompanyDocumentTemplateType DocumentType { get; set; }
        public string OriginalFileName { get; set; }
        public string StoredFilePath { get; set; }
        public string FileExtension { get; set; }
        public string RecognitionStatus { get; set; }
        public bool IsDefault { get; set; }
        public bool UseForInvoices { get; set; }
        public bool UseForQuotations { get; set; }
        public bool UseForPurchaseOrders { get; set; }
        public bool UseForReports { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public TemplateRecognitionResult Recognition { get; set; } = new TemplateRecognitionResult();
        public TemplateFieldMapping Mapping { get; set; } = new TemplateFieldMapping();
    }

    public sealed class TemplateRecognitionResult
    {
        public CompanyDocumentTemplateType DetectedType { get; set; } = CompanyDocumentTemplateType.Other;
        public int Confidence { get; set; }
        public bool LogoDetected { get; set; }
        public bool HeaderDetected { get; set; }
        public bool FooterDetected { get; set; }
        public bool AddressDetected { get; set; }
        public bool TaxFieldsDetected { get; set; }
        public bool BankDetailsDetected { get; set; }
        public bool TermsDetected { get; set; }
        public bool SignatureAreaDetected { get; set; }
        public bool ItemTableDetected { get; set; }
        public string Summary { get; set; }
        public List<string> DetectedFields { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class TemplateFieldMapping
    {
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
