using System;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

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
                if (snapshot != null && snapshot.OnlineValidationRequired)
                    snapshot = TryRefreshOnlineSubscription(snapshot);

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

        public LicenseValidationResult ActivateTrial(string companyName)
        {
            lock (Sync)
            {
                var now = DateTime.UtcNow;
                var snapshot = new LicenseSnapshot
                {
                    LicenseKey = "TRIAL-" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
                    CompanyCode = "TRIAL",
                    PlanType = LicensePlanType.Trial,
                    CompanyName = string.IsNullOrWhiteSpace(companyName) ? "Trial Company" : companyName.Trim(),
                    SubscriptionStartDateUtc = now,
                    SubscriptionEndDateUtc = now.AddDays(14),
                    SubscriptionStatus = "Active",
                    MaxCompanies = 1,
                    MaxDevices = 1,
                    MaxUsers = 1,
                    IssueDateUtc = now,
                    ExpiryDateUtc = now.AddDays(14),
                    LastSuccessfulValidationUtc = now,
                    LastAppOpenUtc = now,
                    LastTrustedServerTimeUtc = now,
                    GracePeriodDays = 3,
                    Status = LicenseStatus.Active,
                    MachineFingerprintHash = _fingerprint.GetFingerprintHash(),
                    ActivatedDeviceId = _fingerprint.GetFingerprintHash(),
                    ActivatedDeviceCount = 1,
                    OnlineValidationRequired = false,
                    SupportLevel = "Limited",
                    PlanName = "Trial Download",
                    BillingCycle = "14 days",
                    Currency = "INR",
                    PriceAmount = 0,
                    RenewalPriceAmount = LicensePlanCatalog.GetAnnualPrice(LicensePlanType.Basic),
                    IsLaunchOffer = false,
                    EnabledModules = LicenseFeatureCatalog.GetModulesForPlan(LicensePlanType.Trial),
                    StatusMessage = "Trial active."
                };

                _cache.Save(snapshot);
                PersistState(snapshot);
                PersistDevice(snapshot);
                PersistEntitlements(snapshot);
                LogEvent(snapshot, "ACTIVATE_TRIAL", "14-day trial activated.");
                SessionManager.LogAction("LICENSE", "Activation", null, "14-day trial activated for " + snapshot.CompanyName);
                _lastValidation = Evaluate(snapshot);
                return _lastValidation;
            }
        }

        public LicenseValidationResult ActivateOnline(LicenseActivationRequest request)
        {
            if (request == null)
                request = new LicenseActivationRequest();

            request.MachineFingerprintHash = _fingerprint.GetFingerprintHash();
            request.DeviceName = _fingerprint.GetDeviceName();
            request.AppVersion = ConfigService.GetAppVersion();
            LicenseValidationResult result = _onlineClient.Activate(request);
            if (result.Success && result.Snapshot != null)
            {
                NormalizeOnlineSnapshot(result.Snapshot, request);
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

            string subscriptionStatus = (snapshot.SubscriptionStatus ?? string.Empty).Trim();
            if (string.Equals(subscriptionStatus, "Suspended", StringComparison.OrdinalIgnoreCase))
                return Frozen(snapshot, false, "This company subscription is suspended. Contact ServoERP support.");
            if (string.Equals(subscriptionStatus, "Expired", StringComparison.OrdinalIgnoreCase))
                return Frozen(snapshot, false, "This company subscription is expired. Renew to continue.");

            snapshot.LastAppOpenUtc = now;
            if (snapshot.ActivatedDeviceCount > snapshot.MaxDevices)
                return Frozen(snapshot, true, "Too many devices are activated for this license.");

            if (snapshot.OnlineValidationRequired)
            {
                DateTime lastServerOk = snapshot.LastSuccessfulValidationUtc ?? snapshot.IssueDateUtc;
                DateTime offlineGraceEnd = lastServerOk.AddDays(Math.Max(0, snapshot.GracePeriodDays));
                if (now > offlineGraceEnd)
                    return Frozen(snapshot, false, "Online subscription validation is required. Connect to the internet and validate the license.");
            }

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

        private LicenseSnapshot TryRefreshOnlineSubscription(LicenseSnapshot snapshot)
        {
            snapshot.LastServerValidationAttemptUtc = DateTime.UtcNow;
            LicenseValidationResult online = _onlineClient.Validate(snapshot);
            if (online.Success && online.Snapshot != null)
            {
                NormalizeOnlineSnapshot(online.Snapshot, new LicenseActivationRequest
                {
                    CompanyCode = snapshot.CompanyCode,
                    CompanyName = snapshot.CompanyName,
                    MachineFingerprintHash = _fingerprint.GetFingerprintHash(),
                    DeviceName = _fingerprint.GetDeviceName(),
                    AppVersion = ConfigService.GetAppVersion()
                });
                LogEvent(online.Snapshot, "VALIDATE_ONLINE", "Online subscription validated.");
                return online.Snapshot;
            }

            LogEvent(snapshot, "VALIDATE_ONLINE_FAILED", online.Message);
            snapshot.StatusMessage = online.Message;
            return snapshot;
        }

        private void NormalizeOnlineSnapshot(LicenseSnapshot snapshot, LicenseActivationRequest request)
        {
            DateTime now = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(snapshot.LicenseKey) && !string.IsNullOrWhiteSpace(request?.LicenseKey))
                snapshot.LicenseKey = SecurityHelpers.HashToken(request.LicenseKey);
            if (string.IsNullOrWhiteSpace(snapshot.CompanyCode))
                snapshot.CompanyCode = request?.CompanyCode;
            if (string.IsNullOrWhiteSpace(snapshot.CompanyName))
                snapshot.CompanyName = request?.CompanyName;
            if (string.IsNullOrWhiteSpace(snapshot.MachineFingerprintHash))
                snapshot.MachineFingerprintHash = _fingerprint.GetFingerprintHash();
            if (string.IsNullOrWhiteSpace(snapshot.ActivatedDeviceId))
                snapshot.ActivatedDeviceId = snapshot.MachineFingerprintHash;
            if (snapshot.SubscriptionStartDateUtc == default(DateTime))
                snapshot.SubscriptionStartDateUtc = snapshot.IssueDateUtc == default(DateTime) ? now : snapshot.IssueDateUtc;
            if (snapshot.SubscriptionEndDateUtc == default(DateTime))
                snapshot.SubscriptionEndDateUtc = snapshot.ExpiryDateUtc == default(DateTime) ? now.AddYears(1) : snapshot.ExpiryDateUtc;
            if (snapshot.IssueDateUtc == default(DateTime))
                snapshot.IssueDateUtc = snapshot.SubscriptionStartDateUtc;
            if (snapshot.ExpiryDateUtc == default(DateTime))
                snapshot.ExpiryDateUtc = snapshot.SubscriptionEndDateUtc;
            if (string.IsNullOrWhiteSpace(snapshot.SubscriptionStatus))
                snapshot.SubscriptionStatus = "Active";
            if (snapshot.MaxCompanies <= 0)
                snapshot.MaxCompanies = 1;
            if (snapshot.MaxDevices <= 0)
                snapshot.MaxDevices = LicensePlanCatalog.GetMaxDevices(snapshot.PlanType);
            if (snapshot.MaxUsers <= 0)
                snapshot.MaxUsers = snapshot.MaxDevices;
            if (snapshot.GracePeriodDays < 0)
                snapshot.GracePeriodDays = 0;
            if (snapshot.ActivatedDeviceCount <= 0)
                snapshot.ActivatedDeviceCount = 1;
            snapshot.OnlineValidationRequired = true;
            snapshot.LastSuccessfulValidationUtc = now;
            snapshot.LastAppOpenUtc = now;
            snapshot.LastTrustedServerTimeUtc = snapshot.LastTrustedServerTimeUtc ?? now;
            snapshot.LastServerValidationAttemptUtc = now;
            snapshot.Status = LicenseStatus.Active;
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
Status=@Status, MachineFingerprintHash=@MachineFingerprintHash, CompanyId=@CompanyId, CompanyCode=@CompanyCode,
SubscriptionStartDateUtc=@SubscriptionStartDateUtc, SubscriptionEndDateUtc=@SubscriptionEndDateUtc, SubscriptionStatus=@SubscriptionStatus,
LastServerValidationAttemptUtc=@LastServerValidationAttemptUtc, OnlineValidationRequired=@OnlineValidationRequired,
SupportLevel=@SupportLevel, PlanName=@PlanName, BillingCycle=@BillingCycle, Currency=@Currency, PriceAmount=@PriceAmount, RenewalPriceAmount=@RenewalPriceAmount,
IsLaunchOffer=@IsLaunchOffer, UpdatedUtc=GETUTCDATE()
WHERE LicenseKey=@LicenseKey
ELSE
INSERT INTO LicenseState (LicenseKey, PlanType, CompanyName, MaxCompanies, MaxDevices, MaxUsers, IssueDateUtc, ExpiryDateUtc,
LastSuccessfulValidationUtc, LastAppOpenUtc, LastTrustedServerTimeUtc, GracePeriodDays, Status, MachineFingerprintHash, SupportLevel,
PlanName, BillingCycle, Currency, PriceAmount, RenewalPriceAmount, IsLaunchOffer, CompanyId, CompanyCode, SubscriptionStartDateUtc,
SubscriptionEndDateUtc, SubscriptionStatus, LastServerValidationAttemptUtc, OnlineValidationRequired)
VALUES (@LicenseKey, @PlanType, @CompanyName, @MaxCompanies, @MaxDevices, @MaxUsers, @IssueDateUtc, @ExpiryDateUtc,
@LastSuccessfulValidationUtc, @LastAppOpenUtc, @LastTrustedServerTimeUtc, @GracePeriodDays, @Status, @MachineFingerprintHash, @SupportLevel,
@PlanName, @BillingCycle, @Currency, @PriceAmount, @RenewalPriceAmount, @IsLaunchOffer, @CompanyId, @CompanyCode, @SubscriptionStartDateUtc,
@SubscriptionEndDateUtc, @SubscriptionStatus, @LastServerValidationAttemptUtc, @OnlineValidationRequired);",
                DbExecutor.Param("@LicenseKey", s.LicenseKey),
                DbExecutor.Param("@CompanyId", (object)s.CompanyId ?? DBNull.Value),
                DbExecutor.Param("@CompanyCode", (object)s.CompanyCode ?? DBNull.Value),
                DbExecutor.Param("@PlanType", s.PlanType.ToString()),
                DbExecutor.Param("@CompanyName", s.CompanyName),
                DbExecutor.Param("@SubscriptionStartDateUtc", s.SubscriptionStartDateUtc == default(DateTime) ? s.IssueDateUtc : s.SubscriptionStartDateUtc),
                DbExecutor.Param("@SubscriptionEndDateUtc", s.SubscriptionEndDateUtc == default(DateTime) ? s.ExpiryDateUtc : s.SubscriptionEndDateUtc),
                DbExecutor.Param("@SubscriptionStatus", string.IsNullOrWhiteSpace(s.SubscriptionStatus) ? "Active" : s.SubscriptionStatus),
                DbExecutor.Param("@MaxCompanies", s.MaxCompanies),
                DbExecutor.Param("@MaxDevices", s.MaxDevices),
                DbExecutor.Param("@MaxUsers", s.MaxUsers),
                DbExecutor.Param("@IssueDateUtc", s.IssueDateUtc),
                DbExecutor.Param("@ExpiryDateUtc", s.ExpiryDateUtc),
                DbExecutor.Param("@LastSuccessfulValidationUtc", (object)s.LastSuccessfulValidationUtc ?? DBNull.Value),
                DbExecutor.Param("@LastAppOpenUtc", (object)s.LastAppOpenUtc ?? DBNull.Value),
                DbExecutor.Param("@LastTrustedServerTimeUtc", (object)s.LastTrustedServerTimeUtc ?? DBNull.Value),
                DbExecutor.Param("@LastServerValidationAttemptUtc", (object)s.LastServerValidationAttemptUtc ?? DBNull.Value),
                DbExecutor.Param("@GracePeriodDays", s.GracePeriodDays),
                DbExecutor.Param("@Status", s.Status.ToString()),
                DbExecutor.Param("@MachineFingerprintHash", s.MachineFingerprintHash),
                DbExecutor.Param("@OnlineValidationRequired", s.OnlineValidationRequired),
                DbExecutor.Param("@SupportLevel", s.SupportLevel),
                DbExecutor.Param("@PlanName", string.IsNullOrWhiteSpace(s.PlanName) ? LicensePlanCatalog.GetDisplayName(s.PlanType) : s.PlanName),
                DbExecutor.Param("@BillingCycle", string.IsNullOrWhiteSpace(s.BillingCycle) ? DefaultBillingCycle(s.PlanType) : s.BillingCycle),
                DbExecutor.Param("@Currency", string.IsNullOrWhiteSpace(s.Currency) ? "INR" : s.Currency),
                DbExecutor.Param("@PriceAmount", s.PriceAmount < 0 ? 0 : s.PriceAmount),
                DbExecutor.Param("@RenewalPriceAmount", s.RenewalPriceAmount < 0 ? 0 : s.RenewalPriceAmount),
                DbExecutor.Param("@IsLaunchOffer", s.IsLaunchOffer));
        }

        private void EnsurePricingColumns()
        {
            _db.Execute(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LicenseState')
CREATE TABLE LicenseState (
    LicenseStateId INT IDENTITY(1,1) PRIMARY KEY,
    LicenseKey NVARCHAR(100) NOT NULL UNIQUE,
    CompanyId NVARCHAR(80) NULL,
    CompanyCode NVARCHAR(80) NULL,
    PlanType NVARCHAR(30) NOT NULL,
    CompanyName NVARCHAR(200) NOT NULL,
    SubscriptionStartDateUtc DATETIME NULL,
    SubscriptionEndDateUtc DATETIME NULL,
    SubscriptionStatus NVARCHAR(30) NULL,
    MaxCompanies INT NOT NULL DEFAULT 1,
    MaxDevices INT NOT NULL DEFAULT 1,
    MaxUsers INT NOT NULL DEFAULT 1,
    IssueDateUtc DATETIME NOT NULL,
    ExpiryDateUtc DATETIME NOT NULL,
    LastSuccessfulValidationUtc DATETIME NULL,
    LastAppOpenUtc DATETIME NULL,
    LastTrustedServerTimeUtc DATETIME NULL,
    LastServerValidationAttemptUtc DATETIME NULL,
    GracePeriodDays INT NOT NULL DEFAULT 7,
    Status NVARCHAR(30) NOT NULL,
    MachineFingerprintHash NVARCHAR(200) NULL,
    OnlineValidationRequired BIT NOT NULL DEFAULT 0,
    SupportLevel NVARCHAR(80) NULL,
    PlanName NVARCHAR(80) NULL,
    BillingCycle NVARCHAR(40) NULL,
    Currency NVARCHAR(10) NULL,
    PriceAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    RenewalPriceAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsLaunchOffer BIT NOT NULL DEFAULT 0,
    CreatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE(),
    UpdatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE()
);
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LicenseEvents')
CREATE TABLE LicenseEvents (
    LicenseEventId INT IDENTITY(1,1) PRIMARY KEY,
    LicenseKey NVARCHAR(100) NULL,
    EventType NVARCHAR(80) NOT NULL,
    Message NVARCHAR(1000) NULL,
    DeviceFingerprintHash NVARCHAR(200) NULL,
    CreatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE()
);
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ActivatedDevices')
CREATE TABLE ActivatedDevices (
    ActivatedDeviceId INT IDENTITY(1,1) PRIMARY KEY,
    LicenseKey NVARCHAR(100) NOT NULL,
    DeviceFingerprintHash NVARCHAR(200) NOT NULL,
    DeviceName NVARCHAR(120) NULL,
    ActivatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE(),
    LastSeenUtc DATETIME NULL,
    Status NVARCHAR(30) NOT NULL DEFAULT 'Active',
    CONSTRAINT UQ_ActivatedDevices_License_Device UNIQUE (LicenseKey, DeviceFingerprintHash)
);
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='FeatureEntitlements')
CREATE TABLE FeatureEntitlements (
    FeatureEntitlementId INT IDENTITY(1,1) PRIMARY KEY,
    LicenseKey NVARCHAR(100) NOT NULL,
    ModuleKey NVARCHAR(50) NOT NULL,
    IsEnabled BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_FeatureEntitlements_License_Module UNIQUE (LicenseKey, ModuleKey)
);
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
    ALTER TABLE LicenseState ADD IsLaunchOffer BIT NOT NULL CONSTRAINT DF_LicenseState_IsLaunchOffer DEFAULT 0;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'CompanyId') IS NULL
    ALTER TABLE LicenseState ADD CompanyId NVARCHAR(80) NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'CompanyCode') IS NULL
    ALTER TABLE LicenseState ADD CompanyCode NVARCHAR(80) NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'SubscriptionStartDateUtc') IS NULL
    ALTER TABLE LicenseState ADD SubscriptionStartDateUtc DATETIME NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'SubscriptionEndDateUtc') IS NULL
    ALTER TABLE LicenseState ADD SubscriptionEndDateUtc DATETIME NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'SubscriptionStatus') IS NULL
    ALTER TABLE LicenseState ADD SubscriptionStatus NVARCHAR(30) NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'LastServerValidationAttemptUtc') IS NULL
    ALTER TABLE LicenseState ADD LastServerValidationAttemptUtc DATETIME NULL;
IF OBJECT_ID('LicenseState', 'U') IS NOT NULL AND COL_LENGTH('LicenseState', 'OnlineValidationRequired') IS NULL
    ALTER TABLE LicenseState ADD OnlineValidationRequired BIT NOT NULL CONSTRAINT DF_LicenseState_OnlineValidationRequired DEFAULT 0;");
        }

        private static string DefaultBillingCycle(LicensePlanType plan)
        {
            return plan == LicensePlanType.Trial ? "14 days" : "Annual";
        }

        private void PersistDevice(LicenseSnapshot s)
        {
            EnsurePricingColumns();
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
            EnsurePricingColumns();
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
