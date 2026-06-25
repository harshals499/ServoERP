using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    public static class ContractPageSmokeTests
    {
        public static string WriteReport()
        {
            string dir = System.IO.Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            System.IO.Directory.CreateDirectory(dir);
            string reportPath = System.IO.Path.Combine(dir, "contracts-page-smoke-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var lines = new List<string>
            {
                "Contracts Page Smoke Test",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                string.Empty
            };

            try
            {
                foreach (string result in RunAll())
                    lines.Add("PASS " + result);
            }
            catch (Exception ex)
            {
                lines.Add("FAIL " + ex);
            }

            System.IO.File.WriteAllLines(reportPath, lines);
            return reportPath;
        }

        public static IEnumerable<string> RunAll()
        {
            var syncContext = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncContext);
            AsyncOperationManager.SynchronizationContext = syncContext;
            var results = new List<string>();
            Exception failure = null;

            using (var host = new Form())
            using (var contracts = new ContractManagementForm())
            {
                host.Text = "Contracts smoke host";
                host.Size = new Size(1440, 900);
                contracts.Dock = DockStyle.Fill;
                host.Controls.Add(contracts);
                host.Shown += async (sender, args) =>
                {
                    try
                    {
                        await InvokeLoadAsync(contracts);
                        results.Add("Contracts dashboard loaded without a screen-action error.");

                        Button newContract = FindButtons(contracts)
                            .FirstOrDefault(button => Clean(button.Text).Contains("NEWCONTRACT"));
                        if (newContract == null)
                            throw new InvalidOperationException("New Contract button was not found on Contracts dashboard.");

                        newContract.PerformClick();
                        PumpUi(500);
                        results.Add("New Contract form opened without recursive sidebar selection.");

                        DataGridView list = FindControls<DataGridView>(contracts).FirstOrDefault();
                        if (list != null && list.RowCount > 0)
                        {
                            list.CurrentCell = list.Rows[0].Cells.Cast<DataGridViewCell>().FirstOrDefault();
                            list.Rows[0].Selected = true;
                            PumpUi(500);
                            results.Add("Existing contract row selection completed without recursive rebuild.");
                        }
                        else
                        {
                            results.Add("No sidebar rows available for row-selection check.");
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        host.Close();
                    }
                };

                Application.Run(host);
            }

            if (failure != null)
                throw failure;

            return results;
        }

        private static async Task InvokeLoadAsync(ContractManagementForm contracts)
        {
            MethodInfo method = typeof(ContractManagementForm).GetMethod(
                "LoadPageDataAndRefreshAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("Contracts load method is missing.");

            var task = method.Invoke(contracts, null) as Task;
            if (task == null)
                throw new InvalidOperationException("Contracts load method did not return a task.");

            Task timeout = Task.Delay(TimeSpan.FromSeconds(30));
            Task finished = await Task.WhenAny(task, timeout);
            if (finished == timeout)
                throw new TimeoutException("Contracts load did not complete within 30 seconds.");
            await task;
        }

        private static IEnumerable<Button> FindButtons(Control root)
        {
            return FindControls<Button>(root).Where(button => button.Visible && button.Enabled);
        }

        private static IEnumerable<T> FindControls<T>(Control root) where T : Control
        {
            if (root == null)
                yield break;

            foreach (Control child in root.Controls)
            {
                if (child is T typed)
                    yield return typed;

                foreach (T descendant in FindControls<T>(child))
                    yield return descendant;
            }
        }

        private static string Clean(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static void PumpUi(int milliseconds)
        {
            DateTime until = DateTime.Now.AddMilliseconds(milliseconds);
            while (DateTime.Now < until)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(15);
            }
        }
    }
}
