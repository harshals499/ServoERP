using System;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Marshals actions safely onto the UI thread.</summary>
    public static class UIThread
    {
        /// <summary>Runs the action on the UI thread whether called from UI or background thread.</summary>
        public static void Run(Control control, Action action)
        {
            if (action == null)
                return;

            try
            {
                if (control == null || control.IsDisposed)
                {
                    action();
                    return;
                }

                if (control.InvokeRequired)
                    control.Invoke(action);
                else
                    action();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        /// <summary>Runs the function on the UI thread and returns its value.</summary>
        public static T Run<T>(Control control, Func<T> func)
        {
            if (func == null)
                return default(T);

            try
            {
                if (control == null || control.IsDisposed)
                    return func();

                if (control.InvokeRequired)
                    return (T)control.Invoke(func);

                return func();
            }
            catch (ObjectDisposedException)
            {
                return default(T);
            }
            catch (InvalidOperationException)
            {
                return default(T);
            }
        }

        /// <summary>Posts the action to the UI thread without waiting.</summary>
        public static void Post(Control control, Action action)
        {
            if (action == null)
                return;

            try
            {
                if (control == null || control.IsDisposed)
                {
                    action();
                    return;
                }

                if (control.InvokeRequired)
                    control.BeginInvoke(action);
                else
                    action();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
