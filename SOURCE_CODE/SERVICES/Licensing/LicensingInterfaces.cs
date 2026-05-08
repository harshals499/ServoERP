using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public interface ILicenseService
    {
        LicenseValidationResult ValidateCurrentLicense();
        LicenseValidationResult ActivateTrial(string companyName);
        LicenseValidationResult ActivateOffline(string licenseFilePath);
        LicenseValidationResult ActivateOnline(LicenseActivationRequest request);
        LicenseSnapshot GetCurrentSnapshot();
    }

    public interface IOnlineLicenseClient
    {
        LicenseValidationResult Activate(LicenseActivationRequest request);
        LicenseValidationResult Validate(LicenseSnapshot snapshot);
    }

    public interface IOfflineLicenseValidator
    {
        LicenseValidationResult ValidateLicenseFile(string licenseFilePath, string expectedMachineFingerprintHash);
    }

    public interface IDeviceFingerprintService
    {
        string GetFingerprintHash();
        string GetDeviceName();
    }

    public interface ILicenseCacheStore
    {
        LicenseSnapshot Load();
        void Save(LicenseSnapshot snapshot);
        void Clear();
    }

    public interface ILicenseEnforcementService
    {
        bool IsFrozen();
        bool IsModuleEnabled(string moduleKey);
        bool CanPerform(string moduleKey, string action);
        LicenseValidationResult Validate();
    }
}
