using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class JobPreviewPage : BaseUserControl
    {
        private static readonly Color White = Color.White;
        private static readonly Color Border = DS.Border;
        private static readonly Color PageBg = Color.FromArgb(243, 246, 251);
        private static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private static readonly Color Teal = DS.Teal600;
        private static readonly Color Blue = DS.Primary600;

        private readonly JobService _jobService = new JobService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly Label _lblTitle = new Label();
        private readonly Label _lblCompany = new Label();
        private readonly Label _lblMeta = new Label();
        private readonly Label _lblStatus = new Label();
        private readonly Label _lblLoading = new Label();
        private readonly Panel _browserHost = new Panel();

        private WebBrowser _browser;
        private JobDetailDto _detail;
        private string _currentHtml;
        private string _tempHtmlPath;
        private bool _loaded;

        public int JobId { get; set; }
        public Action<int> OnBackToJobs { get; set; }
        public Action<int> OnEditJob { get; set; }

        public JobPreviewPage()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            BuildLayout();
            Load += async (s, e) => await EnsureLoadedAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TryDeleteTempHtml();
                if (_browser != null)
                {
                    _browser.Dispose();
                    _browser = null;
                }
            }

            base.Dispose(disposing);
        }

        public async Task EnsureLoadedAsync()
        {
            if (_loaded || JobId <= 0 || IsDisposed)
                return;

            _loaded = true;
            await LoadPreviewAsync();
        }

        public async Task ReloadPreviewAsync()
        {
            if (JobId <= 0 || IsDisposed)
                return;

            _loaded = true;
            await LoadPreviewAsync();
        }

        private void BuildLayout()
        {
            Controls.Clear();

            Panel top = new Panel { Dock = DockStyle.Top, Height = 118, BackColor = White, Padding = new Padding(18, 16, 18, 16) };
            top.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, top.Height - 1, top.Width, top.Height - 1);

            Panel backHost = new Panel
            {
                Dock = DockStyle.Left,
                Width = 154,
                BackColor = White,
                Padding = new Padding(0, 18, 12, 18)
            };
            Button btnBack = MakeButton("<- Back to Jobs", White, TextPrimary, 132);
            btnBack.FlatAppearance.BorderColor = Border;
            btnBack.Location = new Point(0, 18);
            btnBack.Click += (s, e) => OnBackToJobs?.Invoke(JobId);
            backHost.Controls.Add(btnBack);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 116,
                Height = 186,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = White,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            Button btnEdit = MakeButton("Edit Job", Teal, White, 92);
            Button btnPrint = MakeButton("Print", Blue, White, 84);
            Button btnOpenPdf = MakeButton("Open PDF", White, TextPrimary, 90);
            Button btnSavePdf = MakeButton("Save PDF", White, TextPrimary, 90);
            Button btnRefresh = MakeButton("Refresh", White, TextPrimary, 86);

            btnOpenPdf.FlatAppearance.BorderColor = Border;
            btnSavePdf.FlatAppearance.BorderColor = Border;
            btnRefresh.FlatAppearance.BorderColor = Border;

            btnEdit.Click += (s, e) => OnEditJob?.Invoke(JobId);
            btnPrint.Click += (s, e) =>
            {
                if (_browser != null && !_browser.IsDisposed)
                    _browser.ShowPrintDialog();
            };
            btnOpenPdf.Click += (s, e) => OpenPdf();
            btnSavePdf.Click += (s, e) => SavePdf();
            btnRefresh.Click += async (s, e) => await ReloadPreviewAsync();

            btnEdit.Margin = new Padding(0, 0, 0, 6);
            btnPrint.Margin = new Padding(0, 0, 0, 6);
            btnOpenPdf.Margin = new Padding(0, 0, 0, 6);
            btnSavePdf.Margin = new Padding(0, 0, 0, 6);
            btnRefresh.Margin = new Padding(0);

            actions.Controls.AddRange(new Control[] { btnEdit, btnPrint, btnOpenPdf, btnSavePdf, btnRefresh });

            Panel titleHost = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(8, 2, 16, 0) };
            _lblTitle.Dock = DockStyle.Top;
            _lblTitle.Height = 30;
            _lblTitle.Font = new Font("Segoe UI", 17f, FontStyle.Bold);
            _lblTitle.ForeColor = TextPrimary;
            _lblTitle.Text = "Job Preview";
            _lblCompany.Dock = DockStyle.Top;
            _lblCompany.Height = 22;
            _lblCompany.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _lblCompany.ForeColor = Blue;
            _lblCompany.Text = DocumentBranding.DefaultCompanyName;
            _lblMeta.Dock = DockStyle.Top;
            _lblMeta.Height = 20;
            _lblMeta.Font = new Font("Segoe UI", 9f);
            _lblMeta.ForeColor = TextSecondary;
            _lblStatus.AutoSize = true;
            _lblStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _lblStatus.ForeColor = Teal;
            _lblStatus.BackColor = Color.FromArgb(232, 248, 241);
            _lblStatus.Padding = new Padding(10, 4, 10, 4);
            _lblStatus.Location = new Point(0, 72);
            titleHost.Controls.Add(_lblStatus);
            titleHost.Controls.Add(_lblMeta);
            titleHost.Controls.Add(_lblCompany);
            titleHost.Controls.Add(_lblTitle);

            top.Controls.Add(titleHost);
            top.Controls.Add(actions);
            top.Controls.Add(backHost);

            Panel shell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = PageBg };
            Panel previewCard = new Panel { Dock = DockStyle.Fill, BackColor = White, Padding = new Padding(14) };
            previewCard.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, previewCard.Width - 1, previewCard.Height - 1);

            _browserHost.Dock = DockStyle.Fill;
            _browserHost.BackColor = White;

            _lblLoading.Dock = DockStyle.Top;
            _lblLoading.Height = 26;
            _lblLoading.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _lblLoading.ForeColor = TextSecondary;
            _lblLoading.Text = "Loading job preview...";

            previewCard.Controls.Add(_browserHost);
            previewCard.Controls.Add(_lblLoading);
            shell.Controls.Add(previewCard);

            Controls.Add(shell);
            Controls.Add(top);
        }

        private async Task LoadPreviewAsync()
        {
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                SetLoading("Loading job preview...");
                var payload = await Task.Run(() =>
                {
                    JobDetailDto detailPayload = _jobService.GetJobDetail(JobId);
                    IndiaCompanySettings settingsPayload = _settingsService.GetIndiaCompanySettings();
                    string htmlPayload = detailPayload == null || detailPayload.Job == null
                        ? string.Empty
                        : JobPreviewDocumentBuilder.BuildServiceReportHtml(detailPayload, settingsPayload);
                    return new { Detail = detailPayload, Settings = settingsPayload, Html = htmlPayload };
                });

                if (payload.Detail == null || payload.Detail.Job == null)
                    throw new InvalidOperationException("Job not found.");

                _detail = payload.Detail;
                _currentHtml = payload.Html;
                BindHeader(payload.Detail, payload.Settings);
                SetLoading("Rendering preview...");
                AppRuntime.LogTiming("JobPreviewPage.LoadData", watch.ElapsedMilliseconds);
                BeginInvoke((Action)(() => RenderPreviewHtml(_currentHtml, watch)));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("JobPreviewPage.LoadPreviewAsync", ex);
                SetLoading("Unable to load preview.");
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Loading job preview", ex);
            }
        }

        private void RenderPreviewHtml(string html, Stopwatch watch)
        {
            if (IsDisposed)
                return;

            try
            {
                EnsureBrowserCreated();
                WritePreviewHtmlToDisk(html);
                _browser.Visible = true;
                _browser.Navigate(new Uri(_tempHtmlPath));
                AppRuntime.LogTiming("JobPreviewPage.NavigatePreview", watch == null ? 0 : watch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("JobPreviewPage.RenderPreviewHtml", ex);
                SetLoading("Unable to render preview.");
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Rendering job preview", ex);
            }
        }

        private void EnsureBrowserCreated()
        {
            if (_browser != null && !_browser.IsDisposed)
                return;

            _browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = true,
                Visible = false
            };
            _browser.DocumentCompleted += BrowserDocumentCompleted;
            _browserHost.Controls.Clear();
            _browserHost.Controls.Add(_browser);
        }

        private void BrowserDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (IsDisposed)
                return;

            SetLoading(string.Empty);
            if (_browser != null && !_browser.IsDisposed)
                _browser.Visible = true;
        }

        private void BindHeader(JobDetailDto detail, IndiaCompanySettings settings)
        {
            Job job = detail.Job;
            _lblTitle.Text = string.IsNullOrWhiteSpace(job.JobNumber) ? "Job Preview" : job.JobNumber + " Preview";
            _lblCompany.Text = ResolveCompanyName(settings);
            _lblMeta.Text = string.Join("   |   ", new[]
            {
                Safe(detail.Client == null ? null : detail.Client.CompanyName, "Client not linked"),
                Safe(detail.Site == null ? null : detail.Site.SiteName, "Site not linked"),
                job.ScheduledDate == default(DateTime) ? "Date not scheduled" : "Scheduled " + IndiaFormatHelper.FormatDate(job.ScheduledDate)
            });
            _lblStatus.Text = Safe(FirstNonEmpty(job.PipelineStatus, job.Status), "Created");
        }

        private static string ResolveCompanyName(IndiaCompanySettings settings)
        {
            string configured = settings == null ? string.Empty : settings.CompanyName;
            if (string.IsNullOrWhiteSpace(configured) || string.Equals(configured.Trim(), BrandingService.AppName, StringComparison.OrdinalIgnoreCase))
                return DocumentBranding.DefaultCompanyName;

            return configured.Trim();
        }

        private void SetLoading(string text)
        {
            _lblLoading.Text = text;
            _lblLoading.Visible = !string.IsNullOrWhiteSpace(text);
        }

        private void WritePreviewHtmlToDisk(string html)
        {
            TryDeleteTempHtml();
            string directory = Path.Combine(Path.GetTempPath(), "ServoERP", "JobPreview");
            Directory.CreateDirectory(directory);
            _tempHtmlPath = Path.Combine(directory, BuildSafeFileName((_detail != null && _detail.Job != null ? _detail.Job.JobNumber : "job-preview")) + "-" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(_tempHtmlPath, html ?? string.Empty, Encoding.UTF8);
        }

        private void TryDeleteTempHtml()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_tempHtmlPath) && File.Exists(_tempHtmlPath))
                    File.Delete(_tempHtmlPath);
            }
            catch
            {
            }
            finally
            {
                _tempHtmlPath = null;
            }
        }

        private void OpenPdf()
        {
            if (_detail == null || string.IsNullOrWhiteSpace(_currentHtml))
                return;

            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "ServoERP", "JobPreview");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, BuildSafeFileName((_detail.Job == null ? null : _detail.Job.JobNumber) ?? ("job-" + JobId)) + ".pdf");
                HtmlPreviewDialog.ExportHtmlToPdf(_currentHtml, path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Opening preview PDF", ex);
            }
        }

        private void SavePdf()
        {
            if (string.IsNullOrWhiteSpace(_currentHtml))
                return;

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "PDF Files (*.pdf)|*.pdf";
                dialog.DefaultExt = "pdf";
                dialog.AddExtension = true;
                dialog.FileName = BuildSafeFileName((_detail != null && _detail.Job != null ? _detail.Job.JobNumber : "job-preview")) + ".pdf";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    HtmlPreviewDialog.ExportHtmlToPdf(_currentHtml, dialog.FileName);
                    MessageBox.Show(this, "PDF saved to " + dialog.FileName, "Job Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Jobs"), "Saving preview PDF", ex);
                }
            }
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }

        private static string BuildSafeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "job-preview" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '-');
            return safe;
        }

        private static Button MakeButton(string text, Color backColor, Color foreColor, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(6, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 1;
            return button;
        }
    }
}
