using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ReportExplorerSmokeTests
    {
        public static IEnumerable<string> RunAll()
        {
            using (var report = new ReportForm())
            {
                SetField(report, "_initialRefreshQueued", true);
                SetField(report, "_refreshing", true);
                report.LoadProfitabilityPreviewForVisualTest();

                DataGridView grid = GetField<DataGridView>(report, "_detailGrid");
                Chart chart = GetField<Chart>(report, "_explorerChart");
                TextBox search = GetField<TextBox>(report, "_txtResultSearch");
                CheckBox includeIncomplete = GetField<CheckBox>(report, "_chkIncludeIncompleteCosts");
                FlowLayoutPanel library = GetField<FlowLayoutPanel>(report, "_reportLibrary");

                Ensure(grid.Rows.Count == 5, "The profitability preview did not bind all sample rows.");
                Ensure(chart.Series["Revenue"].Points.Count == 12, "The monthly profitability chart did not bind the financial-year trend.");
                string profitabilityHover = ChartHoverService.HoverTextForTest(chart, chart.Series["Revenue"], chart.Series["Revenue"].Points[0]);
                Ensure(!string.IsNullOrWhiteSpace(profitabilityHover) && profitabilityHover.Contains("Exact value:") && profitabilityHover.Contains("Calculation:"), "The profitability graph no longer previews its exact value and calculation on hover.");
                SetField(report, "_currentReportIndex", 11);
                var relationshipPoint = new DataPoint(0d, 94d) { AxisLabel = "Invoices" };
                string relationshipHover = ChartHoverService.HoverTextForTest(chart, chart.Series[0], relationshipPoint);
                Ensure(!string.IsNullOrWhiteSpace(relationshipHover) && relationshipHover.Contains("94%") && relationshipHover.Contains("populated relationship keys / child rows x 100"), "The Relationship Health graph no longer previews its mathematical calculation on hover.");
                Invoke(report, "UpdateExplorerChartCalculationPreview", InvokeResult<object>(report, "BuildExplorerChartHoverContent", chart.Series[0], relationshipPoint));
                Label calculationPreview = GetField<Label>(report, "_lblChartCalculationPreview");
                Ensure(calculationPreview.Text.Contains("Exact value: 94%") && calculationPreview.Text.Contains("Calculation: populated relationship keys / child rows x 100"), "The graph card no longer keeps the calculation preview visible in the report surface.");
                SetField(report, "_currentReportIndex", 9);
                Ensure(library.Controls.Count >= 16, "The categorized report library is incomplete.");
                yield return "Reports explorer binds the report library, financial summary, trend chart, calculation hover preview, and preview table.";

                for (int reportIndex = 0; reportIndex < 12; reportIndex++)
                {
                    Invoke(report, "SelectReport", reportIndex);
                    Ensure(grid.Columns.Count > 0, "Report library item " + reportIndex + " did not bind a report schema.");
                    DataGridViewRow previewRow;
                    bool temporaryRow = grid.Rows.Count == 0;
                    if (temporaryRow)
                    {
                        int rowIndex = grid.Rows.Add();
                        previewRow = grid.Rows[rowIndex];
                        foreach (DataGridViewCell cell in previewRow.Cells)
                            cell.Value = grid.Columns[cell.ColumnIndex].HeaderText + " sample";
                    }
                    else
                    {
                        previewRow = grid.Rows[0];
                    }

                    byte[] previewPdf = InvokeResult<byte[]>(report, "BuildReportRowPreviewPdf", previewRow);
                    EnsurePdf(previewPdf, "Report library item " + reportIndex + " did not create a PDF row preview.");
                    if (temporaryRow)
                        grid.Rows.Remove(previewRow);
                }
                Invoke(report, "SelectReport", 9);
                Ensure(grid.Rows.Count == 5, "Returning to Job profitability did not restore its preview.");
                yield return "Every report-library destination opens a working report schema and creates a read-only PDF row preview.";

                search.Text = "Bluejet";
                Ensure(grid.Rows.Count == 1, "Result search did not narrow the profitability preview.");
                search.Clear();
                includeIncomplete.Checked = false;
                Ensure(grid.Rows.Count == 4, "The incomplete-cost filter did not exclude the incomplete job.");
                includeIncomplete.Checked = true;
                Ensure(grid.Rows.Count == 5, "The incomplete-cost filter did not restore the excluded job.");
                yield return "Reports explorer search and incomplete-cost filters update the live preview.";

                string outputDirectory = Path.Combine(Path.GetTempPath(), "ServoERP-ReportExplorerSmoke");
                Directory.CreateDirectory(outputDirectory);
                string csv = Path.Combine(outputDirectory, "job-profitability.csv");
                string xlsx = Path.Combine(outputDirectory, "job-profitability.xlsx");
                Invoke(report, "WriteCurrentGridExport", csv);
                Invoke(report, "WriteCurrentGridExport", xlsx);
                Ensure(new FileInfo(csv).Length > 40, "CSV report export was empty.");
                Ensure(new FileInfo(xlsx).Length > 1000, "Excel report export was empty.");
                File.Delete(csv);
                File.Delete(xlsx);
                Directory.Delete(outputDirectory, false);
                yield return "Reports explorer exports the visible report to CSV and Excel.";
            }
        }

        private static T GetField<T>(object instance, string name) where T : class
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            T value = field == null ? null : field.GetValue(instance) as T;
            if (value == null) throw new InvalidOperationException("Missing Reports explorer field: " + name);
            return value;
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Missing Reports explorer field: " + name);
            field.SetValue(instance, value);
        }

        private static void Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Missing Reports explorer method: " + name);
            method.Invoke(instance, args);
        }

        private static T InvokeResult<T>(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Missing Reports explorer method: " + name);
            return (T)method.Invoke(instance, args);
        }

        private static void EnsurePdf(byte[] content, string message)
        {
            Ensure(content != null && content.Length > 1000, message);
            Ensure(content[0] == (byte)'%' && content[1] == (byte)'P' && content[2] == (byte)'D' && content[3] == (byte)'F', message);
        }

        private static void Ensure(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
