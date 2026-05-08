using System;
using System.Collections.Generic;
using System.Linq;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;
using HVAC_Pro_Desktop.Models.Validation;
using HVAC_Pro_Desktop.Services.Validation;

namespace HVAC_Pro_Desktop.Services
{
    public class EmployeeService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);
        private readonly EmployeeRepository _repo = new EmployeeRepository();
        private readonly BusinessRuleEngine _businessRules = new BusinessRuleEngine();
        private readonly GlobalValidationEngine _validation = new GlobalValidationEngine();

        public List<Employee> GetAll() => AppDataCache.GetOrCreate("employees:all", CacheTtl, _repo.GetAll);
        public Employee GetById(int id) => _repo.GetById(id);
        public List<Employee> GetByClientSite(string site) => GetAll().FindAll(e => string.Equals(e.ClientSite, site, StringComparison.OrdinalIgnoreCase));
        public int GetActiveCount() => GetAll().FindAll(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase)).Count;
        public int GetActiveEmployeeCount() => _repo.GetActiveCount();

        public EmployeeDashboardStats GetDashboardStats() => _repo.GetDashboardStats();
        public List<EmployeeSummaryDto> GetEmployeeSummaries() => _repo.GetEmployeeSummaries();
        public List<EmployeeJobSummaryDto> GetEmployeeJobs(int employeeId) => _repo.GetEmployeeJobs(employeeId);
        public List<EmployeeAttendanceDayDto> GetEmployeeAttendance(int employeeId, int year, int month) => _repo.GetEmployeeAttendance(employeeId, year, month);
        public EmployeeAttendanceSummaryDto GetEmployeeAttendanceSummary(int employeeId, int year, int month) => _repo.GetEmployeeAttendanceSummary(employeeId, year, month);
        public List<EmployeeSkillDto> GetEmployeeSkills(int employeeId) => _repo.GetEmployeeSkills(employeeId);
        public List<EmployeeSkillDto> GetExpiringSkills(int days) => _repo.GetExpiringSkills(days);
        public List<EmployeeDocumentDto> GetEmployeeDocuments(int employeeId) => _repo.GetEmployeeDocuments(employeeId);
        public EmployeeDocumentDto GetDocumentById(int documentId) => _repo.GetDocumentById(documentId);
        public EmployeeSalaryProfileDto GetSalaryProfile(int employeeId) => _repo.GetSalaryProfile(employeeId);
        public string GenerateNextEmployeeCode() => _repo.GenerateNextEmployeeCode();

        public int Create(Employee employee)
        {
            SessionManager.DemandPermission("Employees", "Create");
            if (string.IsNullOrWhiteSpace(employee.EmployeeCode))
                employee.EmployeeCode = GenerateNextEmployeeCode();

            ValidateEmployeeForSave(employee);
            if (SessionManager.IsLoggedIn)
            {
                employee.CreatedByUserId = SessionManager.CurrentUser.UserId;
                employee.CreatedByName = SessionManager.CurrentUser.DisplayName;
            }

            int id = _repo.Create(employee);
            InvalidateEmployeeCache();
            SessionManager.LogAction("CREATE", "Employees", id, "Employee saved");
            return id;
        }

        public void Update(Employee employee)
        {
            SessionManager.DemandPermission("Employees", "Edit");
            ValidateEmployeeForSave(employee);
            if (SessionManager.IsLoggedIn)
            {
                employee.ModifiedByUserId = SessionManager.CurrentUser.UserId;
                employee.ModifiedByName = SessionManager.CurrentUser.DisplayName;
                employee.ModifiedDate = DateTime.Now;
            }

            _repo.Update(employee);
            InvalidateEmployeeCache();
            SessionManager.LogAction("EDIT", "Employees", employee.EmployeeID, "Employee saved");
        }

        public void SoftDelete(int employeeId)
        {
            SessionManager.DemandPermission("Employees", "Delete");
            _repo.SoftDelete(employeeId);
            InvalidateEmployeeCache();
            SessionManager.LogAction("DELETE", "Employees", employeeId, "Employee marked inactive");
        }

        public int SaveSkill(EmployeeSkillDto skill)
        {
            SessionManager.DemandPermission("Employees", "Edit");
            int id = _repo.SaveSkill(skill);
            SessionManager.LogAction("SAVE", "Employees", skill.EmployeeID, "Employee skill saved");
            return id;
        }

        public int SaveDocument(EmployeeDocumentDto document)
        {
            SessionManager.DemandPermission("Employees", "Edit");
            int id = _repo.SaveDocument(document);
            SessionManager.LogAction("UPLOAD", "Employees", document.EmployeeID, "Employee document uploaded");
            return id;
        }

        public int SaveSalaryProfile(EmployeeSalaryProfileDto profile)
        {
            SessionManager.DemandPermission("Payroll", "Edit");
            int id = _repo.SaveSalaryProfile(profile);
            SessionManager.LogAction("SAVE", "Employees", profile.EmployeeID, "Employee salary profile saved");
            return id;
        }

        public List<Employee> GetActiveTechnicians()
        {
            List<Employee> active = GetAll().FindAll(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase));
            List<Employee> technicians = active.FindAll(e =>
                (e.Designation ?? string.Empty).IndexOf("tech", StringComparison.OrdinalIgnoreCase) >= 0
                || (e.Designation ?? string.Empty).IndexOf("engineer", StringComparison.OrdinalIgnoreCase) >= 0
                || (e.Designation ?? string.Empty).IndexOf("fitter", StringComparison.OrdinalIgnoreCase) >= 0
                || (e.Designation ?? string.Empty).IndexOf("electric", StringComparison.OrdinalIgnoreCase) >= 0
                || (e.Designation ?? string.Empty).IndexOf("operator", StringComparison.OrdinalIgnoreCase) >= 0
                || (e.Designation ?? string.Empty).IndexOf("supervisor", StringComparison.OrdinalIgnoreCase) >= 0);
            return technicians.Count > 0 ? technicians : active;
        }

        private static void InvalidateEmployeeCache()
        {
            AppDataCache.RemovePrefix("employees:");
        }

        private void ValidateEmployeeForSave(Employee employee)
        {
            ValidationResult result = _businessRules.ValidateEmployee(employee);
            if (employee != null)
            {
                string code = (employee.EmployeeCode ?? string.Empty).Trim();
                string phone = (employee.Phone ?? string.Empty).Trim();
                foreach (Employee existing in GetAll() ?? new List<Employee>())
                {
                    if (existing.EmployeeID == employee.EmployeeID)
                        continue;
                    if (!string.IsNullOrWhiteSpace(code) && string.Equals((existing.EmployeeCode ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase))
                        result.Add(ValidationSeverity.Error, "Employees", "EmployeeCode", "Another employee already uses this employee code.", "Open existing employee or generate a new code.");
                    if (!string.IsNullOrWhiteSpace(phone) && string.Equals((existing.Phone ?? string.Empty).Trim(), phone, StringComparison.OrdinalIgnoreCase))
                        result.Add(ValidationSeverity.Error, "Employees", "Phone", "Another employee already uses this phone number.", "Check the technician list before creating a duplicate.");
                }
            }
            _validation.EnsureValid(result, "Employee validation failed");
        }
    }
}
