using System;
using System.Threading;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Registers global WinForms and application-domain exception handlers.</summary>
    public static class AppExceptionHandler
    {
        private static bool _registered;

        /// <summary>Installs global exception hooks once for this process.</summary>
        public static void Register()
        {
            if (_registered)
                return;

            _registered = true;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (sender, e) =>
            {
                ExceptionLogger.Log(e.Exception, "Application.ThreadException");
                ShowGlobalError(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ExceptionLogger.Log(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                ExceptionLogger.Log(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        /// <summary>Shows the global friendly error dialog, falling back to MessageBox if form creation fails.</summary>
        private static void ShowGlobalError(Exception ex)
        {
            const string userMessage =
                "ServoERP encountered an unexpected error and needs your attention.\n" +
                "Your data has not been lost. The error has been logged.";

            try
            {
                using (var dlg = new ServoErrorDialog(userMessage, ex))
                    dlg.ShowDialog();
            }
            catch
            {
                MessageBox.Show(
                    userMessage + "\n\n" + (ex == null ? string.Empty : ex.Message),
                    "ServoERP Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
