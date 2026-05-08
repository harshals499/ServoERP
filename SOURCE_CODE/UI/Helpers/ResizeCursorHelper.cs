using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI.Helpers
{
    public static class ResizeCursorHelper
    {
        private const int SplitterHitPadding = 5;
        private static readonly HashSet<Control> WiredControls = new HashSet<Control>();

        public static void Apply(Control root)
        {
            if (root == null)
                return;

            ApplyRecursive(root);
        }

        private static void ApplyRecursive(Control control)
        {
            if (control == null || WiredControls.Contains(control))
                return;

            WiredControls.Add(control);
            control.Disposed += (s, e) => WiredControls.Remove(control);

            if (control is SplitContainer split)
                WireSplitContainer(split);

            control.ControlAdded += (s, e) => Apply(e.Control);

            foreach (Control child in control.Controls)
                ApplyRecursive(child);
        }

        private static void WireSplitContainer(SplitContainer split)
        {
            split.MouseMove += (s, e) =>
            {
                split.Cursor = IsOverSplitter(split, e.Location)
                    ? (split.Orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit)
                    : Cursors.Default;
            };
            split.MouseLeave += (s, e) => split.Cursor = Cursors.Default;
        }

        private static bool IsOverSplitter(SplitContainer split, Point location)
        {
            int distance = split.SplitterDistance;
            int width = Math.Max(split.SplitterWidth, SplitterHitPadding);
            Rectangle rect;

            if (split.Orientation == Orientation.Vertical)
                rect = new Rectangle(distance - SplitterHitPadding, 0, width + (SplitterHitPadding * 2), split.Height);
            else
                rect = new Rectangle(0, distance - SplitterHitPadding, split.Width, width + (SplitterHitPadding * 2));

            return rect.Contains(location);
        }
    }
}
