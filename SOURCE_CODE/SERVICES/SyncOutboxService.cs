using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class SyncOutboxService
    {
        public void Emit(string entityType, Guid entitySyncPublicId, string operation, object payload)
        {
            string json = JsonConvert.SerializeObject(payload);
            string idempotencyKey = BuildIdempotencyKey(entityType, entitySyncPublicId, operation, json);

            using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection())
            using (SqlCommand cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.SyncOutbox WHERE IdempotencyKey = @idempotencyKey)
BEGIN
    INSERT INTO dbo.SyncOutbox
        (EntityType, EntitySyncPublicId, Operation, PayloadJson, SourceNodeId, OccurredUtc, Status, IdempotencyKey)
    VALUES
        (@entityType, @entitySyncPublicId, @operation, @payloadJson, @sourceNodeId, GETUTCDATE(), @status, @idempotencyKey);
END", conn))
            {
                DatabaseConnectionFactory.Open(conn, "SyncOutboxService.Emit");
                cmd.Parameters.AddWithValue("@entityType", entityType ?? string.Empty);
                cmd.Parameters.AddWithValue("@entitySyncPublicId", entitySyncPublicId);
                cmd.Parameters.AddWithValue("@operation", operation ?? string.Empty);
                cmd.Parameters.AddWithValue("@payloadJson", json ?? string.Empty);
                cmd.Parameters.AddWithValue("@sourceNodeId", NodeIdentityService.GetOrCreateNodePublicId());
                cmd.Parameters.AddWithValue("@status", "Pending");
                cmd.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
                cmd.ExecuteNonQuery();
            }
        }

        public Dictionary<string, int> GetOutboxStatusCounts()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection())
                using (SqlCommand cmd = new SqlCommand(@"
SELECT ISNULL(Status, 'Unknown') AS StatusName, COUNT(1) AS RowCount
FROM dbo.SyncOutbox
GROUP BY ISNULL(Status, 'Unknown');", conn))
                {
                    DatabaseConnectionFactory.Open(conn, "SyncOutboxService.GetOutboxStatusCounts");
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            counts[Convert.ToString(reader["StatusName"])] = Convert.ToInt32(reader["RowCount"]);
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SyncOutboxService.GetOutboxStatusCounts", ex);
            }

            return counts;
        }

        public static string BuildIdempotencyKey(string entityType, Guid entitySyncPublicId, string operation, string payloadJson)
        {
            string payloadHash;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payloadJson ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                payloadHash = BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16);
            }

            return (entityType ?? "Entity") + "|" + entitySyncPublicId.ToString("N") + "|" + (operation ?? "Upsert") + "|" + payloadHash;
        }
    }
}
