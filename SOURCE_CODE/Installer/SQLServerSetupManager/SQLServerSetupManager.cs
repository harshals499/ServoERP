using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace HVACPro.SqlSetup
{
    public sealed class SQLServerSetupManager
    {
        public const string TargetInstanceName = "SQLEXPRESS";
        public const string LogPath = @"C:\HVAC_PRO_MSE\LOGS\SQLSetup.log";

        private const string SqlConnectionString = @"Server=.\SQLEXPRESS;Integrated Security=true;Connection Timeout=15;";
        private readonly bool _autoRemoveConflicts;
        private readonly string _sqlInstallerPath;

        public SQLServerSetupManager(bool autoRemoveConflicts, string sqlInstallerPath)
        {
            _autoRemoveConflicts = autoRemoveConflicts;
            _sqlInstallerPath = sqlInstallerPath;
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        }

        public IList<SqlInstanceInfo> DetectInstances()
        {
            Log("Detecting SQL Server instances from registry and WMI.");
            var instances = new Dictionary<string, SqlInstanceInfo>(StringComparer.OrdinalIgnoreCase);

            DetectInstancesFromRegistry(instances, RegistryView.Registry64);
            DetectInstancesFromRegistry(instances, RegistryView.Registry32);
            DetectInstancesFromWmi(instances);
            DetectServices(instances);

            foreach (SqlInstanceInfo instance in instances.Values.OrderBy(i => i.InstanceName))
            {
                Log("Detected " + instance);
            }

            if (instances.Count == 0)
            {
                Log("No SQL Server instances detected.");
            }

            return instances.Values.OrderBy(i => i.InstanceName).ToList();
        }

        public bool IsTargetInstanceInstalled(IList<SqlInstanceInfo> instances)
        {
            return instances.Any(i => string.Equals(i.InstanceName, TargetInstanceName, StringComparison.OrdinalIgnoreCase));
        }

        public bool RemoveConflictingInstances(IList<SqlInstanceInfo> instances)
        {
            var conflicts = instances
                .Where(i => !string.Equals(i.InstanceName, TargetInstanceName, StringComparison.OrdinalIgnoreCase))
                .Where(i => i.InstanceName.StartsWith("SQLEXPRESS", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (conflicts.Count == 0)
            {
                Log("No conflicting SQL Express instances found.");
                return true;
            }

            Log("Conflicting SQL Express instances found: " + string.Join(", ", conflicts.Select(c => c.InstanceName)));

            if (!_autoRemoveConflicts)
            {
                Console.WriteLine("The following conflicting SQL Server instances were found:");
                foreach (SqlInstanceInfo conflict in conflicts)
                {
                    Console.WriteLine("  - " + conflict.InstanceName);
                }

                Console.Write("Remove these old/conflicting instances now? [y/N]: ");
                string answer = Console.ReadLine();
                if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    Log("User cancelled conflicting instance removal.");
                    return false;
                }
            }

            foreach (SqlInstanceInfo conflict in conflicts)
            {
                try
                {
                    Log("Attempting to uninstall conflicting instance " + conflict.InstanceName + ".");
                    StopInstanceServices(conflict);
                    bool removed = UninstallInstanceWithSetup(conflict) || UninstallInstanceWithWmic(conflict);
                    if (!removed)
                    {
                        LogWarning("Uninstall did not report success for " + conflict.InstanceName + ". Continuing as requested.");
                    }
                }
                catch (Exception ex)
                {
                    LogException("Warning: uninstall failed for " + conflict.InstanceName + ". Continuing.", ex);
                }
            }

            return true;
        }

        public void InstallSQLExpress2019IfMissing(IList<SqlInstanceInfo> instances)
        {
            if (IsTargetInstanceInstalled(instances))
            {
                Log("Target SQL instance " + TargetInstanceName + " is already installed.");
                return;
            }

            InstallSQLExpress2019();
        }

        public void InstallSQLExpress2019()
        {
            string installerPath = ResolveSqlInstallerPath();
            Log("Installing SQL Server Express 2019 from " + installerPath + ".");

            string args =
                "/q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS " +
                "/SECURITYMODE=Integrated /SQLSYSADMINACCOUNTS=\"Builtin\\Administrators\" " +
                "/TCPENABLED=1 /NPENABLED=1 /IACCEPTSQLSERVERLICENSETERMS";

            ProcessResult result = RunProcess(installerPath, args, TimeSpan.FromMinutes(45));
            if (result.ExitCode != 0 && result.ExitCode != 3010)
            {
                throw new InvalidOperationException("SQL Server Express install failed with exit code " + result.ExitCode + ".");
            }

            Log("SQL Server Express install completed. ExitCode=" + result.ExitCode + ".");
        }

        public void EnableProtocols()
        {
            Log("Enabling Shared Memory and Named Pipes protocols.");

            EnableProtocolsByRegistry();

            RunProcess("cmd.exe",
                "/c reg add \"HKLM\\Software\\Microsoft\\MSSQLServer\\MSSQLServer\\SuperSocketNetLib\\NamedPipe\" /v Enabled /t REG_DWORD /d 1 /f",
                TimeSpan.FromMinutes(1), false);
            RunProcess("cmd.exe",
                "/c reg add \"HKLM\\Software\\Microsoft\\MSSQLServer\\MSSQLServer\\SuperSocketNetLib\\SharedMemory\" /v Enabled /t REG_DWORD /d 1 /f",
                TimeSpan.FromMinutes(1), false);
        }

        public void SetServicesToAutomatic()
        {
            Log("Setting SQL services to Automatic startup.");

            string[] serviceNames =
            {
                "MSSQL$SQLEXPRESS",
                "SQLAgent$SQLEXPRESS",
                "MSSQLFDLauncher$SQLEXPRESS",
                "SQLBrowser",
                "MSSQLSERVER",
                "SQLSERVERAGENT",
                "MSSQLFDLauncher"
            };

            foreach (string serviceName in serviceNames)
            {
                ConfigureServiceAutomatic(serviceName);
            }
        }

        public bool VerifyConnection()
        {
            Log("Verifying connection to .\\SQLEXPRESS.");
            if (TryOpenSqlConnection())
            {
                Log("Connection test succeeded.");
                return true;
            }

            LogWarning("Connection test failed. Restarting SQL services and retrying once.");
            RestartService("MSSQL$SQLEXPRESS");
            RestartService("SQLBrowser");
            Thread.Sleep(TimeSpan.FromSeconds(10));

            bool retrySucceeded = TryOpenSqlConnection();
            Log(retrySucceeded ? "Connection retry succeeded." : "Connection retry failed.");
            return retrySucceeded;
        }

        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public void Log(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine;
            File.AppendAllText(LogPath, line, Encoding.UTF8);
            Console.WriteLine(message);
        }

        public void LogWarning(string message)
        {
            Log("WARNING: " + message);
        }

        public void LogException(string message, Exception ex)
        {
            Log(message + Environment.NewLine + ex);
        }

        private void DetectInstancesFromRegistry(IDictionary<string, SqlInstanceInfo> instances, RegistryView view)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL"))
                {
                    if (key == null)
                    {
                        return;
                    }

                    foreach (string instanceName in key.GetValueNames())
                    {
                        string instanceId = Convert.ToString(key.GetValue(instanceName));
                        SqlInstanceInfo info = GetOrCreate(instances, instanceName);
                        info.InstanceId = FirstNonEmpty(info.InstanceId, instanceId);
                        info.Source = AppendSource(info.Source, "Registry " + view);
                    }
                }
            }
            catch (Exception ex)
            {
                LogException("Registry instance detection failed for " + view + ".", ex);
            }
        }

        private void DetectInstancesFromWmi(IDictionary<string, SqlInstanceInfo> instances)
        {
            string[] namespaces =
            {
                @"\\.\root\Microsoft\SqlServer\ComputerManagement15",
                @"\\.\root\Microsoft\SqlServer\ComputerManagement14",
                @"\\.\root\Microsoft\SqlServer\ComputerManagement13",
                @"\\.\root\Microsoft\SqlServer\ComputerManagement12",
                @"\\.\root\Microsoft\SqlServer\ComputerManagement11",
                @"\\.\root\Microsoft\SqlServer\ComputerManagement10"
            };

            foreach (string scopePath in namespaces)
            {
                try
                {
                    var scope = new ManagementScope(scopePath);
                    scope.Connect();
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM SqlServiceAdvancedProperty WHERE PropertyName='INSTANCEID'")))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string serviceName = Convert.ToString(obj["ServiceName"]);
                            string instanceId = Convert.ToString(obj["PropertyStrValue"]);
                            string instanceName = InstanceNameFromServiceName(serviceName);

                            if (string.IsNullOrWhiteSpace(instanceName))
                            {
                                continue;
                            }

                            SqlInstanceInfo info = GetOrCreate(instances, instanceName);
                            info.InstanceId = FirstNonEmpty(info.InstanceId, instanceId);
                            info.ServiceName = FirstNonEmpty(info.ServiceName, serviceName);
                            info.WmiNamespace = scopePath;
                            info.Source = AppendSource(info.Source, "WMI");
                        }
                    }
                }
                catch (ManagementException ex)
                {
                    LogWarning("WMI namespace unavailable: " + scopePath + " (" + ex.Message + ")");
                }
                catch (Exception ex)
                {
                    LogException("WMI detection failed for " + scopePath + ".", ex);
                }
            }
        }

        private void DetectServices(IDictionary<string, SqlInstanceInfo> instances)
        {
            try
            {
                foreach (ServiceController service in ServiceController.GetServices())
                {
                    string instanceName = InstanceNameFromServiceName(service.ServiceName);
                    if (string.IsNullOrWhiteSpace(instanceName))
                    {
                        continue;
                    }

                    SqlInstanceInfo info = GetOrCreate(instances, instanceName);
                    info.ServiceName = FirstNonEmpty(info.ServiceName, service.ServiceName);
                    info.Source = AppendSource(info.Source, "Service");
                }
            }
            catch (Exception ex)
            {
                LogException("Service detection failed.", ex);
            }
        }

        private bool UninstallInstanceWithSetup(SqlInstanceInfo instance)
        {
            string setupPath = FindSqlSetupPath(instance.InstanceId);
            if (string.IsNullOrWhiteSpace(setupPath))
            {
                LogWarning("SQL setup.exe not found for " + instance.InstanceName + ".");
                return false;
            }

            string args = "/q /ACTION=Uninstall /FEATURES=SQLEngine /INSTANCENAME=" + Quote(instance.InstanceName);
            ProcessResult result = RunProcess(setupPath, args, TimeSpan.FromMinutes(30), false);
            return result.ExitCode == 0 || result.ExitCode == 3010;
        }

        private bool UninstallInstanceWithWmic(SqlInstanceInfo instance)
        {
            string productName = "Microsoft SQL Server 2019%";
            string command = "/c wmic product where \"name like '" + productName + "'\" call uninstall /nointeractive";
            Log("Fallback uninstall through WMIC for " + instance.InstanceName + ": " + command);
            ProcessResult result = RunProcess("cmd.exe", command, TimeSpan.FromMinutes(30), false);
            return result.ExitCode == 0 || result.ExitCode == 3010;
        }

        private void EnableProtocolsByRegistry()
        {
            IList<SqlInstanceInfo> instances = DetectInstances();
            SqlInstanceInfo target = instances.FirstOrDefault(i => string.Equals(i.InstanceName, TargetInstanceName, StringComparison.OrdinalIgnoreCase));
            if (target == null || string.IsNullOrWhiteSpace(target.InstanceId))
            {
                LogWarning("Unable to find target instance id. Generic protocol registry commands will still be attempted.");
                return;
            }

            string protocolBase = @"SOFTWARE\Microsoft\Microsoft SQL Server\" + target.InstanceId + @"\MSSQLServer\SuperSocketNetLib";
            SetProtocolEnabled(protocolBase + @"\Np", RegistryView.Registry64);
            SetProtocolEnabled(protocolBase + @"\Sm", RegistryView.Registry64);
            SetProtocolEnabled(protocolBase + @"\NamedPipe", RegistryView.Registry64);
            SetProtocolEnabled(protocolBase + @"\SharedMemory", RegistryView.Registry64);
            SetProtocolEnabled(protocolBase + @"\Np", RegistryView.Registry32);
            SetProtocolEnabled(protocolBase + @"\Sm", RegistryView.Registry32);
        }

        private void SetProtocolEnabled(string subKey, RegistryView view)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (RegistryKey key = baseKey.CreateSubKey(subKey))
                {
                    key.SetValue("Enabled", 1, RegistryValueKind.DWord);
                    Log("Enabled protocol registry key HKLM\\" + subKey + " (" + view + ").");
                }
            }
            catch (Exception ex)
            {
                LogException("Failed to enable protocol key HKLM\\" + subKey + " (" + view + ").", ex);
            }
        }

        private void ConfigureServiceAutomatic(string serviceName)
        {
            if (!ServiceExists(serviceName))
            {
                Log("Service not present, skipping: " + serviceName);
                return;
            }

            RunProcess("cmd.exe", "/c sc config " + serviceName + " start= auto", TimeSpan.FromMinutes(1), false);
            StartService(serviceName);
        }

        private void StopInstanceServices(SqlInstanceInfo instance)
        {
            if (!string.IsNullOrWhiteSpace(instance.ServiceName))
            {
                StopService(instance.ServiceName);
            }

            StopService("MSSQL$" + instance.InstanceName);
            StopService("SQLAgent$" + instance.InstanceName);
            StopService("MSSQLFDLauncher$" + instance.InstanceName);
        }

        private bool TryOpenSqlConnection()
        {
            try
            {
                using (var connection = new SqlConnection(SqlConnectionString))
                {
                    connection.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogException("SQL connection test failed.", ex);
                return false;
            }
        }

        private void RestartService(string serviceName)
        {
            StopService(serviceName);
            StartService(serviceName);
        }

        private void StartService(string serviceName)
        {
            try
            {
                if (!ServiceExists(serviceName))
                {
                    return;
                }

                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status == ServiceControllerStatus.Running ||
                        service.Status == ServiceControllerStatus.StartPending)
                    {
                        return;
                    }

                    Log("Starting service " + serviceName + ".");
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(45));
                }
            }
            catch (Exception ex)
            {
                LogException("Unable to start service " + serviceName + ".", ex);
            }
        }

        private void StopService(string serviceName)
        {
            try
            {
                if (!ServiceExists(serviceName))
                {
                    return;
                }

                using (var service = new ServiceController(serviceName))
                {
                    if (service.Status == ServiceControllerStatus.Stopped ||
                        service.Status == ServiceControllerStatus.StopPending)
                    {
                        return;
                    }

                    Log("Stopping service " + serviceName + ".");
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(45));
                }
            }
            catch (Exception ex)
            {
                LogException("Unable to stop service " + serviceName + ".", ex);
            }
        }

        private bool ServiceExists(string serviceName)
        {
            return ServiceController.GetServices().Any(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveSqlInstallerPath()
        {
            if (!string.IsNullOrWhiteSpace(_sqlInstallerPath) && File.Exists(_sqlInstallerPath))
            {
                return _sqlInstallerPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "SQLEXPR_x64_ENU.exe"),
                Path.Combine(baseDir, "Prerequisites", "SQLEXPR_x64_ENU.exe"),
                Path.Combine(baseDir, "..", "Prerequisites", "SQLEXPR_x64_ENU.exe"),
                @"C:\HVAC_PRO_MSE\SOURCE_CODE\Installer\Prerequisites\SQLEXPR_x64_ENU.exe"
            };

            foreach (string candidate in candidates)
            {
                string path = Path.GetFullPath(candidate);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("SQL Server Express 2019 installer was not found.", _sqlInstallerPath);
        }

        private string FindSqlSetupPath(string instanceId)
        {
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                string setup = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft SQL Server",
                    instanceId,
                    "Setup Bootstrap",
                    "SQLServer2019",
                    "setup.exe");
                if (File.Exists(setup))
                {
                    return setup;
                }
            }

            string bootstrapRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft SQL Server",
                "150",
                "Setup Bootstrap",
                "SQL2019",
                "setup.exe");

            return File.Exists(bootstrapRoot) ? bootstrapRoot : null;
        }

        private ProcessResult RunProcess(string fileName, string arguments, TimeSpan timeout)
        {
            return RunProcess(fileName, arguments, timeout, true);
        }

        private ProcessResult RunProcess(string fileName, string arguments, TimeSpan timeout, bool throwOnFailure)
        {
            Log("Running: " + fileName + " " + arguments);
            var output = new StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    throw new System.TimeoutException("Process timed out: " + fileName + " " + arguments);
                }

                process.WaitForExit();
                var result = new ProcessResult(process.ExitCode, output.ToString());
                Log("Process exit code: " + result.ExitCode);
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    Log("Process output:" + Environment.NewLine + result.Output.Trim());
                }

                if (throwOnFailure && result.ExitCode != 0)
                {
                    throw new InvalidOperationException("Process failed with exit code " + result.ExitCode + ": " + fileName);
                }

                return result;
            }
        }

        private static SqlInstanceInfo GetOrCreate(IDictionary<string, SqlInstanceInfo> instances, string instanceName)
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceName = "MSSQLSERVER";
            }

            SqlInstanceInfo info;
            if (!instances.TryGetValue(instanceName, out info))
            {
                info = new SqlInstanceInfo { InstanceName = instanceName };
                instances.Add(instanceName, info);
            }

            return info;
        }

        private static string InstanceNameFromServiceName(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return null;
            }

            if (string.Equals(serviceName, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
            {
                return "MSSQLSERVER";
            }

            const string sqlPrefix = "MSSQL$";
            if (serviceName.StartsWith(sqlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return serviceName.Substring(sqlPrefix.Length);
            }

            return null;
        }

        private static string AppendSource(string existing, string source)
        {
            if (string.IsNullOrWhiteSpace(existing))
            {
                return source;
            }

            return existing.IndexOf(source, StringComparison.OrdinalIgnoreCase) >= 0
                ? existing
                : existing + ", " + source;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? second : first;
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }

    public sealed class SqlInstanceInfo
    {
        public string InstanceName { get; set; }
        public string InstanceId { get; set; }
        public string ServiceName { get; set; }
        public string WmiNamespace { get; set; }
        public string Source { get; set; }

        public override string ToString()
        {
            return "Instance=" + InstanceName +
                   ", InstanceId=" + (InstanceId ?? "") +
                   ", Service=" + (ServiceName ?? "") +
                   ", WMI=" + (WmiNamespace ?? "") +
                   ", Source=" + (Source ?? "");
        }
    }

    public sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; private set; }
        public string Output { get; private set; }
    }
}
