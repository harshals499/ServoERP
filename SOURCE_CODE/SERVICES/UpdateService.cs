// Release update checklist:
// 1. Bump version in AssemblyInfo.cs
// 2. Build Release in Visual Studio
// 3. Create app update ZIP from bin\Release
// 4. Upload update ZIP to servoerp.in/updates/
// 5. Update version.txt and changelog.json on Cloudflare Pages
// Every client PC gets the update on next app launch.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class UpdateCheckResult
    {
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string PackageUrl { get; set; }
        public string ChangelogText { get; set; }
        public bool IsUpdateAvailable { get; set; }
    }

    public static class UpdateService
    {
        private const string VersionUrl = "https://servoerp.in/version.txt";
        private const string ChangelogUrl = "https://servoerp.in/changelog.json";
        private const string DefaultInstallerDownloadUrl = "https://servoerp.in/download/";
        private const string DefaultPackageBaseUrl = "https://servoerp.in/updates/";
        private const string UpdatesFolder = @"C:\HVAC_PRO_MSE\UPDATES";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(6);

        public static Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            return CheckForUpdatesAsync(CancellationToken.None);
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            var result = new UpdateCheckResult
            {
                CurrentVersion = GetCurrentAssemblyVersion(),
                LatestVersion = GetCurrentAssemblyVersion(),
                DownloadUrl = GetInstallerDownloadUrl(),
                PackageUrl = string.Empty,
                ChangelogText = string.Empty,
                IsUpdateAvailable = false
            };

            try
            {
                using (var client = new HttpClient())
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    client.Timeout = RequestTimeout;
                    timeout.CancelAfter(RequestTimeout);

                    Task<string> versionTask = client.GetStringAsync(VersionUrl);
                    Task<string> changelogTask = client.GetStringAsync(ChangelogUrl);

                    string latestVersionText = (await versionTask.ConfigureAwait(false) ?? string.Empty).Trim();
                    if (!IsValidVersion(latestVersionText))
                        return result;

                    result.LatestVersion = latestVersionText;
                    result.IsUpdateAvailable = IsNewerVersion(result.LatestVersion, result.CurrentVersion);

                    if (result.IsUpdateAvailable)
                    {
                        string changelogJson = string.Empty;
                        try
                        {
                            changelogJson = await changelogTask.ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            AppLogger.LogInfo("Update changelog fetch failed silently: " + ex.Message);
                        }

                        ResolveDownloadUrls(changelogJson, result);
                        result.ChangelogText = BuildChangelogText(changelogJson, result.LatestVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo("Startup update check failed silently: " + ex.Message);
                result.IsUpdateAvailable = false;
            }

            return result;
        }

        public static async Task<string> DownloadUpdatePackageAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            string packageUrl = update.PackageUrl;
            if (string.IsNullOrWhiteSpace(packageUrl))
                packageUrl = DefaultPackageBaseUrl + "ServoERP_Update_" + update.LatestVersion + ".zip";

            Directory.CreateDirectory(UpdatesFolder);
            string targetPath = Path.Combine(UpdatesFolder, "ServoERP_Update_" + update.LatestVersion + ".zip");
            return await DownloadFileAsync(packageUrl, targetPath, "update package", progress, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<string> DownloadInstallerAsync(UpdateCheckResult update, IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            string downloadUrl = update.DownloadUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new InvalidOperationException("The update download URL is not configured.");

            string fileName = "ServoERP_Setup_" + update.LatestVersion + ".exe";
            string targetPath = Path.Combine(UpdatesFolder, fileName);
            return await DownloadFileAsync(downloadUrl, targetPath, "installer", progress, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> DownloadFileAsync(string downloadUrl, string targetPath, string expectedName, IProgress<int> progress, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? UpdatesFolder);
            string tempPath = targetPath + ".download";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(15);
                downloadUrl = await ResolveDirectDownloadUrlAsync(client, downloadUrl, cancellationToken).ConfigureAwait(false);

                using (HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    string contentType = response.Content.Headers.ContentType == null
                        ? string.Empty
                        : response.Content.Headers.ContentType.MediaType;
                    if (contentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("The update link returned a web page, not a direct " + expectedName + " download.");

                    long totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
                    long receivedBytes = 0;
                    byte[] buffer = new byte[81920];

                    using (Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (FileStream target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int read;
                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await target.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            receivedBytes += read;
                            if (totalBytes > 0 && progress != null)
                                progress.Report((int)Math.Min(100, receivedBytes * 100 / totalBytes));
                        }
                    }
                }
            }

            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(tempPath, targetPath);
            progress?.Report(100);
            return targetPath;
        }

        public static void StartPackageUpdater(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                throw new FileNotFoundException("Update package was not found.", packagePath);

            Directory.CreateDirectory(UpdatesFolder);
            string scriptPath = Path.Combine(UpdatesFolder, "Apply-ServoERPUpdate.ps1");
            File.WriteAllText(scriptPath, BuildPackageUpdaterScript(), Encoding.UTF8);

            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string exeName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            int parentPid = System.Diagnostics.Process.GetCurrentProcess().Id;

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArg(scriptPath) +
                            " -PackagePath " + QuoteArg(packagePath) +
                            " -AppDir " + QuoteArg(appDir) +
                            " -ExeName " + QuoteArg(exeName) +
                            " -ParentPid " + parentPid.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };
            System.Diagnostics.Process.Start(startInfo);
        }

        public static void StartInstallerElevated(string installerPath)
        {
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
                throw new FileNotFoundException("Installer was not found.", installerPath);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(startInfo);
        }

        public static string GetCurrentAssemblyVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "0.0.0.0" : version.ToString();
        }

        private static string GetInstallerDownloadUrl()
        {
            try
            {
                string configured = ConfigService.Get("App", "InstallerDownloadUrl", string.Empty);
                if (!string.IsNullOrWhiteSpace(configured))
                    return configured.Trim();
            }
            catch
            {
            }

            return DefaultInstallerDownloadUrl;
        }

        private static void ResolveDownloadUrls(string changelogJson, UpdateCheckResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(changelogJson))
                    return;

                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.Deserialize<Dictionary<string, object>>(changelogJson);
                if (root == null || !root.TryGetValue("download", out object downloadObj))
                    return;

                var download = downloadObj as Dictionary<string, object>;
                if (download == null)
                    return;

                string url = GetString(download, "url");
                if (!string.IsNullOrWhiteSpace(url))
                    result.DownloadUrl = ToAbsoluteServoErpUrl(url);

                string packageUrl = GetString(download, "packageUrl");
                if (string.IsNullOrWhiteSpace(packageUrl))
                    packageUrl = GetString(download, "package");
                if (!string.IsNullOrWhiteSpace(packageUrl))
                    result.PackageUrl = ToAbsoluteServoErpUrl(packageUrl);
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo("Update download URL parse failed silently: " + ex.Message);
            }
        }

        private static string ToAbsoluteServoErpUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri absolute))
                return absolute.ToString();

            return new Uri(new Uri("https://servoerp.in/"), url.TrimStart('/')).ToString();
        }

        private static string QuoteArg(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string BuildPackageUpdaterScript()
        {
            return @"
param(
  [Parameter(Mandatory=$true)][string]$PackagePath,
  [Parameter(Mandatory=$true)][string]$AppDir,
  [Parameter(Mandatory=$true)][string]$ExeName,
  [Parameter(Mandatory=$true)][int]$ParentPid
)
$ErrorActionPreference = 'Stop'
$logDir = Join-Path $AppDir 'LOGS'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$log = Join-Path $logDir 'update-apply.log'
function Write-UpdateLog([string]$message) {
  Add-Content -Path $log -Value ((Get-Date).ToString('yyyy-MM-dd HH:mm:ss') + ' | ' + $message)
}
try {
  Write-UpdateLog ('Starting update from ' + $PackagePath)
  for ($i = 0; $i -lt 90; $i++) {
    $p = Get-Process -Id $ParentPid -ErrorAction SilentlyContinue
    if (-not $p) { break }
    Start-Sleep -Seconds 1
  }
  Get-Process -Name 'HVAC_Pro_Desktop','ServoERP' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 2

  $stage = Join-Path (Split-Path -Parent $PackagePath) ('stage-' + [IO.Path]::GetFileNameWithoutExtension($PackagePath))
  if (Test-Path $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $stage | Out-Null
  Expand-Archive -LiteralPath $PackagePath -DestinationPath $stage -Force

  $sourceDir = $stage
  $expectedExe = Join-Path $stage $ExeName
  if (-not (Test-Path -LiteralPath $expectedExe)) {
    $exeMatch = Get-ChildItem -LiteralPath $stage -Recurse -Filter $ExeName -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $exeMatch) {
      $exeMatch = Get-ChildItem -LiteralPath $stage -Recurse -Filter 'HVAC_Pro_Desktop.exe' -File -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if ($exeMatch) {
      $sourceDir = Split-Path -Parent $exeMatch.FullName
      Write-UpdateLog ('Using package source directory: ' + $sourceDir)
    } else {
      throw ('Update package does not contain ' + $ExeName)
    }
  }

  $backupDir = Join-Path (Split-Path -Parent $PackagePath) ('backup-' + (Get-Date).ToString('yyyyMMdd-HHmmss'))
  New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
  foreach ($name in @($ExeName, 'HVAC_Pro_Desktop.exe.config')) {
    $existing = Join-Path $AppDir $name
    if (Test-Path -LiteralPath $existing) {
      Copy-Item -LiteralPath $existing -Destination (Join-Path $backupDir $name) -Force -ErrorAction SilentlyContinue
    }
  }

  Get-ChildItem -LiteralPath $sourceDir -Force | ForEach-Object {
    if ($_.Name -ieq 'HVACPro.config') { return }
    if ($_.Name -ieq 'LOGS') { return }
    if ($_.Name -ieq 'UPDATES') { return }
    if ($_.Name -ieq 'BACKUPS') { return }
    $dest = Join-Path $AppDir $_.Name
    Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
  }

  $updatedExe = Join-Path $AppDir $ExeName
  if (-not (Test-Path -LiteralPath $updatedExe)) {
    throw ('Updated executable was not found after copy: ' + $updatedExe)
  }
  $fileVersion = (Get-Item -LiteralPath $updatedExe).VersionInfo.FileVersion
  Write-UpdateLog ('Update copied successfully. Installed file version: ' + $fileVersion)
  Start-Process -FilePath (Join-Path $AppDir $ExeName)
} catch {
  Write-UpdateLog ('FAILED: ' + $_.Exception.Message)
  Start-Process -FilePath (Join-Path $AppDir $ExeName) -ErrorAction SilentlyContinue
}
";
        }

        private static async Task<string> ResolveDirectDownloadUrlAsync(HttpClient client, string url, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.TrimEnd('/').EndsWith("/download", StringComparison.OrdinalIgnoreCase))
                return url;

            string html = await client.GetStringAsync(url).ConfigureAwait(false);
            Match hrefMatch = Regex.Match(html, "<a\\s+[^>]*href=[\"'](?<url>https?://[^\"']+)[\"']", RegexOptions.IgnoreCase);
            if (hrefMatch.Success)
                return hrefMatch.Groups["url"].Value.Replace("&amp;", "&");

            Match refreshMatch = Regex.Match(html, "url=(?<url>https?://[^\"'>\\s]+)", RegexOptions.IgnoreCase);
            if (refreshMatch.Success)
                return refreshMatch.Groups["url"].Value.Replace("&amp;", "&");

            return url;
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                return new Version(latest) > new Version(current);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
                return false;

            Version parsed;
            return Version.TryParse(value.Trim(), out parsed);
        }

        private static string BuildChangelogText(string changelogJson, string latestVersion)
        {
            if (string.IsNullOrWhiteSpace(changelogJson))
                return "No changelog details were provided for this version.";

            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.Deserialize<Dictionary<string, object>>(changelogJson);
                if (root == null || !root.TryGetValue("versions", out object versionsObj))
                    return "No changelog details were provided for this version.";

                var versions = versionsObj as IEnumerable;
                if (versions == null || versionsObj is string)
                    return "No changelog details were provided for this version.";

                foreach (object versionObj in versions)
                {
                    var version = versionObj as Dictionary<string, object>;
                    if (version == null)
                        continue;

                    string versionNumber = GetString(version, "version");
                    if (!string.Equals(versionNumber, latestVersion, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return FormatVersionChangelog(version);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo("Update changelog parse failed silently: " + ex.Message);
            }

            return "No changelog details were provided for this version.";
        }

        private static string FormatVersionChangelog(Dictionary<string, object> version)
        {
            var builder = new StringBuilder();
            string title = GetString(version, "title");
            if (!string.IsNullOrWhiteSpace(title))
                builder.AppendLine(title.Trim());

            string date = GetString(version, "date");
            if (!string.IsNullOrWhiteSpace(date))
                builder.AppendLine("Released: " + date.Trim());

            if (builder.Length > 0)
                builder.AppendLine();

            if (version.TryGetValue("changes", out object changesObj) && changesObj is IEnumerable changes && !(changesObj is string))
            {
                foreach (object changeObj in changes)
                {
                    var change = changeObj as Dictionary<string, object>;
                    if (change == null)
                        continue;

                    string type = GetString(change, "type");
                    if (!string.IsNullOrWhiteSpace(type))
                        builder.AppendLine(ToTitleCase(type) + ":");

                    if (change.TryGetValue("items", out object itemsObj) && itemsObj is IEnumerable items && !(itemsObj is string))
                    {
                        foreach (object item in items)
                        {
                            string text = Convert.ToString(item);
                            if (!string.IsNullOrWhiteSpace(text))
                                builder.AppendLine("  - " + text.Trim());
                        }
                    }

                    builder.AppendLine();
                }
            }

            string formatted = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(formatted)
                ? "No changelog details were provided for this version."
                : formatted;
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out object value) || value == null)
                return string.Empty;

            return Convert.ToString(value) ?? string.Empty;
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Replace("_", " ").Replace("-", " ").Trim();
            return string.Join(" ", normalized
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1).ToLowerInvariant() : string.Empty)));
        }
    }
}
