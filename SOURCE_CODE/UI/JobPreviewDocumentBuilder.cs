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
                + "body{font-family:'Segoe UI',Arial,sans-serif;background:#edf3f9;color:#0f172a;margin:0;padding:22px;}"
                + ".page{max-width:880px;margin:0 auto;background:#fff;border:1px solid #d9e2ec;box-shadow:0 18px 40px rgba(15,23,42,.07);padding:22px 24px 26px;border-radius:22px;}"
                + ".hero{display:flex;justify-content:space-between;gap:16px;align-items:center;margin-top:10px;margin-bottom:14px;padding:12px 16px;border:1px solid #dbe5f0;border-radius:18px;background:linear-gradient(135deg,#f8fbff 0%,#eef6ff 52%,#ecfeff 100%);}"
                + ".hero-copy{max-width:58%;}"
                + ".eyebrow{font-size:10px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:#2563eb;margin-bottom:5px;}"
                + ".hero-title{font-size:22px;font-weight:800;line-height:1.05;color:#0f172a;margin:0 0 4px 0;}"
                + ".hero-subtitle{font-size:12.5px;line-height:1.35;color:#475569;}"
                + ".hero-panel{min-width:220px;max-width:250px;background:#fff;border:1px solid #d7e3ef;border-radius:16px;padding:11px 14px;box-shadow:0 8px 18px rgba(15,23,42,.04);}"
                + ".hero-kicker{font-size:9px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;color:#64748b;margin-bottom:7px;}"
                + ".status-chip{display:inline-block;padding:5px 10px;border-radius:999px;background:#e0f2fe;color:#075985;font-size:10.5px;font-weight:800;letter-spacing:.04em;text-transform:uppercase;margin-bottom:8px;}"
                + ".hero-kv{margin:4px 0;font-size:12px;color:#334155;}"
                + ".doc-grid{width:100%;border-collapse:separate;border-spacing:0;table-layout:fixed;margin-top:10px;}"
                + ".doc-grid td,.doc-grid th{border-right:1px solid #dbe5f0;border-bottom:1px solid #dbe5f0;padding:10px 12px;vertical-align:top;font-size:13px;line-height:1.35;text-align:left;}"
                + ".doc-grid th{font-size:11.5px;font-weight:800;background:#f8fbff;color:#334155;letter-spacing:.04em;text-transform:uppercase;}"
                + ".doc-grid tr:first-child td,.doc-grid tr:first-child th{border-top:1px solid #dbe5f0;}"
                + ".doc-grid td:first-child,.doc-grid th:first-child{border-left:1px solid #dbe5f0;}"
                + ".doc-grid tr:first-child td:first-child,.doc-grid tr:first-child th:first-child{border-top-left-radius:16px;}"
                + ".doc-grid tr:first-child td:last-child,.doc-grid tr:first-child th:last-child{border-top-right-radius:16px;}"
                + ".doc-grid tr:last-child td:first-child{border-bottom-left-radius:16px;}"
                + ".doc-grid tr:last-child td:last-child{border-bottom-right-radius:16px;}"
                + ".overview-left{width:56%;}.overview-right{width:44%;}"
                + ".section-title{font-size:14px;font-weight:800;margin:0 0 8px 0;color:#0f172a;}"
                + ".meta{margin:3px 0;font-size:12.5px;line-height:1.3;color:#334155;}"
                + ".status-card{border:1px solid #dbe5f0;background:linear-gradient(180deg,#ffffff 0%,#f8fbff 100%);padding:12px 14px;border-radius:16px;min-height:106px;}"
                + ".status-label{font-size:10px;letter-spacing:.14em;text-transform:uppercase;color:#64748b;margin-bottom:8px;font-weight:700;}"
                + ".status-value{font-size:20px;font-weight:800;color:#0f172a;margin-bottom:8px;line-height:1.05;}"
                + ".summary-line{margin:4px 0;font-size:12.5px;color:#334155;}"
                + ".notes-block{min-height:72px;white-space:normal;}"
                + ".sign{display:flex;justify-content:space-between;gap:28px;margin-top:26px;}"
                + ".line{margin:50px auto 8px;border-top:1px solid #94a3b8;max-width:220px;}"
                + ".footer{margin-top:18px;color:#64748b;font-size:11px;text-align:right;}"
                + "@media screen{body{padding:16px;background:#f5f7fb;}.page{max-width:840px;padding:18px 20px 22px;}.hero{padding:10px 12px;}.hero-title{font-size:20px;}.doc-grid td,.doc-grid th{font-size:12px;padding:8px 10px;}.status-value{font-size:18px;}}"
                + "</style></head><body><div class='page'>"
                + DocumentBranding.BuildOfficialHeaderHtml()
                + "<div class='hero'>"
                + "<div class='hero-copy'>"
                + "<div class='eyebrow'>Field Service Document</div>"
                + "<div class='hero-title'>Service Report</div>"
                + "<div class='hero-subtitle'>" + serviceTitle + " for " + Html(detail.Client == null ? null : detail.Client.CompanyName) + "</div>"
                + "</div>"
                + "<div class='hero-panel'>"
                + "<div class='hero-kicker'>Report Snapshot</div>"
                + "<div class='status-chip'>" + Html(status) + "</div>"
                + "<div class='hero-kv'><strong>Job Number:</strong> " + Html(job.JobNumber) + "</div>"
                + "<div class='hero-kv'><strong>Generated On:</strong> " + Html(generatedOn) + "</div>"
                + "<div class='hero-kv'><strong>Scheduled:</strong> " + Html(scheduled) + "</div>"
                + "</div>"
                + "</div>"
                + "<table class='doc-grid'><tr>"
                + "<td class='overview-left'>"
                + DocumentBranding.BuildFromBlockHtml(companyName, null, null, null, null, settings.PAN, settings.GSTIN, null, false)
                + "</td>"
                + "<td class='overview-right'>"
                + "<div class='section-title'>Work Order Summary</div>"
                + "<div class='meta'><strong>Service Title:</strong> " + serviceTitle + "</div>"
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
