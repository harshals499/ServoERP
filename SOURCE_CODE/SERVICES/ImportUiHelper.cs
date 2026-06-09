using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OfficeOpenXml;

namespace HVAC_Pro_Desktop.Services
{
    public static class ImportUiHelper
    {
        private static readonly ExcelImportService Service = new ExcelImportService();
        private static readonly MasterDataIngestionPipeline Pipeline = new MasterDataIngestionPipeline();

        public static void RunImport(IWin32Window owner = null)
        {
            RunImportInternal(null, null, owner, null);
        }

        public static void RunImport(ExcelImportModule module, IWin32Window owner = null)
        {
            RunImportInternal(module, null, owner, null);
        }

        public static void RunImport(ExcelImportModule module, IWin32Window owner, string quotationImportDirection)
        {
            RunImportInternal(module, null, owner, null, quotationImportDirection);
        }

        public static void RunImport(ExcelImportModule module, IWin32Window owner, Action<AutomatedImportResult> afterSuccess)
        {
            RunImportInternal(module, null, owner, afterSuccess);
        }

        public static void ShowDirectionalImportMenu(Control anchor, ExcelImportModule module, IWin32Window owner = null)
        {
            if (anchor == null)
            {
                RunImport(module, owner);
                return;
            }

            ContextMenuStrip menu = new ContextMenuStrip { ShowImageMargin = false };
            AddDirectionalImportItem(menu, module, owner, "Sent to Suppliers");
            AddDirectionalImportItem(menu, module, owner, "Received from Suppliers");
            AddDirectionalImportItem(menu, module, owner, "Sent to Clients");
            AddDirectionalImportItem(menu, module, owner, "Received from Clients");
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Auto Import", null, (s, e) => RunImport(module, owner));
            menu.Show(anchor, new Point(0, anchor.Height + 2));
        }

        private static void AddDirectionalImportItem(ContextMenuStrip menu, ExcelImportModule module, IWin32Window owner, string label)
        {
            menu.Items.Add(label, null, (s, e) =>
            {
                if (module == ExcelImportModule.Quotations)
                    RunImport(module, owner, label);
                else
                    RunImport(module, owner);
            });
        }

        public static void RunMappedImport(ExcelImportModule module, IWin32Window owner = null)
        {
            RunImportInternal(module, null, owner, null);
        }

        public static void RunMappedImport(ExcelImportModule module, string filePath, IWin32Window owner = null)
        {
            RunImportInternal(module, filePath, owner, null);
        }

