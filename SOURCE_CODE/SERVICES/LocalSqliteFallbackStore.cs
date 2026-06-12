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

        /// <summary>Creates the local SQLite fallback database and schema when missing.</summary>
        public static void EnsureReady()
        {
            try
            {
                lock (Sync)
                {
                    string path = GetDatabasePath();
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? @"C:\HVAC_PRO_MSE\DATABASE");

                    if (!File.Exists(path))
                        SQLiteConnection.CreateFile(path);

                    using (SQLiteConnection conn = OpenConnection())
                    {
                        Execute(conn, @"
CREATE TABLE IF NOT EXISTS FallbackStatus (
    Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
    LastUpdatedUtc TEXT NOT NULL,
    MachineName TEXT NULL,
    AppVersion TEXT NULL,
    ConfiguredSqlServer TEXT NULL,
    DatabaseName TEXT NULL,
    LastSqlStatus TEXT NOT NULL,
    LastSqlError TEXT NULL,
    LastSuccessfulSqlUtc TEXT NULL
);");

                        Execute(conn, @"
CREATE TABLE IF NOT EXISTS FallbackEvents (
    EventId INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedUtc TEXT NOT NULL,
    EventType TEXT NOT NULL,
    Message TEXT NULL,
    ConfiguredSqlServer TEXT NULL,
    DatabaseName TEXT NULL
);");
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("LocalSqliteFallbackStore.EnsureReady", ex);
            }
        }

        /// <summary>Records that the configured SQL Server database is reachable.</summary>
        public static void RecordSqlAvailable(string connectionString)
        {
            RecordSqlStatus(connectionString, "Available", string.Empty, true);
        }

        /// <summary>Records that the configured SQL Server database is unavailable.</summary>
        public static void RecordSqlUnavailable(string connectionString, Exception ex)
        {
            RecordSqlStatus(connectionString, "Unavailable", ex == null ? string.Empty : SensitiveDataRedactor.Redact(ex.Message), false);
        }

        /// <summary>Returns a plain-text summary of the local SQLite fallback state.</summary>
        public static string BuildStatusText()
        {
            try
            {
                EnsureReady();
                using (SQLiteConnection conn = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
SELECT LastUpdatedUtc, MachineName, AppVersion, ConfiguredSqlServer, DatabaseName,
       LastSqlStatus, LastSqlError, LastSuccessfulSqlUtc
FROM FallbackStatus
WHERE Id = 1;", conn))
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return "SQLite fallback: ready; no SQL status recorded yet." + Environment.NewLine +
                               "Path: " + GetDatabasePath();

                    StringBuilder builder = new StringBuilder();
                    builder.AppendLine("SQLite fallback: ready");
                    builder.AppendLine("Path: " + GetDatabasePath());
                    builder.AppendLine("Last Updated UTC: " + SafeRead(reader, 0));
                    builder.AppendLine("Machine: " + SafeRead(reader, 1));
                    builder.AppendLine("App Version: " + SafeRead(reader, 2));
                    builder.AppendLine("SQL Server: " + SafeRead(reader, 3));
                    builder.AppendLine("Database: " + SafeRead(reader, 4));
                    builder.AppendLine("SQL Status: " + SafeRead(reader, 5));
                    builder.AppendLine("Last SQL Error: " + SafeRead(reader, 6));
                    builder.AppendLine("Last Successful SQL UTC: " + SafeRead(reader, 7));
                    builder.AppendLine("Offline Pending Items: " + OfflineSyncService.GetPendingCount());
                    builder.AppendLine("Business Writes: local queue enabled; SQL Server remains the source of truth after sync.");
                    return builder.ToString();
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("LocalSqliteFallbackStore.BuildStatusText", ex);
                return "SQLite fallback unavailable: " + SensitiveDataRedactor.Redact(ex.Message);
            }
        }

        /// <summary>Records a recovery note in the local SQLite fallback event log.</summary>
        public static void RecordEvent(string eventType, string message)
        {
            try
            {
                EnsureReady();
                string server;
                string database;
                ParseConnection(DatabaseManager.GetConfiguredConnectionString(), out server, out database);

                using (SQLiteConnection conn = OpenConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(@"
INSERT INTO FallbackEvents (CreatedUtc, EventType, Message, ConfiguredSqlServer, DatabaseName)
VALUES (@createdUtc, @eventType, @message, @server, @database);", conn))
                {
                    cmd.Parameters.AddWithValue("@createdUtc", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@eventType", eventType ?? string.Empty);
                    cmd.Parameters.AddWithValue("@message", message ?? string.Empty);
                    cmd.Parameters.AddWithValue("@server", server ?? string.Empty);
                    cmd.Parameters.AddWithValue("@database", database ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("LocalSqliteFallbackStore.RecordEvent", ex);
            }
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
