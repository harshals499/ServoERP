using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentValidation;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.Services
{
    // Public connection metadata only. Never add credentials, tokens or SQL connection strings.
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class MobileConnectionProfile
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; } = 1;
        [JsonProperty("companyId")] public int CompanyId { get; set; }
        [JsonProperty("addresses")] public List<string> Addresses { get; set; } = new List<string>();
        [JsonProperty("certificateSha256")] public string CertificateSha256 { get; set; } = "";

        internal void Validate()
        {
            CertificateSha256 = (CertificateSha256 ?? "").Replace(":", "").Replace(" ", "").ToUpperInvariant();
            new ProfileValidator().ValidateAndThrow(this);
            Addresses = Addresses.Select(a => new Uri(a).GetLeftPart(UriPartial.Authority))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private sealed class ProfileValidator : AbstractValidator<MobileConnectionProfile>
        {
            internal ProfileValidator()
            {
                RuleFor(p => p.SchemaVersion).Equal(1).WithMessage("Use a supported ServoERP office connection profile.");
                RuleFor(p => p.CompanyId).GreaterThan(0).WithMessage("Enter the company number provided by your office administrator.");
                RuleFor(p => p.Addresses).Must(a => a != null && a.Count >= 1 && a.Count <= 4 && a.All(IsOfficeAddress))
                    .WithMessage("Enter one to four HTTPS office addresses, one per line, without a path, username, query or fragment (maximum 200 characters each).");
                RuleFor(p => p.CertificateSha256).Must(f => f.Length == 0 || (f.Length == 64 && f.All(Uri.IsHexDigit)))
                    .WithMessage("Enter the full 64-character certificate SHA-256 fingerprint, or leave it blank for a publicly trusted certificate.");
            }

            private static bool IsOfficeAddress(string address)
            {
                Uri uri;
                return !string.IsNullOrWhiteSpace(address) && address.Length <= 200 &&
                    Uri.TryCreate(address, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                    !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" &&
                    string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
            }
        }
    }

    internal static class MobileConnectionService
    {
        internal const string SettingsKey = "MobileConnectionProfileV1";
        internal const string CodePrefix = "servoerp-office:v1:";

        internal static string ToJson(MobileConnectionProfile profile)
        {
            profile.Validate();
            return JsonConvert.SerializeObject(profile);
        }

        internal static string ToCode(MobileConnectionProfile profile)
        {
            return CodePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(ToJson(profile)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        internal static MobileConnectionProfile Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 8192)
                throw new ArgumentException("Choose a valid ServoERP office connection profile (up to 8 KB).");
            string json = value.Trim();
            if (json.StartsWith(CodePrefix, StringComparison.Ordinal))
            {
                string encoded = json.Substring(CodePrefix.Length).Replace('-', '+').Replace('_', '/');
                json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '=')));
            }
            var profile = JsonConvert.DeserializeObject<MobileConnectionProfile>(json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 8 });
            if (profile == null) throw new ArgumentException("Choose a valid ServoERP office connection profile.");
            profile.Validate();
            return profile;
        }

        internal static void Save(MobileConnectionProfile profile)
        {
            if (!string.Equals(SessionManager.CurrentUser?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Only an office administrator can change mobile connection settings.");
            new SettingsService().Set(SettingsKey, ToJson(profile));
        }
    }
}