        public static void DownloadTemplate(ExcelImportModule module, IWin32Window owner = null)
        {
            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                    dialog.FileName = module + "_Template.xlsx";
                    if (dialog.ShowDialog(owner) != DialogResult.OK)
                        return;

                    Service.CreateTemplate(module, dialog.FileName);
                    MessageBox.Show(owner, "Template saved to " + dialog.FileName, "Template Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ImportUiHelper.DownloadTemplate", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Downloading import template", ex);
                MessageBox.Show(owner, "Template could not be generated right now. Please try again.", "Template Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static void RunImportFile(string filePath, ExcelImportModule? preferredModule = null, IWin32Window owner = null)
        {
            RunImportInternal(preferredModule, filePath, owner, null);
        }

        public static void RunImportFile(string filePath, ExcelImportModule? preferredModule, IWin32Window owner, string quotationImportDirection)
        {
            RunImportInternal(preferredModule, filePath, owner, null, quotationImportDirection);
        }

        private static void RunImportInternal(ExcelImportModule? preferredModule, string filePath, IWin32Window owner, Action<AutomatedImportResult> afterSuccess, string quotationImportDirection = null)
        {
            try
            {
                string selectedFile = filePath;
                bool isQuotationImport = preferredModule.HasValue && preferredModule.Value == ExcelImportModule.Quotations;
                if (isQuotationImport && string.IsNullOrWhiteSpace(quotationImportDirection))
                {
                    quotationImportDirection = ShowQuotationImportDirectionDialog(owner);
                    if (string.IsNullOrWhiteSpace(quotationImportDirection))
                        return;
                }

                if (string.IsNullOrWhiteSpace(selectedFile))
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Filter = "Excel Workbook (*.xlsx;*.xls)|*.xlsx;*.xls";
                        dialog.Title = preferredModule.HasValue ? "Import " + preferredModule.Value : "Import Excel";
                        if (dialog.ShowDialog(owner) != DialogResult.OK)
                            return;
                        selectedFile = dialog.FileName;
                    }
                }

                AutomatedImportResult result = Pipeline.ImportFile(selectedFile, preferredModule, quotationImportDirection);
                ShowAutomatedResult(owner, result);
                if (result != null && result.SuccessCount > 0 && afterSuccess != null)
                    afterSuccess(result);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ImportUiHelper.RunImportInternal", ex);
                AppRuntime.ShowRecoverableError(BrandingService.WindowTitle("Master Data"), "Importing Excel", ex);
                MessageBox.Show(owner, "Import could not complete. Check the workbook and try again.", "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void ShowAutomatedResult(IWin32Window owner, AutomatedImportResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(result.SummaryTitle ?? "Import complete.");
            builder.AppendLine("Detected data type: " + result.DetectedModule);
            builder.AppendLine("Worksheet used: " + result.DetectedSheetName);
            builder.AppendLine("Confidence: " + result.DetectionConfidence + "%");
            builder.AppendLine("Imported or refreshed: " + result.SuccessCount + " rows");
            builder.AppendLine("Skipped safely: " + result.SkippedCount + " rows");

            if (result.UserMessages.Any())
            {
                builder.AppendLine();
                foreach (string message in result.UserMessages.Take(6))
                    builder.AppendLine("- " + message);
            }

            if (result.CreatedDefaults.Any())
            {
                builder.AppendLine();
                builder.AppendLine("Automatic fixes:");
                foreach (string detail in result.CreatedDefaults.Take(6))
                    builder.AppendLine("- " + detail);
            }

            if (result.Errors.Any())
            {
                builder.AppendLine();
                builder.AppendLine("Skipped rows:");
                foreach (string error in result.Errors.Take(12))
                    builder.AppendLine("- " + error);
            }

            MessageBox.Show(owner, builder.ToString(), "Import Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string ShowQuotationImportDirectionDialog(IWin32Window owner)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Quotation Import Type";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Size = new Size(460, 230);
                dialog.MinimumSize = new Size(420, 210);
                dialog.BackColor = Color.White;
                dialog.Font = new Font("Segoe UI", 9f);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                Label title = new Label
                {
                    Text = "Select quotation import type",
                    Dock = DockStyle.Top,
                    Height = 44,
                    Padding = new Padding(18, 12, 18, 0),
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42)
                };

                Label hint = new Label
                {
                    Text = "ServoERP will place imported quotations into the matching dashboard workflow card.",
                    Dock = DockStyle.Top,
                    Height = 42,
                    Padding = new Padding(18, 0, 18, 4),
                    ForeColor = Color.FromArgb(71, 85, 105)
                };

                ComboBox selector = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Left = 18,
                    Top = 92,
                    Width = 406,
                    Height = 34
                };
                selector.Items.Add("Received from Suppliers");
                selector.Items.Add("Sent to Suppliers");
                selector.Items.Add("Sent to Clients");
                selector.Items.Add("Received from Clients");
                selector.SelectedIndex = 2;

                Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(18, 10, 18, 10) };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 96, Height = 34, Left = footer.Width - 218, Top = 12, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                Button import = new Button { Text = "Import", DialogResult = DialogResult.OK, Width = 104, Height = 34, Left = footer.Width - 116, Top = 12, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                import.FlatAppearance.BorderSize = 0;
                footer.Controls.Add(cancel);
                footer.Controls.Add(import);

                dialog.Controls.Add(selector);
                dialog.Controls.Add(footer);
                dialog.Controls.Add(hint);
                dialog.Controls.Add(title);
                dialog.AcceptButton = import;
                dialog.CancelButton = cancel;

                return dialog.ShowDialog(owner) == DialogResult.OK
                    ? Convert.ToString(selector.SelectedItem)
                    : null;
            }
        }

        private static List<string> ReadSourceHeaders(string filePath)
        {
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets.FirstOrDefault();
                if (sheet == null || sheet.Dimension == null)
                    throw new InvalidOperationException("The selected workbook has no readable sheet.");

                var headers = new List<string>();
                for (int col = 1; col <= sheet.Dimension.End.Column; col++)
                {
                    string header = Convert.ToString(sheet.Cells[1, col].Value)?.Trim();
                    if (!string.IsNullOrWhiteSpace(header))
                        headers.Add(header);
                }

                if (headers.Count == 0)
                    throw new InvalidOperationException("The first row must contain column headers.");

                return headers;
            }
        }

