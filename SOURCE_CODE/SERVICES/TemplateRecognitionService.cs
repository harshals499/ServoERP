using System;
using System.Collections.Generic;
using System.IO;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class TemplateRecognitionService
    {
        public TemplateRecognitionResult Recognize(string filePath, CompanyDocumentTemplateType selectedType)
        {
            string name = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            string ext = (Path.GetExtension(filePath) ?? string.Empty).ToLowerInvariant();
            CompanyDocumentTemplateType detected = selectedType == CompanyDocumentTemplateType.Other
                ? DetectTypeFromName(name)
                : selectedType;

            var result = new TemplateRecognitionResult
            {
                DetectedType = detected,
                Confidence = selectedType == CompanyDocumentTemplateType.Other ? 58 : 82,
                LogoDetected = IsVisual(ext) || ContainsAny(name, "logo", "letterhead", "invoice", "quotation"),
                HeaderDetected = ContainsAny(name, "letterhead", "invoice", "quotation", "po", "purchase", "contract"),
                FooterDetected = ContainsAny(name, "terms", "invoice", "quotation", "contract"),
                AddressDetected = true,
                TaxFieldsDetected = ContainsAny(name, "invoice", "gst", "tax", "quotation", "po"),
                BankDetailsDetected = ContainsAny(name, "invoice", "payment", "bank"),
                TermsDetected = ContainsAny(name, "terms", "contract", "quotation", "invoice"),
                SignatureAreaDetected = ContainsAny(name, "signed", "signature", "invoice", "quotation", "po"),
                ItemTableDetected = ContainsAny(name, "invoice", "quotation", "po", "excel", "xlsx", "xls")
            };

            result.DetectedFields.AddRange(BuildDefaultFields(detected));
            result.Summary = "Template recognized using file metadata and standard ERP field patterns. Manual mapping is available now; OCR/AI extraction can plug into this service later.";
            if (!result.LogoDetected) result.Warnings.Add("Logo was not confidently detected. Map or upload letterhead manually.");
            if (!result.ItemTableDetected && RequiresItemTable(detected)) result.Warnings.Add("Item table structure needs manual confirmation.");
            if (!result.TaxFieldsDetected && RequiresTax(detected)) result.Warnings.Add("GST/VAT fields need manual mapping.");
            return result;
        }

        private static CompanyDocumentTemplateType DetectTypeFromName(string value)
        {
            if (ContainsAny(value, "invoice", "tax invoice")) return CompanyDocumentTemplateType.Invoice;
            if (ContainsAny(value, "quotation", "quote", "tender")) return CompanyDocumentTemplateType.Quotation;
            if (ContainsAny(value, "purchase", "po")) return CompanyDocumentTemplateType.PurchaseOrder;
            if (ContainsAny(value, "delivery", "challan")) return CompanyDocumentTemplateType.DeliveryNote;
            if (ContainsAny(value, "letterhead", "header")) return CompanyDocumentTemplateType.Letterhead;
            if (ContainsAny(value, "contract", "amc")) return CompanyDocumentTemplateType.Contract;
            if (ContainsAny(value, "report")) return CompanyDocumentTemplateType.Report;
            if (ContainsAny(value, "terms", "conditions")) return CompanyDocumentTemplateType.TermsAndConditions;
            return CompanyDocumentTemplateType.Other;
        }

        private static IEnumerable<string> BuildDefaultFields(CompanyDocumentTemplateType type)
        {
            var fields = new List<string> { "CompanyName", "CompanyAddress", "Logo", "GSTNumber", "PANNumber", "DocumentNumber", "DocumentDate", "ClientName", "ClientAddress" };
            if (RequiresItemTable(type)) fields.AddRange(new[] { "ItemsTable", "Description", "Quantity", "Rate", "TaxableAmount", "GST", "Total" });
            if (RequiresTax(type)) fields.AddRange(new[] { "CGST", "SGST", "IGST", "GrandTotal", "AmountInWords" });
            fields.AddRange(new[] { "PaymentTerms", "TermsAndConditions", "SignatureArea", "Footer" });
            return fields;
        }

        private static bool RequiresItemTable(CompanyDocumentTemplateType type)
        {
            return type == CompanyDocumentTemplateType.Invoice || type == CompanyDocumentTemplateType.Quotation || type == CompanyDocumentTemplateType.PurchaseOrder || type == CompanyDocumentTemplateType.DeliveryNote;
        }

        private static bool RequiresTax(CompanyDocumentTemplateType type)
        {
            return type == CompanyDocumentTemplateType.Invoice || type == CompanyDocumentTemplateType.Quotation || type == CompanyDocumentTemplateType.PurchaseOrder;
        }

        private static bool IsVisual(string ext)
        {
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".pdf";
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            value = value ?? string.Empty;
            foreach (string needle in needles)
                if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}
