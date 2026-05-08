using System;
using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Services
{
    public static class IndiaStateCatalog
    {
        private static readonly (string Code, string Name)[] Entries =
        {
            ("01", "Jammu and Kashmir"),
            ("02", "Himachal Pradesh"),
            ("03", "Punjab"),
            ("04", "Chandigarh"),
            ("05", "Uttarakhand"),
            ("06", "Haryana"),
            ("07", "Delhi"),
            ("08", "Rajasthan"),
            ("09", "Uttar Pradesh"),
            ("10", "Bihar"),
            ("11", "Sikkim"),
            ("12", "Arunachal Pradesh"),
            ("13", "Nagaland"),
            ("14", "Manipur"),
            ("15", "Mizoram"),
            ("16", "Tripura"),
            ("17", "Meghalaya"),
            ("18", "Assam"),
            ("19", "West Bengal"),
            ("20", "Jharkhand"),
            ("21", "Odisha"),
            ("22", "Chhattisgarh"),
            ("23", "Madhya Pradesh"),
            ("24", "Gujarat"),
            ("26", "Dadra and Nagar Haveli and Daman and Diu"),
            ("27", "Maharashtra"),
            ("29", "Karnataka"),
            ("30", "Goa"),
            ("31", "Lakshadweep"),
            ("32", "Kerala"),
            ("33", "Tamil Nadu"),
            ("34", "Puducherry"),
            ("35", "Andaman and Nicobar Islands"),
            ("36", "Telangana"),
            ("37", "Andhra Pradesh"),
            ("38", "Ladakh")
        };

        private static readonly HashSet<string> ValidCodes = new HashSet<string>(
            Entries.Select(e => e.Code).Concat(new[] { "28" }),
            StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<string> Names => Entries.Select(e => e.Name).ToList();

        public static bool IsValidStateCode(string stateCode)
        {
            return !string.IsNullOrWhiteSpace(stateCode) && ValidCodes.Contains(stateCode.Trim());
        }

        public static string GetCodeByName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return string.Empty;

            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Name, stateName.Trim(), StringComparison.OrdinalIgnoreCase))
                    return entry.Code;
            }

            return string.Empty;
        }

        public static string NormalizeStateName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return string.Empty;

            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Name, stateName.Trim(), StringComparison.OrdinalIgnoreCase))
                    return entry.Name;
            }

            return stateName.Trim();
        }
    }
}
