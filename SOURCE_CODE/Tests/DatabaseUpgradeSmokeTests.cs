using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class DatabaseUpgradeSmokeTests
    {
        public static string RunAll()
        {
            Assert(DatabaseUpgradeService.Hash("SELECT 1;\r\nSELECT 2;\r\n") == DatabaseUpgradeService.Hash("SELECT 1;\nSELECT 2;\n"), "Line-ending-only changes altered migration identity");
            string name = "ServoERP_UpgradeQA_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var builder = new SqlConnectionStringBuilder { DataSource = @".\SQLEXPRESS", IntegratedSecurity = true, InitialCatalog = "master", TrustServerCertificate = true };
            using (var master = new SqlConnection(builder.ConnectionString))
            {
                master.Open();
                // Isolated QA database creation is DDL; retained for inspection, never drops business data.
                master.Execute("IF DB_ID(@Name) IS NULL EXEC('CREATE DATABASE [' + @Name + ']')", new { Name = name });
            }
            builder.InitialCatalog = name;
            string cs = builder.ConnectionString;
            int calls = 0;
            Action legacy = () =>
            {
                calls++;
                using (var c = new SqlConnection(cs))
                {
                    c.Open();
                    c.Execute("IF OBJECT_ID('dbo.UpgradeQAData','U') IS NULL CREATE TABLE dbo.UpgradeQAData(Id int PRIMARY KEY, Amount decimal(18,2)); IF NOT EXISTS(SELECT 1 FROM dbo.UpgradeQAData WHERE Id=1) INSERT dbo.UpgradeQAData VALUES(1,123.45);");
                }
            };
            var scripts = new SortedDictionary<string, string>();
            scripts.Add("0003_SafeUpgradePolicy", DatabaseUpgradeService.ReadScripts()["0003_SafeUpgradePolicy"]);
            DatabaseUpgradeService.Run(cs, "1.1.509.0", legacy, scripts, false);
            using (var c = new SqlConnection(cs))
            {
                c.Open();
                Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.ServoSchemaMigrations WHERE Status='Completed'") == 3, "Initial numbered journal incomplete");
                string backup = c.QuerySingle<string>("SELECT BackupPath FROM dbo.ServoSchemaMigrations WHERE MigrationId='0001_UpgradeJournal'");
                DatabaseUpgradeService.VerifyBackup(c, backup);
                Assert(c.QuerySingle<decimal>("SELECT Amount FROM dbo.UpgradeQAData WHERE Id=1") == 123.45m, "Fixture data mismatch");
                DatabaseUpgradeService.Run(cs, "1.1.509.0", legacy, scripts, false);
                Assert(calls == 1, "Completed migration ran again");
                var changed = new SortedDictionary<string,string>(scripts);
                changed["0003_SafeUpgradePolicy"] += "\n-- changed shipped script";
                ExpectBlocked(() => DatabaseUpgradeService.Run(cs, "1.1.509.0", legacy, changed, false), "Changed migration was accepted");
                scripts.Add("0004_TransactionalFailureQA", @"IF OBJECT_ID('dbo.UpgradeQARolledBack','U') IS NULL CREATE TABLE dbo.UpgradeQARolledBack(Id int);
IF OBJECT_ID('dbo.UpgradeQARepair','U') IS NULL THROW 51000,'QA injected migration failure',1;");
                ExpectBlocked(() => DatabaseUpgradeService.Run(cs, "1.1.510.0", legacy, scripts, false), "Injected failure was accepted");
                Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.ServoSchemaMigrations WHERE Status='Failed'") == 1, "Failure not durable");
                Assert(c.QuerySingle<int>("SELECT CASE WHEN OBJECT_ID('dbo.UpgradeQARolledBack','U') IS NULL THEN 1 ELSE 0 END") == 1, "Failed DDL was not rolled back");
                string original = c.QuerySingle<string>("SELECT BackupPath FROM dbo.ServoSchemaMigrations WHERE Status='Failed'");
                ExpectBlocked(() => DatabaseUpgradeService.Run(cs, "1.1.510.0", legacy, scripts, false), "Failed upgrade did not block startup");
                c.Execute("IF OBJECT_ID('dbo.UpgradeQARepair','U') IS NULL CREATE TABLE dbo.UpgradeQARepair(Id int);");
                DatabaseUpgradeService.Run(cs, "1.1.510.0", legacy, scripts, true);
                Assert(c.QuerySingle<string>("SELECT BackupPath FROM dbo.ServoSchemaMigrations WHERE MigrationId='0004_TransactionalFailureQA'") == original, "Retry replaced recovery backup");
                Assert(c.QuerySingle<int>("SELECT COUNT(*) FROM dbo.ServoSchemaMigrations WHERE Status!='Completed'") == 0, "Retry did not finish");
                c.Execute("UPDATE dbo.ServoSchemaMigrations SET Status='Running' WHERE MigrationId='0004_TransactionalFailureQA'");
                ExpectBlocked(() => DatabaseUpgradeService.Run(cs, "1.1.510.0", legacy, scripts, false), "Interrupted upgrade accepted");
                c.Execute("UPDATE dbo.ServoSchemaMigrations SET BackupPath=@Path WHERE MigrationId='0004_TransactionalFailureQA'", new { Path = original + ".missing" });
                ExpectBlocked(() => DatabaseUpgradeService.Run(cs, "1.1.510.0", legacy, scripts, true), "Unverified backup accepted for retry");
                c.Execute("UPDATE dbo.ServoSchemaMigrations SET BackupPath=@Path WHERE MigrationId='0004_TransactionalFailureQA'", new { Path = original });
                DatabaseUpgradeService.Run(cs, "1.1.510.0", legacy, scripts, true);
                ExpectBlocked(() => DatabaseUpgradeService.Run(cs, "1.1.509.0", legacy, scripts, false), "Downgrade accepted");
                string restoredName = name + "_Restore";
                var files = c.Query<DatabaseFile>("SELECT name AS Name, physical_name AS PhysicalName FROM sys.database_files ORDER BY file_id");
                string restoreSql = "RESTORE DATABASE [" + restoredName + "] FROM DISK=@Backup WITH ";
                var parameters = new DynamicParameters(new { Backup = original });
                int index = 0;
                foreach (var file in files)
                {
                    string physical = file.PhysicalName;
                    string target = physical.Substring(0, physical.LastIndexOf('\\') + 1) + restoredName + "_" + index + Path.GetExtension(physical);
                    if (index > 0) restoreSql += ", ";
                    restoreSql += "MOVE @Logical" + index + " TO @Target" + index;
                    parameters.Add("Logical" + index, file.Name);
                    parameters.Add("Target" + index, target);
                    index++;
                }
                // Actual restore goes to a new, uniquely named QA database, without REPLACE.
                c.Execute(restoreSql, parameters, commandTimeout: 600);
                Assert(c.QuerySingle<decimal>("SELECT Amount FROM [" + restoredName + "].dbo.UpgradeQAData WHERE Id=1") == 123.45m, "Restored backup lost original data");

                int concurrentCalls = 0;
                Action concurrentLegacy = () => { Interlocked.Increment(ref concurrentCalls); Thread.Sleep(300); };
                Task.WaitAll(
                    Task.Run(() => DatabaseUpgradeService.Run(cs, "1.1.511.0", concurrentLegacy, scripts, false)),
                    Task.Run(() => DatabaseUpgradeService.Run(cs, "1.1.511.0", concurrentLegacy, scripts, false)));
                Assert(concurrentCalls == 1, "Concurrent terminals ran the migration twice");
            }
            return "PASS verified backup identity/checksums, actual restore preserving data, numbered journal, repeat startup, hash integrity, transaction rollback, failure gate, controlled retry, backup preservation, interrupted upgrade, missing backup refusal, downgrade block and concurrent terminal serialization. QA databases retained: " + name + " and " + name + "_Restore";
        }
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class DatabaseFile
        {
            public string Name { get; set; }
            public string PhysicalName { get; set; }
        }
        private static void ExpectBlocked(Action action, string message)
        {
            try { action(); } catch (DatabaseUpgradeException) { return; }
            throw new InvalidOperationException(message);
        }
    }
}
