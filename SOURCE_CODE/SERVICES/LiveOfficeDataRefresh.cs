using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.Services
{
    /// <summary>Refresh visible read surfaces without interrupting an editor or overlapping database reads.</summary>
    internal static class LiveOfficeDataRefresh
    {
        internal static IDisposable Attach(Control owner, Func<bool> canRefresh, Func<Task> refresh)
        {
            return new Subscription(owner, canRefresh, refresh);
        }
        private sealed class Subscription : IDisposable
        {
            private readonly Control _owner;
            private readonly Func<bool> _canRefresh;
            private readonly Func<Task> _refresh;
            private readonly Timer _timer = new Timer { Interval = 30000 };
            private bool _running, _disposed;
            internal Subscription(Control owner, Func<bool> canRefresh, Func<Task> refresh)
            {
                _owner = owner; _canRefresh = canRefresh; _refresh = refresh;
                _timer.Tick += async (s, e) => await RunAsync();
                owner.HandleCreated += Start; owner.VisibleChanged += Start; owner.Disposed += OwnerDisposed;
                if (owner.IsHandleCreated) _timer.Start();
            }
            private void Start(object sender, EventArgs e) { if (!_disposed && _owner.IsHandleCreated) _timer.Start(); }
            private void OwnerDisposed(object sender, EventArgs e) { Dispose(); }
            private async Task RunAsync()
            {
                if (_running || _disposed || _owner.IsDisposed || !_owner.IsHandleCreated || !_owner.Visible || !_canRefresh()) return;
                var form = _owner.FindForm();
                if (form != null && (form.WindowState == FormWindowState.Minimized || Form.ActiveForm != form)) return;
                if (HasActiveInput(_owner)) return;
                _running = true;
                try { await _refresh(); }
                catch (Exception ex) { AppRuntime.LogException("LiveOfficeDataRefresh", ex); }
                finally { _running = false; }
            }
            private static bool HasActiveInput(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    if (!child.Visible) continue;
                    var combo = child as ComboBox; var grid = child as DataGridView;
                    if ((child is TextBoxBase && child.Focused) || (combo != null && combo.DroppedDown) || (grid != null && grid.IsCurrentCellInEditMode) || HasActiveInput(child)) return true;
                }
                return false;
            }
            public void Dispose()
            {
                if (_disposed) return; _disposed = true; _timer.Stop(); _timer.Dispose();
                _owner.HandleCreated -= Start; _owner.VisibleChanged -= Start; _owner.Disposed -= OwnerDisposed;
            }
        }
    }
}
