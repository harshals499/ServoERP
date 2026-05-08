using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class CompanyTemplateManager
    {
        private readonly TemplateStorageService _storage = new TemplateStorageService();
        private readonly TemplateUploadService _upload = new TemplateUploadService();

        public List<CompanyDocumentTemplate> GetTemplates()
        {
            return _storage.GetAll();
        }

        public CompanyDocumentTemplate UploadTemplate(string path, CompanyDocumentTemplateType type)
        {
            return _upload.Upload(path, type);
        }

        public void SaveTemplate(CompanyDocumentTemplate template)
        {
            _storage.Save(template);
        }

        public void SetDefault(string templateId)
        {
            _storage.SetDefault(templateId);
        }

        public CompanyDocumentTemplate GetDefault(CompanyDocumentTemplateType type)
        {
            return _storage.GetDefault(type);
        }
    }
}
