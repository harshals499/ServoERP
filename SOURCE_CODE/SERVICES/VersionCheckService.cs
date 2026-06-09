using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Web.Script.Serialization;
using System.Threading.Tasks;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class VersionCheckResult
    {
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string Notes { get; set; }
        public bool IsUpdateAvailable { get; set; }

        public string UpdateMessage
        {
            get
            {
                string message = "Version " + LatestVersion + " is available.";
                if (!string.IsNullOrWhiteSpace(Notes))
                    message += " " + Notes.Trim();
                return message;
            }
        }
    }

    internal sealed class VersionManifest
    {
        public string version { get; set; }
        public string downloadUrl { get; set; }
        public string notes { get; set; }
    }

    public static class VersionCheckService
    {
        private static readonly string LastCheckFile = Path.Combine(@"C:\HVAC_PRO_MSE\LOGS", "lastVersionCheck.txt");

        public static bool ShouldCheckNow()
        {
            if (string.IsNullOrWhiteSpace(ConfigService.GetVersionCheckUrl()))
                return false;

            if (!ConfigService.IsVersionCheckEnabled())
                return false;

            try
            {
                if (File.Exists(LastCheckFile))
                {
                    DateTime lastCheck;
                    if (DateTime.TryParseExact(
                        File.ReadAllText(LastCheckFile).Trim(),
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out lastCheck))
                    {
                        return DateTime.Now - lastCheck >= TimeSpan.FromHours(ConfigService.GetVersionCheckIntervalHours());
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo("Version check schedule read failed silently: " + ex.Message);
            }

            return true;
        }

        public static Task<VersionCheckResult> CheckForUpdateAsync()
        {
            return CheckForUpdateAsync(false);
        }

        public static async Task<VersionCheckResult> CheckForUpdateAsync(bool force)
        {
            var result = new VersionCheckResult
            {
                CurrentVersion = ConfigService.GetAppVersion(),
                LatestVersion = ConfigService.GetAppVersion(),
                IsUpdateAvailable = false
            };

            try
            {
                string url = ConfigService.GetVersionCheckUrl();
                if (string.IsNullOrWhiteSpace(url))
                    return result;

                if (!force && !ShouldCheckNow())
                    return result;

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    string response = await client.GetStringAsync(url).ConfigureAwait(false);
                    VersionManifest manifest = ParseManifest(response);
                    string latestVersion = manifest.version;

                    result.LatestVersion = latestVersion;
                    result.DownloadUrl = manifest.downloadUrl;
                    result.Notes = manifest.notes;
                    result.IsUpdateAvailable = IsNewerVersion(latestVersion, result.CurrentVersion);

                    Directory.CreateDirectory(Path.GetDirectoryName(LastCheckFile) ?? @"C:\HVAC_PRO_MSE\LOGS");
                    File.WriteAllText(LastCheckFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

                    AppLogger.LogInfo(
                        "Version check: current=" + result.CurrentVersion +
                        " latest=" + SafeLogValue(result.LatestVersion) +
                        " updateAvailable=" + result.IsUpdateAvailable);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogInfo("Version check failed silently: " + ex.Message);
                result.IsUpdateAvailable = false;
            }

            return result;
        }

        private static VersionManifest ParseManifest(string response)
        {
            string text = NormalizeVersionText(response);
            if (text.StartsWith("{"))
            {
                try
                {
                    var serializer = new JavaScriptSerializer();
                    VersionManifest manifest = serializer.Deserialize<VersionManifest>(text);
                    if (manifest != null && IsPlausibleVersionText(manifest.version))
                        return manifest;
                }
                catch (Exception ex)
                {
                    AppLogger.LogInfo("Version manifest JSON parse failed: " + ex.Message);
                }
            }

            if (IsPlausibleVersionText(text))
                return new VersionManifest { version = text };

            AppLogger.LogInfo("Version manifest ignored: response was not JSON or a version string. length=" + text.Length);
            return new VersionManifest { version = ConfigService.GetAppVersion() };
        }

        public static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestVersion = new Version(latest);
                var currentVersion = new Version(current);
                return latestVersion > currentVersion;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPlausibleVersionText(string value)
        {
            string text = NormalizeVersionText(value);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (text.Length > 64 || text.IndexOfAny(new[] { '<', '>', '\r', '\n', '\t' }) >= 0)
                return false;

            Version parsed;
            return Version.TryParse(text, out parsed);
        }

        private static string NormalizeVersionText(string value)
        {
            return (value ?? string.Empty).Trim().TrimStart('\uFEFF');
        }

        private static string SafeLogValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Length <= 80 ? value : value.Substring(0, 80) + "...";
        }
    }
}
