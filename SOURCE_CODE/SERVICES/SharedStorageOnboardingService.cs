using System;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Shows the private-server storage invitation once after the feature update is first opened.</summary>
    public static class SharedStorageOnboardingService
    {
        public const string FeatureVersion = "1.1.408.0";

        public static bool ShouldShow()
        {
            return !string.Equals(ConfigService.Get("SharedStorage", "OnboardingShownVersion", string.Empty), FeatureVersion, StringComparison.OrdinalIgnoreCase);
        }

        public static void MarkShown()
        {
            ConfigService.Set("SharedStorage", "OnboardingShownVersion", FeatureVersion);
        }
    }
}
