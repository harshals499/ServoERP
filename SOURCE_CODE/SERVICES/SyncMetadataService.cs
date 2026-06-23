using System;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class SyncMetadataService
    {
        public void EnsureClientIdentity(B2BClient client)
        {
            if (client == null)
                return;

            if (!client.SyncPublicId.HasValue || client.SyncPublicId.Value == Guid.Empty)
                client.SyncPublicId = Guid.NewGuid();
        }

        public void EnsureSiteIdentity(ClientSite site)
        {
            if (site == null)
                return;

            if (!site.SyncPublicId.HasValue || site.SyncPublicId.Value == Guid.Empty)
                site.SyncPublicId = Guid.NewGuid();
        }

        public void EnsureJobIdentity(Job job)
        {
            if (job == null)
                return;

            if (!job.SyncPublicId.HasValue || job.SyncPublicId.Value == Guid.Empty)
                job.SyncPublicId = Guid.NewGuid();
        }

        public void TouchClient(int clientId, Guid syncPublicId)
        {
            TouchEntity("B2BClients", "ClientID", clientId, syncPublicId);
        }

        public void TouchSite(int siteId, Guid syncPublicId)
        {
            TouchEntity("ClientSites", "SiteID", siteId, syncPublicId);
        }

        public void TouchJob(int jobId, Guid syncPublicId)
        {
            TouchEntity("Jobs", "JobID", jobId, syncPublicId);
        }

        private static void TouchEntity(string tableName, string keyColumn, int keyValue, Guid syncPublicId)
        {
            Guid nodeId = NodeIdentityService.GetOrCreateNodePublicId();
            using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(@"
UPDATE " + tableName + @"
SET SyncPublicId = COALESCE(SyncPublicId, @syncPublicId),
    OriginNodeId = COALESCE(OriginNodeId, @nodeId),
    LastModifiedNodeId = @nodeId,
    CreatedUtc = COALESCE(CreatedUtc, GETUTCDATE()),
    UpdatedUtc = GETUTCDATE(),
    SyncVersion = ISNULL(SyncVersion, 0) + 1
WHERE " + keyColumn + @" = @id;", conn))
            {
                DatabaseConnectionFactory.Open(conn, "SyncMetadataService.TouchEntity." + tableName);
                cmd.Parameters.AddWithValue("@syncPublicId", syncPublicId);
                cmd.Parameters.AddWithValue("@nodeId", nodeId);
                cmd.Parameters.AddWithValue("@id", keyValue);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
