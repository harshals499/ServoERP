using System;
using System.IO;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.Tests
{
    public static class UiErrorHandlingSmokeTests
    {
        public static string RunAll()
        {
            EnsureLoggerWritesCentralFile();
            EnsureSafeExecuteRestoresButton();
            EnsureSafeLoadShowsInlineError();
            EnsureSqlAuthenticationFailuresAreRecognised();
            EnsureClientSetupVerifiesCredentialsBeforeSaving();
            return "UI error handling safety net and verified SQL startup/client recovery checks passed";
        }

        private static void EnsureLoggerWritesCentralFile()
        {
            Logger.Log("UiErrorHandlingSmokeTests", new InvalidOperationException("smoke-test-log"));
            if (!File.Exists(Logger.CurrentLogPath))
                throw new InvalidOperationException("Central error log file was not created.");
        }

        private static void EnsureSafeExecuteRestoresButton()
        {
            using (var button = new Button { Text = "Save", Enabled = true })
            {
                bool result = CrashProtectionService.SafeExecute(button, "Smoke action", button, "Saving...", () =>
                {
                    if (button.Text != "Saving..." || button.Enabled)
                        throw new InvalidOperationException("SafeExecute did not set processing state before running.");
                });

                if (!result)
                    throw new InvalidOperationException("SafeExecute should return true when the action succeeds.");
                if (!button.Enabled || button.Text != "Save")
                    throw new InvalidOperationException("SafeExecute did not restore button state.");
            }
        }

        private static void EnsureSafeLoadShowsInlineError()
        {
            using (var panel = new Panel())
            {
                bool result = CrashProtectionService.SafeLoad(panel, "SmokePage", () =>
                {
                    throw new InvalidOperationException("load failed");
                });

                if (result)
                    throw new InvalidOperationException("SafeLoad should return false when loading fails.");
                if (panel.Controls.Count == 0)
                    throw new InvalidOperationException("SafeLoad did not create an inline error state.");
            }
        }

        private static void EnsureSqlAuthenticationFailuresAreRecognised()
        {
            var wrapped = new InvalidOperationException(
                "Database startup failed.",
                new InvalidOperationException("Login failed for user 'servoerp_app'."));
            if (!Program.IsSqlAuthenticationFailure(wrapped))
                throw new InvalidOperationException("SQL authentication startup failures are not routed to connection recovery.");

            if (Program.IsSqlAuthenticationFailure(new InvalidOperationException("The office server is offline.")))
                throw new InvalidOperationException("A network outage was incorrectly classified as a SQL authentication failure.");
        }

        private static void EnsureClientSetupVerifiesCredentialsBeforeSaving()
        {
            var profile = new ServerSetupProfile
            {
                ConnectionTarget = @"OFFICE-SERVER\SQLEXPRESS",
                DatabaseName = "HVAC_PRO",
                FallbackSqlitePath = @"C:\HVAC_PRO_MSE\DATABASE\ServoERP_Fallback.sqlite"
            };
            string config = SupportCenterService.BuildClientConfigXml(profile);
            string script = SupportCenterService.BuildClientConnectionScript(profile);

            if (!config.Contains("<UseWindowsAuth>false</UseWindowsAuth>") ||
                !config.Contains("<ServerRole>ClientPC</ServerRole>"))
                throw new InvalidOperationException("The client setup template does not use the safe terminal defaults.");
            if (!script.Contains("$test.Open()") ||
                !script.Contains("DataProtectionScope]::LocalMachine") ||
                !script.Contains("pre-client-connection.bak") ||
                !script.Contains("ServerRole = 'ClientPC'"))
                throw new InvalidOperationException("The client setup script can save an unverified or unprotected SQL configuration.");
        }
    }
}
