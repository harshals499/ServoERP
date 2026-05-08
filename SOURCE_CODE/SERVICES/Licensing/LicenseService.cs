using System;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class LicenseService : ILicenseService, ILicenseEnforcementService
    {
        private static readonly object Sync = new object();
        private static LicenseValidationResult _lastValidation;
        private readonly ILicenseCacheStore _cache;
        private readonly IDeviceFingerprintService _fingerprint;
        private readonly IOfflineLicenseValidator _offlineValidator;
        private readonly IOnlineLicenseClient _onlineClient;
        private readonly DbExecutor _db = new DbExecutor();

        public LicenseService()
            : this(new LicenseCacheStore(), new DeviceFingerprintService(), new OfflineLicenseValidator(), new OnlineLicenseClient())
        {
        }

        public LicenseService(ILicenseCacheStore cache, IDeviceFingerprintService fingerprint, IOfflineLicenseValidator offlineValidator, IOnlineLicenseClient onlineClient)
        {
            _cache = cache;
            _fingerprint = fingerprint;
            _offlineValidator = offlineValidator;
            _onlineClient = onlineClient;
        }

        public LicenseValidationResult ValidateCurrentLicense()
        {
            lock (Sync)
            {
                LicenseSnapshot snapshot = _cache.Load();
                LicenseValidationResult result = Evaluate(snapshot);
                _lastValidation = result;

                if (result.Snapshot != null && result.Success)
                    _cache.Save(result.Snapshot);

                PersistState(result.Snapshot);
                LogEvent(result.Snapshot, result.IsTampered ? "TAMPER" : "VALIDATE", result.Message);
                return result;
            }
        }

        public LicenseValidationResult ActivateOffline(string licenseFilePath)
        {
            lock (Sync)
            {
                LicenseValidationResult result = _offlineValidator.ValidateLicenseFile(licenseFilePath, _fingerprint.GetFingerprintHash());
                if (result.Success && result.Snapshot != null)
                {
                    result.Snapshot.LastSuccessfulValidationUtc = DateTime.UtcNow;
                    result.Snapshot.LastAppOpenUtc = DateTime.UtcNow;
                    result.Snapshot.Status = LicenseStatus.Active;
                    _cache.Save(result.Snapshot);
                    PersistState(result.Snapshot);
                    PersistDevice(result.Snapshot);
                    PersistEntitlements(result.Snapshot);
                    LogEvent(result.Snapshot, "ACTIVATE_OFFLINE", "Offline license activated.");
                    SessionManager.LogAction("LICENSE", "Settings", null, "Offline license activated for " + result.Snapshot.CompanyName);
                    _lastValidation = Evaluate(result.Snapshot);
                    return _lastValidation;
                }

                LogEvent(result.Snapshot, "ACTIVATE_OFFLINE_FAILED", result.Message);
                _lastValidation = result;
                return result;
            }
        }

        public LicenseValidationResult ActivateOnline(LicenseActivationRequest request)
        {
            if (request == null)
                request = new LicenseActivationRequest();

            request.MachineFingerprintHash = _fingerprint.GetFingerprintHash();
            request.DeviceName = _fingerprint.GetDeviceName();
            LicenseValidationResult result = _onlineClient.Activate(request);
            if (result.Success && result.Snapshot != null)
            {
                _cache.Save(result.Snapshot);
                PersistState(result.Snapshot);
                PersistDevice(result.Snapshot);
                PersistEntitlements(result.Snapshot);
                LogEvent(result.Snapshot, "ACTIVATE_ONLINE", "Online license activated.");
            }
            else
            {
                LogEvent(result.Snapshot, "ACTIVATE_ONLINE_FAILED", result.Message);
            }

            _lastValidation = result;
            return result;
        }

        public LicenseSnapshot GetCurrentSnapshot()
        {
            return (_lastValidation != null && _lastValidation.Snapshot != null) ? _lastValidation.Snapshot : _cache.Load();
        }

        public bool IsFrozen()
        {
            LicenseValidationResult result = _lastValidation ?? ValidateCurrentLicense();
            return result.IsFrozen;
        }

        public bool IsModuleEnabled(string moduleKey)
        {
            return LicenseFeatureCatalog.IsModuleEnabled(GetCurrentSnapshot(), moduleKey);
        }

        public bool CanPerform(string moduleKey, string action)
        {
            string act = (action ?? string.Empty).Trim().ToUpperInvariant();
            if (act == "VIEW")
                return IsModuleEnabled(moduleKey);

            if (!IsModuleEnabled(moduleKey))
                return false;

            if (!IsFrozen())
                return true;

            return string.Equals(moduleKey, "Settings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleKey, "Reports", StringComparison.OrdinalIgnoreCase);
        }

        public LicenseValidationResult Validate()
        {
            return ValidateCurrentLicense();
        }

        private LicenseValidationResult Evaluate(LicenseSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new LicenseValidationResult
                {
                    Success = false,
                    RequiresActivation = true,
                    IsFrozen = true,
                    Message = "ServoERP license activation is required.",
                    Snapshot = new LicenseSnapshot { Status = LicenseStatus.Missing, StatusMessage = "Activation required." }
                };
            }

            if (snapshot.Status == LicenseStatus.Tampered)
                return Frozen(snapshot, true, snapshot.StatusMessage ?? "License cache is invalid.");

            string currentFingerprint = _fingerprint.GetFingerprintHash();
            if (!string.Equals(snapshot.MachineFingerprintHash, currentFingerprint, StringComparison.Ordinal))
                return Frozen(snapshot, true, "License cache was copied from another machine or device fingerprint changed.");

            DateTime now = DateTime.UtcNow;
            DateTime lastKnown = Max(snapshot.LastAppOpenUtc, snapshot.LastSuccessfulValidationUtc, snapshot.LastTrustedServerTimeUtc, snapshot.IssueDateUtc);
            if (now.AddMinutes(5) < lastKnown)
                return Frozen(snapshot, true, "System clock rollback detected.");

            snapshot.LastAppOpenUtc = now;
            if (snapshot.ActivatedDeviceCount > snapshot.MaxDevices)
                return Frozen(snapshot, true, "Too many devices are activated for this license.");

            DateTime graceEnd = snapshot.ExpiryDateUtc.AddDays(Math.Max(0, snapshot.GracePeriodDays));
            if (now > graceEnd)
                return Frozen(snapshot, false, "Your ServoERP license has expired. Renew to continue business operations.");

            if (now > snapshot.ExpiryDateUtc)
            {
                snapshot.Status = LicenseStatus.Grace;
                snapshot.StatusMessage = "License expired; grace period is active.";
                return new LicenseValidationResult { Success = true, IsFrozen = false, Message = snapshot.StatusMessage, Snapshot = snapshot };
            }

            int daysRemaining = (int)Math.Ceiling((snapshot.ExpiryDateUtc - now).TotalDays);
            snapshot.Status = daysRemaining <= 30 ? LicenseStatus.Warning : LicenseStatus.Active;
            snapshot.StatusMessage = daysRemaining <= 30
                ? "License expires in " + daysRemaining + " day(s)."
                : "License active.";

            return new LicenseValidationResult { Success = true, IsFrozen = false, Message = snapshot.StatusMessage, Snapshot = snapshot };
        }

        private static LicenseValidationResult Frozen(LicenseSnapshot snapshot, bool tampered, string message)
        {
            snapshot.Status = tampered ? LicenseStatus.Tampered : LicenseStatus.Frozen;
            snapshot.StatusMessage = message;
            return new LicenseValidationResult
            {
                Success = false,
                IsFrozen = true,
                IsTampered = tampered,
                Message = message,
                Snapshot = snapshot
            };
        }

        private static DateTime Max(DateTime? a, DateTime? b, DateTime? c, DateTime d)
        {
            DateTime value = d;
            if (a.HasValue && a.Value > value) value = a.Value;
            if (b.HasValue && b.Value > value) value = b.Value;
            if (c.HasValue && c.Value > value) value = c.Value;
            return value;
        }

        private void PersistState(LicenseSnapshot s)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.LicenseKey))
                return;

            EnsurePricingColumns();

            _db.Execute(@"
IF EXISTS (SELECT 1 FROM LicenseState WHERE LicenseKey=@LicenseKey)
UPDATE LicenseState SET PlanType=@PlanType, CompanyName=@CompanyName, MaxCompanies=@MaxCompanies, MaxDevices=@MaxDevices, MaxUsers=@MaxUsers,
IssueDateUtc=@IssueDateUtc, ExpiryDateUtc=@ExpiryDateUtc, LastSuccessfulValidationUtc=@LastSuccessfulValidationUtc,
LastAppOpenUtc=@LastAppOpenUtc, LastTrustedServerTimeUtc=@LastTrustedServerTimeUtc, GracePeriodDays=@GracePeriodDays,
Status=@Status, MachineFingerprintHash=@MachineFingerprintHash, SupportLevel=@SupportLevel, PlanName=@PlanName,
BillingCycle=@BillingCycle, Currency=@Currency, PriceAmount=@PriceAmount, RenewalPriceAmount=@RenewalPriceAmount,
IsLaunchOffer=@IsLaunchOffer, UpdatedUtc=GETUTCDATE()
WHERE LicenseKey=@LicenseKey
ELSE
INSERT INTO LicenseState (LicenseKey, PlanType, CompanyName, MaxCompanies, MaxDevices, MaxUsers, IssueDateUtc, ExpiryDateUtc,
LastSuccessfulValidationUtc, LastAppOpenUtc, LastTrustedServerTimeUtc, GracePeriodDays, Status, MachineFingerprintHash, SupportLevel,
PlanName, BillingCycle, Currency, PriceAmount, RenewalPriceAmount, IsLaunchOffer)
VALUES (@LicenseKey, @PlanType, @CompanyName, @MaxCompanies, @MaxDevices, @MaxUsers, @IssueDateUtc, @ExpiryDateUtc,
@LastSuccessfulValidationUtc, @LastAppOpenUtc, @LastTrustedServerTimeUtc, @GracePeriodDays, @Status, @MachineFingerprintHash, @SupportLevel,
@PlanName, @BillingCycle, @Currency, @PriceAmount, @RenewalPriceAmount, @IsLaunchOffer);",
                DbExecutor.Param("@LicenseKey", s.LicenseKey),
                DbExecutor.Param("@PlanType", s.PlanType.ToString()),
                DbExecutor.Param("@CompanyName", s.CompanyName),
                DbExecutor.Param("@MaxCompanies", s.MaxCompanies),
                DbExecutor.Param("@MaxDevices", s.MaxDevices),
                DbExecutor.Param("@MaxUsers", s.MaxUsers),
                DbExecutor.Param("@IssueDateUtc", s.IssueDateUtc),
                DbExecutor.Param("@ExpiryDateUtc", s.ExpiryDateUtc),
                DbExecutor.Param("@LastSuccessfulValidationUtc", (object)s.LastSuccessfulValidationUtc ?? DBNull.Value),
                DbExecutor.Param("@LastAppOpenUtc", (object)s.LastAppOpenUtc ?? DBNull.Value),
                DbExecutor.Param("@LastTrustedServerTimeUtc", (object)s.LastTrustedServerTimeUtc ?? DBNull.Value),
                DbExecutor.Param("@GracePeriodDays", s.GracePeriodDays),
                DbExecutor.Param("@Status", s.Status.ToString()),
                DbExecutor.Param("@MachineFingerprintHash", s.MachineFingerprintHash),
                DbExecutor.Param("@SupportLevel", s.SupportLevel),
                DbExecutor.Param("@PlanName", string.IsNullOrWhiteSpace(s.PlanName) ? s.PlanType.ToString() : s.PlanName),
                DbExecutor.Param("@BillingCycle", string.IsNullOrWhiteSpace(s.BillingCycle) ? DefaultBillingCycle(s.PlanType) : s.BillingCycle),
                DbExecutor.Param("@Currency", string.IsNullOrWhiteSpace(s.Currency) ? "INR" : s.Currency),
                DbExecutor.Param("@PriceAmount", s.PriceAmount < 0 ? 0 : s.PriceAmount),
                DbExecutor.Param("@RenewalPriceAmount", s.RenewalPriceAmount < 0 ? 0 : s.RenewalPriceAmount),
                DbExecutor.Param("@IsLaunchOffer", s.IsLaunchOffer));
        }

        private void EnsurePricingColumns()
        {
            _db.Execute(@"
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'PlanName') IS NULL
    ALTER TABLE LicenseState ADD PlanName NVARCHAR(80) NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'BillingCycle') IS NULL
    ALTER TABLE LicenseState ADD BillingCycle NVARCHAR(40) NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'Currency') IS NULL
    ALTER TABLE LicenseState ADD Currency NVARCHAR(10) NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'PriceAmount') IS NULL
    ALTER TABLE LicenseState ADD PriceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_LicenseState_PriceAmount DEFAULT 0;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'RenewalPriceAmount') IS NULL
    ALTER TABLE LicenseState ADD RenewalPriceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_LicenseState_RenewalPriceAmount DEFAULT 0;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'IsLaunchOffer') IS NULL
    ALTER TABLE LicenseState ADD IsLaunchOffer BIT NOT NULL CONSTRAINT DF_LicenseState_IsLaunchOffer DEFAULT 0;");
        }

        private static string DefaultBillingCycle(LicensePlanType plan)
        {
            return plan == LicensePlanType.Trial ? "14 days" : "Annual";
        }

        private void PersistDevice(LicenseSnapshot s)
        {
            _db.Execute(@"
IF NOT EXISTS (SELECT 1 FROM ActivatedDevices WHERE LicenseKey=@LicenseKey AND DeviceFingerprintHash=@DeviceFingerprintHash)
INSERT INTO ActivatedDevices (LicenseKey, DeviceFingerprintHash, DeviceName, ActivatedUtc, LastSeenUtc, Status)
VALUES (@LicenseKey, @DeviceFingerprintHash, @DeviceName, GETUTCDATE(), GETUTCDATE(), 'Active')
ELSE
UPDATE ActivatedDevices SET LastSeenUtc=GETUTCDATE(), Status='Active' WHERE LicenseKey=@LicenseKey AND DeviceFingerprintHash=@DeviceFingerprintHash;",
                DbExecutor.Param("@LicenseKey", s.LicenseKey),
                DbExecutor.Param("@DeviceFingerprintHash", s.MachineFingerprintHash),
                DbExecutor.Param("@DeviceName", _fingerprint.GetDeviceName()));
        }

        private void PersistEntitlements(LicenseSnapshot s)
        {
            _db.Execute("DELETE FROM FeatureEntitlements WHERE LicenseKey=@LicenseKey", DbExecutor.Param("@LicenseKey", s.LicenseKey));
            foreach (string module in s.EnabledModules ?? LicenseFeatureCatalog.GetModulesForPlan(s.PlanType))
            {
                _db.Execute("INSERT INTO FeatureEntitlements (LicenseKey, ModuleKey, IsEnabled) VALUES (@LicenseKey, @ModuleKey, 1)",
                    DbExecutor.Param("@LicenseKey", s.LicenseKey),
                    DbExecutor.Param("@ModuleKey", module));
            }
        }

        private void LogEvent(LicenseSnapshot s, string eventType, string message)
        {
            try
            {
                _db.Execute("INSERT INTO LicenseEvents (LicenseKey, EventType, Message, DeviceFingerprintHash, CreatedUtc) VALUES (@LicenseKey, @EventType, @Message, @DeviceFingerprintHash, GETUTCDATE())",
                    DbExecutor.Param("@LicenseKey", s?.LicenseKey),
                    DbExecutor.Param("@EventType", eventType),
                    DbExecutor.Param("@Message", message),
                    DbExecutor.Param("@DeviceFingerprintHash", _fingerprint.GetFingerprintHash()));
                AppLogger.LogInfo("License event: " + eventType + " | " + message);
            }
            catch (SqlException ex)
            {
                AppLogger.LogError("LicenseService.LogEvent", ex);
            }
        }
    }
}
