using System;
using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Models
{
    internal enum PdfCategory { PurchaseOrder, Invoice, Quotation, Challan, Other, AmcContract, License, Insurance, TaxDocument, Warranty, Manual, Drawing, Certificate, ProformaInvoice, CreditNote, DebitNote, PaymentReceipt, WorkOrder, ServiceReport }

    internal static class PdfCategories
    {
        internal static string Label(PdfCategory value)
            => value == PdfCategory.PurchaseOrder ? "Purchase Order" :
               value == PdfCategory.AmcContract ? "AMC Contract" :
               value == PdfCategory.TaxDocument ? "Tax Document" :
               value == PdfCategory.ProformaInvoice ? "Proforma Invoice" :
               value == PdfCategory.CreditNote ? "Credit Note" :
               value == PdfCategory.DebitNote ? "Debit Note" :
               value == PdfCategory.PaymentReceipt ? "Payment Receipt" :
               value == PdfCategory.WorkOrder ? "Work Order" :
               value == PdfCategory.ServiceReport ? "Service Report" : value.ToString();
        internal static string Module(PdfCategory value)
            => value == PdfCategory.PurchaseOrder ? "Purchases" : value == PdfCategory.Invoice ? "Invoices" : value == PdfCategory.Quotation ? "Quotations" : "MasterData";
        internal static string DocumentType(PdfCategory value)
            => value == PdfCategory.Challan ? "Delivery Note" : Label(value);
        internal static string Destination(PdfCategory value)
            => Module(value) == "MasterData" ? "Master Data / Documents / " + DocumentType(value) : Module(value) + " / Imported PDFs";
        internal static IEnumerable<PdfCategory> All => Enum.GetValues(typeof(PdfCategory)).Cast<PdfCategory>()
                .OrderBy(value => value == PdfCategory.Other).ThenBy(Label);
    }

    internal sealed class PdfReviewFile
    {
        public bool Include { get; set; } = true;
        public string FileName => System.IO.Path.GetFileName(SourcePath);
        public string Folder { get; set; }
        public string Status => Imported ? "Imported" : !Include ? "Excluded" : !string.IsNullOrEmpty(Error) ? Error : Existing ? "Already imported" : Duplicate ? "Duplicate in batch" : Skipped ? "Skipped" : Category.HasValue ? "Categorised: " + PdfCategories.Label(Category.Value) : "To review";
        internal string SourcePath { get; set; }
        internal string SnapshotPath { get; set; }
        internal string Hash { get; set; }
        internal string Password { get; set; }
        internal string Error { get; set; }
        internal PdfCategory? Category { get; set; }
        internal bool Skipped { get; set; }
        internal bool Duplicate { get; set; }
        internal bool Existing { get; set; }
        internal bool Previewed { get; set; }
        internal bool Imported { get; set; }
        internal bool Ready => Include && !Imported && Previewed && Category.HasValue && !Skipped && !Duplicate && !Existing && string.IsNullOrEmpty(Error);
    }

    internal sealed class PdfReviewBatch
    {
        internal List<PdfReviewFile> Files { get; } = new List<PdfReviewFile>();
        internal int Ready => Files.Count(f => f.Ready);
        internal int Unresolved => Files.Count(f => f.Include && !f.Imported && !f.Ready);
        internal void RefreshDuplicates()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Files)
                file.Duplicate = file.Include && !file.Imported && file.Hash != null && !seen.Add(file.Hash);
        }
        internal void SaveChoice(PdfReviewFile file, PdfCategory category)
        {
            RefreshDuplicates();
            if (!Files.Contains(file) || !file.Include || file.Imported || !file.Previewed || file.Duplicate || file.Existing || !string.IsNullOrEmpty(file.Error))
                throw new InvalidOperationException("Open a readable, included PDF and resolve duplicates before saving its category.");
            file.Category = category;
            file.Skipped = false;
        }
        internal List<PdfReviewFile> ConfirmedFiles(bool acknowledgeUnresolved)
        {
            RefreshDuplicates();
            if (Unresolved > 0 && !acknowledgeUnresolved)
                throw new InvalidOperationException("Review the unresolved files or acknowledge leaving them out of this import.");
            var ready = Files.Where(f => f.Ready).ToList();
            if (ready.Count == 0) throw new InvalidOperationException("There are no categorised PDFs ready to import.");
            return ready;
        }
    }

    internal sealed class PdfLibraryRecord
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; }
        public string Category { get; set; }
        public string SourceFolder { get; set; }
        public DateTime ImportedUtc { get; set; }
        public string FilePath { get; set; }
        internal string FileHash { get; set; }
    }
}