        private static string CreateMappedWorkbook(ExcelImportModule module, string sourceFile, IDictionary<string, string> mappings)
        {
            string[] targetHeaders = ExcelImportService.GetHeaders(module);
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImportMap");
            Directory.CreateDirectory(folder);
            string targetFile = Path.Combine(folder, module + "_Mapped_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

            using (var sourcePackage = new ExcelPackage(new FileInfo(sourceFile)))
            using (var targetPackage = new ExcelPackage())
            {
                ExcelWorksheet sourceSheet = sourcePackage.Workbook.Worksheets.FirstOrDefault();
                if (sourceSheet == null || sourceSheet.Dimension == null)
                    throw new InvalidOperationException("The selected workbook has no readable sheet.");

                Dictionary<string, int> sourceMap = BuildSourceMap(sourceSheet);
                ExcelWorksheet targetSheet = targetPackage.Workbook.Worksheets.Add(module.ToString());
                for (int col = 0; col < targetHeaders.Length; col++)
                    targetSheet.Cells[1, col + 1].Value = targetHeaders[col];

                for (int row = 2; row <= sourceSheet.Dimension.End.Row; row++)
                {
                    for (int col = 0; col < targetHeaders.Length; col++)
                    {
                        string targetHeader = targetHeaders[col];
                        string sourceHeader;
                        if (mappings != null && mappings.TryGetValue(targetHeader, out sourceHeader) && !string.IsNullOrWhiteSpace(sourceHeader))
                        {
                            int sourceCol;
                            if (sourceMap.TryGetValue(sourceHeader, out sourceCol))
                                targetSheet.Cells[row, col + 1].Value = sourceSheet.Cells[row, sourceCol].Value;
                        }
                    }
                }

                if (targetSheet.Dimension != null)
                    targetSheet.Cells[targetSheet.Dimension.Address].AutoFitColumns();
                targetPackage.SaveAs(new FileInfo(targetFile));
            }

            return targetFile;
        }

        private static Dictionary<string, int> BuildSourceMap(ExcelWorksheet sheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= sheet.Dimension.End.Column; col++)
            {
                string header = Convert.ToString(sheet.Cells[1, col].Value)?.Trim();
                if (!string.IsNullOrWhiteSpace(header) && !map.ContainsKey(header))
                    map[header] = col;
            }

            return map;
        }

        private sealed class ColumnMappingDialog : Form
        {
            private readonly ExcelImportModule _module;
            private readonly List<string> _sourceHeaders;
            private readonly Dictionary<string, ComboBox> _selectors = new Dictionary<string, ComboBox>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> Mappings { get; private set; }

            public ColumnMappingDialog(ExcelImportModule module, List<string> sourceHeaders)
            {
                _module = module;
                _sourceHeaders = sourceHeaders ?? new List<string>();
                Mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Text = "Map / Validate " + module;
                StartPosition = FormStartPosition.CenterParent;
                Size = new Size(720, 620);
                MinimumSize = new Size(620, 520);
                BackColor = Color.White;
                Font = new Font("Segoe UI", 9f);
                Build();
            }

