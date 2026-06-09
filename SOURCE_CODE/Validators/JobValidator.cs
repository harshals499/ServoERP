using System;
using FluentValidation;
using HVAC_Pro_Desktop.Models;

namespace ServoERP.Validators
{
    /// <summary>Validates job and service-call data before save.</summary>
    public sealed class JobValidator : AbstractValidator<Job>
    {
        /// <summary>Creates the job validation rules.</summary>
        public JobValidator()
        {
            RuleFor(job => job.ClientID)
                .GreaterThan(0).WithMessage("Select a client for the job.");

            RuleFor(job => job.SiteID)
                .GreaterThanOrEqualTo(0).WithMessage("Job site selection is not valid.");

            RuleFor(job => job.JobTitle)
                .NotEmpty().When(job => string.IsNullOrWhiteSpace(job.Title))
                .WithMessage("Job title is required.");

            RuleFor(job => job.ScheduledDate)
                .GreaterThan(DateTime.MinValue).WithMessage("Scheduled date is required.");

            RuleFor(job => job.EstimatedCost)
                .GreaterThanOrEqualTo(0).WithMessage("Estimated cost cannot be negative.");
        }
    }
}
