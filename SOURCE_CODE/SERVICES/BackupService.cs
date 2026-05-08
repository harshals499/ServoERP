using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class BackupService
    {
        private const int DefaultRetentionCount = 10;
        public static string BackupRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "Backups");

        public BackupResult CreateDatabaseBackup(string reason = "Manual")
        {
            Directory.CreateDirectory(BackupRoot);
            string databaseName = GetDatabaseName();
            string backupPath = Path.Combine(BackupRoot, databaseName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");

            try
            {
                new DbExecutor(commandTimeoutSeconds: 600).Execute(
                    "BACKUP DATABASE [" + EscapeSqlIdentifier(databaseName) + "] TO DISK = @path WITH INIT, CHECKSUM",
                    DbExecutor.Param("@path", backupPath));

                PruneOldBackups(DefaultRetentionCount);
                AppLogger.LogInfo("Database backup created: " + backupPath + " | " + reason);
                return new BackupResult { Success = true, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Backup completed." };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.CreateDatabaseBackup", ex);
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = ex.Message };
            }
        }

        public BackupResult RestoreDatabaseBackup(string backupPath, bool createSafetyBackup = true)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Backup file is required." };

            if (!File.Exists(backupPath))
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Backup file was not found." };

            if (!backupPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Only SQL Server .bak files can be restored." };

            string databaseName = GetDatabaseName();
            BackupResult safetyBackup = null;

            try
            {
                BackupResult verifyResult = VerifyBackupFile(backupPath);
                if (!verifyResult.Success)
                    return verifyResult;

                if (createSafetyBackup)
                {
                    safetyBackup = CreateDatabaseBackup("Pre-restore safety backup");
                    if (!safetyBackup.Success)
                        return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Restore cancelled because the safety backup failed: " + safetyBackup.Message };
                }

                using (SqlConnection conn = new SqlConnection(GetMasterConnectionString()))
                {
                    conn.Open();
                    var masterDb = new DbExecutor(GetMasterConnectionString(), 900);
                    masterDb.Execute(conn, null, "ALTER DATABASE [" + EscapeSqlIdentifier(databaseName) + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
                    try
                    {
                        masterDb.Execute(
                            conn,
                            null,
                            "RESTORE DATABASE [" + EscapeSqlIdentifier(databaseName) + "] FROM DISK = @path WITH REPLACE, RECOVERY, CHECKSUM",
                            DbExecutor.Param("@path", backupPath));
                    }
                    finally
                    {
                        TrySetMultiUser(conn, databaseName);
                    }
                }

                string safetyMessage = safetyBackup != null && safetyBackup.Success ? " Safety backup: " + safetyBackup.BackupPath : string.Empty;
                AppLogger.LogInfo("Database restored from backup: " + backupPath + safetyMessage);
                SessionManager.LogAction("RESTORE", "Settings", null, "Database restored from backup. " + Path.GetFileName(backupPath));
                return new BackupResult { Success = true, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Restore completed." + safetyMessage };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.RestoreDatabaseBackup", ex);
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = ex.Message };
            }
        }

        public BackupResult VerifyBackupFile(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Backup file was not found." };

            try
            {
                new DbExecutor(GetMasterConnectionString(), 900).Execute(
                    "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM",
                    DbExecutor.Param("@path", backupPath));

                return new BackupResult { Success = true, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Backup verified." };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.VerifyBackupFile", ex);
                return new BackupResult { Success = false, BackupPath = backupPath, CreatedAt = DateTime.Now, Message = "Backup verification failed: " + ex.Message };
            }
        }

        public BackupResult CreateStartupBackupIfDue()
        {
            int hours = GetIntervalHours();
            if (hours <= 0)
                return new BackupResult { Success = false, Message = "Automatic backup disabled.", CreatedAt = DateTime.Now };

            FileInfo latest = GetBackups().FirstOrDefault();
            if (latest != null && latest.LastWriteTime > DateTime.Now.AddHours(-hours))
                return new BackupResult { Success = true, BackupPath = latest.FullName, Message = "Recent backup already exists.", CreatedAt = latest.LastWriteTime };

            return CreateDatabaseBackup("Scheduled startup backup");
        }

        public List<FileInfo> GetBackups()
        {
            Directory.CreateDirectory(BackupRoot);
            return Directory.GetFiles(BackupRoot, "*.bak", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTime)
                .ToList();
        }

        private void PruneOldBackups(int keep)
        {
            foreach (FileInfo file in GetBackups().Skip(Math.Max(1, keep)))
            {
                try { file.Delete(); }
                catch (Exception ex) { AppLogger.LogError("BackupService.PruneOldBackups", ex); }
            }
        }

        private static int GetIntervalHours()
        {
            int parsed;
            return int.TryParse(new SettingsService().Get("AutoBackupIntervalHours", "24"), out parsed) ? parsed : 24;
        }

        private static string GetDatabaseName()
        {
            var builder = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
            return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "HVAC_PRO" : builder.InitialCatalog.Trim();
        }

        private static string GetMasterConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
            builder.InitialCatalog = "master";
            return builder.ConnectionString;
        }

        private static void TrySetMultiUser(SqlConnection conn, string databaseName)
        {
            try
            {
                new DbExecutor(GetMasterConnectionString(), 900).Execute(conn, null, "ALTER DATABASE [" + EscapeSqlIdentifier(databaseName) + "] SET MULTI_USER");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.TrySetMultiUser", ex);
            }
        }

        private static string EscapeSqlIdentifier(string value)
        {
            return (value ?? string.Empty).Replace("]", "]]");
        }
    }
}
