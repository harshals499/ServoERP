using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class LicenseCacheStore : ILicenseCacheStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ServoERP-LicenseCache-v1");
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public static string CachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "license.cache");
        private static string UserCachePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "license.cache");

        public LicenseSnapshot Load()
        {
            LicenseSnapshot snapshot;
            if (TryLoadFrom(CachePath, out snapshot))
                return snapshot;

            if (TryLoadFrom(UserCachePath, out snapshot))
                return snapshot;

            return null;
        }

        private bool TryLoadFrom(string path, out LicenseSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                if (!File.Exists(path))
                    return false;

                byte[] protectedBytes = File.ReadAllBytes(path);
                byte[] plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                string json = Encoding.UTF8.GetString(plain);
                snapshot = _serializer.Deserialize<LicenseSnapshot>(json);
                return snapshot != null;
            }
            catch (UnauthorizedAccessException ex)
            {
                AppLogger.LogError("LicenseCacheStore.Load:" + path, ex);
                return false;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("LicenseCacheStore.Load:" + path, ex);
                snapshot = new LicenseSnapshot
                {
                    Status = LicenseStatus.Tampered,
                    StatusMessage = "License cache is damaged or has been modified. Reactivation is required."
                };
                return true;
            }
        }

        public void Save(LicenseSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            string json = _serializer.Serialize(snapshot);
            byte[] plain = Encoding.UTF8.GetBytes(json);
            byte[] protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine);
            if (TryWrite(CachePath, protectedBytes))
                return;

            TryWrite(UserCachePath, protectedBytes);
        }

        public void Clear()
        {
            TryDelete(CachePath);
            TryDelete(UserCachePath);
        }

        private static bool TryWrite(string path, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("LicenseCacheStore.Save:" + path, ex);
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("LicenseCacheStore.Clear:" + path, ex);
            }
        }
    }
}
