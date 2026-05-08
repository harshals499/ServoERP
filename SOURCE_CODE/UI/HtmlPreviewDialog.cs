using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class HtmlPreviewDialog : Form
    {
        private readonly WebBrowser _browser = new WebBrowser();
        private readonly string _html;
        private readonly string _title;
        private readonly string _tempHtmlPath;

        public HtmlPreviewDialog(string title, string html)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            _title = string.IsNullOrWhiteSpace(title) ? "Document Preview" : title;
            _html = string.IsNullOrWhiteSpace(html) ? "<html><body><p>No preview content.</p></body></html>" : html;
            _tempHtmlPath = Path.Combine(Path.GetTempPath(), "servo-preview-" + Guid.NewGuid().ToString("N") + ".html");

            Text = _title;
            Width = 1120;
            Height = 780;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
            Button print = MakeButton("Print", 10, Color.FromArgb(39, 174, 96));
            Button savePdf = MakeButton("Save PDF", 108, Color.FromArgb(37, 99, 235));
            Button saveHtml = MakeButton("Save HTML", 220, Color.FromArgb(71, 85, 105));
            Button refresh = MakeButton("Refresh", 342, Color.FromArgb(99, 102, 241));

            print.Click += (s, e) => _browser.ShowPrintDialog();
            savePdf.Click += SavePdf;
            saveHtml.Click += SaveHtml;
            refresh.Click += (s, e) => LoadPreview();
            toolbar.Controls.AddRange(new Control[] { print, savePdf, saveHtml, refresh });

            _browser.Dock = DockStyle.Fill;
            _browser.ScriptErrorsSuppressed = true;

            Controls.Add(_browser);
            Controls.Add(toolbar);
            Shown += (s, e) => LoadPreview();
            FormClosed += (s, e) => TryDeleteTempFile();
        }

        private static Button MakeButton(string text, int left, Color color)
        {
            Button button = new Button
            {
                Text = text,
                Width = text.Length > 8 ? 104 : 90,
                Height = 28,
                Left = left,
                Top = 8,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void LoadPreview()
        {
            File.WriteAllText(_tempHtmlPath, _html);
            _browser.Navigate(new Uri(_tempHtmlPath));
        }

        private void SaveHtml(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "HTML Files (*.html)|*.html";
                dialog.DefaultExt = "html";
                dialog.AddExtension = true;
                dialog.FileName = MakeSafeFileName(_title) + ".html";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                File.WriteAllText(dialog.FileName, _html);
                MessageBox.Show("Preview saved to " + dialog.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SavePdf(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PDF Files (*.pdf)|*.pdf";
                dialog.DefaultExt = "pdf";
                dialog.AddExtension = true;
                dialog.FileName = MakeSafeFileName(_title) + ".pdf";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                ExportHtmlToPdf(_html, dialog.FileName);
                MessageBox.Show("PDF saved to " + dialog.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static void ExportHtmlToPdf(string html, string pdfPath)
        {
            string tempHtml = Path.Combine(Path.GetTempPath(), "servo-pdf-" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(tempHtml, html ?? string.Empty);
            try
            {
                string browserPath = FindPdfBrowser();
                if (string.IsNullOrWhiteSpace(browserPath))
                    throw new Exception("Microsoft Edge or Google Chrome is required to generate PDF output.");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = "--headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf=\"" + pdfPath + "\" \"" + new Uri(tempHtml).AbsoluteUri + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit(20000);
                    if (!File.Exists(pdfPath))
                        throw new Exception("PDF generation did not complete.");
                }
            }
            finally
            {
                try { if (File.Exists(tempHtml)) File.Delete(tempHtml); } catch { }
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
                if (File.Exists(path))
                    return path;
            return null;
        }

        private static string MakeSafeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "document-preview" : value;
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '-');
            return safe.Trim();
        }

        private void TryDeleteTempFile()
        {
            try
            {
                if (File.Exists(_tempHtmlPath))
                    File.Delete(_tempHtmlPath);
            }
            catch { }
        }
    }
}
