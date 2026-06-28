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
            AppRuntime.LogConnection("Offline SQLite queue disabled.");
        }

        public static OfflineQueueResult Queue<T>(string module, string operation, T payload, int? serverRecordId, bool requiresReview, string reason)
        {
            return Queue(module, operation, payload, serverRecordId, requiresReview, reason, null);
        }

        public static OfflineQueueResult Queue<T>(string module, string operation, T payload, int? serverRecordId, bool requiresReview, string reason, Guid? entitySyncPublicId)
        {
            throw new InvalidOperationException("Offline SQLite queue is disabled. ServoERP will not save business entries locally when SQL Server is unavailable.");
        }

        public static int GetPendingCount()
        {
            return 0;
        }

        public static List<OfflineSyncItem> GetPendingItems(int max = 100)
        {
            return new List<OfflineSyncItem>();
        }

        public static int TryReplayPending()
        {
            return 0;
        }

        public static bool ShouldQueue(Exception ex)
        {
            return false;
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
            if (module == "Sites" && operation == "Create")
            {
                new SiteService().Create(JsonConvert.DeserializeObject<ClientSite>(item.PayloadJson));
                return;
            }
            if (module == "Sites" && operation == "Update")
            {
                new SiteService().Update(JsonConvert.DeserializeObject<ClientSite>(item.PayloadJson));
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
