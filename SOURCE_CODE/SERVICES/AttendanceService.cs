using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Data.SqlClient;
using HVAC_Pro_Desktop.DAL;
using HVAC_Pro_Desktop.Models;

namespace HVAC_Pro_Desktop.Services
{
    public class AttendanceService
    {
        private readonly PayrollRepository _repo = new PayrollRepository();
        private readonly EmployeeService _employeeService = new EmployeeService();
        private readonly SettingsService _settingsService = new SettingsService();

        public AttendanceSummaryDto GetMonthlyAttendanceSummary(int employeeId, int month, int year)
        {
            return _repo.GetAttendanceSummary(employeeId, month, year, GetConfiguredWorkWeek());
        }

        public AttendanceSummaryDto GetMonthlyAttendanceSummary(int employeeId, int month, int year, SqlConnection conn, SqlTransaction tx)
        {
            return _repo.GetAttendanceSummary(employeeId, month, year, GetConfiguredWorkWeek(), conn, tx);
        }

        public List<AttendanceRecord> GetMonthlyAttendanceRecords(int employeeId, int month, int year)
        {
            return _repo.GetAttendanceRecordsForMonth(employeeId, month, year);
        }

        public List<AttendanceRecord> GetMonthlyAttendanceRecords(int month, int year)
        {
            return _repo.GetAttendanceRecordsForMonth(month, year);
        }

        public void SaveAttendanceRecord(AttendanceRecord record)
        {
            _repo.UpsertAttendanceRecord(record);
        }

        public ServiceResult<int> BulkMarkAttendance(int month, int year, List<int> employeeIds, string status)
        {
            if (employeeIds == null || employeeIds.Count == 0)
                return ServiceResult<int>.Fail("No employees selected.");

            int saved = 0;
            DateTime start = new DateTime(year, month, 1);
            DateTime end = start.AddMonths(1).AddDays(-1);
            int workWeek = GetConfiguredWorkWeek();

            foreach (int employeeId in employeeIds.Distinct())
            {
                for (DateTime day = start; day <= end; day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Sunday)
                        continue;
                    if (workWeek == 5 && day.DayOfWeek == DayOfWeek.Saturday)
                        continue;

                    _repo.UpsertAttendanceRecord(new AttendanceRecord
                    {
                        EmployeeId = employeeId,
                        AttendanceDate = day,
                        Status = status,
                        OvertimeHours = 0m
                    });
                    saved++;
                }
            }

            return ServiceResult<int>.Ok(saved, "Attendance marked successfully.");
        }

        public ServiceResult<int> ImportAttendanceFromCsv(string filePath)
        {
            if (!File.Exists(filePath))
                return ServiceResult<int>.Fail("Attendance CSV file not found.");

            int imported = 0;
            Dictionary<string, Employee> employees = _employeeService.GetAll()
                .GroupBy(e => Normalize(e.Name))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (string rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine ?? string.Empty;
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Employee", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] cells = line.Split(',');
                if (cells.Length < 3)
                    continue;

                string employeeName = cells[0].Trim();
                string dateText = cells[1].Trim();
                string status = cells[2].Trim();
                string overtimeText = cells.Length > 3 ? cells[3].Trim() : "0";

                if (!employees.TryGetValue(Normalize(employeeName), out Employee employee))
                    continue;

                if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime attendanceDate) &&
                    !DateTime.TryParse(dateText, out attendanceDate))
                {
                    continue;
                }

                decimal.TryParse(overtimeText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal overtimeHours);

                _repo.UpsertAttendanceRecord(new AttendanceRecord
                {
                    EmployeeId = employee.EmployeeID,
                    AttendanceDate = attendanceDate.Date,
                    Status = string.IsNullOrWhiteSpace(status) ? "Present" : status,
                    OvertimeHours = overtimeHours
                });
                imported++;
            }

            return ServiceResult<int>.Ok(imported, "Attendance imported.");
        }

        private int GetConfiguredWorkWeek()
        {
            if (int.TryParse(_settingsService.Get("AttendanceWorkWeek", "6"), out int configured) && (configured == 5 || configured == 6))
                return configured;
            return 6;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
