using System;
using System.Globalization;

namespace HVAC_Pro_Desktop.Services
{
    public static class SafeGet
    {
        public static string String(object value)
        {
            if (value == null || value == DBNull.Value)
                return string.Empty;

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public static int Int(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0;

            int parsed;
            return int.TryParse(String(value), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        public static decimal Decimal(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0m;

            decimal parsed;
            return decimal.TryParse(String(value), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : 0m;
        }

        public static DateTime Date(object value)
        {
            if (value == null || value == DBNull.Value)
                return DateTime.MinValue;

            DateTime parsed;
            return DateTime.TryParse(String(value), CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) ? parsed : DateTime.MinValue;
        }

        public static bool Bool(object value)
        {
            if (value == null || value == DBNull.Value)
                return false;

            bool parsed;
            if (bool.TryParse(String(value), out parsed))
                return parsed;

            return Int(value) != 0;
        }
    }
}
