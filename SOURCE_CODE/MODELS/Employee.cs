using System;
using System.Collections.Generic;

namespace HVAC_Pro_Desktop.Models
{
    public class Employee
    {
        public int EmployeeID { get; set; }
        public string EmployeeCode { get; set; }
        public string Name { get; set; }
        public string Designation { get; set; }
        public string Department { get; set; }
        public string ClientSite { get; set; }
        public string Phone { get; set; }
        public DateTime? JoiningDate { get; set; }
        public DateTime? DateOfJoining { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string EmploymentType { get; set; }
        public string PAN { get; set; }
        public string AadhaarLast4 { get; set; }
        public string UAN { get; set; }
        public string UANNumber { get; set; }
        public string ESICNumber { get; set; }
        public string EPFNumber { get; set; }
        public string TaxRegime { get; set; }
        public string StateCode { get; set; }
        public bool EPFApplicable { get; set; }
        public bool ESIApplicable { get; set; }
        public bool PTApplicable { get; set; }
        public string BankAccountNumber { get; set; }
        public string BankAccount { get; set; }
        public string BankIFSC { get; set; }
        public string IFSCCode { get; set; }
        public string BankName { get; set; }
        public string MaritalStatus { get; set; }
        public string Address { get; set; }
        public string NatureOfWork { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal GrossSalary { get; set; }
        public byte[] Photo { get; set; }
        public string AadhaarNumber { get; set; }
        public string PANNumber { get; set; }
        public string BloodGroup { get; set; }
        public string EmergencyContactName { get; set; }
        public string EmergencyContactPhone { get; set; }
        public DateTime? ProbationEndDate { get; set; }
        public DateTime? ConfirmationDate { get; set; }
        public DateTime? LastWorkingDay { get; set; }
        public bool IsRehire { get; set; }
        public string WhatsAppNumber { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
        public int? ModifiedByUserId { get; set; }
        public string ModifiedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public override string ToString() => EmployeeCode + " - " + Name;
    }

    public class EmployeeDashboardStats
    {
        public int TotalEmployees { get; set; }
        public int ActiveToday { get; set; }
        public int OnDuty { get; set; }
        public int OnLeave { get; set; }
    }

    public class EmployeeSummaryDto
    {
        public int EmployeeID { get; set; }
        public string EmployeeCode { get; set; }
        public string Name { get; set; }
        public string Designation { get; set; }
        public string Department { get; set; }
        public string ClientSite { get; set; }
        public string Phone { get; set; }
        public string Status { get; set; }
        public bool CheckedInToday { get; set; }
        public bool OnLeaveToday { get; set; }
        public bool IsInactive { get; set; }
    }

    public class EmployeeJobSummaryDto
    {
        public int JobID { get; set; }
        public string JobNumber { get; set; }
        public string Site { get; set; }
        public string JobType { get; set; }
        public DateTime AssignedDate { get; set; }
        public string Status { get; set; }
        public DateTime? ClosedDate { get; set; }
        public int ClosureDays { get; set; }
    }

    public class EmployeeAttendanceDayDto
    {
        public int AttendanceID { get; set; }
        public DateTime AttendanceDate { get; set; }
        public TimeSpan? CheckInTime { get; set; }
        public TimeSpan? CheckOutTime { get; set; }
        public decimal HoursWorked { get; set; }
        public string Status { get; set; }
        public decimal? CheckInLatitude { get; set; }
        public decimal? CheckInLongitude { get; set; }
    }

    public class EmployeeAttendanceSummaryDto
    {
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int LateDays { get; set; }
        public int LeaveDays { get; set; }
    }

    public class EmployeeSkillDto
    {
        public int SkillID { get; set; }
        public int EmployeeID { get; set; }
        public string SkillName { get; set; }
        public string CertificationNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsExpired { get; set; }
        public bool ExpiresWithinThirtyDays { get; set; }
        public string EmployeeName { get; set; }
    }

    public class EmployeeDocumentDto
    {
        public int DocumentID { get; set; }
        public int EmployeeID { get; set; }
        public string DocumentType { get; set; }
        public string FileName { get; set; }
        public byte[] FileData { get; set; }
        public DateTime UploadedOn { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class EmployeeSalaryProfileDto
    {
        public int SalaryID { get; set; }
        public int EmployeeID { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HRA { get; set; }
        public decimal Allowances { get; set; }
        public decimal PFDeduction { get; set; }
        public decimal ESICDeduction { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public decimal GrossSalary => BasicSalary + HRA + Allowances;
        public decimal NetSalary => GrossSalary - PFDeduction - ESICDeduction;
    }

    public class EmployeeOverviewDetailDto
    {
        public Employee Employee { get; set; }
        public List<EmployeeJobSummaryDto> Jobs { get; set; } = new List<EmployeeJobSummaryDto>();
        public List<EmployeeSkillDto> Skills { get; set; } = new List<EmployeeSkillDto>();
        public List<EmployeeDocumentDto> Documents { get; set; } = new List<EmployeeDocumentDto>();
        public EmployeeSalaryProfileDto SalaryProfile { get; set; }
    }
}
