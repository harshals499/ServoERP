using System.Text.RegularExpressions;

namespace HVAC_Pro_Desktop.Services
{
    internal static class SensitiveDataRedactor
    {
        private const string Redacted = "[REDACTED]";

        private static readonly Regex[] Patterns =
        {
            new Regex(@"(?i)\b(Password|Pwd|Pass|AccessToken|RefreshToken|BearerToken|Token|ApiKey|APIKey|ClientSecret|Secret|LicenseKey)\b\s*([=:])\s*([^;\s,}\]]+)", RegexOptions.Compiled),
            new Regex(@"(?i)(;?\s*(Password|Pwd|User ID|UserID)\s*=\s*)([^;]+)", RegexOptions.Compiled),
            new Regex(@"(?i)(\bAuthorization\s*[:=]\s*Bearer\s+)([A-Za-z0-9._\-+/=]+)", RegexOptions.Compiled),
            new Regex(@"(?i)([?&](key|access_token|refresh_token|token|api_key|client_secret)=)([^&\s]+)", RegexOptions.Compiled),
            new Regex(@"(?i)(<(Password|AccessToken|BearerToken|Token|ClientSecret|ApiKey)>)(.*?)(</\2>)", RegexOptions.Compiled),
            new Regex(@"\b[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]\b", RegexOptions.Compiled),
            new Regex(@"\b[A-Z]{5}[0-9]{4}[A-Z]\b", RegexOptions.Compiled),
            new Regex(@"(?i)\b(GSTIN|GSTNumber|PAN|PANNumber|BankAccountNumber|BankAccount|BankIFSC|IFSC)\b\s*([=:])\s*([^;\r\n,}\]]+)", RegexOptions.Compiled)
        };

        public static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            string redacted = value;
            redacted = Patterns[0].Replace(redacted, "$1$2" + Redacted);
            redacted = Patterns[1].Replace(redacted, "$1" + Redacted);
            redacted = Patterns[2].Replace(redacted, "$1" + Redacted);
            redacted = Patterns[3].Replace(redacted, "$1" + Redacted);
            redacted = Patterns[4].Replace(redacted, "$1" + Redacted + "$4");
            redacted = Patterns[5].Replace(redacted, Redacted);
            redacted = Patterns[6].Replace(redacted, Redacted);
            redacted = Patterns[7].Replace(redacted, "$1$2" + Redacted);
            return redacted;
        }
    }
}
