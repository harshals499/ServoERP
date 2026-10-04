using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ServoERP.Infrastructure
{
    internal static class AppIconService
    {
        private const string AppUserModelId = "ServoERP.Desktop";
        private const int WmSetIcon = 0x0080;
        private const int IconSmall = 0;
        private const int IconBig = 1;
        private static readonly object Sync = new object();
        private static Icon _applicationIcon;

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        public static void InitializeProcessIdentity()
        {
            try { SetCurrentProcessExplicitAppUserModelID(AppUserModelId); }
            catch { }
        }

        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed)
                return;

            Icon icon = GetApplicationIcon();
            if (icon == null)
                return;

            form.ShowIcon = true;
            form.Icon = icon;
            form.HandleCreated += (s, e) => ApplyNativeWindowIcons(form, icon);
            if (form.IsHandleCreated)
                ApplyNativeWindowIcons(form, icon);
        }

        private static Icon GetApplicationIcon()
        {
            lock (Sync)
            {
                if (_applicationIcon != null)
                    return _applicationIcon;

                try
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                    if (File.Exists(iconPath))
                        _applicationIcon = new Icon(iconPath);
                    else
                        _applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                    _applicationIcon = null;
                }

                return _applicationIcon;
            }
        }

        private static void ApplyNativeWindowIcons(Form form, Icon icon)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated || icon == null)
                return;

            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconBig), icon.Handle);
            SendMessage(form.Handle, WmSetIcon, new IntPtr(IconSmall), icon.Handle);
        }
    }
}
