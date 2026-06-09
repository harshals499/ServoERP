using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Services;

namespace HVAC_Pro_Desktop.UI
{
    public sealed class ErrorLogViewerForm : BaseForm
    {
        private readonly TextBox _logText;

        public ErrorLogViewerForm()
        {
            Text = BrandingService.WindowTitle(T("Error Logs"));
            Name = "ErrorLogViewerForm";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new System.Drawing.Size(820, 520);
            Size = new System.Drawing.Size(980, 650);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8),
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            Button close = MakeButton(T("Close"));
            close.Click += (s, e) => Close();

            Button clear = MakeButton(T("Clear Logs"));
            clear.Click += (s, e) => ClearLogs();

            Button refresh = MakeButton(T("Refresh"));
            refresh.Click += (s, e) => LoadLogs();

            toolbar.Controls.Add(close);
            toolbar.Controls.Add(clear);
            toolbar.Controls.Add(refresh);

            _logText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };

            Controls.Add(_logText);
            Controls.Add(toolbar);
            Load += (s, e) => LoadLogs();
        }

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = Math.Max(96, TextRenderer.MeasureText(text, System.Drawing.SystemFonts.MessageBoxFont).Width + 24),
                Height = 32,
                AutoEllipsis = false,
                AutoSize = true
            };
        }

        private void LoadLogs()
        {
            string dir = @"C:\HVAC_PRO_MSE\LOGS";
            Directory.CreateDirectory(dir);
            var files = Directory.GetFiles(dir, "CrashLog_*.txt")
                .Concat(Directory.GetFiles(dir, "crash-*.log"))
                .Concat(Directory.GetFiles(dir, "servoerp_errors.log"))
                .OrderByDescending(File.GetLastWriteTime)
                .Take(20)
                .ToList();

            var sb = new StringBuilder();
            if (files.Count == 0)
            {
                sb.AppendLine(T("No crash logs found."));
            }
            else
            {
                foreach (string file in files)
                {
                    sb.AppendLine("################################################################");
                    sb.AppendLine(Path.GetFileName(file));
                    sb.AppendLine("################################################################");
                    sb.AppendLine(File.ReadAllText(file));
                    sb.AppendLine();
                }
            }

            _logText.Text = sb.ToString();
            _logText.SelectionStart = 0;
            _logText.SelectionLength = 0;
        }

        private void ClearLogs()
        {
            if (MessageBox.Show(this, T("Clear local crash logs?"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string dir = @"C:\HVAC_PRO_MSE\LOGS";
            foreach (string file in Directory.GetFiles(dir, "CrashLog_*.txt").Concat(Directory.GetFiles(dir, "crash-*.log")).Concat(Directory.GetFiles(dir, "servoerp_errors.log")))
                File.Delete(file);

            LoadLogs();
        }

        private static string T(string key)
        {
            return LanguageManager.Get(key);
        }
    }
}
