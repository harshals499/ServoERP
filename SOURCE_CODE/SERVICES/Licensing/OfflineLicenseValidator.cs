using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class OfflineLicenseValidator : IOfflineLicenseValidator
    {
        private const string PublicKeyXml =
            "<RSAKeyValue><Modulus>6xNXuf1FWXUCj7S5gwawdFNJLbOmJjK5m/5vWqrsX2dtOQt6WGwXKc4fN7a9f7Efg2xrPv03RKvS7O4AZlIwrT/UnaXcUqM+vQMnk6un6yJU0kxur6oyUxFMWTdU5Lm3LM6QsXrlV5baTURt7ZbHBOT2cRYPtZOjDjnE0P3izW4UPIrowEd4Z4XiKE7GxlCj3bruk5ynyp5xBnPKy8aAfKw3GfLAvJul/AX4BsThOnsPRzK0hADt77TxnSzy0F1+iOgOBnOvY0JS/h9rhRuApwp4VnaEoNV+CL7bmqaNQm57cevnlPqpKOZHQfbxBPlK1ZczPi3bv3pkdsZSMEhsDQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

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

                LicensePlanType plan;
                if (!Enum.TryParse(payload.PlanType ?? string.Empty, true, out plan))
                    return Fail("License plan is invalid.", true);

                var snapshot = new LicenseSnapshot
                {
                    LicenseKey = payload.LicenseKey,
                    PlanType = plan,
                    CompanyName = payload.CompanyName,
                    MaxCompanies = payload.MaxCompanies <= 0 ? 1 : payload.MaxCompanies,
                    MaxDevices = payload.MaxDevices <= 0 ? DefaultMaxDevices(plan) : payload.MaxDevices,
                    MaxUsers = payload.MaxUsers <= 0 ? DefaultMaxDevices(plan) : payload.MaxUsers,
                    IssueDateUtc = EnsureUtc(payload.IssueDateUtc),
                    ExpiryDateUtc = EnsureUtc(payload.ExpiryDateUtc),
                    GracePeriodDays = payload.GracePeriodDays < 0 ? 0 : payload.GracePeriodDays,
                    MachineFingerprintHash = string.IsNullOrWhiteSpace(payload.MachineFingerprintHash) ? expectedMachineFingerprintHash : payload.MachineFingerprintHash,
                    ActivatedDeviceId = string.IsNullOrWhiteSpace(payload.ActivatedDeviceId) ? expectedMachineFingerprintHash : payload.ActivatedDeviceId,
                    ActivatedDeviceCount = payload.ActivatedDeviceCount <= 0 ? 1 : payload.ActivatedDeviceCount,
                    SupportLevel = payload.SupportLevel,
                    PlanName = string.IsNullOrWhiteSpace(payload.PlanName) ? plan.ToString() : payload.PlanName,
                    BillingCycle = string.IsNullOrWhiteSpace(payload.BillingCycle) ? DefaultBillingCycle(plan) : payload.BillingCycle,
                    Currency = string.IsNullOrWhiteSpace(payload.Currency) ? "INR" : payload.Currency,
                    PriceAmount = payload.PriceAmount < 0 ? 0 : payload.PriceAmount,
                    RenewalPriceAmount = payload.RenewalPriceAmount < 0 ? 0 : payload.RenewalPriceAmount,
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

        private static int DefaultMaxDevices(LicensePlanType plan)
        {
            if (plan == LicensePlanType.Trial) return 1;
            if (plan == LicensePlanType.Standard) return 3;
            return 10;
        }

        private static string DefaultBillingCycle(LicensePlanType plan)
        {
            return plan == LicensePlanType.Trial ? "14 days" : "Annual";
        }
    }
}
