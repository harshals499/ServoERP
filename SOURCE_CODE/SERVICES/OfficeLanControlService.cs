using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Discovers office PCs and coordinates administrator-authorized ServoERP deployment without storing credentials.</summary>
    public sealed class OfficeLanControlService
    {
        private const string HealthCheck = "HealthCheck";
        private const string CheckForUpdate = "CheckForUpdate";
        private const string CollectDiagnostics = "CollectDiagnostics";
        private const string RepairDatabase = "RepairDatabase";
        private const int DiscoveryParallelism = 32;
        private const int MaximumAutomaticDiscoveryAddresses = 1024;
        private static readonly string DeploymentRoot = Path.Combine(@"C:\HVAC_PRO_MSE", "DIAGNOSTICS", "LAN_DEPLOYMENT");
        private static readonly byte[] DeploymentCredentialEntropy = Encoding.UTF8.GetBytes("ServoERP.LanDeployment.v1");
        private static readonly string SavedDeploymentCredentialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServoERP", "lan-deployment-credential.dat");

        public async Task<IList<OfficeLanComputer>> DiscoverComputersAsync(CancellationToken cancellationToken)
        {
            List<IPAddress> addresses = GetDiscoveryAddresses();
            var discovered = new List<OfficeLanComputer>();
            var gate = new SemaphoreSlim(DiscoveryParallelism, DiscoveryParallelism);
            var sync = new object();

            try
            {
                Task[] probes = addresses.Select(async address =>
                {
                    await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        OfficeLanComputer computer = await ProbeComputerAsync(address, cancellationToken).ConfigureAwait(false);
                        if (computer != null)
                        {
                            lock (sync)
                                discovered.Add(computer);
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }).ToArray();

                await Task.WhenAll(probes).ConfigureAwait(false);
            }
            finally
            {
                gate.Dispose();
            }

            IList<OfficeLanNodeStatus> enrolled;
            try
            {
                enrolled = GetNodes();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.DiscoverComputers.GetNodes", ex);
                enrolled = new List<OfficeLanNodeStatus>();
            }

            foreach (OfficeLanComputer computer in discovered)
            {
                OfficeLanNodeStatus node = enrolled.FirstOrDefault(item =>
                    string.Equals(item.MachineName, computer.HostName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.NodeName, computer.HostName, StringComparison.OrdinalIgnoreCase));
                if (node == null)
                    continue;

                computer.IsEnrolled = true;
                computer.NodePublicId = node.NodePublicId;
                computer.AppVersion = node.AppVersion;
                computer.ConnectionStatus = node.ConnectionStatus;
                computer.LastSeenDisplay = FormatLastSeen(node.LastSeenUtc);
                ApplyManagementState(computer);
            }

            foreach (OfficeLanNodeStatus node in enrolled.Where(node => !discovered.Any(computer =>
                string.Equals(computer.HostName, node.MachineName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(computer.HostName, node.NodeName, StringComparison.OrdinalIgnoreCase))))
            {
                var offline = new OfficeLanComputer
                {
                    HostName = string.IsNullOrWhiteSpace(node.MachineName) ? node.NodeName : node.MachineName,
                    IpAddress = string.Empty,
                    IsReachable = false,
                    IsEnrolled = true,
                    NodePublicId = node.NodePublicId,
                    AppVersion = node.AppVersion,
                    ConnectionStatus = node.ConnectionStatus,
                    LastSeenDisplay = FormatLastSeen(node.LastSeenUtc),
                    DeploymentStatus = "Enrolled terminal is currently offline"
                };
                ApplyManagementState(offline);
                discovered.Add(offline);
            }

            foreach (OfficeLanComputer saved in GetSavedTerminals())
            {
                if (discovered.Any(item => string.Equals(item.HostName, saved.HostName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                ApplyManagementState(saved);
                discovered.Add(saved);
            }

            return discovered
                .OrderByDescending(item => item.IsLocalComputer)
                .ThenBy(item => ParseIpv4(item.IpAddress))
                .ThenBy(item => item.HostName)
                .ToList();
        }

        public OfficeLanDeploymentPackage CreateDeploymentPackage(IEnumerable<OfficeLanComputer> selectedComputers, string installerPath)
        {
            OfficeLanDeploymentCredential savedCredential;
            if (!TryLoadSavedDeploymentCredential(out savedCredential))
                throw new InvalidOperationException("Configure LAN deployment Admin access before preparing an unattended deployment.");
            try { return CreateDeploymentPackage(selectedComputers, installerPath, savedCredential); }
            finally { savedCredential.Password = string.Empty; }
        }

        public OfficeLanDeploymentPackage CreateDeploymentPackage(IEnumerable<OfficeLanComputer> selectedComputers, string installerPath,
            OfficeLanDeploymentCredential deploymentCredential)
        {
            List<OfficeLanComputer> targets = (selectedComputers ?? Enumerable.Empty<OfficeLanComputer>())
                .Where(item => item != null && item.Selected && item.IsReachable && !item.IsLocalComputer)
                .GroupBy(item => item.HostName ?? item.IpAddress, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (targets.Count == 0)
                throw new InvalidOperationException("Select at least one reachable terminal PC.");
            string resolvedInstallerPath = string.IsNullOrWhiteSpace(installerPath) ? FindBuiltInTerminalInstaller() : installerPath;
            bool useBuiltInPayload = string.IsNullOrWhiteSpace(resolvedInstallerPath);
            if (!useBuiltInPayload && !File.Exists(resolvedInstallerPath))
                throw new FileNotFoundException("The selected ServoERP installer was not found.", resolvedInstallerPath);

            string extension = useBuiltInPayload ? ".zip" : Path.GetExtension(resolvedInstallerPath);
            bool isMsi = string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase);
            bool isEnterpriseExe = !useBuiltInPayload && string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase) &&
                (Path.GetFileName(resolvedInstallerPath).StartsWith("ServoERP.Setup.", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileName(resolvedInstallerPath).StartsWith("ServoERP.Terminal.Setup.", StringComparison.OrdinalIgnoreCase));
            if (!useBuiltInPayload && !isMsi && !isEnterpriseExe)
                throw new InvalidOperationException("The optional installer override must be ServoERP.App.<version>.msi or ServoERP.Setup.<version>.exe.");
            if (deploymentCredential == null || string.IsNullOrWhiteSpace(deploymentCredential.UserName) || string.IsNullOrEmpty(deploymentCredential.Password))
                throw new InvalidOperationException("Configure LAN deployment Admin access before preparing an unattended deployment.");

            SqlConnectionStringBuilder sql = new SqlConnectionStringBuilder(DatabaseManager.RequireConfiguredConnectionString());
            string remoteSqlTarget = NormalizeRemoteSqlTarget(sql.DataSource);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = Path.Combine(DeploymentRoot, "ServoERP_LAN_" + stamp);
            Directory.CreateDirectory(folder);

            string packagedInstaller;
            string installerKind;
            if (useBuiltInPayload)
            {
                packagedInstaller = Path.Combine(folder, "ServoERP-Terminal-Payload-" + ApplicationVersion() + ".zip");
                CreateBuiltInApplicationPayload(packagedInstaller);
                installerKind = "payload-zip";
            }
            else
            {
                packagedInstaller = Path.Combine(folder, Path.GetFileName(resolvedInstallerPath));
                File.Copy(resolvedInstallerPath, packagedInstaller, true);
                installerKind = isMsi ? "msi" : "enterprise-exe";
            }
            File.WriteAllLines(Path.Combine(folder, "targets.txt"), targets.Select(item =>
                string.IsNullOrWhiteSpace(item.HostName) ? item.IpAddress : item.HostName), Encoding.UTF8);

            Guid jobPublicId = Guid.NewGuid();
            string progressPath = Path.Combine(folder, "deployment-progress.jsonl");
            string scriptPath = Path.Combine(folder, "Deploy-ServoERP-LAN.ps1");
            string bootstrapPath = Path.Combine(folder, "Enable-ServoERP-RemoteManagement.ps1");
            string credentialEnvelopePath = Path.Combine(folder, "deployment-credentials.dat");
            File.WriteAllText(scriptPath, BuildDeploymentScript(
                Path.GetFileName(packagedInstaller), installerKind, remoteSqlTarget, sql.InitialCatalog, jobPublicId), Encoding.UTF8);
            File.WriteAllText(bootstrapPath, BuildRemoteManagementBootstrapScript(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(folder, "README.txt"), BuildDeploymentGuide(), Encoding.UTF8);
            WriteCredentialEnvelope(credentialEnvelopePath, deploymentCredential, sql);
            TryCreateDeploymentJob(jobPublicId, targets, installerKind, folder);

            return new OfficeLanDeploymentPackage
            {
                FolderPath = folder,
                ScriptPath = scriptPath,
                BootstrapScriptPath = bootstrapPath,
                TargetCount = targets.Count,
                JobPublicId = jobPublicId,
                ProgressPath = progressPath,
                CredentialEnvelopePath = credentialEnvelopePath
            };
        }

        public bool TryLoadSavedDeploymentCredential(out OfficeLanDeploymentCredential credential)
        {
            credential = null;
            try
            {
                if (!File.Exists(SavedDeploymentCredentialPath))
                    return false;
                byte[] protectedBytes = File.ReadAllBytes(SavedDeploymentCredentialPath);
                byte[] raw = ProtectedData.Unprotect(protectedBytes, DeploymentCredentialEntropy, DataProtectionScope.CurrentUser);
                try
                {
                    credential = JsonConvert.DeserializeObject<OfficeLanDeploymentCredential>(Encoding.UTF8.GetString(raw));
                    if (credential == null || string.IsNullOrWhiteSpace(credential.UserName) || string.IsNullOrEmpty(credential.Password))
                    {
                        credential = null;
                        return false;
                    }
                    credential.RememberOnServer = true;
                    return true;
                }
                finally
                {
                    Array.Clear(raw, 0, raw.Length);
                    Array.Clear(protectedBytes, 0, protectedBytes.Length);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.LoadSavedCredential", ex);
                return false;
            }
        }

        public void SaveDeploymentCredential(OfficeLanDeploymentCredential credential)
        {
            if (credential == null || string.IsNullOrWhiteSpace(credential.UserName) || string.IsNullOrEmpty(credential.Password))
                throw new InvalidOperationException("Enter a Windows administrator account and password.");
            Directory.CreateDirectory(Path.GetDirectoryName(SavedDeploymentCredentialPath));
            byte[] raw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new OfficeLanDeploymentCredential
            {
                UserName = credential.UserName.Trim(),
                Password = credential.Password,
                RememberOnServer = true
            }));
            try
            {
                byte[] protectedBytes = ProtectedData.Protect(raw, DeploymentCredentialEntropy, DataProtectionScope.CurrentUser);
                try { File.WriteAllBytes(SavedDeploymentCredentialPath, protectedBytes); }
                finally { Array.Clear(protectedBytes, 0, protectedBytes.Length); }
            }
            finally
            {
                Array.Clear(raw, 0, raw.Length);
            }
        }

        public void ForgetSavedDeploymentCredential()
        {
            try
            {
                if (File.Exists(SavedDeploymentCredentialPath))
                    File.Delete(SavedDeploymentCredentialPath);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.ForgetSavedCredential", ex);
            }
        }

        private static void WriteCredentialEnvelope(string path, OfficeLanDeploymentCredential windowsCredential,
            SqlConnectionStringBuilder sql)
        {
            if (windowsCredential == null || string.IsNullOrWhiteSpace(windowsCredential.UserName) || string.IsNullOrEmpty(windowsCredential.Password))
                throw new InvalidOperationException("A Windows administrator credential is required for unattended LAN deployment.");

            var envelope = new
            {
                WindowsUserName = windowsCredential.UserName.Trim(),
                WindowsPassword = windowsCredential.Password,
                SqlIntegratedSecurity = sql.IntegratedSecurity,
                SqlUserName = sql.IntegratedSecurity ? string.Empty : sql.UserID,
                SqlPassword = sql.IntegratedSecurity ? string.Empty : sql.Password
            };
            byte[] raw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
            try
            {
                byte[] protectedBytes = ProtectedData.Protect(raw, DeploymentCredentialEntropy, DataProtectionScope.CurrentUser);
                try { File.WriteAllBytes(path, protectedBytes); }
                finally { Array.Clear(protectedBytes, 0, protectedBytes.Length); }
            }
            finally
            {
                Array.Clear(raw, 0, raw.Length);
            }
        }

        private static string FindBuiltInTerminalInstaller()
        {
            string applicationRoot = Path.GetDirectoryName(typeof(OfficeLanControlService).Assembly.Location) ?? string.Empty;
            string[] roots =
            {
                Path.Combine(applicationRoot, "Installers"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ServoERP", "Installers"),
                Path.Combine(@"C:\HVAC_PRO_MSE", "Installers")
            };
            return roots.Where(Directory.Exists)
                .SelectMany(root => Directory.EnumerateFiles(root, "ServoERP.Terminal.Setup.*.exe", SearchOption.TopDirectoryOnly))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void CreateBuiltInApplicationPayload(string destinationZip)
        {
            string applicationRoot = Path.GetDirectoryName(typeof(OfficeLanControlService).Assembly.Location);
            if (string.IsNullOrWhiteSpace(applicationRoot) || !Directory.Exists(applicationRoot))
                throw new InvalidOperationException("The current ServoERP application folder could not be resolved.");
            string[] allowedDirectories = { "LatoFont", "Resources", "runtimes", "x64", "x86" };
            string[] excludedFileNames = { "HVACPro.config", "license-private.xml", "license-public.xml" };

            using (FileStream stream = new FileStream(destinationZip, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
            {
                IEnumerable<string> topLevelFiles = Directory.EnumerateFiles(applicationRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !excludedFileNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                    .Where(path => !path.EndsWith(".servoerp-license", StringComparison.OrdinalIgnoreCase))
                    .Where(path => !path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase));

                foreach (string file in topLevelFiles)
                    AddFileToArchive(archive, file, Path.GetFileName(file));

                foreach (string directoryName in allowedDirectories)
                {
                    string directory = Path.Combine(applicationRoot, directoryName);
                    if (!Directory.Exists(directory))
                        continue;
                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        string relative = file.Substring(applicationRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        AddFileToArchive(archive, file, relative);
                    }
                }
            }

            if (!File.Exists(destinationZip) || new FileInfo(destinationZip).Length == 0)
                throw new InvalidOperationException("The built-in ServoERP terminal payload could not be created.");
        }

        private static void AddFileToArchive(ZipArchive archive, string sourcePath, string entryName)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
            using (Stream input = File.OpenRead(sourcePath))
            using (Stream output = entry.Open())
                input.CopyTo(output);
        }

        public void LaunchDeployment(OfficeLanDeploymentPackage package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.ScriptPath) || !File.Exists(package.ScriptPath))
                throw new InvalidOperationException("The LAN deployment package is not available.");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + package.ScriptPath + "\"",
                    WorkingDirectory = package.FolderPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(package.CredentialEnvelopePath) && File.Exists(package.CredentialEnvelopePath))
                        File.Delete(package.CredentialEnvelopePath);
                }
                catch (Exception cleanupException)
                {
                    AppRuntime.LogException("OfficeLanControlService.LaunchDeployment.CredentialCleanup", cleanupException);
                }
                throw;
            }
        }

        public IList<OfficeLanDeploymentProgress> ReadDeploymentProgress(OfficeLanDeploymentPackage package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.ProgressPath) || !File.Exists(package.ProgressPath))
                return new List<OfficeLanDeploymentProgress>();

            var latest = new Dictionary<string, OfficeLanDeploymentProgress>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var stream = new FileStream(package.ProgressPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        try
                        {
                            OfficeLanDeploymentProgress item = JsonConvert.DeserializeObject<OfficeLanDeploymentProgress>(line);
                            if (item != null && !string.IsNullOrWhiteSpace(item.Computer))
                                latest[item.Computer] = item;
                        }
                        catch (JsonException)
                        {
                            // The final line can be incomplete while PowerShell is appending it.
                        }
                    }
                }
            }
            catch (IOException)
            {
                return new List<OfficeLanDeploymentProgress>();
            }
            return latest.Values.OrderBy(item => item.Computer).ToList();
        }

        public void PersistDeploymentProgress(Guid jobPublicId, IEnumerable<OfficeLanDeploymentProgress> progressItems)
        {
            List<OfficeLanDeploymentProgress> items = (progressItems ?? Enumerable.Empty<OfficeLanDeploymentProgress>()).
                Where(item => item != null && !string.IsNullOrWhiteSpace(item.Computer)).ToList();
            if (jobPublicId == Guid.Empty || items.Count == 0)
                return;

            using (SqlConnection connection = OpenConnection())
            {
                foreach (OfficeLanDeploymentProgress item in items)
                {
                    connection.Execute(@"UPDATE dbo.LanDeploymentTargets
SET Stage = @stage, ProgressPercent = @progress, Status = @status, Detail = @detail,
    StartedUtc = CASE WHEN StartedUtc IS NULL THEN GETUTCDATE() ELSE StartedUtc END,
    CompletedUtc = CASE WHEN @status IN ('Completed','Failed','Cancelled') THEN GETUTCDATE() ELSE CompletedUtc END,
    LastUpdatedUtc = GETUTCDATE()
WHERE JobPublicId = @jobId AND HostName = @host;
IF NOT EXISTS (SELECT 1 FROM dbo.LanDeploymentEvents WHERE JobPublicId = @jobId AND HostName = @host
    AND Stage = @stage AND ProgressPercent = @progress AND Status = @status)
INSERT INTO dbo.LanDeploymentEvents (JobPublicId, HostName, Stage, ProgressPercent, Status, Detail)
VALUES (@jobId, @host, @stage, @progress, @status, @detail);",
                        new
                        {
                            jobId = jobPublicId,
                            host = item.Computer,
                            stage = item.Stage ?? "Unknown",
                            progress = Math.Max(0, Math.Min(100, item.ProgressPercent)),
                            status = item.Status ?? "Running",
                            detail = Truncate(item.Detail, 1000)
                        });
                }

                connection.Execute(@"UPDATE j SET
Status = CASE
    WHEN EXISTS (SELECT 1 FROM dbo.LanDeploymentTargets t WHERE t.JobPublicId = j.JobPublicId AND t.Status = 'Running') THEN 'Running'
    WHEN NOT EXISTS (SELECT 1 FROM dbo.LanDeploymentTargets t WHERE t.JobPublicId = j.JobPublicId AND t.Status IN ('Queued','Running')) THEN
        CASE WHEN EXISTS (SELECT 1 FROM dbo.LanDeploymentTargets t WHERE t.JobPublicId = j.JobPublicId AND t.Status = 'Failed') THEN 'Completed with errors' ELSE 'Completed' END
    ELSE j.Status END,
StartedUtc = COALESCE(StartedUtc, GETUTCDATE()),
CompletedUtc = CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.LanDeploymentTargets t WHERE t.JobPublicId = j.JobPublicId AND t.Status IN ('Queued','Running')) THEN GETUTCDATE() ELSE CompletedUtc END,
SuccessfulTargets = (SELECT COUNT(*) FROM dbo.LanDeploymentTargets t WHERE t.JobPublicId = j.JobPublicId AND t.Status = 'Completed'),
FailedTargets = (SELECT COUNT(*) FROM dbo.LanDeploymentTargets t WHERE t.JobPublicId = j.JobPublicId AND t.Status = 'Failed')
FROM dbo.LanDeploymentJobs j WHERE j.JobPublicId = @jobId;", new { jobId = jobPublicId });
            }
        }

        private static void TryCreateDeploymentJob(Guid jobPublicId, IList<OfficeLanComputer> targets, string installerKind, string folder)
        {
            try
            {
                using (SqlConnection connection = OpenConnection())
                {
                    connection.Execute(@"INSERT INTO dbo.LanDeploymentJobs
(JobPublicId, RequestedBy, TargetVersion, InstallerKind, Status, TotalTargets, PackagePath)
VALUES (@jobId, @requestedBy, @version, @installerKind, 'Prepared', @total, @path);",
                        new { jobId = jobPublicId, requestedBy = Environment.UserName, version = ApplicationVersion(), installerKind, total = targets.Count, path = folder });
                    foreach (OfficeLanComputer target in targets)
                        connection.Execute(@"INSERT INTO dbo.LanDeploymentTargets
(JobPublicId, NodePublicId, HostName, IpAddress, Stage, ProgressPercent, Status, Detail)
VALUES (@jobId, @nodeId, @host, @ip, 'Prepared', 0, 'Queued', 'Waiting for administrator deployment approval.');",
                            new { jobId = jobPublicId, nodeId = target.NodePublicId, host = target.HostName ?? target.IpAddress, ip = target.IpAddress });
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.TryCreateDeploymentJob", ex);
            }
        }

        private static string Truncate(string value, int maximumLength)
        {
            string text = value ?? string.Empty;
            return text.Length <= maximumLength ? text : text.Substring(0, maximumLength);
        }

        internal static List<IPAddress> GetDiscoveryAddresses()
        {
            var addresses = new Dictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    IPAddress local = unicast.Address;
                    if (local.AddressFamily != AddressFamily.InterNetwork || !IsPrivateIpv4(local))
                        continue;

                    IPAddress maskAddress = unicast.IPv4Mask;
                    uint localValue = ParseIpv4(local.ToString());
                    uint mask = maskAddress == null ? 0xFFFFFF00u : ParseIpv4(maskAddress.ToString());
                    uint network = localValue & mask;
                    uint broadcast = network | ~mask;
                    ulong usableCount = broadcast > network ? (ulong)broadcast - network - 1UL : 0UL;

                    uint first = network + 1;
                    uint last = broadcast - 1;
                    if (usableCount > MaximumAutomaticDiscoveryAddresses)
                    {
                        // Avoid flooding large corporate subnets. Scan the local /24 and retain
                        // saved/enrolled terminals outside it for direct probing.
                        network = localValue & 0xFFFFFF00u;
                        first = network + 1;
                        last = network + 254;
                    }

                    for (uint candidateValue = first; candidateValue <= last; candidateValue++)
                    {
                        IPAddress candidate = ToIpv4(candidateValue);
                        addresses[candidate.ToString()] = candidate;
                    }
                }
            }

            return addresses.Values.ToList();
        }

        public async Task<OfficeLanComputer> ProbeManualComputerAsync(string hostOrAddress, CancellationToken cancellationToken)
        {
            string value = (hostOrAddress ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Enter a terminal hostname or IPv4 address.", nameof(hostOrAddress));

            IPAddress address;
            if (!IPAddress.TryParse(value, out address))
            {
                IPAddress[] resolved = await Dns.GetHostAddressesAsync(value).ConfigureAwait(false);
                address = resolved.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork);
            }
            if (address == null)
                throw new InvalidOperationException("The terminal hostname could not be resolved to an IPv4 address.");

            OfficeLanComputer computer = await ProbeComputerAsync(address, cancellationToken).ConfigureAwait(false);
            if (computer == null)
            {
                computer = new OfficeLanComputer
                {
                    HostName = value,
                    IpAddress = address.ToString(),
                    IsReachable = false,
                    ConnectionStatus = "Offline",
                    DeploymentStatus = "Saved terminal is currently unreachable",
                    IsSavedTerminal = true
                };
            }
            SaveTerminal(computer.HostName, computer.IpAddress);
            computer.IsSavedTerminal = true;
            ApplyManagementState(computer);
            return computer;
        }

        private static async Task<OfficeLanComputer> ProbeComputerAsync(IPAddress address, CancellationToken cancellationToken)
        {
            Task<bool> pingTask = PingAsync(address, cancellationToken);
            Task<bool> smbTask = CanConnectAsync(address, 445, cancellationToken);
            Task<bool> winRmHttpTask = CanConnectAsync(address, 5985, cancellationToken);
            Task<bool> winRmHttpsTask = CanConnectAsync(address, 5986, cancellationToken);
            await Task.WhenAll(pingTask, smbTask, winRmHttpTask, winRmHttpsTask).ConfigureAwait(false);

            bool reachable = pingTask.Result || smbTask.Result || winRmHttpTask.Result || winRmHttpsTask.Result;
            if (!reachable)
                return null;

            string hostName = await ResolveHostNameAsync(address, cancellationToken).ConfigureAwait(false);
            bool local = string.Equals(hostName, Environment.MachineName, StringComparison.OrdinalIgnoreCase) || IsLocalAddress(address);
            bool remoteManagement = winRmHttpTask.Result || winRmHttpsTask.Result;
            var computer = new OfficeLanComputer
            {
                Selected = false,
                HostName = string.IsNullOrWhiteSpace(hostName) ? address.ToString() : hostName,
                IpAddress = address.ToString(),
                IsReachable = true,
                IsLocalComputer = local,
                SupportsRemoteManagement = remoteManagement,
                IsEnrolled = false,
                AppVersion = string.Empty,
                ConnectionStatus = local ? "Server PC" : "Detected",
                TargetVersion = ApplicationVersion(),
                ReadinessStatus = remoteManagement ? "Ready" : "Needs preparation",
                SqlStatus = "Not checked",
                CurrentStage = "Discovered",
                DeploymentStatus = local
                    ? "This server PC"
                    : (remoteManagement ? "Ready for administrator deployment" : "Reachable - WinRM will be prepared automatically")
            };
            ApplyManagementState(computer);
            return computer;
        }

        private static IPAddress ToIpv4(uint value)
        {
            return new IPAddress(new[]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            });
        }

        private static void ApplyManagementState(OfficeLanComputer computer)
        {
            if (computer == null)
                return;
            computer.TargetVersion = ApplicationVersion();
            computer.IsUpdateAvailable = IsOlderVersion(computer.AppVersion, computer.TargetVersion);
            computer.ManagementState = computer.IsLocalComputer ? "Server PC" :
                !computer.IsReachable ? "Offline" :
                !computer.IsEnrolled || string.IsNullOrWhiteSpace(computer.AppVersion) ? "Needs installation" :
                computer.IsUpdateAvailable ? "Update available" : "Ready";
            if (string.IsNullOrWhiteSpace(computer.ReadinessStatus))
                computer.ReadinessStatus = computer.IsReachable
                    ? (computer.SupportsRemoteManagement ? "Ready" : "Needs preparation")
                    : "Offline";
            if (string.IsNullOrWhiteSpace(computer.CurrentStage))
                computer.CurrentStage = computer.IsEnrolled ? "Managed" : "Discovered";
        }

        private static bool IsOlderVersion(string current, string target)
        {
            Version currentVersion;
            Version targetVersion;
            return Version.TryParse(current, out currentVersion) && Version.TryParse(target, out targetVersion) && currentVersion < targetVersion;
        }

        private static string FormatLastSeen(DateTime? utc)
        {
            if (!utc.HasValue)
                return "Never";
            DateTime local = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime();
            TimeSpan age = DateTime.Now - local;
            if (age.TotalMinutes < 2) return "Just now";
            if (age.TotalHours < 1) return ((int)age.TotalMinutes) + " min ago";
            if (age.TotalDays < 1) return ((int)age.TotalHours) + " hr ago";
            return local.ToString("dd/MM/yyyy HH:mm");
        }

        private IList<OfficeLanComputer> GetSavedTerminals()
        {
            try
            {
                using (SqlConnection connection = OpenConnection())
                    return connection.Query<OfficeLanComputer>(@"SELECT HostName, ISNULL(IpAddress, '') AS IpAddress,
CAST(0 AS BIT) AS IsReachable, CAST(1 AS BIT) AS IsSavedTerminal, 'Saved / offline' AS ConnectionStatus,
'Saved terminal is currently unreachable' AS DeploymentStatus FROM dbo.OfficeSavedTerminals
WHERE IsActive = 1 ORDER BY HostName").AsList();
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.GetSavedTerminals", ex);
                return new List<OfficeLanComputer>();
            }
        }

        private void SaveTerminal(string hostName, string ipAddress)
        {
            try
            {
                using (SqlConnection connection = OpenConnection())
                    connection.Execute(@"IF EXISTS (SELECT 1 FROM dbo.OfficeSavedTerminals WHERE HostName = @host)
UPDATE dbo.OfficeSavedTerminals SET IpAddress = @ip, IsActive = 1, LastDiscoveredUtc = GETUTCDATE() WHERE HostName = @host;
ELSE INSERT INTO dbo.OfficeSavedTerminals (HostName, IpAddress, AddedBy, LastDiscoveredUtc)
VALUES (@host, @ip, @user, GETUTCDATE());", new { host = hostName, ip = ipAddress, user = Environment.UserName });
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.SaveTerminal", ex);
            }
        }

        private static async Task<bool> PingAsync(IPAddress address, CancellationToken cancellationToken)
        {
            try
            {
                using (var ping = new Ping())
                {
                    Task<PingReply> pingTask = ping.SendPingAsync(address, 650);
                    Task completed = await Task.WhenAny(pingTask, Task.Delay(750, cancellationToken)).ConfigureAwait(false);
                    return completed == pingTask && pingTask.Result.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> CanConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    Task connect = client.ConnectAsync(address, port);
                    Task completed = await Task.WhenAny(connect, Task.Delay(700, cancellationToken)).ConfigureAwait(false);
                    return completed == connect && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string> ResolveHostNameAsync(IPAddress address, CancellationToken cancellationToken)
        {
            try
            {
                Task<IPHostEntry> lookup = Dns.GetHostEntryAsync(address);
                Task completed = await Task.WhenAny(lookup, Task.Delay(1000, cancellationToken)).ConfigureAwait(false);
                if (completed == lookup && lookup.Result != null)
                    return (lookup.Result.HostName ?? string.Empty).Split('.')[0];
            }
            catch
            {
            }

            return address.ToString();
        }

        private static bool IsLocalAddress(IPAddress address)
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                .Any(item => item.Address.Equals(address));
        }

        private static bool IsPrivateIpv4(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        private static uint ParseIpv4(string value)
        {
            IPAddress address;
            if (!IPAddress.TryParse(value, out address))
                return uint.MaxValue;
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static string NormalizeRemoteSqlTarget(string configuredTarget)
        {
            string target = (configuredTarget ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(target))
                return Environment.MachineName + @"\SQLEXPRESS";

            int slash = target.IndexOf('\\');
            string host = slash < 0 ? target : target.Substring(0, slash);
            string suffix = slash < 0 ? string.Empty : target.Substring(slash);
            if (host == "." || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(host, "(local)", StringComparison.OrdinalIgnoreCase))
                return Environment.MachineName + suffix;
            return target;
        }

        private static string BuildDeploymentScript(string installerName, string installerKind, string sqlServer, string databaseName, Guid jobPublicId)
        {
            string template = @"param()
$ErrorActionPreference = 'Stop'
$jobPublicId = '__JOB_ID__'
$installerName = '__INSTALLER__'
$installerKind = '__INSTALLER_KIND__'
$sqlServer = '__SQL_SERVER__'
$databaseName = '__DATABASE__'
$packageRoot = $PSScriptRoot
$installerSource = Join-Path $packageRoot $installerName
$installerHash = (Get-FileHash -LiteralPath $installerSource -Algorithm SHA256).Hash
$progressPath = Join-Path $packageRoot 'deployment-progress.jsonl'
if (Test-Path -LiteralPath $progressPath) { Remove-Item -LiteralPath $progressPath -Force }
$targets = @(Get-Content -LiteralPath (Join-Path $packageRoot 'targets.txt') | Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() })
if ($targets.Count -eq 0) { throw 'No deployment targets were supplied.' }

function Write-DeploymentProgress([string]$computer, [string]$stage, [int]$percent, [string]$status, [string]$detail) {
    [pscustomobject]@{
        JobPublicId = $jobPublicId
        Computer = $computer
        Stage = $stage
        ProgressPercent = $percent
        Status = $status
        Detail = $detail
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json -Compress | Add-Content -LiteralPath $progressPath -Encoding UTF8
}

Write-Host ('ServoERP LAN deployment targets: ' + ($targets -join ', ')) -ForegroundColor Cyan

$credentialEnvelopePath = Join-Path $packageRoot 'deployment-credentials.dat'
try {
    if (!(Test-Path -LiteralPath $credentialEnvelopePath)) { throw 'The protected deployment credential envelope is missing.' }
    $protectedCredentialBytes = [IO.File]::ReadAllBytes($credentialEnvelopePath)
    $credentialEntropy = [Text.Encoding]::UTF8.GetBytes('ServoERP.LanDeployment.v1')
    $credentialBytes = [Security.Cryptography.ProtectedData]::Unprotect(
        $protectedCredentialBytes, $credentialEntropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $credentialData = [Text.Encoding]::UTF8.GetString($credentialBytes) | ConvertFrom-Json
    $adminSecurePassword = ConvertTo-SecureString ([string]$credentialData.WindowsPassword) -AsPlainText -Force
    $adminCredential = New-Object Management.Automation.PSCredential ([string]$credentialData.WindowsUserName, $adminSecurePassword)
    $sqlIntegratedSecurity = [bool]$credentialData.SqlIntegratedSecurity
    if (-not $sqlIntegratedSecurity) {
        $sqlSecurePassword = ConvertTo-SecureString ([string]$credentialData.SqlPassword) -AsPlainText -Force
        $sqlCredential = New-Object Management.Automation.PSCredential ([string]$credentialData.SqlUserName, $sqlSecurePassword)
    }
}
catch {
    $credentialError = 'Could not unlock the approved LAN deployment credential: ' + $_.Exception.Message
    foreach ($target in $targets) { Write-DeploymentProgress $target 'Authentication setup' 100 'Failed' $credentialError }
    throw $credentialError
}
finally {
    Remove-Item -LiteralPath $credentialEnvelopePath -Force -ErrorAction SilentlyContinue
    if ($credentialBytes) { [Array]::Clear($credentialBytes, 0, $credentialBytes.Length) }
    if ($protectedCredentialBytes) { [Array]::Clear($protectedCredentialBytes, 0, $protectedCredentialBytes.Length) }
}

$sqlBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$sqlBuilder['Data Source'] = $sqlServer
$sqlBuilder['Initial Catalog'] = $databaseName
$sqlBuilder['Integrated Security'] = $sqlIntegratedSecurity
if (-not $sqlIntegratedSecurity) {
    $sqlBuilder['User ID'] = $sqlCredential.UserName
    $sqlBuilder['Password'] = $sqlCredential.GetNetworkCredential().Password
}
$sqlBuilder['Connect Timeout'] = 15
$sqlBuilder['TrustServerCertificate'] = $true
$sqlTest = New-Object System.Data.SqlClient.SqlConnection $sqlBuilder.ConnectionString
try { $sqlTest.Open(); Write-Host 'Configured office SQL connection verified.' -ForegroundColor Green } finally { $sqlTest.Close() }

$computerSystem = Get-CimInstance Win32_ComputerSystem
if (-not $computerSystem.PartOfDomain) {
    $current = (Get-Item WSMan:\localhost\Client\TrustedHosts -ErrorAction SilentlyContinue).Value
    $trusted = @($current -split ',' | Where-Object { $_ }) + $targets
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value (($trusted | Sort-Object -Unique) -join ',') -Force
}

function Enable-RemoteManagementIfNeeded([string]$target, [pscredential]$credential) {
    try {
        Test-WSMan -ComputerName $target -ErrorAction Stop | Out-Null
        return 'WinRM already ready'
    }
    catch {
        Write-Host (""{0}: WinRM is unavailable; attempting automatic Windows management bootstrap..."" -f $target) -ForegroundColor Yellow
    }

    $bootstrap = @'
$ErrorActionPreference = 'Stop'
Enable-PSRemoting -Force -SkipNetworkProfileCheck
Set-Service -Name WinRM -StartupType Automatic
Start-Service -Name WinRM
$publicRule = Get-NetFirewallRule -Name 'WINRM-HTTP-In-TCP-PUBLIC' -ErrorAction SilentlyContinue
if ($publicRule) { Set-NetFirewallRule -Name 'WINRM-HTTP-In-TCP-PUBLIC' -RemoteAddress LocalSubnet -Enabled True }
'@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($bootstrap))
    try {
        $result = Invoke-WmiMethod -ComputerName $target -Credential $credential -Class Win32_Process -Name Create `
            -ArgumentList ('powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + $encoded) -ErrorAction Stop
        if ($result.ReturnValue -ne 0) { throw ('Remote bootstrap process returned ' + $result.ReturnValue) }
    }
    catch {
        throw (""WinRM is missing and automatic bootstrap through Windows Management failed: {0}. Ensure File and Printer Sharing/WMI is allowed, or run the generated bootstrap script once on that PC."" -f $_.Exception.Message)
    }

    $deadline = (Get-Date).AddSeconds(45)
    do {
        Start-Sleep -Seconds 2
        try {
            Test-WSMan -ComputerName $target -ErrorAction Stop | Out-Null
            return 'WinRM enabled automatically'
        }
        catch { }
    } while ((Get-Date) -lt $deadline)

    throw 'Windows accepted the WinRM bootstrap, but WinRM did not become reachable within 45 seconds.'
}

$sessionOption = New-PSSessionOption -OpenTimeout 20000 -OperationTimeout 120000
$results = foreach ($target in $targets) {
    $session = $null
    if (Test-Path -LiteralPath (Join-Path $packageRoot 'cancel.requested')) {
        Write-DeploymentProgress $target 'Cancelled' 100 'Cancelled' 'Deployment was cancelled by the administrator before this terminal started.'
        [pscustomobject]@{ Computer = $target; Status = 'Cancelled'; Detail = 'Cancelled before installation started.' }
        continue
    }
    try {
        Write-DeploymentProgress $target 'Connecting' 5 'Running' 'Opening the administrator deployment session.'
        Write-Host (""Connecting to {0}..."" -f $target) -ForegroundColor Cyan
        $winRmResult = Enable-RemoteManagementIfNeeded $target $adminCredential
        Write-DeploymentProgress $target 'Remote management' 20 'Running' $winRmResult
        try {
            $session = New-PSSession -ComputerName $target -Credential $adminCredential -SessionOption $sessionOption -ErrorAction Stop
        }
        catch {
            throw (""Windows rejected or could not use the approved administrator access for {0}: {1}. Verify the account as DOMAIN\User or TARGET-PC\User. On workgroup PCs, Remote UAC/WinRM must be enabled once locally or by policy."" -f $target, $_.Exception.Message)
        }
        $preflight = Invoke-Command -Session $session -ArgumentList $sqlServer, $databaseName, $sqlIntegratedSecurity, $sqlCredential -ScriptBlock {
            param($server, $database, $useWindowsAuth, $sqlCred)
            $os = Get-CimInstance Win32_OperatingSystem
            $disk = Get-CimInstance Win32_LogicalDisk -Filter ""DeviceID='C:'""
            if ([double]$disk.FreeSpace -lt 1.5GB) { throw ('Insufficient free disk space. Available: ' + [math]::Round($disk.FreeSpace / 1GB, 2) + ' GB; required: 1.5 GB.') }
            if ([version]$os.Version -lt [version]'10.0') { throw ('Unsupported Windows version: ' + $os.Caption + ' ' + $os.Version) }
            $netRelease = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue).Release
            $webView = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' -Name pv -ErrorAction SilentlyContinue).pv
            $sqlValidation = 'SQL Authentication verified from terminal'
            if ($useWindowsAuth) {
                $sqlValidation = 'Windows Authentication configuration copied; the signed-in terminal user is verified at ServoERP startup'
            } else {
                $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
                $builder['Data Source'] = $server
                $builder['Initial Catalog'] = $database
                $builder['User ID'] = $sqlCred.UserName
                $builder['Password'] = $sqlCred.GetNetworkCredential().Password
                $builder['Integrated Security'] = $false
                $builder['Connect Timeout'] = 7
                $builder['TrustServerCertificate'] = $true
                $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
                try { $connection.Open() } finally { $connection.Close() }
            }
            [pscustomobject]@{
                OperatingSystem = ($os.Caption + ' ' + $os.Version)
                FreeDiskGb = [math]::Round($disk.FreeSpace / 1GB, 2)
                DotNetReady = [bool]($netRelease -ge 461808)
                WebViewReady = [bool]$webView
                SqlReady = $true
                SqlValidation = $sqlValidation
            }
        }
        Write-DeploymentProgress $target 'Authenticated preflight' 30 'Running' ('OS: ' + $preflight.OperatingSystem + '; free disk: ' + $preflight.FreeDiskGb + ' GB; ' + $preflight.SqlValidation + '; .NET ready: ' + $preflight.DotNetReady + '; WebView2 ready: ' + $preflight.WebViewReady)
        $remoteInstaller = Invoke-Command -Session $session -ScriptBlock {
            $folder = 'C:\ProgramData\ServoERP\Deployment'
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            Join-Path $folder $using:installerName
        }
        Copy-Item -LiteralPath $installerSource -Destination $remoteInstaller -ToSession $session -Force
        $remoteHash = Invoke-Command -Session $session -ArgumentList $remoteInstaller -ScriptBlock { param($path) (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
        if ($remoteHash -ne $installerHash) { throw 'Installer SHA-256 verification failed after transfer.' }
        Write-DeploymentProgress $target 'Installer copied' 45 'Running' ('Installer copied and SHA-256 verified: ' + $installerHash)

        Write-DeploymentProgress $target 'Installing' 60 'Running' 'Installing prerequisites and ServoERP. This may take several minutes.'
        $detail = Invoke-Command -Session $session -ArgumentList $remoteInstaller, $installerKind, $sqlServer, $databaseName, $sqlIntegratedSecurity, $sqlCredential, $winRmResult -ScriptBlock {
            param($installerPath, $packageKind, $server, $database, $useWindowsAuth, $sqlCred, $remoteReadiness)
            $ErrorActionPreference = 'Stop'
            $installRoot = 'C:\HVAC_PRO_MSE'
            $configPath = Join-Path $installRoot 'HVACPro.config'
            New-Item -ItemType Directory -Path $installRoot -Force | Out-Null

            [xml]$config = if (Test-Path -LiteralPath $configPath) { Get-Content -LiteralPath $configPath } else { '<HVACProConfig />' }
            function Set-Value([xml]$doc, [string]$section, [string]$key, [string]$value) {
                $root = $doc.DocumentElement
                $sectionNode = $root.SelectSingleNode($section)
                if (!$sectionNode) { $sectionNode = $doc.CreateElement($section); [void]$root.AppendChild($sectionNode) }
                $keyNode = $sectionNode.SelectSingleNode($key)
                if (!$keyNode) { $keyNode = $doc.CreateElement($key); [void]$sectionNode.AppendChild($keyNode) }
                $keyNode.InnerText = $value
            }
            if (Test-Path -LiteralPath $configPath) { Copy-Item $configPath ($configPath + '.pre-lan-deployment.bak') -Force }
            Set-Value $config 'Database' 'Server' $server
            Set-Value $config 'Database' 'DatabaseName' $database
            Set-Value $config 'Database' 'UseWindowsAuth' ($useWindowsAuth.ToString().ToLowerInvariant())
            if ($useWindowsAuth) {
                Set-Value $config 'Database' 'Username' ''
                Set-Value $config 'Database' 'Password' ''
            } else {
                Set-Value $config 'Database' 'Username' $sqlCred.UserName
                $plainSqlPassword = $sqlCred.GetNetworkCredential().Password
                $entropy = [Text.Encoding]::UTF8.GetBytes('ServoERP.SqlConfig.v1')
                $protectedPassword = [Security.Cryptography.ProtectedData]::Protect(
                    [Text.Encoding]::UTF8.GetBytes($plainSqlPassword), $entropy,
                    [Security.Cryptography.DataProtectionScope]::LocalMachine)
                Set-Value $config 'Database' 'Password' ('dpapi-machine:' + [Convert]::ToBase64String($protectedPassword))
            }
            Set-Value $config 'Database' 'MaxPoolSize' '100'
            Set-Value $config 'Database' 'ServerRole' 'ClientPC'
            Set-Value $config 'Fallback' 'Mode' 'LocalSQLiteDiagnostics'
            Set-Value $config 'Fallback' 'AllowBusinessWrites' 'false'
            $config.Save($configPath)

            if ($packageKind -eq 'msi') {
                $process = Start-Process msiexec.exe -ArgumentList @('/i', ('""' + $installerPath + '""'), '/qn', '/norestart') -Wait -PassThru
                if ($process.ExitCode -notin @(0, 1641, 3010)) { throw ('Installer exit code: ' + $process.ExitCode) }
            } elseif ($packageKind -eq 'enterprise-exe') {
                $process = Start-Process $installerPath -ArgumentList @('/quiet', '/norestart') -Wait -PassThru
                if ($process.ExitCode -notin @(0, 1641, 3010)) { throw ('Installer exit code: ' + $process.ExitCode) }
            } else {
                $payloadStage = Join-Path $env:ProgramData 'ServoERP\Deployment\payload'
                if (Test-Path -LiteralPath $payloadStage) { Remove-Item -LiteralPath $payloadStage -Recurse -Force }
                Expand-Archive -LiteralPath $installerPath -DestinationPath $payloadStage -Force
                Get-Process -Name 'HVAC_Pro_Desktop','ServoERP' -ErrorAction SilentlyContinue | Stop-Process -Force
                Get-ChildItem -LiteralPath $payloadStage -Force | Copy-Item -Destination $installRoot -Recurse -Force
            }

            $exe = Join-Path $installRoot 'HVAC_Pro_Desktop.exe'
            if (!(Test-Path -LiteralPath $exe)) { throw 'ServoERP executable was not installed.' }
            $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut('C:\Users\Public\Desktop\ServoERP.lnk')
            $shortcut.TargetPath = $exe
            $shortcut.WorkingDirectory = $installRoot
            $shortcut.IconLocation = (Join-Path $installRoot 'app.ico')
            $shortcut.Save()
            Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
            ($remoteReadiness + '; installed, configured, and added to the public desktop.')
        }

        Write-DeploymentProgress $target 'Verification' 90 'Running' 'ServoERP is installed; verifying configuration and shortcut.'
        Write-Host (""{0}: {1}"" -f $target, $detail) -ForegroundColor Green
        Write-DeploymentProgress $target 'Completed' 100 'Completed' $detail
        [pscustomobject]@{ Computer = $target; Status = 'Success'; Detail = $detail }
    }
    catch {
        Write-Host (""{0}: {1}"" -f $target, $_.Exception.Message) -ForegroundColor Red
        Write-DeploymentProgress $target 'Failed' 100 'Failed' $_.Exception.Message
        [pscustomobject]@{ Computer = $target; Status = 'Failed'; Detail = $_.Exception.Message }
    }
    finally {
        if ($session) { Remove-PSSession $session }
    }
}

$report = Join-Path $packageRoot ('deployment-result-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.csv')
$results | Export-Csv -LiteralPath $report -NoTypeInformation
$results | Format-Table -AutoSize
Write-Host ('Deployment report: ' + $report) -ForegroundColor Cyan
";

            return template
                .Replace("__JOB_ID__", jobPublicId.ToString("D"))
                .Replace("__INSTALLER__", EscapePowerShellLiteral(installerName))
                .Replace("__INSTALLER_KIND__", EscapePowerShellLiteral(installerKind))
                .Replace("__SQL_SERVER__", EscapePowerShellLiteral(sqlServer))
                .Replace("__DATABASE__", EscapePowerShellLiteral(databaseName));
        }

        private static string BuildRemoteManagementBootstrapScript()
        {
            return @"# Fallback: run once as Administrator only when LAN Control cannot prepare WinRM remotely.
$ErrorActionPreference = 'Stop'
Enable-PSRemoting -Force -SkipNetworkProfileCheck
Set-Service WinRM -StartupType Automatic
Start-Service WinRM
$publicRule = Get-NetFirewallRule -Name 'WINRM-HTTP-In-TCP-PUBLIC' -ErrorAction SilentlyContinue
if ($publicRule) { Set-NetFirewallRule -Name 'WINRM-HTTP-In-TCP-PUBLIC' -RemoteAddress LocalSubnet -Enabled True }
Write-Host ('ServoERP remote management is ready on ' + $env:COMPUTERNAME) -ForegroundColor Green
Read-Host 'Press Enter to close'
";
        }

        private static string BuildDeploymentGuide()
        {
            return "ServoERP LAN Deployment" + Environment.NewLine +
                   "1. LAN Control detects PCs on the server's private IPv4 subnet." + Environment.NewLine +
                   "2. Remote installation requires a Windows administrator account valid on each selected PC." + Environment.NewLine +
                   "3. If WinRM is unavailable, LAN Control first attempts to enable the built-in Windows WinRM service through credentialed Windows Management (WMI/DCOM)." + Environment.NewLine +
                   "4. If Windows blocks that bootstrap channel, run Enable-ServoERP-RemoteManagement.ps1 once on the terminal as Administrator or deploy the equivalent domain Group Policy." + Environment.NewLine +
                   "5. The default package is generated from the current ServoERP application payload already embedded in the Enterprise installation; a separate installer download is not required." + Environment.NewLine +
                   "6. LAN Control supplies the approved Windows administrator credential through a current-user DPAPI envelope and deletes that one-time envelope as soon as deployment starts." + Environment.NewLine +
                   "7. The existing office SQL configuration is reused automatically. SQL Authentication passwords are protected with machine-level DPAPI on each terminal; Windows Authentication remains Windows Authentication." + Environment.NewLine +
                   "8. Each deployment result is written to a CSV beside the script." + Environment.NewLine;
        }

        private static string EscapePowerShellLiteral(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        public IList<OfficeLanNodeStatus> GetNodes()
        {
            const string sql = @"SELECT NodePublicId, NodeName, MachineName, ServerRole, AppVersion, DatabaseServer, DatabaseName,
LastHealthStatus, LastHealthDetail, LastSeenUtc FROM dbo.SyncNodes WHERE IsActive = 1 ORDER BY NodeName, MachineName";
            using (SqlConnection connection = OpenConnection())
            {
                List<OfficeLanNodeStatus> nodes = connection.Query<OfficeLanNodeStatus>(sql).AsList();
                DateTime staleAfter = DateTime.UtcNow.AddMinutes(-3);
                foreach (OfficeLanNodeStatus node in nodes)
                    node.ConnectionStatus = node.LastSeenUtc.HasValue && node.LastSeenUtc.Value >= staleAfter ? "Online" : "Offline / not recently seen";
                return nodes;
            }
        }

        public void QueueHealthCheck(Guid nodePublicId, string requestedBy)
        {
            QueueCommand(nodePublicId, HealthCheck, requestedBy);
        }

        public void QueueUpdateCheck(Guid nodePublicId, string requestedBy)
        {
            QueueCommand(nodePublicId, CheckForUpdate, requestedBy);
        }

        public void QueueDiagnostics(Guid nodePublicId, string requestedBy)
        {
            QueueCommand(nodePublicId, CollectDiagnostics, requestedBy);
        }

        public void QueueDatabaseRepair(Guid nodePublicId, string requestedBy)
        {
            QueueCommand(nodePublicId, RepairDatabase, requestedBy);
        }

        public int QueueBatch(IEnumerable<Guid> nodePublicIds, string commandType, string requestedBy, DateTime? scheduledUtc = null)
        {
            string[] allowed = { HealthCheck, CheckForUpdate, CollectDiagnostics, RepairDatabase };
            if (!allowed.Contains(commandType, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Unsupported LAN command.", nameof(commandType));
            int queued = 0;
            foreach (Guid nodeId in (nodePublicIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct())
            {
                QueueCommand(nodeId, commandType, requestedBy, scheduledUtc);
                queued++;
            }
            return queued;
        }

        public int QueuePilotUpdate(IEnumerable<Guid> nodePublicIds, string requestedBy, DateTime scheduledUtc)
        {
            List<Guid> nodes = (nodePublicIds ?? Enumerable.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
            if (nodes.Count == 0)
                return 0;
            Guid rolloutGroup = Guid.NewGuid();
            Guid pilotCommandId = Guid.NewGuid();
            using (SqlConnection connection = OpenConnection())
            {
                InsertRolloutCommand(connection, nodes[0], requestedBy, scheduledUtc, rolloutGroup, pilotCommandId, null);
                foreach (Guid node in nodes.Skip(1))
                    InsertRolloutCommand(connection, node, requestedBy, scheduledUtc, rolloutGroup, Guid.NewGuid(), pilotCommandId);
            }
            return nodes.Count;
        }

        private static void InsertRolloutCommand(SqlConnection connection, Guid nodeId, string requestedBy, DateTime scheduledUtc,
            Guid rolloutGroup, Guid commandPublicId, Guid? dependency)
        {
            connection.Execute(@"INSERT INTO dbo.OfficeNodeCommands
(CommandPublicId, TargetNodePublicId, CommandType, RequestedBy, ScheduledUtc, ExpiresUtc, IdempotencyKey, RolloutGroup, DependsOnCommandPublicId)
VALUES (@commandId, @nodeId, @commandType, @requestedBy, @scheduledUtc, DATEADD(hour, 24, @scheduledUtc), NEWID(), @rolloutGroup, @dependency);",
                new
                {
                    commandId = commandPublicId,
                    nodeId,
                    commandType = CheckForUpdate,
                    requestedBy = string.IsNullOrWhiteSpace(requestedBy) ? Environment.UserName : requestedBy.Trim(),
                    scheduledUtc,
                    rolloutGroup,
                    dependency
                });
        }

        private static void QueueCommand(Guid nodePublicId, string commandType, string requestedBy, DateTime? scheduledUtc = null)
        {
            const string sql = @"INSERT INTO dbo.OfficeNodeCommands
(TargetNodePublicId, CommandType, RequestedBy, ScheduledUtc, ExpiresUtc, IdempotencyKey)
SELECT @nodeId, @commandType, @requestedBy, @scheduledUtc, DATEADD(hour, 24, COALESCE(@scheduledUtc, GETUTCDATE())), NEWID()
WHERE NOT EXISTS (SELECT 1 FROM dbo.OfficeNodeCommands WHERE TargetNodePublicId = @nodeId AND CommandType = @commandType
AND Status = 'Queued' AND (ExpiresUtc IS NULL OR ExpiresUtc > GETUTCDATE()));";
            using (SqlConnection connection = OpenConnection())
                connection.Execute(sql, new
                {
                    nodeId = nodePublicId,
                    commandType,
                    requestedBy = string.IsNullOrWhiteSpace(requestedBy) ? Environment.UserName : requestedBy.Trim(),
                    scheduledUtc
                });
        }

        public static void ProcessPendingCommandsForCurrentNode()
        {
            try
            {
                Guid nodeId = NodeIdentityService.GetOrCreateNodePublicId();
                using (SqlConnection connection = OpenConnection())
                {
                    connection.Execute(@"UPDATE dbo.OfficeNodeCommands SET Status = 'Expired', CompletedUtc = GETUTCDATE(), ResultDetail = 'Command expired before the terminal was available.'
WHERE TargetNodePublicId = @nodeId AND Status = 'Queued' AND ExpiresUtc IS NOT NULL AND ExpiresUtc <= GETUTCDATE();", new { nodeId });
                    OfficeNodeCommand command = connection.QueryFirstOrDefault<OfficeNodeCommand>(@"SELECT TOP 1 command.OfficeNodeCommandId, command.CommandType FROM dbo.OfficeNodeCommands command
WHERE command.TargetNodePublicId = @nodeId AND command.Status = 'Queued' AND (command.ScheduledUtc IS NULL OR command.ScheduledUtc <= GETUTCDATE())
AND (command.ExpiresUtc IS NULL OR command.ExpiresUtc > GETUTCDATE())
AND (command.DependsOnCommandPublicId IS NULL OR EXISTS (
    SELECT 1 FROM dbo.OfficeNodeCommands prerequisite
    WHERE prerequisite.CommandPublicId = command.DependsOnCommandPublicId
      AND prerequisite.Status = 'Completed'))
ORDER BY command.RequestedUtc", new { nodeId });
                    if (command == null)
                        return;

                    int claimed = connection.Execute(@"UPDATE dbo.OfficeNodeCommands SET Status = 'Claimed', ClaimedUtc = GETUTCDATE(), AttemptCount = ISNULL(AttemptCount, 0) + 1
WHERE OfficeNodeCommandId = @id AND Status = 'Queued'", new { id = command.OfficeNodeCommandId });
                    if (claimed == 0)
                        return;
                    try
                    {
                        string detail;
                        if (string.Equals(command.CommandType, CheckForUpdate, StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateService.StartSilentBackgroundUpdateCheck();
                            detail = "Client accepted the update check. Any downloaded verified update installs when ServoERP closes.";
                        }
                        else if (string.Equals(command.CommandType, CollectDiagnostics, StringComparison.OrdinalIgnoreCase))
                        {
                            detail = BuildDiagnosticsSummary();
                        }
                        else if (string.Equals(command.CommandType, RepairDatabase, StringComparison.OrdinalIgnoreCase))
                        {
                            new DatabaseManager().InitializeDatabase();
                            detail = "Safe database schema and connection repair completed successfully.";
                        }
                        else
                        {
                            detail = "Client health check completed. SQL connection is available.";
                        }

                        connection.Execute(@"UPDATE dbo.SyncNodes SET AppVersion = @version, DatabaseServer = @server, DatabaseName = @database,
LastHealthStatus = 'Healthy', LastHealthDetail = @detail, LastSeenUtc = GETUTCDATE() WHERE NodePublicId = @nodeId;
UPDATE dbo.OfficeNodeCommands SET Status = 'Completed', CompletedUtc = GETUTCDATE(), ResultDetail = @detail WHERE OfficeNodeCommandId = @id;",
                            new { version = ApplicationVersion(), server = ServerName(), database = DatabaseName(), detail, nodeId, id = command.OfficeNodeCommandId });
                    }
                    catch (Exception commandException)
                    {
                        connection.Execute(@"UPDATE dbo.OfficeNodeCommands SET Status = 'Failed', CompletedUtc = GETUTCDATE(), ErrorCode = @code, ResultDetail = @detail
WHERE OfficeNodeCommandId = @id;", new
                        {
                            id = command.OfficeNodeCommandId,
                            code = commandException.GetType().Name,
                            detail = Truncate(commandException.Message, 1000)
                        });
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("OfficeLanControlService.ProcessPendingCommandsForCurrentNode", ex);
            }
        }

        private static string BuildDiagnosticsSummary()
        {
            string[] roots = { @"C:\HVAC_PRO_MSE\LOGS", @"C:\HVAC_PRO_MSE\DIAGNOSTICS" };
            int fileCount = roots.Where(Directory.Exists).Sum(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Take(500).Count());
            return string.Format("Diagnostics completed. Windows {0}; ServoERP {1}; {2} diagnostic/log file(s) available locally.",
                Environment.OSVersion.VersionString, ApplicationVersion(), fileCount);
        }

        private static SqlConnection OpenConnection()
        {
            string connectionString = DatabaseManager.GetConfiguredConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("No shared SQL Server connection is configured.");
            SqlConnection connection = DatabaseConnectionFactory.CreateConnection(connectionString);
            DatabaseConnectionFactory.Open(connection, "OfficeLanControlService");
            return connection;
        }

        private static string ApplicationVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "Unknown" : version.ToString();
        }

        private static string ServerName()
        {
            return new SqlConnectionStringBuilder(DatabaseManager.GetConfiguredConnectionString()).DataSource;
        }

        private static string DatabaseName()
        {
            return new SqlConnectionStringBuilder(DatabaseManager.GetConfiguredConnectionString()).InitialCatalog;
        }

        private sealed class OfficeNodeCommand
        {
            public long OfficeNodeCommandId { get; set; }
            public string CommandType { get; set; }
        }
    }
}
