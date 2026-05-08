using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class AuthRepository
    {
        private readonly DatabaseManager _db = new DatabaseManager();

        public AppUserDto GetUserByUsername(string username)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT u.UserId, u.Username, u.DisplayName, u.RoleId, r.RoleName, u.IsActive,
                           u.LastLoginDate, u.ForcePasswordChange, u.PasswordHash, u.PasswordSalt,
                           u.FailedAttempts, u.LockoutUntil
                    FROM AppUsers u
                    INNER JOIN AppRoles r ON u.RoleId = r.RoleId
                    WHERE u.Username = @username OR u.Email = @username";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username ?? string.Empty);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        return new AppUserDto
                        {
                            UserId = Convert.ToInt32(r["UserId"]),
                            Username = Convert.ToString(r["Username"]),
                            DisplayName = Convert.ToString(r["DisplayName"]),
                            RoleId = Convert.ToInt32(r["RoleId"]),
                            RoleName = Convert.ToString(r["RoleName"]),
                            IsActive = r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"]),
                            LastLoginDate = r["LastLoginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LastLoginDate"]),
                            ForcePasswordChange = r["ForcePasswordChange"] != DBNull.Value && Convert.ToBoolean(r["ForcePasswordChange"]),
                            FailedAttempts = r["FailedAttempts"] == DBNull.Value ? 0 : Convert.ToInt32(r["FailedAttempts"]),
                            LockoutUntil = r["LockoutUntil"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LockoutUntil"]),
                            Permissions = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase)
                        };
                    }
                }
            }
        }

        public (string PasswordHash, string PasswordSalt, bool IsActive, bool ForcePasswordChange, int FailedAttempts, DateTime? LockoutUntil)? GetCredentialRecord(string username)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT PasswordHash, PasswordSalt, IsActive, ForcePasswordChange, FailedAttempts, LockoutUntil
                    FROM AppUsers
                    WHERE Username = @username OR Email = @username";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username ?? string.Empty);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        return (
                            Convert.ToString(r["PasswordHash"]),
                            Convert.ToString(r["PasswordSalt"]),
                            r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"]),
                            r["ForcePasswordChange"] != DBNull.Value && Convert.ToBoolean(r["ForcePasswordChange"]),
                            r["FailedAttempts"] == DBNull.Value ? 0 : Convert.ToInt32(r["FailedAttempts"]),
                            r["LockoutUntil"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LockoutUntil"]));
                    }
                }
            }
        }

        public Dictionary<string, RolePermissionDto> GetPermissionsForRole(int roleId)
        {
            var map = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase);
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT PermissionId, RoleId, ModuleKey, CanView, CanCreate, CanEdit, CanDelete
                    FROM RolePermissions
                    WHERE RoleId = @roleId";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@roleId", roleId);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var permission = new RolePermissionDto
                            {
                                PermissionId = Convert.ToInt32(r["PermissionId"]),
                                RoleId = Convert.ToInt32(r["RoleId"]),
                                ModuleKey = Convert.ToString(r["ModuleKey"]),
                                CanView = r["CanView"] != DBNull.Value && Convert.ToBoolean(r["CanView"]),
                                CanCreate = r["CanCreate"] != DBNull.Value && Convert.ToBoolean(r["CanCreate"]),
                                CanEdit = r["CanEdit"] != DBNull.Value && Convert.ToBoolean(r["CanEdit"]),
                                CanDelete = r["CanDelete"] != DBNull.Value && Convert.ToBoolean(r["CanDelete"])
                            };
                            map[permission.ModuleKey] = permission;
                        }
                    }
                }
            }
            return map;
        }

        public void UpdateLastLogin(int userId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE AppUsers SET LastLoginDate = GETDATE(), FailedAttempts = 0, LockoutUntil = NULL WHERE UserId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RecordFailedLogin(string username, bool applyLockout)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE AppUsers
                    SET FailedAttempts = ISNULL(FailedAttempts, 0) + 1,
                        LockoutUntil = CASE
                            WHEN @applyLockout = 1 AND ISNULL(FailedAttempts, 0) + 1 >= 5 THEN DATEADD(minute, 15, GETDATE())
                            ELSE LockoutUntil
                        END
                    WHERE Username = @username OR Email = @username";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username ?? string.Empty);
                    cmd.Parameters.AddWithValue("@applyLockout", applyLockout ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ResetFailedAttempts(int userId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE AppUsers SET FailedAttempts = 0, LockoutUntil = NULL WHERE UserId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdatePassword(int userId, string passwordHash, string passwordSalt, bool forcePasswordChange)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE AppUsers
                    SET PasswordHash = @hash,
                        PasswordSalt = @salt,
                        ForcePasswordChange = @force,
                        FailedAttempts = 0,
                        LockoutUntil = NULL
                    WHERE UserId = @id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.Parameters.AddWithValue("@hash", passwordHash ?? string.Empty);
                    cmd.Parameters.AddWithValue("@salt", passwordSalt ?? string.Empty);
                    cmd.Parameters.AddWithValue("@force", forcePasswordChange ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<RoleDto> GetRoles()
        {
            var roles = new List<RoleDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT RoleId, RoleName, Description
                    FROM AppRoles
                    ORDER BY CASE RoleName
                        WHEN 'Admin' THEN 1
                        WHEN 'Accountant' THEN 2
                        WHEN 'Dispatcher' THEN 3
                        WHEN 'Technician' THEN 4
                        WHEN 'Manager' THEN 5
                        WHEN 'Accounts' THEN 6
                        WHEN 'Supervisor' THEN 7
                        WHEN 'Viewer' THEN 8
                        ELSE 99
                    END, RoleName", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        roles.Add(new RoleDto
                        {
                            RoleId = Convert.ToInt32(r["RoleId"]),
                            RoleName = Convert.ToString(r["RoleName"]),
                            Description = r["Description"] == DBNull.Value ? null : Convert.ToString(r["Description"])
                        });
                    }
                }
            }
            return roles;
        }

        public List<ManagedUserDto> GetUsers()
        {
            var users = new List<ManagedUserDto>();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT u.UserId, u.Username, u.DisplayName, u.RoleId, r.RoleName, u.IsActive,
                           u.ForcePasswordChange, u.LastLoginDate, u.CreatedDate
                    FROM AppUsers u
                    INNER JOIN AppRoles r ON u.RoleId = r.RoleId
                    ORDER BY u.DisplayName, u.Username";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        users.Add(new ManagedUserDto
                        {
                            UserId = Convert.ToInt32(r["UserId"]),
                            Username = Convert.ToString(r["Username"]),
                            DisplayName = Convert.ToString(r["DisplayName"]),
                            RoleId = Convert.ToInt32(r["RoleId"]),
                            RoleName = Convert.ToString(r["RoleName"]),
                            IsActive = r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"]),
                            ForcePasswordChange = r["ForcePasswordChange"] != DBNull.Value && Convert.ToBoolean(r["ForcePasswordChange"]),
                            LastLoginDate = r["LastLoginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LastLoginDate"]),
                            CreatedDate = r["CreatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["CreatedDate"])
                        });
                    }
                }
            }
            return users;
        }

        public int CreateUser(string username, string displayName, int roleId, string passwordHash, string passwordSalt, bool isActive, bool forcePasswordChange)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO AppUsers
                        (Username, DisplayName, PasswordHash, PasswordSalt, RoleId, IsActive, ForcePasswordChange)
                    VALUES
                        (@username, @displayName, @passwordHash, @passwordSalt, @roleId, @isActive, @forcePasswordChange);
                    SELECT SCOPE_IDENTITY();";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username ?? string.Empty);
                    cmd.Parameters.AddWithValue("@displayName", displayName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@passwordHash", passwordHash ?? string.Empty);
                    cmd.Parameters.AddWithValue("@passwordSalt", passwordSalt ?? string.Empty);
                    cmd.Parameters.AddWithValue("@roleId", roleId);
                    cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@forcePasswordChange", forcePasswordChange ? 1 : 0);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void UpdateUser(int userId, string username, string displayName, int roleId, bool isActive)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    UPDATE AppUsers
                    SET Username = @username,
                        DisplayName = @displayName,
                        RoleId = @roleId,
                        IsActive = @isActive
                    WHERE UserId = @id";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.Parameters.AddWithValue("@username", username ?? string.Empty);
                    cmd.Parameters.AddWithValue("@displayName", displayName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@roleId", roleId);
                    cmd.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SetUserActive(int userId, bool isActive)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE AppUsers SET IsActive = @active WHERE UserId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void InsertAuditLog(int? userId, string username, string action, string moduleKey, int? recordId, string description, string ipAddress)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO AuditLog (UserId, Username, Action, ModuleKey, RecordId, Description, IPAddress)
                    VALUES (@userId, @username, @action, @moduleKey, @recordId, @description, @ipAddress);";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@username", string.IsNullOrWhiteSpace(username) ? (object)DBNull.Value : username.Trim());
                    cmd.Parameters.AddWithValue("@action", action ?? string.Empty);
                    cmd.Parameters.AddWithValue("@moduleKey", string.IsNullOrWhiteSpace(moduleKey) ? (object)DBNull.Value : moduleKey.Trim());
                    cmd.Parameters.AddWithValue("@recordId", recordId.HasValue ? (object)recordId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@description", string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description.Trim());
                    cmd.Parameters.AddWithValue("@ipAddress", string.IsNullOrWhiteSpace(ipAddress) ? (object)DBNull.Value : ipAddress.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void InsertLoginAudit(int? userId, string username, bool success, string failureReason, string ipAddress, string deviceName)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO LoginAudit (UserId, Username, Success, FailureReason, IPAddress, DeviceName)
                    VALUES (@userId, @username, @success, @failureReason, @ipAddress, @deviceName);";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@username", string.IsNullOrWhiteSpace(username) ? (object)DBNull.Value : username.Trim());
                    cmd.Parameters.AddWithValue("@success", success ? 1 : 0);
                    cmd.Parameters.AddWithValue("@failureReason", string.IsNullOrWhiteSpace(failureReason) ? (object)DBNull.Value : failureReason.Trim());
                    cmd.Parameters.AddWithValue("@ipAddress", string.IsNullOrWhiteSpace(ipAddress) ? (object)DBNull.Value : ipAddress.Trim());
                    cmd.Parameters.AddWithValue("@deviceName", string.IsNullOrWhiteSpace(deviceName) ? (object)DBNull.Value : deviceName.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public Guid CreateSession(int userId, string tokenHash, string refreshTokenHash, DateTime expiresAt, string ipAddress, string deviceName)
        {
            Guid sessionId = Guid.NewGuid();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    INSERT INTO UserSessions
                        (SessionId, UserId, TokenHash, RefreshTokenHash, DeviceName, IPAddress, ExpiresAt, LastSeenAt)
                    VALUES
                        (@sessionId, @userId, @tokenHash, @refreshTokenHash, @deviceName, @ipAddress, @expiresAt, GETDATE());";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@tokenHash", tokenHash ?? string.Empty);
                    cmd.Parameters.AddWithValue("@refreshTokenHash", string.IsNullOrWhiteSpace(refreshTokenHash) ? (object)DBNull.Value : refreshTokenHash);
                    cmd.Parameters.AddWithValue("@deviceName", string.IsNullOrWhiteSpace(deviceName) ? (object)DBNull.Value : deviceName.Trim());
                    cmd.Parameters.AddWithValue("@ipAddress", string.IsNullOrWhiteSpace(ipAddress) ? (object)DBNull.Value : ipAddress.Trim());
                    cmd.Parameters.AddWithValue("@expiresAt", expiresAt);
                    cmd.ExecuteNonQuery();
                }
            }
            return sessionId;
        }

        public AppUserDto GetUserByValidSession(Guid sessionId, string tokenHash)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT u.UserId, u.Username, u.DisplayName, u.RoleId, r.RoleName, u.IsActive,
                           u.LastLoginDate, u.ForcePasswordChange, u.FailedAttempts, u.LockoutUntil
                    FROM UserSessions s
                    INNER JOIN AppUsers u ON s.UserId = u.UserId
                    INNER JOIN AppRoles r ON u.RoleId = r.RoleId
                    WHERE s.SessionId = @sessionId
                      AND s.TokenHash = @tokenHash
                      AND s.RevokedAt IS NULL
                      AND s.ExpiresAt > GETDATE()
                      AND u.IsActive = 1";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@tokenHash", tokenHash ?? string.Empty);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        return new AppUserDto
                        {
                            UserId = Convert.ToInt32(r["UserId"]),
                            Username = Convert.ToString(r["Username"]),
                            DisplayName = Convert.ToString(r["DisplayName"]),
                            RoleId = Convert.ToInt32(r["RoleId"]),
                            RoleName = Convert.ToString(r["RoleName"]),
                            IsActive = r["IsActive"] == DBNull.Value || Convert.ToBoolean(r["IsActive"]),
                            LastLoginDate = r["LastLoginDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LastLoginDate"]),
                            ForcePasswordChange = r["ForcePasswordChange"] != DBNull.Value && Convert.ToBoolean(r["ForcePasswordChange"]),
                            FailedAttempts = r["FailedAttempts"] == DBNull.Value ? 0 : Convert.ToInt32(r["FailedAttempts"]),
                            LockoutUntil = r["LockoutUntil"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["LockoutUntil"]),
                            Permissions = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase)
                        };
                    }
                }
            }
        }

        public void TouchSession(Guid sessionId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE UserSessions SET LastSeenAt = GETDATE() WHERE SessionId = @id AND RevokedAt IS NULL", conn))
                {
                    cmd.Parameters.AddWithValue("@id", sessionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RevokeSession(Guid sessionId)
        {
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE UserSessions SET RevokedAt = GETDATE() WHERE SessionId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", sessionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public DataTable GetAuditLog(DateTime fromDate, DateTime toDate, string username)
        {
            var table = new DataTable();
            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                const string sql = @"
                    SELECT LogDate, Username, Action, ModuleKey, Description
                    FROM AuditLog
                    WHERE LogDate >= @fromDate
                      AND LogDate < DATEADD(day, 1, @toDate)
                      AND (@username = '' OR Username = @username)
                    ORDER BY LogDate DESC, LogId DESC";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@fromDate", fromDate.Date);
                    cmd.Parameters.AddWithValue("@toDate", toDate.Date);
                    cmd.Parameters.AddWithValue("@username", username ?? string.Empty);
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        da.Fill(table);
                }
            }
            return table;
        }
    }
}
