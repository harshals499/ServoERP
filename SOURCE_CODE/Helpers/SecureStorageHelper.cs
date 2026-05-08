using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HVAC_Pro_Desktop.Helpers
{
    public static class SecureStorageHelper
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ServoERP.AuthToken.v2");

        public static string StoreDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ServoERP");

        public static string RememberedSessionPath => Path.Combine(StoreDirectory, "remembered-session.dat");

        public static void SaveProtectedText(string path, string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] raw = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] protectedBytes = ProtectedData.Protect(raw, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, protectedBytes);
        }

        public static bool TryReadProtectedText(string path, out string value)
        {
            value = null;
            try
            {
                if (!File.Exists(path))
                    return false;

                byte[] protectedBytes = File.ReadAllBytes(path);
                byte[] raw = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                value = Encoding.UTF8.GetString(raw);
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
