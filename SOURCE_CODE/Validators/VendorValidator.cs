using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates supplier and service vendor master data before save.</summary>
    public sealed class VendorValidator : AbstractValidator<Vendor>
    {
        /// <summary>Creates the vendor validation rules.</summary>
        public VendorValidator()
        {
            RuleFor(vendor => vendor.VendorName)
                .NotEmpty().WithMessage("Vendor name is required.")
                .MaximumLength(200).WithMessage("Vendor name must be 200 characters or less.");

            RuleFor(vendor => vendor.Phone)
                .Matches(@"^[0-9+\-\s]{10,20}$").When(vendor => !string.IsNullOrWhiteSpace(vendor.Phone))
                .WithMessage("Vendor phone must be a valid Indian business contact number.");

            RuleFor(vendor => vendor.Email)
                .EmailAddress().When(vendor => !string.IsNullOrWhiteSpace(vendor.Email))
                .WithMessage("Vendor email address is not valid.");

            RuleFor(vendor => vendor.GSTNumber)
                .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")
                .When(vendor => !string.IsNullOrWhiteSpace(vendor.GSTNumber))
                .WithMessage("GST number must be a valid 15-character Indian GSTIN.");

            RuleFor(vendor => vendor.DefaultCreditDays)
                .GreaterThanOrEqualTo(0).WithMessage("Default credit days cannot be negative.");
        }
    }
}
