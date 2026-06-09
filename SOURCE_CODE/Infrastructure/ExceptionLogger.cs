using System;
using System.IO;
using Serilog;

namespace ServoERP.Infrastructure
{
    /// <summary>Routes legacy exception logging calls into the Serilog rolling file logger.</summary>
    public static class ExceptionLogger
    {
        private static readonly string LogFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

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
                Directory.CreateDirectory(LogFolder);
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
                Directory.CreateDirectory(LogFolder);
                Serilog.Log.Information("ServoERP log entry. Context: {Context}. Message: {Message}", context ?? "INFO", message);
            }
            catch
            {
            }
        }

        /// <summary>Returns the path to the current month's log file, or null if none exists.</summary>
        public static string CurrentLogPath()
        {
            string path = Path.Combine(LogFolder, "servoerp_" + DateTime.Now.ToString("yyyy-MM") + ".log");
            if (!File.Exists(path))
                path = Path.Combine(LogFolder, "servoerp_" + DateTime.Now.ToString("yyyyMM") + ".log");
            return File.Exists(path) ? path : null;
        }
    }
}
