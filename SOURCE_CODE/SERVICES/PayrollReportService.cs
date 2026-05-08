using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class PayrollReportService
    {
        private readonly PayrollRepository _repo = new PayrollRepository();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly SettingsService _settingsService = new SettingsService();

        public ServiceResult<string> GenerateEPFECR(int payrollRunId)
        {
            try
            {
                PayrollRun run = _repo.GetPayrollRunById(payrollRunId);
                if (run == null)
                    return ServiceResult<string>.Fail("Payroll run not found.");
                List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(payrollRunId);
                string path = Path.Combine(PayrollFolderHelper.PayrollExportRoot, "EPF_ECR_" + MonthToken(run) + ".csv");
                WriteCsv(path, new[]
                {
                    "UAN","Member Name","Gross Wages","EPF Wages","EPS Wages","EPF Contribution","EPS Contribution","Employee Share","Employer Share","NCP Days","Refund"
                }, entries.Select(e => new[]
                {
                    e.UAN ?? string.Empty,
                    e.EmployeeName ?? string.Empty,
                    FormatDecimal(e.GrossSalary),
                    FormatDecimal(e.BasicSalary + e.DA),
                    FormatDecimal(e.BasicSalary + e.DA),
                    FormatDecimal(e.EPFEmployer + e.EPSEmployer),
                    FormatDecimal(e.EPSEmployer),
                    FormatDecimal(e.EPFEmployee),
                    FormatDecimal(e.EPFEmployer),
                    FormatDecimal(Math.Max(0m, e.WorkingDaysInMonth - (e.DaysPresent + e.LeaveDays))),
                    "0"
                }));
                return ServiceResult<string>.Ok(path, "EPF ECR generated.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollReportService.GenerateEPFECR", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        public ServiceResult<string> GenerateESIContribution(int payrollRunId)
        {
            try
            {
                PayrollRun run = _repo.GetPayrollRunById(payrollRunId);
                if (run == null)
                    return ServiceResult<string>.Fail("Payroll run not found.");
                List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(payrollRunId).Where(e => e.ESIEmployee > 0m || e.ESIEmployer > 0m).ToList();
                string path = Path.Combine(PayrollFolderHelper.PayrollExportRoot, "ESI_" + MonthToken(run) + ".csv");
                WriteCsv(path, new[] { "IP Number", "Employee Name", "Gross Wages", "Employee Contribution", "Employer Contribution" },
                    entries.Select(e => new[] { e.ESICNumber ?? string.Empty, e.EmployeeName ?? string.Empty, FormatDecimal(e.GrossSalary), FormatDecimal(e.ESIEmployee), FormatDecimal(e.ESIEmployer) }));
                return ServiceResult<string>.Ok(path, "ESI contribution export generated.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollReportService.GenerateESIContribution", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        public ServiceResult<string> GenerateForm24QData(int quarter, int year)
        {
            try
            {
                int[] months = QuarterMonths(quarter);
                var rows = new List<string[]>();
                foreach (int month in months)
                {
                    PayrollRun run = _repo.GetPayrollRun(month, year);
                    if (run == null)
                        continue;

                    foreach (PayrollEntry entry in _repo.GetPayrollEntriesByRun(run.PayrollRunId))
                    {
                        Employee employee = _employeeService.GetById(entry.EmployeeId);
                        rows.Add(new[]
                        {
                            employee?.PAN ?? string.Empty,
                            entry.EmployeeName ?? string.Empty,
                            FormatDecimal(entry.GrossSalary),
                            FormatDecimal(entry.TDSDeducted),
                            FormatDecimal(entry.TDSDeducted),
                            "RUN-" + run.PayrollRunId
                        });
                    }
                }

                string path = Path.Combine(PayrollFolderHelper.PayrollExportRoot, "Form24Q_Q" + quarter + "_" + year + ".csv");
                WriteCsv(path, new[] { "Employee PAN", "Name", "Total Salary", "TDS Deducted", "TDS Deposited", "Challan Reference" }, rows);
                return ServiceResult<string>.Ok(path, "Form 24Q data exported.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollReportService.GenerateForm24QData", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        public ServiceResult<string> GeneratePTRegister(int month, int year)
        {
            try
            {
                PayrollRun run = _repo.GetPayrollRun(month, year);
                if (run == null)
                    return ServiceResult<string>.Fail("Payroll run not found.");

                DateTime dueDate = new DateTime(year, month, 1).AddMonths(1).AddDays(14);
                List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(run.PayrollRunId);
                string path = Path.Combine(PayrollFolderHelper.PayrollExportRoot, "PT_" + MonthToken(run) + ".csv");
                WriteCsv(path, new[] { "Employee", "State", "Gross", "PT Deducted", "Due Date" },
                    entries.Select(e =>
                    {
                        Employee employee = _employeeService.GetById(e.EmployeeId);
                        return new[]
                        {
                            e.EmployeeName ?? string.Empty,
                            employee?.StateCode ?? string.Empty,
                            FormatDecimal(e.GrossSalary),
                            FormatDecimal(e.ProfessionalTax),
                            IndiaFormatHelper.FormatDate(dueDate)
                        };
                    }));
                return ServiceResult<string>.Ok(path, "PT register exported.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollReportService.GeneratePTRegister", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        public ServiceResult<string> GeneratePayrollRegister(int payrollRunId)
        {
            try
            {
                PayrollRun run = _repo.GetPayrollRunById(payrollRunId);
                if (run == null)
                    return ServiceResult<string>.Fail("Payroll run not found.");
                List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(payrollRunId);
                string path = Path.Combine(PayrollFolderHelper.PayrollExportRoot, "PayrollRegister_" + MonthToken(run) + ".csv");
                WriteCsv(path, new[] { "Employee", "Basic", "DA", "HRA", "Special", "Gross", "EPF", "ESI", "TDS", "PT", "Total Deductions", "Net Pay" },
                    entries.Select(e => new[]
                    {
                        e.EmployeeName ?? string.Empty,
                        FormatDecimal(e.BasicSalary),
                        FormatDecimal(e.DA),
                        FormatDecimal(e.HRA),
                        FormatDecimal(e.SpecialAllowance),
                        FormatDecimal(e.GrossSalary),
                        FormatDecimal(e.EPFEmployee),
                        FormatDecimal(e.ESIEmployee),
                        FormatDecimal(e.TDSDeducted),
                        FormatDecimal(e.ProfessionalTax),
                        FormatDecimal(e.TotalDeductions),
                        FormatDecimal(e.NetSalary)
                    }));
                return ServiceResult<string>.Ok(path, "Payroll register exported.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollReportService.GeneratePayrollRegister", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        public ServiceResult<string> GenerateForm16(int employeeId, string financialYear)
        {
            try
            {
                Employee employee = _employeeService.GetById(employeeId);
                if (employee == null)
                    return ServiceResult<string>.Fail("Employee not found.");

                List<PayrollEntry> entries = _repo.GetPayrollEntriesByEmployee(employeeId)
                    .Where(e => IndiaFinancialYearHelper.GetFinancialYearCode(new DateTime(e.PayrollYear, e.PayrollMonth, 1)) == financialYear)
                    .ToList();
                TDSCalculation tds = _repo.GetTdsCalculation(employeeId, financialYear);
                IndiaCompanySettings company = _settingsService.GetIndiaCompanySettings();
                string folder = Path.Combine(PayrollFolderHelper.PayrollExportRoot, "Form16");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, MakeSafeFileName((employee.Name ?? "Employee") + "_Form16_" + financialYear + ".pdf"));

                string html = @"<!DOCTYPE html><html><head><meta charset='utf-8'/><style>"
                    + DocumentBranding.BuildOfficialHeaderCss()
                    + @"body{font-family:'Segoe UI',sans-serif;padding:20px;color:#1f2937;}
.title{text-align:center;font-size:22px;font-weight:800;margin:10px 0;color:#1e3a8a;}
.section{border:1px solid #cbd5e1;border-radius:10px;padding:12px;margin-bottom:14px;}
table{width:100%;border-collapse:collapse;}th,td{border:1px solid #e2e8f0;padding:7px 8px;text-align:left;font-size:11px;}
th{background:#eff6ff;color:#1e3a8a;}
</style></head><body>"
                    + DocumentBranding.BuildOfficialHeaderHtml()
                    + "<div class='title'>FORM 16 - " + Html(financialYear) + "</div>"
                    + "<div class='section'><strong>Part A</strong><br/>Employer TAN: " + Html(company.TAN) + "<br/>Employee PAN: " + Html(employee.PAN) + "<br/>Employee Name: " + Html(employee.Name) + "</div>"
                    + "<div class='section'><strong>Part B - Salary and Tax Summary</strong><table><tr><th>Month</th><th>Gross Salary</th><th>TDS Deducted</th><th>Net Salary</th></tr>"
                    + string.Join(string.Empty, entries.Select(e => "<tr><td>" + Html(new DateTime(e.PayrollYear, e.PayrollMonth, 1).ToString("MMM yyyy")) + "</td><td>" + Html(IndiaFormatHelper.FormatCurrency(e.GrossSalary)) + "</td><td>" + Html(IndiaFormatHelper.FormatCurrency(e.TDSDeducted)) + "</td><td>" + Html(IndiaFormatHelper.FormatCurrency(e.NetSalary)) + "</td></tr>"))
                    + "</table></div>"
                    + "<div class='section'>Estimated Annual Income: " + Html(IndiaFormatHelper.FormatCurrency(tds?.EstimatedAnnualIncome ?? 0m))
                    + "<br/>Taxable Income: " + Html(IndiaFormatHelper.FormatCurrency(tds?.TaxableIncome ?? 0m))
                    + "<br/>Annual Tax Liability: " + Html(IndiaFormatHelper.FormatCurrency(tds?.AnnualTaxLiability ?? 0m))
                    + "<br/>Monthly TDS: " + Html(IndiaFormatHelper.FormatCurrency(tds?.MonthlyTDS ?? 0m))
                    + "</div></body></html>";

                PayrollHtmlPdfExporter.ExportHtmlToPdf(html, path);
                return ServiceResult<string>.Ok(path, "Form 16 generated.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollReportService.GenerateForm16", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        private static void WriteCsv(string path, IEnumerable<string> headers, IEnumerable<string[]> rows)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? PayrollFolderHelper.PayrollExportRoot);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
            foreach (string[] row in rows)
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static string FormatDecimal(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string MonthToken(PayrollRun run)
        {
            if (run == null)
                return "Unknown_Run";
            return new DateTime(run.PayrollYear, run.PayrollMonth, 1).ToString("MMM_yyyy", CultureInfo.InvariantCulture);
        }

        private static int[] QuarterMonths(int quarter)
        {
            switch (quarter)
            {
                case 1: return new[] { 4, 5, 6 };
                case 2: return new[] { 7, 8, 9 };
                case 3: return new[] { 10, 11, 12 };
                default: return new[] { 1, 2, 3 };
            }
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c.ToString(), string.Empty);
            return value.Replace(" ", "_");
        }

        private static string Html(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
