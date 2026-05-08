using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;

namespace HVAC_Pro_Desktop.Services.Validation
{
    public sealed class DuplicateDetectionService
    {
        public ValidationResult CheckClient(B2BClient client, IEnumerable<B2BClient> existing)
        {
            var result = new ValidationResult();
            if (client == null) return result;
            string name = Normalize(client.CompanyName);
            string gst = Normalize(client.GSTNumber);
            foreach (B2BClient other in existing ?? Enumerable.Empty<B2BClient>())
            {
                if (other.ClientID == client.ClientID) continue;
                if (!string.IsNullOrWhiteSpace(gst) && string.Equals(gst, Normalize(other.GSTNumber), StringComparison.OrdinalIgnoreCase))
                    result.Add(ValidationSeverity.Error, "Clients", "GSTIN", "Another client already has this GSTIN: " + other.CompanyName, "Open existing client instead of creating a duplicate.");
                else if (!string.IsNullOrWhiteSpace(name) && string.Equals(name, Normalize(other.CompanyName), StringComparison.OrdinalIgnoreCase))
                    result.Add(ValidationSeverity.Error, "Clients", "CompanyName", "Duplicate client name already exists: " + other.CompanyName, "Open existing client instead of creating a duplicate.");
            }
            return result;
        }

        public ValidationResult CheckVendor(Vendor vendor, IEnumerable<Vendor> existing)
        {
            var result = new ValidationResult();
            if (vendor == null) return result;
            string name = Normalize(vendor.VendorName);
            string gst = Normalize(vendor.GSTNumber);
            foreach (Vendor other in existing ?? Enumerable.Empty<Vendor>())
            {
                if (other.VendorID == vendor.VendorID) continue;
                if (!string.IsNullOrWhiteSpace(gst) && string.Equals(gst, Normalize(other.GSTNumber), StringComparison.OrdinalIgnoreCase))
                    result.Add(ValidationSeverity.Error, "Vendors", "GSTIN", "Another vendor already has this GSTIN: " + other.VendorName, "Merge or open existing vendor.");
                else if (!string.IsNullOrWhiteSpace(name) && string.Equals(name, Normalize(other.VendorName), StringComparison.OrdinalIgnoreCase))
                    result.Add(ValidationSeverity.Error, "Vendors", "VendorName", "Duplicate vendor name already exists: " + other.VendorName, "Open existing vendor instead of creating a duplicate.");
            }
            return result;
        }

        private static string Normalize(string value)
        {
            value = (value ?? string.Empty).Trim().ToUpperInvariant();
            value = Regex.Replace(value, @"\b(PVT|PRIVATE|LIMITED|LTD|LLP|M/S|MR|MRS)\b\.?", " ");
            return Regex.Replace(value, @"\s+", " ").Trim();
        }
    }
}
