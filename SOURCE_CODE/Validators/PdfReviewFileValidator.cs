using System;
using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    internal sealed class PdfReviewFileValidator : AbstractValidator<PdfReviewFile>
    {
        internal PdfReviewFileValidator()
        {
            RuleFor(file=>file.Ready).Equal(true).WithMessage("Only included, reviewed PDFs with a category and no unresolved issue can be imported.");
            RuleFor(file=>file.Hash).NotEmpty().Matches("^[a-f0-9]{64}$").WithMessage("Reopen this PDF to verify its contents before importing.");
            RuleFor(file=>file.SnapshotPath).NotEmpty().WithMessage("Open the PDF preview before importing.");
            RuleFor(file=>file.Category).Must(value=>value.HasValue&&Enum.IsDefined(typeof(PdfCategory),value.Value)).WithMessage("Choose one of the available document categories.");
            RuleFor(file=>file.FileName).NotEmpty().MaximumLength(255);
        }
    }
}
