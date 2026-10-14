using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class DatabaseUpgradeException : Exception
    {
        public DatabaseUpgradeException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>Serializes upgrades per office database and fails closed before business access.</summary>
    public static class DatabaseUpgradeService
    {
        internal static bool RetryFailedUpgrade { get; set; }
        [ThreadStatic] private static bool _legacyUpgrade;
        internal static bool IsLegacyUpgrade => _legacyUpgrade;
        private const string Journal = "dbo.ServoSchemaMigrations";
        private const string JournalSql = @"IF OBJECT_ID('dbo.ServoSchemaMigrations','U') IS NULL
CREATE TABLE dbo.ServoSchemaMigrations (
 MigrationId nvarchar(180) NOT NULL PRIMARY KEY,
 ReleaseVersion nvarchar(40) NOT NULL,
 ScriptHash varchar(64) NOT NULL,
 Status varchar(20) NOT NULL,
 BackupPath nvarchar(2000) NOT NULL,
 StartedUtc datetime2 NOT NULL,
 CompletedUtc datetime2 NULL,
 ErrorMessage nvarchar(2000) NULL);";

        internal sealed class Entry
        {
            public string MigrationId { get; set; }
            public string ReleaseVersion { get; set; }
            public string ScriptHash { get; set; }
            public string Status { get; set; }
            public string BackupPath { get; set; }
            public string ErrorMessage { get; set; }
        }

        public static void Upgrade(string connectionString, Action legacySchemaUpgrade)
        {
            Run(connectionString, Assembly.GetExecutingAssembly().GetName().Version.ToString(), legacySchemaUpgrade,
                ReadScripts(), RetryFailedUpgrade);
        }

        internal static void Run(string connectionString, string version, Action legacySchemaUpgrade,
            IDictionary<string, string> scripts, bool retry)
        {
            string backupPath = null;
            string activeId = null;
            bool journalReady = false;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                try
                {
                    // SQL metadata, backup/restore commands and migration DDL intentionally use ADO.NET.
                    int lockResult = connection.QuerySingle<int>(@"DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource=N'ServoERP.SchemaUpgrade', @LockMode='Exclusive',
 @LockOwner='Session', @LockTimeout=60000; SELECT @result;", commandTimeout: 65);
                    if (lockResult < 0)
                        throw new DatabaseUpgradeException("Another terminal is upgrading the office database. Close ServoERP and retry after that upgrade finishes.");

                    journalReady = connection.QuerySingle<int>("SELECT CASE WHEN OBJECT_ID('dbo.ServoSchemaMigrations','U') IS NULL THEN 0 ELSE 1 END") == 1;
                    var entries = journalReady ? connection.Query<Entry>("SELECT MigrationId, ReleaseVersion, ScriptHash, Status, BackupPath, ErrorMessage FROM " + Journal).ToList() : new List<Entry>();
                    Version targetVersion = Version.Parse(version);
                    if (entries.Any(e => Version.Parse(e.ReleaseVersion) > targetVersion))
                        throw new DatabaseUpgradeException("This office database was opened by a newer ServoERP release. Install the same or a newer supported release. Downgrading the database is blocked.");

                    Entry incomplete = entries.FirstOrDefault(e => e.Status != "Completed");
                    if (incomplete != null && (!retry || incomplete.ReleaseVersion != version))
                        throw Recovery(incomplete.BackupPath, incomplete.MigrationId, "A previous upgrade did not finish. Use the original release for the controlled retry. " + SensitiveDataRedactor.Redact(incomplete.ErrorMessage ?? "The upgrade was interrupted before completion."));

                    var pending = new SortedDictionary<string, string>(StringComparer.Ordinal);
                    // Existing schema guards remain the compatibility bridge; each release is journalled separately.
                    pending.Add("0002_LegacySchema_" + version, "legacy-schema-guards:" + version);
                    foreach (var script in scripts) pending.Add(script.Key, script.Value);
                    if (incomplete != null && !pending.ContainsKey(incomplete.MigrationId))
                        throw Recovery(incomplete.BackupPath, incomplete.MigrationId, "The interrupted migration is not included in this release. Reinstall the original release before retrying.");
                    Entry journalEntry = entries.FirstOrDefault(e => e.MigrationId == "0001_UpgradeJournal");
                    if (journalEntry != null && journalEntry.ScriptHash != Hash(JournalSql))
                        throw Recovery(journalEntry.BackupPath, journalEntry.MigrationId, "The migration journal definition does not match the installed release.");
                    foreach (var item in pending)
                    {
                        Entry applied = entries.FirstOrDefault(e => e.MigrationId == item.Key);
                        if (applied != null && applied.ScriptHash != Hash(item.Value))
                            throw Recovery(applied.BackupPath, item.Key, "A previously recorded migration has changed. Reinstall the original verified release.");
                    }
                    var outstanding = pending.Where(p => !entries.Any(e => e.MigrationId == p.Key && e.Status == "Completed")).ToList();
                    if (journalReady && outstanding.Count == 0) return;

                    // Recover with the original pre-upgrade backup, never a backup of partially migrated data.
                    backupPath = incomplete == null ? CreateVerifiedBackup(connection, version) : incomplete.BackupPath;
                    if (incomplete != null) VerifyBackup(connection, backupPath);
                    Execute(connection, JournalSql);
                    journalReady = true;
                    connection.Execute(@"IF NOT EXISTS (SELECT 1 FROM dbo.ServoSchemaMigrations WHERE MigrationId=@Id)
INSERT dbo.ServoSchemaMigrations(MigrationId,ReleaseVersion,ScriptHash,Status,BackupPath,StartedUtc,CompletedUtc)
VALUES(@Id,@Version,@Hash,'Completed',@Backup,SYSUTCDATETIME(),SYSUTCDATETIME());",
                        new { Id = "0001_UpgradeJournal", Version = version, Hash = Hash(JournalSql), Backup = backupPath });

                    foreach (var migration in outstanding)
                    {
                        activeId = migration.Key;
                        connection.Execute(@"IF EXISTS (SELECT 1 FROM dbo.ServoSchemaMigrations WHERE MigrationId=@Id)
UPDATE dbo.ServoSchemaMigrations SET Status='Running',ErrorMessage=NULL WHERE MigrationId=@Id;
ELSE INSERT dbo.ServoSchemaMigrations(MigrationId,ReleaseVersion,ScriptHash,Status,BackupPath,StartedUtc)
VALUES(@Id,@Version,@Hash,'Running',@Backup,SYSUTCDATETIME());",
                            new { Id = activeId, Version = version, Hash = Hash(migration.Value), Backup = backupPath });
                        if (activeId.StartsWith("0002_LegacySchema_", StringComparison.Ordinal))
                        {
                            _legacyUpgrade = true;
                            try { legacySchemaUpgrade(); }
                            finally { _legacyUpgrade = false; }
                            Complete(connection, activeId, null);
                        }
                        else
                        {
                            using (var transaction = connection.BeginTransaction())
                            {
                                Execute(connection, migration.Value, transaction);
                                Complete(connection, activeId, transaction);
                                transaction.Commit();
                            }
                        }
                        AppLogger.LogInfo("Database migration completed: " + activeId + "; verified backup: " + backupPath);
                        activeId = null;
                    }
                }
                catch (Exception ex)
                {
                    if (journalReady && activeId != null)
                    {
                        try { connection.Execute("UPDATE dbo.ServoSchemaMigrations SET Status='Failed',ErrorMessage=@Error WHERE MigrationId=@Id",
                            new { Id = activeId, Error = SensitiveDataRedactor.Redact(ex.Message).Substring(0, Math.Min(2000, SensitiveDataRedactor.Redact(ex.Message).Length)) }); }
                        catch (Exception logError) { AppLogger.LogError("DatabaseUpgradeService.JournalFailure", logError); }
                    }
                    AppLogger.LogError("DatabaseUpgradeService", ex);
                    if (ex is DatabaseUpgradeException) throw;
                    throw Recovery(backupPath, activeId, "The database upgrade or its backup verification failed.", ex);
                }
                finally
                {
                    try { connection.Execute("EXEC sys.sp_releaseapplock @Resource=N'ServoERP.SchemaUpgrade', @LockOwner='Session'"); }
                    catch (Exception ex) { AppLogger.LogError("DatabaseUpgradeService.ReleaseLock", ex); }
                }
            }
        }

        private static void Complete(SqlConnection connection, string id, SqlTransaction transaction)
        {
            connection.Execute("UPDATE dbo.ServoSchemaMigrations SET Status='Completed',CompletedUtc=SYSUTCDATETIME(),ErrorMessage=NULL WHERE MigrationId=@Id", new { Id = id }, transaction);
        }

        private static string CreateVerifiedBackup(SqlConnection connection, string version)
        {
            // Path belongs to SQL Server, not the client PC; supports remote office SQL Express servers.
            string folder = connection.QuerySingleOrDefault<string>("SELECT CONVERT(nvarchar(2000),SERVERPROPERTY('InstanceDefaultBackupPath'))");
            if (string.IsNullOrWhiteSpace(folder))
            {
                string dataFile = connection.QueryFirst<string>("SELECT physical_name FROM sys.database_files WHERE type=0 ORDER BY file_id");
                folder = dataFile.Substring(0, dataFile.LastIndexOf('\\') + 1);
            }
            string path = folder.TrimEnd('\\', '/') + "\\ServoERP_PreUpgrade_" + Guid.NewGuid().ToString("N") + "_" + version + ".bak";
            string database = connection.Database.Replace("]", "]]");
            using (var command = new SqlCommand("BACKUP DATABASE [" + database + "] TO DISK=@Path WITH COPY_ONLY, CHECKSUM, INIT", connection))
            {
                command.Parameters.AddWithValue("@Path", path);
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }
            try { VerifyBackup(connection, path); }
            catch (Exception ex) { throw Recovery(path, null, "The pre-upgrade backup could not be verified. No schema migration was started.", ex); }
            AppLogger.LogInfo("Verified pre-upgrade SQL backup: " + path);
            return path;
        }

        internal static void VerifyBackup(SqlConnection connection, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new DatabaseUpgradeException("The original pre-upgrade backup path is missing. Contact ServoERP support before retrying.");
            // Verify the backup identity and checksum metadata as well as physical readability.
            using (var header = new SqlCommand("RESTORE HEADERONLY FROM DISK=@Path", connection))
            {
                header.Parameters.AddWithValue("@Path", path);
                header.CommandTimeout = 600;
                using (var reader = header.ExecuteReader())
                {
                    if (!reader.Read() || !string.Equals(Convert.ToString(reader["DatabaseName"]), connection.Database, StringComparison.OrdinalIgnoreCase)
                        || Convert.ToInt32(reader["BackupType"]) != 1 || !Convert.ToBoolean(reader["HasBackupChecksums"]) || !Convert.ToBoolean(reader["IsCopyOnly"]))
                        throw new DatabaseUpgradeException("The recovery file is not a checksum-protected full COPY_ONLY backup of this office database.");
                }
            }
            using (var command = new SqlCommand("RESTORE VERIFYONLY FROM DISK=@Path WITH CHECKSUM", connection))
            {
                command.Parameters.AddWithValue("@Path", path);
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }
        }

        internal static SortedDictionary<string, string> ReadScripts()
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            Assembly assembly = Assembly.GetExecutingAssembly();
            const string prefix = "ServoERP.NumberedMigrations.";
            foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal)))
            {
                string id = name.Substring(prefix.Length, name.Length - prefix.Length - 4);
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^\d{4}_[A-Za-z0-9_]+$") || int.Parse(id.Substring(0, 4), CultureInfo.InvariantCulture) < 3)
                    throw new DatabaseUpgradeException("Invalid numbered migration resource: " + name);
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(name))) result.Add(id, reader.ReadToEnd());
            }
            return result;
        }

        private static void Execute(SqlConnection connection, string sql, SqlTransaction transaction = null)
        {
            // Migration batches are trusted embedded release resources, never user input. GO is not supported.
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.CommandTimeout = 600;
                command.ExecuteNonQuery();
            }
        }

        internal static string Hash(string script)
        {
            // Git checkouts and Windows editors may change line endings without changing SQL.
            string canonical = script.Replace("\r\n", "\n").Replace("\r", "\n");
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "").ToLowerInvariant();
        }

        private static DatabaseUpgradeException Recovery(string backup, string id, string reason, Exception inner = null)
        {
            return new DatabaseUpgradeException("ServoERP has blocked startup to protect your office data. " + reason +
                "\r\n\r\nMigration: " + (id ?? "Before schema changes") + "\r\nVerified backup on SQL Server: " + (backup ?? "Not created or not verified; no migration was started") +
                "\r\n\r\nClose ServoERP on every office PC. Ask your administrator to correct the reported issue, then run this same release with /retrydatabaseupgrade to retry the upgrade. The original backup will be verified again." +
                "\r\nIf retry is unsuccessful, contact support to restore the verified backup and install the previous supported release. Restore is never automatic because it can overwrite newer business data." +
                "\r\n\r\nDetails: " + SensitiveDataRedactor.Redact(inner == null ? reason : inner.Message), inner);
        }
    }
}