            private void Build()
            {
                Controls.Add(new Label
                {
                    Text = "Map client Excel columns to ServoERP " + _module + " fields.",
                    Dock = DockStyle.Top,
                    Height = 44,
                    Padding = new Padding(18, 10, 18, 0),
                    Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(15, 23, 42)
                });

                Controls.Add(new Label
                {
                    Text = "Required fields are marked with *. Review the auto-selected columns, then click Validate and Import.",
                    Dock = DockStyle.Top,
                    Height = 34,
                    Padding = new Padding(18, 0, 18, 8),
                    ForeColor = Color.FromArgb(71, 85, 105)
                });

                Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(18, 10, 18, 10), BackColor = Color.FromArgb(248, 250, 252) };
                Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 96, Height = 34, Left = footer.Width - 216, Top = 12, Anchor = AnchorStyles.Top | AnchorStyles.Right };
                Button ok = new Button { Text = "Validate and Import", Width = 150, Height = 34, Left = footer.Width - 168, Top = 12, Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                ok.FlatAppearance.BorderSize = 0;
                ok.Click += (s, e) => SaveAndClose();
                footer.Controls.Add(cancel);
                footer.Controls.Add(ok);
                Controls.Add(footer);

                Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), AutoScroll = true, BackColor = Color.White };
                TableLayoutPanel grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 3,
                    RowCount = 1,
                    BackColor = Color.White
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86f));

                string[] targetHeaders = ExcelImportService.GetHeaders(_module);
                string[] requiredHeaders = ExcelImportService.GetRequiredHeaders(_module);
                for (int i = 0; i < targetHeaders.Length; i++)
                    AddMappingRow(grid, i, targetHeaders[i], requiredHeaders.Contains(targetHeaders[i], StringComparer.OrdinalIgnoreCase));

                body.Controls.Add(grid);
                Controls.Add(body);
            }

            private void AddMappingRow(TableLayoutPanel grid, int row, string targetHeader, bool required)
            {
                grid.RowCount = row + 1;
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));

                Label label = new Label
                {
                    Text = targetHeader + (required ? " *" : string.Empty),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = required ? Color.FromArgb(15, 23, 42) : Color.FromArgb(71, 85, 105)
                };

                ComboBox selector = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
                selector.Items.Add(string.Empty);
                foreach (string sourceHeader in _sourceHeaders)
                    selector.Items.Add(sourceHeader);
                string auto = FindBestSource(targetHeader);
                selector.SelectedItem = auto ?? string.Empty;

                Label status = new Label
                {
                    Text = string.IsNullOrWhiteSpace(auto) ? "Map" : "Auto",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = string.IsNullOrWhiteSpace(auto) ? Color.FromArgb(180, 83, 9) : Color.FromArgb(22, 101, 52)
                };
                selector.SelectedIndexChanged += (s, e) =>
                {
                    bool hasValue = !string.IsNullOrWhiteSpace(Convert.ToString(selector.SelectedItem));
                    status.Text = hasValue ? "Mapped" : "Map";
                    status.ForeColor = hasValue ? Color.FromArgb(22, 101, 52) : Color.FromArgb(180, 83, 9);
                };

                _selectors[targetHeader] = selector;
                grid.Controls.Add(label, 0, row);
                grid.Controls.Add(selector, 1, row);
                grid.Controls.Add(status, 2, row);
            }

            private string FindBestSource(string targetHeader)
            {
                foreach (string candidate in CandidateNames(_module, targetHeader))
                {
                    string normalizedCandidate = NormalizeHeader(candidate);
                    string exact = _sourceHeaders.FirstOrDefault(h => NormalizeHeader(h) == normalizedCandidate);
                    if (!string.IsNullOrWhiteSpace(exact))
                        return exact;
                }

                foreach (string sourceHeader in _sourceHeaders)
                {
                    string normalizedSource = NormalizeHeader(sourceHeader);
                    foreach (string candidate in CandidateNames(_module, targetHeader))
                    {
                        string normalizedCandidate = NormalizeHeader(candidate);
                        if (normalizedSource.Contains(normalizedCandidate) || normalizedCandidate.Contains(normalizedSource))
                            return sourceHeader;
                    }
                }

                return null;
            }

            private void SaveAndClose()
            {
                string[] requiredHeaders = ExcelImportService.GetRequiredHeaders(_module);
                var missing = new List<string>();
                Mappings.Clear();
                foreach (KeyValuePair<string, ComboBox> entry in _selectors)
                {
                    string selected = Convert.ToString(entry.Value.SelectedItem) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(selected))
                        Mappings[entry.Key] = selected;
                    if (requiredHeaders.Contains(entry.Key, StringComparer.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(selected))
                        missing.Add(entry.Key);
                }

                if (missing.Count > 0)
                {
                    MessageBox.Show(this, "Please map required fields first:\r\n" + string.Join("\r\n", missing), "Required Mapping", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private static IEnumerable<string> CandidateNames(ExcelImportModule module, string targetHeader)
        {
            yield return targetHeader;

            if (module != ExcelImportModule.Inventory)
                yield break;

            switch (targetHeader)
            {
                case "ItemName":
                    foreach (string value in new[] { "Item Name", "Name", "Material", "Description", "Part Name", "Product Name" }) yield return value;
                    break;
                case "Category":
                    foreach (string value in new[] { "Group", "Item Group", "Material Group", "Type" }) yield return value;
                    break;
                case "CurrentStock":
                    foreach (string value in new[] { "Current Stock", "Stock", "Qty", "Quantity", "Closing Stock", "Balance Qty" }) yield return value;
                    break;
                case "Unit":
                    foreach (string value in new[] { "UOM", "Unit Of Measure", "Measure" }) yield return value;
                    break;
                case "LastPurchaseRate":
                    foreach (string value in new[] { "Last Purchase Rate", "Purchase Rate", "Rate", "Unit Price", "Price" }) yield return value;
                    break;
                case "ReorderLevel":
                    foreach (string value in new[] { "Reorder Level", "Min Stock", "Minimum Stock", "Safety Stock" }) yield return value;
                    break;
                case "StockValue":
                    foreach (string value in new[] { "Stock Value", "Value", "Amount" }) yield return value;
                    break;
                case "Notes":
                    foreach (string value in new[] { "Remark", "Remarks", "Comments" }) yield return value;
                    break;
            }
        }

        private static string NormalizeHeader(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (char ch in value.Trim())
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
            }

            return builder.ToString();
        }
    }
}
