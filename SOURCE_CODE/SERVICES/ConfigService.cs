using System;
using System.Globalization;
using System.IO;
using System.Xml;

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
                    string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.config");
                    if (File.Exists(appPath))
                        return;

                    string machinePath = @"C:\HVAC_PRO_MSE\HVACPro.config";
                    if (File.Exists(machinePath))
                        return;

                    string samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.sample.config");
                    if (!File.Exists(samplePath))
                        return;

                    Directory.CreateDirectory(Path.GetDirectoryName(appPath) ?? AppDomain.CurrentDomain.BaseDirectory);
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
            lock (Sync)
            {
                try
                {
                    string path = ResolveConfigPath();
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);

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

                    XmlElement keyNode = sectionNode[key];
                    if (keyNode == null)
                    {
                        keyNode = document.CreateElement(key);
                        sectionNode.AppendChild(keyNode);
                    }

                    keyNode.InnerText = value ?? string.Empty;
                    document.Save(path);
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
            return string.Equals(Get("App", "SilentAutoUpdateEnabled", "false"), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldApplySilentUpdateOnExit()
        {
            return string.Equals(Get("App", "SilentAutoUpdateApplyOnExit", "false"), "true", StringComparison.OrdinalIgnoreCase);
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

        private static string ResolveConfigPath()
        {
            string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.config");
            if (File.Exists(appPath))
                return appPath;

            string machinePath = @"C:\HVAC_PRO_MSE\HVACPro.config";
            if (File.Exists(machinePath))
                return machinePath;

            return appPath;
        }
    }
}
