using System;
using System.IO;
using System.Net;
using System.Text;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DocumentTemplateRenderer
    {
        private readonly TemplateStorageService _storage = new TemplateStorageService();

        public CompanyDocumentTemplate GetDefaultTemplate(CompanyDocumentTemplateType type)
        {
            return _storage.GetDefault(type);
        }

        public string BuildTemplateBannerHtml(CompanyDocumentTemplateType type)
        {
            CompanyDocumentTemplate template = GetDefaultTemplate(type);
            if (template == null)
                return string.Empty;

            return "<div class='company-template-banner'>Using company template: "
                + WebUtility.HtmlEncode(template.TemplateName)
                + " (" + WebUtility.HtmlEncode(template.DocumentType.ToString()) + ")</div>";
        }

        public string BuildPreviewHtml(CompanyDocumentTemplate template)
        {
            if (template == null)
                return "<html><body>No template selected.</body></html>";

            var sb = new StringBuilder();
            sb.Append("<html><head><style>body{font-family:Segoe UI,Arial,sans-serif;color:#0f172a;background:#f8fafc;margin:24px}.page{background:#fff;border:1px solid #dbe4f0;border-radius:12px;padding:24px;max-width:820px}.pill{display:inline-block;background:#eff6ff;color:#1d4ed8;padding:6px 10px;border-radius:999px;font-size:12px;font-weight:700}.grid{display:grid;grid-template-columns:180px 1fr;gap:8px 14px;margin-top:20px}.warn{color:#b45309}.field{border:1px dashed #93c5fd;padding:8px;border-radius:8px;background:#f8fbff}.company-template-banner{padding:8px 10px;background:#eef2ff;color:#3730a3;border-radius:8px;margin-bottom:12px}</style></head><body><div class='page'>");
            sb.Append("<span class='pill'>").Append(WebUtility.HtmlEncode(template.RecognitionStatus)).Append("</span>");
            sb.Append("<h1>").Append(WebUtility.HtmlEncode(template.TemplateName)).Append("</h1>");
            sb.Append("<p>Document type: <b>").Append(WebUtility.HtmlEncode(template.DocumentType.ToString())).Append("</b> | Default: <b>").Append(template.IsDefault ? "Yes" : "No").Append("</b></p>");
            sb.Append("<p>").Append(WebUtility.HtmlEncode(template.Recognition?.Summary ?? string.Empty)).Append("</p>");
            sb.Append("<div class='grid'>");
            foreach (var kv in template.Mapping?.Fields ?? new System.Collections.Generic.Dictionary<string, string>())
                sb.Append("<div>").Append(WebUtility.HtmlEncode(kv.Key)).Append("</div><div class='field'>").Append(WebUtility.HtmlEncode(kv.Value)).Append("</div>");
            sb.Append("</div>");
            if (template.Recognition?.Warnings != null && template.Recognition.Warnings.Count > 0)
            {
                sb.Append("<h3>Warnings</h3>");
                foreach (string warning in template.Recognition.Warnings)
                    sb.Append("<p class='warn'>").Append(WebUtility.HtmlEncode(warning)).Append("</p>");
            }
            if (File.Exists(template.StoredFilePath))
                sb.Append("<p>Stored file: ").Append(WebUtility.HtmlEncode(template.StoredFilePath)).Append("</p>");
            sb.Append("</div></body></html>");
            return sb.ToString();
        }
    }
}
