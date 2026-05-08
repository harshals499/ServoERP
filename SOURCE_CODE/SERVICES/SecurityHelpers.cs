using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HVAC_Pro_Desktop.Services
{
    internal static class SecurityHelpers
    {
        private const string SaltChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

        public static string GenerateSalt(int length = 32)
        {
            if (length <= 0)
                length = 32;

            byte[] data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(data);

            char[] chars = new char[length];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = SaltChars[data[i] % SaltChars.Length];
            return new string(chars);
        }

        public static string HashPassword(string password, string salt)
        {
            password = password ?? string.Empty;
            salt = salt ?? string.Empty;

            if (string.Equals(salt, "bcrypt", StringComparison.OrdinalIgnoreCase))
                return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

            using (var sha = SHA256.Create())
            {
                byte[] raw = Encoding.UTF8.GetBytes(salt + password);
                byte[] hash = sha.ComputeHash(raw);
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }

        public static string HashPasswordWithBcrypt(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password ?? string.Empty, workFactor: 12);
        }

        public static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            storedHash = storedHash ?? string.Empty;
            storedSalt = storedSalt ?? string.Empty;

            if (storedHash.StartsWith("$2", StringComparison.Ordinal))
                return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, storedHash);

            string legacyHash = HashPassword(password, storedSalt);
            return FixedTimeEquals(legacyHash, storedHash);
        }

        public static string HashToken(string token)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty));
                return Convert.ToBase64String(hash);
            }
        }

        public static string CreateSecureToken(int byteLength = 48)
        {
            byte[] bytes = new byte[Math.Max(32, byteLength)];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] a = Encoding.UTF8.GetBytes(left ?? string.Empty);
            byte[] b = Encoding.UTF8.GetBytes(right ?? string.Empty);
            if (a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static bool MeetsPasswordPolicy(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasDigit = password.Any(char.IsDigit);
            return hasUpper && hasDigit;
        }

        public static void LogAuthEvent(string message, Exception ex = null)
        {
            try
            {
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "auth-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                var sb = new StringBuilder();
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message);
                if (ex != null)
                    sb.AppendLine(ex.ToString());
                File.AppendAllText(path, sb.ToString());
            }
            catch
            {
            }
        }
    }
}
