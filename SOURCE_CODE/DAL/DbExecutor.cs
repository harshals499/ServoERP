using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

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
            : this(() => new SqlConnection(connectionString), commandTimeoutSeconds)
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

            var result = new List<T>();
            using (SqlConnection conn = _connectionFactory())
            using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(map(reader));
                }
            }

            return result;
        }

        public T QuerySingle<T>(string sql, Func<SqlDataReader, T> map, params SqlParameter[] parameters)
        {
            List<T> rows = Query(sql, map, parameters);
            return rows.Count == 0 ? default(T) : rows[0];
        }

        public DataTable QueryTable(string sql, params SqlParameter[] parameters)
        {
            var table = new DataTable();
            using (SqlConnection conn = _connectionFactory())
            using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                conn.Open();
                adapter.Fill(table);
            }

            return table;
        }

        public int Execute(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = _connectionFactory())
            using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
            {
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public T Scalar<T>(string sql, params SqlParameter[] parameters)
        {
            using (SqlConnection conn = _connectionFactory())
            using (SqlCommand cmd = BuildCommand(sql, conn, null, parameters))
            {
                conn.Open();
                object value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    return default(T);

                Type targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                return (T)Convert.ChangeType(value, targetType);
            }
        }

        public void ExecuteInTransaction(Action<SqlConnection, SqlTransaction> work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            using (SqlConnection conn = _connectionFactory())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        work(conn, tx);
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

        public int Execute(SqlConnection conn, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand cmd = BuildCommand(sql, conn, tx, parameters))
                return cmd.ExecuteNonQuery();
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
    }
}
