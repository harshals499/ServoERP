using System;
using System.IO;

namespace HVAC_Pro_Desktop.Services
{
    public sealed class OneDriveCopyResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Uses the locally installed OneDrive sync client as a customer-owned storage target.
    /// ServoERP never opens SQL database files from OneDrive; only completed backup files and
    /// ordinary documents are placed below this root.
    /// </summary>
    public static class OneDriveStorageService
    {
        private const string Section = "OneDriveStorage";

        public static bool IsEnabled => string.Equals(
            ConfigService.Get(Section, "Enabled", "false"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        public static string ConfiguredRoot => Expand(ConfigService.Get(Section, "RootPath", string.Empty));

        public static string RootPath
        {
            get
            {
                string configured = ConfiguredRoot;
                return string.IsNullOrWhiteSpace(configured) ? DetectInstalledRoot() : configured;
            }
        }

        public static string ServoErpRoot => string.IsNullOrWhiteSpace(RootPath)
            ? string.Empty
            : Path.Combine(RootPath, "ServoERP");

        public static string BackupsPath => ResolveFolder("Backups");

        public static string DocumentsPath => ResolveFolder("Documents");

        public static string DetectInstalledRoot()
        {
            string[] candidates =
            {
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDrive")
            };

            foreach (string candidate in candidates)
            {
                string expanded = Expand(candidate);
                if (!string.IsNullOrWhiteSpace(expanded) && Directory.Exists(expanded))
                    return Path.GetFullPath(expanded);
            }

            return string.Empty;
        }

        public static bool TryPrepare(out string message)
        {
            if (!IsEnabled)
            {
                message = "OneDrive storage is disabled.";
                return false;
            }

            string root = RootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                message = "OneDrive was not detected. Sign in to the OneDrive desktop app or select its local folder.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.Combine(root, "ServoERP", "Backups"));
                Directory.CreateDirectory(Path.Combine(root, "ServoERP", "Documents"));
                Directory.CreateDirectory(Path.Combine(root, "ServoERP", "Exports"));
                message = "Ready: " + Path.Combine(root, "ServoERP");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("OneDriveStorageService.TryPrepare", ex);
                message = "OneDrive folder is unavailable: " + ex.Message;
                return false;
            }
        }

        public static OneDriveCopyResult MirrorBackup(string sourcePath, int retentionDays)
        {
            if (!IsEnabled)
                return new OneDriveCopyResult { Success = false, Message = "OneDrive storage is disabled." };
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return new OneDriveCopyResult { Success = false, Message = "The completed backup file was not found." };

            try
            {
                string folder = BackupsPath;
                if (string.IsNullOrWhiteSpace(folder))
                    return new OneDriveCopyResult { Success = false, Message = "OneDrive was not detected." };

                string target = Path.Combine(folder, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, target, true);
                DeleteExpiredBackups(folder, retentionDays);
                return new OneDriveCopyResult
                {
                    Success = true,
                    FilePath = target,
                    Message = "Backup copied to OneDrive."
                };
            }
            catch (Exception ex)
            {
                AppLogger.LogError("OneDriveStorageService.MirrorBackup", ex);
                return new OneDriveCopyResult { Success = false, Message = "OneDrive copy failed: " + ex.Message };
            }
        }

        public static bool IsPathInsideOneDrive(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string root = RootPath;
            if (string.IsNullOrWhiteSpace(root))
                root = DetectInstalledRoot();
            if (string.IsNullOrWhiteSpace(root))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(Expand(path)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveFolder(string child)
        {
            string root = ServoErpRoot;
            if (string.IsNullOrWhiteSpace(root))
                return string.Empty;

            string folder = Path.Combine(root, child ?? string.Empty);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string Expand(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : Environment.ExpandEnvironmentVariables(value.Trim());
        }

        private static void DeleteExpiredBackups(string folder, int retentionDays)
        {
            DateTime cutoff = DateTime.Now.Date.AddDays(-Math.Max(1, retentionDays));
            foreach (string file in Directory.GetFiles(folder, "ServoERP_Backup_*.bak", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("OneDriveStorageService.DeleteExpiredBackups", ex);
                }
            }
        }
    }
}
