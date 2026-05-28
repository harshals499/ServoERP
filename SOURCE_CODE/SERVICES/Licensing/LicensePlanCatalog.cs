using System;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public static class LicensePlanCatalog
    {
        public const string StarterAmc = "Starter AMC";
        public const string GrowthAmc = "Growth AMC";
        public const string BusinessAmc = "Business AMC";

        public static string GetDisplayName(LicensePlanType plan)
        {
            if (plan == LicensePlanType.Basic || plan == LicensePlanType.Standard)
                return StarterAmc;
            if (plan == LicensePlanType.Pro)
                return GrowthAmc;
            if (plan == LicensePlanType.Enterprise)
                return BusinessAmc;
            return "Trial";
        }

        public static string GetDisplayName(LicenseSnapshot snapshot)
        {
            if (snapshot == null)
                return StarterAmc;
            if (snapshot.PlanType == LicensePlanType.Trial)
                return "Trial";
            if (IsApprovedAmcName(snapshot.PlanName))
                return snapshot.PlanName.Trim();
            return GetDisplayName(GetDisplayPlan(snapshot));
        }

        public static int GetDisplayMaxDevices(LicenseSnapshot snapshot)
        {
            return GetMaxDevices(GetDisplayPlan(snapshot));
        }

        public static decimal GetDisplayAnnualPrice(LicenseSnapshot snapshot)
        {
            return GetAnnualPrice(GetDisplayPlan(snapshot));
        }

        public static int GetMaxDevices(LicensePlanType plan)
        {
            if (plan == LicensePlanType.Trial)
                return 1;
            if (plan == LicensePlanType.Basic || plan == LicensePlanType.Standard)
                return 3;
            if (plan == LicensePlanType.Pro)
                return 7;
            return 15;
        }

        public static decimal GetAnnualPrice(LicensePlanType plan)
        {
            if (plan == LicensePlanType.Basic || plan == LicensePlanType.Standard)
                return 10000m;
            if (plan == LicensePlanType.Pro)
                return 18000m;
            if (plan == LicensePlanType.Enterprise)
                return 30000m;
            return 0m;
        }

        public static bool IsApprovedAmcName(string value)
        {
            return string.Equals(value, StarterAmc, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, GrowthAmc, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, BusinessAmc, StringComparison.OrdinalIgnoreCase);
        }

        public static LicensePlanType ParsePlan(string value)
        {
            string normalized = (value ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            if (normalized.Equals("StarterAMC", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Starter", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Basic", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                return LicensePlanType.Basic;
            if (normalized.Equals("GrowthAMC", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Growth", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Pro", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Professional", StringComparison.OrdinalIgnoreCase))
                return LicensePlanType.Pro;
            if (normalized.Equals("BusinessAMC", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Business", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Enterprise", StringComparison.OrdinalIgnoreCase))
                return LicensePlanType.Enterprise;
            return LicensePlanType.Basic;
        }

        private static LicensePlanType GetDisplayPlan(LicenseSnapshot snapshot)
        {
            if (snapshot == null)
                return LicensePlanType.Basic;
            if (snapshot.PlanType == LicensePlanType.Trial)
                return LicensePlanType.Trial;
            if (IsApprovedAmcName(snapshot.PlanName))
                return ParsePlan(snapshot.PlanName);
            if (snapshot.MaxDevices <= 3)
                return LicensePlanType.Basic;
            if (snapshot.MaxDevices <= 7)
                return LicensePlanType.Pro;
            return LicensePlanType.Enterprise;
        }
    }
}
