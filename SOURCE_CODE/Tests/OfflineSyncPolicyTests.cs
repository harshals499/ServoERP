using System;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>Guards the approved phase-one offline boundary and conflict policy.</summary>
    public static class OfflineSyncPolicyTests
    {
        public static string RunAll()
        {
            if (!OfflineSyncService.IsSupportedOperation("Clients", "Create") ||
                !OfflineSyncService.IsSupportedOperation("Clients", "Update") ||
                !OfflineSyncService.IsSupportedOperation("Sites", "Create") ||
                !OfflineSyncService.IsSupportedOperation("Sites", "Update") ||
                !OfflineSyncService.IsSupportedOperation("Jobs", "Create") ||
                !OfflineSyncService.IsSupportedOperation("Jobs", "Update"))
                throw new InvalidOperationException("Approved phase-one records must support offline create and update.");

            if (OfflineSyncService.IsSupportedOperation("Jobs", "AddPart") ||
                OfflineSyncService.IsSupportedOperation("Invoices", "CreateDraft") ||
                OfflineSyncService.IsSupportedOperation("Payments", "RecordDraft") ||
                OfflineSyncService.IsSupportedOperation("Payroll", "Save"))
                throw new InvalidOperationException("Financial, inventory, and payroll operations must remain online-only.");

            if (!OfflineSyncService.HasVersionConflict(9, 8) || OfflineSyncService.HasVersionConflict(8, 8))
                throw new InvalidOperationException("Offline replay must retain stale updates as conflicts.");

            if (OneDriveStorageService.IsPathInsideOneDrive(LocalSqliteFallbackStore.GetDatabasePath()))
                throw new InvalidOperationException("The machine-local offline database must never be stored inside OneDrive.");

            return "Offline sync scope and conflict policy verified";
        }
    }
}
