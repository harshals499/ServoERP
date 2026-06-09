using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class AuthService
    {
        private readonly AuthRepository _repo = new AuthRepository();

        public Task<LoginResultDto> LoginAsync(string username, string password, bool createRememberedSession)
        {
            return Task.Run(() => Login(username, password, createRememberedSession));
        }

        public LoginResultDto Login(string username, string password)
        {
            return Login(username, password, false);
        }

        public LoginResultDto Login(string username, string password, bool createRememberedSession)
        {
            try
            {
                string safeUsername = (username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(safeUsername) || string.IsNullOrWhiteSpace(password))
                    return Fail("Invalid username or password");

                var credentials = _repo.GetCredentialRecord(safeUsername);
                if (!credentials.HasValue || !credentials.Value.IsActive)
                {
                    if (credentials.HasValue && !credentials.Value.IsActive)
                        SecurityHelpers.LogAuthEvent("Inactive login blocked for user '" + safeUsername + "'.");
                    _repo.InsertLoginAudit(null, safeUsername, false, "invalid", GetLocalIpAddress(), Environment.MachineName);
                    return Fail("Invalid username or password");
                }

                if (credentials.Value.LockoutUntil.HasValue && credentials.Value.LockoutUntil.Value > DateTime.Now)
                {
                    _repo.InsertLoginAudit(null, safeUsername, false, "locked", GetLocalIpAddress(), Environment.MachineName);
                    return new LoginResultDto
                    {
                        Success = false,
                        IsLockedOut = true,
                        ErrorMessage = "This account is temporarily locked. Try again later or contact administrator."
                    };
                }

                if (!SecurityHelpers.VerifyPassword(password, credentials.Value.PasswordHash, credentials.Value.PasswordSalt))
                {
                    _repo.RecordFailedLogin(safeUsername, true);
                    SecurityHelpers.LogAuthEvent("Invalid password attempt for user '" + safeUsername + "'.");
                    _repo.InsertLoginAudit(null, safeUsername, false, "invalid", GetLocalIpAddress(), Environment.MachineName);
                    return new LoginResultDto
                    {
                        Success = false,
                        IsLockedOut = false,
                        ErrorMessage = "Invalid username or password"
                    };
                }

                AppUserDto user = _repo.GetUserByUsername(safeUsername);
                if (user == null || !user.IsActive)
                    return Fail("Invalid username or password");

                user.Permissions = _repo.GetPermissionsForRole(user.RoleId);
                _repo.UpdateLastLogin(user.UserId);
                if (!credentials.Value.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
                    _repo.UpdatePassword(user.UserId, SecurityHelpers.HashPasswordWithBcrypt(password), "bcrypt", user.ForcePasswordChange);

                user.LastLoginDate = DateTime.Now;
                Guid? sessionId = null;
                string sessionToken = null;
                string refreshToken = null;
                DateTime? expiresAt = null;

                if (createRememberedSession)
                {
                    sessionToken = SecurityHelpers.CreateSecureToken();
                    refreshToken = SecurityHelpers.CreateSecureToken();
                    expiresAt = DateTime.Now.AddDays(30);
                    sessionId = _repo.CreateSession(
                        user.UserId,
                        SecurityHelpers.HashToken(sessionToken),
                        SecurityHelpers.HashToken(refreshToken),
                        expiresAt.Value,
                        GetLocalIpAddress(),
                        Environment.MachineName);
                }

                SessionManager.SetSession(user, sessionId, expiresAt);
                SessionManager.LogAction("LOGIN", null, null, "Login successful");
                _repo.InsertLoginAudit(user.UserId, user.Username, true, null, GetLocalIpAddress(), Environment.MachineName);

                return new LoginResultDto
                {
                    Success = true,
                    RequiresPasswordChange = user.ForcePasswordChange,
                    User = user,
                    SessionId = sessionId,
                    SessionToken = sessionToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("Login failed unexpectedly for '" + username + "'.", ex);
                return Fail("Invalid username or password");
            }
        }

        public Task<LoginResultDto> TryResumeSessionAsync(RememberedSessionDto remembered)
        {
            return Task.Run(() => TryResumeSession(remembered));
        }

        public LoginResultDto TryResumeSession(RememberedSessionDto remembered)
        {
            try
            {
                if (remembered == null || remembered.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(remembered.SessionToken))
                    return Fail("Invalid username or password");

                AppUserDto user = _repo.GetUserByValidSession(remembered.SessionId, SecurityHelpers.HashToken(remembered.SessionToken));
                if (user == null)
                    return Fail("Invalid username or password");

                user.Permissions = _repo.GetPermissionsForRole(user.RoleId);
                _repo.TouchSession(remembered.SessionId);
                SessionManager.SetSession(user, remembered.SessionId, remembered.ExpiresAt);
                SessionManager.LogAction("LOGIN", null, null, "Remembered session resumed");
                _repo.InsertLoginAudit(user.UserId, user.Username, true, null, GetLocalIpAddress(), Environment.MachineName);
                return new LoginResultDto { Success = true, User = user, SessionId = remembered.SessionId, ExpiresAt = remembered.ExpiresAt };
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("Remembered session resume failed.", ex);
                return Fail("Invalid username or password");
            }
        }

        public bool ChangePassword(int userId, string currentPassword, string newPassword)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            if (!SecurityHelpers.MeetsPasswordPolicy(newPassword))
                return false;

            try
            {
                ManagedUserDto user = GetUsers().FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                    return false;

                var credentials = _repo.GetCredentialRecord(user.Username);
                if (!credentials.HasValue)
                    return false;

                if (!SecurityHelpers.VerifyPassword(currentPassword, credentials.Value.PasswordHash, credentials.Value.PasswordSalt))
                    return false;

                string newSalt = "bcrypt";
                string newHash = SecurityHelpers.HashPasswordWithBcrypt(newPassword);
                _repo.UpdatePassword(userId, newHash, newSalt, false);
                SessionManager.LogAction("EDIT", "Settings", userId, "Password changed");

                if (SessionManager.CurrentUser != null && SessionManager.CurrentUser.UserId == userId)
                    SessionManager.CurrentUser.ForcePasswordChange = false;

                return true;
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("ChangePassword failed for userId " + userId + ".", ex);
                return false;
            }
        }

        public (bool Success, string ErrorMessage, int UserId) ResetOwnPassword(string usernameOrEmail, string newPassword)
        {
            try
            {
                string safeUsername = (usernameOrEmail ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(safeUsername))
                    return (false, "Enter your username or email.", 0);

                if (!SecurityHelpers.MeetsPasswordPolicy(newPassword))
                    return (false, "Password must be 8+ chars with 1 uppercase and 1 number.", 0);

                AppUserDto user = _repo.GetUserByUsername(safeUsername);
                if (user == null || !user.IsActive)
                    return (false, "No active account found for this username or email.", 0);

                string salt = "bcrypt";
                string hash = SecurityHelpers.HashPasswordWithBcrypt(newPassword);
                _repo.UpdatePassword(user.UserId, hash, salt, false);
                _repo.ResetFailedAttempts(user.UserId);
                SessionManager.LogAction("EDIT", "Login", user.UserId, "Self-service password reset");
                return (true, null, user.UserId);
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("ResetOwnPassword failed for '" + usernameOrEmail + "'.", ex);
                return (false, "Unable to reset password.", 0);
            }
        }

        public void Logout()
        {
            try
            {
                if (SessionManager.IsLoggedIn)
                    SessionManager.LogAction("LOGOUT", null, null, "User logged out");
                if (SessionManager.CurrentSessionId.HasValue)
                    _repo.RevokeSession(SessionManager.CurrentSessionId.Value);
            }
            finally
            {
                SessionManager.ClearSession();
            }
        }

        public List<RoleDto> GetRoles()
        {
            return _repo.GetRoles();
        }

        public List<ManagedUserDto> GetUsers()
        {
            return _repo.GetUsers();
        }

        public DataTable GetAuditLog(DateTime fromDate, DateTime toDate, string username)
        {
            return _repo.GetAuditLog(fromDate, toDate, username);
        }

        public (bool Success, string TempPassword, string ErrorMessage, int UserId) CreateUser(string username, string displayName, int roleId, bool isActive)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName) || roleId <= 0)
                    return (false, null, "Username, display name, and role are required.", 0);

                if (_repo.GetUserByUsername(username.Trim()) != null)
                    return (false, null, "Username already exists.", 0);

                string tempPassword = "Temp@" + new Random().Next(1000, 9999).ToString();
                string salt = "bcrypt";
                string hash = SecurityHelpers.HashPasswordWithBcrypt(tempPassword);
                int userId = _repo.CreateUser(username.Trim(), displayName.Trim(), roleId, hash, salt, isActive, true);
                SessionManager.LogAction("CREATE", "Settings", userId, "User created: " + username.Trim());
                return (true, tempPassword, null, userId);
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("CreateUser failed for '" + username + "'.", ex);
                return (false, null, "Unable to create user.", 0);
            }
        }

        public bool HasAnyUsers()
        {
            try
            {
                return _repo.GetUsers().Count > 0;
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("HasAnyUsers failed.", ex);
                return true;
            }
        }

        public bool ValidateAdminCredentials(string username, string password)
        {
            try
            {
                string safeUsername = (username ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(safeUsername) || string.IsNullOrWhiteSpace(password))
                    return false;

                var credentials = _repo.GetCredentialRecord(safeUsername);
                if (!credentials.HasValue || !credentials.Value.IsActive)
                    return false;

                AppUserDto user = _repo.GetUserByUsername(safeUsername);
                if (user == null || !user.IsActive)
                    return false;

                return SecurityHelpers.VerifyPassword(password, credentials.Value.PasswordHash, credentials.Value.PasswordSalt);
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("ValidateAdminCredentials failed for '" + username + "'.", ex);
                return false;
            }
        }

        public (bool Success, string ErrorMessage, int UserId) CreateUserWithPassword(string username, string displayName, int roleId, string password, bool forcePasswordChange)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(displayName) || roleId <= 0)
                    return (false, "Username, display name, and role are required.", 0);

                if (!SecurityHelpers.MeetsPasswordPolicy(password))
                    return (false, "Password must meet the security policy.", 0);

                if (_repo.GetUserByUsername(username.Trim()) != null)
                    return (false, "Username already exists.", 0);

                string salt = "bcrypt";
                string hash = SecurityHelpers.HashPasswordWithBcrypt(password);
                int userId = _repo.CreateUser(username.Trim(), displayName.Trim(), roleId, hash, salt, true, forcePasswordChange);
                SessionManager.LogAction("CREATE", "Login", userId, "User account created: " + username.Trim());
                return (true, null, userId);
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("CreateUserWithPassword failed for '" + username + "'.", ex);
                return (false, "Unable to create account.", 0);
            }
        }

        public bool UpdateUser(int userId, string username, string displayName, int roleId, bool isActive)
        {
            try
            {
                AppUserDto existing = _repo.GetUserByUsername(username?.Trim());
                if (existing != null && existing.UserId != userId)
                    return false;

                _repo.UpdateUser(userId, username?.Trim(), displayName?.Trim(), roleId, isActive);
                SessionManager.LogAction("EDIT", "Settings", userId, "User updated: " + (username ?? string.Empty).Trim());
                return true;
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("UpdateUser failed for userId " + userId + ".", ex);
                return false;
            }
        }

        public (bool Success, string TempPassword, string ErrorMessage) ResetPassword(int userId)
        {
            try
            {
                if (userId <= 0)
                    return (false, null, "Invalid user.");

                string tempPassword = "Temp@" + new Random().Next(1000, 9999).ToString();
                string salt = "bcrypt";
                string hash = SecurityHelpers.HashPasswordWithBcrypt(tempPassword);
                _repo.UpdatePassword(userId, hash, salt, true);
                SessionManager.LogAction("EDIT", "Settings", userId, "Password reset");
                return (true, tempPassword, null);
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("ResetPassword failed for userId " + userId + ".", ex);
                return (false, null, "Unable to reset password.");
            }
        }

        public bool SetUserActive(int userId, bool isActive)
        {
            try
            {
                if (SessionManager.CurrentUser != null && SessionManager.CurrentUser.UserId == userId && !isActive)
                    return false;

                _repo.SetUserActive(userId, isActive);
                SessionManager.LogAction("EDIT", "Settings", userId, isActive ? "User activated" : "User deactivated");
                return true;
            }
            catch (Exception ex)
            {
                SecurityHelpers.LogAuthEvent("SetUserActive failed for userId " + userId + ".", ex);
                return false;
            }
        }

        private static LoginResultDto Fail(string message)
        {
            return new LoginResultDto
            {
                Success = false,
                ErrorMessage = message
            };
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
