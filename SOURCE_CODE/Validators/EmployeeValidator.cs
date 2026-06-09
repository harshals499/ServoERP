using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates employee master data before save.</summary>
    public sealed class EmployeeValidator : AbstractValidator<Employee>
    {
        /// <summary>Creates the employee validation rules.</summary>
        public EmployeeValidator()
        {
            RuleFor(employee => employee.Name)
                .NotEmpty().WithMessage("Employee name is required.")
                .MaximumLength(150).WithMessage("Employee name must be 150 characters or less.");

            RuleFor(employee => employee.EmployeeCode)
                .MaximumLength(50).WithMessage("Employee code must be 50 characters or less.");

            RuleFor(employee => employee.Phone)
                .Matches(@"^[0-9+\-\s]{10,20}$").When(employee => !string.IsNullOrWhiteSpace(employee.Phone))
                .WithMessage("Employee phone must be a valid Indian mobile or landline number.");

            RuleFor(employee => employee.BasicSalary)
                .GreaterThanOrEqualTo(0).WithMessage("Basic salary cannot be negative.");

            RuleFor(employee => employee.GrossSalary)
                .GreaterThanOrEqualTo(0).WithMessage("Gross salary cannot be negative.");
        }
    }
}
