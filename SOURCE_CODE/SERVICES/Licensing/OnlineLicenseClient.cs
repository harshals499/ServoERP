using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class OnlineLicenseClient : IOnlineLicenseClient
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public LicenseValidationResult Activate(LicenseActivationRequest request)
        {
            if (request == null)
                request = new LicenseActivationRequest();

            if (string.IsNullOrWhiteSpace(request.CompanyCode))
                return Fail("Enter the company code before activating.");
            if (string.IsNullOrWhiteSpace(request.LicenseKey))
                return Fail("Enter the license key before activating.");

            return Post(ConfigService.GetLicenseActivationUrl(), new
            {
                companyCode = request.CompanyCode.Trim(),
                licenseKey = request.LicenseKey.Trim(),
                licenseKeyHash = SecurityHelpers.HashToken(request.LicenseKey.Trim()),
                machineFingerprintHash = request.MachineFingerprintHash,
                deviceName = request.DeviceName,
                appVersion = request.AppVersion,
                clientUtc = DateTime.UtcNow
            }, request);
        }

        public LicenseValidationResult Validate(LicenseSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.LicenseKey))
                return Fail("No cached subscription license is available for online validation.");

            return Post(ConfigService.GetLicenseValidationUrl(), new
            {
                companyId = snapshot.CompanyId,
                companyCode = snapshot.CompanyCode,
                licenseKeyHash = snapshot.LicenseKey,
                activatedDeviceId = snapshot.ActivatedDeviceId,
                machineFingerprintHash = snapshot.MachineFingerprintHash,
                deviceName = new DeviceFingerprintService().GetDeviceName(),
                appVersion = ConfigService.GetAppVersion(),
                clientUtc = DateTime.UtcNow
            }, null);
        }

        private LicenseValidationResult Post(string url, object payload, LicenseActivationRequest activationRequest)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Fail("Online licensing URL is not configured.");

            try
            {
                string json = _serializer.Serialize(payload);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.UserAgent = "ServoERP/" + ConfigService.GetAppVersion();

                using (Stream stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();
                    var dto = _serializer.Deserialize<LicenseServerResponse>(body);
                    if (dto == null)
                        return Fail("License server returned an empty response.");
                    if (!dto.success)
                        return Fail(string.IsNullOrWhiteSpace(dto.message) ? "License server rejected the request." : dto.message);

                    LicenseSnapshot snapshot = BuildSnapshot(dto, activationRequest);
                    return new LicenseValidationResult
                    {
                        Success = true,
                        Message = string.IsNullOrWhiteSpace(dto.message) ? "Subscription validated." : dto.message,
                        Snapshot = snapshot
                    };
                }
            }
            catch (WebException ex)
            {
                string serverMessage = TryReadError(ex);
                return Fail(string.IsNullOrWhiteSpace(serverMessage)
                    ? "Could not reach ServoERP license server: " + ex.Message
                    : serverMessage);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("OnlineLicenseClient.Post", ex);
                return Fail("Online license validation failed: " + ex.Message);
            }
        }

        private LicenseSnapshot BuildSnapshot(LicenseServerResponse dto, LicenseActivationRequest activationRequest)
        {
            string rawPlan = First(dto.planName, dto.planType, LicensePlanCatalog.StarterAmc);
            LicensePlanType plan = LicensePlanCatalog.ParsePlan(rawPlan);

            DateTime now = DateTime.UtcNow;
            DateTime start = EnsureUtc(dto.subscriptionStartDateUtc, now);
            DateTime end = EnsureUtc(dto.subscriptionEndDateUtc, EnsureUtc(dto.expiryDateUtc, now.AddYears(1)));
            string companyCode = First(dto.companyCode, activationRequest?.CompanyCode);
            string safeLicenseKey = First(dto.licenseKeyHash, dto.licenseKey, activationRequest == null ? null : SecurityHelpers.HashToken(activationRequest.LicenseKey));

            return new LicenseSnapshot
            {
                LicenseKey = safeLicenseKey,
                CompanyId = dto.companyId,
                CompanyCode = companyCode,
                PlanType = plan,
                CompanyName = First(dto.companyName, activationRequest?.CompanyName, companyCode, "ServoERP Client"),
                SubscriptionStartDateUtc = start,
                SubscriptionEndDateUtc = end,
                SubscriptionStatus = First(dto.subscriptionStatus, "Active"),
                MaxCompanies = dto.maxCompanies <= 0 ? 1 : dto.maxCompanies,
                MaxDevices = dto.maxDevicesAllowed <= 0 ? LicensePlanCatalog.GetMaxDevices(plan) : dto.maxDevicesAllowed,
                MaxUsers = dto.maxUsers <= 0 ? LicensePlanCatalog.GetMaxDevices(plan) : dto.maxUsers,
                IssueDateUtc = EnsureUtc(dto.issueDateUtc, start),
                ExpiryDateUtc = end,
                LastSuccessfulValidationUtc = now,
                LastAppOpenUtc = now,
                LastTrustedServerTimeUtc = EnsureUtc(dto.serverTimeUtc, now),
                LastServerValidationAttemptUtc = now,
                GracePeriodDays = dto.offlineGraceDays < 0 ? 0 : dto.offlineGraceDays,
                Status = LicenseStatus.Active,
                MachineFingerprintHash = First(dto.machineFingerprintHash, activationRequest?.MachineFingerprintHash),
                ActivatedDeviceId = First(dto.activatedDeviceId, activationRequest?.MachineFingerprintHash),
                ActivatedDeviceCount = dto.activatedDeviceCount <= 0 ? 1 : dto.activatedDeviceCount,
                OnlineValidationRequired = true,
                SupportLevel = dto.supportLevel,
                PlanName = string.IsNullOrWhiteSpace(dto.planName) ? LicensePlanCatalog.GetDisplayName(plan) : dto.planName,
                BillingCycle = First(dto.billingCycle, "Annual"),
                Currency = First(dto.currency, "INR"),
                PriceAmount = dto.priceAmount <= 0 ? LicensePlanCatalog.GetAnnualPrice(plan) : dto.priceAmount,
                RenewalPriceAmount = dto.renewalPriceAmount <= 0 ? LicensePlanCatalog.GetAnnualPrice(plan) : dto.renewalPriceAmount,
                IsLaunchOffer = dto.isLaunchOffer,
                EnabledModules = dto.modulesEnabled ?? LicenseFeatureCatalog.GetModulesForPlan(plan),
                StatusMessage = "Subscription active."
            };
        }

        private static LicenseValidationResult Fail(string message)
        {
            return new LicenseValidationResult
            {
                Success = false,
                RequiresActivation = true,
                Message = message
            };
        }

        private static string TryReadError(WebException ex)
        {
            try
            {
                using (var response = ex.Response)
                using (var stream = response?.GetResponseStream())
                using (var reader = stream == null ? null : new StreamReader(stream, Encoding.UTF8))
                    return reader?.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static DateTime EnsureUtc(DateTime value, DateTime fallback)
        {
            if (value == default(DateTime))
                return fallback;
            return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static string First(params string[] values)
        {
            foreach (string value in values)
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            return string.Empty;
        }

        private sealed class LicenseServerResponse
        {
            public bool success { get; set; }
            public string message { get; set; }
            public string companyId { get; set; }
            public string companyCode { get; set; }
            public string companyName { get; set; }
            public string licenseKey { get; set; }
            public string licenseKeyHash { get; set; }
            public string planType { get; set; }
            public string planName { get; set; }
            public string subscriptionStatus { get; set; }
            public DateTime subscriptionStartDateUtc { get; set; }
            public DateTime subscriptionEndDateUtc { get; set; }
            public DateTime issueDateUtc { get; set; }
            public DateTime expiryDateUtc { get; set; }
            public DateTime serverTimeUtc { get; set; }
            public int maxCompanies { get; set; }
            public int maxDevicesAllowed { get; set; }
            public int maxUsers { get; set; }
            public int offlineGraceDays { get; set; }
            public string machineFingerprintHash { get; set; }
            public string activatedDeviceId { get; set; }
            public int activatedDeviceCount { get; set; }
            public string supportLevel { get; set; }
            public string billingCycle { get; set; }
            public string currency { get; set; }
            public decimal priceAmount { get; set; }
            public decimal renewalPriceAmount { get; set; }
            public bool isLaunchOffer { get; set; }
            public System.Collections.Generic.List<string> modulesEnabled { get; set; }
        }
    }
}
