using System;
using System.Collections.Generic;
using System.Linq;

namespace HVACPro.SqlSetup
{
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitError = 1;
        private const int ExitUserCancelled = 2;

        private static int Main(string[] args)
        {
            bool auto = args.Any(a => string.Equals(a, "/auto", StringComparison.OrdinalIgnoreCase));
            string installerPath = GetOption(args, "/installer=");

            var manager = new SQLServerSetupManager(auto, installerPath);

            try
            {
                manager.Log("HVAC PRO SQL setup utility started. AutoRemove=" + auto + ".");

                if (!SQLServerSetupManager.IsAdministrator())
                {
                    manager.LogWarning("Administrator privileges are required.");
                    Console.Error.WriteLine("Please run this utility as Administrator.");
                    return ExitError;
                }

                IList<SqlInstanceInfo> instances = manager.DetectInstances();

                bool removalAllowed = manager.RemoveConflictingInstances(instances);
                if (!removalAllowed)
                {
                    manager.Log("SQL setup utility cancelled by user.");
                    return ExitUserCancelled;
                }

                instances = manager.DetectInstances();
                manager.InstallSQLExpress2019IfMissing(instances);
                manager.EnableProtocols();
                manager.SetServicesToAutomatic();

                if (!manager.VerifyConnection())
                {
                    manager.LogWarning("Unable to verify SQL Server connection to .\\SQLEXPRESS.");
                    return ExitError;
                }

                manager.Log("HVAC PRO SQL setup utility completed successfully.");
                return ExitSuccess;
            }
            catch (Exception ex)
            {
                manager.LogException("Fatal SQL setup error.", ex);
                Console.Error.WriteLine("SQL Server setup failed: " + ex.Message);
                return ExitError;
            }
        }

        private static string GetOption(string[] args, string prefix)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(prefix.Length).Trim('"');
                }
            }

            return null;
        }
    }
}
