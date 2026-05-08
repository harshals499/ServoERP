using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class PayslipService
    {
        private readonly PayrollRepository _repo = new PayrollRepository();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly SettingsService _settingsService = new SettingsService();

        public ServiceResult<string> GeneratePayslip(int entryId)
        {
            try
            {
                PayrollEntry entry = _repo.GetPayrollEntryById(entryId);
                if (entry == null)
                    return ServiceResult<string>.Fail("Payroll entry not found.");

                Employee employee = _employeeService.GetById(entry.EmployeeId);
                if (employee == null)
                    return ServiceResult<string>.Fail("Employee not found.");

                string pdfFolder = PayrollFolderHelper.EnsurePayslipFolder(entry.PayrollYear, entry.PayrollMonth);
                string safeName = MakeSafeFileName((employee.Name ?? "Employee") + "_" + CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(entry.PayrollMonth) + "_" + entry.PayrollYear + ".pdf");
                string pdfPath = Path.Combine(pdfFolder, safeName);
                string html = BuildPayslipHtml(entry, employee);
                PayrollHtmlPdfExporter.ExportHtmlToPdf(html, pdfPath);
                _repo.UpdatePayslipStatus(entryId, pdfPath);
                return ServiceResult<string>.Ok(pdfPath, "Payslip generated.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayslipService.GeneratePayslip", ex);
                return ServiceResult<string>.Fail(ex.Message);
            }
        }

        public ServiceResult<List<string>> GenerateBatchPayslips(int payrollRunId)
        {
            var output = new List<string>();
            try
            {
                List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(payrollRunId);
                for (int i = 0; i < entries.Count; i++)
                {
                    IndiaComplianceLogger.Log("Payslip", "Generating payslip " + (i + 1) + " of " + entries.Count + " for " + entries[i].EmployeeName);
                    ServiceResult<string> result = GeneratePayslip(entries[i].EntryId);
                    if (result.Success && !string.IsNullOrWhiteSpace(result.Data))
                        output.Add(result.Data);
                }

                return ServiceResult<List<string>>.Ok(output, "Batch payslip generation completed.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayslipService.GenerateBatchPayslips", ex);
                return ServiceResult<List<string>>.Fail(ex.Message);
            }
        }

        private string BuildPayslipHtml(PayrollEntry entry, Employee employee)
        {
            IndiaCompanySettings company = _settingsService.GetIndiaCompanySettings();
            DateTime payDate = new DateTime(entry.PayrollYear, entry.PayrollMonth, 1).AddMonths(1).AddDays(6);
            string monthLabel = new DateTime(entry.PayrollYear, entry.PayrollMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            string bankAccount = !string.IsNullOrWhiteSpace(entry.BankAccount) ? entry.BankAccount : (!string.IsNullOrWhiteSpace(employee.BankAccountNumber) ? employee.BankAccountNumber : employee.BankAccount);
            string bankIfsc = !string.IsNullOrWhiteSpace(entry.BankIFSC) ? entry.BankIFSC : (!string.IsNullOrWhiteSpace(employee.BankIFSC) ? employee.BankIFSC : employee.IFSCCode);
            string amountWords = PayrollWordsHelper.ToIndianCurrencyWords(entry.NetSalary);
            decimal ctc = entry.GrossSalary + entry.EPFEmployer + entry.EPSEmployer + entry.ESIEmployer;

            return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<style>"
            + DocumentBranding.BuildOfficialHeaderCss()
            + @"body{font-family:'Segoe UI',sans-serif;color:#1f2937;padding:20px;font-size:12px;}
.title{text-align:center;font-size:22px;font-weight:800;letter-spacing:1px;color:#1e3a8a;margin:8px 0 16px 0;}
.meta{text-align:center;font-size:11px;color:#475569;margin-bottom:16px;}
.section{border:1px solid #cbd5e1;border-radius:10px;padding:12px;margin-bottom:14px;}
.details{width:100%;border-collapse:collapse;}
.details td{vertical-align:top;width:50%;padding:4px 8px;}
.label{font-size:10px;color:#64748b;text-transform:uppercase;letter-spacing:.4px;}
.value{font-size:12px;font-weight:600;color:#0f172a;}
.split{display:flex;gap:14px;}
.card{flex:1;border:1px solid #cbd5e1;border-radius:10px;overflow:hidden;}
.card table{width:100%;border-collapse:collapse;}
.card th{background:#e0e7ff;color:#1e3a8a;padding:8px;border-bottom:1px solid #cbd5e1;text-align:left;font-size:11px;}
.card td{padding:7px 8px;border-bottom:1px solid #e2e8f0;}
.amount{text-align:right;font-weight:600;}
.strong{font-weight:800;background:#f8fafc;}
.netbox{border:2px solid #1d4ed8;border-radius:12px;padding:14px;background:#eff6ff;margin:12px 0;}
.netbox .big{font-size:24px;font-weight:800;color:#1d4ed8;}
.small{font-size:10px;color:#64748b;}
.footer{text-align:center;font-size:10px;color:#64748b;margin-top:18px;}
</style></head><body>"
            + DocumentBranding.BuildOfficialHeaderHtml()
            + "<div class='title'>SALARY SLIP</div>"
            + "<div class='meta'>Month: " + Html(monthLabel) + " | Pay Date: " + Html(IndiaFormatHelper.FormatDate(payDate)) + "</div>"
            + "<div class='section'><table class='details'>"
            + DetailRow("Employee Name", employee.Name, "Pay Period", monthLabel)
            + DetailRow("Designation", entry.Designation, "Working Days", entry.WorkingDaysInMonth.ToString())
            + DetailRow("Department", employee.Department, "Days Present", entry.DaysPresent.ToString("0.##"))
            + DetailRow("Employee ID", employee.EmployeeCode, "Days Absent", entry.DaysAbsent.ToString("0.##"))
            + DetailRow("Date of Joining", IndiaFormatHelper.FormatDate(employee.DateOfJoining ?? employee.JoiningDate), "Leave Days", entry.LeaveDays.ToString("0.##"))
            + DetailRow("PAN", employee.PAN, "Overtime Hours", entry.OvertimeHours.ToString("0.##"))
            + DetailRow("UAN", !string.IsNullOrWhiteSpace(entry.UAN) ? entry.UAN : employee.UAN, "Tax Regime", entry.TaxRegime)
            + DetailRow("ESIC Number", !string.IsNullOrWhiteSpace(entry.ESICNumber) ? entry.ESICNumber : employee.ESICNumber, "Bank IFSC", bankIfsc)
            + DetailRow("Bank Account", MaskBankAccount(bankAccount), "State Code", employee.StateCode)
            + "</table></div>"
            + "<div class='split'>"
            + BuildEarningsTable(entry)
            + BuildDeductionsTable(entry)
            + "</div>"
            + "<div class='netbox'><div class='small'>NET TAKE HOME</div><div class='big'>" + Html(IndiaFormatHelper.FormatCurrency(entry.NetSalary)) + "</div><div class='value'>Amount in words: " + Html(amountWords) + "</div></div>"
            + "<div class='section'><div class='label'>Employer Contributions</div>"
            + "<table class='details'>"
            + DetailRow("EPF Employer (3.67%)", IndiaFormatHelper.FormatCurrency(entry.EPFEmployer), "EPS Employer (8.33%)", IndiaFormatHelper.FormatCurrency(entry.EPSEmployer))
            + DetailRow("ESI Employer (3.25%)", IndiaFormatHelper.FormatCurrency(entry.ESIEmployer), "Total CTC This Month", IndiaFormatHelper.FormatCurrency(ctc))
            + "</table></div>"
            + "<div class='footer'>This is a computer-generated payslip — HVAC PRO MSE</div>"
            + "</body></html>";
        }

        private static string BuildEarningsTable(PayrollEntry entry)
        {
            return "<div class='card'><table><tr><th>Component</th><th class='amount'>Amount</th></tr>"
                + Row("Basic Salary", entry.BasicSalary)
                + Row("Dearness Allowance", entry.DA)
                + Row("House Rent Allowance", entry.HRA)
                + Row("Conveyance Allowance", entry.ConveyanceAllowance)
                + Row("Medical Allowance", entry.MedicalAllowance)
                + Row("Special Allowance", entry.SpecialAllowance)
                + Row("LTA", entry.LTA)
                + (entry.OvertimePay > 0m ? Row("Overtime Pay", entry.OvertimePay) : string.Empty)
                + (entry.Bonus > 0m ? Row("Bonus", entry.Bonus) : string.Empty)
                + Row("Gross Earnings", entry.GrossSalary, true)
                + "</table></div>";
        }

        private static string BuildDeductionsTable(PayrollEntry entry)
        {
            return "<div class='card'><table><tr><th>Component</th><th class='amount'>Amount</th></tr>"
                + Row("EPF (Employee) 12%", entry.EPFEmployee)
                + (entry.ESIEmployee > 0m ? Row("ESI (Employee) 0.75%", entry.ESIEmployee) : string.Empty)
                + Row("Income Tax (TDS)", entry.TDSDeducted)
                + (entry.ProfessionalTax > 0m ? Row("Professional Tax", entry.ProfessionalTax) : string.Empty)
                + (entry.LoanDeduction > 0m ? Row("Loan Deduction", entry.LoanDeduction) : string.Empty)
                + (entry.AdvanceDeduction > 0m ? Row("Advance Recovery", entry.AdvanceDeduction) : string.Empty)
                + Row("Total Deductions", entry.TotalDeductions, true)
                + "</table></div>";
        }

        private static string DetailRow(string leftLabel, string leftValue, string rightLabel, string rightValue)
        {
            return "<tr><td><div class='label'>" + Html(leftLabel) + "</div><div class='value'>" + Html(leftValue) + "</div></td>"
                + "<td><div class='label'>" + Html(rightLabel) + "</div><div class='value'>" + Html(rightValue) + "</div></td></tr>";
        }

        private static string Row(string label, decimal amount, bool strong = false)
        {
            return "<tr" + (strong ? " class='strong'" : string.Empty) + "><td>" + Html(label) + "</td><td class='amount'>" + Html(IndiaFormatHelper.FormatCurrency(amount)) + "</td></tr>";
        }

        private static string MaskBankAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return string.Empty;
            string trimmed = account.Trim();
            string last4 = trimmed.Length <= 4 ? trimmed : trimmed.Substring(trimmed.Length - 4);
            return "XXXX" + last4;
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

