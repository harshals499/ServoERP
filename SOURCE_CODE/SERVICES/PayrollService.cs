using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class PayrollService
    {
        private readonly DatabaseManager _db = new DatabaseManager();
        private readonly PayrollRepository _repo = new PayrollRepository();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly AttendanceService _attendanceService = new AttendanceService();
        private readonly SettingsService _settingsService = new SettingsService();

        public ServiceResult<PayrollRun> ProcessMonthlyPayroll(int month, int year)
        {
            try
            {
                SessionManager.DemandPermission("Payroll", "Create");
                PayrollFolderHelper.EnsureFolders();
                PayrollRun existing = _repo.GetPayrollRun(month, year);
                if (existing != null && string.Equals(existing.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                    return ServiceResult<PayrollRun>.Fail("Payroll is already locked for this month.");
                if (existing != null && string.Equals(existing.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    return ServiceResult<PayrollRun>.Fail("A completed payroll run already exists for this month.");

                DateTime periodStart = new DateTime(year, month, 1);
                List<Employee> employees = _employeeService.GetAll()
                    .Where(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name)
                    .ToList();

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        PayrollRun run = existing ?? new PayrollRun
                        {
                            PayrollMonth = month,
                            PayrollYear = year,
                            Status = "Processing",
                            ProcessedBy = SessionManager.CurrentUser?.DisplayName
                        };

                        if (existing == null)
                        {
                            run.PayrollRunId = _repo.CreatePayrollRun(conn, tx, run);
                        }
                        else
                        {
                            run.PayrollRunId = existing.PayrollRunId;
                            run.Status = "Processing";
                            _repo.DeletePayrollDataForRun(conn, tx, existing.PayrollRunId);
                            _repo.UpdatePayrollRun(conn, tx, run);
                        }

                        var entries = new List<PayrollEntry>();
                        foreach (Employee employee in employees)
                        {
                            PayrollEntry entry = BuildPayrollEntry(employee, month, year, run.PayrollRunId, conn, tx, 0);
                            entries.Add(entry);
                        }

                        ApplyRunTotals(run, entries);
                        run.Status = "Completed";
                        _repo.UpdatePayrollRun(conn, tx, run);
                        CreateStatutoryPayments(run, conn, tx);

                        tx.Commit();
                        IndiaComplianceLogger.Log("Payroll", "Completed payroll run for " + month.ToString("00") + "/" + year + " | Employees=" + entries.Count);
                        SessionManager.LogAction("PROCESS", "Payroll", run.PayrollRunId, "Processed monthly payroll for " + month.ToString("00") + "/" + year);
                        return ServiceResult<PayrollRun>.Ok(run, "Payroll processed successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollService.ProcessMonthlyPayroll", ex);
                return ServiceResult<PayrollRun>.Fail(ex.Message);
            }
        }

        public ServiceResult<PayrollEntry> CalculateEmployeePayroll(int employeeId, int month, int year)
        {
            try
            {
                SessionManager.DemandPermission("Payroll", "Create");
                Employee employee = _employeeService.GetById(employeeId);
                if (employee == null)
                    return ServiceResult<PayrollEntry>.Fail("Employee not found.");

                PayrollRun run = _repo.GetPayrollRun(month, year) ?? new PayrollRun
                {
                    PayrollMonth = month,
                    PayrollYear = year,
                    Status = "Draft",
                    ProcessedBy = SessionManager.CurrentUser?.DisplayName
                };

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        if (run.PayrollRunId <= 0)
                            run.PayrollRunId = _repo.CreatePayrollRun(conn, tx, run);

                        PayrollEntry entry = BuildPayrollEntry(employee, month, year, run.PayrollRunId, conn, tx, 0);
                        tx.Commit();
                        RecomputeRunTotals(run.PayrollRunId);
                        return ServiceResult<PayrollEntry>.Ok(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollService.CalculateEmployeePayroll", ex);
                return ServiceResult<PayrollEntry>.Fail(ex.Message);
            }
        }

        public PayrollRun GetPayrollRun(int month, int year) => _repo.GetPayrollRun(month, year);
        public PayrollRun GetPayrollRunById(int payrollRunId) => _repo.GetPayrollRunById(payrollRunId);
        public List<PayrollEntry> GetPayrollEntriesByRun(int payrollRunId) => _repo.GetPayrollEntriesByRun(payrollRunId);
        public List<PayrollEntry> GetPayrollEntriesByEmployee(int employeeId) => _repo.GetPayrollEntriesByEmployee(employeeId);
        public List<SalaryStructure> GetSalaryStructures(int employeeId) => _repo.GetSalaryStructures(employeeId);
        public List<StatutoryPayment> GetStatutoryPaymentsByMonth(int month, int year) => _repo.GetStatutoryPaymentsByMonth(month, year);
        public List<TDSCalculation> GetTdsCalculationsByEmployee(int employeeId) => _repo.GetTdsCalculationsByEmployee(employeeId);
        public List<EmployeeLoan> GetLoansByEmployee(int employeeId) => _repo.GetLoansByEmployee(employeeId);
        public List<SalaryAdvance> GetAdvancesByEmployee(int employeeId) => _repo.GetAdvancesByEmployee(employeeId);
        public List<LeaveBalance> GetLeaveBalances(int employeeId, int year) => _repo.GetLeaveBalances(employeeId, year);

        public ServiceResult<int> SaveSalaryStructure(SalaryStructure structure)
        {
            try
            {
                SessionManager.DemandPermission("Payroll", "Edit");
                decimal gross = structure.BasicSalary + structure.DA + structure.HRA + structure.SpecialAllowance
                    + structure.ConveyanceAllowance + structure.MedicalAllowance + structure.LTA + structure.OtherAllowances;
                if (gross > 0m)
                {
                    decimal ratio = ((structure.BasicSalary + structure.DA) / gross) * 100m;
                    if (ratio < 50m)
                        return ServiceResult<int>.Fail("Basic + DA must be at least 50% of Gross Salary as per Labour Code 2025. Current: " + ratio.ToString("0.##", CultureInfo.InvariantCulture) + "%");
                }

                int id = _repo.SaveSalaryStructure(structure);
                SessionManager.LogAction("SAVE", "Payroll", id, "Saved salary structure");
                return ServiceResult<int>.Ok(id, "Salary structure saved.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollService.SaveSalaryStructure", ex);
                return ServiceResult<int>.Fail(ex.Message);
            }
        }

        public PayrollSummaryDto GetPayrollSummary(int month, int year)
        {
            PayrollRun run = _repo.GetPayrollRun(month, year);
            if (run == null)
                return new PayrollSummaryDto();

            List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(run.PayrollRunId);
            List<StatutoryPayment> statutory = _repo.GetStatutoryPaymentsByRun(run.PayrollRunId);
            return new PayrollSummaryDto
            {
                TotalEmployees = entries.Count,
                TotalGross = entries.Sum(e => e.GrossSalary),
                TotalNet = entries.Sum(e => e.NetSalary),
                TotalEmployerLiability = entries.Sum(e => e.EPFEmployer + e.EPSEmployer + e.ESIEmployer + e.ProfessionalTax),
                StatutoryPayments = statutory
            };
        }

        public ServiceResult<bool> LockPayroll(int payrollRunId)
        {
            try
            {
                SessionManager.DemandPermission("Payroll", "Edit");

                PayrollRun run = _repo.GetPayrollRunById(payrollRunId);
                if (run == null)
                    return ServiceResult<bool>.Fail("Payroll run not found.");

                run.Status = "Locked";
                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        _repo.UpdatePayrollRun(conn, tx, run);
                        tx.Commit();
                    }
                }

                SessionManager.LogAction("LOCK", "Payroll", payrollRunId, "Locked payroll run");
                return ServiceResult<bool>.Ok(true, "Payroll locked.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollService.LockPayroll", ex);
                return ServiceResult<bool>.Fail(ex.Message);
            }
        }

        public ServiceResult<PayrollEntry> RecalculateSingleEmployee(int entryId)
        {
            try
            {
                SessionManager.DemandPermission("Payroll", "Edit");
                PayrollEntry existing = _repo.GetPayrollEntryById(entryId);
                if (existing == null)
                    return ServiceResult<PayrollEntry>.Fail("Payroll entry not found.");

                PayrollRun run = _repo.GetPayrollRunById(existing.PayrollRunId);
                if (run == null)
                    return ServiceResult<PayrollEntry>.Fail("Payroll run not found.");
                if (string.Equals(run.Status, "Locked", StringComparison.OrdinalIgnoreCase))
                    return ServiceResult<PayrollEntry>.Fail("Payroll is locked.");

                Employee employee = _employeeService.GetById(existing.EmployeeId);
                if (employee == null)
                    return ServiceResult<PayrollEntry>.Fail("Employee not found.");

                using (SqlConnection conn = _db.GetConnection())
                {
                    conn.Open();
                    using (SqlTransaction tx = conn.BeginTransaction())
                    {
                        PayrollEntry entry = BuildPayrollEntry(employee, run.PayrollMonth, run.PayrollYear, run.PayrollRunId, conn, tx, entryId);
                        tx.Commit();
                        RecomputeRunTotals(run.PayrollRunId);
                        return ServiceResult<PayrollEntry>.Ok(entry, "Employee payroll recalculated.");
                    }
                }
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollService.RecalculateSingleEmployee", ex);
                return ServiceResult<PayrollEntry>.Fail(ex.Message);
            }
        }

        public ServiceResult<bool> MarkStatutoryPaymentPaid(int paymentId, DateTime paidDate, string referenceNumber)
        {
            try
            {
                SessionManager.DemandPermission("Payroll", "Edit");
                _repo.MarkStatutoryPaymentPaid(paymentId, paidDate, referenceNumber);
                SessionManager.LogAction("PAY", "Payroll", paymentId, "Marked statutory payment as paid");
                return ServiceResult<bool>.Ok(true, "Payment updated.");
            }
            catch (Exception ex)
            {
                AppRuntime.LogException("PayrollService.MarkStatutoryPaymentPaid", ex);
                return ServiceResult<bool>.Fail(ex.Message);
            }
        }

        public int SaveEmployeeLoan(EmployeeLoan loan)
        {
            SessionManager.DemandPermission("Payroll", "Edit");
            return _repo.SaveEmployeeLoan(loan);
        }

        public int SaveSalaryAdvance(SalaryAdvance advance)
        {
            SessionManager.DemandPermission("Payroll", "Edit");
            return _repo.SaveSalaryAdvance(advance);
        }

        public PayrollDashboardSnapshot GetDashboardSnapshot()
        {
            PayrollRun lastRun = _repo.GetLatestPayrollRun();
            DateTime today = DateTime.Today;
            DateTime nextPayrollMonth = new DateTime(today.Month == 12 ? today.Year + 1 : today.Year, today.Month == 12 ? 1 : today.Month + 1, 1);
            return new PayrollDashboardSnapshot
            {
                LastRun = lastRun,
                OverdueStatutoryPayments = _repo.GetOverdueStatutoryPaymentCount(),
                DaysUntilNextPayroll = Math.Max(0, (nextPayrollMonth.AddDays(6) - today).Days),
                NextPayrollLabel = nextPayrollMonth.ToString("MMMM yyyy")
            };
        }

        private PayrollEntry BuildPayrollEntry(Employee employee, int month, int year, int payrollRunId, SqlConnection conn, SqlTransaction tx, int existingEntryId)
        {
            DateTime periodStart = new DateTime(year, month, 1);
            SalaryStructure structure = _repo.GetActiveSalaryStructure(employee.EmployeeID, periodStart, conn, tx) ?? BuildFallbackStructure(employee);
            AttendanceSummaryDto attendance = _attendanceService.GetMonthlyAttendanceSummary(employee.EmployeeID, month, year, conn, tx);
            int workingDays = attendance.WorkingDaysInMonth <= 0 ? 26 : attendance.WorkingDaysInMonth;
            decimal payableDays = attendance.DaysPresent + attendance.LeaveDays;
            if (payableDays <= 0m)
                payableDays = attendance.DaysPresent > 0m ? attendance.DaysPresent : workingDays;

            decimal ratio = workingDays <= 0 ? 1m : Math.Min(1m, Math.Max(0m, payableDays / workingDays));

            decimal basic = Round2(structure.BasicSalary * ratio);
            decimal da = Round2(structure.DA * ratio);
            decimal hra = Round2(structure.HRA * ratio);
            decimal special = Round2(structure.SpecialAllowance * ratio);
            decimal conveyance = Round2(structure.ConveyanceAllowance * ratio);
            decimal medical = Round2(structure.MedicalAllowance * ratio);
            decimal lta = Round2(structure.LTA * ratio);
            decimal otherAllowances = Round2(structure.OtherAllowances * ratio);
            decimal overtimeHours = attendance.OvertimeHours;
            decimal overtimePay = basic <= 0m ? 0m : Round2((basic / 26m / 8m) * 2m * overtimeHours);
            decimal bonus = 0m;
            decimal gross = Round2(basic + da + hra + special + conveyance + medical + lta + otherAllowances + overtimePay + bonus);

            decimal epfEmployee = 0m;
            decimal employerEpf = 0m;
            decimal employerEps = 0m;
            decimal epfBase = basic + da;
            if (employee.EPFApplicable)
            {
                epfEmployee = Round2(Math.Min(epfBase * 0.12m, 1800m));
                employerEps = Round2(Math.Min(epfBase * 0.0833m, 1250m));
                employerEpf = Round2(Math.Max(0m, Math.Min(epfBase * 0.12m, 1800m) - employerEps));
            }

            decimal esiEmployee = 0m;
            decimal esiEmployer = 0m;
            if (employee.ESIApplicable && gross <= 21000m)
            {
                esiEmployee = Round2(gross * 0.0075m);
                esiEmployer = Round2(gross * 0.0325m);
            }

            decimal professionalTax = 0m;
            if (employee.PTApplicable && !string.IsNullOrWhiteSpace(employee.StateCode))
            {
                professionalTax = _repo.GetProfessionalTaxAmount(employee.StateCode, gross, periodStart, conn, tx);
                if (string.Equals(employee.StateCode, "MH", StringComparison.OrdinalIgnoreCase) && month == 2 && professionalTax > 0m)
                    professionalTax = 300m;
            }

            decimal tds = CalculateMonthlyTds(employee, gross, month, year, conn, tx);

            List<EmployeeLoan> loans = _repo.GetActiveLoans(employee.EmployeeID, conn, tx);
            decimal loanDeduction = 0m;
            foreach (EmployeeLoan loan in loans)
            {
                decimal deduction = Math.Min(loan.MonthlyDeduction, loan.RemainingBalance);
                loanDeduction += deduction;
                _repo.ApplyLoanRecovery(conn, tx, loan.LoanId, deduction);
            }

            List<SalaryAdvance> advances = _repo.GetSalaryAdvancesForMonth(employee.EmployeeID, month, year, conn, tx);
            decimal advanceDeduction = 0m;
            foreach (SalaryAdvance advance in advances)
            {
                advanceDeduction += advance.AdvanceAmount;
                _repo.MarkAdvanceRecovered(conn, tx, advance.AdvanceId);
            }

            decimal otherDeductions = 0m;
            decimal totalDeductions = Round2(epfEmployee + esiEmployee + tds + professionalTax + loanDeduction + advanceDeduction + otherDeductions);
            decimal net = Round2(gross - totalDeductions);

            var entry = new PayrollEntry
            {
                EntryId = existingEntryId,
                PayrollRunId = payrollRunId,
                EmployeeId = employee.EmployeeID,
                EmployeeName = employee.Name,
                Designation = employee.Designation,
                BasicSalary = basic,
                DA = da,
                HRA = hra,
                SpecialAllowance = special,
                ConveyanceAllowance = conveyance,
                MedicalAllowance = medical,
                LTA = lta,
                OtherAllowances = otherAllowances,
                OvertimePay = overtimePay,
                Bonus = bonus,
                GrossSalary = gross,
                WorkingDaysInMonth = workingDays,
                DaysPresent = attendance.DaysPresent,
                DaysAbsent = attendance.DaysAbsent,
                LeaveDays = attendance.LeaveDays,
                OvertimeHours = overtimeHours,
                EPFEmployee = epfEmployee,
                ESIEmployee = esiEmployee,
                TDSDeducted = tds,
                ProfessionalTax = professionalTax,
                LoanDeduction = loanDeduction,
                AdvanceDeduction = advanceDeduction,
                OtherDeductions = otherDeductions,
                TotalDeductions = totalDeductions,
                EPFEmployer = employerEpf,
                EPSEmployer = employerEps,
                ESIEmployer = esiEmployer,
                NetSalary = net,
                TaxRegime = string.IsNullOrWhiteSpace(employee.TaxRegime) ? "New" : employee.TaxRegime,
                UAN = !string.IsNullOrWhiteSpace(employee.UAN) ? employee.UAN : employee.UANNumber,
                ESICNumber = employee.ESICNumber,
                BankAccount = !string.IsNullOrWhiteSpace(employee.BankAccountNumber) ? employee.BankAccountNumber : employee.BankAccount,
                BankIFSC = !string.IsNullOrWhiteSpace(employee.BankIFSC) ? employee.BankIFSC : employee.IFSCCode,
                PayslipGenerated = false
            };

            if (existingEntryId > 0)
                _repo.UpdatePayrollEntry(conn, tx, entry);
            else
                entry.EntryId = _repo.InsertPayrollEntry(conn, tx, entry);

            return entry;
        }

        private SalaryStructure BuildFallbackStructure(Employee employee)
        {
            decimal basic = employee.BasicSalary;
            decimal gross = employee.GrossSalary;
            if (basic <= 0m && gross > 0m)
                basic = Round2(gross * 0.5m);
            if (gross <= 0m)
                gross = basic;

            return new SalaryStructure
            {
                EmployeeId = employee.EmployeeID,
                EffectiveFrom = employee.DateOfJoining ?? employee.JoiningDate ?? DateTime.Today,
                BasicSalary = basic,
                DA = 0m,
                HRA = Round2(Math.Max(0m, gross * 0.2m)),
                SpecialAllowance = Round2(Math.Max(0m, gross - basic - (gross * 0.2m))),
                ConveyanceAllowance = 0m,
                MedicalAllowance = 0m,
                LTA = 0m,
                OtherAllowances = 0m,
                IsActive = true
            };
        }

        private decimal CalculateMonthlyTds(Employee employee, decimal gross, int month, int year, SqlConnection conn, SqlTransaction tx)
        {
            DateTime periodStart = new DateTime(year, month, 1);
            DateTime fyStart = IndiaFinancialYearHelper.GetFinancialYearStart(periodStart);
            DateTime fyEnd = IndiaFinancialYearHelper.GetFinancialYearEnd(periodStart);
            string financialYear = IndiaFinancialYearHelper.GetFinancialYearCode(periodStart);
            string regime = string.Equals(employee.TaxRegime, "Old", StringComparison.OrdinalIgnoreCase) ? "Old" : "New";

            TDSCalculation existing = _repo.GetTdsCalculation(employee.EmployeeID, financialYear, conn, tx);
            decimal previousGross = _repo.GetGrossPaidInFinancialYear(employee.EmployeeID, fyStart, periodStart, conn, tx);
            decimal previousTds = _repo.GetTdsPaidInFinancialYear(employee.EmployeeID, fyStart, periodStart, conn, tx);
            int remainingMonths = Math.Max(1, ((fyEnd.Year - year) * 12) + fyEnd.Month - month + 1);

            decimal estimatedAnnualIncome = previousGross + (gross * remainingMonths);
            decimal chapter6A = regime == "Old" ? (existing?.Chapter6ADeductions ?? 0m) : 0m;
            decimal standardDeduction = regime == "Old" ? 50000m : 75000m;
            decimal taxableIncome = Math.Max(0m, estimatedAnnualIncome - standardDeduction - chapter6A);
            decimal annualTax = regime == "Old" ? CalculateOldRegimeTax(taxableIncome) : CalculateNewRegimeTax(taxableIncome);
            annualTax = Round2(annualTax * 1.04m);
            decimal monthlyTds = Math.Max(0m, Round2((annualTax - previousTds) / remainingMonths));

            _repo.UpsertTdsCalculation(conn, tx, new TDSCalculation
            {
                EmployeeId = employee.EmployeeID,
                FinancialYear = financialYear,
                TaxRegime = regime,
                EstimatedAnnualIncome = estimatedAnnualIncome,
                Chapter6ADeductions = chapter6A,
                StandardDeduction = standardDeduction,
                TaxableIncome = taxableIncome,
                AnnualTaxLiability = annualTax,
                MonthlyTDS = monthlyTds,
                TDSPaidToDate = previousTds
            });

            return monthlyTds;
        }

        private decimal CalculateNewRegimeTax(decimal taxableIncome)
        {
            return CalculateSlabTax(taxableIncome, new[]
            {
                new TaxSlab(400000m, 0m),
                new TaxSlab(800000m, 0.05m),
                new TaxSlab(1200000m, 0.10m),
                new TaxSlab(1600000m, 0.15m),
                new TaxSlab(2000000m, 0.20m),
                new TaxSlab(2400000m, 0.25m),
                new TaxSlab(decimal.MaxValue, 0.30m)
            });
        }

        private decimal CalculateOldRegimeTax(decimal taxableIncome)
        {
            return CalculateSlabTax(taxableIncome, new[]
            {
                new TaxSlab(250000m, 0m),
                new TaxSlab(500000m, 0.05m),
                new TaxSlab(1000000m, 0.20m),
                new TaxSlab(decimal.MaxValue, 0.30m)
            });
        }

        private decimal CalculateSlabTax(decimal income, TaxSlab[] slabs)
        {
            decimal previousLimit = 0m;
            decimal tax = 0m;
            foreach (TaxSlab slab in slabs)
            {
                if (income <= previousLimit)
                    break;

                decimal slabIncome = Math.Min(income, slab.UpperLimit) - previousLimit;
                if (slabIncome > 0m)
                    tax += slabIncome * slab.Rate;

                previousLimit = slab.UpperLimit;
            }

            return tax;
        }

        private void CreateStatutoryPayments(PayrollRun run, SqlConnection conn, SqlTransaction tx)
        {
            DateTime nextMonth = new DateTime(run.PayrollYear, run.PayrollMonth, 1).AddMonths(1);
            _repo.InsertStatutoryPayment(conn, tx, new StatutoryPayment { PayrollRunId = run.PayrollRunId, PaymentType = "EPF", Amount = run.TotalEPFEmployee + run.TotalEPFEmployer, DueDate = new DateTime(nextMonth.Year, nextMonth.Month, 15), Status = "Pending" });
            _repo.InsertStatutoryPayment(conn, tx, new StatutoryPayment { PayrollRunId = run.PayrollRunId, PaymentType = "ESI", Amount = run.TotalESIEmployee + run.TotalESIEmployer, DueDate = new DateTime(nextMonth.Year, nextMonth.Month, 15), Status = "Pending" });
            _repo.InsertStatutoryPayment(conn, tx, new StatutoryPayment { PayrollRunId = run.PayrollRunId, PaymentType = "TDS", Amount = run.TotalTDS, DueDate = new DateTime(nextMonth.Year, nextMonth.Month, 7), Status = "Pending" });
            _repo.InsertStatutoryPayment(conn, tx, new StatutoryPayment { PayrollRunId = run.PayrollRunId, PaymentType = "PT", Amount = run.TotalPT, DueDate = new DateTime(nextMonth.Year, nextMonth.Month, 15), Status = "Pending" });
        }

        private void ApplyRunTotals(PayrollRun run, List<PayrollEntry> entries)
        {
            run.TotalGross = Round2(entries.Sum(e => e.GrossSalary));
            run.TotalNetPay = Round2(entries.Sum(e => e.NetSalary));
            run.TotalEPFEmployee = Round2(entries.Sum(e => e.EPFEmployee));
            run.TotalEPFEmployer = Round2(entries.Sum(e => e.EPFEmployer + e.EPSEmployer));
            run.TotalESIEmployee = Round2(entries.Sum(e => e.ESIEmployee));
            run.TotalESIEmployer = Round2(entries.Sum(e => e.ESIEmployer));
            run.TotalTDS = Round2(entries.Sum(e => e.TDSDeducted));
            run.TotalPT = Round2(entries.Sum(e => e.ProfessionalTax));
        }

        private void RecomputeRunTotals(int payrollRunId)
        {
            PayrollRun run = _repo.GetPayrollRunById(payrollRunId);
            if (run == null)
                return;

            List<PayrollEntry> entries = _repo.GetPayrollEntriesByRun(payrollRunId);
            ApplyRunTotals(run, entries);

            using (SqlConnection conn = _db.GetConnection())
            {
                conn.Open();
                using (SqlTransaction tx = conn.BeginTransaction())
                {
                    _repo.DeleteStatutoryPaymentsForRun(conn, tx, payrollRunId);
                    CreateStatutoryPayments(run, conn, tx);
                    _repo.UpdatePayrollRun(conn, tx, run);
                    tx.Commit();
                }
            }
        }

        private static decimal Round2(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private struct TaxSlab
        {
            public TaxSlab(decimal upperLimit, decimal rate)
            {
                UpperLimit = upperLimit;
                Rate = rate;
            }

            public decimal UpperLimit { get; private set; }
            public decimal Rate { get; private set; }
        }
    }
}
