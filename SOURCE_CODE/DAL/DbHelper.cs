using System;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    public static class DbHelper
    {
        private static readonly object AMCSchemaLock = new object();
        private static bool _amcSchemaEnsured;

        /// <summary>Reserved for approved quotation schema migrations.</summary>
        public static void EnsureQuotationSchemaMigration()
        {
            // Schema Guard: table renames require Harshal approval and a compatibility view release plan.
        }

        /// <summary>Ensures the additive AMC dashboard columns and guarded constraints exist.</summary>
        public static void EnsureAMCSchema()
        {
            if (_amcSchemaEnsured)
                return;

            lock (AMCSchemaLock)
            {
                if (_amcSchemaEnsured)
                    return;

                using (var connection = DatabaseConnectionFactory.CreateConnection())
                {
                    DatabaseConnectionFactory.Open(connection, "DbHelper.EnsureAMCSchema");

                    Execute(connection, @"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AMCContracts')
BEGIN
    CREATE TABLE AMCContracts (
        ContractID INT IDENTITY(1,1) PRIMARY KEY,
        ClientID INT NOT NULL,
        SiteID INT NULL,
        StartDate DATETIME NULL,
        EndDate DATETIME NULL,
        MonthlyValue DECIMAL(12,2) NOT NULL DEFAULT 0,
        AnnualValue DECIMAL(12,2) NOT NULL DEFAULT 0,
        ContractStatus NVARCHAR(50) NOT NULL DEFAULT 'Active',
        SLAResponseTimeHours INT NOT NULL DEFAULT 0,
        SLAUptimePercent DECIMAL(5,2) NOT NULL DEFAULT 0,
        SLARepairTimeHours INT NOT NULL DEFAULT 0,
        MaintenanceFrequency NVARCHAR(100) NULL,
        ContractType NVARCHAR(50) NOT NULL DEFAULT 'AMC',
        Notes NVARCHAR(MAX) NULL
    );
END;");

                    Execute(connection, @"
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'AMCNumber') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD AMCNumber NVARCHAR(30) NULL;
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'EquipmentDesc') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD EquipmentDesc NVARCHAR(500) NULL;
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'AMCType') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD AMCType NVARCHAR(50) NOT NULL DEFAULT 'Comprehensive';
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'ContractValue') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD ContractValue DECIMAL(12,2) NOT NULL DEFAULT 0;
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'BillingCycle') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD BillingCycle NVARCHAR(30) NOT NULL DEFAULT 'Annual';
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'VisitsPerYear') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD VisitsPerYear INT NOT NULL DEFAULT 2;
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD Status NVARCHAR(30) NOT NULL DEFAULT 'Active';
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'CreatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE();
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD UpdatedAt DATETIME NOT NULL DEFAULT GETDATE();
END;
IF OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL AND COL_LENGTH('dbo.AMCContracts', 'CoverageType') IS NULL
BEGIN
    ALTER TABLE dbo.AMCContracts ADD CoverageType NVARCHAR(30) NOT NULL DEFAULT 'Comprehensive';
END;");

                    Execute(connection, @"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_AMCContracts_AMCNumber'
      AND object_id = OBJECT_ID('dbo.AMCContracts')
)
CREATE UNIQUE INDEX UX_AMCContracts_AMCNumber
ON dbo.AMCContracts(AMCNumber)
WHERE AMCNumber IS NOT NULL;");

                    Execute(connection, @"
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'FK_AMCContracts_Clients'
)
AND OBJECT_ID('dbo.B2BClients', 'U') IS NOT NULL
BEGIN
    ALTER TABLE AMCContracts WITH NOCHECK
        ADD CONSTRAINT FK_AMCContracts_Clients
        FOREIGN KEY (ClientID) REFERENCES B2BClients(ClientID);
