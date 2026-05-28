using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    internal static class RecentDocumentOpenService
    {
        private static readonly string[] PdfFieldPriority =
        {
            "pdfPath",
            "pdfUrl",
            "documentPath",
            "filePath",
            "attachmentUrl",
            "pdf",
            "receiptImagePath",
            "storedFilePath"
        };

        public static void OpenStoredFile(IWin32Window owner, string path, string title)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    MessageBox.Show(owner, "The linked file was not found. It may have been moved or deleted.", title ?? "Open document", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(title ?? BrandingService.WindowTitle("Documents"), "Opening document", ex);
            }
        }

        public static bool OpenPdf(IWin32Window owner, object record)
        {
            string path = ResolvePdfPath(record);
            return TryOpenPdfPath(path);
        }

        public static void OpenQuotationPdf(IWin32Window owner, int bidId)
        {
            try
            {
                TenderService service = new TenderService();
                TenderBid bid = service.GetByIdDetailed(bidId) ?? service.GetById(bidId);
                if (bid == null)
                {
                    MessageBox.Show(owner, "The selected quotation could not be found.", "Open quotation PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                OpenPdf(owner, bid);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Quotations"), "Opening quotation PDF", ex);
            }
        }

        public static void OpenInvoicePdf(IWin32Window owner, int invoiceId)
        {
            try
            {
                InvoiceService service = new InvoiceService();
                Invoice invoice = service.GetInvoiceById(invoiceId);
                if (invoice == null)
                {
                    MessageBox.Show(owner, "The selected invoice could not be found.", "Open invoice PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                OpenPdf(owner, invoice);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Invoices"), "Opening invoice PDF", ex);
            }
        }

        public static void OpenPaymentDocument(IWin32Window owner, int invoiceId)
        {
            if (invoiceId > 0)
                OpenInvoicePdf(owner, invoiceId);
        }

        public static void OpenPurchaseOrderPdf(IWin32Window owner, int poId)
        {
            try
            {
                PurchaseService service = new PurchaseService();
                PurchaseOrder po = service.GetById(poId);
                if (po == null)
                {
                    MessageBox.Show(owner, "The selected purchase order could not be found.", "Open purchase PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                OpenPdf(owner, po);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Purchases"), "Opening purchase order PDF", ex);
            }
        }

        public static void OpenJobReportPdf(IWin32Window owner, int jobId)
        {
            try
            {
                JobDetailDto detail = new JobService().GetJobDetail(jobId);
                if (detail == null || detail.Job == null)
                {
                    MessageBox.Show(owner, "The selected job could not be found.", "Open job report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                OpenPdf(owner, detail.Job);
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Opening job report", ex);
            }
        }

        public static void OpenKnownDocument(IWin32Window owner, string documentType, int recordId)
        {
            if (string.Equals(documentType, "Quotation", StringComparison.OrdinalIgnoreCase))
                OpenQuotationPdf(owner, recordId);
            else if (string.Equals(documentType, "Invoice", StringComparison.OrdinalIgnoreCase))
                OpenInvoicePdf(owner, recordId);
            else if (string.Equals(documentType, "PurchaseOrder", StringComparison.OrdinalIgnoreCase))
                OpenPurchaseOrderPdf(owner, recordId);
            else if (string.Equals(documentType, "Job", StringComparison.OrdinalIgnoreCase))
                OpenJobReportPdf(owner, recordId);
        }

        private static string ResolvePdfPath(object record)
        {
            if (record == null)
                return string.Empty;

            foreach (string fieldName in PdfFieldPriority)
            {
                string value = ReadStringMember(record, fieldName);
                if (LooksLikePdfReference(value))
                    return value.Trim();
            }

            string notes = ReadStringMember(record, "notes");
            string sourcePath = ExtractArchiveSourcePath(notes);
            return LooksLikePdfReference(sourcePath) ? sourcePath.Trim() : string.Empty;
        }

        private static string ReadStringMember(object record, string name)
        {
            if (record == null || string.IsNullOrWhiteSpace(name))
                return string.Empty;

            Type type = record.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.PropertyType == typeof(string))
                return property.GetValue(record, null) as string ?? string.Empty;

            FieldInfo field = type.GetField(name, flags);
            if (field != null && field.FieldType == typeof(string))
                return field.GetValue(record) as string ?? string.Empty;

            return string.Empty;
        }

        private static string ExtractArchiveSourcePath(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return string.Empty;

            Match match = Regex.Match(notes, @"Source\s+Path:\s*(?<path>.+?)(\s+\|\s+|$)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["path"].Value.Trim() : string.Empty;
        }

        private static bool LooksLikePdfReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)))
            {
                return trimmed.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(Path.GetExtension(trimmed), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryOpenPdfPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                string target = path.Trim();
                if (Uri.TryCreate(target, UriKind.Absolute, out Uri uri))
                {
                    if (string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                    {
                        target = uri.LocalPath;
                    }
                    else if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    {
                        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                        return true;
                    }
                }

                if (!File.Exists(target))
                    return false;

                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
