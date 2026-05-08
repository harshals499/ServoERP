using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace HVAC_Pro_Desktop.Services.Licensing
{
    public sealed class DeviceFingerprintService : IDeviceFingerprintService
    {
        public string GetFingerprintHash()
        {
            string raw = Environment.MachineName
                + "|" + Environment.UserDomainName
                + "|" + WindowsIdentity.GetCurrent().User?.Value
                + "|" + GetSystemDriveRoot();

            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        }

        public string GetDeviceName()
        {
            return Environment.MachineName;
        }

        private static string GetSystemDriveRoot()
        {
            try
            {
                return Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
