using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HVAC_Pro_Desktop.Services
{
    public static class SharedStorageService
    {
        public const string DefaultSharedRoot = @"\\SERVERPC\ServoERPShared";
        private const string Section = "SharedStorage";
        public static readonly string[] RequiredFolderNames = { "Backups", "CompanyTemplates", "Documents", "Exports", "Imports", "Logs", "Updates" };
        private static readonly object ConnectionSync = new object();
        private static Timer _reconnectTimer;
        private static string _connectionStatus = "Shared storage has not been checked.";

        /// <summary>Provides the latest non-sensitive shared-storage connection result for Settings diagnostics.</summary>
        public static string ConnectionStatus => _connectionStatus;

        public static string RootPath => ResolveServerPlaceholder(NormalizeRoot(ConfigService.Get(Section, "RootPath", DefaultSharedRoot)));

        public static bool IsEnabled => string.Equals(ConfigService.Get(Section, "Enabled", "true"), "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>Starts background SMB connection attempts so users never need to map a drive before shared folders become available.</summary>
        public static void StartAutomaticConnection()
        {
            lock (ConnectionSync)
            {
                if (_reconnectTimer != null)
                    return;

                _reconnectTimer = new Timer(_ => TryConnectAndPrepareFolders(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            }
        }

        /// <summary>Attempts to connect through the current Windows session and prepares the standard customer-owned folders.</summary>
        public static bool TryConnectAndPrepareFolders()
        {
            if (!IsEnabled)
            {
                _connectionStatus = "Shared storage is disabled; this PC is using local folders.";
                return false;
            }

            string root = RootPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                _connectionStatus = "Shared storage path is not configured.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(root);
                foreach (string folder in RequiredFolderNames)
                    Directory.CreateDirectory(Path.Combine(root, folder));

                _connectionStatus = "Connected to private shared storage: " + root;
                return true;
            }
            catch (Exception ex)
            {
                _connectionStatus = "Private shared storage is unavailable; local folders remain active.";
                AppRuntime.LogException("SharedStorageService.AutomaticConnection", ex);
                return false;
            }
        }

        public static string BackupsPath => ResolveFolder("Backups", LocalFolder("Backups"));

        public static string CompanyTemplatesPath => ResolveFolder("CompanyTemplates", LocalFolder("CompanyTemplates"));

        public static string DocumentsPath => ResolveFolder("Documents", LocalFolder("Documents"));

        public static string ExportsPath => ResolveFolder("Exports", LocalFolder("Exports"));

        public static string ImportsPath => ResolveFolder("Imports", LocalFolder("Imports"));

        public static string LogsPath => ResolveFolder("Logs", LocalFolder("Logs"));

        public static string UpdatesPath => ResolveFolder("Updates", LocalFolder("Updates"));

        public static string ResolveFolder(string childFolder, string localFallback)
        {
            string root = RootPath;
            if (IsEnabled && !string.IsNullOrWhiteSpace(root))
            {
                try
                {
                    string shared = Path.Combine(root, childFolder ?? string.Empty);
                    Directory.CreateDirectory(shared);
                    _connectionStatus = "Connected to private shared storage: " + root;
                    return shared;
                }
                catch (Exception ex)
                {
                    _connectionStatus = "Private shared storage is unavailable; local folders remain active.";
                    AppRuntime.LogException("SharedStorageService.ResolveFolder." + childFolder, ex);
                }
            }

            string fallback = string.IsNullOrWhiteSpace(localFallback) ? LocalFolder(childFolder) : localFallback;
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        public static string ResolveDocumentFolder(string childFolder)
        {
            return ResolveFolder(Path.Combine("Documents", childFolder ?? string.Empty), LocalFolder(Path.Combine("Documents", childFolder ?? string.Empty)));
        }

        public static string ResolveExportFolder(string childFolder)
        {
            return ResolveFolder(Path.Combine("Exports", childFolder ?? string.Empty), LocalFolder(Path.Combine("Exports", childFolder ?? string.Empty)));
        }

        public static string ResolveConfiguredPath(string configuredPath, string fallback)
        {
            string path = NormalizeRoot(configuredPath);
            if (string.IsNullOrWhiteSpace(path))
                path = fallback ?? string.Empty;

            return ResolveServerPlaceholder(path);
        }

        private static string NormalizeRoot(string value)
        {
            return (value ?? string.Empty).Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string ResolveServerPlaceholder(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !root.StartsWith(@"\\SERVERPC\", StringComparison.OrdinalIgnoreCase))
                return root;

            string server = ConfigService.Get("Database", "Server", string.Empty);
            if (string.IsNullOrWhiteSpace(server))
                return root;

            int instanceSeparator = server.IndexOf('\\');
            if (instanceSeparator > 0)
                server = server.Substring(0, instanceSeparator);

            server = server.Trim();
            if (string.IsNullOrWhiteSpace(server) || string.Equals(server, ".", StringComparison.OrdinalIgnoreCase) || string.Equals(server, "(local)", StringComparison.OrdinalIgnoreCase))
                server = Environment.MachineName;

            return @"\\" + server + root.Substring(@"\\SERVERPC".Length);
        }

        private static string LocalFolder(string childFolder)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP");
            return string.IsNullOrWhiteSpace(childFolder) ? root : Path.Combine(root, childFolder);
        }
    }
}
