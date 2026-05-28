using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class FormTemplateLibraryService
    {
        public const string DefaultRoot = @"C:\HVAC_PRO_MSE\research_downloads\field_service_forms";
        public const string DefaultZip = @"C:\HVAC_PRO_MSE\research_downloads\ServoERP_Field_Service_Form_Templates.zip";

        private const string MappingFileName = "ServoERP_Form_Template_Mapping.csv";

        public string RootFolder => ConfigService.Get("FieldServiceForms", "RootFolder", DefaultRoot);

        public string ZipPath => ConfigService.Get("FieldServiceForms", "ZipPath", DefaultZip);

        public bool IsAvailable => Directory.Exists(RootFolder) && File.Exists(MappingPath);

        private string MappingPath => Path.Combine(RootFolder, MappingFileName);

        public List<FieldServiceFormTemplate> GetTemplates()
        {
            if (!File.Exists(MappingPath))
                return new List<FieldServiceFormTemplate>();

            List<string[]> rows = ReadCsv(MappingPath);
            if (rows.Count <= 1)
                return new List<FieldServiceFormTemplate>();

            string[] header = rows[0];
            return rows.Skip(1)
                .Select(row => MapTemplate(header, row))
                .Where(template => template != null)
                .OrderBy(template => template.Trade)
                .ThenBy(template => template.FormName)
                .ToList();
        }

        public List<FieldServiceFormTemplate> Search(string query, string trade = null, string module = null)
        {
            string q = (query ?? string.Empty).Trim();
            return GetTemplates()
                .Where(template => string.IsNullOrWhiteSpace(trade) || string.Equals(template.Trade, trade, StringComparison.OrdinalIgnoreCase))
                .Where(template => string.IsNullOrWhiteSpace(module) || (template.RecommendedServoErpModule ?? string.Empty).IndexOf(module, StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(template => string.IsNullOrWhiteSpace(q) ||
                    (template.FormName ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (template.Trade ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (template.RecommendedServoErpModule ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (template.RecommendedFields ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        public Dictionary<string, int> CountByTrade()
        {
            return GetTemplates()
                .GroupBy(template => template.Trade ?? "Unknown", StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        }

        public string BuildSummary()
        {
            List<FieldServiceFormTemplate> templates = GetTemplates();
            int xlsx = templates.Count(template => string.Equals(Path.GetExtension(template.FullPath), ".xlsx", StringComparison.OrdinalIgnoreCase));
            int docx = templates.Count(template => string.Equals(Path.GetExtension(template.FullPath), ".docx", StringComparison.OrdinalIgnoreCase));
            return templates.Count + " templates available (" + xlsx + " XLSX, " + docx + " DOCX).";
        }

        public string CopyTemplateToWorkingFolder(FieldServiceFormTemplate template, string targetFolder)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.FullPath) || !File.Exists(template.FullPath))
                throw new FileNotFoundException("Template file was not found.", template?.FullPath);

            if (string.IsNullOrWhiteSpace(targetFolder))
                targetFolder = Path.Combine(@"C:\HVAC_PRO_MSE", "FORM_TEMPLATE_WORKING_COPIES");

            Directory.CreateDirectory(targetFolder);
            string target = Path.Combine(targetFolder, Path.GetFileName(template.FullPath));
            File.Copy(template.FullPath, target, true);
            return target;
        }

        public string ResolveReadmePath()
        {
            string path = Path.Combine(RootFolder, "README_INDEX.md");
            return File.Exists(path) ? path : null;
        }

        private FieldServiceFormTemplate MapTemplate(string[] header, string[] row)
        {
            string category = Value(header, row, "Category");
            string fileName = Value(header, row, "FileName");
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(fileName))
                return null;

            string fullPath = Path.Combine(RootFolder, category, fileName);
            return new FieldServiceFormTemplate
            {
                Category = category,
                Trade = Value(header, row, "Trade"),
                FormName = Value(header, row, "FormName"),
                FileName = fileName,
                SourceType = Value(header, row, "SourceType"),
                SourceUrl = Value(header, row, "SourceURL"),
                RecommendedServoErpModule = Value(header, row, "RecommendedServoERPModule"),
                RecommendedFields = Value(header, row, "RecommendedFields"),
                MobileSuitable = Yes(Value(header, row, "MobileSuitableYesNo")),
                RequiresCustomerSignature = Yes(Value(header, row, "RequiresCustomerSignatureYesNo")),
                RequiresTechnicianSignature = Yes(Value(header, row, "RequiresTechnicianSignatureYesNo")),
                RequiresPhotoUpload = Yes(Value(header, row, "RequiresPhotoUploadYesNo")),
                RequiresReadings = Yes(Value(header, row, "RequiresReadingsYesNo")),
                ComplianceCritical = Yes(Value(header, row, "ComplianceCriticalYesNo")),
                FullPath = fullPath
            };
        }

        private static string Value(string[] header, string[] row, string key)
        {
            int index = Array.FindIndex(header, value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index < row.Length ? row[index] : string.Empty;
        }

        private static bool Yes(string value)
        {
            return string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string[]> ReadCsv(string path)
        {
            var rows = new List<string[]>();
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                rows.Add(ParseCsvLine(line).ToArray());
            return rows;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < (line ?? string.Empty).Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            values.Add(current.ToString());
            return values;
        }
    }
}
