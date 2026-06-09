using System;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI;

namespace ServoERP.Infrastructure
{
    /// <summary>Base class for ServoERP modal and sub forms with safe workers, rendering helpers, and diagnostics.</summary>
    public class ServoFormBase : Form
    {
        /// <summary>Creates a low-flicker form surface and enables Escape key handling.</summary>
        protected ServoFormBase()
        {
            RenderHelper.EnableDoubleBuffer(this);
            KeyPreview = true;
        }

        /// <summary>Enables child double buffering and logs form navigation when the form opens.</summary>
        protected override void OnLoad(EventArgs e)
        {
            RenderHelper.EnableDoubleBufferAll(this);
            RenderHelper.OptimiseAllGrids(this);
            base.OnLoad(e);
            ApplySharedFrontendPolish(this);
            ExceptionLogger.Log("Form opened: " + GetType().Name, "Navigation");
        }

        /// <summary>Applies the same shared frontend polish used by full application pages to modal forms.</summary>
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null)
                ApplySharedFrontendPolish(e.Control);
        }

        /// <summary>Logs form navigation when the form closes.</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            ExceptionLogger.Log("Form closed: " + GetType().Name, "Navigation");
        }

        /// <summary>Closes modal forms with Escape using DialogResult.Cancel.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Creates a SafeBackgroundWorker already bound to this form.</summary>
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

        /// <summary>Shows the ServoERP error dialog bound to this form.</summary>
        protected void ShowError(string message, Exception ex = null)
        {
            ServoErrorDialog.Show(this, message, ex);
        }

        /// <summary>Applies low-flicker rendering and performance settings to a grid.</summary>
        protected void OptimiseGrid(DataGridView grid)
        {
            RenderHelper.OptimiseGrid(grid);
        }

        private static void ApplySharedFrontendPolish(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            DS.ApplyTheme(root);
            UIHelper.ApplyInputStyle(root);
            UIHelper.ApplyInputStyles(root.Controls);
            InputOutlineService.ApplyToTree(root);
            UIHelper.ApplyButtonAlignment(root);
            SharedUiPrimitives.ApplyToTree(root);
            CrashProtectionService.AttachToTree(root);
            GlobalCardContextMenu.ApplyToTree(root);
            GlobalDashboardLayoutService.ApplyToTree(root);
            LayoutAuditService.AuditAndFix(root);
        }
    }
}
