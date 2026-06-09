using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    /// <summary>Creates short-lived pooled SQL Server connections from one normalized ServoERP connection string.</summary>
    public static class DatabaseConnectionFactory
    {
        public const int DefaultMaxPoolSize = 100;
        public const int DefaultMinPoolSize = 0;
        public const int DefaultConnectTimeoutSeconds = 15;
        public const int SlowQueryThresholdMilliseconds = 2000;

        /// <summary>Creates a closed pooled connection using the configured ServoERP SQL connection string.</summary>
        public static SqlConnection CreateConnection()
        {
            return CreateConnection(DatabaseManager.RequireConfiguredConnectionString());
        }

        /// <summary>Creates a closed pooled connection using the supplied SQL connection string.</summary>
        public static SqlConnection CreateConnection(string connectionString)
        {
            return new SqlConnection(NormalizeConnectionString(connectionString));
        }

        /// <summary>Opens a configured SQL Server connection asynchronously for Task-based database flows.</summary>
        public static async Task<SqlConnection> OpenConnectionAsync(string context = null)
        {
            var connection = CreateConnection();
            await OpenAsync(connection, context ?? "DatabaseConnectionFactory.OpenConnectionAsync").ConfigureAwait(false);
            return connection;
        }

        /// <summary>Normalizes a SQL Server connection string so ADO.NET pooling is predictable across the app.</summary>
        public static string NormalizeConnectionString(string connectionString)
        {
            return NormalizeConnectionString(connectionString, null);
        }

        /// <summary>Normalizes a SQL Server connection string with an explicit max-pool override.</summary>
        public static string NormalizeConnectionString(string connectionString, int? maxPoolSize)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Database connection is not configured.");

            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Pooling = true,
                MinPoolSize = DefaultMinPoolSize,
                MaxPoolSize = NormalizeMaxPoolSize(maxPoolSize ?? GetConfiguredMaxPoolSize()),
                ConnectTimeout = DefaultConnectTimeoutSeconds
            };

            return builder.ConnectionString;
        }

        /// <summary>Opens a connection and logs open failures, timeout, and pool exhaustion diagnostics.</summary>
        public static void Open(SqlConnection connection, string context = null)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                connection.Open();
                LogSlowOpen(sw.ElapsedMilliseconds, context);
                DatabaseConnectionStateService.RecordOperationSuccess(context);
            }
            catch (SqlException ex)
            {
                LogConnectionFailure(context, ex);
                DatabaseConnectionStateService.RecordOperationFailure(context, ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                LogConnectionFailure(context, ex);
                DatabaseConnectionStateService.RecordOperationFailure(context, ex);
                throw;
            }
        }

        /// <summary>Opens a connection asynchronously and logs open failures, timeout, and pool exhaustion diagnostics.</summary>
        public static async Task OpenAsync(SqlConnection connection, string context = null)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await connection.OpenAsync().ConfigureAwait(false);
                LogSlowOpen(sw.ElapsedMilliseconds, context);
            }
            catch (SqlException ex)
            {
                LogConnectionFailure(context, ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                LogConnectionFailure(context, ex);
                throw;
            }
        }

        /// <summary>Tests database connectivity by opening a pooled connection and running SELECT 1.</summary>
        public static async Task<DatabaseConnectionTestResult> TestDatabaseConnectionAsync(string connectionString = null)
        {
            return await TestDatabaseConnectionAsync(connectionString, null).ConfigureAwait(false);
        }

        /// <summary>Tests database connectivity by opening a pooled connection and running SELECT 1.</summary>
        public static async Task<DatabaseConnectionTestResult> TestDatabaseConnectionAsync(string connectionString, int? maxPoolSize)
        {
            string normalized = null;
            try
            {
                normalized = NormalizeConnectionString(connectionString ?? DatabaseManager.RequireConfiguredConnectionString(), maxPoolSize);
                using (SqlConnection connection = new SqlConnection(normalized))
                using (SqlCommand command = new SqlCommand("SELECT 1", connection))
                {
                    command.CommandTimeout = DefaultConnectTimeoutSeconds;
                    await OpenAsync(connection, "DatabaseConnectionFactory.TestDatabaseConnectionAsync").ConfigureAwait(false);
                    object result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                    bool ok = Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1;
                    if (ok)
                    {
                        LocalSqliteFallbackStore.RecordSqlAvailable(normalized);
                        return DatabaseConnectionTestResult.Ok(BuildSuccessMessage(normalized), normalized);
                    }
                }

                return DatabaseConnectionTestResult.Fail("SQL Server responded, but the validation query did not return the expected result.", normalized);
            }
            catch (Exception ex)
            {
                LocalSqliteFallbackStore.RecordSqlUnavailable(normalized ?? connectionString ?? DatabaseManager.GetConfiguredConnectionString(), ex);
                LogConnectionFailure("DatabaseConnectionFactory.TestDatabaseConnectionAsync", ex);
                return DatabaseConnectionTestResult.Fail(ToUserFriendlyMessage(ex), normalized);
            }
        }

        /// <summary>Logs any query or command that exceeds the global slow-query threshold.</summary>
        public static void LogQueryDuration(string context, string sql, long elapsedMilliseconds)
        {
            if (elapsedMilliseconds < SlowQueryThresholdMilliseconds)
                return;

            AppLogger.LogInfo(
                "SLOW SQL | " + SafeContext(context) +
                " | " + elapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms | " +
                SafeSql(sql));
        }

        /// <summary>Returns true for transient SQL/network errors that are safe to retry before a command starts.</summary>
        public static bool IsTransientSqlError(SqlException ex)
        {
            if (ex == null)
                return false;

            foreach (SqlError error in ex.Errors)
            {
                switch (error.Number)
                {
                    case -2:      // timeout
                    case 64:      // network name no longer available
                    case 233:     // no process on other end of pipe
                    case 10053:
                    case 10054:
                    case 10060:
                    case 10928:
                    case 10929:
                    case 40197:
                    case 40501:
                    case 40613:
                    case 49918:
                    case 49919:
                    case 49920:
                        return true;
                    case 18456:   // bad credentials
                    case 4060:    // invalid database/login cannot open database
                        return false;
                }
            }

            return false;
        }

        /// <summary>Gets the configured maximum SQL connection-pool size.</summary>
        public static int GetConfiguredMaxPoolSize()
        {
            string raw = ConfigService.Get("Database", "MaxPoolSize", DefaultMaxPoolSize.ToString(CultureInfo.InvariantCulture));
            int value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return DefaultMaxPoolSize;

            return NormalizeMaxPoolSize(value);
        }

        /// <summary>Saves the maximum SQL connection-pool size used when normalizing connection strings.</summary>
        public static void SetConfiguredMaxPoolSize(int value)
        {
            int clamped = Math.Max(20, Math.Min(500, value));
            ConfigService.Set("Database", "MaxPoolSize", clamped.ToString(CultureInfo.InvariantCulture));
        }

        private static int NormalizeMaxPoolSize(int value)
        {
            return Math.Max(20, Math.Min(500, value));
        }

        private static void LogSlowOpen(long elapsedMilliseconds, string context)
        {
            if (elapsedMilliseconds < SlowQueryThresholdMilliseconds)
                return;

            AppLogger.LogInfo(
                "SLOW SQL OPEN | " + SafeContext(context) +
                " | " + elapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " ms");
        }

        private static void LogConnectionFailure(string context, Exception ex)
        {
            string prefix = IsPoolExhaustion(ex) ? "SQL POOL EXHAUSTION" :
                IsTimeout(ex) ? "SQL TIMEOUT" :
                "SQL CONNECTION FAILURE";

            AppLogger.LogInfo(prefix + " | " + SafeContext(context) + " | " + (ex == null ? "Unknown error" : ex.Message));
            if (ex != null)
                Logger.Log(SafeContext(context), ex);
        }

        private static bool IsPoolExhaustion(Exception ex)
        {
            return ex != null &&
                   ex.Message != null &&
                   ex.Message.IndexOf("max pool size", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTimeout(Exception ex)
        {
            SqlException sql = ex as SqlException;
            if (sql != null)
            {
                foreach (SqlError error in sql.Errors)
                    if (error.Number == -2)
                        return true;
            }

            return ex != null &&
                   ex.Message != null &&
                   ex.Message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToUserFriendlyMessage(Exception ex)
        {
            if (IsPoolExhaustion(ex))
                return "ServoERP could not get a SQL Server connection from the pool. Close unused ServoERP windows and try again. If this repeats, increase Max Pool Size in Database Connection Setup.";

            if (IsTimeout(ex))
                return "ServoERP could not reach SQL Server within 15 seconds. Check the office server PC, network, SQL Server service, and firewall.";

            SqlException sql = ex as SqlException;
            if (sql != null)
            {
                foreach (SqlError error in sql.Errors)
                {
                    if (error.Number == 18456)
                        return "SQL Server rejected the username or password. Check the saved database login.";
                    if (error.Number == 4060)
                        return "SQL Server is reachable, but the configured database could not be opened. Check the database name.";
                }
            }

            return ex == null ? "Database connection failed." : ex.Message;
        }

        private static string BuildSuccessMessage(string normalizedConnectionString)
        {
            var builder = new SqlConnectionStringBuilder(normalizedConnectionString);
            return "Connected to SQL Server. Server: " + builder.DataSource +
                   " | Database: " + builder.InitialCatalog +
                   " | Pooling: " + builder.Pooling +
                   " | Max Pool Size: " + builder.MaxPoolSize.ToString(CultureInfo.InvariantCulture);
        }

        private static string SafeContext(string context)
        {
            return string.IsNullOrWhiteSpace(context) ? "Database operation" : context.Trim();
        }

        private static string SafeSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return "(empty sql)";

            string compact = sql.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            while (compact.Contains("  "))
                compact = compact.Replace("  ", " ");

            compact = SensitiveDataRedactor.Redact(compact);
            return compact.Length <= 500 ? compact : compact.Substring(0, 500) + " ...";
        }
    }

    public sealed class DatabaseConnectionTestResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public string ConnectionString { get; private set; }

        public static DatabaseConnectionTestResult Ok(string message, string connectionString)
        {
            return new DatabaseConnectionTestResult { Success = true, Message = message ?? "Database connection successful.", ConnectionString = connectionString };
        }

        public static DatabaseConnectionTestResult Fail(string message, string connectionString)
        {
            return new DatabaseConnectionTestResult { Success = false, Message = message ?? "Database connection failed.", ConnectionString = connectionString };
        }
    }
}
