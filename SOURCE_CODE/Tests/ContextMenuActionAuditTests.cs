using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ContextMenuActionAuditTests
    {
        /// <summary>Verifies global context menu items are executable ServoERP actions, not dead Windows placeholders.</summary>
        public static IEnumerable<string> RunAll()
        {
            yield return AssertGlobalCardMenuHasNoDeadItems();
        }

        private static string AssertGlobalCardMenuHasNoDeadItems()
        {
            int actionCalls = 0;
            var actions = new WindowsFileContextMenuActions
            {
                Open = ctx => actionCalls++,
                AddToFavorites = ctx => actionCalls++,
                CopyAsPath = ctx => actionCalls++,
                Share = ctx => actionCalls++,
                SendToDesktop = ctx => actionCalls++,
                SendToEmail = ctx => actionCalls++,
                Cut = ctx => actionCalls++,
                Copy = ctx => actionCalls++,
                CreateShortcut = ctx => actionCalls++,
                ToggleLock = ctx => actionCalls++,
                IsLocked = ctx => false,
                Properties = ctx => actionCalls++
            };

            ContextMenuStrip menu = BuildGlobalMenuForTest(actions);
            string[] forbidden =
            {
                "Move to OneDrive",
                "Edit in Notepad",
                "Scan with Microsoft Defender...",
                "Open with",
                "Give access to",
                "Restore previous versions",
                "Delete",
                "Rename"
            };

            List<string> labels = FlattenItems(menu).Select(item => item.Text).ToList();
            foreach (string text in forbidden)
            {
                if (labels.Any(label => string.Equals(label, text, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Global card context menu still exposes obsolete/dead action: " + text);
            }

            foreach (ToolStripMenuItem item in FlattenItems(menu))
            {
                int before = actionCalls;
                item.PerformClick();
                if (actionCalls == before)
                    throw new InvalidOperationException("Context menu item has no executable handler: " + item.Text);
            }

            return "global card context menu exposes only executable ServoERP actions";
        }

        private static ContextMenuStrip BuildGlobalMenuForTest(WindowsFileContextMenuActions actions)
        {
            MethodInfo build = typeof(WindowsFileContextMenu).GetMethod("BuildMenu", BindingFlags.NonPublic | BindingFlags.Static);
            if (build == null)
                throw new InvalidOperationException("WindowsFileContextMenu.BuildMenu was not found for audit.");

            return (ContextMenuStrip)build.Invoke(null, new object[] { "servoerp://card/Audit/Test", actions });
        }

        private static IEnumerable<ToolStripMenuItem> FlattenItems(ToolStrip menu)
        {
            foreach (ToolStripItem item in menu.Items)
            {
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem == null)
                    continue;

                if (menuItem.DropDownItems.Count == 0)
                {
                    yield return menuItem;
                    continue;
                }

                foreach (ToolStripItem child in menuItem.DropDownItems)
                {
                    ToolStripMenuItem childMenu = child as ToolStripMenuItem;
                    if (childMenu != null)
                        yield return childMenu;
                }
            }
        }
    }
}
