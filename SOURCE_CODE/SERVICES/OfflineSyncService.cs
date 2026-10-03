using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class OfflineQueueResult
    {
        public long QueueId { get; set; }
        public int LocalId { get; set; }
        public string Message { get; set; }
    }

    public sealed class OfflineSyncItem
    {
        public long QueueId { get; set; }
        public string Module { get; set; }
        public string Operation { get; set; }
        public string LocalReference { get; set; }
        public string PayloadJson { get; set; }
        public string Status { get; set; }
        public int Attempts { get; set; }
        public string LastError { get; set; }
        public bool RequiresReview { get; set; }
        public string NodePublicId { get; set; }
        public string EntitySyncPublicId { get; set; }
        public string IdempotencyKey { get; set; }
        public int? ServerRecordId { get; set; }
        public long BaseSyncVersion { get; set; }
    }

    public sealed class OfflineSyncConflictException : InvalidOperationException
    {
        public OfflineSyncConflictException(string message) : base(message) { }
    }

    public static class OfflineSyncService
    {
        private const string StatusPending = "Pending";
        private const string StatusSynced = "Synced";
        private const string StatusFailed = "Failed";
        private const string StatusConflict = "Conflict";
        private static readonly object Sync = new object();

        public static event EventHandler PendingChanged;

        public static bool IsReplaying { get; private set; }

        public static void EnsureReady()
        {
            if (!LocalSqliteFallbackStore.IsOfflineQueueEnabled)
                return;

            LocalSqliteFallbackStore.EnsureReady();
            lock (Sync)
            {
                using (SQLiteConnection conn = OpenConnection())
                {
                    Execute(conn, @"
CREATE TABLE IF NOT EXISTS OfflineSyncQueue (
    QueueId INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL,
    MachineName TEXT NOT NULL,
    NodePublicId TEXT NULL,
    Module TEXT NOT NULL,
    Operation TEXT NOT NULL,
    LocalReference TEXT NOT NULL,
    ServerRecordId INTEGER NULL,
    EntitySyncPublicId TEXT NULL,
    IdempotencyKey TEXT NULL,
    BaseSyncVersion INTEGER NOT NULL DEFAULT 0,
    PayloadJson TEXT NOT NULL,
    Status TEXT NOT NULL,
    Attempts INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL,
    RequiresReview INTEGER NOT NULL DEFAULT 0,
    SyncedUtc TEXT NULL
);");
                    EnsureColumn(conn, "OfflineSyncQueue", "NodePublicId", "TEXT NULL");
                    EnsureColumn(conn, "OfflineSyncQueue", "EntitySyncPublicId", "TEXT NULL");
                    EnsureColumn(conn, "OfflineSyncQueue", "IdempotencyKey", "TEXT NULL");
                    EnsureColumn(conn, "OfflineSyncQueue", "BaseSyncVersion", "INTEGER NOT NULL DEFAULT 0");
                    Execute(conn, "CREATE INDEX IF NOT EXISTS IX_OfflineSyncQueue_Status ON OfflineSyncQueue(Status, QueueId);");
                    Execute(conn, "CREATE INDEX IF NOT EXISTS IX_OfflineSyncQueue_Module ON OfflineSyncQueue(Module, Operation);");
                }
            }
        }

        public static OfflineQueueResult Queue<T>(string module, string operation, T payload, int? serverRecordId, bool requiresReview, string reason)
        {
            return Queue(module, operation, payload, serverRecordId, requiresReview, reason, null);
        }

        public static OfflineQueueResult Queue<T>(string module, string operation, T payload, int? serverRecordId, bool requiresReview, string reason, Guid? entitySyncPublicId)
        {
            if (!LocalSqliteFallbackStore.IsOfflineQueueEnabled)
                throw new InvalidOperationException("Offline saving is not enabled for this ServoERP installation. Connect to SQL Server before saving business records.");

            if (!IsOfflineSafeModule(module, operation))
                throw new InvalidOperationException("Offline saving is available only for Clients, Sites, and Jobs. Financial, stock, and payroll entries require the SQL Server connection.");

            EnsureReady();
            string localReference = BuildLocalReference(module, operation);
            string payloadJson = JsonConvert.SerializeObject(payload);
            string nodePublicId = NodeIdentityService.GetOrCreateNodePublicId().ToString("D");
            string entitySyncPublicIdText = entitySyncPublicId.HasValue && entitySyncPublicId.Value != Guid.Empty ? entitySyncPublicId.Value.ToString("D") : string.Empty;
            string idempotencyKey = SyncOutboxService.BuildIdempotencyKey(module, entitySyncPublicId ?? Guid.Empty, operation, payloadJson);
            long baseSyncVersion = GetSyncVersion(payload);
            long queueId;
            lock (Sync)
            {
                using (SQLiteConnection conn = OpenConnection())
                {
                    long existingQueueId = TryCoalescePending(conn, module, operation, entitySyncPublicIdText, payloadJson, idempotencyKey, reason);
                    if (existingQueueId > 0)
                    {
                        RaisePendingChanged();
                        return new OfflineQueueResult
                        {
                            QueueId = existingQueueId,
                            LocalId = BuildLocalId(existingQueueId),
                            Message = "Saved locally. Pending sync #" + existingQueueId.ToString("0000") + "."
                        };
                    }

                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO OfflineSyncQueue
    (CreatedUtc, UpdatedUtc, MachineName, NodePublicId, Module, Operation, LocalReference, ServerRecordId, EntitySyncPublicId, IdempotencyKey, BaseSyncVersion, PayloadJson, Status, Attempts, LastError, RequiresReview)
VALUES
    (@created, @updated, @machine, @nodePublicId, @module, @operation, @localRef, @serverId, @entitySyncPublicId, @idempotencyKey, @baseSyncVersion, @payload, @status, 0, @reason, @review);
SELECT last_insert_rowid();", conn))
                {
                    string now = DateTime.UtcNow.ToString("o");
                    cmd.Parameters.AddWithValue("@created", now);
                    cmd.Parameters.AddWithValue("@updated", now);
                    cmd.Parameters.AddWithValue("@machine", Environment.MachineName);
                    cmd.Parameters.AddWithValue("@nodePublicId", nodePublicId);
                    cmd.Parameters.AddWithValue("@module", module ?? string.Empty);
                    cmd.Parameters.AddWithValue("@operation", operation ?? string.Empty);
                    cmd.Parameters.AddWithValue("@localRef", localReference);
                    cmd.Parameters.AddWithValue("@serverId", serverRecordId.HasValue ? (object)serverRecordId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@entitySyncPublicId", string.IsNullOrWhiteSpace(entitySyncPublicIdText) ? (object)DBNull.Value : entitySyncPublicIdText);
                    cmd.Parameters.AddWithValue("@idempotencyKey", string.IsNullOrWhiteSpace(idempotencyKey) ? (object)DBNull.Value : idempotencyKey);
                    cmd.Parameters.AddWithValue("@baseSyncVersion", baseSyncVersion);
                    cmd.Parameters.AddWithValue("@payload", payloadJson ?? string.Empty);
                    cmd.Parameters.AddWithValue("@status", StatusPending);
                    cmd.Parameters.AddWithValue("@reason", reason ?? string.Empty);
                    cmd.Parameters.AddWithValue("@review", requiresReview ? 1 : 0);
                    queueId = Convert.ToInt64(cmd.ExecuteScalar());
                }
                }
            }

            LocalSqliteFallbackStore.RecordEvent("OFFLINE_QUEUED", module + " " + operation + " " + localReference);
            RaisePendingChanged();
            return new OfflineQueueResult { QueueId = queueId, LocalId = BuildLocalId(queueId), Message = "Saved locally. Pending sync #" + queueId.ToString("0000") + "." };
        }

        public static int GetPendingCount()
        {
            if (!LocalSqliteFallbackStore.IsOfflineQueueEnabled)
                return 0;

            EnsureReady();
            using (SQLiteConnection conn = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(1) FROM OfflineSyncQueue WHERE Status IN ('Pending','Failed','Conflict');", conn))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static List<OfflineSyncItem> GetPendingItems(int max = 100)
        {
            if (!LocalSqliteFallbackStore.IsOfflineQueueEnabled)
                return new List<OfflineSyncItem>();

            EnsureReady();
            var items = new List<OfflineSyncItem>();
            using (SQLiteConnection conn = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT QueueId, Module, Operation, LocalReference, PayloadJson, Status, Attempts, LastError, RequiresReview, NodePublicId, EntitySyncPublicId, IdempotencyKey, ServerRecordId, BaseSyncVersion
FROM OfflineSyncQueue WHERE Status IN ('Pending','Failed','Conflict') ORDER BY QueueId LIMIT @max;", conn))
            {
                cmd.Parameters.AddWithValue("@max", Math.Max(1, max));
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                    while (reader.Read()) items.Add(MapItem(reader));
            }
            return items;
        }

        public static int TryReplayPending()
        {
            if (!LocalSqliteFallbackStore.IsOfflineQueueEnabled)
                return 0;

            if (IsReplaying)
                return 0;
            DatabaseConnectionStateSnapshot state = DatabaseConnectionStateService.CheckNow("OfflineSyncService.TryReplayPending", false);
            if (!state.BusinessWritesAllowed)
                return 0;
            int synced = 0;
            IsReplaying = true;
            try
            {
                foreach (OfflineSyncItem item in GetReplayItems(100))
                {
                    try
                    {
                        int? serverRecordId = ReplayItem(item);
                        MarkSynced(item.QueueId, serverRecordId);
                        synced++;
                    }
                    catch (Exception ex) { MarkFailed(item.QueueId, ex); }
                }
            }
            finally { IsReplaying = false; }
            if (synced > 0) { LocalSqliteFallbackStore.RecordEvent("OFFLINE_SYNCED", synced + " pending operation(s) synced."); RaisePendingChanged(); }
            return synced;
        }

        public static bool ShouldQueue(Exception ex)
        {
            return LocalSqliteFallbackStore.IsOfflineQueueEnabled && !IsReplaying && ex != null && IsSqlConnectivityFailure(ex);
        }

        public static bool IsSupportedOperation(string module, string operation)
        {
            return IsOfflineSafeModule(module, operation);
        }

        public static bool HasVersionConflict(long currentVersion, long baseSyncVersion)
        {
            return currentVersion != baseSyncVersion;
        }

        private static bool IsSqlConnectivityFailure(Exception ex)
        {
            if (ex == null)
                return false;

            if (ex is System.Data.SqlClient.SqlException || ex is DatabaseBusinessWriteUnavailableException)
                return true;

            string message = ex.Message ?? string.Empty;
            return message.IndexOf("SQL Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("database", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int? ReplayItem(OfflineSyncItem item)
        {
            string module = (item.Module ?? string.Empty).Trim();
            string operation = (item.Operation ?? string.Empty).Trim();

            if (module == "Clients" && operation == "Create")
            {
                B2BClient payload = JsonConvert.DeserializeObject<B2BClient>(item.PayloadJson);
                B2BClient existing = payload != null && payload.SyncPublicId.HasValue
                    ? new ClientRepository().GetBySyncPublicId(payload.SyncPublicId.Value)
                    : null;
                return existing != null ? existing.ClientID : new ClientService().CreateClient(payload);
            }
            if (module == "Clients" && operation == "Update")
            {
                B2BClient payload = JsonConvert.DeserializeObject<B2BClient>(item.PayloadJson);
                B2BClient existing = RequireClient(payload, item.BaseSyncVersion);
                payload.ClientID = existing.ClientID;
                new ClientService().UpdateClient(payload);
                return existing.ClientID;
            }
            if (module == "Jobs" && operation == "Create")
            {
                Job payload = JsonConvert.DeserializeObject<Job>(item.PayloadJson);
                Job existing = payload != null && payload.SyncPublicId.HasValue
                    ? new JobRepository().GetBySyncPublicId(payload.SyncPublicId.Value)
                    : null;
                if (existing != null)
                    return existing.JobID;
                ResolveJobDependencies(payload);
                return new JobService().Create(payload);
            }
            if (module == "Sites" && operation == "Create")
            {
                ClientSite payload = JsonConvert.DeserializeObject<ClientSite>(item.PayloadJson);
                ClientSite existing = payload != null && payload.SyncPublicId.HasValue
                    ? new SiteRepository().GetBySyncPublicId(payload.SyncPublicId.Value)
                    : null;
                if (existing != null)
                    return existing.SiteID;
                payload.ClientID = ResolveServerRecordId(payload.ClientID, "client");
                return new SiteService().Create(payload);
            }
            if (module == "Sites" && operation == "Update")
            {
                ClientSite payload = JsonConvert.DeserializeObject<ClientSite>(item.PayloadJson);
                ClientSite existing = RequireSite(payload, item.BaseSyncVersion);
                payload.SiteID = existing.SiteID;
                payload.ClientID = ResolveServerRecordId(payload.ClientID, "client");
                new SiteService().Update(payload);
                return existing.SiteID;
            }
            if (module == "Jobs" && operation == "Update")
            {
                Job payload = JsonConvert.DeserializeObject<Job>(item.PayloadJson);
                Job existing = RequireJob(payload, item.BaseSyncVersion);
                payload.JobID = existing.JobID;
                ResolveJobDependencies(payload);
                new JobService().Update(payload);
                return existing.JobID;
            }
            throw new NotSupportedException("Offline sync handler missing for " + module + "." + operation);
        }

        private static bool IsOfflineSafeModule(string module, string operation)
        {
            string normalizedModule = (module ?? string.Empty).Trim();
            string normalizedOperation = (operation ?? string.Empty).Trim();
            return (normalizedModule == "Clients" && (normalizedOperation == "Create" || normalizedOperation == "Update"))
                || (normalizedModule == "Sites" && (normalizedOperation == "Create" || normalizedOperation == "Update"))
                || (normalizedModule == "Jobs" && (normalizedOperation == "Create" || normalizedOperation == "Update"));
        }

        private static void MarkSynced(long queueId, int? serverRecordId)
        {
            UpdateQueue(queueId, StatusSynced, null, false, true, serverRecordId);
        }

        private static void MarkFailed(long queueId, Exception ex)
        {
            bool conflict = !IsSqlConnectivityFailure(ex) || ex is InvalidOperationException || ex is ArgumentException;
            UpdateQueue(queueId, conflict ? StatusConflict : StatusFailed, SensitiveDataRedactor.Redact(ex.Message), conflict, false, null);
        }

        private static void UpdateQueue(long queueId, string status, string error, bool requiresReview, bool synced, int? serverRecordId)
        {
            lock (Sync)
            {
                using (SQLiteConnection conn = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
UPDATE OfflineSyncQueue
SET UpdatedUtc=@updated,
    Status=@status,
    Attempts=Attempts + 1,
    LastError=@error,
    RequiresReview=@review,
    SyncedUtc=@synced,
    ServerRecordId=COALESCE(@serverRecordId, ServerRecordId)
WHERE QueueId=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@error", string.IsNullOrWhiteSpace(error) ? (object)DBNull.Value : error);
                    cmd.Parameters.AddWithValue("@review", requiresReview ? 1 : 0);
                    cmd.Parameters.AddWithValue("@synced", synced ? (object)DateTime.UtcNow.ToString("o") : DBNull.Value);
                    cmd.Parameters.AddWithValue("@serverRecordId", serverRecordId.HasValue ? (object)serverRecordId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", queueId);
                    cmd.ExecuteNonQuery();
                }
            }

            RaisePendingChanged();
        }

        private static List<OfflineSyncItem> GetReplayItems(int max)
        {
            EnsureReady();
            var items = new List<OfflineSyncItem>();
            using (SQLiteConnection conn = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT QueueId, Module, Operation, LocalReference, PayloadJson, Status, Attempts, LastError, RequiresReview, NodePublicId, EntitySyncPublicId, IdempotencyKey, ServerRecordId, BaseSyncVersion
FROM OfflineSyncQueue
WHERE Status IN ('Pending','Failed')
ORDER BY QueueId
LIMIT @max;", conn))
            {
                cmd.Parameters.AddWithValue("@max", Math.Max(1, max));
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                    while (reader.Read())
                        items.Add(MapItem(reader));
            }
            return items;
        }

        private static OfflineSyncItem MapItem(SQLiteDataReader reader)
        {
            return new OfflineSyncItem
            {
                QueueId = reader.GetInt64(0),
                Module = Read(reader, 1),
                Operation = Read(reader, 2),
                LocalReference = Read(reader, 3),
                PayloadJson = Read(reader, 4),
                Status = Read(reader, 5),
                Attempts = reader.GetInt32(6),
                LastError = Read(reader, 7),
                RequiresReview = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                NodePublicId = Read(reader, 9),
                EntitySyncPublicId = Read(reader, 10),
                IdempotencyKey = Read(reader, 11),
                ServerRecordId = reader.IsDBNull(12) ? (int?)null : Convert.ToInt32(reader.GetValue(12)),
                BaseSyncVersion = reader.IsDBNull(13) ? 0L : Convert.ToInt64(reader.GetValue(13))
            };
        }

        private static long TryCoalescePending(SQLiteConnection conn, string module, string operation, string entitySyncPublicId, string payloadJson, string idempotencyKey, string reason)
        {
            if (string.IsNullOrWhiteSpace(entitySyncPublicId))
                return 0;

            using (SQLiteCommand find = new SQLiteCommand(@"
SELECT QueueId, Operation
FROM OfflineSyncQueue
WHERE Module=@module
  AND EntitySyncPublicId=@entitySyncPublicId
  AND Status IN ('Pending','Failed')
  AND (Operation=@operation OR (@operation='Update' AND Operation='Create'))
ORDER BY CASE WHEN Operation='Create' THEN 0 ELSE 1 END, QueueId
LIMIT 1;", conn))
            {
                find.Parameters.AddWithValue("@module", module ?? string.Empty);
                find.Parameters.AddWithValue("@operation", operation ?? string.Empty);
                find.Parameters.AddWithValue("@entitySyncPublicId", entitySyncPublicId);
                using (SQLiteDataReader reader = find.ExecuteReader())
                {
                    if (!reader.Read())
                        return 0;

                    long queueId = reader.GetInt64(0);
                    reader.Close();
                    using (SQLiteCommand update = new SQLiteCommand(@"
UPDATE OfflineSyncQueue
SET UpdatedUtc=@updated,
    PayloadJson=@payload,
    IdempotencyKey=@idempotencyKey,
    Status='Pending',
    LastError=@reason,
    RequiresReview=0
WHERE QueueId=@id;", conn))
                    {
                        update.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
                        update.Parameters.AddWithValue("@payload", payloadJson ?? string.Empty);
                        update.Parameters.AddWithValue("@idempotencyKey", idempotencyKey ?? string.Empty);
                        update.Parameters.AddWithValue("@reason", reason ?? string.Empty);
                        update.Parameters.AddWithValue("@id", queueId);
                        update.ExecuteNonQuery();
                    }
                    return queueId;
                }
            }
        }

        private static B2BClient RequireClient(B2BClient payload, long baseSyncVersion)
        {
            if (payload == null || !payload.SyncPublicId.HasValue)
                throw new OfflineSyncConflictException("Offline client update is missing its stable record identity.");
            B2BClient existing = new ClientRepository().GetBySyncPublicId(payload.SyncPublicId.Value);
            EnsureUnchanged("client", existing == null ? (long?)null : existing.SyncVersion, baseSyncVersion);
            return existing;
        }

        private static ClientSite RequireSite(ClientSite payload, long baseSyncVersion)
        {
            if (payload == null || !payload.SyncPublicId.HasValue)
                throw new OfflineSyncConflictException("Offline site update is missing its stable record identity.");
            ClientSite existing = new SiteRepository().GetBySyncPublicId(payload.SyncPublicId.Value);
            EnsureUnchanged("site", existing == null ? (long?)null : existing.SyncVersion, baseSyncVersion);
            return existing;
        }

        private static Job RequireJob(Job payload, long baseSyncVersion)
        {
            if (payload == null || !payload.SyncPublicId.HasValue)
                throw new OfflineSyncConflictException("Offline job update is missing its stable record identity.");
            Job existing = new JobRepository().GetBySyncPublicId(payload.SyncPublicId.Value);
            EnsureUnchanged("job", existing == null ? (long?)null : existing.SyncVersion, baseSyncVersion);
            return existing;
        }

        private static void EnsureUnchanged(string recordType, long? currentVersion, long baseSyncVersion)
        {
            if (!currentVersion.HasValue)
                throw new OfflineSyncConflictException("The " + recordType + " no longer exists on the office database. Review is required.");
            if (HasVersionConflict(currentVersion.Value, baseSyncVersion))
            {
                throw new OfflineSyncConflictException(
                    "The " + recordType + " changed on another PC while this PC was offline. " +
                    "The offline copy was retained for review and did not overwrite the newer record.");
            }
        }

        private static void ResolveJobDependencies(Job payload)
        {
            if (payload == null)
                throw new OfflineSyncConflictException("Offline job data could not be read.");
            payload.ClientID = ResolveServerRecordId(payload.ClientID, "client");
            if (payload.SiteID != 0)
                payload.SiteID = ResolveServerRecordId(payload.SiteID, "site");
        }

        private static int ResolveServerRecordId(int recordId, string relationship)
        {
            if (recordId >= 0)
                return recordId;

            long queueId = Math.Abs((long)recordId);
            using (SQLiteConnection conn = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT ServerRecordId
FROM OfflineSyncQueue
WHERE QueueId=@queueId AND Status='Synced';", conn))
            {
                cmd.Parameters.AddWithValue("@queueId", queueId);
                object value = cmd.ExecuteScalar();
                if (value != null && value != DBNull.Value)
                    return Convert.ToInt32(value);
            }

            throw new InvalidOperationException("The offline " + relationship + " must sync before this related record can be saved.");
        }

        private static long GetSyncVersion<T>(T payload)
        {
            if (payload == null)
                return 0L;
            try
            {
                var property = payload.GetType().GetProperty("SyncVersion");
                if (property == null)
                    return 0L;
                object value = property.GetValue(payload, null);
                return value == null ? 0L : Convert.ToInt64(value);
            }
            catch
            {
                return 0L;
            }
        }

        private static int BuildLocalId(long queueId)
        {
            long local = -Math.Abs(queueId);
            if (local < int.MinValue)
                return int.MinValue + 1;
            return (int)local;
        }

        private static string BuildLocalReference(string module, string operation)
        {
            return "LOCAL-" + (module ?? "Record").ToUpperInvariant() + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        private static SQLiteConnection OpenConnection()
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder
            {
                DataSource = LocalSqliteFallbackStore.GetDatabasePath(),
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal
            };
            SQLiteConnection conn = new SQLiteConnection(builder.ConnectionString);
            conn.Open();
            return conn;
        }

        private static void Execute(SQLiteConnection conn, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        private static void EnsureColumn(SQLiteConnection conn, string tableName, string columnName, string definition)
        {
            if (HasColumn(conn, tableName, columnName))
                return;

            Execute(conn, "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + definition + ";");
        }

        private static bool HasColumn(SQLiteConnection conn, string tableName, string columnName)
        {
            using (SQLiteCommand cmd = new SQLiteCommand("PRAGMA table_info(" + tableName + ");", conn))
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(Convert.ToString(reader["name"]), columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static string Read(SQLiteDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader.GetValue(index));
        }

        private static void RaisePendingChanged()
        {
            EventHandler handler = PendingChanged;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

    }
}
