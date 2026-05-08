using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class TemplateStorageService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public string RootFolder => Path.Combine(GetApplicationBaseDirectory(), "CompanyTemplates");
        public string ManifestPath => Path.Combine(RootFolder, "templates.json");

        public List<CompanyDocumentTemplate> GetAll()
        {
            try
            {
                if (!File.Exists(ManifestPath))
                    return new List<CompanyDocumentTemplate>();
                return _serializer.Deserialize<List<CompanyDocumentTemplate>>(File.ReadAllText(ManifestPath)) ?? new List<CompanyDocumentTemplate>();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("TemplateStorageService.GetAll", ex);
                return new List<CompanyDocumentTemplate>();
            }
        }

        public CompanyDocumentTemplate GetDefault(CompanyDocumentTemplateType type)
        {
            return GetAll().FirstOrDefault(t => t.IsDefault && t.DocumentType == type);
        }

        public void Save(CompanyDocumentTemplate template)
        {
            if (template == null)
                return;

            Directory.CreateDirectory(RootFolder);
            List<CompanyDocumentTemplate> templates = GetAll();
            templates.RemoveAll(t => string.Equals(t.TemplateId, template.TemplateId, StringComparison.OrdinalIgnoreCase));
            if (template.IsDefault)
            {
                foreach (CompanyDocumentTemplate item in templates.Where(t => t.DocumentType == template.DocumentType))
                    item.IsDefault = false;
            }
            template.ModifiedAt = DateTime.Now;
            templates.Add(template);
            File.WriteAllText(ManifestPath, _serializer.Serialize(templates.OrderByDescending(t => t.ModifiedAt).ToList()));
        }

        public string StoreFile(string sourcePath, string templateId)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Selected company template was not found.", sourcePath);

            string folder = Path.Combine(RootFolder, templateId);
            Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, MakeSafeFileName(Path.GetFileName(sourcePath)));
            File.Copy(sourcePath, destination, true);
            return destination;
        }

        public void SetDefault(string templateId)
        {
            List<CompanyDocumentTemplate> templates = GetAll();
            CompanyDocumentTemplate selected = templates.FirstOrDefault(t => string.Equals(t.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
                return;
            foreach (CompanyDocumentTemplate item in templates.Where(t => t.DocumentType == selected.DocumentType))
                item.IsDefault = string.Equals(item.TemplateId, selected.TemplateId, StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(ManifestPath, _serializer.Serialize(templates));
        }

        private static string MakeSafeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "template" : value;
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '-');
            return safe.Trim();
        }

        private static string GetApplicationBaseDirectory()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrWhiteSpace(location))
                    return Path.GetDirectoryName(location);
            }
            catch
            {
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
