using System;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public enum DatabaseConnectionStateKind
    {
        Unknown,
        Online,
        Degraded,
        Offline,
        Reconnecting,
        ConfigError
    }

    public sealed class DatabaseConnectionStateSnapshot
    {
        public DatabaseConnectionStateKind State { get; set; }
        public string Message { get; set; }
        public string LastError { get; set; }
        public string Server { get; set; }
        public string DatabaseName { get; set; }
        public DateTime? LastCheckedUtc { get; set; }
        public DateTime? LastSuccessfulSqlUtc { get; set; }
        public int ConsecutiveFailures { get; set; }
        public bool BusinessWritesAllowed { get; set; }
        public long LastOpenMilliseconds { get; set; }

        public DatabaseConnectionStateSnapshot Clone()
        {
            return (DatabaseConnectionStateSnapshot)MemberwiseClone();
        }
    }

    public sealed class DatabaseConnectionStateChangedEventArgs : EventArgs
    {
        public DatabaseConnectionStateChangedEventArgs(DatabaseConnectionStateSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public DatabaseConnectionStateSnapshot Snapshot { get; private set; }
    }

    public sealed class DatabaseBusinessWriteUnavailableException : InvalidOperationException
    {
        public DatabaseBusinessWriteUnavailableException(string message)
            : base(message)
        {
        }
    }

    public static class DatabaseConnectionStateService
    {
        private const int OnlineCheckIntervalMs = 30000;
        private const int OfflineRetryIntervalMs = 5000;
        private const int DegradedOpenThresholdMs = 2000;
        private static readonly object Sync = new object();
        private static BackgroundWorker _worker;
        private static bool _stopRequested;

        private static DatabaseConnectionStateSnapshot _snapshot = new DatabaseConnectionStateSnapshot
        {
            State = DatabaseConnectionStateKind.Unknown,
            Message = "SQL Server status has not been checked yet.",
            BusinessWritesAllowed = true
        };

        public static event EventHandler<DatabaseConnectionStateChangedEventArgs> StateChanged;

        /// <summary>Starts the background terminal SQL health monitor.</summary>
        public static void StartBackgroundMonitor()
        {
            lock (Sync)
            {
                if (_worker != null && _worker.IsBusy)
                    return;

                _stopRequested = false;
                _worker = new BackgroundWorker { WorkerSupportsCancellation = true };
                _worker.DoWork += MonitorLoop;
                Publish(UpdateSnapshot(DatabaseConnectionStateKind.Reconnecting, "Checking office SQL Server connection...", string.Empty, null, null, false, 0));
                _worker.RunWorkerAsync();
            }
        }

        /// <summary>Stops the background terminal SQL health monitor.</summary>
        public static void StopBackgroundMonitor()
        {
            lock (Sync)
            {
                _stopRequested = true;
                if (_worker != null && _worker.IsBusy)
                    _worker.CancelAsync();
            }
        }

        /// <summary>Returns the current terminal SQL connection state.</summary>
        public static DatabaseConnectionStateSnapshot GetCurrentState()
        {
            lock (Sync)
                return _snapshot.Clone();
        }

        /// <summary>Checks SQL Server immediately and returns the updated terminal state.</summary>
        public static DatabaseConnectionStateSnapshot CheckNow(string context)
        {
            return CheckNow(context, false);
        }

        /// <summary>Checks SQL Server immediately, optionally marking the state as reconnecting first.</summary>
        public static DatabaseConnectionStateSnapshot CheckNow(string context, bool markReconnecting)
        {
            if (markReconnecting)
                Publish(UpdateSnapshot(DatabaseConnectionStateKind.Reconnecting, "Checking office SQL Server connection...", string.Empty, null, null, false, 0));

            string connectionString = null;
            try
            {
                connectionString = DatabaseManager.RequireConfiguredConnectionString();
                using (SqlConnection connection = DatabaseConnectionFactory.CreateConnection(connectionString))
                using (SqlCommand command = new SqlCommand("SELECT 1", connection))
                {
                    command.CommandTimeout = DatabaseConnectionFactory.DefaultConnectTimeoutSeconds;
                    DateTime start = DateTime.UtcNow;
                    DatabaseConnectionFactory.Open(connection, SafeContext(context));
                    object result = command.ExecuteScalar();
                    long elapsed = (long)(DateTime.UtcNow - start).TotalMilliseconds;
                    if (Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1)
                    {
                        DatabaseConnectionStateKind state = elapsed >= DegradedOpenThresholdMs
                            ? DatabaseConnectionStateKind.Degraded
                            : DatabaseConnectionStateKind.Online;
                        string message = state == DatabaseConnectionStateKind.Degraded
                            ? "Office SQL Server is reachable, but response is slow. Work can continue; check server/network if this repeats."
                            : "Office SQL Server is connected. Business entries are available.";
                        return Publish(UpdateSnapshot(state, message, string.Empty, connection.DataSource, connection.Database, true, elapsed));
                    }
                }

                return Publish(UpdateFailure(connectionString, new InvalidOperationException("SQL Server validation query did not return the expected result."), SafeContext(context)));
            }
            catch (Exception ex)
            {
                return Publish(UpdateFailure(connectionString, ex, SafeContext(context)));
            }
        }

        /// <summary>Records that a SQL operation succeeded for the supplied module or context.</summary>
        public static void RecordOperationSuccess(string moduleName)
        {
            try
            {
                DatabaseConnectionStateSnapshot current = GetCurrentState();
                if (current.State == DatabaseConnectionStateKind.Online && current.BusinessWritesAllowed)
                    return;

                string connectionString = DatabaseManager.GetConfiguredConnectionString();
                string server;
                string database;
                ParseConnection(connectionString, out server, out database);
                Publish(UpdateSnapshot(
                    DatabaseConnectionStateKind.Online,
                    "Office SQL Server is connected. Business entries are available.",
                    string.Empty,
                    server,
                    database,
                    true,
                    0));
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("DatabaseConnectionStateService.RecordOperationSuccess", ex);
            }
        }

        /// <summary>Records that a SQL operation failed and updates terminal state when the failure is connection-related.</summary>
        public static void RecordOperationFailure(string moduleName, Exception ex)
        {
            if (!IsConnectionRelated(ex))
                return;

            try
            {
                string connectionString = DatabaseManager.GetConfiguredConnectionString();
                Publish(UpdateFailure(connectionString, ex, SafeContext(moduleName)));
            }
            catch (Exception logEx)
            {
                AppRuntime.LogException("DatabaseConnectionStateService.RecordOperationFailure", logEx);
            }
        }

        /// <summary>Returns true when business writes are currently safe for the supplied module.</summary>
        public static bool RequireBusinessWritesAvailable(string moduleName)
        {
            DatabaseConnectionStateSnapshot current = GetCurrentState();
            if (current.BusinessWritesAllowed)
                return true;

            DatabaseConnectionStateSnapshot refreshed = CheckNow(SafeContext(moduleName) + ".WriteGuard", true);
            return refreshed.BusinessWritesAllowed;
        }

        /// <summary>Throws a shared user-facing exception if business writes are locked.</summary>
        public static void EnsureBusinessWritesAvailable(string moduleName)
        {
            if (RequireBusinessWritesAvailable(moduleName))
                return;

            throw new DatabaseBusinessWriteUnavailableException(BuildBusinessWriteLockMessage());
        }

        /// <summary>Builds the common operator message for the current terminal SQL state.</summary>
        public static string BuildUserMessage()
        {
            DatabaseConnectionStateSnapshot current = GetCurrentState();
            if (current.State == DatabaseConnectionStateKind.Offline || current.State == DatabaseConnectionStateKind.ConfigError)
                return BuildBusinessWriteLockMessage();

            if (current.State == DatabaseConnectionStateKind.Degraded)
                return current.Message;

            return string.IsNullOrWhiteSpace(current.Message)
                ? "SQL Server status has not been checked yet."
                : current.Message;
        }

        /// <summary>Builds a support-ready terminal SQL status block.</summary>
        public static string BuildSupportStatusText()
        {
            DatabaseConnectionStateSnapshot current = GetCurrentState();
            return "Terminal SQL State: " + current.State + Environment.NewLine +
                   "Message: " + SafeValue(current.Message) + Environment.NewLine +
                   "Machine: " + Environment.MachineName + Environment.NewLine +
                   "Server: " + SafeValue(current.Server) + Environment.NewLine +
                   "Database: " + SafeValue(current.DatabaseName) + Environment.NewLine +
                   "Business Writes: " + (current.BusinessWritesAllowed ? "Available" : "Locked") + Environment.NewLine +
                   "Last Checked UTC: " + FormatUtc(current.LastCheckedUtc) + Environment.NewLine +
                   "Last Successful SQL UTC: " + FormatUtc(current.LastSuccessfulSqlUtc) + Environment.NewLine +
                   "Consecutive Failures: " + current.ConsecutiveFailures.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
                   "Last SQL Open: " + current.LastOpenMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms" + Environment.NewLine +
                   "Last Error: " + SafeValue(current.LastError);
        }

        private static void MonitorLoop(object sender, DoWorkEventArgs e)
        {
            while (!_stopRequested)
            {
                try
                {
                    CheckNow("DatabaseConnectionStateService.Monitor", false);
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("DatabaseConnectionStateService.MonitorLoop", ex);
                }

                int waitMs = GetCurrentState().BusinessWritesAllowed ? OnlineCheckIntervalMs : OfflineRetryIntervalMs;
                int slept = 0;
                while (!_stopRequested && slept < waitMs)
                {
                    Thread.Sleep(500);
                    slept += 500;
                }
            }
        }

        private static DatabaseConnectionStateSnapshot UpdateSnapshot(
            DatabaseConnectionStateKind state,
            string message,
            string error,
            string server,
            string database,
            bool success,
            long elapsedMs)
        {
            lock (Sync)
            {
                DateTime now = DateTime.UtcNow;
                DatabaseConnectionStateSnapshot next = _snapshot.Clone();
                next.State = state;
                next.Message = message ?? string.Empty;
                next.LastError = error ?? string.Empty;
                next.LastCheckedUtc = now;
                next.LastOpenMilliseconds = elapsedMs;
                if (!string.IsNullOrWhiteSpace(server))
                    next.Server = server;
                if (!string.IsNullOrWhiteSpace(database))
                    next.DatabaseName = database;
                if (success)
                {
                    next.LastSuccessfulSqlUtc = now;
                    next.ConsecutiveFailures = 0;
                    next.BusinessWritesAllowed = true;
                }
                else if (state == DatabaseConnectionStateKind.Reconnecting)
                {
                    next.BusinessWritesAllowed = false;
                }
                else
                {
                    next.ConsecutiveFailures++;
                    next.BusinessWritesAllowed = false;
                }

                _snapshot = next;
                return next.Clone();
            }
        }

        private static DatabaseConnectionStateSnapshot UpdateFailure(string connectionString, Exception ex, string context)
        {
            string server;
            string database;
            ParseConnection(connectionString, out server, out database);

            DatabaseConnectionStateKind state = IsConfigurationError(ex)
                ? DatabaseConnectionStateKind.ConfigError
                : DatabaseConnectionStateKind.Offline;
            string message = state == DatabaseConnectionStateKind.ConfigError
                ? "Saved SQL Server configuration is invalid. Open Help & Support > Database Check or Database Connection Setup."
                : BuildBusinessWriteLockMessage();
            string error = ex == null ? string.Empty : ex.Message;

            LocalSqliteFallbackStore.RecordSqlUnavailable(connectionString, ex);
            LocalSqliteFallbackStore.RecordEvent("SQL_STATE_" + state, context + " | " + error);
            return UpdateSnapshot(state, message, error, server, database, false, 0);
        }

        private static DatabaseConnectionStateSnapshot Publish(DatabaseConnectionStateSnapshot snapshot)
        {
            EventHandler<DatabaseConnectionStateChangedEventArgs> handler = StateChanged;
            if (handler != null)
                handler(null, new DatabaseConnectionStateChangedEventArgs(snapshot.Clone()));

            return snapshot;
        }

        private static bool IsConnectionRelated(Exception ex)
        {
            if (ex == null)
                return false;

            if (ex is SqlException)
                return true;

            if (ex is InvalidOperationException)
                return true;

            string message = ex.Message ?? string.Empty;
            return message.IndexOf("SQL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsConfigurationError(Exception ex)
        {
            SqlException sql = ex as SqlException;
            if (sql != null)
            {
                foreach (SqlError error in sql.Errors)
                {
                    if (error.Number == 18456 || error.Number == 4060)
                        return true;
                }
            }

            return ex is InvalidOperationException;
        }

        private static string BuildBusinessWriteLockMessage()
        {
            return "ServoERP cannot reach the office SQL Server. Business entries are locked to protect GST, ledger, inventory, and payroll data. Check the server PC/network, then use Help & Support > Database Check.";
        }

        private static void ParseConnection(string connectionString, out string server, out string database)
        {
            server = string.Empty;
            database = string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
                server = builder.DataSource ?? string.Empty;
                database = builder.InitialCatalog ?? string.Empty;
            }
            catch
            {
            }
        }

        private static string SafeContext(string context)
        {
            return string.IsNullOrWhiteSpace(context) ? "Database operation" : context.Trim();
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string FormatUtc(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : "-";
        }
    }
}
