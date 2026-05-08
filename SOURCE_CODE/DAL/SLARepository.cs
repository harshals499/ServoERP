using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class SLARepository
    {
        private DatabaseManager _dbManager;

        public SLARepository()
        {
            _dbManager = new DatabaseManager();
        }

        public void LogSLAEvent(SLALog log)
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = @"INSERT INTO SLALogs
                    (ContractID, MetricType, Target, Actual, LogDate, Compliant, Notes)
                    VALUES
                    (@contractId, @metricType, @target, @actual, @logDate, @compliant, @notes)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@contractId", log.ContractID);
                    cmd.Parameters.AddWithValue("@metricType", log.MetricType ?? "");
                    cmd.Parameters.AddWithValue("@target", log.Target ?? "");
                    cmd.Parameters.AddWithValue("@actual", log.Actual ?? "");
                    cmd.Parameters.AddWithValue("@logDate", log.LogDate);
                    cmd.Parameters.AddWithValue("@compliant", log.Compliant);
                    cmd.Parameters.AddWithValue("@notes", log.Notes ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public decimal CalculateSLACompliance(int contractId, DateTime month)
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = @"SELECT
                    CASE WHEN COUNT(*) = 0 THEN 0
                    ELSE CAST(SUM(CASE WHEN Compliant = 1 THEN 1 ELSE 0 END) AS DECIMAL) / COUNT(*) * 100
                    END AS CompliancePercent
                    FROM SLALogs
                    WHERE ContractID = @contractId
                    AND MONTH(LogDate) = @month
                    AND YEAR(LogDate) = @year";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@contractId", contractId);
                    cmd.Parameters.AddWithValue("@month", month.Month);
                    cmd.Parameters.AddWithValue("@year", month.Year);

                    object result = cmd.ExecuteScalar();
                    return result != DBNull.Value ? (decimal)result : 0;
                }
            }
        }

        public List<SLALog> GetSLABreaches(int contractId)
        {
            List<SLALog> breaches = new List<SLALog>();

            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM SLALogs WHERE ContractID = @contractId AND Compliant = 0 ORDER BY LogDate DESC";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@contractId", contractId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            breaches.Add(new SLALog
                            {
                                LogID = (int)reader["LogID"],
                                ContractID = (int)reader["ContractID"],
                                MetricType = reader["MetricType"].ToString(),
                                Target = reader["Target"].ToString(),
                                Actual = reader["Actual"].ToString(),
                                LogDate = (DateTime)reader["LogDate"],
                                Compliant = (bool)reader["Compliant"],
                                Notes = reader["Notes"].ToString()
                            });
                        }
                    }
                }
            }

            return breaches;
        }

        public List<SLALog> GetAllLogsForContract(int contractId)
        {
            List<SLALog> logs = new List<SLALog>();

            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM SLALogs WHERE ContractID = @contractId ORDER BY LogDate DESC";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@contractId", contractId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            logs.Add(new SLALog
                            {
                                LogID = (int)reader["LogID"],
                                ContractID = (int)reader["ContractID"],
                                MetricType = reader["MetricType"].ToString(),
                                Target = reader["Target"].ToString(),
                                Actual = reader["Actual"].ToString(),
                                LogDate = (DateTime)reader["LogDate"],
                                Compliant = (bool)reader["Compliant"],
                                Notes = reader["Notes"].ToString()
                            });
                        }
                    }
                }
            }

            return logs;
        }

        public List<SLALog> GetAll()
        {
            var list = new List<SLALog>();
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT * FROM SLALogs ORDER BY LogDate DESC", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                        list.Add(new SLALog
                        {
                            LogID = (int)r["LogID"],
                            ContractID = (int)r["ContractID"],
                            MetricType = r["MetricType"].ToString(),
                            Target = r["Target"].ToString(),
                            Actual = r["Actual"].ToString(),
                            LogDate = (DateTime)r["LogDate"],
                            Compliant = (bool)r["Compliant"],
                            Notes = r["Notes"].ToString()
                        });
            }
            return list;
        }
    }
}
