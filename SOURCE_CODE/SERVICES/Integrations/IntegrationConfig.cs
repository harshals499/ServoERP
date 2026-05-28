using System;

namespace HVAC_Pro_Desktop.Services.Integrations
{
    internal static class IntegrationConfig
    {
        public static bool GetBool(string section, string key, bool defaultValue)
        {
            string value = ConfigService.Get(section, key, defaultValue ? "true" : "false");
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        public static string Get(string section, string key, string defaultValue)
        {
            return ConfigService.Get(section, key, defaultValue);
        }

        public static void Set(string section, string key, string value)
        {
            ConfigService.Set(section, key, value);
        }
    }
}
