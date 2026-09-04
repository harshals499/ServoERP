using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.Helpers;
using Newtonsoft.Json;

namespace HVAC_Pro_Desktop.Tests
{
    public static class LanControlManagementSmokeTests
    {
        public static string RunAll()
        {
            EnsureReadinessStatesAreDeterministic();
            EnsureDeploymentScriptReportsAndVerifiesProgress();
            EnsureProgressReaderUsesLatestTerminalState();
            EnsureMachineSecretRoundTrip();
            return "LAN Control management smoke tests passed";
        }

        private static void EnsureMachineSecretRoundTrip()
        {
            const string secret = "ServoERP-LAN-test-secret";
            string protectedValue = SecureStorageHelper.ProtectMachineText(secret);
            if (string.IsNullOrWhiteSpace(protectedValue) || protectedValue == secret || !protectedValue.StartsWith("dpapi-machine:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQL configuration secrets must be protected with machine-level DPAPI.");
            if (SecureStorageHelper.UnprotectMachineText(protectedValue) != secret)
                throw new InvalidOperationException("Machine-level DPAPI configuration secret did not round-trip.");
        }

        private static void EnsureReadinessStatesAreDeterministic()
        {
            var passed = new List<OfficeLanReadinessCheck>
            {
                new OfficeLanReadinessCheck { Status = "Passed", IsBlocking = true }
            };
            if (OfficeLanReadinessService.EvaluateOverallStatus(passed, true) != "Ready")
                throw new InvalidOperationException("WinRM-ready terminals must evaluate as Ready.");
            if (OfficeLanReadinessService.EvaluateOverallStatus(passed, false) != "Needs preparation")
                throw new InvalidOperationException("Reachable terminals without WinRM must evaluate as Needs preparation.");

            passed.Add(new OfficeLanReadinessCheck { Status = "Failed", IsBlocking = true });
            if (OfficeLanReadinessService.EvaluateOverallStatus(passed, true) != "Blocked")
                throw new InvalidOperationException("Blocking failures must prevent deployment.");
        }

        private static void EnsureDeploymentScriptReportsAndVerifiesProgress()
        {
            MethodInfo builder = typeof(OfficeLanControlService).GetMethod("BuildDeploymentScript", BindingFlags.Static | BindingFlags.NonPublic);
            if (builder == null)
                throw new InvalidOperationException("LAN deployment script builder is missing.");
            string script = builder.Invoke(null, new object[] { "ServoERP.Terminal.Setup.1.1.440.0.exe", "enterprise-exe", @"SERVER-PC\SQLEXPRESS", "HVAC_PRO", Guid.NewGuid() }) as string;
            string[] required = { "deployment-progress.jsonl", "Write-DeploymentProgress", "Get-FileHash", "SHA256", "deployment-credentials.dat", "ProtectedData]::Unprotect", "WinRM enabled automatically", "New-PSSessionOption" };
            foreach (string marker in required)
                if (string.IsNullOrWhiteSpace(script) || script.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("LAN deployment script is missing: " + marker);
            if (script.IndexOf("Get-Credential", StringComparison.OrdinalIgnoreCase) >= 0 || script.IndexOf("Read-Host", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("LAN deployment must not block on an interactive PowerShell prompt.");
        }

        private static void EnsureProgressReaderUsesLatestTerminalState()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ServoERP-LanControl-Smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "deployment-progress.jsonl");
                var first = new OfficeLanDeploymentProgress { JobPublicId = Guid.NewGuid(), Computer = "PC-1", Stage = "Connecting", ProgressPercent = 5, Status = "Running", TimestampUtc = DateTime.UtcNow };
                var last = new OfficeLanDeploymentProgress { JobPublicId = first.JobPublicId, Computer = "PC-1", Stage = "Completed", ProgressPercent = 100, Status = "Completed", TimestampUtc = DateTime.UtcNow };
                File.WriteAllLines(path, new[] { JsonConvert.SerializeObject(first), JsonConvert.SerializeObject(last) });
                var package = new OfficeLanDeploymentPackage { JobPublicId = first.JobPublicId, ProgressPath = path };
                IList<OfficeLanDeploymentProgress> progress = new OfficeLanControlService().ReadDeploymentProgress(package);
                if (progress.Count != 1 || progress[0].ProgressPercent != 100 || progress[0].Status != "Completed")
                    throw new InvalidOperationException("LAN progress reader did not retain the latest state for a terminal.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
