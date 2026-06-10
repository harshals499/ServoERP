using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using HVAC_Pro_Desktop.UI;

namespace HVAC_Pro_Desktop.Tests
{
    /// <summary>Times how long AddAMCForm takes to load (construction + OnLoad/shared frontend polish).</summary>
    public static class AddAMCFormTimingTest
    {
        private const long MaxAcceptableShowMs = 5000;

        public static string WriteReport()
        {
            string dir = Path.Combine(@"C:\HVAC_PRO_MSE", "TEST_RESULTS");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "amcform-timing-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

            var lines = new List<string>
            {
                "AddAMCForm Open Timing",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ""
            };

            try
            {
                Stopwatch construct = Stopwatch.StartNew();
                using (var form = new AddAMCForm())
                {
                    construct.Stop();
                    lines.Add("PASS Construct AddAMCForm completed in " + construct.ElapsedMilliseconds + " ms");

                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-32000, -32000);
                    form.ShowInTaskbar = false;

                    Stopwatch show = Stopwatch.StartNew();
                    form.Show();
                    PumpUi(250);
                    show.Stop();
                    string label = "Show AddAMCForm (OnLoad + shared frontend polish) completed in " + show.ElapsedMilliseconds + " ms";
                    if (show.ElapsedMilliseconds > MaxAcceptableShowMs)
                        lines.Add("FAIL " + label + " (exceeds " + MaxAcceptableShowMs + " ms threshold)");
                    else
                        lines.Add("PASS " + label);

                    form.Close();
                }
            }
            catch (Exception ex)
            {
                lines.Add("FAIL AddAMCForm timing aborted: " + ex);
            }

            File.WriteAllLines(path, lines);
            return path;
        }

        private static void PumpUi(int milliseconds)
        {
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            while (sw.ElapsedMilliseconds < milliseconds);
        }
    }
}
