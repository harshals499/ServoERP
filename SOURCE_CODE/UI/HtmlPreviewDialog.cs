using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class HtmlPreviewDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly WebBrowser _browser = new WebBrowser();
        private readonly string _originalHtml;
        private string _workingHtml;
        private readonly string _title;
        private readonly string _tempHtmlPath;
        private readonly bool _fitDocumentToPreview;
        private Button _editButton;
        private Label _editStatus;
        private bool _editMode;

        public HtmlPreviewDialog()
            : this("Quotation Preview - Visual Test", "<html><head><style>body{font-family:Segoe UI;margin:28px}.doc{max-width:760px;margin:auto;border:1px solid #94a3b8;padding:24px}table{width:100%;border-collapse:collapse}th,td{border:1px solid #64748b;padding:8px}h2{text-align:center}</style></head><body><div class='doc'><h2>Editable Document Preview</h2><p>Click Edit to change any text or table value before saving the PDF.</p><table><tr><th>Description</th><th>Qty</th><th>Rate</th></tr><tr><td>HVAC service</td><td>1</td><td>10,000.00</td></tr></table><p>Use Add Signature to place a saved signature at the cursor.</p></div></body></html>")
        {
        }

        public HtmlPreviewDialog(string title, string html)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            _title = string.IsNullOrWhiteSpace(title) ? "Document Preview" : title;
            _originalHtml = string.IsNullOrWhiteSpace(html) ? "<html><body><p>No preview content.</p></body></html>" : html;
            _workingHtml = _originalHtml;
            _tempHtmlPath = Path.Combine(Path.GetTempPath(), "servo-preview-" + Guid.NewGuid().ToString("N") + ".html");

            Text = _title;
            _fitDocumentToPreview = _title.IndexOf("Quotation Preview", StringComparison.OrdinalIgnoreCase) >= 0;
            Width = 1120;
            Height = 780;
            MinimumSize = new Size(920, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;

            Panel toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
            Button print = MakeButton("Print", 10, Color.FromArgb(39, 174, 96));
            Button savePdf = MakeButton("Save PDF", 108, Color.FromArgb(37, 99, 235));
            Button saveHtml = MakeButton("Save HTML", 220, Color.FromArgb(71, 85, 105));
            _editButton = MakeButton("Edit", 342, Color.FromArgb(217, 119, 6));
            Button addSignature = MakeButton("Add Signature", 440, Color.FromArgb(124, 58, 237));
            Button reset = MakeButton("Reset", 562, Color.FromArgb(100, 116, 139));
            _editStatus = new Label { AutoSize = true, Left = 664, Top = 14, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(71, 85, 105), Text = "Preview mode" };

            print.Click += (s, e) => { if (_editMode) SetEditMode(false); else CaptureWorkingHtml(); _browser.ShowPrintDialog(); };
            savePdf.Click += SavePdf;
            saveHtml.Click += SaveHtml;
            _editButton.Click += (s, e) => SetEditMode(!_editMode);
            addSignature.Click += AddSignature;
            reset.Click += ResetPreview;
            toolbar.Controls.AddRange(new Control[] { print, savePdf, saveHtml, _editButton, addSignature, reset, _editStatus });

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
            File.WriteAllText(_tempHtmlPath, _workingHtml);
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
            CaptureWorkingHtml();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "HTML Files (*.html)|*.html";
                dialog.DefaultExt = "html";
                dialog.AddExtension = true;
                dialog.FileName = MakeSafeFileName(_title) + ".html";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                File.WriteAllText(dialog.FileName, _workingHtml);
                MessageBox.Show("Preview saved to " + dialog.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SavePdf(object sender, EventArgs e)
        {
            CaptureWorkingHtml();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PDF Files (*.pdf)|*.pdf";
                dialog.DefaultExt = "pdf";
                dialog.AddExtension = true;
                dialog.FileName = MakeSafeFileName(_title) + ".pdf";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                ExportHtmlToPdf(_workingHtml, dialog.FileName);
                MessageBox.Show("PDF saved to " + dialog.FileName, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SetEditMode(bool enabled)
        {
            if (_browser.Document == null || _browser.Document.Body == null)
                return;

            if (!enabled)
                CaptureWorkingHtml();
            _editMode = enabled;
            _browser.Document.Body.SetAttribute("contentEditable", enabled ? "true" : "false");
            _browser.Document.Body.Style = (_browser.Document.Body.Style ?? string.Empty)
                + (enabled ? ";outline:3px solid #f59e0b;outline-offset:-3px;" : ";outline:none;");
            _editButton.Text = enabled ? "Done" : "Edit";
            _editButton.BackColor = enabled ? Color.FromArgb(22, 163, 74) : Color.FromArgb(217, 119, 6);
            _editStatus.Text = enabled ? "Editing: click text or a table cell and type" : "Preview mode";
            _editStatus.ForeColor = enabled ? Color.FromArgb(22, 101, 52) : Color.FromArgb(71, 85, 105);
            if (enabled)
                _browser.Focus();
        }

        private void AddSignature(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose a signature image";
                dialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    byte[] pngBytes;
                    using (var source = Image.FromFile(dialog.FileName))
                    using (var bitmap = new Bitmap(source))
                    using (var stream = new MemoryStream())
                    {
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        pngBytes = stream.ToArray();
                    }

                    string savedPath = DocumentBranding.GetUserSignaturePath();
                    Directory.CreateDirectory(Path.GetDirectoryName(savedPath));
                    File.WriteAllBytes(savedPath, pngBytes);
                    string dataUri = "data:image/png;base64," + Convert.ToBase64String(pngBytes);
                    InsertSignatureHtml("<span class='servo-added-signature' contenteditable='false' style='display:inline-block;min-width:150px;padding:8px;text-align:center'><img src='" + dataUri + "' alt='Signature' style='display:block;max-width:190px;max-height:80px;margin:0 auto' /></span>");
                    CaptureWorkingHtml();
                    _editStatus.Text = "Signature added; select it and press Delete to remove";
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("HtmlPreviewDialog.AddSignature", ex);
                    MessageBox.Show(this, "The signature could not be added. Choose a valid PNG or JPG image and try again.", "Signature Not Added", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void InsertSignatureHtml(string html)
        {
            if (_browser.Document == null || _browser.Document.Body == null)
                return;
            if (!_editMode)
                SetEditMode(true);
            string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html));
            string script = "(function(){var h=decodeURIComponent(escape(window.atob('" + encoded + "')));if(document.selection&&document.selection.createRange){document.selection.createRange().pasteHTML(h);}else{document.body.insertAdjacentHTML('beforeEnd',h);}})();";
            _browser.Document.InvokeScript("eval", new object[] { script });
        }

        private void ResetPreview(object sender, EventArgs e)
        {
            if (!ServoERP.Infrastructure.ServoConfirmDialog.Show(this, "Discard all preview edits and added signatures?", "The saved business record will not be changed."))
                return;
            _workingHtml = _originalHtml;
            _editMode = false;
            _editButton.Text = "Edit";
            _editButton.BackColor = Color.FromArgb(217, 119, 6);
            _editStatus.Text = "Preview mode";
            LoadPreview();
        }

        private void CaptureWorkingHtml()
        {
            try
            {
                if (_browser.Document == null)
                    return;
                HtmlElement root = _browser.Document.GetElementsByTagName("HTML").Cast<HtmlElement>().FirstOrDefault();
                if (root == null || string.IsNullOrWhiteSpace(root.OuterHtml))
                    return;
                string captured = root.OuterHtml;
                captured = Regex.Replace(captured, "\\scontentEditable=(?:\\\"true\\\"|\\\"false\\\"|true|false)", string.Empty, RegexOptions.IgnoreCase);
                captured = Regex.Replace(captured, "outline\\s*:\\s*[^;\\\"']*;?", string.Empty, RegexOptions.IgnoreCase);
                captured = Regex.Replace(captured, "outline-offset\\s*:\\s*[^;\\\"']*;?", string.Empty, RegexOptions.IgnoreCase);
                _workingHtml = captured;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("HtmlPreviewDialog.CaptureWorkingHtml", ex);
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
            catch (Exception ex)
            {
                AppLogger.LogError("HtmlPreviewDialog.TryDeleteTempFile", ex);
            }
        }
    }
}

