using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class WindowsFileContextMenuActions
    {
        public Action<object> Open { get; set; }
        public Func<object, bool> IsFavorite { get; set; }
        public Action<object> ToggleFavorite { get; set; }
        public Action<object> Copy { get; set; }
        public Action<object> Cut { get; set; }
        public Action<object> Share { get; set; }
        public Action<object> CopyAsPath { get; set; }
        public Action<object> CreateShortcut { get; set; }
        public Action<object> SendToDashboard { get; set; }
        public Action<object> SendToFavorites { get; set; }
        public Action<object> SendToShortcuts { get; set; }
        public Func<object, bool> IsLocked { get; set; }
        public Action<object> ToggleLock { get; set; }
        public Action<object> HideCard { get; set; }
        public Action<object> DeleteCard { get; set; }
        public Action<object> RestoreCard { get; set; }
        public Action<object> Properties { get; set; }
    }

    internal static class WindowsFileContextMenu
    {
        private const int MenuWidth = 276;
        private const int RowHeight = 28;

        private static readonly Color HoverBackColor = Color.FromArgb(229, 240, 251);
        private static readonly Color TextColor = Color.FromArgb(20, 20, 20);
        private static readonly Color DangerColor = Color.FromArgb(185, 28, 28);
        private static readonly Font MenuFont = new Font("Segoe UI", 9f, FontStyle.Regular);

        /// <summary>Attaches the Windows-style context menu to a control tree.</summary>
        public static void Attach(Control control, Func<object> contextFactory, WindowsFileContextMenuActions actions)
        {
            if (control == null || contextFactory == null)
                return;

            AttachControlOnly(control, contextFactory, actions);
            foreach (Control child in control.Controls)
                Attach(child, contextFactory, actions);
            control.ControlAdded += (s, e) => Attach(e.Control, contextFactory, actions);
        }

        /// <summary>Attaches the Windows-style context menu to only one control.</summary>
        public static void AttachControlOnly(Control control, Func<object> contextFactory, WindowsFileContextMenuActions actions)
        {
            if (control == null || contextFactory == null)
                return;

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
            AddItem(menu, IsFavorite(context, actions) ? "Remove from Favorites" : "Add to Favorites", context, actions.ToggleFavorite);
            AddItem(menu, "Copy as Path", context, actions.CopyAsPath);
            AddItem(menu, "Share", context, actions.Share);
            AddSubmenu(menu, "Send To", context,
                new MenuAction("Dashboard", actions.SendToDashboard),
                new MenuAction("Favorites", actions.SendToFavorites),
                new MenuAction("Shortcuts", actions.SendToShortcuts));
            AddSeparator(menu);
            AddItem(menu, "Cut", context, actions.Cut);
            AddItem(menu, "Copy", context, actions.Copy);
            AddItem(menu, "Create Shortcut", context, actions.CreateShortcut);
            AddSeparator(menu);
            AddItem(menu, IsLocked(context, actions) ? "Unlock Card" : "Lock Card", context, actions.ToggleLock);
            AddItem(menu, "Hide Card", context, actions.HideCard);
            AddItem(menu, "Delete Card", context, actions.DeleteCard, false, DangerColor);
            AddItem(menu, "Restore Card", context, actions.RestoreCard);
            AddSeparator(menu);
            AddItem(menu, "Properties", context, actions.Properties);
            RemoveTrailingSeparators(menu);

            menu.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    menu.Close(ToolStripDropDownCloseReason.Keyboard);
            };

            return menu;
        }

        private static void AddItem(ContextMenuStrip menu, string text, object context, Action<object> action, bool bold = false, Color? foreColor = null)
        {
            if (action == null)
                return;

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
                action(context);
            };

            menu.Items.Add(item);
        }

        private static void AddSubmenu(ContextMenuStrip menu, string text, object context, params MenuAction[] actions)
        {
            if (actions == null || actions.All(action => action == null || action.Handler == null))
                return;

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

            foreach (MenuAction action in actions.Where(action => action != null && action.Handler != null))
            {
                var child = new ToolStripMenuItem(action.Text)
                {
                    AutoSize = false,
                    Width = 210,
                    Height = RowHeight,
                    Padding = new Padding(8, 0, 8, 0)
                };
                child.Click += (s, e) => action.Handler(context);
                item.DropDownItems.Add(child);
            }

            menu.Items.Add(item);
        }

        private static void AddSeparator(ContextMenuStrip menu)
        {
            if (menu.Items.Count == 0 || menu.Items[menu.Items.Count - 1] is ToolStripSeparator)
                return;

            menu.Items.Add(new ToolStripSeparator { AutoSize = false, Height = 7, Width = MenuWidth - 8 });
        }

        private static void RemoveTrailingSeparators(ContextMenuStrip menu)
        {
            if (menu == null)
                return;

            while (menu.Items.Count > 0 && menu.Items[menu.Items.Count - 1] is ToolStripSeparator)
                menu.Items.RemoveAt(menu.Items.Count - 1);
        }

        private static bool IsLocked(object context, WindowsFileContextMenuActions actions)
        {
            try
            {
                return actions != null && actions.IsLocked != null && actions.IsLocked(context);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFavorite(object context, WindowsFileContextMenuActions actions)
        {
            try
            {
                return actions != null && actions.IsFavorite != null && actions.IsFavorite(context);
            }
            catch
            {
                return false;
            }
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

        private sealed class MenuAction
        {
            public readonly string Text;
            public readonly Action<object> Handler;

            public MenuAction(string text, Action<object> handler)
            {
                Text = text;
                Handler = handler;
            }
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
