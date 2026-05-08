using System;
using System.Collections.Generic;
using System.Globalization;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class SettingsService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
        private readonly SettingsRepository _repo = new SettingsRepository();

        public Dictionary<string, string> GetAll() => AppDataCache.GetOrCreate("settings:all", CacheTtl, _repo.GetAll);
        public void Set(string key, string value)
        {
            _repo.Set(key, value);
            AppDataCache.RemovePrefix("settings:");
        }
        public string Get(string key, string def = "")
        {
            Dictionary<string, string> settings = GetAll();
            return settings != null && settings.TryGetValue(key, out string value) ? value : def;
        }

        public IndiaCompanySettings GetIndiaCompanySettings()
        {
            Dictionary<string, string> settings = GetAll();
            var snapshot = new IndiaCompanySettings
            {
                CompanyName = GetValue(settings, "CompanyName", BrandingService.AppName),
                GSTIN = IndiaTaxValidationHelper.NormalizeTaxId(GetValue(settings, "CompanyGSTIN", GetValue(settings, "CompanyGST", ""))),
                PAN = IndiaTaxValidationHelper.NormalizeTaxId(GetValue(settings, "CompanyPAN", "")),
                TAN = IndiaTaxValidationHelper.NormalizeTaxId(GetValue(settings, "CompanyTAN", "")),
                Phone = GetValue(settings, "CompanyPhone", ""),
                Email = GetValue(settings, "CompanyEmail", ""),
                Address = GetValue(settings, "CompanyAddress", ""),
                CompanyState = IndiaStateCatalog.NormalizeStateName(GetValue(settings, "CompanyState", "Maharashtra")),
                GSTRegistrationType = GetValue(settings, "GSTRegistrationType", "Regular"),
                InvoicePrefix = GetValue(settings, "InvoicePrefix", "INV"),
                DefaultGSTRate = GetDecimalValue(settings, "DefaultGST", 18m),
                DefaultPaymentTermsDays = (int)GetDecimalValue(settings, "DefaultPaymentTerms", 30m),
                AnnualTurnover = GetDecimalValue(settings, "AnnualTurnover", 0m),
                EInvoiceThresholdAmount = GetDecimalValue(settings, "EInvoiceThresholdAmount", 50000000m),
                CurrencyCode = GetValue(settings, "CurrencyCode", "INR"),
                CurrencySymbol = GetValue(settings, "CurrencySymbol", "\u20B9"),
                DefaultPlaceOfSupply = GetValue(settings, "DefaultPlaceOfSupply", GetValue(settings, "CompanyState", "Maharashtra")),
                DefaultCertificationNote = GetValue(settings, "DefaultCertificationNote", ""),
                OfficeLatitude = GetDoubleValue(settings, "OfficeLatitude"),
                OfficeLongitude = GetDoubleValue(settings, "OfficeLongitude")
            };

            snapshot.EInvoiceAutoEnabled = snapshot.AnnualTurnover >= snapshot.EInvoiceThresholdAmount;
            snapshot.FinancialYearPattern = IndiaFinancialYearHelper.GetFinancialYearDisplay(snapshot.SnapshotDate);
            return snapshot;
        }

        public void SaveIndiaCompanySettings(IndiaCompanySettings settings)
        {
            if (settings == null)
                throw new Exception("Settings payload is missing.");

            if (string.IsNullOrWhiteSpace(settings.CompanyName))
                throw new Exception("Company name is required.");

            settings.GSTIN = IndiaTaxValidationHelper.NormalizeTaxId(settings.GSTIN);
            settings.PAN = IndiaTaxValidationHelper.NormalizeTaxId(settings.PAN);
            settings.TAN = IndiaTaxValidationHelper.NormalizeTaxId(settings.TAN);
            settings.InvoicePrefix = string.IsNullOrWhiteSpace(settings.InvoicePrefix) ? "INV" : settings.InvoicePrefix.Trim().ToUpperInvariant();
            settings.CompanyState = IndiaStateCatalog.NormalizeStateName(settings.CompanyState);
            settings.DefaultPlaceOfSupply = string.IsNullOrWhiteSpace(settings.DefaultPlaceOfSupply)
                ? settings.CompanyState
                : IndiaStateCatalog.NormalizeStateName(settings.DefaultPlaceOfSupply);
            settings.GSTRegistrationType = string.IsNullOrWhiteSpace(settings.GSTRegistrationType) ? "Regular" : settings.GSTRegistrationType.Trim();
            settings.CurrencyCode = "INR";
            settings.CurrencySymbol = "\u20B9";
            settings.FinancialYearPattern = IndiaFinancialYearHelper.GetFinancialYearDisplay(DateTime.Today);
            settings.EInvoiceAutoEnabled = settings.AnnualTurnover >= settings.EInvoiceThresholdAmount;

            IndiaTaxValidationHelper.EnsureValidGSTIN(settings.GSTIN, "Company GSTIN");
            IndiaTaxValidationHelper.EnsureValidPAN(settings.PAN, "Company PAN");
            IndiaTaxValidationHelper.EnsureValidTAN(settings.TAN, "Company TAN");

            Set("CompanyName", settings.CompanyName.Trim());
            Set("CompanyGST", settings.GSTIN);
            Set("CompanyGSTIN", settings.GSTIN);
            Set("CompanyPAN", settings.PAN);
            Set("CompanyTAN", settings.TAN);
            Set("CompanyPhone", settings.Phone?.Trim() ?? string.Empty);
            Set("CompanyEmail", settings.Email?.Trim() ?? string.Empty);
            Set("CompanyAddress", settings.Address?.Trim() ?? string.Empty);
            Set("CompanyState", settings.CompanyState);
            Set("GSTRegistrationType", settings.GSTRegistrationType);
            Set("InvoicePrefix", settings.InvoicePrefix);
            Set("DefaultGST", settings.DefaultGSTRate.ToString("0.##"));
            Set("DefaultPaymentTerms", settings.DefaultPaymentTermsDays.ToString());
            Set("AnnualTurnover", settings.AnnualTurnover.ToString("0.##"));
            Set("EInvoiceThresholdAmount", settings.EInvoiceThresholdAmount.ToString("0.##"));
            Set("EInvoiceAutoEnabled", settings.EInvoiceAutoEnabled ? "1" : "0");
            Set("CurrencyCode", settings.CurrencyCode);
            Set("CurrencySymbol", settings.CurrencySymbol);
            Set("FinancialYearPattern", settings.FinancialYearPattern);
            Set("DefaultPlaceOfSupply", settings.DefaultPlaceOfSupply);
            Set("DefaultCertificationNote", settings.DefaultCertificationNote ?? string.Empty);
            Set("OfficeLatitude", settings.OfficeLatitude.HasValue ? settings.OfficeLatitude.Value.ToString("0.0000000", CultureInfo.InvariantCulture) : string.Empty);
            Set("OfficeLongitude", settings.OfficeLongitude.HasValue ? settings.OfficeLongitude.Value.ToString("0.0000000", CultureInfo.InvariantCulture) : string.Empty);

            IndiaComplianceLogger.Log(
                "Settings",
                "Saved India company settings for " + settings.CompanyName.Trim()
                + " | State=" + settings.CompanyState
                + " | GST Reg=" + settings.GSTRegistrationType
                + " | E-Invoice=" + (settings.EInvoiceAutoEnabled ? "Enabled" : "Disabled"));
        }

        private static string GetValue(Dictionary<string, string> settings, string key, string defaultValue)
        {
            return settings != null && settings.TryGetValue(key, out string value) ? value ?? string.Empty : defaultValue;
        }

        private static decimal GetDecimalValue(Dictionary<string, string> settings, string key, decimal defaultValue)
        {
            if (settings != null && settings.TryGetValue(key, out string value) && decimal.TryParse(value, out decimal parsed))
                return parsed;
            return defaultValue;
        }

        private static double? GetDoubleValue(Dictionary<string, string> settings, string key)
        {
            if (settings != null &&
                settings.TryGetValue(key, out string value) &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
