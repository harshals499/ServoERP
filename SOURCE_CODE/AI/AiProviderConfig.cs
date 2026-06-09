using System;
using System.Globalization;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.AI
{
    /// <summary>
    /// Built-in assistant settings. Values are stored in HVACPro.config through ConfigService.
    /// </summary>
    public class AiProviderConfig
    {
        public bool Enabled { get; set; } = true;
        public string Provider { get; set; } = "Built-in";
        public string EndpointUrl { get; set; } = "";
        public string ModelName { get; set; } = "ServoERP Bot";
        public int MaxTokens { get; set; } = 700;
        public decimal Temperature { get; set; } = 0.2m;
        public int TimeoutSeconds { get; set; } = 60;

        public static AiProviderConfig Load()
        {
            var config = new AiProviderConfig();
            config.Enabled = ReadBool("Enabled", true);
            config.Provider = "Built-in";
            config.EndpointUrl = "";
            config.ModelName = "ServoERP Bot";
            config.MaxTokens = ReadInt("MaxTokens", 700, 64, 4096);
            config.Temperature = ReadDecimal("Temperature", 0.2m, 0m, 2m);
            config.TimeoutSeconds = ReadInt("TimeoutSeconds", 60, 10, 180);
            return config;
        }

        public void Save()
        {
            ConfigService.Set("AI", "Enabled", Enabled ? "true" : "false");
            ConfigService.Set("AI", "Provider", "Built-in");
            ConfigService.Set("AI", "EndpointUrl", "");
            ConfigService.Set("AI", "ModelName", "ServoERP Bot");
            ConfigService.Set("AI", "MaxTokens", MaxTokens.ToString(CultureInfo.InvariantCulture));
            ConfigService.Set("AI", "Temperature", Temperature.ToString("0.###", CultureInfo.InvariantCulture));
            ConfigService.Set("AI", "TimeoutSeconds", TimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        }

        private static bool ReadBool(string key, bool defaultValue)
        {
            string value = ConfigService.Get("AI", key, defaultValue ? "true" : "false");
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        private static int ReadInt(string key, int defaultValue, int min, int max)
        {
            int parsed;
            string value = ConfigService.Get("AI", key, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                parsed = defaultValue;
            return Math.Max(min, Math.Min(max, parsed));
        }

        private static decimal ReadDecimal(string key, decimal defaultValue, decimal min, decimal max)
        {
            decimal parsed;
            string value = ConfigService.Get("AI", key, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
                parsed = defaultValue;
            return Math.Max(min, Math.Min(max, parsed));
        }
    }
}
