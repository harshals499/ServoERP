using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.DAL
{
    public class ContractRepository
    {
        private DatabaseManager _dbManager;

        public ContractRepository()
        {
            _dbManager = new DatabaseManager();
        }

        public List<AMCContract> GetAll()
        {
            List<AMCContract> contracts = new List<AMCContract>();

            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM AMCContracts ORDER BY EndDate ASC";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        contracts.Add(MapContract(reader));
                }
            }

            return contracts;
        }

        public AMCContract GetById(int contractId)
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM AMCContracts WHERE ContractID = @contractId";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@contractId", contractId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            return MapContract(reader);
                    }
                }
            }

            return null;
        }

        public List<AMCContract> GetByClientId(int clientId)
        {
            List<AMCContract> contracts = new List<AMCContract>();

            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT * FROM AMCContracts WHERE ClientID = @clientId ORDER BY EndDate ASC";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", clientId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            contracts.Add(MapContract(reader));
                    }
                }
            }

            return contracts;
        }

        public List<AMCContract> GetExpiringContracts(int daysUntilExpiry)
        {
            List<AMCContract> contracts = new List<AMCContract>();

            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = @"SELECT * FROM AMCContracts
                    WHERE EndDate BETWEEN GETDATE() AND DATEADD(day, @days, GETDATE())
                    ORDER BY EndDate ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@days", daysUntilExpiry);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            contracts.Add(MapContract(reader));
                    }
                }
            }

            return contracts;
        }

        public decimal GetMonthlyRecurringRevenue()
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT ISNULL(SUM(MonthlyValue), 0) FROM AMCContracts WHERE ContractStatus = 'Active'";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    object result = cmd.ExecuteScalar();
                    return result != DBNull.Value ? (decimal)result : 0;
                }
            }
        }

        public int GetActiveContractCount()
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = "SELECT COUNT(*) FROM AMCContracts WHERE ContractStatus = 'Active'";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        public int Create(AMCContract contract)
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = @"INSERT INTO AMCContracts
                    (ClientID, SiteID, StartDate, EndDate, MonthlyValue, AnnualValue, ContractStatus,
                     SLAResponseTimeHours, SLAUptimePercent, SLARepairTimeHours, MaintenanceFrequency,
                     ContractType, Notes, CreatedByUserId, CreatedByName)
                    VALUES
                    (@clientId, @siteId, @startDate, @endDate, @monthlyValue, @annualValue, @contractStatus,
                     @slaResponse, @slaUptime, @slaRepair, @maintenance,
                     @contractType, @notes, @createdByUserId, @createdByName);
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@clientId", contract.ClientID);
                    cmd.Parameters.AddWithValue("@siteId", contract.SiteID);
                    cmd.Parameters.AddWithValue("@startDate", contract.StartDate);
                    cmd.Parameters.AddWithValue("@endDate", contract.EndDate);
                    cmd.Parameters.AddWithValue("@monthlyValue", contract.MonthlyValue);
                    cmd.Parameters.AddWithValue("@annualValue", contract.AnnualValue);
                    cmd.Parameters.AddWithValue("@contractStatus", contract.ContractStatus ?? "Active");
                    cmd.Parameters.AddWithValue("@slaResponse", contract.SLAResponseTimeHours);
                    cmd.Parameters.AddWithValue("@slaUptime", contract.SLAUptimePercent);
                    cmd.Parameters.AddWithValue("@slaRepair", contract.SLARepairTimeHours);
                    cmd.Parameters.AddWithValue("@maintenance",   contract.MaintenanceFrequency ?? "Monthly");
                    cmd.Parameters.AddWithValue("@contractType", contract.ContractType ?? "AMC");
                    cmd.Parameters.AddWithValue("@notes",        contract.Notes        ?? "");
                    cmd.Parameters.AddWithValue("@createdByUserId", contract.CreatedByUserId.HasValue ? (object)contract.CreatedByUserId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@createdByName", string.IsNullOrWhiteSpace(contract.CreatedByName) ? (object)DBNull.Value : contract.CreatedByName);

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Update(AMCContract contract)
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                string query = @"UPDATE AMCContracts SET
                    StartDate = @startDate, EndDate = @endDate,
                    MonthlyValue = @monthlyValue, AnnualValue = @annualValue,
                    ContractStatus = @contractStatus,
                    SLAResponseTimeHours = @slaResponse, SLAUptimePercent = @slaUptime,
                    SLARepairTimeHours = @slaRepair, MaintenanceFrequency = @maintenance,
                    ContractType = @contractType, Notes = @notes,
                    ModifiedByUserId = @modifiedByUserId, ModifiedByName = @modifiedByName, ModifiedDate = @modifiedDate
                    WHERE ContractID = @contractId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@startDate", contract.StartDate);
                    cmd.Parameters.AddWithValue("@endDate", contract.EndDate);
                    cmd.Parameters.AddWithValue("@monthlyValue", contract.MonthlyValue);
                    cmd.Parameters.AddWithValue("@annualValue", contract.AnnualValue);
                    cmd.Parameters.AddWithValue("@contractStatus", contract.ContractStatus ?? "Active");
                    cmd.Parameters.AddWithValue("@slaResponse", contract.SLAResponseTimeHours);
                    cmd.Parameters.AddWithValue("@slaUptime", contract.SLAUptimePercent);
                    cmd.Parameters.AddWithValue("@slaRepair", contract.SLARepairTimeHours);
                    cmd.Parameters.AddWithValue("@maintenance",   contract.MaintenanceFrequency ?? "Monthly");
                    cmd.Parameters.AddWithValue("@contractType", contract.ContractType ?? "AMC");
                    cmd.Parameters.AddWithValue("@notes",        contract.Notes        ?? "");
                    cmd.Parameters.AddWithValue("@modifiedByUserId", contract.ModifiedByUserId.HasValue ? (object)contract.ModifiedByUserId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@modifiedByName", string.IsNullOrWhiteSpace(contract.ModifiedByName) ? (object)DBNull.Value : contract.ModifiedByName);
                    cmd.Parameters.AddWithValue("@modifiedDate", contract.ModifiedDate.HasValue ? (object)contract.ModifiedDate.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@contractId",   contract.ContractID);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private AMCContract MapContract(SqlDataReader reader)
        {
            return new AMCContract
            {
                ContractID = (int)reader["ContractID"],
                ClientID = (int)reader["ClientID"],
                SiteID = (int)reader["SiteID"],
                StartDate = (DateTime)reader["StartDate"],
                EndDate = (DateTime)reader["EndDate"],
                MonthlyValue = (decimal)reader["MonthlyValue"],
                AnnualValue = (decimal)reader["AnnualValue"],
                ContractStatus = reader["ContractStatus"].ToString(),
                SLAResponseTimeHours = (int)reader["SLAResponseTimeHours"],
                SLAUptimePercent = (decimal)reader["SLAUptimePercent"],
                SLARepairTimeHours = (int)reader["SLARepairTimeHours"],
                MaintenanceFrequency = reader["MaintenanceFrequency"].ToString(),
                ContractType = reader["ContractType"] != DBNull.Value ? reader["ContractType"].ToString() : "AMC",
                Notes        = reader["Notes"]        != DBNull.Value ? reader["Notes"].ToString()        : "",
                CreatedByUserId = reader["CreatedByUserId"] != DBNull.Value ? (int?)reader["CreatedByUserId"] : null,
                CreatedByName = reader["CreatedByName"] != DBNull.Value ? reader["CreatedByName"].ToString() : null,
                ModifiedByUserId = reader["ModifiedByUserId"] != DBNull.Value ? (int?)reader["ModifiedByUserId"] : null,
                ModifiedByName = reader["ModifiedByName"] != DBNull.Value ? reader["ModifiedByName"].ToString() : null,
                ModifiedDate = reader["ModifiedDate"] != DBNull.Value ? (DateTime?)reader["ModifiedDate"] : null
            };
        }
    }
}
