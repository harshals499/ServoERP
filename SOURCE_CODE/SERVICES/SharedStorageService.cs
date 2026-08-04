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

        /// <summary>
        /// Finds the office file-server candidate from the authoritative SQL Server setting.
        /// ServoERP never scans arbitrary network machines or stores credentials: it proposes
        /// the standard share on the same private server that holds the business database.
        /// </summary>
        public static PrivateServerDetectionResult DetectPrivateServer()
        {
            string configuredRoot = NormalizeRoot(ConfigService.Get(Section, "RootPath", string.Empty));
            string databaseHost = GetDatabaseServerHost();
            string suggestedRoot = configuredRoot;

            if (string.IsNullOrWhiteSpace(suggestedRoot) || IsServerPlaceholder(suggestedRoot))
            {
                if (string.IsNullOrWhiteSpace(databaseHost))
                {
                    return PrivateServerDetectionResult.NotFound(
                        "No office SQL Server is configured yet. Enter the server once in Connection Setup, then ServoERP can detect its shared folder.");
                }

                suggestedRoot = @"\\" + databaseHost + @"\ServoERPShared";
            }

            try
            {
                bool reachable = Directory.Exists(suggestedRoot);
                string message = reachable
                    ? "Private server found: " + suggestedRoot
                    : "Office SQL Server found at " + databaseHost + ". Suggested shared folder: " + suggestedRoot + ". Create or share this folder on the server if it is not ready yet.";
                return new PrivateServerDetectionResult(databaseHost, suggestedRoot, reachable, message);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("SharedStorageService.DetectPrivateServer", ex);
                return new PrivateServerDetectionResult(databaseHost, suggestedRoot, false,
                    "Office SQL Server found at " + databaseHost + ", but its shared folder could not be checked from this PC.");
            }
        }

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

        private static bool IsServerPlaceholder(string root)
        {
            return string.IsNullOrWhiteSpace(root) || root.StartsWith(@"\\SERVERPC\", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDatabaseServerHost()
        {
            string server = ConfigService.Get("Database", "Server", string.Empty);
            if (string.IsNullOrWhiteSpace(server))
                return string.Empty;

            server = server.Trim();
            if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
                server = server.Substring(4);

            int instanceSeparator = server.IndexOf('\\');
            if (instanceSeparator > 0)
                server = server.Substring(0, instanceSeparator);

            int portSeparator = server.IndexOf(',');
            if (portSeparator > 0)
                server = server.Substring(0, portSeparator);

            if (string.Equals(server, ".", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(server, "(local)", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return Environment.MachineName;
            }

            return server.Trim();
        }

        private static string LocalFolder(string childFolder)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP");
            return string.IsNullOrWhiteSpace(childFolder) ? root : Path.Combine(root, childFolder);
        }
    }

    public sealed class PrivateServerDetectionResult
    {
        public string ServerName { get; private set; }
        public string SuggestedRootPath { get; private set; }
        public bool IsReachable { get; private set; }
        public string Message { get; private set; }

        public PrivateServerDetectionResult(string serverName, string suggestedRootPath, bool isReachable, string message)
        {
            ServerName = serverName ?? string.Empty;
            SuggestedRootPath = suggestedRootPath ?? string.Empty;
            IsReachable = isReachable;
            Message = message ?? string.Empty;
        }

        public static PrivateServerDetectionResult NotFound(string message)
        {
            return new PrivateServerDetectionResult(string.Empty, string.Empty, false, message);
        }
    }
}
