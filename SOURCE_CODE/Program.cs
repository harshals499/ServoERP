using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Services.Licensing;
using HVAC_Pro_Desktop.Tests;
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.UI.Licensing;
using QuestPDF.Infrastructure;
using Serilog;
using Velopack;

namespace HVAC_Pro_Desktop
{
    internal static class AppRuntime
    {
        private static readonly object Sync = new object();
        private static string _lastLogPath;

        public static string LastLogPath => _lastLogPath;

        public static void LogException(string context, Exception ex)
        {
            CrashProtectionService.LogException("(application)", context, ex, false);
            _lastLogPath = CrashProtectionService.LastCrashLogPath;
        }

        public static void ShowRecoverableError(string title, string context, Exception ex)
        {
            CrashProtectionService.ShowFriendlyError(null, "(application)", context, ex);
            _lastLogPath = CrashProtectionService.LastCrashLogPath;
        }

        public static void LogTiming(string context, long elapsedMs, string details = null)
        {
            try
            {
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, "perf-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                              " | " + context +
                              " | " + elapsedMs + " ms" +
                              (string.IsNullOrWhiteSpace(details) ? "" : " | " + details) +
                              Environment.NewLine;

                lock (Sync)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
            }
        }

        public static void LogConnection(string message)
        {
            try
            {
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, "connection-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine;

                lock (Sync)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
            }
        }
    }

    static class Program
    {
        private const string SingleInstanceMutexName = "Global\\HVAC_PRO_MSE_ServoERP_SingleInstance";
        private static readonly string[] AppProcessNames = { "HVAC_Pro_Desktop", "ServoERP" };

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            ServoERP.Infrastructure.AppExceptionHandler.Register();
            VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
            SetProcessDPIAware();
            ShutdownExistingAppInstances();

