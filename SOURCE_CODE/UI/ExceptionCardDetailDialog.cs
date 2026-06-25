using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI.Controls;

namespace HVAC_Pro_Desktop.UI
{
    internal sealed class ExceptionCardDetail
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public List<string> Columns { get; } = new List<string>();
        public List<string[]> Rows { get; } = new List<string[]>();

        public static ExceptionCardDetail Create(string title, string subtitle, params string[] columns)
        {
            var detail = new ExceptionCardDetail { Title = title, Subtitle = subtitle };
            if (columns != null)
                detail.Columns.AddRange(columns);
            return detail;
        }

        public ExceptionCardDetail AddRow(params object[] values)
        {
            Rows.Add((values ?? new object[0]).Select(value => Convert.ToString(value) ?? string.Empty).ToArray());
            return this;
        }
    }

    internal sealed class ExceptionCardDetailDialog : ServoERP.Infrastructure.ServoFormBase
    {
        private readonly ExceptionCardDetail _detail;
        private readonly DataGridView _grid;

        private ExceptionCardDetailDialog(ExceptionCardDetail detail)
        {
            _detail = detail ?? ExceptionCardDetail.Create("Details", "No detail data available.", "Message").AddRow("No rows found.");
            Text = _detail.Title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1080, 680);
            MinimumSize = new Size(780, 520);
            BackColor = DS.BgPage;
            Padding = new Padding(18);

            var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = DS.BgPage };
            var title = new Label
            {
                Text = _detail.Title,
                Location = new Point(0, 0),
                Size = new Size(680, 28),
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = DS.Slate900,
                AutoEllipsis = true
            };
            var subtitle = new Label
            {
                Text = _detail.Subtitle,
                Location = new Point(1, 34),
                Size = new Size(760, 22),
                Font = new Font("Segoe UI", 8.7f),
                ForeColor = DS.Slate600,
                AutoEllipsis = true
            };
            var export = BuildAction("Export CSV", DS.Primary600, Color.White, DS.Primary600);
            export.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            export.Location = new Point(header.Width - 226, 12);
            export.Click += (s, e) => ExportCsv();
            var close = BuildAction("Close", Color.White, DS.Slate900, DS.BorderStrong);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(header.Width - 108, 12);
            close.Click += (s, e) => Close();
            header.Resize += (s, e) =>
            {
                title.Width = Math.Max(240, header.Width - 250);
                subtitle.Width = Math.Max(240, header.Width - 250);
                export.Location = new Point(header.Width - 226, 12);
                close.Location = new Point(header.Width - 108, 12);
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(export);
            header.Controls.Add(close);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeight = 36,
                RowTemplate = { Height = 30 },
                EnableHeadersVisualStyles = false
            };
            GridTheme.Apply(_grid);

            var shell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(1) };
            shell.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, shell.Width - 1, shell.Height - 1), 10))
                using (var pen = new Pen(DS.Border))
                    e.Graphics.DrawPath(pen, path);
            };
            DS.Rounded(shell, 10);
            shell.Controls.Add(_grid);

            Controls.Add(shell);
            Controls.Add(header);
            BindGrid();
        }

        public static void ShowFor(IWin32Window owner, ExceptionCardDetail detail)
        {
            using (var dialog = new ExceptionCardDetailDialog(detail))
                dialog.ShowDialog(owner);
        }

        private static Label BuildAction(string text, Color backColor, Color foreColor, Color borderColor)
        {
            var button = new Label
            {
                Text = string.Empty,
                Size = new Size(98, 34),
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 8.3f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            button.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = DS.RoundedRect(new Rectangle(0, 0, button.Width - 1, button.Height - 1), 7))
                using (var brush = new SolidBrush(backColor))
                using (var pen = new Pen(borderColor))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, text, button.Font, button.ClientRectangle, foreColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            return button;
        }

        private void BindGrid()
        {
            _grid.Columns.Clear();
            _grid.Rows.Clear();
            IEnumerable<string> columns = _detail.Columns.Count == 0 ? new List<string> { "Details" } : new List<string>(_detail.Columns);
            foreach (string column in columns)
                _grid.Columns.Add(column.Replace(" ", string.Empty), column);

            List<string[]> rows = _detail.Rows.Count == 0 ? new List<string[]> { new[] { "No rows found." } } : new List<string[]>(_detail.Rows);
            foreach (string[] sourceRow in rows)
            {
                object[] values = new object[_grid.Columns.Count];
                for (int i = 0; i < values.Length; i++)
                    values[i] = i < sourceRow.Length ? sourceRow[i] : string.Empty;
                _grid.Rows.Add(values);
            }
        }

        private void ExportCsv()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.FileName = (_detail.Title ?? "details").Replace(" ", "-").ToLowerInvariant() + "-" + DateTime.Today.ToString("yyyyMMdd") + ".csv";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                var csv = new StringBuilder();
                csv.AppendLine(string.Join(",", _detail.Columns.Select(Csv)));
                foreach (string[] row in _detail.Rows)
                    csv.AppendLine(string.Join(",", row.Select(Csv)));
                File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
            }
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
