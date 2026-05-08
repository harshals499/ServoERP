using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public static class LocalLoginBypassService
    {
        private const string MarkerFileName = "local-login-bypass.enabled";

        public static string MarkerPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ServoERP");
                return Path.Combine(folder, MarkerFileName);
            }
        }

        public static bool TryStartSession(out string message)
        {
            message = string.Empty;

            try
            {
                if (!IsEnabledForThisPc(out message))
                    return false;

                var repo = new AuthRepository();
                List<ManagedUserDto> activeUsers = repo.GetUsers()
                    .Where(u => u.IsActive)
                    .ToList();

                ManagedUserDto selected = activeUsers
                    .FirstOrDefault(u => string.Equals(u.Username, "admin", StringComparison.OrdinalIgnoreCase))
                    ?? activeUsers.FirstOrDefault(u => (u.RoleName ?? string.Empty).IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? activeUsers.FirstOrDefault();

                if (selected == null)
                {
                    message = "Local login bypass is enabled, but no active app user was found.";
                    SecurityHelpers.LogAuthEvent(message);
                    return false;
                }

                AppUserDto user = repo.GetUserByUsername(selected.Username);
                if (user == null || !user.IsActive)
                {
                    message = "Local login bypass could not load the selected app user.";
                    SecurityHelpers.LogAuthEvent(message);
                    return false;
                }

                user.Permissions = repo.GetPermissionsForRole(user.RoleId);
                repo.UpdateLastLogin(user.UserId);
                user.LastLoginDate = DateTime.Now;
                SessionManager.SetSession(user);
                SessionManager.LogAction("LOGIN", null, null, "Local PC login bypass");
                SecurityHelpers.LogAuthEvent("Local PC login bypass successful for '" + user.Username + "'.");

                message = "Local login bypass active for " + user.Username + ".";
                return true;
            }
            catch (Exception ex)
            {
                message = "Local login bypass failed; falling back to login screen.";
                SecurityHelpers.LogAuthEvent(message, ex);
                return false;
            }
        }

        private static bool IsEnabledForThisPc(out string message)
        {
            message = string.Empty;

            if (!File.Exists(MarkerPath))
                return false;

            Dictionary<string, string> values = File.ReadAllLines(MarkerPath)
                .Select(line => line.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(
                    parts => parts[0].Trim(),
                    parts => parts[1].Trim(),
                    StringComparer.OrdinalIgnoreCase);

            if (!values.TryGetValue("enabled", out string enabled) ||
                !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
                return false;

            if (values.TryGetValue("machine", out string machine) &&
                !string.IsNullOrWhiteSpace(machine) &&
                !string.Equals(machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                message = "Local login bypass marker is for another machine.";
                return false;
            }

            if (values.TryGetValue("windowsUser", out string windowsUser) &&
                !string.IsNullOrWhiteSpace(windowsUser) &&
                !string.Equals(windowsUser, Environment.UserName, StringComparison.OrdinalIgnoreCase))
            {
                message = "Local login bypass marker is for another Windows user.";
                return false;
            }

            return true;
        }
    }
}
