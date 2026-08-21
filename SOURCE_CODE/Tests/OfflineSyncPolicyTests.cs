using System;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>Guards the deliberate no-offline-business-data policy until a full sync design is approved.</summary>
    public static class OfflineSyncPolicyTests
    {
        public static string RunAll()
        {
            if (LocalSqliteFallbackStore.IsOfflineQueueEnabled)
                throw new InvalidOperationException("Offline queue must remain disabled until server replay and conflict handling are approved.");
            if (OfflineSyncService.ShouldQueue(new Exception("SQL Server connection timeout")))
                throw new InvalidOperationException("SQL failures must not be queued while offline persistence is disabled.");
            if (OfflineSyncService.GetPendingCount() != 0 || OfflineSyncService.GetPendingItems().Count != 0 || OfflineSyncService.TryReplayPending() != 0)
                throw new InvalidOperationException("Disabled offline persistence must not expose queue or replay work.");

            bool rejected = false;
            try
            {
                OfflineSyncService.Queue("Clients", "Create", new { Name = "Regression only" }, null, false, "test");
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            if (!rejected)
                throw new InvalidOperationException("Disabled offline persistence accepted a business record.");

            return "Offline sync policy verified";
        }
    }
}
