using System;
using System.IO;

namespace HVAC_Pro_Desktop.Services
{
    public static class Logger
    {
        private static readonly object Sync = new object();
        private static readonly string LogPath = Path.Combine(@"C:\HVAC_PRO_MSE", "LOGS", "servoerp_errors.log");

        public static string CurrentLogPath
        {
            get { return LogPath; }
        }

        public static void Log(string source, Exception ex)
        {
            if (ex == null)
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                string entry =
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                    "[" + Safe(source) + "] " +
                    ex.GetType().Name + ": " + SensitiveDataRedactor.Redact(ex.Message) + Environment.NewLine +
                    "StackTrace: " + SensitiveDataRedactor.Redact(ex.StackTrace ?? "none") + Environment.NewLine +
                    "Inner: " + (ex.InnerException == null ? "none" : SensitiveDataRedactor.Redact(ex.InnerException.Message)) + Environment.NewLine +
                    Environment.NewLine;

                lock (Sync)
                    File.AppendAllText(LogPath, entry);
            }
            catch
            {
                // Logging must never become a second crash.
            }

            try { AppLogger.LogInfo(Safe(source) + " | " + ex.GetType().Name + ": " + SensitiveDataRedactor.Redact(ex.Message)); } catch { }
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(unknown)" : value.Trim();
        }
    }
}
