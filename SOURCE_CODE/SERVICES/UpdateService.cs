using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
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
        public bool RequiresLegacyInstallerMigration { get; set; }
        internal UpdateInfo VelopackUpdateInfo { get; set; }
        internal string LegacyInstallerPath { get; set; }
    }

    internal sealed class GitHubReleaseAssetInfo
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    internal sealed class GitHubLatestReleaseInfo
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public string body { get; set; }
        public GitHubReleaseAssetInfo[] assets { get; set; }
    }

    public static class UpdateService
    {
        public const string DefaultGitHubRepositoryUrl = "https://github.com/harshals499/ServoERP";
        private const string UpdatesFolder = @"C:\HVAC_PRO_MSE\UPDATES";
        private const string LogContext = "Velopack update";
        private const string PendingWhatsNewVersionKey = "PendingWhatsNewVersion";
        private const string PendingWhatsNewTextKey = "PendingWhatsNewTextEn";
        private const string LegacyPendingWhatsNewTextKey = "PendingWhatsNewTextMr";
        private const string SilentAutoUpdateModeKey = "SilentAutoUpdateMode";
        private const string SilentAutoUpdateAutomaticMode = "Automatic";
        private const string SilentAutoUpdateDisabledMode = "Disabled";
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

        /// <summary>Starts a best-effort background update check and silent download. A verified package is applied when ServoERP closes.</summary>
        public static void StartSilentBackgroundUpdateCheck(Control owner = null, Action<UpdateCheckResult> downloadedNotification = null)
        {
            EnsureSilentAutoUpdateDefaults();

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
                        SaveLastStatus("Update v" + result.LatestVersion + " downloaded in the background and will install when ServoERP closes.");
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

        /// <summary>Applies a successfully downloaded silent update when ServoERP is closing.</summary>
        public static bool TryApplySilentUpdateOnExit()
        {
            if (!ConfigService.IsSilentAutoUpdateEnabled() || !ConfigService.ShouldApplySilentUpdateOnExit())
            {
                AppLogger.LogInfo(LogContext + " apply-on-exit skipped: silent apply is disabled.");
                return false;
            }

            UpdateCheckResult downloadedUpdate;
            lock (SilentUpdateSync)
            {
                downloadedUpdate = _downloadedSilentUpdate;
            }

            if (downloadedUpdate == null || !downloadedUpdate.CanApplyUpdate)
            {
                AppLogger.LogInfo(LogContext + " apply-on-exit skipped: no downloaded package is ready.");
                return false;
            }

            try
            {
                BackupConfigurationFiles(downloadedUpdate.LatestVersion);
                StagePostUpdateNotice(downloadedUpdate.LatestVersion, BuildChangelogText(downloadedUpdate.VelopackUpdateInfo));
                SaveLastStatus("Installing downloaded update v" + downloadedUpdate.LatestVersion + " as ServoERP closes.");
                AppLogger.LogInfo(LogContext + " apply-on-exit requested. latest=" + downloadedUpdate.LatestVersion);

                if (downloadedUpdate.RequiresLegacyInstallerMigration)
                {
                    ScheduleLegacyInstallerAfterExit(downloadedUpdate);
                    return true;
                }

                UpdateManager manager = CreateManager(GetGitHubRepositoryUrl());
                manager.WaitExitThenApplyUpdates(downloadedUpdate.VelopackUpdateInfo.TargetFullRelease, true, true, null);
                return true;
            }
            catch (Exception ex)
            {
                SaveLastStatus("Downloaded update v" + downloadedUpdate.LatestVersion + " will be retried later. ServoERP closed normally.");
                AppLogger.LogError("UpdateService.TryApplySilentUpdateOnExit", ex);
                return false;
            }
        }

        /// <summary>Upgrades legacy client settings to the automatic update default once without overriding a later user choice.</summary>
        public static void EnsureSilentAutoUpdateDefaults()
        {
            string mode = ConfigService.Get("App", SilentAutoUpdateModeKey, string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(mode))
                return;

            try
            {
                ConfigService.Set("App", "SilentAutoUpdateEnabled", "true");
                ConfigService.Set("App", "SilentAutoUpdateApplyImmediately", "false");
                ConfigService.Set("App", "SilentAutoUpdateApplyOnExit", "true");
                ConfigService.Set("App", SilentAutoUpdateModeKey, SilentAutoUpdateAutomaticMode);
                AppLogger.LogInfo(LogContext + " migrated legacy settings to automatic download and apply-on-exit.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService.EnsureSilentAutoUpdateDefaults", ex);
            }
        }

        public static void SetSilentAutoUpdatePreference(bool enabled)
        {
            ConfigService.Set("App", "SilentAutoUpdateEnabled", enabled ? "true" : "false");
            ConfigService.Set("App", "SilentAutoUpdateApplyImmediately", "false");
            ConfigService.Set("App", "SilentAutoUpdateApplyOnExit", enabled ? "true" : "false");
            ConfigService.Set("App", SilentAutoUpdateModeKey, enabled ? SilentAutoUpdateAutomaticMode : SilentAutoUpdateDisabledMode);
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
                AppLogger.LogInfo(LogContext + " skipped: not a Velopack install. " + ex.Message);
                return await CheckLatestReleaseForManualInstallAsync(result, cancellationToken).ConfigureAwait(false);
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
            SaveLastStatus("ServoERP v" + result.LatestVersion + " downloaded silently and will install when ServoERP closes.");
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
            if (update.RequiresLegacyInstallerMigration)
                return await DownloadLegacyInstallerAsync(update, progress, cancellationToken).ConfigureAwait(false);
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
            if (update.RequiresLegacyInstallerMigration)
            {
                EnsureSafeToApplyUpdate();
                BackupConfigurationFiles(update.LatestVersion);
                ScheduleLegacyInstallerAfterExit(update);
                SaveLastStatus("Installing ServoERP v" + update.LatestVersion + " silently after ServoERP closes...");
                Application.Exit();
                return;
            }
            if (update.VelopackUpdateInfo == null || update.VelopackUpdateInfo.TargetFullRelease == null)
                throw new InvalidOperationException("No downloaded Velopack update is ready to apply.");
            EnsureSafeToApplyUpdate();

            BackupConfigurationFiles(update.LatestVersion);
            StagePostUpdateNotice(update.LatestVersion, BuildChangelogText(update.VelopackUpdateInfo));
            SaveLastStatus("Applying update v" + update.LatestVersion + " and restarting ServoERP...");
            AppLogger.LogInfo(LogContext + " apply requested. latest=" + update.LatestVersion);

            UpdateManager manager = CreateManager(GetGitHubRepositoryUrl());
            manager.ApplyUpdatesAndRestart(update.VelopackUpdateInfo.TargetFullRelease, null);
        }

        /// <summary>Returns the English What's New notice once after a successful Velopack restart.</summary>
        public static bool TryConsumePostUpdateNotice(out string version, out string text)
        {
            version = ConfigService.Get("App", PendingWhatsNewVersionKey, string.Empty).Trim();
            text = ConfigService.Get("App", PendingWhatsNewTextKey, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(ConfigService.Get("App", LegacyPendingWhatsNewTextKey, string.Empty)))
                text = BuildEnglishWhatsNewText(version, string.Empty);
            string current = GetCurrentAssemblyVersion();
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(text) || !string.Equals(version, current, StringComparison.OrdinalIgnoreCase))
                return false;

            ConfigService.Set("App", PendingWhatsNewVersionKey, string.Empty);
            ConfigService.Set("App", PendingWhatsNewTextKey, string.Empty);
            ConfigService.Set("App", LegacyPendingWhatsNewTextKey, string.Empty);
            return true;
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

        private static async Task<UpdateCheckResult> CheckLatestReleaseForManualInstallAsync(UpdateCheckResult result, CancellationToken cancellationToken)
        {
            if (result == null)
                result = new UpdateCheckResult();

            string repositoryUrl = GetGitHubRepositoryUrl();
            try
            {
                string apiUrl = BuildLatestReleaseApiUrl(repositoryUrl);
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("ServoERP-Desktop-Updater");
                    string response = await client.GetStringAsync(apiUrl).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    var serializer = new JavaScriptSerializer();
                    GitHubLatestReleaseInfo release = serializer.Deserialize<GitHubLatestReleaseInfo>(response);
                    string latestVersion = NormalizeReleaseVersion(release == null ? null : release.tag_name);
                    string currentVersion = string.IsNullOrWhiteSpace(result.CurrentVersion) ? GetCurrentAssemblyVersion() : result.CurrentVersion;

                    result.CurrentVersion = currentVersion;
                    result.LatestVersion = string.IsNullOrWhiteSpace(latestVersion) ? currentVersion : latestVersion;
                    result.DownloadUrl = SelectPreferredInstallerUrl(release) ?? (release == null ? string.Empty : release.html_url) ?? (repositoryUrl + "/releases/latest");
                    result.PackageUrl = Path.GetFileName(result.DownloadUrl ?? string.Empty);
                    result.ChangelogText = string.IsNullOrWhiteSpace(release == null ? null : release.body)
                        ? "Install the latest ServoERP Desktop package to update this copy."
                        : release.body.Trim();

                    if (!IsNewerVersion(result.LatestVersion, currentVersion))
                    {
                        result.IsUpdateAvailable = false;
                        result.CanApplyUpdate = false;
                        result.StatusMessage = "ServoERP is up to date. This copy is not using the Desktop installer update channel.";
                        SaveLastStatus(result.StatusMessage);
                        return result;
                    }

                    result.IsUpdateAvailable = true;
                    result.RequiresLegacyInstallerMigration = Uri.TryCreate(result.DownloadUrl, UriKind.Absolute, out Uri installerUri) &&
                                                                      installerUri.Scheme == Uri.UriSchemeHttps &&
                                                                      installerUri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                    result.CanApplyUpdate = result.RequiresLegacyInstallerMigration;
                    result.StatusMessage = result.CanApplyUpdate
                        ? "Update available: v" + result.LatestVersion + ". ServoERP will migrate this older installation to silent automatic updates."
                        : "Update available: v" + result.LatestVersion + ", but no compatible Desktop installer was found.";
                    SaveLastStatus(result.StatusMessage);
                    AppLogger.LogInfo(LogContext + " legacy-install migration available. latest=" + result.LatestVersion + " url=" + result.DownloadUrl);
                    return result;
                }
            }
            catch (Exception manualEx)
            {
                result.StatusMessage = "ServoERP is running normally. Automatic updates will activate from the installed Desktop shortcut.";
                SaveLastStatus(result.StatusMessage);
                AppLogger.LogInfo(LogContext + " manual fallback failed: " + manualEx.Message);
                return result;
            }
        }

        private static async Task<string> DownloadLegacyInstallerAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(update.DownloadUrl) ||
                !Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out Uri installerUri) ||
                installerUri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("A secure ServoERP installer download was not available.");

            Directory.CreateDirectory(UpdatesFolder);
            string targetPath = Path.Combine(UpdatesFolder, "ServoERP-Setup-" + NormalizeReleaseVersion(update.LatestVersion) + ".exe");
            string temporaryPath = targetPath + ".download";
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            SaveLastStatus("Downloading ServoERP v" + update.LatestVersion + " silently...");
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
            using (HttpResponseMessage response = await client.GetAsync(installerUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? -1L;
                using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        received += read;
                        if (progress != null && total > 0)
                            progress.Report((int)Math.Min(100L, received * 100L / total));
                    }
                }
            }

            if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length < 1024 * 1024)
                throw new InvalidDataException("The downloaded ServoERP installer was incomplete.");

            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(temporaryPath, targetPath);
            update.LegacyInstallerPath = targetPath;
            SaveLastStatus("ServoERP v" + update.LatestVersion + " downloaded and will install silently when ServoERP closes.");
            return targetPath;
        }

        private static void ScheduleLegacyInstallerAfterExit(UpdateCheckResult update)
        {
            string installerPath = update == null ? null : update.LegacyInstallerPath;
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
                throw new FileNotFoundException("The downloaded ServoERP installer could not be found.", installerPath);

            int processId = Process.GetCurrentProcess().Id;
            string escapedInstaller = installerPath.Replace("'", "''");
            string command = "$p=Get-Process -Id " + processId.ToString(CultureInfo.InvariantCulture) + " -ErrorAction SilentlyContinue; " +
                             "if($p){$p.WaitForExit()}; Start-Sleep -Milliseconds 750; " +
                             "Start-Process -FilePath '" + escapedInstaller + "' -ArgumentList '--silent' -WindowStyle Hidden";
            string encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            AppLogger.LogInfo(LogContext + " scheduled silent legacy migration. latest=" + update.LatestVersion);
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

        private static void StagePostUpdateNotice(string version, string releaseNotes)
        {
            try
            {
                ConfigService.Set("App", PendingWhatsNewVersionKey, version ?? string.Empty);
                ConfigService.Set("App", PendingWhatsNewTextKey, BuildEnglishWhatsNewText(version, releaseNotes));
                ConfigService.Set("App", LegacyPendingWhatsNewTextKey, string.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService.StagePostUpdateNotice", ex);
            }
        }

        internal static string BuildEnglishWhatsNewTextForTest(string version, string releaseNotes)
        {
            return BuildEnglishWhatsNewText(version, releaseNotes);
        }

        private static string BuildEnglishWhatsNewText(string version, string releaseNotes)
        {
            string notes = (releaseNotes ?? string.Empty).Trim();
            int marker = notes.IndexOf("[मराठी]", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
                notes = notes.Substring(0, marker).Trim();

            notes = notes.Replace("[English]", string.Empty).Trim();
            string[] englishLines = notes
                .Replace("\r\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !ContainsDevanagari(line))
                .ToArray();
            notes = string.Join("\r\n", englishLines);

            if (string.IsNullOrWhiteSpace(notes))
                notes = "- Performance, stability, and security improvements.";

            return "ServoERP version " + (version ?? string.Empty) + " was updated successfully.\r\n\r\nWhat's new:\r\n" + notes;
        }

        private static bool ContainsDevanagari(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Any(character => character >= '\u0900' && character <= '\u097F');
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

        private static string BuildLatestReleaseApiUrl(string repositoryUrl)
        {
            string trimmed = (repositoryUrl ?? string.Empty).Trim().TrimEnd('/');
            Uri uri;
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out uri))
                throw new InvalidOperationException("Invalid GitHub repository URL: " + repositoryUrl);

            string[] segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2)
                throw new InvalidOperationException("GitHub repository URL must include owner and repository name.");

            return "https://api.github.com/repos/" + segments[0] + "/" + segments[1] + "/releases/latest";
        }

        private static string NormalizeReleaseVersion(string tagName)
        {
            string text = (tagName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text.TrimStart('v', 'V');
        }

        private static string SelectPreferredInstallerUrl(GitHubLatestReleaseInfo release)
        {
            GitHubReleaseAssetInfo[] assets = release == null ? null : release.assets;
            if (assets == null || assets.Length == 0)
                return null;

            GitHubReleaseAssetInfo preferred = null;
            foreach (GitHubReleaseAssetInfo asset in assets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.browser_download_url))
                    continue;

                string name = asset.name ?? string.Empty;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    name.StartsWith("ServoERP_Setup_", StringComparison.OrdinalIgnoreCase))
                    return asset.browser_download_url;

                if (preferred == null &&
                    string.Equals(name, "ServoERP.Desktop-win-Setup.exe", StringComparison.OrdinalIgnoreCase))
                    preferred = asset;
            }

            return preferred == null ? null : preferred.browser_download_url;
        }
    }
}