            using (var singleInstance = new Mutex(true, SingleInstanceMutexName, out bool isFirstInstance))
            {
                if (!isFirstInstance)
                {
                    MessageBox.Show(
                        "HVAC Pro is already running. Use the open window instead of starting another copy.",
                        BrandingService.WindowTitle("Already Running"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                RunApplication();
                GC.KeepAlive(singleInstance);
            }
        }

        private static void ShutdownExistingAppInstances()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string currentPath = GetCurrentExecutablePath();
                foreach (string processName in AppProcessNames)
                {
                    foreach (Process process in Process.GetProcessesByName(processName))
                        ShutdownExistingAppInstance(process, currentPid, currentPath);
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("Startup instance cleanup", ex);
            }
        }

        private static void ShutdownExistingAppInstance(Process process, int currentPid, string currentPath)
        {
            using (process)
            {
                try
                {
                    string processPath = TryGetProcessPath(process);
                    if (!ShouldTerminateExistingAppInstance(process.Id, process.ProcessName, processPath, currentPid, currentPath))
                        return;

                    try
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            process.CloseMainWindow();
                            if (process.WaitForExit(2500))
                                return;
                        }
                    }
                    catch
                    {
                    }

                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(2500);
                    }
                }
                catch (Exception ex)
                {
                    AppRuntime.LogException("Startup instance cleanup process", ex);
                }
            }
        }

        internal static bool ShouldTerminateExistingAppInstance(int processId, string processName, string processPath, int currentProcessId, string currentProcessPath)
        {
            if (processId == currentProcessId)
                return false;

            bool knownName = AppProcessNames.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
            if (!knownName)
                return false;

            if (string.IsNullOrWhiteSpace(processPath) || string.IsNullOrWhiteSpace(currentProcessPath))
                return true;

            return !string.Equals(
                Path.GetFullPath(processPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(currentProcessPath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)
                || knownName;
        }

        private static string GetCurrentExecutablePath()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule.FileName;
            }
            catch
            {
                return Application.ExecutablePath;
            }
        }

        private static string TryGetProcessPath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void RunApplication()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            CrashProtectionService.InstallApplicationHooks();
            QuestPDF.Settings.License = LicenseType.Community;
            ConfigureSerilog();
            Log.Information("ServoERP startup requested. Version {Version}", Application.ProductVersion);

            string[] args = Environment.GetCommandLineArgs();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                LayoutAuditService.AttachGlobalFormAuditor();
                InputOutlineService.InstallGlobalApplicationWatcher();

                if (args.Skip(1).Any(arg => string.Equals(arg, "/firstrun", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        AppStartupService.InitialiseDatabaseOnly();
                        Environment.ExitCode = 0;
                    }
                    catch (Exception ex)
                    {
                        AppRuntime.LogException("FIRSTRUN-ERR", ex);
                        Environment.ExitCode = 1;
                    }
                    return;
                }

                if (args.Skip(1).Any(arg => string.Equals(arg, "/serversetup", StringComparison.OrdinalIgnoreCase)))
                {
                    Application.Run(new ServerFirstRunSetupForm());
                    return;
                }

                Stopwatch startupWatch = Stopwatch.StartNew();
                LocalSqliteFallbackStore.EnsureReady();
                var dbManager = new DatabaseManager();
                Stopwatch stageWatch = Stopwatch.StartNew();
                if (dbManager.IsNormalStartupReady())
                {
                    AppRuntime.LogTiming("Startup.InitializeDatabase", stageWatch.ElapsedMilliseconds, "skipped; database ready");
                }
                else
                {
                    dbManager.InitializeDatabase();
                    AppRuntime.LogTiming("Startup.InitializeDatabase", stageWatch.ElapsedMilliseconds);
                }
                DbHelper.EnsureQuotationSchemaMigration();
                DbHelper.EnsureAMCSchema();
                LocalSqliteFallbackStore.RecordSqlAvailable(DatabaseManager.RequireConfiguredConnectionString());

                stageWatch.Restart();
                if (!DatabaseManager.IsDemoDataEnabled())
                {
                    AppRuntime.LogTiming(
                        "Startup.InsertSampleData",
                        stageWatch.ElapsedMilliseconds,
                        "skipped; tenant demo data disabled for " + DatabaseManager.GetCurrentTenantCode());
                }
                else if (dbManager.IsSampleDataReady())
                {
                    AppRuntime.LogTiming("Startup.InsertSampleData", stageWatch.ElapsedMilliseconds, "skipped; data already ready");
                }
                else
                {
                    dbManager.InsertSampleData();
                    AppRuntime.LogTiming("Startup.InsertSampleData", stageWatch.ElapsedMilliseconds, "seed/import completed");
                }

                stageWatch.Restart();
                DbSettings.EnsureUserSettingsTable();
                LanguageManager.SetLanguage(DbSettings.Get("Language", LanguageManager.English), false);
                new BackupService().EnsureBackupInfrastructure();
                AppRuntime.LogTiming("Startup.Language", stageWatch.ElapsedMilliseconds, LanguageManager.CurrentLanguage);

                if (args.Skip(1).Any(arg => string.Equals(arg, "/smoketest", StringComparison.OrdinalIgnoreCase)))
                {
                    string reportPath = EnterpriseUiSmokeTests.WriteReport();
                    if (File.Exists(reportPath) && File.ReadAllText(reportPath).Contains(Environment.NewLine + "FAIL "))
                        Environment.ExitCode = 1;
                    else
                        Environment.ExitCode = 0;
                    AppRuntime.LogTiming("EnterpriseUiSmokeTests", 0, reportPath);
                    return;
                }

                if (args.Skip(1).Any(arg => string.Equals(arg, "/cardmenuaudit", StringComparison.OrdinalIgnoreCase)))
                {
                    string reportPath = GlobalCardContextMenuFormAuditTests.WriteReport();
                    string reportText = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
                    Environment.ExitCode = reportText.Contains("Failed: 0") ? 0 : 1;
                    AppRuntime.LogTiming("GlobalCardContextMenuFormAuditTests", 0, reportPath);
                    return;
                }

                if (args.Skip(1).Any(arg => string.Equals(arg, "/amctest", StringComparison.OrdinalIgnoreCase)))
                {
                    string dir = System.IO.Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
                    System.IO.Directory.CreateDirectory(dir);
                    string reportPath = System.IO.Path.Combine(dir, "amc-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                    var lines2 = new System.Collections.Generic.List<string>();
                    lines2.Add("AddAMC Smoke Tests");
                    lines2.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    lines2.Add("");
                    try
                    {
                        foreach (string result in AddAMCSmokeTests.RunAll())
                            lines2.Add(result);
                    }
                    catch (Exception amcEx)
                    {
                        lines2.Add("FAIL " + amcEx);
                    }
                    System.IO.File.WriteAllLines(reportPath, lines2);
                    bool anyFail = lines2.Any(l => l.StartsWith("FAIL "));
                    Environment.ExitCode = anyFail ? 1 : 0;
                    return;
                }

                if (ShouldShowServerFirstRunSetup())
                {
                    using (var setup = new ServerFirstRunSetupForm())
                    {
                        if (setup.ShowDialog() != DialogResult.OK)
                            return;
                    }
                }

                stageWatch.Restart();
                WebView2RuntimeHelper.ShowFriendlyMissingRuntimeMessage(null);
                AppRuntime.LogTiming("Startup.WebView2Check", stageWatch.ElapsedMilliseconds);

                stageWatch.Restart();
                LicenseValidationResult licenseResult = new LicenseService().ValidateCurrentLicense();
                AppRuntime.LogTiming("Startup.LicenseValidation", stageWatch.ElapsedMilliseconds, licenseResult.Message);
                if (licenseResult.RequiresActivation)
                {
                    using (var activation = new LicenseActivationForm())
                    {
                        if (activation.ShowDialog() != DialogResult.OK)
                            return;
                    }

                    licenseResult = new LicenseService().ValidateCurrentLicense();
                }
                licenseResult = ShowLicenseStartupWarning(licenseResult);

                stageWatch.Restart();
                if (!LegalAgreementForm.EnsureAccepted(null))
                    return;
                AppRuntime.LogTiming("Startup.LegalAgreement", stageWatch.ElapsedMilliseconds);

                if (args.Skip(1).Any(arg => string.Equals(arg, "/amcformtiming", StringComparison.OrdinalIgnoreCase)))
                {
                    stageWatch.Restart();
                    string bypassMessage2;
                    if (!LocalLoginBypassService.TryStartSession(out bypassMessage2))
                        throw new InvalidOperationException("AddAMCForm timing requires the authorized local login bypass. " + bypassMessage2);

                    string formTimingPath = AddAMCFormTimingTest.WriteReport();
                    if (File.Exists(formTimingPath) && File.ReadAllText(formTimingPath).Contains(Environment.NewLine + "FAIL "))
                        Environment.ExitCode = 1;
                    else
                        Environment.ExitCode = 0;
                    AppRuntime.LogTiming("AddAMCFormTimingTest", stageWatch.ElapsedMilliseconds, formTimingPath);
                    return;
                }

                if (args.Skip(1).Any(arg => string.Equals(arg, "/navtiming", StringComparison.OrdinalIgnoreCase)))
                {
                    stageWatch.Restart();
                    string bypassMessage;
                    if (!LocalLoginBypassService.TryStartSession(out bypassMessage))
                        throw new InvalidOperationException("Navigation timing requires the authorized local login bypass. " + bypassMessage);

                    string reportPath = FullNavigationTimingTests.WriteReport();
                    if (File.Exists(reportPath) && File.ReadAllText(reportPath).Contains(Environment.NewLine + "FAIL "))
                        Environment.ExitCode = 1;
                    else
                        Environment.ExitCode = 0;
                    AppRuntime.LogTiming("FullNavigationTimingTests", stageWatch.ElapsedMilliseconds, reportPath);
                    return;
                }

                stageWatch.Restart();
                if (LocalLoginBypassService.TryStartSession(out _))
                {
                    AppRuntime.LogTiming("Startup.Login", stageWatch.ElapsedMilliseconds, "local bypass");
                    AppRuntime.LogTiming("Startup.TotalBeforeMainForm", startupWatch.ElapsedMilliseconds);
                    Application.Run(new MainForm());
                }
                else
                {
                    AppRuntime.LogTiming("Startup.Login", stageWatch.ElapsedMilliseconds, "login form");
                    AppRuntime.LogTiming("Startup.TotalBeforeLoginForm", startupWatch.ElapsedMilliseconds);
                    Application.Run(new LoginForm());
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "ServoERP startup failed");
                AppRuntime.LogException("Application startup", ex);
                LocalSqliteFallbackStore.RecordSqlUnavailable(DatabaseManager.GetConfiguredConnectionString(), ex);

                if (args.Skip(1).Any(arg => string.Equals(arg, "/smoketest", StringComparison.OrdinalIgnoreCase)))
                {
                    WriteStartupSmokeFailure(ex);
                    Environment.ExitCode = 1;
                    return;
                }

                DialogResult retry = MessageBox.Show(
                    "Cannot connect to the office SQL Server or complete startup. Please ensure the always-on server PC and SQL Server are running, then click Retry.\r\n\r\nA local SQLite fallback status file has been updated for diagnostics only; business entries remain locked until SQL Server is reachable.",
                    BrandingService.WindowTitle("Startup Error"),
                    MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Error);
                if (retry == DialogResult.Retry)
                    RunApplication();
            }
            finally
            {
                Log.Information("ServoERP application loop ended");
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureSerilog()
        {
            try
            {
                string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logFolder);
                string logPath = Path.Combine(logFolder, "servoerp_.log");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Month,
                        retainedFileCountLimit: 12,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
            }
            catch
            {
                Log.Logger = new LoggerConfiguration().CreateLogger();
            }
        }

        private static void WriteStartupSmokeFailure(Exception ex)
        {
            try
            {
                string dir = @"C:\HVAC_PRO_MSE\TEST_RESULTS";
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "enterprise-ui-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(
                    path,
                    "ServoERP Enterprise UI Smoke Test" + Environment.NewLine +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    Environment.NewLine +
                    "FAIL Startup failed before UI smoke tests could run." + Environment.NewLine +
                    ex.GetType().FullName + ": " + ex.Message + Environment.NewLine +
                    Environment.NewLine +
                    DatabaseConnectionStateService.BuildSupportStatusText() + Environment.NewLine +
                    Environment.NewLine +
                    LocalSqliteFallbackStore.BuildStatusText());
                AppRuntime.LogTiming("EnterpriseUiSmokeTests.StartupFailure", 0, path);
            }
            catch
            {
            }
        }

        private static bool ShouldShowServerFirstRunSetup()
        {
            string required = ConfigService.Get("Setup", "ServerFirstRunRequired", "false");
            string complete = ConfigService.Get("Setup", "ServerFirstRunComplete", "false");
            return string.Equals(required, "true", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(complete, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static LicenseValidationResult ShowLicenseStartupWarning(LicenseValidationResult result)
        {
            if (result == null || result.Snapshot == null)
                return result;

            if (result.IsFrozen)
            {
                DialogResult openActivation = MessageBox.Show(
                    "Your ServoERP license has expired. Renew to continue business operations.\r\n\r\nOpen license activation now?",
                    BrandingService.WindowTitle("License Frozen"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (openActivation == DialogResult.Yes)
                {
                    using (var activation = new LicenseActivationForm())
                    {
                        if (activation.ShowDialog() == DialogResult.OK)
                            return new LicenseService().ValidateCurrentLicense();
                    }
                }

                return result;
            }

            int days = (int)Math.Ceiling((result.Snapshot.ExpiryDateUtc - DateTime.UtcNow).TotalDays);
            if (days == 30 || days == 14 || days == 7 || days == 3 || days == 1 || days <= 0)
            {
                string text = days <= 0
                    ? "Your ServoERP license expires today. Renew now to avoid Frozen Mode."
                    : "Your ServoERP license expires in " + days + " day(s).";
                MessageBox.Show(text, BrandingService.WindowTitle("License Renewal"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return result;
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Screen Error"), CrashProtectionService.LastUiAction, e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown fatal error");
            AppRuntime.LogException("AppDomain unhandled exception", ex);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppRuntime.LogException("Task scheduler", e.Exception);
            e.SetObserved();
        }
    }
}
