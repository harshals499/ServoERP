using System;
using System.Reflection;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    /// <summary>Shared rendering helpers for low-flicker ServoERP forms and pages.</summary>
    public static class RenderHelper
    {
        /// <summary>Enables double buffering on a WinForms control when supported.</summary>
        public static void EnableDoubleBuffer(Control control)
        {
            if (control == null)
                return;

            try
            {
                PropertyInfo property = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (property != null)
                    property.SetValue(control, true, null);
            }
            catch
            {
            }
        }

        /// <summary>Enables double buffering on a control and every child control.</summary>
        public static void EnableDoubleBufferAll(Control control)
        {
            if (control == null)
                return;

            EnableDoubleBuffer(control);
            foreach (Control child in control.Controls)
                EnableDoubleBufferAll(child);
        }

        /// <summary>Suspends layout while a control tree is rebuilt, then repaints once.</summary>
        public static void BatchUpdate(Control control, Action action)
        {
            if (action == null)
                return;

            if (control == null || control.IsDisposed)
            {
                action();
                return;
            }

            try
            {
                control.SuspendLayout();
                action();
            }
            finally
            {
                try { control.ResumeLayout(true); } catch { }
                try { control.Invalidate(true); } catch { }
            }
        }

        /// <summary>Applies low-flicker rendering and stable grid performance settings.</summary>
        public static void OptimiseGrid(DataGridView grid)
        {
            if (grid == null)
                return;

            EnableDoubleBuffer(grid);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        }

        /// <summary>Applies grid performance settings to every DataGridView in the control tree.</summary>
        public static void OptimiseAllGrids(Control control)
        {
            if (control == null)
                return;

            DataGridView grid = control as DataGridView;
            if (grid != null)
                OptimiseGrid(grid);

            foreach (Control child in control.Controls)
                OptimiseAllGrids(child);
        }
    }
}
