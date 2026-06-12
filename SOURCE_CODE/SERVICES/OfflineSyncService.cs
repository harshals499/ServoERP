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
    Module TEXT NOT NULL,
    Operation TEXT NOT NULL,
    LocalReference TEXT NOT NULL,
    ServerRecordId INTEGER NULL,
    PayloadJson TEXT NOT NULL,
    Status TEXT NOT NULL,
    Attempts INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL,
    RequiresReview INTEGER NOT NULL DEFAULT 0,
    SyncedUtc TEXT NULL
);");
                    Execute(conn, "CREATE INDEX IF NOT EXISTS IX_OfflineSyncQueue_Status ON OfflineSyncQueue(Status, QueueId);");
                    Execute(conn, "CREATE INDEX IF NOT EXISTS IX_OfflineSyncQueue_Module ON OfflineSyncQueue(Module, Operation);");
                }
            }
        }

        public static OfflineQueueResult Queue<T>(string module, string operation, T payload, int? serverRecordId, bool requiresReview, string reason)
        {
            EnsureReady();
            string localReference = BuildLocalReference(module, operation);
            string payloadJson = JsonConvert.SerializeObject(payload);
            long queueId;
            lock (Sync)
            {
                using (SQLiteConnection conn = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO OfflineSyncQueue
    (CreatedUtc, UpdatedUtc, MachineName, Module, Operation, LocalReference, ServerRecordId, PayloadJson, Status, Attempts, LastError, RequiresReview)
VALUES
    (@created, @updated, @machine, @module, @operation, @localRef, @serverId, @payload, @status, 0, @reason, @review);
SELECT last_insert_rowid();", conn))
                {
                    string now = DateTime.UtcNow.ToString("o");
                    cmd.Parameters.AddWithValue("@created", now);
                    cmd.Parameters.AddWithValue("@updated", now);
                    cmd.Parameters.AddWithValue("@machine", Environment.MachineName);
                    cmd.Parameters.AddWithValue("@module", module ?? string.Empty);
                    cmd.Parameters.AddWithValue("@operation", operation ?? string.Empty);
                    cmd.Parameters.AddWithValue("@localRef", localReference);
                    cmd.Parameters.AddWithValue("@serverId", serverRecordId.HasValue ? (object)serverRecordId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@payload", payloadJson ?? string.Empty);
                    cmd.Parameters.AddWithValue("@status", StatusPending);
                    cmd.Parameters.AddWithValue("@reason", reason ?? string.Empty);
                    cmd.Parameters.AddWithValue("@review", requiresReview ? 1 : 0);
                    queueId = Convert.ToInt64(cmd.ExecuteScalar());
                }
            }

            LocalSqliteFallbackStore.RecordEvent("OFFLINE_QUEUED", module + " " + operation + " " + localReference);
            RaisePendingChanged();
            return new OfflineQueueResult
            {
                QueueId = queueId,
                LocalId = BuildLocalId(queueId),
                Message = "Saved locally. Pending sync #" + queueId.ToString("0000") + "."
            };
        }

        public static int GetPendingCount()
        {
            EnsureReady();
            using (SQLiteConnection conn = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand("SELECT COUNT(1) FROM OfflineSyncQueue WHERE Status IN ('Pending','Failed','Conflict');", conn))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static List<OfflineSyncItem> GetPendingItems(int max = 100)
        {
            EnsureReady();
            var items = new List<OfflineSyncItem>();
            using (SQLiteConnection conn = OpenConnection())
            using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT QueueId, Module, Operation, LocalReference, PayloadJson, Status, Attempts, LastError, RequiresReview
FROM OfflineSyncQueue
WHERE Status IN ('Pending','Failed','Conflict')
ORDER BY QueueId
LIMIT @max;", conn))
            {
                cmd.Parameters.AddWithValue("@max", Math.Max(1, max));
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new OfflineSyncItem
                        {
                            QueueId = reader.GetInt64(0),
                            Module = Read(reader, 1),
                            Operation = Read(reader, 2),
                            LocalReference = Read(reader, 3),
                            PayloadJson = Read(reader, 4),
                            Status = Read(reader, 5),
                            Attempts = reader.GetInt32(6),
                            LastError = Read(reader, 7),
                            RequiresReview = !reader.IsDBNull(8) && reader.GetInt32(8) == 1
                        });
                    }
                }
            }

            return items;
        }

        public static int TryReplayPending()
        {
            if (IsReplaying)
                return 0;

            DatabaseConnectionStateSnapshot state = DatabaseConnectionStateService.CheckNow("OfflineSyncService.TryReplayPending", false);
            if (!state.BusinessWritesAllowed)
                return 0;

            int synced = 0;
            IsReplaying = true;
            try
            {
                foreach (OfflineSyncItem item in GetPendingItems(100))
                {
                    try
                    {
                        ReplayItem(item);
                        MarkSynced(item.QueueId);
                        synced++;
                    }
                    catch (Exception ex)
                    {
                        MarkFailed(item.QueueId, ex);
                    }
                }
            }
            finally
            {
                IsReplaying = false;
            }

            if (synced > 0)
            {
                LocalSqliteFallbackStore.RecordEvent("OFFLINE_SYNCED", synced + " pending operation(s) synced.");
                RaisePendingChanged();
            }

            return synced;
        }

        public static bool ShouldQueue(Exception ex)
        {
            if (IsReplaying || ex == null)
                return false;

            return IsSqlConnectivityFailure(ex);
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

        private static void ReplayItem(OfflineSyncItem item)
        {
            string module = (item.Module ?? string.Empty).Trim();
            string operation = (item.Operation ?? string.Empty).Trim();

            if (module == "Clients" && operation == "Create")
            {
                new ClientService().CreateClient(JsonConvert.DeserializeObject<B2BClient>(item.PayloadJson));
                return;
            }
            if (module == "Clients" && operation == "Update")
            {
                new ClientService().UpdateClient(JsonConvert.DeserializeObject<B2BClient>(item.PayloadJson));
                return;
            }
            if (module == "Jobs" && operation == "Create")
            {
                new JobService().Create(JsonConvert.DeserializeObject<Job>(item.PayloadJson));
                return;
            }
            if (module == "Jobs" && operation == "Update")
            {
                new JobService().Update(JsonConvert.DeserializeObject<Job>(item.PayloadJson));
                return;
            }
            if (module == "Jobs" && operation == "AddPart")
            {
                JobPartPayload payload = JsonConvert.DeserializeObject<JobPartPayload>(item.PayloadJson);
                new JobService().AddPartUsed(payload.JobId, payload.InventoryItemId, payload.Quantity, payload.ItemDescription, payload.UnitCostOverride);
                return;
            }
            if (module == "Invoices" && operation == "CreateDraft")
            {
                Invoice invoice = JsonConvert.DeserializeObject<Invoice>(item.PayloadJson);
                invoice.PaymentStatus = string.IsNullOrWhiteSpace(invoice.PaymentStatus) ? "Draft" : invoice.PaymentStatus;
                new InvoiceService().CreateInvoiceWithLineItems(invoice);
                return;
            }
            if (module == "Invoices" && operation == "UpdateDraft")
            {
                Invoice invoice = JsonConvert.DeserializeObject<Invoice>(item.PayloadJson);
                new InvoiceService().UpdateInvoiceWithLineItems(invoice);
                return;
            }
            if (module == "Payments" && operation == "RecordDraft")
            {
                new PaymentService().RecordPayment(JsonConvert.DeserializeObject<Payment>(item.PayloadJson));
                return;
            }

            throw new NotSupportedException("Offline sync handler missing for " + module + "." + operation);
        }

        private static void MarkSynced(long queueId)
        {
            UpdateQueue(queueId, StatusSynced, null, false, true);
        }

        private static void MarkFailed(long queueId, Exception ex)
        {
            bool conflict = !IsSqlConnectivityFailure(ex) || ex is InvalidOperationException || ex is ArgumentException;
            UpdateQueue(queueId, conflict ? StatusConflict : StatusFailed, SensitiveDataRedactor.Redact(ex.Message), conflict, false);
        }

        private static void UpdateQueue(long queueId, string status, string error, bool requiresReview, bool synced)
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
    SyncedUtc=@synced
WHERE QueueId=@id;", conn))
                {
                    cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@error", string.IsNullOrWhiteSpace(error) ? (object)DBNull.Value : error);
                    cmd.Parameters.AddWithValue("@review", requiresReview ? 1 : 0);
                    cmd.Parameters.AddWithValue("@synced", synced ? (object)DateTime.UtcNow.ToString("o") : DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", queueId);
                    cmd.ExecuteNonQuery();
                }
            }

            RaisePendingChanged();
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

        private sealed class JobPartPayload
        {
            public int JobId { get; set; }
            public int? InventoryItemId { get; set; }
            public decimal Quantity { get; set; }
            public string ItemDescription { get; set; }
            public decimal? UnitCostOverride { get; set; }
        }
    }
}
