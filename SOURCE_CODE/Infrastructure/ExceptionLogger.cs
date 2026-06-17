using System;
using System.IO;
using HVAC_Pro_Desktop.Services.Logging;
using Serilog;

namespace ServoERP.Infrastructure
{
    /// <summary>Routes legacy exception logging calls into the Serilog rolling file logger.</summary>
    public static class ExceptionLogger
    {
        private static readonly string LogFolder = ResolveLogFolder();

        /// <summary>Returns the folder used for monthly exception logs.</summary>
        public static string LogFolderPath
        {
            get { return LogFolder; }
        }

        /// <summary>Writes an exception entry to the current monthly log file.</summary>
        public static void Log(Exception ex, string context = null)
        {
            if (ex == null)
                return;

            try
            {
                ServoLog.EnsureLogDirectory();
                Serilog.Log.Error(ex, "ServoERP exception. Context: {Context}", context ?? "General");
            }
            catch
            {
            }
        }

        /// <summary>Writes a text entry to the current monthly log file.</summary>
        public static void Log(string message, string context = null)
        {
            try
            {
                ServoLog.EnsureLogDirectory();
                Serilog.Log.Information("ServoERP log entry. Context: {Context}. Message: {Message}", context ?? "INFO", message);
            }
            catch
            {
            }
        }

        /// <summary>Returns the path to the current month's log file, or null if none exists.</summary>
        public static string CurrentLogPath()
        {
            string path = ServoLog.RollingLogPath;
            return File.Exists(path) ? path : null;
        }

        private static string ResolveLogFolder()
        {
            const string WorkspaceLogFolder = @"C:\HVAC_PRO_MSE\LOGS";
            try
            {
                ServoLog.EnsureLogDirectory();
                return WorkspaceLogFolder;
            }
            catch
            {
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGS");
        }
    }
}
