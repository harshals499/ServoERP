using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class InvoiceTemplateService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private readonly InvoiceTemplateRepository _repo = new InvoiceTemplateRepository();

        public List<InvoiceTemplate> GetActiveTemplates()
        {
            return AppDataCache.GetOrCreate("invoice-templates:all", CacheTtl, _repo.GetAll);
        }

        public InvoiceTemplate GetByCode(string templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
                return null;

            foreach (var template in GetActiveTemplates())
            {
                if (string.Equals(template.TemplateCode, templateCode, StringComparison.OrdinalIgnoreCase))
                    return template;
            }

            return _repo.GetByCode(templateCode);
        }
    }
}
