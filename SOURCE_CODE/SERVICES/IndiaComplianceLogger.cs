using System;
using System.IO;

namespace HVAC_Pro_Desktop.Services
{
    public static class IndiaComplianceLogger
    {
        private static readonly object Sync = new object();
        private const string LogDirectory = @"C:\HVAC_PRO_MSE\LOGS";

        public static void Log(string context, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                string path = Path.Combine(LogDirectory, "compliance-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    + " | " + context
                    + " | " + message
                    + Environment.NewLine;

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
}
