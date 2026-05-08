using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Helpers;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class RememberedLoginService
    {
        public bool HasSavedLogin()
        {
            return System.IO.File.Exists(SecureStorageHelper.RememberedSessionPath);
        }

        public void Save(RememberedSessionDto session)
        {
            if (session == null || session.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(session.SessionToken))
                return;

            string payload = string.Join("|",
                session.SessionId.ToString("D"),
                Escape(session.Username),
                Escape(session.SessionToken),
                Escape(session.RefreshToken),
                session.ExpiresAt.ToUniversalTime().Ticks.ToString());

            SecureStorageHelper.SaveProtectedText(SecureStorageHelper.RememberedSessionPath, payload);
            SecurityHelpers.LogAuthEvent("Remembered session token saved for this Windows user.");
        }

        [Obsolete("Do not store raw passwords. Use Save(RememberedSessionDto) instead.")]
        public void Save(string username, string password)
        {
            throw new NotSupportedException("ServoERP does not store raw passwords locally.");
        }

        public void Clear()
        {
            SecureStorageHelper.Delete(SecureStorageHelper.RememberedSessionPath);
        }

        public bool TryLoad(out RememberedSessionDto session)
        {
            session = null;
            try
            {
                string payload;
                if (!SecureStorageHelper.TryReadProtectedText(SecureStorageHelper.RememberedSessionPath, out payload))
                    return false;

                string[] parts = payload.Split('|');
                if (parts.Length != 5)
                    return false;

                Guid sessionId;
                long ticks;
                if (!Guid.TryParse(parts[0], out sessionId) || !long.TryParse(parts[4], out ticks))
                    return false;

                var expiresAt = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                if (expiresAt <= DateTime.Now)
                    return false;

                session = new RememberedSessionDto
                {
                    SessionId = sessionId,
                    Username = Unescape(parts[1]),
                    SessionToken = Unescape(parts[2]),
                    RefreshToken = Unescape(parts[3]),
                    ExpiresAt = expiresAt
                };
                return true;
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("Remembered session load failed.", ex);
                return false;
            }
        }

        public bool TryLoad(out string username, out string password)
        {
            username = null;
            password = null;
            RememberedSessionDto session;
            if (!TryLoad(out session))
                return false;

            username = session.Username;
            return true;
        }

        public bool VerifyWindowsHello(IWin32Window owner)
        {
            return true;
        }

        public void OpenSignInOptions()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:signinoptions") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("Unable to open Windows sign-in options.", ex);
            }
        }

        private static string Escape(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Unescape(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
        }
    }
}
