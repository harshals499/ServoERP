using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.DAL
{
    public class DatabaseManager
    {
        // Resolved once at startup by TryConnect()
        private static string _resolvedServer = null;
        private const string DefaultSqlInstanceName = "SQLEXPRESS";
        private const string ConnectionStringName = "HVACPro_Connection";
        private const string SqlBrowserServiceName = "SQLBrowser";
        private const string MseDataFolder = @"C:\HVAC_PRO_MSE\MSE DATA";
        private const string VendorFile = @"C:\HVAC_PRO_MSE\MSE DATA\Vendor details.xlsx";
        private const string PurchaseFile = @"C:\HVAC_PRO_MSE\MSE DATA\Purchase Details April 2025 to March 2026.xlsx";
        private const string StockFile = @"C:\HVAC_PRO_MSE\MSE DATA\Stock Summary.xlsx";
        private const string PayrollFile = @"C:\HVAC_PRO_MSE\MSE DATA\12-Attendance & Payroll_ Mar 26.xls";
        private const string ClientPurchaseArchiveFolder = @"C:\HVAC_PRO_MSE\DATABASE\Purchase Order AMC\Purchase Order AMC";
        private const string ClientPurchaseArchiveVendorName = "Client AMC PO Archive";

        private string _connectionString;
        private string _databaseName = "HVAC_PRO";
        private bool _useWindowsAuth = true;
        private string _sqlUsername = string.Empty;
        private string _sqlPassword = string.Empty;

        public DatabaseManager()
        {
            string dbPath = @"C:\HVAC_PRO_MSE\DATABASE\";
            if (!Directory.Exists(dbPath)) Directory.CreateDirectory(dbPath);
            Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\RECEIPTS");
            Directory.CreateDirectory(@"C:\HVAC_PRO_MSE\LOGS");
            PayrollFolderHelper.EnsureFolders();

            string configuredConnectionString = RequireConfiguredConnectionString();
            ApplyConnectionString(configuredConnectionString);
            _connectionString = configuredConnectionString;
        }

        public static string GetConfiguredConnectionString()
        {
            string tenantConnectionString = TryBuildInstallerConfigConnectionString();
            if (!string.IsNullOrWhiteSpace(tenantConnectionString))
                return DatabaseConnectionFactory.NormalizeConnectionString(tenantConnectionString);

            ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings[ConnectionStringName];
            return setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString)
                ? null
                : DatabaseConnectionFactory.NormalizeConnectionString(setting.ConnectionString);
        }

        public static string RequireConfiguredConnectionString()
        {
            string connectionString = GetConfiguredConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Database connection is not configured. Please set the HVACPro_Connection connection string before using the data layer.");
            }

            return connectionString;
        }

        public static bool IsDemoDataEnabled()
        {
            return !string.Equals(
                ConfigService.Get("Tenant", "SeedDemoData", "false"),
                "false",
                StringComparison.OrdinalIgnoreCase);
        }

        public static string GetCurrentTenantCode()
        {
            return ConfigService.Get("Tenant", "TenantCode", "default");
        }

        private void ApplyConnectionString(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            _resolvedServer = builder.DataSource;
            _databaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? _databaseName : builder.InitialCatalog;
            _useWindowsAuth = builder.IntegratedSecurity;
            _sqlUsername = builder.UserID ?? string.Empty;
            _sqlPassword = builder.Password ?? string.Empty;
        }

        public static string PrepareSqlServer()
        {
            string sqlServiceName = GetPrimarySqlServiceName();

            EnsureServiceRunning(sqlServiceName, 25000);
            EnsureServiceRunning(SqlBrowserServiceName, 10000, false);

            string server = FindWorkingServer(30000);
            if (string.IsNullOrWhiteSpace(server))
                throw new Exception(
                    "SQL Server Express could not be reached after starting the service.\n\n" +
                    BuildSqlDiagnostics());

            _resolvedServer = server;
            return server;
        }

        private static string GetConfiguredServer()
        {
            try
            {
                string connectionString = GetConfiguredConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                    return null;

                var builder = new SqlConnectionStringBuilder(connectionString);
                return string.IsNullOrWhiteSpace(builder.DataSource) ? null : builder.DataSource.Trim();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("DatabaseManager.GetConfiguredServer", ex);
                return null;
            }
        }

        private static string ExtractInstanceName(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
                return null;

            int slashIndex = server.IndexOf('\\');
            if (slashIndex < 0 || slashIndex >= server.Length - 1)
                return null;

            string instanceName = server.Substring(slashIndex + 1).Trim();
            return string.IsNullOrWhiteSpace(instanceName) ? null : instanceName;
        }

        private static bool ServiceExists(string serviceName)
        {
            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    var _ = service.Status;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetPrimarySqlServiceName()
        {
            string configuredServer = GetConfiguredServer();
            string configuredInstance = ExtractInstanceName(configuredServer);

            if (!string.IsNullOrWhiteSpace(configuredInstance))
            {
                string configuredServiceName = "MSSQL$" + configuredInstance;
                if (ServiceExists(configuredServiceName))
                    return configuredServiceName;
            }

            try
            {
                ServiceController runningService = ServiceController
                    .GetServices()
                    .FirstOrDefault(service =>
                        service.ServiceName.StartsWith("MSSQL$", StringComparison.OrdinalIgnoreCase) &&
                        service.Status == ServiceControllerStatus.Running);

                if (runningService != null)
                    return runningService.ServiceName;
            }
            catch
            {
            }

            return "MSSQL$" + DefaultSqlInstanceName;
        }

        private static string[] BuildServerCandidates()
        {
            var candidates = new List<string>();
            string machineName = Environment.MachineName;

            void AddCandidate(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                if (!candidates.Contains(value, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(value);
            }

            string configuredServer = GetConfiguredServer();
            AddCandidate(configuredServer);

            try
            {
                foreach (ServiceController service in ServiceController.GetServices()
                    .Where(s => s.ServiceName.StartsWith("MSSQL$", StringComparison.OrdinalIgnoreCase)))
                {
                    string instanceName = service.ServiceName.Substring(6);
                    AddCandidate(@"localhost\" + instanceName);
                    AddCandidate(@".\" + instanceName);
                    AddCandidate(@"(local)\" + instanceName);
                    AddCandidate(@"127.0.0.1\" + instanceName);
                    AddCandidate(machineName + @"\" + instanceName);
                }
            }
            catch
            {
            }

            AddCandidate(@"localhost\" + DefaultSqlInstanceName);
            AddCandidate(@".\" + DefaultSqlInstanceName);
            AddCandidate(@"(local)\" + DefaultSqlInstanceName);
            AddCandidate(@"127.0.0.1\" + DefaultSqlInstanceName);
            AddCandidate(machineName + @"\" + DefaultSqlInstanceName);

            return candidates.ToArray();
        }

        private sealed class InstallerDatabaseConfig
        {
            public string Server { get; set; }
            public string DatabaseName { get; set; }
            public bool UseWindowsAuth { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        private static string TryBuildInstallerConfigConnectionString()
        {
            InstallerDatabaseConfig config = LoadInstallerDatabaseConfig();
            if (config == null || string.IsNullOrWhiteSpace(config.Server))
                return null;

            string databaseName = !string.IsNullOrWhiteSpace(config.DatabaseName)
                ? config.DatabaseName.Trim()
                : BuildTenantDatabaseName(ConfigService.Get("Tenant", "TenantCode", string.Empty));

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = config.Server.Trim(),
                InitialCatalog = databaseName,
                ConnectTimeout = 15,
                IntegratedSecurity = config.UseWindowsAuth
            };

            if (!config.UseWindowsAuth)
            {
                builder.UserID = config.Username ?? string.Empty;
                builder.Password = config.Password ?? string.Empty;
            }

            return DatabaseConnectionFactory.NormalizeConnectionString(builder.ConnectionString);
        }

        private static string BuildTenantDatabaseName(string tenantCode)
        {
            string normalized = NormalizeTenantCode(tenantCode);
            return string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, "default", StringComparison.OrdinalIgnoreCase)
                ? "HVAC_PRO"
                : "HVAC_PRO_" + normalized;
        }

        private static string NormalizeTenantCode(string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(tenantCode))
                return string.Empty;

            string normalized = Regex.Replace(tenantCode.Trim().ToUpperInvariant(), @"[^A-Z0-9_]", "_");
            normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');
            return normalized.Length <= 40 ? normalized : normalized.Substring(0, 40);
        }

        private static InstallerDatabaseConfig LoadInstallerDatabaseConfig()
        {
            foreach (string path in GetInstallerConfigCandidates())
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var document = new XmlDocument();
                    document.Load(path);

                    return new InstallerDatabaseConfig
                    {
                        Server = ReadConfigValue(document, "/HVACProConfig/Database/Server"),
                        DatabaseName = ReadConfigValue(document, "/HVACProConfig/Database/DatabaseName"),
                        UseWindowsAuth = !string.Equals(
                            ReadConfigValue(document, "/HVACProConfig/Database/UseWindowsAuth"),
                            "false",
                            StringComparison.OrdinalIgnoreCase),
                        Username = ReadConfigValue(document, "/HVACProConfig/Database/Username"),
                        Password = ReadConfigValue(document, "/HVACProConfig/Database/Password")
                    };
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("DatabaseManager.LoadInstallerDatabaseConfig", ex);
                }
            }

            return null;
        }

        private static IEnumerable<string> GetInstallerConfigCandidates()
        {
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVACPro.config");
            yield return @"C:\HVAC_PRO_MSE\HVACPro.config";
        }

        private static string ReadConfigValue(XmlDocument document, string xpath)
        {
            return document.SelectSingleNode(xpath)?.InnerText?.Trim();
        }

        private string BuildConnectionString(string server, string database)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                ConnectTimeout = 15
            };

            if (_useWindowsAuth)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = _sqlUsername;
                builder.Password = _sqlPassword;
            }

            return DatabaseConnectionFactory.NormalizeConnectionString(builder.ConnectionString);
        }

        private string BuildCreateDatabaseSql()
        {
            string dbNameLiteral = _databaseName.Replace("'", "''");
            string dbNameIdentifier = _databaseName.Replace("]", "]]");

            return
                $@"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name=N'{dbNameLiteral}')
                   CREATE DATABASE [{dbNameIdentifier}];";
        }

        private static string FindWorkingServer(int waitMs = 5000)
        {
            string[] serverCandidates = BuildServerCandidates();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, waitMs));

            do
            {
                foreach (string srv in serverCandidates)
                {
                    if (CanOpenServer(srv))
                    {
                        return srv;
                    }
                }

                Thread.Sleep(1500);
            }

            while (DateTime.UtcNow < deadline);

            return null;
        }

        private static bool CanOpenServer(string serverName)
        {
            try
            {
                using (var conn = DatabaseConnectionFactory.CreateConnection(
                    $@"Server={serverName};Database=master;Integrated Security=true;Connection Timeout=5;"))
                {
                    DatabaseConnectionFactory.Open(conn, "DatabaseManager.CanOpenServer");
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureServiceRunning(string serviceName, int waitMs, bool required = true)
        {
            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    service.Refresh();

                    if (service.Status == ServiceControllerStatus.Running)
                        return;

                    if (service.Status == ServiceControllerStatus.StartPending)
                    {
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(waitMs));
                        return;
                    }

                    if (service.Status == ServiceControllerStatus.Paused)
                        service.Continue();
                    else
                        service.Start();

                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(waitMs));
                }
            }
            catch when (!required)
            {
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Unable to start SQL service '{serviceName}'. {ex.Message}\n\n" +
                    BuildSqlDiagnostics());
            }
        }

        public static string BuildSqlDiagnostics()
        {
            string sqlServiceName = GetPrimarySqlServiceName();
            string sqlStatus = GetServiceStatus(sqlServiceName);
            string browserStatus = GetServiceStatus(SqlBrowserServiceName);
            string candidateChecks = "";

            foreach (string srv in BuildServerCandidates())
            {
                bool ok = CanOpenServer(srv);
                candidateChecks += $"  - {srv}: {(ok ? "reachable" : "not reachable")}\n";
            }

            return
                $"Services:\n" +
                $"  - {sqlServiceName}: {sqlStatus}\n" +
                $"  - {SqlBrowserServiceName}: {browserStatus}\n\n" +
                "Server checks:\n" +
                candidateChecks;
        }

        private static string GetServiceStatus(string serviceName)
        {
            try
            {
                using (var service = new ServiceController(serviceName))
                {
                    service.Refresh();
                    return service.Status.ToString();
                }
            }
            catch (Exception ex)
            {
                return "Unavailable (" + ex.Message + ")";
            }
        }

        public SqlConnection GetConnection() => DatabaseConnectionFactory.CreateConnection(_connectionString);
        public string ResolvedServer => _resolvedServer;

        public void InitializeDatabase()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                string configuredConnectionString = RequireConfiguredConnectionString();
                ApplyConnectionString(configuredConnectionString);
                _connectionString = configuredConnectionString;
            }

            if (string.IsNullOrWhiteSpace(_resolvedServer) && string.IsNullOrWhiteSpace(_connectionString))
                _resolvedServer = PrepareSqlServer();

            string masterCs = BuildConnectionStringForDatabase("master");

            try
            {
                using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection(masterCs))
                {
                    DatabaseConnectionFactory.Open(conn, "DatabaseManager.InitializeDatabase");
                    Exec(conn, BuildCreateDatabaseSql());
                }

                CreateBaseTables();
                MigrateSchema();
            }
            catch (SqlException ex)
            {
                try
                {
                    masterCs = BuildConnectionStringForDatabase("master");

                    using (SqlConnection retryConn = DatabaseConnectionFactory.CreateConnection(masterCs))
                    {
                        DatabaseConnectionFactory.Open(retryConn, "DatabaseManager.InitializeDatabase.Retry");
                        Exec(retryConn, BuildCreateDatabaseSql());
                    }

                    CreateBaseTables();
                    MigrateSchema();
                    return;
                }
                catch
                {
                }

                throw new Exception(
                    $"Cannot connect to SQL Server Express.\n" +
                    $"Resolved server: {_resolvedServer ?? "(none)"}\n\n" +
                    $"SQL Error {ex.Number}: {ex.Message}\n\n" +
                    BuildSqlDiagnostics());
            }
            catch (Exception ex)
            {
                throw new Exception("Database initialization failed: " + ex.Message);
            }
        }

        private void CreateBaseTables()
        {
            using (SqlConnection conn = GetConnection())
            {
                DatabaseConnectionFactory.Open(conn, "DatabaseManager.CreateBaseTables");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='B2BClients')
                CREATE TABLE B2BClients (
                    ClientID          INT PRIMARY KEY IDENTITY(1,1),
                    CompanyName       NVARCHAR(255) NOT NULL,
                    IndustryType      NVARCHAR(100),
                    TotalAnnualValue  DECIMAL(12,2),
                    PrimaryContact    NVARCHAR(255),
                    SecondaryContact  NVARCHAR(255),
                    Phone             NVARCHAR(20),
                    CustomerSince     DATETIME
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Technicians')
                CREATE TABLE Technicians (
                    TechnicianID    INT PRIMARY KEY IDENTITY(1,1),
                    Name            NVARCHAR(255),
                    Phone           NVARCHAR(20),
                    HourlyRate      DECIMAL(10,2),
                    Designation     NVARCHAR(100),
                    YearsExperience INT
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientSites')
                CREATE TABLE ClientSites (
                    SiteID                   INT PRIMARY KEY IDENTITY(1,1),
                    ClientID                 INT FOREIGN KEY REFERENCES B2BClients(ClientID),
                    SiteName                 NVARCHAR(255),
                    Address                  NVARCHAR(MAX),
                    City                     NVARCHAR(100),
                    ACSystemCount            INT,
                    RefrigerationSystemCount INT,
                    CoolingTowerCount        INT,
                    IsCritical               BIT,
                    AssignedTechnicianID     INT
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AMCContracts')
                CREATE TABLE AMCContracts (
                    ContractID            INT PRIMARY KEY IDENTITY(1,1),
                    ClientID              INT FOREIGN KEY REFERENCES B2BClients(ClientID),
                    SiteID                INT FOREIGN KEY REFERENCES ClientSites(SiteID),
                    StartDate             DATETIME,
                    EndDate               DATETIME,
                    MonthlyValue          DECIMAL(12,2),
                    AnnualValue           DECIMAL(12,2),
                    ContractStatus        NVARCHAR(50),
                    SLAResponseTimeHours  INT,
                    SLAUptimePercent      DECIMAL(5,2),
                    SLARepairTimeHours    INT,
                    MaintenanceFrequency  NVARCHAR(100)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Invoices')
                CREATE TABLE Invoices (
                    InvoiceID     INT PRIMARY KEY IDENTITY(1,1),
                    ContractID    INT FOREIGN KEY REFERENCES AMCContracts(ContractID),
                    InvoiceNumber NVARCHAR(50) UNIQUE,
                    InvoiceDate   DATETIME,
                    DueDate       DATETIME,
                    SubTotal      DECIMAL(12,2),
                    TaxAmount     DECIMAL(12,2),
                    TotalAmount   DECIMAL(12,2),
                    PaidAmount    DECIMAL(12,2),
                    PaymentStatus NVARCHAR(50),
                    PaymentDate   DATETIME
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SLALogs')
                CREATE TABLE SLALogs (
                    LogID       INT PRIMARY KEY IDENTITY(1,1),
                    ContractID  INT FOREIGN KEY REFERENCES AMCContracts(ContractID),
                    MetricType  NVARCHAR(100),
                    Target      NVARCHAR(100),
                    Actual      NVARCHAR(100),
                    LogDate     DATETIME,
                    Compliant   BIT,
                    Notes       NVARCHAR(MAX)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Quotations')
                CREATE TABLE Quotations (
                    BidID         INT PRIMARY KEY IDENTITY(1,1),
                    TenderName    NVARCHAR(255),
                    SystemCount   INT,
                    BidValue      DECIMAL(12,2),
                    DueDate       DATETIME,
                    SubmittedDate DATETIME,
                    Status        NVARCHAR(50),
                    ClientName    NVARCHAR(255),
                    Notes         NVARCHAR(MAX)
                );");
            }
        }

        private void MigrateSchema()
        {
            using (SqlConnection conn = GetConnection())
            {
                DatabaseConnectionFactory.Open(conn, "DatabaseManager.MigrateSchema");

                // B2BClients
                AddColumn(conn, "B2BClients", "Email",            "NVARCHAR(255) NULL");
                AddColumn(conn, "B2BClients", "GSTNumber",        "NVARCHAR(20)  NULL");
                AddColumn(conn, "B2BClients", "PANNumber",        "NVARCHAR(20)  NULL");
                AddColumn(conn, "B2BClients", "PaymentTermsDays", "INT NOT NULL DEFAULT 30");
                AddColumn(conn, "B2BClients", "CreditLimit",      "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "B2BClients", "BillingAddress",   "NVARCHAR(MAX) NULL");
                AddColumn(conn, "B2BClients", "City",             "NVARCHAR(100) NULL");
                AddColumn(conn, "B2BClients", "GeoLatitude",      "DECIMAL(10,7) NULL");
                AddColumn(conn, "B2BClients", "GeoLongitude",     "DECIMAL(10,7) NULL");
                AddColumn(conn, "B2BClients", "GeocodeAddress",   "NVARCHAR(500) NULL");
                AddColumn(conn, "B2BClients", "GeocodeStatus",    "NVARCHAR(50) NULL");
                AddColumn(conn, "B2BClients", "GeocodeUpdatedOn", "DATETIME NULL");
                AddColumn(conn, "B2BClients", "IsActive",         "BIT NOT NULL DEFAULT 1");
                AddColumn(conn, "B2BClients", "RelationshipStage", "NVARCHAR(50) NULL");
                AddColumn(conn, "B2BClients", "Tags",             "NVARCHAR(500) NULL");
                AddColumn(conn, "B2BClients", "HealthScore",      "INT NULL");
                AddColumn(conn, "B2BClients", "Notes",            "NVARCHAR(MAX) NULL");
                AddColumn(conn, "B2BClients", "AssignedTo",       "NVARCHAR(100) NULL");
                AddColumn(conn, "B2BClients", "LeadSource",       "NVARCHAR(100) NULL");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientTeam')
                CREATE TABLE ClientTeam (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ClientId INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    EmployeeName NVARCHAR(150),
                    Position NVARCHAR(100),
                    EmailId NVARCHAR(200),
                    ContactNo NVARCHAR(30),
                    IsPrimary BIT DEFAULT 0,
                    IsActive BIT DEFAULT 1
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientActivity')
                CREATE TABLE ClientActivity (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ClientId INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    ActivityType NVARCHAR(50),
                    Title NVARCHAR(200),
                    Detail NVARCHAR(500),
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    CreatedBy NVARCHAR(100)
                );");

                // ClientSites
                AddColumn(conn, "ClientSites", "GeoLatitude", "DECIMAL(10,7) NULL");
                AddColumn(conn, "ClientSites", "GeoLongitude", "DECIMAL(10,7) NULL");
                AddColumn(conn, "ClientSites", "GeocodeAddress", "NVARCHAR(500) NULL");
                AddColumn(conn, "ClientSites", "GeocodeStatus", "NVARCHAR(50) NULL");
                AddColumn(conn, "ClientSites", "GeocodeUpdatedOn", "DATETIME NULL");
                AlterColumn(conn, "ClientSites", "GeoLatitude", "DECIMAL(10,7) NULL");
                AlterColumn(conn, "ClientSites", "GeoLongitude", "DECIMAL(10,7) NULL");
                // Site cleanup below references these columns, so ensure older/fresh
                // schemas have them before the data migration runs.
                AddColumn(conn, "Invoices", "SiteID", "INT NULL");
                AddColumn(conn, "Quotations", "SiteID", "INT NULL");
                AddColumn(conn, "Quotations", "CommercialFlow", "NVARCHAR(30) NOT NULL DEFAULT 'Revenue'");
                AddColumn(conn, "Quotations", "CustomerDocumentStatus", "NVARCHAR(40) NOT NULL DEFAULT 'Quote Draft'");
                AddColumn(conn, "Quotations", "SupplierDocumentStatus", "NVARCHAR(40) NOT NULL DEFAULT 'Not Required'");
                AddColumn(conn, "Quotations", "FlowNotes", "NVARCHAR(500) NULL");
                AddColumn(conn, "ServiceDeskIncidents", "SiteId", "INT NULL");
                Exec(conn, @"
                    IF OBJECT_ID('Jobs', 'U') IS NOT NULL
                       AND COL_LENGTH('Jobs', 'SiteID') IS NOT NULL
                    BEGIN
                        ;WITH DuplicateSites AS (
                            SELECT
                                SiteID,
                                KeepSiteID = MIN(SiteID) OVER (
                                    PARTITION BY ClientID,
                                                 UPPER(LTRIM(RTRIM(ISNULL(SiteName, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(City, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(Address, ''))))
                                )
                            FROM ClientSites
                        )
                        UPDATE j
                        SET SiteID = d.KeepSiteID
                        FROM Jobs j
                        INNER JOIN DuplicateSites d ON d.SiteID = j.SiteID
                        WHERE d.SiteID <> d.KeepSiteID;
                    END;

                    ;WITH DuplicateSites AS (
                        SELECT
                            SiteID,
                            KeepSiteID = MIN(SiteID) OVER (
                                PARTITION BY ClientID,
                                             UPPER(LTRIM(RTRIM(ISNULL(SiteName, '')))),
                                             UPPER(LTRIM(RTRIM(ISNULL(City, '')))),
                                             UPPER(LTRIM(RTRIM(ISNULL(Address, ''))))
                            )
                        FROM ClientSites
                    )
                    UPDATE c
                    SET SiteID = d.KeepSiteID
                    FROM AMCContracts c
                    INNER JOIN DuplicateSites d ON d.SiteID = c.SiteID
                    WHERE d.SiteID <> d.KeepSiteID;

                    IF OBJECT_ID('Invoices', 'U') IS NOT NULL
                       AND COL_LENGTH('Invoices', 'SiteID') IS NOT NULL
                    BEGIN
                        ;WITH DuplicateSites AS (
                            SELECT
                                SiteID,
                                KeepSiteID = MIN(SiteID) OVER (
                                    PARTITION BY ClientID,
                                                 UPPER(LTRIM(RTRIM(ISNULL(SiteName, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(City, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(Address, ''))))
                                )
                            FROM ClientSites
                        )
                        UPDATE i
                        SET SiteID = d.KeepSiteID
                        FROM Invoices i
                        INNER JOIN DuplicateSites d ON d.SiteID = i.SiteID
                        WHERE d.SiteID <> d.KeepSiteID;
                    END;

                    IF OBJECT_ID('Quotations', 'U') IS NOT NULL
                       AND COL_LENGTH('Quotations', 'SiteID') IS NOT NULL
                    BEGIN
                        ;WITH DuplicateSites AS (
                            SELECT
                                SiteID,
                                KeepSiteID = MIN(SiteID) OVER (
                                    PARTITION BY ClientID,
                                                 UPPER(LTRIM(RTRIM(ISNULL(SiteName, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(City, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(Address, ''))))
                                )
                            FROM ClientSites
                        )
                        UPDATE t
                        SET SiteID = d.KeepSiteID
                        FROM Quotations t
                        INNER JOIN DuplicateSites d ON d.SiteID = t.SiteID
                        WHERE d.SiteID <> d.KeepSiteID;
                    END;

                    IF OBJECT_ID('ServiceDeskIncidents', 'U') IS NOT NULL
                       AND COL_LENGTH('ServiceDeskIncidents', 'SiteId') IS NOT NULL
                    BEGIN
                        ;WITH DuplicateSites AS (
                            SELECT
                                SiteID,
                                KeepSiteID = MIN(SiteID) OVER (
                                    PARTITION BY ClientID,
                                                 UPPER(LTRIM(RTRIM(ISNULL(SiteName, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(City, '')))),
                                                 UPPER(LTRIM(RTRIM(ISNULL(Address, ''))))
                                )
                            FROM ClientSites
                        )
                        UPDATE s
                        SET SiteId = d.KeepSiteID
                        FROM ServiceDeskIncidents s
                        INNER JOIN DuplicateSites d ON d.SiteID = s.SiteId
                        WHERE d.SiteID <> d.KeepSiteID;
                    END;

                    ;WITH RankedSites AS (
                        SELECT
                            SiteID,
                            rn = ROW_NUMBER() OVER (
                                PARTITION BY ClientID,
                                             UPPER(LTRIM(RTRIM(ISNULL(SiteName, '')))),
                                             UPPER(LTRIM(RTRIM(ISNULL(City, '')))),
                                             UPPER(LTRIM(RTRIM(ISNULL(Address, ''))))
                                ORDER BY SiteID
                            )
                        FROM ClientSites
                    )
                    DELETE cs
                    FROM ClientSites cs
                    INNER JOIN RankedSites r ON r.SiteID = cs.SiteID
                    WHERE r.rn > 1;");

                // AMCContracts
                AddColumn(conn, "AMCContracts", "ContractType", "NVARCHAR(50) NOT NULL DEFAULT 'AMC'");
                AddColumn(conn, "AMCContracts", "Notes",        "NVARCHAR(MAX) NULL");
                DbHelper.EnsureAMCSchema();

                // Invoices
                AddColumn(conn, "Invoices", "ClientID",   "INT NULL");
                AddColumn(conn, "Invoices", "SiteID",     "INT NULL");
                AddColumn(conn, "Invoices", "QuotationBidID", "INT NULL");
                AddColumn(conn, "Invoices", "GSTPercent", "DECIMAL(5,2) NOT NULL DEFAULT 18");
                AddColumn(conn, "Invoices", "BalanceDue", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Invoices", "Notes",      "NVARCHAR(MAX) NULL");
                AddColumn(conn, "Invoices", "InvoiceTitle", "NVARCHAR(100) NOT NULL DEFAULT 'TAX INVOICE'");
                AddColumn(conn, "Invoices", "Subject",      "NVARCHAR(500) NULL");
                AddColumn(conn, "Invoices", "PONumber",     "NVARCHAR(100) NULL");
                AddColumn(conn, "Invoices", "PODate",       "DATETIME NULL");
                AddColumn(conn, "Invoices", "SendInvoiceTo","NVARCHAR(MAX) NULL");
                AddColumn(conn, "Invoices", "CertificationNote","NVARCHAR(MAX) NULL");
                AddColumn(conn, "Invoices", "TemplateCode", "NVARCHAR(50) NULL");
                AddColumn(conn, "Invoices", "WorkflowType", "NVARCHAR(50) NULL");
                AddColumn(conn, "Invoices", "GSTMode", "NVARCHAR(20) NOT NULL DEFAULT 'IGST'");
                AddColumn(conn, "Invoices", "PaymentTerms", "NVARCHAR(200) NULL");
                AddColumn(conn, "Invoices", "PlaceOfSupply", "NVARCHAR(100) NULL");
                AddColumn(conn, "Invoices", "RoundOff", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Invoices", "CGSTAmount", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Invoices", "SGSTAmount", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Invoices", "IGSTAmount", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Invoices", "ContractCoverageType", "NVARCHAR(50) NULL");
                AddColumn(conn, "Invoices", "ServiceChecklist", "NVARCHAR(MAX) NULL");
                AddColumn(conn, "Invoices", "AssetDetails", "NVARCHAR(MAX) NULL");
                AddColumn(conn, "Invoices", "WarrantyStatus", "NVARCHAR(50) NULL");
                AddColumn(conn, "Invoices", "WarrantyExpiry", "DATETIME NULL");
                AddColumn(conn, "Invoices", "PreventiveVisitDate", "DATETIME NULL");
                AddColumn(conn, "Invoices", "NextServiceDueDate", "DATETIME NULL");
                AddColumn(conn, "Invoices", "InventoryReservationStatus", "NVARCHAR(30) NOT NULL DEFAULT 'None'");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='InvoiceLineItems')
                CREATE TABLE InvoiceLineItems (
                    LineItemID  INT PRIMARY KEY IDENTITY(1,1),
                    InvoiceID   INT NOT NULL FOREIGN KEY REFERENCES Invoices(InvoiceID),
                    Description NVARCHAR(500) NOT NULL,
                    HSNCode     NVARCHAR(50)  NULL,
                    Unit        NVARCHAR(50)  NOT NULL DEFAULT 'Nos',
                    Quantity    DECIMAL(10,2) NOT NULL DEFAULT 1,
                    Rate        DECIMAL(12,2) NOT NULL DEFAULT 0,
                    Amount      DECIMAL(12,2) NOT NULL DEFAULT 0
                );");
                AddColumn(conn, "InvoiceLineItems", "HSNCode", "NVARCHAR(50) NULL");
                AddColumn(conn, "InvoiceLineItems", "Unit",    "NVARCHAR(50) NOT NULL DEFAULT 'Nos'");
                AddColumn(conn, "InvoiceLineItems", "Category", "NVARCHAR(50) NOT NULL DEFAULT 'Service'");
                AddColumn(conn, "InvoiceLineItems", "StockItemID", "INT NULL");
                AddColumn(conn, "InvoiceLineItems", "DiscountPercent", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "InvoiceLineItems", "GSTPercent", "DECIMAL(5,2) NOT NULL DEFAULT 18");
                AddColumn(conn, "InvoiceLineItems", "TaxType", "NVARCHAR(30) NOT NULL DEFAULT 'Taxable'");
                AddColumn(conn, "InvoiceLineItems", "TaxAmount", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "InvoiceLineItems", "IsStockItem", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "InvoiceLineItems", "IsBillable", "BIT NOT NULL DEFAULT 1");
                AddColumn(conn, "InvoiceLineItems", "CoverageNote", "NVARCHAR(200) NULL");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='UnitMeasurements')
                CREATE TABLE UnitMeasurements (
                    UnitMeasurementID INT IDENTITY(1,1) PRIMARY KEY,
                    UnitCode         NVARCHAR(20) NOT NULL UNIQUE,
                    DisplayName      NVARCHAR(80) NOT NULL,
                    IsActive         BIT NOT NULL DEFAULT 1,
                    IsSystem         BIT NOT NULL DEFAULT 0,
                    Notes            NVARCHAR(255) NULL,
                    CreatedAt        DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='UnitMeasurementAliases')
                CREATE TABLE UnitMeasurementAliases (
                    UnitAliasID INT IDENTITY(1,1) PRIMARY KEY,
                    UnitAlias NVARCHAR(50) NOT NULL UNIQUE,
                    UnitMeasurementId INT NOT NULL FOREIGN KEY REFERENCES UnitMeasurements(UnitMeasurementID)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='UserCardLayouts')
                CREATE TABLE UserCardLayouts (
                    LayoutId      INT IDENTITY(1,1) PRIMARY KEY,
                    UserId        INT NOT NULL DEFAULT 0,
                    PageKey       NVARCHAR(100) NOT NULL,
                    CardKey       NVARCHAR(100) NOT NULL,
                    Width         INT NOT NULL DEFAULT 0,
                    Height        INT NOT NULL DEFAULT 0,
                    SizePreset    NVARCHAR(20) NULL,
                    SavedAt       DATETIME NOT NULL DEFAULT GETDATE()
                );");
                Exec(conn, @"IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = 'UX_UserCardLayouts_User_Page_Card'
                      AND object_id = OBJECT_ID('UserCardLayouts'))
                CREATE UNIQUE INDEX UX_UserCardLayouts_User_Page_Card
                    ON UserCardLayouts(UserId, PageKey, CardKey);");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Payments')
                CREATE TABLE Payments (
                    PaymentID       INT PRIMARY KEY IDENTITY(1,1),
                    PaymentNumber   NVARCHAR(50) NOT NULL,
                    InvoiceID       INT NOT NULL FOREIGN KEY REFERENCES Invoices(InvoiceID),
                    ClientID        INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    AmountPaid      DECIMAL(12,2) NOT NULL,
                    PaymentDate     DATETIME NOT NULL,
                    PaymentMode     NVARCHAR(50) NOT NULL DEFAULT 'Bank Transfer',
                    ReferenceNumber NVARCHAR(100) NULL,
                    Notes           NVARCHAR(MAX) NULL,
                    CreatedDate     DATETIME NOT NULL DEFAULT GETDATE()
                );");

                // â”€â”€ NEW: Vendors â”€â”€
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Vendors')
                CREATE TABLE Vendors (
                    VendorID    INT PRIMARY KEY IDENTITY(1,1),
                    VendorName  NVARCHAR(255) NOT NULL,
                    GSTNumber   NVARCHAR(20)  NULL,
                    DefaultCreditDays INT NOT NULL DEFAULT 30,
                    PANNumber   NVARCHAR(20)  NULL,
                    Phone       NVARCHAR(20)  NULL,
                    Email       NVARCHAR(255) NULL,
                    Address     NVARCHAR(MAX) NULL,
                    City        NVARCHAR(100) NULL,
                    Category    NVARCHAR(100) NULL,
                    IsActive    BIT NOT NULL DEFAULT 1,
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
                );");
                AddColumn(conn, "Vendors", "GeoLatitude", "DECIMAL(10,6) NULL");
                AddColumn(conn, "Vendors", "GeoLongitude", "DECIMAL(10,6) NULL");
                AddColumn(conn, "Vendors", "GeocodeAddress", "NVARCHAR(500) NULL");
                AddColumn(conn, "Vendors", "GeocodeStatus", "NVARCHAR(50) NULL");
                AddColumn(conn, "Vendors", "GeocodeUpdatedOn", "DATETIME NULL");

                // â”€â”€ NEW: PurchaseOrders â”€â”€
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PurchaseOrders')
                CREATE TABLE PurchaseOrders (
                    POID        INT PRIMARY KEY IDENTITY(1,1),
                    VendorID    INT NOT NULL FOREIGN KEY REFERENCES Vendors(VendorID),
                    PONumber    NVARCHAR(50)  NOT NULL,
                    PODate      DATETIME      NOT NULL,
                    PayByDate   DATETIME      NULL,
                    VendorInvoiceNumber NVARCHAR(100) NULL,
                    LinkedToType NVARCHAR(30) NULL,
                    LinkedToId  INT NULL,
                    TotalAmount DECIMAL(12,2) NOT NULL DEFAULT 0,
                    PaidAmount  DECIMAL(12,2) NOT NULL DEFAULT 0,
                    Status      NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
                    PaymentReference NVARCHAR(100) NULL,
                    DeliveryMode NVARCHAR(30) NOT NULL DEFAULT 'TechPickup',
                    AssignedTechnicianId INT NULL,
                    AssignedTechnicianName NVARCHAR(100) NULL,
                    DeliveryAddress NVARCHAR(500) NULL,
                    AddToClientInvoice BIT NOT NULL DEFAULT 0,
                    PendingChargeCreated BIT NOT NULL DEFAULT 0,
                    ReceiptImagePath NVARCHAR(500) NULL,
                    PriceVarianceFlag BIT NOT NULL DEFAULT 0,
                    CreatedByUserId INT NULL,
                    CreatedByName NVARCHAR(100) NULL,
                    CreatedByDate DATETIME NOT NULL DEFAULT GETDATE(),
                    Notes       NVARCHAR(MAX) NULL,
                    CreatedDate DATETIME      NOT NULL DEFAULT GETDATE()
                );");

                // â”€â”€ NEW: PurchaseLineItems â”€â”€
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PurchaseLineItems')
                CREATE TABLE PurchaseLineItems (
                    LineItemID  INT PRIMARY KEY IDENTITY(1,1),
                    POID        INT NOT NULL FOREIGN KEY REFERENCES PurchaseOrders(POID),
                    InventoryItemId INT NULL,
                    Description NVARCHAR(500) NOT NULL,
                    HsnSacCode  NVARCHAR(20)  NULL,
                    Quantity    DECIMAL(10,2) NOT NULL DEFAULT 1,
                    Rate        DECIMAL(12,2) NOT NULL DEFAULT 0,
                    GSTRate     DECIMAL(5,2)  NOT NULL DEFAULT 0,
                    CGSTRate    DECIMAL(5,2)  NOT NULL DEFAULT 0,
                    SGSTRate    DECIMAL(5,2)  NOT NULL DEFAULT 0,
                    IGSTRate    DECIMAL(5,2)  NOT NULL DEFAULT 0,
                    JobLink     NVARCHAR(50) NOT NULL DEFAULT 'General',
                    LinkedWorkOrderId INT NULL,
                    LinkedWorkOrderName NVARCHAR(200) NULL,
                    PriceVariance DECIMAL(5,2) NOT NULL DEFAULT 0,
                    UOM         NVARCHAR(30) NOT NULL DEFAULT 'Nos',
                    Amount      DECIMAL(12,2) NOT NULL DEFAULT 0
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PendingCharges')
                CREATE TABLE PendingCharges (
                    PendingChargeId INT PRIMARY KEY IDENTITY(1,1),
                    WorkOrderId INT NOT NULL,
                    Description NVARCHAR(500) NOT NULL,
                    Qty DECIMAL(10,2) NOT NULL DEFAULT 0,
                    Rate DECIMAL(12,2) NOT NULL DEFAULT 0,
                    HsnSac NVARCHAR(20) NULL,
                    GSTRate DECIMAL(5,2) NOT NULL DEFAULT 0,
                    SourcePoId INT NOT NULL,
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                    IsBilled BIT NOT NULL DEFAULT 0
                );");

                // â”€â”€ NEW: StockItems â”€â”€
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='StockItems')
                CREATE TABLE StockItems (
                    ItemID           INT PRIMARY KEY IDENTITY(1,1),
                    ItemName         NVARCHAR(255) NOT NULL,
                    Category         NVARCHAR(100) NULL,
                    CurrentStock     DECIMAL(10,2) NOT NULL DEFAULT 0,
                    Unit             NVARCHAR(50)  NOT NULL DEFAULT 'Nos',
                    LastPurchaseRate DECIMAL(12,2) NOT NULL DEFAULT 0,
                    ReorderLevel     DECIMAL(10,2) NOT NULL DEFAULT 5,
                    VendorID         INT NULL FOREIGN KEY REFERENCES Vendors(VendorID),
                    LastUpdated      DATETIME NOT NULL DEFAULT GETDATE()
                );");
                AddColumn(conn, "StockItems", "ReservedStock", "DECIMAL(10,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "StockItems", "IsActive", "BIT NOT NULL DEFAULT 1");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='StockMovements')
                CREATE TABLE StockMovements (
                    MovementID      INT PRIMARY KEY IDENTITY(1,1),
                    ItemID          INT NOT NULL FOREIGN KEY REFERENCES StockItems(ItemID),
                    MovementType    NVARCHAR(30) NOT NULL,
                    Quantity        DECIMAL(10,2) NOT NULL,
                    StockBefore     DECIMAL(10,2) NOT NULL,
                    StockAfter      DECIMAL(10,2) NOT NULL,
                    FromLocation    NVARCHAR(120) NULL,
                    ToLocation      NVARCHAR(120) NULL,
                    ReferenceNo     NVARCHAR(80) NULL,
                    Notes           NVARCHAR(MAX) NULL,
                    CreatedByUserId INT NULL,
                    CreatedByName   NVARCHAR(120) NULL,
                    CreatedDate     DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='InvoiceTemplates')
                CREATE TABLE InvoiceTemplates (
                    TemplateID            INT PRIMARY KEY IDENTITY(1,1),
                    TemplateCode          NVARCHAR(50)  NOT NULL UNIQUE,
                    TemplateName          NVARCHAR(150) NOT NULL,
                    WorkflowType          NVARCHAR(50)  NOT NULL,
                    DefaultSubject        NVARCHAR(500) NULL,
                    DefaultNotes          NVARCHAR(MAX) NULL,
                    DefaultGstMode        NVARCHAR(20)  NOT NULL DEFAULT 'IGST',
                    DefaultGstPercent     DECIMAL(5,2)  NOT NULL DEFAULT 18,
                    DefaultPaymentTerms   NVARCHAR(200) NULL,
                    ContractCoverageType  NVARCHAR(50)  NULL,
                    DefaultChecklist      NVARCHAR(MAX) NULL,
                    DefaultAssetInfo      NVARCHAR(MAX) NULL,
                    IsActive              BIT NOT NULL DEFAULT 1,
                    CreatedDate           DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='InvoiceInventoryReservations')
                CREATE TABLE InvoiceInventoryReservations (
                    ReservationID    INT PRIMARY KEY IDENTITY(1,1),
                    InvoiceID        INT NOT NULL FOREIGN KEY REFERENCES Invoices(InvoiceID),
                    StockItemID      INT NOT NULL FOREIGN KEY REFERENCES StockItems(ItemID),
                    QuantityReserved DECIMAL(10,2) NOT NULL DEFAULT 0,
                    QuantityIssued   DECIMAL(10,2) NOT NULL DEFAULT 0,
                    Status           NVARCHAR(30) NOT NULL DEFAULT 'DraftReserved',
                    UpdatedDate      DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='InventoryUsageLog')
                CREATE TABLE InventoryUsageLog (
                    UsageID       INT PRIMARY KEY IDENTITY(1,1),
                    InvoiceID     INT NULL FOREIGN KEY REFERENCES Invoices(InvoiceID),
                    StockItemID   INT NOT NULL FOREIGN KEY REFERENCES StockItems(ItemID),
                    Quantity      DECIMAL(10,2) NOT NULL DEFAULT 0,
                    UsageAction   NVARCHAR(30) NOT NULL,
                    Notes         NVARCHAR(MAX) NULL,
                    LoggedAt      DATETIME NOT NULL DEFAULT GETDATE()
                );");

                // â”€â”€ NEW: Employees â”€â”€
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Employees')
                CREATE TABLE Employees (
                    EmployeeID   INT PRIMARY KEY IDENTITY(1,1),
                    EmployeeCode NVARCHAR(20)  NOT NULL,
                    Name         NVARCHAR(255) NOT NULL,
                    Designation  NVARCHAR(100) NULL,
                    Department   NVARCHAR(100) NULL,
                    ClientSite   NVARCHAR(255) NULL,
                    Phone        NVARCHAR(20)  NULL,
                    JoiningDate  DATETIME      NULL,
                    Status       NVARCHAR(50)  NOT NULL DEFAULT 'Active',
                    CreatedDate  DATETIME      NOT NULL DEFAULT GETDATE()
                );");

                // Employees â€” extended payroll columns
                AddColumn(conn, "Employees", "DateOfBirth",  "DATETIME      NULL");
                AddColumn(conn, "Employees", "MaritalStatus","NVARCHAR(50)  NULL");
                AddColumn(conn, "Employees", "Address",      "NVARCHAR(MAX) NULL");
                AddColumn(conn, "Employees", "PAN",          "NVARCHAR(20)  NULL");
                AddColumn(conn, "Employees", "ESICNumber",   "NVARCHAR(50)  NULL");
                AddColumn(conn, "Employees", "UANNumber",    "NVARCHAR(50)  NULL");
                AddColumn(conn, "Employees", "EPFNumber",    "NVARCHAR(50)  NULL");
                AddColumn(conn, "Employees", "BankAccount",  "NVARCHAR(50)  NULL");
                AddColumn(conn, "Employees", "IFSCCode",     "NVARCHAR(20)  NULL");
                AddColumn(conn, "Employees", "NatureOfWork", "NVARCHAR(50)  NULL");
                AddColumn(conn, "Employees", "BasicSalary",  "DECIMAL(10,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Employees", "GrossSalary",  "DECIMAL(10,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Employees", "AadhaarLast4", "NVARCHAR(4) NULL");
                AddColumn(conn, "Employees", "UAN", "NVARCHAR(12) NULL");
                AddColumn(conn, "Employees", "DateOfJoining", "DATE NULL");
                AddColumn(conn, "Employees", "EmploymentType", "NVARCHAR(20) NULL CONSTRAINT DF_Employees_EmploymentType DEFAULT 'Permanent'");
                AddColumn(conn, "Employees", "BankAccountNumber", "NVARCHAR(20) NULL");
                AddColumn(conn, "Employees", "BankIFSC", "NVARCHAR(11) NULL");
                AddColumn(conn, "Employees", "BankName", "NVARCHAR(100) NULL");
                AddColumn(conn, "Employees", "TaxRegime", "NVARCHAR(10) NULL CONSTRAINT DF_Employees_TaxRegime DEFAULT 'New'");
                AddColumn(conn, "Employees", "StateCode", "NVARCHAR(5) NULL");
                AddColumn(conn, "Employees", "EPFApplicable", "BIT NOT NULL CONSTRAINT DF_Employees_EPFApplicable DEFAULT 0");
                AddColumn(conn, "Employees", "ESIApplicable", "BIT NOT NULL CONSTRAINT DF_Employees_ESIApplicable DEFAULT 0");
                AddColumn(conn, "Employees", "PTApplicable", "BIT NOT NULL CONSTRAINT DF_Employees_PTApplicable DEFAULT 0");
                AddColumn(conn, "Employees", "Photo", "VARBINARY(MAX) NULL");
                AddColumn(conn, "Employees", "AadhaarNumber", "NVARCHAR(12) NULL");
                AddColumn(conn, "Employees", "PANNumber", "NVARCHAR(10) NULL");
                AddColumn(conn, "Employees", "BloodGroup", "NVARCHAR(5) NULL");
                AddColumn(conn, "Employees", "EmergencyContactName", "NVARCHAR(100) NULL");
                AddColumn(conn, "Employees", "EmergencyContactPhone", "NVARCHAR(15) NULL");
                AddColumn(conn, "Employees", "ProbationEndDate", "DATE NULL");
                AddColumn(conn, "Employees", "ConfirmationDate", "DATE NULL");
                AddColumn(conn, "Employees", "LastWorkingDay", "DATE NULL");
                AddColumn(conn, "Employees", "IsRehire", "BIT NOT NULL CONSTRAINT DF_Employees_IsRehire DEFAULT 0");
                AddColumn(conn, "Employees", "WhatsAppNumber", "NVARCHAR(15) NULL");

                Exec(conn, @"
                    UPDATE Employees
                    SET DateOfJoining = ISNULL(DateOfJoining, CONVERT(date, JoiningDate)),
                        UAN = CASE
                                WHEN NULLIF(UAN, '') IS NOT NULL THEN UAN
                                WHEN NULLIF(UANNumber, '') IS NOT NULL
                                     AND REPLACE(UANNumber, ' ', '') NOT LIKE '%[^0-9]%'
                                     AND LEN(REPLACE(UANNumber, ' ', '')) = 12
                                    THEN REPLACE(UANNumber, ' ', '')
                                WHEN NULLIF(EPFNumber, '') IS NOT NULL
                                     AND REPLACE(EPFNumber, ' ', '') NOT LIKE '%[^0-9]%'
                                     AND LEN(REPLACE(EPFNumber, ' ', '')) = 12
                                    THEN REPLACE(EPFNumber, ' ', '')
                                ELSE UAN
                              END,
                        BankAccountNumber = ISNULL(NULLIF(BankAccountNumber, ''), NULLIF(BankAccount, '')),
                        BankIFSC = ISNULL(NULLIF(BankIFSC, ''), NULLIF(IFSCCode, '')),
                        TaxRegime = ISNULL(NULLIF(TaxRegime, ''), 'New'),
                        EmploymentType = ISNULL(NULLIF(EmploymentType, ''), 'Permanent'),
                        EPFApplicable = CASE
                                            WHEN NULLIF(UAN, '') IS NOT NULL THEN 1
                                            WHEN NULLIF(UANNumber, '') IS NOT NULL
                                                 AND REPLACE(UANNumber, ' ', '') NOT LIKE '%[^0-9]%'
                                                 AND LEN(REPLACE(UANNumber, ' ', '')) = 12
                                                THEN 1
                                            WHEN NULLIF(EPFNumber, '') IS NOT NULL
                                                 AND REPLACE(EPFNumber, ' ', '') NOT LIKE '%[^0-9]%'
                                                 AND LEN(REPLACE(EPFNumber, ' ', '')) = 12
                                                THEN 1
                                            ELSE EPFApplicable
                                        END,
                        ESIApplicable = CASE WHEN NULLIF(ESICNumber, '') IS NOT NULL AND NULLIF(ESICNumber, '') NOT LIKE 'NOT APPLICABLE%' THEN 1 ELSE ESIApplicable END
                    WHERE 1 = 1;");

                Exec(conn, @"
                    UPDATE Employees
                    SET AadhaarNumber = CASE
                                            WHEN NULLIF(AadhaarNumber, '') IS NOT NULL THEN AadhaarNumber
                                            WHEN NULLIF(AadhaarLast4, '') IS NOT NULL THEN AadhaarLast4
                                            ELSE AadhaarNumber
                                        END,
                        PANNumber = ISNULL(NULLIF(PANNumber, ''), NULLIF(PAN, ''))
                    WHERE 1 = 1;");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='EmployeeSkills')
                CREATE TABLE EmployeeSkills (
                    SkillID INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    SkillName NVARCHAR(100) NULL,
                    CertificationNumber NVARCHAR(50) NULL,
                    ExpiryDate DATE NULL,
                    IsExpired AS (CASE WHEN ExpiryDate IS NOT NULL AND ExpiryDate < CONVERT(date, GETDATE()) THEN 1 ELSE 0 END)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='EmployeeAttendance')
                CREATE TABLE EmployeeAttendance (
                    AttendanceID INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    AttendanceDate DATE NULL,
                    CheckInTime TIME NULL,
                    CheckOutTime TIME NULL,
                    CheckInLatitude DECIMAL(9,6) NULL,
                    CheckInLongitude DECIMAL(9,6) NULL,
                    Status NVARCHAR(20) NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='EmployeeDocuments')
                CREATE TABLE EmployeeDocuments (
                    DocumentID INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    DocumentType NVARCHAR(50) NULL,
                    FileName NVARCHAR(200) NULL,
                    FileData VARBINARY(MAX) NULL,
                    UploadedOn DATETIME NOT NULL DEFAULT GETDATE(),
                    ExpiryDate DATE NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SalaryStructure')
                CREATE TABLE SalaryStructure (
                    SalaryID INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeID INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    BasicSalary DECIMAL(10,2) NULL,
                    HRA DECIMAL(10,2) NULL,
                    Allowances DECIMAL(10,2) NULL,
                    PFDeduction DECIMAL(10,2) NULL,
                    ESICDeduction DECIMAL(10,2) NULL,
                    EffectiveFrom DATE NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SalaryStructures')
                CREATE TABLE SalaryStructures (
                    StructureId INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    EffectiveFrom DATE NOT NULL,
                    EffectiveTo DATE NULL,
                    BasicSalary DECIMAL(18,2) NOT NULL DEFAULT 0,
                    DA DECIMAL(18,2) NOT NULL DEFAULT 0,
                    HRA DECIMAL(18,2) NOT NULL DEFAULT 0,
                    SpecialAllowance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ConveyanceAllowance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    MedicalAllowance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    LTA DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OtherAllowances DECIMAL(18,2) NOT NULL DEFAULT 0,
                    GrossSalary AS (BasicSalary + DA + HRA + SpecialAllowance + ConveyanceAllowance + MedicalAllowance + LTA + OtherAllowances) PERSISTED,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PayrollRuns')
                CREATE TABLE PayrollRuns (
                    PayrollRunId INT IDENTITY(1,1) PRIMARY KEY,
                    PayrollMonth INT NOT NULL,
                    PayrollYear INT NOT NULL,
                    RunDate DATETIME NOT NULL DEFAULT GETDATE(),
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Draft',
                    ProcessedBy NVARCHAR(100) NULL,
                    TotalGross DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalNetPay DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalEPFEmployee DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalEPFEmployer DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalESIEmployee DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalESIEmployer DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalTDS DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalPT DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Notes NVARCHAR(500) NULL,
                    CONSTRAINT UQ_PayrollRuns_MonthYear UNIQUE (PayrollMonth, PayrollYear)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PayrollEntries')
                CREATE TABLE PayrollEntries (
                    EntryId INT IDENTITY(1,1) PRIMARY KEY,
                    PayrollRunId INT NOT NULL FOREIGN KEY REFERENCES PayrollRuns(PayrollRunId),
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    EmployeeName NVARCHAR(100) NOT NULL,
                    Designation NVARCHAR(100) NULL,
                    BasicSalary DECIMAL(18,2) NOT NULL DEFAULT 0,
                    DA DECIMAL(18,2) NOT NULL DEFAULT 0,
                    HRA DECIMAL(18,2) NOT NULL DEFAULT 0,
                    SpecialAllowance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ConveyanceAllowance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    MedicalAllowance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    LTA DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OtherAllowances DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OvertimePay DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Bonus DECIMAL(18,2) NOT NULL DEFAULT 0,
                    GrossSalary DECIMAL(18,2) NOT NULL DEFAULT 0,
                    WorkingDaysInMonth INT NOT NULL DEFAULT 26,
                    DaysPresent DECIMAL(9,2) NOT NULL DEFAULT 26,
                    DaysAbsent DECIMAL(9,2) NOT NULL DEFAULT 0,
                    LeaveDays DECIMAL(9,2) NOT NULL DEFAULT 0,
                    OvertimeHours DECIMAL(9,2) NOT NULL DEFAULT 0,
                    EPFEmployee DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ESIEmployee DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TDSDeducted DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ProfessionalTax DECIMAL(18,2) NOT NULL DEFAULT 0,
                    LoanDeduction DECIMAL(18,2) NOT NULL DEFAULT 0,
                    AdvanceDeduction DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OtherDeductions DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalDeductions DECIMAL(18,2) NOT NULL DEFAULT 0,
                    EPFEmployer DECIMAL(18,2) NOT NULL DEFAULT 0,
                    EPSEmployer DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ESIEmployer DECIMAL(18,2) NOT NULL DEFAULT 0,
                    NetSalary DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TaxRegime NVARCHAR(10) NULL,
                    UAN NVARCHAR(12) NULL,
                    ESICNumber NVARCHAR(20) NULL,
                    BankAccount NVARCHAR(20) NULL,
                    BankIFSC NVARCHAR(11) NULL,
                    PayslipGenerated BIT NOT NULL DEFAULT 0,
                    PayslipPath NVARCHAR(500) NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='TDSCalculations')
                CREATE TABLE TDSCalculations (
                    TDSCalcId INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    FinancialYear NVARCHAR(10) NOT NULL,
                    TaxRegime NVARCHAR(10) NOT NULL,
                    EstimatedAnnualIncome DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Chapter6ADeductions DECIMAL(18,2) NOT NULL DEFAULT 0,
                    StandardDeduction DECIMAL(18,2) NOT NULL DEFAULT 75000,
                    TaxableIncome DECIMAL(18,2) NOT NULL DEFAULT 0,
                    AnnualTaxLiability DECIMAL(18,2) NOT NULL DEFAULT 0,
                    MonthlyTDS DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TDSPaidToDate DECIMAL(18,2) NOT NULL DEFAULT 0,
                    LastUpdated DATETIME NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT UQ_TDSCalculations_EmployeeFy UNIQUE (EmployeeId, FinancialYear)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ProfessionalTaxSlabs')
                CREATE TABLE ProfessionalTaxSlabs (
                    SlabId INT IDENTITY(1,1) PRIMARY KEY,
                    StateCode NVARCHAR(5) NOT NULL,
                    StateName NVARCHAR(50) NOT NULL,
                    MinSalary DECIMAL(18,2) NOT NULL,
                    MaxSalary DECIMAL(18,2) NULL,
                    MonthlyPT DECIMAL(18,2) NOT NULL,
                    EffectiveFrom DATE NOT NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='EmployeeLoans')
                CREATE TABLE EmployeeLoans (
                    LoanId INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    LoanAmount DECIMAL(18,2) NOT NULL,
                    MonthlyDeduction DECIMAL(18,2) NOT NULL,
                    LoanDate DATE NOT NULL,
                    RemainingBalance DECIMAL(18,2) NOT NULL,
                    Purpose NVARCHAR(200) NULL,
                    IsActive BIT NOT NULL DEFAULT 1
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SalaryAdvances')
                CREATE TABLE SalaryAdvances (
                    AdvanceId INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    AdvanceAmount DECIMAL(18,2) NOT NULL,
                    AdvanceDate DATE NOT NULL,
                    RecoveryMonth INT NOT NULL,
                    RecoveryYear INT NOT NULL,
                    Recovered BIT NOT NULL DEFAULT 0
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='StatutoryPayments')
                CREATE TABLE StatutoryPayments (
                    PaymentId INT IDENTITY(1,1) PRIMARY KEY,
                    PayrollRunId INT NOT NULL FOREIGN KEY REFERENCES PayrollRuns(PayrollRunId),
                    PaymentType NVARCHAR(20) NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    DueDate DATE NOT NULL,
                    PaidDate DATE NULL,
                    ReferenceNumber NVARCHAR(100) NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    Notes NVARCHAR(200) NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AttendanceRecords')
                CREATE TABLE AttendanceRecords (
                    AttendanceId INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    AttendanceDate DATE NOT NULL,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Present',
                    OvertimeHours DECIMAL(4,2) NOT NULL DEFAULT 0,
                    Notes NVARCHAR(200) NULL,
                    CONSTRAINT UQ_AttendanceRecords_EmployeeDate UNIQUE (EmployeeId, AttendanceDate)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LeaveTypes')
                CREATE TABLE LeaveTypes (
                    LeaveTypeId INT IDENTITY(1,1) PRIMARY KEY,
                    LeaveTypeName NVARCHAR(50) NOT NULL,
                    PaidLeave BIT NOT NULL DEFAULT 1,
                    AnnualQuota INT NOT NULL DEFAULT 0
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LeaveBalances')
                CREATE TABLE LeaveBalances (
                    BalanceId INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    LeaveTypeId INT NOT NULL FOREIGN KEY REFERENCES LeaveTypes(LeaveTypeId),
                    Year INT NOT NULL,
                    Opening DECIMAL(9,2) NOT NULL DEFAULT 0,
                    Accrued DECIMAL(9,2) NOT NULL DEFAULT 0,
                    Used DECIMAL(9,2) NOT NULL DEFAULT 0,
                    Closing AS (Opening + Accrued - Used) PERSISTED,
                    CONSTRAINT UQ_LeaveBalances_EmployeeLeaveYear UNIQUE (EmployeeId, LeaveTypeId, Year)
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='Jobs')
                CREATE TABLE Jobs (
                    JobID               INT PRIMARY KEY IDENTITY(1,1),
                    JobNumber           NVARCHAR(50)  NOT NULL,
                    ClientID            INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    SiteID              INT NOT NULL FOREIGN KEY REFERENCES ClientSites(SiteID),
                    Title               NVARCHAR(255) NOT NULL,
                    Description         NVARCHAR(MAX) NULL,
                    AssignedEmployeeID  INT NULL FOREIGN KEY REFERENCES Employees(EmployeeID),
                    ScheduledDate       DATETIME NOT NULL,
                    CompletedDate       DATETIME NULL,
                    Priority            NVARCHAR(50) NOT NULL DEFAULT 'Medium',
                    Status              NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    EstimatedCost       DECIMAL(12,2) NOT NULL DEFAULT 0,
                    Revenue             DECIMAL(12,2) NOT NULL DEFAULT 0,
                    Notes               NVARCHAR(MAX) NULL,
                    CreatedDate         DATETIME NOT NULL DEFAULT GETDATE()
                );");

                AddColumn(conn, "Jobs", "JobTitle", "NVARCHAR(200) NULL");
                AddColumn(conn, "Jobs", "JobType", "NVARCHAR(50) NULL");
                AddColumn(conn, "Jobs", "LinkedContractId", "INT NULL");
                AddColumn(conn, "Jobs", "PipelineStatus", "NVARCHAR(30) NULL");
                AddColumn(conn, "Jobs", "QuotedRevenue", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Jobs", "ActualRevenue", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Jobs", "IsOverdue", "BIT NULL");
                AddColumn(conn, "Jobs", "ClosedDate", "DATETIME NULL");
                AddColumn(conn, "Jobs", "InvoiceId", "INT NULL");
                AddColumn(conn, "Jobs", "CreatedByUserId", "INT NULL");
                AddColumn(conn, "Jobs", "CreatedByName", "NVARCHAR(100) NULL");
                AddColumn(conn, "Jobs", "ModifiedByUserId", "INT NULL");
                AddColumn(conn, "Jobs", "ModifiedByName", "NVARCHAR(100) NULL");
                AddColumn(conn, "Jobs", "ModifiedDate", "DATETIME NULL");
                AddColumn(conn, "ClientSites", "TravelRateINR", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                EnsureOptionalSiteReferences(conn);

                Exec(conn, @"UPDATE Jobs
                             SET JobTitle = ISNULL(NULLIF(JobTitle, ''), Title),
                                 JobType = ISNULL(NULLIF(JobType, ''), 'General'),
                                 PipelineStatus = ISNULL(NULLIF(PipelineStatus, ''), CASE
                                     WHEN Status = 'Completed' THEN 'Closed'
                                     WHEN Status = 'In Progress' THEN 'InProgress'
                                     WHEN AssignedEmployeeID IS NOT NULL THEN 'Assigned'
                                     ELSE 'Created'
                                 END),
                                 QuotedRevenue = CASE WHEN QuotedRevenue IS NULL OR QuotedRevenue = 0 THEN ISNULL(Revenue, 0) ELSE QuotedRevenue END,
                                 ActualRevenue = ISNULL(ActualRevenue, 0),
                                 IsOverdue = CASE
                                     WHEN CAST(ScheduledDate AS DATE) < CAST(GETDATE() AS DATE)
                                      AND ISNULL(PipelineStatus, Status) NOT IN ('Closed','Invoiced','Completed')
                                     THEN 1 ELSE 0 END
                             WHERE 1 = 1;");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='JobChecklistItems')
                CREATE TABLE JobChecklistItems (
                    ChecklistItemId INT IDENTITY(1,1) PRIMARY KEY,
                    JobId INT NOT NULL FOREIGN KEY REFERENCES Jobs(JobID),
                    ItemText NVARCHAR(200) NOT NULL,
                    IsCompleted BIT NOT NULL DEFAULT 0,
                    CompletedBy NVARCHAR(100) NULL,
                    CompletedDate DATETIME NULL,
                    SortOrder INT NOT NULL DEFAULT 0
                );");
                AddColumn(conn, "JobChecklistItems", "ItemText", "NVARCHAR(200) NOT NULL DEFAULT ''");
                AddColumn(conn, "JobChecklistItems", "IsCompleted", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "JobChecklistItems", "CompletedBy", "NVARCHAR(100) NULL");
                AddColumn(conn, "JobChecklistItems", "CompletedDate", "DATETIME NULL");
                AddColumn(conn, "JobChecklistItems", "SortOrder", "INT NOT NULL DEFAULT 0");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='JobPartsUsed')
                CREATE TABLE JobPartsUsed (
                    PartUsedId INT IDENTITY(1,1) PRIMARY KEY,
                    JobId INT NOT NULL FOREIGN KEY REFERENCES Jobs(JobID),
                    InventoryItemId INT NULL,
                    ItemDescription NVARCHAR(255) NOT NULL,
                    QuantityUsed DECIMAL(10,3) NOT NULL,
                    Unit NVARCHAR(30) NOT NULL DEFAULT 'Nos',
                    UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TotalCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                    IsFromInventory BIT NOT NULL DEFAULT 1,
                    StockStatus NVARCHAR(20) NULL
                );");
                AddColumn(conn, "JobPartsUsed", "InventoryItemId", "INT NULL");
                AddColumn(conn, "JobPartsUsed", "ItemDescription", "NVARCHAR(255) NOT NULL DEFAULT ''");
                AddColumn(conn, "JobPartsUsed", "QuantityUsed", "DECIMAL(10,3) NOT NULL DEFAULT 0");
                AddColumn(conn, "JobPartsUsed", "Unit", "NVARCHAR(30) NOT NULL DEFAULT 'Nos'");
                AddColumn(conn, "JobPartsUsed", "UnitCost", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "JobPartsUsed", "TotalCost", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "JobPartsUsed", "IsFromInventory", "BIT NOT NULL DEFAULT 1");
                AddColumn(conn, "JobPartsUsed", "StockStatus", "NVARCHAR(20) NULL");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='JobActivityLog')
                CREATE TABLE JobActivityLog (
                    ActivityId INT IDENTITY(1,1) PRIMARY KEY,
                    JobId INT NOT NULL FOREIGN KEY REFERENCES Jobs(JobID),
                    ActivityText NVARCHAR(500) NOT NULL,
                    PerformedBy NVARCHAR(100) NULL,
                    ActivityDate DATETIME NOT NULL DEFAULT GETDATE(),
                    ActivityType NVARCHAR(20) NOT NULL DEFAULT 'Info'
                );");
                AddColumn(conn, "JobActivityLog", "ActivityText", "NVARCHAR(500) NOT NULL DEFAULT ''");
                AddColumn(conn, "JobActivityLog", "PerformedBy", "NVARCHAR(100) NULL");
                AddColumn(conn, "JobActivityLog", "ActivityDate", "DATETIME NOT NULL DEFAULT GETDATE()");
                AddColumn(conn, "JobActivityLog", "ActivityType", "NVARCHAR(20) NOT NULL DEFAULT 'Info'");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='JobChecklistTemplates')
                CREATE TABLE JobChecklistTemplates (
                    TemplateId INT IDENTITY(1,1) PRIMARY KEY,
                    JobType NVARCHAR(50) NOT NULL,
                    ItemText NVARCHAR(200) NOT NULL,
                    SortOrder INT NOT NULL DEFAULT 0
                );");
                AddColumn(conn, "JobChecklistTemplates", "JobType", "NVARCHAR(50) NOT NULL DEFAULT 'General'");
                AddColumn(conn, "JobChecklistTemplates", "ItemText", "NVARCHAR(200) NOT NULL DEFAULT ''");
                AddColumn(conn, "JobChecklistTemplates", "SortOrder", "INT NOT NULL DEFAULT 0");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ServiceDeskIncidents')
                CREATE TABLE ServiceDeskIncidents (
                    IncidentId INT IDENTITY(1,1) PRIMARY KEY,
                    IncidentNumber NVARCHAR(50) NOT NULL UNIQUE,
                    ClientId INT NULL,
                    SiteId INT NULL,
                    AssignedEmployeeId INT NULL,
                    LinkedJobId INT NULL,
                    CallerName NVARCHAR(150) NULL,
                    CallerPhone NVARCHAR(50) NULL,
                    Category NVARCHAR(80) NOT NULL DEFAULT 'General',
                    EquipmentType NVARCHAR(80) NULL,
                    AssetSerialNumber NVARCHAR(100) NULL,
                    Priority NVARCHAR(30) NOT NULL DEFAULT 'Medium',
                    Status NVARCHAR(30) NOT NULL DEFAULT 'New',
                    ShortDescription NVARCHAR(250) NOT NULL,
                    Description NVARCHAR(MAX) NULL,
                    RootCause NVARCHAR(MAX) NULL,
                    ResolutionCode NVARCHAR(100) NULL,
                    OpenedAt DATETIME NOT NULL DEFAULT GETDATE(),
                    AssignedAt DATETIME NULL,
                    ResolvedAt DATETIME NULL,
                    ClosedAt DATETIME NULL,
                    SlaDueAt DATETIME NOT NULL,
                    SlaBreached BIT NOT NULL DEFAULT 0,
                    CreatedByName NVARCHAR(100) NULL,
                    ModifiedByName NVARCHAR(100) NULL,
                    ModifiedDate DATETIME NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ServiceDeskNotes')
                CREATE TABLE ServiceDeskNotes (
                    NoteId INT IDENTITY(1,1) PRIMARY KEY,
                    IncidentId INT NOT NULL FOREIGN KEY REFERENCES ServiceDeskIncidents(IncidentId),
                    NoteType NVARCHAR(40) NOT NULL DEFAULT 'Work note',
                    NoteText NVARCHAR(MAX) NOT NULL,
                    CreatedByName NVARCHAR(100) NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ConnectedMailAccounts')
                CREATE TABLE ConnectedMailAccounts (
                    AccountId INT IDENTITY(1,1) PRIMARY KEY,
                    UserId INT NOT NULL,
                    Provider NVARCHAR(30) NOT NULL,
                    EmailAddress NVARCHAR(255) NOT NULL,
                    DisplayName NVARCHAR(255) NULL,
                    AccessTokenEncrypted NVARCHAR(MAX) NULL,
                    RefreshTokenEncrypted NVARCHAR(MAX) NULL,
                    TokenExpiresAtUtc DATETIME NULL,
                    LastSyncUtc DATETIME NULL,
                    LastSyncStatus NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME NULL
                );");

                Exec(conn, @"IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes WHERE name = 'UX_ConnectedMailAccounts_User_Provider_Email'
                      AND object_id = OBJECT_ID('ConnectedMailAccounts'))
                CREATE UNIQUE INDEX UX_ConnectedMailAccounts_User_Provider_Email
                    ON ConnectedMailAccounts(UserId, Provider, EmailAddress);");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ServiceDeskEmails')
                CREATE TABLE ServiceDeskEmails (
                    EmailId INT IDENTITY(1,1) PRIMARY KEY,
                    AccountId INT NOT NULL FOREIGN KEY REFERENCES ConnectedMailAccounts(AccountId),
                    IncidentId INT NULL,
                    ProviderMessageId NVARCHAR(300) NOT NULL,
                    ThreadId NVARCHAR(300) NULL,
                    FromAddress NVARCHAR(255) NULL,
                    FromName NVARCHAR(255) NULL,
                    Subject NVARCHAR(500) NULL,
                    BodyPreview NVARCHAR(MAX) NULL,
                    ReceivedAtUtc DATETIME NULL,
                    SyncedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes WHERE name = 'UX_ServiceDeskEmails_Account_Message'
                      AND object_id = OBJECT_ID('ServiceDeskEmails'))
                CREATE UNIQUE INDEX UX_ServiceDeskEmails_Account_Message
                    ON ServiceDeskEmails(AccountId, ProviderMessageId);");

                AddColumn(conn, "ServiceDeskIncidents", "LinkedJobId", "INT NULL");
                AddColumn(conn, "ServiceDeskIncidents", "RootCause", "NVARCHAR(MAX) NULL");
                AddColumn(conn, "ServiceDeskIncidents", "ResolutionCode", "NVARCHAR(100) NULL");
                AddColumn(conn, "ServiceDeskIncidents", "SlaBreached", "BIT NOT NULL DEFAULT 0");

                Exec(conn, @"
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'PM Visit' AND ItemText = 'Filter cleaning completed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('PM Visit', 'Filter cleaning completed', 1);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'PM Visit' AND ItemText = 'Coil inspection done')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('PM Visit', 'Coil inspection done', 2);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'PM Visit' AND ItemText = 'Electrical panel checked')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('PM Visit', 'Electrical panel checked', 3);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'PM Visit' AND ItemText = 'Gas pressure verified')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('PM Visit', 'Gas pressure verified', 4);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'PM Visit' AND ItemText = 'Client sign-off obtained')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('PM Visit', 'Client sign-off obtained', 5);

                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Breakdown' AND ItemText = 'Problem diagnosed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Breakdown', 'Problem diagnosed', 1);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Breakdown' AND ItemText = 'Root cause identified')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Breakdown', 'Root cause identified', 2);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Breakdown' AND ItemText = 'Repair completed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Breakdown', 'Repair completed', 3);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Breakdown' AND ItemText = 'Test run performed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Breakdown', 'Test run performed', 4);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Breakdown' AND ItemText = 'Client sign-off obtained')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Breakdown', 'Client sign-off obtained', 5);

                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Installation' AND ItemText = 'Site survey done')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Installation', 'Site survey done', 1);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Installation' AND ItemText = 'Equipment installed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Installation', 'Equipment installed', 2);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Installation' AND ItemText = 'Electrical connections verified')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Installation', 'Electrical connections verified', 3);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Installation' AND ItemText = 'Test cooling confirmed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Installation', 'Test cooling confirmed', 4);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Installation' AND ItemText = 'Handover document signed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Installation', 'Handover document signed', 5);

                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Gas Charging' AND ItemText = 'Leak check done')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Gas Charging', 'Leak check done', 1);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Gas Charging' AND ItemText = 'Old gas recovered')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Gas Charging', 'Old gas recovered', 2);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Gas Charging' AND ItemText = 'New gas charged to correct pressure')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Gas Charging', 'New gas charged to correct pressure', 3);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Gas Charging' AND ItemText = 'System temperature verified')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Gas Charging', 'System temperature verified', 4);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'Gas Charging' AND ItemText = 'Client informed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('Gas Charging', 'Client informed', 5);

                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'AMC Visit' AND ItemText = 'Preventive maintenance checklist completed')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('AMC Visit', 'Preventive maintenance checklist completed', 1);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'AMC Visit' AND ItemText = 'Performance readings recorded')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('AMC Visit', 'Performance readings recorded', 2);
                    IF NOT EXISTS (SELECT 1 FROM JobChecklistTemplates WHERE JobType = 'AMC Visit' AND ItemText = 'Client service register updated')
                    INSERT INTO JobChecklistTemplates (JobType, ItemText, SortOrder) VALUES ('AMC Visit', 'Client service register updated', 3);
                ");

                // CompanySettings
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='CompanySettings')
                CREATE TABLE CompanySettings (
                    SettingKey   NVARCHAR(100) NOT NULL PRIMARY KEY,
                    SettingValue NVARCHAR(MAX) NULL,
                    UpdatedDate  DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='HsnSacMaster')
                CREATE TABLE HsnSacMaster (
                    MasterID          INT PRIMARY KEY IDENTITY(1,1),
                    CodeType          NVARCHAR(10) NOT NULL DEFAULT 'HSN',
                    Code              NVARCHAR(20) NOT NULL,
                    Description       NVARCHAR(255) NOT NULL,
                    BusinessCategory  NVARCHAR(100) NULL,
                    TaxRate           DECIMAL(5,2) NOT NULL DEFAULT 18,
                    CGSTRate          DECIMAL(5,2) NOT NULL DEFAULT 9,
                    SGSTRate          DECIMAL(5,2) NOT NULL DEFAULT 9,
                    IGSTRate          DECIMAL(5,2) NOT NULL DEFAULT 18,
                    Notes             NVARCHAR(500) NULL,
                    IsDefault         BIT NOT NULL DEFAULT 0,
                    IsActive          BIT NOT NULL DEFAULT 1,
                    UpdatedDate       DATETIME NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT UQ_HsnSacMaster_Code UNIQUE (CodeType, Code)
                );");

                // Insert default settings if empty
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM CompanySettings WHERE SettingKey='CompanyName')
                BEGIN
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyName',  'New Client');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyGST',   '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyPAN',   '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyPhone', '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyEmail', '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyAddress','');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('InvoicePrefix', 'INV');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('DefaultGST',    '18');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('DefaultPaymentTerms', '30');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('DefaultPlaceOfSupply', 'Maharashtra');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyShopLicense', '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyPFNumber', '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyESICNumber', '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('CompanyMSMENumber', '');
                    INSERT INTO CompanySettings (SettingKey,SettingValue) VALUES ('DefaultCertificationNote', 'I/We hereby certify that this tax invoice is accounted for in the turnover of sales and the due tax, if any, has been paid or shall be paid.');
                END;");
                EnsureSetting(conn, "CompanyGSTIN", "");
                EnsureSetting(conn, "CompanyTAN", "");
                EnsureSetting(conn, "CompanyState", "Maharashtra");
                EnsureSetting(conn, "GSTRegistrationType", "Regular");
                EnsureSetting(conn, "CurrencyCode", "INR");
                EnsureSetting(conn, "CurrencySymbol", "\u20B9");
                EnsureSetting(conn, "FinancialYearPattern", "01/04 - 31/03");
                EnsureSetting(conn, "AnnualTurnover", "0");
                EnsureSetting(conn, "EInvoiceThresholdAmount", "50000000");
                EnsureSetting(conn, "EInvoiceAutoEnabled", "0");
                EnsureSetting(conn, "OfficeLatitude", "19.1977617");
                EnsureSetting(conn, "OfficeLongitude", "72.9558786");
                EnsureSetting(conn, "DefaultMarkupPct", "25");
                EnsureSetting(conn, "AttendanceWorkWeek", "6");
                EnsureSetting(conn, "PayrollHistoricalImportCompleted", "0");

                Exec(conn, @"
                    IF EXISTS (SELECT 1 FROM CompanySettings WHERE SettingKey = 'OfficeLatitude')
                        UPDATE CompanySettings
                        SET SettingValue = '19.1977617',
                            UpdatedDate = GETDATE()
                        WHERE SettingKey = 'OfficeLatitude';
                    ELSE
                        INSERT INTO CompanySettings (SettingKey, SettingValue)
                        VALUES ('OfficeLatitude', '19.1977617');

                    IF EXISTS (SELECT 1 FROM CompanySettings WHERE SettingKey = 'OfficeLongitude')
                        UPDATE CompanySettings
                        SET SettingValue = '72.9558786',
                            UpdatedDate = GETDATE()
                        WHERE SettingKey = 'OfficeLongitude';
                    ELSE
                        INSERT INTO CompanySettings (SettingKey, SettingValue)
                        VALUES ('OfficeLongitude', '72.9558786');
                ");

                ApplyConfiguredTenantSettings(conn);

                Exec(conn, @"
                    IF NOT EXISTS (SELECT 1 FROM HsnSacMaster WHERE CodeType = 'SAC' AND Code = '998719')
                    INSERT INTO HsnSacMaster
                        (CodeType, Code, Description, BusinessCategory, TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive)
                    VALUES
                        ('SAC', '998719', 'Maintenance and repair services of other machinery and equipments', 'HVAC Maintenance Services', 18, 9, 9, 18, 'Default HVAC maintenance service classification.', 1, 1);

                    IF NOT EXISTS (SELECT 1 FROM HsnSacMaster WHERE CodeType = 'SAC' AND Code = '995463')
                    INSERT INTO HsnSacMaster
                        (CodeType, Code, Description, BusinessCategory, TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive)
                    VALUES
                        ('SAC', '995463', 'Installation services of heating, ventilation and air-conditioning equipment', 'HVAC Installation Works Contract', 18, 9, 9, 18, 'Default HVAC installation works contract classification.', 1, 1);

                    IF NOT EXISTS (SELECT 1 FROM HsnSacMaster WHERE CodeType = 'HSN' AND Code = '8415')
                    INSERT INTO HsnSacMaster
                        (CodeType, Code, Description, BusinessCategory, TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive)
                    VALUES
                        ('HSN', '8415', 'Air conditioning machines', 'Air Conditioning Equipment', 18, 9, 9, 18, 'Default goods supply classification for AC units and HVAC machines.', 1, 1);

                    IF NOT EXISTS (SELECT 1 FROM HsnSacMaster WHERE CodeType = 'HSN' AND Code = '7411')
                    INSERT INTO HsnSacMaster
                        (CodeType, Code, Description, BusinessCategory, TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive)
                    VALUES
                        ('HSN', '7411', 'Copper tubes, pipes and hollow profiles', 'Copper Pipe', 18, 9, 9, 18, 'Default goods classification for copper pipe used in HVAC jobs.', 1, 1);

                    IF NOT EXISTS (SELECT 1 FROM HsnSacMaster WHERE CodeType = 'HSN' AND Code = '8544')
                    INSERT INTO HsnSacMaster
                        (CodeType, Code, Description, BusinessCategory, TaxRate, CGSTRate, SGSTRate, IGSTRate, Notes, IsDefault, IsActive)
                    VALUES
                        ('HSN', '8544', 'Insulated wire, cable and other electric conductors', 'Electrical Cable', 18, 9, 9, 18, 'Default goods classification for HVAC electrical cabling.', 1, 1);");

                // PurchaseOrders â€” add RelatedContractID column for cost tracking
                AddColumn(conn, "Vendors", "DefaultCreditDays", "INT NOT NULL DEFAULT 30");
                AddColumn(conn, "Vendors", "WhatsAppNumber", "NVARCHAR(20) NULL");
                AddColumn(conn, "Vendors", "VendorType", "NVARCHAR(30) NULL DEFAULT 'Supplier'");
                AddColumn(conn, "Vendors", "IsSupplier", "BIT NOT NULL DEFAULT 1");
                AddColumn(conn, "Vendors", "IsServiceVendor", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "Vendors", "MSMERegistered", "NVARCHAR(20) NULL DEFAULT 'No'");
                AddColumn(conn, "Vendors", "MSMENumber", "NVARCHAR(50) NULL");
                AddColumn(conn, "Vendors", "GSTRegistrationType", "NVARCHAR(20) NULL DEFAULT 'Regular'");
                AddColumn(conn, "Vendors", "TDSApplicable", "BIT NULL DEFAULT 0");
                AddColumn(conn, "Vendors", "TDSSection", "NVARCHAR(20) NULL");
                AddColumn(conn, "Vendors", "TDSRate", "DECIMAL(5,2) NULL DEFAULT 0");
                AddColumn(conn, "Vendors", "RCMApplicable", "BIT NULL DEFAULT 0");
                AddColumn(conn, "Vendors", "BankAccountNumber", "NVARCHAR(30) NULL");
                AddColumn(conn, "Vendors", "BankIFSC", "NVARCHAR(11) NULL");
                AddColumn(conn, "Vendors", "BankAccountName", "NVARCHAR(100) NULL");
                AddColumn(conn, "Vendors", "BankName", "NVARCHAR(100) NULL");
                AddColumn(conn, "Vendors", "PreferredPaymentMode", "NVARCHAR(20) NULL");
                AddColumn(conn, "Vendors", "StateCode", "NVARCHAR(5) NULL");
                AddColumn(conn, "Vendors", "Notes", "NVARCHAR(1000) NULL");
                AddColumn(conn, "Vendors", "IsArchived", "BIT NULL DEFAULT 0");
                AddColumn(conn, "Vendors", "SpecialisationTags", "NVARCHAR(500) NULL");
                AddColumn(conn, "Vendors", "TotalPurchased", "DECIMAL(18,2) NULL DEFAULT 0");
                AddColumn(conn, "PurchaseOrders", "RelatedContractID", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "ClientID", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "SiteID", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "RecommendedByBidID", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "ComparisonNotes", "NVARCHAR(MAX) NULL");
                AddColumn(conn, "PurchaseOrders", "PayByDate", "DATETIME NULL");
                AddColumn(conn, "PurchaseOrders", "VendorInvoiceNumber", "NVARCHAR(100) NULL");
                AddColumn(conn, "PurchaseOrders", "LinkedToType", "NVARCHAR(30) NULL");
                AddColumn(conn, "PurchaseOrders", "LinkedToId", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "PaymentReference", "NVARCHAR(100) NULL");
                AddColumn(conn, "PurchaseOrders", "DeliveryMode", "NVARCHAR(30) NOT NULL DEFAULT 'TechPickup'");
                AddColumn(conn, "PurchaseOrders", "AssignedTechnicianId", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "AssignedTechnicianName", "NVARCHAR(100) NULL");
                AddColumn(conn, "PurchaseOrders", "DeliveryAddress", "NVARCHAR(500) NULL");
                AddColumn(conn, "PurchaseOrders", "AddToClientInvoice", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseOrders", "PendingChargeCreated", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseOrders", "ReceiptImagePath", "NVARCHAR(500) NULL");
                AddColumn(conn, "PurchaseOrders", "PriceVarianceFlag", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseOrders", "CreatedByUserId", "INT NULL");
                AddColumn(conn, "PurchaseOrders", "CreatedByName", "NVARCHAR(100) NULL");
                AddColumn(conn, "PurchaseOrders", "CreatedByDate", "DATETIME NOT NULL DEFAULT GETDATE()");
                AddColumn(conn, "PurchaseLineItems", "InventoryItemId", "INT NULL");
                AddColumn(conn, "PurchaseLineItems", "HsnSacCode", "NVARCHAR(20) NULL");
                AddColumn(conn, "PurchaseLineItems", "GSTRate", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseLineItems", "CGSTRate", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseLineItems", "SGSTRate", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseLineItems", "IGSTRate", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseLineItems", "JobLink", "NVARCHAR(50) NOT NULL DEFAULT 'General'");
                AddColumn(conn, "PurchaseLineItems", "LinkedWorkOrderId", "INT NULL");
                AddColumn(conn, "PurchaseLineItems", "LinkedWorkOrderName", "NVARCHAR(200) NULL");
                AddColumn(conn, "PurchaseLineItems", "PriceVariance", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "PurchaseLineItems", "UOM", "NVARCHAR(30) NOT NULL DEFAULT 'Nos'");
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='VendorAdvancePayments')
                CREATE TABLE VendorAdvancePayments (
                    AdvancePaymentId INT IDENTITY(1,1) PRIMARY KEY,
                    VendorId INT NOT NULL FOREIGN KEY REFERENCES Vendors(VendorID),
                    POID INT NULL FOREIGN KEY REFERENCES PurchaseOrders(POID),
                    TransactionType NVARCHAR(30) NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    AppliedAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TransactionDate DATETIME NOT NULL,
                    PaymentMode NVARCHAR(50) NULL,
                    ReferenceNumber NVARCHAR(100) NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedByUserId INT NULL,
                    CreatedByName NVARCHAR(100) NULL,
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
                );");
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PendingCharges')
                CREATE TABLE PendingCharges (
                    PendingChargeId INT PRIMARY KEY IDENTITY(1,1),
                    WorkOrderId INT NOT NULL FOREIGN KEY REFERENCES Jobs(JobID),
                    Description NVARCHAR(500) NOT NULL,
                    Qty DECIMAL(10,2) NOT NULL DEFAULT 0,
                    Rate DECIMAL(12,2) NOT NULL DEFAULT 0,
                    HsnSac NVARCHAR(20) NULL,
                    GSTRate DECIMAL(5,2) NOT NULL DEFAULT 0,
                    SourcePoId INT NOT NULL FOREIGN KEY REFERENCES PurchaseOrders(POID),
                    CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                    IsBilled BIT NOT NULL DEFAULT 0
                );");
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SupplierItemPrices')
                CREATE TABLE SupplierItemPrices (
                    PriceID       INT PRIMARY KEY IDENTITY(1,1),
                    VendorID      INT NOT NULL FOREIGN KEY REFERENCES Vendors(VendorID),
                    ItemName      NVARCHAR(255) NOT NULL,
                    Category      NVARCHAR(100) NULL,
                    Unit          NVARCHAR(50) NULL,
                    Rate          DECIMAL(12,2) NOT NULL DEFAULT 0,
                    Source        NVARCHAR(100) NULL,
                    EffectiveDate DATETIME NOT NULL DEFAULT GETDATE()
                );");
                Exec(conn, @"UPDATE Vendors SET DefaultCreditDays = 30 WHERE DefaultCreditDays IS NULL OR DefaultCreditDays <= 0;");
                Exec(conn, @"UPDATE Vendors SET VendorType = 'Supplier' WHERE VendorType IS NULL OR LTRIM(RTRIM(VendorType)) = '';");
                Exec(conn, @"
                    UPDATE Vendors
                    SET IsSupplier = 1
                    WHERE VendorID IN (SELECT VendorID FROM PurchaseOrders)
                       OR VendorID IN (SELECT VendorID FROM StockItems WHERE VendorID IS NOT NULL)
                       OR VendorID IN (SELECT VendorID FROM SupplierItemPrices)
                       OR VendorType IN ('Supplier', 'Distributor', 'Trader');");
                Exec(conn, @"
                    UPDATE Vendors
                    SET IsServiceVendor = 1
                    WHERE VendorType IN ('Vendor', 'Subcontractor', 'Labour', 'Service Provider');");
                Exec(conn, @"UPDATE Vendors SET MSMERegistered = 'No' WHERE MSMERegistered IS NULL OR LTRIM(RTRIM(MSMERegistered)) = '';");
                Exec(conn, @"UPDATE Vendors SET GSTRegistrationType = 'Regular' WHERE GSTRegistrationType IS NULL OR LTRIM(RTRIM(GSTRegistrationType)) = '';");
                Exec(conn, @"UPDATE Vendors SET TDSRate = 0 WHERE TDSRate IS NULL;");
                Exec(conn, @"UPDATE Vendors SET RCMApplicable = CASE WHEN GSTRegistrationType = 'Unregistered' THEN 1 ELSE ISNULL(RCMApplicable,0) END;");
                Exec(conn, @"UPDATE Vendors SET IsArchived = 0 WHERE IsArchived IS NULL;");
                Exec(conn, @"UPDATE v
                             SET TotalPurchased = ISNULL(po.TotalPurchased, 0)
                             FROM Vendors v
                             OUTER APPLY (
                                 SELECT SUM(ISNULL(TotalAmount, 0)) AS TotalPurchased
                                 FROM PurchaseOrders p
                                 WHERE p.VendorID = v.VendorID
                             ) po;");
                Exec(conn, @"UPDATE p
                             SET PayByDate = DATEADD(day, ISNULL(NULLIF(v.DefaultCreditDays, 0), 30), p.PODate)
                             FROM PurchaseOrders p
                             LEFT JOIN Vendors v ON p.VendorID = v.VendorID
                             WHERE p.PayByDate IS NULL;");
                Exec(conn, @"UPDATE p
                             SET PayByDate = DATEADD(day, ISNULL(NULLIF(v.DefaultCreditDays, 0), 30), p.PODate)
                             FROM PurchaseOrders p
                             LEFT JOIN Vendors v ON p.VendorID = v.VendorID
                             WHERE p.PayByDate IS NOT NULL
                               AND (YEAR(p.PayByDate) < 2020 OR YEAR(p.PayByDate) < YEAR(p.PODate) - 1);");
                Exec(conn, @"IF OBJECT_ID('PurchaseOrders', 'U') IS NOT NULL
                             UPDATE PurchaseOrders
                             SET PaidAmount = TotalAmount
                             WHERE ISNULL(Status,'') IN ('Fully Received','Received','Paid','Closed')
                               AND ISNULL(PaidAmount,0) < ISNULL(TotalAmount,0);");

                // StockItems â€” ensure LastUpdated column exists (may already be there as LastUpdated)
                AddColumn(conn, "StockItems", "LastUpdated", "DATETIME NOT NULL DEFAULT GETDATE()");
                AddColumn(conn, "Quotations", "QuotationNumber", "NVARCHAR(50) NULL");
                AddColumn(conn, "Quotations", "ClientID", "INT NULL");
                AddColumn(conn, "Quotations", "SiteID", "INT NULL");
                AddColumn(conn, "Quotations", "RequirementCategory", "NVARCHAR(100) NULL");
                AddColumn(conn, "Quotations", "ItemName", "NVARCHAR(255) NULL");
                AddColumn(conn, "Quotations", "RequiredQuantity", "DECIMAL(10,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "Unit", "NVARCHAR(50) NULL");
                AddColumn(conn, "Quotations", "RequiredByDate", "DATETIME NULL");
                AddColumn(conn, "Quotations", "InventoryAvailable", "DECIMAL(10,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "ShortfallQuantity", "DECIMAL(10,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "EstimatedInternalRate", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "EstimatedSupplierRate", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "EstimatedInternalCost", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "EstimatedExternalCost", "DECIMAL(12,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "RecommendedVendorID", "INT NULL");
                AddColumn(conn, "Quotations", "ComparisonSummary", "NVARCHAR(MAX) NULL");
                AddColumn(conn, "Quotations", "AnalysisStatus", "NVARCHAR(100) NULL");
                AddColumn(conn, "Quotations", "IsMultiLine", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "TotalTaxableValue", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "TotalGSTAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "TotalWithGST", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "AverageMarginPct", "DECIMAL(5,2) NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "TemplateId", "INT NULL");
                AddColumn(conn, "Quotations", "ClientPriceMemoryApplied", "BIT NOT NULL DEFAULT 0");
                AddColumn(conn, "Quotations", "SuggestionsJson", "NVARCHAR(MAX) NULL");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='QuotationLineItems')
                CREATE TABLE QuotationLineItems (
                    LineItemId        INT IDENTITY(1,1) PRIMARY KEY,
                    TenderBidId       INT NOT NULL FOREIGN KEY REFERENCES Quotations(BidID) ON DELETE CASCADE,
                    SortOrder         INT NOT NULL DEFAULT 0,
                    Category          NVARCHAR(100) NULL,
                    InventoryItemId   INT NULL,
                    ItemDescription   NVARCHAR(255) NOT NULL,
                    Quantity          DECIMAL(10,3) NOT NULL DEFAULT 1,
                    Unit              NVARCHAR(30) NOT NULL DEFAULT 'Nos',
                    HsnSacCode        NVARCHAR(20) NULL,
                    GSTRatePct        DECIMAL(5,2) NOT NULL DEFAULT 18.00,
                    BestSupplierId    INT NULL,
                    BestSupplierName  NVARCHAR(100) NULL,
                    CostPerUnit       DECIMAL(18,2) NOT NULL DEFAULT 0,
                    SellPricePerUnit  DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TaxableLineTotal  DECIMAL(18,2) NOT NULL DEFAULT 0,
                    GSTAmount         DECIMAL(18,2) NOT NULL DEFAULT 0,
                    MarginPct         DECIMAL(5,2) NOT NULL DEFAULT 0,
                    StockAvailable    DECIMAL(10,3) NOT NULL DEFAULT 0,
                    Shortfall         DECIMAL(10,3) NOT NULL DEFAULT 0,
                    IsInternalLabour  BIT NOT NULL DEFAULT 0,
                    AnalysisStatus    NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    AnalysisNotes     NVARCHAR(500) NULL,
                    IsSellPriceManual BIT NOT NULL DEFAULT 0,
                    CreatedDate       DATETIME NOT NULL DEFAULT GETDATE(),
                    ModifiedDate      DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='QuoteTemplates')
                CREATE TABLE QuoteTemplates (
                    TemplateId        INT IDENTITY(1,1) PRIMARY KEY,
                    TemplateName      NVARCHAR(100) NOT NULL,
                    Description       NVARCHAR(255) NULL,
                    IsActive          BIT NOT NULL DEFAULT 1,
                    CreatedDate       DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='QuoteTemplateItems')
                CREATE TABLE QuoteTemplateItems (
                    TemplateItemId    INT IDENTITY(1,1) PRIMARY KEY,
                    TemplateId        INT NOT NULL FOREIGN KEY REFERENCES QuoteTemplates(TemplateId) ON DELETE CASCADE,
                    SortOrder         INT NOT NULL DEFAULT 0,
                    Category          NVARCHAR(100) NULL,
                    ItemDescription   NVARCHAR(255) NOT NULL,
                    DefaultQuantity   DECIMAL(10,3) NOT NULL DEFAULT 1,
                    Unit              NVARCHAR(30) NOT NULL DEFAULT 'Nos',
                    HsnSacCode        NVARCHAR(20) NULL,
                    GSTRatePct        DECIMAL(5,2) NOT NULL DEFAULT 18.00,
                    DefaultMarkupPct  DECIMAL(5,2) NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientPriceMemory')
                CREATE TABLE ClientPriceMemory (
                    MemoryId          INT IDENTITY(1,1) PRIMARY KEY,
                    ClientId          INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    ItemDescription   NVARCHAR(255) NOT NULL,
                    LastQuotedPrice   DECIMAL(18,2) NOT NULL,
                    LastQuoteDate     DATETIME NOT NULL,
                    WasAccepted       BIT NOT NULL DEFAULT 0,
                    QuotationNumber   NVARCHAR(50) NULL,
                    CreatedDate       DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientAssets')
                CREATE TABLE ClientAssets (
                    AssetId              INT IDENTITY(1,1) PRIMARY KEY,
                    ClientId             INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    SiteId               INT NULL FOREIGN KEY REFERENCES ClientSites(SiteID),
                    ContractId           INT NULL FOREIGN KEY REFERENCES AMCContracts(ContractID),
                    AssetTag             NVARCHAR(80) NULL,
                    EquipmentType        NVARCHAR(100) NOT NULL,
                    Brand                NVARCHAR(100) NULL,
                    ModelNumber          NVARCHAR(100) NULL,
                    SerialNumber         NVARCHAR(120) NULL,
                    Capacity             NVARCHAR(80) NULL,
                    LocationDetail       NVARCHAR(255) NULL,
                    InstallDate          DATETIME NULL,
                    WarrantyExpiry       DATETIME NULL,
                    IsAmcCovered         BIT NOT NULL DEFAULT 0,
                    MaintenanceFrequency NVARCHAR(50) NULL,
                    Notes                NVARCHAR(MAX) NULL,
                    IsActive             BIT NOT NULL DEFAULT 1,
                    CreatedDate          DATETIME NOT NULL DEFAULT GETDATE(),
                    ModifiedDate         DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientDocuments')
                CREATE TABLE ClientDocuments (
                    DocumentId       INT IDENTITY(1,1) PRIMARY KEY,
                    ClientId         INT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    SiteId           INT NULL FOREIGN KEY REFERENCES ClientSites(SiteID),
                    AssetId          INT NULL FOREIGN KEY REFERENCES ClientAssets(AssetId),
                    ContractId       INT NULL FOREIGN KEY REFERENCES AMCContracts(ContractID),
                    DocumentType     NVARCHAR(80) NULL,
                    Title            NVARCHAR(180) NOT NULL,
                    FilePath         NVARCHAR(500) NOT NULL,
                    OriginalFileName NVARCHAR(255) NULL,
                    ExpiryDate       DATETIME NULL,
                    Notes            NVARCHAR(MAX) NULL,
                    UploadedDate     DATETIME NOT NULL DEFAULT GETDATE(),
                    UploadedBy       NVARCHAR(100) NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ServiceRateCards')
                CREATE TABLE ServiceRateCards (
                    RateId          INT IDENTITY(1,1) PRIMARY KEY,
                    ClientId        INT NULL FOREIGN KEY REFERENCES B2BClients(ClientID),
                    Category        NVARCHAR(80) NULL,
                    ServiceName     NVARCHAR(180) NOT NULL,
                    Unit            NVARCHAR(40) NOT NULL DEFAULT 'Job',
                    Rate            DECIMAL(18,2) NOT NULL DEFAULT 0,
                    GstPercent      DECIMAL(5,2) NOT NULL DEFAULT 18,
                    IsEmergencyRate BIT NOT NULL DEFAULT 0,
                    EffectiveFrom   DATETIME NOT NULL DEFAULT GETDATE(),
                    Notes           NVARCHAR(MAX) NULL,
                    IsActive        BIT NOT NULL DEFAULT 1
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PrivateServerConnections')
                CREATE TABLE PrivateServerConnections (
                    ConnectionId    INT IDENTITY(1,1) PRIMARY KEY,
                    ConnectionName  NVARCHAR(120) NOT NULL,
                    ServerType      NVARCHAR(50) NOT NULL,
                    Host            NVARCHAR(255) NULL,
                    Port            INT NULL,
                    DatabaseName    NVARCHAR(120) NULL,
                    ApiBaseUrl      NVARCHAR(500) NULL,
                    Username        NVARCHAR(120) NULL,
                    EncryptedSecret NVARCHAR(MAX) NULL,
                    SyncDirection   NVARCHAR(40) NULL,
                    LastSyncStatus  NVARCHAR(120) NULL,
                    LastSyncDate    DATETIME NULL,
                    IsActive        BIT NOT NULL DEFAULT 1,
                    CreatedDate     DATETIME NOT NULL DEFAULT GETDATE(),
                    ModifiedDate    DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='DataImportBatches')
                CREATE TABLE DataImportBatches (
                    BatchId     INT IDENTITY(1,1) PRIMARY KEY,
                    ImportType  NVARCHAR(80) NOT NULL,
                    SourceFile  NVARCHAR(500) NULL,
                    Status      NVARCHAR(40) NOT NULL DEFAULT 'Pending',
                    TotalRows   INT NOT NULL DEFAULT 0,
                    SuccessRows INT NOT NULL DEFAULT 0,
                    ErrorRows   INT NOT NULL DEFAULT 0,
                    StartedAt   DATETIME NOT NULL DEFAULT GETDATE(),
                    CompletedAt DATETIME NULL,
                    ImportedBy  NVARCHAR(100) NULL,
                    Notes       NVARCHAR(MAX) NULL
                );");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='DataImportErrors')
                CREATE TABLE DataImportErrors (
                    ErrorId      INT IDENTITY(1,1) PRIMARY KEY,
                    BatchId      INT NOT NULL FOREIGN KEY REFERENCES DataImportBatches(BatchId) ON DELETE CASCADE,
                    RowNumber    INT NOT NULL DEFAULT 0,
                    ColumnName   NVARCHAR(120) NULL,
                    ErrorMessage NVARCHAR(500) NOT NULL,
                    RawValue     NVARCHAR(MAX) NULL
                );");

                Exec(conn, @"
                    IF NOT EXISTS (SELECT 1 FROM QuoteTemplates WHERE TemplateName = '1.5TR Split AC Supply & Install')
                    BEGIN
                        INSERT INTO QuoteTemplates (TemplateName, Description) VALUES
                            ('1.5TR Split AC Supply & Install', 'Standard split AC supply and installation pack.');
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 1, 'AC Unit', 'Split AC 1.5TR', 1, 'Nos', '8415', 18, NULL FROM QuoteTemplates WHERE TemplateName = '1.5TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 2, 'Copper', 'Copper Pipe 3/4 inch', 10, 'Mtr', '7411', 18, NULL FROM QuoteTemplates WHERE TemplateName = '1.5TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 3, 'Drain', 'Drain Pipe', 5, 'Mtr', '998519', 18, NULL FROM QuoteTemplates WHERE TemplateName = '1.5TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 4, 'Electrical', 'Electrical Cable', 10, 'Mtr', '8544', 18, NULL FROM QuoteTemplates WHERE TemplateName = '1.5TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 5, 'Labour', 'Installation Labour', 1, 'Job', '998519', 18, 30 FROM QuoteTemplates WHERE TemplateName = '1.5TR Split AC Supply & Install';
                    END;

                    IF NOT EXISTS (SELECT 1 FROM QuoteTemplates WHERE TemplateName = '2TR Split AC Supply & Install')
                    BEGIN
                        INSERT INTO QuoteTemplates (TemplateName, Description) VALUES
                            ('2TR Split AC Supply & Install', 'Higher tonnage split AC package with extended copper run.');
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 1, 'AC Unit', 'Split AC 2TR', 1, 'Nos', '8415', 18, NULL FROM QuoteTemplates WHERE TemplateName = '2TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 2, 'Copper', 'Copper Pipe 3/4 inch', 15, 'Mtr', '7411', 18, NULL FROM QuoteTemplates WHERE TemplateName = '2TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 3, 'Drain', 'Drain Pipe', 5, 'Mtr', '998519', 18, NULL FROM QuoteTemplates WHERE TemplateName = '2TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 4, 'Electrical', 'Electrical Cable', 12, 'Mtr', '8544', 18, NULL FROM QuoteTemplates WHERE TemplateName = '2TR Split AC Supply & Install';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 5, 'Labour', 'Installation Labour', 1, 'Job', '998519', 18, 30 FROM QuoteTemplates WHERE TemplateName = '2TR Split AC Supply & Install';
                    END;

                    IF NOT EXISTS (SELECT 1 FROM QuoteTemplates WHERE TemplateName = 'AMC Quotation â€“ Comprehensive')
                    BEGIN
                        INSERT INTO QuoteTemplates (TemplateName, Description) VALUES
                            (N'AMC Quotation â€“ Comprehensive', N'Comprehensive quarterly HVAC AMC quotation.');
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 1, 'Service', 'PM Visit Q1', 1, 'Job', '998519', 18, 25 FROM QuoteTemplates WHERE TemplateName = N'AMC Quotation â€“ Comprehensive';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 2, 'Service', 'PM Visit Q2', 1, 'Job', '998519', 18, 25 FROM QuoteTemplates WHERE TemplateName = N'AMC Quotation â€“ Comprehensive';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 3, 'Service', 'PM Visit Q3', 1, 'Job', '998519', 18, 25 FROM QuoteTemplates WHERE TemplateName = N'AMC Quotation â€“ Comprehensive';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 4, 'Service', 'PM Visit Q4', 1, 'Job', '998519', 18, 25 FROM QuoteTemplates WHERE TemplateName = N'AMC Quotation â€“ Comprehensive';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 5, 'Labour', 'Emergency Call Labour', 1, 'Job', '998519', 18, 30 FROM QuoteTemplates WHERE TemplateName = N'AMC Quotation â€“ Comprehensive';
                    END;

                    IF NOT EXISTS (SELECT 1 FROM QuoteTemplates WHERE TemplateName = 'Gas Charging â€“ R32')
                    BEGIN
                        INSERT INTO QuoteTemplates (TemplateName, Description) VALUES
                            (N'Gas Charging â€“ R32', N'Gas charging package for R32 systems.');
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 1, 'Labour', 'Gas Charging Labour', 1, 'Job', '998519', 18, 30 FROM QuoteTemplates WHERE TemplateName = N'Gas Charging â€“ R32';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 2, 'Material', 'R32 Refrigerant', 1, 'Kg', '8415', 18, NULL FROM QuoteTemplates WHERE TemplateName = N'Gas Charging â€“ R32';
                    END;

                    IF NOT EXISTS (SELECT 1 FROM QuoteTemplates WHERE TemplateName = 'Duct Cleaning â€“ 10 Units')
                    BEGIN
                        INSERT INTO QuoteTemplates (TemplateName, Description) VALUES
                            (N'Duct Cleaning â€“ 10 Units', N'Duct and filter cleaning package for 10 units.');
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 1, 'Labour', 'Duct Cleaning Labour', 10, 'Units', '998519', 18, 25 FROM QuoteTemplates WHERE TemplateName = N'Duct Cleaning â€“ 10 Units';
                        INSERT INTO QuoteTemplateItems (TemplateId, SortOrder, Category, ItemDescription, DefaultQuantity, Unit, HsnSacCode, GSTRatePct, DefaultMarkupPct)
                        SELECT TemplateId, 2, 'Service', 'Filter Cleaning', 10, 'Units', '998519', 18, 25 FROM QuoteTemplates WHERE TemplateName = N'Duct Cleaning â€“ 10 Units';
                    END;");

                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='SupplierItemPrices')
                CREATE TABLE SupplierItemPrices (
                    PriceID       INT PRIMARY KEY IDENTITY(1,1),
                    VendorID      INT NOT NULL FOREIGN KEY REFERENCES Vendors(VendorID),
                    ItemName      NVARCHAR(255) NOT NULL,
                    Category      NVARCHAR(100) NULL,
                    Unit          NVARCHAR(50) NULL,
                    Rate          DECIMAL(12,2) NOT NULL DEFAULT 0,
                    Source        NVARCHAR(100) NULL,
                    EffectiveDate DATETIME NOT NULL DEFAULT GETDATE()
                );");

                Exec(conn, @"UPDATE Invoices SET BalanceDue = TotalAmount - PaidAmount
                             WHERE BalanceDue = 0 AND TotalAmount > 0;");

                Exec(conn, @"UPDATE i SET i.ClientID = c.ClientID
                             FROM Invoices i
                             JOIN AMCContracts c ON i.ContractID = c.ContractID
                             WHERE i.ClientID IS NULL;");
                Exec(conn, @"UPDATE i SET i.SiteID = c.SiteID
                             FROM Invoices i
                             JOIN AMCContracts c ON i.ContractID = c.ContractID
                             WHERE i.SiteID IS NULL;");
                Exec(conn, @"UPDATE t
                             SET t.ClientID = c.ClientID,
                                 t.ClientName = c.CompanyName
                             FROM Quotations t
                             JOIN B2BClients c ON t.ClientName = c.CompanyName
                             WHERE t.ClientID IS NULL;");

                // ClientContacts â€” multi-contact support per client
                Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ClientContacts')
                CREATE TABLE ClientContacts (
                    ContactID    INT PRIMARY KEY IDENTITY(1,1),
                    ClientID     INT NOT NULL FOREIGN KEY REFERENCES B2BClients(ClientID) ON DELETE CASCADE,
                    ContactName  NVARCHAR(255) NOT NULL,
                    Role         NVARCHAR(100) NULL,
                    Phone        NVARCHAR(30)  NULL,
                    Email        NVARCHAR(255) NULL,
                    IsPrimary    BIT NOT NULL DEFAULT 0,
                    Notes        NVARCHAR(500) NULL,
                    CreatedDate  DATETIME NOT NULL DEFAULT GETDATE()
                );");

                EnsureAuthSchema(conn);
                EnsureLicenseSchema(conn);
                EnsureTallyIntegrationSchema(conn);
                EnsureAuditMetadataColumns(conn);
                SeedPayrollReferenceData(conn);
                SeedSecurityData(conn);
                ApplyPurchaseOrderFreshStartReset(conn);
                ApplyDevelopmentTestDataCleanup(conn);
            }
        }

        private string BuildConnectionStringForDatabase(string database)
        {
            if (!string.IsNullOrWhiteSpace(_connectionString))
            {
                var builder = new SqlConnectionStringBuilder(_connectionString)
                {
                    InitialCatalog = database
                };
                return DatabaseConnectionFactory.NormalizeConnectionString(builder.ConnectionString);
            }

            return BuildConnectionString(_resolvedServer, database);
        }

        // Real MSE seed data
        public bool IsNormalStartupReady()
        {
            try
            {
                using (SqlConnection conn = GetConnection())
                {
                    DatabaseConnectionFactory.Open(conn, "DatabaseManager.IsNormalStartupReady");
                    string[] requiredTables =
                    {
                        "B2BClients", "ClientSites", "AMCContracts", "Vendors", "PurchaseOrders",
                        "StockItems", "Employees", "Jobs", "JobChecklistItems", "JobPartsUsed",
                        "JobActivityLog", "JobChecklistTemplates", "InvoiceTemplates", "SupplierItemPrices",
                        "RolePermissions", "AppUsers", "ClientAssets", "ClientDocuments",
                        "ServiceRateCards", "PrivateServerConnections", "DataImportBatches", "DataImportErrors",
                        "LicenseState", "LicenseEvents", "ActivatedDevices", "FeatureEntitlements"
                    };

                    return IsNormalStartupReady(conn, requiredTables);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IsNormalStartupReady error: " + ex.Message);
                return false;
            }
        }

        public bool IsSampleDataReady()
        {
            try
            {
                using (SqlConnection conn = GetConnection())
                {
                    DatabaseConnectionFactory.Open(conn, "DatabaseManager.IsSampleDataReady");
                    return IsSampleDataReady(conn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IsSampleDataReady error: " + ex.Message);
                return false;
            }
        }

        private bool IsSampleDataReady(SqlConnection conn)
        {
            string sql = @"
SELECT CASE WHEN
    (SELECT COUNT_BIG(1) FROM B2BClients) > 0
    AND (SELECT COUNT_BIG(1) FROM ClientSites) > 0
    AND (SELECT COUNT_BIG(1) FROM AMCContracts) > 0
    AND (SELECT COUNT_BIG(1) FROM Vendors) >= 25
    AND (SELECT COUNT_BIG(1) FROM PurchaseOrders) >= 25
    AND (SELECT COUNT_BIG(1) FROM StockItems) >= 50
    AND (SELECT COUNT_BIG(1) FROM Employees) >= 20
    AND (SELECT COUNT_BIG(1) FROM Jobs) > 0
    AND (SELECT COUNT_BIG(1) FROM InvoiceTemplates) > 0
    AND (SELECT COUNT_BIG(1) FROM SupplierItemPrices) > 0
THEN 1 ELSE 0 END";
            return ExecuteReadyScalar(conn, sql);
        }

        private bool IsNormalStartupReady(SqlConnection conn, string[] requiredTables)
        {
            string tableNames = string.Join(",", requiredTables.Select(t => "N'" + t.Replace("'", "''") + "'"));
            if (!IsDemoDataEnabled())
            {
                string tenantReadySql = @"
SELECT CASE WHEN
    (SELECT COUNT(DISTINCT name) FROM sys.tables WHERE name IN (" + tableNames + @")) = " + requiredTables.Length + @"
    AND EXISTS (SELECT 1 FROM RolePermissions WHERE ModuleKey = N'MasterData')
    AND COL_LENGTH('Jobs', 'JobTitle') IS NOT NULL
    AND COL_LENGTH('Jobs', 'PipelineStatus') IS NOT NULL
    AND COL_LENGTH('Jobs', 'QuotedRevenue') IS NOT NULL
    AND COL_LENGTH('Jobs', 'ActualRevenue') IS NOT NULL
    AND COL_LENGTH('Jobs', 'ClosedDate') IS NOT NULL
    AND COL_LENGTH('Jobs', 'InvoiceId') IS NOT NULL
    AND COL_LENGTH('ClientSites', 'TravelRateINR') IS NOT NULL
    AND COL_LENGTH('JobChecklistItems', 'CompletedBy') IS NOT NULL
    AND COL_LENGTH('JobPartsUsed', 'TotalCost') IS NOT NULL
    AND COL_LENGTH('JobActivityLog', 'ActivityType') IS NOT NULL
    AND EXISTS (SELECT 1 FROM AppUsers)
THEN 1 ELSE 0 END";
                return ExecuteReadyScalar(conn, tenantReadySql);
            }

            string sql = @"
SELECT CASE WHEN
    (SELECT COUNT(DISTINCT name) FROM sys.tables WHERE name IN (" + tableNames + @")) = " + requiredTables.Length + @"
    AND EXISTS (SELECT 1 FROM RolePermissions WHERE ModuleKey = N'MasterData')
    AND COL_LENGTH('Jobs', 'JobTitle') IS NOT NULL
    AND COL_LENGTH('Jobs', 'PipelineStatus') IS NOT NULL
    AND COL_LENGTH('Jobs', 'QuotedRevenue') IS NOT NULL
    AND COL_LENGTH('Jobs', 'ActualRevenue') IS NOT NULL
    AND COL_LENGTH('Jobs', 'ClosedDate') IS NOT NULL
    AND COL_LENGTH('Jobs', 'InvoiceId') IS NOT NULL
    AND COL_LENGTH('ClientSites', 'TravelRateINR') IS NOT NULL
    AND COL_LENGTH('JobChecklistItems', 'CompletedBy') IS NOT NULL
    AND COL_LENGTH('JobPartsUsed', 'TotalCost') IS NOT NULL
    AND COL_LENGTH('JobActivityLog', 'ActivityType') IS NOT NULL
    AND (SELECT COUNT_BIG(1) FROM B2BClients) > 0
    AND (SELECT COUNT_BIG(1) FROM ClientSites) > 0
    AND (SELECT COUNT_BIG(1) FROM AMCContracts) > 0
    AND (SELECT COUNT_BIG(1) FROM Vendors) >= 25
    AND (SELECT COUNT_BIG(1) FROM PurchaseOrders) >= 25
    AND (SELECT COUNT_BIG(1) FROM StockItems) >= 50
    AND (SELECT COUNT_BIG(1) FROM Employees) >= 20
    AND (SELECT COUNT_BIG(1) FROM Jobs) > 0
    AND (SELECT COUNT_BIG(1) FROM InvoiceTemplates) > 0
    AND (SELECT COUNT_BIG(1) FROM SupplierItemPrices) > 0
THEN 1 ELSE 0 END";
            return ExecuteReadyScalar(conn, sql);
        }

        private bool ExecuteReadyScalar(SqlConnection conn, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 5;
                object value = cmd.ExecuteScalar();
                return value != null && Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
            }
        }

        public void EnsureOperationalSeedData()
        {
            try
            {
                using (SqlConnection conn = GetConnection())
                {
                    DatabaseConnectionFactory.Open(conn, "DatabaseManager.EnsureOperationalSeedData");

                    EnsureUnitMeasurementSeedData(conn);
                    if (GetTableCount(conn, "InvoiceTemplates") == 0)
                        SeedInvoiceTemplateData(conn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("EnsureOperationalSeedData error: " + ex.Message);
                HVAC_Pro_Desktop.AppRuntime.LogException("Startup operational seed skipped", ex);
            }
        }

        private void EnsureUnitMeasurementSeedData(SqlConnection conn)
        {
            if (!TableExists(conn, "UnitMeasurements") || !TableExists(conn, "UnitMeasurementAliases"))
                return;

            string[] defaultUnits = { "NOS", "PCS", "KG", "LTR", "MTR", "SQFT", "SQM", "KIT", "TIN", "SET", "BOX", "JOB", "VISIT", "LOT", "HOUR", "DAY", "RMT" };
            string[] defaultDisplays = { "Nos", "Pcs", "Kg", "Ltr", "Mtr", "Sqft", "Sqm", "Kit", "Tin", "Set", "Box", "Job", "Visit", "Lot", "Hour", "Day", "RMT" };
            for (int i = 0; i < defaultUnits.Length; i++)
            {
                EnsureUnitMeasurement(conn, defaultUnits[i], defaultDisplays[i]);
            }

            UpsertUnitAlias(conn, "R.M.T", "RMT");
            UpsertUnitAlias(conn, "RMT ", "RMT");
            UpsertUnitAlias(conn, "RUNNING METER", "RMT");
            UpsertUnitAlias(conn, "RUNNINGMETER", "RMT");
            UpsertUnitAlias(conn, "METER", "MTR");
            UpsertUnitAlias(conn, "METERS", "MTR");
            UpsertUnitAlias(conn, "SQMTR", "SQM");
            UpsertUnitAlias(conn, "SQMTRS", "SQM");
            UpsertUnitAlias(conn, "SQUARE METER", "SQM");
            UpsertUnitAlias(conn, "SQUARE METERS", "SQM");
            UpsertUnitAlias(conn, "UNITS", "PCS");
        }

        private bool TableExists(SqlConnection conn, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand(
                @"SELECT CASE WHEN OBJECT_ID(@tableName, 'U') IS NOT NULL THEN 1 ELSE 0 END",
                conn))
            {
                cmd.Parameters.AddWithValue("@tableName", tableName);
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        private void EnsureUnitMeasurement(SqlConnection conn, string code, string displayName)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(displayName))
                return;

            using (SqlCommand cmd = new SqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM UnitMeasurements WHERE UnitCode = @code)
                  INSERT INTO UnitMeasurements (UnitCode, DisplayName, IsActive, IsSystem) VALUES (@code, @name, 1, 1)",
                conn))
            {
                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@name", displayName);
                cmd.ExecuteNonQuery();
            }
        }

        private void UpsertUnitAlias(SqlConnection conn, string alias, string unitCode)
        {
            if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(unitCode))
                return;

            using (SqlCommand cmd = new SqlCommand(
                @"DECLARE @unitId INT = (SELECT TOP 1 UnitMeasurementID FROM UnitMeasurements WHERE UnitCode = @unitCode);
                  IF @unitId IS NOT NULL
                  BEGIN
                      IF NOT EXISTS (SELECT 1 FROM UnitMeasurementAliases WHERE UnitAlias = @alias)
                          INSERT INTO UnitMeasurementAliases (UnitAlias, UnitMeasurementId) VALUES (@alias, @unitId);
                  END",
                conn))
            {
                cmd.Parameters.AddWithValue("@alias", alias);
                cmd.Parameters.AddWithValue("@unitCode", unitCode);
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertSampleData()
        {
            try
            {
                using (SqlConnection conn = GetConnection())
                {
                    DatabaseConnectionFactory.Open(conn, "DatabaseManager.InsertSampleData");

                    if (NeedsCoreSeedReset(conn))
                    {
                        ResetCoreSeed(conn);
                        SeedCoreData(conn);
                    }

                    if (NeedsOpsSeedReset(conn))
                    {
                        ResetOpsSeed(conn);
                        if (!ImportOpsDataFromMseFiles(conn))
                        {
                            SeedVendors(conn);
                            SeedPurchaseData(conn);
                            SeedStockData(conn);
                            if (GetTableCount(conn, "Employees") == 0)
                                SeedEmployeeData(conn);
                        }

                        if (GetTableCount(conn, "SupplierItemPrices") == 0)
                            SeedSupplierPriceData(conn);
                    }

                    if (GetTableCount(conn, "Jobs") == 0)
                        SeedJobData(conn);

                    ImportArchivedClientPurchaseOrders(conn);

                    if (GetTableCount(conn, "InvoiceTemplates") == 0)
                        SeedInvoiceTemplateData(conn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InsertSampleData error: " + ex.Message);
                HVAC_Pro_Desktop.AppRuntime.LogException("Startup sample data seed skipped", ex);
            }
        }

        private bool NeedsCoreSeedReset(SqlConnection conn)
        {
            if (GetTableCount(conn, "B2BClients") == 0)
                return true;

            if (Exists(conn, "SELECT 1 FROM B2BClients WHERE CompanyName IN (N'TCS', N'HDFC', N'ABC Industries')"))
                return true;

            return false;
        }

        private void ResetCoreSeed(SqlConnection conn)
        {
            Exec(conn, @"
                DELETE FROM Payments;
                DELETE FROM InvoiceLineItems;
                DELETE FROM Invoices;
                DELETE FROM SLALogs;
                DELETE FROM AMCContracts;
                DELETE FROM ClientSites;
                DELETE FROM Technicians;
                DELETE FROM B2BClients;");
        }

        private bool NeedsOpsSeedReset(SqlConnection conn)
        {
            int vendorCount = GetTableCount(conn, "Vendors");
            int purchaseOrderCount = GetTableCount(conn, "PurchaseOrders");
            int stockItemCount = GetTableCount(conn, "StockItems");
            int employeeCount = GetTableCount(conn, "Employees");

            if (!Directory.Exists(MseDataFolder))
                return vendorCount == 0 && purchaseOrderCount == 0 && stockItemCount == 0 && employeeCount == 0;

            if (vendorCount == 0 && purchaseOrderCount == 0 && stockItemCount == 0 && employeeCount == 0)
                return true;

            return false;
        }

        private void ResetOpsSeed(SqlConnection conn)
        {
            Exec(conn, @"
                IF OBJECT_ID('dbo.PendingCharges', 'U') IS NOT NULL DELETE FROM PendingCharges;
                IF OBJECT_ID('dbo.InvoiceInventoryReservations', 'U') IS NOT NULL DELETE FROM InvoiceInventoryReservations;
                IF OBJECT_ID('dbo.InventoryUsageLog', 'U') IS NOT NULL DELETE FROM InventoryUsageLog;
                IF OBJECT_ID('dbo.PurchaseLineItems', 'U') IS NOT NULL DELETE FROM PurchaseLineItems;
                IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NOT NULL DELETE FROM PurchaseOrders;
                IF OBJECT_ID('dbo.SupplierItemPrices', 'U') IS NOT NULL DELETE FROM SupplierItemPrices;
                IF OBJECT_ID('dbo.StockItems', 'U') IS NOT NULL DELETE FROM StockItems;
                IF OBJECT_ID('dbo.Jobs', 'U') IS NOT NULL DELETE FROM Jobs;
                IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL DELETE FROM Vendors;
                ");
        }

        private bool ImportOpsDataFromMseFiles(SqlConnection conn)
        {
            if (!File.Exists(VendorFile) || !File.Exists(PurchaseFile) || !File.Exists(StockFile) || !File.Exists(PayrollFile))
                return false;

            try
            {
                ImportVendorData(conn);
                ImportPurchaseData(conn);
                ImportStockData(conn);
                if (GetTableCount(conn, "Employees") == 0)
                    ImportEmployeeData(conn);

                return GetTableCount(conn, "Vendors") > 25
                    && GetTableCount(conn, "PurchaseOrders") > 25
                    && GetTableCount(conn, "StockItems") > 50
                    && GetTableCount(conn, "Employees") > 20;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ImportOpsDataFromMseFiles fallback: " + ex.Message);
                return false;
            }
        }

        private void ImportVendorData(SqlConnection conn)
        {
            DataTable data = ReadExcelSheet(VendorFile, "2025-26$", true, 0);
            foreach (DataRow row in data.Rows)
            {
                string vendorName = TrimTo(CleanText(row["Company Name"]), 255);
                if (string.IsNullOrWhiteSpace(vendorName))
                    continue;

                string address = TrimTo(CleanText(row["Company Address"]), 1000);
                string phone = TrimTo(CleanText(row["Contact Mobile No"]), 20);
                string email = TrimTo(CleanText(row["Email Id"]), 255);
                string gst = TrimTo(CleanText(row["GST"]), 50);
                string category = TrimTo(CleanText(row["Job"]), 100);

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Vendors (VendorName, GSTNumber, PANNumber, Phone, Email, Address, City, Category, IsActive)
                    VALUES (@VendorName, @GSTNumber, @PANNumber, @Phone, @Email, @Address, @City, @Category, 1)", conn))
                {
                    cmd.Parameters.AddWithValue("@VendorName", vendorName);
                    cmd.Parameters.AddWithValue("@GSTNumber", NullIfEmpty(gst));
                    cmd.Parameters.AddWithValue("@PANNumber", DBNull.Value);
                    cmd.Parameters.AddWithValue("@Phone", NullIfEmpty(phone));
                    cmd.Parameters.AddWithValue("@Email", NullIfEmpty(email));
                    cmd.Parameters.AddWithValue("@Address", NullIfEmpty(address));
                    cmd.Parameters.AddWithValue("@City", NullIfEmpty(GetCityFromAddress(address)));
                    cmd.Parameters.AddWithValue("@Category", NullIfEmpty(category));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ImportPurchaseData(SqlConnection conn)
        {
            DataTable data = ReadExcelSheet(PurchaseFile, "Purchases$", false, 0);
            for (int i = 2; i < data.Rows.Count; i++)
            {
                DataRow row = data.Rows[i];
                string supplier = TrimTo(CleanText(row[1]), 255);
                if (string.IsNullOrWhiteSpace(supplier) || supplier.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                    continue;

                int vendorId = EnsureVendor(conn, supplier, CleanText(row[20]));
                string invoiceNo = TrimTo(CleanText(row[2]), 100);
                DateTime poDate = ParseExcelDate(row[3]) ?? DateTime.Today;
                decimal total = ParseDecimal(row[18]);
                decimal paid = ParseDecimal(row[22]);
                string site = TrimTo(CleanText(row[23]), 255);
                DateTime? dueDate = ParseExcelDate(row[25]);
                decimal balance = ParseDecimal(row[19]);
                string status = paid >= total && total > 0 ? "Paid" : paid > 0 ? "Partial" : "Pending";
                string poNumber = "PO-IMP-" + poDate.ToString("yyyyMMdd") + "-" + Math.Max(i - 1, 1).ToString("D4");
                string notes = "Imported from MSE purchase register";
                if (!string.IsNullOrWhiteSpace(site))
                    notes += " | Site: " + site;
                if (dueDate.HasValue)
                    notes += " | Due: " + dueDate.Value.ToString("dd/MM/yyyy");
                if (balance > 0)
                    notes += " | Balance: " + balance.ToString("N2");
                notes = TrimTo(notes, 1000);

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO PurchaseOrders (VendorID, PONumber, PODate, TotalAmount, PaidAmount, Status, Notes)
                    VALUES (@VendorID, @PONumber, @PODate, @TotalAmount, @PaidAmount, @Status, @Notes);
                    SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@VendorID", vendorId);
                    cmd.Parameters.AddWithValue("@PONumber", string.IsNullOrWhiteSpace(invoiceNo) ? poNumber : ("PO-" + invoiceNo.Replace("/", "-")));
                    cmd.Parameters.AddWithValue("@PODate", poDate);
                    cmd.Parameters.AddWithValue("@TotalAmount", total);
                    cmd.Parameters.AddWithValue("@PaidAmount", paid);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@Notes", notes);
                    int poId = Convert.ToInt32(cmd.ExecuteScalar());

                    using (SqlCommand lineCmd = new SqlCommand(@"
                        INSERT INTO PurchaseLineItems (POID, Description, Quantity, Rate, Amount)
                        VALUES (@POID, @Description, @Quantity, @Rate, @Amount)", conn))
                    {
                        lineCmd.Parameters.AddWithValue("@POID", poId);
                        lineCmd.Parameters.AddWithValue("@Description", TrimTo("Imported purchase invoice " + (string.IsNullOrWhiteSpace(invoiceNo) ? supplier : invoiceNo), 255));
                        lineCmd.Parameters.AddWithValue("@Quantity", 1m);
                        lineCmd.Parameters.AddWithValue("@Rate", total);
                        lineCmd.Parameters.AddWithValue("@Amount", total);
                        lineCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void ImportStockData(SqlConnection conn)
        {
            DataTable data = ReadExcelSheet(StockFile, "Stock Summary$", false, 0);
            for (int i = 12; i < data.Rows.Count; i++)
            {
                DataRow row = data.Rows[i];
                string itemName = TrimTo(CleanText(row[0]), 255);
                if (string.IsNullOrWhiteSpace(itemName))
                    continue;

                decimal quantity = 0m;
                string unit = "Nos";
                ParseQuantityAndUnit(CleanText(row[1]), out quantity, out unit);

                decimal rate = ParseDecimal(row[2]);
                decimal value = ParseDecimal(row[3]);
                if (quantity == 0m && rate == 0m && value == 0m)
                    continue;

                if (rate == 0m && quantity > 0m && value != 0m)
                    rate = Math.Round(value / quantity, 2);

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO StockItems (ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel)
                    VALUES (@ItemName, @Category, @CurrentStock, @Unit, @LastPurchaseRate, @ReorderLevel)", conn))
                {
                    cmd.Parameters.AddWithValue("@ItemName", itemName);
                    cmd.Parameters.AddWithValue("@Category", TrimTo(InferCategory(itemName), 100));
                    cmd.Parameters.AddWithValue("@CurrentStock", quantity);
                    cmd.Parameters.AddWithValue("@Unit", TrimTo(unit, 50));
                    cmd.Parameters.AddWithValue("@LastPurchaseRate", rate);
                    cmd.Parameters.AddWithValue("@ReorderLevel", CalculateReorderLevel(quantity));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ImportEmployeeData(SqlConnection conn)
        {
            DataTable data = ReadExcelSheet(PayrollFile, "Master$", false, 0);
            for (int i = 3; i < data.Rows.Count; i++)
            {
                DataRow row = data.Rows[i];
                string employeeCode = TrimTo(CleanText(row[1]), 50);
                string name = TrimTo(ToTitleCase(CleanText(row[4])), 255);
                if (string.IsNullOrWhiteSpace(employeeCode) || string.IsNullOrWhiteSpace(name))
                    continue;

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Employees (EmployeeCode, Name, Designation, Department, ClientSite, Phone, JoiningDate, Status)
                    VALUES (@EmployeeCode, @Name, @Designation, @Department, @ClientSite, @Phone, @JoiningDate, @Status)", conn))
                {
                    cmd.Parameters.AddWithValue("@EmployeeCode", employeeCode);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Designation", NullIfEmpty(TrimTo(ToTitleCase(CleanText(row[28])), 255)));
                    cmd.Parameters.AddWithValue("@Department", NullIfEmpty(TrimTo(ToTitleCase(CleanText(row[23])), 100)));
                    cmd.Parameters.AddWithValue("@ClientSite", NullIfEmpty(TrimTo(CleanText(row[24]), 255)));
                    cmd.Parameters.AddWithValue("@Phone", NullIfEmpty(TrimTo(CleanText(row[17]), 20)));
                    cmd.Parameters.AddWithValue("@JoiningDate", (object)ParseExcelDate(row[6]) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", TrimTo(string.IsNullOrWhiteSpace(CleanText(row[26])) ? "Active" : CleanText(row[26]), 50));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private DataTable ReadExcelSheet(string path, string sheetName, bool hdr, int topRows)
        {
            string extProps = Path.GetExtension(path).Equals(".xls", StringComparison.OrdinalIgnoreCase)
                ? $"Excel 8.0;HDR={(hdr ? "YES" : "NO")};IMEX=1"
                : $"Excel 12.0 Xml;HDR={(hdr ? "YES" : "NO")};IMEX=1";

            var builder = new OleDbConnectionStringBuilder
            {
                Provider = "Microsoft.ACE.OLEDB.12.0",
                DataSource = path
            };
            builder["Extended Properties"] = extProps;

            using (OleDbConnection conn = new OleDbConnection(builder.ConnectionString))
            using (OleDbDataAdapter adapter = new OleDbDataAdapter("SELECT " + (topRows > 0 ? "TOP " + topRows + " " : string.Empty) + "* FROM [" + EscapeSheetIdentifier(sheetName) + "]", conn))
            {
                var table = new DataTable();
                conn.Open();
                adapter.Fill(table);
                return table;
            }
        }

        private static string EscapeSheetIdentifier(string sheetName)
        {
            return (sheetName ?? string.Empty).Replace("]", "]]");
        }

        private int EnsureVendor(SqlConnection conn, string vendorName, string gstNumber)
        {
            using (SqlCommand find = new SqlCommand("SELECT TOP 1 VendorID FROM Vendors WHERE VendorName = @VendorName", conn))
            {
                find.Parameters.AddWithValue("@VendorName", vendorName);
                object existing = find.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                {
                    int existingId = Convert.ToInt32(existing);
                    MarkVendorAsSupplier(conn, existingId);
                    return existingId;
                }
            }

            using (SqlCommand insert = new SqlCommand(@"
                INSERT INTO Vendors (VendorName, GSTNumber, PANNumber, Phone, Email, Address, City, Category, VendorType, IsSupplier, IsServiceVendor, IsActive)
                VALUES (@VendorName, @GSTNumber, NULL, NULL, NULL, NULL, NULL, NULL, N'Supplier', 1, 0, 1);
                SELECT SCOPE_IDENTITY();", conn))
            {
                insert.Parameters.AddWithValue("@VendorName", vendorName);
                insert.Parameters.AddWithValue("@GSTNumber", NullIfEmpty(gstNumber));
                return Convert.ToInt32(insert.ExecuteScalar());
            }
        }

        /// <summary>Marks an existing business partner as a supplier when purchase or material evidence is found.</summary>
        private void MarkVendorAsSupplier(SqlConnection conn, int vendorId)
        {
            using (SqlCommand update = new SqlCommand(@"
                UPDATE Vendors
                SET IsSupplier = 1,
                    VendorType = CASE WHEN VendorType IS NULL OR LTRIM(RTRIM(VendorType)) = '' THEN N'Supplier' ELSE VendorType END
                WHERE VendorID = @VendorID;", conn))
            {
                update.Parameters.AddWithValue("@VendorID", vendorId);
                update.ExecuteNonQuery();
            }
        }

        private string CleanText(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : value.ToString().Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private object NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private decimal ParseDecimal(object value)
        {
            string text = CleanText(value).Replace(",", "");
            decimal result;
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 0m;
        }

        private DateTime? ParseExcelDate(object value)
        {
            string text = CleanText(value);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            double oa;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out oa) && oa > 20000 && oa < 60000)
                return DateTime.FromOADate(oa);

            DateTime parsed;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                || DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out parsed)
                ? parsed
                : (DateTime?)null;
        }

        private void ParseQuantityAndUnit(string text, out decimal quantity, out string unit)
        {
            quantity = 0m;
            unit = "Nos";
            if (string.IsNullOrWhiteSpace(text))
                return;

            Match match = Regex.Match(text, @"(-?\d+(?:\.\d+)?)\s*([A-Za-z]+)?");
            if (!match.Success)
                return;

            decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out quantity);
            if (match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                unit = match.Groups[2].Value.ToUpperInvariant();
        }

        private string InferCategory(string itemName)
        {
            string name = (itemName ?? string.Empty).ToLowerInvariant();
            if (name.Contains("compressor")) return "Compressors";
            if (name.Contains("filter")) return "Filters";
            if (name.Contains("copper")) return "Copper";
            if (name.Contains("gas") || name.Contains("refrigerant")) return "Refrigerant";
            if (name.Contains("belt")) return "Belts";
            if (name.Contains("valve")) return "Valves";
            if (name.Contains("motor") || name.Contains("contactor") || name.Contains("mcb") || name.Contains("capacitor")) return "Electrical";
            if (name.Contains("tool") || name.Contains("gauge")) return "Tools";
            return "General HVAC";
        }

        private decimal CalculateReorderLevel(decimal quantity)
        {
            if (quantity <= 0) return 1;
            return Math.Max(1, Math.Round(quantity * 0.2m, 2));
        }

        private string GetCityFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return string.Empty;

            string[] parts = address.Split(',');
            return parts.Length == 0 ? string.Empty : parts[parts.Length - 1].Trim();
        }

        private string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
        }

        private string TrimTo(string value, int maxLength)
        {
            string cleaned = CleanText(value);
            return cleaned.Length <= maxLength ? cleaned : cleaned.Substring(0, maxLength);
        }

        private int GetTableCount(SqlConnection conn, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand($"SELECT COUNT(*) FROM {tableName}", conn))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private bool Exists(SqlConnection conn, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
                return cmd.ExecuteScalar() != null;
        }

        private void SeedCoreData(SqlConnection conn)
        {
            Exec(conn, @"
                INSERT INTO B2BClients
                    (CompanyName, IndustryType, TotalAnnualValue, PrimaryContact, SecondaryContact, Phone, CustomerSince,
                     Email, GSTNumber, PANNumber, PaymentTermsDays, CreditLimit, BillingAddress, City, IsActive)
                VALUES
                    (N'GPL Mahape', N'Pharmaceutical Manufacturing', 1260000, N'Plant Engineering Team', N'Mahape Support Desk', N'022-61234567', DATEADD(year,-6,GETDATE()), N'engineering@gplmahape.in', N'27AAACG1001M1ZP', N'AAACG1001M', 30, 1800000, N'Mahape MIDC, Navi Mumbai', N'Navi Mumbai', 1),
                    (N'TEVA Gajraula', N'Pharma', 1440000, N'Utilities Lead', N'Maintenance Stores', N'05924-223344', DATEADD(year,-5,GETDATE()), N'fm.gajraula@teva.example', N'09AAACT2727F1ZZ', N'AAACT2727F', 45, 2200000, N'Industrial Area, Gajraula, Uttar Pradesh', N'Gajraula', 1),
                    (N'S H Kelkar', N'Flavours & Fragrances', 1188000, N'Facility Admin', N'Projects Desk', N'022-28470000', DATEADD(year,-7,GETDATE()), N'facility@shkelkar.in', N'27AADCS2424N1Z7', N'AADCS2424N', 30, 1500000, N'Jogeshwari East, Mumbai', N'Mumbai', 1),
                    (N'Piramal Enterprises', N'Pharma', 1680000, N'Utilities Head', N'Accounts Payable', N'022-38023000', DATEADD(year,-4,GETDATE()), N'facilities@piramal.example', N'27AAACP1185C1Z0', N'AAACP1185C', 45, 2500000, N'Kurla West, Mumbai', N'Mumbai', 1),
                    (N'Merck India', N'Life Sciences', 1020000, N'EHS & Facilities', N'Plant Services', N'022-62109000', DATEADD(year,-3,GETDATE()), N'services@merckindia.example', N'27AAACM3211J1Z4', N'AAACM3211J', 30, 1750000, N'Powai, Mumbai', N'Mumbai', 1),
                    (N'JLL', N'Real Estate / Facility Management', 900000, N'Client Facilities Desk', N'Soft Services Team', N'022-44001234', DATEADD(year,-2,GETDATE()), N'facilities@jll.example', N'27AAACJ4500L1Z5', N'AAACJ4500L', 30, 1300000, N'BKC, Mumbai', N'Mumbai', 1);

                INSERT INTO Technicians (Name, Phone, HourlyRate, Designation, YearsExperience)
                VALUES
                    (N'Rameshbhai Devjibhai Patel', N'9429072568', 480, N'Senior Fitter', 14),
                    (N'Jigneshbhai Bipinbhai Patel', N'9979484693', 460, N'Electrical Technician', 12),
                    (N'Mahmedsohel Salim Shaikh', N'8238786555', 520, N'DCS Engineer', 10),
                    (N'Hirenkumar Natvar Patel', N'8980366016', 430, N'Fitter', 7),
                    (N'Shivam Anirudh Tripathi', N'7048238754', 410, N'Fitter', 4),
                    (N'Chandan Kailash Sahu', N'9510888269', 500, N'DCS Engineer', 5);

                INSERT INTO ClientSites (ClientID, SiteName, Address, City, ACSystemCount, RefrigerationSystemCount, CoolingTowerCount, IsCritical, AssignedTechnicianID)
                VALUES
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'GPL Mahape' ORDER BY ClientID), N'GPL Utilities Block', N'Mahape MIDC, Navi Mumbai', N'Navi Mumbai', 18, 4, 2, 1, (SELECT TOP 1 TechnicianID FROM Technicians WHERE Name=N'Mahmedsohel Salim Shaikh' ORDER BY TechnicianID)),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'TEVA Gajraula' ORDER BY ClientID), N'TEVA Production Campus', N'Gajraula Industrial Estate', N'Gajraula', 16, 3, 1, 1, (SELECT TOP 1 TechnicianID FROM Technicians WHERE Name=N'Chandan Kailash Sahu' ORDER BY TechnicianID)),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'S H Kelkar' ORDER BY ClientID), N'SHK Aroma Plant', N'Jogeshwari East', N'Mumbai', 12, 2, 0, 0, (SELECT TOP 1 TechnicianID FROM Technicians WHERE Name=N'Jigneshbhai Bipinbhai Patel' ORDER BY TechnicianID)),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Piramal Enterprises' ORDER BY ClientID), N'Piramal API Unit', N'Kurla West', N'Mumbai', 20, 5, 1, 1, (SELECT TOP 1 TechnicianID FROM Technicians WHERE Name=N'Rameshbhai Devjibhai Patel' ORDER BY TechnicianID)),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Merck India' ORDER BY ClientID), N'Merck Lab Services', N'Powai', N'Mumbai', 10, 2, 0, 0, (SELECT TOP 1 TechnicianID FROM Technicians WHERE Name=N'Hirenkumar Natvar Patel' ORDER BY TechnicianID)),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'JLL' ORDER BY ClientID), N'JLL Managed Facility', N'BKC', N'Mumbai', 14, 1, 0, 0, (SELECT TOP 1 TechnicianID FROM Technicians WHERE Name=N'Shivam Anirudh Tripathi' ORDER BY TechnicianID));

                INSERT INTO AMCContracts (ClientID, SiteID, StartDate, EndDate, MonthlyValue, AnnualValue, ContractStatus, SLAResponseTimeHours, SLAUptimePercent, SLARepairTimeHours, MaintenanceFrequency, ContractType, Notes)
                VALUES
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'GPL Mahape' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'GPL Utilities Block' ORDER BY SiteID), DATEADD(month,-10,GETDATE()), DATEADD(month,2,GETDATE()), 105000, 1260000, N'Active', 4, 99.20, 12, N'Monthly', N'Comprehensive AMC', N'Critical clean-room HVAC and utilities coverage.'),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'TEVA Gajraula' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'TEVA Production Campus' ORDER BY SiteID), DATEADD(month,-8,GETDATE()), DATEADD(month,4,GETDATE()), 120000, 1440000, N'Active', 4, 99.50, 10, N'Monthly', N'Comprehensive AMC', N'Chiller, AHU and process cooling support.'),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'S H Kelkar' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'SHK Aroma Plant' ORDER BY SiteID), DATEADD(month,-15,GETDATE()), DATEADD(month,1,GETDATE()), 99000, 1188000, N'Active', 6, 98.70, 16, N'Preventive', N'Preventive AMC', N'Fragrance blending HVAC maintenance.'),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Piramal Enterprises' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'Piramal API Unit' ORDER BY SiteID), DATEADD(month,-5,GETDATE()), DATEADD(month,7,GETDATE()), 140000, 1680000, N'Active', 4, 99.00, 12, N'Monthly', N'Comprehensive AMC', N'Utility systems and HVAC response coverage.'),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Merck India' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'Merck Lab Services' ORDER BY SiteID), DATEADD(month,-4,GETDATE()), DATEADD(month,9,GETDATE()), 85000, 1020000, N'Active', 8, 98.50, 24, N'Quarterly', N'Preventive AMC', N'Laboratory comfort and cold-room maintenance.'),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'JLL' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'JLL Managed Facility' ORDER BY SiteID), DATEADD(month,-3,GETDATE()), DATEADD(month,6,GETDATE()), 75000, 900000, N'Active', 8, 98.00, 24, N'Monthly', N'Facility Support', N'Managed property HVAC upkeep.');

                INSERT INTO Invoices (ContractID, ClientID, SiteID, InvoiceNumber, InvoiceDate, DueDate, SubTotal, GSTPercent, TaxAmount, TotalAmount, PaidAmount, BalanceDue, PaymentStatus, Notes)
                VALUES
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Critical clean-room HVAC and utilities coverage.' ORDER BY ContractID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'GPL Mahape' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'GPL Utilities Block' ORDER BY SiteID), N'TEVA/02/2025-26', DATEADD(day,-18,GETDATE()), DATEADD(day,12,GETDATE()), 105000, 18, 18900, 123900, 60000, 63900, N'Partial', N'GPL Mahape AMC â€” April 2025.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Chiller, AHU and process cooling support.' ORDER BY ContractID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'TEVA Gajraula' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'TEVA Production Campus' ORDER BY SiteID), N'TEVA/01/2025-26', DATEADD(day,-15,GETDATE()), DATEADD(day,15,GETDATE()), 214638, 18, 38635, 253273, 253273, 0, N'Paid', N'TEVA Gajraula AMC Manpower â€” April 2025.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Fragrance blending HVAC maintenance.' ORDER BY ContractID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'S H Kelkar' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'SHK Aroma Plant' ORDER BY SiteID), N'SHK/01/2025-26', DATEADD(day,-25,GETDATE()), DATEADD(day,-2,GETDATE()), 20000, 18, 3600, 23600, 23600, 0, N'Paid', N'SHK Mulund AMC â€” April 2025.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Utility systems and HVAC response coverage.' ORDER BY ContractID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Piramal Enterprises' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'Piramal API Unit' ORDER BY SiteID), N'PIR/01/2025-26', DATEADD(day,-8,GETDATE()), DATEADD(day,22,GETDATE()), 30000, 18, 5400, 35400, 0, 35400, N'Pending', N'Piramal Enterprises AMC â€” April 2025.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Laboratory comfort and cold-room maintenance.' ORDER BY ContractID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Merck India' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'Merck Lab Services' ORDER BY SiteID), N'MRK/01/2025-26', DATEADD(day,-12,GETDATE()), DATEADD(day,-3,GETDATE()), 15000, 18, 2700, 17700, 0, 17700, N'Overdue', N'Merck India Chiller AMC â€” April 2025.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Managed property HVAC upkeep.' ORDER BY ContractID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'JLL' ORDER BY ClientID), (SELECT TOP 1 SiteID FROM ClientSites WHERE SiteName=N'JLL Managed Facility' ORDER BY SiteID), N'JLL/01/2025-26', DATEADD(day,-5,GETDATE()), DATEADD(day,-1,GETDATE()), 12000, 18, 2160, 14160, 0, 14160, N'Overdue', N'JLL Facility HVAC AMC â€” April 2025.');

                INSERT INTO InvoiceLineItems (InvoiceID, Description, Quantity, Rate, Amount)
                SELECT InvoiceID, N'Comprehensive AMC Services', 1, SubTotal, SubTotal FROM Invoices WHERE InvoiceNumber=N'TEVA/02/2025-26'
                UNION ALL SELECT InvoiceID, N'AC AMC Manpower Supply â€” Utility Team', 1, SubTotal, SubTotal FROM Invoices WHERE InvoiceNumber=N'TEVA/01/2025-26'
                UNION ALL SELECT InvoiceID, N'Supply & Installation AMC', 1, SubTotal, SubTotal FROM Invoices WHERE InvoiceNumber=N'SHK/01/2025-26'
                UNION ALL SELECT InvoiceID, N'Monthly AMC Service', 1, SubTotal, SubTotal FROM Invoices WHERE InvoiceNumber=N'PIR/01/2025-26'
                UNION ALL SELECT InvoiceID, N'Chiller AMC', 1, SubTotal, SubTotal FROM Invoices WHERE InvoiceNumber=N'MRK/01/2025-26'
                UNION ALL SELECT InvoiceID, N'Facility HVAC AMC', 1, SubTotal, SubTotal FROM Invoices WHERE InvoiceNumber=N'JLL/01/2025-26';

                INSERT INTO Payments (PaymentNumber, InvoiceID, ClientID, AmountPaid, PaymentDate, PaymentMode, ReferenceNumber, Notes)
                VALUES
                    (N'PAY-GPL-APR-25', (SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=N'TEVA/02/2025-26' ORDER BY InvoiceID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'GPL Mahape' ORDER BY ClientID), 60000, DATEADD(day,-10,GETDATE()), N'NEFT/RTGS', N'GPL-APR-001', N'Part payment received.'),
                    (N'PAY-TEVA-APR-25', (SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=N'TEVA/01/2025-26' ORDER BY InvoiceID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'TEVA Gajraula' ORDER BY ClientID), 253273, DATEADD(day,-8,GETDATE()), N'NEFT/RTGS', N'TEVA-APR-001', N'Paid in full.'),
                    (N'PAY-SHK-APR-25', (SELECT TOP 1 InvoiceID FROM Invoices WHERE InvoiceNumber=N'SHK/01/2025-26' ORDER BY InvoiceID), (SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'S H Kelkar' ORDER BY ClientID), 23600, DATEADD(day,-5,GETDATE()), N'Cheque', N'SHK-CHQ-001', N'Cheque cleared.');

                INSERT INTO SLALogs (ContractID, MetricType, Target, Actual, LogDate, Compliant, Notes)
                VALUES
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Critical clean-room HVAC and utilities coverage.' ORDER BY ContractID), N'Response Time', N'4 Hours', N'3.2 Hours', DATEADD(day,-11,GETDATE()), 1, N'Breakdown response met target.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Chiller, AHU and process cooling support.' ORDER BY ContractID), N'Uptime', N'99.5%', N'99.6%', DATEADD(day,-9,GETDATE()), 1, N'Chiller uptime above commitment.'),
                    ((SELECT TOP 1 ContractID FROM AMCContracts WHERE Notes=N'Fragrance blending HVAC maintenance.' ORDER BY ContractID), N'Repair Time', N'16 Hours', N'18 Hours', DATEADD(day,-6,GETDATE()), 0, N'One aroma AHU exceeded repair target.');

                INSERT INTO ClientContacts (ClientID, ContactName, Role, Phone, Email, IsPrimary)
                VALUES
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'GPL Mahape' ORDER BY ClientID), N'Rajesh Patil', N'Plant Engineering Head', N'9820112233', N'rajesh.patil@gpl.in', 1),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'GPL Mahape' ORDER BY ClientID), N'Sunita Kulkarni', N'Accounts Payable', N'9821223344', N'sunita.kulkarni@gpl.in', 0),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'TEVA Gajraula' ORDER BY ClientID), N'Amit Sharma', N'Utilities Lead', N'9811445566', N'amit.sharma@teva.in', 1),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'TEVA Gajraula' ORDER BY ClientID), N'Priya Nair', N'Maintenance Manager', N'9812556677', N'priya.nair@teva.in', 0),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Piramal Enterprises' ORDER BY ClientID), N'Vikram Mehta', N'Utilities Head', N'9822334455', N'vikram.mehta@piramal.com', 1),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'Merck India' ORDER BY ClientID), N'Suresh Iyer', N'EHS & Facilities', N'9844556677', N'suresh.iyer@merck.com', 1),
                    ((SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName=N'JLL' ORDER BY ClientID), N'Arun Kapoor', N'Facility Manager', N'9855778899', N'arun.kapoor@jll.com', 1);
            ");
        }

        private void SeedVendors(SqlConnection conn)
        {
            Exec(conn, @"
                INSERT INTO Vendors (VendorName, GSTNumber, DefaultCreditDays, PANNumber, Phone, Email, Address, City, Category, VendorType, IsSupplier, IsServiceVendor, IsActive)
                VALUES
                    (N'H.K. Enterprises', N'27AAAFH1101K1ZV', 30, NULL, N'022-23010001', N'sales@hkenterprises.example', N'Masjid Bunder, Mumbai', N'Mumbai', N'HVAC materials', N'Supplier', 1, 0, 1),
                    (N'Bharat Sales Thane', N'27ACIPC0819M1ZL', 21, N'ACIPC0819M', N'9322383025', N'bharatsalesharesh77@gmail.com', N'Naupada, Thane', N'Thane', N'Electrical parts', N'Supplier', 1, 0, 1),
                    (N'Infinity HVAC Spares & Tools Pvt. Ltd', N'27AACCI1314A1ZD', 45, N'AACCI1314A', N'9223521333', N'enquiry@ihvac.in', N'Dadar East, Mumbai', N'Mumbai', N'HVAC spares', N'Supplier', 1, 0, 1),
                    (N'Anand Sales', N'27AJDPA3014A2Z8', 30, NULL, N'9823556677', N'anand.sales@gmail.com', N'Bhandup, Mumbai', N'Mumbai', N'Consumables', N'Supplier', 1, 0, 1),
                    (N'Climate Engineering Company', N'27AAEFC5505D1Z5', 30, NULL, N'9824667788', N'info@climateengg.example', N'MIDC, Navi Mumbai', N'Navi Mumbai', N'Filters and belts', N'Supplier', 1, 0, 1),
                    (N'Clicon Solution', N'27AIQPB1553M1Z0', 30, N'AIQPB1553M', N'9423584795', N'pdbathe@gmail.com', N'Dhankawdi, Pune', N'Pune', N'Controls spares', N'Supplier', 1, 0, 1),
                    (N'K.K. Trading Company', N'27AAMPM3809P1Z1', 15, NULL, N'9826889900', N'kktrading@example.com', N'MIDC Thane', N'Thane', N'HVAC materials', N'Supplier', 1, 0, 1),
                    (N'India Trading Company', N'27AABFI1006M1ZM', 30, N'AABFI1006M', N'7021056934', N'ravindrapathak66@gmail.com', N'Matunga West, Mumbai', N'Mumbai', N'Industrial parts', N'Supplier', 1, 0, 1),
                    (N'Hari Om Refrigeration Co.', N'27AAAPL3157D1Z2', 30, N'AAAPL3157D', N'9870325648', N'sales@hariomrefrigeration.com', N'Mulund West, Mumbai', N'Mumbai', N'Refrigeration spares', N'Supplier', 1, 0, 1),
                    (N'Flowair Engineer', N'27AAFHF1010T1Z1', 20, NULL, N'9987040833', N'service@flowair.example', N'Rabale, Navi Mumbai', N'Navi Mumbai', N'Valves and tools', N'Supplier', 1, 0, 1),
                    (N'Uni-Chem Services', N'27AOHPP3810L1ZP', 30, N'AOHPP3810L', N'9833269232', N'unichem.services@yahoo.com', N'LBS Marg, Thane', N'Thane', N'Chemicals and cleaning material', N'Supplier', 1, 0, 1),
                    (N'Airconditioning Spares Centre', N'27AABFA2809E1ZY', 30, N'AABFA2809E', N'7045966616', N'sales@accentre.in', N'Andheri East, Mumbai', N'Mumbai', N'AC spares', N'Supplier', 1, 0, 1),
                    (N'Shraddha Sales', N'27AAFHS1313N1ZT', 30, NULL, N'9832445566', N'shraddha@example.com', N'Borivali, Mumbai', N'Mumbai', N'General HVAC spares', N'Supplier', 1, 0, 1),
                    (N'Divya Solutions', N'27AAFHD1414G1ZQ', 30, NULL, N'9833556677', N'ops@divyasolutions.example', N'Malad, Mumbai', N'Mumbai', N'HVAC products', N'Supplier', 1, 0, 1),
                    (N'Beardsell Limited', N'27AAACB1429P1ZJ', 45, N'AAACB1429P', N'8369362672', N'thane@beardsell.co.in', N'Kanara Business Centre, Mumbai', N'Mumbai', N'Insulation products', N'Supplier', 1, 0, 1),
                    (N'ABC HVAC Parts', NULL, 30, NULL, N'9820001101', N'sales@abchvacparts.example', N'Wagle Estate, Thane', N'Thane', N'AC spare parts supplier', N'Supplier', 1, 0, 1),
                    (N'CoolAir Spares', NULL, 30, NULL, N'9820001102', N'orders@coolairspares.example', N'Chakan MIDC, Pune', N'Pune', N'Refrigerant and AC spares', N'Supplier', 1, 0, 1),
                    (N'CopperLine Traders', NULL, 21, NULL, N'9820001103', N'sales@copperline.example', N'MIDC Ambad, Nashik', N'Nashik', N'Copper pipe and fittings', N'Supplier', 1, 0, 1),
                    (N'Fast Cooling Services', NULL, 15, NULL, N'9820001201', N'ops@fastcooling.example', N'Ghodbunder Road, Thane', N'Thane', N'Freelance technician support', N'Service Provider', 0, 1, 1),
                    (N'City Duct Cleaning', NULL, 15, NULL, N'9820001202', N'jobs@cityductcleaning.example', N'Andheri East, Mumbai', N'Mumbai', N'Duct cleaning contractor', N'Subcontractor', 0, 1, 1),
                    (N'Lift & Crane Support', NULL, 7, NULL, N'9820001203', N'bookings@liftcrane.example', N'Taloja MIDC, Navi Mumbai', N'Navi Mumbai', N'Crane rental and lifting support', N'Service Provider', 0, 1, 1);
            ");
        }

        private void SeedPurchaseData(SqlConnection conn)
        {
            Exec(conn, @"
                INSERT INTO PurchaseOrders (VendorID, PONumber, PODate, PayByDate, VendorInvoiceNumber, LinkedToType, LinkedToId, TotalAmount, PaidAmount, Status, PaymentReference, Notes)
                VALUES
                    ((SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=N'H.K. Enterprises' ORDER BY VendorID), N'PO-DEMO-2604-001', DATEADD(day,-37,GETDATE()), DATEADD(day,-7,GETDATE()), N'HK-4481', N'Contract', 1, 58410, 20000, N'Partial', N'UTR-HK-APR-01', N'Copper piping and refrigerant purchase.'),
                    ((SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=N'Infinity HVAC Spares & Tools Pvt. Ltd' ORDER BY VendorID), N'PO-DEMO-2604-002', DATEADD(day,-18,GETDATE()), DATEADD(day,27,DATEADD(day,-18,GETDATE())), N'INF-2217', N'WorkOrder', 1, 47200, 0, N'Pending', NULL, N'Spare compressor and contactors for pharma sites.'),
                    ((SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=N'Climate Engineering Company' ORDER BY VendorID), N'PO-DEMO-2604-003', DATEADD(day,-50,GETDATE()), DATEADD(day,-20,GETDATE()), N'CEC-9003', N'General', NULL, 38940, 38940, N'Paid', N'NEFT-CEC-9003', N'Belts and filters for quarterly PM work.'),
                    ((SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=N'Bharat Sales Thane' ORDER BY VendorID), N'PO-DEMO-2604-004', DATEADD(day,-12,GETDATE()), DATEADD(day,9,DATEADD(day,-12,GETDATE())), N'BST-1104', N'Contract', 2, 26680, 0, N'Pending', NULL, N'Electrical consumables for JLL and SHK sites.'),
                    ((SELECT TOP 1 VendorID FROM Vendors WHERE VendorName=N'Flowair Engineer' ORDER BY VendorID), N'PO-DEMO-2604-005', DATEADD(day,-24,GETDATE()), DATEADD(day,-4,GETDATE()), N'FLW-781', N'WorkOrder', 2, 71850, 0, N'Pending', NULL, N'Emergency chiller valve and actuator stock-up.');

                INSERT INTO PurchaseLineItems (POID, Description, HsnSacCode, Quantity, Rate, GSTRate, CGSTRate, SGSTRate, IGSTRate, Amount)
                SELECT POID, N'Copper Tube 5/8 inch', N'7411', 60, 315, 18, 9, 9, 18, 18900 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-001'
                UNION ALL SELECT POID, N'R410A Refrigerant Cylinder', N'8415', 6, 3300, 18, 9, 9, 18, 19800 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-001'
                UNION ALL SELECT POID, N'Insulation Roll', N'8415', 10, 1500, 18, 9, 9, 18, 15000 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-001'
                UNION ALL SELECT POID, N'Compressor Contactors', N'8544', 12, 850, 18, 9, 9, 18, 10200 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-002'
                UNION ALL SELECT POID, N'Scroll Compressor Spare Kit', N'8415', 4, 8200, 18, 9, 9, 18, 32800 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-002'
                UNION ALL SELECT POID, N'Filter Drier', N'8415', 10, 420, 18, 9, 9, 18, 4200 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-002'
                UNION ALL SELECT POID, N'AHU Belt Set', N'8415', 18, 620, 18, 9, 9, 18, 11160 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-003'
                UNION ALL SELECT POID, N'Pre Filter 10x20', N'8415', 40, 285, 18, 9, 9, 18, 11400 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-003'
                UNION ALL SELECT POID, N'Pocket Filter', N'8415', 12, 1450, 18, 9, 9, 18, 17400 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-003'
                UNION ALL SELECT POID, N'MCB 32A', N'8544', 20, 310, 18, 9, 9, 18, 6200 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-004'
                UNION ALL SELECT POID, N'Capacitor 60 MFD', N'8415', 15, 690, 18, 9, 9, 18, 10350 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-004'
                UNION ALL SELECT POID, N'PVC Control Cable 2.5mm', N'8544', 8, 1266, 18, 9, 9, 18, 10128 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-004'
                UNION ALL SELECT POID, N'Motorized Valve 2 inch', N'8415', 6, 6200, 18, 9, 9, 18, 37200 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-005'
                UNION ALL SELECT POID, N'Actuator Assembly', N'8415', 5, 6900, 18, 9, 9, 18, 34500 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-005'
                UNION ALL SELECT POID, N'Pressure Gauge Set', N'8415', 10, 1015, 18, 9, 9, 18, 10150 FROM PurchaseOrders WHERE PONumber=N'PO-DEMO-2604-005';
            ");
        }

        private void SeedStockData(SqlConnection conn)
        {
            Exec(conn, @"
                INSERT INTO StockItems (ItemName, Category, CurrentStock, Unit, LastPurchaseRate, ReorderLevel)
                VALUES
                    (N'R410A Refrigerant Cylinder', N'Refrigerant', 12, N'Cylinder', 3300, 6),
                    (N'Copper Tube 5/8 inch', N'Copper', 95, N'Meter', 315, 40),
                    (N'Pre Filter 10x20', N'Filters', 44, N'Nos', 285, 25),
                    (N'Pocket Filter', N'Filters', 9, N'Nos', 1450, 12),
                    (N'AHU Belt Set', N'Belts', 11, N'Set', 620, 10),
                    (N'Compressor Contactors', N'Electrical', 8, N'Nos', 850, 10),
                    (N'Capacitor 60 MFD', N'Electrical', 14, N'Nos', 690, 8),
                    (N'Scroll Compressor Spare Kit', N'Compressors', 3, N'Kit', 8200, 2),
                    (N'Motorized Valve 2 inch', N'Valves', 4, N'Nos', 6200, 3),
                    (N'Pressure Gauge Set', N'Tools', 6, N'Nos', 1015, 4);
            ");
        }

        private void SeedEmployeeData(SqlConnection conn)
        {
            Exec(conn, @"
                INSERT INTO Employees (EmployeeCode, Name, Designation, Department, ClientSite, Phone, JoiningDate, Status)
                VALUES
                    (N'EMP06', N'Jigneshbhai Bipinbhai Patel', N'Electrician DG Operator', N'Operations', N'Client Site', N'9979484693', '2020-02-01', N'Active'),
                    (N'EMP07', N'Mahmedsohel Salim Shaikh', N'DCS Engineer', N'Automation', N'Client Site', N'8238786555', '2020-02-01', N'Active'),
                    (N'EMP36', N'Rameshbhai Devjibhai Patel', N'Fitter', N'Field Service', N'Client Site', N'9429072568', '2021-02-05', N'Active'),
                    (N'EMP41', N'Haiyumsha Hamidshah Fakir', N'Fitter', N'Field Service', N'Client Site', N'7567562253', '2021-06-02', N'Active'),
                    (N'EMP52', N'Mayur Kumar Mahyavanshi', N'Instrument Technician', N'Instrumentation', N'Client Site', N'9909690809', '2022-11-09', N'Active'),
                    (N'EMP59', N'Ketan Kumar Gohil', N'Fitter', N'Field Service', N'Client Site', N'9081173538', '2023-06-01', N'Active'),
                    (N'EMP60', N'Hirenkumar Natvar Patel', N'Fitter', N'Field Service', N'Client Site', N'8980366016', '2023-06-12', N'Active'),
                    (N'EMP63', N'Shivam Anirudh Tripathi', N'Fitter', N'Field Service', N'Client Site', N'7048238754', '2023-08-23', N'Active'),
                    (N'EMP64', N'Indrajit Parsottambhai Suhagia', N'DCS Engineer', N'Automation', N'Client Site', N'8460083926', '2023-09-20', N'Active'),
                    (N'EMP73', N'Chandan Kailash Sahu', N'DCS Engineer', N'Automation', N'Client Site', N'9510888269', '2024-04-22', N'Active');
            ");
        }

        private void SeedSupplierPriceData(SqlConnection conn)
        {
            var prices = new[]
            {
                new { Vendor = "H.K. Enterprises", Item = "Copper Tube 5/8 inch", Category = "Copper", Unit = "Meter", Rate = 315m },
                new { Vendor = "Bharat Sales, Thane", Item = "Copper Tube 5/8 inch", Category = "Copper", Unit = "Meter", Rate = 327m },
                new { Vendor = "H.K. Enterprises", Item = "R410A Refrigerant Cylinder", Category = "Refrigerant", Unit = "Cylinder", Rate = 3300m },
                new { Vendor = "Climate Engineering Company", Item = "Pre Filter 10x20", Category = "Filters", Unit = "Nos", Rate = 285m },
                new { Vendor = "Air Conditioning Spares Centre", Item = "Pre Filter 10x20", Category = "Filters", Unit = "Nos", Rate = 295m },
                new { Vendor = "Climate Engineering Company", Item = "Pocket Filter", Category = "Filters", Unit = "Nos", Rate = 1450m },
                new { Vendor = "Infinity Hvac Spares & Tools Pvt. Ltd", Item = "Compressor Contactors", Category = "Electrical", Unit = "Nos", Rate = 850m },
                new { Vendor = "Bharat Sales, Thane", Item = "Capacitor 60 MFD", Category = "Electrical", Unit = "Nos", Rate = 690m },
                new { Vendor = "Infinity Hvac Spares & Tools Pvt. Ltd", Item = "Scroll Compressor Spare Kit", Category = "Compressors", Unit = "Kit", Rate = 8200m },
                new { Vendor = "Flowair Engineer", Item = "Motorized Valve 2 inch", Category = "Valves", Unit = "Nos", Rate = 6200m },
                new { Vendor = "Flowair Engineer", Item = "Actuator Assembly", Category = "Valves", Unit = "Nos", Rate = 6900m },
                new { Vendor = "Flowair Engineer", Item = "Pressure Gauge Set", Category = "Tools", Unit = "Nos", Rate = 1015m },
                new { Vendor = "Hari Om Refrigeration Co.", Item = "Filter Drier", Category = "Refrigeration", Unit = "Nos", Rate = 420m },
                new { Vendor = "Climate Engineering Company", Item = "AHU Belt Set", Category = "Belts", Unit = "Set", Rate = 620m },
                new { Vendor = "Bharat Sales, Thane", Item = "MCB 32A", Category = "Electrical", Unit = "Nos", Rate = 310m }
            };

            foreach (var price in prices)
            {
                int vendorId = EnsureVendor(conn, price.Vendor, null);
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO SupplierItemPrices (VendorID, ItemName, Category, Unit, Rate, Source)
                    VALUES (@VendorID, @ItemName, @Category, @Unit, @Rate, @Source)", conn))
                {
                    cmd.Parameters.AddWithValue("@VendorID", vendorId);
                    cmd.Parameters.AddWithValue("@ItemName", price.Item);
                    cmd.Parameters.AddWithValue("@Category", price.Category);
                    cmd.Parameters.AddWithValue("@Unit", price.Unit);
                    cmd.Parameters.AddWithValue("@Rate", price.Rate);
                    cmd.Parameters.AddWithValue("@Source", "Mapped supplier rate card");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SeedJobData(SqlConnection conn)
        {
            var jobs = new[]
            {
                new
                {
                    JobNumber = "JOB-260409-001",
                    ClientName = "GPL Mahape",
                    PreferredSite = "GPL Utilities Block",
                    Title = "Quarterly HVAC preventive maintenance",
                    Description = "PM checklist, filter cleaning and refrigerant pressure audit.",
                    EmployeeName = "Jigneshbhai Bipinbhai Patel",
                    ScheduledDate = DateTime.Today.AddDays(-1),
                    CompletedDate = (DateTime?)null,
                    Priority = "High",
                    Status = "In Progress",
                    EstimatedCost = 18500m,
                    Revenue = 32500m,
                    Notes = "Upsell opportunity for filter replacement."
                },
                new
                {
                    JobNumber = "JOB-260409-002",
                    ClientName = "TEVA Gajraula",
                    PreferredSite = "TEVA Production Campus",
                    Title = "Compressor replacement support",
                    Description = "Replace failed compressor and recommission unit.",
                    EmployeeName = "Chandan Kailash Sahu",
                    ScheduledDate = DateTime.Today.AddDays(1),
                    CompletedDate = (DateTime?)null,
                    Priority = "Urgent",
                    Status = "Pending",
                    EstimatedCost = 265000m,
                    Revenue = 353250m,
                    Notes = "High-value pending job. Confirm customer approval quickly."
                },
                new
                {
                    JobNumber = "JOB-260409-003",
                    ClientName = "JLL",
                    PreferredSite = "JLL Managed Facility",
                    Title = "AHU belt and filter changeout",
                    Description = "Replace pocket filters and AHU belt set.",
                    EmployeeName = "Shivam Anirudh Tripathi",
                    ScheduledDate = DateTime.Today.AddDays(-6),
                    CompletedDate = (DateTime?)DateTime.Today.AddDays(-5),
                    Priority = "Medium",
                    Status = "Completed",
                    EstimatedCost = 12400m,
                    Revenue = 24450m,
                    Notes = "Completed with strong margin. Suggest AMC add-on follow-up."
                },
                new
                {
                    JobNumber = "JOB-260409-004",
                    ClientName = "S H Kelkar",
                    PreferredSite = "SHK Aroma Plant",
                    Title = "Utility room inspection",
                    Description = "Inspect chilled water line insulation and electrical panel health.",
                    EmployeeName = "Rameshbhai Devjibhai Patel",
                    ScheduledDate = DateTime.Today.AddDays(2),
                    CompletedDate = (DateTime?)null,
                    Priority = "Low",
                    Status = "Pending",
                    EstimatedCost = 8500m,
                    Revenue = 16500m,
                    Notes = "Bundle with annual maintenance proposal."
                }
            };

            foreach (var job in jobs)
            {
                int? clientId = GetClientId(conn, job.ClientName);
                if (!clientId.HasValue)
                    continue;

                int? siteId = GetSiteId(conn, clientId.Value, job.PreferredSite);
                if (!siteId.HasValue)
                    continue;

                int? employeeId = GetEmployeeId(conn, job.EmployeeName);

                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO Jobs
                        (JobNumber, ClientID, SiteID, Title, Description, AssignedEmployeeID, ScheduledDate, CompletedDate, Priority, Status, EstimatedCost, Revenue, Notes)
                    VALUES
                        (@JobNumber, @ClientID, @SiteID, @Title, @Description, @AssignedEmployeeID, @ScheduledDate, @CompletedDate, @Priority, @Status, @EstimatedCost, @Revenue, @Notes);", conn))
                {
                    cmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
                    cmd.Parameters.AddWithValue("@ClientID", clientId.Value);
                    cmd.Parameters.AddWithValue("@SiteID", siteId.Value);
                    cmd.Parameters.AddWithValue("@Title", job.Title);
                    cmd.Parameters.AddWithValue("@Description", (object)job.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@AssignedEmployeeID", (object)employeeId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ScheduledDate", job.ScheduledDate);
                    cmd.Parameters.AddWithValue("@CompletedDate", (object)job.CompletedDate ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Priority", job.Priority);
                    cmd.Parameters.AddWithValue("@Status", job.Status);
                    cmd.Parameters.AddWithValue("@EstimatedCost", job.EstimatedCost);
                    cmd.Parameters.AddWithValue("@Revenue", job.Revenue);
                    cmd.Parameters.AddWithValue("@Notes", (object)job.Notes ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SeedInvoiceTemplateData(SqlConnection conn)
        {
            Exec(conn, @"
                INSERT INTO InvoiceTemplates
                    (TemplateCode, TemplateName, WorkflowType, DefaultSubject, DefaultNotes, DefaultGstMode,
                     DefaultGstPercent, DefaultPaymentTerms, ContractCoverageType, DefaultChecklist, DefaultAssetInfo, IsActive)
                VALUES
                    (N'SERVICE_SPLIT_AC', N'Service Visit - Split AC', N'Service Visit',
                     N'Split AC service visit at {SITE}', N'Standard service visit with inspection, cleaning, pressure check, electrical safety check and report submission.',
                     N'IGST', 18, N'Immediate / 7 Days', N'Standard Service',
                     N'Indoor unit cleaning; Outdoor coil wash; Gas pressure check; Electrical ampere check; Drain line flush; Service report handover',
                     N'Equipment: Split AC / indoor-outdoor unit details, model, tonnage, serial no.', 1),
                    (N'AMC_QUARTERLY_SPLIT_AC', N'AMC Quarterly Visit - Split AC', N'AMC Visit',
                     N'Quarterly AMC visit for {SITE}', N'Preventive maintenance visit under AMC coverage. Review included items vs billable items before finalisation.',
                     N'IGST', 18, N'As per AMC terms', N'Comprehensive AMC',
                     N'Quarterly PM checklist; Filter cleaning; Coil cleaning; Electrical terminal tightening; Condensate line inspection; Operating parameter recording; Service due update',
                     N'Covered assets under AMC / site asset register reference', 1),
                    (N'REPAIR_BREAKDOWN_AC', N'Repair Breakdown - AC', N'Repair Job',
                     N'Breakdown repair support at {SITE}', N'Diagnosis, root-cause repair, part recommendation and closure report. Check warranty before billing spares.',
                     N'IGST', 18, N'Immediate / 15 Days', N'Repair',
                     N'Complaint received; Diagnosis completed; Root cause confirmed; Spare parts checked; Repair tested; Closure report issued',
                     N'Equipment under repair / fault code / serial no. / complaint details', 1),
                    (N'INSTALL_SPLIT_AC', N'Installation - Split AC', N'Installation Job',
                     N'Supply and installation of split AC at {SITE}', N'Installation with piping, wiring, stand/bracket fitment, gas charging, testing and commissioning.',
                     N'IGST', 18, N'40% advance / balance on completion', N'Installation',
                     N'Mounting completed; Copper piping; Drain line routing; Wiring termination; Vacuum / gas charging; Testing and commissioning; Handover',
                     N'Installation location / indoor-outdoor placement / power point / tonnage details', 1);");
        }

        private void ImportArchivedClientPurchaseOrders(SqlConnection conn)
        {
            if (!Directory.Exists(ClientPurchaseArchiveFolder))
                return;

            int vendorId = EnsureArchiveVendor(conn);
            foreach (string pdfPath in Directory.GetFiles(ClientPurchaseArchiveFolder, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(pdfPath);
                string fileStem = Path.GetFileNameWithoutExtension(pdfPath);
                string poNumber = TrimTo(fileStem, 50);
                string sourceNote = TrimTo("Imported from client AMC PO archive | Source File: " + fileName, 1000);

                using (SqlCommand exists = new SqlCommand(@"
                    SELECT TOP 1 1
                    FROM PurchaseOrders
                    WHERE PONumber = @PONumber AND Notes LIKE @SourceNote", conn))
                {
                    exists.Parameters.AddWithValue("@PONumber", poNumber);
                    exists.Parameters.AddWithValue("@SourceNote", "%" + fileName + "%");
                    if (exists.ExecuteScalar() != null)
                        continue;
                }

                DateTime poDate = TryReadPdfCreationDate(pdfPath) ?? File.GetCreationTime(pdfPath);
                DateTime payByDate = poDate.Date.AddDays(30);
                string notes = BuildArchiveNotes(fileStem, fileName, pdfPath, poDate);

                int poId;
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO PurchaseOrders
                        (VendorID, PONumber, PODate, PayByDate, VendorInvoiceNumber, LinkedToType, LinkedToId, TotalAmount, PaidAmount, Status, PaymentReference, Notes)
                    VALUES
                        (@VendorID, @PONumber, @PODate, @PayByDate, NULL, N'Contract', NULL, 0, 0, N'Received', NULL, @Notes);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
                {
                    cmd.Parameters.AddWithValue("@VendorID", vendorId);
                    cmd.Parameters.AddWithValue("@PONumber", poNumber);
                    cmd.Parameters.AddWithValue("@PODate", poDate.Date);
                    cmd.Parameters.AddWithValue("@PayByDate", payByDate);
                    cmd.Parameters.AddWithValue("@Notes", notes);
                    poId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (SqlCommand lineCmd = new SqlCommand(@"
                    INSERT INTO PurchaseLineItems
                        (POID, InventoryItemId, Description, HsnSacCode, Quantity, Rate, GSTRate, CGSTRate, SGSTRate, IGSTRate, Amount)
                    VALUES
                        (@POID, NULL, @Description, NULL, 1, 0, 0, 0, 0, 0, 0)", conn))
                {
                    lineCmd.Parameters.AddWithValue("@POID", poId);
                    lineCmd.Parameters.AddWithValue("@Description", TrimTo("AMC client PO archive entry - " + fileStem, 255));
                    lineCmd.ExecuteNonQuery();
                }
            }
        }

        private int EnsureArchiveVendor(SqlConnection conn)
        {
            using (SqlCommand find = new SqlCommand("SELECT TOP 1 VendorID FROM Vendors WHERE VendorName = @VendorName", conn))
            {
                find.Parameters.AddWithValue("@VendorName", ClientPurchaseArchiveVendorName);
                object existing = find.ExecuteScalar();
                if (existing != null && existing != DBNull.Value)
                {
                    int existingId = Convert.ToInt32(existing);
                    MarkVendorAsSupplier(conn, existingId);
                    return existingId;
                }
            }

            using (SqlCommand insert = new SqlCommand(@"
                INSERT INTO Vendors (VendorName, GSTNumber, DefaultCreditDays, PANNumber, Phone, Email, Address, City, Category, VendorType, IsSupplier, IsServiceVendor, IsActive)
                VALUES (@VendorName, NULL, 30, NULL, NULL, NULL, @Address, @City, @Category, N'Supplier', 1, 0, 1);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conn))
            {
                insert.Parameters.AddWithValue("@VendorName", ClientPurchaseArchiveVendorName);
                insert.Parameters.AddWithValue("@Address", @"C:\HVAC_PRO_MSE\DATABASE\Purchase Order AMC");
                insert.Parameters.AddWithValue("@City", "Archive");
                insert.Parameters.AddWithValue("@Category", "Client AMC Archive");
                return Convert.ToInt32(insert.ExecuteScalar());
            }
        }

        private string BuildArchiveNotes(string fileStem, string fileName, string pdfPath, DateTime poDate)
        {
            Match rangeMatch = Regex.Match(fileStem, @"((Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[A-Za-z0-9\s&.-]*)", RegexOptions.IgnoreCase);
            string inferredWindow = rangeMatch.Success ? " | Inferred Coverage: " + rangeMatch.Groups[1].Value.Trim() : string.Empty;
            return TrimTo(
                "Imported from client AMC PO archive"
                + " | Source File: " + fileName
                + " | Source Path: " + pdfPath
                + " | Imported PO Date: " + poDate.ToString("dd-MMM-yyyy")
                + inferredWindow,
                1000);
        }

        private DateTime? TryReadPdfCreationDate(string pdfPath)
        {
            try
            {
                byte[] buffer = new byte[Math.Min(8192, (int)new FileInfo(pdfPath).Length)];
                using (FileStream stream = File.OpenRead(pdfPath))
                {
                    stream.Read(buffer, 0, buffer.Length);
                }

                string header = System.Text.Encoding.ASCII.GetString(buffer);
                Match match = Regex.Match(header, @"/CreationDate\s*\(D:(\d{4})(\d{2})(\d{2})");
                if (match.Success)
                {
                    int year = int.Parse(match.Groups[1].Value);
                    int month = int.Parse(match.Groups[2].Value);
                    int day = int.Parse(match.Groups[3].Value);
                    return new DateTime(year, month, day);
                }
            }
            catch
            {
            }

            return null;
        }

        private int? GetClientId(SqlConnection conn, string clientName)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 ClientID FROM B2BClients WHERE CompanyName = @Name", conn))
            {
                cmd.Parameters.AddWithValue("@Name", clientName);
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? (int?)null : Convert.ToInt32(result);
            }
        }

        private int? GetSiteId(SqlConnection conn, int clientId, string preferredSiteName)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP 1 SiteID
                FROM ClientSites
                WHERE ClientID = @ClientID
                ORDER BY CASE WHEN SiteName = @PreferredSiteName THEN 0 ELSE 1 END, SiteID", conn))
            {
                cmd.Parameters.AddWithValue("@ClientID", clientId);
                cmd.Parameters.AddWithValue("@PreferredSiteName", preferredSiteName ?? string.Empty);
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? (int?)null : Convert.ToInt32(result);
            }
        }

        private int? GetEmployeeId(SqlConnection conn, string employeeName)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 EmployeeID FROM Employees WHERE Name = @Name", conn))
            {
                cmd.Parameters.AddWithValue("@Name", employeeName);
                object result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? (int?)null : Convert.ToInt32(result);
            }
        }

        private void SeedPayrollReferenceData(SqlConnection conn)
        {
            string seedDate = "2025-04-01";

            Exec(conn, @"
                IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE LeaveTypeName = 'CL') INSERT INTO LeaveTypes (LeaveTypeName, PaidLeave, AnnualQuota) VALUES ('CL', 1, 12);
                IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE LeaveTypeName = 'SL') INSERT INTO LeaveTypes (LeaveTypeName, PaidLeave, AnnualQuota) VALUES ('SL', 1, 12);
                IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE LeaveTypeName = 'EL') INSERT INTO LeaveTypes (LeaveTypeName, PaidLeave, AnnualQuota) VALUES ('EL', 1, 15);
                IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE LeaveTypeName = 'ML') INSERT INTO LeaveTypes (LeaveTypeName, PaidLeave, AnnualQuota) VALUES ('ML', 1, 180);
                IF NOT EXISTS (SELECT 1 FROM LeaveTypes WHERE LeaveTypeName = 'PL') INSERT INTO LeaveTypes (LeaveTypeName, PaidLeave, AnnualQuota) VALUES ('PL', 1, 12);");

            InsertProfessionalTaxSlab(conn, "MH", "Maharashtra", 0m, 7500m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "MH", "Maharashtra", 7501m, 10000m, 175m, seedDate);
            InsertProfessionalTaxSlab(conn, "MH", "Maharashtra", 10001m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "KA", "Karnataka", 0m, 15000m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "KA", "Karnataka", 15001m, 24999m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "KA", "Karnataka", 25000m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "TN", "Tamil Nadu", 0m, 3500m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "TN", "Tamil Nadu", 3501m, 5000m, 16.50m, seedDate);
            InsertProfessionalTaxSlab(conn, "TN", "Tamil Nadu", 5001m, 7500m, 25m, seedDate);
            InsertProfessionalTaxSlab(conn, "TN", "Tamil Nadu", 7501m, 10000m, 35m, seedDate);
            InsertProfessionalTaxSlab(conn, "TN", "Tamil Nadu", 10001m, 12500m, 50m, seedDate);
            InsertProfessionalTaxSlab(conn, "TN", "Tamil Nadu", 12501m, null, 62.50m, seedDate);

            InsertProfessionalTaxSlab(conn, "GJ", "Gujarat", 0m, 5999m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "GJ", "Gujarat", 6000m, 8999m, 80m, seedDate);
            InsertProfessionalTaxSlab(conn, "GJ", "Gujarat", 9000m, 11999m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "GJ", "Gujarat", 12000m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "WB", "West Bengal", 0m, 10000m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "WB", "West Bengal", 10001m, 15000m, 110m, seedDate);
            InsertProfessionalTaxSlab(conn, "WB", "West Bengal", 15001m, 25000m, 130m, seedDate);
            InsertProfessionalTaxSlab(conn, "WB", "West Bengal", 25001m, 40000m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "WB", "West Bengal", 40001m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "AP", "Andhra Pradesh", 0m, 15000m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "AP", "Andhra Pradesh", 15001m, 20000m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "AP", "Andhra Pradesh", 20001m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "TS", "Telangana", 0m, 15000m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "TS", "Telangana", 15001m, 20000m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "TS", "Telangana", 20001m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "KL", "Kerala", 0m, 11999m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "KL", "Kerala", 12000m, 17999m, 120m, seedDate);
            InsertProfessionalTaxSlab(conn, "KL", "Kerala", 18000m, 29999m, 180m, seedDate);
            InsertProfessionalTaxSlab(conn, "KL", "Kerala", 30000m, null, 208m, seedDate);

            InsertProfessionalTaxSlab(conn, "MP", "Madhya Pradesh", 0m, 18750m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "MP", "Madhya Pradesh", 18751m, 25000m, 125m, seedDate);
            InsertProfessionalTaxSlab(conn, "MP", "Madhya Pradesh", 25001m, 33333m, 167m, seedDate);
            InsertProfessionalTaxSlab(conn, "MP", "Madhya Pradesh", 33334m, null, 208m, seedDate);

            InsertProfessionalTaxSlab(conn, "OD", "Odisha", 0m, 13333m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "OD", "Odisha", 13334m, 25000m, 125m, seedDate);
            InsertProfessionalTaxSlab(conn, "OD", "Odisha", 25001m, 41666m, 167m, seedDate);
            InsertProfessionalTaxSlab(conn, "OD", "Odisha", 41667m, null, 200m, seedDate);

            InsertProfessionalTaxSlab(conn, "AS", "Assam", 0m, 10000m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "AS", "Assam", 10001m, 14999m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "AS", "Assam", 15000m, 24999m, 180m, seedDate);
            InsertProfessionalTaxSlab(conn, "AS", "Assam", 25000m, null, 208m, seedDate);

            InsertProfessionalTaxSlab(conn, "JH", "Jharkhand", 0m, 25000m, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "JH", "Jharkhand", 25001m, 41666m, 100m, seedDate);
            InsertProfessionalTaxSlab(conn, "JH", "Jharkhand", 41667m, 83333m, 150m, seedDate);
            InsertProfessionalTaxSlab(conn, "JH", "Jharkhand", 83334m, null, 208m, seedDate);

            InsertProfessionalTaxSlab(conn, "CG", "Chhattisgarh", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "BR", "Bihar", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "DL", "Delhi", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "HR", "Haryana", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "UP", "Uttar Pradesh", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "RJ", "Rajasthan", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "PB", "Punjab", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "HP", "Himachal Pradesh", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "JK", "Jammu and Kashmir", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "UK", "Uttarakhand", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "GA", "Goa", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "AR", "Arunachal Pradesh", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "MN", "Manipur", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "ML", "Meghalaya", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "MZ", "Mizoram", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "NL", "Nagaland", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "SK", "Sikkim", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "TR", "Tripura", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "AN", "Andaman and Nicobar Islands", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "CH", "Chandigarh", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "DN", "Dadra and Nagar Haveli and Daman and Diu", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "LD", "Lakshadweep", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "PY", "Puducherry", 0m, null, 0m, seedDate);
            InsertProfessionalTaxSlab(conn, "LA", "Ladakh", 0m, null, 0m, seedDate);
        }

        private void InsertProfessionalTaxSlab(SqlConnection conn, string stateCode, string stateName, decimal minSalary, decimal? maxSalary, decimal monthlyPt, string effectiveFrom)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM ProfessionalTaxSlabs
                    WHERE StateCode = @stateCode
                      AND MinSalary = @minSalary
                      AND ((MaxSalary IS NULL AND @maxSalary IS NULL) OR MaxSalary = @maxSalary)
                      AND EffectiveFrom = @effectiveFrom)
                INSERT INTO ProfessionalTaxSlabs (StateCode, StateName, MinSalary, MaxSalary, MonthlyPT, EffectiveFrom)
                VALUES (@stateCode, @stateName, @minSalary, @maxSalary, @monthlyPt, @effectiveFrom);", conn))
            {
                cmd.Parameters.AddWithValue("@stateCode", stateCode);
                cmd.Parameters.AddWithValue("@stateName", stateName);
                cmd.Parameters.AddWithValue("@minSalary", minSalary);
                cmd.Parameters.AddWithValue("@maxSalary", maxSalary.HasValue ? (object)maxSalary.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@monthlyPt", monthlyPt);
                cmd.Parameters.AddWithValue("@effectiveFrom", DateTime.Parse(effectiveFrom, CultureInfo.InvariantCulture));
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureAuthSchema(SqlConnection conn)
        {
            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AppRoles')
            CREATE TABLE AppRoles (
                RoleId INT IDENTITY(1,1) PRIMARY KEY,
                RoleName NVARCHAR(50) NOT NULL UNIQUE,
                Description NVARCHAR(200) NULL
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AppUsers')
            CREATE TABLE AppUsers (
                UserId INT IDENTITY(1,1) PRIMARY KEY,
                Username NVARCHAR(50) NOT NULL UNIQUE,
                Email NVARCHAR(255) NULL,
                DisplayName NVARCHAR(100) NOT NULL,
                PasswordHash NVARCHAR(256) NOT NULL,
                PasswordSalt NVARCHAR(64) NOT NULL,
                RoleId INT NOT NULL FOREIGN KEY REFERENCES AppRoles(RoleId),
                IsActive BIT NOT NULL DEFAULT 1,
                LastLoginDate DATETIME NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                ForcePasswordChange BIT NOT NULL DEFAULT 0,
                FailedAttempts INT NOT NULL DEFAULT 0,
                LockoutUntil DATETIME NULL
            );");

            AddColumn(conn, "AppUsers", "FailedAttempts", "INT NOT NULL DEFAULT 0");
            AddColumn(conn, "AppUsers", "LockoutUntil", "DATETIME NULL");
            AddColumn(conn, "AppUsers", "Email", "NVARCHAR(255) NULL");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='RolePermissions')
            CREATE TABLE RolePermissions (
                PermissionId INT IDENTITY(1,1) PRIMARY KEY,
                RoleId INT NOT NULL FOREIGN KEY REFERENCES AppRoles(RoleId),
                ModuleKey NVARCHAR(50) NOT NULL,
                CanView BIT NOT NULL DEFAULT 0,
                CanCreate BIT NOT NULL DEFAULT 0,
                CanEdit BIT NOT NULL DEFAULT 0,
                CanDelete BIT NOT NULL DEFAULT 0
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='AuditLog')
            CREATE TABLE AuditLog (
                LogId INT IDENTITY(1,1) PRIMARY KEY,
                UserId INT NULL,
                Username NVARCHAR(50) NULL,
                Action NVARCHAR(100) NOT NULL,
                ModuleKey NVARCHAR(50) NULL,
                RecordId INT NULL,
                Description NVARCHAR(500) NULL,
                IPAddress NVARCHAR(50) NULL,
                LogDate DATETIME NOT NULL DEFAULT GETDATE()
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LoginAudit')
            CREATE TABLE LoginAudit (
                AuditId INT IDENTITY(1,1) PRIMARY KEY,
                UserId INT NULL,
                Username NVARCHAR(255) NULL,
                Success BIT NOT NULL,
                FailureReason NVARCHAR(200) NULL,
                IPAddress NVARCHAR(50) NULL,
                DeviceName NVARCHAR(128) NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='UserSessions')
            CREATE TABLE UserSessions (
                SessionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                UserId INT NOT NULL FOREIGN KEY REFERENCES AppUsers(UserId),
                TokenHash NVARCHAR(128) NOT NULL,
                RefreshTokenHash NVARCHAR(128) NULL,
                DeviceName NVARCHAR(128) NULL,
                IPAddress NVARCHAR(50) NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                ExpiresAt DATETIME NOT NULL,
                LastSeenAt DATETIME NULL,
                RevokedAt DATETIME NULL
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='PasswordResetTokens')
            CREATE TABLE PasswordResetTokens (
                TokenId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                UserId INT NOT NULL FOREIGN KEY REFERENCES AppUsers(UserId),
                TokenHash NVARCHAR(128) NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                ExpiresAt DATETIME NOT NULL,
                UsedAt DATETIME NULL,
                RequestedByUserId INT NULL
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='NotificationDismissals')
            CREATE TABLE NotificationDismissals (
                DismissalId INT IDENTITY(1,1) PRIMARY KEY,
                NotificationKey NVARCHAR(300) NOT NULL,
                UserId INT NULL,
                DismissedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT UQ_NotificationDismissals_User_Key UNIQUE (UserId, NotificationKey)
            );");
        }

        private void EnsureAuditMetadataColumns(SqlConnection conn)
        {
            string[] tables =
            {
                "Quotations",
                "Invoices",
                "PurchaseOrders",
                "AMCContracts",
                "Jobs",
                "Payments",
                "Employees"
            };

            foreach (string table in tables)
            {
                AddColumn(conn, table, "CreatedByUserId", "INT NULL");
                AddColumn(conn, table, "CreatedByName", "NVARCHAR(100) NULL");
                AddColumn(conn, table, "ModifiedByUserId", "INT NULL");
                AddColumn(conn, table, "ModifiedByName", "NVARCHAR(100) NULL");
                AddColumn(conn, table, "ModifiedDate", "DATETIME NULL");
            }
        }

        private void EnsureTallyIntegrationSchema(SqlConnection conn)
        {
            AddColumn(conn, "B2BClients", "TallyLedgerName", "NVARCHAR(255) NULL");
            AddColumn(conn, "B2BClients", "TallyMasterId", "INT NULL");
            AddColumn(conn, "B2BClients", "TallyGuid", "NVARCHAR(100) NULL");
            AddColumn(conn, "B2BClients", "TallySyncStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "B2BClients", "TallyLastSyncedAt", "DATETIME NULL");
            AddColumn(conn, "B2BClients", "TallySyncError", "NVARCHAR(1000) NULL");

            AddColumn(conn, "Vendors", "TallyLedgerName", "NVARCHAR(255) NULL");
            AddColumn(conn, "Vendors", "TallyMasterId", "INT NULL");
            AddColumn(conn, "Vendors", "TallyGuid", "NVARCHAR(100) NULL");
            AddColumn(conn, "Vendors", "TallySyncStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "Vendors", "TallyLastSyncedAt", "DATETIME NULL");
            AddColumn(conn, "Vendors", "TallySyncError", "NVARCHAR(1000) NULL");

            AddColumn(conn, "StockItems", "HSNCode", "NVARCHAR(20) NULL");
            AddColumn(conn, "StockItems", "TallyItemName", "NVARCHAR(255) NULL");
            AddColumn(conn, "StockItems", "TallyMasterId", "INT NULL");
            AddColumn(conn, "StockItems", "TallyGuid", "NVARCHAR(100) NULL");
            AddColumn(conn, "StockItems", "TallyUnitName", "NVARCHAR(50) NULL");
            AddColumn(conn, "StockItems", "TallyStockGroupName", "NVARCHAR(100) NULL");
            AddColumn(conn, "StockItems", "TallyGodownName", "NVARCHAR(100) NULL");
            AddColumn(conn, "StockItems", "TallySyncStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "StockItems", "TallyLastSyncedAt", "DATETIME NULL");
            AddColumn(conn, "StockItems", "TallySyncError", "NVARCHAR(1000) NULL");

            AddColumn(conn, "Invoices", "TallyVoucherId", "INT NULL");
            AddColumn(conn, "Invoices", "TallyGuid", "NVARCHAR(100) NULL");
            AddColumn(conn, "Invoices", "TallyExportStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "Invoices", "TallyExportedAt", "DATETIME NULL");
            AddColumn(conn, "Invoices", "TallyExportError", "NVARCHAR(1000) NULL");

            AddColumn(conn, "Payments", "TallyVoucherId", "INT NULL");
            AddColumn(conn, "Payments", "TallyGuid", "NVARCHAR(100) NULL");
            AddColumn(conn, "Payments", "TallyExportStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "Payments", "TallyExportedAt", "DATETIME NULL");
            AddColumn(conn, "Payments", "TallyExportError", "NVARCHAR(1000) NULL");

            AddColumn(conn, "PurchaseOrders", "TallyVoucherId", "INT NULL");
            AddColumn(conn, "PurchaseOrders", "TallyGuid", "NVARCHAR(100) NULL");
            AddColumn(conn, "PurchaseOrders", "TallyExportStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "PurchaseOrders", "TallyExportedAt", "DATETIME NULL");
            AddColumn(conn, "PurchaseOrders", "TallyExportError", "NVARCHAR(1000) NULL");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='TallyLedgerMappings')
            CREATE TABLE TallyLedgerMappings (
                MappingID INT IDENTITY(1,1) PRIMARY KEY,
                MappingType NVARCHAR(50) NOT NULL,
                ServoKey NVARCHAR(100) NULL,
                TallyLedgerName NVARCHAR(255) NOT NULL,
                TallyMasterId INT NULL,
                IsDefault BIT NOT NULL DEFAULT 0,
                UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='TallyImportBatches')
            CREATE TABLE TallyImportBatches (
                ImportBatchID INT IDENTITY(1,1) PRIMARY KEY,
                ImportType NVARCHAR(50) NOT NULL,
                SourceMode NVARCHAR(30) NOT NULL,
                StartedAt DATETIME NOT NULL DEFAULT GETDATE(),
                CompletedAt DATETIME NULL,
                CreatedCount INT NOT NULL DEFAULT 0,
                UpdatedCount INT NOT NULL DEFAULT 0,
                SkippedCount INT NOT NULL DEFAULT 0,
                ErrorCount INT NOT NULL DEFAULT 0
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='TallySyncLog')
            CREATE TABLE TallySyncLog (
                SyncLogID INT IDENTITY(1,1) PRIMARY KEY,
                Direction NVARCHAR(20) NOT NULL,
                EntityType NVARCHAR(50) NOT NULL,
                EntityID INT NULL,
                Operation NVARCHAR(50) NOT NULL,
                Status NVARCHAR(30) NOT NULL,
                Message NVARCHAR(1000) NULL,
                LocalXmlPath NVARCHAR(500) NULL,
                RawResponse NVARCHAR(MAX) NULL,
                CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'Sales' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('Sales', NULL, 'Sales', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'Purchase' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('Purchase', NULL, 'Purchase', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'CGSTOutput' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('CGSTOutput', NULL, 'Output CGST', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'SGSTOutput' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('SGSTOutput', NULL, 'Output SGST', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'IGSTOutput' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('IGSTOutput', NULL, 'Output IGST', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'CGSTInput' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('CGSTInput', NULL, 'Input CGST', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'SGSTInput' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('SGSTInput', NULL, 'Input SGST', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'IGSTInput' AND IsDefault = 1)
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('IGSTInput', NULL, 'Input IGST', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'PaymentMode' AND ServoKey = 'Cash')
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('PaymentMode', 'Cash', 'Cash', 0);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'PaymentMode' AND ServoKey = 'Bank Transfer')
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('PaymentMode', 'Bank Transfer', 'Bank', 1);
            IF NOT EXISTS (SELECT 1 FROM TallyLedgerMappings WHERE MappingType = 'PaymentMode' AND ServoKey = 'UPI')
                INSERT INTO TallyLedgerMappings (MappingType, ServoKey, TallyLedgerName, IsDefault) VALUES ('PaymentMode', 'UPI', 'UPI Collections', 0);");
        }

        private void EnsureLicenseSchema(SqlConnection conn)
        {
            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LicenseState')
            CREATE TABLE LicenseState (
                LicenseStateId INT IDENTITY(1,1) PRIMARY KEY,
                LicenseKey NVARCHAR(100) NOT NULL UNIQUE,
                CompanyId NVARCHAR(80) NULL,
                CompanyCode NVARCHAR(80) NULL,
                PlanType NVARCHAR(30) NOT NULL,
                CompanyName NVARCHAR(200) NOT NULL,
                SubscriptionStartDateUtc DATETIME NULL,
                SubscriptionEndDateUtc DATETIME NULL,
                SubscriptionStatus NVARCHAR(30) NULL,
                MaxCompanies INT NOT NULL DEFAULT 1,
                MaxDevices INT NOT NULL DEFAULT 1,
                MaxUsers INT NOT NULL DEFAULT 1,
                IssueDateUtc DATETIME NOT NULL,
                ExpiryDateUtc DATETIME NOT NULL,
                LastSuccessfulValidationUtc DATETIME NULL,
                LastAppOpenUtc DATETIME NULL,
                LastTrustedServerTimeUtc DATETIME NULL,
                LastServerValidationAttemptUtc DATETIME NULL,
                GracePeriodDays INT NOT NULL DEFAULT 3,
                Status NVARCHAR(30) NOT NULL,
                MachineFingerprintHash NVARCHAR(200) NULL,
                OnlineValidationRequired BIT NOT NULL DEFAULT 0,
                SupportLevel NVARCHAR(80) NULL,
                PlanName NVARCHAR(80) NULL,
                BillingCycle NVARCHAR(40) NULL,
                Currency NVARCHAR(10) NULL,
                PriceAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                RenewalPriceAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                IsLaunchOffer BIT NOT NULL DEFAULT 0,
                CreatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE(),
                UpdatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE()
            );");

            AddColumn(conn, "LicenseState", "PlanName", "NVARCHAR(80) NULL");
            AddColumn(conn, "LicenseState", "BillingCycle", "NVARCHAR(40) NULL");
            AddColumn(conn, "LicenseState", "Currency", "NVARCHAR(10) NULL");
            AddColumn(conn, "LicenseState", "PriceAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0");
            AddColumn(conn, "LicenseState", "RenewalPriceAmount", "DECIMAL(18,2) NOT NULL DEFAULT 0");
            AddColumn(conn, "LicenseState", "IsLaunchOffer", "BIT NOT NULL DEFAULT 0");
            AddColumn(conn, "LicenseState", "CompanyId", "NVARCHAR(80) NULL");
            AddColumn(conn, "LicenseState", "CompanyCode", "NVARCHAR(80) NULL");
            AddColumn(conn, "LicenseState", "SubscriptionStartDateUtc", "DATETIME NULL");
            AddColumn(conn, "LicenseState", "SubscriptionEndDateUtc", "DATETIME NULL");
            AddColumn(conn, "LicenseState", "SubscriptionStatus", "NVARCHAR(30) NULL");
            AddColumn(conn, "LicenseState", "LastServerValidationAttemptUtc", "DATETIME NULL");
            AddColumn(conn, "LicenseState", "OnlineValidationRequired", "BIT NOT NULL DEFAULT 0");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='LicenseEvents')
            CREATE TABLE LicenseEvents (
                LicenseEventId INT IDENTITY(1,1) PRIMARY KEY,
                LicenseKey NVARCHAR(100) NULL,
                EventType NVARCHAR(80) NOT NULL,
                Message NVARCHAR(1000) NULL,
                DeviceFingerprintHash NVARCHAR(200) NULL,
                CreatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE()
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='ActivatedDevices')
            CREATE TABLE ActivatedDevices (
                ActivatedDeviceId INT IDENTITY(1,1) PRIMARY KEY,
                LicenseKey NVARCHAR(100) NOT NULL,
                DeviceFingerprintHash NVARCHAR(200) NOT NULL,
                DeviceName NVARCHAR(120) NULL,
                ActivatedUtc DATETIME NOT NULL DEFAULT GETUTCDATE(),
                LastSeenUtc DATETIME NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT 'Active',
                CONSTRAINT UQ_ActivatedDevices_License_Device UNIQUE (LicenseKey, DeviceFingerprintHash)
            );");

            Exec(conn, @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='FeatureEntitlements')
            CREATE TABLE FeatureEntitlements (
                FeatureEntitlementId INT IDENTITY(1,1) PRIMARY KEY,
                LicenseKey NVARCHAR(100) NOT NULL,
                ModuleKey NVARCHAR(50) NOT NULL,
                IsEnabled BIT NOT NULL DEFAULT 1,
                CONSTRAINT UQ_FeatureEntitlements_License_Module UNIQUE (LicenseKey, ModuleKey)
            );");
        }

        private void SeedSecurityData(SqlConnection conn)
        {
            Exec(conn, @"
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Admin')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Admin', 'Full system access');
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Accountant')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Accountant', 'Finance, billing, collections, purchases, and financial reports');
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Dispatcher')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Dispatcher', 'Job scheduling, dispatch, service desk, technician assignment, and client coordination');
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Technician')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Technician', 'Field technician access for assigned work, service updates, inventory visibility, and site context');
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Manager')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Manager', 'Operations and commercial management');
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Accounts')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Accounts', 'Legacy finance role; use Accountant for new users');
                IF NOT EXISTS (SELECT 1 FROM AppRoles WHERE RoleName = 'Supervisor')
                    INSERT INTO AppRoles (RoleName, Description) VALUES ('Supervisor', 'Legacy field role; use Dispatcher or Technician for new users');");

            using (SqlCommand clear = new SqlCommand("DELETE FROM RolePermissions WHERE RoleId IN (SELECT RoleId FROM AppRoles)", conn))
                clear.ExecuteNonQuery();

            int adminId = GetRoleId(conn, "Admin");
            string[] modules =
            {
                "Dashboard", "Quotations", "Invoices", "Payments", "Contracts", "Employees", "Inventory",
                "Purchases", "Vendors", "Clients", "Reports", "Settings", "WorkOrders", "ServiceDesk", "GeoIntelligence", "Payroll", "MasterData"
            };

            List<int> roleIds = new List<int>();
            using (SqlCommand roleCommand = new SqlCommand("SELECT RoleId FROM AppRoles", conn))
            using (SqlDataReader reader = roleCommand.ExecuteReader())
            {
                while (reader.Read())
                    roleIds.Add(Convert.ToInt32(reader["RoleId"]));
            }

            foreach (int roleId in roleIds)
            {
                foreach (string module in modules)
                    InsertPermission(conn, roleId, module, true, true, true, true);
            }

            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM AppUsers", conn))
            {
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                if (count > 0)
                    return;
            }

            string salt = "bcrypt";
            string hash = SecurityHelpers.HashPasswordWithBcrypt("Admin@123");
            using (SqlCommand insert = new SqlCommand(@"
                INSERT INTO AppUsers
                    (Username, DisplayName, PasswordHash, PasswordSalt, RoleId, IsActive, ForcePasswordChange)
                VALUES
                    ('admin', 'Administrator', @hash, @salt, @roleId, 1, 1);", conn))
            {
                insert.Parameters.AddWithValue("@hash", hash);
                insert.Parameters.AddWithValue("@salt", salt);
                insert.Parameters.AddWithValue("@roleId", adminId);
                insert.ExecuteNonQuery();
            }

            SecurityHelpers.LogAuthEvent("Default admin created. Change password on first login.");
        }

        private int GetRoleId(SqlConnection conn, string roleName)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 RoleId FROM AppRoles WHERE RoleName = @name", conn))
            {
                cmd.Parameters.AddWithValue("@name", roleName ?? string.Empty);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void InsertPermission(SqlConnection conn, int roleId, string moduleKey, bool canView, bool canCreate, bool canEdit, bool canDelete)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO RolePermissions (RoleId, ModuleKey, CanView, CanCreate, CanEdit, CanDelete)
                VALUES (@roleId, @moduleKey, @canView, @canCreate, @canEdit, @canDelete);", conn))
            {
                cmd.Parameters.AddWithValue("@roleId", roleId);
                cmd.Parameters.AddWithValue("@moduleKey", moduleKey ?? string.Empty);
                cmd.Parameters.AddWithValue("@canView", canView ? 1 : 0);
                cmd.Parameters.AddWithValue("@canCreate", canCreate ? 1 : 0);
                cmd.Parameters.AddWithValue("@canEdit", canEdit ? 1 : 0);
                cmd.Parameters.AddWithValue("@canDelete", canDelete ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        private void ApplyConfiguredTenantSettings(SqlConnection conn)
        {
            UpsertSetting(conn, "TenantCode", GetCurrentTenantCode());
            UpsertSetting(conn, "TenantDatabaseName", _databaseName);

            UpsertSettingFromConfig(conn, "CompanyName", "CompanyName");
            UpsertSettingFromConfig(conn, "CompanyGST", "CompanyGSTIN");
            UpsertSettingFromConfig(conn, "CompanyGSTIN", "CompanyGSTIN");
            UpsertSettingFromConfig(conn, "CompanyPAN", "CompanyPAN");
            UpsertSettingFromConfig(conn, "CompanyPhone", "CompanyPhone");
            UpsertSettingFromConfig(conn, "CompanyEmail", "CompanyEmail");
            UpsertSettingFromConfig(conn, "CompanyAddress", "CompanyAddress");
            UpsertSettingFromConfig(conn, "CompanyState", "CompanyState");
            UpsertSettingFromConfig(conn, "InvoicePrefix", "InvoicePrefix");
            UpsertSettingFromConfig(conn, "DefaultPlaceOfSupply", "DefaultPlaceOfSupply");
            UpsertSettingFromConfig(conn, "OfficeLatitude", "OfficeLatitude");
            UpsertSettingFromConfig(conn, "OfficeLongitude", "OfficeLongitude");
        }

        private void UpsertSettingFromConfig(SqlConnection conn, string settingKey, string configKey)
        {
            string value = ConfigService.Get("Company", configKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
                UpsertSetting(conn, settingKey, value);
        }

        private void UpsertSetting(SqlConnection conn, string key, string value)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM CompanySettings WHERE SettingKey = @key)
                    UPDATE CompanySettings SET SettingValue = @value, UpdatedDate = GETDATE() WHERE SettingKey = @key;
                ELSE
                    INSERT INTO CompanySettings (SettingKey, SettingValue) VALUES (@key, @value);", conn))
            {
                cmd.Parameters.AddWithValue("@key", key ?? string.Empty);
                cmd.Parameters.AddWithValue("@value", value ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Runs the one-time client purchase-order reset introduced for the Madhusuman fresh-start update.</summary>
        private void ApplyPurchaseOrderFreshStartReset(SqlConnection conn)
        {
            const string resetKey = "PurchaseOrderFreshStartReset_1_0_114_0";

            if (HasSettingValue(conn, resetKey, "1"))
                return;

            List<string> purchaseOrderPdfPaths = GetPurchaseOrderPdfPaths(conn);

            Exec(conn, @"
                IF OBJECT_ID('dbo.PendingCharges', 'U') IS NOT NULL DELETE FROM PendingCharges;
                IF OBJECT_ID('dbo.VendorAdvancePayments', 'U') IS NOT NULL UPDATE VendorAdvancePayments SET POID = NULL WHERE POID IS NOT NULL;
                IF OBJECT_ID('dbo.PurchaseLineItems', 'U') IS NOT NULL DELETE FROM PurchaseLineItems;
                IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NOT NULL DELETE FROM PurchaseOrders;
                IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL UPDATE Vendors SET TotalPurchased = 0;");

            DeletePurchaseOrderFiles(purchaseOrderPdfPaths);
            DeletePurchaseOrderFolderContents(@"C:\HVAC_PRO_MSE\RECEIPTS");
            DeletePurchaseOrderFolderContents(@"C:\HVAC_PRO_MSE\DATABASE\Purchase Order AMC");

            UpsertSetting(conn, resetKey, "1");
            UpsertSetting(conn, "PurchaseOrderFreshStartResetAppliedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        /// <summary>Removes known development/demo records that must not appear in live client databases.</summary>
        private void ApplyDevelopmentTestDataCleanup(SqlConnection conn)
        {
            const string cleanupKey = "DevelopmentTestDataCleanup_1_0_117_0";

            if (HasSettingValue(conn, cleanupKey, "1"))
                return;

            Exec(conn, @"
                IF OBJECT_ID('dbo.SupplierItemPrices', 'U') IS NOT NULL
                BEGIN
                    DELETE sip
                    FROM SupplierItemPrices sip
                    INNER JOIN Vendors v ON v.VendorID = sip.VendorID
                    WHERE v.VendorName IN (
                        N'H.K. Enterprises',
                        N'Infinity HVAC Spares & Tools Pvt. Ltd',
                        N'Infinity Hvac Spares & Tools Pvt. Ltd',
                        N'Climate Engineering Company',
                        N'Bharat Sales Thane',
                        N'Flowair Engineer',
                        N'Client AMC PO Archive'
                    );
                END;

                IF OBJECT_ID('dbo.Vendors', 'U') IS NOT NULL
                BEGIN
                    DELETE FROM Vendors
                    WHERE VendorName IN (
                        N'H.K. Enterprises',
                        N'Infinity HVAC Spares & Tools Pvt. Ltd',
                        N'Infinity Hvac Spares & Tools Pvt. Ltd',
                        N'Climate Engineering Company',
                        N'Bharat Sales Thane',
                        N'Flowair Engineer',
                        N'Client AMC PO Archive'
                    );
                END;

                IF OBJECT_ID('dbo.QuotationLineItems', 'U') IS NOT NULL
                BEGIN
                    DELETE li
                    FROM QuotationLineItems li
                    INNER JOIN Quotations t ON t.BidID = li.TenderBidId
                    WHERE ISNULL(t.TenderName, '') LIKE '%Demo%'
                       OR ISNULL(t.TenderName, '') LIKE '%Test%';
                END;

                IF OBJECT_ID('dbo.Quotations', 'U') IS NOT NULL
                BEGIN
                    DELETE FROM Quotations
                    WHERE ISNULL(TenderName, '') LIKE '%Demo%'
                       OR ISNULL(TenderName, '') LIKE '%Test%';
                END;");

            UpsertSetting(conn, cleanupKey, "1");
            UpsertSetting(conn, "DevelopmentTestDataCleanupAppliedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        /// <summary>Checks whether a CompanySettings value already matches the expected marker.</summary>
        private bool HasSettingValue(SqlConnection conn, string key, string expectedValue)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT SettingValue FROM CompanySettings WHERE SettingKey = @key;", conn))
            {
                cmd.Parameters.AddWithValue("@key", key ?? string.Empty);
                object value = cmd.ExecuteScalar();
                return value != null
                    && value != DBNull.Value
                    && string.Equals(Convert.ToString(value), expectedValue, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>Collects stored or archived purchase-order PDF paths before the old PO rows are deleted.</summary>
        private List<string> GetPurchaseOrderPdfPaths(SqlConnection conn)
        {
            var paths = new List<string>();
            using (SqlCommand cmd = new SqlCommand(@"
                IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NOT NULL
                    SELECT ReceiptImagePath, Notes FROM PurchaseOrders;", conn))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    AddPurchaseOrderPdfPath(paths, reader["ReceiptImagePath"] as string);
                    AddPurchaseOrderPdfPath(paths, ExtractPurchaseOrderSourcePath(reader["Notes"] as string));
                }
            }

            return paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Adds a purchase-order PDF path when it is safe for this migration to remove.</summary>
        private void AddPurchaseOrderPdfPath(List<string> paths, string path)
        {
            if (paths == null || string.IsNullOrWhiteSpace(path))
                return;

            string cleaned = path.Trim().Trim('"');
            if (!string.Equals(Path.GetExtension(cleaned), ".pdf", StringComparison.OrdinalIgnoreCase))
                return;

            if (IsSafePurchaseOrderDeletePath(cleaned))
                paths.Add(cleaned);
        }

        /// <summary>Extracts the archived source PDF path stored inside imported purchase-order notes.</summary>
        private string ExtractPurchaseOrderSourcePath(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return null;

            Match match = Regex.Match(notes, @"Source\s+Path:\s*(?<path>.+?)(\s+\|\s+|$)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["path"].Value.Trim() : null;
        }

        /// <summary>Returns true for ServoERP-owned PO file locations only.</summary>
        private bool IsSafePurchaseOrderDeletePath(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                return IsPathUnder(fullPath, @"C:\HVAC_PRO_MSE\RECEIPTS")
                    || IsPathUnder(fullPath, @"C:\HVAC_PRO_MSE\DATABASE\Purchase Order AMC")
                    || IsPathUnder(fullPath, Path.GetTempPath());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Deletes stored purchase-order PDFs from the approved ServoERP file locations.</summary>
        private void DeletePurchaseOrderFiles(IEnumerable<string> paths)
        {
            foreach (string path in paths ?? Enumerable.Empty<string>())
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("DatabaseManager.DeletePurchaseOrderFiles", ex);
                }
            }
        }

        /// <summary>Clears files from a ServoERP purchase-order folder without deleting the folder itself.</summary>
        private void DeletePurchaseOrderFolderContents(string folder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    return;

                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError("DatabaseManager.DeletePurchaseOrderFolderContents.File", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DatabaseManager.DeletePurchaseOrderFolderContents", ex);
            }
        }

        /// <summary>Checks whether a resolved file path is inside a trusted ServoERP cleanup root.</summary>
        private bool IsPathUnder(string fullPath, string root)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(root))
                return false;

            string resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string resolvedPath = Path.GetFullPath(fullPath);
            return resolvedPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private void Exec(SqlConnection conn, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 60;
                cmd.ExecuteNonQuery();
            }
        }

        private void AddColumn(SqlConnection conn, string table, string column, string definition)
        {
            string sql = $@"IF OBJECT_ID('{table}', 'U') IS NOT NULL
                AND NOT EXISTS (
                SELECT * FROM sys.columns
                WHERE object_id = OBJECT_ID('{table}') AND name = '{column}')
                ALTER TABLE {table} ADD {column} {definition};";
            Exec(conn, sql);
        }

        private void AlterColumn(SqlConnection conn, string table, string column, string definition)
        {
            string sql = $@"IF EXISTS (
                SELECT * FROM sys.columns
                WHERE object_id = OBJECT_ID('{table}') AND name = '{column}')
                ALTER TABLE {table} ALTER COLUMN {column} {definition};";
            Exec(conn, sql);
        }

        /// <summary>Allows client-level records to be saved before a service site is known.</summary>
        private void EnsureOptionalSiteReferences(SqlConnection conn)
        {
            AlterColumn(conn, "Jobs", "SiteID", "INT NULL");
            AlterColumn(conn, "AMCContracts", "SiteID", "INT NULL");
            AlterColumn(conn, "Invoices", "SiteID", "INT NULL");
            AlterColumn(conn, "Quotations", "SiteID", "INT NULL");
            AlterColumn(conn, "PurchaseOrders", "SiteID", "INT NULL");
        }

        private void EnsureSetting(SqlConnection conn, string key, string value)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                IF NOT EXISTS (SELECT 1 FROM CompanySettings WHERE SettingKey = @k)
                    INSERT INTO CompanySettings (SettingKey, SettingValue) VALUES (@k, @v);", conn))
            {
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", value ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }
    }
}



