using System;
using System.Collections.Generic;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.OfflineDesktop
{
    internal sealed class OfflineProfile
    {
        public AppUserDto User { get; set; }
        public string VerifierHash { get; set; }
        public string CredentialMarker { get; set; }
        public bool LegalAccepted { get; set; }
        public DateTime LastOnlineUtc { get; set; }
        public DateTime LastObservedUtc { get; set; }
        public DateTime? LastSnapshotUtc { get; set; }
        public DateTime? LockoutUntilUtc { get; set; }
        public int FailedAttempts { get; set; }
    }

    internal sealed class OfflineRecord
    {
        public Guid EntityId { get; set; }
        public string Module { get; set; }
        public int ServerId { get; set; }
        public string Revision { get; set; }
        public string Status { get; set; } = "Downloaded";
        public JObject Data { get; set; } = new JObject();
        public string DisplayName
        {
            get
            {
                foreach(string key in new[]{"CompanyName","SiteName","Title","InvoiceNumber","PaymentNumber","Description","ReferenceNumber"})
                {var value=Data[key];if(value!=null && value.Type!=JTokenType.Null && !string.IsNullOrWhiteSpace((string)value))return (string)value;}
                return Module+" "+EntityId.ToString("N").Substring(0,8).ToUpperInvariant();
            }
        }
    }

    internal sealed class OfflineOperation
    {
        public Guid OperationId { get; set; }
        public Guid DeviceId { get; set; }
        public Guid? ReviewOfOperationId { get; set; }
        public Guid EntityId { get; set; }
        public string Module { get; set; }
        public int ServerId { get; set; }
        public string BaseRevision { get; set; }
        public string PayloadHash { get; set; }
        public bool Attempted { get; set; }
        public JObject Data { get; set; }
        public string Status { get; set; }
        public string Error { get; set; }
        public JObject ServerCopy { get; set; }
    }

    internal sealed class OfflineReceipt
    {
        public Guid OperationId { get; set; }
        public Guid EntityId { get; set; }
        public int ServerId { get; set; }
        public string Status { get; set; }
        public string Revision { get; set; }
        public string Error { get; set; }
        public JObject PreviousData { get; set; }
        public JObject Data { get; set; }
        public JObject ServerCopy { get; set; }
    }

    internal static class OfflineJson
    {
        // Existing SQL rows can have NULL in numeric columns mapped to value-type DTO properties.
        // Match the normal repositories' default-value mapping without altering the canonical row fingerprint.
        public static T Model<T>(JObject data) => data.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings {NullValueHandling=NullValueHandling.Ignore}));
    }

    internal static class OfflineModules
    {
        public static readonly string[] All = { "Clients", "Sites", "Jobs", "Invoices", "Payments", "InvoiceDrafts", "PaymentDrafts" };
        public static bool IsDraft(string module) => module == "InvoiceDrafts" || module == "PaymentDrafts";
        public static string PermissionKey(string module) => module == "Sites" ? "Clients" : module == "Jobs" ? "WorkOrders" : module == "InvoiceDrafts" ? "Invoices" : module == "PaymentDrafts" ? "Payments" : module;
        public static bool CanWrite(string module) => module == "Clients" || module == "Sites" || module == "Jobs" || IsDraft(module);
        public static bool HasPermission(AppUserDto user, string module, bool write)
        {
            if(user == null || !user.IsActive || user.ForcePasswordChange) return false;
            RolePermissionDto permission;
            if(user.Permissions == null || !user.Permissions.TryGetValue(PermissionKey(module), out permission)) return false;
            return permission.CanView && (!write || permission.CanCreate || permission.CanEdit);
        }
    }
}
