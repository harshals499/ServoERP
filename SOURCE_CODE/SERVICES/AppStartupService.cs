using System;
using System.IO;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    public static class AppStartupService
    {
        private const string InstallLogPath = @"C:\HVAC_PRO_MSE\LOGS\install.log";

        public static void InitialiseDatabaseOnly()
        {
            LogInstall("First-run database initialization started.");
            LocalSqliteFallbackStore.EnsureReady();

            try
            {
                DatabaseManager.PrepareSqlServer();

                var manager = new DatabaseManager();
                manager.InitializeDatabase();
                LocalSqliteFallbackStore.RecordSqlAvailable(DatabaseManager.RequireConfiguredConnectionString());

                LogInstall("First-run database initialization completed.");
            }
            catch (Exception ex)
            {
                LocalSqliteFallbackStore.RecordSqlUnavailable(DatabaseManager.GetConfiguredConnectionString(), ex);
                throw;
            }
        }

        public static void RunInteractiveServerSetup()
        {
            LogInstall("Interactive server setup started.");
            LocalSqliteFallbackStore.EnsureReady();

            try
            {
                DatabaseManager.PrepareSqlServer();

                var manager = new DatabaseManager();
                manager.InitializeDatabase();
                LocalSqliteFallbackStore.RecordSqlAvailable(DatabaseManager.RequireConfiguredConnectionString());
                new BackupService().EnsureBackupInfrastructure();

                ConfigService.Set("Database", "ServerRole", "AlwaysOnOfficeServer");
                ConfigService.Set("Fallback", "Mode", "LocalSQLiteDiagnostics");
                ConfigService.Set("Fallback", "SqlitePath", LocalSqliteFallbackStore.GetDatabasePath());
                ConfigService.Set("Fallback", "AllowBusinessWrites", "false");
                ConfigService.Set("Setup", "ServerFirstRunComplete", "true");
                ConfigService.Set("Setup", "ServerFirstRunCompletedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                DatabaseConnectionStateService.CheckNow("AppStartupService.RunInteractiveServerSetup", true);
                LogInstall("Interactive server setup completed.");
            }
            catch (Exception ex)
            {
                LocalSqliteFallbackStore.RecordSqlUnavailable(DatabaseManager.GetConfiguredConnectionString(), ex);
                LogInstall("Interactive server setup failed: " + ex.Message);
                throw;
            }
        }

        public static void LogInstall(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(InstallLogPath) ?? @"C:\HVAC_PRO_MSE\LOGS");
                File.AppendAllText(
                    InstallLogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
