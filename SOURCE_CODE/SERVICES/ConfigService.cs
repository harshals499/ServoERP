using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Linq;

namespace HVAC_Pro_Desktop.Services
{
    public static class ConfigService
    {
        private static readonly object Sync = new object();
        public const string ProductionVersionCheckUrl = UpdateService.DefaultGitHubRepositoryUrl;

        public static string Get(string section, string key, string defaultValue)
        {
            lock (Sync)
            {
                try
                {
                    EnsureLocalConfigFile();
                    string path = ResolveConfigPath();
                    if (!File.Exists(path))
                        return defaultValue;

                    var document = new XmlDocument();
                    document.Load(path);
                    string xpath = string.Format(CultureInfo.InvariantCulture, "/HVACProConfig/{0}/{1}", section, key);
                    XmlNode node = document.SelectSingleNode(xpath);
                    return string.IsNullOrWhiteSpace(node?.InnerText) ? defaultValue : node.InnerText.Trim();
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("ConfigService.Get", ex);
                    return defaultValue;
                }
            }
        }

        public static void EnsureLocalConfigFile()
        {
            lock (Sync)
            {
                try
                {
                    string appPath = ResolveConfigPath();
                    string machinePath = @"C:\HVAC_PRO_MSE\HVACPro.config";
                    if (File.Exists(appPath))
                        return;

                    string legacyAppPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.config");
                    string legacyPath = File.Exists(machinePath) ? machinePath : legacyAppPath;
                    // Older updaters backed up settings before replacing their install folder.
                    // Recover that PC-local copy during the first update to persistent storage.
                    if (!File.Exists(legacyPath))
                    {
                        string updatesPath = @"C:\HVAC_PRO_MSE\UPDATES";
                        if (Directory.Exists(updatesPath))
                        {
                            string backupDirectory = Directory.GetDirectories(updatesPath, "config-backup-v*")
                                .OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault();
                            if (backupDirectory != null)
                            {
                                string rootBackup = Path.Combine(backupDirectory, "HVACPro.root.config");
                                legacyPath = File.Exists(rootBackup) ? rootBackup : Path.Combine(backupDirectory, "HVACPro.config");
                            }
                        }
                    }
                    if (File.Exists(legacyPath))
                    {
                        EnsureConfigurationDirectory(appPath);
                        File.Copy(legacyPath, appPath, false);
                        return;
                    }

                    string samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.sample.config");
                    if (!File.Exists(samplePath))
                        return;

                    EnsureConfigurationDirectory(appPath);
                    File.Copy(samplePath, appPath, false);
                    AppLogger.LogInfo("Created first-run HVACPro.config from packaged sample.");
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("ConfigService.EnsureLocalConfigFile", ex);
                }
            }
        }

        public static void Set(string section, string key, string value)
        {
            SetValues(section, new Dictionary<string, string> { { key, value } });
        }

        internal static void SetValues(string section, IDictionary<string, string> values)
        {
            lock (Sync)
            {
                try
                {
                    EnsureLocalConfigFile();
                    string path = ResolveConfigPath();
                    EnsureConfigurationDirectory(path);

                    var document = new XmlDocument();
                    if (File.Exists(path))
                        document.Load(path);
                    else
                        document.LoadXml("<HVACProConfig />");

                    XmlElement root = document.DocumentElement;
                    if (root == null)
                    {
                        root = document.CreateElement("HVACProConfig");
                        document.AppendChild(root);
                    }

                    XmlElement sectionNode = root[section];
                    if (sectionNode == null)
                    {
                        sectionNode = document.CreateElement(section);
                        root.AppendChild(sectionNode);
                    }

                    foreach (var entry in values)
                    {
                        XmlElement keyNode = sectionNode[entry.Key];
                        if (keyNode == null)
                        {
                            keyNode = document.CreateElement(entry.Key);
                            sectionNode.AppendChild(keyNode);
                        }

                        keyNode.InnerText = entry.Value ?? string.Empty;
                    }
                    string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        document.Save(temporaryPath);
                        if (File.Exists(path))
                            File.Replace(temporaryPath, path, null);
                        else
                            File.Move(temporaryPath, path);
                    }
                    finally
                    {
                        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                    }
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("ConfigService.Set", ex);
                    throw;
                }
            }
        }

        public static string GetAppVersion()
        {
            try
            {
                return UpdateService.GetCurrentAssemblyVersion();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("ConfigService.GetAppVersion", ex);
            }

            return Get("App", "Version", "1.0.0");
        }

        public static string GetVersionCheckUrl()
        {
            string configured = Get("App", "GitHubRepositoryUrl", Get("App", "VersionCheckUrl", ProductionVersionCheckUrl));
            if (!string.Equals(configured, ProductionVersionCheckUrl, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Set("App", "GitHubRepositoryUrl", ProductionVersionCheckUrl);
                    Set("App", "VersionCheckUrl", ProductionVersionCheckUrl);
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("ConfigService.GetVersionCheckUrl.Lock", ex);
                }
            }

            return ProductionVersionCheckUrl;
        }

        public static bool IsVersionCheckEnabled()
        {
            return string.Equals(Get("App", "VersionCheckEnabled", "true"), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static int GetVersionCheckIntervalHours()
        {
            int hours;
            return int.TryParse(Get("App", "VersionCheckIntervalHours", "24"), out hours) && hours > 0 ? hours : 24;
        }

        public static bool IsSilentAutoUpdateEnabled()
        {
            return string.Equals(Get("App", "SilentAutoUpdateEnabled", "true"), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldApplySilentUpdateOnExit()
        {
            return string.Equals(Get("App", "SilentAutoUpdateApplyOnExit", "true"), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldApplySilentUpdateImmediately()
        {
            return string.Equals(Get("App", "SilentAutoUpdateApplyImmediately", "false"), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetLicenseActivationUrl()
        {
            return Get("Licensing", "ActivationUrl", "https://servoerp.in/api/license/activate");
        }

        public static string GetLicenseValidationUrl()
        {
            return Get("Licensing", "ValidationUrl", "https://servoerp.in/api/license/validate");
        }

        private static void EnsureConfigurationDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (Directory.Exists(directory)) return;
            DirectoryInfo info = Directory.CreateDirectory(directory);
            // Office configuration is shared by Windows accounts on this PC, like machine DPAPI.
            DirectorySecurity security = info.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            info.SetAccessControl(security);
        }

        internal static string ResolveConfigPath()
        {
            // Machine settings must survive versioned install directories and installer replacement.
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ServoERP", "Configuration", "HVACPro.config");
        }
    }
}
