using System;
using System.Linq;
using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    public sealed class DocumentImportValidator : AbstractValidator<DocumentImportDraft>
    {
        public DocumentImportValidator()
        {
            RuleFor(x => x.DocumentType).Must(x => x == ImportDocumentType.Quotation || x == ImportDocumentType.SalesInvoice || x == ImportDocumentType.PurchaseInvoice).WithMessage("This type cannot yet be saved by document import. Choose quotation, sales invoice or purchase invoice.");
            RuleFor(x => x.Direction).Must(x => x != ImportDirection.Unknown).WithMessage("Confirm sales or purchase direction.");
            RuleFor(x => x).Must(x => x.DocumentType == ImportDocumentType.PurchaseInvoice ? x.Direction == ImportDirection.Purchase : x.Direction == ImportDirection.Sales).WithMessage("The selected type and direction disagree. Version one supports sales quotations only.");
            RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Subject).MaximumLength(200);
            RuleFor(x => x.DocumentDate).NotNull().Must(x => !x.HasValue || x.Value.Year >= 2000).WithMessage("Enter a valid document date (DD/MM/YYYY).");
            RuleFor(x => x.DueDate).NotNull().WithMessage("Review and enter a due date; no payment terms are inferred.");
            RuleFor(x => x).Must(x => !x.DueDate.HasValue || !x.DocumentDate.HasValue || x.DueDate.Value >= x.DocumentDate.Value).WithMessage("Due date cannot be before document date.");
            RuleFor(x => x).Must(x => x.Lines.Count<=500).WithMessage("Import supports at most 500 lines per document.");
            RuleFor(x => x.PartyId).GreaterThan(0).WithMessage("Match and select an existing customer or supplier.");
            RuleFor(x => x.ReviewConfirmed).Equal(true).WithMessage("Confirm that you reviewed all fields against the source.");
            RuleFor(x => x.Lines).NotEmpty();
            RuleForEach(x => x.Lines).SetValidator(new DocumentImportLineValidator());
            RuleFor(x => x.TaxableAmount).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.CGST).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.SGST).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.IGST).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.RoundOff).NotNull();
            RuleFor(x => x.Total).NotNull().GreaterThan(0).LessThan(999999999);
            RuleFor(x => x).Must(x => new[]{x.TaxableAmount,x.CGST,x.SGST,x.IGST,x.RoundOff,x.Total}.All(v=>!v.HasValue||decimal.Round(v.Value,2)==v.Value)).WithMessage("Money values must use at most two decimal places; no rounding is applied silently.");
            RuleFor(x => x).Must(x => x.IGST.GetValueOrDefault() == 0 || (x.CGST.GetValueOrDefault() == 0 && x.SGST.GetValueOrDefault() == 0)).WithMessage("IGST cannot be combined with CGST/SGST.");
            RuleFor(x => x).Must(x => Math.Abs(x.CGST.GetValueOrDefault() - x.SGST.GetValueOrDefault()) <= .01m).WithMessage("CGST and SGST do not agree.");
            RuleFor(x => x).Must(x => Close(x.TaxableAmount + x.CGST + x.SGST + x.IGST + x.RoundOff, x.Total)).WithMessage("Source totals conflict. Correct or reconcile the source; totals will not be silently changed.");
            RuleFor(x => x).Must(x => Close(x.Lines.Sum(l => l.Taxable), x.TaxableAmount)).WithMessage("Line taxable amounts do not match document taxable amount.");
            RuleFor(x => x).Must(x => Close(x.Lines.Sum(l => l.Tax), x.CGST + x.SGST + x.IGST)).WithMessage("Line taxes do not match document tax amounts.");
            RuleFor(x => x).Must(x => x.DocumentType == ImportDocumentType.SalesInvoice || (x.RoundOff == 0 && x.Lines.All(l => l.DiscountPercent == 0))).WithMessage("Quotation/purchase mappings currently require zero round-off and zero discounts. Use the normal module for other cases.");
            RuleFor(x => x).Must(x => x.DocumentType != ImportDocumentType.SalesInvoice || x.Lines.Select(l => l.GSTPercent).Distinct().Count() == 1).WithMessage("Mixed GST rates on sales invoices require the normal invoice workflow in version one.");
        }
        internal static bool Close(decimal? a, decimal? b) => a.HasValue && b.HasValue && a.Value == b.Value;
    }
    public sealed class DocumentImportLineValidator : AbstractValidator<DocumentImportLine>
    {
        public DocumentImportLineValidator()
        {
            RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            RuleFor(x => x.HSN).MaximumLength(20);
            RuleFor(x => x.Quantity).NotNull().GreaterThan(0).LessThan(100000000);
            RuleFor(x => x.Quantity).Must(x=>!x.HasValue||decimal.Round(x.Value,2)==x.Value).WithMessage("Version one supports quantities with up to two decimal places.");
            RuleFor(x => x).Must(x=>new[]{x.Rate,x.DiscountPercent,x.GSTPercent,x.Taxable,x.Tax}.All(v=>!v.HasValue||decimal.Round(v.Value,2)==v.Value)).WithMessage("Line values must use at most two decimal places.");
            RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
            RuleFor(x => x.Rate).NotNull().GreaterThanOrEqualTo(0).LessThan(999999999);
            RuleFor(x => x.DiscountPercent).NotNull().InclusiveBetween(0, 100);
            RuleFor(x => x.GSTPercent).NotNull().InclusiveBetween(0, 28);
            RuleFor(x => x.Taxable).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x.Tax).NotNull().GreaterThanOrEqualTo(0);
            RuleFor(x => x).Must(x => x.Quantity<100000000 && x.Rate<999999999 && x.DiscountPercent>=0 && x.DiscountPercent<=100 && DocumentImportValidator.Close(Math.Round((x.Quantity * x.Rate * (1 - x.DiscountPercent / 100)).GetValueOrDefault(), 2), x.Taxable)).WithMessage("Quantity, rate, discount and taxable amount disagree.");
            RuleFor(x => x).Must(x => x.Taxable<999999999 && x.GSTPercent>=0 && x.GSTPercent<=28 && DocumentImportValidator.Close(Math.Round((x.Taxable * x.GSTPercent / 100).GetValueOrDefault(), 2), x.Tax)).WithMessage("Line GST amount disagrees with its rate.");
        }
    }
}
