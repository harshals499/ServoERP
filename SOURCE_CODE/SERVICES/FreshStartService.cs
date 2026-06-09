using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class FreshStartResult
    {
        public List<string> ClearedModules { get; } = new List<string>();
    }

    public class FreshStartService
    {
        private sealed class ResetTarget
        {
            public string TableName { get; set; }
            public string DisplayName { get; set; }
            public bool IncludeInSummary { get; set; } = true;
        }

        private static readonly ResetTarget[] Targets =
        {
            // Dependent transactional rows first.
            new ResetTarget { TableName = "InventoryUsageLog", DisplayName = "Invoices", IncludeInSummary = false },
            new ResetTarget { TableName = "InvoiceInventoryReservations", DisplayName = "Invoices", IncludeInSummary = false },
            new ResetTarget { TableName = "InvoiceLineItems", DisplayName = "Invoices", IncludeInSummary = false },
            new ResetTarget { TableName = "Payments", DisplayName = "Payments" },
            new ResetTarget { TableName = "SLALogs", DisplayName = "SLA Logs" },
            new ResetTarget { TableName = "PendingCharges", DisplayName = "Pending charges", IncludeInSummary = false },
            new ResetTarget { TableName = "JobActivityLog", DisplayName = "Jobs", IncludeInSummary = false },
            new ResetTarget { TableName = "JobChecklistItems", DisplayName = "Jobs", IncludeInSummary = false },
            new ResetTarget { TableName = "JobPartsUsed", DisplayName = "Jobs", IncludeInSummary = false },
            new ResetTarget { TableName = "PurchaseLineItems", DisplayName = "Purchases", IncludeInSummary = false },
            new ResetTarget { TableName = "VendorAdvancePayments", DisplayName = "Vendor advances", IncludeInSummary = false },
            new ResetTarget { TableName = "QuotationLineItems", DisplayName = "Quotations", IncludeInSummary = false },
            new ResetTarget { TableName = "PayrollEntries", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "StatutoryPayments", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "EmployeeAttendance", DisplayName = "Attendance records", IncludeInSummary = false },
            new ResetTarget { TableName = "AttendanceRecords", DisplayName = "Attendance records", IncludeInSummary = false },
            new ResetTarget { TableName = "TDSCalculations", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "EmployeeLoans", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "SalaryAdvances", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "LeaveBalances", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "EmployeeSkills", DisplayName = "Employees", IncludeInSummary = false },
            new ResetTarget { TableName = "EmployeeDocuments", DisplayName = "Employees", IncludeInSummary = false },
            new ResetTarget { TableName = "SalaryStructure", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "SalaryStructures", DisplayName = "Salary", IncludeInSummary = false },
            new ResetTarget { TableName = "ClientDocuments", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "ClientAssets", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "ClientContacts", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "ClientTeam", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "ClientActivity", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "ClientPriceMemory", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "ServiceRateCards", DisplayName = "Clients", IncludeInSummary = false },
            new ResetTarget { TableName = "NotificationDismissals", DisplayName = "Notifications", IncludeInSummary = false },

            // Parent transactional rows.
            new ResetTarget { TableName = "Invoices", DisplayName = "Invoices" },
            new ResetTarget { TableName = "Jobs", DisplayName = "Jobs" },
            new ResetTarget { TableName = "Quotations", DisplayName = "Quotations" },
            new ResetTarget { TableName = "PurchaseOrders", DisplayName = "Purchases" },
            new ResetTarget { TableName = "PayrollRuns", DisplayName = "Salary" },

            // Master data parents, ordered by foreign-key dependency.
            new ResetTarget { TableName = "AMCContracts", DisplayName = "Contracts" },
            new ResetTarget { TableName = "ClientSites", DisplayName = "Sites" },
            new ResetTarget { TableName = "StockItems", DisplayName = "Inventory", IncludeInSummary = false },
            new ResetTarget { TableName = "SupplierItemPrices", DisplayName = "Vendors", IncludeInSummary = false },
            new ResetTarget { TableName = "Vendors", DisplayName = "Vendors" },
            new ResetTarget { TableName = "Technicians", DisplayName = "Technicians" },
            new ResetTarget { TableName = "Employees", DisplayName = "Employees" },
            new ResetTarget { TableName = "B2BClients", DisplayName = "Clients" },
            new ResetTarget { TableName = "CompanySettings", DisplayName = "Settings" }
        };

        private readonly DatabaseManager _db = new DatabaseManager();

        public FreshStartResult RunFreshStart()
        {
            var result = new FreshStartResult();

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        DisableForeignKeys(conn, tx);

                        foreach (ResetTarget target in Targets)
                        {
                            if (!TableExists(conn, tx, target.TableName))
                                continue;

                            ExecuteNonQuery(conn, tx, "DELETE FROM dbo." + target.TableName + ";");
                            TryReseed(conn, tx, target.TableName);
                            if (target.IncludeInSummary && !result.ClearedModules.Contains(target.DisplayName))
                                result.ClearedModules.Add(target.DisplayName);
                        }

                        DeleteAllNonSecurityTables(conn, tx);

                        EnableForeignKeys(conn, tx);
                        tx.Commit();
                    }
                    catch
                    {
                        try
                        {
                            EnableForeignKeys(conn, tx);
                        }
                        catch
                        {
                        }

                        tx.Rollback();
                        throw;
                    }
                }
            }

            ConfigService.Set("Tenant", "SeedDemoData", "false");
            AppDataCache.Clear();
            DeleteLocalBusinessFiles();
            SessionManager.LogAction("FRESH_START", "Settings", null, "Fresh Start cleared all tenant data including master data.");
            AppLogger.LogInfo("Fresh Start completed for all tenant data.");
            return result;
        }

        private static void DeleteLocalBusinessFiles()
        {
            string root = @"C:\HVAC_PRO_MSE";
            string[] targets =
            {
                Path.Combine(root, "MSE DATA"),
                Path.Combine(root, "Invoice"),
                Path.Combine(root, "PAYSLIPS"),
                Path.Combine(root, "PAYROLL_EXPORTS"),
                Path.Combine(root, "RECEIPTS"),
                Path.Combine(root, "CLIENT_DATA_UPLOADS"),
                Path.Combine(root, "REPORTS"),
                Path.Combine(root, "DATABASE", "Purchase Order AMC"),
                Path.Combine(root, "SOURCE_CODE", "Payroll"),
                Path.Combine(root, "SOURCE_CODE", "bin", "Release", "CompanyTemplates"),
                Path.Combine(root, "LOGS", "invoice_reference_render")
            };

            foreach (string target in targets)
                DeleteInsideRoot(root, target);

            string[] files =
            {
                Path.Combine(root, "LOGS", "invoice_header_runtime_check.png"),
                Path.Combine(root, "LOGS", "invoice_header_check.html"),
                Path.Combine(root, "Resources", "Branding", "official_invoice_header.png"),
                Path.Combine(root, "SOURCE_CODE", "Resources", "Branding", "official_invoice_header.png"),
                Path.Combine(root, "SOURCE_CODE", "bin", "Release", "Resources", "Branding", "official_invoice_header.png")
            };

            foreach (string file in files)
                DeleteInsideRoot(root, file);

            foreach (string dir in new[] { "Invoice", "PAYSLIPS", "PAYROLL_EXPORTS", "RECEIPTS", "DATABASE" })
                Directory.CreateDirectory(Path.Combine(root, dir));
        }

        private static void DeleteInsideRoot(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
                return;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete outside application data folder: " + fullPath);

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            else
                File.Delete(fullPath);
        }

        private static bool TableExists(SqlConnection conn, SqlTransaction tx, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'U';", conn, tx))
            {
                cmd.Parameters.AddWithValue("@name", "dbo." + tableName);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static void ExecuteNonQuery(SqlConnection conn, SqlTransaction tx, string sql)
        {
            using (SqlCommand cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();
            }
        }

        private static void TryReseed(SqlConnection conn, SqlTransaction tx, string tableName)
        {
            if (!HasIdentityColumn(conn, tx, tableName))
                return;

            using (SqlCommand cmd = new SqlCommand("DBCC CHECKIDENT ('dbo." + tableName + "', RESEED, 0);", conn, tx))
            {
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();
            }
        }

        private static bool HasIdentityColumn(SqlConnection conn, SqlTransaction tx, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM sys.identity_columns
                WHERE object_id = OBJECT_ID(@name);", conn, tx))
            {
                cmd.Parameters.AddWithValue("@name", "dbo." + tableName);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static void DeleteAllNonSecurityTables(SqlConnection conn, SqlTransaction tx)
        {
            ExecuteNonQuery(conn, tx, @"
                DECLARE @sql NVARCHAR(MAX) = N'';

                SELECT @sql = @sql + N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';' + CHAR(13)
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'dbo'
                  AND t.name NOT IN (N'AppUsers', N'AppRoles', N'RolePermissions')
                ORDER BY t.name;

                EXEC sp_executesql @sql;");

            ExecuteNonQuery(conn, tx, @"
                DECLARE @reseed NVARCHAR(MAX) = N'';

                SELECT @reseed = @reseed + N'DBCC CHECKIDENT (''' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N''', RESEED, 0) WITH NO_INFOMSGS;' + CHAR(13)
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = N'dbo'
                  AND t.name NOT IN (N'AppUsers', N'AppRoles', N'RolePermissions')
                  AND EXISTS (SELECT 1 FROM sys.identity_columns ic WHERE ic.object_id = t.object_id);

                EXEC sp_executesql @reseed;");
        }

        private static void DisableForeignKeys(SqlConnection conn, SqlTransaction tx)
        {
            ExecuteNonQuery(conn, tx, @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' NOCHECK CONSTRAINT ALL;'
                FROM sys.tables;
                EXEC sp_executesql @sql;");
        }

        private static void EnableForeignKeys(SqlConnection conn, SqlTransaction tx)
        {
            ExecuteNonQuery(conn, tx, @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N' WITH CHECK CHECK CONSTRAINT ALL;'
                FROM sys.tables;
                EXEC sp_executesql @sql;");
        }
    }
}
