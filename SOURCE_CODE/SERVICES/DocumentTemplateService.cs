using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class DocumentTemplateService
    {
        public const string QuotationTemplateFileName = "CompanyQuotationTemplate.pdf";
        private readonly CompanyTemplateManager _manager = new CompanyTemplateManager();

        public string TemplateFolder
        {
            get
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, "Templates");
            }
        }

        public string QuotationTemplatePath => Path.Combine(TemplateFolder, QuotationTemplateFileName);

        public bool HasQuotationTemplate => File.Exists(QuotationTemplatePath);

        public string UploadQuotationTemplate(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Selected company document was not found.", sourcePath);

            if (!string.Equals(Path.GetExtension(sourcePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Please upload a PDF company quotation document.");

            Directory.CreateDirectory(TemplateFolder);
            File.Copy(sourcePath, QuotationTemplatePath, true);
            CompanyDocumentTemplate template = _manager.UploadTemplate(sourcePath, CompanyDocumentTemplateType.Quotation);
            template.IsDefault = true;
            template.UseForQuotations = true;
            _manager.SaveTemplate(template);
            return QuotationTemplatePath;
        }

        public string CopyQuotationTemplateForDocument(string documentNumber, string destinationFolder)
        {
            CompanyDocumentTemplate template = _manager.GetDefault(CompanyDocumentTemplateType.Quotation);
            string source = template != null && File.Exists(template.StoredFilePath) ? template.StoredFilePath : QuotationTemplatePath;
            if (!File.Exists(source))
                return null;

            Directory.CreateDirectory(destinationFolder);
            string safeName = MakeSafeFileName(string.IsNullOrWhiteSpace(documentNumber) ? "document" : documentNumber);
            string destination = Path.Combine(destinationFolder, safeName + "-company-template" + Path.GetExtension(source));
            File.Copy(source, destination, true);
            return destination;
        }

        public void OpenTemplate()
        {
            CompanyDocumentTemplate template = _manager.GetDefault(CompanyDocumentTemplateType.Quotation);
            string source = template != null && File.Exists(template.StoredFilePath) ? template.StoredFilePath : QuotationTemplatePath;
            if (!File.Exists(source))
                throw new FileNotFoundException("No company quotation document has been uploaded yet.", source);

            Process.Start(new ProcessStartInfo(source) { UseShellExecute = true });
        }

        public static string UploadQuotationTemplateWithDialog(IWin32Window owner)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Upload company quotation PDF";
                dialog.Filter = "PDF documents (*.pdf)|*.pdf";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return null;

                return new DocumentTemplateService().UploadQuotationTemplate(dialog.FileName);
            }
        }

        private static string MakeSafeFileName(string value)
        {
            string safe = value;
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '-');
            return safe.Trim();
        }
    }
}
