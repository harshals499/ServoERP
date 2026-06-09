using System;
using System.IO;
using System.Windows.Forms;
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
            return "UI error handling safety net logs, restores buttons, and shows inline load errors";
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
    }
}
