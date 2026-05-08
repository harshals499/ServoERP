using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public enum LicensePlanType
    {
        Trial,
        Standard,
        Enterprise
    }

    public enum LicenseStatus
    {
        Missing,
        Active,
        Warning,
        Grace,
        Expired,
        Frozen,
        Tampered
    }

    public sealed class LicenseSnapshot
    {
        public string LicenseKey { get; set; }
        public LicensePlanType PlanType { get; set; }
        public string CompanyName { get; set; }
        public int MaxCompanies { get; set; }
        public int MaxDevices { get; set; }
        public int MaxUsers { get; set; }
        public DateTime IssueDateUtc { get; set; }
        public DateTime ExpiryDateUtc { get; set; }
        public DateTime? LastSuccessfulValidationUtc { get; set; }
        public DateTime? LastAppOpenUtc { get; set; }
        public DateTime? LastTrustedServerTimeUtc { get; set; }
        public int GracePeriodDays { get; set; }
        public LicenseStatus Status { get; set; }
        public string MachineFingerprintHash { get; set; }
        public string ActivatedDeviceId { get; set; }
        public int ActivatedDeviceCount { get; set; }
        public string SupportLevel { get; set; }
        public string PlanName { get; set; }
        public string BillingCycle { get; set; }
        public string Currency { get; set; }
        public decimal PriceAmount { get; set; }
        public decimal RenewalPriceAmount { get; set; }
        public bool IsLaunchOffer { get; set; }
        public string StatusMessage { get; set; }
        public List<string> EnabledModules { get; set; } = new List<string>();
    }

    public sealed class LicenseValidationResult
    {
        public bool Success { get; set; }
        public bool RequiresActivation { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsTampered { get; set; }
        public string Message { get; set; }
        public LicenseSnapshot Snapshot { get; set; }
    }

    public sealed class SignedLicenseEnvelope
    {
        public string Algorithm { get; set; }
        public string Payload { get; set; }
        public string Signature { get; set; }
    }

    public sealed class LicensePayload
    {
        public string LicenseKey { get; set; }
        public string PlanType { get; set; }
        public string CompanyName { get; set; }
        public int MaxCompanies { get; set; }
        public int MaxDevices { get; set; }
        public int MaxUsers { get; set; }
        public DateTime IssueDateUtc { get; set; }
        public DateTime ExpiryDateUtc { get; set; }
        public int GracePeriodDays { get; set; }
        public string MachineFingerprintHash { get; set; }
        public string ActivatedDeviceId { get; set; }
        public int ActivatedDeviceCount { get; set; }
        public string SupportLevel { get; set; }
        public string PlanName { get; set; }
        public string BillingCycle { get; set; }
        public string Currency { get; set; }
        public decimal PriceAmount { get; set; }
        public decimal RenewalPriceAmount { get; set; }
        public bool IsLaunchOffer { get; set; }
        public List<string> EnabledModules { get; set; } = new List<string>();
    }

    public sealed class LicenseActivationRequest
    {
        public string LicenseKey { get; set; }
        public string CompanyName { get; set; }
        public string MachineFingerprintHash { get; set; }
        public string DeviceName { get; set; }
    }
}
