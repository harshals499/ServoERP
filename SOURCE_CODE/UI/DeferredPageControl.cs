using System;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
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
        private bool _postLoadPolishQueued;
        private bool _controlTreePolishQueued;
        protected virtual bool EnableAutomaticLayoutScaling => true;

        protected virtual bool EnableMainScrollCanvas => true;

        protected virtual bool SuppressAutomaticChildPolish => false;

        protected override bool SuppressBaseAutomaticChildPolish => SuppressAutomaticChildPolish;

        private bool _firstPaintTimingLogged;

        public bool DeferredLoadQueued => _deferredLoadQueued;

        public bool DeferredLoadCompleted => _deferredLoadCompleted;

        public bool HasDeferredLoad => _hasDeferredLoad;

        /// <summary>Deferred ERP modules are treated as heavy shell pages unless a page explicitly opts out.</summary>
        public virtual bool IsHeavyShellPage => true;

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
            {
                DS.ApplyTheme(e.Control);
                UIHelper.ApplyInputStyle(e.Control);
            }
            ApplyMainScrollCanvas();
            QueueControlTreePolish();
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
                _deferredLoadCompleted = true;
                QueuePostLoadPolish();
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
                _deferredLoadCompleted = true;
                QueuePostLoadPolish();
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

        /// <summary>Lets the main shell notify a page that it became the active visible module.</summary>
        public virtual void OnShellActivated()
        {
        }

        /// <summary>Lets the main shell notify a page that it is no longer the active visible module.</summary>
        public virtual void OnShellDeactivated()
        {
        }

        /// <summary>Lets the main shell notify a page before it is evicted from cache and disposed.</summary>
        public virtual void OnShellCacheEvicted()
        {
        }

        public virtual ModuleState CaptureModuleState(string pageKey)
        {
            return new ModuleState
            {
                PageKey = pageKey,
                ActiveTab = FindActiveTabKey(this),
                ScrollPosition = FindPrimaryScrollPosition(this)
            };
        }

        public virtual void RestoreModuleState(ModuleState state)
        {
            if (state == null)
                return;

            if (!string.IsNullOrWhiteSpace(state.ActiveTab))
                RestoreActiveTab(this, state.ActiveTab);
            RestorePrimaryScrollPosition(this, state.ScrollPosition);
        }

        private static int FindPrimaryScrollPosition(Control root)
        {
            if (root == null)
                return 0;

            ScrollableControl scroll = root as ScrollableControl;
            if (scroll != null && scroll.VerticalScroll != null)
                return scroll.VerticalScroll.Value;

            foreach (Control child in root.Controls)
            {
                int childPosition = FindPrimaryScrollPosition(child);
                if (childPosition > 0)
                    return childPosition;
            }

            return 0;
        }

        private static void RestorePrimaryScrollPosition(Control root, int scrollPosition)
        {
            if (root == null || scrollPosition <= 0)
                return;

            ScrollableControl scroll = root as ScrollableControl;
            if (scroll != null)
            {
                try
                {
                    scroll.AutoScrollPosition = new Point(0, scrollPosition);
                    return;
                }
                catch
                {
                }
            }

            foreach (Control child in root.Controls)
                RestorePrimaryScrollPosition(child, scrollPosition);
        }

        protected void RegisterFirstPaintTiming(string context, Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return;

            VisibleChanged += (s, e) =>
            {
                if (!Visible || _firstPaintTimingLogged)
                    return;

                Action log = () =>
                {
                    if (_firstPaintTimingLogged || IsDisposed || !Visible)
                        return;

                    _firstPaintTimingLogged = true;
                    AppRuntime.LogTiming(context, stopwatch.ElapsedMilliseconds);
                };

                InvokeWhenReady(log);
            };
        }

        private static string FindActiveTabKey(Control root)
        {
            if (root == null)
                return string.Empty;

            TabControl tabs = root as TabControl;
            if (tabs != null && tabs.SelectedTab != null)
                return tabs.SelectedTab.Name ?? tabs.SelectedTab.Text ?? string.Empty;

            foreach (Control child in root.Controls)
            {
                string key = FindActiveTabKey(child);
                if (!string.IsNullOrWhiteSpace(key))
                    return key;
            }

            return string.Empty;
        }

        private static void RestoreActiveTab(Control root, string tabKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(tabKey))
                return;

            TabControl tabs = root as TabControl;
            if (tabs != null)
            {
                foreach (TabPage tab in tabs.TabPages)
                {
                    if (string.Equals(tab.Name, tabKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(tab.Text, tabKey, StringComparison.OrdinalIgnoreCase))
                    {
                        tabs.SelectedTab = tab;
                        return;
                    }
                }
            }

            foreach (Control child in root.Controls)
                RestoreActiveTab(child, tabKey);
        }

        private void QueueControlTreePolish()
        {
            if (_controlTreePolishQueued || IsDisposed || !IsHandleCreated)
                return;

            _controlTreePolishQueued = true;
            InvokeWhenReady(() =>
            {
                _controlTreePolishQueued = false;
                QueuePostLoadPolish();
            });
        }

        private void QueuePostLoadPolish()
        {
            if (_postLoadPolishQueued || IsDisposed)
                return;

            Action polish = () =>
            {
                if (IsDisposed)
                    return;

                _postLoadPolishQueued = false;
                UiPerformanceService.WithSuspendedDrawing(this, () =>
                {
                    ApplyMainScrollCanvas();
                    PageHeaderPolishService.Apply(this);
                    if (SuppressAutomaticChildPolish)
                    {
                        UIHelper.ApplyButtonAlignment(this);
                        return;
                    }

                    DS.ApplyTheme(this);
                    if (EnableAutomaticLayoutScaling)
                        LayoutScaler.ScaleControl(this);
                    LayoutScaler.ApplyGlobalScale(this);
                    UIHelper.ApplyGlobalScrollAndResize(this);
                    UIHelper.ApplyButtonAlignment(this);
                    GlobalCardContextMenu.ApplyToTree(this);
                });
            };

            _postLoadPolishQueued = true;
            InvokeWhenReady(polish);
        }

        private void InvokeWhenReady(Action action)
        {
            if (action == null || IsDisposed)
                return;

            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke(action);
                    return;
                }
                catch (InvalidOperationException)
                {
                }
            }

            action();
        }
    }
}
