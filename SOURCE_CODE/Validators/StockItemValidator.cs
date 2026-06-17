using FluentValidation;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services.Validation;

namespace ServoERP.Validators
{
    /// <summary>Validates inventory item master data before save.</summary>
    public sealed class StockItemValidator : AbstractValidator<StockItem>
    {
        public StockItemValidator()
        {
            RuleFor(item => item.ItemName)
                .NotEmpty().WithMessage("Item name is required.")
                .MaximumLength(200).WithMessage("Item name must be 200 characters or less.");

            RuleFor(item => item.Category)
                .MaximumLength(100).WithMessage("Category must be 100 characters or less.");

            RuleFor(item => item.Unit)
                .NotEmpty().WithMessage("Unit is required.")
                .MaximumLength(30).WithMessage("Unit must be 30 characters or less.");

            RuleFor(item => item.CurrentStock)
                .GreaterThanOrEqualTo(0m).WithMessage("Current stock cannot be negative.");

            RuleFor(item => item.LastPurchaseRate)
                .GreaterThanOrEqualTo(0m).WithMessage("Purchase rate cannot be negative.")
                .LessThanOrEqualTo(GlobalValidationEngine.MaxReasonableMoney).WithMessage("Purchase rate is outside the supported range.");

            RuleFor(item => item.ReorderLevel)
                .GreaterThanOrEqualTo(0m).WithMessage("Reorder level cannot be negative.");
        }
    }
}
