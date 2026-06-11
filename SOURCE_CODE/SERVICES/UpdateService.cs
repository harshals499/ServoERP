using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class UpdateCheckResult
    {
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string PackageUrl { get; set; }
        public string ChangelogText { get; set; }
        public string StatusMessage { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public bool CanApplyUpdate { get; set; }
        internal UpdateInfo VelopackUpdateInfo { get; set; }
    }

    public static class UpdateService
    {
        public const string DefaultGitHubRepositoryUrl = "https://github.com/harshals499/ServoERP";
        private const string UpdatesFolder = @"C:\HVAC_PRO_MSE\UPDATES";
        private const string LogContext = "Velopack update";
        private static readonly object SilentUpdateSync = new object();
        private static bool _silentUpdateWorkerRunning;
        private static UpdateCheckResult _downloadedSilentUpdate;

        public static string GetGitHubRepositoryUrl()
        {
            string configured = ConfigService.Get("App", "GitHubRepositoryUrl", DefaultGitHubRepositoryUrl);
            return string.IsNullOrWhiteSpace(configured) ? DefaultGitHubRepositoryUrl : configured.Trim().TrimEnd('/');
        }

        public static string GetCurrentAssemblyVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "0.0.0" : ToSemVer(version);
        }

        public static string GetLastUpdateStatus()
        {
            return ConfigService.Get("App", "LastUpdateCheckStatus", "Updates have not been checked in this session.");
        }

        public static string GetLastUpdateStatusDisplay()
        {
            string status = GetLastUpdateStatus();
            string rawUtc = ConfigService.Get("App", "LastUpdateCheckUtc", string.Empty);
            DateTime checkedUtc;
            if (DateTime.TryParse(rawUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out checkedUtc))
            {
                string local = checkedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                return status + Environment.NewLine + "Last checked: " + local;
            }

            return status + Environment.NewLine + "Last checked: Never";
        }

        /// <summary>Starts a best-effort background update check and silent download. Restart still requires user confirmation.</summary>
        public static void StartSilentBackgroundUpdateCheck(Control owner = null, Action<UpdateCheckResult> downloadedNotification = null)
        {
            if (!ConfigService.IsVersionCheckEnabled() || !ConfigService.IsSilentAutoUpdateEnabled())
            {
                AppLogger.LogInfo(LogContext + " silent check skipped: disabled.");
                return;
            }

            if (!ShouldRunSilentUpdateCheck())
            {
                AppLogger.LogInfo(LogContext + " silent check skipped: interval not reached.");
                return;
            }

            lock (SilentUpdateSync)
            {
                if (_silentUpdateWorkerRunning)
                    return;

                _silentUpdateWorkerRunning = true;
            }

            Task.Run(async () =>
            {
                UpdateCheckResult result = null;
                try
                {
                    result = RunSilentUpdateCheckAndDownload();
                    if (result != null && result.IsUpdateAvailable && result.CanApplyUpdate)
                    {
                        SaveLastStatus("Update v" + result.LatestVersion + " is downloaded. Open Settings > About & Updates to install and restart.");
                        ServoERP.Infrastructure.UIThread.Post(owner, () => downloadedNotification?.Invoke(result));
                        await Task.CompletedTask.ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    SaveLastStatus("Silent update check failed. ServoERP will continue normally. " + ex.Message);
                    AppLogger.LogError("UpdateService.StartSilentBackgroundUpdateCheck", ex);
                }
                finally
                {
                    lock (SilentUpdateSync)
                    {
                        _silentUpdateWorkerRunning = false;
                    }
                }
            });
        }

        /// <summary>Applies a downloaded silent update when ServoERP is closing.</summary>
        public static bool TryApplySilentUpdateOnExit()
        {
            AppLogger.LogInfo(LogContext + " apply-on-exit skipped: ServoERP requires user confirmation before restart.");
            return false;
        }

        public static Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            return CheckForUpdatesAsync(CancellationToken.None);
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            string currentVersion = GetCurrentAssemblyVersion();
            var result = new UpdateCheckResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = currentVersion,
                DownloadUrl = GetGitHubRepositoryUrl() + "/releases/latest",
                PackageUrl = string.Empty,
                ChangelogText = string.Empty,
                StatusMessage = "No update checked yet.",
                IsUpdateAvailable = false,
                CanApplyUpdate = false
            };

            try
            {
                if (!ConfigService.IsVersionCheckEnabled())
                {
                    result.StatusMessage = "Update checks are turned off in Settings.";
                    SaveLastStatus(result.StatusMessage);
                    return result;
                }

                string repositoryUrl = GetGitHubRepositoryUrl();
                AppLogger.LogInfo(LogContext + " check started. source=" + repositoryUrl + " current=" + currentVersion);
                ConfigService.Set("App", "LastUpdateCheckUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

                UpdateManager manager = CreateManager(repositoryUrl);
                string installedVersion = GetInstalledPackageVersion(manager, currentVersion);
                string effectiveCurrentVersion = GetNewestVersion(currentVersion, installedVersion);
                UpdateInfo update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (update == null)
                {
                    result.StatusMessage = "ServoERP is up to date. Checked " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + ".";
                    SaveLastStatus(result.StatusMessage);
                    return result;
                }

                result.LatestVersion = update.TargetFullRelease == null ? currentVersion : update.TargetFullRelease.Version.ToString();
                if (update.TargetFullRelease == null || !IsNewerVersion(result.LatestVersion, effectiveCurrentVersion))
                {
                    result.LatestVersion = effectiveCurrentVersion;
                    result.StatusMessage = "ServoERP is up to date. Checked " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + ".";
                    SaveLastStatus(result.StatusMessage);
                    AppLogger.LogInfo(
                        LogContext + " ignored non-newer target. current=" + currentVersion +
                        " installed=" + installedVersion +
                        " target=" + (update.TargetFullRelease == null ? "(none)" : update.TargetFullRelease.Version.ToString()));
                    return result;
                }

                result.VelopackUpdateInfo = update;
                result.PackageUrl = update.TargetFullRelease == null ? string.Empty : update.TargetFullRelease.FileName;
                result.ChangelogText = BuildChangelogText(update);
                result.IsUpdateAvailable = true;
                result.CanApplyUpdate = update.TargetFullRelease != null;
                result.StatusMessage = "Update available: v" + result.LatestVersion + ".";
                SaveLastStatus(result.StatusMessage);
                AppLogger.LogInfo(LogContext + " available. latest=" + result.LatestVersion + " package=" + result.PackageUrl);
                return result;
            }
            catch (NotInstalledException ex)
            {
                result.StatusMessage = "ServoERP is running normally. Automatic updates will activate from the installed Desktop shortcut.";
                SaveLastStatus(result.StatusMessage);
                AppLogger.LogInfo(LogContext + " skipped: not a Velopack install. " + ex.Message);
                return result;
            }
            catch (Exception ex)
            {
                result.StatusMessage = "Update check failed. ServoERP will continue normally. " + ex.Message;
                SaveLastStatus(result.StatusMessage);
                AppLogger.LogError("UpdateService.CheckForUpdatesAsync", ex);
                result.IsUpdateAvailable = false;
                result.CanApplyUpdate = false;
                return result;
            }
        }

        /// <summary>Runs the silent update check and download on a BackgroundWorker thread.</summary>
        private static UpdateCheckResult RunSilentUpdateCheckAndDownload()
        {
            MarkSilentUpdateCheckAttempt();
            UpdateCheckResult result = CheckForUpdatesAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (result == null || !result.IsUpdateAvailable || !result.CanApplyUpdate)
                return result;

            AppLogger.LogInfo(LogContext + " silent download starting. latest=" + result.LatestVersion);
            DownloadUpdatePackageAsync(result, null, CancellationToken.None).GetAwaiter().GetResult();
            BackupConfigurationFiles(result.LatestVersion);

            lock (SilentUpdateSync)
            {
                _downloadedSilentUpdate = result;
            }

            ConfigService.Set("App", "PendingSilentUpdateVersion", result.LatestVersion ?? string.Empty);
            SaveLastStatus("ServoERP v" + result.LatestVersion + " downloaded silently. Open Settings > About & Updates to install and restart.");
            AppLogger.LogInfo(LogContext + " silent download ready. latest=" + result.LatestVersion);
            return result;
        }

        /// <summary>Checks the configured silent update interval.</summary>
        private static bool ShouldRunSilentUpdateCheck()
        {
            string raw = ConfigService.Get("App", "LastSilentUpdateCheckUtc", string.Empty);
            DateTime lastCheckUtc;
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out lastCheckUtc))
                return true;

            int intervalHours = ConfigService.GetVersionCheckIntervalHours();
            return DateTime.UtcNow.Subtract(lastCheckUtc).TotalHours >= intervalHours;
        }

        /// <summary>Records a silent update check attempt before network work starts.</summary>
        private static void MarkSilentUpdateCheckAttempt()
        {
            ConfigService.Set("App", "LastSilentUpdateCheckUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        }

        public static async Task<string> DownloadUpdatePackageAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));
            if (update.VelopackUpdateInfo == null || update.VelopackUpdateInfo.TargetFullRelease == null)
                throw new InvalidOperationException("No Velopack update package is available to download.");

            Directory.CreateDirectory(UpdatesFolder);
            string repositoryUrl = GetGitHubRepositoryUrl();
            SaveLastStatus("Downloading update v" + update.LatestVersion + "...");
            AppLogger.LogInfo(LogContext + " download started. latest=" + update.LatestVersion);

            UpdateManager manager = CreateManager(repositoryUrl);
            Action<int> progressAction = value =>
            {
                if (progress != null)
                    progress.Report(value);
            };
            await manager.DownloadUpdatesAsync(update.VelopackUpdateInfo, progressAction, cancellationToken).ConfigureAwait(false);
            string status = "Update v" + update.LatestVersion + " downloaded. Ready to restart.";
            SaveLastStatus(status);
            AppLogger.LogInfo(LogContext + " download completed. latest=" + update.LatestVersion);
            return update.VelopackUpdateInfo.TargetFullRelease.FileName;
        }

        public static void ApplyUpdateAndRestart(UpdateCheckResult update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));
            if (update.VelopackUpdateInfo == null || update.VelopackUpdateInfo.TargetFullRelease == null)
                throw new InvalidOperationException("No downloaded Velopack update is ready to apply.");
            EnsureSafeToApplyUpdate();

            BackupConfigurationFiles(update.LatestVersion);
            SaveLastStatus("Applying update v" + update.LatestVersion + " and restarting ServoERP...");
            AppLogger.LogInfo(LogContext + " apply requested. latest=" + update.LatestVersion);

            UpdateManager manager = CreateManager(GetGitHubRepositoryUrl());
            manager.ApplyUpdatesAndRestart(update.VelopackUpdateInfo.TargetFullRelease, null);
        }

        public static void StartPackageUpdater(string packagePath)
        {
            throw new NotSupportedException("Legacy ZIP updates are disabled. ServoERP now applies updates through Velopack packages from GitHub Releases.");
        }

        public static void StartInstallerElevated(string installerPath)
        {
            throw new NotSupportedException("Manual installer launching is disabled for auto-updates. Upload and install Velopack setup assets from GitHub Releases.");
        }

        private static UpdateManager CreateManager(string repositoryUrl)
        {
            return new UpdateManager(new GithubSource(repositoryUrl, null, false, null), null, null);
        }

        private static void EnsureSafeToApplyUpdate()
        {
            try
            {
                if (Application.OpenForms == null)
                    return;

                foreach (Form form in Application.OpenForms)
                {
                    if (form == null || form.IsDisposed || !form.Visible)
                        continue;

                    string name = form.GetType().Name;
                    if (string.Equals(name, "MainForm", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (string.Equals(form.Text, "Downloading ServoERP update", StringComparison.OrdinalIgnoreCase))
                        continue;

                    throw new InvalidOperationException("Close active ServoERP windows and finish any save operation before installing the update.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService.EnsureSafeToApplyUpdate", ex);
                throw new InvalidOperationException("ServoERP could not confirm that the app is idle. Update cancelled to protect client data.", ex);
            }
        }

        private static void BackupConfigurationFiles(string version)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string safeVersion = string.IsNullOrWhiteSpace(version) ? "unknown" : version.Replace("/", "-").Replace("\\", "-");
                string backupDir = Path.Combine(UpdatesFolder, "config-backup-v" + safeVersion + "-" + timestamp);
                Directory.CreateDirectory(backupDir);

                CopyIfExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.config"), Path.Combine(backupDir, "HVACPro.config"));
                CopyIfExists(@"C:\HVAC_PRO_MSE\HVACPro.config", Path.Combine(backupDir, "HVACPro.root.config"));
                CopyIfExists(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile, Path.Combine(backupDir, "HVAC_Pro_Desktop.exe.config"));
                AppLogger.LogInfo(LogContext + " config backup created: " + backupDir);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService.BackupConfigurationFiles", ex);
                throw new InvalidOperationException("Could not back up ServoERP configuration before applying update. Update cancelled.", ex);
            }
        }

        private static void CopyIfExists(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? UpdatesFolder);
            File.Copy(source, destination, true);
        }

        private static string BuildChangelogText(UpdateInfo update)
        {
            if (update == null || update.TargetFullRelease == null)
                return "No release notes were provided for this version.";

            string notes = update.TargetFullRelease.NotesMarkdown;
            if (string.IsNullOrWhiteSpace(notes))
                notes = update.TargetFullRelease.NotesHTML;

            return string.IsNullOrWhiteSpace(notes)
                ? "No release notes were provided for this version."
                : notes.Trim();
        }

        private static void SaveLastStatus(string status)
        {
            try
            {
                string text = (status ?? string.Empty).Trim();
                ConfigService.Set("App", "LastUpdateCheckStatus", text);
                AppLogger.LogInfo(LogContext + " status: " + text);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService.SaveLastStatus", ex);
            }
        }

        /// <summary>Returns the Velopack installed package version, falling back to the running assembly version.</summary>
        private static string GetInstalledPackageVersion(UpdateManager manager, string fallbackVersion)
        {
            try
            {
                string version = manager == null || manager.CurrentVersion == null
                    ? null
                    : manager.CurrentVersion.ToString();
                return string.IsNullOrWhiteSpace(version) ? fallbackVersion : version;
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo(LogContext + " installed version read failed: " + ex.Message);
                return fallbackVersion;
            }
        }

        /// <summary>Returns the newest parseable version from two version strings.</summary>
        private static string GetNewestVersion(string first, string second)
        {
            Version firstVersion;
            Version secondVersion;
            if (TryParseComparableVersion(first, out firstVersion) && TryParseComparableVersion(second, out secondVersion))
                return secondVersion > firstVersion ? second : first;

            return string.IsNullOrWhiteSpace(first) ? second : first;
        }

        /// <summary>Checks whether latest is greater than current after normalizing missing revision parts.</summary>
        private static bool IsNewerVersion(string latest, string current)
        {
            Version latestVersion;
            Version currentVersion;
            return TryParseComparableVersion(latest, out latestVersion)
                && TryParseComparableVersion(current, out currentVersion)
                && latestVersion > currentVersion;
        }

        /// <summary>Parses semantic versions like 1.0.148 and assembly versions like 1.0.148.0 as comparable four-part versions.</summary>
        private static bool TryParseComparableVersion(string value, out Version version)
        {
            version = null;
            string text = (value ?? string.Empty).Trim().TrimStart('v', 'V');
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int suffixIndex = text.IndexOfAny(new[] { '-', '+' });
            if (suffixIndex >= 0)
                text = text.Substring(0, suffixIndex);

            string[] parts = text.Split('.');
            if (parts.Length < 2 || parts.Length > 4)
                return false;

            int[] numbers = new[] { 0, 0, 0, 0 };
            for (int i = 0; i < parts.Length; i++)
            {
                int parsed;
                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < 0)
                    return false;

                numbers[i] = parsed;
            }

            version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        private static string ToSemVer(Version version)
        {
            int patch = Math.Max(0, version.Build);
            return version.Major.ToString(CultureInfo.InvariantCulture) + "." +
                   version.Minor.ToString(CultureInfo.InvariantCulture) + "." +
                   patch.ToString(CultureInfo.InvariantCulture);
        }
    }
}
