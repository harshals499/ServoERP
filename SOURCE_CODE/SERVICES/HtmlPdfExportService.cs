using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace HVAC_Pro_Desktop.Services
{
    internal static class HtmlPdfExportService
    {
        private const int EXPORT_TIMEOUT_MS = 60000;

        /// <summary>Exports HTML to PDF using the installed Chromium browser engine.</summary>
        public static void ExportHtmlToPdf(string html, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("PDF output path is required.", nameof(outputPath));

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string tempRoot = Path.Combine(Path.GetTempPath(), "ServoERP_Pdf_" + Guid.NewGuid().ToString("N"));
            string tempHtml = Path.Combine(tempRoot, "document.html");
            string userDataDir = Path.Combine(tempRoot, "browser-profile");
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(userDataDir);

            try
            {
                File.WriteAllText(tempHtml, html ?? string.Empty, Encoding.UTF8);
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                string browserPath = FindPdfBrowser();
                if (string.IsNullOrWhiteSpace(browserPath))
                    throw new InvalidOperationException("Microsoft Edge or Google Chrome is required to generate PDF output.");

                string arguments =
                    "--headless --disable-gpu --disable-extensions --disable-background-networking " +
                    "--no-first-run --no-default-browser-check --run-all-compositor-stages-before-draw " +
                    "--no-pdf-header-footer --virtual-time-budget=1500 --user-data-dir=\"" + userDataDir + "\" " +
                    "--print-to-pdf=\"" + outputPath + "\" \"" + new Uri(tempHtml).AbsoluteUri + "\"";

                var psi = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = false
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                        throw new InvalidOperationException("PDF browser process could not start.");

                    bool exited = process.WaitForExit(EXPORT_TIMEOUT_MS);

                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("PDF generation timed out.");
                    }

                    WaitForPdfFile(outputPath);
                    if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
                        throw new InvalidOperationException("PDF generation did not complete. Browser exit code: " + process.ExitCode + ".");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, true);
                }
                catch
                {
                }
            }
        }

        private static void WaitForPdfFile(string outputPath)
        {
            long lastLength = -1;
            int stableReads = 0;
            DateTime until = DateTime.Now.AddSeconds(5);
            while (DateTime.Now < until)
            {
                if (File.Exists(outputPath))
                {
                    long length = new FileInfo(outputPath).Length;
                    if (length > 0 && length == lastLength)
                    {
                        stableReads++;
                        if (stableReads >= 2)
                            return;
                    }
                    else
                    {
                        stableReads = 0;
                    }
                    lastLength = length;
                }

                Thread.Sleep(150);
            }
        }

        private static string FindPdfBrowser()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe")
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                    return path;
            }

            return string.Empty;
        }
    }
}
