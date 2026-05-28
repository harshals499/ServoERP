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

            if (IsImageFile(template.StoredFilePath))
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
            sb.Append("<html><head><style>body{font-family:Segoe UI,Arial,sans-serif;color:#0f172a;background:#f8fafc;margin:10px;zoom:75%}.page{background:#fff;border:1px solid #dbe4f0;padding:16px;width:100%}.pill{display:inline-block;background:#eff6ff;color:#1d4ed8;padding:5px 9px;font-size:12px;font-weight:700}h1{font-size:15px;line-height:1.3;margin:14px 0 10px;word-wrap:break-word}.meta,.summary{font-size:13px;line-height:1.45}.fields{margin-top:16px}.field-row{margin-bottom:10px}.field-name{display:block;font-size:13px;font-weight:600;color:#334155;margin-bottom:3px;word-wrap:break-word}.field{display:block;border:1px dashed #93c5fd;padding:7px;background:#f8fbff;font-size:13px;word-wrap:break-word}.warn{color:#b45309}.company-template-banner{padding:8px 10px;background:#eef2ff;color:#3730a3;margin-bottom:12px}</style></head><body><div class='page'>");
            sb.Append("<span class='pill'>").Append(WebUtility.HtmlEncode(template.RecognitionStatus)).Append("</span>");
            sb.Append("<h1>Template preview</h1>");
            sb.Append("<p class='meta'>Document type: <b>").Append(WebUtility.HtmlEncode(template.DocumentType.ToString())).Append("</b> | Default: <b>").Append(template.IsDefault ? "Yes" : "No").Append("</b></p>");
            sb.Append("<p class='summary'>").Append(WebUtility.HtmlEncode(template.Recognition?.Summary ?? string.Empty)).Append("</p>");
            sb.Append("<div class='fields'>");
            foreach (var kv in template.Mapping?.Fields ?? new System.Collections.Generic.Dictionary<string, string>())
                sb.Append("<div class='field-row'><span class='field-name'>").Append(WebUtility.HtmlEncode(kv.Key)).Append("</span><span class='field'>").Append(WebUtility.HtmlEncode(kv.Value)).Append("</span></div>");
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

        private static bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
        }
    }
}
