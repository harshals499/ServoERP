using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    public class DeferredPageControl : BaseUserControl
    {
        private bool _deferredLoadQueued;
        private bool _deferredLoadCompleted;
        protected virtual bool EnableAutomaticLayoutScaling => true;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            DS.ApplyTheme(this);
            if (EnableAutomaticLayoutScaling)
                LayoutScaler.ScaleControl(this);
            LayoutScaler.ApplyGlobalScale(this);
            UIHelper.ApplyGlobalScrollAndResize(this);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control != null)
                DS.ApplyTheme(e.Control);
            if (e.Control != null)
                UIHelper.ApplyInputStyle(e.Control);
            if (e.Control != null)
                UIHelper.ApplyInputStyles(e.Control.Controls);
            if (e.Control != null)
                UIHelper.ApplyGlobalScrollAndResize(e.Control);
            if (EnableAutomaticLayoutScaling && e.Control != null)
                LayoutScaler.ScaleControl(e.Control);
            if (e.Control != null)
                LayoutScaler.ApplyGlobalScale(e.Control);
        }

        protected void EnableDeferredLoad(Action loadAction, Action<Exception> onError = null)
        {
            HandleCreated += (s, e) => QueueDeferredLoad(loadAction, onError);
            ParentChanged += (s, e) => QueueDeferredLoad(loadAction, onError);
            Load += (s, e) => QueueDeferredLoad(loadAction, onError);
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                    QueueDeferredLoad(loadAction, onError);
            };
            if (IsHandleCreated && Parent != null)
                QueueDeferredLoad(loadAction, onError);
        }

        protected void EnableDeferredLoad(Func<Task> loadAsync, Action<Exception> onError = null)
        {
            HandleCreated += (s, e) => QueueDeferredLoad(loadAsync, onError);
            ParentChanged += (s, e) => QueueDeferredLoad(loadAsync, onError);
            Load += (s, e) => QueueDeferredLoad(loadAsync, onError);
            VisibleChanged += (s, e) =>
            {
                if (Visible)
                    QueueDeferredLoad(loadAsync, onError);
            };
            if (IsHandleCreated && Parent != null)
                QueueDeferredLoad(loadAsync, onError);
        }

        protected void QueueDeferredLoad(Action loadAction, Action<Exception> onError = null)
        {
            Control dispatcher = FindForm() ?? Parent;
            if (_deferredLoadQueued || _deferredLoadCompleted || dispatcher == null || !dispatcher.IsHandleCreated || loadAction == null)
                return;

            _deferredLoadQueued = true;
            try
            {
                if (IsDisposed)
                    return;

                loadAction();
                DS.ApplyTheme(this);
                if (EnableAutomaticLayoutScaling)
                    LayoutScaler.ScaleControl(this);
                LayoutScaler.ApplyGlobalScale(this);
                UIHelper.ApplyGlobalScrollAndResize(this);
                _deferredLoadCompleted = true;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
            finally
            {
                _deferredLoadQueued = false;
            }
        }

        protected void QueueDeferredLoad(Func<Task> loadAsync, Action<Exception> onError = null)
        {
            Control dispatcher = FindForm() ?? Parent;
            if (_deferredLoadQueued || _deferredLoadCompleted || dispatcher == null || !dispatcher.IsHandleCreated || loadAsync == null)
                return;

            _deferredLoadQueued = true;
            RunDeferredLoadAsync(loadAsync, onError);
        }

        private async void RunDeferredLoadAsync(Func<Task> loadAsync, Action<Exception> onError)
        {
            try
            {
                if (IsDisposed)
                    return;

                await loadAsync();
                DS.ApplyTheme(this);
                if (EnableAutomaticLayoutScaling)
                    LayoutScaler.ScaleControl(this);
                LayoutScaler.ApplyGlobalScale(this);
                UIHelper.ApplyGlobalScrollAndResize(this);
                _deferredLoadCompleted = true;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
            finally
            {
                _deferredLoadQueued = false;
            }
        }

        protected void ResetDeferredLoad()
        {
            _deferredLoadQueued = false;
            _deferredLoadCompleted = false;
        }
    }
}
