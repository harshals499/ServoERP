using System;
using System.Data.SQLite;
using System.IO;
using System.Text;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public static class LocalSqliteFallbackStore
    {
        private const string DefaultFallbackPath = @"C:\HVAC_PRO_MSE\DATABASE\ServoERP_Fallback.sqlite";
        private static readonly object Sync = new object();

        /// <summary>Returns the configured local SQLite fallback database path.</summary>
        public static string GetDatabasePath()
        {
            string configured = ConfigService.Get("Fallback", "SqlitePath", DefaultFallbackPath);
            return string.IsNullOrWhiteSpace(configured) ? DefaultFallbackPath : configured.Trim();
        }

        /// <summary>SQLite fallback is intentionally disabled; SQL availability is logged through normal ServoERP logs.</summary>
        public static void EnsureReady()
        {
            AppRuntime.LogConnection("SQLite fallback disabled; startup continues without local fallback storage.");
        }

        /// <summary>Records that the configured SQL Server database is reachable.</summary>
        public static void RecordSqlAvailable(string connectionString)
        {
            AppRuntime.LogConnection("SQL available: " + SensitiveDataRedactor.Redact(connectionString));
        }

        /// <summary>Records that the configured SQL Server database is unavailable.</summary>
        public static void RecordSqlUnavailable(string connectionString, Exception ex)
        {
            string error = ex == null ? string.Empty : SensitiveDataRedactor.Redact(ex.Message);
            AppRuntime.LogConnection("SQL unavailable: " + SensitiveDataRedactor.Redact(connectionString) + " | " + error);
        }

        /// <summary>Returns a plain-text summary of the local SQLite fallback state.</summary>
        public static string BuildStatusText()
        {
            return "SQLite fallback: disabled" + Environment.NewLine +
                   "Offline queue: disabled" + Environment.NewLine +
                   "Startup: SQL is optional; database screens use SQL when it is reachable.";
        }

        /// <summary>Records a recovery note in the local SQLite fallback event log.</summary>
        public static void RecordEvent(string eventType, string message)
        {
            AppRuntime.LogConnection("Fallback event ignored because SQLite fallback is disabled: " + (eventType ?? string.Empty) + " | " + (message ?? string.Empty));
        }

        /// <summary>Writes the current SQL status into the local SQLite fallback database.</summary>
        private static void RecordSqlStatus(string connectionString, string status, string error, bool success)
        {
            try
            {
                EnsureReady();
                string server;
                string database;
                ParseConnection(connectionString, out server, out database);
                string now = DateTime.UtcNow.ToString("o");

                using (SQLiteConnection conn = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT OR REPLACE INTO FallbackStatus
    (Id, LastUpdatedUtc, MachineName, AppVersion, ConfiguredSqlServer, DatabaseName,
     LastSqlStatus, LastSqlError, LastSuccessfulSqlUtc)
VALUES
    (1, @updatedUtc, @machine, @version, @server, @database, @status, @error,
     COALESCE(@successUtc, (SELECT LastSuccessfulSqlUtc FROM FallbackStatus WHERE Id = 1)));", conn))
                {
                    cmd.Parameters.AddWithValue("@updatedUtc", now);
                    cmd.Parameters.AddWithValue("@machine", Environment.MachineName);
                    cmd.Parameters.AddWithValue("@version", ConfigService.GetAppVersion());
                    cmd.Parameters.AddWithValue("@server", server ?? string.Empty);
                    cmd.Parameters.AddWithValue("@database", database ?? string.Empty);
                    cmd.Parameters.AddWithValue("@status", status ?? string.Empty);
                    cmd.Parameters.AddWithValue("@error", error ?? string.Empty);
                    cmd.Parameters.AddWithValue("@successUtc", success ? (object)now : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                RecordEvent("SQL_" + (status ?? "Unknown"), error);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("LocalSqliteFallbackStore.RecordSqlStatus", ex);
            }
        }

        /// <summary>Opens the local SQLite fallback database connection.</summary>
        private static SQLiteConnection OpenConnection()
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder
            {
                DataSource = GetDatabasePath(),
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal
            };

            SQLiteConnection conn = new SQLiteConnection(builder.ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>Executes a local SQLite schema command.</summary>
        private static void Execute(SQLiteConnection conn, string sql)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        /// <summary>Extracts server and database names from a SQL Server connection string.</summary>
        private static void ParseConnection(string connectionString, out string server, out string database)
        {
            server = string.Empty;
            database = string.Empty;

            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            try
            {
                System.Data.SqlClient.SqlConnectionStringBuilder builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                server = builder.DataSource ?? string.Empty;
                database = builder.InitialCatalog ?? string.Empty;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("LocalSqliteFallbackStore.ParseConnection", ex);
            }
        }

        /// <summary>Reads a SQLite string value safely.</summary>
        private static string SafeRead(SQLiteDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? "-" : reader.GetString(index);
        }
    }
}
