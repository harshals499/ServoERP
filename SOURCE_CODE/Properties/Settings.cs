using System.Configuration;

namespace HVAC_Pro_Desktop.Properties
{
    /// <summary>Stores per-user ServoERP desktop preferences.</summary>
    public sealed partial class Settings : ApplicationSettingsBase
    {
        private static readonly Settings DefaultInstance = (Settings)Synchronized(new Settings());

        /// <summary>Gets the current user's settings instance.</summary>
        public static Settings Default
        {
            get { return DefaultInstance; }
        }

        /// <summary>Gets or sets whether the first-launch product tour has been completed.</summary>
        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool TourCompleted
        {
            get { return (bool)this["TourCompleted"]; }
            set { this["TourCompleted"] = value; }
        }
    }
}
