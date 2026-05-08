using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class RolePermissionDto
    {
        public int PermissionId { get; set; }
        public int RoleId { get; set; }
        public string ModuleKey { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class AppUserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public bool ForcePasswordChange { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public Dictionary<string, RolePermissionDto> Permissions { get; set; } = new Dictionary<string, RolePermissionDto>(StringComparer.OrdinalIgnoreCase);

    }

    public class LoginResultDto
    {
        public bool Success { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public bool IsLockedOut { get; set; }
        public string ErrorMessage { get; set; }
        public AppUserDto User { get; set; }
        public Guid? SessionId { get; set; }
        public string SessionToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class RememberedSessionDto
    {
        public Guid SessionId { get; set; }
        public string Username { get; set; }
        public string SessionToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class RoleDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }

    public class AuditLogEntryDto
    {
        public int LogId { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; }
        public string Action { get; set; }
        public string ModuleKey { get; set; }
        public int? RecordId { get; set; }
        public string Description { get; set; }
        public string IPAddress { get; set; }
        public DateTime LogDate { get; set; }
    }

    public class ManagedUserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public bool ForcePasswordChange { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
