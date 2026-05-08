using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public static ServiceResult<T> Ok(T data, string message = "")
        {
            return new ServiceResult<T> { Success = true, Data = data, Message = message ?? string.Empty };
        }

        public static ServiceResult<T> Fail(string message)
        {
            return new ServiceResult<T> { Success = false, Message = message ?? "Operation failed." };
        }
    }

    public class SalaryStructure
    {
        public int StructureId { get; set; }
        public int EmployeeId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal DA { get; set; }
        public decimal HRA { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal ConveyanceAllowance { get; set; }
        public decimal MedicalAllowance { get; set; }
        public decimal LTA { get; set; }
        public decimal OtherAllowances { get; set; }
        public decimal GrossSalary { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class PayrollRun
    {
        public int PayrollRunId { get; set; }
        public int PayrollMonth { get; set; }
        public int PayrollYear { get; set; }
        public DateTime RunDate { get; set; }
        public string Status { get; set; }
        public string ProcessedBy { get; set; }
        public decimal TotalGross { get; set; }
        public decimal TotalNetPay { get; set; }
        public decimal TotalEPFEmployee { get; set; }
        public decimal TotalEPFEmployer { get; set; }
        public decimal TotalESIEmployee { get; set; }
        public decimal TotalESIEmployer { get; set; }
        public decimal TotalTDS { get; set; }
        public decimal TotalPT { get; set; }
        public string Notes { get; set; }
    }

    public class PayrollEntry
    {
        public int EntryId { get; set; }
        public int PayrollRunId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Designation { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal DA { get; set; }
        public decimal HRA { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal ConveyanceAllowance { get; set; }
        public decimal MedicalAllowance { get; set; }
        public decimal LTA { get; set; }
        public decimal OtherAllowances { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal Bonus { get; set; }
        public decimal GrossSalary { get; set; }
        public int WorkingDaysInMonth { get; set; }
        public decimal DaysPresent { get; set; }
        public decimal DaysAbsent { get; set; }
        public decimal LeaveDays { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal EPFEmployee { get; set; }
        public decimal ESIEmployee { get; set; }
        public decimal TDSDeducted { get; set; }
        public decimal ProfessionalTax { get; set; }
        public decimal LoanDeduction { get; set; }
        public decimal AdvanceDeduction { get; set; }
        public decimal OtherDeductions { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal EPFEmployer { get; set; }
        public decimal EPSEmployer { get; set; }
        public decimal ESIEmployer { get; set; }
        public decimal NetSalary { get; set; }
        public string TaxRegime { get; set; }
        public string UAN { get; set; }
        public string ESICNumber { get; set; }
        public string BankAccount { get; set; }
        public string BankIFSC { get; set; }
        public bool PayslipGenerated { get; set; }
        public string PayslipPath { get; set; }
        public int PayrollMonth { get; set; }
        public int PayrollYear { get; set; }
    }

    public class TDSCalculation
    {
        public int TDSCalcId { get; set; }
        public int EmployeeId { get; set; }
        public string FinancialYear { get; set; }
        public string TaxRegime { get; set; }
        public decimal EstimatedAnnualIncome { get; set; }
        public decimal Chapter6ADeductions { get; set; }
        public decimal StandardDeduction { get; set; }
        public decimal TaxableIncome { get; set; }
        public decimal AnnualTaxLiability { get; set; }
        public decimal MonthlyTDS { get; set; }
        public decimal TDSPaidToDate { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ProfessionalTaxSlab
    {
        public int SlabId { get; set; }
        public string StateCode { get; set; }
        public string StateName { get; set; }
        public decimal MinSalary { get; set; }
        public decimal? MaxSalary { get; set; }
        public decimal MonthlyPT { get; set; }
        public DateTime EffectiveFrom { get; set; }
    }

    public class EmployeeLoan
    {
        public int LoanId { get; set; }
        public int EmployeeId { get; set; }
        public decimal LoanAmount { get; set; }
        public decimal MonthlyDeduction { get; set; }
        public DateTime LoanDate { get; set; }
        public decimal RemainingBalance { get; set; }
        public string Purpose { get; set; }
        public bool IsActive { get; set; }
    }

    public class SalaryAdvance
    {
        public int AdvanceId { get; set; }
        public int EmployeeId { get; set; }
        public decimal AdvanceAmount { get; set; }
        public DateTime AdvanceDate { get; set; }
        public int RecoveryMonth { get; set; }
        public int RecoveryYear { get; set; }
        public bool Recovered { get; set; }
    }

    public class StatutoryPayment
    {
        public int PaymentId { get; set; }
        public int PayrollRunId { get; set; }
        public string PaymentType { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }

    public class AttendanceRecord
    {
        public int AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string Status { get; set; }
        public decimal OvertimeHours { get; set; }
        public string Notes { get; set; }
    }

    public class LeaveType
    {
        public int LeaveTypeId { get; set; }
        public string LeaveTypeName { get; set; }
        public bool PaidLeave { get; set; }
        public int AnnualQuota { get; set; }
    }

    public class LeaveBalance
    {
        public int BalanceId { get; set; }
        public int EmployeeId { get; set; }
        public int LeaveTypeId { get; set; }
        public int Year { get; set; }
        public decimal Opening { get; set; }
        public decimal Accrued { get; set; }
        public decimal Used { get; set; }
        public decimal Closing { get; set; }
        public string LeaveTypeName { get; set; }
    }

    public class AttendanceSummaryDto
    {
        public int WorkingDaysInMonth { get; set; }
        public decimal DaysPresent { get; set; }
        public decimal DaysAbsent { get; set; }
        public decimal LeaveDays { get; set; }
        public decimal OvertimeHours { get; set; }
    }

    public class PayrollSummaryDto
    {
        public int TotalEmployees { get; set; }
        public decimal TotalGross { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalEmployerLiability { get; set; }
        public List<StatutoryPayment> StatutoryPayments { get; set; } = new List<StatutoryPayment>();
    }

    public class PayrollDashboardSnapshot
    {
        public PayrollRun LastRun { get; set; }
        public int DaysUntilNextPayroll { get; set; }
        public int OverdueStatutoryPayments { get; set; }
        public string NextPayrollLabel { get; set; }
    }

    public class PayrollImportReport
    {
        public int FilesProcessed { get; set; }
        public int EmployeesMatched { get; set; }
        public int NewEmployeesCreated { get; set; }
        public int SalaryStructuresImported { get; set; }
        public int PayrollEntriesImported { get; set; }
        public int AttendanceRecordsImported { get; set; }
        public int LoansImported { get; set; }
        public int AdvancesImported { get; set; }
        public int ErrorsEncountered { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
