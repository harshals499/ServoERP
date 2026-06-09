using System;
using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates quotation data before save.</summary>
    public sealed class TenderBidValidator : AbstractValidator<TenderBid>
    {
        /// <summary>Creates the quotation validation rules.</summary>
        public TenderBidValidator()
        {
            RuleFor(quotation => quotation.TenderName)
                .NotEmpty().WithMessage("Quotation title is required.")
                .MaximumLength(200).WithMessage("Quotation title must be 200 characters or less.");

            RuleFor(quotation => quotation.ClientID)
                .GreaterThan(0).WithMessage("Select a client for the quotation.");

            RuleFor(quotation => quotation.SiteID)
                .GreaterThanOrEqualTo(0).WithMessage("Site selection is not valid.");

            RuleFor(quotation => quotation.DueDate)
                .GreaterThan(DateTime.MinValue).WithMessage("Quotation due date is required.");

            RuleFor(quotation => quotation.BidValue)
                .GreaterThanOrEqualTo(0).WithMessage("Quotation value cannot be negative.");

            RuleForEach(quotation => quotation.LineItems)
                .SetValidator(new TenderBidLineItemValidator());
        }
    }

    /// <summary>Validates quotation line item data before save.</summary>
    public sealed class TenderBidLineItemValidator : AbstractValidator<TenderBidLineItem>
    {
        /// <summary>Creates quotation line item validation rules.</summary>
        public TenderBidLineItemValidator()
        {
            RuleFor(item => item.ItemDescription)
                .NotEmpty().WithMessage("Quotation line item description is required.");

            RuleFor(item => item.Quantity)
                .GreaterThan(0).WithMessage("Quotation line item quantity must be greater than zero.");

            RuleFor(item => item.SellPricePerUnit)
                .GreaterThanOrEqualTo(0).WithMessage("Quotation line item sell price cannot be negative.");

            RuleFor(item => item.GSTRatePct)
                .InclusiveBetween(0, 28).WithMessage("GST rate must be between 0% and 28%.");
        }
    }
}
