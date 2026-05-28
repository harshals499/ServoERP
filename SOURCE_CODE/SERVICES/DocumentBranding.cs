using System;
using System.IO;
using System.Net;
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
.mse-official-header-logo-fallback{font-family:'Segoe UI',sans-serif;font-size:24px;font-weight:900;line-height:1;color:#dc2626;}
.mse-official-header-logo-fallback .mark{color:#1e3a8a;margin-right:8px;}
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
.meta-cell{text-align:center;vertical-align:middle;font-size:20px;font-weight:700;color:#f00;}
.subject-row td,.po-row td{font-size:17px;font-weight:700;}
.items td,.items th{font-size:14px;}
.items .num{text-align:right;}
.items .center{text-align:center;}
.items .desc{font-weight:400;}
.total-label{font-size:18px;font-weight:700;}
.total-value{text-align:right;font-size:17px;font-weight:700;}
.words{font-size:18px;font-weight:700;color:#f00;}
.footer-left{width:47%;font-size:15px;line-height:1.2;}
.footer-right{font-size:15px;line-height:1.2;}
.compliance{font-weight:700;}
.certification{font-size:14px;line-height:1.18;font-weight:400;}
.send-title{font-weight:700;font-size:16px;}
.signature{text-align:center;font-size:17px;line-height:1.25;padding:12px 6px;min-height:88px;}
.signature .small{font-size:12px;font-family:'Segoe UI',sans-serif;font-weight:400;}
.signature img{display:block;max-width:190px;max-height:70px;margin:8px auto 4px auto;object-fit:contain;}
.signature .blank-space{display:block;height:58px;}
.blank-row td{height:18px;}
.mse-official-header{margin-top:6px;margin-bottom:12px;border-bottom:0;padding-bottom:0;}
.company-template-banner{font-family:'Segoe UI',sans-serif;font-size:11px;font-weight:600;color:#1d4ed8;background:#eff6ff;border:1px solid #bfdbfe;border-radius:6px;padding:6px 8px;margin:0 0 8px 0;}
@media print{body{-webkit-print-color-adjust:exact;print-color-adjust:exact;}.page{max-width:none;}.print-frame{break-inside:avoid;page-break-inside:avoid;}}
";
        }

        public static string BuildOfficialHeaderHtml()
        {
            string imageDataUri = TryBuildImageDataUri(ResolveOfficialHeaderPath());
            string logoHtml = !string.IsNullOrWhiteSpace(imageDataUri)
                ? "<img src='" + imageDataUri + "' alt='Company invoice header' />"
                : "<div class='mse-official-header-logo-fallback'><span class='mark'>ERP</span>NEW CLIENT</div>";

            return "<div class='mse-official-header'>"
                + "<div class='mse-official-header-top'>"
                + "<div class='mse-official-header-logo'>" + logoHtml + "</div>"
                + "</div></div>";
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
            string html = "Shop Lic.No. &nbsp;&nbsp; : &nbsp;" + Html(FirstNonEmpty(shopLicense, DefaultShopLicense)) + "<br/>"
                + "P.F.No. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(FirstNonEmpty(pfNumber, DefaultPfNumber)) + "<br/>"
                + "ESIC Code No. : &nbsp;" + Html(FirstNonEmpty(esicNumber, DefaultEsicNumber)) + "<br/>"
                + "Prof. Tax No. &nbsp;&nbsp; : &nbsp;" + Html(FirstNonEmpty(profTax, DefaultProfTaxNumber)) + "<br/>"
                + "PAN CARD NO.: &nbsp;" + Html(FirstNonEmpty(panNumber, DefaultPanNumber)) + "<br/>"
                + "GST No. &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(FirstNonEmpty(gstNumber, DefaultGstNumber)) + "<br/>"
                + "MSME NO &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; : &nbsp;" + Html(FirstNonEmpty(msmeNumber, DefaultMsmeNumber));

            if (includeCertification)
                html += "<br/>I/We,hereby certify that my/our registration certificate under the Maharashtra Value Added Tax Act,2002 is in force on the date on which sale of goods specified in this tax invoice is made by me/us and that the transaction of sale covered by this tax invoice has been effected by me/us and it shall be accounted for in the turnover of sales while filing of return and the due tax, if any, payable on the sale has been paid or shall be paid.";

            return html;
        }

        public static string BuildCertificationTextHtml()
        {
            return "I/We,hereby certify that my/our registration certificate under the Maharashtra Value Added Tax Act,2002 is in force on the date on which sale of goods specified in this tax invoice is made by me/us and that the transaction of sale covered by this tax invoice has been effected by me/us and it shall be accounted for in the turnover of sales while filing of return and the due tax, if any, payable on the sale has been paid or shall be paid.";
        }

        public static string BuildSignatureHtml(string companyName)
        {
            string imageDataUri = TryBuildImageDataUri(AuthorizedSignaturePath);
            string signatureBody = !string.IsNullOrWhiteSpace(imageDataUri)
                ? "<img src='" + imageDataUri + "' alt='Authorised signature' />"
                : "<span class='blank-space'></span>";

            return "For " + Html(FirstNonEmpty(companyName, DefaultCompanyName))
                + "<br/>" + signatureBody
                + "<span class='small'>Authorised Signatory</span>";
        }

        private static string Html(string text)
        {
            return WebUtility.HtmlEncode(text ?? string.Empty);
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
