using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class HtmlPreviewDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly WebBrowser _browser = new WebBrowser();
        private readonly string _html;
        private readonly string _title;
        private readonly string _tempHtmlPath;
        private readonly bool _fitDocumentToPreview;

        public HtmlPreviewDialog(string title, string html)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            _title = string.IsNullOrWhiteSpace(title) ? "Document Preview" : title;
            _html = string.IsNullOrWhiteSpace(html) ? "<html><body><p>No preview content.</p></body></html>" : html;
            _tempHtmlPath = Path.Combine(Path.GetTempPath(), "servo-preview-" + Guid.NewGuid().ToString("N") + ".html");

            Text = _title;
            _fitDocumentToPreview = _title.IndexOf("Quotation Preview", StringComparison.OrdinalIgnoreCase) >= 0;
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
            _browser.DocumentCompleted += BrowserDocumentCompleted;

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

        private void BrowserDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (!_fitDocumentToPreview || _browser.Document == null || _browser.Document.Body == null)
                return;

            try
            {
                int viewportWidth = Math.Max(1, _browser.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);
                int documentWidth = Math.Max(1, _browser.Document.Body.ScrollRectangle.Width);
                int zoomPercent = Math.Min(100, Math.Max(75, (int)Math.Floor((viewportWidth * 100m) / documentWidth)));
                _browser.Document.Body.Style = "zoom:" + zoomPercent.ToString() + "%; transform-origin: top center;";
            }
            catch
            {
            }
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
            HtmlPdfExportService.ExportHtmlToPdf(html, pdfPath);
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

