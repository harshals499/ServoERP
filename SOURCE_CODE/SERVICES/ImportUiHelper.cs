using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HVAC_Pro_Desktop.Services
{
    public static class ImportUiHelper
    {
        private static readonly ExcelImportService Service = new ExcelImportService();

        public static void RunImport(ExcelImportModule module, IWin32Window owner = null)
        {
            try
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                    dialog.Title = "Import " + module;
                    if (dialog.ShowDialog(owner) != DialogResult.OK)
                        return;

                    ExcelImportResult result = Service.Import(module, dialog.FileName);
                    var builder = new StringBuilder();
                    builder.AppendLine("Import complete.");
                    builder.AppendLine("Successfully imported: " + result.SuccessCount + " rows");
                    builder.AppendLine("Skipped (errors): " + result.SkippedCount + " rows");
                    if (result.Errors.Any())
                    {
                        builder.AppendLine();
                        builder.AppendLine("Errors:");
                        foreach (string error in result.Errors.Take(20))
                            builder.AppendLine(error);
                    }

                    MessageBox.Show(owner, builder.ToString(), "Import Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("ImportUiHelper.RunImport", ex);
                MessageBox.Show(owner, "Import failed. " + ex.Message, "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
                MessageBox.Show(owner, "Template generation failed. " + ex.Message, "Template Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
