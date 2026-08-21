using System;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services.Audit;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Central policy for high-risk actions. Service-layer callers must enforce its result.</summary>
    public static class GuardrailService
    {
        private static readonly AuditTrailService Audit = new AuditTrailService();

        public static void RequireManagerOverride(string moduleKey, int? recordId, string action, string reason)
        {
            AppUserDto user = SessionManager.CurrentUser;
            bool authorized = string.Equals(user?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(user?.RoleName, "Manager", StringComparison.OrdinalIgnoreCase);
            if (!authorized)
                throw new UnauthorizedAccessException("Only a Manager or Admin may override this safeguard.");
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8)
                throw new InvalidOperationException("Enter a clear override reason (at least 8 characters).");

            Audit.Record("OVERRIDE", moduleKey, recordId, (action ?? "Guardrail override") + ". Reason: " + reason.Trim());
        }

        public static void Block(string moduleKey, int? recordId, string message)
        {
            Audit.Record("BLOCK", moduleKey, recordId, message);
            throw new InvalidOperationException(message);
        }
    }
}
