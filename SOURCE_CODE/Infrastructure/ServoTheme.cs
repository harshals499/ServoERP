using System.Drawing;
using System.Windows.Forms;
using Krypton.Toolkit;
using HVAC_Pro_Desktop.UI;

namespace ServoERP.Infrastructure
{
    /// <summary>
    /// Single application-wide theme entry point. Krypton owns the native form chrome and
    /// supported tool strips; the ServoERP design system bridges the same visual language to
    /// the existing WinForms controls while screens are migrated incrementally.
    /// </summary>
    internal static class ServoTheme
    {
        private static readonly Font ApplicationFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        private static readonly KryptonManager PaletteManager = new KryptonManager();

        internal static void Initialize()
        {
            PaletteManager.GlobalPaletteMode = PaletteMode.Microsoft365Blue;
            PaletteManager.BaseFont = ApplicationFont;
            PaletteManager.GlobalApplyToolstrips = true;
        }

        internal static void ApplyTo(Control root)
        {
            if (root == null || root.IsDisposed)
                return;

            root.Font = ApplicationFont;
            DS.ApplyTheme(root);
        }
    }
}
