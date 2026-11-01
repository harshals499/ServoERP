using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace HVAC_Pro_Desktop.Models
{
    public enum ImportDocumentType { Unknown, Quotation, SalesInvoice, PurchaseInvoice, DeliveryChallan, PurchaseOrder, Receipt, CreditNote, DebitNote }
    public enum ImportDirection { Unknown, Sales, Purchase }

    public sealed class DocumentImportDraft
    {
        [Category("1. Classification")] public ImportDocumentType DocumentType { get; set; }
        [Category("1. Classification")] public ImportDirection Direction { get; set; }
        [Category("2. Document")] public string DocumentNumber { get; set; }
        [Category("2. Document")] public string Subject { get; set; }
        [Category("2. Document"), TypeConverter(typeof(ImportDateConverter))] public DateTime? DocumentDate { get; set; }
        [Category("2. Document"), TypeConverter(typeof(ImportDateConverter))] public DateTime? DueDate { get; set; }
        [Category("3. Party")] public string PartyName { get; set; }
        [Category("3. Party")] public string GSTIN { get; set; }
        [Category("3. Party")] public string Address { get; set; }
        [Category("3. Party"), Description("Select an existing party using Match party. New parties must first be reviewed in the normal module.")] public int PartyId { get; set; }
        [Category("4. Totals (INR)")] public decimal? TaxableAmount { get; set; }
        [Category("4. Totals (INR)")] public decimal? CGST { get; set; }
        [Category("4. Totals (INR)")] public decimal? SGST { get; set; }
        [Category("4. Totals (INR)")] public decimal? IGST { get; set; }
        [Category("4. Totals (INR)")] public decimal? RoundOff { get; set; }
        [Category("4. Totals (INR)")] public decimal? Total { get; set; }
        [Category("5. References")] public string Reference { get; set; }
        [Category("5. References"), Description("Source information only. Does not create a payment.")] public string PaymentDetails { get; set; }
        [Category("6. Approval"), Description("Confirm document type, direction, party, every line and all totals against the original.")] public bool ReviewConfirmed { get; set; }
        [Browsable(false)] public BindingList<DocumentImportLine> Lines { get; set; } = new BindingList<DocumentImportLine>();
        [Browsable(false)] public List<ImportFieldEvidence> Evidence { get; set; } = new List<ImportFieldEvidence>();
        [Browsable(false)] public string SourceText { get; set; }
        [Browsable(false)] public string SourceLocation { get; set; }
        [Browsable(false)] public int FirstPage { get; set; }
        [Browsable(false)] public int LastPage { get; set; }
        [Browsable(false)] public string FileHash { get; set; }
        [Browsable(false)] public string SnapshotPath { get; set; }
        [Browsable(false)] public string OriginalFileName { get; set; }
        [Browsable(false)] public string PreviewPath { get; set; }
        [Browsable(false)] public List<string> PreviewPaths { get; set; } = new List<string>();
        [Browsable(false)] public string ExtractionStatus { get; set; }
        public override string ToString() => (DocumentNumber ?? "Review required") + " · " + (SourceLocation??("Pages " + FirstPage + "–" + LastPage)) + " · " + DocumentType;
    }
    public sealed class DocumentImportLine
    {
        public string Description { get; set; }
        public string HSN { get; set; }
        public decimal? Quantity { get; set; }
        public string Unit { get; set; }
        public decimal? Rate { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? GSTPercent { get; set; }
        public decimal? Taxable { get; set; }
        public decimal? Tax { get; set; }
        public int? ProductId { get; set; }
    }
    public sealed class ImportFieldEvidence
    {
        public string Field { get; set; }
        public string Confidence { get; set; }
        public int Page { get; set; }
        public string Source { get; set; }
    }
    public sealed class ImportReference
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string GSTIN { get; set; }
        public override string ToString() => Id + " · " + Name + " · " + GSTIN;
    }
    public sealed class ImportSavedRecord
    {
        public string Module { get; set; }
        public int RecordId { get; set; }
        public string DocumentNumber { get; set; }
        public string FilePath { get; set; }
        public DateTime ReviewedUtc { get; set; }
    }
    public sealed class ImportDateConverter : NullableConverter
    {
        public ImportDateConverter() : base(typeof(DateTime?)) { }
        public override object ConvertTo(ITypeDescriptorContext context,CultureInfo culture,object value,Type destinationType)
        {
            if(destinationType==typeof(string))return value==null?"":((DateTime)value).ToString("dd/MM/yyyy",CultureInfo.InvariantCulture);
            return base.ConvertTo(context,culture,value,destinationType);
        }
        public override object ConvertFrom(ITypeDescriptorContext context,CultureInfo culture,object value)
        {
            if(value is string) { var text=((string)value).Trim();if(text.Length==0)return null; DateTime date;if(DateTime.TryParseExact(text,"dd/MM/yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))return date;throw new FormatException("Enter the date as DD/MM/YYYY."); }
            return base.ConvertFrom(context,culture,value);
        }
    }
}
