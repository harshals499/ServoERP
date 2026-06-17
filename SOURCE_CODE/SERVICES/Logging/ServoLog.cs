using System;
using System.IO;
using Serilog;

namespace HVAC_Pro_Desktop.Services.Logging
{
    /// <summary>Provides one shared logging spine for Serilog and fallback diagnostics.</summary>
    public static class ServoLog
    {
        private const string WorkspaceLogDirectory = @"C:\HVAC_PRO_MSE\LOGS";
        private static readonly object Sync = new object();

        public static string LogDirectory
        {
            get { return WorkspaceLogDirectory; }
        }

        public static string RollingLogPath
        {
            get { return Path.Combine(LogDirectory, "servoerp_" + DateTime.Now.ToString("yyyy-MM") + ".log"); }
        }

        public static void Information(string messageTemplate, params object[] args)
        {
            try
            {
                EnsureLogDirectory();
                Log.Information(messageTemplate, args);
            }
            catch
            {
                AppendFallback("servoerp-fallback.log", "INFO", SafeTemplate(messageTemplate));
            }
        }

        public static void Warning(string messageTemplate, params object[] args)
        {
            try
            {
                EnsureLogDirectory();
                Log.Warning(messageTemplate, args);
            }
            catch
            {
                AppendFallback("servoerp-fallback.log", "WARN", SafeTemplate(messageTemplate));
            }
        }

        public static void Error(Exception ex, string messageTemplate, params object[] args)
        {
            try
            {
                EnsureLogDirectory();
                Log.Error(ex, messageTemplate, args);
            }
            catch
            {
                string message = SafeTemplate(messageTemplate);
                if (ex != null)
                    message += " | " + ex.GetType().Name + ": " + SensitiveDataRedactor.Redact(ex.Message);

                AppendFallback("servoerp-fallback.log", "ERROR", message);
            }
        }

        public static void WriteDiagnosticLine(string fileName, string message)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Diagnostic log file name is required.", nameof(fileName));

            try
            {
                EnsureLogDirectory();
                string path = Path.Combine(LogDirectory, fileName);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + SensitiveDataRedactor.Redact(message ?? string.Empty) + Environment.NewLine;
                lock (Sync)
                    File.AppendAllText(path, line);
            }
            catch
            {
            }
        }

        public static void EnsureLogDirectory()
        {
            Directory.CreateDirectory(LogDirectory);
        }

        private static string SafeTemplate(string messageTemplate)
        {
            return SensitiveDataRedactor.Redact(string.IsNullOrWhiteSpace(messageTemplate) ? "(no message)" : messageTemplate);
        }

        private static void AppendFallback(string fileName, string level, string message)
        {
            try
            {
                EnsureLogDirectory();
                string path = Path.Combine(LogDirectory, fileName);
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + level + " | " + message + Environment.NewLine;
                lock (Sync)
                    File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
}
