using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services.Licensing;

namespace HVAC_Pro_Desktop.Services
{
    public static class SessionManager
    {
        private static readonly AuthRepository Repo = new AuthRepository();
        private static AppUserDto _currentUser;
        private static Guid? _currentSessionId;
        private static DateTime? _expiresAt;

        public static AppUserDto CurrentUser => _currentUser;
        public static bool IsLoggedIn => _currentUser != null;
        public static Guid? CurrentSessionId => _currentSessionId;
        public static DateTime? ExpiresAt => _expiresAt;

        public static void SetSession(AppUserDto user, Guid? sessionId = null, DateTime? expiresAt = null)
        {
            _currentUser = user;
            _currentSessionId = sessionId;
            _expiresAt = expiresAt;
        }

        public static void ClearSession()
        {
            _currentUser = null;
            _currentSessionId = null;
            _expiresAt = null;
        }

        public static bool HasPermission(string moduleKey, string action)
        {
            if (_currentUser == null || string.IsNullOrWhiteSpace(moduleKey) || string.IsNullOrWhiteSpace(action))
                return false;

            if (!new LicenseService().CanPerform(moduleKey, action))
                return false;

            return true;
        }

        public static void DemandPermission(string moduleKey, string action)
        {
            if (HasPermission(moduleKey, action))
                return;

            string act = string.IsNullOrWhiteSpace(action) ? "perform this action" : action.Trim().ToLowerInvariant();
            string module = string.IsNullOrWhiteSpace(moduleKey) ? "this module" : moduleKey.Trim();
            throw new UnauthorizedAccessException("You do not have permission to " + act + " in " + module + ", or the license is frozen.");
        }

        public static void LogAction(string action, string moduleKey, int? recordId, string description)
        {
            try
            {
                string normalizedAction = NormalizeAction(action);
                string normalizedModule = string.IsNullOrWhiteSpace(moduleKey) ? "System" : moduleKey.Trim();
                string actor = !string.IsNullOrWhiteSpace(_currentUser?.DisplayName)
                    ? _currentUser.DisplayName.Trim()
                    : (!string.IsNullOrWhiteSpace(_currentUser?.Username) ? _currentUser.Username.Trim() : Environment.UserName);
                string enterpriseDescription = BuildEnterpriseAuditDescription(normalizedAction, normalizedModule, recordId, actor, description);

                Repo.InsertAuditLog(
                    _currentUser?.UserId,
                    _currentUser?.Username,
                    normalizedAction,
                    normalizedModule,
                    recordId,
                    enterpriseDescription,
                    GetLocalIpAddress());
                AppendAuditFile(normalizedAction, normalizedModule, recordId, actor, enterpriseDescription);
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("Audit log write failed for action '" + action + "'", ex);
            }
        }

        private static string NormalizeAction(string action)
        {
            action = (action ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(action) ? "ACTION" : action;
        }

        private static string BuildEnterpriseAuditDescription(string action, string moduleKey, int? recordId, string actor, string detail)
        {
            string verb;
            switch (action)
            {
                case "CREATE": verb = "created"; break;
                case "EDIT": verb = "edited"; break;
                case "DELETE": verb = "deleted"; break;
                case "ARCHIVE": verb = "archived"; break;
                case "MERGE": verb = "merged"; break;
                case "LOGIN": verb = "logged in"; break;
                case "LOGOUT": verb = "logged out"; break;
                case "SAVE": verb = "saved"; break;
                case "UPLOAD": verb = "uploaded"; break;
                case "PROCESS": verb = "processed"; break;
                case "LOCK": verb = "locked"; break;
                case "PAY": verb = "paid"; break;
                case "SYNC": verb = "synced"; break;
                case "CONNECT": verb = "connected"; break;
                case "DISCONNECT": verb = "disconnected"; break;
                case "FRESH_START": verb = "ran Fresh Start"; break;
                default: verb = action.ToLowerInvariant(); break;
            }

            string entity = moduleKey;
            if (recordId.HasValue)
                entity += " #" + recordId.Value;

            string sentence = action == "LOGIN" || action == "LOGOUT"
                ? actor + " " + verb
                : entity + " " + verb + " by " + actor;

            return string.IsNullOrWhiteSpace(detail)
                ? sentence
                : sentence + ". " + detail.Trim();
        }

        private static void AppendAuditFile(string action, string moduleKey, int? recordId, string actor, string description)
        {
            try
            {
                Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\LOGS");
                File.AppendAllText(
                    @"C:\HVAC_PRO_MSE\LOGS\audit-trail.log",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + " | " + actor
                    + " | " + action
                    + " | " + moduleKey
                    + " | " + (recordId.HasValue ? recordId.Value.ToString() : "")
                    + " | " + description
                    + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                return ip?.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }
}
