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
using HVAC_Pro_Desktop.UI;
using HVAC_Pro_Desktop.UI.Licensing;

namespace HVAC_Pro_Desktop
{
    internal static class AppRuntime
    {
        private static readonly object Sync = new object();
        private static string _lastLogPath;

        public static string LastLogPath => _lastLogPath;

        public static void LogException(string context, Exception ex)
        {
            try
            {
                string dir = @"C:\HVAC_PRO_MSE\LOGS";
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, "crash-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                var sb = new StringBuilder();
                sb.AppendLine("==================================================");
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("Context: " + context);
                sb.AppendLine(ex.ToString());
                sb.AppendLine();

                lock (Sync)
                {
                    File.AppendAllText(path, sb.ToString());
                    _lastLogPath = path;
                }
            }
            catch
            {
            }
        }

        public static void ShowRecoverableError(string title, string context, Exception ex)
        {
            LogException(context, ex);

            string logHint = string.IsNullOrWhiteSpace(_lastLogPath)
                ? ""
                : "\r\n\r\nCrash log: " + _lastLogPath;

            MessageBox.Show(
                context + " failed.\r\n\r\n" + ex.Message + logHint,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
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

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            SetProcessDPIAware();

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

        private static void RunApplication()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string[] args = Environment.GetCommandLineArgs();
                if (args.Skip(1).Any(arg => string.Equals(arg, "/firstrun", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        AppStartupService.InitialiseDatabaseOnly();
                    }
                    catch (Exception ex)
                    {
                        AppRuntime.LogException("FIRSTRUN-ERR", ex);
                    }
                    return;
                }

                Stopwatch startupWatch = Stopwatch.StartNew();
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

                Task.Run(() => new BackupService().CreateStartupBackupIfDue());

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
                AppRuntime.LogException("Application startup", ex);

                MessageBox.Show(
                    "Application startup error:\r\n\r\n" + ex.Message +
                    "\r\n\r\nWhat the app already tried:\r\n" +
                    "  1. Start SQL Server (SQLEXPRESS)\r\n" +
                    "  2. Start SQL Server Browser\r\n" +
                    "  3. Wait for SQL Express to accept connections\r\n" +
                    "  4. Retry database initialization once" +
                    (string.IsNullOrWhiteSpace(AppRuntime.LastLogPath) ? "" : "\r\n\r\nCrash log: " + AppRuntime.LastLogPath),
                    BrandingService.WindowTitle("Startup Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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
            AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Screen Error"), "A screen action", e.Exception);
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
