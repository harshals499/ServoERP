using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates client master data before save.</summary>
    public sealed class B2BClientValidator : AbstractValidator<B2BClient>
    {
        /// <summary>Creates the client validation rules.</summary>
        public B2BClientValidator()
        {
            RuleFor(client => client.CompanyName)
                .NotEmpty().WithMessage("Client company name is required.")
                .MaximumLength(200).WithMessage("Client company name must be 200 characters or less.");

            RuleFor(client => client.Phone)
                .Matches(@"^[0-9+\-\s]{10,20}$").When(client => !string.IsNullOrWhiteSpace(client.Phone))
                .WithMessage("Client phone must be a valid Indian business contact number.");

            RuleFor(client => client.Email)
                .EmailAddress().When(client => !string.IsNullOrWhiteSpace(client.Email))
                .WithMessage("Client email address is not valid.");

            RuleFor(client => client.GSTNumber)
                .Matches(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")
                .When(client => !string.IsNullOrWhiteSpace(client.GSTNumber))
                .WithMessage("GST number must be a valid 15-character Indian GSTIN.");

            RuleFor(client => client.PaymentTermsDays)
                .GreaterThanOrEqualTo(0).WithMessage("Payment terms cannot be negative.");

            RuleFor(client => client.CreditLimit)
                .GreaterThanOrEqualTo(0).WithMessage("Credit limit cannot be negative.");
        }
    }
}
