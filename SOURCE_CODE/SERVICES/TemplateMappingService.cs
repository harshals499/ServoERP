using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class TemplateMappingService
    {
        public TemplateFieldMapping CreateDefaultMapping(TemplateRecognitionResult recognition)
        {
            var mapping = new TemplateFieldMapping { UpdatedAt = DateTime.Now };
            foreach (string field in recognition?.DetectedFields ?? new List<string>())
                mapping.Fields[field] = "{{" + field + "}}";
            return mapping;
        }

        public void SetField(CompanyDocumentTemplate template, string fieldName, string placeholder)
        {
            if (template == null || string.IsNullOrWhiteSpace(fieldName))
                return;

            if (template.Mapping == null)
                template.Mapping = new TemplateFieldMapping();
            template.Mapping.Fields[fieldName.Trim()] = string.IsNullOrWhiteSpace(placeholder) ? "{{" + fieldName.Trim() + "}}" : placeholder.Trim();
            template.Mapping.UpdatedAt = DateTime.Now;
        }
    }
}
