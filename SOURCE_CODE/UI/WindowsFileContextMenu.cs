using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class WindowsFileContextMenuActions
    {
        public Action<object> Open { get; set; }
        public Action<object> Delete { get; set; }
        public Action<object> Rename { get; set; }
        public Action<object> Copy { get; set; }
        public Action<object> Cut { get; set; }
        public Action<object> Share { get; set; }
    }

    internal static class WindowsFileContextMenu
    {
        private const int MenuWidth = 276;
        private const int RowHeight = 28;

        private static readonly Color HoverBackColor = Color.FromArgb(229, 240, 251);
        private static readonly Color TextColor = Color.FromArgb(20, 20, 20);
        private static readonly Color DangerColor = Color.FromArgb(185, 28, 28);
        private static readonly Font MenuFont = new Font("Segoe UI", 9f, FontStyle.Regular);

        public static void Attach(Control control, Func<object> contextFactory, WindowsFileContextMenuActions actions)
        {
            if (control == null || contextFactory == null)
                return;

            AttachSingle(control, contextFactory, actions);
            foreach (Control child in control.Controls)
                Attach(child, contextFactory, actions);
            control.ControlAdded += (s, e) => Attach(e.Control, contextFactory, actions);
        }

        private static void AttachSingle(Control control, Func<object> contextFactory, WindowsFileContextMenuActions actions)
        {
            control.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Right)
                    return;

                Show(control, e.Location, contextFactory(), actions);
            };
        }

        public static void Show(Control owner, Point clientPoint, object context, WindowsFileContextMenuActions actions)
        {
            if (owner == null || owner.IsDisposed)
                return;

            ContextMenuStrip menu = BuildMenu(context, actions ?? new WindowsFileContextMenuActions());
            Point screenPoint = owner.PointToScreen(clientPoint);
            menu.Show(ClampToScreen(screenPoint, menu.GetPreferredSize(Size.Empty)));
        }

        private static ContextMenuStrip BuildMenu(object context, WindowsFileContextMenuActions actions)
        {
            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Font = MenuFont,
                BackColor = Color.White,
                ForeColor = TextColor,
                Padding = new Padding(4, 3, 4, 3),
                Renderer = new WindowsFileContextMenuRenderer(),
                DropShadowEnabled = true,
                AutoSize = true
            };

            AddItem(menu, "Open", context, actions.Open, true);
            AddSeparator(menu);
            AddItem(menu, "Move to OneDrive", context, null);
            AddItem(menu, "Edit in Notepad", context, null);
            AddItem(menu, "Add to Favorites", context, null);
            AddItem(menu, "Scan with Microsoft Defender...", context, null);
            AddSubmenu(menu, "Open with", context, "Choose another app", "Search Microsoft Store");
            AddSeparator(menu);
            AddSubmenu(menu, "Give access to", context, "Specific people...", "Remove access");
            AddItem(menu, "Copy as path", context, null);
            AddItem(menu, "Share", context, actions.Share);
            AddItem(menu, "Restore previous versions", context, null);
            AddSeparator(menu);
            AddSubmenu(menu, "Send to", context, "Desktop (create shortcut)", "Compressed folder");
            AddSeparator(menu);
            AddItem(menu, "Cut", context, actions.Cut);
            AddItem(menu, "Copy", context, actions.Copy);
            AddSeparator(menu);
            AddItem(menu, "Create shortcut", context, null);
            AddItem(menu, "Delete", context, actions.Delete, false, DangerColor);
            AddItem(menu, "Rename", context, actions.Rename);
            AddSeparator(menu);
            AddItem(menu, "Properties", context, null);

            menu.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    menu.Close(ToolStripDropDownCloseReason.Keyboard);
            };

            return menu;
        }

        private static void AddItem(ContextMenuStrip menu, string text, object context, Action<object> action, bool bold = false, Color? foreColor = null)
        {
            var item = new ToolStripMenuItem(text)
            {
                AutoSize = false,
                Width = MenuWidth - 8,
                Height = RowHeight,
                Padding = new Padding(8, 0, 8, 0),
                Font = bold ? new Font(MenuFont, FontStyle.Bold) : MenuFont,
                ForeColor = foreColor ?? TextColor
            };

            item.Click += (s, e) =>
            {
                if (action != null)
                    action(context);
                else
                    Debug.WriteLine("Context menu action: " + text + " -> " + Describe(context));
            };

            menu.Items.Add(item);
        }

        private static void AddSubmenu(ContextMenuStrip menu, string text, object context, params string[] placeholders)
        {
            var item = new ToolStripMenuItem(text)
            {
                AutoSize = false,
                Width = MenuWidth - 8,
                Height = RowHeight,
                Padding = new Padding(8, 0, 8, 0),
                Font = MenuFont,
                ForeColor = TextColor
            };

            item.DropDown.Padding = new Padding(4, 3, 4, 3);
            item.DropDown.BackColor = Color.White;
            item.DropDown.Font = MenuFont;
            item.DropDown.Renderer = new WindowsFileContextMenuRenderer();

            foreach (string placeholder in placeholders)
            {
                var child = new ToolStripMenuItem(placeholder)
                {
                    AutoSize = false,
                    Width = 210,
                    Height = RowHeight,
                    Padding = new Padding(8, 0, 8, 0)
                };
                child.Click += (s, e) => Debug.WriteLine("Context submenu action: " + text + " / " + placeholder + " -> " + Describe(context));
                item.DropDownItems.Add(child);
            }

            menu.Items.Add(item);
        }

        private static void AddSeparator(ContextMenuStrip menu)
        {
            menu.Items.Add(new ToolStripSeparator { AutoSize = false, Height = 7, Width = MenuWidth - 8 });
        }

        private static Point ClampToScreen(Point requested, Size preferredSize)
        {
            Rectangle bounds = Screen.FromPoint(requested).WorkingArea;
            int x = requested.X;
            int y = requested.Y;

            if (x + preferredSize.Width > bounds.Right)
                x = Math.Max(bounds.Left, requested.X - preferredSize.Width);
            if (y + preferredSize.Height > bounds.Bottom)
                y = Math.Max(bounds.Top, requested.Y - preferredSize.Height);

            return new Point(x, y);
        }

        private static string Describe(object context)
        {
            return context == null ? "(null)" : context.ToString();
        }

        private sealed class WindowsFileContextMenuRenderer : ToolStripProfessionalRenderer
        {
            public WindowsFileContextMenuRenderer()
                : base(new WindowsFileContextMenuColorTable())
            {
                RoundedEdges = true;
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                using (Pen border = new Pen(Color.FromArgb(224, 224, 224)))
                    e.Graphics.DrawRectangle(border, bounds);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
                Color back = e.Item.Selected ? HoverBackColor : Color.White;
                using (SolidBrush brush = new SolidBrush(back))
                    e.Graphics.FillRectangle(brush, bounds);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.Height / 2;
                using (Pen pen = new Pen(Color.FromArgb(229, 229, 229)))
                    e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.FromArgb(32, 32, 32);
                base.OnRenderArrow(e);
            }
        }

        private sealed class WindowsFileContextMenuColorTable : ProfessionalColorTable
        {
            public override Color MenuBorder => Color.FromArgb(224, 224, 224);
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color MenuItemSelected => HoverBackColor;
            public override Color MenuItemBorder => HoverBackColor;
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
            public override Color SeparatorDark => Color.FromArgb(229, 229, 229);
            public override Color SeparatorLight => Color.FromArgb(229, 229, 229);
        }
    }
}
