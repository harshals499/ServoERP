using System;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Base class for ServoERP page UserControls with safe workers, rendering helpers, and refresh contract.</summary>
    public class ServoPageBase : UserControl, IRefreshable
    {
        private DateTime _loadStart;

        /// <summary>Creates a low-flicker page surface before child controls are added.</summary>
        protected ServoPageBase()
        {
            RenderHelper.EnableDoubleBuffer(this);
        }

        /// <summary>Enables child double buffering and logs page load timing.</summary>
        protected override void OnLoad(EventArgs e)
        {
            _loadStart = DateTime.Now;
            RenderHelper.EnableDoubleBufferAll(this);
            RenderHelper.OptimiseAllGrids(this);
            base.OnLoad(e);

            double elapsed = (DateTime.Now - _loadStart).TotalMilliseconds;
            ExceptionLogger.Log("Page loaded: " + GetType().Name + " in " + elapsed.ToString("F0") + "ms", "Navigation");
        }

        /// <summary>Reloads page data when navigation returns to the page.</summary>
        public virtual void RefreshData()
        {
        }

        /// <summary>Creates a SafeBackgroundWorker already bound to this page.</summary>
        protected SafeBackgroundWorker CreateWorker()
        {
            return new SafeBackgroundWorker(this);
        }

        /// <summary>Runs an action safely on the UI thread.</summary>
        protected void RunOnUI(Action action)
        {
            UIThread.Run(this, action);
        }

        /// <summary>Runs a low-flicker rebuild on a panel or control.</summary>
        protected void BatchUpdate(Control panel, Action action)
        {
            RenderHelper.BatchUpdate(panel, action);
        }

        /// <summary>Shows the branded ServoERP error dialog and logs the exception.</summary>
        protected void ShowError(string message, Exception ex = null)
        {
            ServoErrorDialog.Show(this, message, ex);
        }

        /// <summary>Applies low-flicker rendering and performance settings to a grid.</summary>
        protected void OptimiseGrid(DataGridView grid)
        {
            RenderHelper.OptimiseGrid(grid);
        }
    }
}
