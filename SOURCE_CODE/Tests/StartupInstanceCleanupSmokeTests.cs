using System;

namespace HVAC_Pro_Desktop.Tests
{
    public static class StartupInstanceCleanupSmokeTests
    {
        public static string RunAll()
        {
            int currentPid = 500;
            string currentPath = @"C:\HVAC_PRO_MSE\SOURCE_CODE\bin\Debug\HVAC_Pro_Desktop.exe";

            if (Program.ShouldTerminateExistingAppInstance(currentPid, "HVAC_Pro_Desktop", currentPath, currentPid, currentPath))
                throw new InvalidOperationException("Startup cleanup must never terminate the current process.");

            if (!Program.ShouldTerminateExistingAppInstance(501, "HVAC_Pro_Desktop", currentPath, currentPid, currentPath))
                throw new InvalidOperationException("Startup cleanup should terminate another HVAC_Pro_Desktop process.");

            if (!Program.ShouldTerminateExistingAppInstance(502, "ServoERP", @"C:\HVAC_PRO_MSE\APP\ServoERP.exe", currentPid, currentPath))
                throw new InvalidOperationException("Startup cleanup should terminate another ServoERP process.");

            if (Program.ShouldTerminateExistingAppInstance(503, "chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", currentPid, currentPath))
                throw new InvalidOperationException("Startup cleanup must not terminate unrelated processes.");

            return "startup cleanup targets stale ServoERP instances without touching the current process";
        }
    }
}
