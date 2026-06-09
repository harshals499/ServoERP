using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public enum BackupTrigger
    {
        Scheduled,
        OnClose,
        Manual
    }

    public sealed class BackupLogEntry
    {
        public DateTime BackupTime { get; set; }
        public string Trigger { get; set; }
        public string Destination { get; set; }
        public string FilePath { get; set; }
        public long FileSizeKB { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public sealed class BackupService
    {
        public const string DefaultLocalBackupPath = @"C:\ServoERP\Backups\";
        private const long MinimumExternalDriveBytes = 500L * 1024L * 1024L;
        private const int CommandTimeoutSeconds = 900;

        public static string BackupRoot => GetSetting("BackupLocalPath", DefaultLocalBackupPath);

        /// <summary>Creates required backup tables and default user settings.</summary>
        public void EnsureBackupInfrastructure()
        {
            try
            {
                DbSettings.EnsureUserSettingsTable();
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        IF OBJECT_ID('dbo.BackupLog', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.BackupLog (
                                Id            INT IDENTITY(1,1) PRIMARY KEY,
                                BackupTime    DATETIME NOT NULL,
                                [Trigger]     NVARCHAR(20),
                                Destination   NVARCHAR(20),
                                FilePath      NVARCHAR(500),
                                FileSizeKB    BIGINT,
                                Success       BIT NOT NULL,
                                ErrorMessage  NVARCHAR(1000)
                            );
                        END;", conn))
                    {
                        cmd.CommandTimeout = 60;
                        cmd.ExecuteNonQuery();
                    }
                }

                EnsureSetting("BackupNetworkPath", string.Empty);
                EnsureSetting("BackupLocalPath", DefaultLocalBackupPath);
                EnsureSetting("BackupScheduledTime", "18:00");
                EnsureSetting("BackupRetentionDays", "30");
                EnsureSetting("BackupLastScheduledRun", string.Empty);
                EnsureSetting("BackupEnabled", "true");
                EnsureSetting("BackupRunOnClose", "true");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.EnsureBackupInfrastructure", ex);
            }
        }

        /// <summary>Runs a database backup using network, local, then external-drive fallback.</summary>
        public BackupResult RunBackup(BackupTrigger trigger)
        {
            EnsureBackupInfrastructure();

            if (!IsEnabled() && trigger != BackupTrigger.Manual)
                return Result(false, trigger, string.Empty, string.Empty, "Automatic backups are disabled.", 0);

            BackupResult lastFailure = null;
            string networkPath = GetSetting("BackupNetworkPath", string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(networkPath))
            {
                BackupResult network = Directory.Exists(networkPath)
                    ? TryBackupToPath(networkPath, "Network", trigger)
                    : LogSkipped(trigger, "Network", networkPath, "Network backup path is unreachable.");
                if (network.Success)
                    return CompleteSuccessfulBackup(network, trigger);
                lastFailure = network;
            }

            string localPath = GetSetting("BackupLocalPath", DefaultLocalBackupPath);
            BackupResult local = TryBackupToPath(localPath, "Local", trigger);
            if (local.Success)
                return CompleteSuccessfulBackup(local, trigger);
            lastFailure = local;

            string externalPath = DetectExternalDrive();
            if (!string.IsNullOrWhiteSpace(externalPath))
            {
                BackupResult external = TryBackupToPath(externalPath, "ExternalDrive", trigger);
                if (external.Success)
                    return CompleteSuccessfulBackup(external, trigger);
                lastFailure = external;
            }
            else
            {
                lastFailure = LogSkipped(trigger, "ExternalDrive", string.Empty, "No removable drive with at least 500 MB free space was found.");
            }

            return lastFailure ?? Result(false, trigger, string.Empty, string.Empty, "Backup failed before any destination could be attempted.", 0);
        }

        /// <summary>Creates a manual database backup while preserving the legacy service API.</summary>
        public BackupResult CreateDatabaseBackup(string reason = "Manual")
        {
            BackupResult result = RunBackup(BackupTrigger.Manual);
            if (result.Success)
                result.Message = "Backup completed.";
            else if (string.IsNullOrWhiteSpace(result.Message))
                result.Message = result.ErrorMessage;
            return result;
        }

        /// <summary>Returns a successful recent backup when one already exists, otherwise runs a scheduled backup.</summary>
        public BackupResult CreateStartupBackupIfDue()
        {
            EnsureBackupInfrastructure();
            if (!IsEnabled())
                return Result(false, BackupTrigger.Scheduled, string.Empty, string.Empty, "Automatic backup disabled.", 0);

            if (!ShouldRunScheduledBackup())
                return Result(true, BackupTrigger.Scheduled, string.Empty, string.Empty, "Daily backup already completed today.", 0);

            return RunBackup(BackupTrigger.Scheduled);
        }

        /// <summary>Determines whether the configured daily scheduled backup is due at the current clock time.</summary>
        public bool IsScheduledBackupDue()
        {
            if (!IsEnabled() || !ShouldRunScheduledBackup())
                return false;

            TimeSpan scheduled = GetScheduledTime();
            DateTime now = DateTime.Now;
            return now.Hour == scheduled.Hours && now.Minute == scheduled.Minutes;
        }

        /// <summary>Determines whether the app-close backup should run before ServoERP exits.</summary>
        public bool ShouldRunOnCloseBackup()
        {
            if (!IsEnabled())
                return false;

            if (!GetBoolSetting("BackupRunOnClose", true))
                return false;

            return ShouldRunScheduledBackup();
        }

        /// <summary>Records that an app-close backup did not finish before the close timeout.</summary>
        public void LogIncompleteOnCloseBackup()
        {
            LogBackupResult(Result(false, BackupTrigger.OnClose, string.Empty, string.Empty, "Backup was still running when ServoERP closed after the 10 second timeout.", 0));
        }

        /// <summary>Reads the latest backup log rows for display.</summary>
        public List<BackupLogEntry> GetBackupLog(int top = 20)
        {
            EnsureBackupInfrastructure();
            var rows = new List<BackupLogEntry>();
            try
            {
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP (@top) BackupTime, [Trigger], Destination, FilePath, FileSizeKB, Success, ErrorMessage
                        FROM dbo.BackupLog
                        ORDER BY Id DESC;", conn))
                    {
                        cmd.Parameters.AddWithValue("@top", Math.Max(1, top));
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rows.Add(new BackupLogEntry
                                {
                                    BackupTime = reader["BackupTime"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["BackupTime"]),
                                    Trigger = ToText(reader["Trigger"]),
                                    Destination = ToText(reader["Destination"]),
                                    FilePath = ToText(reader["FilePath"]),
                                    FileSizeKB = reader["FileSizeKB"] == DBNull.Value ? 0 : Convert.ToInt64(reader["FileSizeKB"]),
                                    Success = reader["Success"] != DBNull.Value && Convert.ToBoolean(reader["Success"]),
                                    ErrorMessage = ToText(reader["ErrorMessage"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.GetBackupLog", ex);
            }

            return rows;
        }

        /// <summary>Deletes all backup log rows at the user's request.</summary>
        public void ClearBackupLog()
        {
            EnsureBackupInfrastructure();
            try
            {
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.BackupLog;", conn))
                        cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.ClearBackupLog", ex);
            }
        }

        /// <summary>Returns local backup files ordered newest first for existing restore screens.</summary>
        public List<FileInfo> GetBackups()
        {
            string root = BackupRoot;
            Directory.CreateDirectory(root);
            return Directory.GetFiles(root, "*.bak", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTime)
                .ToList();
        }

        /// <summary>Restores the selected SQL Server backup file after optional safety backup creation.</summary>
        public BackupResult RestoreDatabaseBackup(string backupPath, bool createSafetyBackup = true)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
                return Result(false, BackupTrigger.Manual, "Restore", backupPath, "Backup file is required.", 0);

            if (!File.Exists(backupPath))
                return Result(false, BackupTrigger.Manual, "Restore", backupPath, "Backup file was not found.", 0);

            if (!backupPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return Result(false, BackupTrigger.Manual, "Restore", backupPath, "Only SQL Server .bak files can be restored.", 0);

            string databaseName = GetDatabaseName();
            BackupResult safetyBackup = null;

            try
            {
                BackupResult verifyResult = VerifyBackupFile(backupPath);
                if (!verifyResult.Success)
                    return verifyResult;

                if (createSafetyBackup)
                {
                    safetyBackup = RunBackup(BackupTrigger.Manual);
                    if (!safetyBackup.Success)
                        return Result(false, BackupTrigger.Manual, "Restore", backupPath, "Restore cancelled because the safety backup failed: " + safetyBackup.Message, 0);
                }

                using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection(GetMasterConnectionString()))
                {
                    DatabaseConnectionFactory.Open(conn, "BackupService.RestoreDatabaseBackup");
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
                return Result(true, BackupTrigger.Manual, "Restore", backupPath, "Restore completed." + safetyMessage, FileSizeKb(backupPath));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.RestoreDatabaseBackup", ex);
                return Result(false, BackupTrigger.Manual, "Restore", backupPath, ex.Message, 0);
            }
        }

        /// <summary>Verifies a SQL Server backup file with RESTORE VERIFYONLY.</summary>
        public BackupResult VerifyBackupFile(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return Result(false, BackupTrigger.Manual, "Verify", backupPath, "Backup file was not found.", 0);

            try
            {
                new DbExecutor(GetMasterConnectionString(), 900).Execute(
                    "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM",
                    DbExecutor.Param("@path", backupPath));

                return Result(true, BackupTrigger.Manual, "Verify", backupPath, "Backup verified.", FileSizeKb(backupPath));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.VerifyBackupFile", ex);
                return Result(false, BackupTrigger.Manual, "Verify", backupPath, "Backup verification failed: " + ex.Message, 0);
            }
        }

        private string GenerateBackupFileName()
        {
            return "ServoERP_Backup_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture) + ".bak";
        }

        private BackupResult TryBackupToPath(string destinationPath, string destinationName, BackupTrigger trigger)
        {
            string backupPath = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath))
                    return LogSkipped(trigger, destinationName, destinationPath, "Destination path is blank.");

                Directory.CreateDirectory(destinationPath);
                CleanOldBackups(destinationPath, GetRetentionDays());

                backupPath = Path.Combine(destinationPath, GenerateBackupFileName());
                string databaseName = GetDatabaseName();

                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    try
                    {
                        ExecuteBackupCommand(conn, databaseName, backupPath, true);
                    }
                    catch (SqlException ex) when (IsCompressionUnsupported(ex))
                    {
                        AppLogger.LogInfo("SQL Server backup compression is unavailable on this edition. Retrying backup without compression.");
                        ExecuteBackupCommand(conn, databaseName, backupPath, false);
                    }
                }

                BackupResult result = Result(true, trigger, destinationName, backupPath, "Backup completed.", FileSizeKb(backupPath));
                LogBackupResult(result);
                return result;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.TryBackupToPath." + destinationName, ex);
                BackupResult result = Result(false, trigger, destinationName, backupPath, ex.Message, 0);
                LogBackupResult(result);
                return result;
            }
        }

        private string DetectExternalDrive()
        {
            try
            {
                DriveInfo drive = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable && d.IsReady && d.AvailableFreeSpace >= MinimumExternalDriveBytes)
                    .OrderBy(d => d.Name)
                    .FirstOrDefault();

                return drive == null ? string.Empty : Path.Combine(drive.RootDirectory.FullName, "ServoERP_Backups");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.DetectExternalDrive", ex);
                return string.Empty;
            }
        }

        private static void ExecuteBackupCommand(SqlConnection conn, string databaseName, string backupPath, bool useCompression)
        {
            string sql = "BACKUP DATABASE [" + EscapeSqlIdentifier(databaseName) + "] TO DISK = @path WITH FORMAT, INIT, " +
                         (useCompression ? "COMPRESSION, " : string.Empty) +
                         "STATS = 10;";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandTimeout = CommandTimeoutSeconds;
                cmd.Parameters.Add("@path", SqlDbType.NVarChar, 500).Value = backupPath;
                cmd.ExecuteNonQuery();
            }
        }

        private static bool IsCompressionUnsupported(SqlException ex)
        {
            return ex != null && ex.Message.IndexOf("COMPRESSION is not supported", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogBackupResult(BackupResult result)
        {
            EnsureBackupInfrastructureForLogOnly();
            try
            {
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        INSERT INTO dbo.BackupLog
                            (BackupTime, [Trigger], Destination, FilePath, FileSizeKB, Success, ErrorMessage)
                        VALUES
                            (@time, @trigger, @destination, @path, @size, @success, @error);", conn))
                    {
                        cmd.Parameters.AddWithValue("@time", result.Timestamp == default(DateTime) ? DateTime.Now : result.Timestamp);
                        cmd.Parameters.AddWithValue("@trigger", result.Trigger ?? string.Empty);
                        cmd.Parameters.AddWithValue("@destination", result.DestinationUsed ?? string.Empty);
                        cmd.Parameters.AddWithValue("@path", result.FilePath ?? result.BackupPath ?? string.Empty);
                        cmd.Parameters.AddWithValue("@size", result.FileSizeKB);
                        cmd.Parameters.AddWithValue("@success", result.Success);
                        cmd.Parameters.AddWithValue("@error", result.Success ? string.Empty : (result.ErrorMessage ?? result.Message ?? string.Empty));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.LogBackupResult", ex);
            }
        }

        private void CleanOldBackups(string folder, int keepDays)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            DateTime cutoff = DateTime.Now.Date.AddDays(-Math.Max(1, keepDays));
            foreach (string file in Directory.GetFiles(folder, "ServoERP_Backup_*.bak", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("BackupService.CleanOldBackups", ex);
                }
            }
        }

        private BackupResult CompleteSuccessfulBackup(BackupResult result, BackupTrigger trigger)
        {
            if (trigger == BackupTrigger.Scheduled)
                DbSettings.Set("BackupLastScheduledRun", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            return result;
        }

        private BackupResult LogSkipped(BackupTrigger trigger, string destination, string path, string message)
        {
            BackupResult result = Result(false, trigger, destination, path, message, 0);
            LogBackupResult(result);
            return result;
        }

        private static BackupResult Result(bool success, BackupTrigger trigger, string destination, string path, string message, long fileSizeKb)
        {
            DateTime timestamp = DateTime.Now;
            return new BackupResult
            {
                Success = success,
                BackupPath = path,
                DestinationUsed = destination,
                FilePath = path,
                ErrorMessage = success ? string.Empty : message,
                Trigger = trigger.ToString(),
                FileSizeKB = fileSizeKb,
                Timestamp = timestamp,
                CreatedAt = timestamp,
                Message = message
            };
        }

        private static long FileSizeKb(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return 0;
                return Math.Max(1, new FileInfo(path).Length / 1024L);
            }
            catch
            {
                return 0;
            }
        }

        private static bool ShouldRunScheduledBackup()
        {
            string raw = GetSetting("BackupLastScheduledRun", string.Empty);
            DateTime date;
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            return date.Date < DateTime.Today;
        }

        private static TimeSpan GetScheduledTime()
        {
            string raw = GetSetting("BackupScheduledTime", "18:00");
            TimeSpan parsed;
            return TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out parsed) ? parsed : new TimeSpan(18, 0, 0);
        }

        private static int GetRetentionDays()
        {
            int parsed;
            return int.TryParse(GetSetting("BackupRetentionDays", "30"), out parsed) ? Math.Max(1, Math.Min(365, parsed)) : 30;
        }

        private static bool IsEnabled()
        {
            return GetBoolSetting("BackupEnabled", true);
        }

        private static bool GetBoolSetting(string key, bool fallback)
        {
            string raw = GetSetting(key, fallback ? "true" : "false");
            bool parsed;
            return bool.TryParse(raw, out parsed) ? parsed : fallback;
        }

        private static string GetSetting(string key, string fallback)
        {
            return DbSettings.Get(key, fallback);
        }

        private static void EnsureSetting(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(DbSettings.Get(key, null)))
                DbSettings.Set(key, value);
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
            return DatabaseConnectionFactory.NormalizeConnectionString(builder.ConnectionString);
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

        private static string ToText(object value)
        {
            return value == null || value == DBNull.Value ? string.Empty : value.ToString();
        }

        private void EnsureBackupInfrastructureForLogOnly()
        {
            try
            {
                using (SqlConnection conn = new DatabaseManager().GetConnection())
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        IF OBJECT_ID('dbo.BackupLog', 'U') IS NULL
                        BEGIN
                            CREATE TABLE dbo.BackupLog (
                                Id            INT IDENTITY(1,1) PRIMARY KEY,
                                BackupTime    DATETIME NOT NULL,
                                [Trigger]     NVARCHAR(20),
                                Destination   NVARCHAR(20),
                                FilePath      NVARCHAR(500),
                                FileSizeKB    BIGINT,
                                Success       BIT NOT NULL,
                                ErrorMessage  NVARCHAR(1000)
                            );
                        END;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.EnsureBackupInfrastructureForLogOnly", ex);
            }
        }
    }
}
