using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    internal static class UiPerformanceService
    {
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Applies low-risk DataGridView settings that reduce repaint lag during large binds.</summary>
        public static void ApplyGridPerformance(DataGridView grid)
        {
            if (grid == null)
                return;

            EnableDoubleBuffer(grid);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        /// <summary>Runs a UI rebuild while redraw is suspended, then refreshes the control once.</summary>
        public static void WithSuspendedDrawing(Control control, Action action)
        {
            if (control == null || action == null || control.IsDisposed || !control.IsHandleCreated)
            {
                action?.Invoke();
                return;
            }

            try
            {
                SendMessage(control.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                control.SuspendLayout();
                action();
            }
            finally
            {
                try { control.ResumeLayout(true); } catch { }
                SendMessage(control.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                control.Invalidate(true);
            }
        }

        /// <summary>Enables the protected DoubleBuffered flag on WinForms controls that repaint frequently.</summary>
        public static void EnableDoubleBuffer(Control control)
        {
            if (control == null)
                return;

            try
            {
                PropertyInfo property = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                property?.SetValue(control, true, null);
            }
            catch
            {
            }
        }
    }
}
