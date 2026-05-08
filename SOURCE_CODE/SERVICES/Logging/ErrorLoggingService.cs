using System;

namespace HVAC_Pro_Desktop.Services.Logging
{
    public sealed class ErrorLoggingService
    {
        public void Log(string context, Exception ex)
        {
            AppLogger.LogError(context, ex);
            AppRuntime.LogException(context, ex);
        }
    }
}
