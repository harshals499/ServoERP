using System.Data.SqlClient;

namespace HVAC_Pro_Desktop.DAL
{
    /// <summary>Provides short-lived SQL Server connections for Dapper repository operations.</summary>
    public static class DapperDatabase
    {
        /// <summary>Creates a closed pooled SQL Server connection for Dapper queries and commands.</summary>
        public static SqlConnection CreateConnection()
        {
            return DatabaseConnectionFactory.CreateConnection();
        }
    }
}
