using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;
using HVAC_Pro_Desktop.UI.Helpers;

namespace HVAC_Pro_Desktop.UI
{
    public class DeferredPageControl : BaseUserControl
    {
        private const int MINIMUM_MAIN_CANVAS_WIDTH = 0;
        private const int MINIMUM_MAIN_CANVAS_HEIGHT = 920;

        private bool _deferredLoadQueued;
        private bool _deferredLoadCompleted;
        private bool _hasDeferredLoad;
        protected virtual bool EnableAutomaticLayoutScaling => true;

        protected virtual bool EnableMainScrollCanvas => true;

        protected virtual bool SuppressAutomaticChildPolish => false;

        protected override bool SuppressBaseAutomaticChildPolish => SuppressAutomaticChildPolish;

        public bool DeferredLoadQueued => _deferredLoadQueued;

        public bool DeferredLoadCompleted => _deferredLoadCompleted;

        public bool HasDeferredLoad => _hasDeferredLoad;

        protected virtual Size MainScrollCanvasMinimum => new Size(MINIMUM_MAIN_CANVAS_WIDTH, MINIMUM_MAIN_CANVAS_HEIGHT);

        /// <summary>Creates the base deferred page surface used by dense ERP module screens.</summary>
        public DeferredPageControl()
        {
            AutoScroll = true;
            Resize += (s, e) => ApplyMainScrollCanvas();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (SuppressAutomaticChildPolish)
                return;

            ApplyMainScrollCanvas();
            DS.ApplyTheme(this);
            if (EnableAutomaticLayoutScaling)
                LayoutScaler.ScaleControl(this);
            LayoutScaler.ApplyGlobalScale(this);
            UIHelper.ApplyGlobalScrollAndResize(this);
            PageHeaderPolishService.Apply(this);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (SuppressAutomaticChildPolish)
                return;

            if (e.Control != null)
                DS.ApplyTheme(e.Control);
            if (e.Control != null)
                UIHelper.ApplyInputStyle(e.Control);
            if (e.Control != null)
                UIHelper.ApplyInputStyles(e.Control.Controls);
            if (e.Control != null)
                UIHelper.ApplyGlobalScrollAndResize(e.Control);
            if (e.Control != null)
                UIHelper.ApplyButtonAlignment(e.Control);
            if (EnableAutomaticLayoutScaling && e.Control != null)
                LayoutScaler.ScaleControl(e.Control);
            if (e.Control != null)
                LayoutScaler.ApplyGlobalScale(e.Control);
            if (e.Control != null)
                LanguageManager.ApplyControlTree(e.Control);
            if (e.Control != null)
                GlobalCardContextMenu.ApplyToTree(e.Control);
            ApplyMainScrollCanvas();
        }

        /// <summary>Keeps dense module pages reachable on smaller desktops by exposing a page-level scroll canvas.</summary>
        protected void ApplyMainScrollCanvas()
        {
            if (!EnableMainScrollCanvas || IsDisposed)
                return;

            Size minimum = MainScrollCanvasMinimum;
            if (minimum.Width <= 0 || minimum.Height <= 0)
                return;

            AutoScroll = true;
            AutoScrollMinSize = new Size(
                0,
                Math.Max(AutoScrollMinSize.Height, minimum.Height));

            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;

            foreach (Control child in Controls)
                ApplyCanvasMinimumToRoot(child, minimum);
        }

        /// <summary>Applies the shared scroll canvas size to top-level fill panels without changing their layout logic.</summary>
        private void ApplyCanvasMinimumToRoot(Control control, Size minimum)
        {
            if (control == null || control.IsDisposed)
                return;

            if (control.Dock == DockStyle.Fill)
            {
                int width = minimum.Width <= 0 ? 0 : Math.Max(control.MinimumSize.Width, minimum.Width);
                int height = Math.Max(control.MinimumSize.Height, minimum.Height);
                if (control.MinimumSize.Width != width || control.MinimumSize.Height != height)
                    control.MinimumSize = new Size(width, height);
            }
        }

        protected void EnableDeferredLoad(Action loadAction, Action<Exception> onError = null)
        {
            _hasDeferredLoad = true;
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

        protected void EnableDeferredLoadBeforeVisible(Action loadAction, Action<Exception> onError = null)
        {
            _hasDeferredLoad = true;
            HandleCreated += (s, e) => QueueDeferredLoad(loadAction, onError);
            ParentChanged += (s, e) => QueueDeferredLoad(loadAction, onError);
            Load += (s, e) => QueueDeferredLoad(loadAction, onError);
            if (IsHandleCreated && Parent != null)
                QueueDeferredLoad(loadAction, onError);
        }

        protected void EnableDeferredLoad(Func<Task> loadAsync, Action<Exception> onError = null)
        {
            _hasDeferredLoad = true;
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
                ApplyMainScrollCanvas();
                UIHelper.ApplyButtonAlignment(this);
                GlobalCardContextMenu.ApplyToTree(this);
                PageHeaderPolishService.Apply(this);
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
                ApplyMainScrollCanvas();
                UIHelper.ApplyButtonAlignment(this);
                GlobalCardContextMenu.ApplyToTree(this);
                PageHeaderPolishService.Apply(this);
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

        protected void MarkDeferredLoadCompleted()
        {
            _hasDeferredLoad = true;
            _deferredLoadQueued = false;
            _deferredLoadCompleted = true;
        }
    }
}
