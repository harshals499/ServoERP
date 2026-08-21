using System;
using System.Reflection;
using System.Threading.Tasks;
using HVAC_Pro_Desktop.DAL;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>
    /// Runs idempotent local maintenance after a Velopack-installed version first starts.
    /// It deliberately contains no business-data migration: schema guards and node registration
    /// are safe to repeat, and a failed office SQL connection is retried at the next launch.
    /// </summary>
    public static class PostUpdateMaintenanceService
    {
        private const string CompletedVersionKey = "PostUpdateMaintenanceVersion";
        private const string CompletedUtcKey = "PostUpdateMaintenanceUtc";
        private static readonly object Sync = new object();
        private static bool _running;

        public static void StartIfRequired()
        {
            string version = GetCurrentVersion();
            if (string.Equals(ConfigService.Get("App", CompletedVersionKey, string.Empty), version, StringComparison.OrdinalIgnoreCase))
                return;

            lock (Sync)
            {
                if (_running)
                    return;
                _running = true;
            }

            Task.Run(() => Run(version));
        }

        private static void Run(string version)
        {
            try
            {
                AppLogger.LogInfo("Post-update maintenance started for v" + version + ".");
                var manager = new DatabaseManager();
                manager.InitializeDatabase();
                DbHelper.EnsureQuotationSchemaMigration();
                DbHelper.EnsureAMCSchema();
                NodeIdentityService.EnsureRegistered();

                ConfigService.Set("App", CompletedVersionKey, version);
                ConfigService.Set("App", CompletedUtcKey, DateTime.UtcNow.ToString("o"));
                AppLogger.LogInfo("Post-update maintenance completed for v" + version + ".");
            }
            catch (Exception ex)
            {
                // Do not mark this version complete: a client that was offline at first launch
                // will retry the same safe maintenance sequence when it next opens ServoERP.
                AppLogger.LogError("PostUpdateMaintenanceService.Run", ex);
            }
            finally
            {
                lock (Sync)
                    _running = false;
            }
        }

        private static string GetCurrentVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "0.0.0.0" : version.ToString();
        }
    }
}
