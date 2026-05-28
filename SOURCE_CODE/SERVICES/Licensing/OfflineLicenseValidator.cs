using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class OfflineLicenseValidator : IOfflineLicenseValidator
    {
        private const string PublicKeyXml =
            "<RSAKeyValue><Modulus>7rKnCt2zsJpmG2xlBr2gnuaPFz9KnufX1I8wCls29YDQkaktNtxJJvRcTh7ibwBr0TrwCdTRzVwbvcNtgars28k7UjZzpLv2/AacMOkktmHNDVY153BthnGrIik3QfsCcwMzfj2D/kaxgSYcs6brP2l//MWHCm87xFevmVIwiWCP1DCjFZCWypDTlhs8nVPkAzuNg3iQdv0tPhhQ3ihIYFElb5EUOdkV+BH05pNvxHfGWUTD6jgWC2a8UXWqJjKMdGL+IgGjSMaEoREHnVcAzkj4N1XzhQ/WBRnQFm+zAvrak2io8JQTapHu4H4C5fqg2rt2K+zzcgE/fInFCTgnZQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public LicenseValidationResult ValidateLicenseFile(string licenseFilePath, string expectedMachineFingerprintHash)
        {
            if (string.IsNullOrWhiteSpace(licenseFilePath) || !File.Exists(licenseFilePath))
                return Fail("License file was not found.", true);

            try
            {
                string envelopeJson = File.ReadAllText(licenseFilePath, Encoding.UTF8);
                SignedLicenseEnvelope envelope = _serializer.Deserialize<SignedLicenseEnvelope>(envelopeJson);
                if (envelope == null || string.IsNullOrWhiteSpace(envelope.Payload) || string.IsNullOrWhiteSpace(envelope.Signature))
                    return Fail("License file is invalid or incomplete.", true);

                if (!string.Equals(envelope.Algorithm, "RS256", StringComparison.OrdinalIgnoreCase))
                    return Fail("Unsupported license signature algorithm.", true);

                byte[] payloadBytes = Convert.FromBase64String(envelope.Payload);
                byte[] signatureBytes = Convert.FromBase64String(envelope.Signature);
                if (!Verify(payloadBytes, signatureBytes))
                    return Fail("License signature is invalid.", true);

                LicensePayload payload = _serializer.Deserialize<LicensePayload>(Encoding.UTF8.GetString(payloadBytes));
                if (payload == null || string.IsNullOrWhiteSpace(payload.LicenseKey))
                    return Fail("License payload is invalid.", true);

                if (!string.IsNullOrWhiteSpace(payload.MachineFingerprintHash)
                    && !string.Equals(payload.MachineFingerprintHash, expectedMachineFingerprintHash, StringComparison.Ordinal))
                    return Fail("License is not activated for this machine.", true);

                LicensePlanType plan = LicensePlanCatalog.ParsePlan(
                    string.IsNullOrWhiteSpace(payload.PlanName) ? payload.PlanType : payload.PlanName);

                var snapshot = new LicenseSnapshot
                {
                    LicenseKey = SecurityHelpers.HashToken(payload.LicenseKey),
                    CompanyId = payload.CompanyId,
                    CompanyCode = payload.CompanyCode,
                    PlanType = plan,
                    CompanyName = payload.CompanyName,
                    SubscriptionStartDateUtc = EnsureUtc(payload.SubscriptionStartDateUtc == default(DateTime) ? payload.IssueDateUtc : payload.SubscriptionStartDateUtc),
                    SubscriptionEndDateUtc = EnsureUtc(payload.SubscriptionEndDateUtc == default(DateTime) ? payload.ExpiryDateUtc : payload.SubscriptionEndDateUtc),
                    SubscriptionStatus = string.IsNullOrWhiteSpace(payload.SubscriptionStatus) ? "Active" : payload.SubscriptionStatus,
                    MaxCompanies = payload.MaxCompanies <= 0 ? 1 : payload.MaxCompanies,
                    MaxDevices = payload.MaxDevices <= 0 ? LicensePlanCatalog.GetMaxDevices(plan) : payload.MaxDevices,
                    MaxUsers = payload.MaxUsers <= 0 ? LicensePlanCatalog.GetMaxDevices(plan) : payload.MaxUsers,
                    IssueDateUtc = EnsureUtc(payload.IssueDateUtc),
                    ExpiryDateUtc = EnsureUtc(payload.ExpiryDateUtc),
                    GracePeriodDays = payload.GracePeriodDays < 0 ? 0 : payload.GracePeriodDays,
                    MachineFingerprintHash = string.IsNullOrWhiteSpace(payload.MachineFingerprintHash) ? expectedMachineFingerprintHash : payload.MachineFingerprintHash,
                    ActivatedDeviceId = string.IsNullOrWhiteSpace(payload.ActivatedDeviceId) ? expectedMachineFingerprintHash : payload.ActivatedDeviceId,
                    ActivatedDeviceCount = payload.ActivatedDeviceCount <= 0 ? 1 : payload.ActivatedDeviceCount,
                    OnlineValidationRequired = payload.OnlineValidationRequired,
                    SupportLevel = payload.SupportLevel,
                    PlanName = string.IsNullOrWhiteSpace(payload.PlanName) ? LicensePlanCatalog.GetDisplayName(plan) : payload.PlanName,
                    BillingCycle = string.IsNullOrWhiteSpace(payload.BillingCycle) ? DefaultBillingCycle(plan) : payload.BillingCycle,
                    Currency = string.IsNullOrWhiteSpace(payload.Currency) ? "INR" : payload.Currency,
                    PriceAmount = payload.PriceAmount <= 0 ? LicensePlanCatalog.GetAnnualPrice(plan) : payload.PriceAmount,
                    RenewalPriceAmount = payload.RenewalPriceAmount <= 0 ? LicensePlanCatalog.GetAnnualPrice(plan) : payload.RenewalPriceAmount,
                    IsLaunchOffer = payload.IsLaunchOffer,
                    EnabledModules = payload.EnabledModules ?? LicenseFeatureCatalog.GetModulesForPlan(plan),
                    LastSuccessfulValidationUtc = DateTime.UtcNow,
                    LastAppOpenUtc = DateTime.UtcNow,
                    Status = LicenseStatus.Active,
                    StatusMessage = "Offline license activated."
                };

                return new LicenseValidationResult { Success = true, Snapshot = snapshot, Message = "License file verified." };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("OfflineLicenseValidator.ValidateLicenseFile", ex);
                return Fail("License file could not be read or verified: " + ex.Message, true);
            }
        }

        private static bool Verify(byte[] payloadBytes, byte[] signatureBytes)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(PublicKeyXml);
                return rsa.VerifyData(payloadBytes, CryptoConfig.MapNameToOID("SHA256"), signatureBytes);
            }
        }

        private static LicenseValidationResult Fail(string message, bool tampered)
        {
            return new LicenseValidationResult
            {
                Success = false,
                IsFrozen = true,
                IsTampered = tampered,
                Message = message,
                Snapshot = new LicenseSnapshot { Status = tampered ? LicenseStatus.Tampered : LicenseStatus.Frozen, StatusMessage = message }
            };
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            if (value == default(DateTime))
                return DateTime.UtcNow;
            return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string DefaultBillingCycle(LicensePlanType plan)
        {
            return plan == LicensePlanType.Trial ? "14 days" : "Annual";
        }
    }
}
