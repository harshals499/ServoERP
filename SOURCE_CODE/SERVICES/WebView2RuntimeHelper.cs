using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace HVAC_Pro_Desktop.Services
{
    public static class WebView2RuntimeHelper
    {
        public const string DownloadUrl = "https://developer.microsoft.com/en-us/microsoft-edge/webview2/";

        public static string GetUserDataFolder()
        {
            string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(baseFolder))
                baseFolder = Path.Combine(Path.GetTempPath(), "ServoERP");

            string folder = Path.Combine(baseFolder, "ServoERP", "WebView2");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            return await CoreWebView2Environment.CreateAsync(null, GetUserDataFolder(), null);
        }

        public static bool IsRuntimeAvailable(out string version, out string friendlyMessage)
        {
            version = string.Empty;
            friendlyMessage = string.Empty;

            try
            {
                version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrWhiteSpace(version))
                    throw new WebView2RuntimeNotFoundException("WebView2 runtime version was empty.");

                return true;
            }
            catch (WebView2RuntimeNotFoundException ex)
            {
                AppRuntime.LogException("WebView2RuntimeHelper.IsRuntimeAvailable", ex);
                friendlyMessage =
                    "The Geo Intelligence map needs the Microsoft Edge WebView2 Runtime.\r\n\r\n" +
                    "Download: " + DownloadUrl;
                return false;
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("WebView2RuntimeHelper.IsRuntimeAvailable", ex);
                friendlyMessage =
                    "The WebView2 map component is not ready on this PC yet.\r\n\r\n" +
                    "Download: " + DownloadUrl;
                return false;
            }
        }

        public static void ShowFriendlyMissingRuntimeMessage(IWin32Window owner)
        {
            if (IsRuntimeAvailable(out _, out string message))
                return;

            bool result = ServoERP.Infrastructure.ServoConfirmDialog.Show(
                owner,
                "Open WebView2 download page?",
                message + "\r\n\r\nServoERP will open the Microsoft WebView2 Runtime download page in your browser.");

            if (!result)
                return;

            try
            {
                Process.Start(DownloadUrl);
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("WebView2RuntimeHelper.OpenDownload", ex);
                MessageBox.Show(
                    owner,
                    "Please open this link in a browser:\r\n\r\n" + DownloadUrl,
                    BrandingService.WindowTitle("WebView2 Download"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
