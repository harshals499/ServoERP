using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
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
            using (SqlConnection conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
                return conn.Query<AMCContract>(ContractSelectSql + " ORDER BY EndDate ASC").ToList();
            }
        }

        public AMCContract GetById(int contractId)
        {
            using (SqlConnection conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
                return conn.QueryFirstOrDefault<AMCContract>(
                    ContractSelectSql + " WHERE ContractID = @contractId",
                    new { contractId });
            }
        }

        public List<AMCContract> GetByClientId(int clientId)
        {
            using (SqlConnection conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
                return conn.Query<AMCContract>(
                    ContractSelectSql + " WHERE ClientID = @clientId ORDER BY EndDate ASC",
                    new { clientId }).ToList();
            }
        }

        public List<AMCContract> GetExpiringContracts(int daysUntilExpiry)
        {
            using (SqlConnection conn = DapperDatabase.CreateConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
                string query = ContractSelectSql + @"
                    WHERE EndDate BETWEEN GETDATE() AND DATEADD(day, @days, GETDATE())
                    ORDER BY EndDate ASC";
                return conn.Query<AMCContract>(query, new { days = daysUntilExpiry }).ToList();
            }
        }

        public decimal GetMonthlyRecurringRevenue()
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
                string query = "SELECT ISNULL(SUM(MonthlyValue), 0) FROM AMCContracts WHERE ContractStatus = 'Active'";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    object result = cmd.ExecuteScalar();
                    return result != null && result != DBNull.Value ? (decimal)result : 0m;
                }
            }
        }

        public int GetActiveContractCount()
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
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
                EnsureReadSchema(conn);
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
                    cmd.Parameters.AddWithValue("@siteId", contract.SiteID > 0 ? (object)contract.SiteID : DBNull.Value);
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
                EnsureReadSchema(conn);
                string query = @"UPDATE AMCContracts SET
                    ClientID = @clientId, SiteID = @siteId,
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
                    cmd.Parameters.AddWithValue("@clientId", contract.ClientID);
                    cmd.Parameters.AddWithValue("@siteId", contract.SiteID > 0 ? (object)contract.SiteID : DBNull.Value);
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

        public void Delete(int contractId)
        {
            using (SqlConnection conn = _dbManager.GetConnection())
            {
                conn.Open();
                EnsureReadSchema(conn);
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        ExecuteDelete(conn, tx, "IF OBJECT_ID('dbo.Invoices', 'U') IS NOT NULL AND COL_LENGTH('dbo.Invoices', 'ContractID') IS NOT NULL UPDATE dbo.Invoices SET ContractID=NULL WHERE ContractID=@id", contractId);
                        ExecuteDelete(conn, tx, "IF OBJECT_ID('dbo.SLALogs', 'U') IS NOT NULL AND COL_LENGTH('dbo.SLALogs', 'ContractID') IS NOT NULL DELETE FROM dbo.SLALogs WHERE ContractID=@id", contractId);
                        ExecuteDelete(conn, tx, "IF OBJECT_ID('dbo.Jobs', 'U') IS NOT NULL AND COL_LENGTH('dbo.Jobs', 'LinkedContractId') IS NOT NULL UPDATE dbo.Jobs SET LinkedContractId=NULL WHERE LinkedContractId=@id", contractId);
                        ExecuteDelete(conn, tx, "IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NOT NULL AND COL_LENGTH('dbo.PurchaseOrders', 'RelatedContractID') IS NOT NULL UPDATE dbo.PurchaseOrders SET RelatedContractID=NULL WHERE RelatedContractID=@id", contractId);
                        ExecuteDelete(conn, tx, "IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NOT NULL AND COL_LENGTH('dbo.PurchaseOrders', 'LinkedToId') IS NOT NULL AND COL_LENGTH('dbo.PurchaseOrders', 'LinkedToType') IS NOT NULL UPDATE dbo.PurchaseOrders SET LinkedToId=NULL WHERE LinkedToType='Contract' AND LinkedToId=@id", contractId);
                        ExecuteDelete(conn, tx, "DELETE FROM AMCContracts WHERE ContractID=@id", contractId);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void ExecuteDelete(SqlConnection conn, SqlTransaction tx, string sql, int id)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        private const string ContractSelectSql = @"
SELECT
    ContractID,
    ClientID,
    ISNULL(SiteID, 0) AS SiteID,
    ISNULL(StartDate, GETDATE()) AS StartDate,
    ISNULL(EndDate, DATEADD(year, 1, GETDATE())) AS EndDate,
    ISNULL(MonthlyValue, 0) AS MonthlyValue,
    ISNULL(AnnualValue, 0) AS AnnualValue,
    ISNULL(ContractStatus, 'Active') AS ContractStatus,
    ISNULL(SLAResponseTimeHours, 0) AS SLAResponseTimeHours,
    ISNULL(SLAUptimePercent, 0) AS SLAUptimePercent,
    ISNULL(SLARepairTimeHours, 0) AS SLARepairTimeHours,
    ISNULL(MaintenanceFrequency, 'Monthly') AS MaintenanceFrequency,
    ISNULL(ContractType, 'AMC') AS ContractType,
    ISNULL(Notes, '') AS Notes,
    CreatedByUserId,
    CreatedByName,
    ModifiedByUserId,
    ModifiedByName,
    ModifiedDate
FROM AMCContracts";

        private static void EnsureReadSchema(SqlConnection conn)
        {
            conn.Execute(@"IF OBJECT_ID('dbo.AMCContracts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AMCContracts (
        ContractID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ClientID INT NOT NULL DEFAULT 0,
        SiteID INT NULL,
        StartDate DATETIME NULL,
        EndDate DATETIME NULL,
        MonthlyValue DECIMAL(12,2) NULL,
        AnnualValue DECIMAL(12,2) NULL,
        ContractStatus NVARCHAR(50) NULL,
        SLAResponseTimeHours INT NULL,
        SLAUptimePercent DECIMAL(5,2) NULL,
        SLARepairTimeHours INT NULL,
        MaintenanceFrequency NVARCHAR(50) NULL,
        ContractType NVARCHAR(50) NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedByUserId INT NULL,
        CreatedByName NVARCHAR(100) NULL,
        ModifiedByUserId INT NULL,
        ModifiedByName NVARCHAR(100) NULL,
        ModifiedDate DATETIME NULL
    );
END

IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.AMCContracts', 'ClientID') IS NULL ALTER TABLE dbo.AMCContracts ADD ClientID INT NOT NULL CONSTRAINT DF_AMCContracts_ClientID DEFAULT 0;
    IF COL_LENGTH('dbo.AMCContracts', 'SiteID') IS NULL ALTER TABLE dbo.AMCContracts ADD SiteID INT NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'StartDate') IS NULL ALTER TABLE dbo.AMCContracts ADD StartDate DATETIME NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'EndDate') IS NULL ALTER TABLE dbo.AMCContracts ADD EndDate DATETIME NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'CreatedByUserId') IS NULL ALTER TABLE dbo.AMCContracts ADD CreatedByUserId INT NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'CreatedByName') IS NULL ALTER TABLE dbo.AMCContracts ADD CreatedByName NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'ModifiedByUserId') IS NULL ALTER TABLE dbo.AMCContracts ADD ModifiedByUserId INT NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'ModifiedByName') IS NULL ALTER TABLE dbo.AMCContracts ADD ModifiedByName NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'ModifiedDate') IS NULL ALTER TABLE dbo.AMCContracts ADD ModifiedDate DATETIME NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'MonthlyValue') IS NULL ALTER TABLE dbo.AMCContracts ADD MonthlyValue DECIMAL(12,2) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'AnnualValue') IS NULL ALTER TABLE dbo.AMCContracts ADD AnnualValue DECIMAL(12,2) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'ContractStatus') IS NULL ALTER TABLE dbo.AMCContracts ADD ContractStatus NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'SLAResponseTimeHours') IS NULL ALTER TABLE dbo.AMCContracts ADD SLAResponseTimeHours INT NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'SLAUptimePercent') IS NULL ALTER TABLE dbo.AMCContracts ADD SLAUptimePercent DECIMAL(5,2) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'SLARepairTimeHours') IS NULL ALTER TABLE dbo.AMCContracts ADD SLARepairTimeHours INT NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'MaintenanceFrequency') IS NULL ALTER TABLE dbo.AMCContracts ADD MaintenanceFrequency NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'ContractType') IS NULL ALTER TABLE dbo.AMCContracts ADD ContractType NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.AMCContracts', 'Notes') IS NULL ALTER TABLE dbo.AMCContracts ADD Notes NVARCHAR(MAX) NULL;
END");
        }
    }
}
