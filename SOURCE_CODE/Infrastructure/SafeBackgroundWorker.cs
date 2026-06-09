using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Drop-in BackgroundWorker that logs errors, marshals completion, and ignores double starts.</summary>
    public class SafeBackgroundWorker : BackgroundWorker
    {
        private readonly Control _owner;

        /// <summary>Creates a safe worker bound to an owning form or user control.</summary>
        public SafeBackgroundWorker(Control owner)
        {
            _owner = owner;
            WorkerReportsProgress = true;
            WorkerSupportsCancellation = true;
        }

        /// <summary>Starts the worker only if it is not already running.</summary>
        public void StartSafe(object argument = null)
        {
            if (IsBusy)
                return;

            if (argument != null)
                RunWorkerAsync(argument);
            else
                RunWorkerAsync();
        }

        /// <summary>Logs worker errors and raises completion safely on the owner UI thread.</summary>
        protected override void OnRunWorkerCompleted(RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                ExceptionLogger.Log(e.Error, "BackgroundWorker in " + (_owner == null ? "unknown" : _owner.GetType().Name));

            UIThread.Run(_owner, () => base.OnRunWorkerCompleted(e));
        }

        /// <summary>Raises progress changes safely on the owner UI thread.</summary>
        protected override void OnProgressChanged(ProgressChangedEventArgs e)
        {
            UIThread.Run(_owner, () => base.OnProgressChanged(e));
        }
    }
}
