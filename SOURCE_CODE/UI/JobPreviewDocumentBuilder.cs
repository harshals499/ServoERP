using System;
using System.Linq;
using System.Text;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class JobPreviewDocumentBuilder
    {
        public static string BuildServiceReportHtml(JobDetailDto detail, IndiaCompanySettings settings)
        {
            detail = detail ?? new JobDetailDto();
            Job job = detail.Job ?? new Job();
            settings = settings ?? new IndiaCompanySettings();

            StringBuilder checklist = new StringBuilder();
            foreach (JobChecklistItem item in (detail.ChecklistItems ?? Enumerable.Empty<JobChecklistItem>()).OrderBy(i => i.SortOrder))
            {
                checklist.Append("<tr><td>")
                    .Append(item.IsCompleted ? "&#10003;" : "&#10007;")
                    .Append("</td><td>")
                    .Append(Html(item.ItemText))
                    .Append("</td><td>")
                    .Append(item.CompletedDate.HasValue ? Html(item.CompletedDate.Value.ToString("dd/MM/yyyy hh:mm tt")) : "-")
                    .Append("</td></tr>");
            }

            StringBuilder parts = new StringBuilder();
            foreach (JobPartUsed part in detail.PartsUsed ?? Enumerable.Empty<JobPartUsed>())
            {
                parts.Append("<tr><td>")
                    .Append(Html(part.ItemDescription))
                    .Append("</td><td>")
                    .Append(part.QuantityUsed.ToString("0.###"))
                    .Append("</td><td>")
                    .Append(Html(part.Unit))
                    .Append("</td><td>")
                    .Append(IndiaFormatHelper.FormatCurrency(part.TotalCost))
                    .Append("</td></tr>");
            }

            string status = FirstNonEmpty(job.PipelineStatus, job.Status, "Created");
            string scheduled = job.ScheduledDate == default(DateTime) ? "-" : IndiaFormatHelper.FormatDate(job.ScheduledDate);
            string generatedOn = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            decimal estimatedCost = detail.LabourCost + detail.PartsCost + detail.TravelCost;
            string companyName = ResolveCompanyName(settings);
            string serviceTitle = Html(FirstNonEmpty(job.JobTitle, job.Title, "Service Job"));
            string notesHtml = Html(job.Notes).Replace("\r\n", "<br/>").Replace("\n", "<br/>");

            return "<html><head><meta charset='utf-8'/><style>"
                + DocumentBranding.BuildOfficialHeaderCss()
                + DocumentBranding.BuildOfficialCompanyDetailsCss()
                + "body{font-family:'Times New Roman',serif;background:#eef3fb;color:#111827;margin:0;padding:28px;}"
                + ".page{max-width:860px;margin:0 auto;background:#fff;border:1px solid #d7deea;box-shadow:0 18px 42px rgba(15,23,42,.08);padding:28px 34px 34px;}"
                + ".document-frame{border:1px solid #000;margin-top:10px;}"
                + ".doc-title{border-bottom:1px solid #000;text-align:center;font-size:18px;font-weight:700;padding:5px 0;}"
                + ".doc-grid{width:100%;border-collapse:collapse;table-layout:fixed;}"
                + ".doc-grid td,.doc-grid th{border:1px solid #000;padding:6px 8px;vertical-align:top;font-size:13px;line-height:1.2;text-align:left;}"
                + ".doc-grid th{text-align:center;font-weight:700;background:#f8fafc;}"
                + ".overview-left{width:56%;}.overview-right{width:44%;}"
                + ".section-title{font-size:15px;font-weight:700;margin:0 0 8px 0;}"
                + ".meta{margin:4px 0;font-size:13px;line-height:1.2;}"
                + ".status-card{border:1px solid #cbd5e1;background:#f8fafc;padding:10px 12px;min-height:120px;}"
                + ".status-label{font-size:11px;letter-spacing:.12em;text-transform:uppercase;color:#64748b;margin-bottom:8px;}"
                + ".status-value{font-size:22px;font-weight:700;color:#0f172a;margin-bottom:8px;}"
                + ".summary-line{margin:4px 0;font-size:13px;}"
                + ".notes-block{min-height:62px;white-space:normal;}"
                + ".sign{display:flex;justify-content:space-between;gap:28px;margin-top:22px;}"
                + ".sign-box{flex:1;text-align:center;}"
                + ".line{margin:48px auto 6px;border-top:1px solid #666;max-width:220px;}"
                + ".footer{margin-top:18px;color:#475569;font-size:11px;text-align:right;}"
                + "@media screen{body{padding:18px;background:#f5f7fb;}.page{max-width:820px;padding:22px 24px 26px;}.doc-grid td,.doc-grid th{font-size:12px;padding:5px 6px;}.status-value{font-size:19px;}}"
                + "</style></head><body><div class='page'>"
                + DocumentBranding.BuildOfficialHeaderHtml()
                + "<div class='document-frame'>"
                + "<div class='doc-title'>Service Report Preview</div>"
                + "<table class='doc-grid'><tr>"
                + "<td class='overview-left'>"
                + DocumentBranding.BuildFromBlockHtml(companyName, null, null, null, null, settings.PAN, settings.GSTIN, null, false)
                + "</td>"
                + "<td class='overview-right'>"
                + "<div class='section-title'>" + serviceTitle + "</div>"
                + "<div class='meta'><strong>Job Number:</strong> " + Html(job.JobNumber) + "</div>"
                + "<div class='meta'><strong>Generated On:</strong> " + Html(generatedOn) + "</div>"
                + "<div class='meta'><strong>Scheduled:</strong> " + Html(scheduled) + "</div>"
                + "<div class='meta'><strong>Client:</strong> " + Html(detail.Client == null ? null : detail.Client.CompanyName) + "</div>"
                + "<div class='meta'><strong>Site:</strong> " + Html(detail.Site == null ? null : detail.Site.SiteName) + "</div>"
                + "</td></tr>"
                + "<tr><td><div class='section-title'>Job Snapshot</div>"
                + "<div class='meta'><strong>Client:</strong> " + Html(detail.Client == null ? null : detail.Client.CompanyName) + "</div>"
                + "<div class='meta'><strong>Site:</strong> " + Html(detail.Site == null ? null : detail.Site.SiteName) + "</div>"
                + "<div class='meta'><strong>Contract:</strong> " + Html(detail.Contract != null ? ("AMC-" + detail.Contract.ContractID) : "-") + "</div>"
                + "<div class='meta'><strong>Technician:</strong> " + Html(detail.Technician == null ? "Unassigned" : detail.Technician.Name) + "</div>"
                + "<div class='meta'><strong>Priority:</strong> " + Html(job.Priority) + "</div>"
                + "<div class='meta'><strong>Type:</strong> " + Html(job.JobType) + "</div></td>"
                + "<td><div class='status-card'><div class='status-label'>Status</div><div class='status-value'>" + Html(status) + "</div>"
                + "<div class='summary-line'><strong>Quoted Revenue:</strong> " + IndiaFormatHelper.FormatCurrency(job.QuotedRevenue) + "</div>"
                + "<div class='summary-line'><strong>Estimated Cost:</strong> " + IndiaFormatHelper.FormatCurrency(estimatedCost) + "</div>"
                + "<div class='summary-line'><strong>Estimated Margin:</strong> " + detail.EstimatedMarginPct.ToString("0.0") + "%</div></div></td></tr></table>"
                + "<table class='doc-grid'><tr><th style='width:64px;'>Status</th><th>Checklist</th><th style='width:170px;'>Completion Time</th></tr>"
                + (checklist.Length == 0 ? "<tr><td colspan='3'>No checklist items recorded.</td></tr>" : checklist.ToString())
                + "</table>"
                + "<table class='doc-grid'><tr><th>Item</th><th style='width:84px;'>Qty</th><th style='width:90px;'>Unit</th><th style='width:140px;'>Cost</th></tr>"
                + (parts.Length == 0 ? "<tr><td colspan='4'>No parts recorded.</td></tr>" : parts.ToString())
                + "</table>"
                + "<table class='doc-grid'><tr><td style='width:35%;'><strong>Total Parts Cost</strong></td><td>" + IndiaFormatHelper.FormatCurrency(detail.PartsCost) + "</td></tr>"
                + "<tr><td><strong>Labour Cost</strong></td><td>" + IndiaFormatHelper.FormatCurrency(detail.LabourCost) + "</td></tr>"
                + "<tr><td><strong>Travel Cost</strong></td><td>" + IndiaFormatHelper.FormatCurrency(detail.TravelCost) + "</td></tr>"
                + "<tr><td><strong>Notes</strong></td><td class='notes-block'>" + notesHtml + "</td></tr></table>"
                + "<div class='sign'><div><div class='line'></div><div class='meta'>Technician signature</div></div><div><div class='line'></div><div class='meta'>Client signature</div></div></div>"
                + "</div>"
                + "<div class='footer'>Generated by " + Html(companyName) + " | " + Html(generatedOn) + "</div>"
                + "</div></body></html>";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static string Html(string value)
        {
            return System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "-" : value.Trim());
        }

        private static string ResolveCompanyName(IndiaCompanySettings settings)
        {
            string configured = settings == null ? string.Empty : settings.CompanyName;
            if (string.IsNullOrWhiteSpace(configured) || string.Equals(configured.Trim(), BrandingService.AppName, StringComparison.OrdinalIgnoreCase))
                return DocumentBranding.DefaultCompanyName;

            return configured.Trim();
        }
    }
}
