using System;
using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates payment receipt data before save.</summary>
    public sealed class PaymentValidator : AbstractValidator<Payment>
    {
        /// <summary>Creates the payment validation rules.</summary>
        public PaymentValidator()
        {
            RuleFor(payment => payment.InvoiceID)
                .GreaterThan(0).WithMessage("Select an invoice for the payment.");

            RuleFor(payment => payment.ClientID)
                .GreaterThan(0).WithMessage("Select a client for the payment.");

            RuleFor(payment => payment.AmountPaid)
                .GreaterThan(0).WithMessage("Payment amount must be greater than zero.");

            RuleFor(payment => payment.PaymentDate)
                .GreaterThan(DateTime.MinValue).WithMessage("Payment date is required.");

            RuleFor(payment => payment.PaymentMode)
                .NotEmpty().WithMessage("Payment mode is required.");
        }
    }
}
