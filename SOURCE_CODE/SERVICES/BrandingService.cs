namespace HVAC_Pro_Desktop.Services
{
    public static class BrandingService
    {
        public const string AppName = "ServoERP";
        public const string Subtitle = "by Harshal Sonawane";

        public static string WindowTitle(string section = null)
        {
            return string.IsNullOrWhiteSpace(section)
                ? AppName
                : AppName + " - " + section;
        }
    }
}
