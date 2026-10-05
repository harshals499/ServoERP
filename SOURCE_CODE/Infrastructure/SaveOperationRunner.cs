using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Runs a standard ServoERP save operation while preserving button state.</summary>
    public static class SaveOperationRunner
    {
        private static readonly HashSet<Button> Running = new HashSet<Button>();
        private static readonly object Sync = new object();

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

            lock (Sync)
            {
                if (!Running.Add(primaryButton)) return;
            }
            var controls = new[] { (Control)primaryButton }.Concat(relatedControls ?? new Control[0])
                .Where(control => control != null && !control.IsDisposed).Distinct().ToDictionary(control => control, control => control.Enabled);
            string originalText = string.IsNullOrWhiteSpace(readyText) ? primaryButton.Text : readyText;
            foreach (Control control in controls.Keys) control.Enabled = false;
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
                if (!primaryButton.IsDisposed) primaryButton.Text = originalText;
                foreach (var state in controls)
                    if (!state.Key.IsDisposed) state.Key.Enabled = state.Value;
                lock (Sync) Running.Remove(primaryButton);
            }
        }

    }
}
