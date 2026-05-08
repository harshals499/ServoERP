using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HVAC_Pro_Desktop.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HVAC_Pro_Desktop.Services
{
    public static class EmployeeSalarySlipService
    {
        public static async Task<string> GenerateSalarySlipPdfAsync(
            Employee employee,
            EmployeeSalaryProfileDto salaryProfile,
            List<SalaryAdvance> advances,
            string monthLabel)
        {
            if (employee == null)
                throw new ArgumentNullException(nameof(employee));
            if (salaryProfile == null)
                throw new ArgumentNullException(nameof(salaryProfile));

            string folder = Path.Combine(@"C:\HVAC_PRO_MSE\PAYSLIPS", DateTime.Today.Year.ToString(), DateTime.Today.ToString("MM"));
            Directory.CreateDirectory(folder);

            string safeName = Sanitize(employee.EmployeeCode + "_" + employee.Name);
            string htmlPath = Path.Combine(folder, safeName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".html");
            string pdfPath = Path.Combine(folder, safeName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".pdf");

            File.WriteAllText(htmlPath, BuildHtml(employee, salaryProfile, advances, monthLabel), Encoding.UTF8);

            if (Application.OpenForms.Count == 0)
                throw new InvalidOperationException("The salary slip PDF requires the application UI context.");

            var tcs = new TaskCompletionSource<string>();
            Control uiContext = Application.OpenForms[0];
            uiContext.BeginInvoke((Action)(() =>
            {
                Form host = new Form
                {
                    Width = 900,
                    Height = 1200,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-32000, -32000),
                    FormBorderStyle = FormBorderStyle.None
                };

                WebView2 browser = new WebView2 { Dock = DockStyle.Fill, Visible = false };
                host.Controls.Add(browser);

                host.Shown += async (s, e) =>
                {
                    try
                    {
                        await browser.EnsureCoreWebView2Async(null);
                        browser.NavigationCompleted += async (sender, args) =>
                        {
                            if (!args.IsSuccess)
                            {
                                tcs.TrySetException(new InvalidOperationException("Could not render salary slip HTML."));
                                host.Close();
                                return;
                            }

                            try
                            {
                                await browser.CoreWebView2.PrintToPdfAsync(pdfPath);
                                tcs.TrySetResult(pdfPath);
                            }
                            catch (Exception ex)
                            {
                                tcs.TrySetException(ex);
                            }
                            finally
                            {
                                host.Close();
                            }
                        };
                        browser.Source = new Uri(htmlPath);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                        host.Close();
                    }
                };

                host.FormClosed += (s, e) =>
                {
                    browser.Dispose();
                    host.Dispose();
                };

                host.Show();
            }));

            return await tcs.Task;
        }

        private static string BuildHtml(Employee employee, EmployeeSalaryProfileDto salaryProfile, List<SalaryAdvance> advances, string monthLabel)
        {
            decimal advanceTotal = 0m;
            foreach (SalaryAdvance advance in advances ?? new List<SalaryAdvance>())
                advanceTotal += advance.AdvanceAmount;

            decimal gross = salaryProfile.GrossSalary;
            decimal totalDeductions = salaryProfile.PFDeduction + salaryProfile.ESICDeduction + advanceTotal;
            decimal net = gross - totalDeductions;

            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>Salary Slip</title>
<style>
body{font-family:'Segoe UI',Arial,sans-serif;margin:24px;color:#1A1A1A;}
.wrap{border:1px solid #E8E8E8;padding:24px;}
.head{display:flex;justify-content:space-between;align-items:flex-start;border-bottom:1px solid #E8E8E8;padding-bottom:16px;margin-bottom:16px;}
.title{font-size:22px;font-weight:700;}
.muted{color:#6B6B6B;font-size:12px;}
table{width:100%;border-collapse:collapse;margin-top:12px;}
th,td{padding:8px 10px;border-bottom:1px solid #F0F0F0;text-align:left;font-size:12px;}
th{background:#F7F7F7;}
.box{margin-top:18px;padding:14px;background:#F7F7F7;border:1px solid #E8E8E8;}
.amt{font-weight:700;font-size:22px;color:#1D9E75;}
</style>
</head>
<body>
<div class='wrap'>
<div class='head'>
<div>
<div class='title'>New Client</div>
<div class='muted'>Salary Slip</div>
</div>
<div class='muted'>Month: " + Html(monthLabel) + @"</div>
</div>
<table>
<tr><th>Employee</th><td>" + Html(employee.Name) + @"</td><th>Code</th><td>" + Html(employee.EmployeeCode) + @"</td></tr>
<tr><th>Designation</th><td>" + Html(employee.Designation) + @"</td><th>Department</th><td>" + Html(employee.Department) + @"</td></tr>
</table>
<table>
<tr><th colspan='2'>Earnings</th><th colspan='2'>Deductions</th></tr>
<tr><td>Basic Salary</td><td>" + Html(IndiaFormatHelper.FormatCurrency(salaryProfile.BasicSalary)) + @"</td><td>PF Deduction</td><td>" + Html(IndiaFormatHelper.FormatCurrency(salaryProfile.PFDeduction)) + @"</td></tr>
<tr><td>HRA</td><td>" + Html(IndiaFormatHelper.FormatCurrency(salaryProfile.HRA)) + @"</td><td>ESIC Deduction</td><td>" + Html(IndiaFormatHelper.FormatCurrency(salaryProfile.ESICDeduction)) + @"</td></tr>
<tr><td>Allowances</td><td>" + Html(IndiaFormatHelper.FormatCurrency(salaryProfile.Allowances)) + @"</td><td>Salary Advances</td><td>" + Html(IndiaFormatHelper.FormatCurrency(advanceTotal)) + @"</td></tr>
<tr><td><b>Gross Salary</b></td><td><b>" + Html(IndiaFormatHelper.FormatCurrency(gross)) + @"</b></td><td><b>Total Deductions</b></td><td><b>" + Html(IndiaFormatHelper.FormatCurrency(totalDeductions)) + @"</b></td></tr>
</table>
<div class='box'>
<div class='muted'>Net Pay</div>
<div class='amt'>" + Html(IndiaFormatHelper.FormatCurrency(net)) + @"</div>
</div>
</div>
</body>
</html>";
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(" ", "_");
        }

        private static string Html(string value)
        {
            return System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }
    }
}
