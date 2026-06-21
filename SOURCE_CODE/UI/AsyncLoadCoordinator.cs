using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServoERP.Infrastructure;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class AsyncLoadCoordinator : IDisposable
    {
        private readonly Control _owner;
        private readonly System.Windows.Forms.Timer _debounceTimer;
        private CancellationTokenSource _cancellation;
        private int _version;
        private Func<CancellationToken, object> _summaryLoader;
        private Func<CancellationToken, object> _detailLoader;

        public AsyncLoadCoordinator(Control owner)
        {
            _owner = owner;
            _debounceTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public Action OnShellReady { get; set; }

        public Action<object> OnSummaryReady { get; set; }

        public Action<object> OnDetailReady { get; set; }

        public Action<Exception> OnError { get; set; }

        public void QueueLoad(Func<CancellationToken, object> summaryLoader, Func<CancellationToken, object> detailLoader = null, int debounceMs = 250)
        {
            _summaryLoader = summaryLoader;
            _detailLoader = detailLoader;
            _debounceTimer.Interval = Math.Max(1, debounceMs);
            _debounceTimer.Stop();
            CancelActiveLoad();
            _debounceTimer.Start();
        }

        public void CancelActiveLoad()
        {
            if (_cancellation == null)
                return;

            try
            {
                _cancellation.Cancel();
            }
            catch
            {
            }

            _cancellation.Dispose();
            _cancellation = null;
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            int version = ++_version;
            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;

            UIThread.Run(_owner, () => OnShellReady?.Invoke());

            Task.Run(() => _summaryLoader == null ? null : _summaryLoader(token), token)
                .ContinueWith(task =>
                {
                    if (task.IsCanceled || token.IsCancellationRequested || version != _version)
                        return;

                    if (task.IsFaulted)
                    {
                        Exception ex = task.Exception == null ? null : task.Exception.GetBaseException();
                        UIThread.Run(_owner, () => OnError?.Invoke(ex));
                        return;
                    }

                    UIThread.Run(_owner, () => OnSummaryReady?.Invoke(task.Result));
                    if (_detailLoader == null)
                        return;

                    Task.Run(() => _detailLoader(token), token)
                        .ContinueWith(detailTask =>
                        {
                            if (detailTask.IsCanceled || token.IsCancellationRequested || version != _version)
                                return;

                            if (detailTask.IsFaulted)
                            {
                                Exception detailEx = detailTask.Exception == null ? null : detailTask.Exception.GetBaseException();
                                UIThread.Run(_owner, () => OnError?.Invoke(detailEx));
                                return;
                            }

                            UIThread.Run(_owner, () => OnDetailReady?.Invoke(detailTask.Result));
                        }, TaskScheduler.Default);
                }, TaskScheduler.Default);
        }

        public void Dispose()
        {
            CancelActiveLoad();
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
        }
    }
}
