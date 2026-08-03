using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public static class DocumentBranding
    {
        public const string OfficialHeaderFileName = "official_invoice_header.png";
        public const string PreferredLetterheadPath = @"C:\Users\Administrator\Pictures\LETTERHEAD.png";
        public const string AuthorizedSignaturePath = @"C:\HVAC_PRO_MSE\Resources\Branding\authorized_signature.png";
        public const string DefaultCompanyName = "Madhusuman Enterprises";
        public const string DefaultShopLicense = "12612200000003717626.";
        public const string DefaultPfNumber = "TH/THA/0205548/000/01/25.";
        public const string DefaultEsicNumber = "34000284380001001.";
        public const string DefaultProfTaxNumber = "99752039470P.";
        public const string DefaultPanNumber = "AMTPS9540G";
        public const string DefaultGstNumber = "27AMTPS9540G1ZA";
        public const string DefaultMsmeNumber = "";

        public static string BuildOfficialHeaderCss()
        {
            return @"
.mse-official-header{padding:0;margin:0 0 18px 0;text-align:center;}
.mse-official-header-top{display:block;}
.mse-official-header-logo{display:flex;align-items:center;justify-content:center;}
.mse-official-header-logo img{display:block;width:100%;max-width:760px;height:auto;}
.mse-official-header-logo-fallback{display:inline-flex;flex-direction:column;align-items:center;justify-content:center;padding:12px 18px;font-family:'Segoe UI',sans-serif;line-height:1.05;color:#1f2937;}
.mse-official-header-logo-fallback .brand-row{display:flex;align-items:center;justify-content:center;gap:12px;}
.mse-official-header-logo-fallback .mark{font-size:30px;font-weight:900;color:#1e3a8a;letter-spacing:.02em;}
.mse-official-header-logo-fallback .company{font-size:30px;font-weight:900;color:#dc2626;letter-spacing:.04em;text-transform:uppercase;}
.mse-official-header-logo-fallback .tagline{margin-top:6px;font-size:10px;font-weight:700;color:#334155;letter-spacing:.08em;text-transform:uppercase;}
.mse-official-header-logo-fallback .contact{margin-top:6px;font-size:10px;font-weight:600;color:#475569;}
@media print{.mse-official-header{break-inside:avoid;page-break-inside:avoid;}}
";
        }

        public static string BuildOfficialPrintCss()
        {
            return @"
@page{size:A4;margin:12mm;}
body{font-family:'Times New Roman',serif;color:#000;margin:0;background:#fff;}
.page{width:100%;max-width:760px;margin:0 auto;background:#fff;}
.print-frame{border:2px solid #000;margin-top:22px;}
.doc-title{text-align:center;font-size:20px;font-weight:700;line-height:1.1;border-bottom:2px solid #000;padding:3px 0;}
.doc-grid{width:100%;border-collapse:collapse;}
.doc-grid td,.doc-grid th{border:1px solid #000;padding:3px 4px;vertical-align:top;font-size:15px;line-height:1.18;}
.doc-grid th{font-weight:700;text-align:center;}
.client-cell{width:47%;font-size:17px;font-weight:700;line-height:1.28;}
.meta-cell{vertical-align:middle;font-size:20px;font-weight:700;color:#000;}
.invoice-meta-line{display:table;width:100%;table-layout:fixed;}
.invoice-meta-label,.invoice-meta-value{display:table-cell;vertical-align:middle;padding:2px 0;}
.invoice-meta-label{width:42%;white-space:nowrap;text-align:left;}
.invoice-meta-value{width:58%;padding-left:8px;text-align:right;word-break:break-word;}
.subject-row td,.po-row td{font-size:17px;font-weight:700;}
.items td,.items th{font-size:14px;}
.items .num{text-align:right;}
.items .center{text-align:center;}
.items .desc{font-weight:400;}
.total-label{font-size:18px;font-weight:700;}
.total-value{text-align:right;font-size:17px;font-weight:700;}
.words{font-size:18px;font-weight:700;color:#f00;}
.total-summary{display:flex;align-items:center;justify-content:space-between;gap:12px;}
.words-inline{font-size:18px;font-weight:700;color:#f00;text-align:right;white-space:nowrap;}
.footer-left{width:47%;font-size:15px;line-height:1.2;}
.footer-right{font-size:15px;line-height:1.2;}
.compliance{font-weight:700;}
.certification{font-size:14px;line-height:1.18;font-weight:400;}
.send-title{font-weight:700;font-size:16px;}
.signature{text-align:center;font-size:17px;line-height:1.25;padding:12px 6px 4px 6px;min-height:168px;}
.signature .signature-body{display:flex;align-items:flex-end;justify-content:center;min-height:112px;margin-top:10px;}
.signature .small{display:block;font-size:12px;font-family:'Segoe UI',sans-serif;font-weight:400;margin-top:24px;}
.signature .signature-company{display:block;font-size:15px;margin-top:6px;}
.signature .signature-signed-by{display:block;font-size:14px;margin-top:4px;position:relative;left:-60px;}
.signature img{display:block;max-width:190px;max-height:70px;margin:0 auto;object-fit:contain;}
.signature .blank-space{display:block;height:112px;}
.blank-row td{height:18px;}
.mse-official-header{margin-top:6px;margin-bottom:12px;border-bottom:0;padding-bottom:0;}
.company-template-banner{font-family:'Segoe UI',sans-serif;font-size:11px;font-weight:600;color:#1d4ed8;background:#eff6ff;border:1px solid #bfdbfe;border-radius:6px;padding:6px 8px;margin:0 0 8px 0;}
@media print{body{-webkit-print-color-adjust:exact;print-color-adjust:exact;}.page{max-width:none;}.print-frame{break-inside:avoid;page-break-inside:avoid;}}
";
        }

        public static string BuildOfficialCompanyDetailsCss()
        {
            return @"
.mse-from-block{font-size:14px;line-height:1.25;color:#000;}
.mse-from-title{font-size:18px;font-weight:700;text-decoration:underline;margin:0 0 8px 0;}
.mse-from-company{font-size:18px;font-weight:700;margin:0 0 8px 0;}
.mse-detail-line{display:flex;align-items:flex-start;gap:8px;white-space:nowrap;}
.mse-detail-label{display:inline-block;min-width:150px;font-weight:700;flex:0 0 150px;}
.mse-detail-value{display:inline-block;font-weight:700;white-space:nowrap;}
";
        }

        public static string BuildOfficialHeaderHtml()
        {
            string imageDataUri = TryBuildImageDataUri(ResolveOfficialHeaderPath());
            string logoHtml = !string.IsNullOrWhiteSpace(imageDataUri)
                ? "<img src='" + imageDataUri + "' alt='Company invoice header' />"
                : "<div class='mse-official-header-logo-fallback'>"
                + "<div class='brand-row'><span class='mark'>MSE</span><span class='company'>" + Html(DefaultCompanyName) + "</span></div>"
                + "<div class='tagline'>Solution Providers For Process Chilling, Ventilation, Comfort Air Conditioning, Humidity Control, AMC, Utility Operation &amp; Maintenance</div>"
                + "<div class='contact'>Thane, Maharashtra | 9967604066 | msentp.info@gmail.com | www.hvacservicesindia.in</div>"
                + "</div>";

            return "<div class='mse-official-header'>"
                + "<div class='mse-official-header-top'>"
                + "<div class='mse-official-header-logo'>" + logoHtml + "</div>"
                + "</div></div>";
        }

        public static Tuple<string, string>[] GetOfficialDetailRows(
            string shopLicense,
            string pfNumber,
            string esicNumber,
            string profTax,
            string panNumber,
            string gstNumber,
            string msmeNumber,
            bool includeMsme)
        {
            var rows = new List<Tuple<string, string>>
            {
                Tuple.Create("Shop Lic.No", FirstNonEmpty(shopLicense, DefaultShopLicense)),
                Tuple.Create("P.F.No.", FirstNonEmpty(pfNumber, DefaultPfNumber)),
                Tuple.Create("ESIC Code No.", FirstNonEmpty(esicNumber, DefaultEsicNumber)),
                Tuple.Create("Prof. Tax No.", FirstNonEmpty(profTax, DefaultProfTaxNumber)),
                Tuple.Create("PAN CARD NO.", FirstNonEmpty(panNumber, DefaultPanNumber)),
                Tuple.Create("GST NUMBER", FirstNonEmpty(gstNumber, DefaultGstNumber))
            };

            string resolvedMsme = FirstNonEmpty(msmeNumber, DefaultMsmeNumber);
            if (includeMsme && !string.IsNullOrWhiteSpace(resolvedMsme))
                rows.Add(Tuple.Create("MSME NO.", resolvedMsme));

            return rows.ToArray();
        }

        public static string BuildFromBlockHtml(
            string companyName,
            string shopLicense,
            string pfNumber,
            string esicNumber,
            string profTax,
            string panNumber,
            string gstNumber,
            string msmeNumber,
            bool includeMsme)
        {
            var html = new StringBuilder();
            html.Append("<div class='mse-from-block'>");
            html.Append("<div class='mse-from-title'>From:</div>");
            html.Append("<div class='mse-from-company'>").Append(Html(FirstNonEmpty(companyName, DefaultCompanyName))).Append("</div>");
            html.Append(BuildDetailLinesHtml(GetOfficialDetailRows(shopLicense, pfNumber, esicNumber, profTax, panNumber, gstNumber, msmeNumber, includeMsme)));
            html.Append("</div>");
            return html.ToString();
        }

        public static string BuildComplianceBlockHtml(
            string shopLicense,
            string pfNumber,
            string esicNumber,
            string profTax,
            string panNumber,
            string gstNumber,
            string msmeNumber,
            bool includeCertification)
        {
            string html = BuildDetailLinesHtml(GetOfficialDetailRows(shopLicense, pfNumber, esicNumber, profTax, panNumber, gstNumber, msmeNumber, false), "<br/>");

            if (includeCertification)
                html += "<br/>I/We,hereby certify that my/our registration certificate under the Maharashtra Value Added Tax Act,2002 is in force on the date on which sale of goods specified in this tax invoice is made by me/us and that the transaction of sale covered by this tax invoice has been effected by me/us and it shall be accounted for in the turnover of sales while filing of return and the due tax, if any, payable on the sale has been paid or shall be paid.";

            return html;
        }

        public static string BuildCertificationTextHtml()
        {
            return "I/We,hereby certify that my/our registration certificate under the Maharashtra Value Added Tax Act,2002 is in force on the date on which sale of goods specified in this tax invoice is made by me/us and that the transaction of sale covered by this tax invoice has been effected by me/us and it shall be accounted for in the turnover of sales while filing of return and the due tax, if any, payable on the sale has been paid or shall be paid.";
        }

        public static string BuildSignatureHtml(string companyName, string authorisedSignatoryName = null)
        {
            string imageDataUri = TryBuildImageDataUri(AuthorizedSignaturePath);
            string signatureBody = !string.IsNullOrWhiteSpace(imageDataUri)
                ? "<img src='" + imageDataUri + "' alt='Authorised signature' />"
                : "<span class='blank-space'></span>";

            string signatoryLabel = string.IsNullOrWhiteSpace(authorisedSignatoryName)
                ? "Authorised Signatory"
                : "Authorised Signatory: " + Html(authorisedSignatoryName.Trim());

            return "<div class='signature-body'>" + signatureBody + "</div>"
                + "<span class='small'>" + signatoryLabel + "</span>"
                + "<span class='signature-company'>From " + Html(FirstNonEmpty(companyName, DefaultCompanyName)) + "</span>"
                + "<span class='signature-signed-by'>Signed by :</span>";
        }

        private static string Html(string text)
        {
            return WebUtility.HtmlEncode(text ?? string.Empty);
        }

        private static string BuildDetailLinesHtml(IEnumerable<Tuple<string, string>> rows, string separator = "")
        {
            var builder = new StringBuilder();
            bool first = true;
            foreach (Tuple<string, string> row in rows)
            {
                if (!first && !string.IsNullOrEmpty(separator))
                    builder.Append(separator);

                builder.Append("<div class='mse-detail-line'><span class='mse-detail-label'>")
                    .Append(Html(row.Item1))
                    .Append("</span><span class='mse-detail-value'>: ")
                    .Append(Html(row.Item2))
                    .Append("</span></div>");

                first = false;
            }

            return builder.ToString();
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

        private static string TryBuildImageDataUri(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return string.Empty;

                byte[] bytes = File.ReadAllBytes(path);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                string mime = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : ext == ".bmp" ? "image/bmp" : "image/png";
                return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveOfficialHeaderPath()
        {
            string templatePath = TryResolveDefaultLetterheadPath();
            if (!string.IsNullOrWhiteSpace(templatePath))
                return templatePath;

            if (File.Exists(PreferredLetterheadPath))
                return PreferredLetterheadPath;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string installedPath = Path.Combine(baseDir, "Resources", "Branding", OfficialHeaderFileName);
            if (File.Exists(installedPath))
                return installedPath;

            string rootPath = Path.Combine(@"C:\HVAC_PRO_MSE\Resources\Branding", OfficialHeaderFileName);
            if (File.Exists(rootPath))
                return rootPath;

            return Path.Combine(@"C:\HVAC_PRO_MSE\SOURCE_CODE\Resources\Branding", OfficialHeaderFileName);
        }

        private static string TryResolveDefaultLetterheadPath()
        {
            try
            {
                CompanyDocumentTemplate template = new TemplateStorageService().GetDefault(CompanyDocumentTemplateType.Letterhead);
                if (template != null && IsImageFile(template.StoredFilePath) && File.Exists(template.StoredFilePath))
                    return template.StoredFilePath;
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
        }
    }
}
