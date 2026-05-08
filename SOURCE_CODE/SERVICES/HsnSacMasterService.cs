using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class HsnSacMasterService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
        private readonly HsnSacMasterRepository _repo = new HsnSacMasterRepository();

        public List<HsnSacMasterEntry> GetAll()
        {
            return AppDataCache.GetOrCreate("hsnsac:all", CacheTtl, _repo.GetAll);
        }

        public void SaveAll(IEnumerable<HsnSacMasterEntry> entries)
        {
            var cleaned = new List<HsnSacMasterEntry>();
            foreach (HsnSacMasterEntry entry in entries ?? Enumerable.Empty<HsnSacMasterEntry>())
            {
                if (string.IsNullOrWhiteSpace(entry.Code) || string.IsNullOrWhiteSpace(entry.Description))
                    continue;

                HsnSacMasterEntry normalized = new HsnSacMasterEntry
                {
                    MasterID = entry.MasterID,
                    CodeType = string.Equals(entry.CodeType, "SAC", StringComparison.OrdinalIgnoreCase) ? "SAC" : "HSN",
                    Code = entry.Code.Trim().ToUpperInvariant(),
                    Description = entry.Description.Trim(),
                    BusinessCategory = string.IsNullOrWhiteSpace(entry.BusinessCategory) ? string.Empty : entry.BusinessCategory.Trim(),
                    TaxRate = ClampRate(entry.TaxRate),
                    CGSTRate = ClampRate(entry.CGSTRate),
                    SGSTRate = ClampRate(entry.SGSTRate),
                    IGSTRate = ClampRate(entry.IGSTRate),
                    Notes = string.IsNullOrWhiteSpace(entry.Notes) ? string.Empty : entry.Notes.Trim(),
                    IsDefault = entry.IsDefault,
                    IsActive = entry.IsActive
                };

                Validate(normalized);
                cleaned.Add(normalized);
            }

            _repo.SaveAll(cleaned);
            AppDataCache.RemovePrefix("hsnsac:");
            IndiaComplianceLogger.Log("HSN/SAC Master", "Saved " + cleaned.Count + " HSN/SAC entries.");
        }

        private static decimal ClampRate(decimal value)
        {
            if (value < 0m)
                return 0m;
            if (value > 100m)
                return 100m;
            return value;
        }

        private static void Validate(HsnSacMasterEntry entry)
        {
            bool isSac = string.Equals(entry.CodeType, "SAC", StringComparison.OrdinalIgnoreCase);
            if (isSac)
            {
                if (entry.Code.Length != 6 || !entry.Code.All(char.IsDigit))
                    throw new Exception("SAC code must be a 6-digit numeric code.");
            }
            else
            {
                if (entry.Code.Length < 4 || entry.Code.Length > 8 || !entry.Code.All(char.IsDigit))
                    throw new Exception("HSN code must be 4 to 8 digits.");
            }

            if (entry.CGSTRate + entry.SGSTRate != entry.TaxRate && entry.IGSTRate != entry.TaxRate)
                throw new Exception("GST split must match the headline tax rate for " + entry.Code + ".");
        }
    }
}
