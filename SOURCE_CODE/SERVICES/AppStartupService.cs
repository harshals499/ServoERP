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
            DatabaseManager.PrepareSqlServer();

            var manager = new DatabaseManager();
            manager.InitializeDatabase();

            LogInstall("First-run database initialization completed.");
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