END;");

                    Execute(connection, @"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AMCEquipment')
BEGIN
    CREATE TABLE AMCEquipment (
        EquipmentID INT IDENTITY(1,1) PRIMARY KEY,
        AMCID INT NOT NULL,
        EquipmentName NVARCHAR(200) NOT NULL,
        ModelNumber NVARCHAR(100) NULL,
        SerialNumber NVARCHAR(100) NULL,
        InstallDate DATE NULL,
        Location NVARCHAR(200) NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'FK_AMCEquipment_AMC'
)
AND OBJECT_ID('dbo.AMCEquipment', 'U') IS NOT NULL
AND OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.AMCEquipment WITH NOCHECK
        ADD CONSTRAINT FK_AMCEquipment_AMC
        FOREIGN KEY (AMCID) REFERENCES dbo.AMCContracts(ContractID);
END;");

                    Execute(connection, @"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AMCVisits')
BEGIN
    CREATE TABLE AMCVisits (
        VisitID INT IDENTITY(1,1) PRIMARY KEY,
        AMCID INT NOT NULL,
        VisitNumber INT NOT NULL,
        ScheduledDate DATE NOT NULL,
        CompletedDate DATE NULL,
        JobID INT NULL,
        TechnicianName NVARCHAR(200) NULL,
        WorkDone NVARCHAR(1000) NULL,
        PartsUsed NVARCHAR(500) NULL,
        Status NVARCHAR(30) NOT NULL DEFAULT 'Scheduled',
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'FK_AMCVisits_AMC'
)
AND OBJECT_ID('dbo.AMCVisits', 'U') IS NOT NULL
AND OBJECT_ID('dbo.AMCContracts', 'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.AMCVisits WITH NOCHECK
        ADD CONSTRAINT FK_AMCVisits_AMC
        FOREIGN KEY (AMCID) REFERENCES dbo.AMCContracts(ContractID);
END;");
                }

                _amcSchemaEnsured = true;
            }
        }

        public static bool SafeExecute(Action work, Action<Exception> onError = null, string context = null)
        {
            if (work == null)
                return false;

            try
            {
                work();
                return true;
            }
            catch (SqlException ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure(context ?? "Database operation", ex);
                HandleError(ex, onError, context ?? "Database operation");
                return false;
            }
            catch (NullReferenceException ex)
            {
                HandleError(ex, onError, context ?? "Database operation");
                return false;
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure(context ?? "Database operation", ex);
                HandleError(ex, onError, context ?? "Database operation");
                return false;
            }
        }

        public static T SafeExecute<T>(Func<T> work, T fallback = default(T), Action<Exception> onError = null, string context = null)
        {
            if (work == null)
                return fallback;

            try
            {
                return work();
            }
            catch (SqlException ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure(context ?? "Database operation", ex);
                HandleError(ex, onError, context ?? "Database operation");
                return fallback;
            }
            catch (NullReferenceException ex)
            {
                HandleError(ex, onError, context ?? "Database operation");
                return fallback;
            }
            catch (Exception ex)
            {
                DatabaseConnectionStateService.RecordOperationFailure(context ?? "Database operation", ex);
                HandleError(ex, onError, context ?? "Database operation");
                return fallback;
            }
        }

        public static bool ConnectionHealthCheck()
        {
            try
            {
                using (var connection = DatabaseConnectionFactory.CreateConnection())
                using (var command = new SqlCommand("SELECT 1", connection))
                {
                    DatabaseConnectionFactory.Open(connection, "DbHelper.ConnectionHealthCheck");
                    command.ExecuteScalar();
                    LocalSqliteFallbackStore.RecordSqlAvailable(connection.ConnectionString);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LocalSqliteFallbackStore.RecordSqlUnavailable(DatabaseManager.GetConfiguredConnectionString(), ex);
                CrashProtectionService.LogException("(database)", "ConnectionHealthCheck", ex, false);
                return false;
            }
        }

        private static void HandleError(Exception ex, Action<Exception> onError, string context)
        {
            CrashProtectionService.LogException("(database)", context, ex, false);
            if (onError != null)
                onError(ex);
        }

        /// <summary>Executes a guarded schema statement.</summary>
        private static void Execute(SqlConnection connection, string sql)
        {
            using (var command = new SqlCommand(sql, connection))
            {
                command.CommandTimeout = 60;
                command.ExecuteNonQuery();
            }
        }
    }
}
