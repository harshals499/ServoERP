using System;
using System.IO;

namespace HVAC_Pro_Desktop.Services
{
    public static class AppLogger
    {
        private static readonly object Sync = new object();
        private const string LogFilePath = @"C:\HVAC_PRO_MSE\LOGS\app.log";
        private const int MaxMessageLength = 1200;

        public static void LogInfo(string message)
        {
            try
            {
                string safeMessage = Truncate(SensitiveDataRedactor.Redact(message));
                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath) ?? @"C:\HVAC_PRO_MSE\LOGS");
                    File.AppendAllText(
                        LogFilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | INFO | " + safeMessage + Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        public static void LogError(string context, Exception ex)
        {
            AppRuntime.LogException(context, ex);
            LogInfo(context + " | " + (ex == null ? "Unknown error" : ex.Message));
        }

        private static string Truncate(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            message = message.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            if (message.Length <= MaxMessageLength)
                return message;

            return message.Substring(0, MaxMessageLength) + " ... [truncated " + (message.Length - MaxMessageLength) + " chars]";
        }
    }
}
