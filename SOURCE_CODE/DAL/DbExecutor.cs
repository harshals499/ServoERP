using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    public sealed class DbExecutor
    {
        private readonly Func<SqlConnection> _connectionFactory;
        private readonly int _commandTimeoutSeconds;

        public DbExecutor()
            : this(() => new DatabaseManager().GetConnection(), 120)
        {
        }

        public DbExecutor(int commandTimeoutSeconds)
            : this(() => new DatabaseManager().GetConnection(), commandTimeoutSeconds)
        {
        }

        public DbExecutor(string connectionString, int commandTimeoutSeconds = 120)
            : this(() => DatabaseConnectionFactory.CreateConnection(connectionString), commandTimeoutSeconds)
        {
        }

        public DbExecutor(Func<SqlConnection> connectionFactory, int commandTimeoutSeconds = 120)
        {
            if (connectionFactory == null)
                throw new ArgumentNullException(nameof(connectionFactory));

            _connectionFactory = connectionFactory;
            _commandTimeoutSeconds = commandTimeoutSeconds <= 0 ? 120 : commandTimeoutSeconds;
        }

        public List<T> Query<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] parameters)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            try
            {
                var result = new List<T>();
                using (SqlConnection conn = _connectionFactory())
                using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
                {
                    OpenWithRetry(conn, "DbExecutor.Query");
                    Stopwatch sw = Stopwatch.StartNew();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            result.Add(map(reader));
                    }
                    DatabaseConnectionFactory.LogQueryDuration("DbExecutor.Query", sql, sw.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure("DbExecutor.Query", ex);
                CrashProtectionService.LogException("(database)", "DbExecutor.Query", ex, false);
                throw;
            }
        }

        public T QuerySingle<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] parameters)
        {
            List<T> rows = Query(sql, map, parameters);
            return rows.Count == 0 ? default(T) : rows[0];
        }

        public DataTable QueryTable(string sql, params SqlParameter[] parameters)
        {
            try
            {
                var table = new DataTable();
                using (SqlConnection conn = _connectionFactory())
                using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                {
                    OpenWithRetry(conn, "DbExecutor.QueryTable");
                    Stopwatch sw = Stopwatch.StartNew();
                    adapter.Fill(table);
                    DatabaseConnectionFactory.LogQueryDuration("DbExecutor.QueryTable", sql, sw.ElapsedMilliseconds);
                }

                return table;
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure("DbExecutor.QueryTable", ex);
                CrashProtectionService.LogException("(database)", "DbExecutor.QueryTable", ex, false);
                throw;
            }
        }

        public int Execute(string sql, params SqlParameter[] parameters)
        {
            try
            {
                DatabaseConnectionStateService.EnsureBusinessWritesAvailable("DbExecutor.Execute");
                using (SqlConnection conn = _connectionFactory())
                using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
                {
                    OpenWithRetry(conn, "DbExecutor.Execute");
                    Stopwatch sw = Stopwatch.StartNew();
                    int affected = cmd.ExecuteNonQuery();
                    DatabaseConnectionFactory.LogQueryDuration("DbExecutor.Execute", sql, sw.ElapsedMilliseconds);
                    return affected;
                }
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure("DbExecutor.Execute", ex);
                CrashProtectionService.LogException("(database)", "DbExecutor.Execute", ex, false);
                throw;
            }
        }

        public T Scalar<T>(string sql, params SqlParameter[] parameters)
        {
            try
            {
                using (SqlConnection conn = _connectionFactory())
                using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
                {
                    OpenWithRetry(conn, "DbExecutor.Scalar");
                    Stopwatch sw = Stopwatch.StartNew();
                    object value = cmd.ExecuteScalar();
                    DatabaseConnectionFactory.LogQueryDuration("DbExecutor.Scalar", sql, sw.ElapsedMilliseconds);
                    if (value == null || value == DBNull.Value)
                        return default(T);

                    Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                    return (T)Convert.ChangeType(value, targetType);
                }
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure("DbExecutor.Scalar", ex);
                CrashProtectionService.LogException("(database)", "DbExecutor.Scalar", ex, false);
                throw;
            }
        }

        public void ExecuteInTransaction(Action<SqlConnection, SqlTransaction> work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            try
            {
                DatabaseConnectionStateService.EnsureBusinessWritesAvailable("DbExecutor.ExecuteInTransaction");
                using (SqlConnection conn = _connectionFactory())
                {
                    OpenWithRetry(conn, "DbExecutor.ExecuteInTransaction");
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        try
                        {
                            Stopwatch sw = Stopwatch.StartNew();
                            work(conn, tx);
                            DatabaseConnectionFactory.LogQueryDuration("DbExecutor.ExecuteInTransaction", "transaction delegate", sw.ElapsedMilliseconds);
                            tx.Commit();
                        }
                        catch
                        {
                            try { tx.Rollback(); } catch { }
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure("DbExecutor.ExecuteInTransaction", ex);
                CrashProtectionService.LogException("(database)", "DbExecutor.ExecuteInTransaction", ex, false);
                throw;
            }
        }

        public int Execute(SqlConnection conn, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            try
            {
                using (SqlCommand cmd = BuildCommand(sql, conn, tx, parameters))
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    int affected = cmd.ExecuteNonQuery();
                    DatabaseConnectionFactory.LogQueryDuration("DbExecutor.ExecuteTransactionCommand", sql, sw.ElapsedMilliseconds);
                    return affected;
                }
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure("DbExecutor.ExecuteTransactionCommand", ex);
                CrashProtectionService.LogException("(database)", "DbExecutor.ExecuteTransactionCommand", ex, false);
                throw;
            }
        }

        public static SqlParameter Param(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        private SqlCommand BuildCommand(string sql, SqlConnection conn, SqlTransaction tx, SqlParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL command is required.", nameof(sql));

            var cmd = new SqlCommand(sql, conn, tx)
            {
                CommandTimeout = _commandTimeoutSeconds
            };

            if (parameters != null)
            {
                foreach (SqlParameter parameter in parameters)
                {
                    if (parameter != null)
                        cmd.Parameters.Add(parameter);
                }
            }

            return cmd;
        }

        private static void OpenWithRetry(SqlConnection conn, string context)
        {
            try
            {
                DatabaseConnectionFactory.Open(conn, context);
            }
            catch (SqlException ex) when (DatabaseConnectionFactory.IsTransientSqlError(ex))
            {
                AppLogger.LogInfo("Transient SQL open failure; retrying once | " + context + " | " + ex.Message);
                try { conn.Close(); } catch { }
                Thread.Sleep(300);
                DatabaseConnectionFactory.Open(conn, context + ".Retry");
            }
        }
    }
}
