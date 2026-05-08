using System;
using System.IO;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class TemplateUploadService
    {
        private readonly TemplateRecognitionService _recognition = new TemplateRecognitionService();
        private readonly TemplateMappingService _mapping = new TemplateMappingService();
        private readonly TemplateStorageService _storage = new TemplateStorageService();

        public CompanyDocumentTemplate Upload(string sourcePath, CompanyDocumentTemplateType selectedType, string companyKey = "default")
        {
            if (!IsSupported(sourcePath))
                throw new InvalidOperationException("Upload PDF, Word, Excel, image, quotation, invoice, PO, or letterhead samples only.");

            string id = Guid.NewGuid().ToString("N");
            string stored = _storage.StoreFile(sourcePath, id);
            TemplateRecognitionResult recognition = _recognition.Recognize(stored, selectedType);
            var template = new CompanyDocumentTemplate
            {
                TemplateId = id,
                CompanyKey = string.IsNullOrWhiteSpace(companyKey) ? "default" : companyKey,
                TemplateName = Path.GetFileNameWithoutExtension(sourcePath),
                DocumentType = recognition.DetectedType,
                OriginalFileName = Path.GetFileName(sourcePath),
                StoredFilePath = stored,
                FileExtension = Path.GetExtension(sourcePath),
                RecognitionStatus = "Recognized",
                UploadedAt = DateTime.Now,
                ModifiedAt = DateTime.Now,
                Recognition = recognition,
                Mapping = _mapping.CreateDefaultMapping(recognition),
                UseForInvoices = recognition.DetectedType == CompanyDocumentTemplateType.Invoice,
                UseForQuotations = recognition.DetectedType == CompanyDocumentTemplateType.Quotation,
                UseForPurchaseOrders = recognition.DetectedType == CompanyDocumentTemplateType.PurchaseOrder,
                UseForReports = recognition.DetectedType == CompanyDocumentTemplateType.Report
            };

            if (_storage.GetDefault(template.DocumentType) == null)
                template.IsDefault = true;

            _storage.Save(template);
            return template;
        }

        private static bool IsSupported(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            string ext = (Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            return ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".xls" || ext == ".xlsx" || ext == ".csv" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
        }
    }
}
