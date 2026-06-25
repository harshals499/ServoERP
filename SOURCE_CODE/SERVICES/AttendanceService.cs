using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Data.SqlClient;
using Dapper;
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
            List<AttendanceRecord> records = _repo.GetAttendanceRecordsForMonth(employeeId, month, year);
            return MergeLegacyAttendance(records, GetLegacyAttendanceRecords(employeeId, month, year));
        }

        public List<AttendanceRecord> GetMonthlyAttendanceRecords(int month, int year)
        {
            List<AttendanceRecord> records = _repo.GetAttendanceRecordsForMonth(month, year);
            return MergeLegacyAttendance(records, GetLegacyAttendanceRecords(null, month, year));
        }

        public void SaveAttendanceRecord(AttendanceRecord record)
        {
            _repo.UpsertAttendanceRecord(record);
        }

        public ServiceResult<int> ClearEmployeeMonth(int employeeId, int month, int year)
        {
            if (employeeId <= 0)
                return ServiceResult<int>.Fail("Select an employee before clearing attendance.");
            int removed = _repo.DeleteAttendanceRecordsForEmployeeMonth(employeeId, month, year);
            SessionManager.LogAction("DELETE", "Attendance", employeeId, "Cleared employee attendance for " + month + "/" + year);
            return ServiceResult<int>.Ok(removed, removed + " attendance record(s) cleared.");
        }

        public ServiceResult<int> ClearMonth(int month, int year)
        {
            int removed = _repo.DeleteAttendanceRecordsForMonth(month, year);
            SessionManager.LogAction("DELETE", "Attendance", 0, "Cleared attendance month " + month + "/" + year);
            return ServiceResult<int>.Ok(removed, removed + " attendance record(s) cleared.");
        }

        public AttendanceSourceReconciliation GetSourceReconciliation(int month, int year)
        {
            using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(conn, "AttendanceService.GetSourceReconciliation");
                int authoritativeRows = CountTableRows(conn, "AttendanceRecords", "AttendanceDate", month, year);
                int legacyRows = CountTableRows(conn, "EmployeeAttendance", "AttendanceDate", month, year);
                int legacyOnlyRows = CountLegacyOnlyRows(conn, month, year);
                return new AttendanceSourceReconciliation
                {
                    Month = month,
                    Year = year,
                    AuthoritativeTable = "AttendanceRecords",
                    LegacyTable = "EmployeeAttendance",
                    AuthoritativeRows = authoritativeRows,
                    LegacyRows = legacyRows,
                    LegacyOnlyRows = legacyOnlyRows
                };
            }
        }

        public string GetSourceReconciliationBanner(int month, int year)
        {
            AttendanceSourceReconciliation reconciliation = GetSourceReconciliation(month, year);
            return reconciliation.RequiresReview
                ? "Attendance reconciliation: payroll uses AttendanceRecords; " + reconciliation.LegacyOnlyRows.ToString("N0") + " legacy EmployeeAttendance row(s) are not yet saved into AttendanceRecords for " + new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture) + ". Review and save before final close."
                : string.Empty;
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

        private static int CountTableRows(SqlConnection conn, string tableName, string dateColumn, int month, int year)
        {
            const string tableExistsSql = "SELECT COUNT(1) FROM sys.tables WHERE name = @tableName;";
            if (conn.ExecuteScalar<int>(tableExistsSql, new { tableName }) == 0)
                return 0;

            string sql = "SELECT COUNT(1) FROM dbo." + tableName + " WHERE MONTH(" + dateColumn + ") = @month AND YEAR(" + dateColumn + ") = @year;";
            return conn.ExecuteScalar<int>(sql, new { month, year });
        }

        private static int CountLegacyOnlyRows(SqlConnection conn, int month, int year)
        {
            const string tableExistsSql = "SELECT COUNT(1) FROM sys.tables WHERE name = @tableName;";
            if (conn.ExecuteScalar<int>(tableExistsSql, new { tableName = "EmployeeAttendance" }) == 0 ||
                conn.ExecuteScalar<int>(tableExistsSql, new { tableName = "AttendanceRecords" }) == 0)
            {
                return 0;
            }

            const string sql = @"
                SELECT COUNT(1)
                FROM dbo.EmployeeAttendance legacy
                WHERE legacy.AttendanceDate IS NOT NULL
                  AND MONTH(legacy.AttendanceDate) = @month
                  AND YEAR(legacy.AttendanceDate) = @year
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.AttendanceRecords currentRows
                      WHERE currentRows.EmployeeId = legacy.EmployeeID
                        AND currentRows.AttendanceDate = legacy.AttendanceDate
                  );";
            return conn.ExecuteScalar<int>(sql, new { month, year });
        }

        private static List<AttendanceRecord> MergeLegacyAttendance(List<AttendanceRecord> currentRows, List<AttendanceRecord> legacyRows)
        {
            List<AttendanceRecord> merged = currentRows ?? new List<AttendanceRecord>();
            if (legacyRows == null || legacyRows.Count == 0)
                return merged;

            HashSet<string> existingKeys = new HashSet<string>(
                merged.Select(row => BuildAttendanceKey(row.EmployeeId, row.AttendanceDate)));

            foreach (AttendanceRecord legacyRow in legacyRows)
            {
                string key = BuildAttendanceKey(legacyRow.EmployeeId, legacyRow.AttendanceDate);
                if (existingKeys.Add(key))
                    merged.Add(legacyRow);
            }

            return merged
                .OrderBy(row => row.EmployeeId)
                .ThenBy(row => row.AttendanceDate)
                .ToList();
        }

        private static string BuildAttendanceKey(int employeeId, DateTime attendanceDate)
        {
            return employeeId.ToString(CultureInfo.InvariantCulture) + "|" + attendanceDate.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static List<AttendanceRecord> GetLegacyAttendanceRecords(int? employeeId, int month, int year)
        {
            using (SqlConnection conn = DatabaseConnectionFactory.CreateConnection())
            {
                DatabaseConnectionFactory.Open(conn, "AttendanceService.GetLegacyAttendanceRecords");
                const string tableExistsSql = "SELECT COUNT(1) FROM sys.tables WHERE name = @tableName;";
                if (conn.ExecuteScalar<int>(tableExistsSql, new { tableName = "EmployeeAttendance" }) == 0)
                    return new List<AttendanceRecord>();

                const string sql = @"
                    SELECT
                        legacy.AttendanceID AS AttendanceId,
                        legacy.EmployeeID AS EmployeeId,
                        CAST(legacy.AttendanceDate AS date) AS AttendanceDate,
                        COALESCE(NULLIF(LTRIM(RTRIM(legacy.Status)), ''), 'Present') AS Status,
                        CAST(0 AS decimal(4,2)) AS OvertimeHours,
                        'Legacy EmployeeAttendance row' AS Notes
                    FROM dbo.EmployeeAttendance legacy
                    WHERE legacy.AttendanceDate IS NOT NULL
                      AND MONTH(legacy.AttendanceDate) = @month
                      AND YEAR(legacy.AttendanceDate) = @year
                      AND (@employeeId IS NULL OR legacy.EmployeeID = @employeeId)
                    ORDER BY legacy.EmployeeID, legacy.AttendanceDate;";

                return conn.Query<AttendanceRecord>(sql, new { employeeId, month, year }).ToList();
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }
    }

    public sealed class AttendanceSourceReconciliation
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string AuthoritativeTable { get; set; }
        public string LegacyTable { get; set; }
        public int AuthoritativeRows { get; set; }
        public int LegacyRows { get; set; }
        public int LegacyOnlyRows { get; set; }
        public bool RequiresReview => LegacyOnlyRows > 0;
    }
}
