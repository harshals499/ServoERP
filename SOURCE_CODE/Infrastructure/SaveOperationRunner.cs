using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Runs a standard ServoERP save operation while preserving button state.</summary>
    public static class SaveOperationRunner
    {
        public static async Task RunAsync(
            Button primaryButton,
            string busyText,
            string readyText,
            Func<Task> saveAction,
            Action<Exception> handleException,
            params Control[] relatedControls)
        {
            if (primaryButton == null)
                throw new ArgumentNullException(nameof(primaryButton));
            if (saveAction == null)
                throw new ArgumentNullException(nameof(saveAction));

            string originalText = string.IsNullOrWhiteSpace(readyText) ? primaryButton.Text : readyText;
            SetEnabled(primaryButton, relatedControls, false);
            if (!primaryButton.IsDisposed)
                primaryButton.Text = string.IsNullOrWhiteSpace(busyText) ? "Saving..." : busyText;

            try
            {
                await saveAction().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                if (handleException != null)
                    handleException(ex);
                else
                    throw;
            }
            finally
            {
                if (!primaryButton.IsDisposed)
                {
                    primaryButton.Text = originalText;
                    SetEnabled(primaryButton, relatedControls, true);
                }
            }
        }

        private static void SetEnabled(Button primaryButton, IEnumerable<Control> relatedControls, bool enabled)
        {
            if (primaryButton != null && !primaryButton.IsDisposed)
                primaryButton.Enabled = enabled;

            if (relatedControls == null)
                return;

            foreach (Control control in relatedControls)
            {
                if (control != null && !control.IsDisposed)
                    control.Enabled = enabled;
            }
        }
    }
}
