using System;
using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates AMC contract data before save.</summary>
    public sealed class AMCContractValidator : AbstractValidator<AMCContract>
    {
        /// <summary>Creates the AMC validation rules.</summary>
        public AMCContractValidator()
        {
            RuleFor(contract => contract.ClientID)
                .GreaterThan(0).WithMessage("Select a client for the AMC.");

            RuleFor(contract => contract.SiteID)
                .GreaterThan(0).WithMessage("Select a site for the AMC.");

            RuleFor(contract => contract.StartDate)
                .GreaterThan(DateTime.MinValue).WithMessage("AMC start date is required.");

            RuleFor(contract => contract.EndDate)
                .GreaterThan(contract => contract.StartDate).WithMessage("AMC end date must be after the start date.");

            RuleFor(contract => contract.AnnualValue)
                .GreaterThanOrEqualTo(0).WithMessage("AMC annual value cannot be negative.");

            RuleFor(contract => contract.SLAResponseTimeHours)
                .GreaterThanOrEqualTo(0).WithMessage("SLA response time cannot be negative.");

            RuleFor(contract => contract.MaintenanceFrequency)
                .NotEmpty().WithMessage("Maintenance frequency is required.");
        }
    }
}
